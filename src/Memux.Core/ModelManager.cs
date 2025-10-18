using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;

namespace Memux.Core;

/// <summary>
/// Manages downloading and storing AI models for Memux
/// </summary>
public class ModelManager
{
    private readonly string _modelsDirectory;
    private readonly HttpClient _httpClient;
    private CancellationTokenSource _cancellationTokenSource;

    public ModelManager()
    {
        // Store models in the same directory as the database
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _modelsDirectory = Path.Combine(appData, "Memux", "Models");
        Directory.CreateDirectory(_modelsDirectory);
        
        _httpClient = new HttpClient();
        _cancellationTokenSource = new CancellationTokenSource();
    }

    public event EventHandler<ModelDownloadProgressEventArgs>? DownloadProgress;
    public event EventHandler<ModelDownloadCompletedEventArgs>? DownloadCompleted;

    // OCR model methods removed - now handled via tessdata_best git submodule

    /// <summary>
    /// Cancel the current download
    /// </summary>
    public void CancelDownload()
    {
        _cancellationTokenSource.Cancel();
    }

    /// <summary>
    /// Reset the cancellation token for a new download
    /// </summary>
    public void ResetCancellation()
    {
        _cancellationTokenSource.Dispose();
        _cancellationTokenSource = new CancellationTokenSource();
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _cancellationTokenSource?.Dispose();
    }
}

public class ModelDownloadProgress
{
    public long BytesDownloaded { get; set; }
    public long TotalBytes { get; set; }
    public double Percentage { get; set; }
}

public class ModelDownloadProgressEventArgs : EventArgs
{
    public ModelDownloadProgress Progress { get; }

    public ModelDownloadProgressEventArgs(ModelDownloadProgress progress)
    {
        Progress = progress;
    }
}

public class ModelDownloadCompletedEventArgs : EventArgs
{
    public bool Success { get; }
    public string? ErrorMessage { get; }

    public ModelDownloadCompletedEventArgs(bool success, string? errorMessage)
    {
        Success = success;
        ErrorMessage = errorMessage;
    }
}
