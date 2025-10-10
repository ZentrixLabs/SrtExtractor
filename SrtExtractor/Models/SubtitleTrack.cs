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

        // ====================================
        // User-Friendly Computed Properties
        // (Technical details preserved in original properties)
        // ====================================

        /// <summary>
        /// Human-readable format description instead of technical codec string.
        /// Technical details still available in Codec property and tooltips.
        /// </summary>
        public string FormatDisplay
        {
            get
            {
                var codec = Codec.ToUpperInvariant();
                
                // Image-based formats (require OCR)
                if (codec.Contains("S_HDMV/PGS") || codec.Contains("PGS"))
                    return "Image-based (PGS)";
                if (codec.Contains("VOBSUB") || codec.Contains("S_VOBSUB"))
                    return "Image-based (VobSub)";
                if (codec.Contains("DVBSUB"))
                    return "Image-based (DVB)";
                
                // Text-based formats (fast extraction)
                if (codec.Contains("S_TEXT/UTF8") || codec.Contains("SUBRIP") || codec.Contains("SRT"))
                    return "Text (SRT)";
                if (codec.Contains("S_TEXT/ASS") || codec.Contains("ASS") || codec.Contains("SSA"))
                    return "Text (ASS/SSA)";
                if (codec.Contains("S_TEXT/WEBVTT") || codec.Contains("WEBVTT"))
                    return "Text (WebVTT)";
                if (codec.Contains("S_TEXT"))
                    return "Text-based";
                
                // Fallback for unknown formats
                return Codec.Length > 20 ? Codec.Substring(0, 20) + "..." : Codec;
            }
        }

        /// <summary>
        /// Speed indicator: Fast (text-based) or Slow (image-based requiring OCR).
        /// </summary>
        public string SpeedIndicator
        {
            get
            {
                var codec = Codec.ToUpperInvariant();
                
                // Image-based formats require OCR (slow)
                if (codec.Contains("S_HDMV/PGS") || codec.Contains("PGS") || 
                    codec.Contains("VOBSUB") || codec.Contains("S_VOBSUB") ||
                    codec.Contains("DVBSUB"))
                {
                    return "🐢 OCR Required";
                }
                
                // Text-based formats are fast
                if (codec.Contains("S_TEXT") || codec.Contains("SUBRIP") || 
                    codec.Contains("SRT") || codec.Contains("ASS") || 
                    codec.Contains("WEBVTT"))
                {
                    return "⚡ Fast";
                }
                
                return "❓ Unknown";
            }
        }

        /// <summary>
        /// Simple icon representing the track format type.
        /// </summary>
        public string FormatIcon
        {
            get
            {
                var codec = Codec.ToUpperInvariant();
                
                // Image-based = camera icon
                if (codec.Contains("S_HDMV/PGS") || codec.Contains("PGS") || 
                    codec.Contains("VOBSUB") || codec.Contains("S_VOBSUB") ||
                    codec.Contains("DVBSUB"))
                {
                    return "🖼️";
                }
                
                // Text-based = document icon
                if (codec.Contains("S_TEXT") || codec.Contains("SUBRIP") || 
                    codec.Contains("SRT") || codec.Contains("ASS") || 
                    codec.Contains("WEBVTT"))
                {
                    return "📝";
                }
                
                return "📄";
            }
        }

        /// <summary>
        /// Extended tooltip with all technical details for power users.
        /// </summary>
        public string TechnicalDetails
        {
            get
            {
                return $"Technical Details:\n" +
                       $"Track ID: {TrackId}\n" +
                       $"Extraction ID: {ExtractionId}\n" +
                       $"Codec: {Codec}\n" +
                       $"Language: {Language}\n" +
                       $"Type: {TrackType}\n" +
                       $"Bitrate: {Bitrate:N0} bps\n" +
                       $"Frames: {FrameCount}\n" +
                       $"Duration: {Duration}s\n" +
                       $"Forced: {(Forced ? "Yes" : "No")}\n" +
                       $"CC: {(IsClosedCaption ? "Yes" : "No")}\n" +
                       $"Default: {(IsDefault ? "Yes" : "No")}\n" +
                       $"Name: {(string.IsNullOrEmpty(Name) ? "N/A" : Name)}";
            }
        }
    }
}
