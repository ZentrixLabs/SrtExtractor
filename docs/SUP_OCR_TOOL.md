# SUP OCR Tool - Feature Documentation

## Overview
The SUP OCR Tool is a dedicated utility for processing PGS (Blu-ray) subtitle files directly, without requiring extraction from an MKV container. This is useful for testing, debugging, and re-processing existing SUP files.

## Access
**Menu**: Tools → Load SUP File...

## Features

### 1. File Selection
- Browse button to select SUP files
- Displays full file path
- Validates file exists before processing

### 2. OCR Settings
- **Language Selection**: Choose OCR language (eng, fra, deu, spa, ita)
- **Auto-Correction Toggle**: Enable/disable OCR error correction
  - Uses ZentrixLabs.OcrCorrection package (~837+ patterns)
  - Fixes common Tesseract errors automatically

### 3. Progress Tracking
- **Status Messages**: Real-time processing status
- **Progress Bar**: Visual feedback during OCR
- **Processing Log**: Timestamped log of operations
  - File selection
  - OCR start
  - Success/error messages
  - Output file location

### 4. Output
- SRT file saved next to source SUP file
- Same filename with `.srt` extension
- Success notification with output path
- Automatic error handling and reporting

## Use Cases

### 1. Testing OCR Settings
- Test different OCR languages on the same SUP file
- Compare results with/without correction
- Verify OCR quality improvements

### 2. Debugging OCR Issues
- Re-process specific SUP files with known problems
- Inspect OCR output without full MKV extraction
- Test preprocessing changes quickly

### 3. Batch Re-Processing (Future)
- Could be extended to support multiple SUP files
- Useful for re-processing old extractions with improved OCR

### 4. Quality Assurance
- Quick spot-check of OCR quality
- Verify subtitle content before committing
- Compare with reference subtitles

## Technical Details

### OCR Engine
- **Tesseract OCR** (command-line): Bundled with application
- **PSM Mode**: 6 (single uniform block of text)
- **Quality**: ~100% accuracy on standard Blu-ray subtitles
- **Speed**: ~30-40 seconds for full movie (~1000 subtitles)

### Processing Flow
1. Parse SUP file to extract PGS images
2. Save each image as temp PNG
3. Call `tesseract.exe` with optimal parameters
4. Read OCR text from output file
5. Build SRT with timecodes from SUP
6. Optionally apply OCR correction
7. Save final SRT file

### Error Handling
- File not found validation
- OCR engine availability check
- Graceful error messages
- Detailed logging for troubleshooting

## User Interface

### Window Layout
```
┌─────────────────────────────────────────┐
│          SUP OCR Tool                   │
│  Extract text from PGS subtitle images  │
├─────────────────────────────────────────┤
│                                         │
│  Input File: [____________] [Browse]    │
│                                         │
│  OCR Settings:                          │
│    Language: [eng ▼]                    │
│    ☑ Apply OCR correction              │
│                                         │
│  Processing Status: (when running)      │
│    Status: "Processing SUP file..."     │
│    Progress: [████████░░░░░░] 65%      │
│    Details: "OCR on image 650/1000"     │
│                                         │
│  Processing Log:                        │
│    [15:30:12] Selected SUP file...      │
│    [15:30:15] Starting OCR...           │
│    [15:30:45] ✓ OCR completed!          │
│                                         │
├─────────────────────────────────────────┤
│               [Start OCR] [Close]       │
└─────────────────────────────────────────┘
```

## Benefits

### User Experience
- ✅ **Dedicated UI**: Professional tool window instead of simple file dialog
- ✅ **Visual Feedback**: Progress bar and status messages
- ✅ **Real-time Logging**: See exactly what's happening
- ✅ **Easy to Use**: Simple, intuitive interface

### Developer Benefits
- ✅ **Testable**: Easy to test OCR improvements
- ✅ **Reusable**: Can be extended for batch processing
- ✅ **Maintainable**: Follows MVVM pattern
- ✅ **Documented**: Clear code and logging

## Future Enhancements
- [ ] Batch SUP file processing
- [ ] OCR quality preview before saving
- [ ] Custom tessdata path selection
- [ ] Advanced Tesseract parameter tuning
- [ ] Side-by-side comparison with reference SRT
- [ ] Export OCR statistics and error report

## Version History
- **v2.0.4** (October 12, 2025): Initial release as dedicated tool window
  - Full UI with progress tracking
  - Integrated OCR correction
  - Professional presentation

## Related Features
- **SUP Preservation Toggle**: Settings → Advanced → Debugging Options
- **SRT Correction Tool**: Tools → SRT Correction (for post-processing)
- **Main Extraction**: Extract SUP from MKV then OCR

## Files
- `Views/SupOcrWindow.xaml` - UI definition
- `Views/SupOcrWindow.xaml.cs` - Code-behind
- `ViewModels/SupOcrViewModel.cs` - Business logic and state
- `Services/Implementations/TesseractOcrService.cs` - OCR engine

