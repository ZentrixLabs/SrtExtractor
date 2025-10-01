namespace SrtExtractor.Services.Interfaces;

/// <summary>
/// Service for managing package installation via winget.
/// </summary>
public interface IWingetService
{
    /// <summary>
    /// Check if winget is available on the system.
    /// </summary>
    /// <returns>True if winget is available</returns>
    Task<bool> IsWingetAvailableAsync();

    /// <summary>
    /// Install a package using winget.
    /// </summary>
    /// <param name="packageId">The winget package ID</param>
    /// <returns>True if installation was successful</returns>
    Task<bool> InstallPackageAsync(string packageId);

    /// <summary>
    /// Get the installed version of a package.
    /// </summary>
    /// <param name="packageId">The winget package ID</param>
    /// <returns>Version string if installed, null otherwise</returns>
    Task<string?> GetInstalledVersionAsync(string packageId);

    /// <summary>
    /// Check if a package is installed.
    /// </summary>
    /// <param name="packageId">The winget package ID</param>
    /// <returns>True if the package is installed</returns>
    Task<bool> IsPackageInstalledAsync(string packageId);

    /// <summary>
    /// Install MKVToolNix via winget.
    /// </summary>
    Task<bool> InstallMkvToolNixAsync();

    /// <summary>
    /// Install Subtitle Edit CLI via dotnet tool.
    /// </summary>
    Task<bool> InstallSubtitleEditAsync();
}
