namespace SrtExtractor.Models;

/// <summary>
/// Represents known external tools used by SrtExtractor.
/// Used for type-safe tool identification and configuration.
/// </summary>
public enum KnownTool
{
    /// <summary>
    /// Unknown or unrecognized tool
    /// </summary>
    Unknown,
    
    /// <summary>
    /// mkvmerge.exe from MKVToolNix - Used for probing MKV files
    /// </summary>
    MkvMerge,
    
    /// <summary>
    /// mkvextract.exe from MKVToolNix - Used for extracting subtitles from MKV
    /// </summary>
    MkvExtract,
    
    /// <summary>
    /// ffmpeg.exe - Used for extracting from MP4 files
    /// </summary>
    FFmpeg,
    
    /// <summary>
    /// ffprobe.exe - Used for probing MP4 files
    /// </summary>
    FFprobe,
    
    /// <summary>
    /// tesseract.exe - Used for OCR conversion of image-based subtitles (bundled with app)
    /// </summary>
    Tesseract
}

