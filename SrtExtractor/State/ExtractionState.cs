using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using SrtExtractor.Models;

namespace SrtExtractor.State;

/// <summary>
/// Observable state object for MVVM data binding.
/// Contains all the state needed for the UI and business logic.
/// </summary>
public partial class ExtractionState : ObservableObject
{
    // File Management
    [ObservableProperty]
    private string? _mkvPath;

    [ObservableProperty]
    private ObservableCollection<SubtitleTrack> _tracks = new();

    [ObservableProperty]
    private SubtitleTrack? _selectedTrack;

    // Tool Status
    [ObservableProperty]
    private ToolStatus _mkvToolNixStatus = new(false, null, null, null);

    [ObservableProperty]
    private ToolStatus _subtitleEditStatus = new(false, null, null, null);

    // Settings
    [ObservableProperty]
    private bool _preferForced = true;

    [ObservableProperty]
    private string _ocrLanguage = "eng";

    [ObservableProperty]
    private string _fileNamePattern = "{basename}.{lang}{forced}.srt";

    // UI State
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _logText = string.Empty;

    // Computed Properties
    public bool AreToolsAvailable => MkvToolNixStatus.IsInstalled && SubtitleEditStatus.IsInstalled;
    
    public bool CanProbe => !string.IsNullOrEmpty(MkvPath) && AreToolsAvailable && !IsBusy;
    
    public bool CanExtract => SelectedTrack != null && AreToolsAvailable && !IsBusy;

    // Available OCR languages
    public string[] AvailableLanguages { get; } = { "eng", "spa", "fra", "deu", "ita", "por", "rus", "jpn", "kor", "chi" };

    /// <summary>
    /// Add a log message to the UI log display.
    /// </summary>
    /// <param name="message">The message to add</param>
    public void AddLogMessage(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var logEntry = $"[{timestamp}] {message}";
        
        LogText += logEntry + Environment.NewLine;
        
        // Keep only the last 1000 lines to prevent memory issues
        var lines = LogText.Split('\n');
        if (lines.Length > 1000)
        {
            LogText = string.Join(Environment.NewLine, lines.Skip(lines.Length - 1000));
        }
    }

    /// <summary>
    /// Clear the log display.
    /// </summary>
    public void ClearLog()
    {
        LogText = string.Empty;
    }

    /// <summary>
    /// Update tool status and refresh computed properties.
    /// </summary>
    /// <param name="toolName">Name of the tool (MKVToolNix or SubtitleEdit)</param>
    /// <param name="status">New tool status</param>
    public void UpdateToolStatus(string toolName, ToolStatus status)
    {
        if (string.Equals(toolName, "MKVToolNix", StringComparison.OrdinalIgnoreCase))
        {
            MkvToolNixStatus = status;
        }
        else if (string.Equals(toolName, "SubtitleEdit", StringComparison.OrdinalIgnoreCase))
        {
            SubtitleEditStatus = status;
        }

        // Notify that computed properties have changed
        OnPropertyChanged(nameof(AreToolsAvailable));
        OnPropertyChanged(nameof(CanProbe));
        OnPropertyChanged(nameof(CanExtract));
    }

    /// <summary>
    /// Generate output filename based on the current pattern and selected track.
    /// </summary>
    /// <param name="mkvPath">Path to the source MKV file</param>
    /// <param name="track">The selected subtitle track</param>
    /// <returns>Generated output filename</returns>
    public string GenerateOutputFilename(string mkvPath, SubtitleTrack track)
    {
        var baseName = Path.GetFileNameWithoutExtension(mkvPath);
        var directory = Path.GetDirectoryName(mkvPath) ?? "";
        
        var forcedSuffix = track.Forced ? ".forced" : "";
        var pattern = FileNamePattern
            .Replace("{basename}", baseName)
            .Replace("{lang}", track.Language)
            .Replace("{forced}", forcedSuffix);

        return Path.Combine(directory, pattern);
    }

    /// <summary>
    /// Reset the state to initial values.
    /// </summary>
    public void Reset()
    {
        MkvPath = null;
        Tracks.Clear();
        SelectedTrack = null;
        IsBusy = false;
        ClearLog();
    }
}
