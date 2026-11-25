using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
// WinForms también define Application (el ImplicitUsings de UseWindowsForms lo
// agrega como global using), así que hay que decir cuál queremos.
using Application = System.Windows.Application;
using Midicon.Services;
using Midicon.ViewModels;

namespace Midicon.Views;

/// <summary>
/// Vista de la ventana principal. Solo se ocupa de lo que es propio de WPF:
/// enlazar el ViewModel, la bandeja del sistema y el ciclo de vida de la
/// ventana (la X oculta; el cierre real lo pide la bandeja o el apagado).
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly TrayIconService _trayIconService;
    private bool _exitRequested;
    private bool _resourcesDisposed;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.Log.CollectionChanged += OnLogChanged;

        // La bandeja es una preocupación de la vista: el ViewModel no sabe que existe.
        _trayIconService = new TrayIconService();
        _trayIconService.ShowRequested += ShowFromTray;
        _trayIconService.ExitRequested += RequestExit;
    }

    /// <summary>
    /// El ListBox no se desplaza solo al añadir elementos: lo hacemos desde la
    /// vista, que es quien conoce el control.
    /// </summary>
    private void OnLogChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add || MidiLogList.Items.Count == 0)
        {
            return;
        }

        MidiLogList.ScrollIntoView(MidiLogList.Items[MidiLogList.Items.Count - 1]);
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
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

        _viewModel.Log.CollectionChanged -= OnLogChanged;
        _trayIconService.ShowRequested -= ShowFromTray;
        _trayIconService.ExitRequested -= RequestExit;

        _viewModel.Dispose();
        _trayIconService.Dispose();
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
}
