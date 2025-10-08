using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SrtExtractor.Models;
using SrtExtractor.Services.Interfaces;

namespace SrtExtractor.ViewModels;

/// <summary>
/// ViewModel for batch SRT correction functionality.
/// </summary>
public partial class BatchSrtCorrectionViewModel : ObservableObject
{
    private readonly ILoggingService _loggingService;
    private readonly INotificationService _notificationService;
    private readonly ISrtCorrectionService _srtCorrectionService;
    private readonly IAsyncFileService _asyncFileService;
    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    private string _selectedFolder = string.Empty;

    [ObservableProperty]
    private bool _includeSubfolders = true;

    [ObservableProperty]
    private bool _createBackups = true;

    [ObservableProperty]
    private ObservableCollection<SrtFileInfo> _srtFiles = new();

    partial void OnSrtFilesChanged(ObservableCollection<SrtFileInfo> value)
    {
        OnPropertyChanged(nameof(HasSrtFiles));
        OnPropertyChanged(nameof(CanStartBatch));
    }

    [ObservableProperty]
    private SrtFileInfo? _selectedSrtFile;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private string _currentFile = string.Empty;

    [ObservableProperty]
    private double _progressPercentage;

    [ObservableProperty]
    private string _progressText = string.Empty;

    public BatchSrtCorrectionViewModel(
        ILoggingService loggingService,
        INotificationService notificationService,
        ISrtCorrectionService srtCorrectionService,
        IAsyncFileService asyncFileService)
    {
        _loggingService = loggingService;
        _notificationService = notificationService;
        _srtCorrectionService = srtCorrectionService;
        _asyncFileService = asyncFileService;
    }

    // Computed properties
    public bool HasSelectedFolder => !string.IsNullOrEmpty(SelectedFolder);
    public bool HasSrtFiles => SrtFiles.Count > 0;
    public bool CanStartBatch => HasSrtFiles && !IsProcessing;
    public bool IsNotProcessing => !IsProcessing;

    [RelayCommand]
    private void SelectFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select folder containing SRT files"
        };

        if (dialog.ShowDialog() == true)
        {
            SelectedFolder = dialog.FolderName;
            SrtFiles.Clear();
            _loggingService.LogInfo($"Selected folder for batch SRT correction: {SelectedFolder}");
        }
    }

    [RelayCommand]
    private async Task ScanFolder()
    {
        if (string.IsNullOrEmpty(SelectedFolder))
            return;

        try
        {
            SrtFiles.Clear();
            _loggingService.LogInfo($"Scanning folder for SRT files: {SelectedFolder}");

            await Task.Run(() =>
            {
                var searchOption = IncludeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var srtFiles = Directory.GetFiles(SelectedFolder, "*.srt", searchOption);
                var srtFileInfos = new List<SrtFileInfo>();

                foreach (var filePath in srtFiles)
                {
                    srtFileInfos.Add(new SrtFileInfo(filePath));
                }

                // Add all files to UI on the main thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var srtFile in srtFileInfos)
                    {
                        SrtFiles.Add(srtFile);
                    }
                    _loggingService.LogInfo($"Added {srtFileInfos.Count} files to UI collection");
                });
            });

            _loggingService.LogInfo($"Found {SrtFiles.Count} SRT files in collection");
            OnPropertyChanged(nameof(HasSrtFiles));
            OnPropertyChanged(nameof(CanStartBatch));
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Error scanning folder: {SelectedFolder}", ex);
            _notificationService.ShowError($"Error scanning folder: {ex.Message}", "Error");
        }
    }

    [RelayCommand]
    private async Task StartBatch()
    {
        if (SrtFiles.Count == 0)
            return;

        try
        {
            IsProcessing = true;
            _cancellationTokenSource = new CancellationTokenSource();
            ProgressPercentage = 0;
            ProgressText = "Starting batch correction...";

            var processedCount = 0;
            var totalFiles = SrtFiles.Count;
            var totalCorrections = 0;

            _loggingService.LogInfo($"Starting batch SRT correction for {totalFiles} files");

            foreach (var srtFile in SrtFiles)
            {
                if (_cancellationTokenSource.Token.IsCancellationRequested)
                    break;

                CurrentFile = srtFile.FileName;
                srtFile.Status = "Processing...";

                try
                {
                    // Create backup if requested
                    if (CreateBackups)
                    {
                        var backupPath = srtFile.FilePath + ".backup";
                        await _asyncFileService.ReadAllTextAsync(srtFile.FilePath, _cancellationTokenSource.Token);
                        var content = await _asyncFileService.ReadAllTextAsync(srtFile.FilePath, _cancellationTokenSource.Token);
                        await _asyncFileService.WriteAllTextAsync(backupPath, content, _cancellationTokenSource.Token);
                    }

                    // Apply corrections
                    var correctionsApplied = await _srtCorrectionService.CorrectSrtFileAsync(srtFile.FilePath, _cancellationTokenSource.Token);
                    
                    srtFile.CorrectionsApplied = correctionsApplied;
                    srtFile.Status = "Completed";
                    srtFile.IsProcessed = true;
                    totalCorrections += correctionsApplied;

                    _loggingService.LogInfo($"Corrected {srtFile.FileName}: {correctionsApplied} corrections applied");
                }
                catch (Exception ex)
                {
                    srtFile.Status = "Error";
                    srtFile.ErrorMessage = ex.Message;
                    _loggingService.LogError($"Error correcting {srtFile.FileName}: {ex.Message}", ex);
                }

                processedCount++;
                ProgressPercentage = (double)processedCount / totalFiles * 100;
                ProgressText = $"Processed {processedCount} of {totalFiles} files ({totalCorrections} total corrections)";

                // Small delay to allow UI updates
                await Task.Delay(50, _cancellationTokenSource.Token);
            }

            if (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                ProgressText = $"Completed! Processed {processedCount} files with {totalCorrections} total corrections.";
                _notificationService.ShowSuccess($"Batch correction completed!\n\nProcessed: {processedCount} files\nTotal corrections: {totalCorrections}", 
                              "Batch Correction Complete");
            }
            else
            {
                ProgressText = "Cancelled by user.";
            }
        }
        catch (OperationCanceledException)
        {
            ProgressText = "Cancelled by user.";
            _loggingService.LogInfo("Batch SRT correction cancelled by user");
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Error during batch SRT correction", ex);
            _notificationService.ShowError($"Error during batch correction: {ex.Message}", "Error");
        }
        finally
        {
            IsProcessing = false;
            CurrentFile = string.Empty;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _cancellationTokenSource?.Cancel();
        _loggingService.LogInfo("User requested cancellation of batch SRT correction");
    }

    // Property change notifications for computed properties
    partial void OnSelectedFolderChanged(string value)
    {
        OnPropertyChanged(nameof(HasSelectedFolder));
    }

    partial void OnIsProcessingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanStartBatch));
        OnPropertyChanged(nameof(IsNotProcessing));
    }
}
