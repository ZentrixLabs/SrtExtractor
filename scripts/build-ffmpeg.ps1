param(
    [string]$Configuration = "Release", # or Debug
    [string]$Architecture = "x64" # x64 or x86
)

Write-Host "Building FFmpeg..." -ForegroundColor Green

# Navigate to the FFmpeg directory
$ffmpegPath = Join-Path (Split-Path $PSScriptRoot -Parent) "FFmpeg"
if (-not (Test-Path $ffmpegPath)) {
    Write-Error "FFmpeg not found at $ffmpegPath"
    exit 1
}

# Build FFmpeg
Push-Location $ffmpegPath
try {
    Write-Host "Configuring FFmpeg build..." -ForegroundColor Yellow
    
    # Configure FFmpeg with minimal dependencies for subtitle extraction
    $configureArgs = @(
        "--enable-gpl",
        "--enable-version3",
        "--enable-static",
        "--disable-shared",
        "--disable-debug",
        "--disable-doc",
        "--disable-ffmpeg",
        "--disable-ffplay",
        "--enable-ffprobe",
        "--disable-avdevice",
        "--disable-avfilter",
        "--disable-swscale",
        "--disable-swresample",
        "--disable-postproc",
        "--disable-network",
        "--disable-dct",
        "--disable-dwt",
        "--disable-lsp",
        "--disable-lzo",
        "--disable-mdct",
        "--disable-rdft",
        "--disable-fft",
        "--disable-faan",
        "--disable-pixelutils",
        "--disable-hwaccels",
        "--disable-hwaccel=h264_dxva2",
        "--disable-hwaccel=h264_d3d11va",
        "--disable-hwaccel=h264_nvdec",
        "--disable-hwaccel=hevc_dxva2",
        "--disable-hwaccel=hevc_d3d11va",
        "--disable-hwaccel=hevc_nvdec",
        "--disable-hwaccel=mpeg2_dxva2",
        "--disable-hwaccel=mpeg2_d3d11va",
        "--disable-hwaccel=mpeg2_nvdec",
        "--disable-hwaccel=vc1_dxva2",
        "--disable-hwaccel=vc1_d3d11va",
        "--disable-hwaccel=vc1_nvdec",
        "--disable-hwaccel=wmv3_dxva2",
        "--disable-hwaccel=wmv3_d3d11va",
        "--disable-hwaccel=wmv3_nvdec",
        "--disable-hwaccel=vp8_dxva2",
        "--disable-hwaccel=vp8_d3d11va",
        "--disable-hwaccel=vp8_nvdec",
        "--disable-hwaccel=vp9_dxva2",
        "--disable-hwaccel=vp9_d3d11va",
        "--disable-hwaccel=vp9_nvdec",
        "--disable-hwaccel=av1_dxva2",
        "--disable-hwaccel=av1_d3d11va",
        "--disable-hwaccel=av1_nvdec",
        "--disable-encoders",
        "--disable-decoders",
        "--enable-decoder=mov_text",
        "--enable-decoder=srt",
        "--enable-decoder=ass",
        "--enable-decoder=ssa",
        "--enable-decoder=webvtt",
        "--enable-decoder=subrip",
        "--enable-decoder=dvd_subtitle",
        "--enable-decoder=dvb_subtitle",
        "--enable-decoder=dvb_teletext",
        "--enable-decoder=hdmv_pgs_subtitle",
        "--enable-decoder=hdmv_text_subtitle",
        "--enable-decoder=jacosub",
        "--enable-decoder=microdvd",
        "--enable-decoder=mpl2",
        "--enable-decoder=pjs",
        "--enable-decoder=realtext",
        "--enable-decoder=sami",
        "--enable-decoder=srt",
        "--enable-decoder=stl",
        "--enable-decoder=subviewer",
        "--enable-decoder=subviewer1",
        "--enable-decoder=vplayer",
        "--enable-decoder=webvtt",
        "--enable-decoder=xsub",
        "--disable-muxers",
        "--enable-muxer=srt",
        "--enable-muxer=ass",
        "--enable-muxer=ssa",
        "--enable-muxer=webvtt",
        "--disable-demuxers",
        "--enable-demuxer=mov",
        "--enable-demuxer=mp4",
        "--enable-demuxer=m4v",
        "--enable-demuxer=matroska",
        "--enable-demuxer=mkv",
        "--enable-demuxer=matroska,webm",
        "--enable-demuxer=webm",
        "--disable-parsers",
        "--enable-parser=movtext",
        "--enable-parser=srt",
        "--enable-parser=ass",
        "--enable-parser=ssa",
        "--enable-parser=webvtt",
        "--disable-bsfs",
        "--disable-protocols",
        "--enable-protocol=file",
        "--disable-filters",
        "--disable-indevs",
        "--disable-outdevs",
        "--arch=$Architecture",
        "--target-os=win32",
        "--cross-prefix=",
        "--enable-cross-compile"
    )
    
    # Run configure
    & .\configure @configureArgs
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to configure FFmpeg"
        exit 1
    }
    
    Write-Host "Compiling FFmpeg..." -ForegroundColor Yellow
    & make -j4
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to build FFmpeg"
        exit 1
    }
    
    # Find the output directory
    $outputDir = Join-Path $ffmpegPath "ffbuild"
    $ffprobePath = Join-Path $outputDir "ffprobe.exe"
    
    if (-not (Test-Path $ffprobePath)) {
        Write-Error "ffprobe.exe not found after build at $ffprobePath"
        exit 1
    }

    # Define our application's output directory
    $ourOutputDir = Join-Path (Split-Path $PSScriptRoot -Parent) "SrtExtractor\bin\$Configuration\net9.0-windows"
    if (-not (Test-Path $ourOutputDir)) {
        New-Item -ItemType Directory -Path $ourOutputDir -Force | Out-Null
    }
    
    Write-Host "Copying FFmpeg executables to output directory..." -ForegroundColor Yellow
    
    # Copy ffprobe.exe (we only need this for subtitle extraction)
    Copy-Item $ffprobePath $ourOutputDir -Force
    
    # Create a symlink or copy ffmpeg.exe to ffprobe.exe location for compatibility
    $ffmpegPath = Join-Path $outputDir "ffmpeg.exe"
    if (Test-Path $ffmpegPath) {
        Copy-Item $ffmpegPath $ourOutputDir -Force
    }
    
    Write-Host "FFmpeg built successfully!" -ForegroundColor Green
    Write-Host "ffprobe.exe is now available at: $ourOutputDir\ffprobe.exe" -ForegroundColor Cyan
    Write-Host "ffmpeg.exe is now available at: $ourOutputDir\ffmpeg.exe" -ForegroundColor Cyan
        
} finally {
    Pop-Location
}
