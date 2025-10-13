using System.Diagnostics;
using System.IO;
using System.Text;
using SrtExtractor.Services.Interfaces;
using SrtExtractor.Utils;
using seconv.libse.BluRaySup;

namespace SrtExtractor.Services.Implementations;

/// <summary>
/// Service for performing OCR using command-line Tesseract on PGS subtitle images.
/// This bypasses the buggy Tesseract.NET library and calls tesseract.exe directly.
/// </summary>
public class TesseractOcrService : ITesseractOcrService
{
    private readonly ILoggingService _loggingService;
    private readonly ISettingsService _settingsService;
    private readonly IProcessRunner _processRunner;

    public TesseractOcrService(
        ILoggingService loggingService, 
        ISettingsService settingsService,
        IProcessRunner processRunner)
    {
        _loggingService = loggingService;
        _settingsService = settingsService;
        _processRunner = processRunner;
    }

    public async Task OcrSupToSrtAsync(
        string supPath,
        string outSrt,
        string language,
        CancellationToken cancellationToken = default)
    {
        // Call the overload with progress reporting (passing null for progress)
        await OcrSupToSrtAsync(supPath, outSrt, language, cancellationToken, null);
    }

    public async Task OcrSupToSrtAsync(
        string supPath,
        string outSrt,
        string language,
        CancellationToken cancellationToken = default,
        IProgress<(int processed, int total, string phase)>? progress = null)
    {
        var validatedSupPath = Path.GetFullPath(supPath);
        if (!File.Exists(validatedSupPath))
        {
            throw new FileNotFoundException($"SUP file not found: {supPath}");
        }
        
        SafeFileOperations.ValidateAndPrepareOutputPath(outSrt, supPath);

        // Parse the BluRay SUP file to extract subtitle images and timecodes
        progress?.Report((0, 0, "Parsing SUP file"));
        _loggingService.LogInfo($"Parsing PGS SUP file: {Path.GetFileName(validatedSupPath)}");
        var log = new StringBuilder();
        var bluRaySubtitles = BluRaySupParser.ParseBluRaySup(validatedSupPath, log);

        if (bluRaySubtitles == null || bluRaySubtitles.Count == 0)
        {
            throw new InvalidOperationException($"No subtitle images found in SUP file: {Path.GetFileName(validatedSupPath)}");
        }

        var totalFrames = bluRaySubtitles.Count;
        _loggingService.LogInfo($"Found {totalFrames} subtitle images to OCR using command-line Tesseract");

        // Find tesseract.exe
        progress?.Report((0, totalFrames, "Initializing Tesseract"));
        var tesseractExe = FindTesseractExecutable();
        if (string.IsNullOrEmpty(tesseractExe))
        {
            throw new FileNotFoundException("tesseract.exe not found. Please install Tesseract OCR from https://github.com/UB-Mannheim/tesseract/wiki");
        }

        _loggingService.LogInfo($"Using Tesseract executable: {tesseractExe}");

        // Build SRT content
        var srtBuilder = new StringBuilder();
        int subtitleIndex = 1;

        progress?.Report((0, totalFrames, "Processing frames"));

        foreach (var pcsData in bluRaySubtitles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Get the subtitle image bitmap
                var bitmap = pcsData.GetBitmap();
                if (bitmap == null || bitmap.Width == 0 || bitmap.Height == 0)
                {
                    _loggingService.LogInfo($"Skipping empty/invalid bitmap at index {subtitleIndex}");
                    progress?.Report((subtitleIndex - 1, totalFrames, "Processing frames"));
                    continue;
                }

                // Perform OCR using command-line tesseract
                var text = await OcrImageAsync(bitmap, language, cancellationToken);

                if (string.IsNullOrWhiteSpace(text))
                {
                    _loggingService.LogInfo($"No text recognized in subtitle {subtitleIndex}");
                    progress?.Report((subtitleIndex - 1, totalFrames, "Processing frames"));
                    continue;
                }

                // Calculate timestamps (PTS is in 90kHz units)
                var startTime = TimeSpan.FromMilliseconds(pcsData.StartTime / 90.0);
                var endTime = TimeSpan.FromMilliseconds(pcsData.EndTime / 90.0);

                // Add to SRT
                srtBuilder.AppendLine(subtitleIndex.ToString());
                srtBuilder.AppendLine($"{FormatSrtTime(startTime)} --> {FormatSrtTime(endTime)}");
                srtBuilder.AppendLine(text.Trim());
                srtBuilder.AppendLine();

                subtitleIndex++;
                
                // Report progress after processing each frame
                progress?.Report((subtitleIndex - 1, totalFrames, "Processing frames"));
            }
            catch (Exception ex)
            {
                _loggingService.LogWarning($"Failed to OCR subtitle at index {subtitleIndex}: {ex.Message}");
                progress?.Report((subtitleIndex - 1, totalFrames, "Processing frames"));
                // Continue with next subtitle
            }
        }

