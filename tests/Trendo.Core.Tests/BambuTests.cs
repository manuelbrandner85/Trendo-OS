using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using Trendo.Core.Print3D;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>N24: Bambu-Lab-Anbindung im LAN-Modus – MQTT-Pakete, Status, Befehle, Druckstart, Kamera-Anmeldung. Mit nachgebautem Drucker.</summary>
public class BambuTests
{
    const string Report = """{"print":{"gcode_state":"RUNNING","mc_percent":42,"mc_remaining_time":37,"layer_num":120,"total_layer_num":300,"nozzle_temper":219.8,"nozzle_target_temper":220,"bed_temper":"55.0","bed_target_temper":55,"subtask_name":"Halter"}}""";

    [Fact(DisplayName = "N24: Statusmeldungen – Teilmeldungen ergänzen den Stand, deutsche Zustandsnamen")]
    public void Status()
    {
        var s = new BambuStatus().Merge(Report);
        Assert.Equal(("Druckt", 42, 37, 120, 300, "Halter"), (s.StateText, s.Percent, s.RemainingMin, s.Layer, s.TotalLayers, s.File));
        Assert.Equal(55, s.Bed); Assert.Equal(219.8, s.Nozzle, 3);
        var t = s.Merge("""{"print":{"mc_percent":43}}""");
        Assert.Equal((43, "RUNNING", 120), (t.Percent, t.State, t.Layer));
        Assert.Same(t, t.Merge("kaputt"));
    }

    [Fact(DisplayName = "N24: Druckstart-Befehl und Kamera-Anmeldung haben das Format der Firmware")]
    public void Payloads()
    {
        var o = JsonNode.Parse(BambuClient.StartPayload("Halter.gcode.3mf", 1, false, "7"))!["print"]!;
        Assert.Equal("project_file", (string?)o["command"]); Assert.Equal("Metadata/plate_1.gcode", (string?)o["param"]);
        Assert.Equal("file:///sdcard/Halter.gcode.3mf", (string?)o["url"]); Assert.Equal("Halter", (string?)o["subtask_name"]); Assert.False((bool)o["use_ams"]!);
        var a = BambuCamera.AuthPacket("12345678");
        Assert.Equal(80, a.Length); Assert.Equal(0x40, BitConverter.ToInt32(a, 0)); Assert.Equal(0x3000, BitConverter.ToInt32(a, 4));
        Assert.Equal("bblp", Encoding.ASCII.GetString(a, 16, 4)); Assert.Equal("12345678", Encoding.ASCII.GetString(a, 48, 8));
        Assert.Contains("rtsps://bblp:12345678@192.168.1.20:322/streaming/live/1", BambuCamera.RtspArgs("192.168.1.20", "12345678", "x.jpg"));
        // Länge > 127 Byte wird mehrstellig kodiert
        var big = Mqtt.Publish("t", new string('x', 300));
        Assert.Equal(0x30, big[0]); Assert.Equal(300 + 3, (big[1] & 127) + 128 * big[2]);
    }

    [Fact(DisplayName = "N24: Verbinden mit Zugangscode, Status empfangen, Pause senden – mit nachgebautem Drucker; falscher Code wird verständlich gemeldet")]
    public async Task Fake_printer()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); var ct = cts.Token;
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var received = new List<string>(); string? user = null, pass = null;
        var server = Task.Run(async () =>
        {
            using var c = await listener.AcceptTcpClientAsync(ct); var s = c.GetStream();
            var connect = (await Mqtt.ReadAsync(s, ct))!.Value;
            // CONNECT: Protokollname(6) Level(1) Flags(1) KeepAlive(2), dann ClientId, User, Passwort
            var i = 10; string Read() { var l = (connect.Body[i] << 8) | connect.Body[i + 1]; var v = Encoding.UTF8.GetString(connect.Body, i + 2, l); i += 2 + l; return v; }
            Read(); user = Read(); pass = Read();
            await s.WriteAsync(new byte[] { 0x20, 2, 0, (byte)(pass == "geheim12" ? 0 : 5) }, ct);
            if (pass != "geheim12") return;
            var sub = (await Mqtt.ReadAsync(s, ct))!.Value; Assert.Equal(8, sub.Type);
            await s.WriteAsync(new byte[] { 0x90, 3, sub.Body[0], sub.Body[1], 0 }, ct);
            var push = (await Mqtt.ReadAsync(s, ct))!.Value; received.Add(Mqtt.ParsePublish(push.Body).Payload);
            await s.WriteAsync(Mqtt.Publish("device/01P00A000000000/report", Report), ct);
            for (var k = 0; k < 5; k++)
            {
                var p = await Mqtt.ReadAsync(s, ct); if (p is null) break;
                if (p.Value.Type == 3) { received.Add(Mqtt.ParsePublish(p.Value.Body).Payload); break; }
            }
        }, ct);
        var printer = new BambuPrinter("Werkstatt", "127.0.0.1", "01P00A000000000");
        await using (var client = new BambuClient(printer, "geheim12"))
        {
            var got = new TaskCompletionSource<BambuStatus>();
            client.Changed += st => got.TrySetResult(st);
            var tcp = new TcpClient(); await tcp.ConnectAsync(IPAddress.Loopback, port, ct);
            await client.ConnectAsync(tcp.GetStream(), ct);
            var status = await got.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            Assert.Equal(42, status.Percent);
            await client.Command("pause", ct);
            await server.WaitAsync(TimeSpan.FromSeconds(10), ct);
        }
        Assert.Equal(("bblp", "geheim12"), (user, pass));
        Assert.Contains("\"command\":\"pushall\"", received[0]);
        Assert.Contains("\"command\":\"pause\"", received[1]);

        // falscher Zugangscode
        var server2 = Task.Run(async () =>
        {
            using var c = await listener.AcceptTcpClientAsync(ct); var s = c.GetStream();
            await Mqtt.ReadAsync(s, ct); await s.WriteAsync(new byte[] { 0x20, 2, 0, 5 }, ct);
        }, ct);
        await using var bad = new BambuClient(printer, "falsch");
        var tcp2 = new TcpClient(); await tcp2.ConnectAsync(IPAddress.Loopback, port, ct);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => bad.ConnectAsync(tcp2.GetStream(), ct));
        Assert.Contains("Zugangscode", ex.Message);
        await server2;
        listener.Stop();
    }
}
