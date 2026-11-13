using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>W1 und Z4: Geräte-Ansichten. Desktop ist die Grundlage; jede kleinere Ansicht hat einen Namen und eine Breite, bis zu der sie gilt.
/// Anpassungen eines Bausteins gelten in ihrer Ansicht und allen kleineren, bis eine kleinere sie überschreibt; nach oben wirken sie nie.
/// Im Export werden sie zu Medienregeln. Gleiche Regel-Erzeugung wie im Editor (<c>tools/webdesign-editor/src/ansichten.js</c>).</summary>
public static partial class WebDesignViews
{
    public const string Base = "Desktop";
    public const int MinWidth = 240, MaxWidth = 1920, MaxViews = 8;
    public sealed record View(string Name, int Breite);
    public static readonly IReadOnlyList<View> Defaults = [new("Tablet", 1024), new("Handy", 600)];
    /// <summary>Was eine Ansicht überschreiben kann. „spalten“ nur bei Spalten.</summary>
    public static readonly IReadOnlyList<string> Fields = ["ausblenden", "ausrichtung", "schrift", "spalten"];
    static readonly string[] Align = ["left", "center", "right"];

    [GeneratedRegex(@"^[\p{L}0-9 ._-]{1,40}$")] private static partial Regex NamePattern();
    [GeneratedRegex("^[A-Za-z0-9_-]{1,80}$")] private static partial Regex IdPattern();

    /// <summary>Ansichten der Seite, breiteste zuerst. Fehlt die Liste, gelten Tablet und Handy; eine leere Liste heißt: nur Desktop.</summary>
    public static IReadOnlyList<View> Of(JsonObject? rootProps)
    {
        if (rootProps?["ansichten"] is not JsonArray a) return Defaults;
        var list = new List<View>();
        foreach (var v in a.OfType<JsonObject>())
            if (v["name"] is JsonValue n && n.TryGetValue(out string? name) && WebDesign.Number(v["breite"]) is { } w && w == Math.Floor(w)) list.Add(new(name, (int)w));
        return list.OrderByDescending(v => v.Breite).ToList();
    }

    public static JsonArray ToJson(IEnumerable<View> views) => new(views.OrderByDescending(v => v.Breite).Select(v => (JsonNode)new JsonObject { ["name"] = v.Name, ["breite"] = v.Breite }).ToArray());

