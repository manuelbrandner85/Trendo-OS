using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Trendo.App.Tests;

/// T-0214: Sicherung – Rückfrage, Ergebnis sichtbar, kein Absturz.
public class BackupUiTests
{
    [AvaloniaFact(DisplayName = "T-0214: Jetzt sichern → Meldung; Wiederherstellen fragt nach und meldet Ergebnis")]
    public void Restore_asks_and_reports()
    {
        var (w, _, root) = Ui.Open();
        File.WriteAllText(Path.Combine(root, "notiz.txt"), "v1");
        w.Show("sicherung");
        w.Find<Button>("btn-backup-now").Press();
        Assert.Contains(w.GetLogicalTexts(), t => t.StartsWith("Gesichert:"));
        w.Find<Button>("backup-restore-0").Press();
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Sicher?"));
        w.Find<Button>("backup-restore-yes-0").Press();
        w.Find<Button>("backup-restore-0").Press();
        w.Find<Button>("backup-restore-yes-0").Press();
        Assert.Contains(w.GetLogicalTexts(), t => t.StartsWith("Wiederhergestellt:"));
    }
}
