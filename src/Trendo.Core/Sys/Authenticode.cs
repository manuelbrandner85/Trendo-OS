using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Runtime.Versioning;
using System.Security.Cryptography.X509Certificates;

namespace Trendo.Core.Sys;

public enum SigState { Gueltig, Unsigniert, Ungueltig, Fehlt, NichtPruefbar }

/// <summary>Ergebnis der Signaturprüfung einer Datei. <see cref="Publisher"/>: Name aus dem Zertifikat (CN), sonst null.</summary>
public sealed record SignatureInfo(SigState State, string? Publisher, string Detail, bool ViaCatalog = false);

/// <summary>T-0755 SPEC-12: Authenticode-Prüfung nur lesend über die Windows-Schnittstelle WinVerifyTrust – eingebettete Signatur und
/// Katalogsignatur (so sind die meisten Windows-Systemdateien signiert). Kein Netz: Sperrlisten werden nicht abgefragt.</summary>
public static class Authenticode
{
    public static SignatureInfo Check(string path)
    {
        if (!File.Exists(path)) return new(SigState.Fehlt, null, "Die Datei gibt es nicht.");
        if (!OperatingSystem.IsWindows()) return new(SigState.NichtPruefbar, null, "Signaturen prüft Trendo nur unter Windows.");
        try { return CheckWindows(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or DllNotFoundException or EntryPointNotFoundException or COMException)
        { return new(SigState.NichtPruefbar, null, "Nicht prüfbar: " + e.Message); }
    }

    const uint TRUST_E_NOSIGNATURE = 0x800B0100, TRUST_E_SUBJECT_FORM_UNKNOWN = 0x800B0003, TRUST_E_PROVIDER_UNKNOWN = 0x800B0001;

    /// <summary>Fehlercodes von WinVerifyTrust in Alltagssprache.</summary>
    public static string Explain(uint code) => code switch
    {
        0 => "gültig",
        0x80096010 => "Die Datei wurde nach dem Signieren verändert (Prüfsumme passt nicht zur Signatur).",
        0x800B0109 => "Das Zertifikat stammt von keiner vertrauenswürdigen Stelle.",
        0x800B010A => "Die Zertifikatskette ist unvollständig.",
        0x800B0101 => "Das Zertifikat ist abgelaufen.",
        0x800B010C => "Das Zertifikat wurde zurückgezogen.",
        0x800B0111 => "Dem Zertifikat wird auf diesem PC ausdrücklich misstraut.",
        0x800B0004 => "Der Herausgeber gilt auf diesem PC als nicht vertrauenswürdig.",
        0x80092026 => "Die Sicherheitseinstellungen verhindern die Prüfung.",
        TRUST_E_NOSIGNATURE or TRUST_E_SUBJECT_FORM_UNKNOWN or TRUST_E_PROVIDER_UNKNOWN => "Keine Signatur.",
        _ => $"Die Signatur ist nicht gültig (Code 0x{code:X8}).",
    };

    [SupportedOSPlatform("windows")]
    static SignatureInfo CheckWindows(string path)
    {
        var code = VerifyFile(path);
        if (code == 0) return new(SigState.Gueltig, Signer(path), "Gültig signiert (in der Datei).");
        if (code is TRUST_E_NOSIGNATURE or TRUST_E_SUBJECT_FORM_UNKNOWN or TRUST_E_PROVIDER_UNKNOWN)
        {
            foreach (var algo in new[] { "SHA256", null })
                if (VerifyCatalog(path, algo) is { } cat)
                    return cat.Code == 0 ? new(SigState.Gueltig, Signer(cat.Catalog), "Gültig signiert (Windows-Katalog).", true)
                        : new(SigState.Ungueltig, Signer(cat.Catalog), Explain(cat.Code), true);
            return new(SigState.Unsigniert, null, "Die Datei ist nicht signiert.");
        }
        // Datei nicht lesbar (z. B. Store-App-Verknüpfung unter WindowsApps): nicht prüfbar – nicht „ungültig“
        if (code is 0x80092003 or 0x80070005 or 0x80070020) return new(SigState.NichtPruefbar, null, path.Contains(@"\Microsoft\WindowsApps", StringComparison.OrdinalIgnoreCase)
            ? "App aus dem Microsoft Store (nur eine Verknüpfung) – Windows prüft sie selbst, Trendo kann sie nicht lesen." : "Die Datei ließ sich zum Prüfen nicht lesen.");
        return new(SigState.Ungueltig, Signer(path), Explain(code));
    }

    static string? Signer(string signedFile)
    {
        try
        {
#pragma warning disable SYSLIB0057 // Authenticode: X509CertificateLoader liest keine signierten Programmdateien
            using var c = new X509Certificate2(X509Certificate.CreateFromSignedFile(signedFile));
#pragma warning restore SYSLIB0057
            // Firma (O=) vor Zertifikatsname (CN=): „.NET“ wird so zu „Microsoft Corporation“
            var org = c.SubjectName.EnumerateRelativeDistinguishedNames().Where(r => r.GetSingleElementType().Value == "2.5.4.10").Select(r => r.GetSingleElementValue()).FirstOrDefault();
            var n = string.IsNullOrWhiteSpace(org) ? c.GetNameInfo(X509NameType.SimpleName, false) : org;
            return string.IsNullOrWhiteSpace(n) ? null : n.Trim();
        }
        catch (CryptographicException) { return null; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or PlatformNotSupportedException) { return null; }
    }

    // ---- WinVerifyTrust (nur lesend) ----
    static readonly Guid GenericVerifyV2 = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
    const uint WTD_UI_NONE = 2, WTD_REVOKE_NONE = 0, WTD_CHOICE_FILE = 1, WTD_CHOICE_CATALOG = 2, WTD_STATEACTION_VERIFY = 1, WTD_STATEACTION_CLOSE = 2;
    const uint WTD_REVOCATION_CHECK_NONE = 0x10, WTD_CACHE_ONLY_URL_RETRIEVAL = 0x1000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct WINTRUST_FILE_INFO { public uint cbStruct; [MarshalAs(UnmanagedType.LPWStr)] public string pcwszFilePath; public IntPtr hFile; public IntPtr pgKnownSubject; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct WINTRUST_CATALOG_INFO
    {
        public uint cbStruct; public uint dwCatalogVersion;
        [MarshalAs(UnmanagedType.LPWStr)] public string pcwszCatalogFilePath;
        [MarshalAs(UnmanagedType.LPWStr)] public string pcwszMemberTag;
        [MarshalAs(UnmanagedType.LPWStr)] public string pcwszMemberFilePath;
        public IntPtr hMemberFile; public IntPtr pbCalculatedFileHash; public uint cbCalculatedFileHash; public IntPtr pcCatalogContext; public IntPtr hCatAdmin;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct WINTRUST_DATA
    {
        public uint cbStruct; public IntPtr pPolicyCallbackData; public IntPtr pSIPClientData; public uint dwUIChoice; public uint fdwRevocationChecks;
        public uint dwUnionChoice; public IntPtr pUnion; public uint dwStateAction; public IntPtr hWVTStateData; public IntPtr pwszURLReference;
        public uint dwProvFlags; public uint dwUIContext; public IntPtr pSignatureSettings;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct CATALOG_INFO { public uint cbStruct; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string wszCatalogFile; }

    [DllImport("wintrust.dll", ExactSpelling = true)] static extern uint WinVerifyTrust(IntPtr hwnd, ref Guid action, ref WINTRUST_DATA data);
    [DllImport("wintrust.dll", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CryptCATAdminAcquireContext2(out IntPtr hCatAdmin, IntPtr subsystem, string? hashAlgorithm, IntPtr strongHashPolicy, uint flags);
    [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = true)]
    static extern bool CryptCATAdminCalcHashFromFileHandle2(IntPtr hCatAdmin, IntPtr hFile, ref uint cbHash, [Out] byte[]? hash, uint flags);
    [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = true)]
    static extern IntPtr CryptCATAdminEnumCatalogFromHash(IntPtr hCatAdmin, byte[] hash, uint cbHash, uint flags, IntPtr prevCatInfo);
    [DllImport("wintrust.dll", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CryptCATCatalogInfoFromContext(IntPtr hCatInfo, ref CATALOG_INFO info, uint flags);
    [DllImport("wintrust.dll", ExactSpelling = true)] static extern bool CryptCATAdminReleaseCatalogContext(IntPtr hCatAdmin, IntPtr hCatInfo, uint flags);
    [DllImport("wintrust.dll", ExactSpelling = true)] static extern bool CryptCATAdminReleaseContext(IntPtr hCatAdmin, uint flags);

    const uint WTD_REVOKE_WHOLECHAIN = 1, WTD_REVOCATION_CHECK_CHAIN = 0x40;

    /// <summary>T-0764 (5. Prüfrunde H2, N1): nur die in der Datei eingebettete Signatur (keine Katalog-Signatur – eine kopierte Windows-Datei zählt
    /// so nicht) und Sperrlistenprüfung der ganzen Kette. Sperrlisten nur aus dem Zwischenspeicher von Windows (kein Warten auf das Netz);
    /// fehlt dort eine Auskunft, gilt die Signatur als nicht bestätigt (fail-closed).</summary>
    public static SignatureInfo CheckEmbeddedStrict(string path)
    {
        if (!File.Exists(path)) return new(SigState.Fehlt, null, "Die Datei gibt es nicht.");
        if (!OperatingSystem.IsWindows()) return new(SigState.NichtPruefbar, null, "Signaturen prüft Trendo nur unter Windows.");
        try
        {
            var code = Verify(WTD_CHOICE_FILE, new WINTRUST_FILE_INFO { cbStruct = (uint)Marshal.SizeOf<WINTRUST_FILE_INFO>(), pcwszFilePath = path }, revoke: true);
            if (code == 0) return new(SigState.Gueltig, Signer(path), "Gültig signiert (in der Datei, Sperrlisten geprüft).");
            return code is TRUST_E_NOSIGNATURE or TRUST_E_SUBJECT_FORM_UNKNOWN or TRUST_E_PROVIDER_UNKNOWN
                ? new(SigState.Unsigniert, null, "Keine eingebettete Signatur.") : new(SigState.Ungueltig, Signer(path), Explain(code));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or DllNotFoundException or EntryPointNotFoundException or COMException)
        { return new(SigState.NichtPruefbar, null, "Nicht prüfbar: " + e.Message); }
    }

    static uint Verify<T>(uint choice, T info, bool revoke = false) where T : struct
    {
        var p = Marshal.AllocHGlobal(Marshal.SizeOf<T>());
        try
        {
            Marshal.StructureToPtr(info, p, false);
            var d = new WINTRUST_DATA
            {
                cbStruct = (uint)Marshal.SizeOf<WINTRUST_DATA>(), dwUIChoice = WTD_UI_NONE, fdwRevocationChecks = revoke ? WTD_REVOKE_WHOLECHAIN : WTD_REVOKE_NONE, dwUnionChoice = choice, pUnion = p,
                dwStateAction = WTD_STATEACTION_VERIFY, dwProvFlags = (revoke ? WTD_REVOCATION_CHECK_CHAIN : WTD_REVOCATION_CHECK_NONE) | WTD_CACHE_ONLY_URL_RETRIEVAL,
            };
            var g = GenericVerifyV2;
            var code = WinVerifyTrust(IntPtr.Zero, ref g, ref d);
            d.dwStateAction = WTD_STATEACTION_CLOSE; WinVerifyTrust(IntPtr.Zero, ref g, ref d);
            return code;
        }
        finally { Marshal.DestroyStructure<T>(p); Marshal.FreeHGlobal(p); }
    }

    static uint VerifyFile(string path) => Verify(WTD_CHOICE_FILE, new WINTRUST_FILE_INFO { cbStruct = (uint)Marshal.SizeOf<WINTRUST_FILE_INFO>(), pcwszFilePath = path });

    /// <summary>Datei in den Windows-Katalogen suchen; gefunden → Prüfergebnis und Katalogdatei, sonst null.</summary>
    static (uint Code, string Catalog)? VerifyCatalog(string path, string? algo)
    {
        if (!CryptCATAdminAcquireContext2(out var admin, IntPtr.Zero, algo, IntPtr.Zero, 0)) return null;
        try
        {
            byte[] hash;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                var h = fs.SafeFileHandle.DangerousGetHandle(); uint n = 0;
                CryptCATAdminCalcHashFromFileHandle2(admin, h, ref n, null, 0);
                if (n == 0 || n > 256) return null;
                hash = new byte[n];
                if (!CryptCATAdminCalcHashFromFileHandle2(admin, h, ref n, hash, 0)) return null;
            }
            var cat = CryptCATAdminEnumCatalogFromHash(admin, hash, (uint)hash.Length, 0, IntPtr.Zero);
            if (cat == IntPtr.Zero) return null;
            try
            {
                var ci = new CATALOG_INFO { cbStruct = (uint)Marshal.SizeOf<CATALOG_INFO>(), wszCatalogFile = "" };
                if (!CryptCATCatalogInfoFromContext(cat, ref ci, 0)) return null;
                var info = new WINTRUST_CATALOG_INFO
                {
                    cbStruct = (uint)Marshal.SizeOf<WINTRUST_CATALOG_INFO>(), pcwszCatalogFilePath = ci.wszCatalogFile,
                    pcwszMemberTag = Convert.ToHexString(hash), pcwszMemberFilePath = path, hCatAdmin = admin,
                };
                return (Verify(WTD_CHOICE_CATALOG, info), ci.wszCatalogFile);
            }
            finally { CryptCATAdminReleaseCatalogContext(admin, cat, 0); }
        }
        finally { CryptCATAdminReleaseContext(admin, 0); }
    }
}
