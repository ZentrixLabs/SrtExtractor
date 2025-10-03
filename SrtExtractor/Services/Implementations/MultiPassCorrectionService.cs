using System.Diagnostics;
using SrtExtractor.Services.Interfaces;

namespace SrtExtractor.Services.Implementations;

/// <summary>
/// Service for performing multi-pass SRT correction.
/// Handles multiple passes of correction until convergence or max passes reached.
/// </summary>
public class MultiPassCorrectionService : IMultiPassCorrectionService
{
    private readonly ISrtCorrectionService _srtCorrectionService;
    private readonly ILoggingService _loggingService;

    public MultiPassCorrectionService(ISrtCorrectionService srtCorrectionService, ILoggingService loggingService)
    {
        _srtCorrectionService = srtCorrectionService;
        _loggingService = loggingService;
    }

    public async Task<MultiPassCorrectionResult> ProcessWithMultiPassAsync(
        string srtContent, 
        int maxPasses, 
        bool useSmartConvergence = true,
        CancellationToken cancellationToken = default)
    {
        _loggingService.LogInfo($"Starting multi-pass correction (max passes: {maxPasses}, smart convergence: {useSmartConvergence})");

        var stopwatch = Stopwatch.StartNew();
        var result = new MultiPassCorrectionResult();
        var currentContent = srtContent;
        var previousContent = "";
        var passNumber = 0;

        try
        {
            while (passNumber < maxPasses)
            {
                passNumber++;
                _loggingService.LogInfo($"🔄 Starting correction pass {passNumber} of {maxPasses}");

                var passStopwatch = Stopwatch.StartNew();
                previousContent = currentContent;

                // Perform correction on current content
                var correctionResult = await Task.Run(() => _srtCorrectionService.CorrectSrtContentWithCount(currentContent), cancellationToken);
                currentContent = correctionResult.correctedContent;
                var correctionCount = correctionResult.correctionCount;

                passStopwatch.Stop();

                // Track pass statistics
                var passStats = new PassStatistics
                {
                    PassNumber = passNumber,
                    CorrectionsMade = correctionCount,
                    ProcessingTimeMs = passStopwatch.ElapsedMilliseconds,
                    CorrectionTypes = new Dictionary<string, int>
                    {
                        { "OCR_Fixes", correctionCount }
                    }
                };

                result.PassStatistics.Add(passStats);
                result.TotalCorrections += correctionCount;

                _loggingService.LogInfo($"✅ Pass {passNumber} completed: {correctionCount} corrections in {passStopwatch.ElapsedMilliseconds}ms");

                // Check for convergence if smart convergence is enabled
                if (useSmartConvergence && currentContent == previousContent)
                {
                    _loggingService.LogInfo($"🎯 Convergence reached after {passNumber} passes - no more changes detected");
                    result.Converged = true;
                    break;
                }

                // Check for cancellation
                cancellationToken.ThrowIfCancellationRequested();
            }

            stopwatch.Stop();

            // Set final result properties
            result.CorrectedContent = currentContent;
            result.PassesCompleted = passNumber;
            result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;

            // Log summary
            _loggingService.LogInfo($"🎉 Multi-pass correction completed:");
            _loggingService.LogInfo($"   • Passes completed: {result.PassesCompleted}/{maxPasses}");
            _loggingService.LogInfo($"   • Total corrections: {result.TotalCorrections}");
            _loggingService.LogInfo($"   • Converged: {result.Converged}");
            _loggingService.LogInfo($"   • Total time: {result.ProcessingTimeMs}ms");

            return result;
        }
        catch (OperationCanceledException)
        {
            _loggingService.LogWarning("Multi-pass correction was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Error during multi-pass correction", ex);
            
            // Return partial result if we have some progress
            result.CorrectedContent = currentContent;
            result.PassesCompleted = passNumber;
            result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
            result.Warnings.Add($"Error during processing: {ex.Message}");
            
            return result;
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    public async Task<MultiPassCorrectionResult> ProcessWithModeAsync(
        string srtContent, 
        string correctionMode,
        CancellationToken cancellationToken = default)
    {
        _loggingService.LogInfo($"Starting correction with mode: {correctionMode}");

        var (maxPasses, useSmartConvergence) = correctionMode switch
        {
            "Quick" => (1, false),
            "Standard" => (3, true),
            "Thorough" => (5, false),
            _ => (3, true)
        };

        return await ProcessWithMultiPassAsync(srtContent, maxPasses, useSmartConvergence, cancellationToken);
    }
}
