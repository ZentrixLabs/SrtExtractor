using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using SrtExtractor.Constants;
using SrtExtractor.Models;
using SrtExtractor.Services.Interfaces;
using SrtExtractor.State;

namespace SrtExtractor.Coordinators;

/// <summary>
/// Coordinates subtitle extraction operations including text-based, image-based (PGS), 
/// and MP4 extraction strategies, as well as OCR correction.
/// </summary>
public class ExtractionCoordinator
{
    private readonly ILoggingService _loggingService;
    private readonly INotificationService _notificationService;
    private readonly IMkvToolService _mkvToolService;
    private readonly IFfmpegService _ffmpegService;
    private readonly ISubtitleOcrService _ocrService;
    private readonly ISrtCorrectionService _srtCorrectionService;
    private readonly IMultiPassCorrectionService _multiPassCorrectionService;
    private readonly ExtractionState _state;

    public ExtractionCoordinator(
        ILoggingService loggingService,
        INotificationService notificationService,
        IMkvToolService mkvToolService,
        IFfmpegService ffmpegService,
        ISubtitleOcrService ocrService,
        ISrtCorrectionService srtCorrectionService,
        IMultiPassCorrectionService multiPassCorrectionService,
        ExtractionState state)
    {
        _loggingService = loggingService;
        _notificationService = notificationService;
        _mkvToolService = mkvToolService;
        _ffmpegService = ffmpegService;
        _ocrService = ocrService;
        _srtCorrectionService = srtCorrectionService;
        _multiPassCorrectionService = multiPassCorrectionService;
        _state = state;
    }

    /// <summary>
    /// Extract subtitles from the specified file using the appropriate strategy.
    /// </summary>
    /// <param name="mkvPath">Path to the video file</param>
    /// <param name="selectedTrack">The subtitle track to extract</param>
    /// <param name="outputPath">Where to save the extracted SRT file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task ExtractSubtitlesAsync(string mkvPath, SubtitleTrack selectedTrack, string outputPath, CancellationToken cancellationToken)
    {
        _loggingService.LogInfo($"Starting extraction: {Path.GetFileName(mkvPath)} -> {Path.GetFileName(outputPath)}");
        
        try
        {
            // Use appropriate extraction strategy based on file type and codec
            var fileExtension = Path.GetExtension(mkvPath).ToLowerInvariant();
            if (fileExtension == ".mp4")
            {
                await ExtractFromMp4Async(mkvPath, selectedTrack, outputPath, cancellationToken);
            }
            else
            {
                // Use CodecType enum for type-safe dispatch
                await ExecuteExtractionByCodecType(mkvPath, selectedTrack, outputPath, cancellationToken);
            }

            _loggingService.LogInfo($"Extraction completed successfully: {Path.GetFileName(outputPath)}");
        }
        catch (OperationCanceledException)
        {
            _loggingService.LogInfo("Subtitle extraction was cancelled by user");
            await CleanupTemporaryFiles(mkvPath, selectedTrack, outputPath);
            throw;
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to extract subtitles", ex);
            await CleanupTemporaryFiles(mkvPath, selectedTrack, outputPath);
            throw;
        }
    }

