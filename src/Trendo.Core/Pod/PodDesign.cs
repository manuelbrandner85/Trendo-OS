using System.Globalization;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trendo.Core.Pod;

/// <summary>Ein Element des Entwurfs. Maße in Einheiten der Arbeitsfläche (30 Einheiten je Zentimeter Druckbereich).</summary>
public sealed record PodItem(string Id, string Kind, double X, double Y, double W, double H, double Rot = 0, string Fill = "#2F6BF6", string Stroke = "", double StrokeW = 0,
    string Text = "", string Font = "Inter", double Size = 80, bool Bold = false, bool Shadow = false, string Src = "", string Group = "", bool Hidden = false)
{
    public double Right => X + W;
    public double Bottom => Y + H;
    public double CenterX => X + W / 2;
    public double CenterY => Y + H / 2;
}

/// <summary>Print on Demand: Entwurf aus Text, Formen und Bildern. Der Inhalt liegt unter „design“ im Dokument, jede Änderung läuft über das Dokument und lässt sich rückgängig machen.</summary>
public static class PodDesign
{
    public const double UnitsPerCm = 30;
    public const double PrintDpi = 300;
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static readonly JsonSerializerOptions Json = new() { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never };

    public static (double W, double H) Board(PodProduct p) => (p.PrintWidthCm * UnitsPerCm, p.PrintHeightCm * UnitsPerCm);
    /// <summary>Pixel der Druckdatei je Einheit bei 300 DPI.</summary>
    public static double PixelsPerUnit => PrintDpi / 2.54 / UnitsPerCm;

    public static List<PodItem> Items(JsonObject doc) => doc["design"] is JsonArray a ? a.Deserialize<List<PodItem>>(Json) ?? [] : [];
    public static void Store(JsonObject doc, IEnumerable<PodItem> items) => doc["design"] = JsonSerializer.SerializeToNode(items.ToList(), Json);

    static string NewId() => Guid.NewGuid().ToString("N")[..8];

    public static PodItem New(string kind, double boardW, double boardH, string text = "", string src = "")
    {
        if (kind is not ("text" or "rect" or "ellipse" or "image")) throw new ArgumentException("Unbekannte Art: " + kind);
        var (w, h) = kind == "text" ? (boardW * 0.7, 120d) : (boardW * 0.4, boardW * 0.4);
        return new(NewId(), kind, (boardW - w) / 2, (boardH - h) / 2, w, h, Text: kind == "text" ? (text.Length > 0 ? text : "Dein Text") : "", Src: src, Fill: kind == "text" ? "#111111" : "#2F6BF6");
    }

    /// <summary>Alle Elemente, die mit dieser Auswahl mitgehen: ganze Gruppen statt einzelner Mitglieder.</summary>
    public static HashSet<string> WithGroups(IReadOnlyList<PodItem> items, IEnumerable<string> ids)
    {
        var set = ids.ToHashSet();
        var groups = items.Where(i => set.Contains(i.Id) && i.Group.Length > 0).Select(i => i.Group).ToHashSet();
        foreach (var i in items) if (groups.Contains(i.Group)) set.Add(i.Id);
        return set;
    }

    public static List<PodItem> Duplicates(IEnumerable<PodItem> items, double offset = 24)
    {
        var map = new Dictionary<string, string>();
        return items.Select(i => i with { Id = NewId(), X = i.X + offset, Y = i.Y + offset, Group = i.Group.Length == 0 ? "" : map.TryGetValue(i.Group, out var g) ? g : map[i.Group] = NewId() }).ToList();
    }

    /// <summary>Reihenfolge ändern: „up“, „down“, „front“, „back“. Am Ende der Liste liegt das Oberste.</summary>
    public static List<PodItem> Arrange(IReadOnlyList<PodItem> items, IReadOnlySet<string> ids, string how)
    {
        var l = items.ToList();
        var chosen = l.Where(i => ids.Contains(i.Id)).ToList(); var rest = l.Where(i => !ids.Contains(i.Id)).ToList();
        switch (how)
        {
            case "front": return [.. rest, .. chosen];
            case "back": return [.. chosen, .. rest];
            case "up":
                for (var i = l.Count - 2; i >= 0; i--) if (ids.Contains(l[i].Id) && !ids.Contains(l[i + 1].Id)) (l[i], l[i + 1]) = (l[i + 1], l[i]);
                return l;
            case "down":
                for (var i = 1; i < l.Count; i++) if (ids.Contains(l[i].Id) && !ids.Contains(l[i - 1].Id)) (l[i], l[i - 1]) = (l[i - 1], l[i]);
                return l;
            default: throw new ArgumentException("Unbekannt: " + how);
        }
    }

    public static List<PodItem> Group(IReadOnlyList<PodItem> items, IReadOnlySet<string> ids)
    {
        if (ids.Count < 2) return items.ToList();
        var g = NewId();
        return items.Select(i => ids.Contains(i.Id) ? i with { Group = g } : i).ToList();
    }

