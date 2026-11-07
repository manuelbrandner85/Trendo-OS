using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Web Design Studio im Studio „Webseite bauen“: Bereich mit Knöpfen, Speichern über die Editor-Brücke, Export als fertige Seite.</summary>
public class WebDesign_Ui
{
    static void Pump() { for (var i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs(); }

    static string EditorDir()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (Directory.Exists(Path.Combine(d.FullName, "src", "Trendo.App", "Assets", "webdesign"))) return Path.Combine(d.FullName, "src", "Trendo.App", "Assets", "webdesign");
        throw new DirectoryNotFoundException("Editor-Bündel nicht gefunden");
    }

    [AvaloniaFact(DisplayName = "Visueller Editor: Knöpfe da, im Testmodus kein WebView; „speichern“ vom Editor schreibt die Seite, Export legt die fertige Seite an")]
    public void Visual_editor()
    {
        MainWindow.WebDesignEditorDirOverride = EditorDir();
        try
        {
            var (w, _, _) = Ui.Open();
            w.Show("dev"); Pump();
            Assert.NotNull(w.Find<Button>("web-visual-export")); Assert.NotNull(w.Find<Button>("web-visual-browser"));
            w.Find<Button>("web-visual-open").Press(); Pump();
            Assert.Empty(w.GetVisualDescendants().OfType<NativeWebView>()); // Testmodus lädt keinen Browser
            Assert.Contains(w.GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.Contains("Testmodus: Der Editor wird nicht", StringComparison.Ordinal) == true);
            w.WebDesignMessage("""{"art":"speichern","daten":{"root":{"props":{"title":"Muster-Café"}},"content":[{"type":"Ueberschrift","props":{"id":"u1","text":"Unser Kaffee","level":"h2"}}],"zones":{}}}""");
            Assert.StartsWith("Gespeichert", w.WebDesignNote);
            Assert.Contains("Unser Kaffee", WebDesign.Load(w.WebDesignDir));
            w.WebDesignMessage("""{"art":"speichern","daten":{"kaputt":true}}"""); // ungültig: nichts überschrieben
            Assert.Contains("unvollständig", w.WebDesignNote); Assert.Contains("Unser Kaffee", WebDesign.Load(w.WebDesignDir));
            w.WebDesignExport();
            Assert.StartsWith("Fertige Webseite", w.WebDesignNote);
            Assert.Contains("<title>Muster-Café</title>", File.ReadAllText(Path.Combine(w.WebDesignDir, WebDesign.ExportDir, "index.html")));
        }
        finally { MainWindow.WebDesignEditorDirOverride = null; }
    }
}
