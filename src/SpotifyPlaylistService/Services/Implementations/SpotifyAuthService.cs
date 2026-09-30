using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SpotifyPlaylistService.Storage;

namespace SpotifyPlaylistService.Services
{
    /// <summary>
    /// Implementation of Spotify OAuth 2.0 authentication
    /// </summary>
    public class SpotifyAuthService : ISpotifyAuthService
    {
        public const int CallbackPort = 8888;
        public static readonly string RedirectUri = $"http://127.0.0.1:{CallbackPort}/callback";

        private const string TokenUrl = "https://accounts.spotify.com/api/token";
        private const string Scopes = "user-library-read user-library-modify playlist-read-private " +
                                      "playlist-read-collaborative playlist-modify-public playlist-modify-private";
        private static readonly TimeSpan LoginTimeout = TimeSpan.FromMinutes(5);

        private readonly HttpClient _httpClient;
        private readonly ISessionStore? _sessionStore;
        private readonly SemaphoreSlim _refreshLock = new(1, 1);
        private string? _clientSecret;
        private DateTimeOffset _lastRefresh = DateTimeOffset.MinValue;

        public string? AccessToken { get; private set; }
        public string? RefreshToken { get; private set; }
        public string? ClientId { get; private set; }

        public event Action<string>? AuthorizationUrlReady;

        public SpotifyAuthService(ISessionStore? sessionStore = null, HttpMessageHandler? handler = null)
        {
            _sessionStore = sessionStore;
            _httpClient = handler == null ? new HttpClient() : new HttpClient(handler);
        }

        public async Task<bool> AuthenticateAsync(string clientId, string clientSecret)
        {
            ClientId = clientId;
            _clientSecret = clientSecret;

            var state = RandomString(16);
            var authUrl = BuildAuthorizeUrl(clientId, state, codeChallenge: null);

            var code = await WaitForAuthorizationCodeAsync(authUrl, state, CancellationToken.None);
            if (string.IsNullOrEmpty(code))
                return false;

            return await RequestTokenAsync(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = RedirectUri
            }, CancellationToken.None);
        }

        public async Task<bool> AuthenticateWithPkceAsync(string clientId, CancellationToken ct = default)
        {
            ClientId = clientId;
            _clientSecret = null;

            var verifier = RandomString(64);
            var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
            var state = RandomString(16);
            var authUrl = BuildAuthorizeUrl(clientId, state, challenge);

            var code = await WaitForAuthorizationCodeAsync(authUrl, state, ct);
            if (string.IsNullOrEmpty(code))
                return false;

            var success = await RequestTokenAsync(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = RedirectUri,
                ["client_id"] = clientId,
                ["code_verifier"] = verifier
            }, ct);

            if (success)
                SaveSession();