    public static List<PodItem> Ungroup(IReadOnlyList<PodItem> items, IReadOnlySet<string> ids) => items.Select(i => ids.Contains(i.Id) ? i with { Group = "" } : i).ToList();

    /// <summary>Rastet die Kanten und Mitten eines Rechtecks an denen der anderen Elemente und der Arbeitsfläche ein. Liefert die Verschiebung je Achse.</summary>
    public static (double Dx, double Dy) Snap(double x, double y, double w, double h, IEnumerable<PodItem> others, double boardW, double boardH, double tolerance)
    {
        var xs = new List<double> { 0, boardW / 2, boardW }; var ys = new List<double> { 0, boardH / 2, boardH };
        foreach (var o in others.Where(o => !o.Hidden)) { xs.AddRange([o.X, o.CenterX, o.Right]); ys.AddRange([o.Y, o.CenterY, o.Bottom]); }
        static double Best(double[] mine, List<double> lines, double tol)
        {
            var best = 0d; var dist = tol;
            foreach (var m in mine) foreach (var t in lines) { var d = Math.Abs(t - m); if (d < dist) { dist = d; best = t - m; } }
            return best;
        }
        return (Best([x, x + w / 2, x + w], xs, tolerance), Best([y, y + h / 2, y + h], ys, tolerance));
    }

    /// <summary>Punkt liegt im gedrehten Rechteck?</summary>
    public static bool Hit(PodItem i, double px, double py)
    {
        var a = -i.Rot * Math.PI / 180; var (dx, dy) = (px - i.CenterX, py - i.CenterY);
        var (rx, ry) = (dx * Math.Cos(a) - dy * Math.Sin(a), dx * Math.Sin(a) + dy * Math.Cos(a));
        return Math.Abs(rx) <= i.W / 2 && Math.Abs(ry) <= i.H / 2;
    }

    /// <summary>Palette der Person: gültige Farben, ohne Doppelte, höchstens 24.</summary>
    public static List<string> AddColor(IEnumerable<string> palette, string color)
    {
        if (!IsColor(color)) throw new ArgumentException("Farben bitte als #RRGGBB angeben.");
        return palette.Append(color.ToUpperInvariant()).Distinct().TakeLast(24).ToList();
    }
    public static bool IsColor(string c) => c.Length == 7 && c[0] == '#' && c.Skip(1).All(Uri.IsHexDigit);

    public static string Svg(IEnumerable<PodItem> items, double boardW, double boardH)
    {
        static string N(double v) => v.ToString("0.##", Inv);
        var sb = new StringBuilder($"<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" viewBox=\"0 0 {N(boardW)} {N(boardH)}\" width=\"{N(boardW / UnitsPerCm)}cm\" height=\"{N(boardH / UnitsPerCm)}cm\">\n");
        foreach (var i in items.Where(i => !i.Hidden))
        {
            var rot = i.Rot != 0 ? $" transform=\"rotate({N(i.Rot)} {N(i.CenterX)} {N(i.CenterY)})\"" : "";
            var stroke = i.StrokeW > 0 && i.Stroke.Length > 0 ? $" stroke=\"{SecurityElement.Escape(i.Stroke)}\" stroke-width=\"{N(i.StrokeW)}\"" : "";
            var fill = SecurityElement.Escape(i.Fill);
            switch (i.Kind)
            {
                case "rect": sb.Append($"<rect x=\"{N(i.X)}\" y=\"{N(i.Y)}\" width=\"{N(i.W)}\" height=\"{N(i.H)}\" fill=\"{fill}\"{stroke}{rot}/>\n"); break;
                case "ellipse": sb.Append($"<ellipse cx=\"{N(i.CenterX)}\" cy=\"{N(i.CenterY)}\" rx=\"{N(i.W / 2)}\" ry=\"{N(i.H / 2)}\" fill=\"{fill}\"{stroke}{rot}/>\n"); break;
                case "text": sb.Append($"<text x=\"{N(i.X)}\" y=\"{N(i.Y + i.Size)}\" font-family=\"{SecurityElement.Escape(i.Font)}\" font-size=\"{N(i.Size)}\" font-weight=\"{(i.Bold ? "bold" : "normal")}\" fill=\"{fill}\"{stroke}{rot}>{SecurityElement.Escape(i.Text)}</text>\n"); break;
                case "image" when File.Exists(i.Src):
                    var mime = i.Src.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg";
                    sb.Append($"<image x=\"{N(i.X)}\" y=\"{N(i.Y)}\" width=\"{N(i.W)}\" height=\"{N(i.H)}\" preserveAspectRatio=\"none\" xlink:href=\"data:{mime};base64,{Convert.ToBase64String(File.ReadAllBytes(i.Src))}\"{rot}/>\n"); break;
            }
        }
        return sb.Append("</svg>\n").ToString();
    }
}
