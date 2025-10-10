using System.IO;

namespace SrtExtractor.Services.Interfaces;

/// <summary>
/// Service for caching file information to avoid repeated file system calls.
/// </summary>
public interface IFileCacheService
{
    /// <summary>
    /// Check if a file exists, using cache when possible.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <returns>True if file exists, false otherwise</returns>
    Task<bool> FileExistsAsync(string filePath);

    /// <summary>
    /// Get file information, using cache when possible.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <returns>FileInfo object or null if file doesn't exist</returns>
    Task<FileInfo?> GetFileInfoAsync(string filePath);

    /// <summary>
    /// Get file size in bytes, using cache when possible.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <returns>File size in bytes, or 0 if file doesn't exist</returns>
    Task<long> GetFileSizeAsync(string filePath);

    /// <summary>
    /// Clear the cache for a specific file.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    void InvalidateFile(string filePath);

    /// <summary>
    /// Clear the entire cache.
    /// </summary>
    void ClearCache();

    /// <summary>
    /// Get cache statistics for debugging.
    /// </summary>
    /// <returns>Cache hit/miss statistics</returns>
    (int Hits, int Misses) GetCacheStats();
}
