using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SrtExtractor.Models;
using SrtExtractor.Services.Interfaces;

namespace SrtExtractor.ViewModels;

/// <summary>
/// ViewModel for VobSub track analyzer tool.
/// Helps users identify VobSub tracks across MKV files for batch processing in Subtitle Edit.
/// </summary>
public partial class VobSubTrackAnalyzerViewModel : ObservableObject
{
    private readonly ILoggingService _loggingService;
    private readonly INotificationService _notificationService;
    private readonly IMkvToolService _mkvToolService;
    private readonly ISettingsService _settingsService;
    private CancellationTokenSource? _cancellationTokenSource;
    
    private bool _preferForced = true;
    private bool _preferClosedCaptions = false;

    [ObservableProperty]
    private string _selectedPath = string.Empty;

    [ObservableProperty]
    private bool _includeSubfolders = true;

    [ObservableProperty]
    private bool _isFolderSelected;

    [ObservableProperty]
    private ObservableCollection<VobSubTrackInfo> _vobSubTracks = new();

    [ObservableProperty]
    private VobSubTrackInfo? _selectedTrack;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _currentFile = string.Empty;

    [ObservableProperty]
    private double _progressPercentage;

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private string _analysisResults = string.Empty;

    [ObservableProperty]
    private int _totalFiles;

    [ObservableProperty]
    private int _filesWithVobSub;

    [ObservableProperty]
    private Dictionary<string, int> _trackFrequency = new();

    public VobSubTrackAnalyzerViewModel(
        ILoggingService loggingService,
        INotificationService notificationService,
        IMkvToolService mkvToolService,
        ISettingsService settingsService)
    {
        _loggingService = loggingService;
        _notificationService = notificationService;
        _mkvToolService = mkvToolService;
        _settingsService = settingsService;
        
        // Load user preferences
        _ = LoadPreferencesAsync();
    }
    
    private async Task LoadPreferencesAsync()
    {
        try
        {
            var settings = await _settingsService.LoadSettingsAsync();
            _preferForced = settings.PreferForced;
            _preferClosedCaptions = settings.PreferClosedCaptions;
            _loggingService.LogInfo($"Loaded subtitle preferences: PreferForced={_preferForced}, PreferCC={_preferClosedCaptions}");
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Error loading preferences for VobSub analyzer", ex);
        }
    }

    // Computed properties
    public bool HasSelectedPath => !string.IsNullOrEmpty(SelectedPath);
    public bool HasVobSubTracks => VobSubTracks.Count > 0;
    public bool CanStartScan => HasSelectedPath && !IsScanning;
    public bool IsNotScanning => !IsScanning;

