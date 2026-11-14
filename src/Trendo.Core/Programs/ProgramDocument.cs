using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trendo.Core.Programs;

/// <summary>T-0231 V3: Projektdatei eines Studios. Inhalt als JSON, dazu Titel, Studio und Zeitstempel.
/// Rückgängig/Wiederholen über Schnappschüsse, automatisches Sichern in eine Nebendatei, „Zuletzt geöffnet“ je Studio.
/// Die Datei enthält keinen Programm- oder Herstellernamen (Vorgabe 26.10.2026).</summary>
/// <summary>Werte aus Studio-Dokumenten sicher als Text lesen (Befund 02.11.2026: Claude schrieb in eine Textliste Einträge mit Name und Beschreibung – die Spielbibel stürzte ab).
/// Text bleibt Text, Zahlen und Wahrheitswerte werden zu Text, Einträge mit mehreren Feldern zu „Name – Beschreibung“, Listen zu „a, b, c“.</summary>
public static class JsonText
{
    public static string? AsText(this JsonNode? n) => n switch
    {
        null => null,
        JsonValue v when v.TryGetValue<string>(out var s) => s,
        JsonValue v => v.ToJsonString().Trim('"'),
        JsonObject o => string.Join(" – ", o.Select(kv => kv.Value.AsText()).Where(x => !string.IsNullOrWhiteSpace(x))),
        JsonArray a => string.Join(", ", a.Select(x => x.AsText()).Where(x => !string.IsNullOrWhiteSpace(x))),
        _ => n.ToJsonString(),
    };

    /// <summary>Wahrheitswert sicher lesen (true, "ja", "true", 1) – sonst <paramref name="fallback"/>.</summary>
    public static bool AsBool(this JsonNode? n, bool fallback = false) => n?.GetValueKind() switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.String => n.AsText()?.Trim().ToLowerInvariant() switch { "ja" or "true" or "1" or "wahr" => true, "nein" or "false" or "0" or "falsch" => false, _ => fallback },
        JsonValueKind.Number => ToNumberSafe(n) != 0,
        _ => fallback,
    };
    static double ToNumberSafe(JsonNode n) => double.TryParse(n.ToJsonString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
}

public sealed class ProgramDocument
{
    public const string Extension = ".tdoc";
    public const int MaxUndo = 100;
    static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    public string Studio { get; }
    public string Title { get; set; }
    public JsonObject Data { get; private set; }
    /// <summary>T-0610: Begleitdaten außerhalb von Rückgängig und Versionen (z. B. Chat-Verläufe); werden mit der Datei gespeichert.</summary>
    public JsonObject Meta { get; private set; } = [];
    public string? Path { get; private set; }
    public bool Dirty { get; private set; }
    public DateTime Created { get; }
    public DateTime Changed { get; private set; }
    public string Id { get; } = Guid.NewGuid().ToString("N")[..10];
    readonly Stack<(string Data, string Label)> _undo = new(), _redo = new();

    public ProgramDocument(string studio, string title, JsonObject? data = null)
    {
        Studio = studio; Title = string.IsNullOrWhiteSpace(title) ? "Unbenannt" : title.Trim();
        Data = data ?? []; Created = Changed = DateTime.Now;
    }

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public string? UndoLabel => _undo.Count > 0 ? _undo.Peek().Label : null;
    /// <summary>Änderungsverlauf, neueste zuerst (für den Verlauf in der Profi-Stufe).</summary>
    public IReadOnlyList<string> History => _undo.Select(u => u.Label).ToList();

    /// <summary>Profi-Stufe: Projektdaten als Ganzes ersetzen – nur ein JSON-Objekt, als ein Schritt rückgängig machbar.</summary>
    public void ReplaceData(string json)
    {
        JsonNode? n;
        try { n = JsonNode.Parse(json); } catch (JsonException ex) { throw new InvalidDataException("Die Projektdaten sind kein gültiges JSON: " + ex.Message); }
        if (n is not JsonObject o) throw new InvalidDataException("Die Projektdaten müssen ein JSON-Objekt { … } sein.");
        Edit("Projektdaten bearbeitet", d => { d.Clear(); foreach (var (k, v) in o.ToList()) { o.Remove(k); d[k] = v; } });
    }

    /// <summary>Jede Änderung läuft hierüber: vorher Schnappschuss, danach „geändert“.</summary>
    public void Edit(string label, Action<JsonObject> change)
    {
        var before = Data.ToJsonString();
        change(Data);
        if (Data.ToJsonString() == before) return;
        _undo.Push((before, label)); if (_undo.Count > MaxUndo) Trim(_undo);
        _redo.Clear(); Dirty = true; Changed = DateTime.Now;
    }

    /// <summary>Begleitdaten ändern: kein Rückgängig-Schritt, aber das Dokument gilt als geändert.</summary>
    public void EditMeta(Action<JsonObject> change) { change(Meta); Dirty = true; Changed = DateTime.Now; }

    public bool Undo()
    {
        if (_undo.Count == 0) return false;
        var (data, label) = _undo.Pop(); _redo.Push((Data.ToJsonString(), label));
        Data = JsonNode.Parse(data)!.AsObject(); Dirty = true; Changed = DateTime.Now; return true;
    }

