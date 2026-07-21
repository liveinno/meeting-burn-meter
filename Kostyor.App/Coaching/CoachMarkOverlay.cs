using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace Kostyor.App.Coaching;

/// <summary>
/// Полупрозрачный слой поверх реального окна: золотой рамкой подсвечивает настоящий контрол
/// и рядом показывает карточку-выноску (ТЗ часть B). НЕ затемняет экран. Приклеен к окну и
/// повторяет его движения; прячется/показывается вместе с ним.
/// </summary>
public sealed class CoachMarkOverlay : Window
{
    private static readonly Color Gold = (Color)ColorConverter.ConvertFromString("#F4C542");
    private const double Inset = 200;   // насколько слой шире окна (место под выноски)
    private const double RingPad = 8;

    private Window? _followed;
    private IList<CoachStep> _steps = Array.Empty<CoachStep>();
    private int _index;
    private bool _finished;

    private readonly Canvas _canvas = new();
    private readonly Border _ring;
    private readonly Border _callout;
    private readonly TextBlock _title = new();
    private readonly TextBlock _body = new();
    private readonly TextBlock _counter = new();
    private readonly Button _back = new();
    private readonly Button _next = new();
    private readonly Button _skip = new();

    /// <summary>true — тур пройден до конца; false — Пропустить/Esc/закрытие окна.</summary>
    public event Action<bool>? Completed;

