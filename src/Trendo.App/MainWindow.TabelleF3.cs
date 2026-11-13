using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core.Office;

namespace Trendo.App;

/// <summary>F3 Tabelle (Vorbild-Analyse „Nächste Schritte“): Autofilter mit Werteliste je Spalte und Pivot-Tabelle – bedient in der rechten Leiste.</summary>
public partial class MainWindow
{
    /// <summary>Rechte Leiste: „pivot“ zeigt den Pivot-Bauer, sonst Zelle und Funktionen.</summary>
    private string _shSide = "";
    private (int Rows, int? Cols, int Values, SheetPivot.Agg Agg) _shPivot = (-1, null, -1, SheetPivot.Agg.Summe);

    /// <summary>Pivot-Bauer rechts öffnen (wie „⊞ Pivot“).</summary>
    public void SheetShowPivotPanel() { _shSide = "pivot"; Show("tabelle"); }
    /// <summary>Zahlenformat der Markierung setzen.</summary>
    public void SheetFormatSelection(NumFmt f) => ForSelection(c => c.Format = f);

    /// <summary>Liste mit Kopfzeile: (Kopfzeile, links, letzte Datenzeile, rechts).</summary>
    (int Header, int Left, int Bottom, int Right) SheetList()
    {
        var (t, l, b, r) = Region();
        var header = Math.Max(0, t - 1);
        if (t == 0) header = 0; // ohne Textkopf: erste Zeile gilt als Kopf
        return (header, l, Math.Max(header + 1, b), r);
    }

    /// <summary>Autofilter an/aus für die Liste um die aktive Zelle.</summary>
    public void SheetToggleAutoFilter()
    {
        var s = ActiveSheet;
        if (s.AutoFilter is not null) { s.ClearFilter(); _shMsg = "Autofilter aus – alle Zeilen sichtbar."; SheetChanged(); return; }
        var (h, l, b, r) = SheetList();
        if (b <= h) { _shMsg = "Für den Autofilter braucht die Liste eine Kopfzeile und Daten darunter."; return; }
        s.AutoFilter = new SheetAutoFilter(h, b, l, r); s.HiddenRows.Clear();
        _shMsg = $"Autofilter an für {Addr.Col(l)}{h + 1}:{Addr.Col(r)}{b + 1} – rechts je Spalte Werte an- oder abhaken.";
        SheetChanged();
    }

    /// <summary>Spaltenfilter setzen: nur diese Werte zeigen (null = Spalte ungefiltert).</summary>
    public int SheetSetColumnFilter(int col, IEnumerable<string>? allowed)
    {
        var af = ActiveSheet.AutoFilter ?? throw new InvalidOperationException("Der Autofilter ist aus.");
        if (allowed is null) af.Columns.Remove(col); else af.Columns[col] = [.. allowed];
        var n = SheetPivot.Apply(SheetDocument, ActiveSheet);
        _shMsg = n == 0 ? "Autofilter: alle Zeilen sichtbar." : "Autofilter angewendet."; SheetChanged(); // die Anzahl zeigt die Statusleiste schon
        return n;
    }

    /// <summary>Pivot-Tabelle aus der Liste um die aktive Zelle auf ein neues Blatt.</summary>
    public Sheet SheetCreatePivot(int rowCol, int? colCol, int valCol, SheetPivot.Agg agg)
    {
        var (h, l, b, r) = SheetList(); var src = ActiveSheet;
        SheetSnapshot();
        var p = SheetPivot.Build(SheetDocument, src, h, l, b, r, rowCol, colCol, valCol, agg);
        SheetIndex = SheetDocument.Sheets.IndexOf(p); _shActive = _shAnchor = (4, 1); _shTop = _shLeft = 0; _shSide = "";
        AfterDocSwap();
        _shMsg = $"Pivot-Tabelle auf Blatt „{p.Name}“ – die Zahlen sind Formeln und rechnen mit, wenn sich „{src.Name}“ ändert.";
        _core.Audit.Append(Trendo.Core.Principal.LocalOwner.Name, "tabelle.pivot", $"{src.Name} → {p.Name}");
        return p;
    }

    /// <summary>Abschnitte der rechten Leiste für Autofilter und Pivot (null = nichts davon zu zeigen).</summary>
    private Control? SheetF3Side()
    {
        var sp = new StackPanel { Spacing = 6 };
        if (_shSide == "pivot") sp.Children.Add(SheetPivotCard());
        if (ActiveSheet.AutoFilter is { } af) sp.Children.Add(SheetAutoFilterCard(af));
        return sp.Children.Count == 0 ? null : sp;
    }

