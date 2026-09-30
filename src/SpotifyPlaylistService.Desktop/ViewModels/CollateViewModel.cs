using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpotifyPlaylistService.Models;

namespace SpotifyPlaylistService.Desktop.ViewModels
{
    /// <summary>
    /// Editable view of one <see cref="GenreRule"/>; lists are edited as comma-separated text
    /// </summary>
    public partial class RuleItem : ObservableObject
    {
        [ObservableProperty]
        private string _group = "";

        [ObservableProperty]
        private string _keywordsText = "";

        [ObservableProperty]
        private string _exactText = "";

        public static RuleItem FromModel(GenreRule rule) => new()
        {
            Group = rule.Group,
            KeywordsText = string.Join(", ", rule.Keywords),
            ExactText = string.Join(", ", rule.Exact)
        };

        public GenreRule ToModel() => new()
        {
            Group = Group.Trim(),
            Keywords = Split(KeywordsText),
            Exact = Split(ExactText)
        };

        private static List<string> Split(string text) =>
            text.Split(new[] { ',', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
    }

    public partial class GroupItem : ObservableObject
    {
        private readonly Action _selectionChanged;

        public GenreGroup Group { get; }

        [ObservableProperty]
        private bool _isSelected = true;

        public GroupItem(GenreGroup group, Action selectionChanged)
        {
            Group = group;
            _selectionChanged = selectionChanged;
        }

        public string Name => Group.Name;

        public int TrackCount => Group.Tracks.Count;

        public string Details => Group.SourceGenres.Count == 0
            ? "Artists with no Spotify genre"
            : $"{Group.SourceGenres.Count} genre{(Group.SourceGenres.Count == 1 ? "" : "s")}: {string.Join(", ", Group.SourceGenres.Take(12))}" +
              (Group.SourceGenres.Count > 12 ? ", ..." : "");

        partial void OnIsSelectedChanged(bool value) => _selectionChanged();
    }

    public partial class CollateViewModel : ViewModelBase
    {
        private readonly AppState _state;
        private bool _loadingOptions;

        public ObservableCollection<RuleItem> Rules { get; } = new();

        public ObservableCollection<GroupItem> Groups { get; } = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RemoveRuleCommand), nameof(MoveRuleUpCommand), nameof(MoveRuleDownCommand))]
        private RuleItem? _selectedRule;

        [ObservableProperty]
        private int _minTracks;

        [ObservableProperty]
        private bool _includeOther;

