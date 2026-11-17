using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Trendo.Core.Film;
using Trendo.Core.Media;
using Xunit;
namespace Trendo.Core.Tests;

/// <summary>T-0693: „Filme machen“ erzeugt einen Film – Material je Einstellung, Rohschnitt im Datenmodell des Videoschnitts, Untertitel, und der Nachweis mit echtem FFmpeg.</summary>
public class FilmCutTests
{
    const string Script = """
Titel: Drei Farben

INNEN. STUDIO – TAG

Eine Frau steht vor einer roten Wand.

- Totale: Rote Wand im Studio (2 s)
- Nah: ERIKA spricht in die Kamera (3 s)

ERIKA
Heute drehen wir einen Film.

AUSSEN. HOF – ABEND

- Totale: Blauer Himmel über dem Hof (2 s)

ERIKA
Und er ist schon fertig.
""";

    static JsonObject Film(string script = Script) => new() { ["logline"] = "Ein Film aus drei Farben.", ["genre"] = "Test", ["script"] = script, ["figures"] = new JsonArray(), ["places"] = new JsonArray() };

    [Fact(DisplayName = "Film: Material bleibt an seiner Einstellung, auch wenn darüber eine neue eingefügt oder die Reihenfolge getauscht wird; alte Dokumente ohne Material bleiben lesbar")]
    public void Material_stays_with_shot()
    {
        var f = Film();
        var shots = FilmCut.Shots(f);
        Assert.Equal(["SZ01-E01", "SZ01-E02", "SZ02-E01"], shots.Select(s => s.Id));
        Assert.All(shots, s => Assert.Null(s.Material)); // Dokument aus Teil 1 (ohne „material“)
        FilmCut.Assign(f, shots[1], "gruen.mp4", 10, true);
        var s2 = FilmCut.Shots(f)[1];
        Assert.Equal("gruen.mp4", s2.Material!.File); Assert.Equal(3, s2.Material.Out); Assert.Equal(3, s2.Seconds); Assert.Equal("video", s2.Kind);

        // eine Einstellung darüber einfügen: die Nummer wandert, das Material bleibt bei „ERIKA spricht …“
        f["script"] = ((string)f["script"]!).Replace("- Totale: Rote Wand", "- Detail: Eine Hand am Lichtschalter (1 s)\n- Totale: Rote Wand");
        var after = FilmCut.Shots(f);
        Assert.Equal(4, after.Count);
        Assert.Equal("SZ01-E03", after.Single(s => s.Material is not null).Id);
        Assert.Contains("ERIKA spricht", after.Single(s => s.Material is not null).Shot.Description);

        // tauschen: nur innerhalb der Szene
        Assert.True(FilmCut.Move(f, after[2], -1));
        var moved = FilmCut.Shots(f);
        Assert.Contains("ERIKA spricht", moved[1].Shot.Description); Assert.Equal("gruen.mp4", moved[1].Material!.File);
        Assert.False(FilmCut.Move(f, moved[2], 1)); // nächste Einstellung liegt in der anderen Szene

        // Ein- und Ausstieg, Dauer
        FilmCut.Trim(f, moved[1], 1.5, 99);
        Assert.Equal((1.5, 10.0), (FilmCut.Shots(f)[1].Material!.In, FilmCut.Shots(f)[1].Material!.Out));
        FilmCut.Assign(f, FilmCut.Shots(f)[0], "bild.png", 0, false);
        FilmCut.SetSeconds(f, FilmCut.Shots(f)[0], 6);
        Assert.Contains("(6 s)", (string)f["script"]!);
        Assert.Equal(6, FilmCut.Shots(f)[0].Seconds); Assert.Equal("bild", FilmCut.Shots(f)[0].Kind);
        Assert.Throws<InvalidOperationException>(() => FilmCut.Assign(f, FilmCut.Shots(f)[0], "ton.mp3", 5, true));
        FilmCut.Unassign(f, FilmCut.Shots(f)[0]);
        Assert.Equal("karte", FilmCut.Shots(f)[0].Kind);
    }

