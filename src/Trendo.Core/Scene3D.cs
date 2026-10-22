using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core;

public sealed record SceneMod(string Art, double Wert, string Achse);

/// <summary>Ein Teil: Grundform oder eingelesene Datei, dazu Maße, Lage und Werkzeuge.</summary>
public sealed class ScenePart
{
    public string Name { get; set; } = "Teil";
    public string Form { get; set; } = "wuerfel";
    public double Bx { get; set; } = 10;
    public double By { get; set; } = 10;
    public double Bz { get; set; } = 10;
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public double Rx { get; set; }
    public double Ry { get; set; }
    public double Rz { get; set; }
    public string Farbe { get; set; } = "#2F86F6";
    public string Text { get; set; } = "";
    public List<string> Notiz { get; set; } = [];
    public List<SceneMod> Mods { get; set; } = [];
    /// <summary>Eingelesenes Netz in Zentimetern, schon auf den Ursprung gesetzt. Null bei Grundformen.</summary>
    public Mesh3D? Datei { get; set; }
    public double DateiBx { get; set; } = 1;
    public double DateiBy { get; set; } = 1;
    public double DateiBz { get; set; } = 1;

    public Poly3D Basis()
    {
        if (Form == "datei" && Datei is { } d)
        {
            var sx = Math.Abs(DateiBx) < 1e-6 ? 1 : Bx / DateiBx;
            var sy = Math.Abs(DateiBy) < 1e-6 ? 1 : By / DateiBy;
            var sz = Math.Abs(DateiBz) < 1e-6 ? 1 : Bz / DateiBz;
            var p = new Poly3D();
            foreach (var v in d.V) p.V.Add(new Vec3(v.X * sx, v.Y * sy, v.Z * sz));
            foreach (var (a, b, c) in d.T) p.F.Add([a, b, c]);
            return p;
        }
        return Form switch
        {
            "kugel" => Poly3D.Sphere(Bx, By, Bz),
            "zylinder" => Poly3D.Cylinder(Bx, By, Bz),
            "kegel" => Poly3D.Cone(Bx, By, Bz),
            "torus" => Poly3D.Torus(Bx, By, Bz),
            "ebene" => Poly3D.Box(Bx, By, Math.Max(0.05, Bz)),
            "text" => Poly3D.Text(Text, Bx, By, Bz),
            _ => Poly3D.Box(Bx, By, Bz),
        };
    }

    public Poly3D Gebaut()
    {
        var p = Basis();
        foreach (var m in Mods)
        {
            p = m.Art switch
            {
                "bevel" => Poly3D.Bevel(p, m.Wert),
                "smooth" => Poly3D.Smooth(p),
                "solid" => Poly3D.Solidify(p, m.Wert),
                "hole" => Poly3D.Drill(p, m.Achse.Length > 0 ? m.Achse[0] : 'z', m.Wert, 0, 0),
                _ => p,
            };
        }
        return p.Transform(Rx, Ry, Rz, X, Y, Z);
    }

    public ScenePart Copy()
    {
        var c = new ScenePart
        {
            Name = Name, Form = Form, Bx = Bx, By = By, Bz = Bz, X = X, Y = Y, Z = Z, Rx = Rx, Ry = Ry, Rz = Rz,
            Farbe = Farbe, Text = Text, DateiBx = DateiBx, DateiBy = DateiBy, DateiBz = DateiBz,
            Notiz = Notiz.ToList(), Mods = Mods.ToList(),
        };
        if (Datei is { } d)
        {
            c.Datei = new Mesh3D();
            c.Datei.V.AddRange(d.V); c.Datei.T.AddRange(d.T); c.Datei.Farbe.AddRange(d.Farbe);
        }
        return c;
    }
}

/// <summary>Zusammengebautes Objekt aus mehreren Teilen, mit Rückgängig und Projektstand.</summary>
public sealed partial class Scene3D
{
    public static readonly (string Id, string Label)[] Formen =
    [
        ("wuerfel", "Würfel"), ("kugel", "Kugel"), ("zylinder", "Zylinder"), ("kegel", "Kegel"),
        ("torus", "Torus"), ("ebene", "Ebene"), ("text", "3D-Schrift"),
    ];

