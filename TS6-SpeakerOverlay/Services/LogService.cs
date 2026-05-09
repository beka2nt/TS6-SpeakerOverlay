using System;
using System.IO;

namespace TS6_SpeakerOverlay.Services
{
    public static class LogService
    {
        private static readonly string LogFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "TS6-SpeakerOverlay", 
            "error.log"
        );

        public static void Log(string message)
        {
            try
            {
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
                File.AppendAllText(LogFile, logEntry);
            }
            catch { }
        }

        public static void LogError(Exception ex, string context = "")
        {
            Log($"[ERROR] {context}: {ex.Message}\n{ex.StackTrace}");
        }
    }
}