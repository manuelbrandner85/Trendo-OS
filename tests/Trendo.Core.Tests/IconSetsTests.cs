using System.Xml.Linq;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Alle Studios, Runde 8 – X-07: Iconify-Symbolsätze (reine Daten, Lizenz je Satz). Echtdaten-Test nutzt die geladenen Sätze im Prüfordner.</summary>
public class IconSetsTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "trendo-icons-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    const string Collections = """
        {"mdi":{"name":"Material Design Icons","total":7447,"author":{"name":"Pictogrammers","url":"https://x"},"license":{"title":"Apache 2.0","spdx":"Apache-2.0","url":"https://l"},"category":"Material","height":24},
         "gi":{"name":"Game Icons","total":4133,"author":{"name":"GameIcons","url":"https://g"},"license":{"title":"CC BY 3.0","spdx":"CC-BY-3.0","url":"https://cc"}},
         "nc":{"name":"Nur privat","total":99999,"author":{"name":"A"},"license":{"title":"CC BY-NC 4.0","spdx":"CC-BY-NC-4.0","url":"https://nc"}},
         "../boese":{"name":"x","total":1,"license":{"spdx":"MIT"}}}
        """;
    const string Set = """
        {"prefix":"mdi","width":24,"height":24,
         "icons":{"home":{"body":"<path fill=\"currentColor\" d=\"M10 20v-6h4v6h5v-8h3L12 3L2 12h3v8z\"/>"},
                  "arrow-left":{"body":"<path d=\"M20 11v2H8l5.5 5.5l-1.42 1.42L4.16 12l7.92-7.92L13.5 5.5L8 11z\"/>"},
                  "wide":{"body":"<path d=\"M0 0h32v16H0z\"/>","width":32,"height":16},
                  "bad":{"body":"<path d=\"M0 0\" onload=\"alert(1)\"/>"},
                  "ext":{"body":"<image href=\"https://x/y.png\"/>"}},
         "aliases":{"arrow-right":{"parent":"arrow-left","hFlip":true},"home-turned":{"parent":"home","rotate":1},"chain":{"parent":"arrow-right"}}}
        """;

    [Fact(DisplayName = "Symbole +1: Lizenzregel – MIT/Apache/OFL/CC0 frei, CC BY mit Nennung, nicht-kommerziell/SA/GPL/unbekannt ausgeschlossen")]
    public void Policy()
    {
        foreach (var f in new[] { "MIT", "Apache-2.0", "ISC", "BSD-3-Clause", "Unlicense", "CC0-1.0", "OFL-1.1" }) Assert.Equal(IconSets.Use.Frei, IconSets.Policy(f));
        Assert.Equal(IconSets.Use.MitNennung, IconSets.Policy("CC-BY-4.0"));
        foreach (var x in new[] { "CC-BY-NC-4.0", "CC-BY-NC-SA-4.0", "CC-BY-SA-4.0", "GPL-3.0", "GPL-2.0-only", "MPL-2.0", "?" }) Assert.Equal(IconSets.Use.Ausgeschlossen, IconSets.Policy(x));
    }

    [Fact(DisplayName = "Symbole +2: Satzliste lesen – freie zuerst, ungültige Kürzel verworfen, Namensnennung als Text")]
    public void Collections_parse()
    {
        var l = IconSets.ParseCollections(Collections);
        Assert.Equal(["mdi", "gi", "nc"], l.Select(s => s.Prefix));
        Assert.Equal((IconSets.Use.Frei, 24), (l[0].Use, l[0].Height));
        Assert.Equal("„Game Icons“ von GameIcons – CC BY 3.0 (https://cc)", l[1].Attribution);
        Assert.Throws<ArgumentException>(() => IconSets.SetUrl("../json"));
        Assert.Equal("https://raw.githubusercontent.com/iconify/icon-sets/" + IconSets.Commit + "/json/mdi.json", IconSets.SetUrl("mdi"));
    }

    [Fact(DisplayName = "Symbole +3: SVG mit viewBox und Größe; breite Symbole behalten ihr Seitenverhältnis; gültiges XML")]
    public void Svg()
    {
        var s = new IconSets.Icons(Set);
        var home = s.Svg("home", 48)!;
        Assert.StartsWith("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"48\" height=\"48\" viewBox=\"0 0 24 24\">", home);
        Assert.Contains("viewBox=\"0 0 32 16\"", s.Svg("wide", 24)); Assert.Contains("width=\"48\" height=\"24\"", s.Svg("wide", 24));
        XDocument.Parse(home);
    }

    [Fact(DisplayName = "Symbole +4: Aliasse – Spiegeln und Drehen werden als Transformation angewandt, Kette bis zum Original")]
    public void Aliases()
    {
        var s = new IconSets.Icons(Set);
        Assert.Contains("<g transform=\"translate(24 0) scale(-1 1)\">", s.Svg("arrow-right"));
        Assert.Contains("<g transform=\"rotate(90 12 12)\">", s.Svg("home-turned"));
        Assert.Equal(s.Svg("arrow-right"), s.Svg("chain"));
        Assert.Null(s.Svg("gibtsnicht"));
    }

    [Fact(DisplayName = "Symbole +5: Suche – alle Wörter müssen vorkommen, kürzeste zuerst, Aliasse eingeschlossen")]
    public void Search()
    {
        var s = new IconSets.Icons(Set);
        Assert.Equal(["arrow-left", "arrow-right"], s.Search("arrow"));
        Assert.Equal(["arrow-right"], s.Search("right arrow"));
        Assert.Equal(["home", "home-turned"], s.Search("home"));
    }

    [Fact(DisplayName = "Symbole −1: unsichere Inhalte (Ereignis-Attribute, eingebettete Bilder, externe Verweise, Skripte) werden nicht ausgegeben; interne #-Verweise bleiben erlaubt")]
    public void Unsafe()
    {
        var s = new IconSets.Icons(Set);
        Assert.Null(s.Svg("bad")); Assert.Null(s.Svg("ext"));
        Assert.False(IconSets.SafeBody("<script>x</script>")); Assert.False(IconSets.SafeBody("<path fill=\"url(https://x)\"/>")); Assert.False(IconSets.SafeBody("<a href=javascript:x>"));
        Assert.True(IconSets.SafeBody("<defs><linearGradient id=\"a\"/></defs><path fill=\"url(#a)\" d=\"M0 0\"/><use href=\"#a\"/>")); // interne Wiederverwendung (echte Sätze, z. B. tabler)
        Assert.False(IconSets.SafeBody("<use href=\"https://x/s.svg#a\"/>"));
        Assert.True(IconSets.SafeBody("<path fill=\"url( '#a' )\" d=\"M0 0\"/>"));
    }

    [Fact(DisplayName = "Symbole −2: Speichern – ausgeschlossene Lizenz verweigert; CC BY schreibt Namensnennung einmal in Lizenzen.txt")]
    public void Save()
    {
        var sets = IconSets.ParseCollections(Collections); var s = new IconSets.Icons(Set);
        Assert.Throws<InvalidOperationException>(() => IconSets.Save(_dir, sets[2], s, "home"));
        var f = IconSets.Save(_dir, sets[1], s, "home"); IconSets.Save(_dir, sets[1], s, "home");
        Assert.Equal("gi-home.svg", Path.GetFileName(f)); XDocument.Load(f);
        Assert.Single(File.ReadAllLines(Path.Combine(_dir, "Lizenzen.txt")));
        Assert.Throws<InvalidOperationException>(() => IconSets.Save(_dir, sets[0], s, "bad"));
    }
}

/// <summary>Echte Daten: Iconify-Sätze vom gepinnten Stand (mdi Apache-2.0, tabler MIT, game-icons CC BY 3.0, fluent-emoji-flat MIT) – nur gelesen.</summary>
public class IconSetsRealTests
{
    static string Dir => Path.Combine(Path.GetTempPath(), "trendo-tools-b", "iconify");
    public sealed class IconFactAttribute : FactAttribute { public IconFactAttribute() { if (!File.Exists(Path.Combine(Dir, "collections.json"))) Skip = "Übersprungen – keine Iconify-Daten im Prüfordner."; } }

    [IconFact(DisplayName = "Symbole echt: 276 Sätze mit Lizenzregel; in 4 echten Sätzen (21 000+ Symbole inkl. Aliasse) wird jedes Symbol sicher und als gültiges XML ausgegeben")]
    public void Real_sets()
    {
        var sets = IconSets.ParseCollections(File.ReadAllText(Path.Combine(Dir, "collections.json")));
        Assert.True(sets.Count > 250); Assert.Contains(sets, s => s.Use == IconSets.Use.Ausgeschlossen);
        Assert.Equal(IconSets.Use.MitNennung, sets.Single(s => s.Prefix == "game-icons").Use);
        var total = 0;
        foreach (var p in new[] { "mdi", "tabler", "game-icons", "fluent-emoji-flat" })
        {
            var icons = new IconSets.Icons(File.ReadAllText(Path.Combine(Dir, p + ".json")));
            Assert.Equal(p, icons.Prefix); Assert.InRange(sets.Single(s => s.Prefix == p).Total - icons.Count, 0, icons.AliasCount); // Iconify zählt teils Aliasse mit, nie versteckte
            foreach (var n in icons.Names) { var svg = icons.Svg(n); Assert.True(svg is not null, $"{p}:{n}"); XDocument.Parse(svg!); total++; }
        }
        Assert.True(total > 21_000, total.ToString());
    }
}
