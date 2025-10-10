using CommunityToolkit.Mvvm.ComponentModel;

namespace SrtExtractor.ViewModels;

/// <summary>
/// ViewModel for the unified progress indicator component
/// </summary>
public partial class ProgressIndicatorViewModel : ObservableObject
{
    [ObservableProperty]
    private string _stageText = "Ready";

    [ObservableProperty]
    private double _progressPercentage;

    [ObservableProperty]
    private bool _isIndeterminate = false;

    [ObservableProperty]
    private string _progressText = "";

    [ObservableProperty]
    private string _timeRemainingText = "";

    [ObservableProperty]
    private bool _showTimeRemaining = false;

    [ObservableProperty]
    private string _progressTooltip = "";

    /// <summary>
    /// Update progress with stage information
    /// </summary>
    public void UpdateProgress(string stage, double percentage, string message = "", string timeRemaining = "")
    {
        StageText = stage;
        ProgressPercentage = percentage;
        ProgressText = message;
        TimeRemainingText = timeRemaining;
        ShowTimeRemaining = !string.IsNullOrEmpty(timeRemaining);
        IsIndeterminate = false;
        
        ProgressTooltip = $"Stage: {stage}\nProgress: {percentage:F1}%\n{message}";
    }

    /// <summary>
    /// Set indeterminate progress (spinning)
    /// </summary>
    public void SetIndeterminate(string stage, string message = "")
    {
        StageText = stage;
        IsIndeterminate = true;
        ProgressText = message;
        ShowTimeRemaining = false;
        
        ProgressTooltip = $"Stage: {stage}\n{message}";
    }

    /// <summary>
    /// Clear progress indicator
    /// </summary>
    public void Clear()
    {
        StageText = "Ready";
        ProgressPercentage = 0;
        IsIndeterminate = false;
        ProgressText = "";
        TimeRemainingText = "";
        ShowTimeRemaining = false;
        ProgressTooltip = "";
    }
}
