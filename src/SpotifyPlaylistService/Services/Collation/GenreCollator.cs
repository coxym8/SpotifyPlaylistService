using SpotifyPlaylistService.Models;

namespace SpotifyPlaylistService.Services.Collation
{
    /// <summary>
    /// Collates Spotify's fine-grained artist genres ("bedroom pop", "k-pop") into parent
    /// groups ("Pop") using an ordered <see cref="GenreRuleSet"/>
    /// </summary>
    public class GenreCollator
    {
        private readonly Dictionary<string, string> _exact = new();
        private readonly List<(string Keyword, string Group)> _keywords = new();
        private readonly GenreRuleSet _rules;

        public GenreCollator(GenreRuleSet rules)
        {
            _rules = rules;

            foreach (var rule in rules.Rules)
            {
                if (string.IsNullOrWhiteSpace(rule.Group))
                    continue;

                foreach (var exact in rule.Exact)
                    _exact.TryAdd(Normalize(exact), rule.Group);

                foreach (var keyword in rule.Keywords)
                {
                    var normalized = Normalize(keyword);
                    if (normalized.Length > 0)
                        _keywords.Add((normalized, rule.Group));
                }
            }
        }

        /// <summary>
        /// Returns the parent group for a Spotify genre, or null if no rule matches
        /// </summary>
        public string? MapGenre(string genre)
        {
            var normalized = Normalize(genre);

            if (_exact.TryGetValue(normalized, out var exactGroup))
                return exactGroup;

            foreach (var (keyword, group) in _keywords)
            {
                if (MatchesAtWordStart(normalized, keyword))
                    return group;
            }

            return null;
        }

        /// <summary>
        /// Groups tracks by parent genre. A track joins every group one of its genres maps to.
        /// Groups smaller than <see cref="GenreRuleSet.MinTracks"/> are dropped; the result is
        /// ordered by track count, largest first.
        /// </summary>
        public List<GenreGroup> Collate(IEnumerable<SpotifyTrack> tracks)
        {
            var groups = new Dictionary<string, GenreGroup>(StringComparer.OrdinalIgnoreCase);
            var seen = new HashSet<(string Group, string TrackId)>();

            GenreGroup GetGroup(string name)
            {
                if (!groups.TryGetValue(name, out var group))
                {
                    group = new GenreGroup { Name = name };
                    groups[name] = group;
                }
                return group;
            }

            void AddTrack(string groupName, SpotifyTrack track)
            {
                if (seen.Add((groupName.ToLowerInvariant(), track.Id)))
                    GetGroup(groupName).Tracks.Add(track);
            }

            foreach (var track in tracks)
            {
                if (track.Genres.Count == 0)
                {
                    if (_rules.IncludeUnclassified)
                        AddTrack(GenreRuleSet.UnclassifiedGroup, track);
                    continue;
                }

                foreach (var genre in track.Genres)
                {
                    var groupName = MapGenre(genre) ?? (_rules.IncludeOther ? GenreRuleSet.OtherGroup : null);
                    if (groupName == null)
                        continue;

                    GetGroup(groupName).SourceGenres.Add(genre);
                    AddTrack(groupName, track);
                }
            }

            return groups.Values
                         .Where(g => g.Tracks.Count >= Math.Max(1, _rules.MinTracks))
                         .OrderByDescending(g => g.Tracks.Count)
                         .ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
                         .ToList();
        }

        /// <summary>
        /// True when the keyword starts at a word boundary: "metal" matches "nu metal" and
        /// "metalcore", but "rap" does not match "scrap"
        /// </summary>
        private static bool MatchesAtWordStart(string genre, string keyword) =>
            genre.StartsWith(keyword, StringComparison.Ordinal) ||
            genre.Contains(" " + keyword, StringComparison.Ordinal);

        /// <summary>
        /// Lowercases, turns hyphens/underscores into spaces and collapses whitespace,
        /// so "Hip-Hop" and "hip hop" compare equal
        /// </summary>
        public static string Normalize(string value)
        {
            var chars = value.Trim().ToLowerInvariant()
                             .Select(c => c == '-' || c == '_' ? ' ' : c)
                             .ToArray();

            return string.Join(' ', new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
