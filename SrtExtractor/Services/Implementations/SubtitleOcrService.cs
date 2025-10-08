using System.IO;
using SrtExtractor.Services.Interfaces;

namespace SrtExtractor.Services.Implementations;

/// <summary>
/// Service for Subtitle Edit OCR operations.
/// </summary>
public class SubtitleOcrService : ISubtitleOcrService
{
    private readonly ILoggingService _loggingService;
    private readonly IProcessRunner _processRunner;
    private readonly ISettingsService _settingsService;

    public SubtitleOcrService(ILoggingService loggingService, IProcessRunner processRunner, ISettingsService settingsService)
    {
        _loggingService = loggingService;
        _processRunner = processRunner;
        _settingsService = settingsService;
    }

    public async Task OcrSupToSrtAsync(
        string supPath, 
        string outSrt, 
        string language, 
        bool fixCommonErrors = true, 
        bool removeHi = true,
        CancellationToken cancellationToken = default)
    {
        _loggingService.LogInfo($"Starting OCR conversion: {supPath} -> {outSrt}");

        if (!File.Exists(supPath))
        {
            throw new FileNotFoundException($"SUP file not found: {supPath}");
        }

        try
        {
            var settings = await _settingsService.LoadSettingsAsync();
            if (string.IsNullOrEmpty(settings.SubtitleEditPath))
            {
                throw new InvalidOperationException("Subtitle Edit path not configured");
            }

            // Ensure output directory exists
            var outputDir = Path.GetDirectoryName(outSrt);
            if (!string.IsNullOrEmpty(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Build Subtitle Edit command line arguments
            var args = BuildOcrArguments(settings.SubtitleEditPath, supPath, outSrt, language, fixCommonErrors, removeHi);
            
            _loggingService.LogInfo($"Running Subtitle Edit CLI with args: {args}");
            
            // Calculate timeout based on file size (OCR is slow, especially for large files)
            var timeout = CalculateOcrTimeout(supPath);
            _loggingService.LogInfo($"Using OCR timeout of {timeout.TotalMinutes:F0} minutes for file size {GetFileSizeMB(supPath):F1} MB");
            
            using var cts = new CancellationTokenSource(timeout);
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);
            var (exitCode, stdout, stderr) = await _processRunner.RunAsync(settings.SubtitleEditPath, args, combinedCts.Token);

            if (exitCode != 0)
            {
                throw new InvalidOperationException($"Subtitle Edit failed with exit code {exitCode}: {stderr}");
            }

            if (!File.Exists(outSrt))
            {
                throw new InvalidOperationException($"Output SRT file was not created: {outSrt}");
            }

            _loggingService.LogExtraction($"OCR conversion ({language})", true);
        }
        catch (Exception ex)
        {
            _loggingService.LogExtraction($"OCR conversion ({language})", false, ex.Message);
            throw;
        }
    }

    private static string BuildOcrArguments(string toolPath, string supPath, string outSrt, string language, bool fixCommonErrors, bool removeHi)
    {
        // seconv syntax: seconv <pattern> <format> [options]
        // For SUP to SRT conversion with OCR
        // Map language codes to OCR database names
        var ocrDb = MapLanguageToOcrDb(language);
        var args = $"\"{supPath}\" subrip /outputfilename:\"{outSrt}\" /ocrdb:{ocrDb}";
        
        if (fixCommonErrors)
        {
            // Note: fixCommonErrors functionality might be handled differently in this version
            // Check if there's a specific parameter for this
        }

        if (removeHi)
        {
            args += " /RemoveTextForHI";
        }

        return args;
    }

    /// <summary>
    /// Map language codes to OCR database names.
    /// </summary>
    /// <param name="language">Language code (e.g., "eng", "spa", "fre")</param>
    /// <returns>OCR database name</returns>
    private static string MapLanguageToOcrDb(string language)
    {
        // Map common language codes to available OCR databases
        return language.ToLowerInvariant() switch
        {
            "eng" or "en" => "Latin",  // English uses Latin script
            "spa" or "es" => "Latin",  // Spanish uses Latin script
            "fre" or "fr" => "Latin",  // French uses Latin script
            "deu" or "de" => "Latin",  // German uses Latin script
            "ita" or "it" => "Latin",  // Italian uses Latin script
            "por" or "pt" => "Latin",  // Portuguese uses Latin script
            _ => "Latin"               // Default to Latin for most Western languages
        };
    }

    /// <summary>
    /// Calculate appropriate OCR timeout based on SUP file size.
    /// OCR is very slow, especially for large PGS subtitle files.
    /// </summary>
    /// <param name="supPath">Path to the SUP file</param>
    /// <returns>Calculated timeout duration</returns>
    private static TimeSpan CalculateOcrTimeout(string supPath)
    {
        try
        {
            var fileInfo = new FileInfo(supPath);
            var sizeMB = fileInfo.Length / (1024.0 * 1024.0);
            
            // OCR is much slower than extraction
            // Base: 5 minutes, add 3 minutes per 50MB
            // Network files and large files need more time
            var baseMinutes = 5.0;
            var additionalMinutes = (sizeMB / 50.0) * 3.0;
            
            var totalMinutes = baseMinutes + additionalMinutes;
            
            // Cap at 2 hours for very large files
            var maxMinutes = 120;
            totalMinutes = Math.Min(totalMinutes, maxMinutes);
            
            return TimeSpan.FromMinutes(totalMinutes);
        }
        catch
        {
            // If we can't determine file size, use a safe default of 30 minutes
            return TimeSpan.FromMinutes(30);
        }
    }

    /// <summary>
    /// Get file size in MB for logging purposes.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <returns>File size in MB</returns>
    private static double GetFileSizeMB(string filePath)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            return fileInfo.Length / (1024.0 * 1024.0);
        }
        catch
        {
            return 0;
        }
    }
}
