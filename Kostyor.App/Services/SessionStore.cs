using System.Text.Json;
using Kostyor.Core.Session;

namespace Kostyor.App.Services;

/// <summary>
/// Автосохранение/восстановление сессии (ТЗ §5, AGENTS §4). Снимок пишется раз в ~5 с
/// (не только при закрытии — при падении обработчик закрытия не вызовется). Битый снимок
/// не роняет старт — просто нет предложения восстановить.
/// </summary>
public sealed class SessionStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private readonly string _path;

    public SessionStore(string? path = null) => _path = path ?? AppPaths.SessionFile;

    public void Save(SessionSnapshot snapshot)
    {
        try
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(snapshot, Json));
            File.Move(tmp, _path, overwrite: true);
        }
        catch
        {
            // Автосейв не должен ронять приложение.
        }
    }

    public SessionSnapshot? TryLoad()
    {
        try
        {
            if (!File.Exists(_path)) return null;
            return JsonSerializer.Deserialize<SessionSnapshot>(File.ReadAllText(_path), Json);
        }
        catch
        {
            return null; // повреждённый снимок — как будто его нет (ТЗ «Качество и логи»)
        }
    }

    public void Clear()
    {
        try { if (File.Exists(_path)) File.Delete(_path); }
        catch { /* ignore */ }
    }
}
