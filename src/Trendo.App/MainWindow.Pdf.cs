using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Trendo.Core;
using Trendo.Core.Sys;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>T-0223 Trendo PDF (Stufe 1, P1–P6): eigenes PDF-Programm statt SumatraPDF/qpdf. Ansehen, Suchen, Lesezeichen, Markieren, Notizen,
/// Zusammenfügen, Seiten auswählen, Teilen, Drehen, Passwort, Formulare. Die geöffnete Datei wird nie überschrieben: Anmerkungen landen in
/// „Name (Trendo).pdf“, alle Werkzeuge schreiben neue Dateien daneben.</summary>
public partial class MainWindow
{
    public string? PdfPath { get; private set; }
    public int PdfPage { get; private set; }
    public int PdfPages { get; private set; }
    private string? _pdfPassword, _pdfZoom = "seite", _pdfMode;
    private double _pdfPercent = 100;
    private int _pdfRot; private bool _pdfDouble; private string _pdfMsg = "", _pdfQuery = "";
    private List<PdfView.Hit> _pdfHits = [];
    private IReadOnlyList<(double W, double H)> _pdfSizes = [];
    private bool _pdfLocked;

    private void InitPdf()
    {
        _showMap["pdf"] = PdfPageView;
        PageModules["pdf"] = "SYS-10"; // Dokumente
        ExtraPages.Add(("Trendo PDF", "pdf", ["pdf", "zusammenfügen", "teilen", "passwort", "formular", "markieren", "lesezeichen"]));
    }

    private void PdfSay(string m) { _pdfMsg = m; }

    /// <summary>PDF öffnen (auch per Test). Mit Passwortschutz erscheint zuerst die Passwortabfrage.</summary>
    public void OpenPdf(string path, string? password = null)
    {
        PdfPath = path; _pdfPassword = password; PdfPage = 0; _pdfHits = []; _pdfQuery = ""; _pdfMode = null;
        var info = PdfTools.Read(path, password);
        _pdfLocked = info.NeedsPassword;
        if (_pdfLocked) { PdfPages = 0; _pdfSizes = []; PdfSay(password is null ? "Diese PDF ist mit einem Passwort geschützt." : "Das Passwort stimmt nicht."); }
        else
        {
            PdfPages = PdfView.PageCount(path, password); _pdfSizes = PdfView.Sizes(path, password);
            PdfSay($"{Path.GetFileName(path)} · {PdfPages} {(PdfPages == 1 ? "Seite" : "Seiten")}{(info.Encrypted ? " · geschützt" : "")}");
        }
        _core.Audit.Append(Principal.LocalOwner.Name, "pdf.open", Path.GetFileName(path));
    }

    public void PdfGo(int page) { if (PdfPages == 0) return; PdfPage = Math.Clamp(page, 0, PdfPages - 1); }

