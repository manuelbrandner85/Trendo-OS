using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Trendo.Core.Kreativ;

/// <summary>T-0697 (D4b): Comic-Heft mit mehreren Seiten. Bildfelder sind freie Vierecke (Raster nur als Vorschlag, Ecken einzeln ziehbar),
/// Sprechblasen und Textkästen liegen frei auf der Seite und haben einen ziehbaren Dorn, je Seite gibt es Mal-Ebenen (Mal-Kern des Künstlerstudios).
/// Ältere Dokumente (eine Seite mit fester Aufteilung und Texten je Bild) werden beim Lesen umgewandelt.</summary>
public sealed class ComicBook
{
    public const double PageW = ComicKit.PageW, PageH = ComicKit.PageH, Margin = ComicKit.Margin, Gap = ComicKit.Gap;
    public static readonly IReadOnlyList<(string Id, string Name)> Kinds = [("sprech", "Sprechblase"), ("gedanke", "Gedankenblase"), ("schrei", "Ausruf"), ("kasten", "Textkasten")];
    /// <summary>Raster-Vorschläge: Comic und Storyboard (16:9-Bilder mit Zeile für die Einstellung).</summary>
    public static readonly IReadOnlyList<(string Id, string Label)> Layouts =
        [.. ComicKit.Layouts, ("1", "Ein Bild"), ("2-spalten", "Zwei Spalten"), ("sb-6", "Storyboard 6"), ("sb-4", "Storyboard 4")];

    public string Title { get; set; } = "";
    /// <summary>„comic“ oder „storyboard“ (Bildfeld = Einstellung, mit Beschreibung, Kamera, Dauer).</summary>
    public string Mode { get; set; } = "comic";
    public List<ComicPage> Pages { get; } = [];
    public bool Storyboard => Mode == "storyboard";

    static string S(JsonNode? n, string def = "") => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : def;
    static double D(JsonNode? n, double def) => Programs.ProgramDocument.ToNumber(n, def);
    static string NewId(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8];

    public static ComicBook Read(JsonObject d)
    {
        var b = new ComicBook { Title = S(d["title"]), Mode = S(d["mode"]) == "storyboard" ? "storyboard" : "comic" };
        if (d["pages"] is JsonArray pages)
            foreach (var p in pages.OfType<JsonObject>()) b.Pages.Add(ComicPage.FromJson(p));
        else if (d["panels"] is JsonArray || d["layout"] is not null) b.Pages.Add(Legacy(d));
        if (b.Pages.Count == 0) b.Pages.Add(ComicPage.Grid("2x2"));
        return b;
    }

    /// <summary>Altes Format (T-0610): feste Aufteilung, je Bild Sprechblase und Bildunterschrift → freie Bildfelder, Blasen und Kästen.</summary>
    static ComicPage Legacy(JsonObject d)
    {
        var layout = S(d["layout"], "2x2"); var page = ComicPage.Grid(layout.Length > 0 ? layout : "2x2");
        var old = (d["panels"] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];
        for (var i = 0; i < old.Count; i++)
        {
            if (i >= page.Panels.Count) page.Panels.Add(ComicPanel.Rect(Margin, Margin, 200, 200));
            var p = page.Panels[i]; p.Image = S(old[i]["image"]);
            var (x, y, w, h) = p.Bounds();
            if (S(old[i]["speech"]).Trim() is { Length: > 0 } sp) page.Balloons.Add(ComicBalloon.Make("sprech", x + 20, y + 20, Math.Min(w - 40, 370), sp, x + 70, y + 200));
            if (S(old[i]["caption"]).Trim() is { Length: > 0 } cap) page.Balloons.Add(ComicBalloon.Make("kasten", x, y + h - 52, w, cap));
        }
        foreach (var bl in page.Balloons) bl.Fit();
        return page;
    }

    public void Write(JsonObject d)
    {
        d["title"] = Title; d["mode"] = Mode;
        d["pages"] = new JsonArray([.. Pages.Select(p => (JsonNode)p.ToJson())]);
        d.Remove("panels"); d.Remove("layout");
    }

    public static JsonObject NewDoc(string title, string mode, string layout, int pages = 1)
    {
        var b = new ComicBook { Title = title, Mode = mode };
        for (var i = 0; i < pages; i++) b.Pages.Add(ComicPage.Grid(layout));
        var j = new JsonObject(); b.Write(j); j["page"] = 0; return j;
    }

    /// <summary>Raster als Vorschlag: vorhandene Bildfelder behalten Bild und Einstellung und nehmen nur die neue Lage an; fehlende kommen dazu, leere überzählige gehen.</summary>
    public static void ApplyLayout(ComicPage page, string layout)
    {
        var quads = Boxes(layout);
        for (var i = 0; i < quads.Count; i++)
        {
            if (i < page.Panels.Count) page.Panels[i].P = quads[i].P;
            else page.Panels.Add(quads[i]);
        }
        for (var i = page.Panels.Count - 1; i >= quads.Count; i--)
            if (page.Panels[i].IsEmpty) page.Panels.RemoveAt(i);
    }

