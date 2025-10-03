using System.IO;
using System.Text.Json;
using SrtExtractor.Services.Interfaces;

namespace SrtExtractor.Services.Implementations;

/// <summary>
/// Service for managing recently processed files with persistent storage.
/// </summary>
public class RecentFilesService : IRecentFilesService
{
    private readonly ILoggingService _loggingService;
    private readonly ISettingsService _settingsService;
    private readonly List<string> _recentFiles = new();
    private const int MaxRecentFiles = 10;
    private const string RecentFilesFileName = "recent_files.json";

    public event EventHandler<List<string>>? RecentFilesChanged;

    public RecentFilesService(ILoggingService loggingService, ISettingsService settingsService)
    {
        _loggingService = loggingService;
        _settingsService = settingsService;
    }

    public async Task AddFileAsync(string filePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            // Normalize the path
            var normalizedPath = Path.GetFullPath(filePath);

            // Remove if already exists to avoid duplicates
            _recentFiles.RemoveAll(f => string.Equals(f, normalizedPath, StringComparison.OrdinalIgnoreCase));

            // Add to the beginning of the list
            _recentFiles.Insert(0, normalizedPath);

            // Limit to max recent files
            if (_recentFiles.Count > MaxRecentFiles)
            {
                _recentFiles.RemoveRange(MaxRecentFiles, _recentFiles.Count - MaxRecentFiles);
            }

            // Save to persistent storage
            await SaveRecentFilesAsync();

            // Notify listeners
            RecentFilesChanged?.Invoke(this, new List<string>(_recentFiles));

            _loggingService.LogInfo($"Added file to recent files: {normalizedPath}");
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Failed to add file to recent files: {filePath}", ex);
        }
    }

    public async Task<List<string>> GetRecentFilesAsync()
    {
        try
        {
            // Load from persistent storage if not already loaded
            if (_recentFiles.Count == 0)
            {
                await LoadRecentFilesAsync();
            }

            // Filter out files that no longer exist
            var existingFiles = new List<string>();
            foreach (var filePath in _recentFiles)
            {
                if (File.Exists(filePath))
                {
                    existingFiles.Add(filePath);
                }
            }

            // Update the list if any files were removed
            if (existingFiles.Count != _recentFiles.Count)
            {
                _recentFiles.Clear();
                _recentFiles.AddRange(existingFiles);
                await SaveRecentFilesAsync();
                RecentFilesChanged?.Invoke(this, new List<string>(_recentFiles));
            }

            return new List<string>(_recentFiles);
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to get recent files", ex);
            return new List<string>();
        }
    }

    public async Task ClearRecentFilesAsync()
    {
        try
        {
            _recentFiles.Clear();
            await SaveRecentFilesAsync();
            RecentFilesChanged?.Invoke(this, new List<string>());
            _loggingService.LogInfo("Cleared all recent files");
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to clear recent files", ex);
        }
    }

    public async Task RemoveFileAsync(string filePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            var normalizedPath = Path.GetFullPath(filePath);
            var removed = _recentFiles.RemoveAll(f => string.Equals(f, normalizedPath, StringComparison.OrdinalIgnoreCase)) > 0;

            if (removed)
            {
                await SaveRecentFilesAsync();
                RecentFilesChanged?.Invoke(this, new List<string>(_recentFiles));
                _loggingService.LogInfo($"Removed file from recent files: {normalizedPath}");
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Failed to remove file from recent files: {filePath}", ex);
        }
    }

    public Task<bool> IsRecentFileAsync(string filePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return Task.FromResult(false);

            var normalizedPath = Path.GetFullPath(filePath);
            var result = _recentFiles.Any(f => string.Equals(f, normalizedPath, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Failed to check if file is recent: {filePath}", ex);
            return Task.FromResult(false);
        }
    }

    private async Task LoadRecentFilesAsync()
    {
        try
        {
            var appDataPath = await _settingsService.GetAppDataPathAsync();
            var recentFilesPath = Path.Combine(appDataPath, RecentFilesFileName);

            if (File.Exists(recentFilesPath))
            {
                var json = await File.ReadAllTextAsync(recentFilesPath);
                var loadedFiles = JsonSerializer.Deserialize<List<string>>(json);
                
                if (loadedFiles != null)
                {
                    _recentFiles.Clear();
                    _recentFiles.AddRange(loadedFiles);
                    _loggingService.LogInfo($"Loaded {_recentFiles.Count} recent files");
                }
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to load recent files from storage", ex);
        }
    }

    private async Task SaveRecentFilesAsync()
    {
        try
        {
            var appDataPath = await _settingsService.GetAppDataPathAsync().ConfigureAwait(false);
            Directory.CreateDirectory(appDataPath);
            
            var recentFilesPath = Path.Combine(appDataPath, RecentFilesFileName);
            var json = JsonSerializer.Serialize(_recentFiles, new JsonSerializerOptions { WriteIndented = true });
            
            await File.WriteAllTextAsync(recentFilesPath, json).ConfigureAwait(false);
            _loggingService.LogInfo($"Saved {_recentFiles.Count} recent files to storage");
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to save recent files to storage", ex);
        }
    }
}
