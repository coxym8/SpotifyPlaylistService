using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpotifyPlaylistService.Services;

namespace SpotifyPlaylistService.Desktop.ViewModels
{
    public partial class ConnectViewModel : ViewModelBase
    {
        public AppState State { get; }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SignInCommand))]
        private string _clientId;

        [ObservableProperty]
        private string? _authorizationUrl;

        public ConnectViewModel(AppState state)
        {
            State = state;
            _clientId = state.Auth.ClientId ?? Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_ID") ?? "";
            state.Auth.AuthorizationUrlReady += url => AuthorizationUrl = url;
        }

        public string RedirectUri => SpotifyAuthService.RedirectUri;

        public async Task TryRestoreSessionAsync()
        {
            try
            {
                State.SetStatus("Restoring saved session...");
                if (await State.Auth.TryRestoreSessionAsync())
                {
                    ClientId = State.Auth.ClientId ?? ClientId;
                    await State.CompleteSignInAsync();
                }
                else
                {
                    State.SetStatus("Sign in to get started.");
                }
            }
            catch (Exception ex)
            {
                State.SetStatus($"Could not restore session: {ex.Message}");
            }
        }

        [RelayCommand(CanExecute = nameof(CanSignIn), IncludeCancelCommand = true)]
        private async Task SignInAsync(CancellationToken ct)
        {
            try
            {
                State.SetStatus("Waiting for you to approve access in the browser...");
                if (await State.Auth.AuthenticateWithPkceAsync(ClientId.Trim(), ct))
                    await State.CompleteSignInAsync(ct);
                else
                    State.SetStatus("Sign-in failed. Check the Client ID and redirect URI.");
            }
            catch (OperationCanceledException)
            {
                State.SetStatus("Sign-in cancelled.");
            }
            catch (Exception ex)
            {
                State.SetStatus($"Sign-in failed: {ex.Message}");
            }
            finally
            {
                AuthorizationUrl = null;
            }
        }

        private bool CanSignIn() => !string.IsNullOrWhiteSpace(ClientId);

        [RelayCommand]
        private void SignOut() => State.SignOut();
    }
}
