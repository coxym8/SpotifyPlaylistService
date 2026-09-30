using SpotifyPlaylistService.Desktop.Services;
using SpotifyPlaylistService.Desktop.ViewModels;
using SpotifyPlaylistService.Services;
using SpotifyPlaylistService.Storage;
using SpotifyPlaylistService.Tests.Fakes;

namespace SpotifyPlaylistService.Tests.Desktop
{
    public class FakeDialogs : IDialogService
    {
        public bool Answer { get; set; } = true;
        public List<IReadOnlyList<string>> Requests { get; } = new();

        public Task<bool> ConfirmDeleteAsync(IReadOnlyList<string> playlistNames)
        {
            Requests.Add(playlistNames);
            return Task.FromResult(Answer);
        }
    }

    /// <summary>
    /// A desktop view model graph wired to an in-memory Spotify account
    /// </summary>
    public sealed class DesktopFixture : IDisposable
    {
        private readonly TempDir _dir = new();

        public FakeSpotifyApi Api { get; } = new();
        public FakeDialogs Dialogs { get; } = new();
        public AppState State { get; }
        public MainWindowViewModel Main { get; }

        public DesktopFixture()
        {
            State = new AppState(new FakeAuthService(), new SpotifyManagerService(Api), new GenreRuleStore(_dir.File("rules.json")));
            Main = new MainWindowViewModel(State, Dialogs);
        }

        public void SeedLibrary()
        {
            var genres = new[]
            {
                "dance pop", "k-pop", "indie pop", "bedroom pop", "hard rock", "classic rock", "metalcore",
                "pop rap", "trap", "deep house", "techno", "neo soul", "jazz fusion", "gregorian chant"
            };
            for (int i = 0; i < 120; i++)
                Api.AddLiked($"t{i}", $"a{i % 40}", genres[i % genres.Length], genres[(i * 7) % genres.Length]);

            foreach (var name in new[] { "Pop", "Rock", "Hip Hop", "Electronic", "Metal", "Jazz", "R&B & Soul" })
                Api.AddPlaylist($"pl-{name}", GenrePlaylistNaming.PlaylistName(name), tracks: name.Length * 9);
            Api.AddPlaylist("mine", "Road trip mix");
        }

        public async Task SignInAndLoadAsync()
        {
            await State.CompleteSignInAsync();
            await Main.Library.LoadCommand.ExecuteAsync(null);
        }

        public void Dispose() => _dir.Dispose();
    }
}
