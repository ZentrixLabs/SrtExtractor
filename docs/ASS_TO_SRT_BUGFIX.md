# Subtitle Format Conversion Bug Fix

## Issue
When extracting subtitles from MKV/MP4 files containing non-SRT text-based formats (ASS/WebVTT), the application was outputting files with a `.srt` extension but with the original format content. This resulted in "fake SRT" files that couldn't be read by most subtitle players.

### Affected Formats
1. **ASS (Advanced SubStation Alpha)** - Contains style metadata and formatting tags
2. **WebVTT (Web Video Text Tracks)** - Uses dots for milliseconds and may have cue settings

### Example - ASS Format
A file named `subtitle.eng.srt` would contain ASS format:
```
[Script Info]
ScriptType: v4.00+
...
[Events]
Dialogue: 0,0:00:00.81,0:00:02.14,Default,,0,0,0,,Text here
```

### Example - WebVTT Format
A file named `subtitle.eng.srt` would contain WebVTT format:
```
WEBVTT

00:00:00.810 --> 00:00:02.140
Text here
```

**Both should be converted to proper SRT format:**
```
1
00:00:00,810 --> 00:00:02,140
Text here
```

## Root Cause
The format conversion function (`ConvertAssToSrtIfNeededAsync`) was only being called during OCR operations (PGS subtitle extraction). When extracting text-based subtitles (like ASS or WebVTT embedded in MKV/MP4 files), the application used a different code path that bypassed the conversion, resulting in non-SRT content being saved with an `.srt` extension.

### Code Path Issue
- **PGS Extraction** (image-based): `ExtractPgsSubtitlesAsync()` → `OcrSupToSrtAsync()` → ✅ **Conversion runs**
- **Text Extraction** (text-based): `ExtractTextSubtitlesAsync()` → `MkvToolService.ExtractTextAsync()` → ❌ **No conversion**
- **MP4 Extraction**: `ExtractFromMp4Async()` → `FFmpegService.ExtractSubtitleAsync()` → ❌ **No conversion**

## Solution
1. **Made the conversion method public** in `ISubtitleOcrService` interface
2. **Added conversion calls** to all extraction paths:
   - `ExtractTextSubtitlesAsync()` - for MKV text-based subtitles
   - `ExtractFromMp4Async()` - for MP4 subtitles

Now, regardless of the extraction method, if the resulting file contains ASS format content, it will automatically be converted to proper SRT format.

## Implementation Details

### Files Modified
1. **`SrtExtractor/Services/Interfaces/ISubtitleOcrService.cs`**
   - Added `ConvertAssToSrtIfNeededAsync()` to the interface

2. **`SrtExtractor/Services/Implementations/SubtitleOcrService.cs`**
   - Changed method visibility from `private` to `public`

3. **`SrtExtractor/ViewModels/MainViewModel.cs`**
   - Added conversion call in `ExtractTextSubtitlesAsync()`
   - Added conversion call in `ExtractFromMp4Async()`

### Conversion Logic

#### ASS to SRT Conversion
1. Detects ASS format by checking for `[Script Info]` or `[Events]` sections
2. Parses ASS dialogue lines: `Dialogue: Layer,Start,End,Style,Name,MarginL,MarginR,MarginV,Effect,Text`
3. Converts time format: ASS `0:00:00.81` (H:MM:SS.CS) → SRT `00:00:00,810` (HH:MM:SS,MMM)
4. Removes ASS formatting tags (e.g., `{\\b1}`, `{\\i1}`)
5. Converts line breaks: `\\N` → actual newlines
6. Outputs proper SRT format with sequential numbering

#### WebVTT to SRT Conversion
1. Detects WebVTT format by checking for `WEBVTT` header
2. Skips WebVTT metadata sections (STYLE, REGION, NOTE)
3. Parses timing lines with optional cue settings
4. Converts time format: WebVTT `00:00:00.810` (dots) → SRT `00:00:00,810` (commas)
5. Handles short-form times: WebVTT `00.810` → SRT `00:00:00,810`
6. Strips cue settings (e.g., `align:start position:0%`)
7. Outputs proper SRT format with sequential numbering

## Testing
To test the fix:
1. Extract subtitles from files containing ASS or WebVTT format tracks
2. Verify the output `.srt` file has proper SRT format:
   - Sequential numbers (1, 2, 3...)
   - Time format: `HH:MM:SS,MMM --> HH:MM:SS,MMM` (commas, not dots)
   - Plain text content (no `[Script Info]`, `Dialogue:`, or `WEBVTT` lines)
   - No cue settings or formatting tags

## Impact
- **Positive**: All subtitle extractions now produce proper SRT files
- **Performance**: Negligible impact (conversion only runs if non-SRT format is detected)
- **Compatibility**: Fixes compatibility with subtitle players that don't support ASS or WebVTT formats
- **Formats Supported**: ASS, SSA, and WebVTT are all automatically converted to SRT

## Date
October 11, 2025

