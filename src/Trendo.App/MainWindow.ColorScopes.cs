using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Trendo.Core;
using Trendo.Core.Media;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Paket 01 – Studio 3, S3-11 (06.11.2026): Scopes je Stück (Bild · Waveform · Vektorskop) mit Messwerten und Farbvergleich mit einem anderen Stück –
/// mit der Farbe und LUT des Stücks, wie sie im Export landen.</summary>
public partial class MainWindow
{
    /// <summary>Letzte Scopes: Stück, Bilddatei, Messwerte, Vergleich.</summary>
    public (string ClipId, string Png, ColorScopes.Stats Stats, ColorScopes.Match? Match, ColorScopes.Match? Effect)? EditScopesLast { get; private set; }
    bool _veScopesBusy;

    public async Task EditScopes(ProgramDocument d, string clipId, string? referenceClipId = null)
    {
        if (_veScopesBusy) return;
        var clips = VideoEdit.Clips(d.Data); var c = clips.FirstOrDefault(x => x.Id == clipId); if (c is null) return;
        if (MediaStudio.IsAudio(c.File)) { _progMsg[VK] = "Scopes gibt es nur für Bilder und Videos."; Show(VK); return; }
        var folder = Path.Combine(WorkFolder(Programs[VK].Folder), ".scopes"); Directory.CreateDirectory(folder);
        if (FfmpegRunner(folder, out var why) is not { } run) { _progMsg[VK] = why!; Show(VK); return; }
        var fx = VideoPro.Fx(d.Data, c.Id); var extra = VideoPro.ColorFilter(fx);
        var at = c.IsImage ? 0 : c.In + Math.Min(c.Length / 2, 2);
        var png = Path.Combine(folder, $"{c.Id}-{DateTime.Now:HHmmssfff}.png");
        var refClip = referenceClipId is null ? null : clips.FirstOrDefault(x => x.Id == referenceClipId);
        _veScopesBusy = true; _progMsg[VK] = "Scopes werden berechnet …"; Show(VK);
        try
        {
            var (stats, match, effect) = await Task.Run(() =>
            {
                if (fx.Lut.Length > 0) ColorScopes.ValidateCubeFile(fx.Lut);
                var r = run(ColorScopes.ScopeArgs(c.File, at, png, extra));
                if (!r.Ok || !File.Exists(png)) throw new InvalidOperationException("Scopes gingen nicht: " + (r.Err.Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? ""));
                var s = ColorScopes.ParseStats(run(ColorScopes.StatsArgs(c.File, at, extra)).Err) ?? throw new InvalidOperationException("Messwerte (signalstats) fehlen.");
                ColorScopes.Match? m = null;
                if (refClip is not null)
                {
                    var rat = refClip.IsImage ? 0 : refClip.In + Math.Min(refClip.Length / 2, 2);
                    var rs = ColorScopes.ParseStats(run(ColorScopes.StatsArgs(refClip.File, rat, VideoPro.ColorFilter(VideoPro.Fx(d.Data, refClip.Id)))).Err) ?? throw new InvalidOperationException("Messwerte des Vergleichsstücks fehlen.");
                    m = ColorScopes.Compare(s, rs);
                }
                // Wirkung von Farbe/LUT bildgenau gegen das unbearbeitete Bild (erkennt auch vertauschte Kanäle)
                ColorScopes.Match? e = extra.Length == 0 ? null : VideoEnhance.ParseQuality(run(ColorScopes.EquivalenceArgs(c.File, at, extra)).Err) is { } q ? ColorScopes.Equivalence(q) : null;
                return (s, m, e);
            });
            EditScopesLast = (clipId, png, stats, match, effect);
            _progMsg[VK] = match?.Text ?? (effect is not null ? "Farbe/LUT gegenüber dem Original: " + effect.Text : null) ?? $"Helligkeit Ø {stats.YAvg:0} (min {stats.YMin:0}, max {stats.YMax:0}) · Sättigung Ø {stats.SatAvg:0.0} · Farbton Ø {stats.HueAvg:0}°";
        }
        catch (Exception ex) when (ex is InvalidOperationException or FileNotFoundException or IOException or ArgumentException or System.ComponentModel.Win32Exception) { _progMsg[VK] = ex.Message; }
        finally { _veScopesBusy = false; }
        Show(VK);
    }

    Control EditClipScopes(ProgramDocument d, EditClip c)
    {
        var sp = new StackPanel { Name = "ve-scopes", Spacing = 6, Margin = new Thickness(0, 10, 0, 0) };
        sp.Children.Add(H("Scopes und Farbvergleich", 13));
        if (MediaStudio.IsAudio(c.File)) return sp;
        var others = VideoEdit.Clips(d.Data).Where(x => x.Id != c.Id && !MediaStudio.IsAudio(x.File)).ToList();
        var refBox = new ComboBox { Name = "ve-scopes-ref", ItemsSource = new[] { "Ohne Vergleich" }.Concat(others.Select(o => "Mit „" + Path.GetFileName(o.File) + "“ vergleichen")).ToList(), SelectedIndex = 0, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
        if (others.Count > 0) sp.Children.Add(refBox);
        var run = new Button { Name = "ve-scopes-run", Content = _veScopesBusy ? "Wird berechnet …" : "Waveform und Vektorskop zeigen", IsEnabled = !_veScopesBusy };
        run.Click += (_, _) => _ = EditScopes(d, c.Id, refBox.SelectedIndex > 0 ? others[refBox.SelectedIndex - 1].Id : null);
        sp.Children.Add(run);
        if (EditScopesLast is { } last && last.ClipId == c.Id)
        {
            if (File.Exists(last.Png))
                try { using var fs = File.OpenRead(last.Png); sp.Children.Add(new Image { Name = "ve-scopes-image", Source = new Bitmap(fs), Stretch = Stretch.Uniform, MaxHeight = 140 }); }
                catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException) { }
            var s = last.Stats;
            sp.Children.Add(P($"Helligkeit Ø {s.YAvg:0} (min {s.YMin:0}, max {s.YMax:0}) · Sättigung Ø {s.SatAvg:0.0} · Farbton Ø {s.HueAvg:0}°", Text, 11.5));
            if (last.Effect is { } e)
                sp.Children.Add(P("Farbe/LUT gegenüber dem Original: " + e.Text, Muted, 11));
            if (last.Match is { } m)
                sp.Children.Add(new Border { Name = "ve-scopes-match", CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 6), Background = Brush.Parse(m.Equivalent ? "#3348C774" : "#33E05A5A"), Child = P(m.Text, Text, 11.5) });
        }
        return sp;
    }
}
