using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0784 (Volltest 2, X-02): WebView2 legte seinen Zwischenspeicher neben der Programmdatei an („TrendoOS.exe.WebView2/…“),
/// die Reparaturprüfung meldete das als „nicht aus dem Paket“. Jetzt: Standard-Datenordner im Trendo-Datenordner, und der Ordner älterer Fassungen
/// gilt als bekannter Zwischenspeicher. Ein Ordner mit anderem Namen wird weiter gemeldet.</summary>
[Collection("T0763 Lastprobe")] // nicht parallel: ändert eine Umgebungsvariable des Prozesses
public class T0784WebViewDataTests
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0784-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "T-0784: „TrendoOS.exe.WebView2/…“ älterer Fassungen ist kein Fund; „fremd.WebView2/…“ und eine Datei „TrendoOS.exe.WebView2“ ohne Ordner schon")]
    public void Old_webview_cache_is_not_extra()
    {
        var root = Tmp(); var app = T0763SystemAlltagTests.FakeInstall(root);
        var cache = Path.Combine(app, "TrendoOS.exe.WebView2", "EBWebView", "Default"); Directory.CreateDirectory(cache);
        File.WriteAllText(Path.Combine(cache, "Preferences"), "{}");
        File.WriteAllText(Path.Combine(app, "TrendoOS.exe.WebView2", "EBWebView", "Local State"), "{}");
        var ok = InstallIntegrity.Check(app);
        Assert.True(ok.Ok); Assert.Empty(ok.Extra);

        var fremd = Path.Combine(app, "fremd.WebView2", "EBWebView"); Directory.CreateDirectory(fremd);
        File.WriteAllText(Path.Combine(fremd, "Local State"), "{}");
        File.WriteAllText(Path.Combine(app, "Assets", "TrendoOS.exe.WebView2"), "x"); // gleicher Name, aber keine Ordnerwurzel
        var r = InstallIntegrity.Check(app);
        Assert.Equal(["Assets/TrendoOS.exe.WebView2", "fremd.WebView2/EBWebView/Local State"], r.Extra);
        Assert.True(InstallIntegrity.WebViewCache("TrendoOS.exe.WebView2/EBWebView/x"));
        Assert.False(InstallIntegrity.WebViewCache("TrendoOS.exe.WebView2"));
        Assert.False(InstallIntegrity.WebViewCache("unter/TrendoOS.exe.WebView2/x"));
    }

    [Fact(DisplayName = "T-0784: WEBVIEW2_USER_DATA_FOLDER zeigt nach dem Start in den Trendo-Datenordner, nie in den Programmordner; eine schon gesetzte Variable bleibt")]
    public void Webview_folder_in_data_root()
    {
        var before = Environment.GetEnvironmentVariable(WebViewData.Variable);
        try
        {
            var data = Tmp();
            Environment.SetEnvironmentVariable(WebViewData.Variable, null);
            var dir = WebViewData.Ensure(data);
            Assert.Equal(Path.Combine(Path.GetFullPath(data), "webview2"), dir);
            Assert.Equal(dir, Environment.GetEnvironmentVariable(WebViewData.Variable));
            Assert.True(Directory.Exists(dir));
            Assert.False(dir.StartsWith(AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase));
            // schon gesetzt (von außen oder vom ersten Fenster): bleibt
            Assert.Equal(dir, WebViewData.Ensure(Tmp()));
            Assert.Equal(dir, Environment.GetEnvironmentVariable(WebViewData.Variable));
        }
        finally { Environment.SetEnvironmentVariable(WebViewData.Variable, before); }
    }
}
