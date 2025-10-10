using System;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SrtExtractor.Services.Interfaces;

namespace SrtExtractor.ViewModels;

/// <summary>
/// ViewModel for the Welcome/Onboarding window.
/// Handles page navigation and user preferences for first-run experience.
/// </summary>
public partial class WelcomeViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly ILoggingService _loggingService;
    
    [ObservableProperty]
    private int _currentPage = 1;
    
    [ObservableProperty]
    private bool _dontShowAgain = false;
    
    public event Action? CloseRequested;
    
    public WelcomeViewModel(ISettingsService settingsService, ILoggingService loggingService)
    {
        _settingsService = settingsService;
        _loggingService = loggingService;
        
        UpdatePageVisibility();
    }
    
    // Page Visibility Properties
    public Visibility Page1Visibility => CurrentPage == 1 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility Page2Visibility => CurrentPage == 2 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility Page3Visibility => CurrentPage == 3 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility Page4Visibility => CurrentPage == 4 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility Page5Visibility => CurrentPage == 5 ? Visibility.Visible : Visibility.Collapsed;
    
    // Button Visibility
    public bool ShowBackButton => CurrentPage > 1;
    public bool ShowNextButton => CurrentPage < 5;
    public bool ShowFinishButton => CurrentPage == 5;
    
    // Page Indicator Active States (for XAML to use with styles)
    public bool IsPage1Active => CurrentPage == 1;
    public bool IsPage2Active => CurrentPage == 2;
    public bool IsPage3Active => CurrentPage == 3;
    public bool IsPage4Active => CurrentPage == 4;
    public bool IsPage5Active => CurrentPage == 5;
    
    [RelayCommand]
    private void Back()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            UpdatePageVisibility();
            _loggingService.LogInfo($"Welcome: Navigated to page {CurrentPage}");
        }
    }
    
    [RelayCommand]
    private void Next()
    {
        if (CurrentPage < 5)
        {
            CurrentPage++;
            UpdatePageVisibility();
            _loggingService.LogInfo($"Welcome: Navigated to page {CurrentPage}");
        }
    }
    
    [RelayCommand]
    private async Task Finish()
    {
        _loggingService.LogInfo($"Welcome: User completed onboarding (Don't show again: {DontShowAgain})");
        
        // Save preference
        if (DontShowAgain)
        {
            try
            {
                var settings = await _settingsService.LoadSettingsAsync();
                var updatedSettings = settings with { ShowWelcomeScreen = false };
                await _settingsService.SaveSettingsAsync(updatedSettings);
                _loggingService.LogInfo("Welcome: User opted out of future welcome screens");
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Welcome: Failed to save settings", ex);
            }
        }
        
        // Close window
        CloseRequested?.Invoke();
    }
    
    partial void OnCurrentPageChanged(int value)
    {
        UpdatePageVisibility();
    }
    
    private void UpdatePageVisibility()
    {
        OnPropertyChanged(nameof(Page1Visibility));
        OnPropertyChanged(nameof(Page2Visibility));
        OnPropertyChanged(nameof(Page3Visibility));
        OnPropertyChanged(nameof(Page4Visibility));
        OnPropertyChanged(nameof(Page5Visibility));
        OnPropertyChanged(nameof(ShowBackButton));
        OnPropertyChanged(nameof(ShowNextButton));
        OnPropertyChanged(nameof(ShowFinishButton));
        OnPropertyChanged(nameof(IsPage1Active));
        OnPropertyChanged(nameof(IsPage2Active));
        OnPropertyChanged(nameof(IsPage3Active));
        OnPropertyChanged(nameof(IsPage4Active));
        OnPropertyChanged(nameof(IsPage5Active));
    }
}

