using System.IO;
using SrtExtractor.Services.Interfaces;

namespace SrtExtractor.Services.Implementations;

/// <summary>
/// Service for performing file operations asynchronously.
/// </summary>
public class AsyncFileService : IAsyncFileService
{
    private readonly ILoggingService _loggingService;

    public AsyncFileService(ILoggingService loggingService)
    {
        _loggingService = loggingService;
    }

    public async Task<bool> FileExistsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            return await Task.Run(() => File.Exists(filePath), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _loggingService.LogWarning($"File existence check cancelled for: {filePath}");
            throw;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Error checking file existence for: {filePath}", ex);
            return false;
        }
    }

    public async Task<bool> DirectoryExistsAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(directoryPath))
                return false;

            return await Task.Run(() => Directory.Exists(directoryPath), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _loggingService.LogWarning($"Directory existence check cancelled for: {directoryPath}");
            throw;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Error checking directory existence for: {directoryPath}", ex);
            return false;
        }
    }

    public async Task CreateDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default)
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

    public async Task<string> ReadAllTextAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

            return await File.ReadAllTextAsync(filePath, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _loggingService.LogWarning($"File read cancelled for: {filePath}");
            throw;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Error reading file: {filePath}", ex);
            throw;
        }
    }

    public async Task WriteAllTextAsync(string filePath, string content, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

            await File.WriteAllTextAsync(filePath, content, cancellationToken);
            _loggingService.LogInfo($"Wrote file: {filePath}");
        }
        catch (OperationCanceledException)
        {
            _loggingService.LogWarning($"File write cancelled for: {filePath}");
            throw;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Error writing file: {filePath}", ex);
            throw;
        }
    }

    public async Task DeleteFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath))
                return;

            await Task.Run(() =>
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _loggingService.LogInfo($"Deleted file: {filePath}");
                }
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _loggingService.LogWarning($"File deletion cancelled for: {filePath}");
            throw;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Error deleting file: {filePath}", ex);
            throw;
        }
    }

    public async Task<long?> GetFileSizeAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath))
                return null;

            return await Task.Run(() =>
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    return fileInfo.Exists ? fileInfo.Length : (long?)null;
                }
                catch
                {
                    return (long?)null;
                }
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _loggingService.LogWarning($"File size check cancelled for: {filePath}");
            throw;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Error getting file size for: {filePath}", ex);
            return null;
        }
    }
}
