using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>CORE-02: Gedächtnis im Chat bedienen.</summary>
public class KiMemory_Ui
{
    [AvaloniaFact(DisplayName = "Gedächtnis: aus, bis man es einschaltet; danach merkt der Chat Aussagen, Einträge sichtbar und löschbar; alles vergessen fragt nach")]
    public async Task Flow()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.KiBackendsOverride = () => [new FakeKi("Alles klar.")];
            w.Show("ki-fragen"); W2Ui.Settle();
            await w.AskChat("Ich wohne in Köln."); W2Ui.Settle();
            Assert.Empty(w.KiMemory.All()); Assert.Equal("Einschalten", w.Find<Button>("ask-mem-toggle").Content);

            w.Find<Button>("ask-mem-toggle").Press(); W2Ui.Settle();
            Assert.True(w.KiMemory.Enabled);
            await w.AskChat("Ich wohne in Köln."); W2Ui.Settle();
            Assert.Equal(["Wohnt in Köln"], w.KiMemory.All().Select(f => f.Text));
            Assert.Equal("Wohnt in Köln", w.Find<TextBlock>("ask-mem-0").Text);

            w.Find<TextBox>("ask-mem-new").Text = "Mein Passwort ist x"; w.Find<Button>("ask-mem-add").Press(); W2Ui.Settle();
            Assert.Single(w.KiMemory.All());
            w.Find<TextBox>("ask-mem-new").Text = "Mag Tee"; w.Find<Button>("ask-mem-add").Press(); W2Ui.Settle();
            Assert.Equal(2, w.KiMemory.All().Count);

            w.Find<Button>("ask-mem-del-0").Press(); W2Ui.Settle();
            Assert.Single(w.KiMemory.All());

            w.Find<Button>("ask-mem-clear").Press(); W2Ui.Settle();
            Assert.Single(w.KiMemory.All()); Assert.Equal("Wirklich alles vergessen?", w.Find<Button>("ask-mem-clear").Content);
            w.Find<Button>("ask-mem-clear").Press(); W2Ui.Settle();
            Assert.Empty(w.KiMemory.All());

            w.Find<Button>("ask-mem-toggle").Press(); W2Ui.Settle();
            Assert.False(w.KiMemory.Enabled);
        }
        finally { w.KiMemory.SetEnabled(false); w.KiMemory.Clear(); w.Close(); }
    }
}
