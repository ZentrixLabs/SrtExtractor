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

    private StringBuilder _logBuilder = new();

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
            IsIndeterminate = true;
            _cancellationTokenSource = new CancellationTokenSource();

            StatusMessage = "Processing SUP file...";
            ProgressText = "Starting OCR extraction";
            AddLog($"Starting OCR processing: {Path.GetFileName(SupFilePath)}");

            var outputPath = Path.ChangeExtension(SupFilePath, ".srt");

            // Run OCR
            StatusMessage = "Performing OCR on subtitle images...";
            AddLog($"Output will be saved to: {Path.GetFileName(outputPath)}");
            AddLog($"Using language: {OcrLanguage}");
            AddLog($"Auto-correction: {(ApplyCorrection ? "Enabled" : "Disabled")}");

            await _ocrService.OcrSupToSrtAsync(
                SupFilePath,
                outputPath,
                OcrLanguage,
                fixCommonErrors: ApplyCorrection,
                removeHi: false,
                _cancellationTokenSource.Token
            );

            // Success
            StatusMessage = "OCR completed successfully!";
            ProgressText = $"Output: {Path.GetFileName(outputPath)}";
            AddLog($"✓ OCR completed successfully!");
            AddLog($"✓ Output file: {outputPath}");

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
}

