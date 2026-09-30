using SpotifyPlaylistService.Models;
using SpotifyPlaylistService.Services.Collation;

namespace SpotifyPlaylistService.Tests
{
    public class GenreCollatorTests
    {
        private static readonly GenreCollator Defaults = new(GenreRuleSet.CreateDefault());

        [Theory]
        [InlineData("metalcore", "Metal")]
        [InlineData("nu metal", "Metal")]
        [InlineData("k-pop", "Pop")]
        [InlineData("dance pop", "Pop")]
        [InlineData("indie pop", "Indie & Alternative")]
        [InlineData("pop rap", "Hip Hop")]
        [InlineData("Hip-Hop", "Hip Hop")]
        [InlineData("trap latino", "Latin")]
        [InlineData("dub", "Reggae")]
        [InlineData("dubstep", "Electronic")]
        [InlineData("deep house", "Electronic")]
        [InlineData("skate punk", "Punk & Emo")]
        [InlineData("garage rock", "Rock")]
        [InlineData("neo soul", "R&B & Soul")]
        [InlineData("alt z", "Indie & Alternative")]
        public void DefaultRules_MapCommonSpotifyGenres(string genre, string expected)
        {
            Assert.Equal(expected, Defaults.MapGenre(genre));
        }

        [Fact]
        public void Keywords_MatchOnlyAtWordStart()
        {
            var collator = new GenreCollator(RulesWith(new GenreRule { Group = "Hip Hop", Keywords = { "rap" } }));

            Assert.Equal("Hip Hop", collator.MapGenre("rap"));
            Assert.Equal("Hip Hop", collator.MapGenre("gangster rap"));
            Assert.Equal("Hip Hop", collator.MapGenre("rapcore"));
            Assert.Null(collator.MapGenre("scrap metal"));
        }

        [Fact]
        public void ExactAlias_WinsOverEarlierKeywordRule()
        {
            var collator = new GenreCollator(RulesWith(
                new GenreRule { Group = "Pop", Keywords = { "pop" } },
                new GenreRule { Group = "Korean", Exact = { "k-pop" } }));

            Assert.Equal("Korean", collator.MapGenre("K Pop"));
            Assert.Equal("Pop", collator.MapGenre("j-pop"));
        }

        [Fact]
        public void EarlierRule_WinsForKeywords()
        {
            var collator = new GenreCollator(RulesWith(
                new GenreRule { Group = "Metal", Keywords = { "metal" } },
                new GenreRule { Group = "Rock", Keywords = { "rock", "metal" } }));

            Assert.Equal("Metal", collator.MapGenre("metal"));
        }

        [Fact]
        public void Collate_GroupsTracksAndCountsEachTrackOncePerGroup()
        {
            var rules = RulesWith(
                new GenreRule { Group = "Pop", Keywords = { "pop" } },
                new GenreRule { Group = "Rock", Keywords = { "rock" } });
            rules.MinTracks = 1;

            var tracks = new[]
            {
                Track("1", "pop", "dance pop"),
                Track("2", "pop rock", "rock"),
                Track("3", "rock"),
            };

            var groups = new GenreCollator(rules).Collate(tracks);

            var pop = groups.Single(g => g.Name == "Pop");
            var rock = groups.Single(g => g.Name == "Rock");
            Assert.Equal(new[] { "1", "2" }, pop.Tracks.Select(t => t.Id));
            Assert.Equal(new[] { "2", "3" }, rock.Tracks.Select(t => t.Id));
            Assert.Equal(new[] { "dance pop", "pop", "pop rock" }, pop.SourceGenres);
        }

