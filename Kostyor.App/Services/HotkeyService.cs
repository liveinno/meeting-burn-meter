using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace Kostyor.App.Services;

/// <summary>
/// Глобальные хоткеи через <c>RegisterHotKey</c> + <c>HwndSource</c> (ТЗ §5).
/// Занятый хоткей не роняет приложение — <see cref="Failed"/> сообщает, что не удалось
/// зарегистрировать (UI показывает уведомление в трее и даёт переназначить).
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001, ModControl = 0x0002, ModShift = 0x0004, ModWin = 0x0008;
    private const uint ModNoRepeat = 0x4000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly IntPtr _hwnd;
    private readonly HwndSource _source;
    private readonly Dictionary<int, Action> _actions = new();
    private int _nextId = 1;

    /// <summary>Сработало при неудачной регистрации: (описание хоткея, причина).</summary>
    public event Action<string, string>? Failed;

    public HotkeyService(Window window)
    {
        var helper = new WindowInteropHelper(window);
        _hwnd = helper.EnsureHandle();
        _source = HwndSource.FromHwnd(_hwnd)!;
        _source.AddHook(WndProc);
    }

    /// <summary>Регистрирует хоткей из строки вида «Ctrl+Alt+K». Возвращает false, если занят/невалиден.</summary>
    public bool Register(string gesture, Action action)
    {
        if (!TryParse(gesture, out var mods, out var vk))
        {
            Failed?.Invoke(gesture, "не распознан");
            return false;
        }

        var id = _nextId++;
        if (!RegisterHotKey(_hwnd, id, mods | ModNoRepeat, vk))
        {
            var err = Marshal.GetLastWin32Error();
            Failed?.Invoke(gesture, err == 1409 ? "уже занят другой программой" : $"код ошибки {err}");
            return false;
        }

        _actions[id] = action;
        return true;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && _actions.TryGetValue(wParam.ToInt32(), out var action))
        {
            handled = true;
            action();
        }
        return IntPtr.Zero;
    }

    private static bool TryParse(string gesture, out uint mods, out uint vk)
    {
        mods = 0;
        vk = 0;
        if (string.IsNullOrWhiteSpace(gesture)) return false;

        var parts = gesture.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string? keyPart = null;
        foreach (var p in parts)
        {
            switch (p.ToLowerInvariant())
            {
                case "ctrl":
                case "control": mods |= ModControl; break;
                case "alt": mods |= ModAlt; break;
                case "shift": mods |= ModShift; break;
                case "win":
                case "windows": mods |= ModWin; break;
                default: keyPart = p; break;
            }
        }

        if (keyPart is null) return false;

        // Через WPF Key → virtual key.
        if (string.Equals(keyPart, "Space", StringComparison.OrdinalIgnoreCase))
        {
            vk = (uint)KeyInterop.VirtualKeyFromKey(Key.Space);
            return true;
        }

        if (Enum.TryParse<Key>(keyPart, ignoreCase: true, out var key) && key != Key.None)
        {
            vk = (uint)KeyInterop.VirtualKeyFromKey(key);
            return vk != 0;
        }

        // Одиночная буква/цифра.
        if (keyPart.Length == 1)
        {
            var c = char.ToUpperInvariant(keyPart[0]);
            if (Enum.TryParse<Key>(c.ToString(), out var k2))
            {
                vk = (uint)KeyInterop.VirtualKeyFromKey(k2);
                return vk != 0;
            }
        }

        return false;
    }

    public void Dispose()
    {
        foreach (var id in _actions.Keys)
        {
            try { UnregisterHotKey(_hwnd, id); } catch { /* ignore */ }
        }
        _actions.Clear();
        try { _source.RemoveHook(WndProc); } catch { /* ignore */ }
    }
}
