using System.Text.Json.Nodes;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0231 V3: Projektdatei, Rückgängig/Wiederholen, Sichern, Zuletzt.</summary>
public class ProgramDocumentTests
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-prog-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "V3: Änderungen rückgängig und wiederholen; keine Änderung = kein Schritt")]
    public void Undo_redo()
    {
        var d = new ProgramDocument("SPEC-08", "Shirt", new JsonObject { ["name"] = "A" });
        Assert.False(d.CanUndo); Assert.False(d.Dirty);
        d.Set("name", "B"); d.Set("name", "C");
        d.Set("name", "C"); // gleich → kein Schritt
        Assert.Equal("C", d.Text("name")); Assert.True(d.Dirty);
        Assert.True(d.Undo()); Assert.Equal("B", d.Text("name"));
        Assert.True(d.Undo()); Assert.Equal("A", d.Text("name"));
        Assert.False(d.Undo());
        Assert.True(d.Redo()); Assert.Equal("B", d.Text("name"));
        d.Set("price", 12.5); Assert.False(d.CanRedo); // neue Änderung verwirft Wiederholen
        Assert.Equal(12.5, d.Number("price"));
    }

    [Fact(DisplayName = "V3: Speichern und Öffnen; falsches Studio und kaputte Datei werden abgelehnt; kein Programmname in der Datei")]
    public void Save_load()
    {
        var dir = Tmp();
        try
        {
            var d = new ProgramDocument("SPEC-08", "Sommerkleid"); d.Set("sizes", new JsonArray("S", "M"));
            d.Save(Path.Combine(dir, "kleid"));
            Assert.Equal(Path.Combine(dir, "kleid.tdoc"), d.Path); Assert.False(d.Dirty);
            var text = File.ReadAllText(d.Path!);
            Assert.DoesNotContain("Trendo", text, StringComparison.OrdinalIgnoreCase);
            var e = ProgramDocument.Load(d.Path!, "SPEC-08");
            Assert.Equal(("Sommerkleid", 2), (e.Title, e.List("sizes").Count));
            Assert.Throws<InvalidDataException>(() => ProgramDocument.Load(d.Path!, "CORE-05"));
            File.WriteAllText(Path.Combine(dir, "x.tdoc"), "kein json");
            Assert.Throws<InvalidDataException>(() => ProgramDocument.Load(Path.Combine(dir, "x.tdoc")));
            Assert.Throws<InvalidOperationException>(() => new ProgramDocument("A", "B").Save());
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "V3: Automatisches Sichern – neben der Datei bzw. im Sicherungsordner, Wiederherstellen, Speichern räumt auf")]
    public void Autosave_restore()
    {
        var dir = Tmp();
        try
        {
            var backup = Path.Combine(dir, "sicherung");
            var d = new ProgramDocument("SPEC-08", "Neu");
            Assert.Null(d.Autosave(backup)); // nichts geändert
            d.Set("name", "Entwurf");
            var f = d.Autosave(backup)!;
            Assert.Single(ProgramDocument.Orphans(backup, "SPEC-08"));
            var r = ProgramDocument.Restore(f);
            Assert.Equal("Entwurf", r.Text("name")); Assert.True(r.Dirty); Assert.Null(r.Path);
            d.Save(Path.Combine(dir, "a.tdoc")); d.Set("name", "weiter");
            var side = d.Autosave(backup)!;
            Assert.Equal(Path.Combine(dir, "a.tdoc.autosave"), side);
            var r2 = ProgramDocument.Restore(side);
            Assert.Equal(Path.Combine(dir, "a.tdoc"), r2.Path);
            d.Save(); Assert.False(File.Exists(side));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "V3: Zuletzt geöffnet je Studio, neueste zuerst, nur vorhandene Dateien")]
    public void Recent()
    {
        var dir = Tmp();
        try
        {
            var r = new ProgramRecent(dir);
            var a = Path.Combine(dir, "a.tdoc"); var b = Path.Combine(dir, "b.tdoc"); File.WriteAllText(a, "{}"); File.WriteAllText(b, "{}");
            r.Add("SPEC-08", a); r.Add("SPEC-08", b); r.Add("SPEC-08", a); r.Add("CORE-05", b);
            Assert.Equal([a, b], r.Get("SPEC-08"));
            File.Delete(b); Assert.Equal([a], r.Get("SPEC-08"));
        }
        finally { Directory.Delete(dir, true); }
    }
}
