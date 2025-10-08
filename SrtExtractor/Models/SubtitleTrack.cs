using CommunityToolkit.Mvvm.ComponentModel;

namespace SrtExtractor.Models
{
    public partial class SubtitleTrack : ObservableObject
    {
        [ObservableProperty]
        private int _trackId;

        // Alias for TrackId to match existing code
        public int Id => TrackId;

        /// <summary>
        /// The mkvmerge/mkvextract ID used for extraction commands.
        /// This is different from TrackId (the actual Matroska track number displayed to users).
        /// </summary>
        [ObservableProperty]
        private int _extractionId;

        [ObservableProperty]
        private string _codec = string.Empty;

        [ObservableProperty]
        private string _language = string.Empty;

        [ObservableProperty]
        private bool _isDefault;

        [ObservableProperty]
        private bool _isForced;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private long _bitrate;

        [ObservableProperty]
        private int _duration;

        [ObservableProperty]
        private int _width;

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private bool _forced;

        [ObservableProperty]
        private bool _isClosedCaption;

        [ObservableProperty]
        private bool _isRecommended;

        [ObservableProperty]
        private string _trackType = string.Empty;

        [ObservableProperty]
        private int _frameCount;

        public SubtitleTrack()
        {
        }

        public SubtitleTrack(int trackId, string codec, string language, bool isDefault, bool isForced, 
                           string name, long bitrate, int duration, int width, string title, bool isSelected,
                           bool forced = false, bool isClosedCaption = false, bool isRecommended = false,
                           string trackType = "", int frameCount = 0, int extractionId = 0)
        {
            TrackId = trackId;
            ExtractionId = extractionId > 0 ? extractionId : trackId; // Default to trackId for backward compatibility
            Codec = codec;
            Language = language;
            IsDefault = isDefault;
            IsForced = isForced;
            Name = name;
            Bitrate = bitrate;
            Duration = duration;
            Width = width;
            Title = title;
            IsSelected = isSelected;
            Forced = forced;
            IsClosedCaption = isClosedCaption;
            IsRecommended = isRecommended;
            TrackType = trackType;
            FrameCount = frameCount;
        }

        public override string ToString()
        {
            return $"{TrackId}: {Name} ({Language}) - {Codec}";
        }
    }
}
