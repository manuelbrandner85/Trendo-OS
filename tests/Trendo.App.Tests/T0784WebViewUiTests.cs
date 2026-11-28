using Avalonia.Headless.XUnit;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0784 (Volltest 2, X-02): Nach dem Öffnen eines Trendo-Fensters zeigt der Standard-Datenordner von WebView2 in einen Trendo-Datenordner
/// („…/webview2“), nie in den Programmordner – Webansichten ohne eigenes Profil (Claude-Vorschau, IFC, Vektorkarte, Webdesign) schreiben damit nichts
/// mehr neben die Programmdatei.</summary>
public class T0784WebView_Ui
{
    [AvaloniaFact(DisplayName = "T-0784: Fenster offen → WEBVIEW2_USER_DATA_FOLDER liegt in einem Datenordner (…/webview2), nicht im Programmordner")]
    public void Window_sets_webview_folder()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            var dir = Environment.GetEnvironmentVariable(WebViewData.Variable);
            Assert.False(string.IsNullOrEmpty(dir));
            Assert.Equal(WebViewData.FolderName, Path.GetFileName(dir!.TrimEnd(Path.DirectorySeparatorChar)));
            Assert.False(Path.GetFullPath(dir).StartsWith(Path.GetFullPath(AppContext.BaseDirectory), StringComparison.OrdinalIgnoreCase));
        }
        finally { w.Close(); }
    }
}