    /// <summary>Prüft eine Ansicht gegen die übrigen. Null heißt gültig.</summary>
    public static string? CheckView(string name, int breite, IEnumerable<View> others)
    {
        if (!NamePattern().IsMatch(name)) return "Der Name der Ansicht darf 1 bis 40 Buchstaben, Ziffern, Leerzeichen, Punkt, Strich oder Unterstrich haben.";
        if (name.Equals(Base, StringComparison.OrdinalIgnoreCase)) return $"{Base} ist die Grundlage und keine eigene Ansicht.";
        if (breite is < MinWidth or > MaxWidth) return $"Die Breite muss zwischen {MinWidth} und {MaxWidth} Pixeln liegen.";
        var rest = others.Where(o => !o.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (rest.Any(o => o.Breite == breite)) return $"Eine andere Ansicht hat schon die Breite {breite}.";
        if (rest.Count >= MaxViews) return $"Höchstens {MaxViews} Ansichten.";
        return null;
    }

    /// <summary>Prüft einen Wert für eine Anpassung. Liefert den bereinigten Wert oder einen Fehlertext.</summary>
    public static (JsonNode? Value, string? Error) CheckOverride(string type, string field, JsonNode? value)
    {
        switch (field)
        {
            case "ausblenden": return value is JsonValue b && b.TryGetValue(out bool x) ? (JsonValue.Create(x), null) : (null, "„ausblenden“ muss wahr oder falsch sein.");
            case "ausrichtung": return value is JsonValue s && s.TryGetValue(out string? a) && Align.Contains(a) ? (JsonValue.Create(a), null) : (null, "„ausrichtung“ muss left, center oder right sein.");
            case "schrift": return WebDesign.Number(value) is { } d && d == Math.Floor(d) && d is >= 50 and <= 200 ? (JsonValue.Create((int)d), null) : (null, "„schrift“ muss eine ganze Zahl von 50 bis 200 (Prozent) sein.");
            case "spalten":
                if (type != "Spalten") return (null, "„spalten“ gibt es nur bei Spalten.");
                return WebDesign.Number(value) is { } k && k is 1 or 2 ? (JsonValue.Create((int)k), null) : (null, "„spalten“ muss 1 oder 2 sein.");
            default: return (null, $"„{field}“ lässt sich je Ansicht nicht anpassen (möglich: {string.Join(", ", Fields)}).");
        }
    }

    static IEnumerable<JsonObject> Blocks(JsonObject doc)
    {
        IEnumerable<JsonObject> Walk(JsonNode? n)
        {
            if (n is not JsonArray list) yield break;
            foreach (var b in list.OfType<JsonObject>())
            {
                yield return b;
                if (b["props"] is JsonObject p)
                    foreach (var kv in p)
                        if (kv.Value is JsonArray a && a.Count > 0 && a.All(x => x is JsonObject o && o["type"] is not null))
                            foreach (var x in Walk(a)) yield return x;
            }
        }
        foreach (var b in Walk(doc["content"])) yield return b;
        if (doc["zones"] is JsonObject z) foreach (var kv in z) foreach (var b in Walk(kv.Value)) yield return b;
    }

    /// <summary>Entfernt ungültige Ansichten und Anpassungen (Daten aus dem Editor oder einer Datei).</summary>
    public static void Sanitize(JsonObject doc)
    {
        var props = doc["root"]?["props"] as JsonObject;
        if (props?["ansichten"] is { } raw)
        {
            var kept = new List<View>();
            if (raw is JsonArray a)
                foreach (var v in Of(new JsonObject { ["ansichten"] = a.DeepClone() }))
                    if (CheckView(v.Name, v.Breite, kept) is null && !kept.Any(k => k.Name.Equals(v.Name, StringComparison.OrdinalIgnoreCase))) kept.Add(v);
            props["ansichten"] = ToJson(kept);
        }
        var names = Of(props).Select(v => v.Name).ToHashSet();
        foreach (var b in Blocks(doc))
        {
            if (b["props"] is not JsonObject p || p["anpassungen"] is not { } ov) continue;
            if (ov is not JsonObject all) { p.Remove("anpassungen"); continue; }
            var type = b["type"]?.ToString() ?? "";
            foreach (var view in all.Select(kv => kv.Key).ToList())
            {
                if (!names.Contains(view) || all[view] is not JsonObject vals) { all.Remove(view); continue; }
                foreach (var f in vals.Select(kv => kv.Key).ToList())
                {
                    var (val, err) = CheckOverride(type, f, vals[f]);
                    if (err is null) vals[f] = val; else vals.Remove(f);
                }
                if (vals.Count == 0) all.Remove(view);
            }
            if (all.Count == 0) p.Remove("anpassungen");
        }
    }

    /// <summary>Wert eines Feldes in einer Ansicht: eigene Anpassung, sonst die der nächstgrößeren Ansicht; null heißt: wie Desktop.</summary>
    public static (JsonNode? Value, string? From) Effective(JsonObject doc, string id, string view, string field)
    {
        var block = Blocks(doc).FirstOrDefault(b => b["props"]?["id"]?.ToString() == id);
        var ov = block?["props"]?["anpassungen"] as JsonObject;
        JsonNode? value = null; string? from = null;
        if (view.Equals(Base, StringComparison.OrdinalIgnoreCase)) return (null, null);
        foreach (var v in Of(doc["root"]?["props"] as JsonObject))
        {
            if (ov?[v.Name]?[field] is { } w) { value = w; from = v.Name; }
            if (v.Name == view) break;
        }
        return (value, from);
    }

    /// <summary>CSS der Anpassungen. <paramref name="container"/>: Regeln nach der Seitenbreite (Editor), sonst nach der Bildschirmbreite (Export).</summary>
    public static string Css(JsonObject doc, bool container = false)
    {
        var parts = new List<string>();
        var blocks = Blocks(doc).ToList();
        foreach (var v in Of(doc["root"]?["props"] as JsonObject))
        {
            var rules = new StringBuilder();
            foreach (var b in blocks)
            {
                var id = b["props"]?["id"]?.ToString();
                if (id is null || !IdPattern().IsMatch(id) || b["props"]?["anpassungen"]?[v.Name] is not JsonObject a) continue;
                var d = new List<string>();
                if (a["ausblenden"] is JsonValue h && h.TryGetValue(out bool hide)) d.Add(hide ? "display:none" : "display:block");
                if (WebDesign.Number(a["schrift"]) is { } size && size == Math.Floor(size) && size is >= 50 and <= 200) { d.Add($"font-size:{(int)size}%"); d.Add("--t-schrift:" + (size / 100).ToString(System.Globalization.CultureInfo.InvariantCulture)); }
                if (d.Count > 0) rules.Append($"[data-b=\"{id}\"]{{{string.Join(";", d)}}}");
                // Ausrichtung auch direkt am Inhalt, damit sie die Grundausrichtung des Bausteins schlägt
                if (a["ausrichtung"] is JsonValue al && al.TryGetValue(out string? align) && Align.Contains(align)) rules.Append($"[data-b=\"{id}\"],.t-page [data-b=\"{id}\"]>*,[data-b=\"{id}\"] .t-hero{{text-align:{align}}}");
                if (b["type"]?.ToString() == "Spalten" && WebDesign.Number(a["spalten"]) is { } cols && cols is 1 or 2)
                    rules.Append($"[data-b=\"{id}\"]>.t-cols-box>.t-cols{{grid-template-columns:repeat({(int)cols},minmax(0,1fr))}}");
            }
            if (rules.Length > 0) parts.Add($"{(container ? "@container seite" : "@media")} (max-width: {v.Breite}px){{{rules}}}");
        }
        return string.Join("\n", parts);
    }
}
