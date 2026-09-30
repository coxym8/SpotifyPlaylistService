namespace SpotifyPlaylistService.Services
{
    /// <summary>
    /// How playlists created by this library are named and recognised
    /// </summary>
    public static class GenrePlaylistNaming
    {
        public const string Prefix = "[Genre] ";

        public const string DescriptionMarker = "Created by SpotifyPlaylistService.";

        public static string PlaylistName(string genre) => Prefix + genre;

        public static bool IsGenrePlaylistName(string name) =>
            name.StartsWith(Prefix.TrimEnd(), StringComparison.Ordinal);

        public static string GenreFromName(string name) =>
            IsGenrePlaylistName(name) ? name[Prefix.TrimEnd().Length..].Trim() : name;

        public static string Description(string genre, int trackCount, IEnumerable<string>? sourceGenres = null)
        {
            var description = $"{genre}: {trackCount} tracks from your liked songs. {DescriptionMarker}";
            var sources = sourceGenres?.ToList();

            if (sources is { Count: > 0 })
            {
                var extra = " Includes: " + string.Join(", ", sources);
                description += extra;
            }

            // Spotify rejects descriptions over 300 characters
            return description.Length <= 300 ? description : description[..297] + "...";
        }
    }
}