    /// <summary>
    /// Extract subtitles from MP4 files using FFmpeg.
    /// </summary>
    private async Task ExtractFromMp4Async(string mkvPath, SubtitleTrack selectedTrack, string outputPath, CancellationToken cancellationToken)
    {
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            _state.UpdateProcessingMessage("Extracting subtitles with FFmpeg...");
        });
        
        await _ffmpegService.ExtractSubtitleAsync(mkvPath, selectedTrack.Id, outputPath, cancellationToken);
        
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            _state.UpdateProcessingMessage("MP4 extraction completed!");
            _state.AddLogMessage($"Subtitles extracted to: {outputPath}");
        });
        
        // BUGFIX: Convert ASS to SRT if needed (some MP4s contain ASS subtitles that get extracted with .srt extension)
        await _ocrService.ConvertAssToSrtIfNeededAsync(outputPath, cancellationToken);

        // Apply multi-pass SRT corrections to MP4 subtitles
        await ApplyMultiPassCorrectionAsync(outputPath, cancellationToken);
    }

    /// <summary>
    /// Execute extraction strategy based on codec type (type-safe dispatch).
    /// </summary>
    private async Task ExecuteExtractionByCodecType(string mkvPath, SubtitleTrack selectedTrack, string outputPath, CancellationToken cancellationToken)
    {
        switch (selectedTrack.CodecType)
        {
            case SubtitleCodecType.TextBasedSrt:
            case SubtitleCodecType.TextBasedAss:
            case SubtitleCodecType.TextBasedWebVtt:
            case SubtitleCodecType.TextBasedGeneric:
                await ExtractTextSubtitlesAsync(mkvPath, selectedTrack, outputPath, cancellationToken);
                break;

            case SubtitleCodecType.ImageBasedPgs:
                await ExtractPgsSubtitlesAsync(mkvPath, selectedTrack, outputPath, cancellationToken);
                break;

            case SubtitleCodecType.ImageBasedVobSub:
                ShowVobSubGuidance();
                throw new InvalidOperationException("VobSub subtitles require Subtitle Edit for OCR processing. See the VobSub Track Analyzer tool for help.");

            case SubtitleCodecType.ImageBasedDvb:
                throw new NotSupportedException("DVB subtitles are not currently supported");

            default:
                throw new NotSupportedException($"Unsupported subtitle codec: {selectedTrack.Codec}");
        }
    }

    /// <summary>
    /// Extract text-based subtitles (SRT, ASS, WebVTT).
    /// </summary>
    private async Task ExtractTextSubtitlesAsync(string mkvPath, SubtitleTrack selectedTrack, string outputPath, CancellationToken cancellationToken)
    {
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            _state.UpdateProcessingMessage("Extracting text subtitles...");
            // Simulate progress for text extraction (this is typically very fast)
            _state.UpdateProgress(ProgressMilestones.CalculateBytes(_state.TotalBytes, ProgressMilestones.TextExtractionStart), "Extracting text subtitles");
        });
        
        await _mkvToolService.ExtractTextAsync(mkvPath, selectedTrack.ExtractionId, outputPath, cancellationToken);
        
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            _state.UpdateProgress(ProgressMilestones.CalculateBytes(_state.TotalBytes, ProgressMilestones.TextExtractionComplete), "Text extraction completed");
            _state.UpdateProcessingMessage("Text extraction completed!");
            _state.AddLogMessage($"Text subtitles extracted to: {outputPath}");
        });
        
        // BUGFIX: Convert ASS to SRT if needed (some MKVs contain ASS subtitles that get extracted with .srt extension)
        await _ocrService.ConvertAssToSrtIfNeededAsync(outputPath, cancellationToken);

        // Apply multi-pass SRT corrections to text subtitles
        await ApplyMultiPassCorrectionAsync(outputPath, cancellationToken);
    }

    /// <summary>
    /// Extract PGS (image-based) subtitles and convert to SRT using OCR.
    /// </summary>
    private async Task ExtractPgsSubtitlesAsync(string mkvPath, SubtitleTrack selectedTrack, string outputPath, CancellationToken cancellationToken)
    {
        var tempSupPath = Path.ChangeExtension(outputPath, ".sup");
        
        // Extract PGS to SUP file
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            _state.UpdateProcessingMessage("Extracting PGS subtitles... (this can take a while, please be patient)");
            _state.UpdateProgress(ProgressMilestones.CalculateBytes(_state.TotalBytes, ProgressMilestones.PgsExtractionStart), "Extracting PGS subtitles");
        });
        
        await _mkvToolService.ExtractPgsAsync(mkvPath, selectedTrack.ExtractionId, tempSupPath, cancellationToken);
        
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            _state.AddLogMessage($"PGS subtitles extracted to: {tempSupPath}");
        });

        // Convert SUP to SRT using OCR
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            _state.UpdateProcessingMessage("Starting OCR conversion... (this is the slowest step, please be patient)");
            _state.AddLogMessage($"Starting OCR conversion to: {outputPath}");
            _state.UpdateProgress(ProgressMilestones.CalculateBytes(_state.TotalBytes, ProgressMilestones.OcrStart), "Starting OCR conversion");
        });
        
        await _ocrService.OcrSupToSrtAsync(tempSupPath, outputPath, _state.OcrLanguage, true, true, cancellationToken);
        
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            _state.UpdateProgress(ProgressMilestones.CalculateBytes(_state.TotalBytes, ProgressMilestones.OcrComplete), "OCR conversion completed");
            _state.UpdateProcessingMessage("OCR conversion completed!");
            _state.AddLogMessage($"OCR conversion completed: {outputPath}");
        });

        // Apply multi-pass OCR corrections
        await ApplyMultiPassCorrectionAsync(outputPath, cancellationToken);

        // Clean up temporary SUP file (unless user wants to preserve it for debugging)
        if (_state.PreserveSupFiles)
        {
            _loggingService.LogInfo($"Preserving SUP file for debugging: {tempSupPath}");
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _state.AddLogMessage($"SUP file preserved: {Path.GetFileName(tempSupPath)}");
            });
            
            // Show guidance notification to help users know what to do next
            _notificationService.ShowInfo(
                "SUP file preserved for debugging!\n\n" +
                "💡 Next steps:\n" +
                "• Use Tools → Load SUP File... to re-process with different settings\n" +
                "• Try different OCR languages or correction levels\n" +
                "• Perfect for testing OCR quality improvements",
                "SUP File Preserved");
        }
        else
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _state.UpdateProcessingMessage("Cleaning up temporary files...");
            });
            
            try
            {
                File.Delete(tempSupPath);
                _loggingService.LogInfo("Temporary SUP file deleted");
            }
            catch (Exception ex)
            {
                _loggingService.LogWarning($"Failed to delete temporary SUP file: {ex.Message}");
                // Ignore cleanup errors
            }
        }
        
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            _state.UpdateProcessingMessage("PGS extraction completed!");
        });
    }

    /// <summary>
    /// Show guidance message for VobSub subtitles which require Subtitle Edit.
    /// </summary>
    private void ShowVobSubGuidance()
    {
        _loggingService.LogInfo("VobSub track detected - directing user to Subtitle Edit for OCR processing");
        
        var message = "VobSub Image-Based Subtitles Detected\n\n" +
                     "This subtitle track is VobSub (image-based) which requires OCR processing.\n\n" +
                     "We recommend using Subtitle Edit for VobSub extraction:\n\n" +
                     "1. Open Subtitle Edit\n" +
                     "2. Go to: Tools → Batch Convert\n" +
                     "3. Add your MKV file(s)\n" +
                     "4. Set format: SubRip (.srt)\n" +
                     "5. Configure OCR settings\n" +
                     "6. Click Convert\n\n" +
                     "Tip: Use Tools → VobSub Track Analyzer in SrtExtractor to identify track numbers across multiple files!";
        
        Application.Current.Dispatcher.Invoke(() =>
        {
            _notificationService.ShowInfo(message, "VobSub Subtitles - Use Subtitle Edit", 8000);
            _state.UpdateProcessingMessage("VobSub extraction cancelled - please use Subtitle Edit");
            _state.AddLogMessage("VobSub track detected. Please use Subtitle Edit's batch convert feature for OCR.");
        });
    }

    /// <summary>
    /// Apply multi-pass correction to an SRT file based on current settings.
    /// </summary>
    private async Task ApplyMultiPassCorrectionAsync(string srtPath, CancellationToken cancellationToken)
    {
        try
        {
            // Check if SRT correction is completely disabled
            if (!_state.EnableSrtCorrection)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _state.AddLogMessage("ℹ️ SRT correction is disabled - using raw OCR output");
                });
                _loggingService.LogInfo("SRT correction disabled - skipping all corrections");
                return;
            }

            if (!_state.EnableMultiPassCorrection)
            {
                // Use single-pass correction if multi-pass is disabled
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _state.UpdateProcessingMessage("Correcting common subtitle errors...");
                    _state.AddLogMessage("Correcting common subtitle errors...");
                });
                
                var correctionCount = await _srtCorrectionService.CorrectSrtFileAsync(srtPath, cancellationToken).ConfigureAwait(false);
                
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _state.UpdateProcessingMessage("Subtitle correction completed!");
                    _state.AddLogMessage($"🎯 Subtitle correction completed! Applied {correctionCount} corrections.");
                });
                return;
            }

            // Read the SRT content
            var srtContent = await File.ReadAllTextAsync(srtPath, cancellationToken).ConfigureAwait(false);
            
            // Apply multi-pass correction based on current mode
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _state.UpdateProcessingMessage($"Starting {_state.CorrectionMode.ToLower()} multi-pass correction...");
                _state.AddLogMessage($"Starting {_state.CorrectionMode.ToLower()} multi-pass correction...");
            });
            
            var result = await _multiPassCorrectionService.ProcessWithModeAsync(
                srtContent, 
                _state.CorrectionMode, 
                cancellationToken).ConfigureAwait(false);
            
            // Write the corrected content back to file
            if (result.CorrectedContent != srtContent)
            {
                await File.WriteAllTextAsync(srtPath, result.CorrectedContent, cancellationToken).ConfigureAwait(false);
            }
            
            // Update UI with results - marshal back to UI thread
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _state.UpdateProcessingMessage("Multi-pass correction completed!");
                
                var convergenceText = result.Converged ? " (converged)" : "";
                _state.AddLogMessage($"🎯 Multi-pass correction completed!{convergenceText}");
                _state.AddLogMessage($"   • Passes completed: {result.PassesCompleted}");
                _state.AddLogMessage($"   • Total corrections: {result.TotalCorrections}");
                _state.AddLogMessage($"   • Processing time: {result.ProcessingTimeMs}ms");
            });
            
            // Log any warnings
            if (result.Warnings.Any())
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    foreach (var warning in result.Warnings)
                    {
                        _state.AddLogMessage($"⚠️ Warning: {warning}");
                    }
                });
            }
            
            _loggingService.LogInfo($"Multi-pass correction completed: {result.PassesCompleted} passes, {result.TotalCorrections} corrections, {result.ProcessingTimeMs}ms");
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Error during multi-pass correction", ex);
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _state.AddLogMessage($"❌ Error during correction: {ex.Message}");
            });
            
            // Fall back to single-pass correction
            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _state.UpdateProcessingMessage("Falling back to single-pass correction...");
                    _state.AddLogMessage("Falling back to single-pass correction...");
                });
                
                var correctionCount = await _srtCorrectionService.CorrectSrtFileAsync(srtPath, cancellationToken).ConfigureAwait(false);
                
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _state.UpdateProcessingMessage("Single-pass correction completed!");
                    _state.AddLogMessage($"🎯 Single-pass correction completed! Applied {correctionCount} corrections.");
                });
            }
            catch (Exception fallbackEx)
            {
                _loggingService.LogError("Error during fallback single-pass correction", fallbackEx);
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _state.AddLogMessage($"❌ Error during fallback correction: {fallbackEx.Message}");
                });
                throw;
            }
        }
    }

    /// <summary>
    /// Clean up temporary files that might have been created during extraction.
    /// </summary>
    private async Task CleanupTemporaryFiles(string mkvPath, SubtitleTrack? selectedTrack, string outputPath)
    {
        if (selectedTrack == null || string.IsNullOrEmpty(mkvPath))
            return;

        try
        {
            // Check if this was a PGS extraction (which creates a .sup file)
            if (selectedTrack.Codec.Contains("PGS") || selectedTrack.Codec.Contains("S_HDMV/PGS"))
            {
                var tempSupPath = Path.ChangeExtension(outputPath, ".sup");
                
                if (File.Exists(tempSupPath))
                {
                    _loggingService.LogInfo($"Cleaning up temporary SUP file: {tempSupPath}");
                    
                    // Use exponential backoff with shorter total time to prevent UI blocking
                    var retryDelays = new[] { 100, 200, 500, 1000, 1500 }; // Total: max 3.3 seconds
                    for (int i = 0; i < retryDelays.Length; i++)
                    {
                        try
                        {
                            File.Delete(tempSupPath);
                            _loggingService.LogInfo($"Successfully cleaned up temporary file: {Path.GetFileName(tempSupPath)}");
                            break;
                        }
                        catch (IOException) when (i < retryDelays.Length - 1)
                        {
                            _loggingService.LogInfo($"File still in use, retrying in {retryDelays[i]}ms... (attempt {i + 1}/{retryDelays.Length})");
                            await Task.Delay(retryDelays[i]);
                        }
                        catch (UnauthorizedAccessException) when (i < retryDelays.Length - 1)
                        {
                            _loggingService.LogInfo($"Access denied, retrying in {retryDelays[i]}ms... (attempt {i + 1}/{retryDelays.Length})");
                            await Task.Delay(retryDelays[i]);
                        }
                        catch (Exception ex) when (i == retryDelays.Length - 1)
                        {
                            // Log final failure but don't throw - cleanup is best-effort
                            _loggingService.LogWarning($"Failed to clean up temporary file after {retryDelays.Length} attempts: {ex.Message}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to clean up temporary files", ex);
            // Don't throw - cleanup is best-effort
        }
    }
}

