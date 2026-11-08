using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>CORE-02: Gedächtnis im Chat „KI fragen“ – von Haus aus aus, jeder Eintrag sichtbar und löschbar, nichts geht ins Internet.</summary>
public partial class MainWindow
{
    private KiMemoryStore? _kiMemory;
    public KiMemoryStore KiMemory => _kiMemory ??= new KiMemoryStore(_core.DataRoot, _core.Audit);
    private bool _memOpen, _memClearArmed;

    private void LearnFromChat(string question)
    {
        var added = KiMemory.Learn(question);
        if (added.Count == 0) return;
        var ids = added.Select(f => f.Id).ToList();
        Toast("Gemerkt: " + string.Join(" · ", added.Select(f => f.Text)), ("Vergessen", new Action(() => { foreach (var id in ids) KiMemory.Remove(id); Show("ki-fragen"); })));
    }

    private Control KiMemoryPanel()
    {
        var on = KiMemory.Enabled; var facts = KiMemory.All();
        var sp = new StackPanel { Spacing = 4, Margin = new Thickness(0, 12, 0, 0) };
        sp.Children.Add(H("Gedächtnis", 13));
        sp.Children.Add(P(on ? "Die KI auf diesem PC merkt sich, was du über dich sagst. Es verlässt den PC nicht." : "Aus. Eingeschaltet merkt sich die KI, was du über dich sagst, und nutzt es später. Es bleibt auf diesem PC.", Muted, 11));
        var row = new WrapPanel();
        row.Children.Add(MSp(Act("ask-mem-toggle", on ? "Ausschalten" : "Einschalten", () => { KiMemory.SetEnabled(!on); _memOpen = !on || _memOpen; _memClearArmed = false; Show("ki-fragen"); }, !on)));
        if (facts.Count > 0) row.Children.Add(MSp(Act("ask-mem-open", _memOpen ? "Einträge ausblenden" : $"Einträge ansehen ({facts.Count})", () => { _memOpen = !_memOpen; _memClearArmed = false; Show("ki-fragen"); })));
        sp.Children.Add(row);
        if (!_memOpen) return sp;
        var i = 0;
        foreach (var f in facts)
        {
            var id = f.Id;
            var del = new Button { Name = $"ask-mem-del-{i}", Content = "✕", Padding = new Thickness(6, 1), Background = Brushes.Transparent, Margin = new Thickness(4, 0, 0, 0) };
            ToolTip.SetTip(del, "Diesen Eintrag vergessen");
            del.Click += (_, _) => { KiMemory.Remove(id); Show("ki-fragen"); };
            sp.Children.Add(new DockPanel { Children = { Dk(del, Avalonia.Controls.Dock.Right), new TextBlock { Name = $"ask-mem-{i}", Text = f.Text, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center } } });
            i++;
        }
        var box = new TextBox { Name = "ask-mem-new", PlaceholderText = "Selbst etwas eintragen", FontSize = 12 };
        sp.Children.Add(box);
        var actions = new WrapPanel();
        actions.Children.Add(MSp(Act("ask-mem-add", "Merken", () =>
        {
            _askState = KiMemory.Add(box.Text ?? "") is null ? "Nicht gemerkt: leer, schon vorhanden oder ein Geheimnis wie ein Passwort." : "Gemerkt.";
            Show("ki-fragen");
        })));
        if (facts.Count > 0) actions.Children.Add(MSp(Act("ask-mem-clear", _memClearArmed ? "Wirklich alles vergessen?" : "Alles vergessen", () =>
        {
            if (_memClearArmed) { KiMemory.Clear(); _memClearArmed = false; _askState = "Alles vergessen."; } else _memClearArmed = true;
            Show("ki-fragen");
        })));
        sp.Children.Add(actions);
        return sp;
    }
}
