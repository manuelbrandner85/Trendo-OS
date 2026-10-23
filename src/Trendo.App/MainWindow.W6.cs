using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Welle 6: Fach-Studios – eigene Reiterleiste (max. 2 Ebenen), einheitliche Rechen-Werkzeuge.</summary>
public partial class MainWindow
{
    public static readonly List<(string Key, string Label, string Module)> FachTabs = [];
    private readonly Dictionary<string, string> _fachOut = [];
    private readonly Dictionary<string, string> _fachIn = [];

    private void InitWave6()
    {
        InitSpec01(); InitSpec02(); InitSpec03(); InitSpec04(); InitSpec05(); InitSpec06(); InitSpec08(); InitSpec09(); InitSpec10(); InitSpec13(); InitSpec14(); InitSpec15(); InitSpec16(); InitCore09();
    }

    /// <summary>Aufklappbare Gruppe, damit jedes Studio übersichtlich bleibt.</summary>
    private Control FachGroup(string name, string title, bool open, params Control[] tools)
    {
        var sp = new StackPanel { Spacing = 8 }; foreach (var t in tools) sp.Children.Add(t);
        var key = "grp-" + name; var ex = new Expander { Name = key, Header = H(title, 14), IsExpanded = _fachIn.TryGetValue(key, out var v) ? v == "1" : open, HorizontalAlignment = HorizontalAlignment.Stretch, Content = sp };
        ex.Expanded += (_, _) => _fachIn[key] = "1"; ex.Collapsed += (_, _) => _fachIn[key] = "0";
        return ex;
    }

    private readonly HashSet<string> _fachGeladen = [];