    public CoachMarkOverlay()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)); // почти прозрачно, но ловит клики
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        FontFamily = new FontFamily("Segoe UI");
        Content = _canvas;

        _ring = new Border
        {
            BorderBrush = new SolidColorBrush(Gold),
            BorderThickness = new Thickness(3),
            CornerRadius = new CornerRadius(12),
            Background = Brushes.Transparent,
            IsHitTestVisible = false,
            Effect = new DropShadowEffect { Color = Gold, BlurRadius = 16, ShadowDepth = 0, Opacity = 0.9 },
            Visibility = Visibility.Collapsed,
        };
        _canvas.Children.Add(_ring);

        _callout = BuildCallout();
        _canvas.Children.Add(_callout);

        PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape && !CurrentRequiresAction()) Finish(false); };
        MouseLeftButtonDown += OnOverlayClick;
    }

    private Border BuildCallout()
    {
        _title.FontWeight = FontWeights.Bold;
        _title.FontSize = 15;
        _title.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f1f5f9"));
        _title.TextWrapping = TextWrapping.Wrap;

        _body.FontSize = 13;
        _body.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#cbd5e1"));
        _body.TextWrapping = TextWrapping.Wrap;
        _body.Margin = new Thickness(0, 8, 0, 0);
        _body.LineHeight = 19;

        _counter.FontSize = 12;
        _counter.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8a93a3"));
        _counter.VerticalAlignment = VerticalAlignment.Center;

        StyleGhost(_back, "Назад");
        StyleGhost(_skip, "Пропустить");
        StylePrimary(_next, "Далее");
        _back.Click += (_, _) => Go(-1);
        _next.Click += (_, _) => Go(1);
        _skip.Click += (_, _) => Finish(false);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(_back);
        buttons.Children.Add(_skip);
        buttons.Children.Add(_next);

        var footer = new Grid { Margin = new Thickness(0, 16, 0, 0) };
        footer.Children.Add(_counter);
        footer.Children.Add(buttons);

        var stack = new StackPanel();
        stack.Children.Add(_title);
        stack.Children.Add(_body);
        stack.Children.Add(footer);

        return new Border
        {
            Width = 300,
            Padding = new Thickness(18),
            CornerRadius = new CornerRadius(16),
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#141820")),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x33, 0xF4, 0xC5, 0x42)),
            BorderThickness = new Thickness(1),
            Effect = new DropShadowEffect { BlurRadius = 40, ShadowDepth = 12, Direction = 270, Opacity = 0.6, Color = Colors.Black },
            Child = stack,
            Visibility = Visibility.Collapsed,
        };
    }

    private static void StylePrimary(Button b, string text)
    {
        b.Content = text; b.MinWidth = 84; b.Height = 34; b.Margin = new Thickness(8, 0, 0, 0);
        b.Cursor = System.Windows.Input.Cursors.Hand; b.FontWeight = FontWeights.Bold; b.Foreground = Brushes.Black;
        b.Template = ButtonTemplate("#F4C542", "#0c0e13");
    }

    private static void StyleGhost(Button b, string text)
    {
        b.Content = text; b.MinWidth = 84; b.Height = 34; b.Margin = new Thickness(8, 0, 0, 0);
        b.Cursor = System.Windows.Input.Cursors.Hand; b.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#cbd5e1"));
        b.Template = ButtonTemplate("#12FFFFFF", "#cbd5e1");
    }

    private static ControlTemplate ButtonTemplate(string bg, string fg)
    {
        var t = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString(bg)));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        cp.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(cp);
        t.VisualTree = border;
        return t;
    }

    public void Start(Window followed, IList<CoachStep> steps)
    {
        if (steps.Count == 0) { Completed?.Invoke(true); return; }
        _followed = followed;
        _steps = steps;
        _index = 0;
        _finished = false;

        Owner = followed;
        Topmost = followed.Topmost; // НЕ ставим свой Topmost=true (иначе пузырь поверх всего)

        followed.LocationChanged += OnFollowedMoved;
        followed.SizeChanged += OnFollowedMoved;
        followed.StateChanged += OnFollowedState;
        followed.IsVisibleChanged += OnFollowedVisible;
        followed.Closed += OnFollowedClosed;

        Reposition();
        Show();
        ShowStep();
    }

    private void OnFollowedMoved(object? s, EventArgs e) { Reposition(); ShowStep(); }
    private void OnFollowedState(object? s, EventArgs e)
    {
        if (_followed!.WindowState == WindowState.Minimized) Hide();
        else { Show(); Reposition(); ShowStep(); }
    }
    private void OnFollowedVisible(object? s, DependencyPropertyChangedEventArgs e)
    {
        if (_followed!.IsVisible) { Show(); Reposition(); ShowStep(); } else Hide();
    }
    private void OnFollowedClosed(object? s, EventArgs e) => Finish(false);

    private void Reposition()
    {
        if (_followed is null || !_followed.IsVisible) return;
        try
        {
            var w = _followed.ActualWidth <= 0 ? _followed.Width : _followed.ActualWidth;
            var h = _followed.ActualHeight <= 0 ? _followed.Height : _followed.ActualHeight;
            var left = _followed.Left - Inset;
            var top = _followed.Top - Inset;
            var width = w + Inset * 2;
            var height = h + Inset * 2;

            // Кламп в рабочую область.
            var wa = SystemParameters.WorkArea;
            if (left < wa.Left) left = wa.Left;
            if (top < wa.Top) top = wa.Top;
            if (left + width > wa.Right) width = wa.Right - left;
            if (top + height > wa.Bottom) height = wa.Bottom - top;

            Left = left; Top = top; Width = width; Height = height;
        }
        catch { /* окно ещё не готово */ }
    }

    private void ShowStep()
    {
        if (_finished || _followed is null) return;
        var step = _steps[_index];
        step.Prepare?.Invoke();

        _title.Text = step.Title;
        _body.Text = step.Body;
        _counter.Text = $"{_index + 1} / {_steps.Count}";
        var forced = step.RequireAction;
        _back.Visibility = (!forced && _index > 0) ? Visibility.Visible : Visibility.Collapsed;
        _skip.Visibility = forced ? Visibility.Collapsed : Visibility.Visible;
        _next.Visibility = forced ? Visibility.Collapsed : Visibility.Visible;
        _next.Content = _index == _steps.Count - 1 ? "Готово" : "Далее";
        _callout.Visibility = Visibility.Visible;

        // Раскладка не готова мгновенно — измеряем цель дважды (сразу и после Loaded).
        PlaceForTarget(step);
        Dispatcher.BeginInvoke(new Action(() => PlaceForTarget(step)), DispatcherPriority.Loaded);
    }

    private bool CurrentRequiresAction()
        => !_finished && _index >= 0 && _index < _steps.Count && _steps[_index].RequireAction;

    // Клик по подсвеченной цели на обязательном шаге: выполнить действие (свернуть/развернуть) и дальше.
    private void OnOverlayClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!CurrentRequiresAction()) return;
        var step = _steps[_index];

        // Принимаем клик по рамке цели (с запасом). Если рамки нет — принимаем любой клик.
        if (_ring.Visibility == Visibility.Visible)
        {
            var rect = new Rect(Canvas.GetLeft(_ring), Canvas.GetTop(_ring), _ring.Width, _ring.Height);
            rect.Inflate(24, 24);
            if (!rect.Contains(e.GetPosition(_canvas))) return;
        }

        step.OnHotspotClick!.Invoke();
        Go(1);
    }

    private void PlaceForTarget(CoachStep step)
    {
        if (_finished) return;
        var target = step.Target?.Invoke();
        Rect? local = null;
        if (target is { IsVisible: true } && target.ActualWidth > 0)
        {
            try
            {
                var tl = target.PointToScreen(new Point(0, 0));
                var br = target.PointToScreen(new Point(target.ActualWidth, target.ActualHeight));
                var a = PointFromScreen(tl);
                var b = PointFromScreen(br);
                local = new Rect(a, b);
            }
            catch { local = null; }
        }

        _callout.Measure(new Size(_callout.Width, double.PositiveInfinity));
        var ch = _callout.DesiredSize.Height;
        var cw = _callout.Width;

        if (local is { } r)
        {
            _ring.Visibility = Visibility.Visible;
            Canvas.SetLeft(_ring, r.Left - RingPad);
            Canvas.SetTop(_ring, r.Top - RingPad);
            _ring.Width = r.Width + RingPad * 2;
            _ring.Height = r.Height + RingPad * 2;

            // Карточка под целью, не влезла — над целью; кламп по горизонтали.
            var cx = r.Left + r.Width / 2 - cw / 2;
            cx = Math.Max(8, Math.Min(cx, ActualWidth - cw - 8));
            var cy = r.Bottom + RingPad + 12;
            if (cy + ch > ActualHeight - 8) cy = r.Top - RingPad - 12 - ch;
            if (cy < 8) cy = 8;
            Canvas.SetLeft(_callout, cx);
            Canvas.SetTop(_callout, cy);
        }
        else
        {
            // Цель не видна — карточка по центру, без рамки.
            _ring.Visibility = Visibility.Collapsed;
            Canvas.SetLeft(_callout, Math.Max(8, (ActualWidth - cw) / 2));
            Canvas.SetTop(_callout, Math.Max(8, (ActualHeight - ch) / 2));
        }
    }

    private void Go(int delta)
    {
        var next = _index + delta;
        if (next < 0) return;
        if (next >= _steps.Count) { Finish(true); return; }
        _index = next;
        ShowStep();
    }

    private void Finish(bool finishedAll)
    {
        if (_finished) return;
        _finished = true;

        if (_followed is not null)
        {
            _followed.LocationChanged -= OnFollowedMoved;
            _followed.SizeChanged -= OnFollowedMoved;
            _followed.StateChanged -= OnFollowedState;
            _followed.IsVisibleChanged -= OnFollowedVisible;
            _followed.Closed -= OnFollowedClosed;
        }

        Completed?.Invoke(finishedAll);
        try { Close(); } catch { /* ignore */ }
    }
}
