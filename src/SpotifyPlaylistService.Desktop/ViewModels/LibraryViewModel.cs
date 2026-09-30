using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpotifyPlaylistService.Models;
using SpotifyPlaylistService.Services.Collation;

namespace SpotifyPlaylistService.Desktop.ViewModels
{
    public record GenreRow(string Genre, string Group, int Tracks);

    public partial class LibraryViewModel : ViewModelBase
    {
        private readonly AppState _state;
        private List<GenreRow> _allRows = new();

        public ObservableCollection<GenreRow> Genres { get; } = new();

        [ObservableProperty]
        private string _filterText = "";

        [ObservableProperty]
        private string _summaryText = "Load your liked songs to see their genres.";

        [ObservableProperty]
        private double _progressValue;

        [ObservableProperty]
        private double _progressMaximum = 1;

        [ObservableProperty]
        private string? _progressText;

        public LibraryViewModel(AppState state)
        {
            _state = state;
            _state.PropertyChanged += OnStateChanged;
        }

        [RelayCommand(CanExecute = nameof(CanLoad), IncludeCancelCommand = true)]
        private async Task LoadAsync(CancellationToken ct)
        {
            var progress = new Progress<OperationProgress>(p =>
            {
                ProgressMaximum = Math.Max(1, p.Total);
                ProgressValue = p.Completed;
                ProgressText = p.Message;
            });

            try
            {
                _state.SetStatus("Loading liked songs...");
                var tracks = await _state.Manager.GetLikedSongsWithGenresAsync(progress, ct);
                _state.LikedTracks = tracks;
                _state.SetStatus($"Loaded {tracks.Count} liked songs.");
            }
            catch (OperationCanceledException)
            {
                _state.SetStatus("Loading cancelled.");
            }
            catch (Exception ex)
            {
                _state.SetStatus($"Loading failed: {ex.Message}");
            }
            finally
            {
                ProgressText = null;
            }
        }

        private bool CanLoad() => _state.IsSignedIn;

        partial void OnFilterTextChanged(string value) => ApplyFilter();

        private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(AppState.IsSignedIn):
                    LoadCommand.NotifyCanExecuteChanged();
                    break;
                case nameof(AppState.LikedTracks):
                case nameof(AppState.Rules):
                    Rebuild();
                    break;
            }
        }

        private void Rebuild()
        {
            var tracks = _state.LikedTracks;
            var collator = new GenreCollator(_state.Rules);

            _allRows = tracks.SelectMany(t => t.Genres)
                             .GroupBy(g => g, StringComparer.OrdinalIgnoreCase)
                             .Select(g => new GenreRow(g.Key, collator.MapGenre(g.Key) ?? GenreRuleSet.OtherGroup, g.Count()))
                             .OrderByDescending(r => r.Tracks)
                             .ToList();

            var artists = tracks.Select(t => t.ArtistId).Distinct().Count();
            var noGenre = tracks.Count(t => t.Genres.Count == 0);
            SummaryText = tracks.Count == 0
                ? "Load your liked songs to see their genres."
                : $"{tracks.Count} songs · {artists} artists · {_allRows.Count} genres · {noGenre} songs without genre";

            ApplyFilter();
        }

        private void ApplyFilter()
        {
            Genres.Clear();
            foreach (var row in _allRows.Where(r => string.IsNullOrWhiteSpace(FilterText) ||
                                                    r.Genre.Contains(FilterText.Trim(), StringComparison.OrdinalIgnoreCase) ||
                                                    r.Group.Contains(FilterText.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                Genres.Add(row);
            }
        }
    }
}
