using System.IO;

namespace SrtExtractor.Models;

/// <summary>
/// Represents information about an SRT file for batch processing.
/// </summary>
public class SrtFileInfo
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Folder { get; set; } = string.Empty;
    public long Size { get; set; }
    public string FormattedSize { get; set; } = string.Empty;
    public string Status { get; set; } = "Ready";
    public int CorrectionsApplied { get; set; }
    public bool IsProcessed { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;

    public SrtFileInfo(string filePath)
    {
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);
        Folder = Path.GetDirectoryName(filePath) ?? string.Empty;
        
        var fileInfo = new FileInfo(filePath);
        Size = fileInfo.Length;
        FormattedSize = FormatFileSize(Size);
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}
