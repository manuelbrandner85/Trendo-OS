using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// Notiz-Wiki: Verlinkungen, Rückverweise, fehlende Seiten, Umbenennen passt Verweise an, Papierkorb statt endgültig löschen.
public class KnowledgeWikiTests
{
    static KnowledgeStore Store() { var d = Path.Combine(Path.GetTempPath(), "trendo-wiki-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return new KnowledgeStore(d, new AuditLog(Path.Combine(d, "a.jsonl"))); }

    [Fact(DisplayName = "Notiz-Wiki: [[Links]] und [[Titel|Anzeige]] erkennen, Text in Stücke zerlegen, Rückverweise und fehlende Seiten")]
    public void Links_and_backlinks()
    {
        Assert.Equal(["Garten", "Rasen"], KnowledgeStore.Links("Im [[Garten]] wächst [[Rasen|der Rasen]], siehe [[garten]]."));
        var seg = KnowledgeStore.Segments("Im [[Garten]] wächst [[Rasen|der Rasen]].");
        Assert.Equal([("Im ", null), ("Garten", "Garten"), (" wächst ", null), ("der Rasen", "Rasen"), (".", null)], seg);
        var s = Store();
        var g = s.Add("Garten", "Mein Garten hat einen [[Rasen]] und einen [[Teich]].");
        s.Add("Rasen", "Rasen mähen jeden Samstag. Zurück zum [[garten]].");
        s.Add("Werkzeug", "Rasenmäher steht im Schuppen, siehe [[Rasen]].");
        Assert.Equal(["Garten", "Werkzeug"], s.Backlinks("Rasen").Select(i => i.Title));
        Assert.Equal(["Rasen"], s.Backlinks("Garten").Select(i => i.Title));
        Assert.Equal(["Teich"], s.Missing(g));
        Assert.Equal(g.Id, s.ByTitle("garten")!.Id);
    }

    [Fact(DisplayName = "Notiz-Wiki: Seite ändern, Umbenennen passt Verweise anderer Seiten an (Anzeige bleibt), doppelter Titel wird abgelehnt")]
    public void Update_and_rename()
    {
        var s = Store();
        var r = s.Add("Rasen", "Samstags mähen.");
        var g = s.Add("Garten", "Mit [[Rasen]] und [[rasen|Grünfläche]].");
        s.Add("Teich", "Fische füttern.");
        var u = s.Update(r.Id, "Wiese", "Samstags mähen, im Herbst vertikutieren.");
        Assert.Equal("Wiese", u.Title); Assert.Contains("vertikutieren", s.Get(r.Id)!.Text);
        Assert.Equal("Mit [[Wiese]] und [[Wiese|Grünfläche]].", s.Get(g.Id)!.Text);
        Assert.Throws<ArgumentException>(() => s.Update(r.Id, "Teich", "x"));
        Assert.Throws<ArgumentException>(() => s.Update(r.Id, "Wiese", " "));
        Assert.Throws<KeyNotFoundException>(() => s.Update("fehlt", "a", "b"));
    }

    [Fact(DisplayName = "Notiz-Wiki: Löschen legt die Seite in den Papierkorb des Wissens, Wiederherstellen holt sie zurück – auch nach einem Neustart")]
    public void Trash_and_restore()
    {
        var d = Path.Combine(Path.GetTempPath(), "trendo-wiki-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d);
        var audit = new AuditLog(Path.Combine(d, "a.jsonl"));
        var s = new KnowledgeStore(d, audit); var it = s.Add("Notiz", "Wichtig.");
        Assert.True(s.Trash(it.Id)); Assert.Empty(s.All()); Assert.Single(s.Trashed());
        var again = new KnowledgeStore(d, audit); Assert.Single(again.Trashed());
        Assert.True(again.Restore(it.Id)); Assert.Single(again.All()); Assert.Empty(again.Trashed());
        Assert.False(again.Trash("fehlt")); Assert.False(again.Restore("fehlt"));
    }
}
