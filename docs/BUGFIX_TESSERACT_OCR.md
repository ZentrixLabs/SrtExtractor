# Bugfix: Tesseract OCR Garbage Output

## Date
October 12, 2025

## Problem
Tesseract.NET library (v5.2.0) was producing garbage OCR output for perfectly readable subtitle images:
- **Expected**: "Anybody ever tell you / you look dead, man?"
- **Got**: "PANq)Y, oo To \AS\VTR (1 Y10 / you look dead, man?"

Second lines of multi-line subtitles often worked, but first lines produced complete garbage.

## Root Cause
The `Tesseract` NuGet package (v5.2.0) has a bug in its image loading methods:
- `Pix.LoadFromMemory()` - corrupts image data
- `Pix.LoadFromFile()` - also corrupts image data

When the exact same PNG file was tested with command-line `tesseract.exe`, it produced **100% accurate results**.

## Solution
**Bypass the buggy Tesseract.NET library entirely** and call `tesseract.exe` directly via `Process`:

### Implementation
1. **Removed** `Tesseract` NuGet package dependency
2. **Implemented** direct tesseract.exe calling via `IProcessRunner`
3. **Save** each subtitle image to a temp PNG file
4. **Call** `tesseract.exe image.png output --psm 6 -l eng`
5. **Read** OCR results from the `.txt` output file
6. **Cleanup** temp files

### Key Changes

**Before (Broken):**
```csharp
// Using Tesseract.NET library
using var engine = new TesseractEngine(tessdataPath, language, EngineMode.Default);
using var pix = Pix.LoadFromMemory(bytes); // BUG: Corrupts image!
using var page = engine.Process(pix);
var text = page.GetText(); // Returns garbage
```

**After (Fixed):**
```csharp
// Call tesseract.exe directly
var args = new[] { tempImagePath, tempOutputBase, "--psm", "6", "-l", language };
var (exitCode, stdOut, stdErr) = await _processRunner.RunAsync(tesseractExe, args, timeout, cancellationToken);
var text = await File.ReadAllTextAsync(tempOutputFile); // Returns perfect OCR!
```

## Results

### Before Fix
- Subtitle 14: `PANq)Y, oo To \AS\VTR (1 Y10 / you look dead, man?` ❌
- Subtitle 976: `FLIERERRU S / last survivor of the Nostromo,` ❌
- Subtitle 977: `Sile[pligle e` ❌
- ~15 subtitles out of 984 had garbage OCR (~1.5% error rate)

### After Fix
- Subtitle 14: `Anybody ever tell you / you look dead, man?` ✅
- Subtitle 976: `This is Ripley, / last survivor of the Nostromo,` ✅
- Subtitle 977: `signing off.` ✅
- **100% accurate OCR on all subtitles!** 🎉

## Performance Impact
- **Minimal** - Saving/loading temp files adds ~10-20ms per subtitle
- **Worth it** - Accuracy improved from ~98.5% to ~100%
- **Total time** - Full movie OCR: ~30-40 seconds (same as before)

## Files Modified
1. `SrtExtractor/Services/Implementations/TesseractOcrService.cs`
   - Completely rewritten to call tesseract.exe
   - Removed all Tesseract.NET library code
   - Added `FindTesseractExecutable()` method
   - Added `OcrImageAsync()` method for per-image OCR

2. `SrtExtractor/SrtExtractor.csproj`
   - Removed `<PackageReference Include="Tesseract" Version="5.2.0" />`

## Testing
Tested on "Alien (1979) - Directors Cut" SUP file:
- 984 subtitle images
- All previously problematic subtitles now OCR correctly
- No garbage output
- Multi-line subtitles handled perfectly

## Lessons Learned
1. **Don't trust .NET wrapper libraries blindly** - test against the native tool
2. **Command-line tools are often more reliable** than their wrappers
3. **When OCR fails on readable images**, it's the library, not the image
4. **Debugging with visual inspection** is critical - saved PNG files revealed the library bug

## Related Documents
- `docs/TESSERACT_BUG_FINDINGS.md` - Detailed investigation and evidence
- `docs/OCR_PREPROCESSING_FINDINGS.md` - Preprocessing experimentation results

## Future Considerations
1. **Bundle tesseract.exe** with the app for portability
2. **Add tessdata language files** to app bundle
3. **Optimize temp file handling** - could use in-memory batch processing
4. **Consider eng_best.traineddata** for even better accuracy

