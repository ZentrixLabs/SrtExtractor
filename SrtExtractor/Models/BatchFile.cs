using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using SrtExtractor.Services.Interfaces;

namespace SrtExtractor.Models;

/// <summary>
/// Represents a file in the batch processing queue.
/// </summary>
public partial class BatchFile : ObservableObject
{
    [ObservableProperty]
    private string _filePath = "";

    [ObservableProperty]
    private string _fileName = "";

    [ObservableProperty]
    private long _fileSizeBytes;

    [ObservableProperty]
    private string _formattedFileSize = "";

    [ObservableProperty]
    private bool _isNetworkFile;

    [ObservableProperty]
    private double _estimatedProcessingTimeMinutes;

    [ObservableProperty]
    private string _networkIndicator = "";

    [ObservableProperty]
    private BatchFileStatus _status = BatchFileStatus.Pending;

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private string? _outputPath;

    [ObservableProperty]
    private DateTime _addedAt = DateTime.Now;

    /// <summary>
    /// Gets the display name for the file.
    /// </summary>
    public string DisplayName => $"{FileName} ({FormattedFileSize})";

    /// <summary>
    /// Gets the estimated processing time as a formatted string.
    /// </summary>
    public string FormattedEstimatedTime
    {
        get
        {
            if (EstimatedProcessingTimeMinutes < 1)
                return "< 1 min";
            else if (EstimatedProcessingTimeMinutes < 60)
                return $"{EstimatedProcessingTimeMinutes:F1} min";
            else
            {
                var hours = (int)(EstimatedProcessingTimeMinutes / 60);
                var minutes = (int)(EstimatedProcessingTimeMinutes % 60);
                return $"{hours}h {minutes}m";
            }
        }
    }

    /// <summary>
    /// Updates the file properties from the file system.
    /// </summary>
    public void UpdateFromFileSystem()
    {
        UpdateFromFileSystem(null);
    }

    /// <summary>
    /// Updates the file properties from the file system using file cache service.
    /// </summary>
    /// <param name="fileCacheService">Optional file cache service for performance</param>
    public async void UpdateFromFileSystem(IFileCacheService? fileCacheService)
    {
        try
        {
            bool fileExists;
            long fileSize;
            string fileName;

            if (fileCacheService != null)
            {
                // Use cached file operations for better performance
                fileExists = await fileCacheService.FileExistsAsync(FilePath);
                fileSize = await fileCacheService.GetFileSizeAsync(FilePath);
                fileName = Path.GetFileName(FilePath);
            }
            else
            {
                // Fallback to direct file operations
                fileExists = File.Exists(FilePath);
                if (!fileExists)
                {
                    Status = BatchFileStatus.Error;
                    StatusMessage = "File not found";
                    return;
                }

                var fileInfo = new FileInfo(FilePath);
                fileSize = fileInfo.Length;
                fileName = fileInfo.Name;
            }

            if (!fileExists)
            {
                Status = BatchFileStatus.Error;
                StatusMessage = "File not found";
                return;
            }

            FileSizeBytes = fileSize;
            FileName = fileName;

            // Format file size
            const long kb = 1024;
            const long mb = kb * 1024;
            const long gb = mb * 1024;

            if (FileSizeBytes >= gb)
            {
                FormattedFileSize = $"{FileSizeBytes / (double)gb:F1} GB";
            }
            else if (FileSizeBytes >= mb)
            {
                FormattedFileSize = $"{FileSizeBytes / (double)mb:F1} MB";
            }
            else if (FileSizeBytes >= kb)
            {
                FormattedFileSize = $"{FileSizeBytes / (double)kb:F1} KB";
            }
            else
            {
                FormattedFileSize = $"{FileSizeBytes} bytes";
            }
        }
        catch (Exception)
        {
            Status = BatchFileStatus.Error;
            StatusMessage = "Error reading file";
        }
    }

    /// <summary>
    /// Updates network status and processing time estimation.
    /// </summary>
    /// <param name="isNetwork">Whether the file is on a network drive</param>
    /// <param name="estimatedMinutes">Estimated processing time in minutes</param>
    public void UpdateNetworkStatus(bool isNetwork, double estimatedMinutes)
    {
        IsNetworkFile = isNetwork;
        EstimatedProcessingTimeMinutes = estimatedMinutes;
        NetworkIndicator = isNetwork ? "🌐" : "";
        
        // Notify that the formatted time has changed
        OnPropertyChanged(nameof(FormattedEstimatedTime));
    }
}

/// <summary>
/// Status of a file in the batch processing queue.
/// </summary>
public enum BatchFileStatus
{
    Pending,
    Processing,
    Completed,
    Error,
    Cancelled
}
