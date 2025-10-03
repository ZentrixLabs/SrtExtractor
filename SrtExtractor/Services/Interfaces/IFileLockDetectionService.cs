using System.IO;

namespace SrtExtractor.Services.Interfaces;

/// <summary>
/// Service for detecting file locks and checking file accessibility.
/// </summary>
public interface IFileLockDetectionService
{
    /// <summary>
    /// Checks if a file is locked or in use by another process.
    /// </summary>
    /// <param name="filePath">Path to the file to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if file is locked, false if accessible</returns>
    Task<bool> IsFileLockedAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a file exists and is accessible for reading.
    /// </summary>
    /// <param name="filePath">Path to the file to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if file exists and is accessible, false otherwise</returns>
    Task<bool> IsFileAccessibleAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures a directory exists, creating it if necessary.
    /// </summary>
    /// <param name="directoryPath">Path to the directory</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task EnsureDirectoryExistsAsync(string directoryPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets file information asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>File information or null if file doesn't exist</returns>
    Task<FileInfo?> GetFileInfoAsync(string filePath, CancellationToken cancellationToken = default);
}
