using SpotifyPlaylistService.Models;

namespace SpotifyPlaylistService.Services
{
    /// <summary>
    /// Interface for low-level Spotify API operations.
    /// Targets the Web API as changed for development-mode apps in February 2026.
    /// </summary>
    public interface ISpotifyApiService
    {
        /// <summary>
        /// Sets the access token for API requests
        /// </summary>
        void SetAccessToken(string token);

        /// <summary>
        /// Sets a callback used to obtain a fresh access token when a request returns 401
        /// </summary>
        void SetTokenRefresher(Func<CancellationToken, Task<string?>> refresher);

        /// <summary>
        /// Gets the current user's Spotify ID
        /// </summary>
        Task<string> GetUserIdAsync(CancellationToken ct = default);

        /// <summary>
        /// Gets all liked songs for the current user
        /// </summary>
        Task<List<SpotifyTrack>> GetLikedSongsAsync(IProgress<OperationProgress>? progress = null,
            CancellationToken ct = default);

        /// <summary>
        /// Gets all playlists for the current user
        /// </summary>
        Task<List<SpotifyPlaylist>> GetUserPlaylistsAsync(CancellationToken ct = default);

        /// <summary>
        /// Gets all tracks from a specific playlist (episodes and local files are skipped)
        /// </summary>
        Task<List<SpotifyTrack>> GetPlaylistTracksAsync(string playlistId, CancellationToken ct = default);

        /// <summary>
        /// Gets genre information for an artist (empty if the artist is not found)
        /// </summary>
        Task<List<string>> GetArtistGenresAsync(string artistId, CancellationToken ct = default);

        /// <summary>
        /// Adds tracks to the user's liked songs
        /// </summary>
        Task<bool> AddTracksToLikedAsync(List<string> trackIds, CancellationToken ct = default);

        /// <summary>
        /// Creates a new private playlist and returns its ID
        /// </summary>
        Task<string> CreatePlaylistAsync(string name, string description, CancellationToken ct = default);

        /// <summary>
        /// Removes all tracks from a playlist
        /// </summary>
        Task<bool> ClearPlaylistAsync(string playlistId, CancellationToken ct = default);

        /// <summary>
        /// Adds tracks to a playlist
        /// </summary>
        Task<bool> AddTracksToPlaylistAsync(string playlistId, List<string> trackIds, CancellationToken ct = default);

        /// <summary>
        /// Replaces a playlist's tracks with the given tracks
        /// </summary>
        Task<bool> ReplacePlaylistTracksAsync(string playlistId, List<string> trackIds, CancellationToken ct = default);

        /// <summary>
        /// Finds a playlist by exact name match
        /// </summary>
        Task<SpotifyPlaylist?> FindPlaylistByNameAsync(string name, CancellationToken ct = default);

        /// <summary>
        /// Moves a playlist to a folder (not supported by public API)
        /// </summary>
        Task<bool> MovePlaylistToFolderAsync(string playlistId, string folderName);

        /// <summary>
        /// Deletes a playlist (Spotify only supports unfollowing; the owner can recover it
        /// from their account page). Returns false if the playlist does not exist; throws
        /// <see cref="Http.SpotifyApiException"/> for other errors.
        /// </summary>
        Task<bool> DeletePlaylistAsync(string playlistId, CancellationToken ct = default);

        /// <summary>
        /// Unfollows a playlist
        /// </summary>
        Task<bool> UnfollowPlaylistAsync(string playlistId, CancellationToken ct = default);
    }
}
