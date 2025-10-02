# SrtExtractor

A powerful and easy-to-use desktop application for extracting subtitles from MKV and MP4 video files and converting them to SRT format.

## 🎯 Overview

SrtExtractor simplifies the process of extracting subtitles from your video files. Whether you're dealing with MKV containers or MP4 files with embedded "Timed Text" subtitles, SrtExtractor provides a seamless experience to convert them into the widely compatible SRT format.

## ✨ Features

- **Multi-format Support**: Handles both MKV and MP4 video files
- **Automatic Tool Management**: Detects and auto-downloads necessary external tools
- **Smart Subtitle Detection**: Automatically finds and lists available subtitle tracks
- **Flexible Output**: Generates SRT files with customizable naming patterns
- **Real-time Logging**: Provides detailed logs of all operations
- **User-friendly Interface**: Clean, intuitive WPF interface
- **OCR Support**: Converts image-based subtitles (PGS) to text using Subtitle Edit
- **Preference Settings**: Choose between forced subtitles or closed captions

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

## 🔧 Requirements

- **Windows 10/11** (x64)
- **.NET 9.0 Runtime**
- **External Tools** (auto-detected and managed):
  - MKVToolNix (for MKV processing)
  - Subtitle Edit CLI (for OCR conversion)
  - FFmpeg (for MP4 processing)

### ⚠️ Windows 10 Users

**Important**: The automatic tool installation via winget requires **Windows 10 version 1809 (build 17763) or later**. If you're using an older version of Windows 10, you'll need to manually install MKVToolNix:

1. Download MKVToolNix from: https://mkvtoolnix.download/downloads.html
2. Install it to the default location
3. SrtExtractor will automatically detect the installation

The app will show a helpful dialog with download instructions if winget is not available.

**To check your Windows 10 version**: Press `Win + R`, type `winver`, and press Enter.

## 📦 Installation

1. **Download** the latest release from the [Releases](https://github.com/ZentrixLabs/SrtExtractor/releases) page
2. **Extract** the ZIP file to your desired location
3. **Run** `SrtExtractor.exe`

The application will automatically detect and download required external tools on first run.

## 🚀 Usage

### Basic Workflow

1. **Select Video File**: Click "Pick Video..." to choose your MKV or MP4 file
2. **Probe Tracks**: Click "Probe Tracks" to analyze the file for subtitle tracks
3. **Select Track**: Choose your preferred subtitle track from the list
4. **Extract**: Click "Extract Selected → SRT" to generate the SRT file

### Settings

- **Subtitle Preference**: Choose between "Prefer forced subtitles" or "Prefer CC (Closed Captions)"
- **OCR Language**: Set the language for OCR conversion (default: English)
- **File Pattern**: Customize output filename pattern (default: `{basename}.{lang}{forced}{cc}.srt`)

### File Naming Pattern

The output filename follows this pattern:
- `{basename}` - Original video filename (without extension)
- `{lang}` - Subtitle language code
- `{forced}` - ".forced" if it's a forced subtitle
- `{cc}` - ".cc" if it's a closed caption

Example: `Movie.eng.forced.srt` or `Show.eng.cc.srt`

## 🏗️ Architecture

Built with modern .NET 9 and WPF, SrtExtractor follows the MVVM pattern:

- **Models**: Data structures for tracks, settings, and tool status
- **ViewModels**: Business logic and UI state management
- **Views**: XAML-based user interface
- **Services**: External tool integration and file operations
- **State**: Observable state management for data binding

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

## 📝 Logging

SrtExtractor provides comprehensive logging:
- **UI Log**: Real-time display in the application
- **File Log**: Rolling daily logs saved to `C:\ProgramData\ZentrixLabs\SrtExtractor\Logs\`
- **Log Format**: `srt_YYYYMMDD.txt`

## 🎨 User Interface

- **Clean Design**: Modern, intuitive interface
- **Real-time Feedback**: Progress indicators and status updates
- **Tool Status**: Visual indicators for external tool availability
- **About Window**: Branding and credits information

## 🐛 Troubleshooting

### Common Issues

1. **Tools Not Found**: Use "Re-detect Tools" button or check tool installation
2. **Extraction Fails**: Verify the selected track is a supported format
3. **OCR Issues**: Ensure Subtitle Edit CLI is properly built and available

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