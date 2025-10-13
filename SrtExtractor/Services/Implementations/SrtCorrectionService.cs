using System.IO;
using SrtExtractor.Services.Interfaces;
using ZentrixLabs.OcrCorrection.Core;
using ZentrixLabs.OcrCorrection.Configuration;
using ZentrixLabs.OcrCorrection.Patterns;

namespace SrtExtractor.Services.Implementations;

/// <summary>
/// Service for correcting common OCR errors in SRT files.
/// Uses ZentrixLabs.OcrCorrection library with ~837 comprehensive correction patterns.
/// </summary>
public class SrtCorrectionService : ISrtCorrectionService
{
    private readonly ILoggingService _loggingService;
    private readonly IOcrCorrectionEngine _ocrCorrectionEngine;

    public SrtCorrectionService(ILoggingService loggingService, IOcrCorrectionEngine ocrCorrectionEngine)
    {
        _loggingService = loggingService;
        _ocrCorrectionEngine = ocrCorrectionEngine;
    }

    public async Task<int> CorrectSrtFileAsync(string srtPath, CancellationToken cancellationToken = default)
    {
        _loggingService.LogInfo($"Correcting OCR errors in SRT file: {srtPath}");

        if (!File.Exists(srtPath))
        {
            throw new FileNotFoundException($"SRT file not found: {srtPath}");
        }

        try
        {
            var content = await File.ReadAllTextAsync(srtPath, cancellationToken);
            var (correctedContent, correctionCount) = CorrectSrtContentWithCount(content);
            
            if (content != correctedContent)
            {
                await File.WriteAllTextAsync(srtPath, correctedContent, cancellationToken);
                _loggingService.LogInfo($"✅ SRT file corrected successfully: {correctionCount} corrections applied");
            }
            else
            {
                _loggingService.LogInfo("No corrections needed in SRT file");
            }

            return correctionCount;
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to correct SRT file", ex);
            throw;
        }
    }

    public (string correctedContent, int correctionCount) CorrectSrtContentWithCount(string content)
    {
        _loggingService.LogInfo("Starting OCR correction using ZentrixLabs.OcrCorrection engine (~837 patterns)");

        try
        {
            // Use the comprehensive OCR correction engine
            var options = new CorrectionOptions
            {
                EnableDetailedLogging = false, // Set to true for debugging if needed
                TrackPerformanceMetrics = true,
                CollectCorrectionDetails = false // Set to true for detailed logging if needed
            };

            var result = _ocrCorrectionEngine.Correct(content, options);

            // Log summary
            if (result.CorrectionCount > 0)
            {
                _loggingService.LogInfo($"🎯 OCR corrections applied: {result.CorrectionCount} corrections");
                _loggingService.LogInfo($"   • Processing time: {result.ProcessingTime.TotalMilliseconds:F0}ms");
            }
            else
            {
                _loggingService.LogInfo("No OCR corrections needed");
            }

            return (result.CorrectedText, result.CorrectionCount);
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Error during OCR correction", ex);
            // Return original content if correction fails
            return (content, 0);
        }
    }

    public string CorrectSrtContent(string content)
    {
        var (corrected, _) = CorrectSrtContentWithCount(content);
        return corrected;
    }
}
