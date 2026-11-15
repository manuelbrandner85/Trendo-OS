using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>K5: benannte Stände der Seite und Varianten. Ein Stand hält den Seiteninhalt Zeichen für Zeichen; vor jeder KI-Änderung und vor jedem Wiederherstellen entsteht automatisch einer.
/// Varianten sind eigene Fassungen neben der Seite: Ändern einer Variante lässt die Seite unberührt, bis eine Variante übernommen wird.</summary>
public static partial class WebDesignStates
{
    public const string StatesDir = ".trendo-staende", VariantsDir = ".trendo-varianten";
    /// <summary>So viele automatische Stände bleiben, ältere werden entfernt. Benannte Stände bleiben immer.</summary>
    public const int MaxAuto = 30;

    /// <remarks>Stufe 3: Stand und Variante merken sich ihre Seite (Adresse und Sprache); ohne Angabe gilt die Startseite (ältere Stände).</remarks>
    public sealed record Stand(string Id, string Name, bool Auto, DateTimeOffset Zeit, string? Variante, string Seite = WebDesignSite.Index, string? Sprache = null);
    public sealed record Variante(string Id, string Name, DateTimeOffset Zeit, string Seite = WebDesignSite.Index, string? Sprache = null);

    [GeneratedRegex("^[0-9]{19}-[0-9a-f]{8}$")] private static partial Regex IdPattern();
    static string NewId() => $"{DateTime.UtcNow.Ticks:D19}-{Guid.NewGuid():N}"[..28];
    static string StateFile(string dir, string id) => Path.Combine(dir, StatesDir, id + ".json");
    static string VariantFile(string dir, string id) => Path.Combine(dir, VariantsDir, id + ".json");

