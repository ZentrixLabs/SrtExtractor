# UX Improvement Plan - SrtExtractor
**Version:** 1.0  
**Date:** October 9, 2025  
**Status:** Ready for Implementation  
**Target Release:** v2.0

---

## 📊 Executive Summary

Based on comprehensive UX review, SrtExtractor scores **7.5/10** in usability. The application has excellent functionality but suffers from interface complexity and information overload. This plan outlines actionable improvements prioritized by impact and effort.

### Key Findings
- ✅ **Strengths:** Solid feature set, good architecture, professional design
- ⚠️ **Weaknesses:** Overwhelming UI, unclear mode switching, technical jargon
- 🎯 **Goal:** Simplify while maintaining power-user features (progressive disclosure)

### Success Metrics
- Reduce time-to-first-extraction from ~2 minutes to ~30 seconds for new users
- Increase batch mode adoption from unknown baseline to 40% of users
- Reduce support requests about "which track to select" by 60%

---

## 🚀 Phase 1: Critical Issues (Must Have for v2.0)

These changes have the highest impact on user experience and should be completed before release.

### 1.1 Simplify Main Window Layout ⭐⭐⭐
**Priority:** CRITICAL  
**Effort:** HIGH (3-5 days)  
**Impact:** Reduces cognitive overload by 60%

#### Problem
Main window tries to do everything in one view (1200+ lines of XAML). Users are overwhelmed by options and don't know where to look first.

#### Solution: Tab-Based Interface
Split the monolithic UI into logical sections using a modern tab control.

#### Tasks
- [ ] **Create TabControl structure in MainWindow.xaml** (4 hours)
  - Tab 1: "Extract" - Single file extraction workflow
  - Tab 2: "Batch" - Batch processing interface  
  - Tab 3: "History" - Recent files and activity log
  - Tab 4: "Tools" - Advanced tools (SRT correction, VobSub analyzer)

- [ ] **Refactor "Extract" tab** (6 hours)
  - Move file selection to top
  - Track list in center (simplified to 5 columns)
  - Extract button prominent at bottom right
  - Hide settings in collapsible panel
  - Remove log from this tab

- [ ] **Refactor "Batch" tab** (4 hours)
  - Move batch queue to main area (full width)
  - Settings panel at top
  - Large "Process Batch" button at bottom
  - Progress indicator integrated

- [ ] **Create "History" tab** (3 hours)
  - Recent files list (last 20 files)
  - Quick re-process button per item
  - Full log viewer with filtering
  - Export log functionality

- [ ] **Create "Tools" tab** (2 hours)
  - SRT Correction launcher
  - Batch SRT Correction launcher
  - VobSub Track Analyzer launcher
  - Settings button (opens SettingsWindow)

#### Files to Modify
- `SrtExtractor/Views/MainWindow.xaml` (Lines 109-1193)
- `SrtExtractor/Views/MainWindow.xaml.cs`
- `SrtExtractor/ViewModels/MainViewModel.cs`

#### Implementation Notes
```xaml
<TabControl>
  <TabItem Header="Extract">
    <!-- Simplified single-file UI -->
  </TabItem>
  <TabItem Header="Batch">
    <!-- Batch processing UI -->
  </TabItem>
  <TabItem Header="History">
    <!-- Recent files + full log -->
  </TabItem>
  <TabItem Header="Tools">
    <!-- Advanced tools -->
  </TabItem>
</TabControl>
```

---

### 1.2 Make Mode Selection Obvious ⭐⭐⭐
**Priority:** CRITICAL  
**Effort:** LOW (4-6 hours)  
**Impact:** Eliminates confusion about dual modes

#### Problem
Users don't realize there are two modes. Elements appear/disappear based on a checkbox that's easy to miss (Line 308).

#### Solution: Remove Mode Toggle Entirely
Replace dual-mode UI with separate tabs (covered in 1.1). This naturally separates single-file from batch operations.

