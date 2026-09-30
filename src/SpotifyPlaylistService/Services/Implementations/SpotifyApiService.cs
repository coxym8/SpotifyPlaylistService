using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SpotifyPlaylistService.Http;
using SpotifyPlaylistService.Models;

namespace SpotifyPlaylistService.Services
{
    /// <summary>
    /// Implementation of low-level Spotify API operations
    /// </summary>
    public class SpotifyApiService : ISpotifyApiService
    {
        private const int LibraryBatchSize = 40;
        private const int PlaylistItemsBatchSize = 100;

        private readonly HttpClient _httpClient;
        private readonly SpotifyHttpHandler _handler;
        private string? _userId;

        public SpotifyApiService(HttpMessageHandler? innerHandler = null,
            Func<TimeSpan, CancellationToken, Task>? delay = null)
        {
            _handler = new SpotifyHttpHandler(innerHandler, delay);
            _httpClient = new HttpClient(_handler)
            {
                BaseAddress = new Uri("https://api.spotify.com/v1/")
            };
        }

        public void SetAccessToken(string token)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        public void SetTokenRefresher(Func<CancellationToken, Task<string?>> refresher)
        {
            _handler.TokenRefresher = async ct =>
            {
                var token = await refresher(ct);
                if (!string.IsNullOrEmpty(token))
                    SetAccessToken(token);
                return token;
            };
        }

        public async Task<string> GetUserIdAsync(CancellationToken ct = default)
        {
            if (!string.IsNullOrEmpty(_userId))
                return _userId;

            using var doc = await GetJsonAsync("me", ct);
            _userId = doc.RootElement.GetProperty("id").GetString();

            return _userId!;
        }

        public async Task<List<SpotifyTrack>> GetLikedSongsAsync(IProgress<OperationProgress>? progress = null,
            CancellationToken ct = default)
        {
            var tracks = new List<SpotifyTrack>();

            await ForEachPageAsync("me/tracks?limit=50", ct, root =>
            {
                foreach (var item in root.GetProperty("items").EnumerateArray())
                {
                    if (TryParseTrack(item, out var track))
                        tracks.Add(track);
                }

                var total = root.TryGetProperty("total", out var totalProp) ? totalProp.GetInt32() : tracks.Count;
                progress?.Report(new OperationProgress(tracks.Count, total, $"Loaded {tracks.Count} of {total} liked songs"));
            });

            return tracks;
        }

        public async Task<List<SpotifyPlaylist>> GetUserPlaylistsAsync(CancellationToken ct = default)
        {
            var playlists = new List<SpotifyPlaylist>();

            await ForEachPageAsync("me/playlists?limit=50", ct, root =>
            {
                foreach (var item in root.GetProperty("items").EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object)
                        continue;

                    playlists.Add(new SpotifyPlaylist
                    {
                        Id = item.GetProperty("id").GetString()!,
                        Name = item.GetProperty("name").GetString() ?? "",
                        OwnerId = item.GetProperty("owner").GetProperty("id").GetString() ?? "",
                        Description = GetStringOrNull(item, "description"),
                        TotalTracks = GetItemTotal(item)
                    });
                }
            });

            return playlists;
        }

        public async Task<List<SpotifyTrack>> GetPlaylistTracksAsync(string playlistId, CancellationToken ct = default)
        {
            var tracks = new List<SpotifyTrack>();

            await ForEachPageAsync($"playlists/{playlistId}/items?limit=50", ct, root =>
            {
                foreach (var item in root.GetProperty("items").EnumerateArray())
                {
                    if (TryParseTrack(item, out var track))
                        tracks.Add(track);
                }
            });

            return tracks;
        }

        public async Task<List<string>> GetArtistGenresAsync(string artistId, CancellationToken ct = default)
        {
            using var response = await _httpClient.GetAsync($"artists/{artistId}", ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return new List<string>();

            await SpotifyApiException.EnsureSuccessAsync(response, ct);

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var genres = new List<string>();

            if (doc.RootElement.TryGetProperty("genres", out var genresArray) &&
                genresArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var genre in genresArray.EnumerateArray())
                {
                    var genreStr = genre.GetString();
                    if (!string.IsNullOrWhiteSpace(genreStr))
                        genres.Add(genreStr);
                }
            }

            return genres;
        }

        public async Task<bool> AddTracksToLikedAsync(List<string> trackIds, CancellationToken ct = default)
        {
            foreach (var batch in trackIds.Distinct().Chunk(LibraryBatchSize))
            {
                var uris = Uri.EscapeDataString(string.Join(",", batch.Select(TrackUri)));
                using var response = await _httpClient.PutAsync($"me/library?uris={uris}", null, ct);

                if (!response.IsSuccessStatusCode)
                    return false;
            }

            return true;
        }

        public async Task<string> CreatePlaylistAsync(string name, string description, CancellationToken ct = default)
        {
            var body = new
            {
                name,
                description,
                @public = false
            };

            using var response = await _httpClient.PostAsync("me/playlists", JsonContent(body), ct);
            await SpotifyApiException.EnsureSuccessAsync(response, ct);

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            return doc.RootElement.GetProperty("id").GetString()!;
        }

