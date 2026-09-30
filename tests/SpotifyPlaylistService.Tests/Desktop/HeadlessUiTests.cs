using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SpotifyPlaylistService.Desktop.ViewModels;
using SpotifyPlaylistService.Desktop.Views;

namespace SpotifyPlaylistService.Tests.Desktop
{
    /// <summary>
    /// Renders the real windows headlessly. Set SCREENSHOT_DIR to save a PNG of each page.
    /// </summary>
    public class HeadlessUiTests
    {
        [AvaloniaFact]
        public async Task EveryPageRenders()
        {
            using var f = new DesktopFixture();
            f.SeedLibrary();
            var window = new MainWindow { DataContext = f.Main, Width = 1180, Height = 760 };
            window.Show();

            Capture(window, "1-connect");
            Assert.Contains(window.GetVisualDescendants().OfType<Button>(),
                b => b.Content as string == "Sign in with Spotify");

            await f.SignInAndLoadAsync();
            Capture(window, "2-library");
            Assert.NotEmpty(window.GetVisualDescendants().OfType<DataGridRow>());

            f.Main.SelectedPage = f.Main.Pages[2];
            f.Main.Collate.SelectedRule = f.Main.Collate.Rules.First(r => r.Group == "Hip Hop");
            Capture(window, "3-collate");
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Metal");

            f.Main.SelectedPage = f.Main.Pages[3];
            await f.Main.Manage.RefreshCommand.ExecuteAsync(null);
            f.Main.Manage.Playlists[0].IsSelected = true;
            f.Main.Manage.Playlists[2].IsSelected = true;
            Capture(window, "4-manage");
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Delete 2 selected");

            window.Close();
        }

        [AvaloniaFact]
        public void ConfirmDialog_EnablesDeleteOnlyAfterTypingWord()
        {
            var names = Enumerable.Range(1, 12).Select(i => $"[Genre] Group {i}").ToList();
            var dialog = new ConfirmDeleteWindow { DataContext = new ConfirmDeleteViewModel(names) };
            dialog.Show();

            var delete = dialog.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == "Delete");
            Assert.False(delete.IsEnabled);

            var box = dialog.FindControl<TextBox>("ConfirmationBox")!;
            box.Focus();
            dialog.KeyTextInput("DELETE");
            Dispatcher.UIThread.RunJobs();

            Assert.True(delete.IsEnabled);
            Capture(dialog, "5-confirm-delete");
            dialog.Close();
        }

        private static void Capture(Window window, string name)
        {
            Dispatcher.UIThread.RunJobs();
            var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);

            var dir = Environment.GetEnvironmentVariable("SCREENSHOT_DIR");
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
                frame!.Save(Path.Combine(dir, name + ".png"));
            }
        }
    }
}
