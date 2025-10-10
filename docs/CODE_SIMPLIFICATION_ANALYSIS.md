# Code Simplification Analysis

**Date**: October 10, 2025  
**Scope**: Review of logic complexity and opportunities for boolean simplification

## Executive Summary

The codebase generally follows good patterns, but there are several areas where overly complex logic could be replaced with simpler booleans, enums, or lookup tables. This document identifies these opportunities with specific recommendations.

---

## 1. ExtractionState.cs - Redundant Computed Properties

### Issue: Proxy Properties That Add No Value

**Location**: Lines 307-309

```csharp
public bool ShowNetworkWarning => IsNetworkFile && !string.IsNullOrEmpty(MkvPath);
public bool ShowNoTracksWarning => ShowNoTracksError;
public bool ShowExtractionSuccessMessage => ShowExtractionSuccess;
```

**Problem**:
- `ShowNoTracksWarning` is just a direct proxy for `ShowNoTracksError` - no computation
- `ShowExtractionSuccessMessage` is just a direct proxy for `ShowExtractionSuccess` - no computation
- These add an extra layer of indirection without benefit

**Recommendation**: 
Remove the proxy properties and use the backing fields directly in bindings:
- Use `ShowNoTracksError` instead of `ShowNoTracksWarning`
- Use `ShowExtractionSuccess` instead of `ShowExtractionSuccessMessage`

**Impact**: Reduces cognitive load, simplifies property change notifications

---

## 2. ExtractionState.cs - Over-Engineered Network Warning Logic

### Issue: Complex String Building When Boolean Would Suffice

**Location**: Lines 461-481

```csharp
public void UpdateNetworkDetection(bool isNetwork, double estimatedMinutes, string formattedSize, string networkDriveInfo)
{
    IsNetworkFile = isNetwork;
    EstimatedProcessingTimeMinutes = estimatedMinutes;
    FormattedFileSize = formattedSize;
    NetworkDriveInfo = networkDriveInfo;

    if (isNetwork)
    {
        NetworkWarningMessage = $"File is on network drive {networkDriveInfo}\n" +
                              $"File size: {formattedSize}\n" +
                              $"Estimated processing time: ~{FormatEstimatedTime(estimatedMinutes)}";
    }
    else
    {
        NetworkWarningMessage = "";
    }
    
    OnPropertyChanged(nameof(ShowNetworkWarning));
}
```

**Problem**:
- Stores redundant information (networkDriveInfo, formattedSize are stored separately AND in the message)
- UI could construct this message using data binding
- `ShowNetworkWarning` computed property adds extra layer

**Recommendation**: 
Simplify to just store booleans and let UI construct messages:

```csharp
public void UpdateNetworkDetection(bool isNetwork, double estimatedMinutes, string formattedSize, string networkDriveInfo)
{
    IsNetworkFile = isNetwork;
    EstimatedProcessingTimeMinutes = estimatedMinutes;
    FormattedFileSize = formattedSize;
    NetworkDriveInfo = networkDriveInfo;
}
```

Then in XAML, use data binding and a converter to construct the message.

**Impact**: Reduces string manipulation, simplifies state management

---

## 3. SubtitleTrack.cs - Repeated Codec Detection Logic

### Issue: Same Codec Checks Duplicated Across 3 Properties

**Location**: Lines 107-192 (FormatDisplay, SpeedIndicator, FormatIcon)

```csharp
public string FormatDisplay
{
    get
    {
        var codec = Codec.ToUpperInvariant();
        
        if (codec.Contains("S_HDMV/PGS") || codec.Contains("PGS"))
            return "Image-based (PGS)";
        if (codec.Contains("VOBSUB") || codec.Contains("S_VOBSUB"))
            return "Image-based (VobSub)";
        // ... repeated checks ...
    }
}

public string SpeedIndicator
{
    get
    {
        var codec = Codec.ToUpperInvariant();
        
        if (codec.Contains("S_HDMV/PGS") || codec.Contains("PGS") || /* ... same checks again ... */)
            return "🐢 OCR Required";
        // ... repeated checks ...
    }
}
```

**Problem**:
- Same codec detection logic repeated 3 times
- Each property call does string operations (ToUpperInvariant, Contains) repeatedly
- No caching of codec type determination

