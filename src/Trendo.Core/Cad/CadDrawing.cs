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
                    if (!NoOutline.Contains(val)) unread.Add(((IsFill(val) ? FillPrefix : "") + val, ShapeBox(val, body, [], kUnits)));
                    break;
            }
            if (flip) for (var i = before; i < d.Entities.Count; i++) Mirror(d.Entities[i]); // T-0747-fix4
            _ = start;
        }
        foreach (var (idx, ib) in pendingInserts) unread[idx] = (unread[idx].What, InsertBox(ib, 0));
        foreach (var name in d.Entities.Select(e => e.Layer).Distinct().ToList()) d.Layer(name);
        if (d.Entities.Count == 0 && skipped.Count == 0) throw new CadError("In der DXF-Datei ist keine Zeichnung (kein ENTITIES-Abschnitt mit Linien, Kreisen, Bögen oder Text).");
        var k = MmPerUnitOf(units);
        if (Math.Abs(k - 1) > 1e-12) Scale(d, k); // T-0747: Zoll, cm, m … gleich in mm
        return new ReadResult(d, skipped) { Units = units, MmPerUnit = k, Unread = unread.Select(x => (x.What, x.B.Scale(k))).ToList() };

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
        public Box Union(Box o) => IsEmpty ? o : o.IsEmpty ? this : new(Math.Min(MinX, o.MinX), Math.Min(MinY, o.MinY), Math.Max(MaxX, o.MaxX), Math.Max(MaxY, o.MaxY));
        public Box With((double X, double Y) q) => Union(new Box(q.X, q.Y, q.X, q.Y));
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
            if (!(c is >= 10 and <= 39 or >= 40 and <= 42)) continue;
            if (!double.TryParse(v.Trim(), NumberStyles.Float, Inv, out var x)) continue; // kein Zahlwert: meldet der Leser wie bisher
            if (double.IsNaN(x) || double.IsInfinity(x)) return false;
            if (c is >= 10 and <= 40 && Math.Abs(x) * k > 1e9) return false;
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
        "ACAD_TABLE", "SEQEND", "VERTEX", "BLOCK", "ENDBLK", "LIGHT", "SUN", "ACAD_PROXY_ENTITY_TEXT",
    };

    static bool IsFill(string val) => val is "HATCH" or "SOLID" or "TRACE" or "3DFACE";

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
            case "MLINE" or "REGION" or "3DSOLID" or "BODY" or "SURFACE" or "MESH" or "WIPEOUT": return Box.All; // Ausdehnung nicht sicher bestimmbar
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
            case "HATCH":
            {
                // Randpunkte; Bögen liegen im Radius (40) um ihren Mittelpunkt, Ellipsen in |Hauptachse| (11/21), Ausbuchtungen höchstens
                // max(1, |b|) · halbe Sehne von der Sehnenmitte – vorsichtig: halbe Diagonale aller Punkte.
                var z = Num(b, 30); foreach (var (x, y) in Pairs(b, 10)) o.Pt(x, y, z);
                double g = 0, maxB = 0;
                foreach (var (c, s) in b)
                {
                    if (!double.TryParse(s.Trim(), NumberStyles.Float, Inv, out var w)) continue;
                    if (c == 40) g = Math.Max(g, Math.Abs(w)); else if (c == 42) maxB = Math.Max(maxB, Math.Abs(w));
                }
                foreach (var (x, y) in Pairs(b, 11)) g = Math.Max(g, Math.Sqrt(x * x + y * y));
                if (maxB > 1e-12 && !o.Box.IsEmpty) g = Math.Max(g, Math.Sqrt(Math.Pow(o.Box.MaxX - o.Box.MinX, 2) + Math.Pow(o.Box.MaxY - o.Box.MinY, 2)) / 2 * Math.Max(1, maxB));
                return o.Box.Grow(g);
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
