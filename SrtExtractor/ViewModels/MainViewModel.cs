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
    private readonly ISettingsService _settingsService;
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
        ISettingsService settingsService)
    {
        _loggingService = loggingService;
        _toolDetectionService = toolDetectionService;
        _wingetService = wingetService;
        _mkvToolService = mkvToolService;
        _ffmpegService = ffmpegService;
        _ocrService = ocrService;
        _srtCorrectionService = srtCorrectionService;
        _settingsService = settingsService;

        // Subscribe to preference changes
        State.PreferencesChanged += OnPreferencesChanged;

        // Initialize commands
        PickMkvCommand = new AsyncRelayCommand(PickMkvAsync);
        ProbeCommand = new AsyncRelayCommand(ProbeTracksAsync, () => State.CanProbe);
        ExtractCommand = new AsyncRelayCommand(ExtractSubtitlesAsync, () => State.CanExtract);
        CancelCommand = new RelayCommand(CancelExtraction, () => State.IsProcessing);
        InstallMkvToolNixCommand = new AsyncRelayCommand(InstallMkvToolNixAsync);
        BrowseMkvToolNixCommand = new RelayCommand(BrowseMkvToolNix);
        ReDetectToolsCommand = new AsyncRelayCommand(ReDetectToolsAsync);
        CorrectSrtCommand = new AsyncRelayCommand(CorrectSrtAsync);

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

    private async Task ProbeTracksAsync()
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
                result = await _ffmpegService.ProbeAsync(State.MkvPath);
            }
            else
            {
                result = await _mkvToolService.ProbeAsync(State.MkvPath);
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

    private async Task ExtractSubtitlesAsync()
    {
        if (State.SelectedTrack == null || string.IsNullOrEmpty(State.MkvPath))
            return;

        // Create cancellation token source for this extraction
        _extractionCancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _extractionCancellationTokenSource.Token;

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
                await _ffmpegService.ExtractSubtitleAsync(State.MkvPath, State.SelectedTrack.Id, outputPath, cancellationToken);
                State.UpdateProcessingMessage("MP4 extraction completed!");
                State.AddLogMessage($"Subtitles extracted to: {outputPath}");
            }
            else if (State.SelectedTrack.Codec.Contains("S_TEXT/UTF8") || State.SelectedTrack.Codec.Contains("SubRip/SRT"))
            {
                // Direct text extraction
                State.UpdateProcessingMessage("Extracting text subtitles...");
                await _mkvToolService.ExtractTextAsync(State.MkvPath, State.SelectedTrack.Id, outputPath, cancellationToken);
                State.UpdateProcessingMessage("Text extraction completed!");
                State.AddLogMessage($"Text subtitles extracted to: {outputPath}");
            }
            else if (State.SelectedTrack.Codec.Contains("PGS") || State.SelectedTrack.Codec.Contains("S_HDMV/PGS"))
            {
                // PGS extraction + OCR
                var tempSupPath = Path.ChangeExtension(outputPath, ".sup");
                
                State.UpdateProcessingMessage("Extracting PGS subtitles... (this can take a while, please be patient)");
                await _mkvToolService.ExtractPgsAsync(State.MkvPath, State.SelectedTrack.Id, tempSupPath, cancellationToken);
                State.AddLogMessage($"PGS subtitles extracted to: {tempSupPath}");

                State.UpdateProcessingMessage("Starting OCR conversion... (this is the slowest step, please be patient)");
                State.AddLogMessage($"Starting OCR conversion to: {outputPath}");
                await _ocrService.OcrSupToSrtAsync(tempSupPath, outputPath, State.OcrLanguage, cancellationToken: cancellationToken);
                State.UpdateProcessingMessage("OCR conversion completed!");
                State.AddLogMessage($"OCR conversion completed: {outputPath}");

                // Correct common OCR errors
                State.UpdateProcessingMessage("Correcting common OCR errors...");
                State.AddLogMessage("Correcting common OCR errors...");
                var correctionCount = await _srtCorrectionService.CorrectSrtFileAsync(outputPath, cancellationToken);
                State.UpdateProcessingMessage("OCR correction completed!");
                State.AddLogMessage($"🎯 OCR correction completed! Applied {correctionCount} corrections.");

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
            MessageBox.Show($"Subtitles extracted successfully!\n\nOutput: {outputPath}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            _loggingService.LogInfo("Subtitle extraction was cancelled by user");
            State.AddLogMessage("Extraction cancelled by user");
            // Don't show error message for user cancellation
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to extract subtitles", ex);
            State.AddLogMessage($"Error extracting subtitles: {ex.Message}");
            MessageBox.Show($"Failed to extract subtitles: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            State.AddLogMessage("Some required tools are missing. Use the install buttons to install them.");
            
            // Check if MKVToolNix is missing and winget is not available (Windows 10)
            if (!mkvStatus.IsInstalled)
            {
                var wingetAvailable = await _wingetService.IsWingetAvailableAsync();
                if (!wingetAvailable)
                {
                    // Show Windows 10 dialog
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var dialog = new Views.Windows10Dialog();
                        dialog.Owner = Application.Current.MainWindow;
                        dialog.ShowDialog();
                    });
                }
            }
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
                State.UpdateProcessingMessage("Correcting common OCR errors...");
                
                var correctionCount = await _srtCorrectionService.CorrectSrtFileAsync(openFileDialog.FileName);
                
                State.AddLogMessage($"🎯 SRT correction completed successfully! Applied {correctionCount} corrections.");
                State.UpdateProcessingMessage("SRT correction completed!");
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
            State.UpdateProcessingMessage("");
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

    #endregion
}
