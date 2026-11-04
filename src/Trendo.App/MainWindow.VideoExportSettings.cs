using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Media;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Videoschnitt X1/X2/X11/X16 (freigegeben 04.11.2026): Export-Vorlage wählen, Grafikkarten-Encoder anzeigen, nur einen Bereich,
/// Lautheit −14 LUFS – und das Projekt als EDL oder FCPXML an Resolve, Premiere oder Final Cut übergeben.</summary>
public partial class MainWindow
{
    /// <summary>Gewählte Vorlage (öffentlich für Tests und die KI).</summary>
    public string EditPreset { get; set; } = "projekt";
    public bool EditLoudness { get; set; }
    public (double From, double To) EditRange { get; set; }
    IReadOnlySet<string>? _veGpu;

    public VideoExport.Options EditExportOptions(ProgramDocument d) => new(VideoExport.Get(EditPreset), _veGpu, EditRange.From, EditRange.To, EditLoudness);

    /// <summary>Übergabe-Datei schreiben (EDL oder FCPXML) – neben die Exporte.</summary>
    public string EditInterchange(ProgramDocument d, string kind)
    {
        if (VideoEdit.Clips(d.Data).Count == 0) throw new InvalidOperationException("Die Bildspur ist leer – zuerst Videos oder Bilder hinzufügen.");
        var file = Path.Combine(WorkFolder(Programs[VK].Folder), DevStudio.SafeName(d.Title) + (kind == "edl" ? ".edl" : ".fcpxml"));
        File.WriteAllText(file, kind == "edl" ? VideoExport.Edl(d.Data, d.Title) : VideoExport.Fcpxml(d.Data, d.Title));
        _core.Audit.Append(Principal.LocalOwner.Name, "video.interchange", Path.GetFileName(file));
        _progMsg[VK] = $"Gespeichert: {file} – öffnen mit DaVinci Resolve, Premiere oder Final Cut.";
        return file;
    }

    Control EditExportSettings(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "ve-export-settings", Spacing = 6 };
        sp.Children.Add(H("Exportieren", 13));
        // Vorlagen als Kacheln: ein Klick wählt
        var grid = new WrapPanel();
        foreach (var p in VideoExport.Presets)
        {
            var on = p.Id == EditPreset; var id = p.Id;
            var b = new Button { Name = "ve-preset-" + p.Id, Width = 150, Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(8, 6), CornerRadius = new CornerRadius(8),
                Background = on ? Brush.Parse("#33F5B83D") : TileBg, BorderBrush = on ? Brush.Parse("#F5B83D") : Edge, BorderThickness = new Thickness(on ? 2 : 1), HorizontalContentAlignment = HorizontalAlignment.Left,
                Content = new StackPanel { Children = { new TextBlock { Text = p.Label, FontSize = 12.5, FontWeight = FontWeight.SemiBold, Foreground = Text }, new TextBlock { Text = p.Hint, FontSize = 10.5, Foreground = Muted, TextWrapping = TextWrapping.Wrap } } } };
            b.Click += (_, _) => { EditPreset = id; Show(VK); };
            grid.Children.Add(b);
        }
        sp.Children.Add(grid);
        var loud = new CheckBox { Name = "ve-loud", Content = "Lautheit angleichen (−14 LUFS, wie YouTube und Spotify)", IsChecked = EditLoudness, FontSize = 12 };
        loud.IsCheckedChanged += (_, _) => EditLoudness = loud.IsChecked == true;
        sp.Children.Add(loud);
        var len = VideoEdit.VideoLength(VideoEdit.Clips(d.Data));
        var range = new CheckBox { Name = "ve-range", Content = "Nur einen Bereich exportieren", IsChecked = EditRange.To > EditRange.From, FontSize = 12 };
        var ra = new NumericUpDown { Name = "ve-range-a", Value = (decimal)Math.Round(EditRange.From, 1), Minimum = 0, Maximum = (decimal)Math.Max(1, Math.Ceiling(len)), Increment = 0.5m, FormatString = "0.0", Width = 120, FontSize = 12 };
        var rb = new NumericUpDown { Name = "ve-range-b", Value = (decimal)Math.Round(EditRange.To > 0 ? EditRange.To : len, 1), Minimum = 0, Maximum = (decimal)Math.Max(1, Math.Ceiling(len)), Increment = 0.5m, FormatString = "0.0", Width = 120, FontSize = 12 };
        void SetRange() => EditRange = range.IsChecked == true ? ((double)(ra.Value ?? 0), (double)(rb.Value ?? 0)) : default;
        range.IsCheckedChanged += (_, _) => SetRange(); ra.ValueChanged += (_, _) => SetRange(); rb.ValueChanged += (_, _) => SetRange();
        sp.Children.Add(range);
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Text = "von", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center }, ra, new TextBlock { Text = "bis", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center }, rb, new TextBlock { Text = "Sekunden", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center } } });
        // Grafikkarte: einmal messen, ehrlich anzeigen
        var gpuText = _veGpu is null ? "Grafikkarte: wird beim ersten Export geprüft." : _veGpu.Count == 0 ? "Grafikkarte: kein NVENC – Trendo kodiert mit dem Prozessor (gleiche Qualität, langsamer)."
            : "Grafikkarte: " + string.Join(", ", _veGpu.Select(e => e.Replace("_nvenc", "").ToUpperInvariant().Replace("H264", "H.264").Replace("HEVC", "H.265"))) + " über NVENC – schnell.";
        var pr = VideoExport.Get(EditPreset);
        var (enc, _) = VideoExport.Encoder(pr, _veGpu, Math.Max(1, len));
        sp.Children.Add(P(gpuText + (pr.AudioOnly ? "" : $" Encoder für diese Vorlage: {enc}."), Muted, 11.5));
        var go = new Button { Name = "ve-export-go", Content = pr.AudioOnly ? "⬇ Ton exportieren" : "⬇ Exportieren", Background = Accent, Foreground = Brushes.White, FontSize = 13, Padding = new Thickness(16, 7), CornerRadius = new CornerRadius(8), IsEnabled = _veExport is null };
        go.Click += async (_, _) => await EditExportBackground(d);
        sp.Children.Add(go);
        // X16: Übergabe an andere Schnittprogramme
        sp.Children.Add(H("An anderes Schnittprogramm geben", 12.5));
        var edl = new Button { Name = "ve-edl", Content = "EDL (Resolve, Premiere, Avid)", FontSize = 12, Margin = new Thickness(0, 0, 6, 0) };
        edl.Click += (_, _) => ProgramRun(VK, () => EditInterchange(d, "edl"));
        var fcp = new Button { Name = "ve-fcpxml", Content = "FCPXML (Final Cut, Resolve)", FontSize = 12 };
        fcp.Click += (_, _) => ProgramRun(VK, () => EditInterchange(d, "fcpxml"));
        sp.Children.Add(new WrapPanel { Children = { edl, fcp } });
        return sp;
    }
}
