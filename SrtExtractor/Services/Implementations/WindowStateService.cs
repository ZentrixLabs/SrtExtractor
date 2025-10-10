using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using SrtExtractor.Services.Interfaces;

namespace SrtExtractor.Services.Implementations;

/// <summary>
/// Service for managing window state persistence with JSON storage.
/// </summary>
public class WindowStateService : IWindowStateService
{
    private const string WindowStateFileName = "window_state.json";
    
    private readonly ILoggingService _loggingService;
    private readonly ISettingsService _settingsService;

    public WindowStateService(ILoggingService loggingService, ISettingsService settingsService)
    {
        _loggingService = loggingService;
        _settingsService = settingsService;
    }

    public async Task SaveWindowStateAsync(WindowState windowState)
    {
        try
        {
            var appDataPath = await _settingsService.GetAppDataPathAsync().ConfigureAwait(false);
            Directory.CreateDirectory(appDataPath);
            
            var windowStatePath = Path.Combine(appDataPath, WindowStateFileName);
            var json = JsonSerializer.Serialize(windowState, new JsonSerializerOptions { WriteIndented = true });
            
            await File.WriteAllTextAsync(windowStatePath, json).ConfigureAwait(false);
            _loggingService.LogInfo($"Window state saved: {windowState.Width}x{windowState.Height} at ({windowState.Left}, {windowState.Top})");
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to save window state", ex);
        }
    }

    public async Task<WindowState> LoadWindowStateAsync()
    {
        try
        {
            var appDataPath = await _settingsService.GetAppDataPathAsync().ConfigureAwait(false);
            var windowStatePath = Path.Combine(appDataPath, WindowStateFileName);

            if (File.Exists(windowStatePath))
            {
                var json = await File.ReadAllTextAsync(windowStatePath).ConfigureAwait(false);
                var windowState = JsonSerializer.Deserialize<WindowState>(json);
                
                if (windowState != null)
                {
                    // Validate window state to ensure it's reasonable
                    windowState = ValidateWindowState(windowState);
                    _loggingService.LogInfo($"Window state loaded: {windowState.Width}x{windowState.Height} at ({windowState.Left}, {windowState.Top})");
                    return windowState;
                }
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to load window state", ex);
        }

        // Return default window state if loading fails
        _loggingService.LogInfo("Using default window state");
        return new WindowState();
    }

    public async Task ClearWindowStateAsync()
    {
        try
        {
            var appDataPath = await _settingsService.GetAppDataPathAsync().ConfigureAwait(false);
            var windowStatePath = Path.Combine(appDataPath, WindowStateFileName);

            if (File.Exists(windowStatePath))
            {
                File.Delete(windowStatePath);
                _loggingService.LogInfo("Window state cleared");
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to clear window state", ex);
        }
    }

    /// <summary>
    /// Validates and corrects window state to ensure it's within reasonable bounds.
    /// </summary>
    /// <param name="windowState">The window state to validate</param>
    /// <returns>Validated window state</returns>
    private static WindowState ValidateWindowState(WindowState windowState)
    {
        // Ensure minimum window size
        windowState.Width = Math.Max(windowState.Width, 800);
        windowState.Height = Math.Max(windowState.Height, 600);

        // Ensure maximum window size (reasonable limit)
        windowState.Width = Math.Min(windowState.Width, 3000);
        windowState.Height = Math.Min(windowState.Height, 2000);

        // Ensure window position is not too far off-screen
        windowState.Left = Math.Max(windowState.Left, -100);
        windowState.Top = Math.Max(windowState.Top, -100);

        // Ensure selected tab index is valid (0-3)
        windowState.SelectedTabIndex = Math.Max(0, Math.Min(windowState.SelectedTabIndex, 3));

        return windowState;
    }
}
