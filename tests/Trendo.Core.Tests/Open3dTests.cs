using System.Globalization;
using System.Text;
using System.Text.Json;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Alle Studios, Runde 2 – SPEC-09: Open3D (MIT) als Laufzeit „ScanNetz“. Echter Lauf nur mit TRENDO_OPEN3D_PYTHON (Python mit open3d), sonst übersprungen.
/// Beleg: reports/alle-studios/open3d-e2e.md.</summary>
public class Open3dTests
{
    [Fact(DisplayName = "Open3D +1: Einrichtung – nur PyPI, feste Fassung, kein PyTorch, Skript trendo_open3d.py")]
    public void Setup()
    {
        var a = KiPyApps.For(KiRuntime.ScanNetz)!;
        Assert.True(a.PackagesOnly); Assert.Empty(a.Torch); Assert.Contains("open3d==0.20.0", a.Packages); Assert.Equal("trendo_open3d.py", a.Script);
        var steps = KiPyApps.Steps(a, new KiPyApps.Paths("uv", "git", "d", "h"), true);
        Assert.DoesNotContain(steps, s => s.Args.Contains("clone") || s.Args.Contains("--index-url"));
    }

    [Fact(DisplayName = "Open3D +2: Laufzeit mit Lizenz MIT und Herkunft isl-org/Open3D")]
    public void Runtime_info()
    {
        var i = KiLibrary.RuntimeInfo(KiRuntime.ScanNetz);
        Assert.Equal("MIT", i.License); Assert.Equal("isl-org/Open3D", i.Repo); Assert.Equal("open3d-0.20.0", i.Tag);
    }

    [Fact(DisplayName = "IFC/Open3D +3: jede Python-Laufzeit hat Info, eigenes Skript und eindeutige Kennung")]
    public void All_runtimes_consistent()
    {
        foreach (var a in KiPyApps.All) { Assert.NotNull(KiLibrary.Runtimes.FirstOrDefault(r => r.Id == a.Id)); Assert.EndsWith(".py", a.Script); }
        Assert.Equal(KiPyApps.All.Count, KiPyApps.All.Select(a => a.Script).Distinct().Count());
        Assert.Equal(KiPyApps.All.Count, KiPyApps.All.Select(a => a.Id).Distinct().Count());
    }

    public sealed class Open3dFactAttribute : FactAttribute
    {
        public Open3dFactAttribute() { if (Python() is null) Skip = "Übersprungen – TRENDO_OPEN3D_PYTHON nicht gesetzt (Python mit open3d)."; }
    }
    static string? Python() => Environment.GetEnvironmentVariable("TRENDO_OPEN3D_PYTHON") is { Length: > 0 } p && File.Exists(p) ? p : null;
    static string Script() { var d = new DirectoryInfo(AppContext.BaseDirectory); while (d is not null && !File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) d = d.Parent; return Path.Combine(d!.FullName, "src", "Trendo.App", "Assets", "ki", "trendo_open3d.py"); }

    /// <summary>Kugel 10 cm Radius (OBJ, Meter) mit 3 mm Rauschen entlang der Normalen – wie ein verrauschter Scan.</summary>
    static string NoisySphere(string file, int rings = 90, int segs = 180)
    {
        var rnd = new Random(7); var sb = new StringBuilder(); var ci = CultureInfo.InvariantCulture;
        for (var i = 0; i <= rings; i++) for (var j = 0; j < segs; j++)
        {
            double th = Math.PI * i / rings, ph = 2 * Math.PI * j / segs; var r = 0.1 + (i is 0 || i == rings ? 0 : (rnd.NextDouble() * 2 - 1) * 0.0052);
            sb.Append(ci, $"v {r * Math.Sin(th) * Math.Cos(ph)} {r * Math.Sin(th) * Math.Sin(ph)} {r * Math.Cos(th)}\n");
        }
        for (var i = 0; i < rings; i++) for (var j = 0; j < segs; j++)
        {
            int a = i * segs + j + 1, b = i * segs + (j + 1) % segs + 1, c = (i + 1) * segs + j + 1, d = (i + 1) * segs + (j + 1) % segs + 1;
            sb.Append(ci, $"f {a} {c} {d}\nf {a} {d} {b}\n");
        }
        File.WriteAllText(file, sb.ToString()); return file;
    }
    static (double Mean, double Std) Radius(string obj)
    {
        var r = File.ReadLines(obj).Where(l => l.StartsWith("v ", StringComparison.Ordinal)).Select(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1..4].Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToArray())
            .Select(v => Math.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2])).Where(x => x > 0.05).ToList();
        var m = r.Average(); return (m, Math.Sqrt(r.Average(x => (x - m) * (x - m))));
    }

    [Open3dFact(DisplayName = "Open3D echt: Glätten halbiert das Rauschen ohne Schrumpfen, Vereinfachen erreicht die Ziel-Dreieckszahl, Unsinn wird abgelehnt")]
    public void Real_smooth_and_simplify()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-o3d-echt-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            var src = NoisySphere(Path.Combine(dir, "rauh.obj")); var (m0, s0) = Radius(src);
            var env = new Dictionary<string, string> { ["PYTHONUTF8"] = "1" };
            var smooth = Path.Combine(dir, "glatt.obj");
            var r = Cli.Run(Python()!, ["-I", Script(), "repo", "glaetten", src, smooth, "10"], dir, 300_000, env); Assert.True(r.Ok, r.Err);
            var (m1, s1) = Radius(smooth);
            Assert.True(s1 < s0 * 0.7, $"Rauschen {s0:F4} → {s1:F4}"); Assert.InRange(m1, m0 - 0.002, m0 + 0.002);
            var small = Path.Combine(dir, "klein.obj");
            var r2 = Cli.Run(Python()!, ["-I", Script(), "repo", "vereinfachen", src, small, "2000"], dir, 300_000, env); Assert.True(r2.Ok, r2.Err);
            using var j = JsonDocument.Parse(r2.Out.Trim().Split('\n')[^1]);
            Assert.Equal(32400, j.RootElement.GetProperty("vorher").GetInt32()); Assert.InRange(j.RootElement.GetProperty("nachher").GetInt32(), 1800, 2000);
            Assert.InRange(Radius(small).Mean, m0 - 0.004, m0 + 0.004);
            Assert.Equal(2, Cli.Run(Python()!, ["-I", Script(), "repo", "glaetten", src, smooth, "99"], dir, 60_000, env).Exit);
            Assert.Equal(2, Cli.Run(Python()!, ["-I", Script(), "repo", "loeschen", src, smooth, "1"], dir, 60_000, env).Exit);
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
