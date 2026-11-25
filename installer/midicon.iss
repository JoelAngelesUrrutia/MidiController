; Script de Inno Setup para Midicon.
;
; Cómo compilar:
;   1) dotnet publish -c Release -r win-x64 --self-contained true ^
;        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ^
;        -o publish
;   2) "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\midicon.iss
;
; El resultado queda en installer\output\Midicon-Setup-<version>.exe

#define MyAppName "Midicon"
#define MyAppExeName "midicon.exe"
#define MyAppVersion GetVersionNumbersString("..\publish\" + MyAppExeName)
#define MyAppPublisher "Midicon"

[Setup]
; IMPORTANTE: este GUID identifica la app entre versiones. No cambiarlo.
AppId={{DA23385E-FE18-4765-9598-5674C1F9EBE6}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
; Instalación por usuario: no pide administrador y no toca Program Files.
; La app cambió de nombre: ignorar la carpeta de la versión anterior y usar
; siempre la nueva (la antigua se borra en [InstallDelete]).
DefaultDirName={localappdata}\Programs\{#MyAppName}
UsePreviousAppDir=no
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=output
OutputBaseFilename=Midicon-Setup-{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
; Mismo icono que la app, también para el setup y el desinstalador.
SetupIconFile=..\Assets\app.ico
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}
; El setup espera a que termine la instancia en la bandeja antes de reemplazar archivos.
AppMutex=midicon.singleinstance
CloseApplications=force
RestartApplications=no

[Tasks]
Name: "autostart"; Description: "Iniciar Midicon con Windows (en segundo plano)"; GroupDescription: "Opciones:"
Name: "desktopicon"; Description: "Crear un acceso directo en el escritorio"; GroupDescription: "Opciones:"; Flags: unchecked

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Excludes: "*.dylib"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
; Limpieza de la versión anterior, que se llamaba MidiController.
Type: filesandordirs; Name: "{localappdata}\Programs\MidiController"
Type: files; Name: "{autoprograms}\MidiController.lnk"
Type: files; Name: "{autodesktop}\MidiController.lnk"

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Borra el autostart de la versión anterior (apuntaba al .exe renombrado).
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: none; ValueName: "MidiController"; Flags: deletevalue
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "{#MyAppName}"; \
    ValueData: """{app}\{#MyAppExeName}"" --background"; \
    Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Iniciar {#MyAppName} ahora"; \
    Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM {#MyAppExeName}"; \
    Flags: runhidden skipifdoesntexist; RunOnceId: "KillMidicon"
; Por si quedó una instancia de la versión anterior.
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM MidiController.exe"; \
    Flags: runhidden skipifdoesntexist; RunOnceId: "KillLegacyMidiController"
