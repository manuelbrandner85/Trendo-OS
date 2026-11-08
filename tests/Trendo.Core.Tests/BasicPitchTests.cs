using Trendo.Core;
using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Alle Studios, Runde 1 – basic-pitch (Spotify, Apache-2.0) als KI-Laufzeit „Noten“: Einrichtung ohne TensorFlow, Auswertung der Notenliste.
/// Die JSON-Beispiele stammen aus dem echten Lauf vom 08.11.2026 (C-Dur-Akkord, dann A4–H4–C5).</summary>
public class BasicPitchTests
{
    const string Real = "{\"werkzeug\":\"basic-pitch 0.4.0\",\"noten\":[{\"tonhoehe\":60,\"start\":0.01,\"ende\":1.94,\"staerke\":0.55},{\"tonhoehe\":64,\"start\":0.01,\"ende\":1.96,\"staerke\":0.5},{\"tonhoehe\":67,\"start\":0.01,\"ende\":1.95,\"staerke\":0.52},{\"tonhoehe\":69,\"start\":1.99,\"ende\":2.47,\"staerke\":0.6},{\"tonhoehe\":71,\"start\":2.49,\"ende\":2.97,\"staerke\":0.6},{\"tonhoehe\":72,\"start\":2.99,\"ende\":3.47,\"staerke\":0.6}]}";

    [Fact(DisplayName = "basic-pitch +1: echte Ausgabe → 6 Noten, Akkord C-E-G gleichzeitig, danach A4 H4 C5")]
    public void Parse_real()
    {
        var r = AudioToMidi.FromBasicPitch(Real);
        Assert.Equal([60, 64, 67, 69, 71, 72], r.Notes.Select(n => n.Pitch));
        Assert.Equal(0.01, r.Notes[0].Start, 3); Assert.Equal(1.93, r.Notes[0].Length, 3);
        Assert.Equal(0.5, r.PolyphonicShare, 3); // 3 von 6 Noten klingen zusammen
        Assert.False(r.Exact); Assert.Contains("Mehrstimmig erkannt (basic-pitch)", r.Verdict);
    }

    [Fact(DisplayName = "basic-pitch +2: Noten werden zum Piano-Roll-Lied und zurück zu MIDI (Akkord bleibt Akkord)")]
    public void To_song_and_midi()
    {
        var song = AudioToMidi.ToSong(AudioToMidi.FromBasicPitch(Real), 120, "Test");
        var chord = song.Notes.Where(n => n.Start == song.Notes[0].Start).Select(n => n.Pitch).OrderBy(p => p);
        Assert.Equal([60, 64, 67], chord);
        var back = PianoRoll.FromMidi(PianoRoll.ToMidi(song)); Assert.True(back.Ok); Assert.Equal(6, back.Song!.Notes.Count);
    }

    [Fact(DisplayName = "basic-pitch +3: einstimmige Folge → 0 % mehrstimmig, Bericht listet Notennamen")]
    public void Monophonic()
    {
        var r = AudioToMidi.FromBasicPitch("{\"noten\":[{\"tonhoehe\":69,\"start\":0,\"ende\":0.5,\"staerke\":0.9},{\"tonhoehe\":71,\"start\":0.5,\"ende\":1,\"staerke\":0.9}]}");
        Assert.Equal(0, r.PolyphonicShare); Assert.StartsWith("Einstimmig erkannt", r.Verdict);
        var rep = AudioToMidi.Report("Probe", r); Assert.Contains("| A4 |", rep); Assert.Contains("| B4 |", rep.Replace("H4", "B4"));
    }

    [Fact(DisplayName = "basic-pitch −1: unsinnige Einträge (Tonhöhe, Zeit) werden verworfen, leere Liste → keine Noten")]
    public void Refuse_bad_notes()
    {
        var r = AudioToMidi.FromBasicPitch("{\"noten\":[{\"tonhoehe\":200,\"start\":0,\"ende\":1},{\"tonhoehe\":60,\"start\":1,\"ende\":0.5},{\"tonhoehe\":60,\"start\":-1,\"ende\":0.5},{\"tonhoehe\":62,\"start\":0,\"ende\":1}]}");
        Assert.Single(r.Notes); Assert.Equal(0.5, r.Notes[0].Confidence);
        Assert.Equal("Keine Noten erkannt.", AudioToMidi.FromBasicPitch("{\"noten\":[]}").Verdict);
    }

    [Fact(DisplayName = "basic-pitch −2: Antwort ohne Notenliste oder kaputtes JSON wird abgelehnt")]
    public void Refuse_bad_json()
    {
        Assert.Throws<FormatException>(() => AudioToMidi.FromBasicPitch("{\"fehler\":\"x\"}"));
        Assert.ThrowsAny<System.Text.Json.JsonException>(() => AudioToMidi.FromBasicPitch("Traceback (most recent call last)"));
    }

    [Fact(DisplayName = "basic-pitch +4: Einrichtung – Pakete fest, basic-pitch ohne Abhängigkeiten (kein TensorFlow), Skript trendo_noten.py")]
    public void Setup_steps()
    {
        var app = KiPyApps.For(KiRuntime.Noten)!;
        Assert.True(app.PackagesOnly); Assert.Empty(app.Torch); Assert.Equal("trendo_noten.py", app.Script);
        Assert.DoesNotContain(app.Packages, p => p.Contains("tensorflow", StringComparison.OrdinalIgnoreCase));
        var steps = KiPyApps.Steps(app, new KiPyApps.Paths("uv.exe", "git.exe", "dir", "uvhome"), nvidia: false);
        Assert.DoesNotContain(steps, s => s.Args.Contains("clone"));
        var last = steps[^1]; Assert.Contains("--no-deps", last.Args); Assert.Contains("basic-pitch==0.4.0", last.Args);
        Assert.DoesNotContain("--no-deps", steps[^2].Args); Assert.Contains("onnxruntime==1.31.1", steps[^2].Args);
    }

    [Fact(DisplayName = "basic-pitch +5: in der KI-Bibliothek als Musik-Laufzeit mit Lizenz Apache-2.0")]
    public void Library_entry()
    {
        var info = KiLibrary.RuntimeInfo(KiRuntime.Noten);
        Assert.Equal("Apache-2.0", info.License); Assert.Equal("spotify/basic-pitch", info.Repo);
        var m = Assert.Single(KiLibrary.Models, x => x.Runtime == KiRuntime.Noten); Assert.Equal(KiKind.Musik, m.Kind);
    }

    [Fact(DisplayName = "basic-pitch +6: andere Laufzeiten bekommen keinen --no-deps-Schritt")]
    public void Others_unchanged()
    {
        foreach (var a in KiPyApps.All.Where(a => a.Id != KiRuntime.Noten))
            Assert.DoesNotContain(KiPyApps.Steps(a, new KiPyApps.Paths("uv", "git", "d", "h"), false), s => s.Args.Contains("--no-deps"));
    }
}
