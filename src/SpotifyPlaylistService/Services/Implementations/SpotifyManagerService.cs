using SpotifyPlaylistService.Models;
using SpotifyPlaylistService.Services.Collation;
using SpotifyPlaylistService.Storage;

namespace SpotifyPlaylistService.Services
{
    /// <summary>
    /// Implementation of high-level Spotify playlist management
    /// </summary>
    public class SpotifyManagerService : ISpotifyManagerService
    {
        private const int ArtistLookupConcurrency = 4;

        private readonly ISpotifyApiService _apiService;
        private readonly IArtistGenreCache? _genreCache;

        public SpotifyManagerService(ISpotifyApiService apiService, IArtistGenreCache? genreCache = null)
        {
            _apiService = apiService;
            _genreCache = genreCache;
        }

        public async Task InitializeAsync(string accessToken)
        {
            _apiService.SetAccessToken(accessToken);
            await _apiService.GetUserIdAsync();
        }

        public async Task InitializeAsync(ISpotifyAuthService authService, CancellationToken ct = default)
        {
            if (authService.AccessToken == null)
                throw new InvalidOperationException("Sign in before initializing the manager.");

            _apiService.SetAccessToken(authService.AccessToken);
            _apiService.SetTokenRefresher(async refreshCt =>
                await authService.RefreshAccessTokenAsync(refreshCt) ? authService.AccessToken : null);

            await _apiService.GetUserIdAsync(ct);
        }

        public Task<string> GetCurrentUserIdAsync(CancellationToken ct = default) => _apiService.GetUserIdAsync(ct);

        public async Task<List<SpotifyTrack>> GetLikedSongsWithGenresAsync(IProgress<OperationProgress>? progress = null,
            CancellationToken ct = default)
        {
            var tracks = await _apiService.GetLikedSongsAsync(progress, ct);

            var artistIds = tracks.Select(t => t.ArtistId).Distinct().ToList();
            var artistGenres = new Dictionary<string, List<string>>();
            var toFetch = new List<string>();

            foreach (var artistId in artistIds)
            {
                if (_genreCache != null && _genreCache.TryGet(artistId, out var cached))
                    artistGenres[artistId] = cached;
                else
                    toFetch.Add(artistId);
            }

            // Spotify removed the batch artists endpoint, so look artists up individually
            // with bounded concurrency; the HTTP handler backs off on 429
            int completed = 0;
            using var gate = new SemaphoreSlim(ArtistLookupConcurrency);
            using var failFast = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var lookups = toFetch.Select(async artistId =>
            {
                await gate.WaitAsync(failFast.Token);
                try
                {
                    var genres = await _apiService.GetArtistGenresAsync(artistId, failFast.Token);
                    _genreCache?.Set(artistId, genres);

                    lock (artistGenres)
                        artistGenres[artistId] = genres;

                    var done = Interlocked.Increment(ref completed);
                    progress?.Report(new OperationProgress(done, toFetch.Count,
                        $"Looked up genres for {done} of {toFetch.Count} artists"));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Stop queued lookups; the first error is what the caller sees
                    failFast.Cancel();
                    throw;
                }
                finally
                {
                    gate.Release();
                }
            }).ToList();

            try
            {
                await Task.WhenAll(lookups);
            }
            finally
            {
                // Keep whatever was fetched, even if cancelled part-way
                _genreCache?.Save();
            }

            foreach (var track in tracks)
            {
                if (artistGenres.TryGetValue(track.ArtistId, out var genres))
                    track.Genres = genres;
            }

            return tracks;
        }

        public Task<List<SpotifyPlaylist>> GetUserPlaylistsAsync(CancellationToken ct = default) =>
            _apiService.GetUserPlaylistsAsync(ct);

        public async Task<bool> AddPlaylistTracksToLikedAsync(string playlistId, CancellationToken ct = default)
        {
            var tracks = await _apiService.GetPlaylistTracksAsync(playlistId, ct);
            var trackIds = tracks.Select(t => t.Id).ToList();

            return await _apiService.AddTracksToLikedAsync(trackIds, ct);
        }

        public async Task<bool> AddMultiplePlaylistsToLikedAsync(List<string> playlistIds, CancellationToken ct = default)
        {
            foreach (var playlistId in playlistIds)
            {
                var success = await AddPlaylistTracksToLikedAsync(playlistId, ct);
                if (!success)
                    return false;
            }

            return true;
        }

        public Dictionary<string, int> GetGenreStatistics(List<SpotifyTrack> tracks)
        {
            return tracks.SelectMany(t => t.Genres)
                         .GroupBy(g => g)
                         .OrderByDescending(g => g.Count())
                         .ToDictionary(g => g.Key, g => g.Count());
        }

        public List<GenreGroup> CollateGenres(List<SpotifyTrack> tracks, GenreRuleSet rules) =>
            new GenreCollator(rules).Collate(tracks);

        public async Task<string?> CreateOrUpdateGenrePlaylistAsync(string genre, List<SpotifyTrack> allTracks,
            CancellationToken ct = default)
        {
            var genreTracks = allTracks.Where(t => t.Genres.Any(g =>
                g.Equals(genre, StringComparison.OrdinalIgnoreCase))).ToList();

            if (genreTracks.Count == 0)
                return null;

            var existing = await GetExistingGenrePlaylistsByNameAsync(ct);
            return await SyncPlaylistAsync(genre, genreTracks, null, existing, ct);
        }

