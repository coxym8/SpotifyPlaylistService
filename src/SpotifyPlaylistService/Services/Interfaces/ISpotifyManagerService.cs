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
        /// Gets all liked songs with genre information populated
        /// </summary>
        Task<List<SpotifyTrack>> GetLikedSongsWithGenresAsync();

        /// <summary>
        /// Gets all user playlists
        /// </summary>
        Task<List<SpotifyPlaylist>> GetUserPlaylistsAsync();

        /// <summary>
        /// Adds all tracks from a playlist to liked songs
        /// </summary>
        Task<bool> AddPlaylistTracksToLikedAsync(string playlistId);

        /// <summary>
        /// Adds tracks from multiple playlists to liked songs
        /// </summary>
        Task<bool> AddMultiplePlaylistsToLikedAsync(List<string> playlistIds);

        /// <summary>
        /// Gets statistics about genres in the track collection
        /// </summary>
        Dictionary<string, int> GetGenreStatistics(List<SpotifyTrack> tracks);

        /// <summary>
        /// Creates or updates a playlist for a specific genre
        /// </summary>
        Task<string?> CreateOrUpdateGenrePlaylistAsync(string genre, List<SpotifyTrack> allTracks);

        /// <summary>
        /// Creates or updates playlists for all genres found in tracks
        /// </summary>
        Task<Dictionary<string, string>> CreateAllGenrePlaylistsAsync(List<SpotifyTrack> allTracks);

        /// <summary>
        /// Gets all playlists that were created by this library (start with [Genre])
        /// </summary>
        Task<List<SpotifyPlaylist>> GetGenrePlaylistsAsync();

        /// <summary>
        /// Deletes all genre playlists
        /// </summary>
        Task<int> DeleteAllGenrePlaylistsAsync();

        /// <summary>
        /// Deletes a specific genre playlist
        /// </summary>
        Task<bool> DeleteGenrePlaylistAsync(string genre);
    }
}