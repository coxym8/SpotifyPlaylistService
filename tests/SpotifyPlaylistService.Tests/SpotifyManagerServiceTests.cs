using SpotifyPlaylistService.Models;
using SpotifyPlaylistService.Services;
using SpotifyPlaylistService.Storage;
using SpotifyPlaylistService.Tests.Fakes;

namespace SpotifyPlaylistService.Tests
{
    public class SpotifyManagerServiceTests
    {
        [Fact]
        public async Task GenrePlaylists_AreOnlyOwnedPlaylistsWithPrefix()
        {
            var api = new FakeSpotifyApi();
            api.AddPlaylist("1", "[Genre] Pop");
            api.AddPlaylist("2", "[Genre] Rock", owner: "someone-else");
            api.AddPlaylist("3", "Road trip");
            var manager = new SpotifyManagerService(api);

            var playlists = await manager.GetGenrePlaylistsAsync();

            Assert.Equal(new[] { "1" }, playlists.Select(p => p.Id));
        }

        [Fact]
        public async Task BulkDelete_DeletesAppPlaylistsAndReportsSkipsAndFailures()
        {
            var api = new FakeSpotifyApi();
            var pop = api.AddPlaylist("1", "[Genre] Pop");
            var rock = api.AddPlaylist("2", "[Genre] Rock");
            var mine = api.AddPlaylist("3", "Road trip");
            var theirs = api.AddPlaylist("4", "[Genre] Jazz", owner: "someone-else");
            api.FailingDeletes.Add("2");
            var manager = new SpotifyManagerService(api);
            var reports = new List<OperationProgress>();

            var result = await manager.DeleteGenrePlaylistsAsync(new[] { pop, rock, mine, theirs, pop },
                new SyncProgress(reports));

            Assert.Equal(new[] { "1" }, result.Deleted.Select(p => p.Id));
            Assert.Equal(new[] { "2", "3", "4" }, result.Failed.Select(f => f.Playlist.Id));
            Assert.Contains("403", result.Failed[0].Reason);
            Assert.Contains("Not created by this app", result.Failed[1].Reason);
            Assert.Equal(new[] { "2", "3", "4" }, api.Playlists.Select(p => p.Id));
            Assert.Equal("Deleted 1 of 4 playlists", reports.Last().Message);
        }

        [Fact]
        public async Task DeleteAll_DeletesEveryGenrePlaylist()
        {
            var api = new FakeSpotifyApi();
            api.AddPlaylist("1", "[Genre] Pop");
            api.AddPlaylist("2", "[Genre] Rock");
            api.AddPlaylist("3", "Road trip");

            var deleted = await new SpotifyManagerService(api).DeleteAllGenrePlaylistsAsync();

            Assert.Equal(2, deleted);
            Assert.Equal(new[] { "3" }, api.Playlists.Select(p => p.Id));
        }

        [Fact]
        public async Task CreateCollated_UpdatesExistingAndCreatesMissingWithOnePlaylistListing()
        {
            var api = new FakeSpotifyApi();
            api.AddPlaylist("existing", "[Genre] Pop");
            api.AddLiked("t1", "a1", "dance pop");
            api.AddLiked("t2", "a2", "hard rock");
            api.AddLiked("t3", "a3", "k-pop");
            var manager = new SpotifyManagerService(api);

            var tracks = await manager.GetLikedSongsWithGenresAsync();
            var rules = GenreRuleSet.CreateDefault();
            rules.MinTracks = 1;
            var groups = manager.CollateGenres(tracks, rules);

            var result = await manager.CreateCollatedPlaylistsAsync(groups);

            Assert.Equal("existing", result["Pop"]);
            Assert.Equal(new[] { "t1", "t3" }, api.PlaylistTracks["existing"]);
            Assert.Equal(1, api.CreatedCount);
            Assert.Equal(new[] { "t2" }, api.PlaylistTracks[result["Rock"]]);
            Assert.Equal(1, api.PlaylistListCalls);

            var created = api.Playlists.Single(p => p.Name == "[Genre] Rock");
            Assert.Contains(GenrePlaylistNaming.DescriptionMarker, created.Description);
            Assert.Contains("hard rock", created.Description);
        }

        [Fact]
        public async Task CreateCollated_DoesNotReuseSameNamedPlaylistOwnedBySomeoneElse()
        {
            var api = new FakeSpotifyApi();
            api.AddPlaylist("theirs", "[Genre] Pop", owner: "someone-else");
            var group = new GenreGroup { Name = "Pop" };
            group.Tracks.Add(new SpotifyTrack { Id = "t1", Name = "n", Artist = "a", ArtistId = "a1" });

            var result = await new SpotifyManagerService(api).CreateCollatedPlaylistsAsync(new[] { group });

            Assert.NotEqual("theirs", result["Pop"]);
        }

        [Fact]
        public async Task LikedSongs_UseGenreCacheAndOnlyLookUpNewArtists()
        {
            using var dir = new TempDir();
            var cache = new JsonArtistGenreCache(dir.File("cache.json"));
            cache.Set("a1", new List<string> { "cached genre" });

            var api = new FakeSpotifyApi();
            api.AddLiked("t1", "a1", "fresh genre");
            api.AddLiked("t2", "a2", "rock");
            api.AddLiked("t3", "a2", "rock");

            var tracks = await new SpotifyManagerService(api, cache).GetLikedSongsWithGenresAsync();

            Assert.Equal(new[] { "a2" }, api.ArtistLookups);
            Assert.Equal(new[] { "cached genre" }, tracks[0].Genres);
            Assert.Equal(new[] { "rock" }, tracks[2].Genres);
            Assert.True(new JsonArtistGenreCache(dir.File("cache.json")).TryGet("a2", out _));
        }

        [Fact]
        public async Task LikedSongs_ArtistLookupFailureSurfacesAndStopsQueuedLookups()
        {
            var api = new ThrowingArtistApi();
            for (int i = 0; i < 50; i++)
                api.AddLiked($"t{i}", $"a{i}", "pop");

            await Assert.ThrowsAsync<Http.SpotifyApiException>(() =>
                new SpotifyManagerService(api).GetLikedSongsWithGenresAsync());

            Assert.True(api.ArtistLookups.Count < 50, $"expected fail-fast, got {api.ArtistLookups.Count} lookups");
        }

        private class ThrowingArtistApi : FakeSpotifyApi, ISpotifyApiService
        {
            async Task<List<string>> ISpotifyApiService.GetArtistGenresAsync(string artistId, CancellationToken ct)
            {
                await base.GetArtistGenresAsync(artistId, ct);
                await Task.Delay(10, ct);
                throw new Http.SpotifyApiException(System.Net.HttpStatusCode.Forbidden, "blocked");
            }
        }

        [Fact]
        public void PlaylistDescription_IsTrimmedToSpotifyLimit()
        {
            var description = GenrePlaylistNaming.Description("Pop", 10,
                Enumerable.Range(0, 100).Select(i => $"genre number {i}"));

            Assert.True(description.Length <= 300);
        }

        /// <summary>
        /// Reports synchronously; Progress&lt;T&gt; posts to the thread pool and would race the assertions
        /// </summary>
        private class SyncProgress : IProgress<OperationProgress>
        {
            private readonly List<OperationProgress> _reports;

            public SyncProgress(List<OperationProgress> reports) => _reports = reports;

            public void Report(OperationProgress value) => _reports.Add(value);
        }
    }
}
