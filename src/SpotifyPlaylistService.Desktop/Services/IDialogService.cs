namespace SpotifyPlaylistService.Desktop.Services
{
    /// <summary>
    /// Keeps view models free of window code so they can be tested without a UI
    /// </summary>
    public interface IDialogService
    {
        /// <summary>
        /// Asks the user to confirm deleting the named playlists
        /// </summary>
        Task<bool> ConfirmDeleteAsync(IReadOnlyList<string> playlistNames);
    }
}
