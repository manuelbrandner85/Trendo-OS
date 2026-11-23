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

    // ---------- Gegenprüfung (Befunde 1–3) ----------

    /// <summary>Befund 1: Ein Element „Papierkorb“ im Arbeitsordner des Prozesses darf nie als Lage im Papierkorb gelten.</summary>
    [Fact(DisplayName = "T-0754 Gegenprüfung 1: Nicht im Papierkorb gelandet – ein fremder Ordner „Papierkorb“ im Arbeitsordner wird weder beim Rückgängig noch beim Wiederholen verschoben")]
    public void Fremder_Ordner_Papierkorb_bleibt()
    {
        var root = NewRoot(); var cwd = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = root;
            var fremd = Directory.CreateDirectory(Path.Combine(root, "Papierkorb")).FullName; File.WriteAllText(Path.Combine(fremd, "fremd.txt"), "gehört jemand anderem");
            var fb = new FileBrowser();
            var u = new FileUndo(fb, p => File.Delete(p)); // wie ein Laufwerk, auf dem Windows endgültig löscht
            var g = Path.Combine(root, "geloescht.txt"); File.WriteAllText(g, "weg");
            // a) Aufzeichnen mit dem Wort „Papierkorb“ (so lieferte Trash es früher)
            u.Record(FileUndo.Kind.Papierkorb, g, "Papierkorb"); File.Delete(g);
            var r = u.Undo();
            Assert.False(r.Done); Assert.Contains("endgültig gelöscht", r.Message);
            Assert.True(File.Exists(Path.Combine(fremd, "fremd.txt"))); Assert.False(File.Exists(g)); Assert.False(Directory.Exists(g));
            // b) Wiederholen: erst echt in den Windows-Papierkorb, zurückholen; Wiederholen löscht dann „endgültig“ – Trash liefert null, nicht das Wort
            if (OperatingSystem.IsWindows())
            {
                File.WriteAllText(g, "weg"); var inBin = fb.Trash(g, FileBrowser.SystemRecycle); Assert.True(fb.IsTrashLocation(inBin), "nicht im Papierkorb: " + inBin);
                u.Record(FileUndo.Kind.Papierkorb, g, inBin);
                Assert.True(u.Undo().Done); Assert.True(File.Exists(g)); // eigene Datei ist wieder aus dem Papierkorb heraus
                r = u.Redo(); Assert.True(r.Done, r.Message); Assert.False(File.Exists(g));
                Assert.Null(fb.Trash(CreateFile(Path.Combine(root, "ohne.txt")), p => File.Delete(p))); // nicht im Papierkorb gelandet: null statt des Worts
                r = u.Undo();
                Assert.False(r.Done); Assert.Contains("endgültig gelöscht", r.Message);
                Assert.True(File.Exists(Path.Combine(fremd, "fremd.txt"))); Assert.False(File.Exists(g)); Assert.False(Directory.Exists(g));
            }
            // Zweitprüfung 3: nur die echten Papierkorb-Stellen zählen
            var echt = new FileBrowser(); var test = new FileBrowser { Root = root };
            Assert.False(echt.IsTrashLocation("Papierkorb")); Assert.False(echt.IsTrashLocation(fremd));
            Assert.True(test.IsTrashLocation(Path.Combine(root, ".Papierkorb", "a.txt"))); Assert.False(echt.IsTrashLocation(Path.Combine(root, ".Papierkorb", "a.txt")));
            Assert.False(test.IsTrashLocation(Path.Combine(root, "x", ".Papierkorb", "a.txt")));
            if (OperatingSystem.IsWindows())
            {
                Assert.True(echt.IsTrashLocation(@"C:\$Recycle.Bin\S-X\$RABC.txt"));
                Assert.False(echt.IsTrashLocation(Path.Combine(root, "$Recycle.Bin", "S-X", "$RABC.txt"))); // nicht unter der Laufwerkswurzel
                Assert.False(echt.IsTrashLocation(@"C:\$Recycle.Bin\S-X\ABC.txt"));
                Assert.False(echt.IsTrashLocation(Path.Combine(root, ".Trash", "a.txt")));
            }
        }
        finally { Environment.CurrentDirectory = cwd; Directory.Delete(root, true); }
    }

    static string CreateFile(string p) { File.WriteAllText(p, "x"); return p; }

    /// <summary>Befund 2: Laufwerk ohne Papierkorb – es wird nichts gelöscht, sondern abgelehnt (die Oberfläche fragt dann ausdrücklich). Echt mit SUBST.</summary>
    [Fact(DisplayName = "T-0754 Gegenprüfung 2: SUBST-Laufwerk hat keinen Papierkorb – Löschen bricht ohne Wirkung ab; Rückgängig nach endgültigem Löschen sagt „endgültig gelöscht“")]
    public void Kein_Papierkorb_wird_erkannt()
    {
        var root = NewRoot();
        try
        {
            // nachgebildet (alle Systeme)
            var fbT = new FileBrowser { Root = root, RecycleBinCheck = _ => false };
            var f = Path.Combine(root, "netz.txt"); File.WriteAllText(f, "bleibt");
            Assert.Throws<NoRecycleBinException>(() => fbT.Trash(f)); Assert.Equal("bleibt", File.ReadAllText(f));
            Assert.True(new FileBrowser { Root = root }.HasTrash(f));
            if (!OperatingSystem.IsWindows()) return;
            // echt: eigenes SUBST-Laufwerk auf den Temp-Ordner
            var letter = "PONMLKJIHGFEDZYXWVUTSRQ".Select(c => c + ":").FirstOrDefault(d => !Directory.Exists(d + "\\"));
            Assert.NotNull(letter);
            Assert.Equal(0, Run("subst", $"{letter} \"{root}\""));
            try
            {
                var viaSubst = letter + "\\netz.txt";
                Assert.True(File.Exists(viaSubst));
                var fb = new FileBrowser();
                Assert.False(fb.HasTrash(viaSubst));
                Assert.Throws<NoRecycleBinException>(() => fb.Trash(viaSubst, FileBrowser.SystemRecycle));
                Assert.True(File.Exists(viaSubst)); Assert.Equal("bleibt", File.ReadAllText(f));
                Assert.True(fb.HasTrash(f)); // dieselbe Datei über C: hat einen Papierkorb
            }
            finally { Run("subst", $"{letter} /D"); }
        }
        finally { Directory.Delete(root, true); }
    }

    static int Run(string exe, string args)
    {
        using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true })!;
        p.WaitForExit(20000); return p.ExitCode;
    }

    /// <summary>Befund 3: Teilfehler bei Mehrfachauswahl – der Rest bleibt als eigener Schritt, das Erledigte lässt sich wiederholen.</summary>
    [Fact(DisplayName = "T-0754 Gegenprüfung 3: Mehrfachauswahl, eine Datei gesperrt – Rest bleibt als eigener Rückgängig-Schritt, Erledigtes als Wiederholen-Schritt")]
    public void Teilfehler_bleibt_in_der_Liste()
    {
        if (!OperatingSystem.IsWindows()) return; // Sperre beim Verschieben nur unter Windows
        var root = NewRoot();
        try
        {
            var fb = new FileBrowser { Root = root }; var u = new FileUndo(fb);
            var dst = Directory.CreateDirectory(Path.Combine(root, "Ziel")).FullName;
            var x = Path.Combine(root, "x.txt"); var y = Path.Combine(root, "y.txt"); File.WriteAllText(x, "1"); File.WriteAllText(y, "2");
            var mx = fb.Paste(x, dst, true); var my = fb.Paste(y, dst, true); u.Record(K.Verschieben, [(x, mx), (y, my)]);
            FileUndo.Result r;
            using (new FileStream(mx, FileMode.Open, FileAccess.Read, FileShare.None)) r = u.Undo(); // x gesperrt; rückwärts: erst y, dann x
            Assert.False(r.Done); Assert.Contains("nur teilweise", r.Message); Assert.Contains("eigener Schritt", r.Message);
            Assert.True(File.Exists(y)); Assert.True(File.Exists(mx));
            Assert.Equal(["Verschieben von „x.txt“"], u.UndoList); Assert.Equal(["Verschieben von „y.txt“"], u.RedoList);
            r = u.Undo(); Assert.True(r.Done, r.Message); Assert.True(File.Exists(x)); Assert.False(File.Exists(mx));
            r = u.Redo(); Assert.True(r.Done, r.Message); Assert.True(File.Exists(mx));
        }
        finally { Directory.Delete(root, true); }
    }

    // ---------- Zweitprüfung ----------

    /// <summary>Zweitprüfung 1: Papierkorb auf „sofort löschen“ gestellt oder Element zu groß – nachgebildet über die einstellbare Abfrage.</summary>
    [Fact(DisplayName = "T-0754 Zweitprüfung 1: „Dateien sofort löschen“ oder größer als der Papierkorb – nichts wird gelöscht, Grund wird genannt")]
    public void Wuerde_endgueltig_loeschen_nachgebildet()
    {
        var root = NewRoot();
        try
        {
            var f = Path.Combine(root, "gross.bin"); File.WriteAllBytes(f, new byte[10]);
            var nuke = new FileBrowser { Root = root, RecycleBinPolicy = _ => new FileBrowser.BinPolicy(true, null) };
            var e = Assert.Throws<NoRecycleBinException>(() => nuke.Trash(f)); Assert.Contains("sofort löschen", e.Reason); Assert.True(File.Exists(f));
            var klein = new FileBrowser { Root = root, RecycleBinPolicy = _ => new FileBrowser.BinPolicy(false, 5) };
            e = Assert.Throws<NoRecycleBinException>(() => klein.Trash(f)); Assert.Contains("größer als der Papierkorb", e.Reason); Assert.True(File.Exists(f));
            var ordner = Directory.CreateDirectory(Path.Combine(root, "Ordner")).FullName; File.WriteAllBytes(Path.Combine(ordner, "a.bin"), new byte[4]); File.WriteAllBytes(Path.Combine(ordner, "b.bin"), new byte[4]);
            Assert.NotNull(klein.TrashProblem(ordner)); // 8 Bytes im Ordner > 5
            var passt = new FileBrowser { Root = root, RecycleBinPolicy = _ => new FileBrowser.BinPolicy(false, 100) };
            Assert.Null(passt.TrashProblem(f)); Assert.True(passt.IsTrashLocation(passt.Trash(f))); Assert.False(File.Exists(f));
            // Rückfrage nennt Grund; Wiederholen wird abgelehnt, wenn es endgültig löschen würde
            var g = Path.Combine(root, "g.txt"); File.WriteAllText(g, "g");
            var u = new FileUndo(passt); var t = passt.Trash(g); u.Record(K.Papierkorb, g, t); Assert.True(u.Undo().Done);
            passt.RecycleBinPolicy = _ => new FileBrowser.BinPolicy(true, null);
            var r = u.Redo(); Assert.False(r.Done); Assert.Contains("sofort löschen", r.Message); Assert.True(File.Exists(g));
            if (OperatingSystem.IsWindows()) Assert.Null(FileBrowser.WindowsTrashProblem(Path.Combine(root, "g.txt"))); // echte, nur lesende Abfrage für C: (Voreinstellung)
        }
        finally { Directory.Delete(root, true); }
    }

    /// <summary>Zweitprüfung 1 echt: SystemRecycle direkt auf einem SUBST-Laufwerk löscht nichts mehr still.</summary>
    [Fact(DisplayName = "T-0754 Zweitprüfung 1: SystemRecycle direkt auf einem echten SUBST-Laufwerk – kein stilles Löschen, Datei bleibt")]
    public void SystemRecycle_auf_Subst_loescht_nicht()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = NewRoot();
        var letter = "PONMLKJIHGFEDZYXWVUTSRQ".Select(c => c + ":").First(d => !Directory.Exists(d + "\\"));
        try
        {
            File.WriteAllText(Path.Combine(root, "netz.txt"), "bleibt");
            Assert.Equal(0, Run("subst", $"{letter} \"{root}\""));
            try
            {
                var viaSubst = Path.Combine(letter + Path.DirectorySeparatorChar, "netz.txt");
                Assert.True(File.Exists(viaSubst));
                var e = Assert.Throws<NoRecycleBinException>(() => FileBrowser.SystemRecycle(viaSubst));
                Assert.Contains("keinen Papierkorb", e.Reason);
                Assert.True(File.Exists(viaSubst)); Assert.Equal("bleibt", File.ReadAllText(Path.Combine(root, "netz.txt")));
            }
            finally { Run("subst", $"{letter} /D"); }
        }
        finally { Directory.Delete(root, true); }
    }

    /// <summary>Zweitprüfung 2: Pfad über 260 Zeichen – im $I-Vermerk steht er mit „\\?\“; Trendo findet ihn trotzdem und holt zurück.</summary>
    [Fact(DisplayName = "T-0754 Zweitprüfung 2: Langer Pfad (328 Zeichen) – echt in den Papierkorb, als „im Papierkorb“ erkannt, Rückgängig holt zurück")]
    public void Langer_Pfad_echt()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = NewRoot();
        try
        {
            var dir = root; var i = 0;
            while (dir.Length < 300) dir = Path.Combine(dir, $"Unterordner-mit-langem-Namen-{i++:00}");
            Directory.CreateDirectory(dir);
            var name = new string('d', 328 - dir.Length - 1 - 4) + ".txt"; var f = Path.Combine(dir, name);
            Assert.Equal(328, f.Length);
            File.WriteAllText(f, "lang");
            var fb = new FileBrowser(); var u = new FileUndo(fb, FileBrowser.SystemRecycle);
            var t = fb.Trash(f, FileBrowser.SystemRecycle);
            Assert.False(File.Exists(f)); Assert.NotNull(t); Assert.True(fb.IsTrashLocation(t), "nicht erkannt: " + t);
            u.Record(K.Papierkorb, f, t);
            var r = u.Undo(); Assert.True(r.Done, r.Message); Assert.Equal("lang", File.ReadAllText(f)); // eigene Datei wieder heraus
            Assert.Equal(@"C:\a\b.txt", FileBrowser.StripLongPrefix(@"\\?\C:\a\b.txt")); Assert.Equal(@"\\srv\x\b.txt", FileBrowser.StripLongPrefix(@"\\?\UNC\srv\x\b.txt"));
        }
        finally { Directory.Delete(root, true); }
    }
}
