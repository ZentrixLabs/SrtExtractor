using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SrtExtractor.Models;
using SrtExtractor.Services.Interfaces;
using SrtExtractor.State;

namespace SrtExtractor.Coordinators;

/// <summary>
/// Coordinates batch processing operations including queue management, 
/// processing multiple files, and handling batch statistics.
/// </summary>
public class BatchCoordinator
{
    private readonly ILoggingService _loggingService;
    private readonly INotificationService _notificationService;
    private readonly INetworkDetectionService _networkDetectionService;
    private readonly IFileCacheService _fileCacheService;
    private readonly ExtractionState _state;
    
    // Delegates for operations that need to be performed by MainViewModel
    private readonly Func<Task> _probeTracksAsync;
    private readonly Func<CancellationToken?, Task> _extractSubtitlesAsync;
    private readonly Action<string> _updateNetworkDetection;

    public BatchCoordinator(
        ILoggingService loggingService,
        INotificationService notificationService,
        INetworkDetectionService networkDetectionService,
        IFileCacheService fileCacheService,
        ExtractionState state,
        Func<Task> probeTracksAsync,
        Func<CancellationToken?, Task> extractSubtitlesAsync,
        Action<string> updateNetworkDetection)
    {
        _loggingService = loggingService;
        _notificationService = notificationService;
        _networkDetectionService = networkDetectionService;
        _fileCacheService = fileCacheService;
        _state = state;
        _probeTracksAsync = probeTracksAsync;
        _extractSubtitlesAsync = extractSubtitlesAsync;
        _updateNetworkDetection = updateNetworkDetection;
    }

    /// <summary>
    /// Process all files in the batch queue.
    /// </summary>
    public async Task ProcessBatchAsync(CancellationTokenSource? cancellationTokenSource)
    {
        if (!_state.BatchQueue.Any())
            return;

        await ProcessBatchFromIndexAsync(0, cancellationTokenSource);
    }

