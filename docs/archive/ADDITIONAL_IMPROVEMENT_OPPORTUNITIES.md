# Additional Improvement Opportunities

**Date**: October 10, 2025  
**Status**: Analysis Phase  
**Context**: Post-implementation of high priority simplifications

After completing the high priority code simplifications, here are additional areas identified for potential improvement.

---

## 🔴 Critical Issues

### 1. ExtractSubtitlesAsync - 165 Line God Method

**Location**: `MainViewModel.cs` lines 318-483

**Problem**: Single method with 165 lines handling multiple codec types with nested conditionals.

**Current Structure**:
```csharp
private async Task ExtractSubtitlesAsync(...)
{
    // Setup (15 lines)
    
    try
    {
        // Common setup (10 lines)
        
        if (fileExtension == ".mp4")
        {
            // MP4 handling (7 lines)
        }
        else if (State.SelectedTrack.Codec.Contains("S_TEXT/UTF8") || State.SelectedTrack.Codec.Contains("SubRip/SRT"))
        {
            // Text extraction (13 lines)
        }
        else if (State.SelectedTrack.Codec.Contains("PGS") || State.SelectedTrack.Codec.Contains("S_HDMV/PGS"))
        {
            // PGS extraction + OCR (30 lines)
        }
        else if (State.SelectedTrack.Codec.Contains("VobSub") || State.SelectedTrack.Codec.Contains("S_VOBSUB"))
        {
            // VobSub guidance (22 lines)
        }
        else
        {
            throw new NotSupportedException(...);
        }
        
        // Common completion (12 lines)
    }
    catch (OperationCanceledException) { ... }
    catch (Exception ex) { ... }
    finally { ... }
}
```

**Issue**: Still using string Contains checks instead of the new CodecType property we just added!

**Recommendation**: 
Use Strategy Pattern with the new CodecType enum:

```csharp
private async Task ExtractSubtitlesAsync(CancellationToken? cancellationToken = null)
{
    if (State.SelectedTrack == null || string.IsNullOrEmpty(State.MkvPath))
        return;

    var cancellation = PrepareExtractionCancellation(cancellationToken);

    try
    {
        State.IsBusy = true;
        var fileInfo = new FileInfo(State.MkvPath);
        State.StartProcessingWithProgress("Preparing extraction...", fileInfo.Length);
        
        var outputPath = State.GenerateOutputFilename(State.MkvPath, State.SelectedTrack);
        
        // Use strategy pattern based on codec type
        await ExecuteExtractionStrategy(outputPath, cancellation);
        await CompleteExtraction(outputPath);
    }
    catch (OperationCanceledException)
    {
        await HandleExtractionCancellation();
    }
    catch (Exception ex)
    {
        await HandleExtractionError(ex);
    }
    finally
    {
        CleanupExtraction();
    }
}

private async Task ExecuteExtractionStrategy(string outputPath, CancellationToken ct)
{
    var fileExtension = Path.GetExtension(State.MkvPath).ToLowerInvariant();
    
    if (fileExtension == ".mp4")
    {
        await ExtractFromMp4Async(outputPath, ct);
        return;
    }
    
    // Use the new CodecType property instead of string checks
    switch (State.SelectedTrack.CodecType)
    {
        case SubtitleCodecType.TextBasedSrt:
        case SubtitleCodecType.TextBasedAss:
        case SubtitleCodecType.TextBasedWebVtt:
        case SubtitleCodecType.TextBasedGeneric:
            await ExtractTextSubtitlesAsync(outputPath, ct);
            break;
            
        case SubtitleCodecType.ImageBasedPgs:
            await ExtractPgsSubtitlesAsync(outputPath, ct);
            break;
            
        case SubtitleCodecType.ImageBasedVobSub:
            ShowVobSubGuidance();
            throw new InvalidOperationException("VobSub requires Subtitle Edit");
            
        default:
            throw new NotSupportedException($"Unsupported codec: {State.SelectedTrack.Codec}");
    }
}

private async Task ExtractTextSubtitlesAsync(string outputPath, CancellationToken ct)
{
    State.UpdateProcessingMessage("Extracting text subtitles...");
    State.UpdateProgress(State.TotalBytes * 50 / 100, "Extracting text subtitles");
    
    await _mkvToolService.ExtractTextAsync(State.MkvPath, State.SelectedTrack.ExtractionId, outputPath, ct);
    
    State.UpdateProgress(State.TotalBytes * 80 / 100, "Text extraction completed");
    State.UpdateProcessingMessage("Text extraction completed!");
    State.AddLogMessage($"Text subtitles extracted to: {outputPath}");
    
    await ApplyMultiPassCorrectionAsync(outputPath, ct).ConfigureAwait(false);
}

private async Task ExtractPgsSubtitlesAsync(string outputPath, CancellationToken ct)
{
    var tempSupPath = Path.ChangeExtension(outputPath, ".sup");
    
    // PGS extraction
    State.UpdateProcessingMessage("Extracting PGS subtitles...");
    State.UpdateProgress(State.TotalBytes * 30 / 100, "Extracting PGS subtitles");
    await _mkvToolService.ExtractPgsAsync(State.MkvPath, State.SelectedTrack.ExtractionId, tempSupPath, ct);
    
    // OCR conversion
    State.UpdateProcessingMessage("Starting OCR conversion...");
    State.UpdateProgress(State.TotalBytes * 50 / 100, "Starting OCR");
    await _ocrService.OcrSupToSrtAsync(tempSupPath, outputPath, State.OcrLanguage, cancellationToken: ct);
    
    State.UpdateProgress(State.TotalBytes * 90 / 100, "OCR completed");
    
    // Correction
    await ApplyMultiPassCorrectionAsync(outputPath, ct).ConfigureAwait(false);
    
    // Cleanup
    try { File.Delete(tempSupPath); } catch { }
}
```

