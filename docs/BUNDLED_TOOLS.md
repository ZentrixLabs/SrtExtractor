# Bundled Tools - Zero Dependency Architecture

## Date
October 12-13, 2025

## Overview
SrtExtractor v2.0.4+ is now a **completely self-contained, zero-dependency application**. All required external tools are bundled with the app, eliminating the need for system installations, internet connections, or complex setup procedures.

## Bundled Tools

### 1. Tesseract OCR
- **Location**: `tesseract-bin/`
- **Files**: tesseract.exe + 51 DLL dependencies
- **Size**: ~160 MB
- **License**: Apache 2.0 (redistribution allowed)
- **Purpose**: High-quality OCR conversion of image-based subtitles (PGS/HDMV)
- **Version**: 5.x (latest stable)

### 2. MKVToolNix
- **Location**: `mkvtoolnix-bin/`
- **Files**: mkvmerge.exe, mkvextract.exe + DLL dependencies
- **Size**: ~37 MB
- **License**: GPL-2.0 (redistribution allowed)
- **Purpose**: Probing and extracting subtitles from MKV containers
- **Tools**:
  - `mkvmerge.exe` - Probe MKV files for track information
  - `mkvextract.exe` - Extract subtitle tracks from MKV

### 3. FFmpeg
- **Location**: `ffmpeg-bin/`
- **Files**: ffmpeg.exe, ffprobe.exe
- **Size**: ~334 MB
- **License**: GPL (redistribution allowed)
- **Purpose**: Probing and extracting subtitles from MP4 files
- **Tools**:
  - `ffmpeg.exe` - Extract subtitle tracks from MP4
  - `ffprobe.exe` - Probe MP4 files for track information

### 4. Language Data
- **Location**: `tessdata/`
- **Files**: eng.traineddata (English OCR training data)
- **Size**: ~4 MB
- **License**: Apache 2.0
- **Purpose**: Tesseract language model for English text recognition

## Total Bundle Size
**~530 MB** (all tools + dependencies)

## Benefits

### For Users
1. ✅ **Zero Setup** - Download, extract, run. That's it.
2. ✅ **Fully Portable** - Run from USB drive, network location, or any folder
3. ✅ **No Internet Required** - All tools bundled, no downloads needed
4. ✅ **No Administrator Rights** - No system installations required
5. ✅ **Consistent Experience** - Same tools, same versions, always works

### For Developers
1. ✅ **Simpler Codebase** - Removed ~500+ lines of tool detection/installation logic
2. ✅ **Removed Services**:
   - `WingetService` - No longer needed
   - Tool installation dialogs - Obsolete
   - Complex detection logic - Simplified
3. ✅ **Easier Testing** - Tools always in same location
4. ✅ **Fewer Support Issues** - No "tool not found" errors
5. ✅ **Better Reliability** - No dependency on external installations

## Tool Detection Changes

### Before (v2.0.3 and earlier)
```csharp
// Complex detection with multiple fallback paths
var toolStatus = await _toolDetectionService.CheckMkvToolNixAsync();
if (!toolStatus.IsInstalled)
{
    // Prompt user to install via winget or manually
    await InstallMkvToolNixAsync();
}
```

### After (v2.0.4+)
```csharp
// Simple: Always use bundled tools
var bundledPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mkvtoolnix-bin");
// Tools are always there, always work
```

## File Structure

```
SrtExtractor\
├── SrtExtractor.exe
├── tesseract-bin\
│   ├── tesseract.exe
│   └── *.dll (51 dependencies)
├── mkvtoolnix-bin\
│   ├── mkvmerge.exe
│   ├── mkvextract.exe
│   └── *.dll (dependencies)
├── ffmpeg-bin\
│   ├── ffmpeg.exe
│   └── ffprobe.exe
├── tessdata\
│   └── eng.traineddata
└── (other app files)
```

## License Compliance

All bundled tools are open-source with redistribution-friendly licenses:

- **Tesseract OCR**: Apache 2.0
- **MKVToolNix**: GPL-2.0
- **FFmpeg**: GPL/LGPL (built with GPL configuration)

**Our License**: MIT (compatible with all bundled tool licenses)

As required by GPL licenses, we provide:
- Source code repository: https://github.com/ZentrixLabs/SrtExtractor
- Clear attribution in About dialog
- License files in distribution

## Performance Impact

**Negligible** - Bundled tools run at the same speed as system-installed versions:
- MKV extraction: Same performance
- MP4 extraction: Same performance
- OCR processing: Same performance

The only difference is bundle size (~530 MB vs ~50 MB), which is a worthwhile trade-off for zero dependencies.

## Distribution Size Comparison

| Version | Size | Tools Bundled |
|---------|------|---------------|
| v2.0.3 | ~50 MB | None (user installs) |
| v2.0.4 | ~530 MB | All (fully portable) |

## Future Considerations

### Additional Languages
Users can add more Tesseract language files to `tessdata/`:
- Download from: https://github.com/tesseract-ocr/tessdata
- Place `.traineddata` files in `tessdata/` folder
- Select language in OCR settings

### Smaller Builds (Optional)
For users who want smaller downloads, we could offer:
- **Lite version**: No tools bundled, user installs (50 MB)
- **Full version**: All tools bundled, zero setup (530 MB)

Currently, we only ship the **Full version** for the best user experience.

## Removed Code

With full bundling, we removed:
- `WingetService.cs` (~200 lines)
- `IWingetService.cs` (~50 lines)
- Tool installation dialogs and prompts
- Complex fallback detection logic
- Winget availability checks
- Manual installation instructions

**Total code removed**: ~500+ lines of complexity

## Testing Checklist

- [x] MKV extraction with bundled mkvmerge/mkvextract
- [x] MP4 extraction with bundled ffmpeg/ffprobe
- [x] OCR with bundled tesseract
- [x] Batch processing works correctly
- [x] No "tool not found" errors
- [x] App runs without any system tools installed

## Conclusion

By bundling all required tools, SrtExtractor v2.0.4 delivers a **professional, zero-dependency, fully portable** subtitle extraction solution. Users can download and start working immediately, and developers benefit from a simpler, more maintainable codebase.

**Trade-off**: Larger download size (~530 MB)  
**Benefit**: Perfect user experience + zero support issues

The trade-off is absolutely worth it for a professional application.

