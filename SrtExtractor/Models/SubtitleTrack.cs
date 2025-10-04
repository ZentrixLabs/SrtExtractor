namespace SrtExtractor.Models;

/// <summary>
/// Represents a subtitle track found in an MKV file.
/// </summary>
/// <param name="Id">The track ID within the MKV file</param>
/// <param name="Codec">The codec type (e.g., S_TEXT/UTF8, S_HDMV/PGS)</param>
/// <param name="Language">The language code (e.g., eng, spa, fra)</param>
/// <param name="Forced">Whether this is a forced subtitle track</param>
/// <param name="IsClosedCaption">Whether this is a closed caption track</param>
/// <param name="Name">Optional track name or description</param>
/// <param name="Bitrate">Track bitrate in bits per second</param>
/// <param name="FrameCount">Number of subtitle frames</param>
/// <param name="Duration">Track duration in seconds</param>
/// <param name="TrackType">Detected track type (Full, Forced, Commentary, SDH)</param>
/// <param name="IsRecommended">Whether this track is the recommended choice based on user preferences</param>
/// <param name="StreamIndex">The actual stream index in the file (for FFmpeg extraction)</param>
public record SubtitleTrack(
    int Id,
    string Codec,
    string Language,
    bool Forced,
    bool IsClosedCaption,
    string? Name,
    long? Bitrate = null,
    int? FrameCount = null,
    double? Duration = null,
    string? TrackType = null,
    bool IsRecommended = false,
    int? StreamIndex = null
);
