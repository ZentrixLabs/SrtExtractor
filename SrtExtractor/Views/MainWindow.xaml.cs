using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using SrtExtractor.ViewModels;

namespace SrtExtractor.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly IServiceProvider _serviceProvider;

        public MainWindow(MainViewModel viewModel, IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            InitializeComponent();
            DataContext = viewModel;
            
            // Note: Drag and drop is now handled by the queue panel specifically
            
            // Check if settings should be opened on startup
            Loaded += MainWindow_Loaded;
            
            // Subscribe to recent files changes
            if (viewModel.State != null)
            {
                viewModel.State.PropertyChanged += State_PropertyChanged;
            }
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
                MessageBox.Show($"Error opening settings: {ex.Message}", 
                              "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            MessageBox.Show("User Guide coming soon!\n\nFor now, please refer to the README file in the project repository:\nhttps://github.com/ZentrixLabs/SrtExtractor", 
                          "User Guide", MessageBoxButton.OK, MessageBoxImage.Information);
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
                MessageBox.Show($"Error opening SRT correction: {ex.Message}", 
                              "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    MessageBox.Show("Please enable Batch Mode first to use drag & drop functionality.\n\nCheck the 'Enable Batch Mode' checkbox in the Settings panel.", 
                                  "Batch Mode Required", MessageBoxButton.OK, MessageBoxImage.Information);
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
                    MessageBox.Show("No valid video files found. Please drag MKV or MP4 files.", 
                                  "Invalid Files", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Add files to batch queue
                viewModel.AddFilesToBatchQueue(videoFiles);

                // Show feedback
                if (videoFiles.Length == 1)
                {
                    MessageBox.Show($"Added {Path.GetFileName(videoFiles[0])} to the batch queue.", 
                                  "File Added", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Added {videoFiles.Length} files to the batch queue.", 
                                  "Files Added", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing dropped files: {ex.Message}", 
                              "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
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
                        MessageBox.Show($"Error opening settings: {ex.Message}", 
                                      "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void State_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.State.RecentFiles))
            {
                Dispatcher.Invoke(PopulateRecentFilesMenu);
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
    }
}