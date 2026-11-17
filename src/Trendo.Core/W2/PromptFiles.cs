using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trendo.Core;

/// <summary>Eine Vorlage aus einer Datei, noch nicht in der Sammlung.</summary>
public sealed record PromptFileEntry(string Name, string Category, string Text);

/// <summary>T-0740 (CORE-12): KI-Anweisungen als Datei – Export als Markdown mit Kopfzeilen (eine Vorlage) oder JSON (eine oder mehrere),
/// Import aus .md, .txt und .json. Gleicher Name: der Aufrufer fragt nach („ersetzen“ oder „als Kopie“).</summary>
public sealed partial class PromptLibrary
{
    public const string FileFormat = "trendo-ki-anweisung", FileFormatMany = "trendo-ki-anweisungen";
    public const int FileFormatVersion = 1;
    /// <summary>Größte Datei, die eingelesen wird.</summary>
    public const long MaxImportBytes = 2L * 1024 * 1024;

    public enum ImportMode { Replace, Copy }

    static string OneLine(string s) => s.ReplaceLineEndings(" ").Trim();

    /// <summary>Eine Vorlage als Markdown mit Kopfzeilen („---“-Block): Name, Bereich, Version; darunter unverändert der Anweisungstext.</summary>
    public static string ToMarkdown(PromptTemplate t)
    {
        var sb = new StringBuilder();
        sb.Append("---\n")
          .Append("format: ").Append(FileFormat).Append('\n')
          .Append("name: ").Append(OneLine(t.Name)).Append('\n')
          .Append("bereich: ").Append(OneLine(t.Category)).Append('\n')
          .Append("version: ").Append(t.Version).Append('\n')
          .Append("---\n");
        sb.Append(t.Text.ReplaceLineEndings("\n"));
        if (!t.Text.EndsWith('\n')) sb.Append('\n');
        return sb.ToString();
    }