    public static List<ComicPanel> Boxes(string layout)
    {
        double w = PageW - 2 * Margin, h = PageH - 2 * Margin;
        List<ComicPanel> Sb(int rows)
        {   // Storyboard: zwei Spalten, 16:9, darunter Platz für Beschreibung
            var list = new List<ComicPanel>(); var cw = (w - Gap) / 2; var ch = cw * 9 / 16; var rowH = (h + Gap) / rows;
            for (var r = 0; r < rows; r++) for (var c = 0; c < 2; c++) list.Add(ComicPanel.Rect(Margin + c * (cw + Gap), Margin + r * rowH, cw, ch));
            return list;
        }
        return layout switch
        {
            "1" => [ComicPanel.Rect(Margin, Margin, w, h)],
            "2-spalten" => [ComicPanel.Rect(Margin, Margin, (w - Gap) / 2, h), ComicPanel.Rect(Margin + (w + Gap) / 2, Margin, (w - Gap) / 2, h)],
            "sb-6" => Sb(3),
            "sb-4" => Sb(2),
            _ => [.. ComicKit.Boxes(layout).Select(b => ComicPanel.Rect(b.X, b.Y, b.W, b.H))],
        };
    }

    // ---------- Geometrie ----------
    public static bool InQuad(double[] p, double x, double y)
    {   // Punkt im Vieleck (gerade/ungerade Regel)
        var inside = false;
        for (int i = 0, j = 3; i < 4; j = i++)
        {
            double xi = p[i * 2], yi = p[i * 2 + 1], xj = p[j * 2], yj = p[j * 2 + 1];
            if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi + 1e-12) + xi) inside = !inside;
        }
        return inside;
    }

    /// <summary>Oberstes Bildfeld unter dem Punkt (später angelegte liegen oben), sonst −1.</summary>
    public static int HitPanel(ComicPage page, double x, double y)
    {
        for (var i = page.Panels.Count - 1; i >= 0; i--) if (InQuad(page.Panels[i].P, x, y)) return i;
        return -1;
    }

    public static int HitBalloon(ComicPage page, double x, double y)
    {
        for (var i = page.Balloons.Count - 1; i >= 0; i--) { var b = page.Balloons[i]; if (x >= b.X && x <= b.X + b.W && y >= b.Y && y <= b.Y + b.H) return i; }
        return -1;
    }

    public static (double X, double Y) Clamp(double x, double y) => (Math.Clamp(x, 0, PageW), Math.Clamp(y, 0, PageH));

    /// <summary>Text auf eine Breite umbrechen (Schätzung der Zeichenbreite, gleiche Regel für SVG und die Höhe der Blase). Lange Wörter werden geteilt.</summary>
    public static List<string> Wrap(string text, double width, double size)
    {
        var max = Math.Max(4, (int)(width / (size * 0.55)));
        var lines = new List<string>();
        foreach (var para in (text ?? "").Replace("\r", "").Split('\n'))
        {
            var line = new StringBuilder();
            foreach (var raw in para.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var word = raw;
                while (word.Length > max) { if (line.Length > 0) { lines.Add(line.ToString()); line.Clear(); } lines.Add(word[..max]); word = word[max..]; }
                if (line.Length > 0 && line.Length + 1 + word.Length > max) { lines.Add(line.ToString()); line.Clear(); }
                if (line.Length > 0) line.Append(' ');
                line.Append(word);
            }
            lines.Add(line.ToString());
        }
        while (lines.Count > 1 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        return lines;
    }

    // ---------- KI ----------
    /// <summary>Antwort der KI („Bild 1: Sprechblase | Bildunterschrift“ …) auf die Bildfelder der Seite verteilen: je Bildfeld eine Sprechblase und ein Textkasten.
    /// Liefert die Zahl der belegten Bildfelder (0 = nichts verstanden, nichts geändert).</summary>
    public static int AdoptStory(ComicPage page, string answer)
    {
        var parts = ComicKit.SplitStory(answer);
        if (parts.Count == 0 || page.Panels.Count == 0) return 0;
        var n = 0;
        foreach (var (part, i) in parts.Take(page.Panels.Count).Select((p, i) => (p, i)))
        {
            var pn = ComicKit.ParsePanel(part); var (x, y, w, h) = page.Panels[i].Bounds();
            if (pn.Speech.Length > 0) { var b = ComicBalloon.Make("sprech", x + 20, y + 20, Math.Min(w - 40, 360), pn.Speech, x + 80, y + h * 0.6); b.Fit(); page.Balloons.Add(b); }
            if (pn.Caption.Length > 0) { var c = ComicBalloon.Make("kasten", x, y + h - 52, w, pn.Caption); c.Fit(); c.Y = y + h - c.H; page.Balloons.Add(c); }
            n++;
        }
        return n;
    }

    // ---------- SVG je Seite ----------
    static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    public static string Svg(ComicBook book, int index)
    {
        var page = book.Pages[Math.Clamp(index, 0, book.Pages.Count - 1)];
        var sb = new StringBuilder($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{F(PageW)}\" height=\"{F(PageH)}\" viewBox=\"0 0 {F(PageW)} {F(PageH)}\"><rect width=\"100%\" height=\"100%\" fill=\"#FFFFFF\"/>");
        if (index == 0 && book.Title.Trim().Length > 0) sb.Append($"<text x=\"{F(Margin)}\" y=\"30\" font-size=\"24\" font-family=\"Segoe UI\" font-weight=\"700\">{KreativSafe.Xml(book.Title.Trim())}</text>");
        for (var i = 0; i < page.Panels.Count; i++)
        {
            var p = page.Panels[i]; var pts = string.Join(" ", Enumerable.Range(0, 4).Select(k => F(p.P[k * 2]) + "," + F(p.P[k * 2 + 1])));
            sb.Append($"<clipPath id=\"c{i}\"><polygon points=\"{pts}\"/></clipPath><polygon points=\"{pts}\" fill=\"#FFFFFF\"/>");
            if (DataUri(p.Image) is { } img)
            {
                var (x, y, w, h) = p.Bounds();
                // Ausschnitt: Bild füllt das Feld, Zoom und Versatz verschieben es darin
                sb.Append($"<g clip-path=\"url(#c{i})\"><image href=\"{img}\" x=\"{F(x + p.Ox * w - (p.Zoom - 1) * w / 2)}\" y=\"{F(y + p.Oy * h - (p.Zoom - 1) * h / 2)}\" width=\"{F(w * p.Zoom)}\" height=\"{F(h * p.Zoom)}\" preserveAspectRatio=\"xMidYMid slice\"/></g>");
            }
        }
        if (page.Art is { } art && PaintDoc.FromJson(art) is { } pd && pd.Layers.Any(l => l.Visible && !l.Px.IsEmpty()))
            sb.Append($"<image href=\"data:image/png;base64,{Convert.ToBase64String(PngCodec.Encode(pd.Composite(false)))}\" x=\"0\" y=\"0\" width=\"{F(PageW)}\" height=\"{F(PageH)}\"/>");
        for (var i = 0; i < page.Panels.Count; i++)
        {
            var p = page.Panels[i];
            sb.Append($"<polygon points=\"{string.Join(" ", Enumerable.Range(0, 4).Select(k => F(p.P[k * 2]) + "," + F(p.P[k * 2 + 1])))}\" fill=\"none\" stroke=\"#111\" stroke-width=\"4\" stroke-linejoin=\"round\"/>");
            if (book.Storyboard)
            {
                var (x, y, w, h) = p.Bounds(); var line = y + h + 24;
                sb.Append($"<text x=\"{F(x)}\" y=\"{F(line)}\" font-size=\"18\" font-family=\"Segoe UI\" font-weight=\"700\">{KreativSafe.Xml(p.ShotHead(i))}</text>");
                foreach (var l in Wrap(p.Shot, w, 16).Take(4)) { line += 21; sb.Append($"<text x=\"{F(x)}\" y=\"{F(line)}\" font-size=\"16\" font-family=\"Segoe UI\">{KreativSafe.Xml(l)}</text>"); }
            }
        }
        foreach (var b in page.Balloons) sb.Append(b.Svg());
        return sb.Append("</svg>").ToString();
    }

    static string? DataUri(string path)
    {
        if (!KreativSafe.LocalFile(path, KreativSafe.MaxImageBytes)) return null;
        var mime = Path.GetExtension(path).ToLowerInvariant() switch { ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".webp" => "image/webp", _ => null };
        return mime is null ? null : $"data:{mime};base64,{Convert.ToBase64String(File.ReadAllBytes(path))}";
    }
}