    /// <summary>
    /// Process batch files starting from a specific index.
    /// </summary>
    public async Task ProcessBatchFromIndexAsync(int startIndex, CancellationTokenSource? cancellationTokenSource)
    {
        if (!_state.BatchQueue.Any() || startIndex >= _state.BatchQueue.Count)
            return;

        try
        {
            // Start a new batch logging session with separate log file
            _loggingService.StartBatchSession();
            
            _state.IsBusy = true;
            _state.StartProcessing("Starting batch processing...");
            _state.AddLogMessage($"Starting batch processing of {_state.BatchQueue.Count} files from index {startIndex}");
            _loggingService.LogInfo($"Starting batch processing of {_state.BatchQueue.Count} files from index {startIndex}");

            var totalFiles = _state.BatchQueue.Count;
            var processedCount = startIndex;
            var successCount = _state.BatchQueue.Take(startIndex).Count(f => f.Status == BatchFileStatus.Completed);
            var errorCount = _state.BatchQueue.Take(startIndex).Count(f => f.Status == BatchFileStatus.Error);

            // Reset the last processed index
            _state.LastProcessedBatchIndex = startIndex - 1;

            foreach (var batchFile in _state.BatchQueue.Skip(startIndex).ToList())
            {
                // Check for cancellation before processing each file
                if (cancellationTokenSource?.Token.IsCancellationRequested == true)
                {
                    _state.AddLogMessage("Batch processing cancelled by user");
                    // Update progress to show current state before breaking
                    _state.UpdateBatchProgress(processedCount, totalFiles, "Batch processing cancelled");
                    break;
                }

                try
                {
                    _state.UpdateBatchProgress(processedCount, totalFiles, $"Processing {batchFile.FileName}...");
                    batchFile.Status = BatchFileStatus.Processing;
                    batchFile.StatusMessage = "Processing...";

                    // Set the current file as the active file for processing
                    _state.MkvPath = batchFile.FilePath;
                    _state.Tracks.Clear();
                    _state.SelectedTrack = null;

                    // Update network detection for this file
                    _updateNetworkDetection(batchFile.FilePath);
                    batchFile.UpdateNetworkStatus(_state.IsNetworkFile, _state.EstimatedProcessingTimeMinutes);

                    // Probe tracks
                    await _probeTracksAsync();
                    
                    if (_state.SelectedTrack == null)
                    {
                        throw new InvalidOperationException("No suitable track found for extraction");
                    }

                    // Extract subtitles with cancellation token
                    await _extractSubtitlesAsync(cancellationTokenSource?.Token);

                    // Check for cancellation after extraction
                    if (cancellationTokenSource?.Token.IsCancellationRequested == true)
                    {
                        batchFile.Status = BatchFileStatus.Cancelled;
                        batchFile.StatusMessage = "Cancelled";
                        _state.AddLogMessage($"⏹️ Cancelled: {batchFile.FileName}");
                        // Update progress to show current state before breaking
                        _state.UpdateBatchProgress(processedCount, totalFiles, "Batch processing cancelled");
                        break; // Exit the loop when cancelled
                    }

                    // Only increment success count after extraction is actually complete
                    batchFile.Status = BatchFileStatus.Completed;
                    batchFile.StatusMessage = "Completed successfully";
                    batchFile.OutputPath = _state.GenerateOutputFilename(batchFile.FilePath, _state.SelectedTrack);
                    
                    successCount++;
                    _state.AddLogMessage($"✅ Completed: {batchFile.FileName}");
                    
                    // Use fast statistics update during batch processing to reduce UI overhead
                    _state.UpdateBatchStatisticsFast();
                }
                catch (OperationCanceledException)
                {
                    batchFile.Status = BatchFileStatus.Cancelled;
                    batchFile.StatusMessage = "Cancelled";
                    _state.AddLogMessage($"⏹️ Cancelled: {batchFile.FileName}");
                    
                    // Update progress to show current state before breaking
                    _state.UpdateBatchProgress(processedCount, totalFiles, "Batch processing cancelled");
                    break; // Exit the loop when cancelled
                }
                catch (Exception ex)
                {
                    batchFile.Status = BatchFileStatus.Error;
                    batchFile.StatusMessage = $"Error: {ex.Message}";
                    errorCount++;
                    
                    _loggingService.LogError($"Failed to process batch file {batchFile.FileName}", ex);
                    _state.AddLogMessage($"❌ Failed: {batchFile.FileName} - {ex.Message}");
                    
                    // Use fast statistics update during batch processing to reduce UI overhead
                    _state.UpdateBatchStatisticsFast();
                }

                processedCount++;
                _state.LastProcessedBatchIndex = processedCount - 1;
            }

            // Update progress to 100% before stopping
            _state.UpdateBatchProgress(totalFiles, totalFiles, "Batch processing completed!");
            
            _state.StopProcessingWithProgress();
            
            // Final statistics update with full UI notifications after batch processing completes
            _state.UpdateBatchStatistics();
            
            // Create detailed summary
            var successfulFiles = _state.BatchQueue.Where(f => f.Status == BatchFileStatus.Completed).ToList();
            var errorFiles = _state.BatchQueue.Where(f => f.Status == BatchFileStatus.Error).ToList();
            var cancelledFiles = _state.BatchQueue.Where(f => f.Status == BatchFileStatus.Cancelled).ToList();
            
            // Log detailed summary
            _state.AddLogMessage($"🎯 Batch processing completed! Success: {successCount}, Errors: {errorCount}, Cancelled: {cancelledFiles.Count}");
            
            if (successfulFiles.Any())
            {
                _state.AddLogMessage("✅ Successful files:");
                foreach (var file in successfulFiles)
                {
                    _state.AddLogMessage($"   • {file.FileName}");
                }
            }
            
            if (errorFiles.Any())
            {
                _state.AddLogMessage("❌ Failed files:");
                foreach (var file in errorFiles)
                {
                    _state.AddLogMessage($"   • {file.FileName} - {file.StatusMessage}");
                }
            }
            
            if (cancelledFiles.Any())
            {
                _state.AddLogMessage("⏹️ Cancelled files:");
                foreach (var file in cancelledFiles)
                {
                    _state.AddLogMessage($"   • {file.FileName}");
                }
            }

            // Create detailed message box
            var message = $"Batch processing completed!\n\n" +
                         $"Total files: {totalFiles}\n" +
                         $"Successful: {successCount}\n" +
                         $"Errors: {errorCount}\n" +
                         $"Cancelled: {cancelledFiles.Count}\n\n";
            
            if (successfulFiles.Any())
            {
                message += "✅ Successful files:\n";
                foreach (var file in successfulFiles)
                {
                    message += $"   • {file.FileName}\n";
                }
                message += "\n";
            }
            
            if (errorFiles.Any())
            {
                message += "❌ Failed files:\n";
                foreach (var file in errorFiles)
                {
                    message += $"   • {file.FileName} - {file.StatusMessage}\n";
                }
                message += "\n";
            }
            
            if (cancelledFiles.Any())
            {
                message += "⏹️ Cancelled files:\n";
                foreach (var file in cancelledFiles)
                {
                    message += $"   • {file.FileName}\n";
                }
            }

            if (errorCount > 0)
            {
                _notificationService.ShowWarning(message, "Batch Processing Complete");
            }
            else
            {
                _notificationService.ShowInfo(message, "Batch Processing Complete");
            }
            
            // Keep the batch queue visible after completion so users can review results
            // Users can manually clear the queue using "Clear All" or "Clear Completed" buttons
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Batch processing failed", ex);
            _state.AddLogMessage($"Batch processing failed: {ex.Message}");
            _notificationService.ShowError($"Batch processing failed:\n{ex.Message}", "Batch Processing Error");
        }
        finally
        {
            _state.IsBusy = false;
            _state.StopProcessingWithProgress();
            
            // End the batch logging session and clean up old logs
            _loggingService.EndBatchSession();
        }
    }

