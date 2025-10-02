using System.IO;
using System.Text.RegularExpressions;
using SrtExtractor.Services.Interfaces;

namespace SrtExtractor.Services.Implementations;

/// <summary>
/// Service for correcting common OCR errors in SRT files.
/// </summary>
public class SrtCorrectionService : ISrtCorrectionService
{
    private readonly ILoggingService _loggingService;

    public SrtCorrectionService(ILoggingService loggingService)
    {
        _loggingService = loggingService;
    }

    public async Task<int> CorrectSrtFileAsync(string srtPath, CancellationToken cancellationToken = default)
    {
        _loggingService.LogInfo($"Correcting OCR errors in SRT file: {srtPath}");

        if (!File.Exists(srtPath))
        {
            throw new FileNotFoundException($"SRT file not found: {srtPath}");
        }

        try
        {
            var content = await File.ReadAllTextAsync(srtPath, cancellationToken);
            var (correctedContent, correctionCount) = CorrectSrtContentWithCount(content);
            
            if (content != correctedContent)
            {
                await File.WriteAllTextAsync(srtPath, correctedContent, cancellationToken);
                _loggingService.LogInfo("SRT file corrected successfully");
            }
            else
            {
                _loggingService.LogInfo("No corrections needed in SRT file");
            }

            return correctionCount;
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to correct SRT file", ex);
            throw;
        }
    }

