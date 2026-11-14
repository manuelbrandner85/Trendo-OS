using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Xunit;

namespace Trendo.App.Tests;

/// T-0210: Einrichtung – Wahl ist sichtbar, Reparieren meldet das Ergebnis.
public class SetupFeedbackTests
{
    [AvaloniaFact(DisplayName = "T-0210: Design-Wahl zeigt sofort Vorschau und „Gewählt“")]
    public void Theme_choice_visible()
    {
        var (w, _, _) = Ui.Open();
        w.Show("einrichtung");
        for (var i = 0; i < 3; i++) w.Find<Button>("wiz-next").Press(); // Willkommen, Anmeldung, Ordner
        w.Find<RadioButton>("wiz-theme-kontrast").IsChecked = true; Dispatcher.UIThread.RunJobs();
        Assert.Contains(w.GetLogicalTexts(), t => t == "Vorschau: Hoher Kontrast");
        Assert.Contains(w.GetLogicalTexts(), t => t.StartsWith("Gewählt: Hoher Kontrast"));
    }

    [AvaloniaFact(DisplayName = "T-0210: Paket-Wahl erscheint in der Zusammenfassung")]
    public void Pack_choice_in_summary()
    {
        var (w, _, _) = Ui.Open();
        w.Show("einrichtung");
        for (var i = 0; i < 4; i++) w.Find<Button>("wiz-next").Press();
        var cb = w.GetLogicalDescendants().OfType<CheckBox>().First(c => c.Name!.StartsWith("wiz-pack-") && c.IsChecked != true);
        cb.IsChecked = true; Dispatcher.UIThread.RunJobs();
        var summary = w.Find<TextBlock>("wiz-pack-summary").Text!;
        Assert.True(summary.Split(", ").Length >= 2, summary);
    }

    [AvaloniaFact(DisplayName = "T-0210: Reparieren meldet „repariert ✓“ bzw. „hat nicht geklappt“")]
    public async Task Repair_reports()
    {
        var (w, _, _) = Ui.Open();
        w.InstallOverride = _ => Task.FromResult(true);
        w.Show("systemcheck");
        Button? fix = null;
        for (var i = 0; i < 200 && fix is null; i++) { await Task.Delay(20); Dispatcher.UIThread.RunJobs(); fix = w.TryFind<Button>("fix-"); }
        Assert.NotNull(fix); // im Test fehlen Programme, also gibt es „Reparieren“
        fix!.Press();
        TextBlock? msg = null;
        for (var i = 0; i < 200; i++) { await Task.Delay(20); Dispatcher.UIThread.RunJobs(); msg = w.TryFind<TextBlock>("repair-msg"); if (msg?.Text?.Contains('✓') == true) break; }
        Assert.NotNull(msg); Assert.Contains("repariert ✓", msg!.Text);
    }

    [AvaloniaFact(DisplayName = "T-0210: „Ordner wählen …“ im Assistenten öffnet den Ordnerdialog und übernimmt die Wahl")]
    public async Task Folder_pick_in_wizard()
    {
        var (w, _, root) = Ui.Open();
        var ziel = Path.Combine(root, "MeineProjekte"); Directory.CreateDirectory(ziel);
        MainWindow.PickRequest? frage = null;
        w.PickOverride = r => { frage = r; return Task.FromResult<string[]?>([ziel]); };
        w.Show("einrichtung-2");
        w.Find<Button>("wiz-folder-pickdir").Press();
        for (var i = 0; i < 20 && w.Find<TextBox>("wiz-folder").Text != ziel; i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); }
        Assert.True(frage?.Folder);
        Assert.Equal(ziel, w.Find<TextBox>("wiz-folder").Text);
        Assert.Equal(ziel, w.Settings.ProjectsFolder);
        w.Close();
    }
}
