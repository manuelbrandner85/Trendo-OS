using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trendo.Core;

/// <summary>Z5: mehrsprachige Websites. Texte einer Seite werden über feste Schlüssel (Baustein-id, Feld, Listenplatz) erfasst; eine Übersetzung gilt als fehlend,
/// solange die Seite in der Sprache fehlt oder ein Text noch dem Original gleicht. Die KI übersetzt nur als Vorschlag, übernommen wird erst auf Knopfdruck.</summary>
public static class WebDesignI18n
{
    public const string ProposalDir = ".trendo-vorschlaege";
    public const int MaxTexts = 400, MaxTextLength = 4000;

    public sealed record TextEntry(string Key, string Text);
    public sealed record Status(string Adresse, string Sprache, bool Fehlt, int Offen, int Gesamt);

    static string Str(JsonNode? n) => n is JsonValue v && v.TryGetValue(out string? s) ? s : "";

    /// <summary>Alle übersetzbaren Texte einer Seite (Seitentitel und -beschreibung, Textfelder aller Bausteine, auch in Bereichen und Listen).</summary>
    public static IReadOnlyList<TextEntry> Texts(JsonObject doc)
    {
        var list = new List<TextEntry>();
        if (doc["root"]?["props"] is JsonObject rp)
            foreach (var k in new[] { "title", "description" }) if (Str(rp[k]) is { Length: > 0 } t) list.Add(new("root/" + k, t));
        void Block(JsonObject b)
        {
            var type = Str(b["type"]); var id = Str(b["props"]?["id"]);
            if (b["props"] is not JsonObject p || id.Length == 0) return;
            foreach (var (feld, sub) in WebDesignActions.TextFields(type))
                if (sub is null) { if (Str(p[feld]) is { Length: > 0 } t && !t.Contains("{{")) list.Add(new($"{id}/{feld}", t)); }
                else if (p[feld] is JsonArray arr)
                    for (var i = 0; i < arr.Count; i++) if (Str(arr[i]?[sub]) is { Length: > 0 } t && !t.Contains("{{")) list.Add(new($"{id}/{feld}/{i}/{sub}", t));
            foreach (var slot in WebDesignActions.SlotFields(type)) if (p[slot] is JsonArray inner) foreach (var x in inner.OfType<JsonObject>()) Block(x);
        }
        if (doc["content"] is JsonArray c) foreach (var b in c.OfType<JsonObject>()) Block(b);
        if (doc["zones"] is JsonObject z) foreach (var (_, v) in z) if (v is JsonArray za) foreach (var b in za.OfType<JsonObject>()) Block(b);
        return list.Take(MaxTexts).ToList();
    }

    /// <summary>Übersetzte Texte in eine Kopie der Seite schreiben. Nur bekannte Schlüssel, nur Text, Länge begrenzt; alles andere bleibt, wie es war.</summary>
    public static (JsonObject Doc, int Applied) Apply(JsonObject doc, IReadOnlyDictionary<string, string> texts)
    {
        var copy = doc.DeepClone().AsObject();
        var known = Texts(copy).Select(t => t.Key).ToHashSet();
        var ids = new Dictionary<string, JsonObject>();
        void Index(JsonObject b)
        {
            if (b["props"] is JsonObject p && Str(p["id"]) is { Length: > 0 } id) ids.TryAdd(id, p);
            foreach (var slot in WebDesignActions.SlotFields(Str(b["type"]))) if (b["props"]?[slot] is JsonArray inner) foreach (var x in inner.OfType<JsonObject>()) Index(x);
        }
        if (copy["content"] is JsonArray c) foreach (var b in c.OfType<JsonObject>()) Index(b);
        if (copy["zones"] is JsonObject z) foreach (var (_, v) in z) if (v is JsonArray za) foreach (var b in za.OfType<JsonObject>()) Index(b);
        var applied = 0;
        foreach (var (key, raw) in texts)
        {
            if (!known.Contains(key) || raw.Length == 0) continue;
            var text = raw.Length > MaxTextLength ? raw[..MaxTextLength] : raw;
            var parts = key.Split('/');
            if (parts[0] == "root" && copy["root"]?["props"] is JsonObject rp) { rp[parts[1]] = text; applied++; }
            else if (ids.TryGetValue(parts[0], out var p))
            {
                if (parts.Length == 2) { p[parts[1]] = text; applied++; }
                else if (parts.Length == 4 && p[parts[1]] is JsonArray arr && int.TryParse(parts[2], out var i) && i >= 0 && i < arr.Count && arr[i] is JsonObject item) { item[parts[3]] = text; applied++; }
            }
        }
        return (copy, applied);
    }

