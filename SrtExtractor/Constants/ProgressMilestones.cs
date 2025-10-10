namespace SrtExtractor.Constants;

/// <summary>
/// Progress milestone constants for extraction operations.
/// Defines the percentage completion at each stage of processing.
/// </summary>
public static class ProgressMilestones
{
    // Text extraction milestones
    public const double TextExtractionStart = 0.50;
    public const double TextExtractionComplete = 0.80;
    
    // PGS extraction milestones
    public const double PgsExtractionStart = 0.30;
    public const double OcrStart = 0.50;
    public const double OcrComplete = 0.90;
    
    // Common milestones
    public const double Complete = 1.0;
    
    /// <summary>
    /// Calculate actual bytes from percentage milestone.
    /// </summary>
    /// <param name="totalBytes">Total bytes to process</param>
    /// <param name="milestone">Progress milestone (0.0 to 1.0)</param>
    /// <returns>Bytes processed at this milestone</returns>
    public static long CalculateBytes(long totalBytes, double milestone)
    {
        return (long)(totalBytes * milestone);
    }
}

