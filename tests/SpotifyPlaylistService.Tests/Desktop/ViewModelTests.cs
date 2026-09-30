using SpotifyPlaylistService.Desktop.ViewModels;

namespace SpotifyPlaylistService.Tests.Desktop
{
    public class ViewModelTests
    {
        [Fact]
        public async Task SigningIn_NavigatesToLibraryAndLoadingPopulatesGenres()
        {
            using var f = new DesktopFixture();
            f.SeedLibrary();

            Assert.False(f.Main.Library.LoadCommand.CanExecute(null));
            await f.SignInAndLoadAsync();

            Assert.Same(f.Main.Library, f.Main.SelectedPage.ViewModel);
            Assert.Equal(120, f.State.LikedTracks.Count);
            Assert.Contains(f.Main.Library.Genres, r => r.Genre == "metalcore" && r.Group == "Metal");

            f.Main.Library.FilterText = "rock";
            Assert.All(f.Main.Library.Genres, r => Assert.Contains("rock", r.Genre + r.Group, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task Collate_PreviewsGroupsAndCreatesSelectedPlaylists()
        {
            using var f = new DesktopFixture();
            f.SeedLibrary();
            await f.SignInAndLoadAsync();
            var collate = f.Main.Collate;

            Assert.Contains(collate.Groups, g => g.Name == "Pop");
            Assert.Equal(collate.Groups.Count, collate.SelectedCount);

            collate.SelectNoneCommand.Execute(null);
            Assert.False(collate.CreatePlaylistsCommand.CanExecute(null));

            collate.Groups.Single(g => g.Name == "Metal").IsSelected = true;
            collate.Groups.Single(g => g.Name == "Other").IsSelected = true;
            await collate.CreatePlaylistsCommand.ExecuteAsync(null);

            Assert.Contains(f.Api.Playlists, p => p.Name == "[Genre] Other");
            Assert.Single(f.Api.Playlists, p => p.Name == "[Genre] Metal");
        }

        [Fact]
        public async Task Collate_MergeSelectedCreatesCustomRuleThatIsSaved()
        {
            using var f = new DesktopFixture();
            f.SeedLibrary();
            await f.SignInAndLoadAsync();
            var collate = f.Main.Collate;

            collate.SelectNoneCommand.Execute(null);
            collate.Groups.Single(g => g.Name == "Metal").IsSelected = true;
            collate.Groups.Single(g => g.Name == "Rock").IsSelected = true;
            collate.CustomGroupName = "Heavy";
            collate.MergeSelectedCommand.Execute(null);

            Assert.Equal("Heavy", collate.Rules[0].Group);
            Assert.Contains(collate.Groups, g => g.Name == "Heavy");
            Assert.DoesNotContain(collate.Groups, g => g.Name == "Metal");

            collate.SaveRulesCommand.Execute(null);
            Assert.Equal("Heavy", f.State.RuleStore.Load().Rules[0].Group);
        }

        [Fact]
        public async Task Collate_MinTracksOptionRefreshesPreview()
        {
            using var f = new DesktopFixture();
            f.SeedLibrary();
            await f.SignInAndLoadAsync();
            var before = f.Main.Collate.Groups.Count;

            f.Main.Collate.MinTracks = 1000;

            Assert.True(before > 0);
            Assert.Empty(f.Main.Collate.Groups);
        }

        [Fact]
        public async Task Manage_BulkDeletesSelectedAfterConfirmation()
        {
            using var f = new DesktopFixture();
            f.SeedLibrary();
            await f.State.CompleteSignInAsync();
            var manage = f.Main.Manage;

            f.Main.SelectedPage = f.Main.Pages[3];
            await manage.RefreshCommand.ExecuteAsync(null);
            Assert.Equal(7, manage.Playlists.Count);
            Assert.DoesNotContain(manage.Playlists, p => p.Name == "Road trip mix");

            manage.FilterText = "o";
            manage.SelectAllCommand.Execute(null);
            var expected = manage.Playlists.Select(p => p.Name).ToList();
            manage.FilterText = "";

            await manage.DeleteSelectedCommand.ExecuteAsync(null);

            Assert.Equal(expected, f.Dialogs.Requests.Single());
            Assert.Equal(7 - expected.Count, manage.Playlists.Count);
            Assert.Contains(f.Api.Playlists, p => p.Name == "Road trip mix");
            Assert.StartsWith($"Deleted {expected.Count} playlists", manage.LastResult);
        }

        [Fact]
        public async Task Manage_DoesNothingWhenConfirmationDeclined()
        {
            using var f = new DesktopFixture();
            f.SeedLibrary();
            await f.State.CompleteSignInAsync();
            f.Dialogs.Answer = false;
            var manage = f.Main.Manage;
            await manage.RefreshCommand.ExecuteAsync(null);

            manage.SelectAllCommand.Execute(null);
            await manage.DeleteSelectedCommand.ExecuteAsync(null);

            Assert.Equal(8, f.Api.Playlists.Count);
            Assert.Equal(7, manage.Playlists.Count);
        }

        [Fact]
        public void ConfirmDelete_RequiresTypedWordAboveThreshold()
        {
            var few = new ConfirmDeleteViewModel(new[] { "a", "b" });
            Assert.True(few.CanConfirm);

            var many = new ConfirmDeleteViewModel(Enumerable.Range(0, 11).Select(i => $"p{i}").ToList());
            Assert.False(many.CanConfirm);
            many.ConfirmationText = "delete";
            Assert.False(many.CanConfirm);
            many.ConfirmationText = "DELETE";
            Assert.True(many.CanConfirm);
        }

        [Fact]
        public async Task SignOut_ClearsStateAndReturnsToConnect()
        {
            using var f = new DesktopFixture();
            f.SeedLibrary();
            await f.SignInAndLoadAsync();

            f.Main.Connect.SignOutCommand.Execute(null);

            Assert.Same(f.Main.Connect, f.Main.SelectedPage.ViewModel);
            Assert.Empty(f.State.LikedTracks);
            Assert.False(f.Main.Manage.RefreshCommand.CanExecute(null));
        }
    }
}
