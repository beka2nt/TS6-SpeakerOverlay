using System;
using System.IO;
using System.Text;

namespace TS6_SpeakerOverlay.Services
{
    internal static class Ts6AvatarCache
    {
        public static string ResolveServerAvatarPath(
            string serverUniqueIdentifier,
            string clientUniqueIdentifier,
            string avatarHash)
        {
            if (string.IsNullOrWhiteSpace(serverUniqueIdentifier) ||
                string.IsNullOrWhiteSpace(clientUniqueIdentifier) ||
                string.IsNullOrWhiteSpace(avatarHash))
            {
                return string.Empty;
            }

            try
            {
                string serverDirectory = Convert.ToHexString(
                    Encoding.UTF8.GetBytes(serverUniqueIdentifier)).ToLowerInvariant();
                byte[] decodedClientIdentifier = Convert.FromBase64String(clientUniqueIdentifier);

                var encodedClientIdentifier = new StringBuilder(decodedClientIdentifier.Length * 2);
                foreach (byte value in decodedClientIdentifier)
                {
                    encodedClientIdentifier.Append((char)('a' + (value >> 4)));
                    encodedClientIdentifier.Append((char)('a' + (value & 0x0F)));
                }

                string cacheRoot = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TeamSpeak",
                    "Cache",
                    "Default");
                string avatarPath = Path.Combine(
                    cacheRoot,
                    serverDirectory,
                    "clients",
                    $"avatar_{encodedClientIdentifier}");

                return File.Exists(avatarPath) ? avatarPath : string.Empty;
            }
            catch (FormatException)
            {
                return string.Empty;
            }
        }
    }
}
