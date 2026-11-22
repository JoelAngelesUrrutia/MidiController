; Script de Inno Setup para MidiController.
;
; Cómo compilar:
;   1) dotnet publish -c Release -r win-x64 --self-contained true ^
;        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ^
;        -o publish
;   2) "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\MidiController.iss
;
; El resultado queda en installer\output\MidiController-Setup-<version>.exe

#define MyAppName "MidiController"
#define MyAppExeName "MidiController.exe"
#define MyAppVersion GetVersionNumbersString("..\publish\" + MyAppExeName)
#define MyAppPublisher "MidiController"

[Setup]
; IMPORTANTE: este GUID identifica la app entre versiones. No cambiarlo.
AppId={{DA23385E-FE18-4765-9598-5674C1F9EBE6}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
; Instalación por usuario: no pide administrador y no toca Program Files.
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=output
OutputBaseFilename=MidiController-Setup-{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
; Mismo icono que la app, también para el setup y el desinstalador.
SetupIconFile=..\Assets\app.ico
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}
; Permite que una instalación nueva cierre la versión que está en la bandeja.
AppMutex=MidiController.SingleInstance
CloseApplications=force
RestartApplications=no

[Tasks]
Name: "autostart"; Description: "Iniciar MidiController con Windows (en segundo plano)"; GroupDescription: "Opciones:"
Name: "desktopicon"; Description: "Crear un acceso directo en el escritorio"; GroupDescription: "Opciones:"; Flags: unchecked

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Excludes: "*.dylib"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "{#MyAppName}"; \
    ValueData: """{app}\{#MyAppExeName}"" --background"; \
    Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Iniciar {#MyAppName} ahora"; \
    Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM {#MyAppExeName}"; \
    Flags: runhidden skipifdoesntexist; RunOnceId: "KillMidiController"
