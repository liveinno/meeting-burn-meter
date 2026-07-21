using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

// Генератор иконки «Костёр» — точная реализация финального макета дизайнера (вариант 1b,
// "Иконка Костёр - финал.dc.html"): пламя костра над тлеющими поленьями на тёмной радиальной
// плитке со свечением и углями. Координаты — из SVG (viewBox 0 0 512), масштабируются под размер.
// Выдаёт многоразмерный .ico (16..256) и превью-PNG.

static class IconGen
{
    const float VB = 512f; // дизайнерский viewBox

    static int Main(string[] args)
    {
        string outDir = args.Length > 0 ? args[0] : ".";
        Directory.CreateDirectory(outDir);
        int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
        var frames = new List<(int size, byte[] data)>();

        foreach (var s in sizes)
        {
            using var bmp = Render(s);
            byte[] data = s == 256 ? ToPng(bmp) : ToDib(bmp);
            frames.Add((s, data));
            if (s == 256 || s == 64) { using var fs = File.Create(Path.Combine(outDir, $"preview_{s}.png")); fs.Write(ToPng(bmp)); }
        }

        var icoPath = Path.Combine(outDir, "kostyor.ico");
        WriteIco(icoPath, frames, sizes);
        Console.WriteLine($"OK: {icoPath} ({frames.Count} frames), preview_256.png / preview_64.png");
        return 0;
    }

