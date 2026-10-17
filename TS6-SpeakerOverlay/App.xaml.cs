using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Threading.Tasks;
using System.Windows.Threading;
using TS6_SpeakerOverlay.Models;
using TS6_SpeakerOverlay.Services;

namespace TS6_SpeakerOverlay
{
    public partial class App : System.Windows.Application
    {
        private const string InstanceMutexName = @"Local\TS6SpeakerOverlay_Instance";
        private const string ShowRequestName = @"Local\TS6SpeakerOverlay_ShowRequest";
        private Mutex? _instanceMutex;
        private EventWaitHandle? _showRequest;
        private DispatcherTimer? _launchTimer;
        private AppConfig? _config;
        private MainWindow? _overlayWindow;
        private bool _revertingAutoOpen;
        private bool _waitingForTeamSpeak;

        protected override void OnStartup(StartupEventArgs e)
        {
            // 1. 捕获 UI 线程的崩溃
            this.DispatcherUnhandledException += (s, args) =>
            {
                AppLogger.Error("Unhandled exception on the UI thread.", args.Exception);
                // [修复] 明确写出 System.Windows.MessageBox
                System.Windows.MessageBox.Show(
                    $"程序崩溃 (UI):\n{args.Exception.Message}\n\n{args.Exception.StackTrace}", 
                    "Error", 
                    System.Windows.MessageBoxButton.OK, 
                    System.Windows.MessageBoxImage.Error);
                
                args.Handled = true;
                Current.Shutdown();
            };

            // 2. 捕获非 UI 线程的崩溃 (比如 Task.Run 里的)
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                AppLogger.Error("Unhandled exception outside the UI thread.", ex ?? new Exception("Unknown fatal exception."));
                System.Windows.MessageBox.Show(
                    $"程序崩溃 (System):\n{ex?.Message}\n\n{ex?.StackTrace}", 
                    "Fatal Error", 
                    System.Windows.MessageBoxButton.OK, 
                    System.Windows.MessageBoxImage.Error);
            };

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                AppLogger.Error("Unobserved task exception.", args.Exception);
                args.SetObserved();
            };

            try 
            {
                AppLogger.Info($"Application starting. Version {typeof(App).Assembly.GetName().Version}; OS {Environment.OSVersion}.");
                var config = Services.ConfigService.Load();
                Helpers.LanguageHelper.SetLanguage(config.Language);
                RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
                base.OnStartup(e);

                bool waitForTeamSpeak = e.Args.Contains(AutoOpenService.WaitArgument, StringComparer.OrdinalIgnoreCase);
                _showRequest = new EventWaitHandle(false, EventResetMode.AutoReset, ShowRequestName);
                _instanceMutex = new Mutex(true, InstanceMutexName, out bool isFirstInstance);
                if (!isFirstInstance)
                {
                    if (!waitForTeamSpeak || AutoOpenService.IsTeamSpeakRunning()) _showRequest.Set();
                    Shutdown();
                    return;
                }

                _config = config;
                config.PropertyChanged += Config_PropertyChanged;
                if (config.AutoOpenWithTeamSpeak || config.AutoStart)
                {
                    try { AutoOpenService.SetEnabled(true, config.AutoOpenWithTeamSpeak); }
                    catch (Exception ex) { AppLogger.Error("Unable to update the TeamSpeak startup entry.", ex); }
                }

                _overlayWindow = new MainWindow();
                MainWindow = _overlayWindow;
                _waitingForTeamSpeak = waitForTeamSpeak && !AutoOpenService.IsTeamSpeakRunning();
                if (!_waitingForTeamSpeak) _overlayWindow.ShowOverlay();

                _launchTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _launchTimer.Tick += LaunchTimer_Tick;
                _launchTimer.Start();
            }
            catch (Exception ex)
            {
                AppLogger.Error("Application startup failed.", ex);
                System.Windows.MessageBox.Show($"启动失败: {ex.Message}");
                Current.Shutdown(-1);
            }
        }

        private void LaunchTimer_Tick(object? sender, EventArgs e)
        {
            if (_overlayWindow == null) return;
            if (_showRequest?.WaitOne(0) == true)
            {
                _waitingForTeamSpeak = false;
                _overlayWindow.ShowOverlay();
            }
            if (_waitingForTeamSpeak && AutoOpenService.IsTeamSpeakRunning())
            {
                _waitingForTeamSpeak = false;
                _overlayWindow.ShowOverlay();
            }
        }

        private void Config_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if ((e.PropertyName != nameof(AppConfig.AutoOpenWithTeamSpeak) && e.PropertyName != nameof(AppConfig.AutoStart)) ||
                _config == null || _revertingAutoOpen) return;

            try
            {
                AutoOpenService.SetEnabled(_config.AutoOpenWithTeamSpeak || _config.AutoStart, _config.AutoOpenWithTeamSpeak);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Unable to change the TeamSpeak startup entry.", ex);
                _revertingAutoOpen = true;
                try
                {
                    if (e.PropertyName == nameof(AppConfig.AutoStart)) _config.AutoStart = !_config.AutoStart;
                    else _config.AutoOpenWithTeamSpeak = !_config.AutoOpenWithTeamSpeak;
                }
                finally { _revertingAutoOpen = false; }
                System.Windows.MessageBox.Show(
                    TryFindResource("Lang_AutoOpen_Error") as string ?? "Unable to change startup settings. See logs for details.",
                    "TS6 Speaker Overlay", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _launchTimer?.Stop();
            if (_config != null) _config.PropertyChanged -= Config_PropertyChanged;
            _showRequest?.Dispose();
            if (_instanceMutex != null)
            {
                try { _instanceMutex.ReleaseMutex(); }
                catch (ApplicationException) { /* A second instance does not own the mutex. */ }
                _instanceMutex.Dispose();
            }
            AppLogger.Info($"Application stopped with exit code {e.ApplicationExitCode}.");
            base.OnExit(e);
        }
    }
}
