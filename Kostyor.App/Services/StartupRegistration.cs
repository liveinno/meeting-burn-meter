using Microsoft.Win32;

namespace Kostyor.App.Services;

/// <summary>
/// Автозапуск через ключ реестра <c>HKCU\...\Run</c> (ТЗ §5, трей «автозапуск»).
/// Пользовательский ветка — не требует прав администратора, не трогает системные настройки.
/// </summary>
public static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Kostyor";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            return key?.GetValue(ValueName) is string s && !string.IsNullOrWhiteSpace(s);
        }
        catch
        {
            return false;
        }
    }

    public static bool Set(bool enabled, string exePath, Logger? log = null)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                            ?? Registry.CurrentUser.CreateSubKey(RunKey);
            if (key is null) return false;

            if (enabled)
                key.SetValue(ValueName, $"\"{exePath}\"");
            else if (key.GetValue(ValueName) is not null)
                key.DeleteValue(ValueName, throwOnMissingValue: false);

            return true;
        }
        catch (Exception ex)
        {
            log?.Error("Не удалось изменить автозапуск", ex);
            return false;
        }
    }
}
