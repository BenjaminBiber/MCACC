; Inno Setup script for MCACC. Built by scripts/publish-windows.ps1 -Installer:
;   iscc /DAppVersion=1.2.3 /DPublishDir=<publish dir> /DOutputDir=<artifacts> build\windows\mcacc.iss
;
; Installs per user (no admin rights) to %LOCALAPPDATA%\Programs\MCACC, so the autostart entry
; that MCACC writes points at a stable path.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef PublishDir
  #error PublishDir must point at the publish output
#endif
#ifndef OutputDir
  #define OutputDir "."
#endif

[Setup]
AppId={{6C1D3E7A-2B4F-4E8C-9A51-0F3B7D2C9E64}
AppName=MCACC
AppVersion={#AppVersion}
AppVerName=MCACC {#AppVersion}
AppPublisher=Benjamin Biber
AppPublisherURL=https://github.com/BenjaminBiber/MCACC
AppSupportURL=https://github.com/BenjaminBiber/MCACC/issues
DefaultDirName={localappdata}\Programs\MCACC
DefaultGroupName=MCACC
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
OutputDir={#OutputDir}
OutputBaseFilename=MCACC-{#AppVersion}-win-x64-setup
SetupIconFile=..\..\assets\icon\mcacc.ico
UninstallDisplayIcon={app}\MCACC.exe
UninstallDisplayName=MCACC
LicenseFile=..\..\LICENSE
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; MCACC keeps running in the notification area; the single-instance mutex lets setup ask to close it.
AppMutex=MCACC
CloseApplications=yes

[Languages]
Name: "de"; MessagesFile: "compiler:Languages\German.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\MCACC"; Filename: "{app}\MCACC.exe"
Name: "{autodesktop}\MCACC"; Filename: "{app}\MCACC.exe"; Tasks: desktopicon

[Registry]
; MCACC writes this autostart value itself; remove it together with the program.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "MCACC"; Flags: uninsdeletevalue dontcreatekey

[Run]
Filename: "{app}\MCACC.exe"; Description: "{cm:LaunchProgram,MCACC}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM MCACC.exe"; Flags: runhidden; RunOnceId: "StopMcacc"
