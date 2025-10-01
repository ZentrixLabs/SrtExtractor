namespace SrtExtractor.Models;

/// <summary>
/// Represents the status of an external tool (MKVToolNix or Subtitle Edit).
/// </summary>
/// <param name="IsInstalled">Whether the tool is installed and available</param>
/// <param name="Path">The path to the tool executable (if installed)</param>
/// <param name="Version">The version of the tool (if available)</param>
/// <param name="ErrorMessage">Error message if tool is not available</param>
public record ToolStatus(
    bool IsInstalled,
    string? Path,
    string? Version,
    string? ErrorMessage
);