    public bool Redo()
    {
        if (_redo.Count == 0) return false;
        var (data, label) = _redo.Pop(); _undo.Push((Data.ToJsonString(), label));
        Data = JsonNode.Parse(data)!.AsObject(); Dirty = true; Changed = DateTime.Now; return true;
    }

    /// <summary>T-0315: Stand merken (Daten, Rückgängig-Tiefe, „geändert“) – für Vorschau-Läufe des Assistenten.</summary>
    public readonly record struct Mark(string Data, int Undo, int Redo, bool Dirty, DateTime Changed);
    public Mark Remember() => new(Data.ToJsonString(), _undo.Count, _redo.Count, Dirty, Changed);

    /// <summary>Genau auf den gemerkten Stand zurück: Daten, Rückgängig-Liste (seitdem hinzugekommene Schritte fallen weg), „geändert“.</summary>
    public void Rollback(Mark m)
    {
        while (_undo.Count > m.Undo) _undo.Pop();
        if (_redo.Count == 0 && m.Redo > 0) { } // Wiederholen-Liste ist durch Bearbeiten geleert – sie bleibt leer (wie nach jeder Änderung)
        Data = JsonNode.Parse(m.Data)!.AsObject(); Dirty = m.Dirty; Changed = m.Changed;
    }

    static void Trim(Stack<(string, string)> s) { var keep = s.Take(MaxUndo).Reverse().ToList(); s.Clear(); foreach (var x in keep) s.Push(x); }

    // ---------- Werte bequem lesen/schreiben ----------
    public string Text(string key, string fallback = "") => Data[key]?.GetValueKind() == JsonValueKind.String ? Data[key]!.GetValue<string>() : fallback;
    public double Number(string key, double fallback = 0) => ToNumber(Data[key], fallback);

