using System.Net;
using System.Net.Sockets;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

public class CORE09_CameraRobotUi
{
    [AvaloniaFact(DisplayName = "CORE-09 UI: Not-Aus ist da, Simulation fährt ohne Freigabe")]
    public void Simulation()
    {
        var (w, _, _) = Ui.Open();
        w.Show(MainWindow.StudioPages["CORE-09"]);
        Assert.Equal("Not-Aus", w.Find<Button>("rob-estop").Content);
        Assert.Equal(0, w.Find<ComboBox>("rob-mode").SelectedIndex);
        w.Find<Button>("rob-go").Press();
        Assert.Contains("Simulation", w.Find<TextBlock>("rob-drive-out").Text);
        Assert.Contains("Greifer", w.Find<TextBlock>("rob-pose").Text);
        Assert.Null(w.TryFind<Button>("rob-yes"));
        w.Find<Button>("rob-estop-reset").Press();
        Assert.Contains("nicht aktiv", w.Find<TextBlock>("rob-drive-out").Text);
        w.Find<ComboBox>("rob-mode").SelectedIndex = 1;
        w.Find<Button>("rob-go").Press();
        Assert.Contains("Trockenlauf", w.Find<TextBlock>("rob-drive-out").Text);
        Assert.Contains("MOVE", w.Find<TextBlock>("rob-drive-out").Text);
        w.Find<Button>("rob-run").Press();
        Assert.Contains("Trockenlauf", w.Find<TextBlock>("rob-drive-out").Text);
        Assert.Contains("MOVE", w.Find<TextBlock>("rob-drive-out").Text);
        w.Find<Button>("rob-disconnect").Press();
        Assert.Contains("kein Gerät", w.Find<TextBlock>("rob-drive-out").Text);
    }

    [AvaloniaFact(DisplayName = "CORE-09 UI: Echte Fahrt wartet auf Freigabe, Not-Aus stoppt")]
    public void LiveGate()
    {
        var (w, _, _) = Ui.Open();
        var link = new MemoryMotionLink();
        w.OpenRobotLink = _ => link;
        w.Show(MainWindow.StudioPages["CORE-09"]);
        w.Find<ComboBox>("rob-mode").SelectedIndex = 2;
        w.Find<Button>("rob-go").Press();
        Assert.Contains("Kein Gerät", w.Find<TextBlock>("rob-drive-out").Text);
        Assert.Empty(link.Sent);

        w.Find<TextBox>("rob-host").Text = "127.0.0.1:9";
        w.Find<Button>("rob-connect").Press();
        Assert.Contains("Verbunden", w.Find<TextBlock>("rob-drive-out").Text);
        w.Find<ComboBox>("rob-mode").SelectedIndex = 2;
        w.Find<Button>("rob-go").Press();
        Assert.Empty(link.Sent);
        Assert.NotNull(w.TryFind<Button>("rob-yes"));
        w.Find<Button>("rob-no").Press();
        Assert.Empty(link.Sent);
        Assert.Contains("Abgelehnt", w.Find<TextBlock>("rob-drive-out").Text);

        w.Find<Button>("rob-go").Press();
        w.Find<Button>("rob-yes").Press();
        Assert.Contains(link.Sent, s => s.StartsWith("MOVE"));
        w.Find<Button>("rob-estop").Press();
        Assert.Contains(MotionText.Stop, link.Sent);
        Assert.Contains("aktiv", w.Find<TextBlock>("rob-estop-state").Text);
        var moves = link.Sent.Count(s => s.StartsWith("MOVE"));
        w.Find<Button>("rob-go").Press();
        Assert.Contains("Not-Aus", w.Find<TextBlock>("rob-drive-out").Text);
        Assert.Equal(moves, link.Sent.Count(s => s.StartsWith("MOVE")));
        w.Find<Button>("rob-estop-reset").Press();
        w.Find<Button>("rob-grip-close").Press();
        w.Find<Button>("rob-yes").Press();
        Assert.Contains("GRIP CLOSE", link.Sent);
    }

