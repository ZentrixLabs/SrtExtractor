[Setup]
AppName=SrtExtractor
AppVersion=1.0.0
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
SetupIconFile=SrtExtractor\SrtExtractor.ico
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
Source: "SrtExtractor\bin\Release\net9.0-windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\SrtExtractor"; Filename: "{app}\SrtExtractor.exe"; WorkingDir: "{app}"
Name: "{group}\{cm:UninstallProgram,SrtExtractor}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\SrtExtractor"; Filename: "{app}\SrtExtractor.exe"; WorkingDir: "{app}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\SrtExtractor"; Filename: "{app}\SrtExtractor.exe"; WorkingDir: "{app}"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\SrtExtractor.exe"; Description: "{cm:LaunchProgram,SrtExtractor}"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
  // Check if .NET 9.0 is installed
  if not IsDotNetInstalled('Microsoft.NETCore.App', '9.0.0') then
  begin
    MsgBox('SrtExtractor requires .NET 9.0 Runtime. Please install it from https://dotnet.microsoft.com/download/dotnet/9.0 and try again.', mbError, MB_OK);
    Result := False;
  end;
end;

function IsDotNetInstalled(const FrameworkName: String; const MinVersion: String): Boolean;
var
  Version: String;
begin
  Result := RegQueryStringValue(HKEY_LOCAL_MACHINE, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\' + FrameworkName, 'Version', Version) and
            (CompareVersion(Version, MinVersion) >= 0);
end;

function CompareVersion(Version1, Version2: String): Integer;
var
  P1, P2: Integer;
  N1, N2: Integer;
begin
  Result := 0;
  while (Version1 <> '') or (Version2 <> '') do
  begin
    P1 := Pos('.', Version1);
    P2 := Pos('.', Version2);
    if P1 = 0 then P1 := Length(Version1) + 1;
    if P2 = 0 then P2 := Length(Version2) + 1;
    N1 := StrToIntDef(Copy(Version1, 1, P1 - 1), 0);
    N2 := StrToIntDef(Copy(Version2, 1, P2 - 1), 0);
    if N1 < N2 then Result := -1
    else if N1 > N2 then Result := 1
    else Result := 0;
    if Result <> 0 then Exit;
    Delete(Version1, 1, P1);
    Delete(Version2, 1, P2);
  end;
end;
