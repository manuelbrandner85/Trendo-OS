using System.IO.Compression;
using Trendo.Core;
using Xunit;

public class Audit_Spiele_Bilder_Web
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-audit-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "T-0204: Eigenes Spiel schreibt Regeln, gültiges JavaScript und ein ZIP")]
    public void Eigenes_Spiel()
    {
        var e = new SpielEntwurf { Name = "Fang", Art = "breakout", Tempo = 8, Leben = 2, Sieg = 4, Ton = true, Breite = 10, Hoehe = 8, Figur = "#112233", Hintergrund = "#abcdef" };
        e.Raster = Enumerable.Repeat(false, 80).ToList();
        e.Raster[0] = true;
        var dir = SpielBaukasten.Bauen(Tmp(), e);
        var html = File.ReadAllText(Path.Combine(dir, "index.html"));
        Assert.Contains("window.REGELN", html);
        Assert.Contains("\"tempo\":8", html);
        Assert.Contains("\"leben\":2", html);
        Assert.Contains("\"figurBild\":\"\"", html);
        Assert.Contains("game.js", html);
        var js = Path.Combine(dir, "game.js");
        Assert.Contains("AudioContext", File.ReadAllText(js));
        if (Cli.Find("node") is not null)
        {
            var r = Cli.Run("node", ["--check", js]);
            Assert.True(r.Ok, r.Err);
        }
        var zip = SpielBaukasten.Zip(dir);
        using var pack = ZipFile.OpenRead(zip);
        Assert.Contains(pack.Entries, x => x.Name == "index.html");
        Assert.Contains(pack.Entries, x => x.Name == "game.js");
    }

    [Fact(DisplayName = "T-0205: JPEG einer roten Fläche lässt sich lesen, Text ändert Pixel")]
    public void Bild_und_Jpeg()
    {
        Assert.Equal(12, Jpeg.TabellenSumme(new byte[] { 0, 1, 5, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0 }));
        var rot = new byte[16 * 16 * 3];
        for (var i = 0; i < 16 * 16; i++) rot[i * 3] = 220;
        var jpg = Jpeg.Encode(new RgbImage(16, 16, rot), 95);
        Assert.Equal(0xFF, jpg[0]); Assert.Equal(0xD8, jpg[1]);
        Assert.Equal(0xFF, jpg[^2]); Assert.Equal(0xD9, jpg[^1]);
        var dir = Tmp();
        var datei = Path.Combine(dir, "rot.jpg");
        File.WriteAllBytes(datei, jpg);
        if (Cli.Find("ffmpeg") is { } ff)
        {
            var png = Path.Combine(dir, "rot.png");
            var r = Cli.Run(ff, ["-y", "-i", datei, png]);
            Assert.True(r.Ok, r.Err);
            var back = Vision.Load(png);
            Assert.True(back.Rgb[0] > 180, "Rotkanal " + back.Rgb[0]);
            Assert.True(back.Rgb[1] < 80, "Grünkanal " + back.Rgb[1]);
            Assert.True(back.Rgb[2] < 80, "Blaukanal " + back.Rgb[2]);
        }
        var weiss = new byte[80 * 30 * 3];
        Array.Fill(weiss, (byte)255);
        var mit = BildStudio.Text(new RgbImage(80, 30, weiss), "Hallo", 2, 2, 0, 0, 0);
        Assert.Contains(mit.Rgb, b => b < 20);
        var gedreht = BildStudio.Drehen90(new RgbImage(2, 1, [255, 0, 0, 0, 0, 255]));
        Assert.Equal(1, gedreht.Width); Assert.Equal(2, gedreht.Height);
        Assert.Equal(255, gedreht.Rgb[0]);
        Assert.Equal(0, gedreht.Rgb[3]);
        var hell = BildStudio.Helligkeit(new RgbImage(1, 1, [10, 10, 10]), 40, 0);
        Assert.True(hell.Rgb[0] > 10);
        var pngOut = Path.Combine(dir, "text.png");
        BildStudio.SavePng(mit, pngOut);
        Assert.Equal(80, Vision.Load(pngOut).Width);
    }

    [Fact(DisplayName = "T-0206: Überschrift wird erst in der Vorschau gezeigt und erst beim Übernehmen geschrieben")]
    public void Ueberschrift_vorschau()
    {
        var dir = DevStudio.Create(Tmp(), "web", "Hallo Web");
        var vorher = File.ReadAllText(Path.Combine(dir, "index.html"));
        var hit = WebArbeit.Ueberschrift(dir, "Ändere die Überschrift in Willkommen daheim");
        Assert.NotNull(hit);
        Assert.Equal(vorher, File.ReadAllText(Path.Combine(dir, "index.html")));
        Assert.Contains("Willkommen daheim", hit.Value.Nachher);
        Assert.DoesNotContain("Willkommen daheim", hit.Value.Vorher);
        var blick = WebArbeit.Blick(dir);
        Assert.False(string.IsNullOrWhiteSpace(blick.Ueberschrift));
        WebArbeit.Speichern(hit.Value.Datei, hit.Value.Nachher);
        Assert.Contains("Willkommen daheim", WebArbeit.Blick(dir).Ueberschrift);
        Assert.Contains(WebArbeit.Dateien(dir), f => f.EndsWith("index.html"));
    }

    [Fact(DisplayName = "T-0207: Feste Listen treffen die Rechenfunktionen, Zahlenfelder erkennen die Einheit")]
    public void Fach_auswahl()
    {
        Assert.Equal(["1", "2", "3", "4"], FachChoices.For("eng-beam", "case")!);
        Assert.Contains("Aluminium", FachChoices.For("cnc-feeds", "mat")!);
        Assert.Contains("YouTube", FachChoices.For("bc-loud", "pf")!);
        Assert.Equal("m", FachChoices.Einheit("Länge (m)"));
        Assert.Null(FachChoices.Einheit("Code oder Wert"));
        Assert.True(FachChoices.IstZahl("Länge (m)", "2"));
        Assert.False(FachChoices.IstZahl("Code oder Wert", "104"));
        Assert.True(FachChoices.JaNein("voll? (j/n)"));
        Assert.Equal("architektur", FachChoices.Studio("arch-heat"));
        var root = Tmp();
        Fach.Save(root, "technik", "stand", new FachStand { Eingaben = new() { ["eng-beam-len"] = "3" }, Ergebnisse = new() { ["eng-beam"] = "gemerkt" } });
        var geladen = Fach.Load<FachStand>(root, "technik", "stand");
        Assert.Equal("3", geladen.Eingaben["eng-beam-len"]);
    }
}
