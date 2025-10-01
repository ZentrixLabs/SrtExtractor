namespace SrtExtractor.Models;

/// <summary>
/// Application settings and configuration.
/// </summary>
/// <param name="MkvMergePath">Path to mkvmerge.exe</param>
/// <param name="MkvExtractPath">Path to mkvextract.exe</param>
/// <param name="SubtitleEditPath">Path to SubtitleEdit.exe</param>
/// <param name="AutoDetectTools">Whether to automatically detect tools on startup</param>
/// <param name="LastToolCheck">Timestamp of last tool detection</param>
/// <param name="PreferForced">Whether to prefer forced subtitle tracks</param>
/// <param name="DefaultOcrLanguage">Default language for OCR processing</param>
/// <param name="FileNamePattern">Pattern for output file naming</param>
public record AppSettings(
    string? MkvMergePath,
    string? MkvExtractPath,
    string? SubtitleEditPath,
    bool AutoDetectTools,
    DateTime? LastToolCheck,
    bool PreferForced,
    string DefaultOcrLanguage,
    string FileNamePattern
)
{
    /// <summary>
    /// Default settings for new installations.
    /// </summary>
    public static AppSettings Default => new(
        MkvMergePath: null,
        MkvExtractPath: null,
        SubtitleEditPath: null,
        AutoDetectTools: true,
        LastToolCheck: null,
        PreferForced: true,
        DefaultOcrLanguage: "eng",
        FileNamePattern: "{basename}.{lang}{forced}.srt"
    );
}