    public static readonly (string Hex, string Label)[] Farben =
    [
        ("#2F86F6", "Blau"), ("#E11D48", "Rot"), ("#16A34A", "Grün"), ("#F59E0B", "Gelb"),
        ("#F8FAFC", "Weiß"), ("#111827", "Schwarz"), ("#C4A574", "Holz"), ("#9CA3AF", "Grau"),
    ];

    public string Name { get; set; } = "Mein Modell";
    public List<ScenePart> Teile { get; } = [];
    public int Auswahl { get; set; } = -1;
    public string? ProjektId { get; set; }
    private readonly Stack<string> _undo = new();

    public ScenePart? Gewaehlt => Auswahl >= 0 && Auswahl < Teile.Count ? Teile[Auswahl] : null;

    public string Hinzu(string form)
    {
        if (!Formen.Any(f => f.Id == form)) return "Diese Form gibt es nicht.";
        Merken();
        var n = Teile.Count;
        var (bx, by, bz) = form switch
        {
            "kugel" => (10.0, 10, 10),
            "zylinder" => (6.0, 6, 12),
            "kegel" => (8.0, 8, 12),
            "torus" => (12.0, 12, 3),
            "ebene" => (20.0, 14, 0.4),
            "text" => (12.0, 4, 1),
            _ => (10.0, 10, 10),
        };
        var label = Formen.First(f => f.Id == form).Label;
        var teil = new ScenePart
        {
            Name = label + (n == 0 ? "" : " " + (n + 1)),
            Form = form, Bx = bx, By = by, Bz = bz,
            X = n * (bx + 4), Y = 0, Z = bz / 2,
            Farbe = Farben[n % Farben.Length].Hex,
            Text = form == "text" ? "Text" : "",
        };
        Teile.Add(teil);
        Auswahl = Teile.Count - 1;
        return $"{label} hinzugefügt. Größe, Lage und Farbe stellst du rechts ein.";
    }

