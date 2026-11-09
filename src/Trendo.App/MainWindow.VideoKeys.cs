using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Trendo.Core.Media;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Pilot 4.0: V5 Keyframes und V6 Effektstapel in den Einstellungen eines Stücks.</summary>
public partial class MainWindow
{
    Control EditKeysPanel(ProgramDocument d, string id, double localTime)
    {
        var sp = new StackPanel { Name = "ve-keys-" + id, Spacing = 4, Margin = new Thickness(0, 6, 0, 0) };
        sp.Children.Add(H("Bewegung (Keyframes)", 13));
        sp.Children.Add(P($"Abspielkopf steht bei {localTime.ToString("0.00", De)} s in diesem Stück. Sichtbar wird die Bewegung im Export.", Muted, 11));
        var choice = new ComboBox { Name = "ve-key-param-" + id, HorizontalAlignment = HorizontalAlignment.Stretch, ItemsSource = VideoAnim.Params.Select(p => p.Label).ToList(), SelectedIndex = 0 };
        var slider = new Slider { Name = "ve-key-value-" + id, Minimum = VideoAnim.Params[0].Min, Maximum = VideoAnim.Params[0].Max, Value = VideoAnim.Params[0].Default };
        var list = new StackPanel { Name = "ve-key-list-" + id, Spacing = 2 };
        VideoAnim.Param Current() => VideoAnim.Params[Math.Max(0, choice.SelectedIndex)];
        void Fill()
        {
            list.Children.Clear();
            var par = Current();
            var keys = VideoAnim.Keys(d.Data, id, par.Id);
            for (var i = 0; i < keys.Count; i++)
            {
                var n = i;
                var del = new Button { Name = $"ve-key-del-{id}-{i}", Content = "×", Padding = new Thickness(6, 0) };
                del.Click += (_, _) => ProgramRun(VK, () => d.Edit("Keyframe entfernen", x => VideoAnim.RemoveKey(x, id, par.Id, n)));
                list.Children.Add(new DockPanel { Children = { Dk(del, Avalonia.Controls.Dock.Right), new TextBlock { Text = $"{keys[i].Time.ToString("0.00", De)} s: {keys[i].Value.ToString("0.##", De)}", Foreground = Text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center } } });
            }
            if (keys.Count == 0) list.Children.Add(P("Noch kein Keyframe für diesen Wert.", Muted, 11));
        }
        choice.SelectionChanged += (_, _) => { var par = Current(); slider.Minimum = par.Min; slider.Maximum = par.Max; slider.Value = VideoAnim.ValueAt(VideoAnim.Keys(d.Data, id, par.Id), localTime, par.Default); Fill(); };
        var set = new Button { Name = "ve-key-set-" + id, Content = "Keyframe hier setzen" };
        set.Click += (_, _) => ProgramRun(VK, () => d.Edit("Keyframe", x => VideoAnim.SetKey(x, id, Current().Id, localTime, slider.Value)));
        sp.Children.Add(choice); sp.Children.Add(slider); sp.Children.Add(set); sp.Children.Add(list);
        Fill();
        return sp;
    }

    Control EditStackPanel(ProgramDocument d, string clip)
    {
        var sp = new StackPanel { Name = "ve-stack-" + clip, Spacing = 4, Margin = new Thickness(0, 6, 0, 0) };
        sp.Children.Add(H("Effekte", 13));
        sp.Children.Add(P("Die Effekte wirken von oben nach unten. Sichtbar werden sie im Export.", Muted, 11));
        var items = VideoAnim.Stack(d.Data, clip);
        for (var i = 0; i < items.Count; i++)
        {
            var it = items[i];
            var def = VideoAnim.Effects.First(e => e.Id == it.Type);
            var on = new CheckBox { Name = $"ve-fx-on-{i}", IsChecked = it.On, Content = def.Label };
            on.IsCheckedChanged += (_, _) => { if (on.IsChecked != it.On) ProgramRun(VK, () => d.Edit("Effekt an oder aus", x => VideoAnim.ToggleEffect(x, clip, it.Id))); };
            Button B(string name, string label, string tip, Action<System.Text.Json.Nodes.JsonObject> a) { var b = new Button { Name = name, Content = label, Padding = new Thickness(6, 0) }; ToolTip.SetTip(b, tip); b.Click += (_, _) => ProgramRun(VK, () => d.Edit("Effekte", a)); return b; }
            var amount = new Slider { Name = $"ve-fx-amount-{i}", Minimum = 0, Maximum = 1, Value = it.Amount, Width = 120 };
            sp.Children.Add(new WrapPanel { Children = { on, B($"ve-fx-up-{i}", "▲", "Weiter nach oben", x => VideoAnim.MoveEffect(x, clip, it.Id, -1)), B($"ve-fx-down-{i}", "▼", "Weiter nach unten", x => VideoAnim.MoveEffect(x, clip, it.Id, 1)), B($"ve-fx-del-{i}", "×", "Entfernen", x => VideoAnim.RemoveEffect(x, clip, it.Id)), amount, B($"ve-fx-set-{i}", "Stärke übernehmen", "Regler übernehmen", x => VideoAnim.SetAmount(x, clip, it.Id, amount.Value)) } });
        }
        var pick = new ComboBox { Name = "ve-fx-pick", ItemsSource = VideoAnim.Effects.Select(e => e.Label).ToList(), SelectedIndex = 0, MinWidth = 150 };
        var add = new Button { Name = "ve-fx-add", Content = "Effekt hinzufügen" };
        add.Click += (_, _) => ProgramRun(VK, () => d.Edit("Effekt hinzufügen", x => VideoAnim.AddEffect(x, clip, VideoAnim.Effects[Math.Max(0, pick.SelectedIndex)].Id)));
        sp.Children.Add(new WrapPanel { Children = { pick, new Border { Width = 6 }, add } });
        return sp;
    }
}
