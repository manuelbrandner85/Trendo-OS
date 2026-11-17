using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Trendo.Core.Media;

/// <summary>Selbst nachgebaut (04.11.2026, statt Inkscape/Krita – Clean-Room): Vektorpfade, Mischmodi und SVG für die Trendo-Bildbearbeitung.
/// Pfad-Ebene („path“): Punkte, offen/geschlossen, geglättet (Catmull-Rom → Bézier), Kontur und Füllung. SVG-Ausgabe aller Ebenen,
/// SVG-Einlesen der üblichen Formen (rect, circle, ellipse, line, polyline, polygon, path mit M/L/H/V/C/Q/Z, text).</summary>
public static class ImageVector
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string F(double v) => Math.Round(v, 2).ToString(Inv);

    public static readonly (string Id, string Label)[] BlendModes = [("normal", "Normal"), ("multiply", "Multiplizieren"), ("screen", "Negativ multiplizieren"), ("overlay", "Überlagern"), ("darken", "Abdunkeln"), ("lighten", "Aufhellen"), ("difference", "Differenz")];

    /// <summary>Punkte vereinfachen (Ramer-Douglas-Peucker) – aus einem Pinselstrich wird ein sauberer Vektorpfad.</summary>
    public static List<(double X, double Y)> Simplify(IReadOnlyList<(double X, double Y)> pts, double tolerance = 1.5)
    {
        if (pts.Count < 3) return [.. pts];
        var keep = new bool[pts.Count]; keep[0] = keep[^1] = true;
        void Rdp(int a, int b)
        {
            if (b <= a + 1) return;
            var (ax, ay) = pts[a]; var (bx, by) = pts[b]; var dx = bx - ax; var dy = by - ay; var len = Math.Sqrt(dx * dx + dy * dy) + 1e-9;
            var best = -1; var bestD = 0.0;
            for (var i = a + 1; i < b; i++) { var d = Math.Abs(dy * pts[i].X - dx * pts[i].Y + bx * ay - by * ax) / len; if (d > bestD) { bestD = d; best = i; } }
            if (bestD > tolerance) { keep[best] = true; Rdp(a, best); Rdp(best, b); }
        }
        Rdp(0, pts.Count - 1);
        return pts.Where((_, i) => keep[i]).ToList();
    }

    public static JsonObject AddPath(JsonObject d, IReadOnlyList<(double X, double Y)> pts, string stroke, double width, string fill = "", bool closed = false, bool smooth = true)
    {
        if (pts.Count < 2) throw new ArgumentException("Ein Pfad braucht mindestens zwei Punkte.");
        double x0 = pts.Min(p => p.X), y0 = pts.Min(p => p.Y), x1 = pts.Max(p => p.X), y1 = pts.Max(p => p.Y);
        var arr = new JsonArray(); foreach (var (x, y) in pts) { arr.Add(Math.Round(x, 2)); arr.Add(Math.Round(y, 2)); }
        var id = "l" + Guid.NewGuid().ToString("N")[..8];
        var layer = new JsonObject { ["id"] = id, ["kind"] = "path", ["name"] = closed ? "Form" : "Pfad", ["x"] = Math.Round(x0, 1), ["y"] = Math.Round(y0, 1), ["w"] = Math.Round(Math.Max(1, x1 - x0), 1), ["h"] = Math.Round(Math.Max(1, y1 - y0), 1),
            ["ox"] = Math.Round(x0, 1), ["oy"] = Math.Round(y0, 1), ["visible"] = true, ["opacity"] = 1, ["points"] = arr, ["stroke"] = stroke, ["strokeWidth"] = width, ["fill"] = fill, ["closed"] = closed, ["smooth"] = smooth };
        return ImageDoc.Add(d, layer);
    }

    /// <summary>Punkte einer Pfad-Ebene in Dokument-Koordinaten (beim Verschieben wandern x/y mit, die Punkte sind relativ zum Ursprung gespeichert).</summary>
    public static List<(double X, double Y)> Points(JsonObject layer)
    {
        var a = (layer["points"] as JsonArray ?? []).Select(n => ImageDoc.N(n)).ToList();
        var res = new List<(double, double)>();
        for (var i = 0; i + 1 < a.Count; i += 2) res.Add((a[i], a[i + 1]));
        // Verschiebung seit dem Anlegen: Differenz zwischen gespeichertem Ursprung und Minimum der Punkte
        if (res.Count == 0) return res;
        // Ursprung beim Anlegen (ox/oy) gegen die aktuelle Lage (x/y): genau, ohne Rundungsfehler
        var dx = ImageDoc.N(layer["x"]) - ImageDoc.N(layer["ox"], res.Min(p => p.Item1)); var dy = ImageDoc.N(layer["y"]) - ImageDoc.N(layer["oy"], res.Min(p => p.Item2));
        return res.Select(p => (p.Item1 + dx, p.Item2 + dy)).ToList();
    }

    /// <summary>SVG-Pfaddaten (geglättet als kubische Bézierkurven nach Catmull-Rom).</summary>
    public static string PathData(IReadOnlyList<(double X, double Y)> p, bool closed, bool smooth)
    {
        var sb = new StringBuilder($"M{F(p[0].X)} {F(p[0].Y)}");
        if (!smooth || p.Count < 3) { for (var i = 1; i < p.Count; i++) sb.Append($" L{F(p[i].X)} {F(p[i].Y)}"); }
        else
        {
            var n = p.Count;
            (double, double) P(int i) => closed ? p[(i % n + n) % n] : p[Math.Clamp(i, 0, n - 1)];
            var segs = closed ? n : n - 1;
            for (var i = 0; i < segs; i++)
            {
                var (x0, y0) = P(i - 1); var (x1, y1) = P(i); var (x2, y2) = P(i + 1); var (x3, y3) = P(i + 2);
                sb.Append($" C{F(x1 + (x2 - x0) / 6)} {F(y1 + (y2 - y0) / 6)} {F(x2 - (x3 - x1) / 6)} {F(y2 - (y3 - y1) / 6)} {F(x2)} {F(y2)}");
            }
        }
        if (closed) sb.Append(" Z");
        return sb.ToString();
    }

    static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    /// <summary>Ganzes Dokument als SVG (Bilder werden eingebettet, Malstriche als Pfade, Text als Text).</summary>
    public static string ToSvg(JsonObject d)
    {
        double w = ImageDoc.N(d["width"], 800), h = ImageDoc.N(d["height"], 600);
        var sb = new StringBuilder($"<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" width=\"{F(w)}\" height=\"{F(h)}\" viewBox=\"0 0 {F(w)} {F(h)}\">\n");
        var bg = ImageDoc.S(d["background"], "#FFFFFF");
        if (bg != "transparent") sb.Append($"  <rect width=\"100%\" height=\"100%\" fill=\"{Esc(bg)}\"/>\n");
        foreach (var l in ImageDoc.Layers(d).OfType<JsonObject>())
        {
            if (l["visible"]?.GetValue<bool>() == false) continue;
            double x = ImageDoc.N(l["x"]), y = ImageDoc.N(l["y"]), lw = ImageDoc.N(l["w"]), lh = ImageDoc.N(l["h"]);
            var blend = ImageDoc.S(l["blend"], "normal");
            var g = $"  <g opacity=\"{F(ImageDoc.N(l["opacity"], 1))}\"{(blend != "normal" ? $" style=\"mix-blend-mode:{blend}\"" : "")}>";
            sb.Append(g);
            switch (ImageDoc.S(l["kind"]))
            {
                case "shape":
                    var fill = ImageDoc.S(l["fill"], "#2F86F6"); var st = ImageDoc.S(l["stroke"]); var sw = ImageDoc.N(l["strokeWidth"]);
                    var strokeAttr = st.Length > 0 && sw > 0 ? $" stroke=\"{Esc(st)}\" stroke-width=\"{F(sw)}\"" : "";
                    sb.Append(ImageDoc.S(l["shape"]) == "ellipse" ? $"<ellipse cx=\"{F(x + lw / 2)}\" cy=\"{F(y + lh / 2)}\" rx=\"{F(lw / 2)}\" ry=\"{F(lh / 2)}\" fill=\"{Esc(fill)}\"{strokeAttr}/>"
                        : $"<rect x=\"{F(x)}\" y=\"{F(y)}\" width=\"{F(lw)}\" height=\"{F(lh)}\" rx=\"{F(Math.Min(lw, lh) * 0.04)}\" fill=\"{Esc(fill)}\"{strokeAttr}/>");
                    break;
                case "text":
                    var size = ImageDoc.N(l["size"], 64); var lines = ImageDoc.S(l["text"]).Split('\n');
                    sb.Append($"<text font-family=\"Inter, sans-serif\" font-size=\"{F(size)}\" fill=\"{Esc(ImageDoc.S(l["color"], "#FFFFFF"))}\">");
                    for (var i = 0; i < lines.Length; i++) sb.Append($"<tspan x=\"{F(x)}\" y=\"{F(y + size + i * size * 1.2)}\">{Esc(lines[i])}</tspan>");
                    sb.Append("</text>");
                    break;
                case "path":
                {
                    var pts = Points(l); if (pts.Count < 2) break;
                    var pf = ImageDoc.S(l["fill"]); var ps = ImageDoc.S(l["stroke"], "#000000");
                    sb.Append($"<path d=\"{PathData(pts, l["closed"]?.GetValue<bool>() == true, l["smooth"]?.GetValue<bool>() != false)}\" fill=\"{(pf.Length > 0 ? Esc(pf) : "none")}\" stroke=\"{Esc(ps)}\" stroke-width=\"{F(ImageDoc.N(l["strokeWidth"], 4))}\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>");
                    break;
                }
                case "paint":
                    foreach (var s in (l["strokes"] as JsonArray ?? []).OfType<JsonObject>())
                    {
                        if (s["erase"]?.GetValue<bool>() == true) continue;
                        var a = (s["points"] as JsonArray ?? []).Select(n => ImageDoc.N(n)).ToList(); if (a.Count < 2) continue;
                        var pts = new List<(double, double)>(); for (var i = 0; i + 1 < a.Count; i += 2) pts.Add((a[i], a[i + 1]));
                        sb.Append($"<path d=\"{PathData(pts, false, false)}\" fill=\"none\" stroke=\"{Esc(ImageDoc.S(s["color"], "#000000"))}\" stroke-width=\"{F(ImageDoc.N(s["size"], 8))}\" stroke-opacity=\"{F(ImageDoc.N(s["alpha"], 1))}\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>");
                    }
                    break;
                case "image":
                    var file = ImageDoc.S(l["file"]);
                    if (File.Exists(file))
                    {
                        var mime = Path.GetExtension(file).ToLowerInvariant() is ".jpg" or ".jpeg" ? "image/jpeg" : "image/png";
                        sb.Append($"<image x=\"{F(x)}\" y=\"{F(y)}\" width=\"{F(lw)}\" height=\"{F(lh)}\" preserveAspectRatio=\"none\" xlink:href=\"data:{mime};base64,{Convert.ToBase64String(File.ReadAllBytes(file))}\"/>");
                    }
                    break;
            }
            sb.Append("</g>\n");
        }
        sb.Append("</svg>\n");
        return sb.ToString();
    }

    // ───────────── SVG einlesen ─────────────
    static double D(string? s, double fallback = 0) => double.TryParse(Regex.Match(s ?? "", @"-?[\d.]+(?:e-?\d+)?").Value, NumberStyles.Float, Inv, out var v) ? v : fallback;

    /// <summary>SVG-Datei in ein neues Trendo-Bilddokument (Formen, Pfade, Text). Unbekanntes wird übersprungen und gezählt.</summary>
    public static (JsonObject Doc, int Skipped) FromSvg(string svg)
    {
        var x = XDocument.Parse(svg, LoadOptions.None);
        var root = x.Root ?? throw new InvalidDataException("Leere SVG-Datei.");
        var vb = (root.Attribute("viewBox")?.Value ?? "").Split([' ', ','], StringSplitOptions.RemoveEmptyEntries).Select(v => D(v)).ToArray();
        double w = D(root.Attribute("width")?.Value, vb.Length == 4 ? vb[2] : 800), h = D(root.Attribute("height")?.Value, vb.Length == 4 ? vb[3] : 600);
        var d = ImageDoc.New((int)Math.Max(1, w), (int)Math.Max(1, h), "transparent");
        var skipped = 0;
        string Attr(XElement e, string name) { var v = e.Attribute(name)?.Value; if (v is not null) return v; var st = e.Attribute("style")?.Value ?? ""; var m = Regex.Match(st, name + @"\s*:\s*([^;]+)"); return m.Success ? m.Groups[1].Value.Trim() : (e.Parent is { } p && p.Name.LocalName == "g" ? Attr(p, name) : ""); }
        string Paint(string v) => v is "none" or "" ? "" : v;
        foreach (var e in root.Descendants())
        {
            switch (e.Name.LocalName)
            {
                case "rect":
                {
                    string? Wv = e.Attribute("width")?.Value, Hv = e.Attribute("height")?.Value;
                    double rx = D(e.Attribute("x")?.Value), ry = D(e.Attribute("y")?.Value), rw = Wv?.EndsWith('%') == true ? w * D(Wv) / 100 : D(Wv), rh = Hv?.EndsWith('%') == true ? h * D(Hv) / 100 : D(Hv);
                    if (rw <= 0 || rh <= 0) break;
                    if (rw >= w - 0.5 && rh >= h - 0.5 && rx == 0 && ry == 0) { d["background"] = Paint(Attr(e, "fill")) is { Length: > 0 } bg ? bg : "transparent"; break; }
                    var s = ImageDoc.AddShape(d, "rect", rx, ry, rw, rh, Paint(Attr(e, "fill")) is { Length: > 0 } f ? f : "#000000", Paint(Attr(e, "stroke")), D(Attr(e, "stroke-width")));
                    break;
                }
                case "circle" or "ellipse":
                {
                    double cx = D(e.Attribute("cx")?.Value), cy = D(e.Attribute("cy")?.Value);
                    double rxv = e.Name.LocalName == "circle" ? D(e.Attribute("r")?.Value) : D(e.Attribute("rx")?.Value), ryv = e.Name.LocalName == "circle" ? rxv : D(e.Attribute("ry")?.Value);
                    if (rxv <= 0 || ryv <= 0) break;
                    ImageDoc.AddShape(d, "ellipse", cx - rxv, cy - ryv, rxv * 2, ryv * 2, Paint(Attr(e, "fill")) is { Length: > 0 } f ? f : "#000000", Paint(Attr(e, "stroke")), D(Attr(e, "stroke-width")));
                    break;
                }
                case "line":
                    AddPath(d, [(D(e.Attribute("x1")?.Value), D(e.Attribute("y1")?.Value)), (D(e.Attribute("x2")?.Value), D(e.Attribute("y2")?.Value))], Paint(Attr(e, "stroke")) is { Length: > 0 } ls ? ls : "#000000", D(Attr(e, "stroke-width"), 1), smooth: false);
                    break;
                case "polyline" or "polygon":
                {
                    var n = Regex.Matches(e.Attribute("points")?.Value ?? "", @"-?[\d.]+(?:e-?\d+)?").Select(m => D(m.Value)).ToList();
                    var pts = new List<(double, double)>(); for (var i = 0; i + 1 < n.Count; i += 2) pts.Add((n[i], n[i + 1]));
                    if (pts.Count >= 2) AddPath(d, pts, Paint(Attr(e, "stroke")), D(Attr(e, "stroke-width"), 1), Paint(Attr(e, "fill")), e.Name.LocalName == "polygon", false);
                    break;
                }
                case "path":
                {
                    var (pts, closed) = ParsePath(e.Attribute("d")?.Value ?? "");
                    if (pts.Count >= 2) AddPath(d, pts, Paint(Attr(e, "stroke")), D(Attr(e, "stroke-width"), 1), Paint(Attr(e, "fill")), closed, false);
                    else skipped++;
                    break;
                }
                case "text":
                {
                    var txt = string.Join("\n", e.Elements().Where(t => t.Name.LocalName == "tspan").Select(t => t.Value).DefaultIfEmpty(e.Value));
                    var size = D(Attr(e, "font-size"), 24);
                    var tx = D(e.Attribute("x")?.Value ?? e.Elements().FirstOrDefault()?.Attribute("x")?.Value); var ty = D(e.Attribute("y")?.Value ?? e.Elements().FirstOrDefault()?.Attribute("y")?.Value);
                    if (txt.Trim().Length > 0) ImageDoc.AddText(d, txt.Trim(), tx, ty - size, size, Paint(Attr(e, "fill")) is { Length: > 0 } tf ? tf : "#000000");
                    break;
                }
                case "svg" or "g" or "tspan" or "title" or "desc" or "defs" or "metadata": break;
                default: skipped++; break;
            }
        }
        return (d, skipped);
    }

    /// <summary>SVG-Pfad in Punkte (Kurven werden abgetastet). Unterstützt M/L/H/V/C/S/Q/T/A/Z, groß und klein. Mehrere Teilpfade hängen aneinander –
    /// getrennt liefert sie <see cref="ParseSubpaths"/>.</summary>
    public static (List<(double X, double Y)> Points, bool Closed) ParsePath(string data)
    {
        var subs = ParseSubpaths(data);
        return (subs.SelectMany(s => s.Points).ToList(), subs.Any(s => s.Closed));
    }

    /// <summary>T-0747: SVG-Pfad als einzelne Teilpfade (jedes M beginnt einen neuen) – für Konturen in CAD und Fräsen. Bögen (A) werden richtig
    /// als Ellipsenbogen abgetastet, S/T spiegeln den letzten Kontrollpunkt, Q ist eine echte quadratische Kurve.</summary>
    public static List<(List<(double X, double Y)> Points, bool Closed)> ParseSubpaths(string data, int curveSteps = 8)
    {
        var tokens = Regex.Matches(data, @"[MmLlHhVvCcSsQqTtZzAa]|[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?").Select(m => m.Value).ToList();
        var res = new List<(List<(double X, double Y)> Points, bool Closed)>();
        List<(double X, double Y)>? cur = null; var curClosed = false;
        double cx = 0, cy = 0, sx = 0, sy = 0, lcx = 0, lcy = 0; var i = 0; var cmd = 'M'; var last = ' ';
        double Num() => double.Parse(tokens[i++], Inv);
        bool IsNum() => i < tokens.Count && !char.IsLetter(tokens[i][0]);
        void Flush() { if (cur is { Count: > 0 }) res.Add((cur, curClosed)); cur = null; curClosed = false; }
        void Add(double x, double y) { cur ??= [(cx, cy)]; cur.Add((x, y)); }
        while (i < tokens.Count)
        {
            if (char.IsLetter(tokens[i][0])) cmd = tokens[i++][0];
            else if (cmd is 'Z' or 'z') { i++; continue; } // Zahl nach Z: überspringen
            var rel = char.IsLower(cmd); var up = char.ToUpperInvariant(cmd);
            switch (up)
            {
                case 'M':
                    if (!IsNum()) break;
                    { var x = Num(); var y = Num(); if (rel) { x += cx; y += cy; } Flush(); cx = sx = x; cy = sy = y; cur = [(x, y)]; cmd = rel ? 'l' : 'L'; last = 'M'; }
                    break;
                case 'L': while (IsNum()) { var x = Num(); var y = Num(); if (rel) { x += cx; y += cy; } Add(x, y); cx = x; cy = y; } last = 'L'; break;
                case 'H': while (IsNum()) { var x = Num(); x = rel ? cx + x : x; Add(x, cy); cx = x; } last = 'L'; break;
                case 'V': while (IsNum()) { var y = Num(); y = rel ? cy + y : y; Add(cx, y); cy = y; } last = 'L'; break;
                case 'C' or 'S':
                    while (IsNum())
                    {
                        double x1, y1;
                        if (up == 'C') { x1 = Num(); y1 = Num(); if (rel) { x1 += cx; y1 += cy; } }
                        else if (last == 'C') { x1 = 2 * cx - lcx; y1 = 2 * cy - lcy; } else { x1 = cx; y1 = cy; }
                        double x2 = Num(), y2 = Num(), x = Num(), y = Num(); if (rel) { x2 += cx; y2 += cy; x += cx; y += cy; }
                        cur ??= [(cx, cy)]; Bezier(cur, (cx, cy), (x1, y1), (x2, y2), (x, y), curveSteps); lcx = x2; lcy = y2; cx = x; cy = y; last = 'C';
                    }
                    break;
                case 'Q' or 'T':
                    while (IsNum())
                    {
                        double qx, qy;
                        if (up == 'Q') { qx = Num(); qy = Num(); if (rel) { qx += cx; qy += cy; } }
                        else if (last == 'Q') { qx = 2 * cx - lcx; qy = 2 * cy - lcy; } else { qx = cx; qy = cy; }
                        double x = Num(), y = Num(); if (rel) { x += cx; y += cy; }
                        cur ??= [(cx, cy)];
                        Bezier(cur, (cx, cy), (cx + 2.0 / 3 * (qx - cx), cy + 2.0 / 3 * (qy - cy)), (x + 2.0 / 3 * (qx - x), y + 2.0 / 3 * (qy - y)), (x, y), curveSteps); // quadratisch = kubisch mit 2/3-Punkten
                        lcx = qx; lcy = qy; cx = x; cy = y; last = 'Q';
                    }
                    break;
                case 'A':
                    while (IsNum())
                    {
                        double rx = Math.Abs(Num()), ry = Math.Abs(Num()), rot = Num(); var large = Num() != 0; var sweep = Num() != 0; double x = Num(), y = Num(); if (rel) { x += cx; y += cy; }
                        cur ??= [(cx, cy)];
                        cur.AddRange(Arc(cx, cy, rx, ry, rot, large, sweep, x, y));
                        cx = x; cy = y; last = 'A';
                    }
                    break;
                case 'Z':
                    if (cur is { Count: > 0 }) { if (cur.Count > 1 && Math.Abs(cur[^1].X - sx) < 1e-9 && Math.Abs(cur[^1].Y - sy) < 1e-9) cur.RemoveAt(cur.Count - 1); curClosed = true; Flush(); }
                    cx = sx; cy = sy; last = 'Z';
                    break;
                default: i++; break;
            }
        }
        Flush();
        return res;
    }

    /// <summary>SVG-Ellipsenbogen (Endpunkt-Schreibweise) in Punkte – Umrechnung auf Mittelpunkt und Winkel nach SVG 1.1, Anhang F.6.</summary>
    static List<(double X, double Y)> Arc(double x1, double y1, double rx, double ry, double rotDeg, bool large, bool sweep, double x2, double y2)
    {
        if (rx < 1e-12 || ry < 1e-12 || Math.Abs(x1 - x2) < 1e-12 && Math.Abs(y1 - y2) < 1e-12) return [(x2, y2)];
        var phi = rotDeg * Math.PI / 180; double cos = Math.Cos(phi), sin = Math.Sin(phi);
        double dx = (x1 - x2) / 2, dy = (y1 - y2) / 2, x1p = cos * dx + sin * dy, y1p = -sin * dx + cos * dy;
        var lam = x1p * x1p / (rx * rx) + y1p * y1p / (ry * ry); if (lam > 1) { var k = Math.Sqrt(lam); rx *= k; ry *= k; }
        var num = rx * rx * ry * ry - rx * rx * y1p * y1p - ry * ry * x1p * x1p; var den = rx * rx * y1p * y1p + ry * ry * x1p * x1p;
        var co = Math.Sqrt(Math.Max(0, num / den)) * (large == sweep ? -1 : 1);
        double cxp = co * rx * y1p / ry, cyp = -co * ry * x1p / rx;
        double ccx = cos * cxp - sin * cyp + (x1 + x2) / 2, ccy = sin * cxp + cos * cyp + (y1 + y2) / 2;
        static double Ang(double ux, double uy, double vx, double vy) => Math.Atan2(ux * vy - uy * vx, ux * vx + uy * vy);
        var t1 = Ang(1, 0, (x1p - cxp) / rx, (y1p - cyp) / ry);
        var dt = Ang((x1p - cxp) / rx, (y1p - cyp) / ry, (-x1p - cxp) / rx, (-y1p - cyp) / ry);
        if (!sweep && dt > 0) dt -= 2 * Math.PI; else if (sweep && dt < 0) dt += 2 * Math.PI;
        var n = Math.Clamp((int)Math.Ceiling(Math.Abs(dt) / (Math.PI / 72)), 2, 288); // höchstens 2,5° je Stück
        var res = new List<(double X, double Y)>(n);
        for (var k = 1; k < n; k++)
        {
            var t = t1 + dt * k / n; double ex = rx * Math.Cos(t), ey = ry * Math.Sin(t);
            res.Add((cos * ex - sin * ey + ccx, sin * ex + cos * ey + ccy));
        }
        res.Add((x2, y2));
        return res;
    }

    static void Bezier(List<(double, double)> pts, (double X, double Y) p0, (double X, double Y) p1, (double X, double Y) p2, (double X, double Y) p3, int steps = 8)
    {
        for (var s = 1; s <= steps; s++)
        {
            var t = s / (double)steps; var u = 1 - t;
            pts.Add((u * u * u * p0.X + 3 * u * u * t * p1.X + 3 * u * t * t * p2.X + t * t * t * p3.X, u * u * u * p0.Y + 3 * u * u * t * p1.Y + 3 * u * t * t * p2.Y + t * t * t * p3.Y));
        }
    }
}
