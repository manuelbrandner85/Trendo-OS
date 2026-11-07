using Trendo.Core;
using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Paket 01 – Studio 3, S3-01/S3-03 (07.11.2026): Drehbuch → Shotliste, Prüfung, Storyboard-Tafeln.</summary>
public class ShotPlanTests
{
    const string Script = """
        1 INNEN. KÜCHE – MORGEN
        Erika steht verschlafen am Fenster.
        - Totale: Erika betritt die Küche (4 s)
        - Nah: Kaffee läuft in die Tasse (3 s)
        - Detail: Ihre Hand greift nach der Tasse

        2 AUSSEN. STRASSE – TAG
        - Halbtotale: Erika geht los (5 s)
        - Fahrt: Kamera folgt ihr bis zur Ecke (6,5 s)
        """;

    [Fact(DisplayName = "S3-01-P: Drehbuch → Szenen mit Codes (SZ01, SZ01-E02), Innen/Außen, Ort, Tageszeit, Dauer; CSV-Shotliste")]
    public void Parse_script()
    {
        var p = ShotPlan.Parse(Script);
        Assert.Empty(p.Problems);
        Assert.Equal(["SZ01", "SZ02"], p.Scenes.Select(s => s.Code));
        Assert.Equal(("Innen", "KÜCHE", "MORGEN"), (p.Scenes[0].Place, p.Scenes[0].Location, p.Scenes[0].Time));
        Assert.Equal(["SZ01-E01", "SZ01-E02", "SZ01-E03"], p.Scenes[0].Shots.Select(x => x.Id));
        Assert.Equal(3, p.Scenes[0].Shots[2].Seconds); // ohne Angabe 3 s
        Assert.Equal(6.5, p.Scenes[1].Shots[1].Seconds); Assert.Equal(21.5, p.Seconds);
        var csv = ShotPlan.Csv(p);
        Assert.Contains("SZ02-E02;SZ02;Außen;STRASSE;TAG;Fahrt;Kamera folgt ihr bis zur Ecke;6.5", csv);
    }

    [Fact(DisplayName = "S3-01-N: widersprüchliche Reihenfolge, doppelte Nummer, Einstellung vor der ersten Szene und leere Szene werden gemeldet – nichts wird umsortiert")]
    public void Problems()
    {
        var p = ShotPlan.Parse("- Totale: zu früh\n3 INNEN. FLUR – NACHT\n- Nah: Tür\n2 AUSSEN. HOF – NACHT\n- Totale: Hof\n2 INNEN. KELLER – NACHT\n4 INNEN. DACH – NACHT\n- Wackelkamera: Blick");
        Assert.Contains(p.Problems, x => x.Contains("vor dem ersten Szenenkopf"));
        Assert.Contains(p.Problems, x => x.Contains("Szene 2 steht nach Szene 3"));
        Assert.Contains(p.Problems, x => x.Contains("Szenennummer 2 kommt 2×"));
        Assert.Contains(p.Problems, x => x.Contains("„KELLER“ hat keine Einstellung"));
        Assert.Contains(p.Problems, x => x.Contains("„Wackelkamera“ unbekannt"));
        Assert.Equal(["SZ03", "SZ02", "SZ02", "SZ04"], p.Scenes.Select(s => s.Code)); // Reihenfolge wie geschrieben
        Assert.Contains("Kein Szenenkopf", ShotPlan.Parse("nur Text").Problems[0]);
    }

    [Fact(DisplayName = "S3-03 echt mit FFmpeg: Storyboard-Tafel je Einstellung in Plan-Länge (MP4, 4 s), Code sichtbar")]
    public void Real_slate()
    {
        var ff = Cli.Find("ffmpeg"); var font = VideoEdit.FontFile(); if (ff is null || font is null) return;
        var dir = Path.Combine(Path.GetTempPath(), "trendo-shot-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            var p = ShotPlan.Parse(Script); var s = p.Scenes[0]; var x = s.Shots[0];
            var f = Path.Combine(dir, x.Id + ".mp4");
            var r = Cli.Run(ff, ShotPlan.SlateArgs(s, x, font, f, 640, 360), dir, 60000); Assert.True(File.Exists(f), r.Err);
            Assert.Contains("Duration: 00:00:04.00", Cli.Run(ff, ["-hide_banner", "-i", f], dir, 30000).Err);
            if (Environment.GetEnvironmentVariable("TRENDO_SHOT_BELEG") is { Length: > 0 } keep)
            {
                Directory.CreateDirectory(keep); File.Copy(f, Path.Combine(keep, x.Id + ".mp4"), true);
                Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", "-ss", "1", "-i", f, "-frames:v", "1", Path.Combine(keep, "tafel.png")], dir, 30000);
                File.WriteAllText(Path.Combine(keep, "Shotliste.csv"), ShotPlan.Csv(p));
            }
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
