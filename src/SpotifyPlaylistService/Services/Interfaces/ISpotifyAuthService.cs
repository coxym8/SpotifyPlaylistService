namespace SpotifyPlaylistService.Services
{
    /// <summary>
    /// Interface for Spotify OAuth 2.0 authentication
    /// </summary>
    public interface ISpotifyAuthService
    {
        /// <summary>
        /// Current access token
        /// </summary>
        string? AccessToken { get; }

        /// <summary>
        /// Refresh token for obtaining new access tokens
        /// </summary>
        string? RefreshToken { get; }

        /// <summary>
        /// Client ID of the Spotify app used to sign in
        /// </summary>
        string? ClientId { get; }

        /// <summary>
        /// Raised with the authorization URL before the browser is opened, so a UI can
        /// show it if the browser fails to launch
        /// </summary>
        event Action<string>? AuthorizationUrlReady;

        /// <summary>
        /// Authenticates with Spotify using OAuth 2.0 Authorization Code flow
        /// </summary>
        /// <param name="clientId">Spotify application client ID</param>
        /// <param name="clientSecret">Spotify application client secret</param>
        /// <returns>True if authentication successful</returns>
        Task<bool> AuthenticateAsync(string clientId, string clientSecret);

        /// <summary>
        /// Authenticates using Authorization Code with PKCE, which needs no client secret and is
        /// Spotify's recommended flow for desktop apps. The session is saved for later restore.
        /// </summary>
        Task<bool> AuthenticateWithPkceAsync(string clientId, CancellationToken ct = default);

        /// <summary>
        /// Restores a saved PKCE session by refreshing its token
        /// </summary>
        Task<bool> TryRestoreSessionAsync(CancellationToken ct = default);

        /// <summary>
        /// Refreshes the access token using the refresh token
        /// </summary>
        /// <returns>True if refresh successful</returns>
        Task<bool> RefreshAccessTokenAsync(CancellationToken ct = default);

        /// <summary>
        /// Forgets tokens and any saved session
        /// </summary>
        void SignOut();
    }
}
