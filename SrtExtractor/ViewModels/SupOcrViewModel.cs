using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SrtExtractor.Services.Interfaces;

namespace SrtExtractor.ViewModels;

public partial class SupOcrViewModel : ObservableObject
{
    private readonly ISubtitleOcrService _ocrService;
    private readonly ILoggingService _loggingService;
    private readonly INotificationService _notificationService;
    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    private string _supFilePath = string.Empty;

    [ObservableProperty]
    private string _ocrLanguage = "eng";

    [ObservableProperty]
    private bool _applyCorrection = true;

    [ObservableProperty]
    private bool _isProcessing = false;

    [ObservableProperty]
    private bool _isIndeterminate = false;

    [ObservableProperty]
    private double _progressPercentage = 0;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private string _logMessages = string.Empty;

    [ObservableProperty]
    private int _totalFrames = 0;

    [ObservableProperty]
    private int _processedFrames = 0;

    [ObservableProperty]
    private double _processingSpeed = 0; // frames per second

    [ObservableProperty]
    private TimeSpan _estimatedTimeRemaining = TimeSpan.Zero;

    [ObservableProperty]
    private TimeSpan _elapsedTime = TimeSpan.Zero;

    [ObservableProperty]
    private string _currentPhase = string.Empty;

    private StringBuilder _logBuilder = new();
    private DateTime _startTime;
    private readonly object _progressLock = new();

    public SupOcrViewModel(
        ISubtitleOcrService ocrService,
        ILoggingService loggingService,
        INotificationService notificationService)
    {
        _ocrService = ocrService;
        _loggingService = loggingService;
        _notificationService = notificationService;
    }

    public bool CanStartOcr => !IsProcessing && !string.IsNullOrEmpty(SupFilePath) && File.Exists(SupFilePath);

