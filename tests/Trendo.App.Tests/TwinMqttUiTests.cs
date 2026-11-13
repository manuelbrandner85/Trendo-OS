using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Alle Studios, Runde 5 – Geräte überwachen: „Live über MQTT“ (Empfang mit echtem Broker in TwinMqttTests).</summary>
public class TwinMqttUi
{
    [AvaloniaFact(DisplayName = "MQTT-Oberfläche: Verknüpfungen lesen (Kommentare, JSON-Feld), Fehler verständlich; Verbinden legt Sensoren an, merkt Broker ohne Passwort; Trennen")]
    public async Task Mqtt_panel()
    {
        var links = MainWindow.ParseMqttLinks("# Kommentar\nhaus/keller/temp;Keller;Temperatur\n\ntele/+/SENSOR;Steckdose;Leistung;ENERGY.Power\n");
        Assert.Equal(2, links.Count); Assert.Equal("ENERGY.Power", links[1].JsonField); Assert.Null(links[0].JsonField);
        Assert.Contains("Zeile", Assert.Throws<ArgumentException>(() => MainWindow.ParseMqttLinks("haus/#/x;A;B")).Message);
        Assert.Throws<ArgumentException>(() => MainWindow.ParseMqttLinks("# nur Kommentar"));

        var (w, _, root) = Ui.Open();
        try
        {
            w.Show("zwilling"); Assert.NotNull(w.Find<Expander>("twin-mqtt-box"));
            w.TwinMqttStart("mqtt://falsch", 1883, "", "", "haus/keller/temp;Keller;Temperatur");
            Assert.StartsWith("⚠", w.TwinMqttMessage); Assert.False(w.TwinMqttRunning);
            // Port 1 auf 127.0.0.1: niemand hört zu → Verbindung scheitert im Hintergrund, Trendo bleibt bedienbar
            w.TwinMqttStart("127.0.0.1", 1, "nutzer", "geheim", "haus/keller/temp;Keller;Temperatur");
            Assert.Contains(w.Twin.Model.Assets, a => a.Name == "Keller" && a.Sensors.Any(s => s.Name == "Temperatur"));
            var saved = File.ReadAllText(Path.Combine(w.Core.DataRoot, "zwilling", "mqtt.json"));
            Assert.Contains("127.0.0.1", saved); Assert.Contains("nutzer", saved); Assert.DoesNotContain("geheim", saved);
            for (var i = 0; i < 100 && !w.TwinMqttMessage.StartsWith('⚠'); i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(50); }
            Assert.StartsWith("MQTT:", w.TwinMqttMessage);
            w.TwinMqttStop(); Assert.False(w.TwinMqttRunning); Assert.StartsWith("MQTT getrennt", w.TwinMqttMessage);
        }
        finally { w.Close(); }
    }
}
