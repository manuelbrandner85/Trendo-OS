using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// Runde 7: Wissen als Notiz-Wiki – Seiten mit [[Verweisen]] anklicken, fehlende Seite anlegen, Rückverweise, Bearbeiten, Papierkorb, „Frag dein Wissen“ (KI nachgebildet).
public class NotizWiki_Ui
{
    static async Task Until(Func<bool> ok, int ms = 6000) { for (var i = 0; i < ms / 20 && !ok(); i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } Assert.True(ok()); }

    [AvaloniaFact(DisplayName = "Notiz-Wiki: Seite mit Verweis anlegen, Verweis auf fehlende Seite legt sie an, Rückverweis führt zurück, Bearbeiten, Papierkorb und Wiederherstellen, Frag dein Wissen mit Quellen")]
    public async Task Wiki()
    {
        var (w, _, _) = Ui.Open();
        w.KiBackendsOverride = () => [new FakeKi("Dienstags.")];
        w.Show("ki-wissen");
        w.Find<TextBox>("wissen-title").Text = "Haushalt";
        w.Find<TextBox>("wissen-text").Text = "Der Müll wird dienstags abgeholt.\nSiehe auch [[Garten|unseren Garten]].";
        w.Find<Button>("wissen-add").Press();
        Assert.Equal("Haushalt", w.Wissen.Get(w.WikiPage!)!.Title);
        // Leseansicht: Verweis auf eine fehlende Seite (mit ＋)
        var link = w.Find<Button>("wissen-link-0"); Assert.Equal("＋ unseren Garten", link.Content);
        Assert.Contains("Garten", w.GetLogicalTexts().Concat(w.GetLogicalDescendantButtons()).Last(t => t.Contains("Garten")));
        link.Press();
        Assert.True(w.WikiEditing); Assert.Equal("Garten", w.Find<TextBox>("wissen-title").Text);
        w.Find<TextBox>("wissen-text").Text = "Rasen samstags mähen. Zurück zum [[Haushalt]].";
        w.Find<Button>("wissen-add").Press();
        // Rückverweis: Garten ← Haushalt
        Assert.Equal("← Haushalt", w.Find<Button>("wissen-back-0").Content);
        w.Find<Button>("wissen-back-0").Press(); Assert.Equal("Haushalt", w.Wissen.Get(w.WikiPage!)!.Title);
        Assert.Equal("unseren Garten", w.Find<Button>("wissen-link-0").Content); // jetzt vorhanden
        // Bearbeiten
        w.Find<Button>("wissen-edit").Press();
        w.Find<TextBox>("wissen-text").Text = "Der Müll wird dienstags abgeholt, Papier jeden zweiten Freitag. [[Garten]]";
        w.Find<Button>("wissen-save").Press();
        Assert.Contains("Papier", w.Wissen.ByTitle("Haushalt")!.Text); Assert.Equal("Gespeichert.", w.Find<TextBlock>("wissen-msg").Text);
        // Frag dein Wissen
        w.Find<TextBox>("wissen-ask").Text = "Wann wird der Müll abgeholt?"; w.Find<Button>("wissen-ask-go").Press();
        await Until(() => w.WikiAnswer.StartsWith("Dienstags."));
        Assert.Contains("Quellen: Haushalt", w.WikiAnswer);
        // Papierkorb
        w.Find<Button>("wissen-del").Press(); Assert.Single(w.Wissen.All().Where(i => i.Title == "Haushalt"));
        w.Find<Button>("wissen-del").Press(); Assert.Null(w.Wissen.ByTitle("Haushalt"));
        w.Find<Button>("wissen-trash").Press(); w.Find<Button>("wissen-restore-0").Press();
        Assert.NotNull(w.Wissen.ByTitle("Haushalt")); Assert.Contains("wieder da", w.Find<TextBlock>("wissen-msg").Text);
        Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "wissen.trash");
        w.Close();
    }
}

static class WikiTestExt
{
    public static IEnumerable<string> GetLogicalDescendantButtons(this Window w) =>
        Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(w).OfType<Button>().Select(b => b.Content as string).Where(s => s is not null).Select(s => s!);
}
