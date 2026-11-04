using System.Globalization;
using System.Text.Json.Nodes;

namespace Trendo.Core.Media;

/// <summary>Masterprompt 131 (Beispiel Bildbearbeitung): Bild-Dokument mit Ebenen – Bild, Text, Form, Malen. Unten liegt die erste Ebene.
/// Die Daten liegen als JSON im Programmdokument (.tdoc): Rückgängig, Autosave und Wiederherstellen kommen aus der Programm-Hülle.
/// Gezeichnet wird in Trendo.App (Skia); hier stehen Modell, Bearbeitungen und Trefferprüfung.</summary>
public static class ImageDoc
{
    public static readonly string[] Kinds = ["image", "text", "shape", "paint"];
    public static readonly string[] Filters = ["brightness", "contrast", "saturation", "blur", "grayscale", "sepia", "invert"];

    public static JsonObject New(int w, int h, string background = "#FFFFFF") => new() { ["width"] = w, ["height"] = h, ["background"] = background, ["layers"] = new JsonArray(), ["selected"] = "" };

    public static JsonArray Layers(JsonObject d) => d["layers"] as JsonArray ?? (JsonArray)(d["layers"] = new JsonArray());
    public static JsonObject? Layer(JsonObject d, string id) => Layers(d).OfType<JsonObject>().FirstOrDefault(l => (string?)l["id"] == id);
    public static int IndexOf(JsonObject d, string id) => Layers(d).OfType<JsonObject>().ToList().FindIndex(l => (string?)l["id"] == id);
    public static double N(JsonNode? n, double fallback = 0) => n is null ? fallback : double.TryParse(n.GetValueKind() == System.Text.Json.JsonValueKind.String ? n.GetValue<string>() : n.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;
    public static string S(JsonNode? n, string fallback = "") => n?.GetValueKind() == System.Text.Json.JsonValueKind.String ? n.GetValue<string>() : fallback;

    static JsonObject Base(string kind, string name, double x, double y, double w, double h) => new()
    {
        ["id"] = Guid.NewGuid().ToString("N")[..8], ["kind"] = kind, ["name"] = name, ["visible"] = true, ["opacity"] = 1.0,
        ["x"] = Math.Round(x, 1), ["y"] = Math.Round(y, 1), ["w"] = Math.Round(w, 1), ["h"] = Math.Round(h, 1),
    };

    /// <summary>Ebene oben anlegen und auswählen.</summary>
    public static JsonObject Add(JsonObject d, JsonObject layer) { Layers(d).Add(layer); d["selected"] = (string?)layer["id"]; return layer; }

    /// <summary>Bild-Ebene: eingepasst und mittig, wenn es größer als die Leinwand ist.</summary>
    public static JsonObject AddImage(JsonObject d, string file, int imgW, int imgH, string? name = null)
    {
        double cw = N(d["width"]), ch = N(d["height"]); var k = Math.Min(1, Math.Min(cw / imgW, ch / imgH));
        var w = imgW * k; var h = imgH * k;
        var l = Base("image", name ?? Path.GetFileNameWithoutExtension(file), (cw - w) / 2, (ch - h) / 2, w, h);
        l["file"] = file; l["filter"] = new JsonObject();
        return Add(d, l);
    }

    public static JsonObject AddText(JsonObject d, string text, double x, double y, double size = 64, string color = "#FFFFFF")
    {
        var l = Set(Base("text", "Text", x, y, Math.Max(20, text.Length * size * 0.55), size * 1.25), ("text", text), ("size", size), ("color", color));
        return Add(d, l);
    }

    public static JsonObject AddShape(JsonObject d, string shape, double x, double y, double w, double h, string fill = "#2F86F6", string stroke = "", double strokeWidth = 0)
    {
        if (w < 0) { x += w; w = -w; } if (h < 0) { y += h; h = -h; }
        return Add(d, Set(Base("shape", shape == "ellipse" ? "Ellipse" : "Rechteck", x, y, Math.Max(1, w), Math.Max(1, h)), ("shape", shape), ("fill", fill), ("stroke", stroke), ("strokeWidth", strokeWidth)));
    }

    /// <summary>Mal-Ebene für Pinsel und Radierer (über die ganze Leinwand).</summary>
    public static JsonObject AddPaint(JsonObject d) => Add(d, Set(Base("paint", "Malen", 0, 0, N(d["width"]), N(d["height"])), ("strokes", new JsonArray())));

    static JsonObject Set(JsonObject l, params (string K, JsonNode? V)[] kv) { foreach (var (k, v) in kv) l[k] = v; return l; }

    /// <summary>Strich auf die Mal-Ebene (Punkte in Leinwand-Pixeln); Radierer löscht auf dieser Ebene.</summary>
    public static void AddStroke(JsonObject layer, IReadOnlyList<(double X, double Y)> pts, string color, double size, bool erase, double alpha = 1)
    {
        if (S(layer["kind"]) != "paint") throw new InvalidOperationException("Malen geht nur auf einer Mal-Ebene.");
        if (pts.Count == 0) return;
        var a = new JsonArray(); foreach (var (x, y) in pts) { a.Add(Math.Round(x, 1)); a.Add(Math.Round(y, 1)); }
        var strokes = layer["strokes"] as JsonArray ?? (JsonArray)(layer["strokes"] = new JsonArray());
        strokes.Add(new JsonObject { ["color"] = color, ["size"] = Math.Clamp(size, 1, 400), ["erase"] = erase, ["points"] = a, ["alpha"] = Math.Round(Math.Clamp(alpha, 0.02, 1), 2) });
    }

    public static void Move(JsonObject layer, double dx, double dy) { layer["x"] = Math.Round(N(layer["x"]) + dx, 1); layer["y"] = Math.Round(N(layer["y"]) + dy, 1); }

    /// <summary>Reihenfolge ändern: +1 = eine Ebene nach oben.</summary>
    public static void Reorder(JsonObject d, string id, int by)
    {
        var l = Layers(d); var i = IndexOf(d, id); var j = Math.Clamp(i + by, 0, l.Count - 1); if (i < 0 || i == j) return;
        var n = l[i]!; l.RemoveAt(i); l.Insert(j, n);
    }

    public static void Remove(JsonObject d, string id)
    {
        var i = IndexOf(d, id); if (i < 0) return; Layers(d).RemoveAt(i);
        d["selected"] = Layers(d).Count == 0 ? "" : (string?)((JsonObject)Layers(d)[Math.Max(0, i - 1)]!)["id"];
    }

    /// <summary>Oberste sichtbare Ebene unter einem Leinwandpunkt (Mal-Ebenen zählen nicht, sie decken alles ab).</summary>
    public static string? HitTest(JsonObject d, double x, double y) =>
        Layers(d).OfType<JsonObject>().Reverse().Where(l => l["visible"]?.GetValue<bool>() != false && S(l["kind"]) != "paint")
            .FirstOrDefault(l => x >= N(l["x"]) && y >= N(l["y"]) && x <= N(l["x"]) + N(l["w"]) && y <= N(l["y"]) + N(l["h"])) is { } hit ? (string?)hit["id"] : null;

    /// <summary>Leinwand zuschneiden: neue Größe, alle Ebenen und Striche rücken mit.</summary>
    public static void Crop(JsonObject d, double x, double y, double w, double h)
    {
        double cw = N(d["width"]), ch = N(d["height"]);
        if (w < 0) { x += w; w = -w; } if (h < 0) { y += h; h = -h; }
        x = Math.Clamp(x, 0, cw - 1); y = Math.Clamp(y, 0, ch - 1); w = Math.Clamp(w, 1, cw - x); h = Math.Clamp(h, 1, ch - y);
        if (w < 4 || h < 4) throw new InvalidOperationException("Der Ausschnitt ist zu klein.");
        foreach (var l in Layers(d).OfType<JsonObject>())
        {
            Move(l, -x, -y);
            if (S(l["kind"]) == "paint" && l["strokes"] is JsonArray st)
                foreach (var s in st.OfType<JsonObject>())
                    if (s["points"] is JsonArray p) for (var i = 0; i < p.Count; i++) p[i] = Math.Round(N(p[i]) - (i % 2 == 0 ? x : y), 1);
            if (S(l["kind"]) == "paint") { l["x"] = 0; l["y"] = 0; l["w"] = Math.Round(w); l["h"] = Math.Round(h); }
        }
        d["width"] = (int)Math.Round(w); d["height"] = (int)Math.Round(h);
    }

    /// <summary>Filterwert setzen: Helligkeit/Kontrast/Sättigung −100…100, Unschärfe 0…50, Schwarzweiß/Sepia/Invertieren 0 oder 1.</summary>
    public static void SetFilter(JsonObject layer, string key, double value)
    {
        if (!Filters.Contains(key)) throw new ArgumentException("Unbekannter Filter: " + key);
        var f = layer["filter"] as JsonObject ?? (JsonObject)(layer["filter"] = new JsonObject());
        f[key] = key switch { "blur" => Math.Clamp(value, 0, 50), "grayscale" or "sepia" or "invert" => value > 0 ? 1 : 0, _ => Math.Clamp(value, -100, 100) };
    }

    /// <summary>Farbmatrix (4×5, Skia-Reihenfolge) aus Helligkeit, Kontrast, Sättigung, Schwarzweiß, Sepia, Invertieren – in dieser Reihenfolge angewandt.</summary>
    public static float[] ColorMatrix(JsonObject? f)
    {
        double V(string k) => f is null ? 0 : N(f[k]);
        float[] m = [1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0];
        float[] Mul(float[] a, float[] b)
        {
            var r = new float[20];
            for (var i = 0; i < 4; i++)
            {
                for (var j = 0; j < 4; j++) r[i * 5 + j] = a[i * 5] * b[j] + a[i * 5 + 1] * b[5 + j] + a[i * 5 + 2] * b[10 + j] + a[i * 5 + 3] * b[15 + j];
                r[i * 5 + 4] = a[i * 5] * b[4] + a[i * 5 + 1] * b[9] + a[i * 5 + 2] * b[14] + a[i * 5 + 3] * b[19] + a[i * 5 + 4];
            }
            return r;
        }
        float k = 1 + (float)V("contrast") / 100f, off = 128 * (1 - k) + (float)V("brightness") / 100f * 255f;
        m = Mul([k, 0, 0, 0, off, 0, k, 0, 0, off, 0, 0, k, 0, off, 0, 0, 0, 1, 0], m);
        float s = 1 + (float)V("saturation") / 100f; const float lr = 0.2126f, lg = 0.7152f, lb = 0.0722f;
        float[] sat = [lr * (1 - s) + s, lg * (1 - s), lb * (1 - s), 0, 0, lr * (1 - s), lg * (1 - s) + s, lb * (1 - s), 0, 0, lr * (1 - s), lg * (1 - s), lb * (1 - s) + s, 0, 0, 0, 0, 0, 1, 0];
        m = Mul(sat, m);
        if (V("grayscale") > 0) m = Mul([lr, lg, lb, 0, 0, lr, lg, lb, 0, 0, lr, lg, lb, 0, 0, 0, 0, 0, 1, 0], m);
        if (V("sepia") > 0) m = Mul([0.393f, 0.769f, 0.189f, 0, 0, 0.349f, 0.686f, 0.168f, 0, 0, 0.272f, 0.534f, 0.131f, 0, 0, 0, 0, 0, 1, 0], m);
        if (V("invert") > 0) m = Mul([-1, 0, 0, 0, 255, 0, -1, 0, 0, 255, 0, 0, -1, 0, 255, 0, 0, 0, 1, 0], m);
        return m;
    }
}
