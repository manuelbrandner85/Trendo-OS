using System.Security.Cryptography;
using System.Text.Json;

namespace Trendo.Core;

/// <summary>Beleg zu einer Karte: Datei im Projekt (relativ), SHA-256 beim Hinzufügen, Notiz.</summary>
public sealed record BoardEvidence(string Path, string Sha256, string Note, DateTimeOffset Added);

/// <summary>Karte auf der Tafel. Column: 0 Offen, 1 Läuft, 2 Fertig. Role: zuständige KI-Rolle bzw. Studio. Progress 0–100.</summary>
public sealed class BoardCard
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Title { get; set; } = "";
    public string Detail { get; set; } = "";
    public int Column { get; set; }
    public string Role { get; set; } = "Planung";
    public int Progress { get; set; }
    public List<BoardEvidence> Evidence { get; set; } = [];
    public DateTimeOffset Created { get; set; } = DateTimeOffset.Now;
}

public sealed class BigBoard
{
    public string Goal { get; set; } = "";
    public List<BoardCard> Cards { get; set; } = [];
    /// <summary>Entfernte Karten (wiederherstellbar) – nichts geht endgültig verloren.</summary>
    public List<BoardCard> Trash { get; set; } = [];
    public int Percent => Cards.Count == 0 ? 0 : (int)Math.Round(Cards.Average(c => c.Column == 2 ? 100 : c.Progress));
}

/// <summary>Großprojekte als Kanban-Tafel (Masterprompt 131, Runde 7): Die Tafel liegt als Datei im vorhandenen Trendo-Projekt (ProjectStore, kein eigenes Register),
/// Karten mit zuständiger KI-Rolle, Fortschritt und Belegen (Dateien werden ins Projekt kopiert und mit SHA-256 festgehalten).</summary>
public static class BigBoards
{
    public const string File = "grossprojekt/tafel.json";
    public static readonly string[] Columns = ["Offen", "Läuft", "Fertig"];
    public static readonly string[] Roles = ["Planung", "Text", "Design", "Medien", "Software", "3D", "Recht", "Verkauf", "Prüfung (QA)"];
    static readonly JsonSerializerOptions Opt = new() { WriteIndented = true };

    public static BigBoard Load(ProjectStore store, string projectId)
    {
        try { var f = store.ResolveInProject(projectId, File); return System.IO.File.Exists(f) ? JsonSerializer.Deserialize<BigBoard>(System.IO.File.ReadAllText(f)) ?? new() : new(); }
        catch (JsonException) { return new(); }
    }
    public static void Save(ProjectStore store, string projectId, BigBoard b) => store.WriteFile(projectId, File, JsonSerializer.Serialize(b, Opt));

    /// <summary>Neues Großprojekt im ProjectStore; mit Ablauf-Vorlage werden die Schritte zu Karten (Rolle = Studio des Schritts).</summary>
    public static (TrendoProject Project, BigBoard Board) Create(ProjectStore store, string name, string goal, Recipe? recipe = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Bitte einen Namen eingeben.");
        var baseId = new string(DevStudio.SafeName(name).ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        if (baseId.Length < 2) baseId = "projekt"; if (baseId.Length > 40) baseId = baseId[..40].Trim('-');
        var id = baseId; for (var n = 2; store.List().Any(p => p.Id == id); n++) id = $"{baseId}-{n}";
        var p = store.Create(id, name.Trim());
        var b = new BigBoard { Goal = goal.Trim() };
        if (recipe is not null) b.Cards.AddRange(recipe.Steps.Select(s => new BoardCard { Title = s.Label, Role = s.Studio, Detail = $"Aus dem Ablauf „{recipe.Label}“" }));
        Save(store, id, b);
        return (p, b);
    }

    /// <summary>Karte verschieben: Fertig setzt 100 %, Läuft beginnt mit mindestens 10 %, zurück nach Offen behält den Stand.</summary>
    public static void Move(BoardCard c, int column)
    {
        c.Column = Math.Clamp(column, 0, 2);
        if (c.Column == 2) c.Progress = 100;
        else if (c.Column == 1 && c.Progress == 0) c.Progress = 10;
        else if (c.Progress == 100) c.Progress = 90;
    }

    /// <summary>Fortschritt setzen; 100 % schiebt nach Fertig, unter 100 aus Fertig zurück nach Läuft.</summary>
    public static void SetProgress(BoardCard c, int value)
    {
        c.Progress = Math.Clamp(value, 0, 100);
        if (c.Progress == 100) c.Column = 2; else if (c.Column == 2) c.Column = 1; else if (c.Progress > 0 && c.Column == 0) c.Column = 1;
    }

    /// <summary>Beleg hinzufügen: Datei ins Projekt kopieren (grossprojekt/belege) und SHA-256 festhalten.</summary>
    public static BoardEvidence AddEvidence(ProjectStore store, string projectId, BoardCard c, string source, string note = "")
    {
        if (!System.IO.File.Exists(source)) throw new FileNotFoundException("Diese Datei gibt es nicht.");
        var bytes = System.IO.File.ReadAllBytes(source);
        var name = System.IO.Path.GetFileName(source); var rel = $"grossprojekt/belege/{c.Id}-{name}";
        store.WriteBytes(projectId, rel, bytes);
        var e = new BoardEvidence(rel, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), note.Trim(), DateTimeOffset.Now);
        c.Evidence.Add(e);
        return e;
    }

    /// <summary>Stimmt der Beleg noch mit der Prüfsumme überein?</summary>
    public static bool Intact(ProjectStore store, string projectId, BoardEvidence e)
    {
        try { var f = store.ResolveInProject(projectId, e.Path); return System.IO.File.Exists(f) && Convert.ToHexString(SHA256.HashData(System.IO.File.ReadAllBytes(f))).ToLowerInvariant() == e.Sha256; }
        catch (ProjectIsolationException) { return false; }
    }

    public static void Remove(BigBoard b, string cardId) { var c = b.Cards.FirstOrDefault(x => x.Id == cardId); if (c is null) return; b.Cards.Remove(c); b.Trash.Add(c); }
    public static void Restore(BigBoard b, string cardId) { var c = b.Trash.FirstOrDefault(x => x.Id == cardId); if (c is null) return; b.Trash.Remove(c); b.Cards.Add(c); }
}
