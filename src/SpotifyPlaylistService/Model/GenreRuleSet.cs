namespace SpotifyPlaylistService.Models
{
    /// <summary>
    /// Ordered collation rules. Earlier rules win, so specific groups (Metal) must come
    /// before generic ones (Rock).
    /// </summary>
    public class GenreRuleSet
    {
        public const string OtherGroup = "Other";
        public const string UnclassifiedGroup = "Unclassified";

        public List<GenreRule> Rules { get; set; } = new();

        /// <summary>
        /// Put genres that match no rule into an "Other" group
        /// </summary>
        public bool IncludeOther { get; set; } = true;

        /// <summary>
        /// Put tracks whose artist has no Spotify genres into an "Unclassified" group
        /// </summary>
        public bool IncludeUnclassified { get; set; }

        /// <summary>
        /// Groups with fewer tracks than this are dropped
        /// </summary>
        public int MinTracks { get; set; } = 5;

        /// <summary>
        /// Creates a custom group from specific Spotify genres. The genres are removed from
        /// other rules' exact aliases and the new rule gets top priority.
        /// </summary>
        public GenreRule AddCustomGroup(string name, IEnumerable<string> genres)
        {
            var exact = genres.Select(g => g.Trim())
                              .Where(g => g.Length > 0)
                              .Distinct(StringComparer.OrdinalIgnoreCase)
                              .ToList();

            foreach (var rule in Rules)
                rule.Exact.RemoveAll(e => exact.Contains(e, StringComparer.OrdinalIgnoreCase));

            var existing = Rules.FirstOrDefault(r => r.Group.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                Rules.Remove(existing);
                existing.Exact.AddRange(exact);
                Rules.Insert(0, existing);
                return existing;
            }

            var created = new GenreRule { Group = name, Exact = exact };
            Rules.Insert(0, created);
            return created;
        }

        public static GenreRuleSet CreateDefault() => new()
        {
            Rules = new List<GenreRule>
            {
                Rule("Soundtrack", "soundtrack", "score", "video game", "anime", "show tunes", "broadway", "musical"),
                Rule("Christian & Gospel", "christian", "gospel", "worship", "ccm"),
                Rule("Classical", "classical", "orchestra", "opera", "choral", "symphon", "string quartet", "early music"),
                Rule("Metal", "metal", "deathcore", "grindcore", "djent", "thrash", "doom", "sludge"),
                Rule("Latin", "latin", "latino", "reggaeton", "salsa", "bachata", "cumbia", "merengue", "corrido",
                    "banda", "mariachi", "mexican", "sertanejo", "bossa nova", "mpb", "tango", "flamenco", "urbano",
                    "brazilian", "funk carioca"),
                Rule("Hip Hop", "hip hop", "hiphop", "rap", "trap", "drill", "grime", "boom bap", "phonk"),
                Rule("R&B & Soul", "r&b", "rnb", "soul", "funk", "motown", "quiet storm", "new jack swing"),
                Rule("Reggae", "reggae", "dancehall", "rocksteady", "ska punk").WithExact("dub", "ska", "2 tone"),
                Rule("Jazz", "jazz", "bebop", "swing", "big band"),
                Rule("Blues", "blues"),
                Rule("Country", "country", "americana", "bluegrass", "honky tonk", "outlaw", "red dirt"),
                Rule("Folk & Acoustic", "folk", "singer songwriter", "acoustic", "celtic"),
                Rule("Electronic", "electronic", "electro", "edm", "house", "techno", "trance", "dubstep",
                    "drum and bass", "dnb", "ambient", "idm", "synthwave", "uk garage", "breakbeat", "downtempo",
                    "chillwave", "hardstyle", "eurodance", "lofi", "lo fi beats", "chillhop"),
                Rule("Punk & Emo", "punk", "emo", "hardcore", "screamo"),
                Rule("Rock", "rock", "grunge", "shoegaze"),
                Rule("Indie & Alternative", "indie", "alternative", "bedroom", "dream pop", "britpop").WithExact("alt z"),
                Rule("Pop", "pop", "idol", "boy band", "girl group", "easy listening", "schlager", "adult standards",
                    "synthpop", "hyperpop"),
            }
        };

        private static GenreRule Rule(string group, params string[] keywords) =>
            new() { Group = group, Keywords = keywords.ToList() };
    }

    internal static class GenreRuleExtensions
    {
        public static GenreRule WithExact(this GenreRule rule, params string[] exact)
        {
            rule.Exact.AddRange(exact);
            return rule;
        }
    }
}
