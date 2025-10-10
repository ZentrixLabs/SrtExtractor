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
    /// Allows writing to user directories, network paths, and any location where the user selected a source file.
    /// </summary>
    /// <param name="outputPath">The output path to validate</param>
    /// <param name="sourceFilePath">Optional source file path - if provided, output can be written to same directory</param>
    /// <returns>The validated full path</returns>
    /// <exception cref="SecurityException">If the path is not in an allowed directory</exception>
    public static string ValidateOutputPath(string outputPath, string? sourceFilePath = null)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("Output path cannot be empty", nameof(outputPath));
        
        // First apply basic path validation
        var validatedPath = PathValidator.ValidateAndSanitizeFilePath(outputPath);
        
        // Get the full path to resolve any relative paths
        var fullPath = Path.GetFullPath(validatedPath);
        
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
        
        // If source file path is provided, allow writing to the same directory
        // This is safe because the user explicitly selected that source file
        if (!string.IsNullOrEmpty(sourceFilePath))
        {
            try
            {
                var sourceDir = Path.GetDirectoryName(Path.GetFullPath(sourceFilePath));
                var outputDir = Path.GetDirectoryName(fullPath);
                
                // Allow if output is in same directory as source (most common case)
                if (sourceDir != null && outputDir != null && 
                    sourceDir.Equals(outputDir, StringComparison.OrdinalIgnoreCase))
                {
                    return fullPath;
                }
            }
            catch
            {
                // If we can't determine source directory, continue with other checks
            }
        }
        
        // Check if the path is a network path (UNC path like \\server\share OR mapped drive like X:, Y:, Z:)
        var isNetworkPath = fullPath.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase);
        var isNetworkDrive = false;
        
        // Check if it's a mapped network drive (X:, Y:, Z:, etc.)
        if (!isNetworkPath && fullPath.Length >= 2 && fullPath[1] == ':')
        {
            try
            {
                var driveInfo = new DriveInfo(fullPath.Substring(0, 1));
                isNetworkDrive = driveInfo.DriveType == DriveType.Network;
            }
            catch
            {
                // If we can't determine drive type, assume it's safe
            }
        }
        
        // Allow network paths (both UNC and mapped drives)
        // Users choose these locations explicitly, they should be able to save there
        if (isNetworkPath || isNetworkDrive)
        {
            return fullPath;
        }
        
        // Check if path is within allowed user directories
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
                $"Files can only be saved to: Documents, Desktop, Videos, Music, Pictures, Temp, network locations, or the source file directory. " +
                $"Path: {fullPath}");
        }
        
        return fullPath;
    }
    
    /// <summary>
    /// Safely creates a directory after validating the path.
    /// </summary>
    /// <param name="path">The directory path to create</param>
    /// <param name="sourceFilePath">Optional source file path for validation context</param>
    /// <exception cref="SecurityException">If the path is not safe</exception>
    public static void SafeCreateDirectory(string path, string? sourceFilePath = null)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
        
        // Validate the path before creating
        var validatedPath = ValidateOutputPath(path, sourceFilePath);
        
        // Create the directory
        Directory.CreateDirectory(validatedPath);
    }
    
    /// <summary>
    /// Ensures the directory for an output file exists, creating it if necessary.
    /// </summary>
    /// <param name="filePath">The file path whose directory should be created</param>
    /// <param name="sourceFilePath">Optional source file path for validation context</param>
    public static void EnsureOutputDirectoryExists(string filePath, string? sourceFilePath = null)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            SafeCreateDirectory(directory, sourceFilePath);
        }
    }
    
    /// <summary>
    /// Validates an output path and ensures its directory exists.
    /// </summary>
    /// <param name="outputPath">The output file path</param>
    /// <param name="sourceFilePath">Optional source file path for validation context</param>
    /// <returns>The validated full path</returns>
    public static string ValidateAndPrepareOutputPath(string outputPath, string? sourceFilePath = null)
    {
        var validatedPath = ValidateOutputPath(outputPath, sourceFilePath);
        EnsureOutputDirectoryExists(validatedPath, sourceFilePath);
        return validatedPath;
    }
}

