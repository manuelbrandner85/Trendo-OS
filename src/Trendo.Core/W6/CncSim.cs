using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>T-0266: Ein Stück Werkzeugweg (Bögen in kurze Stücke zerlegt) mit Quellzeile und Dauer – Grundlage für Vorschau und Simulation.</summary>
public readonly record struct CncSeg(double X0, double Y0, double Z0, double X1, double Y1, double Z1, bool Rapid, int Line, double Minutes);

/// <summary>T-0266: Werkzeugweg aus G-Code für Vorschau und Simulation (G0/G1/G2/G3, G20/G21, G90/G91, F).</summary>
public static partial class CncSim
{
    [GeneratedRegex(@"([A-Z])\s*([-+]?\d*\.?\d+)")] private static partial Regex Word();

    public static List<CncSeg> Segments(string gcode, double rapidMmMin = 3000)
    {
        var res = new List<CncSeg>();
        double x = 0, y = 0, z = 0, feed = 0; bool abs = true, inch = false; int motion = 0, lineNo = 0;
        foreach (var raw in gcode.Replace("\r", "").Split('\n'))
        {
            lineNo++;
            var l = Regex.Replace(raw, @"\(.*?\)", ""); var sc = l.IndexOf(';'); if (sc >= 0) l = l[..sc]; l = l.Trim().ToUpperInvariant();
            if (l == "" || l.StartsWith('%')) continue;
            double? nx = null, ny = null, nz = null, ci = null, cj = null, cr = null;
            foreach (Match m in Word().Matches(l))
            {
                var v = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                switch (m.Groups[1].Value[0])
                {
                    case 'G': switch ((int)Math.Round(v * 10)) { case 0: motion = 0; break; case 10: motion = 1; break; case 20: motion = 2; break; case 30: motion = 3; break; case 200: inch = true; break; case 210: inch = false; break; case 900: abs = true; break; case 910: abs = false; break; } break;
                    case 'X': nx = v; break; case 'Y': ny = v; break; case 'Z': nz = v; break; case 'I': ci = v; break; case 'J': cj = v; break; case 'R': cr = v; break;
                    case 'F': feed = inch ? v * 25.4 : v; break;
                }
            }
            if (nx is null && ny is null && nz is null) continue;
            var k = inch ? 25.4 : 1;
            double tx = nx is { } a ? (abs ? a * k : x + a * k) : x, ty = ny is { } b ? (abs ? b * k : y + b * k) : y, tz = nz is { } c ? (abs ? c * k : z + c * k) : z;
            void Add(double ax, double ay, double az, double bx, double by, double bz)
            {
                var len = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay) + (bz - az) * (bz - az));
                res.Add(new(ax, ay, az, bx, by, bz, motion == 0, lineNo, motion == 0 ? len / rapidMmMin : feed > 0 ? len / feed : 0));
            }
            if (motion is 2 or 3 && (ci.HasValue || cj.HasValue || cr.HasValue))
            {
                double cx, cy, rad;
                if (cr is { } rr)
                {
                    rad = Math.Abs(rr * k); var d = Math.Sqrt((tx - x) * (tx - x) + (ty - y) * (ty - y)); var h = Math.Sqrt(Math.Max(0, rad * rad - d * d / 4));
                    var mx = (x + tx) / 2; var my = (y + ty) / 2; var sgn = (motion == 2) == (rr > 0) ? -1 : 1;
                    cx = d < 1e-12 ? mx : mx + sgn * h * (ty - y) / d; cy = d < 1e-12 ? my : my - sgn * h * (tx - x) / d;
                }
                else { cx = x + (ci ?? 0) * k; cy = y + (cj ?? 0) * k; rad = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)); }
                double a0 = Math.Atan2(y - cy, x - cx), a1 = Math.Atan2(ty - cy, tx - cx), sweep = a1 - a0;
                if (motion == 2) { if (sweep >= -1e-9) sweep -= 2 * Math.PI; } else if (sweep <= 1e-9) sweep += 2 * Math.PI;
                var n = Math.Clamp((int)(Math.Abs(sweep) * rad / 1.0), 6, 72); double px = x, py = y, pz = z;
                for (var i = 1; i <= n; i++) { var an = a0 + sweep * i / n; var qx = i == n ? tx : cx + rad * Math.Cos(an); var qy = i == n ? ty : cy + rad * Math.Sin(an); var qz = z + (tz - z) * i / n; Add(px, py, pz, qx, qy, qz); (px, py, pz) = (qx, qy, qz); }
            }
            else Add(x, y, z, tx, ty, tz);
            x = tx; y = ty; z = tz;
        }
        return res;
    }

    /// <summary>Index des Segments, das nach <paramref name="minutes"/> Laufzeit gerade gefahren wird.</summary>
    public static int AtTime(IReadOnlyList<CncSeg> segs, double minutes)
    {
        var t = 0.0;
        for (var i = 0; i < segs.Count; i++) { t += segs[i].Minutes; if (t >= minutes) return i; }
        return segs.Count - 1;
    }

    public static string Report(string name, GcodeReport r, (double X, double Y, double Z) envelope)
    {
        var sb = new System.Text.StringBuilder($"# Prüfbericht {name}\n\nErstellt am {DateTime.Now:dd.MM.yyyy HH:mm}. Vor dem Fräsen immer an der Maschine kontrollieren.\n\n| Kennzahl | Wert |\n|---|---|\n");
        var de = CultureInfo.GetCultureInfo("de-DE");
        sb.Append($"| Dauer (geschätzt) | {(int)r.Time.TotalHours}:{r.Time.Minutes:00}:{r.Time.Seconds:00} |\n| Fräsweg | {(r.CutMm / 1000).ToString("0.00", de)} m |\n| Eilgang | {(r.RapidMm / 1000).ToString("0.00", de)} m |\n| Bewegungen | {r.Moves} |\n");
        sb.Append($"| Größe X × Y × Z | {(r.Box.X1 - r.Box.X0).ToString("0.0", de)} × {(r.Box.Y1 - r.Box.Y0).ToString("0.0", de)} × {(r.Box.Z1 - r.Box.Z0).ToString("0.0", de)} mm |\n| Arbeitsraum | {envelope.X:0} × {envelope.Y:0} × {envelope.Z:0} mm |\n");
        sb.Append($"| Werkzeuge | {(r.Tools.Count == 0 ? "–" : string.Join(", ", r.Tools.Select(t => "T" + t)))} |\n| Höchste Drehzahl | {(r.MaxSpindle > 0 ? r.MaxSpindle.ToString("0", de) + " U/min" : "–")} |\n\n## Hinweise\n\n");
        sb.Append(r.Warnings.Count == 0 ? "✓ Keine Auffälligkeiten.\n" : string.Join("\n", r.Warnings.Select(w => "- ⚠ " + w)) + "\n");
        return sb.ToString();
    }
}

