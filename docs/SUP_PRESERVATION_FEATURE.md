# SUP File Preservation Feature

## Summary
Added a toggle to preserve SUP (PGS subtitle) files after OCR extraction for debugging OCR quality issues.

## Changes Made

### 1. **AppSettings Model** (`Models/AppSettings.cs`)
- Added `PreserveSupFiles` boolean parameter
- Default value: `false`
- Saves to `settings.json` in AppData

### 2. **ExtractionState** (`State/ExtractionState.cs`)
- Added `PreserveSupFiles` observable property
- Allows UI binding and state management

### 3. **SettingsWindow UI** (`Views/SettingsWindow.xaml`)
- Added "Debugging Options" GroupBox in Advanced tab
- Checkbox: "Preserve SUP files for debugging"
- User-friendly explanation text

### 4. **MainViewModel** (`ViewModels/MainViewModel.cs`)
- **Loading**: Reads `PreserveSupFiles` from settings on startup
- **Saving**: Saves `PreserveSupFiles` to settings when user changes it
- **Extraction**: Modified `ExtractPgsSubtitlesAsync` to check setting before deleting SUP file

## How It Works

### Before (Default Behavior)
```
1. Extract PGS to SUP file
2. OCR SUP to SRT
3. Delete SUP file ✓
4. Only SRT remains
```

### After (When Toggle Enabled)
```
1. Extract PGS to SUP file
2. OCR SUP to SRT
3. Keep SUP file ✓
4. Both SRT and SUP remain
```

## Usage

1. Open Settings (⚙️ button)
2. Go to "Advanced" tab
3. Enable "Preserve SUP files for debugging"
4. Click OK
5. Extract subtitles as normal
6. SUP file will remain next to the SRT output

## Benefits for Debugging

### What You Can Do With Preserved SUP Files:
- **Visual Inspection**: Open SUP in video player to see actual subtitle images
- **Font Analysis**: Check if text has unusual fonts, italics, or styling
- **Image Quality**: Verify resolution, contrast, and clarity
- **OCR Testing**: Test different OCR engines or configurations on the same source
- **Comparison**: Compare SUP against OpenSubtitles or other reference SRTs
- **Bug Reports**: Include SUP file when reporting OCR errors

## File Locations

**SUP files are saved next to the output SRT:**
```
Movie.mkv              (source)
Movie.eng.srt          (extracted SRT)
Movie.eng.sup          (preserved SUP for debugging)
```

## Implementation Details

### Settings Persistence
- Settings saved to: `%APPDATA%\SrtExtractor\settings.json`
- Persists across app restarts
- Default: `false` (backward compatible)

### Logging
- When enabled: Logs "Preserving SUP file for debugging: [path]"
- When disabled: Logs "Temporary SUP file deleted"
- Errors during deletion are logged but not fatal

## Next Steps for OCR Improvement

Now that we can preserve SUP files, we can:
1. Extract problematic subtitles and save SUP files
2. Visually inspect what Tesseract is receiving
3. Test preprocessing techniques:
   - Image upscaling (2-3x)
   - Contrast enhancement
   - Binarization
   - Noise removal
4. Compare different OCR configurations
5. Test alternative OCR engines if needed

## Testing Checklist

- [ ] Toggle appears in Settings > Advanced tab
- [ ] Setting persists after app restart
- [ ] SUP file is deleted when toggle is OFF (default)
- [ ] SUP file is preserved when toggle is ON
- [ ] Log messages show correct behavior
- [ ] File naming matches SRT file
- [ ] No errors when extraction completes

## Related Documents

- `OCR_ERROR_ANALYSIS.md` - Analysis of OCR errors from Alien (1979)
- `OCR_CORRECTION_MISSING_PATTERNS.md` - Missing correction patterns
- `docs/ASS_TO_SRT_BUGFIX.md` - Previous OCR improvements

