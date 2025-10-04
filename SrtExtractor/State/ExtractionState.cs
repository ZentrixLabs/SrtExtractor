using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
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

    [ObservableProperty]
    private bool _hasProbedFile = false;

    [ObservableProperty]
    private bool _showNoTracksError = false;


    // Tool Status
    [ObservableProperty]
    private ToolStatus _mkvToolNixStatus = new(false, null, null, null);

    [ObservableProperty]
    private ToolStatus _subtitleEditStatus = new(false, null, null, null);

    [ObservableProperty]
    private ToolStatus _ffmpegStatus = new(false, null, null, null);

    // Settings
    [ObservableProperty]
    private bool _preferForced = true;

    [ObservableProperty]
    private bool _preferClosedCaptions = false;

    [ObservableProperty]
    private string _ocrLanguage = "eng";

    [ObservableProperty]
    private string _fileNamePattern = "{basename}.{lang}{forced}.srt";

    // Multi-Pass Correction Settings
    [ObservableProperty]
    private bool _enableMultiPassCorrection = true;

    [ObservableProperty]
    private int _maxCorrectionPasses = 3;

    [ObservableProperty]
    private bool _useSmartConvergence = true;

    [ObservableProperty]
    private string _correctionMode = "Standard"; // Quick, Standard, Thorough

    // Network Detection
    [ObservableProperty]
    private bool _isNetworkFile;

    [ObservableProperty]
    private string _networkWarningMessage = "";

    [ObservableProperty]
    private double _estimatedProcessingTimeMinutes;

    [ObservableProperty]
    private string _formattedFileSize = "";

    [ObservableProperty]
    private string _networkDriveInfo = "";

    // Batch Mode
    [ObservableProperty]
    private bool _isBatchMode;

    [ObservableProperty]
    private double _queueColumnWidth = 0;

    [ObservableProperty]
    private ObservableCollection<BatchFile> _batchQueue = new();

    [ObservableProperty]
    private int _batchCompletedCount = 0;

    [ObservableProperty]
    private int _batchErrorCount = 0;

    [ObservableProperty]
    private int _batchPendingCount = 0;

    [ObservableProperty]
    private int _currentBatchIndex;

    [ObservableProperty]
    private int _lastProcessedBatchIndex = -1;

    [ObservableProperty]
    private int _totalBatchFiles;

    [ObservableProperty]
    private string _batchProgressMessage = "";

    [ObservableProperty]
    private double _batchProgressPercentage;

    [ObservableProperty]
    private bool _showSettingsOnStartup;

    // Recent Files
    [ObservableProperty]
    private ObservableCollection<string> _recentFiles = new();

    // Events
    public event EventHandler? PreferencesChanged;

    partial void OnPreferForcedChanged(bool value)
    {
        if (value)
        {
            PreferClosedCaptions = false;
            FileNamePattern = "{basename}.{lang}{forced}.srt";
        }
        PreferencesChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnPreferClosedCaptionsChanged(bool value)
    {
        if (value)
        {
            PreferForced = false;
            FileNamePattern = "{basename}.{lang}{cc}.srt";
        }
        PreferencesChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnIsBatchModeChanged(bool value)
    {
        // Notify computed properties that depend on IsBatchMode
        OnPropertyChanged(nameof(ShowBatchMode));
        OnPropertyChanged(nameof(ShowSingleFileMode));
    }

    partial void OnCorrectionModeChanged(string value)
    {
        // Update max passes based on correction mode
        MaxCorrectionPasses = value switch
        {
            "Quick" => 1,
            "Standard" => 3,
            "Thorough" => 5,
            _ => 3
        };
        
        // Update convergence setting
        UseSmartConvergence = value != "Thorough";
        
        PreferencesChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnMkvPathChanged(string? value)
    {
        OnPropertyChanged(nameof(CanProbe));
        OnPropertyChanged(nameof(CanExtract));
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanProbe));
        OnPropertyChanged(nameof(CanExtract));
        OnPropertyChanged(nameof(CanProcessBatch));
    }

    partial void OnSelectedTrackChanged(SubtitleTrack? value)
    {
        OnPropertyChanged(nameof(CanExtract));
    }

    // UI State
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isProcessing = false;

    [ObservableProperty]
    private string _processingMessage = "";

    [ObservableProperty]
    private string _logText = string.Empty;

    // Enhanced Progress Information
    [ObservableProperty]
    private double _progressPercentage = 0;

    [ObservableProperty]
    private string _currentOperation = "";

    [ObservableProperty]
    private string _currentFile = "";

    [ObservableProperty]
    private string _processingSpeed = "";

    [ObservableProperty]
    private string _estimatedTimeRemaining = "";

    [ObservableProperty]
    private string _memoryUsage = "";

    [ObservableProperty]
    private DateTime _operationStartTime;

    [ObservableProperty]
    private long _bytesProcessed = 0;

    [ObservableProperty]
    private long _totalBytes = 0;

    [ObservableProperty]
    private string _detailedProgressMessage = "";

    // Computed Properties
    public bool AreToolsAvailable => MkvToolNixStatus.IsInstalled && SubtitleEditStatus.IsInstalled && FfmpegStatus.IsInstalled;
    
    public bool CanProbe => !string.IsNullOrEmpty(MkvPath) && AreToolsAvailable && !IsBusy;
    
    public bool CanExtract => SelectedTrack != null && AreToolsAvailable && !IsBusy;

    // Batch Mode Computed Properties
    public bool CanProcessBatch => BatchQueue.Any() && AreToolsAvailable && !IsBusy;

    public bool HasBatchQueue => BatchQueue.Any();

    public string BatchProgressText => $"Processing {CurrentBatchIndex + 1} of {TotalBatchFiles} files";

    public bool ShowNetworkWarning => IsNetworkFile && !string.IsNullOrEmpty(MkvPath);

    public bool ShowBatchMode => IsBatchMode;

    public bool ShowSingleFileMode => !IsBatchMode;

    public bool ShowNoTracksWarning => ShowNoTracksError;

    // Resume Batch Computed Properties
    public bool CanResumeBatch => BatchQueue.Any() && LastProcessedBatchIndex >= 0 && LastProcessedBatchIndex < BatchQueue.Count - 1 && AreToolsAvailable && !IsBusy;

    public bool ShowResumeBatchButton => CanResumeBatch && BatchCompletedCount > 0;

    // Enhanced Progress Computed Properties
    public string FormattedBytesProcessed => FormatBytes(BytesProcessed);

    public string FormattedTotalBytes => FormatBytes(TotalBytes);

    public string ProgressDetails => $"({FormattedBytesProcessed} / {FormattedTotalBytes})";

    public string ElapsedTime => IsProcessing ? FormatTimeSpan(DateTime.Now - OperationStartTime) : "00:00:00";

    public string FormattedProgressPercentage => $"{ProgressPercentage:F1}%";

    // Available OCR languages
    public string[] AvailableLanguages { get; } = { "eng", "spa", "fra", "deu", "ita", "por", "rus", "jpn", "kor", "chi" };

    // Available correction modes
    public string[] AvailableCorrectionModes { get; } = { "Quick", "Standard", "Thorough" };

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
        else if (string.Equals(toolName, "FFmpeg", StringComparison.OrdinalIgnoreCase))
        {
            FfmpegStatus = status;
        }

        // Notify that computed properties have changed
        OnPropertyChanged(nameof(AreToolsAvailable));
        OnPropertyChanged(nameof(CanProbe));
        OnPropertyChanged(nameof(CanExtract));
        OnPropertyChanged(nameof(CanProcessBatch));
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
        var ccSuffix = track.IsClosedCaption ? ".cc" : "";
        var pattern = FileNamePattern
            .Replace("{basename}", baseName)
            .Replace("{lang}", track.Language)
            .Replace("{forced}", forcedSuffix)
            .Replace("{cc}", ccSuffix);

        return Path.Combine(directory, pattern);
    }

    /// <summary>
    /// Update processing message.
    /// </summary>
    public void UpdateProcessingMessage(string message)
    {
        ProcessingMessage = message;
    }

    /// <summary>
    /// Start processing with initial message.
    /// </summary>
    public void StartProcessing(string message)
    {
        IsProcessing = true;
        ProcessingMessage = message;
    }

    /// <summary>
    /// Stop processing and reset message.
    /// </summary>
    public void StopProcessing()
    {
        IsProcessing = false;
        ProcessingMessage = "";
    }

    /// <summary>
    /// Update network detection properties for the current file.
    /// </summary>
    /// <param name="isNetwork">Whether the file is on a network drive</param>
    /// <param name="estimatedMinutes">Estimated processing time in minutes</param>
    /// <param name="formattedSize">Formatted file size string</param>
    /// <param name="networkDriveInfo">Network drive information</param>
    public void UpdateNetworkDetection(bool isNetwork, double estimatedMinutes, string formattedSize, string networkDriveInfo)
    {
        IsNetworkFile = isNetwork;
        EstimatedProcessingTimeMinutes = estimatedMinutes;
        FormattedFileSize = formattedSize;
        NetworkDriveInfo = networkDriveInfo;

        if (isNetwork)
        {
            NetworkWarningMessage = $"File is on network drive {networkDriveInfo}\n" +
                                  $"File size: {formattedSize}\n" +
                                  $"Estimated processing time: ~{FormatEstimatedTime(estimatedMinutes)}";
        }
        else
        {
            NetworkWarningMessage = "";
        }

        // Notify computed properties
        OnPropertyChanged(nameof(ShowNetworkWarning));
    }

    /// <summary>
    /// Add a file to the batch queue.
    /// </summary>
    /// <param name="filePath">Path to the file to add</param>
    /// <returns>True if file was added, false if already exists</returns>
    public bool AddToBatchQueue(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || BatchQueue.Any(f => f.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var batchFile = new BatchFile { FilePath = filePath };
        batchFile.UpdateFromFileSystem();
        BatchQueue.Add(batchFile);

        TotalBatchFiles = BatchQueue.Count;
        UpdateBatchStatistics();
        
        OnPropertyChanged(nameof(CanProcessBatch));
        OnPropertyChanged(nameof(HasBatchQueue));

        return true;
    }

    /// <summary>
    /// Remove a file from the batch queue.
    /// </summary>
    /// <param name="batchFile">The batch file to remove</param>
    public void RemoveFromBatchQueue(BatchFile batchFile)
    {
        if (BatchQueue.Remove(batchFile))
        {
            TotalBatchFiles = BatchQueue.Count;
            UpdateBatchStatistics();
            OnPropertyChanged(nameof(CanProcessBatch));
            OnPropertyChanged(nameof(HasBatchQueue));
        }
    }

    /// <summary>
    /// Clear all files from the batch queue.
    /// </summary>
    public void ClearBatchQueue()
    {
        BatchQueue.Clear();
        TotalBatchFiles = 0;
        CurrentBatchIndex = 0;
        LastProcessedBatchIndex = -1;
        BatchProgressMessage = "";
        BatchProgressPercentage = 0;
        
        UpdateBatchStatistics();
        
        OnPropertyChanged(nameof(CanProcessBatch));
        OnPropertyChanged(nameof(HasBatchQueue));
    }

    /// <summary>
    /// Clear only completed files from the batch queue.
    /// </summary>
    public void ClearCompletedBatchItems()
    {
        var completedItems = BatchQueue.Where(f => f.Status == BatchFileStatus.Completed).ToList();
        foreach (var item in completedItems)
        {
            BatchQueue.Remove(item);
        }
        
        TotalBatchFiles = BatchQueue.Count;
        UpdateBatchStatistics();
        
        OnPropertyChanged(nameof(CanProcessBatch));
        OnPropertyChanged(nameof(HasBatchQueue));
    }

    /// <summary>
    /// Update batch statistics based on current queue status.
    /// </summary>
    public void UpdateBatchStatistics()
    {
        BatchCompletedCount = BatchQueue.Count(f => f.Status == BatchFileStatus.Completed);
        BatchErrorCount = BatchQueue.Count(f => f.Status == BatchFileStatus.Error);
        BatchPendingCount = BatchQueue.Count(f => f.Status == BatchFileStatus.Pending);
        
        // Update computed properties
        OnPropertyChanged(nameof(CanProcessBatch));
        OnPropertyChanged(nameof(CanResumeBatch));
        OnPropertyChanged(nameof(ShowResumeBatchButton));
    }

    /// <summary>
    /// Update batch progress.
    /// </summary>
    /// <param name="currentIndex">Current file index</param>
    /// <param name="totalFiles">Total number of files</param>
    /// <param name="message">Progress message</param>
    public void UpdateBatchProgress(int currentIndex, int totalFiles, string message)
    {
        CurrentBatchIndex = currentIndex;
        TotalBatchFiles = totalFiles;
        BatchProgressMessage = message;
        BatchProgressPercentage = totalFiles > 0 ? (currentIndex / (double)totalFiles) * 100 : 0;
        
        OnPropertyChanged(nameof(BatchProgressText));
    }

    /// <summary>
    /// Format estimated time as a human-readable string.
    /// </summary>
    /// <param name="minutes">Time in minutes</param>
    /// <returns>Formatted time string</returns>
    private static string FormatEstimatedTime(double minutes)
    {
        if (minutes < 1)
            return "< 1 min";
        else if (minutes < 60)
            return $"{minutes:F1} min";
        else
        {
            var hours = (int)(minutes / 60);
            var minutesRemainder = (int)(minutes % 60);
            return $"{hours}h {minutesRemainder}m";
        }
    }

    /// <summary>
    /// Format bytes as a human-readable string.
    /// </summary>
    /// <param name="bytes">Number of bytes</param>
    /// <returns>Formatted byte string</returns>
    private static string FormatBytes(long bytes)
    {
        if (bytes == 0) return "0 B";
        
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int suffixIndex = 0;
        double size = bytes;
        
        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }
        
        return $"{size:F1} {suffixes[suffixIndex]}";
    }

    /// <summary>
    /// Format a TimeSpan as a human-readable string.
    /// </summary>
    /// <param name="timeSpan">TimeSpan to format</param>
    /// <returns>Formatted time string (HH:mm:ss)</returns>
    private static string FormatTimeSpan(TimeSpan timeSpan)
    {
        return $"{(int)timeSpan.TotalHours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
    }

    /// <summary>
    /// Start processing with enhanced progress tracking.
    /// </summary>
    /// <param name="operation">Operation description</param>
    /// <param name="totalBytes">Total bytes to process (0 if unknown)</param>
    public void StartProcessingWithProgress(string operation, long totalBytes = 0)
    {
        IsProcessing = true;
        OperationStartTime = DateTime.Now;
        CurrentOperation = operation;
        TotalBytes = totalBytes;
        BytesProcessed = 0;
        ProgressPercentage = 0;
        ProcessingSpeed = "";
        EstimatedTimeRemaining = "";
        MemoryUsage = GetCurrentMemoryUsage();
        
        UpdateProcessingMessage(operation);
        
        // Notify computed properties
        OnPropertyChanged(nameof(ElapsedTime));
    }

    /// <summary>
    /// Update progress information.
    /// </summary>
    /// <param name="bytesProcessed">Bytes processed so far</param>
    /// <param name="currentFile">Current file being processed (optional)</param>
    public void UpdateProgress(long bytesProcessed, string? currentFile = null)
    {
        BytesProcessed = bytesProcessed;
        
        if (!string.IsNullOrEmpty(currentFile))
        {
            CurrentFile = currentFile;
        }
        
        // Calculate progress percentage
        if (TotalBytes > 0)
        {
            ProgressPercentage = Math.Min(100.0, (double)bytesProcessed / TotalBytes * 100);
        }
        
        // Calculate processing speed
        var elapsed = DateTime.Now - OperationStartTime;
        if (elapsed.TotalSeconds > 0)
        {
            var speedBytesPerSecond = bytesProcessed / elapsed.TotalSeconds;
            ProcessingSpeed = FormatBytes((long)speedBytesPerSecond) + "/s";
            
            // Estimate time remaining
            if (TotalBytes > 0 && speedBytesPerSecond > 0)
            {
                var remainingBytes = TotalBytes - bytesProcessed;
                var remainingSeconds = remainingBytes / speedBytesPerSecond;
                EstimatedTimeRemaining = FormatEstimatedTime(remainingSeconds / 60.0);
            }
        }
        
        // Update memory usage
        MemoryUsage = GetCurrentMemoryUsage();
        
        // Create detailed progress message
        UpdateDetailedProgressMessage();
        
        // Notify computed properties
        OnPropertyChanged(nameof(FormattedBytesProcessed));
        OnPropertyChanged(nameof(FormattedTotalBytes));
        OnPropertyChanged(nameof(ProgressDetails));
        OnPropertyChanged(nameof(FormattedProgressPercentage));
        OnPropertyChanged(nameof(ElapsedTime));
    }

    /// <summary>
    /// Stop processing and reset progress information.
    /// </summary>
    public void StopProcessingWithProgress()
    {
        IsProcessing = false;
        ProgressPercentage = 100;
        ProcessingSpeed = "";
        EstimatedTimeRemaining = "";
        CurrentOperation = "";
        CurrentFile = "";
        DetailedProgressMessage = "";
        
        UpdateProcessingMessage("Operation completed");
        
        // Notify computed properties
        OnPropertyChanged(nameof(ElapsedTime));
    }

    /// <summary>
    /// Get current memory usage as a formatted string.
    /// </summary>
    /// <returns>Formatted memory usage string</returns>
    private static string GetCurrentMemoryUsage()
    {
        try
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();
            var memoryBytes = process.WorkingSet64;
            return FormatBytes(memoryBytes);
        }
        catch
        {
            return "Unknown";
        }
    }

    /// <summary>
    /// Update the detailed progress message.
    /// </summary>
    private void UpdateDetailedProgressMessage()
    {
        var parts = new List<string>();
        
        if (!string.IsNullOrEmpty(CurrentOperation))
            parts.Add(CurrentOperation);
            
        if (TotalBytes > 0)
            parts.Add($"{FormattedProgressPercentage} ({ProgressDetails})");
            
        if (!string.IsNullOrEmpty(ProcessingSpeed))
            parts.Add($"Speed: {ProcessingSpeed}");
            
        if (!string.IsNullOrEmpty(EstimatedTimeRemaining) && EstimatedTimeRemaining != "0.0 min")
            parts.Add($"ETA: {EstimatedTimeRemaining}");
            
        if (!string.IsNullOrEmpty(ElapsedTime) && ElapsedTime != "00:00:00")
            parts.Add($"Elapsed: {ElapsedTime}");
            
        if (!string.IsNullOrEmpty(MemoryUsage))
            parts.Add($"Memory: {MemoryUsage}");
        
        DetailedProgressMessage = string.Join(" • ", parts);
    }

    /// <summary>
    /// Reset the state to initial values.
    /// </summary>
    public void Reset()
    {
        MkvPath = null;
        Tracks.Clear();
        SelectedTrack = null;
        HasProbedFile = false;
        ShowNoTracksError = false;
        IsBusy = false;
        IsProcessing = false;
        ProcessingMessage = "";
        
        // Reset enhanced progress information
        ProgressPercentage = 0;
        CurrentOperation = "";
        CurrentFile = "";
        ProcessingSpeed = "";
        EstimatedTimeRemaining = "";
        MemoryUsage = "";
        BytesProcessed = 0;
        TotalBytes = 0;
        DetailedProgressMessage = "";
        
        // Reset network detection
        IsNetworkFile = false;
        NetworkWarningMessage = "";
        EstimatedProcessingTimeMinutes = 0;
        FormattedFileSize = "";
        NetworkDriveInfo = "";
        
        ClearLog();
    }

}