        // Write SRT file
        progress?.Report((totalFrames, totalFrames, "Saving SRT file"));
        await File.WriteAllTextAsync(outSrt, srtBuilder.ToString(), cancellationToken);
        _loggingService.LogInfo($"Successfully completed Tesseract OCR conversion ({language})");
    }

    /// <summary>
    /// Perform OCR on a single image using command-line tesseract.exe
    /// </summary>
    private async Task<string> OcrImageAsync(
        SkiaSharp.SKBitmap bitmap, 
        string language, 
        CancellationToken cancellationToken)
    {
        var tempImagePath = Path.Combine(Path.GetTempPath(), $"ocr_temp_{Guid.NewGuid()}.png");
        var tempOutputBase = Path.Combine(Path.GetTempPath(), $"ocr_output_{Guid.NewGuid()}");
        var tempOutputFile = tempOutputBase + ".txt";

        try
        {
            // Save bitmap as PNG
            using (var image = SkiaSharp.SKImage.FromBitmap(bitmap))
            using (var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100))
            {
                await File.WriteAllBytesAsync(tempImagePath, data.ToArray(), cancellationToken);
            }

            // Find tesseract.exe
            var tesseractExe = FindTesseractExecutable();
            if (string.IsNullOrEmpty(tesseractExe))
            {
                throw new FileNotFoundException("tesseract.exe not found");
            }

            // Get tessdata directory path
            var exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var tessdataPath = Path.Combine(exeDirectory, "tessdata");

            // Call tesseract.exe with PSM 6 (single uniform block of text)
            // Format: tesseract input.png output_base --tessdata-dir path --psm 6 -l eng
            var args = new[] { tempImagePath, tempOutputBase, "--tessdata-dir", tessdataPath, "--psm", "6", "-l", language };

            var (exitCode, stdOut, stdErr) = await _processRunner.RunAsync(
                tesseractExe,
                args,
                TimeSpan.FromSeconds(30),
                cancellationToken
            );

            if (exitCode != 0)
            {
                _loggingService.LogWarning($"Tesseract returned exit code {exitCode}: {stdErr}");
                return string.Empty;
            }

            // Read output from the .txt file that tesseract creates
            if (File.Exists(tempOutputFile))
            {
                var text = await File.ReadAllTextAsync(tempOutputFile, cancellationToken);
                _loggingService.LogInfo($"OCR result length: {text.Length} characters");
                return text;
            }

            _loggingService.LogWarning($"Tesseract output file not found: {tempOutputFile}");
            return string.Empty;
        }
        finally
        {
            // Cleanup temp files
            try
            {
                if (File.Exists(tempImagePath)) File.Delete(tempImagePath);
                if (File.Exists(tempOutputFile)) File.Delete(tempOutputFile);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    public async Task<bool> IsTesseractAvailableAsync()
    {
        try
        {
            var tesseractExe = FindTesseractExecutable();
            if (string.IsNullOrEmpty(tesseractExe))
            {
                _loggingService.LogInfo("Tesseract executable not found");
                return false;
            }

            // Try to run tesseract --version to verify it works
            var (exitCode, _, _) = await _processRunner.RunAsync(
                tesseractExe,
                new[] { "--version" },
                TimeSpan.FromSeconds(5),
                CancellationToken.None
            );

            return exitCode == 0;
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Error checking Tesseract availability", ex);
            return false;
        }
    }

    /// <summary>
    /// Find tesseract.exe - checks bundled version first, then system installation
    /// </summary>
    private string? FindTesseractExecutable()
    {
        // Priority 1: Check bundled tesseract.exe next to our executable
        var exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var bundledTesseract = Path.Combine(exeDirectory, "tesseract-bin", "tesseract.exe");
        if (File.Exists(bundledTesseract))
        {
            _loggingService.LogInfo($"Using bundled Tesseract: {bundledTesseract}");
            return bundledTesseract;
        }

        // Priority 2: Check system installation paths (Windows)
        var searchPaths = new[]
        {
            @"C:\Program Files\Tesseract-OCR\tesseract.exe",
            @"C:\Program Files (x86)\Tesseract-OCR\tesseract.exe",
            @"C:\Tesseract-OCR\tesseract.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tesseract-OCR", "tesseract.exe")
        };

        foreach (var path in searchPaths)
        {
            if (File.Exists(path))
            {
                _loggingService.LogInfo($"Using system Tesseract: {path}");
                return path;
            }
        }

        // Priority 3: Check PATH environment variable
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            var paths = pathEnv.Split(Path.PathSeparator);
            foreach (var dir in paths)
            {
                try
                {
                    var fullPath = Path.Combine(dir, "tesseract.exe");
                    if (File.Exists(fullPath))
                    {
                        _loggingService.LogInfo($"Using PATH Tesseract: {fullPath}");
                        return fullPath;
                    }
                }
                catch
                {
                    // Continue to next path
                }
            }
        }

        _loggingService.LogWarning("Tesseract.exe not found in bundled location or system installation");
        return null;
    }

    /// <summary>
    /// Format TimeSpan as SRT timestamp (HH:MM:SS,mmm).
    /// </summary>
    private static string FormatSrtTime(TimeSpan time)
    {
        return $"{(int)time.TotalHours:D2}:{time.Minutes:D2}:{time.Seconds:D2},{time.Milliseconds:D3}";
    }
}
