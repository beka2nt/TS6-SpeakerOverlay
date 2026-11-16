using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace TS6_SpeakerOverlay.Helpers;

internal static class TeamSpeakProcessIdentity
{
    internal static bool IsTrustedProcess(int sessionId, int currentSessionId, string path, Func<string, bool>? verifyPublisher = null)
        => sessionId == currentSessionId && string.Equals(Path.GetFileName(path), "TeamSpeak.exe", StringComparison.OrdinalIgnoreCase)
            && (verifyPublisher ?? HasTrustedPublisher)(path);

    internal static bool HasTrustedPublisher(string path)
    {
        var file = new TrustFile { Size = (uint)Marshal.SizeOf<TrustFile>(), Path = path };
        IntPtr filePointer = Marshal.AllocHGlobal(Marshal.SizeOf<TrustFile>());
        var data = new TrustData
        {
            Size = (uint)Marshal.SizeOf<TrustData>(), UiChoice = 2, UnionChoice = 1,
            File = filePointer, StateAction = 1, ProviderFlags = 0x1000 // Cache-only: no verification network request.
        };
        var action = new Guid("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");
        try
        {
            Marshal.StructureToPtr(file, filePointer, false);
            if (WinVerifyTrust(new IntPtr(-1), ref action, ref data) != 0) return false;
            // X509CertificateLoader loads certificate formats, not Authenticode PE files.
#pragma warning disable SYSLIB0057
            using var signed = X509Certificate.CreateFromSignedFile(path);
#pragma warning restore SYSLIB0057
            using var certificate = X509CertificateLoader.LoadCertificate(signed.GetRawCertData());
            return certificate.GetNameInfo(X509NameType.SimpleName, false) == "TeamSpeak Systems GmbH";
        }
        catch (Exception error) when (error is CryptographicException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            data.StateAction = 2; // Close every verification state, including failed checks.
            WinVerifyTrust(new IntPtr(-1), ref action, ref data);
            Marshal.DestroyStructure<TrustFile>(filePointer);
            Marshal.FreeHGlobal(filePointer);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct TrustFile
    {
        internal uint Size;
        [MarshalAs(UnmanagedType.LPWStr)] internal string Path;
        internal IntPtr Handle, KnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TrustData
    {
        internal uint Size;
        internal IntPtr PolicyCallback, SipClient;
        internal uint UiChoice, RevocationChecks, UnionChoice;
        internal IntPtr File;
        internal uint StateAction;
        internal IntPtr StateData, UrlReference;
        internal uint ProviderFlags, UiContext;
        internal IntPtr SignatureSettings;
    }

    [DllImport("wintrust.dll", ExactSpelling = true)]
    private static extern int WinVerifyTrust(IntPtr window, ref Guid action, ref TrustData data);
}
