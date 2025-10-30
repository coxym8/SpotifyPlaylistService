namespace SpotifyPlaylistService.Models
{
    /// <summary>
    /// Represents a Spotify playlist
    /// </summary>
    public class SpotifyPlaylist
    {
        /// <summary>
        /// Spotify playlist ID
        /// </summary>
        public required string Id { get; set; }

        /// <summary>
        /// Playlist name
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// Owner's Spotify user ID
        /// </summary>
        public required string OwnerId { get; set; }

        /// <summary>
        /// Total number of tracks in the playlist
        /// </summary>
        public int TotalTracks { get; set; }

        /// <summary>
        /// Optional folder path for organization (client-side only)
        /// </summary>
        public string? FolderPath { get; set; }
    }
}