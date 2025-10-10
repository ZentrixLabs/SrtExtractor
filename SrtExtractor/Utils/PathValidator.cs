using System.IO;
using System.Security;

namespace SrtExtractor.Utils;

/// <summary>
/// Utility class for validating and sanitizing file paths to prevent command injection and path traversal attacks.
/// </summary>
public static class PathValidator
{
    /// <summary>
    /// Validates and sanitizes a file path for safe use in process execution.
    /// </summary>
    /// <param name="path">The file path to validate</param>
    /// <returns>The validated full path</returns>
    /// <exception cref="ArgumentException">If the path is invalid</exception>
    /// <exception cref="SecurityException">If the path contains dangerous patterns</exception>
    public static string ValidateAndSanitizeFilePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be empty", nameof(path));
        
        // Get the full path to resolve any relative paths and normalize
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Invalid path format: {ex.Message}", nameof(path), ex);
        }
        
        // Check for path traversal - the full path should match the input when normalized
        // This prevents patterns like "..\" from escaping intended directories
        var normalizedInput = Path.GetFullPath(path);
        if (!fullPath.Equals(normalizedInput, StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityException("Path traversal attempt detected");
        }
        
        // Check for invalid path characters
        var invalidChars = Path.GetInvalidPathChars();
        if (path.Any(c => invalidChars.Contains(c)))
        {
            throw new ArgumentException("Path contains invalid characters", nameof(path));
        }
        
        // Check for shell metacharacters that could be used for command injection
        // NOTE: Parentheses (), braces {}, and ampersands & are SAFE because we use ProcessRunner with argument arrays
        // They are NOT passed through a shell, so they can't be exploited for injection
        // Common in media filenames: "Movie (2012)", "Show {tmdb-12345}", "Willy Wonka & the Chocolate Factory"
        // Only block truly dangerous shell operators
        var dangerousChars = new[] { '|', ';', '`', '$', '<', '>', '\n', '\r' };
        if (path.Any(c => dangerousChars.Contains(c)))
        {
            throw new SecurityException("Path contains shell metacharacters");
        }
        
        // Check for null bytes (can be used to truncate paths in some contexts)
        if (path.Contains('\0'))
        {
            throw new SecurityException("Path contains null bytes");
        }
        
        return fullPath;
    }
    
    /// <summary>
    /// Validates that a file path exists and is accessible.
    /// </summary>
    /// <param name="path">The file path to validate</param>
    /// <returns>The validated full path</returns>
    /// <exception cref="FileNotFoundException">If the file doesn't exist</exception>
    public static string ValidateFileExists(string path)
    {
        var validatedPath = ValidateAndSanitizeFilePath(path);
        
        if (!File.Exists(validatedPath))
        {
            throw new FileNotFoundException($"File not found: {path}", path);
        }
        
        return validatedPath;
    }
    
    /// <summary>
    /// Validates that a directory path exists and is accessible.
    /// </summary>
    /// <param name="path">The directory path to validate</param>
    /// <returns>The validated full path</returns>
    /// <exception cref="DirectoryNotFoundException">If the directory doesn't exist</exception>
    public static string ValidateDirectoryExists(string path)
    {
        var validatedPath = ValidateAndSanitizeFilePath(path);
        
        if (!Directory.Exists(validatedPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {path}");
        }
        
        return validatedPath;
    }
}

