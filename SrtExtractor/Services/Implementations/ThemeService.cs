using ModernWpf;
using SrtExtractor.Services.Interfaces;

namespace SrtExtractor.Services.Implementations;

/// <summary>
/// Service for managing application theme using ModernWPF.
/// </summary>
public class ThemeService : IThemeService
{
    private readonly ILoggingService _loggingService;
    private ElementTheme _currentTheme;

    public ThemeService(ILoggingService loggingService)
    {
        _loggingService = loggingService;
        _currentTheme = ThemeManager.Current.ActualApplicationTheme == ApplicationTheme.Dark 
            ? ElementTheme.Dark 
            : ElementTheme.Light;
        _loggingService.LogInfo($"ThemeService initialized with theme: {_currentTheme}");
    }

    public string CurrentTheme => _currentTheme.ToString();

    public bool IsDarkMode => _currentTheme == ElementTheme.Dark;

    public void SetTheme(string theme)
    {
        if (Enum.TryParse<ElementTheme>(theme, out var elementTheme))
        {
            _currentTheme = elementTheme;
            ThemeManager.Current.ApplicationTheme = elementTheme == ElementTheme.Light 
                ? ApplicationTheme.Light 
                : ApplicationTheme.Dark;
            
            _loggingService.LogInfo($"Theme changed to: {theme}");
        }
        else
        {
            _loggingService.LogWarning($"Invalid theme name: {theme}. Using Light theme.");
            SetTheme("Light");
        }
    }

    public void ToggleTheme()
    {
        var newTheme = _currentTheme == ElementTheme.Light ? "Dark" : "Light";
        SetTheme(newTheme);
        _loggingService.LogInfo($"Theme toggled to: {newTheme}");
    }
}

