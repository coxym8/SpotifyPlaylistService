using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SpotifyPlaylistService.Desktop.Views
{
    public partial class ConfirmDeleteWindow : Window
    {
        public ConfirmDeleteWindow()
        {
            InitializeComponent();
        }

        private void OnConfirm(object? sender, RoutedEventArgs e) => Close(true);

        private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
    }
}
