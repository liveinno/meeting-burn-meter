using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using FlaUI.Core.AutomationElements;
using Kostyor.UITests.Infrastructure;
using Kostyor.UITests.Reporting;

namespace Kostyor.UITests.Scenarios;

/// <summary>
/// Сценарий главного окна: структурная проверка (AutomationId + BoundingRectangle —
/// источник правды) + прогон состояний (idle → running → панель → компакт) + Vision-скрин.
/// Структурные провалы валят билд (exit 1); Vision-вердикты — вспомогательные (BUGS.md).
/// </summary>
public sealed class MainWindowScenario
{
    private readonly AppLauncher _launcher;
    private readonly VisionClient _vision;
    private readonly HtmlReportBuilder _report;

    public MainWindowScenario(AppLauncher launcher, VisionClient vision, HtmlReportBuilder report)
    {
        _launcher = launcher;
        _vision = vision;
        _report = report;
    }

    public async Task RunAsync()
    {
        var window = _launcher.GetMainWindow();
        Thread.Sleep(1200); // дать окну дорисоваться

        // ---- 1. Idle: структура + скрин ----
        CheckIdleStructure(window);
        await VisionStep(window, "Главное окно — состояние покоя (00:00)",
            "круглый счётчик стоимости встречи. Сверху вниз: время (00:00), крупная сумма в ₽, " +
            "строка $/€/¥, скорость ₽/мин, снизу кнопки «+» и play");

        // ---- 2. Running: старт и проверка счёта ----
        var startBtn = Find(window, "BtnStart");
        if (startBtn is not null)
        {
            InvokeOrClick(startBtn);
            Thread.Sleep(2600);

            var timer = Find(window, "Timer");
            var amount = Find(window, "AmountRub");
            var timerText = timer?.Name ?? "";
            var amountText = amount?.Name ?? "";

            if (!Regex.IsMatch(timerText, @"^\d+:\d\d(:\d\d)?$"))
                _report.AddFailure("Формат таймера", $"Таймер показывает '{timerText}', ожидался ММ:СС/Ч:ММ:СС");
            else if (timerText == "00:00")
                _report.AddFailure("Счётчик не идёт", "После старта таймер остался 00:00 (деньги/время не считаются)");
            else
                _report.AddStep($"Счётчик идёт: время {timerText}, сумма {amountText} ₽", null,
                    "Таймер тикает и сумма растёт после старта.");

            CheckRunningStructure(window);
            await VisionStep(window, $"Главное окно — идёт встреча (время {timerText})",
                "круглый счётчик во время встречи: идущее время, растущая сумма ₽, кольцо-прогресс, " +
                "снизу кнопки «+», пауза и стоп");
        }

        // ---- 3. Панель участников ----
        var addBtn = Find(window, "BtnAddParticipant");
        if (addBtn is not null)
        {
            InvokeOrClick(addBtn);
            Thread.Sleep(900);
            var panel = Find(window, "RolesPanel");
            if (panel is null)
                _report.AddFailure("Панель участников", "После клика по «+» панель RolesPanel не появилась");
            else
            {
                CheckNonZero("RolesPanel", panel);
                await VisionStep(window, "Панель участников открыта",
                    "круглый счётчик и под ним панель «Участники» со списком ролей, ставками ₽/час и " +
                    "степперами «− N +»");
            }
        }

        // ---- 4. Компакт-режим ----
        var timerEl = Find(window, "Timer");
        if (timerEl is not null)
        {
            try { window.Focus(); } catch { /* ignore */ }
            Thread.Sleep(300);
            timerEl.Click(); // клик по времени → компакт
            Thread.Sleep(900);
            var mini = Find(window, "MiniModeToggle");
            if (mini is null)
                _report.AddInfo("Компакт-режим", "Компактный круг MiniModeToggle не найден после клика по времени");
            else
            {
                CheckNonZero("MiniModeToggle", mini);
                await VisionStep(window, "Компакт-режим (мини-круг)",
                    "маленький круглый счётчик ~120px: только сумма ₽ и время, цветная рамка");
            }
        }
    }

    // ---------- Структурные проверки ----------

    private void CheckIdleStructure(Window window)
    {
        var sb = new StringBuilder();

        // Текстовый блок — не пересекается между собой, не нулевой.
        var text = new[] { "Timer", "AmountRub", "AmountsRow", "RatePerMin" };
        CheckGroupNonOverlap(window, text, sb);

        // Кнопки — не пересекаются.
        CheckGroupNonOverlap(window, new[] { "BtnAddParticipant", "BtnStart" }, sb);

        // Кольцо — ненулевой размер.
        var ring = Find(window, "ProgressRing");
        if (ring is not null) CheckNonZero("ProgressRing", ring);

        // Скрытые в покое — SKIPPED (не FAILED): BtnStop, RolesPanel, MiniModeToggle.
        foreach (var id in new[] { "BtnStop", "RolesPanel", "MiniModeToggle" })
            sb.AppendLine(Find(window, id) is null ? $"SKIPPED: {id} (скрыт в покое)" : $"виден: {id}");

        _report.AddStep("Структурная проверка (покой)", null,
            sb.Length == 0 ? "Все зоны корректны." : sb.ToString());
    }

