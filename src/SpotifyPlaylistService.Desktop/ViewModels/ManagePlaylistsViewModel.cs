using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpotifyPlaylistService.Desktop.Services;
using SpotifyPlaylistService.Models;

namespace SpotifyPlaylistService.Desktop.ViewModels
{
    public partial class PlaylistItem : ObservableObject
    {
        private readonly Action _selectionChanged;

        public SpotifyPlaylist Playlist { get; }

        [ObservableProperty]
        private bool _isSelected;

        public PlaylistItem(SpotifyPlaylist playlist, Action selectionChanged)
        {
            Playlist = playlist;
            _selectionChanged = selectionChanged;
        }

        public string Name => Playlist.Name;

        public int TrackCount => Playlist.TotalTracks;

        partial void OnIsSelectedChanged(bool value) => _selectionChanged();
    }

    public partial class ManagePlaylistsViewModel : ViewModelBase, INavigationAware
    {
        private readonly AppState _state;
        private readonly IDialogService _dialogs;
        private readonly List<PlaylistItem> _all = new();

        public ObservableCollection<PlaylistItem> Playlists { get; } = new();

        [ObservableProperty]
        private string _filterText = "";

        [ObservableProperty]
        private string _summaryText = "";

        [ObservableProperty]
        private string? _lastResult;

        [ObservableProperty]
        private double _progressValue;

        [ObservableProperty]
        private double _progressMaximum = 1;

        public ManagePlaylistsViewModel(AppState state, IDialogService dialogs)
        {
            _state = state;
            _dialogs = dialogs;
            _state.PropertyChanged += OnStateChanged;
            UpdateSummary();
        }

        public int SelectedCount => _all.Count(p => p.IsSelected);

        public async Task OnNavigatedToAsync()
        {
            if (_state.IsSignedIn && RefreshCommand.CanExecute(null))
                await RefreshCommand.ExecuteAsync(null);
        }

        [RelayCommand(CanExecute = nameof(IsSignedIn))]
        private async Task RefreshAsync(CancellationToken ct)
        {
            try
            {
                var playlists = await _state.Manager.GetGenrePlaylistsAsync(ct);
                _all.Clear();
                _all.AddRange(playlists.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                                       .Select(p => new PlaylistItem(p, OnSelectionChanged)));
                ApplyFilter();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _state.SetStatus($"Could not load playlists: {ex.Message}");
            }
        }

        private bool IsSignedIn() => _state.IsSignedIn;

        [RelayCommand]
        private void SelectAll()
        {
            foreach (var item in Playlists)
                item.IsSelected = true;
        }

        [RelayCommand]
        private void SelectNone()
        {
            foreach (var item in _all)
                item.IsSelected = false;
        }

        [RelayCommand(CanExecute = nameof(CanDelete), IncludeCancelCommand = true)]
        private async Task DeleteSelectedAsync(CancellationToken ct)
        {
            var selected = _all.Where(p => p.IsSelected).ToList();
            if (!await _dialogs.ConfirmDeleteAsync(selected.Select(p => p.Name).ToList()))
                return;

            var progress = new Progress<OperationProgress>(p =>
            {
                ProgressMaximum = Math.Max(1, p.Total);
                ProgressValue = p.Completed;
                _state.SetStatus(p.Message);
            });

            try
            {
                var result = await _state.Manager.DeleteGenrePlaylistsAsync(selected.Select(p => p.Playlist), progress, ct);

                var deletedIds = result.Deleted.Select(p => p.Id).ToHashSet();
                _all.RemoveAll(p => deletedIds.Contains(p.Playlist.Id));
                ApplyFilter();

                LastResult = result.Failed.Count == 0
                    ? $"Deleted {result.Deleted.Count} playlists."
                    : $"Deleted {result.Deleted.Count}; {result.Failed.Count} failed:\n" +
                      string.Join("\n", result.Failed.Select(f => $"• {f.Playlist.Name}: {f.Reason}"));
                _state.SetStatus($"Deleted {result.Deleted.Count} of {selected.Count} playlists.");
            }
            catch (OperationCanceledException)
            {
                _state.SetStatus("Delete cancelled; refresh to see what remains.");
                await RefreshAsync(CancellationToken.None);
            }
        }

        private bool CanDelete() => _state.IsSignedIn && SelectedCount > 0;

        partial void OnFilterTextChanged(string value) => ApplyFilter();

        private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(AppState.IsSignedIn))
                return;

            if (!_state.IsSignedIn)
            {
                _all.Clear();
                ApplyFilter();
            }

            RefreshCommand.NotifyCanExecuteChanged();
            DeleteSelectedCommand.NotifyCanExecuteChanged();
        }

        private void ApplyFilter()
        {
            Playlists.Clear();
            foreach (var item in _all.Where(p => string.IsNullOrWhiteSpace(FilterText) ||
                                                 p.Name.Contains(FilterText.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                Playlists.Add(item);
            }

            OnSelectionChanged();
        }

        private void OnSelectionChanged()
        {
            OnPropertyChanged(nameof(SelectedCount));
            UpdateSummary();
            DeleteSelectedCommand.NotifyCanExecuteChanged();
        }

        private void UpdateSummary() =>
            SummaryText = $"{_all.Count} playlists created by this app · {SelectedCount} selected";
    }
}
