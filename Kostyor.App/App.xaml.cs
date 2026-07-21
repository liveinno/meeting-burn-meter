using System.Windows;
using System.Windows.Threading;
using Kostyor.Core.Config;
using Kostyor.Core.Money;
using Kostyor.App.Coaching;
using Kostyor.App.Services;
using Kostyor.App.ViewModels;
using Kostyor.App.Views;

namespace Kostyor.App;

/// <summary>
/// Composition root (AGENTS §5): логгер + version-marker, конфиг, курсы, сессия, история,
/// трей, хоткеи, автосейв. Всё оборачивается в try/лог — старт не должен падать молча.
/// </summary>
public partial class App : Application
{
    private Logger _log = null!;
    private AppConfig _config = null!;
    private RatesService _rates = null!;
    private SessionStore _session = null!;
    private HistoryRepository _history = null!;
    private MainViewModel _vm = null!;
    private MainWindow _window = null!;

    private HotkeyService? _hotkeys;
    private H.NotifyIcon.TaskbarIcon? _tray;
    private DispatcherTimer? _autosaveTimer;
    private System.Threading.Mutex? _singleInstance;

    private SettingsWindow? _settingsWindow;
    private HistoryWindow? _historyWindow;
    private CoachMarkOverlay? _coach;
    private Window? _whiteBackdrop;
    private bool _opaque;
    private bool _pendingCoach;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Единственный экземпляр (трей-приложение).
        _singleInstance = new System.Threading.Mutex(initiallyOwned: true, "Kostyor.SingleInstance", out var isNew);
        if (!isNew)
        {
            Shutdown();
            return;
        }

        // Трей-приложение живёт, пока не выбрали «Выход» — закрытие любого окна не гасит его
        // и не сворачивает главное окно (иначе «Новая встреча» уводила круг в трей).
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        DispatcherUnhandledException += (_, args) =>
        {
            _log?.Error("Необработанное исключение UI", args.Exception);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            _log?.Error("Необработанное исключение домена", args.ExceptionObject as Exception);

        try
        {
            AppPaths.EnsureAppData();
        }
        catch { /* фолбэк логгера сам разберётся */ }

        _log = new Logger();
        _log.Info("Инициализация приложения…");

        _config = ConfigStore.Load(AppPaths.ConfigFile);
        _rates = RatesFactory.Create(_config);
        _log.Info($"Курс валют (локальный): $ {_config.ManualRates.UsdPerUnit}, € {_config.ManualRates.EurPerUnit}, ¥ {_config.ManualRates.CnyPerUnit}");
        _session = new SessionStore();
        _history = new HistoryRepository(AppPaths.HistoryDb, _log);

        _vm = new MainViewModel(_config, _rates, _session, _history, _log);
        _vm.MeetingStopped += ShowSummary;

        _window = new MainWindow { DataContext = _vm };
        MainWindow = _window;

        // Пользовательский масштаб круга: восстановить и сохранять при растягивании за обод.
        _window.SetUiScale(_config.UiScale);
        _window.UiScaleCommitted += scale =>
        {
            _config.UiScale = scale;
            try { ConfigStore.Save(AppPaths.ConfigFile, _config); }
            catch (Exception ex) { _log.Error("Не удалось сохранить масштаб окна", ex); }
        };

        // Форсируем HWND до Show — нужно для регистрации хоткеев.
        var helper = new System.Windows.Interop.WindowInteropHelper(_window);
        helper.EnsureHandle();

        SetupTray();
        SetupWindowMenu();
        SetupHotkeys();

        // --opaque: непрозрачный fallback-фон (screen-share / чистые кадры автотестера).
        _opaque = e.Args.Any(a => a.Equals("--opaque", StringComparison.OrdinalIgnoreCase));

        // --white: белая подложка под всеми (прозрачными) окнами — для чистых скриншотов README.
        var white = e.Args.Any(a => a.Equals("--white", StringComparison.OrdinalIgnoreCase));
        if (white) ShowWhiteBackdrop();

        // Обучение: первый запуск (!OnboardingDone) или deep-link --coach. Показываем после старта.
        // --no-coach подавляет тур (автотестер: чтобы слой не перекрывал UI при проверках).
        var noCoach = e.Args.Any(a => a.Equals("--no-coach", StringComparison.OrdinalIgnoreCase));
        _pendingCoach = !noCoach && (e.Args.Any(a => a.Equals("--coach", StringComparison.OrdinalIgnoreCase)) || !_config.OnboardingDone);

        // --opaque/--white = режим захвата: окно всегда показываем (иначе нечего снимать/тестить).
        if (!_config.StartMinimized || _opaque || white)
            _window.Show();
        else
            _log.Info("Старт свёрнутым в трей");

        _window.SetOpaqueBackground(_opaque);
        ApplyClickThrough(_config.ClickThrough);
        StartTimers();

        // Предложение восстановить сессию — не в потоке старта (модалка не должна блокировать
        // инициализацию и перекрывать окно до полного запуска). Показываем после отрисовки.
        Dispatcher.BeginInvoke(new Action(TryOfferRestore), DispatcherPriority.ApplicationIdle);

        _log.Info("Приложение запущено");
    }

