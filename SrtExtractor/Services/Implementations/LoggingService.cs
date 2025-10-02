using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using SrtExtractor.Services.Interfaces;

namespace SrtExtractor.Services.Implementations;

/// <summary>
/// Centralized logging service that writes to both UI and rolling file logs.
/// </summary>
public class LoggingService : ILoggingService
{
    private readonly string _logDirectory;
    private readonly string _logFilePath;
    private readonly object _lock = new();

    public LoggingService()
    {
        _logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), 
                                   "ZentrixLabs", "SrtExtractor", "Logs");
        _logFilePath = Path.Combine(_logDirectory, $"srt_{DateTime.Now:yyyyMMdd}.txt");
        
        // Ensure log directory exists
        Directory.CreateDirectory(_logDirectory);
        
        // Clean up old logs on startup
        CleanupOldLogs();
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
            // Keep logs for the last 7 days
            var cutoffDate = DateTime.Now.AddDays(-7);
            
            if (!Directory.Exists(_logDirectory))
                return;
                
            var logFiles = Directory.GetFiles(_logDirectory, "srt_*.txt");
            var deletedCount = 0;
            
            foreach (var logFile in logFiles)
            {
                try
                {
                    var fileInfo = new FileInfo(logFile);
                    if (fileInfo.CreationTime < cutoffDate)
                    {
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
                LogInfo($"Cleaned up {deletedCount} old log files (older than 7 days)");
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
