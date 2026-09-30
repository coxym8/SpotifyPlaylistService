using SpotifyPlaylistService.Models;

namespace SpotifyPlaylistService.Services
{
    /// <summary>
    /// Interface for high-level Spotify playlist management operations
    /// </summary>
    public interface ISpotifyManagerService
    {
        /// <summary>
        /// Initializes the service with an access token
        /// </summary>
        Task InitializeAsync(string accessToken);

        /// <summary>
        /// Initializes the service from a signed-in auth service, refreshing the token automatically on 401
        /// </summary>
        Task InitializeAsync(ISpotifyAuthService authService, CancellationToken ct = default);

        /// <summary>
        /// Gets the current user's Spotify ID
        /// </summary>
        Task<string> GetCurrentUserIdAsync(CancellationToken ct = default);

        /// <summary>
        /// Gets all liked songs with genre information populated
        /// </summary>
        Task<List<SpotifyTrack>> GetLikedSongsWithGenresAsync(IProgress<OperationProgress>? progress = null,
            CancellationToken ct = default);

        /// <summary>
        /// Gets all user playlists
        /// </summary>
        Task<List<SpotifyPlaylist>> GetUserPlaylistsAsync(CancellationToken ct = default);

        /// <summary>
        /// Adds all tracks from a playlist to liked songs
        /// </summary>
        Task<bool> AddPlaylistTracksToLikedAsync(string playlistId, CancellationToken ct = default);

        /// <summary>
        /// Adds tracks from multiple playlists to liked songs
        /// </summary>
        Task<bool> AddMultiplePlaylistsToLikedAsync(List<string> playlistIds, CancellationToken ct = default);

        /// <summary>
        /// Gets statistics about genres in the track collection
        /// </summary>
        Dictionary<string, int> GetGenreStatistics(List<SpotifyTrack> tracks);

        /// <summary>
        /// Collates fine-grained Spotify genres into parent groups using the given rules
        /// </summary>
        List<GenreGroup> CollateGenres(List<SpotifyTrack> tracks, GenreRuleSet rules);

        /// <summary>
        /// Creates or updates a playlist for a specific genre
        /// </summary>
        Task<string?> CreateOrUpdateGenrePlaylistAsync(string genre, List<SpotifyTrack> allTracks,
            CancellationToken ct = default);

        /// <summary>
        /// Creates or updates playlists for all genres found in tracks
        /// </summary>
        Task<Dictionary<string, string>> CreateAllGenrePlaylistsAsync(List<SpotifyTrack> allTracks,
            IProgress<OperationProgress>? progress = null, CancellationToken ct = default);

        /// <summary>
        /// Creates or updates one playlist per collated group. Returns group name to playlist ID.
        /// </summary>
        Task<Dictionary<string, string>> CreateCollatedPlaylistsAsync(IEnumerable<GenreGroup> groups,
            IProgress<OperationProgress>? progress = null, CancellationToken ct = default);

        /// <summary>
        /// Gets all playlists created by this library: owned by the current user and named "[Genre] ..."
        /// </summary>
        Task<List<SpotifyPlaylist>> GetGenrePlaylistsAsync(CancellationToken ct = default);

        /// <summary>
        /// Deletes the given playlists. Anything not created by this library is skipped and reported
        /// as failed, so this can never remove a user's own or followed playlists.
        /// </summary>
        Task<BulkDeleteResult> DeleteGenrePlaylistsAsync(IEnumerable<SpotifyPlaylist> playlists,
            IProgress<OperationProgress>? progress = null, CancellationToken ct = default);

        /// <summary>
        /// Deletes all genre playlists
        /// </summary>
        Task<int> DeleteAllGenrePlaylistsAsync(IProgress<OperationProgress>? progress = null,
            CancellationToken ct = default);

        /// <summary>
        /// Deletes a specific genre playlist
        /// </summary>
        Task<bool> DeleteGenrePlaylistAsync(string genre, CancellationToken ct = default);
    }
}
