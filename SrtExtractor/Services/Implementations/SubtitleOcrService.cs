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
            
            // Use a shorter timeout for Subtitle Edit to prevent GUI hanging
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
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
        var args = $"\"{supPath}\" subrip --output \"{outSrt}\" --ocr-language {language}";
        
        if (fixCommonErrors)
        {
            args += " --fix-common-errors";
        }

        if (removeHi)
        {
            args += " --remove-hi";
        }

        return args;
    }
}
