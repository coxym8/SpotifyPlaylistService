using SpotifyPlaylistService.Models;
using SpotifyPlaylistService.Services;

namespace SpotifyPlaylistService.Tests.Fakes
{
    /// <summary>
    /// In-memory Spotify account for manager and view model tests
    /// </summary>
    public class FakeSpotifyApi : ISpotifyApiService
    {
        public const string UserId = "me";

        public List<SpotifyTrack> LikedSongs { get; } = new();
        public List<SpotifyPlaylist> Playlists { get; } = new();
        public Dictionary<string, List<string>> PlaylistTracks { get; } = new();
        public Dictionary<string, List<string>> ArtistGenres { get; } = new();
        public List<string> ArtistLookups { get; } = new();
        public HashSet<string> FailingDeletes { get; } = new();
        public int PlaylistListCalls { get; private set; }
        public int CreatedCount { get; private set; }

        public void SetAccessToken(string token) { }

        public void SetTokenRefresher(Func<CancellationToken, Task<string?>> refresher) { }

        public Task<string> GetUserIdAsync(CancellationToken ct = default) => Task.FromResult(UserId);

        public Task<List<SpotifyTrack>> GetLikedSongsAsync(IProgress<OperationProgress>? progress = null,
            CancellationToken ct = default) =>
            Task.FromResult(LikedSongs.Select(t => new SpotifyTrack
            {
                Id = t.Id, Name = t.Name, Artist = t.Artist, ArtistId = t.ArtistId
            }).ToList());

        public Task<List<SpotifyPlaylist>> GetUserPlaylistsAsync(CancellationToken ct = default)
        {
            PlaylistListCalls++;
            return Task.FromResult(Playlists.ToList());
        }

        public Task<List<SpotifyTrack>> GetPlaylistTracksAsync(string playlistId, CancellationToken ct = default) =>
            Task.FromResult(new List<SpotifyTrack>());

        public Task<List<string>> GetArtistGenresAsync(string artistId, CancellationToken ct = default)
        {
            lock (ArtistLookups)
                ArtistLookups.Add(artistId);
            return Task.FromResult(ArtistGenres.TryGetValue(artistId, out var g) ? g : new List<string>());
        }

        public Task<bool> AddTracksToLikedAsync(List<string> trackIds, CancellationToken ct = default) =>
            Task.FromResult(true);

        public Task<string> CreatePlaylistAsync(string name, string description, CancellationToken ct = default)
        {
            CreatedCount++;
            var id = $"created-{CreatedCount}";
            Playlists.Add(new SpotifyPlaylist { Id = id, Name = name, OwnerId = UserId, Description = description });
            return Task.FromResult(id);
        }

        public Task<bool> ClearPlaylistAsync(string playlistId, CancellationToken ct = default) =>
            ReplacePlaylistTracksAsync(playlistId, new List<string>(), ct);

        public Task<bool> AddTracksToPlaylistAsync(string playlistId, List<string> trackIds, CancellationToken ct = default)
        {
            PlaylistTracks.TryAdd(playlistId, new List<string>());
            PlaylistTracks[playlistId].AddRange(trackIds);
            return Task.FromResult(true);
        }

        public Task<bool> ReplacePlaylistTracksAsync(string playlistId, List<string> trackIds, CancellationToken ct = default)
        {
            PlaylistTracks[playlistId] = trackIds.ToList();
            return Task.FromResult(true);
        }

        public Task<SpotifyPlaylist?> FindPlaylistByNameAsync(string name, CancellationToken ct = default) =>
            Task.FromResult(Playlists.FirstOrDefault(p => p.Name == name));

        public Task<bool> MovePlaylistToFolderAsync(string playlistId, string folderName) => Task.FromResult(false);

        public Task<bool> DeletePlaylistAsync(string playlistId, CancellationToken ct = default)
        {
            if (FailingDeletes.Contains(playlistId))
                throw new Http.SpotifyApiException(System.Net.HttpStatusCode.Forbidden, "Forbidden");

            return Task.FromResult(Playlists.RemoveAll(p => p.Id == playlistId) > 0);
        }

        public Task<bool> UnfollowPlaylistAsync(string playlistId, CancellationToken ct = default) =>
            DeletePlaylistAsync(playlistId, ct);

        public void AddLiked(string id, string artistId, params string[] genres)
        {
            LikedSongs.Add(new SpotifyTrack { Id = id, Name = $"Song {id}", Artist = $"Artist {artistId}", ArtistId = artistId });
            ArtistGenres[artistId] = genres.ToList();
        }

        public SpotifyPlaylist AddPlaylist(string id, string name, string owner = UserId, int tracks = 10)
        {
            var playlist = new SpotifyPlaylist { Id = id, Name = name, OwnerId = owner, TotalTracks = tracks };
            Playlists.Add(playlist);
            return playlist;
        }
    }
}
