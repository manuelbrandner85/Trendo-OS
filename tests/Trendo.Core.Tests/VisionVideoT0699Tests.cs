using Trendo.Core.Kreativ;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0699: „Bilder erkennen“ (Schlagworte, Suche, Seitendatei, CSV) und „Videos mit KI“ (Sprecher, Musik, Untertitel, KI-Clip je Szene).</summary>
public class VisionVideoT0699Tests
{
    static VisionTags.Recognition Rec(string file, string[] tags, string desc = "", string text = "", string sha = "aa", params ObjectDetect.Hit[] hits) =>
        VisionTags.Build(file, sha, 768, 576, new ObjectDetect.Result(768, 576, 10, hits), desc, tags, text, ["YOLOX-S"], new DateTime(2026, 11, 16, 10, 0, 0, DateTimeKind.Utc));

    [Fact(DisplayName = "T-0699 Erkennen 1: Modellantwort → Beschreibung und Schlagworte; ohne Schlagwort-Zeile keine erfundenen Schlagworte")]
    public void Parse_description()
    {
        var (d, t) = VisionTags.ParseDescription("Ein brauner Hund spielt auf einer Wiese mit einem Ball.\n\n**Schlagworte:** Hund, wiese, Ball, „Sommer“, Hund");
        Assert.Equal("Ein brauner Hund spielt auf einer Wiese mit einem Ball.", d);
        Assert.Equal(["Hund", "Wiese", "Ball", "Sommer"], t);
        var (d2, t2) = VisionTags.ParseDescription("Nur ein Satz ohne Liste.");
        Assert.Equal("Nur ein Satz ohne Liste.", d2); Assert.Empty(t2);
        Assert.Equal(("", (IReadOnlyList<string>)[]), VisionTags.ParseDescription(null));
    }

    [Fact(DisplayName = "T-0699 Erkennen 1b: echte Antwort von Claude über das Abo (Beleg reports/T-0699, 16.11.2026) wird zu Beschreibung und Schlagworten; Suche findet „Hund“ und „Fahrräder“")]
    public void Parse_real_claude_answer()
    {
        const string real = "Ein langhaariger, schwarz-brauner Hund sitzt auf einer hellen Holzveranda vor einem roten Rennrad, das an einer gelben Brüstung lehnt. Dahinter liegen eine sonnige Wohnstraße mit grünen Bäumen und ein geparkter weißer Pick-up, und die Stimmung wirkt ruhig, freundlich und sommerlich.\n\nSchlagworte: Hund, Rennrad, Veranda, Fahrrad, Straße, Baum, Pick-up, Sommer, Wohngegend, draußen";
        var (d, t) = VisionTags.ParseDescription(real);
        Assert.StartsWith("Ein langhaariger, schwarz-brauner Hund", d); Assert.DoesNotContain("Schlagworte", d);
        Assert.Equal(10, t.Count); Assert.Equal("Draußen", t[^1]);
        var r = VisionTags.Build(@"C:\f\dog.jpg", "s", 768, 576, null, d, t, "", ["Claude (dein Abo)"], DateTime.UtcNow);
        Assert.True(VisionTags.Matches(r, "zeig alle Fotos mit Hund")); Assert.True(VisionTags.Matches(r, "Fahrräder")); Assert.False(VisionTags.Matches(r, "Katze"));
    }

