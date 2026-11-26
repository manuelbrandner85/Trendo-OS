using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Trendo.Core;
using Trendo.Core.Cad;

// T-0772 Mess-Harness: gleicher Weg wie die Oberfläche (CncJob.ImportDrawing) bzw. einzelne Phasen.
// --perf <datei> <sek>        ImportDrawing mit Zeitgrenze (Abbruch über CancellationToken), Prüfwert G-Code/Meldungen
// --phasen <datei> <sek>      Lesen / Konturen / Generate getrennt, je mit Zeitgrenze
// --cancel <datei> <ms...>    ImportDrawing nach ms abbrechen, Nachlauf messen
static string H(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)))[..16];
var gravur = args.Length > 1 && Path.GetFileName(args[1]).Contains("gravur");
CncJob Job() => new() { ToolD = 6, Depth = 3, StepDown = 1, CamMode = gravur ? "auf" : "auto" };
var mode = args[0]; var f = args[1];
if (mode == "--batch") { Batch.Run(f, int.Parse(args[2])); return; }
if (mode == "--perf")
{
    var secs = int.Parse(args[2]); var j = Job(); var sw = Stopwatch.StartNew();
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(secs));
    try { var r = j.ImportDrawing(f, cts.Token); Console.WriteLine($"ok {sw.ElapsedMilliseconds} ms G {H(r.Gcode)} W {H(string.Join("\n", r.Warnings))} Bahnen {r.Paths.Count} Meldungen {r.Warnings.Count}"); }
    catch (Exception e) { Console.WriteLine($"{e.GetType().Name} nach {sw.ElapsedMilliseconds} ms W {H(e.Message)}: {e.Message[..Math.Min(240, e.Message.Length)]}"); }
    Console.WriteLine($"PeakWS {Process.GetCurrentProcess().PeakWorkingSet64 / 1048576} MB");
    return;
}
if (mode == "--phasen")
{
    var secs = int.Parse(args[2]); var j = Job();
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(secs)); var sw = Stopwatch.StartNew();
    try
    {
        var rd = CadDxf.Read(f, cts.Token); Console.WriteLine($"Lesen {sw.ElapsedMilliseconds} ms ({rd.Drawing.Entities.Count} Elemente)"); sw.Restart();
        var notes = new List<string>();
        var cs = CncCam.Contours(rd.Drawing, notes, ct: cts.Token); Console.WriteLine($"Konturen {sw.ElapsedMilliseconds} ms {Stats()} ({cs.Count}, Hinweise {notes.Count}) K {H(string.Join(";", cs.Select(c => c.Closed + ":" + c.JunctionGroup + ":" + string.Join(",", c.Pts.Select(p => p.X.ToString("R") + "/" + p.Y.ToString("R"))))))} N {H(string.Join("\n", notes))}"); sw.Restart();
        var r = CncCam.Generate(cs, j.CamSettings() with { Unread = rd.Unread }, cts.Token); Console.WriteLine($"Generate {sw.ElapsedMilliseconds} ms G {H(r.Gcode)} W {H(string.Join("\n", r.Warnings))} Bahnen {r.Paths.Count}");
    }
    catch (Exception e) { Console.WriteLine($"{e.GetType().Name} nach {sw.ElapsedMilliseconds} ms (Phase): {e.Message[..Math.Min(200, e.Message.Length)]}"); }
    return;
}
if (mode == "--cancel")
{
    long worst = 0;
    foreach (var d in args.Skip(2).Select(int.Parse))
    {
        var j = Job(); using var cts = new CancellationTokenSource(); var sw = Stopwatch.StartNew();
        var t = Task.Run(() => j.ImportDrawing(f, cts.Token));
        if (t.Wait(d)) { Console.WriteLine($"Abbruch {d} ms: war fertig nach {sw.ElapsedMilliseconds} ms"); continue; }
        var s2 = Stopwatch.StartNew(); cts.Cancel();
        try { t.Wait(); Console.WriteLine($"Abbruch {d} ms: fertig ohne Abbruch, {s2.ElapsedMilliseconds} ms nach Anforderung"); }
        catch (AggregateException e) { Console.WriteLine($"Abbruch {d} ms: {e.InnerException?.GetType().Name} nach {s2.ElapsedMilliseconds} ms"); }
        worst = Math.Max(worst, s2.ElapsedMilliseconds);
    }
    Console.WriteLine($"Nachlauf hoechstens {worst} ms");
}
static string Stats() { var f = typeof(CadContours).GetField("LastBridgeStats"); return f is null ? "" : "Stats " + f.GetValue(null); }
static class Batch
{
    // --batch <ordner> <sek>: je Datei ImportDrawing, Prüfwert aus G-Code und Meldungen (bzw. Fehlermeldung)
    public static void Run(string dir, int secs)
    {
        foreach (var f in Directory.GetFiles(dir, "*.dxf").Order(StringComparer.Ordinal))
        {
            var j = new CncJob { ToolD = 6, Depth = 3, StepDown = 1, CamMode = Path.GetFileName(f).Contains("gravur") ? "auf" : "auto" };
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(secs)); var sw = Stopwatch.StartNew(); string res;
            try { var r = j.ImportDrawing(f, cts.Token); res = "ok " + Hx(r.Gcode) + " " + Hx(string.Join("\n", r.Warnings)) + " " + r.Paths.Count; }
            catch (Exception e) { res = e.GetType().Name + " " + Hx(e.Message); }
            Console.WriteLine($"{Path.GetFileName(f)}\t{res}\t{sw.ElapsedMilliseconds}");
        }
    }
    static string Hx(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)))[..16];
}
