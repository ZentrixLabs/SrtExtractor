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

    private enum LogLevel
    {
        Info,
        Warning,
        Error
    }
}