    /// <summary>Белая подложка на весь экран под (прозрачными) окнами — только для чистых
    /// скриншотов README (флаг --white). НЕ topmost: стоит ПОД окнами приложения, ничего не блокирует.</summary>
    private void ShowWhiteBackdrop()
    {
        _whiteBackdrop = new Window
        {
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            Topmost = false,
            Background = System.Windows.Media.Brushes.White,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = SystemParameters.VirtualScreenLeft,
            Top = SystemParameters.VirtualScreenTop,
            Width = SystemParameters.VirtualScreenWidth,
            Height = SystemParameters.VirtualScreenHeight,
        };
        _whiteBackdrop.Show();
    }

    private void SetupTray()
    {
        try
        {
            _tray = new H.NotifyIcon.TaskbarIcon
            {
                ToolTipText = "Костёр — счётчик стоимости встречи",
                IconSource = TrayIconFactory.CreateImage(),
                Visibility = Visibility.Visible,
            };
            _tray.ContextMenu = BuildMenu(forTray: true);
            _tray.TrayMouseDoubleClick += (_, _) => ToggleWindowVisibility();
            _tray.ForceCreate();
        }
        catch (Exception ex)
        {
            _log.Error("Не удалось создать иконку в трее", ex);
        }
    }

    /// <summary>Правый клик по кругу — то же меню, что и в трее (гарантированный доступ, в т.ч. «Выход»).</summary>
    private void SetupWindowMenu() => _window.SetCircleMenu(BuildMenu(forTray: false));

    private System.Windows.Controls.ContextMenu BuildMenu(bool forTray)
    {
        var menu = new System.Windows.Controls.ContextMenu();
        void Add(string header, Action action) => menu.Items.Add(MenuItem(header, (_, _) => action()));
        void Sep() => menu.Items.Add(new System.Windows.Controls.Separator());

        Add("▶  Старт / Пауза", () => _vm.ToggleRunCommand.Execute(null));
        Add("■  Стоп", () => _vm.StopCommand.Execute(null));
        Add("Новая встреча", () => _vm.ResetMeeting());
        Sep();
        Add("Участники", () => _vm.TogglePanelCommand.Execute(null));
        Add(forTray ? "Показать / скрыть" : "Свернуть в трей", () => ToggleWindowVisibility());
        Add("Компакт-режим", () => _vm.ToggleCompactCommand.Execute(null));
        Sep();
        Add("Настройки…", OpenSettings);
        Add("История…", OpenHistory);
        Add("Обучение (тур по кнопкам)", StartOnboarding);
        Sep();
        Add("Режим захвата (вкл/выкл)", ToggleCapture);
        Add("Сквозные клики (click-through)", ToggleClickThrough);
        Sep();
        Add("Выход из Костра", () => Shutdown());
        return menu;
    }

    private static System.Windows.Controls.MenuItem MenuItem(string header, RoutedEventHandler onClick)
    {
        var item = new System.Windows.Controls.MenuItem { Header = header };
        item.Click += onClick;
        return item;
    }

