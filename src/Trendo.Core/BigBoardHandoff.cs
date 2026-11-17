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
    /// den Auftrag als angenommen markieren, Karte „erledigt“ und in „Fertig“. Ist die Karte schon erledigt, wird die Datei die neue Fassung des Ergebnisses –
    /// die alte bleibt als Beleg an der Karte.</summary>
    public static BoardEvidence Return(ProjectStore store, string projectId, BoardCard c, string studio, string file, string? studioName = null)
    {
        if (!File.Exists(file)) throw new FileNotFoundException("Die Ergebnisdatei gibt es nicht.");
        var version = Versions(c) + 1;
        var ev = BigBoards.AddEvidence(store, projectId, c, file, "Ergebnis aus " + (studioName ?? studio) + (version > 1 ? $", Fassung {version}" : ""));
        var h = Handoffs(store, projectId);
        if (c.Handoff is { } order && h.OpenFor(studio).Any(x => x.Id == order))
            try { h.Take(order, studio); } catch (ArgumentException) { /* Auftrag inzwischen geändert – das Ergebnis zählt trotzdem */ }
        var back = h.Add(studio, Board, ev.Path, "ergebnis", c.Title);
        h.Take(back.Id, Board);
        ev = ev with { Handoff = back.Id }; c.Evidence[^1] = ev;
        c.Result = ev.Path; c.ResultSha = ev.Sha256; c.Status = Done;
        BigBoards.Move(c, 2);
        return ev;
    }

    /// <summary>Wie viele Fassungen des Ergebnisses sind an die Karte zurückgekommen?</summary>
    public static int Versions(BoardCard c) => c.Evidence.Count(e => e.Handoff is not null);

    /// <summary>Erledigte Karte wieder öffnen: neuer Auftrag an dasselbe Studio, der alte bleibt (als angenommen) in <see cref="BoardCard.Earlier"/>,
    /// das bisherige Ergebnis bleibt an der Karte (und Eingabe der Karte danach). Der nächste Export kommt als neue Fassung zurück.</summary>
    public static HandoffItem Reopen(ProjectStore store, string projectId, BigBoard b, BoardCard c, string studio, string programTitle)
    {
        if (c.Status != Done) throw new InvalidOperationException("Diese Karte ist noch nicht erledigt.");
        if (c.Handoff is { } old) c.Earlier.Add(old);
        c.Status = Open;
        return HandOver(store, projectId, b, c, studio, programTitle);
    }

    // ───── T-0743 Gegenprüfung: Rückgängig/Wiederholen der Tafel hält Register und Dateien passend ─────

    /// <summary>Ablage für Zurückgenommenes (Dateien und Übergaben), damit Wiederholen sie zurückbringen kann.</summary>
    public const string AsideFolder = "grossprojekt/zurueckgenommen";
    const string AsideRegister = AsideFolder + "/uebergaben.json";

    /// <summary>Pfad im Projekt, an dem eine zurückgenommene Datei liegt (<c>grossprojekt/x/y</c> → <c>grossprojekt/zurueckgenommen/x/y</c>).</summary>
    public static string Aside(string rel) => AsideFolder + "/" + (rel.StartsWith("grossprojekt/", StringComparison.Ordinal) ? rel["grossprojekt/".Length..] : rel);

    static IEnumerable<BoardCard> AllCards(BigBoard b) => b.Cards.Concat(b.Trash);

    /// <summary>Übergaben, die eine Tafel festhält, mit dem Stand, den sie haben müssen (true = angenommen).</summary>
    public static Dictionary<string, bool> Referenced(BigBoard b)
    {
        var d = new Dictionary<string, bool>();
        foreach (var c in AllCards(b))
        {
            if (c.Handoff is { } h) d[h] = c.Status == Done;
            foreach (var e in c.Earlier) d[e] = true;
            foreach (var e in c.Evidence) if (e.Handoff is { } eh) d[eh] = true;
        }
        return d;
    }

    /// <summary>Dateien der Tafel im Projekt (Aufträge, vorbereitete Dokumente, Ergebnisse, Belege).</summary>
    public static HashSet<string> Files(BigBoard b) =>
        [.. AllCards(b).SelectMany(c => new[] { c.Order, c.Doc, c.Result }.Concat(c.Evidence.Select(e => e.Path))).OfType<string>()];

    /// <summary>Rückgängig/Wiederholen der Tafel: Stand <paramref name="from"/> → <paramref name="to"/>. Was nur <paramref name="from"/> kennt, wird zurückgenommen
    /// (Übergaben aus <c>uebergaben.json</c> in die Ablage, Dateien nach <see cref="AsideFolder"/>); was <paramref name="to"/> braucht, kommt zurück, und jede Übergabe der
    /// Tafel bekommt den passenden Stand (Auftrag offen bzw. angenommen). Danach stimmen Tafel, Register und Dateien überein. Fremde Übergaben bleiben unberührt.</summary>
    public static void Reconcile(ProjectStore store, string projectId, BigBoard from, BigBoard to)
    {
        var want = Referenced(to); var had = Referenced(from);
        var aside = LoadAside(store, projectId);
        Handoffs(store, projectId).Rewrite(all =>
        {
            var list = all.ToList();
            foreach (var id in had.Keys.Where(k => !want.ContainsKey(k)))
                if (list.FindIndex(x => x.Id == id) is var i and >= 0) { aside.RemoveAll(x => x.Id == id); aside.Add(list[i]); list.RemoveAt(i); }
            foreach (var (id, received) in want)
            {
                var i = list.FindIndex(x => x.Id == id);
                if (i < 0 && aside.FirstOrDefault(x => x.Id == id) is { } back) { list.Add(back); aside.Remove(back); i = list.Count - 1; }
                if (i < 0) continue;
                if (received && list[i].Received is null) list[i] = list[i] with { Received = DateTimeOffset.Now };
                else if (!received && list[i].Received is not null) list[i] = list[i] with { Received = null };
            }
            return list;
        });
        store.WriteFile(projectId, AsideRegister, System.Text.Json.JsonSerializer.Serialize(aside));

        var need = Files(to);
        foreach (var rel in Files(from).Where(f => !need.Contains(f))) MoveIn(store, projectId, rel, Aside(rel));
        foreach (var rel in need) MoveIn(store, projectId, Aside(rel), rel, onlyIfMissing: true);
    }

    static List<HandoffItem> LoadAside(ProjectStore store, string projectId)
    {
        try { var f = store.ResolveInProject(projectId, AsideRegister); return File.Exists(f) ? System.Text.Json.JsonSerializer.Deserialize<List<HandoffItem>>(File.ReadAllText(f)) ?? [] : []; }
        catch (System.Text.Json.JsonException) { return []; }
    }

    /// <summary>Datei im Projekt verschieben (beide Pfade über die Projekt-Isolation). Pfade außerhalb des Projekts werden übergangen.</summary>
    static void MoveIn(ProjectStore store, string projectId, string fromRel, string toRel, bool onlyIfMissing = false)
    {
        string src, dst;
        try { src = store.ResolveInProject(projectId, fromRel); dst = store.ResolveInProject(projectId, toRel); }
        catch (ProjectIsolationException) { return; }
        if (!File.Exists(src) || onlyIfMissing && File.Exists(dst)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
        File.Move(src, dst, overwrite: true);
    }

    /// <summary>Stimmt das Ergebnis der Karte noch mit der Prüfsumme überein?</summary>
    public static bool ResultIntact(ProjectStore store, string projectId, BoardCard c)
    {
        if (c.Result is null || c.ResultSha is null) return false;
        try { var f = store.ResolveInProject(projectId, c.Result); return File.Exists(f) && Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))).Equals(c.ResultSha, StringComparison.OrdinalIgnoreCase); }
        catch (ProjectIsolationException) { return false; }
    }
}
