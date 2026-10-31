using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// T-0299: Sicherung mit Zeitleiste – Punkt wählen, geänderte Datei ansehen, nach „Ja“ einzeln zurückholen.
public class BackupTimeline_Ui
{
    [AvaloniaFact(DisplayName = "T-0299: Zeitleiste zeigt Sicherungen, Vergleich mit heute, Vorschau, Einzeldatei nach Ja zurück (Nein ändert nichts)")]
    public void Ui_timeline()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1500; w.Height = 1000;
        var file = Path.Combine(root, "notiz.txt"); File.WriteAllText(file, "erste Fassung");
        w.Show("sicherung"); Settle();
        RealClick(w, V<Button>(w, "btn-backup-now")); Settle();
        File.WriteAllText(file, "heute geändert");
        w.Show("sicherung"); Settle();
        Assert.NotNull(V<Button>(w, "backup-pick-0"));
        Assert.Contains("1 geändert", w.Find<TextBlock>("backup-summary").Text);
        RealClick(w, V<Button>(w, "backup-file-0")); Settle();
        Assert.Equal("erste Fassung", w.Find<SelectableTextBlock>("backup-preview-text").Text);
        RealClick(w, V<Button>(w, "backup-one")); Settle();
        RealClick(w, V<Button>(w, "backup-one-no")); Settle();
        Assert.Equal("heute geändert", File.ReadAllText(file));
        RealClick(w, V<Button>(w, "backup-one")); Settle();
        RealClick(w, V<Button>(w, "backup-one-yes")); Settle();
        Assert.Equal("erste Fassung", File.ReadAllText(file));
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("ist zurückgeholt"));
        Assert.Single(Directory.GetFiles(Path.Combine(root, "backups", "einzeln"), "notiz.txt", SearchOption.AllDirectories));
        Assert.Contains("nichts geändert", ((TextBlock)w.Find<StackPanel>("backup-files").Children[0]).Text);
    }
}
