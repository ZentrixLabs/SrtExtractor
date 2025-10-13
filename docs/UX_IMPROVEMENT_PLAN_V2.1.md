# UX Improvement Plan v2.1 - SUP Feature & Application Polish
**Version:** 2.1  
**Date:** October 13, 2025  
**Status:** Ready for Implementation  
**Target Release:** v2.0.5 (Phase 1), v2.1 (Phase 2)

---

## 📋 Table of Contents

1. [Executive Summary](#executive-summary)
2. [Phase 1: Critical Fixes (v2.0.5)](#phase-1-critical-fixes-v205)
3. [Phase 2: Important Improvements (v2.1)](#phase-2-important-improvements-v21)
4. [Phase 3: Future Enhancements (v2.2+)](#phase-3-future-enhancements-v22)
5. [Implementation Details](#implementation-details)
6. [Testing & Validation](#testing--validation)

---

## 📊 Executive Summary

Based on comprehensive UX assessment, SrtExtractor v2.0.4 is **excellent** (8.5/10) but has specific opportunities for improvement around the new SUP feature and settings organization.

### Key Issues Identified

1. 🔴 **CRITICAL:** Batch SRT Correction feature virtually hidden
2. 🟡 **HIGH:** SUP OCR Tool difficult to discover
3. 🟡 **HIGH:** Correction settings too complex (5+ UI elements)
4. 🟡 **MEDIUM:** SUP preservation setting disconnected from tool
5. 🟢 **LOW:** Various polish and consistency improvements

### Implementation Strategy

- **Phase 1 (v2.0.5):** 4 critical improvements - 9 hours total
- **Phase 2 (v2.1):** 3 important improvements - 13 hours total
- **Phase 3 (v2.2+):** Nice-to-have enhancements - Future

---

## 🚀 Phase 1: Critical Fixes (v2.0.5)

**Goal:** Address discoverability and confusion issues  
**Effort:** 9 hours total  
**Impact:** High - Makes hidden features visible and simplifies complex settings

---

### Task 1.1: Make Batch SRT Correction Discoverable ⭐⭐⭐

**Priority:** CRITICAL  
**Effort:** 2 hours  
**Impact:** Reveals powerful feature that's currently hidden

#### Current Problem
Batch SRT Correction is hidden behind Tools → SRT Correction → Batch button. Most users never find it.

#### Solution
Add Batch SRT Correction as a first-class tool in Tools tab and menu.

#### Implementation

**Step 1: Add Button to Tools Tab** (30 min)

File: `SrtExtractor/Views/MainWindow.xaml`

Find the Tools section (around line 1000-1100 in Tools tab) and add:

```xaml
<!-- Subtitle Tools Section -->
<GroupBox Header="Subtitle Tools" Margin="0,0,0,15">
    <StackPanel>
        <!-- SUP OCR Tool -->
        <Button Content="Load SUP File..." 
                Click="LoadSupFile_Click"
                Style="{StaticResource SecondaryButton}"
                HorizontalAlignment="Left"
                Margin="0,5">
            <Button.ToolTip>
                <TextBlock TextWrapping="Wrap" MaxWidth="300">
                    Process SUP (PGS subtitle) files directly for OCR without extracting from MKV.&#x0a;
                    Useful for testing different OCR settings or re-processing existing SUP files.
                </TextBlock>
            </Button.ToolTip>
        </Button>
        <TextBlock Text="Process SUP files for OCR conversion" 
                   FontStyle="Italic" 
                   Foreground="{StaticResource TextSecondaryBrush}" 
                   FontSize="{StaticResource FontSizeSmall}"
                   Margin="20,0,0,10"/>
        
        <!-- Single SRT Correction -->
        <Button Content="Correct SRT File..." 
                Click="SrtCorrection_Click"
                Style="{StaticResource SecondaryButton}"
                HorizontalAlignment="Left"
                Margin="0,5">
            <Button.ToolTip>
                <TextBlock TextWrapping="Wrap" MaxWidth="300">
                    Fix OCR errors in a single SRT file.&#x0a;
                    Applies ~841 correction patterns to improve subtitle quality.
                </TextBlock>
            </Button.ToolTip>
        </Button>
        <TextBlock Text="Fix OCR errors in a single SRT file" 
                   FontStyle="Italic" 
                   Foreground="{StaticResource TextSecondaryBrush}" 
                   FontSize="{StaticResource FontSizeSmall}"
                   Margin="20,0,0,10"/>
        
        <!-- Batch SRT Correction - NEW! -->
        <Button Content="Batch SRT Correction..." 
                Click="BatchSrtCorrection_Click"
                Style="{StaticResource SecondaryButton}"
                HorizontalAlignment="Left"
                Margin="0,5">
            <Button.ToolTip>
                <TextBlock TextWrapping="Wrap" MaxWidth="300">
                    Process hundreds of SRT files simultaneously with bulk correction.&#x0a;
                    Typical results: 1000+ corrections per file!
                </TextBlock>
            </Button.ToolTip>
        </Button>
        <TextBlock Text="Process multiple SRT files at once (hundreds of files supported)" 
                   FontStyle="Italic" 
                   Foreground="{StaticResource TextSecondaryBrush}" 
                   FontSize="{StaticResource FontSizeSmall}"
                   Margin="20,0,0,10"/>
        
        <!-- VobSub Track Analyzer -->
        <Button Content="VobSub Track Analyzer..." 
                Click="VobSubTrackAnalyzer_Click"
                Style="{StaticResource SecondaryButton}"
                HorizontalAlignment="Left"
                Margin="0,5">
            <Button.ToolTip>
                <TextBlock TextWrapping="Wrap" MaxWidth="300">
                    Analyze VobSub (image-based) subtitle tracks for batch processing in Subtitle Edit.
                </TextBlock>
            </Button.ToolTip>
        </Button>
        <TextBlock Text="Analyze VobSub subtitle tracks across multiple files" 
                   FontStyle="Italic" 
                   Foreground="{StaticResource TextSecondaryBrush}" 
                   FontSize="{StaticResource FontSizeSmall}"
                   Margin="20,0,0,10"/>
    </StackPanel>
</GroupBox>
```

**Step 2: Add to Tools Menu** (15 min)

File: `SrtExtractor/Views/MainWindow.xaml`

Update the Tools menu (around line 155-166):

```xaml
<MenuItem Header="_Tools">
    <MenuItem Header="Load _SUP File..." 
              Click="LoadSupFile_Click"
              ToolTip="Load a SUP file directly for OCR processing"/>
    <Separator/>
    <MenuItem Header="_Correct SRT File..." 
              Click="SrtCorrection_Click"
              InputGestureText="Ctrl+R"
              ToolTip="Fix OCR errors in a single SRT file"/>
    <MenuItem Header="_Batch SRT Correction..." 
              Click="BatchSrtCorrection_Click"
              InputGestureText="Ctrl+Shift+R"
              ToolTip="Process multiple SRT files at once with bulk correction"/>
    <Separator/>
    <MenuItem Header="_VobSub Track Analyzer..." 
              Click="VobSubTrackAnalyzer_Click"
              ToolTip="Analyze VobSub subtitle tracks for batch processing"/>
</MenuItem>
```

**Step 3: Add Event Handler** (15 min)

File: `SrtExtractor/Views/MainWindow.xaml.cs`

Add the click handler:

```csharp
private void BatchSrtCorrection_Click(object sender, RoutedEventArgs e)
{
    try
    {
        var window = new BatchSrtCorrectionWindow
        {
            Owner = this
        };
        window.ShowDialog();
        
        _loggingService.LogInfo("User opened Batch SRT Correction tool");
    }
    catch (Exception ex)
    {
        _loggingService.LogError("Failed to open Batch SRT Correction window", ex);
        _notificationService.ShowError($"Failed to open Batch SRT Correction:\n{ex.Message}", "Error");
    }
}
```

**Step 4: Add Keyboard Shortcut** (15 min)

File: `SrtExtractor/Views/MainWindow.xaml`

Add to Window.InputBindings (around line 22-30):

```xaml
<KeyBinding Key="R" Modifiers="Ctrl+Shift" Command="{Binding OpenBatchSrtCorrectionCommand}"/>
```

File: `SrtExtractor/ViewModels/MainViewModel.cs`

Add command:

```csharp
public IRelayCommand OpenBatchSrtCorrectionCommand { get; }

// In constructor:
OpenBatchSrtCorrectionCommand = new RelayCommand(OpenBatchSrtCorrection);

private void OpenBatchSrtCorrection()
{
    try
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var window = new BatchSrtCorrectionWindow
            {
                Owner = Application.Current.MainWindow
            };
            window.ShowDialog();
        });
        
        _loggingService.LogInfo("User opened Batch SRT Correction via keyboard shortcut");
    }
    catch (Exception ex)
    {
        _loggingService.LogError("Failed to open Batch SRT Correction window", ex);
    }
}
```

**Step 5: Update Keyboard Shortcuts Window** (15 min)

File: `SrtExtractor/Views/KeyboardShortcutsWindow.xaml`

Add to the shortcuts list:

```xaml
<!-- In Tools & Utilities Section -->
<StackPanel Orientation="Horizontal" Margin="0,5">
    <Border Style="{StaticResource KeyBadgeStyle}">
        <TextBlock Text="Ctrl"/>
    </Border>
    <TextBlock Text="+" Margin="2,0"/>
    <Border Style="{StaticResource KeyBadgeStyle}">
        <TextBlock Text="Shift"/>
    </Border>
    <TextBlock Text="+" Margin="2,0"/>
    <Border Style="{StaticResource KeyBadgeStyle}">
        <TextBlock Text="R"/>
    </Border>
    <TextBlock Text="Batch SRT Correction" Margin="10,0" FontSize="13"/>
</StackPanel>
```

**Step 6: Update Welcome Screen** (30 min)

File: `SrtExtractor/Views/WelcomeWindow.xaml`

Add a new page or update existing page to highlight batch correction:

```xaml
<!-- Add to the pages carousel -->
<Border Background="White" Padding="40">
    <StackPanel>
        <TextBlock Text="💪 Powerful Batch Tools" 
                   FontSize="28" 
                   FontWeight="Bold" 
                   Foreground="{StaticResource PrimaryBrush}"
                   TextAlignment="Center"
                   Margin="0,0,0,20"/>
        
        <TextBlock Text="Process Hundreds of Files at Once" 
                   FontSize="16"
                   Foreground="{StaticResource TextSecondaryBrush}"
                   TextAlignment="Center"
                   Margin="0,0,0,30"/>
        
        <Border Background="{StaticResource BackgroundAltBrush}"
                BorderBrush="{StaticResource BorderBrush}"
                BorderThickness="1"
                CornerRadius="8"
                Padding="20"
                Margin="0,0,0,20">
            <StackPanel>
                <TextBlock Text="🚀 Batch SRT Correction" 
                           FontSize="18" 
                           FontWeight="SemiBold"
                           Margin="0,0,0,10"/>
                <TextBlock TextWrapping="Wrap" Foreground="{StaticResource TextSecondaryBrush}">
                    • Process 100+ SRT files in minutes<LineBreak/>
                    • Apply 841 correction patterns automatically<LineBreak/>
                    • Typical results: 1000+ corrections per file<LineBreak/>
                    • Optional backup creation for safety<LineBreak/>
                    • Real-time progress tracking
                </TextBlock>
                <TextBlock Text="Find it: Tools Tab → Batch SRT Correction" 
                           FontStyle="Italic"
                           FontSize="12"
                           Foreground="{StaticResource PrimaryBrush}"
                           Margin="0,10,0,0"/>
            </StackPanel>
        </Border>
        
        <Border Background="{StaticResource BackgroundAltBrush}"
                BorderBrush="{StaticResource BorderBrush}"
                BorderThickness="1"
                CornerRadius="8"
                Padding="20">
            <StackPanel>
                <TextBlock Text="🔧 SUP OCR Tool" 
                           FontSize="18" 
                           FontWeight="SemiBold"
                           Margin="0,0,0,10"/>
                <TextBlock TextWrapping="Wrap" Foreground="{StaticResource TextSecondaryBrush}">
                    • Process SUP files directly without MKV extraction<LineBreak/>
                    • Test different OCR settings quickly<LineBreak/>
                    • Re-process subtitles for better quality<LineBreak/>
                    • Dedicated window with progress tracking
                </TextBlock>
                <TextBlock Text="Find it: Tools Menu → Load SUP File" 
                           FontStyle="Italic"
                           FontSize="12"
                           Foreground="{StaticResource PrimaryBrush}"
                           Margin="0,10,0,0"/>
            </StackPanel>
        </Border>
    </StackPanel>
</Border>
```

#### Testing
- [ ] Batch SRT Correction button visible in Tools tab
- [ ] Button click opens BatchSrtCorrectionWindow
- [ ] Menu item works correctly
- [ ] Keyboard shortcut (Ctrl+Shift+R) works
- [ ] Welcome screen shows batch correction info
- [ ] Tooltips display correctly

---

### Task 1.2: Simplify Correction Settings ⭐⭐⭐

**Priority:** CRITICAL  
**Effort:** 4 hours  
**Impact:** Eliminates user confusion about correction settings

#### Current Problem
Settings window has 5+ UI elements for correction (two checkboxes, dropdown, textbox, checkbox). Users confused about relationships.

#### Solution
Simplify to 3-option radio button system with "Advanced..." button for power users.

#### Implementation

**Step 1: Update Settings Window XAML** (2 hours)

File: `SrtExtractor/Views/SettingsWindow.xaml`

Replace the current SRT Correction and Multi-Pass Correction sections (lines ~94-193) with:

```xaml
<!-- Simplified Correction Settings -->
<GroupBox Header="Subtitle Correction" Margin="0,0,0,15">
    <StackPanel>
        <TextBlock Text="Choose correction quality level:" 
                   FontWeight="SemiBold" 
                   Margin="0,5,0,10"/>
        
        <RadioButton GroupName="CorrectionLevel"
                     IsChecked="{Binding State.CorrectionLevel, Converter={StaticResource EnumToBoolConverter}, ConverterParameter=Off}"
                     Margin="0,8">
            <StackPanel>
                <TextBlock Text="Off (raw OCR output)" 
                           FontWeight="SemiBold"/>
                <TextBlock Text="No corrections applied - use for debugging or manual review" 
                           FontStyle="Italic" 
                           Foreground="{StaticResource TextSecondaryBrush}" 
                           FontSize="{StaticResource FontSizeSmall}"
                           Margin="0,2,0,0"/>
            </StackPanel>
        </RadioButton>
        
        <RadioButton GroupName="CorrectionLevel"
                     IsChecked="{Binding State.CorrectionLevel, Converter={StaticResource EnumToBoolConverter}, ConverterParameter=Standard}"
                     Margin="0,8">
            <StackPanel>
                <StackPanel Orientation="Horizontal">
                    <TextBlock Text="Standard (recommended)" 
                               FontWeight="SemiBold"/>
                    <Border Background="{StaticResource PrimaryBrush}"
                            CornerRadius="3"
                            Padding="6,2"
                            Margin="8,0,0,0">
                        <TextBlock Text="RECOMMENDED" 
                                   FontSize="10" 
                                   Foreground="White" 
                                   FontWeight="Bold"/>
                    </Border>
                </StackPanel>
                <TextBlock Text="Smart multi-pass correction with automatic convergence detection" 
                           FontStyle="Italic" 
                           Foreground="{StaticResource TextSecondaryBrush}" 
                           FontSize="{StaticResource FontSizeSmall}"
                           Margin="0,2,0,0"/>
                <TextBlock Text="• Typically 3 passes • ~841 correction patterns • Fastest with excellent quality" 
                           FontStyle="Italic" 
                           Foreground="{StaticResource TextTertiaryBrush}" 
                           FontSize="{StaticResource FontSizeCaption}"
                           Margin="0,2,0,0"/>
            </StackPanel>
        </RadioButton>
        
        <RadioButton GroupName="CorrectionLevel"
                     IsChecked="{Binding State.CorrectionLevel, Converter={StaticResource EnumToBoolConverter}, ConverterParameter=Thorough}"
                     Margin="0,8">
            <StackPanel>
                <TextBlock Text="Thorough (maximum quality)" 
                           FontWeight="SemiBold"/>
                <TextBlock Text="5+ correction passes without early stopping - best for challenging OCR" 
                           FontStyle="Italic" 
                           Foreground="{StaticResource TextSecondaryBrush}" 
                           FontSize="{StaticResource FontSizeSmall}"
                           Margin="0,2,0,0"/>
                <TextBlock Text="• Always 5 passes • Finds subtle errors • Slightly slower" 
                           FontStyle="Italic" 
                           Foreground="{StaticResource TextTertiaryBrush}" 
                           FontSize="{StaticResource FontSizeCaption}"
                           Margin="0,2,0,0"/>
            </StackPanel>
        </RadioButton>
        
        <!-- Advanced Options (collapsed by default) -->
        <Expander Header="Advanced Settings..." 
                  IsExpanded="False" 
                  Margin="0,15,0,0">
            <Border Background="{StaticResource BackgroundAltBrush}"
                    BorderBrush="{StaticResource BorderBrush}"
                    BorderThickness="1"
                    CornerRadius="4"
                    Padding="15"
                    Margin="0,10,0,0">
                <StackPanel>
                    <TextBlock Text="⚠️ Advanced users only - these settings override the mode above" 
                               FontWeight="SemiBold"
                               Foreground="{StaticResource WarningBrush}"
                               Margin="0,0,0,10"/>
                    
                    <CheckBox Content="Enable multi-pass correction" 
                              IsChecked="{Binding State.EnableMultiPassCorrection}"
                              Margin="0,5"/>
                    
                    <StackPanel Orientation="Horizontal" Margin="0,10">
                        <TextBlock Text="Mode:" Width="120" VerticalAlignment="Center"/>
                        <ComboBox SelectedItem="{Binding State.CorrectionMode}" 
                                  ItemsSource="{Binding State.AvailableCorrectionModes}"
                                  Width="150"
                                  IsEnabled="{Binding State.EnableMultiPassCorrection}"/>
                    </StackPanel>
                    
                    <StackPanel Orientation="Horizontal" Margin="0,5">
                        <TextBlock Text="Max Passes:" Width="120" VerticalAlignment="Center"/>
                        <TextBox Text="{Binding State.MaxCorrectionPasses}" 
                                 Width="60"
                                 IsEnabled="{Binding State.EnableMultiPassCorrection}"/>
                    </StackPanel>
                    
                    <CheckBox Content="Use smart convergence" 
                              IsChecked="{Binding State.UseSmartConvergence}"
                              IsEnabled="{Binding State.EnableMultiPassCorrection}"
                              Margin="0,10,0,0"/>
                </StackPanel>
            </Border>
        </Expander>
    </StackPanel>
</GroupBox>
```

**Step 2: Add CorrectionLevel Enum** (30 min)

File: `SrtExtractor/Models/CorrectionLevel.cs` (new file)

```csharp
namespace SrtExtractor.Models;

/// <summary>
/// Defines the correction quality levels for subtitle OCR correction.
/// </summary>
public enum CorrectionLevel
{
    /// <summary>
    /// No corrections applied - raw OCR output.
    /// </summary>
    Off,
    
    /// <summary>
    /// Standard correction with smart convergence (recommended).
    /// Typically 3 passes with automatic stopping when no more corrections found.
    /// </summary>
    Standard,
    
    /// <summary>
    /// Thorough correction with 5+ passes.
    /// No early stopping - always runs full pass count for maximum quality.
    /// </summary>
    Thorough
}
```

**Step 3: Update ExtractionState** (30 min)

File: `SrtExtractor/State/ExtractionState.cs`

Add new property and update related properties:

```csharp
[ObservableProperty]
private CorrectionLevel _correctionLevel = CorrectionLevel.Standard;

// Update OnCorrectionLevelChanged to set internal flags
partial void OnCorrectionLevelChanged(CorrectionLevel value)
{
    switch (value)
    {
        case CorrectionLevel.Off:
            EnableSrtCorrection = false;
            EnableMultiPassCorrection = false;
            break;
            
        case CorrectionLevel.Standard:
            EnableSrtCorrection = true;
            EnableMultiPassCorrection = true;
            CorrectionMode = "Standard";
            UseSmartConvergence = true;
            MaxCorrectionPasses = 3;
            break;
            
        case CorrectionLevel.Thorough:
            EnableSrtCorrection = true;
            EnableMultiPassCorrection = true;
            CorrectionMode = "Thorough";
            UseSmartConvergence = false;
            MaxCorrectionPasses = 5;
            break;
    }
    
    OnPropertyChanged(nameof(SettingsSummary));
    _loggingService.LogInfo($"Correction level changed to: {value}");
}
```

**Step 4: Add Enum to Bool Converter** (30 min)

File: `SrtExtractor/Converters/EnumToBoolConverter.cs` (new file)

```csharp
using System;
using System.Globalization;
using System.Windows.Data;

namespace SrtExtractor.Converters;

/// <summary>
/// Converts an enum value to a boolean for radio button binding.
/// </summary>
public class EnumToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false;
        
        return value.ToString() == parameter.ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter != null)
        {
            return Enum.Parse(targetType, parameter.ToString());
        }
        
        return Binding.DoNothing;
    }
}
```

**Step 5: Register Converter** (5 min)

File: `SrtExtractor/Views/SettingsWindow.xaml`

Add to Window.Resources:

```xaml
<Window.Resources>
    <converters:EnumToBoolConverter x:Key="EnumToBoolConverter"/>
    <!-- ... existing resources ... -->
</Window.Resources>
```

**Step 6: Update AppSettings** (15 min)

File: `SrtExtractor/Models/AppSettings.cs`

Update to store CorrectionLevel:

```csharp
public record AppSettings(
    // ... existing parameters ...
    CorrectionLevel CorrectionLevel = CorrectionLevel.Standard  // Add this
);
```

**Step 7: Update Settings Load/Save** (15 min)

File: `SrtExtractor/ViewModels/MainViewModel.cs`

Update InitializeAsync and SaveSettingsFromDialogAsync:

```csharp
// In InitializeAsync:
State.CorrectionLevel = settings.CorrectionLevel;

// In SaveSettingsFromDialogAsync:
var settings = new AppSettings(
    // ... existing parameters ...
    CorrectionLevel: State.CorrectionLevel
);
```

#### Testing
- [ ] Radio buttons work correctly
- [ ] "Standard" is selected by default
- [ ] Switching modes updates internal flags correctly
- [ ] Advanced expander shows/hides properly
- [ ] Settings persist across app restarts
- [ ] Extraction uses correct correction level

---

### Task 1.3: Add SUP Tool to Tools Tab ⭐⭐

**Priority:** HIGH  
**Effort:** 1 hour  
**Impact:** Improves SUP feature discoverability

#### Implementation

Already covered in Task 1.1 Step 1 above. The SUP OCR Tool button is included in the "Subtitle Tools" GroupBox.

Additional step: Remove obsolete tool status section if it still exists.

---

### Task 1.4: Connect SUP Preservation to SUP Tool ⭐⭐

**Priority:** MEDIUM  
**Effort:** 2 hours  
**Impact:** Closes user journey gap between setting and tool

#### Implementation

**Step 1: Update Settings Description** (15 min)

File: `SrtExtractor/Views/SettingsWindow.xaml`

Update the SUP preservation checkbox description (Advanced tab, around line 254-270):

```xaml
<CheckBox Content="Preserve SUP files for debugging" 
          IsChecked="{Binding State.PreserveSupFiles}"
          FontWeight="Bold"
          Margin="0,5"/>
<TextBlock TextWrapping="Wrap" Margin="0,5,0,5">
    <Run Text="Keep extracted SUP (PGS subtitle) files instead of deleting them after OCR." Foreground="{StaticResource TextSecondaryBrush}" FontStyle="Italic"/>
    <LineBreak/>
    <Run Text="Useful for:" Foreground="{StaticResource TextSecondaryBrush}" FontStyle="Italic"/>
    <LineBreak/>
    <Run Text="• Debugging OCR quality issues" Foreground="{StaticResource TextSecondaryBrush}" FontStyle="Italic"/>
    <LineBreak/>
    <Run Text="• Testing different OCR configurations" Foreground="{StaticResource TextSecondaryBrush}" FontStyle="Italic"/>
    <LineBreak/>
    <Run Text="• Re-processing subtitles with " Foreground="{StaticResource TextSecondaryBrush}" FontStyle="Italic"/>
    <Hyperlink NavigateUri="LoadSupTool" 
               RequestNavigate="LoadSupToolHyperlink_Navigate"
               Foreground="{StaticResource PrimaryBrush}">
        <Run Text="SUP OCR Tool"/>
    </Hyperlink>
</TextBlock>
<TextBlock Text="SUP files are saved next to the output SRT file with the same name." 
           FontStyle="Italic" 
           Foreground="{StaticResource TextSecondaryBrush}" 
           FontSize="{StaticResource FontSizeSmall}"
           Margin="0,0,0,5"/>
```

**Step 2: Add Hyperlink Handler** (15 min)

File: `SrtExtractor/Views/SettingsWindow.xaml.cs`

```csharp
private void LoadSupToolHyperlink_Navigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
{
    try
    {
        // Close settings window
        this.Close();
        
        // Open SUP OCR tool
        var supWindow = new SupOcrWindow(
            App.Services.GetRequiredService<ISubtitleOcrService>(),
            App.Services.GetRequiredService<ILoggingService>(),
            App.Services.GetRequiredService<INotificationService>())
        {
            Owner = this.Owner
        };
        supWindow.ShowDialog();
        
        e.Handled = true;
    }
    catch (Exception ex)
    {
        _loggingService?.LogError("Failed to open SUP OCR Tool from hyperlink", ex);
    }
}
```

**Step 3: Show Hint After Extraction** (45 min)

File: `SrtExtractor/ViewModels/MainViewModel.cs`

Update ExtractPgsSubtitlesAsync method (around line 500-543):

```csharp
// After successful OCR, before cleanup
if (State.PreserveSupFiles)
{
    _loggingService.LogInfo($"Preserving SUP file for debugging: {tempSupPath}");
    State.AddLogMessage($"SUP file preserved: {Path.GetFileName(tempSupPath)}");
    
    // Show hint to user about SUP OCR Tool
    await Application.Current.Dispatcher.InvokeAsync(() =>
    {
        _notificationService.ShowInfo(
            $"SUP file preserved for debugging:\n{Path.GetFileName(tempSupPath)}\n\n" +
            "💡 Tip: Use Tools → Load SUP File to re-process this file with different OCR settings.",
            "SUP File Preserved",
            durationMs: 8000);
    });
}
```

**Step 4: Add Context Menu (Future Enhancement)** (30 min)

This requires more complex file association work. Document for v2.1:

```
Future: Add Windows Explorer context menu for .sup files
- Right-click .sup file → "Open with SrtExtractor SUP OCR Tool"
- Requires installer modifications (InnoSetup)
- Registry entries for file associations
- Defer to v2.1
```

#### Testing
- [ ] Hyperlink in settings opens SUP OCR Tool
- [ ] Toast notification appears when SUP preserved
- [ ] Notification has correct information
- [ ] Settings window closes when hyperlink clicked

---

## 📊 Phase 1 Summary

**Total Effort:** 9 hours  
**Tasks Completed:** 4  
**Impact:** High - Critical discoverability and usability improvements

### Changes Made
1. ✅ Batch SRT Correction visible in Tools tab and menu
2. ✅ Simplified correction settings to 3-option radio button
3. ✅ SUP OCR Tool prominent in Tools tab
4. ✅ SUP preservation connected to tool usage

### Files Modified
- `SrtExtractor/Views/MainWindow.xaml` - Tools tab, menu, keyboard shortcuts
- `SrtExtractor/Views/MainWindow.xaml.cs` - Event handlers
- `SrtExtractor/Views/SettingsWindow.xaml` - Simplified correction UI
- `SrtExtractor/Views/SettingsWindow.xaml.cs` - Hyperlink handler
- `SrtExtractor/Views/KeyboardShortcutsWindow.xaml` - New shortcut
- `SrtExtractor/Views/WelcomeWindow.xaml` - Batch correction info
- `SrtExtractor/ViewModels/MainViewModel.cs` - Commands, notifications
- `SrtExtractor/State/ExtractionState.cs` - CorrectionLevel property
- `SrtExtractor/Models/AppSettings.cs` - CorrectionLevel field
- `SrtExtractor/Models/CorrectionLevel.cs` - NEW FILE
- `SrtExtractor/Converters/EnumToBoolConverter.cs` - NEW FILE

### Testing Checklist
- [ ] All 4 tools visible in Tools tab
- [ ] Batch SRT Correction opens from menu and tab
- [ ] Keyboard shortcut (Ctrl+Shift+R) works
- [ ] Correction settings simplified and functional
- [ ] Settings persist across restarts
- [ ] SUP preservation shows notification
- [ ] Hyperlink in settings works
- [ ] Welcome screen shows batch correction

---

## 🔧 Phase 2: Important Improvements (v2.1)

**Goal:** Improve consistency and organization  
**Effort:** 13 hours total  
**Impact:** Medium - Better organization and workflow

### Task 2.1: Inherit Settings in SUP OCR Window
**Effort:** 3 hours  
**Impact:** Consistency improvement

[Details omitted for brevity - available on request]

### Task 2.2: Reorganize Settings Window
**Effort:** 6 hours  
**Impact:** Better organization

[Details omitted for brevity - available on request]

### Task 2.3: Enhanced SUP Window Progress
**Effort:** 3 hours  
**Impact:** Polish

[Details omitted for brevity - available on request]

---

## 🌟 Phase 3: Future Enhancements (v2.2+)

**Goal:** Advanced features and polish  
**Effort:** 12+ hours  
**Impact:** Nice-to-have improvements

### Task 3.1: SUP Files in Batch Tab
**Effort:** 8 hours

[Details omitted for brevity - available on request]

### Task 3.2: Dynamic Language Detection
**Effort:** 4 hours

[Details omitted for brevity - available on request]

---

## ✅ Testing & Validation

### Automated Tests
- [ ] Unit tests for CorrectionLevel enum conversion
- [ ] Integration tests for simplified settings
- [ ] Regression tests for existing correction functionality

### Manual Testing Scenarios

#### Scenario 1: Find Batch SRT Correction
**Steps:**
1. Launch app
2. Navigate to Tools tab
3. Find "Batch SRT Correction" button

**Expected:** User finds button within 10 seconds  
**Current Baseline:** Most users never find it  
**Target:** >80% find within 30 seconds

#### Scenario 2: Understand Correction Settings
**Steps:**
1. Open Settings
2. View correction options
3. Select "Standard" (recommended)

**Expected:** User understands options without help  
**Current Baseline:** Confusion about 5+ settings  
**Target:** >90% correctly select Standard mode

#### Scenario 3: Use SUP OCR Tool After Preservation
**Steps:**
1. Enable SUP preservation in settings
2. Extract PGS subtitles
3. Find and use SUP OCR Tool

**Expected:** User follows notification hint  
**Current Baseline:** Unclear what to do with SUP file  
**Target:** >70% successfully use tool

---

## 📋 Implementation Checklist

### Pre-Implementation
- [ ] Review this plan with team
- [ ] Create feature branch: `feature/ux-improvements-v2.1`
- [ ] Back up current codebase
- [ ] Review cursor rules and coding standards

### Phase 1 Implementation (v2.0.5)
- [ ] Task 1.1: Batch SRT Correction discoverability (2h)
- [ ] Task 1.2: Simplify correction settings (4h)
- [ ] Task 1.3: SUP tool to Tools tab (1h)
- [ ] Task 1.4: Connect SUP preservation (2h)
- [ ] Code review and testing (2h)
- [ ] Update documentation (1h)

### Phase 2 Implementation (v2.1)
- [ ] Task 2.1: Inherit SUP settings (3h)
- [ ] Task 2.2: Reorganize Settings window (6h)
- [ ] Task 2.3: Enhanced SUP progress (3h)
- [ ] Code review and testing (3h)
- [ ] Update documentation (1h)

### Post-Implementation
- [ ] Run full test suite
- [ ] Performance testing
- [ ] User acceptance testing
- [ ] Update CHANGELOG.md
- [ ] Update README.md
- [ ] Create release notes
- [ ] Deploy to staging
- [ ] Production release

---

## 📚 Resources

### Related Documents
- `docs/UX_ASSESSMENT_SUP_FEATURE.md` - Full UX assessment
- `docs/UX_IMPROVEMENT_PLAN.md` - Original v2.0 plan
- `docs/CURSOR_RULES.md` - Coding standards
- `README.md` - User-facing documentation

### Design References
- Microsoft Fluent Design System
- HandBrake UI patterns
- Windows 11 Settings app

---

**Document Owner:** Development Team  
**Status:** Ready for Implementation  
**Next Review:** After Phase 1 completion  
**Target Completion:** v2.0.5 (Phase 1) - November 2025

---

*This plan provides actionable steps with code examples to elevate SrtExtractor from excellent to exceptional.*

