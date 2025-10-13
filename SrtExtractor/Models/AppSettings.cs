namespace SrtExtractor.Models;

/// <summary>
/// Application settings and configuration.
/// </summary>
/// <param name="MkvMergePath">Path to mkvmerge.exe</param>
/// <param name="MkvExtractPath">Path to mkvextract.exe</param>
/// <param name="TesseractDataPath">Path to Tesseract data directory</param>
/// <param name="AutoDetectTools">Whether to automatically detect tools on startup</param>
/// <param name="LastToolCheck">Timestamp of last tool detection</param>
/// <param name="PreferForced">Whether to prefer forced subtitle tracks</param>
/// <param name="PreferClosedCaptions">Whether to prefer closed caption tracks</param>
/// <param name="DefaultOcrLanguage">Default language for OCR processing</param>
/// <param name="FileNamePattern">Pattern for output file naming</param>
/// <param name="ShowWelcomeScreen">Whether to show the welcome screen on startup</param>
/// <param name="CorrectionLevel">Level of SRT correction to apply (Off, Standard, Thorough)</param>
/// <param name="PreserveSupFiles">Whether to keep extracted SUP files for debugging OCR issues</param>
/// <param name="EnableSrtCorrection">Legacy: Whether to enable SRT correction (deprecated, use CorrectionLevel instead)</param>
/// <param name="EnableMultiPassCorrection">Legacy: Whether to enable multi-pass correction (deprecated, use CorrectionLevel instead)</param>
/// <param name="MaxCorrectionPasses">Legacy: Maximum correction passes (deprecated, use CorrectionLevel instead)</param>
/// <param name="UseSmartConvergence">Legacy: Whether to use smart convergence (deprecated, use CorrectionLevel instead)</param>
/// <param name="CorrectionMode">Legacy: Correction mode string (deprecated, use CorrectionLevel instead)</param>
    public record AppSettings(
        string? MkvMergePath,
        string? MkvExtractPath,
        string? TesseractDataPath,
        bool AutoDetectTools,
        DateTime? LastToolCheck,
        bool PreferForced,
        bool PreferClosedCaptions,
        string DefaultOcrLanguage,
        string FileNamePattern,
        bool ShowWelcomeScreen,
        CorrectionLevel CorrectionLevel,
        bool PreserveSupFiles,
        bool EnableSrtCorrection = true,
        bool EnableMultiPassCorrection = true,
        int MaxCorrectionPasses = 3,
        bool UseSmartConvergence = true,
        string CorrectionMode = "Standard"
    )
{
    /// <summary>
    /// Default settings for new installations.
    /// </summary>
    public static AppSettings Default => new(
        MkvMergePath: null,
        MkvExtractPath: null,
        TesseractDataPath: null,
        AutoDetectTools: true,
        LastToolCheck: null,
        PreferForced: true,
        PreferClosedCaptions: false,
        DefaultOcrLanguage: "eng",
        FileNamePattern: "{basename}.{lang}{forced}.srt",
        ShowWelcomeScreen: true,
        CorrectionLevel: CorrectionLevel.Standard,
        PreserveSupFiles: false
    );
}
