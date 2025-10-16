using System.IO;
using System.Linq;
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
            // CRITICAL FIX: Apply OCR corrections ONLY to subtitle text, not to sequence numbers or timestamps
            // Parse SRT content line by line and only correct the text portions
            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var correctedLines = new List<string>();
            int totalCorrections = 0;
            var options = new CorrectionOptions
            {
                EnableDetailedLogging = false,
                TrackPerformanceMetrics = true,
                CollectCorrectionDetails = false
            };

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                
                // Skip empty lines, sequence numbers, and timestamp lines
                // Sequence number: just digits (e.g., "1", "123")
                // Timestamp line: contains " --> " (e.g., "00:00:01,000 --> 00:00:03,500")
                if (string.IsNullOrWhiteSpace(line) || 
                    IsSequenceNumber(line) || 
                    line.Contains(" --> "))
                {
                    // Keep these lines unchanged
                    correctedLines.Add(line);
                }
                else
                {
                    // This is subtitle text - apply OCR corrections
                    var result = _ocrCorrectionEngine.Correct(line, options);
                    correctedLines.Add(result.CorrectedText);
                    totalCorrections += result.CorrectionCount;
                }
            }

            var correctedContent = string.Join(Environment.NewLine, correctedLines);

            // Log summary
            if (totalCorrections > 0)
            {
                _loggingService.LogInfo($"🎯 OCR corrections applied: {totalCorrections} corrections");
            }
            else
            {
                _loggingService.LogInfo("No OCR corrections needed");
            }

            return (correctedContent, totalCorrections);
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Error during OCR correction", ex);
            // Return original content if correction fails
            return (content, 0);
        }
    }

    /// <summary>
    /// Check if a line is an SRT sequence number (e.g., "1", "123", "4567").
    /// Sequence numbers are lines containing only digits and optional whitespace.
    /// </summary>
    private static bool IsSequenceNumber(string line)
    {
        var trimmed = line.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return false;
        }
        
        // Check if line contains only digits
        return trimmed.All(char.IsDigit);
    }

    public string CorrectSrtContent(string content)
    {
        var (corrected, _) = CorrectSrtContentWithCount(content);
        return corrected;
    }
}
