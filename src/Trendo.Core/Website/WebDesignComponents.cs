using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>W5: wiederverwendbare Komponenten. Eine Bausteingruppe wird zur Komponente; das Original gilt für das ganze Projekt (Seite und Varianten).
/// Jede Verwendung (Baustein „Komponente“) zeigt das Original, kann aber einzelne Felder überschreiben. Komponenten lassen sich als eigene Vorlagen speichern und in andere Projekte einfügen.
/// Gespeichert in <c>komponenten.trendo.json</c>; beim Laden und im Export setzt Trendo sie in <c>root.props.komponenten</c> ein.</summary>
public static partial class WebDesignComponents
{
    public const string FileName = "komponenten.trendo.json", TemplatesDir = "vorlagen";
    public const int MaxComponents = 100, MaxBlocks = 60;
    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")] private static partial Regex IdPattern();
    [GeneratedRegex(@"^[\p{L}0-9 ._-]{1,60}$")] private static partial Regex NamePattern();

    public sealed record Component(string Id, string Name, JsonArray Blocks);

    static string File(string dir) => Path.Combine(dir, FileName);

    public static bool ValidName(string name) => NamePattern().IsMatch(name);

    /// <summary>Komponenten aus Seitendaten oder einer Datei lesen: nur gültige Einträge, Bausteine mit Typ.</summary>
    public static List<Component> Read(JsonNode? node)
    {
        var list = new List<Component>();
        if (node is not JsonArray a) return list;
        foreach (var o in a.OfType<JsonObject>())
        {
            var id = o["id"]?.ToString(); var name = o["name"]?.ToString();
            if (id is null || !IdPattern().IsMatch(id) || name is null || !NamePattern().IsMatch(name) || list.Any(c => c.Id == id)) continue;
            if (o["bausteine"] is not JsonArray blocks || blocks.Count == 0 || blocks.Count > MaxBlocks || !blocks.All(b => b is JsonObject bo && bo["type"] is JsonValue && bo["type"]!.ToString() != "Komponente")) continue;
            list.Add(new(id, name, (JsonArray)blocks.DeepClone()));
            if (list.Count >= MaxComponents) break;
        }
        return list;
    }

    public static JsonArray ToJson(IEnumerable<Component> list) => new(list.Select(c => (JsonNode)new JsonObject { ["id"] = c.Id, ["name"] = c.Name, ["bausteine"] = c.Blocks.DeepClone() }).ToArray());