**Benefits**:
- 165 lines → ~100 lines (40% reduction)
- Uses new CodecType enum (eliminates string Contains)
- Each strategy is testable independently
- Easier to add new codec support
- Clear separation of concerns

---

## 🟡 Medium Priority Issues

### 2. Repeated State Clearing Pattern

**Locations**: 
- `MainViewModel.cs` lines 173-180 (PickMkvAsync)
- `MainViewModel.cs` lines 206-208 (ProbeTracksAsync)
- `MainViewModel.cs` lines 1769-1771 (OpenRecentFile)

**Problem**: Same 4-line pattern repeated 3+ times:

```csharp
State.ShowNoTracksError = false;
State.ShowExtractionSuccess = false;
State.LastExtractionOutputPath = "";
// Sometimes also includes:
State.Tracks.Clear();
State.SelectedTrack = null;
State.HasProbedFile = false;
```

**Recommendation**: Create helper method in ExtractionState:

```csharp
// In ExtractionState.cs
public void ClearFileState()
{
    ShowNoTracksError = false;
    ShowExtractionSuccess = false;
    LastExtractionOutputPath = "";
    Tracks.Clear();
    SelectedTrack = null;
    HasProbedFile = false;
}
```

**Usage**:
```csharp
// Instead of 6 lines
State.ClearFileState();
```

**Impact**: 
- Reduces ~18 lines of duplicate code
- Single source of truth for state reset
- Easier to add new state properties

---

### 3. ExtractionState - Manual Property Change Notifications

**Location**: `ExtractionState.cs` - 13 partial void methods

**Problem**: Manual `OnPropertyChanged()` calls that could be declarative:

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
```

**Recommendation**: Use `[NotifyPropertyChangedFor]` attributes:

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
```

Then delete all 13 partial void methods.

**Impact**: 
- Declarative vs imperative
- ~50 lines of boilerplate removed
- Compile-time safety
- Self-documenting dependencies

---

### 4. Tool Name Checking Pattern

**Location**: `ToolDetectionService.cs` multiple locations

**Problem**: Repeated pattern:
```csharp
if (toolName.Contains("ffmpeg") || toolName.Contains("ffprobe"))
    // ...
else if (toolName.Contains("seconv"))
    // ...
else if (toolName.Contains("mkvmerge") || toolName.Contains("mkvextract"))
    // ...
```

**Recommendation**: Use enum or tool registry pattern:

