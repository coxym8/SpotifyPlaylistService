namespace SpotifyPlaylistService.Models
{
    /// <summary>
    /// Maps Spotify sub-genres onto a parent group. Exact aliases win over keywords;
    /// a keyword matches when a word in the (normalised) genre name starts with it,
    /// so "metal" matches "nu metal" and "metalcore" but "rap" does not match "scrap".
    /// </summary>
    public class GenreRule
    {
        public string Group { get; set; } = "";

        public List<string> Keywords { get; set; } = new();

        public List<string> Exact { get; set; } = new();
    }
}
