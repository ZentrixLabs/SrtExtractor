using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using SrtExtractor.Services.Interfaces;

namespace SrtExtractor.Services.Implementations;

/// <summary>
/// Centralized logging service that writes to separate log files per batch session.
/// </summary>
public class LoggingService : ILoggingService
{
    private readonly string _logDirectory;
    private string _logFilePath;
    private readonly object _lock = new();
    private bool _inBatchSession = false;

    public LoggingService()
    {
        _logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), 
                                   "ZentrixLabs", "SrtExtractor", "Logs");
        
        // Start with a default log file for non-batch operations
        _logFilePath = Path.Combine(_logDirectory, $"srt_general_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
        
        // Ensure log directory exists
        Directory.CreateDirectory(_logDirectory);
        
        // Clean up old logs on startup (older than 24 hours)
        CleanupOldLogs();
    }

    public void StartBatchSession()
    {
        lock (_lock)
        {
            // Create a new log file for this batch with timestamp
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _logFilePath = Path.Combine(_logDirectory, $"srt_batch_{timestamp}.txt");
            _inBatchSession = true;
            
            LogInfo($"=== Batch Session Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            LogInfo($"Log file: {Path.GetFileName(_logFilePath)}");
        }
    }

    public void EndBatchSession()
    {
        lock (_lock)
        {
            if (_inBatchSession)
            {
                LogInfo($"=== Batch Session Ended at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                _inBatchSession = false;
                
                // Clean up old logs after batch completes
                CleanupOldLogs();
            }
        }
    }

    public void LogInfo(string message, [CallerMemberName] string memberName = "")
    {
        Log(LogLevel.Info, message, memberName);
    }

    public void LogWarning(string message, [CallerMemberName] string memberName = "")
    {
        Log(LogLevel.Warning, message, memberName);
    }

    public void LogError(string message, Exception? exception = null, [CallerMemberName] string memberName = "")
    {
        var fullMessage = exception != null ? $"{message} - {exception}" : message;
        Log(LogLevel.Error, fullMessage, memberName);
    }

    public void LogToolDetection(string toolName, Models.ToolStatus status)
    {
        var message = status.IsInstalled 
            ? $"Tool '{toolName}' detected at {status.Path} (v{status.Version})"
            : $"Tool '{toolName}' not found: {status.ErrorMessage}";
        
        if (status.IsInstalled)
            LogInfo(message);
        else
            LogWarning(message);
    }

    public void LogInstallation(string toolName, bool success, string? error = null)
    {
        var message = success 
            ? $"Successfully installed {toolName}"
            : $"Failed to install {toolName}: {error}";
        
        if (success)
            LogInfo(message);
        else
            LogError(message);
    }

    public void LogExtraction(string operation, bool success, string? error = null)
    {
        var message = success 
            ? $"Successfully completed {operation}"
            : $"Failed {operation}: {error}";
        
        if (success)
            LogInfo(message);
        else
            LogError(message);
    }

    private void Log(LogLevel level, string message, string memberName)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logEntry = $"[{timestamp}] [{level}] [{memberName}] {message}";
        
        lock (_lock)
        {
            try
            {
                File.AppendAllText(_logFilePath, logEntry + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
                // If file logging fails, we can't do much about it
                // In a production app, you might want to fall back to event log
            }
        }
    }

    private void CleanupOldLogs()
    {
        try
        {
            // Keep logs for the last 24 hours only
            var cutoffDate = DateTime.Now.AddHours(-24);
            
            if (!Directory.Exists(_logDirectory))
                return;
                
            var logFiles = Directory.GetFiles(_logDirectory, "srt_*.txt");
            var deletedCount = 0;
            var totalSize = 0L;
            
            foreach (var logFile in logFiles)
            {
                try
                {
                    var fileInfo = new FileInfo(logFile);
                    
                    // Don't delete the current log file
                    if (logFile.Equals(_logFilePath, StringComparison.OrdinalIgnoreCase))
                        continue;
                    
                    // Delete files older than 24 hours
                    if (fileInfo.LastWriteTime < cutoffDate)
                    {
                        totalSize += fileInfo.Length;
                        File.Delete(logFile);
                        deletedCount++;
                    }
                }
                catch
                {
                    // Ignore individual file deletion errors
                }
            }
            
            if (deletedCount > 0)
            {
                var sizeMB = totalSize / (1024.0 * 1024.0);
                LogInfo($"Cleaned up {deletedCount} old log files (older than 24 hours) - freed {sizeMB:F2} MB");
            }
        }
        catch
        {
            // If cleanup fails, don't crash the application
        }
    }

    private enum LogLevel
    {
        Info,
        Warning,
        Error
    }
}
