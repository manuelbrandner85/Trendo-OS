using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Office;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>T-0228 Stufe C: Trendo Tabelle – eigenes Tabellenprogramm (wie Excel/Calc) mit Startseite (G1), Menüband (G2),
/// Seitenleiste „Zelle &amp; Funktionen“ (G4) und Statusleiste mit Summe/Mittelwert/Anzahl (G5). Dateien: XLSX, ODS, CSV.</summary>
public partial class MainWindow
{
    public SheetDoc SheetDocument { get; private set; } = new();
    public string? SheetPath { get; private set; }
    public bool SheetDirty { get; private set; }
    public int SheetIndex { get; private set; }
    public Sheet ActiveSheet => SheetDocument.Sheets[Math.Clamp(SheetIndex, 0, SheetDocument.Sheets.Count - 1)];
    public SheetGridView? SheetGrid { get; private set; }
    /// <summary>G1: Startseite zeigen, solange noch keine Mappe offen ist.</summary>
    public bool SheetStartVisible { get; private set; } = true;
    private SheetCalc _shCalc = new(new SheetDoc());
    private readonly List<SheetDoc> _shUndo = [], _shRedo = [];
    private string _shMsg = "", _shRibbon = "start", _shFnQuery = "";
    private (int Row, int Col) _shActive, _shAnchor; private int _shTop, _shLeft;
    private List<(int Row, int Col, Cell Cell)>? _shClip; private (int Row, int Col) _shClipOrigin;
    private bool _shLandscape; private readonly List<SheetChartView> _shCharts = [];
    private TextBox? _shFormula, _shEditor; private TextBlock? _shName, _shStats; private Canvas? _shOverlay;

    private void InitTabelle()
    {
        _showMap["tabelle"] = SheetPageView;
        PageModules["tabelle"] = "SYS-10";
        ExtraPages.Add(("Trendo Tabelle", "tabelle", ["tabelle", "excel", "calc", "xlsx", "ods", "csv", "rechnen", "haushaltsbuch", "summe", "formel"]));
    }

    // ---------- Öffentliche Wege (auch für Tests und die KI) ----------
    public void SheetNew(string template = "leer")
    {
        SheetDocument = SheetTemplates.Create(template); SheetPath = null; SheetDirty = false; SheetIndex = 0; ResetSheetState();
        _shMsg = template == "leer" ? "Neue Mappe." : $"Vorlage „{SheetTemplates.Builtin.First(t => t.Id == template).Name}“ – Werte einfach überschreiben, die Formeln rechnen mit.";
    }

    public void SheetOpen(string path)
    {
        SheetDocument = SheetFiles.Open(path); SheetIndex = 0; ResetSheetState();
        SheetPath = Path.GetExtension(path).ToLowerInvariant() is ".xlsx" or ".ods" ? path : null; SheetDirty = false;
        _shMsg = $"Geöffnet: {Path.GetFileName(path)}"; RememberSheetRecent(path);
        _core.Audit.Append(Principal.LocalOwner.Name, "tabelle.open", Path.GetFileName(path));
    }

