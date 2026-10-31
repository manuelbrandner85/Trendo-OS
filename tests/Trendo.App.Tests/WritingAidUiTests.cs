using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core.Office;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Trendo Text – Schreibhilfe: Regeln im Kern und Ja/Nein in der Oberfläche.</summary>
public class WritingAid_Ui
{
    static TextDoc Doc(params string[] paras) => new() { Blocks = [.. paras.Select(t => (Block)new Para { Text = t })] };

    [Fact(DisplayName = "Schreibhilfe-Regeln: doppeltes Wort, Leerzeichen, Satzzeichen, Anführungszeichen, Schreibweisen, Füllwort, langer Satz; Anwenden prüft die Stelle")]
    public void Rules()
    {
        var d = Doc("Das ist ist gut  so , oder?", "Er sagte \"hallo\" per Email, z.B. morgen.", "Das ist eigentlich klar.",
            string.Join(" ", Enumerable.Repeat("Wort", 32)) + ".");
        var h = WritingAid.Check(d);
        Assert.Contains(h, x => x.Kind == "doppelt" && x.Replacement == "ist");
        Assert.Contains(h, x => x.Kind == "leer" && x.Replacement == " ");
        Assert.Contains(h, x => x.Kind == "satzzeichen" && x.Original == " ,");
        Assert.Contains(h, x => x.Kind == "anfuehrung" && x.Replacement == "„hallo“");
        Assert.Contains(h, x => x.Kind == "schreibweise" && x.Replacement == "E-Mail");
        Assert.Contains(h, x => x.Kind == "schreibweise" && x.Replacement == "z. B.");
        Assert.Contains(h, x => x.Kind == "fuellwort" && x.Original.Trim() == "eigentlich");
        Assert.Contains(h, x => x.Kind == "lang" && x.Replacement is null);
        var fix = h.First(x => x.Kind == "anfuehrung"); Assert.True(WritingAid.Apply(d, fix)); Assert.Contains("„hallo“", ((Para)d.Blocks[1]).Text);
        Assert.False(WritingAid.Apply(d, fix)); // Stelle stimmt nicht mehr
        Assert.Empty(WritingAid.Check(Doc("Alles in Ordnung, z. B. diese E-Mail.")));
    }

    [AvaloniaFact(DisplayName = "Schreibhilfe UI: Ja korrigiert (Strg+Z-fähig), Nein bleibt gemerkt, langer Satz wird markiert")]
    public void Yes_no_in_text()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1000;
        w.TextNew(); w.TextDocument.Blocks = [new Para { Text = "Wir treffen uns uns morgen per Email." }]; w.TextAssistOn = true; w.Show("text"); Settle();
        var first = w.TextHints()[0]; Assert.Equal("doppelt", first.Kind);
        var b = V<Button>(w, "sugg-yes-0"); b.BringIntoView(); Settle(); RealClick(w, V<Button>(w, "sugg-yes-0"));
        Assert.Equal("Wir treffen uns morgen per Email.", ((Para)w.TextDocument.Blocks[0]).Text);
        var no = V<Button>(w, "sugg-no-0"); no.BringIntoView(); Settle(); RealClick(w, V<Button>(w, "sugg-no-0")); // „Email“ bleibt
        Assert.Empty(w.TextHints()); Assert.Contains("Email", ((Para)w.TextDocument.Blocks[0]).Text);
        w.TextUndo(); Assert.Equal("Wir treffen uns uns morgen per Email.", ((Para)w.TextDocument.Blocks[0]).Text);
    }
}
