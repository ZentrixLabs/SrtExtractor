using CommunityToolkit.Mvvm.ComponentModel;

namespace SrtExtractor.Models;

/// <summary>
/// Represents VobSub track information for analysis across multiple MKV files.
/// </summary>
public partial class VobSubTrackInfo : ObservableObject
{
    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private int _trackNumber;

    [ObservableProperty]
    private string _codec = string.Empty;

    [ObservableProperty]
    private string _language = string.Empty;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private bool _isForced;

    [ObservableProperty]
    private bool _isDefault;

    [ObservableProperty]
    private bool _isCommentary;

    [ObservableProperty]
    private bool _isSdh;

    [ObservableProperty]
    private int _frameCount;

    [ObservableProperty]
    private string _status = "Ready";

    [ObservableProperty]
    private string _recommendationReason = string.Empty;

    public VobSubTrackInfo()
    {
    }

    public VobSubTrackInfo(
        string fileName,
        string filePath,
        int trackNumber,
        string codec,
        string language,
        string title,
        bool isForced,
        bool isDefault,
        int frameCount = 0)
    {
        FileName = fileName;
        FilePath = filePath;
        TrackNumber = trackNumber;
        Codec = codec;
        Language = language;
        Title = title;
        IsForced = isForced;
        IsDefault = isDefault;
        FrameCount = frameCount;

        // Detect special track types from title
        var titleLower = title.ToLowerInvariant();
        IsCommentary = titleLower.Contains("commentary") || titleLower.Contains("comment");
        IsSdh = titleLower.Contains("sdh") || titleLower.Contains("deaf") || titleLower.Contains("hard of hearing");
    }

    /// <summary>
    /// Gets a formatted display string for the track.
    /// </summary>
    public string DisplayName
    {
        get
        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(Language))
                parts.Add(Language);

            if (IsForced)
                parts.Add("Forced");

            if (IsSdh)
                parts.Add("SDH");

            if (IsCommentary)
                parts.Add("Commentary");

            if (!string.IsNullOrEmpty(Title))
                parts.Add($"\"{Title}\"");

            var display = parts.Count > 0 ? string.Join(" | ", parts) : "Unknown";
            return $"Track #{TrackNumber}: {display}";
        }
    }

    /// <summary>
    /// Gets a short track type description for UI display.
    /// </summary>
    public string TrackType
    {
        get
        {
            if (IsForced) return "Forced";
            if (IsSdh) return "SDH";
            if (IsCommentary) return "Commentary";
            return "Full";
        }
    }

    public override string ToString() => DisplayName;
}
