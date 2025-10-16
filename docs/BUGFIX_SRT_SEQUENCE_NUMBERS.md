# Critical Bug Fix: OCR Correction Corrupting SRT Sequence Numbers

## Issue
When extracting PGS (Blu-ray) subtitles using OCR, the resulting SRT files had **alphabetic characters instead of numbers** in the sequence numbering:
- `I` instead of `1`
- `S` instead of `5`
- `B` instead of `8`

### Example - Broken Output
```
I
00:03:36,883 --> 00:03:38,093
RICCO:
Colonel Sandurz?

2
00:03:38,176 --> 00:03:40,303
What is i,
Sergeant Ricco?

S
00:03:47,394 --> 00:03:49,563
Planet Druidia
Is in sight, sir.
```

**Should be:**
```
1
00:03:36,883 --> 00:03:38,093
RICCO:
Colonel Sandurz?

2
00:03:38,176 --> 00:03:40,303
What is i,
Sergeant Ricco?

5
00:03:47,394 --> 00:03:49,563
Planet Druidia
Is in sight, sir.
```

## Impact
- **Plex and other media players couldn't recognize the SRT files** at all
- Files were technically invalid SRT format (sequence numbers must be numeric)
- Affected ALL PGS subtitle extractions using OCR
- Only occurred after v2.0.4 when OCR correction was introduced

## Root Cause
The `SrtCorrectionService` was applying OCR corrections to the **ENTIRE SRT file content**, including:
- ✅ Subtitle text (correct - should be corrected)
- ❌ Sequence numbers (incorrect - should NOT be corrected)
- ❌ Timestamps (incorrect - should NOT be corrected)

The `ZentrixLabs.OcrCorrection` library has patterns to fix common OCR mistakes like:
- `1` → `I` (when OCR misreads the number 1 as letter I)
- `5` → `S` (when OCR misreads the number 5 as letter S)
- `8` → `B` (when OCR misreads the number 8 as letter B)

These corrections are **valid for subtitle text content**, but when applied to sequence numbers, they **corrupted the SRT format**.

### Code Location
**File:** `SrtExtractor/Services/Implementations/SrtCorrectionService.cs`

**Before (Buggy Code):**
```csharp
public (string correctedContent, int correctionCount) CorrectSrtContentWithCount(string content)
{
    // ...
    var result = _ocrCorrectionEngine.Correct(content, options); // ❌ Corrects EVERYTHING
    // ...
}
```

## Solution
Modified `SrtCorrectionService.CorrectSrtContentWithCount()` to:
1. **Parse the SRT file line by line**
2. **Identify** sequence numbers, timestamps, and empty lines
3. **Only apply OCR corrections to subtitle text lines**
4. **Preserve** sequence numbers and timestamps unchanged

### After (Fixed Code)
```csharp
public (string correctedContent, int correctionCount) CorrectSrtContentWithCount(string content)
{
    // CRITICAL FIX: Apply OCR corrections ONLY to subtitle text
    var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
    var correctedLines = new List<string>();
    int totalCorrections = 0;
    
    for (int i = 0; i < lines.Length; i++)
    {
        var line = lines[i];
        
        // Skip empty lines, sequence numbers, and timestamp lines
        if (string.IsNullOrWhiteSpace(line) || 
            IsSequenceNumber(line) ||           // ✅ Detect and skip sequence numbers
            line.Contains(" --> "))             // ✅ Detect and skip timestamps
        {
            correctedLines.Add(line);            // Keep unchanged
        }
        else
        {
            // This is subtitle text - apply OCR corrections
            var result = _ocrCorrectionEngine.Correct(line, options);
            correctedLines.Add(result.CorrectedText);
            totalCorrections += result.CorrectionCount;
        }
    }
    
    return (string.Join(Environment.NewLine, correctedLines), totalCorrections);
}

/// <summary>
/// Check if a line is an SRT sequence number (e.g., "1", "123", "4567").
/// </summary>
private static bool IsSequenceNumber(string line)
{
    var trimmed = line.Trim();
    if (string.IsNullOrEmpty(trimmed))
        return false;
    
    // Check if line contains only digits
    return trimmed.All(char.IsDigit);
}
```

## Verification
The fix correctly identifies:
- `"1"`, `"123"`, `"4567"` → **Sequence numbers** (not corrected) ✅
- `"I"`, `"Hello world"` → **Subtitle text** (corrected) ✅
- `"00:00:01,000 --> 00:00:03,500"` → **Timestamp** (not corrected) ✅
- Empty/whitespace lines → **Separator** (not corrected) ✅

## Benefits
- ✅ **Valid SRT format** - Plex and all media players can now read the files
- ✅ **Preserved OCR corrections** - Subtitle text still gets corrected
- ✅ **No performance impact** - Line-by-line processing is negligible
- ✅ **Backward compatible** - Existing functionality preserved

## Testing
To test the fix:
1. Extract PGS subtitles from a Blu-ray MKV file
2. Verify the output SRT file has numeric sequence numbers (1, 2, 3...)
3. Verify Plex recognizes and displays the subtitles correctly

## Date
October 16, 2025

## Related Files
- `SrtExtractor/Services/Implementations/SrtCorrectionService.cs` - Fixed
- `SrtExtractor/Services/Implementations/TesseractOcrService.cs` - Calls correction service
- `SrtExtractor/Coordinators/ExtractionCoordinator.cs` - Orchestrates OCR + correction

## Prevention
To prevent similar issues in the future:
- Always parse structured formats (SRT, VTT, ASS) before applying corrections
- Only correct content fields, never metadata or structural elements
- Add unit tests for format-preserving corrections