    public string AusDatei(string name, Mesh3D mesh, int farbe)
    {
        if (mesh.T.Count == 0) return "In der Datei steckt kein Körper.";
        Merken();
        var (min, max) = mesh.Bounds();
        var size = max - min;
        var mid = (min + max) / 2;
        var centered = new Mesh3D();
        foreach (var v in mesh.V) centered.V.Add(v - mid);
        centered.T.AddRange(mesh.T);
        centered.Farbe.AddRange(mesh.Farbe);
        var teil = new ScenePart
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Datei" : name,
            Form = "datei",
            Bx = Math.Max(0.01, size.X), By = Math.Max(0.01, size.Y), Bz = Math.Max(0.01, size.Z),
            DateiBx = Math.Max(0.01, size.X), DateiBy = Math.Max(0.01, size.Y), DateiBz = Math.Max(0.01, size.Z),
            X = 0, Y = 0, Z = Math.Max(0.01, size.Z) / 2,
            Farbe = Mesh3D.Hex(farbe),
            Datei = centered,
        };
        Teile.Add(teil);
        Auswahl = Teile.Count - 1;
        return $"„{teil.Name}“ geladen: {centered.T.Count} Flächen, etwa {teil.Bx:0.#} × {teil.By:0.#} × {teil.Bz:0.#} cm.";
    }

    public string Setzen(int index, string name, double bx, double by, double bz, double x, double y, double z, double rx, double ry, double rz, string farbe, string? text)
    {
        if (index < 0 || index >= Teile.Count) return "Wähle zuerst ein Teil in der Liste.";
        if (bx <= 0 || by <= 0 || bz <= 0) return "Die Größe muss in jeder Richtung größer als 0 sein.";
        if (Math.Abs(x) > 100000 || Math.Abs(y) > 100000 || Math.Abs(z) > 100000) return "Die Position ist unglaubwürdig weit weg.";
        if (!Regex.IsMatch(farbe, "^#[0-9A-Fa-f]{6}$")) return "Die Farbe braucht die Form #RRGGBB, zum Beispiel #2F86F6.";
        Merken();
        var p = Teile[index];
        p.Name = string.IsNullOrWhiteSpace(name) ? p.Name : name.Trim();
        p.Bx = bx; p.By = by; p.Bz = bz; p.X = x; p.Y = y; p.Z = z; p.Rx = rx; p.Ry = ry; p.Rz = rz; p.Farbe = farbe.ToUpperInvariant();
        if (p.Form == "text") p.Text = string.IsNullOrWhiteSpace(text) ? "Text" : text!.Trim();
        Auswahl = index;
        return $"„{p.Name}“ übernommen.";
    }

    public string Entfernen(int index)
    {
        if (index < 0 || index >= Teile.Count) return "Da ist kein Teil zum Löschen.";
        Merken();
        var name = Teile[index].Name;
        Teile.RemoveAt(index);
        if (Teile.Count == 0) Auswahl = -1;
        else Auswahl = Math.Min(index, Teile.Count - 1);
        return $"„{name}“ gelöscht.";
    }

    public string Werkzeug(int index, string art, double wert, string achse)
    {
        if (index < 0 || index >= Teile.Count) return "Wähle zuerst ein Teil in der Liste.";
        var p = Teile[index];
        if (art == "spiegel") return Spiegel(p, achse);
        if (art is "bevel" or "solid" or "hole")
        {
            if (wert <= 0) return art == "hole" ? "Der Bohrer braucht einen Radius größer als 0." : "Der Wert muss größer als 0 sein.";
        }
        Merken();
        p.Mods.Add(new SceneMod(art, wert, achse));
        try { _ = p.Gebaut(); }
        catch (Exception ex)
        {
            p.Mods.RemoveAt(p.Mods.Count - 1);
            if (_undo.Count > 0) _undo.Pop();
            return ex.Message;
        }
        var note = art switch
        {
            "bevel" => $"Kanten um {wert.ToString("0.##", De)} cm abgerundet",
            "smooth" => "Geglättet",
            "solid" => $"Wandstärke {wert.ToString("0.##", De)} cm",
            "hole" => $"Loch {wert.ToString("0.##", De)} cm Radius, Achse {achse.ToUpperInvariant()}",
            _ => art,
        };
        p.Notiz.Add(note);
        return note + ".";
    }

    private string Spiegel(ScenePart p, string achse)
    {
        achse = (achse ?? "x").ToLowerInvariant();
        if (achse is not ("x" or "y" or "z")) return "Die Spiegel-Achse ist X, Y oder Z.";
        Merken();
        var c = p.Copy();
        c.Name = p.Name + " Spiegel";
        c.Mods = p.Mods.ToList();
        if (achse == "x") { c.X = -p.X; c.Ry = -p.Ry; c.Rz = -p.Rz; }
        else if (achse == "y") { c.Y = -p.Y; c.Rx = -p.Rx; c.Rz = -p.Rz; }
        else { c.Z = -p.Z; c.Rx = -p.Rx; c.Ry = -p.Ry; }
        var dist = Math.Abs(c.X - p.X) + Math.Abs(c.Y - p.Y) + Math.Abs(c.Z - p.Z);
        Teile.Add(c);
        Auswahl = Teile.Count - 1;
        if (dist < 0.2) return "Kopie angelegt. Sie liegt auf dem Original, weil das Teil in der Mitte steht. Schiebe das Original von der Mitte weg und spiegele noch einmal.";
        return $"Gespiegelt an {achse.ToUpperInvariant()}. Die Kopie heißt „{c.Name}“.";
    }

    public string Rueckgaengig()
    {
        if (_undo.Count == 0) return "Nichts zum Rückgängigmachen.";
        var json = _undo.Pop();
        var back = FromJson(json);
        Name = back.Name;
        Teile.Clear();
        Teile.AddRange(back.Teile);
        Auswahl = Math.Clamp(back.Auswahl, Teile.Count == 0 ? -1 : 0, Teile.Count - 1);
        if (Teile.Count == 0) Auswahl = -1;
        return "Letzten Schritt rückgängig gemacht.";
    }

    public Mesh3D Welt()
    {
        var m = new Mesh3D();
        foreach (var t in Teile)
        {
            var built = t.Gebaut().ToMesh();
            m.Add(built, Mesh3D.ParseColor(t.Farbe));
        }
        return m;
    }

    public string Info()
    {
        if (Teile.Count == 0) return "Noch kein Teil.";
        var m = Welt();
        var (a, b) = m.Bounds();
        var s = b - a;
        return $"{Teile.Count} Teil{(Teile.Count == 1 ? "" : "e")} · {m.T.Count} Flächen · {s.X:0.#} × {s.Y:0.#} × {s.Z:0.#} cm";
    }

    public void Ersetzen(IEnumerable<ScenePart> teile)
    {
        Merken();
        Teile.Clear();
        Teile.AddRange(teile.Select(t => t.Copy()));
        Auswahl = Teile.Count == 0 ? -1 : 0;
    }

    private void Merken() => _undo.Push(ToJson());

    public string ToJson()
    {
        var dto = new SzeneDto
        {
            Name = Name,
            Teile = Teile.Select(t => new TeilDto
            {
                Name = t.Name, Form = t.Form, Bx = t.Bx, By = t.By, Bz = t.Bz, X = t.X, Y = t.Y, Z = t.Z,
                Rx = t.Rx, Ry = t.Ry, Rz = t.Rz, Farbe = t.Farbe, Text = t.Text,
                DateiBx = t.DateiBx, DateiBy = t.DateiBy, DateiBz = t.DateiBz,
                Notiz = t.Notiz.ToList(),
                Mods = t.Mods.Select(m => new ModDto { Art = m.Art, Wert = m.Wert, Achse = m.Achse }).ToList(),
                Netz = t.Datei is { } d ? new NetzDto
                {
                    V = d.V.SelectMany(v => new[] { v.X, v.Y, v.Z }).ToList(),
                    F = d.T.SelectMany(t => new[] { t.A, t.B, t.C }).ToList(),
                } : null,
            }).ToList(),
        };
        return JsonSerializer.Serialize(dto);
    }

    public static Scene3D FromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<SzeneDto>(json) ?? throw new InvalidOperationException("Die gespeicherte Szene ist leer.");
        var s = new Scene3D { Name = string.IsNullOrWhiteSpace(dto.Name) ? "Mein Modell" : dto.Name };
        foreach (var t in dto.Teile ?? [])
        {
            var p = new ScenePart
            {
                Name = t.Name ?? "Teil", Form = t.Form ?? "wuerfel",
                Bx = t.Bx, By = t.By, Bz = t.Bz, X = t.X, Y = t.Y, Z = t.Z, Rx = t.Rx, Ry = t.Ry, Rz = t.Rz,
                Farbe = t.Farbe ?? "#2F86F6", Text = t.Text ?? "",
                DateiBx = t.DateiBx == 0 ? 1 : t.DateiBx, DateiBy = t.DateiBy == 0 ? 1 : t.DateiBy, DateiBz = t.DateiBz == 0 ? 1 : t.DateiBz,
                Notiz = t.Notiz ?? [],
                Mods = (t.Mods ?? []).Select(m => new SceneMod(m.Art ?? "", m.Wert, m.Achse ?? "z")).ToList(),
            };
            if (t.Netz is { V.Count: > 0, F.Count: >= 3 })
            {
                var m = new Mesh3D();
                for (var i = 0; i + 2 < t.Netz.V.Count; i += 3) m.V.Add(new Vec3(t.Netz.V[i], t.Netz.V[i + 1], t.Netz.V[i + 2]));
                for (var i = 0; i + 2 < t.Netz.F.Count; i += 3) m.T.Add((t.Netz.F[i], t.Netz.F[i + 1], t.Netz.F[i + 2]));
                p.Datei = m;
            }
            s.Teile.Add(p);
        }
        if (s.Teile.Count > 0) s.Auswahl = 0;
        return s;
    }

    private static readonly CultureInfo De = new("de-DE");

    private sealed class SzeneDto
    {
        public string? Name { get; set; }
        public List<TeilDto>? Teile { get; set; }
    }
    private sealed class TeilDto
    {
        public string? Name { get; set; }
        public string? Form { get; set; }
        public double Bx { get; set; }
        public double By { get; set; }
        public double Bz { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double Rx { get; set; }
        public double Ry { get; set; }
        public double Rz { get; set; }
        public string? Farbe { get; set; }
        public string? Text { get; set; }
        public double DateiBx { get; set; }
        public double DateiBy { get; set; }
        public double DateiBz { get; set; }
        public List<string>? Notiz { get; set; }
        public List<ModDto>? Mods { get; set; }
        public NetzDto? Netz { get; set; }
    }
    private sealed class ModDto
    {
        public string? Art { get; set; }
        public double Wert { get; set; }
        public string? Achse { get; set; }
    }
    private sealed class NetzDto
    {
        public List<double>? V { get; set; }
        public List<int>? F { get; set; }
    }
}

