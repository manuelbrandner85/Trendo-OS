using System.Text.Json;
using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Paket 01 – Studio 4, S4-17 (06.11.2026): Geräusch-Werkstatt und Bibliothek mit Lizenzprüfung.</summary>
public class FoleyTests
{
    /// <summary>Spektraler Schwerpunkt in Hz (Mittel über Blöcke von 4096 Werten).</summary>
    static double Centroid(AudioBuf a)
    {
        const int size = 4096; double num = 0, den = 0; var x = a.Data[0];
        for (var s = 0; s + size <= x.Length; s += size * 2)
        {
            var re = new double[size]; var im = new double[size];
            for (var i = 0; i < size; i++) re[i] = x[s + i] * (0.5 - 0.5 * Math.Cos(2 * Math.PI * i / size));
            AudioDsp.Fft(re, im);
            for (var k = 1; k < size / 2; k++) { var m = Math.Sqrt(re[k] * re[k] + im[k] * im[k]); num += m * k * a.Rate / (double)size; den += m; }
        }
        return num / Math.Max(1e-9, den);
    }

    [Fact(DisplayName = "S4-17-P: Geräusche selbst erzeugt – hörbar, ohne Übersteuerung, je Art typisch (Regen hell, Wind und Maschine dunkel, Schritte mit Einzelschlägen); Varianten per Zufallsstart")]
    public void Make_sounds()
    {
        foreach (var (id, _, _) in Foley.Kinds)
        {
            var a = Foley.Make(id, 3);
            var m = AudioQa.Analyze(a, 24);
            Assert.True(m.SamplePeakDb is > -3.5 and < -2.5, $"{id}: Spitze {m.SamplePeakDb}"); Assert.Equal(0, m.ClipEvents);
        }
        var rain = Centroid(Foley.Make("regen", 4)); var wind = Centroid(Foley.Make("wind", 4)); var engine = Centroid(Foley.Make("maschine", 4));
        Assert.True(rain > 2 * wind, $"Regen {rain:0} Hz, Wind {wind:0} Hz"); Assert.InRange(rain, 3000, 9000); Assert.InRange(wind, 200, 1500); // wie echte Aufnahmen Assert.True(engine < wind, $"Maschine {engine:0} Hz, Wind {wind:0} Hz");
        Assert.InRange(BeatDetect.Find(Foley.Make("schritte-kies", 8, 0.5)).Beats.Count, 6, 30); // einzelne Schritte erkennbar
        Assert.NotEqual(Foley.Make("regen", 1, seed: 1).Data[0][24000], Foley.Make("regen", 1, seed: 2).Data[0][24000]);
        Assert.Throws<ArgumentException>(() => Foley.Make("donner", 2));
        Assert.Throws<ArgumentException>(() => Foley.Make("regen", 500));
    }

    [Fact(DisplayName = "S4-17-N: Paket nur mit gültigen Rechten – unlizenziertes und verändertes Geräusch werden ausgeschlossen und mit Grund gelistet; Lizenzen.csv und Ereignisse.json")]
    public void Library_and_pack()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-foley-" + Guid.NewGuid().ToString("N")[..6]);
        try
        {
            var lib = Path.Combine(dir, "Geräusche");
            var rain = Foley.Add(lib, Foley.Make("regen", 2), "Regen leicht", ["Wetter.Regen", "Loop"], Foley.OwnLicense, "Trendo Geräusch-Werkstatt");
            var steps = Foley.Add(lib, Foley.Make("schritte-kies", 2), "Schritte Kies", ["Schritt.Kies"], Foley.OwnLicense, "Trendo Geräusch-Werkstatt");
            var foreign = Foley.Add(lib, Foley.Make("feuer", 1), "Feuer aus dem Netz", ["Umgebung.Feuer"], "unbekannt", "Download ohne Angabe");
            var changed = Foley.Add(lib, Foley.Make("wind", 1), "Wind", ["Wetter.Wind"], "CC0 1.0", "Freesound (Beispiel)");
            File.AppendAllText(changed.File, "x"); // nachträglich verändert
            Assert.Equal(["Regen leicht"], Foley.Find(lib, "Regen").Select(e => e.Name));
            Assert.Single(Foley.Find(lib, "Schritt."));
            var pack = Foley.Pack(lib, Path.Combine(dir, "Paket"));
            Assert.Equal(new[] { rain.Name, steps.Name }, pack.Included.Select(e => e.Name));
            Assert.Contains(pack.Excluded, x => x.Entry.Name == foreign.Name && x.Reason.Contains("unbekannt"));
            Assert.Contains(pack.Excluded, x => x.Entry.Name == changed.Name && x.Reason.Contains("Prüfsumme"));
            Assert.False(File.Exists(Path.Combine(pack.Folder, Path.GetFileName(foreign.File))));
            var csv = File.ReadAllText(Path.Combine(pack.Folder, "Lizenzen.csv"));
            Assert.Contains("Regen leicht", csv); Assert.DoesNotContain("Feuer", csv); Assert.Contains(rain.Sha256, csv);
            var ev = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(File.ReadAllText(Path.Combine(pack.Folder, "Ereignisse.json")))!;
            Assert.Equal(["Regen leicht.wav"], ev["Wetter.Regen"]); Assert.False(ev.ContainsKey("Umgebung.Feuer"));
            Assert.Contains("Feuer aus dem Netz", File.ReadAllText(Path.Combine(pack.Folder, "Ausgeschlossen.txt")));
            if (Environment.GetEnvironmentVariable("TRENDO_FOLEY_BELEG") is { Length: > 0 } keep)
            {
                Directory.CreateDirectory(keep);
                foreach (var f in Directory.GetFiles(pack.Folder)) File.Copy(f, Path.Combine(keep, Path.GetFileName(f)), true);
                File.WriteAllText(Path.Combine(keep, "messung.txt"), $"Spektraler Schwerpunkt: Regen {Centroid(Foley.Make("regen", 4)):0} Hz, Wind {Centroid(Foley.Make("wind", 4)):0} Hz, Maschine {Centroid(Foley.Make("maschine", 4)):0} Hz\nSchritte Kies (8 s): {BeatDetect.Find(Foley.Make("schritte-kies", 8, 0.5)).Beats.Count} Schläge erkannt\n");
            }
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