    private Control FachPage(string title, Control body)
    {
        if (_settings.SimpleMode)
            return W45Page(title, FachTabs, "fach", new StackPanel { Spacing = 8, Children = { P("Im einfachen Modus sind die Fachwerkzeuge ausgeblendet. Unter Bedienung kannst du ihn ausschalten.") } });
        var bar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                Act("fach-save", "Projekt speichern", () => { FachSchreiben(CurrentView); Say("Projekt gespeichert.", CurrentView); }),
                Act("fach-open", "Projekt laden", () => { FachLesen(CurrentView, true); Say("Projekt geladen.", CurrentView); }),
            },
        };
        return W45Page(title, FachTabs, "fach", new StackPanel { Spacing = 8, Children = { bar, body } });
    }

    private void FachSicher(string toolId)
    {
        var studio = FachChoices.Studio(toolId);
        if (studio is null || !_fachGeladen.Add(studio)) return;
        FachLesen(studio, false);
    }

    private void FachLesen(string studio, bool ersetzen)
    {
        if (ersetzen)
        {
            foreach (var k in _fachIn.Keys.Where(k => FachChoices.Gehoert(studio, k)).ToList()) _fachIn.Remove(k);
            foreach (var k in _fachOut.Keys.Where(k => FachChoices.Gehoert(studio, k)).ToList()) _fachOut.Remove(k);
        }
        var st = Fach.Load<FachStand>(_core.DataRoot, studio, "stand");
        foreach (var kv in st.Eingaben)
            if (FachChoices.Gehoert(studio, kv.Key)) _fachIn[kv.Key] = kv.Value;
        foreach (var kv in st.Ergebnisse)
            if (FachChoices.Gehoert(studio, kv.Key)) _fachOut[kv.Key] = kv.Value;
        _fachGeladen.Add(studio);
    }

    private void FachSchreiben(string studio)
    {
        if (string.IsNullOrWhiteSpace(studio) || FachChoices.Studio(studio + "-x") is null && !FachTabs.Exists(t => t.Key == studio)) return;
        var st = new FachStand();
        foreach (var kv in _fachIn)
            if (FachChoices.Gehoert(studio, kv.Key)) st.Eingaben[kv.Key] = kv.Value;
        foreach (var kv in _fachOut)
            if (FachChoices.Gehoert(studio, kv.Key)) st.Ergebnisse[kv.Key] = kv.Value;
        Fach.Save(_core.DataRoot, studio, "stand", st);
    }

    /// <summary>Ein Werkzeug: Listen für feste Werte, Zahlen mit Einheit, ein Knopf, ein Ergebnis.</summary>
    private Control FachTool(string id, string title, string hint, (string Key, string Label, string Default)[] fields, Func<Func<string, string>, string> run, string button = "Berechnen")
    {
        FachSicher(id);
        var boxes = new Dictionary<string, TextBox>();
        var combos = new Dictionary<string, ComboBox>();
        var wrap = new WrapPanel();
        foreach (var (k, l, d) in fields)
        {
            var key = $"{id}-{k}";
            var cur = _fachIn.TryGetValue(key, out var gespeichert) ? gespeichert : d;
            var choices = FachChoices.JaNein(l) ? FachChoices.JaNeinWerte : FachChoices.For(id, k);
            var sicht = FachChoices.SichtLabel(l);
            var feld = new StackPanel();
            feld.Children.Add(new TextBlock { Text = sicht, Foreground = Muted, FontSize = 11 });
            if (choices is not null)
            {
                var cb = new ComboBox { Name = key, ItemsSource = choices, Width = fields.Length > 3 ? 140 : 180 };
                var idx = Array.FindIndex(choices, x => string.Equals(x, cur, StringComparison.OrdinalIgnoreCase));
                if (idx < 0) idx = Array.FindIndex(choices, x => cur.Length > 0 && x.StartsWith(cur, StringComparison.OrdinalIgnoreCase));
                cb.SelectedIndex = idx < 0 ? 0 : idx;
                cb.LostFocus += (_, _) => { _fachIn[key] = cb.SelectedItem as string ?? ""; if (FachChoices.Studio(id) is { } s) FachSchreiben(s); };
                if (id == "eng-beam" && k == "case") ToolTip.SetTip(cb, "1 zwei Auflager, Last in der Mitte. 2 zwei Auflager, gleichmäßige Last. 3 Kragarm, Last am Ende. 4 Kragarm, gleichmäßig.");
                if (id == "eng-buckle" && k == "fall") ToolTip.SetTip(cb, "1 unten eingespannt, oben frei. 2 beide Enden gelenkig. 3 eingespannt und gelenkig. 4 beidseitig eingespannt.");
                if (id == "el-comb" && k == "kind") ToolTip.SetTip(cb, "r steht für Widerstände. c steht für Kondensatoren.");
                combos[k] = cb;
                feld.Children.Add(cb);
            }
            else
            {
                var tb = new TextBox { Name = key, Text = cur, Width = fields.Length > 3 ? 150 : 220, Watermark = l };
                boxes[k] = tb;
                var einheit = FachChoices.Einheit(l);
                var zahl = FachChoices.IstZahl(l, d);
                if (einheit is not null) feld.Children.Add(new TextBlock { Name = key + "-unit", Text = einheit, Foreground = Muted, FontSize = 11 });
                var err = new TextBlock { Name = key + "-hinweis", Foreground = Brush.Parse("#FACC15"), FontSize = 11, Text = "" };
                if (zahl)
                    tb.TextChanged += (_, _) =>
                    {
                        var t = tb.Text ?? "";
                        if (string.IsNullOrWhiteSpace(t)) { err.Text = ""; return; }
                        try { Fach.Num(t, sicht); err.Text = ""; }
                        catch (FormatException) { err.Text = "Bitte eine Zahl eingeben"; }
                    };
                tb.LostFocus += (_, _) => { _fachIn[key] = tb.Text ?? ""; if (FachChoices.Studio(id) is { } s) FachSchreiben(s); };
                feld.Children.Add(tb);
                if (zahl) feld.Children.Add(err);
            }
            wrap.Children.Add(MSp(feld));
        }
        string Read(string k)
        {
            if (combos.TryGetValue(k, out var cb)) return cb.SelectedItem as string ?? "";
            if (boxes.TryGetValue(k, out var tb)) return tb.Text ?? "";
            return "";
        }
        var outp = new SelectableTextBlock { Name = $"{id}-out", Text = _fachOut.GetValueOrDefault(id, ""), Foreground = Text, TextWrapping = TextWrapping.Wrap };
        var host = new ContentControl { Name = id + "-bild" };
        void Zeichnen()
        {
            try { host.Content = FachBild(id, Read); }
            catch (Exception) { host.Content = null; }
        }
        var go = new Button { Name = $"{id}-run", Content = button, Background = Accent, VerticalAlignment = VerticalAlignment.Bottom };
        go.Click += (_, _) =>
        {
            foreach (var (k, b) in boxes) _fachIn[$"{id}-{k}"] = b.Text ?? "";
            foreach (var (k, b) in combos) _fachIn[$"{id}-{k}"] = b.SelectedItem as string ?? "";
            string res;
            try { res = run(Read); }
            catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException or IOException) { res = "⚠ " + ex.Message; }
            _fachOut[id] = res; outp.Text = res;
            foreach (var o in Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(this).OfType<SelectableTextBlock>().Where(c => c.Name == outp.Name)) o.Text = res;
            if (FachChoices.Studio(id) is { } studio) FachSchreiben(studio);
            Zeichnen();
        };
        Zeichnen();
        wrap.Children.Add(MSp(go));
        return WCard(new StackPanel { Spacing = 4, Children = { H(title, 14), P(hint, null, 11), wrap, host, outp } });
    }
}