    Control SheetPivotCard()
    {
        var (h, l, b, r) = SheetList();
        var heads = SheetPivot.Headers(SheetDocument, ActiveSheet, h, l, r);
        var cols = Enumerable.Range(l, r - l + 1).ToList();
        if (_shPivot.Rows < l || _shPivot.Rows > r) _shPivot.Rows = cols.FirstOrDefault(c => !_shCalc.Value(ActiveSheet, h + 1, c).IsNumber, l);
        if (_shPivot.Values < l || _shPivot.Values > r) _shPivot.Values = cols.LastOrDefault(c => _shCalc.Value(ActiveSheet, h + 1, c).IsNumber, r);
        ComboBox Pick(string name, IEnumerable<string> items, int index) => new() { Name = name, ItemsSource = items.ToArray(), SelectedIndex = index, HorizontalAlignment = HorizontalAlignment.Stretch };
        var rows = Pick("tabelle-pivot-rows", heads, _shPivot.Rows - l);
        var colsBox = Pick("tabelle-pivot-cols", heads.Prepend("(keine)"), _shPivot.Cols is { } pc ? pc - l + 1 : 0);
        var vals = Pick("tabelle-pivot-values", heads, _shPivot.Values - l);
        var agg = Pick("tabelle-pivot-agg", SheetPivot.AggLabels, (int)_shPivot.Agg);
        var msg = new TextBlock { Name = "tabelle-pivot-msg", Foreground = Brush.Parse("#F87171"), FontSize = 12, TextWrapping = TextWrapping.Wrap };
        var go = new Button { Name = "tabelle-pivot-go", Content = "Pivot-Tabelle erstellen", Background = Accent, Foreground = Brushes.White, Focusable = false, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center };
        go.Click += (_, _) =>
        {
            // Auswahl beim Klick lesen (SelectionChanged kann noch ausstehen)
            _shPivot = (l + Math.Max(0, rows.SelectedIndex), colsBox.SelectedIndex <= 0 ? null : l + colsBox.SelectedIndex - 1, l + Math.Max(0, vals.SelectedIndex), (SheetPivot.Agg)Math.Max(0, agg.SelectedIndex));
            try { SheetCreatePivot(_shPivot.Rows, _shPivot.Cols, _shPivot.Values, _shPivot.Agg); Show("tabelle"); }
            catch (ArgumentException ex) { msg.Text = ex.Message; }
        };
        var close = new Button { Name = "tabelle-pivot-close", Content = "Schließen", Focusable = false };
        close.Click += (_, _) => { _shSide = ""; Show("tabelle"); };
        Control L(string t) => new TextBlock { Text = t, Foreground = Muted, FontSize = 12 };
        return Card("tabelle-pivot", "⊞ Pivot-Tabelle", $"Fasst die Liste {Addr.Col(l)}{h + 1}:{Addr.Col(r)}{b + 1} zusammen – auf einem neuen Blatt.",
            L("Zeilen"), rows, L("Spalten (optional)"), colsBox, L("Werte"), vals, L("Berechnung"), agg, msg, go, close);
    }

    Control SheetAutoFilterCard(SheetAutoFilter af)
    {
        var heads = SheetPivot.Headers(SheetDocument, ActiveSheet, af.Header, af.Left, af.Right);
        var list = new StackPanel { Spacing = 4 };
        for (var c = af.Left; c <= af.Right; c++)
        {
            var col = c; var values = SheetPivot.Values(SheetDocument, ActiveSheet, af.Header + 1, af.Bottom, col);
            var on = af.Columns.TryGetValue(col, out var set) ? set : null;
            var boxes = new StackPanel { Spacing = 0 };
            var all = new Button { Name = $"tabelle-af-all-{col}", Content = "Alle", FontSize = 12, Padding = new Thickness(8, 1), Focusable = false, IsEnabled = on is not null };
            all.Click += (_, _) => { SheetSetColumnFilter(col, null); Show("tabelle"); };
            boxes.Children.Add(all);
            for (var i = 0; i < values.Count; i++)
            {
                var (v, n) = values[i];
                var cb = new CheckBox { Name = $"tabelle-af-{col}-{i}", Content = $"{v}  ({n})", IsChecked = on is null || on.Contains(v), Foreground = Text, FontSize = 12, Focusable = false };
                cb.IsCheckedChanged += (_, _) =>
                {
                    var cur = ActiveSheet.AutoFilter?.Columns.TryGetValue(col, out var s0) == true ? new HashSet<string>(s0) : values.Select(x => x.Value).ToHashSet();
                    if (cb.IsChecked == true) cur.Add(v); else cur.Remove(v);
                    SheetSetColumnFilter(col, cur.Count == values.Count ? null : cur); Show("tabelle");
                };
                boxes.Children.Add(cb);
            }
            var label = on is null ? heads[col - af.Left] : $"{heads[col - af.Left]} ({on.Count} von {values.Count})";
            list.Children.Add(new Expander { Name = $"tabelle-af-col-{col}", Header = label, IsExpanded = on is not null, HorizontalAlignment = HorizontalAlignment.Stretch,
                Content = new ScrollViewer { MaxHeight = 220, Content = boxes } });
        }
        var off = new Button { Name = "tabelle-af-off", Content = "Autofilter aus", Focusable = false };
        off.Click += (_, _) => { SheetToggleAutoFilter(); Show("tabelle"); };
        return Card("tabelle-af", "Autofilter", ActiveSheet.HiddenRows.Count == 0 ? "Werte abhaken, die du sehen willst." : $"{ActiveSheet.HiddenRows.Count} Zeile(n) ausgeblendet.", list, off);
    }

    Border Card(string name, string title, string hint, params Control[] items)
    {
        var sp = new StackPanel { Spacing = 5, Children = { new TextBlock { Text = title, Foreground = Text, FontSize = 14, FontWeight = FontWeight.SemiBold }, new TextBlock { Text = hint, Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap } } };
        foreach (var i in items) sp.Children.Add(i);
        return new Border { Name = name, Background = TileBg, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 8), Child = sp };
    }
}