/// <summary>T-0266: Dokument des Programms „CNC-Werkstatt“ – G-Code, Maschine, Generator-Werte.</summary>
public sealed class CncJob
{
    public string Gcode { get; set; } = "";
    public double EnvX { get; set; } = 300;
    public double EnvY { get; set; } = 300;
    public double EnvZ { get; set; } = 60;
    public double Rapid { get; set; } = 3000;
    public string Material { get; set; } = "Aluminium";
    public double ToolD { get; set; } = 6;
    public double Feed { get; set; } = 600;
    public double Plunge { get; set; } = 200;
    public double StepDown { get; set; } = 1;
    public double W { get; set; } = 40;
    public double H { get; set; } = 20;
    public double Depth { get; set; } = 3;
    /// <summary>T-0747: Konturen aus einer eingelesenen DXF- oder SVG-Datei (mm, y nach oben, Lage wie in der Datei) und die CAM-Einstellungen dazu.</summary>
    public List<Cad.CadContour> Contours { get; set; } = [];
    public string CamSource { get; set; } = "";
    public string CamMode { get; set; } = "auto";
    public int CamTabs { get; set; }
    public double CamTabW { get; set; } = 6;
    public double CamTabH { get; set; } = 1;

    public static CncJob From(JsonObject o)
    {
        double N(string k, double d) => o[k] is JsonValue v && v.TryGetValue<double>(out var x) ? x : d;
        return new()
        {
            Gcode = (string?)o["gcode"] ?? "", EnvX = N("envX", 300), EnvY = N("envY", 300), EnvZ = N("envZ", 60), Rapid = N("rapid", 3000), Material = (string?)o["material"] ?? "Aluminium",
            ToolD = N("toolD", 6), Feed = N("feed", 600), Plunge = N("plunge", 200), StepDown = N("stepDown", 1), W = N("w", 40), H = N("h", 20), Depth = N("depth", 3),
            Contours = o["contours"] is JsonArray ca ? ca.OfType<JsonObject>().Select(ReadContour).Where(c => c.Pts.Count >= 2).ToList() : [],
            CamSource = (string?)o["camSource"] ?? "", CamMode = (string?)o["camMode"] ?? "auto", CamTabs = (int)N("camTabs", 0), CamTabW = N("camTabW", 6), CamTabH = N("camTabH", 1),
        };
    }

