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
/// <param name="EnableSrtCorrection">Whether to enable SRT correction (true for corrections, false for raw OCR)</param>
/// <param name="PreserveSupFiles">Whether to keep extracted SUP files for debugging OCR issues</param>
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
        bool EnableSrtCorrection,
        bool PreserveSupFiles
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
            EnableSrtCorrection: true,
            PreserveSupFiles: false
    );
}
