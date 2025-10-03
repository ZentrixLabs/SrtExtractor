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
public partial class MainViewModel : ObservableObject
{
    private readonly ILoggingService _loggingService;
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
    private CancellationTokenSource? _extractionCancellationTokenSource;

    [ObservableProperty]
    private ExtractionState _state = new();

    public MainViewModel(
        ILoggingService loggingService,
        IToolDetectionService toolDetectionService,
        IWingetService wingetService,
        IMkvToolService mkvToolService,
        IFfmpegService ffmpegService,
        ISubtitleOcrService ocrService,
        ISrtCorrectionService srtCorrectionService,
        IMultiPassCorrectionService multiPassCorrectionService,
        ISettingsService settingsService,
        INetworkDetectionService networkDetectionService,
        IRecentFilesService recentFilesService)
    {
        _loggingService = loggingService;
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
        CorrectSrtCommand = new AsyncRelayCommand(CorrectSrtAsync);
        
        // Batch mode commands
        ProcessBatchCommand = new AsyncRelayCommand(ProcessBatchAsync, () => State.HasBatchQueue);
        ClearBatchQueueCommand = new RelayCommand(ClearBatchQueue);
        RemoveFromBatchCommand = new RelayCommand<BatchFile>(RemoveFromBatch);

        // Menu commands
        ToggleBatchModeCommand = new RelayCommand(ToggleBatchMode);
        ShowHelpCommand = new RelayCommand(ShowHelp);
        OpenRecentFileCommand = new RelayCommand<string>(OpenRecentFile);

        // Subscribe to state changes to update command states
        State.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(State.CanProbe) or nameof(State.CanExtract) or nameof(State.IsProcessing))
            {
                // Use Dispatcher to ensure UI thread access
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ProbeCommand.NotifyCanExecuteChanged();
                    ExtractCommand.NotifyCanExecuteChanged();
                    CancelCommand.NotifyCanExecuteChanged();
                });
            }
            else if (e.PropertyName == nameof(State.HasBatchQueue))
            {
                // Use Dispatcher to ensure UI thread access
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ProcessBatchCommand.NotifyCanExecuteChanged();
                });
            }
            else if (e.PropertyName == nameof(State.IsBatchMode))
            {
                OnBatchModeChanged(State.IsBatchMode);
            }
            // Note: Settings are saved manually when user changes them
            // Automatic saving was removed to prevent infinite loops
        };

        // Initialize the application
        Task.Run(async () => await InitializeAsync());
    }

    #region Commands

    public IAsyncRelayCommand PickMkvCommand { get; }
    public IAsyncRelayCommand ProbeCommand { get; }
    public IAsyncRelayCommand ExtractCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IAsyncRelayCommand InstallMkvToolNixCommand { get; }
    public IRelayCommand BrowseMkvToolNixCommand { get; }
    public IAsyncRelayCommand ReDetectToolsCommand { get; }
    public IAsyncRelayCommand CorrectSrtCommand { get; }
    
    // Batch mode commands
    public IAsyncRelayCommand ProcessBatchCommand { get; }
    public IRelayCommand ClearBatchQueueCommand { get; }
    public IRelayCommand<BatchFile> RemoveFromBatchCommand { get; }

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
                
                // Update network detection
                UpdateNetworkDetection(State.MkvPath);
                
                _loggingService.LogInfo($"Selected MKV file: {State.MkvPath}");
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to pick MKV file", ex);
            MessageBox.Show($"Failed to select MKV file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            State.StartProcessing("Analyzing video file...");
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
                var recommendedTrack = track with { IsRecommended = isRecommended };
                State.Tracks.Add(recommendedTrack);
            }

            State.SelectedTrack = selectedTrack;

            State.UpdateProcessingMessage("Analysis completed!");
            State.AddLogMessage($"Found {result.Tracks.Count} subtitle tracks");
            if (selectedTrack != null)
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
            MessageBox.Show($"Failed to probe video file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            State.IsBusy = false;
            State.StopProcessing();
        }
    }

    private void CancelExtraction()
    {
        try
        {
            _loggingService.LogInfo("User requested cancellation of extraction");
            State.AddLogMessage("Cancelling extraction...");
            
            _extractionCancellationTokenSource?.Cancel();
            
            State.StopProcessing();
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
            State.StartProcessing("Preparing extraction...");
            State.AddLogMessage($"Extracting subtitle track {State.SelectedTrack.Id}...");

            var outputPath = State.GenerateOutputFilename(State.MkvPath, State.SelectedTrack);

            // Use appropriate service based on file extension
            var fileExtension = Path.GetExtension(State.MkvPath).ToLowerInvariant();
            if (fileExtension == ".mp4")
            {
                // Use FFmpeg for MP4 files
                State.UpdateProcessingMessage("Extracting subtitles with FFmpeg...");
                await _ffmpegService.ExtractSubtitleAsync(State.MkvPath, State.SelectedTrack.Id, outputPath, cancellationToken ?? CancellationToken.None);
                State.UpdateProcessingMessage("MP4 extraction completed!");
                State.AddLogMessage($"Subtitles extracted to: {outputPath}");

                // Apply multi-pass SRT corrections to MP4 subtitles
                await ApplyMultiPassCorrectionAsync(outputPath, cancellationToken ?? CancellationToken.None);
            }
            else if (State.SelectedTrack.Codec.Contains("S_TEXT/UTF8") || State.SelectedTrack.Codec.Contains("SubRip/SRT"))
            {
                // Direct text extraction
                State.UpdateProcessingMessage("Extracting text subtitles...");
                await _mkvToolService.ExtractTextAsync(State.MkvPath, State.SelectedTrack.Id, outputPath, cancellationToken ?? CancellationToken.None);
                State.UpdateProcessingMessage("Text extraction completed!");
                State.AddLogMessage($"Text subtitles extracted to: {outputPath}");

                // Apply multi-pass SRT corrections to text subtitles
                await ApplyMultiPassCorrectionAsync(outputPath, cancellationToken ?? CancellationToken.None);
            }
            else if (State.SelectedTrack.Codec.Contains("PGS") || State.SelectedTrack.Codec.Contains("S_HDMV/PGS"))
            {
                // PGS extraction + OCR
                var tempSupPath = Path.ChangeExtension(outputPath, ".sup");
                
                State.UpdateProcessingMessage("Extracting PGS subtitles... (this can take a while, please be patient)");
                await _mkvToolService.ExtractPgsAsync(State.MkvPath, State.SelectedTrack.Id, tempSupPath, cancellationToken ?? CancellationToken.None);
                State.AddLogMessage($"PGS subtitles extracted to: {tempSupPath}");

                State.UpdateProcessingMessage("Starting OCR conversion... (this is the slowest step, please be patient)");
                State.AddLogMessage($"Starting OCR conversion to: {outputPath}");
                await _ocrService.OcrSupToSrtAsync(tempSupPath, outputPath, State.OcrLanguage, cancellationToken: cancellationToken ?? CancellationToken.None);
                State.UpdateProcessingMessage("OCR conversion completed!");
                State.AddLogMessage($"OCR conversion completed: {outputPath}");

                // Apply multi-pass OCR corrections
                await ApplyMultiPassCorrectionAsync(outputPath, cancellationToken ?? CancellationToken.None);

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
            else
            {
                throw new NotSupportedException($"Unsupported subtitle codec: {State.SelectedTrack.Codec}");
            }

            State.AddLogMessage("Subtitle extraction completed successfully!");
            
            // Add to recent files
            await _recentFilesService.AddFileAsync(State.MkvPath).ConfigureAwait(false);
            await LoadRecentFilesAsync().ConfigureAwait(false); // Refresh the UI list
            
            // Only show success dialog in single file mode, not batch mode
            if (!State.IsBatchMode)
            {
                MessageBox.Show($"Subtitles extracted successfully!\n\nOutput: {outputPath}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
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
                MessageBox.Show($"Failed to extract subtitles: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        finally
        {
            State.IsBusy = false;
            State.StopProcessing();
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
                MessageBox.Show("Failed to install MKVToolNix. Please check the log for details.", "Installation Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to install MKVToolNix", ex);
            State.AddLogMessage($"Error installing MKVToolNix: {ex.Message}");
            MessageBox.Show($"Failed to install MKVToolNix: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            
            // Show a message directing users to settings
            Application.Current.Dispatcher.Invoke(() =>
            {
                var result = MessageBox.Show(
                    "Some required tools are missing and need to be configured.\n\n" +
                    "Would you like to open Settings to configure the tools now?",
                    "Tools Missing",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    // This will be handled by the main window when it opens
                    State.ShowSettingsOnStartup = true;
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
        var originalTracks = State.Tracks.Select(t => t with { IsRecommended = false }).ToList();
        
        // Re-select best track based on current preferences
        var selectedTrack = SelectBestTrack(originalTracks);
        
        // Update all tracks with new recommendation flags
        State.Tracks.Clear();
        foreach (var track in originalTracks)
        {
            var isRecommended = selectedTrack != null && track.Id == selectedTrack.Id;
            var recommendedTrack = track with { IsRecommended = isRecommended };
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

        // Default: Prefer Full tracks, then by quality
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
    private static SubtitleTrack GetBestQualityTrack(IList<SubtitleTrack> tracks)
    {
        if (!tracks.Any()) return tracks.First();

        // Prefer text-based subtitles over PGS when available
        var textTracks = tracks.Where(t => t.Codec.Contains("S_TEXT")).ToList();
        if (textTracks.Any())
        {
            tracks = textTracks;
        }

        // Sort by quality metrics: bitrate (desc), frame count (desc), then by codec preference
        return tracks.OrderByDescending(t => t.Bitrate ?? 0)
                     .ThenByDescending(t => t.FrameCount ?? 0)
                     .ThenByDescending(t => t.Codec.Contains("S_TEXT") ? 1 : 0)
                     .First();
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
                MessageBox.Show("SRT file has been corrected successfully!", "Correction Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to correct SRT file", ex);
            State.AddLogMessage($"Error correcting SRT file: {ex.Message}");
            MessageBox.Show($"Failed to correct SRT file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            var settings = new AppSettings(
                MkvMergePath: null, // These will be updated when tools are detected
                MkvExtractPath: null,
                SubtitleEditPath: null,
                AutoDetectTools: true,
                LastToolCheck: DateTime.Now,
                    PreferForced: State.PreferForced,
                    PreferClosedCaptions: State.PreferClosedCaptions,
                    DefaultOcrLanguage: State.OcrLanguage,
                    FileNamePattern: State.FileNamePattern
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
                    
                    // Give the process a moment to release the file handle
                    await Task.Delay(2000); // Increased delay for better reliability
                    
                    // Try to delete the file, with retry logic for file handle issues
                    var maxRetries = 5; // Increased retries
                    for (int i = 0; i < maxRetries; i++)
                    {
                        try
                        {
                            File.Delete(tempSupPath);
                            _loggingService.LogInfo($"Successfully cleaned up temporary file: {Path.GetFileName(tempSupPath)}");
                            // Don't spam the UI log with cleanup messages - this is automatic
                            break;
                        }
                        catch (IOException) when (i < maxRetries - 1)
                        {
                            _loggingService.LogInfo($"File still in use, retrying in 2 seconds... (attempt {i + 1}/{maxRetries})");
                            await Task.Delay(2000); // Increased delay between retries
                        }
                        catch (UnauthorizedAccessException) when (i < maxRetries - 1)
                        {
                            _loggingService.LogInfo($"Access denied, retrying in 2 seconds... (attempt {i + 1}/{maxRetries})");
                            await Task.Delay(2000);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to clean up temporary files", ex);
            // Don't show this to the user as it's automatic cleanup - just log it
        }
    }


    /// <summary>
    /// Process all files in the batch queue.
    /// </summary>
    private async Task ProcessBatchAsync()
    {
        if (!State.BatchQueue.Any())
            return;

        try
        {
            State.IsBusy = true;
            State.StartProcessing("Starting batch processing...");
            State.AddLogMessage($"Starting batch processing of {State.BatchQueue.Count} files");

            var totalFiles = State.BatchQueue.Count;
            var processedCount = 0;
            var successCount = 0;
            var errorCount = 0;

            foreach (var batchFile in State.BatchQueue.ToList())
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
                }

                processedCount++;
            }

            State.StopProcessing();
            
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

            MessageBox.Show(message, "Batch Processing Complete", MessageBoxButton.OK, 
                           errorCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
            
            // Clear the batch queue and reset progress after completion
            State.ClearBatchQueue();
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Batch processing failed", ex);
            State.AddLogMessage($"Batch processing failed: {ex.Message}");
            MessageBox.Show($"Batch processing failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            State.IsBusy = false;
            State.StopProcessing();
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
    /// </summary>
    /// <param name="filePaths">Array of file paths to add</param>
    public void AddFilesToBatchQueue(string[] filePaths)
    {
        try
        {
            var addedCount = 0;
            var skippedCount = 0;

            foreach (var filePath in filePaths)
            {
                if (State.AddToBatchQueue(filePath))
                {
                    addedCount++;
                    
                    // Update network detection for the batch file
                    var batchFile = State.BatchQueue.FirstOrDefault(f => f.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
                    if (batchFile != null)
                    {
                        var isNetwork = _networkDetectionService.IsNetworkPath(filePath);
                        var estimatedMinutes = _networkDetectionService.GetEstimatedProcessingTime(filePath);
                        batchFile.UpdateNetworkStatus(isNetwork, estimatedMinutes);
                    }
                }
                else
                {
                    skippedCount++;
                }
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
                
                var result = MessageBox.Show(message, "Batch Mode Settings Confirmation", 
                                           MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.No)
                {
                    // User declined, disable batch mode
                    State.IsBatchMode = false;
                    return;
                }
                
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
            MessageBox.Show(
                "SrtExtractor Help\n\n" +
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
                MessageBoxButton.OK,
                MessageBoxImage.Information);
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
            State.AddLogMessage($"Opened recent file: {Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Error opening recent file: {filePath}", ex);
            MessageBox.Show($"Failed to open recent file:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task LoadRecentFilesAsync()
    {
        try
        {
            var recentFiles = await _recentFilesService.GetRecentFilesAsync().ConfigureAwait(false);
            
            // Update UI on UI thread
            Application.Current.Dispatcher.Invoke(() =>
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
                State.UpdateProcessingMessage("Correcting common subtitle errors...");
                State.AddLogMessage("Correcting common subtitle errors...");
                var correctionCount = await _srtCorrectionService.CorrectSrtFileAsync(srtPath, cancellationToken);
                State.UpdateProcessingMessage("Subtitle correction completed!");
                State.AddLogMessage($"🎯 Subtitle correction completed! Applied {correctionCount} corrections.");
                return;
            }

            // Read the SRT content
            var srtContent = await File.ReadAllTextAsync(srtPath, cancellationToken);
            
            // Apply multi-pass correction based on current mode
            State.UpdateProcessingMessage($"Starting {State.CorrectionMode.ToLower()} multi-pass correction...");
            State.AddLogMessage($"Starting {State.CorrectionMode.ToLower()} multi-pass correction...");
            
            var result = await _multiPassCorrectionService.ProcessWithModeAsync(
                srtContent, 
                State.CorrectionMode, 
                cancellationToken);
            
            // Write the corrected content back to file
            if (result.CorrectedContent != srtContent)
            {
                await File.WriteAllTextAsync(srtPath, result.CorrectedContent, cancellationToken);
            }
            
            // Update UI with results
            State.UpdateProcessingMessage("Multi-pass correction completed!");
            
            var convergenceText = result.Converged ? " (converged)" : "";
            State.AddLogMessage($"🎯 Multi-pass correction completed!{convergenceText}");
            State.AddLogMessage($"   • Passes completed: {result.PassesCompleted}");
            State.AddLogMessage($"   • Total corrections: {result.TotalCorrections}");
            State.AddLogMessage($"   • Processing time: {result.ProcessingTimeMs}ms");
            
            // Log any warnings
            if (result.Warnings.Any())
            {
                foreach (var warning in result.Warnings)
                {
                    State.AddLogMessage($"⚠️ Warning: {warning}");
                }
            }
            
            _loggingService.LogInfo($"Multi-pass correction completed: {result.PassesCompleted} passes, {result.TotalCorrections} corrections, {result.ProcessingTimeMs}ms");
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Error during multi-pass correction", ex);
            State.AddLogMessage($"❌ Error during correction: {ex.Message}");
            
            // Fall back to single-pass correction
            try
            {
                State.UpdateProcessingMessage("Falling back to single-pass correction...");
                State.AddLogMessage("Falling back to single-pass correction...");
                var correctionCount = await _srtCorrectionService.CorrectSrtFileAsync(srtPath, cancellationToken);
                State.UpdateProcessingMessage("Single-pass correction completed!");
                State.AddLogMessage($"🎯 Single-pass correction completed! Applied {correctionCount} corrections.");
            }
            catch (Exception fallbackEx)
            {
                _loggingService.LogError("Error during fallback single-pass correction", fallbackEx);
                State.AddLogMessage($"❌ Error during fallback correction: {fallbackEx.Message}");
                throw;
            }
        }
    }

    #endregion
}