    public static List<Component> Load(string dir)
    {
        if (!System.IO.File.Exists(File(dir))) return [];
        try { return Read(JsonNode.Parse(System.IO.File.ReadAllText(File(dir)))?["komponenten"]); }
        catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException or IOException) { return []; }
    }

    public static void Save(string dir, IEnumerable<Component> list)
    {
        Directory.CreateDirectory(dir);
        System.IO.File.WriteAllText(File(dir) + ".neu", new JsonObject { ["komponenten"] = ToJson(list) }.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        System.IO.File.Move(File(dir) + ".neu", File(dir), true);
    }

    /// <summary>Komponenten des Projekts in Seitendaten einsetzen (für Editor, Export und Prüfung).</summary>
    public static string Inject(string json, string dir)
    {
        if (!System.IO.File.Exists(File(dir))) return json;
        try
        {
            if (JsonNode.Parse(json) is not JsonObject doc || doc["root"] is not JsonObject root) return json;
            if (root["props"] is not JsonObject props) root["props"] = props = [];
            props["komponenten"] = ToJson(Load(dir));
            return doc.ToJsonString();
        }
        catch (Exception e) when (e is JsonException or ArgumentException) { return json; }
    }

    /// <summary>Nach dem Speichern: Komponenten aus der Seite gelten ab jetzt für das ganze Projekt. Seiten ohne Komponenten-Angabe ändern nichts.</summary>
    public static bool Lift(string dir, string json)
    {
        JsonNode? node;
        try { node = JsonNode.Parse(json)?["root"]?["props"]?["komponenten"]; }
        catch (Exception e) when (e is JsonException or ArgumentException) { return false; }
        if (node is not JsonArray) return false;
        var page = Read(node); var current = Load(dir);
        if (System.IO.File.Exists(File(dir)) && JsonNode.DeepEquals(ToJson(page), ToJson(current))) return false;
        Save(dir, page);
        return true;
    }

    /// <summary>Verwendungen durch die Bausteine ihres Originals ersetzen, überschriebene Felder eingesetzt. Ids werden je Verwendung eindeutig: „verwendung--baustein“.</summary>
    public static JsonObject Expand(JsonObject doc)
    {
        var copy = (JsonObject)doc.DeepClone();
        var defs = Read(copy["root"]?["props"]?["komponenten"]).ToDictionary(c => c.Id);
        void Walk(JsonArray list)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] is not JsonObject b) continue;
                var p = b["props"] as JsonObject;
                if (b["type"]?.ToString() == "Komponente")
                {
                    list.RemoveAt(i);
                    if (p?["komponente"]?.ToString() is { } k && defs.TryGetValue(k, out var def))
                    {
                        var usage = p["id"]?.ToString() ?? "k";
                        var over = p["ueberschreibungen"] as JsonObject;
                        foreach (var inner in Instance(def, usage, over)) list.Insert(i++, inner);
                    }
                    i--;
                    continue;
                }
                if (p is not null) foreach (var kv in p.ToList()) if (kv.Value is JsonArray a && a.All(x => x is JsonObject o && o["type"] is not null)) Walk(a);
            }
        }
        if (copy["content"] is JsonArray c) Walk(c);
        if (copy["zones"] is JsonObject z) foreach (var kv in z) if (kv.Value is JsonArray a) Walk(a);
        return copy;
    }

    /// <summary>Bausteine einer Verwendung: Original mit eingesetzten Überschreibungen (nur oberste Ebene der Komponente).</summary>
    public static IEnumerable<JsonObject> Instance(Component def, string usage, JsonObject? over)
    {
        foreach (var b in def.Blocks.OfType<JsonObject>())
        {
            var inner = (JsonObject)b.DeepClone();
            if (inner["props"] is not JsonObject ip) inner["props"] = ip = [];
            var innerId = ip["id"]?.ToString() ?? "b";
            if (over?[innerId] is JsonObject o) foreach (var kv in o) ip[kv.Key] = kv.Value?.DeepClone();
            ip["id"] = usage + "--" + innerId;
            yield return inner;
        }
    }

    // ---------- Vorlagen ----------

    static string TemplateFile(string templatesDir, string name) => Path.Combine(templatesDir, SkillFiles.Slug(name) + ".json");

    /// <summary>Komponente als eigene Vorlage speichern (unabhängig vom Projekt).</summary>
    public static string SaveTemplate(string templatesDir, Component c)
    {
        Directory.CreateDirectory(templatesDir);
        var f = TemplateFile(templatesDir, c.Name);
        System.IO.File.WriteAllText(f, new JsonObject { ["name"] = c.Name, ["bausteine"] = c.Blocks.DeepClone() }.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return f;
    }

    /// <summary>Bausteine einer Vorlage als Angaben für „komponente_definieren“: nur Typ, id und einfache Felder (Bereiche und Ansichts-Anpassungen bleiben leer).</summary>
    public static JsonArray TemplateBlocks(JsonArray blocks) => new(blocks.OfType<JsonObject>().Select(b =>
    {
        var props = new JsonObject();
        if (b["props"] is JsonObject p)
            foreach (var kv in p)
                if (kv.Key is not ("id" or "anpassungen") && kv.Value is not JsonArray && kv.Value is not JsonObject) props[kv.Key] = kv.Value?.DeepClone();
        var o = new JsonObject { ["typ"] = b["type"]?.ToString(), ["props"] = props };
        if (b["props"]?["id"]?.ToString() is { } id) o["id"] = id;
        return (JsonNode)o;
    }).ToArray());

    public static List<(string Name, JsonArray Blocks)> Templates(string templatesDir)
    {
        var list = new List<(string, JsonArray)>();
        if (!Directory.Exists(templatesDir)) return list;
        foreach (var f in Directory.GetFiles(templatesDir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
            try
            {
                if (JsonNode.Parse(System.IO.File.ReadAllText(f)) is JsonObject o && o["name"]?.ToString() is { } n && NamePattern().IsMatch(n) && o["bausteine"] is JsonArray b && b.Count > 0)
                    list.Add((n, (JsonArray)b.DeepClone()));
            }
            catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException or IOException) { }
        return list;
    }
}
