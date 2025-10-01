namespace SrtExtractor.Models;

/// <summary>
/// Represents a subtitle track found in an MKV file.
/// </summary>
/// <param name="Id">The track ID within the MKV file</param>
/// <param name="Codec">The codec type (e.g., S_TEXT/UTF8, S_HDMV/PGS)</param>
/// <param name="Language">The language code (e.g., eng, spa, fra)</param>
/// <param name="Forced">Whether this is a forced subtitle track</param>
/// <param name="Name">Optional track name or description</param>
public record SubtitleTrack(
    int Id,
    string Codec,
    string Language,
    bool Forced,
    string? Name
);
