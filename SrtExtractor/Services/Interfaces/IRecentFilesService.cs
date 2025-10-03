namespace SrtExtractor.Services.Interfaces;

/// <summary>
/// Service for managing recently processed files.
/// </summary>
public interface IRecentFilesService
{
    /// <summary>
    /// Add a file to the recent files list.
    /// </summary>
    /// <param name="filePath">Path to the file to add</param>
    Task AddFileAsync(string filePath);

    /// <summary>
    /// Get the list of recent files.
    /// </summary>
    /// <returns>List of recent file paths, ordered by most recent first</returns>
    Task<List<string>> GetRecentFilesAsync();

    /// <summary>
    /// Clear all recent files.
    /// </summary>
    Task ClearRecentFilesAsync();

    /// <summary>
    /// Remove a specific file from the recent files list.
    /// </summary>
    /// <param name="filePath">Path to the file to remove</param>
    Task RemoveFileAsync(string filePath);

    /// <summary>
    /// Check if a file exists in the recent files list.
    /// </summary>
    /// <param name="filePath">Path to the file to check</param>
    /// <returns>True if the file is in the recent files list</returns>
    Task<bool> IsRecentFileAsync(string filePath);

    /// <summary>
    /// Event fired when the recent files list changes.
    /// </summary>
    event EventHandler<List<string>> RecentFilesChanged;
}