    /// <summary>Stand der Übersetzungen je Seite und Sprache.</summary>
    public static IReadOnlyList<Status> Report(string dir)
    {
        var s = WebDesignSite.Load(dir); var res = new List<Status>();
        foreach (var lang in s.Sprachen.Where(l => l != s.Standardsprache))
            foreach (var page in WebDesignSite.Pages(dir))
            {
                var original = Doc(WebDesignSite.FileOf(dir, page, s.Standardsprache, s.Standardsprache));
                var texts = original is null ? [] : Texts(original);
                var f = WebDesignSite.FileOf(dir, page, lang, s.Standardsprache);
                if (!File.Exists(f)) { res.Add(new(page, lang, true, texts.Count, texts.Count)); continue; }
                var translated = Doc(f);
                var map = translated is null ? new Dictionary<string, string>() : Texts(translated).ToDictionary(t => t.Key, t => t.Text);
                var open = texts.Count(t => !map.TryGetValue(t.Key, out var v) || v == t.Text);
                res.Add(new(page, lang, false, open, texts.Count));
            }
        return res;
    }

    static JsonObject? Doc(string f)
    {
        try { return File.Exists(f) && WebDesign.Validate(File.ReadAllText(f)).Json is { } ok ? JsonNode.Parse(ok) as JsonObject : null; }
        catch (Exception e) when (e is JsonException or IOException) { return null; }
    }

    // ---------- KI-Vorschlag ----------

    static readonly Dictionary<string, string> LangNames = new() { ["de"] = "Deutsch", ["en"] = "Englisch", ["fr"] = "Französisch", ["it"] = "Italienisch", ["es"] = "Spanisch", ["nl"] = "Niederländisch", ["pl"] = "Polnisch", ["tr"] = "Türkisch", ["pt"] = "Portugiesisch", ["cs"] = "Tschechisch" };
    public static string LangName(string code) => LangNames.TryGetValue(code.Split('-')[0], out var n) ? n : code;

    /// <summary>Auftrag an die KI: nur die Texte, als JSON, Schlüssel unverändert zurück.</summary>
    public static string Prompt(IReadOnlyList<TextEntry> texts, string from, string to)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Übersetze die Texte einer Webseite von {LangName(from)} nach {LangName(to)}.");
        sb.AppendLine("Regeln: natürlich und kurz wie auf einer echten Website, Fachbegriffe und Eigennamen beibehalten, Zeilenumbrüche beibehalten, keine Erklärungen.");
        sb.AppendLine("Antworte nur mit einem JSON-Objekt: dieselben Schlüssel, als Wert die Übersetzung.");
        var o = new JsonObject(texts.Select(t => KeyValuePair.Create(t.Key, (JsonNode?)t.Text)));
        sb.AppendLine(o.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        return sb.ToString();
    }

