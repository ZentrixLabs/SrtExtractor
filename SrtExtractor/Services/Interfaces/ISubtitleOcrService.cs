namespace SrtExtractor.Services.Interfaces;

/// <summary>
/// Service for Subtitle Edit OCR operations.
/// </summary>
public interface ISubtitleOcrService
{
    /// <summary>
    /// Perform OCR on a SUP file to convert it to SRT format.
    /// </summary>
    /// <param name="supPath">Path to the input SUP file</param>
    /// <param name="outSrt">Path to the output SRT file</param>
    /// <param name="language">Language code for OCR (e.g., eng, spa, fra)</param>
    /// <param name="fixCommonErrors">Whether to apply common error fixes</param>
    /// <param name="removeHi">Whether to remove hearing impaired text</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task OcrSupToSrtAsync(
        string supPath, 
        string outSrt, 
        string language, 
        bool fixCommonErrors = true, 
        bool removeHi = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Perform OCR on a SUP file to convert it to SRT format with progress reporting.
    /// </summary>
    /// <param name="supPath">Path to the input SUP file</param>
    /// <param name="outSrt">Path to the output SRT file</param>
    /// <param name="language">Language code for OCR (e.g., eng, spa, fra)</param>
    /// <param name="fixCommonErrors">Whether to apply common error fixes</param>
    /// <param name="removeHi">Whether to remove hearing impaired text</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <param name="progress">Progress reporter for frame processing updates</param>
    Task OcrSupToSrtAsync(
        string supPath, 
        string outSrt, 
        string language, 
        bool fixCommonErrors = true, 
        bool removeHi = true,
        CancellationToken cancellationToken = default,
        IProgress<(int processed, int total, string phase)>? progress = null);
    
    /// <summary>
    /// Convert ASS subtitle format to SRT format if the file contains ASS content.
    /// This method checks if a file is in ASS format and converts it to SRT if needed.
    /// </summary>
    /// <param name="filePath">Path to the subtitle file to check and convert</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ConvertAssToSrtIfNeededAsync(string filePath, CancellationToken cancellationToken = default);
}
