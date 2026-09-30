using System.Net;
using SpotifyPlaylistService.Http;
using SpotifyPlaylistService.Services;
using SpotifyPlaylistService.Tests.Fakes;

namespace SpotifyPlaylistService.Tests
{
    public class SpotifyApiServiceTests
    {
        private static readonly Func<TimeSpan, CancellationToken, Task> NoDelay = (_, _) => Task.CompletedTask;

        private static (SpotifyApiService Api, FakeHttpHandler Http) Create(Func<RecordedRequest, HttpResponseMessage> respond)
        {
            var http = new FakeHttpHandler(respond);
            var api = new SpotifyApiService(http, NoDelay);
            api.SetAccessToken("token");
            return (api, http);
        }

        [Fact]
        public async Task PlaylistTracks_ParsesNewItemShapeAndSkipsEpisodesAndLocalFiles()
        {
            var (api, http) = Create(_ => FakeHttpHandler.Json("""
                { "items": [
                    { "item": { "type": "track", "id": "t1", "name": "One", "artists": [ { "id": "a1", "name": "A" } ] } },
                    { "item": { "type": "episode", "id": "e1", "name": "Pod", "artists": [] } },
                    { "item": { "type": "track", "id": null, "name": "Local", "is_local": true, "artists": [ { "id": null, "name": "L" } ] } },
                    { "track": { "id": "t2", "name": "Two", "artists": [ { "id": "a2", "name": "B" } ] } },
                    { "item": null }
                  ], "next": null }
                """));

            var tracks = await api.GetPlaylistTracksAsync("p1");

            Assert.Equal(new[] { "t1", "t2" }, tracks.Select(t => t.Id));
            Assert.Equal("/v1/playlists/p1/items?limit=50", http.Requests.Single().PathAndQuery);
        }

        [Fact]
        public async Task UserPlaylists_FollowsPagingAndReadsItemsOrTracksTotal()
        {
            var (api, http) = Create(r => r.PathAndQuery.Contains("offset=50")
                ? FakeHttpHandler.Json("""
                    { "items": [ { "id": "p2", "name": "Old", "owner": { "id": "me" }, "tracks": { "total": 7 } } ], "next": null }
                    """)
                : FakeHttpHandler.Json("""
                    { "items": [ { "id": "p1", "name": "[Genre] Pop", "description": "d", "owner": { "id": "me" }, "items": { "total": 42 } } ],
                      "next": "https://api.spotify.com/v1/me/playlists?offset=50&limit=50" }
                    """));

            var playlists = await api.GetUserPlaylistsAsync();

            Assert.Equal(2, http.Requests.Count);
            Assert.Equal(42, playlists[0].TotalTracks);
            Assert.Equal("d", playlists[0].Description);
            Assert.Equal(7, playlists[1].TotalTracks);
        }

        [Fact]
        public async Task CreatePlaylist_PostsPrivatePlaylistToMePlaylists()
        {
            var (api, http) = Create(_ => FakeHttpHandler.Json("""{ "id": "new" }""", HttpStatusCode.Created));

            var id = await api.CreatePlaylistAsync("[Genre] Pop", "desc");

            var request = http.Requests.Single();
            Assert.Equal("new", id);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/v1/me/playlists", request.PathAndQuery);
            Assert.Contains("\"public\":false", request.Body);
        }

        [Fact]
        public async Task ReplacePlaylistTracks_PutsFirstHundredThenPostsTheRest()
        {
            var (api, http) = Create(_ => FakeHttpHandler.Json("""{ "snapshot_id": "s" }"""));
            var ids = Enumerable.Range(0, 250).Select(i => $"t{i}").ToList();

            Assert.True(await api.ReplacePlaylistTracksAsync("p1", ids));

            Assert.Equal(new[] { HttpMethod.Put, HttpMethod.Post, HttpMethod.Post }, http.Requests.Select(r => r.Method));
            Assert.All(http.Requests, r => Assert.Equal("/v1/playlists/p1/items", r.PathAndQuery));
            Assert.Contains("spotify:track:t0", http.Requests[0].Body);
            Assert.Contains("spotify:track:t249", http.Requests[2].Body);
        }

        [Fact]
        public async Task ClearPlaylist_PutsEmptyList()
        {
            var (api, http) = Create(_ => FakeHttpHandler.Json("{}"));

            await api.ClearPlaylistAsync("p1");

            var request = Assert.Single(http.Requests);
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal("""{"uris":[]}""", request.Body);
        }

        [Fact]
        public async Task AddTracksToLiked_UsesLibraryEndpointInBatchesOf40()
        {
            var (api, http) = Create(_ => new HttpResponseMessage(HttpStatusCode.OK));

            await api.AddTracksToLikedAsync(Enumerable.Range(0, 85).Select(i => $"t{i}").ToList());

            Assert.Equal(3, http.Requests.Count);
            Assert.All(http.Requests, r => Assert.StartsWith("/v1/me/library?uris=spotify%3Atrack%3At", r.PathAndQuery));
        }

        [Fact]
        public async Task DeletePlaylist_RemovesPlaylistUriFromLibrary()
        {
            var (api, http) = Create(_ => new HttpResponseMessage(HttpStatusCode.OK));

            Assert.True(await api.DeletePlaylistAsync("abc"));

            var request = http.Requests.Single();
            Assert.Equal(HttpMethod.Delete, request.Method);
            Assert.Equal("/v1/me/library?uris=spotify%3Aplaylist%3Aabc", request.PathAndQuery);
        }

        [Fact]
        public async Task DeletePlaylist_ThrowsWithSpotifyMessageOnForbidden()
        {
            var (api, _) = Create(_ => FakeHttpHandler.Json("""{"error":"no"}""", HttpStatusCode.Forbidden));

            var ex = await Assert.ThrowsAsync<SpotifyApiException>(() => api.DeletePlaylistAsync("abc"));
            Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
        }

        [Fact]
        public async Task RateLimitedRequests_AreRetriedAfterWaiting()
        {
            var waits = new List<TimeSpan>();
            int calls = 0;
            var http = new FakeHttpHandler(_ =>
            {
                if (++calls == 1)
                {
                    var limited = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                    limited.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(3));
                    return limited;
                }
                return FakeHttpHandler.Json("""{ "id": "me" }""");
            });
            var api = new SpotifyApiService(http, (d, _) => { waits.Add(d); return Task.CompletedTask; });
            api.SetAccessToken("token");

            Assert.Equal("me", await api.GetUserIdAsync());
            Assert.Equal(new[] { TimeSpan.FromSeconds(3) }, waits);
        }

        [Fact]
        public async Task Unauthorized_RefreshesTokenOnceAndRetries()
        {
            var (api, http) = Create(r => r.Authorization == "Bearer fresh"
                ? FakeHttpHandler.Json("""{ "id": "me" }""")
                : new HttpResponseMessage(HttpStatusCode.Unauthorized));
            int refreshes = 0;
            api.SetTokenRefresher(_ => { refreshes++; return Task.FromResult<string?>("fresh"); });

            Assert.Equal("me", await api.GetUserIdAsync());
            Assert.Equal(1, refreshes);
            Assert.Equal(new[] { "Bearer token", "Bearer fresh" }, http.Requests.Select(r => r.Authorization));
        }

        [Fact]
        public async Task ArtistGenres_EmptyWhenArtistNotFound()
        {
            var (api, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

            Assert.Empty(await api.GetArtistGenresAsync("missing"));
        }
    }
}