#### Tasks
- [ ] **Remove IsBatchMode property** from ExtractionState.cs (1 hour)
- [ ] **Remove mode-switching logic** from MainViewModel.cs (1 hour)
- [ ] **Remove visibility converters** for ShowBatchMode/ShowSingleFileMode (30 min)
- [ ] **Update Settings** to remove "Enable Batch Mode" checkbox (30 min)
- [ ] **Add tooltip to Batch tab** explaining batch mode (15 min)

#### Files to Modify
- `SrtExtractor/State/ExtractionState.cs` (Lines 86-169)
- `SrtExtractor/ViewModels/MainViewModel.cs` (Lines 1703-1760)
- `SrtExtractor/Views/MainWindow.xaml` (Lines 308-322, 440-457, 461-485)
- `SrtExtractor/Views/SettingsWindow.xaml` (Lines 331-341)

#### Implementation Notes
- Simplifies state management
- Removes ~200 lines of visibility binding code
- Makes UI predictable

---

### 1.3 Humanize Track Information ⭐⭐⭐
**Priority:** CRITICAL  
**Effort:** MEDIUM (1-2 days)  
**Impact:** Makes track selection obvious to non-technical users

#### Problem
DataGrid shows technical details: "S_HDMV/PGS", "S_TEXT/UTF8", cryptic codec names that mean nothing to users.

#### Solution: Human-Friendly Labels
Replace technical terms with user-friendly descriptions and visual indicators.

#### Tasks
- [ ] **Add TrackFormatDisplay computed property** to SubtitleTrack model (2 hours)
  ```csharp
  public string FormatDisplay => Codec switch {
    var c when c.Contains("S_HDMV/PGS") => "Image-based (OCR required)",
    var c when c.Contains("S_TEXT/UTF8") => "Text-based (fast)",
    var c when c.Contains("SubRip") => "SRT Text (fast)",
    _ => "Other format"
  };
  ```

- [ ] **Add TrackFormatIcon property** (1 hour)
  ```csharp
  public string FormatIcon => Codec switch {
    var c when c.Contains("S_HDMV/PGS") => "🐢", // Slow
    var c when c.Contains("S_TEXT") => "⚡", // Fast
    _ => "📄"
  };
  ```

- [ ] **Simplify DataGrid columns** (3 hours)
  - Remove: ID, Codec, Bitrate, Frames, Duration (technical details)
  - Keep: Language, Format (humanized), Type, Recommended, Name
  - Add: Speed indicator column (icon + text: "Fast" or "Slow OCR")

- [ ] **Create expandable row details** for technical info (4 hours)
  - Add DataGrid.RowDetailsTemplate
  - Show ID, Codec, Bitrate, etc. when row is expanded
  - Add "Show Technical Details" toggle above grid

- [ ] **Update tooltips** to explain what each column means (1 hour)

#### Files to Modify
- `SrtExtractor/Models/SubtitleTrack.cs`
- `SrtExtractor/Views/MainWindow.xaml` (Lines 622-727)
- `SrtExtractor/Converters/` (create new converter if needed)

#### Visual Mockup
```
[Track List]
Language | Format              | Type    | Recommended | Name
---------|---------------------|---------|-------------|-----
English  | ⚡ Text (fast)      | Full    | ⭐         | English Subtitles
English  | 🐢 Image (OCR)     | Forced  |            | Forced Subs
Spanish  | ⚡ Text (fast)      | Full    |            | Spanish
```

---

### 1.4 Reduce Log Visibility ⭐⭐⭐
**Priority:** CRITICAL  
**Effort:** LOW (4 hours)  
**Impact:** Reclaims 200px of vertical space, reduces visual noise

#### Problem
Log TextBox takes 150-200px of screen space and shows every log message. Most users never read it, but it's always visible.

#### Solution: Minimal Status Bar + Expandable Log
Show only critical messages in main view. Full log available in History tab.

#### Tasks
- [ ] **Remove log from Extract/Batch tabs** (30 min)
  - Log display should only exist in History tab