    private void SetupHotkeys()
    {
        try
        {
            _hotkeys = new HotkeyService(_window);
            _hotkeys.Failed += (gesture, reason) =>
            {
                _log.Warn($"Хоткей '{gesture}' не зарегистрирован: {reason}");
                _tray?.ShowNotification("Костёр", $"Хоткей {gesture} занят ({reason}). Переназначьте в настройках.");
            };

            _hotkeys.Register(_config.Hotkeys.ToggleVisibility, ToggleWindowVisibility);
            _hotkeys.Register(_config.Hotkeys.StartPause, () => _vm.ToggleRunCommand.Execute(null));
            _hotkeys.Register(_config.Hotkeys.CaptureMode, ToggleCapture);
        }
        catch (Exception ex)
        {
            _log.Error("Ошибка настройки хоткеев", ex);
        }
    }

    private void StartTimers()
    {
        // Автосохранение сессии раз в ~5 с (ТЗ §5).
        _autosaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _autosaveTimer.Tick += (_, _) => _vm.AutoSave();
        _autosaveTimer.Start();
    }

    /// <summary>Применяет курс валют из настроек (локальный, сеть не используется) и освежает конвертер VM.</summary>
    private void ApplyRatesConfig()
    {
        _rates.Apply(_config.ManualRates.ToSnapshot(DateTimeOffset.Now));
        _vm.OnRatesUpdated();
    }

    private void TryOfferRestore()
    {
        try { OfferRestoreCore(); }
        finally
        {
            // Обучение — после решения о восстановлении (не наслаивать модалку и тур).
            if (_pendingCoach) { _pendingCoach = false; StartOnboarding(); }
        }
    }

    private void OfferRestoreCore()
    {
        var snap = _session.TryLoad();
        if (snap is null || snap.AccumulatedSeconds <= 0) return;

        var elapsed = TimeSpan.FromSeconds(snap.AccumulatedSeconds);
        var msg = $"Найдена незавершённая встреча от {snap.SavedAt:HH:mm} — " +
                  $"{Kostyor.Core.Formatting.RuFormat.Money(snap.AccumulatedRub)} ₽ за {Kostyor.Core.Formatting.RuFormat.Time(elapsed)}.\n" +
                  $"Продолжить эту встречу?";

        var dialog = new RestoreDialog(msg);
        if (_window.IsVisible) dialog.Owner = _window;
        var yes = dialog.ShowDialog() == true;

        if (yes)
        {
            _vm.RestoreSession(snap);
            _log.Info("Сессия восстановлена");
        }
        else
        {
            _session.Clear();
        }
    }

    private void ShowSummary(SummaryViewModel summary)
    {
        // Без Owner: закрытие карточки не должно затрагивать/сворачивать главное окно.
        var win = new SummaryWindow { DataContext = summary };
        summary.ResetRequested += () =>
        {
            _vm.ResetMeeting();
            win.Close();
            ShowMainWindow(); // после «Новая встреча» круг остаётся на экране
        };
        win.Show();
    }

