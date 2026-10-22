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

    [AvaloniaFact(DisplayName = "T-0214: Wiederherstellen nennt die Datei, ändert erst nach Ja und bricht auf Wunsch ab")]
    public void Restore_names_file_and_waits_for_yes()
    {
        var (w, _, root) = Ui.Open();
        var note = Path.Combine(root, "notiz.txt");
        File.WriteAllText(note, "alt");
        w.Show("sicherung");
        w.Find<Button>("btn-backup-now").Press();
        File.WriteAllText(note, "neu");
        w.Show("sicherung");
        w.Find<Button>("backup-restore-0").Press();
        var ask = w.Find<TextBlock>("backup-ask").Text ?? "";
        Assert.Contains("Sicher?", ask);
        Assert.Contains("notiz.txt", ask);
        Assert.Contains("überschreibt", ask);
        Assert.Equal("neu", File.ReadAllText(note));
        w.Find<Button>("backup-restore-no-0").Press();
        Assert.Equal("neu", File.ReadAllText(note));
        Assert.Contains(w.GetLogicalTexts(), t => t.Contains("nichts geändert"));
        w.Find<Button>("backup-restore-0").Press();
        w.Find<Button>("backup-restore-yes-0").Press();
        Assert.Contains(w.GetLogicalTexts(), t => t.StartsWith("Wiederhergestellt:"));
        Assert.Equal("alt", File.ReadAllText(note));
        w.Close();
    }
}