    private void CheckRunningStructure(Window window)
    {
        var sb = new StringBuilder();
        CheckGroupNonOverlap(window, new[] { "BtnAddParticipant", "BtnStart", "BtnStop" }, sb);
        _report.AddStep("Структурная проверка (идёт встреча)", null,
            sb.Length == 0 ? "Кнопки управления не пересекаются." : sb.ToString());
    }

    private void CheckGroupNonOverlap(Window window, string[] ids, StringBuilder log)
    {
        var found = new List<(string Id, Rectangle Rect)>();
        foreach (var id in ids)
        {
            var el = Find(window, id);
            if (el is null) { log.AppendLine($"SKIPPED: {id} (не найден)"); continue; }
            var r = el.BoundingRectangle;
            if (r.Width <= 0 || r.Height <= 0)
                _report.AddFailure($"Нулевой размер: {id}", $"{id} имеет размер {r.Width}×{r.Height}");
            else
                found.Add((id, r));
        }

        for (var i = 0; i < found.Count; i++)
            for (var j = i + 1; j < found.Count; j++)
                if (Overlaps(found[i].Rect, found[j].Rect, tolerance: 2))
                    _report.AddFailure($"Наложение зон: {found[i].Id} и {found[j].Id}",
                        $"{found[i].Id} {found[i].Rect} пересекается с {found[j].Id} {found[j].Rect}");
    }

    private void CheckNonZero(string id, AutomationElement el)
    {
        var r = el.BoundingRectangle;
        if (r.Width <= 0 || r.Height <= 0)
            _report.AddFailure($"Нулевой размер: {id}", $"{id} размер {r.Width}×{r.Height}");
    }

    private static bool Overlaps(Rectangle a, Rectangle b, int tolerance)
    {
        var i = Rectangle.Intersect(a, b);
        return i.Width > tolerance && i.Height > tolerance;
    }

    // ---------- Vision ----------

    private async Task VisionStep(Window window, string title, string zonesDescription)
    {
        var base64 = CaptureBase64(window);
        var prompt = BuildPrompt(zonesDescription);
        var verdict = await _vision.AskAboutImageAsync(base64, prompt);

        if (verdict is null)
            _report.AddInfo(title + " (Vision недоступен)",
                _vision.LastFailureReason ?? "нет ответа от Vision");
        else
            _report.AddStep(title, base64, "Vision: " + verdict);
    }

    private static string BuildPrompt(string zones) =>
        $"Ты — QA-инженер, проверяешь скриншот окна: {zones}.\n" +
        "Отвечай ТОЛЬКО на русском.\n" +
        "1) Если какая-то надпись нечитаема как русское/английское слово — начни с 'ОШИБКА КОДИРОВКИ:' и дай ДОСЛОВНУЮ цитату.\n" +
        "2) Если элементы наложены друг на друга или текст обрезан — 'ОШИБКА КОМПОНОВКИ:' и какие.\n" +
        "3) Если всё в порядке — начни со слова 'OK' и перечисли зоны, которые видишь.\n" +
        "Запрещено выдумывать проблемы и давать советы.";

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);

    private static string CaptureBase64(Window window)
    {
        // Увести курсор в угол, чтобы не всплывали тултипы поверх кадра.
        try { FlaUI.Core.Input.Mouse.Position = new System.Drawing.Point(3, 3); } catch { /* ignore */ }
        try { window.Focus(); } catch { /* ignore */ }
        Thread.Sleep(800); // выйти вперёд и дорисоваться (BUGS.md)

        // Снимаем ТОЧНЫЙ прямоугольник окна (GetWindowRect), а не UIA BoundingRectangle:
        // последний у прозрачного окна с тенью «выпирает» выше HWND и тянет в кадр фон (bleed).
        var hwnd = window.Properties.NativeWindowHandle.ValueOrDefault;
        if (hwnd != IntPtr.Zero && GetWindowRect(hwnd, out var r) && r.Right > r.Left && r.Bottom > r.Top)
        {
            var w = r.Right - r.Left;
            var h = r.Bottom - r.Top;
            using var bmp = new Bitmap(w, h);
            using (var g = Graphics.FromImage(bmp))
                g.CopyFromScreen(r.Left, r.Top, 0, 0, new System.Drawing.Size(w, h));
            using var ms1 = new MemoryStream();
            bmp.Save(ms1, ImageFormat.Png);
            return Convert.ToBase64String(ms1.ToArray());
        }

        // Фолбэк — штатный захват FlaUI.
        var image = FlaUI.Core.Capturing.Capture.Element(window);
        using var ms = new MemoryStream();
        image.Bitmap.Save(ms, ImageFormat.Png);
        return Convert.ToBase64String(ms.ToArray());
    }

    // ---------- Утилиты ----------

    private static AutomationElement? Find(Window window, string automationId)
        => window.FindFirstDescendant(cf => cf.ByAutomationId(automationId));

    private static void InvokeOrClick(AutomationElement el)
    {
        try
        {
            var btn = el.AsButton();
            if (btn.Patterns.Invoke.IsSupported) { btn.Invoke(); return; }
        }
        catch { /* fallback */ }
        el.Click();
    }
}
