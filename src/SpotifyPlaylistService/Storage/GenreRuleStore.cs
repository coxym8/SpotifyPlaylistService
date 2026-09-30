using SpotifyPlaylistService.Models;

namespace SpotifyPlaylistService.Storage
{
    /// <summary>
    /// Loads and saves user-editable genre collation rules as JSON
    /// </summary>
    public class GenreRuleStore
    {
        private readonly string _path;

        public GenreRuleStore(string? path = null)
        {
            _path = path ?? AppDataPaths.GenreRulesFile;
        }

        public string FilePath => _path;

        /// <summary>
        /// Loads saved rules, falling back to the built-in defaults when none are saved
        /// </summary>
        public GenreRuleSet Load()
        {
            var rules = JsonFile.Read<GenreRuleSet>(_path);
            return rules is { Rules.Count: > 0 } ? rules : GenreRuleSet.CreateDefault();
        }

        public void Save(GenreRuleSet rules) => JsonFile.Write(_path, rules);

        public GenreRuleSet ResetToDefaults()
        {
            var defaults = GenreRuleSet.CreateDefault();
            Save(defaults);
            return defaults;
        }
    }
}
