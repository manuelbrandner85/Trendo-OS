using System.Runtime.InteropServices;
using System.Text;

namespace Trendo.App;

/// <summary>„PIN vergessen?“ (27.10.2026): mit der Windows-Anmeldung des aktuellen Benutzers bestätigen. Windows zeigt seinen eigenen
/// Anmeldedialog; Trendo prüft das Passwort nur bei Windows (LogonUser) und speichert oder sieht es danach nicht weiter.</summary>
public static class WindowsLogon
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct CREDUI_INFO { public int cbSize; public IntPtr hwndParent; public string pszMessageText; public string pszCaptionText; public IntPtr hbmBanner; }

    [DllImport("credui.dll", CharSet = CharSet.Unicode)]
    static extern int CredUIPromptForWindowsCredentialsW(ref CREDUI_INFO info, int authError, ref uint authPackage, IntPtr inBuffer, uint inSize, out IntPtr outBuffer, out uint outSize, ref bool save, int flags);
    [DllImport("credui.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CredUnPackAuthenticationBufferW(int flags, IntPtr buffer, uint size, StringBuilder user, ref int userLen, StringBuilder domain, ref int domainLen, StringBuilder password, ref int passwordLen);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool LogonUserW(string user, string? domain, string password, int logonType, int provider, out IntPtr token);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    [DllImport("ole32.dll")] static extern void CoTaskMemFree(IntPtr p);

    const int CREDUIWIN_ENUMERATE_CURRENT_USER = 0x200, LOGON32_LOGON_INTERACTIVE = 2, LOGON32_PROVIDER_DEFAULT = 0;

    /// <summary>Fragt die Windows-Anmeldung ab. true nur, wenn Windows das Passwort des aktuellen Benutzers bestätigt.</summary>
    public static bool Confirm(IntPtr owner, string message)
    {
        if (!OperatingSystem.IsWindows()) return false;
        var info = new CREDUI_INFO { cbSize = Marshal.SizeOf<CREDUI_INFO>(), hwndParent = owner, pszCaptionText = "Mit Windows bestätigen", pszMessageText = message };
        uint pkg = 0; var save = false;
        if (CredUIPromptForWindowsCredentialsW(ref info, 0, ref pkg, IntPtr.Zero, 0, out var buf, out var size, ref save, CREDUIWIN_ENUMERATE_CURRENT_USER) != 0) return false;
        var user = new StringBuilder(256); var dom = new StringBuilder(256); var pw = new StringBuilder(256);
        int ul = 256, dl = 256, pl = 256;
        try
        {
            if (!CredUnPackAuthenticationBufferW(0, buf, size, user, ref ul, dom, ref dl, pw, ref pl)) return false;
            var u = user.ToString(); string? d = dom.Length > 0 ? dom.ToString() : null;
            if (d is null && u.Contains('\\')) { d = u[..u.IndexOf('\\')]; u = u[(u.IndexOf('\\') + 1)..]; }
            if (d is not null && d.Equals("MicrosoftAccount", StringComparison.OrdinalIgnoreCase)) { u = d + "\\" + u; d = null; }
            if (!LogonUserW(u, d, pw.ToString(), LOGON32_LOGON_INTERACTIVE, LOGON32_PROVIDER_DEFAULT, out var token)) return false;
            CloseHandle(token);
            return true;
        }
        finally
        {
            pw.Clear(); // Passwort sofort vergessen
            CoTaskMemFree(buf);
        }
    }
}
