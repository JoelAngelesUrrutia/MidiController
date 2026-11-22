using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MidiController;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Aseguramos que el teclado llegue a la ventana y no se pierda en el foco inicial.
        Loaded += (_, _) => KeysBorder.Focus();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Alt+tecla llega como Key.System y la tecla real viene en SystemKey.
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        switch (key)
        {
            // Los modificadores por sí solos no escriben nada.
            case Key.LeftShift:
            case Key.RightShift:
            case Key.LeftCtrl:
            case Key.RightCtrl:
            case Key.LeftAlt:
            case Key.RightAlt:
            case Key.LWin:
            case Key.RWin:
            case Key.Apps:
            case Key.None:
            case Key.ImeProcessed:
            case Key.DeadCharProcessed:
                return;

            case Key.Back:
                if (KeysTextBlock.Text.Length > 0)
                {
                    KeysTextBlock.Text = KeysTextBlock.Text[..^1];
                }
                e.Handled = true;
                UpdateHint();
                return;

            case Key.Escape:
                KeysTextBlock.Text = string.Empty;
                e.Handled = true;
                UpdateHint();
                return;

            case Key.Enter:
                Append(Environment.NewLine);
                e.Handled = true;
                return;

            case Key.Tab:
                Append("\t");
                e.Handled = true;
                return;

            case Key.Space:
                Append(" ");
                e.Handled = true;
                return;

            default:
                Append(GetKeyText(key));
                e.Handled = true;
                return;
        }
    }

    private void Append(string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        KeysTextBlock.Text += text;
        UpdateHint();
    }

    private void UpdateHint()
    {
        HintTextBlock.Visibility = KeysTextBlock.Text.Length == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private static string GetKeyText(Key key)
    {
        if (key >= Key.D0 && key <= Key.D9)
        {
            return ((char)('0' + (key - Key.D0))).ToString();
        }

        if (key >= Key.NumPad0 && key <= Key.NumPad9)
        {
            return ((char)('0' + (key - Key.NumPad0))).ToString();
        }

        return key switch
        {
            Key.OemComma => ",",
            Key.OemPeriod => ".",
            Key.OemMinus => "-",
            Key.OemPlus => "=",
            Key.OemQuestion => "/",
            Key.OemSemicolon => ";",
            Key.OemQuotes => "'",
            Key.OemOpenBrackets => "[",
            Key.OemCloseBrackets => "]",
            Key.OemPipe => "\\",
            Key.OemBackslash => "\\",
            Key.OemTilde => "`",
            Key.Left or Key.Right or Key.Up or Key.Down => $"<{key}>",
            _ => key.ToString(),
        };
    }
}