    static void WriteAtomic(string file, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file + ".neu", text); SafeFile.Move(file + ".neu", file);
    }

    static string PageRaw(string dir)
    {
        var f = WebDesignSite.ActiveFile(dir);
        return File.Exists(f) && WebDesign.Validate(File.ReadAllText(f)).Json is { } ok ? ok : WebDesign.Load(dir); // bereinigt: auch eine von außen veränderte Datei kommt ohne gefährliche Links in den Editor
    }

    /// <summary>Inhalt der Seite oder einer Variante.</summary>
    /// <remarks>W6, W5: Gestaltungswerte und Komponenten des Projekts gelten für die Seite und den Export. Eine Variante bringt ihre eigenen mit (beim Anlegen kopiert),
    /// damit sich auch eine andere Farbwelt ausprobieren lässt, ohne die Seite zu ändern (K5); erst beim Übernehmen gelten sie fürs Projekt.</remarks>
    public static string Read(string dir, string? variant = null) => WithLists(dir, WebDesignAssets.Inject(variant is null // Z7: Medienliste für die Auswahl im Editor
        ? WebDesignComponents.Inject(WebDesignTokens.Inject(PageRaw(dir), WebDesignTokens.Load(dir)), dir)
        : VariantContent(dir, variant) ?? WebDesign.Empty(), dir));

    /// <summary>W2: Listen aus dem CMS fertig in die Seite schreiben, damit der Editor sie zeigt. Beim Speichern fallen sie wieder weg.</summary>
    static string WithLists(string dir, string json)
    {
        var cms = WebDesignCms.Load(dir);
        if (cms.Count == 0 || JsonNode.Parse(json) is not JsonObject doc) return json;
        WebDesignCms.Resolve(doc, cms, WebDesignSite.Active(dir).Sprache, "", false);
        if (doc["root"]?["props"] is JsonObject rp) rp["sammlungen"] = new JsonArray(cms.Select(c => (JsonNode)new JsonObject { ["id"] = c.Id, ["name"] = c.Name }).ToArray()); // Auswahl im Editor
        return doc.ToJsonString();
    }

    /// <summary>Gestaltungswerte und Komponenten aus Seitendaten ins Projekt übernehmen.</summary>
    static void LiftProject(string dir, string json)
    {
        if (WebDesign.Validate(json).Json is not { } ok) return;
        WebDesignTokens.Lift(dir, ok); WebDesignComponents.Lift(dir, ok);
    }

    /// <summary>Schreibt die Seite oder eine Variante. Nur gültige Daten; bei der Seite bleibt die vorige Fassung als Sicherung.</summary>
    public static string? Write(string dir, string json, string? variant = null)
    {
        var err = variant is null ? WebDesign.Save(dir, json) : SaveVariant(dir, variant, json);
        if (err is null && variant is null) LiftProject(dir, json); // Werte der Seite gelten ab jetzt fürs Projekt; eine Variante ändert die Seite nicht
        return err;
    }

    /// <summary>Aktuellen Inhalt als Stand speichern.</summary>
    public static (Stand? Stand, string? Error) Save(string dir, string name, string? variant = null) => SaveContent(dir, name, Read(dir, variant), false, variant);

    /// <summary>Genau diesen Inhalt als Stand speichern (auch einen noch nicht gespeicherten Entwurf).</summary>
    public static (Stand? Stand, string? Error) SaveContent(string dir, string name, string content, bool auto = false, string? variant = null)
    {
        if (string.IsNullOrWhiteSpace(name)) return (null, "Bitte einen Namen für den Stand.");
        var (ok, err) = WebDesign.Validate(content); if (ok is null) return (null, err);
        var trimmed = name.Trim(); trimmed = trimmed[..Math.Min(120, trimmed.Length)];
        var page = variant is { } vid ? VariantPage(dir, vid) : WebDesignSite.Active(dir);
        var s = new Stand(NewId(), trimmed, auto, DateTimeOffset.Now, variant, page.Adresse, page.Sprache);
        WriteAtomic(StateFile(dir, s.Id), new JsonObject { ["name"] = s.Name, ["auto"] = auto, ["zeit"] = s.Zeit.ToString("O"), ["variante"] = variant, ["seite"] = content, ["adresse"] = page.Adresse, ["sprache"] = page.Sprache }.ToJsonString());
        if (auto) Prune(dir);
        return (s, null);
    }

    static Stand? ReadState(string file)
    {
        try
        {
            if (JsonNode.Parse(File.ReadAllText(file)) is not JsonObject o) return null;
            var p = PageOf(o);
            return new(Path.GetFileNameWithoutExtension(file), o["name"]?.ToString() ?? "", o["auto"]?.GetValue<bool>() == true,
                DateTimeOffset.TryParse(o["zeit"]?.ToString(), out var t) ? t : DateTimeOffset.MinValue, o["variante"]?.ToString(), p.Adresse, p.Sprache);
        }
        catch (Exception e) when (e is JsonException or IOException or InvalidOperationException or ArgumentException) { return null; }
    }

    /// <summary>Alle Stände, neueste zuerst.</summary>
    public static IReadOnlyList<Stand> List(string dir)
    {
        var d = Path.Combine(dir, StatesDir);
        return !Directory.Exists(d) ? [] : Directory.GetFiles(d, "*.json").OrderByDescending(f => Path.GetFileName(f), StringComparer.Ordinal).Select(ReadState).OfType<Stand>().ToList();
    }

    static void Prune(string dir)
    {
        foreach (var s in List(dir).Where(x => x.Auto).Skip(MaxAuto)) File.Delete(StateFile(dir, s.Id));
    }

    /// <summary>Inhalt eines Stands, unverändert.</summary>
    public static string? Content(string dir, string id)
    {
        if (!IdPattern().IsMatch(id) || !File.Exists(StateFile(dir, id))) return null;
        try { return JsonNode.Parse(File.ReadAllText(StateFile(dir, id)))?["seite"]?.ToString(); }
        catch (Exception e) when (e is JsonException or InvalidOperationException or ArgumentException) { return null; }
    }

    /// <summary>Stand wiederherstellen: Vorher wird der aktuelle Inhalt automatisch gesichert, danach steht exakt der alte Inhalt in der Datei.</summary>
    public static string? Restore(string dir, string id)
    {
        if (Content(dir, id) is not { } text || ReadState(StateFile(dir, id)) is not { } s) return "Diesen Stand gibt es nicht mehr.";
        var (ok, err) = WebDesign.Validate(text); if (ok is null) return err;
        if (s.Variante is { } v && VariantContent(dir, v) is null) return "Die Variante dieses Stands gibt es nicht mehr.";
        if (s.Variante is null && WebDesignSite.Activate(dir, s.Seite, s.Sprache) is { } perr) return "Die Seite dieses Stands gibt es nicht mehr: " + perr; // Stufe 3: zurück auf die Seite des Stands
        var (auto, aerr) = SaveContent(dir, "Vor Wiederherstellen", Read(dir, s.Variante), true, s.Variante);
        if (auto is null) return aerr;
        if (s.Variante is { } vv) WriteAtomic(VariantFile(dir, vv), VariantFileText(dir, vv, text));
        else
        {
            var f = WebDesignSite.ActiveFile(dir);
            if (File.Exists(f)) File.Copy(f, f + ".bak", true);
            WriteAtomic(f, text);
            LiftProject(dir, text); // auch Gestaltungswerte und Komponenten kehren zum alten Stand zurück; die neuen liegen im automatischen Stand
        }
        return null;
    }

    public static bool Delete(string dir, string id)
    {
        if (!IdPattern().IsMatch(id) || !File.Exists(StateFile(dir, id))) return false;
        File.Delete(StateFile(dir, id)); return true;
    }

    // ---------- Varianten ----------

    /// <summary>Variante als Kopie der Seite (oder einer anderen Variante) anlegen.</summary>
    public static (Variante? Variante, string? Error) CreateVariant(string dir, string name, string? from = null)
    {
        if (string.IsNullOrWhiteSpace(name)) return (null, "Bitte einen Namen für die Variante.");
        var trimmed = name.Trim(); trimmed = trimmed[..Math.Min(80, trimmed.Length)];
        var page = from is { } fid ? VariantPage(dir, fid) : WebDesignSite.Active(dir);
        var v = new Variante(NewId(), trimmed, DateTimeOffset.Now, page.Adresse, page.Sprache);
        WriteAtomic(VariantFile(dir, v.Id), new JsonObject { ["name"] = v.Name, ["zeit"] = v.Zeit.ToString("O"), ["seite"] = Read(dir, from), ["adresse"] = page.Adresse, ["sprache"] = page.Sprache }.ToJsonString());
        return (v, null);
    }

    public static IReadOnlyList<Variante> Variants(string dir)
    {
        var d = Path.Combine(dir, VariantsDir);
        if (!Directory.Exists(d)) return [];
        var list = new List<Variante>();
        foreach (var f in Directory.GetFiles(d, "*.json").OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal))
            try
            {
                if (JsonNode.Parse(File.ReadAllText(f)) is JsonObject o)
                {
                    var p = PageOf(o);
                    list.Add(new(Path.GetFileNameWithoutExtension(f), o["name"]?.ToString() ?? "", DateTimeOffset.TryParse(o["zeit"]?.ToString(), out var t) ? t : DateTimeOffset.MinValue, p.Adresse, p.Sprache));
                }
            }
            catch (Exception e) when (e is JsonException or InvalidOperationException or ArgumentException) { }
        return list;
    }

    public static string? VariantContent(string dir, string id)
    {
        if (!IdPattern().IsMatch(id) || !File.Exists(VariantFile(dir, id))) return null;
        try { return JsonNode.Parse(File.ReadAllText(VariantFile(dir, id)))?["seite"]?.ToString() is { } s ? WebDesign.Validate(s).Json : null; } // kaputte Datei gilt als nicht vorhanden, eine veränderte kommt bereinigt
        catch (Exception e) when (e is JsonException or InvalidOperationException or ArgumentException) { return null; }
    }

    static string VariantFileText(string dir, string id, string content)
    {
        var o = JsonNode.Parse(File.ReadAllText(VariantFile(dir, id)))!.AsObject();
        o["seite"] = content;
        return o.ToJsonString();
    }

    /// <summary>Variante speichern. Die Seite bleibt unberührt.</summary>
    public static string? SaveVariant(string dir, string id, string json)
    {
        if (VariantContent(dir, id) is null) return "Diese Variante gibt es nicht mehr.";
        var (ok, err) = WebDesign.Validate(json); if (ok is null) return err;
        WriteAtomic(VariantFile(dir, id), VariantFileText(dir, id, ok));
        return null;
    }

    /// <summary>Variante als Seite übernehmen. Die bisherige Seite bleibt als automatischer Stand erhalten.</summary>
    public static string? AdoptVariant(string dir, string id)
    {
        if (VariantContent(dir, id) is not { } text) return "Diese Variante gibt es nicht mehr.";
        var page = VariantPage(dir, id);
        if (WebDesignSite.Activate(dir, page.Adresse, page.Sprache) is { } perr) return "Die Seite dieser Variante gibt es nicht mehr: " + perr; // Stufe 3: Variante gehört zu ihrer Seite
        var (auto, err) = SaveContent(dir, "Vor Übernahme einer Variante", Read(dir), true);
        if (auto is null) return err;
        var save = WebDesign.Save(dir, text);
        if (save is null) LiftProject(dir, text); // mit der Variante gelten ihre Gestaltungswerte und Komponenten fürs Projekt
        return save;
    }

    /// <summary>Seite aus einem Stand oder einer Variante; ohne Sprache (ältere Dateien) gilt null = Standardsprache.</summary>
    static WebDesignSite.PageKey PageOf(JsonObject o)
    {
        var a = o["adresse"]?.ToString() is { } x && (x == WebDesignSite.Index || WebDesignSite.CheckSlug(x) is null) ? x : WebDesignSite.Index;
        var l = o["sprache"]?.ToString() is { } y && WebDesignSite.IsLanguage(y) ? y : null;
        return new(a, l!);
    }

    /// <summary>Seite, zu der eine Variante gehört.</summary>
    public static WebDesignSite.PageKey VariantPage(string dir, string id)
    {
        try
        {
            if (IdPattern().IsMatch(id) && File.Exists(VariantFile(dir, id)) && JsonNode.Parse(File.ReadAllText(VariantFile(dir, id))) is JsonObject o)
            {
                var p = PageOf(o);
                return p with { Sprache = p.Sprache ?? WebDesignSite.Load(dir).Standardsprache };
            }
        }
        catch (Exception e) when (e is JsonException or IOException or ArgumentException) { }
        return new(WebDesignSite.Index, WebDesignSite.Load(dir).Standardsprache);
    }

    public static bool DeleteVariant(string dir, string id)
    {
        if (!IdPattern().IsMatch(id) || !File.Exists(VariantFile(dir, id))) return false;
        File.Delete(VariantFile(dir, id)); return true;
    }
}
