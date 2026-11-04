using Trendo.Core.Shell;
using Xunit;
using M = Trendo.Core.Shell.OsBehavior.Mods;

namespace Trendo.Core.Tests;

/// <summary>Bedienverhalten wie Windows 11, macOS und GNOME (04.11.2026): Ziehen/Ablegen, Kopie- und Ordnernamen, Tastenkürzel, Papierkorb.</summary>
public class OsBehaviorTests
{
    [Fact(DisplayName = "Ziehen und Ablegen: gleiches Laufwerk verschiebt, anderes kopiert; Strg/Umschalt (Windows, GNOME) und Wahltaste/⌘ (Mac) ändern das")]
    public void Drop()
    {
        foreach (var s in new[] { "windows", "linux" })
        {
            Assert.False(OsBehavior.DropCopies(s, true, M.None)); Assert.True(OsBehavior.DropCopies(s, false, M.None));
            Assert.True(OsBehavior.DropCopies(s, true, M.Ctrl)); Assert.False(OsBehavior.DropCopies(s, false, M.Shift));
        }
        Assert.False(OsBehavior.DropCopies("mac", true, M.None)); Assert.True(OsBehavior.DropCopies("mac", false, M.None));
        Assert.True(OsBehavior.DropCopies("mac", true, M.Alt)); Assert.False(OsBehavior.DropCopies("mac", false, M.Ctrl)); // PC-Tastatur: Strg = ⌘
        Assert.False(OsBehavior.DropCopies("mac", false, M.Meta, macHardware: true));
    }

    [Fact(DisplayName = "Kopie- und Ordnernamen wie im System: „ - Kopie“, „ Kopie 2“, „(weitere Kopie)“, „Unbenannter Ordner“")]
    public void Names()
    {
        var have = new HashSet<string> { "Bericht.txt", "Bericht - Kopie.txt", "Bericht Kopie.txt", "Bericht (Kopie).txt", "Neuer Ordner", "Unbenannter Ordner" };
        bool Ex(string n) => have.Contains(n);
        Assert.Equal("Bericht - Kopie (2).txt", OsBehavior.CopyName("windows", "Bericht.txt", false, Ex));
        Assert.Equal("Bericht Kopie 2.txt", OsBehavior.CopyName("mac", "Bericht.txt", false, Ex));
        Assert.Equal("Bericht (weitere Kopie).txt", OsBehavior.CopyName("linux", "Bericht.txt", false, Ex));
        Assert.Equal("Neu.txt", OsBehavior.CopyName("windows", "Neu.txt", false, Ex));
        Assert.Equal("Neuer Ordner (2)", OsBehavior.NewFolderName("windows", Ex));
        Assert.Equal("Neuer Ordner 2", OsBehavior.NewFolderName("mac", Ex));
        Assert.Equal("Unbenannter Ordner 2", OsBehavior.NewFolderName("linux", Ex));
    }

    [Fact(DisplayName = "Tasten: Finder Zeilenschalter = umbenennen, ⌘⌫ = Papierkorb, ⌘⌥V = hierher bewegen; Explorer/GNOME Enter = öffnen, F2, Entf, Alt+↑")]
    public void Keys()
    {
        Assert.Equal("rename", OsBehavior.FileKey("mac", "Enter", M.None));
        Assert.Equal("trash", OsBehavior.FileKey("mac", "Back", M.Ctrl));
        Assert.Null(OsBehavior.FileKey("mac", "Delete", M.None)); // Entf allein löscht im Finder nichts
        Assert.Equal("move-here", OsBehavior.FileKey("mac", "V", M.Ctrl | M.Alt));
        Assert.Equal("open", OsBehavior.FileKey("mac", "Down", M.Ctrl));
        Assert.Equal("preview", OsBehavior.FileKey("mac", "Space", M.None));
        foreach (var s in new[] { "windows", "linux" })
        {
            Assert.Equal("open", OsBehavior.FileKey(s, "Enter", M.None)); Assert.Equal("rename", OsBehavior.FileKey(s, "F2", M.None));
            Assert.Equal("trash", OsBehavior.FileKey(s, "Delete", M.None)); Assert.Equal("up", OsBehavior.FileKey(s, "Up", M.Alt));
            Assert.Equal("cut", OsBehavior.FileKey(s, "X", M.Ctrl)); Assert.Equal("newfolder", OsBehavior.FileKey(s, "N", M.Ctrl | M.Shift));
        }
        Assert.Equal("close", OsBehavior.WindowKey("mac", "W", M.Ctrl)); Assert.Equal("spotlight", OsBehavior.WindowKey("mac", "Space", M.Ctrl));
        Assert.Equal("close", OsBehavior.WindowKey("windows", "F4", M.Alt)); Assert.Equal("files", OsBehavior.WindowKey("windows", "E", M.Meta));
        Assert.Equal("overview", OsBehavior.WindowKey("linux", "LWin", M.None)); Assert.Equal("maximize", OsBehavior.WindowKey("linux", "Up", M.Meta));
    }

    [Fact(DisplayName = "Dateimanager-Kern: Liste (Ordner zuerst), neuer Ordner, umbenennen, kopieren mit Kopiename, verschieben, Papierkorb statt Löschen, nur im Testordner")]
    public void Browser()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-fb-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(root);
        try
        {
            var fb = new FileBrowser { Root = root, Style = "mac" };
            File.WriteAllText(Path.Combine(root, "b.txt"), "x"); Directory.CreateDirectory(Path.Combine(root, "z-ordner")); File.WriteAllText(Path.Combine(root, ".versteckt"), "");
            var l = fb.List(root); Assert.Equal(["z-ordner", "b.txt"], l.Select(i => i.Name)); Assert.Equal("Textdokument", l[1].Kind);
            var nf = fb.NewFolder(root); Assert.Equal("Neuer Ordner", Path.GetFileName(nf));
            var copy = fb.Paste(Path.Combine(root, "b.txt"), root, false); Assert.Equal("b Kopie.txt", Path.GetFileName(copy));
            var moved = fb.Paste(copy, nf, true); Assert.True(File.Exists(moved)); Assert.False(File.Exists(copy));
            var ren = fb.Rename(moved, "Notiz.txt"); Assert.Equal("Notiz.txt", Path.GetFileName(ren));
            Assert.Throws<InvalidOperationException>(() => fb.Rename(ren, "a/b"));
            Assert.Throws<InvalidOperationException>(() => fb.Paste(Path.Combine(root, "z-ordner"), Path.Combine(root, "z-ordner"), false)); // Ordner nicht in sich selbst
            var t = fb.Trash(ren); Assert.False(File.Exists(ren)); Assert.StartsWith(fb.TestTrash, t);
            Assert.Throws<UnauthorizedAccessException>(() => fb.List(Path.GetTempPath()));
            Assert.Single(fb.Drives());
            Assert.Equal(root, fb.Crumbs(Path.Combine(root, "z-ordner"))[0].Path.TrimEnd('\\', '/'));
        }
        finally { Directory.Delete(root, true); }
    }
}
