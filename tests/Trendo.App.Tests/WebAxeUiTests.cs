using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Alle Studios, Runde 7 – CORE-07: „Barrierefreiheit genau prüfen (axe-core)“ in der Qualitätsprüfung (axe nachgebildet; echter Lauf in AxeCheckTests).</summary>
public class WebAxe_Ui
{
    const string Answer = """{"axe":"4.14.0","results":[{"file":"index.html","passes":24,"violations":[{"id":"image-alt","impact":"critical","help":"x","nodes":1,"target":"img"},{"id":"region","impact":"moderate","help":"x","nodes":3,"target":"p"}]}]}""";

    [AvaloniaFact(DisplayName = "axe-Oberfläche: ohne Einrichtung Einrichten-Knopf; mit axe → Befunde auf Deutsch (Fehler, • Hinweis) mit Version und Zahl der bestandenen Regeln")]
    public void Check()
    {
        var (w, _, root) = Ui.Open();
        var site = Path.Combine(root, "seite-axe"); Directory.CreateDirectory(site); File.WriteAllText(Path.Combine(site, "index.html"), "<html><body><img src=a.jpg></body></html>");
        try
        {
            MainWindow.AxeDirOverride = null; MainWindow.AxeRunOverride = null;
            w.WebDir = site; w.Show("dev");
            Assert.NotNull(w.Find<Button>("web-axe-setup"));
            IReadOnlyList<string>? seen = null;
            MainWindow.AxeDirOverride = Path.Combine(root, "axe-fake"); MainWindow.AxeRunOverride = (_, a, _) => { seen = a; return new Cli.Result(0, Answer, ""); };
            w.Show("dev"); w.Find<Button>("web-axe-run").Press();
            Assert.StartsWith("axe-core 4.14.0: 1 Seite(n), 24 Regeln bestanden, 2 Befund(e).", w.AxeMessage);
            Assert.Contains("index.html: Bild ohne Alternativtext", w.AxeMessage); Assert.Contains("• index.html: Inhalt steht außerhalb von Bereichen", w.AxeMessage);
            Assert.EndsWith("index.html", seen![^1]); Assert.Equal(2, w.AxeLast!.Findings.Count);
            Assert.Contains("Bild ohne Alternativtext", w.Find<TextBlock>("web-axe-msg").Text);
        }
        finally { MainWindow.AxeDirOverride = null; MainWindow.AxeRunOverride = null; w.Close(); }
    }
}