```csharp
private enum KnownTool
{
    Unknown,
    MkvMerge,
    MkvExtract,
    SubtitleEdit,
    FFmpeg,
    FFprobe
}

private static KnownTool IdentifyTool(string toolPath)
{
    var toolName = Path.GetFileNameWithoutExtension(toolPath).ToLowerInvariant();
    
    if (toolName.Contains("mkvmerge")) return KnownTool.MkvMerge;
    if (toolName.Contains("mkvextract")) return KnownTool.MkvExtract;
    if (toolName.Contains("seconv")) return KnownTool.SubtitleEdit;
    if (toolName.Contains("ffmpeg")) return KnownTool.FFmpeg;
    if (toolName.Contains("ffprobe")) return KnownTool.FFprobe;
    
    return KnownTool.Unknown;
}

private async Task<string?> GetToolVersionAsync(string toolPath)
{
    var tool = IdentifyTool(toolPath);
    
    var versionArgs = tool switch
    {
        KnownTool.FFmpeg or KnownTool.FFprobe => new[] { "-version" },
        KnownTool.SubtitleEdit => new[] { "--version" },
        KnownTool.MkvMerge or KnownTool.MkvExtract => new[] { "--version" },
        _ => new[] { "--version" }
    };
    
    // ... rest of method
}
```

**Impact**:
- Type-safe tool identification
- Single source for tool-specific logic
- Easier to add new tools

---

### 5. Redundant Proxy Properties in ExtractionState

**Location**: `ExtractionState.cs` lines 307-309

**Problem**: Properties that just proxy other properties with no logic:

```csharp
public bool ShowNoTracksWarning => ShowNoTracksError;
public bool ShowExtractionSuccessMessage => ShowExtractionSuccess;
```

**Recommendation**: Remove these and update XAML bindings:

```xml
<!-- Change from: -->
<TextBlock Visibility="{Binding State.ShowNoTracksWarning, Converter={StaticResource BoolToVisibilityConverter}}" />

<!-- To: -->
<TextBlock Visibility="{Binding State.ShowNoTracksError, Converter={StaticResource BoolToVisibilityConverter}}" />
```

**Impact**: 
- 2-3 properties removed
- Less indirection
- Clearer bindings

---

### 6. Magic Progress Percentages

**Location**: `MainViewModel.cs` lines 366, 368, 382, 388, 390

**Problem**: Magic numbers for progress tracking:

```csharp
State.UpdateProgress(State.TotalBytes * 50 / 100, "Extracting text subtitles");
State.UpdateProgress(State.TotalBytes * 80 / 100, "Text extraction completed");
State.UpdateProgress(State.TotalBytes * 30 / 100, "Extracting PGS subtitles");
State.UpdateProgress(State.TotalBytes * 50 / 100, "Starting OCR conversion");
State.UpdateProgress(State.TotalBytes * 90 / 100, "OCR conversion completed");
```

**Recommendation**: Use named constants:

```csharp
private static class ProgressMilestones
{
    public const double TextExtractionStart = 0.50;
    public const double TextExtractionComplete = 0.80;
    public const double PgsExtractionStart = 0.30;
    public const double OcrStart = 0.50;
    public const double OcrComplete = 0.90;
    public const double Complete = 1.0;
}

// Usage
State.UpdateProgress((long)(State.TotalBytes * ProgressMilestones.TextExtractionStart), "Extracting text subtitles");
```

**Impact**:
- Self-documenting progress stages
- Easy to adjust timing
- Centralized progress configuration

---

### 7. ToolDetectionService - Overly Verbose Logging

**Location**: `ToolDetectionService.cs` lines 158-172

**Problem**: Logging every path check creates noise:

```csharp
foreach (var path in commonPaths)
{
    var fullPath = Path.Combine(path, toolName);
    _loggingService.LogInfo($"Checking path: {fullPath} (exists: {File.Exists(fullPath)})");
    if (File.Exists(fullPath))
    {
        _loggingService.LogInfo($"Found {toolName} at {fullPath}");
        return fullPath;
    }
}
```

**Recommendation**: Only log successful finds and summary:

```csharp
_loggingService.LogInfo($"Searching for {toolName} in {commonPaths.Length} paths");

foreach (var path in commonPaths)
{
    var fullPath = Path.Combine(path, toolName);
    if (File.Exists(fullPath))
    {
        _loggingService.LogInfo($"Found {toolName} at {fullPath}");
        return fullPath;
    }
}

_loggingService.LogInfo($"{toolName} not found in common paths, checking PATH");
```

**Impact**:
- Cleaner logs (remove ~50% of log entries during startup)
- Easier to debug actual issues
- Better signal-to-noise ratio

---

## 🟢 Low Priority / Nice to Have

### 8. Repeated Codec String Checks

**Location**: `MainViewModel.cs` lines 360, 376, 409

**Problem**: STILL using string Contains after we added CodecType enum:

