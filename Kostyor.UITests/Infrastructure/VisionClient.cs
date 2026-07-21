using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Kostyor.UITests.Infrastructure;

/// <summary>
/// Клиент к локальной Vision-LLM (OpenAI-совместимый <c>/v1/chat/completions</c>, Ollama).
/// Pre-flight ping + мультимодальные запросы с перебором провайдеров и детектором «слабого»
/// ответа (ТЗ_UI-тестер). Vision — вспомогательный уровень (склонен к false positives, BUGS.md).
/// </summary>
public sealed class VisionClient
{
    private static readonly string[] RefusalMarkers =
        { "i cannot", "i can't", "i'm unable", "unable to", "sorry", "as an ai", "не могу", "извините" };

    private readonly IReadOnlyList<VisionProvider> _providers;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(120) };

    public bool LastCallAllFailed { get; private set; }
    public string? LastFailureReason { get; private set; }

    public VisionClient(IReadOnlyList<VisionProvider> providers) => _providers = providers;

    /// <summary>PRE-FLIGHT: крошечный text-only «ping» каждому. true, если ВСЕ мертвы.</summary>
    public async Task<bool> PingAllProvidersAsync()
    {
        var allDead = true;
        foreach (var p in _providers)
        {
            try
            {
                var body = new
                {
                    model = p.VisionModel,
                    messages = new object[] { new { role = "user", content = "ping" } },
                    max_tokens = 5
                };
                var resp = await PostAsync(p, body);
                if (resp is not null) allDead = false;
            }
            catch
            {
                // провайдер недоступен
            }
        }
        return allDead;
    }

    /// <summary>Мультимодальный запрос: перебирает провайдеров, пропускает «слабые» ответы.</summary>
    public async Task<string?> AskAboutImageAsync(string base64Png, string prompt)
    {
        LastCallAllFailed = false;
        LastFailureReason = null;

        foreach (var p in _providers)
        {
            try
            {
                var body = new
                {
                    model = p.VisionModel,
                    messages = new object[]
                    {
                        new
                        {
                            role = "user",
                            content = new object[]
                            {
                                new { type = "text", text = prompt },
                                new { type = "image_url", image_url = new { url = $"data:image/png;base64,{base64Png}" } }
                            }
                        }
                    },
                    max_tokens = 1000
                };

                var answer = await PostAsync(p, body);
                if (answer is null) { LastFailureReason = "нет ответа от провайдера"; continue; }

                if (IsWeak(answer)) { LastFailureReason = "слабый/отказной ответ"; continue; }
                return answer.Trim();
            }
            catch (Exception ex)
            {
                LastFailureReason = ex.Message;
            }
        }

        LastCallAllFailed = true;
        return null;
    }

    private static bool IsWeak(string answer)
    {
        var trimmed = answer.Trim();
        if (trimmed.Length == 0) return true;
        // Короткий валидный «OK» — не слабый.
        if (trimmed.StartsWith("OK", StringComparison.OrdinalIgnoreCase)) return false;
        if (trimmed.Length < 20) return true;
        var lower = trimmed.ToLowerInvariant();
        return RefusalMarkers.Any(m => lower.Contains(m));
    }

    private async Task<string?> PostAsync(VisionProvider p, object body)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, p.BaseUrl);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", p.ApiKey);
        var json = JsonSerializer.Serialize(body);
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode) return null;

        var text = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(text);
        if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            return null;
        var msg = choices[0].GetProperty("message").GetProperty("content").GetString();
        return msg;
    }
}
