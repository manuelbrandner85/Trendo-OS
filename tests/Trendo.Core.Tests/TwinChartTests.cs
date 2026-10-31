using Trendo.Core.Arch;
using Xunit;
namespace Trendo.Core.Tests;

/// <summary>T-0265: Anlagen-Monitor – Beispielanlagen, Zeitdiagramm mit Grenzen/Ereignissen/Prognose, SVG.</summary>
public class T0265_TwinChart
{
    [Fact(DisplayName = "T-0265: Beispielanlagen – 7 Tage alle 15 min, Kühlraum steigt am letzten Tag in die Warnung, Prognose sagt Alarm voraus")]
    public void Demo_seed()
    {
        using var f = new Fixture(); var now = new DateTime(2026, 10, 31, 12, 0, 0);
        var hub = new TwinHub(f.Root);
        var n = TwinDemo.Seed(hub, now);
        Assert.Equal(5 * (7 * 96 + 1), n);
        var st = hub.States(); Assert.Equal(5, st.Count);
        var cold = st.Single(s => s.Asset == "Kühlraum");
        Assert.Equal(TwinLevel.Warnung, cold.Level); Assert.True(cold.TrendPerHour > 0); Assert.InRange(cold.HoursToAlarm!.Value, 1, 48);
        Assert.Equal(TwinLevel.Ok, st.Single(s => s.Sensor == "Rücklauf").Level); // ohne Grenzen immer Ok
        Assert.InRange(TwinPlus.EnergyKwh(TwinPlus.Series(hub.Model, "Solaranlage", "Leistung")), 150, 400); // 7 Tage × 6,5 kW Spitze, halber Sinus über 12 h
        Assert.Equal(5 * (7 * 96 + 1), new TwinHub(f.Root).Model.Readings.Count); // gespeichert
    }

    [Fact(DisplayName = "T-0265: Diagramm – Grenzlinien, Verlauf höchstens 2 Punkte je Pixel, Ampelwechsel mit Hysterese, Prognose-Linie; Zeitraum 24 h; SVG gültig")]
    public void Chart()
    {
        using var f = new Fixture(); var now = new DateTime(2026, 10, 31, 12, 0, 0);
        var hub = new TwinHub(f.Root); TwinDemo.Seed(hub, now);
        var sh = TwinChart.Build(hub.Model, "Kühlraum", "Temperatur", 600, 300, "7d", 24);
        Assert.Equal(3, sh.OfType<DLine>().Count(l => l.Layer == "GRENZE")); // Warnung 6, Alarm 8, Alarm unter −2
        var line = sh.OfType<DPoly>().Single(p => p.Layer == "VERLAUF"); Assert.True(line.Pts.Count <= 2 * 600, $"{line.Pts.Count}");
        Assert.Single(sh.OfType<DLine>(), l => l.Layer == "PROGNOSE");
        var events = sh.OfType<DPoly>().Count(p => p.Layer == "EREIGNIS"); Assert.InRange(events, 1, 3); // kein Flattern an der Grenze
        var day = TwinChart.Build(hub.Model, "Kühlraum", "Temperatur", 600, 300, "24h");
        Assert.Contains(day.OfType<DText>(), t => t.Layer == "ACHSE" && t.Text.Contains(':')); // Uhrzeiten statt Tage
        Assert.Contains(TwinChart.Build(hub.Model, "Heizung", "fehlt", 600, 300).OfType<DText>(), t => t.Text == "Noch keine Werte");
        Assert.NotEmpty(TwinChart.Spark(hub.Model, "Solaranlage", "Leistung", 180, 30, "#4ADE80"));
        System.Xml.Linq.XDocument.Parse(TwinChart.Svg(hub.Model, "Heizung", "Vorlauf", "7d", 12));
    }
}
