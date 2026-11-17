using System.Text.Json.Nodes;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0715 (B6 „Programm-Hülle für alle“, Etappe E3 Kreativ/Medien): alte Dateien von „Live streamen“ bleiben lesbar und bekommen ein Versionsfeld,
/// neuere Fassungen werden nicht überschrieben; Ergebnisse von „Medien bearbeiten“ entstehen atomar (echtes FFmpeg); harte Abbrüche beim sofortigen Speichern
/// des Sendeablaufs und bei der Notsicherung der Medien-Werkbank verlieren höchstens den laufenden Schritt.</summary>
public class T0715_HuelleE3Tests(ITestOutputHelper output)
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0715-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "T-0715 Sendeablauf: alte ablauf.json ohne Version bleibt lesbar (Fassung 1), Speichern schreibt die Version; eine neuere Fassung wird nicht überschrieben")]
    public void Rundown_version()
    {
        var dir = Tmp();
        try
        {
            var f = Path.Combine(Fach.StoreDir(dir, "sendung"), "ablauf.json");
            // so schrieb Trendo vor T-0715 (Fach.Save, ohne Version)
            File.WriteAllText(f, "{\n  \"Show\": \"Abendshow\",\n  \"Start\": \"2026-10-21T20:00:00\",\n  \"SlotMinutes\": 30,\n  \"Segments\": [ { \"Title\": \"Begrüßung\", \"Seconds\": 90, \"Note\": \"Kamera 1\" } ]\n}");
            var b = new BroadcastDesk(dir);
            Assert.Equal(1, b.Plan.Version); Assert.False(b.Newer);
            Assert.Equal("Abendshow", b.Plan.Show); Assert.Equal("Begrüßung", Assert.Single(b.Plan.Segments).Title);
            Assert.Equal(new DateTime(2026, 10, 21, 20, 1, 30), b.Rows()[0].End);
            b.Add("Interview", 300); // speichert
            Assert.Contains("\"Version\": 1", File.ReadAllText(f));
            Assert.Equal(2, new BroadcastDesk(dir).Plan.Segments.Count);

            var newer = File.ReadAllText(f).Replace("\"Version\": 1", "\"Version\": 2");
            File.WriteAllText(f, newer);
            var c = new BroadcastDesk(dir);
            Assert.True(c.Newer);
            c.Add("Abspann", 60); // im Speicher ja, Datei bleibt unverändert
            Assert.Equal(newer, File.ReadAllText(f));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "T-0715 Studio: alte szenen.json ohne Version bleibt lesbar, Speichern atomar mit Version (keine feste .neu-Datei mehr); neuere Fassung bleibt unverändert")]
    public void Scenes_version()
    {
        var dir = Tmp();
        try
        {
            var f = Path.Combine(dir, "szenen.json");
            File.WriteAllText(f, "{\"Scenes\":[{\"Id\":\"kamera\",\"Name\":\"Kamera\",\"Main\":2,\"Overlays\":[{\"Id\":\"t1\",\"Kind\":\"text\",\"Value\":\"Hallo\",\"X\":0.05,\"Y\":0.78,\"Width\":0.9,\"Visible\":true}]}],\"Active\":\"kamera\",\"Mix\":{\"MicDb\":-3,\"MicMute\":false,\"MusicFile\":null,\"MusicDb\":-14,\"MusicMute\":false},\"Plan\":[]}");
            var s = LiveShow.Load(dir);
            Assert.Equal(1, s.Version); Assert.False(s.Newer);
            Assert.Equal("Hallo", Assert.Single(Assert.Single(s.Scenes).Overlays).Value); Assert.Equal(-3, s.Mix.MicDb);
            s.Add("Interview", VideoKind.Camera);
            Assert.True(s.Save(dir));
            Assert.Contains("\"Version\": 1", File.ReadAllText(f));
            Assert.Equal(2, LiveShow.Load(dir).Scenes.Count);
            Assert.Empty(Directory.GetFiles(dir, "*.neu")); Assert.Empty(Directory.GetFiles(dir, "*.tmp"));

            var newer = File.ReadAllText(f).Replace("\"Version\": 1", "\"Version\": 3");
            File.WriteAllText(f, newer);
            var n = LiveShow.Load(dir);
            Assert.True(n.Newer);
            Assert.False(n.Save(dir));
            Assert.Equal(newer, File.ReadAllText(f));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "T-0715 Medien bearbeiten: Ergebnisse entstehen atomar mit dem echten FFmpeg – fertige Datei unter dem Namen, keine Zwischendatei; ein Fehler hinterlässt nichts")]
    public void Media_outputs_atomic()
    {
        var ff = Cli.Find("ffmpeg");
        if (ff is null) Ueberspringen.Weil("BLOCKIERT – FFmpeg fehlt auf diesem PC.");
        var dir = Tmp();
        try
        {
            var src = Path.Combine(dir, "clip.mp4");
            Assert.True(Cli.Run(ff, ["-loglevel", "error", "-f", "lavfi", "-i", "testsrc=duration=3:size=320x240", "-f", "lavfi", "-i", "sine=duration=3", "-c:v", "libx264", "-preset", "ultrafast", "-c:a", "aac", "-shortest", src], dir).Ok);
            var (r, o) = MediaStudio.Cut(ff, src, 0.5, 2);
            Assert.True(r.Ok, r.Err);
            Assert.Equal(Path.Combine(dir, "clip-trim.mp4"), o);
            Assert.True(new FileInfo(o).Length > 1000);
            var d = MediaStudio.Duration(ff, o); Assert.NotNull(d); Assert.InRange(d!.Value, 1.2, 1.8);
            var (r2, o2) = MediaStudio.Concat(ff, [src, o], dir);
            Assert.True(r2.Ok, r2.Err); Assert.InRange(MediaStudio.Duration(ff, o2)!.Value, 4.2, 4.9);
            Assert.Empty(Directory.GetFiles(dir, "*.teil*"));
            // Fehler (Eingabe ohne Tonspur → mp3 geht nicht): nichts unter dem Ergebnisnamen, keine Zwischendatei
            var silent = Path.Combine(dir, "stumm.mp4");
            Assert.True(Cli.Run(ff, ["-loglevel", "error", "-f", "lavfi", "-i", "testsrc=duration=1:size=160x120", "-c:v", "libx264", "-preset", "ultrafast", silent], dir).Ok);
            var (r3, o3) = MediaStudio.Run(ff, silent, "mp3");
            Assert.False(r3.Ok);
            Assert.False(File.Exists(o3));
            Assert.Empty(Directory.GetFiles(dir, "*.teil*"));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "T-0715 Absturz: Live streamen speichert Ablauf und Szenen sofort – 12-mal hart beendet, beide Dateien jedes Mal lesbar, höchstens der laufende Schritt fehlt")]
    public void Kill_while_saving_broadcast()
    {
        var dir = Tmp(); var rnd = new Random(715); int during = 0, worst = 0;
        try
        {
            for (var round = 1; round <= 3 * 12 && (round <= 12 || during < 6); round++) // unter Last weiter abbrechen, bis genug mitten im Schreiben lagen (wie T0708)
            {
                if (ProgramCrashTests.RunAndKill("sendung", dir, 2, rnd)) during++;
                var (started, done, _) = ProgramCrashTests.ReadLog(dir);
                var desk = new BroadcastDesk(dir);
                Assert.StartsWith("Stand ", desk.Plan.Show); // lesbar, nicht auf „Meine Sendung“ zurückgefallen
                Assert.Equal(4000, desk.Plan.Segments.Count);
                var show = LiveShow.Load(Fach.StoreDir(dir, "sendung"));
                Assert.Equal(12, show.Scenes.Count);
                var n = int.Parse(desk.Plan.Show[6..], System.Globalization.CultureInfo.InvariantCulture);
                var ns = int.Parse(show.Scenes[0].Name[6..], System.Globalization.CultureInfo.InvariantCulture);
                foreach (var x in new[] { n, ns })
                {
                    Assert.True(x >= done, $"Runde {round}: Stand {x}, fertig gespeichert war {done}");
                    Assert.True(x <= started, $"Runde {round}: Stand {x} > begonnen {started}");
                    worst = Math.Max(worst, started - x);
                }
            }
            Assert.True(worst <= 1, "Mehr als der eine laufende Schritt ging verloren: " + worst);
            Assert.True(during >= 6, $"Nur {during} von 12 Abbrüchen fielen in einen laufenden Schreibvorgang – der Test taugt so nicht.");
            output.WriteLine($"sendung: 12 harte Abbrüche, {during} mitten im Schreiben, höchstens {worst} Schritt verloren.");
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    [Fact(DisplayName = "T-0715 Absturz: Medien bearbeiten an der Notsicherung der Hülle (ProgramLink mit Sperre) – 12-mal hart beendet, Sicherung jedes Mal lesbar und trotz liegengebliebener Sperre zurückzuholen")]
    public void Kill_while_autosaving_media_link()
    {
        var dir = Tmp(); var rnd = new Random(7150); int during = 0, worst = 0;
        try
        {
            for (var round = 1; round <= 3 * 12 && (round <= 12 || during < 6); round++) // unter Last weiter abbrechen, bis genug mitten im Schreiben lagen (wie T0708)
            {
                if (ProgramCrashTests.RunAndKill("link-medien", dir, 2, rnd)) during++;
                var (started, done, _) = ProgramCrashTests.ReadLog(dir);
                var bak = Path.Combine(dir, "sicherung");
                Assert.Empty(ProgramDocument.OrphanScan(bak, "LINK-MEDIEN").Broken); // nie eine halbe Sicherung
                using var locks = new ProgramLocks(Path.Combine(dir, "sperren"));
                var link = new ProgramLink("LINK-MEDIEN", "medien") { Locks = locks };
                var pending = link.Pending(bak);
                Assert.True(pending.Count == 1, $"Runde {round}: {pending.Count} lesbare Sicherungen statt einer");
                var st = link.Restore(pending[0].File, bak)!;
                var n = (int)ProgramDocument.ToNumber(st["anfang"]);
                Assert.Equal(80000, st["clips"]!.AsArray().Count);
                Assert.True(n >= done, $"Runde {round}: Sicherung hat Stand {n}, fertig gesichert war {done}");
                Assert.True(n <= started, $"Runde {round}: Stand {n} > begonnen {started}");
                worst = Math.Max(worst, started - n);
            }
            Assert.True(worst <= 1, "Mehr als der eine laufende Schritt ging verloren: " + worst);
            Assert.True(during >= 6, $"Nur {during} von 12 Abbrüchen fielen in einen laufenden Schreibvorgang – der Test taugt so nicht.");
            output.WriteLine($"link-medien: 12 harte Abbrüche, {during} mitten im Schreiben, höchstens {worst} Schritt verloren.");
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    [Fact(DisplayName = "T-0715 Atomar: Exporte von Live streamen, Spuren-Mischer/MIDI/Karaoke, Bildwerkzeuge, freie Assets und Medien-Ergebnisse ohne direktes File.Write*/File.Create")]
    public void Creative_stores_atomic()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory); while (root is not null && !File.Exists(Path.Combine(root.FullName, "Trendo.sln"))) root = root.Parent;
        Assert.NotNull(root);
        string Src(string rel) => File.ReadAllText(Path.Combine(root!.FullName, "src", rel));
        foreach (var (file, bad) in new[]
        {
            ("Trendo.App/MainWindow.Spec15.cs", "File.WriteAllText("), ("Trendo.App/MainWindow.MusikStemsPlus.cs", "File.WriteAllTextAsync("), ("Trendo.App/MainWindow.MusikStemsPlus.cs", "File.WriteAllBytesAsync("),
            ("Trendo.App/MainWindow.MusikStemsPlus.cs", "AudioFile.WriteWav(a, f"), ("Trendo.App/ImageTools.cs", "File.Create("), ("Trendo.App/MainWindow.W4Media.cs", "File.WriteAllBytes("),
            ("Trendo.Core/MediaStudio.cs", "File.WriteAllText("), ("Trendo.Core/W6/LiveShow.cs", "File.WriteAllText("), ("Trendo.Core/Assets.cs", "File.WriteAllText("),
        })
            Assert.False(System.Text.RegularExpressions.Regex.IsMatch(Src(file), @"(?<![A-Za-z.])" + System.Text.RegularExpressions.Regex.Escape(bad)), $"{file} schreibt noch direkt: {bad}"); // „AtomicFile.WriteAllText(“ zählt nicht
        Assert.Contains("RunInto(", Src("Trendo.Core/MediaStudio.cs"));
        Assert.Contains(".teil", Src("Trendo.App/MainWindow.W4Assets.cs"));
    }
}
