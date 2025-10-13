using System.IO;
using SrtExtractor.Services.Interfaces;
using SrtExtractor.Utils;

namespace SrtExtractor.Services.Implementations;

/// <summary>
/// Service for OCR operations on subtitle files.
/// Uses Tesseract OCR for high-quality PGS subtitle recognition.
/// </summary>
public class SubtitleOcrService : ISubtitleOcrService
{
    private readonly ILoggingService _loggingService;
    private readonly ITesseractOcrService _tesseractOcrService;

    public SubtitleOcrService(
        ILoggingService loggingService,
        ITesseractOcrService tesseractOcrService)
    {
        _loggingService = loggingService;
        _tesseractOcrService = tesseractOcrService;
    }

    public async Task OcrSupToSrtAsync(
        string supPath, 
        string outSrt, 
        string language, 
        bool fixCommonErrors = true, 
        bool removeHi = true,
        CancellationToken cancellationToken = default)
    {
        _loggingService.LogInfo("Starting OCR conversion with Tesseract");

        // Check if Tesseract is available
        var tesseractAvailable = await _tesseractOcrService.IsTesseractAvailableAsync();
        if (!tesseractAvailable)
        {
            throw new InvalidOperationException(
                "Tesseract OCR is not available. Please ensure tessdata folder with language files is present.");
        }

        // Use Tesseract OCR for high-quality recognition
        _loggingService.LogInfo("Using Tesseract OCR engine for high-quality recognition");
        await _tesseractOcrService.OcrSupToSrtAsync(supPath, outSrt, language, cancellationToken);
        
        // CRITICAL FIX: Check if we need to convert output format (ASS/WebVTT to SRT)
        await ConvertAssToSrtIfNeededAsync(outSrt, cancellationToken);
    }

    /// <summary>
    /// Convert ASS or WebVTT format to SRT format if the file is not already in SRT format.
    /// Subtitle Edit CLI sometimes outputs ASS format even when told to output SRT.
    /// Similarly, mkvextract and ffmpeg preserve WebVTT format when extracting.
    /// </summary>
    /// <param name="filePath">Path to the subtitle file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task ConvertAssToSrtIfNeededAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var content = await File.ReadAllTextAsync(filePath, cancellationToken);
            string? srtContent = null;
            
            // Check if file is in ASS format (starts with [Script Info] or contains [Events] section)
            if (content.Contains("[Script Info]") || content.Contains("[Events]"))
            {
                _loggingService.LogInfo("Detected ASS format - converting to SRT format");
                srtContent = ConvertAssToSrt(content);
            }
            // Check if file is in WebVTT format (starts with WEBVTT or has WebVTT header)
            else if (content.TrimStart().StartsWith("WEBVTT") || content.Contains("\nWEBVTT"))
            {
                _loggingService.LogInfo("Detected WebVTT format - converting to SRT format");
                srtContent = ConvertWebVttToSrt(content);
            }
            
