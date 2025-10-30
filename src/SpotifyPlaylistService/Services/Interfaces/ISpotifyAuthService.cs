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
        /// Authenticates with Spotify using OAuth 2.0 Authorization Code flow
        /// </summary>
        /// <param name="clientId">Spotify application client ID</param>
        /// <param name="clientSecret">Spotify application client secret</param>
        /// <returns>True if authentication successful</returns>
        Task<bool> AuthenticateAsync(string clientId, string clientSecret);

        /// <summary>
        /// Refreshes the access token using the refresh token
        /// </summary>
        /// <returns>True if refresh successful</returns>
        Task<bool> RefreshAccessTokenAsync();
    }
}