using SrtExtractor.Models;

namespace SrtExtractor.Services.Interfaces;

/// <summary>
/// Service for managing application settings and configuration.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Load application settings from persistent storage.
    /// </summary>
    /// <returns>Application settings</returns>
    Task<AppSettings> LoadSettingsAsync();

    /// <summary>
    /// Save application settings to persistent storage.
    /// </summary>
    /// <param name="settings">Settings to save</param>
    Task SaveSettingsAsync(AppSettings settings);

    /// <summary>
    /// Get the application data directory path.
    /// </summary>
    /// <returns>Path to the application data directory</returns>
    Task<string> GetAppDataPathAsync();
}
