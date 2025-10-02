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

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1

[Files]
Source: "{#SrtExtractorBin}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
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
