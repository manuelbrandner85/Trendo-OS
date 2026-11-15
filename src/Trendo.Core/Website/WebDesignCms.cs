using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>W2 im Studio „Webseite bauen“: Sammlungen mit Feldern (Text, langer Text, Bild, Datum, Verweis) und Einträgen, gespeichert in <c>cms.trendo.json</c>.
/// Eine Vorlage-Seite (root.props.vorlage = Sammlung) ergibt im Export eine Seite je Eintrag; Platzhalter <c>{{feld}}</c> in Textfeldern werden ersetzt.
/// Der Baustein „Sammlung“ zeigt Einträge als Liste. Z5: Werte je Sprache, fehlende Übersetzungen werden gemeldet.</summary>
public static partial class WebDesignCms
{
    public const string FileName = "cms.trendo.json";
    public const int MaxCollections = 20, MaxFields = 20, MaxEntries = 500, MaxValue = 20_000;
    public static readonly string[] Arten = ["text", "langtext", "bild", "datum", "verweis"];
    public static readonly Dictionary<string, string> ArtNamen = new() { ["text"] = "Text", ["langtext"] = "Langer Text", ["bild"] = "Bild", ["datum"] = "Datum", ["verweis"] = "Verweis" };

    public sealed record Feld(string Name, string Art, bool Pflicht, string? Ziel);
    public sealed record Eintrag(string Adresse, IReadOnlyDictionary<string, string> Werte, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Sprachen);
    public sealed record Sammlung(string Id, string Name, IReadOnlyList<Feld> Felder, IReadOnlyList<Eintrag> Eintraege)
    {
        /// <summary>Feld, das als Titel eines Eintrags dient: das erste Textfeld.</summary>
        public string? TitelFeld => Felder.FirstOrDefault(f => f.Art == "text")?.Name;
    }
    public sealed record Fund(string Text, string Ebene);

    [GeneratedRegex("^[a-z][a-z0-9_]{0,29}$")] private static partial Regex FieldName();
    [GeneratedRegex("^[0-9]{4}-[0-9]{2}-[0-9]{2}$")] private static partial Regex IsoDate();
    [GeneratedRegex(@"\{\{\s*([a-z][a-z0-9_]{0,29})(\.link)?\s*\}\}")] private static partial Regex Placeholder();

    static string Str(JsonNode? n) => n is JsonValue v && v.TryGetValue(out string? s) ? s : "";

    // ---------- Lesen und Schreiben ----------

    /// <summary>Alle Sammlungen; Ungültiges wird beim Lesen verworfen.</summary>
    public static IReadOnlyList<Sammlung> Load(string dir)
    {
        var f = Path.Combine(dir, FileName);
        try { if (File.Exists(f) && JsonNode.Parse(File.ReadAllText(f)) is JsonObject o) return Read(o); }
        catch (Exception e) when (e is JsonException or IOException or ArgumentException) { }
        return [];
    }

    public static IReadOnlyList<Sammlung> Read(JsonObject o)
    {
        var list = new List<Sammlung>();
        if (o["sammlungen"] is not JsonArray arr) return list;
        foreach (var c in arr.OfType<JsonObject>().Take(MaxCollections))
        {
            var id = Str(c["id"]);
            if (WebDesignSite.CheckSlug(id) is not null || list.Any(x => x.Id == id)) continue;
            var felder = new List<Feld>();
            if (c["felder"] is JsonArray fa)
                foreach (var fo in fa.OfType<JsonObject>().Take(MaxFields))
                {
                    var name = Str(fo["name"]); var art = Str(fo["art"]);
                    if (!FieldName().IsMatch(name) || !Arten.Contains(art) || felder.Any(x => x.Name == name)) continue;
                    var ziel = art == "verweis" && WebDesignSite.CheckSlug(Str(fo["ziel"])) is null ? Str(fo["ziel"]) : null;
                    if (art == "verweis" && ziel is null) continue;
                    felder.Add(new(name, art, fo["pflicht"] is JsonValue pv && pv.TryGetValue(out bool p) && p, ziel));
                }
            var eintraege = new List<Eintrag>();
            if (c["eintraege"] is JsonArray ea)
                foreach (var eo in ea.OfType<JsonObject>().Take(MaxEntries))
                {
                    var adr = Str(eo["adresse"]);
                    if (WebDesignSite.CheckSlug(adr) is not null || eintraege.Any(x => x.Adresse == adr)) continue;
                    eintraege.Add(new(adr, Values(eo["werte"], felder), Langs(eo["sprachen"], felder)));
                }
            list.Add(new(id, Str(c["name"]) is { Length: > 0 } n ? n[..Math.Min(80, n.Length)] : id, felder, eintraege));
        }
        return list;
    }