**Recommendation**: 
Add a `CodecType` enum and determine it once:

```csharp
public enum SubtitleCodecType
{
    Unknown,
    TextBasedSrt,
    TextBasedAss,
    TextBasedWebVtt,
    TextBasedGeneric,
    ImageBasedPgs,
    ImageBasedVobSub,
    ImageBasedDvb
}

private CodecType? _codecType;
public CodecType CodecType
{
    get
    {
        if (_codecType.HasValue)
            return _codecType.Value;
            
        var codec = Codec.ToUpperInvariant();
        
        if (codec.Contains("S_HDMV/PGS") || codec.Contains("PGS"))
            _codecType = CodecType.ImageBasedPgs;
        else if (codec.Contains("VOBSUB") || codec.Contains("S_VOBSUB"))
            _codecType = CodecType.ImageBasedVobSub;
        // ... single place for all codec detection
        else
            _codecType = CodecType.Unknown;
            
        return _codecType.Value;
    }
}

public string FormatDisplay => CodecType switch
{
    CodecType.ImageBasedPgs => "Image-based (PGS)",
    CodecType.ImageBasedVobSub => "Image-based (VobSub)",
    CodecType.TextBasedSrt => "Text (SRT)",
    // ... clean switch expression
    _ => Codec
};

public string SpeedIndicator => CodecType switch
{
    CodecType.ImageBasedPgs or CodecType.ImageBasedVobSub or CodecType.ImageBasedDvb => "🐢 OCR Required",
    CodecType.TextBasedSrt or CodecType.TextBasedAss or CodecType.TextBasedWebVtt => "⚡ Fast",
    _ => "❓ Unknown"
};

public bool RequiresOcr => CodecType is CodecType.ImageBasedPgs or CodecType.ImageBasedVobSub or CodecType.ImageBasedDvb;
public bool IsTextBased => CodecType is CodecType.TextBasedSrt or CodecType.TextBasedAss or CodecType.TextBasedWebVtt or CodecType.TextBasedGeneric;
```

**Impact**: 
- Single source of truth for codec detection
- Cached result (computed once)
- Clean, readable switch expressions
- Easy to add new codec types
- Boolean properties for common checks

---

## 4. MkvToolService.cs - Over-Complicated Track Type Detection

### Issue: Deeply Nested If Logic Could Be Simplified

**Location**: Lines 445-489

```csharp
private static string DetectTrackType(long? bitrate, int? frameCount, double? duration, bool forced, bool isClosedCaption)
{
    // Handle closed captions first
    if (isClosedCaption)
    {
        return forced ? "CC Forced" : "CC";
    }

    // Handle explicitly forced tracks
    if (forced)
    {
        return "Forced";
    }

    // Analyze characteristics for PGS tracks
    if (bitrate.HasValue && frameCount.HasValue)
    {
        // Very low bitrate and frame count = likely forced/partial
        if (bitrate < 1000 && frameCount < 50)
            return "Forced";

        // Low bitrate and few frames = likely forced
        if (bitrate < 10000 && frameCount < 200)
            return "Forced";

        // High bitrate and many frames = likely full subtitles
        if (bitrate > 20000 && frameCount > 1000)
            return "Full";

        // Medium characteristics = likely full subtitles
        if (bitrate > 10000 && frameCount > 500)
            return "Full";
    }

    return "Full";
}
```

**Problem**:
- Multiple nested if checks with magic numbers
- Complex heuristic logic that's hard to maintain
- Return string instead of enum
- No clear separation between definitive flags and heuristics

**Recommendation**: 
Use enum and separate definitive checks from heuristics:

```csharp
public enum TrackType
{
    Full,
    Forced,
    ClosedCaption,
    ClosedCaptionForced
}

private static TrackType DetectTrackType(long? bitrate, int? frameCount, double? duration, bool forced, bool isClosedCaption)
{
    // Definitive flags take precedence
    if (isClosedCaption && forced) return TrackType.ClosedCaptionForced;
    if (isClosedCaption) return TrackType.ClosedCaption;
    if (forced) return TrackType.Forced;
    
    // Heuristics only if no definitive flags
    if (bitrate.HasValue && frameCount.HasValue)
    {
        var isForcedLikely = (bitrate < 1000 && frameCount < 50) ||
                             (bitrate < 10000 && frameCount < 200);
        
        if (isForcedLikely)
            return TrackType.Forced;
    }
    
    return TrackType.Full;
}
```