public sealed class ComicPage
{
    public string Id { get; set; } = "s" + Guid.NewGuid().ToString("N")[..8];
    public List<ComicPanel> Panels { get; } = [];
    public List<ComicBalloon> Balloons { get; } = [];
    /// <summary>Mal-Ebenen der Seite (Format des Künstlerstudios, <see cref="PaintDoc"/>), null = noch nichts gezeichnet.</summary>
    public JsonObject? Art { get; set; }

    public static ComicPage Grid(string layout) { var p = new ComicPage(); p.Panels.AddRange(ComicBook.Boxes(layout)); return p; }

    public bool IsEmpty => Balloons.Count == 0 && Panels.All(p => p.IsEmpty) && !HasArt;
    public bool HasArt => Art?["layers"] is JsonArray a && a.OfType<JsonObject>().Any(l => (l["png"] as JsonValue)?.ToString().Length > 400);

    /// <summary>Mal-Ebenen holen (beim ersten Zeichnen angelegt: eine durchsichtige Ebene „Zeichnung“ in Seitengröße).</summary>
    public PaintDoc Paint()
    {
        if (Art is null)
        {
            var d = new PaintDoc((int)ComicBook.PageW, (int)ComicBook.PageH, "#00000000"); d.Add("Zeichnung");
            Art = d.ToJson(); Art["active"] = d.Layers[0].Id;
        }
        return PaintDoc.FromJson(Art);
    }