    [AvaloniaFact(DisplayName = "CORE-09 UI: Webcam, Livebild, Foto, Aufnahme, IP-Adresse")]
    public async Task Cameras()
    {
        var (w, _, root) = Ui.Open();
        w.Webcams = new ListProbe(new CameraDevice("/dev/video9", "Labor-Kamera", CameraKind.Webcam, "/dev/video9"));
        var png = TinyPng();
        w.CameraFrames = new Still(png);
        string? recorded = null;
        w.StartCameraRecord = (_, path) => { recorded = path; return new Stop(() => File.WriteAllBytes(path, new byte[32])); };
        w.Show(MainWindow.StudioPages["CORE-09"]);
        w.Find<Button>("cam-scan").Press();
        Assert.Contains("Labor-Kamera", w.Find<Button>("cam-pick-0").Content?.ToString());
        w.Find<Button>("cam-live").Press();
        await Until(() => w.Bench.Frame is not null);
        Assert.NotNull(w.Find<Image>("cam-preview").Source);
        Assert.IsType<Bitmap>(w.Find<Image>("cam-preview").Source);
        w.Find<Button>("cam-live").Press();
        await Until(() => !w.Bench.Live);
        w.Find<Button>("cam-snap").Press();
        // Die Datei existiert schon während des Schreibens; fertig ist das Foto erst, wenn der Status es meldet
        await Until(() => (w.Find<TextBlock>("cam-status").Text ?? "").Contains("Foto gespeichert"));
        Assert.NotEmpty(Directory.GetFiles(Path.Combine(root, "fach", "roboter"), "foto-*"));
        Assert.Contains("Foto gespeichert", w.Find<TextBlock>("cam-status").Text);
        w.Find<Button>("cam-rec").Press();
        Assert.Contains("Aufnahme läuft", w.Find<TextBlock>("cam-status").Text);
        w.Find<Button>("cam-stop").Press();
        Assert.True(File.Exists(recorded));
        Assert.Contains("Aufnahme gespeichert", w.Find<TextBlock>("cam-status").Text);
        w.Find<Button>("cam-stop").Press();
        Assert.Contains("keine Aufnahme", w.Find<TextBlock>("cam-status").Text);

        w.Find<TextBox>("cam-rtsp").Text = "http://nein";
        w.Find<Button>("cam-add").Press();
        Assert.Contains("rtsp://", w.Find<TextBlock>("cam-status").Text);
        w.Find<TextBox>("cam-rtsp").Text = "rtsp://10.0.0.8/live";
        w.Find<Button>("cam-add").Press();
        Assert.Contains("10.0.0.8", w.Find<Button>("cam-pick-1").Content?.ToString());
        w.Find<Button>("cam-add").Press();
        Assert.Contains("schon in der Liste", w.Find<TextBlock>("cam-status").Text);
        w.Close(); // Fenster mit Kamera nicht offen lassen – sonst läuft es in späteren Tests weiter
    }

    [AvaloniaFact(DisplayName = "CORE-09 UI: ONVIF trägt eine Kamera ein, FFmpeg wird geprüft")]
    public async Task OnvifAndFfmpeg()
    {
        var (w, _, _) = Ui.Open();
        w.Onvif = new UiOnvif();
        w.Show(MainWindow.StudioPages["CORE-09"]);
        w.Find<Button>("cam-onvif").Press();
        await Until(() => w.TryFind<Button>("cam-pick-0") is not null || (w.Find<TextBlock>("cam-status").Text ?? "").Contains("fehlgeschlagen"));
        Assert.Contains("10.0.0.5", w.Find<Button>("cam-pick-0").Content?.ToString());
        w.Find<Button>("cam-ffmpeg").Press();
        await Until(() => w.CurrentView == "studio-deps" || (w.TryFind<TextBlock>("cam-status")?.Text ?? "").Contains("bereit"));
        Assert.True(w.CurrentView == "studio-deps" || w.Find<TextBlock>("cam-status").Text!.Contains("bereit"));
    }

    [AvaloniaFact(DisplayName = "CORE-09 UI: Echte Fahrt geht erst nach Freigabe über TCP raus, Not-Aus sendet STOP")]
    public void TcpMove()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var link = TcpMotionLink.Open($"127.0.0.1:{port}");
        using var client = listener.AcceptTcpClient();
        using var reader = new StreamReader(client.GetStream(), Encoding.ASCII);
        var (w, _, _) = Ui.Open();
        w.OpenRobotLink = _ => link;
        w.Show(MainWindow.StudioPages["CORE-09"]);
        w.Find<TextBox>("rob-host").Text = "127.0.0.1:" + port;
        w.Find<Button>("rob-connect").Press();
        w.Find<ComboBox>("rob-mode").SelectedIndex = 2;
        w.Find<Button>("rob-go").Press();
        Assert.Equal(0, client.Available);
        w.Find<Button>("rob-yes").Press();
        Assert.StartsWith("MOVE ", reader.ReadLine());
        w.Find<Button>("rob-estop").Press();
        Assert.Equal("STOP", reader.ReadLine());
        listener.Stop();
    }

    static async Task Until(Func<bool> done)
    {
        for (var i = 0; i < 50 && !done(); i++) { await Task.Delay(30); Dispatcher.UIThread.RunJobs(); }
    }

    static byte[] TinyPng()
    {
        var img = new RgbImage(2, 2, [10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120]);
        return VisionPlus.EncodePng(img);
    }

    sealed class ListProbe(CameraDevice cam) : IWebcamProbe
    {
        public IReadOnlyList<CameraDevice> List() => [cam];
    }

    sealed class Still(byte[] bytes) : IFrameSource
    {
        public Task<byte[]> GrabAsync(CameraDevice camera, CancellationToken ct) => Task.FromResult(bytes);
    }

    sealed class Stop(Action stop) : IDisposable
    {
        public void Dispose() => stop();
    }

    sealed class UiOnvif : IOnvifTransport
    {
        public Task<IReadOnlyList<string>> ProbeAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>(["<d:XAddrs>http://10.0.0.5/onvif/device_service</d:XAddrs>"]);
        public Task<string> PostAsync(string url, string soapAction, string body, CancellationToken ct) =>
            Task.FromResult(soapAction.EndsWith("GetProfiles", StringComparison.Ordinal)
                ? "<trt:Profiles token=\"P1\"></trt:Profiles>"
                : "<tt:Uri>rtsp://10.0.0.5:554/live</tt:Uri>");
    }
}