- [ ] **Create compact status bar** at bottom of window (2 hours)
  - Show last status message only
  - Icon indicator (✅ ℹ️ ⚠️ ❌)
  - "View Full Log" button that switches to History tab
  - Example: `✅ Extraction completed successfully • View Full Log`

- [ ] **Add log level filtering** to History tab (3 hours)
  - Dropdown: All | Errors | Warnings | Info
  - Search box for text filtering
  - Export log button
  - Clear log button

- [ ] **Implement smart notifications** (2 hours)
  - Show toast for errors/warnings instead of relying on log
  - Only log detailed technical info
  - User-facing messages go to status bar

#### Files to Modify
- `SrtExtractor/Views/MainWindow.xaml` (Lines 732-799)
- `SrtExtractor/State/ExtractionState.cs` (Add log filtering)
- Create new `StatusBar.xaml` control
- Update `MainViewModel.cs` to use status bar

#### Implementation Notes
```xaml
<!-- Status Bar at bottom of MainWindow -->
<Border Background="{StaticResource BackgroundAltBrush}" 
        Height="32" 
        DockPanel.Dock="Bottom">
  <Grid>
    <StackPanel Orientation="Horizontal" Margin="10,0">
      <TextBlock FontFamily="{StaticResource IconFontFamily}" 
                 Text="{Binding LastStatusIcon}"/>
      <TextBlock Text="{Binding LastStatusMessage}" 
                 Margin="8,0"/>
    </StackPanel>
    <Button Content="View Full Log" 
            Command="{Binding ShowHistoryTabCommand}"
            HorizontalAlignment="Right"/>
  </Grid>
</Border>
```

---

## ⚡ Phase 2: Major Improvements (Should Have)

These improvements significantly enhance UX but aren't blocking for release.

### 2.1 Better Settings Placement ⭐⭐
**Priority:** HIGH  
**Effort:** MEDIUM (1 day)  
**Impact:** Makes critical settings discoverable

#### Problem
OCR language, file pattern, and preferences are buried in a GroupBox on the right side. Users may extract without knowing these options exist.

#### Solution: Settings Summary + Quick Access
Show active settings near action buttons, with quick access to change them.

#### Tasks
- [ ] **Create settings summary component** (3 hours)
  - Compact display showing: Language, Subtitle preference, Multi-pass mode
  - Example: `⚙️ English • Forced Subs • Standard Correction • Change`
  - Click "Change" opens SettingsWindow

- [ ] **Add quick settings panel** in Extract tab (2 hours)
  - Language dropdown (most common: English, Spanish, French)
  - Subtitle preference radio buttons (Forced/CC)
  - "Advanced Settings..." button for full dialog

- [ ] **Move infrequently-used settings** to SettingsWindow only (1 hour)
  - File naming pattern
  - Multi-pass correction configuration
  - Network file handling

- [ ] **Add settings validation** (2 hours)
  - Warn if OCR language doesn't match subtitle language
  - Suggest settings based on detected track types

#### Files to Modify
- `SrtExtractor/Views/MainWindow.xaml` (Lines 305-433)
- Create new `Views/Controls/SettingsSummary.xaml`
- `SrtExtractor/Views/SettingsWindow.xaml`

---

### 2.2 Consistent Button Hierarchy ⭐⭐
**Priority:** HIGH  
**Effort:** MEDIUM (1 day)  
**Impact:** Makes primary actions obvious

#### Problem
Multiple button styles (Primary, Secondary, Success, Warning, Danger, Accent) without clear hierarchy. Extract button not visually prominent.

#### Solution: Three-Tier Button System
Establish clear visual hierarchy with size, color, and placement.

#### Tasks
- [ ] **Define button hierarchy** (2 hours)
  - **Tier 1 (Primary Action)**: Large, colored, icon + text
    - Extract to SRT, Process Batch
  - **Tier 2 (Secondary Action)**: Medium, neutral, text only
    - Probe Tracks, Pick Video, Clear Log
  - **Tier 3 (Tertiary/Danger)**: Small, outlined/red, minimal
    - Cancel, Remove, Delete

