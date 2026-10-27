using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Sys;

namespace Trendo.App;

/// <summary>N5 Notizbuch, N9 Editor, N11 Rechner, N18 Zettel, N20 Uhr (Freigabe Teil 5, Punkt 4) – als Reiter unter „Alltag“ (SYS-02).</summary>
public partial class MainWindow
{
    public Notebook Notizbuch => new(_core.DataRoot);
    public ZettelStore Zettel => new(_core.DataRoot);
    public AlarmStore Wecker => new(_core.DataRoot);
    public WorldClock Weltuhr => new(_core.DataRoot);
    private static readonly FontFamily Mono = new("Cascadia Mono, Consolas, DejaVu Sans Mono, monospace");
    private DispatcherTimer? _uhrTick;

    private void InitHelfer()
    {
        RegisterTab(AlltagTabs, "notizbuch", "Notizbuch", "SYS-02", NotebookView, "notizbuch", "notizen", "abschnitt", "seite", "onenote", "schlagwort");
        RegisterTab(AlltagTabs, "editor", "Editor", "SYS-02", EditorView, "editor", "texteditor", "notepad", "txt", "kodierung", "utf-8", "suchen ersetzen");
        RegisterTab(AlltagTabs, "rechner", "Rechner", "SYS-02", RechnerView, "rechner", "taschenrechner", "wissenschaftlich", "programmierer", "hex", "binär", "graph", "funktion", "datum rechnen");
        RegisterTab(AlltagTabs, "uhr", "Uhr & Wecker", "SYS-02", UhrView, "wecker", "alarm", "timer", "stoppuhr", "weltuhr", "zeitzone");
        RegisterTab(AlltagTabs, "zettel", "Zettel", "SYS-02", ZettelView, "zettel", "haftnotiz", "post-it", "sticky");
        _uhrTick = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _uhrTick.Tick += (_, _) => UhrTick(DateTime.Now);
        _uhrTick.Start();
    }

    // Eigene, schlichte Symbole (Rechner, Uhr, Zettel, Notizbuch); gerade Linien und Kreise, keine fremden Vorlagen.
    private const string IcoCalc = "F1 M6,2H18A2,2 0 0,1 20,4V20A2,2 0 0,1 18,22H6A2,2 0 0,1 4,20V4A2,2 0 0,1 6,2Z M7,4V9H17V4Z M7,11V13H9V11Z M11,11V13H13V11Z M15,11V13H17V11Z M7,15V17H9V15Z M11,15V17H13V15Z M15,15V20H17V15Z M7,18V20H9V18Z M11,18V20H13V18Z";
    private const string IcoClock = "F1 M12,2A10,10 0 1,0 12,22A10,10 0 1,0 12,2Z M12,4A8,8 0 1,1 12,20A8,8 0 1,1 12,4Z M11,6H13V11.4L16.6,13.5L15.6,15.2L11,12.6Z";
    private const string IcoNote = "F1 M4,3H20V14H15A1,1 0 0,0 14,15V20H4Z M16,16H19.5L16,19.5Z";
    private const string IcoBook = "F1 M6,2H19A1,1 0 0,1 20,3V21A1,1 0 0,1 19,22H6A2,2 0 0,1 4,20V4A2,2 0 0,1 6,2Z M8,4V20H18V4Z M2,6H4V8H2Z M2,11H4V13H2Z M2,16H4V18H2Z M10,7H16V9H10Z M10,11H16V13H10Z";

    /// <summary>SYS-02 U1: Werkzeugkasten – alle kleinen Programme als große Kacheln.</summary>
    private Control AlltagStartView()
    {
        var grid = new UniformGrid { Name = "alltag-apps", Columns = 4 };
        foreach (var (key, name, sub, icon, c1, c2) in new[] {
                     ("notizbuch", "Notizbuch", "Abschnitte, Seiten, Schlagworte", IcoBook, "#C7A6FF", "#7C4DFF"),
                     ("editor", "Editor", "Textdateien mit jeder Kodierung", Mdi.FileDocumentEdit, "#9FD3FF", "#2F86F6"),
                     ("rechner", "Rechner", "Standard, wissenschaftlich, Grafik", IcoCalc, "#9CF5A8", "#22B35E"),
                     ("uhr", "Uhr & Wecker", "Wecker, Timer, Stoppuhr, Weltuhr", IcoClock, "#FFD39A", "#F08A3C"),
                     ("zettel", "Zettel", "Haftnotizen für die Startseite", IcoNote, "#FFF3A6", "#E0B400"),
                     ("alltag-bilder", "Bilder & Bildschirmfoto", "Drehen, verkleinern, Bildschirmfoto", Mdi.Image, "#FFB3C7", "#E5484D"),
                     ("medien", "Musik & Videos", "Abspielen aus deinen Ordnern", Mdi.Palette, "#80DEEA", "#00838F"),
                     ("alltag-dateien", "Dateien", "Umbenennen, Duplikate, ZIP", Mdi.Folder, "#FFE29A", "#D4A017"),
                     ("alltag-zeit", "Zeit & Kalender", "Termine, Erinnerungen, Fokus", Mdi.CalendarMonth, "#8FB8FF", "#3D5BFF"),
                     ("alltag-helfer", "Kleine Helfer", "Zwischenablage, Einheiten, Farben", Mdi.Tools, "#ECEFF1", "#90A4AE") })
        {
            if (!_showMap.ContainsKey(key)) continue;
            var k = key; var chip = Chip(icon, c1, c2, 44, 0.58); chip.HorizontalAlignment = HorizontalAlignment.Left;
            var b = new Button { Name = "alltag-app-" + key, Height = 112, Margin = new Thickness(5), Padding = new Thickness(12), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = Edge, BorderThickness = new Thickness(1),
                Content = new StackPanel { Spacing = 6, Children = { chip, new TextBlock { Text = name, Foreground = Text, FontSize = 14, FontWeight = FontWeight.SemiBold }, new TextBlock { Text = sub, Foreground = Muted, FontSize = 11.5, TextTrimming = TextTrimming.CharacterEllipsis } } } };
            Avalonia.Automation.AutomationProperties.SetName(b, $"{name}: {sub}");
            b.Click += (_, _) => { _w45Msg = ""; Show(k); };
            grid.Children.Add(b);
        }
        return AlltagPage("Werkzeugkasten", new StackPanel { Spacing = 8, Children = { P("Alle kleinen Programme für jeden Tag. Alles bleibt auf deinem PC.", null, 12), grid } });
    }

    // =====================================================================
    // N9 Editor
    // =====================================================================
    public sealed class EdDoc
    {
        public string? Path; public string Text = ""; public TextFileInfo Info = new("UTF-8", false, OperatingSystem.IsWindows() ? "CRLF" : "LF"); public bool Dirty; public int Caret;
        public string Name => Path is null ? "Unbenannt" : System.IO.Path.GetFileName(Path);
    }
    public List<EdDoc> EdDocs { get; } = [new()];
    private int _edIdx, _edCloseAsk = -1;
    private bool _edWrap, _edCase, _edRegex;
    private double _edFont = 13;
    private string _edFind = "", _edRepl = "";
    private EdDoc EdCur => EdDocs[Math.Clamp(_edIdx, 0, EdDocs.Count - 1)];

