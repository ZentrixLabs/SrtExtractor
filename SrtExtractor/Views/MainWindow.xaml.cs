using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using SrtExtractor.Services.Interfaces;
using SrtExtractor.Services.Implementations;
using SrtExtractor.ViewModels;
using SrtExtractor.Models;
using System.Diagnostics;
using Microsoft.Win32;

namespace SrtExtractor.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IWindowStateService _windowStateService;
        private readonly ILoggingService _loggingService;
        private CancellationTokenSource? _saveStateCts;
        private readonly TimeSpan _saveStateDebounce = TimeSpan.FromSeconds(1);

        public MainWindow(MainViewModel viewModel, IServiceProvider serviceProvider, IWindowStateService windowStateService, ILoggingService loggingService)
        {
            _serviceProvider = serviceProvider;
            _windowStateService = windowStateService;
            _loggingService = loggingService;
            InitializeComponent();
            DataContext = viewModel;
            
            // Note: Drag and drop is now handled by the queue panel specifically
            
            // Subscribe to recent files changes
            if (viewModel.State != null)
            {
                viewModel.State.PropertyChanged += State_PropertyChanged;
            }
            
            // Subscribe to window events for state persistence
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            Closed += MainWindow_Closed;
            LocationChanged += MainWindow_LocationChanged;
            SizeChanged += MainWindow_SizeChanged;
            StateChanged += MainWindow_StateChanged;
            
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.State.ClearLog();
            }
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            var aboutWindow = new AboutWindow();
            aboutWindow.Owner = this;
            aboutWindow.ShowDialog();
        }

        private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            About_Click(sender, e);
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settingsWindow = _serviceProvider.GetRequiredService<SettingsWindow>();
                settingsWindow.Owner = this;
                settingsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening settings:\n{ex.Message}", "Settings Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void KeyboardShortcuts_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "SrtExtractor Keyboard Shortcuts\n\n" +
                "File Operations:\n" +
                "• Ctrl+O - Open Video File\n" +
                "• Alt+F4 - Exit Application\n\n" +
                "Processing:\n" +
                "• Ctrl+P - Probe Tracks\n" +
                "• Ctrl+E - Extract Subtitles\n" +
                "• Ctrl+C - Cancel Operation\n" +
                "• Escape - Cancel Operation\n\n" +
                "Tools & Settings:\n" +
                "• F5 - Re-detect Tools\n" +
                "• Ctrl+B - Toggle Batch Mode\n\n" +
                "Help:\n" +
                "• F1 - Show Help",
                "Keyboard Shortcuts",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void UserGuide_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var userGuideWindow = new UserGuideWindow();
                userGuideWindow.Owner = this;
                userGuideWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Error opening user guide", ex);
                MessageBox.Show($"Error opening user guide:\n{ex.Message}", "User Guide Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void PreferForcedMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.State.PreferForced = true;
            }
        }

        private void PreferClosedCaptionsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.State.PreferClosedCaptions = true;
            }
        }

        private void ThemeLight_Click(object sender, RoutedEventArgs e)
        {
            var themeService = _serviceProvider.GetRequiredService<IThemeService>();
            themeService.SetTheme("Light");
            _loggingService.LogInfo("User switched to Light theme");
        }

        private void ThemeDark_Click(object sender, RoutedEventArgs e)
        {
            var themeService = _serviceProvider.GetRequiredService<IThemeService>();
            themeService.SetTheme("Dark");
            _loggingService.LogInfo("User switched to Dark theme");
        }

        private void SrtCorrection_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Create the unified SRT correction window with dependency injection
                var srtCorrectionWindow = _serviceProvider.GetRequiredService<SrtCorrectionWindow>();
                srtCorrectionWindow.Owner = this;
                srtCorrectionWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening SRT correction:\n{ex.Message}", "SRT Correction Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void VobSubTrackAnalyzer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Create the VobSub Track Analyzer window with dependency injection
                var vobSubTrackAnalyzerWindow = _serviceProvider.GetRequiredService<VobSubTrackAnalyzerWindow>();
                vobSubTrackAnalyzerWindow.Owner = this;
                vobSubTrackAnalyzerWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Error opening VobSub Track Analyzer", ex);
                MessageBox.Show($"Error opening VobSub Track Analyzer:\n{ex.Message}", "VobSub Track Analyzer Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void QueuePanel_DragEnter(object sender, DragEventArgs e)
        {
            // Only allow drag & drop if batch mode is enabled
            if (DataContext is MainViewModel viewModel && viewModel.State.IsBatchMode && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                // Change the queue panel appearance to indicate drop target
                if (sender is GroupBox queuePanel)
                {
                    queuePanel.Background = System.Windows.Media.Brushes.LightBlue;
                    queuePanel.BorderBrush = System.Windows.Media.Brushes.Blue;
                    queuePanel.BorderThickness = new Thickness(2);
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void QueuePanel_DragOver(object sender, DragEventArgs e)
        {
            // Only allow drag & drop if batch mode is enabled
            if (DataContext is MainViewModel viewModel && viewModel.State.IsBatchMode && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void QueuePanel_DragLeave(object sender, DragEventArgs e)
        {
            // Restore original queue panel appearance
            if (sender is GroupBox queuePanel)
            {
                queuePanel.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F8FAFC"));
                queuePanel.BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E2E8F0"));
                queuePanel.BorderThickness = new Thickness(1);
            }
        }

        private void QueuePanel_Drop(object sender, DragEventArgs e)
        {
            try
            {
                // Restore original queue panel appearance
                if (sender is GroupBox queuePanel)
                {
                    queuePanel.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F8FAFC"));
                    queuePanel.BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E2E8F0"));
                    queuePanel.BorderThickness = new Thickness(1);
                }
                
                if (DataContext is not MainViewModel viewModel)
                    return;

                if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                    return;

                // Check if batch mode is enabled
                if (!viewModel.State.IsBatchMode)
                {
                    MessageBox.Show("Please enable Batch Mode first to use drag & drop functionality.\n\nCheck the 'Enable Batch Mode' checkbox in the Settings panel.", "Batch Mode Required", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files == null || files.Length == 0)
                    return;

                // Filter for supported video files only
                var videoExtensions = new[] { ".mkv", ".mp4" };
                var videoFiles = files.Where(file =>
                {
                    if (string.IsNullOrEmpty(file))
                        return false;

                    var extension = Path.GetExtension(file).ToLowerInvariant();
                    return File.Exists(file) && videoExtensions.Contains(extension);
                }).ToArray();

                if (videoFiles.Length == 0)
                {
                    MessageBox.Show("No valid video files found. Please drag MKV or MP4 files.", "Invalid Files", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Add files to batch queue
                viewModel.AddFilesToBatchQueue(videoFiles);

                // Show feedback
                if (videoFiles.Length == 1)
                {
                    MessageBox.Show($"Added {Path.GetFileName(videoFiles[0])} to the batch queue.", "File Added", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Added {videoFiles.Length} files to the batch queue.", "Files Added", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing dropped files:\n{ex.Message}", "Drag & Drop Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Load window state first
            await LoadWindowStateAsync();
            
            if (DataContext is MainViewModel viewModel)
            {
                // Populate recent files menu
                PopulateRecentFilesMenu();
                
                // Check if settings should be opened on startup
                if (viewModel.State.ShowSettingsOnStartup)
                {
                    viewModel.State.ShowSettingsOnStartup = false; // Reset the flag
                    
                    // Open settings window
                    try
                    {
                        var settingsWindow = _serviceProvider.GetRequiredService<SettingsWindow>();
                        settingsWindow.Owner = this;
                        settingsWindow.ShowDialog();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error opening settings:\n{ex.Message}", "Settings Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private async void State_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            try
            {
                if (e.PropertyName == nameof(MainViewModel.State.RecentFiles))
                {
                    Dispatcher.Invoke(PopulateRecentFilesMenu);
                }
                else if (e.PropertyName == nameof(MainViewModel.State.IsBatchMode) || 
                         e.PropertyName == nameof(MainViewModel.State.QueueColumnWidth))
                {
                    // Save window state when batch mode or queue column width changes
                    await SaveWindowStateAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Unhandled error in State_PropertyChanged", ex);
            }
        }

        private void PopulateRecentFilesMenu()
        {
            if (DataContext is not MainViewModel viewModel) return;

            // Clear existing recent file menu items (except the "No recent files" item)
            var itemsToRemove = RecentFilesMenuItem.Items.Cast<MenuItem>()
                .Where(item => item != NoRecentFilesMenuItem)
                .ToList();
            
            foreach (var item in itemsToRemove)
            {
                RecentFilesMenuItem.Items.Remove(item);
            }

            // Show/hide "No recent files" message
            NoRecentFilesMenuItem.Visibility = viewModel.State.RecentFiles.Count == 0 
                ? Visibility.Visible 
                : Visibility.Collapsed;

            // Add recent files
            foreach (var filePath in viewModel.State.RecentFiles)
            {
                var fileName = Path.GetFileName(filePath);
                var menuItem = new MenuItem
                {
                    Header = fileName,
                    ToolTip = filePath,
                    Command = viewModel.OpenRecentFileCommand,
                    CommandParameter = filePath
                };
                
                // Insert before the "No recent files" item
                RecentFilesMenuItem.Items.Insert(RecentFilesMenuItem.Items.Count - 1, menuItem);
            }
        }

        #region Window State Persistence

        /// <summary>
        /// Loads the window state from persistent storage and applies it to the window.
        /// </summary>
        private async Task LoadWindowStateAsync()
        {
            try
            {
                var windowState = await _windowStateService.LoadWindowStateAsync().ConfigureAwait(false);
                
                // Check if this is the first run (default values)
                var isFirstRun = windowState.Width == 1250 && windowState.Height == 900 && 
                                windowState.Left == 100 && windowState.Top == 100;
                
                // Apply window state on UI thread
                Dispatcher.Invoke(() =>
                {
                    // Apply window state
                    Width = windowState.Width;
                    Height = windowState.Height;
                    
                    if (isFirstRun)
                    {
                        // Center window on first run
                        WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    }
                    else
                    {
                        // Use saved position
                        Left = windowState.Left;
                        Top = windowState.Top;
                        WindowStartupLocation = WindowStartupLocation.Manual;
                    }
                    
                    WindowState = windowState.WindowStateEnum;
                });
                
                // Apply batch mode state if available (this can be done on any thread)
                if (DataContext is MainViewModel viewModel)
                {
                    viewModel.State.IsBatchMode = windowState.IsBatchMode;
                    viewModel.State.QueueColumnWidth = windowState.QueueColumnWidth;
                }
                
                if (isFirstRun)
                {
                    _loggingService.LogInfo("First run detected - centering window on screen");
                }
                _loggingService.LogInfo($"Window state loaded and applied: {windowState.Width}x{windowState.Height}");
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Failed to load window state", ex);
                
                // Fallback: center window on error (on UI thread)
                Dispatcher.Invoke(() =>
                {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen;
                });
            }
        }

        /// <summary>
        /// Saves the current window state to persistent storage.
        /// </summary>
        private async Task SaveWindowStateAsync()
        {
            try
            {
                var windowState = new Services.Interfaces.WindowState
                {
                    Width = Width,
                    Height = Height,
                    Left = Left,
                    Top = Top,
                    WindowStateEnum = WindowState,
                    QueueColumnWidth = DataContext is MainViewModel viewModel ? viewModel.State.QueueColumnWidth : 0,
                    IsBatchMode = DataContext is MainViewModel vm ? vm.State.IsBatchMode : false
                };
                
                await _windowStateService.SaveWindowStateAsync(windowState).ConfigureAwait(false);
                _loggingService.LogInfo("Window state saved");
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Failed to save window state", ex);
            }
        }

        /// <summary>
        /// Debounces window state saves to prevent excessive disk I/O during resize/move operations.
        /// </summary>
        private async Task DebounceWindowStateSaveAsync()
        {
            // Cancel any pending save
            _saveStateCts?.Cancel();
            _saveStateCts?.Dispose();
            _saveStateCts = new CancellationTokenSource();
            
            try
            {
                await Task.Delay(_saveStateDebounce, _saveStateCts.Token);
                await SaveWindowStateAsync();
            }
            catch (OperationCanceledException)
            {
                // Debounced - this is expected
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Error in debounced window state save", ex);
            }
        }

        /// <summary>
        /// Handles window closing event to save state.
        /// </summary>
        private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                await SaveWindowStateAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Unhandled error in MainWindow_Closing", ex);
            }
        }

        /// <summary>
        /// Handles window closed event to clean up event handlers and prevent memory leaks.
        /// </summary>
        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            try
            {
                // Unsubscribe from all events to prevent memory leaks
                if (DataContext is MainViewModel viewModel && viewModel.State != null)
                {
                    viewModel.State.PropertyChanged -= State_PropertyChanged;
                }
                
                Loaded -= MainWindow_Loaded;
                Closing -= MainWindow_Closing;
                Closed -= MainWindow_Closed;
                LocationChanged -= MainWindow_LocationChanged;
                SizeChanged -= MainWindow_SizeChanged;
                StateChanged -= MainWindow_StateChanged;
                
                // Cancel any pending debounced saves and dispose
                _saveStateCts?.Cancel();
                _saveStateCts?.Dispose();
                
                _loggingService.LogInfo("MainWindow event handlers unsubscribed successfully");
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Error unsubscribing MainWindow event handlers", ex);
            }
        }

        /// <summary>
        /// Handles window location changes to save state (debounced).
        /// </summary>
        private async void MainWindow_LocationChanged(object? sender, EventArgs e)
        {
            try
            {
                // Only save if window is not minimized or maximized
                if (WindowState == System.Windows.WindowState.Normal)
                {
                    await DebounceWindowStateSaveAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Unhandled error in MainWindow_LocationChanged", ex);
            }
        }

        /// <summary>
        /// Handles window size changes to save state (debounced).
        /// </summary>
        private async void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            try
            {
                // Only save if window is not minimized or maximized
                if (WindowState == System.Windows.WindowState.Normal)
                {
                    await DebounceWindowStateSaveAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Unhandled error in MainWindow_SizeChanged", ex);
            }
        }

        /// <summary>
        /// Handles window state changes (normal, minimized, maximized) to save state.
        /// </summary>
        private async void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            try
            {
                await SaveWindowStateAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Unhandled error in MainWindow_StateChanged", ex);
            }
        }

        #endregion

        #region Context Menu Event Handlers

        /// <summary>
        /// Shows detailed information about the selected subtitle track.
        /// </summary>
        private void ShowTrackDetails_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel && viewModel.State.SelectedTrack != null)
            {
                var track = viewModel.State.SelectedTrack;
                var details = $"Track Details:\n\n" +
                             $"ID: {track.Id}\n" +
                             $"Codec: {track.Codec}\n" +
                             $"Language: {track.Language}\n" +
                             $"Type: {track.TrackType}\n" +
                             $"Forced: {(track.Forced ? "Yes" : "No")}\n" +
                             $"Bitrate: {track.Bitrate:N0} bps\n" +
                             $"Frames: {track.FrameCount}\n" +
                             $"Duration: {track.Duration}\n" +
                             $"Name: {track.Name ?? "N/A"}";

                MessageBox.Show(details, "Track Details", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// Copies track information to clipboard.
        /// </summary>
        private void CopyTrackInfo_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel && viewModel.State.SelectedTrack != null)
            {
                var track = viewModel.State.SelectedTrack;
                var info = $"ID: {track.Id}, Codec: {track.Codec}, Language: {track.Language}, " +
                          $"Type: {track.TrackType}, Forced: {(track.Forced ? "Yes" : "No")}, " +
                          $"Bitrate: {track.Bitrate:N0} bps, Frames: {track.FrameCount}, " +
                          $"Duration: {track.Duration}";

                System.Windows.Clipboard.SetText(info);
                _loggingService.LogInfo("Track information copied to clipboard");
            }
        }

        /// <summary>
        /// Copies the full file path to clipboard.
        /// </summary>
        private void CopyFilePath_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel && !string.IsNullOrEmpty(viewModel.State.MkvPath))
            {
                System.Windows.Clipboard.SetText(viewModel.State.MkvPath);
                _loggingService.LogInfo("File path copied to clipboard");
            }
        }

        /// <summary>
        /// Copies just the file name to clipboard.
        /// </summary>
        private void CopyFileName_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel && !string.IsNullOrEmpty(viewModel.State.MkvPath))
            {
                var fileName = Path.GetFileName(viewModel.State.MkvPath);
                System.Windows.Clipboard.SetText(fileName);
                _loggingService.LogInfo("File name copied to clipboard");
            }
        }

        /// <summary>
        /// Opens the folder containing the current video file.
        /// </summary>
        private void OpenFileLocation_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel && !string.IsNullOrEmpty(viewModel.State.MkvPath))
            {
                try
                {
                    var folderPath = Path.GetDirectoryName(viewModel.State.MkvPath);
                    if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
                    {
                        Process.Start("explorer.exe", folderPath);
                        _loggingService.LogInfo($"Opened file location: {folderPath}");
                    }
                }
                catch (Exception ex)
                {
                    _loggingService.LogError("Error opening file location", ex);
                    MessageBox.Show($"Error opening file location: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Shows file properties dialog for the current video file.
        /// </summary>
        private void ShowFileProperties_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel && !string.IsNullOrEmpty(viewModel.State.MkvPath))
            {
                try
                {
                    if (File.Exists(viewModel.State.MkvPath))
                    {
                        Process.Start("explorer.exe", $"/select,\"{viewModel.State.MkvPath}\"");
                        _loggingService.LogInfo($"Opened file properties for: {viewModel.State.MkvPath}");
                    }
                }
                catch (Exception ex)
                {
                    _loggingService.LogError("Error opening file properties", ex);
                    MessageBox.Show($"Error opening file properties:\n{ex.Message}", "File Properties Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Copies all log text to clipboard.
        /// </summary>
        private void CopyLogText_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                System.Windows.Clipboard.SetText(viewModel.State.LogText);
                _loggingService.LogInfo("Log text copied to clipboard");
            }
        }

        /// <summary>
        /// Copies selected log text to clipboard.
        /// </summary>
        private void CopyLogSelection_Click(object sender, RoutedEventArgs e)
        {
            // This would need to be implemented with a reference to the TextBox
            // For now, just copy all text
            CopyLogText_Click(sender, e);
        }

        /// <summary>
        /// Saves log content to a text file.
        /// </summary>
        private void SaveLogToFile_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                try
                {
                    var saveDialog = new Microsoft.Win32.SaveFileDialog
                    {
                        Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                        DefaultExt = "txt",
                        FileName = $"SrtExtractor_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
                    };

                    if (saveDialog.ShowDialog() == true)
                    {
                        File.WriteAllText(saveDialog.FileName, viewModel.State.LogText);
                        _loggingService.LogInfo($"Log saved to: {saveDialog.FileName}");
                        MessageBox.Show($"Log saved successfully to:\n{saveDialog.FileName}", "Log Saved", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    _loggingService.LogError("Error saving log to file", ex);
                    MessageBox.Show($"Error saving log:\n{ex.Message}", "Log Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Opens the folder containing log files.
        /// </summary>
        private void OpenLogFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Use the same log directory path as LoggingService
                var logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), 
                                               "ZentrixLabs", "SrtExtractor", "Logs");
                
                if (Directory.Exists(logDirectory))
                {
                    Process.Start("explorer.exe", logDirectory);
                    _loggingService.LogInfo($"Opened log folder: {logDirectory}");
                }
                else
                {
                    MessageBox.Show("Log directory does not exist yet. Log files will be created when the application starts logging.", "Log Directory", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Error opening log folder", ex);
                MessageBox.Show($"Error opening log folder:\n{ex.Message}", "Log Folder Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Moves a batch item to the top of the queue.
        /// </summary>
        private void MoveBatchItemToTop_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is Models.BatchFile batchFile)
            {
                if (DataContext is MainViewModel viewModel)
                {
                    viewModel.MoveBatchItemToTop(batchFile);
                    _loggingService.LogInfo($"Moved batch item to top: {batchFile.FileName}");
                }
            }
        }

        /// <summary>
        /// Moves a batch item to the bottom of the queue.
        /// </summary>
        private void MoveBatchItemToBottom_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is Models.BatchFile batchFile)
            {
                if (DataContext is MainViewModel viewModel)
                {
                    viewModel.MoveBatchItemToBottom(batchFile);
                    _loggingService.LogInfo($"Moved batch item to bottom: {batchFile.FileName}");
                }
            }
        }

        /// <summary>
        /// Opens the folder containing a batch file.
        /// </summary>
        private void OpenBatchFileLocation_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is Models.BatchFile batchFile)
            {
                try
                {
                    var folderPath = Path.GetDirectoryName(batchFile.FilePath);
                    if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
                    {
                        Process.Start("explorer.exe", folderPath);
                        _loggingService.LogInfo($"Opened batch file location: {folderPath}");
                    }
                }
                catch (Exception ex)
                {
                    _loggingService.LogError("Error opening batch file location", ex);
                    MessageBox.Show($"Error opening file location:\n{ex.Message}", "File Location Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Shows file properties dialog for a batch file.
        /// </summary>
        private void ShowBatchFileProperties_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is Models.BatchFile batchFile)
            {
                try
                {
                    if (File.Exists(batchFile.FilePath))
                    {
                        Process.Start("explorer.exe", $"/select,\"{batchFile.FilePath}\"");
                        _loggingService.LogInfo($"Opened batch file properties for: {batchFile.FilePath}");
                    }
                }
                catch (Exception ex)
                {
                    _loggingService.LogError("Error opening batch file properties", ex);
                    MessageBox.Show($"Error opening file properties:\n{ex.Message}", "File Properties Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion

        #region Batch Queue Drag & Drop Reordering

        private Point _dragStartPoint;
        private BatchFile? _draggedItem;

        private void BatchQueueListBox_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is ListBox listBox)
            {
                _dragStartPoint = e.GetPosition(listBox);
                _draggedItem = null;
                
                // Find the item under the mouse
                var item = FindAnchestor<ListBoxItem>((DependencyObject)e.OriginalSource);
                if (item != null && item.DataContext is BatchFile batchFile)
                {
                    _draggedItem = batchFile;
                }
            }
        }

        private void BatchQueueListBox_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed && _draggedItem != null)
            {
                var position = e.GetPosition((IInputElement)sender);
                var distance = Math.Abs(position.X - _dragStartPoint.X) + Math.Abs(position.Y - _dragStartPoint.Y);
                
                if (distance > 10) // Minimum drag distance
                {
                    var data = new DataObject(typeof(BatchFile), _draggedItem);
                    DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Move);
                    
                    _draggedItem = null;
                }
            }
        }

        private void BatchQueueListBox_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _draggedItem = null;
        }

        private void BatchQueueListBox_DragLeave(object sender, DragEventArgs e)
        {
            // Clear any remaining highlights when drag leaves the ListBox
            if (sender is ListBox listBox)
            {
                foreach (var item in listBox.Items)
                {
                    if (listBox.ItemContainerGenerator.ContainerFromItem(item) is ListBoxItem listBoxItem)
                    {
                        listBoxItem.Background = System.Windows.Media.Brushes.Transparent;
                    }
                }
            }
        }

        private void BatchQueueListBox_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(BatchFile)))
            {
                e.Effects = DragDropEffects.Move;
                
                // Visual feedback - highlight the drop target
                if (sender is ListBox listBox)
                {
                    var targetListBoxItem = FindAnchestor<ListBoxItem>((DependencyObject)e.OriginalSource);
                    if (targetListBoxItem != null)
                    {
                        // Clear previous highlights
                        foreach (var item in listBox.Items)
                        {
                            if (listBox.ItemContainerGenerator.ContainerFromItem(item) is ListBoxItem listBoxItem)
                            {
                                listBoxItem.Background = System.Windows.Media.Brushes.Transparent;
                            }
                        }
                        
                        // Highlight current target
                        targetListBoxItem.Background = System.Windows.Media.Brushes.LightBlue;
                    }
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void BatchQueueListBox_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(BatchFile)))
            {
                var draggedItem = (BatchFile)e.Data.GetData(typeof(BatchFile));
                
                // Find the ListBoxItem that was dropped on
                var targetListBoxItem = FindAnchestor<ListBoxItem>((DependencyObject)e.OriginalSource);
                if (targetListBoxItem?.DataContext is BatchFile targetBatchFile && draggedItem != targetBatchFile)
                {
                    if (DataContext is MainViewModel viewModel)
                    {
                        viewModel.ReorderBatchQueue(draggedItem, targetBatchFile);
                    }
                }
                
                // Reset visual feedback
                if (sender is ListBox listBox)
                {
                    foreach (var item in listBox.Items)
                    {
                        if (listBox.ItemContainerGenerator.ContainerFromItem(item) is ListBoxItem listBoxItem)
                        {
                            listBoxItem.Background = System.Windows.Media.Brushes.Transparent;
                        }
                    }
                }
            }
        }

        private static T? FindAnchestor<T>(DependencyObject current) where T : class
        {
            do
            {
                if (current is T ancestor)
                {
                    return ancestor;
                }
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            while (current != null);
            return null;
        }

        #endregion

    }
}