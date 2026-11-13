using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Z7: Medien je Projekt an einem Ort – Bilder, Videos, Schriften, Symbole, 3D-Modelle. Jede Datei hat Herkunft und Lizenz; durchsuchbar und in Bausteinen nutzbar.
/// Ablage in <c>medien/&lt;art&gt;/</c>, Verzeichnis in <c>medien.trendo.json</c>. Beim Export wird gemeldet, was ohne Lizenz genutzt wird; CC-BY erzeugt eine Namensnennung.
/// Schriften liegen lokal im Projekt und reisen mit; der Export lädt keine Schrift aus dem Netz.</summary>
public static partial class WebDesignAssets
{
    public const string FileName = "medien.trendo.json", Folder = "medien";
    public const long MaxBytes = 50L * 1024 * 1024;

    public sealed record Asset(string Datei, string Art, string Titel, string Stichworte, string Herkunft, string Lizenz, string Urheber, string Quelle, string? Familie = null);

    /// <summary>Erlaubte Dateiarten je Art.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Types = new Dictionary<string, string[]>
    {
        ["bild"] = [".png", ".jpg", ".jpeg", ".webp", ".gif", ".avif"], ["video"] = [".mp4", ".webm"], ["schrift"] = [".woff2", ".woff", ".ttf", ".otf"],
        ["symbol"] = [".svg", ".png"], ["modell"] = [".glb", ".gltf"],
    };

    [GeneratedRegex(@"^[\p{L}0-9 -]{1,40}$")] private static partial Regex FamilyPattern();
    public static bool ValidFamily(string f) => FamilyPattern().IsMatch(f);

    static string File(string dir) => Path.Combine(dir, FileName);

    public static List<Asset> Load(string dir)
    {
        if (!System.IO.File.Exists(File(dir))) return [];
        try { return JsonSerializer.Deserialize<List<Asset>>(System.IO.File.ReadAllText(File(dir)))?.Where(a => a.Datei is { Length: > 0 } && Types.ContainsKey(a.Art)).ToList() ?? []; }
        catch (Exception e) when (e is JsonException or IOException or NotSupportedException) { return []; }
    }

