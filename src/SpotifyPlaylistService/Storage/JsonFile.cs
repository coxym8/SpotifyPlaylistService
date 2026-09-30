using System.Text.Json;

namespace SpotifyPlaylistService.Storage
{
    internal static class JsonFile
    {
        public static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public static T? Read<T>(string path)
        {
            if (!File.Exists(path))
                return default;

            try
            {
                return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options);
            }
            catch (JsonException)
            {
                return default;
            }
        }

        public static void Write<T>(string path, T value)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            // Write to a temp file first so a crash never leaves a half-written file
            var tempPath = path + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(value, Options));
            File.Move(tempPath, path, overwrite: true);
        }
    }
}