/// <summary>Liest die Teileliste, die die lokale KI als JSON liefert.</summary>
public static partial class Scene3DAi
{
    public static string Auftrag(string satz) =>
        "Du planst ein einfaches 3D-Objekt aus Grundformen für Trendo OS. Antworte nur mit JSON, ohne Erklärung. Aufbau: " +
        "{\"teile\":[{\"form\":\"wuerfel\",\"name\":\"Platte\",\"bx\":10,\"by\":10,\"bz\":2,\"x\":0,\"y\":0,\"z\":1,\"farbe\":\"#C4A574\"}]}. " +
        "Erlaubte form: wuerfel, kugel, zylinder, kegel, torus, ebene. " +
        "bx, by, bz und x, y, z sind Zentimeter. x, y, z ist die Mitte des Teils. z ist die Höhe der Mitte. " +
        "Teile sollen sich berühren und auf dem Boden stehen (unterste Kante bei z = 0). Höchstens 12 Teile. Der Wunsch: " + satz;

    public static bool TryLesen(string text, out List<ScenePart> teile, out string fehler)
    {
        teile = [];
        fehler = "";
        if (string.IsNullOrWhiteSpace(text)) { fehler = "Die KI hat nichts zurückgegeben."; return false; }
        var raw = text.Trim();
        var fence = Fence().Match(raw);
        if (fence.Success) raw = fence.Groups[1].Value.Trim();
        var start = raw.IndexOf('{'); var end = raw.LastIndexOf('}');
        if (start < 0 || end <= start) { fehler = "Die KI hat keine Teileliste geschickt. Formuliere den Satz noch einmal, zum Beispiel: ein Tisch 120 mal 80 cm mit vier Beinen."; return false; }
        raw = raw[start..(end + 1)];
        JsonDocument doc;
        try { doc = JsonDocument.Parse(raw); }
        catch { fehler = "Die Antwort der KI ist kein gültiges JSON. Versuch es mit einem kürzeren Satz."; return false; }
        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("teile", out var arr) || arr.ValueKind != JsonValueKind.Array)
            { fehler = "Der KI-Antwort fehlt die Liste „teile“."; return false; }
            var i = 0;
            foreach (var el in arr.EnumerateArray())
            {
                if (i++ >= 12) break;
                var form = Str(el, "form") ?? "wuerfel";
                if (!Scene3D.Formen.Any(f => f.Id == form)) { fehler = $"Unbekannte Form „{form}“."; return false; }
                double N(string k, double d) => el.TryGetProperty(k, out var v) && v.TryGetDouble(out var n) ? n : d;
                var bx = Math.Clamp(Math.Abs(N("bx", 10)), 0.1, 2000);
                var by = Math.Clamp(Math.Abs(N("by", 10)), 0.1, 2000);
                var bz = Math.Clamp(Math.Abs(N("bz", 10)), 0.1, 2000);
                var farbe = Str(el, "farbe") ?? "#C4A574";
                if (!Regex.IsMatch(farbe, "^#[0-9A-Fa-f]{6}$")) farbe = "#C4A574";
                teile.Add(new ScenePart
                {
                    Name = Str(el, "name") ?? Scene3D.Formen.First(f => f.Id == form).Label,
                    Form = form, Bx = bx, By = by, Bz = bz,
                    X = N("x", 0), Y = N("y", 0), Z = N("z", bz / 2),
                    Farbe = farbe.ToUpperInvariant(),
                });
            }
        }
        if (teile.Count == 0) { fehler = "Die Liste enthält kein Teil."; return false; }
        return true;
    }

    private static string? Str(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    [GeneratedRegex("```(?:json)?\\s*([\\s\\S]*?)```", RegexOptions.IgnoreCase)]
    private static partial Regex Fence();
}
