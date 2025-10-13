# SrtExtractor

[![Release](https://img.shields.io/github/v/release/ZentrixLabs/SrtExtractor?style=flat-square)](https://github.com/ZentrixLabs/SrtExtractor/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/ZentrixLabs/SrtExtractor/total?style=flat-square)](https://github.com/ZentrixLabs/SrtExtractor/releases)
[![License](https://img.shields.io/github/license/ZentrixLabs/SrtExtractor?style=flat-square)](LICENSE.txt)
[![.NET](https://img.shields.io/badge/.NET-9.0-blue?style=flat-square)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11-lightgrey?style=flat-square)](https://www.microsoft.com/windows)

A powerful and easy-to-use desktop application for extracting subtitles from MKV and MP4 video files and converting them to SRT format.

## 🎯 Overview

SrtExtractor simplifies the process of extracting subtitles from your video files. Whether you're dealing with MKV containers or MP4 files with embedded "Timed Text" subtitles, SrtExtractor provides a seamless experience to convert them into the widely compatible SRT format.

### 🆕 What's New in v2.5.0

**Architecture Refactoring & Code Quality:**
- **🏗️ Major Code Restructuring** - Eliminated God Object anti-pattern from MainViewModel
- **📉 46% Code Reduction** - MainViewModel reduced from 2,190 to 1,172 lines
- **🎯 Coordinator Pattern** - Introduced 5 focused coordinators for better separation of concerns
- **✨ Better Maintainability** - Each coordinator has a single, clear responsibility
- **🧪 Improved Testability** - Coordinators can be unit tested independently
- **📚 Enhanced Readability** - No file exceeds 600 lines, easier to understand
- **🔧 Zero Breaking Changes** - Fully backward compatible, all features preserved

**What This Means for You:**
- Same powerful features, better code architecture
- Foundation for faster future development
- More reliable and maintainable codebase
- Easier to extend with new features

**Previous Major Updates (v2.0.4 & v2.0):**
- **🔥 Fixed Tesseract OCR Quality** - OCR accuracy improved to ~100%
- **📦 Fully Bundled Tools** - No external dependencies (~530 MB, completely portable)
- **🎨 Clean Tab-Based Interface** - Separate Extract, Batch, History, and Tools tabs
- **👤 Humanized Track Information** - User-friendly labels instead of technical jargon
- **⚡ Speed Indicators** - Know instantly which tracks are fast (text) vs slow (OCR)
- **🚀 Performance Boost** - 2-3x faster codec detection and batch processing

## ✨ Features

- **Multi-format Support**: Handles both MKV and MP4 video files
- **Batch Processing**: Process multiple files simultaneously with drag & drop support
- **Network File Detection**: Automatically detects network drives with performance estimates
- **Automatic Tool Management**: Detects and auto-downloads necessary external tools
- **Smart Subtitle Detection**: Automatically finds and lists available subtitle tracks
- **Intelligent Track Recommendation**: Prioritizes SubRip/SRT subtitles over HDMV PGS when both are available
- **Flexible Output**: Generates SRT files with customizable naming patterns
- **Real-time Logging**: Provides detailed logs of all operations
- **User-friendly Interface**: Clean, intuitive WPF interface with modern design
- **Professional OCR**: High-quality image-based subtitle conversion using Tesseract OCR (~100% accuracy)
- **Bundled Tesseract**: No external installation required, fully portable and self-contained
- **SUP OCR Tool**: Dedicated window for processing SUP files directly with progress tracking
- **SUP Preservation**: Optional debugging mode to keep SUP files for inspection
- **Smart OCR Correction**: Automatically fixes common OCR errors using ZentrixLabs.OcrCorrection v1.0.1
- **Multi-Pass Correction**: Advanced correction system with ~841 professionally-tested patterns
- **Standalone Correction Tool**: Correct existing SRT files independently
- **Batch SRT Correction**: Process hundreds of SRT files simultaneously with bulk correction
- **Preference Settings**: Choose between forced subtitles or closed captions
- **Process Cancellation**: Cancel long-running operations with proper cleanup
- **Temporary File Management**: Automatic cleanup of temporary files during processing
- **High Performance**: Optimized regex patterns, intelligent caching, and efficient memory management
- **Responsive UI**: Debounced window operations and optimized batch processing for smooth experience

## 🛠️ Supported Subtitle Formats

### Direct Text Extraction
- `S_TEXT/UTF8` - UTF-8 encoded text subtitles
- `SubRip/SRT` - Standard SRT format
- `S_TEXT/3GPP` - 3GPP Timed Text (MP4)
- `S_TEXT/ASS` - Advanced SubStation Alpha
- `S_TEXT/SSA` - SubStation Alpha
- `S_TEXT/VTT` - WebVTT format

### OCR Conversion
- `S_HDMV/PGS` - Blu-ray PGS subtitles (converted via OCR)

## 🎯 Smart Track Recommendation

SrtExtractor features an intelligent recommendation engine that automatically selects the best subtitle track based on your preferences and available options:

### **Priority Order**
1. **SubRip/SRT Tracks** (Highest Priority) - Direct text extraction, no OCR required
2. **Other Text-based Tracks** (ASS, SSA, VTT) - Also direct extraction
3. **HDMV PGS Tracks** (Lower Priority) - Requires OCR conversion

### **Smart Selection Logic**
- **When both SubRip/SRT and HDMV PGS are available**: SubRip/SRT is automatically recommended
- **Quality-based selection**: Within each priority group, tracks are ranked by bitrate and frame count
- **User preferences respected**: Forced subtitles and closed captions are prioritized based on settings
- **Comprehensive logging**: Track selection decisions are logged for transparency

### **Example Scenarios**
- **File with SubRip/SRT + HDMV PGS**: SubRip/SRT track recommended (faster extraction, no OCR)
- **File with only HDMV PGS**: HDMV PGS track recommended (with OCR processing)
- **Multiple SubRip/SRT tracks**: Highest quality SubRip/SRT track recommended

## 🔧 Requirements

- **Windows 10/11** (x64)
- **.NET 9.0 Runtime**
- **That's it!** ✨

### 📦 Everything Bundled

**No external tools or installation required!** SrtExtractor comes with everything you need:
- **Tesseract OCR** - For image-based subtitle conversion (~160 MB)
- **MKVToolNix** - For MKV file processing (~37 MB)
- **FFmpeg** - For MP4 file processing (~334 MB)
- **Total size**: ~530 MB (completely portable)

**Just download, extract, and run!** No setup, no configuration, no internet connection needed.

**To check your Windows 10 version**: Press `Win + R`, type `winver`, and press Enter.

## 📦 Installation

1. **Download** the latest release from the [Releases](https://github.com/ZentrixLabs/SrtExtractor/releases) page
2. **Extract** the ZIP file to your desired location
3. **Run** `SrtExtractor.exe`

**That's it!** All tools are bundled - no setup, no downloads, no configuration required.

## 🚀 Usage

### Single File Processing (Extract Tab)

1. **Open Extract Tab**: The default view when you launch the app
2. **Select Video File**: Click "Pick Video..." or press Ctrl+O to choose your MKV or MP4 file
3. **Probe Tracks**: Click "Probe Tracks" or press Ctrl+P to analyze available subtitle tracks
4. **Review Tracks**: View human-friendly track information with speed indicators:
   - **⚡ Fast** - Text-based subtitles (instant extraction)
   - **🐢 OCR Required** - Image-based subtitles (requires processing time)
5. **Review Recommendation**: The smart recommendation engine automatically selects the best track
6. **Select Track**: Choose your preferred subtitle track from the list (or use the recommended one)
7. **Extract**: Click "Extract to SRT" or press Ctrl+E to generate the SRT file
8. **Auto-Correction**: OCR errors are automatically corrected using advanced multi-pass correction

### Batch Processing (Batch Tab)

Process multiple files efficiently with the dedicated Batch tab:

1. **Switch to Batch Tab**: Click the "Batch" tab or press Ctrl+B
2. **Confirm Settings**: Review your subtitle preferences shown in the settings panel
3. **Add Files**: Drag & drop MKV/MP4 files anywhere on the window to add them to the queue
4. **Review Queue**: Check the batch queue showing:
   - File sizes and network indicators (🌐)
   - Estimated processing times
   - Individual file status
5. **Process Batch**: Click "🚀 Process Batch" to extract subtitles from all files
6. **Monitor Progress**: Watch real-time progress with detailed status updates for each file
7. **Review Results**: Get a comprehensive summary of successful, failed, and cancelled files

#### Batch Mode Features
- **Network Detection**: Files on network drives show 🌐 indicator with realistic time estimates
- **Drag & Drop**: Simply drag files onto the application window to add them to the queue
- **Progress Tracking**: Real-time progress bar and "Processing X of Y files" counter
- **Cancellation Support**: Cancel batch processing at any time with proper cleanup
- **Detailed Summary**: Complete breakdown of processing results when finished
- **Automatic Cleanup**: Temporary files are automatically cleaned up during and after processing

### Standalone SRT Correction (Tools Tab)

Use the SRT Correction tool to fix OCR errors in existing SRT files:
1. **Switch to Tools Tab**: Click the "Tools" tab
2. **Launch Tool**: Click "SRT Correction" button or press Ctrl+R
3. **Select SRT File**: Choose the file you want to correct
4. **Auto-Correction**: Common OCR errors are automatically fixed
5. **Updated File**: File is corrected in-place with detailed statistics

### Batch SRT Correction (Tools Tab)

Process hundreds of SRT files simultaneously with the batch correction tool:

1. **Switch to Tools Tab**: Click the "Tools" tab
2. **Launch Batch Tool**: Click "Batch SRT Correction" button
3. **Select Folder**: Choose a folder containing SRT files (with option to include subfolders)
4. **Scan Files**: Click "🔍 Scan for SRT Files" to discover all SRT files
5. **Configure Options**: 
   - Include subfolders (recommended for large collections)
   - Create backup copies (recommended for safety)
6. **Start Processing**: Click "🚀 Start Batch Correction" to process all files
7. **Monitor Progress**: Watch real-time progress with file-by-file status updates
8. **Review Results**: Get detailed statistics on corrections applied per file

#### Batch SRT Correction Features
- **Lightning Fast**: Process hundreds of SRT files in minutes
- **Massive Corrections**: Typical results of 1000+ corrections per file
- **Safe Processing**: Optional backup creation before correction
- **Progress Tracking**: Real-time progress bar and file-by-file status
- **Cancellation Support**: Stop processing at any time
- **Detailed Results**: See exact correction count per file
- **Professional Output**: Clean, properly formatted SRT files

**Example Results**: 79 SRT files processed with 81,000+ total corrections applied!

### Settings (Tools Tab)

Access settings in the Tools tab or via menu Options → Settings:

- **Subtitle Preference**: Choose between "Prefer forced subtitles" or "Prefer CC (Closed Captions)"
- **OCR Language**: Set the language for OCR conversion (default: English)
- **File Pattern**: Customize output filename pattern (default: `{basename}.{lang}{forced}{cc}.srt`)
- **Multi-Pass Correction**: Configure advanced correction settings:
  - **Enable Multi-Pass**: Toggle advanced multi-pass correction (recommended)
  - **Correction Mode**: Choose between Quick (1 pass), Standard (3 passes), or Thorough (5 passes)
  - **Smart Convergence**: Automatically stop when no more corrections are found
  - **Max Passes**: Set maximum number of correction passes

### File Naming Pattern

The output filename follows this pattern:
- `{basename}` - Original video filename (without extension)
- `{lang}` - Subtitle language code
- `{forced}` - ".forced" if it's a forced subtitle
- `{cc}` - ".cc" if it's a closed caption

Example: `Movie.eng.forced.srt` or `Show.eng.cc.srt`

## 🏗️ Architecture

Built with modern .NET 9 and WPF, SrtExtractor follows the MVVM pattern with clean separation of concerns:

- **Models**: Data structures for tracks, settings, and tool status
- **ViewModels**: UI coordination, track selection, and settings management
- **Views**: XAML-based user interface with modern design
- **Coordinators** (NEW in v2.5.0): Focused business logic handlers
  - **ExtractionCoordinator**: Extraction strategies and OCR correction
  - **BatchCoordinator**: Batch queue management and processing
  - **FileCoordinator**: File picking, recent files, network detection
  - **ToolCoordinator**: Tool detection and path management
  - **CleanupCoordinator**: Temporary file cleanup operations
- **Services**: External tool integration and file operations
- **State**: Observable state management for data binding
- **Recommendation Engine**: Intelligent track selection prioritizing SubRip/SRT over HDMV PGS

## 🔧 External Tools

### MKVToolNix
- **Purpose**: MKV file probing and subtitle extraction
- **Installation**: Auto-detected or installed via winget
- **Tools Used**: `mkvmerge.exe`, `mkvextract.exe`

### Subtitle Edit CLI
- **Purpose**: OCR conversion of image-based subtitles
- **Installation**: Auto-built from source during development
- **Tool Used**: `seconv.exe`

### FFmpeg
- **Purpose**: MP4 file processing and subtitle extraction
- **Installation**: Auto-downloaded during development
- **Tools Used**: `ffmpeg.exe`, `ffprobe.exe`

## 🧠 Smart OCR Correction

SrtExtractor includes an intelligent OCR correction system that automatically fixes common errors:

### Automatic Fixes
- **Extra Spaces**: `T he` → `The`, `sh it` → `shit`
- **Missing Spaces**: `Yougotanybiscuits` → `You got any biscuits`
- **Character Substitutions**: `RipIey` → `Ripley`, `feeIdead` → `feel dead`
- **Apostrophe Issues**: `you)re` → `you're`, `won)t` → `won't`
- **Contractions**: `I am` → `I'm`, `you are` → `you're`
- **Common Phrases**: `Whatyoudoing` → `What you doing`

### Correction Patterns
The system uses regex-based patterns to identify and fix over 100 common OCR errors, ensuring your subtitles are clean and professional.

### 🚀 Multi-Pass Correction System

SrtExtractor now features an advanced multi-pass correction system that significantly improves subtitle quality:

#### Why Multi-Pass?
OCR errors often come in different types that require multiple passes to catch:
- **First Pass**: Catches obvious spacing and character issues
- **Second Pass**: Identifies missed contractions and phrase corrections
- **Third Pass**: Finds subtle character substitutions and remaining errors

#### Correction Modes
- **Quick Mode**: 1 pass (fastest, catches obvious errors)
- **Standard Mode**: 3 passes with smart convergence (recommended)
- **Thorough Mode**: 5 passes without convergence (maximum quality)

#### Smart Convergence
The system automatically detects when no more corrections are needed and stops early, saving processing time while ensuring quality.

#### Real-World Results
Multi-pass correction has been proven to find **additional corrections** on subsequent passes:
- **Example**: Blade (1998) subtitle file showed improvements across multiple correction runs
- **Typical Results**: Each pass finds 5-15% additional corrections missed by previous passes
- **Quality Improvement**: Significantly cleaner, more professional subtitle output

### Batch Correction Results
The correction system is incredibly effective when processing large collections:
- **Average**: 1000+ corrections per SRT file
- **Large Collections**: 80,000+ total corrections across 79 files
- **Processing Speed**: Hundreds of files processed in minutes
- **Quality Improvement**: Professional-grade subtitle formatting

## 📝 Logging

SrtExtractor provides comprehensive logging:
- **UI Log**: Real-time display in the application
- **File Log**: Rolling daily logs saved to `C:\ProgramData\ZentrixLabs\SrtExtractor\Logs\`
- **Log Format**: `srt_YYYYMMDD.txt`
- **Recommendation Logging**: Track selection decisions and reasoning are logged for transparency

## 🎨 User Interface (v2.0)

### Tab-Based Interface
- **Extract Tab**: Single-file extraction with focused workflow
- **Batch Tab**: Multiple-file processing with drag & drop queue
- **History Tab**: Recent files list and complete log viewer
- **Tools Tab**: Advanced tools, settings, and tool status

### Design Features
- **Clean Modern Design**: Microsoft 365-inspired light theme
- **Humanized Track Display**: User-friendly labels instead of technical jargon
- **Speed Indicators**: Visual cues showing fast (⚡) vs slow (🐢) subtitle tracks
- **3-Tier Button Hierarchy**: Clear visual priority for primary/secondary/tertiary actions
- **Real-time Feedback**: Progress indicators and status updates throughout
- **Keyboard Shortcuts**: Press F1 to view comprehensive shortcut help window
- **Network Indicators**: Visual indicators (🌐) for network files with time estimates
- **Accessibility**: Screen reader support and full keyboard navigation
- **Tool Status Display**: Visual indicators for external tool availability (MKVToolNix, FFmpeg, etc.)

## 🐛 Troubleshooting

### Common Issues

1. **Tools Not Found**: Use "Re-detect Tools" button or check tool installation
2. **Extraction Fails**: Verify the selected track is a supported format
3. **OCR Issues**: Ensure Subtitle Edit CLI is properly built and available
4. **Batch Mode Not Working**: Switch to the Batch tab and add files to the queue via drag & drop
5. **Network Files Slow**: Files on network drives will take longer - this is normal
6. **Temporary Files Left Behind**: Use the "🧹 Cleanup Temp Files" button if needed
7. **Cancellation Issues**: If processes don't stop, restart the application
8. **Batch SRT Correction Shows "None Found"**: Ensure you've selected a folder and clicked "Scan for SRT Files"
9. **SRT Files Not Updating**: Check that files aren't read-only or locked by another application
10. **Wrong Track Recommended**: Check the log for recommendation decisions; you can manually select a different track

### Log Files

Check the log files in `C:\ProgramData\ZentrixLabs\SrtExtractor\Logs\` for detailed error information.

## 🤝 Contributing

Contributions are welcome! Please feel free to submit issues and pull requests.

## 📄 License

This project is licensed under the MIT License - see the [LICENSE.txt](LICENSE.txt) file for details.

## 🙏 Acknowledgments

- **MKVToolNix** - Matroska tools for video container operations
- **Subtitle Edit** - OCR conversion for image-based subtitles  
- **FFmpeg** - Complete multimedia framework for MP4 processing

## 🔗 Links

- **Project Repository**: [https://github.com/ZentrixLabs/SrtExtractor](https://github.com/ZentrixLabs/SrtExtractor)
- **ZentrixLabs Website**: [https://zentrixlabs.net/](https://zentrixlabs.net/)

---

**Developed by ZentrixLabs** - Making video processing simple and efficient.