        public async Task<Dictionary<string, string>> CreateAllGenrePlaylistsAsync(List<SpotifyTrack> allTracks,
            IProgress<OperationProgress>? progress = null, CancellationToken ct = default)
        {
            var groups = GetGenreStatistics(allTracks).Keys.Select(genre =>
            {
                var group = new GenreGroup { Name = genre };
                group.SourceGenres.Add(genre);
                group.Tracks.AddRange(allTracks.Where(t => t.Genres.Contains(genre)));
                return group;
            });

            return await CreateCollatedPlaylistsAsync(groups, progress, ct);
        }

        public async Task<Dictionary<string, string>> CreateCollatedPlaylistsAsync(IEnumerable<GenreGroup> groups,
            IProgress<OperationProgress>? progress = null, CancellationToken ct = default)
        {
            var groupList = groups.Where(g => g.Tracks.Count > 0).ToList();
            var existing = await GetExistingGenrePlaylistsByNameAsync(ct);
            var result = new Dictionary<string, string>();

            for (int i = 0; i < groupList.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var group = groupList[i];
                progress?.Report(new OperationProgress(i, groupList.Count, $"Syncing {group.Name}"));

                var playlistId = await SyncPlaylistAsync(group.Name, group.Tracks, group.SourceGenres, existing, ct);
                if (playlistId != null)
                    result[group.Name] = playlistId;
            }

            progress?.Report(new OperationProgress(groupList.Count, groupList.Count,
                $"Created or updated {result.Count} playlists"));
            return result;
        }

        public async Task<List<SpotifyPlaylist>> GetGenrePlaylistsAsync(CancellationToken ct = default)
        {
            var userId = await _apiService.GetUserIdAsync(ct);
            var allPlaylists = await _apiService.GetUserPlaylistsAsync(ct);

            return allPlaylists.Where(p => IsCreatedByLibrary(p, userId)).ToList();
        }

        public async Task<BulkDeleteResult> DeleteGenrePlaylistsAsync(IEnumerable<SpotifyPlaylist> playlists,
            IProgress<OperationProgress>? progress = null, CancellationToken ct = default)
        {
            var userId = await _apiService.GetUserIdAsync(ct);
            var toDelete = playlists.DistinctBy(p => p.Id).ToList();
            var result = new BulkDeleteResult();

            for (int i = 0; i < toDelete.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var playlist = toDelete[i];
                progress?.Report(new OperationProgress(i, toDelete.Count, $"Deleting {playlist.Name}"));

                if (!IsCreatedByLibrary(playlist, userId))
                {
                    result.Failed.Add((playlist, "Not created by this app; skipped"));
                    continue;
                }

                try
                {
                    if (await _apiService.DeletePlaylistAsync(playlist.Id, ct))
                        result.Deleted.Add(playlist);
                    else
                        result.Failed.Add((playlist, "Playlist not found"));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    result.Failed.Add((playlist, ex.Message));
                }
            }

            progress?.Report(new OperationProgress(toDelete.Count, toDelete.Count,
                $"Deleted {result.Deleted.Count} of {toDelete.Count} playlists"));
            return result;
        }

        public async Task<int> DeleteAllGenrePlaylistsAsync(IProgress<OperationProgress>? progress = null,
            CancellationToken ct = default)
        {
            var genrePlaylists = await GetGenrePlaylistsAsync(ct);
            var result = await DeleteGenrePlaylistsAsync(genrePlaylists, progress, ct);
            return result.Deleted.Count;
        }

        public async Task<bool> DeleteGenrePlaylistAsync(string genre, CancellationToken ct = default)
        {
            var playlistName = GenrePlaylistNaming.PlaylistName(genre);
            var playlist = (await GetGenrePlaylistsAsync(ct)).FirstOrDefault(p =>
                p.Name.Equals(playlistName, StringComparison.OrdinalIgnoreCase));

            if (playlist == null)
                return false;

            var result = await DeleteGenrePlaylistsAsync(new[] { playlist }, null, ct);
            return result.Deleted.Count == 1;
        }

        private static bool IsCreatedByLibrary(SpotifyPlaylist playlist, string userId) =>
            playlist.OwnerId == userId && GenrePlaylistNaming.IsGenrePlaylistName(playlist.Name);

        /// <summary>
        /// Fetches the user's genre playlists once so a bulk sync does not re-list every playlist per genre
        /// </summary>
        private async Task<Dictionary<string, SpotifyPlaylist>> GetExistingGenrePlaylistsByNameAsync(CancellationToken ct)
        {
            var existing = new Dictionary<string, SpotifyPlaylist>(StringComparer.OrdinalIgnoreCase);
            foreach (var playlist in await GetGenrePlaylistsAsync(ct))
                existing.TryAdd(playlist.Name, playlist);
            return existing;
        }

        private async Task<string?> SyncPlaylistAsync(string genre, List<SpotifyTrack> tracks,
            IEnumerable<string>? sourceGenres, Dictionary<string, SpotifyPlaylist> existing, CancellationToken ct)
        {
            var playlistName = GenrePlaylistNaming.PlaylistName(genre);
            var trackIds = tracks.Select(t => t.Id).Distinct().ToList();

            string playlistId;
            if (existing.TryGetValue(playlistName, out var playlist))
            {
                playlistId = playlist.Id;
            }
            else
            {
                var description = GenrePlaylistNaming.Description(genre, trackIds.Count, sourceGenres);
                playlistId = await _apiService.CreatePlaylistAsync(playlistName, description, ct);
                existing[playlistName] = new SpotifyPlaylist
                {
                    Id = playlistId,
                    Name = playlistName,
                    OwnerId = await _apiService.GetUserIdAsync(ct),
                    TotalTracks = trackIds.Count
                };
            }

            var success = await _apiService.ReplacePlaylistTracksAsync(playlistId, trackIds, ct);
            return success ? playlistId : null;
        }
    }
}
