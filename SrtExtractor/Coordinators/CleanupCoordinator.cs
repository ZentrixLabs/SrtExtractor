using System;
using System.IO;
using System.Threading.Tasks;
using SrtExtractor.Models;
using SrtExtractor.Services.Interfaces;
using SrtExtractor.State;

namespace SrtExtractor.Coordinators;

/// <summary>
/// Coordinates cleanup operations for temporary files and directories.
/// </summary>
public class CleanupCoordinator
{
    private readonly ILoggingService _loggingService;
    private readonly ExtractionState _state;

    public CleanupCoordinator(
        ILoggingService loggingService,
        ExtractionState state)
    {
        _loggingService = loggingService;
        _state = state;
    }

    /// <summary>
    /// Clean up all temporary files - VobSub directories and any leftover extraction files.
    /// </summary>
    public async Task CleanupTempFilesAsync()
    {
        try
        {
            _state.IsBusy = true;
            _state.UpdateProcessingMessage("Cleaning up temporary files...");
            _state.AddLogMessage("🧹 Cleaning up temporary files...");

            // Clean up VobSub temp directories
            await CleanupVobSubTempDirectories();

            _state.UpdateProcessingMessage("Cleanup completed!");
            _state.AddLogMessage("✅ Temporary files cleaned up successfully");
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to clean up temporary files", ex);
            _state.AddLogMessage($"❌ Failed to clean up temporary files: {ex.Message}");
        }
        finally
        {
            _state.IsBusy = false;
        }
    }

    /// <summary>
    /// Cleans up VobSub temporary directories created during extraction.
    /// </summary>
    public async Task CleanupVobSubTempDirectories()
    {
        try
        {
            var srtExtractorTempDir = Path.Combine(Path.GetTempPath(), "SrtExtractor");
            
            if (!Directory.Exists(srtExtractorTempDir))
            {
                _loggingService.LogInfo("No SrtExtractor temp directory found to clean up");
                return;
            }

            _loggingService.LogInfo($"Cleaning up VobSub temporary directories in: {srtExtractorTempDir}");

            // Get all subdirectories (each extraction creates a GUID-named directory)
            var tempDirectories = Directory.GetDirectories(srtExtractorTempDir);
            var cleanedCount = 0;

            foreach (var tempDir in tempDirectories)
            {
                try
                {
                    // Check if this directory contains VobSub files (.idx/.sub)
                    var idxFiles = Directory.GetFiles(tempDir, "*.idx");
                    var subFiles = Directory.GetFiles(tempDir, "*.sub");
                    
                    if (idxFiles.Length > 0 || subFiles.Length > 0)
                    {
                        _loggingService.LogInfo($"Cleaning up VobSub temp directory: {Path.GetFileName(tempDir)}");
                        
                        // Give processes time to release file handles
                        await Task.Delay(1000);
                        
                        // Delete the entire directory
                        Directory.Delete(tempDir, true);
                        cleanedCount++;
                    }
                }
                catch (IOException ex)
                {
                    _loggingService.LogWarning($"Could not clean up temp directory {Path.GetFileName(tempDir)}: {ex.Message}");
                }
                catch (UnauthorizedAccessException ex)
                {
                    _loggingService.LogWarning($"Access denied cleaning up temp directory {Path.GetFileName(tempDir)}: {ex.Message}");
                }
            }

            if (cleanedCount > 0)
            {
                _loggingService.LogInfo($"Successfully cleaned up {cleanedCount} VobSub temporary directories");
            }
            else
            {
                _loggingService.LogInfo("No VobSub temporary directories found to clean up");
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to clean up VobSub temporary directories", ex);
        }
    }

    /// <summary>
    /// Clean up temporary files that might have been created during extraction.
    /// This is called automatically after each extraction to ensure no temp files are left behind.
    /// Note: This is kept separate from ExtractionCoordinator's cleanup as it's used by batch processing too.
    /// </summary>
    public async Task CleanupTemporaryFiles(string mkvPath, SubtitleTrack? selectedTrack)
    {
        if (selectedTrack == null || string.IsNullOrEmpty(mkvPath))
            return;

        try
        {
            var outputPath = _state.GenerateOutputFilename(mkvPath, selectedTrack);
            
            // Check if this was a PGS extraction (which creates a .sup file)
            if (selectedTrack.Codec.Contains("PGS") || selectedTrack.Codec.Contains("S_HDMV/PGS"))
            {
                var tempSupPath = Path.ChangeExtension(outputPath, ".sup");
                
                if (File.Exists(tempSupPath))
                {
                    _loggingService.LogInfo($"Cleaning up temporary SUP file: {tempSupPath}");
                    
                    // Use exponential backoff with shorter total time to prevent UI blocking
                    var retryDelays = new[] { 100, 200, 500, 1000, 1500 }; // Total: max 3.3 seconds
                    for (int i = 0; i < retryDelays.Length; i++)
                    {
                        try
                        {
                            File.Delete(tempSupPath);
                            _loggingService.LogInfo($"Successfully cleaned up temporary file: {Path.GetFileName(tempSupPath)}");
                            break;
                        }
                        catch (IOException) when (i < retryDelays.Length - 1)
                        {
                            _loggingService.LogInfo($"File still in use, retrying in {retryDelays[i]}ms... (attempt {i + 1}/{retryDelays.Length})");
                            await Task.Delay(retryDelays[i]);
                        }
                        catch (UnauthorizedAccessException) when (i < retryDelays.Length - 1)
                        {
                            _loggingService.LogInfo($"Access denied, retrying in {retryDelays[i]}ms... (attempt {i + 1}/{retryDelays.Length})");
                            await Task.Delay(retryDelays[i]);
                        }
                        catch (Exception ex) when (i == retryDelays.Length - 1)
                        {
                            // Log final failure but don't throw - cleanup is best-effort
                            _loggingService.LogWarning($"Failed to clean up temporary file after {retryDelays.Length} attempts: {ex.Message}");
                        }
                    }
                }
            }
            // Check if this was a VobSub extraction (which creates temporary .idx/.sub files)
            else if (selectedTrack.Codec.Contains("VobSub") || selectedTrack.Codec.Contains("S_VOBSUB"))
            {
                // Clean up VobSub temporary directories
                await CleanupVobSubTempDirectories();
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to clean up temporary files", ex);
            // Don't throw - cleanup is best-effort
        }
    }
}

