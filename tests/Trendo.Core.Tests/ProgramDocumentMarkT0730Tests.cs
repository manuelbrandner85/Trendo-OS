using System.Text.Json.Nodes;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0730: Marke (Remember/Rollback/Squash) auch bei vollem Rückgängig-Stapel (100 Schritte). Vorher galt die Stapelhöhe als Marke –
/// bei vollem Stapel bleibt sie durch das Kürzen gleich, Rollback entfernte nichts und Squash fasste nichts zusammen.</summary>
public class ProgramDocumentMarkT0730Tests
{
    static ProgramDocument Full()
    {
        var d = new ProgramDocument("SPEC-08", "Voll", new JsonObject { ["n"] = 0 });
        for (var i = 1; i <= ProgramDocument.MaxUndo + 5; i++) d.Set("n", i); // mehr als 100 Schritte → Stapel ist voll und wurde gekürzt
        Assert.Equal(ProgramDocument.MaxUndo, d.History.Count);
        return d;
    }

    static int N(ProgramDocument d) => (int)ProgramDocument.ToNumber(d.Data["n"], -1);

    [Fact(DisplayName = "T-0730: Rollback bei vollem Stapel entfernt genau die verworfenen KI-Schritte; Rückgängig bringt sie nie zurück")]
    public void Rollback_full_stack()
    {
        var d = Full(); var before = N(d); // 105
        var m = d.Remember();
        d.Set("n", 1001, "KI 1"); d.Set("n", 1002, "KI 2"); d.Set("n", 1003, "KI 3");
        Assert.Equal(ProgramDocument.MaxUndo, d.History.Count); // Höhe unverändert – genau der alte Fehlerfall
        d.Rollback(m);
        Assert.Equal(before, N(d));
        Assert.Equal(ProgramDocument.MaxUndo - 3, d.History.Count);
        Assert.DoesNotContain(d.History, l => l.StartsWith("KI", StringComparison.Ordinal));
        Assert.Equal("n", d.UndoLabel); // oben liegt der Schritt vor der Marke
        Assert.True(d.Undo()); Assert.Equal(before - 1, N(d)); // der erste Rückgängig-Schritt führt zum Stand vor der letzten echten Änderung
        while (d.Undo()) Assert.True(N(d) < 1000, "Rückgängig darf keinen verworfenen Stand liefern");
    }

    [Fact(DisplayName = "T-0730: Squash bei vollem Stapel macht aus den Schritten seit der Marke genau einen")]
    public void Squash_full_stack()
    {
        var d = Full(); var before = N(d);
        var m = d.Remember();
        d.Set("n", 2001, "Teil 1"); d.Set("n", 2002, "Teil 2"); d.Set("n", 2003, "Teil 3");
        Assert.True(d.Squash(m, "Auftrag"));
        Assert.Equal(2003, N(d)); // Daten bleiben
        Assert.Equal("Auftrag", d.UndoLabel);
        Assert.Equal(1, d.History.Count(l => l == "Auftrag"));
        Assert.DoesNotContain(d.History, l => l.StartsWith("Teil", StringComparison.Ordinal));
        Assert.Equal(ProgramDocument.MaxUndo - 2, d.History.Count); // 100 − 3 + 1
        Assert.True(d.Undo()); Assert.Equal(before, N(d)); // EIN Schritt zurück = Stand beim Merken
        Assert.False(d.Squash(d.Remember(), "leer")); // nichts seit der Marke → nichts zusammenzufassen
    }

    [Fact(DisplayName = "T-0730: Mehr als 100 Schritte seit der Marke – Rollback leert, Squash nimmt den gemerkten Stand als Stand davor")]
    public void Mark_trimmed_away()
    {
        var d = new ProgramDocument("SPEC-08", "Lang", new JsonObject { ["n"] = 0 });
        d.Set("n", 1); var m = d.Remember();
        for (var i = 0; i < ProgramDocument.MaxUndo + 10; i++) d.Set("n", 5000 + i, "KI");
        Assert.True(d.Squash(m, "Auftrag"));
        Assert.Equal(1, d.History.Count);
        Assert.True(d.Undo()); Assert.Equal(1, N(d));

        var e = new ProgramDocument("SPEC-08", "Lang", new JsonObject { ["n"] = 0 });
        e.Set("n", 1); var m2 = e.Remember();
        for (var i = 0; i < ProgramDocument.MaxUndo + 10; i++) e.Set("n", 5000 + i, "KI");
        e.Rollback(m2);
        Assert.Equal(1, N(e));
        Assert.False(e.CanUndo); // nichts Verworfenes bleibt übrig
    }

    [Fact(DisplayName = "T-0730: Bei nicht vollem Stapel verhalten sich Rollback und Squash wie bisher")]
    public void Small_stack_unchanged()
    {
        var d = new ProgramDocument("SPEC-08", "Klein", new JsonObject { ["n"] = 0 });
        d.Set("n", 1); var m = d.Remember();
        d.Set("n", 2); d.Set("n", 3);
        d.Rollback(m);
        Assert.Equal(1, N(d)); Assert.Single(d.History);
        d.Set("n", 4); d.Set("n", 5);
        Assert.True(d.Squash(d.Remember() with { Seq = 1 }, "Zusammen")); // Marke direkt nach dem ersten Schritt
        Assert.Equal(2, d.History.Count);
        Assert.True(d.Undo()); Assert.Equal(1, N(d));
    }
}