**Impact**: 
- Type-safe enum instead of strings
- Clear separation: definitive flags vs. heuristics
- Single boolean expression for heuristic check
- Easier to test and maintain

---

## 5. MkvToolService.cs - Timeout Calculation Over-Engineering

### Issue: Complex Switch Expression for Simple Scaling

**Location**: Lines 496-530

```csharp
private static TimeSpan CalculateTimeoutForFile(string filePath)
{
    var fileInfo = new FileInfo(filePath);
    var sizeGB = fileInfo.Length / (1024.0 * 1024.0 * 1024.0);
    
    var baseMinutes = 5.0;
    
    var additionalMinutes = sizeGB switch
    {
        < 10 => sizeGB * 1.0,
        < 50 => sizeGB * 2.0,
        _ => sizeGB * 3.0
    };
    
    var totalMinutes = baseMinutes + additionalMinutes;
    var maxMinutes = 4 * 60;
    totalMinutes = Math.Min(totalMinutes, maxMinutes);
    
    return TimeSpan.FromMinutes(totalMinutes);
}
```

**Problem**:
- Switch expression obscures simple linear scaling
- Multiple temporary variables
- Magic numbers not clearly explained

**Recommendation**: 
Simplify with constants and clear formula:

```csharp
private static class TimeoutConstants
{
    public const double BaseMinutes = 5.0;
    public const double SmallFileMultiplier = 1.0;   // < 10GB
    public const double MediumFileMultiplier = 2.0;  // 10-50GB
    public const double LargeFileMultiplier = 3.0;   // > 50GB
    public const double MaxMinutes = 240.0; // 4 hours
}

private static TimeSpan CalculateTimeoutForFile(string filePath)
{
    var fileInfo = new FileInfo(filePath);
    var sizeGB = fileInfo.Length / (1024.0 * 1024.0 * 1024.0);
    
    double multiplier = sizeGB switch
    {
        < 10 => TimeoutConstants.SmallFileMultiplier,
        < 50 => TimeoutConstants.MediumFileMultiplier,
        _ => TimeoutConstants.LargeFileMultiplier
    };
    
    double totalMinutes = TimeoutConstants.BaseMinutes + (sizeGB * multiplier);
    totalMinutes = Math.Min(totalMinutes, TimeoutConstants.MaxMinutes);
    
    return TimeSpan.FromMinutes(totalMinutes);
}
```

**Impact**: 
- Named constants explain magic numbers
- Simpler logic flow
- Easier to adjust timeout thresholds

---

## 6. MainViewModel.cs - Track Selection Over-Complication

### Issue: Deeply Nested Track Filtering Logic

**Location**: Lines 743-810 (SelectBestTrack)

```csharp
private SubtitleTrack? SelectBestTrack(IReadOnlyList<SubtitleTrack> tracks)
{
    if (!tracks.Any())
        return null;

    var preferredLanguage = "eng";

    var languageTracks = tracks.Where(t => 
        string.Equals(t.Language, preferredLanguage, StringComparison.OrdinalIgnoreCase)).ToList();

    if (!languageTracks.Any())
        return tracks.First();

    if (State.PreferClosedCaptions)
    {
        var ccTracks = languageTracks.Where(t => t.IsClosedCaption).ToList();
        if (ccTracks.Any())
        {
            var forcedCC = ccTracks.FirstOrDefault(t => t.TrackType == "CC Forced" || t.Forced);
            if (forcedCC != null) return forcedCC;

            var regularCC = ccTracks.FirstOrDefault(t => t.TrackType == "CC");
            if (regularCC != null) return regularCC;

            return GetBestQualityTrack(ccTracks);
        }
    }
    else if (State.PreferForced)
    {
        var forcedTracks = languageTracks.Where(t => t.TrackType == "Forced" || t.TrackType == "CC Forced" || t.Forced).ToList();
        if (forcedTracks.Any())
        {
            return GetBestQualityTrack(forcedTracks);
        }
    }

    // Default selection logic...
}
```

