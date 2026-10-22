using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

public class LiveUiTests
{
    static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    static string Texts(MainWindow w) => string.Join("\n", w.GetLogicalTexts());

    [AvaloniaFact(DisplayName = "Live: Quellen, Vorschau und Aufnahme tun etwas, ohne von allein zu senden")]
    public void Preview_and_record_stay_local()
    {
        var (w, l, _) = Ui.Open();
        w.CaptureFactsOverride = new CaptureFacts { Videos = [("/dev/video0", "Testkamera")], Pulse = "0\talso_input\tmod\tRUN\n" };
        w.CaptureRun = (_, args) =>
        {
            var last = args[^1];
            if (last.EndsWith(".png") || last.EndsWith(".mp4") || last.EndsWith(".m4a") || last.EndsWith(".wav")) File.WriteAllBytes(last, last.EndsWith(".png") ? Png : "x"u8.ToArray());
            return (0, "");
        };
        w.Show(MainWindow.StudioPages["SPEC-15"]);
        Assert.Empty(w.CaptureLog);
        Assert.Empty(l.Opened);
        w.Find<Button>("live-scan").Press();
        Assert.Contains("Testkamera", w.Find<TextBlock>("live-sources").Text);
        w.Find<Button>("live-src-0").Press();
        w.Find<Button>("live-preview").Press();
        Assert.Contains("v4l2", w.CaptureLog[^1]);
        Assert.DoesNotContain("rtmp", w.CaptureLog[^1]);
        Assert.NotNull(w.Find<Image>("live-preview-image"));
        w.Find<Button>("live-rec").Press();
        Assert.Contains(".mp4", w.CaptureLog[^1]);
        Assert.Contains("Aufnahme gespeichert", Texts(w));
        w.Find<Button>("live-win-add").Press();
        Assert.Contains("Linux", Texts(w));
        Assert.Empty(l.Opened);
    }

    [AvaloniaFact(DisplayName = "Live: Sendung startet erst nach Freigabe, Schlüssel nicht im Protokoll")]
    public void Broadcast_needs_approval()
    {
        var (w, l, root) = Ui.Open();
        const string key = "geheim-schluessel";
        w.CaptureFactsOverride = new CaptureFacts { Videos = [("/dev/video0", "Testkamera")] };
        w.CaptureRun = (_, _) => (0, "");
        w.Show("sendung");
        w.Find<Button>("live-scan").Press();
        w.Find<Button>("live-src-0").Press();
        w.Find<Button>("live-go").Press();
        Assert.Empty(w.CaptureLog);
        Assert.Contains("nichts gesendet", Texts(w));

        w.Find<TextBox>("live-key").Text = key;
        w.Find<Button>("live-ask").Press();
        Assert.Empty(w.CaptureLog);
        Assert.Contains("Freigaben", Texts(w));
        var id = Assert.Single(w.Core.Approvals.Pending()).Id;
        w.Show("approvals");
        Assert.Equal("Freigeben", w.Find<Button>("btn-approve-" + id).Content);
        w.Find<Button>("btn-approve-" + id).Press();
        Assert.Empty(l.Opened);
        Assert.Empty(w.CaptureLog);

        w.Show("sendung");
        w.Find<TextBox>("live-key").Text = key;
        w.Find<Button>("live-go").Press();
        Assert.Contains(key, w.CaptureLog[^1]);
        Assert.Contains("rtmp://a.rtmp.youtube.com/live2/" + key, w.CaptureLog[^1]);
        Assert.DoesNotContain(w.Core.Audit.ReadAll(), e => e.Detail.Contains(key));
        Assert.Contains(w.Core.Audit.ReadAll(), e => e.Action == "live.started");
        w.Find<Button>("live-go").Press();
        Assert.Contains("nichts gesendet", Texts(w));
        _ = root;
    }

    [AvaloniaFact(DisplayName = "Live: OBS schreibt den Schlüssel erst nach Freigabe und startet dann")]
    public void Obs_starts_only_after_approval()
    {
        var (w, l, root) = Ui.Open();
        const string key = "obs-schluessel-1";
        var profile = Path.Combine(root, "obs", "basic", "profiles", "Unbenannt");
        Directory.CreateDirectory(profile);
        var service = Path.Combine(profile, "service.json");
        File.WriteAllText(service, """{"type":"rtmp_common","settings":{"server":"auto","bwtest":false}}""");
        w.ObsConfigOverride = Path.Combine(root, "obs");
        w.AppPathOverride = id => id == "obs" ? "/fake/obs" : null;
        w.CaptureFactsOverride = new CaptureFacts { Videos = [("/dev/video0", "Testkamera")] };
        w.Show("sendung");
        w.Find<Button>("live-scan").Press();
        w.Find<Button>("live-src-0").Press();
        w.Find<ComboBox>("live-via").SelectedIndex = 1;
        w.Find<TextBox>("live-key").Text = key;
        w.Find<Button>("live-ask").Press();
        Assert.DoesNotContain(key, File.ReadAllText(service));
        Assert.Empty(l.Opened);
        var id = Assert.Single(w.Core.Approvals.Pending()).Id;
        w.Core.Approvals.Approve(id, Principal.LocalOwner);
        w.Show("sendung");
        w.Find<ComboBox>("live-via").SelectedIndex = 1;
        w.Find<TextBox>("live-key").Text = key;
        w.Find<Button>("live-go").Press();
        var saved = File.ReadAllText(service);
        Assert.Contains(key, saved);
        Assert.Contains("bwtest", saved);
        Assert.Contains(l.Opened, x => x.Contains("/fake/obs") && x.Contains("--startstreaming"));
        Assert.DoesNotContain(l.Opened, x => x.Contains(key));
        Assert.DoesNotContain(w.Core.Audit.ReadAll(), e => e.Detail.Contains(key));
    }
}
