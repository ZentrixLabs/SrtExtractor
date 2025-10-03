using System.IO;
using SrtExtractor.Services.Interfaces;

namespace SrtExtractor.Services.Implementations;

/// <summary>
/// Service for detecting file locks and checking file accessibility.
/// </summary>
public class FileLockDetectionService : IFileLockDetectionService
{
    private readonly ILoggingService _loggingService;

    public FileLockDetectionService(ILoggingService loggingService)
    {
        _loggingService = loggingService;
    }

    public async Task<bool> IsFileLockedAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            // Use Task.Run to avoid blocking the calling thread
            return await Task.Run(() =>
            {
                try
                {
                    // Try to open the file for exclusive access
                    using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                    return false; // File is not locked
                }
                catch (UnauthorizedAccessException)
                {
                    return true; // File is locked or access denied
                }
                catch (FileNotFoundException)
                {
                    return false; // File doesn't exist, so it's not locked
                }
                catch (IOException)
                {
                    return true; // File is locked or in use
                }
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _loggingService.LogWarning($"File lock check cancelled for: {filePath}");
            throw;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Error checking file lock for: {filePath}", ex);
            return true; // Assume locked on error to be safe
        }
    }

    public async Task<bool> IsFileAccessibleAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            return await Task.Run(() =>
            {
                try
                {
                    return File.Exists(filePath) && !IsFileLockedSync(filePath);
                }
                catch
                {
                    return false;
                }
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _loggingService.LogWarning($"File accessibility check cancelled for: {filePath}");
            throw;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Error checking file accessibility for: {filePath}", ex);
            return false;
        }
    }

    public async Task EnsureDirectoryExistsAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(directoryPath))
                return;

            await Task.Run(() =>
            {
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                    _loggingService.LogInfo($"Created directory: {directoryPath}");
                }
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _loggingService.LogWarning($"Directory creation cancelled for: {directoryPath}");
            throw;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Error creating directory: {directoryPath}", ex);
            throw;
        }
    }

    public async Task<FileInfo?> GetFileInfoAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath))
                return null;

            return await Task.Run(() =>
            {
                try
                {
                    return File.Exists(filePath) ? new FileInfo(filePath) : null;
                }
                catch
                {
                    return null;
                }
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _loggingService.LogWarning($"File info retrieval cancelled for: {filePath}");
            throw;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Error getting file info for: {filePath}", ex);
            return null;
        }
    }

    private static bool IsFileLockedSync(string filePath)
    {
        try
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }
}
