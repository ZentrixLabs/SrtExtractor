namespace SrtExtractor.Models;

/// <summary>
/// Represents the type of subtitle codec.
/// Used to avoid repeated string parsing and provide type-safe codec categorization.
/// </summary>
public enum SubtitleCodecType
{
    /// <summary>
    /// Unknown or unrecognized codec format
    /// </summary>
    Unknown,
    
    /// <summary>
    /// SubRip/SRT text-based subtitle format (S_TEXT/UTF8, subrip)
    /// </summary>
    TextBasedSrt,
    
    /// <summary>
    /// ASS/SSA text-based subtitle format
    /// </summary>
    TextBasedAss,
    
    /// <summary>
    /// WebVTT text-based subtitle format
    /// </summary>
    TextBasedWebVtt,
    
    /// <summary>
    /// Generic text-based subtitle format (S_TEXT)
    /// </summary>
    TextBasedGeneric,
    
    /// <summary>
    /// PGS image-based subtitle format (S_HDMV/PGS) - requires OCR
    /// </summary>
    ImageBasedPgs,
    
    /// <summary>
    /// VobSub image-based subtitle format - requires OCR
    /// </summary>
    ImageBasedVobSub,
    
    /// <summary>
    /// DVB image-based subtitle format - requires OCR
    /// </summary>
    ImageBasedDvb
}

