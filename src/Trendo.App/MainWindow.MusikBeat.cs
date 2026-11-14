using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Media;

namespace Trendo.App;

/// <summary>Paket 01 – Studio 4, S4-13 (06.11.2026): Modus „Beat“ in Trendo Musik – Step-Sequencer wie ein Drumcomputer (16tel-Raster je Spur),
/// Tempo, Swing, Takte, Grundakkord und Arpeggiator. „Als Lied speichern“ schreibt WAV 24 Bit und das Muster (JSON) und legt es in die Bibliothek.</summary>
public partial class MainWindow
{
    bool _msBeat;
    public StepSequencer.Pattern MsBeatPattern { get; set; } = StepSequencer.Edm();

    public void MsBeatToggle(string voice, int step)
    {
        var p = MsBeatPattern; var t = p.Tracks.FirstOrDefault(x => x.Id == voice);
        if (t is null) { t = new StepSequencer.Track(voice, new string('.', p.Steps)); p = p with { Tracks = [.. p.Tracks, t] }; }
        var s = t.Steps.ToCharArray(); s[step] = s[step] == '.' ? 'x' : s[step] == 'x' ? 'X' : '.'; // aus → an → betont → aus
        MsBeatPattern = p with { Tracks = [.. p.Tracks.Select(x => x.Id == voice ? x with { Steps = new string(s) } : x)] };
    }