    public (string correctedContent, int correctionCount) CorrectSrtContentWithCount(string content)
    {
        var corrected = content;
        var totalCorrections = 0;

        // Common OCR error patterns
        var corrections = new Dictionary<string, string>
        {
            // Missing spaces before common words
            { @"(\w)(is|are|was|were|have|has|had|will|would|could|should)(\s)", "$1 $2$3" },
            { @"(\w)(the|and|or|but|for|with|from|to|of|in|on|at|by)(\s)", "$1 $2$3" },
            { @"(\w)(I|you|he|she|it|we|they)(\s)", "$1 $2$3" },
            
            // Common character substitutions
            { @"\bl've\b", "I've" },
            { @"\bl'm\b", "I'm" },
            { @"\bl'll\b", "I'll" },
            { @"\bl'd\b", "I'd" },
            { @"\bl\s", "I " },
            { @"\bl$", "I" },
            
            // Missing apostrophes
            { @"\byoure\b", "you're" },
            { @"\bwere\b", "we're" },
            { @"\btheyre\b", "they're" },
            { @"\bhes\b", "he's" },
            { @"\bshes\b", "she's" },
            { @"\bits\b", "it's" },
            { @"\bwont\b", "won't" },
            { @"\bcant\b", "can't" },
            { @"\bdont\b", "don't" },
            { @"\bdoesnt\b", "doesn't" },
            { @"\bdidnt\b", "didn't" },
            { @"\bhavent\b", "haven't" },
            { @"\bhasnt\b", "hasn't" },
            { @"\bhadnt\b", "hadn't" },
            
            // Double letters that shouldn't be
            { @"\bRattIing\b", "Rattling" },
            { @"\bChatterIing\b", "Chattering" },
            { @"\bClackIing\b", "Clacking" },
            
            // Common OCR mistakes
            { @"\bterriblego\b", "terrible go" },
            { @"\bWhatyou\b", "What you" },
            { @"\bYougotany\b", "You got any" },
            { @"\bIkeep\b", "I keep" },
            { @"\bYellow lights\b", "Yellow light's" },
            { @"\bget dressed\b", "get dressed" },
            
            // Extra spaces (OCR often adds spaces in the middle of words)
            { @"\bT he\b", "The" },
            { @"\bsh it\b", "shit" },
            { @"\bwh at\b", "what" },
            { @"\bf or\b", "for" },
            { @"\bth e\b", "the" },
            { @"\ban d\b", "and" },
            { @"\bwi th\b", "with" },
            { @"\bto o\b", "too" },
            { @"\bso me\b", "some" },
            { @"\bmo re\b", "more" },
            { @"\bth is\b", "this" },
            { @"\bth at\b", "that" },
            { @"\bth ey\b", "they" },
            { @"\bth eir\b", "their" },
            { @"\bth ere\b", "there" },
            { @"\bth en\b", "then" },
            { @"\bth an\b", "than" },
            { @"\bth rough\b", "through" },
            { @"\bth ink\b", "think" },
            { @"\bth ing\b", "thing" },
            { @"\bth ose\b", "those" },
            { @"\bth ree\b", "three" },
            { @"\bth row\b", "throw" },
            { @"\bth us\b", "thus" },
            
            // Missing spaces in common phrases
            { @"\bYougotanybiscuits\b", "You got any biscuits" },
            { @"\bWhatyoudoing\b", "What you doing" },
            { @"\bHowyoudoing\b", "How you doing" },
            { @"\bWhereyougoing\b", "Where you going" },
            { @"\bWhenyoucoming\b", "When you coming" },
            { @"\bWhyyouhere\b", "Why you here" },
            { @"\bWhoareyou\b", "Who are you" },
            { @"\bWhatareyou\b", "What are you" },
            { @"\bHowareyou\b", "How are you" },
            { @"\bWhereareyou\b", "Where are you" },
            { @"\bWhenareyou\b", "When are you" },
            { @"\bWhyareyou\b", "Why are you" },
            
            // Common contractions that OCR misses
            { @"\bterrible go\b", "terrible" },
            { @"\bI am\b", "I'm" },
            { @"\byou are\b", "you're" },
            { @"\bwe are\b", "we're" },
            { @"\bthey are\b", "they're" },
            { @"\bit is\b", "it's" },
            { @"\bhe is\b", "he's" },
            { @"\bshe is\b", "she's" },
            { @"\bI will\b", "I'll" },
            { @"\byou will\b", "you'll" },
            { @"\bwe will\b", "we'll" },
            { @"\bthey will\b", "they'll" },
            { @"\bit will\b", "it'll" },
            { @"\bhe will\b", "he'll" },
            { @"\bshe will\b", "she'll" },
            { @"\bI have\b", "I've" },
            { @"\byou have\b", "you've" },
            { @"\bwe have\b", "we've" },
            { @"\bthey have\b", "they've" },
            { @"\bI would\b", "I'd" },
            { @"\byou would\b", "you'd" },
            { @"\bwe would\b", "we'd" },
            { @"\bthey would\b", "they'd" },
            { @"\bhe would\b", "he'd" },
            { @"\bshe would\b", "she'd" },
            { @"\bit would\b", "it'd" },
            
            // Common OCR character substitutions
            { @"\b0\b", "O" }, // Zero to O in words
            { @"\b1\b", "I" }, // One to I in words
            { @"\b5\b", "S" }, // Five to S in words
            { @"\b8\b", "B" }, // Eight to B in words
            
            // Apostrophe issues (OCR often uses wrong characters)
            { @"\)re\b", "'re" }, // )re to 're
            { @"\)ll\b", "'ll" }, // )ll to 'll
            { @"\)ve\b", "'ve" }, // )ve to 've
            { @"\)d\b", "'d" }, // )d to 'd
            { @"\)m\b", "'m" }, // )m to 'm
            { @"\)s\b", "'s" }, // )s to 's
            { @"\)t\b", "'t" }, // )t to 't
            
            // More character substitutions
            { @"\bRipIey\b", "Ripley" },
            { @"\bfeeIdead\b", "feel dead" },
            { @"\bfeeI\b", "feel" },
            { @"\bIeeI\b", "feel" },
            { @"\bIee\b", "feel" },
            
            // Common OCR mistakes with specific words
            { @"\bI feeIdead\b", "I feel dead" },
            { @"\bI feeI\b", "I feel" },
            
            // More apostrophe patterns
            { @"\byou\)re\b", "you're" },
            { @"\bwe\)re\b", "we're" },
            { @"\bthey\)re\b", "they're" },
            { @"\bit\)s\b", "it's" },
            { @"\bhe\)s\b", "he's" },
            { @"\bshe\)s\b", "she's" },
            { @"\bI\)m\b", "I'm" },
            { @"\bI\)ll\b", "I'll" },
            { @"\bI\)ve\b", "I've" },
            { @"\bI\)d\b", "I'd" },
            { @"\bwon\)t\b", "won't" },
            { @"\bcan\)t\b", "can't" },
            { @"\bdon\)t\b", "don't" },
            { @"\bdoesn\)t\b", "doesn't" },
            { @"\bdidn\)t\b", "didn't" },
            { @"\bhaven\)t\b", "haven't" },
            { @"\bhasn\)t\b", "hasn't" },
            { @"\bhadn\)t\b", "hadn't" },
        };

        foreach (var correction in corrections)
        {
            var beforeCount = Regex.Matches(corrected, correction.Key).Count;
            corrected = Regex.Replace(corrected, correction.Key, correction.Value, RegexOptions.IgnoreCase);
            var afterCount = Regex.Matches(corrected, correction.Key).Count;
            
            if (beforeCount > afterCount)
            {
                var instances = beforeCount - afterCount;
                totalCorrections += instances;
                _loggingService.LogInfo($"Applied correction: '{correction.Key}' → '{correction.Value}' ({instances} instances)");
            }
        }

        // Log total corrections summary
        _loggingService.LogInfo($"🎯 Total OCR corrections applied: {totalCorrections}");

        return (corrected, totalCorrections);
    }

    public string CorrectSrtContent(string content)
    {
        var (corrected, _) = CorrectSrtContentWithCount(content);
        return corrected;
    }
}
