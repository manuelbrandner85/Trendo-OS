using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

public class CameraRobotUiTests
{
    static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
    static string Texts(MainWindow w) => string.Join("\n", w.GetLogicalTexts());

    [AvaloniaFact(DisplayName = "Kameras: Not-Aus ist sichtbar, Livebild und Aufnahme tun etwas, RTSP wird geprüft")]
    public void Cameras_preview_record_and_estop()
    {
        var (w, _, root) = Ui.Open();
        w.CameraFactsOverride = new CameraFacts { Videos = [("/dev/video0", "Testkamera")] };
        w.RigRun = (_, args) =>
        {
            var last = args[^1];
            if (!last.Contains("rtsp://", StringComparison.Ordinal) && !last.StartsWith("/dev/", StringComparison.Ordinal))
                File.WriteAllBytes(last, last.EndsWith(".png") ? Png : "film"u8.ToArray());
            return (0, "");
        };
        w.Show(MainWindow.StudioPages["CORE-09"]);
        Assert.Equal("NOT-AUS", w.Find<Button>("rob-estop").Content);
        w.Find<Button>("cam-scan").Press();
        Assert.Contains("Testkamera", w.Find<TextBlock>("cam-list").Text);
        w.Find<Button>("cam-pick-0").Press();
        w.Find<Button>("cam-live").Press();
        Assert.Contains("v4l2", w.RigLog[^1]);
        Assert.NotNull(w.Find<Image>("cam-preview-image"));
        w.Find<Button>("cam-rec").Press();
        Assert.Contains("-t", w.RigLog[^1]);
        Assert.Contains("10", w.RigLog[^1]);
        Assert.Contains("Aufnahme gespeichert", Texts(w));
        w.Find<Button>("cam-add").Press();
        Assert.Contains("rtsp://", Texts(w));
        w.Find<TextBox>("cam-rtsp").Text = "rtsp://10.0.0.8/live";
        w.Find<Button>("cam-add").Press();
        Assert.Contains("Wohnzimmer", w.Find<TextBlock>("cam-list").Text);
        Assert.True(File.Exists(Path.Combine(root, "fach", "kameras", "kameras.json")));
        w.Find<Button>("rob-estop").Press();
        Assert.Contains("NOT-AUS", Texts(w));
        Assert.Contains("Not-Aus ist aktiv", w.Find<TextBlock>("rob-estop-state").Text);
    }

    [AvaloniaFact(DisplayName = "Roboter: Probelauf sendet nichts, echt schreibt erst nach Freigabe, Not-Aus stoppt")]
    public void Robot_dry_run_then_real_move()
    {
        var (w, l, root) = Ui.Open();
        var port = Path.Combine(root, "ttyUSB0");
        File.WriteAllText(port, "");
        w.Show("roboter");
        w.Find<Button>("rob-dry").Press();
        Assert.Contains("Probelauf", w.Find<TextBlock>("rob-journal").Text);
        Assert.Contains("nichts gesendet", Texts(w));
        Assert.Equal("", File.ReadAllText(port));
        w.Find<Button>("rob-sim").Press();
        Assert.Contains("150,0", Texts(w));
        Assert.Equal("", File.ReadAllText(port));

        w.Find<TextBox>("rob-port").Text = port;
        w.Find<Button>("rob-send").Press();
        Assert.Contains("nichts gesendet", Texts(w));
        Assert.Equal("", File.ReadAllText(port));
        w.Find<Button>("rob-ask").Press();
        Assert.Contains("Freigaben", Texts(w));
        var id = Assert.Single(w.Core.Approvals.Pending()).Id;
        w.Show("approvals");
        w.Find<Button>("btn-approve-" + id).Press();
        Assert.Empty(l.Opened);
        w.Show("roboter");
        w.Find<TextBox>("rob-port").Text = port;
        w.Find<Button>("rob-send").Press();
        Assert.Equal("MOVE Arm 0 45 -30 2 halten\n", File.ReadAllText(port));
        w.Find<Button>("rob-send").Press();
        Assert.Contains("nicht mehr", Texts(w));
        Assert.Equal("MOVE Arm 0 45 -30 2 halten\n", File.ReadAllText(port));

        w.Find<Button>("rob-ask").Press();
        w.Core.Approvals.Approve(Assert.Single(w.Core.Approvals.Pending()).Id, Principal.LocalOwner);
        w.Show("roboter");
        w.Find<Button>("rob-estop").Press();
        w.Find<TextBox>("rob-port").Text = port;
        File.WriteAllText(port, "");
        w.Find<Button>("rob-send").Press();
        Assert.Contains("Not-Aus", Texts(w));
        Assert.Equal("", File.ReadAllText(port));
    }

    [AvaloniaFact(DisplayName = "ONVIF: Lesen übernimmt die RTSP-Adresse und schreibt das Kennwort nicht ins Protokoll")]
    public async Task Onvif_read_hides_password()
    {
        var (w, _, _) = Ui.Open();
        const string pass = "kamera-kennwort";
        w.OnvifFetch = (host, user, pw) =>
        {
            Assert.Equal(pass, pw);
            Assert.Equal("192.168.0.20", host);
            _ = user;
            return Task.FromResult("rtsp://192.168.0.20/live");
        };
        w.Show("roboter");
        w.Find<TextBox>("cam-onvif-host").Text = "192.168.0.20";
        w.Find<TextBox>("cam-onvif-user").Text = "anna";
        w.Find<TextBox>("cam-onvif-pass").Text = pass;
        w.Find<Button>("cam-onvif").Press();
        for (var i = 0; i < 50 && !Texts(w).Contains("RTSP-Adresse"); i++)
        {
            await Task.Delay(20);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }
        Assert.Contains("rtsp://192.168.0.20/live", Texts(w));
        Assert.DoesNotContain(w.Core.Audit.ReadAll(), e => e.Detail.Contains(pass));
        Assert.Contains("ONVIF", w.Find<TextBlock>("cam-list").Text);
    }
}
