using System.Text.Json.Nodes;

namespace Trendo.Core.Programs;

/// <summary>T-0708 (B6 „Programm-Hülle für alle“, Etappe 1): schließt ein Programm außerhalb der Hülle an deren Rückgängig- und Notsicherungssystem an –
/// ohne zweites System. Der Stand des Programms liegt als JSON in einem <see cref="ProgramDocument"/> (<c>data.format</c> + <c>data.inhalt</c>):
/// Rückgängig/Wiederholen sind die 100 Schritte der Hülle, die Notsicherung ist die der Hülle (<c>programme/sicherung/&lt;Studio&gt;-&lt;Kennung&gt;.tdoc</c>,
/// atomar über <see cref="AtomicFile"/>, Dateiformat mit <c>version</c>). Das eigene Dateiformat des Programms bleibt unverändert – gespeichert wird weiter dort.
/// Ablauf: <see cref="Reset"/> beim Neu/Öffnen, <see cref="Commit"/> nach jeder Änderung, <see cref="Autosave"/> im Takt der Hülle,
/// <see cref="Saved"/> nach dem Speichern, beim Start <see cref="Pending"/> anbieten und mit <see cref="Restore"/> zurückholen.</summary>
public sealed class ProgramLink
{
    public string Studio { get; }
    /// <summary>Art des Inhalts (z. B. „brief“) – eine Sicherung eines anderen Formats wird nicht eingespielt.</summary>
    public string Format { get; }
    public ProgramDocument Doc { get; private set; }
    /// <summary>T-0711: Sperren dieses Fensters (wie bei der Hülle, <see cref="ProgramLocks"/>). Gesetzt: die eigene Notsicherung wird beim Schreiben für dieses Fenster gesperrt,
    /// Sicherungen, die ein anderes noch laufendes Fenster hält, werden nicht angeboten und lassen sich nicht übernehmen. Eine Sperre eines abgestürzten Prozesses gilt als verwaist.</summary>
    public ProgramLocks? Locks { get; set; }

    public ProgramLink(string studio, string format, JsonNode? state = null, string title = "Unbenannt")
    {
        Studio = studio; Format = format; Doc = Make(state, title);
    }

    ProgramDocument Make(JsonNode? state, string title) => new(Studio, title, new JsonObject { ["format"] = Format, ["inhalt"] = state?.DeepClone() });

    /// <summary>Aktueller Stand (so wie zuletzt übernommen).</summary>
    public JsonNode? State => Doc.Data["inhalt"];
    public bool Dirty => Doc.Dirty;
    public bool CanUndo => Doc.CanUndo;
    public bool CanRedo => Doc.CanRedo;
    public string? UndoLabel => Doc.UndoLabel;

    /// <summary>Neues oder frisch geöffnetes Dokument: leerer Verlauf, gilt als gespeichert; die alte Notsicherung (falls vorhanden) entfällt.</summary>
    public void Reset(JsonNode? state, string? title = null, string? backupDir = null)
    {
        if (backupDir is not null) DropBackup(backupDir);
        Doc = Make(state, title ?? Doc.Title);
    }

    /// <summary>Stand nach einer Änderung übernehmen – ein Rückgängig-Schritt. Gleicher Stand: nichts passiert (false).</summary>
    public bool Commit(string label, JsonNode? state)
    {
        var was = Doc.Data.ToJsonString();
        Doc.Edit(label, d => d["inhalt"] = state?.DeepClone());
        return Doc.Data.ToJsonString() != was;
    }

    /// <summary>T-0714: Stand für die Notsicherung übernehmen, ohne Rückgängig-Schritt – für Programme mit eigenem Rückgängig (Text, Tabelle, Präsentation, CAD, 3D).
    /// Danach gilt das Dokument als ungespeichert; <see cref="Autosave"/> schreibt es.</summary>
    public void Track(JsonNode? state, string? title = null)
    {
        Doc.Track(d => d["inhalt"] = state);
        if (!string.IsNullOrWhiteSpace(title)) Doc.Title = title.Trim();
    }

    /// <summary>T-0714: Stand eines Programms, das in einem eigenen Dateiformat speichert (DOCX, XLSX, PPTX, .tcad, Szene-JSON): Dateiname und Inhalt der Datei
    /// genau so, wie das Programm sie selbst schreibt (Base64) – die Notsicherung verliert nichts, was die Datei kann.</summary>
    public static JsonObject FileState(string name, byte[] bytes) => new() { ["datei"] = name, ["base64"] = Convert.ToBase64String(bytes) };

