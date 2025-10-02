using System.IO;
using System.Text.Json;
using SrtExtractor.Models;
using SrtExtractor.Services.Interfaces;

namespace SrtExtractor.Services.Implementations;

/// <summary>
/// Service for MKVToolNix operations (probe and extract).
/// </summary>
public class MkvToolService : IMkvToolService
{
    private readonly ILoggingService _loggingService;
    private readonly IProcessRunner _processRunner;
    private readonly IToolDetectionService _toolDetectionService;

    public MkvToolService(ILoggingService loggingService, IProcessRunner processRunner, IToolDetectionService toolDetectionService)
    {
        _loggingService = loggingService;
        _processRunner = processRunner;
        _toolDetectionService = toolDetectionService;
    }

    public async Task<ProbeResult> ProbeAsync(string mkvPath)
    {
        _loggingService.LogInfo($"Probing MKV file: {mkvPath}");

        if (!File.Exists(mkvPath))
        {
            throw new FileNotFoundException($"MKV file not found: {mkvPath}");
        }

        try
        {
            // Get the tool path directly from tool detection
            var toolStatus = await _toolDetectionService.CheckMkvToolNixAsync();
            if (!toolStatus.IsInstalled || string.IsNullOrEmpty(toolStatus.Path))
            {
                throw new InvalidOperationException("MKVToolNix not found");
            }

            // Run mkvmerge -J to get JSON output
            var (exitCode, stdout, stderr) = await _processRunner.RunAsync(toolStatus.Path, $"-J \"{mkvPath}\"");

            if (exitCode != 0)
            {
                throw new InvalidOperationException($"mkvmerge failed with exit code {exitCode}: {stderr}");
            }

            // Parse JSON output
            var tracks = ParseSubtitleTracks(stdout);
            _loggingService.LogInfo($"Found {tracks.Count} subtitle tracks");

            return new ProbeResult(tracks);
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Failed to probe MKV file: {mkvPath}", ex);
            throw;
        }
    }