    public JsonObject ToJson()
    {
        var o = new JsonObject
        {
            ["id"] = Id,
            ["panels"] = new JsonArray([.. Panels.Select(p => (JsonNode)p.ToJson())]),
            ["balloons"] = new JsonArray([.. Balloons.Select(b => (JsonNode)b.ToJson())]),
        };
        if (Art is not null) o["art"] = Art.Parent is null ? Art : Art.DeepClone();
        return o;
    }

    public static ComicPage FromJson(JsonObject o)
    {
        var p = new ComicPage { Id = o["id"] is JsonValue v && v.TryGetValue<string>(out var id) && id.Length > 0 ? id : "s" + Guid.NewGuid().ToString("N")[..8] };
        foreach (var x in (o["panels"] as JsonArray)?.OfType<JsonObject>() ?? []) p.Panels.Add(ComicPanel.FromJson(x));
        foreach (var x in (o["balloons"] as JsonArray)?.OfType<JsonObject>() ?? []) p.Balloons.Add(ComicBalloon.FromJson(x));
        p.Art = o["art"] is JsonObject a ? (JsonObject)a.DeepClone() : null;
        return p;
    }
}

public sealed class ComicPanel
{
    public string Id { get; set; } = "f" + Guid.NewGuid().ToString("N")[..8];
    /// <summary>Ecken im Uhrzeigersinn: oben links, oben rechts, unten rechts, unten links (x, y je Ecke).</summary>
    public double[] P { get; set; } = new double[8];
    public string Image { get; set; } = "";
    /// <summary>Bildausschnitt: Zoom ≥ 1, Versatz in Anteilen der Feldgröße.</summary>
    public double Zoom { get; set; } = 1;
    public double Ox { get; set; }
    public double Oy { get; set; }
    /// <summary>Storyboard: Beschreibung der Einstellung, Kamera, Dauer in Sekunden.</summary>
    public string Shot { get; set; } = "";
    public string Camera { get; set; } = "";
    public double Seconds { get; set; } = 3;

    public bool IsEmpty => Image.Length == 0 && Shot.Trim().Length == 0;

    public static ComicPanel Rect(double x, double y, double w, double h) => new() { P = [x, y, x + w, y, x + w, y + h, x, y + h] };

    public (double X, double Y, double W, double H) Bounds()
    {
        double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
        for (var k = 0; k < 4; k++) { x0 = Math.Min(x0, P[k * 2]); x1 = Math.Max(x1, P[k * 2]); y0 = Math.Min(y0, P[k * 2 + 1]); y1 = Math.Max(y1, P[k * 2 + 1]); }
        return (x0, y0, Math.Max(1, x1 - x0), Math.Max(1, y1 - y0));
    }

    public void Move(double dx, double dy)
    {
        var (x, y, w, h) = Bounds();
        dx = Math.Clamp(dx, -x, ComicBook.PageW - x - w); dy = Math.Clamp(dy, -y, ComicBook.PageH - y - h);
        for (var k = 0; k < 4; k++) { P[k * 2] += dx; P[k * 2 + 1] += dy; }
    }

    /// <summary>Eine Ecke frei ziehen (schräge Bildfelder) – auf der Seite festgehalten.</summary>
    public void MoveCorner(int k, double x, double y) { (x, y) = ComicBook.Clamp(x, y); P[k * 2] = x; P[k * 2 + 1] = y; }

    /// <summary>Nächste Ecke im Umkreis <paramref name="radius"/>, sonst −1.</summary>
    public int Corner(double x, double y, double radius)
    {
        for (var k = 0; k < 4; k++) if (Math.Abs(P[k * 2] - x) <= radius && Math.Abs(P[k * 2 + 1] - y) <= radius) return k;
        return -1;
    }

    public string ShotHead(int index) => $"Einstellung {index + 1}" + (Camera.Trim().Length > 0 ? " · " + Camera.Trim() : "") + $" · {Seconds.ToString("0.#", CultureInfo.GetCultureInfo("de-DE"))} s";

    public JsonObject ToJson() => new()
    {
        ["id"] = Id, ["p"] = new JsonArray([.. P.Select(v => (JsonNode)Math.Round(v, 2))]), ["image"] = Image,
        ["zoom"] = Math.Round(Zoom, 3), ["ox"] = Math.Round(Ox, 4), ["oy"] = Math.Round(Oy, 4), ["shot"] = Shot, ["camera"] = Camera, ["seconds"] = Seconds,
    };

