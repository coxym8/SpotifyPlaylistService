using System.Net;
using SpotifyPlaylistService.Services;
using SpotifyPlaylistService.Storage;
using SpotifyPlaylistService.Tests.Fakes;

namespace SpotifyPlaylistService.Tests
{
    [Collection("Loopback port")]
    public class SpotifyAuthServiceTests
    {
        [Fact]
        public async Task Pkce_CompletesLoopbackRedirectExchangesCodeAndSavesSession()
        {
            using var dir = new TempDir();
            var store = new JsonSessionStore(dir.File("session.json"));
            var tokenEndpoint = new FakeHttpHandler(_ => FakeHttpHandler.Json(
                """{ "access_token": "at", "refresh_token": "rt", "expires_in": 3600 }"""));
            var auth = new SpotifyAuthService(store, tokenEndpoint);

            string? authUrl = null;
            auth.AuthorizationUrlReady += url => authUrl = url;

            var login = auth.AuthenticateWithPkceAsync("client-123");
            await WaitUntil(() => authUrl != null);

            Assert.Contains("code_challenge_method=S256", authUrl);
            var state = SpotifyAuthService.ParseQuery(authUrl!)["state"];

            using var browser = new HttpClient();
            await browser.GetAsync($"{SpotifyAuthService.RedirectUri.Replace("/callback", "")}/favicon.ico");
            var page = await browser.GetStringAsync($"{SpotifyAuthService.RedirectUri}?code=the-code&state={state}");

            Assert.True(await login);
            Assert.Contains("Signed in", page);
            Assert.Equal("at", auth.AccessToken);

            var body = tokenEndpoint.Requests.Single().Body!;
            Assert.Contains("code=the-code", body);
            Assert.Contains("client_id=client-123", body);
            Assert.Contains("code_verifier=", body);
            Assert.Null(tokenEndpoint.Requests.Single().Authorization);

            Assert.Equal("rt", store.Load()!.RefreshToken);
        }

        [Fact]
        public async Task Pkce_IsNotBlockedByIdleSpeculativeBrowserConnection()
        {
            var auth = new SpotifyAuthService(null, new FakeHttpHandler(_ => FakeHttpHandler.Json(
                """{ "access_token": "at", "refresh_token": "rt" }""")));
            string? authUrl = null;
            auth.AuthorizationUrlReady += url => authUrl = url;

            var login = auth.AuthenticateWithPkceAsync("client");
            await WaitUntil(() => authUrl != null);

            // A browser preconnect: opens a socket and sends nothing
            using var idle = new System.Net.Sockets.TcpClient();
            await idle.ConnectAsync(IPAddress.Loopback, SpotifyAuthService.CallbackPort);

            var state = SpotifyAuthService.ParseQuery(authUrl!)["state"];
            using var browser = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            await browser.GetStringAsync($"{SpotifyAuthService.RedirectUri}?code=c&state={state}");

            Assert.True(await login.WaitAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task Pkce_RejectsRedirectWithWrongState()
        {
            var auth = new SpotifyAuthService(null, new FakeHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
            string? authUrl = null;
            auth.AuthorizationUrlReady += url => authUrl = url;

            var login = auth.AuthenticateWithPkceAsync("client");
            await WaitUntil(() => authUrl != null);

            using var browser = new HttpClient();
            await browser.GetStringAsync($"{SpotifyAuthService.RedirectUri}?code=c&state=forged");

            Assert.False(await login);
        }

        [Fact]
        public async Task RestoreSession_RefreshesWithClientIdAndStoresRotatedToken()
        {
            using var dir = new TempDir();
            var store = new JsonSessionStore(dir.File("session.json"));
            store.Save(new StoredSession { ClientId = "client", RefreshToken = "old" });
            var tokenEndpoint = new FakeHttpHandler(_ => FakeHttpHandler.Json(
                """{ "access_token": "at2", "refresh_token": "new" }"""));

            var auth = new SpotifyAuthService(store, tokenEndpoint);

            Assert.True(await auth.TryRestoreSessionAsync());
            Assert.Equal("at2", auth.AccessToken);
            Assert.Contains("refresh_token=old", tokenEndpoint.Requests.Single().Body);
            Assert.Contains("client_id=client", tokenEndpoint.Requests.Single().Body);
            Assert.Equal("new", store.Load()!.RefreshToken);
        }

        [Fact]
        public async Task RestoreSession_ClearsSessionWhenRefreshIsRejected()
        {
            using var dir = new TempDir();
            var store = new JsonSessionStore(dir.File("session.json"));
            store.Save(new StoredSession { ClientId = "client", RefreshToken = "revoked" });
            var auth = new SpotifyAuthService(store, new FakeHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)));

            Assert.False(await auth.TryRestoreSessionAsync());
            Assert.Null(store.Load());
        }

        [Fact]
        public void ParseQuery_DecodesValues()
        {
            var query = SpotifyAuthService.ParseQuery("/callback?code=a%2Fb&state=xyz&error=access_denied");

            Assert.Equal("a/b", query["code"]);
            Assert.Equal("access_denied", query["error"]);
        }

        private static async Task WaitUntil(Func<bool> condition)
        {
            for (int i = 0; i < 200 && !condition(); i++)
                await Task.Delay(10);
            Assert.True(condition());
        }
    }
}
