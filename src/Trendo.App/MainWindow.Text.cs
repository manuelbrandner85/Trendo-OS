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
/// T-0692: durchgehend bearbeiten – ein Schreibfeld über das ganze Dokument (<see cref="TextPageView"/>) mit echten Seiten; Cursor und Auswahl gehen über Absatzgrenzen.</summary>
public partial class MainWindow
{
    public TextDoc TextDocument { get; private set; } = TextDoc.Empty();
    /// <summary>Für Tests: Projektordner (isoliert im Datenordner).</summary>
    public string ProjectsRootForTests => ProjectsRoot;
    public string? TextPath { get; private set; }
    public bool TextDirty { get; private set; }
    /// <summary>Baustein, in dem der Cursor steht.</summary>
    public int TextFocusBlock { get => _txView.Caret.Block; private set { if (value != _txView.Caret.Block) _txView.SetQuiet(new TextPos(value, 0), new TextPos(value, 0)); else _txView.ClampSelection(); } }
    /// <summary>Auswahl im Baustein mit dem Cursor (Anfang, Länge) – geht die Auswahl über mehrere Absätze, der Teil in diesem Absatz.</summary>
    private int _txSel { get => TextSelHere().Start; set { var c = _txView.Caret with { Offset = Math.Max(0, value) }; _txView.SetQuiet(c, c); } }
    private int _txSelLen { get => TextSelHere().Length; set { var st = TextSelHere().Start; var c = _txView.Caret; _txView.SetQuiet(c with { Offset = st }, c with { Offset = st + Math.Max(0, value) }); } }
    private (int Start, int Length) TextSelHere()
    {
        var (a, b) = _txView.Range; var c = _txView.Caret;
        if (a.SameBox(b)) return (a.Offset, b.Offset - a.Offset);
        return c == b ? (0, c.Offset) : (c.Offset, Math.Max(0, TextEdit.Length(TextDocument, c) - c.Offset));
    }
    private string _txMsg = "";
    private bool _txMore, _txFind, _txSpellOn;
    private readonly List<TextDoc> _txUndo = [], _txRedo = [];
    private List<Spelling.Mistake> _txMistakes = [];
    private Spelling? _spelling; private bool _spellTried;
    private List<TextDoc.Hit> _txHits = []; private int _txHit = -1;

    private void InitText()
    {
        _showMap["text"] = TextPageView;
        PageModules["text"] = "SYS-10";
        ExtraPages.Add(("Trendo Text", "text", ["text", "schreiben", "word", "docx", "odt", "lebenslauf", "rechnung", "rechtschreibung"]));
    }

    // ---------- Öffentliche Wege (auch für Tests und die KI) ----------
    public void TextNew(string template = "leer")
    {
        TextDocument = TextTemplates.Create(template); TextPath = null; TextDirty = false; TextFocusBlock = 0; _txSel = _txSelLen = 0; ImportGuardClear("text");
        _txUndo.Clear(); _txRedo.Clear(); _txMistakes = []; _txHits = [];
        _txMsg = template == "leer" ? "Neues Dokument." : $"Vorlage „{TextTemplates.Builtin.First(t => t.Id == template).Name}“ – Platzhalter in [Klammern] einfach überschreiben.";
    }

    public void TextOpen(string path)
    {
        TextDocument = TextFiles.Open(path); TextPath = Path.GetExtension(path).Equals(".txt", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path).Equals(".odt", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path).Equals(".docx", StringComparison.OrdinalIgnoreCase) ? path : null;
        TextDirty = false; TextFocusBlock = 0; _txSel = _txSelLen = 0; _txUndo.Clear(); _txRedo.Clear(); _txMistakes = []; _txHits = [];
        _txMsg = $"Geöffnet: {Path.GetFileName(path)}";
        ImportGuardSet("text", path, TextDocument.Import); // T-0682: fremde Inhalte, die Trendo nicht hält – Original vor stillem Überschreiben schützen
        RememberRecent(path);
        _core.Audit.Append(Principal.LocalOwner.Name, "text.open", Path.GetFileName(path));
    }

    /// <summary>Speichert das Dokument. Liefert false, wenn das Speichern aufgehalten wurde, weil es eine fremde Datei mit nicht übernommenen Inhalten überschriebe (T-0682).</summary>
    public bool TextSaveAs(string path)
    {
        if (ImportGuardStops("text", path)) { _txMsg = ImportStopMessage("text"); return false; }
        TextFiles.Save(TextDocument, path);
        if (!path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) { TextPath = path; TextDirty = false; RememberRecent(path); OfficeSaved("text"); ImportGuardClear("text"); }
        _txMsg = $"Gespeichert: {Path.GetFileName(path)}";
        _core.Audit.Append(Principal.LocalOwner.Name, "text.save", Path.GetFileName(path));
        return true;
    }

    /// <summary>Cursor/Auswahl in einem Absatz setzen (wie Klicken und Markieren).</summary>
    public void TextSelect(int block, int start, int length = 0)
    {
        _txView.Refresh();
        _txView.Select(new TextPos(block, start), new TextPos(block, start + length));
    }

    /// <summary>T-0692: Auswahl über Absatzgrenzen – von (Baustein, Zeichen) bis (Baustein, Zeichen).</summary>
    public void TextSelectRange(int fromBlock, int fromOffset, int toBlock, int toOffset)
    {
        _txView.Refresh();
        _txView.Select(new TextPos(fromBlock, fromOffset), new TextPos(toBlock, toOffset));
    }

