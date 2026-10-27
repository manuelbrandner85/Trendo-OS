using Trendo.Core.Office;
using Xunit;

namespace Trendo.Core.Tests;

public class SheetSeriesTests
{
    [Fact(DisplayName = "T-0228 E1: Ausfüllen setzt Zahlen-, Wochentag-, Monats-, Datums- und Textreihen fort")]
    public void Series()
    {
        Assert.Equal(["3", "4"], SheetSeries.Continue(["1", "2"], 2));
        Assert.Equal(["20", "25"], SheetSeries.Continue(["10", "15"], 2));
        Assert.Equal(["Di", "Mi"], SheetSeries.Continue(["Mo"], 2));
        Assert.Equal(["Samstag", "Sonntag", "Montag"], SheetSeries.Continue(["Freitag"], 3));
        Assert.Equal(["Dezember", "Januar"], SheetSeries.Continue(["November"], 2));
        Assert.Equal(["02.01.2026", "03.01.2026"], SheetSeries.Continue(["01.01.2026"], 2));
        Assert.Equal(["05.01.2026"], SheetSeries.Continue(["01.01.2026", "03.01.2026"], 1));
        Assert.Equal(["Raum 2", "Raum 3"], SheetSeries.Continue(["Raum 1"], 2));
        Assert.Equal(["a", "b", "a"], SheetSeries.Continue(["a", "b"], 3));
    }

    [Fact(DisplayName = "T-0228 G1: Vorlagen rechnen ohne Fehler und enthalten keine Personennamen")]
    public void Templates()
    {
        foreach (var t in SheetTemplates.Builtin)
        {
            var d = SheetTemplates.Create(t.Id); var calc = new SheetCalc(d);
            foreach (var s in d.Sheets) foreach (var kv in s.Cells) Assert.False(calc.Value(s, kv.Key.Row, kv.Key.Col).IsError, $"{t.Id} {Addr.Name(kv.Key.Row, kv.Key.Col)}");
        }
        var h = SheetTemplates.Create("haushalt"); Assert.Equal(1160, new SheetCalc(h).Value(h.Sheets[0], "C11").Number);
    }
}