    /// <summary>
    /// Clear the batch queue.
    /// </summary>
    public void ClearBatchQueue()
    {
        try
        {
            _state.ClearBatchQueue();
            _state.AddLogMessage("Batch queue cleared");
            _loggingService.LogInfo("User cleared batch queue");
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to clear batch queue", ex);
        }
    }

    /// <summary>
    /// Clear only completed items from the batch queue.
    /// </summary>
    public void ClearCompletedBatchItems()
    {
        try
        {
            var completedCount = _state.BatchCompletedCount;
            _state.ClearCompletedBatchItems();
            _state.AddLogMessage($"Cleared {completedCount} completed items from batch queue");
            _loggingService.LogInfo($"User cleared {completedCount} completed batch items");
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to clear completed batch items", ex);
        }
    }

    /// <summary>
    /// Resume batch processing from where it was interrupted.
    /// </summary>
    public async Task ResumeBatchAsync(CancellationTokenSource? cancellationTokenSource)
    {
        try
        {
            if (!_state.AreToolsAvailable)
            {
                _notificationService.ShowWarning("Required tools are not available. Please install MKVToolNix and Subtitle Edit first.", "Tools Required");
                return;
            }

            var startIndex = _state.LastProcessedBatchIndex + 1;
            var remainingFiles = _state.BatchQueue.Skip(startIndex).ToList();
            
            if (!remainingFiles.Any())
            {
                _state.AddLogMessage("No remaining files to process in batch queue");
                return;
            }

            _state.AddLogMessage($"Resuming batch processing from file {startIndex + 1} of {_state.BatchQueue.Count}");
            _loggingService.LogInfo($"User resumed batch processing from index {startIndex}");

            // Start processing from the next unprocessed file
            await ProcessBatchFromIndexAsync(startIndex, cancellationTokenSource);
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to resume batch processing", ex);
            _state.AddLogMessage($"Failed to resume batch processing: {ex.Message}");
            _notificationService.ShowError($"Failed to resume batch processing:\n{ex.Message}", "Resume Error");
        }
    }

