using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

public class SPEC15_LiveUi
{
    sealed class Cap : ICaptureProcess
    {
        public bool Running { get; set; } = true;
        public string Tail { get; set; } = "";
        public bool Stopped { get; private set; }
        public void Stop() { Stopped = true; Running = false; }
    }

    sealed class Factory : ICaptureProcessFactory
    {
        public List<IReadOnlyList<string>> Args { get; } = [];
        public List<Cap> All { get; } = [];
        public ICaptureProcess Start(string ffmpeg, IReadOnlyList<string> arguments)
        {
            Args.Add(arguments.ToList());
            var c = new Cap();
            All.Add(c);
            return c;
        }
    }

    sealed class Runner : IProcessRunner
    {
        public List<(string F, string A)> Calls { get; } = [];
        public Task<(int, string)> RunAsync(string file, string args, Action<string>? line = null, CancellationToken ct = default)
        {
            Calls.Add((file, args));
            return Task.FromResult((0, "ok"));
        }
    }

    static async Task Settle()
    {
        for (var i = 0; i < 40; i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(15); }
    }

    static string FakeFfmpeg(string root)
    {
        var p = Path.Combine(root, "ffmpeg-fake");
        File.WriteAllText(p, "x");
        return p;
    }

    static void Arm(MainWindow w, string root, Factory factory)
    {
        w.CaptureFactory = factory;
        w.LiveFfmpegPath = FakeFfmpeg(root);
        w.FindWindows = () => [new WindowGrab("Editor", 10, 20, 800, 600, false)];
        w.FindDevices = _ => new CaptureDevices([new DeviceItem("/dev/video0", "USB-Kamera")], [new DeviceItem("mic-in", "Tischmikrofon")]);
    }

    static string Files(string root) => string.Join("\n", Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Select(File.ReadAllText));

