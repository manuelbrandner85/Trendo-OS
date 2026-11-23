using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Trendo.Core;
using Trendo.Core.Arch;
// Prüf-Harness pruef8-regression-T-0747: gleicher Weg wie die Oberfläche (CncJob.ImportDrawing).
static CncJob MakeJob(string f)
{
    var setFile = Path.Combine(Path.GetDirectoryName(f)!, Path.GetFileNameWithoutExtension(f) + ".set.json");
    var set = File.Exists(setFile) ? JsonNode.Parse(File.ReadAllText(setFile))!.AsObject() : new JsonObject();
    double N(string k, double def) => set[k] is JsonValue v ? v.GetValue<double>() : def;
    return new CncJob { ToolD = N("toolD", 6), Depth = N("depth", 3), StepDown = N("stepDown", 1), CamMode = (string?)set["mode"] ?? "auto", CamTabs = (int)N("tabs", 0), CamTabW = N("tabW", 6), CamTabH = N("tabH", 1) };
}
static string H(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)))[..16];
static string State(CncJob j) => H(j.Gcode) + "|" + j.Contours.Count + "|" + H(string.Join(";", j.Contours.Select(c => c.Closed + ":" + string.Join(",", c.Pts.Select(p => p.X.ToString("R") + "/" + p.Y.ToString("R")))))) + "|" + j.CamSource + "|" + j.Unread.Count + "|" + H(string.Join(";", j.Unread.Select(u => u.What + u.B.ToString()))) + "|" + j.CamKey + "|" + j.CamHash;