    public async Task<string> ExtractTextAsync(string mkvPath, int trackId, string outSrt, CancellationToken cancellationToken = default)
    {
        _loggingService.LogInfo($"Extracting text subtitle track {trackId} to: {outSrt}");

        try
        {
            // Get the tool path directly from tool detection
            var toolStatus = await _toolDetectionService.CheckMkvToolNixAsync();
            if (!toolStatus.IsInstalled || string.IsNullOrEmpty(toolStatus.Path))
            {
                throw new InvalidOperationException("MKVToolNix not found");
            }

            // Get mkvextract path (same directory as mkvmerge)
            var mkvDir = Path.GetDirectoryName(toolStatus.Path);
            var mkvextractPath = Path.Combine(mkvDir ?? "", "mkvextract.exe");
            if (!File.Exists(mkvextractPath))
            {
                throw new InvalidOperationException($"mkvextract.exe not found at: {mkvextractPath}");
            }

            // Ensure output directory exists
            var outputDir = Path.GetDirectoryName(outSrt);
            if (!string.IsNullOrEmpty(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Run mkvextract tracks
            var args = $"tracks \"{mkvPath}\" {trackId}:\"{outSrt}\"";
            var (exitCode, stdout, stderr) = await _processRunner.RunAsync(mkvextractPath, args, cancellationToken);

            if (exitCode != 0)
            {
                throw new InvalidOperationException($"mkvextract failed with exit code {exitCode}: {stderr}");
            }

            if (!File.Exists(outSrt))
            {
                throw new InvalidOperationException($"Output file was not created: {outSrt}");
            }

            _loggingService.LogExtraction($"Text subtitle extraction (track {trackId})", true);
            return outSrt;
        }
        catch (Exception ex)
        {
            _loggingService.LogExtraction($"Text subtitle extraction (track {trackId})", false, ex.Message);
            throw;
        }
    }

    public async Task<string> ExtractPgsAsync(string mkvPath, int trackId, string outSup, CancellationToken cancellationToken = default)
    {
        _loggingService.LogInfo($"Extracting PGS subtitle track {trackId} to: {outSup}");

        try
        {
            // Calculate timeout based on file size
            var timeout = CalculateTimeoutForFile(mkvPath);
            _loggingService.LogInfo($"Using timeout of {timeout.TotalMinutes:F0} minutes for file size {GetFileSizeGB(mkvPath):F1} GB");
            // Get the tool path directly from tool detection
            var toolStatus = await _toolDetectionService.CheckMkvToolNixAsync();
            if (!toolStatus.IsInstalled || string.IsNullOrEmpty(toolStatus.Path))
            {
                throw new InvalidOperationException("MKVToolNix not found");
            }

            // Get mkvextract path (same directory as mkvmerge)
            var mkvDir = Path.GetDirectoryName(toolStatus.Path);
            var mkvextractPath = Path.Combine(mkvDir ?? "", "mkvextract.exe");
            if (!File.Exists(mkvextractPath))
            {
                throw new InvalidOperationException($"mkvextract.exe not found at: {mkvextractPath}");
            }

            // Ensure output directory exists
            var outputDir = Path.GetDirectoryName(outSup);
            if (!string.IsNullOrEmpty(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Run mkvextract tracks
            var args = $"tracks \"{mkvPath}\" {trackId}:\"{outSup}\"";
            var (exitCode, stdout, stderr) = await _processRunner.RunAsync(mkvextractPath, args, timeout, cancellationToken);

            if (exitCode != 0)
            {
                throw new InvalidOperationException($"mkvextract failed with exit code {exitCode}: {stderr}");
            }

            if (!File.Exists(outSup))
            {
                throw new InvalidOperationException($"Output file was not created: {outSup}");
            }

            _loggingService.LogExtraction($"PGS subtitle extraction (track {trackId})", true);
            return outSup;
        }
        catch (Exception ex)
        {
            _loggingService.LogExtraction($"PGS subtitle extraction (track {trackId})", false, ex.Message);
            throw;
        }
    }

    private List<SubtitleTrack> ParseSubtitleTracks(string jsonOutput)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonOutput);
            var tracks = new List<SubtitleTrack>();

            if (doc.RootElement.TryGetProperty("tracks", out var tracksElement))
            {
                foreach (var trackElement in tracksElement.EnumerateArray())
                {
                    if (trackElement.TryGetProperty("type", out var typeElement) &&
                        string.Equals(typeElement.GetString(), "subtitles", StringComparison.OrdinalIgnoreCase))
                    {
                        var id = trackElement.GetProperty("id").GetInt32();
                        var codec = trackElement.TryGetProperty("codec", out var codecElement) 
                            ? codecElement.GetString() ?? "" 
                            : "";

                        // Extract language
                        var language = "";
                        if (trackElement.TryGetProperty("properties", out var propertiesElement))
                        {
                            if (propertiesElement.TryGetProperty("language", out var langElement))
                            {
                                language = langElement.GetString() ?? "";
                            }
                        }

                        // Extract forced flag
                        var forced = false;
                        if (trackElement.TryGetProperty("properties", out var propsElement))
                        {
                            if (propsElement.TryGetProperty("forced_track", out var forcedElement))
                            {
                                forced = forcedElement.GetBoolean();
                            }
                        }

                        // Extract track name
                        var name = (string?)null;
                        if (trackElement.TryGetProperty("properties", out var namePropsElement))
                        {
                            if (namePropsElement.TryGetProperty("track_name", out var nameElement))
                            {
                                name = nameElement.GetString();
                            }
                        }

                        // Detect closed captions based on track name or codec
                        var isClosedCaption = false;
                        if (!string.IsNullOrEmpty(name))
                        {
                            var nameLower = name.ToLowerInvariant();
                            isClosedCaption = nameLower.Contains("cc") || 
                                            nameLower.Contains("closed caption") ||
                                            nameLower.Contains("caption");
                        }

                        tracks.Add(new SubtitleTrack(id, codec, language, forced, isClosedCaption, name));
                    }
                }
            }

            return tracks;
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to parse mkvmerge JSON output", ex);
            throw new InvalidOperationException("Failed to parse MKV track information", ex);
        }
    }

    /// <summary>
    /// Calculate appropriate timeout based on file size.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <returns>Calculated timeout duration</returns>
    private static TimeSpan CalculateTimeoutForFile(string filePath)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            var sizeGB = fileInfo.Length / (1024.0 * 1024.0 * 1024.0);
            
            // Base timeout: 5 minutes for small files
            var baseMinutes = 5.0;
            
            // Add time based on file size:
            // - 1 minute per GB for files under 10GB
            // - 2 minutes per GB for files 10-50GB  
            // - 3 minutes per GB for files over 50GB
            var additionalMinutes = sizeGB switch
            {
                < 10 => sizeGB * 1.0,
                < 50 => sizeGB * 2.0,
                _ => sizeGB * 3.0
            };
            
            var totalMinutes = baseMinutes + additionalMinutes;
            
            // Cap at 4 hours maximum
            var maxMinutes = 4 * 60;
            totalMinutes = Math.Min(totalMinutes, maxMinutes);
            
            return TimeSpan.FromMinutes(totalMinutes);
        }
        catch
        {
            // If we can't determine file size, use a conservative 2-hour timeout
            return TimeSpan.FromHours(2);
        }
    }

    /// <summary>
    /// Get file size in GB for logging purposes.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <returns>File size in GB</returns>
    private static double GetFileSizeGB(string filePath)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            return fileInfo.Length / (1024.0 * 1024.0 * 1024.0);
        }
        catch
        {
            return 0;
        }
    }
}
