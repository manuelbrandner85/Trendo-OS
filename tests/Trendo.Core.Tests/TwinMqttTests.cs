using System.Net;
using System.Net.Sockets;
using MQTTnet;
using MQTTnet.Server;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Alle Studios, Runde 5 – SPEC-10: Live-Messwerte über MQTT (MQTTnet 5.2, MIT). Echter Lauf mit einem MQTTnet-Broker im Testprozess auf 127.0.0.1.</summary>
public class TwinMqttTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "trendo-mqtt-" + Guid.NewGuid().ToString("N"));
    public TwinMqttTests() => Directory.CreateDirectory(_root);
    public void Dispose() { try { Directory.Delete(_root, true); } catch (IOException) { } }

    [Fact(DisplayName = "MQTT +1: Themen-Filter nach MQTT-Regeln (+ eine Ebene, # Rest)")]
    public void Matches()
    {
        Assert.True(TwinMqtt.Matches("haus/keller/temp", "haus/keller/temp"));
        Assert.True(TwinMqtt.Matches("shelly/+/status", "shelly/plug1/status")); Assert.False(TwinMqtt.Matches("shelly/+/status", "shelly/a/b/status"));
        Assert.True(TwinMqtt.Matches("haus/#", "haus/keller/temp")); Assert.True(TwinMqtt.Matches("haus/#", "haus"));
        Assert.False(TwinMqtt.Matches("haus/keller", "haus/keller/temp"));
    }

    [Fact(DisplayName = "MQTT +2: Messwert als Zahl (Komma/Punkt) oder JSON-Feld mit Pfad (Tasmota „ENERGY.Power“)")]
    public void Parse()
    {
        Assert.Equal(21.5, TwinMqtt.ParseValue("21,5")); Assert.Equal(21.5, TwinMqtt.ParseValue(" 21.5 "));
        Assert.Equal(53.2, TwinMqtt.ParseValue("{\"ENERGY\":{\"Power\":53.2}}", "ENERGY.Power"));
        Assert.Equal(7.0, TwinMqtt.ParseValue("{\"wert\":\"7\"}", "wert")); Assert.Equal(1.0, TwinMqtt.ParseValue("{\"an\":true}", "an"));
    }

    [Fact(DisplayName = "MQTT −1: Unsinn wird verworfen – Text, fehlendes Feld, kaputtes JSON, unendlich")]
    public void Parse_refuses()
    {
        Assert.Null(TwinMqtt.ParseValue("an")); Assert.Null(TwinMqtt.ParseValue("{\"a\":1}", "b")); Assert.Null(TwinMqtt.ParseValue("{kaputt", "a"));
        Assert.Null(TwinMqtt.ParseValue("Infinity")); Assert.Null(TwinMqtt.ParseValue("{\"a\":{\"b\":[1]}}", "a.b"));
    }

    [Fact(DisplayName = "MQTT −2: ungültige Filter, Rechner und Ports werden abgelehnt")]
    public void Validation()
    {
        Assert.False(TwinMqtt.ValidFilter("haus/#/temp")); Assert.False(TwinMqtt.ValidFilter("haus/te+mp")); Assert.False(TwinMqtt.ValidFilter("")); Assert.False(TwinMqtt.ValidFilter("a\u0000b"));
        Assert.True(TwinMqtt.ValidFilter("+/status/#"));
        Assert.True(TwinMqtt.ValidHost("192.168.1.20")); Assert.True(TwinMqtt.ValidHost("broker.local"));
        Assert.False(TwinMqtt.ValidHost("mqtt://broker")); Assert.False(TwinMqtt.ValidHost("a b")); Assert.False(TwinMqtt.ValidPort(0)); Assert.False(TwinMqtt.ValidPort(70000));
    }

    [Fact(DisplayName = "MQTT +3: Nachricht landet bei allen passenden Sensoren, unpassende bleiben leer")]
    public void Apply()
    {
        var hub = new TwinHub(_root); hub.Define("Keller", "Temperatur", "°C"); hub.Define("Steckdose", "Leistung", "W");
        var links = new[] { new MqttLink("haus/keller/temp", "Keller", "Temperatur"), new MqttLink("tele/+/SENSOR", "Steckdose", "Leistung", "ENERGY.Power") };
        Assert.Equal(1, TwinMqtt.Apply(hub, links, "haus/keller/temp", "18,4", new DateTime(2026, 11, 8, 10, 0, 0)));
        Assert.Equal(1, TwinMqtt.Apply(hub, links, "tele/plug1/SENSOR", "{\"ENERGY\":{\"Power\":120}}", new DateTime(2026, 11, 8, 10, 0, 1)));
        Assert.Equal(0, TwinMqtt.Apply(hub, links, "andere/sache", "5", DateTime.Now));
        Assert.Equal([18.4, 120.0], hub.Model.Readings.Select(r => r.Value));
        Assert.Equal(2, new TwinHub(_root).Model.Readings.Count); // gespeichert
    }

    [Fact(DisplayName = "MQTT −3: Verbinden mit ungültigen Angaben scheitert sofort, ohne Netzwerk")]
    public async Task Listen_refuses()
    {
        var hub = new TwinHub(_root);
        await Assert.ThrowsAsync<ArgumentException>(() => TwinMqtt.ListenAsync(hub, "http://x", 1883, [new MqttLink("a", "A", "S")], CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => TwinMqtt.ListenAsync(hub, "localhost", 1883, [], CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => TwinMqtt.ListenAsync(hub, "localhost", 1883, [new MqttLink("a/#/b", "A", "S")], CancellationToken.None));
    }

    static int FreePort() { var l = new TcpListener(IPAddress.Loopback, 0); l.Start(); var p = ((IPEndPoint)l.LocalEndpoint).Port; l.Stop(); return p; }

    [Fact(DisplayName = "MQTT echt: Broker auf 127.0.0.1, Gerät sendet Zahl und Tasmota-JSON → Zwilling hat die Werte mit Ampel")]
    public async Task Real_broker()
    {
        var port = FreePort();
        var server = new MqttServerFactory().CreateMqttServer(new MqttServerOptionsBuilder().WithDefaultEndpoint().WithDefaultEndpointBoundIPAddress(IPAddress.Loopback).WithDefaultEndpointPort(port).Build());
        await server.StartAsync();
        try
        {
            var hub = new TwinHub(_root); hub.Define("Keller", "Temperatur", "°C", warnAbove: 25, alarmAbove: 30); hub.Define("Steckdose", "Leistung", "W");
            var links = new[] { new MqttLink("haus/keller/temp", "Keller", "Temperatur"), new MqttLink("tele/+/SENSOR", "Steckdose", "Leistung", "ENERGY.Power") };
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20)); var got = 0;
            var listen = TwinMqtt.ListenAsync(hub, "127.0.0.1", port, links, cts.Token, (_, n) => Interlocked.Add(ref got, n));
            await Task.Delay(800); // abonniert
            var factory = new MqttClientFactory(); using var device = factory.CreateMqttClient();
            await device.ConnectAsync(new MqttClientOptionsBuilder().WithTcpServer("127.0.0.1", port).WithClientId("geraet").Build());
            await device.PublishStringAsync("haus/keller/temp", "26,5");
            await device.PublishStringAsync("tele/plug1/SENSOR", "{\"ENERGY\":{\"Power\":53.2,\"Today\":0.4}}");
            await device.PublishStringAsync("haus/garten/temp", "12");
            for (var i = 0; i < 50 && Volatile.Read(ref got) < 2; i++) await Task.Delay(100);
            cts.Cancel(); await listen;
            Assert.Equal(2, got);
            var states = hub.States();
            var keller = states.Single(s => s.Asset == "Keller"); Assert.Equal(26.5, keller.Last); Assert.Equal(TwinLevel.Warnung, keller.Level);
            Assert.Equal(53.2, states.Single(s => s.Asset == "Steckdose").Last);
        }
        finally { await server.StopAsync(); server.Dispose(); }
    }
}
