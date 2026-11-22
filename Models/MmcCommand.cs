namespace MidiController.Models;

/// <summary>
/// Comandos MMC (MIDI Machine Control) que puede enviar el controlador.
/// </summary>
public enum MmcCommand : byte
{
    Stop = 0x01,
    Play = 0x02,
    DeferredPlay = 0x03,
    FastForward = 0x04,
    Rewind = 0x05,
    Record = 0x06,          // Record Strobe en la spec MMC
    RecordExit = 0x07,
    RecordPause = 0x08,
    Pause = 0x09,
    Eject = 0x0A,
    Chase = 0x0B,
    CommandErrorReset = 0x0C,
    MmcReset = 0x0D,
}
