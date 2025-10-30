using System;
using System.Linq;
using System.Threading.Tasks;
using SpotifyPlaylistService.Services;

namespace SpotifyGenrePlaylistManager
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== Spotify Genre Playlist Manager ===\n");

            // Initialize services
            var authService = new SpotifyAuthService();
            var apiService = new SpotifyApiService();
            var managerService = new SpotifyManagerService(apiService);

            // Authenticate
            Console.Write("Enter your Spotify Client ID: ");
            string? clientId = Console.ReadLine();

            Console.Write("Enter your Spotify Client Secret: ");
            string? clientSecret = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                Console.WriteLine("Client ID and Secret are required.");
                return;
            }

            Console.WriteLine("\nAuthenticating...");
            if (!await authService.AuthenticateAsync(clientId, clientSecret))
            {
                Console.WriteLine("Authentication failed.");
                return;
            }

            Console.WriteLine("Authentication successful!");

            if (authService.AccessToken == null)
            {
                Console.WriteLine("Failed to obtain access token.");
                return;
            }

            // Initialize manager
            await managerService.InitializeAsync(authService.AccessToken);

            while (true)
            {
                Console.WriteLine("\n=== Main Menu ===");
                Console.WriteLine("1. View liked songs and genres");
                Console.WriteLine("2. Add playlist(s) to liked songs");
                Console.WriteLine("3. Create/update genre playlist");
                Console.WriteLine("4. Create/update ALL genre playlists");
                Console.WriteLine("5. View all playlists");
                Console.WriteLine("6. View genre playlists");
                Console.WriteLine("7. Delete specific genre playlist");
                Console.WriteLine("8. Delete ALL genre playlists");
                Console.WriteLine("9. Exit");
                Console.Write("\nSelect option: ");

                string? choice = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(choice))
                {
                    Console.WriteLine("Invalid option.");
                    continue;
                }

                try
                {
                    switch (choice)
                    {
                        case "1":
                            await ViewLikedSongsAsync(managerService);
                            break;

                        case "2":
                            await AddPlaylistsToLikedAsync(managerService);
                            break;

                        case "3":
                            await CreateGenrePlaylistAsync(managerService);
                            break;

                        case "4":
                            await CreateAllGenrePlaylistsAsync(managerService);
                            break;

                        case "5":
                            await ViewPlaylistsAsync(managerService);
                            break;

                        case "6":
                            await ViewGenrePlaylistsAsync(managerService);
                            break;

                        case "7":
                            await DeleteGenrePlaylistAsync(managerService);
                            break;

                        case "8":
                            await DeleteAllGenrePlaylistsAsync(managerService);
                            break;

                        case "9":
                            Console.WriteLine("\nGoodbye!");
                            return;

                        default:
                            Console.WriteLine("Invalid option.");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\nError: {ex.Message}");
                }
            }
        }

        static async Task ViewLikedSongsAsync(ISpotifyManagerService manager)
        {
            Console.WriteLine("\nFetching liked songs and genres (this may take a few minutes)...");
            var tracks = await manager.GetLikedSongsWithGenresAsync();

            Console.WriteLine($"\nTotal liked songs: {tracks.Count}");

            var genreStats = manager.GetGenreStatistics(tracks);
            Console.WriteLine($"\nTop 15 Genres:");
            foreach (var genre in genreStats.Take(15))
            {
                Console.WriteLine($"  {genre.Key}: {genre.Value} tracks");
            }
        }

        static async Task AddPlaylistsToLikedAsync(ISpotifyManagerService manager)
        {
            var playlists = await manager.GetUserPlaylistsAsync();

            Console.WriteLine("\n=== Your Playlists ===");
            for (int i = 0; i < playlists.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {playlists[i].Name} ({playlists[i].TotalTracks} tracks)");
            }

            Console.Write("\nEnter playlist numbers to add (comma-separated, or 'all'): ");
            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                Console.WriteLine("No input provided.");
                return;
            }

            var playlistIds = new System.Collections.Generic.List<string>();

            if (input.Trim().ToLower() == "all")
            {
                playlistIds = playlists.Select(p => p.Id).ToList();
            }
            else
            {
                var indices = input.Split(',').Select(s => int.Parse(s.Trim()) - 1);
                playlistIds = indices.Select(i => playlists[i].Id).ToList();
            }

            Console.WriteLine($"\nAdding {playlistIds.Count} playlist(s) to liked songs...");
            var success = await manager.AddMultiplePlaylistsToLikedAsync(playlistIds);

            if (success)
                Console.WriteLine("Successfully added all tracks to liked songs!");
            else
                Console.WriteLine("Some tracks may not have been added.");
        }

        static async Task CreateGenrePlaylistAsync(ISpotifyManagerService manager)
        {
            Console.WriteLine("\nFetching liked songs and genres (this may take a few minutes)...");
            var tracks = await manager.GetLikedSongsWithGenresAsync();

            var genreStats = manager.GetGenreStatistics(tracks);
            Console.WriteLine("\n=== Available Genres ===");
            foreach (var genre in genreStats.Take(20))
            {
                Console.WriteLine($"  {genre.Key} ({genre.Value} tracks)");
            }

            Console.Write("\nEnter genre name: ");
            string? genreName = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(genreName))
            {
                Console.WriteLine("No genre provided.");
                return;
            }

            Console.WriteLine($"\nCreating/updating playlist for '{genreName}'...");
            var playlistId = await manager.CreateOrUpdateGenrePlaylistAsync(genreName, tracks);

            if (playlistId != null)
                Console.WriteLine($"Playlist created/updated successfully! ID: {playlistId}");
            else
                Console.WriteLine("No tracks found for this genre.");
        }

        static async Task CreateAllGenrePlaylistsAsync(ISpotifyManagerService manager)
        {
            Console.WriteLine("\nFetching liked songs and genres (this may take a few minutes)...");
            var tracks = await manager.GetLikedSongsWithGenresAsync();

            var genreStats = manager.GetGenreStatistics(tracks);
            Console.WriteLine($"\nFound {genreStats.Count} genres. This will create/update {genreStats.Count} playlists.");
            Console.Write("Continue? (y/n): ");

            string? response = Console.ReadLine();
            if (response?.ToLower() != "y")
                return;

            Console.WriteLine("\nCreating/updating playlists (this will take several minutes)...");
            var results = await manager.CreateAllGenrePlaylistsAsync(tracks);

            Console.WriteLine($"\nSuccessfully created/updated {results.Count} playlists!");
        }

        static async Task ViewPlaylistsAsync(ISpotifyManagerService manager)
        {
            Console.WriteLine("\nFetching playlists...");
            var playlists = await manager.GetUserPlaylistsAsync();

            Console.WriteLine($"\n=== Your Playlists ({playlists.Count} total) ===");
            foreach (var playlist in playlists)
            {
                Console.WriteLine($"  {playlist.Name} ({playlist.TotalTracks} tracks)");
            }
        }

        static async Task ViewGenrePlaylistsAsync(ISpotifyManagerService manager)
        {
            Console.WriteLine("\nFetching genre playlists...");
            var genrePlaylists = await manager.GetGenrePlaylistsAsync();

            if (genrePlaylists.Count == 0)
            {
                Console.WriteLine("\nNo genre playlists found.");
                Console.WriteLine("Genre playlists are named '[Genre] {genre_name}'");
                return;
            }

            Console.WriteLine($"\n=== Genre Playlists ({genrePlaylists.Count} total) ===");
            foreach (var playlist in genrePlaylists)
            {
                Console.WriteLine($"  {playlist.Name} ({playlist.TotalTracks} tracks)");
            }
        }

        static async Task DeleteGenrePlaylistAsync(ISpotifyManagerService manager)
        {
            var genrePlaylists = await manager.GetGenrePlaylistsAsync();

            if (genrePlaylists.Count == 0)
            {
                Console.WriteLine("\nNo genre playlists found to delete.");
                return;
            }

            Console.WriteLine("\n=== Genre Playlists ===");
            for (int i = 0; i < genrePlaylists.Count; i++)
            {
                var genreName = genrePlaylists[i].Name.Replace("[Genre] ", "");
                Console.WriteLine($"{i + 1}. {genreName} ({genrePlaylists[i].TotalTracks} tracks)");
            }

            Console.Write("\nEnter number of playlist to delete (or genre name): ");
            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                Console.WriteLine("No input provided.");
                return;
            }

            string? genreToDelete = null;

            // Check if input is a number
            if (int.TryParse(input, out int index) && index > 0 && index <= genrePlaylists.Count)
            {
                genreToDelete = genrePlaylists[index - 1].Name.Replace("[Genre] ", "");
            }
            else
            {
                genreToDelete = input;
            }

            Console.Write($"\nAre you sure you want to delete '[Genre] {genreToDelete}'? (y/n): ");
            string? confirm = Console.ReadLine();

            if (confirm?.ToLower() != "y")
            {
                Console.WriteLine("Cancelled.");
                return;
            }

            Console.WriteLine($"\nDeleting playlist...");
            var success = await manager.DeleteGenrePlaylistAsync(genreToDelete);

            if (success)
                Console.WriteLine($"Successfully deleted '[Genre] {genreToDelete}'");
            else
                Console.WriteLine($"Failed to delete playlist. It may not exist or you may not have permission.");
        }

        static async Task DeleteAllGenrePlaylistsAsync(ISpotifyManagerService manager)
        {
            var genrePlaylists = await manager.GetGenrePlaylistsAsync();

            if (genrePlaylists.Count == 0)
            {
                Console.WriteLine("\nNo genre playlists found to delete.");
                return;
            }

            Console.WriteLine($"\n⚠️  WARNING: This will delete {genrePlaylists.Count} genre playlists:");
            foreach (var playlist in genrePlaylists.Take(10))
            {
                Console.WriteLine($"  - {playlist.Name}");
            }

            if (genrePlaylists.Count > 10)
            {
                Console.WriteLine($"  ... and {genrePlaylists.Count - 10} more");
            }

            Console.Write("\nType 'DELETE ALL' to confirm: ");
            string? confirm = Console.ReadLine();

            if (confirm != "DELETE ALL")
            {
                Console.WriteLine("Cancelled. Exact text 'DELETE ALL' required.");
                return;
            }

            Console.WriteLine("\nDeleting playlists...");
            int deletedCount = await manager.DeleteAllGenrePlaylistsAsync();

            Console.WriteLine($"\nSuccessfully deleted {deletedCount} out of {genrePlaylists.Count} genre playlists.");
        }
    }
}
