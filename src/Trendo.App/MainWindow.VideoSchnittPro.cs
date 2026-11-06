using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Media;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Videoschnitt wie Premiere/DaVinci (Profi 23, 05.11.2026): Farbe, Tonpegel, Übergangsart und Titellage je Stück; zweite Bildspur.</summary>
public partial class MainWindow
{
    /// <summary>Stück auf die zweite Bildspur legen – ab der Abspielmarke, 5 Sekunden, oben rechts.</summary>
    public void EditAddOverlay(ProgramDocument d, string file)
    {
        if (!File.Exists(file)) throw new FileNotFoundException("Datei nicht gefunden: " + Path.GetFileName(file));
        if (MediaStudio.IsAudio(file)) throw new InvalidOperationException("Auf die zweite Bildspur passen Bilder und Videos.");
        var start = Math.Max(0, d.Number("playhead"));
        d.Edit("Zweite Bildspur", x => VideoPro.AddOverlay(x, file, start, 5));
        _progMsg[VK] = $"„{Path.GetFileName(file)}“ liegt auf der zweiten Bildspur ab {start:0.0} s.";
    }

    Control EditClipPro(ProgramDocument d, int index, EditClip c)
    {
        var fx = VideoPro.Fx(d.Data, c.Id);
        var sp = new StackPanel { Name = "ve-pro", Spacing = 6, Margin = new Thickness(0, 6, 0, 0) };
        Slider S(string name, string label, double min, double max, double value)
        {
            var sl = new Slider { Name = name, Minimum = min, Maximum = max, Value = value };
            sp.Children.Add(new TextBlock { Text = label, Foreground = Muted, FontSize = 11.5 }); sp.Children.Add(sl); return sl;
        }
        sp.Children.Add(H("Farbe", 13));
        var look = new ComboBox { Name = "ve-look", HorizontalAlignment = HorizontalAlignment.Stretch, ItemsSource = VideoPro.Looks.Select(l => l.Label).ToList(), PlaceholderText = "Look wählen …" };
        sp.Children.Add(look);
        var bri = S("ve-color-b", "Helligkeit", -0.5, 0.5, fx.Brightness); var con = S("ve-color-c", "Kontrast", 0.3, 2, fx.Contrast);
        var sat = S("ve-color-s", "Sättigung", 0, 3, fx.Saturation); var warm = S("ve-color-w", "Wärme (kühl ↔ warm)", -1, 1, fx.Warmth);
        look.SelectionChanged += (_, _) => { if (look.SelectedIndex < 0) return; var l = VideoPro.Looks[look.SelectedIndex].Fx; bri.Value = l.Brightness; con.Value = l.Contrast; sat.Value = l.Saturation; warm.Value = l.Warmth; };
        sp.Children.Add(H("Ton", 13));
        var vol = S("ve-clip-vol", $"Lautstärke dieses Stücks ({c.Volume * 100:0} %)", 0, 2, c.Volume);
        sp.Children.Add(H("Übergang ins nächste Stück", 13));
        var trans = new ComboBox { Name = "ve-transition", HorizontalAlignment = HorizontalAlignment.Stretch, ItemsSource = VideoPro.Transitions.Select(t => t.Label).ToList(),
            SelectedIndex = Math.Max(0, Array.FindIndex(VideoPro.Transitions, t => t.Id == fx.Transition)) };
        sp.Children.Add(trans);
        sp.Children.Add(P(c.Fade > 0 ? $"Dauer: {c.Fade:0.#} s (oben bei „Überblenden“)." : "Wirkt, sobald oben eine Überblend-Dauer steht.", Muted, 11));
        sp.Children.Add(H("Titel", 13));
        var pos = new ComboBox { Name = "ve-title-pos", HorizontalAlignment = HorizontalAlignment.Stretch, ItemsSource = new[] { "Unten", "Mitte", "Oben" }, SelectedIndex = fx.TitlePos switch { "mitte" => 1, "oben" => 2, _ => 0 } };
        sp.Children.Add(pos);
        var size = S("ve-title-size", "Schriftgröße", 0.5, 3, fx.TitleSize);
        var apply = new Button { Name = "ve-pro-apply", Content = "Farbe, Ton, Übergang und Titel übernehmen", Background = Accent, Foreground = Brushes.White, Margin = new Thickness(0, 4, 0, 0) };
        apply.Click += (_, _) => ProgramRun(VK, () => d.Edit("Stück einstellen", x =>
        {
            VideoPro.SetFx(x, c.Id, new VideoPro.ClipFx(Math.Round(bri.Value, 3), Math.Round(con.Value, 3), Math.Round(sat.Value, 3), Math.Round(warm.Value, 3),
                VideoPro.Transitions[Math.Max(0, trans.SelectedIndex)].Id, pos.SelectedIndex switch { 1 => "mitte", 2 => "oben", _ => "unten" }, Math.Round(size.Value, 2)));
            VideoPro.SetClipVolume(x, index, vol.Value);
        }));
        var reset = new Button { Name = "ve-pro-reset", Content = "Farbe zurücksetzen", Margin = new Thickness(0, 4, 0, 0) };
        reset.Click += (_, _) => ProgramRun(VK, () => d.Edit("Farbe zurücksetzen", x => VideoPro.SetFx(x, c.Id, new VideoPro.ClipFx(Transition: fx.Transition, TitlePos: fx.TitlePos, TitleSize: fx.TitleSize))));
        sp.Children.Add(new WrapPanel { Children = { apply, new Border { Width = 6 }, reset } });
        sp.Children.Add(EditClipEnhance(d, c)); // Paket 01 S3-12: Bild verbessern mit Messung
        return sp;
    }

