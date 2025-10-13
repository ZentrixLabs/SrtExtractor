# OCR Correction Pattern Testing Workflow

This document outlines the systematic workflow for discovering, documenting, and batch-updating OCR correction patterns before releasing a new version of the ZentrixLabs.OcrCorrection NuGet package.

## Overview

Instead of releasing incremental pattern updates, we batch all discovered issues together for a comprehensive release. This approach:

- ✅ Provides users with more value per update
- ✅ Reduces testing overhead
- ✅ Ensures patterns are validated against multiple sources
- ✅ Minimizes version churn

## Workflow Steps

### 1. Extract Multiple SRT Files

Extract 5-10 diverse subtitle files using Tesseract OCR:

```powershell
# In SrtExtractor app:
# 1. Load MKV file
# 2. Probe tracks
# 3. Select PGS track
# 4. Extract to SRT
# 5. Copy output SRT to testing folder
```

**Recommended Test Set:**
- Action movies (fast dialogue, terse sentences)
- Drama films (complex dialogue, long sentences)
- Comedies (puns, wordplay, unusual phrasing)
- Sci-fi (technical terms, made-up words)
- Foreign films with English subs (translation quirks)
- Old films (different subtitle styling)

### 2. Run Quality Analysis

Use the provided PowerShell script to analyze each file:

```powershell
# Analyze a single file
.\analyze-ocr-quality.ps1 -SrtFile "Movie Name (Year).eng.srt"

# Analyze with report output
.\analyze-ocr-quality.ps1 -SrtFile "Movie Name (Year).eng.srt" -OutputReport "Movie_analysis.txt"

# Batch analyze all SRT files in current directory
Get-ChildItem *.srt | ForEach-Object {
    .\analyze-ocr-quality.ps1 -SrtFile $_.FullName -OutputReport "$($_.BaseName)_analysis.txt"
}
```

The script checks for:

1. **Capital I / Lowercase l Confusion**
   - Pattern: `[a-z]I\b|II[a-z]|\bI[a-z]{2,}`
   - Examples: `HeIIo`, `teII`, `stiII`

2. **Spacing Problems**
   - Missing space after punctuation: `\w[,\.][A-Za-z]`
   - Extra spaces: `\b\w \w\b`
   - Compound words: `\b(of|to|on|in|at)the\b|\bona\b`

3. **Apostrophe Issues**
   - Wrong characters: `\)re\b|\)ll\b|\)ve\b|\)nt\b`
   - Missing apostrophes: `\b(dont|wont|cant|youre|theyre)\b`

4. **Number/Letter Confusion**
   - Pattern: `\bI [0-9]|\$I[0-9O ]|\b0[A-Za-z]`
   - Examples: `$I O`, `I 00`, `0kay`

5. **Double Letter Issues**
   - Pattern: `\b\w*II\w*\b`
   - Examples: `feeIdead`, `RipIey`

6. **Sentence Start Issues**
   - Pattern: `^\s*[a-z]|[.!?]\s+[a-z]`
   - Lowercase after punctuation

### 3. Document Findings

Add all discovered issues to `OCR_CORRECTION_MISSING_PATTERNS.md`:

```markdown
## [Film Name] ([Year]) - [X] Issues Found

### Category: [Issue Type]

| Line | Original | Should Be | Context |
|------|----------|-----------|---------|
| 42   | `HeIIo` | `Hello` | "HeIIo, how are you?" |
| 89   | `ona` | `on a` | "Not ona good day" |

**Suggested Pattern:**
\`\`\`regex
Pattern: specific_regex_here
Replacement: $1 replacement $2
Category: CharacterSubstitution|Spacing|Apostrophes|Numbers
Description: What this pattern fixes
Priority: 40-60
\`\`\`
```

### 4. Group Patterns by Category

Organize all discovered patterns into logical groups:

**Character Substitution**
- Capital I ↔ lowercase l patterns
- O ↔ 0 confusion
- Other letter/number swaps

**Spacing Patterns**
- Missing space after punctuation
- Extra spaces within words
- Compound word issues
- Common phrase patterns

**Apostrophe Patterns**
- Wrong character apostrophes (`)` to `'`)
- Missing apostrophes in contractions

**Number Patterns**
- Currency patterns ($I O → $10)
- Standalone number/letter confusion

**Special Cases**
- Sentence start capitalization
- Common word misspellings specific to OCR

### 5. Create Test Cases

For each new pattern, create at least 2 test cases:

```csharp
[Fact]
public void CorrectText_FixesCompoundWord_Ona()
{
    // Arrange
    var input = "Not ona four-hour layover";
    var expected = "Not on a four-hour layover";
    
    // Act
    var result = _engine.Correct(input);
    
    // Assert
    Assert.Equal(expected, result.CorrectedText);
    Assert.Equal(1, result.CorrectionCount);
}

[Fact]
public void CorrectText_DoesNotBreakValidWords_Arena()
{
    // Arrange
    var input = "The arena was full"; // "arena" ends in "a" - should not split
    var expected = "The arena was full";
    
    // Act
    var result = _engine.Correct(input);
    
    // Assert
    Assert.Equal(expected, result.CorrectedText);
    Assert.Equal(0, result.CorrectionCount); // Should make NO corrections
}
```

### 6. Implement All Patterns

Add patterns to the appropriate files in ZentrixLabs.OcrCorrection:

