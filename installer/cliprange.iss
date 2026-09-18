; Per-user install: no elevation, and the native messaging registration lives in HKCU.
; Build the host first: dotnet publish host -c Release

#define AppVersion "0.1.0"
#define HostName "com.cliprange.host"

[Setup]
AppId={{B3C7E5D2-4A1F-4C0E-9B7A-6D2F1E8C5A31}
AppName=ClipRange
AppVersion={#AppVersion}
AppPublisher=ClipRange
DefaultDirName={localappdata}\Programs\ClipRange
PrivilegesRequired=lowest
DisableProgramGroupPage=yes
DisableDirPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=dist
OutputBaseFilename=ClipRange-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName=ClipRange

[Files]
Source: "..\host\bin\Release\net9.0\win-x64\publish\ClipRange.Host.exe"; DestDir: "{app}"
Source: "..\host\{#HostName}.json"; DestDir: "{app}"
; The extension cannot be installed by a program: the browser loads it from this folder.
Source: "..\extension\*"; DestDir: "{app}\extension"; Flags: recursesubdirs

[Registry]
; Brave reads the Chrome key as well (verified against the strings in Brave 153's chrome.dll).
Root: HKCU; Subkey: "Software\Google\Chrome\NativeMessagingHosts\{#HostName}"; ValueType: string; ValueData: "{app}\{#HostName}.json"; Flags: uninsdeletekey

[Run]
Filename: "{app}\extension"; Description: "Open the extension folder, to load it in your browser"; Flags: shellexec postinstall skipifsilent

[Messages]
FinishedLabel=ClipRange is installed.%n%nLast step, in Chrome or Brave: open the extensions page (chrome://extensions or brave://extensions), enable Developer mode, click "Load unpacked" and choose the "extension" folder inside:%n[name]%n%nThe extension options page then lets you download yt-dlp and ffmpeg.
