using System.Globalization;
using System.Text;

namespace Trendo.Core;

/// <summary>BUS-013 Marke, BUS-015 Kundenweg & Abläufe, BUS-021 Sichtbarkeit (SEO), BUS-022 Verkaufstrichter.</summary>
public sealed record Touchpoint(string Stage, string Where, string Feeling, string Improve);
public sealed record ProcessStep(string Who, string What);

public static class BusinessBrand
{
    // ---------- BUS-013 ----------
    public sealed record NameCheck(int Score, IReadOnlyList<string> Hints, IReadOnlyList<string> Domains);
    public static NameCheck CheckName(string name)
    {
        var h = new List<string>(); var n = name.Trim(); var score = 100;
        var letters = new string(GlobalSearch.Norm(n).Where(char.IsLetterOrDigit).ToArray());
        if (letters.Length == 0) return new(0, ["Gib einen Namen ein."], []);
        if (letters.Length > 14) { score -= 25; h.Add("Kürzer ist leichter zu merken (unter 15 Zeichen)."); }
        if (letters.Length < 3) { score -= 25; h.Add("Sehr kurz – schwer zu finden."); }
        if (n.Any(char.IsDigit)) { score -= 10; h.Add("Zahlen im Namen werden oft falsch geschrieben."); }
        if (n.Contains('-') || n.Contains('_')) { score -= 10; h.Add("Bindestriche gehen beim Weitersagen verloren."); }
        var vowels = letters.Count(c => "aeiouy".Contains(c));
        if (vowels * 4 < letters.Length) { score -= 20; h.Add("Wenig Selbstlaute – schwer auszusprechen."); }
        if (h.Count == 0) h.Add("Gut: kurz, aussprechbar, ohne Sonderzeichen.");
        h.Add("Prüfe die Marke im DPMA-Register (register.dpma.de) und ob die Domain frei ist.");
        return new(Math.Max(0, score), h, [letters + ".de", letters + ".com", "get" + letters + ".de"]);
    }