    /// <summary>Datei im Editor öffnen (auch von außen, z. B. aus dem Datei-Explorer).</summary>
    public void EditorOpen(string path)
    {
        var have = EdDocs.FindIndex(d => d.Path is not null && string.Equals(Path.GetFullPath(d.Path), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase));
        if (have >= 0) { _edIdx = have; Show("editor"); return; }
        var (text, info) = TextFileCodec.Load(path);
        var doc = new EdDoc { Path = path, Text = text, Info = info };
        if (EdDocs.Count == 1 && EdDocs[0] is { Path: null, Dirty: false, Text.Length: 0 }) EdDocs[0] = doc; else EdDocs.Add(doc);
        _edIdx = EdDocs.IndexOf(doc); _core.Audit.Append("app", "editor.open", Path.GetFileName(path));
        Show("editor");
    }

    public void EditorSave(string? path = null)
    {
        var d = EdCur; var target = path ?? d.Path ?? throw new ArgumentException("Bitte oben einen Dateinamen eintragen, z. B. C:\\Texte\\notiz.txt.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(target))!);
        TextFileCodec.Save(target, d.Text, d.Info); d.Path = target; d.Dirty = false;
        _core.Audit.Append("app", "editor.save", Path.GetFileName(target));
    }

    private Control EditorView()
    {
        const string V = "editor";
        var d = EdCur;
        var path = Field("ed-path", "Datei, z. B. C:\\Texte\\liste.txt", 330, d.Path);
        var enc = new ComboBox { Name = "ed-enc", ItemsSource = TextFileCodec.Encodings, SelectedItem = d.Info.Encoding, MinWidth = 150 };
        var bom = new CheckBox { Name = "ed-bom", Content = "BOM", IsChecked = d.Info.Bom, FontSize = 11.5 };
        var eol = new ComboBox { Name = "ed-eol", ItemsSource = TextFileCodec.Eols, SelectedItem = d.Info.Eol };
        enc.SelectionChanged += (_, _) => { if (enc.SelectedItem is string e && e != d.Info.Encoding) { d.Info = d.Info with { Encoding = e, Bom = e.StartsWith("UTF-16") || (e == "UTF-8" && d.Info.Bom) }; d.Dirty = true; Show(V); } };
        bom.IsCheckedChanged += (_, _) => { if (bom.IsChecked != d.Info.Bom) { d.Info = d.Info with { Bom = bom.IsChecked == true }; d.Dirty = true; } };
        eol.SelectionChanged += (_, _) => { if (eol.SelectedItem is string e && e != d.Info.Eol) { d.Info = d.Info with { Eol = e }; d.Dirty = true; } };
        var wrap = new CheckBox { Name = "ed-wrap", Content = "Zeilenumbruch", IsChecked = _edWrap, FontSize = 11.5 };
        wrap.IsCheckedChanged += (_, _) => { _edWrap = wrap.IsChecked == true; Show(V); };

        var bar = Line(
            Act("ed-new", "Neu", () => { EdDocs.Add(new EdDoc()); _edIdx = EdDocs.Count - 1; Show(V); }),
            path,
            Act("ed-open", "Öffnen", () => Try(V, () => { var p = Need(path, "die Datei"); if (!File.Exists(p)) throw new FileNotFoundException("Diese Datei gibt es nicht: " + p); EditorOpen(p); })),
            Act("ed-save", "Speichern", () => Try(V, () => { EditorSave(d.Path ?? Need(path, "einen Dateinamen")); Say("Gespeichert: " + d.Path, V); }), true),
            Act("ed-saveas", "Speichern unter", () => Try(V, () => { EditorSave(Need(path, "den neuen Dateinamen")); Say("Gespeichert: " + d.Path, V); })),
            enc, bom, eol, wrap,
            Act("ed-font-minus", "A−", () => { _edFont = Math.Max(9, _edFont - 1); Show(V); }),
            Act("ed-font-plus", "A+", () => { _edFont = Math.Min(28, _edFont + 1); Show(V); }));

        // Reiter der offenen Dateien
        var tabs = new WrapPanel { Name = "ed-tabs" };
        for (var k = 0; k < EdDocs.Count; k++)
        {
            var idx = k; var doc = EdDocs[k];
            var t = new Button { Name = $"ed-tab-{k}", Content = doc.Name + (doc.Dirty ? " •" : ""), FontSize = 11.5, Padding = new Thickness(10, 4), Margin = new Thickness(0, 0, 2, 4),
                Background = k == _edIdx ? Accent : Brushes.Transparent, Foreground = Text, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8, 8, 0, 0) };
            ToolTip.SetTip(t, doc.Path ?? "Noch nicht gespeichert");
            t.Click += (_, _) => { _edIdx = idx; _edCloseAsk = -1; Show(V); };
            var x = new Button { Name = $"ed-close-{k}", Content = "×", FontSize = 11, Padding = new Thickness(6, 2), Margin = new Thickness(0, 0, 8, 4), Background = Brushes.Transparent };
            ToolTip.SetTip(x, "Schließen");
            x.Click += (_, _) =>
            {
                if (doc.Dirty && _edCloseAsk != idx) { _edCloseAsk = idx; Say($"„{doc.Name}“ hat ungespeicherte Änderungen. Nochmal auf × klicken schließt ohne Speichern.", V); return; }
                _edCloseAsk = -1; EdDocs.RemoveAt(idx); if (EdDocs.Count == 0) EdDocs.Add(new EdDoc()); _edIdx = Math.Min(_edIdx, EdDocs.Count - 1); _w45Msg = ""; Show(V);
            };
            tabs.Children.Add(t); tabs.Children.Add(x);
        }

        // Textfeld mit Zeilennummern
        var tb = new TextBox
        {
            Name = "ed-text", Text = d.Text, AcceptsReturn = true, AcceptsTab = true, FontFamily = Mono, FontSize = _edFont, TextWrapping = _edWrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(8, 6), MinHeight = 420, VerticalContentAlignment = VerticalAlignment.Top,
        };
        ScrollViewer.SetVerticalScrollBarVisibility(tb, ScrollBarVisibility.Disabled);
        var lines = new TextBlock { Name = "ed-lines", FontFamily = Mono, FontSize = _edFont, Foreground = Muted, TextAlignment = TextAlignment.Right, Padding = new Thickness(8, 6, 6, 6), IsVisible = !_edWrap,
            LineHeight = double.NaN };
        var status = new TextBlock { Name = "ed-status", Foreground = Muted, FontSize = 11 };
        var lastCount = -1;
        void Numbers() { var n = d.Text.Count(c => c == '\n') + 1; if (n == lastCount) return; lastCount = n; lines.Text = string.Join('\n', Enumerable.Range(1, n)); }
        void Status() { var (l, c) = TextFileCodec.LineCol(d.Text, tb.CaretIndex); status.Text = $"Zeile {l}, Spalte {c} · {d.Text.Length:N0} Zeichen · {d.Info.Encoding}{(d.Info.Bom ? " mit BOM" : "")} · {d.Info.Eol}{(d.Dirty ? " · geändert" : "")}"; }
        Numbers(); Status();
        tb.TextChanged += (_, _) => { if (tb.Text == d.Text) return; d.Text = tb.Text ?? ""; if (!d.Dirty) { d.Dirty = true; if (tabs.Children[_edIdx * 2] is Button cur) cur.Content = d.Name + " •"; } Numbers(); Status(); };
        tb.PropertyChanged += (_, e) => { if (e.Property == TextBox.CaretIndexProperty) { d.Caret = tb.CaretIndex; Status(); } };
        tb.AttachedToVisualTree += (_, _) => { tb.CaretIndex = Math.Min(d.Caret, d.Text.Length); };
        tb.KeyDown += (_, e) =>
        {
            if (e.KeyModifiers != KeyModifiers.Control) return;
            if (e.Key == Key.S) { Try(V, () => { EditorSave(d.Path ?? Need(path, "einen Dateinamen")); Say("Gespeichert: " + d.Path, V); }); e.Handled = true; }
        };
        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Children = { lines, Pos(tb, 1) } };
        var editor = new Border { Name = "ed-frame", Background = TileBg, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(0, 8, 8, 8),
            Child = new ScrollViewer { Height = 470, HorizontalScrollBarVisibility = _edWrap ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = body } };

