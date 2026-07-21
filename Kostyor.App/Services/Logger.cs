using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace Kostyor.App.Services;

/// <summary>
/// Файловый логгер — первый источник правды (AGENTS §8). Пишет в <c>%APPDATA%\Kostyor\logs</c>,
/// при отказе записи падает в <c>%TEMP%\Kostyor</c>. В первую строку — version-marker (ловим
/// silent-skip старого exe, BUGS.md). Реальный путь лога дублирует в «хлебную крошку»
/// <c>C:\ProgramData\Kostyor\last-session.txt</c> (видно снаружи виртуализации).
/// </summary>
public sealed class Logger : IDisposable
{
    private readonly object _gate = new();
    private readonly StreamWriter? _writer;

    public string? LogFilePath { get; }
    public string Version { get; }

    public Logger()
    {
        Version = ReadVersion();
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        LogFilePath = TryOpen(AppPaths.LogsDir, stamp, out _writer)
            ? Path.Combine(AppPaths.LogsDir, $"kostyor_{stamp}.log")
            : TryOpen(AppPaths.TempFallbackDir, stamp, out _writer)
                ? Path.Combine(AppPaths.TempFallbackDir, $"kostyor_{stamp}.log")
                : null;

        // Version-marker первой строкой.
        WriteRaw($"==== Костёр v{Version} · старт {DateTime.Now:yyyy-MM-dd HH:mm:ss} · PID {Environment.ProcessId} ====");
        Info($"Лог: {LogFilePath ?? "(нет файла)"}");
        DropBreadcrumb();
    }

    private bool TryOpen(string dir, string stamp, out StreamWriter? writer)
    {
        writer = null;
        try
        {
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"kostyor_{stamp}.log");
            writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite), Encoding.UTF8)
            {
                AutoFlush = true
            };
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);

    public void Error(string message, Exception? ex = null)
        => Write("ERROR", ex is null ? message : $"{message} :: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");

    private void Write(string level, string message)
        => WriteRaw($"{DateTime.Now:HH:mm:ss.fff} [{level}] {message}");

    private void WriteRaw(string line)
    {
        lock (_gate)
        {
            try { _writer?.WriteLine(line); } catch { /* лог не должен ронять приложение */ }
        }
        Debug.WriteLine(line);
    }

    private void DropBreadcrumb()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.ProgramData);
            File.AppendAllText(AppPaths.Breadcrumb,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\tv{Version}\tPID {Environment.ProcessId}\t{LogFilePath}{Environment.NewLine}");
        }
        catch
        {
            // ProgramData недоступен (нет прав) — не критично, лог всё равно есть.
        }
    }

    private static string ReadVersion()
    {
        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        return asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
               ?? asm.GetName().Version?.ToString()
               ?? "0.0.0";
    }

    public void Dispose()
    {
        lock (_gate)
        {
            try { _writer?.Flush(); _writer?.Dispose(); } catch { /* ignore */ }
        }
    }
}