    [Fact(DisplayName = "T-0699 Erkennen 2: Suche in Alltagssprache – „zeig alle Fotos mit Hund“, Mehrzahl, Text im Bild; „Hund“ findet nicht „Hundert“")]
    public void Search()
    {
        var dog = Rec(@"C:\f\a.jpg", [], hits: new ObjectDetect.Hit("dog", "Hund", 0.9, 1, 2, 3, 4));
        var cat = Rec(@"C:\f\b.jpg", ["Katze", "Sofa"], "Eine Katze schläft.");
        var bill = Rec(@"C:\f\c.png", [], "", "Rechnung über hundert Euro");
        Assert.Equal(["Hund"], dog.Tags);
        Assert.Equal(["hund"], VisionTags.Terms("Zeig mir alle Fotos mit Hund!"));
        Assert.Equal([dog], VisionTags.Search([dog, cat, bill], "zeig alle Fotos mit Hund"));
        Assert.Equal([dog], VisionTags.Search([dog, cat, bill], "Hunde"));
        Assert.Equal([cat], VisionTags.Search([dog, cat, bill], "Katzen auf dem Sofa"));
        Assert.Equal([bill], VisionTags.Search([dog, cat, bill], "Bilder mit Rechnung"));
        Assert.Contains("Text", bill.Tags);
        Assert.Equal(3, VisionTags.Search([dog, cat, bill], "  ").Count);
        Assert.Empty(VisionTags.Search([dog, cat, bill], "Elefant"));
    }