        public Task<bool> ClearPlaylistAsync(string playlistId, CancellationToken ct = default) =>
            ReplacePlaylistTracksAsync(playlistId, new List<string>(), ct);

        public async Task<bool> AddTracksToPlaylistAsync(string playlistId, List<string> trackIds,
            CancellationToken ct = default)
        {
            foreach (var batch in trackIds.Chunk(PlaylistItemsBatchSize))
            {
                var body = new { uris = batch.Select(TrackUri).ToList() };
                using var response = await _httpClient.PostAsync($"playlists/{playlistId}/items", JsonContent(body), ct);

                if (!response.IsSuccessStatusCode)
                    return false;
            }

            return true;
        }

        public async Task<bool> ReplacePlaylistTracksAsync(string playlistId, List<string> trackIds,
            CancellationToken ct = default)
        {
            // PUT replaces the whole playlist but accepts at most 100 items; the rest are appended
            var first = trackIds.Take(PlaylistItemsBatchSize).ToList();
            var body = new { uris = first.Select(TrackUri).ToList() };

            using var response = await _httpClient.PutAsync($"playlists/{playlistId}/items", JsonContent(body), ct);
            if (!response.IsSuccessStatusCode)
                return false;

            return await AddTracksToPlaylistAsync(playlistId, trackIds.Skip(PlaylistItemsBatchSize).ToList(), ct);
        }

        public async Task<SpotifyPlaylist?> FindPlaylistByNameAsync(string name, CancellationToken ct = default)
        {
            var playlists = await GetUserPlaylistsAsync(ct);
            return playlists.FirstOrDefault(p =>
                p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<bool> MovePlaylistToFolderAsync(string playlistId, string folderName)
        {
            // Not supported by Spotify's public API
            await Task.CompletedTask;
            return false;
        }

        public Task<bool> DeletePlaylistAsync(string playlistId, CancellationToken ct = default) =>
            UnfollowPlaylistAsync(playlistId, ct);

        public async Task<bool> UnfollowPlaylistAsync(string playlistId, CancellationToken ct = default)
        {
            var uri = Uri.EscapeDataString($"spotify:playlist:{playlistId}");
            using var response = await _httpClient.DeleteAsync($"me/library?uris={uri}", ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return false;

            await SpotifyApiException.EnsureSuccessAsync(response, ct);
            return true;
        }

        private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken ct)
        {
            using var response = await _httpClient.GetAsync(url, ct);
            await SpotifyApiException.EnsureSuccessAsync(response, ct);
            return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        }

        private async Task ForEachPageAsync(string firstUrl, CancellationToken ct, Action<JsonElement> handlePage)
        {
            string? url = firstUrl;

            while (!string.IsNullOrEmpty(url))
            {
                using var doc = await GetJsonAsync(url, ct);
                var root = doc.RootElement;
                handlePage(root);

                url = root.TryGetProperty("next", out var nextProp) && nextProp.ValueKind == JsonValueKind.String
                    ? nextProp.GetString()
                    : null;
            }
        }

        /// <summary>
        /// Parses a saved-track or playlist-item entry. Spotify renamed the "track" field to
        /// "item" in February 2026, so both are accepted.
        /// </summary>
        private static bool TryParseTrack(JsonElement entry, out SpotifyTrack track)
        {
            track = null!;

            if (!TryGetObject(entry, "item", out var trackElement) &&
                !TryGetObject(entry, "track", out trackElement))
                return false;

            if (trackElement.TryGetProperty("type", out var type) && type.GetString() != "track")
                return false;

            if (trackElement.TryGetProperty("is_local", out var isLocal) && isLocal.ValueKind == JsonValueKind.True)
                return false;

            var trackId = GetStringOrNull(trackElement, "id");
            var trackName = GetStringOrNull(trackElement, "name");

            if (!trackElement.TryGetProperty("artists", out var artists) ||
                artists.ValueKind != JsonValueKind.Array || artists.GetArrayLength() == 0)
                return false;

            var firstArtist = artists[0];
            var artistName = GetStringOrNull(firstArtist, "name");
            var artistId = GetStringOrNull(firstArtist, "id");

            if (trackId == null || trackName == null || artistName == null || artistId == null)
                return false;

            track = new SpotifyTrack
            {
                Id = trackId,
                Name = trackName,
                Artist = artistName,
                ArtistId = artistId
            };
            return true;
        }

        /// <summary>
        /// Playlist track count; the "tracks" object was renamed to "items" in February 2026
        /// </summary>
        private static int GetItemTotal(JsonElement playlist)
        {
            if ((TryGetObject(playlist, "items", out var items) || TryGetObject(playlist, "tracks", out items)) &&
                items.TryGetProperty("total", out var total) && total.ValueKind == JsonValueKind.Number)
                return total.GetInt32();

            return 0;
        }

        private static bool TryGetObject(JsonElement element, string name, out JsonElement value) =>
            element.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.Object;

        private static string? GetStringOrNull(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        private static string TrackUri(string trackId) => $"spotify:track:{trackId}";

        private static StringContent JsonContent(object body) =>
            new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
    }
}
