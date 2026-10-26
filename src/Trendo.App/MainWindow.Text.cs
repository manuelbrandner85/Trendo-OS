using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Trendo.Core;
using Trendo.Core.Office;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>T-0223 Trendo Text (Stufe 1, T1–T10): eigenes Schreibprogramm statt LibreOffice Writer. Vorn das Häufigste, unter „Mehr“ alles Weitere.
/// Absatzweise bearbeiten: der Absatz mit dem Cursor ist ein Eingabefeld, alle anderen zeigen ihr Format. Formate bleiben beim Tippen erhalten.</summary>
public partial class MainWindow
{
    public TextDoc TextDocument { get; private set; } = TextDoc.Empty();
    /// <summary>Für Tests: Projektordner (isoliert im Datenordner).</summary>
    public string ProjectsRootForTests => ProjectsRoot;
    public string? TextPath { get; private set; }
    public bool TextDirty { get; private set; }
    public int TextFocusBlock { get; private set; }
    private int _txSel, _txSelLen;
    private string _txMsg = "";
    private bool _txMore, _txFind, _txSpellOn;
    private readonly List<TextDoc> _txUndo = [], _txRedo = [];
    private List<Spelling.Mistake> _txMistakes = [];
    private Spelling? _spelling; private bool _spellTried;
    private List<TextDoc.Hit> _txHits = []; private int _txHit = -1;
    private TextBox? _txActive;

    private void InitText()
    {
        _showMap["text"] = TextPageView;
        PageModules["text"] = "SYS-10";
        ExtraPages.Add(("Trendo Text", "text", ["text", "schreiben", "word", "docx", "odt", "lebenslauf", "rechnung", "rechtschreibung"]));
    }

    // ---------- Öffentliche Wege (auch für Tests und die KI) ----------
    public void TextNew(string template = "leer")
    {
        TextDocument = TextTemplates.Create(template); TextPath = null; TextDirty = false; TextFocusBlock = 0; _txSel = _txSelLen = 0;
        _txUndo.Clear(); _txRedo.Clear(); _txMistakes = []; _txHits = [];
        _txMsg = template == "leer" ? "Neues Dokument." : $"Vorlage „{TextTemplates.Builtin.First(t => t.Id == template).Name}“ – Platzhalter in [Klammern] einfach überschreiben.";
    }

    public void TextOpen(string path)
    {
        TextDocument = TextFiles.Open(path); TextPath = Path.GetExtension(path).Equals(".txt", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path).Equals(".odt", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path).Equals(".docx", StringComparison.OrdinalIgnoreCase) ? path : null;
        TextDirty = false; TextFocusBlock = 0; _txSel = _txSelLen = 0; _txUndo.Clear(); _txRedo.Clear(); _txMistakes = []; _txHits = [];
        _txMsg = $"Geöffnet: {Path.GetFileName(path)}";
        RememberRecent(path);
        _core.Audit.Append(Principal.LocalOwner.Name, "text.open", Path.GetFileName(path));
    }

    public void TextSaveAs(string path)
    {
        TextFiles.Save(TextDocument, path);
        if (!path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) { TextPath = path; TextDirty = false; RememberRecent(path); }
        _txMsg = $"Gespeichert: {Path.GetFileName(path)}";
        _core.Audit.Append(Principal.LocalOwner.Name, "text.save", Path.GetFileName(path));
    }

    /// <summary>Cursor/Auswahl setzen (wie Klicken und Markieren).</summary>
    public void TextSelect(int block, int start, int length = 0)
    {
        var same = TextFocusBlock == block;
        TextFocusBlock = Math.Clamp(block, 0, TextDocument.Blocks.Count - 1); _txSel = start; _txSelLen = length;
        // im offenen Eingabefeld sofort markieren (wie mit der Maus) – sonst überschreibt es die Auswahl beim nächsten Zeichnen
        if (same && _txActive is { } tb && tb.IsAttachedToVisualTree())
        {
            var s = Math.Clamp(start, 0, tb.Text?.Length ?? 0); var e = Math.Clamp(start + length, s, tb.Text?.Length ?? 0);
            tb.SelectionStart = s; tb.SelectionEnd = e; _txSel = start; _txSelLen = length;
        }
    }

    /// <summary>Zeichenformat auf die Auswahl umschalten (Knöpfe F, K, U …).</summary>
    public void TextToggle(Fmt f) { if (CurPara() is not { } p) return; Snapshot(); TextDoc.Toggle(p, _txSel, _txSelLen, f); Changed(); }

    private Para? CurPara() => TextFocusBlock < TextDocument.Blocks.Count ? TextDocument.Blocks[TextFocusBlock] as Para : null;

    private void Snapshot()
    {
        _txUndo.Add(TextDocument.Copy()); if (_txUndo.Count > 80) _txUndo.RemoveAt(0);
        _txRedo.Clear();
    }

    private void Changed() { TextDirty = true; if (_txSpellOn) RunSpelling(); }

    public void TextUndo() { if (_txUndo.Count == 0) return; _txRedo.Add(TextDocument); TextDocument = _txUndo[^1]; _txUndo.RemoveAt(_txUndo.Count - 1); TextFocusBlock = Math.Min(TextFocusBlock, TextDocument.Blocks.Count - 1); TextDirty = true; }
    public void TextRedo() { if (_txRedo.Count == 0) return; _txUndo.Add(TextDocument); TextDocument = _txRedo[^1]; _txRedo.RemoveAt(_txRedo.Count - 1); TextFocusBlock = Math.Min(TextFocusBlock, TextDocument.Blocks.Count - 1); TextDirty = true; }

