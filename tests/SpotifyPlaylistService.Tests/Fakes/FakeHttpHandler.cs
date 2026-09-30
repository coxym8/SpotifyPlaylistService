using System.Net;
using System.Text;

namespace SpotifyPlaylistService.Tests.Fakes
{
    public record RecordedRequest(HttpMethod Method, string PathAndQuery, string? Body, string? Authorization);

    /// <summary>
    /// Records requests and answers them from a routing function
    /// </summary>
    public class FakeHttpHandler : HttpMessageHandler
    {
        private readonly Func<RecordedRequest, HttpResponseMessage> _respond;

        public List<RecordedRequest> Requests { get; } = new();

        public FakeHttpHandler(Func<RecordedRequest, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            var recorded = new RecordedRequest(request.Method, request.RequestUri!.PathAndQuery, body,
                request.Headers.Authorization?.ToString());

            lock (Requests)
                Requests.Add(recorded);

            return _respond(recorded);
        }

        public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
            new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }
}