- [ ] **Increase Extract button size** (1 hour)
  - Height: 48px (up from 32px)
  - Width: 200px (up from auto)
  - Font size: 16px (up from 14px)
  - Add rocket icon: 🚀 Extract to SRT

- [ ] **Update button styles** in ButtonStyles.xaml (3 hours)
  - Remove Accent, Success button styles
  - Keep: Primary (blue), Secondary (gray), Danger (red)
  - Add size variants: Large, Medium, Small

- [ ] **Update all buttons** throughout app (2 hours)
  - Review every button usage
  - Apply consistent hierarchy
  - Add icons to primary buttons

#### Files to Modify
- `SrtExtractor/Themes/ButtonStyles.xaml`
- `SrtExtractor/Views/MainWindow.xaml` (all button declarations)
- `SrtExtractor/Views/SettingsWindow.xaml`
- `SrtExtractor/Views/BatchSrtCorrectionWindow.xaml`

#### Visual Example
```
[🚀 Extract to SRT]  ← Large, blue, primary
[Probe Tracks]       ← Medium, gray, secondary  
[Cancel]             ← Small, red outline, danger
```

---

### 2.3 Keyboard Shortcut Discoverability ⭐⭐
**Priority:** HIGH  
**Effort:** LOW (4 hours)  
**Impact:** Improves efficiency for power users

#### Problem
Great keyboard shortcuts exist but are only visible in menu items. Users don't discover them during normal use.

#### Solution: Multi-Layered Discovery
Make shortcuts visible in multiple places.

#### Tasks
- [ ] **Add shortcuts to button tooltips** (1 hour)
  - Example: "Extract selected subtitle track to SRT format (Ctrl+E)"

- [ ] **Show shortcuts in button labels** (1 hour)
  - For primary actions only
  - Example: "Extract to SRT [Ctrl+E]"
  - Use lighter color for shortcut text

- [ ] **Create keyboard shortcuts help** (2 hours)
  - Add Help menu item: "Keyboard Shortcuts (F1)"
  - Create simple dialog with shortcut list
  - Group by category: File, Extract, Batch, Tools

- [ ] **Add shortcuts to welcome window** (already done! ✅)
  - Page 5 shows shortcuts - good!

#### Files to Modify
- `SrtExtractor/Views/MainWindow.xaml` (button tooltips)
- Create new `Views/KeyboardShortcutsWindow.xaml`
- `SrtExtractor/ViewModels/MainViewModel.cs` (add ShowKeyboardShortcutsCommand)

---

### 2.4 Simplify DataGrid Columns ⭐⭐
**Priority:** MEDIUM  
**Effort:** LOW (3 hours)  
**Impact:** Prevents horizontal scrolling, improves clarity

#### Problem
10 columns in subtitle track grid causes horizontal scrolling on smaller screens. Users don't need all information upfront.

#### Solution: Progressive Disclosure
Show essential columns by default, hide technical details.

#### Tasks
- [ ] **Define column presets** (2 hours)
  - **Simple Mode** (default): Language, Format, Recommended, Name (4 columns)
  - **Detailed Mode**: Add Type, Speed indicator (6 columns)
  - **Technical Mode**: Show all 10 columns

- [ ] **Add view switcher** above DataGrid (1 hour)
  - Radio buttons or segmented control
  - "Simple | Detailed | Technical"
  - Save preference to settings

- [ ] **Update default columns** (1 hour)
  - Hide by default: ID, Codec, Bitrate, Frames, Duration
  - Keep visible: Language, Format (humanized), Type, Recommended, Name

#### Files to Modify
- `SrtExtractor/Views/MainWindow.xaml` (Lines 622-727)
- `SrtExtractor/State/ExtractionState.cs` (add TrackGridMode property)

