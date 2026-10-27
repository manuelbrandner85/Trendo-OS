using System.Net;
using System.Net.Sockets;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core.Cad;
using Trendo.Core.Print3D;
using Xunit;
namespace Trendo.App.Tests;

/// <summary>N23 Trendo CAD und N24 Trendo 3D-Druck (Bambu Lab) – Oberfläche.</summary>
public class N23N24_CadPrintUi
{
    static void Pump() { for (var i = 0; i < 5; i++) Dispatcher.UIThread.RunJobs(); }

    [AvaloniaFact(DisplayName = "N23: Bauschritte über das Menüband, Maß als Formel ändern, Parameter ändern rechnet neu, Rückgängig, Export 3MF/STEP/Zeichnung")]
    public void Cad_ui()
    {
        var (w, _, _) = Ui.Open();
        w.Show("cad"); Pump();
        Assert.NotNull(w.TryFind<CadView>("cad-view"));
        Assert.Equal(4, w.Cad.Bodies[0].Features.Count);
        w.CadSelect(0, 3); w.Find<Button>("cad-add-bohrung").Press(); Pump();
        Assert.Equal(CadOp.Bohrung, w.CadSelected!.Op);
        var tb = w.Find<TextBox>("cad-p-durchmesser"); tb.Text = "Breite/10"; tb.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter }); Pump();
        Assert.Equal("Breite/10", w.CadSelected!["durchmesser"]);
        Assert.Contains("60 × 40 × 8", w.Find<TextBlock>("cad-status").Text);
        w.CadSetParam("Breite", 90); w.Show("cad"); Pump();
        Assert.Contains("90 × 40 × 8", w.Find<TextBlock>("cad-status").Text);
        w.Find<Button>("cad-undo").Press(); Pump();
        Assert.Equal(60, w.Cad.Params["Breite"]);
        w.Find<Button>("cad-add-body").Press(); Pump();
        Assert.Equal(2, w.Cad.Bodies.Count);
        foreach (var kind in new[] { "3mf", "step", "pdf", "stl" }) Assert.True(File.Exists(w.CadExportAs(kind)));
        w.Close();
    }

    [AvaloniaFact(DisplayName = "N24: Drucker eintragen (Zugangscode geschützt), mit nachgebautem Drucker verbinden, Status sehen; Druck nur nach Bestätigung")]
    public async Task Print_ui()
    {
        var (w, _, _) = Ui.Open(); var core = w.Core;
        var ct = TestContext.Current.CancellationToken;
        // aus Trendo CAD weitergeben
        w.Show("3d-druck"); Pump();
        w.Find<Button>("print-from-cad").Press(); Pump();
        Assert.EndsWith(".3mf", w.PrintFile);
        w.SavePrinter("Werkstatt", "127.0.0.1", "01P00A123456789", "abc12345");
        var json = File.ReadAllText(Path.Combine(core.DataRoot, "3d-druck", "drucker.json"));
        Assert.DoesNotContain("abc12345", json);
        Assert.Equal("abc12345", core.Secrets.Get(w.Printer!.SecretKey));
        Assert.Throws<ArgumentException>(() => w.SavePrinter("X", "8.8.8.8", "01P00A123456789", null)); // nur Heimnetz
        // nachgebauter Drucker
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var commands = new List<string>();
        var server = Task.Run(async () =>
        {
            using var c = await listener.AcceptTcpClientAsync(ct); var s = c.GetStream();
            await Mqtt.ReadAsync(s, ct); await s.WriteAsync(new byte[] { 0x20, 2, 0, 0 }, ct);
            var sub = (await Mqtt.ReadAsync(s, ct))!.Value; await s.WriteAsync(new byte[] { 0x90, 3, sub.Body[0], sub.Body[1], 0 }, ct);
            await Mqtt.ReadAsync(s, ct);
            await s.WriteAsync(Mqtt.Publish("device/01P00A123456789/report", """{"print":{"gcode_state":"IDLE","mc_percent":0,"nozzle_temper":24,"bed_temper":23}}"""), ct);
            while (await Mqtt.ReadAsync(s, ct) is { } p) if (p.Type == 3) { commands.Add(Mqtt.ParsePublish(p.Body).Payload); if (commands.Count == 1) break; }
        }, ct);
        Assert.True(await w.ConnectPrinter(async () => { var t = new TcpClient(); await t.ConnectAsync(IPAddress.Loopback, port, ct); return t.GetStream(); }));
        for (var i = 0; i < 100 && w.PrinterStatus?.State != "IDLE"; i++) { Pump(); await Task.Delay(20, ct); }
        w.Show("3d-druck"); Pump();
        Assert.Equal("Bereit", w.PrinterStatus!.StateText);
        Assert.Contains("Bereit", w.Find<TextBlock>("print-state").Text);
        // .3mf aus CAD ist noch nicht druckfertig
        w.AskPrint(); Assert.False(w.PrintConfirmOpen);
        // druckfertige Datei: erst Bestätigung, dann Hochladen und Start
        var ready = Path.Combine(Path.GetTempPath(), "trendo-test-halter.gcode.3mf"); File.WriteAllBytes(ready, [1, 2, 3]);
        w.Open3dPrint(ready); w.AskPrint(); w.Show("3d-druck"); Pump();
        Assert.True(w.PrintConfirmOpen);
        Assert.NotNull(w.TryFind<Border>("print-confirm"));
        Assert.Empty(commands);
        string? uploaded = null;
        await w.ConfirmPrint(true, f => { uploaded = f; return Task.CompletedTask; });
        await server.WaitAsync(TimeSpan.FromSeconds(10), ct);
        Assert.Equal(ready, uploaded);
        Assert.Contains("\"command\":\"project_file\"", commands.Single());
        Assert.Contains("trendo-test-halter.gcode.3mf", commands.Single());
        await w.DisconnectPrinter(); listener.Stop(); File.Delete(ready);
        w.Close();
    }
}
