using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// Großprojekte als Kanban: Tafel im vorhandenen Projekt, Karten aus Ablauf-Vorlage, Verschieben/Fortschritt, Belege mit Prüfsumme, Papierkorb.
public class BigBoardTests
{
    static ProjectStore Store() { var d = Path.Combine(Path.GetTempPath(), "trendo-board-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return new ProjectStore(d, new AuditLog(Path.Combine(d, "a.jsonl"))); }

    [Fact(DisplayName = "Großprojekt: liegt im ProjectStore, Karten aus der Ablauf-Vorlage mit Studio als Rolle, eindeutige Kennung, Tafel wird gespeichert und geladen")]
    public void Create_from_recipe()
    {
        var s = Store(); var r = Fusion.Recipes[0];
        var (p, b) = BigBoards.Create(s, "Hofladen Webseite", "Online bestellen", r);
        Assert.Equal("hofladen-webseite", p.Id); Assert.Contains(s.List(), x => x.Id == p.Id);
        Assert.Equal(r.Steps.Count, b.Cards.Count); Assert.Equal(r.Steps[0].Studio, b.Cards[0].Role); Assert.All(b.Cards, c => Assert.Equal(0, c.Column));
        var again = BigBoards.Load(s, p.Id); Assert.Equal("Online bestellen", again.Goal); Assert.Equal(b.Cards.Count, again.Cards.Count);
        Assert.Equal("hofladen-webseite-2", BigBoards.Create(s, "Hofladen Webseite", "").Project.Id);
        Assert.Throws<ArgumentException>(() => BigBoards.Create(s, " ", ""));
    }

    [Fact(DisplayName = "Großprojekt: Verschieben setzt den Fortschritt (Läuft ≥ 10 %, Fertig 100 %), Fortschritt verschiebt die Karte, Gesamtfortschritt")]
    public void Move_and_progress()
    {
        var b = new BigBoard { Cards = [new() { Title = "A" }, new() { Title = "B" }] };
        BigBoards.Move(b.Cards[0], 1); Assert.Equal(10, b.Cards[0].Progress);
        BigBoards.Move(b.Cards[0], 2); Assert.Equal(100, b.Cards[0].Progress);
        BigBoards.Move(b.Cards[0], 1); Assert.Equal(90, b.Cards[0].Progress);
        BigBoards.SetProgress(b.Cards[1], 40); Assert.Equal(1, b.Cards[1].Column);
        BigBoards.SetProgress(b.Cards[1], 100); Assert.Equal(2, b.Cards[1].Column);
        Assert.Equal(95, b.Percent);
        BigBoards.SetProgress(b.Cards[1], 150); Assert.Equal(100, b.Cards[1].Progress);
    }

    [Fact(DisplayName = "Großprojekt: Beleg wird ins Projekt kopiert, Prüfsumme erkennt Änderungen; Karte entfernen geht in den Papierkorb der Tafel")]
    public void Evidence_and_trash()
    {
        var s = Store(); var (p, b) = BigBoards.Create(s, "Messe", "");
        var c = new BoardCard { Title = "Flyer" }; b.Cards.Add(c);
        var src = Path.Combine(Path.GetTempPath(), $"trendo-beleg-{Guid.NewGuid():N}.txt"); File.WriteAllText(src, "Druckfreigabe");
        var e = BigBoards.AddEvidence(s, p.Id, c, src, "Freigabe vom Kunden");
        Assert.StartsWith("grossprojekt/belege/", e.Path); Assert.Equal(64, e.Sha256.Length);
        Assert.True(BigBoards.Intact(s, p.Id, e));
        File.WriteAllText(s.ResolveInProject(p.Id, e.Path), "geändert"); Assert.False(BigBoards.Intact(s, p.Id, e));
        Assert.Throws<FileNotFoundException>(() => BigBoards.AddEvidence(s, p.Id, c, src + ".fehlt"));
        BigBoards.Remove(b, c.Id); Assert.Empty(b.Cards); Assert.Single(b.Trash);
        BigBoards.Restore(b, c.Id); Assert.Single(b.Cards); Assert.Empty(b.Trash);
        File.Delete(src);
    }
}
