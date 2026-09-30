using CommunityToolkit.Mvvm.ComponentModel;

namespace SpotifyPlaylistService.Desktop.ViewModels
{
    public abstract class ViewModelBase : ObservableObject
    {
    }

    /// <summary>
    /// Implemented by pages that refresh when the user navigates to them
    /// </summary>
    public interface INavigationAware
    {
        Task OnNavigatedToAsync();
    }
}
