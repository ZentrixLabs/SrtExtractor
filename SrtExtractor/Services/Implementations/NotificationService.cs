using System;
using System.Threading.Tasks;
using System.Windows;
using SrtExtractor.Services.Interfaces;

namespace SrtExtractor.Services.Implementations;

/// <summary>
/// Implementation of INotificationService that displays toast notifications via the MainWindow.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly ILoggingService _loggingService;

    public NotificationService(ILoggingService loggingService)
    {
        _loggingService = loggingService;
    }

    /// <summary>
    /// Event that fires when a toast notification should be displayed.
    /// MainWindow subscribes to this event.
    /// </summary>
    public static event Action<ToastNotificationData>? ToastRequested;

    public void ShowInfo(string message, string? title = null, int durationMs = 4000)
    {
        _loggingService.LogInfo($"Toast Info: {title ?? "Info"} - {message}");
        ShowToast(ToastType.Info, message, title, durationMs);
    }

    public void ShowSuccess(string message, string? title = null, int durationMs = 4000)
    {
        _loggingService.LogInfo($"Toast Success: {title ?? "Success"} - {message}");
        ShowToast(ToastType.Success, message, title, durationMs);
    }

    public void ShowWarning(string message, string? title = null, int durationMs = 5000)
    {
        _loggingService.LogWarning($"Toast Warning: {title ?? "Warning"} - {message}");
        ShowToast(ToastType.Warning, message, title, durationMs);
    }

    public void ShowError(string message, string? title = null, int durationMs = 6000)
    {
        _loggingService.LogError($"Toast Error: {title ?? "Error"} - {message}");
        ShowToast(ToastType.Error, message, title, durationMs);
    }

    public void ShowConfirmation(string message, string? title, Action onConfirm, Action? onCancel = null)
    {
        _loggingService.LogInfo($"Toast Confirmation: {title ?? "Confirm"} - {message}");
        ShowToast(ToastType.Confirmation, message, title, 0, onConfirm, onCancel);
    }

    public Task<bool> ShowConfirmationAsync(string message, string? title = null)
    {
        _loggingService.LogInfo($"Toast Confirmation (Async): {title ?? "Confirm"} - {message}");
        
        var tcs = new TaskCompletionSource<bool>();
        
        ShowToast(
            ToastType.Confirmation, 
            message, 
            title, 
            0, 
            () => tcs.TrySetResult(true),
            () => tcs.TrySetResult(false)
        );
        
        return tcs.Task;
    }

    private void ShowToast(ToastType type, string message, string? title, int durationMs, Action? onConfirm = null, Action? onCancel = null)
    {
        var data = new ToastNotificationData
        {
            Type = type,
            Title = title ?? GetDefaultTitle(type),
            Message = message,
            DurationMs = durationMs,
            OnConfirm = onConfirm,
            OnCancel = onCancel
        };

        // Must be invoked on UI thread
        Application.Current?.Dispatcher.Invoke(() =>
        {
            ToastRequested?.Invoke(data);
        });
    }

    private string GetDefaultTitle(ToastType type) => type switch
    {
        ToastType.Info => "Information",
        ToastType.Success => "Success",
        ToastType.Warning => "Warning",
        ToastType.Error => "Error",
        ToastType.Confirmation => "Confirm Action",
        _ => "Notification"
    };
}

/// <summary>
/// Types of toast notifications.
/// </summary>
public enum ToastType
{
    Info,
    Success,
    Warning,
    Error,
    Confirmation
}

/// <summary>
/// Data structure for toast notification information.
/// </summary>
public class ToastNotificationData
{
    public required ToastType Type { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public int DurationMs { get; init; }
    public Action? OnConfirm { get; init; }
    public Action? OnCancel { get; init; }
}