    /// <summary>Zeichenformat auf die Auswahl umschalten (Knöpfe F, K, U …) – über alle markierten Absätze und Tabellenzellen.
    /// Ohne Auswahl wird das Format für das Weitertippen vorgemerkt.</summary>
    public void TextToggle(Fmt f)
    {
        var v = _txView;
        if (TextEdit.ParaAt(TextDocument, v.Caret) is null) return;
        var on = !TextIsOn(f);
        if (!v.HasSelection)
        {
            v.PendingOn = on ? v.PendingOn | f : v.PendingOn & ~f; v.PendingOff = on ? v.PendingOff & ~f : v.PendingOff | f;
            return;
        }
        Snapshot(); TextEdit.Apply(TextDocument, v.Anchor, v.Caret, (p, s, l) => TextDoc.Format(p, s, l, f, on)); Changed();
    }

    /// <summary>Ist das Zeichenformat in der ganzen Auswahl gesetzt – ohne Auswahl: gilt es für das nächste getippte Zeichen?</summary>
    public bool TextIsOn(Fmt f)
    {
        var v = _txView;
        if (v.HasSelection)
        {
            var parts = TextEdit.Parts(TextDocument, v.Anchor, v.Caret).Where(x => x.Length > 0).ToList();
            return parts.Count > 0 && parts.All(x => TextEdit.ParaAt(TextDocument, x.Box) is { } p && TextDoc.Has(p, x.Start, x.Length, f));
        }
        if (v.PendingOn.HasFlag(f)) return true;
        if (v.PendingOff.HasFlag(f)) return false;
        return TextEdit.ParaAt(TextDocument, v.Caret) is { Text.Length: > 0 } q && q.At(Math.Max(0, v.Caret.Offset - 1)).Fmt.HasFlag(f);
    }

    /// <summary>Absatzformat (Absatzart, Ausrichtung, Liste, Abstand) auf alle Absätze der Auswahl anwenden.</summary>
    private bool TextEachPara(Action<Para> change)
    {
        var v = _txView;
        if (TextEdit.ParaAt(TextDocument, v.Caret) is null) return false;
        Snapshot(); TextEdit.ApplyParas(TextDocument, v.Anchor, v.Caret, change); Changed();
        return true;
    }

    /// <summary>Der Absatz bzw. die Tabellenzelle am Cursor (nur zum Lesen – für die Anzeige in der Werkzeugzeile).</summary>
    private Para? CurBox() => TextEdit.ParaAt(TextDocument, _txView.Caret);

    private Para? CurPara() => TextFocusBlock < TextDocument.Blocks.Count ? TextDocument.Blocks[TextFocusBlock] as Para : null;

    private void Snapshot()
    {
        _txUndo.Add(TextDocument.Copy()); if (_txUndo.Count > Trendo.Core.Programs.ProgramDocument.MaxUndo) _txUndo.RemoveAt(0); // T-0714: 100 Schritte wie die Hülle (vorher 80)
        _txRedo.Clear();
        _txLastKind = ""; // jede andere Änderung beendet einen laufenden Tipp-Schritt
    }

    private void Changed() { TextDirty = true; if (_txSpellOn) RunSpelling(); _txView.Refresh(); }

    public void TextUndo() { if (_txUndo.Count == 0) return; _txLastKind = ""; _txRedo.Add(TextDocument); TextDocument = _txUndo[^1]; _txUndo.RemoveAt(_txUndo.Count - 1); _txView.Refresh(); _txView.ClampSelection(); TextDirty = true; }
    public void TextRedo() { if (_txRedo.Count == 0) return; _txLastKind = ""; _txUndo.Add(TextDocument); TextDocument = _txRedo[^1]; _txRedo.RemoveAt(_txRedo.Count - 1); _txView.Refresh(); _txView.ClampSelection(); TextDirty = true; }

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
    private Control TextPageView() => new Border { Name = "text-page-root", Background = PageSurface, CornerRadius = new CornerRadius(10), Padding = new Thickness(10), Child = TextShell() };

    private Control TextShell()
    {
        var g = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*,Auto"), ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        _txOverflow.Clear(); // nur Werkzeuge dieses Aufbaus kommen in die Leiste „Mehr …“
        var head = PageHead("Trendo Text"); Grid.SetColumnSpan(head, 2); g.Children.Add(head);
        var ribbon = new StackPanel { Children = { TextRibbonTabs() } };
        // V-20 (T-0692): in „Einfach“ genau eine Werkzeugzeile; Formatvorlagen-Kacheln und alles Weitere unter „Mehr …“ bzw. ab „Erweitert“
        if (_txRibbon == "start") { ribbon.Children.Add(TextToolbar()); if (!TextOneRow) ribbon.Children.Add(StyleGallery()); } else ribbon.Children.Add(TextRibbonBody());
        if (TextBlockTools() is { } blockTools) ribbon.Children.Add(blockTools);
        if (TextPromptBar() is { } prompt) ribbon.Children.Add(prompt);
        if (TextAiProposalCard() is { } proposal) ribbon.Children.Add(proposal); // 108-49: Vorschlag statt sofort ersetzen
        if (RecoveryBanner("text", TextDirty) is { } recovery) ribbon.Children.Add(recovery); // T-0254
        if (ImportBanner("text") is { } import) ribbon.Children.Add(import); // T-0682
        Grid.SetRow(ribbon, 1); Grid.SetColumnSpan(ribbon, 2); g.Children.Add(ribbon);
        var more = TextMoreBar(); more.IsVisible = _txMore && _txRibbon == "start"; Grid.SetRow(more, 2); Grid.SetColumnSpan(more, 2); g.Children.Add(more);
        var page = TextPaper(); Grid.SetRow(page, 3); g.Children.Add(page);
        var side = TextSidePanel(); if (side is not null) { Grid.SetRow(side, 3); Grid.SetColumn(side, 1); g.Children.Add(side); }
        var status = _txStatus = new TextBlock { Name = "text-status", Foreground = Muted, FontSize = 11.5, Margin = new Thickness(4, 6, 0, 0), TextWrapping = TextWrapping.Wrap, Text = TextStatusText() };
        Grid.SetRow(status, 4); Grid.SetColumnSpan(status, 2); g.Children.Add(status);
        return g;
    }

