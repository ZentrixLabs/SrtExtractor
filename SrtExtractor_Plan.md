# SrtExtractor (WPF, Windows) — Project Plan

**Goal:** A tiny Windows desktop app that extracts subtitles from MKV files.  
- If the subtitle track is **text** (`S_TEXT/UTF8`) → save straight to `.srt`.  
- If it's **PGS** (`S_HDMV/PGS`) → extract `.sup`, then OCR to `.srt` via **Subtitle Edit** CLI.

---

## Mission Objective

Deliver a minimal, reliable **WPF (.NET 9)** app that wraps **MKVToolNix** and **Subtitle Edit** CLIs with a clean UX:

- **Pick MKV → Probe tracks → Select track → Extract → (If PGS) OCR → SRT.**
- Preserve original folder and name; append `.en.forced.srt` or `.en.srt` style suffixes.
- Keep logs visible in the UI and also write to a text log for debugging.
- **Automatic tool detection** with graceful degradation when tools are missing.
- **One-click installation** via winget integration for required tools.
- **No crashes** due to missing dependencies - app provides clear guidance.

> Architecture pattern: **Services → State Objects → UI** (MVVM-friendly).

---

## Tech Stack

- **Framework:** .NET 9, **WPF**
- **Language:** C#
- **MVVM:** `CommunityToolkit.Mvvm` (required for commands and binding)
- **JSON:** `System.Text.Json` for settings management
- **External tools:**
  - **MKVToolNix**: `mkvmerge.exe`, `mkvextract.exe`
  - **Subtitle Edit**: `SubtitleEdit.exe` (for OCR of PGS → SRT)
  - **Winget**: For automatic tool installation

> Install via winget (integrated into app):  
> ```powershell
> winget install MoritzBunkus.MKVToolNix
> winget install SubtitleEdit.SubtitleEdit
> ```

---

## External Tool Detection & Installation

### **Multi-Layered Detection Strategy**

At app start, resolve executable paths in this order:

1. **User-specified path** (stored in settings)  
2. **PATH** lookup (use `where mkvmerge`, `where mkvextract`, `where SubtitleEdit`)  
3. **Common install locations**:
   - MKVToolNix: `C:\Program Files\MKVToolNix\`, `C:\Program Files (x86)\MKVToolNix\`
   - Subtitle Edit: `C:\Program Files\Subtitle Edit\`, `C:\Program Files (x86)\Subtitle Edit\`
4. **Registry lookup** (if needed)
5. **Prompt user** with installation options

### **Winget Integration**

- **Automatic detection** of winget availability
- **One-click installation** buttons in UI
- **Progress feedback** during installation
- **Re-detection** after installation completes

### **Graceful Degradation**

- **Disable features** when tools are missing
- **Clear status indicators** (✅ Installed, ❌ Missing, ⚠️ Error)
- **Helpful error messages** with next steps
- **No crashes** due to missing dependencies

### **Settings Storage**

Store configuration in JSON settings file under:  
`%AppData%\SrtExtractor\settings.json`

```jsonc
{
  "MkvMergePath": "C:\\Program Files\\MKVToolNix\\mkvmerge.exe",
  "MkvExtractPath": "C:\\Program Files\\MKVToolNix\\mkvextract.exe",
  "SubtitleEditPath": "C:\\Program Files\\Subtitle Edit\\SubtitleEdit.exe",
  "AutoDetectTools": true,
  "LastToolCheck": "2024-01-15T10:30:00Z",
  "PreferForced": true,
  "DefaultOcrLanguage": "eng",
  "FileNamePattern": "{basename}.{lang}{forced}.srt"
}
```

---

## Enhanced UX (WPF) with Tool Management

```
+-------------------------------------------------------+
| [Pick MKV…]   C:\Movies\MyFilm.mkv                    |
|                                                       |
| Tool Status                                           |
| ┌─────────────────────────────────────────────────┐   |
| │ MKVToolNix:     ✅ Installed v81.0.1            │   |
| │                 [Browse...] [Reinstall]         │   |
| │ Subtitle Edit:  ❌ Not Found                    │   |
| │                 [Install via winget] [Browse...]│   |
| └─────────────────────────────────────────────────┘   |
|                                                       |
| [Probe Tracks]  [ ] Prefer forced   OCR lang: [ eng ] |
|                                                       |
|  Subtitle Tracks                                      |
|  ---------------------------------------------------  |
|  ID | Codec        | Lang | Forced | Name             |
|  2  | S_TEXT/UTF8  | eng  | false  | English          |
|  3  | S_HDMV/PGS   | eng  | true   | English Forced   |
|  ...                                                  |
|                                                       |
|                               [Extract Selected → SRT]|
|                                                       |
|  Log                                                 v|
|  [ ... timestamped lines; stderr inlined … ]          |
+-------------------------------------------------------+
```

### **UI Features**

- **Pick MKV…** → file dialog for `.mkv` + drag & drop support
- **Tool Status Panel** → Real-time status with installation options
- **Probe Tracks** → calls `mkvmerge -J` and populates the list (auto-selects based on options)
- **Extract Selected → SRT** → text subs: `.srt` directly; PGS: `.sup` then OCR to `.srt`
- **Log** → real-time process output and errors
- **Status Indicators** → ✅ Installed, ❌ Missing, ⚠️ Error
- **Installation Buttons** → One-click winget installation with progress feedback
- **Graceful Degradation** → Disable features when tools missing

---

## Data & Models

### **Core Models**
```csharp
public record SubtitleTrack(
    int Id,
    string Codec,
    string Language,
    bool Forced,
    string? Name
);

