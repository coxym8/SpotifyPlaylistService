using SpotifyPlaylistService.Services;

namespace SpotifyPlaylistService.Tests.Fakes
{
    public class FakeAuthService : ISpotifyAuthService
    {
        public string? AccessToken { get; set; } = "token";
        public string? RefreshToken { get; set; } = "refresh";
        public string? ClientId { get; set; } = "client";
        public bool SignedOut { get; private set; }

        public event Action<string>? AuthorizationUrlReady;

        public Task<bool> AuthenticateAsync(string clientId, string clientSecret) => Task.FromResult(true);

        public Task<bool> AuthenticateWithPkceAsync(string clientId, CancellationToken ct = default)
        {
            AuthorizationUrlReady?.Invoke("https://accounts.spotify.com/authorize?fake");
            return Task.FromResult(true);
        }

        public Task<bool> TryRestoreSessionAsync(CancellationToken ct = default) => Task.FromResult(true);

        public Task<bool> RefreshAccessTokenAsync(CancellationToken ct = default) => Task.FromResult(true);

        public void SignOut() => SignedOut = true;
    }
}
