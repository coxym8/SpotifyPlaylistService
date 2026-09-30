using Avalonia;
using Avalonia.Headless;
using SpotifyPlaylistService.Desktop;

[assembly: Avalonia.Headless.AvaloniaTestApplication(typeof(SpotifyPlaylistService.Tests.Desktop.TestAppBuilder))]

namespace SpotifyPlaylistService.Tests.Desktop
{
    public class TestAppBuilder
    {
        // Real Skia rendering so CaptureRenderedFrame produces screenshots
        public static AppBuilder BuildAvaloniaApp() =>
            AppBuilder.Configure<App>()
                      .UseSkia()
                      .WithInterFont()
                      .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }
}