**Problem**:
- Multiple filtering passes over same data
- String comparisons for track types (should be enum)
- Deeply nested if statements
- Multiple early returns make flow hard to follow

**Recommendation**: 
Use a prioritized selection strategy:

```csharp
// After implementing TrackType enum in previous recommendation
private SubtitleTrack? SelectBestTrack(IReadOnlyList<SubtitleTrack> tracks)
{
    if (!tracks.Any())
        return null;

    var preferredLanguage = "eng";
    var languageTracks = tracks
        .Where(t => string.Equals(t.Language, preferredLanguage, StringComparison.OrdinalIgnoreCase))
        .ToList();

    if (!languageTracks.Any())
        return tracks.First();

    // Define selection priority based on user preferences
    var priorities = GetTrackPriorities();
    
    foreach (var priority in priorities)
    {
        var candidates = FilterTracksByPriority(languageTracks, priority);
        if (candidates.Any())
            return GetBestQualityTrack(candidates);
    }

    return GetBestQualityTrack(languageTracks);
}

private IEnumerable<TrackSelectionPriority> GetTrackPriorities()
{
    if (State.PreferClosedCaptions)
    {
        yield return new TrackSelectionPriority(TrackType.ClosedCaptionForced);
        yield return new TrackSelectionPriority(TrackType.ClosedCaption);
    }
    else if (State.PreferForced)
    {
        yield return new TrackSelectionPriority(TrackType.Forced);
        yield return new TrackSelectionPriority(TrackType.ClosedCaptionForced);
    }
    
    // Default priorities
    yield return new TrackSelectionPriority(isSubRip: true);
    yield return new TrackSelectionPriority(TrackType.Full);
}

private record TrackSelectionPriority(TrackType? TrackType = null, bool? isSubRip = null);

private List<SubtitleTrack> FilterTracksByPriority(List<SubtitleTrack> tracks, TrackSelectionPriority priority)
{
    if (priority.TrackType.HasValue)
        return tracks.Where(t => t.Type == priority.TrackType.Value).ToList();
        
    if (priority.isSubRip == true)
        return tracks.Where(t => t.CodecType == CodecType.TextBasedSrt).ToList();
        
    return tracks;
}
```

**Impact**: 
- Strategy pattern makes selection logic explicit
- Single pass through tracks
- Easy to add new selection priorities
- Testable selection logic

---

## 7. BatchFile.cs - Duplicate File Size Formatting

### Issue: Same Logic in Multiple Places

**Location**: Lines 124-143 and similar code in ExtractionState.cs lines 628-643

```csharp
// In BatchFile.cs
if (FileSizeBytes >= gb)
    FormattedFileSize = $"{FileSizeBytes / (double)gb:F1} GB";
else if (FileSizeBytes >= mb)
    FormattedFileSize = $"{FileSizeBytes / (double)mb:F1} MB";
// ...

// In ExtractionState.cs - FormatBytes method
if (bytes == 0) return "0 B";
string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
// ... different implementation of same logic
```

**Problem**:
- Same formatting logic duplicated
- Different implementations might produce inconsistent results
- Should be in a single utility class

**Recommendation**: 
Create a static utility class:

```csharp
public static class FileUtilities
{
    private const long KB = 1024;
    private const long MB = KB * 1024;
    private const long GB = MB * 1024;
    private const long TB = GB * 1024;
    
    public static string FormatFileSize(long bytes)
    {
        if (bytes == 0) return "0 B";
        if (bytes >= TB) return $"{bytes / (double)TB:F1} TB";
        if (bytes >= GB) return $"{bytes / (double)GB:F1} GB";
        if (bytes >= MB) return $"{bytes / (double)MB:F1} MB";
        if (bytes >= KB) return $"{bytes / (double)KB:F1} KB";
        return $"{bytes} bytes";
    }
}
```

Then use it everywhere:
```csharp
FormattedFileSize = FileUtilities.FormatFileSize(FileSizeBytes);
```

**Impact**: 
- DRY principle
- Consistent formatting
- Single place to adjust formatting

---

## 8. MainViewModel.cs - Boolean Helper Methods Could Be Constants or Enums

### Issue: Helper Methods That Are Really Just Codec Classifications

**Location**: Lines 855-874

