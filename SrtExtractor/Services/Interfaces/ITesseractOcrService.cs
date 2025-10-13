namespace SrtExtractor.Services.Interfaces;

/// <summary>
/// Service for performing OCR using Tesseract directly on PGS subtitle images.
/// This provides much better quality than SubtitleEdit CLI's nOCR engine.
/// </summary>
public interface ITesseractOcrService
{
    /// <summary>
    /// Perform OCR on a PGS/BluRay SUP file using Tesseract engine.
    /// </summary>
    /// <param name="supPath">Path to the .sup file containing PGS subtitles</param>
    /// <param name="outSrt">Path where the output SRT file should be saved</param>
    /// <param name="language">Tesseract language code (e.g., "eng", "fra", "spa")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task OcrSupToSrtAsync(
        string supPath,
        string outSrt,
        string language,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if Tesseract is properly configured and available.
    /// </summary>
    /// <returns>True if Tesseract is available, false otherwise</returns>
    Task<bool> IsTesseractAvailableAsync();
}

