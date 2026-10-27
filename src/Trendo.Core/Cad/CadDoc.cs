using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Trendo.Core.Cad;

/// <summary>N23 Trendo CAD: Art eines Bauschritts. Grundkörper fügen hinzu, Bohrung/Tasche ziehen ab, Rundung/Fase ändern Kanten eines Quaders,
/// Muster/Spiegeln/Verschieben/Drehen verändern den bisherigen Körper.</summary>
public enum CadOp { Quader, Zylinder, Kegel, Kugel, Extrusion, Drehteil, Bohrung, Tasche, Rundung, Fase, Muster, Spiegeln, Verschieben, Drehen }

/// <summary>Skizze in der XY-Ebene: Umriss als Rechteck, Kreis oder Vieleck (Punkte als Formeln „x;y“).</summary>
public sealed class CadSketch
{
    public string Kind { get; set; } = "rechteck";   // rechteck | kreis | vieleck
    public string X { get; set; } = "0";
    public string Y { get; set; } = "0";
    public string W { get; set; } = "20";
    public string H { get; set; } = "10";
    public string R { get; set; } = "5";
    public List<string> Points { get; set; } = [];
}

/// <summary>Ein Bauschritt. Alle Maße sind Formeln (z. B. „Breite/2 + 3“) mit den Parametern des Dokuments.</summary>
public sealed class CadFeature
{
    public CadOp Op { get; set; }
    public string Name { get; set; } = "";
    public Dictionary<string, string> P { get; set; } = [];
    public CadSketch? Sketch { get; set; }
    public bool Off { get; set; }

    public string this[string key] { get => P.GetValueOrDefault(key, "0"); set => P[key] = value; }
    public CadFeature Clone() => new() { Op = Op, Name = Name, P = new(P), Off = Off, Sketch = Sketch is null ? null : JsonSerializer.Deserialize<CadSketch>(JsonSerializer.Serialize(Sketch)) };
}

/// <summary>Ein Körper (Teil) der Baugruppe mit eigener Lage und Farbe.</summary>
public sealed class CadBody
{
    public string Name { get; set; } = "Teil 1";
    public string Color { get; set; } = "#2F86F6";
    public List<CadFeature> Features { get; set; } = [];
    public string X { get; set; } = "0";
    public string Y { get; set; } = "0";
    public string Z { get; set; } = "0";
    public bool Hidden { get; set; }
    public CadBody Clone() => new() { Name = Name, Color = Color, X = X, Y = Y, Z = Z, Hidden = Hidden, Features = Features.Select(f => f.Clone()).ToList() };
}

public sealed class CadDoc
{
    /// <summary>Benannte Maße, z. B. Breite = 40. Formeln in den Bauschritten greifen darauf zu.</summary>
    public Dictionary<string, double> Params { get; set; } = [];
    public List<CadBody> Bodies { get; set; } = [new()];
    public int Segments { get; set; } = 48;

    public CadDoc Clone() => new() { Params = new(Params), Bodies = Bodies.Select(b => b.Clone()).ToList(), Segments = Segments };

