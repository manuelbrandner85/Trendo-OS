using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// T-0743: Karte → Studio → Ergebnis über die vorhandenen Übergaben (ProjectHandoff) im ProjectStore; Kette = Reihenfolge der Karten.
public class BigBoardHandoffTests
{
    static ProjectStore Store() { var d = Path.Combine(Path.GetTempPath(), "trendo-board-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return new ProjectStore(d, new AuditLog(Path.Combine(d, "a.jsonl"))); }

    [Fact(DisplayName = "Großprojekt-Karte: Übergabe schreibt den Auftrag ins Projekt und trägt ihn als Übergabe ein, das Ergebnis kommt mit Prüfsumme zurück, beide Übergaben sind angenommen")]
    public void Handover_and_return()
    {
        var s = Store(); var (p, b) = BigBoards.Create(s, "Hofladen", "Flyer für den Markt");
        var c = new BoardCard { Title = "Flyer-Text", Detail = "Kurzer Text mit Öffnungszeiten" }; b.Cards.Add(c);
        var item = BigBoardHandoff.HandOver(s, p.Id, b, c, "STUDIO-BOOK-01", "Bücher schreiben");
        Assert.Equal((BigBoardHandoff.Handed, 1), (c.Status, c.Column));
        var order = s.ReadFile(p.Id, c.Order!);
        Assert.Contains("Kurzer Text mit Öffnungszeiten", order); Assert.Contains("Flyer für den Markt", order);
        var h = BigBoardHandoff.Handoffs(s, p.Id);
        Assert.Equal(item.Id, Assert.Single(h.OpenFor("STUDIO-BOOK-01")).Id);

        var file = Path.Combine(Path.GetTempPath(), "trendo-board-ergebnis-" + Guid.NewGuid().ToString("N")[..6] + ".txt"); File.WriteAllText(file, "Hofladen Muster – Mo bis Sa");
        var ev = BigBoardHandoff.Return(s, p.Id, c, "STUDIO-BOOK-01", file);
        Assert.Equal((BigBoardHandoff.Done, 2, 100), (c.Status, c.Column, c.Progress));
        Assert.Equal(ev.Path, c.Result); Assert.True(BigBoardHandoff.ResultIntact(s, p.Id, c));
        Assert.Empty(h.OpenFor(null)); // Auftrag und Ergebnis sind angenommen
        Assert.Contains(h.All(), x => x.Kind == "ergebnis" && x.From == "STUDIO-BOOK-01" && x.To == BigBoardHandoff.Board && x.Received is not null);
        File.AppendAllText(s.ResolveInProject(p.Id, c.Result!), "!");
        Assert.False(BigBoardHandoff.ResultIntact(s, p.Id, c));
    }

    [Fact(DisplayName = "Großprojekt-Kette: Eingabe ist das Ergebnis der Karte davor, Umsortieren ändert die Kette, ohne Studio und doppelt erledigt wird abgelehnt")]
    public void Chain_input_and_reorder()
    {
        var s = Store(); var (p, b) = BigBoards.Create(s, "Kette", "");
        var a = new BoardCard { Title = "A" }; var z = new BoardCard { Title = "Z" }; b.Cards.AddRange([a, z]);
        Assert.Null(BigBoardHandoff.InputFor(s, p.Id, b, z));
        var f = Path.Combine(Path.GetTempPath(), "trendo-board-a-" + Guid.NewGuid().ToString("N")[..6] + ".pdf"); File.WriteAllBytes(f, [1, 2, 3]);
        BigBoardHandoff.HandOver(s, p.Id, b, a, "S1", "Studio 1"); BigBoardHandoff.Return(s, p.Id, a, "S1", f);
        Assert.Equal(s.ResolveInProject(p.Id, a.Result!), BigBoardHandoff.InputFor(s, p.Id, b, z));
        Assert.True(BigBoardHandoff.Reorder(b, z.Id, -1)); Assert.Equal(["Z", "A"], b.Cards.Select(x => x.Title));
        Assert.Null(BigBoardHandoff.InputFor(s, p.Id, b, z));
        Assert.False(BigBoardHandoff.Reorder(b, z.Id, -1));
        Assert.Throws<ArgumentException>(() => BigBoardHandoff.HandOver(s, p.Id, b, z, " ", "x"));
        Assert.Throws<InvalidOperationException>(() => BigBoardHandoff.HandOver(s, p.Id, b, a, "S1", "Studio 1"));
    }

    static BigBoard Clone(BigBoard b) => System.Text.Json.JsonSerializer.Deserialize<BigBoard>(System.Text.Json.JsonSerializer.Serialize(b))!;

    [Fact(DisplayName = "Gegenprüfung 1: Abgleich beim Rückgängig nimmt Übergaben und Dateien der Tafel zurück und bringt sie beim Wiederholen wieder – fremde Übergaben bleiben")]
    public void Reconcile_undo_redo()
    {
        var s = Store(); var (p, b) = BigBoards.Create(s, "Abgleich", "");
        var c = new BoardCard { Title = "K" }; b.Cards.Add(c);
        s.WriteFile(p.Id, "fremd.txt", "x"); var h = BigBoardHandoff.Handoffs(s, p.Id); var foreign = h.Add("STUDIO-A", "STUDIO-B", "fremd.txt", "datei");
        var s0 = Clone(b);
        BigBoardHandoff.HandOver(s, p.Id, b, c, "S1", "Studio 1"); var s1 = Clone(b);
        var f = Path.Combine(Path.GetTempPath(), "trendo-board-r-" + Guid.NewGuid().ToString("N")[..6] + ".txt"); File.WriteAllText(f, "Ergebnis");
        BigBoardHandoff.Return(s, p.Id, c, "S1", f); var s2 = Clone(b);
        Assert.Equal(1, BigBoardHandoff.Versions(c));

        BigBoardHandoff.Reconcile(s, p.Id, s2, s1); // Ergebnis zurück
        Assert.Equal(new[] { c.Handoff!, foreign.Id }.Order(), h.All().Select(x => x.Id).Order());
        Assert.Null(h.All().Single(x => x.Id == c.Handoff).Received);
        Assert.False(File.Exists(s.ResolveInProject(p.Id, c.Result!)));
        Assert.True(File.Exists(s.ResolveInProject(p.Id, BigBoardHandoff.Aside(c.Result!))));

        BigBoardHandoff.Reconcile(s, p.Id, s1, s0); // Übergabe zurück
        Assert.Equal(foreign.Id, Assert.Single(h.All()).Id);
        Assert.False(File.Exists(s.ResolveInProject(p.Id, c.Order!)));

        BigBoardHandoff.Reconcile(s, p.Id, s0, s1); BigBoardHandoff.Reconcile(s, p.Id, s1, s2); // wiederholen
        Assert.Equal(3, h.All().Count); Assert.All(h.All().Where(x => x.Id != foreign.Id), x => Assert.NotNull(x.Received));
        Assert.True(BigBoardHandoff.ResultIntact(s, p.Id, s2.Cards[0]));
        Assert.True(File.Exists(s.ResolveInProject(p.Id, c.Order!)));
    }

    [Fact(DisplayName = "Gegenprüfung 3: erledigte Karte nimmt eine neue Fassung an (alte bleibt Beleg), wieder öffnen gibt einen neuen Auftrag")]
    public void New_version_and_reopen()
    {
        var s = Store(); var (p, b) = BigBoards.Create(s, "Fassung", "");
        var c = new BoardCard { Title = "K" }; b.Cards.Add(c);
        BigBoardHandoff.HandOver(s, p.Id, b, c, "S1", "Studio 1");
        var f = Path.Combine(Path.GetTempPath(), "trendo-board-v-" + Guid.NewGuid().ToString("N")[..6] + ".txt");
        File.WriteAllText(f, "eins"); var e1 = BigBoardHandoff.Return(s, p.Id, c, "S1", f);
        File.WriteAllText(f, "zwei"); var e2 = BigBoardHandoff.Return(s, p.Id, c, "S1", f);
        Assert.NotEqual(e1.Path, e2.Path); Assert.Equal(e2.Path, c.Result); Assert.Equal(2, BigBoardHandoff.Versions(c));
        Assert.Equal("eins", s.ReadFile(p.Id, e1.Path)); Assert.Contains("Fassung 2", e2.Note);
        var first = c.Handoff;
        var again = BigBoardHandoff.Reopen(s, p.Id, b, c, "S1", "Studio 1");
        Assert.Equal((BigBoardHandoff.Handed, again.Id), (c.Status, c.Handoff));
        Assert.Equal([first!], c.Earlier);
        Assert.Throws<InvalidOperationException>(() => BigBoardHandoff.Reopen(s, p.Id, b, c, "S1", "Studio 1"));
        Assert.Equal(again.Id, Assert.Single(BigBoardHandoff.Handoffs(s, p.Id).OpenFor("S1")).Id);
    }

    [Fact(DisplayName = "Gegenprüfung 4: zwei Belege mit gleichem Dateinamen überschreiben sich nicht")]
    public void Evidence_with_same_name_gets_unique_path()
    {
        var s = Store(); var (p, b) = BigBoards.Create(s, "Belege", "");
        var c = new BoardCard { Title = "K" }; b.Cards.Add(c);
        var d1 = Path.Combine(Path.GetTempPath(), "trendo-ev1-" + Guid.NewGuid().ToString("N")[..6]); var d2 = Path.Combine(Path.GetTempPath(), "trendo-ev2-" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(d1); Directory.CreateDirectory(d2);
        File.WriteAllText(Path.Combine(d1, "foto.txt"), "erste"); File.WriteAllText(Path.Combine(d2, "foto.txt"), "zweite");
        var e1 = BigBoards.AddEvidence(s, p.Id, c, Path.Combine(d1, "foto.txt"));
        var e2 = BigBoards.AddEvidence(s, p.Id, c, Path.Combine(d2, "foto.txt"));
        var e3 = BigBoards.AddEvidence(s, p.Id, c, Path.Combine(d1, "foto.txt"));
        Assert.Equal(3, new[] { e1.Path, e2.Path, e3.Path }.Distinct().Count());
        Assert.All(c.Evidence, e => Assert.True(BigBoards.Intact(s, p.Id, e)));
        Assert.Equal("erste", s.ReadFile(p.Id, e1.Path)); Assert.Equal("zweite", s.ReadFile(p.Id, e2.Path));
        Assert.EndsWith(".txt", e2.Path);
    }

    [Fact(DisplayName = "Großprojekt-Karte: alte Tafeln ohne die neuen Felder bleiben lesbar")]
    public void Old_board_reads()
    {
        var s = Store(); var (p, _) = BigBoards.Create(s, "Alt", "");
        s.WriteFile(p.Id, BigBoards.File, "{\"Goal\":\"x\",\"Cards\":[{\"Id\":\"ab12cd34\",\"Title\":\"T\",\"Column\":1,\"Role\":\"Text\",\"Progress\":20,\"Evidence\":[]}],\"Trash\":[]}");
        var c = Assert.Single(BigBoards.Load(s, p.Id).Cards);
        Assert.Equal(("T", BigBoardHandoff.Open, (string?)null), (c.Title, c.Status, c.Program));
    }
}
