using System.IO;
using SrtExtractor.Services.Interfaces;

namespace SrtExtractor.Services.Implementations;

/// <summary>
/// Implementation of network detection service.
/// </summary>
public class NetworkDetectionService : INetworkDetectionService
{
    private readonly ILoggingService _loggingService;

    // Performance data based on real-world testing
    private const double NetworkSpeedGBPerMinute = 1.2; // 32GB = 27min actual data
    private const double LocalSpeedGBPerMinute = 5.0;   // Estimated for local drives

    public NetworkDetectionService(ILoggingService loggingService)
    {
        _loggingService = loggingService;
    }

    /// <summary>
    /// Determines if the given path is on a network drive.
    /// </summary>
    /// <param name="filePath">The file path to check</param>
    /// <returns>True if the path is on a network drive, false otherwise</returns>
    public bool IsNetworkPath(string filePath)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            // Check for UNC paths (\\server\share)
            if (filePath.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase))
            {
                _loggingService.LogInfo($"Detected UNC network path: {filePath}");
                return true;
            }

            // Get the drive letter for the path
            var root = Path.GetPathRoot(filePath);
            if (string.IsNullOrEmpty(root))
                return false;

            // Check if it's a network drive
            var driveInfo = new DriveInfo(root);
            var isNetwork = driveInfo.DriveType == DriveType.Network;

            if (isNetwork)
            {
                _loggingService.LogInfo($"Detected network drive {root} for path: {filePath}");
            }

            return isNetwork;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Error detecting network path for {filePath}", ex);
            return false;
        }
    }

    /// <summary>
    /// Gets the estimated processing time for a file based on its size and location.
    /// </summary>
    /// <param name="filePath">The file path to analyze</param>
    /// <returns>Estimated processing time in minutes</returns>
    public double GetEstimatedProcessingTime(string filePath)
    {
        try
        {
            var fileSizeBytes = GetFileSize(filePath);
            if (fileSizeBytes == 0)
                return 0;

            var fileSizeGB = fileSizeBytes / (1024.0 * 1024.0 * 1024.0);
            var speedGBPerMinute = IsNetworkPath(filePath) ? NetworkSpeedGBPerMinute : LocalSpeedGBPerMinute;
            
            var estimatedMinutes = fileSizeGB / speedGBPerMinute;
            
            _loggingService.LogInfo($"Estimated processing time for {filePath}: {estimatedMinutes:F1} minutes (Size: {GetFormattedFileSize(filePath)}, Network: {IsNetworkPath(filePath)})");
            
            return estimatedMinutes;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Error calculating processing time for {filePath}", ex);
            return 0;
        }
    }

    /// <summary>
    /// Gets the file size in bytes for the given path.
    /// </summary>
    /// <param name="filePath">The file path</param>
    /// <returns>File size in bytes, or 0 if file doesn't exist</returns>
    public long GetFileSize(string filePath)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return 0;

            var fileInfo = new FileInfo(filePath);
            return fileInfo.Length;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Error getting file size for {filePath}", ex);
            return 0;
        }
    }

    /// <summary>
    /// Gets a formatted string representation of the file size.
    /// </summary>
    /// <param name="filePath">The file path</param>
    /// <returns>Formatted file size string (e.g., "32.0 GB")</returns>
    public string GetFormattedFileSize(string filePath)
    {
        try
        {
            var fileSizeBytes = GetFileSize(filePath);
            if (fileSizeBytes == 0)
                return "Unknown";

            const long kb = 1024;
            const long mb = kb * 1024;
            const long gb = mb * 1024;

            if (fileSizeBytes >= gb)
            {
                return $"{fileSizeBytes / (double)gb:F1} GB";
            }
            else if (fileSizeBytes >= mb)
            {
                return $"{fileSizeBytes / (double)mb:F1} MB";
            }
            else if (fileSizeBytes >= kb)
            {
                return $"{fileSizeBytes / (double)kb:F1} KB";
            }
            else
            {
                return $"{fileSizeBytes} bytes";
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Error formatting file size for {filePath}", ex);
            return "Error";
        }
    }
}
