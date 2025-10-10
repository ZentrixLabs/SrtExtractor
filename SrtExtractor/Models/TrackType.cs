namespace SrtExtractor.Models;

/// <summary>
/// Represents the type/category of a subtitle track based on its content.
/// Replaces string-based track type comparisons with type-safe enum.
/// </summary>
public enum TrackType
{
    /// <summary>
    /// Full subtitle track with all dialogue
    /// </summary>
    Full,
    
    /// <summary>
    /// Forced subtitle track (typically only foreign language parts)
    /// </summary>
    Forced,
    
    /// <summary>
    /// Closed caption track for hearing impaired
    /// </summary>
    ClosedCaption,
    
    /// <summary>
    /// Forced closed caption track
    /// </summary>
    ClosedCaptionForced
}

