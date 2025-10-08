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
    private readonly IAsyncFileService _asyncFileService;
    private readonly IFileLockDetectionService _fileLockDetectionService;

    public FfmpegService(
        ILoggingService loggingService, 
        IProcessRunner processRunner, 
        IToolDetectionService toolDetectionService,
        IAsyncFileService asyncFileService,
        IFileLockDetectionService fileLockDetectionService)
    {
        _loggingService = loggingService;
        _processRunner = processRunner;
        _toolDetectionService = toolDetectionService;
        _asyncFileService = asyncFileService;
        _fileLockDetectionService = fileLockDetectionService;
    }

    public async Task<ProbeResult> ProbeAsync(string mp4Path, CancellationToken cancellationToken = default)
    {
        _loggingService.LogInfo($"Probing MP4 file: {mp4Path}");

        // Check if file exists and is accessible
        var fileExists = await _asyncFileService.FileExistsAsync(mp4Path, cancellationToken);
        if (!fileExists)
        {
            throw new FileNotFoundException($"MP4 file not found: {mp4Path}");
        }

        // Check if file is locked
        var isLocked = await _fileLockDetectionService.IsFileLockedAsync(mp4Path, cancellationToken);
        if (isLocked)
        {
            throw new IOException($"MP4 file is locked or in use by another process: {mp4Path}");
        }

        try
        {
            // Get FFmpeg path and construct ffprobe path
            var ffmpegPath = await FindFfmpegPathAsync(cancellationToken);
            if (string.IsNullOrEmpty(ffmpegPath))
            {
                throw new InvalidOperationException("FFmpeg not found");
            }

            // Use ffprobe for probing (ffprobe is usually in the same directory as ffmpeg)
            var ffprobePath = Path.Combine(Path.GetDirectoryName(ffmpegPath) ?? "", "ffprobe.exe");
            var ffprobeExists = await _asyncFileService.FileExistsAsync(ffprobePath, cancellationToken);
            if (!ffprobeExists)
            {
                throw new InvalidOperationException($"ffprobe.exe not found at {ffprobePath}");
            }

            // Run ffprobe to get stream information
            var (exitCode, stdout, stderr) = await _processRunner.RunAsync(ffprobePath, $"-v quiet -print_format json -show_streams \"{mp4Path}\"", cancellationToken);

            if (exitCode != 0)
            {
                throw new InvalidOperationException($"ffprobe failed with exit code {exitCode}: {stderr}");
            }

            // Parse JSON output
            using var jsonDoc = JsonDocument.Parse(stdout);
            var streams = jsonDoc.RootElement.GetProperty("streams");

            var tracks = new List<SubtitleTrack>();
            int trackId = 0;
            int streamIndexCounter = 0;

            foreach (var stream in streams.EnumerateArray())
            {
                // Check for subtitle streams or data streams that are actually subtitles
                var isSubtitleStream = false;
                if (stream.TryGetProperty("codec_type", out var codecType))
                {
                    var codecTypeStr = codecType.GetString();
                    if (codecTypeStr == "subtitle")
                    {
                        isSubtitleStream = true;
                    }
                    else if (codecTypeStr == "data")
                    {
                        // Check if this data stream is actually a subtitle track
                        var codecName = stream.GetProperty("codec_name").GetString() ?? "";
                        var handlerName = "";
                        if (stream.TryGetProperty("tags", out var tags) && 
                            tags.TryGetProperty("handler_name", out var handler))
                        {
                            handlerName = handler.GetString() ?? "";
                        }
                        
                        // Check for subtitle-related indicators, but exclude bin_data as it's not supported
                        isSubtitleStream = !codecName.Contains("bin_data") && 
                                         (handlerName.ToLowerInvariant().Contains("subtitle") ||
                                          handlerName.ToLowerInvariant().Contains("text"));
                    }
                }
                
                if (isSubtitleStream)
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

                    // Extract additional properties for MP4 tracks
                    long? bitrate = null;
                    int? frameCount = null;
                    double? duration = null;

                    if (stream.TryGetProperty("bit_rate", out var bitRateElement))
                    {
                        if (long.TryParse(bitRateElement.GetString(), out var bps))
                        {
                            bitrate = bps;
                        }
                    }

                    if (stream.TryGetProperty("nb_frames", out var framesElement))
                    {
                        if (int.TryParse(framesElement.GetString(), out var frames))
                        {
                            frameCount = frames;
                        }
                    }

                    if (stream.TryGetProperty("duration", out var durationElement))
                    {
                        if (double.TryParse(durationElement.GetString(), out var dur))
                        {
                            duration = dur;
                        }
                    }

                    // Detect track type
                    var trackType = DetectTrackType(bitrate, frameCount, duration, forced, isClosedCaption);

                    // Get the actual stream index for this track
                    var streamIndex = streamIndexCounter;

                    tracks.Add(new SubtitleTrack(trackId, mappedCodec, language, false, forced, 
                        "", bitrate ?? 0, frameCount ?? 0, 0, "", false, 
                        forced, isClosedCaption, false, trackType, frameCount ?? 0));
                    trackId++;
                }
                
                // Always increment stream index counter for all streams
                streamIndexCounter++;
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

    public async Task<string> ExtractSubtitleAsync(string mp4Path, int trackId, string outputPath, CancellationToken cancellationToken = default)
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
                await _asyncFileService.EnsureDirectoryExistsAsync(outputDir, cancellationToken);
            }

            // Get the stream index from the track ID
            // We need to re-probe to get the track information since we don't have access to the original probe result
            var actualStreamIndex = await FindStreamIndexForTrackAsync(mp4Path, trackId, cancellationToken);
            
            // Extract the subtitle using FFmpeg
            _loggingService.LogInfo($"Extracting subtitle track {trackId} from MP4...");
            var ffmpegArgs = $"-i \"{mp4Path}\" -map 0:{actualStreamIndex} -c:s srt \"{outputPath}\"";
            var (exitCode, stdout, stderr) = await _processRunner.RunAsync(ffmpegPath, ffmpegArgs, cancellationToken);
            
            if (exitCode != 0)
            {
                throw new InvalidOperationException($"ffmpeg failed with exit code {exitCode}: {stderr}");
            }

            var outputExists = await _asyncFileService.FileExistsAsync(outputPath, cancellationToken);
            if (!outputExists)
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

    /// <summary>
    /// Find the actual stream index for a given track ID by probing the file again.
    /// </summary>
    private async Task<int> FindStreamIndexForTrackAsync(string mp4Path, int trackId, CancellationToken cancellationToken = default)
    {
        _loggingService.LogInfo($"Finding stream index for track ID {trackId}");
        
        // Get FFmpeg path and construct ffprobe path
        var ffmpegPath = await FindFfmpegPathAsync(cancellationToken);
        if (string.IsNullOrEmpty(ffmpegPath))
        {
            throw new InvalidOperationException("FFmpeg not found");
        }

        var ffprobePath = Path.Combine(Path.GetDirectoryName(ffmpegPath) ?? "", "ffprobe.exe");
        
        // Run ffprobe to get stream information with a timeout
        var (exitCode, stdout, stderr) = await _processRunner.RunAsync(ffprobePath, $"-v quiet -print_format json -show_streams \"{mp4Path}\"", cancellationToken);

        if (exitCode != 0)
        {
            throw new InvalidOperationException($"ffprobe failed with exit code {exitCode}: {stderr}");
        }

            // Parse JSON output
            _loggingService.LogInfo("Parsing ffprobe JSON output...");
            using var jsonDoc = JsonDocument.Parse(stdout);
            var streams = jsonDoc.RootElement.GetProperty("streams");

        int currentTrackId = 0;
        int streamIndex = 0;

        _loggingService.LogInfo($"Processing {streams.GetArrayLength()} streams...");
        foreach (var stream in streams.EnumerateArray())
        {
            // Check for subtitle streams or data streams that are actually subtitles
            var isSubtitleStream = false;
            if (stream.TryGetProperty("codec_type", out var codecType))
            {
                var codecTypeStr = codecType.GetString();
                if (codecTypeStr == "subtitle")
                {
                    isSubtitleStream = true;
                }
                else if (codecTypeStr == "data")
                {
                    // Check if this data stream is actually a subtitle track
                    var codecName = stream.GetProperty("codec_name").GetString() ?? "";
                    var handlerName = "";
                    if (stream.TryGetProperty("tags", out var tags) && 
                        tags.TryGetProperty("handler_name", out var handler))
                    {
                        handlerName = handler.GetString() ?? "";
                    }
                    
                    // Check for subtitle-related indicators
                    isSubtitleStream = codecName.Contains("bin_data") || 
                                     handlerName.ToLowerInvariant().Contains("subtitle") ||
                                     handlerName.ToLowerInvariant().Contains("text");
                }
            }
            
            if (isSubtitleStream)
            {
                _loggingService.LogInfo($"Found subtitle stream at index {streamIndex}, track ID {currentTrackId}");
                if (currentTrackId == trackId)
                {
                    _loggingService.LogInfo($"Found matching track ID {trackId} at stream index {streamIndex}");
                    return streamIndex;
                }
                currentTrackId++;
            }
            streamIndex++;
        }

        throw new InvalidOperationException($"Could not find stream index for track ID {trackId}");
    }

    private async Task<string?> FindMkvmergePathAsync(CancellationToken cancellationToken = default)
    {
        // Get MKVToolNix path from tool detection service
        var mkvStatus = await _toolDetectionService.CheckMkvToolNixAsync();
        if (mkvStatus.IsInstalled && !string.IsNullOrEmpty(mkvStatus.Path))
        {
            var mkvDir = Path.GetDirectoryName(mkvStatus.Path);
            var mkvmergePath = Path.Combine(mkvDir ?? "", "mkvmerge.exe");
            if (await _asyncFileService.FileExistsAsync(mkvmergePath, cancellationToken))
            {
                return mkvmergePath;
            }
        }
        return null;
    }

    private async Task<string?> FindMkvextractPathAsync(CancellationToken cancellationToken = default)
    {
        // Get MKVToolNix path from tool detection service
        var mkvStatus = await _toolDetectionService.CheckMkvToolNixAsync();
        if (mkvStatus.IsInstalled && !string.IsNullOrEmpty(mkvStatus.Path))
        {
            var mkvDir = Path.GetDirectoryName(mkvStatus.Path);
            var mkvextractPath = Path.Combine(mkvDir ?? "", "mkvextract.exe");
            if (await _asyncFileService.FileExistsAsync(mkvextractPath, cancellationToken))
            {
                return mkvextractPath;
            }
        }
        return null;
    }



    private async Task<string?> FindFfmpegPathAsync(CancellationToken cancellationToken = default)
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
            cancellationToken.ThrowIfCancellationRequested();
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

    /// <summary>
    /// Detect track type based on characteristics like bitrate, frame count, and duration.
    /// </summary>
    /// <param name="bitrate">Track bitrate in bits per second</param>
    /// <param name="frameCount">Number of subtitle frames</param>
    /// <param name="duration">Track duration in seconds</param>
    /// <param name="forced">Whether track is marked as forced</param>
    /// <param name="isClosedCaption">Whether track is closed caption</param>
    /// <returns>Detected track type</returns>
    private static string DetectTrackType(long? bitrate, int? frameCount, double? duration, bool forced, bool isClosedCaption)
    {
        // Handle closed captions first
        if (isClosedCaption)
        {
            return forced ? "CC Forced" : "CC";
        }

        // Handle explicitly forced tracks
        if (forced)
        {
            return "Forced";
        }

        // Analyze characteristics for PGS tracks (which don't always have forced_track: true)
        if (bitrate.HasValue && frameCount.HasValue)
        {
            // Very low bitrate and frame count = likely forced/partial
            if (bitrate < 1000 && frameCount < 50)
            {
                return "Forced";
            }

            // Low bitrate and few frames = likely forced
            if (bitrate < 10000 && frameCount < 200)
            {
                return "Forced";
            }

            // High bitrate and many frames = likely full subtitles
            if (bitrate > 20000 && frameCount > 1000)
            {
                return "Full";
            }

            // Medium characteristics = likely full subtitles
            if (bitrate > 10000 && frameCount > 500)
            {
                return "Full";
            }
        }

        // Default to Full for text-based subtitles (S_TEXT/UTF8)
        return "Full";
    }
}
