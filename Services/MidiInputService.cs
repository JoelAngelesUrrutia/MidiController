using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using Melanchall.DryWetMidi.MusicTheory;
using MidiController.Models;

namespace MidiController.Services;

/// <summary>
/// Escucha el primer dispositivo MIDI de entrada disponible (preferentemente
/// el AKM322) y se reconecta solo cuando se conecta o desconecta un dispositivo.
/// </summary>
public sealed class MidiInputService : IDisposable
{
    private const string PreferredDeviceName = "AKM322";

    private readonly object _sync = new();
    private InputDevice? _inputDevice;
    private bool _disposed;

    /// <summary>Hay un dispositivo abierto y escuchando.</summary>
    public bool IsConnected => _inputDevice is not null;

    /// <summary>Nombre del dispositivo abierto, o null si no hay ninguno.</summary>
    public string? DeviceName { get; private set; }

    /// <summary>
    /// (conectado, nombre del dispositivo). ¡Ojo! Llega en un hilo de fondo.
    /// </summary>
    public event Action<bool, string?>? ConnectionChanged;

    /// <summary>
    /// Se dispara por cada evento MIDI recibido, con el mensaje ya formateado.
    /// ¡Ojo! Llega en un hilo de fondo: la UI debe actualizarse con el Dispatcher.
    /// </summary>
    public event Action<string>? MessageReceived;

    /// <summary>
    /// Se dispara cuando el controlador envía un comando MMC reconocido
    /// (Play, Stop, Record...). ¡Ojo! Llega en un hilo de fondo.
    /// </summary>
    public event Action<MmcCommand>? ButtonPressed;

    /// <summary>
    /// Se dispara por cada Control Change: (canal 1-16, número de control, valor).
    /// ¡Ojo! Llega en un hilo de fondo.
    /// </summary>
    public event Action<int, int, int>? ControlChangeReceived;

    public MidiInputService()
    {
        // DevicesWatcher avisa cuando entra o sale cualquier dispositivo MIDI.
        DevicesWatcher.Instance.DeviceAdded += OnDeviceAdded;
        DevicesWatcher.Instance.DeviceRemoved += OnDeviceRemoved;

        TryOpenDevice();
    }

    private void OnDeviceAdded(object? sender, DeviceAddedRemovedEventArgs e)
    {
        // En DeviceAdded el dispositivo sí es utilizable.
        if (e.Device is InputDevice)
        {
            TryOpenDevice();
        }
    }

    private void OnDeviceRemoved(object? sender, DeviceAddedRemovedEventArgs e)
    {
        // En DeviceRemoved NO se puede usar e.Device (ni leer su Name),
        // pero GetType() sí funciona.
        if (e.Device is InputDevice)
        {
            CloseDevice();
            TryOpenDevice(); // puede quedar otro dispositivo conectado
        }
    }

    private void TryOpenDevice()
    {
        string? openedName = null;

        lock (_sync)
        {
            if (_disposed || _inputDevice is not null)
            {
                return;
            }

            List<InputDevice> available;
            try
            {
                available = InputDevice.GetAll().ToList();
            }
            catch
            {
                return; // todavía no hay subsistema MIDI disponible
            }

            if (available.Count == 0)
            {
                return;
            }

            InputDevice selected =
                available.FirstOrDefault(d =>
                    d.Name.Contains(PreferredDeviceName, StringComparison.OrdinalIgnoreCase))
                ?? available[0];

            // Los que no vamos a usar no deben quedarse abiertos.
            foreach (InputDevice unused in available.Where(d => !ReferenceEquals(d, selected)))
            {
                try
                {
                    unused.Dispose();
                }
                catch
                {
                    // Si ya desapareció, no hay nada que hacer.
                }
            }

            try
            {
                selected.EventReceived += OnEventReceived;
                selected.StartEventsListening();
                DeviceName = selected.Name;
            }
            catch
            {
                selected.EventReceived -= OnEventReceived;

                try
                {
                    selected.Dispose();
                }
                catch
                {
                    // Ídem: el dispositivo pudo desaparecer.
                }

                DeviceName = null;
                return;
            }

            _inputDevice = selected;
            openedName = DeviceName;
        }

        ConnectionChanged?.Invoke(true, openedName);
    }

    private void CloseDevice()
    {
        InputDevice? device;

        lock (_sync)
        {
            device = _inputDevice;
            if (device is null)
            {
                return;
            }

            _inputDevice = null;
            DeviceName = null;
        }

        try
        {
            device.EventReceived -= OnEventReceived;
            device.StopEventsListening();
        }
        catch
        {
            // Si lo desenchufaron, detener la escucha puede fallar.
        }
        finally
        {
            try
            {
                device.Dispose();
            }
            catch
            {
                // Ídem: el dispositivo ya no existe.
            }
        }

        ConnectionChanged?.Invoke(false, null);
    }