        [Fact]
        public void Collate_PutsUnmatchedInOtherAndGenrelessInUnclassifiedWhenEnabled()
        {
            var rules = RulesWith(new GenreRule { Group = "Pop", Keywords = { "pop" } });
            rules.MinTracks = 1;
            rules.IncludeOther = true;
            rules.IncludeUnclassified = true;

            var groups = new GenreCollator(rules).Collate(new[]
            {
                Track("1", "pop"),
                Track("2", "gregorian chant"),
                Track("3"),
            });

            Assert.Equal(new[] { "1" }, groups.Single(g => g.Name == "Pop").Tracks.Select(t => t.Id));
            Assert.Equal(new[] { "2" }, groups.Single(g => g.Name == GenreRuleSet.OtherGroup).Tracks.Select(t => t.Id));
            Assert.Equal(new[] { "3" }, groups.Single(g => g.Name == GenreRuleSet.UnclassifiedGroup).Tracks.Select(t => t.Id));
        }

        [Fact]
        public void Collate_DropsOtherAndUnclassifiedWhenDisabled()
        {
            var rules = RulesWith(new GenreRule { Group = "Pop", Keywords = { "pop" } });
            rules.MinTracks = 1;
            rules.IncludeOther = false;
            rules.IncludeUnclassified = false;

            var groups = new GenreCollator(rules).Collate(new[] { Track("1", "pop"), Track("2", "chant"), Track("3") });

            Assert.Equal(new[] { "Pop" }, groups.Select(g => g.Name));
        }

        [Fact]
        public void Collate_AppliesMinTracksAndOrdersBySize()
        {
            var rules = RulesWith(
                new GenreRule { Group = "Pop", Keywords = { "pop" } },
                new GenreRule { Group = "Rock", Keywords = { "rock" } },
                new GenreRule { Group = "Jazz", Keywords = { "jazz" } });
            rules.MinTracks = 2;

            var groups = new GenreCollator(rules).Collate(new[]
            {
                Track("1", "rock"), Track("2", "rock"), Track("3", "rock"),
                Track("4", "pop"), Track("5", "pop"),
                Track("6", "jazz"),
            });

            Assert.Equal(new[] { "Rock", "Pop" }, groups.Select(g => g.Name));
        }

        [Fact]
        public void AddCustomGroup_TakesPriorityAndClaimsExactGenres()
        {
            var rules = RulesWith(
                new GenreRule { Group = "Pop", Keywords = { "pop" }, Exact = { "k-pop" } },
                new GenreRule { Group = "Rock", Keywords = { "rock" } });

            rules.AddCustomGroup("Korean", new[] { "k-pop", "k-rock" });

            Assert.Equal("Korean", rules.Rules[0].Group);
            Assert.Empty(rules.Rules.Single(r => r.Group == "Pop").Exact);

            var collator = new GenreCollator(rules);
            Assert.Equal("Korean", collator.MapGenre("k-pop"));
            Assert.Equal("Korean", collator.MapGenre("k-rock"));
            Assert.Equal("Rock", collator.MapGenre("hard rock"));
        }

        [Fact]
        public void AddCustomGroup_WithExistingNameExtendsThatRule()
        {
            var rules = RulesWith(
                new GenreRule { Group = "Pop", Keywords = { "pop" } },
                new GenreRule { Group = "Chill", Exact = { "lo-fi" } });

            rules.AddCustomGroup("chill", new[] { "ambient" });

            Assert.Equal(2, rules.Rules.Count);
            Assert.Equal("Chill", rules.Rules[0].Group);
            Assert.Equal(new[] { "lo-fi", "ambient" }, rules.Rules[0].Exact);
        }

        [Fact]
        public void DefaultRules_HaveUniqueGroupNames()
        {
            var groups = GenreRuleSet.CreateDefault().Rules.Select(r => r.Group).ToList();
            Assert.Equal(groups.Count, groups.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }

        private static GenreRuleSet RulesWith(params GenreRule[] rules) => new() { Rules = rules.ToList() };

        private static SpotifyTrack Track(string id, params string[] genres) => new()
        {
            Id = id, Name = id, Artist = "a", ArtistId = "a" + id, Genres = genres.ToList()
        };
    }
}
