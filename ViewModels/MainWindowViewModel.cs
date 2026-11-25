using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Midicon.Models;
using Midicon.Services;

namespace Midicon.ViewModels;

/// <summary>
/// Estado y comportamiento de la ventana principal. No conoce WPF: los eventos
/// MIDI llegan en un hilo de fondo y se devuelven al hilo de UI con el
/// SynchronizationContext capturado al construirse (en App.OnStartup).
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject, IDisposable
{
    // CC7 = Channel Volume: lo usamos como volumen del sistema.
    private const int VolumeControlNumber = 7;

    private readonly MidiInputService _midiInputService;
    private readonly SystemVolumeService? _systemVolumeService;
    private readonly SynchronizationContext? _uiContext;
    private bool _disposed;

    /// <summary>Líneas del log de eventos MIDI, en orden de llegada.</summary>
    public ObservableCollection<string> Log { get; } = [];

    /// <summary>Texto con el estado de la conexión MIDI.</summary>
    [ObservableProperty]
    private string _deviceStatus = string.Empty;

    public MainWindowViewModel(
        MidiInputService midiInputService,
        SystemVolumeService? systemVolumeService)
    {
        _midiInputService = midiInputService;
        _systemVolumeService = systemVolumeService;
        _uiContext = SynchronizationContext.Current;

        _midiInputService.MessageReceived += OnMidiMessageReceived;
        _midiInputService.ButtonPressed += OnMidiButtonPressed;
        _midiInputService.ControlChangeReceived += OnControlChangeReceived;
        _midiInputService.ConnectionChanged += OnMidiConnectionChanged;

        // El evento inicial se lanzó en el constructor del servicio, antes de
        // suscribirnos.
        UpdateDeviceStatus(_midiInputService.IsConnected, _midiInputService.DeviceName);
    }

    /// <summary>
    /// Añade una línea al log. Se puede llamar desde cualquier hilo.
    /// </summary>
    public void AppendLog(string message)
    {
        if (SynchronizationContext.Current == _uiContext)
        {
            AppendLogCore(message);
            return;
        }

        RunOnUiThread(() => AppendLogCore(message));
    }

    private void AppendLogCore(string message) => Log.Add(message);

    private void OnMidiConnectionChanged(bool connected, string? deviceName)
    {
        RunOnUiThread(() => UpdateDeviceStatus(connected, deviceName));
    }

    /// <summary>
    /// Llega desde el hilo de fondo de DryWetMidi: no tocar la UI directamente.
    /// </summary>
    private void OnMidiMessageReceived(string message)
    {
        RunOnUiThread(() => AppendLogCore(message));
    }

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
                AppendLogCore($"[error] No se pudo cambiar el volumen: {ex.Message}");
            }
        });
    }

    private void UpdateDeviceStatus(bool connected, string? deviceName)
    {
        DeviceStatus = connected
            ? $"Escuchando: {deviceName}"
            : "Sin dispositivos MIDI: esperando conexión…";
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
            AppendLogCore($"[error] No se pudo enviar la tecla multimedia: {ex.Message}");
        }
    }

    /// <summary>
    /// Los eventos MIDI llegan en un hilo de fondo. Además, pueden llegar
    /// mientras la aplicación se está cerrando: ahí el Dispatcher ya no sirve.
    /// </summary>
    private void RunOnUiThread(Action action)
    {
        if (_disposed)
        {
            return;
        }

        if (_uiContext is null)
        {
            // Sin contexto de UI (por ejemplo, en tests): se ejecuta tal cual.
            action();
            return;
        }

        try
        {
            _uiContext.Post(_ => action(), null);
        }
        catch (InvalidOperationException)
        {
            // La aplicación se está cerrando; no hay UI que actualizar.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _midiInputService.MessageReceived -= OnMidiMessageReceived;
        _midiInputService.ButtonPressed -= OnMidiButtonPressed;
        _midiInputService.ControlChangeReceived -= OnControlChangeReceived;
        _midiInputService.ConnectionChanged -= OnMidiConnectionChanged;

        _midiInputService.Dispose();
        _systemVolumeService?.Dispose();
    }
}
