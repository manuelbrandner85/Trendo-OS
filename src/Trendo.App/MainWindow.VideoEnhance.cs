using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Media;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Paket 01 – Studio 3, S3-12 (06.11.2026): „Bild verbessern“ je Stück im Videoschnitt – Wackeln ausgleichen, entrauschen, schärfen, 2× größer.
/// Ergebnis wird gemessen (SSIM/PSNR zum Original); erst „Verbesserte Fassung verwenden“ tauscht das Stück aus (rückgängig machbar).</summary>
public partial class MainWindow
{
    /// <summary>Letztes Ergebnis von „Bild verbessern“ (Stück-ID und Messung).</summary>
    public (string ClipId, VideoEnhance.Result Result)? EditEnhanceLast { get; private set; }
    bool _veEnhBusy;

    Func<IReadOnlyList<string>, Cli.Result>? FfmpegRunner(string cwd, out string? why)
    {
        why = null;
        if (FfmpegRunOverride is { } fake) return fake;
        if (FfmpegPath is not { } ff) { why = "Dafür fehlt FFmpeg – unter Zusatzprogramme einrichten."; return null; }
        if (_opt.Isolated) { why = "Im abgeschotteten Testmodus startet Trendo kein FFmpeg."; return null; }
        return args => Cli.Run(ff, args, cwd, 30 * 60_000);
    }

    public async Task EditEnhance(ProgramDocument d, string clipId, VideoEnhance.Options o)
    {
        if (_veEnhBusy) return;
        var c = VideoEdit.Clips(d.Data).FirstOrDefault(x => x.Id == clipId);
        if (c is null) return;
        if (c.IsImage || MediaStudio.IsAudio(c.File)) { _progMsg[VK] = "Bild verbessern geht nur bei Videos."; Show(VK); return; }
        if (!o.Any) { _progMsg[VK] = "Erst wählen, was verbessert werden soll."; Show(VK); return; }
        var folder = WorkFolder(Programs[VK].Folder);
        var output = Path.Combine(folder, Path.GetFileNameWithoutExtension(c.File) + " – verbessert.mp4");
        for (var i = 2; File.Exists(output); i++) output = Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(c.File)} – verbessert {i}.mp4");
        if (FfmpegRunner(folder, out var why) is not { } run) { _progMsg[VK] = why!; Show(VK); return; }
        _veEnhBusy = true; _progMsg[VK] = "Bild wird verbessert und gemessen …"; Show(VK);
        try
        {
            var r = await Task.Run(() => VideoEnhance.Enhance(run, c.File, output, o));
            EditEnhanceLast = (clipId, r);
            _core.Audit.Append(Principal.LocalOwner.Name, "video.enhance", Path.GetFileName(c.File) + " → " + (r.Verdict.MayApply ? Path.GetFileName(r.Output) : "verworfen"));
            _progMsg[VK] = r.Verdict.Text;
        }
        catch (Exception ex) when (ex is InvalidOperationException or FileNotFoundException or IOException or ArgumentException or System.ComponentModel.Win32Exception) { _progMsg[VK] = ex.Message; }
        finally { _veEnhBusy = false; }
        Show(VK);
    }

    /// <summary>Verbesserte Fassung ins Stück übernehmen – gleiche Schnittpunkte, nur die Datei wechselt.</summary>
    public void EditEnhanceApply(ProgramDocument d)
    {
        if (EditEnhanceLast is not { } last || !last.Result.Verdict.MayApply || !File.Exists(last.Result.Output)) { _progMsg[VK] = "Keine verbesserte Fassung zum Übernehmen."; return; }
        d.Edit("Bild verbessert", x => VideoEdit.Store(x, [.. VideoEdit.Clips(x).Select(c => c.Id == last.ClipId ? c with { File = last.Result.Output } : c)]));
        _progMsg[VK] = $"Stück nutzt jetzt „{Path.GetFileName(last.Result.Output)}“ – Rückgängig holt das Original zurück.";
        EditEnhanceLast = null;
    }

    Control EditClipEnhance(ProgramDocument d, EditClip c)
    {
        var sp = new StackPanel { Name = "ve-enh", Spacing = 6, Margin = new Thickness(0, 10, 0, 0) };
        sp.Children.Add(H("Bild verbessern", 13));
        if (c.IsImage || MediaStudio.IsAudio(c.File)) { sp.Children.Add(P("Nur bei Videos.", Muted, 11)); return sp; }
        var stab = new CheckBox { Name = "ve-enh-stab", Content = "Wackeln ausgleichen" };
        Slider S(string name, string label) { var sl = new Slider { Name = name, Minimum = 0, Maximum = 1, Value = 0 }; sp.Children.Add(new TextBlock { Text = label, Foreground = Muted, FontSize = 11.5 }); sp.Children.Add(sl); return sl; }
        sp.Children.Add(stab);
        var den = S("ve-enh-denoise", "Rauschen entfernen"); var sharp = S("ve-enh-sharpen", "Schärfen");
        var up = new ComboBox { Name = "ve-enh-up", ItemsSource = new[] { "Größe behalten", "2× größer (Lanczos)" }, SelectedIndex = 0, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
        sp.Children.Add(up);
        var run = new Button { Name = "ve-enh-run", Content = _veEnhBusy ? "Wird berechnet …" : "Verbesserte Fassung berechnen", IsEnabled = !_veEnhBusy, Margin = new Thickness(0, 4, 0, 0) };
        run.Click += (_, _) => _ = EditEnhance(d, c.Id, new VideoEnhance.Options(stab.IsChecked == true, Math.Round(den.Value, 2), Math.Round(sharp.Value, 2), up.SelectedIndex == 1 ? 2 : 1));
        sp.Children.Add(run);
        if (EditEnhanceLast is { } last && last.ClipId == c.Id)
        {
            var v = last.Result.Verdict;
            sp.Children.Add(new Border { Name = "ve-enh-verdict", CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 6), Background = Brush.Parse(v.Light switch { "rot" => "#33E05A5A", "gruen" => "#3348C774", _ => "#33E0B34A" }),
                Child = P(v.Text, Text, 11.5) });
            if (v.MayApply)
            {
                var apply = new Button { Name = "ve-enh-apply", Content = "Verbesserte Fassung verwenden", Background = Accent, Foreground = Brushes.White };
                apply.Click += (_, _) => ProgramRun(VK, () => EditEnhanceApply(d));
                sp.Children.Add(apply);
            }
        }
        sp.Children.Add(P("Trendo misst das Ergebnis (SSIM/PSNR). Mehr Pixel allein gelten nie als besser.", Muted, 10.5));
        return sp;
    }
}
