using System;
using System.Threading.Tasks;

namespace OMNI.Services.WebView
{
    public interface IMapViewerService : IDisposable
    {
        event EventHandler<string> StatusChanged;
        event EventHandler<Exception> ErrorOccurred;

        Task WaitForInitializationAsync();
        Task<string> AddMarkerAsync(float x, float y, float heading);
        Task<string> ClearMarkersAsync();
        Task SetKeepHistoryAsync(bool keepHistory);
        Task SetMapOpacity(float opacity);
    }
}