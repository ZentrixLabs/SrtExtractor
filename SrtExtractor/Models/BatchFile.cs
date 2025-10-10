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
    /// Updates the file properties from the file system using file cache service.
    /// Now requires IFileCacheService to be provided (no nullable fallback).
    /// </summary>
    /// <param name="fileCacheService">File cache service for file operations</param>
    public async Task UpdateFromFileSystemAsync(IFileCacheService fileCacheService)
    {
        if (fileCacheService == null)
            throw new ArgumentNullException(nameof(fileCacheService));
            
        try
        {
            var fileExists = await fileCacheService.FileExistsAsync(FilePath);
            if (!fileExists)
            {
                Status = BatchFileStatus.Error;
                StatusMessage = "File not found";
                return;
            }

            FileSizeBytes = await fileCacheService.GetFileSizeAsync(FilePath);
            FileName = Path.GetFileName(FilePath);
            
            // Use shared utility for consistent file size formatting
            FormattedFileSize = Utils.FileUtilities.FormatFileSize(FileSizeBytes);
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