    static Dictionary<string, string> Values(JsonNode? n, IReadOnlyList<Feld> felder)
    {
        var d = new Dictionary<string, string>();
        if (n is JsonObject o) foreach (var f in felder) if (Str(o[f.Name]) is { Length: > 0 } v && Clean(f, v) is { Length: > 0 } c) d[f.Name] = c; // Ungültiges fällt weg
        return d;
    }

    static Dictionary<string, IReadOnlyDictionary<string, string>> Langs(JsonNode? n, IReadOnlyList<Feld> felder)
    {
        var d = new Dictionary<string, IReadOnlyDictionary<string, string>>();
        if (n is JsonObject o) foreach (var (k, v) in o) if (WebDesignSite.IsLanguage(k)) d[k] = Values(v, felder.Where(f => f.Art is "text" or "langtext").ToList());
        return d;
    }

    /// <summary>Wert passend zur Feldart: Bilder nur als Bilddatei im Projekt, Datum nur als JJJJ-MM-TT, Länge begrenzt.</summary>
    static string Clean(Feld f, string v)
    {
        v = v.Length > MaxValue ? v[..MaxValue] : v;
        return f.Art switch
        {
            "bild" => WebDesign.IsSafeImagePath(v) ? v : "",
            "datum" => IsoDate().IsMatch(v) && DateOnly.TryParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ? v : "",
            "verweis" => WebDesignSite.CheckSlug(v) is null ? v : "",
            "text" => v.Replace('\n', ' ').Replace('\r', ' '),
            _ => v,
        };
    }

