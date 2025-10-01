using SrtExtractor.Services.Interfaces;

namespace SrtExtractor.Services.Implementations;

/// <summary>
/// Service for managing package installation via winget.
/// </summary>
public class WingetService : IWingetService
{
    private readonly ILoggingService _loggingService;
    private readonly IProcessRunner _processRunner;

    // Package IDs for the tools we need
    private const string MKV_PACKAGE_ID = "MoritzBunkus.MKVToolNix";
    private const string SUBTITLE_EDIT_PACKAGE_ID = "SubtitleEdit.SubtitleEdit";
    private const string SUBTITLE_EDIT_CLI_PACKAGE_ID = "SubtitleEdit.CLI";

    public WingetService(ILoggingService loggingService, IProcessRunner processRunner)
    {
        _loggingService = loggingService;
        _processRunner = processRunner;
    }

    public async Task<bool> IsWingetAvailableAsync()
    {
        try
        {
            _loggingService.LogInfo("Checking winget availability");
            var (exitCode, _, _) = await _processRunner.RunAsync("winget", "--version");
            var isAvailable = exitCode == 0;
            
            _loggingService.LogInfo($"Winget available: {isAvailable}");
            return isAvailable;
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to check winget availability", ex);
            return false;
        }
    }

    public async Task<bool> InstallPackageAsync(string packageId)
    {
        try
        {
            _loggingService.LogInfo($"Installing package: {packageId}");
            
            // Check if winget is available first
            if (!await IsWingetAvailableAsync())
            {
                _loggingService.LogError("Winget is not available for package installation");
                return false;
            }

            // Install the package with silent flags
            var args = $"install {packageId} --accept-package-agreements --accept-source-agreements --silent";
            var (exitCode, stdout, stderr) = await _processRunner.RunAsync("winget", args);

            if (exitCode == 0)
            {
                _loggingService.LogInstallation(packageId, true);
                return true;
            }
            else
            {
                _loggingService.LogInstallation(packageId, false, stderr);
                return false;
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Failed to install package {packageId}", ex);
            return false;
        }
    }

    public async Task<string?> GetInstalledVersionAsync(string packageId)
    {
        try
        {
            _loggingService.LogInfo($"Getting installed version for package: {packageId}");
            
            var args = $"list {packageId} --exact";
            var (exitCode, stdout, stderr) = await _processRunner.RunAsync("winget", args);

            if (exitCode == 0 && !string.IsNullOrEmpty(stdout))
            {
                // Parse the output to extract version
                var lines = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.Contains(packageId))
                    {
                        var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2)
                        {
                            var version = parts[1].Trim();
                            _loggingService.LogInfo($"Found installed version: {version}");
                            return version;
                        }
                    }
                }
            }

            _loggingService.LogWarning($"Could not determine version for package: {packageId}");
            return null;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Failed to get version for package {packageId}", ex);
            return null;
        }
    }

    public async Task<bool> IsPackageInstalledAsync(string packageId)
    {
        try
        {
            _loggingService.LogInfo($"Checking if package is installed: {packageId}");
            
            var version = await GetInstalledVersionAsync(packageId);
            var isInstalled = !string.IsNullOrEmpty(version);
            
            _loggingService.LogInfo($"Package {packageId} installed: {isInstalled}");
            return isInstalled;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Failed to check if package {packageId} is installed", ex);
            return false;
        }
    }

    /// <summary>
    /// Install MKVToolNix via winget.
    /// </summary>
    public async Task<bool> InstallMkvToolNixAsync()
    {
        return await InstallPackageAsync(MKV_PACKAGE_ID);
    }

    /// <summary>
    /// Install Subtitle Edit CLI by downloading from GitHub releases.
    /// </summary>
    public Task<bool> InstallSubtitleEditAsync()
    {
        _loggingService.LogInfo("Opening SubtitleEdit-CLI GitHub releases page...");
        
        try
        {
            // Open the GitHub releases page in the default browser
            var url = "https://github.com/SubtitleEdit/subtitleedit-cli/releases";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            
            _loggingService.LogInfo("GitHub releases page opened in browser");
            _loggingService.LogInstallation("Subtitle Edit CLI", true, "GitHub page opened - please download and extract seconv.exe");
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to open GitHub releases page", ex);
            _loggingService.LogInstallation("Subtitle Edit CLI", false, "Failed to open browser");
            return Task.FromResult(false);
        }
    }
}
