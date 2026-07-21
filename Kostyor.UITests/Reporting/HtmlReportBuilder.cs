using System.Net;
using System.Text;

namespace Kostyor.UITests.Reporting;

/// <summary>
/// HTML-отчёт со скриншотами и вердиктами (ТЗ_UI-тестер). Скриншоты встраиваются как
/// data-URI. Наверху — PASS/FAIL и счётчики. Русские вердикты смотреть тут, не в консоли (BUGS.md).
/// </summary>
public sealed class HtmlReportBuilder
{
    private sealed record Step(string Title, string? Base64Png, string Verdict, string Kind);

    private readonly List<Step> _steps = new();
    private int _failures;
    private int _passes;
    private bool _providersFailed;
    private string? _providersReason;

    public bool HasCriticalFailure => _failures > 0 || _providersFailed;

    public void AddStep(string title, string? base64Screenshot, string verdict)
    {
        _passes++;
        _steps.Add(new Step(title, base64Screenshot, verdict, "ok"));
    }

    public void AddFailure(string title, string details)
    {
        _failures++;
        _steps.Add(new Step(title, null, details, "fail"));
    }

    public void AddInfo(string title, string details)
        => _steps.Add(new Step(title, null, details, "info"));

    public void MarkAllProvidersFailed(string reason)
    {
        _providersFailed = true;
        _providersReason = reason;
        _steps.Add(new Step("Vision API недоступен", null, reason, "fail"));
    }

    public void Save(string path)
    {
        var sb = new StringBuilder();
        var status = HasCriticalFailure ? "FAIL" : "PASS";
        var statusColor = HasCriticalFailure ? "#f43f5e" : "#34d399";

        sb.Append("<!doctype html><html lang='ru'><head><meta charset='utf-8'>");
        sb.Append("<title>Костёр — отчёт UI-автотестера</title><style>");
        sb.Append("body{font-family:Segoe UI,Arial,sans-serif;background:#141117;color:#e2e8f0;margin:0;padding:24px}");
        sb.Append("h1{font-size:22px}.summary{font-size:18px;font-weight:800;padding:12px 18px;border-radius:12px;display:inline-block;margin-bottom:18px}");
        sb.Append(".step{background:#1c2029;border:1px solid #2a2f3a;border-radius:14px;padding:16px;margin-bottom:16px}");
        sb.Append(".title{font-weight:700;font-size:15px;margin-bottom:8px}");
        sb.Append(".ok{border-left:4px solid #34d399}.fail{border-left:4px solid #f43f5e}.info{border-left:4px solid #60a5fa}");
        sb.Append(".verdict{white-space:pre-wrap;color:#cbd5e1;font-size:13px;margin-top:8px}");
        sb.Append("img{max-width:520px;border-radius:12px;border:1px solid #2a2f3a;margin-top:10px}");
        sb.Append(".counts{color:#8a93a3;font-size:14px;margin-bottom:20px}");
        sb.Append("</style></head><body>");
        sb.Append("<h1>Костёр — отчёт UI-автотестера</h1>");
        sb.Append($"<div class='summary' style='background:{statusColor};color:#0c0e13'>{status}</div>");
        sb.Append($"<div class='counts'>Шагов: {_steps.Count} · успешных проверок: {_passes} · провалов: {_failures}");
        if (_providersFailed) sb.Append($" · Vision недоступен: {WebUtility.HtmlEncode(_providersReason)}");
        sb.Append("</div>");

        foreach (var s in _steps)
        {
            sb.Append($"<div class='step {s.Kind}'>");
            sb.Append($"<div class='title'>{WebUtility.HtmlEncode(s.Title)}</div>");
            if (!string.IsNullOrEmpty(s.Base64Png))
                sb.Append($"<img src='data:image/png;base64,{s.Base64Png}' alt='screenshot'/>");
            sb.Append($"<div class='verdict'>{WebUtility.HtmlEncode(s.Verdict)}</div>");
            sb.Append("</div>");
        }

        sb.Append("</body></html>");
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