public record ProbeResult(IReadOnlyList<SubtitleTrack> Tracks);
```

### **Tool Management Models**
```csharp
public record ToolStatus(
    bool IsInstalled,
    string? Path,
    string? Version,
    string? ErrorMessage
);

public enum ToolErrorType
{
    NotFound,
    PathInvalid,
    VersionIncompatible,
    ExecutionFailed,
    WingetNotAvailable
}
```

### **Settings Model**
```csharp
public record AppSettings(
    string? MkvMergePath,
    string? MkvExtractPath,
    string? SubtitleEditPath,
    bool AutoDetectTools,
    DateTime? LastToolCheck,
    bool PreferForced,
    string DefaultOcrLanguage,
    string FileNamePattern
);
```

**File naming:**  
`{basename}.{lang}{.forced}.srt` (omit `.forced` if not forced). Example:  
- `MyFilm.eng.forced.srt`
- `MyFilm.eng.srt`

---

## Services (Core)

### 1) `IProcessRunner`
Thin wrapper to run external processes and capture stdout/stderr/exit code.

```csharp
public interface IProcessRunner
{
    Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(string exe, string args, CancellationToken ct = default);
}
```

### 2) `IToolDetectionService`
Multi-layered tool detection with validation.

```csharp
public interface IToolDetectionService
{
    Task<ToolStatus> CheckMkvToolNixAsync();
    Task<ToolStatus> CheckSubtitleEditAsync();
    Task<string?> FindToolPathAsync(string toolName, string[] commonPaths);
    Task<bool> ValidateToolAsync(string toolPath);
}
```

### 3) `IWingetService`
Package management and installation.

```csharp
public interface IWingetService
{
    Task<bool> IsWingetAvailableAsync();
    Task<bool> InstallPackageAsync(string packageId);
    Task<string?> GetInstalledVersionAsync(string packageId);
    Task<bool> IsPackageInstalledAsync(string packageId);
}
```

### 4) `IMkvToolService`
MKVToolNix integration for probe and extract operations.

```csharp
public interface IMkvToolService
{
    Task<ProbeResult> ProbeAsync(string mkvPath);
    Task<string> ExtractTextAsync(string mkvPath, int trackId, string outSrt);
    Task<string> ExtractPgsAsync(string mkvPath, int trackId, string outSup);
}
```

### 5) `ISubtitleOcrService`
Subtitle Edit integration for PGS OCR processing.

```csharp
public interface ISubtitleOcrService
{
    Task OcrSupToSrtAsync(string supPath, string outSrt, string language, bool fixCommonErrors = true, bool removeHi = true);
}
```

> **Note:** Validate the exact CLI flags on your local Subtitle Edit build: run `SubtitleEdit.exe /help`. We'll keep flags behind options so they're easy to adjust.

### 6) `ISettingsService`
Configuration management with tool path persistence.

```csharp
public interface ISettingsService
{
    Task<AppSettings> LoadSettingsAsync();
    Task SaveSettingsAsync(AppSettings settings);
    Task<string> GetAppDataPathAsync();
}
```

---

## State (MVVM-friendly)

`ExtractionState : ObservableObject`
- **File Management:**
  - `string? MkvPath`
  - `ObservableCollection<SubtitleTrack> Tracks`
  - `SubtitleTrack? SelectedTrack`
- **Tool Status:**
  - `ToolStatus MkvToolNixStatus`
  - `ToolStatus SubtitleEditStatus`
  - `bool AreToolsAvailable` (computed property)
- **Settings:**
  - `bool PreferForced`
  - `string OcrLanguage`
- **UI State:**
  - `bool CanProbe` (computed: MkvPath + AreToolsAvailable)
  - `bool CanExtract` (computed: SelectedTrack + AreToolsAvailable)
  - `bool IsBusy` (toggle buttons/progress)
- **Logging:**
  - `string LogText` (append-only; also write to a rolling file)

---

## ViewModel

`MainViewModel : ObservableObject`
- **Core Commands:**
  - `PickMkvCommand` - File selection with drag & drop
  - `ProbeCommand` - MKV track analysis
  - `ExtractCommand` - Subtitle extraction workflow
- **Tool Management Commands:**
  - `InstallMkvToolNixCommand` - Winget installation
  - `InstallSubtitleEditCommand` - Winget installation
  - `BrowseMkvToolNixCommand` - Manual path selection
  - `BrowseSubtitleEditCommand` - Manual path selection
- **Logic:**
  - **Startup**: Auto-detect tools, update UI status, enable/disable features
  - **Probe**: call `IMkvToolService.ProbeAsync`, populate tracks, auto-select (forced first if enabled; else first UTF-8; else first PGS)
  - **Extract**:
    - If `S_TEXT/UTF8` → `ExtractTextAsync` to `.srt`  
    - If `S_HDMV/PGS` → `ExtractPgsAsync` to `.sup` → `ISubtitleOcrService.OcrSupToSrtAsync` to `.srt`
  - **Tool Management**: Handle installation, path validation, status updates
  - **Error Handling**: Graceful degradation, clear user feedback

> Uses `CommunityToolkit.Mvvm` for `ObservableObject` + `RelayCommand` to reduce boilerplate.

---

## Error Handling & Logging

### **Comprehensive Error Handling**
- **Missing Tools**: Clear status indicators with installation options
- **Process Failures**: Detailed error messages with next steps
- **Invalid Files**: Helpful descriptions and recovery suggestions
- **Network Issues**: Graceful handling of winget failures
- **Tool Validation**: Verify tool paths and versions before use

### **Logging System**
- **Real-time UI Logging**: Timestamped messages in UI log display
- **File Logging**: Rolling log files at `%AppData%\SrtExtractor\Logs\srt_YYYYMMDD.txt`
- **Tool Detection Events**: Log tool discovery, installation, and validation
- **Process Output**: Capture stdout/stderr from external tools
- **Error Recovery**: Log failed operations with context for debugging

### **User Feedback**
- **Status Indicators**: Visual feedback for tool availability
- **Progress Updates**: Real-time feedback during long operations
- **Error Messages**: Clear, actionable error descriptions
- **Success Confirmation**: Positive feedback for completed operations

---

## Accessibility & UX polish (later)

- Keyboard shortcuts: `Ctrl+O` (Pick MKV), `Ctrl+P` (Probe), `Ctrl+E` (Extract)
- Drag‑and‑drop MKV onto the window to set `MkvPath`
- Persist last used folder and preferences
- Status bar + progress ring during external process runs

---

## CLI Cheat Sheet (for reference)

```powershell
# List tracks (JSON):
mkvmerge.exe -J "Movie.mkv"

