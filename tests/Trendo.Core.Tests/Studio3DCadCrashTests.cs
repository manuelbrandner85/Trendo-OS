using Trendo.Core.Cad;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0691: Die automatische Sicherung von „3D-Modelle erstellen“ und Trendo CAD übersteht einen echten Prozessabbruch.
/// Das Hilfsprogramm tests/Trendo.CrashProbe schreibt die Sicherung genau wie die App (Scene3D.SaveTo / CadDoc.Save) und wird hart beendet.</summary>
public class Studio3DCadCrashTests(ITestOutputHelper output)
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-absturz-3d-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    void Run(string mode, string file, Func<string, int> read)
    {
        var dir = Tmp(); var rnd = new Random(691); int during = 0, worst = 0;
        try
        {
            for (var round = 1; round <= 16; round++)
            {
                if (ProgramCrashTests.RunAndKill(mode, dir, 2, rnd)) during++;
                var (started, done, _) = ProgramCrashTests.ReadLog(dir);
                var path = Path.Combine(dir, file);
                int n;
                try { n = read(path); }
                catch (Exception e) { Assert.Fail($"Runde {round}: Sicherung nach Abbruch unlesbar: {e.Message}"); return; }
                Assert.True(n >= done, $"Runde {round}: Sicherung hat Stand {n}, fertig gemeldet war {done}");
                Assert.True(n <= started, $"Runde {round}: Stand {n} > begonnen {started}");
                worst = Math.Max(worst, started - n);
            }
            Assert.True(worst <= 1, "Mehr als der eine laufende Schritt ging verloren: " + worst);
            Assert.True(during >= 8, $"Nur {during} von 16 Abbrüchen fielen in einen laufenden Schreibvorgang – der Test taugt so nicht.");
            output.WriteLine($"{mode}: 16 harte Abbrüche, {during} mitten im Schreiben, höchstens {worst} Schritt verloren.");
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    [Fact(DisplayName = "T-0691 Absturz: 3D-Szene – 16-mal hart beendet, die Sicherung ist jedes Mal vollständig lesbar, kein fertig gesicherter Stand fehlt")]
    public void Kill_while_autosaving_scene() => Run("szene3d", "3d-szene.json", f =>
    {
        var s = Scene3D.Load(f);
        Assert.Equal(40_000 * 9, s.Parts[0].Positions!.Length); // nichts abgeschnitten
        return int.Parse(s.Name.Split(' ')[1], System.Globalization.CultureInfo.InvariantCulture);
    });

    [Fact(DisplayName = "T-0691 Absturz: CAD-Bauteil – 16-mal hart beendet, die Sicherung ist jedes Mal vollständig lesbar, kein fertig gesicherter Stand fehlt")]
    public void Kill_while_autosaving_cad() => Run("cad", "cad.tcad", f =>
    {
        var d = CadDoc.Load(f);
        Assert.Equal(40_000 * 9, d.References[0].Positions.Length);
        return (int)d.Params["n"];
    });
}
