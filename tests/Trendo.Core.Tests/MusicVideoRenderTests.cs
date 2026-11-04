using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>KI-Musikvideo C/G: Aufrufe der Video-KIs und Regeln fürs Verbessern nach einer fehlgeschlagenen Prüfung.</summary>
public class MusicVideoRenderTests
{
    static MusicScript.Shot Shot(bool sing = true, string who = "Sängerin", string size = "Halbnah", string move = "Kreisfahrt") =>
        new(0, 10, 13.5, "Refrain", size, move, "golden hour", who, sing, "Wir zwei", "Strand", "p", 5);

    [Fact(DisplayName = "Wan braucht 4·k+1 Bilder; Szene mit Figurenbild als Startbild, singende Figur mit Gesangsspur, 16 Bilder/s; Ausschluss immer dabei")]
    public void Args()
    {
        Assert.Equal(81, MusicVideoRender.Frames(5, 16));
        Assert.Equal(121, MusicVideoRender.Frames(5, 24));
        Assert.Equal(1, MusicVideoRender.Frames(3.5, 24) % 4);
        var a = MusicVideoRender.SceneArgs(new("d", "v", "t"), Shot(), "prompt", "fig.png", "o.webm", MusicVideoRender.Final, 9);
        Assert.Equal("fig.png", a[a.IndexOf("-i") + 1]); Assert.Equal("1280", a[a.IndexOf("-W") + 1]); Assert.Equal(MusicScript.Negative, a[a.IndexOf("-n") + 1]);
        Assert.Equal("85", a[a.IndexOf("--video-frames") + 1]); // 3,5 s × 24
        Assert.DoesNotContain("-i", MusicVideoRender.SceneArgs(new("d", "v", "t"), Shot(false, ""), "p", null, "o", MusicVideoRender.Preview, 1));
        var s = MusicVideoRender.SingArgs(new("d", "v", "t", "w"), Shot(), "p", "fig.png", "teil.wav", "o.webm", MusicVideoRender.SingFinal, 3);
        Assert.Equal("teil.wav", s[s.IndexOf("--audio") + 1]); Assert.Equal("w", s[s.IndexOf("--audio-encoder") + 1]); Assert.Equal("16", s[s.IndexOf("--fps") + 1]);
        var v = MusicVideoRender.VocalClipArgs("gesang.wav", Shot(), "teil.wav");
        Assert.Equal("10", v[v.ToList().IndexOf("-ss") + 1]); Assert.Equal("3.5", v[v.ToList().IndexOf("-t") + 1]); Assert.Contains("16000", v);
    }

    [Fact(DisplayName = "Nach „3 Arme“: neuer Zufall, ruhige Kamera, näher ran, ausdrücklich zwei Arme je Person; Bericht nennt den Fehler; Erwartung an Personen je Einstellung")]
    public void Retry_and_report()
    {
        var bad = MusicVideoRender.ParseCheck("""{"ok":false,"issues":[{"code":"haende","text":"Zu viele Hände","at":1.2}]}""");
        Assert.False(bad.Ok); Assert.Equal(1.2, bad.Issues[0].At);
        var (s, p) = MusicVideoRender.Retry(Shot(), "base", bad, 1);
        Assert.Equal("statisch", s.Move); Assert.Equal("Nah", s.Size); Assert.NotEqual(5, s.Seed);
        Assert.Contains("two arms and two hands per person", p);
        var (_, p2) = MusicVideoRender.Retry(Shot(), "base", MusicVideoRender.ParseCheck("""{"ok":false,"issues":[{"code":"lippen","text":"x"}]}"""), 1);
        Assert.Contains("mouth clearly", p2);
        var good = MusicVideoRender.ParseCheck("""{"ok":true,"issues":[]}""");
        Assert.Equal("✓ geprüft, in Ordnung", MusicVideoRender.Report([good]));
        Assert.Contains("nach 2 Versuchen (vorher: Zu viele Hände)", MusicVideoRender.Report([bad, good]));
        Assert.StartsWith("✗ nach 3 Versuchen", MusicVideoRender.Report([bad, bad, bad]));
        MusicScript.Figure[] figs = [new("fig0", "Sängerin", "w", "x"), new("fig1", "Sänger", "m", "y")];
        Assert.Equal(2, MusicVideoRender.ExpectedPeople(Shot(who: "beide"), figs));
        Assert.Equal(1, MusicVideoRender.ExpectedPeople(Shot(), figs));
        Assert.Equal(0, MusicVideoRender.ExpectedPeople(Shot(false, "Strand"), figs));
        Assert.Null(MusicVideoRender.ParseVision("OK"));
        Assert.Null(MusicVideoRender.ParseVision(""));
        Assert.Equal("Bild-KI sieht einen Fehler: sechs Finger an der linken Hand", MusicVideoRender.ParseVision("ERROR: sechs Finger an der linken Hand")!.Text);
        Assert.Contains("Expected number of people: 2", MusicVideoRender.VisionQuestion(2));
        Assert.Contains("fictional", MusicVideoRender.FigurePrompt(new("f", "Sängerin", "w", "a fictional woman"), "pop"));
    }
}