**File Structure:**
```
src/Patterns/
├── CharacterSubstitutionPatterns.cs
├── SpacingPatterns.cs
├── ApostrophePatterns.cs
├── NumberPatterns.cs
└── EnglishPatternProvider.cs (combines all)
```

**Example Implementation:**
```csharp
// SpacingPatterns.cs
new CorrectionPattern(
    pattern: @"(\w),([a-z])",
    replacement: "$1, $2",
    category: "Spacing",
    description: "Add missing space after comma",
    priority: 55
),
new CorrectionPattern(
    pattern: @"(\w)\.([A-Z])",
    replacement: "$1. $2",
    category: "Spacing", 
    description: "Add missing space after period",
    priority: 55
),
```

### 7. Run Full Test Suite

```bash
# Run all tests
dotnet test

# Run specific category tests
dotnet test --filter "Category=Spacing"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

**Required Test Results:**
- ✅ All existing tests must pass (no regressions)
- ✅ All new pattern tests must pass
- ✅ Code coverage ≥ 90% on new patterns
- ✅ No false positives in validation tests

### 8. Validate Against Real Files

Re-run the quality analysis on all test SRT files:

```powershell
# Batch re-analyze after implementing patterns
Get-ChildItem *.srt | ForEach-Object {
    Write-Host "`nAnalyzing: $($_.Name)" -ForegroundColor Cyan
    .\analyze-ocr-quality.ps1 -SrtFile $_.FullName
}
```

**Success Criteria:**
- Total issues should decrease significantly
- New patterns should reduce errors in target categories
- No new errors introduced (validate old patterns still work)
- Overall success rate should improve

### 9. Performance Testing

Ensure new patterns don't impact performance:

```csharp
[Fact]
public void CorrectText_Performance_LargeFile()
{
    var input = File.ReadAllText("large_subtitle_file.srt"); // ~1500 lines
    var stopwatch = Stopwatch.StartNew();
    
    var result = _engine.Correct(input);
    
    stopwatch.Stop();
    Assert.True(stopwatch.ElapsedMilliseconds < 1000, 
        $"Processing took {stopwatch.ElapsedMilliseconds}ms (should be < 1000ms)");
}
```

**Performance Targets:**
- < 1000ms for 1500-line subtitle file
- < 50ms for 100-line subtitle file
- Linear scaling with file size

### 10. Update Documentation

Before release, update:

**CHANGELOG.md:**
```markdown
## Version 1.0.1 - Enhanced Pattern Coverage (2025-10-XX)

### Added
- 8 new spacing patterns for punctuation and compound words
- 3 new capital I/lowercase l patterns for common words
- 2 new apostrophe patterns for contractions

### Improved
- Success rate: 99.85% → 99.98% on test corpus
- Total patterns: 837 → 845
- Tested against 10 additional feature films (15,000+ subtitles)

### Performance
- No measurable impact on processing time
- All patterns pre-compiled for optimal performance
```

**README.md:**
- Update pattern count
- Update success rate metrics
- Add any new pattern categories

**OCR_CORRECTION_MISSING_PATTERNS.md:**
- Mark all implemented patterns as ✅ Implemented in v1.0.1
- Keep for historical reference
- Start new section for future patterns

### 11. Version and Release

```bash
# Update version in .csproj
# Version follows semantic versioning:
# - Patch (1.0.X): New patterns, bug fixes, performance improvements
# - Minor (1.X.0): New pattern categories, API additions
# - Major (X.0.0): Breaking API changes

# Build release package
dotnet pack -c Release

# Test package locally
dotnet add package ZentrixLabs.OcrCorrection --source ./bin/Release

# Publish to NuGet.org
dotnet nuget push bin/Release/ZentrixLabs.OcrCorrection.1.0.1.nupkg --api-key YOUR_KEY --source https://api.nuget.org/v3/index.json
```

## Summary Checklist

Before publishing a new version:

- [ ] Extracted 5-10 diverse SRT files
- [ ] Ran quality analysis on all files
- [ ] Documented all issues in markdown report
- [ ] Grouped patterns by category
- [ ] Created test cases for all new patterns
- [ ] Implemented patterns in appropriate files
- [ ] All tests passing (existing + new)
- [ ] Validated against real files (improved results)
- [ ] Performance tests passing
- [ ] Documentation updated (CHANGELOG, README)
- [ ] Version number incremented
- [ ] Package built and tested locally
- [ ] Ready to publish to NuGet.org

## Expected Results by Version

### v1.0.0 (Current)
- Pattern count: ~837
- Success rate: 99.85%
- Test corpus: 7,164 subtitles (2 films)

### v1.0.1 (Next Release - Target)
- Pattern count: ~845-850
- Success rate: 99.98%+
- Test corpus: 15,000+ subtitles (10+ films)
- New categories: Enhanced spacing, punctuation

### v1.1.0 (Future - Major Patterns)
- Pattern count: ~900-950
- Success rate: 99.99%+
- Test corpus: 50,000+ subtitles (30+ films)
- New categories: Context-aware corrections, multi-language support

## Tools

- **analyze-ocr-quality.ps1**: Automated OCR error detection
- **OCR_CORRECTION_MISSING_PATTERNS.md**: Issue tracking document
- **Unit test framework**: Pattern validation
- **SrtExtractor app**: Test file generation

---

*This workflow ensures high-quality, well-tested pattern releases that provide maximum value to users.*

