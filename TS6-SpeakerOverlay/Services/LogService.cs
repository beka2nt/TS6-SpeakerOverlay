using System;

namespace TS6_SpeakerOverlay.Services
{
    public static class LogService
    {
        public static void Log(string message) => AppLogger.Info(message);

        public static void LogError(Exception ex, string context = "")
        {
            AppLogger.Error(string.IsNullOrWhiteSpace(context) ? "Application error." : context, ex);
        }
    }
}
