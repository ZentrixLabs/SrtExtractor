using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using SrtExtractor.Services.Interfaces;
using SrtExtractor.State;

namespace SrtExtractor.Coordinators;

/// <summary>
/// Coordinates file-related operations including file picking, 
/// recent files management, and network detection.
/// </summary>
public class FileCoordinator
{
    private readonly ILoggingService _loggingService;
    private readonly INotificationService _notificationService;
    private readonly INetworkDetectionService _networkDetectionService;
    private readonly IRecentFilesService _recentFilesService;
    private readonly ExtractionState _state;

    public FileCoordinator(
        ILoggingService loggingService,
        INotificationService notificationService,
        INetworkDetectionService networkDetectionService,
        IRecentFilesService recentFilesService,
        ExtractionState state)
    {
        _loggingService = loggingService;
        _notificationService = notificationService;
        _networkDetectionService = networkDetectionService;
        _recentFilesService = recentFilesService;
        _state = state;
    }

    /// <summary>
    /// Opens a file picker dialog to select an MKV/MP4 file.
    /// </summary>
    public Task PickMkvAsync()
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
                _state.MkvPath = openFileDialog.FileName;
                _state.ClearFileState();
                
                // Update network detection
                UpdateNetworkDetection(_state.MkvPath);
                
                _loggingService.LogInfo($"Selected MKV file: {_state.MkvPath}");
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to pick MKV file", ex);
            _notificationService.ShowError($"Failed to select MKV file:\n{ex.Message}", "File Selection Error");
        }
        
        return Task.CompletedTask;
    }

        /// <summary>
        /// Opens a folder picker dialog and returns the selected path.
        /// </summary>
        /// <returns>Selected folder path or null if cancelled</returns>
        public Task<string?> PickFolderAsync()
        {
            try
            {
                var dialog = new VistaFolderBrowserDialog
                {
                    Description = "Select a folder to scan for MKV/MP4 files",
                    UseDescriptionForTitle = true,
                    ShowNewFolderButton = false
                };

                var result = dialog.ShowDialog(Application.Current.MainWindow);
                if (result == true && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
                {
                    _loggingService.LogInfo($"Selected folder: {dialog.SelectedPath}");
                    return Task.FromResult<string?>(dialog.SelectedPath);
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Failed to pick folder", ex);
                _notificationService.ShowError($"Failed to select folder:\n{ex.Message}", "Folder Selection Error");
            }

            return Task.FromResult<string?>(null);
        }

    /// <summary>
    /// Opens a recent file by setting it as the current MKV path.
    /// </summary>
    public void OpenRecentFile(string? filePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            _loggingService.LogInfo($"User selected recent file: {filePath}");
            _state.MkvPath = filePath;
            _state.ClearFileState();
            
            // Update network detection
            UpdateNetworkDetection(filePath);
            
            _state.AddLogMessage($"Opened recent file: {Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Error opening recent file: {filePath}", ex);
            _notificationService.ShowError($"Failed to open recent file:\n{ex.Message}", "Error");
        }
    }

    /// <summary>
    /// Loads the list of recent files from the service.
    /// </summary>
    public async Task LoadRecentFilesAsync()
    {
        try
        {
            var recentFiles = await _recentFilesService.GetRecentFilesAsync().ConfigureAwait(false);
            
            // Update UI on UI thread (non-blocking)
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _state.RecentFiles.Clear();
                foreach (var file in recentFiles)
                {
                    _state.RecentFiles.Add(file);
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
    /// Updates network detection for the given file path.
    /// </summary>
    public void UpdateNetworkDetection(string filePath)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath))
            {
                _state.UpdateNetworkDetection(false, 0, "", "");
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

            _state.UpdateNetworkDetection(isNetwork, estimatedMinutes, formattedSize, networkDriveInfo);
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to update network detection", ex);
        }
    }
}

