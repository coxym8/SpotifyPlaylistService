namespace SpotifyPlaylistService.Storage
{
    /// <summary>
    /// Per-user storage locations (~/.config/SpotifyPlaylistService on Linux,
    /// %APPDATA%\SpotifyPlaylistService on Windows)
    /// </summary>
    public static class AppDataPaths
    {
        public static string BaseDirectory { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SpotifyPlaylistService");

        public static string GenreRulesFile => Path.Combine(BaseDirectory, "genre-rules.json");

        public static string ArtistGenreCacheFile => Path.Combine(BaseDirectory, "artist-genres.json");

        public static string SessionFile => Path.Combine(BaseDirectory, "session.json");
    }
}
