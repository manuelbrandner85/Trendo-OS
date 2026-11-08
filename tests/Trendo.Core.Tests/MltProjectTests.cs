using System.Globalization;
using System.Xml.Linq;
using Trendo.Core;
using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Alle Studios, Runde 1 – CORE-08: Trendo-Schnitt als MLT-XML. Echter Lauf mit melt (aus Kdenlive) nur mit TRENDO_MELT_EXE und TRENDO_FFMPEG_DIR,
/// sonst übersprungen. Beleg: reports/alle-studios/mlt-e2e.md.</summary>
public class MltProjectTests
{
    static System.Text.Json.Nodes.JsonObject Project(string a = "a.mp4", string img = "bild.png", string b = "b.mp4", string? music = "musik.wav")
    {
        var p = VideoEdit.NewProject(1280, 720); p["fps"] = 25;
        VideoEdit.Store(p, [new EditClip("1", a, 0.5, 2.0, 2.0), new EditClip("2", img, 0, 1, 1, false), new EditClip("3", b, 0, 3, 3, false)]);
        if (music is not null) VideoEdit.Store(p, [new EditClip("m", music, 0, 4, 4, true, 1.0, 0.5)], "music");
        return p;
    }
    static XElement X(System.Text.Json.Nodes.JsonObject p) => XDocument.Parse(MltProject.ToXml(p)).Root!;
    static string P(XElement e, string n) => e.Elements("property").First(x => (string?)x.Attribute("name") == n).Value;

    [Fact(DisplayName = "MLT +1: Profil mit Bildgröße und Bildrate, Hauptelement ist die Spurmischung")]
    public void Profile()
    {
        var r = X(Project()); var pr = r.Element("profile")!;
        Assert.Equal(("1280", "720", "25"), ((string)pr.Attribute("width")!, (string)pr.Attribute("height")!, (string)pr.Attribute("frame_rate_num")!));
        Assert.Equal("main", (string)r.Attribute("producer")!); Assert.Equal("C", (string)r.Attribute("LC_NUMERIC")!);
    }

    [Fact(DisplayName = "MLT +2: Bildspur – Schnittpunkte in Bildern (0,5–2,0 s bei 25 fps = 13–49), Bild 1 s = 25 Bilder, Reihenfolge bleibt")]
    public void Video_track()
    {
        var e = X(Project()).Elements("playlist").First(x => (string?)x.Attribute("id") == "bildspur").Elements("entry").ToList();
        Assert.Equal(["bild0", "bild1", "bild2"], e.Select(x => (string)x.Attribute("producer")!));
        Assert.Equal(("13", "49"), ((string)e[0].Attribute("in")!, (string)e[0].Attribute("out")!));
        Assert.Equal(("0", "24"), ((string)e[1].Attribute("in")!, (string)e[1].Attribute("out")!));
        Assert.Equal(("0", "74"), ((string)e[2].Attribute("in")!, (string)e[2].Attribute("out")!));
    }

    [Fact(DisplayName = "MLT +3: Bild bekommt eine feste Länge, Clip ohne Ton keinen Tonkanal")]
    public void Image_and_mute()
    {
        var prods = X(Project()).Elements("producer").ToList();
        Assert.Equal("25", P(prods.First(x => (string?)x.Attribute("id") == "bild1"), "length"));
        Assert.Equal("-1", P(prods.First(x => (string?)x.Attribute("id") == "bild2"), "audio_index"));
        Assert.DoesNotContain(prods.First(x => (string?)x.Attribute("id") == "bild0").Elements("property"), x => (string?)x.Attribute("name") == "audio_index");
    }

    [Fact(DisplayName = "MLT +4: Musikspur – Lücke bis zum Start, Lautstärke als Filter, nur Ton, Mischung mit der Bildspur")]
    public void Music_track()
    {
        var r = X(Project()); var pl = r.Elements("playlist").First(x => (string?)x.Attribute("id") == "musik");
        Assert.Equal("25", (string)pl.Element("blank")!.Attribute("length")!);
        var m = r.Elements("producer").First(x => (string?)x.Attribute("id") == "musik0");
        Assert.Equal("-1", P(m, "video_index")); Assert.Equal("0.5", P(m.Element("filter")!, "gain"));
        Assert.Equal("video", (string)r.Element("tractor")!.Element("multitrack")!.Elements("track").Last().Attribute("hide")!);
        Assert.Equal("mix", P(r.Element("tractor")!.Element("transition")!, "mlt_service"));
    }

    [Fact(DisplayName = "MLT +5: ohne Musik nur eine Spur und keine Mischung")]
    public void No_music()
    {
        var t = X(Project(music: null)).Element("tractor")!;
        Assert.Single(t.Element("multitrack")!.Elements("track")); Assert.Null(t.Element("transition"));
    }

    [Fact(DisplayName = "MLT +6: Dateinamen mit &, < und Umlauten bleiben gültiges XML")]
    public void Escaping()
    {
        var x = MltProject.ToXml(Project(a: @"C:\Videos\Müller & Söhne <Teil 1>.mp4"));
        Assert.Equal(@"C:\Videos\Müller & Söhne <Teil 1>.mp4", P(XDocument.Parse(x).Root!.Elements("producer").First(), "resource"));
    }

