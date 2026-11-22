using System;
using System.ComponentModel;
using System.Windows;
using MidiController.Models;
using MidiController.Services;

namespace MidiController;

public partial class MainWindow : Window
{
    private readonly MidiInputService? _midiInputService;

    public MainWindow()
    {
        InitializeComponent();

        try
        {
            _midiInputService = new MidiInputService();

            DeviceTextBlock.Text = $"Escuchando: {_midiInputService.DeviceName}";
            _midiInputService.MessageReceived += OnMidiMessageReceived;
            _midiInputService.ButtonPressed += OnMidiButtonPressed;

            // Liberamos el dispositivo MIDI al cerrar la ventana.
            Closed += (_, _) => _midiInputService.Dispose();
        }
        catch (InvalidOperationException ex)
        {
            DeviceTextBlock.Text = ex.Message;
        }
    }

    /// <summary>
    /// Llega desde el hilo de fondo de DryWetMidi: no tocar la UI directamente.
    /// </summary>
    private void OnMidiMessageReceived(string message)
    {
        Dispatcher.BeginInvoke(new Action(() => AppendLog(message)));
    }

    /// <summary>
    /// También llega en el hilo de fondo; lo pasamos a la UI para futuras
    /// acciones que necesiten interfaz o diálogos.
    /// </summary>
    private void OnMidiButtonPressed(MmcCommand command)
    {
        Dispatcher.BeginInvoke(new Action(() => HandleButton(command)));
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
