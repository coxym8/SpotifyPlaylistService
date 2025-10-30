namespace SpotifyPlaylistService.Models
{
    /// <summary>
    /// Represents a folder structure for organizing playlists (client-side concept)
    /// </summary>
    public class SpotifyFolder
    {
        /// <summary>
        /// Folder name
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// Playlists contained in this folder
        /// </summary>
        public List<SpotifyPlaylist> Playlists { get; set; } = new();

        /// <summary>
        /// Subfolders within this folder
        /// </summary>
        public List<SpotifyFolder> SubFolders { get; set; } = new();
    }
}