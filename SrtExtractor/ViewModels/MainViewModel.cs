using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SrtExtractor.Models;
using SrtExtractor.Services.Interfaces;
using SrtExtractor.State;

namespace SrtExtractor.ViewModels;

/// <summary>
/// Main ViewModel for the SrtExtractor application.
/// Handles all business logic, commands, and tool management.
/// </summary>
public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly ILoggingService _loggingService;
    private readonly INotificationService _notificationService;
    private readonly IToolDetectionService _toolDetectionService;
    private readonly IWingetService _wingetService;
    private readonly IMkvToolService _mkvToolService;
    private readonly IFfmpegService _ffmpegService;
    private readonly ISubtitleOcrService _ocrService;
    private readonly ISrtCorrectionService _srtCorrectionService;
    private readonly IMultiPassCorrectionService _multiPassCorrectionService;
    private readonly ISettingsService _settingsService;
    private readonly INetworkDetectionService _networkDetectionService;
    private readonly IRecentFilesService _recentFilesService;
    private readonly IFileCacheService _fileCacheService;
    private CancellationTokenSource? _extractionCancellationTokenSource;
    private Task? _initializationTask;

    [ObservableProperty]
    private ExtractionState _state = new();

    public MainViewModel(
        ILoggingService loggingService,
        INotificationService notificationService,
        IToolDetectionService toolDetectionService,
        IWingetService wingetService,
        IMkvToolService mkvToolService,
        IFfmpegService ffmpegService,
        ISubtitleOcrService ocrService,
        ISrtCorrectionService srtCorrectionService,
        IMultiPassCorrectionService multiPassCorrectionService,
        ISettingsService settingsService,
        INetworkDetectionService networkDetectionService,
        IRecentFilesService recentFilesService,
        IFileCacheService fileCacheService)
    {
        _loggingService = loggingService;
        _notificationService = notificationService;
        _toolDetectionService = toolDetectionService;
        _wingetService = wingetService;
        _mkvToolService = mkvToolService;
        _ffmpegService = ffmpegService;
        _ocrService = ocrService;
        _srtCorrectionService = srtCorrectionService;
        _multiPassCorrectionService = multiPassCorrectionService;
        _settingsService = settingsService;
        _networkDetectionService = networkDetectionService;
        _recentFilesService = recentFilesService;
        _fileCacheService = fileCacheService;

        // Subscribe to preference changes
        State.PreferencesChanged += OnPreferencesChanged;
        

        // Initialize commands
        PickMkvCommand = new AsyncRelayCommand(PickMkvAsync);
        ProbeCommand = new AsyncRelayCommand(async () => await ProbeTracksAsync(CancellationToken.None), () => State.CanProbe);
        ExtractCommand = new AsyncRelayCommand(async () => await ExtractSubtitlesAsync(), () => State.CanExtract);
        CancelCommand = new RelayCommand(CancelExtraction, () => State.IsProcessing);
        InstallMkvToolNixCommand = new AsyncRelayCommand(InstallMkvToolNixAsync);
        BrowseMkvToolNixCommand = new RelayCommand(BrowseMkvToolNix);
        ReDetectToolsCommand = new AsyncRelayCommand(ReDetectToolsAsync);
        CleanupTempFilesCommand = new AsyncRelayCommand(CleanupTempFilesAsync);
        CorrectSrtCommand = new AsyncRelayCommand(CorrectSrtAsync);
        
        // Batch mode commands
        ProcessBatchCommand = new AsyncRelayCommand(ProcessBatchAsync, () => State.HasBatchQueue);
        ResumeBatchCommand = new AsyncRelayCommand(ResumeBatchAsync, () => State.CanResumeBatch);
        ClearBatchQueueCommand = new RelayCommand(ClearBatchQueue);
        ClearCompletedBatchItemsCommand = new RelayCommand(ClearCompletedBatchItems);
        RemoveFromBatchCommand = new RelayCommand<BatchFile>(RemoveFromBatch);
        ProcessSingleBatchFileCommand = new AsyncRelayCommand<BatchFile>(ProcessSingleBatchFileAsync);

        // Menu commands
        ToggleBatchModeCommand = new RelayCommand(ToggleBatchMode);
        ShowHelpCommand = new RelayCommand(ShowHelp);
        OpenRecentFileCommand = new RelayCommand<string>(OpenRecentFile);

        // Subscribe to state changes to update command states
        State.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(State.CanProbe) or nameof(State.CanExtract) or nameof(State.IsProcessing))
            {
                // Use Dispatcher to ensure UI thread access (non-blocking)
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    ProbeCommand.NotifyCanExecuteChanged();
                    ExtractCommand.NotifyCanExecuteChanged();
                    CancelCommand.NotifyCanExecuteChanged();
                });
            }
            else if (e.PropertyName == nameof(State.HasBatchQueue))
            {
                // Use Dispatcher to ensure UI thread access (non-blocking)
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    ProcessBatchCommand.NotifyCanExecuteChanged();
                    ResumeBatchCommand.NotifyCanExecuteChanged();
                });
            }
            else if (e.PropertyName == nameof(State.IsBatchMode))
            {
                OnBatchModeChanged(State.IsBatchMode);
            }
            // Note: Settings are saved manually when user changes them
            // Automatic saving was removed to prevent infinite loops
        };

        // Initialize the application - track the task for proper disposal
        _initializationTask = Task.Run(async () => await InitializeAsync());
    }

    #region Commands

    public IAsyncRelayCommand PickMkvCommand { get; }
    public IAsyncRelayCommand ProbeCommand { get; }
    public IAsyncRelayCommand ExtractCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IAsyncRelayCommand InstallMkvToolNixCommand { get; }
    public IRelayCommand BrowseMkvToolNixCommand { get; }
    public IAsyncRelayCommand ReDetectToolsCommand { get; }
    public IAsyncRelayCommand CleanupTempFilesCommand { get; }
    public IAsyncRelayCommand CorrectSrtCommand { get; }
    
    // Batch mode commands
    public IAsyncRelayCommand ProcessBatchCommand { get; }
    public IAsyncRelayCommand ResumeBatchCommand { get; }
    public IRelayCommand ClearBatchQueueCommand { get; }
    public IRelayCommand ClearCompletedBatchItemsCommand { get; }
    public IRelayCommand<BatchFile> RemoveFromBatchCommand { get; }
    public IAsyncRelayCommand<BatchFile> ProcessSingleBatchFileCommand { get; }

    // Menu commands
    public IRelayCommand ToggleBatchModeCommand { get; }
    public IRelayCommand ShowHelpCommand { get; }
    public IRelayCommand<string> OpenRecentFileCommand { get; }

    #endregion

    #region Command Implementations

    private Task PickMkvAsync()
    {
        try
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select Video File",
                Filter = "Video Files (*.mkv;*.mp4)|*.mkv;*.mp4|MKV Files (*.mkv)|*.mkv|MP4 Files (*.mp4)|*.mp4|All Files (*.*)|*.*",
                DefaultExt = "mkv"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                State.MkvPath = openFileDialog.FileName;
                State.Tracks.Clear();
                State.SelectedTrack = null;
                State.HasProbedFile = false;
                // Clear the message state when selecting a new file
                State.ShowNoTracksError = false;
                
                // Update network detection
                UpdateNetworkDetection(State.MkvPath);
                
                _loggingService.LogInfo($"Selected MKV file: {State.MkvPath}");
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to pick MKV file", ex);
            _notificationService.ShowError($"Failed to select MKV file:\n{ex.Message}", "File Selection Error");
        }
        
        return Task.CompletedTask;
    }

    private async Task ProbeTracksAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(State.MkvPath))
            return;

        try
        {
            State.IsBusy = true;
            // Clear the message state when starting probe
            State.ShowNoTracksError = false;
            
            // Get file size for progress tracking
            var fileInfo = new FileInfo(State.MkvPath);
            State.StartProcessingWithProgress("Analyzing video file...", fileInfo.Length);
            State.AddLogMessage("Probing video file for subtitle tracks...");

            State.UpdateProcessingMessage("Reading subtitle tracks...");
            
            // Use appropriate service based on file extension
            ProbeResult result;
            var fileExtension = Path.GetExtension(State.MkvPath).ToLowerInvariant();
            if (fileExtension == ".mp4")
            {
                result = await _ffmpegService.ProbeAsync(State.MkvPath, cancellationToken);
            }
            else
            {
                result = await _mkvToolService.ProbeAsync(State.MkvPath, cancellationToken);
            }
            
            State.Tracks.Clear();
            
            // Auto-select best track first
            var selectedTrack = SelectBestTrack(result.Tracks);
            
            // Add tracks with recommendation flag
            foreach (var track in result.Tracks)
            {
                var isRecommended = selectedTrack != null && track.Id == selectedTrack.Id;
                var recommendedTrack = new SubtitleTrack(track.TrackId, track.Codec, track.Language, track.IsDefault, track.IsForced,
                    track.Name, track.Bitrate, track.Duration, track.Width, track.Title, track.IsSelected,
                    track.Forced, track.IsClosedCaption, isRecommended, track.TrackType, track.FrameCount, extractionId: track.ExtractionId);
                State.Tracks.Add(recommendedTrack);
            }

            State.SelectedTrack = selectedTrack;
            
            // Show message only if no tracks were found - do this AFTER all other state changes
            State.ShowNoTracksError = result.Tracks.Count == 0;

            State.UpdateProcessingMessage("Analysis completed!");
            State.AddLogMessage($"Found {result.Tracks.Count} subtitle tracks");
            
            // Mark that we've probed this file
            State.HasProbedFile = true;
            
            if (result.Tracks.Count == 0)
            {
                State.AddLogMessage("⚠️ No subtitle tracks found in this video file");
                State.AddLogMessage("💡 Try selecting a different video file or check if the file has embedded subtitles");
            }
            else if (selectedTrack != null)
            {
                var englishTracks = result.Tracks.Where(t => string.Equals(t.Language, "eng", StringComparison.OrdinalIgnoreCase)).ToList();
                if (englishTracks.Count == 1)
                {
                    State.AddLogMessage($"Auto-selected track {selectedTrack.Id}: {selectedTrack.Codec} ({selectedTrack.Language}) - Only English track available");
                }
                else
                {
                    State.AddLogMessage($"Auto-selected track {selectedTrack.Id}: {selectedTrack.Codec} ({selectedTrack.Language}) - Best of {englishTracks.Count} English tracks");
                }
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to probe video tracks", ex);
            State.AddLogMessage($"Error probing tracks: {ex.Message}");
            _notificationService.ShowError($"Failed to probe video file:\n{ex.Message}", "Probe Error");
        }
        finally
        {
            State.IsBusy = false;
            State.StopProcessingWithProgress();
        }
    }

    private void CancelExtraction()
    {
        try
        {
            _loggingService.LogInfo("User requested cancellation of extraction");
            State.AddLogMessage("Cancelling extraction...");
            
            _extractionCancellationTokenSource?.Cancel();
            
            State.StopProcessingWithProgress();
            State.AddLogMessage("Extraction cancelled by user");
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Error during cancellation", ex);
            State.AddLogMessage($"Error during cancellation: {ex.Message}");
        }
    }

    private async Task ExtractSubtitlesAsync(CancellationToken? cancellationToken = null)
    {
        if (State.SelectedTrack == null || string.IsNullOrEmpty(State.MkvPath))
            return;

        // Use provided cancellation token or create a new one
        if (cancellationToken.HasValue)
        {
            // Use the provided cancellation token (from batch processing)
            _extractionCancellationTokenSource = null;
        }
        else
        {
            // Create cancellation token source for single file extraction
            _extractionCancellationTokenSource = new CancellationTokenSource();
            cancellationToken = _extractionCancellationTokenSource.Token;
        }

        try
        {
            State.IsBusy = true;
            
            // Get file size for progress tracking
            var fileInfo = new FileInfo(State.MkvPath);
            State.StartProcessingWithProgress("Preparing extraction...", fileInfo.Length);
            State.AddLogMessage($"Extracting subtitle track {State.SelectedTrack.Id}...");

            var outputPath = State.GenerateOutputFilename(State.MkvPath, State.SelectedTrack);

            // Use appropriate service based on file extension
            var fileExtension = Path.GetExtension(State.MkvPath).ToLowerInvariant();
            if (fileExtension == ".mp4")
            {
                // Use FFmpeg for MP4 files (uses the display ID for MP4 files)
                State.UpdateProcessingMessage("Extracting subtitles with FFmpeg...");
                await _ffmpegService.ExtractSubtitleAsync(State.MkvPath, State.SelectedTrack.Id, outputPath, cancellationToken ?? CancellationToken.None);
                State.UpdateProcessingMessage("MP4 extraction completed!");
                State.AddLogMessage($"Subtitles extracted to: {outputPath}");

                // Apply multi-pass SRT corrections to MP4 subtitles
                await ApplyMultiPassCorrectionAsync(outputPath, cancellationToken ?? CancellationToken.None).ConfigureAwait(false);
            }
            else if (State.SelectedTrack.Codec.Contains("S_TEXT/UTF8") || State.SelectedTrack.Codec.Contains("SubRip/SRT"))
            {
                // Direct text extraction
                State.UpdateProcessingMessage("Extracting text subtitles...");
                
                // Simulate progress for text extraction (this is typically very fast)
                State.UpdateProgress(State.TotalBytes * 50 / 100, "Extracting text subtitles");
                await _mkvToolService.ExtractTextAsync(State.MkvPath, State.SelectedTrack.ExtractionId, outputPath, cancellationToken ?? CancellationToken.None);
                State.UpdateProgress(State.TotalBytes * 80 / 100, "Text extraction completed");
                
                State.UpdateProcessingMessage("Text extraction completed!");
                State.AddLogMessage($"Text subtitles extracted to: {outputPath}");

                // Apply multi-pass SRT corrections to text subtitles
                await ApplyMultiPassCorrectionAsync(outputPath, cancellationToken ?? CancellationToken.None).ConfigureAwait(false);
            }
            else if (State.SelectedTrack.Codec.Contains("PGS") || State.SelectedTrack.Codec.Contains("S_HDMV/PGS"))
            {
                // PGS extraction + OCR
                var tempSupPath = Path.ChangeExtension(outputPath, ".sup");
                
                State.UpdateProcessingMessage("Extracting PGS subtitles... (this can take a while, please be patient)");
                State.UpdateProgress(State.TotalBytes * 30 / 100, "Extracting PGS subtitles");
                await _mkvToolService.ExtractPgsAsync(State.MkvPath, State.SelectedTrack.ExtractionId, tempSupPath, cancellationToken ?? CancellationToken.None);
                State.AddLogMessage($"PGS subtitles extracted to: {tempSupPath}");

                State.UpdateProcessingMessage("Starting OCR conversion... (this is the slowest step, please be patient)");
                State.AddLogMessage($"Starting OCR conversion to: {outputPath}");
                State.UpdateProgress(State.TotalBytes * 50 / 100, "Starting OCR conversion");
                await _ocrService.OcrSupToSrtAsync(tempSupPath, outputPath, State.OcrLanguage, cancellationToken: cancellationToken ?? CancellationToken.None);
                State.UpdateProgress(State.TotalBytes * 90 / 100, "OCR conversion completed");
                State.UpdateProcessingMessage("OCR conversion completed!");
                State.AddLogMessage($"OCR conversion completed: {outputPath}");

                // Apply multi-pass OCR corrections
                await ApplyMultiPassCorrectionAsync(outputPath, cancellationToken ?? CancellationToken.None).ConfigureAwait(false);

                // Clean up temporary SUP file
                State.UpdateProcessingMessage("Cleaning up temporary files...");
                try
                {
                    File.Delete(tempSupPath);
                }
                catch
                {
                    // Ignore cleanup errors
                }
                State.UpdateProcessingMessage("PGS extraction completed!");
            }
            else if (State.SelectedTrack.Codec.Contains("VobSub") || State.SelectedTrack.Codec.Contains("S_VOBSUB"))
            {
                // VobSub (image-based) subtitles detected - direct users to Subtitle Edit
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
                    State.UpdateProcessingMessage("VobSub extraction cancelled - please use Subtitle Edit");
                    State.AddLogMessage("VobSub track detected. Please use Subtitle Edit's batch convert feature for OCR.");
                });
                
                throw new InvalidOperationException("VobSub subtitles require Subtitle Edit for OCR processing. See the VobSub Track Analyzer tool for help.");
            }
            else
            {
                throw new NotSupportedException($"Unsupported subtitle codec: {State.SelectedTrack.Codec}");
            }

            State.AddLogMessage("Subtitle extraction completed successfully!");
            
            // Complete progress tracking
            State.UpdateProgress(State.TotalBytes, "Extraction completed successfully");
            
            // Add to recent files
            await _recentFilesService.AddFileAsync(State.MkvPath).ConfigureAwait(false);
            await LoadRecentFilesAsync().ConfigureAwait(false); // Refresh the UI list
            
            // Only show success dialog in single file mode, not batch mode
            if (!State.IsBatchMode)
            {
                _notificationService.ShowSuccess($"Subtitles extracted successfully!\n\nOutput: {outputPath}", "Extraction Complete");
            }
        }
        catch (OperationCanceledException)
        {
            _loggingService.LogInfo("Subtitle extraction was cancelled by user");
            State.AddLogMessage("Extraction cancelled by user");
            
            // Clean up any temporary files that might have been created
            await CleanupTemporaryFiles(State.MkvPath, State.SelectedTrack);
            
            // Don't show error message for user cancellation
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to extract subtitles", ex);
            State.AddLogMessage($"Error extracting subtitles: {ex.Message}");
            
            // Clean up any temporary files that might have been created
            await CleanupTemporaryFiles(State.MkvPath, State.SelectedTrack);
            
            // Only show error dialog in single file mode, not batch mode
            if (!State.IsBatchMode)
            {
                _notificationService.ShowError($"Failed to extract subtitles:\n{ex.Message}", "Extraction Error");
            }
        }
        finally
        {
            State.IsBusy = false;
            State.StopProcessingWithProgress();
            _extractionCancellationTokenSource?.Dispose();
            _extractionCancellationTokenSource = null;
        }
    }

    private async Task InstallMkvToolNixAsync()
    {
        try
        {
            State.IsBusy = true;
            
            // Check if winget is available
            var wingetAvailable = await _wingetService.IsWingetAvailableAsync();
            if (!wingetAvailable)
            {
                State.AddLogMessage("Winget not available - showing manual installation dialog");
                // Show Windows 10 dialog
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var dialog = new Views.Windows10Dialog();
                    dialog.Owner = Application.Current.MainWindow;
                    dialog.ShowDialog();
                });
                return;
            }

            State.AddLogMessage("Installing MKVToolNix via winget...");

            var success = await _wingetService.InstallPackageAsync("MoritzBunkus.MKVToolNix");
            
            if (success)
            {
                State.AddLogMessage("MKVToolNix installed successfully!");
                // Re-detect tools
                await DetectToolsAsync();
            }
            else
            {
                State.AddLogMessage("Failed to install MKVToolNix");
                _notificationService.ShowWarning("Failed to install MKVToolNix. Please check the log for details.", "Installation Failed");
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to install MKVToolNix", ex);
            State.AddLogMessage($"Error installing MKVToolNix: {ex.Message}");
            _notificationService.ShowError($"Failed to install MKVToolNix:\n{ex.Message}", "Installation Error");
        }
        finally
        {
            State.IsBusy = false;
        }
    }

    private void BrowseMkvToolNix()
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Select mkvmerge.exe",
            Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*",
            FileName = "mkvmerge.exe"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            // Update settings and re-detect
            _ = UpdateToolPathAsync("MKVToolNix", openFileDialog.FileName);
        }
    }


    #endregion

    #region Private Methods

    private async Task InitializeAsync()
    {
        try
        {
            State.AddLogMessage("Initializing SrtExtractor...");
            
            // Load settings
            var settings = await _settingsService.LoadSettingsAsync();
                State.PreferForced = settings.PreferForced;
                State.PreferClosedCaptions = settings.PreferClosedCaptions;
                State.OcrLanguage = settings.DefaultOcrLanguage;
                State.FileNamePattern = settings.FileNamePattern;

            // Load recent files
            await LoadRecentFilesAsync();

            // Detect tools
            await DetectToolsAsync();
            
            State.AddLogMessage("Initialization completed");
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to initialize application", ex);
            State.AddLogMessage($"Initialization error: {ex.Message}");
        }
    }

    private async Task DetectToolsAsync()
    {
        State.AddLogMessage("Detecting external tools...");

        // Load current settings
        var settings = await _settingsService.LoadSettingsAsync();
        var settingsUpdated = false;

        // Detect MKVToolNix
        var mkvStatus = await _toolDetectionService.CheckMkvToolNixAsync();
        State.UpdateToolStatus("MKVToolNix", mkvStatus);
        
        // Update settings if MKVToolNix was found and not already configured
        if (mkvStatus.IsInstalled && mkvStatus.Path != null && string.IsNullOrEmpty(settings.MkvMergePath))
        {
            var mkvDir = Path.GetDirectoryName(mkvStatus.Path);
            settings = settings with 
            { 
                MkvMergePath = mkvStatus.Path,
                MkvExtractPath = Path.Combine(mkvDir ?? "", "mkvextract.exe")
            };
            settingsUpdated = true;
        }

        // Detect Subtitle Edit
        var seStatus = await _toolDetectionService.CheckSubtitleEditAsync();
        State.UpdateToolStatus("SubtitleEdit", seStatus);
        
        // Update settings if Subtitle Edit was found and not already configured
        if (seStatus.IsInstalled && seStatus.Path != null && string.IsNullOrEmpty(settings.SubtitleEditPath))
        {
            settings = settings with { SubtitleEditPath = seStatus.Path };
            settingsUpdated = true;
        }

        // Detect FFmpeg
        var ffmpegStatus = await _toolDetectionService.CheckFfmpegAsync();
        State.UpdateToolStatus("FFmpeg", ffmpegStatus);

        // Save updated settings if any tool paths were detected
        if (settingsUpdated)
        {
            await _settingsService.SaveSettingsAsync(settings);
            _loggingService.LogInfo("Updated settings with detected tool paths");
        }

        if (State.AreToolsAvailable)
        {
            State.AddLogMessage("All required tools are available");
        }
        else
        {
            State.AddLogMessage("Some required tools are missing. Please configure them in Settings.");
            
            // Show a message directing users to settings (fire and forget)
            _ = Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                var result = await _notificationService.ShowConfirmationAsync(
                    "Some required tools are missing and need to be configured.\n\nWould you like to open Settings to configure the tools now?",
                    "Tools Missing");
                
                if (result)
                {
                    // Trigger the event to open settings immediately
                    State.TriggerOpenSettings();
                }
            });
        }
    }

    private async Task UpdateToolPathAsync(string toolName, string toolPath)
    {
        try
        {
            var settings = await _settingsService.LoadSettingsAsync();
            
            if (string.Equals(toolName, "MKVToolNix", StringComparison.OrdinalIgnoreCase))
            {
                var mkvDir = Path.GetDirectoryName(toolPath);
                settings = settings with 
                { 
                    MkvMergePath = toolPath,
                    MkvExtractPath = Path.Combine(mkvDir ?? "", "mkvextract.exe")
                };
            }
            else if (string.Equals(toolName, "SubtitleEdit", StringComparison.OrdinalIgnoreCase))
            {
                settings = settings with { SubtitleEditPath = toolPath };
            }

            await _settingsService.SaveSettingsAsync(settings);
            State.AddLogMessage($"Updated {toolName} path: {toolPath}");
            
            // Re-detect tools
            await DetectToolsAsync();
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Failed to update {toolName} path", ex);
            State.AddLogMessage($"Error updating {toolName} path: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles preference changes and updates recommendations.
    /// </summary>
    private void OnPreferencesChanged(object? sender, EventArgs e)
    {
        // Only update if we have tracks loaded
        if (State.Tracks.Any())
        {
            UpdateRecommendationAndSelection();
        }
    }

    /// <summary>
    /// Updates the recommendation and selection when user preferences change.
    /// </summary>
    public void UpdateRecommendationAndSelection()
    {
        if (!State.Tracks.Any())
            return;

        // Get the original tracks without recommendation flags
        var originalTracks = State.Tracks.Select(t => new SubtitleTrack(t.TrackId, t.Codec, t.Language, t.IsDefault, t.IsForced,
            t.Name, t.Bitrate, t.Duration, t.Width, t.Title, t.IsSelected,
            t.Forced, t.IsClosedCaption, false, t.TrackType, t.FrameCount, extractionId: t.ExtractionId)).ToList();
        
        // Re-select best track based on current preferences
        var selectedTrack = SelectBestTrack(originalTracks);
        
        // Update all tracks with new recommendation flags
        State.Tracks.Clear();
        foreach (var track in originalTracks)
        {
            var isRecommended = selectedTrack != null && track.Id == selectedTrack.Id;
            var recommendedTrack = new SubtitleTrack(track.TrackId, track.Codec, track.Language, track.IsDefault, track.IsForced,
                track.Name, track.Bitrate, track.Duration, track.Width, track.Title, track.IsSelected,
                track.Forced, track.IsClosedCaption, isRecommended, track.TrackType, track.FrameCount, extractionId: track.ExtractionId);
            State.Tracks.Add(recommendedTrack);
        }

        // Update selection
        State.SelectedTrack = selectedTrack;
        
        if (selectedTrack != null)
        {
            var availableTracks = originalTracks.Where(t => string.Equals(t.Language, "eng", StringComparison.OrdinalIgnoreCase)).ToList();
            if (availableTracks.Count == 1)
            {
                State.AddLogMessage($"Updated recommendation: Track {selectedTrack.Id} ({selectedTrack.Codec}, {selectedTrack.Language}) - Only English track available");
            }
            else
            {
                State.AddLogMessage($"Updated recommendation: Track {selectedTrack.Id} ({selectedTrack.Codec}, {selectedTrack.Language}) - Best of {availableTracks.Count} English tracks");
            }
        }
    }

    private SubtitleTrack? SelectBestTrack(IReadOnlyList<SubtitleTrack> tracks)
    {
        if (!tracks.Any())
            return null;

        var preferredLanguage = "eng"; // Could be made configurable

        // Enhanced priority order based on user preferences and track characteristics:
        // If PreferClosedCaptions: CC tracks first, then by type and quality
        // If PreferForced: Forced tracks first, then by type and quality
        // If neither: Full tracks first, then by quality

        // Filter tracks by preferred language
        var languageTracks = tracks.Where(t => 
            string.Equals(t.Language, preferredLanguage, StringComparison.OrdinalIgnoreCase)).ToList();

        if (!languageTracks.Any())
        {
            // If no tracks in preferred language, return the first track
            return tracks.First();
        }

        if (State.PreferClosedCaptions)
        {
            // Prefer CC tracks first, then by type and quality
            var ccTracks = languageTracks.Where(t => t.IsClosedCaption).ToList();
            if (ccTracks.Any())
            {
                // Prefer forced CC, then regular CC, then by quality
                var forcedCC = ccTracks.FirstOrDefault(t => t.TrackType == "CC Forced" || t.Forced);
                if (forcedCC != null) return forcedCC;

                var regularCC = ccTracks.FirstOrDefault(t => t.TrackType == "CC");
                if (regularCC != null) return regularCC;

                // Return highest quality CC track
                return GetBestQualityTrack(ccTracks);
            }
        }
        else if (State.PreferForced)
        {
            // Prefer forced tracks first, then by type and quality
            var forcedTracks = languageTracks.Where(t => t.TrackType == "Forced" || t.TrackType == "CC Forced" || t.Forced).ToList();
            if (forcedTracks.Any())
            {
                // Return highest quality forced track
                return GetBestQualityTrack(forcedTracks);
            }
        }

        // Default: Prefer SubRip/SRT tracks first, then Full tracks, then by quality
        var subripTracks = languageTracks.Where(t => IsSubRipTrack(t.Codec)).ToList();
        if (subripTracks.Any())
        {
            _loggingService.LogInfo($"Found {subripTracks.Count} SubRip/SRT tracks, prioritizing over HDMV PGS");
            return GetBestQualityTrack(subripTracks);
        }

        // If no SubRip/SRT, prefer Full tracks, then by quality
        var fullTracks = languageTracks.Where(t => t.TrackType == "Full").ToList();
        if (fullTracks.Any())
        {
            return GetBestQualityTrack(fullTracks);
        }

        // Fallback: Return highest quality track in preferred language
        return GetBestQualityTrack(languageTracks);
    }

    /// <summary>
    /// Select the best quality track from a list of tracks based on bitrate, frame count, and codec type.
    /// </summary>
    /// <param name="tracks">List of tracks to choose from</param>
    /// <returns>Best quality track</returns>
    private SubtitleTrack GetBestQualityTrack(IList<SubtitleTrack> tracks)
    {
        if (!tracks.Any()) return tracks.First();

        // Priority order: SubRip/SRT > Other text-based > HDMV PGS > Other PGS
        var subripTracks = tracks.Where(t => IsSubRipTrack(t.Codec)).ToList();
        if (subripTracks.Any())
        {
            _loggingService.LogInfo($"Selecting from {subripTracks.Count} SubRip/SRT tracks (highest priority)");
            tracks = subripTracks;
        }
        else
        {
            // If no SubRip/SRT, prefer other text-based subtitles over PGS
            var textTracks = tracks.Where(t => IsTextBasedTrack(t.Codec)).ToList();
            if (textTracks.Any())
            {
                _loggingService.LogInfo($"No SubRip/SRT found, selecting from {textTracks.Count} text-based tracks");
                tracks = textTracks;
            }
            else
            {
                _loggingService.LogInfo($"No text-based tracks found, selecting from {tracks.Count} available tracks (including PGS)");
            }
        }

        // Sort by quality metrics: bitrate (desc), frame count (desc), then by codec preference
        return tracks.OrderByDescending(t => t.Bitrate)
                     .ThenByDescending(t => t.FrameCount)
                     .ThenByDescending(t => GetCodecPriority(t.Codec))
                     .First();
    }

    /// <summary>
    /// Check if a codec is SubRip/SRT format.
    /// </summary>
    /// <param name="codec">Codec string to check</param>
    /// <returns>True if SubRip/SRT</returns>
    private static bool IsSubRipTrack(string codec)
    {
        return codec.Contains("S_TEXT/UTF8") || 
               codec.Contains("SubRip/SRT") || 
               codec.Contains("subrip") ||
               codec.Contains("srt");
    }

    /// <summary>
    /// Check if a codec is text-based (not PGS).
    /// </summary>
    /// <param name="codec">Codec string to check</param>
    /// <returns>True if text-based</returns>
    private static bool IsTextBasedTrack(string codec)
    {
        return codec.Contains("S_TEXT") || 
               codec.Contains("ASS") || 
               codec.Contains("SSA") || 
               codec.Contains("VTT");
    }

    /// <summary>
    /// Get codec priority for sorting (higher number = higher priority).
    /// </summary>
    /// <param name="codec">Codec string</param>
    /// <returns>Priority value</returns>
    private static int GetCodecPriority(string codec)
    {
        if (IsSubRipTrack(codec)) return 100;  // Highest priority
        if (IsTextBasedTrack(codec)) return 50; // Medium priority
        if (codec.Contains("PGS") || codec.Contains("S_HDMV/PGS")) return 10; // Lower priority
        return 0; // Lowest priority
    }

    /// <summary>
    /// Test method to verify recommendation logic prioritizes SubRip/SRT over HDMV PGS.
    /// This method can be called for testing purposes.
    /// </summary>
    public void TestRecommendationLogic()
    {
        var testTracks = new List<SubtitleTrack>
        {
            new SubtitleTrack(1, "S_HDMV/PGS", "eng", false, false, "HDMV PGS Full", 50000, 2000, 7200, "Full", false),
            new SubtitleTrack(2, "S_TEXT/UTF8", "eng", false, false, "SubRip/SRT Full", 1000, 1500, 7200, "Full", false),
            new SubtitleTrack(3, "S_HDMV/PGS", "eng", false, false, "HDMV PGS Forced", 5000, 100, 7200, "Forced", false),
            new SubtitleTrack(4, "S_TEXT/ASS", "eng", false, false, "ASS Full", 2000, 1800, 7200, "Full", false)
        };

        var recommended = SelectBestTrack(testTracks);
        
        _loggingService.LogInfo($"Test Recommendation Result:");
        _loggingService.LogInfo($"  Recommended Track: {recommended?.Id} - {recommended?.Codec} - {recommended?.Name}");
        _loggingService.LogInfo($"  Expected: Track 2 (SubRip/SRT) should be recommended over HDMV PGS");
        
        if (recommended?.Codec.Contains("S_TEXT/UTF8") == true)
        {
            _loggingService.LogInfo("  ✅ Test PASSED: SubRip/SRT correctly prioritized over HDMV PGS");
        }
        else
        {
            _loggingService.LogError($"  ❌ Test FAILED: Expected SubRip/SRT, got {recommended?.Codec}");
        }
    }

    private async Task ReDetectToolsAsync()
    {
        try
        {
            State.IsBusy = true;
            State.AddLogMessage("Re-detecting tools...");
            
            await DetectToolsAsync();
            
            State.AddLogMessage("Tool detection completed!");
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to re-detect tools", ex);
            State.AddLogMessage($"Error re-detecting tools: {ex.Message}");
        }
        finally
        {
            State.IsBusy = false;
        }
    }

    private async Task CleanupTempFilesAsync()
    {
        try
        {
            State.IsBusy = true;
            State.UpdateProcessingMessage("Cleaning up temporary files...");
            State.AddLogMessage("🧹 Cleaning up temporary files...");

            // Clean up VobSub temp directories
            await CleanupVobSubTempDirectories();

            // Clean up any other temp files if needed
            if (State.SelectedTrack != null && !string.IsNullOrEmpty(State.MkvPath))
            {
                await CleanupTemporaryFiles(State.MkvPath, State.SelectedTrack);
            }

            State.UpdateProcessingMessage("Cleanup completed!");
            State.AddLogMessage("✅ Temporary files cleaned up successfully");
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to clean up temporary files", ex);
            State.AddLogMessage($"❌ Failed to clean up temporary files: {ex.Message}");
        }
        finally
        {
            State.IsBusy = false;
        }
    }

    private async Task CorrectSrtAsync()
    {
        try
        {
            State.IsBusy = true;
            
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select SRT file to correct",
                Filter = "SRT Files (*.srt)|*.srt|All Files (*.*)|*.*",
                FileName = "*.srt"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                State.AddLogMessage($"Correcting OCR errors in: {openFileDialog.FileName}");
                // Don't modify ProcessingMessage as this is a separate operation that can run concurrently
                
                var correctionCount = await _srtCorrectionService.CorrectSrtFileAsync(openFileDialog.FileName);
                
                State.AddLogMessage($"🎯 SRT correction completed successfully! Applied {correctionCount} corrections.");
                _notificationService.ShowSuccess("SRT file has been corrected successfully!", "Correction Complete");
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to correct SRT file", ex);
            State.AddLogMessage($"Error correcting SRT file: {ex.Message}");
            _notificationService.ShowError($"Failed to correct SRT file:\n{ex.Message}", "Correction Error");
        }
        finally
        {
            State.IsBusy = false;
            // Don't clear ProcessingMessage as it belongs to the main extraction process
        }
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            // Load existing settings to preserve ShowWelcomeScreen preference
            var existingSettings = await _settingsService.LoadSettingsAsync();
            
            var settings = new AppSettings(
                MkvMergePath: null, // These will be updated when tools are detected
                MkvExtractPath: null,
                SubtitleEditPath: null,
                TesseractDataPath: null,
                AutoDetectTools: true,
                LastToolCheck: DateTime.Now,
                    PreferForced: State.PreferForced,
                    PreferClosedCaptions: State.PreferClosedCaptions,
                    DefaultOcrLanguage: State.OcrLanguage,
                    FileNamePattern: State.FileNamePattern,
                    ShowWelcomeScreen: existingSettings.ShowWelcomeScreen
            );

            await _settingsService.SaveSettingsAsync(settings);
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to save settings", ex);
        }
    }

    /// <summary>
    /// Updates network detection for the given file path.
    /// </summary>
    /// <param name="filePath">The file path to analyze</param>
    private void UpdateNetworkDetection(string filePath)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath))
            {
                State.UpdateNetworkDetection(false, 0, "", "");
                return;
            }

            var isNetwork = _networkDetectionService.IsNetworkPath(filePath);
            var estimatedMinutes = _networkDetectionService.GetEstimatedProcessingTime(filePath);
            var formattedSize = _networkDetectionService.GetFormattedFileSize(filePath);
            
            // Get network drive info
            var networkDriveInfo = "";
            if (isNetwork)
            {
                var root = Path.GetPathRoot(filePath);
                if (!string.IsNullOrEmpty(root))
                {
                    networkDriveInfo = root.TrimEnd('\\');
                }
            }

            State.UpdateNetworkDetection(isNetwork, estimatedMinutes, formattedSize, networkDriveInfo);
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to update network detection", ex);
        }
    }

    /// <summary>
    /// Clean up temporary files that might have been created during extraction.
    /// This is called automatically after each extraction to ensure no temp files are left behind.
    /// </summary>
    private async Task CleanupTemporaryFiles(string mkvPath, SubtitleTrack? selectedTrack)
    {
        if (selectedTrack == null || string.IsNullOrEmpty(mkvPath))
            return;

        try
        {
            var outputPath = State.GenerateOutputFilename(mkvPath, selectedTrack);
            
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
            // Check if this was a VobSub extraction (which creates temporary .idx/.sub files)
            else if (selectedTrack.Codec.Contains("VobSub") || selectedTrack.Codec.Contains("S_VOBSUB"))
            {
                // Clean up VobSub temporary directories
                await CleanupVobSubTempDirectories();
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to clean up temporary files", ex);
            // Don't show this to the user as it's automatic cleanup - just log it
        }
    }

    /// <summary>
    /// Cleans up VobSub temporary directories created during extraction.
    /// </summary>
    private async Task CleanupVobSubTempDirectories()
    {
        try
        {
            var srtExtractorTempDir = Path.Combine(Path.GetTempPath(), "SrtExtractor");
            
            if (!Directory.Exists(srtExtractorTempDir))
            {
                _loggingService.LogInfo("No SrtExtractor temp directory found to clean up");
                return;
            }

            _loggingService.LogInfo($"Cleaning up VobSub temporary directories in: {srtExtractorTempDir}");

            // Get all subdirectories (each extraction creates a GUID-named directory)
            var tempDirectories = Directory.GetDirectories(srtExtractorTempDir);
            var cleanedCount = 0;

            foreach (var tempDir in tempDirectories)
            {
                try
                {
                    // Check if this directory contains VobSub files (.idx/.sub)
                    var idxFiles = Directory.GetFiles(tempDir, "*.idx");
                    var subFiles = Directory.GetFiles(tempDir, "*.sub");
                    
                    if (idxFiles.Length > 0 || subFiles.Length > 0)
                    {
                        _loggingService.LogInfo($"Cleaning up VobSub temp directory: {Path.GetFileName(tempDir)}");
                        
                        // Give processes time to release file handles
                        await Task.Delay(1000);
                        
                        // Delete the entire directory
                        Directory.Delete(tempDir, true);
                        cleanedCount++;
                    }
                }
                catch (IOException ex)
                {
                    _loggingService.LogWarning($"Could not clean up temp directory {Path.GetFileName(tempDir)}: {ex.Message}");
                }
                catch (UnauthorizedAccessException ex)
                {
                    _loggingService.LogWarning($"Access denied cleaning up temp directory {Path.GetFileName(tempDir)}: {ex.Message}");
                }
            }

            if (cleanedCount > 0)
            {
                _loggingService.LogInfo($"Successfully cleaned up {cleanedCount} VobSub temporary directories");
            }
            else
            {
                _loggingService.LogInfo("No VobSub temporary directories found to clean up");
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to clean up VobSub temporary directories", ex);
        }
    }


    /// <summary>
    /// Process all files in the batch queue.
    /// </summary>
    private async Task ProcessBatchAsync()
    {
        if (!State.BatchQueue.Any())
            return;

        await ProcessBatchFromIndexAsync(0);
    }

    /// <summary>
    /// Process batch files starting from a specific index.
    /// </summary>
    /// <param name="startIndex">The index to start processing from</param>
    private async Task ProcessBatchFromIndexAsync(int startIndex)
    {
        if (!State.BatchQueue.Any() || startIndex >= State.BatchQueue.Count)
            return;

        try
        {
            State.IsBusy = true;
            State.StartProcessing("Starting batch processing...");
            State.AddLogMessage($"Starting batch processing of {State.BatchQueue.Count} files from index {startIndex}");

            var totalFiles = State.BatchQueue.Count;
            var processedCount = startIndex;
            var successCount = State.BatchQueue.Take(startIndex).Count(f => f.Status == BatchFileStatus.Completed);
            var errorCount = State.BatchQueue.Take(startIndex).Count(f => f.Status == BatchFileStatus.Error);

            // Reset the last processed index
            State.LastProcessedBatchIndex = startIndex - 1;

            foreach (var batchFile in State.BatchQueue.Skip(startIndex).ToList())
            {
                // Check for cancellation before processing each file
                if (_extractionCancellationTokenSource?.Token.IsCancellationRequested == true)
                {
                    State.AddLogMessage("Batch processing cancelled by user");
                    break;
                }

                try
                {
                    State.UpdateBatchProgress(processedCount, totalFiles, $"Processing {batchFile.FileName}...");
                    batchFile.Status = BatchFileStatus.Processing;
                    batchFile.StatusMessage = "Processing...";

                    // Set the current file as the active file for processing
                    State.MkvPath = batchFile.FilePath;
                    State.Tracks.Clear();
                    State.SelectedTrack = null;

                    // Update network detection for this file
                    UpdateNetworkDetection(batchFile.FilePath);
                    batchFile.UpdateNetworkStatus(State.IsNetworkFile, State.EstimatedProcessingTimeMinutes);

                    // Probe tracks
                    await ProbeTracksAsync();
                    
                    if (State.SelectedTrack == null)
                    {
                        throw new InvalidOperationException("No suitable track found for extraction");
                    }

                    // Extract subtitles with cancellation token
                    await ExtractSubtitlesAsync(_extractionCancellationTokenSource?.Token);

                    // Check for cancellation after extraction
                    if (_extractionCancellationTokenSource?.Token.IsCancellationRequested == true)
                    {
                        batchFile.Status = BatchFileStatus.Cancelled;
                        batchFile.StatusMessage = "Cancelled";
                        State.AddLogMessage($"⏹️ Cancelled: {batchFile.FileName}");
                        break; // Exit the loop when cancelled
                    }

                    // Only increment success count after extraction is actually complete
                    batchFile.Status = BatchFileStatus.Completed;
                    batchFile.StatusMessage = "Completed successfully";
                    batchFile.OutputPath = State.GenerateOutputFilename(batchFile.FilePath, State.SelectedTrack);
                    
                    successCount++;
                    State.AddLogMessage($"✅ Completed: {batchFile.FileName}");
                    
                    // Use fast statistics update during batch processing to reduce UI overhead
                    State.UpdateBatchStatisticsFast();
                }
                catch (OperationCanceledException)
                {
                    batchFile.Status = BatchFileStatus.Cancelled;
                    batchFile.StatusMessage = "Cancelled";
                    State.AddLogMessage($"⏹️ Cancelled: {batchFile.FileName}");
                    
                    // Clean up any temporary files that might have been created
                    await CleanupTemporaryFiles(batchFile.FilePath, State.SelectedTrack);
                    
                    break; // Exit the loop when cancelled
                }
                catch (Exception ex)
                {
                    batchFile.Status = BatchFileStatus.Error;
                    batchFile.StatusMessage = $"Error: {ex.Message}";
                    errorCount++;
                    
                    _loggingService.LogError($"Failed to process batch file {batchFile.FileName}", ex);
                    State.AddLogMessage($"❌ Failed: {batchFile.FileName} - {ex.Message}");
                    
                    // Use fast statistics update during batch processing to reduce UI overhead
                    State.UpdateBatchStatisticsFast();
                }

                processedCount++;
                State.LastProcessedBatchIndex = processedCount - 1;
            }

            State.StopProcessingWithProgress();
            
            // Final statistics update with full UI notifications after batch processing completes
            State.UpdateBatchStatistics();
            
            // Create detailed summary
            var successfulFiles = State.BatchQueue.Where(f => f.Status == BatchFileStatus.Completed).ToList();
            var errorFiles = State.BatchQueue.Where(f => f.Status == BatchFileStatus.Error).ToList();
            var cancelledFiles = State.BatchQueue.Where(f => f.Status == BatchFileStatus.Cancelled).ToList();
            
            // Log detailed summary
            State.AddLogMessage($"🎯 Batch processing completed! Success: {successCount}, Errors: {errorCount}, Cancelled: {cancelledFiles.Count}");
            
            if (successfulFiles.Any())
            {
                State.AddLogMessage("✅ Successful files:");
                foreach (var file in successfulFiles)
                {
                    State.AddLogMessage($"   • {file.FileName}");
                }
            }
            
            if (errorFiles.Any())
            {
                State.AddLogMessage("❌ Failed files:");
                foreach (var file in errorFiles)
                {
                    State.AddLogMessage($"   • {file.FileName} - {file.StatusMessage}");
                }
            }
            
            if (cancelledFiles.Any())
            {
                State.AddLogMessage("⏹️ Cancelled files:");
                foreach (var file in cancelledFiles)
                {
                    State.AddLogMessage($"   • {file.FileName}");
                }
            }

            // Create detailed message box
            var message = $"Batch processing completed!\n\n" +
                         $"Total files: {totalFiles}\n" +
                         $"Successful: {successCount}\n" +
                         $"Errors: {errorCount}\n" +
                         $"Cancelled: {cancelledFiles.Count}\n\n";
            
            if (successfulFiles.Any())
            {
                message += "✅ Successful files:\n";
                foreach (var file in successfulFiles)
                {
                    message += $"   • {file.FileName}\n";
                }
                message += "\n";
            }
            
            if (errorFiles.Any())
            {
                message += "❌ Failed files:\n";
                foreach (var file in errorFiles)
                {
                    message += $"   • {file.FileName} - {file.StatusMessage}\n";
                }
                message += "\n";
            }
            
            if (cancelledFiles.Any())
            {
                message += "⏹️ Cancelled files:\n";
                foreach (var file in cancelledFiles)
                {
                    message += $"   • {file.FileName}\n";
                }
            }

            if (errorCount > 0)
            {
                _notificationService.ShowWarning(message, "Batch Processing Complete");
            }
            else
            {
                _notificationService.ShowInfo(message, "Batch Processing Complete");
            }
            
            // Clear the batch queue and reset progress after completion
            State.ClearBatchQueue();
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Batch processing failed", ex);
            State.AddLogMessage($"Batch processing failed: {ex.Message}");
            _notificationService.ShowError($"Batch processing failed:\n{ex.Message}", "Batch Processing Error");
        }
        finally
        {
            State.IsBusy = false;
            State.StopProcessingWithProgress();
        }
    }

    /// <summary>
    /// Clear the batch queue.
    /// </summary>
    private void ClearBatchQueue()
    {
        try
        {
            State.ClearBatchQueue();
            State.AddLogMessage("Batch queue cleared");
            _loggingService.LogInfo("User cleared batch queue");
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to clear batch queue", ex);
        }
    }

    /// <summary>
    /// Clear only completed items from the batch queue.
    /// </summary>
    private void ClearCompletedBatchItems()
    {
        try
        {
            var completedCount = State.BatchCompletedCount;
            State.ClearCompletedBatchItems();
            State.AddLogMessage($"Cleared {completedCount} completed items from batch queue");
            _loggingService.LogInfo($"User cleared {completedCount} completed batch items");
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to clear completed batch items", ex);
        }
    }

    /// <summary>
    /// Resume batch processing from where it was interrupted.
    /// </summary>
    private async Task ResumeBatchAsync()
    {
        try
        {
            if (!State.AreToolsAvailable)
            {
                _notificationService.ShowWarning("Required tools are not available. Please install MKVToolNix and Subtitle Edit first.", "Tools Required");
                return;
            }

            var startIndex = State.LastProcessedBatchIndex + 1;
            var remainingFiles = State.BatchQueue.Skip(startIndex).ToList();
            
            if (!remainingFiles.Any())
            {
                State.AddLogMessage("No remaining files to process in batch queue");
                return;
            }

            State.AddLogMessage($"Resuming batch processing from file {startIndex + 1} of {State.BatchQueue.Count}");
            _loggingService.LogInfo($"User resumed batch processing from index {startIndex}");

            // Start processing from the next unprocessed file
            await ProcessBatchFromIndexAsync(startIndex);
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to resume batch processing", ex);
            State.AddLogMessage($"Failed to resume batch processing: {ex.Message}");
            _notificationService.ShowError($"Failed to resume batch processing:\n{ex.Message}", "Resume Error");
        }
    }

    /// <summary>
    /// Remove a file from the batch queue.
    /// </summary>
    /// <param name="batchFile">The batch file to remove</param>
    private void RemoveFromBatch(BatchFile? batchFile)
    {
        try
        {
            if (batchFile != null)
            {
                State.RemoveFromBatchQueue(batchFile);
                State.AddLogMessage($"Removed from queue: {batchFile.FileName}");
                _loggingService.LogInfo($"User removed file from batch queue: {batchFile.FileName}");
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to remove file from batch queue", ex);
        }
    }

    /// <summary>
    /// Add files to batch queue from drag and drop.
    /// Uses bulk add to prevent excessive UI updates.
    /// </summary>
    /// <param name="filePaths">Array of file paths to add</param>
    public void AddFilesToBatchQueue(string[] filePaths)
    {
        try
        {
            var addedCount = 0;
            var skippedCount = 0;
            var filesToAdd = new List<BatchFile>();

            // First pass: validate and prepare files without adding to ObservableCollection
            foreach (var filePath in filePaths)
            {
                // Check for duplicates
                if (string.IsNullOrEmpty(filePath) || 
                    State.BatchQueue.Any(f => f.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
                {
                    skippedCount++;
                    continue;
                }

                var batchFile = new BatchFile { FilePath = filePath };
                batchFile.UpdateFromFileSystem(_fileCacheService);
                
                // Update network detection
                var isNetwork = _networkDetectionService.IsNetworkPath(filePath);
                var estimatedMinutes = _networkDetectionService.GetEstimatedProcessingTime(filePath);
                batchFile.UpdateNetworkStatus(isNetwork, estimatedMinutes);
                
                filesToAdd.Add(batchFile);
                addedCount++;
            }

            // Second pass: bulk add to ObservableCollection (single UI update per file, but better than alternative)
            // Note: WPF doesn't support true bulk operations on ObservableCollection,
            // but this pattern is still better than State.AddToBatchQueue which does more work per item
            foreach (var file in filesToAdd)
            {
                State.BatchQueue.Add(file);
            }

            // Update statistics once at the end
            if (filesToAdd.Any())
            {
                State.TotalBatchFiles = State.BatchQueue.Count;
                State.UpdateBatchStatistics();
                // Note: UpdateBatchStatistics() already calls OnPropertyChanged for computed properties
            }

            if (addedCount > 0)
            {
                State.AddLogMessage($"Added {addedCount} files to batch queue");
                _loggingService.LogInfo($"Added {addedCount} files to batch queue via drag and drop");
            }

            if (skippedCount > 0)
            {
                State.AddLogMessage($"Skipped {skippedCount} duplicate files");
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to add files to batch queue", ex);
            State.AddLogMessage($"Error adding files to batch queue: {ex.Message}");
        }
    }

    /// <summary>
    /// Move a batch item to the top of the queue.
    /// </summary>
    /// <param name="batchFile">The batch file to move</param>
    public void MoveBatchItemToTop(BatchFile batchFile)
    {
        try
        {
            if (State.BatchQueue.Contains(batchFile))
            {
                State.BatchQueue.Remove(batchFile);
                State.BatchQueue.Insert(0, batchFile);
                State.AddLogMessage($"Moved to top of queue: {batchFile.FileName}");
                _loggingService.LogInfo($"User moved batch item to top: {batchFile.FileName}");
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Error moving batch item to top: {batchFile.FileName}", ex);
        }
    }

    /// <summary>
    /// Move a batch item to the bottom of the queue.
    /// </summary>
    /// <param name="batchFile">The batch file to move</param>
    public void MoveBatchItemToBottom(BatchFile batchFile)
    {
        try
        {
            if (State.BatchQueue.Contains(batchFile))
            {
                State.BatchQueue.Remove(batchFile);
                State.BatchQueue.Add(batchFile);
                State.AddLogMessage($"Moved to bottom of queue: {batchFile.FileName}");
                _loggingService.LogInfo($"User moved batch item to bottom: {batchFile.FileName}");
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Error moving batch item to bottom: {batchFile.FileName}", ex);
        }
    }

    /// <summary>
    /// Reorder batch queue items by dragging and dropping.
    /// </summary>
    /// <param name="draggedItem">The item being dragged</param>
    /// <param name="targetItem">The item being dropped on</param>
    public void ReorderBatchQueue(BatchFile draggedItem, BatchFile targetItem)
    {
        try
        {
            if (State.BatchQueue.Contains(draggedItem) && State.BatchQueue.Contains(targetItem) && draggedItem != targetItem)
            {
                var draggedIndex = State.BatchQueue.IndexOf(draggedItem);
                var targetIndex = State.BatchQueue.IndexOf(targetItem);
                
                State.BatchQueue.RemoveAt(draggedIndex);
                
                // Adjust target index if we removed an item before it
                if (draggedIndex < targetIndex)
                {
                    targetIndex--;
                }
                
                State.BatchQueue.Insert(targetIndex, draggedItem);
                
                State.AddLogMessage($"Reordered queue: {draggedItem.FileName} moved to position {targetIndex + 1}");
                _loggingService.LogInfo($"User reordered batch queue: {draggedItem.FileName} moved to position {targetIndex + 1}");
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Error reordering batch queue: {draggedItem.FileName}", ex);
        }
    }

    /// <summary>
    /// Process a single file from the batch queue.
    /// </summary>
    /// <param name="batchFile">The batch file to process</param>
    public async Task ProcessSingleBatchFileAsync(BatchFile? batchFile)
    {
        if (batchFile == null)
        {
            _loggingService.LogWarning("ProcessSingleBatchFileAsync called with null batchFile");
            return;
        }

        try
        {
            if (!State.AreToolsAvailable)
            {
                _notificationService.ShowWarning("Required tools are not available. Please install MKVToolNix and Subtitle Edit first.", "Tools Required");
                return;
            }

            State.AddLogMessage($"Processing single file: {batchFile.FileName}");
            _loggingService.LogInfo($"User requested single file processing: {batchFile.FilePath}");

            // Set the current file path and probe tracks
            State.MkvPath = batchFile.FilePath;
            await ProbeTracksAsync(CancellationToken.None);

            // If we found tracks, extract the best one
            if (State.SelectedTrack != null)
            {
                await ExtractSubtitlesAsync(CancellationToken.None);
                
                // Mark this batch item as completed
                batchFile.Status = BatchFileStatus.Completed;
                batchFile.StatusMessage = "Processed successfully";
                
                State.AddLogMessage($"✅ Single file processing completed: {batchFile.FileName}");
            }
            else
            {
                batchFile.Status = BatchFileStatus.Error;
                batchFile.StatusMessage = "No suitable tracks found";
                State.AddLogMessage($"❌ No suitable tracks found in: {batchFile.FileName}");
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Error processing single batch file: {batchFile.FileName}", ex);
            batchFile.Status = BatchFileStatus.Error;
            batchFile.StatusMessage = ex.Message;
            State.AddLogMessage($"❌ Error processing {batchFile.FileName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles batch mode changes and provides user feedback.
    /// </summary>
    /// <param name="isBatchMode">Whether batch mode is enabled</param>
    private void OnBatchModeChanged(bool isBatchMode)
    {
        try
        {
            // Update queue column width
            State.QueueColumnWidth = isBatchMode ? 350 : 0;
            
            if (isBatchMode)
            {
                // Show confirmation dialog about using preferred settings
                var preferenceText = State.PreferForced ? "forced subtitles" : 
                                   State.PreferClosedCaptions ? "closed captions" : "full subtitles";
                
                var message = $"🎬 Batch Mode will use your preferred settings:\n\n" +
                             $"• Subtitle preference: {preferenceText}\n" +
                             $"• OCR language: {State.OcrLanguage}\n" +
                             $"• File pattern: {State.FileNamePattern}\n\n" +
                             $"All files in the batch will be processed with these settings.\n\n" +
                             $"Do you want to continue?";
                
                _notificationService.ShowConfirmation(
                    message,
                    "Batch Mode Settings Confirmation",
                    () => { /* User confirmed, continue */ },
                    () => { 
                        // User declined, disable batch mode
                        State.IsBatchMode = false;
                    });
                
                State.AddLogMessage("🎬 Batch Mode enabled! Drag & drop video files anywhere on this window to add them to the queue.");
                State.AddLogMessage("💡 Tip: Files with 🌐 are on network drives and may take longer to process.");
                State.AddLogMessage($"⚙️ Using settings: {preferenceText}, {State.OcrLanguage} language");
            }
            else
            {
                State.AddLogMessage("📁 Batch Mode disabled. Switch back to single file processing mode.");
                // Clear the batch queue when disabling batch mode
                ClearBatchQueue();
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Error handling batch mode change", ex);
        }
    }

    private void ToggleBatchMode()
    {
        try
        {
            State.IsBatchMode = !State.IsBatchMode;
            _loggingService.LogInfo($"Batch mode toggled to: {State.IsBatchMode}");
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Error toggling batch mode", ex);
        }
    }

    private void ShowHelp()
    {
        try
        {
            _loggingService.LogInfo("User requested help");
            _notificationService.ShowInfo(
                "Keyboard Shortcuts:\n" +
                "• Ctrl+O - Open Video File\n" +
                "• Ctrl+P - Probe Tracks\n" +
                "• Ctrl+E - Extract Subtitles\n" +
                "• Ctrl+B - Toggle Batch Mode\n" +
                "• Ctrl+C - Cancel Operation\n" +
                "• F5 - Re-detect Tools\n" +
                "• F1 - Show Help\n" +
                "• Escape - Cancel Operation\n\n" +
                "For more detailed help, visit the project repository:\n" +
                "https://github.com/ZentrixLabs/SrtExtractor",
                "SrtExtractor Help",
                7000);
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Error showing help", ex);
        }
    }

    private void OpenRecentFile(string? filePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            _loggingService.LogInfo($"User selected recent file: {filePath}");
            State.MkvPath = filePath;
            State.Tracks.Clear();
            State.SelectedTrack = null;
            State.HasProbedFile = false;
            // Clear the message state when opening a recent file
            State.ShowNoTracksError = false;
            
            // Update network detection
            UpdateNetworkDetection(filePath);
            
            State.AddLogMessage($"Opened recent file: {Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Error opening recent file: {filePath}", ex);
            _notificationService.ShowError($"Failed to open recent file:\n{ex.Message}", "Error");
        }
    }

    private async Task LoadRecentFilesAsync()
    {
        try
        {
            var recentFiles = await _recentFilesService.GetRecentFilesAsync().ConfigureAwait(false);
            
            // Update UI on UI thread (non-blocking)
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                State.RecentFiles.Clear();
                foreach (var file in recentFiles)
                {
                    State.RecentFiles.Add(file);
                }
            });
            
            _loggingService.LogInfo($"Loaded {recentFiles.Count} recent files");
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to load recent files", ex);
        }
    }

    /// <summary>
    /// Apply multi-pass correction to an SRT file based on current settings.
    /// </summary>
    /// <param name="srtPath">Path to the SRT file to correct</param>
    /// <param name="cancellationToken">Cancellation token</param>
    private async Task ApplyMultiPassCorrectionAsync(string srtPath, CancellationToken cancellationToken)
    {
        try
        {
            if (!State.EnableMultiPassCorrection)
            {
                // Use single-pass correction if multi-pass is disabled
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    State.UpdateProcessingMessage("Correcting common subtitle errors...");
                    State.AddLogMessage("Correcting common subtitle errors...");
                });
                
                var correctionCount = await _srtCorrectionService.CorrectSrtFileAsync(srtPath, cancellationToken).ConfigureAwait(false);
                
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    State.UpdateProcessingMessage("Subtitle correction completed!");
                    State.AddLogMessage($"🎯 Subtitle correction completed! Applied {correctionCount} corrections.");
                });
                return;
            }

            // Read the SRT content
            var srtContent = await File.ReadAllTextAsync(srtPath, cancellationToken).ConfigureAwait(false);
            
            // Apply multi-pass correction based on current mode
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                State.UpdateProcessingMessage($"Starting {State.CorrectionMode.ToLower()} multi-pass correction...");
                State.AddLogMessage($"Starting {State.CorrectionMode.ToLower()} multi-pass correction...");
            });
            
            var result = await _multiPassCorrectionService.ProcessWithModeAsync(
                srtContent, 
                State.CorrectionMode, 
                cancellationToken).ConfigureAwait(false);
            
            // Write the corrected content back to file
            if (result.CorrectedContent != srtContent)
            {
                await File.WriteAllTextAsync(srtPath, result.CorrectedContent, cancellationToken).ConfigureAwait(false);
            }
            
            // Update UI with results - marshal back to UI thread
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                State.UpdateProcessingMessage("Multi-pass correction completed!");
                
                var convergenceText = result.Converged ? " (converged)" : "";
                State.AddLogMessage($"🎯 Multi-pass correction completed!{convergenceText}");
                State.AddLogMessage($"   • Passes completed: {result.PassesCompleted}");
                State.AddLogMessage($"   • Total corrections: {result.TotalCorrections}");
                State.AddLogMessage($"   • Processing time: {result.ProcessingTimeMs}ms");
            });
            
            // Log any warnings
            if (result.Warnings.Any())
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    foreach (var warning in result.Warnings)
                    {
                        State.AddLogMessage($"⚠️ Warning: {warning}");
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
                State.AddLogMessage($"❌ Error during correction: {ex.Message}");
            });
            
            // Fall back to single-pass correction
            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    State.UpdateProcessingMessage("Falling back to single-pass correction...");
                    State.AddLogMessage("Falling back to single-pass correction...");
                });
                
                var correctionCount = await _srtCorrectionService.CorrectSrtFileAsync(srtPath, cancellationToken).ConfigureAwait(false);
                
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    State.UpdateProcessingMessage("Single-pass correction completed!");
                    State.AddLogMessage($"🎯 Single-pass correction completed! Applied {correctionCount} corrections.");
                });
            }
            catch (Exception fallbackEx)
            {
                _loggingService.LogError("Error during fallback single-pass correction", fallbackEx);
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    State.AddLogMessage($"❌ Error during fallback correction: {fallbackEx.Message}");
                });
                throw;
            }
        }
    }

    #endregion

    #region IDisposable Implementation

    private bool _disposed = false;

    /// <summary>
    /// Disposes of resources used by the MainViewModel.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Protected implementation of Dispose pattern.
    /// </summary>
    /// <param name="disposing">True if called from Dispose(), false if called from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Wait for initialization to complete (with timeout to prevent hanging on shutdown)
                try
                {
                    if (_initializationTask != null && !_initializationTask.IsCompleted)
                    {
                        _loggingService.LogInfo("Waiting for initialization to complete before disposal...");
                        if (!_initializationTask.Wait(TimeSpan.FromSeconds(5)))
                        {
                            _loggingService.LogWarning("Initialization task did not complete within timeout during disposal");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _loggingService.LogError("Error waiting for initialization during disposal", ex);
                }

                // Dispose managed resources
                _extractionCancellationTokenSource?.Dispose();
                _extractionCancellationTokenSource = null;

                // Unsubscribe from events to prevent memory leaks
                if (State != null)
                {
                    State.PreferencesChanged -= OnPreferencesChanged;
                }

                _loggingService.LogInfo("MainViewModel disposed successfully");
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Finalizer to ensure resources are cleaned up even if Dispose() is not called.
    /// </summary>
    ~MainViewModel()
    {
        Dispose(false);
    }

    #endregion

}
