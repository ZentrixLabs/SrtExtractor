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
    private readonly ISubtitleOcrService _ocrService;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private ExtractionState _state = new();

    public MainViewModel(
        ILoggingService loggingService,
        IToolDetectionService toolDetectionService,
        IWingetService wingetService,
        IMkvToolService mkvToolService,
        ISubtitleOcrService ocrService,
        ISettingsService settingsService)
    {
        _loggingService = loggingService;
        _toolDetectionService = toolDetectionService;
        _wingetService = wingetService;
        _mkvToolService = mkvToolService;
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
                Title = "Select MKV File",
                Filter = "MKV Files (*.mkv)|*.mkv|All Files (*.*)|*.*",
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
            State.AddLogMessage("Probing MKV file for subtitle tracks...");

            var result = await _mkvToolService.ProbeAsync(State.MkvPath);
            
            State.Tracks.Clear();
            foreach (var track in result.Tracks)
            {
                State.Tracks.Add(track);
            }

            // Auto-select best track
            var selectedTrack = SelectBestTrack(result.Tracks);
            State.SelectedTrack = selectedTrack;

            State.AddLogMessage($"Found {result.Tracks.Count} subtitle tracks");
            if (selectedTrack != null)
            {
                State.AddLogMessage($"Auto-selected track {selectedTrack.Id}: {selectedTrack.Codec} ({selectedTrack.Language})");
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to probe MKV tracks", ex);
            State.AddLogMessage($"Error probing tracks: {ex.Message}");
            MessageBox.Show($"Failed to probe MKV file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            State.IsBusy = false;
        }
    }

    private async Task ExtractSubtitlesAsync()
    {
        if (State.SelectedTrack == null || string.IsNullOrEmpty(State.MkvPath))
            return;

        try
        {
            State.IsBusy = true;
            State.AddLogMessage($"Extracting subtitle track {State.SelectedTrack.Id}...");

            var outputPath = State.GenerateOutputFilename(State.MkvPath, State.SelectedTrack);

            if (State.SelectedTrack.Codec.Contains("S_TEXT/UTF8"))
            {
                // Direct text extraction
                await _mkvToolService.ExtractTextAsync(State.MkvPath, State.SelectedTrack.Id, outputPath);
                State.AddLogMessage($"Text subtitles extracted to: {outputPath}");
            }
            else if (State.SelectedTrack.Codec.Contains("S_HDMV/PGS"))
            {
                // PGS extraction + OCR
                var tempSupPath = Path.ChangeExtension(outputPath, ".sup");
                await _mkvToolService.ExtractPgsAsync(State.MkvPath, State.SelectedTrack.Id, tempSupPath);
                State.AddLogMessage($"PGS subtitles extracted to: {tempSupPath}");

                State.AddLogMessage($"Starting OCR conversion to: {outputPath}");
                await _ocrService.OcrSupToSrtAsync(tempSupPath, outputPath, State.OcrLanguage);
                State.AddLogMessage($"OCR conversion completed: {outputPath}");

                // Clean up temporary SUP file
                try
                {
                    File.Delete(tempSupPath);
                }
                catch
                {
                    // Ignore cleanup errors
                }
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
        }
    }

    private async Task InstallMkvToolNixAsync()
    {
        try
        {
            State.IsBusy = true;
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

        if (State.AreToolsAvailable)
        {
            State.AddLogMessage("All required tools are available");
        }
        else
        {
            State.AddLogMessage("Some required tools are missing. Use the install buttons to install them.");
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

        // Priority order: forced UTF-8 -> UTF-8 -> forced PGS -> PGS
        var preferredLanguage = "eng"; // Could be made configurable

        // First, try to find forced UTF-8 in preferred language
        var forcedUtf8 = tracks.FirstOrDefault(t => 
            t.Forced && 
            t.Codec.Contains("S_TEXT/UTF8") && 
            string.Equals(t.Language, preferredLanguage, StringComparison.OrdinalIgnoreCase));
        
        if (forcedUtf8 != null)
            return forcedUtf8;

        // Then, try any UTF-8 in preferred language
        var utf8 = tracks.FirstOrDefault(t => 
            t.Codec.Contains("S_TEXT/UTF8") && 
            string.Equals(t.Language, preferredLanguage, StringComparison.OrdinalIgnoreCase));
        
        if (utf8 != null)
            return utf8;

        // Then, try forced PGS in preferred language
        var forcedPgs = tracks.FirstOrDefault(t => 
            t.Forced && 
            t.Codec.Contains("S_HDMV/PGS") && 
            string.Equals(t.Language, preferredLanguage, StringComparison.OrdinalIgnoreCase));
        
        if (forcedPgs != null)
            return forcedPgs;

        // Finally, try any PGS in preferred language
        var pgs = tracks.FirstOrDefault(t => 
            t.Codec.Contains("S_HDMV/PGS") && 
            string.Equals(t.Language, preferredLanguage, StringComparison.OrdinalIgnoreCase));
        
        if (pgs != null)
            return pgs;

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

    #endregion
}