# Extract text subtitle track (ID 2) straight to SRT:
mkvextract.exe tracks "Movie.mkv" 2:"Movie.en.srt"

# Extract PGS subtitle track (ID 3) to SUP:
mkvextract.exe tracks "Movie.mkv" 3:"Movie.en.sup"

# OCR SUP → SRT with Subtitle Edit (English, try also: spa, deu, fra):
SubtitleEdit.exe /ocr "Movie.en.sup" srt /tesseract_language eng /output "Movie.en.srt" /fixcommonerrors /remove_text_for_hi
```

---

## Acceptance Criteria (MVP)

### **Core Functionality**
- ✅ Select an MKV and **probe** tracks successfully (with language, codec, forced flag).
- ✅ **Extract text subs** (`S_TEXT/UTF8`) to `.srt` in the same folder.
- ✅ **Extract PGS** (`S_HDMV/PGS`) to `.sup` and **OCR** to `.srt`.
- ✅ App remembers tool paths and preferences across runs.
- ✅ UI shows a simple, clear log; errors aren't silent.

### **Tool Management**
- ✅ **Automatic tool detection** on app startup with multi-layered strategy.
- ✅ **One-click winget installation** for missing tools.
- ✅ **Graceful degradation** - no crashes when tools are missing.
- ✅ **Clear status indicators** showing tool availability.
- ✅ **Manual path selection** as fallback option.

### **User Experience**
- ✅ **Real-time feedback** during tool installation and extraction.
- ✅ **Helpful error messages** with actionable next steps.
- ✅ **Drag & drop support** for MKV files.
- ✅ **Progress indicators** for long-running operations.

---

## Non‑Goals (MVP)

- Subtitle editing/timing UI
- Advanced batch mode, forced‑only filtering on image subs (flag detection is okay)
- Multi‑language OCR in one pass
- MKV remuxing/retiming beyond simple extraction

---

## Edge Cases & Notes

- Some discs may **mis‑flag forced**; heuristic: prefer tracks with “forced” in name or `forced_track=true` in `mkvmerge -J` properties.
- If multiple English tracks exist, choose by order: `forced UTF-8` → `UTF-8` → `forced PGS` → `PGS`.
- Subtitle Edit’s CLI flags can vary by version: verify with `SubtitleEdit.exe /help`.
- If tools are missing, prompt to open **Settings** to browse paths; provide winget command hints.

---

## Directory Layout (suggested)

```
SrtExtractor/
  SrtExtractor.Wpf/              # WPF app (UI)
    Views/
      MainWindow.xaml
    ViewModels/
      MainViewModel.cs
    State/
      ExtractionState.cs
    Services/
      MkvToolService.cs
      SubtitleOcrService.cs
      ProcessRunner.cs
      SettingsService.cs
    Models/
      SubtitleTrack.cs
      ProbeResult.cs
    App.xaml
    App.xaml.cs
    SrtExtractor.Wpf.csproj
  README.md
