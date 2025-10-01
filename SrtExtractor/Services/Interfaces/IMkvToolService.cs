using SrtExtractor.Models;

namespace SrtExtractor.Services.Interfaces;

/// <summary>
/// Service for MKVToolNix operations (probe and extract).
/// </summary>
public interface IMkvToolService
{
    /// <summary>
    /// Probe an MKV file to discover subtitle tracks.
    /// </summary>
    /// <param name="mkvPath">Path to the MKV file</param>
    /// <returns>Result containing discovered subtitle tracks</returns>
    Task<ProbeResult> ProbeAsync(string mkvPath);

    /// <summary>
    /// Extract a text subtitle track to SRT format.
    /// </summary>
    /// <param name="mkvPath">Path to the MKV file</param>
    /// <param name="trackId">ID of the subtitle track to extract</param>
    /// <param name="outSrt">Output path for the SRT file</param>
    /// <returns>Path to the extracted SRT file</returns>
    Task<string> ExtractTextAsync(string mkvPath, int trackId, string outSrt);

    /// <summary>
    /// Extract a PGS subtitle track to SUP format.
    /// </summary>
    /// <param name="mkvPath">Path to the MKV file</param>
    /// <param name="trackId">ID of the subtitle track to extract</param>
    /// <param name="outSup">Output path for the SUP file</param>
    /// <returns>Path to the extracted SUP file</returns>
    Task<string> ExtractPgsAsync(string mkvPath, int trackId, string outSup);
}