---

## 💡 Phase 3: Polish & Nice-to-Have

These improvements add polish but can be deferred to v2.1 if needed.

### 3.1 Menu Reorganization ⭐
**Priority:** MEDIUM  
**Effort:** LOW (2 hours)  
**Impact:** More intuitive menu structure

#### Tasks
- [ ] **Reorganize menu structure** (2 hours)
  ```
  File
    ├─ Open Video File... (Ctrl+O)
    ├─ Recent Files →
    └─ Exit (Alt+F4)
  
  Extract
    ├─ Probe Tracks (Ctrl+P)
    ├─ Extract to SRT (Ctrl+E)
    └─ Cancel (Esc)
  
  Tools
    ├─ SRT Correction...
    ├─ Batch SRT Correction...
    ├─ VobSub Track Analyzer...
    └─ Re-detect Tools (F5)
  
  Options
    ├─ Settings...
    └─ Preferences...
  
  Help
    ├─ Keyboard Shortcuts (F1)
    ├─ User Guide
    └─ About SrtExtractor...
  ```

#### Files to Modify
- `SrtExtractor/Views/MainWindow.xaml` (Lines 114-213)

---

### 3.2 Enhanced Batch Queue UI ⭐
**Priority:** MEDIUM  
**Effort:** MEDIUM (1 day)  
**Impact:** More professional appearance

#### Tasks
- [ ] **Increase item height** from current to 80px (1 hour)
- [ ] **Replace emoji with icon font** (2 hours)
  - Use Segoe MDL2 Assets or ModernWPF icons
  - Consistent rendering across systems
- [ ] **Add file thumbnails** (4 hours)
  - Extract first frame using FFmpeg
  - Show 64x64 thumbnail in queue item
  - Cache thumbnails for performance
- [ ] **Visual status grouping** (2 hours)
  - Separator lines between Pending/Processing/Completed groups
  - Different background colors per group
- [ ] **Larger remove button** (30 min)
  - Increase from 20x20 to 32x32
  - Better touch target

#### Files to Modify
- `SrtExtractor/Views/MainWindow.xaml` (Lines 1011-1080)
- Create thumbnail service in `Services/Implementations/`

---

### 3.3 Improved Error States ⭐
**Priority:** LOW  
**Effort:** MEDIUM (4 hours)  
**Impact:** Better user guidance when issues occur

#### Tasks
- [ ] **Enhance "No Tracks Found" message** (2 hours)
  - Add file information (duration, size, codec)
  - Suggest actions: Try different file, Check with VLC, Visit help docs
  - Add "Report Issue" button with pre-filled GitHub issue

- [ ] **Add extraction failure details** (2 hours)
  - Show what went wrong: Missing tool, unsupported format, file error
  - Provide specific next steps
  - Link to troubleshooting docs

#### Files to Modify
- `SrtExtractor/Views/MainWindow.xaml` (Lines 523-555)
- Create new error detail templates

---

### 3.4 Network Warning Enhancement ⭐
**Priority:** LOW  
**Effort:** LOW (2 hours)  
**Impact:** Better network file handling UX

#### Tasks
- [ ] **Show warning as toast** instead of inline border (1 hour)
- [ ] **Make time estimate a range** (30 min)
  - "Estimated: 15-25 minutes" instead of "~18 minutes"
- [ ] **Add dismissible notice** (30 min)
  - Allow users to acknowledge warning
  - Don't show again for this session

#### Files to Modify
- `SrtExtractor/Views/MainWindow.xaml` (Lines 405-430)
- Use existing ToastNotification system

---

### 3.5 Progress Indicator Consolidation ⭐
**Priority:** LOW  
**Effort:** LOW (3 hours)  
**Impact:** Clearer progress feedback

#### Tasks
- [ ] **Create unified progress component** (2 hours)
  - Single reusable progress indicator
  - Shows: Stage (1/3 Analyzing), Progress bar, Time remaining
  - Smooth animations

