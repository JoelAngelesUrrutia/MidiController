using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Resources;
// WinForms también define Application (el ImplicitUsings de UseWindowsForms lo
// agrega como global using), así que hay que decir cuál queremos.
using Application = System.Windows.Application;
using Forms = System.Windows.Forms;
using MidiController.Models;
using MidiController.Services;

namespace MidiController;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    // CC7 = Channel Volume: lo usamos como volumen del sistema.
    private const int VolumeControlNumber = 7;
    private readonly MidiInputService _midiInputService;
    private readonly SystemVolumeService? _systemVolumeService;
    private readonly System.Drawing.Icon _trayIconImage;
    private readonly Forms.NotifyIcon _trayIcon;
    private bool _exitRequested;
    private bool _resourcesDisposed;

    public MainWindow()
    {
        InitializeComponent();

        try
        {
            _systemVolumeService = new SystemVolumeService();
        }
        catch (Exception ex)
        {
            AppendLog($"[error] Volumen del sistema no disponible: {ex.Message}");
        }

        _midiInputService = new MidiInputService();
        _midiInputService.MessageReceived += OnMidiMessageReceived;
        _midiInputService.ButtonPressed += OnMidiButtonPressed;
        _midiInputService.ControlChangeReceived += OnControlChangeReceived;
        _midiInputService.ConnectionChanged += OnMidiConnectionChanged;

        // El evento inicial se lanzó en el constructor, antes de suscribirnos.
        UpdateDeviceStatus(_midiInputService.IsConnected, _midiInputService.DeviceName);

        _trayIconImage = LoadAppIcon();
        _trayIcon = CreateTrayIcon();
    }

    private Forms.NotifyIcon CreateTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Abrir", null, (_, _) => ShowFromTray());
        menu.Items.Add("Salir", null, (_, _) => ExitApplication());

        var icon = new Forms.NotifyIcon
        {
            Icon = _trayIconImage,
            Text = "MidiController",
            ContextMenuStrip = menu,
            Visible = true,
        };

        icon.DoubleClick += (_, _) => ShowFromTray();
        return icon;
    }

    /// <summary>
    /// Icono incrustado en el ejecutable (Assets\app.ico). Si el recurso no
    /// estuviera disponible, se usa el genérico de Windows para no romper la app.
    /// </summary>
    private static System.Drawing.Icon LoadAppIcon()
    {
        try
        {
            StreamResourceInfo? resource =
                Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"));

            if (resource is not null)
            {
                using Stream stream = resource.Stream;
                return new System.Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize);
            }
        }
        catch (Exception)
        {
            // Sin recurso: seguimos con el icono por defecto.
        }

        return System.Drawing.SystemIcons.Application;
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitApplication()
    {
        RequestExit();
    }

    /// <summary>
    /// Cierre real del proceso (menú de la bandeja o fin de sesión de Windows).
    /// </summary>
    internal void RequestExit()
    {
        _exitRequested = true;

        if (IsLoaded)
        {
            Close();
            return;
        }

        // Se arrancó con --background y nunca se mostró: no hay ventana que
        // cerrar, así que liberamos los recursos y apagamos la aplicación.
        DisposeResources();
        Application.Current.Shutdown();
    }

    private void DisposeResources()
    {
        if (_resourcesDisposed)
        {
            return;
        }

        _resourcesDisposed = true;
        _trayIcon.Visible = false;
        _trayIcon.Dispose();

        // SystemIcons.Application es compartido: no se libera.
        if (!ReferenceEquals(_trayIconImage, System.Drawing.SystemIcons.Application))
        {
            _trayIconImage.Dispose();
        }

        _midiInputService.Dispose();
        _systemVolumeService?.Dispose();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_exitRequested)
        {
            // La X solo oculta: seguimos escuchando en segundo plano.
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        DisposeResources();
        base.OnClosed(e);
        Application.Current.Shutdown();
    }

    private void OnMidiConnectionChanged(bool connected, string? deviceName)
    {
        RunOnUiThread(() => UpdateDeviceStatus(connected, deviceName));
    }

    /// <summary>
    /// Los eventos MIDI llegan en un hilo de fondo. Además, pueden llegar
    /// mientras la aplicación se está cerrando: ahí el Dispatcher ya no sirve.
    /// </summary>
    private void RunOnUiThread(Action action)
    {
        if (_resourcesDisposed)
        {
            return;
        }

        try
        {
            Dispatcher.BeginInvoke(action);
        }
        catch (InvalidOperationException)
        {
            // La aplicación se está cerrando; no hay UI que actualizar.
        }
    }

    private void UpdateDeviceStatus(bool connected, string? deviceName)
    {
        DeviceTextBlock.Text = connected
            ? $"Escuchando: {deviceName}"
            : "Sin dispositivos MIDI: esperando conexión…";
    }

    /// <summary>
    /// Llega desde el hilo de fondo de DryWetMidi: no tocar la UI directamente.
    /// </summary>
    private void OnMidiMessageReceived(string message)
    {
        RunOnUiThread(() => AppendLog(message));
    }

    /// <summary>
    /// También llega en el hilo de fondo; lo pasamos a la UI.
    /// </summary>
    private void OnMidiButtonPressed(MmcCommand command)
    {
        RunOnUiThread(() => HandleButton(command));
    }

    /// <summary>
    /// CC del controlador. CC7 mueve el volumen del sistema.
    /// </summary>
    private void OnControlChangeReceived(int channel, int controlNumber, int value)
    {
        if (controlNumber != VolumeControlNumber)
        {
            return;
        }

        RunOnUiThread(() =>
        {
            try
            {
                _systemVolumeService?.SetFromMidiValue(value);
            }
            catch (Exception ex)
            {
                AppendLog($"[error] No se pudo cambiar el volumen: {ex.Message}");
            }
        });
    }

    private void HandleButton(MmcCommand command)
    {
        // Mapeo provisional; más adelante será configurable desde la UI.
        try
        {
            switch (command)
            {
                case MmcCommand.Play:
                    MediaKeyService.PlayPause();
                    break;

                case MmcCommand.Stop:
                    MediaKeyService.PreviousTrack();
                    break;

                case MmcCommand.Record:
                    MediaKeyService.NextTrack();
                    break;
            }
        }
        catch (Win32Exception ex)
        {
            AppendLog($"[error] No se pudo enviar la tecla multimedia: {ex.Message}");
        }
    }

    private void AppendLog(string message)
    {
        MidiLogTextBox.AppendText(message + Environment.NewLine);
        MidiLogTextBox.ScrollToEnd();
    }
}