    [Fact(DisplayName = "T-0699 Erkennen 3: Seitendatei (XMP) mit Schlagworten – Foto unverändert, eigene Datei wieder lesbar, fremde Seitendatei wird nie überschrieben")]
    public void Sidecar()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-t0699-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            var img = Path.Combine(dir, "foto.jpg"); File.WriteAllBytes(img, [0xFF, 0xD8, 0xFF, 1, 2, 3]);
            var before = File.ReadAllBytes(img);
            var r = Rec(img, ["Strand", "Meer & Sonne"], "Ein Strand <mit> Wellen.", sha: "abc", hits: new ObjectDetect.Hit("person", "Person", 0.8, 5, 6, 70, 80));
            Assert.True(VisionTags.WriteSidecar(r));
            Assert.Equal(img + ".xmp", VisionTags.SidecarPath(img));
            var xmp = File.ReadAllText(img + ".xmp");
            Assert.Contains("<rdf:li>Meer &amp; Sonne</rdf:li>", xmp); Assert.Contains("Ein Strand &lt;mit&gt; Wellen.", xmp);
            System.Xml.Linq.XDocument.Parse(xmp); // gültiges XML
            Assert.Equal(before, File.ReadAllBytes(img));
            var back = VisionTags.ReadSidecar(img, "abc");
            Assert.NotNull(back); Assert.Equal(["Person", "Strand", "Meer & Sonne"], back!.Value.Tags);
            Assert.Null(VisionTags.ReadSidecar(img, "anderes-foto")); // Foto verändert: Seitendatei gilt nicht mehr
            var foreign = Path.Combine(dir, "fremd.jpg"); File.WriteAllBytes(foreign, [1]);
            File.WriteAllText(foreign + ".xmp", "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"/>");
            Assert.False(VisionTags.WriteSidecar(r with { File = foreign }));
            Assert.Equal("<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"/>", File.ReadAllText(foreign + ".xmp"));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "T-0699 Erkennen 4: Dokument hin und zurück, CSV mit Schlagworten (Formelschutz), Doppelte über die Prüfsumme")]
    public void Json_csv_duplicates()
    {
        var a = Rec(@"C:\f\a.jpg", ["Hund"], "=Hund", "Text", "x1", new ObjectDetect.Hit("dog", "Hund", 0.91, 1, 2, 3, 4), new ObjectDetect.Hit("dog", "Hund", 0.7, 5, 6, 7, 8));
        var back = VisionTags.FromJson(VisionTags.ToJson(a))!;
        Assert.Equal(a.Tags, back.Tags); Assert.Equal(a.Objects, back.Objects); Assert.Equal(a.Description, back.Description); Assert.Equal(a.CreatedUtc, back.CreatedUtc);
        Assert.Null(VisionTags.FromJson(new System.Text.Json.Nodes.JsonObject { ["file"] = "" }));
        var csv = VisionTags.Csv([a]).Split("\r\n");
        Assert.Equal("Datei;Ordner;Schlagworte;Beschreibung;Objekte;Text im Bild;Erkannt mit", csv[0]);
        Assert.Contains(";'=Hund;", csv[1]); Assert.Contains("2× Hund", csv[1]);
        var dup = VisionTags.Duplicates([a, a with { File = @"C:\f\kopie.jpg" }, Rec(@"C:\f\z.jpg", [], sha: "zz")]);
        Assert.Single(dup); Assert.Equal(2, dup[0].Count);
    }

    [Fact(DisplayName = "T-0699 Video 1: Szenenplan liest „Sprecher: …“ und „Text: …“ in beliebiger Reihenfolge; Hinweis, wenn der Sprechertext länger als die Szene ist")]
    public void Plan_speech()
    {
        var s = VideoAiKit.Plan("Leuchtturm im Morgenlicht, Zoom rein, 5 s, Text: Ein neuer Tag, Sprecher: Am Meer beginnt der Tag.\nMöwen, 3 s, Sprecher: Die Möwen sind wach, Text: Hallo");
        Assert.Equal(("Leuchtturm im Morgenlicht", "Ein neuer Tag", "Am Meer beginnt der Tag."), (s[0].Prompt, s[0].Caption, s[0].Speech));
        Assert.Equal(("Möwen", "Hallo", "Die Möwen sind wach"), (s[1].Prompt, s[1].Caption, s[1].Speech));
        var longText = string.Join(" ", Enumerable.Repeat("Wort", 20));
        Assert.Contains(VideoAiKit.Check([new("Wiese", 3, "ruhig", "", "x.png", longText)], true), c => c.StartsWith("⚠ Szene 1: Der Sprechertext braucht"));
        Assert.Equal("Bild mit Kamerafahrt", VideoAiKit.Kind(s[0] with { Image = "a.png" }));
        Assert.Equal("KI-Video (bewegt)", VideoAiKit.Kind(s[0] with { Image = "a.png", Clip = "c.mp4" }));
    }

    [Fact(DisplayName = "T-0699 Video 2: Untertitel (SRT) im Zeitfenster jeder Szene, lange Texte in Zeilen bis 42 Zeichen geteilt")]
    public void Srt()
    {
        var scenes = new List<VideoAiKit.Scene> { new("A", 4, "ruhig", "", "", "Kurzer Satz."), new("B", 4, "ruhig", ""), new("C", 4, "ruhig", "", "", "Ein deutlich längerer Sprechertext, der auf mehrere Zeilen verteilt werden muss, damit man ihn lesen kann.") };
        Assert.Equal([0, 3.5, 7], VideoAiKit.Starts([4, 4, 4]));
        var srt = VideoAiKit.Srt(scenes).Replace("\r", "");
        Assert.StartsWith("1\n00:00:00,000 --> 00:00:03,500\nKurzer Satz.\n\n2\n00:00:07,500 --> ", srt);
        Assert.All(srt.Split('\n').Where(l => l.Length > 0 && !l.Contains("-->") && !int.TryParse(l, out _)), l => Assert.True(l.Length <= 42, l));
        Assert.DoesNotContain("B", srt.Split('\n'));
        Assert.Equal("", VideoAiKit.Srt([new("A", 4, "ruhig", "")]));
    }

    [Fact(DisplayName = "T-0699 Video 3: FFmpeg-Aufruf mit KI-Clip, Sprecher, Musik und Untertitelspur – Ton beginnt je Szene, Musik leise darunter; ohne Ton weiter „-an“")]
    public void Ffmpeg_sound()
    {
        var clips = new List<VideoAiKit.Clip> { new("a.png", 4, "zoom-rein"), new("b.png", 4, "links", "cap.png", "clip.mp4") };
        var a = VideoAiKit.FfmpegArgs(clips, 640, 360, "o.mp4", new([new("v1.wav", 0.2, 3.3), new("v2.wav", 3.7, 3.8)], "musik.mp3"), "u.srt");
        var inputs = a.Select((x, i) => (x, i)).Where(p => p.x == "-i").Select(p => a[p.i + 1]).ToList();
        Assert.Equal(["a.png", "clip.mp4", "cap.png", "v1.wav", "v2.wav", "musik.mp3", "u.srt"], inputs);
        var f = a[a.ToList().IndexOf("-filter_complex") + 1];
        Assert.Contains("[1:v]scale=640:360:force_original_aspect_ratio=increase,crop=640:360,setsar=1,fps=30,tpad=stop_mode=clone:stop_duration=4,trim=end_frame=120", f);
        Assert.Contains("[0:v]scale=1280:720", f); Assert.Contains("zoompan", f);
        Assert.Contains("[3:a]aresample=48000,aformat=channel_layouts=stereo,atrim=0:3.3,adelay=200:all=1[vo0]", f);
        Assert.Contains("[4:a]", f); Assert.Contains("adelay=3700:all=1", f);
        Assert.Contains("[5:a]aresample=48000,aformat=channel_layouts=stereo,volume=0.18,atrim=0:7.5", f);
        Assert.EndsWith("[vo0][vo1][mu]amix=inputs=3:normalize=0:duration=longest,apad=whole_dur=7.5,atrim=0:7.5[aout]", f);
        Assert.Equal("-stream_loop", a[a.ToList().IndexOf("musik.mp3") - 3]);
        Assert.Equal("6:s", a[a.ToList().LastIndexOf("-map") + 1]);
        Assert.Contains("aac", a); Assert.Contains("mov_text", a); Assert.DoesNotContain("-an", a);
        var silent = VideoAiKit.FfmpegArgs([new("a.png", 4, "zoom-rein")], 640, 360, "o.mp4");
        Assert.Contains("-an", silent); Assert.DoesNotContain("[aout]", silent);
        var onlyMusic = VideoAiKit.FfmpegArgs([new("a.png", 4, "zoom-rein")], 640, 360, "o.mp4", new([], "m.wav"));
        Assert.Contains("volume=0.6", onlyMusic[onlyMusic.ToList().IndexOf("-filter_complex") + 1]); Assert.Contains("[mu]anull,apad", onlyMusic[onlyMusic.ToList().IndexOf("-filter_complex") + 1]);
    }

    [Fact(DisplayName = "T-0699 Video 5: KI-Clip (2 s) auf Szenenlänge – 4 s werden doppelt so langsam, 10 s höchstens 2,5-fach plus letztes Bild, genau Szenenlänge, ohne Ton")]
    public void Clip_fit()
    {
        var a = VideoAiKit.ClipFitArgs("roh.webm", 2, 4, 1280, 720, "c.mp4");
        Assert.StartsWith("setpts=2*PTS,scale=1280:720", a[a.ToList().IndexOf("-vf") + 1]); Assert.Equal("4", a[a.ToList().IndexOf("-t") + 1]); Assert.Contains("-an", a);
        Assert.StartsWith("setpts=2.5*PTS", VideoAiKit.ClipFitArgs("r", 2, 10, 640, 360, "c")[8]);
        Assert.StartsWith("setpts=1*PTS", VideoAiKit.ClipFitArgs("r", 2, 1, 640, 360, "c")[8]);
    }

    [Fact(DisplayName = "T-0699 Video 4: Vorschau der Kamerafahrt rechnet wie zoompan – Zoom rein wird enger, Schwenk wandert, ruhig bleibt ganz")]
    public void Motion_view()
    {
        Assert.Equal((0, 0, 1, 1), VideoAiKit.View("ruhig", 0.5));
        var z0 = VideoAiKit.View("zoom-rein", 0); var z1 = VideoAiKit.View("zoom-rein", 1);
        Assert.Equal(1, z0.W); Assert.Equal(0.8, z1.W); Assert.Equal(0.1, z1.X);
        Assert.Equal(0.8, VideoAiKit.View("zoom-raus", 0).W); Assert.Equal(1, VideoAiKit.View("zoom-raus", 1).W);
        Assert.True(VideoAiKit.View("rechts", 1).X > VideoAiKit.View("rechts", 0).X);
        Assert.True(VideoAiKit.View("links", 1).X < VideoAiKit.View("links", 0).X);
    }
}