    [RelayCommand]
    private void BrowseSupFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select SUP File for OCR",
            Filter = "SUP Files (*.sup)|*.sup|All Files (*.*)|*.*",
            DefaultExt = "sup"
        };

        if (dialog.ShowDialog() == true)
        {
            SupFilePath = dialog.FileName;
            AddLog($"Selected SUP file: {Path.GetFileName(SupFilePath)}");
            OnPropertyChanged(nameof(CanStartOcr));
        }
    }

    [RelayCommand]
    private async Task StartOcr()
    {
        if (string.IsNullOrEmpty(SupFilePath) || !File.Exists(SupFilePath))
        {
            _notificationService.ShowError("Please select a valid SUP file.", "Invalid File");
            return;
        }

        try
        {
            IsProcessing = true;
            IsIndeterminate = false; // We'll have determinate progress now
            _cancellationTokenSource = new CancellationTokenSource();

            // Reset progress tracking
            ResetProgress();

            StatusMessage = "Preparing SUP file for processing...";
            CurrentPhase = "Initializing";
            AddLog($"Starting OCR processing: {Path.GetFileName(SupFilePath)}");

            var outputPath = Path.ChangeExtension(SupFilePath, ".srt");

            // Run OCR with progress tracking
            StatusMessage = "Extracting subtitle images from SUP file...";
            CurrentPhase = "Parsing SUP";
            AddLog($"Output will be saved to: {Path.GetFileName(outputPath)}");
            AddLog($"Using language: {OcrLanguage}");
            AddLog($"Auto-correction: {(ApplyCorrection ? "Enabled" : "Disabled")}");

            // Create a progress callback for the OCR service
            var progressCallback = new Progress<(int processed, int total, string phase)>(
                progress => UpdateProgress(progress.processed, progress.total, progress.phase));

            await _ocrService.OcrSupToSrtAsync(
                SupFilePath,
                outputPath,
                OcrLanguage,
                fixCommonErrors: ApplyCorrection,
                removeHi: false,
                _cancellationTokenSource.Token,
                progressCallback
            );

            // Success
            StatusMessage = "OCR completed successfully!";
            CurrentPhase = "Completed";
            ProgressText = $"✓ Completed: {Path.GetFileName(outputPath)}";
            AddLog($"✓ OCR completed successfully!");
            AddLog($"✓ Output file: {outputPath}");
            AddLog($"✓ Total processing time: {FormatTimeSpan(ElapsedTime)}");

            _notificationService.ShowSuccess(
                $"SUP file processed successfully!\n\nOutput: {Path.GetFileName(outputPath)}",
                "OCR Complete"
            );

            _loggingService.LogInfo($"SUP OCR completed successfully: {Path.GetFileName(outputPath)}");
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Processing cancelled";
            AddLog("✗ Processing cancelled by user");
            _loggingService.LogInfo("SUP OCR cancelled by user");
        }
        catch (Exception ex)
        {
            StatusMessage = "Error occurred during processing";
            AddLog($"✗ ERROR: {ex.Message}");
            _loggingService.LogError("SUP OCR failed", ex);
            _notificationService.ShowError($"Failed to process SUP file:\n{ex.Message}", "OCR Error");
        }
        finally
        {
            IsProcessing = false;
            IsIndeterminate = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            OnPropertyChanged(nameof(CanStartOcr));
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _cancellationTokenSource?.Cancel();
        AddLog("Cancellation requested...");
    }

    private void AddLog(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        _logBuilder.AppendLine($"[{timestamp}] {message}");
        LogMessages = _logBuilder.ToString();
    }

    /// <summary>
    /// Update progress information with detailed metrics.
    /// </summary>
    public void UpdateProgress(int processedFrames, int totalFrames, string phase = "")
    {
        lock (_progressLock)
        {
            ProcessedFrames = processedFrames;
            TotalFrames = totalFrames;
            
            if (!string.IsNullOrEmpty(phase))
            {
                CurrentPhase = phase;
                UpdateStatusMessageForPhase(phase, processedFrames, totalFrames);
            }

            // Calculate progress percentage
            if (totalFrames > 0)
            {
                ProgressPercentage = (double)processedFrames / totalFrames * 100;
            }

            // Calculate elapsed time
            ElapsedTime = DateTime.Now - _startTime;

            // Calculate processing speed (frames per second)
            if (ElapsedTime.TotalSeconds > 0)
            {
                ProcessingSpeed = processedFrames / ElapsedTime.TotalSeconds;
            }

            // Calculate estimated time remaining
            if (ProcessingSpeed > 0 && processedFrames > 0)
            {
                var remainingFrames = totalFrames - processedFrames;
                EstimatedTimeRemaining = TimeSpan.FromSeconds(remainingFrames / ProcessingSpeed);
            }

            // Update progress text with detailed information
            ProgressText = $"{processedFrames}/{totalFrames} frames ({ProgressPercentage:F1}%) • " +
                          $"{ProcessingSpeed:F1} fps • " +
                          $"ETA: {FormatTimeSpan(EstimatedTimeRemaining)}";
        }
    }

    /// <summary>
    /// Update status message based on the current processing phase.
    /// </summary>
    private void UpdateStatusMessageForPhase(string phase, int processedFrames, int totalFrames)
    {
        switch (phase.ToLowerInvariant())
        {
            case "parsing sup file":
                StatusMessage = "Analyzing SUP file structure...";
                break;
            case "initializing tesseract":
                StatusMessage = "Setting up Tesseract OCR engine...";
                break;
            case "processing frames":
                if (totalFrames > 0)
                {
                    var percentage = (double)processedFrames / totalFrames * 100;
                    if (percentage < 25)
                        StatusMessage = "Starting OCR processing...";
                    else if (percentage < 50)
                        StatusMessage = "Processing subtitle images...";
                    else if (percentage < 75)
                        StatusMessage = "Converting images to text...";
                    else if (percentage < 90)
                        StatusMessage = "Finalizing OCR results...";
                    else
                        StatusMessage = "Completing OCR processing...";
                }
                else
                {
                    StatusMessage = "Processing subtitle images...";
                }
                break;
            case "saving srt file":
                StatusMessage = "Saving results to SRT file...";
                break;
            default:
                StatusMessage = "Processing SUP file...";
                break;
        }
    }

    /// <summary>
    /// Reset progress information for a new operation.
    /// </summary>
    public void ResetProgress()
    {
        lock (_progressLock)
        {
            _startTime = DateTime.Now;
            TotalFrames = 0;
            ProcessedFrames = 0;
            ProcessingSpeed = 0;
            EstimatedTimeRemaining = TimeSpan.Zero;
            ElapsedTime = TimeSpan.Zero;
            CurrentPhase = string.Empty;
            ProgressPercentage = 0;
            ProgressText = string.Empty;
        }
    }

    /// <summary>
    /// Format TimeSpan for display (h:mm:ss or m:ss).
    /// </summary>
    private static string FormatTimeSpan(TimeSpan timeSpan)
    {
        if (timeSpan == TimeSpan.Zero)
            return "0s";

        if (timeSpan.TotalHours >= 1)
            return $"{(int)timeSpan.TotalHours}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
        else
            return $"{timeSpan.Minutes}:{timeSpan.Seconds:D2}";
    }
}

