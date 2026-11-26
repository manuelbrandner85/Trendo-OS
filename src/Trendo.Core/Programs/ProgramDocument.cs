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
    public string Id { get; private set; } = Guid.NewGuid().ToString("N")[..10];
    /// <summary>T-0730: Jeder Rückgängig-Schritt trägt eine fortlaufende Nummer (<see cref="_pushes"/>). Eine Marke merkt sich die Nummer statt der
    /// Stapelhöhe – die bleibt bei vollem Stapel (100) durch das Kürzen gleich, und Zurücksetzen bzw. Zusammenfassen träfe sonst nichts.</summary>
    readonly Stack<(string Data, string Label, long Seq)> _undo = new(), _redo = new();
    long _pushes;
    void PushUndo(string data, string label) { _undo.Push((data, label, ++_pushes)); if (_undo.Count > MaxUndo) Trim(_undo); }

    /// <summary>T-0680: Fassung des Dateiformats, die dieses Programm schreibt und versteht. Neuere Dateien werden nicht geöffnet (sonst gingen beim Speichern Inhalte verloren).</summary>
    public const int FormatVersion = 1;
    /// <summary>T-0680: nur zum Lesen geöffnet (die Datei ist in einem anderen Fenster offen) – Speichern über dieselbe Datei ist gesperrt, „Speichern unter“ geht.</summary>
    public bool ReadOnly { get; set; }
    /// <summary>Stand der Datei auf der Platte beim Laden bzw. nach dem letzten Speichern (Zeit, Größe) – daran erkennt <see cref="ChangedOnDisk"/> fremde Änderungen.</summary>
    (DateTime Utc, long Length)? _disk;
    byte[]? _diskHash; // Prüfsumme des Inhalts dazu: ein nur neuer Zeitstempel (Synchronisierung, Zurückkopieren derselben Fassung) ist keine fremde Änderung
    static byte[]? HashOf(string file)
    {
        try { using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); return System.Security.Cryptography.SHA256.HashData(fs); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }
    static (DateTime Utc, long Length)? Stamp(string file)
    {
        try { var fi = new FileInfo(file); return fi.Exists ? (fi.LastWriteTimeUtc, fi.Length) : null; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }
    /// <summary>T-0680: Die Datei wurde seit dem Laden/Speichern von außen geändert (anderes Programm, anderes Fenster, Synchronisierung).</summary>
    public bool ChangedOnDisk
    {
        get
        {
            if (Path is not { } p || _disk is not { } then || Stamp(p) is not { } now || now == then) return false;
            if (now.Length == then.Length && _diskHash is { } known && HashOf(p) is { } current && known.AsSpan().SequenceEqual(current)) { _disk = now; return false; } // gleicher Inhalt, nur neuer Zeitstempel
            return true;
        }
    }
    /// <summary>T-0680: Die Fassung auf der Platte gilt als bekannt – für Wege, die eine fremde Änderung bewusst ins Dokument übernommen haben
    /// (Claude bearbeitet die Datei, die Hülle spielt den Inhalt ein). Danach fragt das Speichern nicht nach.</summary>
    public void AcceptDisk() { if (Path is { } p) { _disk = Stamp(p); _diskHash = HashOf(p); } }
    /// <summary>Zeitpunkt der Fassung auf der Platte (für die Rückfrage).</summary>
    public DateTime? DiskTime => Path is { } p && File.Exists(p) ? File.GetLastWriteTime(p) : null;

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
        PushUndo(before, label);
        _redo.Clear(); Dirty = true; Changed = DateTime.Now;
    }

    /// <summary>T-0708: Der Stand wurde außerhalb gespeichert (eigenes Dateiformat eines angeschlossenen Programms, siehe <see cref="ProgramLink"/>) – gilt als gespeichert.</summary>
    public void MarkClean() => Dirty = false;

    /// <summary>T-0714: Inhalt ändern, ohne einen Rückgängig-Schritt anzulegen – für angeschlossene Programme, die ihr eigenes Rückgängig behalten
    /// (Text, Tabelle, Präsentation, CAD, 3D) und die Hülle nur für die Notsicherung nutzen. Danach gilt das Dokument als geändert.</summary>
    public void Track(Action<JsonObject> change) { change(Data); Dirty = true; Changed = DateTime.Now; }

    /// <summary>Begleitdaten ändern: kein Rückgängig-Schritt, aber das Dokument gilt als geändert.</summary>
    public void EditMeta(Action<JsonObject> change) { change(Meta); Dirty = true; Changed = DateTime.Now; }

    /// <summary>T-0723: Die Daten wurden durch Rückgängig, Wiederholen oder Zurücksetzen auf einen früheren Stand gebracht. Programme, deren Dokument
    /// auf echte Dateien außerhalb des .tdoc zeigt (Code mit KI), bringen hier diese Dateien mit – so bleibt es EIN Rückgängig-Schritt.</summary>
    public event Action<ProgramDocument>? Restored;

    /// <summary>T-0774: Ein Schritt wurde wirklich zurückgenommen (<c>Undo = true</c>) oder wiederholt – nur durch <see cref="Undo"/> und <see cref="Redo"/>,
    /// nie durch <see cref="Rollback"/> (Vorschau). <c>From</c> ist die Nummer des Schritts vorher, <c>To</c> danach (Wiederholen vergibt eine neue Nummer).</summary>
    public event Action<ProgramDocument, (long From, long To, bool Undo)>? Stepped;

    /// <summary>T-0774: Nummer des obersten Rückgängig-Schritts (0 = keiner).</summary>
    public long TopStep => _undo.Count > 0 ? _undo.Peek().Seq : 0;

    public bool Undo()
    {
        if (_undo.Count == 0) return false;
        var (data, label, seq) = _undo.Pop(); _redo.Push((Data.ToJsonString(), label, seq)); // T-0774: Nummer merken, damit Wiederholen den Schritt wiedererkennt
        Data = JsonNode.Parse(data)!.AsObject(); Dirty = true; Changed = DateTime.Now; Restored?.Invoke(this); Stepped?.Invoke(this, (seq, seq, true)); return true;
    }

    public bool Redo()
    {
        if (_redo.Count == 0) return false;
        var (data, label, old) = _redo.Pop(); PushUndo(Data.ToJsonString(), label); // neue Nummer: gilt als nach einer Marke hinzugekommen
        Data = JsonNode.Parse(data)!.AsObject(); Dirty = true; Changed = DateTime.Now; Restored?.Invoke(this); Stepped?.Invoke(this, (old, _pushes, false)); return true;
    }

    /// <summary>T-0315: Stand merken (Daten, Rückgängig-Tiefe, „geändert“) – für Vorschau-Läufe des Assistenten.</summary>
    /// <param name="Seq">T-0730: Nummer des letzten Rückgängig-Schritts beim Merken – alles mit größerer Nummer kam danach hinzu.</param>
    public readonly record struct Mark(string Data, int Undo, int Redo, bool Dirty, DateTime Changed, long Seq = 0);
    public Mark Remember() => new(Data.ToJsonString(), _undo.Count, _redo.Count, Dirty, Changed, _pushes);

    /// <summary>T-0730: Anzahl der Schritte oben auf dem Stapel, die nach der Marke hinzugekommen sind (auch bei vollem, gekürztem Stapel richtig).</summary>
    int SinceMark(Mark m) => _undo.TakeWhile(e => e.Seq > m.Seq).Count();

    /// <summary>Genau auf den gemerkten Stand zurück: Daten, Rückgängig-Liste (seitdem hinzugekommene Schritte fallen weg), „geändert“.
    /// T-0730: Es fallen genau die Schritte weg, die nach der Marke hinzukamen – auch wenn der Stapel voll ist. Wurden durch das Kürzen schon Schritte
    /// unterhalb der Marke entfernt, bleibt eben nur das übrig, was unterhalb noch da ist; verworfene Stände kommen nie über Rückgängig zurück.</summary>
    public void Rollback(Mark m)
    {
        for (var n = SinceMark(m); n > 0; n--) _undo.Pop();
        if (_redo.Count == 0 && m.Redo > 0) { } // Wiederholen-Liste ist durch Bearbeiten geleert – sie bleibt leer (wie nach jeder Änderung)
        Data = JsonNode.Parse(m.Data)!.AsObject(); Dirty = m.Dirty; Changed = m.Changed; Restored?.Invoke(this);
    }

    /// <summary>T-0728: Alle Rückgängig-Schritte seit <paramref name="since"/> werden EIN Schritt mit <paramref name="label"/> (Stand davor = Stand beim Merken).
    /// Für Aufträge, die das Dokument in mehreren Zügen ändern (Claude-Brücke). Daten und Wiederholen bleiben unberührt. true = es wurde zusammengefasst.
    /// T-0730: zählt über die Schrittnummer statt über die Stapelhöhe (bei vollem Stapel fasste es sonst nichts zusammen). Ist der älteste Schritt seit der
    /// Marke schon weggekürzt, gilt als „Stand davor“ der gemerkte Stand.</summary>
    public bool Squash(Mark since, string label)
    {
        var n = SinceMark(since);
        if (n < 2) return false;
        var trimmed = _undo.Count == n && _undo.Last().Seq != since.Seq + 1; // unterste Einträge seit der Marke fehlen
        (string Data, string Label, long Seq) first = default;
        for (var i = 0; i < n; i++) first = _undo.Pop();
        PushUndo(trimmed ? since.Data : first.Data, label);
        return true;
    }

    static void Trim(Stack<(string, string, long)> s) { var keep = s.Take(MaxUndo).Reverse().ToList(); s.Clear(); foreach (var x in keep) s.Push(x); }

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
    /// <param name="overwriteChanged">true: auch dann speichern, wenn die Datei inzwischen von außen geändert wurde (nach Rückfrage; die fremde Fassung bleibt als Version).</param>
    public void Save(string? path = null, bool keepVersion = true, bool overwriteChanged = false)
    {
        var target = TargetOf(path ?? Path ?? throw new InvalidOperationException("Bitte einen Speicherort wählen."));
        var old = Path; var same = old is not null && SameFile(old, target);
        if (same && ReadOnly) throw new InvalidOperationException("Dieses Dokument ist nur zum Lesen geöffnet, weil es in einem anderen Fenster offen ist. Speichere es unter einem anderen Namen oder arbeite mit einer Kopie weiter.");
        if (same && !overwriteChanged && ChangedOnDisk)
            throw new FileChangedOutsideException($"„{System.IO.Path.GetFileName(target)}“ wurde inzwischen außerhalb dieses Fensters geändert. Entscheide bitte, welche Fassung bleiben soll.");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(target))!);
        var json = Serialize(); if (keepVersion || same && ChangedOnDisk) KeepVersion(target, json); // die fremde Fassung geht beim Überschreiben nie verloren
        var bytes = new System.Text.UTF8Encoding(false).GetBytes(json);
        AtomicFile.WriteAllBytes(target, bytes); // T-0680: Zwischendatei → auf den Datenträger → ersetzen
        Path = target; Dirty = false; _disk = Stamp(target); _diskHash = System.Security.Cryptography.SHA256.HashData(bytes); if (!same) ReadOnly = false;
        // Notsicherung erst jetzt entfernen – das Speichern hat geklappt
        TryDelete(AutosaveFile(target)); if (!same && old is not null) TryDelete(AutosaveFile(old));
    }

    /// <summary>Dateiname, unter dem wirklich gespeichert wird (mit Endung).</summary>
    public static string TargetOf(string path) => path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase) ? path : path + Extension;
    public static bool SameFile(string a, string b) => string.Equals(System.IO.Path.GetFullPath(a), System.IO.Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
    static void TryDelete(string f) { try { if (File.Exists(f)) File.Delete(f); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } }

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
            if (!old.AsSpan().TrimStart().StartsWith("{") || !old.AsSpan().TrimEnd().EndsWith("}")) return; // leere oder abgeschnittene Datei ist kein Stand zum Zurückholen
            var dir = VersionDir(target); Directory.CreateDirectory(dir);
            var last = Directory.GetFiles(dir, "*" + Extension).Order(StringComparer.Ordinal).LastOrDefault();
            if (last is not null && File.ReadAllText(last) == old) return;
            var name = File.GetLastWriteTime(target).ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            var f = System.IO.Path.Combine(dir, name + Extension);
            for (var n = 2; File.Exists(f); n++) f = System.IO.Path.Combine(dir, $"{name}-{n}{Extension}");
            AtomicFile.WriteAllText(f, old); // T-0680: auch Versionen nie halb
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
        JsonObject root; string studio, title; JsonObject? data, meta; int version;
        var bytes = File.ReadAllBytes(path); var stamp = Stamp(path);
        try
        {
            string text; using (var reader = new StreamReader(new MemoryStream(bytes), System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true)) text = reader.ReadToEnd();
            root = JsonNode.Parse(text) as JsonObject ?? throw new InvalidDataException();
            if (root["format"] is not JsonValue fv || !fv.TryGetValue<string>(out var format) || format != "tdoc") throw Fail(LoadProblem.Foreign, "Diese Datei ist keine Projektdatei.");
            version = root["version"] is JsonValue vv && vv.TryGetValue<int>(out var n) ? n : 1; // ältere Dateien ohne Angabe gelten als Fassung 1
            studio = (string?)root["studio"] ?? ""; title = (string?)root["title"] ?? System.IO.Path.GetFileNameWithoutExtension(path);
            data = root["data"] is null ? null : root["data"]!.DeepClone().AsObject(); meta = root["meta"]?.DeepClone() as JsonObject;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException || e is InvalidDataException && ProblemOf(e) is null)
        { throw Fail(LoadProblem.Damaged, "Diese Datei ist keine lesbare Projektdatei."); }
        // T-0680: neuere Fassung nicht öffnen – beim Speichern gingen sonst Inhalte verloren, die dieses Programm noch nicht kennt
        if (version > FormatVersion) throw Fail(LoadProblem.Newer, $"Diese Datei wurde mit einer neueren Trendo-Fassung gespeichert (Dateiformat {version}, dieses Trendo kennt {FormatVersion}). Bitte aktualisiere Trendo – die Datei bleibt so lange unverändert.");
        if (expectStudio is not null && !studio.Equals(expectStudio, StringComparison.OrdinalIgnoreCase)) throw Fail(LoadProblem.OtherStudio, "Diese Projektdatei gehört zu einem anderen Studio.");
        var d = new ProgramDocument(studio, title, data);
        d.Path = path; d.Meta = meta ?? []; d._disk = stamp; d._diskHash = System.Security.Cryptography.SHA256.HashData(bytes); return d;
    }

    /// <summary>T-0680: Warum sich eine Datei nicht laden ließ. Die Ausnahme bleibt <see cref="InvalidDataException"/>; der Grund hängt daran.</summary>
    public enum LoadProblem { Damaged, Foreign, Newer, OtherStudio }
    const string ProblemKey = "tdoc.problem";
    static InvalidDataException Fail(LoadProblem p, string message) { var e = new InvalidDataException(message); e.Data[ProblemKey] = p; return e; }
    public static LoadProblem? ProblemOf(Exception e) => e.Data[ProblemKey] as LoadProblem?;

    /// <summary>T-0680: Ein Stand, aus dem sich eine Datei zurückholen lässt. <paramref name="Kind"/>: „sicherung“ (automatisch gesichert) oder „version“ (früherer gespeicherter Stand).</summary>
    public sealed record Recovery(string File, DateTime When, string Kind);

    /// <summary>T-0680: Lesbare Stände zu einer Datei, neueste zuerst – offene Wiederherstellung, Notsicherung daneben, frühere Versionen. Unlesbare werden übersprungen.</summary>
    public static List<Recovery> Recoveries(string path, string? studio = null)
    {
        var all = new List<Recovery>();
        foreach (var f in new[] { RecoveryFile(path), AutosaveFile(path) }) if (File.Exists(f)) all.Add(new(f, File.GetLastWriteTime(f), "sicherung"));
        try { all.AddRange(Versions(path).Select(v => new Recovery(v.File, v.When, "version"))); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        return all.Where(r => Readable(r.File, studio)).OrderByDescending(r => r.When).ToList();
    }

    public static bool Readable(string file, string? studio = null)
    {
        try { Load(file, studio); return true; }
        catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException) { return false; }
    }

    // ---------- automatisches Sichern ----------
    public static string AutosaveFile(string path) => path + ".autosave";
    /// <summary>T-0680: Notsicherung, die beim Öffnen gefunden und angeboten wurde – liegt hier, bis entschieden ist (Wiederherstellen oder Verwerfen).
    /// Eigener Name, damit weiteres Speichern oder Sichern das Angebot nicht still überschreibt oder löscht.</summary>
    public static string RecoveryFile(string path) => path + ".wiederherstellen";

    /// <summary>Sichert ungespeicherte Änderungen neben der Datei, bei neuen (oder nur zum Lesen geöffneten) Dokumenten in den Sicherungsordner.</summary>
    public string? Autosave(string backupDir)
    {
        if (!Dirty) return null;
        var f = Path is { } p && !ReadOnly ? AutosaveFile(p) : BackupFile(backupDir);
        AtomicFile.WriteAllText(f, Serialize()); return f; // T-0680: nie eine halbe Sicherung
    }

    /// <summary>Platz der Notsicherung dieses Dokuments im Sicherungsordner.</summary>
    public string BackupFile(string backupDir) => System.IO.Path.Combine(backupDir, $"{Studio}-{Id}{Extension}");

    /// <summary>Gesicherte, nie gespeicherte Dokumente eines Studios (zum Wiederherstellen nach einem Absturz).</summary>
    public static List<string> Orphans(string backupDir, string studio) =>
        Directory.Exists(backupDir) ? Directory.GetFiles(backupDir, $"{studio}-*{Extension}").OrderByDescending(File.GetLastWriteTime).ToList() : [];

    /// <summary>T-0680: wie <see cref="Orphans"/>, aber geprüft: lesbare Sicherungen mit Titel und Zeit, unlesbare getrennt (zum Melden statt Abstürzen).</summary>
    public static (List<(string File, string Title, DateTime When)> Good, List<string> Broken) OrphanScan(string backupDir, string studio)
    {
        var good = new List<(string, string, DateTime)>(); var broken = new List<string>();
        foreach (var f in Orphans(backupDir, studio))
            try { good.Add((f, Load(f).Title, File.GetLastWriteTime(f))); }
            catch (InvalidDataException) { broken.Add(f); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } // gerade nicht lesbar (z. B. wird geschrieben) – weder anbieten noch als kaputt melden
        return (good, broken);
    }

    /// <summary>Dokument aus einer Sicherung. Eine Sicherung neben der Datei gehört wieder zu dieser Datei; eine aus dem Sicherungsordner behält ihre Kennung,
    /// damit die Sicherung erst nach dem nächsten erfolgreichen Speichern verschwindet. <paramref name="target"/>: ausdrücklich für diese Datei (beschädigte Datei ersetzen).</summary>
    public static ProgramDocument Restore(string autosaveFile, string? target = null)
    {
        var d = Load(autosaveFile);
        target ??= new[] { ".autosave", ".wiederherstellen" }.Where(s => autosaveFile.EndsWith(s, StringComparison.Ordinal)).Select(s => autosaveFile[..^s.Length]).FirstOrDefault();
        var r = new ProgramDocument(d.Studio, d.Title, d.Data) { Path = target, Dirty = true, Meta = d.Meta };
        if (target is null)
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(autosaveFile);
            if (name.StartsWith(d.Studio + "-", StringComparison.OrdinalIgnoreCase) && name.Length > d.Studio.Length + 1) r.Id = name[(d.Studio.Length + 1)..];
        }
        else r.AcceptDisk();
        return r;
    }

    /// <summary>T-0680: Inhalt, Titel und Begleitdaten aus einem anderen Stand übernehmen – als ein Bearbeitungsschritt (Rückgängig bringt den jetzigen Stand zurück).</summary>
    public void TakeOver(ProgramDocument other, string label)
    {
        var json = other.Data.ToJsonString();
        Edit(label, d => { d.Clear(); foreach (var kv in JsonNode.Parse(json)!.AsObject().ToList()) d[kv.Key] = kv.Value?.DeepClone(); });
        Title = other.Title; Meta = other.Meta.DeepClone().AsObject();
    }

    /// <summary>T-0680: Fassung von der Platte neu laden (nach „von außen geändert“): Inhalt übernehmen, danach gilt das Dokument als gespeichert.</summary>
    public void ReloadFromDisk()
    {
        var fresh = Load(Path ?? throw new InvalidOperationException("Dieses Dokument hat noch keine Datei."), Studio);
        TakeOver(fresh, "Fassung von der Platte geladen"); _disk = fresh._disk; _diskHash = fresh._diskHash; Dirty = false;
    }

    /// <summary>T-0680: Als Kopie weiterarbeiten – löst das Dokument von seiner Datei (nichts wird überschrieben), der nächste Speichervorgang legt eine neue Datei an.</summary>
    public void Detach(string? title = null)
    {
        Path = null; _disk = null; _diskHash = null; ReadOnly = false; Dirty = true; Changed = DateTime.Now;
        if (!string.IsNullOrWhiteSpace(title)) Title = title.Trim();
    }
}

/// <summary>T-0680: Die Datei wurde seit dem Laden von außen geändert – vor dem Überschreiben muss nachgefragt werden.</summary>
public sealed class FileChangedOutsideException(string message) : IOException(message);

/// <summary>V3: „Zuletzt geöffnet“ je Studio (höchstens 12, nur noch vorhandene Dateien).</summary>
public sealed class ProgramRecent(string dataRoot)
{
    string F => System.IO.Path.Combine(dataRoot, "programme", "zuletzt.json");
    Dictionary<string, List<string>> Load() { try { return File.Exists(F) ? JsonSerializer.Deserialize<Dictionary<string, List<string>>>(File.ReadAllText(F)) ?? [] : []; } catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException) { return []; } }
    public List<string> Get(string studio) => Load().TryGetValue(studio, out var l) ? l.Where(File.Exists).ToList() : [];
    public void Add(string studio, string path)
    {
        var d = Load(); var l = d.TryGetValue(studio, out var x) ? x : [];
        l.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)); l.Insert(0, path); if (l.Count > 12) l.RemoveRange(12, l.Count - 12);
        d[studio] = l; AtomicFile.WriteAllText(F, JsonSerializer.Serialize(d)); // T-0680: nie eine halbe Liste
    }
}
