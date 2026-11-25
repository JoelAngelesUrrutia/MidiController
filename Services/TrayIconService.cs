using System;
using System.Drawing;
using System.IO;
using System.Windows.Resources;
// WinForms también define Application (el ImplicitUsings de UseWindowsForms lo
// agrega como global using), así que hay que decir cuál queremos.
using Application = System.Windows.Application;
using Forms = System.Windows.Forms;

namespace Midicon.Services;

/// <summary>
/// Icono de la bandeja del sistema: menú "Abrir"/"Salir" y doble clic para
/// mostrar la ventana. Publica eventos en lugar de tocar la ventana, así que
/// no conoce a la vista ni al ViewModel.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly Icon _iconImage;
    private readonly Forms.NotifyIcon _notifyIcon;
    private bool _disposed;

    /// <summary>Se pidió mostrar la ventana (doble clic o menú "Abrir").</summary>
    public event Action? ShowRequested;

    /// <summary>Se pidió cerrar la aplicación (menú "Salir").</summary>
    public event Action? ExitRequested;

    public TrayIconService()
    {
        _iconImage = LoadAppIcon();
        _notifyIcon = CreateNotifyIcon();
    }

    private Forms.NotifyIcon CreateNotifyIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Abrir", null, (_, _) => ShowRequested?.Invoke());
        menu.Items.Add("Salir", null, (_, _) => ExitRequested?.Invoke());

        var icon = new Forms.NotifyIcon
        {
            Icon = _iconImage,
            Text = "Midicon",
            ContextMenuStrip = menu,
            Visible = true,
        };

        icon.DoubleClick += (_, _) => ShowRequested?.Invoke();
        return icon;
    }

    /// <summary>
    /// Icono incrustado en el ejecutable (Assets\app.ico). Si el recurso no
    /// estuviera disponible, se usa el genérico de Windows para no romper la app.
    /// </summary>
    private static Icon LoadAppIcon()
    {
        try
        {
            StreamResourceInfo? resource =
                Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"));

            if (resource is not null)
            {
                using Stream stream = resource.Stream;
                return new Icon(stream, Forms.SystemInformation.SmallIconSize);
            }
        }
        catch (Exception)
        {
            // Sin recurso: seguimos con el icono por defecto.
        }

        return SystemIcons.Application;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();

        // SystemIcons.Application es compartido: no se libera.
        if (!ReferenceEquals(_iconImage, SystemIcons.Application))
        {
            _iconImage.Dispose();
        }
    }
}