```csharp
else if (State.SelectedTrack.Codec.Contains("S_TEXT/UTF8") || State.SelectedTrack.Codec.Contains("SubRip/SRT"))
{
    // Text extraction
}
else if (State.SelectedTrack.Codec.Contains("PGS") || State.SelectedTrack.Codec.Contains("S_HDMV/PGS"))
{
    // PGS extraction
}
else if (State.SelectedTrack.Codec.Contains("VobSub") || State.SelectedTrack.Codec.Contains("S_VOBSUB"))
{
    // VobSub
}
```

**Should be**:
```csharp
switch (State.SelectedTrack.CodecType)
{
    case SubtitleCodecType.TextBasedSrt:
    case SubtitleCodecType.TextBasedAss:
    case SubtitleCodecType.TextBasedWebVtt:
    case SubtitleCodecType.TextBasedGeneric:
        await ExtractTextSubtitlesAsync(outputPath, ct);
        break;
        
    case SubtitleCodecType.ImageBasedPgs:
        await ExtractPgsSubtitlesAsync(outputPath, ct);
        break;
        
    case SubtitleCodecType.ImageBasedVobSub:
        ShowVobSubGuidance();
        throw new InvalidOperationException("VobSub requires Subtitle Edit");
        
    default:
        throw new NotSupportedException($"Unsupported codec: {State.SelectedTrack.Codec}");
}
```

**Impact**:
- Uses the new enum we created (completes the refactoring)
- Type-safe dispatch
- No repeated string operations

---

### 9. Batch Processing Statistics - Repeated LINQ Queries

**Location**: `ExtractionState.cs` lines 562-566, 579-583

**Problem**: Same LINQ queries executed multiple times:

```csharp
public void UpdateBatchStatistics()
{
    BatchCompletedCount = BatchQueue.Count(f => f.Status == BatchFileStatus.Completed);
    BatchErrorCount = BatchQueue.Count(f => f.Status == BatchFileStatus.Error);
    BatchPendingCount = BatchQueue.Count(f => f.Status == BatchFileStatus.Pending);
    // ...
}

public void UpdateBatchStatisticsFast()
{
    BatchCompletedCount = BatchQueue.Count(f => f.Status == BatchFileStatus.Completed);
    BatchErrorCount = BatchQueue.Count(f => f.Status == BatchFileStatus.Error);
    BatchPendingCount = BatchQueue.Count(f => f.Status == BatchFileStatus.Pending);
    // ...
}
```

**Recommendation**: Single iteration with grouping:

```csharp
public void UpdateBatchStatistics()
{
    var statusCounts = BatchQueue
        .GroupBy(f => f.Status)
        .ToDictionary(g => g.Key, g => g.Count());
    
    BatchCompletedCount = statusCounts.GetValueOrDefault(BatchFileStatus.Completed, 0);
    BatchErrorCount = statusCounts.GetValueOrDefault(BatchFileStatus.Error, 0);
    BatchPendingCount = statusCounts.GetValueOrDefault(BatchFileStatus.Pending, 0);
    
    // Notify properties...
}
```

**Impact**:
- Single pass through collection instead of 3
- O(n) instead of O(3n)
- Better performance for large batches

---

### 10. Cancellation Token Handling Complexity

**Location**: `MainViewModel.cs` lines 323-334

**Problem**: Complex nullable cancellation token logic:

```csharp
// Use provided cancellation token or create a new one
if (cancellationToken.HasValue)
{
    // Use the provided cancellation token (from batch processing)
    _extractionCancellationTokenSource = null;
}
else
{
    // Create cancellation token source for single file extraction
    _extractionCancellationTokenSource = new CancellationTokenSource();
    cancellationToken = _extractionCancellationTokenSource.Token;
}
```

**Recommendation**: Simplify with null coalescing:

```csharp
// Create cancellation token source for single file extraction only
_extractionCancellationTokenSource = cancellationToken.HasValue ? null : new CancellationTokenSource();
var effectiveCancellationToken = cancellationToken ?? _extractionCancellationTokenSource!.Token;

// Then use effectiveCancellationToken throughout
```

Or better yet, just use non-nullable parameter with default:

```csharp
private async Task ExtractSubtitlesAsync(CancellationToken cancellationToken = default)
{
    // If default token, create our own source for cancellation
    if (cancellationToken == default)
    {
        _extractionCancellationTokenSource = new CancellationTokenSource();
        cancellationToken = _extractionCancellationTokenSource.Token;
    }
    
    // Rest of method uses cancellationToken directly
}
```