- [ ] **Replace multiple progress elements** (1 hour)
  - Remove duplicate progress bars and messages
  - Use single component throughout app

#### Files to Modify
- Create `Views/Controls/ProgressIndicator.xaml`
- Update MainWindow.xaml (Lines 498-516, 1119-1135)

---

### 3.6 Accessibility Improvements ⭐
**Priority:** LOW  
**Effort:** MEDIUM (1 day)  
**Impact:** WCAG compliance, broader user base

#### Tasks
- [ ] **Add AutomationProperties.Name** to all controls (3 hours)
- [ ] **Test with Windows Narrator** (2 hours)
- [ ] **Add keyboard alternative to drag-drop** (2 hours)
  - "Add Files" button for batch mode
- [ ] **Verify color contrast** (1 hour)
  - Test with contrast checker tool
- [ ] **Add visible focus indicators** (1 hour)

#### Files to Modify
- All XAML files
- `SrtExtractor/Themes/FocusStyles.xaml`

---

## 🎁 Quick Wins (Immediate Impact, Minimal Effort)

These can be done in a single afternoon and have high visible impact.

### Quick Win 1: Larger Extract Button ✅
**Effort:** 15 minutes  
**Impact:** Makes primary action obvious

```xaml
<Button Content="🚀 Extract to SRT" 
        Command="{Binding ExtractCommand}"
        Style="{StaticResource PrimaryButton}"
        Height="48"
        Width="200"
        FontSize="16"
        FontWeight="SemiBold"
        Margin="0,10,0,0"/>
```

**File:** `SrtExtractor/Views/MainWindow.xaml` (Line 446)

---

### Quick Win 2: Collapse Log by Default ✅
**Effort:** 10 minutes  
**Impact:** Saves 200px vertical space

```xaml
<Expander Header="Log" 
          Grid.Row="4" 
          Grid.ColumnSpan="2"
          IsExpanded="False">
  <!-- Existing log TextBox -->
</Expander>
```

**File:** `SrtExtractor/Views/MainWindow.xaml` (Line 733)

---

### Quick Win 3: Mode Indicator in Title Bar ✅
**Effort:** 5 minutes  
**Impact:** Users always know which mode they're in

```csharp
// In MainViewModel.cs
partial void OnIsBatchModeChanged(bool value)
{
    Application.Current.MainWindow.Title = value 
        ? "SrtExtractor - Batch Mode" 
        : "SrtExtractor - Extract Mode";
}
```

---

### Quick Win 4: Add Tooltips Everywhere ✅ COMPLETED
**Effort:** 30 minutes  
**Impact:** Inline help for all controls  
**Status:** ✅ Completed October 9, 2025

**What was added:**
- Comprehensive tooltips for all buttons with keyboard shortcuts
- Multi-line tooltips with detailed explanations
- Context-aware help for complex controls
- Technical details in tooltips (file paths, settings info)
- Right-click hints for context menus

**Files Modified:**
- `SrtExtractor/Views/MainWindow.xaml` - 15+ tooltip improvements

---

### Quick Win 5: Settings Summary Display ✅
**Effort:** 20 minutes  
**Impact:** Shows active settings at a glance

```xaml
<TextBlock Text="{Binding SettingsSummary}"
           Foreground="{StaticResource TextSecondaryBrush}"
           Margin="0,0,0,10"/>
```

```csharp
public string SettingsSummary => $"⚙️ {State.OcrLanguage} • {(State.PreferForced ? "Forced" : "CC")} • {State.CorrectionMode}";
```

---

## 📋 Implementation Checklist

Use this checklist to track progress through the improvement plan.

### Phase 1: Critical (Target: Week 1-2)
- [ ] 1.1 Simplify Main Window (Tab-based interface)
- [ ] 1.2 Remove dual-mode confusion
- [ ] 1.3 Humanize track information
- [ ] 1.4 Reduce log visibility

