using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Trendo.Core.Media;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Paket 01 – Studio 4, S4-09 (07.11.2026): Busse im Mehrspur-Mischpult – Ausgang je Spur, Sends, je Bus EQ, Kompressor mit Sidechain, Hall, Pegel.</summary>
public partial class MainWindow
{
    Control AuBusPanel(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "au-buses", Spacing = 6, Margin = new Thickness(0, 8, 0, 0) };
        sp.Children.Add(H("Busse und Sends", 13));
        var buses = MixRouting.Buses(d.Data); var tracks = AudioMulti.Tracks(d.Data);
        var add = new Button { Name = "au-bus-add", Content = "＋ Bus", FontSize = 12 };
        var busName = new TextBox { Name = "au-bus-name", Watermark = "Name, z. B. Hall oder Musik", Width = 170 };
        add.Click += (_, _) => ProgramRun(AUK, () => d.Edit("Bus hinzufügen", x => MixRouting.AddBus(x, busName.Text ?? "")));
        sp.Children.Add(new WrapPanel { Children = { busName, new Border { Width = 6 }, add } });
        if (buses.Count == 0) { sp.Children.Add(P("Ein Bus fasst Spuren zusammen (z. B. alle Musik) oder nimmt per Send einen Anteil auf (z. B. Hall).", Muted, 11)); return sp; }
        var targets = new List<(string Id, string Label)> { (MixRouting.Master, "Summe") }; targets.AddRange(buses.Select(b => (b.Id, b.Name)));
        // Spuren: Ausgang und Send
        for (var i = 0; i < tracks.Count; i++)
        {
            var t = tracks[i]; var cur = MixRouting.TrackOut(d.Data, t.Id);
            var outBox = new ComboBox { Name = $"au-track-out-{i}", ItemsSource = targets.Select(x => x.Label).ToList(), SelectedIndex = Math.Max(0, targets.FindIndex(x => x.Id == cur)), MinWidth = 110, FontSize = 12 };
            outBox.SelectionChanged += (_, _) => { if (outBox.SelectedIndex >= 0 && targets[outBox.SelectedIndex].Id != cur) ProgramRun(AUK, () => d.Edit("Ausgang", x => MixRouting.Route(x, t.Id, targets[outBox.SelectedIndex].Id))); };
            sp.Children.Add(new WrapPanel { Children = { new TextBlock { Text = t.Name + " → ", Foreground = Text, FontSize = 12, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, MaxWidth = 140, TextTrimming = TextTrimming.CharacterEllipsis }, outBox } });
        }
        // Busse
        for (var k = 0; k < buses.Count; k++)
        {
            var b = buses[k]; var card = new StackPanel { Name = $"au-bus-{k}", Spacing = 3 };
            card.Children.Add(new TextBlock { Text = b.Name, Foreground = Text, FontSize = 12.5, FontWeight = FontWeight.SemiBold });
            Slider S(string name, string label, double min, double max, double v) { var s = new Slider { Name = $"au-bus-{name}-{k}", Minimum = min, Maximum = max, Value = v }; card.Children.Add(new TextBlock { Text = label, Foreground = Muted, FontSize = 10.5 }); card.Children.Add(s); return s; }
            var low = S("low", "Tiefen (dB)", -15, 15, b.LowDb); var mid = S("mid", "Mitten (dB)", -15, 15, b.MidDb); var high = S("high", "Höhen (dB)", -15, 15, b.HighDb);
            var thr = S("thr", "Kompressor-Schwelle (dB)", -60, 0, b.CompThresholdDb); var ratio = S("ratio", "Verhältnis", 1, 20, b.CompRatio);
            var rev = S("rev", "Hall", 0, 1, b.ReverbMix); var gain = S("gain", "Pegel (dB)", -30, 6, b.GainDb);
            var sc = new ComboBox { Name = $"au-bus-sc-{k}", ItemsSource = new[] { "Sidechain: aus" }.Concat(tracks.Select(t => "Sidechain: " + t.Name)).ToList(), SelectedIndex = Math.Max(0, tracks.FindIndex(t => t.Id == b.Sidechain) + 1), FontSize = 12 };
            var outs = targets.Where(x => x.Id != b.Id).ToList();
            var bout = new ComboBox { Name = $"au-bus-out-{k}", ItemsSource = outs.Select(x => "→ " + x.Label).ToList(), SelectedIndex = Math.Max(0, outs.FindIndex(x => x.Id == b.Out)), FontSize = 12 };
            card.Children.Add(new WrapPanel { Children = { sc, new Border { Width = 6 }, bout } });
            var sends = tracks.Select((t, i) =>
            {
                var lvl = MixRouting.Sends(d.Data).FirstOrDefault(s => s.TrackId == t.Id && s.BusId == b.Id)?.Level ?? 0;
                var s = new Slider { Name = $"au-send-{k}-{i}", Minimum = 0, Maximum = 1, Value = lvl, Width = 90 };
                card.Children.Add(new WrapPanel { Children = { new TextBlock { Text = "Send " + t.Name + " ", Foreground = Muted, FontSize = 10.5, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center }, s } });
                return (t.Id, s);
            }).ToList();
            var apply = new Button { Name = $"au-bus-apply-{k}", Content = "Übernehmen", FontSize = 12 };
            apply.Click += (_, _) => ProgramRun(AUK, () => d.Edit("Bus einstellen", x =>
            {
                MixRouting.SetBus(x, b.Id, gain.Value, low.Value, mid.Value, high.Value, thr.Value, ratio.Value, sc.SelectedIndex <= 0 ? "" : tracks[sc.SelectedIndex - 1].Id, rev.Value);
                if (outs[Math.Max(0, bout.SelectedIndex)].Id != b.Out) MixRouting.Route(x, b.Id, outs[Math.Max(0, bout.SelectedIndex)].Id);
                foreach (var (tid, s) in sends) if (MixRouting.TrackOut(x, tid) != b.Id) MixRouting.SetSend(x, tid, b.Id, Math.Round(s.Value, 2));
            }));
            card.Children.Add(apply);
            sp.Children.Add(new Border { CornerRadius = new CornerRadius(8), Background = GroupBg, BorderBrush = PaneEdge, BorderThickness = new Thickness(1), Padding = new Thickness(8, 6), Child = card });
        }
        return sp;
    }
}
