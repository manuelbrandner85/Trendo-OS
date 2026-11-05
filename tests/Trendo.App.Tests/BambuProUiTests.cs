using System.Net;
using System.Net.Sockets;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Print3D;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Bambu Lab voll aus Trendo (B1–B5 + WLAN, 05.11.2026) mit nachgebautem Drucker im Heimnetz.</summary>
public class BambuPro_Ui
{
    static void Pump() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    const string Serial = "01S00C123456789";
    const string Report = """{"print":{"gcode_state":"RUNNING","mc_percent":40,"wifi_signal":"-58dBm","spd_lvl":2,"lights_report":[{"node":"chamber_light","mode":"off"}],"hms":[{"attr":50331904,"code":196609}],"ams":{"tray_now":"0","ams":[{"id":"0","tray":[{"id":"0","tray_type":"PLA","tray_color":"2F86F6FF","remain":70}]}]}}}""";

    [AvaloniaFact(DisplayName = "CORE-05 B1–B5: Drucker finden und übernehmen, Einrichtung mit WLAN-Schritten, verbinden, Licht/Tempo/Düse steuern, AMS und Meldungen sehen, Fertig-Meldung, mehrere Drucker, vorbereiten und mit Zeitraffer drucken")]
    public async Task Bambu_voll()
    {
        var (w, _, root) = Ui.Open(); var ct = TestContext.Current.CancellationToken;
        try
        {
            w.Show("3d-druck"); Pump();
            // B1: finden und übernehmen
            w.DiscoverOverride = () => Task.FromResult(new List<BambuFound> { new("127.0.0.1", Serial, "C12", "Werkstatt", "lan", false) });
            w.Find<Button>("print-find").Press(); for (var i = 0; i < 20 && w.PrintersFound.Count == 0; i++) { Pump(); await Task.Delay(20, ct); }
            Assert.Single(w.PrintersFound); w.Show("3d-druck"); Pump();
            w.Find<Button>("print-found-0").Press(); Pump();
            Assert.Equal(("Werkstatt", "127.0.0.1", Serial), (w.Printer!.Name, w.Printer.Ip, w.Printer.Serial));
            // B2/WLAN: Einrichtung
            w.Find<Button>("print-setup").Press(); Pump();
            Assert.Contains("WLAN", string.Join("|", w.GetLogicalTexts())); Assert.Contains("Entwicklermodus", string.Join("|", w.GetLogicalTexts()));
            // B5: zweiter Drucker in der Liste
            w.SavePrinter("Büro", "127.0.0.2", "03W00A2B3C4D5", "zweiter1");
            w.SavePrinter("Werkstatt", "127.0.0.1", Serial, "abc12345");
            Assert.Equal(2, w.PrinterList.Count);
            // nachgebauter Drucker
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var commands = new List<string>(); NetworkStream? srv = null;
            var server = Task.Run(async () =>
            {
                using var c = await listener.AcceptTcpClientAsync(ct); var s = c.GetStream(); srv = s;
                await Mqtt.ReadAsync(s, ct); await s.WriteAsync(new byte[] { 0x20, 2, 0, 0 }, ct);
                var sub = (await Mqtt.ReadAsync(s, ct))!.Value; await s.WriteAsync(new byte[] { 0x90, 3, sub.Body[0], sub.Body[1], 0 }, ct);
                await Mqtt.ReadAsync(s, ct);
                await s.WriteAsync(Mqtt.Publish($"device/{Serial}/report", Report), ct);
                while (await Mqtt.ReadAsync(s, ct) is { } p) if (p.Type == 3) { lock (commands) commands.Add(Mqtt.ParsePublish(p.Body).Payload); if (commands.Count == 4) break; }
            }, ct);
            Assert.True(await w.ConnectPrinter(async () => { var t = new TcpClient(); await t.ConnectAsync(IPAddress.Loopback, port, ct); return t.GetStream(); }));
            for (var i = 0; i < 100 && w.PrinterDetails?.Trays.Count != 1; i++) { Pump(); await Task.Delay(20, ct); }
            w.Show("3d-druck"); Pump();
            // B4: Werte und Steuerung
            Assert.StartsWith("WLAN gut", w.PrinterDetails!.WifiText);
            Assert.NotNull(w.TryFind<WrapPanel>("print-ams")); Assert.NotNull(w.TryFind<Button>("print-hms-0"));
            w.Find<Button>("print-light").Press(); Pump();
            await w.PrinterSpeed(3); Pump();
            w.Find<TextBox>("print-nozzle").Text = "215"; w.Find<Button>("print-nozzle-set").Press(); Pump();
            for (var i = 0; i < 100 && commands.Count < 3; i++) { Pump(); await Task.Delay(20, ct); }
            lock (commands)
            {
                Assert.Contains(commands, c => c.Contains("\"ledctrl\"") && c.Contains("\"on\""));
                Assert.Contains(commands, c => c.Contains("\"print_speed\"") && c.Contains("\"3\""));
                Assert.Contains(commands, c => c.Contains("M104 S215"));
            }
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "3ddruck.steuerung");
            // B4: Fertig-Meldung
            w.PrinterClient!.Feed("""{"print":{"gcode_state":"FINISH","mc_percent":100,"subtask_name":"Halter"}}"""); Pump();
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "3ddruck.fertig");
            // B3: vorbereiten und mit Zeitraffer drucken
            var stl = Path.Combine(root, "halter.stl"); File.WriteAllText(stl, "solid x\nendsolid x\n");
            w.SliceOverride = (model, out3mf) => { File.WriteAllBytes(out3mf, [1, 2, 3]); return Task.FromResult(true); };
            w.Open3dPrint(stl); Pump();
            w.Find<Button>("print-prepare").Press(); for (var i = 0; i < 50 && !(w.PrintFile ?? "").EndsWith(".gcode.3mf"); i++) { Pump(); await Task.Delay(20, ct); }
            Assert.EndsWith("halter.gcode.3mf", w.PrintFile);
            w.AskPrint(); w.Show("3d-druck"); Pump();
            w.Find<CheckBox>("print-confirm-timelapse").IsChecked = true; Pump();
            await w.ConfirmPrint(true, _ => Task.CompletedTask);
            await server.WaitAsync(TimeSpan.FromSeconds(10), ct);
            lock (commands) Assert.Contains(commands, c => c.Contains("\"project_file\"") && c.Contains("\"timelapse\":true") && c.Contains("\"use_ams\":true"));
            // B5: Liste zeigt den Zustand, Auswählen wechselt den Drucker
            w.Show("3d-druck"); Pump();
            Assert.NotNull(w.TryFind<StackPanel>("print-fleet"));
            var other = w.PrinterList.ToList().FindIndex(p => p.Serial != Serial);
            w.Find<Button>($"print-fleet-sel-{other}").Press(); Pump();
            Assert.Equal("Büro", w.Printer!.Name); Assert.Null(w.PrinterClient); // der zweite ist nicht verbunden
            w.Find<Button>($"print-fleet-del-{other}").Press(); Pump();
            Assert.Single(w.PrinterList); Assert.Null(w.Core.Secrets.Get(new BambuPrinter("", "", "03W00A2B3C4D5").SecretKey));
            listener.Stop();
        }
        finally { w.DiscoverOverride = null; w.SliceOverride = null; await w.DisconnectPrinter(); w.Close(); }
    }
}
