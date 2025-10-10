using System.IO;

namespace SrtExtractor.Utils;

/// <summary>
/// Shared utility methods for file operations.
/// Consolidates duplicate logic across the codebase.
/// </summary>
public static class FileUtilities
{
    private const long KB = 1024;
    private const long MB = KB * 1024;
    private const long GB = MB * 1024;
    private const long TB = GB * 1024;
    
    /// <summary>
    /// Formats a file size in bytes to a human-readable string.
    /// </summary>
    /// <param name="bytes">File size in bytes</param>
    /// <returns>Formatted string like "1.5 GB", "500.0 MB", etc.</returns>
    public static string FormatFileSize(long bytes)
    {
        if (bytes == 0) 
            return "0 B";
            
        if (bytes >= TB) 
            return $"{bytes / (double)TB:F1} TB";
            
        if (bytes >= GB) 
            return $"{bytes / (double)GB:F1} GB";
            
        if (bytes >= MB) 
            return $"{bytes / (double)MB:F1} MB";
            
        if (bytes >= KB) 
            return $"{bytes / (double)KB:F1} KB";
            
        return $"{bytes} bytes";
    }
    
    /// <summary>
    /// Checks if a file is an MP4 video file.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <returns>True if the file has .mp4 extension</returns>
    public static bool IsMp4File(string filePath) => 
        Path.GetExtension(filePath).Equals(".mp4", StringComparison.OrdinalIgnoreCase);
    
    /// <summary>
    /// Checks if a file is an MKV video file.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <returns>True if the file has .mkv extension</returns>
    public static bool IsMkvFile(string filePath) => 
        Path.GetExtension(filePath).Equals(".mkv", StringComparison.OrdinalIgnoreCase);
    
    /// <summary>
    /// Checks if a file is a supported video file (MKV or MP4).
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <returns>True if the file has .mkv or .mp4 extension</returns>
    public static bool IsVideoFile(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return ext.Equals(".mp4", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".mkv", StringComparison.OrdinalIgnoreCase);
    }
}

