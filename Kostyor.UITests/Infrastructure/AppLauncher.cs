using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace Kostyor.UITests.Infrastructure;

/// <summary>
/// Запуск/остановка тестируемого exe через FlaUI (ТЗ_UI-тестер). В Dispose добивает
/// остаточные процессы по имени — FlaUI не всегда закрывает окно (BUGS.md).
/// </summary>
public sealed class AppLauncher : IDisposable
{
    private const string ProcessName = "Kostyor";

    private readonly Application _app;
    private readonly UIA3Automation _automation;

    public UIA3Automation Automation => _automation;

    public AppLauncher(string exePath)
    {
        if (!File.Exists(exePath))
            throw new FileNotFoundException($"Не найден exe тестируемого приложения: {exePath}", exePath);

        // На всякий случай прибиваем прошлые экземпляры (single-instance mutex иначе закроет наш запуск).
        KillStray();
        // Чистое состояние: убираем снимок сессии от прошлых прогонов, иначе всплывёт модалка
        // «восстановить сессию?» и перекроет окно (детерминизм теста).
        CleanState();

        // --opaque: непрозрачный фон, чтобы фон рабочего стола не просвечивал в кадре
        // (иначе Vision читает посторонний текст как «ошибку кодировки» — false positives).
        // Явный ProcessStartInfo — надёжно передаёт аргумент.
        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = "--opaque --no-coach",
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(exePath) ?? Environment.CurrentDirectory,
        };
        _app = Application.Launch(psi);
        _automation = new UIA3Automation();
    }

    /// <summary>
    /// Ищет главное окно через дерево рабочего стола по ProcessId, а не через
    /// <c>Process.MainWindowHandle</c>: у безрамочного окна (WindowStyle=None) MainWindowHandle
    /// часто = 0, и штатный <c>GetMainWindow</c> его не находит.
    /// </summary>
    public Window GetMainWindow()
    {
        var pid = _app.ProcessId;
        var desktop = _automation.GetDesktop();
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            var win = desktop.FindFirstChild(cf => cf.ByProcessId(pid))?.AsWindow();
            if (win is not null) return win;
            Thread.Sleep(250);
        }
        throw new InvalidOperationException("Главное окно не появилось за 15 с");
    }

    public IReadOnlyList<Window> GetAllTopLevelWindows()
        => _app.GetAllTopLevelWindows(_automation);

    public void Dispose()
    {
        try { _app.Close(); } catch { /* ignore */ }
        try { _app.Kill(); } catch { /* ignore */ }
        try { _app.Dispose(); } catch { /* ignore */ }
        try { _automation.Dispose(); } catch { /* ignore */ }
        KillStray();
    }

    private static void KillStray()
    {
        foreach (var p in Process.GetProcessesByName(ProcessName))
        {
            try { p.Kill(); p.WaitForExit(2000); } catch { /* ignore */ }
        }
    }

    private static void CleanState()
    {
        try
        {
            var session = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Kostyor", "session.json");
            if (File.Exists(session)) File.Delete(session);
        }
        catch { /* не критично */ }
    }
}
