using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace Trendo.Core.Elektro;

/// <summary>Ergebnis einer gelesenen Gerber- oder Bohrdatei: Fehler (Datei ist kaputt), Hinweise, gezählte Inhalte und Ausmaße in mm.</summary>
public sealed record GerberFileReport(string Name, string Kind, List<string> Errors, List<string> Notes, int Flashes, int Strokes, int Apertures,
    double MinX, double MinY, double MaxX, double MaxY, List<(double X, double Y, double D)> Points)
{
    public bool Ok => Errors.Count == 0;
}

public sealed record GerberPackageReport(List<GerberFileReport> Files, List<string> Errors)
{
    public bool Ok => Errors.Count == 0 && Files.All(f => f.Ok);
    public string Text => Ok
        ? $"✓ Fertigungsdaten gelesen: {Files.Count} Dateien, {Files.Sum(f => f.Flashes)} Lötflächen, {Files.Sum(f => f.Strokes)} Bahnstücke, Bohrungen passen auf die Lötflächen"
        : "⚠ Fertigungsdaten fehlerhaft: " + string.Join("; ", Errors.Concat(Files.SelectMany(f => f.Errors.Select(e => $"{f.Name}: {e}"))).Take(5));
}

/// <summary>T-0748 (SPEC-02): eigener, unabhängiger Leser für Gerber RS-274X (Ucamco-Spezifikation) und Excellon-Bohrdaten.
/// Er liest die Dateien so, wie ein Betrachter sie liest – Befehl für Befehl, mit Koordinatenformat, Einheit, Blenden und Belichtungsart –,
/// und prüft: Kopf (%FS…%, %MO…%), jede benutzte Blende ist vorher definiert, Befehle enden mit „*“, Koordinaten passen ins Format,
/// Dateiende M02, Bohrdatei mit Kopf M48 … %, Werkzeugdurchmessern und M30; über alle Dateien: alles liegt im Platinenumriss,
/// jede Bohrung trifft eine Lötfläche auf Ober- und Unterseite. Das ist eine Plausibilitätsprüfung, KEIN Fremdwerkzeug (gerbv/KiCad);
/// sie ersetzt die Sichtprüfung im Betrachter des Platinenherstellers nicht.</summary>
public static class GerberCheck
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static readonly Regex Coord = new(@"^(?:G0?[123])?(?:X(?<x>[+-]?\d+))?(?:Y(?<y>[+-]?\d+))?(?:I[+-]?\d+)?(?:J[+-]?\d+)?(?:D0?(?<d>[123]))?$", RegexOptions.Compiled);

    public static GerberFileReport ReadGerber(string name, string text)
    {
        var err = new List<string>(); var notes = new List<string>(); var pts = new List<(double, double, double)>();
        int intX = -1, decX = -1, intY = -1, decY = -1; string? unit = null; var apertures = new Dictionary<int, double>(); int? current = null;
        double x = 0, y = 0; int flashes = 0, strokes = 0; var ended = false; int mode = 1;
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        void Grow(double px, double py, double r) { minX = Math.Min(minX, px - r); minY = Math.Min(minY, py - r); maxX = Math.Max(maxX, px + r); maxY = Math.Max(maxY, py + r); }
        var i = 0; var s = text.Replace("\r", "");
        while (i < s.Length)
        {
            if (char.IsWhiteSpace(s[i])) { i++; continue; }
            if (ended) { err.Add("Inhalt nach dem Dateiende M02"); break; }
            if (s[i] == '%')
            {
                var close = s.IndexOf('%', i + 1);
                if (close < 0) { err.Add("Erweiterter Befehl ohne schließendes %"); break; }
                foreach (var raw in s[(i + 1)..close].Split('*', StringSplitOptions.RemoveEmptyEntries))
                {
                    var c = raw.Trim(); if (c.Length == 0) continue;
                    if (!s[(i + 1)..close].TrimEnd().EndsWith('*')) err.Add($"Befehl „%{c}%“ endet nicht mit *");
                    if (c.StartsWith("FS"))
                    {
                        var m = Regex.Match(c, @"^FS([LT])AX(\d)(\d)Y(\d)(\d)$");
                        if (!m.Success) { err.Add($"Koordinatenformat „{c}“ unbekannt"); continue; }
                        if (m.Groups[1].Value != "L") notes.Add("nachgestellte Nullen (veraltet)");
                        intX = int.Parse(m.Groups[2].Value); decX = int.Parse(m.Groups[3].Value); intY = int.Parse(m.Groups[4].Value); decY = int.Parse(m.Groups[5].Value);
                    }
                    else if (c.StartsWith("MO")) { unit = c[2..]; if (unit is not ("MM" or "IN")) err.Add($"Einheit „{unit}“ unbekannt"); }
                    else if (c.StartsWith("AD"))
                    {
                        var m = Regex.Match(c, @"^ADD(\d+)([CROP]|[A-Za-z_][\w.]*),?([\d.X]*)$");
                        if (!m.Success) { err.Add($"Blende „{c}“ nicht lesbar"); continue; }
                        var num = int.Parse(m.Groups[1].Value, Inv); if (num < 10) err.Add($"Blende D{num}: Nummern unter 10 sind reserviert");
                        if (apertures.ContainsKey(num)) err.Add($"Blende D{num} doppelt definiert");
                        var size = m.Groups[3].Value.Split('X', StringSplitOptions.RemoveEmptyEntries).Select(v => double.Parse(v, Inv)).DefaultIfEmpty(0).Max();
                        if (m.Groups[2].Value is "C" or "R" or "O" && size <= 0) err.Add($"Blende D{num} ohne Größe");
                        apertures[num] = size;
                    }
                    else if (c.StartsWith("LP")) { if (c is not ("LPD" or "LPC")) err.Add($"Belichtung „{c}“ unbekannt"); }
                    else if (c.StartsWith("TF") || c.StartsWith("TA") || c.StartsWith("TO") || c.StartsWith("TD") || c.StartsWith("AM") || c.StartsWith("SR") || c.StartsWith("LM") || c.StartsWith("LR") || c.StartsWith("LS")) { }
                    else err.Add($"Unbekannter erweiterter Befehl „{c}“");
                }
                i = close + 1; continue;
            }
            var star = s.IndexOf('*', i);
            if (star < 0) { err.Add("Befehl ohne abschließendes * am Dateiende"); break; }
            var cmd = s[i..star].Trim(); i = star + 1;
            if (cmd.StartsWith("G04")) continue; // Kommentar
            if (cmd == "M02") { ended = true; continue; }
            if (cmd is "G01" or "G1") { mode = 1; continue; }
            if (cmd is "G02" or "G03" or "G2" or "G3" or "G74" or "G75") { mode = 2; continue; }
            if (cmd is "G36" or "G37") continue;
            var dsel = Regex.Match(cmd, @"^(?:G54)?D(\d+)$");
            if (dsel.Success)
            {
                var n = int.Parse(dsel.Groups[1].Value, Inv);
                if (n >= 10) { if (!apertures.ContainsKey(n)) err.Add($"Blende D{n} benutzt, aber nicht definiert"); current = n; continue; }
            }
            var mc = Coord.Match(cmd);
            if (!mc.Success || cmd.Length == 0) { err.Add($"Befehl „{cmd}“ nicht lesbar"); continue; }
            if (intX < 0 || unit is null) { err.Add("Koordinaten vor %FS…% oder %MO…%"); break; }
            double Val(string digits, int ip, int dp, string axis)
            {
                var neg = digits.StartsWith('-'); var d = digits.TrimStart('+', '-');
                if (d.Length > ip + dp) err.Add($"{axis}-Koordinate {digits} hat mehr Stellen als das Format {ip}.{dp}");
                return (neg ? -1 : 1) * long.Parse(d, Inv) / Math.Pow(10, dp) * (unit == "IN" ? 25.4 : 1);
            }
            if (mc.Groups["x"].Success) x = Val(mc.Groups["x"].Value, intX, decX, "X");
            if (mc.Groups["y"].Success) y = Val(mc.Groups["y"].Value, intY, decY, "Y");
            if (!mc.Groups["d"].Success) continue;
            var dc = mc.Groups["d"].Value;
            if (dc != "2" && current is null) { err.Add("Belichtung ohne gewählte Blende"); continue; }
            var rad = current is { } cur ? apertures.GetValueOrDefault(cur) / 2 : 0;
            if (dc == "3") { flashes++; Grow(x, y, rad); pts.Add((x, y, rad * 2)); }
            else if (dc == "1") { strokes++; Grow(x, y, rad); if (mode == 2) notes.Add("Kreisbögen enthalten"); }
            else Grow(x, y, 0);
        }
        if (!ended) err.Add("Dateiende M02 fehlt");
        if (intX < 0) err.Add("Koordinatenformat %FS…% fehlt");
        if (unit is null) err.Add("Einheit %MO…% fehlt");
        if (flashes + strokes == 0) notes.Add("keine Belichtung (leere Lage)");
        if (minX == double.MaxValue) minX = minY = maxX = maxY = 0;
        return new(name, "Gerber", err, notes.Distinct().ToList(), flashes, strokes, apertures.Count, minX, minY, maxX, maxY, pts);
    }

    public static GerberFileReport ReadDrill(string name, string text)
    {
        var err = new List<string>(); var pts = new List<(double, double, double)>(); var tools = new Dictionary<int, double>();
        var lines = text.Replace("\r", "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith(';')).ToList();
        if (lines.Count == 0 || lines[0] != "M48") err.Add("Kopf M48 fehlt");
        var headerEnd = lines.FindIndex(l => l is "%" or "M95");
        if (headerEnd < 0) { err.Add("Kopfende % fehlt"); headerEnd = lines.Count; }
        var metric = false; int? tool = null; var ended = false;
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        for (var k = 1; k < lines.Count; k++)
        {
            var l = lines[k];
            if (ended) { err.Add("Inhalt nach M30"); break; }
            if (k < headerEnd)
            {
                if (l.StartsWith("METRIC")) metric = true;
                else if (l.StartsWith("INCH")) metric = false;
                else if (Regex.Match(l, @"^T(\d+)C([\d.]+)$") is { Success: true } t) { var n = int.Parse(t.Groups[1].Value, Inv); var d = double.Parse(t.Groups[2].Value, Inv); if (d <= 0) err.Add($"Werkzeug T{n} ohne Durchmesser"); tools[n] = d; }
                else if (l is "FMAT,2" or "FMAT,1" || l.StartsWith("VER")) { }
                else err.Add($"Kopfzeile „{l}“ unbekannt");
                continue;
            }
            if (k == headerEnd) continue;
            if (l is "G90" or "G05" or "M71" or "M72") continue;
            if (l == "M30") { ended = true; continue; }
            if (Regex.Match(l, @"^T(\d+)$") is { Success: true } ts)
            {
                var n = int.Parse(ts.Groups[1].Value, Inv); if (n == 0) { tool = null; continue; }
                if (!tools.ContainsKey(n)) err.Add($"Werkzeug T{n} benutzt, aber nicht im Kopf"); tool = n; continue;
            }
            var m = Regex.Match(l, @"^X([+-]?[\d.]+)Y([+-]?[\d.]+)$");
            if (!m.Success) { err.Add($"Zeile „{l}“ nicht lesbar"); continue; }
            if (!m.Groups[1].Value.Contains('.') || !m.Groups[2].Value.Contains('.')) err.Add("Koordinaten ohne Dezimalpunkt – Format nicht eindeutig");
            if (tool is null) { err.Add("Bohrung ohne gewähltes Werkzeug"); continue; }
            var f = metric ? 1 : 25.4; double px = double.Parse(m.Groups[1].Value, Inv) * f, py = double.Parse(m.Groups[2].Value, Inv) * f;
            pts.Add((px, py, tools[tool.Value] * f)); minX = Math.Min(minX, px); minY = Math.Min(minY, py); maxX = Math.Max(maxX, px); maxY = Math.Max(maxY, py);
        }
        if (!ended) err.Add("Dateiende M30 fehlt");
        if (!metric && !lines.Any(l => l.StartsWith("INCH"))) err.Add("Einheit METRIC/INCH fehlt");
        if (minX == double.MaxValue) minX = minY = maxX = maxY = 0;
        return new(name, "Excellon", err, [], pts.Count, 0, tools.Count, minX, minY, maxX, maxY, pts);
    }

    /// <summary>Ganzes Fertigungspaket (ZIP) lesen und die Lagen gegeneinander prüfen.</summary>
    public static GerberPackageReport ReadZip(string zipPath)
    {
        using var z = ZipFile.OpenRead(zipPath);
        var files = new List<GerberFileReport>();
        foreach (var e in z.Entries.OrderBy(e => e.Name, StringComparer.Ordinal))
        {
            using var r = new StreamReader(e.Open(), Encoding.UTF8); var text = r.ReadToEnd();
            files.Add(e.Name.EndsWith(".drl", StringComparison.OrdinalIgnoreCase) ? ReadDrill(e.Name, text) : ReadGerber(e.Name, text));
        }
        return CrossLayer(files);
    }

    public static GerberPackageReport CrossLayer(List<GerberFileReport> files)
    {
        var err = new List<string>();
        var edge = files.FirstOrDefault(f => f.Name.Contains("Edge_Cuts")); var drill = files.FirstOrDefault(f => f.Kind == "Excellon");
        var top = files.FirstOrDefault(f => f.Name.Contains("F_Cu")); var bottom = files.FirstOrDefault(f => f.Name.Contains("B_Cu"));
        foreach (var (f, label) in new[] { (edge, "Umriss (Edge_Cuts)"), (drill, "Bohrdatei"), (top, "Kupfer oben (F_Cu)"), (bottom, "Kupfer unten (B_Cu)") })
            if (f is null) err.Add($"{label} fehlt im Paket");
        if (edge is not null)
            foreach (var f in files.Where(f => f != edge && f.Flashes + f.Strokes > 0))
                if (f.MinX < edge.MinX - 0.01 || f.MinY < edge.MinY - 0.01 || f.MaxX > edge.MaxX + 0.01 || f.MaxY > edge.MaxY + 0.01)
                    err.Add($"{f.Name} ragt über den Platinenumriss hinaus");
        if (drill is not null)
            foreach (var cu in new[] { top, bottom }.OfType<GerberFileReport>())
            {
                foreach (var (x, y, d) in drill.Points)
                {
                    var pad = cu.Points.FirstOrDefault(p => Math.Abs(p.X - x) < 0.01 && Math.Abs(p.Y - y) < 0.01);
                    if (pad == default) { err.Add($"Bohrung bei X {x.ToString("0.###", Inv)} Y {y.ToString("0.###", Inv)} trifft keine Lötfläche in {cu.Name}"); continue; }
                    if (pad.D <= d) err.Add($"Lötfläche bei X {x.ToString("0.###", Inv)} Y {y.ToString("0.###", Inv)} ist nicht größer als die Bohrung");
                }
                if (cu.Points.Count != drill.Points.Count) err.Add($"{cu.Name}: {cu.Points.Count} Lötflächen, aber {drill.Points.Count} Bohrungen");
            }
        return new(files, err.Distinct().ToList());
    }
}
