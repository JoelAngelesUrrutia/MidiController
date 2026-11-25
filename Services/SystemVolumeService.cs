using NAudio.CoreAudioApi;

namespace Midicon.Services;

/// <summary>
/// Controla el volumen general (master) del dispositivo de salida predeterminado.
/// </summary>
public sealed class SystemVolumeService : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator;
    private readonly MMDevice _device;

    public SystemVolumeService()
    {
        _enumerator = new MMDeviceEnumerator();
        _device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
    }

    /// <summary>Volumen actual, de 0.0 a 1.0.</summary>
    public float Volume => _device.AudioEndpointVolume.MasterVolumeLevelScalar;

    /// <summary>Fija el volumen a partir de un valor MIDI (0-127).</summary>
    public void SetFromMidiValue(int midiValue)
    {
        int clamped = Math.Clamp(midiValue, 0, 127);
        _device.AudioEndpointVolume.MasterVolumeLevelScalar = clamped / 127f;
    }

    public void Dispose()
    {
        _device.Dispose();
        _enumerator.Dispose();
    }
}
