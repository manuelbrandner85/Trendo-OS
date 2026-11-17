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

    [Fact(DisplayName = "Großprojekt-Karte: alte Tafeln ohne die neuen Felder bleiben lesbar")]
    public void Old_board_reads()
    {
        var s = Store(); var (p, _) = BigBoards.Create(s, "Alt", "");
        s.WriteFile(p.Id, BigBoards.File, "{\"Goal\":\"x\",\"Cards\":[{\"Id\":\"ab12cd34\",\"Title\":\"T\",\"Column\":1,\"Role\":\"Text\",\"Progress\":20,\"Evidence\":[]}],\"Trash\":[]}");
        var c = Assert.Single(BigBoards.Load(s, p.Id).Cards);
        Assert.Equal(("T", BigBoardHandoff.Open, (string?)null), (c.Title, c.Status, c.Program));
    }
}
