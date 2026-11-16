using TS6_SpeakerOverlay.Helpers;

internal static class ProcessIdentityChecks
{
    internal static int Run()
    {
        int checks = 0, lookups = 0;
        void Check(bool value, string name)
        {
            if (!value) throw new InvalidOperationException("FAIL: " + name);
            checks++;
            Console.WriteLine("PASS: " + name);
        }
        bool Verified(string _) { lookups++; return true; }
        Check(!TeamSpeakProcessIdentity.IsTrustedProcess(2, 1, @"C:\TS\TeamSpeak.exe", Verified) && lookups == 0,
            "automatic discovery excludes another Windows session before checking its file");
        Check(!TeamSpeakProcessIdentity.IsTrustedProcess(1, 1, @"C:\TS\impostor.exe", Verified),
            "automatic discovery requires the intended executable name");
        Check(!TeamSpeakProcessIdentity.IsTrustedProcess(1, 1, @"C:\TS\TeamSpeak.exe", _ => false),
            "a matching executable name without a trusted publisher cannot grant automatic access");
        Check(TeamSpeakProcessIdentity.IsTrustedProcess(1, 1, @"C:\Portable\TeamSpeak.exe", Verified),
            "verified TS publisher in the current session allows installed or portable paths");
        Check(!TeamSpeakProcessIdentity.HasTrustedPublisher(typeof(ProcessIdentityChecks).Assembly.Location),
            "native Authenticode check rejects the unsigned regression executable");
        return checks;
    }
}
