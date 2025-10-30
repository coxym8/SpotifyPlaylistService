using SpotifyPlaylistService.Models;

namespace SpotifyPlaylistService.Services
{
    /// <summary>
    /// Interface for low-level Spotify API operations
    /// </summary>
    public interface ISpotifyApiService
    {
        /// <summary>
        /// Sets the access token for API requests
        /// </summary>
        void SetAccessToken(string token);

        /// <summary>
        /// Gets the current user's Spotify ID
        /// </summary>
        Task<string> GetUserIdAsync();

        /// <summary>
        /// Gets all liked songs for the current user
        /// </summary>
        Task<List<SpotifyTrack>> GetLikedSongsAsync();

        /// <summary>
        /// Gets all playlists for the current user
        /// </summary>
        Task<List<SpotifyPlaylist>> GetUserPlaylistsAsync();

        /// <summary>
        /// Gets all tracks from a specific playlist
        /// </summary>
        Task<List<SpotifyTrack>> GetPlaylistTracksAsync(string playlistId);

        /// <summary>
        /// Gets genre information for an artist
        /// </summary>
        Task<List<string>> GetArtistGenresAsync(string artistId);

        /// <summary>
        /// Adds tracks to the user's liked songs
        /// </summary>
        Task<bool> AddTracksToLikedAsync(List<string> trackIds);

        /// <summary>
        /// Creates a new playlist
        /// </summary>
        Task<string> CreatePlaylistAsync(string name, string description);

        /// <summary>
        /// Removes all tracks from a playlist
        /// </summary>
        Task<bool> ClearPlaylistAsync(string playlistId);

        /// <summary>
        /// Adds tracks to a playlist
        /// </summary>
        Task<bool> AddTracksToPlaylistAsync(string playlistId, List<string> trackIds);

        /// <summary>
        /// Finds a playlist by exact name match
        /// </summary>
        Task<SpotifyPlaylist?> FindPlaylistByNameAsync(string name);

        /// <summary>
        /// Moves a playlist to a folder (not supported by public API)
        /// </summary>
        Task<bool> MovePlaylistToFolderAsync(string playlistId, string folderName);

        /// <summary>
        /// Deletes a playlist (unfollows for the current user)
        /// </summary>
        Task<bool> DeletePlaylistAsync(string playlistId);

        /// <summary>
        /// Unfollows a playlist
        /// </summary>
        Task<bool> UnfollowPlaylistAsync(string playlistId);
    }
}