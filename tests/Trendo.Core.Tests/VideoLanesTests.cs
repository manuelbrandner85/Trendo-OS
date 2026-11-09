using System.Text.Json.Nodes;
using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Pilot 4.0: V2 Spuren, V3 Bildraster, V4 Werkzeuge.</summary>
public class VideoLanesTests
{
    static JsonObject P(string dir)
    {
        var p = VideoEdit.NewProject();
        string F(string n) { var f = Path.Combine(dir, n); File.WriteAllBytes(f, [1, 2]); return f; }
        VideoEdit.Store(p, [new EditClip("c1", F("a.mp4"), 2, 8, 20), new EditClip("c2", F("b.mp4"), 1, 5, 10)]);
        VideoEdit.Store(p, [new EditClip("m1", F("m.mp3"), 0, 4, 10, Start: 0, Volume: 0.5), new EditClip("m2", F("n.mp3"), 0, 3, 10, Start: 6, Volume: 1), new EditClip("m3", F("o.mp3"), 0, 2, 10, Start: 1, Volume: 1, Lane: 1)], "music");
        return p;
    }
    static T In<T>(Func<string, T> f) { var d = Directory.CreateTempSubdirectory("trendo-spuren").FullName; try { return f(d); } finally { Directory.Delete(d, true); } }
    static void In(Action<string> f) => In<int>(d => { f(d); return 0; });

    [Fact(DisplayName = "V2: Spuren hinzufügen; leere letzte Spur lässt sich entfernen, Spuren mit Inhalt nie")]
    public void Add_remove() => In(d =>
    {
        var p = P(d);
        Assert.Equal(2, VideoLanes.Audio(p));
        Assert.False(VideoLanes.RemoveLast(p, "audio")); // Lane 1 ist belegt
        Assert.Equal(3, VideoLanes.Add(p, "audio"));
        Assert.True(VideoLanes.RemoveLast(p, "audio"));
        Assert.Equal(2, VideoLanes.Audio(p));
        Assert.Equal(1, VideoLanes.Video(p));
        Assert.False(VideoLanes.RemoveLast(p, "video"));
    });

    [Fact(DisplayName = "V2: mehr als die Höchstzahl an Spuren wird verständlich abgelehnt")]
    public void Too_many() => In(d =>
    {
        var p = P(d);
        while (VideoLanes.Audio(p) < VideoLanes.MaxLanes) VideoLanes.Add(p, "audio");
        Assert.Contains("Spuren", Assert.Throws<InvalidOperationException>(() => VideoLanes.Add(p, "audio")).Message);
    });

    [Fact(DisplayName = "V2: Stumm nimmt eine Tonspur aus dem Export, Solo lässt nur diese Spur übrig")]
    public void Mute_solo() => In(d =>
    {
        var p = P(d);
        int Inputs() => VideoEdit.ExportArgs(p, Path.Combine(d, "x.mp4")).Count(a => a.EndsWith(".mp3", StringComparison.Ordinal));
        Assert.Equal(3, Inputs());
        VideoLanes.ToggleMute(p, "audio", 1); Assert.Equal(2, Inputs());
        VideoLanes.ToggleMute(p, "audio", 1); Assert.Equal(3, Inputs());
        VideoLanes.ToggleSolo(p, "audio", 1); Assert.Equal(1, Inputs());
        Assert.True(VideoLanes.IsOn(p, "audio", 1)); Assert.False(VideoLanes.IsOn(p, "audio", 0));
    });

    [Fact(DisplayName = "V2: Höhere Bildspuren liegen im Export oben, stumme Bildspur fehlt")]
    public void Overlay_order() => In(d =>
    {
        var p = P(d);
        string F(string n) { var f = Path.Combine(d, n); File.WriteAllBytes(f, [1]); return f; }
        var top = VideoPro.AddOverlay(p, F("oben.png"), 0, 2); var low = VideoPro.AddOverlay(p, F("unten.png"), 0, 2);
        VideoLanes.SetOverlayLane(p, top.Id, 1);
        var a = VideoEdit.ExportArgs(p, Path.Combine(d, "x.mp4"));
        Assert.True(a.ToList().FindIndex(x => x.EndsWith("unten.png", StringComparison.Ordinal)) < a.ToList().FindIndex(x => x.EndsWith("oben.png", StringComparison.Ordinal)));
        VideoLanes.ToggleMute(p, "video", 1);
        Assert.DoesNotContain(VideoEdit.ExportArgs(p, Path.Combine(d, "x.mp4")), x => x.EndsWith("oben.png", StringComparison.Ordinal));
        _ = low;
    });

    [Fact(DisplayName = "V3: Zeit rastet auf Einzelbilder, Zeitcode zählt Bilder bei 25 und 30 Bildern")]
    public void Timecode()
    {
        Assert.Equal(1.04, VideoTools.Quantize(1.05, 25), 4);
        Assert.Equal(0, VideoTools.Quantize(-3, 25));
        Assert.Equal("00:00:05:12", VideoTools.TimeCode(5.5, 24));
        Assert.Equal("01:01:01:15", VideoTools.TimeCode(3661.5, 30));
        Assert.Equal(30, VideoTools.Fps(new JsonObject()));
    }

    [Fact(DisplayName = "V4 Slip: Ausschnitt wandert, Länge bleibt, Grenzen der Quelle halten")]
    public void Slip() => In(d =>
    {
        var p = P(d);
        Assert.Equal(1.5, VideoTools.Slip(p, 0, 1.5), 3);
        var c = VideoEdit.Clips(p)[0]; Assert.Equal(3.5, c.In, 3); Assert.Equal(9.5, c.Out, 3);
        Assert.Equal(-3.5, VideoTools.Slip(p, 0, -99), 3);
        Assert.Equal(0, VideoEdit.Clips(p)[0].In, 3);
        Assert.Equal(6, VideoEdit.Clips(p)[0].Length, 3);
    });

    [Fact(DisplayName = "V4 Rolling: Schnitt verschiebt sich, Gesamtlänge bleibt, Mindestlänge und Quelle begrenzen")]
    public void Roll() => In(d =>
    {
        var p = P(d);
        var len = VideoEdit.VideoLength(VideoEdit.Clips(p));
        Assert.Equal(1, VideoTools.Roll(p, 0, 1), 3);
        var l = VideoEdit.Clips(p); Assert.Equal(9, l[0].Out, 3); Assert.Equal(2, l[1].In, 3);
        Assert.Equal(len, VideoEdit.VideoLength(l), 3);
        VideoTools.Roll(p, 0, 99);
        Assert.Equal(VideoEdit.MinLength, VideoEdit.Clips(p)[1].Length, 3);
        Assert.Equal(0, VideoTools.Roll(p, 1, 1));
    });

    [Fact(DisplayName = "V4 Ripple: Löschen rückt nur spätere Stücke derselben Tonspur auf")]
    public void Ripple() => In(d =>
    {
        var p = P(d);
        VideoTools.RippleDeleteMusic(p, 0);
        var l = VideoEdit.Clips(p, "music");
        Assert.Equal(2, l.Count);
        Assert.Equal(2, l[0].Start, 3); // m2 von 6 um 4 nach vorn
        Assert.Equal(1, l[1].Start, 3); // m3 liegt auf der anderen Spur
    });

    [Fact(DisplayName = "V4 Einrasten: nächster Punkt innerhalb der Toleranz, sonst unverändert")]
    public void Snap()
    {
        Assert.Equal(5, VideoTools.Snap(5.1, [2, 5, 9], 0.2));
        Assert.Equal(5.5, VideoTools.Snap(5.5, [2, 5, 9], 0.2));
    }
}