### Phase 2: Major (Target: Week 3-4)
- [ ] 2.1 Better settings placement
- [ ] 2.2 Consistent button hierarchy
- [ ] 2.3 Keyboard shortcut discoverability
- [ ] 2.4 Simplify DataGrid columns

### Phase 3: Polish (Target: Week 5-6 or v2.1)
- [ ] 3.1 Menu reorganization
- [ ] 3.2 Enhanced batch queue UI
- [ ] 3.3 Improved error states
- [ ] 3.4 Network warning enhancement
- [ ] 3.5 Progress indicator consolidation
- [ ] 3.6 Accessibility improvements

### Quick Wins (Target: Day 1)
- [ ] Larger extract button
- [ ] Collapse log by default
- [ ] Mode indicator in title bar
- [ ] Add tooltips everywhere
- [ ] Settings summary display

---

## 📊 Success Metrics & Testing

### User Testing Scenarios
After implementing Phase 1, test with 5 users (mix of technical and non-technical):

1. **First-time extraction** (success = completed in < 60 seconds)
   - Give user an MKV file
   - Ask them to extract English subtitles
   - Don't provide instructions
   - Measure: Time to completion, errors made, satisfaction (1-10)

2. **Batch processing** (success = 80% understand without help)
   - Give 3 video files
   - Ask them to extract subtitles from all
   - Measure: Do they find batch mode? Time to completion?

3. **Track selection** (success = choose correct track without help)
   - Give file with 3 subtitle tracks (PGS, SRT, Forced)
   - Ask for "English subtitles that won't require slow processing"
   - Measure: Do they select SRT track? Understand speed difference?

### Analytics to Track
- Time from launch to first extraction
- Percentage of users who try batch mode
- Error rate (failed extractions)
- Support ticket volume (before/after)
- User ratings in Microsoft Store

---

## 🎯 Design Philosophy

All improvements should follow these principles:

1. **Progressive Disclosure**: Show simple UI first, reveal complexity as needed
2. **Don't Make Me Think**: Primary actions should be obvious
3. **Provide Feedback**: Always show what's happening and what will happen
4. **Forgiveness**: Allow easy undo/cancel, prevent errors
5. **Consistency**: Same patterns throughout the app

---

## 📚 Resources & References

### Design Inspiration
- **Windows 11 Settings App**: Clean, tabbed interface
- **HandBrake**: Queue-based batch processing
- **VLC Media Player**: Track selection UI
- **Visual Studio Code**: Status bar implementation

### WCAG Guidelines
- [WCAG 2.1 Level AA](https://www.w3.org/WAI/WCAG21/quickref/)
- [Windows Accessibility](https://docs.microsoft.com/en-us/windows/apps/design/accessibility/accessibility)

### User Research
- Jakob Nielsen's 10 Usability Heuristics
- Microsoft Fluent Design System
- Material Design Guidelines (for reference)

---

## 📝 Notes & Decisions

### Decisions Made
1. **Separate tabs instead of dual-mode**: Clearer mental model, less complexity
2. **Remove technical jargon**: Focus on user goals, not implementation details
3. **Default to simple**: Power users can enable advanced features
4. **Log in separate tab**: Most users never need detailed logs

### Open Questions
1. Should we add a "Simple Mode" toggle that hides advanced features entirely?
2. Do we need video thumbnails in batch queue, or is that overkill?
3. Should History tab show extraction results or just recent files?
4. Do we add analytics/telemetry to understand actual usage patterns?

### Future Considerations (Post v2.0)
- Cloud sync for settings across devices
- Preset profiles (e.g., "Movies", "TV Shows", "Anime")
- Integration with Plex/Jellyfin for automatic subtitle extraction
- Command-line interface for automation
- Multi-language UI (internationalization)

---

**Document Owner:** UX Team  
**Last Updated:** October 9, 2025  
**Next Review:** After Phase 1 completion

---

*This is a living document. Update as requirements change and new insights emerge from user testing.*