    static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    public string ToJson() => JsonSerializer.Serialize(this, Json);
    public static CadDoc FromJson(string json) => JsonSerializer.Deserialize<CadDoc>(json, Json) ?? new CadDoc();
    public void Save(string path) { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!); File.WriteAllText(path, ToJson()); }
    public static CadDoc Load(string path) => FromJson(File.ReadAllText(path));

    /// <summary>Formel ausrechnen: Zahlen (Komma oder Punkt), + − × ÷ ^, Klammern, Parameter, sin/cos/tan (Grad), wurzel, min, max, pi.</summary>
    public double Eval(string expr)
    {
        var e = new Expr(expr, Params); var v = e.Parse();
        if (double.IsNaN(v) || double.IsInfinity(v)) throw new CadError($"„{expr}“ ergibt keine gültige Zahl.");
        return v;
    }

    /// <summary>Alle sichtbaren Körper ausrechnen – Fehler eines Schritts melden, ohne abzubrechen.</summary>
    public List<(CadBody Body, Solid Solid, string? Error)> Build()
    {
        var res = new List<(CadBody, Solid, string?)>();
        foreach (var b in Bodies)
        {
            if (b.Hidden) continue;
            try { res.Add((b, BuildBody(b), null)); }
            catch (CadError e) { res.Add((b, new Solid(), e.Message)); }
        }
        return res;
    }

    public Solid BuildBody(CadBody b)
    {
        Solid? s = null; var n = 0;
        foreach (var f in b.Features)
        {
            n++;
            if (f.Off) continue;
            try { s = Apply(s, f); }
            catch (CadError e) { throw new CadError($"Schritt {n} „{(f.Name.Length > 0 ? f.Name : f.Op.ToString())}“: {e.Message}"); }
        }
        s ??= new Solid();
        var (dx, dy, dz) = (Eval(b.X), Eval(b.Y), Eval(b.Z));
        return dx == 0 && dy == 0 && dz == 0 ? s : s.Transform(v => v + new Vec3(dx, dy, dz));
    }

    double V(CadFeature f, string key, double? def = null) => f.P.ContainsKey(key) ? Eval(f[key]) : def ?? throw new CadError($"Maß „{key}“ fehlt.");

    Solid Apply(Solid? s, CadFeature f)
    {
        var seg = Math.Clamp(Segments, 8, 256);
        Solid Need() => s ?? throw new CadError("Zuerst einen Grundkörper anlegen (Quader, Zylinder, Extrusion …).");
        Solid Add(Solid add) => s is null ? add : s.Union(add);
        var pos = new Vec3(V(f, "x", 0), V(f, "y", 0), V(f, "z", 0));
        switch (f.Op)
        {
            case CadOp.Quader:
            {
                double w = Pos(V(f, "breite", 20)), d = Pos(V(f, "tiefe", 20)), h = Pos(V(f, "hoehe", 20));
                return Add(CadShapes.Box(pos, w, d, h));
            }
            case CadOp.Zylinder: return Add(CadShapes.Cylinder(pos, Pos(V(f, "radius", 10)), Pos(V(f, "hoehe", 20)), seg));
            case CadOp.Kegel: return Add(CadShapes.Cone(pos, Pos(V(f, "radius", 10)), Math.Max(0, V(f, "radius2", 0)), Pos(V(f, "hoehe", 20)), seg));
            case CadOp.Kugel: return Add(CadShapes.Sphere(pos, Pos(V(f, "radius", 10)), seg));
            case CadOp.Extrusion:
            {
                var prof = Profile(f.Sketch ?? throw new CadError("Die Extrusion braucht eine Skizze."), seg);
                var ext = CadShapes.Extrude(prof, Pos(V(f, "hoehe", 10))).Transform(v => v + new Vec3(0, 0, pos.Z));
                return f["modus"] == "abziehen" ? Need().Subtract(ext) : Add(ext);
            }
            case CadOp.Drehteil:
            {
                // Profil in der XZ-Ebene: Skizzen-X = Abstand zur Achse, Skizzen-Y = Höhe
                var prof = Profile(f.Sketch ?? throw new CadError("Das Drehteil braucht eine Skizze (Umriss neben der Achse)."), seg);
                if (prof.Any(p => p.X < -1e-9)) throw new CadError("Der Umriss darf die Drehachse nicht überschreiten (X ≥ 0).");
                return Add(CadShapes.Revolve(prof, Math.Clamp(V(f, "winkel", 360), 1, 360), seg).Transform(v => v + pos));
            }
            case CadOp.Bohrung:
            {
                var body = Need(); var (min, max) = body.Bounds();
                var r = Pos(V(f, "durchmesser", 5)) / 2; var depth = V(f, "tiefe", 0);
                var top = f.P.ContainsKey("z") ? pos.Z : max.Z;
                var h = depth <= 0 ? top - min.Z + 2 : depth; // 0 = durchgehend
                var hole = CadShapes.Cylinder(new Vec3(pos.X, pos.Y, top - h), r, h + 1, seg);
                var res = body.Subtract(hole);
                if (V(f, "senkung", 0) is var sk && sk > r * 2) res = res.Subtract(CadShapes.Cone(new Vec3(pos.X, pos.Y, top - (sk / 2 - r)), r, sk / 2 + 0.01, sk / 2 - r + 0.5, seg));
                return res;
            }
            case CadOp.Tasche:
            {
                var body = Need(); var (_, max) = body.Bounds();
                var prof = Profile(f.Sketch ?? new CadSketch { X = f["x"], Y = f["y"], W = f.P.GetValueOrDefault("breite", "10"), H = f.P.GetValueOrDefault("tiefe", "10") }, seg);
                var depth = Pos(V(f, "tiefe2", 5));
                var top = f.P.ContainsKey("z") ? pos.Z : max.Z;
                return body.Subtract(CadShapes.Extrude(prof, depth + 1).Transform(v => v + new Vec3(0, 0, top - depth)));
            }
            case CadOp.Rundung or CadOp.Fase:
            {
                // Kanten eines quaderförmigen Körpers: senkrechte Kanten runden/fasen, optional auch oben
                var body = Need(); var (min, max) = body.Bounds();
                var r = Pos(V(f, "radius", 2));
                var cutter = CadShapes.EdgeCutter(min, max, r, f.Op == CadOp.Rundung, f["oben"] == "ja", seg);
                return body.Intersect(cutter);
            }
            case CadOp.Muster:
            {
                var body = Need(); var count = (int)Math.Clamp(Math.Round(V(f, "anzahl", 3)), 1, 50);
                var step = new Vec3(V(f, "dx", 20), V(f, "dy", 0), V(f, "dz", 0));
                var res = body;
                for (var i = 1; i < count; i++) { var k = i; res = res.Union(body.Transform(v => v + step * k)); }
                return res;
            }
            case CadOp.Spiegeln:
            {
                var body = Need(); var ax = f["ebene"].ToUpperInvariant(); var at = V(f, "bei", 0);
                Vec3 M(Vec3 v) => ax switch { "Y" => new Vec3(v.X, 2 * at - v.Y, v.Z), "Z" => new Vec3(v.X, v.Y, 2 * at - v.Z), _ => new Vec3(2 * at - v.X, v.Y, v.Z) };
                var mirrored = body.Transform(M, flip: true);
                return f["behalten"] == "nein" ? mirrored : body.Union(mirrored);
            }
            case CadOp.Verschieben: return Need().Transform(v => v + pos);
            case CadOp.Drehen:
            {
                var a = V(f, "winkel", 90) * Math.PI / 180; var ax = f["achse"].ToUpperInvariant();
                double c = Math.Cos(a), sn = Math.Sin(a);
                return Need().Transform(v => ax switch
                {
                    "X" => new Vec3(v.X, v.Y * c - v.Z * sn, v.Y * sn + v.Z * c),
                    "Y" => new Vec3(v.X * c + v.Z * sn, v.Y, -v.X * sn + v.Z * c),
                    _ => new Vec3(v.X * c - v.Y * sn, v.X * sn + v.Y * c, v.Z),
                });
            }
        }
        throw new CadError("Unbekannter Schritt.");
    }

    static double Pos(double v) => v > 0 ? v : throw new CadError("Maße müssen größer als 0 sein.");

    /// <summary>Umriss der Skizze als Punktfolge (gegen den Uhrzeigersinn).</summary>
    public List<(double X, double Y)> Profile(CadSketch k, int seg)
    {
        List<(double, double)> pts;
        switch (k.Kind)
        {
            case "kreis":
            {
                double cx = Eval(k.X), cy = Eval(k.Y), r = Pos(Eval(k.R));
                pts = Enumerable.Range(0, seg).Select(i => (cx + r * Math.Cos(2 * Math.PI * i / seg), cy + r * Math.Sin(2 * Math.PI * i / seg))).ToList();
                break;
            }
            case "vieleck":
                pts = k.Points.Select(p => { var xy = p.Split(';'); if (xy.Length != 2) throw new CadError($"Punkt „{p}“: bitte als „x;y“ angeben."); return (Eval(xy[0]), Eval(xy[1])); }).ToList();
                if (pts.Count < 3) throw new CadError("Ein Vieleck braucht mindestens 3 Punkte.");
                break;
            default:
            {
                double x = Eval(k.X), y = Eval(k.Y), w = Pos(Eval(k.W)), h = Pos(Eval(k.H));
                pts = [(x, y), (x + w, y), (x + w, y + h), (x, y + h)];
                break;
            }
        }
        // gegen den Uhrzeigersinn ausrichten
        var area = 0.0; for (var i = 0; i < pts.Count; i++) { var (x1, y1) = pts[i]; var (x2, y2) = pts[(i + 1) % pts.Count]; area += x1 * y2 - x2 * y1; }
        if (area < 0) pts.Reverse();
        return pts;
    }

    /// <summary>Neues Teil mit einem Bauschritt.</summary>
    public static CadFeature Feature(CadOp op, params (string Key, string Value)[] p)
    {
        var f = new CadFeature { Op = op, Name = op switch { CadOp.Quader => "Quader", CadOp.Zylinder => "Zylinder", CadOp.Kegel => "Kegel", CadOp.Kugel => "Kugel", CadOp.Extrusion => "Extrusion", CadOp.Drehteil => "Drehteil", CadOp.Bohrung => "Bohrung", CadOp.Tasche => "Tasche", CadOp.Rundung => "Rundung", CadOp.Fase => "Fase", CadOp.Muster => "Muster", CadOp.Spiegeln => "Spiegeln", CadOp.Verschieben => "Verschieben", _ => "Drehen" } };
        foreach (var (k, v) in p) f.P[k] = v;
        return f;
    }

    /// <summary>Maße, die der Schritt anbietet (für die Eigenschaften-Leiste).</summary>
    public static IReadOnlyList<(string Key, string Label, string Default)> Fields(CadOp op) => op switch
    {
        CadOp.Quader => [("breite", "Breite (X)", "40"), ("tiefe", "Tiefe (Y)", "30"), ("hoehe", "Höhe (Z)", "10"), ("x", "Lage X", "0"), ("y", "Lage Y", "0"), ("z", "Lage Z", "0")],
        CadOp.Zylinder => [("radius", "Radius", "10"), ("hoehe", "Höhe", "20"), ("x", "Mitte X", "0"), ("y", "Mitte Y", "0"), ("z", "Lage Z", "0")],
        CadOp.Kegel => [("radius", "Radius unten", "10"), ("radius2", "Radius oben", "0"), ("hoehe", "Höhe", "20"), ("x", "Mitte X", "0"), ("y", "Mitte Y", "0"), ("z", "Lage Z", "0")],
        CadOp.Kugel => [("radius", "Radius", "10"), ("x", "Mitte X", "0"), ("y", "Mitte Y", "0"), ("z", "Mitte Z", "0")],
        CadOp.Extrusion => [("hoehe", "Höhe", "10"), ("z", "Lage Z", "0"), ("modus", "hinzufügen/abziehen", "hinzufügen")],
        CadOp.Drehteil => [("winkel", "Winkel (°)", "360"), ("x", "Achse X", "0"), ("y", "Achse Y", "0"), ("z", "Lage Z", "0")],
        CadOp.Bohrung => [("durchmesser", "Durchmesser", "5"), ("tiefe", "Tiefe (0 = durch)", "0"), ("x", "Mitte X", "10"), ("y", "Mitte Y", "10"), ("senkung", "Senkung Ø (0 = keine)", "0")],
        CadOp.Tasche => [("tiefe2", "Tiefe", "5"), ("x", "Lage X", "5"), ("y", "Lage Y", "5"), ("breite", "Breite", "10"), ("tiefe", "Länge", "10")],
        CadOp.Rundung => [("radius", "Radius", "3"), ("oben", "Oberkante auch (ja/nein)", "nein")],
        CadOp.Fase => [("radius", "Fasenbreite", "2"), ("oben", "Oberkante auch (ja/nein)", "nein")],
        CadOp.Muster => [("anzahl", "Anzahl", "3"), ("dx", "Abstand X", "20"), ("dy", "Abstand Y", "0"), ("dz", "Abstand Z", "0")],
        CadOp.Spiegeln => [("ebene", "Spiegeln an (X/Y/Z)", "X"), ("bei", "bei Wert", "0"), ("behalten", "Original behalten (ja/nein)", "ja")],
        CadOp.Verschieben => [("x", "X", "10"), ("y", "Y", "0"), ("z", "Z", "0")],
        _ => [("achse", "Achse (X/Y/Z)", "Z"), ("winkel", "Winkel (°)", "90")],
    };

    /// <summary>Formel-Rechner mit Parametern.</summary>
    sealed class Expr(string s, IReadOnlyDictionary<string, double> pars)
    {
        int i;
        public double Parse() { var v = Sum(); Ws(); if (i < s.Length) throw new CadError($"In „{s}“ ist „{s[i..]}“ unverständlich."); return v; }
        void Ws() { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }
        bool Eat(char c) { Ws(); if (i < s.Length && s[i] == c) { i++; return true; } return false; }
        double Sum() { var v = Prod(); while (true) { if (Eat('+')) v += Prod(); else if (Eat('-') || Eat('−')) v -= Prod(); else return v; } }
        double Prod() { var v = Pow(); while (true) { if (Eat('*') || Eat('×')) v *= Pow(); else if (Eat('/') || Eat('÷')) v /= Pow(); else return v; } }
        double Pow() { var v = Unary(); return Eat('^') ? Math.Pow(v, Pow()) : v; }
        double Unary() => Eat('-') || Eat('−') ? -Unary() : Eat('+') ? Unary() : Atom();
        double Atom()
        {
            Ws();
            if (Eat('(')) { var v = Sum(); if (!Eat(')')) throw new CadError($"In „{s}“ fehlt eine „)“."); return v; }
            var st = i;
            if (i < s.Length && (char.IsDigit(s[i]) || s[i] is '.' or ','))
            {
                while (i < s.Length && (char.IsDigit(s[i]) || s[i] is '.' or ',')) i++;
                return double.Parse(s[st..i].Replace(',', '.'), CultureInfo.InvariantCulture);
            }
            while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i++;
            var name = s[st..i];
            if (name.Length == 0) throw new CadError($"In „{s}“ fehlt eine Zahl.");
            if (Eat('('))
            {
                var args = new List<double> { Sum() };
                while (Eat(';') || Eat(',')) args.Add(Sum());
                if (!Eat(')')) throw new CadError($"In „{s}“ fehlt eine „)“.");
                return name.ToLowerInvariant() switch
                {
                    "sin" => Math.Sin(args[0] * Math.PI / 180), "cos" => Math.Cos(args[0] * Math.PI / 180), "tan" => Math.Tan(args[0] * Math.PI / 180),
                    "wurzel" or "sqrt" => Math.Sqrt(args[0]), "min" => args.Min(), "max" => args.Max(), "abs" => Math.Abs(args[0]), "runden" or "round" => Math.Round(args[0]),
                    _ => throw new CadError($"Unbekannte Funktion „{name}“."),
                };
            }
            if (name.Equals("pi", StringComparison.OrdinalIgnoreCase)) return Math.PI;
            foreach (var (k, v) in pars) if (k.Equals(name, StringComparison.CurrentCultureIgnoreCase)) return v;
            throw new CadError($"Unbekanntes Maß „{name}“ – unter „Parameter“ anlegen.");
        }
    }
}

public sealed class CadError(string message) : Exception(message);