    Control EditOverlayPanel(ProgramDocument d)
    {
        var list = VideoPro.Overlays(d.Data);
        var sp = new StackPanel { Name = "ve-overlays", Spacing = 6 };
        sp.Children.Add(H($"Zweite Bildspur ({list.Count})", 13));
        if (list.Count == 0) { sp.Children.Add(P("Noch leer – ein Bild oder Video oben wählen und „Auf die zweite Bildspur“ – z. B. Logo, Bild im Bild, Einblendung.", Muted, 11.5)); return sp; }
        var corners = new[] { ("oben-rechts", "Oben rechts"), ("oben-links", "Oben links"), ("unten-rechts", "Unten rechts"), ("unten-links", "Unten links"), ("mitte", "Mitte") };
        for (var i = 0; i < list.Count; i++)
        {
            var o = list[i]; var k = i;
            var card = new StackPanel { Name = "ve-overlay-" + i, Spacing = 4 };
            card.Children.Add(new TextBlock { Text = $"{(MediaStudio.IsImage(o.File) ? "🖼" : "🎬")} {Path.GetFileName(o.File)} · ab {o.Start:0.0} s · {o.Length:0.0} s", Foreground = Text, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis });
            var corner = new ComboBox { Name = "ve-overlay-corner-" + i, HorizontalAlignment = HorizontalAlignment.Stretch, ItemsSource = corners.Select(c => c.Item2).ToList(), PlaceholderText = "Lage …" };
            var width = new Slider { Name = "ve-overlay-size-" + i, Minimum = 0.05, Maximum = 1, Value = o.Width };
            var op = new Slider { Name = "ve-overlay-op-" + i, Minimum = 0.05, Maximum = 1, Value = o.Opacity };
            var start = new TextBox { Name = "ve-overlay-start-" + i, Text = o.Start.ToString("0.##", De), Width = 64 }; var len = new TextBox { Name = "ve-overlay-len-" + i, Text = o.Length.ToString("0.##", De), Width = 64 };
            double N(TextBox t, double d0) => double.TryParse((t.Text ?? "").Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : d0;
            card.Children.Add(corner);
            card.Children.Add(new TextBlock { Text = "Größe", Foreground = Muted, FontSize = 11 }); card.Children.Add(width);
            card.Children.Add(new TextBlock { Text = "Deckkraft", Foreground = Muted, FontSize = 11 }); card.Children.Add(op);
            card.Children.Add(new WrapPanel { Children = { new TextBlock { Text = "ab ", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center }, start, new TextBlock { Text = " s · Dauer ", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center }, len, new TextBlock { Text = " s", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center } } });
            var ok = new Button { Name = "ve-overlay-apply-" + i, Content = "Übernehmen" };
            ok.Click += (_, _) => ProgramRun(VK, () => d.Edit("Zweite Bildspur", x =>
            {
                var (cx, cy) = corner.SelectedIndex >= 0 ? VideoPro.Corner(corners[corner.SelectedIndex].Item1, width.Value) : (o.X, o.Y);
                VideoPro.SetOverlay(x, o.Id, N(start, o.Start), N(len, o.Length), cx, cy, width.Value, op.Value);
            }));
            var del = new Button { Name = "ve-overlay-del-" + i, Content = "Entfernen" };
            del.Click += (_, _) => ProgramRun(VK, () => d.Edit("Zweite Bildspur entfernen", x => VideoPro.RemoveOverlay(x, o.Id)));
            card.Children.Add(new WrapPanel { Children = { ok, new Border { Width = 6 }, del } });
            sp.Children.Add(new Border { CornerRadius = new CornerRadius(10), Background = Brush.Parse("#10FFFFFF"), BorderBrush = Edge, BorderThickness = new Thickness(1), Padding = new Thickness(10, 8), Child = card });
        }
        return sp;
    }
}