    static void Save(string dir, List<Asset> list)
    {
        System.IO.File.WriteAllText(File(dir) + ".neu", JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
        System.IO.File.Move(File(dir) + ".neu", File(dir), true);
    }

    /// <summary>Datei ins Projekt übernehmen (Kopie) und mit Herkunft und Lizenz verzeichnen. Liefert den Eintrag oder einen Fehlertext.</summary>
    public static (Asset? Asset, string? Error) Import(string dir, string source, string art, string titel, string herkunft, string lizenz, string urheber = "", string quelle = "", string stichworte = "", string? familie = null)
    {
        if (!Types.TryGetValue(art, out var exts)) return (null, "Unbekannte Medienart.");
        if (!System.IO.File.Exists(source)) return (null, "Die Datei gibt es nicht.");
        var ext = Path.GetExtension(source).ToLowerInvariant();
        if (!exts.Contains(ext)) return (null, $"Für „{art}“ sind nur {string.Join(", ", exts)} erlaubt.");
        if (new FileInfo(source).Length > MaxBytes) return (null, "Die Datei ist größer als 50 MB.");
        if (art == "schrift" && (familie is null || !ValidFamily(familie))) return (null, "Eine Schrift braucht einen Familiennamen aus Buchstaben, Ziffern, Leerzeichen oder Strich.");
        var name = SkillFiles.Slug(Path.GetFileNameWithoutExtension(source)) + ext;
        var rel = $"{Folder}/{art}/{name}";
        var list = Load(dir);
        for (var n = 2; list.Any(a => a.Datei == rel); n++) rel = $"{Folder}/{art}/{SkillFiles.Slug(Path.GetFileNameWithoutExtension(source))}-{n}{ext}";
        var target = Path.Combine(dir, rel.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        System.IO.File.Copy(source, target, false);
        var asset = new Asset(rel, art, Clean(titel, 120) is { Length: > 0 } t ? t : Path.GetFileNameWithoutExtension(source), Clean(stichworte, 300), Clean(herkunft, 200), Clean(lizenz, 80), Clean(urheber, 200), Clean(quelle, 500), familie);
        list.Add(asset); Save(dir, list);
        return (asset, null);
    }

    static string Clean(string s, int max) { var t = string.Concat((s ?? "").Where(c => !char.IsControl(c))).Trim(); return t[..Math.Min(max, t.Length)]; }

    /// <summary>Herkunft und Lizenz nachtragen oder ändern.</summary>
    public static string? Update(string dir, string datei, string herkunft, string lizenz, string urheber, string quelle)
    {
        var list = Load(dir); var i = list.FindIndex(a => a.Datei == datei);
        if (i < 0) return "Diese Datei steht nicht im Verzeichnis.";
        list[i] = list[i] with { Herkunft = Clean(herkunft, 200), Lizenz = Clean(lizenz, 80), Urheber = Clean(urheber, 200), Quelle = Clean(quelle, 500) };
        Save(dir, list); return null;
    }

    /// <summary>Suche in Titel, Stichworten, Datei, Lizenz und Herkunft; optional nur eine Art.</summary>
    public static List<Asset> Search(string dir, string? query, string? art = null) =>
        Load(dir).Where(a => (art is null || a.Art == art) && (string.IsNullOrWhiteSpace(query) ||
            $"{a.Titel} {a.Stichworte} {a.Datei} {a.Lizenz} {a.Herkunft} {a.Familie}".Contains(query.Trim(), StringComparison.OrdinalIgnoreCase))).ToList();

    public static bool NeedsAttribution(string lizenz) => Regex.IsMatch(lizenz, @"\bCC[- ]?BY\b", RegexOptions.IgnoreCase);

    /// <summary>Dateien, die die Seite nutzt (Bilder in allen Feldern src/bild, Schriften über den Gestaltungswert).</summary>
    public static HashSet<string> Used(JsonNode? doc, List<Asset> list)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        void Walk(JsonNode? n)
        {
            if (n is JsonArray a) foreach (var x in a) Walk(x);
            else if (n is JsonObject o) foreach (var kv in o)
            {
                if (kv.Key is "src" or "bild" && kv.Value is JsonValue v && v.TryGetValue(out string? s) && s.Length > 0) used.Add(s.Split('#', '?')[0].Replace('\\', '/'));
                else if (kv.Key != "medien") Walk(kv.Value);
            }
        }
        Walk(doc);
        if (FontFamilyOf(doc?["root"]?["props"]?["font"]?.ToString()) is { } fam) foreach (var a in list.Where(x => x.Art == "schrift" && x.Familie == fam)) used.Add(a.Datei);
        return used;
    }

    /// <summary>Eigene Schrift im Gestaltungswert: „eigene:Familie“.</summary>
    public static string? FontFamilyOf(string? font) => font is not null && font.StartsWith("eigene:", StringComparison.Ordinal) && ValidFamily(font[7..]) ? font[7..] : null;

    /// <summary>Prüfung vor dem Export: genutzte Medien ohne Lizenz sind Fehler, CC-BY ohne Urheber ebenfalls; nicht verzeichnete Dateien ein Hinweis.</summary>
    public static IReadOnlyList<WebsiteFinding> Check(string dir, JsonNode? doc)
    {
        var list = Load(dir); var f = new List<WebsiteFinding>();
        foreach (var file in Used(doc, list))
        {
            var a = list.FirstOrDefault(x => x.Datei == file);
            if (a is null) { if (file.StartsWith(Folder + "/", StringComparison.Ordinal) || WebDesignCheck.IsLocalFile(dir, file)) f.Add(new("Lizenzen", "Hinweis", $"„{file}“ steht nicht im Medienverzeichnis: Herkunft und Lizenz sind unbekannt.", "license-unknown")); continue; }
            if (a.Lizenz.Length == 0) f.Add(new("Lizenzen", "Fehler", $"„{a.Titel}“ ({a.Datei}) hat keinen Lizenzeintrag.", "license"));
            else if (NeedsAttribution(a.Lizenz) && a.Urheber.Length == 0) f.Add(new("Lizenzen", "Fehler", $"„{a.Titel}“ steht unter {a.Lizenz}, der Urheber für die Namensnennung fehlt.", "license-author"));
        }
        return f;
    }

    /// <summary>Namensnennung für genutzte Medien unter CC-BY (leer, wenn keine nötig ist).</summary>
    public static IReadOnlyList<Asset> AttributionFor(string dir, JsonNode? doc)
    {
        var list = Load(dir); var used = Used(doc, list);
        return list.Where(a => used.Contains(a.Datei) && NeedsAttribution(a.Lizenz)).ToList();
    }

    public static string AttributionHtml(IReadOnlyList<Asset> items)
    {
        static string E(string s) => WebUtility.HtmlEncode(s);
        var sb = new StringBuilder("<!doctype html>\n<html lang=\"de\">\n<head>\n<meta charset=\"UTF-8\" />\n<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />\n<title>Namensnennung</title>\n</head>\n<body>\n<main>\n<h1>Namensnennung</h1>\n<ul>\n");
        foreach (var a in items)
        {
            var src = a.Quelle.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? $" – <a href=\"{E(a.Quelle)}\">Quelle</a>" : "";
            sb.Append($"<li>„{E(a.Titel)}“ von {E(a.Urheber)}, Lizenz {E(a.Lizenz)}{(a.Herkunft.Length > 0 ? ", über " + E(a.Herkunft) : "")}{src}</li>\n");
        }
        return sb.Append("</ul>\n</main>\n</body>\n</html>\n").ToString();
    }

    /// <summary>@font-face für eine eigene Schrift aus dem Projekt; Pfad relativ, nie aus dem Netz.</summary>
    public static string FontFaceCss(string dir, JsonNode? doc)
    {
        if (FontFamilyOf(doc?["root"]?["props"]?["font"]?.ToString()) is not { } fam) return "";
        var sb = new StringBuilder();
        foreach (var a in Load(dir).Where(x => x.Art == "schrift" && x.Familie == fam && WebDesign.IsSafeFontPath(x.Datei)))
        {
            var ext = Path.GetExtension(a.Datei).ToLowerInvariant();
            string kind = ext == ".woff2" ? "woff2" : ext == ".woff" ? "woff" : ext == ".otf" ? "opentype" : "truetype";
            sb.Append("@font-face{font-family:\"" + fam + "\";src:url(\"" + a.Datei + "\") format(\"" + kind + "\");font-display:swap}");
        }
        return sb.ToString();
    }

    /// <summary>Liste für den Editor (Auswahl in Bild-Feldern), ohne Lizenzangaben.</summary>
    public static JsonArray EditorList(string dir) => new(Load(dir).Where(a => a.Art is "bild" or "symbol").Select(a => (JsonNode)new JsonObject { ["datei"] = a.Datei, ["titel"] = a.Titel, ["art"] = a.Art }).ToArray());

    /// <summary>Medienliste für den Editor in Seitendaten einsetzen.</summary>
    public static string Inject(string json, string dir)
    {
        if (!System.IO.File.Exists(File(dir))) return json;
        try
        {
            if (JsonNode.Parse(json) is not JsonObject doc || doc["root"] is not JsonObject root) return json;
            if (root["props"] is not JsonObject props) root["props"] = props = [];
            props["medien"] = EditorList(dir);
            return doc.ToJsonString();
        }
        catch (Exception e) when (e is JsonException or ArgumentException) { return json; }
    }
}
