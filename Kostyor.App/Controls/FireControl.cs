using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Kostyor.Core.Fire;

namespace Kostyor.App.Controls;

/// <summary>
/// Огонь в кольце (design v2 «Костёр — огонь»), 1:1 с макетом. Слои снизу вверх:
/// тепловое свечение → угли (emberrise) → языки (flamelick) → крупное пламя → затемняющий скрим.
/// Рисуется в координатах 420×420 (как дизайн); в компакте масштабируется родителем (тот же
/// приём, что у дизайнера — scale). Прозрачности слоёв берутся из <see cref="FireModel"/> по фазе;
/// частицы анимируются всегда, фаза лишь плавно проявляет/гасит слои (как transition:opacity в CSS).
/// </summary>
public sealed class FireControl : Canvas
{
    private const double Box = 420;
    private static readonly Duration Fade = new(TimeSpan.FromMilliseconds(1400)); // как transition 1.6s в дизайне

    private readonly FrameworkElement _heatGlow;
    private readonly Canvas _embers = NewLayer();
    private readonly Canvas _tongues = NewLayer();
    private readonly Canvas _blaze = NewLayer();
    private readonly FrameworkElement _scrim;
    private readonly Random _rnd = new(20260718); // детерминированная раскладка частиц

    public FireControl()
    {
        Width = Box;
        Height = Box;
        IsHitTestVisible = false;
        ClipToBounds = false;

        _heatGlow = BuildHeatGlow();
        _scrim = BuildScrim();
        BuildEmbers(_embers);
        BuildTongues(_tongues, count: 11, hMin: 70, hMax: 150, wMax: 68, brush: TongueGradient(), blur: 13);
        BuildTongues(_blaze, count: 9, hMin: 170, hMax: 310, wMax: 92, brush: BlazeGradient(), blur: 17);

        // Порядок = z-order: свечение → угли → языки → пламя → скрим (текст читается поверх).
        foreach (var layer in new[] { _heatGlow, _embers, _tongues, _blaze, _scrim })
        {
            layer.Opacity = 0;
            Children.Add(layer);
        }
    }

    private static Canvas NewLayer() => new() { Width = Box, Height = Box, IsHitTestVisible = false };

    #region DP: Phase / Active / ShowScrim

    public static readonly DependencyProperty PhaseProperty = DependencyProperty.Register(
        nameof(Phase), typeof(int), typeof(FireControl), new PropertyMetadata(0, (d, _) => ((FireControl)d).Apply()));

    public int Phase { get => (int)GetValue(PhaseProperty); set => SetValue(PhaseProperty, value); }

    public static readonly DependencyProperty ActiveProperty = DependencyProperty.Register(
        nameof(Active), typeof(bool), typeof(FireControl), new PropertyMetadata(true, (d, _) => ((FireControl)d).Apply()));

    public bool Active { get => (bool)GetValue(ActiveProperty); set => SetValue(ActiveProperty, value); }

    /// <summary>Скрим (затемнение центра для читаемости текста) — только в развёрнутом виде; в компакте выключен.</summary>
    public static readonly DependencyProperty ShowScrimProperty = DependencyProperty.Register(
        nameof(ShowScrim), typeof(bool), typeof(FireControl), new PropertyMetadata(true, (d, _) => ((FireControl)d).Apply()));

    public bool ShowScrim { get => (bool)GetValue(ShowScrimProperty); set => SetValue(ShowScrimProperty, value); }

    private void Apply()
    {
        var v = Active ? FireModel.Visual(Phase) : default;
        FadeTo(_heatGlow, v.HeatGlowOpacity);
        FadeTo(_embers, v.EmberOpacity);
        FadeTo(_tongues, v.TongueOpacity);
        FadeTo(_blaze, v.BlazeOpacity);
        FadeTo(_scrim, ShowScrim ? v.ScrimOpacity : 0);
    }

    private static void FadeTo(UIElement el, double target)
        => el.BeginAnimation(OpacityProperty, new DoubleAnimation(target, Fade) { EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });

    #endregion

    #region Слои

