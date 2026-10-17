using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace TS6_SpeakerOverlay.Services
{
    internal static class ApiKeyStore
    {
        private const string FileName = "apikey.txt";
        private const string ProtectedPrefix = "dpapi:";
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("TS6-SpeakerOverlay.ApiKey.v1");

        public static string Load()
        {
            try
            {
                string path = AppDataPaths.GetFilePath(FileName);
                AppDataPaths.MigrateLegacyFile(FileName, path);
                if (!File.Exists(path)) return string.Empty;

                string storedValue = File.ReadAllText(path).Trim();
                if (string.IsNullOrEmpty(storedValue)) return string.Empty;

                if (!storedValue.StartsWith(ProtectedPrefix, StringComparison.Ordinal))
                {
                    Save(storedValue);
                    return storedValue;
                }

                byte[] encrypted = Convert.FromBase64String(storedValue[ProtectedPrefix.Length..]);
                byte[] decrypted = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Failed to load the TS6 API key.", ex);
                return string.Empty;
            }
        }

        public static void Save(string apiKey)
        {
            if (TrySave(apiKey)) RemoveLegacyCopies();
        }

        private static bool TrySave(string apiKey)
        {
            try
            {
                byte[] plaintext = Encoding.UTF8.GetBytes(apiKey);
                byte[] encrypted = ProtectedData.Protect(plaintext, Entropy, DataProtectionScope.CurrentUser);
                string value = ProtectedPrefix + Convert.ToBase64String(encrypted);
                File.WriteAllText(AppDataPaths.GetFilePath(FileName), value);
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Error("Failed to save the TS6 API key.", ex);
                return false;
            }
        }

        private static void RemoveLegacyCopies()
        {
            string currentPath = AppDataPaths.GetFilePath(FileName);
            string[] legacyPaths =
            {
                Path.Combine(AppContext.BaseDirectory, FileName),
                Path.Combine(Environment.CurrentDirectory, FileName)
            };

            foreach (string legacyPath in legacyPaths)
            {
                if (string.Equals(legacyPath, currentPath, StringComparison.OrdinalIgnoreCase) ||
                    !File.Exists(legacyPath))
                {
                    continue;
                }

                try
                {
                    File.Delete(legacyPath);
                }
                catch (Exception ex)
                {
                    AppLogger.Error($"Failed to remove legacy API key file at '{legacyPath}'.", ex);
                }
            }
        }
    }
}