    /// <summary>Knopf, der dem Eingabefeld den Fokus nicht wegnimmt – so bleibt die Auswahl für Formate erhalten.</summary>
    private Button TB(string name, string label, Action go, string tip, bool on = false, bool enabled = true)
    {
        var b = new Button { Name = name, Content = label, Focusable = false, Margin = new Thickness(0, 0, 4, 4), Padding = new Thickness(9, 4), MinWidth = 30, IsEnabled = enabled, HorizontalContentAlignment = HorizontalAlignment.Center };
        if (on) { b.Background = Brush.Parse("#33C9A227"); b.BorderBrush = Accent; b.BorderThickness = new Thickness(1); } // eingeschaltet: goldener Rand – die eine goldene Fläche bleibt „Speichern“
        ToolTip.SetTip(b, tip); Avalonia.Automation.AutomationProperties.SetName(b, tip);
        b.Click += (_, _) => { go(); Show("text"); };
        return b;
    }

    /// <summary>V-20 (T-0692): Stufe „Einfach“ zeigt genau eine Werkzeugzeile mit den wichtigsten Formaten; „Mehr …“ öffnet alles Weitere. Ab „Erweitert“ steht alles offen.</summary>
    private bool TextOneRow => IsSimple && !_txMore;
    /// <summary>In der einen Zeile nicht gezeigte Werkzeuge – sie stehen in der Leiste „Mehr …“.</summary>
    private readonly List<Control> _txOverflow = [];