    private string TextRecentFile => Path.Combine(_core.DataRoot, "text", "zuletzt.txt");
    private List<string> TextRecent() => File.Exists(TextRecentFile) ? File.ReadAllLines(TextRecentFile).Where(File.Exists).Take(10).ToList() : [];
    private void RememberRecent(string path)
    {
        var list = TextRecent().Where(p => !p.Equals(path, StringComparison.OrdinalIgnoreCase)).Prepend(Path.GetFullPath(path)).Take(10).ToList();
        Directory.CreateDirectory(Path.GetDirectoryName(TextRecentFile)!); File.WriteAllLines(TextRecentFile, list);
    }

    private void RunSpelling()
    {
        if (!_spellTried) { _spellTried = true; _spelling = Spelling.Load(Spelling.Candidates(_deps.ToolsRoot), Path.Combine(_core.DataRoot, "text", "eigene-woerter.txt")); }
        _txMistakes = _spelling?.Check(TextDocument) ?? [];
    }

    private async Task<string?> PickText(bool save, string suggested)
    {
        var req = new PickRequest(save ? "text-save" : "text-open", false, false, [.. TextFiles.Extensions]);
        if (PickOverride is { } fake) return (await fake(req)) is [var f, ..] ? f : null;
        if (save)
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Speichern unter", SuggestedFileName = suggested, DefaultExtension = "docx",
                FileTypeChoices = [new FilePickerFileType("Word-Dokument") { Patterns = ["*.docx"] }, new FilePickerFileType("LibreOffice-Dokument") { Patterns = ["*.odt"] }, new FilePickerFileType("Reiner Text") { Patterns = ["*.txt"] }, new FilePickerFileType("PDF") { Patterns = ["*.pdf"] }] });
            return file?.TryGetLocalPath();
        }
        var got = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Dokument öffnen", FileTypeFilter = [new FilePickerFileType("Dokumente") { Patterns = ["*.docx", "*.odt", "*.txt"] }] });
        return got.Select(f => f.TryGetLocalPath()).OfType<string>().FirstOrDefault();
    }

    private string DefaultTextName(string ext)
    {
        var title = TextDocument.Paras.FirstOrDefault(p => p.Text.Trim().Length > 0)?.Text.Trim() ?? "Dokument";
        var safe = string.Concat(title.Take(40).Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c)).Trim();
        var dir = Path.Combine(ProjectsRoot, "Dokumente"); Directory.CreateDirectory(dir);
        var f = Path.Combine(dir, safe + ext);
        for (var i = 2; File.Exists(f); i++) f = Path.Combine(dir, $"{safe} ({i}){ext}");
        return f;
    }

    // ---------- Oberfläche ----------
    private Control TextPageView() => new Border { Name = "text-page-root", Background = Brush.Parse(C("#F2081329", "#F7F4F7FC")), CornerRadius = new CornerRadius(10), Padding = new Thickness(10), Child = TextShell() };

    private Control TextShell()
    {
        var g = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*,Auto"), ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var head = PageHead("Trendo Text"); Grid.SetColumnSpan(head, 2); g.Children.Add(head);
        var tools = TextToolbar(); Grid.SetRow(tools, 1); Grid.SetColumnSpan(tools, 2); g.Children.Add(tools);
        var more = TextMoreBar(); Grid.SetRow(more, 2); Grid.SetColumnSpan(more, 2); g.Children.Add(more);
        var page = TextPaper(); Grid.SetRow(page, 3); g.Children.Add(page);
        var side = TextSidePanel(); if (side is not null) { Grid.SetRow(side, 3); Grid.SetColumn(side, 1); g.Children.Add(side); }
        var c = TextDocument.Count();
        var status = new TextBlock { Name = "text-status", Foreground = Muted, FontSize = 11.5, Margin = new Thickness(4, 6, 0, 0), TextWrapping = TextWrapping.Wrap,
            Text = $"{(TextPath is null ? "Noch nicht gespeichert" : Path.GetFileName(TextPath))}{(TextDirty ? " · geändert" : "")} · {c.Words} Wörter · {c.Characters} Zeichen · {c.Paragraphs} Absätze{(_txMsg.Length > 0 ? " · " + _txMsg : "")}" };
        Grid.SetRow(status, 4); Grid.SetColumnSpan(status, 2); g.Children.Add(status);
        return g;
    }

    /// <summary>Knopf, der dem Eingabefeld den Fokus nicht wegnimmt – so bleibt die Auswahl für Formate erhalten.</summary>
    private Button TB(string name, string label, Action go, string tip, bool on = false, bool enabled = true)
    {
        var b = new Button { Name = name, Content = label, Focusable = false, Margin = new Thickness(0, 0, 4, 4), Padding = new Thickness(9, 4), MinWidth = 30, IsEnabled = enabled, HorizontalContentAlignment = HorizontalAlignment.Center };
        if (on) b.Background = Accent;
        ToolTip.SetTip(b, tip); Avalonia.Automation.AutomationProperties.SetName(b, tip);
        b.Click += (_, _) => { go(); Show("text"); };
        return b;
    }

    private Control TextToolbar()
    {
        var bar = new WrapPanel();
        var p = CurPara();
        var newBtn = new Button { Name = "text-new", Content = "Neu ▾", Focusable = false, Margin = new Thickness(0, 0, 4, 4), Padding = new Thickness(9, 4) };
        ToolTip.SetTip(newBtn, "Neues Dokument oder Vorlage");
        var menu = new ContextMenu();
        foreach (var (id, name, hint) in TextTemplates.Builtin)
        {
            var mi = new MenuItem { Header = $"{name} – {hint}", Name = "text-new-" + id };
            mi.Click += (_, _) => { TextNew(id); Show("text"); };
            menu.Items.Add(mi);
        }
        foreach (var (file, label) in new[] { ("DIN5008-Privatbrief-FormB.docx", "Privatbrief (DIN 5008)"), ("DIN5008-Geschaeftsbrief-FormB.docx", "Geschäftsbrief (DIN 5008)") })
        {
            var mi = new MenuItem { Header = label, Name = "text-new-" + (file.Contains("Privat") ? "privatbrief" : "geschaeftsbrief") };
            mi.Click += (_, _) =>
            {
                var tmp = Path.Combine(Path.GetTempPath(), $"trendo-vorlage-{Guid.NewGuid():N}.docx");
                using (var s = Avalonia.Platform.AssetLoader.Open(new Uri($"avares://TrendoOS/Assets/templates/{file}"))) using (var f = File.Create(tmp)) s.CopyTo(f);
                TextDocument = TextFiles.Open(tmp); File.Delete(tmp); TextPath = null; TextDirty = false; TextFocusBlock = 0; _txUndo.Clear(); _txMsg = $"Vorlage „{label}“."; Show("text");
            };
            menu.Items.Add(mi);
        }
        newBtn.ContextMenu = menu; newBtn.Click += (_, _) => menu.Open(newBtn);
        bar.Children.Add(newBtn);
        var open = new Button { Name = "text-open", Content = "Öffnen …", Focusable = false, Margin = new Thickness(0, 0, 4, 4), Padding = new Thickness(9, 4) };
        open.Click += async (_, _) => { if (await PickText(false, "") is { } f) { try { TextOpen(f); } catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or System.Xml.XmlException) { _txMsg = "Öffnen ging nicht: " + ex.Message; } } Show("text"); };
        bar.Children.Add(open);
        var save = new Button { Name = "text-save", Content = "Speichern", Focusable = false, Background = Accent, Margin = new Thickness(0, 0, 4, 4), Padding = new Thickness(9, 4) };
        save.Click += (_, _) => { try { TextSaveAs(TextPath ?? DefaultTextName(".docx")); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _txMsg = "Nicht gespeichert: " + ex.Message; } Show("text"); };
        bar.Children.Add(save);
        bar.Children.Add(TB("text-pdf", "Als PDF", () => { try { var f = DefaultTextName(".pdf"); var n = TextPdf.Export(TextDocument, f); _txMsg = $"PDF gespeichert: {Path.GetFileName(f)} ({n} Seiten)"; _core.Audit.Append(Principal.LocalOwner.Name, "text.pdf", Path.GetFileName(f)); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) { _txMsg = "PDF ging nicht: " + ex.Message; } }, "Als PDF speichern"));
        bar.Children.Add(TB("text-print", "Drucken", TextPrint, "Drucken (über PDF)"));
        bar.Children.Add(new Border { Width = 10 });
        bar.Children.Add(TB("text-undo", "↶", TextUndo, "Rückgängig (Strg+Z)", enabled: _txUndo.Count > 0));
        bar.Children.Add(TB("text-redo", "↷", TextRedo, "Wiederholen (Strg+Y)", enabled: _txRedo.Count > 0));
        bar.Children.Add(new Border { Width = 10 });
        var style = new ComboBox { Name = "text-style", Focusable = false, Width = 140, Margin = new Thickness(0, 0, 4, 4), IsEnabled = p is not null };
        foreach (var s in Enum.GetValues<ParaStyle>()) style.Items.Add(TextDoc.StyleName(s));
        style.SelectedIndex = p is null ? 0 : (int)p.Style;
        style.SelectionChanged += (_, _) => { if (CurPara() is { } cp && style.SelectedIndex >= 0 && (int)cp.Style != style.SelectedIndex) { Snapshot(); cp.Style = (ParaStyle)style.SelectedIndex; Changed(); Show("text"); } };
        ToolTip.SetTip(style, "Absatzart: Titel, Überschriften, Text, Zitat");
        bar.Children.Add(style);
        bool Has(Fmt f) => p is not null && TextDoc.Has(p, _txSel, _txSelLen, f);
        bar.Children.Add(TB("text-bold", "F", () => TextToggle(Fmt.Bold), "Fett (Strg+B)", Has(Fmt.Bold)));
        bar.Children.Add(TB("text-italic", "K", () => TextToggle(Fmt.Italic), "Kursiv (Strg+I)", Has(Fmt.Italic)));
        bar.Children.Add(TB("text-underline", "U", () => TextToggle(Fmt.Underline), "Unterstrichen (Strg+U)", Has(Fmt.Underline)));
        bar.Children.Add(TB("text-bullets", "• Liste", () => SetList(ListKind.Bullet), "Aufzählung", p?.List == ListKind.Bullet));
        bar.Children.Add(TB("text-numbers", "1. Liste", () => SetList(ListKind.Number), "Nummerierung", p?.List == ListKind.Number));
        foreach (var (a, label, tip) in new[] { (Align.Left, "⯇", "Linksbündig"), (Align.Center, "≡", "Zentriert"), (Align.Right, "⯈", "Rechtsbündig"), (Align.Justify, "☰", "Blocksatz") })
            bar.Children.Add(TB("text-align-" + a.ToString().ToLowerInvariant(), label, () => { if (CurPara() is { } cp) { Snapshot(); cp.Align = a; Changed(); } }, tip, p?.Align == a));
        bar.Children.Add(new Border { Width = 10 });
        bar.Children.Add(TB("text-find", "Suchen", () => _txFind = !_txFind, "Suchen und Ersetzen (Strg+F)", _txFind));
        bar.Children.Add(TB("text-spell", "Rechtschreibung", () => { _txSpellOn = !_txSpellOn; if (_txSpellOn) RunSpelling(); else _txMistakes = []; }, "Rechtschreibung prüfen", _txSpellOn));
        bar.Children.Add(TB("text-more", _txMore ? "Weniger" : "Mehr …", () => _txMore = !_txMore, "Alle weiteren Funktionen"));
        return bar;
    }

    private void SetList(ListKind k) { if (CurPara() is not { } p) return; Snapshot(); p.List = p.List == k ? ListKind.None : k; Changed(); }

    private Control TextMoreBar()
    {
        var bar = new WrapPanel { IsVisible = _txMore, Name = "text-more-bar" };
        var p = CurPara();
        var saveAs = new Button { Name = "text-saveas", Content = "Speichern unter …", Focusable = false, Margin = new Thickness(0, 0, 4, 4), Padding = new Thickness(9, 4) };
        saveAs.Click += async (_, _) => { if (await PickText(true, Path.GetFileName(TextPath ?? DefaultTextName(".docx"))) is { } f) { try { TextSaveAs(f); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { _txMsg = "Nicht gespeichert: " + ex.Message; } } Show("text"); };
        bar.Children.Add(saveAs);
        var recent = TextRecent();
        if (recent.Count > 0)
        {
            var rb = new Button { Name = "text-recent", Content = "Zuletzt ▾", Focusable = false, Margin = new Thickness(0, 0, 4, 4), Padding = new Thickness(9, 4) };
            var m = new ContextMenu();
            for (var i = 0; i < recent.Count; i++) { var f = recent[i]; var mi = new MenuItem { Header = Path.GetFileName(f), Name = "text-recent-" + i }; mi.Click += (_, _) => { TextOpen(f); Show("text"); }; m.Items.Add(mi); }
            rb.ContextMenu = m; rb.Click += (_, _) => m.Open(rb); bar.Children.Add(rb);
        }
        bar.Children.Add(TB("text-cut", "Ausschneiden", () => _txActive?.Cut(), "Ausschneiden (Strg+X)"));
        bar.Children.Add(TB("text-copy", "Kopieren", () => _txActive?.Copy(), "Kopieren (Strg+C)"));
        bar.Children.Add(TB("text-paste", "Einfügen", () => _txActive?.Paste(), "Einfügen ohne Formatierung (Strg+V)"));
        bar.Children.Add(TB("text-selectall", "Alles markieren", () => { if (CurPara() is { } cp) TextSelect(TextFocusBlock, 0, cp.Text.Length); }, "Ganzen Absatz markieren"));
        bool Has(Fmt f) => p is not null && TextDoc.Has(p, _txSel, _txSelLen, f);
        bar.Children.Add(TB("text-strike", "Durchgestrichen", () => TextToggle(Fmt.Strike), "Durchgestrichen", Has(Fmt.Strike)));
        bar.Children.Add(TB("text-super", "x²", () => TextToggle(Fmt.Super), "Hochgestellt", Has(Fmt.Super)));
        bar.Children.Add(TB("text-sub", "x₂", () => TextToggle(Fmt.Sub), "Tiefgestellt", Has(Fmt.Sub)));
        bar.Children.Add(TB("text-grow", "A+", () => { if (CurPara() is { } cp) { Snapshot(); TextDoc.Grow(cp, _txSel, Math.Max(_txSelLen, 0), true); Changed(); } }, "Schrift größer"));
        bar.Children.Add(TB("text-shrink", "A−", () => { if (CurPara() is { } cp) { Snapshot(); TextDoc.Grow(cp, _txSel, Math.Max(_txSelLen, 0), false); Changed(); } }, "Schrift kleiner"));
        bar.Children.Add(TB("text-upper", "GROSS", () => { if (CurPara() is { } cp) { Snapshot(); TextDoc.ChangeCase(cp, _txSel, _txSelLen, TextDoc.Case.Upper); Changed(); } }, "In Großbuchstaben"));
        bar.Children.Add(TB("text-lower", "klein", () => { if (CurPara() is { } cp) { Snapshot(); TextDoc.ChangeCase(cp, _txSel, _txSelLen, TextDoc.Case.Lower); Changed(); } }, "In Kleinbuchstaben"));
        var rows = new NumericUpDown { Name = "text-table-rows", Value = 3, Minimum = 1, Maximum = 50, Width = 90, Focusable = true, Margin = new Thickness(6, 0, 2, 4) };
        var cols = new NumericUpDown { Name = "text-table-cols", Value = 3, Minimum = 1, Maximum = 12, Width = 90, Margin = new Thickness(0, 0, 2, 4) };
        bar.Children.Add(rows); bar.Children.Add(new TextBlock { Text = "×", Foreground = Text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 2, 4) }); bar.Children.Add(cols);
        bar.Children.Add(TB("text-table", "Tabelle einfügen", () => InsertBlock(new Table((int)(rows.Value ?? 3), (int)(cols.Value ?? 3))), "Tabelle einfügen"));
        var img = new Button { Name = "text-image", Content = "Bild einfügen …", Focusable = false, Margin = new Thickness(0, 0, 4, 4), Padding = new Thickness(9, 4) };
        img.Click += async (_, _) =>
        {
            var req = new PickRequest("text-image", false, false, ["png", "jpg", "jpeg", "gif"]);
            var f = PickOverride is { } fake ? (await fake(req))?.FirstOrDefault() : (await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Bild wählen", FileTypeFilter = [new FilePickerFileType("Bilder") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.gif"] }] })).Select(x => x.TryGetLocalPath()).OfType<string>().FirstOrDefault();
            if (f is not null) TextInsertImage(f);
            Show("text");
        };
        bar.Children.Add(img);
        bar.Children.Add(TB("text-pagebreak", "Seitenumbruch", () => InsertBlock(new PageBreak()), "Neue Seite beginnen"));
        var c = TextDocument.Count();
        bar.Children.Add(TB("text-count", "Wörter zählen", () => _txMsg = $"{c.Words} Wörter, {c.Characters} Zeichen ({c.CharactersNoSpaces} ohne Leerzeichen), {c.Paragraphs} Absätze", "Wörter und Zeichen zählen"));
        return bar;
    }

    public void TextInsertImage(string file)
    {
        var data = File.ReadAllBytes(file);
        double w = 12, h = 8;
        try { using var ms = new MemoryStream(data); var bmp = new Bitmap(ms); if (bmp.PixelSize.Width > 0) h = w * bmp.PixelSize.Height / (double)bmp.PixelSize.Width; } catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException) { }
        InsertBlock(new Picture { Data = data, Ext = Path.GetExtension(file).TrimStart('.').ToLowerInvariant().Replace("jpeg", "jpg"), WidthCm = w, HeightCm = Math.Clamp(h, 1, 24) });
    }

    private void InsertBlock(Trendo.Core.Office.Block b)
    {
        Snapshot();
        var at = Math.Min(TextDocument.Blocks.Count, TextFocusBlock + 1);
        TextDocument.Blocks.Insert(at, b);
        if (at + 1 >= TextDocument.Blocks.Count || TextDocument.Blocks[at + 1] is not Para) TextDocument.Blocks.Insert(at + 1, new Para()); // danach weiterschreiben
        TextFocusBlock = at + 1; _txSel = _txSelLen = 0; Changed();
    }

    private void TextPrint()
    {
        try
        {
            var f = Path.Combine(Path.GetTempPath(), $"trendo-druck-{DateTime.Now:yyyyMMdd-HHmmss}.pdf");
            TextPdf.Export(TextDocument, f);
            var sumatra = ToolPath("sumatra");
            if (sumatra is not null && !_opt.Isolated) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(sumatra, $"-print-dialog \"{f}\"") { UseShellExecute = false });
            else Launch(f);
            _txMsg = "Druckansicht geöffnet – dort Drucker wählen.";
            _core.Audit.Append(Principal.LocalOwner.Name, "text.print", Path.GetFileName(f));
        }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception or InvalidOperationException) { _txMsg = "Drucken ging nicht: " + ex.Message; }
    }

    // ---------- Seite ----------
    private Control TextPaper()
    {
        var page = new StackPanel { Name = "text-paper", Spacing = 2 };
        var number = 0;
        for (var i = 0; i < TextDocument.Blocks.Count; i++)
        {
            var b = TextDocument.Blocks[i]; var index = i;
            number = b is Para { List: ListKind.Number } ? number + 1 : 0;
            page.Children.Add(b switch
            {
                Para p => ParaControl(p, index, number),
                Table t => TableControl(t, index),
                Picture pic => PictureControl(pic, index),
                PageBreak => BreakControl(index),
                _ => new Border(),
            });
        }
        var paper = new Border { Background = Brushes.White, Width = 700, Padding = new Thickness(64, 56), Margin = new Thickness(0, 4, 8, 8), HorizontalAlignment = HorizontalAlignment.Center,
            BoxShadow = BoxShadows.Parse("0 6 24 0 #66000000"), Child = page, MinHeight = 900 };
        return new ScrollViewer { Name = "text-scroll", Content = paper, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private static readonly IBrush Ink = new SolidColorBrush(Color.Parse("#1A1A1A"));
    private static IBrush StyleBrush(ParaStyle s) => s switch
    {
        ParaStyle.Heading1 => Brush.Parse("#1F3864"), ParaStyle.Heading2 or ParaStyle.Heading3 => Brush.Parse("#2F5496"), ParaStyle.Subtitle => Brush.Parse("#595959"), ParaStyle.Quote => Brush.Parse("#404040"), _ => Ink,
    };

    /// <summary>Eingabefelder auf dem weißen Blatt: das dunkle Design färbt sie sonst beim Bearbeiten schwarz.</summary>
    private static void PaperBox(TextBox tb, IBrush background)
    {
        foreach (var k in new[] { "TextControlBackground", "TextControlBackgroundPointerOver", "TextControlBackgroundFocused" }) tb.Resources[k] = background;
        foreach (var k in new[] { "TextControlForeground", "TextControlForegroundPointerOver", "TextControlForegroundFocused" }) tb.Resources[k] = tb.Foreground ?? Ink;
        tb.Resources["TextControlBorderBrushFocused"] = Brush.Parse("#2F86F6");
        tb.Resources["TextControlBorderBrushPointerOver"] = Brush.Parse("#7FA7E0");
        tb.Resources["TextControlSelectionHighlightColor"] = Brush.Parse("#662F86F6");
    }

    private static TextAlignment ToAlign(Align a) => a switch { Align.Center => TextAlignment.Center, Align.Right => TextAlignment.Right, Align.Justify => TextAlignment.Justify, _ => TextAlignment.Left };

    private Control ParaControl(Para p, int index, int number)
    {
        var size = TextDoc.StyleSize(p.Style) * 1.33; // Punkt → Bildschirm
        var bold = p.Style is ParaStyle.Title or ParaStyle.Heading1 or ParaStyle.Heading2 or ParaStyle.Heading3;
        Control body;
        if (index == TextFocusBlock)
        {
            var tb = new TextBox
            {
                Name = "text-edit", Text = p.Text, FontSize = size, FontWeight = bold ? FontWeight.Bold : FontWeight.Normal, FontStyle = p.Style == ParaStyle.Quote ? FontStyle.Italic : FontStyle.Normal,
                Foreground = StyleBrush(p.Style), Background = Brush.Parse("#0F2F86F6"), BorderThickness = new Thickness(0, 0, 0, 1), BorderBrush = Brush.Parse("#552F86F6"),
                TextWrapping = TextWrapping.Wrap, AcceptsReturn = false, TextAlignment = ToAlign(p.Align), Padding = new Thickness(2, 1), MinHeight = 0, FontFamily = new FontFamily("Arial"),
                CaretBrush = Ink, SelectionBrush = Brush.Parse("#662F86F6"),
            };
            PaperBox(tb, Brush.Parse("#F2F6FC"));
            Avalonia.Automation.AutomationProperties.SetName(tb, "Absatz bearbeiten");
            _txActive = tb;
            tb.TextChanged += (_, _) =>
            {
                if (tb.Text == p.Text) return;
                if (!TextDirty || _txUndo.Count == 0 || _txUndo[^1].Blocks.Count != TextDocument.Blocks.Count) Snapshot();
                TextDoc.EditText(p, tb.Text ?? ""); TextDirty = true;
            };
            void Track() { var a = Math.Min(tb.SelectionStart, tb.SelectionEnd); var e = Math.Max(tb.SelectionStart, tb.SelectionEnd); _txSel = a; _txSelLen = e - a; if (e == a) _txSel = tb.CaretIndex; }
            tb.PropertyChanged += (_, e) => { if (e.Property == TextBox.SelectionStartProperty || e.Property == TextBox.SelectionEndProperty || e.Property == TextBox.CaretIndexProperty) Track(); };
            tb.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter) { e.Handled = true; Snapshot(); TextDocument.SplitAt(index, tb.CaretIndex); TextFocusBlock = index + 1; _txSel = _txSelLen = 0; Changed(); Show("text"); }
                else if (e.Key == Key.Back && tb.CaretIndex == 0 && tb.SelectionStart == tb.SelectionEnd && index > 0)
                {
                    Snapshot(); var at = TextDocument.MergeWithPrevious(index);
                    if (at >= 0) { e.Handled = true; TextFocusBlock = TextDocument.Blocks[index - 1] is Para ? index - 1 : Math.Max(0, index - 1); _txSel = at; _txSelLen = 0; Changed(); Show("text"); }
                }
                else if (e.Key == Key.Up && tb.CaretIndex == 0 && index > 0) { e.Handled = true; FocusPara(index - 1, int.MaxValue); }
                else if (e.Key == Key.Down && tb.CaretIndex >= (tb.Text?.Length ?? 0) && index + 1 < TextDocument.Blocks.Count) { e.Handled = true; FocusPara(index + 1, 0); }
                else if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                {
                    Track();
                    var f = e.Key switch { Key.B => Fmt.Bold, Key.I => Fmt.Italic, Key.U => Fmt.Underline, _ => Fmt.None };
                    if (f != Fmt.None) { e.Handled = true; TextToggle(f); Show("text"); }
                    else if (e.Key == Key.Z) { e.Handled = true; TextUndo(); Show("text"); }
                    else if (e.Key == Key.Y) { e.Handled = true; TextRedo(); Show("text"); }
                    else if (e.Key == Key.F) { e.Handled = true; _txFind = true; Show("text"); }
                    else if (e.Key == Key.S) { e.Handled = true; TextSaveAs(TextPath ?? DefaultTextName(".docx")); Show("text"); }
                }
            };
            tb.AttachedToVisualTree += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                tb.Focus();
                var s = Math.Clamp(_txSel, 0, tb.Text?.Length ?? 0); var l = Math.Clamp(_txSelLen, 0, (tb.Text?.Length ?? 0) - s);
                tb.SelectionStart = s; tb.SelectionEnd = s + l; tb.CaretIndex = s + l;
            });
            body = tb;
        }
        else
        {
            var view = new TextBlock { Name = "text-para-" + index, FontSize = size, FontWeight = bold ? FontWeight.Bold : FontWeight.Normal, FontStyle = p.Style == ParaStyle.Quote ? FontStyle.Italic : FontStyle.Normal,
                Foreground = StyleBrush(p.Style), TextWrapping = TextWrapping.Wrap, TextAlignment = ToAlign(p.Align), Padding = new Thickness(2, 1), FontFamily = new FontFamily("Arial"), MinHeight = size * 1.2, Background = Brushes.Transparent, Cursor = new Cursor(StandardCursorType.Ibeam) };
            var errors = _txMistakes.Where(m => m.Block == index).ToList();
            var pos = 0;
            foreach (var (t, f, sz) in p.Runs())
            {
                // Abschnitt an Fehlergrenzen teilen, Fehler rot unterstreichen
                var cuts = new SortedSet<int> { 0, t.Length };
                foreach (var m in errors) { cuts.Add(Math.Clamp(m.Start - pos, 0, t.Length)); cuts.Add(Math.Clamp(m.Start + m.Length - pos, 0, t.Length)); }
                var list = cuts.ToList();
                for (var k = 0; k + 1 < list.Count; k++)
                {
                    var a = list[k]; var e2 = list[k + 1]; if (e2 <= a) continue;
                    var wrong = errors.Any(m => pos + a >= m.Start && pos + a < m.Start + m.Length);
                    var run = new Run(t[a..e2]);
                    if (f.HasFlag(Fmt.Bold)) run.FontWeight = FontWeight.Bold;
                    if (f.HasFlag(Fmt.Italic)) run.FontStyle = FontStyle.Italic;
                    if (sz is { } s2) run.FontSize = s2 * 1.33;
                    if (f.HasFlag(Fmt.Super) || f.HasFlag(Fmt.Sub)) { run.FontSize = (sz ?? TextDoc.StyleSize(p.Style)) * 1.33 * 0.62; run.BaselineAlignment = f.HasFlag(Fmt.Super) ? BaselineAlignment.Superscript : BaselineAlignment.Subscript; }
                    var deco = new TextDecorationCollection();
                    if (f.HasFlag(Fmt.Underline)) deco.Add(new TextDecoration { Location = TextDecorationLocation.Underline });
                    if (f.HasFlag(Fmt.Strike)) deco.Add(new TextDecoration { Location = TextDecorationLocation.Strikethrough });
                    if (wrong) deco.Add(new TextDecoration { Location = TextDecorationLocation.Underline, Stroke = Brushes.Red, StrokeThickness = 1.6, StrokeOffset = 2 });
                    if (deco.Count > 0) run.TextDecorations = deco;
                    view.Inlines!.Add(run);
                }
                pos += t.Length;
            }
            if (p.Text.Length == 0) view.Inlines!.Add(new Run(" "));
            view.PointerPressed += (_, e) =>
            {
                var at = p.Text.Length;
                try { at = Math.Clamp(view.TextLayout.HitTestPoint(e.GetPosition(view)).TextPosition, 0, p.Text.Length); } catch (InvalidOperationException) { }
                e.Handled = true; FocusPara(index, at);
            };
            body = view;
        }
        if (p.List == ListKind.None) return body;
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("28,*") };
        g.Children.Add(new TextBlock { Text = p.List == ListKind.Bullet ? "•" : number + ".", FontSize = size, Foreground = Ink, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 1, 6, 0), FontFamily = new FontFamily("Arial") });
        Grid.SetColumn(body, 1); g.Children.Add(body);
        return g;
    }

    private void FocusPara(int index, int caret)
    {
        TextFocusBlock = index;
        _txSel = TextDocument.Blocks[index] is Para p ? Math.Clamp(caret, 0, p.Text.Length) : 0; _txSelLen = 0;
        Show("text");
    }

    private Control BlockTools(int index, params Control[] extra)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var c in extra) row.Children.Add(c);
        var del = TB("text-block-delete-" + index, "✕", () => { Snapshot(); TextDocument.Blocks.RemoveAt(index); if (TextDocument.Blocks.Count == 0) TextDocument.Blocks.Add(new Para()); TextFocusBlock = Math.Clamp(index - 1, 0, TextDocument.Blocks.Count - 1); Changed(); }, "Entfernen");
        row.Children.Add(del);
        return row;
    }

    private Control TableControl(Table t, int index)
    {
        var grid = new Grid { Name = "text-table-" + index };
        for (var c = 0; c < t.Cols; c++) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (var r = 0; r < t.Rows.Count; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (var c = 0; c < t.Cols; c++)
            {
                var rr = r; var cc = c;
                while (t.Rows[rr].Count < t.Cols) t.Rows[rr].Add("");
                var cell = new TextBox { Name = $"text-cell-{index}-{r}-{c}", Text = t.Rows[r][c], Foreground = Ink, Background = Brushes.White, BorderBrush = Brush.Parse("#999999"), BorderThickness = new Thickness(0.5), CornerRadius = new CornerRadius(0), TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, FontSize = 13, FontFamily = new FontFamily("Arial"), CaretBrush = Ink, MinHeight = 0, Padding = new Thickness(4, 2) };
                PaperBox(cell, Brushes.White);
                cell.TextChanged += (_, _) => { if (t.Rows[rr][cc] != cell.Text) { t.Rows[rr][cc] = cell.Text ?? ""; TextDirty = true; } };
                cell.GotFocus += (_, _) => { TextFocusBlock = index; };
                Grid.SetRow(cell, r); Grid.SetColumn(cell, c); grid.Children.Add(cell);
            }
        }
        var tools = BlockTools(index,
            TB("text-row-add-" + index, "+ Zeile", () => { Snapshot(); TextDoc.InsertRow(t, t.Rows.Count); Changed(); }, "Zeile anfügen"),
            TB("text-col-add-" + index, "+ Spalte", () => { Snapshot(); TextDoc.InsertColumn(t, t.Cols); Changed(); }, "Spalte anfügen"),
            TB("text-row-del-" + index, "− Zeile", () => { Snapshot(); TextDoc.DeleteRow(t, t.Rows.Count - 1); Changed(); }, "Letzte Zeile entfernen"),
            TB("text-col-del-" + index, "− Spalte", () => { Snapshot(); TextDoc.DeleteColumn(t, t.Cols - 1); Changed(); }, "Letzte Spalte entfernen"));
        return new StackPanel { Margin = new Thickness(0, 6), Children = { grid, tools } };
    }

    private Control PictureControl(Picture pic, int index)
    {
        Control img;
        try { using var ms = new MemoryStream(pic.Data); img = new Image { Source = new Bitmap(ms), Width = Math.Min(572, pic.WidthCm * 37.8), Height = Math.Min(572, pic.WidthCm * 37.8) * pic.HeightCm / Math.Max(0.1, pic.WidthCm), Stretch = Stretch.Uniform }; }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException) { img = new TextBlock { Text = "[Bild]", Foreground = Ink }; }
        img.Name = "text-picture-" + index;
        var tools = BlockTools(index,
            TB("text-img-smaller-" + index, "kleiner", () => { Snapshot(); pic.HeightCm *= Math.Max(2, pic.WidthCm - 2) / pic.WidthCm; pic.WidthCm = Math.Max(2, pic.WidthCm - 2); Changed(); }, "Bild kleiner"),
            TB("text-img-bigger-" + index, "größer", () => { Snapshot(); var w = Math.Min(16, pic.WidthCm + 2); pic.HeightCm *= w / pic.WidthCm; pic.WidthCm = w; Changed(); }, "Bild größer"));
        return new StackPanel { Margin = new Thickness(0, 6), HorizontalAlignment = HorizontalAlignment.Left, Children = { img, tools } };
    }

    private Control BreakControl(int index) => new Grid
    {
        Name = "text-break-" + index, Margin = new Thickness(0, 10), ColumnDefinitions = new ColumnDefinitions("*,Auto"),
        Children = { new Border { BorderBrush = Brush.Parse("#9AA5B1"), BorderThickness = new Thickness(0, 1, 0, 0), VerticalAlignment = VerticalAlignment.Center, Child = new TextBlock { Text = "Seitenumbruch", Foreground = Brush.Parse("#7A8591"), FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, Background = Brushes.White, Padding = new Thickness(8, 0) } }, Pos(BlockTools(index), 1) },
    };

    // ---------- Seitenleiste: Suchen/Ersetzen, Rechtschreibung ----------
    private Control? TextSidePanel()
    {
        if (!_txFind && !_txSpellOn) return null;
        var sp = new StackPanel { Spacing = 6, Width = 260, Margin = new Thickness(6, 4, 0, 0) };
        if (_txFind)
        {
            var q = new TextBox { Name = "text-find-q", PlaceholderText = "Suchen nach …", Text = _txFindQ };
            var r = new TextBox { Name = "text-find-r", PlaceholderText = "Ersetzen durch …", Text = _txFindR };
            var mc = new CheckBox { Name = "text-find-case", Content = "Groß/klein beachten", IsChecked = _txFindCase, Foreground = Text };
            var ww = new CheckBox { Name = "text-find-word", Content = "Nur ganze Wörter", IsChecked = _txFindWord, Foreground = Text };
            void Grab() { _txFindQ = q.Text ?? ""; _txFindR = r.Text ?? ""; _txFindCase = mc.IsChecked == true; _txFindWord = ww.IsChecked == true; }
            var next = new Button { Name = "text-find-next", Content = "Weitersuchen" };
            next.Click += (_, _) =>
            {
                Grab(); _txHits = TextDocument.Find(_txFindQ, _txFindCase, _txFindWord);
                if (_txHits.Count == 0) { _txMsg = $"„{_txFindQ}“ nicht gefunden."; Show("text"); return; }
                _txHit = (_txHit + 1) % _txHits.Count; var h = _txHits[_txHit];
                if (h.Row < 0) TextSelect(h.Block, h.Start, h.Length); else TextFocusBlock = h.Block;
                _txMsg = $"Treffer {_txHit + 1} von {_txHits.Count}"; Show("text");
            };
            var one = new Button { Name = "text-replace-one", Content = "Ersetzen" };
            one.Click += (_, _) =>
            {
                Grab();
                if (CurPara() is { } p && _txSelLen > 0 && string.Equals(p.Text.Substring(_txSel, _txSelLen), _txFindQ, _txFindCase ? StringComparison.Ordinal : StringComparison.CurrentCultureIgnoreCase))
                { Snapshot(); TextDoc.ReplaceAt(p, _txSel, _txSelLen, _txFindR); _txSelLen = _txFindR.Length; Changed(); _txMsg = "Ersetzt."; }
                else _txMsg = "Zuerst „Weitersuchen“ – dann ersetzen.";
                Show("text");
            };
            var all = new Button { Name = "text-replace-all", Content = "Alle ersetzen" };
            all.Click += (_, _) => { Grab(); Snapshot(); var n = TextDocument.ReplaceAll(_txFindQ, _txFindR, _txFindCase, _txFindWord); if (n > 0) Changed(); _txMsg = $"{n}× ersetzt."; Show("text"); };
            sp.Children.Add(Glass2(new StackPanel { Spacing = 6, Children = { H("Suchen & Ersetzen", 13), q, r, mc, ww, new WrapPanel { Children = { next, new Border { Width = 4 }, one, new Border { Width = 4 }, all } } } }, 10));
        }
        if (_txSpellOn)
        {
            var box = new StackPanel { Spacing = 4, Children = { H("Rechtschreibung", 13) } };
            if (_spelling is null) box.Children.Add(P("Das deutsche Wörterbuch fehlt. Es kommt mit dem Trendo-Vollpaket (LibreOffice-Paket) – unter „Zusatzprogramme“ einrichten.", null, 11));
            else if (_txMistakes.Count == 0) box.Children.Add(P("Keine Fehler gefunden ✓", Brush.Parse("#4ADE80"), 12));
            else
            {
                box.Children.Add(P($"{_txMistakes.Count} unbekannte Wörter – rot unterstrichen.", null, 11));
                for (var i = 0; i < Math.Min(_txMistakes.Count, 25); i++)
                {
                    var m = _txMistakes[i];
                    var row = new WrapPanel();
                    row.Children.Add(new TextBlock { Text = m.Word, Foreground = Brush.Parse("#FF8A8A"), FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 4) });
                    var k = 0;
                    foreach (var s in _spelling.Suggest(m.Word).Take(3))
                    {
                        var b = new Button { Name = $"text-fix-{i}-{k++}", Content = s, Padding = new Thickness(6, 2), Margin = new Thickness(0, 0, 4, 4), FontSize = 11 };
                        b.Click += (_, _) => { if (TextDocument.Blocks[m.Block] is Para p && p.Text.Substring(m.Start, m.Length) == m.Word) { Snapshot(); TextDoc.ReplaceAt(p, m.Start, m.Length, s); Changed(); } Show("text"); };
                        row.Children.Add(b);
                    }
                    var add = new Button { Name = $"text-learn-{i}", Content = "Ins Wörterbuch", Padding = new Thickness(6, 2), Margin = new Thickness(0, 0, 4, 4), FontSize = 11 };
                    add.Click += (_, _) => { _spelling.AddOwn(m.Word); RunSpelling(); Show("text"); };
                    row.Children.Add(add);
                    box.Children.Add(row);
                }
            }
            sp.Children.Add(Glass2(box, 10));
        }
        return new ScrollViewer { Content = sp };
    }

    private string _txFindQ = "", _txFindR = ""; private bool _txFindCase, _txFindWord;
}
