using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SpotifyPlaylistService.Models;

namespace SpotifyPlaylistService.Services
{
    /// <summary>
    /// Implementation of low-level Spotify API operations
    /// </summary>
    public class SpotifyApiService : ISpotifyApiService
    {
        private readonly HttpClient _httpClient;
        private string? _userId;

        public SpotifyApiService()
        {
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri("https://api.spotify.com/v1/");
        }

        public void SetAccessToken(string token)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        public async Task<string> GetUserIdAsync()
        {
            if (!string.IsNullOrEmpty(_userId))
                return _userId;

            var response = await _httpClient.GetAsync("me");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(content);
            _userId = doc.RootElement.GetProperty("id").GetString();

            return _userId!;
        }

        public async Task<List<SpotifyTrack>> GetLikedSongsAsync()
        {
            var tracks = new List<SpotifyTrack>();
            string? url = "me/tracks?limit=50";

            while (!string.IsNullOrEmpty(url))
            {
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                foreach (var item in root.GetProperty("items").EnumerateArray())
                {
                    var track = item.GetProperty("track");
                    var trackId = track.GetProperty("id").GetString();
                    var trackName = track.GetProperty("name").GetString();
                    var artists = track.GetProperty("artists").EnumerateArray();
                    var artistName = artists.First().GetProperty("name").GetString();
                    var artistId = artists.First().GetProperty("id").GetString();

                    if (trackId == null || trackName == null || artistName == null || artistId == null)
                        continue;

                    tracks.Add(new SpotifyTrack
                    {
                        Id = trackId,
                        Name = trackName,
                        Artist = artistName,
                        ArtistId = artistId
                    });
                }

                url = root.TryGetProperty("next", out var nextProp) &&
                      nextProp.ValueKind != JsonValueKind.Null
                    ? nextProp.GetString()?.Replace("https://api.spotify.com/v1/", "")
                    : null;
            }

            return tracks;
        }

        public async Task<List<SpotifyPlaylist>> GetUserPlaylistsAsync()
        {
            var playlists = new List<SpotifyPlaylist>();
            string? url = "me/playlists?limit=50";

            while (!string.IsNullOrEmpty(url))
            {
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                foreach (var item in root.GetProperty("items").EnumerateArray())
                {
                    playlists.Add(new SpotifyPlaylist
                    {
                        Id = item.GetProperty("id").GetString()!,
                        Name = item.GetProperty("name").GetString()!,
                        OwnerId = item.GetProperty("owner").GetProperty("id").GetString()!,
                        TotalTracks = item.GetProperty("tracks").GetProperty("total").GetInt32()
                    });
                }

                url = root.TryGetProperty("next", out var nextProp) &&
                      nextProp.ValueKind != JsonValueKind.Null
                    ? nextProp.GetString()?.Replace("https://api.spotify.com/v1/", "")
                    : null;
            }

            return playlists;
        }

        public async Task<List<SpotifyTrack>> GetPlaylistTracksAsync(string playlistId)
        {
            var tracks = new List<SpotifyTrack>();
            string? url = $"playlists/{playlistId}/tracks?limit=50";

            while (!string.IsNullOrEmpty(url))
            {
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                foreach (var item in root.GetProperty("items").EnumerateArray())
                {
                    if (!item.TryGetProperty("track", out var track) ||
                        track.ValueKind == JsonValueKind.Null)
                        continue;

                    var trackId = track.GetProperty("id").GetString();
                    var trackName = track.GetProperty("name").GetString();
                    var artists = track.GetProperty("artists").EnumerateArray();
                    var artistName = artists.First().GetProperty("name").GetString();
                    var artistId = artists.First().GetProperty("id").GetString();

                    if (trackId == null || trackName == null || artistName == null || artistId == null)
                        continue;

                    tracks.Add(new SpotifyTrack
                    {
                        Id = trackId,
                        Name = trackName,
                        Artist = artistName,
                        ArtistId = artistId
                    });
                }

                url = root.TryGetProperty("next", out var nextProp) &&
                      nextProp.ValueKind != JsonValueKind.Null
                    ? nextProp.GetString()?.Replace("https://api.spotify.com/v1/", "")
                    : null;
            }

            return tracks;
        }

        public async Task<List<string>> GetArtistGenresAsync(string artistId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"artists/{artistId}");
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(content);
                var genres = new List<string>();

                if (doc.RootElement.TryGetProperty("genres", out var genresArray))
                {
                    foreach (var genre in genresArray.EnumerateArray())
                    {
                        var genreStr = genre.GetString();
                        if (genreStr != null)
                            genres.Add(genreStr);
                    }
                }

                return genres;
            }
            catch
            {
                return new List<string>();
            }
        }

        public async Task<bool> AddTracksToLikedAsync(List<string> trackIds)
        {
            for (int i = 0; i < trackIds.Count; i += 50)
            {
                var batch = trackIds.Skip(i).Take(50).ToList();
                var ids = string.Join(",", batch);

                var response = await _httpClient.PutAsync($"me/tracks?ids={ids}", null);

                if (!response.IsSuccessStatusCode)
                    return false;

                await Task.Delay(100);
            }

            return true;
        }

        public async Task<string> CreatePlaylistAsync(string name, string description)
        {
            var userId = await GetUserIdAsync();

            var body = new
            {
                name = name,
                description = description,
                @public = false
            };

            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"users/{userId}/playlists", content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(responseContent);

            return doc.RootElement.GetProperty("id").GetString()!;
        }

        public async Task<bool> ClearPlaylistAsync(string playlistId)
        {
            var tracks = await GetPlaylistTracksAsync(playlistId);

            if (tracks.Count == 0)
                return true;

            for (int i = 0; i < tracks.Count; i += 100)
            {
                var batch = tracks.Skip(i).Take(100).ToList();
                var tracksToRemove = batch.Select(t => new { uri = $"spotify:track:{t.Id}" }).ToList();

                var body = new { tracks = tracksToRemove };
                var json = JsonSerializer.Serialize(body);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Delete, $"playlists/{playlistId}/tracks")
                {
                    Content = content
                };

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                    return false;
            }

            return true;
        }

        public async Task<bool> AddTracksToPlaylistAsync(string playlistId, List<string> trackIds)
        {
            for (int i = 0; i < trackIds.Count; i += 100)
            {
                var batch = trackIds.Skip(i).Take(100).ToList();
                var uris = batch.Select(id => $"spotify:track:{id}").ToList();

                var body = new { uris };
                var json = JsonSerializer.Serialize(body);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"playlists/{playlistId}/tracks", content);

                if (!response.IsSuccessStatusCode)
                    return false;
            }

            return true;
        }

        public async Task<SpotifyPlaylist?> FindPlaylistByNameAsync(string name)
        {
            var playlists = await GetUserPlaylistsAsync();
            return playlists.FirstOrDefault(p =>
                p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<bool> MovePlaylistToFolderAsync(string playlistId, string folderName)
        {
            // Not supported by Spotify's public API
            await Task.CompletedTask;
            return false;
        }

        public async Task<bool> DeletePlaylistAsync(string playlistId)
        {
            return await UnfollowPlaylistAsync(playlistId);
        }

        public async Task<bool> UnfollowPlaylistAsync(string playlistId)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"playlists/{playlistId}/followers");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}