    /// <summary>Zahl aus einem JSON-Knoten – egal ob als Ganzzahl oder Kommazahl gespeichert.</summary>
    public static double ToNumber(JsonNode? n, double fallback = 0) =>
        n?.GetValueKind() == JsonValueKind.Number && double.TryParse(n.ToJsonString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : fallback;
    public bool Flag(string key) => Data[key]?.GetValueKind() == JsonValueKind.True;
    public JsonArray List(string key) => Data[key] as JsonArray ?? [];
    public void Set(string key, JsonNode? value, string? label = null) => Edit(label ?? key, d => d[key] = value?.DeepClone());

    // ---------- Datei ----------
    public string Serialize()
    {
        var root = new JsonObject
        {
            ["format"] = "tdoc", ["version"] = 1, ["studio"] = Studio, ["title"] = Title,
            ["created"] = Created.ToString("O"), ["changed"] = Changed.ToString("O"), ["data"] = Data.DeepClone(),
        };
        if (Meta.Count > 0) root["meta"] = Meta.DeepClone();
        return root.ToJsonString(Pretty);
    }

    /// <param name="keepVersion">false: den alten Stand nicht als Version behalten (automatisches Speichern zwischen zwei Versionen, siehe <see cref="VersionDue"/>).</param>
    public void Save(string? path = null, bool keepVersion = true)
    {
        var target = path ?? Path ?? throw new InvalidOperationException("Bitte einen Speicherort wählen.");
        if (!target.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)) target += Extension;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(target))!);
        var json = Serialize(); if (keepVersion) KeepVersion(target, json);
        var tmp = target + ".tmp"; File.WriteAllText(tmp, json); SafeFile.Move(tmp, target);
        Path = target; Dirty = false;
        if (File.Exists(AutosaveFile(target))) File.Delete(AutosaveFile(target));
    }

    // ───────────── X17: Versionen (freigegeben 04.11.2026) ─────────────
    public const int MaxVersions = 20;

    /// <summary>Ordner der Versionen einer Datei: neben der Datei unter „.versionen/Name“.</summary>
    public static string VersionDir(string file) => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(file))!, ".versionen", System.IO.Path.GetFileNameWithoutExtension(file));

    /// <summary>Vor dem Überschreiben den alten Stand behalten (nur wenn er sich unterscheidet); höchstens 20 Versionen.</summary>
    static void KeepVersion(string target, string next)
    {
        if (!File.Exists(target)) return;
        try
        {
            var old = File.ReadAllText(target);
            if (old == next) return; // nichts geändert
            var dir = VersionDir(target); Directory.CreateDirectory(dir);
            var last = Directory.GetFiles(dir, "*" + Extension).Order(StringComparer.Ordinal).LastOrDefault();
            if (last is not null && File.ReadAllText(last) == old) return;
            var name = File.GetLastWriteTime(target).ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            var f = System.IO.Path.Combine(dir, name + Extension);
            for (var n = 2; File.Exists(f); n++) f = System.IO.Path.Combine(dir, $"{name}-{n}{Extension}");
            File.WriteAllText(f, old);
            foreach (var x in Directory.GetFiles(dir, "*" + Extension).Order(StringComparer.Ordinal).SkipLast(MaxVersions)) File.Delete(x);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } // Versionen sind ein Zusatz – Speichern geht trotzdem
    }

    /// <summary>E3 (T-0616): Beim automatischen Speichern nach jeder Änderung wird höchstens alle <paramref name="gap"/> ein früherer Stand behalten,
    /// damit die 20 Versionen nicht nach 20 Klicks voll sind. Gibt es noch keine Version, ist eine fällig.</summary>
    public static bool VersionDue(string file, TimeSpan gap, DateTime? now = null)
    {
        if (!File.Exists(file)) return false;
        var last = Versions(file).FirstOrDefault();
        return last.File is null || (now ?? DateTime.Now) - last.When >= gap;
    }

    /// <summary>Versionen einer Datei, neueste zuerst: (Datei, Zeitpunkt).</summary>
    public static List<(string File, DateTime When)> Versions(string file)
    {
        var dir = VersionDir(file);
        if (!Directory.Exists(dir)) return [];
        return Directory.GetFiles(dir, "*" + Extension).Order(StringComparer.Ordinal).Reverse()
            .Select(f => (f, DateTime.TryParseExact(System.IO.Path.GetFileNameWithoutExtension(f)[..15], "yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var t) ? t : File.GetLastWriteTime(f))).ToList();
    }

    /// <summary>Version zurückholen – als Bearbeitungsschritt (Rückgängig bringt den jetzigen Stand zurück).</summary>
    public void RestoreVersion(string versionFile)
    {
        var v = Load(versionFile, Studio);
        var json = v.Data.ToJsonString();
        Edit("Version wiederherstellen", d => { d.Clear(); foreach (var kv in JsonNode.Parse(json)!.AsObject().ToList()) d[kv.Key] = kv.Value?.DeepClone(); });
    }

    public static ProgramDocument Load(string path, string? expectStudio = null)
    {
        JsonObject root;
        try { root = JsonNode.Parse(File.ReadAllText(path))?.AsObject() ?? throw new InvalidDataException(); }
        catch (Exception e) when (e is JsonException or InvalidOperationException or InvalidDataException) { throw new InvalidDataException("Diese Datei ist keine lesbare Projektdatei."); }
        if ((string?)root["format"] != "tdoc") throw new InvalidDataException("Diese Datei ist keine Projektdatei.");
        var studio = (string?)root["studio"] ?? "";
        if (expectStudio is not null && !studio.Equals(expectStudio, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Diese Projektdatei gehört zu einem anderen Studio.");
        var d = new ProgramDocument(studio, (string?)root["title"] ?? System.IO.Path.GetFileNameWithoutExtension(path), root["data"]?.DeepClone().AsObject());
        d.Path = path; d.Meta = root["meta"]?.DeepClone() as JsonObject ?? []; return d;
    }

    // ---------- automatisches Sichern ----------
    public static string AutosaveFile(string path) => path + ".autosave";

    /// <summary>Sichert ungespeicherte Änderungen neben der Datei, bei neuen Dokumenten in den Sicherungsordner.</summary>
    public string? Autosave(string backupDir)
    {
        if (!Dirty) return null;
        var f = Path is { } p ? AutosaveFile(p) : System.IO.Path.Combine(backupDir, $"{Studio}-{Id}{Extension}");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(f)!);
        File.WriteAllText(f, Serialize()); return f;
    }

    /// <summary>Gesicherte, nie gespeicherte Dokumente eines Studios (zum Wiederherstellen nach einem Absturz).</summary>
    public static List<string> Orphans(string backupDir, string studio) =>
        Directory.Exists(backupDir) ? Directory.GetFiles(backupDir, $"{studio}-*{Extension}").OrderByDescending(File.GetLastWriteTime).ToList() : [];

    public static ProgramDocument Restore(string autosaveFile)
    {
        var d = Load(autosaveFile); var target = autosaveFile.EndsWith(".autosave", StringComparison.Ordinal) ? autosaveFile[..^".autosave".Length] : null;
        var r = new ProgramDocument(d.Studio, d.Title, d.Data) { Path = target, Dirty = true, Meta = d.Meta };
        return r;
    }
}

/// <summary>V3: „Zuletzt geöffnet“ je Studio (höchstens 12, nur noch vorhandene Dateien).</summary>
public sealed class ProgramRecent(string dataRoot)
{
    string F => System.IO.Path.Combine(dataRoot, "programme", "zuletzt.json");
    Dictionary<string, List<string>> Load() { try { return File.Exists(F) ? JsonSerializer.Deserialize<Dictionary<string, List<string>>>(File.ReadAllText(F)) ?? [] : []; } catch (JsonException) { return []; } }
    public List<string> Get(string studio) => Load().TryGetValue(studio, out var l) ? l.Where(File.Exists).ToList() : [];
    public void Add(string studio, string path)
    {
        var d = Load(); var l = d.TryGetValue(studio, out var x) ? x : [];
        l.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)); l.Insert(0, path); if (l.Count > 12) l.RemoveRange(12, l.Count - 12);
        d[studio] = l; Directory.CreateDirectory(System.IO.Path.GetDirectoryName(F)!); File.WriteAllText(F, JsonSerializer.Serialize(d));
    }
}
