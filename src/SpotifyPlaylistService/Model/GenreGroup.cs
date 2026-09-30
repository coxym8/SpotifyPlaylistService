namespace SpotifyPlaylistService.Models
{
    /// <summary>
    /// A collated genre (e.g. "Pop") and the Spotify sub-genres and tracks it contains
    /// </summary>
    public class GenreGroup
    {
        public required string Name { get; init; }

        public SortedSet<string> SourceGenres { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<SpotifyTrack> Tracks { get; } = new();
    }
}