    static Bitmap Render(int S)
    {
        float k = S / VB;                 // масштаб дизайнерских координат под размер
        float X(float v) => v * k;
        PointF P(float x, float y) => new(x * k, y * k);

        var bmp = new Bitmap(S, S, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.Clear(Color.Transparent);

        // --- Плитка: скруглённый прямоугольник x24 y24 w464 h464 rx116, радиальная заливка tileDark ---
        using (var tile = RoundedRect(X(24), X(24), X(464), X(464), X(116)))
        {
            g.SetClip(tile);
            // tileDark: radial cx50% cy34% r78% → #1d222c → #0a0c11
            using (var ell = new GraphicsPath())
            {
                float cx = X(256), cy = X(0.34f * VB), r = X(0.78f * VB);
                ell.AddEllipse(cx - r, cy - r, r * 2, r * 2);
                using var pg = new PathGradientBrush(ell) { CenterPoint = new PointF(cx, cy) };
                pg.CenterColor = Hex("#1d222c");
                pg.SurroundColors = new[] { Hex("#0a0c11") };
                g.FillRectangle(new SolidBrush(Hex("#0a0c11")), 0, 0, S, S);
                g.FillPath(pg, ell);
            }
            g.ResetClip();

            // Обводка края rgba(255,255,255,.08)
            using var edge = new Pen(Color.FromArgb(20, 255, 255, 255), Math.Max(1f, k));
            g.DrawPath(edge, tile);

            // Дальше рисуем внутри плитки (свечение/угли/пламя не должны вылезать за скругление).
            g.SetClip(tile);

            // --- Свечение под пламенем: ellipse cx256 cy270 r150, radial #fb923c a.55 → a0 ---
            using (var glow = new GraphicsPath())
            {
                float cx = X(256), cy = X(255), r = X(150);
                glow.AddEllipse(cx - r, cy - r, r * 2, r * 2);
                using var pg = new PathGradientBrush(glow) { CenterPoint = new PointF(cx, cy) };
                pg.CenterColor = Color.FromArgb(140, 0xfb, 0x92, 0x3c);
                pg.SurroundColors = new[] { Color.FromArgb(0, 0xfb, 0x92, 0x3c) };
                g.FillPath(pg, glow);
            }

            // --- Угли: ellipse cx256 cy404 rx150 ry30, radial #fdba74 → #f97316 → a0, opacity .9 ---
            using (var ember = new GraphicsPath())
            {
                float cx = X(256), cy = X(404), rx = X(150), ry = X(30);
                ember.AddEllipse(cx - rx, cy - ry, rx * 2, ry * 2);
                using var pg = new PathGradientBrush(ember) { CenterPoint = new PointF(cx, cy) };
                pg.CenterColor = Color.FromArgb(230, 0xfd, 0xba, 0x74);
                pg.SurroundColors = new[] { Color.FromArgb(0, 0xf9, 0x73, 0x16) };
                g.FillPath(pg, ember);
            }

            // --- Два тлеющих полена: roundrect x150 y384 w212 h34 rx17, повёрнуты ±19° вокруг (256,401) ---
            DrawLog(g, k, "#3f2a1c", +19f);
            DrawLog(g, k, "#4a3122", -19f);

            // --- Пламя (внешнее): flameNat, снизу #fde047 → 48% #fb923c → верх #f43f5e ---
            using (var flame = FlameOuter(P))
            {
                using var br = new LinearGradientBrush(P(256, 404), P(256, 116), Color.White, Color.White);
                br.InterpolationColors = new ColorBlend
                {
                    Colors = new[] { Hex("#fde047"), Hex("#fb923c"), Hex("#f43f5e") },
                    Positions = new[] { 0f, 0.48f, 1f }
                };
                g.FillPath(br, flame);
            }

            // --- Пламя (ядро): flameCore, снизу #fffbeb → верх #fde047 ---
            using (var core = FlameCore(P))
            {
                using var br = new LinearGradientBrush(P(258, 388), P(258, 250), Color.White, Color.White);
                br.InterpolationColors = new ColorBlend
                {
                    Colors = new[] { Hex("#fffbeb"), Hex("#fde047") },
                    Positions = new[] { 0f, 1f }
                };
                g.FillPath(br, core);
            }

            g.ResetClip();
        }

        return bmp;
    }

    static void DrawLog(Graphics g, float k, string hex, float deg)
    {
        float X(float v) => v * k;
        using var log = RoundedRect(X(150), X(384), X(212), X(34), X(17));
        using var m = new Matrix();
        m.RotateAt(deg, new PointF(X(256), X(401)));
        log.Transform(m);
        using var br = new SolidBrush(Hex(hex));
        g.FillPath(br, log);
    }

    // Внешнее пламя — путь из SVG: M256 116 C… (6 кубических сегментов) Z
    static GraphicsPath FlameOuter(Func<float, float, PointF> P)
    {
        var p = new GraphicsPath();
        p.AddBezier(P(256, 116), P(247, 176), P(306, 200), P(320, 272));
        p.AddBezier(P(320, 272), P(332, 332), P(300, 388), P(256, 404));
        p.AddBezier(P(256, 404), P(210, 404), P(180, 350), P(190, 302));
        p.AddBezier(P(190, 302), P(196, 270), P(216, 252), P(230, 228));
        p.AddBezier(P(230, 228), P(237, 256), P(258, 252), P(254, 220));
        p.AddBezier(P(254, 220), P(251, 182), P(250, 148), P(256, 116));
        p.CloseFigure();
        return p;
    }

    // Ядро пламени — путь из SVG: M258 250 C… (5 сегментов) Z
    static GraphicsPath FlameCore(Func<float, float, PointF> P)
    {
        var p = new GraphicsPath();
        p.AddBezier(P(258, 250), P(255, 288), P(290, 302), P(292, 336));
        p.AddBezier(P(292, 336), P(294, 366), P(274, 388), P(253, 386));
        p.AddBezier(P(253, 386), P(232, 384), P(220, 360), P(228, 336));
        p.AddBezier(P(228, 336), P(233, 320), P(249, 314), P(254, 296));
        p.AddBezier(P(254, 296), P(257, 284), P(255, 262), P(258, 250));
        p.CloseFigure();
        return p;
    }

    static GraphicsPath RoundedRect(float x, float y, float w, float h, float r)
    {
        var p = new GraphicsPath();
        float d = r * 2;
        p.AddArc(x, y, d, d, 180, 90);
        p.AddArc(x + w - d, y, d, d, 270, 90);
        p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        p.AddArc(x, y + h - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    static Color Hex(string s) => ColorTranslator.FromHtml(s);

    static byte[] ToPng(Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    // 32-bpp DIB для .ico: BITMAPINFOHEADER (высота×2) + BGRA снизу-вверх + пустая AND-маска.
    static byte[] ToDib(Bitmap bmp)
    {
        int S = bmp.Width;
        var rect = new Rectangle(0, 0, S, S);
        var bits = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        int stride = bits.Stride;
        var pixels = new byte[stride * S];
        Marshal.Copy(bits.Scan0, pixels, 0, pixels.Length);
        bmp.UnlockBits(bits);

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(40); bw.Write(S); bw.Write(S * 2);
        bw.Write((short)1); bw.Write((short)32);
        bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0);
        for (int row = S - 1; row >= 0; row--) bw.Write(pixels, row * stride, stride);
        int maskStride = ((S + 31) / 32) * 4;
        var maskRow = new byte[maskStride];
        for (int row = 0; row < S; row++) bw.Write(maskRow, 0, maskStride);
        bw.Flush();
        return ms.ToArray();
    }

    static void WriteIco(string path, List<(int size, byte[] data)> frames, int[] sizes)
    {
        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs);
        bw.Write((short)0); bw.Write((short)1); bw.Write((short)frames.Count);
        int offset = 6 + 16 * frames.Count;
        foreach (var fr in frames)
        {
            byte dim = (byte)(fr.size >= 256 ? 0 : fr.size);
            bw.Write(dim); bw.Write(dim); bw.Write((byte)0); bw.Write((byte)0);
            bw.Write((short)1); bw.Write((short)32);
            bw.Write(fr.data.Length); bw.Write(offset);
            offset += fr.data.Length;
        }
        foreach (var fr in frames) bw.Write(fr.data);
    }
}
