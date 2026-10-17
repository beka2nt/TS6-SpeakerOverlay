using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace TS6_SpeakerOverlay.Services
{
    internal static class AutoOpenService
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "TS6 Speaker Overlay (TeamSpeak)";
        internal const string WaitArgument = "--wait-for-teamspeak";

        internal static bool IsTeamSpeakRunning()
        {
            try
            {
                var processes = Process.GetProcessesByName("TeamSpeak");
                try { return processes.Length > 0; }
                finally
                {
                    foreach (var process in processes) process.Dispose();
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Unable to check whether TeamSpeak is running.", ex);
                return false;
            }
        }

        internal static void SetEnabled(bool enabled)
        {
            using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
                ?? throw new InvalidOperationException("Unable to open the current user's startup settings.");

            if (!enabled)
            {
                runKey.DeleteValue(RunValueName, throwOnMissingValue: false);
                return;
            }

            string? executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath) ||
                !Path.GetFileName(executablePath).StartsWith("TS6-SpeakerOverlay", StringComparison.OrdinalIgnoreCase) ||
                !executablePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Startup can only be enabled from the packaged application.");

            string command = $"\"{executablePath}\" {WaitArgument}";
            if (!string.Equals(runKey.GetValue(RunValueName) as string, command, StringComparison.Ordinal))
                runKey.SetValue(RunValueName, command, RegistryValueKind.String);
        }
    }
}