    [RelayCommand]
    private void SelectFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select MKV file to analyze",
            Filter = "MKV Files (*.mkv)|*.mkv|All Files (*.*)|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            SelectedPath = dialog.FileName;
            IsFolderSelected = false;
            VobSubTracks.Clear();
            AnalysisResults = string.Empty;
            _loggingService.LogInfo($"Selected file for VobSub track analysis: {SelectedPath}");
        }
    }

    [RelayCommand]
    private void SelectFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select folder containing MKV files"
        };

        if (dialog.ShowDialog() == true)
        {
            SelectedPath = dialog.FolderName;
            IsFolderSelected = true;
            VobSubTracks.Clear();
            AnalysisResults = string.Empty;
            _loggingService.LogInfo($"Selected folder for VobSub track analysis: {SelectedPath}");
        }
    }

    [RelayCommand]
    private async Task ScanForVobSubTracks()
    {
        if (string.IsNullOrEmpty(SelectedPath))
            return;

        try
        {
            IsScanning = true;
            _cancellationTokenSource = new CancellationTokenSource();
            VobSubTracks.Clear();
            TotalFiles = 0;
            FilesWithVobSub = 0;
            ProgressPercentage = 0;
            ProgressText = "Scanning for MKV files...";

            _loggingService.LogInfo($"Starting VobSub track scan: {SelectedPath}");

            await Task.Run(async () =>
            {
                string[] mkvFiles;
                
                // Check if it's a file or folder
                if (File.Exists(SelectedPath))
                {
                    // Single file
                    mkvFiles = new[] { SelectedPath };
                    _loggingService.LogInfo("Scanning single MKV file");
                }
                else if (Directory.Exists(SelectedPath))
                {
                    // Folder
                    var searchOption = IncludeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                    mkvFiles = Directory.GetFiles(SelectedPath, "*.mkv", searchOption);
                    _loggingService.LogInfo($"Found {mkvFiles.Length} MKV files in folder");
                }
                else
                {
                    throw new FileNotFoundException($"Path not found: {SelectedPath}");
                }
                
                TotalFiles = mkvFiles.Length;
                _loggingService.LogInfo($"Scanning {TotalFiles} MKV file(s)");

                var processedCount = 0;
                var vobSubTracksList = new List<VobSubTrackInfo>();

                foreach (var mkvFile in mkvFiles)
                {
                    if (_cancellationTokenSource.Token.IsCancellationRequested)
                        break;

                    var fileName = Path.GetFileName(mkvFile);
                    CurrentFile = fileName;
                    ProgressText = $"Scanning {fileName}...";

                    try
                    {
                        // Probe the MKV to get all tracks
                        var probeResult = await _mkvToolService.ProbeAsync(mkvFile, _cancellationTokenSource.Token);

                        // Filter for VobSub/image-based tracks
                        var vobSubTracks = probeResult.Tracks.Where(t => 
                            t.Codec.Contains("VobSub", StringComparison.OrdinalIgnoreCase) ||
                            t.Codec.Contains("HDMV PGS", StringComparison.OrdinalIgnoreCase) ||
                            t.Codec.Contains("DVB", StringComparison.OrdinalIgnoreCase) ||
                            t.TrackType.Contains("image", StringComparison.OrdinalIgnoreCase)).ToList();

                        if (vobSubTracks.Any())
                        {
                            FilesWithVobSub++;
                            
                            foreach (var track in vobSubTracks)
                            {
                                var trackInfo = new VobSubTrackInfo(
                                    fileName: fileName,
                                    filePath: mkvFile,
                                    trackNumber: track.TrackId,
                                    codec: track.Codec,
                                    language: track.Language,
                                    title: track.Title,
                                    isForced: track.IsForced || track.Forced,
                                    isDefault: track.IsDefault,
                                    frameCount: track.FrameCount
                                );

                                vobSubTracksList.Add(trackInfo);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _loggingService.LogError($"Error scanning {fileName}", ex);
                    }

                    processedCount++;
                    ProgressPercentage = (double)processedCount / TotalFiles * 100;
                    ProgressText = $"Scanned {processedCount} of {TotalFiles} files...";
                }

                // Add all tracks to UI on the main thread (non-blocking)
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    foreach (var track in vobSubTracksList)
                    {
                        VobSubTracks.Add(track);
                    }
                });

                _loggingService.LogInfo($"Scan complete: Found {vobSubTracksList.Count} VobSub tracks in {FilesWithVobSub} files");
            });

            ProgressText = $"Scan complete: {VobSubTracks.Count} VobSub tracks found";
            ApplyRecommendations();
            GenerateAnalysisReport();
            OnPropertyChanged(nameof(HasVobSubTracks));
        }
        catch (OperationCanceledException)
        {
            ProgressText = "Scan cancelled by user.";
            _loggingService.LogInfo("VobSub track scan cancelled by user");
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Error during VobSub track scan", ex);
            _notificationService.ShowError($"Error during scan: {ex.Message}", "Error");
        }
        finally
        {
            IsScanning = false;
            CurrentFile = string.Empty;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    [RelayCommand]
    private void CancelScan()
    {
        _cancellationTokenSource?.Cancel();
        _loggingService.LogInfo("User requested cancellation of VobSub track scan");
    }

    /// <summary>
    /// Apply recommendations to tracks based on user preferences.
    /// Groups tracks by file and recommends the best track per file.
    /// </summary>
    private void ApplyRecommendations()
    {
        if (!VobSubTracks.Any())
            return;

        _loggingService.LogInfo($"Applying recommendations with preferences: PreferForced={_preferForced}, PreferCC={_preferClosedCaptions}");

        // Group tracks by file
        var tracksByFile = VobSubTracks.GroupBy(t => t.FilePath);

        foreach (var fileGroup in tracksByFile)
        {
            var tracks = fileGroup.ToList();
            if (!tracks.Any()) continue;

            VobSubTrackInfo? recommendedTrack = null;

            if (_preferClosedCaptions)
            {
                // Prefer SDH/CC tracks
                var sdhTracks = tracks.Where(t => t.IsSdh).ToList();
                if (sdhTracks.Any())
                {
                    recommendedTrack = sdhTracks.First();
                    recommendedTrack.RecommendationReason = "✅ Recommended (SDH - matches your preference)";
                    _loggingService.LogInfo($"Recommended SDH track #{recommendedTrack.TrackNumber} for {recommendedTrack.FileName}");
                }
            }
            else if (_preferForced)
            {
                // Prefer forced tracks
                var forcedTracks = tracks.Where(t => t.IsForced).ToList();
                if (forcedTracks.Any())
                {
                    recommendedTrack = forcedTracks.First();
                    recommendedTrack.RecommendationReason = "✅ Recommended (Forced - matches your preference)";
                    _loggingService.LogInfo($"Recommended Forced track #{recommendedTrack.TrackNumber} for {recommendedTrack.FileName}");
                }
            }

            // If no match based on preference, recommend the first non-commentary track
            if (recommendedTrack == null)
            {
                var nonCommentaryTracks = tracks.Where(t => !t.IsCommentary).ToList();
                if (nonCommentaryTracks.Any())
                {
                    recommendedTrack = nonCommentaryTracks.First();
                    recommendedTrack.RecommendationReason = _preferForced 
                        ? "✅ Recommended (No Forced track found, using standard track)"
                        : _preferClosedCaptions
                            ? "✅ Recommended (No SDH/CC track found, using standard track)"
                            : "✅ Recommended (First available)";
                    _loggingService.LogInfo($"Recommended default track #{recommendedTrack.TrackNumber} for {recommendedTrack.FileName}");
                }
                else
                {
                    // Fallback to first track
                    recommendedTrack = tracks.First();
                    recommendedTrack.RecommendationReason = "✅ Recommended (Only available)";
                }
            }
        }
    }

    [RelayCommand]
    private void CopyInstructions()
    {
        var instructions = GenerateSubtitleEditInstructions();
        Clipboard.SetText(instructions);
        _notificationService.ShowSuccess("Instructions copied to clipboard!", "Success");
        _loggingService.LogInfo("Copied Subtitle Edit instructions to clipboard");
    }

    [RelayCommand]
    private void ExportReport()
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export VobSub Track Analysis Report",
                Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                FileName = $"VobSub_Track_Analysis_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (dialog.ShowDialog() == true)
            {
                var report = GenerateFullReport();
                File.WriteAllText(dialog.FileName, report);
                _notificationService.ShowSuccess($"Report exported successfully!\n\n{dialog.FileName}", "Export Complete");
                _loggingService.LogInfo($"Exported VobSub track analysis report to: {dialog.FileName}");
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Error exporting VobSub track report", ex);
            _notificationService.ShowError($"Error exporting report: {ex.Message}", "Error");
        }
    }

    private void GenerateAnalysisReport()
    {
        if (VobSubTracks.Count == 0)
        {
            AnalysisResults = "No VobSub tracks found.";
            return;
        }

        var report = new System.Text.StringBuilder();
        report.AppendLine("═══ VobSub Track Analysis ═══\n");
        
        // Show user preferences
        var preference = _preferForced ? "Forced" : _preferClosedCaptions ? "SDH/CC" : "Standard";
        report.AppendLine($"🎯 Your Preference: {preference}");
        report.AppendLine($"📁 Total MKV Files: {TotalFiles}");
        report.AppendLine($"🎬 Files with VobSub: {FilesWithVobSub}");
        report.AppendLine($"📊 Total VobSub Tracks: {VobSubTracks.Count}\n");
        
        // Show recommendation summary
        var recommendedTracks = VobSubTracks.Where(t => !string.IsNullOrEmpty(t.RecommendationReason)).ToList();
        if (recommendedTracks.Any())
        {
            report.AppendLine("═══ Recommended Tracks ═══");
            foreach (var track in recommendedTracks)
            {
                report.AppendLine($"  {track.FileName} → Track #{track.TrackNumber}");
                report.AppendLine($"    {track.RecommendationReason}");
            }
            report.AppendLine();
        }

        // Group by track number
        var tracksByNumber = VobSubTracks.GroupBy(t => t.TrackNumber).OrderBy(g => g.Key);
        
        report.AppendLine("═══ Track Number Distribution ═══");
        foreach (var group in tracksByNumber)
        {
            report.AppendLine($"  Track #{group.Key}: {group.Count()} occurrences");
            
            // Show track types
            var types = group.GroupBy(t => t.TrackType);
            foreach (var type in types)
            {
                report.AppendLine($"    └─ {type.Key}: {type.Count()}");
            }
        }

        report.AppendLine("\n═══ Common Track Patterns ═══");
        var commonPatterns = VobSubTracks
            .GroupBy(t => new { t.TrackNumber, t.Language, t.TrackType })
            .Where(g => g.Count() > 1)
            .OrderByDescending(g => g.Count())
            .Take(5);

        foreach (var pattern in commonPatterns)
        {
            report.AppendLine($"  Track #{pattern.Key.TrackNumber} ({pattern.Key.Language} - {pattern.Key.TrackType}): {pattern.Count()} files");
        }

        AnalysisResults = report.ToString();
    }

    private string GenerateSubtitleEditInstructions()
    {
        var instructions = new System.Text.StringBuilder();
        instructions.AppendLine("═══ Subtitle Edit - VobSub Batch Conversion Guide ═══\n");
        
        var preference = _preferForced ? "Forced" : _preferClosedCaptions ? "SDH/CC" : "Standard";
        instructions.AppendLine($"🎯 Your Preference: {preference}\n");
        
        instructions.AppendLine("✅ Track Numbering");
        instructions.AppendLine("The track numbers shown below match what Subtitle Edit displays.");
        instructions.AppendLine("Use these exact track numbers in Subtitle Edit's batch converter.\n");
        
        instructions.AppendLine("1. Open Subtitle Edit");
        instructions.AppendLine("2. Go to: Tools → Batch Convert");
        instructions.AppendLine("3. Click 'Add' and select your MKV file(s)");
        instructions.AppendLine("4. Set 'Convert to format': SubRip (.srt)");
        instructions.AppendLine("5. Choose 'OCR' settings:");
        instructions.AppendLine("   - Select OCR engine (Tesseract recommended)");
        instructions.AppendLine("   - Set OCR language");
        instructions.AppendLine("6. Click 'Convert'\n");

        // Show recommended tracks first
        var recommendedTracks = VobSubTracks.Where(t => !string.IsNullOrEmpty(t.RecommendationReason)).ToList();
        if (recommendedTracks.Any())
        {
            instructions.AppendLine("═══ Recommended Tracks (Based on Your Preferences) ═══\n");
            
            var groupedRecommended = recommendedTracks
                .GroupBy(t => t.FileName)
                .OrderBy(g => g.Key);

            foreach (var fileGroup in groupedRecommended)
            {
                var track = fileGroup.First();
                instructions.AppendLine($"📁 {track.FileName}");
                instructions.AppendLine($"   → Track #{track.TrackNumber} ({track.Language} - {track.TrackType})");
                instructions.AppendLine($"   {track.RecommendationReason}");
                instructions.AppendLine();
            }
        }

        if (VobSubTracks.Any())
        {
            instructions.AppendLine("═══ All Track Numbers Found ═══\n");
            
            var tracksByNumber = VobSubTracks
                .GroupBy(t => t.TrackNumber)
                .OrderBy(g => g.Key);

            foreach (var group in tracksByNumber)
            {
                var example = group.First();
                instructions.AppendLine($"Track #{example.TrackNumber}: {example.Language} - {example.TrackType}");
                
                if (!string.IsNullOrEmpty(example.Title))
                {
                    instructions.AppendLine($"  Title: \"{example.Title}\"");
                }
                
                instructions.AppendLine($"  Appears in {group.Count()} file(s)");
                instructions.AppendLine();
            }
        }

        return instructions.ToString();
    }

    private string GenerateFullReport()
    {
        var report = new System.Text.StringBuilder();
        report.AppendLine("═══════════════════════════════════════════════════════");
        report.AppendLine("     VobSub Track Analysis Report");
        report.AppendLine($"     Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine("═══════════════════════════════════════════════════════\n");

        if (IsFolderSelected)
        {
            report.AppendLine($"Folder: {SelectedPath}");
            report.AppendLine($"Include Subfolders: {IncludeSubfolders}\n");
        }
        else
        {
            report.AppendLine($"File: {SelectedPath}\n");
        }

        report.AppendLine(AnalysisResults);
        report.AppendLine("\n═══ Detailed Track List ═══\n");

        var groupedByFile = VobSubTracks.GroupBy(t => t.FileName).OrderBy(g => g.Key);

        foreach (var fileGroup in groupedByFile)
        {
            report.AppendLine($"📁 {fileGroup.Key}");
            foreach (var track in fileGroup.OrderBy(t => t.TrackNumber))
            {
                report.AppendLine($"   {track.DisplayName}");
                report.AppendLine($"      Codec: {track.Codec}");
                if (track.FrameCount > 0)
                {
                    report.AppendLine($"      Frames: {track.FrameCount}");
                }
                report.AppendLine();
            }
        }

        report.AppendLine("\n" + GenerateSubtitleEditInstructions());

        return report.ToString();
    }

    // Property change notifications for computed properties
    partial void OnSelectedPathChanged(string value)
    {
        OnPropertyChanged(nameof(HasSelectedPath));
        OnPropertyChanged(nameof(CanStartScan));
    }

    partial void OnVobSubTracksChanged(ObservableCollection<VobSubTrackInfo> value)
    {
        OnPropertyChanged(nameof(HasVobSubTracks));
    }

    partial void OnIsScanningChanged(bool value)
    {
        OnPropertyChanged(nameof(CanStartScan));
        OnPropertyChanged(nameof(IsNotScanning));
    }
}
