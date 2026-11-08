using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Alle Studios, Runde 8 – X-07: Reiter „Symbole“ (Iconify). Abruf nachgebildet; mit echten Daten im Prüfordner zusätzlich ein Bild der Seite.</summary>
public class MediaIcons_Ui
{
    const string Collections = """{"mdi":{"name":"Material Design Icons","total":2,"author":{"name":"Pictogrammers","url":"https://x"},"license":{"title":"Apache 2.0","spdx":"Apache-2.0","url":"https://l"}},"gi":{"name":"Game Icons","total":1,"author":{"name":"GI","url":"https://g"},"license":{"title":"CC BY 3.0","spdx":"CC-BY-3.0","url":"https://cc"}},"nc":{"name":"Privat","total":5,"license":{"title":"CC BY-NC","spdx":"CC-BY-NC-4.0"}}}""";
    const string Mdi = """{"prefix":"mdi","width":24,"height":24,"icons":{"home":{"body":"<path fill=\"currentColor\" d=\"M10 20v-6h4v6h5v-8h3L12 3L2 12h3v8z\"/>"},"heart":{"body":"<path fill=\"currentColor\" d=\"M12 21L3 12a5 5 0 0 1 9-6a5 5 0 0 1 9 6z\"/>"},"old":{"body":"<path d=\"M0 0\"/>","hidden":true}}}""";
    const string Gi = """{"prefix":"gi","width":512,"height":512,"icons":{"sword":{"body":"<path fill=\"currentColor\" d=\"M0 0L512 512\"/>"}}}""";

    [AvaloniaFact(DisplayName = "Symbole-Reiter: Sätze laden (nur freie sichtbar), suchen, Klick speichert SVG; CC BY mit Namensnennung in Lizenzen.txt; versteckte Symbole fehlen")]
    public async Task Icons()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1500; w.Height = 1000; var urls = new List<string>();
        var real = Path.Combine(Path.GetTempPath(), "trendo-tools-b", "iconify");
        var useReal = Environment.GetEnvironmentVariable("TRENDO_ICON_SHOT") is { Length: > 0 } && File.Exists(Path.Combine(real, "mdi.json"));
        MainWindow.IconFetchOverride = u =>
        {
            urls.Add(u); var n = u[(u.LastIndexOf('/') + 1)..];
            if (useReal) return Task.FromResult(File.ReadAllText(Path.Combine(real, n)));
            return Task.FromResult(n switch { "collections.json" => Collections, "mdi.json" => Mdi, "gi.json" => Gi, _ => throw new HttpRequestException("404") });
        };
        try
        {
            w.Show("assets"); w.Find<Button>("mb-tab-symbole").Press(); Settle();
            w.Find<Button>("mb-icons-load").Press();
            for (var i = 0; i < 50 && w.TryFind<ComboBox>("mb-icons-set") is null; i++) { await Task.Delay(20); Settle(); }
            var items = w.Find<ComboBox>("mb-icons-set").ItemsSource!.Cast<string>().ToList();
            if (!useReal) { Assert.Equal(2, items.Count); Assert.DoesNotContain(items, x => x.StartsWith("Privat")); }
            await w.IconSearch("mdi", useReal ? "arrow" : "");
            if (!useReal) Assert.Equal(["home", "heart"], w.IconHits.OrderByDescending(x => x).ToList());
            Settle(); Assert.NotNull(w.Find<Button>("mb-icon-0"));
            if (useReal)
            {
                var frame = w.CaptureRenderedFrame(); frame?.Save(Path.Combine(Path.GetTempPath(), "trendo-symbole-seite.png"));
                return;
            }
            w.Find<Button>("mb-icon-0").Press();
            Assert.StartsWith("✓ Gespeichert: mdi-", w.IconMessage); Assert.Contains("frei nutzbar", w.IconMessage);
            await w.IconSearch("gi", "sword"); var f = w.IconSave("sword")!;
            Assert.Contains("Namensnennung nötig", w.IconMessage);
            Assert.Contains("„Game Icons“ von GI – CC BY 3.0", File.ReadAllText(Path.Combine(Path.GetDirectoryName(f)!, "Lizenzen.txt")));
            await w.IconSearch("nc", ""); Assert.StartsWith("⚠ Diesen Symbolsatz bietet Trendo nicht an", w.IconMessage);
            Assert.All(urls, u => Assert.Contains(IconSets.Commit, u));
            Assert.Equal(1, urls.Count(u => u.EndsWith("mdi.json"))); // danach aus dem Zwischenspeicher
        }
        finally { MainWindow.IconFetchOverride = null; w.Close(); }
    }
}
