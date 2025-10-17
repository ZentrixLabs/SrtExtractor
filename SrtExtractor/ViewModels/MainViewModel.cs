using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SrtExtractor.Constants;
using SrtExtractor.Coordinators;
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
    private readonly IMkvToolService _mkvToolService;
    private readonly IFfmpegService _ffmpegService;
    private readonly ISubtitleOcrService _ocrService;
    private readonly ISrtCorrectionService _srtCorrectionService;
    private readonly IMultiPassCorrectionService _multiPassCorrectionService;
    private readonly ISettingsService _settingsService;
    private readonly INetworkDetectionService _networkDetectionService;
    private readonly IRecentFilesService _recentFilesService;
    private readonly IFileCacheService _fileCacheService;
    private readonly ExtractionCoordinator _extractionCoordinator;
    private readonly BatchCoordinator _batchCoordinator;
    private readonly FileCoordinator _fileCoordinator;
    private readonly ToolCoordinator _toolCoordinator;
    private readonly CleanupCoordinator _cleanupCoordinator;
    private CancellationTokenSource? _extractionCancellationTokenSource;
    private Task? _initializationTask;

    [ObservableProperty]
    private ExtractionState _state = new();

    public MainViewModel(
        ILoggingService loggingService,
        INotificationService notificationService,
        IToolDetectionService toolDetectionService,
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
        _mkvToolService = mkvToolService;
        _ffmpegService = ffmpegService;
        _ocrService = ocrService;
        _srtCorrectionService = srtCorrectionService;
        _multiPassCorrectionService = multiPassCorrectionService;
        _settingsService = settingsService;
        _networkDetectionService = networkDetectionService;
        _recentFilesService = recentFilesService;
        _fileCacheService = fileCacheService;
        
        // Create ExtractionCoordinator with our State instance to ensure they share state
        _extractionCoordinator = new ExtractionCoordinator(
            loggingService,
            notificationService,
            mkvToolService,
            ffmpegService,
            ocrService,
            srtCorrectionService,
            multiPassCorrectionService,
            State);

        // Create FileCoordinator for file-related operations (created before BatchCoordinator since batch needs it)
        _fileCoordinator = new FileCoordinator(
            loggingService,
            notificationService,
            networkDetectionService,
            recentFilesService,
            State);

        // Create ToolCoordinator for tool management
        _toolCoordinator = new ToolCoordinator(
            loggingService,
            notificationService,
            toolDetectionService,
            settingsService,
            State);

        // Create CleanupCoordinator for temporary file cleanup
        _cleanupCoordinator = new CleanupCoordinator(
            loggingService,
            State);

        // Create BatchCoordinator with delegates to our methods (it needs to call ProbeTracksAsync and ExtractSubtitlesAsync)
        _batchCoordinator = new BatchCoordinator(
            loggingService,
            notificationService,
            networkDetectionService,
            fileCacheService,
            State,
            () => ProbeTracksAsync(),
            (ct) => ExtractSubtitlesAsync(ct),
            (path) => _fileCoordinator.UpdateNetworkDetection(path));

        // Subscribe to preference changes
        State.PreferencesChanged += OnPreferencesChanged;
        
        // Ensure clean state on startup
        State.ClearFileState();
        
        // Load settings asynchronously
        _initializationTask = LoadSettingsAsync();
        

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
        AddFolderToBatchCommand = new AsyncRelayCommand(AddFolderToBatchAsync);
        
        
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
        OpenBatchSrtCorrectionCommand = new RelayCommand(OpenBatchSrtCorrection);
        OpenSupOcrCommand = new RelayCommand(OpenSupOcr);

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
    public IAsyncRelayCommand AddFolderToBatchCommand { get; }
    
    
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
    public IRelayCommand OpenBatchSrtCorrectionCommand { get; }
    public IRelayCommand OpenSupOcrCommand { get; }

    #endregion

    #region Command Implementations

    private Task PickMkvAsync()
    {
        return _fileCoordinator.PickMkvAsync();
    }

    /// <summary>
    /// Opens a folder picker and adds all MKV/MP4 files (recursively) to the batch queue.
    /// </summary>
    private async Task AddFolderToBatchAsync()
    {
        try
        {
            var folder = await _fileCoordinator.PickFolderAsync();
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                return;
            }

            State.AddLogMessage($"Scanning folder for videos: {folder}");
            _loggingService.LogInfo($"User selected folder for batch add: {folder}");

            var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mkv", ".mp4" };
            var files = new List<string>();

            try
            {
                foreach (var path in Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories))
                {
                    var ext = Path.GetExtension(path);
                    if (!string.IsNullOrEmpty(ext) && supportedExtensions.Contains(ext))
                    {
                        files.Add(path);
                    }
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Error while scanning folder for videos", ex);
                State.AddLogMessage($"Error scanning folder: {ex.Message}");
            }

            if (files.Count == 0)
            {
                _notificationService.ShowWarning("No MKV/MP4 files found in the selected folder.", "No Videos Found");
                return;
            }

            // Ensure Batch tab is visible
            State.SelectedTabIndex = 1;

            await AddFilesToBatchQueueAsync(files.ToArray());
            State.AddLogMessage($"Added {files.Count} file(s) from folder to batch queue");
            _loggingService.LogInfo($"Added {files.Count} files to batch queue from folder");
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to add folder to batch queue", ex);
            _notificationService.ShowError($"Failed to add folder to batch queue:\n{ex.Message}", "Batch Add Error");
        }
    }

    private async Task ProbeTracksAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(State.MkvPath))
            return;

        try
        {
            State.IsBusy = true;
            State.ClearFileState();
            _loggingService.LogInfo("Probe started - file state cleared");
            
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
            
            // Force UI update by explicitly setting success state to false again
            State.ShowExtractionSuccess = false;
            
            _loggingService.LogInfo($"Probe completed - ShowNoTracksError={State.ShowNoTracksError}, ShowExtractionSuccess={State.ShowExtractionSuccess}");

            State.UpdateProcessingMessage("Analysis completed!");
            State.AddLogMessage($"Found {result.Tracks.Count} subtitle tracks");
            
            // If no tracks found, log diagnostic information
            if (result.Tracks.Count == 0)
            {
                var fileSizeFormatted = Utils.FileUtilities.FormatFileSize(fileInfo.Length);
                
                State.AddLogMessage(""); // Blank line for readability
                State.AddLogMessage("📊 DIAGNOSTIC INFORMATION:");
                State.AddLogMessage($"  File: {Path.GetFileName(State.MkvPath)}");
                State.AddLogMessage($"  Container: {fileExtension.TrimStart('.').ToUpper()}");
                State.AddLogMessage($"  Size: {fileSizeFormatted}");
                State.AddLogMessage($"  Probe Tool: {(fileExtension == ".mp4" ? "FFprobe" : "mkvmerge")}");
                State.AddLogMessage("");
                State.AddLogMessage("🔍 ANALYSIS RESULTS:");
                State.AddLogMessage("  ✗ No subtitle streams found in container");
                State.AddLogMessage("  ℹ This indicates the file does not have embedded subtitle tracks");
                State.AddLogMessage("");
                State.AddLogMessage("💡 POSSIBLE REASONS:");
                State.AddLogMessage("  • File was encoded without subtitles");
                State.AddLogMessage("  • Subtitles are hardcoded (burned into video - cannot be extracted)");
                State.AddLogMessage("  • Subtitles are in a separate .srt/.ass file");
                State.AddLogMessage("  • Wrong source file (not the original release)");
                State.AddLogMessage("");
                State.AddLogMessage("✓ NEXT STEPS:");
                State.AddLogMessage("  1. Verify file in VLC (View → Track → Subtitle Track)");
                State.AddLogMessage("  2. Check if subtitle file exists separately (same folder)");
                State.AddLogMessage("  3. Try opening original source file if this is a re-encode");
            }
            else
            {
                // Log technical details for each track (for power users inspecting History tab)
                State.AddLogMessage(""); // Blank line for readability
                foreach (var track in State.Tracks)
                {
                    var speedInfo = track.SpeedIndicator.Contains("Fast") ? "FAST" : "OCR-REQUIRED";
                    State.AddLogMessage($"  Track {track.Id}: {track.Language} | {track.FormatDisplay} ({speedInfo}) | Codec: {track.Codec} | Frames: {track.FrameCount} | {(track.Forced ? "FORCED" : "FULL")}");
                }
            }
            
            // Mark that we've probed this file
            State.HasProbedFile = true;
            
            // Auto-select messages (only shown when tracks exist)
            if (result.Tracks.Count > 0 && selectedTrack != null)
            {
                var englishTracks = result.Tracks.Where(t => string.Equals(t.Language, "eng", StringComparison.OrdinalIgnoreCase)).ToList();
                if (englishTracks.Count == 1)
                {
                    State.AddLogMessage($"⭐ Auto-selected track {selectedTrack.Id}: {selectedTrack.FormatDisplay} ({selectedTrack.Language}) - Only English track available");
                }
                else
                {
                    State.AddLogMessage($"⭐ Auto-selected track {selectedTrack.Id}: {selectedTrack.FormatDisplay} ({selectedTrack.Language}) - Best of {englishTracks.Count} English tracks");
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

            // Delegate to ExtractionCoordinator
            await _extractionCoordinator.ExtractSubtitlesAsync(State.MkvPath, State.SelectedTrack, outputPath, cancellationToken ?? CancellationToken.None);

            State.AddLogMessage("Subtitle extraction completed successfully!");
            
            // Complete progress tracking
            State.UpdateProgress(State.TotalBytes, "Extraction completed successfully");
            
            // Add to recent files
            await _recentFilesService.AddFileAsync(State.MkvPath).ConfigureAwait(false);
            await LoadRecentFilesAsync().ConfigureAwait(false); // Refresh the UI list
            
            // Set success state for UI feedback (after all processing is complete)
            State.ShowExtractionSuccess = true;
            State.LastExtractionOutputPath = outputPath;
            
            // Note: Success message is now shown in-app via State.ShowExtractionSuccess
            // Toast notification removed to prevent confusion with other messages
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
            
            // Show error notification only for single file extraction (not batch)
            // For batch processing, let the exception propagate so batch can handle it
            if (_extractionCancellationTokenSource == null)
            {
                // This is batch processing - rethrow so batch handler can catch and mark as error
                throw;
            }
            
            // Single file extraction - show error dialog
            _notificationService.ShowError($"Failed to extract subtitles:\n{ex.Message}", "Extraction Error");
        }
        finally
        {
            State.IsBusy = false;
            State.StopProcessingWithProgress();
            _extractionCancellationTokenSource?.Dispose();
            _extractionCancellationTokenSource = null;
        }
    }


    private Task InstallMkvToolNixAsync()
    {
        return _toolCoordinator.InstallMkvToolNixAsync();
    }

    private void BrowseMkvToolNix()
    {
        _toolCoordinator.BrowseMkvToolNix();
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
                
                // Handle new CorrectionLevel with backward compatibility
                if (settings.CorrectionLevel != default)
                {
                    // New settings format - use CorrectionLevel directly
                    State.CorrectionLevel = settings.CorrectionLevel;
                }
                else
                {
                    // Legacy settings format - convert from boolean flags
                    State.CorrectionLevel = CorrectionLevelExtensions.FromLegacySettings(
                        settings.EnableSrtCorrection, 
                        settings.EnableMultiPassCorrection);
                }
                
                State.PreserveSupFiles = settings.PreserveSupFiles;

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
        await _toolCoordinator.DetectToolsAsync();
    }

    private async Task UpdateToolPathAsync(string toolName, string toolPath)
    {
        await _toolCoordinator.UpdateToolPathAsync(toolName, toolPath);
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
        var subripTracks = languageTracks.Where(t => t.IsSubRip).ToList();
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
    /// Select the best quality track from a list of tracks based on codec type, reasonable quality threshold, and track order.
    /// BUGFIX: Now prioritizes the FIRST high-quality track (by TrackId) to avoid commentary tracks which often have very high bitrates.
    /// Uses the SubtitleTrack model's cached codec properties for better performance.
    /// </summary>
    /// <param name="tracks">List of tracks to choose from</param>
    /// <returns>Best quality track</returns>
    private SubtitleTrack GetBestQualityTrack(IList<SubtitleTrack> tracks)
    {
        if (!tracks.Any()) return tracks.First();

        // CRITICAL: Filter out commentary tracks first (they should never be auto-selected)
        var nonCommentaryTracks = tracks.Where(t => !t.IsCommentary).ToList();
        if (nonCommentaryTracks.Any())
        {
            _loggingService.LogInfo($"Filtered out {tracks.Count - nonCommentaryTracks.Count} commentary track(s), selecting from {nonCommentaryTracks.Count} non-commentary tracks");
            tracks = nonCommentaryTracks;
        }
        else
        {
            _loggingService.LogInfo("All tracks are commentary tracks - will select from all available");
        }

        // Priority order: SubRip/SRT > Other text-based > HDMV PGS > Other PGS
        // Use the track's built-in codec properties instead of parsing strings
        var subripTracks = tracks.Where(t => t.IsSubRip).ToList();
        if (subripTracks.Any())
        {
            _loggingService.LogInfo($"Selecting from {subripTracks.Count} SubRip/SRT tracks (highest priority)");
            tracks = subripTracks;
        }
        else
        {
            // If no SubRip/SRT, prefer other text-based subtitles over PGS
            var textTracks = tracks.Where(t => t.IsTextBased).ToList();
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

        // BUGFIX: Prioritize FIRST (lowest TrackId) track among high-quality tracks
        // This avoids selecting tracks that might have issues or come later in the file.
        // Strategy: Filter tracks that meet a reasonable quality threshold, then pick the first one (lowest TrackId)
        
        // Define quality thresholds (tracks below these are likely incomplete/poor quality)
        const long minBitrate = 500; // 500 bps minimum (very low bar, just to filter out clearly broken tracks)
        const int minFrameCount = 100; // 100 frames minimum (filters out test/stub tracks)
        
        // Filter to high-quality tracks (meeting minimum thresholds)
        var qualityTracks = tracks.Where(t => t.Bitrate >= minBitrate && t.FrameCount >= minFrameCount).ToList();
        
        // If we have quality tracks, pick the FIRST one (lowest TrackId = earliest in file)
        // Otherwise fall back to first track in the original list
        if (qualityTracks.Any())
        {
            var selectedTrack = qualityTracks.OrderBy(t => t.TrackId).First();
            _loggingService.LogInfo($"Selected FIRST high-quality track (ID: {selectedTrack.TrackId}, Bitrate: {selectedTrack.Bitrate}, Frames: {selectedTrack.FrameCount})");
            return selectedTrack;
        }
        
        // Fallback: No tracks meet quality threshold, just return the first track
        _loggingService.LogInfo($"No tracks met quality threshold, selecting first available track");
        return tracks.OrderBy(t => t.TrackId).First();
    }

    // Codec helper methods removed - now using SubtitleTrack.CodecType, .IsSubRip, .IsTextBased, and .CodecPriority properties

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
        await _toolCoordinator.ReDetectToolsAsync();
    }

    private async Task CleanupTempFilesAsync()
    {
        await _cleanupCoordinator.CleanupTempFilesAsync();
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

    /// <summary>
    /// Save settings when user changes them in the settings dialog.
    /// Called by SettingsWindow when user clicks OK.
    /// </summary>
    public async Task SaveSettingsFromDialogAsync()
    {
        try
        {
            // Load existing settings to preserve ShowWelcomeScreen preference
            var existingSettings = await _settingsService.LoadSettingsAsync();
            
            var settings = new AppSettings(
                MkvMergePath: null, // These will be updated when tools are detected
                MkvExtractPath: null,
                TesseractDataPath: null,
                AutoDetectTools: true,
                LastToolCheck: DateTime.Now,
                    PreferForced: State.PreferForced,
                    PreferClosedCaptions: State.PreferClosedCaptions,
                    DefaultOcrLanguage: State.OcrLanguage,
                    FileNamePattern: State.FileNamePattern,
                    ShowWelcomeScreen: existingSettings.ShowWelcomeScreen,
                    CorrectionLevel: State.CorrectionLevel,
                    PreserveSupFiles: State.PreserveSupFiles
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
        _fileCoordinator.UpdateNetworkDetection(filePath);
    }



    /// <summary>
    /// Process all files in the batch queue.
    /// </summary>
    private async Task ProcessBatchAsync()
    {
        await _batchCoordinator.ProcessBatchAsync(_extractionCancellationTokenSource);
    }


    /// <summary>
    /// Clear the batch queue.
    /// </summary>
    private void ClearBatchQueue()
    {
        _batchCoordinator.ClearBatchQueue();
    }

    /// <summary>
    /// Clear only completed items from the batch queue.
    /// </summary>
    private void ClearCompletedBatchItems()
    {
        _batchCoordinator.ClearCompletedBatchItems();
    }

    /// <summary>
    /// Resume batch processing from where it was interrupted.
    /// </summary>
    private async Task ResumeBatchAsync()
    {
        await _batchCoordinator.ResumeBatchAsync(_extractionCancellationTokenSource);
    }

    /// <summary>
    /// Remove a file from the batch queue.
    /// </summary>
    /// <param name="batchFile">The batch file to remove</param>
    private void RemoveFromBatch(BatchFile? batchFile)
    {
        _batchCoordinator.RemoveFromBatch(batchFile);
    }

    /// <summary>
    /// Add files to batch queue from drag and drop.
    /// Uses bulk add to prevent excessive UI updates.
    /// </summary>
    /// <param name="filePaths">Array of file paths to add</param>
    public async Task AddFilesToBatchQueueAsync(string[] filePaths)
    {
        await _batchCoordinator.AddFilesToBatchQueueAsync(filePaths);
    }

    /// <summary>
    /// Move a batch item to the top of the queue.
    /// </summary>
    /// <param name="batchFile">The batch file to move</param>
    public void MoveBatchItemToTop(BatchFile batchFile)
    {
        _batchCoordinator.MoveBatchItemToTop(batchFile);
    }

    /// <summary>
    /// Move a batch item to the bottom of the queue.
    /// </summary>
    /// <param name="batchFile">The batch file to move</param>
    public void MoveBatchItemToBottom(BatchFile batchFile)
    {
        _batchCoordinator.MoveBatchItemToBottom(batchFile);
    }

    /// <summary>
    /// Reorder batch queue items by dragging and dropping.
    /// </summary>
    /// <param name="draggedItem">The item being dragged</param>
    /// <param name="targetItem">The item being dropped on</param>
    public void ReorderBatchQueue(BatchFile draggedItem, BatchFile targetItem)
    {
        _batchCoordinator.ReorderBatchQueue(draggedItem, targetItem);
    }

    /// <summary>
    /// Process a single file from the batch queue.
    /// </summary>
    /// <param name="batchFile">The batch file to process</param>
    public async Task ProcessSingleBatchFileAsync(BatchFile? batchFile)
    {
        await _batchCoordinator.ProcessSingleBatchFileAsync(batchFile);
    }

    private void ToggleBatchMode()
    {
        try
        {
            // With tab-based interface, Ctrl+B switches to the Batch tab
            State.SelectedTabIndex = 1; // 0=Extract, 1=Batch, 2=History, 3=Tools
            _loggingService.LogInfo("Switched to Batch tab via keyboard shortcut (Ctrl+B)");
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Error switching to batch tab", ex);
        }
    }

    private void ShowHelp()
    {
        try
        {
            _loggingService.LogInfo("User requested keyboard shortcuts help");
            
            // Open the keyboard shortcuts window
            var helpWindow = new Views.KeyboardShortcutsWindow
            {
                Owner = Application.Current.MainWindow
            };
            helpWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Error showing help", ex);
        }
    }

    private void OpenBatchSrtCorrection()
    {
        try
        {
            _loggingService.LogInfo("User opened Batch SRT Correction via keyboard shortcut");
            
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Find the MainWindow and call the click handler
                if (Application.Current.MainWindow is Views.MainWindow mainWindow)
                {
                    mainWindow.BatchSrtCorrection_Click(this, new RoutedEventArgs());
                }
            });
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Error opening Batch SRT Correction", ex);
        }
    }

    private void OpenSupOcr()
    {
        try
        {
            _loggingService.LogInfo("User opened SUP OCR Tool via keyboard shortcut");
            
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Find the MainWindow and call the click handler
                if (Application.Current.MainWindow is Views.MainWindow mainWindow)
                {
                    mainWindow.LoadSupFile_Click(this, new RoutedEventArgs());
                }
            });
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Error opening SUP OCR Tool", ex);
        }
    }

    private void OpenRecentFile(string? filePath)
    {
        _fileCoordinator.OpenRecentFile(filePath);
    }

    private async Task LoadRecentFilesAsync()
    {
        await _fileCoordinator.LoadRecentFilesAsync();
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
            // Check if SRT correction is completely disabled
            if (!State.EnableSrtCorrection)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    State.AddLogMessage("ℹ️ SRT correction is disabled - using raw OCR output");
                });
                _loggingService.LogInfo("SRT correction disabled - skipping all corrections");
                return;
            }

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

    #region Settings Management

    /// <summary>
    /// Load application settings from persistent storage.
    /// </summary>
    private async Task LoadSettingsAsync()
    {
        try
        {
            _loggingService.LogInfo("Loading application settings...");
            
            var settings = await _settingsService.LoadSettingsAsync().ConfigureAwait(false);
            
            // Update UI on UI thread
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // Load settings into state
                State.PreferForced = settings.PreferForced;
                State.PreferClosedCaptions = settings.PreferClosedCaptions;
                State.OcrLanguage = settings.DefaultOcrLanguage;
                State.FileNamePattern = settings.FileNamePattern;
                State.PreserveSupFiles = settings.PreserveSupFiles;
                
                // Handle new CorrectionLevel with backward compatibility
                if (settings.CorrectionLevel != default)
                {
                    // New settings format - use CorrectionLevel directly
                    State.CorrectionLevel = settings.CorrectionLevel;
                }
                else
                {
                    // Legacy settings format - convert from boolean flags
                    State.CorrectionLevel = CorrectionLevelExtensions.FromLegacySettings(
                        settings.EnableSrtCorrection, 
                        settings.EnableMultiPassCorrection);
                }
                
                _loggingService.LogInfo($"Settings loaded - Correction Level: {State.CorrectionLevel}");
            });
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to load settings", ex);
            // Continue with default settings
        }
    }

    /// <summary>
    /// Save current settings to persistent storage.
    /// </summary>
    public async Task SaveSettingsAsync()
    {
        try
        {
            _loggingService.LogInfo("Saving application settings...");
            
            var settings = new AppSettings(
                MkvMergePath: null, // Tool paths are managed separately
                MkvExtractPath: null,
                TesseractDataPath: null,
                AutoDetectTools: true,
                LastToolCheck: DateTime.Now,
                PreferForced: State.PreferForced,
                PreferClosedCaptions: State.PreferClosedCaptions,
                DefaultOcrLanguage: State.OcrLanguage,
                FileNamePattern: State.FileNamePattern,
                ShowWelcomeScreen: true,
                CorrectionLevel: State.CorrectionLevel,
                PreserveSupFiles: State.PreserveSupFiles
            );
            
            await _settingsService.SaveSettingsAsync(settings).ConfigureAwait(false);
            _loggingService.LogInfo("Settings saved successfully");
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to save settings", ex);
            throw;
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
