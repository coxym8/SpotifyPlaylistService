using System.Net;

namespace SpotifyPlaylistService.Http
{
    /// <summary>
    /// Raised when the Spotify Web API returns a non-success status code
    /// </summary>
    public class SpotifyApiException : Exception
    {
        public HttpStatusCode StatusCode { get; }

        public SpotifyApiException(HttpStatusCode statusCode, string message)
            : base($"Spotify API error {(int)statusCode} ({statusCode}): {message}")
        {
            StatusCode = statusCode;
        }

        internal static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
        {
            if (response.IsSuccessStatusCode)
                return;

            var body = await response.Content.ReadAsStringAsync(ct);
            throw new SpotifyApiException(response.StatusCode,
                string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase ?? "no details" : body);
        }
    }
}
