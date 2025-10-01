using SrtExtractor.Models;

namespace SrtExtractor.Services.Interfaces;

/// <summary>
/// Service for detecting and validating external tools (MKVToolNix and Subtitle Edit).
/// </summary>
public interface IToolDetectionService
{
    /// <summary>
    /// Check if MKVToolNix is installed and available.
    /// </summary>
    /// <returns>Tool status with installation details</returns>
    Task<ToolStatus> CheckMkvToolNixAsync();

    /// <summary>
    /// Check if Subtitle Edit is installed and available.
    /// </summary>
    /// <returns>Tool status with installation details</returns>
    Task<ToolStatus> CheckSubtitleEditAsync();

    /// <summary>
    /// Find a tool executable in common installation locations.
    /// </summary>
    /// <param name="toolName">Name of the tool to find</param>
    /// <param name="commonPaths">Array of common installation paths to search</param>
    /// <returns>Path to the tool if found, null otherwise</returns>
    Task<string?> FindToolPathAsync(string toolName, string[] commonPaths);

    /// <summary>
    /// Validate that a tool path points to a working executable.
    /// </summary>
    /// <param name="toolPath">Path to the tool executable</param>
    /// <returns>True if the tool is valid and executable</returns>
    Task<bool> ValidateToolAsync(string toolPath);
}
