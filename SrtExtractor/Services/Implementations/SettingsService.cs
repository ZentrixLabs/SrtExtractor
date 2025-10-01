using System.IO;
using System.Text.Json;
using SrtExtractor.Models;
using SrtExtractor.Services.Interfaces;

namespace SrtExtractor.Services.Implementations;

/// <summary>
/// Service for managing application settings and configuration.
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly ILoggingService _loggingService;
    private readonly string _settingsPath;
    private readonly JsonSerializerOptions _jsonOptions;

    public SettingsService(ILoggingService loggingService)
    {
        _loggingService = loggingService;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        
        // Initialize settings path
        var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                                     "SrtExtractor");
        Directory.CreateDirectory(appDataPath);
        _settingsPath = Path.Combine(appDataPath, "settings.json");
    }

    public async Task<AppSettings> LoadSettingsAsync()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                _loggingService.LogInfo("Settings file not found, using defaults");
                return AppSettings.Default;
            }

            var json = await File.ReadAllTextAsync(_settingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);
            
            if (settings == null)
            {
                _loggingService.LogWarning("Failed to deserialize settings, using defaults");
                return AppSettings.Default;
            }

            _loggingService.LogInfo("Settings loaded successfully");
            return settings;
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to load settings", ex);
            return AppSettings.Default;
        }
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, _jsonOptions);
            await File.WriteAllTextAsync(_settingsPath, json);
            _loggingService.LogInfo("Settings saved successfully");
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to save settings", ex);
            throw;
        }
    }

    public Task<string> GetAppDataPathAsync()
    {
        var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                                     "SrtExtractor");
        Directory.CreateDirectory(appDataPath);
        return Task.FromResult(appDataPath);
    }
}
