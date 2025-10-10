using System.Collections.Concurrent;
using System.IO;
using SrtExtractor.Services.Interfaces;

namespace SrtExtractor.Services.Implementations;

/// <summary>
/// Service for caching file information to avoid repeated file system calls.
/// </summary>
public class FileCacheService : IFileCacheService
{
    private readonly ConcurrentDictionary<string, CachedFileInfo> _cache = new();
    private readonly ILoggingService _loggingService;
    private int _cacheHits = 0;
    private int _cacheMisses = 0;

    public FileCacheService(ILoggingService loggingService)
    {
        _loggingService = loggingService;
    }

    public async Task<bool> FileExistsAsync(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return false;

        var cached = _cache.GetOrAdd(filePath, key =>
        {
            _cacheMisses++;
            return new CachedFileInfo(key);
        });

        if (cached.IsValid)
        {
            _cacheHits++;
            return cached.Exists;
        }

        // Cache expired or invalid, refresh
        _cacheMisses++;
        await RefreshFileInfo(cached);
        return cached.Exists;
    }

    public async Task<FileInfo?> GetFileInfoAsync(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return null;

        var cached = _cache.GetOrAdd(filePath, key =>
        {
            _cacheMisses++;
            return new CachedFileInfo(key);
        });

        if (cached.IsValid && cached.Exists)
        {
            _cacheHits++;
            return cached.FileInfo;
        }

        // Cache expired or invalid, refresh
        _cacheMisses++;
        await RefreshFileInfo(cached);
        return cached.Exists ? cached.FileInfo : null;
    }

    public async Task<long> GetFileSizeAsync(string filePath)
    {
        var fileInfo = await GetFileInfoAsync(filePath);
        return fileInfo?.Length ?? 0;
    }

    public void InvalidateFile(string filePath)
    {
        if (_cache.TryRemove(filePath, out var cached))
        {
            _loggingService.LogInfo($"Invalidated cache for file: {filePath}");
        }
    }

    public void ClearCache()
    {
        var count = _cache.Count;
        _cache.Clear();
        _loggingService.LogInfo($"Cleared file cache ({count} entries)");
    }

    public (int Hits, int Misses) GetCacheStats()
    {
        return (_cacheHits, _cacheMisses);
    }

    private async Task RefreshFileInfo(CachedFileInfo cached)
    {
        try
        {
            var fileInfo = new FileInfo(cached.FilePath);
            
            // Use async file existence check for better performance
            await Task.Run(() =>
            {
                cached.Exists = fileInfo.Exists;
                cached.FileInfo = cached.Exists ? fileInfo : null;
                cached.LastChecked = DateTime.UtcNow;
                cached.IsValid = true;
            });
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Failed to refresh file info for {cached.FilePath}", ex);
            cached.Exists = false;
            cached.FileInfo = null;
            cached.LastChecked = DateTime.UtcNow;
            cached.IsValid = false;
        }
    }

    private class CachedFileInfo
    {
        public string FilePath { get; }
        public bool Exists { get; set; }
        public FileInfo? FileInfo { get; set; }
        public DateTime LastChecked { get; set; }
        public bool IsValid { get; set; }

        // Cache validity period - 30 seconds for file operations
        private static readonly TimeSpan CacheValidity = TimeSpan.FromSeconds(30);

        public CachedFileInfo(string filePath)
        {
            FilePath = filePath;
            LastChecked = DateTime.MinValue;
            IsValid = false;
        }

        public bool IsCacheExpired => DateTime.UtcNow - LastChecked > CacheValidity;
    }
}
