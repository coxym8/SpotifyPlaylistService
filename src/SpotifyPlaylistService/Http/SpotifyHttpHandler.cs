using System.Net;
using System.Net.Http.Headers;

namespace SpotifyPlaylistService.Http
{
    /// <summary>
    /// Handles cross-cutting Spotify HTTP concerns in one place: waits and retries on
    /// 429 (honouring Retry-After), retries transient 5xx errors, and refreshes the
    /// access token once on 401.
    /// </summary>
    public class SpotifyHttpHandler : DelegatingHandler
    {
        private const int MaxRateLimitRetries = 5;
        private const int MaxServerErrorRetries = 3;
        private static readonly TimeSpan MaxRetryAfter = TimeSpan.FromSeconds(60);

        private readonly Func<TimeSpan, CancellationToken, Task> _delay;

        /// <summary>
        /// Called on 401; returns a fresh access token, or null if refresh is not possible
        /// </summary>
        public Func<CancellationToken, Task<string?>>? TokenRefresher { get; set; }

        public SpotifyHttpHandler(HttpMessageHandler? innerHandler = null,
            Func<TimeSpan, CancellationToken, Task>? delay = null)
            : base(innerHandler ?? new HttpClientHandler())
        {
            _delay = delay ?? Task.Delay;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            int rateLimitRetries = 0;
            int serverErrorRetries = 0;
            bool refreshed = false;

            while (true)
            {
                var response = await base.SendAsync(request, cancellationToken);

                if (response.StatusCode == HttpStatusCode.TooManyRequests &&
                    rateLimitRetries < MaxRateLimitRetries)
                {
                    rateLimitRetries++;
                    var wait = GetRetryAfter(response) ?? TimeSpan.FromSeconds(Math.Pow(2, rateLimitRetries));
                    response.Dispose();
                    await _delay(wait > MaxRetryAfter ? MaxRetryAfter : wait, cancellationToken);
                    continue;
                }

                if ((int)response.StatusCode >= 500 && serverErrorRetries < MaxServerErrorRetries)
                {
                    serverErrorRetries++;
                    response.Dispose();
                    await _delay(TimeSpan.FromSeconds(serverErrorRetries), cancellationToken);
                    continue;
                }

                if (response.StatusCode == HttpStatusCode.Unauthorized && !refreshed && TokenRefresher != null)
                {
                    refreshed = true;
                    var token = await TokenRefresher(cancellationToken);
                    if (!string.IsNullOrEmpty(token))
                    {
                        response.Dispose();
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                        continue;
                    }
                }

                return response;
            }
        }

        private static TimeSpan? GetRetryAfter(HttpResponseMessage response)
        {
            var retryAfter = response.Headers.RetryAfter;
            if (retryAfter?.Delta is TimeSpan delta)
                return delta;
            if (retryAfter?.Date is DateTimeOffset date)
                return date - DateTimeOffset.UtcNow;
            return null;
        }
    }
}
