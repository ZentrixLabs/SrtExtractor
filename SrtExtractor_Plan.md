# SrtExtractor (WPF, Windows) — Project Plan

**Goal:** A tiny Windows desktop app that extracts subtitles from MKV files.  
- If the subtitle track is **text** (`S_TEXT/UTF8`) → save straight to `.srt`.  
- If it’s **PGS** (`S_HDMV/PGS`) → extract `.sup`, then OCR to `.srt` via **Subtitle Edit** CLI.

---

## Mission Objective

Deliver a minimal, reliable **WPF (.NET 8)** app that wraps **MKVToolNix** and **Subtitle Edit** CLIs with a clean UX:

- **Pick MKV → Probe tracks → Select track → Extract → (If PGS) OCR → SRT.**
- Preserve original folder and name; append `.en.forced.srt` or `.en.srt` style suffixes.
- Keep logs visible in the UI and also write to a text log for debugging.

> Architecture pattern: **Services → State Objects → UI** (MVVM-friendly).

---

## Tech Stack

- **Framework:** .NET 8, **WPF**
- **Language:** C#
- **MVVM:** Optional: `CommunityToolkit.Mvvm` (recommended)  
- **External tools:**
  - **MKVToolNix**: `mkvmerge.exe`, `mkvextract.exe`
  - **Subtitle Edit**: `SubtitleEdit.exe` (for OCR of PGS → SRT)

> Install via winget (expected for dev/test machines):  
> ```powershell
> winget install MoritzBunkus.MKVToolNix
> winget install SubtitleEdit.SubtitleEdit
> ```

---

## External Tool Detection

At app start (or on first probe/extract), resolve executable paths in this order:

1. **User-specified path** (stored in settings)  
2. **PATH** lookup (use `where mkvmerge`, `where mkvextract`, `where SubtitleEdit`)  
3. **Common install locations** (e.g., `C:\Program Files\MKVToolNix\`, `C:\Program Files\Subtitle Edit\`)  
4. If not found, surface a **“Browse…”** button to set the path.

Store resolved paths in a JSON settings file under:  
`%AppData%\SrtExtractor\settings.json`

```jsonc
{
  "MkvMergePath": "C:\\Program Files\\MKVToolNix\\mkvmerge.exe",
  "MkvExtractPath": "C:\\Program Files\\MKVToolNix\\mkvextract.exe",
  "SubtitleEditPath": "C:\\Program Files\\Subtitle Edit\\SubtitleEdit.exe",
  "PreferForced": true,
  "DefaultOcrLanguage": "eng",
  "FileNamePattern": "{basename}.{lang}{forced}.srt" // forced becomes ".forced" if true else ""
}
```

---

## Minimal UX (WPF)

```
+-------------------------------------------------------+
| [Pick MKV…]   C:\Movies\MyFilm.mkv                    |
|                                                       |
| mkvmerge:      [ C:\Program Files\MKVToolNix\mkvmerge ]
| mkvextract:    [ C:\Program Files\MKVToolNix\mkvextract]
| SubtitleEdit:  [ C:\Program Files\Subtitle Edit\...   ]
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

- **Pick MKV…** → file dialog for `.mkv`
- **Probe Tracks** → calls `mkvmerge -J` and populates the list (auto-selects based on options)
- **Extract Selected → SRT** → text subs: `.srt` directly; PGS: `.sup` then OCR to `.srt`
- **Log** → real-time process output and errors

---

## Data & Models

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

### 2) `IMkvToolService`
- `Task<ProbeResult> ProbeAsync(string mkvPath)` → `mkvmerge -J`
- `Task<string> ExtractTextAsync(string mkvPath, int trackId, string outSrt)` → `mkvextract tracks`
- `Task<string> ExtractPgsAsync(string mkvPath, int trackId, string outSup)` → `mkvextract tracks`

### 3) `ISubtitleOcrService`
- `Task OcrSupToSrtAsync(string supPath, string outSrt, string language, bool fixCommonErrors = true, bool removeHi = true)`  
  → `SubtitleEdit.exe /ocr "in.sup" srt /tesseract_language <lang> /output "out.srt" /fixcommonerrors /remove_text_for_hi`

> **Note:** Validate the exact CLI flags on your local Subtitle Edit build: run `SubtitleEdit.exe /help`. We’ll keep flags behind options so they’re easy to adjust.

### 4) `ISettingsService`
- Load/save settings JSON under `%AppData%\SrtExtractor\settings.json`.
- Properties include tool paths, preferred OCR language, “prefer forced”, filename pattern.

---

## State (MVVM-friendly)

`ExtractionState : INotifyPropertyChanged`
- `string? MkvPath`
- `ObservableCollection<SubtitleTrack> Tracks`
- `SubtitleTrack? SelectedTrack`
- `bool PreferForced`
- `string OcrLanguage`
- `string LogText` (append-only; also write to a rolling file)
- `bool CanProbe`
- `bool CanExtract`
- `bool IsBusy` (toggle buttons/progress)

---

## ViewModel

`MainViewModel`
- Commands:
  - `PickMkvCommand`
  - `ProbeCommand`
  - `ExtractCommand`
- Logic:
  - **Probe**: call `IMkvToolService.ProbeAsync`, populate tracks, auto-select (forced first if enabled; else first UTF-8; else first PGS).
  - **Extract**:
    - If `S_TEXT/UTF8` → `ExtractTextAsync` to `.srt`  
    - If `S_HDMV/PGS` → `ExtractPgsAsync` to `.sup` → `ISubtitleOcrService.OcrSupToSrtAsync` to `.srt`
  - Build output file names from pattern.

> Consider `CommunityToolkit.Mvvm` for `ObservableObject` + `RelayCommand` to reduce boilerplate.

---

## Error Handling & Logging

- Surface non‑zero exit codes with clear messages and next steps (e.g., “Check tool path under Settings”).
- Write stdout/stderr into the UI log and a rolling log file at `%AppData%\SrtExtractor\Logs\srt_YYYYMMDD.txt`.
- If OCR fails, leave the `.sup` on disk and log the last 30 lines of stderr to help debugging.

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

- ✅ Select an MKV and **probe** tracks successfully (with language, codec, forced flag).
- ✅ **Extract text subs** (`S_TEXT/UTF8`) to `.srt` in the same folder.
- ✅ **Extract PGS** (`S_HDMV/PGS`) to `.sup` and **OCR** to `.srt`.
- ✅ App remembers tool paths and preferences across runs.
- ✅ UI shows a simple, clear log; errors aren’t silent.
- ✅ No crashes on bad inputs or missing tools (graceful prompts).

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

## Cursor‑Ready Task List (Checklist)

- [ ] Create WPF project `SrtExtractor.Wpf` targeting `net8.0-windows10.0.19041.0`
- [ ] Add `CommunityToolkit.Mvvm` NuGet (optional)
- [ ] Implement `IProcessRunner` + `ProcessRunner`
- [ ] Implement `IMkvToolService` (probe/extract)
- [ ] Implement `ISubtitleOcrService` (SE CLI wrapper)
- [ ] Implement `ISettingsService` (JSON in `%AppData%`)
- [ ] Create `ExtractionState` (INPC)
- [ ] Create `MainViewModel` with `Pick/Probe/Extract` commands
- [ ] Build `MainWindow.xaml` UI (as per mock)
- [ ] Wire up log to UI and rolling file
- [ ] Implement filename pattern + forced/lang suffixing
- [ ] Handle missing tools (browse + store paths)
- [ ] Manual tests with: UTF-8 subs, PGS subs, missing tools, wrong paths

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

## Done = Ready to Use

Once the checklist is green, you can:
1. Open SrtExtractor.
2. Pick an MKV.
3. Probe tracks.
4. Select and Extract.  
If it’s PGS, Subtitle Edit does the OCR and you’ll have a clean `.srt` beside the movie.