    static Cad.CadContour ReadContour(JsonObject c)
    {
        var xy = c["xy"] is JsonArray a ? a.Select(v => v is JsonValue jv && jv.TryGetValue<double>(out var d) ? d : 0).ToList() : [];
        var pts = new List<(double X, double Y)>(xy.Count / 2); for (var i = 0; i + 1 < xy.Count; i += 2) pts.Add((xy[i], xy[i + 1]));
        return new Cad.CadContour(pts, c["closed"] is JsonValue v2 && v2.TryGetValue<bool>(out var b) && b, (string?)c["layer"] ?? "0");
    }

    public void WriteTo(JsonObject o)
    {
        o["gcode"] = Gcode; o["envX"] = EnvX; o["envY"] = EnvY; o["envZ"] = EnvZ; o["rapid"] = Rapid; o["material"] = Material;
        o["toolD"] = ToolD; o["feed"] = Feed; o["plunge"] = Plunge; o["stepDown"] = StepDown; o["w"] = W; o["h"] = H; o["depth"] = Depth;
        if (Contours.Count == 0) { o.Remove("contours"); o.Remove("camSource"); }
        else
        {
            o["contours"] = new JsonArray([.. Contours.Select(c => (JsonNode)new JsonObject { ["layer"] = c.Layer, ["closed"] = c.Closed, ["xy"] = new JsonArray([.. c.Pts.SelectMany(p => new[] { Math.Round(p.X, 9), Math.Round(p.Y, 9) }).Select(v => (JsonNode)JsonValue.Create(v))]) })]);
            o["camSource"] = CamSource;
        }
        o["camMode"] = CamMode; o["camTabs"] = (double)CamTabs; o["camTabW"] = CamTabW; o["camTabH"] = CamTabH; // Stegzahl als double: From liest Zahlen als double
    }

    /// <summary>T-0747: Einstellungen für Fräsbahnen aus den Konturen (Fräser, Tiefe, Zustellung, Vorschübe, Art, Haltestege; Drehzahl aus Werkstoff und Fräser).</summary>
    public CamSettings CamSettings()
    {
        double rpm = 0; try { rpm = Cnc.Feeds(Material, ToolD, 2).Rpm; } catch (ArgumentException) { }
        return new CamSettings { ToolD = ToolD, Depth = Depth, StepDown = StepDown, Feed = Feed, Plunge = Plunge, Mode = CamMode, Tabs = CamTabs, TabW = CamTabW, TabH = CamTabH, Rpm = rpm };
    }

    /// <summary>T-0747: Fräsbahnen aus den eingelesenen Konturen.</summary>
    public CamResult Cam() => Contours.Count == 0 ? throw new InvalidOperationException("Es ist noch keine Zeichnung eingelesen – unter „Erzeugen“ auf „Aus DXF/SVG …“ klicken.") : CncCam.Generate(Contours, CamSettings());

    /// <summary>T-0747: Zeichnung (DXF oder SVG) einlesen: Konturen übernehmen und die Fräsbahnen erzeugen. Rückgabe: Ergebnis mit Hinweisen.</summary>
    public CamResult ImportDrawing(string path)
    {
        var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        var drawing = ext switch
        {
            ".dxf" => Cad.CadDxf.Read(path).Drawing,
            ".svg" => Cad.CadSvg.Read(path).Drawing,
            _ => throw new Cad.CadError("Für Fräsbahnen bitte eine DXF- oder SVG-Datei wählen."),
        };
        var cs = CncCam.Contours(drawing);
        if (cs.Count == 0) throw new Cad.CadError("In der Zeichnung sind keine Konturen (Linien, Kreise, Bögen, Pfade).");
        var old = (Contours, CamSource); Contours = cs; CamSource = System.IO.Path.GetFileName(path);
        try { var r = Cam(); Gcode = r.Gcode; return r; }
        catch { (Contours, CamSource) = old; throw; }
    }

    public string Pocket() => CncPlus.Pocket(W, H, Depth, ToolD, 50, StepDown, Feed, Plunge);
    public string Outline() => CncPlus.Outline(W, H, Depth, ToolD, StepDown, Feed, Plunge);
    public string Bolt(int n = 6) => CncPlus.BoltCircle(W / 2, H / 2, Math.Min(W, H) * 0.7, n, 0, Depth, Math.Max(1, ToolD / 2), Math.Max(50, Feed / 4)).Gcode;
}