            // Write converted content if we detected a non-SRT format
            if (srtContent != null)
            {
                await File.WriteAllTextAsync(filePath, srtContent, cancellationToken);
                _loggingService.LogInfo("Successfully converted to SRT format");
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogWarning($"Failed to convert subtitle format to SRT: {ex.Message}");
            // Don't throw - let the file be processed as-is
        }
    }

    /// <summary>
    /// Convert ASS subtitle format to SRT format.
    /// </summary>
    /// <param name="assContent">ASS format content</param>
    /// <returns>SRT format content</returns>
    private static string ConvertAssToSrt(string assContent)
    {
        var srtLines = new List<string>();
        var subtitleNumber = 1;
        
        // Find the [Events] section
        var lines = assContent.Split('\n');
        var inEventsSection = false;
        var formatLine = "";
        
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            
            if (trimmedLine == "[Events]")
            {
                inEventsSection = true;
                continue;
            }
            
            if (inEventsSection && trimmedLine.StartsWith("["))
            {
                // Left the Events section
                break;
            }
            
            if (inEventsSection && trimmedLine.StartsWith("Format:"))
            {
                formatLine = trimmedLine.Substring(7).Trim(); // Remove "Format:"
                continue;
            }
            
            if (inEventsSection && trimmedLine.StartsWith("Dialogue:"))
            {
                // Parse ASS dialogue line
                var dialoguePart = trimmedLine.Substring(9).Trim(); // Remove "Dialogue:"
                var parts = dialoguePart.Split(',');
                
                if (parts.Length >= 10) // ASS dialogue has at least 10 fields
                {
                    // Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
                    var startTime = parts[1].Trim();
                    var endTime = parts[2].Trim();
                    var text = string.Join(",", parts.Skip(9)).Trim(); // Text might contain commas
                    
                    // Remove ASS formatting tags
                    text = System.Text.RegularExpressions.Regex.Replace(text, @"\{[^}]*\}", "");
                    text = text.Replace("\\N", "\n").Replace("\\n", "\n"); // Handle line breaks
                    
                    // Convert ASS time format (0:00:00.00) to SRT format (00:00:00,000)
                    var srtStartTime = ConvertAssTimeToSrtTime(startTime);
                    var srtEndTime = ConvertAssTimeToSrtTime(endTime);
                    
                    // Add to SRT format
                    srtLines.Add(subtitleNumber.ToString());
                    srtLines.Add($"{srtStartTime} --> {srtEndTime}");
                    srtLines.Add(text);
                    srtLines.Add(""); // Empty line between subtitles
                    
                    subtitleNumber++;
                }
            }
        }
        
        return string.Join("\n", srtLines);
    }

    /// <summary>
    /// Convert ASS time format to SRT time format.
    /// ASS: 0:00:00.00 (H:MM:SS.CS where CS is centiseconds)
    /// SRT: 00:00:00,000 (HH:MM:SS,MMM where MMM is milliseconds)
    /// </summary>
    /// <param name="assTime">Time in ASS format</param>
    /// <returns>Time in SRT format</returns>
    private static string ConvertAssTimeToSrtTime(string assTime)
    {
        try
        {
            var parts = assTime.Split(':');
            if (parts.Length != 3) return "00:00:00,000";
            
            var hours = int.Parse(parts[0]);
            var minutes = int.Parse(parts[1]);
            var secondsParts = parts[2].Split('.');
            var seconds = int.Parse(secondsParts[0]);
            var centiseconds = secondsParts.Length > 1 ? int.Parse(secondsParts[1].PadRight(2, '0')) : 0;
            
            // Convert centiseconds to milliseconds
            var milliseconds = centiseconds * 10;
            
            return $"{hours:D2}:{minutes:D2}:{seconds:D2},{milliseconds:D3}";
        }
        catch
        {
            return "00:00:00,000";
        }
    }

    /// <summary>
    /// Convert WebVTT subtitle format to SRT format.
    /// WebVTT format uses dots for milliseconds and may have cue settings/identifiers.
    /// </summary>
    /// <param name="webvttContent">WebVTT format content</param>
    /// <returns>SRT format content</returns>
    private static string ConvertWebVttToSrt(string webvttContent)
    {
        var srtLines = new List<string>();
        var subtitleNumber = 1;
        
        var lines = webvttContent.Split('\n');
        var i = 0;
        
        // Skip WebVTT header and any metadata
        while (i < lines.Length)
        {
            var line = lines[i].Trim();
            
            // Skip WEBVTT header, STYLE, REGION, NOTE blocks
            if (line.StartsWith("WEBVTT") || line.StartsWith("STYLE") || 
                line.StartsWith("REGION") || line.StartsWith("NOTE"))
            {
                i++;
                // Skip until we find an empty line or timing line
                while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i].Trim()) && !lines[i].Contains("-->"))
                {
                    i++;
                }
                continue;
            }
            
            // Found timing line
            if (line.Contains("-->"))
            {
                // Parse timing line
                var timingPart = line.Split(new[] { "-->" }, StringSplitOptions.None);
                if (timingPart.Length >= 2)
                {
                    var startTime = timingPart[0].Trim();
                    // Remove cue settings (everything after the end time)
                    var endTimePart = timingPart[1].Trim();
                    var endTime = endTimePart.Split(new[] { ' ' }, 2)[0]; // Get just the time, ignore settings
                    
                    // Convert WebVTT time to SRT time (dots to commas)
                    var srtStartTime = ConvertWebVttTimeToSrtTime(startTime);
                    var srtEndTime = ConvertWebVttTimeToSrtTime(endTime);
                    
                    // Collect subtitle text
                    i++;
                    var textLines = new List<string>();
                    while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i].Trim()))
                    {
                        textLines.Add(lines[i].TrimEnd('\r'));
                        i++;
                    }
                    
                    // Add to SRT format (only if we have text)
                    if (textLines.Count > 0)
                    {
                        srtLines.Add(subtitleNumber.ToString());
                        srtLines.Add($"{srtStartTime} --> {srtEndTime}");
                        srtLines.AddRange(textLines);
                        srtLines.Add(""); // Empty line between subtitles
                        subtitleNumber++;
                    }
                }
            }
            
            i++;
        }
        
        return string.Join("\n", srtLines);
    }

    /// <summary>
    /// Convert WebVTT time format to SRT time format.
    /// WebVTT: 00:00:00.000 (uses dots for milliseconds)
    /// SRT: 00:00:00,000 (uses commas for milliseconds)
    /// </summary>
    /// <param name="webvttTime">Time in WebVTT format</param>
    /// <returns>Time in SRT format</returns>
    private static string ConvertWebVttTimeToSrtTime(string webvttTime)
    {
        try
        {
            // WebVTT format: HH:MM:SS.mmm or MM:SS.mmm
            // SRT format: HH:MM:SS,mmm
            
            // Simply replace the last dot with a comma
            var lastDotIndex = webvttTime.LastIndexOf('.');
            if (lastDotIndex > 0)
            {
                var result = webvttTime.Substring(0, lastDotIndex) + "," + webvttTime.Substring(lastDotIndex + 1);
                
                // Ensure HH:MM:SS,mmm format (WebVTT may omit hours if 0)
                var parts = result.Split(':');
                if (parts.Length == 2)
                {
                    // Add missing hours
                    result = "00:" + result;
                }
                
                return result;
            }
            
            return "00:00:00,000";
        }
        catch
        {
            return "00:00:00,000";
        }
    }
}