    private Control TextToolbar()
    {
        var one = TextOneRow; _txOverflow.Clear();
        var bar = new WrapPanel { Name = "text-toolbar" };
        void Add(Control c, bool main) { if (main || !one) bar.Children.Add(c); else _txOverflow.Add(c); }
        var p = CurBox(); var para = CurPara();
        var newBtn = new Button { Name = "text-new", Content = "Neu", Focusable = false, Margin = new Thickness(0, 0, 4, 4), Padding = new Thickness(9, 4) };
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
                TextDocument = TextFiles.Open(tmp); File.Delete(tmp); TextPath = null; TextDirty = false; ImportGuardClear("text"); TextFocusBlock = 0; _txUndo.Clear(); _txMsg = $"Vorlage „{label}“."; Show("text");
            };
            menu.Items.Add(mi);
        }
        newBtn.ContextMenu = menu; newBtn.Click += (_, _) => menu.Open(newBtn);
        var docActions = new StackPanel(); bar.Children.Add(docActions); // V-14 (T-0700): Platzhalter – Speichern · Rückgängig · Wiederholen ganz links
        bar.Children.Add(newBtn);
        var open = new Button { Name = "text-open", Content = "Öffnen …", Focusable = false, Margin = new Thickness(0, 0, 4, 4), Padding = new Thickness(9, 4) };
        open.Click += async (_, _) => { if (await PickText(false, "") is { } f) { try { TextOpen(f); } catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or System.Xml.XmlException) { _txMsg = "Öffnen ging nicht: " + Trendo.Core.FriendlyError.Say(ex); } } Show("text"); };
        bar.Children.Add(open);
        var save = new Button { Name = "text-save", Content = "Speichern", Focusable = false, Background = Accent, Margin = new Thickness(0, 0, 4, 4), Padding = new Thickness(9, 4) };
        ToolTip.SetTip(save, "Speichern (Strg+S)");
        save.Click += (_, _) => { try { TextSaveAs(TextPath ?? DefaultTextName(".docx")); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _txMsg = "Nicht gespeichert: " + Trendo.Core.FriendlyError.Say(ex); } Show("text"); };
        bar.Children.Add(save);
        {
            Add(TB("text-pdf", "Als PDF", () => { try { var f = DefaultTextName(".pdf"); var n = TextPdf.Export(TextDocument, f); _txMsg = $"PDF gespeichert: {Path.GetFileName(f)} ({n} Seiten)"; _core.Audit.Append(Principal.LocalOwner.Name, "text.pdf", Path.GetFileName(f)); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) { _txMsg = "PDF ging nicht: " + Trendo.Core.FriendlyError.Say(ex); } }, "Als PDF speichern – dieselben Seiten wie am Bildschirm"), false);
            Add(TB("text-print", "Drucken", TextPrint, "Drucken (über PDF)"), false);
        }
        bar.Children.Add(new Border { Width = 10 });
        // V-14 (T-0700): gemeinsamer Platz für Speichern · Rückgängig · Wiederholen (wie im Menüband der Programm-Hülle)
        bar.Children[bar.Children.IndexOf(docActions)] = DocActions(save, TB("text-undo", "↶", TextUndo, "Rückgängig (Strg+Z)", enabled: _txUndo.Count > 0), TB("text-redo", "↷", TextRedo, "Wiederholen (Strg+Y)", enabled: _txRedo.Count > 0));
        var style = new ComboBox { Name = "text-style", Focusable = false, Width = 140, Margin = new Thickness(0, 0, 4, 4), IsEnabled = para is not null };
        foreach (var s in Enum.GetValues<ParaStyle>()) style.Items.Add(TextDoc.StyleName(s));
        style.SelectedIndex = para is null ? 0 : (int)para.Style;
        style.SelectionChanged += (_, _) => { if (CurPara() is { } cp && style.SelectedIndex >= 0 && (int)cp.Style != style.SelectedIndex) { var st = (ParaStyle)style.SelectedIndex; TextEachPara(x => x.Style = st); Show("text"); } };
        ToolTip.SetTip(style, "Absatzart: Titel, Überschriften, Text, Zitat");
        bar.Children.Add(style);
        // Pilot 0.9.47: Schriftart, Größe, Farbe, Hervorhebung, Zeilen- und Absatzabstand – in der einen Zeile nur Größe und Farbe
        var look = TextLookItems().ToList();
        foreach (var item in look) Add(item, item.Name is "text-size" or "text-color");
        bar.Children.Add(TB("text-bold", "F", () => TextToggle(Fmt.Bold), "Fett (Strg+B)", TextIsOn(Fmt.Bold), p is not null));
        bar.Children.Add(TB("text-italic", "K", () => TextToggle(Fmt.Italic), "Kursiv (Strg+I)", TextIsOn(Fmt.Italic), p is not null));
        bar.Children.Add(TB("text-underline", "U", () => TextToggle(Fmt.Underline), "Unterstrichen (Strg+U)", TextIsOn(Fmt.Underline), p is not null));
        bar.Children.Add(TB("text-bullets", "• Liste", () => SetList(ListKind.Bullet), "Aufzählung", para?.List == ListKind.Bullet, para is not null));
        bar.Children.Add(TB("text-numbers", "1. Liste", () => SetList(ListKind.Number), "Nummerierung", para?.List == ListKind.Number, para is not null));
        {
            // F3: mehrstufige Liste (auch Tab / Umschalt+Tab) und Formatierung löschen
            Add(TB("text-list-out", "⇤", () => TextListLevel(-1), "Listenebene höher (Umschalt+Tab)", enabled: para is { List: not ListKind.None, ListLevel: > 0 }), false);
            Add(TB("text-list-in", "⇥", () => TextListLevel(1), "Listenebene tiefer (Tab) – 1. → a) → i. bzw. • → →", enabled: para is { List: not ListKind.None } && para.ListLevel < TextTools.MaxListLevel), false);
            Add(TB("text-clear-format", "A", TextClearFormat, "Formatierung löschen – Markiertes bzw. ganzen Absatz wieder schlicht (Links, Kommentare und Liste bleiben)", enabled: p is not null), false);
        }
        // T-0700: Ausrichtung mit Ausrichtungs-Symbolen statt Dreiecken („▶ Rechtsbündig“ sah aus wie Abspielen)
        foreach (var (a, kind, tip) in new[] { (Align.Left, "links", "Linksbündig"), (Align.Center, "mitte", "Zentriert"), (Align.Right, "rechts", "Rechtsbündig"), (Align.Justify, "block", "Blocksatz") })
        {
            var ab = TB("text-align-" + a.ToString().ToLowerInvariant(), "", () => TextEachPara(x => x.Align = a), tip, p?.Align == a, p is not null);
            ab.Content = GlyphIcons.AlignIcon(kind, ab);
            Add(ab, a != Align.Justify);
        }
        bar.Children.Add(new Border { Width = 10 });
        {
            Add(TB("text-find", "Suchen", () => _txFind = !_txFind, "Suchen und Ersetzen (Strg+F)", _txFind), false);
            Add(TB("text-assist", "Schreibhilfe", () => _txAssistOn = !_txAssistOn, "Trendo schlägt Verbesserungen vor – du sagst Ja oder Nein", _txAssistOn), false);
            Add(TB("text-spell", "Rechtschreibung", () => { _txSpellOn = !_txSpellOn; if (_txSpellOn) RunSpelling(); else _txMistakes = []; }, "Rechtschreibung prüfen", _txSpellOn), false);
        }
        if (one) _txOverflow.Add(TextPainterButton()); // „Format übertragen“ steht sonst in der Vorlagen-Galerie
        bar.Children.Add(TB("text-more", _txMore ? "Weniger" : "Mehr …", () => _txMore = !_txMore, one ? "Alle weiteren Werkzeuge: PDF, Drucken, Schriftart, Suchen, Rechtschreibung, Tabelle, Bild …" : "Alle weiteren Funktionen"));
        return bar;
    }

    /// <summary>F3: Listenebene des aktuellen Absatzes ändern.</summary>
    public void TextListLevel(int delta) { if (CurPara() is not { } p || p.List == ListKind.None) return; var before = TextDocument.Copy(); if (TextTools.ChangeListLevel(p, delta)) { _txUndo.Add(before); _txRedo.Clear(); Changed(); } }
    /// <summary>F3: Formatierung löschen – Markierung, sonst der ganze Absatz.</summary>
    public void TextClearFormat()
    {
        var v = _txView;
        if (v.HasSelection && !v.Anchor.SameBox(v.Caret)) { Snapshot(); TextEdit.Apply(TextDocument, v.Anchor, v.Caret, (p, s, l) => { if (l > 0) TextTools.ClearFormat(p, s, l); }); Changed(); return; }
        if (CurBox() is not { } cp) return;
        Snapshot(); TextTools.ClearFormat(cp, _txSel, _txSelLen); TextEdit.Commit(TextDocument, v.Caret, cp); Changed();
    }

    private void SetList(ListKind k) { if (CurPara() is not { } p) return; var to = p.List == k ? ListKind.None : k; TextEachPara(x => x.List = to); }

    private Control TextMoreBar()
    {
        var bar = new WrapPanel { IsVisible = _txMore, Name = "text-more-bar" };
        foreach (var over in _txOverflow) bar.Children.Add(over); // V-20: was in „Einfach“ nicht in der einen Zeile steht
        var p = CurPara();
        var saveAs = new Button { Name = "text-saveas", Content = "Speichern unter …", Focusable = false, Margin = new Thickness(0, 0, 4, 4), Padding = new Thickness(9, 4) };
        saveAs.Click += async (_, _) => { if (await PickText(true, Path.GetFileName(TextPath ?? DefaultTextName(".docx"))) is { } f) { try { TextSaveAs(f); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { _txMsg = "Nicht gespeichert: " + Trendo.Core.FriendlyError.Say(ex); } } Show("text"); };
        bar.Children.Add(saveAs);
        var recent = TextRecent();
        if (recent.Count > 0)
        {
            var rb = new Button { Name = "text-recent", Content = "Zuletzt", Focusable = false, Margin = new Thickness(0, 0, 4, 4), Padding = new Thickness(9, 4) };
            var m = new ContextMenu();
            for (var i = 0; i < recent.Count; i++) { var f = recent[i]; var mi = new MenuItem { Header = Path.GetFileName(f), Name = "text-recent-" + i }; mi.Click += (_, _) => { TextOpen(f); Show("text"); }; m.Items.Add(mi); }
            rb.ContextMenu = m; rb.Click += (_, _) => m.Open(rb); bar.Children.Add(rb);
        }
        bar.Children.Add(TB("text-cut", "Ausschneiden", () => _txView.Cut(), "Ausschneiden (Strg+X)"));
        bar.Children.Add(TB("text-copy", "Kopieren", () => _txView.Copy(), "Kopieren (Strg+C)"));
        bar.Children.Add(TB("text-paste", "Einfügen", () => _txView.Paste(), "Einfügen (Strg+V) – aus Trendo Text mit Format, von außen als reiner Text"));
        bar.Children.Add(TB("text-selectall", "Alles markieren", () => _txView.SelectAll(), "Das ganze Dokument markieren (Strg+A)"));
        bool Has(Fmt f) => TextIsOn(f);
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
            if (!CanManageSoftware) { OpenPdf(f); Show("pdf"); _txMsg = NurErwachseneDrucken; _core.Audit.Append(CurrentPrincipal.Name, "rbac.denied", "User fehlt ManageSoftware: drucken " + Path.GetFileName(f)); return; } // T-0764 (7. Prüfrunde): nie über Windows
            if (sumatra is not null && !_opt.Isolated) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(sumatra, $"-print-dialog \"{f}\"") { UseShellExecute = false });
            else Launch(f);
            _txMsg = "Druckansicht geöffnet – dort Drucker wählen.";
            _core.Audit.Append(Principal.LocalOwner.Name, "text.print", Path.GetFileName(f));
        }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception or InvalidOperationException) { _txMsg = "Drucken ging nicht: " + Trendo.Core.FriendlyError.Say(ex); }
    }

    // ---------- Seite ----------
    private TextPageView? _txViewField; private ScrollViewer? _txScrollView; private TextBlock? _txStatus;
    private string _txLastKind = "", _txBarSig = ""; private DateTime _txLastEdit; private TextPos _txLastCaret;
    private Avalonia.Threading.DispatcherTimer? _txLater;

    /// <summary>T-0692: Das Schreibfeld – ein Steuerelement über das ganze Dokument. Es bleibt über alle Neuaufbauten der Seite dasselbe (Cursor, Auswahl und Rollstand bleiben).</summary>
    public TextPageView TextView => _txViewField ??= NewTextView();
    private TextPageView _txView => TextView;

    private TextPageView NewTextView()
    {
        var v = new TextPageView { Name = "text-edit", DocSource = () => TextDocument };
        v.Changing += kind =>
        {
            // Tippen und Löschen am Stück ergeben einen Rückgängig-Schritt; alles andere je einen
            var run = kind is "tippen" or "rueck" or "entf" && kind == _txLastKind && v.Caret == _txLastCaret && !v.HasSelection && (DateTime.UtcNow - _txLastEdit).TotalSeconds < 2 && _txUndo.Count > 0;
            if (!run) Snapshot();
            _txLastKind = kind;
        };
        v.Changed += () => { TextDirty = true; _txLastEdit = DateTime.UtcNow; _txLastCaret = v.Caret; TextStatusNow(); TextLater(); };
        v.Moved += () => { TextStatusNow(); TextLater(); };
        v.Shortcut = TextShortcut;
        v.MenuWanted += () => v.ContextMenu = TextParaMenu(TextFocusBlock);
        v.Mistakes = () => _txMistakes.Select(m => (m.Block, m.Start, m.Length));
        return v;
    }

    /// <summary>Tastenkürzel aus dem Schreibfeld, die das Programm ausführt.</summary>
    private bool TextShortcut(Key key, KeyModifiers mods)
    {
        var ctrl = mods.HasFlag(KeyModifiers.Control) || mods.HasFlag(KeyModifiers.Meta);
        if (key == Key.Tab)
        {
            if (ctrl || _txView.Caret.InCell || CurPara() is not { List: not ListKind.None }) return false;
            TextListLevel(mods.HasFlag(KeyModifiers.Shift) ? -1 : 1); Show("text"); return true;
        }
        if (!ctrl) return false;
        var f = key switch { Key.B => Fmt.Bold, Key.I => Fmt.Italic, Key.U => Fmt.Underline, _ => Fmt.None };
        if (f != Fmt.None) TextToggle(f);
        else if (key == Key.Z) TextUndo();
        else if (key == Key.Y) TextRedo();
        else if (key == Key.F) _txFind = true;
        else if (key == Key.S) { try { TextSaveAs(TextPath ?? DefaultTextName(".docx")); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _txMsg = "Nicht gespeichert: " + Trendo.Core.FriendlyError.Say(ex); } }
        else return false;
        Show("text"); return true;
    }

    private string TextStatusText()
    {
        var c = TextDocument.Count();
        return $"{(TextPath is null ? "Noch nicht gespeichert" : Path.GetFileName(TextPath))}{(TextDirty ? " · geändert" : "")} · Seite {_txView.CaretPage} von {_txView.PageCount} · {c.Words} Wörter · {c.Characters} Zeichen · {c.Paragraphs} Absätze{(_txMsg.Length > 0 ? " · " + _txMsg : "")}";
    }

    private void TextStatusNow() { if (_txStatus is { } s && s.IsAttachedToVisualTree()) s.Text = TextStatusText(); }

    /// <summary>Was die Werkzeugzeile gerade anzeigt (Absatzart, Fett an/aus, Rückgängig möglich …) – ändert es sich, wird die Zeile neu gezeichnet.</summary>
    private string TextBarSignature()
    {
        var v = _txView; var p = TextEdit.ParaAt(TextDocument, v.Caret);
        var at = p is null ? 0 : Math.Clamp(v.Caret.Offset - 1, 0, Math.Max(0, p.Text.Length - 1));
        return $"{TextDocument.Blocks.ElementAtOrDefault(v.Caret.Block)?.GetType().Name}|{v.Caret.InCell}|{p?.Style}|{p?.Align}|{p?.List}|{p?.ListLevel}|{p?.LineSpacing}|{TextIsOn(Fmt.Bold)}{TextIsOn(Fmt.Italic)}{TextIsOn(Fmt.Underline)}|{(p is null || p.Text.Length == 0 ? "" : TextDoc.InkAt(p, at, MarkKind.Font) + "/" + p.At(at).Size + "/" + TextDoc.InkAt(p, at, MarkKind.Color))}|{_txUndo.Count > 0}{_txRedo.Count > 0}";
    }

    /// <summary>Kurz nach dem letzten Tastendruck: Rechtschreibung nachziehen und die Werkzeugzeile auffrischen, wenn sie etwas anderes zeigen müsste.</summary>
    private void TextLater()
    {
        _txLater ??= NewTextLater();
        _txLater.Stop(); _txLater.Start();
    }

    private Avalonia.Threading.DispatcherTimer NewTextLater()
    {
        var t = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(260) };
        t.Tick += (_, _) =>
        {
            t.Stop();
            if (CurrentView != "text") return;
            if (_txSpellOn && TextDirty) { RunSpelling(); _txView.InvalidateVisual(); }
            if (TextBarSignature() != _txBarSig && _txPrompt is null) Show("text");
        };
        return t;
    }

    private Control TextPaper()
    {
        var v = _txView;
        v.Zoom = _txZoom; v.Refresh(); v.ClampSelection();
        v.ContextMenu = TextParaMenu(TextFocusBlock);
        _txFootNos = TextTools.NoteNumbers(TextDocument, MarkKind.Footnote); _txEndNos = TextTools.NoteNumbers(TextDocument, MarkKind.Endnote);
        _txScrollView ??= new ScrollViewer { Name = "text-scroll", Content = v, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
        if (_txScrollView.Parent is Panel old) old.Children.Remove(_txScrollView);
        _txBarSig = TextBarSignature();
        if (_txPrompt is null) _ui.Post(() => { if (v.IsAttachedToVisualTree() && CurrentView == "text") v.Focus(); });
        return _txScrollView;
    }

    private void FocusPara(int index, int caret) { TextSelect(index, Math.Max(0, Math.Min(caret, (TextDocument.Blocks.ElementAtOrDefault(index) as Para)?.Text.Length ?? 0))); Show("text"); }

    /// <summary>Cursor in eine Tabellenzelle setzen (wie Hineinklicken) – öffentlich für Tests und Claude.</summary>
    public void TextSelectCell(int block, int row, int col, int start = 0, int length = 0)
    {
        _txView.Refresh();
        _txView.Select(new TextPos(block, start, row, col), new TextPos(block, start + length, row, col));
    }

    /// <summary>Die Zelle am Cursor mit Nachbarn verbinden: <paramref name="right"/> Spalten nach rechts und <paramref name="down"/> Zeilen nach unten dazu.</summary>
    public bool TextMergeCells(int right, int down)
    {
        var c = _txView.Caret;
        if (!c.InCell || TextDocument.Blocks[c.Block] is not Table t) { _txMsg = "Erst in eine Tabellenzelle klicken."; return false; }
        var (cs, rs) = t.SpanOf(c.Row, c.Col);
        var before = TextDocument.Copy();
        // der Nachbar kann selbst schon verbunden sein: dann wird er ganz mitgenommen
        var addC = 0; for (var k = 0; k < right && c.Col + cs + addC < t.Cols; k++) addC += t.SpanOf(c.Row, c.Col + cs + addC).ColSpan;
        var addR = 0; for (var k = 0; k < down && c.Row + rs + addR < t.Rows.Count; k++) addR += t.SpanOf(c.Row + rs + addR, c.Col).RowSpan;
        if (addC == 0 && addR == 0 || !t.Merge(c.Row, c.Col, cs + addC, rs + addR)) { _txMsg = "Das geht hier nicht – daneben ist keine Zelle frei oder sie ist anders verbunden."; return false; }
        _txUndo.Add(before); _txRedo.Clear(); Changed(); _txView.Refresh(); TextSelectCell(c.Block, c.Row, c.Col, 0);
        _txMsg = "Zellen verbunden."; return true;
    }

    /// <summary>Verbundene Zelle am Cursor wieder teilen.</summary>
    public bool TextUnmergeCell()
    {
        var c = _txView.Caret;
        if (!c.InCell || TextDocument.Blocks[c.Block] is not Table t) return false;
        var before = TextDocument.Copy();
        if (!t.Unmerge(c.Row, c.Col)) { _txMsg = "Diese Zelle ist nicht verbunden."; return false; }
        _txUndo.Add(before); _txRedo.Clear(); Changed(); _txMsg = "Verbindung gelöst."; return true;
    }

    /// <summary>Werkzeuge zum Baustein am Cursor: Tabelle (Zeilen, Spalten, Zellen verbinden), Bild (Größe, Beschriftung), Seitenumbruch und Verzeichnis (entfernen).</summary>
    private Control? TextBlockTools()
    {
        var index = TextFocusBlock; if (index < 0 || index >= TextDocument.Blocks.Count) return null;
        var row = new WrapPanel { Name = "text-block-tools" };
        void Label(string s) => row.Children.Add(new TextBlock { Text = s, Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 8, 4) });
        Button Delete(string what) => TB("text-block-delete-" + index, "✕ " + what, () => { Snapshot(); TextDocument.Blocks.RemoveAt(index); if (TextDocument.Blocks.Count == 0) TextDocument.Blocks.Add(new Para()); TextFocusBlock = Math.Clamp(index - 1, 0, TextDocument.Blocks.Count - 1); Changed(); }, "Entfernen");
        switch (TextDocument.Blocks[index])
        {
            case Table t:
                var c = _txView.Caret; var merged = c.InCell && t.SpanOf(c.Row, c.Col) != (1, 1);
                Label("Tabelle");
                row.Children.Add(TB("text-row-add-" + index, "+ Zeile", () => { Snapshot(); TextDoc.InsertRow(t, t.Rows.Count); Changed(); }, "Zeile anfügen"));
                row.Children.Add(TB("text-col-add-" + index, "+ Spalte", () => { Snapshot(); TextDoc.InsertColumn(t, t.Cols); Changed(); }, "Spalte anfügen"));
                row.Children.Add(TB("text-row-del-" + index, "− Zeile", () => { Snapshot(); TextDoc.DeleteRow(t, c.InCell ? c.Row : t.Rows.Count - 1); Changed(); }, "Zeile am Cursor entfernen"));
                row.Children.Add(TB("text-col-del-" + index, "− Spalte", () => { Snapshot(); TextDoc.DeleteColumn(t, c.InCell ? c.Col : t.Cols - 1); Changed(); }, "Spalte am Cursor entfernen"));
                row.Children.Add(TB("text-cell-merge-right-" + index, "Verbinden →", () => TextMergeCells(1, 0), "Zelle mit der rechten Nachbarzelle verbinden", enabled: c.InCell));
                row.Children.Add(TB("text-cell-merge-down-" + index, "Verbinden ↓", () => TextMergeCells(0, 1), "Zelle mit der Zelle darunter verbinden", enabled: c.InCell));
                row.Children.Add(TB("text-cell-unmerge-" + index, "Verbindung lösen", () => TextUnmergeCell(), "Verbundene Zelle wieder teilen", enabled: merged));
                row.Children.Add(TB("text-col-even-" + index, "Spalten gleich breit", () => { Snapshot(); t.Widths = []; Changed(); }, "Alle Spalten gleich breit – Breite sonst an der Spaltenlinie ziehen", enabled: t.Widths.Count > 0));
                row.Children.Add(Delete("Tabelle"));
                return row;
            case Picture pic:
                Label("Bild");
                row.Children.Add(TB("text-img-smaller-" + index, "kleiner", () => { Snapshot(); pic.HeightCm *= Math.Max(2, pic.WidthCm - 2) / pic.WidthCm; pic.WidthCm = Math.Max(2, pic.WidthCm - 2); Changed(); }, "Bild kleiner"));
                row.Children.Add(TB("text-img-bigger-" + index, "größer", () => { Snapshot(); var w = Math.Min(16, pic.WidthCm + 2); pic.HeightCm *= w / pic.WidthCm; pic.WidthCm = w; Changed(); }, "Bild größer"));
                row.Children.Add(TB("text-img-caption-" + index, "Beschriftung", () => TextAsk("text-prompt", "Bildbeschriftung („Abbildung n:“ setzt Trendo davor):", pic.Caption, v => { Snapshot(); pic.Caption = v.Trim(); Changed(); }), "Beschriftung unter dem Bild"));
                row.Children.Add(TB("text-img-alt-" + index, "Alternativtext", () => TextAsk("text-prompt", "Was zeigt das Bild? (für Bildschirmleser):", pic.Alt, v => { Snapshot(); pic.Alt = v.Trim(); Changed(); }), "Alternativtext für Barrierefreiheit", pic.Alt.Length > 0));
                row.Children.Add(Delete("Bild"));
                return row;
            case PageBreak: Label("Seitenumbruch"); row.Children.Add(Delete("Seitenumbruch")); return row;
            case TocBlock: Label("Inhaltsverzeichnis – Doppelklick auf einen Eintrag springt zur Überschrift"); row.Children.Add(Delete("Verzeichnis")); return row;
            default: return null;
        }
    }

    // ---------- Seitenleiste: Suchen/Ersetzen, Rechtschreibung ----------
    private Control? TextSidePanel()
    {
        var extra = TextProPanels().ToList();
        if (!_txFind && !_txSpellOn && !_txAssistOn && extra.Count == 0) return null;
        var sp = new StackPanel { Spacing = 6, Width = 260, Margin = new Thickness(6, 4, 0, 0) };
        foreach (var c in extra) sp.Children.Add(c);
        if (_txAssistOn) sp.Children.Add(Glass2(TextAssistPanel(), 10));
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
                if (h.Row < 0) TextSelect(h.Block, h.Start, h.Length); else TextSelectCell(h.Block, h.Row, h.Col, h.Start, h.Length);
                _txMsg = $"Treffer {_txHit + 1} von {_txHits.Count}"; Show("text");
            };
            var one = new Button { Name = "text-replace-one", Content = "Ersetzen" };
            one.Click += (_, _) =>
            {
                Grab();
                var (fs, fl) = (_txSel, _txSelLen); var fbox = _txView.Caret;
                if (CurBox() is { } p && fl > 0 && _txView.Anchor.SameBox(fbox) && fs + fl <= p.Text.Length && string.Equals(p.Text.Substring(fs, fl), _txFindQ, _txFindCase ? StringComparison.Ordinal : StringComparison.CurrentCultureIgnoreCase))
                { Snapshot(); TextDoc.ReplaceAt(p, fs, fl, _txFindR); TextEdit.Commit(TextDocument, fbox, p); Changed(); _txView.Select(fbox with { Offset = fs }, fbox with { Offset = fs + _txFindR.Length }); _txMsg = "Ersetzt."; }
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

    // ---------- Schreibhilfe (Masterprompt 131): Vorschläge mit Ja/Nein ----------
    private bool _txAssistOn;
    public bool TextAssistOn { get => _txAssistOn; set => _txAssistOn = value; }
    /// <summary>Offene Hinweise der Schreibhilfe (ohne die mit „Nein“ beantworteten).</summary>
    public List<WritingHint> TextHints()
    {
        if (!_spellTried) { _spellTried = true; _spelling = Spelling.Load(Spelling.Candidates(_deps.ToolsRoot), Path.Combine(_core.DataRoot, "text", "eigene-woerter.txt")); }
        return WritingAid.Check(TextDocument, _spelling).Where(h => !SuggestionDismissed(h.Key)).ToList();
    }

    private Control TextAssistPanel()
    {
        var hints = TextHints();
        var box = new StackPanel { Name = "text-assist-panel", Spacing = 6, Children = { H("Schreibhilfe", 13) } };
        if (hints.Count == 0) { box.Children.Add(P("Keine Vorschläge – der Text sieht gut aus ✓", Brush.Parse("#4ADE80"), 12)); return box; }
        box.Children.Add(P($"{hints.Count} Vorschläge · „Ja“ ändert die Stelle (Rückgängig mit Strg+Z), „Nein“ wird gemerkt.", Muted, 11));
        var items = hints.Select(h => (new Trendo.Core.Assist.Suggestion(h.Key, h.Title, h.Detail, h.Kind switch { "rechtschreibung" => "ABC", "lang" => "📏", "fuellwort" => "✂", "anfuehrung" => "„“", _ => "✎" }),
            (Action)(() =>
            {
                if (h.Replacement is null) { TextSelect(h.Block, h.Start, h.Length); _txMsg = "Langer Satz markiert – teile ihn an einer passenden Stelle."; return; }
                Snapshot(); if (WritingAid.Apply(TextDocument, h)) { Changed(); _txMsg = "Geändert: " + h.Title; } else _txMsg = "Die Stelle hat sich inzwischen geändert.";
            })));
        if (SuggestionCard("text", items) is { } card) box.Children.Add(card);
        return box;
    }
}
