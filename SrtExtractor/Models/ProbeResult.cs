namespace SrtExtractor.Models;

/// <summary>
/// Contains the result of probing an MKV file for subtitle tracks.
/// </summary>
/// <param name="Tracks">List of discovered subtitle tracks</param>
public record ProbeResult(IReadOnlyList<SubtitleTrack> Tracks);
