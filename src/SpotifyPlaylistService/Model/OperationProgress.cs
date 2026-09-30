namespace SpotifyPlaylistService.Models
{
    /// <summary>
    /// Progress report for long-running operations
    /// </summary>
    public record OperationProgress(int Completed, int Total, string Message);
}
