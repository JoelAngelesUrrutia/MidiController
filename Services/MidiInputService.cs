using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using Melanchall.DryWetMidi.MusicTheory;
using MidiController.Models;

namespace MidiController.Services;

/// <summary>
/// Abre el primer dispositivo MIDI de entrada disponible y expone los eventos
/// que llegan, ya formateados como texto listo para mostrar.
/// </summary>
public sealed class MidiInputService : IDisposable
{
    private readonly InputDevice _inputDevice;
    private bool _disposed;

    /// <summary>Nombre del dispositivo que se está escuchando.</summary>
    public string DeviceName => _inputDevice.Name;

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

    public MidiInputService()
    {
        _inputDevice = InputDevice.GetAll().FirstOrDefault()
            ?? throw new InvalidOperationException("No hay dispositivos MIDI de entrada conectados.");

        _inputDevice.EventReceived += OnEventReceived;
        _inputDevice.StartEventsListening();
    }

    /// <summary>
    /// Handler del evento
    /// </summary>
    private void OnEventReceived(object? sender, MidiEventReceivedEventArgs e)
    {
        if (e.Event is SysExEvent sysEx && TryGetMmcCommand(sysEx.Data, out MmcCommand command))
        {
            ButtonPressed?.Invoke(command);
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
        // BitConverter da "F0-00-20-..." ; lo dejo con espacios para comparar a ojo.
        return BitConverter.ToString(data).Replace('-', ' ');
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _inputDevice.EventReceived -= OnEventReceived;
        _inputDevice.StopEventsListening();
        _inputDevice.Dispose();
    }
}