```csharp
private static bool IsSubRipTrack(string codec)
{
    return codec.Contains("S_TEXT/UTF8") || 
           codec.Contains("SubRip/SRT") || 
           codec.Contains("subrip") ||
           codec.Contains("srt");
}

private static bool IsTextBasedTrack(string codec)
{
    return codec.Contains("S_TEXT") || 
           codec.Contains("ASS") || 
           codec.Contains("SSA") || 
           codec.Contains("VTT");
}

private static int GetCodecPriority(string codec)
{
    if (IsSubRipTrack(codec)) return 100;
    if (IsTextBasedTrack(codec)) return 50;
    if (codec.Contains("PGS") || codec.Contains("S_HDMV/PGS")) return 10;
    return 0;
}
```

**Problem**:
- These are properties of the codec, not operations
- String operations on every check
- Magic priority numbers (100, 50, 10, 0)
- Should be part of SubtitleTrack model, not ViewModel

**Recommendation**: 
Move to SubtitleTrack model using CodecType enum from recommendation #3:

```csharp
// In SubtitleTrack.cs
public int CodecPriority => CodecType switch
{
    CodecType.TextBasedSrt => 100,
    CodecType.TextBasedAss or CodecType.TextBasedWebVtt or CodecType.TextBasedGeneric => 50,
    CodecType.ImageBasedPgs or CodecType.ImageBasedVobSub or CodecType.ImageBasedDvb => 10,
    _ => 0
};

public bool IsSubRip => CodecType == CodecType.TextBasedSrt;
public bool IsTextBased => CodecType is CodecType.TextBasedSrt or CodecType.TextBasedAss or CodecType.TextBasedWebVtt or CodecType.TextBasedGeneric;
```

**Impact**: 
- Properties belong in model, not ViewModel
- Single source of truth (CodecType)
- Testable at model level
- No repeated string operations

---

## 9. BatchFile.cs - Overly Complex UpdateFromFileSystem

### Issue: Complex Method With Nullable Service Parameter

**Location**: Lines 82-150

```csharp
public async void UpdateFromFileSystem(IFileCacheService? fileCacheService)
{
    try
    {
        bool fileExists;
        long fileSize;
        string fileName;

        if (fileCacheService != null)
        {
            // Use cached file operations
            fileExists = await fileCacheService.FileExistsAsync(FilePath);
            fileSize = await fileCacheService.GetFileSizeAsync(FilePath);
            fileName = Path.GetFileName(FilePath);
        }
        else
        {
            // Fallback to direct file operations
            fileExists = File.Exists(FilePath);
            if (!fileExists)
            {
                Status = BatchFileStatus.Error;
                StatusMessage = "File not found";
                return;
            }
            
            var fileInfo = new FileInfo(FilePath);
            fileSize = fileInfo.Length;
            fileName = fileInfo.Name;
        }
        
        // ... rest of method
    }
    catch (Exception)
    {
        Status = BatchFileStatus.Error;
        StatusMessage = "Error reading file";
    }
}
```

