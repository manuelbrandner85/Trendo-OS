using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Trendo.Core;
// Prüf-Harness pruef2-T-0747: ruft denselben Weg wie die Oberfläche (CncJob.ImportDrawing) und schreibt G-Code + Hinweise.
if (args[0] == "--rueck")
{
    // 1) Trendo-CAD-Rundreise mit und ohne Kennung 999
    var NL = ((char)10).ToString();
    var d = new Trendo.Core.Cad.CadDrawing();
    d.Entities.Add(new Trendo.Core.Cad.CadEntity { Kind = "linie", Layer = "Wand", Points = [[0, 0, 0], [100.125, 0, 0]] });
    d.Entities.Add(new Trendo.Core.Cad.CadEntity { Kind = "kreis", Layer = "0", Cx = 10, Cy = 20, R = 5.5 });
    d.Entities.Add(new Trendo.Core.Cad.CadEntity { Kind = "bogen", Layer = "0", Cx = 1, Cy = 2, R = 3, A0 = 10, A1 = 200 });
    d.Entities.Add(new Trendo.Core.Cad.CadEntity { Kind = "polylinie", Layer = "P", Closed = true, Points = [[0, 0, 0.5], [10, 0, 0], [10, 10, 0]] });
    d.Entities.Add(new Trendo.Core.Cad.CadEntity { Kind = "text", Layer = "T", Points = [[1, 1, 0]], Text = "Größe Ø 10 mm – „Küche“ € %%c \\U+00B2", Height = 3, Rotation = 15 });
    foreach (var l in d.Entities.Select(e => e.Layer).Distinct()) d.Layer(l);
    var w = Trendo.Core.Cad.CadDxf.Write(d);
    Console.WriteLine("Kopf: " + string.Join("|", w.Split(NL).Take(4)));
    var back = Trendo.Core.Cad.CadDxf.Parse(w).Drawing;
    var oldFile = string.Join(NL, w.Split(NL).Skip(2)); // wie vor T-0747 geschrieben (ohne 999)
    var backOld = Trendo.Core.Cad.CadDxf.Parse(oldFile).Drawing;
    string Dump(Trendo.Core.Cad.CadDrawing x) => string.Join(" ; ", x.Entities.Select(e => $"{e.Kind}/{e.Layer}/{string.Join(",", e.Points.Select(p => string.Join(":", p)))}/{e.Cx},{e.Cy},{e.R},{e.A0},{e.A1}/{e.Closed}/{e.Text}/{e.Height}/{e.Rotation}"));
    Console.WriteLine("orig : " + Dump(d)); Console.WriteLine("neu  : " + Dump(back)); Console.WriteLine("alt  : " + Dump(backOld));
    Console.WriteLine("Rundreise gleich: " + (Dump(d) == Dump(back)) + " | alte Datei gleich: " + (Dump(d) == Dump(backOld)));
    // Datei als UTF-8 und als Windows-1252 schreiben und mit Read lesen
    var tmp = Path.Combine(Path.GetTempPath(), "pruef2-T-0747-rund.dxf");
    File.WriteAllText(tmp, w); Console.WriteLine("UTF-8-Datei: " + (Dump(Trendo.Core.Cad.CadDxf.Read(tmp).Drawing) == Dump(d)));
    System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
    File.WriteAllText(tmp, oldFile, System.Text.Encoding.GetEncoding(1252)); Console.WriteLine("1252-Datei ohne Kennung: " + Dump(Trendo.Core.Cad.CadDxf.Read(tmp).Drawing).Split(" ; ").Last());
    File.Delete(tmp);
    // 2) ImageVector.ParsePath alt gegen neu
    string[] paths = ["M10 10 h20 v20 L5 30 c 5 5 10 5 15 0 Z", "M0 0 L10 0 L10 10 Z M20 20 L30 20 L30 30 Z", "M0 0 C 0 10 10 10 10 0 S 20 -10 20 0", "M 5 5 Q 10 0 15 5 T 25 5", "M0 0 A 5 5 0 0 1 10 0", "M1e1 1e-1 l+5-5", "M0 0 L10 0 L10 10 Z l5 5"];
    foreach (var pd in paths)
    {
        var a = OldPath.ParsePath(pd); var b = Trendo.Core.Media.ImageVector.ParsePath(pd);
        Console.WriteLine($"{pd,-46} alt {a.Points.Count,3} Pkt closed={a.Closed}  neu {b.Points.Count,3} Pkt closed={b.Closed}  gleich={a.Points.SequenceEqual(b.Points) && a.Closed == b.Closed}");
    }
    // eigenes SVG der Bildbearbeitung rundreisen
    var doc = Trendo.Core.Media.ImageVector.FromSvg("<svg xmlns='http://www.w3.org/2000/svg' width='200' height='100'><path d='M10 10 L50 10 L50 50 Z' stroke='#000' fill='#ff0'/><path d='M60 60 C70 80 90 80 100 60' stroke='#000'/></svg>").Doc;
    var svg = Trendo.Core.Media.ImageVector.ToSvg(doc);
    foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(svg, "d=\"([^\"]+)\""))
    {
        var a = OldPath.ParsePath(m.Groups[1].Value); var b = Trendo.Core.Media.ImageVector.ParsePath(m.Groups[1].Value);
        Console.WriteLine($"eigenes SVG: alt {a.Points.Count} neu {b.Points.Count} gleich={a.Points.SequenceEqual(b.Points) && a.Closed == b.Closed}");
    }
    return;
}
if (args[0] == "--abbruch")
{
    foreach (var delay in (Environment.GetEnvironmentVariable("ABBRUCH_MS") ?? "50,200,600").Split(",").Select(int.Parse))
    {
        var j = new CncJob { ToolD = double.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture) };
        using var cts = new CancellationTokenSource(); var sw = Stopwatch.StartNew();
        var t = Task.Run(() => j.ImportDrawing(args[1], cts.Token));
        Thread.Sleep(delay); var tc = sw.ElapsedMilliseconds; cts.Cancel();
        try { t.Wait(); Console.WriteLine($"Abbruch nach {delay} ms: war schon fertig nach {sw.ElapsedMilliseconds} ms"); }
        catch (AggregateException e) { Console.WriteLine($"Abbruch nach {delay} ms: {e.InnerException?.GetType().Name} {sw.ElapsedMilliseconds - tc} ms nach dem Abbrechen"); }
    }
    { var j = new CncJob { ToolD = double.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture) }; var sw = Stopwatch.StartNew(); j.ImportDrawing(args[1]); Console.WriteLine($"ohne Abbruch: {sw.ElapsedMilliseconds} ms"); }
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
    foreach (var delay in new[] { 200, 1000, 3000 })
    {
        using var cts = new CancellationTokenSource(); var sw = Stopwatch.StartNew();
        var t = Task.Run(() => Trendo.Core.Cad.CadDxf.Read(args[1], cts.Token));
        Thread.Sleep(delay); var tc = sw.ElapsedMilliseconds; cts.Cancel();
        try { t.Wait(); Console.WriteLine($"Lesen-Abbruch nach {delay} ms: war fertig nach {sw.ElapsedMilliseconds} ms"); }
        catch (AggregateException e) { Console.WriteLine($"Lesen-Abbruch nach {delay} ms: {e.InnerException?.GetType().Name} {sw.ElapsedMilliseconds - tc} ms nach dem Abbrechen"); }
    }
    return;
}
if (args[0] == "--konturen")
{
    var dr = Trendo.Core.Cad.CadDxf.Read(args[1]).Drawing; var cs0 = CncCam.Contours(dr);
    foreach (var c in cs0) Console.WriteLine($"geschlossen={c.Closed} n={c.Pts.Count} : " + string.Join(" ", c.Pts.Take(14).Select(p => $"({p.X:0.###};{p.Y:0.###})")));
    return;
}
if (args[0] == "--zeit")
{
    var dr = Trendo.Core.Cad.CadDxf.Read(args[1]).Drawing; var sw0 = Stopwatch.StartNew();
    var cs0 = CncCam.Contours(dr); Console.WriteLine($"Konturen {sw0.ElapsedMilliseconds} ms, {cs0.Count} Stück, Punkte {cs0.Sum(c => c.Pts.Count)}");
    foreach (var c in cs0) { sw0.Restart(); var lo = CncCam.OffsetLoops(c.Pts, 3); var t1 = sw0.ElapsedMilliseconds; sw0.Restart(); var lo2 = CncCam.OffsetLoops(c.Pts, -3); Console.WriteLine($"  OffsetLoops +3: {t1} ms ({lo.Count} Schleifen), -3: {sw0.ElapsedMilliseconds} ms ({lo2.Count})"); sw0.Restart(); CncCam.SelfIntersects(c.Pts); Console.WriteLine($"  SelfIntersects {sw0.ElapsedMilliseconds} ms"); }
    sw0.Restart(); var rr = CncCam.Generate(cs0, new CamSettings { ToolD = 6 }); Console.WriteLine($"Generate gesamt {sw0.ElapsedMilliseconds} ms");
    return;
}
var inDir = args[0]; var outDir = args[1]; Directory.CreateDirectory(outDir);
var filter = args.Length > 2 ? args[2] : "";
foreach (var f in Directory.GetFiles(inDir).Where(f => f.EndsWith(".dxf") || f.EndsWith(".svg")).OrderBy(f => f))
{
    var name = Path.GetFileNameWithoutExtension(f); if (filter.Length > 0 && !name.Contains(filter)) continue;
    var setFile = Path.Combine(inDir, name + ".set.json");
    var set = File.Exists(setFile) ? JsonNode.Parse(File.ReadAllText(setFile))!.AsObject() : new JsonObject();
    double N(string k, double def) => set[k] is JsonValue v ? v.GetValue<double>() : def;
    var j = new CncJob { ToolD = N("toolD", 6), Depth = N("depth", 3), StepDown = N("stepDown", 1), CamMode = (string?)set["mode"] ?? "auto", CamTabs = (int)N("tabs", 0), CamTabW = N("tabW", 6), CamTabH = N("tabH", 1) };
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
        if (false) res["contours"] = new JsonArray(r.Contours.Select(c => (JsonNode)new JsonObject { ["closed"] = c.Closed, ["xy"] = new JsonArray(c.Pts.SelectMany(p => new[] { p.X, p.Y }).Select(v => (JsonNode)JsonValue.Create(v)!).ToArray()) }).ToArray());
        res["paths"] = r.Paths.Count; res["pl"] = new JsonArray(r.Paths.Select(pp => { var cx = pp.Pts.Average(q => q.X) - r.ShiftX; var cy = pp.Pts.Average(q => q.Y) - r.ShiftY; var rr = pp.Pts.Average(q => Math.Sqrt(Math.Pow(q.X - r.ShiftX - cx, 2) + Math.Pow(q.Y - r.ShiftY - cy, 2))); var sd = pp.Pts.Max(q => Math.Abs(Math.Sqrt(Math.Pow(q.X - r.ShiftX - cx, 2) + Math.Pow(q.Y - r.ShiftY - cy, 2)) - rr)); return (JsonNode)new JsonObject { ["k"] = pp.Kind, ["cx"] = cx, ["cy"] = cy, ["r"] = rr, ["sd"] = sd, ["n"] = pp.Pts.Count }; }).ToArray());
    }
    catch (Exception ex) { sw.Stop(); res["ok"] = false; res["error"] = ex.GetType().Name + ": " + ex.Message; }
    res["ms"] = sw.Elapsed.TotalMilliseconds;
    File.WriteAllText(Path.Combine(outDir, name + ".json"), res.ToJsonString());
    Console.WriteLine($"{name}: {(res["ok"]!.GetValue<bool>() ? "ok" : "FEHLER " + res["error"])} {sw.Elapsed.TotalMilliseconds:0} ms");
}
