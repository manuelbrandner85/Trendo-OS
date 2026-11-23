using Trendo.Core.Shell;
using Xunit;
using K = Trendo.Core.Shell.FileUndo.Kind;
using M = Trendo.Core.Shell.OsBehavior.Mods;

namespace Trendo.Core.Tests;

/// <summary>T-0754 (CORE-01, audit-5): Dateiaktionen lassen sich rückgängig machen und wiederholen – mit echten Dateien in einem Temp-Ordner.
/// Nie überschreiben, nie endgültig löschen; ehrliche Meldung, wenn es nicht mehr geht.</summary>
public class T0754DateiRueckgaengigTests
{
    static string NewRoot() { var r = Path.Combine(Path.GetTempPath(), "trendo-t0754-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(r); return r; }

    [Fact(DisplayName = "T-0754: Strg+Z / Strg+Y (Explorer), Strg+Umschalt+Z (GNOME), ⌘Z / ⌘⇧Z (Finder) sind Rückgängig und Wiederholen")]
    public void Tasten()
    {
        foreach (var s in new[] { "windows", "linux", "trendo" })
        {
            Assert.Equal("undo", OsBehavior.FileKey(s, "Z", M.Ctrl));
            Assert.Equal("redo", OsBehavior.FileKey(s, "Y", M.Ctrl));
            Assert.Equal("redo", OsBehavior.FileKey(s, "Z", M.Ctrl | M.Shift));
        }
        Assert.Equal("undo", OsBehavior.FileKey("mac", "Z", M.Ctrl));
        Assert.Equal("redo", OsBehavior.FileKey("mac", "Z", M.Ctrl | M.Shift));
        Assert.Null(OsBehavior.FileKey("windows", "Z", M.None));
    }

    [Fact(DisplayName = "T-0754: Verschieben, Umbenennen, Kopieren, Neuer Ordner, Löschen – mehrere Schritte zurück und wieder vor, Liste der Sitzung")]
    public void Rundreise()
    {
        var root = NewRoot();
        try
        {
            var fb = new FileBrowser { Root = root }; var u = new FileUndo(fb);
            var a = Path.Combine(root, "Brief.txt"); File.WriteAllText(a, "Hallo");
            var sub = fb.NewFolder(root); u.Record(K.NeuerOrdner, root, sub);
            var moved = fb.Paste(a, sub, true); u.Record(K.Verschieben, a, moved);
            var ren = fb.Rename(moved, "Antwort.txt"); u.Record(K.Umbenennen, moved, ren);
            var copy = fb.Paste(ren, root, false); u.Record(K.Kopieren, ren, copy);
            var trashed = fb.Trash(copy); u.Record(K.Papierkorb, copy, trashed);
            Assert.Equal(["Löschen von „Antwort.txt“", "Kopieren von „Antwort.txt“", "Umbenennen von „Brief.txt“ in „Antwort.txt“", "Verschieben von „Brief.txt“", "Neuer Ordner „Neuer Ordner“"], u.UndoList);

            var r = u.Undo(); Assert.True(r.Done, r.Message); Assert.True(File.Exists(copy)); Assert.Equal("Hallo", File.ReadAllText(copy)); Assert.False(File.Exists(trashed));
            r = u.Undo(); Assert.True(r.Done, r.Message); Assert.False(File.Exists(copy)); Assert.True(Directory.GetFiles(fb.TestTrash).Length >= 1); // Kopie liegt im Papierkorb, nicht endgültig weg
            r = u.Undo(); Assert.True(r.Done, r.Message); Assert.True(File.Exists(moved)); Assert.False(File.Exists(ren));
            r = u.Undo(); Assert.True(r.Done, r.Message); Assert.True(File.Exists(a)); Assert.Equal("Hallo", File.ReadAllText(a)); Assert.False(File.Exists(moved));
            r = u.Undo(); Assert.True(r.Done, r.Message); Assert.False(Directory.Exists(sub));
            Assert.False(u.CanUndo); Assert.False(u.Undo().Done);
            Assert.Equal(5, u.RedoList.Count); Assert.Equal("Neuer Ordner „Neuer Ordner“", u.RedoList[0]);

            for (var i = 0; i < 5; i++) { r = u.Redo(); Assert.True(r.Done, r.Message); }
            Assert.True(Directory.Exists(sub)); Assert.False(File.Exists(a)); Assert.True(File.Exists(ren)); Assert.False(File.Exists(copy)); Assert.Equal("Hallo", File.ReadAllText(ren));
            Assert.False(u.CanRedo);
            // neue Aktion leert „Wiederholen“; UndoMany geht mehrere Schritte auf einmal
            u.Undo(); Assert.True(u.CanRedo);
            var n2 = fb.NewFolder(root); u.Record(K.NeuerOrdner, root, n2); Assert.False(u.CanRedo);
            r = u.UndoMany(3); Assert.True(r.Done, r.Message); Assert.False(Directory.Exists(n2)); Assert.True(File.Exists(moved)); Assert.False(File.Exists(ren));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "T-0754: Mehrfachauswahl ist ein Schritt; ist eine Datei extern geändert, bleibt alles, wie es ist – ehrliche Meldung")]
    public void Extern_geaendert()
    {
        var root = NewRoot();
        try
        {
            var fb = new FileBrowser { Root = root }; var u = new FileUndo(fb);
            var dst = Directory.CreateDirectory(Path.Combine(root, "Ziel")).FullName;
            var x = Path.Combine(root, "x.txt"); var y = Path.Combine(root, "y.txt"); File.WriteAllText(x, "1"); File.WriteAllText(y, "2");
            var mx = fb.Paste(x, dst, true); var my = fb.Paste(y, dst, true); u.Record(K.Verschieben, [(x, mx), (y, my)]);
            Assert.Equal("Verschieben von 2 Elementen", u.UndoList[0]);
            File.WriteAllText(my, "von außen geändert"); File.SetLastWriteTimeUtc(my, DateTime.UtcNow.AddMinutes(5));
            var r = u.Undo();
            Assert.False(r.Done); Assert.Contains("„y.txt“ wurde inzwischen verändert", r.Message); Assert.Contains("nichts geändert", r.Message);
            Assert.True(File.Exists(mx)); Assert.True(File.Exists(my)); Assert.False(File.Exists(x)); Assert.False(File.Exists(y)); // auch x bleibt am Ziel
            Assert.Equal("von außen geändert", File.ReadAllText(my));
            Assert.False(u.CanUndo); // aus der Liste genommen
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "T-0754: Nie überschreiben – alter Platz belegt, Ordner nicht mehr leer, Papierkorb geleert: nichts passiert, ehrliche Meldung")]
    public void Nie_ueberschreiben()
    {
        var root = NewRoot();
        try
        {
            var fb = new FileBrowser { Root = root }; var u = new FileUndo(fb);
            // alter Platz belegt
            var a = Path.Combine(root, "a.txt"); File.WriteAllText(a, "alt");
            var b = fb.Rename(a, "b.txt"); u.Record(K.Umbenennen, a, b);
            File.WriteAllText(a, "neu angelegt");
            var r = u.Undo(); Assert.False(r.Done); Assert.Contains("überschreibt nichts", r.Message);
            Assert.Equal("neu angelegt", File.ReadAllText(a)); Assert.Equal("alt", File.ReadAllText(b));
            // Ordner nicht mehr leer
            var nf = fb.NewFolder(root); u.Record(K.NeuerOrdner, root, nf); File.WriteAllText(Path.Combine(nf, "drin.txt"), "x");
            r = u.Undo(); Assert.False(r.Done); Assert.Contains("nicht mehr leer", r.Message); Assert.True(File.Exists(Path.Combine(nf, "drin.txt")));
            // Papierkorb geleert
            var c = Path.Combine(root, "c.txt"); File.WriteAllText(c, "c"); var t = fb.Trash(c); u.Record(K.Papierkorb, c, t);
            File.Delete(t); // so sieht ein geleerter Papierkorb aus
            r = u.Undo(); Assert.False(r.Done); Assert.Contains("nicht mehr im Papierkorb", r.Message); Assert.False(File.Exists(c));
            // Wiederholen trifft auf ein belegtes Ziel
            var d = Path.Combine(root, "d.txt"); File.WriteAllText(d, "d"); var e = fb.Rename(d, "e.txt"); u.Record(K.Umbenennen, d, e);
            Assert.True(u.Undo().Done); File.WriteAllText(e, "dazwischen");
            r = u.Redo(); Assert.False(r.Done); Assert.Contains("überschreibt nichts", r.Message); Assert.Equal("dazwischen", File.ReadAllText(e)); Assert.Equal("d", File.ReadAllText(d));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "T-0754: Echter Windows-Papierkorb – Löschen, Rückgängig holt die Datei zurück, Wiederholen löscht wieder; geleerter Papierkorb wird ehrlich gemeldet")]
    public void Windows_Papierkorb_echt()
    {
        if (!OperatingSystem.IsWindows()) return; // nur unter Windows prüfbar
        var root = NewRoot();
        try
        {
            var fb = new FileBrowser(); var u = new FileUndo(fb, FileBrowser.SystemRecycle); // echtes Dateisystem, echter Papierkorb, keine Nachbildung
            var f = Path.Combine(root, "trendo-t0754-papierkorb.txt"); File.WriteAllText(f, "Inhalt bleibt");
            var t = fb.Trash(f, FileBrowser.SystemRecycle);
            Assert.False(File.Exists(f));
            Assert.True(File.Exists(t), "Im Papierkorb nicht gefunden: " + t); Assert.StartsWith("$R", Path.GetFileName(t));
            u.Record(K.Papierkorb, f, t);
            var r = u.Undo(); Assert.True(r.Done, r.Message);
            Assert.Equal("Inhalt bleibt", File.ReadAllText(f)); Assert.False(File.Exists(t));
            Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(t)!, "$I" + Path.GetFileName(t)[2..]))); // Vermerk mit aufgeräumt
            r = u.Redo(); Assert.True(r.Done, r.Message); Assert.False(File.Exists(f));
            var t2 = FileBrowser.FindInWindowsRecycleBin(Path.GetFullPath(f), DateTime.Now.AddMinutes(-1))!; Assert.NotNull(t2);
            // eigene Testdatei aus dem Papierkorb entfernen – so sieht „Papierkorb geleert“ aus
            File.Delete(t2); File.Delete(Path.Combine(Path.GetDirectoryName(t2)!, "$I" + Path.GetFileName(t2)[2..]));
            r = u.Undo(); Assert.False(r.Done); Assert.Contains("nicht mehr im Papierkorb", r.Message); Assert.False(File.Exists(f));
        }
        finally { Directory.Delete(root, true); }
    }
}
