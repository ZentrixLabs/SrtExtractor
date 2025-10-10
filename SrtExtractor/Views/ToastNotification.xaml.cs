using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using SrtExtractor.Services.Implementations;

namespace SrtExtractor.Views;

public partial class ToastNotification : UserControl
{
    private DispatcherTimer? _autoCloseTimer;
    private Action? _onConfirm;
    private Action? _onCancel;

    public event Action<ToastNotification>? Closed;

    public ToastNotification()
    {
        InitializeComponent();
    }

    public void Show(ToastNotificationData data)
    {
        // Set title and message
        TitleText.Text = data.Title;
        MessageText.Text = data.Message;

        // Set icon and colors based on type
        ConfigureToastAppearance(data.Type);

        // Handle confirmation buttons
        if (data.Type == ToastType.Confirmation)
        {
            ActionButtonsPanel.Visibility = Visibility.Visible;
            _onConfirm = data.OnConfirm;
            _onCancel = data.OnCancel;
        }
        else
        {
            ActionButtonsPanel.Visibility = Visibility.Collapsed;
        }

        // Show animation
        var showStoryboard = (Storyboard)Resources["ShowAnimation"];
        showStoryboard.Begin();

        // Auto-close timer (if duration > 0)
        if (data.DurationMs > 0)
        {
            _autoCloseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(data.DurationMs)
            };
            _autoCloseTimer.Tick += (s, e) =>
            {
                _autoCloseTimer.Stop();
                Close();
            };
            _autoCloseTimer.Start();
        }
    }

    private void ConfigureToastAppearance(ToastType type)
    {
        switch (type)
        {
            case ToastType.Info:
                IconText.Text = "ℹ";
                ToastBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 120, 212)); // Blue
                IconText.Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 212));
                break;

            case ToastType.Success:
                IconText.Text = "✓";
                ToastBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(16, 124, 16)); // Green
                IconText.Foreground = new SolidColorBrush(Color.FromRgb(16, 124, 16));
                break;

            case ToastType.Warning:
                IconText.Text = "⚠";
                ToastBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(252, 163, 17)); // Orange
                IconText.Foreground = new SolidColorBrush(Color.FromRgb(252, 163, 17));
                break;

            case ToastType.Error:
                IconText.Text = "✖";
                ToastBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(232, 17, 35)); // Red
                IconText.Foreground = new SolidColorBrush(Color.FromRgb(232, 17, 35));
                break;

            case ToastType.Confirmation:
                IconText.Text = "❓";
                ToastBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(136, 23, 152)); // Purple
                IconText.Foreground = new SolidColorBrush(Color.FromRgb(136, 23, 152));
                break;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _onCancel?.Invoke();
        Close();
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        _onConfirm?.Invoke();
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _onCancel?.Invoke();
        Close();
    }

    private void Close()
    {
        _autoCloseTimer?.Stop();

        var hideStoryboard = (Storyboard)Resources["HideAnimation"];
        hideStoryboard.Completed += (s, e) =>
        {
            Closed?.Invoke(this);
        };
        hideStoryboard.Begin();
    }
}
