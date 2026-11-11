using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Trendo.App;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Etappe „Kreativ &amp; KI“ Teil 1 (T-0610): Texte zusammenfassen, Logos &amp; Marken, Podcast aufnehmen, Comics &amp; Storyboards.</summary>
public class KreativStudios_Ui
{
    static string Export(MainWindow w, string key, string name, string root)
    {
        var spec = MainWindow.Programs[key];
        var e = spec.Exports.Single(x => x.Name == name);
        return e.Write(w.ProgramActive(key)!, Path.Combine(root, "export-" + key + e.Extension));
    }

    [AvaloniaFact(DisplayName = "Kreativ & KI: alle vier Studios haben eine eigene Seite und öffnen eine Vorlage")]
    public void Pages()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            foreach (var (studio, key, tpl) in new[] { ("STUDIO-SUMMARY-01", "zusammenfassen-programm", "beispiel"), ("STUDIO-LOGO-01", "logo-programm", "cafe"), ("STUDIO-PODCAST-01", "podcast-programm", "interview"), ("STUDIO-COMIC-01", "comic-programm", "beispiel") })
            {
                Assert.Equal(key, MainWindow.StudioPages[studio]);
                w.Show(key); Settle();
                RealClick(w, w.Find<Button>("prog-tpl-" + tpl)); Settle();
                Assert.NotNull(w.ProgramActive(key));
            }
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Zusammenfassen: Beispiel zeigt Kernsätze, Länge umschalten ändert die Zahl, Getipptes kommt an, Export als Markdown")]
    public void Summary()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        try
        {
            const string K = "zusammenfassen-programm";
            w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-beispiel")); Settle();
            int Bullets() => w.Find<StackPanel>("sum-result").Children.OfType<Grid>().Count();
            Assert.Equal(5, Bullets());
            RealClick(w, w.Find<Button>("prog-cmd-sum-kurz")); Settle();
            Assert.Equal(3, Bullets());
            var box = w.Find<TextBox>("sum-text"); box.Focus(); box.Text = "Erster Satz über Äpfel. Zweiter Satz über Birnen."; w.Find<TextBox>("sum-title").Focus(); Settle();
            Assert.Equal("Erster Satz über Äpfel. Zweiter Satz über Birnen.", w.ProgramActive(K)!.Text("text"));
            Assert.Contains("Stichwörter", File.ReadAllText(Export(w, K, "md", root)));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Logos: Vorschau hell und dunkel, Form wechseln, falsche Farbe wird gemeldet, SVG-Export ist gültig")]
    public void Logo()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        try
        {
            const string K = "logo-programm";
            w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-cafe")); Settle();
            Assert.NotNull(w.Find<Border>("logo-light")); Assert.NotNull(w.Find<Border>("logo-dark"));
            Assert.Contains(w.Find<Border>("logo-light").GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Café Morgenrot");
            RealClick(w, w.Find<Button>("prog-cmd-logo-form-quadrat")); Settle();
            Assert.Equal("quadrat", w.ProgramActive(K)!.Text("layout"));
            var svg = XElement.Parse(File.ReadAllText(Export(w, K, "svg", root)));
            Assert.Contains("Café Morgenrot", svg.Value);
            var c = w.Find<TextBox>("logo-p-primary"); c.Focus(); c.Text = "rot"; w.Find<TextBox>("logo-p-name").Focus(); Settle();
            w.Show(K); Settle();
            Assert.Contains(w.GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.Contains("als #RRGGBB eintragen") == true);
            Assert.Contains("<svg", File.ReadAllText(Export(w, K, "svg", root))); // ungültige Farbe fällt auf Standard zurück
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Podcast: Kapitel erscheinen wie im Player, Fehler werden genannt, Shownotes und Kapiteldatei, Feed nur mit Aufnahme und https")]
    public void Podcast()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        try
        {
            const string K = "podcast-programm";
            w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-interview")); Settle();
            var view = w.Find<StackPanel>("podc-chapters-view").GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
            Assert.Contains("14:40", view); Assert.Contains("Die besten Pflanzen", view);
            var box = w.Find<TextBox>("podc-chapters"); box.Focus(); box.Text ="00:00 Start\nkaputt"; Settle();
            Assert.Contains(w.Find<StackPanel>("podc-chapters-view").GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.StartsWith("⚠ Zeile 2") == true);
            box.Text = "00:00 Start\n03:00 Thema"; w.Find<TextBox>("podc-p-title").Focus(); Settle();
            Assert.Contains("03:00 Thema", File.ReadAllText(Export(w, K, "notes", root)));
            Assert.Contains("START=180000", File.ReadAllText(Export(w, K, "meta", root)));
            Assert.Throws<InvalidOperationException>(() => Export(w, K, "rss", root));
            var mp3 = Path.Combine(root, "folge.mp3"); File.WriteAllBytes(mp3, new byte[2048]);
            var d = w.ProgramActive(K)!; d.Set("audio", mp3, "Aufnahme"); d.Set("baseUrl", "https://example.org/pod", "Adresse");
            var rss = XDocument.Parse(File.ReadAllText(Export(w, K, "rss", root)));
            Assert.Equal("2048", (string?)rss.Descendants("enclosure").Single().Attribute("length"));
            d.Set("baseUrl", "http://example.org", "Adresse");
            Assert.Throws<ArgumentException>(() => Export(w, K, "rss", root));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Comics: Seite mit Sprechblasen, Aufteilung wechseln ergänzt Bilder, Text bearbeiten, SVG-Export mit eingebettetem Bild")]
    public void Comic()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        try
        {
            const string K = "comic-programm";
            w.Show(K); Settle(); RealClick(w, w.Find<Button>("prog-tpl-beispiel")); Settle();
            Assert.Contains(w.Find<Border>("comic-page").GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Miau!");
            RealClick(w, w.Find<Button>("prog-cmd-comic-layout-6er")); Settle();
            var d = w.ProgramActive(K)!;
            Assert.Equal(6, d.List("panels").Count);
            Assert.Equal("Miau!", (string?)d.List("panels")[2]!["speech"]); // vorhandene Texte bleiben
            var t = w.Find<TextBox>("comic-speech-5"); t.Focus(); t.Text = "Ende."; w.Find<TextBox>("comic-caption-5").Focus(); Settle();
            Assert.Equal("Ende.", (string?)d.List("panels")[5]!["speech"]);
            var png = Path.Combine(root, "bild.png");
            File.WriteAllBytes(png, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="));
            d.Edit("Bild", x => ((System.Text.Json.Nodes.JsonObject)((System.Text.Json.Nodes.JsonArray)x["panels"]!)[0]!)["image"] = png);
            w.Show(K); Settle();
            Assert.Contains(w.Find<Border>("comic-page").GetVisualDescendants().OfType<Image>(), i => i.Source is not null);
            var svg = File.ReadAllText(Export(w, K, "svg", root));
            Assert.Contains("data:image/png;base64,", svg);
            Assert.Contains("Ende.", XElement.Parse(svg).Value);
        }
        finally { w.Close(); }
    }
}