        // Suchen & Ersetzen, Gehe zu Zeile
        var find = Field("ed-find", "Suchen", 180, _edFind); var repl = Field("ed-replace", "Ersetzen durch", 180, _edRepl);
        var cs = new CheckBox { Name = "ed-case", Content = "Groß/klein", IsChecked = _edCase, FontSize = 11.5 }; cs.IsCheckedChanged += (_, _) => _edCase = cs.IsChecked == true;
        var rx = new CheckBox { Name = "ed-regex", Content = "Regulärer Ausdruck", IsChecked = _edRegex, FontSize = 11.5 }; rx.IsCheckedChanged += (_, _) => _edRegex = rx.IsChecked == true;
        var go = Field("ed-goto", "Zeile", 70);
        bool FindNext()
        {
            _edFind = find.Text ?? ""; var hit = TextFileCodec.Find(d.Text, _edFind, Math.Max(tb.SelectionStart, tb.SelectionEnd), _edCase, _edRegex);
            if (hit is not { } h) { status.Text = $"„{_edFind}“ nicht gefunden."; return false; }
            tb.Focus(); tb.CaretIndex = h.Start + h.Length; tb.SelectionStart = h.Start; tb.SelectionEnd = h.Start + h.Length; return true;
        }
        var search = Line(find, repl, cs, rx,
            Act("ed-find-next", "Weitersuchen", () => Try(V, () => FindNext()), true),
            Act("ed-replace-one", "Ersetzen", () => Try(V, () =>
            {
                _edRepl = repl.Text ?? "";
                var sel = tb.SelectedText ?? "";
                if (sel.Length > 0 && TextFileCodec.Find(sel, find.Text ?? "", 0, _edCase, _edRegex) is { Start: 0 } m && m.Length == sel.Length)
                {
                    var start = Math.Min(tb.SelectionStart, tb.SelectionEnd); var with = _edRegex ? TextFileCodec.ReplaceAll(sel, find.Text!, _edRepl, _edCase, true).Text : _edRepl;
                    tb.Text = d.Text[..start] + with + d.Text[(start + sel.Length)..]; tb.SelectionStart = tb.SelectionEnd = tb.CaretIndex = start + with.Length;
                }
                FindNext();
            })),
            Act("ed-replace-all", "Alle ersetzen", () => Try(V, () => { _edFind = find.Text ?? ""; _edRepl = repl.Text ?? ""; var (t, n) = TextFileCodec.ReplaceAll(d.Text, _edFind, _edRepl, _edCase, _edRegex); if (n > 0) tb.Text = t; status.Text = n == 0 ? $"„{_edFind}“ nicht gefunden." : $"{n} Stellen ersetzt."; })),
            go, Act("ed-goto-btn", "Gehe zu", () => Try(V, () => { var l = int.Parse(Need(go, "die Zeilennummer")); tb.CaretIndex = TextFileCodec.LineStart(d.Text, l); tb.Focus(); })));
        find.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Try(V, () => FindNext()); e.Handled = true; } };

        return AlltagPage("Editor", new StackPanel { Spacing = 6, Children = { bar, search, tabs, editor, status } });
    }

    // =====================================================================
    // N11 Rechner
    // =====================================================================
    private string _rcMode = "standard", _rcExpr = "", _rcResult = "", _rcGraph = "sin(x); x^2/4", _rcFrom = "-6,28", _rcTo = "6,28";
    private bool _rcDeg = true;
    private int _rcRadix = 10;
    private readonly List<string> _rcHist = [];
    public IReadOnlyList<string> CalcHistory => _rcHist;

    private static readonly Dictionary<string, string> KeyIds = new()
    {
        ["+"] = "plus", ["−"] = "minus", ["×"] = "mal", ["÷"] = "durch", ["="] = "gleich", [","] = "komma", ["%"] = "prozent", ["("] = "auf", [")"] = "zu", ["^"] = "hoch", ["!"] = "fak",
        ["π"] = "pi", ["√"] = "wurzel", ["C"] = "c", ["⌫"] = "zurueck", ["x²"] = "quadrat", ["1/x"] = "kehrwert", ["e"] = "e", ["±"] = "vorzeichen",
    };

    /// <summary>Ausdruck ausrechnen und im Verlauf merken (auch für Tests und die Tastatur).</summary>
    public string CalcRun(string expr)
    {
        try
        {
            var v = Calc.Eval(expr, _rcDeg, null); _rcResult = Calc.Format(v);
            _rcHist.Insert(0, $"{expr} = {_rcResult}"); if (_rcHist.Count > 30) _rcHist.RemoveAt(30);
        }
        catch (Exception ex) when (ex is FormatException or DivideByZeroException or OverflowException) { _rcResult = ex is DivideByZeroException ? ex.Message : "Fehler: " + ex.Message; }
        _rcExpr = expr; return _rcResult;
    }

    private Control RechnerView()
    {
        const string V = "rechner";
        var modes = new WrapPanel { Name = "rc-modes" };
        foreach (var (k, l) in new[] { ("standard", "Standard"), ("wissenschaftlich", "Wissenschaftlich"), ("programmierer", "Programmierer"), ("grafik", "Grafik"), ("datum", "Datum") })
        {
            var b = new Button { Name = "rc-mode-" + k, Content = l, FontSize = 12, Padding = new Thickness(12, 5), Margin = new Thickness(0, 0, 6, 4), CornerRadius = new CornerRadius(14),
                Background = _rcMode == k ? Accent : Brushes.Transparent, Foreground = Text, BorderBrush = Edge, BorderThickness = new Thickness(1) };
            b.Click += (_, _) => { _rcMode = k; Show(V); };
            modes.Children.Add(b);
        }
        Control body = _rcMode switch { "programmierer" => ProgView(), "grafik" => GraphPane(), "datum" => DatePane(), _ => KeypadView(_rcMode == "wissenschaftlich") };
        var units = Act("rc-units", "Einheiten umrechnen …", () => Show("alltag-helfer"));
        return AlltagPage("Rechner", new StackPanel { Spacing = 8, Children = { Row(modes, units), body } });
    }

    private Control KeypadView(bool sci)
    {
        const string V = "rechner";
        var input = new TextBox { Name = "rc-input", Text = _rcExpr, FontSize = 22, FontFamily = Mono, PlaceholderText = sci ? "z. B. sin(30) + √2 × π" : "z. B. (12,5 + 7) × 3", HorizontalContentAlignment = HorizontalAlignment.Right };
        var result = new TextBlock { Name = "rc-result", Text = _rcResult.Length > 0 ? "= " + _rcResult : "", FontSize = 28, FontWeight = FontWeight.SemiBold, Foreground = Text, HorizontalAlignment = HorizontalAlignment.Right };
        var hist = new StackPanel { Name = "rc-history", Spacing = 2 };
        void History() { hist.Children.Clear(); var i = 0; foreach (var h in _rcHist) { var expr = h[..h.LastIndexOf(" = ", StringComparison.Ordinal)]; var b = new Button { Name = $"rc-hist-{i++}", Content = h, FontSize = 11.5, Background = Brushes.Transparent, Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Right }; b.Click += (_, _) => { input.Text = expr; input.CaretIndex = expr.Length; }; hist.Children.Add(b); } if (i == 0) hist.Children.Add(P("Noch nichts gerechnet.", null, 11)); }
        void Run() { var r = CalcRun(input.Text ?? ""); result.Text = r.StartsWith("Fehler") || r.Contains("geht nicht") ? r : "= " + r; History(); }
        void Put(string s) { var c = Math.Clamp(input.CaretIndex, 0, input.Text?.Length ?? 0); input.Text = (input.Text ?? "").Insert(c, s); input.CaretIndex = c + s.Length; }
        input.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Run(); e.Handled = true; } else if (e.Key == Key.Escape) { input.Text = ""; result.Text = ""; } };

        string[][] std = [["C", "⌫", "%", "÷"], ["7", "8", "9", "×"], ["4", "5", "6", "−"], ["1", "2", "3", "+"], ["±", "0", ",", "="]];
        string[][] extra = [["sin", "cos", "tan", "π"], ["asin", "acos", "atan", "e"], ["x²", "^", "√", "!"], ["ln", "log", "1/x", "("], ["abs", "exp", "round", ")"]];
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(sci ? "*,*,*,*,12,*,*,*,*" : "*,*,*,*"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto"), Width = sci ? 700 : 340, HorizontalAlignment = HorizontalAlignment.Left };
        void AddKey(string k, int r, int c, bool fn)
        {
            var b = new Button { Name = "rc-key-" + (KeyIds.TryGetValue(k, out var id) ? id : k), Content = k, FontSize = 17, Height = 48, Margin = new Thickness(3), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
                CornerRadius = new CornerRadius(8), Foreground = Text, Background = k == "=" ? Accent : fn || !char.IsDigit(k[0]) && k != "," ? TileBg : Brush.Parse(C("#33FFFFFF", "#FFFFFFFF")), BorderBrush = Edge, BorderThickness = new Thickness(1) };
            b.Click += (_, _) =>
            {
                switch (k)
                {
                    case "=": Run(); break;
                    case "C": input.Text = ""; result.Text = ""; break;
                    case "⌫": var c0 = input.CaretIndex; if (c0 > 0) { input.Text = input.Text!.Remove(c0 - 1, 1); input.CaretIndex = c0 - 1; } break;
                    case "±": input.Text = (input.Text ?? "").StartsWith('-') ? input.Text![1..] : "-(" + input.Text + ")"; input.CaretIndex = input.Text.Length; break;
                    case "x²": Put("^2"); break;
                    case "1/x": input.Text = "1/(" + input.Text + ")"; input.CaretIndex = input.Text.Length; break;
                    case "sin" or "cos" or "tan" or "asin" or "acos" or "atan" or "ln" or "log" or "abs" or "exp" or "round": Put(k + "("); break;
                    case "√": Put("√("); break;
                    default: Put(k); break;
                }
            };
            Grid.SetRow(b, r); Grid.SetColumn(b, c); grid.Children.Add(b);
        }
        for (var r = 0; r < 5; r++) for (var c = 0; c < 4; c++) AddKey(std[r][c], r, sci ? c + 5 : c, false);
        if (sci) for (var r = 0; r < 5; r++) for (var c = 0; c < 4; c++) AddKey(extra[r][c], r, c, true);
        History();
        var head = new StackPanel { Spacing = 4, Children = { input, result } };
        if (sci)
        {
            var deg = new ToggleSwitch { Name = "rc-deg", IsChecked = _rcDeg, OnContent = "Grad", OffContent = "Bogenmaß" };
            deg.IsCheckedChanged += (_, _) => _rcDeg = deg.IsChecked == true;
            head.Children.Insert(0, deg);
        }
        return new Grid { ColumnDefinitions = new ColumnDefinitions("*,260"), Children = {
            Card(new StackPanel { Spacing = 8, Children = { head, grid } }),
            Pos(Card(new StackPanel { Spacing = 4, Children = { H("Verlauf", 13), hist, Act("rc-hist-clear", "Verlauf leeren", () => { _rcHist.Clear(); Show(V); }) } }), 1) } };
    }

    private Control ProgView()
    {
        const string V = "rechner";
        var a = Field("rc-prog-a", "Zahl", 220, ProgCalc.Show(0, _rcRadix)); var b = Field("rc-prog-b", "zweite Zahl", 220);
        a.FontFamily = Mono; b.FontFamily = Mono;
        var reps = new StackPanel { Spacing = 3 };
        var outs = new Dictionary<int, TextBlock>();
        foreach (var (r, l) in new[] { (16, "HEX"), (10, "DEC"), (8, "OCT"), (2, "BIN") })
        {
            var t = new TextBlock { Name = "rc-" + l.ToLowerInvariant(), FontFamily = Mono, FontSize = 14, Foreground = Text, TextWrapping = TextWrapping.Wrap };
            var radio = new Button { Name = "rc-radix-" + r, Content = l, Width = 54, FontSize = 11.5, Background = _rcRadix == r ? Accent : Brushes.Transparent, Foreground = Text, BorderBrush = Edge, BorderThickness = new Thickness(1) };
            var rr = r;
            radio.Click += (_, _) => Try(V, () => { var v = ProgCalc.Parse(a.Text ?? "", _rcRadix); _rcRadix = rr; Show(V); });
            outs[r] = t; reps.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("60,*"), Children = { radio, Pos(t, 1) } });
        }
        void Update() { try { var v = ProgCalc.Parse(a.Text ?? "", _rcRadix); foreach (var (r, t) in outs) t.Text = ProgCalc.Show(v, r); } catch (FormatException ex) { outs[10].Text = ex.Message; } }
        a.TextChanged += (_, _) => Update(); Update();
        var ops = new WrapPanel();
        foreach (var op in new[] { "AND", "OR", "XOR", "NAND", "NOR", "<<", ">>", "+", "-", "*", "/", "MOD" })
        {
            var o = op;
            ops.Children.Add(Act("rc-op-" + o.Replace("<<", "shl").Replace(">>", "shr").Replace("+", "plus").Replace("-", "minus").Replace("*", "mal").Replace("/", "durch").ToLowerInvariant(), o,
                () => Try(V, () => { var r = ProgCalc.Apply(o, ProgCalc.Parse(a.Text ?? "", _rcRadix), ProgCalc.Parse(Need(b, "die zweite Zahl"), _rcRadix)); a.Text = ProgCalc.Show(r, _rcRadix).Replace(" ", ""); })));
        }
        ops.Children.Add(Act("rc-op-not", "NOT", () => Try(V, () => a.Text = ProgCalc.Show(~ProgCalc.Parse(a.Text ?? "", _rcRadix), _rcRadix).Replace(" ", ""))));
        foreach (Control c in ops.Children) c.Margin = new Thickness(0, 0, 6, 6);
        return Card(new StackPanel { Spacing = 8, Children = {
            P($"Ganze Zahlen mit 64 Bit. Eingabe im {ProgCalc.Name(_rcRadix)}-System – oben links umstellen.", null, 11.5),
            Line(a, b), reps, ops } });
    }

    private Control GraphPane()
    {
        const string V = "rechner";
        var fx = Field("rc-graph-expr", "f(x), mehrere mit ; trennen", 340, _rcGraph);
        var from = Field("rc-graph-from", "von", 80, _rcFrom); var to = Field("rc-graph-to", "bis", 80, _rcTo);
        var gv = new GraphView { Name = "rc-graph", Height = 420, Background = TileBg, Foreground = Text };
        var legend = new WrapPanel();
        void Draw()
        {
            _rcGraph = fx.Text ?? ""; _rcFrom = from.Text ?? ""; _rcTo = to.Text ?? "";
            double a = Calc.Eval(_rcFrom), b = Calc.Eval(_rcTo);
            gv.Series.Clear(); legend.Children.Clear();
            foreach (var f in _rcGraph.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(GraphView.Palette.Length))
            {
                gv.Series.Add((f, Calc.Plot(f, a, b, 600, false)));
                legend.Children.Add(new TextBlock { Text = "■ f(x) = " + f, Foreground = Brush.Parse(GraphView.Palette[gv.Series.Count - 1]), FontSize = 12, Margin = new Thickness(0, 0, 14, 0) });
            }
            gv.InvalidateVisual();
        }
        try { Draw(); } catch (Exception ex) when (ex is FormatException or ArgumentException or DivideByZeroException) { legend.Children.Add(P(ex.Message, Brush.Parse("#F97316"))); }
        var draw = Act("rc-graph-draw", "Zeichnen", () => Try(V, Draw), true);
        fx.KeyDown += (_, e) => { if (e.Key == Key.Enter) Try(V, Draw); };
        return Card(new StackPanel { Spacing = 8, Children = {
            P("x im Bogenmaß. Beispiele: sin(x), x^3 − 2x, √x, 1/x, e^(−x^2). π darfst du als „pi“ schreiben.", null, 11.5),
            Line(P("f(x) ="), fx, P("von"), from, P("bis"), to, draw), legend, gv } });
    }

    private Control DatePane()
    {
        const string V = "rechner";
        var a = Field("rc-date-a", "TT.MM.JJJJ", 120, DateTime.Today.ToString("dd.MM.yyyy", De)); var b = Field("rc-date-b", "TT.MM.JJJJ", 120);
        var o1 = new TextBlock { Name = "rc-date-out", Foreground = Text, FontSize = 14, TextWrapping = TextWrapping.Wrap };
        var bs = Field("rc-date-base", "TT.MM.JJJJ", 120, DateTime.Today.ToString("dd.MM.yyyy", De));
        var yy = Field("rc-date-years", "Jahre", 70, "0"); var mm = Field("rc-date-months", "Monate", 70, "0"); var dd = Field("rc-date-days", "Tage", 70, "30");
        var o2 = new TextBlock { Name = "rc-date-out2", Foreground = Text, FontSize = 14 };
        DateTime D(TextBox t) => DateTime.TryParseExact(Clean(t), "d.M.yyyy", De, System.Globalization.DateTimeStyles.None, out var x) ? x : throw new ArgumentException("Datum bitte als TT.MM.JJJJ.");
        int I(TextBox t) => string.IsNullOrWhiteSpace(t.Text) ? 0 : int.Parse(t.Text.Trim(), De);
        return new StackPanel { Spacing = 10, Children = {
            Card(Section("Abstand zwischen zwei Tagen", "Wie lange noch bis zum Urlaub? Wie alt ist etwas? Werktage zählen Montag bis Freitag (ohne Feiertage).",
                Line(a, P("bis"), b, Act("rc-date-diff", "Berechnen", () => Try(V, () =>
                {
                    var r = DateCalc.Between(D(a), D(b));
                    o1.Text = $"{r.TotalDays:N0} Tage · {r.Years} Jahre, {r.Months} Monate, {r.Days} Tage · {r.TotalDays / 7} Wochen und {r.TotalDays % 7} Tage · {r.Workdays:N0} Werktage";
                }), true)), o1)),
            Card(Section("Zu einem Datum rechnen", "Negative Zahlen rechnen zurück, z. B. −14 Tage.",
                Line(bs, P("+"), yy, mm, dd, Act("rc-date-add", "Berechnen", () => Try(V, () => o2.Text = DateCalc.Add(D(bs), I(yy), I(mm), I(dd)).ToString("dddd, dd. MMMM yyyy", De)), true)), o2)) } };
    }

    // =====================================================================
    // N20 Uhr & Wecker
    // =====================================================================
    private readonly List<TimeSpan> _swLaps = [];
    private TextBlock? _uhrNow, _uhrSw, _uhrTimer;
    private readonly List<(string City, TextBlock Time)> _uhrWorld = [];
    private DateTime? _tmEnd; private TimeSpan _tmPaused;
    public bool TimerRunning => _tmEnd is not null;

    /// <summary>Wecker prüfen – auch wenn die Seite nicht offen ist. Liefert die fälligen Wecker.</summary>
    public List<Alarm> CheckAlarms(DateTime now)
    {
        var due = Wecker.TakeDue(now);
        foreach (var a in due) Ring($"Wecker {a.Hour:00}:{a.Minute:00} – {a.Label}", a.Label);
        return due;
    }

    private void Ring(string text, string label)
    {
        _core.Audit.Append("app", "clock.ring", label);
        Toast(text, ("Schlummern 5 Min.", () => { var t = DateTime.Now.AddMinutes(5); Wecker.Add(t.Hour, t.Minute, label + " (Schlummern)", 0); }));
        if (A11y.MayInterrupt(_settings.NotifyStyle, Focus.Until(DateTime.Now) is not null))
        {
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Activate();
            if (ToolPath("mpv") is { } mpv)
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(mpv) { ArgumentList = { "--no-video", "--really-quiet", AlarmTone.Ensure(_core.DataRoot) }, UseShellExecute = false, CreateNoWindow = true }); }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { }
        }
    }

    public void TimerStart(TimeSpan t) { _tmEnd = DateTime.Now + t; _tmPaused = TimeSpan.Zero; _core.Audit.Append("app", "clock.timer", $"{t.TotalMinutes:0.#} Min."); }
    public TimeSpan TimerLeft(DateTime now) => _tmEnd is { } e ? (e - now < TimeSpan.Zero ? TimeSpan.Zero : e - now) : _tmPaused;

    private void UhrTick(DateTime now)
    {
        if (now.Millisecond < 250) CheckAlarms(now);
        if (_tmEnd is { } end && now >= end) { _tmEnd = null; _tmPaused = TimeSpan.Zero; Ring("Die Zeit ist um!", "Timer"); }
        if (CurrentView != "uhr") return;
        if (_uhrNow is not null) _uhrNow.Text = now.ToString("HH:mm:ss", De);
        if (_uhrSw is not null) _uhrSw.Text = SwNow().ToString(@"hh\:mm\:ss\,ff");
        if (_uhrTimer is not null) _uhrTimer.Text = TimerLeft(now).ToString(@"hh\:mm\:ss");
        foreach (var (c, t) in _uhrWorld) if (WorldClock.At(c, now.ToUniversalTime()) is { } w) t.Text = w.Local.ToString("HH:mm", De);
    }

    private Control UhrView()
    {
        const string V = "uhr";
        var now = DateTime.Now;
        _uhrNow = new TextBlock { Name = "uhr-now", Text = now.ToString("HH:mm:ss", De), FontSize = 44, FontWeight = FontWeight.Light, Foreground = Text, FontFamily = Mono };
        var clock = Card(new StackPanel { Children = { _uhrNow, P(now.ToString("dddd, dd. MMMM yyyy", De), Text, 14), P("Kalenderwoche " + System.Globalization.ISOWeek.GetWeekOfYear(now), null, 11) } });

        // Wecker
        var at = Field("uhr-al-time", "HH:MM", 80, "07:00"); var lbl = Field("uhr-al-label", "Bezeichnung (optional)", 180);
        var days = new ComboBox { Name = "uhr-al-days", ItemsSource = new[] { "einmalig", "täglich", "Mo–Fr", "Wochenende" }, SelectedIndex = 0 };
        var list = new StackPanel { Spacing = 3 }; var i = 0;
        foreach (var a in Wecker.All())
        {
            var id = a.Id; var k = i++;
            var sw = new ToggleSwitch { Name = $"uhr-al-on-{k}", IsChecked = a.Enabled, OnContent = "", OffContent = "", MinWidth = 0 };
            sw.IsCheckedChanged += (_, _) => Wecker.Set(id, sw.IsChecked == true);
            var next = AlarmStore.Next(a, now);
            list.Children.Add(Row(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = {
                    new TextBlock { Text = $"{a.Hour:00}:{a.Minute:00}", FontSize = 22, Foreground = a.Enabled ? Text : Muted, FontFamily = Mono, VerticalAlignment = VerticalAlignment.Center },
                    new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { P(a.Label, Text, 12.5), P(AlarmStore.DaysText(a.Days) + (next is { } n ? $" · nächstes Mal {n:ddd dd.MM. HH:mm}" : " · aus"), null, 10.5) } } } },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { sw, Act($"uhr-al-del-{k}", "Löschen", () => { Wecker.Delete(id); Show(V); }) } }));
        }
        if (i == 0) list.Children.Add(P("Noch kein Wecker gestellt."));
        var alarms = Card(Section("Wecker", "Klingelt, solange Trendo läuft – mit Benachrichtigung und Schlummern. Im Fokusmodus nur still.",
            Line(at, lbl, days, Act("uhr-al-add", "Wecker stellen", () => Try(V, () =>
            {
                var p = Need(at, "die Uhrzeit").Split(':', '.');
                if (p.Length != 2 || !int.TryParse(p[0], out var h) || !int.TryParse(p[1], out var m)) throw new ArgumentException("Uhrzeit bitte als HH:MM, z. B. 06:30.");
                var a = Wecker.Add(h, m, lbl.Text ?? "", days.SelectedIndex switch { 1 => 127, 2 => 31, 3 => 96, _ => 0 });
                Say($"Wecker gestellt: {AlarmStore.Next(a, DateTime.Now):dddd, HH:mm} Uhr.", V);
            }), true)), list));

        // Timer
        _uhrTimer = new TextBlock { Name = "uhr-timer", Text = TimerLeft(now).ToString(@"hh\:mm\:ss"), FontSize = 30, Foreground = Text, FontFamily = Mono };
        var tmin = Field("uhr-tm-min", "Minuten", 80);
        var presets = new List<Control>();
        foreach (var m in new[] { 1, 3, 5, 10, 15, 30, 60 }) { var mm = m; presets.Add(Act($"uhr-tm-{m}", m < 60 ? $"{m} Min." : "1 Std.", () => { TimerStart(TimeSpan.FromMinutes(mm)); Show(V); })); }
        var timer = Card(Section("Timer", "Zählt rückwärts und meldet sich, auch wenn du die Seite wechselst.", _uhrTimer, Line([.. presets]),
            Line(tmin, Act("uhr-tm-start", "Start", () => Try(V, () => { TimerStart(TimeSpan.FromMinutes(double.Parse(Need(tmin, "die Minuten"), De))); Show(V); }), true),
                Act("uhr-tm-pause", _tmEnd is null ? "Weiter" : "Pause", () => { if (_tmEnd is { } e) { _tmPaused = e - DateTime.Now; _tmEnd = null; } else if (_tmPaused > TimeSpan.Zero) _tmEnd = DateTime.Now + _tmPaused; Show(V); }),
                Act("uhr-tm-stop", "Stopp", () => { _tmEnd = null; _tmPaused = TimeSpan.Zero; Show(V); }))));
        if (_tmEnd is null && _tmPaused == TimeSpan.Zero && timer.GetLogicalDescendants().OfType<Button>().FirstOrDefault(b => b.Name == "uhr-tm-pause") is { } pz) pz.IsVisible = false;

        // Stoppuhr mit Runden (teilt sich die Zeit mit „Zeit & Kalender“)
        _uhrSw = new TextBlock { Name = "uhr-sw", Text = SwNow().ToString(@"hh\:mm\:ss\,ff"), FontSize = 30, Foreground = Text, FontFamily = Mono };
        var laps = new StackPanel { Name = "uhr-laps", Spacing = 1 };
        for (var k = 0; k < _swLaps.Count; k++) laps.Children.Add(P($"Runde {k + 1}: {_swLaps[k]:hh\\:mm\\:ss\\,ff}  (+{(_swLaps[k] - (k > 0 ? _swLaps[k - 1] : TimeSpan.Zero)):mm\\:ss\\,ff})", Text, 11.5));
        var sw2 = Card(Section("Stoppuhr", "Läuft weiter, auch wenn du die Seite wechselst.", _uhrSw,
            Line(Act("uhr-sw-start", _swStart is null ? "Start" : "Pause", () => { if (_swStart is null) _swStart = DateTime.Now; else { _swAcc += DateTime.Now - _swStart.Value; _swStart = null; } Show(V); }, true),
                Act("uhr-sw-lap", "Runde", () => { if (_swStart is not null) { _swLaps.Add(SwNow()); Show(V); } }),
                Act("uhr-sw-reset", "Zurücksetzen", () => { _swStart = null; _swAcc = TimeSpan.Zero; _swLaps.Clear(); Show(V); })), laps));

        // Weltuhr
        _uhrWorld.Clear();
        var world = new UniformGrid { Columns = 4 };
        var wi = 0;
        foreach (var c in Weltuhr.Cities())
        {
            if (WorldClock.At(c, now.ToUniversalTime()) is not { } w) continue;
            var t = new TextBlock { Text = w.Local.ToString("HH:mm", De), FontSize = 24, Foreground = Text, FontFamily = Mono }; _uhrWorld.Add((c, t));
            var city = c;
            var del = new Button { Name = $"uhr-wc-del-{wi++}", Content = "×", Padding = new Thickness(6, 0), Background = Brushes.Transparent, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
            ToolTip.SetTip(del, "Entfernen"); del.Click += (_, _) => { Weltuhr.Remove(city); Show(V); };
            world.Children.Add(new Border { Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(10, 6), CornerRadius = new CornerRadius(10), Background = TileBg, BorderBrush = Edge, BorderThickness = new Thickness(1),
                Child = new Grid { Children = { new StackPanel { Children = { P(c, Text, 12.5), t, P((w.Local.Date != now.Date ? (w.Local.Date > now.Date ? "morgen · " : "gestern · ") : "") + WorldClock.OffsetText(w.Offset), null, 10.5) } }, del } } });
        }
        var pick = new ComboBox { Name = "uhr-wc-pick", ItemsSource = WorldClock.Choices.Select(x => x.City).Where(x => !Weltuhr.Cities().Contains(x)).ToList(), PlaceholderText = "Stadt wählen", MinWidth = 160 };
        var wc = Card(Section("Weltuhr", "Uhrzeit in anderen Städten, Sommerzeit wird automatisch berücksichtigt.", world,
            Line(pick, Act("uhr-wc-add", "Hinzufügen", () => { if (pick.SelectedItem is string s) { Weltuhr.Add(s); Show(V); } }))));

        return AlltagPage("Uhr & Wecker", new StackPanel { Spacing = 10, Children = { clock, alarms, new Grid { ColumnDefinitions = new ColumnDefinitions("*,10,*"), Children = { timer, Pos(sw2, 2) } }, wc } });
    }

    // =====================================================================
    // N18 Zettel
    // =====================================================================
    private Control ZettelView()
    {
        const string V = "zettel";
        var wrap = new WrapPanel { Name = "zt-board" };
        var all = Zettel.All(); var i = 0;
        foreach (var z0 in all)
        {
            var k = i++; var z = z0;
            var fg = Brush.Parse("#1F2933");
            var text = new TextBox { Name = $"zt-text-{k}", Text = z.Text, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 120, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = fg, FontSize = 13.5, PlaceholderText = "Schreib etwas …" };
            text.TextChanged += (_, _) => { if (text.Text != z.Text) { z = z with { Text = text.Text ?? "" }; Zettel.Update(z); } };
            var colors = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            foreach (var (name, hex) in ZettelStore.Colors)
            {
                var cb = new Button { Name = $"zt-color-{k}-{name}", Width = 16, Height = 16, Padding = new Thickness(0), CornerRadius = new CornerRadius(8), Background = Brush.Parse(hex), BorderBrush = Brush.Parse(z.Color == name ? "#1F2933" : "#661F2933"), BorderThickness = new Thickness(z.Color == name ? 2 : 1) };
                ToolTip.SetTip(cb, name); var n = name;
                cb.Click += (_, _) => { Zettel.Update(z with { Color = n }); Show(V); };
                colors.Children.Add(cb);
            }
            var pin = new CheckBox { Name = $"zt-pin-{k}", Content = "Startseite", IsChecked = z.OnDesktop, Foreground = fg, FontSize = 11 };
            pin.IsCheckedChanged += (_, _) => { z = z with { OnDesktop = pin.IsChecked == true }; Zettel.Update(z); };
            var del = new Button { Name = $"zt-del-{k}", Content = "Löschen", FontSize = 11, Padding = new Thickness(8, 2), Foreground = fg, Background = Brushes.Transparent, BorderBrush = Brush.Parse("#661F2933"), BorderThickness = new Thickness(1) };
            del.Click += (_, _) => { Zettel.Delete(z.Id); Show(V); };
            wrap.Children.Add(new Border { Width = 240, Margin = new Thickness(0, 0, 12, 12), Padding = new Thickness(10, 8), CornerRadius = new CornerRadius(4), Background = Brush.Parse(ZettelStore.HexOf(z.Color)),
                BoxShadow = BoxShadows.Parse("0 4 12 0 #40000000"),
                Child = new StackPanel { Spacing = 6, Children = { text, new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { colors, Pos(del, 1) } }, pin } } });
        }
        var add = Act("zt-add", "+ Neuer Zettel", () => { Zettel.Add(); Show(V); }, true);
        var body = new StackPanel { Spacing = 8, Children = { Row(P("Kurze Notizen zum Ankleben. Angeheftete Zettel siehst du auf der Startseite. Alles wird sofort gespeichert.", null, 11.5), add), wrap } };
        if (i == 0) body.Children.Add(P("Noch keine Zettel – „+ Neuer Zettel“ legt einen an."));
        return AlltagPage("Zettel", body);
    }

    /// <summary>N18: angeheftete Zettel auf der Startseite.</summary>
    private Control DesktopZettel()
    {
        var pinned = Zettel.Pinned();
        var row = new WrapPanel { Name = "desk-zettel", Margin = new Thickness(0, 0, 0, 6), HorizontalAlignment = HorizontalAlignment.Center, IsVisible = pinned.Count > 0 };
        foreach (var z in pinned.Take(6))
        {
            var b = new Button { Width = 170, Margin = new Thickness(4), Padding = new Thickness(10, 8), CornerRadius = new CornerRadius(4), Background = Brush.Parse(ZettelStore.HexOf(z.Color)),
                HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Top,
                Content = new TextBlock { Text = z.Text.Length > 140 ? z.Text[..140] + " …" : z.Text, Foreground = Brush.Parse("#1F2933"), FontSize = 12, TextWrapping = TextWrapping.Wrap, MaxHeight = 90 } };
            Avalonia.Automation.AutomationProperties.SetName(b, "Zettel: " + z.Text);
            b.Click += (_, _) => Show("zettel");
            row.Children.Add(b);
        }
        return row;
    }

    // =====================================================================
    // N5 Notizbuch
    // =====================================================================
    private string? _nbSec, _nbPage;
    private string _nbQuery = "";

    private Control NotebookView()
    {
        const string V = "notizbuch";
        var nb = Notizbuch;
        var secs = nb.Sections();
        if (secs.Count == 0) { nb.AddSection("Allgemein"); secs = nb.Sections(); }
        if (_nbSec is null || !secs.Contains(_nbSec)) { _nbSec = secs[0]; _nbPage = null; }
        var pages = nb.Pages(_nbSec);
        if (_nbPage is not null && !pages.Contains(_nbPage)) _nbPage = null;
        _nbPage ??= pages.FirstOrDefault();

        Button Item(string name, string label, bool on, Action go)
        {
            var b = new Button { Name = name, Content = label, FontSize = 12, Padding = new Thickness(10, 5), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = on ? Accent : Brushes.Transparent, Foreground = Text, CornerRadius = new CornerRadius(6) };
            b.Click += (_, _) => go(); return b;
        }
        // Abschnitte
        var secCol = new StackPanel { Spacing = 2, Children = { H("Abschnitte", 13) } };
        for (var k = 0; k < secs.Count; k++) { var s = secs[k]; secCol.Children.Add(Item($"nb-sec-{k}", s, s == _nbSec, () => { _nbSec = s; _nbPage = null; _w45Msg = ""; Show(V); })); }
        var ns = Field("nb-new-sec", "Neuer Abschnitt", 150);
        secCol.Children.Add(ns); secCol.Children.Add(Act("nb-add-sec", "Anlegen", () => Try(V, () => { nb.AddSection(Need(ns, "einen Namen")); _nbSec = Notebook.SafeName(ns.Text!); _nbPage = null; Show(V); })));

        // Seiten
        var pageCol = new StackPanel { Spacing = 2, Children = { H("Seiten", 13) } };
        for (var k = 0; k < pages.Count; k++) { var p = pages[k]; pageCol.Children.Add(Item($"nb-page-{k}", p, p == _nbPage, () => { _nbPage = p; _w45Msg = ""; Show(V); })); }
        if (pages.Count == 0) pageCol.Children.Add(P("Noch keine Seiten.", null, 11));
        var np = Field("nb-new-page", "Neue Seite", 150);
        pageCol.Children.Add(np);
        pageCol.Children.Add(Act("nb-add-page", "Anlegen", () => Try(V, () =>
        {
            var name = Notebook.SafeName(Need(np, "einen Seitentitel"));
            if (nb.Exists(_nbSec!, name)) throw new InvalidOperationException("Diese Seite gibt es schon.");
            nb.Write(_nbSec!, name, ""); _nbPage = name; Show(V);
        }), true));

        // Seite bearbeiten
        Control editor;
        if (_nbPage is null) editor = P("Leg links eine Seite an.", Text);
        else
        {
            var sec = _nbSec!; var page = _nbPage;
            var title = new TextBox { Name = "nb-title", Text = page, FontSize = 18, FontWeight = FontWeight.SemiBold };
            var text = new TextBox { Name = "nb-text", Text = nb.Read(sec, page), AcceptsReturn = true, AcceptsTab = true, TextWrapping = TextWrapping.Wrap, MinHeight = 380, FontSize = 13.5,
                PlaceholderText = "Schreib los … Überschriften mit #, Listen mit -, Schlagworte mit #wort." };
            var tags = new WrapPanel { Name = "nb-tags" };
            void Tags() { tags.Children.Clear(); foreach (var t in Notebook.Tags(text.Text ?? "")) tags.Children.Add(new Border { Margin = new Thickness(0, 0, 6, 4), Padding = new Thickness(8, 2), CornerRadius = new CornerRadius(10), Background = TileBg, Child = new TextBlock { Text = "#" + t, Foreground = Text, FontSize = 11 } }); }
            Tags();
            var state = new TextBlock { Name = "nb-state", Foreground = Muted, FontSize = 11, Text = "Gespeichert." };
            IDisposable? save = null;
            text.TextChanged += (_, _) =>
            {
                state.Text = "Wird gespeichert …";
                save?.Dispose(); save = DispatcherTimer.RunOnce(() => { nb.Write(sec, page, text.Text ?? ""); state.Text = "Gespeichert."; Tags(); }, TimeSpan.FromMilliseconds(600));
            };
            text.LostFocus += (_, _) => { save?.Dispose(); nb.Write(sec, page, text.Text ?? ""); state.Text = "Gespeichert."; Tags(); };
            editor = new StackPanel { Spacing = 6, Children = {
                Row(title, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
                    Act("nb-rename", "Umbenennen", () => Try(V, () => { var to = Notebook.SafeName(title.Text ?? ""); if (to != page) { nb.Write(sec, page, text.Text ?? ""); nb.RenamePage(sec, page, to); _nbPage = to; } Show(V); })),
                    Act("nb-to-ki", "Der KI als Wissen geben", () => { nb.Write(sec, page, text.Text ?? ""); Wissen.Add(page, text.Text ?? ""); Say($"„{page}“ ist jetzt Wissen für die KI.", V); }),
                    Act("nb-del", "Löschen", () => { nb.DeletePage(sec, page); _nbPage = null; Say($"„{page}“ liegt im Papierkorb des Notizbuchs.", V); }) } }),
                P($"{sec} · zuletzt geändert {File.GetLastWriteTime(Path.Combine(nb.Root, sec, page + ".md")):dd.MM.yyyy HH:mm}", null, 10.5), tags, text, state } };
            text.KeyDown += (_, e) => { if (e.Key == Key.S && e.KeyModifiers == KeyModifiers.Control) { nb.Write(sec, page, text.Text ?? ""); state.Text = "Gespeichert."; Tags(); e.Handled = true; } };
        }

        // Suche
        var q = Field("nb-search", "Im Notizbuch suchen … (#wort für Schlagworte)", 340, _nbQuery);
        var hits = new StackPanel { Name = "nb-hits", Spacing = 2 };
        if (_nbQuery.Length > 0)
        {
            var found = nb.Search(_nbQuery); var k = 0;
            foreach (var h in found) { var hh = h; hits.Children.Add(Item($"nb-hit-{k++}", $"{h.Section} › {h.Page}: {h.Snippet}", false, () => { _nbSec = hh.Section; _nbPage = hh.Page; Show(V); })); }
            if (k == 0) hits.Children.Add(P($"Nichts gefunden für „{_nbQuery}“."));
        }
        void Search() { _nbQuery = q.Text?.Trim() ?? ""; Show(V); }
        q.KeyDown += (_, e) => { if (e.Key == Key.Enter) Search(); };
        var tagLine = new WrapPanel();
        foreach (var (t, n) in nb.AllTags().OrderByDescending(x => x.Value).Take(12)) { var tt = t; var b = Act("nb-tag-" + t, $"#{t} ({n})", () => { _nbQuery = "#" + tt; Show(V); }); b.Margin = new Thickness(0, 0, 4, 4); tagLine.Children.Add(b); }

        var cols = new Grid { ColumnDefinitions = new ColumnDefinitions("190,10,200,10,*"), Children = { Card(secCol), Pos(Card(pageCol), 2), Pos(Card(editor), 4) } };
        return AlltagPage("Notizbuch", new StackPanel { Spacing = 8, Children = { Line(q, Act("nb-search-go", "Suchen", Search), Act("nb-search-clear", "Zurücksetzen", () => { _nbQuery = ""; Show(V); })), tagLine, hits, cols } });
    }
}
