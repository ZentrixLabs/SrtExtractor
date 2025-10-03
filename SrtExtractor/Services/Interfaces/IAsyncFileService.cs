using System.IO;

namespace SrtExtractor.Services.Interfaces;

/// <summary>
/// Service for performing file operations asynchronously.
/// </summary>
public interface IAsyncFileService
{
    /// <summary>
    /// Checks if a file exists asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if file exists, false otherwise</returns>
    Task<bool> FileExistsAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a directory exists asynchronously.
    /// </summary>
    /// <param name="directoryPath">Path to the directory</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if directory exists, false otherwise</returns>
    Task<bool> DirectoryExistsAsync(string directoryPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a directory asynchronously.
    /// </summary>
    /// <param name="directoryPath">Path to the directory</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task CreateDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures a directory exists, creating it if necessary.
    /// </summary>
    /// <param name="directoryPath">Path to the directory</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task EnsureDirectoryExistsAsync(string directoryPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads all text from a file asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>File content as string</returns>
    Task<string> ReadAllTextAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes all text to a file asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <param name="content">Content to write</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task WriteAllTextAsync(string filePath, string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a file asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task DeleteFileAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets file size asynchronously.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>File size in bytes, or null if file doesn't exist</returns>
    Task<long?> GetFileSizeAsync(string filePath, CancellationToken cancellationToken = default);
}