    /// <summary>Muster rendern und als Lied speichern; ungültige Muster → Meldung mit Grund, nichts gespeichert.</summary>
    public MusicSong? MsBeatSave(string title)
    {
        var p = StepSequencer.Validate(MsBeatPattern);
        var folder = Path.Combine(WorkFolder("Musik"), "Beats"); Directory.CreateDirectory(folder);
        var name = DevStudio.SafeName(title.Length > 0 ? title : $"Beat {p.Bpm:0} BPM");
        var wav = Path.Combine(folder, name + ".wav");
        for (var i = 2; File.Exists(wav); i++) wav = Path.Combine(folder, $"{name} {i}.wav");
        var a = StepSequencer.Render(p);
        AudioFile.WriteWav(a, wav, 24);
        File.WriteAllText(Path.ChangeExtension(wav, ".beat.json"), StepSequencer.ToJson(p).ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        var song = Songs.Add(new MusicSong { Title = title.Length > 0 ? title : Path.GetFileNameWithoutExtension(wav), Style = "Beat, Step-Sequencer", Lyrics = "[Instrumental]", Seconds = Math.Round(a.Seconds, 2), Bpm = (int)Math.Round(p.Bpm), Key = p.Chord, TimeSignature = "4/4", File = wav, Kind = "beat" });
        _core.Audit.Append(Principal.LocalOwner.Name, "musik.beat", Path.GetFileName(wav));
        _msSel = song.Id; _msMsg = $"Beat gespeichert: {Path.GetFileName(wav)} ({a.Seconds:0.0} s) – in der Bibliothek abspielbar, Muster daneben als .beat.json.";
        return song;
    }

    Control MsBeatPanel()
    {
        var sp = new StackPanel { Name = "ms-beat", Spacing = 8 };
        var p = MsBeatPattern;
        NumericUpDown Num(string name, double v, double min, double max, double inc) => new() { Name = name, Value = (decimal)v, Minimum = (decimal)min, Maximum = (decimal)max, Increment = (decimal)inc, FormatString = "0", Width = 96, FontSize = 12 };
        var bpm = Num("ms-beat-bpm", p.Bpm, 40, 300, 1); var bars = Num("ms-beat-bars", p.Bars, 1, 512, 1);
        var chord = new TextBox { Name = "ms-beat-chord", Text = p.Chord, Width = 70 };
        var swing = new Slider { Name = "ms-beat-swing", Minimum = 0, Maximum = 0.75, Value = p.Swing };
        sp.Children.Add(new WrapPanel { Children = { new TextBlock { Text = "Tempo ", VerticalAlignment = VerticalAlignment.Center, Foreground = Muted }, bpm, new TextBlock { Text = "  Takte ", VerticalAlignment = VerticalAlignment.Center, Foreground = Muted }, bars } });
        sp.Children.Add(new WrapPanel { Children = { new TextBlock { Text = "Akkord ", VerticalAlignment = VerticalAlignment.Center, Foreground = Muted }, chord } });
        sp.Children.Add(new TextBlock { Text = "Swing", Foreground = Muted, FontSize = 11 }); sp.Children.Add(swing);
        // Raster: je Spur 16 Felder, Viertel abgesetzt wie bei einem Drumcomputer
        var grid = new StackPanel { Name = "ms-beat-grid", Spacing = 3 };
        foreach (var (id, label) in StepSequencer.Voices)
        {
            var t = p.Tracks.FirstOrDefault(x => x.Id == id); var steps = t?.Steps ?? new string('.', p.Steps);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
            row.Children.Add(new TextBlock { Text = label, Width = 70, FontSize = 10.5, Foreground = Text, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
            for (var i = 0; i < steps.Length; i++)
            {
                var k = i; var c = steps[i];
                var b = new Button { Name = $"ms-beat-{id}-{i}", Width = 14, Height = 18, Padding = new Thickness(0), CornerRadius = new CornerRadius(3), Margin = new Thickness(i % 4 == 0 && i > 0 ? 3 : 0, 0, 0, 0),
                    Background = c switch { 'X' => Brush.Parse("#F5B83D"), 'x' => Brush.Parse("#B8862B"), _ => Brush.Parse(i % 4 == 0 ? "#2A313C" : "#1E242D") }, BorderThickness = new Thickness(0) };
                ToolTip.SetTip(b, $"{label} · Schritt {i + 1}");
                b.Click += (_, _) => { MsBeatToggle(id, k); Show(MS); };
                row.Children.Add(b);
            }
            grid.Children.Add(row);
        }
        sp.Children.Add(grid);
        // Arpeggiator
        var arpOn = new CheckBox { Name = "ms-beat-arp", Content = "Arpeggiator", IsChecked = p.Arp.On };
        var modes = new[] { ("up", "auf"), ("down", "ab"), ("updown", "auf und ab") };
        var arpMode = new ComboBox { Name = "ms-beat-arp-mode", ItemsSource = modes.Select(m => m.Item2).ToList(), SelectedIndex = Math.Max(0, Array.FindIndex(modes, m => m.Item1 == p.Arp.Mode)), MinWidth = 100 };
        var arpOct = Num("ms-beat-arp-oct", p.Arp.Octaves, 1, 3, 1);
        sp.Children.Add(new WrapPanel { Children = { arpOn, new Border { Width = 6 }, arpMode, new Border { Width = 6 }, arpOct } });
        var title = new TextBox { Name = "ms-beat-title", Watermark = "Name (optional)" };
        sp.Children.Add(title);
        void Read() => MsBeatPattern = MsBeatPattern with
        {
            Bpm = (double)(bpm.Value ?? 128), Bars = (int)(bars.Value ?? 4), Swing = Math.Round(swing.Value, 2), Chord = (chord.Text ?? "").Trim(),
            Arp = MsBeatPattern.Arp with { On = arpOn.IsChecked == true, Mode = modes[Math.Max(0, arpMode.SelectedIndex)].Item1, Octaves = (int)(arpOct.Value ?? 2), Chord = (chord.Text ?? "").Trim() },
        };
        var save = new Button { Name = "ms-beat-save", Content = "Als Lied speichern (WAV)", Background = Accent, Foreground = Brushes.White };
        save.Click += (_, _) =>
        {
            try { Read(); MsBeatSave((title.Text ?? "").Trim()); }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException) { _msMsg = "Beat nicht gespeichert: " + Trendo.Core.FriendlyError.Say(ex); }
            Show(MS);
        };
        var edm = new Button { Name = "ms-beat-edm", Content = "Vorlage EDM" };
        edm.Click += (_, _) => { MsBeatPattern = StepSequencer.Edm(); Show(MS); };
        var clear = new Button { Name = "ms-beat-clear", Content = "Leeren" };
        clear.Click += (_, _) => { MsBeatPattern = MsBeatPattern with { Tracks = [.. MsBeatPattern.Tracks.Select(t => t with { Steps = new string('.', MsBeatPattern.Steps), Automation = null })] }; Show(MS); };
        sp.Children.Add(new WrapPanel { Children = { save, new Border { Width = 6 }, edm, new Border { Width = 6 }, clear } });
        sp.Children.Add(P("Klick: an → betont → aus. Alle Klänge erzeugt Trendo selbst – keine fremden Samples.", Muted, 10.5));
        return sp;
    }
}
