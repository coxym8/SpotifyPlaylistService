using CommunityToolkit.Mvvm.ComponentModel;

namespace SpotifyPlaylistService.Desktop.ViewModels
{
    public partial class ConfirmDeleteViewModel : ViewModelBase
    {
        /// <summary>
        /// Deleting more than this many playlists requires typing a confirmation word
        /// </summary>
        public const int TypedConfirmationThreshold = 10;
        public const string ConfirmationWord = "DELETE";

        public IReadOnlyList<string> PlaylistNames { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanConfirm))]
        private string _confirmationText = "";

        public ConfirmDeleteViewModel(IReadOnlyList<string> playlistNames)
        {
            PlaylistNames = playlistNames;
        }

        public string Title => $"Delete {PlaylistNames.Count} playlist{(PlaylistNames.Count == 1 ? "" : "s")}?";

        public bool RequiresTypedConfirmation => PlaylistNames.Count > TypedConfirmationThreshold;

        public string ConfirmationPrompt => $"Type {ConfirmationWord} to confirm";

        public bool CanConfirm => !RequiresTypedConfirmation || ConfirmationText.Trim() == ConfirmationWord;
    }
}
