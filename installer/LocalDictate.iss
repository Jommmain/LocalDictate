; LocalDictate per-user installer.
; Build on Windows after publishing the full folder:
;   dotnet publish src\LocalDictate\LocalDictate.csproj -c Release -r win-x64 --self-contained false -o artifacts\LocalDictate
;   iscc installer\LocalDictate.iss
; Keep AppVersion in step with <Version> in LocalDictate.csproj.

#define MyAppName "LocalDictate"
#define MyAppVersion "0.2.6"
#define MyAppPublisher "LocalDictate"
#define MyAppURL "https://github.com/Jommmain/LocalDictate"
#define MyAppExeName "LocalDictate.exe"
#define PublishDir "..\artifacts\LocalDictate"

[Setup]
AppId={{7C3E1A44-9B2F-4E6D-8A11-5F0C2D6B91A4}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
DefaultDirName={localappdata}\Programs\LocalDictate
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\artifacts
OutputBaseFilename=LocalDictate-Setup-{#MyAppVersion}
SetupIconFile=..\src\LocalDictate\Assets\LocalDictate.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

[Tasks]
Name: "desktopicon"; Description: "Ярлык на рабочем столе"; GroupDescription: "Ярлыки:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Запустить LocalDictate"; Flags: nowait postinstall skipifsilent
