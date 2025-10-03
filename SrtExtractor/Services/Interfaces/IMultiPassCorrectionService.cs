using System.Threading.Tasks;

namespace SrtExtractor.Services.Interfaces;

/// <summary>
/// Service interface for performing multi-pass SRT correction.
/// Handles multiple passes of correction until convergence or max passes reached.
/// </summary>
public interface IMultiPassCorrectionService
{
    /// <summary>
    /// Process SRT content with multiple correction passes.
    /// </summary>
    /// <param name="srtContent">The original SRT content</param>
    /// <param name="maxPasses">Maximum number of correction passes</param>
    /// <param name="useSmartConvergence">Whether to stop when no changes are detected</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the corrected content and statistics</returns>
    Task<MultiPassCorrectionResult> ProcessWithMultiPassAsync(
        string srtContent, 
        int maxPasses, 
        bool useSmartConvergence = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Process SRT content using predefined correction mode.
    /// </summary>
    /// <param name="srtContent">The original SRT content</param>
    /// <param name="correctionMode">Correction mode (Quick, Standard, Thorough)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the corrected content and statistics</returns>
    Task<MultiPassCorrectionResult> ProcessWithModeAsync(
        string srtContent, 
        string correctionMode,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of multi-pass correction processing.
/// </summary>
public class MultiPassCorrectionResult
{
    /// <summary>
    /// The corrected SRT content after all passes.
    /// </summary>
    public string CorrectedContent { get; set; } = string.Empty;

    /// <summary>
    /// Number of passes that were actually performed.
    /// </summary>
    public int PassesCompleted { get; set; }

    /// <summary>
    /// Total number of corrections made across all passes.
    /// </summary>
    public int TotalCorrections { get; set; }

    /// <summary>
    /// Whether the process converged (no more changes detected).
    /// </summary>
    public bool Converged { get; set; }

    /// <summary>
    /// Processing time in milliseconds.
    /// </summary>
    public long ProcessingTimeMs { get; set; }

    /// <summary>
    /// Detailed statistics for each pass.
    /// </summary>
    public List<PassStatistics> PassStatistics { get; set; } = new();

    /// <summary>
    /// Any warnings or issues encountered during processing.
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Statistics for a single correction pass.
/// </summary>
public class PassStatistics
{
    /// <summary>
    /// Pass number (1-based).
    /// </summary>
    public int PassNumber { get; set; }

    /// <summary>
    /// Number of corrections made in this pass.
    /// </summary>
    public int CorrectionsMade { get; set; }

    /// <summary>
    /// Processing time for this pass in milliseconds.
    /// </summary>
    public long ProcessingTimeMs { get; set; }

    /// <summary>
    /// Types of corrections made in this pass.
    /// </summary>
    public Dictionary<string, int> CorrectionTypes { get; set; } = new();
}
