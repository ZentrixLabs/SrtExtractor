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
        private Models.TrackType _type = Models.TrackType.Full;

        [ObservableProperty]
        private int _frameCount;
        
        // Keep legacy string property for backwards compatibility with existing code
        public string TrackType => Type switch
        {
            Models.TrackType.Full => "Full",
            Models.TrackType.Forced => "Forced",
            Models.TrackType.ClosedCaption => "CC",
            Models.TrackType.ClosedCaptionForced => "CC Forced",
            _ => "Full"
        };
        
        // Cached codec type - computed once on first access
        private SubtitleCodecType? _codecType;

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
            
            // Convert legacy string trackType to enum
            Type = trackType switch
            {
                "Forced" => Models.TrackType.Forced,
                "CC" => Models.TrackType.ClosedCaption,
                "CC Forced" => Models.TrackType.ClosedCaptionForced,
                _ => Models.TrackType.Full
            };
            
            FrameCount = frameCount;
        }

        public override string ToString()
        {
            return $"{TrackId}: {Name} ({Language}) - {Codec}";
        }

        // ====================================
        // Codec Type Detection (Cached)
        // Single source of truth for codec classification
        // ====================================
        
        /// <summary>
        /// Gets the codec type, performing the detection only once and caching the result.
        /// This eliminates repeated string operations across multiple property accesses.
        /// </summary>
        public SubtitleCodecType CodecType
        {
            get
            {
                if (_codecType.HasValue)
                    return _codecType.Value;
                
                var codec = Codec.ToUpperInvariant();
                
                // Detect codec type based on codec string
                if (codec.Contains("S_HDMV/PGS") || codec.Contains("PGS"))
                    _codecType = SubtitleCodecType.ImageBasedPgs;
                else if (codec.Contains("VOBSUB") || codec.Contains("S_VOBSUB"))
                    _codecType = SubtitleCodecType.ImageBasedVobSub;
                else if (codec.Contains("DVBSUB"))
                    _codecType = SubtitleCodecType.ImageBasedDvb;
                else if (codec.Contains("S_TEXT/UTF8") || codec.Contains("SUBRIP") || codec.Contains("SRT"))
                    _codecType = SubtitleCodecType.TextBasedSrt;
                else if (codec.Contains("S_TEXT/ASS") || codec.Contains("ASS") || codec.Contains("SSA"))
                    _codecType = SubtitleCodecType.TextBasedAss;
                else if (codec.Contains("S_TEXT/WEBVTT") || codec.Contains("WEBVTT"))
                    _codecType = SubtitleCodecType.TextBasedWebVtt;
                else if (codec.Contains("S_TEXT"))
                    _codecType = SubtitleCodecType.TextBasedGeneric;
                else
                    _codecType = SubtitleCodecType.Unknown;
                
                return _codecType.Value;
            }
        }
        
        /// <summary>
        /// Gets whether this codec requires OCR processing (image-based).
        /// </summary>
        public bool RequiresOcr => CodecType is SubtitleCodecType.ImageBasedPgs or SubtitleCodecType.ImageBasedVobSub or SubtitleCodecType.ImageBasedDvb;
        
        /// <summary>
        /// Gets whether this is a text-based codec (fast extraction).
        /// </summary>
        public bool IsTextBased => CodecType is SubtitleCodecType.TextBasedSrt or SubtitleCodecType.TextBasedAss or SubtitleCodecType.TextBasedWebVtt or SubtitleCodecType.TextBasedGeneric;
        
        /// <summary>
        /// Gets whether this is a SubRip/SRT codec (highest priority).
        /// </summary>
        public bool IsSubRip => CodecType == SubtitleCodecType.TextBasedSrt;
        
        /// <summary>
        /// Gets the priority of this codec for automatic selection (higher is better).
        /// Used by track selection logic to prefer faster/better formats.
        /// </summary>
        public int CodecPriority => CodecType switch
        {
            SubtitleCodecType.TextBasedSrt => 100,
            SubtitleCodecType.TextBasedAss or SubtitleCodecType.TextBasedWebVtt or SubtitleCodecType.TextBasedGeneric => 50,
            SubtitleCodecType.ImageBasedPgs or SubtitleCodecType.ImageBasedVobSub or SubtitleCodecType.ImageBasedDvb => 10,
            _ => 0
        };

        // ====================================
        // User-Friendly Computed Properties
        // Now using cached CodecType for better performance
        // ====================================

        /// <summary>
        /// Human-readable format description instead of technical codec string.
        /// Technical details still available in Codec property and tooltips.
        /// </summary>
        public string FormatDisplay => CodecType switch
        {
            SubtitleCodecType.ImageBasedPgs => "Image-based (PGS)",
            SubtitleCodecType.ImageBasedVobSub => "Image-based (VobSub)",
            SubtitleCodecType.ImageBasedDvb => "Image-based (DVB)",
            SubtitleCodecType.TextBasedSrt => "Text (SRT)",
            SubtitleCodecType.TextBasedAss => "Text (ASS/SSA)",
            SubtitleCodecType.TextBasedWebVtt => "Text (WebVTT)",
            SubtitleCodecType.TextBasedGeneric => "Text-based",
            _ => Codec.Length > 20 ? Codec.Substring(0, 20) + "..." : Codec
        };

        /// <summary>
        /// Speed indicator: Fast (text-based) or Slow (image-based requiring OCR).
        /// </summary>
        public string SpeedIndicator => RequiresOcr ? "🐢 OCR Required" : (IsTextBased ? "⚡ Fast" : "❓ Unknown");

        /// <summary>
        /// Simple icon representing the track format type.
        /// </summary>
        public string FormatIcon => RequiresOcr ? "🖼️" : (IsTextBased ? "📝" : "📄");

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