    private async Task<string[]?> PickPdf(string field, bool multiple)
    {
        var req = new PickRequest(field, false, multiple, ["pdf"]);
        if (PickOverride is { } fake) return await fake(req);
        var got = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "PDF wählen", AllowMultiple = multiple,
            FileTypeFilter = [new FilePickerFileType("PDF") { Patterns = ["*.pdf"] }] });
        return got.Select(f => f.TryGetLocalPath()).OfType<string>().ToArray();
    }

    /// <summary>Freier Dateiname neben der Vorlage: „Name-zusatz.pdf“, bei Bedarf „Name-zusatz (2).pdf“.</summary>
    private static string Beside(string input, string suffix, string ext = ".pdf")
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(input))!; var stem = Path.GetFileNameWithoutExtension(input);
        var f = Path.Combine(dir, stem + suffix + ext);
        for (var i = 2; File.Exists(f) || Directory.Exists(f); i++) f = Path.Combine(dir, $"{stem}{suffix} ({i}){ext}");
        return f;
    }

    /// <summary>Arbeitskopie für Anmerkungen: Original bleibt unverändert.</summary>
    private string WorkingCopy()
    {
        var p = PdfPath!;
        return Path.GetFileNameWithoutExtension(p).EndsWith("(Trendo)", StringComparison.Ordinal) ? p : Beside(p, " (Trendo)");
    }

    private void ApplyEdit(Action<string, string> edit, string done)
    {
        var target = WorkingCopy();
        var tmp = Path.Combine(Path.GetTempPath(), $"trendo-pdf-{Guid.NewGuid():N}.pdf");
        try
        {
            edit(PdfPath!, tmp);
            File.Copy(tmp, target, true);
            var page = PdfPage; OpenPdf(target, _pdfPassword); PdfGo(page);
            PdfSay($"{done} – gespeichert in {Path.GetFileName(target)}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException) { PdfSay("Nicht gespeichert: " + ex.Message); }
        finally { try { File.Delete(tmp); } catch (IOException) { } }
    }

    private void PdfTool(Func<string> run)
    {
        try { PdfSay(run()); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or PdfSharp.Pdf.IO.PdfReaderException) { PdfSay("Hat nicht geklappt: " + ex.Message); }
        Show("pdf");
    }

    private int Dpi()
    {
        if (_pdfSizes.Count == 0) return 96;
        var (w, h) = _pdfSizes[PdfPage]; if (_pdfRot % 180 != 0) (w, h) = (h, w);
        var pages = _pdfDouble && PdfPage + 1 < PdfPages ? 2 : 1;
        var dpi = _pdfZoom switch { "seite" => 72 * Math.Min(600 / h, 860.0 / pages / w), "breite" => 72 * 860.0 / pages / w, _ => 72 * _pdfPercent / 100 };
        return (int)Math.Clamp(dpi, 20, 400);
    }

    /// <summary>Eigene dunkle Fläche: das Fensterglas lässt sonst den Desktop durchscheinen, Text und Werkzeuge wären schwer lesbar.</summary>
    private Control PdfPageView() => new Border { Name = "pdf-page-root", Background = Brush.Parse(C("#F2081329", "#F7F4F7FC")), CornerRadius = new CornerRadius(10), Padding = new Thickness(10), Child = PdfShell() };

    private Control PdfShell()
    {
        var g = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        g.Children.Add(PageHead("Trendo PDF"));
        var body = PdfPageBody(); Grid.SetRow(body, 1); g.Children.Add(body);
        return g;
    }

    private Control PdfPageBody()
    {
        var root = new Grid { ColumnDefinitions = new ColumnDefinitions("230,*"), RowDefinitions = new RowDefinitions("Auto,Auto,*") };
        Button B(string name, string label, Action go, string? tip = null, bool on = true)
        {
            var b = new Button { Name = name, Content = label, Margin = new Thickness(0, 0, 5, 5), Padding = new Thickness(10, 4), IsEnabled = on };
            if (tip is not null) ToolTip.SetTip(b, tip);
            b.Click += (_, _) => { go(); Show("pdf"); };
            return b;
        }
        // Kopf: Öffnen und Neu
        var head = new WrapPanel();
        var open = new Button { Name = "pdf-open", Content = "PDF öffnen …", Background = Accent, Margin = new Thickness(0, 0, 5, 5), Padding = new Thickness(12, 4) };
        open.Click += async (_, _) => { if (await PickPdf("pdf-open", false) is [var f, ..]) OpenPdf(f); Show("pdf"); };
        head.Children.Add(open);
        head.Children.Add(B("pdf-new", "Neue leere PDF", () =>
        {
            var dir = Path.Combine(ProjectsRoot, "Dokumente"); Directory.CreateDirectory(dir);
            var f = Beside(Path.Combine(dir, $"{DateTime.Now:yyyy-MM-dd}-neu.pdf"), "");
            PdfTools.NewBlank(f); OpenPdf(f);
        }));
        head.Children.Add(new TextBlock { Name = "pdf-msg", Text = _pdfMsg, Foreground = Text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 5), TextWrapping = TextWrapping.Wrap, MaxWidth = 760 });
        Grid.SetColumnSpan(head, 2); root.Children.Add(head);

        if (PdfPath is null || !File.Exists(PdfPath))
        {
            var hint = P("Öffne eine PDF oder lege eine neue an. Trendo PDF zeigt, sucht, markiert und bearbeitet PDF-Dateien ohne Zusatzprogramm.", Text, 13);
            Grid.SetRow(hint, 1); Grid.SetColumnSpan(hint, 2); root.Children.Add(hint);
            return root;
        }
        if (_pdfLocked)
        {
            var pw = new TextBox { Name = "pdf-unlock-pw", PasswordChar = '•', Width = 220, PlaceholderText = "Passwort" };
            var go = new Button { Name = "pdf-unlock", Content = "Öffnen", Background = Accent, Margin = new Thickness(8, 0, 0, 0) };
            go.Click += (_, _) => { OpenPdf(PdfPath!, pw.Text); Show("pdf"); };
            var lockRow = new StackPanel { Orientation = Orientation.Horizontal, Children = { pw, go } };
            Grid.SetRow(lockRow, 1); Grid.SetColumnSpan(lockRow, 2); root.Children.Add(lockRow);
            return root;
        }

        // Leiste: Blättern, Zoom, Ansicht (P1)
        var bar = new WrapPanel();
        bar.Children.Add(B("pdf-first", "⏮", () => PdfGo(0), "Erste Seite"));
        bar.Children.Add(B("pdf-prev", "◀", () => PdfGo(PdfPage - (_pdfDouble ? 2 : 1)), "Vorherige Seite"));
        var pageBox = new TextBox { Name = "pdf-page", Text = (PdfPage + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), Width = 56, Margin = new Thickness(0, 0, 4, 5) };
        pageBox.KeyDown += (_, e) => { if (e.Key == Key.Enter && int.TryParse(pageBox.Text, out var n)) { PdfGo(n - 1); Show("pdf"); } };
        bar.Children.Add(pageBox);
        bar.Children.Add(new TextBlock { Name = "pdf-pages-label", Text = $"von {PdfPages}", Foreground = Text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 5) });
        bar.Children.Add(B("pdf-next", "▶", () => PdfGo(PdfPage + (_pdfDouble ? 2 : 1)), "Nächste Seite"));
        bar.Children.Add(B("pdf-last", "⏭", () => PdfGo(PdfPages - 1), "Letzte Seite"));
        bar.Children.Add(B("pdf-fit-page", "Ganze Seite", () => _pdfZoom = "seite"));
        bar.Children.Add(B("pdf-fit-width", "Breite", () => _pdfZoom = "breite"));
        bar.Children.Add(B("pdf-100", "100 %", () => { _pdfZoom = "prozent"; _pdfPercent = 100; }));
        bar.Children.Add(B("pdf-zoom-out", "−", () => { _pdfZoom = "prozent"; _pdfPercent = Math.Max(25, (_pdfZoom == "prozent" ? _pdfPercent : 100) - 25); }, "Verkleinern"));
        bar.Children.Add(B("pdf-zoom-in", "+", () => { _pdfZoom = "prozent"; _pdfPercent = Math.Min(400, _pdfPercent + 25); }, "Vergrößern"));
        bar.Children.Add(B("pdf-double", _pdfDouble ? "Einzelseite" : "Doppelseite", () => _pdfDouble = !_pdfDouble));
        bar.Children.Add(B("pdf-rotate-view", "Ansicht drehen ⟳", () => _pdfRot = (_pdfRot + 90) % 360));
        bar.Children.Add(B("pdf-fullscreen", WindowState == WindowState.FullScreen ? "Vollbild aus" : "Vollbild", () => WindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen));
        Grid.SetRow(bar, 1); Grid.SetColumnSpan(bar, 2); root.Children.Add(bar);

        // Seitenleiste: Suche, Lesezeichen, Markieren, Werkzeuge, Formular
        var side = new StackPanel { Spacing = 6, Margin = new Thickness(0, 0, 10, 0) };
        var search = new TextBox { Name = "pdf-search", Text = _pdfQuery, PlaceholderText = "Im PDF suchen …" };
        search.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            _pdfQuery = search.Text ?? "";
            _pdfHits = PdfView.Search(PdfPath!, _pdfQuery, _pdfPassword);
            PdfSay(_pdfHits.Count == 0 ? $"„{_pdfQuery}“ nicht gefunden." : $"„{_pdfQuery}“: {_pdfHits.Count} Treffer");
            if (_pdfHits.Count > 0) PdfGo(_pdfHits[0].Page);
            Show("pdf");
        };
        side.Children.Add(search);
        var hits = new StackPanel { Spacing = 2 };
        for (var i = 0; i < Math.Min(_pdfHits.Count, 40); i++)
        {
            var h = _pdfHits[i];
            hits.Children.Add(B("pdf-hit-" + i, $"S. {h.Page + 1}: {h.Snippet}", () => PdfGo(h.Page)));
        }
        if (_pdfHits.Count > 0) side.Children.Add(new ScrollViewer { MaxHeight = 160, Content = hits });
        var marks = PdfView.Bookmarks(PdfPath!, _pdfPassword);
        if (marks.Count > 0)
        {
            var bm = new StackPanel { Spacing = 1 };
            for (var i = 0; i < Math.Min(marks.Count, 200); i++)
            {
                var m = marks[i];
                var b = B("pdf-bm-" + i, m.Title, () => { if (m.Page >= 0) PdfGo(m.Page); }); b.Margin = new Thickness(m.Level * 12, 0, 0, 2); b.Background = Brushes.Transparent;
                bm.Children.Add(b);
            }
            side.Children.Add(new Expander { Header = $"Lesezeichen ({marks.Count})", IsExpanded = true, Content = new ScrollViewer { MaxHeight = 180, Content = bm } });
        }
        side.Children.Add(B("pdf-copy-text", "Text der Seite kopieren", () =>
        {
            var t = PdfView.Text(PdfPath!, PdfPage, _pdfPassword);
            if (Clipboard is { } cb) _ = cb.SetTextAsync(t);
            PdfSay(t.Length == 0 ? "Diese Seite hat keinen Text (vielleicht ein Scan – dann hilft „Text erkennen“ im Briefstudio)." : $"{t.Length} Zeichen kopiert.");
        }));

        // P5 Markieren und Notizen
        var noteText = new TextBox { Name = "pdf-note-text", PlaceholderText = "Text für Notiz", AcceptsReturn = true, Height = 54, TextWrapping = TextWrapping.Wrap };
        var markRow = new WrapPanel();
        foreach (var (mode, label) in new[] { ("Highlight", "Markieren"), ("Underline", "Unterstreichen"), ("StrikeOut", "Durchstreichen"), ("Note", "Notiz setzen") })
        {
            var on = _pdfMode == mode;
            var b = B("pdf-mark-" + mode.ToLowerInvariant(), label, () => _pdfMode = on ? null : mode);
            if (on) b.Background = Accent;
            markRow.Children.Add(b);
        }
        side.Children.Add(new Expander { Header = "Markieren & Notizen", IsExpanded = _pdfMode is not null, Content = new StackPanel { Spacing = 4, Children = {
            markRow, noteText, P(_pdfMode is null ? "Art wählen, dann auf der Seite ziehen (Notiz: klicken). Gespeichert wird in einer Kopie." : _pdfRot != 0 ? "Zum Markieren die Ansicht wieder gerade drehen." : "Jetzt auf der Seite ziehen bzw. klicken.", null, 11) } } });

        // P3/P4 Werkzeuge
        var tools = new StackPanel { Spacing = 4 };
        var merge = new Button { Name = "pdf-merge", Content = "Mit weiteren PDFs zusammenfügen …", Margin = new Thickness(0, 0, 0, 4) };
        merge.Click += async (_, _) =>
        {
            if (await PickPdf("pdf-merge", true) is not { Length: > 0 } more) return;
            PdfTool(() => { var o = Beside(PdfPath!, "-zusammen"); var n = PdfTools.Merge([PdfPath!, .. more], o, _core.Audit); return $"Zusammengefügt: {Path.GetFileName(o)} ({n} Seiten)"; });
        };
        tools.Children.Add(merge);
        var spec = new TextBox { Name = "pdf-select-spec", PlaceholderText = "Seiten, z. B. 1-3,5,8-z", Width = 150 };
        var selGo = new Button { Name = "pdf-select-go", Content = "Als neue PDF" };
        selGo.Click += (_, _) => PdfTool(() => { var o = Beside(PdfPath!, "-seiten"); var n = PdfTools.SelectPages(PdfPath!, spec.Text ?? "", o, _core.Audit); return $"{n} Seiten gespeichert: {Path.GetFileName(o)}"; });
        tools.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { spec, selGo } });
        var every = new TextBox { Name = "pdf-split-n", Text = "1", Width = 50 };
        var split = new Button { Name = "pdf-split-go", Content = "Teilen (Seiten je Datei)" };
        split.Click += (_, _) => PdfTool(() => { var dir = Beside(PdfPath!, "-teile", ""); var n = int.TryParse(every.Text, out var x) ? x : 1; var files = PdfTools.Split(PdfPath!, dir, n, _core.Audit); return $"{files.Count} Dateien im Ordner {Path.GetFileName(dir)}"; });
        tools.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { every, split } });
        var rotSpec = new TextBox { Name = "pdf-rot-spec", PlaceholderText = "Seiten (leer = alle)", Width = 120 };
        var rotRow = new WrapPanel { Children = { rotSpec } };
        foreach (var deg in new[] { 90, 180, 270 })
        {
            var b = new Button { Name = "pdf-rot-" + deg, Content = deg + "°", Margin = new Thickness(4, 0, 0, 0) };
            b.Click += (_, _) => PdfTool(() => { var o = Beside(PdfPath!, "-gedreht"); PdfTools.Rotate(PdfPath!, o, deg, string.IsNullOrWhiteSpace(rotSpec.Text) ? null : rotSpec.Text, _core.Audit); return $"Gedreht gespeichert: {Path.GetFileName(o)}"; });
            rotRow.Children.Add(b);
        }
        tools.Children.Add(rotRow);
        var pwNew = new TextBox { Name = "pdf-pw-new", PasswordChar = '•', PlaceholderText = "Neues Passwort", Width = 150 };
        var enc = new Button { Name = "pdf-encrypt", Content = "Schützen" };
        enc.Click += (_, _) => PdfTool(() => { var o = Beside(PdfPath!, "-geschuetzt"); PdfTools.Encrypt(PdfPath!, o, pwNew.Text ?? "", null, _pdfPassword, _core.Audit); return $"Mit Passwort gespeichert: {Path.GetFileName(o)}"; });
        tools.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { pwNew, enc } });
        if (PdfTools.Read(PdfPath!, _pdfPassword).Encrypted)
        {
            var dec = new Button { Name = "pdf-decrypt", Content = "Passwortschutz entfernen" };
            dec.Click += (_, _) => PdfTool(() => { var o = Beside(PdfPath!, "-offen"); PdfTools.Decrypt(PdfPath!, o, _pdfPassword ?? "", _core.Audit); return $"Ohne Passwort gespeichert: {Path.GetFileName(o)}"; });
            tools.Children.Add(dec);
        }
        var info = PdfTools.Read(PdfPath!, _pdfPassword);
        tools.Children.Add(new TextBlock { Name = "pdf-info", Text = $"{info.Pages} Seiten · {(info.Encrypted ? "mit Passwort geschützt" : "nicht geschützt")}{(info.Title is { } t ? " · " + t : "")}", Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap });
        side.Children.Add(new Expander { Header = "Werkzeuge", IsExpanded = false, Content = tools });

        // P6 Formular
        List<PdfTools.Field> fields;
        try { fields = PdfTools.Fields(PdfPath!); } catch (Exception ex) when (ex is InvalidOperationException or PdfSharp.Pdf.IO.PdfReaderException) { fields = []; }
        if (fields.Count > 0)
        {
            var form = new StackPanel { Spacing = 3 };
            var inputs = new Dictionary<string, Func<string>>();
            foreach (var f in fields.Take(80))
            {
                if (f.Kind == "Ankreuzfeld")
                {
                    var cb = new CheckBox { Name = "pdf-field-" + f.Name, Content = f.Name, IsChecked = f.Value == "ja", Foreground = Text };
                    inputs[f.Name] = () => cb.IsChecked == true ? "ja" : "nein"; form.Children.Add(cb);
                }
                else
                {
                    var tb = new TextBox { Name = "pdf-field-" + f.Name, Text = f.Value, PlaceholderText = f.Name };
                    inputs[f.Name] = () => tb.Text ?? ""; form.Children.Add(new TextBlock { Text = f.Name, Foreground = Muted, FontSize = 11 }); form.Children.Add(tb);
                }
            }
            var fill = new Button { Name = "pdf-fill-go", Content = "Ausgefüllt speichern", Background = Accent };
            fill.Click += (_, _) => PdfTool(() =>
            {
                var o = Beside(PdfPath!, "-ausgefuellt");
                var missing = PdfTools.Fill(PdfPath!, o, inputs.ToDictionary(k => k.Key, k => k.Value()), _core.Audit);
                OpenPdf(o);
                return $"Formular gespeichert: {Path.GetFileName(o)}" + (missing.Count > 0 ? $" ({missing.Count} Felder unbekannt)" : "");
            });
            form.Children.Add(fill);
            side.Children.Add(new Expander { Header = $"Formular ({fields.Count} Felder)", IsExpanded = true, Content = form });
        }
        var sideScroll = new ScrollViewer { Content = side, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(sideScroll, 2); root.Children.Add(sideScroll);

        // Seite(n) zeichnen
        var dpi = Dpi();
        var pagesRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Center };
        for (var k = 0; k < (_pdfDouble ? 2 : 1) && PdfPage + k < PdfPages; k++)
        {
            var index = PdfPage + k;
            Avalonia.Media.Imaging.Bitmap bmp; int pw, ph;
            try { bmp = PdfView.Render(PdfPath!, index, dpi, _pdfRot, _pdfPassword, out pw, out ph); }
            catch (Exception ex) when (ex is InvalidOperationException or IOException or DllNotFoundException) { pagesRow.Children.Add(P("Seite lässt sich nicht zeichnen: " + ex.Message, Text)); continue; }
            var img = new Image { Name = "pdf-image-" + k, Source = bmp, Width = pw, Height = ph, Stretch = Stretch.Fill }; // 1 Bildpunkt = 1 Einheit, passt zur Umrechnung in PDF-Punkte
            var canvas = new Canvas { Width = pw, Height = ph, Background = Brushes.Transparent, Cursor = _pdfMode is null ? Cursor.Default : new Cursor(StandardCursorType.Cross) };
            var rect = new Rectangle { Stroke = Accent, StrokeThickness = 1.5, Fill = Brush.Parse("#332F86F6"), IsVisible = false };
            canvas.Children.Add(rect);
            Point? start = null;
            canvas.PointerPressed += (_, e) => { if (_pdfMode is null || _pdfRot != 0) return; start = e.GetPosition(canvas); Canvas.SetLeft(rect, start.Value.X); Canvas.SetTop(rect, start.Value.Y); rect.Width = rect.Height = 0; rect.IsVisible = _pdfMode != "Note"; };
            canvas.PointerMoved += (_, e) =>
            {
                if (start is not { } s0 || _pdfMode == "Note") return;
                var p = e.GetPosition(canvas);
                Canvas.SetLeft(rect, Math.Min(s0.X, p.X)); Canvas.SetTop(rect, Math.Min(s0.Y, p.Y)); rect.Width = Math.Abs(p.X - s0.X); rect.Height = Math.Abs(p.Y - s0.Y);
            };
            canvas.PointerReleased += (_, e) =>
            {
                if (start is not { } s0) return; start = null;
                var p = e.GetPosition(canvas);
                PdfMarkAt(index, s0, p, dpi, noteText.Text ?? "");
                Show("pdf");
            };
            pagesRow.Children.Add(new Border { BoxShadow = BoxShadows.Parse("0 4 18 0 #66000000"), Child = new Grid { Children = { img, canvas } } });
        }
        var view = new ScrollViewer { Name = "pdf-view", Content = pagesRow, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(view, 2); Grid.SetColumn(view, 1); root.Children.Add(view);
        return root;
    }

    /// <summary>Bildpunkte der gezeichneten Seite in PDF-Punkte umrechnen und Anmerkung setzen (auch für Tests).</summary>
    public void PdfMarkAt(int pageIndex, Point a, Point b, int dpi, string note)
    {
        if (_pdfMode is null || PdfPath is null) return;
        var k = 72.0 / dpi; var hPt = _pdfSizes[pageIndex].H;
        var x = Math.Min(a.X, b.X) * k; var w = Math.Abs(a.X - b.X) * k;
        var yTop = Math.Min(a.Y, b.Y) * k; var h = Math.Abs(a.Y - b.Y) * k;
        var y = hPt - yTop - h;
        if (_pdfMode == "Note")
        {
            if (string.IsNullOrWhiteSpace(note)) { PdfSay("Bitte zuerst den Text für die Notiz eingeben."); return; }
            ApplyEdit((i, o) => PdfTools.AddNote(i, o, pageIndex, a.X * k, hPt - a.Y * k, note, _core.Audit), "Notiz gesetzt");
            return;
        }
        if (w < 2 || h < 2) { PdfSay("Bitte über den Text ziehen."); return; }
        var kind = Enum.Parse<PdfTools.Mark>(_pdfMode);
        ApplyEdit((i, o) => PdfTools.AddMark(i, o, pageIndex, kind, x, y, w, h, string.IsNullOrWhiteSpace(note) ? null : note, _core.Audit), kind switch { PdfTools.Mark.Highlight => "Markiert", PdfTools.Mark.Underline => "Unterstrichen", _ => "Durchgestrichen" });
    }

    /// <summary>Für Tests: Markier-Art wählen wie über die Knöpfe.</summary>
    public void PdfSetMode(string? mode) => _pdfMode = mode;
}