    /// <summary>
    /// Handler del evento MIDI.
    /// </summary>
    private void OnEventReceived(object? sender, MidiEventReceivedEventArgs e)
    {
        if (e.Event is SysExEvent sysEx && TryGetMmcCommand(sysEx.Data, out MmcCommand command))
        {
            ButtonPressed?.Invoke(command);
        }

        if (e.Event is ControlChangeEvent controlChange)
        {
            ControlChangeReceived?.Invoke(
                (byte)controlChange.Channel + 1,
                controlChange.ControlNumber,
                controlChange.ControlValue);
        }

        MessageReceived?.Invoke(FormatEvent(e.Event));
    }

    private static string FormatEvent(MidiEvent midiEvent)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");

        return midiEvent switch
        {
            // Algunos teclados mandan Note On con velocidad 0 en lugar de Note Off.
            NoteOnEvent { Velocity: var velocity } noteOn when velocity == 0 =>
                FormatNote("NoteOff", timestamp, noteOn.Channel, noteOn.NoteNumber, noteOn.Velocity),

            NoteOnEvent noteOn =>
                FormatNote("NoteOn ", timestamp, noteOn.Channel, noteOn.NoteNumber, noteOn.Velocity),

            NoteOffEvent noteOff =>
                FormatNote("NoteOff", timestamp, noteOff.Channel, noteOff.NoteNumber, noteOff.Velocity),

            ControlChangeEvent controlChange =>
                $"[{timestamp}] CC       ch{(byte)controlChange.Channel + 1}  " +
                $"#{controlChange.ControlNumber}  valor {controlChange.ControlValue}",

            PitchBendEvent pitchBend =>
                $"[{timestamp}] PitchBend ch{(byte)pitchBend.Channel + 1}  valor {pitchBend.PitchValue}",

            SysExEvent sysEx =>
                FormatSysEx(timestamp, sysEx.Data),

            _ => $"[{timestamp}] {midiEvent.EventType}"
        };
    }

    private static string FormatNote(
        string kind,
        string timestamp,
        FourBitNumber channel,
        SevenBitNumber noteNumber,
        SevenBitNumber velocity)
    {
        string noteName = NoteUtilities.GetNoteName(noteNumber).ToString().Replace("Sharp", "#");
        int octave = NoteUtilities.GetNoteOctave(noteNumber);

        return $"[{timestamp}] {kind} ch{(byte)channel + 1}  " +
               $"nota {noteNumber} ({noteName}{octave})  vel {velocity}";
    }

    /// <summary>
    /// Reconoce los SysEx del controlador. Los botones de transporte envían MMC:
    /// F0 7F 7F 06 &lt;comando&gt; F7, que en Data llega como 7F 7F 06 &lt;comando&gt; F7.
    /// </summary>
    private static string FormatSysEx(string timestamp, byte[] data)
    {
        if (TryGetMmcCommand(data, out MmcCommand command))
        {
            return $"[{timestamp}] MMC: {command}";
        }

        if (IsMmcMessage(data))
        {
            return $"[{timestamp}] MMC: Desconocido (0x{data[3]:X2})";
        }

        // SysEx que no reconocemos: mostramos los bytes crudos.
        return $"[{timestamp}] SysEx ({data.Length} bytes)  {FormatBytes(data)}";
    }

    // F0 7F 7F 06 <comando> F7 -> en Data llega 7F 7F 06 <comando> F7
    private static bool IsMmcMessage(byte[] data) =>
        data.Length >= 4 && data[0] == 0x7F && data[1] == 0x7F && data[2] == 0x06;

    private static bool TryGetMmcCommand(byte[] data, out MmcCommand command)
    {
        command = default;

        if (!IsMmcMessage(data))
        {
            return false;
        }

        MmcCommand candidate = (MmcCommand)data[3];
        if (!Enum.IsDefined(candidate))
        {
            return false;
        }

        command = candidate;
        return true;
    }

    private static string FormatBytes(byte[] data)
    {
        // BitConverter da "F0-00-20-..."; lo dejo con espacios para comparar a ojo.
        return BitConverter.ToString(data).Replace('-', ' ');
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        DevicesWatcher.Instance.DeviceAdded -= OnDeviceAdded;
        DevicesWatcher.Instance.DeviceRemoved -= OnDeviceRemoved;

        CloseDevice();
    }
}
