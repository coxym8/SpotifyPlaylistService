namespace SpotifyPlaylistService.Storage
{
    /// <summary>
    /// Persisted login so users are not sent through the browser on every launch.
    /// With PKCE no client secret is involved, so only the client ID and refresh token are stored.
    /// </summary>
    public class StoredSession
    {
        public string ClientId { get; set; } = "";

        public string RefreshToken { get; set; } = "";
    }

    public interface ISessionStore
    {
        StoredSession? Load();

        void Save(StoredSession session);

        void Clear();
    }

    public class JsonSessionStore : ISessionStore
    {
        private readonly string _path;

        public JsonSessionStore(string? path = null)
        {
            _path = path ?? AppDataPaths.SessionFile;
        }

        public StoredSession? Load() => JsonFile.Read<StoredSession>(_path);

        public void Save(StoredSession session)
        {
            JsonFile.Write(_path, session);

            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(_path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        public void Clear()
        {
            if (File.Exists(_path))
                File.Delete(_path);
        }
    }
}
