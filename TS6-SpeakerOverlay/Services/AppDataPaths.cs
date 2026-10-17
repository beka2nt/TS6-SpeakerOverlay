using System;
using System.Collections.Generic;
using System.IO;

namespace TS6_SpeakerOverlay.Services
{
    internal static class AppDataPaths
    {
        private const string AppDirectoryName = "TS6-SpeakerOverlay";

        public static string DataDirectoryPath
        {
            get
            {
                string directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    AppDirectoryName);

                Directory.CreateDirectory(directory);
                return directory;
            }
        }

        public static string GetFilePath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName)
            {
                throw new ArgumentException("A plain file name is required.", nameof(fileName));
            }

            return Path.Combine(DataDirectoryPath, fileName);
        }

        public static void MigrateLegacyFile(string fileName, string destinationPath)
        {
            if (File.Exists(destinationPath)) return;

            var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Path.Combine(AppContext.BaseDirectory, fileName),
                Path.Combine(Environment.CurrentDirectory, fileName)
            };

            foreach (string sourcePath in candidates)
            {
                if (string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase) ||
                    !File.Exists(sourcePath))
                {
                    continue;
                }

                File.Copy(sourcePath, destinationPath, overwrite: false);
                return;
            }
        }
    }
}