    public static JsonObject ToJson(IReadOnlyList<Sammlung> list) => new()
    {
        ["sammlungen"] = new JsonArray(list.Select(c => (JsonNode)new JsonObject
        {
            ["id"] = c.Id, ["name"] = c.Name,
            ["felder"] = new JsonArray(c.Felder.Select(f => (JsonNode)new JsonObject { ["name"] = f.Name, ["art"] = f.Art, ["pflicht"] = f.Pflicht, ["ziel"] = f.Ziel }).ToArray()),
            ["eintraege"] = new JsonArray(c.Eintraege.Select(e => (JsonNode)new JsonObject
            {
                ["adresse"] = e.Adresse,
                ["werte"] = new JsonObject(e.Werte.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value))),
                ["sprachen"] = new JsonObject(e.Sprachen.Select(l => KeyValuePair.Create(l.Key, (JsonNode?)new JsonObject(l.Value.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value)))))),
            }).ToArray()),
        }).ToArray()),
    };

    static void Save(string dir, IReadOnlyList<Sammlung> list)
    {
        var f = Path.Combine(dir, FileName);
        File.WriteAllText(f + ".neu", ToJson(list).ToJsonString(new JsonSerializerOptions { WriteIndented = true })); SafeFile.Move(f + ".neu", f);
    }

    // ---------- Bearbeiten ----------

    /// <summary>Felder aus einer kurzen Beschreibung: „titel:text*, bild:bild, datum:datum, autor:verweis>autoren“ (* = Pflicht).</summary>
    public static (IReadOnlyList<Feld>? Felder, string? Error) ParseFields(string spec)
    {
        var list = new List<Feld>();
        foreach (var raw in spec.Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pflicht = raw.EndsWith('*'); var part = pflicht ? raw[..^1].Trim() : raw;
            var bits = part.Split(':', 2, StringSplitOptions.TrimEntries);
            if (bits.Length != 2) return (null, $"„{raw}“: bitte als name:art angeben, z. B. titel:text.");
            var name = bits[0].ToLowerInvariant(); var art = bits[1]; string? ziel = null;
            if (art.StartsWith("verweis", StringComparison.Ordinal)) { var z = art.Split('>', 2, StringSplitOptions.TrimEntries); art = "verweis"; ziel = z.Length == 2 ? z[1] : null; }
            if (!FieldName().IsMatch(name)) return (null, $"Feldname „{bits[0]}“: Kleinbuchstaben, Ziffern und _, mit Buchstabe am Anfang.");
            if (!Arten.Contains(art)) return (null, $"Feldart „{bits[1]}“ gibt es nicht. Möglich: {string.Join(", ", Arten)}.");
            if (art == "verweis" && WebDesignSite.CheckSlug(ziel) is not null) return (null, $"Verweis „{name}“ braucht eine Ziel-Sammlung, z. B. {name}:verweis>autoren.");
            if (list.Any(f => f.Name == name)) return (null, $"Feld „{name}“ steht doppelt.");
            list.Add(new(name, art, pflicht, ziel));
        }
        if (list.Count == 0) return (null, "Bitte mindestens ein Feld angeben, z. B. titel:text*.");
        if (list.Count > MaxFields) return (null, $"Höchstens {MaxFields} Felder.");
        if (!list.Any(f => f.Art == "text")) return (null, "Mindestens ein Textfeld (für den Titel) wird gebraucht.");
        return (list, null);
    }

    public static string? CreateCollection(string dir, string id, string name, string fieldSpec)
    {
        if (WebDesignSite.CheckSlug(id) is { } err) return err;
        var list = Load(dir).ToList();
        if (list.Count >= MaxCollections) return $"Höchstens {MaxCollections} Sammlungen.";
        if (list.Any(c => c.Id == id)) return $"Die Sammlung „{id}“ gibt es schon.";
        if (WebDesignSite.Pages(dir).Contains(id)) return $"„{id}“ ist schon die Adresse einer Seite.";
        var (felder, ferr) = ParseFields(fieldSpec); if (felder is null) return ferr;
        foreach (var f in felder.Where(f => f.Art == "verweis" && f.Ziel != id && list.All(c => c.Id != f.Ziel))) return $"Die Ziel-Sammlung „{f.Ziel}“ gibt es nicht.";
        list.Add(new(id, name.Trim().Length > 0 ? name.Trim() : id, felder, []));
        Save(dir, list); return null;
    }

    public static string? DeleteCollection(string dir, string id)
    {
        var list = Load(dir).ToList();
        if (list.All(c => c.Id != id)) return "Diese Sammlung gibt es nicht.";
        if (list.FirstOrDefault(c => c.Id != id && c.Felder.Any(f => f.Ziel == id)) is { } other) return $"„{other.Name}“ verweist noch auf diese Sammlung.";
        Save(dir, list.Where(c => c.Id != id).ToList()); return null;
    }

    /// <summary>Eintrag anlegen oder ändern. Werte je Feld; mit <paramref name="sprache"/> nur die Übersetzung der Textfelder.</summary>
    public static string? SetEntry(string dir, string collection, string adresse, IReadOnlyDictionary<string, string> werte, string? sprache = null)
    {
        if (WebDesignSite.CheckSlug(adresse) is { } err) return err;
        var list = Load(dir).ToList();
        var i = list.FindIndex(c => c.Id == collection); if (i < 0) return "Diese Sammlung gibt es nicht.";
        var c = list[i];
        var site = WebDesignSite.Load(dir);
        var entries = c.Eintraege.ToList();
        var j = entries.FindIndex(e => e.Adresse == adresse);
        if (j < 0 && entries.Count >= MaxEntries) return $"Höchstens {MaxEntries} Einträge je Sammlung.";
        if (sprache is not null && sprache != site.Standardsprache)
        {
            if (!site.Sprachen.Contains(sprache)) return "Diese Sprache gibt es im Projekt nicht.";
            if (j < 0) return "Erst den Eintrag in der Standardsprache anlegen.";
            var langs = entries[j].Sprachen.ToDictionary(k => k.Key, k => k.Value);
            langs[sprache] = Values(new JsonObject(werte.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value))), c.Felder.Where(f => f.Art is "text" or "langtext").ToList());
            entries[j] = entries[j] with { Sprachen = langs };
        }
        else
        {
            var vals = Values(new JsonObject(werte.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value))), c.Felder);
            foreach (var f in c.Felder.Where(f => werte.ContainsKey(f.Name) && werte[f.Name].Length > 0 && !vals.ContainsKey(f.Name)))
                return f.Art switch { "bild" => $"„{f.Name}“: bitte eine Bilddatei aus dem Projekt.", "datum" => $"„{f.Name}“: Datum bitte als JJJJ-MM-TT.", _ => $"„{f.Name}“: ungültiger Wert." };
            var e = new Eintrag(adresse, vals, j >= 0 ? entries[j].Sprachen : new Dictionary<string, IReadOnlyDictionary<string, string>>());
            if (j >= 0) entries[j] = e; else entries.Add(e);
        }
        list[i] = c with { Eintraege = entries };
        Save(dir, list); return null;
    }

    public static string? DeleteEntry(string dir, string collection, string adresse)
    {
        var list = Load(dir).ToList();
        var i = list.FindIndex(c => c.Id == collection); if (i < 0) return "Diese Sammlung gibt es nicht.";
        list[i] = list[i] with { Eintraege = list[i].Eintraege.Where(e => e.Adresse != adresse).ToList() };
        Save(dir, list); return null;
    }

    // ---------- Prüfen ----------

    /// <summary>W2: leere Pflichtfelder und Verweise ins Leere; Z5: fehlende Übersetzungen der Textfelder.</summary>
    public static IReadOnlyList<Fund> Check(string dir)
    {
        var list = Load(dir); var site = WebDesignSite.Load(dir); var res = new List<Fund>();
        foreach (var c in list)
        {
            foreach (var e in c.Eintraege)
            {
                foreach (var f in c.Felder.Where(f => f.Pflicht && !e.Werte.ContainsKey(f.Name))) res.Add(new($"{c.Name} › {e.Adresse}: Pflichtfeld „{f.Name}“ ist leer.", "Fehler"));
                foreach (var f in c.Felder.Where(f => f.Art == "verweis" && e.Werte.TryGetValue(f.Name, out var v) && !(list.FirstOrDefault(x => x.Id == f.Ziel)?.Eintraege.Any(x => x.Adresse == v) ?? false)))
                    res.Add(new($"{c.Name} › {e.Adresse}: „{f.Name}“ verweist auf einen Eintrag, den es nicht gibt.", "Fehler"));
                foreach (var lang in site.Sprachen.Where(l => l != site.Standardsprache))
                {
                    var texts = c.Felder.Where(f => f.Art is "text" or "langtext" && e.Werte.ContainsKey(f.Name)).ToList();
                    var missing = texts.Where(f => !(e.Sprachen.TryGetValue(lang, out var t) && t.ContainsKey(f.Name))).Select(f => f.Name).ToList();
                    if (missing.Count > 0) res.Add(new($"{c.Name} › {e.Adresse}: Übersetzung ({lang}) fehlt für {string.Join(", ", missing)}.", "Hinweis"));
                }
            }
            if (c.Eintraege.Count > 0 && TemplateOf(dir, c.Id) is null) res.Add(new($"{c.Name}: keine Vorlage-Seite. Ohne Vorlage gibt es keine Seite je Eintrag, nur die Liste.", "Hinweis"));
        }
        return res;
    }

    /// <summary>Vorlage-Seite einer Sammlung (Adresse) oder null.</summary>
    public static string? TemplateOf(string dir, string collection)
    {
        var s = WebDesignSite.Load(dir);
        foreach (var p in WebDesignSite.Pages(dir))
        {
            var f = WebDesignSite.FileOf(dir, p, s.Standardsprache, s.Standardsprache);
            try { if (File.Exists(f) && JsonNode.Parse(File.ReadAllText(f))?["root"]?["props"]?["vorlage"]?.ToString() == collection) return p; }
            catch (Exception e) when (e is JsonException or IOException or ArgumentException) { }
        }
        return null;
    }

    // ---------- Inhalte einsetzen ----------

    /// <summary>Wert eines Felds in einer Sprache: Übersetzung, sonst der Wert der Standardsprache.</summary>
    public static string ValueOf(Eintrag e, Feld f, string? sprache) =>
        sprache is not null && e.Sprachen.TryGetValue(sprache, out var t) && t.TryGetValue(f.Name, out var tv) ? tv : e.Werte.GetValueOrDefault(f.Name, "");

    /// <summary>Datum in der Schreibweise der Sprache (de: 14.11.2026).</summary>
    public static string FormatDate(string iso, string sprache)
    {
        if (!DateOnly.TryParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return iso;
        try { return d.ToString("d", CultureInfo.GetCultureInfo(sprache)); } catch (CultureNotFoundException) { return iso; }
    }

    /// <summary>Z5: Eintrag gilt in einer Sprache als übersetzt, wenn mindestens ein Textfeld dort einen eigenen Wert hat.</summary>
    public static bool IsTranslated(Eintrag e, string sprache, string standard) => sprache == standard || e.Sprachen.TryGetValue(sprache, out var t) && t.Count > 0;

    /// <summary>Pfad der Seite eines Eintrags im Export, relativ zur Wurzel (ohne Sprachordner).</summary>
    public static string EntryPath(string collection, string adresse) => collection + "/" + adresse + ".html";

    /// <summary>Platzhalter <c>{{feld}}</c> und <c>{{verweis.link}}</c> in allen Texten einer Vorlage ersetzen. Bild-Felder ersetzen ihren ganzen Wert.</summary>
    public static JsonObject Fill(JsonObject template, Sammlung c, Eintrag e, IReadOnlyList<Sammlung> all, string sprache, string linkPrefix)
    {
        var doc = template.DeepClone().AsObject();
        var rpRef = doc["root"]?["props"] as JsonObject;
        if (rpRef is not null)
        {
            rpRef.Remove("vorlage");
            if (c.TitelFeld is { } tf && !Str(rpRef["title"]).Contains("{{")) rpRef["title"] = ValueOf(e, c.Felder.First(f => f.Name == tf), sprache) + (Str(rpRef["title"]) is { Length: > 0 } t ? " – " + t : "");
        }
        string Replace(string s) => Placeholder().Replace(s, m =>
        {
            var f = c.Felder.FirstOrDefault(x => x.Name == m.Groups[1].Value); if (f is null) return "";
            var v = ValueOf(e, f, sprache);
            if (f.Art == "datum") return FormatDate(v, sprache);
            if (f.Art == "verweis")
            {
                var target = all.FirstOrDefault(x => x.Id == f.Ziel)?.Eintraege.FirstOrDefault(x => x.Adresse == v);
                var tc = all.FirstOrDefault(x => x.Id == f.Ziel);
                if (target is null || tc is null) return "";
                return m.Groups[2].Success ? linkPrefix + EntryPath(tc.Id, target.Adresse) : (tc.TitelFeld is { } ttf ? ValueOf(target, tc.Felder.First(x => x.Name == ttf), sprache) : target.Adresse);
            }
            return v;
        });
        void Walk(JsonNode? n)
        {
            switch (n)
            {
                case JsonObject o:
                    foreach (var key in o.Select(kv => kv.Key).ToList())
                        if (o[key] is JsonValue v && v.TryGetValue(out string? s) && s.Contains("{{")) o[key] = Replace(s);
                        else Walk(o[key]);
                    break;
                case JsonArray a: foreach (var x in a) Walk(x); break;
            }
        }
        Walk(doc["content"]); Walk(doc["zones"]);
        if (rpRef is not null) foreach (var k in new[] { "title", "description", "vorschaubild" }) if (Str(rpRef[k]) is { } v && v.Contains("{{")) rpRef[k] = Replace(v); // Titel, Beschreibung und Vorschaubild je Eintrag
        return doc;
    }

    /// <summary>Baustein „Sammlung“: Einträge als fertige Liste in die Seite schreiben (Editor und Export zeigen dasselbe, ohne Zugriff auf das CMS).</summary>
    public static void Resolve(JsonObject doc, IReadOnlyList<Sammlung> all, string sprache, string linkPrefix, bool withPages) => Resolve(doc, all, sprache, linkPrefix, withPages ? all.Select(c => c.Id).ToHashSet() : []);

    /// <summary>Wie oben; Links auf Eintragsseiten nur für Sammlungen in <paramref name="withPages"/> (die eine Vorlage-Seite haben).</summary>
    public static void Resolve(JsonObject doc, IReadOnlyList<Sammlung> all, string sprache, string linkPrefix, IReadOnlySet<string> withPages)
    {
        void Walk(JsonNode? n)
        {
            switch (n)
            {
                case JsonObject o:
                    if (Str(o["type"]) == "Sammlung" && o["props"] is JsonObject p)
                    {
                        var c = all.FirstOrDefault(x => x.Id == Str(p["sammlung"]));
                        var max = (int)Math.Clamp(WebDesign.Number(p["anzahl"]) ?? 12, 1, 100);
                        var items = new JsonArray();
                        if (c is not null)
                        {
                            var entries = c.Eintraege.AsEnumerable();
                            var dateField = c.Felder.FirstOrDefault(f => f.Art == "datum");
                            entries = Str(p["sortierung"]) == "neueste" && dateField is not null ? entries.OrderByDescending(e => e.Werte.GetValueOrDefault(dateField.Name, "")) : entries.OrderBy(e => c.TitelFeld is { } tf ? ValueOf(e, c.Felder.First(f => f.Name == tf), sprache) : e.Adresse, StringComparer.CurrentCultureIgnoreCase);
                            foreach (var e in entries.Take(max))
                            {
                                var text = c.Felder.FirstOrDefault(f => f.Art == "langtext") is { } lt ? ValueOf(e, lt, sprache) : "";
                                items.Add(new JsonObject
                                {
                                    ["titel"] = c.TitelFeld is { } tf ? ValueOf(e, c.Felder.First(f => f.Name == tf), sprache) : e.Adresse,
                                    ["text"] = text.Length > 220 ? text[..220].TrimEnd() + " …" : text,
                                    ["bild"] = c.Felder.FirstOrDefault(f => f.Art == "bild") is { } bf ? e.Werte.GetValueOrDefault(bf.Name, "") : "",
                                    ["datum"] = dateField is not null && e.Werte.TryGetValue(dateField.Name, out var dv) ? FormatDate(dv, sprache) : "",
                                    ["href"] = withPages.Contains(c.Id) ? linkPrefix + EntryPath(c.Id, e.Adresse) : "",
                                });
                            }
                        }
                        p["eintraege"] = items;
                    }
                    foreach (var kv in o.ToList()) Walk(kv.Value);
                    break;
                case JsonArray a: foreach (var x in a) Walk(x); break;
            }
        }
        Walk(doc["content"]); Walk(doc["zones"]);
    }

    /// <summary>Aufgelöste Listen gehören nicht in die gespeicherte Seite (sie entstehen beim Lesen neu).</summary>
    public static void Strip(JsonNode? n)
    {
        switch (n)
        {
            case JsonObject o:
                if (Str(o["type"]) == "Sammlung" && o["props"] is JsonObject p) p.Remove("eintraege");
                foreach (var kv in o.ToList()) Strip(kv.Value);
                break;
            case JsonArray a: foreach (var x in a) Strip(x); break;
        }
    }
}
