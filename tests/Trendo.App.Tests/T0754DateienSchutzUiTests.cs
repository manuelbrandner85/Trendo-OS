using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Shell;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0754: Dateien – Rückgängig/Wiederholen über Tasten, Befehlsleiste und Rechtsklick mit echten Dateien (Testordner);
/// Daten schützen – deutlicher Hinweis „nicht verschlüsselt“ mit Ordner, Ampel gelb, ehrlicher Studio-Text.</summary>
public class T0754DateienSchutzUi
{
    static void Pump() { for (var i = 0; i < 6; i++) Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }
    static MenuItem AreaItem(MainWindow w, string name) => w.GetVisualDescendants().OfType<Border>().First(b => b.Name == "fb-area").ContextMenu!.Items.OfType<MenuItem>().First(m => m.Name == name);

    [AvaloniaFact(DisplayName = "T-0754 UI: Verschieben, Umbenennen, Löschen per Strg+Z zurück, Strg+Y wieder; Knopf und Rechtsklick „Rückgängig“; extern geändert → ehrliche Meldung, nichts überschrieben")]
    public void Dateien_rueckgaengig()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show("dateien"); Pump();
            var home = w.Files.Home; var docs = w.Files.KnownFolders().First(k => k.Id == "dokumente").Path;
            var f = Path.Combine(home, "Rechnung.txt"); File.WriteAllText(f, "Betrag 12,50");
            w.FilesGo(home); Pump();
            Assert.False(w.Find<Button>("fb-undo").IsEnabled); // noch nichts zum Zurücknehmen
            var moved = w.FilesDrop(f, docs, OsBehavior.Mods.None)!; Pump();
            Assert.False(File.Exists(f)); Assert.True(File.Exists(moved));
            Assert.True(w.Find<Button>("fb-undo").IsEnabled);
            Assert.Contains("Verschieben von „Rechnung.txt“", ToolTip.GetTip(w.Find<Button>("fb-undo"))?.ToString());
            Assert.True(w.FilesKey(Key.Z, KeyModifiers.Control)); Pump();
            Assert.True(File.Exists(f)); Assert.False(File.Exists(moved)); Assert.StartsWith("Rückgängig: Verschieben", w.FilesMessage);
            Assert.True(w.FilesKey(Key.Y, KeyModifiers.Control)); Pump();
            Assert.False(File.Exists(f)); Assert.True(File.Exists(moved));

            // Umbenennen und Löschen, dann zwei Schritte zurück: einer per Knopf, einer per Rechtsklick
            w.FilesGo(docs); Pump(); w.FilesSelected = moved; w.FilesRename("Rechnung März.txt"); Pump();
            var ren = Path.Combine(docs, "Rechnung März.txt"); Assert.True(File.Exists(ren));
            w.FilesSelected = ren; Assert.True(w.FilesKey(Key.Delete, KeyModifiers.None)); Pump();
            Assert.False(File.Exists(ren)); Assert.Contains("Papierkorb", w.FilesMessage);
            Assert.Equal(["Löschen von „Rechnung März.txt“", "Umbenennen von „Rechnung.txt“ in „Rechnung März.txt“", "Verschieben von „Rechnung.txt“"], w.FilesHistory.UndoList);
            w.Find<Button>("fb-undo").Press(); Pump();
            Assert.True(File.Exists(ren)); Assert.Equal("Betrag 12,50", File.ReadAllText(ren)); Assert.Equal([ren], w.FilesSelection);
            var mi = AreaItem(w, "fb-mi-undo"); Assert.True(mi.IsEnabled); Assert.Contains("Umbenennen", mi.Header?.ToString());
            mi.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Pump();
            Assert.True(File.Exists(moved)); Assert.False(File.Exists(ren));
            Assert.True(AreaItem(w, "fb-mi-redo").IsEnabled);

            // Neuer Ordner – von außen gefüllt: Rückgängig entfernt ihn nicht
            var nf = w.FilesNewFolder(); Pump(); File.WriteAllText(Path.Combine(nf, "wichtig.txt"), "x");
            w.FilesUndo(); Pump();
            Assert.True(Directory.Exists(nf)); Assert.Contains("nicht mehr leer", w.FilesMessage);

            // extern geändert: Verschieben lässt sich nicht mehr zurücknehmen, nichts wird überschrieben
            w.FilesGo(docs); Pump(); var g = Path.Combine(docs, "Liste.txt"); File.WriteAllText(g, "1");
            var target = w.Files.KnownFolders().First(k => k.Id == "bilder").Path;
            var gm = w.FilesDrop(g, target, OsBehavior.Mods.None)!; File.WriteAllText(gm, "1 und mehr"); File.SetLastWriteTimeUtc(gm, DateTime.UtcNow.AddMinutes(3));
            w.FilesUndo(); Pump();
            Assert.Contains("wurde inzwischen verändert", w.FilesMessage); Assert.Contains("nichts geändert", w.FilesMessage);
            Assert.True(File.Exists(gm)); Assert.False(File.Exists(g)); Assert.Equal("1 und mehr", File.ReadAllText(gm));
            Assert.Contains("nichts geändert", w.Find<TextBlock>("fb-status").Text);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0754 UI: Sicherung zeigt deutlich „nicht verschlüsselt“, Ordner und „Ordner öffnen“; Meldung nach dem Sichern sagt es; Ampel gelb; Studio verspricht keine Passwortverwaltung")]
    public void Sicherung_ehrlicher_Hinweis()
    {
        var (w, _, root) = Ui.Open();
        string? opened = null; MainWindow.BackupOpenFolderOverride = d => opened = d;
        try
        {
            File.WriteAllText(Path.Combine(root, "notiz.txt"), "v1");
            w.Show("sicherung"); Pump();
            Assert.True(w.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "backup-unencrypted").IsEffectivelyVisible);
            Assert.Contains("nicht verschlüsselt", w.Find<TextBlock>("backup-unencrypted-text").Text);
            Assert.Contains("BitLocker", w.Find<TextBlock>("backup-unencrypted-advice").Text);
            Assert.Contains(w.BackupStore.Folder, w.Find<TextBlock>("backup-folder").Text);
            w.Find<Button>("backup-open-folder").Press(); Pump();
            Assert.Equal(w.BackupStore.Folder, opened); Assert.True(Directory.Exists(opened));
            w.Find<Button>("btn-backup-now").Press(); Pump();
            Assert.Contains("nicht verschlüsselt", w.Find<TextBlock>("backup-msg").Text);
            // Ampel: unverschlüsselt ist gelb, nie grün
            w.Show("schutz"); Pump();
            var card = w.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "sec-check-backup-crypt");
            Assert.Contains(card.GetLogicalDescendants().OfType<TextBlock>(), t => (t.Text ?? "").Contains("nicht verschlüsselt"));
            Assert.Equal(CheckLevel.Hinweis, SecurityCenter.Checks(w.Core, w.BackupStore, DateTime.Now).Single(c => c.Id == "backup-crypt").Level);
            Assert.DoesNotContain("Passwörter und Sicherungen", StudioCatalog.ById["CORE-11"].Sentence);
        }
        finally { MainWindow.BackupOpenFolderOverride = null; w.Close(); }
    }
}
