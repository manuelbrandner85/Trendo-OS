// T-0760 Regressionslauf: liest jede DXF wie Trendo (CadDxf.Read, Fräsen über CncJob.ImportDrawing, Grundriss über PlanDxfImport)
// und schreibt je Datei eine JSON-Zeile. Aufruf: Harness <dateiliste> <ausgabe.jsonl> [teil] [teile]
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Trendo.Core.Arch;
using Trendo.Core.Cad;
using Trendo.Core;

var files = File.ReadAllLines(args[0]).Where(l => l.Trim().Length > 0).ToList();
int part = args.Length > 2 ? int.Parse(args[2]) : 0, parts = args.Length > 3 ? int.Parse(args[3]) : 1; var full = args.Length > 4 && args[4] == "voll";
using var outw = new StreamWriter(args[1], false, new UTF8Encoding(false));
var inv = CultureInfo.InvariantCulture;
static string Sha(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)))[..16];
string R(double v) => double.IsFinite(v) ? Math.Round(v, 6).ToString("R", inv) : v.ToString(inv);
for (var i = part; i < files.Count; i += parts)
{
    var f = files[i]; var o = new Dictionary<string, object?> { ["file"] = f };
    var sw = System.Diagnostics.Stopwatch.StartNew();
    try
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(150));
        var rd = CadDxf.Read(f, cts.Token);
        o["kinds"] = rd.Drawing.Entities.GroupBy(e => e.Kind + (e.Source.Length > 0 ? "/" + e.Source : "")).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count());
        var geo = new StringBuilder();
        foreach (var e in rd.Drawing.Entities)
        {
            geo.Append(e.Kind).Append('|').Append(e.Layer).Append('|').Append(e.Closed).Append('|').Append(e.Invisible).Append('|').Append(R(e.Cx)).Append(',').Append(R(e.Cy)).Append(',').Append(R(e.R)).Append(',').Append(R(e.A0)).Append(',').Append(R(e.A1)).Append('|');
            foreach (var p in e.Points) geo.Append(string.Join(",", p.Select(R))).Append(';');
            geo.Append(e.Text).Append('\n');
        }
        o["geo"] = Sha(geo.ToString());
        o["skipped"] = rd.Skipped.OrderBy(k => k.Key, StringComparer.Ordinal).ToDictionary(k => k.Key, k => k.Value);
        o["unread"] = rd.Unread.Select(u => $"{u.What} [{R(u.B.MinX)},{R(u.B.MinY)},{R(u.B.MaxX)},{R(u.B.MaxY)}]").ToList();
        o["redundant"] = rd.Redundant; o["hiddenFills"] = rd.HiddenFills; o["hiddenKinds"] = rd.HiddenFillKinds; o["hiddenLayers"] = rd.HiddenFillLayers; o["hiddenUnsure"] = rd.HiddenFillsUnsure; o["units"] = $"{rd.Units}/{rd.MmPerUnit}";
        o["layers"] = string.Join(";", rd.Drawing.Layers.Select(l => $"{l.Name}:{l.Color}:{l.Visible}"));
        if (full) try
        {
            var job = new CncJob { ToolD = 3 };
            var r = job.ImportDrawing(f, cts.Token);
            o["cam"] = new Dictionary<string, object?> { ["paths"] = r.Paths.Count, ["kinds"] = r.Paths.GroupBy(p => p.Kind).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()), ["gcode"] = Sha(r.Gcode), ["warnings"] = r.Warnings };
        }
        catch (OperationCanceledException) { o["cam"] = "ZEITLIMIT"; }
        catch (Exception e) { o["cam"] = $"{e.GetType().Name}: {e.Message}"; }
        if (full) try
        {
            var rep = PlanDxfImport.Import(new FloorPlan(), rd, null, cts.Token);
            o["plan"] = $"Wände {rep.Walls}, Öffnungen {rep.Openings}, Räume {rep.Rooms}, Beschriftungen {rep.Labels}, nicht übernommen: " + string.Join(", ", rep.SkippedKinds.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => $"{k.Value}× {k.Key}"));
        }
        catch (OperationCanceledException) { o["plan"] = "ZEITLIMIT"; }
        catch (Exception e) { o["plan"] = $"{e.GetType().Name}: {e.Message}"; }
    }
    catch (OperationCanceledException) { o["read"] = "ZEITLIMIT"; }
    catch (Exception e) { o["read"] = $"{e.GetType().Name}: {e.Message}"; }
    o["ms"] = sw.ElapsedMilliseconds;
    outw.WriteLine(JsonSerializer.Serialize(o, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    outw.Flush();
}
