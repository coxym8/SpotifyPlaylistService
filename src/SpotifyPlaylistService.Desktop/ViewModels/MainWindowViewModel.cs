using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SpotifyPlaylistService.Desktop.Services;

namespace SpotifyPlaylistService.Desktop.ViewModels
{
    public record PageItem(string Title, string Subtitle, ViewModelBase ViewModel);

    public partial class MainWindowViewModel : ViewModelBase
    {
        public AppState State { get; }

        public ConnectViewModel Connect { get; }

        public LibraryViewModel Library { get; }

        public CollateViewModel Collate { get; }

        public ManagePlaylistsViewModel Manage { get; }

        public ObservableCollection<PageItem> Pages { get; }

        [ObservableProperty]
        private PageItem _selectedPage;

        public MainWindowViewModel(AppState state, IDialogService dialogs)
        {
            State = state;
            Connect = new ConnectViewModel(state);
            Library = new LibraryViewModel(state);
            Collate = new CollateViewModel(state);
            Manage = new ManagePlaylistsViewModel(state, dialogs);

            Pages = new ObservableCollection<PageItem>
            {
                new("Connect", "Sign in to Spotify", Connect),
                new("Library", "Liked songs & genres", Library),
                new("Collate", "Build genre playlists", Collate),
                new("Manage", "Bulk delete playlists", Manage),
            };
            _selectedPage = Pages[0];

            State.PropertyChanged += OnStateChanged;
        }

        public Task InitializeAsync() => Connect.TryRestoreSessionAsync();

        partial void OnSelectedPageChanged(PageItem value)
        {
            if (value?.ViewModel is INavigationAware page)
                _ = page.OnNavigatedToAsync();
        }

        private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(AppState.IsSignedIn))
                return;

            SelectedPage = State.IsSignedIn ? Pages[1] : Pages[0];
        }
    }
}
