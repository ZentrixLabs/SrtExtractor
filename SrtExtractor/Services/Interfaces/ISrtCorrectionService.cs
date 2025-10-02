using SrtExtractor.Services.Interfaces;

namespace SrtExtractor.Services.Interfaces;

/// <summary>
/// Service for correcting common OCR errors in SRT files.
/// </summary>
public interface ISrtCorrectionService
{
    /// <summary>
    /// Corrects common OCR errors in an SRT file.
    /// </summary>
    /// <param name="srtPath">Path to the SRT file to correct</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the correction operation, returning the number of corrections applied</returns>
    Task<int> CorrectSrtFileAsync(string srtPath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Corrects common OCR errors in SRT content text.
    /// </summary>
    /// <param name="content">SRT content to correct</param>
    /// <returns>Corrected SRT content</returns>
    string CorrectSrtContent(string content);
}
