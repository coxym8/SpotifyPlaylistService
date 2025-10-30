using SpotifyPlaylistService.Models;

namespace SpotifyPlaylistService.Services
{
    /// <summary>
    /// Implementation of high-level Spotify playlist management
    /// </summary>
    public class SpotifyManagerService : ISpotifyManagerService
    {
        private readonly ISpotifyApiService _apiService;

        public SpotifyManagerService(ISpotifyApiService apiService)
        {
            _apiService = apiService;
        }

        public async Task InitializeAsync(string accessToken)
        {
            _apiService.SetAccessToken(accessToken);
            await _apiService.GetUserIdAsync();
        }

        public async Task<List<SpotifyTrack>> GetLikedSongsWithGenresAsync()
        {
            var tracks = await _apiService.GetLikedSongsAsync();

            var uniqueArtists = tracks.GroupBy(t => t.ArtistId)
                                     .Select(g => g.First())
                                     .ToList();

            var artistGenres = new Dictionary<string, List<string>>();

            foreach (var track in uniqueArtists)
            {
                var genres = await _apiService.GetArtistGenresAsync(track.ArtistId);
                artistGenres[track.ArtistId] = genres;
            }

            foreach (var track in tracks)
            {
                if (artistGenres.ContainsKey(track.ArtistId))
                {
                    track.Genres = artistGenres[track.ArtistId];
                }
            }

            return tracks;
        }

        public async Task<List<SpotifyPlaylist>> GetUserPlaylistsAsync()
        {
            return await _apiService.GetUserPlaylistsAsync();
        }

        public async Task<bool> AddPlaylistTracksToLikedAsync(string playlistId)
        {
            var tracks = await _apiService.GetPlaylistTracksAsync(playlistId);
            var trackIds = tracks.Select(t => t.Id).ToList();

            return await _apiService.AddTracksToLikedAsync(trackIds);
        }

        public async Task<bool> AddMultiplePlaylistsToLikedAsync(List<string> playlistIds)
        {
            foreach (var playlistId in playlistIds)
            {
                var success = await AddPlaylistTracksToLikedAsync(playlistId);
                if (!success)
                    return false;
            }

            return true;
        }

        public Dictionary<string, int> GetGenreStatistics(List<SpotifyTrack> tracks)
        {
            var genreCounts = new Dictionary<string, int>();

            foreach (var track in tracks)
            {
                foreach (var genre in track.Genres)
                {
                    if (genreCounts.ContainsKey(genre))
                        genreCounts[genre]++;
                    else
                        genreCounts[genre] = 1;
                }
            }

            return genreCounts.OrderByDescending(x => x.Value)
                             .ToDictionary(x => x.Key, x => x.Value);
        }

        public async Task<string?> CreateOrUpdateGenrePlaylistAsync(string genre,
            List<SpotifyTrack> allTracks)
        {
            var playlistName = $"[Genre] {genre}";
            var genreTracks = allTracks.Where(t => t.Genres.Any(g =>
                g.Equals(genre, StringComparison.OrdinalIgnoreCase))).ToList();

            if (genreTracks.Count == 0)
                return null;

            var existingPlaylist = await _apiService.FindPlaylistByNameAsync(playlistName);

            string playlistId;

            if (existingPlaylist != null)
            {
                await _apiService.ClearPlaylistAsync(existingPlaylist.Id);
                playlistId = existingPlaylist.Id;
            }
            else
            {
                var description = $"Auto-generated genre playlist for {genre} ({genreTracks.Count} tracks). " +
                                $"Organize under 'Genre' folder in your Spotify client.";
                playlistId = await _apiService.CreatePlaylistAsync(playlistName, description);
            }

            var trackIds = genreTracks.Select(t => t.Id).ToList();
            await _apiService.AddTracksToPlaylistAsync(playlistId, trackIds);

            return playlistId;
        }

        public async Task<Dictionary<string, string>> CreateAllGenrePlaylistsAsync(
            List<SpotifyTrack> allTracks)
        {
            var genreStats = GetGenreStatistics(allTracks);
            var result = new Dictionary<string, string>();

            foreach (var genre in genreStats.Keys)
            {
                var playlistId = await CreateOrUpdateGenrePlaylistAsync(genre, allTracks);
                if (!string.IsNullOrEmpty(playlistId))
                {
                    result[genre] = playlistId;
                }
            }

            return result;
        }

        public async Task<List<SpotifyPlaylist>> GetGenrePlaylistsAsync()
        {
            var allPlaylists = await _apiService.GetUserPlaylistsAsync();
            return allPlaylists.Where(p => p.Name.StartsWith("[Genre]")).ToList();
        }

        public async Task<int> DeleteAllGenrePlaylistsAsync()
        {
            var genrePlaylists = await GetGenrePlaylistsAsync();
            int deletedCount = 0;

            foreach (var playlist in genrePlaylists)
            {
                var success = await _apiService.DeletePlaylistAsync(playlist.Id);
                if (success)
                {
                    deletedCount++;
                }

                await Task.Delay(100);
            }

            return deletedCount;
        }

        public async Task<bool> DeleteGenrePlaylistAsync(string genre)
        {
            var playlistName = $"[Genre] {genre}";
            var playlist = await _apiService.FindPlaylistByNameAsync(playlistName);

            if (playlist == null)
                return false;

            return await _apiService.DeletePlaylistAsync(playlist.Id);
        }
    }
}