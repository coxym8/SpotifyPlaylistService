using SpotifyPlaylistService.Models;
using SpotifyPlaylistService.Storage;

namespace SpotifyPlaylistService.Tests
{
    public class StorageTests
    {
        [Fact]
        public void GenreRuleStore_ReturnsDefaultsWhenNoFile()
        {
            using var dir = new TempDir();
            var rules = new GenreRuleStore(dir.File("rules.json")).Load();

            Assert.Equal(GenreRuleSet.CreateDefault().Rules.Count, rules.Rules.Count);
        }

        [Fact]
        public void GenreRuleStore_RoundTripsEdits()
        {
            using var dir = new TempDir();
            var store = new GenreRuleStore(dir.File("nested/rules.json"));
            var rules = new GenreRuleSet
            {
                Rules = { new GenreRule { Group = "Chill", Keywords = { "lo fi" }, Exact = { "ambient" } } },
                MinTracks = 3,
                IncludeOther = false,
                IncludeUnclassified = true
            };

            store.Save(rules);
            var loaded = store.Load();

            var rule = Assert.Single(loaded.Rules);
            Assert.Equal("Chill", rule.Group);
            Assert.Equal(new[] { "lo fi" }, rule.Keywords);
            Assert.Equal(new[] { "ambient" }, rule.Exact);
            Assert.Equal(3, loaded.MinTracks);
            Assert.False(loaded.IncludeOther);
            Assert.True(loaded.IncludeUnclassified);
        }

        [Fact]
        public void GenreRuleStore_FallsBackToDefaultsOnCorruptFile()
        {
            using var dir = new TempDir();
            File.WriteAllText(dir.File("rules.json"), "{ not json");

            var rules = new GenreRuleStore(dir.File("rules.json")).Load();

            Assert.NotEmpty(rules.Rules);
        }

        [Fact]
        public void ArtistGenreCache_PersistsAndExpires()
        {
            using var dir = new TempDir();
            var path = dir.File("cache.json");

            var cache = new JsonArtistGenreCache(path);
            cache.Set("artist", new List<string> { "pop" });
            cache.Save();

            Assert.True(new JsonArtistGenreCache(path).TryGet("artist", out var genres));
            Assert.Equal(new[] { "pop" }, genres);
            Assert.False(new JsonArtistGenreCache(path, TimeSpan.Zero).TryGet("artist", out _));
        }

        [Fact]
        public void SessionStore_SavesLoadsAndClears()
        {
            using var dir = new TempDir();
            var store = new JsonSessionStore(dir.File("session.json"));

            store.Save(new StoredSession { ClientId = "id", RefreshToken = "rt" });
            Assert.Equal("rt", store.Load()!.RefreshToken);

            if (!OperatingSystem.IsWindows())
            {
                var mode = File.GetUnixFileMode(dir.File("session.json"));
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, mode);
            }

            store.Clear();
            Assert.Null(store.Load());
        }
    }
}