    // Тепловое свечение у дна: left:-12% right:-12% bottom:-8% height:56%, radial ellipse at 50% 100%.
    private static FrameworkElement BuildHeatGlow()
    {
        double left = -0.12 * Box, width = 1.24 * Box, height = 0.56 * Box;
        double top = Box + 0.08 * Box - height;
        var brush = new RadialGradientBrush
        {
            Center = new Point(0.5, 1.0),
            GradientOrigin = new Point(0.5, 1.0),
            RadiusX = 0.62,
            RadiusY = 1.0,
        };
        brush.GradientStops.Add(new GradientStop(Argb(0x8C, 0xFB, 0x92, 0x3C), 0.0));   // rgba(251,146,60,.55)
        brush.GradientStops.Add(new GradientStop(Argb(0x29, 0xF4, 0x3F, 0x5E), 0.55));  // rgba(244,63,94,.16)
        brush.GradientStops.Add(new GradientStop(Argb(0x00, 0xF4, 0x3F, 0x5E), 0.78));
        brush.Freeze();
        var r = new Rectangle { Width = width, Height = height, Fill = brush, IsHitTestVisible = false };
        SetLeft(r, left);
        SetTop(r, top);
        return r;
    }

    // Скрим: radial circle at 50% 44%: rgba(10,12,17,.72) → .2 @42% → transparent @64%.
    private static FrameworkElement BuildScrim()
    {
        var brush = new RadialGradientBrush
        {
            Center = new Point(0.5, 0.44),
            GradientOrigin = new Point(0.5, 0.44),
            RadiusX = 0.64,
            RadiusY = 0.64,
        };
        brush.GradientStops.Add(new GradientStop(Argb(0xB8, 0x0A, 0x0C, 0x11), 0.0));
        brush.GradientStops.Add(new GradientStop(Argb(0x33, 0x0A, 0x0C, 0x11), 0.42));
        brush.GradientStops.Add(new GradientStop(Argb(0x00, 0x0A, 0x0C, 0x11), 0.64));
        brush.Freeze();
        var r = new Rectangle { Width = Box, Height = Box, Fill = brush, IsHitTestVisible = false };
        SetLeft(r, 0);
        SetTop(r, 0);
        return r;
    }

