using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0214: „Wiederherstellen“ fragt einmal und stürzt nicht ab.</summary>
public class T0214_Sicherung
{
    [AvaloniaFact(DisplayName = "T-0214: Wiederherstellen zeigt, was überschrieben wird, und meldet danach die Dateianzahl")]
    public void Restore_asks_once_then_reports_count()
    {
        var (w, _, root) = Ui.Open();
        File.WriteAllText(Path.Combine(root, "notiz.txt"), "alt");
        w.Show("sicherung");
        w.Find<Button>("btn-backup-now").Press();
        File.WriteAllText(Path.Combine(root, "notiz.txt"), "neu");
        w.Show("sicherung");
        w.Find<Button>("backup-restore-0").Press();
        var ask = w.Find<TextBlock>("backup-msg").Text ?? "";
        Assert.Contains("überschreibt", ask);
        Assert.Contains("notiz.txt", ask);
        Assert.Equal("neu", File.ReadAllText(Path.Combine(root, "notiz.txt")));
        w.Find<Button>("backup-restore-yes").Press();
        var done = w.Find<TextBlock>("backup-msg").Text ?? "";
        Assert.StartsWith("Wiederhergestellt:", done);
        Assert.Contains("Dateien", done);
        Assert.Equal("alt", File.ReadAllText(Path.Combine(root, "notiz.txt")));
        w.Close();
    }
}