    [Fact(DisplayName = "Film: Rohschnitt-Plan – Titel, Tafel wo Material fehlt, Abspann aus den Figuren, Überblenden nur an Abschnittsgrenzen, Untertitel bei der richtigen Einstellung")]
    public void Plan_pieces_and_subtitles()
    {
        var f = Film(); var dir = Path.Combine(Path.GetTempPath(), "trendo-film-plan");
        var plan = FilmCut.Plan(f, "Unbenannt", dir);
        Assert.Equal(["titel", "shot", "shot", "shot", "abspann"], plan.Pieces.Select(p => p.Kind));
        Assert.All(plan.Pieces, p => Assert.NotNull(p.Card)); // ohne Material: überall Texttafeln
        Assert.Equal("Drei Farben", plan.Pieces[0].Card![0].Text);
        Assert.Contains(plan.Pieces[^1].Card!, l => l.Text == "Erika");
        // Überblenden: Titel→Szene, Szene 1→2, Szene→Abspann; in Szene 1 harter Schnitt
        Assert.Equal([0.5, 0, 0.5, 0.5, 0], plan.Pieces.Select(p => p.Fade));
        Assert.Equal(3 + 2 + 3 + 2 + 4 - 1.5, plan.Seconds, 3);
        // Untertitel: Rede 1 bei „ERIKA spricht …“ (zweite Einstellung), Rede 2 in Szene 2
        Assert.Equal(2, plan.Subtitles.Count);
        var starts = VideoEdit.Starts(FilmCut.Clips(plan));
        Assert.InRange(plan.Subtitles[0].Start, starts[2], starts[2] + 3); Assert.Contains("Heute drehen wir", plan.Subtitles[0].Text);
        Assert.InRange(plan.Subtitles[1].Start, starts[3], starts[3] + 2);
        Assert.Contains("00:00:0", FilmCut.Srt(plan)); Assert.Contains("-->", FilmCut.Srt(plan));

        // Zwischentitel je Akt und je Szene, ohne Titel und Abspann
        f["script"] = ((string)f["script"]!).Replace("INNEN. STUDIO", "# Erster Akt\n\nINNEN. STUDIO");
        Assert.Equal(["titel", "zwischentitel", "shot", "shot", "shot", "abspann"], FilmCut.Plan(f, "x", dir).Pieces.Select(p => p.Kind));
        FilmCutOptions.Set(f, "sceneTitles", FilmCutOptions.TitlesScenes); FilmCutOptions.Set(f, "title", false); FilmCutOptions.Set(f, "credits", false); FilmCutOptions.Set(f, "fade", 0);
        var bare = FilmCut.Plan(f, "x", dir);
        Assert.Equal(["zwischentitel", "shot", "shot", "zwischentitel", "shot"], bare.Pieces.Select(p => p.Kind));
        Assert.Equal(2 + 2 + 3 + 2 + 2, bare.Seconds, 3);

        // Projekt des Videoschnitts: Marken je Einstellung, Musik in Filmlänge
        FilmCutOptions.Set(f, "music", "musik.mp3"); FilmCutOptions.Set(f, "musicLength", 60);
        var p = FilmCut.ToProject(FilmCut.Plan(f, "x", dir), "film.srt");
        Assert.Equal(5, VideoEdit.Clips(p).Count); Assert.Equal(3, VideoEdit.Markers(p).Count(m => m.Kind == "shot"));
        Assert.Equal(11, VideoEdit.Clips(p, "music").Single().Length, 3); Assert.Equal("film.srt", (string)p["subs"]!);
    }

    [Fact(DisplayName = "Film: Clips ohne Drehbuch werden zu Einstellungen mit Material; die KI-Antwort wird nur als lesbares Drehbuch übernommen")]
    public void Clips_first_and_ki_answer()
    {
        var f = Film("");
        Assert.Equal(2, FilmCut.AddClipsAsShots(f, [("C:/x/Strand (1).mp4", 7.26, true), ("C:/x/foto.jpg", 0, false)]));
        var shots = FilmCut.Shots(f);
        Assert.Equal(2, shots.Count); Assert.Equal("video", shots[0].Kind); Assert.Equal(7.26, shots[0].Seconds, 3); Assert.Contains("(7,3 s)", (string)f["script"]!); Assert.Equal("bild", shots[1].Kind); Assert.Equal(4, shots[1].Seconds);

        Assert.Null(FilmWriter.Accept("Gern! Hier ist eine Idee ohne Szenen.").Script);
        Assert.Null(FilmWriter.Accept("").Script);
        var (script, msg) = FilmWriter.Accept("Hier ist dein Drehbuch:\n```fountain\nTitel: Der Regen\n\nINNEN. FLUR – NACHT\n\nEs tropft.\n\nMIA\nHörst du das?\n```\nViel Erfolg!");
        Assert.NotNull(script); Assert.DoesNotContain("```", script); Assert.DoesNotContain("Viel Erfolg", script);
        Assert.Contains("- Totale:", script); Assert.Contains("1 Szenen", msg); // fehlende Einstellungen werden ergänzt
        Assert.Contains("Idee: Regen", FilmWriter.Prompt("Regen", "Drama", "")); Assert.Contains("Supertotale", FilmWriter.Prompt("Regen", "", ""));
    }

    // ---------- Nachweis mit echtem FFmpeg ----------
    static string Probe(string ffprobe, string dir, params string[] args) { var r = Cli.Run(ffprobe, ["-v", "error", .. args], dir, 60000); Assert.True(r.Ok, r.Err); return r.Out.Trim(); }

