using System.Runtime.CompilerServices;

namespace SrtExtractor.Services.Interfaces;

/// <summary>
/// Centralized logging service for the application.
/// All output should go through this service instead of Console.WriteLine or Debug.WriteLine.
/// </summary>
public interface ILoggingService
{
    /// <summary>
    /// Log an informational message.
    /// </summary>
    /// <param name="message">The message to log</param>
    /// <param name="memberName">The calling member name (automatically populated)</param>
    void LogInfo(string message, [CallerMemberName] string memberName = "");

    /// <summary>
    /// Log a warning message.
    /// </summary>
    /// <param name="message">The warning message to log</param>
    /// <param name="memberName">The calling member name (automatically populated)</param>
    void LogWarning(string message, [CallerMemberName] string memberName = "");

    /// <summary>
    /// Log an error message with optional exception details.
    /// </summary>
    /// <param name="message">The error message to log</param>
    /// <param name="exception">Optional exception details</param>
    /// <param name="memberName">The calling member name (automatically populated)</param>
    void LogError(string message, Exception? exception = null, [CallerMemberName] string memberName = "");

    /// <summary>
    /// Log tool detection events.
    /// </summary>
    /// <param name="toolName">Name of the tool being detected</param>
    /// <param name="status">The detection status</param>
    void LogToolDetection(string toolName, Models.ToolStatus status);

    /// <summary>
    /// Log tool installation events.
    /// </summary>
    /// <param name="toolName">Name of the tool being installed</param>
    /// <param name="success">Whether installation was successful</param>
    /// <param name="error">Error message if installation failed</param>
    void LogInstallation(string toolName, bool success, string? error = null);

    /// <summary>
    /// Log subtitle extraction events.
    /// </summary>
    /// <param name="operation">The extraction operation being performed</param>
    /// <param name="success">Whether the operation was successful</param>
    /// <param name="error">Error message if operation failed</param>
    void LogExtraction(string operation, bool success, string? error = null);

    /// <summary>
    /// Start a new batch logging session with a separate log file.
    /// </summary>
    void StartBatchSession();

    /// <summary>
    /// End the current batch logging session.
    /// </summary>
    void EndBatchSession();
}
