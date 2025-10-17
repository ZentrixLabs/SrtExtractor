using System;
using System.Collections.Generic;

namespace SrtExtractor.Utils;

/// <summary>
/// Maps language tags to ISO 639-1 two-letter codes for filename compatibility (e.g., Plex).
/// Does not affect OCR language settings which may require three-letter codes (e.g., Tesseract).
/// </summary>
public static class LanguageCodeUtilities
{
    private static readonly Dictionary<string, string> ThreeToTwoLetter = new(StringComparer.OrdinalIgnoreCase)
    {
        // Common languages
        ["eng"] = "en",
        ["spa"] = "es",
        ["fra"] = "fr", ["fre"] = "fr",
        ["deu"] = "de", ["ger"] = "de",
        ["ita"] = "it",
        ["por"] = "pt",
        ["rus"] = "ru",
        ["jpn"] = "ja",
        ["kor"] = "ko",
        ["zho"] = "zh", ["chi"] = "zh",
        ["ara"] = "ar",
        ["nld"] = "nl", ["dut"] = "nl",
        ["swe"] = "sv",
        ["nor"] = "no",
        ["dan"] = "da",
        ["fin"] = "fi",
        ["pol"] = "pl",
        ["tur"] = "tr",
        ["heb"] = "he", ["iw"] = "he",
        ["ell"] = "el", ["gre"] = "el",
        ["ces"] = "cs", ["cze"] = "cs",
        ["slk"] = "sk", ["slo"] = "sk",
        ["ron"] = "ro", ["rum"] = "ro",
        ["hun"] = "hu",
        ["tha"] = "th",
        ["vie"] = "vi",
        ["ind"] = "id",
        ["msa"] = "ms", ["zsm"] = "ms",
        ["hin"] = "hi",
        ["ben"] = "bn",
        ["tam"] = "ta",
        ["tel"] = "te",
        ["urd"] = "ur",
        ["fas"] = "fa", ["per"] = "fa",
        ["ukr"] = "uk",
        ["bul"] = "bg",
        ["srp"] = "sr",
        ["hrv"] = "hr",
        ["slv"] = "sl",
        ["est"] = "et",
        ["lav"] = "lv",
        ["lit"] = "lt",
        ["isl"] = "is",
        ["gle"] = "ga",
        ["afr"] = "af",
        ["sqi"] = "sq",
        ["mkd"] = "mk",
        ["cat"] = "ca",
        ["eus"] = "eu",
        ["glg"] = "gl",
    };

    /// <summary>
    /// Convert a language tag to ISO 639-1 two-letter code if possible.
    /// If already two letters, returns lowercased input. Otherwise returns a sensible fallback.
    /// </summary>
    public static string ToIso6391(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return "und"; // Undefined

        var trimmed = language.Trim();

        // If already two-letter (e.g., "en"), normalize to lowercase and return
        if (trimmed.Length == 2)
            return trimmed.ToLowerInvariant();

        if (ThreeToTwoLetter.TryGetValue(trimmed, out var two))
            return two;

        // Some tags come as "en-US" etc. Take the primary subtag if two letters
        var dashIndex = trimmed.IndexOf('-');
        if (dashIndex > 0)
        {
            var primary = trimmed.Substring(0, dashIndex);
            if (primary.Length == 2)
                return primary.ToLowerInvariant();
            if (ThreeToTwoLetter.TryGetValue(primary, out var mapped))
                return mapped;
        }

        // Fallback: return lowercase of input if short, else "und"
        return trimmed.Length <= 3 ? trimmed.ToLowerInvariant() : "und";
    }
}


