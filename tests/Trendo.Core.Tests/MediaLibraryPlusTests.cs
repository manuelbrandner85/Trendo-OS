using Trendo.Core.Bibliothek;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Freie Bilder & Klänge – Vorbild-Funktionen (Openverse, Unsplash, Pexels, Google Fonts; 04.11.2026): zweite Quelle, Mischen, Ausrichtung, Namensnennung, Schriften.</summary>
public class MediaLibraryPlusTests
{
    const string Audio = """
        {"results":[
          {"id":"k1","title":"Regen &amp; Wind","foreign_landing_url":"https://freesound.org/s/1","url":"https://cdn.freesound.org/previews/1.mp3","creator":"Max Beispiel","license":"by","license_version":"4.0","license_url":"https://creativecommons.org/licenses/by/4.0/","source":"freesound","filetype":"mp3","filesize":1379890,"duration":12500},
          {"id":"k2","title":"Gesperrt","url":"https://cdn.freesound.org/previews/2.mp3","license":"by-nd","license_version":"4.0","filetype":"mp3"},
          {"id":"k3","title":"Wald","url":"https://cdn.freesound.org/previews/3.mp3","license":"cc0","license_version":"1.0","filetype":"mp3","source":"freesound"},
          {"id":"k4","title":"Video","url":"https://example.org/4.mp4","license":"cc0","filetype":"mp4"}
        ]}
        """;

    [Fact(DisplayName = "Openverse: Lizenzname, Dauer in Sekunden, HTML-Zeichen, ohne „ND/NC“ und fremde Formate; Filter frei/Nennung; Suchadresse nur erlaubte Lizenzen")]
    public void Openverse_parse()
    {
        var all = MediaLibrary.ParseOpenverse(Audio, "klang");
        Assert.Equal(["Regen & Wind", "Wald"], all.Select(h => h.Title));
        var r = all[0];
        Assert.Equal(("CC BY 4.0", 12.5, "Openverse · freesound", "ov-k1"), (r.License, r.Seconds, r.Provider, r.Id));
        Assert.False(r.Free); Assert.True(all[1].Free); Assert.Equal("CC0 1.0", all[1].License);
        Assert.Equal(["Wald"], MediaLibrary.ParseOpenverse(Audio, "klang", "frei").Select(h => h.Title));
        Assert.Equal(["Regen & Wind"], MediaLibrary.ParseOpenverse(Audio, "klang", "nennung").Select(h => h.Title));
        Assert.Empty(MediaLibrary.ParseOpenverse(Audio, "klang", "alle", "ogg"));
        Assert.Contains("/v1/audio/?q=Regen%20Wind", MediaLibrary.OpenverseUrl("klang", " Regen Wind "));
        Assert.EndsWith("license=cc0,pdm", MediaLibrary.OpenverseUrl("bild", "x", "frei"));
        Assert.Equal("Public Domain Mark", MediaLibrary.LicenseName("pdm", "1.0")); Assert.True(MediaLibrary.IsFree("Public Domain Mark"));
        Assert.Empty(MediaLibrary.ParseOpenverse("{}", "bild"));
    }

    static MediaHit Img(string id, int w, int h, string file = "") => new(id, "bild", id, "Erika Muster", "CC BY-SA 4.0", "https://creativecommons.org/licenses/by-sa/4.0/", "https://example.org/" + id, file.Length > 0 ? file : "https://upload.wikimedia.org/" + id, "", "jpg", 0, w, h, 0);

    [Fact(DisplayName = "Mischen abwechselnd ohne doppelte Dateien; Ausrichtung quer/hoch/quadrat (±8 %) und Mindestbreite")]
    public void Mix_and_orientation()
    {
        var mixed = MediaLibrary.Mix([Img("a", 1, 1), Img("b", 1, 1), Img("c", 1, 1)], [Img("x", 1, 1), Img("dup", 1, 1, "https://upload.wikimedia.org/a")]);
        Assert.Equal(["a", "x", "b", "c"], mixed.Select(h => h.Id));
        Assert.Equal(("quer", "hoch", "quadrat", "quadrat", "unbekannt"), (MediaLibrary.Orientation(Img("1", 1600, 900)), MediaLibrary.Orientation(Img("2", 900, 1600)),
            MediaLibrary.Orientation(Img("3", 1000, 1000)), MediaLibrary.Orientation(Img("4", 1050, 1000)), MediaLibrary.Orientation(Img("5", 0, 0))));
        List<MediaHit> hits = [Img("q", 1600, 900), Img("h", 900, 1600), Img("klein", 300, 200)];
        Assert.Equal(["h"], MediaLibrary.Filter(hits, "hoch").Select(h => h.Id));
        Assert.Equal(["q"], MediaLibrary.Filter(hits, "alle", 1000).Select(h => h.Id));
    }

    [Fact(DisplayName = "Namensnennung als Text, HTML (maskiert, mit Links) und Markdown")]
    public void Credits()
    {
        var h = Img("Berg <im> Nebel", 10, 10) with { Title = "Berg <im> Nebel" };
        Assert.Equal(h.Credit, MediaLibrary.CreditAs(h, "text"));
        var html = MediaLibrary.CreditAs(h, "html");
        Assert.Contains("„Berg &lt;im&gt; Nebel“</a> von Erika Muster", html); Assert.Contains("<a href=\"https://creativecommons.org/licenses/by-sa/4.0/\">CC BY-SA 4.0</a>", html);
        Assert.StartsWith("[„Berg <im> Nebel“](https://example.org/", MediaLibrary.CreditAs(h, "md"));
        Assert.Contains("[CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/)", MediaLibrary.CreditAs(h, "md"));
    }

    [Fact(DisplayName = "Schriften: nur offene Lizenzen und Latin, Art-Filter, Wortanfang zuerst, Datei-Adresse mit nächster Stärke")]
    public void Fonts()
    {
        const string json = """
            [{"id":"roboto-slab","family":"Roboto Slab","subsets":["latin"],"weights":[100,300,700],"category":"serif","license":"OFL-1.1"},
             {"id":"roboto","family":"Roboto","subsets":["latin","greek"],"weights":[400,700],"category":"sans-serif","license":"OFL-1.1","variable":true},
             {"id":"noto-roboto","family":"Noto Roboto","subsets":["latin"],"weights":[400],"category":"sans-serif","license":"Apache License 2.0"},
             {"id":"nur-griechisch","family":"Roboto Greek","subsets":["greek"],"weights":[400],"category":"sans-serif","license":"OFL-1.1"},
             {"id":"teuer","family":"Roboto Pro","subsets":["latin"],"weights":[400],"category":"sans-serif","license":"Commercial"}]
            """;
        Assert.Equal(["Roboto", "Roboto Slab", "Noto Roboto"], FreeFonts.Parse(json, "roboto").Select(f => f.Family));
        Assert.Equal(["Roboto Slab"], FreeFonts.Parse(json, "", "serif").Select(f => f.Family));
        var slab = FreeFonts.Parse(json, "slab")[0];
        Assert.Equal("https://cdn.jsdelivr.net/fontsource/fonts/roboto-slab@latest/latin-300-normal.ttf", FreeFonts.FileUrl(slab, 400));
        Assert.Equal("Mit Serifen", slab.Kategorie); Assert.False(FreeFonts.OpenLicense("Commercial"));
    }
}