    public static (double H, double S, double L) Hsl(string hex)
    {
        double r = Convert.ToInt32(hex[1..3], 16) / 255.0, g = Convert.ToInt32(hex[3..5], 16) / 255.0, b = Convert.ToInt32(hex[5..7], 16) / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), l = (max + min) / 2, d = max - min, h = 0, s = 0;
        if (d > 0) { s = l > 0.5 ? d / (2 - max - min) : d / (max + min); h = max == r ? (g - b) / d + (g < b ? 6 : 0) : max == g ? (b - r) / d + 2 : (r - g) / d + 4; h *= 60; }
        return (h, s, l);
    }
    public static string Hex(double h, double s, double l)
    {
        h = ((h % 360) + 360) % 360; double c = (1 - Math.Abs(2 * l - 1)) * s, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = l - c / 2;
        var (r, g, b) = h < 60 ? (c, x, 0.0) : h < 120 ? (x, c, 0.0) : h < 180 ? (0.0, c, x) : h < 240 ? (0.0, x, c) : h < 300 ? (x, 0.0, c) : (c, 0.0, x);
        int B(double v) => (int)Math.Round(Math.Clamp(v + m, 0, 1) * 255);
        return $"#{B(r):X2}{B(g):X2}{B(b):X2}";
    }
    /// <summary>Hauptfarbe, Ergänzung (gegenüber), zwei Nachbarn, dunkler Text und heller Hintergrund.</summary>
    public static IReadOnlyList<(string Role, string Hex)> Palette(string main)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(main, "^#[0-9A-Fa-f]{6}$")) main = "#2F86F6";
        var (h, s, l) = Hsl(main);
        return [("Hauptfarbe", main.ToUpperInvariant()), ("Akzent", Hex(h + 180, s, l)), ("Nachbar 1", Hex(h + 30, s, l)), ("Nachbar 2", Hex(h - 30, s, l)), ("Text", Hex(h, Math.Min(s, 0.3), 0.12)), ("Hintergrund", Hex(h, Math.Min(s, 0.4), 0.97))];
    }
    /// <summary>Kontrast nach WCAG (1..21).</summary>
    public static double Contrast(string a, string b)
    {
        double Lum(string x) { double C(int i) { var v = Convert.ToInt32(x[i..(i + 2)], 16) / 255.0; return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4); } return 0.2126 * C(1) + 0.7152 * C(3) + 0.0722 * C(5); }
        var (l1, l2) = (Lum(a), Lum(b)); return (Math.Max(l1, l2) + 0.05) / (Math.Min(l1, l2) + 0.05);
    }
    public static IReadOnlyList<string> Taglines(BusinessIdea i) =>
    [
        $"{i.Name} – {(i.Solution.Length > 0 ? i.Solution : "einfach gelöst")}.",
        $"Nie wieder {(i.Problem.Length > 0 ? i.Problem.TrimEnd('.') : "Stress")}.",
        $"Gemacht für {(i.Audience.Length > 0 ? i.Audience : "dich")}.",
    ];
    public static string BrandCss(IReadOnlyList<(string Role, string Hex)> p) =>
        ":root {\n" + string.Concat(p.Select(x => $"  --{GlobalSearch.Norm(x.Role).Replace(' ', '-')}: {x.Hex};\n")) + "}\n";

    // ---------- BUS-015 ----------
    public static readonly string[] Stages = ["Entdecken", "Vergleichen", "Kaufen", "Nutzen", "Wiederkommen", "Weiterempfehlen"];
    public static string JourneyMarkdown(BusinessIdea i, IReadOnlyList<Touchpoint> t, IReadOnlyList<ProcessStep> p)
    {
        var sb = new StringBuilder($"# Kundenweg: {i.Name}\n\n| Schritt | Wo | Gefühl | Besser machen |\n|---|---|---|---|\n");
        foreach (var s in Stages) foreach (var x in t.Where(x => x.Stage == s)) sb.AppendLine($"| {s} | {x.Where} | {x.Feeling} | {x.Improve} |");
        var missing = Stages.Where(s => t.All(x => x.Stage != s)).ToList();
        if (missing.Count > 0) sb.AppendLine($"\n⚠ Noch leer: {string.Join(", ", missing)}");
        sb.AppendLine("\n## Ablauf\n\n```mermaid\nflowchart LR");
        for (var k = 0; k < p.Count; k++) sb.AppendLine($"  S{k}[\"{p[k].Who}: {p[k].What.Replace("\"", "'")}\"]" + (k > 0 ? $"\n  S{k - 1} --> S{k}" : ""));
        return sb.AppendLine("```").ToString();
    }

    // ---------- BUS-021 ----------
    public sealed record SeoCheck(string Title, string Description, string Slug, IReadOnlyList<string> Hints, double Density);
    public static string Slug(string s)
    {
        var n = GlobalSearch.Norm(s).Replace("ae", "ae");
        var sb = new StringBuilder(); foreach (var c in n) sb.Append(char.IsLetterOrDigit(c) ? c : '-');
        return System.Text.RegularExpressions.Regex.Replace(sb.ToString(), "-+", "-").Trim('-');
    }
    public static SeoCheck Seo(string keyword, string brand, string text)
    {
        var h = new List<string>(); keyword = keyword.Trim();
        var title = $"{keyword} | {brand}"; if (title.Length > 60) title = title[..57] + "…";
        var first = text.Split(['.', '!', '?'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "";
        var desc = first.Length > 0 ? first : $"{keyword} von {brand}."; if (desc.Length > 155) desc = desc[..152] + "…";
        var words = BusinessMarket.Words(text).ToList(); var kw = BusinessMarket.Words(keyword).ToList();
        var hits = kw.Count == 0 ? 0 : Enumerable.Range(0, Math.Max(0, words.Count - kw.Count + 1)).Count(k => words.Skip(k).Take(kw.Count).SequenceEqual(kw));
        var density = words.Count == 0 ? 0 : Math.Round(hits * kw.Count * 100.0 / words.Count, 1);
        if (words.Count < 300) h.Add($"Text hat {words.Count} Wörter – für Suchmaschinen besser 300 oder mehr.");
        if (density == 0) h.Add("Das Suchwort kommt im Text nicht vor.");
        else if (density > 3) h.Add($"Suchwort zu oft ({density} %) – wirkt unnatürlich.");
        if (!GlobalSearch.Norm(desc).Contains(GlobalSearch.Norm(keyword))) h.Add("Nimm das Suchwort in den ersten Satz.");
        if (h.Count == 0) h.Add("Sieht gut aus.");
        return new(title, desc, Slug(keyword), h, density);
    }

    // ---------- BUS-022 ----------
    public sealed record FunnelStage(string Name, double Count);
    public sealed record FunnelResult(IReadOnlyList<(string Name, double Count, double Rate)> Stages, string Bottleneck, double Revenue);
    public static FunnelResult Funnel(IReadOnlyList<FunnelStage> s, double price)
    {
        var res = new List<(string, double, double)>();
        for (var k = 0; k < s.Count; k++) res.Add((s[k].Name, s[k].Count, k == 0 || s[k - 1].Count <= 0 ? 1 : s[k].Count / s[k - 1].Count));
        var worst = res.Skip(1).OrderBy(r => r.Item3).FirstOrDefault();
        return new(res, worst.Item1 is null ? "" : $"Größter Verlust vor „{worst.Item1}“ ({worst.Item3:P0} kommen weiter).", s.Count == 0 ? 0 : s[^1].Count * price);
    }
    public static readonly string[] DefaultFunnel = ["Besucher", "Interessenten", "Gespräch/Test", "Kunden"];
}
