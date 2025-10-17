; Define source directory for build output
#ifndef SrtExtractorBin
  #define SrtExtractorBin "SrtExtractor\bin\Release\net9.0-windows"
#endif

; Optional signing configuration injected via command-line defines
#ifndef EnableSigning
  #define EnableSigning "0"
#endif

#ifndef MyCertThumbprint
  #define MyCertThumbprint ""
#endif

#ifndef MyTimestampUrl
  #define MyTimestampUrl "https://timestamp.digicert.com"
#endif

[Setup]
AppName=SrtExtractor
AppVersion={#MyAppVersion}
AppPublisher=ZentrixLabs
AppPublisherURL=https://zentrixlabs.net/
AppSupportURL=https://github.com/ZentrixLabs/SrtExtractor
AppUpdatesURL=https://github.com/ZentrixLabs/SrtExtractor/releases
DefaultDirName={autopf}\SrtExtractor
DefaultGroupName=SrtExtractor
AllowNoIcons=yes
LicenseFile=LICENSE.txt
OutputDir=artifacts
OutputBaseFilename=SrtExtractorInstaller
SetupIconFile=SrtExtractor.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
UninstallDisplayIcon={app}\SrtExtractor.exe
UninstallDisplayName=SrtExtractor - MKV/MP4 Subtitle Extractor

; Sign uninstaller when signing is enabled
SignedUninstaller={#iif(EnableSigning == "1", "yes", "no")}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1

[Files]
; Main application files
Source: "{#SrtExtractorBin}\*.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SrtExtractorBin}\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SrtExtractorBin}\*.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SrtExtractorBin}\*.pdb"; DestDir: "{app}"; Flags: ignoreversion; Attribs: hidden
Source: "{#SrtExtractorBin}\*.nocr"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

; Bundled tools with subdirectories
Source: "{#SrtExtractorBin}\tesseract-bin\*"; DestDir: "{app}\tesseract-bin"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SrtExtractorBin}\tessdata\*"; DestDir: "{app}\tessdata"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SrtExtractorBin}\mkvtoolnix-bin\*"; DestDir: "{app}\mkvtoolnix-bin"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SrtExtractorBin}\ffmpeg-bin\*"; DestDir: "{app}\ffmpeg-bin"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

; Documentation
Source: "LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\SrtExtractor"; Filename: "{app}\SrtExtractor.exe"; WorkingDir: "{app}"; IconFilename: "{app}\SrtExtractor.exe"
Name: "{group}\{cm:UninstallProgram,SrtExtractor}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\SrtExtractor"; Filename: "{app}\SrtExtractor.exe"; WorkingDir: "{app}"; IconFilename: "{app}\SrtExtractor.exe"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\SrtExtractor"; Filename: "{app}\SrtExtractor.exe"; WorkingDir: "{app}"; IconFilename: "{app}\SrtExtractor.exe"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\SrtExtractor.exe"; Description: "{cm:LaunchProgram,SrtExtractor}"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
  // Note: .NET 9.0 Runtime check removed to avoid compilation issues
  // Users will need to install .NET 9.0 Desktop Runtime manually if not present
end;

// Configure SignTool when signing is enabled via defines
#if EnableSigning == "1"
[Setup]
SignTool="signtool sign /sha1 $q$MyCertThumbprint$q$ /fd SHA256 /td SHA256 /tr $q$MyTimestampUrl$q$ $f"
#endif
