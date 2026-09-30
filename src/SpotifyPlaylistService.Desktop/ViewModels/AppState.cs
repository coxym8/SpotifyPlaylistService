using CommunityToolkit.Mvvm.ComponentModel;
using SpotifyPlaylistService.Models;
using SpotifyPlaylistService.Services;
using SpotifyPlaylistService.Storage;

namespace SpotifyPlaylistService.Desktop.ViewModels
{
    /// <summary>
    /// State shared between pages: services, the signed-in user, loaded tracks and collation rules
    /// </summary>
    public partial class AppState : ObservableObject
    {
        public ISpotifyAuthService Auth { get; }

        public ISpotifyManagerService Manager { get; }

        public GenreRuleStore RuleStore { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SignedInText))]
        private bool _isSignedIn;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SignedInText))]
        private string? _userId;

        [ObservableProperty]
        private string _statusMessage = "Sign in to get started.";

        [ObservableProperty]
        private IReadOnlyList<SpotifyTrack> _likedTracks = Array.Empty<SpotifyTrack>();

        [ObservableProperty]
        private GenreRuleSet _rules;

        public AppState(ISpotifyAuthService auth, ISpotifyManagerService manager, GenreRuleStore ruleStore)
        {
            Auth = auth;
            Manager = manager;
            RuleStore = ruleStore;
            _rules = ruleStore.Load();
        }

        public string SignedInText => IsSignedIn ? $"Signed in as {UserId}" : "Not signed in";

        public async Task CompleteSignInAsync(CancellationToken ct = default)
        {
            await Manager.InitializeAsync(Auth, ct);
            UserId = await Manager.GetCurrentUserIdAsync(ct);
            IsSignedIn = true;
            SetStatus($"Signed in as {UserId}.");
        }

        public void SignOut()
        {
            Auth.SignOut();
            IsSignedIn = false;
            UserId = null;
            LikedTracks = Array.Empty<SpotifyTrack>();
            SetStatus("Signed out.");
        }

        public void SetStatus(string message) => StatusMessage = message;
    }
}
