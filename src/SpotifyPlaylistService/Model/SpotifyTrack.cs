namespace SpotifyPlaylistService.Models
{
    public class SpotifyTrack
    {
        /// <summary>
        /// Spotify track ID
        /// </summary>
        public required string Id { get; set; }

        /// <summary>
        /// Track name
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// Primary artist name
        /// </summary>
        public required string Artist { get; set; }

        /// <summary>
        /// Primary artist ID
        /// </summary>
        public required string ArtistId { get; set; }

        /// <summary>
        /// List of genres associated with the track's artist
        /// </summary>
        public List<string> Genres { get; set; } = [];
    }
}