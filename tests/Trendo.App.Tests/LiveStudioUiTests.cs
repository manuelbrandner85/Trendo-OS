using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Live streamen wie OBS (Profi 27, 05.11.2026): Szenen, Einblendungen, Tonmischer mit Pegeln und Ton-Wache, Probelauf, Sendeplan – FFmpeg nachgebildet.</summary>
public class LiveStudio_Ui
{
    sealed class Cap : ICaptureProcess { public bool Running { get; set; } = true; public string Tail { get; set; } = ""; public void Stop() => Running = false; }
    sealed class Factory : ICaptureProcessFactory
    {
        public List<IReadOnlyList<string>> Args { get; } = [];
        public ICaptureProcess Start(string ffmpeg, IReadOnlyList<string> arguments) { Args.Add([.. arguments]); return new Cap(); }
    }
    static async Task Settle() { for (var i = 0; i < 30; i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(10); } }

    [AvaloniaFact(DisplayName = "Live streamen wie OBS: Szene wählen und anlegen, Bauchbinde einblenden (im FFmpeg-Graph), Tonmischer mit Lautstärke und Stumm, Pegel und Aussetzer-Warnung, Probelauf mit Ampel, Sendeplan")]
    public async Task Scenes_mixer_probe_plan()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1100;
        var factory = new Factory(); w.CaptureFactory = factory;
        var ff = Path.Combine(root, "ffmpeg-fake"); Directory.CreateDirectory(root); File.WriteAllText(ff, "x"); w.LiveFfmpegPath = ff;
        w.FindDevices = _ => new CaptureDevices([new DeviceItem("USB-Kamera", "USB-Kamera")], [new DeviceItem("Tischmikrofon", "Tischmikrofon")]);
        MainWindow.LiveProbeSeconds = 0;
        try
        {
            w.Show("sendung"); await Settle();
            Assert.Null(w.TryFind<StackPanel>("live-studio")); // Ablauf-Ansicht: Live schmal rechts
            w.Find<Button>("live-open-studio").Press(); await Settle();
            Assert.Equal("studio", w.BcMode); Assert.NotNull(w.TryFind<StackPanel>("live-studio"));
            Assert.Equal(4, w.LiveShowState.Scenes.Count); Assert.NotNull(w.TryFind<Button>("live-scene-start"));
            // Szene „Gleich geht's los“ hat eine Bauchbinde – Vorschau zeigt sie im Graph
            w.Find<Button>("live-scene-start").Press(); await Settle();
            Assert.Equal("start", w.LiveShowState.Active);
            w.Find<Button>("live-preview-start").Press(); await Settle();
            var fc = factory.Args[^1][factory.Args[^1].ToList().IndexOf("-filter_complex") + 1];
            if (Trendo.Core.Media.VideoEdit.FontFile() is not null) Assert.Contains("Gleich geht", fc);
            w.Find<Button>("live-stop").Press(); await Settle();
            // neue Szene mit Bauchbinde
            w.Find<TextBox>("live-scene-name").Text = "Interview"; w.Find<Button>("live-scene-add").Press(); await Settle();
            Assert.Equal("Interview", w.LiveShowState.Current!.Name);
            w.Find<TextBox>("live-overlay-value").Text = "Erika Muster – Gast"; w.Find<Button>("live-overlay-add").Press(); await Settle();
            Assert.Single(w.LiveShowState.Current!.Overlays);
            // Mikrofon dazu, Tonmischer: stumm schalten und wieder an
            w.Find<CheckBox>("live-mic").IsChecked = true; w.Find<Button>("live-mic-search").Press(); await Settle();
            w.Find<Button>("live-mix-mikrofon-mute").Press(); await Settle(); Assert.True(w.LiveShowState.Mix.MicMute);
            w.Find<Button>("live-mix-mikrofon-mute").Press(); await Settle(); Assert.False(w.LiveShowState.Mix.MicMute);
            // Aufnahme: Mikrofon im Mischer mit Pegel-Datei; Pegel zeigt Aussetzer als Warnung
            w.Find<Button>("live-record").Press(); await Settle();
            var rec = string.Join(" ", factory.Args[^1]);
            Assert.Contains("audio=Tischmikrofon", rec); Assert.Contains("ebur128", rec); Assert.Contains("[aout]", rec);
            var meter = Path.Combine(Path.GetTempPath(), "trendo-live-pegel", "mikrofon.txt");
            File.WriteAllText(meter, string.Concat(Enumerable.Range(0, 70).Select(i => $"frame:{i} pts:{i} pts_time:{(i * 0.1).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}\nlavfi.r128.M={(i < 5 ? "-20" : "-inf")}\n")));
            w.LiveMeterTick();
            Assert.Contains("kein Ton", w.LiveAudioWarning); Assert.True(w.Find<TextBlock>("live-audio-warn").IsVisible);
            w.Find<Button>("live-stop").Press(); await Settle();
            // Probelauf: Aufnahme kurz, dann Ampel aus der Auswertung
            w.ProbeAnalyzeOverride = f => { File.WriteAllBytes(f, new byte[2_000_000]); return "time=00:00:08.00\nmean_volume: -21.0 dB\nmax_volume: -3.5 dB"; };
            await w.LiveProbeRun(); await Settle();
            Assert.NotNull(w.LiveProbeResult); Assert.All(w.LiveProbeResult!, c => Assert.Equal("gruen", c.Light));
            Assert.Contains("Probelauf bestanden", w.Find<TextBlock>("live-status").Text);
            // Sendeplan übernehmen und Punkt umschalten
            w.Find<TextBox>("live-plan-text").Text = "00:00 Gleich geht's los – Begrüßung\n01:30 Interview – Gast vorstellen";
            w.Find<Button>("live-plan-set").Press(); await Settle();
            Assert.Equal(2, w.LiveShowState.Plan.Count);
            w.Find<Button>("live-plan-go-90").Press(); await Settle();
            Assert.Equal("Interview", w.LiveShowState.Current!.Name);
            w.Find<TextBox>("live-plan-text").Text = "00:00 Talkshow"; w.Find<Button>("live-plan-set").Press(); await Settle();
            Assert.Contains("Talkshow", w.Find<TextBlock>("live-status").Text);
            // gespeichert
            Assert.Equal(2, LiveShow.Load(Fach.StoreDir(w.Core.DataRoot, "sendung")).Plan.Count);
        }
        finally { MainWindow.LiveProbeSeconds = 8; w.Close(); }
    }
}
