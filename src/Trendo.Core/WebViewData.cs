namespace Trendo.Core;

/// <summary>T-0784: Zwischenspeicher der eingebetteten Webansichten (WebView2). Ohne eigenen Ordner legt WebView2 ihn neben der Programmdatei an
/// („TrendoOS.exe.WebView2/“) – also im Installationsordner, den die Reparaturprüfung (X-02) dann als „nicht aus dem Paket“ meldet.
/// Trendo setzt deshalb beim Start für den eigenen Prozess <c>WEBVIEW2_USER_DATA_FOLDER</c> auf einen Ordner im Trendo-Datenordner – nur, wenn die
/// Variable noch nicht gesetzt ist. Webansichten mit eigenem Profil (Claude-Anmeldung, Mixamo) setzen ihren Ordner weiter selbst.</summary>
public static class WebViewData
{
    public const string Variable = "WEBVIEW2_USER_DATA_FOLDER";
    public const string FolderName = "webview2";

    /// <summary>Setzt den Standard-Datenordner für WebView2 auf <c>&lt;dataRoot&gt;/webview2</c>, falls die Variable leer ist. Rückgabe: der gültige Ordner.</summary>
    public static string Ensure(string dataRoot)
    {
        if (Environment.GetEnvironmentVariable(Variable) is { Length: > 0 } set) return set;
        var dir = Path.Combine(Path.GetFullPath(dataRoot), FolderName);
        try { Directory.CreateDirectory(dir); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } // WebView2 legt ihn notfalls selbst an
        Environment.SetEnvironmentVariable(Variable, dir);
        return dir;
    }
}