**Impact**:
- Simpler logic
- No nullable complexity
- Clearer intent

---

### 11. Repeated Try-Catch-Finally in Command Methods

**Location**: Multiple command methods throughout MainViewModel

**Pattern**:
```csharp
private async Task SomeCommandAsync()
{
    try
    {
        State.IsBusy = true;
        // ... command logic ...
    }
    catch (Exception ex)
    {
        _loggingService.LogError("Failed to ...", ex);
        State.AddLogMessage($"Error: {ex.Message}");
    }
    finally
    {
        State.IsBusy = false;
    }
}
```

**Recommendation**: Command wrapper/decorator:

```csharp
private async Task ExecuteCommandAsync(Func<Task> command, string commandName)
{
    try
    {
        State.IsBusy = true;
        await command();
    }
    catch (Exception ex)
    {
        _loggingService.LogError($"Failed to {commandName}", ex);
        State.AddLogMessage($"Error in {commandName}: {ex.Message}");
        _notificationService.ShowError($"Failed to {commandName}:\n{ex.Message}", $"{commandName} Error");
    }
    finally
    {
        State.IsBusy = false;
    }
}

// Usage
private Task SomeCommandAsync() => ExecuteCommandAsync(async () =>
{
    // Just the command logic
}, "some command");
```

**Impact**:
- DRY principle
- Consistent error handling
- Reduced boilerplate

---

### 12. File Extension Checking - Not Using FileUtilities

**Location**: Multiple places

**Problem**: Repeated pattern:
```csharp
var fileExtension = Path.GetExtension(State.MkvPath).ToLowerInvariant();
if (fileExtension == ".mp4")
    // ...
else
    // ...
```

**Recommendation**: Add to FileUtilities:

```csharp
public static class FileUtilities
{
    public static bool IsMp4File(string filePath) => 
        Path.GetExtension(filePath).Equals(".mp4", StringComparison.OrdinalIgnoreCase);
        
    public static bool IsMkvFile(string filePath) => 
        Path.GetExtension(filePath).Equals(".mkv", StringComparison.OrdinalIgnoreCase);
        
    public static bool IsVideoFile(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return ext.Equals(".mp4", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".mkv", StringComparison.OrdinalIgnoreCase);
    }
}
```

**Impact**:
- Centralized file type checking
- Consistent comparison (OrdinalIgnoreCase)
- Easy to add new video formats

---

## Summary Priority List

### Quick Wins (High Impact, Low Effort):

1. **Repeated State Clearing** → Extract to `ClearFileState()` method (30 min)
2. **Codec String Checks** → Use `CodecType` switch instead of Contains (15 min)
3. **Redundant Proxy Properties** → Remove and update bindings (20 min)

### Medium Effort (High Impact):

4. **ExtractSubtitlesAsync** → Refactor to strategy pattern (2-3 hours)
5. **Property Change Notifications** → Use `[NotifyPropertyChangedFor]` attributes (1 hour)

### Low Effort (Nice to Have):

6. **Tool Name Checking** → Use enum pattern (1 hour)
7. **Magic Progress Numbers** → Extract to constants (30 min)
8. **Batch Statistics** → Single-pass grouping (30 min)
9. **File Extension Helpers** → Add to FileUtilities (15 min)

---

## Recommended Next Steps

If implementing incrementally:

**Phase 1** (Quick Wins - 1 hour):
1. Create `ClearFileState()` helper
2. Fix codec string checks to use CodecType
3. Remove redundant proxy properties

**Phase 2** (Medium Effort - 3-4 hours):
4. Refactor ExtractSubtitlesAsync with strategy pattern
5. Add [NotifyPropertyChangedFor] attributes

**Phase 3** (Polish - 2 hours):
6. Tool name enum pattern
7. Progress constants
8. Batch statistics optimization

---

## Files That Need Attention

1. `SrtExtractor/ViewModels/MainViewModel.cs` - God method, state clearing, codec checks
2. `SrtExtractor/State/ExtractionState.cs` - Property notifications, proxy properties
3. `SrtExtractor/Services/Implementations/ToolDetectionService.cs` - Tool name pattern, verbose logging
4. XAML files - Update bindings if proxy properties removed

---

Would you like to proceed with any of these improvements?

