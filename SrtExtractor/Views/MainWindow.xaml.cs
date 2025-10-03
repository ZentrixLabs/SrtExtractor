using System.IO;
using System.Linq;
using System.Windows;
using SrtExtractor.ViewModels;

namespace SrtExtractor.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            
            // Enable drag and drop
            AllowDrop = true;
            DragEnter += MainWindow_DragEnter;
            DragOver += MainWindow_DragOver;
            DragLeave += MainWindow_DragLeave;
            Drop += MainWindow_Drop;
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

        private void MainWindow_DragEnter(object sender, DragEventArgs e)
        {
            // Check if the dragged data contains files
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                DragOverlay.Visibility = Visibility.Visible;
            }
            else
            {
                e.Effects = DragDropEffects.None;
                DragOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void MainWindow_DragOver(object sender, DragEventArgs e)
        {
            // Maintain the copy effect during drag over
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                DragOverlay.Visibility = Visibility.Visible;
            }
            else
            {
                e.Effects = DragDropEffects.None;
                DragOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void MainWindow_DragLeave(object sender, DragEventArgs e)
        {
            DragOverlay.Visibility = Visibility.Collapsed;
        }

        private void MainWindow_Drop(object sender, DragEventArgs e)
        {
            try
            {
                DragOverlay.Visibility = Visibility.Collapsed;
                
                if (DataContext is not MainViewModel viewModel)
                    return;

                if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                    return;

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

                // Show feedback only if not in batch mode (to avoid interrupting batch processing)
                if (!viewModel.State.IsBatchMode)
                {
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
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing dropped files: {ex.Message}", 
                              "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}