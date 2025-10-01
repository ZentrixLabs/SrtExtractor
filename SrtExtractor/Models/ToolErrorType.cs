namespace SrtExtractor.Models;

/// <summary>
/// Types of errors that can occur during tool detection and validation.
/// </summary>
public enum ToolErrorType
{
    /// <summary>
    /// Tool was not found in any expected location
    /// </summary>
    NotFound,
    
    /// <summary>
    /// Tool path is invalid or points to non-existent file
    /// </summary>
    PathInvalid,
    
    /// <summary>
    /// Tool version is incompatible with requirements
    /// </summary>
    VersionIncompatible,
    
    /// <summary>
    /// Tool execution failed during validation
    /// </summary>
    ExecutionFailed,
    
    /// <summary>
    /// Winget is not available for tool installation
    /// </summary>
    WingetNotAvailable
}
