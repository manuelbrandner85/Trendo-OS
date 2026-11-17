using System.Security.Cryptography;
using System.Text;

namespace Trendo.Core;

/// <summary>T-0743 (Audit X-01, Rang 18): Karten eines Großprojekts tun etwas. Eine Karte übergibt ihren Auftrag an ein Studio, das Ergebnis kommt als Datei zurück.
/// Kein zweites Register: die Tafel liegt im <see cref="ProjectStore"/>, beide Richtungen laufen über die vorhandenen Übergaben (<see cref="ProjectHandoff"/>,
/// <c>.trendo/uebergaben.json</c> im Projekt) – hin als Art „auftrag“, zurück als Art „ergebnis“, mit SHA-256.</summary>
public static class BigBoardHandoff
{
    /// <summary>Kennung der Großprojekte als Absender bzw. Empfänger der Übergaben.</summary>
    public const string Board = "X-01";
    public const string Open = "", Handed = "uebergeben", Done = "erledigt";

    public static ProjectHandoff Handoffs(ProjectStore store, string projectId) => new(store.ProjectFilesRoot(projectId));

    /// <summary>Eingabe der Karte: das Ergebnis der nächsten Karte davor (Reihenfolge der Tafel = Reihenfolge der Kette), die schon eins hat. Sonst null.</summary>
    public static string? InputFor(ProjectStore store, string projectId, BigBoard b, BoardCard c)
    {
        var i = b.Cards.FindIndex(x => x.Id == c.Id);
        for (var k = i - 1; k >= 0; k--)
            if (b.Cards[k].Result is { Length: > 0 } r)
            {
                var full = store.ResolveInProject(projectId, r);
                if (File.Exists(full)) return full;
            }
        return null;
    }

    /// <summary>Karte in der Kette verschieben (−1 nach vorn, +1 nach hinten). false = geht nicht weiter.</summary>
    public static bool Reorder(BigBoard b, string cardId, int delta)
    {
        var i = b.Cards.FindIndex(x => x.Id == cardId); var j = i + delta;
        if (i < 0 || j < 0 || j >= b.Cards.Count) return false;
        (b.Cards[i], b.Cards[j]) = (b.Cards[j], b.Cards[i]);
        return true;
    }

    /// <summary>Auftrag schreiben (<c>grossprojekt/auftraege/&lt;Karte&gt;.md</c>) und als Übergabe an das Studio eintragen. Die Karte steht danach auf „Läuft“.</summary>
    public static HandoffItem HandOver(ProjectStore store, string projectId, BigBoard b, BoardCard c, string studio, string programTitle)
    {
        if (string.IsNullOrWhiteSpace(studio)) throw new ArgumentException("Bitte zuerst ein Studio für die Karte wählen.");
        if (c.Status == Done) throw new InvalidOperationException("Diese Karte ist schon erledigt.");
        var input = InputFor(store, projectId, b, c);
        var md = new StringBuilder()
            .AppendLine("# Auftrag: " + c.Title).AppendLine()
            .AppendLine("- Großprojekt-Ziel: " + (b.Goal.Length > 0 ? b.Goal : "–"))
            .AppendLine("- Studio: " + programTitle)
            .AppendLine("- Eingabe: " + (input is null ? "keine" : Path.GetRelativePath(store.ProjectFilesRoot(projectId), input).Replace('\\', '/')))
            .AppendLine().AppendLine("## Was ist zu tun?").AppendLine().AppendLine(c.Detail.Length > 0 ? c.Detail : c.Title)
            .AppendLine().AppendLine("Ergebnis: im Studio exportieren – die Datei kommt von selbst an diese Karte zurück.").ToString();
        var rel = $"grossprojekt/auftraege/{c.Id}.md";
        store.WriteFile(projectId, rel, md.Replace("\r\n", "\n"));
        var item = Handoffs(store, projectId).Add(Board, studio, rel, "auftrag", c.Title);
        c.Studio = studio; c.Handoff = item.Id; c.Status = Handed; c.Order = rel;
        BigBoards.Move(c, 1);
        return item;
    }

    /// <summary>Ergebnis zurück an die Karte: Datei ins Projekt kopieren (Beleg mit SHA-256), als Übergabe „ergebnis“ an die Großprojekte eintragen und gleich annehmen,
    /// den Auftrag als angenommen markieren, Karte „erledigt“ und in „Fertig“.</summary>
    public static BoardEvidence Return(ProjectStore store, string projectId, BoardCard c, string studio, string file, string? studioName = null)
    {
        if (!File.Exists(file)) throw new FileNotFoundException("Die Ergebnisdatei gibt es nicht.");
        var ev = BigBoards.AddEvidence(store, projectId, c, file, "Ergebnis aus " + (studioName ?? studio));
        var h = Handoffs(store, projectId);
        if (c.Handoff is { } order && h.OpenFor(studio).Any(x => x.Id == order))
            try { h.Take(order, studio); } catch (ArgumentException) { /* Auftrag inzwischen geändert – das Ergebnis zählt trotzdem */ }
        var back = h.Add(studio, Board, ev.Path, "ergebnis", c.Title);
        h.Take(back.Id, Board);
        c.Result = ev.Path; c.ResultSha = ev.Sha256; c.Status = Done;
        BigBoards.Move(c, 2);
        return ev;
    }

    /// <summary>Stimmt das Ergebnis der Karte noch mit der Prüfsumme überein?</summary>
    public static bool ResultIntact(ProjectStore store, string projectId, BoardCard c)
    {
        if (c.Result is null || c.ResultSha is null) return false;
        try { var f = store.ResolveInProject(projectId, c.Result); return File.Exists(f) && Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))).Equals(c.ResultSha, StringComparison.OrdinalIgnoreCase); }
        catch (ProjectIsolationException) { return false; }
    }
}
