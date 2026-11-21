using System.Globalization;
using System.Text;

namespace Trendo.Core.Cad;

/// <summary>T-0691: 2D-Zeichnung im CAD-Dokument (aus DXF eingelesen oder für DXF geschrieben). Maße in mm, XY-Ebene, Ebenen bleiben erhalten.</summary>
public sealed class CadDrawing
{
    public List<CadLayer> Layers { get; set; } = [];
    public List<CadEntity> Entities { get; set; } = [];
    public CadDrawing Clone() => new() { Layers = Layers.Select(l => l.Clone()).ToList(), Entities = Entities.Select(e => e.Clone()).ToList() };

    public CadLayer Layer(string name)
    {
        var l = Layers.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (l is null) Layers.Add(l = new CadLayer { Name = name });
        return l;
    }
}

public sealed class CadLayer
{
    public string Name { get; set; } = "0";
    /// <summary>AutoCAD-Farbnummer (ACI, 1–255; 7 = weiß/schwarz).</summary>
    public int Color { get; set; } = 7;
    public bool Visible { get; set; } = true;
    public CadLayer Clone() => new() { Name = Name, Color = Color, Visible = Visible };
}

/// <summary>Ein Zeichenelement: linie, polylinie, kreis, bogen oder text.</summary>
public sealed class CadEntity
{
    public string Kind { get; set; } = "linie";
    public string Layer { get; set; } = "0";
    /// <summary>Punkte (x, y, Ausbuchtung zum nächsten Punkt – DXF „bulge“, 0 = gerade). Linie: zwei Punkte.</summary>
    public List<double[]> Points { get; set; } = [];
    public bool Closed { get; set; }
    public double Cx { get; set; }
    public double Cy { get; set; }
    public double R { get; set; }
    /// <summary>Bogen: Start- und Endwinkel in Grad, gegen den Uhrzeigersinn.</summary>
    public double A0 { get; set; }
    public double A1 { get; set; }
    public string Text { get; set; } = "";
    public double Height { get; set; } = 2.5;
    public double Rotation { get; set; }
    /// <summary>T-0747-fix6: DXF-Element, aus dem der Zug abgetastet wurde (z. B. „ELLIPSE“), sonst leer. Nur beim Einlesen gesetzt, nicht gespeichert.</summary>
    public string Source { get; set; } = "";
    public CadEntity Clone() => new() { Kind = Kind, Layer = Layer, Points = Points.Select(p => (double[])p.Clone()).ToList(), Closed = Closed, Cx = Cx, Cy = Cy, R = R, A0 = A0, A1 = A1, Text = Text, Height = Height, Rotation = Rotation, Source = Source };

    /// <summary>Als Linienzug (Bögen und Ausbuchtungen in kurze Stücke zerlegt), für Anzeige und Extrudieren.</summary>
    public List<(double X, double Y)> Outline(int seg = 48)
    {
        var res = new List<(double, double)>();
        switch (Kind)
        {
            case "kreis":
                for (var i = 0; i < seg; i++) res.Add((Cx + R * Math.Cos(2 * Math.PI * i / seg), Cy + R * Math.Sin(2 * Math.PI * i / seg)));
                break;
            case "bogen":
            {
                var a0 = A0; var a1 = A1; while (a1 <= a0) a1 += 360;
                var n = Math.Max(2, (int)Math.Ceiling(seg * (a1 - a0) / 360));
                for (var i = 0; i <= n; i++) { var a = (a0 + (a1 - a0) * i / n) * Math.PI / 180; res.Add((Cx + R * Math.Cos(a), Cy + R * Math.Sin(a))); }
                break;
            }
            case "linie" or "polylinie":
                for (var i = 0; i < Points.Count; i++)
                {
                    var p = Points[i]; res.Add((p[0], p[1]));
                    var last = i == Points.Count - 1;
                    if (last && !Closed) break;
                    var q = Points[(i + 1) % Points.Count]; var bulge = p.Length > 2 ? p[2] : 0;
                    if (Math.Abs(bulge) > 1e-9) res.AddRange(BulgeArc(p[0], p[1], q[0], q[1], bulge, seg));
                }
                break;
        }
        return res;
    }

    /// <summary>Zwischenpunkte eines DXF-Bogensegments (ohne Anfang und Ende).</summary>
    static IEnumerable<(double, double)> BulgeArc(double x1, double y1, double x2, double y2, double bulge, int seg)
    {
        var theta = 4 * Math.Atan(bulge); var dx = x2 - x1; var dy = y2 - y1; var chord = Math.Sqrt(dx * dx + dy * dy);
        if (chord < 1e-12) yield break;
        var r = chord / (2 * Math.Sin(Math.Abs(theta) / 2));
        var mx = (x1 + x2) / 2; var my = (y1 + y2) / 2;
        var h = r * Math.Cos(theta / 2) * Math.Sign(bulge); // Abstand Sehnenmitte → Mittelpunkt
        var cx = mx - dy / chord * h; var cy = my + dx / chord * h;
        var a1 = Math.Atan2(y1 - cy, x1 - cx);
        var n = Math.Max(1, (int)Math.Ceiling(seg * Math.Abs(theta) / (2 * Math.PI)));
        for (var i = 1; i < n; i++) { var a = a1 + theta * i / n; yield return (cx + r * Math.Cos(a), cy + r * Math.Sin(a)); }
    }

    public bool IsClosedContour => Kind == "kreis" || Kind == "polylinie" && Closed && Points.Count >= 3;
}

