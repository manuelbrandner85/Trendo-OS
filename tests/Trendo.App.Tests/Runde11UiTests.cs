using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Alle Studios, Runde 11: Artikel-Ansicht in der Recherche, Anweisungs-Test in den KI-Vorlagen.</summary>
public class Runde11_Ui
{
    const string Satz = "Wildbienen bestäuben viele Pflanzen, sie brauchen offene Böden, trockene Stängel und Blüten über das ganze Jahr hinweg. ";
    static readonly string Seite = $"<html><head><title>Bienen</title></head><body><nav><a href='/m'>Hauptmenü</a></nav><div class='story'><p>{Satz}</p><p>{Satz}</p><p>{Satz}</p></div><footer><p>Impressum der Beispiel GmbH in Musterstadt, alle Rechte vorbehalten.</p></footer></body></html>";

    [AvaloniaFact(DisplayName = "Recherche: zeigt zuerst nur den Artikel, Knopf wechselt ohne neuen Abruf zur ganzen Seite und zurück; ohne Artikel ein Hinweis")]
    public async Task Reader_toggle()
    {
        var (w, _, _) = Ui.Open();
        var calls = 0;
        w.ReaderFetchOverride = u => { calls++; return Task.FromResult(("text/html", u.Host == "kurz.example.org" ? "<title>Kurz</title><p>Inhalt</p>" : Seite, u)); };
        try
        {
            await w.ReadUrlAsync("https://example.org/a"); w.Show("recherche"); W2Ui.Settle();
            Assert.True(w.ReaderShown!.IsArticle); Assert.DoesNotContain("Impressum", w.ReaderShown.Text);
            var b = w.Find<Button>("rs-whole"); Assert.Equal("Ganze Seite zeigen", b.Content);
            b.Press(); W2Ui.Settle();
            Assert.Contains("Impressum", w.ReaderShown!.Text); Assert.Equal(1, calls);
            b = w.Find<Button>("rs-whole"); Assert.Equal("Nur den Artikel zeigen", b.Content);
            b.Press(); W2Ui.Settle();
            Assert.DoesNotContain("Impressum", w.ReaderShown!.Text); Assert.Equal(1, calls);
            await w.ReadUrlAsync("https://kurz.example.org/"); w.Show("recherche"); W2Ui.Settle();
            Assert.False(w.ReaderShown!.IsArticle); Assert.NotNull(w.TryFind<TextBlock>("rs-noarticle"));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Anweisungs-Test: falsche Regeln werden gemeldet; Ergebnis zählt Durchläufe beider Fassungen; Regeln bleiben gespeichert")]
    public void Prompt_test()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.KiBackendsOverride = () => [new FakeKi("Strom fließt durch den Draht.")];
            var t = w.Prompts.Save(null, "Erklärung", "Test", "Erkläre {{Thema}} kurz."); w.Prompts.Save(t.Id, "Erklärung", "Test", "Erkläre {{Thema}} sehr kurz.");
            w.Show("ki-vorlagen"); W2Ui.Settle();
            w.Find<Button>("prompt-" + t.Id).Press(); W2Ui.Settle();
            w.Find<TextBox>("pvar-Thema").Text = "Strom";
            w.Find<TextBox>("ptest-checks").Text = "blabla"; w.Find<Button>("ptest-run").Press(); W2Ui.Settle();
            Assert.Contains("Nicht verstanden", w.Find<TextBlock>("prompt-msg").Text);
            w.Find<TextBox>("pvar-Thema").Text = "Strom";
            w.Find<TextBox>("ptest-checks").Text = "enthält: Strom\nnicht: Draht"; w.Find<Button>("ptest-run").Press(); W2Ui.Settle();
            Assert.Equal(["Version 2", "Version 1"], w.LastPromptTest.Select(r => r.Label));
            Assert.All(w.LastPromptTest, r => { Assert.Equal(3, r.Runs.Count); Assert.Equal(0, r.Passed); });
            Assert.Contains("0 von 3", w.Find<TextBlock>("ptest-result-Version-2").Text);
            Assert.Equal("enthält: Strom\nnicht: Draht", w.Prompts.ChecksFor(t.Id));
            Assert.Equal("enthält: Strom\nnicht: Draht", w.Find<TextBox>("ptest-checks").Text);
        }
        finally { w.Close(); }
    }
}
