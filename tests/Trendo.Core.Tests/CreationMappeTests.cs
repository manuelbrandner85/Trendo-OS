using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// Neues Projekt als Projektmappe: Übersicht (Spalten, überfällig, nächster Schritt, Zieltermin), Mappen-Baum, Umbenennen.
public class CreationMappeTests
{
    static string Root() { var r = Path.Combine(Path.GetTempPath(), "trendo-mappe-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(r); return r; }

    [Fact(DisplayName = "Projektmappe: Übersicht zählt Offen/In Arbeit/Fertig, überfällige Schritte, nächsten Schritt (In Arbeit zuerst), Zieltermin und Resttage")]
    public void Summary()
    {
        var root = Root(); var start = new DateTime(2026, 11, 1);
        var d = CreationSuite.Create(root, "event", "Gartenfest", start);
        var s = CreationSuite.Steps(d);
        CreationSuite.SetState(d, s[0].Line, 2); CreationSuite.SetState(d, s[2].Line, 1);
        var today = start.AddDays(12); // Schritt 2 (fällig nach 5 Tagen) ist überfällig
        var x = CreationSuite.Summary(d, today);
        Assert.Equal("Gartenfest", x.Name); Assert.Equal("Alles für einen gelungenen Termin", x.Goal); Assert.Equal("Feier oder Treffen planen", x.Kind);
        Assert.Equal((4, 1, 1), (x.Open, x.InWork, x.Done));
        Assert.Equal(1, x.Overdue);
        Assert.Equal(s[2].Text, x.Next!.Text); // In Arbeit geht vor
        Assert.Equal(start.AddDays(30), x.End); Assert.Equal(18, x.DaysLeft);
        Assert.Equal(0, x.Files);
    }

    [Fact(DisplayName = "Projektmappe: Baum zeigt Ordner zuerst mit Dateizahl, dann Dateien; Schrittliste, versteckte Ordner und node_modules fehlen")]
    public void Tree()
    {
        var root = Root(); var d = CreationSuite.Create(root, "video", "Urlaub");
        File.WriteAllText(Path.Combine(d, "Aufnahmen", "a.mp4"), "x");
        Directory.CreateDirectory(Path.Combine(d, "Aufnahmen", "Tag 1")); File.WriteAllText(Path.Combine(d, "Aufnahmen", "Tag 1", "b.mp4"), "x");
        File.WriteAllText(Path.Combine(d, "notiz.txt"), "x");
        Directory.CreateDirectory(Path.Combine(d, ".git")); File.WriteAllText(Path.Combine(d, ".git", "HEAD"), "x");
        Directory.CreateDirectory(Path.Combine(d, "node_modules"));
        var t = CreationSuite.Tree(d);
        Assert.DoesNotContain(t, e => e.Name is CreationSuite.ListFile or ".git" or "node_modules" or "HEAD");
        var auf = t.Single(e => e.Name == "Aufnahmen"); Assert.True(auf.IsFolder); Assert.Equal(2, auf.Count); Assert.Equal(0, auf.Depth);
        Assert.Equal(1, t.Single(e => e.Name == "Tag 1").Depth); Assert.Equal(2, t.Single(e => e.Name == "b.mp4").Depth);
        Assert.Equal("notiz.txt", t[^1].Name); // Dateien nach den Ordnern
        Assert.True(t.FindIndex(e => e.Name == "Tag 1") < t.FindIndex(e => e.Name == "a.mp4")); // Unterordner vor Dateien
        Assert.Equal(3, CreationSuite.Tree(d, 3).Count);
    }

    [Fact(DisplayName = "Projektmappe: Umbenennen verschiebt den Ordner und passt die Überschrift an; leerer oder vorhandener Name wird abgelehnt")]
    public void Rename()
    {
        var root = Root(); var a = CreationSuite.Create(root, "web", "Seite"); CreationSuite.Create(root, "app", "Tool");
        var b = CreationSuite.Rename(a, "Meine Seite");
        Assert.False(Directory.Exists(a)); Assert.Equal("Meine-Seite", Path.GetFileName(b));
        Assert.Equal("# Meine Seite", File.ReadLines(Path.Combine(b, CreationSuite.ListFile)).First());
        Assert.Equal(6, CreationSuite.Steps(b).Count);
        Assert.Throws<ArgumentException>(() => CreationSuite.Rename(b, "  "));
        Assert.Throws<IOException>(() => CreationSuite.Rename(b, "Tool"));
        Assert.Equal(b, CreationSuite.Rename(b, "Meine Seite"));
    }
}
