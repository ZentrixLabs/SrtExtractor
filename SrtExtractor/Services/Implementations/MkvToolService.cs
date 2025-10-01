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
    private readonly ISettingsService _settingsService;

    public MkvToolService(ILoggingService loggingService, IProcessRunner processRunner, ISettingsService settingsService)
    {
        _loggingService = loggingService;
        _processRunner = processRunner;
        _settingsService = settingsService;
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
            var settings = await _settingsService.LoadSettingsAsync();
            if (string.IsNullOrEmpty(settings.MkvMergePath))
            {
                throw new InvalidOperationException("MKVToolNix path not configured");
            }

            // Run mkvmerge -J to get JSON output
            var (exitCode, stdout, stderr) = await _processRunner.RunAsync(settings.MkvMergePath, $"-J \"{mkvPath}\"");

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

    public async Task<string> ExtractTextAsync(string mkvPath, int trackId, string outSrt)
    {
        _loggingService.LogInfo($"Extracting text subtitle track {trackId} to: {outSrt}");

        try
        {
            var settings = await _settingsService.LoadSettingsAsync();
            if (string.IsNullOrEmpty(settings.MkvExtractPath))
            {
                throw new InvalidOperationException("MKVToolNix extract path not configured");
            }

            // Ensure output directory exists
            var outputDir = Path.GetDirectoryName(outSrt);
            if (!string.IsNullOrEmpty(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Run mkvextract tracks
            var args = $"tracks \"{mkvPath}\" {trackId}:\"{outSrt}\"";
            var (exitCode, stdout, stderr) = await _processRunner.RunAsync(settings.MkvExtractPath, args);

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

    public async Task<string> ExtractPgsAsync(string mkvPath, int trackId, string outSup)
    {
        _loggingService.LogInfo($"Extracting PGS subtitle track {trackId} to: {outSup}");

        try
        {
            var settings = await _settingsService.LoadSettingsAsync();
            if (string.IsNullOrEmpty(settings.MkvExtractPath))
            {
                throw new InvalidOperationException("MKVToolNix extract path not configured");
            }

            // Ensure output directory exists
            var outputDir = Path.GetDirectoryName(outSup);
            if (!string.IsNullOrEmpty(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Run mkvextract tracks
            var args = $"tracks \"{mkvPath}\" {trackId}:\"{outSup}\"";
            var (exitCode, stdout, stderr) = await _processRunner.RunAsync(settings.MkvExtractPath, args);

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

                        tracks.Add(new SubtitleTrack(id, codec, language, forced, name));
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
}
