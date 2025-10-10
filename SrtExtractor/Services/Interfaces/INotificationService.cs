using System;
using System.Threading.Tasks;

namespace SrtExtractor.Services.Interfaces;

/// <summary>
/// Service for displaying toast notifications to users.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Shows an informational toast notification.
    /// </summary>
    /// <param name="message">The message to display</param>
    /// <param name="title">Optional title for the notification</param>
    /// <param name="durationMs">Duration in milliseconds (default: 4000)</param>
    void ShowInfo(string message, string? title = null, int durationMs = 4000);

    /// <summary>
    /// Shows a success toast notification.
    /// </summary>
    /// <param name="message">The message to display</param>
    /// <param name="title">Optional title for the notification</param>
    /// <param name="durationMs">Duration in milliseconds (default: 4000)</param>
    void ShowSuccess(string message, string? title = null, int durationMs = 4000);

    /// <summary>
    /// Shows a warning toast notification.
    /// </summary>
    /// <param name="message">The message to display</param>
    /// <param name="title">Optional title for the notification</param>
    /// <param name="durationMs">Duration in milliseconds (default: 5000)</param>
    void ShowWarning(string message, string? title = null, int durationMs = 5000);

    /// <summary>
    /// Shows an error toast notification.
    /// </summary>
    /// <param name="message">The message to display</param>
    /// <param name="title">Optional title for the notification</param>
    /// <param name="durationMs">Duration in milliseconds (default: 6000)</param>
    void ShowError(string message, string? title = null, int durationMs = 6000);

    /// <summary>
    /// Shows a confirmation toast notification with Yes/No action buttons.
    /// </summary>
    /// <param name="message">The message to display</param>
    /// <param name="title">Optional title for the notification</param>
    /// <param name="onConfirm">Action to execute when user clicks Yes</param>
    /// <param name="onCancel">Optional action to execute when user clicks No</param>
    void ShowConfirmation(string message, string? title, Action onConfirm, Action? onCancel = null);

    /// <summary>
    /// Shows a confirmation toast notification with Yes/No action buttons (async version).
    /// </summary>
    /// <param name="message">The message to display</param>
    /// <param name="title">Optional title for the notification</param>
    /// <returns>Task that completes with true if confirmed, false if canceled</returns>
    Task<bool> ShowConfirmationAsync(string message, string? title = null);
}