    private void ShowMainWindow()
    {
        if (_config.StartMinimized && !_window.IsVisible) return;
        _window.Show();
        if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    private void OpenSettings()
    {
        if (_settingsWindow is { IsLoaded: true }) { _settingsWindow.Activate(); return; }
        var svm = new SettingsViewModel(_config, _log, ExePath());
        svm.Saved += () =>
        {
            ApplyRatesConfig();
            _vm.ApplyConfigChanges();
            ApplyClickThrough(_config.ClickThrough);
        };
        _settingsWindow = new SettingsWindow { DataContext = svm };
        _settingsWindow.Show();
    }

    private void OpenHistory()
    {
        if (_historyWindow is { IsLoaded: true }) { _historyWindow.Activate(); return; }
        var hvm = new HistoryViewModel(_history, _log);
        _historyWindow = new HistoryWindow { DataContext = hvm };
        _historyWindow.Show();
    }

    private void StartOnboarding()
    {
        ShowMainWindow();
        if (_coach is not null) { try { _coach.Close(); } catch { /* ignore */ } _coach = null; }

        _coach = new CoachMarkOverlay();
        _coach.Completed += finishedAll =>
        {
            _coach = null;
            _config.OnboardingDone = true;
            try { ConfigStore.Save(AppPaths.ConfigFile, _config); } catch (Exception ex) { _log.Error("Не удалось сохранить OnboardingDone", ex); }
            _log.Info($"Обучение закрыто (пройдено до конца: {finishedAll})");
        };
        _coach.Start(_window, BuildCoachSteps());
        _log.Info("Обучающий тур запущен");
    }

    private IList<CoachStep> BuildCoachSteps() => new List<CoachStep>
    {
        new()
        {
            Title = "Сначала — как свернуть",
            Body = "Костёр умеет сворачиваться в компактный кружок ~100px, чтобы не мешать поверх Zoom/Teams. Попробуй прямо сейчас: кликни по времени (секундам) в центре круга.",
            Target = () => _window.CoachTarget("time"),
            OnHotspotClick = () => _vm.ToggleCompactCommand.Execute(null),
        },
        new()
        {
            Title = "А теперь разверни",
            Body = "Отлично — это компакт-режим. Кликни по кружку ещё раз, чтобы вернуть полный вид.",
            Target = () => _window.CoachTarget("compact"),
            OnHotspotClick = () => _vm.ToggleCompactCommand.Execute(null),
        },
        new()
        {
            Title = "А это — деньги",
            Body = "Крупная сумма под временем — сколько уже сожгла встреча. Она растёт каждую секунду по ставкам участников (цифры прокручиваются, как одометр). Ниже — та же сумма в $/€/¥ и скорость ₽/мин.",
            Target = () => _window.CoachTarget("money"),
        },
        new()
        {
            Title = "Старт и пауза",
            Body = "Жми эту кнопку, чтобы запустить и время, и счёт денег. Ещё раз — пауза. Деньги считаются от времени, так что пауза честно останавливает счёт.",
            Target = () => _window.CoachTarget("play"),
        },
        new()
        {
            Title = "Кто на встрече",
            Body = "Открой панель участников: степперы «− N +» добавляют людей, клик по имени/ставке — редактирует. Меняй состав хоть на ходу — стоимость пересчитается с этого момента.",
            Target = () => _window.CoachTarget("add"),
        },
        new()
        {
            Title = "Стоп и итог",
            Body = "Стоп завершит встречу и покажет карточку итога: сумма, человеко-часы, «сожгли, как N пицц», оценка 👍/👎. Оттуда — скопировать в чат или сохранить картинкой.",
            Target = () => _window.CoachTarget("stop"),
        },
        new()
        {
            Title = "Правый клик = меню",
            Body = "Правый клик по кругу открывает меню: настройки, история, режимы захвата и выход. То же меню — в иконке трея. Запустить этот тур снова можно оттуда.",
            Target = () => _window.CoachTarget("circle"),
        },
    };

    private void ToggleWindowVisibility()
    {
        if (_window.IsVisible)
        {
            _window.Hide();
        }
        else
        {
            _window.Show();
            _window.Activate();
        }
    }

    private void ToggleCapture()
    {
        var excluded = CaptureControl.Toggle(_window);
        _log.Info($"Захват экрана: окно {(excluded ? "исключено (не видно в share)" : "видно в share")}");
        _tray?.ShowNotification("Костёр",
            excluded ? "Окно скрыто из захвата экрана" : "Окно видно в захвате экрана");
    }

    private void ToggleOpaque()
    {
        _opaque = !_opaque;
        _window.SetOpaqueBackground(_opaque);
        _log.Info($"Непрозрачный фон: {(_opaque ? "вкл" : "выкл")}");
    }

    private void ToggleClickThrough() => ApplyClickThrough(!_config.ClickThrough);

    private void ApplyClickThrough(bool enabled)
    {
        _config.ClickThrough = enabled;
        _window.SetClickThrough(enabled);
        _log.Info($"Click-through: {(enabled ? "вкл" : "выкл")}");
    }

    private static string ExePath()
        => Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _vm?.AutoSave();
            _vm?.StopRenderTimer();
            _autosaveTimer?.Stop();
            _hotkeys?.Dispose();
            _tray?.Dispose();
            _log?.Info("Выход из приложения");
            _log?.Dispose();
            _singleInstance?.Dispose();
        }
        catch { /* закрытие — best effort */ }
        base.OnExit(e);
    }
}
