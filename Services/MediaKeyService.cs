using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Midicon.Services;

/// <summary>
/// Envía teclas multimedia de Windows (Play/Pausa, anterior, siguiente):
/// el mismo efecto que pulsar esos botones en un teclado físico.
/// </summary>
public static class MediaKeyService
{
    // Códigos de tecla virtual (winuser.h).
    private const ushort VkMediaNextTrack = 0xB0;
    private const ushort VkMediaPrevTrack = 0xB1;
    private const ushort VkMediaPlayPause = 0xB3;

    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;

    public static void PlayPause() => SendKey(VkMediaPlayPause);

    public static void PreviousTrack() => SendKey(VkMediaPrevTrack);

    public static void NextTrack() => SendKey(VkMediaNextTrack);

    private static void SendKey(ushort virtualKey)
    {
        // Los reproductores esperan la pulsación completa: tecla abajo + tecla arriba.
        INPUT[] inputs =
        [
            CreateKeyInput(virtualKey, keyUp: false),
            CreateKeyInput(virtualKey, keyUp: true),
        ];

        uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent != (uint)inputs.Length)
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "No se pudo enviar la tecla multimedia.");
        }
    }

    private static INPUT CreateKeyInput(ushort virtualKey, bool keyUp) => new()
    {
        Type = InputKeyboard,
        Union = new InputUnion
        {
            Keyboard = new KEYBDINPUT
            {
                VirtualKey = virtualKey,
                Flags = keyUp ? KeyEventKeyUp : 0,
            },
        },
    };

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint numberOfInputs, INPUT[] inputs, int sizeOfInput);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint Type;
        public InputUnion Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT Mouse;
        [FieldOffset(0)] public KEYBDINPUT Keyboard;
        [FieldOffset(0)] public HARDWAREINPUT Hardware;
    }

    // Necesario para que el tamaño de INPUT coincida con el de Win32 (40 bytes en x64).
    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint Msg;
        public ushort ParamL;
        public ushort ParamH;
    }
}
