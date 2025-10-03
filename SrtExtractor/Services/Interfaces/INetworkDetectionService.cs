using System.IO;

namespace SrtExtractor.Services.Interfaces;

/// <summary>
/// Service for detecting network drives and paths.
/// </summary>
public interface INetworkDetectionService
{
    /// <summary>
    /// Determines if the given path is on a network drive.
    /// </summary>
    /// <param name="filePath">The file path to check</param>
    /// <returns>True if the path is on a network drive, false otherwise</returns>
    bool IsNetworkPath(string filePath);

    /// <summary>
    /// Gets the estimated processing time for a file based on its size and location.
    /// </summary>
    /// <param name="filePath">The file path to analyze</param>
    /// <returns>Estimated processing time in minutes</returns>
    double GetEstimatedProcessingTime(string filePath);

    /// <summary>
    /// Gets the file size in bytes for the given path.
    /// </summary>
    /// <param name="filePath">The file path</param>
    /// <returns>File size in bytes, or 0 if file doesn't exist</returns>
    long GetFileSize(string filePath);

    /// <summary>
    /// Gets a formatted string representation of the file size.
    /// </summary>
    /// <param name="filePath">The file path</param>
    /// <returns>Formatted file size string (e.g., "32.0 GB")</returns>
    string GetFormattedFileSize(string filePath);
}