    public static ComicPanel FromJson(JsonObject o)
    {
        var p = new ComicPanel
        {
            Id = (o["id"] as JsonValue)?.ToString() is { Length: > 0 } id ? id : "f" + Guid.NewGuid().ToString("N")[..8],
            Image = o["image"] is JsonValue v && v.TryGetValue<string>(out var s) ? s : "",
            Zoom = Math.Clamp(Programs.ProgramDocument.ToNumber(o["zoom"], 1), 1, 8), Ox = Programs.ProgramDocument.ToNumber(o["ox"], 0), Oy = Programs.ProgramDocument.ToNumber(o["oy"], 0),
            Shot = o["shot"] is JsonValue sv && sv.TryGetValue<string>(out var sh) ? sh : "", Camera = o["camera"] is JsonValue cv && cv.TryGetValue<string>(out var c) ? c : "",
            Seconds = Math.Clamp(Programs.ProgramDocument.ToNumber(o["seconds"], 3), 0.2, 600),
        };
        var pts = (o["p"] as JsonArray)?.Select(n => Programs.ProgramDocument.ToNumber(n, double.NaN)).ToArray() ?? [];
        p.P = pts.Length == 8 && pts.All(double.IsFinite) ? pts : Rect(ComicBook.Margin, ComicBook.Margin, 300, 300).P;
        return p;
    }
}

public sealed class ComicBalloon
{
    public string Id { get; set; } = "b" + Guid.NewGuid().ToString("N")[..8];
    public string Kind { get; set; } = "sprech";
    public double X { get; set; }
    public double Y { get; set; }
    public double W { get; set; } = 260;
    public double H { get; set; } = 110;
    /// <summary>Spitze des Dorns (auf der Seite); Textkästen haben keinen Dorn.</summary>
    public double TailX { get; set; }
    public double TailY { get; set; }
    public string Text { get; set; } = "";
    public double Size { get; set; } = 22;
    public bool HasTail => Kind != "kasten";
    public double Pad => Kind == "kasten" ? 12 : Kind == "schrei" ? 30 : 22;

    public static ComicBalloon Make(string kind, double x, double y, double w, string text, double? tailX = null, double? tailY = null)
    {
        var b = new ComicBalloon { Kind = ComicBook.Kinds.Any(k => k.Id == kind) ? kind : "sprech", X = x, Y = y, W = Math.Max(80, w), Text = text, Size = kind == "kasten" ? 18 : 22, H = kind == "kasten" ? 52 : 110 };
        b.TailX = tailX ?? x + b.W * 0.3; b.TailY = tailY ?? y + b.H + 60;
        return b;
    }

    /// <summary>Automatischer Zeilenumbruch: Höhe wächst mit dem Text, damit nichts abgeschnitten wird.</summary>
    public void Fit()
    {
        var lines = Math.Max(1, ComicBook.Wrap(Text, W - 2 * Pad, Size).Count);
        var need = lines * Size * 1.25 + 2 * Pad * (Kind == "kasten" ? 1 : 0.9);
        if (H < need) H = Math.Min(ComicBook.PageH, need);
    }

    public void Move(double dx, double dy)
    {
        var nx = Math.Clamp(X + dx, 0, ComicBook.PageW - W); var ny = Math.Clamp(Y + dy, 0, ComicBook.PageH - H);
        TailX += nx - X; TailY += ny - Y; X = nx; Y = ny; (TailX, TailY) = ComicBook.Clamp(TailX, TailY);
    }

    public void Resize(double w, double h) { W = Math.Clamp(w, 60, ComicBook.PageW - X); H = Math.Clamp(h, 36, ComicBook.PageH - Y); Fit(); }

    /// <summary>Fußpunkte des Dorns am Rand der Blase (Breite ca. 12 % der Blase).</summary>
    public ((double X, double Y) A, (double X, double Y) B) TailBase()
    {
        double cx = X + W / 2, cy = Y + H / 2, dx = TailX - cx, dy = TailY - cy, len = Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
        double nx = -dy / len, ny = dx / len, half = Math.Min(W, H) * 0.16;
        // Fußpunkte liegen etwas innerhalb der Blase, damit der Dorn sauber ansetzt
        double bx = cx + dx / len * Math.Min(W, H) * 0.25, by = cy + dy / len * Math.Min(W, H) * 0.25;
        return ((bx + nx * half, by + ny * half), (bx - nx * half, by - ny * half));
    }

    /// <summary>Zacken des Ausrufs (Stern um die Ellipse).</summary>
    public List<(double X, double Y)> Burst()
    {
        var pts = new List<(double, double)>(); const int n = 22; double cx = X + W / 2, cy = Y + H / 2;
        for (var i = 0; i < n * 2; i++) { var a = Math.PI * i / n; var r = i % 2 == 0 ? 1.0 : 0.82; pts.Add((cx + Math.Cos(a) * W / 2 * r, cy + Math.Sin(a) * H / 2 * r)); }
        return pts;
    }