    [AvaloniaFact(DisplayName = "SPEC-15 UI: Vorschau, Aufnahme und Stopp tun etwas, ohne von allein zu senden")]
    public async Task Preview_record_stop()
    {
        var (w, _, root) = Ui.Open();
        var factory = new Factory();
        Arm(w, root, factory);
        w.Show("sendung");
        Assert.Contains("von allein", w.Find<TextBlock>("live-status").Text);
        w.Find<Button>("live-stop").Press();
        Assert.Contains("nichts", w.Find<TextBlock>("live-status").Text);
        Assert.Empty(factory.Args);

        w.Find<Button>("live-preview-start").Press();
        await Settle();
        Assert.Equal(LiveMode.Preview, w.Live.Mode);
        Assert.Contains(factory.Args[0], a => a is "gdigrab" or "x11grab");
        Assert.DoesNotContain(factory.Args[0], a => a.Contains("rtmp", StringComparison.OrdinalIgnoreCase));
        w.Find<Button>("live-stop").Press();
        Assert.True(factory.All[0].Stopped);
        Assert.Equal(LiveMode.Idle, w.Live.Mode);

        w.Find<Button>("live-record").Press();
        await Settle();
        Assert.Equal(LiveMode.Recording, w.Live.Mode);
        Assert.Contains(factory.Args[1], a => a.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(factory.Args[1], a => a.Contains("rtmp", StringComparison.OrdinalIgnoreCase));
        var file = w.Live.OutputFile!;
        File.WriteAllBytes(file, [0, 1, 2]);
        w.Find<Button>("live-open-file").Press();
        w.Find<Button>("live-stop").Press();
        w.Close();
    }

    [AvaloniaFact(DisplayName = "SPEC-15 UI: Fenster, Kamera und Mikrofon lassen sich wählen")]
    public async Task Sources()
    {
        var (w, _, root) = Ui.Open();
        var factory = new Factory();
        Arm(w, root, factory);
        w.Show("sendung");
        w.Find<Button>("live-src-window").Press();
        w.Find<Button>("live-windows-search").Press();
        Assert.Contains("Editor", w.Find<ComboBox>("live-windows").Items.Cast<object>().First().ToString());
        w.Find<Button>("live-preview-start").Press();
        await Settle();
        var preview = string.Join(" ", factory.Args[0]);
        if (OperatingSystem.IsWindows()) Assert.Contains("title=Editor", preview);
        else Assert.Contains("800x600", preview);
        w.Find<Button>("live-stop").Press();

        w.Find<Button>("live-src-camera").Press();
        w.Find<Button>("live-cam-search").Press();
        await Settle();
        Assert.Contains("USB-Kamera", w.Find<ComboBox>("live-cameras").Items.Cast<object>().First().ToString());
        w.Find<CheckBox>("live-mic").IsChecked = true;
        w.Find<Button>("live-mic-search").Press();
        await Settle();
        Assert.Contains("Tischmikrofon", w.Find<ComboBox>("live-mic-list").Items.Cast<object>().First().ToString());
        w.Find<Button>("live-record").Press();
        await Settle();
        var rec = string.Join(" ", factory.Args[^1]);
        Assert.Contains("/dev/video0", rec);
        Assert.Contains("mic-in", rec);
        w.Find<Button>("live-stop").Press();
        w.Close();
    }

    [AvaloniaFact(DisplayName = "SPEC-15 UI: Live startet erst nach Freigabe, Schlüssel bleibt aus Dateien und Protokoll")]
    public async Task Go_live_needs_approval()
    {
        var (w, _, root) = Ui.Open();
        var factory = new Factory();
        Arm(w, root, factory);
        const string key = "yt-test-key-1234";
        w.Show("sendung");
        w.Find<TextBox>("live-key").Text = key;
        w.Find<Button>("live-key-save").Press();
        Assert.Equal("", w.Find<TextBox>("live-key").Text);
        Assert.Equal(key, w.Core.Secrets.Get("stream-key:youtube"));
        Assert.DoesNotContain(key, Files(root));

        w.Find<Button>("live-ask").Press();
        Assert.Empty(factory.Args);
        Assert.Contains("nicht von allein", w.Find<TextBlock>("live-status").Text);
        Assert.Single(w.Core.Approvals.Pending());
        w.Find<Button>("live-no").Press();
        Assert.Empty(factory.Args);
        Assert.Contains("abgelehnt", w.Find<TextBlock>("live-status").Text);
        Assert.Empty(w.Core.Approvals.Pending());

        w.Find<Button>("live-ask").Press();
        var id = w.Core.Approvals.Pending().Single(r => r.Action == LiveBroadcast.Action).Id;
        w.Show("approvals");
        w.Find<Button>("btn-approve-" + id).Press();
        Assert.Empty(factory.Args);
        Assert.Equal("sendung", w.CurrentView);
        Assert.Contains("nicht von allein", w.Find<TextBlock>("live-status").Text);
        w.Find<Button>("live-send").Press();
        await Settle();
        Assert.Equal(LiveMode.Broadcasting, w.Live.Mode);
        Assert.Contains(factory.Args[0], a => a == "rtmp://a.rtmp.youtube.com/live2/" + key);
        Assert.DoesNotContain(key, w.Find<TextBlock>("live-status").Text);
        Assert.DoesNotContain(key, Files(root));
        w.Find<Button>("live-stop").Press();
        Assert.Equal(LiveMode.Idle, w.Live.Mode);

        w.Find<ComboBox>("live-platform").SelectedIndex = 2;
        w.Find<TextBox>("live-rtmp").Text = "rtmps://example.com/live";
        w.Find<TextBox>("live-key").Text = "custom-key-99";
        w.Find<CheckBox>("live-copy").IsChecked = true;
        w.Find<Button>("live-ask").Press();
        w.Find<Button>("live-yes").Press();
        await Settle();
        Assert.Contains(factory.Args[^1], a => a == "rtmps://example.com/live/custom-key-99");
        Assert.Contains(factory.Args[^1], a => a.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("custom-key-99", Files(root));
        w.Find<Button>("live-stop").Press();
        w.Close();
    }

    [AvaloniaFact(DisplayName = "SPEC-15 UI: FFmpeg wird erst nach Zustimmung installiert")]
    public async Task Ffmpeg_asks_before_install()
    {
        var (w, _, root) = Ui.Open();
        var factory = new Factory();
        w.CaptureFactory = factory;
        w.LiveFfmpegPath = "";
        w.Show("sendung");
        w.Find<Button>("live-preview-start").Press();
        await Settle();
        Assert.NotNull(w.TryFind<Button>("live-ffmpeg-yes"));
        Assert.False(File.Exists(Path.Combine(root, "deps-consent.txt")));
        Assert.Empty(factory.Args);
        w.Find<Button>("live-ffmpeg-later").Press();
        Assert.False(File.Exists(Path.Combine(root, "deps-consent.txt")));
        Assert.Null(w.TryFind<Button>("live-ffmpeg-yes"));
        w.Find<Button>("live-preview-start").Press();
        await Settle();
        var runner = new Runner();
        w.Runner = runner;
        w.Find<Button>("live-ffmpeg-yes").Press();
        await Settle();
        Assert.True(File.Exists(Path.Combine(root, "deps-consent.txt")));
        Assert.Contains(runner.Calls, c => c.A.Contains("Gyan.FFmpeg", StringComparison.Ordinal));
        Assert.Empty(factory.Args);
        w.Close();
    }

    [AvaloniaFact(DisplayName = "SPEC-15 UI: offline kein Stream, Aufnahme bleibt")]
    public async Task Offline_blocks_broadcast_only()
    {
        var (w, _, root) = Ui.Open(offline: true);
        var factory = new Factory();
        Arm(w, root, factory);
        w.Show("sendung");
        w.Find<TextBox>("live-key").Text = "yt-test-key-1234";
        w.Find<Button>("live-ask").Press();
        Assert.Contains("Offline", w.Find<TextBlock>("live-status").Text);
        Assert.Empty(w.Core.Approvals.Pending());
        Assert.Empty(factory.Args);
        w.Find<Button>("live-record").Press();
        await Settle();
        Assert.Equal(LiveMode.Recording, w.Live.Mode);
        w.Find<Button>("live-stop").Press();
        w.Close();
    }
}
