using System.Text;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Alle Studios, Runde 1 – CORE-05: 3D-Dateien umwandeln mit assimp 6.0.5 im eigenen Prozess. Echte Läufe nur mit TRENDO_ASSIMP_DLL
/// (Pfad zur assimp-DLL aus der offiziellen Zip), sonst übersprungen. Beleg: reports/alle-studios/assimp-e2e.md.</summary>
public class AssimpConvertTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "trendo-assimp-" + Guid.NewGuid().ToString("N"));
    public AssimpConvertTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    string Cube(string name = "wuerfel.obj")
    {
        var f = Path.Combine(_dir, name);
        File.WriteAllText(f, "o Wuerfel\nv 0 0 0\nv 1 0 0\nv 1 1 0\nv 0 1 0\nv 0 0 1\nv 1 0 1\nv 1 1 1\nv 0 1 1\nf 1 4 3 2\nf 5 6 7 8\nf 1 2 6 5\nf 2 3 7 6\nf 3 4 8 7\nf 4 1 5 8\n");
        return f;
    }
    string FakeDll() { var f = Path.Combine(_dir, "assimp.dll"); File.WriteAllText(f, "x"); return f; }

    [Fact(DisplayName = "assimp +1: Prozess-Argumente – Trendo selbst mit Schalter, DLL, Ein- und Ausgabe")]
    public void Args() => Assert.Equal(["--assimp-convert", "a.dll", "x.fbx", "y.glb"], AssimpConvert.Args("a.dll", "x.fbx", "y.glb"));

    [Fact(DisplayName = "assimp +2: Ausgabeformate auf die assimp-Exportkennungen abgebildet (GLB = glb2)")]
    public void Output_ids()
    {
        Assert.Equal("glb2", AssimpConvert.Outputs[".glb"]); Assert.Equal("stlb", AssimpConvert.Outputs[".stl"]); Assert.Equal("collada", AssimpConvert.Outputs[".dae"]);
    }

    [Fact(DisplayName = "assimp +3: Umwandeln über eigenen Prozess – Ergebnis-Datei, Ordner angelegt")]
    public void Convert_ok()
    {
        var o = Path.Combine(_dir, "aus", "w.glb"); IReadOnlyList<string>? seen = null;
        var (ok, msg) = AssimpConvert.Convert("TrendoOS.exe", FakeDll(), Cube(), o, run: (e, a, t) => { seen = a; File.WriteAllText(a[3], "glTF"); return new Cli.Result(0, "", ""); });
        Assert.True(ok, msg); Assert.Equal(o, msg); Assert.Equal("--assimp-convert", seen![0]);
    }

    [Fact(DisplayName = "assimp −1: Absturz des Umwandlungs-Prozesses wird gemeldet – Trendo läuft weiter")]
    public void Crash_reported()
    {
        var (ok, msg) = AssimpConvert.Convert("TrendoOS.exe", FakeDll(), Cube(), Path.Combine(_dir, "w.glb"), run: (_, _, _) => new Cli.Result(-1073741819, "", ""));
        Assert.False(ok); Assert.Contains("abgestürzt", msg);
        Assert.Equal("assimp kann die Datei nicht lesen: X", AssimpConvert.Convert("t", FakeDll(), Cube(), Path.Combine(_dir, "w.glb"), run: (_, _, _) => new Cli.Result(3, "", "assimp kann die Datei nicht lesen: X\n")).Message);
    }

    [Fact(DisplayName = "assimp −2: fehlende/leere Datei, falsches Format, falsches Ziel, Quelle = Ziel werden abgelehnt")]
    public void Check_refuses()
    {
        Assert.Contains("gibt es nicht", AssimpConvert.Check(Path.Combine(_dir, "nichts.fbx"), "a.glb"));
        var empty = Path.Combine(_dir, "leer.fbx"); File.WriteAllText(empty, ""); Assert.Contains("leer", AssimpConvert.Check(empty, "a.glb"));
        var txt = Path.Combine(_dir, "a.txt"); File.WriteAllText(txt, "x"); Assert.Contains("nicht unterstützt", AssimpConvert.Check(txt, "a.glb"));
        Assert.Contains("Ziel muss", AssimpConvert.Check(Cube(), Path.Combine(_dir, "a.exe")));
        var c = Cube(); Assert.Contains("dieselbe", AssimpConvert.Check(c, c));
    }

    [Fact(DisplayName = "assimp −3: ohne DLL keine Umwandlung und kein Prozessstart")]
    public void No_dll()
    {
        var started = false;
        var r = AssimpConvert.Convert("t", Path.Combine(_dir, "fehlt.dll"), Cube(), Path.Combine(_dir, "w.glb"), run: (_, _, _) => { started = true; return new Cli.Result(0, "", ""); });
        Assert.False(r.Ok); Assert.Contains("nicht eingerichtet", r.Message); Assert.False(started);
    }

    [Fact(DisplayName = "assimp −4: im Prozess – ungültige Eingabe Exit 2, fehlende DLL 5, keine DLL-Datei 5 (BadImageFormat)")]
    public void In_process_codes()
    {
        var err = new StringWriter();
        Assert.Equal(2, AssimpConvert.RunInThisProcess(FakeDll(), Path.Combine(_dir, "x.fbx"), Path.Combine(_dir, "y.glb"), err));
        Assert.Equal(5, AssimpConvert.RunInThisProcess(Path.Combine(_dir, "fehlt.dll"), Cube(), Path.Combine(_dir, "y.glb"), err));
        Assert.Equal(5, AssimpConvert.RunInThisProcess(FakeDll(), Cube(), Path.Combine(_dir, "y.glb"), err));
        Assert.Contains("nicht laden", err.ToString());
    }

    // ---------- echte Läufe ----------
    public sealed class AssimpFactAttribute : FactAttribute
    {
        public AssimpFactAttribute() { if (Dll() is null) Skip = "Übersprungen – TRENDO_ASSIMP_DLL nicht gesetzt (assimp-DLL aus der offiziellen Zip)."; }
    }
    static string? Dll() => Environment.GetEnvironmentVariable("TRENDO_ASSIMP_DLL") is { Length: > 0 } p && File.Exists(p) ? p : null;

    [AssimpFact(DisplayName = "assimp echt: OBJ → GLB → OBJ (8 Ecken, 12 Dreiecke bleiben), kaputte Datei ergibt Exit 3 ohne Absturz")]
    public void Real_roundtrip()
    {
        var glb = Path.Combine(_dir, "w.glb"); var back = Path.Combine(_dir, "zurueck.obj"); var err = new StringWriter();
        Assert.Equal(0, AssimpConvert.RunInThisProcess(Dll()!, Cube(), glb, err));
        Assert.Equal("glTF", Encoding.ASCII.GetString(File.ReadAllBytes(glb), 0, 4));
        Assert.Equal(0, AssimpConvert.RunInThisProcess(Dll()!, glb, back, err));
        var lines = File.ReadAllLines(back);
        Assert.Equal(12, lines.Count(l => l.StartsWith("f ", StringComparison.Ordinal)));
        Assert.Equal(8, lines.Where(l => l.StartsWith("v ", StringComparison.Ordinal)).Distinct().Count());
        var bad = Path.Combine(_dir, "kaputt.fbx"); File.WriteAllBytes(bad, [.. "Kaydara FBX Binary  \0"u8, .. Enumerable.Repeat((byte)0xFF, 200)]);
        Assert.Equal(3, AssimpConvert.RunInThisProcess(Dll()!, bad, Path.Combine(_dir, "k.glb"), err));
        if (Environment.GetEnvironmentVariable("TRENDO_WERKZEUGE_BELEG") is { Length: > 0 } b) File.WriteAllText(Path.Combine(b, "assimp-fehlermeldung.txt"), err.ToString());
    }

    [AssimpFact(DisplayName = "assimp echt über TrendoOS --assimp-convert: eigener Prozess schreibt GLB")]
    public void Real_via_trendo_process()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory); while (d is not null && !File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) d = d.Parent;
        var exe = Path.Combine(d!.FullName, "src", "Trendo.App", "bin", "Release", "net10.0", OperatingSystem.IsWindows() ? "TrendoOS.exe" : "TrendoOS");
        Assert.True(File.Exists(exe), "TrendoOS bitte vorher bauen: " + exe);
        var glb = Path.Combine(_dir, "p.glb");
        var (ok, msg) = AssimpConvert.Convert(exe, Dll()!, Cube(), glb);
        Assert.True(ok, msg); Assert.Equal("glTF", Encoding.ASCII.GetString(File.ReadAllBytes(glb), 0, 4));
    }
}
