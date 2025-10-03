using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using SrtExtractor.Models;
using SrtExtractor.Services.Interfaces;
using SrtExtractor.ViewModels;

namespace SrtExtractor.Views;

/// <summary>
/// Interaction logic for SrtCorrectionWindow.xaml
/// </summary>
public partial class SrtCorrectionWindow : Window
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ISrtCorrectionService _srtCorrectionService;
    private readonly ILoggingService _loggingService;
    private string? _selectedSingleFile;
    private string? _selectedFolder;
    private List<SrtFileInfo> _srtFiles = new();
    private CancellationTokenSource? _batchCancellationTokenSource;

    public SrtCorrectionWindow(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _srtCorrectionService = serviceProvider.GetRequiredService<ISrtCorrectionService>();
        _loggingService = serviceProvider.GetRequiredService<ILoggingService>();
        
        InitializeComponent();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    #region Single File Processing

    private void SelectSingleFile_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Select SRT File",
            Filter = "SRT Files (*.srt)|*.srt|All Files (*.*)|*.*",
            DefaultExt = "srt"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            _selectedSingleFile = openFileDialog.FileName;
            SingleFileStatus.Text = Path.GetFileName(_selectedSingleFile);
            SingleFilePath.Text = _selectedSingleFile;
            SingleFilePath.Visibility = Visibility.Visible;
            ProcessSingleButton.IsEnabled = true;
            
            _loggingService.LogInfo($"Selected SRT file for correction: {_selectedSingleFile}");
        }
    }

    private async void ProcessSingleFile_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedSingleFile))
            return;

        try
        {
            ProcessSingleButton.IsEnabled = false;
            ProcessSingleButton.Content = "Processing...";
            
            var stopwatch = Stopwatch.StartNew();
            
            // Create backup if requested
            if (CreateBackupCheckbox.IsChecked == true)
            {
                var backupPath = _selectedSingleFile + ".bak";
                File.Copy(_selectedSingleFile, backupPath, overwrite: true);
                _loggingService.LogInfo($"Created backup: {backupPath}");
            }

            // Apply corrections
            var correctionsApplied = await _srtCorrectionService.CorrectSrtFileAsync(_selectedSingleFile);
            
            stopwatch.Stop();
            
            // Show results
            SingleFileCorrections.Text = correctionsApplied.ToString();
            SingleFileTime.Text = $"{stopwatch.ElapsedMilliseconds}ms";
            SingleFileResults.Visibility = Visibility.Visible;
            
            _loggingService.LogInfo($"Single file correction completed: {correctionsApplied} corrections in {stopwatch.ElapsedMilliseconds}ms");
            
            MessageBox.Show($"SRT correction completed!\n\nCorrections applied: {correctionsApplied}\nProcessing time: {stopwatch.ElapsedMilliseconds}ms", 
                          "Correction Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Error during single file correction", ex);
            MessageBox.Show($"Error correcting SRT file: {ex.Message}", 
                          "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ProcessSingleButton.IsEnabled = true;
            ProcessSingleButton.Content = "🔧 Correct File";
        }
    }

    #endregion

    #region Batch Processing

    private void SelectFolder_Click(object sender, RoutedEventArgs e)
    {
        var openFolderDialog = new OpenFolderDialog
        {
            Title = "Select Folder Containing SRT Files"
        };

        if (openFolderDialog.ShowDialog() == true)
        {
            _selectedFolder = openFolderDialog.FolderName;
            FolderStatus.Text = Path.GetFileName(_selectedFolder);
            FolderPath.Text = _selectedFolder;
            FolderPath.Visibility = Visibility.Visible;
            ScanButton.IsEnabled = true;
            
            _loggingService.LogInfo($"Selected folder for batch correction: {_selectedFolder}");
        }
    }

    private void ScanFolder_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedFolder))
            return;

        try
        {
            ScanButton.IsEnabled = false;
            ScanButton.Content = "Scanning...";
            
            _srtFiles.Clear();
            
            var searchOption = IncludeSubfoldersCheckbox.IsChecked == true 
                ? SearchOption.AllDirectories 
                : SearchOption.TopDirectoryOnly;
            
            var srtFiles = Directory.GetFiles(_selectedFolder, "*.srt", searchOption);
            
            foreach (var file in srtFiles)
            {
                _srtFiles.Add(new SrtFileInfo(file));
            }
            
            SrtFilesListBox.ItemsSource = _srtFiles;
            
            if (_srtFiles.Count > 0)
            {
                SrtFilesListBox.Visibility = Visibility.Visible;
                NoFilesMessage.Visibility = Visibility.Collapsed;
                StartBatchButton.IsEnabled = true;
                
                FolderStatus.Text = $"Found {_srtFiles.Count} SRT files";
            }
            else
            {
                SrtFilesListBox.Visibility = Visibility.Collapsed;
                NoFilesMessage.Visibility = Visibility.Visible;
                StartBatchButton.IsEnabled = false;
                
                FolderStatus.Text = "No SRT files found";
            }
            
            _loggingService.LogInfo($"Scanned folder: found {_srtFiles.Count} SRT files");
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Error scanning folder", ex);
            MessageBox.Show($"Error scanning folder: {ex.Message}", 
                          "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ScanButton.IsEnabled = true;
            ScanButton.Content = "🔍 Scan Folder";
        }
    }

    private async void StartBatchCorrection_Click(object sender, RoutedEventArgs e)
    {
        if (_srtFiles.Count == 0)
            return;

        try
        {
            _batchCancellationTokenSource = new CancellationTokenSource();
            
            StartBatchButton.IsEnabled = false;
            CancelBatchButton.IsEnabled = true;
            BatchProgressBar.Visibility = Visibility.Visible;
            BatchProgressBar.IsIndeterminate = true;
            
            var stopwatch = Stopwatch.StartNew();
            var totalCorrections = 0;
            var processedFiles = 0;
            
            _loggingService.LogInfo($"Starting batch correction of {_srtFiles.Count} files");
            
            for (int i = 0; i < _srtFiles.Count; i++)
            {
                if (_batchCancellationTokenSource.Token.IsCancellationRequested)
                    break;
                
                var file = _srtFiles[i];
                
                try
                {
                    // Update progress
                    var progress = (double)(i + 1) / _srtFiles.Count * 100;
                    BatchProgressBar.Value = progress;
                    BatchProgressBar.IsIndeterminate = false;
                    BatchProgressText.Text = $"Processing {file.FileName} ({i + 1}/{_srtFiles.Count})";
                    
                    // Update file status
                    file.Status = "Processing...";
                    
                    // Create backup if requested
                    if (CreateBackupBatchCheckbox.IsChecked == true)
                    {
                        var backupPath = file.FilePath + ".bak";
                        File.Copy(file.FilePath, backupPath, overwrite: true);
                    }
                    
                    // Apply corrections
                    var corrections = await _srtCorrectionService.CorrectSrtFileAsync(file.FilePath, _batchCancellationTokenSource.Token);
                    
                    // Update file info
                    file.CorrectionsApplied = corrections;
                    file.Status = $"{corrections} corrections";
                    file.IsProcessed = true;
                    
                    totalCorrections += corrections;
                    processedFiles++;
                    
                    _loggingService.LogInfo($"Processed {file.FileName}: {corrections} corrections");
                }
                catch (OperationCanceledException)
                {
                    file.Status = "Cancelled";
                    break;
                }
                catch (Exception ex)
                {
                    file.Status = "Failed";
                    file.ErrorMessage = ex.Message;
                    _loggingService.LogError($"Error processing {file.FileName}", ex);
                }
            }
            
            stopwatch.Stop();
            
            // Show results
            BatchFilesProcessed.Text = processedFiles.ToString();
            BatchTotalCorrections.Text = totalCorrections.ToString();
            BatchProcessingTime.Text = $"{stopwatch.ElapsedMilliseconds}ms";
            BatchAverageCorrections.Text = processedFiles > 0 ? (totalCorrections / processedFiles).ToString() : "0";
            BatchResults.Visibility = Visibility.Visible;
            
            BatchProgressText.Text = $"Completed: {processedFiles} files, {totalCorrections} corrections";
            BatchProgressBar.Value = 100;
            
            _loggingService.LogInfo($"Batch correction completed: {processedFiles} files, {totalCorrections} corrections in {stopwatch.ElapsedMilliseconds}ms");
            
            MessageBox.Show($"Batch correction completed!\n\nFiles processed: {processedFiles}\nTotal corrections: {totalCorrections}\nProcessing time: {stopwatch.ElapsedMilliseconds}ms", 
                          "Batch Correction Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Error during batch correction", ex);
            MessageBox.Show($"Error during batch correction: {ex.Message}", 
                          "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            StartBatchButton.IsEnabled = true;
            CancelBatchButton.IsEnabled = false;
            BatchProgressBar.Visibility = Visibility.Collapsed;
            
            if (_batchCancellationTokenSource != null)
            {
                _batchCancellationTokenSource.Dispose();
                _batchCancellationTokenSource = null;
            }
        }
    }

    private void CancelBatch_Click(object sender, RoutedEventArgs e)
    {
        _batchCancellationTokenSource?.Cancel();
        BatchProgressText.Text = "Cancelling...";
    }

    #endregion
}
