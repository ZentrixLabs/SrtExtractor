using System.IO;
using System.Text.Json;
using SrtExtractor.Models;
using SrtExtractor.Services.Interfaces;

namespace SrtExtractor.Services.Implementations;

/// <summary>
/// Service for FFmpeg operations (probe and extract subtitles from MP4s).
/// </summary>
public class FfmpegService : IFfmpegService
{
    private readonly ILoggingService _loggingService;
    private readonly IProcessRunner _processRunner;
    private readonly IToolDetectionService _toolDetectionService;

    public FfmpegService(ILoggingService loggingService, IProcessRunner processRunner, IToolDetectionService toolDetectionService)
    {
        _loggingService = loggingService;
        _processRunner = processRunner;
        _toolDetectionService = toolDetectionService;
    }

    public async Task<ProbeResult> ProbeAsync(string mp4Path)
    {
        _loggingService.LogInfo($"Probing MP4 file: {mp4Path}");

        if (!File.Exists(mp4Path))
        {
            throw new FileNotFoundException($"MP4 file not found: {mp4Path}");
        }

        try
        {
            // Get FFmpeg path and construct ffprobe path
            var ffmpegPath = await FindFfmpegPathAsync();
            if (string.IsNullOrEmpty(ffmpegPath))
            {
                throw new InvalidOperationException("FFmpeg not found");
            }

            // Use ffprobe for probing (ffprobe is usually in the same directory as ffmpeg)
            var ffprobePath = Path.Combine(Path.GetDirectoryName(ffmpegPath) ?? "", "ffprobe.exe");
            if (!File.Exists(ffprobePath))
            {
                throw new InvalidOperationException($"ffprobe.exe not found at {ffprobePath}");
            }

            // Run ffprobe to get stream information
            var (exitCode, stdout, stderr) = await _processRunner.RunAsync(ffprobePath, $"-v quiet -print_format json -show_streams \"{mp4Path}\"");

            if (exitCode != 0)
            {
                throw new InvalidOperationException($"ffprobe failed with exit code {exitCode}: {stderr}");
            }

            // Parse JSON output
            var jsonDoc = JsonDocument.Parse(stdout);
            var streams = jsonDoc.RootElement.GetProperty("streams");

            var tracks = new List<SubtitleTrack>();
            int trackId = 0;

            foreach (var stream in streams.EnumerateArray())
            {
                if (stream.TryGetProperty("codec_type", out var codecType) && 
                    codecType.GetString() == "subtitle")
                {
                    var codec = stream.GetProperty("codec_name").GetString() ?? "unknown";
                    var language = stream.TryGetProperty("tags", out var tags) && 
                                  tags.TryGetProperty("language", out var lang) ? 
                                  lang.GetString() ?? "und" : "und";
                    
                    // Check for forced flag
                    var forced = false;
                    if (stream.TryGetProperty("disposition", out var disposition))
                    {
                        forced = disposition.TryGetProperty("forced", out var forcedProp) && 
                                forcedProp.GetInt32() == 1;
                    }

                    // Check for closed captions
                    var isClosedCaption = false;
                    if (stream.TryGetProperty("tags", out var streamTags))
                    {
                        if (streamTags.TryGetProperty("title", out var title))
                        {
                            var titleStr = title.GetString() ?? "";
                            var titleLower = titleStr.ToLowerInvariant();
                            isClosedCaption = titleLower.Contains("cc") || 
                                            titleLower.Contains("closed caption") ||
                                            titleLower.Contains("caption");
                        }
                    }

                    // Map FFmpeg codec names to our internal format
                    var mappedCodec = MapFfmpegCodec(codec);

                    tracks.Add(new SubtitleTrack(trackId, mappedCodec, language, forced, isClosedCaption, null));
                    trackId++;
                }
            }

            _loggingService.LogInfo($"Found {tracks.Count} subtitle tracks in MP4 file");
            return new ProbeResult(tracks);
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Failed to probe MP4 file: {mp4Path}", ex);
            throw;
        }
    }

    public async Task<string> ExtractSubtitleAsync(string mp4Path, int trackId, string outputPath)
    {
        _loggingService.LogInfo($"Extracting subtitle track {trackId} from MP4 to: {outputPath}");

        try
        {
            // Get FFmpeg path
            var ffmpegPath = await FindFfmpegPathAsync();
            if (string.IsNullOrEmpty(ffmpegPath))
            {
                throw new InvalidOperationException("FFmpeg not found");
            }

            // Ensure output directory exists
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Run ffmpeg to extract subtitle
            var args = $"-i \"{mp4Path}\" -map 0:s:{trackId} -c:s srt \"{outputPath}\"";
            var (exitCode, stdout, stderr) = await _processRunner.RunAsync(ffmpegPath, args);

            if (exitCode != 0)
            {
                throw new InvalidOperationException($"ffmpeg failed with exit code {exitCode}: {stderr}");
            }

            if (!File.Exists(outputPath))
            {
                throw new InvalidOperationException($"Output file was not created: {outputPath}");
            }

            _loggingService.LogInfo($"Subtitle extracted successfully: {outputPath}");
            return outputPath;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Failed to extract subtitle from MP4: {mp4Path}", ex);
            throw;
        }
    }

    private async Task<string?> FindFfmpegPathAsync()
    {
        // Common FFmpeg installation paths
        var commonPaths = new[]
        {
            @"C:\ffmpeg\bin\ffmpeg.exe",
            @"C:\Program Files\ffmpeg\bin\ffmpeg.exe",
            @"C:\Program Files (x86)\ffmpeg\bin\ffmpeg.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "ffmpeg", "bin", "ffmpeg.exe"),
            "ffmpeg.exe" // Try PATH
        };

        foreach (var path in commonPaths)
        {
            if (await _toolDetectionService.ValidateToolAsync(path))
            {
                return path;
            }
        }

        return null;
    }

    private static string MapFfmpegCodec(string ffmpegCodec)
    {
        return ffmpegCodec.ToLowerInvariant() switch
        {
            "mov_text" or "timed_text" or "3gpp" => "S_TEXT/3GPP",
            "srt" => "S_TEXT/UTF8",
            "ass" => "S_TEXT/ASS",
            "ssa" => "S_TEXT/SSA",
            "vtt" => "S_TEXT/VTT",
            _ => $"S_TEXT/{ffmpegCodec.ToUpperInvariant()}"
        };
    }
}
