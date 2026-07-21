using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Kostyor.App.Services;

/// <summary>
/// Управление попаданием окна в захват экрана (AGENTS §12, ТЗ §3). По умолчанию окно
/// <b>видно</b> в screen-share (главная фича). Хоткей <c>Ctrl+Shift+C</c> / трей могут
/// временно исключить окно из захвата (<c>WDA_EXCLUDEFROMCAPTURE</c>) — напр. для приватного
/// показа. Автотестер снимает исключение перед скрином, чтобы кадр не был чёрным.
/// </summary>
public static class CaptureControl
{
    private const uint WdaNone = 0x00000000;
    private const uint WdaExcludeFromCapture = 0x00000011;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

    /// <summary>true — окно исключено из захвата (не видно в screen-share).</summary>
    public static bool IsExcluded { get; private set; }

    public static void SetExcluded(Window window, bool excluded)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        if (SetWindowDisplayAffinity(hwnd, excluded ? WdaExcludeFromCapture : WdaNone))
            IsExcluded = excluded;
    }

    public static bool Toggle(Window window)
    {
        SetExcluded(window, !IsExcluded);
        return IsExcluded;
    }

    /// <summary>Гарантирует, что окно попадает в захват (для автотестера перед скрином).</summary>
    public static void EnsureCapturable(Window window) => SetExcluded(window, false);
}
