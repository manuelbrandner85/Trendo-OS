using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>A6: Die KI prüft ihr Ergebnis. Ein Kreislauf aus Prüfen und gezieltem Nachbessern mit fester Obergrenze; geprüft wird die exportierte Seite. Das Ergebnis bleibt eine Vorschau, bis der Mensch übernimmt.</summary>
public static partial class WebDesignCheck
{
    /// <summary>Höchstzahl der Nachbesserungen je Auftrag.</summary>
    public const int MaxRepairs = 3;
    /// <summary>Breiten der Bilder (Handy, Tablet, Desktop), wie die Ansichten im Editor.</summary>
    public static readonly (int Width, int Height, string Name)[] Widths = [(390, 844, "handy"), (820, 1180, "tablet"), (1440, 900, "desktop")];

    /// <summary>Diese Prüfungen der Qualitätsprüfung suchen feste Texte in der <c>stil.css</c> der Seiten-Vorlage. Beim Export gilt stattdessen das Messen im Browser und die CSS des Editors.</summary>
    public static readonly IReadOnlySet<string> TemplateOnlyCodes = new HashSet<string> { "tap", "media", "overflow", "font", "focus", "motion" };

    public sealed record Round(int Nr, IReadOnlyList<WebsiteFinding> Errors, IReadOnlyList<string> Repairs, string? Note);
    public sealed record LoopResult(string Json, bool Clean, bool LimitReached, int Repairs, IReadOnlyList<WebsiteFinding> Findings, IReadOnlyList<Round> Rounds);

    /// <summary>Prüfen, bei Fehlern gezielt nachbessern, erneut prüfen. Nachbesserungen laufen durch dieselbe Aktionsprüfung wie jeder andere Plan; ungültige werden verworfen.</summary>
    public static async Task<LoopResult> RunAsync(string json, Func<string, Task<IReadOnlyList<WebsiteFinding>>> check, Func<string, IReadOnlyList<WebsiteFinding>, Task<string?>>? repair = null, WebPlanScope? scope = null, int maxRepairs = MaxRepairs)
    {
        var current = json; var rounds = new List<Round>(); var repairs = 0;
        for (var nr = 1; ; nr++)
        {
            var findings = await check(current);
            var errors = findings.Where(f => f.Level == "Fehler").ToList();
            if (errors.Count == 0) { rounds.Add(new(nr, errors, [], null)); return new(current, true, false, repairs, findings, rounds); }
            if (repair is null) { rounds.Add(new(nr, errors, [], null)); return new(current, false, false, repairs, findings, rounds); }
            if (repairs >= maxRepairs) { rounds.Add(new(nr, errors, [], $"Obergrenze von {maxRepairs} Nachbesserungen erreicht.")); return new(current, false, true, repairs, findings, rounds); }
            repairs++;
            var answer = await repair(current, errors);
            if (answer is null) { rounds.Add(new(nr, errors, [], "Die KI hat nicht nachgebessert.")); return new(current, false, false, repairs, findings, rounds); }
            var r = WebDesignActions.Apply(current, answer, scope);
            if (r.Json is null) { rounds.Add(new(nr, errors, [], "Nachbesserung verworfen: " + r.Error)); return new(current, false, false, repairs, findings, rounds); }
            rounds.Add(new(nr, errors, r.Changes, null));
            if (r.Json == current) return new(current, false, false, repairs, findings, rounds);
            current = r.Json;
        }
    }

    [GeneratedRegex("lorem ipsum", RegexOptions.IgnoreCase)] private static partial Regex Lorem();
    static readonly string[] DefaultTexts = ["Hier steht dein Text.", "Eine Seite, gebaut mit Trendo."];

