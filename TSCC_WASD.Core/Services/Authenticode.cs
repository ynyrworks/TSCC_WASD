using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography.X509Certificates;

namespace TSCC_WASD.Core.Services;

public enum SignatureStatus { Unsigned, Valid, Invalid }

/// <summary>An executable's Authenticode state and, when it is validly signed, the signer's subject.</summary>
public sealed record SignatureInfo(SignatureStatus Status, string? Publisher = null);

/// <summary>Checks Authenticode signatures with WinVerifyTrust, offline (no revocation lookup, no UI).</summary>
[SupportedOSPlatform("windows")]
public static class Authenticode
{
    private const uint TrustNoSignature = 0x800B0100;

    public static SignatureInfo Check(string path)
    {
        uint result = Verify(path);
        if (result == TrustNoSignature) return new SignatureInfo(SignatureStatus.Unsigned);
        if (result != 0) return new SignatureInfo(SignatureStatus.Invalid);
        string? publisher = null;
        try
        {
#pragma warning disable SYSLIB0057 // Reads the signer of a file; X509CertificateLoader has no replacement for this.
            using var certificate = X509Certificate.CreateFromSignedFile(path);
#pragma warning restore SYSLIB0057
            publisher = certificate.Subject;
        }
        catch (System.Security.Cryptography.CryptographicException) { }
        return new SignatureInfo(SignatureStatus.Valid, publisher);
    }

    private static uint Verify(string path)
    {
        var pathPtr = Marshal.StringToHGlobalUni(path);
        var fileInfo = new FileInfo { Size = (uint)Marshal.SizeOf<FileInfo>(), FilePath = pathPtr };
        var filePtr = Marshal.AllocHGlobal(Marshal.SizeOf<FileInfo>());
        try
        {
            Marshal.StructureToPtr(fileInfo, filePtr, false);
            var data = new TrustData
            {
                Size = (uint)Marshal.SizeOf<TrustData>(),
                UiChoice = 2,          // WTD_UI_NONE
                RevocationChecks = 0,  // WTD_REVOKE_NONE
                UnionChoice = 1,       // WTD_CHOICE_FILE
                File = filePtr,
                StateAction = 1,       // WTD_STATEACTION_VERIFY
                ProviderFlags = 0x10   // WTD_REVOCATION_CHECK_NONE
            };
            var action = GenericVerifyV2;
            uint result = (uint)WinVerifyTrust(IntPtr.Zero, ref action, ref data);
            data.StateAction = 2;      // WTD_STATEACTION_CLOSE
            WinVerifyTrust(IntPtr.Zero, ref action, ref data);
            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(filePtr);
            Marshal.FreeHGlobal(pathPtr);
        }
    }

    private static readonly Guid GenericVerifyV2 = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    [DllImport("wintrust.dll", ExactSpelling = true)]
    private static extern int WinVerifyTrust(IntPtr window, ref Guid action, ref TrustData data);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileInfo
    {
        public uint Size;
        public IntPtr FilePath;
        public IntPtr FileHandle;
        public IntPtr KnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TrustData
    {
        public uint Size;
        public IntPtr PolicyCallbackData;
        public IntPtr SipClientData;
        public uint UiChoice;
        public uint RevocationChecks;
        public uint UnionChoice;
        public IntPtr File;
        public uint StateAction;
        public IntPtr StateData;
        public IntPtr UrlReference;
        public uint ProviderFlags;
        public uint UiContext;
        public IntPtr SignatureSettings;
    }
}