    /// <summary>T-0714: <see cref="FileState"/> über das eigene Speichern des Programms (DOCX, XLSX, PPTX …): in eine Zwischendatei in <paramref name="scratchDir"/> schreiben,
    /// Inhalt übernehmen, Zwischendatei löschen. Die App und das Absturz-Hilfsprogramm nutzen genau diesen Weg.</summary>
    public static JsonObject FileStateVia(string name, Action<string> save, string scratchDir)
    {
        Directory.CreateDirectory(scratchDir);
        var tmp = Path.Combine(scratchDir, Guid.NewGuid().ToString("N")[..10] + "-" + Path.GetFileName(name));
        try { save(tmp); return FileState(name, File.ReadAllBytes(tmp)); }
        finally { try { File.Delete(tmp); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } }
    }

    /// <summary>T-0714: Datei aus einem <see cref="FileState"/> zurück (Name, Inhalt). Unbrauchbarer Stand: <see cref="InvalidDataException"/>.</summary>
    public static (string Name, byte[] Bytes) FileOf(JsonNode? state)
    {
        try
        {
            var name = state?["datei"]?.GetValue<string>(); var b64 = state?["base64"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(name) || b64 is null) throw new InvalidDataException("Die Sicherung enthält keine Datei.");
            return (Path.GetFileName(name), Convert.FromBase64String(b64));
        }
        catch (Exception e) when (e is FormatException or InvalidOperationException) { throw new InvalidDataException("Die Sicherung ist beschädigt.", e); }
    }

    /// <summary>Einen Schritt zurück; liefert den Stand, den das Programm übernehmen soll (null: nichts zurückzunehmen).</summary>
    public JsonNode? Undo() => Doc.Undo() ? State : null;
    public JsonNode? Redo() => Doc.Redo() ? State : null;

    /// <summary>Notsicherung schreiben, wenn es ungespeicherte Änderungen gibt (<paramref name="worth"/> = false: Stand ist nichts wert, z. B. leer).</summary>
    public string? Autosave(string backupDir, bool worth = true)
    {
        if (!worth) return null;
        var f = Doc.Autosave(backupDir);
        if (f is not null) Locks?.Acquire(f); // T-0711: ein zweites Fenster bietet diese Sicherung nicht als „nicht gespeichert“ an
        return f;
    }

    /// <summary>Nach erfolgreichem Speichern im eigenen Format: gilt als gespeichert, Notsicherung weg.</summary>
    public void Saved(string backupDir) { Doc.MarkClean(); DropBackup(backupDir); }

    void DropBackup(string backupDir) { try { var f = Doc.BackupFile(backupDir); Locks?.Release(f); if (File.Exists(f)) File.Delete(f); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } }

    /// <summary>Lesbare Notsicherungen dieses Studios und Formats, neueste zuerst – ohne die des gerade offenen Dokuments.</summary>
    public List<(string File, string Title, DateTime When)> Pending(string backupDir)
    {
        var mine = Doc.BackupFile(backupDir);
        return ProgramDocument.OrphanScan(backupDir, Studio).Good
            .Where(g => !ProgramDocument.SameFile(g.File, mine) && FormatOf(g.File) == Format && Locks?.HeldByOther(g.File) is null).ToList();
    }

    static string? FormatOf(string file) { try { return ProgramDocument.Load(file).Data["format"].AsText(); } catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException) { return null; } }

    /// <summary>Notsicherung übernehmen: Das Dokument behält deren Kennung (die Sicherung verschwindet erst nach dem nächsten Speichern), gilt als ungespeichert.
    /// Die bisherige Notsicherung des offenen Dokuments entfällt – außer es war ungespeichert (T-0714): dann bleibt sie liegen und wird zum Zurückholen angeboten
    /// (vorher galt das nur laut Kommentar der Aufrufer, die Sicherung wurde aber gelöscht). Liefert den Stand für das Programm.</summary>
    public JsonNode? Restore(string file, string backupDir)
    {
        var r = ProgramDocument.Restore(file);
        if (!r.Studio.Equals(Studio, StringComparison.OrdinalIgnoreCase) || r.Data["format"].AsText() != Format) throw new InvalidDataException("Diese Sicherung gehört zu einem anderen Programm.");
        if (Locks?.Acquire(file) is { } other) // T-0711: in einem anderen Fenster schon zurückgeholt
            throw new IOException($"Diese Sicherung ist gerade in einem anderen Trendo-Fenster offen (seit {other.Since:HH:mm} Uhr).");
        if (Doc.Dirty) { try { Locks?.Release(Doc.BackupFile(backupDir)); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } }
        else DropBackup(backupDir);
        Doc = r; return State;
    }

    /// <summary>Notsicherung verwerfen (sie wird gelöscht – sie ist nur eine Kopie ungespeicherter Änderungen).</summary>
    public static void Discard(string file, ProgramLocks? locks = null) { try { locks?.Release(file); if (File.Exists(file)) File.Delete(file); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } }
}
