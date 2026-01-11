using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace TS6_SpeakerOverlay
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            var config = Services.ConfigService.Load();
            Helpers.LanguageHelper.SetLanguage(config.Language);
            // 强制软件渲染
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

            // 1. 捕获 UI 线程的崩溃
            this.DispatcherUnhandledException += (s, args) =>
            {
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
                System.Windows.MessageBox.Show(
                    $"程序崩溃 (System):\n{ex?.Message}\n\n{ex?.StackTrace}", 
                    "Fatal Error", 
                    System.Windows.MessageBoxButton.OK, 
                    System.Windows.MessageBoxImage.Error);
            };

            try 
            {
                base.OnStartup(e);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"启动失败: {ex.Message}");
            }
        }
    }
}