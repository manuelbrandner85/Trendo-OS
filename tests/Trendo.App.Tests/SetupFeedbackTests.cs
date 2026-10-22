using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Xunit;

namespace Trendo.App.Tests;

/// T-0210: Einrichtung – Wahl ist sichtbar, Reparieren meldet das Ergebnis und fragt vor dem Installieren.
public class SetupFeedbackTests
{
    [AvaloniaFact(DisplayName = "T-0210: Design-Wahl zeigt sofort Vorschau und „Gewählt“")]
    public void Theme_choice_visible()
    {
        var (w, _, _) = Ui.Open();
        w.Show("einrichtung-2");
        Assert.Contains("Aussehen: Dunkel Premium", w.Find<TextBlock>("wiz-choice").Text);
        Assert.StartsWith("✓", w.Find<RadioButton>("wiz-theme-dunkel").Content as string);
        Assert.Equal(2, w.Find<RadioButton>("wiz-theme-dunkel").BorderThickness.Left);
        w.Find<RadioButton>("wiz-theme-kontrast").IsChecked = true; Dispatcher.UIThread.RunJobs();
        Assert.Contains(w.GetLogicalTexts(), t => t == "Vorschau: Hoher Kontrast");
        Assert.Contains(w.GetLogicalTexts(), t => t.StartsWith("Gewählt: Hoher Kontrast"));
        Assert.StartsWith("✓", w.Find<RadioButton>("wiz-theme-kontrast").Content as string);
        Assert.Contains("Aussehen: Hoher Kontrast", w.Find<TextBlock>("wiz-choice").Text);
        w.Find<RadioButton>("wiz-theme-hell").IsChecked = true; Dispatcher.UIThread.RunJobs();
        Assert.Equal(Color.Parse("#0F1B2E"), Assert.IsAssignableFrom<ISolidColorBrush>(w.Find<TextBlock>("wiz-choice").Foreground).Color);
        Assert.Contains("Aussehen: Hell", w.Find<TextBlock>("wiz-choice").Text);
    }

    [AvaloniaFact(DisplayName = "T-0210: Paket-Wahl erscheint in der Zusammenfassung")]
    public void Pack_choice_in_summary()
    {
        var (w, _, _) = Ui.Open();
        w.Show("einrichtung");
        for (var i = 0; i < 3; i++) w.Find<Button>("wiz-next").Press();
        var cb = w.GetLogicalDescendants().OfType<CheckBox>().First(c => c.Name!.StartsWith("wiz-pack-") && c.IsChecked != true);
        cb.IsChecked = true; Dispatcher.UIThread.RunJobs();
        var summary = w.Find<TextBlock>("wiz-pack-summary").Text!;
        Assert.True(summary.Split(", ").Length >= 2, summary);
        Assert.Contains("Pakete:", w.Find<TextBlock>("wiz-choice").Text);
        Assert.StartsWith("✓", w.Find<CheckBox>("wiz-pack-ki").GetLogicalDescendants().OfType<TextBlock>().First().Text);
    }

    [AvaloniaFact(DisplayName = "T-0210: „Ordner wählen …“ öffnet den bestehenden Ordnerdialog")]
    public void Folder_pick_uses_existing_dialog()
    {
        var (w, _, root) = Ui.Open();
        w.Show("einrichtung-1");
        var dir = Path.Combine(root, "MeinOrdner"); Directory.CreateDirectory(dir);
        MainWindow.PickRequest? asked = null;
        w.PickOverride = r => { asked = r; return Task.FromResult<string[]?>([dir]); };
        var pick = w.Find<Button>("wiz-folder-pickdir");
        Assert.Equal("Ordner wählen …", pick.Content);
        pick.Press();
        Assert.NotNull(asked); Assert.True(asked!.Folder); Assert.Equal("wiz-folder", asked.Field);
        Assert.Equal(dir, w.Find<TextBox>("wiz-folder").Text);
        Assert.Contains("Ordner gewählt", w.Find<TextBlock>("wiz-folder-msg").Text);
        Assert.Contains(dir, w.Find<TextBlock>("wiz-choice").Text);
        w.PickOverride = _ => Task.FromResult<string[]?>(null);
        w.Find<Button>("wiz-folder-pickdir").Press();
        Assert.Contains("Kein Ordner", w.Find<TextBlock>("wiz-folder-msg").Text);
        Assert.Equal(dir, w.Find<TextBox>("wiz-folder").Text);
    }

    [AvaloniaFact(DisplayName = "T-0210: Reparieren fragt und meldet „repariert ✓“, installiert nicht von allein")]
    public async Task Repair_reports()
    {
        var (w, _, _) = Ui.Open();
        var installs = 0;
        w.InstallOverride = _ => { installs++; return Task.FromResult(true); };
        w.Show("systemcheck");
        Button? fix = null;
        for (var i = 0; i < 200 && fix is null; i++) { await Task.Delay(20); Dispatcher.UIThread.RunJobs(); fix = w.TryFind<Button>("fix-libreoffice"); }
        Assert.NotNull(fix);
        foreach (var id in new[] { "sumatra", "mpv", "7zip", "ollama", "model", "libreoffice" })
            Assert.NotNull(w.TryFind<Button>("fix-" + id));
        fix!.Press();
        TextBlock? msg = null;
        for (var i = 0; i < 200; i++) { await Task.Delay(20); Dispatcher.UIThread.RunJobs(); msg = w.TryFind<TextBlock>("repair-msg"); if (msg?.Text?.Contains("fehlt – Installieren?") == true) break; }
        Assert.NotNull(msg); Assert.Contains("LibreOffice: fehlt – Installieren?", msg!.Text);
        Assert.Equal(0, installs);
        w.Find<Button>("repair-yes").Press();
        for (var i = 0; i < 200; i++) { await Task.Delay(20); Dispatcher.UIThread.RunJobs(); msg = w.TryFind<TextBlock>("repair-msg"); if (msg?.Text?.Contains('✓') == true) break; }
        Assert.Contains("LibreOffice: repariert ✓", msg!.Text);
        Assert.Equal(1, installs);
        Button? sumatra = null;
        for (var i = 0; i < 200 && sumatra is null; i++) { await Task.Delay(20); Dispatcher.UIThread.RunJobs(); sumatra = w.TryFind<Button>("fix-sumatra"); }
        Assert.NotNull(sumatra);
        sumatra!.Press();
        for (var i = 0; i < 200; i++) { await Task.Delay(20); Dispatcher.UIThread.RunJobs(); if (w.TryFind<Button>("repair-no") is not null) break; }
        w.Find<Button>("repair-no").Press();
        Assert.Contains("nicht installiert", w.Find<TextBlock>("repair-msg").Text);
        Assert.Equal(1, installs);
    }

    [AvaloniaFact(DisplayName = "T-0210: „Jetzt prüfen und reparieren“ zeigt Fortschritt und Ergebnis")]
    public async Task Heal_reports()
    {
        var (w, _, _) = Ui.Open();
        w.StartupHeal!.Wait(5000); Dispatcher.UIThread.RunJobs();
        w.Show("heilung");
        w.Find<Button>("heal-run").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var mid = w.Find<TextBlock>("heal-summary").Text ?? "";
        Assert.Contains("Prüfung läuft", mid);
        for (var i = 0; i < 200 && !(w.Find<TextBlock>("heal-summary").Text ?? "").Contains("Gerade geprüft"); i++)
        { await Task.Delay(20); Dispatcher.UIThread.RunJobs(); }
        Assert.Contains("Gerade geprüft", w.Find<TextBlock>("heal-summary").Text);
    }
}
