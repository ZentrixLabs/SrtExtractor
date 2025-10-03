using System.Threading.Tasks;

namespace SrtExtractor.Services.Interfaces;

/// <summary>
/// Service for managing window state persistence.
/// </summary>
public interface IWindowStateService
{
    /// <summary>
    /// Saves the current window state to persistent storage.
    /// </summary>
    /// <param name="windowState">The window state to save.</param>
    Task SaveWindowStateAsync(WindowState windowState);

    /// <summary>
    /// Loads the window state from persistent storage.
    /// </summary>
    /// <returns>The saved window state, or default values if none exists.</returns>
    Task<WindowState> LoadWindowStateAsync();

    /// <summary>
    /// Clears the saved window state.
    /// </summary>
    Task ClearWindowStateAsync();
}

/// <summary>
/// Represents the state of a window that can be persisted.
/// </summary>
public class WindowState
{
    public double Width { get; set; } = 1250;
    public double Height { get; set; } = 900;
    public double Left { get; set; } = 100;
    public double Top { get; set; } = 100;
    public System.Windows.WindowState WindowStateEnum { get; set; } = System.Windows.WindowState.Normal;
    public double QueueColumnWidth { get; set; } = 0;
    public bool IsBatchMode { get; set; } = false;
}
