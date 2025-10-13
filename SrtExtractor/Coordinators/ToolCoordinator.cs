using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using SrtExtractor.Models;
using SrtExtractor.Services.Interfaces;
using SrtExtractor.State;

namespace SrtExtractor.Coordinators;

/// <summary>
/// Coordinates tool detection, installation, and path management operations.
/// </summary>
public class ToolCoordinator
{
    private readonly ILoggingService _loggingService;
    private readonly INotificationService _notificationService;
    private readonly IToolDetectionService _toolDetectionService;
    private readonly ISettingsService _settingsService;
    private readonly ExtractionState _state;

    public ToolCoordinator(
        ILoggingService loggingService,
        INotificationService notificationService,
        IToolDetectionService toolDetectionService,
        ISettingsService settingsService,
        ExtractionState state)
    {
        _loggingService = loggingService;
        _notificationService = notificationService;
        _toolDetectionService = toolDetectionService;
        _settingsService = settingsService;
        _state = state;
    }

    /// <summary>
    /// Detect all required tools and update their status in the state.
    /// </summary>
    public async Task DetectToolsAsync()
    {
        _state.AddLogMessage("Detecting external tools...");

        // Load current settings
        var settings = await _settingsService.LoadSettingsAsync();
        var settingsUpdated = false;

        // Detect MKVToolNix
        var mkvStatus = await _toolDetectionService.CheckMkvToolNixAsync();
        _state.UpdateToolStatus("MKVToolNix", mkvStatus);
        
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

        // Detect FFmpeg
        var ffmpegStatus = await _toolDetectionService.CheckFfmpegAsync();
        _state.UpdateToolStatus("FFmpeg", ffmpegStatus);

        // Save updated settings if any tool paths were detected
        if (settingsUpdated)
        {
            await _settingsService.SaveSettingsAsync(settings);
            _loggingService.LogInfo("Updated settings with detected tool paths");
        }

        if (_state.AreToolsAvailable)
        {
            _state.AddLogMessage("All required tools are available");
        }
        else
        {
            _state.AddLogMessage("Some required tools are missing. Please configure them in Settings.");
            
            // Show a message directing users to settings (fire and forget)
            _ = Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                var result = await _notificationService.ShowConfirmationAsync(
                    "Some required tools are missing and need to be configured.\n\nWould you like to open Settings to configure the tools now?",
                    "Tools Missing");
                
                if (result)
                {
                    // Trigger the event to open settings immediately
                    _state.TriggerOpenSettings();
                }
            });
        }
    }

    /// <summary>
    /// Re-detect all tools.
    /// </summary>
    public async Task ReDetectToolsAsync()
    {
        try
        {
            _state.IsBusy = true;
            _state.AddLogMessage("Re-detecting tools...");
            
            await DetectToolsAsync();
            
            _state.AddLogMessage("Tool detection completed!");
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to re-detect tools", ex);
            _state.AddLogMessage($"Error re-detecting tools: {ex.Message}");
        }
        finally
        {
            _state.IsBusy = false;
        }
    }

    /// <summary>
    /// Update the path for a specific tool and re-detect.
    /// </summary>
    public async Task UpdateToolPathAsync(string toolName, string toolPath)
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

            await _settingsService.SaveSettingsAsync(settings);
            _state.AddLogMessage($"Updated {toolName} path: {toolPath}");
            
            // Re-detect tools
            await DetectToolsAsync();
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Failed to update {toolName} path", ex);
            _state.AddLogMessage($"Error updating {toolName} path: {ex.Message}");
        }
    }

    /// <summary>
    /// Show information about bundled tools (installation not needed).
    /// </summary>
    public Task InstallMkvToolNixAsync()
    {
        // All tools are bundled - no installation needed
        _loggingService.LogInfo("MKVToolNix is bundled with the application - no installation needed");
        _notificationService.ShowInfo("All required tools are bundled with SrtExtractor.\n\nNo installation needed!", "Tools Already Available");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Browse for MKVToolNix executable and update settings.
    /// </summary>
    public void BrowseMkvToolNix()
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
}

