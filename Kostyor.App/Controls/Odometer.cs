using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Kostyor.App.Controls;

/// <summary>
/// Одометр суммы: цифры «прокручиваются» сверху вниз при смене (1:1 с дизайном
/// <c>Костёр.dc.html</c> — каждая цифра это лента 0–9 с плавным <c>translateY</c>).
/// Наследник <see cref="StackPanel"/> — контейнер сам измеряет/раскладывает ячейки при любой смене.
/// </summary>
public sealed class Odometer : StackPanel
{
    private static readonly Duration RollDuration = new(TimeSpan.FromMilliseconds(700));

    private readonly List<Cell> _cells = new();
    private string _pattern = "";

    public Odometer()
    {
        Orientation = Orientation.Horizontal;
        VerticalAlignment = VerticalAlignment.Bottom;
    }

    // Виден автотестеру (AutomationId + Name = сумма).
    protected override AutomationPeer OnCreateAutomationPeer() => new FrameworkElementAutomationPeer(this);

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(Odometer),
        new PropertyMetadata("0", (d, e) => ((Odometer)d).Rebuild((string)e.NewValue, animate: true)));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly DependencyProperty FontSizeProperty = DependencyProperty.Register(
        nameof(FontSize), typeof(double), typeof(Odometer), new PropertyMetadata(48.0, ForceRebuild));
    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    public static readonly DependencyProperty FontFamilyProperty = DependencyProperty.Register(
        nameof(FontFamily), typeof(FontFamily), typeof(Odometer), new PropertyMetadata(SystemFonts.MessageFontFamily, ForceRebuild));
    public FontFamily FontFamily { get => (FontFamily)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    public static readonly DependencyProperty FontWeightProperty = DependencyProperty.Register(
        nameof(FontWeight), typeof(FontWeight), typeof(Odometer), new PropertyMetadata(FontWeights.Normal, ForceRebuild));
    public FontWeight FontWeight { get => (FontWeight)GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }

    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(
        nameof(Foreground), typeof(Brush), typeof(Odometer), new PropertyMetadata(Brushes.White, ForceRebuild));
    public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    private static void ForceRebuild(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var o = (Odometer)d;
        o._pattern = ""; // сменились метрики шрифта — пересобрать ячейки
        o.Rebuild(o.Text, animate: false);
    }

    private double _digitWidth;
    private double _lineHeight;

    private void Rebuild(string text, bool animate)
    {
        text ??= "";
        MeasureGlyph();

        var pattern = new string(text.Select(c => char.IsDigit(c) ? '#' : c).ToArray());
        if (pattern == _pattern && _cells.Count == text.Length)
        {
            for (var i = 0; i < text.Length; i++)
                if (char.IsDigit(text[i]))
                    _cells[i].SetDigit(text[i] - '0', animate);
            return;
        }

        _pattern = pattern;
        Children.Clear();
        _cells.Clear();
        foreach (var ch in text)
        {
            var cell = new Cell(ch, _digitWidth, _lineHeight, FontFamily, FontSize, FontWeight, Foreground);
            _cells.Add(cell);
            Children.Add(cell.Element);
        }
    }

    private void MeasureGlyph()
    {
        var probe = new TextBlock { Text = "0", FontFamily = FontFamily, FontSize = FontSize, FontWeight = FontWeight };
        probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        _digitWidth = Math.Ceiling(probe.DesiredSize.Width) + 1;
        _lineHeight = Math.Ceiling(probe.DesiredSize.Height);
        if (_lineHeight <= 0) _lineHeight = Math.Ceiling(FontSize * 1.32);
        if (_digitWidth <= 1) _digitWidth = Math.Ceiling(FontSize * 0.62);
    }

    private sealed class Cell
    {
        public readonly FrameworkElement Element;
        private readonly TranslateTransform? _tt;
        private readonly double _lineHeight;
        private readonly bool _isDigit;
        private int _digit = -1;

        public Cell(char ch, double width, double lineHeight, FontFamily family, double fontSize, FontWeight weight, Brush fg)
        {
            _isDigit = char.IsDigit(ch);
            _lineHeight = lineHeight;

            if (!_isDigit)
            {
                Element = new TextBlock
                {
                    Text = ch.ToString(),
                    FontFamily = family,
                    FontSize = fontSize,
                    FontWeight = weight,
                    Foreground = fg,
                    VerticalAlignment = VerticalAlignment.Bottom,
                };
                return;
            }

            var strip = new StackPanel { Orientation = Orientation.Vertical };
            for (var n = 0; n <= 9; n++)
            {
                strip.Children.Add(new TextBlock
                {
                    Text = n.ToString(),
                    FontFamily = family,
                    FontSize = fontSize,
                    FontWeight = weight,
                    Foreground = fg,
                    Width = width,
                    Height = lineHeight,
                    TextAlignment = TextAlignment.Center,
                });
            }
            _tt = new TranslateTransform();
            strip.RenderTransform = _tt;

            // Canvas, а НЕ Border: Border зажимает высоту дочернего элемента до своей и обрезает
            // ленту 0–9 по лейауту — тогда сдвинутая цифра исчезает. Canvas высоту не ограничивает.
            var canvas = new Canvas { Width = width, Height = lineHeight, ClipToBounds = true };
            Canvas.SetLeft(strip, 0);
            Canvas.SetTop(strip, 0);
            canvas.Children.Add(strip);
            Element = canvas;
            SetDigit(ch - '0', animate: false);
        }

        public void SetDigit(int digit, bool animate)
        {
            if (!_isDigit || _tt is null || digit == _digit) return;
            _digit = digit;
            var target = -digit * _lineHeight;

            if (!animate)
            {
                _tt.BeginAnimation(TranslateTransform.YProperty, null);
                _tt.Y = target;
                return;
            }

            _tt.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(target, RollDuration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }
    }
}
