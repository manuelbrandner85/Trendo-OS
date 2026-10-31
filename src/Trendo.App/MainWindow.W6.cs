using Avalonia;
using Avalonia.Automation;
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

    /// <summary>T-0207: Felder mit fester Wertemenge werden Auswahllisten. Schlüssel „werkzeug-feld“; dazu Ja/Nein-Felder über die Beschriftung.</summary>
    public static readonly Dictionary<string, string[]> FachChoices = new()
    {
        ["scan-scale-from"] = ["mm", "cm", "m", "in", "ft"], ["scan-scale-to"] = ["mm", "cm", "m", "in", "ft"],
        ["eng-unit-from"] = ["mm", "m", "in", "ft", "N", "kN", "kg", "lbf", "bar", "psi", "MPa", "kW", "PS"],
        ["eng-unit-to"] = ["mm", "m", "in", "ft", "N", "kN", "kg", "lbf", "bar", "psi", "MPa", "kW", "PS"],
        ["eng-beam-case"] = ["1", "2", "3", "4"], ["eng-buckle-fall"] = ["1", "2", "3", "4"],
        ["eng-bolt-grade"] = ["8.8", "10.9", "12.9", "A2-70"], ["eng-bolt-size"] = [.. EngineeringPlus.BoltSizes],
        ["eng-beam-mat"] = [.. Engineering.Materials.Select(m => m.Name)], ["eng-buckle-mat"] = [.. Engineering.Materials.Select(m => m.Name)],
        ["cnc-feeds-mat"] = [.. Cnc.Materials.Keys], ["sci-err-op"] = ["*", "/", "+", "-"],
        ["rob-see-color"] = [.. Vision.Hues.Keys], ["rob-count-color"] = [.. Vision.Hues.Keys], ["rob-mask-color"] = [.. Vision.Hues.Keys],
    };

    static readonly Dictionary<string, string[]> ChoiceByLabel = new(StringComparer.Ordinal)
    {
        ["(j/n)"] = ["j", "n"], ["(d/h)"] = ["d", "h"], ["(N/S)"] = ["N", "S"], ["(90/95/99)"] = ["90", "95", "99"],
        ["reihe/parallel"] = ["reihe", "parallel"], ["r oder c"] = ["r", "c"],
    };

    static string? Pick(string[] opts, string v) =>
        opts.FirstOrDefault(o => o == v) ?? (v.Length > 0 ? opts.FirstOrDefault(o => o.StartsWith(v, StringComparison.OrdinalIgnoreCase)) : null);

    public static string[]? ChoicesFor(string id, string key, string label) =>
        FachChoices.TryGetValue($"{id}-{key}", out var c) ? c : ChoiceByLabel.FirstOrDefault(kv => label.EndsWith(kv.Key, StringComparison.Ordinal)).Value;

    static readonly System.Text.RegularExpressions.Regex NumberLike = new(@"^-?\d+([.,]\d+)?$");
    public static bool LooksNumeric(string s) => NumberLike.IsMatch(s.Trim());

    string FachStateFile => Path.Combine(_core.DataRoot, "fach-stand.json");

    /// <summary>T-0207: Eingaben und Ergebnisse aller Fach-Werkzeuge bleiben über das Schließen hinaus erhalten.</summary>
    void LoadFachState()
    {
        try
        {
            if (!File.Exists(FachStateFile)) return;
            var s = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(FachStateFile));
            foreach (var (k, v) in s?.GetValueOrDefault("in") ?? []) _fachIn[k] = v;
            foreach (var (k, v) in s?.GetValueOrDefault("out") ?? []) _fachOut[k] = v;
        }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or UnauthorizedAccessException) { }
    }

    void SaveFachState()
    {
        try { File.WriteAllText(FachStateFile, System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, Dictionary<string, string>> { ["in"] = _fachIn, ["out"] = _fachOut })); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private void InitWave6()
    {
        LoadFachState();
        InitSpec01(); InitSpec02(); InitSpec03(); InitSpec04(); InitSpec05(); InitSpec06(); InitSpec08(); InitSpec09(); InitSpec10(); InitSpec13(); InitSpec14(); InitSpec15(); InitSpec16(); InitCore09(); InitTv();
    }

    /// <summary>Aufklappbare Gruppe, damit jedes Studio übersichtlich bleibt.</summary>
    private Control FachGroup(string name, string title, bool open, params Control[] tools)
    {
        var sp = new StackPanel { Spacing = 8 }; foreach (var t in tools) sp.Children.Add(t);
        var key = "grp-" + name; var ex = new Expander { Name = key, Header = H(title, 14), IsExpanded = _fachIn.TryGetValue(key, out var v) ? v == "1" : open, HorizontalAlignment = HorizontalAlignment.Stretch, Content = sp };
        ex.Expanded += (_, _) => _fachIn[key] = "1"; ex.Collapsed += (_, _) => _fachIn[key] = "0";
        return ex;
    }

    private Control FachPage(string title, Control body) => W45Page(title, FachTabs, "fach", body);

    /// <summary>Ein Werkzeug: Eingabefelder, ein Knopf, ein Ergebnis. Fehler erscheinen als verständlicher Satz.</summary>
    private Control FachTool(string id, string title, string hint, (string Key, string Label, string Default)[] fields, Func<Func<string, string>, string> run, string button = "Berechnen")
    {
        var values = new Dictionary<string, Func<string>>();
        var wrap = new WrapPanel();
        foreach (var (k, l, d) in fields)
        {
            var current = _fachIn.TryGetValue($"{id}-{k}", out var v) ? v : d;
            Control input;
            if (ChoicesFor(id, k, l) is { } opts)
            {
                // Auswahlliste statt Freitext: nur gültige Werte
                var cb = new ComboBox { Name = $"{id}-{k}", ItemsSource = opts, SelectedItem = Pick(opts, current) ?? Pick(opts, d) ?? opts[0], Width = fields.Length > 3 ? 150 : 220 };
                AutomationProperties.SetName(cb, l);
                values[k] = () => cb.SelectedItem as string ?? "";
                input = cb;
            }
            else
            {
                var tb = new TextBox { Name = $"{id}-{k}", Text = current, Width = fields.Length > 3 ? 150 : 220, PlaceholderText = l };
                values[k] = () => tb.Text ?? "";
                if (LooksNumeric(d))
                {
                    // Zahlenfeld: sofort prüfen, Fehler am Feld zeigen
                    var normal = tb.BorderBrush;
                    void Check() { var ok = string.IsNullOrWhiteSpace(tb.Text) || LooksNumeric(tb.Text); tb.BorderBrush = ok ? normal : Brush.Parse("#F87171"); ToolTip.SetTip(tb, ok ? null : "Bitte eine Zahl eingeben (z. B. 12,5)."); }
                    tb.TextChanged += (_, _) => Check(); Check();
                }
                input = tb;
            }
            wrap.Children.Add(MSp(new StackPanel { Children = { new TextBlock { Text = l, Foreground = Muted, FontSize = 11 }, input } }));
        }
        var outp = new SelectableTextBlock { Name = $"{id}-out", Text = _fachOut.GetValueOrDefault(id, ""), Foreground = Text, TextWrapping = TextWrapping.Wrap };
        var pic = new ContentControl { Name = $"{id}-grafik" };
        void ShowPicture(string res)
        {
            // Nennt das Ergebnis eine erzeugte SVG-Grafik, wird sie gleich hier gezeigt
            var m = System.Text.RegularExpressions.Regex.Match(res, @"(?<p>[A-Za-z]:[\\/][^\n]*?\.svg|/[^\n]*?\.svg)\s*$", System.Text.RegularExpressions.RegexOptions.Multiline);
            try { pic.Content = m.Success && File.Exists(m.Groups["p"].Value.Trim()) ? SvgLite.Render(File.ReadAllText(m.Groups["p"].Value.Trim())) : null; }
            catch (Exception ex) when (ex is System.Xml.XmlException or FormatException or IOException or ArgumentException) { pic.Content = null; }
        }
        ShowPicture(outp.Text ?? "");
        var go = new Button { Name = $"{id}-run", Content = button, Background = Accent, VerticalAlignment = VerticalAlignment.Bottom };
        go.Click += (_, _) =>
        {
            foreach (var (k, get) in values) _fachIn[$"{id}-{k}"] = get();
            string res;
            try { res = run(k => values.TryGetValue(k, out var get) ? get() : ""); }
            catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException or IOException) { res = "⚠ " + ex.Message; }
            _fachOut[id] = res; outp.Text = res;
            ShowPicture(res);
            foreach (var o in Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(this).OfType<SelectableTextBlock>().Where(c => c.Name == outp.Name)) o.Text = res;
            SaveFachState();
        };
        wrap.Children.Add(MSp(go));
        return WCard(new StackPanel { Spacing = 4, Children = { H(title, 14), P(hint, null, 11), wrap, outp, pic } });
    }
}
