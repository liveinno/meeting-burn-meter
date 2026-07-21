namespace Kostyor.App.Services;

/// <summary>
/// Пути данных приложения (AGENTS §5, §8). Данные — в <c>%APPDATA%\Kostyor</c>,
/// «хлебная крошка» с реальными путями логов — в неупакованном <c>C:\ProgramData\Kostyor</c>,
/// фолбэк логов — в <c>%TEMP%\Kostyor</c>.
/// </summary>
public static class AppPaths
{
    public const string AppFolderName = "Kostyor";

    public static string AppData { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppFolderName);

    public static string LogsDir { get; } = Path.Combine(AppData, "logs");
    public static string ConfigFile { get; } = Path.Combine(AppData, "config.json");
    public static string SessionFile { get; } = Path.Combine(AppData, "session.json");
    public static string HistoryDb { get; } = Path.Combine(AppData, "history.db");

    public static string ProgramData { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), AppFolderName);

    public static string Breadcrumb { get; } = Path.Combine(ProgramData, "last-session.txt");

    public static string TempFallbackDir { get; } = Path.Combine(Path.GetTempPath(), AppFolderName);

    public static void EnsureAppData()
    {
        Directory.CreateDirectory(AppData);
        Directory.CreateDirectory(LogsDir);
    }
}