    static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    public string Svg()
    {
        var sb = new StringBuilder(); var fill = Kind == "kasten" ? "#FFF3B0" : "#FFFFFF";
        string Shape(string extra) => Kind switch
        {
            "kasten" => $"<rect x=\"{F(X)}\" y=\"{F(Y)}\" width=\"{F(W)}\" height=\"{F(H)}\" {extra}/>",
            "schrei" => $"<polygon points=\"{string.Join(" ", Burst().Select(p => F(p.X) + "," + F(p.Y)))}\" {extra}/>",
            _ => $"<ellipse cx=\"{F(X + W / 2)}\" cy=\"{F(Y + H / 2)}\" rx=\"{F(W / 2)}\" ry=\"{F(H / 2)}\" {extra}/>",
        };
        string Tail(string extra)
        {
            if (!HasTail) return "";
            if (Kind == "gedanke")
            {   // Gedankenblase: drei kleiner werdende Kreise zur Figur
                var s = new StringBuilder(); double cx = X + W / 2, cy = Y + H / 2;
                for (var k = 1; k <= 3; k++) { var t = 0.45 + k * 0.17; s.Append($"<circle cx=\"{F(cx + (TailX - cx) * t)}\" cy=\"{F(cy + (TailY - cy) * t)}\" r=\"{F(Math.Max(4, 16 - k * 4))}\" {extra}/>"); }
                return s.ToString();
            }
            var (a, b) = TailBase();
            return $"<polygon points=\"{F(a.X)},{F(a.Y)} {F(TailX)},{F(TailY)} {F(b.X)},{F(b.Y)}\" {extra}/>";
        }
        // erst mit Rand, dann noch einmal nur Füllung: so verschwindet die Linie, wo Dorn und Blase zusammenstoßen
        var stroke = $"fill=\"{fill}\" stroke=\"#111\" stroke-width=\"3\" stroke-linejoin=\"round\"";
        sb.Append(Tail(stroke)).Append(Shape(stroke));
        if (Kind != "gedanke") sb.Append(Tail($"fill=\"{fill}\"")).Append(Shape($"fill=\"{fill}\""));
        var lines = ComicBook.Wrap(Text, W - 2 * Pad, Size); var lh = Size * 1.25; var top = Y + (H - lines.Count * lh) / 2 + Size * 0.95;
        var anchor = Kind == "kasten" ? "start" : "middle"; var tx = Kind == "kasten" ? X + Pad : X + W / 2;
        var font = Kind == "kasten" ? "Segoe UI" : "Comic Sans MS, Segoe UI";
        for (var i = 0; i < lines.Count; i++)
            sb.Append($"<text x=\"{F(tx)}\" y=\"{F(top + i * lh)}\" font-size=\"{F(Size)}\" font-family=\"{font}\" text-anchor=\"{anchor}\"{(Kind == "schrei" ? " font-weight=\"700\"" : "")}>{KreativSafe.Xml(lines[i])}</text>");
        return sb.ToString();
    }

    public JsonObject ToJson() => new()
    {
        ["id"] = Id, ["kind"] = Kind, ["x"] = Math.Round(X, 2), ["y"] = Math.Round(Y, 2), ["w"] = Math.Round(W, 2), ["h"] = Math.Round(H, 2),
        ["tx"] = Math.Round(TailX, 2), ["ty"] = Math.Round(TailY, 2), ["text"] = Text, ["size"] = Size,
    };

    public static ComicBalloon FromJson(JsonObject o)
    {
        static double N(JsonNode? n, double d) => Programs.ProgramDocument.ToNumber(n, d);
        var kind = (o["kind"] as JsonValue)?.ToString() ?? "sprech";
        return new ComicBalloon
        {
            Id = (o["id"] as JsonValue)?.ToString() is { Length: > 0 } id ? id : "b" + Guid.NewGuid().ToString("N")[..8],
            Kind = ComicBook.Kinds.Any(k => k.Id == kind) ? kind : "sprech",
            X = N(o["x"], 40), Y = N(o["y"], 40), W = Math.Max(40, N(o["w"], 260)), H = Math.Max(30, N(o["h"], 110)), TailX = N(o["tx"], 100), TailY = N(o["ty"], 220),
            Text = o["text"] is JsonValue v && v.TryGetValue<string>(out var t) ? t : "", Size = Math.Clamp(N(o["size"], 22), 8, 96),
        };
    }
}