    static (byte R, byte G, byte B) PixelAt(string ff, string dir, string film, double t)
    {
        var raw = Path.Combine(dir, $"pixel-{t.ToString("0.00", CultureInfo.InvariantCulture)}.rgb");
        var r = Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", "-ss", t.ToString("0.###", CultureInfo.InvariantCulture), "-i", film, "-frames:v", "1", "-vf", "scale=1:1", "-f", "rawvideo", "-pix_fmt", "rgb24", raw], dir, 60000);
        Assert.True(r.Ok, r.Err);
        var b = File.ReadAllBytes(raw); return (b[0], b[1], b[2]);
    }

    [Fact(DisplayName = "Film mit echtem FFmpeg: Beispiel-Drehbuch mit drei Einstellungen (Clips mit Ton aus FFmpeg) → MP4; ffprobe prüft Dauer, Bild- und Tonspur, Auflösung, Untertitel; Standbild aus der Mitte zeigt den erwarteten Clip")]
    public void Render_real()
    {
        var ff = Cli.Find("ffmpeg"); var probe = Cli.Find("ffprobe");
        if (ff is null || probe is null) { Ueberspringen.Weil("FFmpeg oder ffprobe fehlt auf diesem PC."); return; }
        var dir = Path.Combine(Path.GetTempPath(), "trendo-film-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        var log = new StringBuilder();
        void Log(string s) => log.AppendLine(s);
        try
        {
            string P(string n) => Path.Combine(dir, n);
            Cli.Result Run(IReadOnlyList<string> a) { var r = Cli.Run(ff, a, dir, 300000); Log("ffmpeg " + string.Join(' ', a.Select(x => x.Replace(dir, "<temp>"))) + " → " + r.Exit + (r.Err.Trim().Length > 0 ? " " + r.Err.Trim() : "")); return r; }
            // Testclips: Farbfläche mit Ton, länger als die geplante Einstellung
            foreach (var (name, color, hz) in new[] { ("rot", "0xFF0000", 440), ("gruen", "0x00FF00", 660), ("blau", "0x0000FF", 880) })
                Assert.True(Run(["-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i", $"color=c={color}:size=640x360:rate=25:duration=5", "-f", "lavfi", "-i", $"sine=frequency={hz}:duration=5", "-shortest", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", P(name + ".mp4")]).Ok);
            Assert.True(Run(["-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i", "sine=frequency=220:duration=30", P("musik.mp3")]).Ok);

            var f = Film();
            FilmCutOptions.Set(f, "width", 640); FilmCutOptions.Set(f, "height", 360);
            foreach (var (i, name) in new[] { (0, "rot"), (1, "gruen"), (2, "blau") })
                FilmCut.Assign(f, FilmCut.Shots(f)[i], P(name + ".mp4"), MediaStudio.Duration(ff, P(name + ".mp4"))!.Value, VideoEdit.HasAudioStream(ff, P(name + ".mp4")));
            FilmCut.Trim(f, FilmCut.Shots(f)[1], 1, 4); // Ein- und Ausstieg: 3 s aus der Mitte
            FilmCutOptions.Set(f, "music", P("musik.mp3")); FilmCutOptions.Set(f, "musicLength", MediaStudio.Duration(ff, P("musik.mp3"))!.Value); FilmCutOptions.Set(f, "musicVolume", 0.3);

            var plan = FilmCut.Plan(f, "Unbenannt", P("tafeln"));
            Assert.Empty(plan.Problems);
            Assert.Empty(FilmCut.MakeCards(plan, Run));
            Assert.All(plan.Cards, c => Assert.True(new FileInfo(c.File).Length > 0));
            var shotSum = plan.Shots.Sum(s => s.Seconds);
            Assert.Equal(7, shotSum, 3);
            var srt = P("Drei Farben.srt"); File.WriteAllText(srt, FilmCut.Srt(plan));
            var project = FilmCut.ToProject(plan, srt);
            Log("ffmpeg " + string.Join(' ', VideoEdit.ExportArgs(project, P("Drei Farben.mp4")).Select(x => x.Replace(dir, "<temp>"))));
            if (Environment.GetEnvironmentVariable("TRENDO_T0693_DEBUG") is { Length: > 0 } dbg) File.WriteAllText(dbg, log.ToString());
            var film = VideoEdit.Export(ff, project, P("Drei Farben.mp4"));

            // Dauer: Titel 3 + Einstellungen 7 + Abspann 4 − drei Überblendungen 0,5
            var dur = double.Parse(Probe(probe, dir, "-show_entries", "format=duration", "-of", "csv=p=0", film), CultureInfo.InvariantCulture);
            Log($"Dauer {dur.ToString("0.000", CultureInfo.InvariantCulture)} s, geplant {plan.Seconds.ToString("0.000", CultureInfo.InvariantCulture)} s (Einstellungen {shotSum} s + Titel 3 s + Abspann 4 s − Überblenden 1,5 s)");
            Assert.Equal(12.5, plan.Seconds, 3);
            Assert.InRange(dur, plan.Seconds - 0.25, plan.Seconds + 0.25);
            var streams = Probe(probe, dir, "-show_entries", "stream=codec_type,codec_name,width,height", "-of", "csv=p=0", film);
            Log("Spuren: " + streams.Replace("\r", "").Replace("\n", " | "));
            Assert.Contains("video", streams); Assert.Contains("audio", streams); Assert.Contains("640,360", streams);
            // Untertitel: Datei daneben, zwei Zeilen, Zeiten liegen in den richtigen Einstellungen
            var cues = TimedText.FromSrt(File.ReadAllText(srt));
            Assert.Equal(2, cues.Count);
            var starts = VideoEdit.Starts(FilmCut.Clips(plan));
            Assert.InRange(cues[0].Start, starts[2], starts[2] + 3); Assert.InRange(cues[1].Start, starts[3], starts[3] + 2);
            Log("Untertitel: " + string.Join(" | ", cues.Select(c => $"{c.Start.ToString("0.00", CultureInfo.InvariantCulture)}–{c.End.ToString("0.00", CultureInfo.InvariantCulture)} {c.Text.Replace('\n', ' ')}")));
            // Standbild aus der Mitte jeder Einstellung: rot, grün, blau
            var want = new[] { ("rot", 1), ("gruen", 2), ("blau", 3) };
            foreach (var (name, i) in want)
            {
                var t = starts[i] + plan.Pieces[i].Seconds / 2; var (r, g, b) = PixelAt(ff, dir, film, t);
                Log($"Standbild bei {t.ToString("0.00", CultureInfo.InvariantCulture)} s ({plan.Pieces[i].ShotId}, erwartet {name}): R={r} G={g} B={b}");
                var max = Math.Max(r, Math.Max(g, b));
                Assert.True(max > 150 && (name switch { "rot" => r, "gruen" => g, _ => b }) == max && (r + g + b - max) < 120, $"{name} bei {t}: {r},{g},{b}");
            }
            // Titel am Anfang ist die dunkle Tafel, kein Clip
            var (tr, tg, tb) = PixelAt(ff, dir, film, 1); Log($"Standbild bei 1,00 s (Titel): R={tr} G={tg} B={tb}");
            Assert.True(tr < 90 && tg < 90 && tb < 90);

            // Ohne Titel, Abspann und Überblenden ist die Dauer genau die Summe der Einstellungen
            FilmCutOptions.Set(f, "title", false); FilmCutOptions.Set(f, "credits", false); FilmCutOptions.Set(f, "fade", 0);
            var bare = FilmCut.Plan(f, "Unbenannt", P("tafeln"));
            var film2 = VideoEdit.Export(ff, FilmCut.ToProject(bare), P("nur-einstellungen.mp4"));
            var dur2 = double.Parse(Probe(probe, dir, "-show_entries", "format=duration", "-of", "csv=p=0", film2), CultureInfo.InvariantCulture);
            Log($"Nur Einstellungen: Dauer {dur2.ToString("0.000", CultureInfo.InvariantCulture)} s, Summe der Einstellungen {shotSum} s");
            Assert.InRange(dur2, shotSum - 0.2, shotSum + 0.2);

            Log($"SHA-256 {Path.GetFileName(film)}: {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(film))).ToLowerInvariant()} ({new FileInfo(film).Length} Bytes)");
            Log($"SHA-256 {Path.GetFileName(srt)}: {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(srt))).ToLowerInvariant()}");
            // Beleg nur auf Anforderung (reports/T-0693): Rohlog, SRT und ein Standbild
            if (Environment.GetEnvironmentVariable("TRENDO_T0693_BELEG") is { Length: > 0 } beleg)
            {
                Directory.CreateDirectory(beleg);
                File.WriteAllText(Path.Combine(beleg, "kern-rohlog.txt"), log.ToString());
                File.Copy(srt, Path.Combine(beleg, "Drei Farben.srt"), true);
                Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", "-ss", (starts[2] + 1.5).ToString("0.###", CultureInfo.InvariantCulture), "-i", film, "-frames:v", "1", Path.Combine(beleg, "kern-standbild-einstellung-2.png")], dir, 60000);
                Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", "-ss", "1", "-i", film, "-frames:v", "1", Path.Combine(beleg, "kern-standbild-titel.png")], dir, 60000);
            }
        }
        finally { Directory.Delete(dir, true); }
    }
}
