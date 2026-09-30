using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SpotifyPlaylistService.Desktop.Services;
using SpotifyPlaylistService.Desktop.ViewModels;
using SpotifyPlaylistService.Desktop.Views;
using SpotifyPlaylistService.Services;
using SpotifyPlaylistService.Storage;

namespace SpotifyPlaylistService.Desktop
{
    public partial class App : Application
    {
        public override void Initialize() => AvaloniaXamlLoader.Load(this);

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Composition root
                var auth = new SpotifyAuthService(new JsonSessionStore());
                var manager = new SpotifyManagerService(new SpotifyApiService(), new JsonArtistGenreCache());
                var state = new AppState(auth, manager, new GenreRuleStore());

                var window = new MainWindow();
                var viewModel = new MainWindowViewModel(state, new DialogService(() => window));
                window.DataContext = viewModel;
                window.Opened += async (_, _) => await viewModel.InitializeAsync();

                desktop.MainWindow = window;
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