```

---

## Complete Implementation Plan

### **Phase 1: Foundation & Dependencies** 🏗️
- [ ] Add required NuGet packages (CommunityToolkit.Mvvm, System.Text.Json)
- [ ] Create project folder structure (Models, Services, ViewModels, State)
- [ ] Create data models (SubtitleTrack, ProbeResult, AppSettings, ToolStatus)
- [ ] Define all service interfaces (IProcessRunner, IMkvToolService, ISubtitleOcrService, ISettingsService, IToolDetectionService, IWingetService)

### **Phase 2: Core Services** ⚙️
- [ ] Implement ProcessRunner service for external tool execution
- [ ] Implement ToolDetectionService with multi-layered detection strategy
- [ ] Implement WingetService for package installation and management
- [ ] Implement SettingsService for JSON configuration management
- [ ] Implement MkvToolService for MKVToolNix integration (probe/extract)
- [ ] Implement SubtitleOcrService for Subtitle Edit integration

### **Phase 3: State Management & MVVM** 📊
- [ ] Create ExtractionState class for MVVM data binding with tool status
- [ ] Implement MainViewModel with commands and tool detection logic
- [ ] Add graceful degradation for missing tools
- [ ] Implement startup tool detection and status updates

### **Phase 4: User Interface** 🎨
- [ ] Design MainWindow.xaml with tool status indicators and installation buttons
- [ ] Add real-time logging to UI and rolling file logging
- [ ] Implement drag & drop support for MKV files
- [ ] Add comprehensive error handling and user feedback

### **Phase 5: Integration & Testing** 🧪
- [ ] Test complete workflow: missing tools → installation → detection → extraction
- [ ] Test with various MKV files (text and PGS subtitles)
- [ ] Test error conditions and recovery scenarios
- [ ] Validate tool detection and installation flows

### **Phase 6: Polish & UX** ✨
- [ ] Add keyboard shortcuts and tooltips
- [ ] Implement progress indicators for long operations
- [ ] Add settings validation and reset functionality
- [ ] Final testing and bug fixes

---

## Cursor “Task Queue” (JSON)

```json
{
  "project": "SrtExtractor.Wpf",
  "tasks": [
    {"title": "Scaffold WPF app", "cmd": "dotnet new wpf -n SrtExtractor.Wpf"},
    {"title": "Add MVVM toolkit", "cmd": "dotnet add SrtExtractor.Wpf package CommunityToolkit.Mvvm"},
    {"title": "Create Models", "files": ["Models/SubtitleTrack.cs", "Models/ProbeResult.cs"]},
    {"title": "Create Services Interface", "files": ["Services/IProcessRunner.cs", "Services/IMkvToolService.cs", "Services/ISubtitleOcrService.cs", "Services/ISettingsService.cs"]},
    {"title": "Implement Services", "files": ["Services/ProcessRunner.cs", "Services/MkvToolService.cs", "Services/SubtitleOcrService.cs", "Services/SettingsService.cs"]},
    {"title": "Create State", "files": ["State/ExtractionState.cs"]},
    {"title": "Create ViewModel", "files": ["ViewModels/MainViewModel.cs"]},
    {"title": "Build MainWindow", "files": ["Views/MainWindow.xaml", "Views/MainWindow.xaml.cs"]},
    {"title": "Wire Commands", "desc": "Bind commands; enable/disable buttons via IsBusy/CanProbe/CanExtract"},
    {"title": "Logging", "desc": "Append log to UI and to %AppData%\\SrtExtractor\\Logs\\srt_YYYYMMDD.txt"},
    {"title": "Manual Test", "desc": "UTF-8 track, PGS track (SE OCR), missing tool paths, invalid MKV"}
  ]
}
```

---

## Example Implementations (snippets)

**Probe (`mkvmerge -J`)**

```csharp
var (code, stdout, stderr) = await _runner.RunAsync(mkvmergePath, $"-J \"{mkvPath}\"", ct);
using var doc = JsonDocument.Parse(stdout);
var tracks = doc.RootElement.GetProperty("tracks").EnumerateArray()
    .Where(t => string.Equals(t.GetProperty("type").GetString(), "subtitles", StringComparison.OrdinalIgnoreCase))
    .Select(t => new SubtitleTrack(
        id: t.GetProperty("id").GetInt32(),
        codec: t.TryGetProperty("codec", out var c) ? c.GetString() ?? "" : "",
        language: t.TryGetProperty("properties", out var p) && p.TryGetProperty("language", out var l) ? l.GetString() ?? "" : "",
        forced: t.TryGetProperty("properties", out var p2) && p2.TryGetProperty("forced_track", out var f) ? f.GetBoolean() : false,
        name: t.TryGetProperty("properties", out var p3) && p3.TryGetProperty("track_name", out var n) ? n.GetString() : null
    ))
    .ToList();
