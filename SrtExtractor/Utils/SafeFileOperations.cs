using System.IO;
using System.Security;

namespace SrtExtractor.Utils;

/// <summary>
/// Utility class for safe file operations that prevent path traversal attacks.
/// </summary>
public static class SafeFileOperations
{
    /// <summary>
    /// Allowed base paths where output files can be written.
    /// This prevents writing to system directories or other sensitive locations.
    /// </summary>
    private static readonly string[] AllowedBasePaths = 
    {
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
        Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        Path.GetTempPath()
    };
    
    /// <summary>
    /// Validates an output path to ensure it's safe to write to.
    /// </summary>
    /// <param name="outputPath">The output path to validate</param>
    /// <returns>The validated full path</returns>
    /// <exception cref="SecurityException">If the path is not in an allowed directory</exception>
    public static string ValidateOutputPath(string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("Output path cannot be empty", nameof(outputPath));
        
        // First apply basic path validation
        var validatedPath = PathValidator.ValidateAndSanitizeFilePath(outputPath);
        
        // Get the full path to resolve any relative paths
        var fullPath = Path.GetFullPath(validatedPath);
        
        // Check if the path is a network path (UNC path like \\server\share)
        // Allow network paths but log them for security monitoring
        var isNetworkPath = fullPath.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase);
        
        // Check if path is within allowed directories (unless it's a network path or same directory as source)
        if (!isNetworkPath)
        {
            var isAllowed = AllowedBasePaths.Any(basePath => 
                fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase));
            
            // Also allow the current directory and user profile
            var currentDirectory = Directory.GetCurrentDirectory();
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            
            isAllowed = isAllowed || 
                       fullPath.StartsWith(currentDirectory, StringComparison.OrdinalIgnoreCase) ||
                       fullPath.StartsWith(userProfile, StringComparison.OrdinalIgnoreCase);
            
            if (!isAllowed)
            {
                throw new SecurityException(
                    $"Output path is not in an allowed directory. " +
                    $"Files can only be saved to: Documents, Desktop, Videos, Music, Pictures, Temp, or the current directory. " +
                    $"Path: {fullPath}");
            }
        }
        
        // Additional check: Verify no path traversal in the directory component
        var directory = Path.GetDirectoryName(fullPath);
        if (directory != null && directory.Contains(".."))
        {
            throw new SecurityException("Path traversal detected in output path");
        }
        
        // Check that we're not trying to write to Windows or Program Files
        var windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var programFilesDir = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86Dir = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        
        if (fullPath.StartsWith(windowsDir, StringComparison.OrdinalIgnoreCase) ||
            fullPath.StartsWith(programFilesDir, StringComparison.OrdinalIgnoreCase) ||
            fullPath.StartsWith(programFilesX86Dir, StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityException("Cannot write to Windows or Program Files directories");
        }
        
        return fullPath;
    }
    
    /// <summary>
    /// Safely creates a directory after validating the path.
    /// </summary>
    /// <param name="path">The directory path to create</param>
    /// <exception cref="SecurityException">If the path is not safe</exception>
    public static void SafeCreateDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
        
        // Validate the path before creating
        var validatedPath = ValidateOutputPath(path);
        
        // Create the directory
        Directory.CreateDirectory(validatedPath);
    }
    
    /// <summary>
    /// Ensures the directory for an output file exists, creating it if necessary.
    /// </summary>
    /// <param name="filePath">The file path whose directory should be created</param>
    public static void EnsureOutputDirectoryExists(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            SafeCreateDirectory(directory);
        }
    }
    
    /// <summary>
    /// Validates an output path and ensures its directory exists.
    /// </summary>
    /// <param name="outputPath">The output file path</param>
    /// <returns>The validated full path</returns>
    public static string ValidateAndPrepareOutputPath(string outputPath)
    {
        var validatedPath = ValidateOutputPath(outputPath);
        EnsureOutputDirectoryExists(validatedPath);
        return validatedPath;
    }
}

