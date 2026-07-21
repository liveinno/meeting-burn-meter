using Kostyor.UITests.Infrastructure;
using Kostyor.UITests.Reporting;
using Kostyor.UITests.Scenarios;

// Vision-LLM UI-автотестер «Костра» (ТЗ_UI-тестер).
// Коды возврата: 0 = ОК; 1 = найдены UI-баги (структурные); 2 = Vision API недоступен.

Console.OutputEncoding = System.Text.Encoding.UTF8;

var report = new HtmlReportBuilder();
var reportPath = Path.Combine(Directory.GetCurrentDirectory(), "report.html");

try
{
    var configPath = ResolveConfigPath();
    Console.WriteLine($"Конфиг: {configPath}");
    var config = TestConfig.Load(configPath);

    // --exe <path> переопределяет ExePath (напр. когда Smart App Control блокирует
    // self-contained single-file: тогда гоняем framework-dependent сборку — тот же код/DLL).
    var exeOverride = GetArg(args, "--exe");
    var exePath = exeOverride is not null
        ? Path.GetFullPath(exeOverride)
        : ResolveExePath(config.ExePath, Path.GetDirectoryName(configPath)!);
    Console.WriteLine($"Тестируемый exe: {exePath}");

    var vision = new VisionClient(config.VisionProviders);

    // PRE-FLIGHT: все провайдеры мертвы → отчёт и exit 2 (приложение НЕ запускаем).
    Console.WriteLine("Pre-flight ping Vision-провайдеров…");
    if (await vision.PingAllProvidersAsync())
    {
        report.MarkAllProvidersFailed(
            "Все Vision-провайдеры недоступны (проверь Ollama: ollama serve, ollama pull qwen2.5vl:7b).");
        report.Save(reportPath);
        Console.WriteLine($"Vision недоступен. Отчёт: {reportPath}");
        return 2;
    }

    using var launcher = new AppLauncher(exePath);
    var scenario = new MainWindowScenario(launcher, vision, report);
    await scenario.RunAsync();

    report.Save(reportPath);
    Console.WriteLine($"Отчёт: {reportPath}");
    Console.WriteLine(report.HasCriticalFailure ? "РЕЗУЛЬТАТ: FAIL (найдены UI-баги)" : "РЕЗУЛЬТАТ: PASS");
    return report.HasCriticalFailure ? 1 : 0;
}
catch (Exception ex)
{
    report.AddFailure("Сбой автотестера", ex.ToString());
    report.Save(reportPath);
    Console.WriteLine($"ИСКЛЮЧЕНИЕ: {ex.Message}");
    Console.WriteLine($"Отчёт: {reportPath}");
    return 1;
}

static string? GetArg(string[] args, string name)
{
    for (var i = 0; i < args.Length - 1; i++)
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            return args[i + 1];
    return null;
}

static string ResolveConfigPath()
{
    var cwd = Path.Combine(Directory.GetCurrentDirectory(), "config.test.json");
    if (File.Exists(cwd)) return cwd;
    var beside = Path.Combine(AppContext.BaseDirectory, "config.test.json");
    if (File.Exists(beside)) return beside;
    throw new FileNotFoundException("Не найден config.test.json (запускай тестер из его папки).");
}

static string ResolveExePath(string exePath, string configDir)
{
    if (Path.IsPathRooted(exePath)) return Path.GetFullPath(exePath);
    var fromCwd = Path.GetFullPath(exePath);
    if (File.Exists(fromCwd)) return fromCwd;
    return Path.GetFullPath(Path.Combine(configDir, exePath));
}
