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
    public CadEntity Clone() => new() { Kind = Kind, Layer = Layer, Points = Points.Select(p => (double[])p.Clone()).ToList(), Closed = Closed, Cx = Cx, Cy = Cy, R = R, A0 = A0, A1 = A1, Text = Text, Height = Height, Rotation = Rotation };

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

    public static ReadResult Read(string path)
    {
        if (new FileInfo(path).Length > 64L * 1024 * 1024) throw new CadError("Die DXF-Datei ist zu groß (über 64 MB).");
        var text = Decode(File.ReadAllBytes(path));
        if (text.StartsWith("AutoCAD Binary DXF", StringComparison.Ordinal)) throw new CadError("Binäre DXF-Dateien liest Trendo nicht – bitte als ASCII-DXF speichern.");
        return Parse(text);
    }

    /// <summary>T-0747: Text der Datei – UTF-8 (ab AutoCAD 2007 und aus vielen Programmen, auch Trendos eigener Export), sonst Westeuropa wie bisher.</summary>
    static string Decode(byte[] bytes)
    {
        try { return new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF'); }
        catch (DecoderFallbackException) { return Encoding.Latin1.GetString(bytes); }
    }

    /// <summary>T-0747: DXF-Sonderzeichen in Texten: \U+00B2 → ², %%c → Ø, %%d → °, %%p → ±.</summary>
    static string Unescape(string s) => System.Text.RegularExpressions.Regex.Replace(s, @"\\U\+([0-9A-Fa-f]{4})", m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString())
        .Replace("%%c", "Ø", StringComparison.OrdinalIgnoreCase).Replace("%%d", "°", StringComparison.OrdinalIgnoreCase).Replace("%%p", "±", StringComparison.OrdinalIgnoreCase);

    public static ReadResult Parse(string text)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var pairs = new List<(int Code, string Value)>(lines.Length / 2);
        for (var i = 0; i + 1 < lines.Length; i += 2)
        {
            if (!int.TryParse(lines[i].Trim(), NumberStyles.Integer, Inv, out var code)) throw new CadError($"Zeile {i + 1}: „{lines[i].Trim()}“ ist kein DXF-Gruppencode.");
            pairs.Add((code, lines[i + 1].TrimEnd()));
        }
        var d = new CadDrawing(); var skipped = new Dictionary<string, int>();
        string section = ""; var p = 0; var units = 0;
        while (p < pairs.Count)
        {
            var (code, val) = pairs[p];
            if (code == 0 && val == "SECTION") { section = p + 1 < pairs.Count && pairs[p + 1].Code == 2 ? pairs[p + 1].Value.Trim() : ""; p += 2; continue; }
            if (code == 0 && val == "ENDSEC") { section = ""; p++; continue; }
            if (code == 0 && val == "EOF") break;
            if (section == "HEADER" && code == 9 && val.Trim() == "$INSUNITS")
            {
                if (p + 1 < pairs.Count && pairs[p + 1].Code == 70 && int.TryParse(pairs[p + 1].Value.Trim(), NumberStyles.Integer, Inv, out var u)) units = u;
                p += 2; continue;
            }
            if (code != 0) { p++; continue; }
            var start = p; p++;
            var body = new List<(int Code, string Value)>();
            while (p < pairs.Count && pairs[p].Code != 0) body.Add(pairs[p++]);
            if (section == "TABLES" && val == "LAYER")
            {
                var name = Str(body, 2);
                if (name is { Length: > 0 }) { var l = d.Layer(name); var c = Int(body, 62, 7); l.Color = Math.Abs(c) is >= 1 and <= 255 ? Math.Abs(c) : 7; l.Visible = c >= 0; }
                continue;
            }
            if (section != "ENTITIES" && section != "BLOCKS") continue;
            if (section == "BLOCKS") { if (val is not ("BLOCK" or "ENDBLK")) Count(skipped, "Element in Blockdefinition"); continue; }
            var layer = Str(body, 8) is { Length: > 0 } ln ? ln : "0";
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
                    while (p < pairs.Count && pairs[p].Code == 0 && pairs[p].Value == "VERTEX")
                    {
                        p++; var vb = new List<(int, string)>(); while (p < pairs.Count && pairs[p].Code != 0) vb.Add(pairs[p++]);
                        e.Points.Add([Num(vb, 10), Num(vb, 20), Num(vb, 42)]);
                    }
                    if (p < pairs.Count && pairs[p].Code == 0 && pairs[p].Value == "SEQEND") { p++; while (p < pairs.Count && pairs[p].Code != 0) p++; }
                    d.Entities.Add(e);
                    break;
                }
                case "CIRCLE":
                    d.Entities.Add(new CadEntity { Kind = "kreis", Layer = layer, Cx = Num(body, 10), Cy = Num(body, 20), R = Num(body, 40) });
                    break;
                case "ARC":
                    d.Entities.Add(new CadEntity { Kind = "bogen", Layer = layer, Cx = Num(body, 10), Cy = Num(body, 20), R = Num(body, 40), A0 = Num(body, 50), A1 = Num(body, 51) });
                    break;
                case "TEXT":
                {
                    // ausgerichteter Text: Ausrichtungspunkt (11/21) gilt, sobald eine Ausrichtung gesetzt ist
                    var aligned = Int(body, 72, 0) != 0 || Int(body, 73, 0) != 0;
                    var x = aligned && Has(body, 11) ? Num(body, 11) : Num(body, 10); var y = aligned && Has(body, 21) ? Num(body, 21) : Num(body, 20);
                    d.Entities.Add(new CadEntity { Kind = "text", Layer = layer, Points = [[x, y, 0]], Text = Unescape(Str(body, 1) ?? ""), Height = Num(body, 40, 2.5), Rotation = Num(body, 50) });
                    break;
                }
                case "MTEXT":
                {
                    var t = string.Concat(body.Where(b => b.Code == 3).Select(b => b.Value)) + (Str(body, 1) ?? "");
                    t = System.Text.RegularExpressions.Regex.Replace(Unescape(t).Replace("\\P", "\n"), @"\\[A-Za-z][^;\\]*;|[{}]", "");
                    d.Entities.Add(new CadEntity { Kind = "text", Layer = layer, Points = [[Num(body, 10), Num(body, 20), 0]], Text = t, Height = Num(body, 40, 2.5), Rotation = Num(body, 50) });
                    break;
                }
                case "SEQEND" or "VERTEX": break;
                default: Count(skipped, val); break;
            }
            _ = start;
        }
        foreach (var name in d.Entities.Select(e => e.Layer).Distinct().ToList()) d.Layer(name);
        if (d.Entities.Count == 0 && skipped.Count == 0) throw new CadError("In der DXF-Datei ist keine Zeichnung (kein ENTITIES-Abschnitt mit Linien, Kreisen, Bögen oder Text).");
        var k = MmPerUnitOf(units);
        if (Math.Abs(k - 1) > 1e-12) Scale(d, k); // T-0747: Zoll, cm, m … gleich in mm
        return new ReadResult(d, skipped) { Units = units, MmPerUnit = k };

        static void Count(Dictionary<string, int> s, string k) => s[k] = s.GetValueOrDefault(k) + 1;
        static void SkipVertices(List<(int Code, string Value)> pairs, ref int p)
        {
            while (p < pairs.Count && !(pairs[p].Code == 0 && pairs[p].Value == "SEQEND")) p++;
            if (p < pairs.Count) { p++; while (p < pairs.Count && pairs[p].Code != 0) p++; }
        }
    }

    /// <summary>Alle Maße mit <paramref name="k"/> multiplizieren (Ausbuchtungen und Winkel bleiben).</summary>
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