```

**Extract text or PGS (`mkvextract tracks`)**

```csharp
// Text:
await _runner.RunAsync(mkvextractPath, $"tracks \"{mkvPath}\" {trackId}:\"{outSrt}\"", ct);

// PGS:
await _runner.RunAsync(mkvextractPath, $"tracks \"{mkvPath}\" {trackId}:\"{outSup}\"", ct);
```

**OCR via Subtitle Edit**

```csharp
var args = $"/ocr \"{supPath}\" srt /tesseract_language {lang} /output \"{outSrt}\" /fixcommonerrors /remove_text_for_hi";
await _runner.RunAsync(subtitleEditPath, args, ct);
```

---

## Future Enhancements

- **Folder batch**: iterate over `.mkv` files, auto-pick best track by language/forced.
- **Forced-only extractor**: if a UTF‑8 track is “full” but PGS has “forced,” process both and keep only forced lines.
- **Better heuristics**: detect “forced” strings in track names when the flag isn’t set.
- **Multiple OCR langs**: try `"eng+spa"` combos if Subtitle Edit supports it for your version.
- **Telemetry (local only)**: anonymized counters for success/fail to guide polish.

---

## Licensing & Credit

- Respect each tool’s license (MKVToolNix GPL, Subtitle Edit GPL). We **do not bundle**; we shell out to user‑installed tools.
- This app is a convenience wrapper for personal use over lawful rips you own.

---

## Key Features Summary

### **🛠️ Tool Management**
- **Automatic Detection**: Multi-layered tool discovery on startup
- **Winget Integration**: One-click installation of required tools
- **Graceful Degradation**: App works even when tools are missing
- **Status Indicators**: Clear visual feedback for tool availability
- **Manual Override**: Browse buttons for custom tool paths

### **🎬 MKV Processing**
- **Text Subtitles**: Direct extraction from `S_TEXT/UTF8` tracks
- **PGS Subtitles**: Extract to SUP → OCR to SRT via Subtitle Edit
- **Smart Selection**: Auto-pick best track based on preferences
- **File Naming**: Configurable patterns with language/forced suffixes

### **🎨 User Experience**
- **Modern UI**: Clean WPF interface with real-time feedback
- **Drag & Drop**: Drop MKV files directly onto the window
- **Progress Tracking**: Visual indicators for long operations
- **Comprehensive Logging**: UI and file logging for debugging
- **Error Recovery**: Clear guidance for common issues

### **⚙️ Technical Architecture**
- **MVVM Pattern**: Clean separation of concerns
- **Service Layer**: Modular, testable design
- **Async/Await**: Non-blocking UI operations
- **Settings Persistence**: JSON configuration management
- **Error Handling**: Robust error recovery throughout

## Done = Ready to Use

Once the implementation is complete, users can:
1. **Launch SrtExtractor** - Tools auto-detect on startup
2. **Install missing tools** - One-click via winget if needed
3. **Pick an MKV** - Drag & drop or browse
4. **Probe tracks** - See all available subtitle tracks
5. **Select and Extract** - Get clean SRT files beside the movie

The app handles both text and PGS subtitles automatically, with clear feedback throughout the process.