    /// <summary>
    /// Remove a file from the batch queue.
    /// </summary>
    public void RemoveFromBatch(BatchFile? batchFile)
    {
        try
        {
            if (batchFile != null)
            {
                _state.RemoveFromBatchQueue(batchFile);
                _state.AddLogMessage($"Removed from queue: {batchFile.FileName}");
                _loggingService.LogInfo($"User removed file from batch queue: {batchFile.FileName}");
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to remove file from batch queue", ex);
        }
    }

    /// <summary>
    /// Add files to batch queue from drag and drop.
    /// Uses bulk add to prevent excessive UI updates.
    /// </summary>
    public async Task AddFilesToBatchQueueAsync(string[] filePaths)
    {
        try
        {
            var addedCount = 0;
            var skippedCount = 0;
            var filesToAdd = new List<BatchFile>();

            // First pass: validate and prepare files without adding to ObservableCollection
            foreach (var filePath in filePaths)
            {
                // Check for duplicates
                if (string.IsNullOrEmpty(filePath) || 
                    _state.BatchQueue.Any(f => f.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
                {
                    skippedCount++;
                    continue;
                }

                var batchFile = new BatchFile { FilePath = filePath };
                await batchFile.UpdateFromFileSystemAsync(_fileCacheService);
                
                // Update network detection
                var isNetwork = _networkDetectionService.IsNetworkPath(filePath);
                var estimatedMinutes = _networkDetectionService.GetEstimatedProcessingTime(filePath);
                batchFile.UpdateNetworkStatus(isNetwork, estimatedMinutes);
                
                filesToAdd.Add(batchFile);
                addedCount++;
            }

            // Second pass: bulk add to ObservableCollection (single UI update per file, but better than alternative)
            // Note: WPF doesn't support true bulk operations on ObservableCollection,
            // but this pattern is still better than State.AddToBatchQueue which does more work per item
            foreach (var file in filesToAdd)
            {
                _state.BatchQueue.Add(file);
            }

            // Update statistics once at the end
            if (filesToAdd.Any())
            {
                _state.TotalBatchFiles = _state.BatchQueue.Count;
                _state.UpdateBatchStatistics();
                // Note: UpdateBatchStatistics() already calls OnPropertyChanged for computed properties
            }

            if (addedCount > 0)
            {
                _state.AddLogMessage($"Added {addedCount} files to batch queue");
                _loggingService.LogInfo($"Added {addedCount} files to batch queue via drag and drop");
            }

            if (skippedCount > 0)
            {
                _state.AddLogMessage($"Skipped {skippedCount} duplicate files");
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to add files to batch queue", ex);
            _state.AddLogMessage($"Error adding files to batch queue: {ex.Message}");
        }
    }

    /// <summary>
    /// Move a batch item to the top of the queue.
    /// </summary>
    public void MoveBatchItemToTop(BatchFile batchFile)
    {
        try
        {
            if (_state.BatchQueue.Contains(batchFile))
            {
                _state.BatchQueue.Remove(batchFile);
                _state.BatchQueue.Insert(0, batchFile);
                _state.AddLogMessage($"Moved to top of queue: {batchFile.FileName}");
                _loggingService.LogInfo($"User moved batch item to top: {batchFile.FileName}");
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Error moving batch item to top: {batchFile.FileName}", ex);
        }
    }

    /// <summary>
    /// Move a batch item to the bottom of the queue.
    /// </summary>
    public void MoveBatchItemToBottom(BatchFile batchFile)
    {
        try
        {
            if (_state.BatchQueue.Contains(batchFile))
            {
                _state.BatchQueue.Remove(batchFile);
                _state.BatchQueue.Add(batchFile);
                _state.AddLogMessage($"Moved to bottom of queue: {batchFile.FileName}");
                _loggingService.LogInfo($"User moved batch item to bottom: {batchFile.FileName}");
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Error moving batch item to bottom: {batchFile.FileName}", ex);
        }
    }

    /// <summary>
    /// Reorder batch queue items by dragging and dropping.
    /// </summary>
    public void ReorderBatchQueue(BatchFile draggedItem, BatchFile targetItem)
    {
        try
        {
            if (_state.BatchQueue.Contains(draggedItem) && _state.BatchQueue.Contains(targetItem) && draggedItem != targetItem)
            {
                var draggedIndex = _state.BatchQueue.IndexOf(draggedItem);
                var targetIndex = _state.BatchQueue.IndexOf(targetItem);
                
                _state.BatchQueue.RemoveAt(draggedIndex);
                
                // Adjust target index if we removed an item before it
                if (draggedIndex < targetIndex)
                {
                    targetIndex--;
                }
                
                _state.BatchQueue.Insert(targetIndex, draggedItem);
                
                _state.AddLogMessage($"Reordered queue: {draggedItem.FileName} moved to position {targetIndex + 1}");
                _loggingService.LogInfo($"User reordered batch queue: {draggedItem.FileName} moved to position {targetIndex + 1}");
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Error reordering batch queue: {draggedItem.FileName}", ex);
        }
    }

    /// <summary>
    /// Process a single file from the batch queue.
    /// </summary>
    public async Task ProcessSingleBatchFileAsync(BatchFile? batchFile)
    {
        if (batchFile == null)
        {
            _loggingService.LogWarning("ProcessSingleBatchFileAsync called with null batchFile");
            return;
        }

        try
        {
            if (!_state.AreToolsAvailable)
            {
                _notificationService.ShowWarning("Required tools are not available. Please install MKVToolNix and Subtitle Edit first.", "Tools Required");
                return;
            }

            _state.AddLogMessage($"Processing single file: {batchFile.FileName}");
            _loggingService.LogInfo($"User requested single file processing: {batchFile.FilePath}");

            // Set the current file path and probe tracks
            _state.MkvPath = batchFile.FilePath;
            await _probeTracksAsync();

            // If we found tracks, extract the best one
            if (_state.SelectedTrack != null)
            {
                await _extractSubtitlesAsync(CancellationToken.None);
                
                // Mark this batch item as completed
                batchFile.Status = BatchFileStatus.Completed;
                batchFile.StatusMessage = "Processed successfully";
                
                _state.AddLogMessage($"✅ Single file processing completed: {batchFile.FileName}");
            }
            else
            {
                batchFile.Status = BatchFileStatus.Error;
                batchFile.StatusMessage = "No suitable tracks found";
                _state.AddLogMessage($"❌ No suitable tracks found in: {batchFile.FileName}");
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Error processing single batch file: {batchFile.FileName}", ex);
            batchFile.Status = BatchFileStatus.Error;
            batchFile.StatusMessage = ex.Message;
            _state.AddLogMessage($"❌ Error processing {batchFile.FileName}: {ex.Message}");
        }
    }
}