    [Fact(DisplayName = "MLT −1: leere Bildspur und unsinnige Bildgröße werden abgelehnt")]
    public void Refuse()
    {
        Assert.Throws<ArgumentException>(() => MltProject.ToXml(VideoEdit.NewProject()));
        var p = Project(); p["width"] = 100000; Assert.Throws<ArgumentException>(() => MltProject.ToXml(p));
    }

    [Fact(DisplayName = "MLT +7: Grenzen werden benannt – Überblendung und Text")]
    public void Limits()
    {
        var p = Project(); Assert.Empty(MltProject.Limits(p));
        VideoEdit.SetClip(p, 0, 0.5, "Hallo"); Assert.Equal(["Überblendungen", "eingeblendete Texte"], MltProject.Limits(p));
    }

    [Fact(DisplayName = "MLT +8: melt-Argumente – Datei, Ausgabe als eigene Argumente, H.264/AAC")]
    public void Render_args()
    {
        var a = MltProject.RenderArgs("p.mlt", "a b.mp4");
        Assert.Equal("p.mlt", a[0]); Assert.Contains("avformat:a b.mp4", a); Assert.Contains("vcodec=libx264", a); Assert.Contains("acodec=aac", a); Assert.Contains("terminate_on_pause=1", a);
    }

    // ---------- echter Lauf ----------
    public sealed class MeltFactAttribute : FactAttribute
    {
        public MeltFactAttribute() { if (Melt() is null || Ff("ffmpeg") is null) Skip = "Übersprungen – TRENDO_MELT_EXE / TRENDO_FFMPEG_DIR nicht gesetzt."; }
    }
    static string? Melt() => Environment.GetEnvironmentVariable("TRENDO_MELT_EXE") is { Length: > 0 } p && File.Exists(p) ? p : null;
    static string? Ff(string n) => Environment.GetEnvironmentVariable("TRENDO_FFMPEG_DIR") is { Length: > 0 } d && File.Exists(Path.Combine(d, n + ".exe")) ? Path.Combine(d, n + ".exe") : null;

    [MeltFact(DisplayName = "MLT echt: melt rendert den Trendo-Schnitt – 5,5 s, 1280×720, Bild und Ton, rote Stelle bei 3,5 s")]
    public void Real_render()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-mlt-echt-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            string F(string n) => Path.Combine(dir, n);
            void Ff1(params string[] a) { var r = Cli.Run(Ff("ffmpeg")!, ["-hide_banner", "-loglevel", "error", "-y", .. a], dir, 60_000); Assert.True(r.Ok, r.Err); }
            Ff1("-f", "lavfi", "-i", "testsrc=size=640x360:rate=25:duration=2", "-f", "lavfi", "-i", "sine=frequency=440:duration=2", "-c:v", "libx264", "-c:a", "aac", "-shortest", F("a.mp4"));
            Ff1("-f", "lavfi", "-i", "color=c=blue:size=640x360:duration=1", "-frames:v", "1", F("bild.png"));
            Ff1("-f", "lavfi", "-i", "color=c=red:size=640x360:rate=25:duration=3", "-c:v", "libx264", F("b.mp4"));
            Ff1("-f", "lavfi", "-i", "sine=frequency=220:duration=4", F("musik.wav"));
            var mlt = F("schnitt.mlt"); File.WriteAllText(mlt, MltProject.ToXml(Project(F("a.mp4"), F("bild.png"), F("b.mp4"), F("musik.wav"))));
            var outFile = F("ergebnis.mp4");
            var r = Cli.Run(Melt()!, MltProject.RenderArgs(mlt, outFile), dir, 300_000); Assert.True(r.Ok && File.Exists(outFile), r.Err + r.Out);
            var probe = Cli.Run(Ff("ffprobe")!, ["-v", "error", "-show_entries", "format=duration:stream=codec_type,width,height", "-of", "csv=p=0", outFile], dir, 60_000).Out;
            var dur = double.Parse(probe.Trim().Split('\n').Last().Trim(), CultureInfo.InvariantCulture);
            Assert.InRange(dur, 5.3, 5.7); Assert.Contains("video,1280,720", probe.Replace("\r", "")); Assert.Contains("audio", probe);
            Ff1("-ss", "3.5", "-i", outFile, "-frames:v", "1", "-vf", "scale=1:1", "-f", "rawvideo", "-pix_fmt", "rgb24", F("px.raw"));
            var px = File.ReadAllBytes(F("px.raw")); Assert.True(px[0] > 180 && px[1] < 60 && px[2] < 60, $"erwartet Rot, war {px[0]},{px[1]},{px[2]}");
            if (Environment.GetEnvironmentVariable("TRENDO_WERKZEUGE_BELEG") is { Length: > 0 } b) File.WriteAllText(Path.Combine(b, "mlt-probe.txt"), probe + "\n" + MltProject.ToXml(Project("a.mp4", "bild.png", "b.mp4", "musik.wav")));
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