            return success;
        }

        public async Task<bool> TryRestoreSessionAsync(CancellationToken ct = default)
        {
            var session = _sessionStore?.Load();
            if (session == null || string.IsNullOrEmpty(session.ClientId) || string.IsNullOrEmpty(session.RefreshToken))
                return false;

            ClientId = session.ClientId;
            RefreshToken = session.RefreshToken;
            _clientSecret = null;

            if (await RefreshAccessTokenAsync(ct))
                return true;

            SignOut();
            return false;
        }

        public async Task<bool> RefreshAccessTokenAsync(CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(RefreshToken) || ClientId == null)
                return false;

            await _refreshLock.WaitAsync(ct);
            try
            {
                // Several requests can hit 401 at once; only the first needs to refresh
                if (DateTimeOffset.UtcNow - _lastRefresh < TimeSpan.FromSeconds(10) && AccessToken != null)
                    return true;

                var form = new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = RefreshToken
                };

                if (_clientSecret == null)
                    form["client_id"] = ClientId;

                if (!await RequestTokenAsync(form, ct))
                    return false;

                _lastRefresh = DateTimeOffset.UtcNow;
                if (_clientSecret == null)
                    SaveSession();

                return true;
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        public void SignOut()
        {
            AccessToken = null;
            RefreshToken = null;
            _sessionStore?.Clear();
        }

        private string BuildAuthorizeUrl(string clientId, string state, string? codeChallenge)
        {
            var url = "https://accounts.spotify.com/authorize?" +
                      $"client_id={Uri.EscapeDataString(clientId)}" +
                      "&response_type=code" +
                      $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
                      $"&scope={Uri.EscapeDataString(Scopes)}" +
                      $"&state={state}";

            if (codeChallenge != null)
                url += $"&code_challenge_method=S256&code_challenge={codeChallenge}";

            return url;
        }

        /// <summary>
        /// Opens the browser and waits on a loopback socket for Spotify's redirect. A raw
        /// TcpListener is used instead of HttpListener, which needs a URL ACL on Windows.
        /// Connections are handled concurrently because browsers open idle speculative
        /// connections that would otherwise block the real redirect.
        /// </summary>
        private async Task<string?> WaitForAuthorizationCodeAsync(string authUrl, string expectedState,
            CancellationToken ct)
        {
            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(ct);
            lifetime.CancelAfter(LoginTimeout);

            var result = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var listener = new TcpListener(IPAddress.Loopback, CallbackPort);
            listener.Start();

            try
            {
                AuthorizationUrlReady?.Invoke(authUrl);
                OpenBrowser(authUrl);

                _ = AcceptLoopAsync(listener, expectedState, result, lifetime.Token);
                return await result.Task.WaitAsync(lifetime.Token);
            }
            finally
            {
                lifetime.Cancel();
                listener.Stop();
            }
        }

        private static async Task AcceptLoopAsync(TcpListener listener, string expectedState,
            TaskCompletionSource<string?> result, CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var client = await listener.AcceptTcpClientAsync(ct);
                    _ = HandleCallbackConnectionAsync(client, expectedState, result, ct);
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException)
            {
                // Listener stopped
            }
        }

        private static async Task HandleCallbackConnectionAsync(TcpClient client, string expectedState,
            TaskCompletionSource<string?> result, CancellationToken ct)
        {
            using (client)
            {
                try
                {
                    await using var stream = client.GetStream();

                    var requestTarget = await ReadRequestTargetAsync(stream, ct);
                    if (requestTarget == null || !requestTarget.StartsWith("/callback", StringComparison.Ordinal))
                    {
                        await WriteResponseAsync(stream, "404 Not Found", "Not found", ct);
                        return;
                    }

                    var query = ParseQuery(requestTarget);
                    query.TryGetValue("code", out var code);
                    query.TryGetValue("state", out var state);
                    query.TryGetValue("error", out var error);

                    var ok = error == null && code != null && state == expectedState;
                    await WriteResponseAsync(stream, "200 OK",
                        ok ? "<h1>Signed in</h1><p>You can close this window and return to the app.</p>"
                           : $"<h1>Sign-in failed</h1><p>{WebUtility.HtmlEncode(error ?? "State mismatch")}</p>",
                        ct);

                    result.TrySetResult(ok ? code : null);
                }
                catch (Exception ex) when (ex is OperationCanceledException or IOException or SocketException)
                {
                    // Idle or aborted browser connection
                }
            }
        }

        private static async Task<string?> ReadRequestTargetAsync(NetworkStream stream, CancellationToken ct)
        {
            var buffer = new byte[8192];
            var received = new StringBuilder();

            while (!received.ToString().Contains("\r\n\r\n") && received.Length < 65536)
            {
                var read = await stream.ReadAsync(buffer, ct);
                if (read == 0)
                    break;
                received.Append(Encoding.ASCII.GetString(buffer, 0, read));
            }

            // Request line: "GET /callback?code=...&state=... HTTP/1.1"
            var requestLine = received.ToString().Split("\r\n")[0].Split(' ');
            return requestLine.Length >= 2 ? requestLine[1] : null;
        }

        private static async Task WriteResponseAsync(NetworkStream stream, string status, string html,
            CancellationToken ct)
        {
            var body = Encoding.UTF8.GetBytes($"<html><body style=\"font-family:sans-serif\">{html}</body></html>");
            var header = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 {status}\r\nContent-Type: text/html; charset=utf-8\r\n" +
                $"Content-Length: {body.Length}\r\nConnection: close\r\n\r\n");

            await stream.WriteAsync(header, ct);
            await stream.WriteAsync(body, ct);
        }

        internal static Dictionary<string, string> ParseQuery(string requestTarget)
        {
            var result = new Dictionary<string, string>();
            var queryStart = requestTarget.IndexOf('?');
            if (queryStart < 0)
                return result;

            foreach (var pair in requestTarget[(queryStart + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split('=', 2);
                result[Uri.UnescapeDataString(parts[0])] =
                    parts.Length > 1 ? Uri.UnescapeDataString(parts[1].Replace('+', ' ')) : "";
            }

            return result;
        }

        private async Task<bool> RequestTokenAsync(Dictionary<string, string> form, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl)
            {
                Content = new FormUrlEncodedContent(form)
            };

            if (_clientSecret != null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ClientId}:{_clientSecret}")));
            }

            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                return false;

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            AccessToken = doc.RootElement.GetProperty("access_token").GetString();

            // Spotify may rotate the refresh token; keep the old one if none is returned
            if (doc.RootElement.TryGetProperty("refresh_token", out var refresh) &&
                refresh.ValueKind == JsonValueKind.String)
                RefreshToken = refresh.GetString();

            return AccessToken != null;
        }

        private void SaveSession()
        {
            if (_sessionStore != null && ClientId != null && RefreshToken != null)
                _sessionStore.Save(new StoredSession { ClientId = ClientId, RefreshToken = RefreshToken });
        }

        private static void OpenBrowser(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch
            {
                // Browser failed to open; AuthorizationUrlReady lets the caller show the URL
            }
        }

        private static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            return RandomNumberGenerator.GetString(chars, length);
        }

        private static string Base64Url(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
