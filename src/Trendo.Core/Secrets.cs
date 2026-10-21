using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Trendo.Core;

/// <summary>Abstraktion für Geheimnisse. Werte werden nie geloggt.</summary>
public interface ISecretStore
{
    string BackendName { get; }
    void Set(string key, string value);
    string? Get(string key);
    bool Delete(string key);
}

public static class SecretStoreFactory
{
    public static ISecretStore CreateDefault() =>
        OperatingSystem.IsWindows() ? new WindowsCredentialStore() : new InMemorySecretStore();
}

/// <summary>Nur für Tests/Nicht-Windows: hält Geheimnisse ausschließlich im Speicher, nie auf Platte.</summary>
public sealed class InMemorySecretStore : ISecretStore
{
    private readonly Dictionary<string, string> _d = new();
    public string BackendName => "InMemory (Test-Fallback, nicht persistent)";
    public void Set(string key, string value) => _d[key] = value;
    public string? Get(string key) => _d.TryGetValue(key, out var v) ? v : null;
    public bool Delete(string key) => _d.Remove(key);
}

/// <summary>Windows Credential Manager (Generic Credentials) über advapi32, Zielpräfix "TrendoOS:".</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsCredentialStore : ISecretStore
{
    private const string Prefix = "TrendoOS:";
    public string BackendName => "Windows Credential Manager";

    public void Set(string key, string value)
    {
        var blob = Encoding.Unicode.GetBytes(value);
        var ptr = Marshal.AllocHGlobal(blob.Length);
        try
        {
            Marshal.Copy(blob, 0, ptr, blob.Length);
            var c = new CREDENTIAL
            {
                Type = 1 /*GENERIC*/, TargetName = Prefix + key, CredentialBlobSize = (uint)blob.Length,
                CredentialBlob = ptr, Persist = 2 /*LOCAL_MACHINE*/, UserName = "trendo"
            };
            if (!CredWrite(ref c, 0)) throw new InvalidOperationException($"CredWrite fehlgeschlagen ({Marshal.GetLastWin32Error()}).");
        }
        finally { Marshal.FreeHGlobal(ptr); }
    }

    public string? Get(string key)
    {
        if (!CredRead(Prefix + key, 1, 0, out var p)) return null;
        try
        {
            var c = Marshal.PtrToStructure<CREDENTIAL>(p);
            return c.CredentialBlobSize == 0 ? "" : Marshal.PtrToStringUni(c.CredentialBlob, (int)c.CredentialBlobSize / 2);
        }
        finally { CredFree(p); }
    }

    public bool Delete(string key) => CredDelete(Prefix + key, 1, 0);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public uint Flags; public uint Type; public string TargetName; public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize; public IntPtr CredentialBlob; public uint Persist;
        public uint AttributeCount; public IntPtr Attributes; public string? TargetAlias; public string UserName;
    }
    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(ref CREDENTIAL c, uint flags);
    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr cred);
    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, uint type, uint flags);
    [DllImport("advapi32.dll")] private static extern void CredFree(IntPtr p);
}