    /// <summary>Mehrere Vorlagen (oder eine) als JSON – mit aktuellem Text, ohne frühere Versionen.</summary>
    public static string ToJson(IEnumerable<PromptTemplate> items)
    {
        var arr = new JsonArray(items.Select(t => (JsonNode)new JsonObject { ["name"] = t.Name, ["bereich"] = t.Category, ["version"] = t.Version, ["text"] = t.Text }).ToArray());
        var o = new JsonObject { ["format"] = FileFormatMany, ["formatVersion"] = FileFormatVersion, ["vorlagen"] = arr };
        return o.ToJsonString(new JsonSerializerOptions { WriteIndented = true, NewLine = "\n", Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }

    /// <summary>Vorlagen aus einer Datei lesen. <paramref name="fileName"/> gibt die Art (.md, .txt, .json) und – ohne Kopfzeile – den Namen.</summary>
    public static IReadOnlyList<PromptFileEntry> ParseFile(string fileName, string content)
    {
        content = (content ?? "").TrimStart('﻿').ReplaceLineEndings("\n");
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var fallbackName = Path.GetFileNameWithoutExtension(fileName).Replace('_', ' ').Trim();
        if (fallbackName.Length == 0) fallbackName = "Importierte Vorlage";
        List<PromptFileEntry> list;
        if (ext == ".json" || (ext != ".md" && ext != ".txt" && content.TrimStart().StartsWith('{')))
            list = ParseJson(content);
        else
            list = [ParseMarkdown(content, fallbackName)];
        list = list.Where(e => e.Text.Trim().Length > 0).ToList();
        if (list.Count == 0) throw new InvalidDataException("In der Datei steht keine Anweisung.");
        return list;
    }

    static PromptFileEntry ParseMarkdown(string content, string fallbackName)
    {
        string name = fallbackName, cat = "Allgemein", body = content;
        if (content.StartsWith("---\n"))
        {
            var end = content.IndexOf("\n---\n", 3, StringComparison.Ordinal);
            var endAtEof = end < 0 && content.EndsWith("\n---") ? content.Length - 4 : -1;
            var stop = end >= 0 ? end : endAtEof;
            if (stop >= 0)
            {
                foreach (var line in content[4..stop].Split('\n'))
                {
                    var i = line.IndexOf(':'); if (i <= 0) continue;
                    var k = line[..i].Trim().ToLowerInvariant(); var v = line[(i + 1)..].Trim().Trim('"');
                    if (k is "name" or "titel" or "title" && v.Length > 0) name = v;
                    else if (k is "bereich" or "kategorie" or "category" && v.Length > 0) cat = v;
                    else if (k == "format" && v != FileFormat && v.Length > 0) throw new InvalidDataException("Diese Datei ist keine KI-Anweisung (Format „" + OneLine(v) + "“).");
                }
                body = end >= 0 ? content[(end + 5)..] : "";
            }
        }
        if (body.EndsWith('\n')) body = body[..^1];
        return new(Cut(name, 120), Cut(cat, 60), body);
    }

    static List<PromptFileEntry> ParseJson(string content)
    {
        JsonNode? n;
        try { n = JsonNode.Parse(content); } catch (JsonException) { throw new InvalidDataException("Die JSON-Datei ist beschädigt."); }
        IEnumerable<JsonNode?> items = n switch
        {
            JsonArray a => a,
            JsonObject o when o["vorlagen"] is JsonArray a => a,
            JsonObject o => [o],
            _ => throw new InvalidDataException("Diese Datei ist keine KI-Anweisung."),
        };
        if (n is JsonObject root && root["format"] is JsonValue fv && fv.TryGetValue<string>(out var f) && f != FileFormat && f != FileFormatMany)
            throw new InvalidDataException("Diese Datei ist keine KI-Anweisung (Format „" + OneLine(f) + "“).");
        var list = new List<PromptFileEntry>();
        foreach (var it in items)
        {
            if (it is not JsonObject o) continue;
            string S(params string[] keys) { foreach (var k in keys) if (o[k] is JsonValue v && v.TryGetValue<string>(out var s)) return s; return ""; }
            var text = S("text", "anweisung", "prompt");
            if (text.Trim().Length == 0) continue;
            var name = S("name", "titel", "title").Trim(); var cat = S("bereich", "kategorie", "category").Trim();
            list.Add(new(Cut(name.Length == 0 ? "Importierte Vorlage" : name, 120), Cut(cat.Length == 0 ? "Allgemein" : cat, 60), text.ReplaceLineEndings("\n")));
            if (list.Count >= 500) break;
        }
        return list;
    }

    static string Cut(string s, int max) { s = OneLine(s); return s.Length > max ? s[..max] : s; }

    /// <summary>Vorlage mit gleichem Namen (ohne Groß-/Kleinschreibung).</summary>
    public PromptTemplate? FindByName(string name) => _items.FirstOrDefault(i => string.Equals(i.Name.Trim(), name.Trim(), StringComparison.CurrentCultureIgnoreCase));

    /// <summary>Freier Name „Name (Kopie)“, „Name (Kopie 2)“ …</summary>
    public string CopyName(string name)
    {
        var n = name.Trim() + " (Kopie)"; var i = 2;
        while (FindByName(n) is not null) n = $"{name.Trim()} (Kopie {i++})";
        return n;
    }

    /// <summary>Übernimmt eine Vorlage aus einer Datei. Ohne gleichnamige Vorlage entsteht eine neue; sonst entscheidet <paramref name="mode"/>:
    /// „ersetzen“ legt eine neue Version der vorhandenen an (die alte bleibt unter „Frühere Versionen“), „als Kopie“ legt eine neue Vorlage mit freiem Namen an.
    /// Ohne <paramref name="mode"/> bei gleichem Namen: Fehler – erst fragen.</summary>
    public PromptTemplate Import(PromptFileEntry e, ImportMode? mode = null)
    {
        var existing = FindByName(e.Name);
        PromptTemplate t;
        if (existing is null) t = Save(null, e.Name, e.Category, e.Text, "importiert");
        else if (mode == ImportMode.Replace) t = Save(existing.Id, existing.Name, e.Category, e.Text, "aus Datei ersetzt");
        else if (mode == ImportMode.Copy) t = Save(null, CopyName(e.Name), e.Category, e.Text, "importiert (Kopie)");
        else throw new InvalidOperationException($"Es gibt schon eine Vorlage „{existing.Name}“.");
        _audit.Append(Principal.LocalOwner.Name, "prompt.import", $"{t.Id} v{t.Version}");
        return t;
    }
}