    /// <summary>KI-Antwort lesen: das erste JSON-Objekt, nur Text-Werte zu bekannten Schlüsseln.</summary>
    public static (IReadOnlyDictionary<string, string>? Texts, string? Error) ParseAnswer(string? answer, IReadOnlyList<TextEntry> asked)
    {
        if (string.IsNullOrWhiteSpace(answer)) return (null, "Die KI hat nichts geliefert.");
        var start = answer.IndexOf('{'); var end = answer.LastIndexOf('}');
        if (start < 0 || end <= start) return (null, "Die KI hat kein JSON geliefert.");
        try
        {
            if (JsonNode.Parse(answer[start..(end + 1)]) is not JsonObject o) return (null, "Die KI hat kein JSON-Objekt geliefert.");
            var keys = asked.Select(t => t.Key).ToHashSet();
            var d = new Dictionary<string, string>();
            foreach (var (k, v) in o) if (keys.Contains(k) && Str(v) is { Length: > 0 } t) d[k] = t;
            return d.Count == 0 ? (null, "Die Antwort der KI passt zu keinem Text der Seite.") : (d, null);
        }
        catch (Exception e) when (e is JsonException or ArgumentException) { return (null, "Die Antwort der KI ist kein gültiges JSON."); }
    }

    static string ProposalFile(string dir, string adresse, string sprache) => Path.Combine(dir, ProposalDir, sprache, adresse + ".json");

    /// <summary>Vorschlag ablegen; die Seite in der Sprache bleibt unverändert.</summary>
    public static string? SaveProposal(string dir, string adresse, string sprache, IReadOnlyDictionary<string, string> texts)
    {
        var s = WebDesignSite.Load(dir);
        if (!s.Sprachen.Contains(sprache) || sprache == s.Standardsprache) return "Für diese Sprache gibt es keine Übersetzung.";
        if (!WebDesignSite.Pages(dir).Contains(adresse)) return "Diese Seite gibt es nicht.";
        var f = ProposalFile(dir, adresse, sprache);
        Directory.CreateDirectory(Path.GetDirectoryName(f)!);
        File.WriteAllText(f, new JsonObject(texts.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value))).ToJsonString());
        return null;
    }

    public static IReadOnlyDictionary<string, string>? Proposal(string dir, string adresse, string sprache)
    {
        if (WebDesignSite.CheckSlug(adresse) is not null && adresse != WebDesignSite.Index || !WebDesignSite.IsLanguage(sprache)) return null;
        var f = ProposalFile(dir, adresse, sprache);
        try { return File.Exists(f) && JsonNode.Parse(File.ReadAllText(f)) is JsonObject o ? o.Where(kv => Str(kv.Value).Length > 0).ToDictionary(kv => kv.Key, kv => Str(kv.Value)) : null; }
        catch (Exception e) when (e is JsonException or IOException or ArgumentException) { return null; }
    }

    /// <summary>Vorschlag als Übersetzung übernehmen: Grundlage ist die Seite in der Sprache (fehlt sie, entsteht sie als Kopie des Originals), die Texte kommen aus dem Vorschlag.
    /// Von Hand geänderte Bausteine bleiben, nur vorgeschlagene Texte werden ersetzt. Vorher entsteht ein Stand.</summary>
    public static string? AdoptProposal(string dir, string adresse, string sprache)
    {
        if (Proposal(dir, adresse, sprache) is not { } texts) return "Für diese Seite liegt kein Vorschlag vor.";
        if (WebDesignSite.Activate(dir, adresse, sprache) is { } err) return err;
        if (JsonNode.Parse(WebDesignStates.Read(dir)) is not JsonObject current) return "Die Seite lässt sich nicht lesen.";
        var (doc, applied) = Apply(current, texts);
        if (applied == 0) return "Der Vorschlag passt nicht mehr zur Seite (Texte wurden inzwischen geändert).";
        var (_, serr) = WebDesignStates.SaveContent(dir, $"Vor Übersetzung ({sprache})", WebDesignStates.Read(dir), true);
        if (serr is not null) return serr;
        var werr = WebDesignStates.Write(dir, doc.ToJsonString());
        if (werr is null) File.Delete(ProposalFile(dir, adresse, sprache));
        return werr;
    }

    public static void DiscardProposal(string dir, string adresse, string sprache)
    {
        if (Proposal(dir, adresse, sprache) is not null) File.Delete(ProposalFile(dir, adresse, sprache));
    }
}