    public void SheetSaveAs(string path)
    {
        if (path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)) SheetFiles.WriteCsv(SheetDocument, ActiveSheet, path);
        else { SheetFiles.Save(SheetDocument, path); if (!path.EndsWith(".ods", StringComparison.OrdinalIgnoreCase)) { SheetPath = path; SheetDirty = false; } }
        RememberSheetRecent(path); _shMsg = $"Gespeichert: {Path.GetFileName(path)}";
        _core.Audit.Append(Principal.LocalOwner.Name, "tabelle.save", Path.GetFileName(path));
    }

    public void SheetShowStart() => SheetStartVisible = true;

    /// <summary>E1: Fenster fixieren – Zeilen oberhalb/Spalten links der aktiven Zelle bleiben stehen.</summary>
    public void SheetToggleFreeze()
    {
        SheetSnapshot();
        if (SheetGrid is { } g) g.ToggleFreeze();
        else if (ActiveSheet.FreezeRows > 0 || ActiveSheet.FreezeCols > 0) ActiveSheet.FreezeRows = ActiveSheet.FreezeCols = 0;
        else { ActiveSheet.FreezeRows = Math.Max(1, _shActive.Row); ActiveSheet.FreezeCols = _shActive.Col; }
        SheetDirty = true;
        _shMsg = ActiveSheet.FreezeRows + ActiveSheet.FreezeCols > 0 ? $"Fixiert: {ActiveSheet.FreezeRows} Zeile(n), {ActiveSheet.FreezeCols} Spalte(n)." : "Fixierung aufgehoben.";
    }

    /// <summary>Zelle setzen wie durch Eintippen (mit Rückgängig).</summary>
    public void SheetSetCell(string a1, string raw) { var (r, c) = Addr.Parse(a1); SheetSnapshot(); WriteCell(r, c, raw); SheetChanged(); }

    /// <summary>Bereich markieren, z. B. „B2“ oder „A1:C4“.</summary>
    public void SheetSelect(string range)
    {
        var (t, l, b, r) = range.Contains(':') ? Addr.Range(range) : (Addr.Parse(range).Row, Addr.Parse(range).Col, Addr.Parse(range).Row, Addr.Parse(range).Col);
        _shAnchor = (t, l); _shActive = (b, r);
        if (SheetGrid is { } g) g.SelectRange(t, l, b, r);
        RefreshSheetChrome();
    }

    public string SheetDisplay(string a1) { var (r, c) = Addr.Parse(a1); return _shCalc.Display(ActiveSheet, r, c); }

    /// <summary>G5: Summe, Mittelwert und Anzahl der markierten Zahlen.</summary>
    public (double Sum, double? Average, int Count, int Numbers) SheetSelectionStats()
    {
        var (t, l, b, r) = Sel();
        double sum = 0; int count = 0, nums = 0;
        foreach (var kv in ActiveSheet.Cells)
        {
            var (row, col) = kv.Key;
            if (row < t || row > b || col < l || col > r || kv.Value.Raw.Length == 0 || ActiveSheet.HiddenRows.Contains(row)) continue;
            count++;
            var v = _shCalc.Value(ActiveSheet, row, col);
            if (v.IsNumber) { sum += v.Number; nums++; }
        }
        return (sum, nums > 0 ? sum / nums : null, count, nums);
    }

    public void SheetUndo() { if (_shUndo.Count == 0) return; _shRedo.Add(SheetDocument); SheetDocument = _shUndo[^1]; _shUndo.RemoveAt(_shUndo.Count - 1); AfterDocSwap(); }
    public void SheetRedo() { if (_shRedo.Count == 0) return; _shUndo.Add(SheetDocument); SheetDocument = _shRedo[^1]; _shRedo.RemoveAt(_shRedo.Count - 1); AfterDocSwap(); }

    /// <summary>Σ: Summe unter/neben die Zahlen setzen (wie AutoSumme).</summary>
    public void SheetAutoSum()
    {
        var (t, l, b, r) = Sel(); var s = ActiveSheet;
        SheetSnapshot();
        if (t == b && l == r)
        {
            // Zahlen direkt darüber suchen, sonst links davon
            var top = t - 1; while (top >= 0 && _shCalc.Value(s, top, l).IsNumber) top--;
            if (top < t - 1) WriteCell(t, l, $"=SUMME({Addr.Name(top + 1, l)}:{Addr.Name(t - 1, l)})");
            else
            {
                var left = l - 1; while (left >= 0 && _shCalc.Value(s, t, left).IsNumber) left--;
                WriteCell(t, l, left < l - 1 ? $"=SUMME({Addr.Name(t, left + 1)}:{Addr.Name(t, l - 1)})" : "=SUMME()");
            }
        }
        else for (var c = l; c <= r; c++) WriteCell(b + 1, c, $"=SUMME({Addr.Name(t, c)}:{Addr.Name(b, c)})");
        SheetChanged();
    }

    public void SheetSort(bool ascending)
    {
        var (t, l, b, r) = Region(); var by = _shActive.Col;
        SheetSnapshot(); ActiveSheet.SortRows(t, b, l, r, by, ascending, SheetDocument); SheetChanged();
        _shMsg = $"Sortiert nach Spalte {Addr.Col(by)} ({(ascending ? "A–Z" : "Z–A")}).";
    }

    /// <summary>Filter: nur Zeilen zeigen, deren Wert in dieser Spalte der aktiven Zelle gleicht.</summary>
    public void SheetFilterLikeActive()
    {
        var (t, _, b, _) = Region(); var key = _shCalc.Display(ActiveSheet, _shActive.Row, _shActive.Col); var target = _shCalc.Value(ActiveSheet, _shActive.Row, _shActive.Col);
        var hidden = ActiveSheet.Filter(t - 1, b, _shActive.Col, v => v.Kind == target.Kind && (v.Kind == CellValue.K.Text ? v.Text == target.Text : v.Number == target.Number), SheetDocument);
        _shMsg = $"Filter „{key}“: {hidden} Zeile(n) ausgeblendet."; SheetDirty = true;
    }

    public void SheetFill(bool down = true)
    {
        var (t, l, b, r) = Sel(); var s = ActiveSheet;
        if (down ? t == b : l == r) { _shMsg = "Zum Ausfüllen erst die Startwerte und die leeren Zellen darunter markieren."; return; }
        SheetSnapshot();
        for (var k = down ? l : t; k <= (down ? r : b); k++)
        {
            var seed = new List<string>(); var seedCells = new List<(int, int)>();
            for (var i = down ? t : l; i <= (down ? b : r); i++) { var (rr, cc) = down ? (i, k) : (k, i); var raw = s.Raw(rr, cc); if (raw.Length == 0) break; seed.Add(raw); seedCells.Add((rr, cc)); }
            if (seed.Count == 0) continue;
            var total = (down ? b - t : r - l) + 1 - seed.Count;
            if (seed.Any(x => x.StartsWith('=')))
            {
                var (sr, sc) = seedCells[^1];
                for (var n = 1; n <= total; n++)
                {
                    var (rr, cc) = down ? (sr + n, sc) : (sr, sc + n);
                    WriteCell(rr, cc, Formula.ShiftRefs(seed[^1], s.Name, 0, down ? n : 0, 0, down ? 0 : n));
                    s.GetOrAdd(rr, cc).Format = s.Get(sr, sc)?.Format ?? NumFmt.Standard;
                }
                continue;
            }
            var next = SheetSeries.Continue(seed, total);
            for (var n = 0; n < next.Count; n++) { var (rr, cc) = down ? (t + seed.Count + n, k) : (k, l + seed.Count + n); WriteCell(rr, cc, next[n]); }
        }
        SheetChanged(); _shMsg = "Ausgefüllt.";
    }

    /// <summary>E2: Diagramm aus der Markierung (oder der ganzen Liste) einfügen.</summary>
    public SheetChart SheetInsertChart(ChartKind kind)
    {
        var (t, l, b, r) = Sel();
        if (t == b && l == r) { var reg = Region(); (t, l, b, r) = (Math.Max(0, reg.Top - 1), reg.Left, reg.Bottom, reg.Right); }
        var range = $"{Addr.Name(t, l)}:{Addr.Name(b, r)}";
        var d = ChartData.From(ActiveSheet, _shCalc, range);
        var title = d.Series.Count == 1 && !d.Series[0].Name.StartsWith("Reihe ", StringComparison.Ordinal) ? d.Series[0].Name : kind switch { ChartKind.Kreis => "Anteile", ChartKind.Linie => "Verlauf", _ => "Vergleich" };
        var ch = new SheetChart(kind, range, title);
        SheetSnapshot(); ActiveSheet.Charts.Add(ch); SheetDirty = true;
        _shMsg = $"Diagramm „{title}“ aus {range} eingefügt – es rechnet mit, wenn du Zahlen änderst.";
        return ch;
    }

    /// <summary>E3: als PDF speichern (A4, auf Seitenbreite angepasst, Kopfzeile wiederholt, Diagramme dabei).</summary>
    public int SheetExportPdf(string path) => SheetPdf.Export(SheetDocument, ActiveSheet, path, new SheetPdf.Options(Landscape: _shLandscape));

    public void SheetSetLandscape(bool on) => _shLandscape = on;

    private void SheetPdfClick()
    {
        try
        {
            var f = DefaultSheetName(".pdf"); var n = SheetExportPdf(f);
            _shMsg = $"PDF gespeichert: {Path.GetFileName(f)} ({n} Seite{(n == 1 ? "" : "n")})";
            Toast($"PDF gespeichert: {Path.GetFileName(f)}", ("Öffnen", new Action(() => Launch(f))), ("Ordner öffnen", new Action(() => Launch(Path.GetDirectoryName(f)!))));
            _core.Audit.Append(Principal.LocalOwner.Name, "tabelle.pdf", Path.GetFileName(f));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) { _shMsg = "PDF ging nicht: " + ex.Message; }
    }

    private void SheetPrint()
    {
        try
        {
            var f = Path.Combine(Path.GetTempPath(), $"trendo-druck-{DateTime.Now:yyyyMMdd-HHmmss}.pdf");
            SheetExportPdf(f);
            var sumatra = ToolPath("sumatra");
            if (sumatra is not null && !_opt.Isolated) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(sumatra, $"-print-dialog \"{f}\"") { UseShellExecute = false });
            else Launch(f);
            _shMsg = "Druckansicht geöffnet – dort Drucker wählen.";
            _core.Audit.Append(Principal.LocalOwner.Name, "tabelle.print", Path.GetFileName(f));
        }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception or InvalidOperationException) { _shMsg = "Drucken ging nicht: " + ex.Message; }
    }

    // ---------- Innenleben ----------
    private void ResetSheetState()
    {
        _shUndo.Clear(); _shRedo.Clear(); _shActive = _shAnchor = (0, 0); _shTop = _shLeft = 0; SheetStartVisible = false;
        _shCalc = new SheetCalc(SheetDocument);
    }

    private void AfterDocSwap() { SheetIndex = Math.Min(SheetIndex, SheetDocument.Sheets.Count - 1); _shCalc = new SheetCalc(SheetDocument); SheetDirty = true; if (SheetGrid is { } g) { g.Doc = SheetDocument; g.Sheet = ActiveSheet; g.Calc = _shCalc; g.InvalidateVisual(); } RefreshSheetChrome(); }

    private void SheetSnapshot() { _shUndo.Add(SheetDocument.Clone()); if (_shUndo.Count > 100) _shUndo.RemoveAt(0); _shRedo.Clear(); }

    private void SheetChanged() { SheetDirty = true; _shCalc.Invalidate(); SheetGrid?.InvalidateVisual(); foreach (var c in _shCharts) c.InvalidateVisual(); RefreshSheetChrome(); }

    /// <summary>Eingabe deuten (deutsche Zahl, Euro, Prozent, Datum) und das passende Format vorschlagen.</summary>
    private void WriteCell(int r, int c, string raw)
    {
        var s = ActiveSheet;
        if (raw.Length == 0) { if (s.Get(r, c) is { } old) { old.Raw = ""; if (old.IsEmpty) s.Clear(r, c); } return; }
        var cell = s.GetOrAdd(r, c); cell.Raw = raw;
        if (!cell.IsFormula && cell.Format == NumFmt.Standard && SheetCalc.Literal(raw).Suggest is { } f) cell.Format = f;
    }

    private (int Top, int Left, int Bottom, int Right) Sel() =>
        (Math.Min(_shActive.Row, _shAnchor.Row), Math.Min(_shActive.Col, _shAnchor.Col), Math.Max(_shActive.Row, _shAnchor.Row), Math.Max(_shActive.Col, _shAnchor.Col));

    /// <summary>Datenbereich für Sortieren/Filtern: die Markierung, sonst die ganze Liste ohne Kopfzeile.</summary>
    private (int Top, int Left, int Bottom, int Right) Region()
    {
        var sel = Sel(); if (sel.Top != sel.Bottom) return sel;
        var s = ActiveSheet; var headerIsText = Enumerable.Range(0, Math.Max(1, s.MaxCol + 1)).Any(c => _shCalc.Value(s, 0, c).Kind == CellValue.K.Text);
        var bottom = s.MaxRow; var top = headerIsText ? 1 : 0;
        // bis zur ersten leeren Zeile (Summenzeilen darunter bleiben stehen)
        for (var r = top; r <= s.MaxRow; r++) if (Enumerable.Range(0, s.MaxCol + 1).All(c => s.Raw(r, c).Length == 0)) { bottom = r - 1; break; }
        return (top, 0, Math.Max(top, bottom), Math.Max(0, s.MaxCol));
    }

    private void ForSelection(Action<Cell> change) { var (t, l, b, r) = Sel(); SheetSnapshot(); for (var i = t; i <= b; i++) for (var j = l; j <= r; j++) change(ActiveSheet.GetOrAdd(i, j)); ActiveSheet.Compact(); SheetChanged(); }

    private string SheetRecentFile => Path.Combine(_core.DataRoot, "tabelle", "zuletzt.txt");
    private List<string> SheetRecent() => File.Exists(SheetRecentFile) ? File.ReadAllLines(SheetRecentFile).Where(File.Exists).Take(8).ToList() : [];
    private void RememberSheetRecent(string path)
    {
        var list = SheetRecent().Where(p => !p.Equals(path, StringComparison.OrdinalIgnoreCase)).Prepend(Path.GetFullPath(path)).Take(8).ToList();
        Directory.CreateDirectory(Path.GetDirectoryName(SheetRecentFile)!); File.WriteAllLines(SheetRecentFile, list);
    }

    private string DefaultSheetName(string ext)
    {
        var dir = Path.Combine(ProjectsRoot, "Tabellen"); Directory.CreateDirectory(dir);
        var baseName = string.Concat(ActiveSheet.Name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c));
        var f = Path.Combine(dir, baseName + ext);
        for (var i = 2; File.Exists(f); i++) f = Path.Combine(dir, $"{baseName} ({i}){ext}");
        return f;
    }

    private async Task<string?> PickSheet(bool save)
    {
        var req = new PickRequest(save ? "tabelle-save" : "tabelle-open", false, false, [.. SheetFiles.Extensions]);
        if (PickOverride is { } fake) return (await fake(req)) is [var f, ..] ? f : null;
        if (save)
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Speichern unter", SuggestedFileName = Path.GetFileName(DefaultSheetName(".xlsx")), DefaultExtension = "xlsx",
                FileTypeChoices = [new FilePickerFileType("Excel-Mappe") { Patterns = ["*.xlsx"] }, new FilePickerFileType("CSV (nur dieses Blatt)") { Patterns = ["*.csv"] }] });
            return file?.TryGetLocalPath();
        }
        var got = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Tabelle öffnen", FileTypeFilter = [new FilePickerFileType("Tabellen") { Patterns = ["*.xlsx", "*.ods", "*.csv"] }] });
        return got.Select(f => f.TryGetLocalPath()).OfType<string>().FirstOrDefault();
    }

    private async Task SheetSaveClick(bool asNew)
    {
        var path = !asNew && SheetPath is not null ? SheetPath : await PickSheet(true);
        if (path is null) return;
        try { SheetSaveAs(path); Toast($"Gespeichert: {Path.GetFileName(path)}", ("Ordner öffnen", new Action(() => Launch(Path.GetDirectoryName(path)!)))); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _shMsg = "Speichern ging nicht: " + ex.Message; }
        Show("tabelle");
    }

    private async Task SheetOpenClick()
    {
        var path = await PickSheet(false);
        if (path is null) return;
        try { SheetOpen(path); } catch (Exception ex) when (ex is IOException or InvalidDataException or System.Xml.XmlException) { _shMsg = "Öffnen ging nicht: " + ex.Message; }
        Show("tabelle");
    }

    // ---------- Zwischenablage ----------
    private void SheetCopy(bool cut)
    {
        var (t, l, b, r) = Sel(); var s = ActiveSheet;
        _shClip = []; _shClipOrigin = (t, l);
        var lines = new List<string>();
        for (var i = t; i <= b; i++)
        {
            var row = new List<string>();
            for (var j = l; j <= r; j++) { if (s.Get(i, j) is { } c) _shClip.Add((i - t, j - l, c.Clone())); row.Add(_shCalc.Display(s, i, j)); }
            lines.Add(string.Join('\t', row));
        }
        if (Clipboard is { } cb) _ = cb.SetTextAsync(string.Join("\r\n", lines));
        if (cut) { SheetSnapshot(); for (var i = t; i <= b; i++) for (var j = l; j <= r; j++) s.Clear(i, j); SheetChanged(); }
        _shMsg = cut ? "Ausgeschnitten." : "Kopiert.";
    }

    private async Task SheetPaste()
    {
        var (t, l, _, _) = Sel(); var s = ActiveSheet;
        if (_shClip is { } clip)
        {
            SheetSnapshot();
            foreach (var (dr, dc, cell) in clip)
            {
                var copy = cell.Clone();
                copy.Raw = Formula.ShiftRefs(cell.Raw, s.Name, 0, t - _shClipOrigin.Row, 0, l - _shClipOrigin.Col);
                var target = s.GetOrAdd(t + dr, l + dc); target.Raw = copy.Raw; target.Bold = copy.Bold; target.Fill = copy.Fill; target.Border = copy.Border; target.Format = copy.Format;
            }
            SheetChanged(); _shMsg = "Eingefügt."; return;
        }
        if (Clipboard is not { } cb || await cb.TryGetTextAsync() is not { Length: > 0 } text) return;
        SheetSnapshot();
        var rows = text.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        for (var i = 0; i < rows.Length; i++) { var cols = rows[i].Split('\t'); for (var j = 0; j < cols.Length; j++) WriteCell(t + i, l + j, cols[j]); }
        SheetChanged(); _shMsg = "Eingefügt.";
    }

    // ---------- Oberfläche ----------
    private Control SheetPageView() => new Border { Name = "tabelle-page-root", Background = Brush.Parse(C("#F2081329", "#F7F4F7FC")), CornerRadius = new CornerRadius(10), Padding = new Thickness(10), Child = SheetStartVisible ? SheetStartPage() : SheetShell() };

    /// <summary>G1: Startseite mit „Neu“, Vorlagen, Zuletzt geöffnet und Öffnen.</summary>
    private Control SheetStartPage()
    {
        var sp = new StackPanel { Spacing = 14 };
        sp.Children.Add(PageHead("Trendo Tabelle"));
        sp.Children.Add(new TextBlock { Text = "Neu", Foreground = Text, FontSize = 16, FontWeight = FontWeight.SemiBold });
        var tiles = new WrapPanel();
        foreach (var t in SheetTemplates.Builtin)
        {
            var id = t.Id;
            var b = new Button { Name = "tabelle-tpl-" + id, Width = 180, Height = 132, Margin = new Thickness(0, 0, 12, 12), Padding = new Thickness(12), CornerRadius = new CornerRadius(12), Background = TileBg, BorderBrush = Edge, BorderThickness = new Thickness(1),
                Content = new StackPanel { Spacing = 6, Children = {
                    new TextBlock { Text = t.Icon, FontSize = 34 },
                    new TextBlock { Text = t.Name, Foreground = Text, FontSize = 14, FontWeight = FontWeight.SemiBold },
                    new TextBlock { Text = t.Hint, Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap } } } };
            b.Click += (_, _) => { SheetNew(id); Show("tabelle"); };
            tiles.Children.Add(b);
        }
        sp.Children.Add(tiles);
        var open = new Button { Name = "tabelle-open", Content = "📂  Datei öffnen … (Excel, LibreOffice, CSV)", Padding = new Thickness(14, 8) };
        open.Click += (_, _) => _ = SheetOpenClick();
        sp.Children.Add(open);
        var recent = SheetRecent();
        sp.Children.Add(new TextBlock { Text = "Zuletzt geöffnet", Foreground = Text, FontSize = 16, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 6, 0, 0) });
        if (recent.Count == 0) sp.Children.Add(new TextBlock { Text = "Noch nichts – deine Tabellen erscheinen hier, sobald du sie speicherst oder öffnest.", Foreground = Muted, FontSize = 13 });
        var i = 0;
        foreach (var f in recent)
        {
            var path = f;
            var b = new Button { Name = $"tabelle-recent-{i++}", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(10, 6),
                Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = {
                    new TextBlock { Text = "▦", FontSize = 18, Foreground = Brush.Parse("#34D399") },
                    new StackPanel { Children = { new TextBlock { Text = Path.GetFileName(path), Foreground = Text, FontSize = 13 }, new TextBlock { Text = $"{Path.GetDirectoryName(path)} · {File.GetLastWriteTime(path):dd.MM.yyyy HH:mm}", Foreground = Muted, FontSize = 12 } } } } } };
            b.Click += (_, _) => { try { SheetOpen(path); } catch (Exception ex) when (ex is IOException or InvalidDataException or System.Xml.XmlException) { _shMsg = "Öffnen ging nicht: " + ex.Message; } Show("tabelle"); };
            sp.Children.Add(b);
        }
        return new ScrollViewer { Content = sp };
    }

    private Control SheetShell()
    {
        var g = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*,Auto,Auto"), ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var head = PageHead("Trendo Tabelle"); Grid.SetColumnSpan(head, 2); g.Children.Add(head);
        var ribbon = SheetRibbon(); Grid.SetRow(ribbon, 1); Grid.SetColumnSpan(ribbon, 2); g.Children.Add(ribbon);
        var fbar = SheetFormulaBar(); Grid.SetRow(fbar, 2); Grid.SetColumnSpan(fbar, 2); g.Children.Add(fbar);
        var area = SheetArea(); Grid.SetRow(area, 3); g.Children.Add(area);
        var side = SheetSidePanel(); Grid.SetRow(side, 3); Grid.SetColumn(side, 1); g.Children.Add(side);
        var tabs = SheetTabs(); Grid.SetRow(tabs, 4); Grid.SetColumnSpan(tabs, 2); g.Children.Add(tabs);
        _shStats = new TextBlock { Name = "tabelle-status", Foreground = Muted, FontSize = 12, Margin = new Thickness(4, 6, 0, 0), TextWrapping = TextWrapping.Wrap };
        Grid.SetRow(_shStats, 5); Grid.SetColumnSpan(_shStats, 2); g.Children.Add(_shStats);
        RefreshSheetChrome();
        return g;
    }

    /// <summary>Knopf im Menüband (nimmt dem Raster den Fokus nicht weg).</summary>
    private Button SB(string name, string icon, string label, Action go, string tip, bool on = false)
    {
        var b = new Button { Name = name, Focusable = false, Margin = new Thickness(0, 0, 4, 0), Padding = new Thickness(8, 4), MinWidth = 54, HorizontalContentAlignment = HorizontalAlignment.Center,
            Content = new StackPanel { Spacing = 1, Children = { new TextBlock { Text = icon, FontSize = 17, HorizontalAlignment = HorizontalAlignment.Center }, new TextBlock { Text = label, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center } } } };
        if (on) b.Background = Accent;
        ToolTip.SetTip(b, tip); Avalonia.Automation.AutomationProperties.SetName(b, label);
        b.Click += (_, _) => { go(); Show("tabelle"); };
        return b;
    }

    private static Control RibbonGroup(string title, params Control[] items)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var i in items) row.Children.Add(i);
        return new Border { BorderBrush = Brush.Parse("#33FFFFFF"), BorderThickness = new Thickness(0, 0, 1, 0), Padding = new Thickness(6, 0, 8, 0), Margin = new Thickness(0, 0, 6, 4),
            Child = new StackPanel { Spacing = 2, Children = { row, new TextBlock { Text = title, FontSize = 12, Opacity = 0.7, HorizontalAlignment = HorizontalAlignment.Center } } } };
    }

    /// <summary>G2: Menüband mit Reitern Start · Einfügen · Daten · Ansicht und Gruppen.</summary>
    private Control SheetRibbon()
    {
        var tabRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Margin = new Thickness(0, 4, 0, 4) };
        var file = new Button { Name = "tabelle-file", Content = "Datei ▾", Focusable = false, Padding = new Thickness(10, 4), Background = Accent };
        var fm = new ContextMenu();
        MenuItem Mi(string name, string header, Action a) { var m = new MenuItem { Name = name, Header = header }; m.Click += (_, _) => a(); return m; }
        fm.Items.Add(Mi("tabelle-m-start", "Startseite (Neu, Vorlagen, Zuletzt)", () => { SheetShowStart(); Show("tabelle"); }));
        fm.Items.Add(Mi("tabelle-m-open", "Öffnen …", () => _ = SheetOpenClick()));
        fm.Items.Add(Mi("tabelle-m-save", "Speichern  (Strg+S)", () => _ = SheetSaveClick(false)));
        fm.Items.Add(Mi("tabelle-m-saveas", "Speichern unter … (XLSX, CSV)", () => _ = SheetSaveClick(true)));
        fm.Items.Add(Mi("tabelle-m-pdf", "Als PDF speichern", () => { SheetPdfClick(); Show("tabelle"); }));
        fm.Items.Add(Mi("tabelle-m-print", "Drucken …", () => { SheetPrint(); Show("tabelle"); }));
        file.ContextMenu = fm; file.Click += (_, _) => fm.Open(file);
        tabRow.Children.Add(file);
        foreach (var (id, label) in new[] { ("start", "Start"), ("einfuegen", "Einfügen"), ("daten", "Daten"), ("ansicht", "Ansicht") })
        {
            var key = id;
            var b = new Button { Name = "tabelle-rib-" + id, Content = label, Focusable = false, Padding = new Thickness(12, 4), Background = _shRibbon == id ? Brush.Parse("#332F86F6") : Brushes.Transparent, FontWeight = _shRibbon == id ? FontWeight.SemiBold : FontWeight.Normal };
            b.Click += (_, _) => { _shRibbon = key; Show("tabelle"); };
            tabRow.Children.Add(b);
        }
        var cur = ActiveSheet.Get(_shActive.Row, _shActive.Col);
        var groups = new WrapPanel();
        switch (_shRibbon)
        {
            case "einfuegen":
                groups.Children.Add(RibbonGroup("Zeilen & Spalten",
                    SB("tabelle-row-ins", "⤓", "Zeile", () => { SheetSnapshot(); ActiveSheet.InsertRows(Sel().Top, Sel().Bottom - Sel().Top + 1); SheetChanged(); }, "Zeile(n) oberhalb einfügen"),
                    SB("tabelle-col-ins", "⇥", "Spalte", () => { SheetSnapshot(); ActiveSheet.InsertCols(Sel().Left, Sel().Right - Sel().Left + 1); SheetChanged(); }, "Spalte(n) links einfügen"),
                    SB("tabelle-row-del", "✕", "Zeile weg", () => { SheetSnapshot(); ActiveSheet.DeleteRows(Sel().Top, Sel().Bottom - Sel().Top + 1); SheetChanged(); }, "Markierte Zeile(n) löschen"),
                    SB("tabelle-col-del", "✕", "Spalte weg", () => { SheetSnapshot(); ActiveSheet.DeleteCols(Sel().Left, Sel().Right - Sel().Left + 1); SheetChanged(); }, "Markierte Spalte(n) löschen")));
                groups.Children.Add(RibbonGroup("Diagramm",
                    SB("tabelle-chart-balken", "▮▮", "Säulen", () => SheetInsertChart(ChartKind.Balken), "Säulendiagramm aus der Markierung"),
                    SB("tabelle-chart-linie", "📈", "Linie", () => SheetInsertChart(ChartKind.Linie), "Liniendiagramm (Verlauf)"),
                    SB("tabelle-chart-kreis", "◔", "Kreis", () => SheetInsertChart(ChartKind.Kreis), "Kreisdiagramm (Anteile)")));
                groups.Children.Add(RibbonGroup("Formeln",
                    SB("tabelle-sum2", "Σ", "Summe", SheetAutoSum, "Summe der Zahlen darüber"),
                    SB("tabelle-today", "📅", "Heute", () => SheetSetCell(Addr.Name(_shActive.Row, _shActive.Col), DateTime.Today.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)), "Heutiges Datum eintragen")));
                break;
            case "daten":
                groups.Children.Add(RibbonGroup("Sortieren & Filtern",
                    SB("tabelle-sort-az", "↑", "A–Z", () => SheetSort(true), "Liste nach der aktiven Spalte aufsteigend sortieren"),
                    SB("tabelle-sort-za", "↓", "Z–A", () => SheetSort(false), "Absteigend sortieren"),
                    SB("tabelle-filter", "⏷", "Filter", SheetFilterLikeActive, "Nur Zeilen mit dem Wert der aktiven Zelle zeigen"),
                    SB("tabelle-filter-off", "⊘", "Alle zeigen", () => { ActiveSheet.ClearFilter(); _shMsg = "Filter aufgehoben."; }, "Filter aufheben", ActiveSheet.HiddenRows.Count > 0)));
                groups.Children.Add(RibbonGroup("Ausfüllen",
                    SB("tabelle-fill-down", "⬇", "Nach unten", () => SheetFill(true), "Reihe nach unten fortsetzen (1, 2, 3 … · Mo, Di … · Januar, Februar …)"),
                    SB("tabelle-fill-right", "➡", "Nach rechts", () => SheetFill(false), "Reihe nach rechts fortsetzen")));
                break;
            case "ansicht":
                groups.Children.Add(RibbonGroup("Ansicht",
                    SB("tabelle-focus", "⛶", "Vollbild", ToggleFocusMode, "Vollbild/Fokusmodus (F11)"),
                    SB("tabelle-freeze", "❄", "Fixieren", SheetToggleFreeze, "Zeilen oberhalb und Spalten links der aktiven Zelle beim Blättern stehen lassen (erneut = aufheben)", ActiveSheet.FreezeRows > 0 || ActiveSheet.FreezeCols > 0),
                    SB("tabelle-gotoA1", "⌂", "Zu A1", () => { _shActive = _shAnchor = (0, 0); _shTop = _shLeft = 0; }, "Zum Anfang (Strg+Pos1)")));
                groups.Children.Add(RibbonGroup("Drucken",
                    SB("tabelle-landscape", "▭", "Querformat", () => _shLandscape = !_shLandscape, "Seite quer statt hoch (für breite Tabellen)", _shLandscape),
                    SB("tabelle-pdf", "📄", "Als PDF", SheetPdfClick, "Als PDF speichern – auf Seitenbreite angepasst, mit Diagrammen"),
                    SB("tabelle-print", "🖨", "Drucken", SheetPrint, "Drucken (Strg+P)")));
                break;
            default:
                groups.Children.Add(RibbonGroup("Zwischenablage",
                    SB("tabelle-paste", "📋", "Einfügen", () => _ = SheetPaste(), "Einfügen (Strg+V)"),
                    SB("tabelle-copy", "⧉", "Kopieren", () => SheetCopy(false), "Kopieren (Strg+C)"),
                    SB("tabelle-cut", "✂", "Ausschneiden", () => SheetCopy(true), "Ausschneiden (Strg+X)")));
                groups.Children.Add(RibbonGroup("Schrift",
                    SB("tabelle-bold", "𝐅", "Fett", () => { var on = !(cur?.Bold ?? false); ForSelection(c => c.Bold = on); }, "Fett (Strg+B)", cur?.Bold == true),
                    SB("tabelle-border", "▢", "Rahmen", () => { var on = !(cur?.Border ?? false); ForSelection(c => c.Border = on); }, "Rahmen um die Zellen", cur?.Border == true),
                    FillPicker()));
                var fmt = new ComboBox { Name = "tabelle-format", Width = 130, Focusable = false, ItemsSource = Enum.GetNames<NumFmt>(), SelectedItem = (cur?.Format ?? NumFmt.Standard).ToString() };
                fmt.SelectionChanged += (_, _) => { if (fmt.SelectedItem is string s && Enum.TryParse<NumFmt>(s, out var f) && f != (ActiveSheet.Get(_shActive.Row, _shActive.Col)?.Format ?? NumFmt.Standard)) { ForSelection(c => c.Format = f); Show("tabelle"); } };
                ToolTip.SetTip(fmt, "Zahlenformat: Euro, Prozent, Datum …");
                groups.Children.Add(RibbonGroup("Zahl", new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { fmt } },
                    SB("tabelle-euro", "€", "Euro", () => ForSelection(c => c.Format = NumFmt.Euro), "Als Euro-Betrag"),
                    SB("tabelle-pct", "%", "Prozent", () => ForSelection(c => c.Format = NumFmt.Prozent), "Als Prozent")));
                groups.Children.Add(RibbonGroup("Bearbeiten",
                    SB("tabelle-sum", "Σ", "Summe", SheetAutoSum, "Summe der Zahlen darüber (wie AutoSumme)"),
                    SB("tabelle-sort", "↑", "Sortieren", () => SheetSort(true), "Liste nach der aktiven Spalte sortieren"),
                    SB("tabelle-undo", "↶", "Zurück", SheetUndo, "Rückgängig (Strg+Z)"),
                    SB("tabelle-redo", "↷", "Vor", SheetRedo, "Wiederholen (Strg+Y)")));
                break;
        }
        return new StackPanel { Children = { tabRow, groups } };
    }

    private Control FillPicker()
    {
        var b = SB("tabelle-fill", "🎨", "Füllfarbe", () => { }, "Hintergrundfarbe der Zellen");
        var menu = new ContextMenu();
        foreach (var (name, hex) in new[] { ("Keine", (string?)null), ("Gelb", "#FFFFF3B0"), ("Grün", "#FFD1FAE5"), ("Blau", "#FFDCE8FB"), ("Rot", "#FFFDE2E2"), ("Grau", "#FFE5E7EB") })
        {
            var h = hex; var mi = new MenuItem { Name = "tabelle-fill-" + name.ToLowerInvariant(), Header = name };
            mi.Click += (_, _) => { ForSelection(c => c.Fill = h); Show("tabelle"); };
            menu.Items.Add(mi);
        }
        b.ContextMenu = menu; b.Click += (_, _) => menu.Open(b);
        return b;
    }

    private Control SheetFormulaBar()
    {
        _shName = new TextBlock { Name = "tabelle-namebox", Width = 84, FontSize = 13, VerticalAlignment = VerticalAlignment.Center, Foreground = Text, Margin = new Thickness(6, 0) };
        _shFormula = new TextBox { Name = "tabelle-formula", FontSize = 13, PlaceholderText = "Inhalt oder Formel, z. B. =SUMME(B2:B8)", MinHeight = 30 };
        _shFormula.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { SheetSnapshot(); WriteCell(_shActive.Row, _shActive.Col, _shFormula.Text ?? ""); SheetChanged(); SheetGrid?.Focus(); e.Handled = true; }
            else if (e.Key == Key.Escape) { RefreshSheetChrome(); SheetGrid?.Focus(); e.Handled = true; }
        };
        var fx = new TextBlock { Text = "fx", FontStyle = FontStyle.Italic, FontSize = 14, Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 8, 0) };
        var d = new DockPanel { Margin = new Thickness(0, 2, 0, 6) };
        DockPanel.SetDock(fx, Avalonia.Controls.Dock.Left);
        d.Children.Add(new Border { Child = _shName, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Margin = new Thickness(0, 0, 6, 0), [DockPanel.DockProperty] = Avalonia.Controls.Dock.Left });
        d.Children.Add(fx); d.Children.Add(_shFormula);
        return d;
    }

    private Control SheetArea()
    {
        var grid = new SheetGridView { Name = "tabelle-grid", Doc = SheetDocument, Sheet = ActiveSheet, Calc = _shCalc, Dark = false };
        grid.Restore(_shActive, _shAnchor, _shTop, _shLeft);
        SheetGrid = grid;
        _shOverlay = new Canvas { IsHitTestVisible = true };
        grid.SelectionChanged += () => { _shActive = grid.Active; _shAnchor = grid.Anchor; _shTop = grid.TopRow; _shLeft = grid.LeftCol; CloseEditor(true); RefreshSheetChrome(); };
        grid.EditRequested += OpenEditor;
        grid.Changed += () => SheetDirty = true;
        grid.Command += e =>
        {
            var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control); e.Handled = true;
            switch (e.Key)
            {
                case Key.Delete or Key.Back: ForSelection(c => c.Raw = ""); break;
                case Key.C when ctrl: SheetCopy(false); break;
                case Key.X when ctrl: SheetCopy(true); break;
                case Key.V when ctrl: _ = SheetPaste(); break;
                case Key.Z when ctrl: SheetUndo(); break;
                case Key.Y when ctrl: SheetRedo(); break;
                case Key.B when ctrl: var on = !(ActiveSheet.Get(_shActive.Row, _shActive.Col)?.Bold ?? false); ForSelection(c => c.Bold = on); break;
                case Key.S when ctrl: _ = SheetSaveClick(false); break;
                case Key.A when ctrl: grid.SelectRange(0, 0, Math.Max(0, ActiveSheet.MaxRow), Math.Max(0, ActiveSheet.MaxCol)); break;
                case Key.D when ctrl: SheetFill(true); break;
                case Key.P when ctrl: SheetPrint(); break;
                default: e.Handled = false; break;
            }
        };
        Dispatcher.UIThread.Post(() => grid.Focus());
        var charts = new StackPanel { Name = "tabelle-charts", Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 30, 14, 0) };
        _shCharts.Clear();
        for (var i = 0; i < ActiveSheet.Charts.Count; i++)
        {
            var ch = ActiveSheet.Charts[i]; var idx = i;
            var view = new SheetChartView(ch, () => ChartData.From(ActiveSheet, _shCalc, ch.Range)) { Name = $"tabelle-chart-{i}", Width = 400, Height = 250 };
            _shCharts.Add(view);
            var del = new Button { Name = $"tabelle-chart-del-{i}", Content = "✕", Focusable = false, Padding = new Thickness(6, 0), Background = Brushes.Transparent, Foreground = Brush.Parse("#5B6B82"), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
            ToolTip.SetTip(del, "Diagramm entfernen");
            del.Click += (_, _) => { SheetSnapshot(); ActiveSheet.Charts.RemoveAt(idx); SheetDirty = true; Show("tabelle"); };
            charts.Children.Add(new Border { Background = Brushes.White, CornerRadius = new CornerRadius(8), BorderBrush = Brush.Parse("#FFD8DEE8"), BorderThickness = new Thickness(1), BoxShadow = BoxShadows.Parse("0 4 16 0 #40000000"), ClipToBounds = true,
                Child = new Panel { Children = { view, del } } });
        }
        var host = new Panel { Children = { grid, charts, _shOverlay } };
        return new Border { Child = host, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), ClipToBounds = true, MinHeight = 320 };
    }

    private void OpenEditor(string? first)
    {
        if (SheetGrid is not { } g || _shOverlay is null || g.CellRect(_shActive.Row, _shActive.Col) is not { } rect) return;
        CloseEditor(true);
        var (row, col) = _shActive;
        var tb = new TextBox { Name = "tabelle-editor", Text = first ?? ActiveSheet.Raw(row, col), MinWidth = rect.Width, Height = rect.Height, MinHeight = 0, Padding = new Thickness(3, 2), FontSize = 13,
            Background = Brushes.White, Foreground = Brushes.Black, BorderBrush = Brush.Parse("#2F86F6"), BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(0) };
        Canvas.SetLeft(tb, rect.X); Canvas.SetTop(tb, rect.Y);
        tb.KeyDown += (_, e) =>
        {
            var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
            if (e.Key is Key.Enter or Key.Tab) { CommitEditor(); g.Select(e.Key == Key.Enter ? (shift ? row - 1 : row + 1) : row, e.Key == Key.Tab ? (shift ? col - 1 : col + 1) : col); g.Focus(); e.Handled = true; }
            else if (e.Key == Key.Escape) { CloseEditor(false); g.Focus(); e.Handled = true; }
        };
        tb.TextChanged += (_, _) => { if (_shFormula is { } f) f.Text = tb.Text; };
        tb.LostFocus += (_, _) => { if (_shEditor == tb) CommitEditor(); };
        _shEditor = tb; _shOverlay.Children.Add(tb);
        Dispatcher.UIThread.Post(() => { tb.Focus(); tb.CaretIndex = tb.Text?.Length ?? 0; });
    }

    private void CommitEditor()
    {
        if (_shEditor is not { } tb) return;
        _shEditor = null; _shOverlay?.Children.Remove(tb);
        var text = tb.Text ?? "";
        if (text == ActiveSheet.Raw(_shActive.Row, _shActive.Col)) return;
        SheetSnapshot(); WriteCell(_shActive.Row, _shActive.Col, text); SheetChanged();
    }

    private void CloseEditor(bool commit) { if (_shEditor is null) return; if (commit) CommitEditor(); else { _shOverlay?.Children.Remove(_shEditor); _shEditor = null; RefreshSheetChrome(); } }

    /// <summary>Für Tests: Bearbeiten öffnen, Text tippen und mit Enter bestätigen.</summary>
    public void SheetType(string text) { OpenEditor(text); if (_shEditor is { } tb) { tb.Text = text; CommitEditor(); SheetGrid?.Select(_shActive.Row + 1, _shActive.Col); } }

    /// <summary>G4: Seitenleiste – die gewählte Zelle verständlich erklärt, dazu „Funktion einfügen“.</summary>
    private Control SheetSidePanel()
    {
        var sp = new StackPanel { Name = "tabelle-side", Width = 250, Spacing = 6, Margin = new Thickness(10, 0, 0, 0) };
        var (r, c) = _shActive; var cell = ActiveSheet.Get(r, c); var v = _shCalc.Value(ActiveSheet, r, c);
        sp.Children.Add(new TextBlock { Text = $"Zelle {Addr.Name(r, c)}", Foreground = Text, FontSize = 15, FontWeight = FontWeight.SemiBold });
        var what = cell is null || cell.Raw.Length == 0 ? "Leer – einfach lostippen." :
            cell.IsFormula ? (v.IsError ? $"Formel mit Fehler {v.Text}: {SheetErrorHelp(v.Text)}" : $"Formel, Ergebnis: {SheetFormat.Display(v, cell.Format)}") :
            v.IsNumber ? $"Zahl ({cell.Format})" : v.Kind == CellValue.K.Bool ? "Wahrheitswert" : "Text";
        sp.Children.Add(new TextBlock { Name = "tabelle-side-what", Text = what, Foreground = v.IsError ? Brush.Parse("#F87171") : Muted, FontSize = 12.5, TextWrapping = TextWrapping.Wrap });
        sp.Children.Add(new TextBlock { Text = "Funktion einfügen", Foreground = Text, FontSize = 14, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 8, 0, 0) });
        var q = new TextBox { Name = "tabelle-fn-search", Text = _shFnQuery, PlaceholderText = "z. B. summe, wenn, datum" };
        var list = new StackPanel { Name = "tabelle-fn-list", Spacing = 2 };
        void Fill()
        {
            list.Children.Clear();
            var query = (q.Text ?? "").Trim().ToUpperInvariant(); _shFnQuery = q.Text ?? "";
            var names = Formula.GermanNames.Where(n => query.Length == 0 ? Popular.Contains(n) : n.Contains(query, StringComparison.Ordinal)).Take(10);
            foreach (var n in names)
            {
                var name = n;
                var b = new Button { Name = "tabelle-fn-" + name, Content = name, Focusable = false, HorizontalAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(8, 3) };
                ToolTip.SetTip(b, FnHint(name));
                b.Click += (_, _) =>
                {
                    var (sr, sc) = _shActive;
                    var arg = sr > 0 && _shCalc.Value(ActiveSheet, sr - 1, sc).IsNumber ? $"{Addr.Name(FirstNumberAbove(sr, sc), sc)}:{Addr.Name(sr - 1, sc)}" : "";
                    SheetSnapshot(); WriteCell(sr, sc, $"={name}({arg})"); SheetChanged(); OpenEditor(null);
                };
                list.Children.Add(b);
            }
        }
        q.TextChanged += (_, _) => Fill(); Fill();
        sp.Children.Add(q); sp.Children.Add(list);
        sp.Children.Add(new TextBlock { Text = "So geht's: Zelle anklicken, tippen, Enter. Formeln beginnen mit „=“, z. B. =B2*C2. Strg+Q findet jeden Befehl.", Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) });
        return new ScrollViewer { Content = sp };
    }

    int FirstNumberAbove(int r, int c) { var top = r - 1; while (top > 0 && _shCalc.Value(ActiveSheet, top - 1, c).IsNumber) top--; return top; }

    static readonly HashSet<string> Popular = ["SUMME", "MITTELWERT", "ANZAHL", "MIN", "MAX", "WENN", "RUNDEN", "SVERWEIS", "SUMMEWENN", "HEUTE"];

    static string FnHint(string n) => n switch
    {
        "SUMME" => "Addiert Zahlen: =SUMME(B2:B8)", "MITTELWERT" => "Durchschnitt: =MITTELWERT(B2:B8)", "ANZAHL" => "Zählt Zahlen im Bereich",
        "MIN" => "Kleinster Wert", "MAX" => "Größter Wert", "WENN" => "=WENN(B2>100;\"teuer\";\"ok\")", "RUNDEN" => "=RUNDEN(B2;2) rundet auf 2 Stellen",
        "SVERWEIS" => "Sucht in der ersten Spalte und gibt einen Wert daneben zurück", "SUMMEWENN" => "Summe nur, wenn eine Bedingung passt", "HEUTE" => "Heutiges Datum",
        _ => "Funktion " + n,
    };

    static string SheetErrorHelp(string code) => code switch
    {
        "#DIV/0!" => "Hier wird durch 0 oder eine leere Zelle geteilt.",
        "#NAME?" => "Unbekannter Funktionsname – Schreibweise prüfen.",
        "#WERT!" => "Ein Text steht dort, wo eine Zahl gebraucht wird.",
        "#BEZUG!" => "Die Formel zeigt auf eine gelöschte Zelle.",
        "#ZIRKEL!" => "Die Formel braucht ihr eigenes Ergebnis (Zirkelbezug).",
        "#NV" => "Der gesuchte Wert wurde nicht gefunden.",
        _ => "Formel prüfen.",
    };

    private Control SheetTabs()
    {
        var row = new StackPanel { Name = "tabelle-sheets", Orientation = Orientation.Horizontal, Spacing = 2, Margin = new Thickness(0, 6, 0, 0) };
        for (var i = 0; i < SheetDocument.Sheets.Count; i++)
        {
            var idx = i; var sh = SheetDocument.Sheets[i];
            var b = new Button { Name = $"tabelle-sheet-{i}", Content = sh.Name, Focusable = false, Padding = new Thickness(12, 4), CornerRadius = new CornerRadius(0, 0, 6, 6),
                Background = idx == SheetIndex ? Brushes.White : Brush.Parse("#22FFFFFF"), Foreground = idx == SheetIndex ? Brush.Parse("#0F7B3F") : Text, FontWeight = idx == SheetIndex ? FontWeight.SemiBold : FontWeight.Normal };
            b.Click += (_, _) => { CloseEditor(true); SheetIndex = idx; _shActive = _shAnchor = (0, 0); _shTop = _shLeft = 0; Show("tabelle"); };
            var menu = new ContextMenu();
            var ren = new MenuItem { Header = "Umbenennen …" }; ren.Click += (_, _) => RenameSheetPrompt(sh);
            var del = new MenuItem { Header = "Blatt löschen", IsEnabled = SheetDocument.Sheets.Count > 1 }; del.Click += (_, _) => { SheetSnapshot(); SheetDocument.RemoveSheet(sh); SheetIndex = Math.Max(0, SheetIndex - 1); AfterDocSwap(); Show("tabelle"); };
            menu.Items.Add(ren); menu.Items.Add(del); b.ContextMenu = menu;
            row.Children.Add(b);
        }
        var add = new Button { Name = "tabelle-sheet-add", Content = "+", Focusable = false, Padding = new Thickness(10, 4) };
        ToolTip.SetTip(add, "Neues Blatt");
        add.Click += (_, _) => { SheetSnapshot(); SheetDocument.AddSheet(); SheetIndex = SheetDocument.Sheets.Count - 1; _shActive = _shAnchor = (0, 0); _shTop = _shLeft = 0; SheetDirty = true; Show("tabelle"); };
        row.Children.Add(add);
        return new ScrollViewer { Content = row, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
    }

    private void RenameSheetPrompt(Sheet sh)
    {
        var tb = new TextBox { Name = "tabelle-rename", Text = sh.Name, Width = 200 };
        tb.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            try { SheetSnapshot(); SheetDocument.RenameSheet(sh, tb.Text ?? sh.Name); SheetDirty = true; }
            catch (ArgumentException ex) { _shMsg = ex.Message; }
            Show("tabelle");
        };
        if (_shOverlay is { } o) { Canvas.SetLeft(tb, 60); Canvas.SetTop(tb, 40); o.Children.Add(tb); Dispatcher.UIThread.Post(() => { tb.Focus(); tb.SelectAll(); }); }
    }

    public void SheetRename(int index, string name) { SheetSnapshot(); SheetDocument.RenameSheet(SheetDocument.Sheets[index], name); SheetDirty = true; }

    /// <summary>Formelleiste, Namensfeld und Statusleiste (G5) auf den neuesten Stand bringen, ohne die Seite neu zu bauen.</summary>
    private void RefreshSheetChrome()
    {
        var (r, c) = _shActive; var (t, l, b, rr) = Sel();
        if (_shName is { } n) n.Text = t == b && l == rr ? Addr.Name(r, c) : $"{Addr.Name(t, l)}:{Addr.Name(b, rr)}";
        if (_shFormula is { } f && _shEditor is null) f.Text = ActiveSheet.Raw(r, c);
        if (_shStats is { } s)
        {
            var st = SheetSelectionStats();
            var parts = new List<string> { SheetPath is null ? "Noch nicht gespeichert" : Path.GetFileName(SheetPath) + (SheetDirty ? " · geändert" : " · gespeichert") };
            if (st.Numbers > 1 || (t != b || l != rr)) { var nf = ActiveSheet.Get(r, c)?.Format is NumFmt.Euro or NumFmt.Prozent ? ActiveSheet.Get(r, c)!.Format : NumFmt.Zahl;
                parts.Add($"Summe: {SheetFormat.Display(CellValue.Num(st.Sum), nf)}"); if (st.Average is { } a) parts.Add($"Mittelwert: {SheetFormat.Display(CellValue.Num(a), nf)}"); parts.Add($"Anzahl: {st.Count}"); }
            if (ActiveSheet.HiddenRows.Count > 0) parts.Add($"{ActiveSheet.HiddenRows.Count} Zeile(n) gefiltert");
            if (_shMsg.Length > 0) parts.Add(_shMsg);
            s.Text = string.Join(" · ", parts);
        }
    }
}