    private void BuildEmbers(Canvas layer)
    {
        for (var i = 0; i < 16; i++)
        {
            var size = Rnd(3, 6.5);
            var leftPct = Rnd(16, 84) / 100.0;
            var bottomPct = Rnd(2, 12) / 100.0;
            var dx = Rnd(-36, 36);
            var dur = TimeSpan.FromSeconds(Rnd(2.4, 4.8));
            var delay = TimeSpan.FromSeconds(-Rnd(0, 4.8));
            var color = i % 3 != 0 ? Hex(0xFB, 0xBF, 0x24) : Hex(0xFB, 0x92, 0x3C);

            var dot = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = new SolidColorBrush(color),
                Effect = new DropShadowEffect { ShadowDepth = 0, BlurRadius = 8, Color = Hex(0xFB, 0x92, 0x3C), Opacity = 0.8 },
                RenderTransformOrigin = new Point(0.5, 0.5),
            };
            var tt = new TranslateTransform();
            var st = new ScaleTransform(1, 1);
            dot.RenderTransform = new TransformGroup { Children = { st, tt } };
            SetLeft(dot, leftPct * Box);
            SetTop(dot, Box - bottomPct * Box - size);
            layer.Children.Add(dot);

            // emberrise: translate(0,0)→(dx,-250), scale 1→.35, opacity 0→1@10%→0.
            tt.BeginAnimation(TranslateTransform.YProperty, Loop(new DoubleAnimation(0, -250, dur), delay));
            tt.BeginAnimation(TranslateTransform.XProperty, Loop(new DoubleAnimation(0, dx, dur), delay));
            st.BeginAnimation(ScaleTransform.ScaleXProperty, Loop(new DoubleAnimation(1, 0.35, dur), delay));
            st.BeginAnimation(ScaleTransform.ScaleYProperty, Loop(new DoubleAnimation(1, 0.35, dur), delay));
            var op = new DoubleAnimationUsingKeyFrames();
            op.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0)));
            op.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(0.10)));
            op.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1.0)));
            dot.BeginAnimation(OpacityProperty, Loop(op, dur, delay));
        }
    }

    private void BuildTongues(Canvas layer, int count, double hMin, double hMax, double wMax, Brush brush, double blur)
    {
        for (var i = 0; i < count; i++)
        {
            var w = Rnd(34, wMax);
            var h = Rnd(hMin, hMax);
            var leftPct = Rnd(8, 84) / 100.0;
            var dur = TimeSpan.FromSeconds(Rnd(0.7, 1.35));
            var delay = TimeSpan.FromSeconds(-Rnd(0, 1.4));

            var flame = new Rectangle
            {
                Width = w,
                Height = h,
                RadiusX = w * 0.5,
                RadiusY = h * 0.42,
                Fill = brush,
                Effect = new BlurEffect { Radius = blur, KernelType = KernelType.Gaussian },
                RenderTransformOrigin = new Point(0.5, 1.0),
                IsHitTestVisible = false,
            };
            var st = new ScaleTransform(1, 1);
            var tt = new TranslateTransform();
            flame.RenderTransform = new TransformGroup { Children = { st, tt } };
            SetLeft(flame, leftPct * Box);
            SetTop(flame, Box + 26 - h); // bottom:-26px
            layer.Children.Add(flame);

            // flamelick: 0/100 (1,1,0); 28% (1.28,.9,-9); 55% (.82,1.08,+4); 78% (1.14,.94,-6).
            st.BeginAnimation(ScaleTransform.ScaleYProperty, Loop(Keys((0,1),(0.28,1.28),(0.55,0.82),(0.78,1.14),(1,1)), dur, delay));
            st.BeginAnimation(ScaleTransform.ScaleXProperty, Loop(Keys((0,1),(0.28,0.90),(0.55,1.08),(0.78,0.94),(1,1)), dur, delay));
            tt.BeginAnimation(TranslateTransform.YProperty, Loop(Keys((0,0),(0.28,-9),(0.55,4),(0.78,-6),(1,0)), dur, delay));
        }
    }

    #endregion

    #region Градиенты и helpers

    private static LinearGradientBrush TongueGradient()
    {
        var b = new LinearGradientBrush { StartPoint = new Point(0.5, 1), EndPoint = new Point(0.5, 0) };
        b.GradientStops.Add(new GradientStop(Hex(0xFD, 0xE0, 0x47), 0.0));
        b.GradientStops.Add(new GradientStop(Hex(0xFB, 0x92, 0x3C), 0.48));
        b.GradientStops.Add(new GradientStop(Argb(0xBF, 0xF4, 0x3F, 0x5E), 0.78));
        b.GradientStops.Add(new GradientStop(Argb(0x00, 0xF4, 0x3F, 0x5E), 1.0));
        b.Freeze();
        return b;
    }

    private static LinearGradientBrush BlazeGradient()
    {
        var b = new LinearGradientBrush { StartPoint = new Point(0.5, 1), EndPoint = new Point(0.5, 0) };
        b.GradientStops.Add(new GradientStop(Hex(0xFE, 0xF0, 0x8A), 0.0));
        b.GradientStops.Add(new GradientStop(Hex(0xFB, 0x92, 0x3C), 0.38));
        b.GradientStops.Add(new GradientStop(Hex(0xF4, 0x3F, 0x5E), 0.68));
        b.GradientStops.Add(new GradientStop(Argb(0x66, 0xF4, 0x3F, 0x5E), 0.86));
        b.GradientStops.Add(new GradientStop(Argb(0x00, 0xF4, 0x3F, 0x5E), 1.0));
        b.Freeze();
        return b;
    }

    private static DoubleAnimationUsingKeyFrames Keys(params (double t, double v)[] frames)
    {
        var a = new DoubleAnimationUsingKeyFrames();
        foreach (var (t, v) in frames)
            a.KeyFrames.Add(new SplineDoubleKeyFrame(v, KeyTime.FromPercent(t), new KeySpline(0.42, 0, 0.58, 1)));
        return a;
    }

    private static T Loop<T>(T anim, TimeSpan begin) where T : AnimationTimeline
    {
        anim.RepeatBehavior = RepeatBehavior.Forever;
        anim.BeginTime = begin;
        return anim;
    }

    private static DoubleAnimationUsingKeyFrames Loop(DoubleAnimationUsingKeyFrames anim, TimeSpan dur, TimeSpan begin)
    {
        anim.Duration = dur;
        anim.RepeatBehavior = RepeatBehavior.Forever;
        anim.BeginTime = begin;
        return anim;
    }

    private double Rnd(double a, double b) => a + _rnd.NextDouble() * (b - a);

    private static Color Hex(byte r, byte g, byte b) => Color.FromRgb(r, g, b);
    private static Color Argb(byte a, byte r, byte g, byte b) => Color.FromArgb(a, r, g, b);

    #endregion
}