if (args[0] == "--det")
{
    // --det <datei> <läufe> [<ausgabe-nc>]
    var n = int.Parse(args[2]); string? first = null; var diff = 0;
    for (var k = 1; k <= n; k++)
    {
        var j = MakeJob(args[1]); var sw = Stopwatch.StartNew();
        string g, w; int paths;
        try { var r = j.ImportDrawing(args[1]); g = r.Gcode; w = string.Join("\n", r.Warnings); paths = r.Paths.Count; }
        catch (Exception e) { g = ""; w = e.GetType().Name + ": " + e.Message; paths = -1; }
        sw.Stop();
        var key = H(g) + " " + H(w);
        if (first is null) { first = key; if (args.Length > 3) { File.WriteAllText(args[3], g); File.WriteAllText(args[3] + ".warn.txt", w); } }
        else if (key != first) diff++;
        Console.WriteLine($"Lauf {k,2}: G-Code {H(g)} Meldungen {H(w)} ({w.Split('\n').Length}) Bahnen {paths} {sw.ElapsedMilliseconds} ms {(key == first ? "gleich" : "ABWEICHEND")}");
    }
    Console.WriteLine($"Determinismus: {n} Läufe, abweichend {diff}; PeakWorkingSet {Process.GetCurrentProcess().PeakWorkingSet64 / 1048576} MB");
    return;
}
if (args[0] == "--cancel")
{
    // --cancel <datei> <ms...>: Fräsbahn-Erzeugung nach ms abbrechen, Nachlauf messen
    var f = args[1]; var j = MakeJob(f); var rd = Trendo.Core.Cad.CadDxf.Read(f); var cs = CncCam.Contours(rd.Drawing); var set = j.CamSettings() with { Unread = rd.Unread };
    long worst = 0;
    foreach (var d in args.Skip(2).Select(int.Parse))
    {
        using var cts = new CancellationTokenSource(); var sw = Stopwatch.StartNew(); var t = Task.Run(() => CncCam.Generate(cs, set, cts.Token));
        if (t.Wait(d)) { Console.WriteLine($"Abbruch {d} ms: war fertig nach {sw.ElapsedMilliseconds} ms"); break; }
        var s2 = Stopwatch.StartNew(); cts.Cancel();
        try { t.Wait(); Console.WriteLine($"Abbruch {d} ms: fertig ohne Abbruch, {s2.ElapsedMilliseconds} ms nach Anforderung"); worst = Math.Max(worst, s2.ElapsedMilliseconds); }
        catch (AggregateException e) { Console.WriteLine($"Abbruch {d} ms: {e.InnerException?.GetType().Name} nach {s2.ElapsedMilliseconds} ms"); worst = Math.Max(worst, s2.ElapsedMilliseconds); }
    }
    Console.WriteLine($"Nachlauf hoechstens {worst} ms");
    return;
}
if (args[0] == "--gen")
{
    // --gen <datei> [<sek>]: nur Fräsbahn-Erzeugung (Konturen vorher), Zeit und Speicher
    var f = args[1]; var secs = args.Length > 2 ? int.Parse(args[2]) : 600; var j = MakeJob(f); var rd = Trendo.Core.Cad.CadDxf.Read(f); var cs = CncCam.Contours(rd.Drawing); var set = j.CamSettings() with { Unread = rd.Unread };
    var m0 = Process.GetCurrentProcess().PeakWorkingSet64 / 1048576; var a0 = GC.GetTotalAllocatedBytes(true);
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(secs)); var sw = Stopwatch.StartNew();
    try { var r = CncCam.Generate(cs, set, cts.Token); Console.WriteLine($"Generate ok {sw.ElapsedMilliseconds} ms G {H(r.Gcode)} W {H(string.Join("|", r.Warnings))} Bahnen {r.Paths.Count}"); }
    catch (Exception e) { Console.WriteLine($"Generate {e.GetType().Name} nach {sw.ElapsedMilliseconds} ms"); }
    Console.WriteLine($"PeakWS vor {m0} MB nach {Process.GetCurrentProcess().PeakWorkingSet64 / 1048576} MB, alloziert {(GC.GetTotalAllocatedBytes(true) - a0) / 1048576} MB");
    return;
}
if (args[0] == "--phasen")
{
    var f = args[1]; var j = MakeJob(f); var sw = Stopwatch.StartNew();
    var rd = Trendo.Core.Cad.CadDxf.Read(f); Console.WriteLine($"Read {sw.ElapsedMilliseconds} ms"); sw.Restart();
    var cs = CncCam.Contours(rd.Drawing); Console.WriteLine($"Contours {sw.ElapsedMilliseconds} ms ({cs.Count})"); sw.Restart();
    typeof(CncCam).GetField("DbgPhase")?.SetValue(null, (Action<string>)(m => Console.WriteLine("  " + m)));
    var gr = CncCam.Generate(cs, j.CamSettings()); Console.WriteLine($"Generate {sw.ElapsedMilliseconds} ms, Bahnen {gr.Paths.Count}, G {gr.Gcode.Length}, PeakWS {System.Diagnostics.Process.GetCurrentProcess().PeakWorkingSet64 / 1048576} MB");
    typeof(CncCam).GetField("DbgPhase")?.SetValue(null, null);
    foreach (var d in args.Skip(2).Select(int.Parse))
    {
        using var cts = new CancellationTokenSource(); var t = Task.Run(() => CncCam.Generate(cs, j.CamSettings(), cts.Token)); Thread.Sleep(d); var s2 = Stopwatch.StartNew(); cts.Cancel();
        try { t.Wait(); Console.WriteLine($"Generate-Abbruch {d} ms: war fertig"); } catch (AggregateException e) { Console.WriteLine($"Generate-Abbruch {d} ms: {e.InnerException?.GetType().Name} nach {s2.ElapsedMilliseconds} ms"); }
    }
    return;
}
if (args[0] == "--abbruch")
{
    // --abbruch <A> <B>
    foreach (var delay in new[] { 1, 50, 300, 1000, 3000, 6000 })
    {
        var j = MakeJob(args[1]);
        j.ImportDrawing(args[1]);
        var before = State(j);
        using var cts = new CancellationTokenSource(); var sw = Stopwatch.StartNew();
        var t = Task.Run(() => j.ImportDrawing(args[2], cts.Token));
        Thread.Sleep(delay); var tc = sw.ElapsedMilliseconds; cts.Cancel();
        string how;
        try { t.Wait(); how = $"war schon fertig nach {sw.ElapsedMilliseconds} ms (Auftrag ist jetzt B)"; Console.WriteLine($"Abbruch nach {delay} ms: {how}"); continue; }
        catch (AggregateException e) { how = $"{e.InnerException?.GetType().Name} {sw.ElapsedMilliseconds - tc} ms nach dem Abbrechen"; }
        Console.WriteLine($"Abbruch nach {delay} ms: {how}; Auftrag unverändert: {State(j) == before}");
    }
    { var j = MakeJob(args[2]); var sw = Stopwatch.StartNew(); try { j.ImportDrawing(args[2]); } catch (Exception e) { Console.Write(e.GetType().Name + " "); } Console.WriteLine($"B ohne Abbruch: {sw.ElapsedMilliseconds} ms"); }
    return;
}
if (args[0] == "--fix")
{
    // --fix <repo> <ausgabe>: Belegdateien wie die Repo-Tests erzeugen
    var repo = args[1]; var od = args[2]; Directory.CreateDirectory(od);
    var fx = Path.Combine(repo, "tests", "fixtures", "t0747");
    var s = new CamSettings { ToolD = 4, Depth = 6, StepDown = 2, Feed = 800, Plunge = 200, Tabs = 3, TabW = 5, TabH = 1.5, Rpm = 18000 };
    File.WriteAllText(Path.Combine(od, "teil-fremd-dxf.nc"), CncCam.Generate(CncCam.Contours(Trendo.Core.Cad.CadDxf.Read(Path.Combine(fx, "teil-fremd.dxf")).Drawing), s).Gcode);
    File.WriteAllText(Path.Combine(od, "teil-fremd-svg.nc"), CncCam.Generate(CncCam.Contours(Trendo.Core.Cad.CadSvg.Read(Path.Combine(fx, "teil-fremd.svg")).Drawing), s).Gcode);
    var cs = CncCam.Contours(Trendo.Core.Cad.CadDxf.Read(Path.Combine(repo, "reports", "T-0747", "fix-faelle.dxf")).Drawing);
    var r = CncCam.Generate(cs, new CamSettings { ToolD = 6, Depth = 3, StepDown = 1.5, Feed = 800, Plunge = 200 });
    File.WriteAllText(Path.Combine(od, "fix-faelle.nc"), r.Gcode);
    File.WriteAllText(Path.Combine(od, "fix-faelle-hinweise.txt"), string.Join("\n", r.Warnings) + "\n");
    var rw = Trendo.Core.Cad.CadDxf.Read(Path.Combine(fx, "wohnung-fremd.dxf"));
    var p = new FloorPlan(); PlanDxfImport.Import(p, rw);
    p.Walls.Single(w => Math.Abs(w.Thickness - 11.5) < 0.01).Thickness = 17.5;
    File.WriteAllText(Path.Combine(od, "wohnung-bearbeitet.dxf"), PlanDrawing.Dxf(p));
    File.WriteAllText(Path.Combine(od, "wohnung-unread.txt"), string.Join("\n", rw.Unread.Select(u => u.What + " " + u.B)) + "\n" + string.Join("\n", rw.Skipped.Select(k => k.Key + "=" + k.Value)));
    foreach (var (f, name) in new[] { (Path.Combine(fx, "wohnung-fremd.dxf"), "wohnung-fremd-job"), (Path.Combine(fx, "teil-fremd.dxf"), "teil-fremd-job"), (Path.Combine(fx, "teil-fremd.svg"), "teil-fremd-svg-job") })
    {
        try { var j = new CncJob { ToolD = 4, Depth = 6, StepDown = 2 }; var rr = j.ImportDrawing(f); File.WriteAllText(Path.Combine(od, name + ".nc"), rr.Gcode); File.WriteAllText(Path.Combine(od, name + ".warn.txt"), string.Join("\n", rr.Warnings)); }
        catch (Exception e) { File.WriteAllText(Path.Combine(od, name + ".nc"), ""); File.WriteAllText(Path.Combine(od, name + ".warn.txt"), e.GetType().Name + ": " + e.Message); }
    }
    Console.WriteLine("fix fertig");
    return;
}
if (args[0] == "--perf")
{
    // --perf <datei> [<sek>]  Zeit, Speicher
    var secs = args.Length > 2 ? int.Parse(args[2]) : 600;
    var j = MakeJob(args[1]); var sw = Stopwatch.StartNew(); var a0 = GC.GetTotalAllocatedBytes(true);
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(secs));
    try
    {
        var r = j.ImportDrawing(args[1], cts.Token);
        Console.WriteLine($"ok {sw.ElapsedMilliseconds} ms, Bahnen {r.Paths.Count}, G-Code {r.Gcode.Length} Zeichen, Meldungen {r.Warnings.Count}: {string.Join(" | ", r.Warnings).Substring(0, Math.Min(400, string.Join(" | ", r.Warnings).Length))}");
    }
    catch (Exception e) { Console.WriteLine($"{e.GetType().Name} nach {sw.ElapsedMilliseconds} ms: {e.Message[..Math.Min(400, e.Message.Length)]}"); }
    Console.WriteLine($"PeakWorkingSet {Process.GetCurrentProcess().PeakWorkingSet64 / 1048576} MB, alloziert {(GC.GetTotalAllocatedBytes(true) - a0) / 1048576} MB");
    return;
}
if (args[0] == "--lesenmem")
{
    var secs = args.Length > 2 ? int.Parse(args[2]) : 120;
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(secs));
    var swl = Stopwatch.StartNew();
    try { var rdl = Trendo.Core.Cad.CadDxf.Read(args[1], cts.Token); var tr = swl.ElapsedMilliseconds; swl.Restart(); var csl = CncCam.Contours(rdl.Drawing, ct: cts.Token);
      Console.WriteLine($"Read {tr} ms, Contours {swl.ElapsedMilliseconds} ms ({csl.Count}), Unread {rdl.Unread.Count}, Redundant {string.Join(",", rdl.Redundant.Select(k => k.Key + "=" + k.Value))}, Skipped {string.Join(",", rdl.Skipped.Select(k => k.Key + "=" + k.Value))}");
      foreach (var u in rdl.Unread.Take(3)) Console.WriteLine($"  {u.What}: {u.B}"); }
    catch (Exception e) { Console.WriteLine($"{e.GetType().Name} nach {swl.ElapsedMilliseconds} ms"); }
    Console.WriteLine($"PeakWorkingSet {Process.GetCurrentProcess().PeakWorkingSet64 / 1048576} MB");
    return;
}
if (args[0] == "--lesen")
{
    var swl = Stopwatch.StartNew(); var rdl = Trendo.Core.Cad.CadDxf.Read(args[1]); var tr = swl.ElapsedMilliseconds;
    swl.Restart(); var csl = CncCam.Contours(rdl.Drawing); Console.WriteLine($"Read {tr} ms, Contours {swl.ElapsedMilliseconds} ms, Unread {rdl.Unread.Count}, Redundant {string.Join(",", rdl.Redundant.Select(k => k.Key + "=" + k.Value))}, Skipped {string.Join(",", rdl.Skipped.Select(k => k.Key + "=" + k.Value))}");
    foreach (var u in rdl.Unread.Take(5)) Console.WriteLine($"  {u.What}: {u.B}");
    return;
}
if (args[0] == "--abbruchlesen")
{
    foreach (var delay in new[] { 1, 50, 300, 1000, 3000, 6000 })
    {
        using var cts = new CancellationTokenSource(); var sw = Stopwatch.StartNew();
        var t = Task.Run(() => Trendo.Core.Cad.CadDxf.Read(args[1], cts.Token));
        Thread.Sleep(delay); var tc = sw.ElapsedMilliseconds; cts.Cancel();
        try { t.Wait(); Console.WriteLine($"Lesen-Abbruch nach {delay} ms: war fertig nach {sw.ElapsedMilliseconds} ms"); }
        catch (AggregateException e) { Console.WriteLine($"Lesen-Abbruch nach {delay} ms: {e.InnerException?.GetType().Name} {sw.ElapsedMilliseconds - tc} ms nach dem Abbrechen"); }
    }
    return;
}
var inDir = args[0]; var outDir = args[1]; Directory.CreateDirectory(outDir);
var filter = args.Length > 2 ? args[2] : "";
foreach (var f in Directory.GetFiles(inDir).Where(f => f.EndsWith(".dxf") || f.EndsWith(".svg")).OrderBy(f => f, StringComparer.Ordinal))
{
    var name = Path.GetFileNameWithoutExtension(f); if (filter.Length > 0 && !name.Contains(filter)) continue;
    var j = MakeJob(f);
    var res = new JsonObject { ["name"] = name, ["toolD"] = j.ToolD, ["depth"] = j.Depth, ["safe"] = 5.0 };
    var sw = Stopwatch.StartNew();
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
    try
    {
        var r = j.ImportDrawing(f, cts.Token);
        sw.Stop();
        File.WriteAllText(Path.Combine(outDir, name + ".nc"), r.Gcode);
        res["ok"] = true; res["shiftX"] = r.ShiftX; res["shiftY"] = r.ShiftY;
        res["warnings"] = new JsonArray(r.Warnings.Select(w => (JsonNode)JsonValue.Create(w)!).ToArray());
        res["contours"] = new JsonArray(r.Contours.Select(c => (JsonNode)new JsonObject { ["closed"] = c.Closed, ["xy"] = new JsonArray(c.Pts.SelectMany(p => new[] { p.X, p.Y }).Select(v => (JsonNode)JsonValue.Create(v)!).ToArray()) }).ToArray());
        res["paths"] = r.Paths.Count;
    }
    catch (Exception ex) { sw.Stop(); res["ok"] = false; res["error"] = ex.GetType().Name + ": " + ex.Message; }
    res["ms"] = sw.Elapsed.TotalMilliseconds;
    File.WriteAllText(Path.Combine(outDir, name + ".json"), res.ToJsonString());
    Console.WriteLine($"{name}: {(res["ok"]!.GetValue<bool>() ? "ok" : "FEHLER " + res["error"])} {sw.Elapsed.TotalMilliseconds:0} ms");
}
