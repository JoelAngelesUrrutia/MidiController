# MidiController

Escucha un teclado MIDI (preferentemente el **AKM322**) y traduce sus controles:

| Control MIDI | Acción |
|---|---|
| MMC Play | Play/Pausa |
| MMC Stop | Pista anterior |
| MMC Record | Pista siguiente |
| CC #7 (Channel Volume) | Volumen general de Windows |

La app vive en la bandeja del sistema: cerrar la ventana solo la oculta y sigue
escuchando. Si se desconecta el teclado, espera y se reconecta solo cuando
vuelve a aparecer, sin reiniciar.

## Requisitos

- Windows 10/11 x64.
- Para compilar: .NET SDK 10.
- Para generar el instalador: [Inno Setup 6](https://jrsoftware.org/isinfo.php)
  (`winget install --id JRSoftware.InnoSetup -e`).

## Uso

- **Primer arranque**: muestra la ventana con el log de eventos MIDI.
- **Cerrar (X)**: oculta la ventana; el proceso sigue en la bandeja.
- **Doble clic / menú "Abrir"** en el icono de la bandeja: vuelve a mostrar la ventana.
- **Menú "Salir"**: cierra el proceso de verdad.
- **`--background`**: arranca directamente oculto (lo usa el autostart de Windows).
- Lanzar el `.exe` con la app ya en ejecución solo hace visible la ventana existente.

## Compilar

```powershell
dotnet build
```

## Publicar

```powershell
dotnet publish MidiController.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:DebugType=None -p:DebugSymbols=false `
  -o publish
```

Queda un único `publish\MidiController.exe` autocontenido (no hace falta instalar
el runtime de .NET en la máquina destino).

## Crear el instalador

```powershell
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\MidiController.iss
```

Si Inno se instaló por usuario, `ISCC.exe` está en:

```text
%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe
```

Resultado: `installer\output\MidiController-Setup-<versión>.exe`.

El setup:

- Instala por usuario en `%LOCALAPPDATA%\Programs\MidiController` (sin UAC).
- Crea accesos directos en el menú Inicio (y opcionalmente en el escritorio).
- Opción marcada por defecto: iniciar con Windows en segundo plano, mediante
  `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` con el argumento `--background`.
- Al actualizar/desinstalar cierra la instancia en ejecución y borra el autostart.

La versión del instalador se toma del `Version` del `.csproj`, así que hay que
volver a publicar antes de recompilar el setup.

## Publicar una versión (GitHub Releases)

El workflow `.github/workflows/release.yml` compila todo en un runner de Windows
y adjunta el instalador al release. Para sacar una versión:

1. Subir el cambio de versión (si lo hay) en `MidiController.csproj`:

   ```powershell
   git add .
   git commit -m "version 1.0.0"
   git push origin main
   ```

2. Crear y empujar el tag con la misma versión:

   ```powershell
   git tag v1.0.0
   git push origin v1.0.0
   ```

El tag `v1.0.0` genera automáticamente el release **MidiController 1.0.0** con:

- `MidiController-Setup-1.0.0.0.exe` (instalador generado por Inno Setup).
- `SHA256SUMS.txt` para verificar la descarga.

La versión del ejecutable se fuerza con `-p:Version=<tag sin la v>`, así que el
instalador y el tag siempre coinciden.

También se puede lanzar a mano desde la pestaña **Actions → Release → Run
workflow**: en ese caso solo compila y deja el instalador como artefacto del
run, sin crear release. Para un release manual desde la web de GitHub basta con
`Draft a new release`, crear el tag y arrastrar el `.exe` de
`installer\output\`.

## Estructura

```
App.xaml(.cs)          Instancia única, arranque oculto y apagado explícito
MainWindow.xaml(.cs)   Log, bandeja del sistema y estado de conexión MIDI
Models/MmcCommand.cs   Comandos MMC reconocidos
Services/MidiInputService.cs     Escucha MIDI + reconexión por DevicesWatcher
Services/KeyMediaService.cs     MediaKeyService: teclas multimedia (SendInput)
Services/SystemVolumeService.cs Volumen general (NAudio / Core Audio)
installer/MidiController.iss    Script de Inno Setup
```

## Notas

- El instalador no está firmado: Windows SmartScreen puede pedir confirmación
  ("Más información" → "Ejecutar de todas formas").
- El icono de la bandeja es el genérico de Windows. Para uno propio hay que
  añadir un `.ico` y cargarlo como recurso embebido.
