using System;
using System.Linq;
using System.Threading;
using System.Windows;
// WinForms también define Application (el ImplicitUsings de UseWindowsForms lo
// agrega como global using), así que hay que decir cuál queremos.
using Application = System.Windows.Application;

namespace Midicon;

public partial class App : Application
{
    private const string MutexName = "midicon.singleinstance";
    private const string ShowWindowEventName = "midicon.showwindow";

    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _showWindowEvent;
    private Thread? _showWindowThread;

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(true, MutexName, out bool isFirstInstance);

        if (!isFirstInstance)
        {
            // Ya hay una instancia en la bandeja: le pedimos que se muestre y salimos.
            try
            {
                using EventWaitHandle showWindowEvent = EventWaitHandle.OpenExisting(ShowWindowEventName);
                showWindowEvent.Set();
            }
            catch
            {
                // Si no podemos avisarla, simplemente no abrimos otra.
            }

            Shutdown();
            return;
        }

        base.OnStartup(e);

        // Señal para que un segundo lanzamiento (doble clic en el acceso directo)
        // haga visible la ventana existente.
        _showWindowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowWindowEventName);
        _showWindowThread = new Thread(() =>
        {
            while (true)
            {
                try
                {
                    if (!_showWindowEvent.WaitOne())
                    {
                        break;
                    }

                    Dispatcher.BeginInvoke(new Action(ShowMainWindow));
                }
                catch
                {
                    // El handle se cerró o el Dispatcher ya no está disponible:
                    // la app se está cerrando, no hay nada que hacer.
                    break;
                }
            }
        })
        {
            IsBackground = true,
        };
        _showWindowThread.Start();

        bool startHidden = e.Args.Any(arg =>
            string.Equals(arg, "--background", StringComparison.OrdinalIgnoreCase));

        var window = new MainWindow();
        MainWindow = window;

        if (!startHidden)
        {
            window.Show();
        }
        // Si es --background, la ventana queda creada (escuchando) pero sin mostrar.
    }

    private void ShowMainWindow()
    {
        if (MainWindow is not MainWindow window)
        {
            return;
        }

        window.Show();
        window.WindowState = WindowState.Normal;
        window.Activate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _showWindowEvent?.Dispose();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        // Apagado/cierre de sesión de Windows: salir limpiamente (la X normal
        // solo oculta, así que hay que pedir el cierre de verdad).
        if (MainWindow is MainWindow window)
        {
            window.RequestExit();
        }

        base.OnSessionEnding(e);
    }
}
