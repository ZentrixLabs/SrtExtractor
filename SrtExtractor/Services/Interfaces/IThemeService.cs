namespace SrtExtractor.Services.Interfaces;

/// <summary>
/// Service for managing application theme (Light/Dark mode).
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// Gets the current theme (Light or Dark).
    /// </summary>
    string CurrentTheme { get; }
    
    /// <summary>
    /// Sets the application theme.
    /// </summary>
    /// <param name="theme">Theme name: "Light" or "Dark"</param>
    void SetTheme(string theme);
    
    /// <summary>
    /// Toggles between Light and Dark theme.
    /// </summary>
    void ToggleTheme();
    
    /// <summary>
    /// Gets whether the current theme is Dark mode.
    /// </summary>
    bool IsDarkMode { get; }
}