/// <summary>T-0691: DXF lesen und schreiben (ASCII, Gruppencode-Paare). Gelesen werden LINE, LWPOLYLINE, POLYLINE/VERTEX, CIRCLE, ARC, TEXT, MTEXT
/// samt Ebenentabelle; anderes (Bemaßung, Schraffur, Blöcke, Splines) wird gezählt und gemeldet, nicht still verworfen.</summary>
public static class CadDxf
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Ergebnis des Lesens. T-0747: <see cref="Units"/> ist der DXF-Kopfwert $INSUNITS (0 = ohne Angabe), <see cref="MmPerUnit"/> der Faktor,
    /// mit dem alle Maße bereits in Millimeter umgerechnet wurden (die Zeichnung ist immer in mm).</summary>
    public sealed record ReadResult(CadDrawing Drawing, Dictionary<string, int> Skipped)
    {
        /// <summary>T-0747-fix5: Ausdehnung (mm) nicht gelesener Elemente mit Fläche (Blockverweise, SPLINE, Elemente außerhalb der Zeichenebene). Was darin liegt,
        /// lässt sich nicht sicher als Loch oder Teil deuten. <see cref="Box.All"/> = Ausdehnung unbekannt.</summary>
        public List<(string What, Box B)> Unread { get; init; } = [];
        /// <summary>T-0747-fix8: Füllflächen, deren Rand ganz formgleich gezeichnet ist – sie sind in <see cref="Skipped"/> gezählt, fehlen beim Fräsen aber nicht.</summary>
        public Dictionary<string, int> Redundant { get; init; } = [];
        /// <summary>T-0747-fix9: Füllflächen auf ausgeblendeten Ebenen („Schraffur auf Ebene AUS“ → Anzahl) – werden gemeldet.</summary>
        public Dictionary<string, int> HiddenFills { get; init; } = [];
        /// <summary>T-0747-fix10: dieselben Füllflächen nach DXF-Art (zum Abziehen von „Nicht gelesen“) und je Ebene nach Bezeichnung (für „Ausgeblendete Ebenen“).</summary>
        public Dictionary<string, int> HiddenFillKinds { get; init; } = [];
        public Dictionary<string, Dictionary<string, int>> HiddenFillLayers { get; init; } = [];
        public int Units { get; init; }
        public double MmPerUnit { get; init; } = 1;
        public string UnitName => UnitLabel(Units);
    }

    /// <summary>T-0747: Millimeter je Zeichnungseinheit für $INSUNITS (AutoCAD-Tabelle). Ohne Angabe (0) gilt mm, wie in Trendo CAD üblich.</summary>
    public static double MmPerUnitOf(int insunits) => insunits switch
    {
        1 => 25.4, 2 => 304.8, 3 => 1_609_344, 4 => 1, 5 => 10, 6 => 1000, 7 => 1_000_000, 8 => 25.4e-6, 9 => 0.0254, 10 => 914.4,
        11 => 1e-7, 12 => 1e-6, 13 => 1e-3, 14 => 100, 15 => 10_000, 16 => 100_000, 17 => 1e12, 18 => 1.495978707e14, 19 => 9.4607304725808e18, 20 => 3.0856775814913673e19,
        _ => 1,
    };

    public static string UnitLabel(int insunits) => insunits switch
    {
        0 => "ohne Angabe (als mm gelesen)", 1 => "Zoll", 2 => "Fuß", 4 => "mm", 5 => "cm", 6 => "m", 9 => "mil", 10 => "Yard", 14 => "dm", _ => $"Einheit {insunits}",
    };

    /// <summary>T-0747-fix: Kennung in Trendos eigenen DXF-Dateien (DXF-Kommentar 999 am Dateianfang, wie andere CAD-Programme ihren Namen eintragen).
    /// Texte aus solchen Dateien bleiben wörtlich – die DXF-Sonderzeichen (%%c, \U+…) werden nur in fremden Dateien umgewandelt.</summary>
    public const string OwnMarker = "Trendo OS";

    public static ReadResult Read(string path, CancellationToken ct = default)
    {
        if (new FileInfo(path).Length > 64L * 1024 * 1024) throw new CadError("Die DXF-Datei ist zu groß (über 64 MB).");
        var text = Decode(File.ReadAllBytes(path));
        if (text.StartsWith("AutoCAD Binary DXF", StringComparison.Ordinal)) throw new CadError("Binäre DXF-Dateien liest Trendo nicht – bitte als ASCII-DXF speichern.");
        return Parse(text, ct);
    }

    /// <summary>T-0747: Text der Datei – UTF-8 (ab AutoCAD 2007 und aus vielen Programmen, auch Trendos eigener Export), UTF-16 mit Kennung,
    /// sonst Windows-1252 (T-0747-fix: statt Latin-1, damit €, „ “ und – aus älteren deutschen DXF-Dateien richtig ankommen).</summary>
    static string Decode(byte[] bytes) => Sys.TextFileCodec.Decode(bytes).Text;

    /// <summary>T-0747: DXF-Sonderzeichen in Texten: \U+00B2 → ², %%c → Ø, %%d → °, %%p → ±.</summary>
    static string Unescape(string s) => System.Text.RegularExpressions.Regex.Replace(s, @"\\U\+([0-9A-Fa-f]{4})", m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString())
        .Replace("%%c", "Ø", StringComparison.OrdinalIgnoreCase).Replace("%%d", "°", StringComparison.OrdinalIgnoreCase).Replace("%%p", "±", StringComparison.OrdinalIgnoreCase);

    public static ReadResult Parse(string text, CancellationToken ct = default)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var pairs = new List<(int Code, string Value)>(lines.Length / 2);
        for (var i = 0; i + 1 < lines.Length; i += 2)
        {
            if (!int.TryParse(lines[i].Trim(), NumberStyles.Integer, Inv, out var code)) throw new CadError($"Zeile {i + 1}: „{lines[i].Trim()}“ ist kein DXF-Gruppencode.");
            pairs.Add((code, lines[i + 1].TrimEnd()));
        }
        var d = new CadDrawing(); var skipped = new Dictionary<string, int>();
        var own = pairs.Count > 0 && pairs[0].Code == 999 && pairs[0].Value.Trim().StartsWith(OwnMarker, StringComparison.Ordinal); // T-0747-fix: Trendos eigene Datei
        string Txt(string s) => own ? s : Unescape(s);
        pairs.RemoveAll(x => x.Code == 999); // T-0747-fix5: Kommentare überall überspringen (z. B. zwischen $INSUNITS und dem Wert)
        string section = ""; var p = 0; var units = 0; var kUnits = 1.0;
        string? curBlock = null; var blocks = new Dictionary<string, BlockInfo>(StringComparer.OrdinalIgnoreCase); var unread = new List<(string What, Box B)>();
        var pendingInserts = new List<(int Index, List<(int Code, string Value)> Body)>(); // T-0747-fix6: Ausdehnung erst am Ende (Blöcke dürfen nach ENTITIES stehen)
        long ellipsePoints = 0;
        var fills = new List<(int Index, List<(double X, double Y)>? Shape, string Layer, int Serial, bool Ambiguous)>(); // T-0747-fix7: Füllflächen-Schleifen (fix8: alle, Form wo bestimmbar)
        var fillKinds = new List<string>(); var fillCircles = new Dictionary<int, (double Cx, double Cy, double R)>(); var redundant = new Dictionary<string, int>(); // T-0747-fix8: je Füllfläche ihre Art – ganz formgleich zählt sie nicht als „fehlt in den Fräsbahnen“
        Box BlockBox(string name, int depth)
        {
            if (depth > 16) return Box.All; // zu tief (oder im Kreis) verschachtelt → Ausdehnung unbekannt, überall unsicher
            if (!blocks.TryGetValue(name, out var bi)) return Box.Empty; // Block gibt es nicht: es wird nichts gezeichnet
            var box = bi.Box;
            foreach (var ins in bi.Inserts) box = box.Union(InsertBox(ins, depth + 1));
            return box;
        }
        Box InsertBox(List<(int Code, string Value)> ib, int depth)
        {
            var name = Str(ib, 2) ?? ""; var bb = BlockBox(name, depth);
            if (bb.IsEmpty || bb.IsAll) return bb;
            var bi = blocks[name];
            double sx = Num(ib, 41, 1), sy = Num(ib, 42, 1), rot = Num(ib, 50) * Math.PI / 180, ix = Num(ib, 10), iy = Num(ib, 20), cs = Num(ib, 44), rs = Num(ib, 45);
            int cols = Math.Max(1, Int(ib, 70, 1)), rows = Math.Max(1, Int(ib, 71, 1));
            var res = Box.Empty;
            foreach (var x in new[] { bb.MinX, bb.MaxX }) foreach (var y in new[] { bb.MinY, bb.MaxY })
                foreach (var c in new[] { 0, cols - 1 }) foreach (var r in new[] { 0, rows - 1 })
                {
                    double lx = (x - bi.BaseX) * sx + c * cs, ly = (y - bi.BaseY) * sy + r * rs;
                    res = res.With((ix + lx * Math.Cos(rot) - ly * Math.Sin(rot), iy + lx * Math.Sin(rot) + ly * Math.Cos(rot)));
                }
            return ApplyNormal(ib, res);
        }
        while (p < pairs.Count)
        {
            if ((p & 4095) == 0) ct.ThrowIfCancellationRequested();
            var (code, val) = pairs[p];
            if (code == 0 && val == "SECTION") { section = p + 1 < pairs.Count && pairs[p + 1].Code == 2 ? pairs[p + 1].Value.Trim() : ""; p += 2; continue; }
            if (code == 0 && val == "ENDSEC") { section = ""; p++; continue; }
            if (code == 0 && val == "EOF") break;
            if (section == "HEADER" && code == 9 && val.Trim() == "$INSUNITS")
            {
                if (p + 1 < pairs.Count && pairs[p + 1].Code == 70 && int.TryParse(pairs[p + 1].Value.Trim(), NumberStyles.Integer, Inv, out var u)) { units = u; kUnits = MmPerUnitOf(u); }
                p += 2; continue;
            }
            if (code != 0) { p++; continue; }
            var start = p; p++;
            var body = new List<(int Code, string Value)>();
            while (p < pairs.Count && pairs[p].Code != 0) body.Add(pairs[p++]);
            if (section == "TABLES" && val == "LAYER")
            {
                var name = Str(body, 2);
                if (name is { Length: > 0 }) { var l = d.Layer(name); var c = Int(body, 62, 7); l.Color = Math.Abs(c) is >= 1 and <= 255 ? Math.Abs(c) : 7; l.Visible = c >= 0 && (Int(body, 70, 0) & 1) == 0; } // T-0747-fix4: ausgeschaltet (Farbe < 0) oder gefroren (Flag 1) = unsichtbar
                continue;
            }
            if (section != "ENTITIES" && section != "BLOCKS") continue;
            // T-0747-fix5: unsinnige Zahlen (NaN, unendlich, |Wert| > 1e9 mm) – nur dieses Element verwerfen und melden
            if (val is not ("BLOCK" or "ENDBLK" or "SEQEND") && !SaneNumbers(body, kUnits))
            {
                Count(skipped, $"{val} mit ungültiger Koordinate");
                // T-0747-fix6: wo das Element liegt, ist unbekannt – kann es einen Umriss bilden, gilt die ganze Zeichnung als unsicher
                if (section == "BLOCKS") { if (curBlock is not null && blocks.TryGetValue(curBlock, out var bx) && !NoOutline.Contains(val)) bx.Add(Box.All); }
                else if (OutlineCapable(val, body)) unread.Add(($"{val} mit ungültiger Koordinate", Box.All));
                if (val == "POLYLINE") SkipVertices(pairs, ref p);
                continue;
            }
            if (section == "BLOCKS")
            {
                // T-0747-fix5: Blockinhalt nicht übernehmen, aber seine Ausdehnung merken (für INSERT: wo liegt nicht Gelesenes?)
                if (val == "BLOCK")
                {
                    curBlock = Str(body, 2) ?? ""; blocks[curBlock] = new BlockInfo(Num(body, 10), Num(body, 20));
                    if ((Int(body, 70, 0) & (4 | 8)) != 0) blocks[curBlock].Add(Box.All); // T-0747-fix6: Xref – Inhalt steht in einer anderen Datei, Ausdehnung unbekannt
                    continue;
                }
                if (val == "ENDBLK") { curBlock = null; continue; }
                Count(skipped, "Element in Blockdefinition");
                if (curBlock is not null && blocks.TryGetValue(curBlock, out var bi))
                {
                    if (val == "INSERT") bi.Inserts.Add(body);
                    else if (val == "POLYLINE") bi.Add(ShapeBox(val, body, ReadVertexBodies(pairs, ref p), kUnits)); // T-0747-fix6: mit Ausbuchtungen und Normale
                    else bi.Add(ShapeBox(val, body, [], kUnits));
                }
                else if (val == "POLYLINE") SkipVertices(pairs, ref p);
                continue;
            }
            var layer = Str(body, 8) is { Length: > 0 } ln ? ln : "0";
            // T-0747-fix4: Extrusionsrichtung (210/220/230). CIRCLE, ARC, LWPOLYLINE und 2D-POLYLINE liegen im Objekt-Koordinatensystem; bei der
            // Normale (0,0,−1) ist Welt-x = −x (gespiegelt, Bogenrichtung umgekehrt). Schräge Normalen liegen nicht in der Zeichenebene → nicht gelesen, gemeldet.
            // T-0747-fix5: 3D-Polylinien (Flag 8) und Netze (16/64) liegen in Weltkoordinaten wie LINE – dort gilt die Normale nicht.
            var flip = false;
            var worldPoly = val == "POLYLINE" && (Int(body, 70, 0) & (8 | 16 | 64)) != 0;
            if (val is "CIRCLE" or "ARC" or "LWPOLYLINE" or "POLYLINE" or "ELLIPSE" && !worldPoly && (Has(body, 210) || Has(body, 220) || Has(body, 230)))
            {
                double ex = Num(body, 210), ey = Num(body, 220), ez = Num(body, 230, 1), el = Math.Sqrt(ex * ex + ey * ey + ez * ez);
                if (el > 1e-12) { ex /= el; ey /= el; ez /= el; }
                if (el <= 1e-12 || Math.Abs(ex) > 1e-9 || Math.Abs(ey) > 1e-9)
                {
                    Count(skipped, $"{val} nicht in der Zeichenebene");
                    var vbs = val == "POLYLINE" ? ReadVertexBodies(pairs, ref p) : [];
                    unread.Add(($"{val} (nicht in der Zeichenebene)", ShapeBox(val, body, vbs, kUnits)));
                    continue;
                }
                flip = ez < 0;
            }
            var before = d.Entities.Count;
            switch (val)
            {
                case "LINE":
                    d.Entities.Add(new CadEntity { Kind = "linie", Layer = layer, Points = [[Num(body, 10), Num(body, 20), 0], [Num(body, 11), Num(body, 21), 0]] });
                    break;
                case "LWPOLYLINE":
                {
                    var e = new CadEntity { Kind = "polylinie", Layer = layer, Closed = (Int(body, 70, 0) & 1) == 1 };
                    double[]? cur = null;
                    foreach (var (c, v) in body)
                    {
                        if (c == 10) { cur = [D(v), 0, 0]; e.Points.Add(cur); }
                        else if (c == 20 && cur is not null) cur[1] = D(v);
                        else if (c == 42 && cur is not null) cur[2] = D(v);
                    }
                    d.Entities.Add(e);
                    break;
                }
                case "POLYLINE":
                {
                    var flags = Int(body, 70, 0);
                    if ((flags & (16 | 64)) != 0) { Count(skipped, "3D-Netz (POLYLINE)"); SkipVertices(pairs, ref p); break; }
                    var e = new CadEntity { Kind = "polylinie", Layer = layer, Closed = (flags & 1) == 1 };
                    var bad = false;
                    foreach (var vb in ReadVertexBodies(pairs, ref p))
                    {
                        if (!SaneNumbers(vb, kUnits)) { bad = true; continue; }
                        // T-0747-fix5: PEDIT-Spline (Flag 4): Rahmenpunkte (Vertex-Flag 16) sind unsichtbar – nur die Kurvenpunkte zählen
                        if ((flags & 4) != 0 && (Int(vb, 70, 0) & 16) != 0) continue;
                        e.Points.Add([Num(vb, 10), Num(vb, 20), (flags & 8) != 0 ? 0 : Num(vb, 42)]);
                    }
                    if (bad) { Count(skipped, "POLYLINE mit ungültiger Koordinate"); if ((flags & 1) != 0) unread.Add(("POLYLINE mit ungültiger Koordinate", Box.All)); break; }
                    d.Entities.Add(e);
                    break;
                }
                case "CIRCLE":
                    if (!(Num(body, 40) > 0))
                    {
                        Count(skipped, "CIRCLE mit Radius ≤ 0"); // T-0747-fix5
                        if (Num(body, 40) < 0) unread.Add(("CIRCLE mit Radius ≤ 0", ShapeBox(val, body, [], kUnits))); // T-0747-fix6: könnte ein Umriss mit |r| sein
                        break;
                    }
                    d.Entities.Add(new CadEntity { Kind = "kreis", Layer = layer, Cx = Num(body, 10), Cy = Num(body, 20), R = Num(body, 40) });
                    break;
                case "ARC":
                    if (!(Num(body, 40) > 0)) { Count(skipped, "ARC mit Radius ≤ 0"); break; } // T-0747-fix5
                    d.Entities.Add(new CadEntity { Kind = "bogen", Layer = layer, Cx = Num(body, 10), Cy = Num(body, 20), R = Num(body, 40), A0 = Num(body, 50), A1 = Num(body, 51) });
                    break;
                case "ELLIPSE":
                {
                    // T-0747-fix5: Ellipse abtasten (Weltkoordinaten; Nebenachse = Normale × Hauptachse · Verhältnis).
                    // T-0747-fix6: Parameter per Rest normalisiert (keine Schleife, die von der Eingabe abhängt), Beträge > 1e6 verworfen; Verhältnis > 1 richtig
                    // abgetastet; Schrittweite aus |r''(t)| ≤ max(a, b): Pfeilhöhe ≤ h²/8 · max(a, b) ≤ 0,001 mm.
                    double cx = Num(body, 10), cy = Num(body, 20), ax = Num(body, 11), ay = Num(body, 21), ratio = Math.Abs(Num(body, 40, 1)), t0r = Num(body, 41), t1r = Num(body, 42, 2 * Math.PI);
                    var a = Math.Sqrt(ax * ax + ay * ay); var b = a * ratio;
                    if (!(a > 0) || !(b > 0)) { Count(skipped, "ELLIPSE ohne Ausdehnung"); break; }
                    if (Math.Abs(t0r) > 1e6 || Math.Abs(t1r) > 1e6)
                    {
                        Count(skipped, "ELLIPSE mit unsinnigem Parameter"); unread.Add(("ELLIPSE (unsinniger Parameter)", ShapeBox(val, body, [], kUnits))); flip = false; break;
                    }
                    var sweep = (t1r - t0r) % (2 * Math.PI); if (sweep < 0) sweep += 2 * Math.PI;
                    var full = sweep < 1e-9 || sweep > 2 * Math.PI - 1e-9; if (full) sweep = 2 * Math.PI;
                    var t0 = t0r % (2 * Math.PI);
                    var sgn = flip ? -1 : 1; // Normale −Z: Nebenachse zeigt in die andere Richtung
                    double bx = -ay * ratio * sgn, by = ax * ratio * sgn;
                    var dt = Math.Sqrt(8 * (0.001 / kUnits) / Math.Max(a, b));
                    var nD = Math.Ceiling(sweep / dt);
                    if (!(nD <= MaxEllipsePoints) || ellipsePoints + nD > MaxEllipsePointsTotal)
                    {
                        Count(skipped, "ELLIPSE zu groß zum Abtasten"); unread.Add(("ELLIPSE (zu groß zum Abtasten)", ShapeBox(val, body, [], kUnits))); flip = false; break;
                    }
                    // Scheitel (Vielfache von π/2) sind immer Stützpunkte: sonst lägen bei sehr flachen Ellipsen zwei Punkte beidseits der Spitze näher als
                    // 0,001 mm beieinander, würden beim Verketten zusammengefasst, und die Spitze würde abgeschnitten.
                    if (full) t0 = 0;
                    var e = new CadEntity { Kind = "polylinie", Layer = layer, Closed = full, Source = "ELLIPSE" };
                    void At(double t) => e.Points.Add([cx + Math.Cos(t) * ax + Math.Sin(t) * bx, cy + Math.Cos(t) * ay + Math.Sin(t) * by, 0]);
                    var q = Math.PI / 2; var cut = new List<double> { t0 };
                    for (var kq = Math.Floor(t0 / q) + 1; kq * q < t0 + sweep - 1e-12 && cut.Count < 6; kq++) cut.Add(kq * q);
                    cut.Add(t0 + sweep);
                    for (var j = 0; j + 1 < cut.Count; j++)
                    {
                        var len = cut[j + 1] - cut[j]; var m = (int)Math.Max(1, Math.Max(Math.Ceiling(len / dt), Math.Ceiling(len / (Math.PI / 32))));
                        for (var i = 0; i < m; i++) At(cut[j] + len * i / m);
                    }
                    if (!full) At(t0 + sweep);
                    ellipsePoints += e.Points.Count;
                    d.Entities.Add(e);
                    flip = false; // schon in Weltkoordinaten gerechnet
                    break;
                }
                case "INSERT":
                    Count(skipped, val);
                    unread.Add(($"Blockverweis „{Str(body, 2)}“", Box.Empty)); pendingInserts.Add((unread.Count - 1, body));
                    break;
                case "SPLINE":
                    Count(skipped, val);
                    unread.Add(("SPLINE", ShapeBox(val, body, [], kUnits)));
                    break;
                case "TEXT":
                {
                    // ausgerichteter Text: Ausrichtungspunkt (11/21) gilt, sobald eine Ausrichtung gesetzt ist
                    var aligned = Int(body, 72, 0) != 0 || Int(body, 73, 0) != 0;
                    var x = aligned && Has(body, 11) ? Num(body, 11) : Num(body, 10); var y = aligned && Has(body, 21) ? Num(body, 21) : Num(body, 20);
                    d.Entities.Add(new CadEntity { Kind = "text", Layer = layer, Points = [[x, y, 0]], Text = Txt(Str(body, 1) ?? ""), Height = Num(body, 40, 2.5), Rotation = Num(body, 50) });
                    break;
                }
                case "MTEXT":
                {
                    var t = string.Concat(body.Where(b => b.Code == 3).Select(b => b.Value)) + (Str(body, 1) ?? "");
                    t = System.Text.RegularExpressions.Regex.Replace(Txt(t).Replace("\\P", "\n"), @"\\[A-Za-z][^;\\]*;|[{}]", "");
                    d.Entities.Add(new CadEntity { Kind = "text", Layer = layer, Points = [[Num(body, 10), Num(body, 20), 0]], Text = t, Height = Num(body, 40, 2.5), Rotation = Num(body, 50) });
                    break;
                }
                case "SEQEND" or "VERTEX": break;
                default:
                    Count(skipped, val);
                    // T-0747-fix6: jedes nicht gelesene Element, das einen Umriss bilden kann (MLINE, REGION, HATCH, SOLID …), mit seiner (vorsichtigen) Ausdehnung
                    if (IsFill(val))
                    {
                        // T-0747-fix7: je Randschleife ein Eintrag (Inseln einzeln); Schleifen mit bestimmbarer Form werden am Ende mit den gezeichneten Konturen verglichen
                        var loops = FillLoops(val, body, kUnits);
                        if (loops is null) unread.Add((KindLabel(val), Box.All)); // T-0747-fix10: deutsche Bezeichnung (Schraffur, Abdeckfläche …)
                        else
                        {
                            var serial = fillKinds.Count; fillKinds.Add(val);
                            foreach (var fl in loops)
                            {
                                var fb = fl.B.IsEmpty || fl.B.HasNaN ? Box.All : fl.B; // T-0747-fix8: nie still verwerfen
                                unread.Add((KindLabel(val), fb));
                                fills.Add((unread.Count - 1, fb.IsAll ? null : fl.Shape is { Count: >= 3 } ? fl.Shape : null, layer, serial, fl.Ambiguous));
                                if (!fb.IsAll && fl.Circle is { } fc) fillCircles[unread.Count - 1] = fc;
                            }
                        }
                    }
                    else if (!NoOutline.Contains(val)) unread.Add((val, ShapeBox(val, body, [], kUnits)));
                    break;
            }
            if (flip) for (var i = before; i < d.Entities.Count; i++) Mirror(d.Entities[i]); // T-0747-fix4
            _ = start;
        }
        foreach (var (idx, ib) in pendingInserts) unread[idx] = (unread[idx].What, InsertBox(ib, 0));
        foreach (var name in d.Entities.Select(e => e.Layer).Distinct().ToList()) d.Layer(name);
        var hiddenFills = new Dictionary<string, int>(); var hiddenKinds = new Dictionary<string, int>(); var hiddenLayers = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        if (fills.Count > 0)
        {
            // T-0747-fix7/9: formgleich (je Achse ± 0,01 mm) gezeichnete Schleifen fallen weg; ausgeblendete nur, wenn ganz in einer gezeichneten Kontur
            var (drop, same) = MatchFills(d, fills, unread, kUnits, ct, fillCircles);
            foreach (var grp in fills.GroupBy(f => f.Serial).Where(g => g.All(f => same.Contains(f.Index)))) // nur echte Formgleichheit zählt als „schon gezeichnet“
            {
                var kind = fillKinds[grp.Key]; redundant[kind] = redundant.GetValueOrDefault(kind) + 1;
            }
            var hiddenSet = d.Layers.Where(l => !l.Visible).Select(l => l.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var grp in fills.GroupBy(f => f.Serial).Where(g => hiddenSet.Contains(g.First().Layer)))
            {
                var key = $"{KindLabel(fillKinds[grp.Key])} auf Ebene {grp.First().Layer}"; hiddenFills[key] = hiddenFills.GetValueOrDefault(key) + 1;
                if (grp.All(f => same.Contains(f.Index))) continue; // schon als formgleich gezählt
                // T-0747-fix10: für die Meldung je Ebene (statt doppelt unter „Nicht gelesen“)
                var kind = fillKinds[grp.Key]; hiddenKinds[kind] = hiddenKinds.GetValueOrDefault(kind) + 1;
                var layer = grp.First().Layer; if (!hiddenLayers.TryGetValue(layer, out var byKind)) hiddenLayers[layer] = byKind = [];
                byKind[KindLabel(kind)] = byKind.GetValueOrDefault(KindLabel(kind)) + 1;
            }
            unread = unread.Where((_, j) => !drop.Contains(j)).ToList();
        }
        if (d.Entities.Count == 0 && skipped.Count == 0) throw new CadError("In der DXF-Datei ist keine Zeichnung (kein ENTITIES-Abschnitt mit Linien, Kreisen, Bögen oder Text).");
        var k = MmPerUnitOf(units);
        if (Math.Abs(k - 1) > 1e-12) Scale(d, k); // T-0747: Zoll, cm, m … gleich in mm
        return new ReadResult(d, skipped) { Units = units, MmPerUnit = k, Unread = unread.Select(x => (x.What, x.B.ToSafe().Scale(k))).ToList(), Redundant = redundant, HiddenFills = hiddenFills, HiddenFillKinds = hiddenKinds, HiddenFillLayers = hiddenLayers };

        static void Count(Dictionary<string, int> s, string k) => s[k] = s.GetValueOrDefault(k) + 1;
        // Normale (0,0,−1): x spiegeln; Bögen laufen danach in Weltrichtung wieder gegen den Uhrzeigersinn (Winkel 180−a1 … 180−a0), Ausbuchtungen umgekehrt
        static void Mirror(CadEntity e)
        {
            e.Cx = -e.Cx;
            if (e.Kind == "bogen") { var (a0, a1) = (e.A0, e.A1); e.A0 = Norm(180 - a1); e.A1 = Norm(180 - a0); }
            foreach (var pt in e.Points) { pt[0] = -pt[0]; if (pt.Length > 2) pt[2] = -pt[2]; }
        }
        static double Norm(double a) { a %= 360; return a < 0 ? a + 360 : a; }
        static void SkipVertices(List<(int Code, string Value)> pairs, ref int p)
        {
            while (p < pairs.Count && !(pairs[p].Code == 0 && pairs[p].Value == "SEQEND")) p++;
            if (p < pairs.Count) { p++; while (p < pairs.Count && pairs[p].Code != 0) p++; }
        }
    }

    /// <summary>Alle Maße mit <paramref name="k"/> multiplizieren (Ausbuchtungen und Winkel bleiben).</summary>
    /// <summary>T-0747-fix5: achsparalleles Rechteck (leer oder „überall“ möglich).</summary>
    public readonly record struct Box(double MinX, double MinY, double MaxX, double MaxY)
    {
        public static readonly Box Empty = new(double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity, double.NegativeInfinity);
        public static readonly Box All = new(double.NegativeInfinity, double.NegativeInfinity, double.PositiveInfinity, double.PositiveInfinity);
        public bool IsEmpty => MinX > MaxX || MinY > MaxY;
        public bool IsAll => double.IsNegativeInfinity(MinX) || double.IsNegativeInfinity(MinY) || double.IsPositiveInfinity(MaxX) || double.IsPositiveInfinity(MaxY);
        /// <summary>T-0747-fix8: enthält eine Grenze NaN, sagt der Rahmen nichts (jeder Vergleich wäre falsch) – er gilt dann als „überall“.</summary>
        public bool HasNaN => double.IsNaN(MinX) || double.IsNaN(MinY) || double.IsNaN(MaxX) || double.IsNaN(MaxY);
        /// <summary>Vorsichtig: NaN → <see cref="All"/>.</summary>
        public Box ToSafe() => HasNaN ? All : this; // Methode, keine Eigenschaft: sonst druckt ToString sich selbst endlos
        public Box Union(Box o) => HasNaN || o.HasNaN ? All : IsEmpty ? o : o.IsEmpty ? this : new(Math.Min(MinX, o.MinX), Math.Min(MinY, o.MinY), Math.Max(MaxX, o.MaxX), Math.Max(MaxY, o.MaxY));
        public Box With((double X, double Y) q) => double.IsFinite(q.X) && double.IsFinite(q.Y) ? Union(new Box(q.X, q.Y, q.X, q.Y)) : All; // T-0747-fix8: nicht endlich → überall
        public Box Grow(double r) => IsEmpty ? this : new(MinX - r, MinY - r, MaxX + r, MaxY + r);
        public Box Scale(double k) => IsEmpty ? this : new(MinX * k, MinY * k, MaxX * k, MaxY * k);
        public Box Shift(double dx, double dy) => IsEmpty ? this : new(MinX + dx, MinY + dy, MaxX + dx, MaxY + dy);
    }

    sealed class BlockInfo(double bx, double by)
    {
        public double BaseX = bx, BaseY = by; public Box Box = Box.Empty; public List<List<(int Code, string Value)>> Inserts = [];
        public void Add(Box b) => Box = Box.Union(b);
    }

    /// <summary>T-0747-fix5: alle Zahlen eines Elements brauchbar? (keine NaN/∞, Koordinaten und Radien höchstens 1e9 mm)</summary>
    static bool SaneNumbers(List<(int Code, string Value)> b, double k)
    {
        foreach (var (c, v) in b)
        {
            // T-0747-fix8: alle Gleitkomma-Gruppen (Koordinaten, Maße, Winkel, Normale, Faktoren), nicht nur 10 … 42
            if (!(c is >= 10 and <= 59 or >= 110 and <= 149 or >= 210 and <= 239 or >= 460 and <= 469 or >= 1010 and <= 1059)) continue;
            if (!double.TryParse(v.Trim(), NumberStyles.Float, Inv, out var x)) continue; // kein Zahlwert: meldet der Leser wie bisher
            if (double.IsNaN(x) || double.IsInfinity(x)) return false;
            if (c is >= 10 and <= 40 && Math.Abs(x) * k > 1e9) return false;
            if (c is >= 50 and <= 59 && Math.Abs(x) > 1e6) return false; // Winkel in Grad
        }
        return true;
    }

    static List<List<(int Code, string Value)>> ReadVertexBodies(List<(int Code, string Value)> pairs, ref int p)
    {
        var res = new List<List<(int Code, string Value)>>();
        while (p < pairs.Count && pairs[p].Code == 0 && pairs[p].Value == "VERTEX")
        {
            p++; var vb = new List<(int Code, string Value)>(); while (p < pairs.Count && pairs[p].Code != 0) vb.Add(pairs[p++]); res.Add(vb);
        }
        if (p < pairs.Count && pairs[p].Code == 0 && pairs[p].Value == "SEQEND") { p++; while (p < pairs.Count && pairs[p].Code != 0) p++; }
        return res;
    }

    static Box ApplyNormal(List<(int Code, string Value)> b, Box box)
    {
        if (box.IsEmpty || !(Has(b, 210) || Has(b, 220) || Has(b, 230))) return box;
        double ex = Num(b, 210), ey = Num(b, 220), ez = Num(b, 230, 1), el = Math.Sqrt(ex * ex + ey * ey + ez * ez);
        if (el <= 1e-12 || Math.Abs(ex / el) > 1e-9 || Math.Abs(ey / el) > 1e-9) return Box.All;
        return ez < 0 ? new Box(-box.MaxX, box.MinY, -box.MinX, box.MaxY) : box;
    }

    /// <summary>T-0747-fix6: Höchstzahl Punkte einer abgetasteten Ellipse und aller Ellipsen einer Datei.</summary>
    public const int MaxEllipsePoints = 200_000;
    public const long MaxEllipsePointsTotal = 2_000_000;

    /// <summary>T-0747-fix6: Vorsilbe nicht gelesener Füllflächen (HATCH, SOLID …). Deckt sich ihr Rechteck mit einer gelesenen Kontur, ist ihr Rand schon gezeichnet.</summary>
    public const string FillPrefix = "Füllfläche ";

    /// <summary>T-0747-fix6: Elemente, die keinen Umriss bilden (Texte, Maße, Punkte, Ansichtsfenster …).</summary>
    static readonly HashSet<string> NoOutline = new(StringComparer.Ordinal)
    {
        "TEXT", "MTEXT", "POINT", "DIMENSION", "ATTRIB", "ATTDEF", "LEADER", "MLEADER", "MULTILEADER", "VIEWPORT", "TOLERANCE", "XLINE", "RAY", "IMAGE",
        "ACAD_TABLE", "SEQEND", "VERTEX", "BLOCK", "ENDBLK", "LIGHT", "SUN", "ACAD_PROXY_ENTITY_TEXT", "SHAPE", // SHAPE: Symbol aus einer Formdatei, wie Text
    };

    /// <summary>T-0747-fix8: deutsche Bezeichnung nicht übernommener DXF-Elemente – dieselbe Tabelle in Grundriss, Trendo CAD und Fräsen.</summary>
    public static string KindLabel(string k) => k switch
    {
        "INSERT" => "Blockverweis (INSERT)", "SPLINE" => "SPLINE (Freiformkurve)", "HATCH" => "Schraffur", "WIPEOUT" => "Abdeckfläche", "DIMENSION" => "Bemaßung",
        "SOLID" => "Füllfläche (SOLID)", "TRACE" => "Füllfläche (TRACE)", "3DFACE" => "3D-Fläche", "MLINE" => "Mehrfachlinie (MLINE)", "REGION" => "Region",
        "LEADER" or "MLEADER" or "MULTILEADER" => "Hinweislinie", "POINT" => "Punkt", "VIEWPORT" => "Ansichtsfenster", "ATTRIB" or "ATTDEF" => "Attribut",
        "IMAGE" => "Bild", "OLE2FRAME" => "OLE-Objekt", "ACAD_TABLE" => "Tabelle", "3DSOLID" => "Volumenkörper", "text" => "SVG-Text", _ => k,
    };

    static bool IsFill(string val) => val is "HATCH" or "SOLID" or "TRACE" or "3DFACE" or "WIPEOUT";

    /// <summary>T-0747-fix6: Kann das (verworfene) Element allein einen Umriss bilden? Linien, Bögen und offene Züge nicht – fehlen sie, bleibt eine offene
    /// Kontur übrig, und die wird schon ausgelassen und gemeldet (samt allem, was darin liegt).</summary>
    static bool OutlineCapable(string val, List<(int Code, string Value)> b) =>
        !NoOutline.Contains(val) && val is not ("LINE" or "ARC") && !(val is "LWPOLYLINE" or "POLYLINE" && (IntSafe(b, 70) & 1) == 0);

    static int IntSafe(List<(int Code, string Value)> b, int code) => b.FirstOrDefault(x => x.Code == code) is { Value: { } v } && int.TryParse(v.Trim(), NumberStyles.Integer, Inv, out var i) ? i : 0;

    /// <summary>T-0747-fix6: vorsichtige Ausdehnung (Weltkoordinaten) eines Elements: exakte Bögen und Ausbuchtungen beliebiger Größe, Normale −Z gespiegelt,
    /// schräge Normale über die DXF-Regel „beliebige Achse“ (Kreise als Kugelschnitt), SPLINE über die Kontrollpunkte (konvexe Hülle).
    /// Nicht sicher bestimmbar (nur Fit-Punkte, ACIS-Körper, MLINE, ungültige Zahlen …) → <see cref="Box.All"/>.</summary>
    static Box ShapeBox(string val, List<(int Code, string Value)> b, List<List<(int Code, string Value)>> verts, double k)
    {
        if (NoOutline.Contains(val)) return Box.Empty;
        if (!SaneNumbers(b, k) || verts.Any(v => !SaneNumbers(v, k))) return Box.All;
        switch (val)
        {
            case "LINE": return WPts(b, 10, 11);
            case "3DFACE": return WPts(b, 10, 11, 12, 13);
            case "SPLINE":
            {
                // B-Spline liegt in der konvexen Hülle seiner Kontrollpunkte (bei positiven Gewichten); nur Fit-Punkte → Kurve kann darüber hinaus laufen
                if (b.Any(x => x.Code == 41 && double.TryParse(x.Value.Trim(), NumberStyles.Float, Inv, out var w) && !(w > 0))) return Box.All;
                var cp = WPts(b, 10); if (!cp.IsEmpty) return cp;
                return WPts(b, 11).IsEmpty ? Box.Empty : Box.All; // gar keine Punkte: nichts gezeichnet
            }
            case "ELLIPSE":
            {
                double nx = Num(b, 210), ny = Num(b, 220), nz = Num(b, 230, 1), nl = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (!(nl > 1e-12)) return Box.All;
                nx /= nl; ny /= nl; nz /= nl;
                double ax = Num(b, 11), ay = Num(b, 21), az = Num(b, 31), r = Math.Abs(Num(b, 40, 1));
                double bxv = (ny * az - nz * ay) * r, byv = (nz * ax - nx * az) * r; // Nebenachse = N × A · Verhältnis
                double hx = Math.Sqrt(ax * ax + bxv * bxv), hy = Math.Sqrt(ay * ay + byv * byv); // volle Ellipse (Teilstück liegt darin)
                return new Box(Num(b, 10) - hx, Num(b, 20) - hy, Num(b, 10) + hx, Num(b, 20) + hy);
            }
            case "POLYLINE" when (Int(b, 70, 0) & (8 | 16 | 64)) != 0:
            {
                var box = Box.Empty; foreach (var v in verts) box = box.Union(WPts(v, 10)); return box; // 3D-Polylinie, Netz: Weltkoordinaten
            }
            case "MLINE" or "REGION" or "3DSOLID" or "BODY" or "SURFACE" or "MESH": return Box.All; // Ausdehnung nicht sicher bestimmbar
            case "HATCH": // T-0747-fix7: nur echte Randpfad-Koordinaten (nicht Erhebungs- und Saatpunkte)
            {
                if (Ocs.Of(b) is not { Flat: true }) return Box.All; // T-0747-fix10: schräge Normale
                var loops = HatchLoops(b, 0.01 / k); if (loops is null) return Box.All;
                var box = Box.Empty; foreach (var l in loops) box = box.Union(l.B); return box;
            }
            case "WIPEOUT": // T-0747-fix7: Bildrahmen Einfügepunkt + u·U + v·V (Pixel-Ränder mit ½ Pixel Reserve); der Ausschnitt liegt darin
            {
                double px = Num(b, 10), py = Num(b, 20), ux = Num(b, 11), uy = Num(b, 21), vx = Num(b, 12), vy = Num(b, 22), w = Math.Max(1, Num(b, 13, 1)), h = Math.Max(1, Num(b, 23, 1));
                var box = Box.Empty;
                foreach (var su in new[] { -0.5, w + 0.5 }) foreach (var sv in new[] { -0.5, h + 0.5 }) box = box.With((px + su * ux + sv * vx, py + su * uy + sv * vy));
                // T-0747-fix8: Ausschnitt-Punkte (14/24, Pixel mit ½ Versatz) dürfen über den Rahmen hinausgehen – mit umrechnen
                foreach (var (cu, cv) in Pairs(b, 14)) box = box.With((px + (cu + 0.5) * ux + (cv + 0.5) * vx, py + (cu + 0.5) * uy + (cv + 0.5) * vy));
                return box;
            }
            case "OLE2FRAME": return WPts(b, 10, 11); // T-0747-fix7: linke obere und rechte untere Ecke
            case "ACAD_PROXY_ENTITY": return IntSafe(b, 92) == 0 && !b.Any(x => x.Code == 310) ? Box.Empty : Box.All; // ohne Ersatzgrafik wird nichts gezeichnet
        }
        var o = Ocs.Of(b); if (o is null) return Box.All;
        switch (val)
        {
            case "CIRCLE": o.Circle(Num(b, 10), Num(b, 20), Num(b, 30), Math.Abs(Num(b, 40))); break;
            case "ARC":
            {
                var sw = (Num(b, 51) - Num(b, 50)) % 360; if (sw <= 0) sw += 360;
                o.Arc(Num(b, 10), Num(b, 20), Num(b, 30), Math.Abs(Num(b, 40)), Num(b, 50) * Math.PI / 180, sw * Math.PI / 180); break;
            }
            case "SOLID" or "TRACE": { var z = Num(b, 30); foreach (var (x, y) in Pairs(b, 10, 11, 12, 13)) o.Pt(x, y, z); break; }
            case "LWPOLYLINE":
            {
                var v = new List<(double X, double Y, double B)>();
                foreach (var (c, s) in b)
                {
                    if (c == 10) v.Add((D(s), 0, 0)); else if (c == 20 && v.Count > 0) v[^1] = v[^1] with { Y = D(s) }; else if (c == 42 && v.Count > 0) v[^1] = v[^1] with { B = D(s) };
                }
                o.Poly(v, (Int(b, 70, 0) & 1) != 0, Num(b, 38)); break;
            }
            case "POLYLINE":
            {
                var v = verts.Select(vb => (Num(vb, 10), Num(vb, 20), Num(vb, 42))).ToList();
                o.Poly(v, (Int(b, 70, 0) & 1) != 0, Num(b, 30)); break;
            }
            default: return Box.All;
        }
        return o.Box;
    }

    /// <summary>Punktpaare (c, c+10) in Reihenfolge, z. B. 10/20.</summary>
    static IEnumerable<(double X, double Y)> Pairs(List<(int Code, string Value)> b, params int[] codes)
    {
        double? px = null; var pc = 0;
        foreach (var (c, v) in b)
        {
            if (Array.IndexOf(codes, c) >= 0) { px = D(v); pc = c; }
            else if (px is { } x && c == pc + 10) { yield return (x, D(v)); px = null; }
        }
    }

    static Box WPts(List<(int Code, string Value)> b, params int[] codes) { var box = Box.Empty; foreach (var q in Pairs(b, codes)) box = box.With(q); return box; }

    /// <summary>T-0747-fix6: Ausdehnung im Objekt-Koordinatensystem sammeln und in Weltkoordinaten (x/y) umrechnen.</summary>
    sealed class Ocs
    {
        bool _flat; int _sgn = 1; (double X, double Y, double Z) _n, _ax, _ay;
        public Box Box = Box.Empty;
        public bool Flat => _flat;
        public int Sgn => _sgn;

        public static Ocs? Of(List<(int Code, string Value)> b)
        {
            double ex = Num(b, 210), ey = Num(b, 220), ez = Num(b, 230, 1), el = Math.Sqrt(ex * ex + ey * ey + ez * ez);
            if (!(el > 1e-12)) return null;
            ex /= el; ey /= el; ez /= el;
            var o = new Ocs { _n = (ex, ey, ez), _flat = Math.Abs(ex) <= 1e-9 && Math.Abs(ey) <= 1e-9, _sgn = ez < 0 ? -1 : 1 };
            static (double X, double Y, double Z) Cross((double X, double Y, double Z) a, (double X, double Y, double Z) c) => (a.Y * c.Z - a.Z * c.Y, a.Z * c.X - a.X * c.Z, a.X * c.Y - a.Y * c.X);
            var ax = Math.Abs(ex) < 1.0 / 64 && Math.Abs(ey) < 1.0 / 64 ? Cross((0, 1, 0), o._n) : Cross((0, 0, 1), o._n);
            var al = Math.Sqrt(ax.X * ax.X + ax.Y * ax.Y + ax.Z * ax.Z); o._ax = (ax.X / al, ax.Y / al, ax.Z / al); o._ay = Cross(o._n, o._ax);
            return o;
        }

        (double X, double Y) W(double x, double y, double z) => _flat ? (_sgn * x, y) : (x * _ax.X + y * _ay.X + z * _n.X, x * _ax.Y + y * _ay.Y + z * _n.Y);
        public void Pt(double x, double y, double z) => Box = Box.With(W(x, y, z));

        public void Circle(double cx, double cy, double z, double r)
        {
            var c = W(cx, cy, z);
            double hx = _flat ? r : r * Math.Sqrt(Math.Max(0, 1 - _n.X * _n.X)), hy = _flat ? r : r * Math.Sqrt(Math.Max(0, 1 - _n.Y * _n.Y)); // Kreis im Raum: Ausdehnung je Achse
            Box = Box.Union(new Box(c.X - hx, c.Y - hy, c.X + hx, c.Y + hy));
        }

        /// <summary>Bogen ab <paramref name="a0"/> über <paramref name="sweep"/> (Bogenmaß, Vorzeichen = Richtung): Endpunkte und die Achsen-Extrempunkte im Bereich.</summary>
        public void Arc(double cx, double cy, double z, double r, double a0, double sweep)
        {
            if (!_flat || Math.Abs(sweep) >= 2 * Math.PI) { Circle(cx, cy, z, r); return; }
            if (sweep < 0) { a0 += sweep; sweep = -sweep; }
            Pt(cx + r * Math.Cos(a0), cy + r * Math.Sin(a0), z); Pt(cx + r * Math.Cos(a0 + sweep), cy + r * Math.Sin(a0 + sweep), z);
            var k = Math.Ceiling(a0 / (Math.PI / 2));
            for (var i = 0; i < 5; i++, k++)
            {
                var t = k * Math.PI / 2; if (t > a0 + sweep) break;
                Pt(cx + r * Math.Cos(t), cy + r * Math.Sin(t), z);
            }
        }

        /// <summary>Zug mit Ausbuchtungen (bulge, beliebig groß).</summary>
        public void Poly(List<(double X, double Y, double B)> v, bool closed, double z)
        {
            for (var i = 0; i < v.Count; i++)
            {
                Pt(v[i].X, v[i].Y, z);
                if (i == v.Count - 1 && !closed) break;
                var q = v[(i + 1) % v.Count]; var bl = v[i].B;
                if (Math.Abs(bl) < 1e-12) continue;
                double dx = q.X - v[i].X, dy = q.Y - v[i].Y, c = Math.Sqrt(dx * dx + dy * dy); if (!(c > 0)) continue;
                var th = 4 * Math.Atan(bl); var r = c / (2 * Math.Sin(Math.Abs(th) / 2)); var hh = c / 2 / Math.Tan(th / 2);
                double mx = (v[i].X + q.X) / 2 - dy / c * hh, my = (v[i].Y + q.Y) / 2 + dx / c * hh;
                Arc(mx, my, z, r, Math.Atan2(v[i].Y - my, v[i].X - mx), th);
            }
        }
    }

    /// <summary>T-0747-fix7: eine Randschleife einer Füllfläche – Ausdehnung (Weltkoordinaten, Zeichnungseinheiten) und, wo sicher bestimmbar,
    /// ihre Form als geschlossener Zug (für den Vergleich mit gezeichneten Konturen); sonst <c>null</c>.</summary>
    /// <remarks>T-0747-fix10: <c>Ambiguous</c> – die Form hängt von einer Lesart ab, die Programme verschieden schreiben (Bögen im Uhrzeigersinn, Ellipsenkanten):
    /// dann ist der Rahmen vorsichtig (Vollkreis, volle Ellipse) und die Form dient nur dem Formvergleich, nicht dem Enthaltensein.</remarks>
    internal sealed record FillLoop(Box B, List<(double X, double Y)>? Shape, bool Ambiguous = false)
    {
        /// <summary>T-0747-fix10: die Schleife ist ein Vollkreis (Weltkoordinaten) – dann genügt ein Vergleich von Mittelpunkt und Radius.</summary>
        public (double Cx, double Cy, double R)? Circle { get; init; }
    }

    /// <summary>T-0747-fix9: Gruppen, die nach dem Randpfad-Abschnitt erlaubt sind (Stil, Muster, Pixelgröße, Saatpunkte, Farbverlauf, Zusatzdaten).</summary>
    static bool IsHatchTailCode(int c) => c is 75 or 76 or 52 or 41 or 77 or 78 or 53 or 43 or 44 or 45 or 46 or 79 or 49 or 47 or 98 or 10 or 20 or 30 or 63 or 421 or 102
        or (>= 450 and <= 470) or (>= 1000 and <= 1071);

    /// <summary>T-0747-fix7/8/9: Randschleifen einer HATCH – als vollständige, strenge Grammatik gelesen:
    /// Vorspann (Kopf, Erhebungspunkt, Normale, Muster-Name, Flags) ohne Randgruppen → 91 Schleifenzahl → genau so viele Schleifen (92 …, Polylinienrand
    /// 72/73/93/10/20[/42] oder Kanten 93 + je Kante 72 und ihre festen Gruppen, danach 97 + 330 Kennungen als Text) → Nachspann nur aus erlaubten Gruppen
    /// (<see cref="IsHatchTailCode"/>). Jede Abweichung (Zahl nicht ganzzahlig/negativ/zu groß, Gruppe an falscher Stelle, Randgruppe im Nachspann,
    /// zweites 91 …) → <c>null</c> = Ausdehnung unbekannt. Eine leere Liste nur bei 91 = 0 ohne Randdaten.</summary>
    static List<FillLoop>? HatchLoops(List<(int Code, string Value)> b, double tolU)
    {
        var o0 = Ocs.Of(b); if (o0 is null) return null;
        var i = b.FindIndex(x => x.Code == 91); if (i < 0) return null; // ohne Schleifenzahl: Rand nicht bestimmbar
        for (var j = 0; j < i; j++) if (b[j].Code is 92 or 93 or 72 or 73 or 94 or 95 or 96 or 97) return null; // Randgruppen vor 91
        bool Is(int code) => i < b.Count && b[i].Code == code;
        double N(int code)
        {
            if (!Is(code) || !double.TryParse(b[i].Value.Trim(), NumberStyles.Float, Inv, out var v) || !double.IsFinite(v)) throw new CadError("HATCH-Rand unvollständig");
            i++; return v;
        }
        int I(int code, int min, bool count = true)
        {
            var v = N(code);
            if (v != Math.Floor(v) || v < min || v > (count ? b.Count : 1 << 20)) throw new CadError("HATCH-Rand: Zahl ungültig"); // ganzzahlig, nicht negativ, nicht mehr als Daten da sind
            return (int)v;
        }
        double Opt(int code, double def) => Is(code) ? N(code) : def;
        var loops = new List<FillLoop>(); var z = Num(b, 30);
        try
        {
            var nLoops = I(91, 0);
            for (var l = 0; l < nLoops; l++)
            {
                if (!Is(92)) return null;
                var flags = I(92, 0, false); var o = Ocs.Of(b)!; List<(double X, double Y)>? shape = []; var grow = 0.0; var ambiguous = false; (double Cx, double Cy, double R)? circle = null;
                if ((flags & 2) != 0)
                {
                    var hasBulge = I(72, 0, false); if (hasBulge > 1) return null;
                    if (Is(73)) { var cl = I(73, 0, false); if (cl > 1) return null; }
                    var n = I(93, 2); // Polylinienrand: mindestens 3 Punkte, oder 2 mit Ausbuchtung (Kreis aus zwei Halbkreisen)
                    var v = new List<(double X, double Y, double B)>();
                    for (var k = 0; k < n; k++) { var x = N(10); var y = N(20); var bl = hasBulge != 0 ? Opt(42, 0) : 0; v.Add((x, y, bl)); }
                    if (v.Count < 3 && v.All(q => Math.Abs(q.B) < 1e-12)) return null;
                    // T-0747-fix9: entartet – Stück der Länge 0 mit Ausbuchtung, oder alle Punkte gleich → Form unbekannt
                    for (var k = 0; k < v.Count; k++) { var q = v[(k + 1) % v.Count]; if (Math.Abs(v[k].B) >= 1e-12 && v[k].X == q.X && v[k].Y == q.Y) return null; }
                    if (v.All(q => q.X == v[0].X && q.Y == v[0].Y)) return null;
                    o.Poly(v, true, z); // Randschleifen sind immer geschlossen
                    shape = SamplePoly(v, tolU);
                    if (v.Count == 2 && Math.Abs(Math.Abs(v[0].B) - 1) < 1e-12 && v[0].B == v[1].B) circle = ((v[0].X + v[1].X) / 2, (v[0].Y + v[1].Y) / 2, Math.Sqrt(Math.Pow(v[1].X - v[0].X, 2) + Math.Pow(v[1].Y - v[0].Y, 2)) / 2); // zwei Halbkreise
                }
                else
                {
                    var n = I(93, 1); // Kantenrand: mindestens eine Kante
                    // T-0747-fix10: Zusammenhang – Ende einer Kante = Anfang der nächsten (je Achse ± tol), sonst keine Form (kein Formvergleich, kein Enthaltensein)
                    (double X, double Y)? first = null, last = null; var broken = false;
                    void Edge((double X, double Y) a, (double X, double Y) e)
                    {
                        if (first is null) first = a;
                        else if (last is { } lp && Math.Max(Math.Abs(lp.X - a.X), Math.Abs(lp.Y - a.Y)) > tolU) broken = true;
                        last = e;
                    }
                    for (var k = 0; k < n; k++)
                    {
                        var type = I(72, 1, false);
                        switch (type)
                        {
                            case 1:
                            {
                                double x0 = N(10), y0 = N(20), x1 = N(11), y1 = N(21);
                                o.Pt(x0, y0, z); o.Pt(x1, y1, z); shape?.Add((x0, y0)); shape?.Add((x1, y1)); Edge((x0, y0), (x1, y1)); break;
                            }
                            case 2:
                            {
                                double cx = N(10), cy = N(20), r = N(40), a0 = N(50), a1 = N(51); var ccw = !Is(73) || I(73, 0, false) != 0;
                                if (!(r > 0)) return null; // T-0747-fix10: Radius ≤ 0 ungültig
                                if (Math.Abs(a0) > 1e6 || Math.Abs(a1) > 1e6) throw new CadError("HATCH-Bogen: Winkel ungültig");
                                // T-0747-fix10: Bögen im Uhrzeigersinn (73 = 0) speichern AutoCAD/ezdxf mit Komplementwinkeln – echter Start 360° − a0,
                                // Bogen −((a1 − a0) mod 360°, bei 0 volle 360°); gegen den Uhrzeigersinn von a0 um +(a1 − a0) mod 360°
                                var sw = (a1 - a0) % 360; if (sw <= 0) sw += 360;
                                var st = (ccw ? a0 : 360 - a0) * Math.PI / 180; var swr = (ccw ? 1 : -1) * sw * Math.PI / 180;
                                // Rahmen: gegen den Uhrzeigersinn genau; im Uhrzeigersinn vorsichtig der Vollkreis – manche Programme schreiben die Winkel wörtlich statt als Komplement
                                if (ccw) o.Arc(cx, cy, z, r, st, swr); else { o.Circle(cx, cy, z, r); if (sw < 360) ambiguous = true; }
                                if (n == 1 && sw >= 360) circle = (cx, cy, r); // Vollkreis als einzige Kante
                                shape?.AddRange(SampleArc(cx, cy, r, st, swr, tolU));
                                Edge((cx + r * Math.Cos(st), cy + r * Math.Sin(st)), (cx + r * Math.Cos(st + swr), cy + r * Math.Sin(st + swr))); break;
                            }
                            case 3:
                            {
                                double cx = N(10), cy = N(20), mx = N(11), my = N(21), ratio = N(40), p0 = N(50), p1 = N(51); var ccw = !Is(73) || I(73, 0, false) != 0;
                                if (!(ratio > 0) || ratio > 1 + 1e-12 || !(mx * mx + my * my > 0)) return null; // T-0747-fix10: Verhältnis ≤ 0 oder > 1 ungültig
                                if (Math.Abs(p0) > 1e6 || Math.Abs(p1) > 1e6) throw new CadError("HATCH-Ellipse: Parameter ungültig");
                                // T-0747-fix10: Parameter im Uhrzeigersinn wie beim Bogen mit Komplement (Start 360° − p0, negativer Bogen); Rahmen aus den abgetasteten
                                // Punkten (Pfeilhöhe ≤ h²/8 · max(a, b) ≤ tol/100, unten auf den Rahmen zugeschlagen)
                                double a = Math.Sqrt(mx * mx + my * my), bb = a * ratio, bxv = -my * ratio, byv = mx * ratio;
                                var sw = (p1 - p0) % 360; if (sw <= 0) sw += 360;
                                var st = (ccw ? p0 : 360 - p0) * Math.PI / 180; var swr = (ccw ? 1 : -1) * sw * Math.PI / 180;
                                var dt = Math.Sqrt(8 * tolU / 100 / Math.Max(Math.Max(a, bb), 1e-12));
                                var m = (int)Math.Clamp(Math.Ceiling(Math.Abs(swr) / dt), 4, MaxShapePoints);
                                (double X, double Y) At(double t) => (cx + Math.Cos(t) * mx + Math.Sin(t) * bxv, cy + Math.Cos(t) * my + Math.Sin(t) * byv);
                                for (var q = 0; q < m; q++) shape?.Add(At(st + swr * q / m));
                                // Rahmen vorsichtig die volle Ellipse (Parameter werden von Programmen verschieden geschrieben, z. B. im Bogenmaß)
                                double hx = Math.Sqrt(mx * mx + bxv * bxv), hy = Math.Sqrt(my * my + byv * byv);
                                o.Pt(cx - hx, cy - hy, z); o.Pt(cx + hx, cy - hy, z); o.Pt(cx - hx, cy + hy, z); o.Pt(cx + hx, cy + hy, z);
                                if (sw < 360) ambiguous = true;
                                Edge(At(st), At(st + swr));
                                break;
                            }
                            case 4:
                            {
                                var deg = I(94, 1, false); var rational = I(73, 0, false); I(74, 0, false); var nk = I(95, 0); var nc = I(96, 1);
                                var knots = new List<double>(); for (var q = 0; q < nk; q++) knots.Add(N(40));
                                var ctrl = new List<(double X, double Y)>();
                                for (var q = 0; q < nc; q++)
                                {
                                    double x = N(10), y = N(20); o.Pt(x, y, z); ctrl.Add((x, y));
                                    if (Is(42) && !(N(42) > 0)) return null; // Gewicht ≤ 0: Hülle gilt nicht
                                }
                                _ = rational;
                                // T-0747-fix8: 97 nach den Kontrollpunkten ist immer die Zahl der Fit-Punkte (ab R2010, auch 0)
                                if (Is(97)) { var nf = I(97, 0); for (var q = 0; q < nf; q++) o.Pt(N(11), N(21), z); }
                                if (Is(12)) { N(12); N(22); }
                                if (Is(13)) { N(13); N(23); }
                                // Form nur, wo die Kurve sicher der Kontrollzug ist: Grad 1 oder alle Kontrollpunkte auf einer Geraden, Knoten nicht fallend und
                                // an den Enden geklemmt (T-0747-fix9: fallende Knoten sind ungültig – keine Form)
                                var monotone = knots.Zip(knots.Skip(1)).All(t => t.Second >= t.First);
                                var clamped = monotone && knots.Count == nc + deg + 1 && knots.Take(deg + 1).All(k => k == knots[0]) && knots.TakeLast(deg + 1).All(k => k == knots[^1]);
                                if (shape is not null && clamped && (deg == 1 || Collinear(ctrl))) { shape.AddRange(deg == 1 ? ctrl : [ctrl[0], ctrl[^1]]); Edge(ctrl[0], ctrl[^1]); }
                                else shape = null;
                                break;
                            }
                            default: return null;
                        }
                    }
                    if (broken || first is { } f0 && last is { } l0 && Math.Max(Math.Abs(f0.X - l0.X), Math.Abs(f0.Y - l0.Y)) > tolU) shape = null; // nicht zusammenhängend
                }
                // Quellobjekte: 97 Anzahl, dann genau so viele 330 (Kennungen sind Text, z. B. hexadezimal „2A5“ – nicht als Zahl lesen)
                if (Is(97)) { var ns = I(97, 0); for (var q = 0; q < ns; q++) { if (!Is(330)) return null; i++; } }
                List<(double X, double Y)>? world = null;
                if (shape is { Count: >= 3 } && o.Flat) world = shape.Select(p => (o.Sgn * p.X, p.Y)).ToList();
                if (o.Box.IsEmpty || o.Box.HasNaN) return null;
                loops.Add(new FillLoop(o.Box.Grow(grow), world, ambiguous) { Circle = circle is { } cc && o.Flat && world is not null ? (o.Sgn * cc.Cx, cc.Cy, cc.R) : null });
            }
            // Nachspann: im ganzen Rest nur erlaubte Gruppen – Randdaten nach dem Abschnittsende oder ein zweites 91 machen die Schraffur unsicher
            for (; i < b.Count; i++) if (!IsHatchTailCode(b[i].Code)) return null;
        }
        catch (CadError) { return null; }
        catch (FormatException) { return null; }
        return loops;
    }

    /// <summary>T-0747-fix9: höchstens so viele Punkte je Form im Formvergleich – darüber gilt „nicht formgleich“ (sicher).</summary>
    const int MaxShapePoints = 400_000;

    static bool Collinear(List<(double X, double Y)> p)
    {
        if (p.Count < 2) return false;
        var (ax, ay) = p[0]; var (bx, by) = p[^1]; var l = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay)); if (!(l > 0)) return false;
        return p.All(q => Math.Abs((q.X - ax) * (by - ay) - (q.Y - ay) * (bx - ax)) / l <= 1e-9 * Math.Max(1, l)
            && ((q.X - ax) * (bx - ax) + (q.Y - ay) * (by - ay)) >= -1e-9 && ((q.X - bx) * (ax - bx) + (q.Y - by) * (ay - by)) >= -1e-9); // auch zwischen den Enden
    }

    /// <summary>Bogen als Punkte (ohne Endpunkt), Pfeilhöhe ≤ <paramref name="tol"/>/100 (T-0747-fix9/10: fein genug, der Formvergleich rechnet
    /// diesen Zerlegungsfehler mit ein; 1/100 statt 1/1000 spart bei großen Kreisen zwei Drittel der Punkte).</summary>
    static IEnumerable<(double X, double Y)> SampleArc(double cx, double cy, double r, double a0, double sweep, double tol)
    {
        var sag = tol / 100;
        var step = r > sag ? 2 * Math.Acos(Math.Max(-1, 1 - sag / r)) : Math.PI / 8;
        var n = (int)Math.Clamp(Math.Ceiling(Math.Abs(sweep) / Math.Max(step, 1e-12)), 1, MaxShapePoints);
        for (var k = 0; k < n; k++) yield return (cx + r * Math.Cos(a0 + sweep * k / n), cy + r * Math.Sin(a0 + sweep * k / n));
    }

    /// <summary>Geschlossener Zug mit Ausbuchtungen als Punkte.</summary>
    static List<(double X, double Y)> SamplePoly(List<(double X, double Y, double B)> v, double tol)
    {
        var res = new List<(double X, double Y)>();
        for (var k = 0; k < v.Count; k++)
        {
            var p = v[k]; var q = v[(k + 1) % v.Count];
            if (Math.Abs(p.B) < 1e-12) { res.Add((p.X, p.Y)); continue; }
            double dx = q.X - p.X, dy = q.Y - p.Y, c = Math.Sqrt(dx * dx + dy * dy); if (!(c > 0)) { res.Add((p.X, p.Y)); continue; }
            var th = 4 * Math.Atan(p.B); var r = c / (2 * Math.Sin(Math.Abs(th) / 2)); var hh = c / 2 / Math.Tan(th / 2);
            double mx = (p.X + q.X) / 2 - dy / c * hh, my = (p.Y + q.Y) / 2 + dx / c * hh;
            res.AddRange(SampleArc(mx, my, r, Math.Atan2(p.Y - my, p.X - mx), th, tol));
        }
        return res;
    }

    /// <summary>T-0747-fix7: Randschleifen einer Füllfläche (HATCH je Schleife; SOLID/TRACE/3DFACE als Viereck; WIPEOUT nur Rahmen) – <c>null</c> = nicht bestimmbar.</summary>
    static List<FillLoop>? FillLoops(string val, List<(int Code, string Value)> b, double k)
    {
        if (!SaneNumbers(b, k)) return null;
        var tolU = 0.01 / k;
        switch (val)
        {
            case "HATCH": return Ocs.Of(b) is { Flat: true } ? HatchLoops(b, tolU) : null; // T-0747-fix10: schräge Normale → Lage unsicher, überall
            case "SOLID" or "TRACE":
            {
                var o = Ocs.Of(b); if (o is null) return null;
                var p = Pairs(b, 10, 11, 12, 13).ToList(); if (p.Count < 3) return [];
                var z = Num(b, 30); foreach (var (x, y) in p) o.Pt(x, y, z);
                var ring = p.Count == 4 ? new List<(double X, double Y)> { p[0], p[1], p[3], p[2] } : p; // SOLID: Eckenfolge 1-2-4-3
                return [new FillLoop(o.Box, o.Flat ? ring.Select(q => (o.Sgn * q.X, q.Y)).ToList() : null)];
            }
            case "3DFACE":
            {
                var p = Pairs(b, 10, 11, 12, 13).ToList(); if (p.Count < 3) return [];
                return [new FillLoop(WPts(b, 10, 11, 12, 13), p)];
            }
            default: return [new FillLoop(ShapeBox(val, b, [], k), null)];
        }
    }

    /// <summary>T-0747-fix7/9: deckt sich der Zug <paramref name="a"/> mit der geschlossenen Kontur <paramref name="c"/>? Jeder Punkt und jede Stückmitte beider
    /// Züge liegt je Achse (Chebyshev) höchstens <paramref name="tol"/> vom anderen Zug. T-0747-fix9: über ein Streckenraster (nicht jeder gegen jeden),
    /// abbrechbar, mit Punktgrenze (darüber: nicht formgleich).</summary>
    static bool SameShape(List<(double X, double Y)> a, IReadOnlyList<(double X, double Y)> c, double tol, CancellationToken ct = default, SegGrid? gridA = null, SegGrid? gridC = null)
    {
        if (a.Count < 2 || c.Count < 2 || a.Count > MaxShapePoints || c.Count > MaxShapePoints) return false;
        return Covered(a, gridC ?? Grid(c, tol * 4, ct), tol, ct) && Covered(c, gridA ?? Grid(a, tol * 4, ct), tol, ct);
    }

    static double Cheb((double X, double Y) p, (double X, double Y) s, (double X, double Y) e)
    {
        double dx = e.X - s.X, dy = e.Y - s.Y, ox = p.X - s.X, oy = p.Y - s.Y;
        double At(double t) { t = Math.Clamp(t, 0, 1); return Math.Max(Math.Abs(ox - t * dx), Math.Abs(oy - t * dy)); }
        var m = Math.Min(At(0), At(1)); // konvex und stückweise linear: Minimum an einem Knick oder am Ende
        if (dx != 0) m = Math.Min(m, At(ox / dx));
        if (dy != 0) m = Math.Min(m, At(oy / dy));
        if (dx != dy) m = Math.Min(m, At((ox - oy) / (dx - dy)));
        if (dx != -dy) m = Math.Min(m, At((ox + oy) / (dx + dy)));
        return m;
    }

    /// <summary>T-0747-fix9/10: Streckenraster eines geschlossenen Zugs. Zellgröße ≥ doppelte mittlere Stücklänge; lange Strecken werden in Stücke
    /// von höchstens einer Zelle geteilt (sonst belegt eine lange schräge Strecke sehr viele Zellen). Abbruch alle 1024 Strecken.</summary>
    static SegGrid Grid(IReadOnlyList<(double X, double Y)> z, double minCell, CancellationToken ct = default)
    {
        double len = 0;
        for (var i = 0; i < z.Count; i++) { var e = z[(i + 1) % z.Count]; len += Math.Sqrt((e.X - z[i].X) * (e.X - z[i].X) + (e.Y - z[i].Y) * (e.Y - z[i].Y)); }
        var cell = Math.Max(minCell, 2 * len / Math.Max(1, z.Count));
        var g = new SegGrid(cell);
        for (var i = 0; i < z.Count; i++)
        {
            if ((i & 1023) == 0) ct.ThrowIfCancellationRequested();
            var a = z[i]; var b = z[(i + 1) % z.Count]; var l = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
            var parts = (int)Math.Clamp(Math.Ceiling(l / cell), 1, 1 << 20);
            if (parts == 1) { g.Add(a, b, i); continue; }
            for (var k = 0; k < parts; k++)
                g.Add((a.X + (b.X - a.X) * k / parts, a.Y + (b.Y - a.Y) * k / parts), (a.X + (b.X - a.X) * (k + 1) / parts, a.Y + (b.Y - a.Y) * (k + 1) / parts), i);
        }
        return g;
    }

    /// <summary>T-0747-fix10: Index für „Punkt im geschlossenen Zug“ (gerade/ungerade-Regel) – Strecken nach waagrechten Streifen sortiert,
    /// je Abfrage nur der Streifen des Punkts. Die Streifenzahl passt sich an, damit der Index höchstens etwa 16 × so groß wird wie der Zug.</summary>
    sealed class PolyIndex
    {
        readonly IReadOnlyList<(double X, double Y)> _p; readonly double _minY, _h; readonly List<int>[] _rows;
        public PolyIndex(IReadOnlyList<(double X, double Y)> p, CancellationToken ct)
        {
            _p = p; _minY = p.Min(q => q.Y); var maxY = p.Max(q => q.Y); var n = p.Count;
            var rows = Math.Clamp(n / 8, 1, 1 << 16);
            while (rows > 1)
            {
                double h = Math.Max((maxY - _minY) / rows, 1e-12); long total = 0;
                for (var i = 0; i < n && total <= 16L * n; i++) { var a = p[i]; var b = p[(i + 1) % n]; total += (long)Math.Floor((Math.Max(a.Y, b.Y) - _minY) / h) - (long)Math.Floor((Math.Min(a.Y, b.Y) - _minY) / h) + 1; }
                if (total <= 16L * n) break;
                rows /= 4;
            }
            _h = Math.Max((maxY - _minY) / rows, 1e-12); _rows = new List<int>[rows];
            for (var r = 0; r < rows; r++) _rows[r] = [];
            for (var i = 0; i < n; i++)
            {
                if ((i & 1023) == 0) ct.ThrowIfCancellationRequested();
                var a = p[i]; var b = p[(i + 1) % n];
                var r0 = Math.Clamp((int)Math.Floor((Math.Min(a.Y, b.Y) - _minY) / _h), 0, rows - 1); var r1 = Math.Clamp((int)Math.Floor((Math.Max(a.Y, b.Y) - _minY) / _h), 0, rows - 1);
                for (var r = r0; r <= r1; r++) _rows[r].Add(i);
            }
        }
        public bool Contains(double x, double y)
        {
            var r = (int)Math.Floor((y - _minY) / _h); if (r < 0 || r >= _rows.Length) return false;
            var inside = false;
            foreach (var i in _rows[r])
            {
                var (xi, yi) = _p[(i + 1) % _p.Count]; var (xj, yj) = _p[i];
                if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) inside = !inside;
            }
            return inside;
        }
    }

    /// <summary>Liegt jeder Punkt und jede Stückmitte von <paramref name="p"/> je Achse höchstens <paramref name="tol"/> vom Zug <paramref name="z"/>?</summary>
    static bool Covered(IReadOnlyList<(double X, double Y)> p, SegGrid g, double tol, CancellationToken ct)
    {
        bool Near((double X, double Y) q) => g.Any(q.X - tol, q.Y - tol, q.X + tol, q.Y + tol, j => Cheb(q, g.Segs[j].A, g.Segs[j].B) <= tol);
        for (var i = 0; i < p.Count; i++)
        {
            if ((i & 1023) == 0) ct.ThrowIfCancellationRequested();
            var e = p[(i + 1) % p.Count];
            if (!Near(p[i]) || !Near(((p[i].X + e.X) / 2, (p[i].Y + e.Y) / 2))) return false;
        }
        return true;
    }

    /// <summary>T-0747-fix9/10: liegt der geschlossene Zug <paramref name="f"/> in der Kontur <paramref name="c"/>? Der Zug wird im Abstand
    /// ≤ <paramref name="tol"/> abgetastet; jeder Punkt muss innen liegen oder höchstens <paramref name="tol"/> vom Rand (Toleranz wie beim Formvergleich).
    /// Raster und Punkt-im-Zug-Index der Kontur werden je Kontur einmal gebaut (<paramref name="index"/>).</summary>
    static bool InsideContour(List<(double X, double Y)> f, CadContour c, double tol, CancellationToken ct, Func<(SegGrid G, PolyIndex P)> index)
    {
        if (f.Count < 3 || f.Count > MaxShapePoints || c.Pts.Count > MaxShapePoints) return false;
        var (g, pi) = index();
        bool Ok((double X, double Y) q) => pi.Contains(q.X, q.Y) || g.Any(q.X - tol, q.Y - tol, q.X + tol, q.Y + tol, j => SegDistE(q, g.Segs[j].A, g.Segs[j].B) <= tol);
        long done = 0;
        for (var i = 0; i < f.Count; i++)
        {
            var a = f[i]; var b = f[(i + 1) % f.Count];
            var n = (int)Math.Clamp(Math.Ceiling(Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y)) / tol), 1, 1 << 22);
            done += n; if (done > 2_000_000) return false; // zu fein für eine sichere Prüfung → nicht verwerfen
            for (var k = 0; k < n; k++)
            {
                if ((done + k & 1023) == 0) ct.ThrowIfCancellationRequested();
                if (!Ok((a.X + (b.X - a.X) * k / n, a.Y + (b.Y - a.Y) * k / n))) return false;
            }
        }
        return true;
    }

    static double SegDistE((double X, double Y) p, (double X, double Y) s, (double X, double Y) e)
    {
        double dx = e.X - s.X, dy = e.Y - s.Y, l2 = dx * dx + dy * dy; var t = l2 > 0 ? Math.Clamp(((p.X - s.X) * dx + (p.Y - s.Y) * dy) / l2, 0, 1) : 0;
        return Math.Sqrt(Math.Pow(p.X - s.X - t * dx, 2) + Math.Pow(p.Y - s.Y - t * dy, 2));
    }

    static double SegSegDist((double X, double Y) a, (double X, double Y) b, (double X, double Y) c, (double X, double Y) d)
    {
        static double Cross((double X, double Y) o, (double X, double Y) p, (double X, double Y) q) => (p.X - o.X) * (q.Y - o.Y) - (p.Y - o.Y) * (q.X - o.X);
        double d1 = Cross(c, d, a), d2 = Cross(c, d, b), d3 = Cross(a, b, c), d4 = Cross(a, b, d);
        if ((d1 > 0) != (d2 > 0) && (d3 > 0) != (d4 > 0) && d1 != 0 && d2 != 0 && d3 != 0 && d4 != 0) return 0; // echte Kreuzung
        static double PS((double X, double Y) p, (double X, double Y) s, (double X, double Y) e)
        {
            double dx = e.X - s.X, dy = e.Y - s.Y, l2 = dx * dx + dy * dy; var t = l2 > 0 ? Math.Clamp(((p.X - s.X) * dx + (p.Y - s.Y) * dy) / l2, 0, 1) : 0;
            return Math.Sqrt(Math.Pow(p.X - s.X - t * dx, 2) + Math.Pow(p.Y - s.Y - t * dy, 2));
        }
        return Math.Min(Math.Min(PS(a, c, d), PS(b, c, d)), Math.Min(PS(c, a, b), PS(d, a, b)));
    }

    static Box BoxOf(IReadOnlyList<(double X, double Y)> p) { var bx = Box.Empty; foreach (var q in p) bx = bx.With(q); return bx; }

    /// <summary>T-0747-fix9: Füllflächen-Schleifen mit gezeichneten Konturen vergleichen – formgleich → entfernen (zählt als „schon gezeichnet“);
    /// ausgeblendete Ebene und ganz in einer gezeichneten Kontur → entfernen (zählt nicht als formgleich). Vorfilter über die Rahmen (Raster nach linker
    /// unterer Ecke), feine Konturen (Bögen auf 0,0001 mm) nur für die Kandidaten, abbrechbar.</summary>
    static (HashSet<int> Drop, HashSet<int> Same) MatchFills(CadDrawing d, List<(int Index, List<(double X, double Y)>? Shape, string Layer, int Serial, bool Ambiguous)> fills,
        List<(string What, Box B)> unread, double kUnits, CancellationToken ct, Dictionary<int, (double Cx, double Cy, double R)>? circles = null)
    {
        var drop = new HashSet<int>(); var same = new HashSet<int>();
        var hidden = d.Layers.Where(l => !l.Visible).Select(l => l.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        double tolU = 0.01 / kUnits, fineChord = 0.0001 / kUnits, cell = 0.05 / kUnits;
        var vis = d.Entities.Where(e => e.Kind != "text" && !hidden.Contains(e.Layer)).ToList();
        var coarse = CadContours.Build(vis, 0.1 / kUnits, 0.001 / kUnits, ct).Where(c => c.Closed).ToList();
        var coarseBox = coarse.Select(c => BoxOf(c.Pts)).ToArray();
        Dictionary<(long, long), List<int>> Index(Box[] bx)
        {
            var m = new Dictionary<(long, long), List<int>>();
            for (var j = 0; j < bx.Length; j++) { var key = ((long)Math.Floor(bx[j].MinX / cell), (long)Math.Floor(bx[j].MinY / cell)); if (!m.TryGetValue(key, out var l)) m[key] = l = []; l.Add(j); }
            return m;
        }
        IEnumerable<int> Near(Dictionary<(long, long), List<int>> m, Box[] bx, Box u, double t)
        {
            var (kx, ky) = ((long)Math.Floor(u.MinX / cell), (long)Math.Floor(u.MinY / cell));
            for (var dx = -1L; dx <= 1; dx++) for (var dy = -1L; dy <= 1; dy++)
                    if (m.TryGetValue((kx + dx, ky + dy), out var l))
                        foreach (var j in l)
                            if (Math.Abs(bx[j].MinX - u.MinX) <= t && Math.Abs(bx[j].MinY - u.MinY) <= t && Math.Abs(bx[j].MaxX - u.MaxX) <= t && Math.Abs(bx[j].MaxY - u.MaxY) <= t) yield return j;
        }
        var coarseIdx = Index(coarseBox);
        // T-0747-fix10: Vollkreis gegen gezeichneten Kreis analytisch – |Δx| + |Δr| und |Δy| + |Δr| ≤ Toleranz genügt (jeder Punkt je Achse so nah)
        if (circles is { Count: > 0 })
        {
            var drawnCircles = new Dictionary<(long, long), List<CadEntity>>();
            foreach (var e in vis.Where(e => e.Kind == "kreis" && e.R > 0)) { var key = ((long)Math.Floor(e.Cx / cell), (long)Math.Floor(e.Cy / cell)); if (!drawnCircles.TryGetValue(key, out var l)) drawnCircles[key] = l = []; l.Add(e); }
            foreach (var (idx, c) in circles)
            {
                var (kx, ky) = ((long)Math.Floor(c.Cx / cell), (long)Math.Floor(c.Cy / cell)); var hit = false;
                for (var dx = -1L; dx <= 1 && !hit; dx++) for (var dy = -1L; dy <= 1 && !hit; dy++)
                        if (drawnCircles.TryGetValue((kx + dx, ky + dy), out var l))
                            hit = l.Any(e => Math.Abs(e.Cx - c.Cx) + Math.Abs(e.R - c.R) <= tolU && Math.Abs(e.Cy - c.Cy) + Math.Abs(e.R - c.R) <= tolU);
                if (hit) { drop.Add(idx); same.Add(idx); }
            }
        }
        // T-0747-fix10: Kandidaten über den Rahmen der Form (der gemeldete Rahmen kann vorsichtig größer sein, z. B. Vollkreis bei Bögen im Uhrzeigersinn)
        var shapeBox = fills.Where(f => f.Shape is not null).ToDictionary(f => f.Index, f => BoxOf(f.Shape!));
        var want = fills.Where(f => f.Shape is not null && !same.Contains(f.Index) && !unread[f.Index].B.IsAll && Near(coarseIdx, coarseBox, shapeBox[f.Index], tolU + 0.0011 / kUnits).Any()).ToList();
        if (want.Count > 0)
        {
            // feine Konturen nur aus Elementen, die ganz in einem Kandidaten-Rahmen liegen
            var areas = want.Select(f => shapeBox[f.Index].Grow(0.2 / kUnits)).ToList();
            var ag = new SegGrid(Math.Max(Math.Max(cell, areas.Average(a => Math.Max(a.MaxX - a.MinX, a.MaxY - a.MinY))), areas.Max(a => Math.Max(a.MaxX - a.MinX, a.MaxY - a.MinY)) / 256));
            for (var j = 0; j < areas.Count; j++) ag.Add((areas[j].MinX, areas[j].MinY), (areas[j].MaxX, areas[j].MaxY), j);
            var pick = new List<CadEntity>();
            foreach (var e in vis)
            {
                ct.ThrowIfCancellationRequested();
                var eb = BoxOf(e.Kind is "kreis" or "bogen" ? [(e.Cx - e.R, e.Cy - e.R), (e.Cx + e.R, e.Cy + e.R)] : e.Outline(64));
                if (eb.IsEmpty || eb.HasNaN || eb.IsAll) continue;
                var cx = (eb.MinX + eb.MaxX) / 2; var cy = (eb.MinY + eb.MaxY) / 2;
                if (ag.Near(cx, cy, cx, cy).Select(s => areas[ag.Segs[s].Tag]).Any(a => eb.MinX >= a.MinX && eb.MinY >= a.MinY && eb.MaxX <= a.MaxX && eb.MaxY <= a.MaxY)) pick.Add(e);
            }
            var fine = CadContours.Build(pick, 0.1 / kUnits, fineChord, ct).Where(c => c.Closed).ToList();
            var fineBox = fine.Select(c => BoxOf(c.Pts)).ToArray(); var fineIdx = Index(fineBox);
            var shapeTol = tolU + 2 * fineChord; // Zerlegungsfehler: Kontur ≤ 0,0001 mm, Schraffur ≤ 0,0001 mm (tol/100)
            var fineGrid = new Dictionary<int, SegGrid>(); // T-0747-fix10: Raster je Kontur einmal
            foreach (var f in want)
            {
                ct.ThrowIfCancellationRequested();
                var ub = shapeBox[f.Index]; SegGrid? ga = null;
                foreach (var j in Near(fineIdx, fineBox, ub, shapeTol))
                {
                    if (f.Shape!.Count > MaxShapePoints || fine[j].Pts.Count > MaxShapePoints) continue;
                    if (!fineGrid.TryGetValue(j, out var gc)) fineGrid[j] = gc = Grid(fine[j].Pts, shapeTol * 4, ct);
                    ga ??= Grid(f.Shape, shapeTol * 4, ct);
                    if (SameShape(f.Shape, fine[j].Pts, shapeTol, ct, ga, gc)) { drop.Add(f.Index); same.Add(f.Index); break; }
                }
            }
        }
        // ausgeblendete Ebene: ganz in einer gezeichneten Kontur → zählt nicht
        var hid = fills.Where(f => !drop.Contains(f.Index) && f.Shape is not null && !f.Ambiguous && hidden.Contains(f.Layer) && !unread[f.Index].B.IsAll).ToList();
        if (hid.Count > 0 && coarse.Count > 0)
        {
            var cg = new SegGrid(Math.Max(Math.Max(cell, coarseBox.Average(b => Math.Max(b.MaxX - b.MinX, b.MaxY - b.MinY)) / 4), coarseBox.Max(b => Math.Max(b.MaxX - b.MinX, b.MaxY - b.MinY)) / 256));
            for (var j = 0; j < coarse.Count; j++) cg.Add((coarseBox[j].MinX, coarseBox[j].MinY), (coarseBox[j].MaxX, coarseBox[j].MaxY), j);
            var cache = new Dictionary<int, (SegGrid G, PolyIndex P)>(); // T-0747-fix10: je grober Kontur einmal, bei Bedarf
            (SegGrid G, PolyIndex P) Idx(int j) => cache.TryGetValue(j, out var v) ? v : cache[j] = (Grid(coarse[j].Pts, tolU * 4, ct), new PolyIndex(coarse[j].Pts, ct));
            foreach (var f in hid)
            {
                ct.ThrowIfCancellationRequested();
                var ub = unread[f.Index].B; var cx = (ub.MinX + ub.MaxX) / 2; var cy = (ub.MinY + ub.MaxY) / 2;
                var cand = cg.Near(cx, cy, cx, cy).Select(s => cg.Segs[s].Tag).Distinct()
                    .Where(j => coarseBox[j].MinX <= ub.MinX + tolU && coarseBox[j].MinY <= ub.MinY + tolU && coarseBox[j].MaxX >= ub.MaxX - tolU && coarseBox[j].MaxY >= ub.MaxY - tolU).ToList();
                if (!cand.Any(j => InsideContour(f.Shape!, coarse[j], tolU, ct, () => Idx(j)))) continue;
                // T-0747-fix10: liegt in der Schraffur eine gezeichnete Kontur, die selbst wieder etwas enthält (z. B. Loch mit Teil darin), änderte ein Umriss
                // der Schraffur die Rolle des Inneren – dann nicht verwerfen. Löcher ohne Inhalt (direkt in der Platte) bleiben Löcher.
                var fp = new PolyIndex(f.Shape!, ct);
                bool In(int x) => coarseBox[x].MinX >= ub.MinX - tolU && coarseBox[x].MinY >= ub.MinY - tolU && coarseBox[x].MaxX <= ub.MaxX + tolU && coarseBox[x].MaxY <= ub.MaxY + tolU
                    && fp.Contains(coarse[x].Pts[0].X, coarse[x].Pts[0].Y);
                var inner = cg.Near(ub.MinX, ub.MinY, ub.MaxX, ub.MaxY).Select(s => cg.Segs[s].Tag).Distinct().Where(x => !cand.Contains(x) && In(x)).ToList();
                bool Nested(int x) => cg.Near(coarseBox[x].MinX, coarseBox[x].MinY, coarseBox[x].MaxX, coarseBox[x].MaxY).Select(s => cg.Segs[s].Tag).Distinct()
                    .Any(y => y != x && coarseBox[y].MinX >= coarseBox[x].MinX && coarseBox[y].MinY >= coarseBox[x].MinY && coarseBox[y].MaxX <= coarseBox[x].MaxX && coarseBox[y].MaxY <= coarseBox[x].MaxY
                        && Idx(x).P.Contains(coarse[y].Pts[0].X, coarse[y].Pts[0].Y));
                if (!inner.Any(Nested)) drop.Add(f.Index);
            }
        }
        return (drop, same);
    }

    public static void Scale(CadDrawing d, double k)
    {
        foreach (var e in d.Entities)
        {
            foreach (var pt in e.Points) { pt[0] *= k; pt[1] *= k; }
            e.Cx *= k; e.Cy *= k; e.R *= k; e.Height *= k;
        }
    }

    static bool Has(List<(int Code, string Value)> b, int code) => b.Any(x => x.Code == code);
    static string? Str(List<(int Code, string Value)> b, int code) => b.FirstOrDefault(x => x.Code == code).Value?.Trim();
    static int Int(List<(int Code, string Value)> b, int code, int def) => b.FirstOrDefault(x => x.Code == code) is { Value: { } v } && int.TryParse(v.Trim(), NumberStyles.Integer, Inv, out var i) ? i : def;
    static double Num(List<(int Code, string Value)> b, int code, double def = 0) => b.FirstOrDefault(x => x.Code == code) is { Value: { } v } ? D(v) : def;
    static double D(string v) => double.TryParse(v.Trim(), NumberStyles.Float, Inv, out var x) ? x : throw new CadError($"„{v.Trim()}“ ist keine Zahl.");

    /// <summary>Zeichnung als DXF R12 (AC1009, Einheiten mm) – Ebenen mit Farbe und „aus“-Zustand, Polylinien mit Ausbuchtungen.
    /// <paramref name="extra"/> sind zusätzliche Linien (z. B. Umriss der Körper von oben) auf einer eigenen Ebene.</summary>
    public static string Write(CadDrawing d, IReadOnlyList<(string Layer, double X1, double Y1, double X2, double Y2)>? extra = null)
    {
        var sb = new StringBuilder();
        void G(int code, object v) => sb.Append(code).Append('\n').Append(Convert.ToString(v, Inv)).Append('\n');
        static string N(double v) => v.ToString("0.############", Inv);
        G(999, OwnMarker); // T-0747-fix: Kennung (Kommentar), damit Texte beim Wiedereinlesen wörtlich bleiben
        G(0, "SECTION"); G(2, "HEADER"); G(9, "$ACADVER"); G(1, "AC1009"); G(9, "$INSUNITS"); G(70, 4); G(0, "ENDSEC");
        var layers = d.Layers.Select(l => (l.Name, l.Color, l.Visible)).ToList();
        foreach (var name in (extra ?? []).Select(e => e.Layer).Distinct()) if (layers.All(l => !l.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) layers.Add((name, 8, true));
        if (layers.All(l => l.Name != "0")) layers.Insert(0, ("0", 7, true));
        G(0, "SECTION"); G(2, "TABLES"); G(0, "TABLE"); G(2, "LAYER"); G(70, layers.Count);
        foreach (var (n, c, vis) in layers) { G(0, "LAYER"); G(2, n); G(70, 0); G(62, vis ? c : -c); G(6, "CONTINUOUS"); }
        G(0, "ENDTAB"); G(0, "ENDSEC");
        G(0, "SECTION"); G(2, "ENTITIES");
        foreach (var e in d.Entities)
            switch (e.Kind)
            {
                case "linie" when e.Points.Count >= 2:
                    G(0, "LINE"); G(8, e.Layer); G(10, N(e.Points[0][0])); G(20, N(e.Points[0][1])); G(30, 0); G(11, N(e.Points[1][0])); G(21, N(e.Points[1][1])); G(31, 0); break;
                case "polylinie" when e.Points.Count >= 2:
                    G(0, "POLYLINE"); G(8, e.Layer); G(66, 1); G(10, 0); G(20, 0); G(30, 0); G(70, e.Closed ? 1 : 0);
                    foreach (var pt in e.Points) { G(0, "VERTEX"); G(8, e.Layer); G(10, N(pt[0])); G(20, N(pt[1])); G(30, 0); if (pt.Length > 2 && Math.Abs(pt[2]) > 1e-12) G(42, N(pt[2])); }
                    G(0, "SEQEND"); G(8, e.Layer); break;
                case "kreis": G(0, "CIRCLE"); G(8, e.Layer); G(10, N(e.Cx)); G(20, N(e.Cy)); G(30, 0); G(40, N(e.R)); break;
                case "bogen": G(0, "ARC"); G(8, e.Layer); G(10, N(e.Cx)); G(20, N(e.Cy)); G(30, 0); G(40, N(e.R)); G(50, N(e.A0)); G(51, N(e.A1)); break;
                case "text" when e.Points.Count >= 1:
                    foreach (var (line, i) in e.Text.Split('\n').Select((l, i) => (l, i)))
                    {
                        var y = e.Points[0][1] - i * e.Height * 1.4;
                        G(0, "TEXT"); G(8, e.Layer); G(10, N(e.Points[0][0])); G(20, N(y)); G(30, 0); G(40, N(e.Height)); G(1, line); if (Math.Abs(e.Rotation) > 1e-9) G(50, N(e.Rotation));
                    }
                    break;
            }
        foreach (var (layer, x1, y1, x2, y2) in extra ?? []) { G(0, "LINE"); G(8, layer); G(10, N(x1)); G(20, N(y1)); G(30, 0); G(11, N(x2)); G(21, N(y2)); G(31, 0); }
        G(0, "ENDSEC"); G(0, "EOF");
        return sb.ToString();
    }

    /// <summary>Kurzbeschreibung: „12 Elemente auf 3 Ebenen (5 Linien, 2 Kreise …)“.</summary>
    public static string Summary(CadDrawing d)
    {
        var de = CultureInfo.GetCultureInfo("de-DE");
        var kinds = d.Entities.GroupBy(e => e.Kind).Select(g => $"{g.Count()} {Label(g.Key, g.Count())}");
        return string.Format(de, "{0} Elemente auf {1} Ebenen ({2})", d.Entities.Count, d.Layers.Count, string.Join(", ", kinds));
        static string Label(string k, int n) => k switch { "linie" => n == 1 ? "Linie" : "Linien", "polylinie" => n == 1 ? "Polylinie" : "Polylinien", "kreis" => n == 1 ? "Kreis" : "Kreise", "bogen" => n == 1 ? "Bogen" : "Bögen", _ => n == 1 ? "Text" : "Texte" };
    }
}

/// <summary>T-0691: Referenzkörper im CAD (eingelesenes STL oder facettiertes STEP): wird angezeigt und vermessen, aber nicht in den Bauplan gerechnet.</summary>
public sealed class CadReference
{
    public string Name { get; set; } = "Referenz";
    public string Source { get; set; } = "";
    public string Color { get; set; } = "#9CA3AF";
    public bool Visible { get; set; } = true;
    public float[] Positions { get; set; } = [];
    public int[] Indices { get; set; } = [];
    public CadReference Clone() => new() { Name = Name, Source = Source, Color = Color, Visible = Visible, Positions = (float[])Positions.Clone(), Indices = (int[])Indices.Clone() };
    /// <summary>T-0716: wie <see cref="Clone"/>, das Netz wird geteilt (es wird nie verändert) – für Rückgängig-Schritte (<see cref="CadDoc.Snapshot"/>).</summary>
    public CadReference Share() => new() { Name = Name, Source = Source, Color = Color, Visible = Visible, Positions = Positions, Indices = Indices };

    public Solid ToSolid()
    {
        var s = new Solid();
        for (var t = 0; t + 2 < Indices.Length; t += 3)
        {
            Vec3 P(int i) => new(Positions[i * 3], Positions[i * 3 + 1], Positions[i * 3 + 2]);
            if ((uint)Indices[t] * 3 + 2 >= Positions.Length || (uint)Indices[t + 1] * 3 + 2 >= Positions.Length || (uint)Indices[t + 2] * 3 + 2 >= Positions.Length) continue;
            if (Poly.TryCreate([P(Indices[t]), P(Indices[t + 1]), P(Indices[t + 2])]) is { } p) s.Polys.Add(p);
        }
        return s;
    }

    public static CadReference FromMesh(string name, string source, Mesh m)
    {
        var w = Mesh3D.Welded(m);
        var pos = new float[w.V.Count * 3];
        for (var i = 0; i < w.V.Count; i++) { pos[i * 3] = (float)w.V[i].X; pos[i * 3 + 1] = (float)w.V[i].Y; pos[i * 3 + 2] = (float)w.V[i].Z; }
        return new CadReference { Name = name, Source = source, Positions = pos, Indices = [.. w.I] };
    }
}

/// <summary>T-0691: STEP lesen, soweit es ohne Geometrie-Kern geht: facettierte Körper (FACETED_BREP / POLY_LOOP), wie Trendo und viele
/// Netz-Programme sie schreiben. STEP mit echten Flächen (ADVANCED_FACE, B-Splines, Zylinderflächen) bräuchte einen B-Rep-Kern
/// (z. B. OpenCascade) – dafür gibt es eine klare Meldung statt eines halben Ergebnisses.</summary>
public static class CadStepRead
{
    public static Mesh ReadFacetted(string path)
    {
        if (new FileInfo(path).Length > 256L * 1024 * 1024) throw new CadError("Die STEP-Datei ist zu groß (über 256 MB).");
        var text = File.ReadAllText(path);
        if (!text.Contains("ISO-10303-21", StringComparison.Ordinal)) throw new CadError("Das ist keine STEP-Datei (ISO 10303-21).");
        var ents = new Dictionary<int, (string Type, string Args)>();
        var data = text.IndexOf("DATA;", StringComparison.Ordinal); if (data < 0) throw new CadError("In der STEP-Datei fehlt der DATA-Abschnitt.");
        foreach (var stmt in Statements(text[(data + 5)..]))
        {
            var s = stmt.Trim(); if (!s.StartsWith('#')) continue;
            var eq = s.IndexOf('='); if (eq < 0) continue;
            if (!int.TryParse(s[1..eq].Trim(), out var id)) continue;
            var rest = s[(eq + 1)..].Trim(); var par = rest.IndexOf('(');
            if (par <= 0) { ents[id] = (rest, ""); continue; }
            ents[id] = (rest[..par].Trim().ToUpperInvariant(), rest[(par + 1)..^1]);
        }
        var loops = ents.Where(e => e.Value.Type == "POLY_LOOP").ToList();
        if (loops.Count == 0)
        {
            var exact = ents.Values.Count(e => e.Type is "ADVANCED_FACE" or "B_SPLINE_SURFACE_WITH_KNOTS" or "CYLINDRICAL_SURFACE" or "MANIFOLD_SOLID_BREP");
            throw new CadError(exact > 0
                ? "Diese STEP-Datei beschreibt echte Flächen (B-Rep mit Rundungen). Trendo liest nur facettierte STEP-Dateien. Weg: im Ursprungsprogramm als STL oder facettiertes STEP speichern."
                : "In der STEP-Datei ist kein Körper, den Trendo lesen kann.");
        }
        var m = new Mesh();
        foreach (var (_, (_, args)) in loops)
        {
            var refs = System.Text.RegularExpressions.Regex.Matches(args, @"#(\d+)").Select(x => int.Parse(x.Groups[1].Value, CultureInfo.InvariantCulture)).ToList();
            var pts = refs.Select(r => ents.TryGetValue(r, out var e) && e.Type == "CARTESIAN_POINT" ? Point(e.Args) : (Vec3?)null).OfType<Vec3>().ToList();
            for (var i = 1; i + 1 < pts.Count; i++) m.Add(pts[0], pts[i], pts[i + 1]); // Fächer (facettierte Flächen sind eben und konvex)
        }
        if (m.Triangles == 0) throw new CadError("Die STEP-Datei enthält keine lesbaren Flächen.");
        return m;
    }

    static Vec3 Point(string args)
    {
        var a = args.IndexOf('('); var b = args.LastIndexOf(')');
        var nums = args[(a + 1)..b].Split(',').Select(x => double.Parse(x.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture)).ToArray();
        return new Vec3(nums[0], nums.Length > 1 ? nums[1] : 0, nums.Length > 2 ? nums[2] : 0);
    }

    /// <summary>Anweisungen bis „;“ – Semikolons in Zeichenketten zählen nicht.</summary>
    static IEnumerable<string> Statements(string s)
    {
        var sb = new StringBuilder(); var inStr = false;
        foreach (var c in s)
        {
            if (c == '\'') inStr = !inStr;
            if (c == ';' && !inStr) { var t = sb.ToString(); sb.Clear(); if (t.Trim() == "ENDSEC") yield break; yield return t; continue; }
            sb.Append(c);
        }
    }
}