        [ObservableProperty]
        private bool _includeUnclassified;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(MergeSelectedCommand))]
        private string _customGroupName = "";

        [ObservableProperty]
        private string _summaryText = "";

        [ObservableProperty]
        private double _progressValue;

        [ObservableProperty]
        private double _progressMaximum = 1;

        public CollateViewModel(AppState state)
        {
            _state = state;
            _state.PropertyChanged += OnStateChanged;
            LoadRules(state.Rules);
            RefreshPreview();
        }

        public string RulesFilePath => _state.RuleStore.FilePath;

        public bool HasTracks => _state.LikedTracks.Count > 0;

        public int SelectedCount => Groups.Count(g => g.IsSelected);

        [RelayCommand]
        private void AddRule()
        {
            var rule = new RuleItem { Group = "New group" };
            Rules.Insert(0, rule);
            SelectedRule = rule;
        }

        [RelayCommand(CanExecute = nameof(HasSelectedRule))]
        private void RemoveRule()
        {
            if (SelectedRule != null)
                Rules.Remove(SelectedRule);
        }

        [RelayCommand(CanExecute = nameof(HasSelectedRule))]
        private void MoveRuleUp() => MoveSelected(-1);

        [RelayCommand(CanExecute = nameof(HasSelectedRule))]
        private void MoveRuleDown() => MoveSelected(1);

        private bool HasSelectedRule() => SelectedRule != null;

        /// <summary>
        /// Applies the edited rules to the preview without saving them
        /// </summary>
        [RelayCommand]
        private void ApplyRules()
        {
            _state.Rules = BuildRuleSet();
            _state.SetStatus("Rules applied to preview.");
        }

        [RelayCommand]
        private void SaveRules()
        {
            var rules = BuildRuleSet();
            try
            {
                _state.RuleStore.Save(rules);
                _state.Rules = rules;
                _state.SetStatus($"Rules saved to {RulesFilePath}.");
            }
            catch (Exception ex)
            {
                _state.SetStatus($"Could not save rules: {ex.Message}");
            }
        }

        [RelayCommand]
        private void ResetRules()
        {
            var defaults = GenreRuleSet.CreateDefault();
            LoadRules(defaults);
            _state.Rules = defaults;
            _state.SetStatus("Rules reset to defaults (not saved yet).");
        }

        /// <summary>
        /// Merges the checked groups into one custom group by adding a top-priority rule
        /// that claims all of their Spotify genres
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanMerge))]
        private void MergeSelected()
        {
            var genres = Groups.Where(g => g.IsSelected).SelectMany(g => g.Group.SourceGenres).ToList();
            var rules = BuildRuleSet();
            rules.AddCustomGroup(CustomGroupName.Trim(), genres);

            LoadRules(rules);
            _state.Rules = rules;
            _state.SetStatus($"Merged {genres.Count} genres into '{CustomGroupName.Trim()}'. Save rules to keep it.");
            CustomGroupName = "";
        }

        private bool CanMerge() => !string.IsNullOrWhiteSpace(CustomGroupName) && SelectedCount > 0;

        [RelayCommand]
        private void SelectAll() => SetAllSelected(true);

        [RelayCommand]
        private void SelectNone() => SetAllSelected(false);

        [RelayCommand(CanExecute = nameof(CanCreate), IncludeCancelCommand = true)]
        private async Task CreatePlaylistsAsync(CancellationToken ct)
        {
            var selected = Groups.Where(g => g.IsSelected).Select(g => g.Group).ToList();
            var progress = new Progress<OperationProgress>(p =>
            {
                ProgressMaximum = Math.Max(1, p.Total);
                ProgressValue = p.Completed;
                _state.SetStatus(p.Message);
            });

            try
            {
                var created = await _state.Manager.CreateCollatedPlaylistsAsync(selected, progress, ct);
                _state.SetStatus($"Created or updated {created.Count} of {selected.Count} playlists.");
            }
            catch (OperationCanceledException)
            {
                _state.SetStatus("Playlist creation cancelled.");
            }
            catch (Exception ex)
            {
                _state.SetStatus($"Playlist creation failed: {ex.Message}");
            }
        }

        private bool CanCreate() => _state.IsSignedIn && SelectedCount > 0;

        partial void OnMinTracksChanged(int value) => OnOptionChanged();

        partial void OnIncludeOtherChanged(bool value) => OnOptionChanged();

        partial void OnIncludeUnclassifiedChanged(bool value) => OnOptionChanged();

        private void OnOptionChanged()
        {
            if (!_loadingOptions)
                _state.Rules = BuildRuleSet();
        }

        private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(AppState.LikedTracks):
                    OnPropertyChanged(nameof(HasTracks));
                    RefreshPreview();
                    break;
                case nameof(AppState.Rules):
                    RefreshPreview();
                    break;
                case nameof(AppState.IsSignedIn):
                    CreatePlaylistsCommand.NotifyCanExecuteChanged();
                    break;
            }
        }

        private void RefreshPreview()
        {
            Groups.Clear();
            foreach (var group in _state.Manager.CollateGenres(_state.LikedTracks.ToList(), _state.Rules))
                Groups.Add(new GroupItem(group, OnGroupSelectionChanged));

            SummaryText = HasTracks
                ? $"{Groups.Count} playlists from {_state.LikedTracks.Count} liked songs"
                : "Load your liked songs on the Library page to preview playlists.";
            OnGroupSelectionChanged();
        }

        private void OnGroupSelectionChanged()
        {
            OnPropertyChanged(nameof(SelectedCount));
            CreatePlaylistsCommand.NotifyCanExecuteChanged();
            MergeSelectedCommand.NotifyCanExecuteChanged();
        }

        private void SetAllSelected(bool selected)
        {
            foreach (var group in Groups)
                group.IsSelected = selected;
        }

        private void LoadRules(GenreRuleSet rules)
        {
            Rules.Clear();
            foreach (var rule in rules.Rules)
                Rules.Add(RuleItem.FromModel(rule));

            _loadingOptions = true;
            MinTracks = rules.MinTracks;
            IncludeOther = rules.IncludeOther;
            IncludeUnclassified = rules.IncludeUnclassified;
            _loadingOptions = false;
        }

        private GenreRuleSet BuildRuleSet() => new()
        {
            Rules = Rules.Select(r => r.ToModel()).Where(r => r.Group.Length > 0).ToList(),
            MinTracks = MinTracks,
            IncludeOther = IncludeOther,
            IncludeUnclassified = IncludeUnclassified
        };

        private void MoveSelected(int offset)
        {
            if (SelectedRule == null)
                return;

            var index = Rules.IndexOf(SelectedRule);
            var target = index + offset;
            if (target < 0 || target >= Rules.Count)
                return;

            var rule = SelectedRule;
            Rules.Move(index, target);
            SelectedRule = rule;
        }
    }
}
