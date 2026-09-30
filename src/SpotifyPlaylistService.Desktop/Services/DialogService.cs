using Avalonia.Controls;
using SpotifyPlaylistService.Desktop.ViewModels;
using SpotifyPlaylistService.Desktop.Views;

namespace SpotifyPlaylistService.Desktop.Services
{
    public class DialogService : IDialogService
    {
        private readonly Func<Window> _owner;

        public DialogService(Func<Window> owner)
        {
            _owner = owner;
        }

        public async Task<bool> ConfirmDeleteAsync(IReadOnlyList<string> playlistNames)
        {
            var dialog = new ConfirmDeleteWindow
            {
                DataContext = new ConfirmDeleteViewModel(playlistNames)
            };

            return await dialog.ShowDialog<bool>(_owner());
        }
    }
}
