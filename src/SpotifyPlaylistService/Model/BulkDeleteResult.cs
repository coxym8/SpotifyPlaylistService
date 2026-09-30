namespace SpotifyPlaylistService.Models
{
    /// <summary>
    /// Outcome of a bulk playlist delete
    /// </summary>
    public class BulkDeleteResult
    {
        public List<SpotifyPlaylist> Deleted { get; } = new();

        public List<(SpotifyPlaylist Playlist, string Reason)> Failed { get; } = new();
    }
}
