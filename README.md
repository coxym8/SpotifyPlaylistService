# SpotifyPlaylistService

A C# library for managing Spotify playlists with genre-based organization, bulk operations, and liked songs management.

## Features

- 🎵 **Genre-Based Playlist Management** - Automatically create playlists organized by music genres
- ❤️ **Liked Songs Integration** - Add tracks from playlists to your liked songs
- 🔄 **Bulk Operations** - Create or delete multiple genre playlists at once
- 📁 **Playlist Organization** - Genre playlists use `[Genre]` prefix for easy organization
- 🔐 **OAuth 2.0 Authentication** - Secure authentication flow with token refresh support

## Installation

### NuGet Package (Coming Soon)
```bash
dotnet add package SpotifyPlaylistService
```

### From Source
```bash
git clone https://github.com/yourusername/SpotifyPlaylistService.git
cd SpotifyPlaylistService
dotnet build
```

## Quick Start

### 1. Setup Spotify App
1. Go to [Spotify Developer Dashboard](https://developer.spotify.com/dashboard)
2. Create a new app
3. Add redirect URI: `http://127.0.0.1:8888/callback`
4. Copy your **Client ID** and **Client Secret**

### 2. Basic Usage

```csharp
using SpotifyPlaylistService;
using SpotifyPlaylistService.Services;

// Initialize services
var authService = new SpotifyAuthService();
var apiService = new SpotifyApiService();
var playlistService = new SpotifyManagerService(apiService);

// Authenticate
await authService.AuthenticateAsync("YOUR_CLIENT_ID", "YOUR_CLIENT_SECRET");

// Initialize service with access token
await playlistService.InitializeAsync(authService.AccessToken);

// Get liked songs with genres
var tracks = await playlistService.GetLikedSongsWithGenresAsync();
Console.WriteLine($"Found {tracks.Count} liked songs");

// Create genre playlists
var results = await playlistService.CreateAllGenrePlaylistsAsync(tracks);
Console.WriteLine($"Created {results.Count} genre playlists");
```

## Configuration

### Option 1: Pass Values Directly
```csharp
var authService = new SpotifyAuthService();
await authService.AuthenticateAsync(clientId, clientSecret);
```

### Option 2: Environment Variables
```bash
# Set environment variables
export SPOTIFY_CLIENT_ID="your_client_id"
export SPOTIFY_CLIENT_SECRET="your_client_secret"
```

```csharp
var clientId = Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_ID");
var clientSecret = Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_SECRET");

var authService = new SpotifyAuthService();
await authService.AuthenticateAsync(clientId, clientSecret);
```

### Option 3: Configuration File (appsettings.json)
```json
{
  "Spotify": {
    "ClientId": "your_client_id",
    "ClientSecret": "your_client_secret"
  }
}
```

```csharp
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

var clientId = configuration["Spotify:ClientId"];
var clientSecret = configuration["Spotify:ClientSecret"];
```

## API Reference

### SpotifyAuthService
Handles OAuth 2.0 authentication flow.

```csharp
public interface ISpotifyAuthService
{
    string? AccessToken { get; }
    string? RefreshToken { get; }
    Task<bool> AuthenticateAsync(string clientId, string clientSecret);
    Task<bool> RefreshAccessTokenAsync();
}
```

### SpotifyApiService
Low-level Spotify API operations.

```csharp
public interface ISpotifyApiService
{
    Task<List<SpotifyTrack>> GetLikedSongsAsync();
    Task<List<SpotifyPlaylist>> GetUserPlaylistsAsync();
    Task<string> CreatePlaylistAsync(string name, string description);
    Task<bool> DeletePlaylistAsync(string playlistId);
    // ... and more
}
```

### SpotifyManagerService
High-level business logic and orchestration.

```csharp
public interface ISpotifyManagerService
{
    Task<List<SpotifyTrack>> GetLikedSongsWithGenresAsync();
    Task<Dictionary<string, string>> CreateAllGenrePlaylistsAsync(List<SpotifyTrack> tracks);
    Task<int> DeleteAllGenrePlaylistsAsync();
    Task<bool> AddPlaylistTracksToLikedAsync(string playlistId);
    // ... and more
}
```

## Examples

### Example 1: Create Genre Playlists
```csharp
var tracks = await playlistService.GetLikedSongsWithGenresAsync();
var genreStats = playlistService.GetGenreStatistics(tracks);

foreach (var genre in genreStats.Keys)
{
    await playlistService.CreateOrUpdateGenrePlaylistAsync(genre, tracks);
    Console.WriteLine($"Created playlist for: {genre}");
}
```

### Example 2: Add Playlist Songs to Liked
```csharp
var playlists = await playlistService.GetUserPlaylistsAsync();
var playlistIds = playlists.Select(p => p.Id).ToList();

await playlistService.AddMultiplePlaylistsToLikedAsync(playlistIds);
Console.WriteLine("All playlist tracks added to liked songs");
```

### Example 3: Clean Up Genre Playlists
```csharp
var genrePlaylists = await playlistService.GetGenrePlaylistsAsync();
Console.WriteLine($"Found {genrePlaylists.Count} genre playlists");

var deletedCount = await playlistService.DeleteAllGenrePlaylistsAsync();
Console.WriteLine($"Deleted {deletedCount} playlists");
```

## Project Structure

```
SpotifyPlaylistService/
├── src/
│   ├── SpotifyPlaylistService/
│   │   ├── Models/
│   │   │   ├── SpotifyTrack.cs
│   │   │   ├── SpotifyPlaylist.cs
│   │   │   └── SpotifyFolder.cs
│   │   ├── Services/
│   │   │   ├── ISpotifyAuthService.cs
│   │   │   ├── SpotifyAuthService.cs
│   │   │   ├── ISpotifyApiService.cs
│   │   │   ├── SpotifyApiService.cs
│   │   │   ├── ISpotifyManagerService.cs
│   │   │   └── SpotifyManagerService.cs
│   │   └── SpotifyPlaylistService.csproj
│   └── SpotifyPlaylistService.Console/
│       ├── Program.cs
│       └── SpotifyPlaylistService.Console.csproj
├── tests/
│   └── SpotifyPlaylistService.Tests/
├── .gitignore
├── README.md
├── LICENSE
└── SpotifyPlaylistService.sln
```

## Requirements

- .NET 6.0 or higher
- Spotify Developer Account
- Required Scopes:
  - `user-library-read`
  - `user-library-modify`
  - `playlist-read-private`
  - `playlist-read-collaborative`
  - `playlist-modify-public`
  - `playlist-modify-private`

## Limitations

- **Playlist Folders**: Spotify's public API does not support folder operations. Genre playlists use the `[Genre]` naming convention for manual organization.
- **Rate Limiting**: The library includes basic rate limiting (100ms delays) but heavy usage may hit Spotify's rate limits.
- **Genre Detection**: Genres are retrieved from artist data, so tracks may inherit multiple or no genres.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Built with [Spotify Web API](https://developer.spotify.com/documentation/web-api/)
- Inspired by the need for better playlist organization

## Support

- 📫 Report issues: [GitHub Issues](https://github.com/yourusername/SpotifyPlaylistService/issues)
- 📖 Documentation: [Wiki](https://github.com/yourusername/SpotifyPlaylistService/wiki)
- 💬 Discussions: [GitHub Discussions](https://github.com/yourusername/SpotifyPlaylistService/discussions)

---

**Note**: This library is not affiliated with Spotify AB. Spotify is a trademark of Spotify AB.