using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace TS6_SpeakerOverlay.Services
{
    internal static class AppLogger
    {
        private const int RetainedLogFiles = 30;
        private const long MaximumFileBytes = 3 * 1024 * 1024;
        private const int MaximumEntryCharacters = 64 * 1024;
        private static readonly object SyncRoot = new();
        private static readonly Regex EntryStart = new(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}", RegexOptions.Compiled);
        private static readonly Regex ApiKeyField = new(@"(apiKey[""']?\s*[:=]\s*[""']?)[^\s""',;}]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static string? _lastCleanedLogFile;

        public static string LogDirectoryPath
        {
            get
            {
                string directory = Path.Combine(AppDataPaths.DataDirectoryPath, "logs");
                Directory.CreateDirectory(directory);
                return directory;
            }
        }

        public static void Info(string message) => Write("INFO", message, null);

        public static void Warning(string message) => Write("WARN", message, null);

        public static void Error(string context, Exception exception) => Write("ERROR", context, exception);

        private static void Write(string level, string message, Exception? exception)
        {
            var entry = new StringBuilder()
                .Append(DateTimeOffset.Now.ToString("O"))
                .Append(" [").Append(level).Append("]")
                .Append(" [PID:").Append(Environment.ProcessId)
                .Append(" TID:").Append(Environment.CurrentManagedThreadId).Append("] ")
                .AppendLine(message);

            if (exception != null)
            {
                entry.AppendLine(exception.ToString());
            }

            string logEntry = ApiKeyField.Replace(entry.ToString(), "$1[redacted]");
            if (logEntry.Length > MaximumEntryCharacters)
                logEntry = logEntry[..MaximumEntryCharacters] + Environment.NewLine + "[entry truncated]" + Environment.NewLine;
            Debug.WriteLine(logEntry);

            try
            {
                lock (SyncRoot)
                {
                    byte[] bytes = new UTF8Encoding(false).GetBytes(logEntry);
                    string logFile = GetWritableLogFile(bytes.Length);
                    using (var stream = new FileStream(logFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                        stream.Write(bytes);
                    CleanupOldLogs(logFile);
                }
            }
            catch (Exception ex)
            {
                // Logging must never crash the overlay.
                Debug.WriteLine($"Failed to write application log: {ex}");
            }
        }

        private static string GetWritableLogFile(int incomingBytes)
        {
            string day = DateTime.Now.ToString("yyyy-MM-dd");
            for (int index = 0; index < 1000; index++)
            {
                string suffix = index == 0 ? "" : $".{index:000}";
                string path = Path.Combine(LogDirectoryPath, $"app-{day}{suffix}.log");
                if (!File.Exists(path) || new FileInfo(path).Length + incomingBytes <= MaximumFileBytes)
                    return path;
            }
            throw new IOException("The daily log rotation limit has been reached.");
        }

        private static void CleanupOldLogs(string activeFile)
        {
            if (string.Equals(_lastCleanedLogFile, activeFile, StringComparison.OrdinalIgnoreCase)) return;

            try
            {
                var files = new DirectoryInfo(LogDirectoryPath)
                    .EnumerateFiles("app-*.log")
                    .OrderByDescending(file => file.LastWriteTimeUtc)
                    .ToArray();
                for (int index = 0; index < files.Length; index++)
                {
                    if (string.Equals(files[index].FullName, activeFile, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (index >= RetainedLogFiles)
                        files[index].Delete();
                }
                _lastCleanedLogFile = activeFile;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to clean old log files: {ex}");
            }
        }

        public static IReadOnlyList<string> ReadRecentEntries(int maximumEntries = 200, bool errorsOnly = false)
        {
            if (maximumEntries <= 0) return Array.Empty<string>();
            try
            {
                lock (SyncRoot)
                {
                    var entries = new List<string>();
                    var files = new DirectoryInfo(LogDirectoryPath)
                        .EnumerateFiles("app-*.log")
                        .OrderByDescending(file => file.LastWriteTimeUtc)
                        .Take(5);
                    foreach (FileInfo file in files)
                    {
                        var fileEntries = new List<string>();
                        var current = new StringBuilder();
                        foreach (string line in File.ReadLines(file.FullName))
                        {
                            if (EntryStart.IsMatch(line) && current.Length > 0)
                            {
                                fileEntries.Add(current.ToString().TrimEnd());
                                current.Clear();
                            }
                            current.AppendLine(line);
                        }
                        if (current.Length > 0) fileEntries.Add(current.ToString().TrimEnd());
                        foreach (string entry in fileEntries.AsEnumerable().Reverse())
                        {
                            if (errorsOnly && !entry.Contains(" [ERROR] ", StringComparison.Ordinal))
                                continue;
                            entries.Add(entry);
                            if (entries.Count >= maximumEntries) return entries;
                        }
                    }
                    return entries;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to read application logs: {ex}");
                return Array.Empty<string>();
            }
        }
    }
}
