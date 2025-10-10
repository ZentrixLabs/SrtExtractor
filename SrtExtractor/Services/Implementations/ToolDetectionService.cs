using System.IO;
using SrtExtractor.Models;
using SrtExtractor.Services.Interfaces;

namespace SrtExtractor.Services.Implementations;

/// <summary>
/// Service for detecting and validating external tools.
/// </summary>
public class ToolDetectionService : IToolDetectionService
{
    private readonly ILoggingService _loggingService;
    private readonly IProcessRunner _processRunner;

    // Common installation paths for MKVToolNix
    private readonly string[] _mkvCommonPaths = {
        @"C:\Program Files\MKVToolNix\",
        @"C:\Program Files (x86)\MKVToolNix\",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "MKVToolNix")
    };

    // Common installation paths for Subtitle Edit CLI (seconv.exe)
    private readonly string[] _subtitleEditCommonPaths = {
        // First check our built CLI in the output directory
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "seconv.exe"),
        // Then check common installation paths
        @"C:\Program Files\Subtitle Edit\seconv.exe",
        @"C:\Program Files (x86)\Subtitle Edit\seconv.exe",
        @"C:\Program Files\SubtitleEdit-CLI\seconv.exe",
        @"C:\Program Files (x86)\SubtitleEdit-CLI\seconv.exe",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "SubtitleEdit-CLI", "seconv.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet", "tools", "seconv.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "tools", "seconv.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "dotnet", "tools", "seconv.exe")
    };

    public ToolDetectionService(ILoggingService loggingService, IProcessRunner processRunner)
    {
        _loggingService = loggingService;
        _processRunner = processRunner;
    }

    public async Task<ToolStatus> CheckMkvToolNixAsync()
    {
        _loggingService.LogInfo("Checking MKVToolNix installation");

        // Check for mkvmerge.exe
        var mkvmergePath = await FindToolPathAsync("mkvmerge.exe", _mkvCommonPaths);
        if (mkvmergePath == null)
        {
            return new ToolStatus(false, null, null, "MKVToolNix not found in common locations or PATH");
        }

        // Validate the tool
        var isValid = await ValidateToolAsync(mkvmergePath);
        if (!isValid)
        {
            return new ToolStatus(false, mkvmergePath, null, "mkvmerge.exe found but validation failed");
        }

        // Get version
        var version = await GetToolVersionAsync(mkvmergePath);
        
        _loggingService.LogToolDetection("MKVToolNix", new ToolStatus(true, mkvmergePath, version, null));
        return new ToolStatus(true, mkvmergePath, version, null);
    }

    public async Task<ToolStatus> CheckSubtitleEditAsync()
    {
        _loggingService.LogInfo("Checking Subtitle Edit CLI installation");
        _loggingService.LogInfo($"AppDomain.CurrentDomain.BaseDirectory: {AppDomain.CurrentDomain.BaseDirectory}");

        // Look for seconv.exe (the actual CLI executable)
        var cliPath = await FindToolPathAsync("seconv.exe", _subtitleEditCommonPaths);
        if (cliPath != null)
        {
            var isValid = await ValidateToolAsync(cliPath);
            if (isValid)
            {
                var version = await GetToolVersionAsync(cliPath);
                var isBuiltIn = cliPath.Contains(AppDomain.CurrentDomain.BaseDirectory);
                var statusText = isBuiltIn ? "Built-in" : "External";
                _loggingService.LogToolDetection("Subtitle Edit CLI", new ToolStatus(true, cliPath, version, null));
                return new ToolStatus(true, cliPath, version, null);
            }
        }

        // Also check if it's in PATH
        try
        {
            var (exitCode, _, _) = await _processRunner.RunAsync("seconv", new[] { "--help" });
            if (exitCode == 0 || exitCode == 1) // Help usually returns 1
            {
                // Try to get version even for PATH tools
                var version = await GetToolVersionAsync("seconv");
                _loggingService.LogToolDetection("Subtitle Edit CLI", new ToolStatus(true, "seconv", version, null));
                return new ToolStatus(true, "seconv", version, null);
            }
        }
        catch
        {
            // Tool not in PATH
        }

        return new ToolStatus(false, null, null, "Subtitle Edit CLI (seconv.exe) not found. Will be built automatically on next build.");
    }

    public async Task<ToolStatus> CheckFfmpegAsync()
    {
        _loggingService.LogInfo("Checking FFmpeg installation");

        // Common installation paths for FFmpeg
        var commonPaths = new[]
        {
            // First check our built FFmpeg in the output directory
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe"),
            // Then check common installation paths
            @"C:\ffmpeg\bin\ffmpeg.exe",
            @"C:\Program Files\ffmpeg\bin\ffmpeg.exe",
            @"C:\Program Files (x86)\ffmpeg\bin\ffmpeg.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "ffmpeg", "bin", "ffmpeg.exe")
        };

        var ffmpegPath = await FindToolPathAsync("ffmpeg.exe", commonPaths);
        if (ffmpegPath != null)
        {
            var isValid = await ValidateToolAsync(ffmpegPath);
            if (isValid)
            {
                var version = await GetToolVersionAsync(ffmpegPath);
                var isBuiltIn = ffmpegPath.Contains(AppDomain.CurrentDomain.BaseDirectory);
                var statusText = isBuiltIn ? "Built-in" : "External";
                _loggingService.LogToolDetection("FFmpeg", new ToolStatus(true, ffmpegPath, version, null));
                return new ToolStatus(true, ffmpegPath, version, null);
            }
        }

        // Also check if it's in PATH
        try
        {
            var (exitCode, _, _) = await _processRunner.RunAsync("ffmpeg", new[] { "-version" });
            if (exitCode == 0)
            {
                // Try to get version even for PATH tools
                var version = await GetToolVersionAsync("ffmpeg");
                _loggingService.LogToolDetection("FFmpeg", new ToolStatus(true, "ffmpeg", version, null));
                return new ToolStatus(true, "ffmpeg", version, null);
            }
        }
        catch
        {
            // Tool not in PATH
        }

        return new ToolStatus(false, null, null, "FFmpeg not found. Will be downloaded automatically on next build.");
    }

    public async Task<string?> FindToolPathAsync(string toolName, string[] commonPaths)
    {
        _loggingService.LogInfo($"Searching for {toolName} in {commonPaths.Length} common paths");

        // Check common installation paths
        foreach (var path in commonPaths)
        {
            var fullPath = Path.Combine(path, toolName);
            _loggingService.LogInfo($"Checking path: {fullPath} (exists: {File.Exists(fullPath)})");
            if (File.Exists(fullPath))
            {
                _loggingService.LogInfo($"Found {toolName} at {fullPath}");
                return fullPath;
            }
        }

        // Check PATH environment variable
        try
        {
            var whereResult = await _processRunner.RunAsync("where", new[] { toolName });
            if (whereResult.ExitCode == 0 && !string.IsNullOrEmpty(whereResult.StdOut))
            {
                var path = whereResult.StdOut.Trim().Split('\n')[0].Trim();
                _loggingService.LogInfo($"Found {toolName} in PATH at {path}");
                return path;
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogWarning($"Failed to check PATH for {toolName}: {ex.Message}");
        }

        _loggingService.LogWarning($"{toolName} not found in common paths or PATH");
        return null;
    }

    private async Task<string?> FindDotnetToolAsync(string toolName)
    {
        try
        {
            _loggingService.LogInfo($"Checking for dotnet tool: {toolName}");
            var (exitCode, stdout, _) = await _processRunner.RunAsync("dotnet", new[] { "tool", "list", "--global" });
            
            if (exitCode == 0 && !string.IsNullOrEmpty(stdout))
            {
                var lines = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.Contains(toolName, StringComparison.OrdinalIgnoreCase))
                    {
                        // Get the tool path
                        var toolPath = await GetDotnetToolPathAsync(toolName);
                        if (!string.IsNullOrEmpty(toolPath))
                        {
                            _loggingService.LogInfo($"Found dotnet tool {toolName} at {toolPath}");
                            return toolPath;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogWarning($"Failed to check dotnet tools: {ex.Message}");
        }

        return null;
    }

    private async Task<string?> GetDotnetToolPathAsync(string toolName)
    {
        try
        {
            // Get the dotnet tools directory
            var (exitCode, stdout, _) = await _processRunner.RunAsync("dotnet", new[] { "tool", "list", "--global" });
            
            if (exitCode == 0 && !string.IsNullOrEmpty(stdout))
            {
                // Check if the tool is installed
                if (stdout.Contains(toolName, StringComparison.OrdinalIgnoreCase))
                {
                    // Try to find the tool in common dotnet tool locations
                    var dotnetToolPaths = new[]
                    {
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet", "tools", toolName + ".exe"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "tools", toolName + ".exe"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "dotnet", "tools", toolName + ".exe")
                    };

                    foreach (var toolPath in dotnetToolPaths)
                    {
                        if (File.Exists(toolPath))
                        {
                            _loggingService.LogInfo($"Found dotnet tool {toolName} at {toolPath}");
                            return toolPath;
                        }
                    }

                    // If not found in common locations, try to run it directly
                    try
                    {
                        var (testExitCode, _, _) = await _processRunner.RunAsync(toolName, new[] { "--help" });
                        if (testExitCode == 0 || testExitCode == 1) // Help usually returns 1
                        {
                            return toolName; // Tool is in PATH
                        }
                    }
                    catch
                    {
                        // Tool not in PATH
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogWarning($"Failed to get dotnet tool path: {ex.Message}");
        }

        return null;
    }

    public async Task<bool> ValidateToolAsync(string toolPath)
    {
        if (!File.Exists(toolPath))
        {
            _loggingService.LogWarning($"Tool path does not exist: {toolPath}");
            return false;
        }

        try
        {
            var toolName = Path.GetFileNameWithoutExtension(toolPath).ToLowerInvariant();
            
            if (toolName.Contains("mkvmerge") || toolName.Contains("mkvextract"))
            {
                // MKVToolNix tools - try --version
                var (exitCode, _, _) = await _processRunner.RunAsync(toolPath, new[] { "--version" });
                if (exitCode == 0)
                {
                    _loggingService.LogInfo($"MKVToolNix validation successful: {toolPath}");
                    return true;
                }
            }
            else if (toolName.Contains("subtitleedit"))
            {
                // Subtitle Edit - try /help (but it might open GUI, so we'll just check if file exists and is executable)
                // For now, just verify the file exists and is executable
                _loggingService.LogInfo($"Subtitle Edit validation successful (file exists): {toolPath}");
                return true;
            }
            else if (toolName.Contains("ffmpeg") || toolName.Contains("ffprobe"))
            {
                // FFmpeg tools - just check if file exists and is executable
                // FFmpeg has quirky behavior with --version, so we'll trust that if it exists, it works
                _loggingService.LogInfo($"FFmpeg validation successful (file exists): {toolPath}");
                return true;
            }
            else
            {
                // Generic tool - try common help flags
                var helpFlags = new[] { "--version", "/version", "--help", "/help", "/?" };
                foreach (var flag in helpFlags)
                {
                    try
                    {
                        var (exitCode, _, _) = await _processRunner.RunAsync(toolPath, new[] { flag });
                        if (exitCode == 0)
                        {
                            _loggingService.LogInfo($"Tool validation successful with {flag}: {toolPath}");
                            return true;
                        }
                    }
                    catch
                    {
                        // Continue to next flag
                    }
                }
            }

            _loggingService.LogWarning($"Tool validation failed: {toolPath}");
            return false;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Tool validation exception: {toolPath}", ex);
            return false;
        }
    }

    private async Task<string?> GetToolVersionAsync(string toolPath)
    {
        try
        {
            var toolName = Path.GetFileNameWithoutExtension(toolPath).ToLowerInvariant();
            
            // Different tools use different version flags
            string[] versionArgs;
            if (toolName.Contains("ffmpeg") || toolName.Contains("ffprobe"))
            {
                versionArgs = new[] { "-version" };
            }
            else if (toolName.Contains("seconv"))
            {
                versionArgs = new[] { "--version" };
            }
            else
            {
                versionArgs = new[] { "--version" };
            }
            
            var (exitCode, stdout, stderr) = await _processRunner.RunAsync(toolPath, versionArgs);
            
            // Some tools return version in stdout, some in stderr, and some use exit code 1
            if ((exitCode == 0 || exitCode == 1) && !string.IsNullOrEmpty(stdout))
            {
                // Extract version from output
                var lines = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length > 0)
                {
                    var firstLine = lines[0].Trim();
                    
                    // Parse version based on tool type
                    if (toolName.Contains("ffmpeg") || toolName.Contains("ffprobe"))
                    {
                        // FFmpeg version format: "ffmpeg version N-xxxxx-gxxxxxxx Copyright..."
                        // Extract just the version number
                        var versionMatch = System.Text.RegularExpressions.Regex.Match(firstLine, @"ffmpeg version\s+([^\s]+)");
                        if (versionMatch.Success)
                        {
                            return versionMatch.Groups[1].Value;
                        }
                    }
                    else if (toolName.Contains("mkvmerge") || toolName.Contains("mkvextract"))
                    {
                        // MKVToolNix format: "mkvmerge v84.0 ('Something') 64-bit"
                        var versionMatch = System.Text.RegularExpressions.Regex.Match(firstLine, @"v(\d+\.\d+(?:\.\d+)?)");
                        if (versionMatch.Success)
                        {
                            return versionMatch.Groups[1].Value;
                        }
                    }
                    else if (toolName.Contains("seconv"))
                    {
                        // Try to extract version number from various formats
                        var versionMatch = System.Text.RegularExpressions.Regex.Match(firstLine, @"(\d+\.\d+(?:\.\d+)?)");
                        if (versionMatch.Success)
                        {
                            return versionMatch.Groups[1].Value;
                        }
                    }
                    
                    // Fallback: return first 50 chars of first line
                    return firstLine.Length > 50 ? firstLine.Substring(0, 50) + "..." : firstLine;
                }
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogWarning($"Failed to get version for {toolPath}: {ex.Message}");
        }

        return "Unknown";
    }
}
