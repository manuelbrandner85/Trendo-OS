using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trendo.Core.Programs;

/// <summary>T-0231 V3: Projektdatei eines Studios. Inhalt als JSON, dazu Titel, Studio und Zeitstempel.
/// Rückgängig/Wiederholen über Schnappschüsse, automatisches Sichern in eine Nebendatei, „Zuletzt geöffnet“ je Studio.
/// Die Datei enthält keinen Programm- oder Herstellernamen (Vorgabe 26.10.2026).</summary>
public sealed class ProgramDocument
{
    public const string Extension = ".tdoc";
    public const int MaxUndo = 100;
    static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    public string Studio { get; }
    public string Title { get; set; }
    public JsonObject Data { get; private set; }
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

    /// <summary>Jede Änderung läuft hierüber: vorher Schnappschuss, danach „geändert“.</summary>
    public void Edit(string label, Action<JsonObject> change)
    {
        var before = Data.ToJsonString();
        change(Data);
        if (Data.ToJsonString() == before) return;
        _undo.Push((before, label)); if (_undo.Count > MaxUndo) Trim(_undo);
        _redo.Clear(); Dirty = true; Changed = DateTime.Now;
    }

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
    public string Serialize() => new JsonObject
    {
        ["format"] = "tdoc", ["version"] = 1, ["studio"] = Studio, ["title"] = Title,
        ["created"] = Created.ToString("O"), ["changed"] = Changed.ToString("O"), ["data"] = Data.DeepClone(),
    }.ToJsonString(Pretty);

    public void Save(string? path = null)
    {
        var target = path ?? Path ?? throw new InvalidOperationException("Bitte einen Speicherort wählen.");
        if (!target.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)) target += Extension;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(target))!);
        var tmp = target + ".tmp"; File.WriteAllText(tmp, Serialize()); File.Move(tmp, target, true);
        Path = target; Dirty = false;
        if (File.Exists(AutosaveFile(target))) File.Delete(AutosaveFile(target));
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
        d.Path = path; return d;
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
        var r = new ProgramDocument(d.Studio, d.Title, d.Data) { Path = target, Dirty = true };
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
