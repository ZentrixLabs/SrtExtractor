# SrtExtractor - Batch Mode Implementation Plan

## 🎯 Overview
Implement drag & drop batch processing mode to allow users to queue multiple movies and process them automatically with smart track selection.

## 📋 Feature Requirements

### 1. Network Detection (Basic Feature)
**Priority: HIGH** - Implement first as it benefits single file processing too

- [ ] **Network Drive Detection**
  - Detect mapped network drives (X:, Y:, etc.)
  - Detect UNC paths (\\server\share)
  - Use `DriveInfo.DriveType == DriveType.Network`

- [ ] **Time Estimation Logic**
  - **Network files**: ~1.2 GB/minute (based on real-world 32GB = 27min data)
  - **Local files**: ~5.0 GB/minute (estimated, much faster)
  - Formula: `estimatedMinutes = fileSizeBytes / (speed * 1024^3)`
  - Show file size and estimated time in warning

- [ ] **Non-Intrusive Warning UI**
  - Yellow warning box in Settings area
  - Shows when network file is detected
  - No blocking dialogs - just informational
  - Auto-calculate time estimates based on actual file size

- [ ] **UI Location**: Empty space in Settings section
  ```
  ┌─────────────────────────────────────────┐
  │ Settings                                │
  ├─────────────────────────────────────────┤
  │ ○ Prefer forced subtitles              │
  │ ○ Prefer CC (Closed Captions)          │
  │                                         │
  │ ┌─────────────────────────────────────┐ │
  │ │ ⚠️ Network File Detected            │ │
  │ │ File is on network drive X:         │ │
  │ │ File size: 32.0 GB                  │ │
  │ │ Estimated processing time: ~27 min  │ │
  │ └─────────────────────────────────────┘ │
  │                                         │
  │ OCR Language: [eng ▼]                   │
  └─────────────────────────────────────────┘
  ```

### 2. Batch Mode Core Features
**Priority: HIGH** - Main feature

- [ ] **Batch Mode Toggle**
  - Checkbox in Settings: "Enable Batch Mode"
  - Changes UI from single file to batch queue mode

- [ ] **Drag & Drop Support**
  - Enable drag & drop on main window
  - Accept MKV/MP4 files only
  - Visual feedback when dragging files over window
  - Prevent duplicate files in queue

- [ ] **Batch Queue UI**
  - ListBox/DataGrid showing queued files
  - Show file name, path, size, estimated time
  - Network indicator (🌐) for network files
  - Remove individual files, clear all queue
  - Reorder files if needed

### 3. Batch Processing Engine
**Priority: HIGH** - Core functionality

- [ ] **Auto-Selection Logic**
  - Use existing smart track selection
  - Apply user preference (CC/Forced/Default) to all files
  - "Do you trust us?" approach - minimal user intervention

- [ ] **Progress Tracking**
  - Current file being processed
  - Overall progress (3 of 15 files)
  - Estimated time remaining
  - Real-time status updates

- [ ] **Error Handling**
  - Skip problematic files, continue with others
  - Log errors for review
  - Don't stop entire batch for one failure

### 4. Batch Results & Reporting
**Priority: MEDIUM** - Polish features

- [ ] **Results Summary**
  - Total files processed
  - Success/failure counts
  - List of failed files with reasons
  - Processing time statistics

- [ ] **Enhanced Logging**
  - Per-file processing logs
  - Batch summary at end
  - Export results to file

## 🛠️ Implementation Order

### Phase 1: Network Detection (Basic Feature)
1. Add `NetworkDetectionService` with `IsNetworkPath()` method
2. Add network warning properties to `ExtractionState`
3. Implement warning UI in Settings section
4. Add time estimation logic with real-world performance data:
   - Network: ~1.2 GB/minute (32GB = 27min actual data)
   - Local: ~5.0 GB/minute (estimated)
5. Test with X: drive and UNC paths

### Phase 2: Batch Mode Foundation
1. Add batch mode toggle to Settings
2. Add batch queue properties to `ExtractionState`
3. Create `BatchFile` model class
4. Implement drag & drop on main window
5. Create batch queue UI (ListBox/DataGrid)

### Phase 3: Batch Processing
1. Create `BatchProcessingService`
2. Implement auto-selection for batch files
3. Add progress tracking and cancellation
4. Implement error handling and recovery
5. Add batch results summary

### Phase 4: Polish & Testing
1. Add batch queue management (remove, reorder, clear)
2. Enhance error reporting and logging
3. Add batch statistics and timing
4. Test with mixed local/network files
5. Performance optimization

## 📁 Files to Modify/Create

### New Files:
- `Services/Interfaces/INetworkDetectionService.cs`
- `Services/Implementations/NetworkDetectionService.cs`
- `Services/Interfaces/IBatchProcessingService.cs`
- `Services/Implementations/BatchProcessingService.cs`
- `Models/BatchFile.cs`
- `Models/BatchResult.cs`

### Modified Files:
- `State/ExtractionState.cs` - Add batch and network properties
- `ViewModels/MainViewModel.cs` - Add batch commands and logic
- `Views/MainWindow.xaml` - Add batch UI and network warning
- `App.xaml.cs` - Register new services in DI

## 🎯 Success Criteria

### User Experience:
- [ ] Drag movies from Explorer → SrtExtractor window
- [ ] See network warning without blocking dialogs
- [ ] Queue up multiple files (local + network)
- [ ] Set preference once, applies to all files
- [ ] Click "Process Batch" and walk away
- [ ] Return to completed SRT files

### Technical:
- [ ] Network detection works for mapped drives and UNC paths
- [ ] Drag & drop accepts only video files
- [ ] Batch processing handles errors gracefully
- [ ] Progress tracking is accurate and responsive
- [ ] Results are logged and summarized

## 🚀 Future Enhancements

- **Smart Scheduling**: Process local files first, network files in background
- **Resume Capability**: Restart batch from where it left off
- **File Watching**: Auto-detect new files added to queue
- **Cloud Integration**: Support for cloud storage paths
- **Advanced Filtering**: Filter by file size, age, etc.

---

**Next Steps**: Start with Phase 1 (Network Detection) as it provides immediate value for single file processing and sets up the foundation for batch mode.