/// <summary>Ausgabe eines Comics: PDF für den Druck (eine Seite je Comicseite, DIN-A4-Verhältnis), CBZ (Comic-Archiv mit ComicInfo.xml) und Bildfelder als PNG-Folge.</summary>
public static class ComicExport
{
    /// <summary>Heft als Vektor-PDF für den Druck: eine DIN-A4-Seite je Comicseite. Rahmen, Blasen und Schrift bleiben gestochen scharf,
    /// Bilder werden im Bildfeld zugeschnitten, die Mal-Ebenen liegen als Bild in Seitengröße darüber. Liefert die Seitenzahl.</summary>
    public static int Pdf(ComicBook book, string path)
    {
        if (book.Pages.Count == 0) throw new InvalidOperationException("Das Heft hat keine Seite.");
        Sys.PdfTools.EnsureFonts();
        using var pdf = new PdfDocument(); pdf.Info.Title = book.Title.Trim().Length > 0 ? book.Title.Trim() : "Comic"; pdf.Info.Creator = "Trendo Comics";
        var k = XUnit.FromMillimeter(210).Point / ComicBook.PageW;
        var images = new Dictionary<string, XImage?>(StringComparer.OrdinalIgnoreCase);
        try
        {
            for (var i = 0; i < book.Pages.Count; i++)
            {
                var page = pdf.AddPage(); page.Width = XUnit.FromPoint(ComicBook.PageW * k); page.Height = XUnit.FromPoint(ComicBook.PageH * k);
                using var g = XGraphics.FromPdfPage(page);
                g.ScaleTransform(k);
                DrawPage(g, book, i, f =>
                {
                    if (images.TryGetValue(f, out var x)) return x;
                    try { x = KreativSafe.LocalFile(f, KreativSafe.MaxImageBytes) && Path.GetExtension(f).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" ? XImage.FromFile(f) : null; }
                    catch (Exception e) when (e is InvalidOperationException or IOException or ArgumentException or NotSupportedException) { x = null; }
                    return images[f] = x;
                });
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            var n = pdf.PageCount; var tmp = path + ".tmp"; pdf.Save(tmp); File.Move(tmp, path, true); return n;
        }
        finally { foreach (var x in images.Values) x?.Dispose(); }
    }

    static XPoint[] Pts(ComicPanel p) => [.. Enumerable.Range(0, 4).Select(i => new XPoint(p.P[i * 2], p.P[i * 2 + 1]))];

    static void DrawPage(XGraphics g, ComicBook book, int index, Func<string, XImage?> image)
    {
        var page = book.Pages[index]; var frame = new XPen(XColors.Black, 4) { LineJoin = XLineJoin.Round };
        g.DrawRectangle(XBrushes.White, 0, 0, ComicBook.PageW, ComicBook.PageH);
        if (index == 0 && book.Title.Trim().Length > 0) g.DrawString(book.Title.Trim(), new XFont("Segoe UI", 24, XFontStyleEx.Bold), XBrushes.Black, ComicBook.Margin, 28);
        foreach (var p in page.Panels)
        {
            if (p.Image.Length == 0 || image(p.Image) is not { } img) continue;
            var (x, y, w, h) = p.Bounds(); double iw = img.PixelWidth, ih = img.PixelHeight;
            var s = Math.Max(w / iw, h / ih) * Math.Max(1, p.Zoom); double dw = iw * s, dh = ih * s;
            var state = g.Save();
            var clip = new XGraphicsPath(); clip.AddPolygon(Pts(p)); g.IntersectClip(clip);
            g.DrawImage(img, x + (w - dw) / 2 + p.Ox * w, y + (h - dh) / 2 + p.Oy * h, dw, dh);
            g.Restore(state);
        }
        if (page.HasArt && page.Art is { } art && PaintDoc.FromJson(art) is { } pd && pd.Layers.Any(l => l.Visible && !l.Px.IsEmpty()))
        {
            using var ms = new MemoryStream(PngCodec.Encode(pd.Composite(false)));
            using var ai = XImage.FromStream(ms);
            g.DrawImage(ai, 0, 0, ComicBook.PageW, ComicBook.PageH);
        }
        for (var i = 0; i < page.Panels.Count; i++)
        {
            var p = page.Panels[i];
            g.DrawPolygon(frame, Pts(p));
            if (!book.Storyboard) continue;
            var (x, y, w, h) = p.Bounds(); var head = new XFont("Segoe UI", 18, XFontStyleEx.Bold); var body = new XFont("Segoe UI", 16);
            g.DrawString(p.ShotHead(i), head, XBrushes.Black, x, y + h + 24);
            var line = y + h + 24;
            foreach (var l in Lines(g, p.Shot.Trim(), body, w).Take(4)) { line += 21; g.DrawString(l, body, XBrushes.Black, x, line); }
        }
        foreach (var b in page.Balloons) Balloon(g, b);
    }

    /// <summary>Zeilenumbruch nach gemessener Breite (Wörter, zu lange Wörter werden geteilt).</summary>
    static List<string> Lines(XGraphics g, string text, XFont font, double width)
    {
        var lines = new List<string>();
        foreach (var para in (text ?? "").Replace("\r", "").Split('\n'))
        {
            var line = "";
            foreach (var raw in para.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var word = raw;
                while (g.MeasureString(word, font).Width > width && word.Length > 1)
                {
                    var n = word.Length - 1; while (n > 1 && g.MeasureString(word[..n], font).Width > width) n--;
                    if (line.Length > 0) { lines.Add(line); line = ""; }
                    lines.Add(word[..n]); word = word[n..];
                }
                var test = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && g.MeasureString(test, font).Width > width) { lines.Add(line); line = word; } else line = test;
            }
            lines.Add(line);
        }
        while (lines.Count > 1 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        return lines;
    }

    static void Balloon(XGraphics g, ComicBalloon b)
    {
        var fill = b.Kind == "kasten" ? new XSolidBrush(XColor.FromArgb(255, 255, 243, 176)) : XBrushes.White;
        var ink = new XPen(XColors.Black, 3) { LineJoin = XLineJoin.Round };
        void Shape(XPen? pen)
        {
            switch (b.Kind)
            {
                case "kasten": if (pen is null) g.DrawRectangle(fill, b.X, b.Y, b.W, b.H); else g.DrawRectangle(pen, fill, b.X, b.Y, b.W, b.H); break;
                case "schrei": { var pts = b.Burst().Select(p => new XPoint(p.X, p.Y)).ToArray(); if (pen is null) g.DrawPolygon(fill, pts, XFillMode.Winding); else g.DrawPolygon(pen, fill, pts, XFillMode.Winding); break; }
                default: if (pen is null) g.DrawEllipse(fill, b.X, b.Y, b.W, b.H); else g.DrawEllipse(pen, fill, b.X, b.Y, b.W, b.H); break;
            }
        }
        if (b.Kind == "gedanke")
        {
            double cx = b.X + b.W / 2, cy = b.Y + b.H / 2;
            for (var k = 1; k <= 3; k++) { var t = 0.45 + k * 0.17; var r = Math.Max(4, 16 - k * 4); g.DrawEllipse(ink, fill, cx + (b.TailX - cx) * t - r, cy + (b.TailY - cy) * t - r, 2 * r, 2 * r); }
            Shape(ink);
        }
        else
        {
            XPoint[]? tail = null;
            if (b.HasTail) { var (a, c) = b.TailBase(); tail = [new(a.X, a.Y), new(b.TailX, b.TailY), new(c.X, c.Y)]; }
            if (tail is not null) g.DrawPolygon(ink, fill, tail, XFillMode.Winding);
            Shape(ink);
            if (tail is not null) { g.DrawPolygon(fill, tail, XFillMode.Winding); Shape(null); }
        }
        var font = new XFont(b.Kind == "kasten" ? "Segoe UI" : "Comic Sans MS", b.Size, b.Kind == "schrei" ? XFontStyleEx.Bold : XFontStyleEx.Regular);
        var lines = Lines(g, b.Text, font, b.W - 2 * b.Pad); var lh = b.Size * 1.25;
        var top = b.Y + (b.H - lines.Count * lh) / 2;
        for (var i = 0; i < lines.Count; i++)
        {
            var w = g.MeasureString(lines[i], font).Width;
            var x = b.Kind == "kasten" ? b.X + b.Pad : b.X + (b.W - w) / 2;
            g.DrawString(lines[i], font, XBrushes.Black, x, top + i * lh + b.Size * 0.95);
        }
    }

    /// <summary>Comic-Archiv (.cbz): Seiten als 001.png, 002.png … und eine ComicInfo.xml (Titel, Seitenzahl) – lesen u. a. CDisplayEx, YACReader, Calibre.</summary>
    public static int Cbz(IReadOnlyList<byte[]> pages, string path, string title)
    {
        if (pages.Count == 0) throw new InvalidOperationException("Das Heft hat keine Seite.");
        var tmp = path + ".tmp";
        using (var fs = File.Create(tmp))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            for (var i = 0; i < pages.Count; i++)
            {
                var e = zip.CreateEntry($"{i + 1:000}.png", CompressionLevel.NoCompression); // PNG ist schon gepackt
                using var s = e.Open(); s.Write(pages[i]);
            }
            var info = zip.CreateEntry("ComicInfo.xml");
            using var w = new StreamWriter(info.Open(), new UTF8Encoding(false));
            w.Write($"<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<ComicInfo xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">\n  <Title>{KreativSafe.Xml(title)}</Title>\n  <PageCount>{pages.Count}</PageCount>\n  <LanguageISO>de</LanguageISO>\n</ComicInfo>\n");
        }
        File.Move(tmp, path, true);
        return pages.Count;
    }

    /// <summary>Ausschnitt eines Bildfelds aus der gerenderten Seite (Storyboard → Einstellungen für „Filme machen“). <paramref name="scale"/>: Bildpunkte je Seiteneinheit.</summary>
    public static Pixmap Crop(Pixmap page, ComicPanel panel, double scale)
    {
        var (x, y, w, h) = panel.Bounds();
        int x0 = Math.Clamp((int)Math.Floor(x * scale), 0, page.W - 1), y0 = Math.Clamp((int)Math.Floor(y * scale), 0, page.H - 1);
        int cw = Math.Clamp((int)Math.Ceiling(w * scale), 1, page.W - x0), ch = Math.Clamp((int)Math.Ceiling(h * scale), 1, page.H - y0);
        var r = new Pixmap(cw, ch);
        for (var row = 0; row < ch; row++) Buffer.BlockCopy(page.Px, ((y0 + row) * page.W + x0) * 4, r.Px, row * cw * 4, cw * 4);
        return r;
    }

    /// <summary>Breite und Höhe aus dem IHDR einer PNG-Datei.</summary>
    public static (int W, int H) PngSize(byte[] png)
    {
        if (png.Length < 24 || png[0] != 0x89 || png[1] != (byte)'P') throw new InvalidDataException("Keine PNG-Datei.");
        static int Be(byte[] b, int i) => (b[i] << 24) | (b[i + 1] << 16) | (b[i + 2] << 8) | b[i + 3];
        return (Be(png, 16), Be(png, 20));
    }
}