**Problem**:
- `async void` is dangerous (can't await, swallows exceptions)
- Nullable parameter with branching logic
- Duplicate code for file existence check
- Should use dependency injection, not optional parameter

**Recommendation**: 
Always require the service and make it return Task:

```csharp
public async Task UpdateFromFileSystemAsync(IFileCacheService fileCacheService)
{
    if (fileCacheService == null)
        throw new ArgumentNullException(nameof(fileCacheService));
        
    try
    {
        var fileExists = await fileCacheService.FileExistsAsync(FilePath);
        if (!fileExists)
        {
            Status = BatchFileStatus.Error;
            StatusMessage = "File not found";
            return;
        }

        FileSizeBytes = await fileCacheService.GetFileSizeAsync(FilePath);
        FileName = Path.GetFileName(FilePath);
        FormattedFileSize = FileUtilities.FormatFileSize(FileSizeBytes);
    }
    catch (Exception)
    {
        Status = BatchFileStatus.Error;
        StatusMessage = "Error reading file";
    }
}
```

**Impact**: 
- Proper async/await pattern
- No branching on nullable parameter
- Cleaner code
- Better testability

---

## 10. ExtractionState.cs - Overly Verbose Property Change Notifications

### Issue: Manual OnPropertyChanged Calls That Could Be Automatic

**Location**: Multiple partial methods like lines 221-231

```csharp
partial void OnMkvPathChanged(string? value)
{
    OnPropertyChanged(nameof(CanProbe));
    OnPropertyChanged(nameof(CanExtract));
}

partial void OnIsBusyChanged(bool value)
{
    OnPropertyChanged(nameof(CanProbe));
    OnPropertyChanged(nameof(CanExtract));
    OnPropertyChanged(nameof(CanProcessBatch));
}

partial void OnSelectedTrackChanged(SubtitleTrack? value)
{
    OnPropertyChanged(nameof(CanExtract));
}
```

**Problem**:
- Manual property change notifications are error-prone
- If you add a new computed property that depends on IsBusy, you must remember to update this method
- No compile-time safety
- Verbose and repetitive

**Recommendation**: 
Use `[NotifyPropertyChangedFor]` attribute from CommunityToolkit.Mvvm:

```csharp
[ObservableProperty]
[NotifyPropertyChangedFor(nameof(CanProbe))]
[NotifyPropertyChangedFor(nameof(CanExtract))]
private string? _mkvPath;

[ObservableProperty]
[NotifyPropertyChangedFor(nameof(CanProbe))]
[NotifyPropertyChangedFor(nameof(CanExtract))]
[NotifyPropertyChangedFor(nameof(CanProcessBatch))]
private bool _isBusy;

[ObservableProperty]
[NotifyPropertyChangedFor(nameof(CanExtract))]
private SubtitleTrack? _selectedTrack;
```

Then remove all the partial methods for property change notifications.

**Impact**: 
- Declarative instead of imperative
- Compile-time safety
- Less boilerplate code
- Easier to maintain

---

## Summary of Recommendations

### High Priority (Significant Simplification):

1. **SubtitleTrack.cs**: Add `CodecType` enum to eliminate repeated string operations (Recommendation #3)
2. **MkvToolService.cs**: Use `TrackType` enum instead of strings (Recommendation #4)
3. **BatchFile.cs**: Fix `async void` and require service parameter (Recommendation #9)
4. **FileUtilities**: Create shared utility for file size formatting (Recommendation #7)

### Medium Priority (Moderate Simplification):

5. **ExtractionState.cs**: Use `[NotifyPropertyChangedFor]` attributes (Recommendation #10)
6. **ExtractionState.cs**: Remove redundant proxy properties (Recommendation #1)
7. **MainViewModel.cs**: Move codec helper methods to SubtitleTrack model (Recommendation #8)

### Low Priority (Nice to Have):

8. **ExtractionState.cs**: Simplify network warning logic (Recommendation #2)
9. **MkvToolService.cs**: Simplify timeout calculation (Recommendation #5)
10. **MainViewModel.cs**: Refactor track selection strategy (Recommendation #6)

---

## General Patterns Observed

### Good Practices Already in Use:
✅ Computed properties for UI state (`CanProbe`, `CanExtract`)  
✅ Helper methods for readability (`IsSubRipTrack`, `IsTextBasedTrack`)  
✅ Separation of concerns (State, ViewModel, Services)  
✅ Use of CommunityToolkit.Mvvm for MVVM boilerplate  

### Areas for Improvement:
❌ Enums vs. strings for type-safe categorization  
❌ Repeated string operations on codecs  
❌ `async void` instead of `async Task`  
❌ Manual property change notifications instead of attributes  
❌ Nullable optional parameters instead of required dependencies  
❌ Duplicated utility code (file size formatting)  

---

## Implementation Order

If you want to tackle these incrementally:

**Phase 1** (Foundation - Do First):
1. Create `CodecType` enum in SubtitleTrack
2. Create `TrackType` enum in Models
3. Create `FileUtilities` static class
4. Fix `async void` in BatchFile

**Phase 2** (Cleanup - Quick Wins):
5. Add `[NotifyPropertyChangedFor]` attributes
6. Remove proxy properties in ExtractionState
7. Move codec helper methods to SubtitleTrack

**Phase 3** (Refactoring - More Involved):
8. Refactor track selection logic
9. Simplify network warning logic
10. Simplify timeout calculation


