using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Kostyor.App.ViewModels;

namespace Kostyor.App;

/// <summary>
/// Круглое прозрачное topmost-окно (ТЗ §UI). Перетаскивание за любую область — <c>DragMove</c>;
/// клик по времени — компакт-режим; click-through — через <c>WS_EX_TRANSPARENT</c>.
/// </summary>
public partial class MainWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExLayered = 0x00080000;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint p);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X; public int Y; }

    public MainWindow()
    {
        InitializeComponent();
    }

    private MainViewModel? Vm => DataContext as MainViewModel;

    // ==== Пользовательский ресайз круга (зум за обод), сохраняется между запусками ====
    private const double MinScale = 0.40;
    private const double MaxScale = 1.25;
    private const double EdgeBand = 30;      // ширина «ручки» у обода в координатах элемента (0..420)
    private bool _resizing;
    private double _resizeStartScale;
    private int _resizeStartY;

    /// <summary>Пользователь отпустил обод — сохранить новый масштаб (App пишет в конфиг).</summary>
    public event Action<double>? UiScaleCommitted;

    private double CurrentScale => UiScaleTransform.ScaleX;

    /// <summary>Задать масштаб UI (клампится). Применяется и к компакту — тот же LayoutTransform.</summary>
    public void SetUiScale(double scale)
    {
        var s = Math.Clamp(scale, MinScale, MaxScale);
        UiScaleTransform.ScaleX = s;
        UiScaleTransform.ScaleY = s;
    }

    // Курсор у обода (внешнее кольцо ~30px) — это зона растягивания.
    private static bool IsNearEdge(FrameworkElement fe, Point p)
    {
        var cx = fe.ActualWidth / 2;
        var cy = fe.ActualHeight / 2;
        var r = Math.Min(cx, cy);
        var dx = p.X - cx;
        var dy = p.Y - cy;
        return Math.Sqrt(dx * dx + dy * dy) >= r - EdgeBand;
    }

    private void BeginResize(FrameworkElement fe)
    {
        _resizing = true;
        _resizeStartScale = CurrentScale;
        GetCursorPos(out var p);
        _resizeStartY = p.Y;
        fe.CaptureMouse();
        fe.Cursor = Cursors.SizeNWSE;
    }

    // Перетаскивание за круг: у обода — ресайз, иначе — перенос окна.
    // (Кнопки/время помечают событие Handled и сюда не доходят.)
    private void Circle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
        if (sender is FrameworkElement fe && IsNearEdge(fe, e.GetPosition(fe)))
        {
            BeginResize(fe);
            e.Handled = true;
            return;
        }
        try { DragMove(); } catch { /* DragMove кидает, если кнопка уже отпущена */ }
    }

    private void Border_MouseMove(object sender, MouseEventArgs e)
    {
        if (_resizing)
        {
            GetCursorPos(out var cur);
            // Тянем вниз — больше, вверх — меньше. Ход ~200px = весь диапазон.
            SetUiScale(_resizeStartScale + (cur.Y - _resizeStartY) * 0.004);
            e.Handled = true;
            return;
        }
        if (sender is FrameworkElement fe)
            fe.Cursor = IsNearEdge(fe, e.GetPosition(fe)) ? Cursors.SizeNWSE : null;
    }

    private void Border_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_resizing) return;
        _resizing = false;
        if (sender is FrameworkElement fe) fe.ReleaseMouseCapture();
        UiScaleCommitted?.Invoke(CurrentScale);
        e.Handled = true;
    }

    private void TimeText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        Vm?.ToggleCompactCommand.Execute(null);
    }

    // Компакт: DragMove тащит окно; если курсор почти не сдвинулся — это был клик → развернуть.
    // Сравниваем АБСОЛЮТНЫЕ экранные координаты курсора до/после (надёжно, без ложных разворотов).
    private void Compact_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        GetCursorPos(out var down);
        try { DragMove(); } catch { /* кнопка уже отпущена */ }
        GetCursorPos(out var up);

        if (Math.Abs(up.X - down.X) <= 4 && Math.Abs(up.Y - down.Y) <= 4)
            Vm?.ToggleCompactCommand.Execute(null);
    }

    private void EditBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TextBox tb && tb.IsVisible)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                tb.Focus();
                tb.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    private void RoleEdit_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: RoleRowViewModel row })
            row.CommitCommand.Execute(null);
    }

    private static readonly Brush OpaqueBackground = CreateOpaqueBackground();

    /// <summary>
    /// Непрозрачный fallback-фон (ТЗ §3): заливает всё окно тёмным градиентом (как страница
    /// дизайна) вместо прозрачности. Нужен, если screen-share/захват плохо берёт прозрачное окно,
    /// и для чистых кадров автотестера (иначе фон рабочего стола просвечивает — false positives Vision).
    /// </summary>
    public void SetOpaqueBackground(bool opaque)
    {
        // Красим САМО окно (заливает весь HWND-клиент, включая поля вокруг круга,
        // раздутые эффектом тени), а не только Root — иначе сверху остаётся прозрачная полоса.
        Background = opaque ? OpaqueBackground : Brushes.Transparent;
        Root.Background = opaque ? OpaqueBackground : null;
    }

    private static Brush CreateOpaqueBackground()
    {
        // radial-gradient(circle at 28% 18%, #2b303b, #141117 70%) — фон страницы из дизайна.
        var b = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.28, 0.18),
            Center = new Point(0.28, 0.18),
            RadiusX = 1.1,
            RadiusY = 1.1,
        };
        b.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#2b303b"), 0));
        b.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#141117"), 0.7));
        b.Freeze();
        return b;
    }

    /// <summary>Реальный контрол для подсветки в обучающем туре (ТЗ часть B).</summary>
    public FrameworkElement? CoachTarget(string key) => key switch
    {
        "circle" => CircleBorder,
        "compact" => CompactBorder,
        "time" => TimeText,
        "money" => AmountOdometer,
        "play" => PlayButton,
        "add" => AddButton,
        "stop" => StopButton,
        _ => null,
    };

    /// <summary>Контекстное меню по правому клику на круге (и в компакте) — гарантированный доступ к «Выходу».</summary>
    public void SetCircleMenu(ContextMenu menu)
    {
        CircleBorder.ContextMenu = menu;
        CompactBorder.ContextMenu = menu;
    }

    /// <summary>Сквозные клики: окно не перехватывает мышь (ТЗ §5, переключатель в трее).</summary>
    public void SetClickThrough(bool enabled)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        var style = GetWindowLong(hwnd, GwlExStyle);
        style = enabled
            ? style | WsExTransparent | WsExLayered
            : style & ~WsExTransparent;
        SetWindowLong(hwnd, GwlExStyle, style);
    }
}
