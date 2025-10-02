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
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private ExtractionState _state = new();

    public MainViewModel(
        ILoggingService loggingService,
        IToolDetectionService toolDetectionService,
        IWingetService wingetService,
        IMkvToolService mkvToolService,
        IFfmpegService ffmpegService,
        ISubtitleOcrService ocrService,
        ISettingsService settingsService)
    {
        _loggingService = loggingService;
        _toolDetectionService = toolDetectionService;
        _wingetService = wingetService;
        _mkvToolService = mkvToolService;
        _ffmpegService = ffmpegService;
        _ocrService = ocrService;
        _settingsService = settingsService;

        // Initialize commands
        PickMkvCommand = new AsyncRelayCommand(PickMkvAsync);
        ProbeCommand = new AsyncRelayCommand(ProbeTracksAsync, () => State.CanProbe);
        ExtractCommand = new AsyncRelayCommand(ExtractSubtitlesAsync, () => State.CanExtract);
        InstallMkvToolNixCommand = new AsyncRelayCommand(InstallMkvToolNixAsync);
        BrowseMkvToolNixCommand = new RelayCommand(BrowseMkvToolNix);
        ReDetectToolsCommand = new AsyncRelayCommand(ReDetectToolsAsync);

        // Subscribe to state changes to update command states
        State.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(State.CanProbe) or nameof(State.CanExtract))
            {
                // Use Dispatcher to ensure UI thread access
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ProbeCommand.NotifyCanExecuteChanged();
                    ExtractCommand.NotifyCanExecuteChanged();
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
    public IAsyncRelayCommand InstallMkvToolNixCommand { get; }
    public IRelayCommand BrowseMkvToolNixCommand { get; }
    public IAsyncRelayCommand ReDetectToolsCommand { get; }

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
            foreach (var track in result.Tracks)
            {
                State.Tracks.Add(track);
            }

            // Auto-select best track
            var selectedTrack = SelectBestTrack(result.Tracks);
            State.SelectedTrack = selectedTrack;

            State.UpdateProcessingMessage("Analysis completed!");
            State.AddLogMessage($"Found {result.Tracks.Count} subtitle tracks");
            if (selectedTrack != null)
            {
                State.AddLogMessage($"Auto-selected track {selectedTrack.Id}: {selectedTrack.Codec} ({selectedTrack.Language})");
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

    private async Task ExtractSubtitlesAsync()
    {
        if (State.SelectedTrack == null || string.IsNullOrEmpty(State.MkvPath))
            return;

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
                await _ffmpegService.ExtractSubtitleAsync(State.MkvPath, State.SelectedTrack.Id, outputPath);
                State.UpdateProcessingMessage("MP4 extraction completed!");
                State.AddLogMessage($"Subtitles extracted to: {outputPath}");
            }
            else if (State.SelectedTrack.Codec.Contains("S_TEXT/UTF8") || State.SelectedTrack.Codec.Contains("SubRip/SRT"))
            {
                // Direct text extraction
                State.UpdateProcessingMessage("Extracting text subtitles...");
                await _mkvToolService.ExtractTextAsync(State.MkvPath, State.SelectedTrack.Id, outputPath);
                State.UpdateProcessingMessage("Text extraction completed!");
                State.AddLogMessage($"Text subtitles extracted to: {outputPath}");
            }
            else if (State.SelectedTrack.Codec.Contains("PGS") || State.SelectedTrack.Codec.Contains("S_HDMV/PGS"))
            {
                // PGS extraction + OCR
                var tempSupPath = Path.ChangeExtension(outputPath, ".sup");
                
                State.UpdateProcessingMessage("Extracting PGS subtitles...");
                await _mkvToolService.ExtractPgsAsync(State.MkvPath, State.SelectedTrack.Id, tempSupPath);
                State.AddLogMessage($"PGS subtitles extracted to: {tempSupPath}");

                State.UpdateProcessingMessage("Starting OCR conversion...");
                State.AddLogMessage($"Starting OCR conversion to: {outputPath}");
                await _ocrService.OcrSupToSrtAsync(tempSupPath, outputPath, State.OcrLanguage);
                State.UpdateProcessingMessage("OCR conversion completed!");
                State.AddLogMessage($"OCR conversion completed: {outputPath}");

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

        // Detect MKVToolNix
        var mkvStatus = await _toolDetectionService.CheckMkvToolNixAsync();
        State.UpdateToolStatus("MKVToolNix", mkvStatus);

        // Detect Subtitle Edit
        var seStatus = await _toolDetectionService.CheckSubtitleEditAsync();
        State.UpdateToolStatus("SubtitleEdit", seStatus);

        // Detect FFmpeg
        var ffmpegStatus = await _toolDetectionService.CheckFfmpegAsync();
        State.UpdateToolStatus("FFmpeg", ffmpegStatus);

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

    private SubtitleTrack? SelectBestTrack(IReadOnlyList<SubtitleTrack> tracks)
    {
        if (!tracks.Any())
            return null;

        var preferredLanguage = "eng"; // Could be made configurable

        // Priority order based on user preferences:
        // If PreferClosedCaptions: CC forced UTF-8 -> CC UTF-8 -> forced UTF-8 -> UTF-8 -> CC forced PGS -> CC PGS -> forced PGS -> PGS
        // If PreferForced: forced UTF-8 -> UTF-8 -> forced PGS -> PGS -> CC forced UTF-8 -> CC UTF-8 -> CC forced PGS -> CC PGS

        if (State.PreferClosedCaptions)
        {
            // Prefer CC tracks first
            var ccForcedUtf8 = tracks.FirstOrDefault(t => 
                t.IsClosedCaption && t.Forced && 
                t.Codec.Contains("S_TEXT/UTF8") && 
                string.Equals(t.Language, preferredLanguage, StringComparison.OrdinalIgnoreCase));
            if (ccForcedUtf8 != null) return ccForcedUtf8;

            var ccUtf8 = tracks.FirstOrDefault(t => 
                t.IsClosedCaption && 
                t.Codec.Contains("S_TEXT/UTF8") && 
                string.Equals(t.Language, preferredLanguage, StringComparison.OrdinalIgnoreCase));
            if (ccUtf8 != null) return ccUtf8;

            var forcedUtf8 = tracks.FirstOrDefault(t => 
                t.Forced && 
                t.Codec.Contains("S_TEXT/UTF8") && 
                string.Equals(t.Language, preferredLanguage, StringComparison.OrdinalIgnoreCase));
            if (forcedUtf8 != null) return forcedUtf8;

            var utf8 = tracks.FirstOrDefault(t => 
                t.Codec.Contains("S_TEXT/UTF8") && 
                string.Equals(t.Language, preferredLanguage, StringComparison.OrdinalIgnoreCase));
            if (utf8 != null) return utf8;

            var ccForcedPgs = tracks.FirstOrDefault(t => 
                t.IsClosedCaption && t.Forced && 
                (t.Codec.Contains("PGS") || t.Codec.Contains("S_HDMV/PGS")) && 
                string.Equals(t.Language, preferredLanguage, StringComparison.OrdinalIgnoreCase));
            if (ccForcedPgs != null) return ccForcedPgs;

            var ccPgs = tracks.FirstOrDefault(t => 
                t.IsClosedCaption && 
                (t.Codec.Contains("PGS") || t.Codec.Contains("S_HDMV/PGS")) && 
                string.Equals(t.Language, preferredLanguage, StringComparison.OrdinalIgnoreCase));
            if (ccPgs != null) return ccPgs;

            var forcedPgs = tracks.FirstOrDefault(t => 
                t.Forced && 
                (t.Codec.Contains("PGS") || t.Codec.Contains("S_HDMV/PGS")) && 
                string.Equals(t.Language, preferredLanguage, StringComparison.OrdinalIgnoreCase));
            if (forcedPgs != null) return forcedPgs;

            var pgs = tracks.FirstOrDefault(t => 
                (t.Codec.Contains("PGS") || t.Codec.Contains("S_HDMV/PGS")) && 
                string.Equals(t.Language, preferredLanguage, StringComparison.OrdinalIgnoreCase));
            if (pgs != null) return pgs;
        }
        else if (State.PreferForced)
        {
            // Prefer forced tracks first
            var forcedUtf8 = tracks.FirstOrDefault(t => 
                t.Forced && 
                t.Codec.Contains("S_TEXT/UTF8") && 
                string.Equals(t.Language, preferredLanguage, StringComparison.OrdinalIgnoreCase));
            if (forcedUtf8 != null) return forcedUtf8;

            var utf8 = tracks.FirstOrDefault(t => 
                t.Codec.Contains("S_TEXT/UTF8") && 
                string.Equals(t.Language, preferredLanguage, StringComparison.OrdinalIgnoreCase));
            if (utf8 != null) return utf8;

            var forcedPgs = tracks.FirstOrDefault(t => 
                t.Forced && 
                (t.Codec.Contains("PGS") || t.Codec.Contains("S_HDMV/PGS")) && 
                string.Equals(t.Language, preferredLanguage, StringComparison.OrdinalIgnoreCase));
            if (forcedPgs != null) return forcedPgs;

            var pgs = tracks.FirstOrDefault(t => 
                (t.Codec.Contains("PGS") || t.Codec.Contains("S_HDMV/PGS")) && 
                string.Equals(t.Language, preferredLanguage, StringComparison.OrdinalIgnoreCase));
            if (pgs != null) return pgs;

            // Then try CC tracks
            var ccForcedUtf8 = tracks.FirstOrDefault(t => 
                t.IsClosedCaption && t.Forced && 
                t.Codec.Contains("S_TEXT/UTF8") && 
                string.Equals(t.Language, preferredLanguage, StringComparison.OrdinalIgnoreCase));
            if (ccForcedUtf8 != null) return ccForcedUtf8;

            var ccUtf8 = tracks.FirstOrDefault(t => 
                t.IsClosedCaption && 
                t.Codec.Contains("S_TEXT/UTF8") && 
                string.Equals(t.Language, preferredLanguage, StringComparison.OrdinalIgnoreCase));
            if (ccUtf8 != null) return ccUtf8;

            var ccForcedPgs = tracks.FirstOrDefault(t => 
                t.IsClosedCaption && t.Forced && 
                (t.Codec.Contains("PGS") || t.Codec.Contains("S_HDMV/PGS")) && 
                string.Equals(t.Language, preferredLanguage, StringComparison.OrdinalIgnoreCase));
            if (ccForcedPgs != null) return ccForcedPgs;

            var ccPgs = tracks.FirstOrDefault(t => 
                t.IsClosedCaption && 
                (t.Codec.Contains("PGS") || t.Codec.Contains("S_HDMV/PGS")) && 
                string.Equals(t.Language, preferredLanguage, StringComparison.OrdinalIgnoreCase));
            if (ccPgs != null) return ccPgs;
        }

        // If no preferred language found, return the first track
        return tracks.First();
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
