using System.IO;
using System.Text.Json;
using SrtExtractor.Models;
using SrtExtractor.Services.Interfaces;
using SrtExtractor.Utils;

namespace SrtExtractor.Services.Implementations;

/// <summary>
/// Service for MKVToolNix operations (probe and extract).
/// </summary>
public class MkvToolService : IMkvToolService
{
    private readonly ILoggingService _loggingService;
    private readonly IProcessRunner _processRunner;
    private readonly IToolDetectionService _toolDetectionService;
    private readonly IAsyncFileService _asyncFileService;
    private readonly IFileLockDetectionService _fileLockDetectionService;

    public MkvToolService(
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

    public async Task<ProbeResult> ProbeAsync(string mkvPath, CancellationToken cancellationToken = default)
    {
        _loggingService.LogInfo($"Probing MKV file: {Path.GetFileName(mkvPath)}");

        // SECURITY: Validate and sanitize the input path
        var validatedMkvPath = PathValidator.ValidateFileExists(mkvPath);

        // Check if file exists and is accessible
        var fileExists = await _asyncFileService.FileExistsAsync(validatedMkvPath, cancellationToken);
        if (!fileExists)
        {
            throw new FileNotFoundException($"MKV file not found: {mkvPath}");
        }

        // Check if file is locked
        var isLocked = await _fileLockDetectionService.IsFileLockedAsync(validatedMkvPath, cancellationToken);
        if (isLocked)
        {
            throw new IOException($"MKV file is locked or in use by another process: {mkvPath}");
        }

        try
        {
            // Get the tool path directly from tool detection
            var toolStatus = await _toolDetectionService.CheckMkvToolNixAsync();
            if (!toolStatus.IsInstalled || string.IsNullOrEmpty(toolStatus.Path))
            {
                throw new InvalidOperationException("MKVToolNix not found");
            }

            // SECURITY: Use argument array to prevent command injection
            // Run mkvmerge -J to get JSON output
            var (exitCode, stdout, stderr) = await _processRunner.RunAsync(
                toolStatus.Path, 
                new[] { "-J", validatedMkvPath },
                cancellationToken);

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
        _loggingService.LogInfo($"Extracting text subtitle track {trackId}");

        try
        {
            // SECURITY: Validate and sanitize paths
            var validatedMkvPath = PathValidator.ValidateFileExists(mkvPath);
            var validatedOutSrt = SafeFileOperations.ValidateAndPrepareOutputPath(outSrt);

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

            // SECURITY: Use argument array to prevent command injection
            // Run mkvextract tracks
            var (exitCode, stdout, stderr) = await _processRunner.RunAsync(
                mkvextractPath, 
                new[] { "tracks", validatedMkvPath, $"{trackId}:{validatedOutSrt}" }, 
                cancellationToken);

            if (exitCode != 0)
            {
                throw new InvalidOperationException($"mkvextract failed with exit code {exitCode}: {stderr}");
            }

            if (!File.Exists(validatedOutSrt))
            {
                throw new InvalidOperationException($"Output file was not created: {outSrt}");
            }

            _loggingService.LogExtraction($"Text subtitle extraction (track {trackId})", true);
            return validatedOutSrt;
        }
        catch (Exception ex)
        {
            _loggingService.LogExtraction($"Text subtitle extraction (track {trackId})", false, ex.Message);
            throw;
        }
    }

    public async Task<string> ExtractPgsAsync(string mkvPath, int trackId, string outSup, CancellationToken cancellationToken = default)
    {
        _loggingService.LogInfo($"Extracting PGS subtitle track {trackId}");

        try
        {
            // SECURITY: Validate and sanitize paths
            var validatedMkvPath = PathValidator.ValidateFileExists(mkvPath);
            var validatedOutSup = SafeFileOperations.ValidateAndPrepareOutputPath(outSup);

            // Calculate timeout based on file size
            var timeout = CalculateTimeoutForFile(validatedMkvPath);
            _loggingService.LogInfo($"Using timeout of {timeout.TotalMinutes:F0} minutes for file size {GetFileSizeGB(validatedMkvPath):F1} GB");
            
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

            // SECURITY: Use argument array to prevent command injection
            // Run mkvextract tracks
            var (exitCode, stdout, stderr) = await _processRunner.RunAsync(
                mkvextractPath, 
                new[] { "tracks", validatedMkvPath, $"{trackId}:{validatedOutSup}" }, 
                timeout, 
                cancellationToken);

            if (exitCode != 0)
            {
                throw new InvalidOperationException($"mkvextract failed with exit code {exitCode}: {stderr}");
            }

            if (!File.Exists(validatedOutSup))
            {
                throw new InvalidOperationException($"Output file was not created: {outSup}");
            }

            _loggingService.LogExtraction($"PGS subtitle extraction (track {trackId})", true);
            return validatedOutSup;
        }
        catch (Exception ex)
        {
            _loggingService.LogExtraction($"PGS subtitle extraction (track {trackId})", false, ex.Message);
            throw;
        }
    }

    public async Task<(string idxFilePath, string subFilePath)> ExtractVobSubAsync(string mkvPath, int trackId, string outputDirectory, CancellationToken cancellationToken = default)
    {
        _loggingService.LogInfo($"Extracting VobSub track {trackId}");

        try
        {
            // SECURITY: Validate and sanitize paths
            var validatedMkvPath = PathValidator.ValidateFileExists(mkvPath);
            var validatedOutputDir = SafeFileOperations.ValidateOutputPath(outputDirectory, validatedMkvPath);
            SafeFileOperations.SafeCreateDirectory(validatedOutputDir, validatedMkvPath);

            // Calculate timeout based on file size
            var timeout = CalculateTimeoutForFile(validatedMkvPath);
            _loggingService.LogInfo($"Using timeout of {timeout.TotalMinutes:F0} minutes for file size {GetFileSizeGB(validatedMkvPath):F1} GB");

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

            // Generate output file names
            var baseFileName = Path.GetFileNameWithoutExtension(validatedMkvPath);
            var idxFilePath = Path.Combine(validatedOutputDir, $"{baseFileName}.{trackId}.idx");
            var subFilePath = Path.Combine(validatedOutputDir, $"{baseFileName}.{trackId}.sub");
            
            // Validate output paths (allow same directory as source)
            idxFilePath = SafeFileOperations.ValidateOutputPath(idxFilePath, validatedMkvPath);
            subFilePath = SafeFileOperations.ValidateOutputPath(subFilePath, validatedMkvPath);

            // SECURITY: Use argument array to prevent command injection
            // Run mkvextract tracks to extract VobSub
            // VobSub tracks are extracted as .idx/.sub file pairs
            var (exitCode, stdout, stderr) = await _processRunner.RunAsync(
                mkvextractPath, 
                new[] { "tracks", validatedMkvPath, $"{trackId}:{idxFilePath}" }, 
                timeout, 
                cancellationToken);

            if (exitCode != 0)
            {
                throw new InvalidOperationException($"mkvextract failed with exit code {exitCode}: {stderr}");
            }

            // Check if both .idx and .sub files were created
            if (!File.Exists(idxFilePath))
            {
                throw new InvalidOperationException($"VobSub .idx file was not created: {idxFilePath}");
            }

            if (!File.Exists(subFilePath))
            {
                throw new InvalidOperationException($"VobSub .sub file was not created: {subFilePath}");
            }

            _loggingService.LogExtraction($"VobSub extraction (track {trackId})", true);
            return (idxFilePath, subFilePath);
        }
        catch (Exception ex)
        {
            _loggingService.LogExtraction($"VobSub extraction (track {trackId})", false, ex.Message);
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

                        // Extract properties including the actual Matroska track number
                        var trackNumber = id; // Default to id if number not found
                        var language = "";
                        if (trackElement.TryGetProperty("properties", out var propertiesElement))
                        {
                            // Get the actual Matroska track number (what Subtitle Edit shows)
                            if (propertiesElement.TryGetProperty("number", out var numberElement))
                            {
                                trackNumber = numberElement.GetInt32();
                            }
                            
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

                        // Extract additional properties for enhanced track analysis
                        long? bitrate = null;
                        int? frameCount = null;
                        double? duration = null;

                        if (trackElement.TryGetProperty("properties", out var enhancedPropsElement))
                        {
                            // Extract bitrate
                            if (enhancedPropsElement.TryGetProperty("tag_bps", out var bitrateElement))
                            {
                                if (long.TryParse(bitrateElement.GetString(), out var bps))
                                {
                                    bitrate = bps;
                                }
                            }

                            // Extract frame count
                            if (enhancedPropsElement.TryGetProperty("tag_number_of_frames", out var framesElement))
                            {
                                if (int.TryParse(framesElement.GetString(), out var frames))
                                {
                                    frameCount = frames;
                                }
                            }

                            // Extract duration
                            if (enhancedPropsElement.TryGetProperty("tag_duration", out var durationElement))
                            {
                                var durationStr = durationElement.GetString();
                                if (!string.IsNullOrEmpty(durationStr))
                                {
                                    // Parse duration in format "HH:MM:SS.sssssssss"
                                    // The format might have too many decimal places, so we'll parse manually
                                    if (TryParseDuration(durationStr, out var totalSeconds))
                                    {
                                        duration = totalSeconds;
                                    }
                                }
                            }
                        }

                        // Detect track type based on characteristics
                        var trackType = DetectTrackType(bitrate, frameCount, duration, forced, isClosedCaption);

                        // Create track with:
                        // - trackNumber (actual Matroska track number) for display
                        // - id (mkvmerge zero-based index) for extraction commands
                        tracks.Add(new SubtitleTrack(trackNumber, codec, language, false, forced, 
                            name ?? "", bitrate ?? 0, frameCount ?? 0, 0, "", false, 
                            forced, isClosedCaption, false, trackType, frameCount ?? 0, extractionId: id));
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
    /// Try to parse duration string in format "HH:MM:SS.sssssssss" to total seconds.
    /// </summary>
    /// <param name="durationStr">Duration string to parse</param>
    /// <param name="totalSeconds">Output total seconds</param>
    /// <returns>True if parsing succeeded</returns>
    private static bool TryParseDuration(string durationStr, out double totalSeconds)
    {
        totalSeconds = 0;
        
        try
        {
            // Split by colon to get HH:MM:SS.sssssssss
            var parts = durationStr.Split(':');
            if (parts.Length != 3) return false;
            
            // Parse hours
            if (!int.TryParse(parts[0], out var hours)) return false;
            
            // Parse minutes  
            if (!int.TryParse(parts[1], out var minutes)) return false;
            
            // Parse seconds with decimal part
            if (!double.TryParse(parts[2], out var seconds)) return false;
            
            // Calculate total seconds
            totalSeconds = hours * 3600 + minutes * 60 + seconds;
            return true;
        }
        catch
        {
            return false;
        }
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

    public async Task<(string idxFilePath, string subFilePath)> ExtractVobSubWithFfmpegAsync(string mkvPath, int trackId, string outputDirectory, CancellationToken cancellationToken = default)
    {
        _loggingService.LogInfo($"Extracting VobSub track {trackId} using FFmpeg");

        try
        {
            // SECURITY: Validate and sanitize paths
            var validatedMkvPath = PathValidator.ValidateFileExists(mkvPath);
            var validatedOutputDir = SafeFileOperations.ValidateOutputPath(outputDirectory, validatedMkvPath);
            SafeFileOperations.SafeCreateDirectory(validatedOutputDir, validatedMkvPath);

            // Calculate timeout based on file size
            var timeout = CalculateTimeoutForFile(validatedMkvPath);
            _loggingService.LogInfo($"Using timeout of {timeout.TotalMinutes:F0} minutes for file size {GetFileSizeGB(validatedMkvPath):F1} GB");

            // Get FFmpeg path from tool detection
            var toolStatus = await _toolDetectionService.CheckFfmpegAsync();
            if (!toolStatus.IsInstalled || string.IsNullOrEmpty(toolStatus.Path))
            {
                throw new InvalidOperationException("FFmpeg not found");
            }

            // Generate output file names
            var baseFileName = Path.GetFileNameWithoutExtension(validatedMkvPath);
            var outputBasePath = Path.Combine(validatedOutputDir, $"{baseFileName}.{trackId}");
            var idxFilePath = $"{outputBasePath}.idx";
            var subFilePath = $"{outputBasePath}.sub";
            
            // Validate output paths (allow same directory as source)
            outputBasePath = SafeFileOperations.ValidateOutputPath(outputBasePath, validatedMkvPath);
            idxFilePath = SafeFileOperations.ValidateOutputPath(idxFilePath, validatedMkvPath);
            subFilePath = SafeFileOperations.ValidateOutputPath(subFilePath, validatedMkvPath);

            // SECURITY: Use argument array to prevent command injection
            // Use FFmpeg to extract VobSub subtitles
            // FFmpeg can extract VobSub directly from MKV and create .idx/.sub files
            var (exitCode, stdout, stderr) = await _processRunner.RunAsync(
                toolStatus.Path, 
                new[] { "-i", validatedMkvPath, "-map", $"0:s:{trackId - 1}", "-c", "copy", "-f", "vobsub", outputBasePath }, 
                timeout, 
                cancellationToken);

            if (exitCode != 0)
            {
                _loggingService.LogError($"FFmpeg failed with exit code {exitCode}: {stderr}");
                throw new InvalidOperationException($"FFmpeg failed with exit code {exitCode}: {stderr}");
            }

            // Check if both .idx and .sub files were created
            if (!File.Exists(idxFilePath))
            {
                throw new InvalidOperationException($"VobSub .idx file was not created: {idxFilePath}");
            }

            if (!File.Exists(subFilePath))
            {
                throw new InvalidOperationException($"VobSub .sub file was not created: {subFilePath}");
            }

            _loggingService.LogExtraction($"VobSub extraction with FFmpeg (track {trackId})", true);
            return (idxFilePath, subFilePath);
        }
        catch (Exception ex)
        {
            _loggingService.LogExtraction($"VobSub extraction with FFmpeg (track {trackId})", false, ex.Message);
            throw;
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
