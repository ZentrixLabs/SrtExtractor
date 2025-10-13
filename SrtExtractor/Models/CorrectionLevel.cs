namespace SrtExtractor.Models;

/// <summary>
/// Defines the level of SRT correction to apply during OCR processing.
/// </summary>
public enum CorrectionLevel
{
    /// <summary>
    /// No correction applied - raw OCR output with potential errors.
    /// </summary>
    Off,
    
    /// <summary>
    /// Standard correction - single pass with common OCR error fixes.
    /// Recommended for most users.
    /// </summary>
    Standard,
    
    /// <summary>
    /// Thorough correction - multiple passes with smart convergence.
    /// Best quality but slower processing.
    /// </summary>
    Thorough
}

/// <summary>
/// Extension methods for CorrectionLevel enum.
/// </summary>
public static class CorrectionLevelExtensions
{
    /// <summary>
    /// Gets the display name for the correction level.
    /// </summary>
    /// <param name="level">The correction level</param>
    /// <returns>User-friendly display name</returns>
    public static string GetDisplayName(this CorrectionLevel level)
    {
        return level switch
        {
            CorrectionLevel.Off => "Off (Raw OCR)",
            CorrectionLevel.Standard => "Standard (Recommended)",
            CorrectionLevel.Thorough => "Thorough (Best Quality)",
            _ => level.ToString()
        };
    }
    
    /// <summary>
    /// Gets the description for the correction level.
    /// </summary>
    /// <param name="level">The correction level</param>
    /// <returns>Detailed description</returns>
    public static string GetDescription(this CorrectionLevel level)
    {
        return level switch
        {
            CorrectionLevel.Off => "No correction applied. You'll get raw OCR output with potential errors.",
            CorrectionLevel.Standard => "Single pass correction with ~841 common OCR error patterns. Fast and effective.",
            CorrectionLevel.Thorough => "Multiple correction passes with smart convergence. Best quality but slower.",
            _ => "Unknown correction level"
        };
    }
    
    /// <summary>
    /// Converts the correction level to legacy boolean settings.
    /// </summary>
    /// <param name="level">The correction level</param>
    /// <returns>Tuple of (EnableSrtCorrection, EnableMultiPassCorrection)</returns>
    public static (bool EnableSrtCorrection, bool EnableMultiPassCorrection) ToLegacySettings(this CorrectionLevel level)
    {
        return level switch
        {
            CorrectionLevel.Off => (false, false),
            CorrectionLevel.Standard => (true, false),
            CorrectionLevel.Thorough => (true, true),
            _ => (false, false)
        };
    }
    
    /// <summary>
    /// Converts legacy boolean settings to correction level.
    /// </summary>
    /// <param name="enableSrtCorrection">Whether SRT correction is enabled</param>
    /// <param name="enableMultiPassCorrection">Whether multi-pass correction is enabled</param>
    /// <returns>The corresponding correction level</returns>
    public static CorrectionLevel FromLegacySettings(bool enableSrtCorrection, bool enableMultiPassCorrection)
    {
        if (!enableSrtCorrection)
            return CorrectionLevel.Off;
        
        if (enableMultiPassCorrection)
            return CorrectionLevel.Thorough;
            
        return CorrectionLevel.Standard;
    }
}
