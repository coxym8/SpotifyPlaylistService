using System.Collections.Concurrent;

namespace SpotifyPlaylistService.Storage
{
    /// <summary>
    /// Caches artist genres between runs. Spotify removed the batch artists endpoint, so
    /// every uncached artist costs one request.
    /// </summary>
    public interface IArtistGenreCache
    {
        bool TryGet(string artistId, out List<string> genres);

        void Set(string artistId, List<string> genres);

        void Save();
    }

    public class JsonArtistGenreCache : IArtistGenreCache
    {
        private readonly string _path;
        private readonly TimeSpan _maxAge;
        private readonly ConcurrentDictionary<string, Entry> _entries;

        public JsonArtistGenreCache(string? path = null, TimeSpan? maxAge = null)
        {
            _path = path ?? AppDataPaths.ArtistGenreCacheFile;
            _maxAge = maxAge ?? TimeSpan.FromDays(30);
            _entries = new ConcurrentDictionary<string, Entry>(
                JsonFile.Read<Dictionary<string, Entry>>(_path) ?? new Dictionary<string, Entry>());
        }

        public bool TryGet(string artistId, out List<string> genres)
        {
            if (_entries.TryGetValue(artistId, out var entry) &&
                DateTimeOffset.UtcNow - entry.FetchedAt < _maxAge)
            {
                genres = entry.Genres;
                return true;
            }

            genres = new List<string>();
            return false;
        }

        public void Set(string artistId, List<string> genres) =>
            _entries[artistId] = new Entry { Genres = genres, FetchedAt = DateTimeOffset.UtcNow };

        public void Save() => JsonFile.Write(_path, new Dictionary<string, Entry>(_entries));

        public class Entry
        {
            public List<string> Genres { get; set; } = new();

            public DateTimeOffset FetchedAt { get; set; }
        }
    }
}
