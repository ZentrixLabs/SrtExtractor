# Message Windows Bugfix Summary

## Issue
When probing a video file with no subtitles, users were seeing "extraction complete" message instead of the "no subtitles found" message. The message display logic was overcomplicated and not properly controlled by state booleans.

## Root Causes

1. **Hardcoded Visibility**: The "Extraction Complete" message border in `MainWindow.xaml` had `Visibility="Collapsed"` hardcoded instead of being bound to the state property.

2. **Conflicting Messages**: No mechanism to ensure `ShowExtractionSuccess` and `ShowNoTracksError` were mutually exclusive.

3. **Toast Notification Confusion**: A toast notification was being shown for extraction success, which could persist or confuse users when combined with in-app messages.

## Solution

### 1. Fixed XAML Binding (MainWindow.xaml)
**Changed:**
```xml
<!-- Before: Hardcoded to Collapsed -->
<Border Visibility="Collapsed">

<!-- After: Bound to state property -->
<Border Visibility="{Binding State.ShowExtractionSuccess, Converter={StaticResource BoolToVisibilityConverter}}">
```

### 2. Added State Property Handlers (ExtractionState.cs)
Added mutual exclusion logic to ensure only one message shows at a time:

```csharp
partial void OnShowExtractionSuccessChanged(bool value)
{
    // When showing extraction success, hide the no tracks error message
    if (value)
    {
        ShowNoTracksError = false;
    }
    OnPropertyChanged(nameof(ShowExtractionSuccessMessage));
}

partial void OnShowNoTracksErrorChanged(bool value)
{
    // When showing no tracks error, hide the extraction success message
    if (value)
    {
        ShowExtractionSuccess = false;
    }
    OnPropertyChanged(nameof(ShowNoTracksWarning));
}
```

### 3. Removed Conflicting Toast (MainViewModel.cs)
Removed the toast notification for extraction success since we now have a proper in-app message:

```csharp
// Before:
_notificationService.ShowSuccess($"Subtitles extracted successfully!\n\nOutput: {outputPath}", "Extraction Complete");

// After:
// Note: Success message is now shown in-app via State.ShowExtractionSuccess
// Toast notification removed to prevent confusion with other messages
```

## Message Display Logic

Now the message display is controlled by **two simple boolean state properties**:

| State Property          | When True                          | Message Shown                |
|------------------------|-----------------------------------|------------------------------|
| `ShowExtractionSuccess` | Extraction completes successfully | "Extraction Complete!" ✅     |
| `ShowNoTracksError`     | Probe finds no subtitle tracks    | "No Subtitle Tracks Found" ⚠ |

These properties are **mutually exclusive** - setting one to `true` automatically sets the other to `false`.

## State Transitions

### Probe Flow
1. User clicks "Probe Tracks"
2. `ProbeTracksAsync` sets: `ShowExtractionSuccess = false`, `ShowNoTracksError = false`
3. After probing:
   - If tracks found: Both remain `false`, track list displays
   - If no tracks: `ShowNoTracksError = true` (automatically sets `ShowExtractionSuccess = false`)

### Extraction Flow
1. User clicks "Extract to SRT"
2. Extraction completes successfully
3. `ShowExtractionSuccess = true` (automatically sets `ShowNoTracksError = false`)
4. Success message displays with output path

### Pick New File Flow
1. User picks a new file
2. Both properties are cleared: `ShowExtractionSuccess = false`, `ShowNoTracksError = false`
3. UI ready for new operation

## Benefits

✅ **Simple**: Two boolean states control all message visibility
✅ **Clear**: Messages are mutually exclusive - no confusion
✅ **Reliable**: State-driven UI updates, no timing issues
✅ **Consistent**: Same pattern used throughout the app

## Testing

Test scenarios:
1. ✅ Probe video with subtitles → No message, shows track list
2. ✅ Probe video without subtitles → Shows "No Subtitle Tracks Found"
3. ✅ Extract subtitles successfully → Shows "Extraction Complete!"
4. ✅ Pick new file after success → Clears success message
5. ✅ Probe after successful extraction → Success message clears, shows appropriate result

## Files Modified

- `SrtExtractor/Views/MainWindow.xaml` - Fixed visibility binding
- `SrtExtractor/State/ExtractionState.cs` - Added mutual exclusion logic
- `SrtExtractor/ViewModels/MainViewModel.cs` - Removed conflicting toast

## Related Issues

This fix aligns with the UX improvement goals:
- Clear, unambiguous user feedback
- State-driven UI (no complex timing logic)
- Consistent message display patterns

---

**Status**: ✅ Fixed
**Date**: 2025-10-10