    /// <summary>Prüfung der Editor-Daten, ohne Browser: defekte Verweise, fehlende Alternativtexte, Überschriften, Platzhalter, Kontrast.</summary>
    public static IReadOnlyList<WebsiteFinding> CheckData(string json, string projectDir)
    {
        var f = new List<WebsiteFinding>();
        if (JsonNode.Parse(json) is not JsonObject doc) return [new("SEO", "Fehler", "Die Seitendaten sind unlesbar.", "data")];
        var blocks = new List<JsonObject>();
        void Walk(JsonNode? n)
        {
            if (n is JsonArray a) foreach (var x in a) Walk(x);
            else if (n is JsonObject o) { if (o["type"] is not null) blocks.Add(o); foreach (var kv in o) Walk(kv.Value); }
        }
        Walk(doc["content"]); Walk(doc["zones"]);
        var root = doc["root"]?["props"] as JsonObject;
        if (string.IsNullOrWhiteSpace(root?["title"]?.ToString())) f.Add(new("SEO", "Fehler", "Der Seite fehlt ein Titel.", "title"));
        if (string.IsNullOrWhiteSpace(root?["description"]?.ToString())) f.Add(new("SEO", "Hinweis", "Die Seite hat keine Beschreibung für Suchmaschinen.", "description"));
        if (root?["ink"]?.ToString() is { Length: 7 } ink && root["paper"]?.ToString() is { Length: 7 } paper && WebsiteColor.Contrast(ink, paper) < 4.5)
            f.Add(new("Barrierefreiheit", "Fehler", $"Text auf dem Hintergrund ist zu schwach ({WebsiteColor.Contrast(ink, paper):0.0}:1).", "contrast"));
        var heroes = blocks.Count(b => b["type"]!.ToString() == "Kopfbereich");
        if (heroes == 0) f.Add(new("SEO", "Fehler", "Die Seite hat keine Hauptüberschrift (Kopfbereich fehlt).", "h1"));
        if (heroes > 1) f.Add(new("SEO", "Fehler", $"Die Seite hat {heroes} Hauptüberschriften, es sollte eine sein.", "h1"));
        var previous = 1;
        foreach (var b in blocks)
        {
            var t = b["type"]!.ToString(); var p = b["props"] as JsonObject; var id = p?["id"]?.ToString() ?? t;
            if (t == "Ueberschrift" && p?["level"]?.ToString() is { } lv && int.TryParse(lv.AsSpan(1), out var level))
            {
                if (level > previous + 1) f.Add(new("Barrierefreiheit", "Fehler", $"Überschrift „{id}“ springt von Ebene {previous} auf {level}.", "heading-order"));
                previous = level;
            }
            if (t == "Bild")
            {
                var src = p?["src"]?.ToString() ?? "";
                if (src.Length == 0) f.Add(new("Links", "Fehler", $"Bild „{id}“ hat keine Datei.", "image-empty"));
                else
                {
                    if (string.IsNullOrWhiteSpace(p?["alt"]?.ToString())) f.Add(new("Barrierefreiheit", "Fehler", $"Bild „{id}“ hat keinen Alternativtext.", "alt"));
                    if (!IsLocalFile(projectDir, src)) f.Add(new("Links", "Fehler", $"Bild „{id}“: Die Datei „{src}“ liegt nicht im Projekt.", "missing"));
                }
            }
            foreach (var field in new[] { "href" })
                if (p?[field]?.ToString() is { Length: > 1 } link && !link.StartsWith("http", StringComparison.OrdinalIgnoreCase) && !link.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                {
                    if (link.StartsWith('#')) f.Add(new("Links", "Fehler", $"Baustein „{id}“ springt zu „{link}“, dieses Ziel gibt es auf der Seite nicht.", "anchor"));
                    else if (!IsLocalFile(projectDir, link)) f.Add(new("Links", "Fehler", $"Baustein „{id}“ verweist auf „{link}“, das liegt nicht im Projekt.", "missing"));
                }
            foreach (var kv in p ?? [])
                if (kv.Value is JsonValue v && v.TryGetValue(out string? s) && (Lorem().IsMatch(s) || DefaultTexts.Contains(s)))
                    f.Add(new("SEO", "Hinweis", $"Baustein „{id}“ enthält noch Platzhaltertext.", "placeholder"));
        }
        return f;
    }

    /// <summary>Ein Pfad liegt im Projektordner und die Datei ist vorhanden (ohne Weg nach oben).</summary>
    public static bool IsLocalFile(string projectDir, string rel)
    {
        var clean = rel.Split('#', '?')[0].Replace('\\', '/');
        if (clean.Length == 0 || clean.StartsWith('/') || clean.Split('/').Contains("..")) return false;
        var full = Path.GetFullPath(Path.Combine(projectDir, clean));
        var root = Path.GetFullPath(projectDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return full.StartsWith(root, StringComparison.Ordinal) && File.Exists(full);
    }

    /// <summary>Für die Ausgabe: gleiche Befunde nur einmal, Fehler zuerst.</summary>
    public static IReadOnlyList<WebsiteFinding> Distinct(IEnumerable<WebsiteFinding> all) =>
        all.DistinctBy(f => f.Text).OrderBy(f => f.Level == "Fehler" ? 0 : f.Level == "Hinweis" ? 1 : 2).ToList();
}
