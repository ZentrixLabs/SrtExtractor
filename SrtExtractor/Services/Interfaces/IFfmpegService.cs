using SrtExtractor.Models;

namespace SrtExtractor.Services.Interfaces;

/// <summary>
/// Service for FFmpeg operations (probe and extract subtitles from MP4s).
/// </summary>
public interface IFfmpegService
{
    /// <summary>
    /// Probe an MP4 file to find subtitle tracks.
    /// </summary>
    /// <param name="mp4Path">Path to the MP4 file</param>
    /// <returns>Probe result with subtitle tracks</returns>
    Task<ProbeResult> ProbeAsync(string mp4Path);

    /// <summary>
    /// Extract a subtitle track from an MP4 file.
    /// </summary>
    /// <param name="mp4Path">Path to the MP4 file</param>
    /// <param name="trackId">Track ID to extract</param>
    /// <param name="outputPath">Output SRT file path</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Path to the extracted SRT file</returns>
    Task<string> ExtractSubtitleAsync(string mp4Path, int trackId, string outputPath, CancellationToken cancellationToken = default);
}
