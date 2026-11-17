using System.Text;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using Trendo.Core;
using Trendo.Core.Text;

namespace Trendo.App;

/// <summary>T-0740 (CORE-12, Restliste Rang 15): KI-Anweisungen als Datei – eine Vorlage als Markdown mit Kopfzeilen, alle als JSON; Import aus .md/.txt/.json mit Rückfrage
/// bei gleichem Namen („Ersetzen“ legt eine neue Version an, „Als Kopie“ eine neue Vorlage). Dazu der Texteditor für den Anweisungstext: AvaloniaEdit (wie im Code-Editor)
/// mit Rückgängig im Feld (Strg+Z/Strg+Y) und hervorgehobenen Lücken „{{…}}“.</summary>
public partial class MainWindow
{
    /// <summary>Noch offene Einträge eines Imports; der erste wartet auf die Antwort „Ersetzen / Als Kopie / Überspringen“.</summary>
    readonly List<PromptFileEntry> _promptImportQueue = [];
    int _promptImported, _promptImportSkipped;
    /// <summary>Wartet ein Import auf die Rückfrage wegen eines gleichen Namens?</summary>
    public PromptFileEntry? PromptImportPending => _promptImportQueue.Count > 0 ? _promptImportQueue[0] : null;

    /// <summary>Datei einlesen und übernehmen; bei gleichem Namen wird gefragt (Karte über der Liste).</summary>
    public void PromptImportFile(string file)
    {
        try
        {
            if (!File.Exists(file)) throw new FileNotFoundException("Die Datei gibt es nicht.");
            if (new FileInfo(file).Length > PromptLibrary.MaxImportBytes) throw new InvalidDataException("Die Datei ist zu groß (höchstens 2 MB).");
            var entries = PromptLibrary.ParseFile(Path.GetFileName(file), File.ReadAllText(file));
            _promptImportQueue.Clear(); _promptImportQueue.AddRange(entries); _promptImported = 0; _promptImportSkipped = 0;
            PromptImportContinue();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        { _promptMsg = "Import ging nicht: " + ex.Message; }
        Show("ki-vorlagen");
    }

    /// <summary>Alle Einträge ohne Namensgleichheit übernehmen, beim ersten gleichen Namen anhalten und fragen.</summary>
    void PromptImportContinue()
    {
        while (_promptImportQueue.Count > 0)
        {
            var e = _promptImportQueue[0];
            if (Prompts.FindByName(e.Name) is not null) { _promptMsg = $"Es gibt schon eine Vorlage „{e.Name}“. Ersetzen oder als Kopie behalten?"; return; }
            var t = Prompts.Import(e); _promptImportQueue.RemoveAt(0); _promptImported++; _promptSel = t.Id; _promptEdit = false;
        }
        _promptMsg = _promptImported == 0 ? "Nichts übernommen." : $"✓ {_promptImported} Vorlage{(_promptImported == 1 ? "" : "n")} übernommen" + (_promptImportSkipped > 0 ? $", {_promptImportSkipped} übersprungen." : ".");
    }

    /// <summary>Antwort auf die Rückfrage: ersetzen, als Kopie oder (null) überspringen.</summary>
    public void PromptImportAnswer(PromptLibrary.ImportMode? mode)
    {
        if (_promptImportQueue.Count == 0) return;
        var e = _promptImportQueue[0]; _promptImportQueue.RemoveAt(0);
        if (mode is { } m) { var t = Prompts.Import(e, m); _promptImported++; _promptSel = t.Id; _promptEdit = false; }
        else _promptImportSkipped++;
        PromptImportContinue();
        Show("ki-vorlagen");
    }

    Control PromptImportCard(PromptFileEntry e)
    {
        var sp = new StackPanel { Name = "prompt-import-ask", Spacing = 6 };
        sp.Children.Add(H($"„{e.Name}“ gibt es schon", 14));
        sp.Children.Add(P("„Ersetzen“ macht den Text aus der Datei zur neuen Version – die bisherige bleibt unter „Frühere Versionen“. „Als Kopie“ legt eine zweite Vorlage an.", Muted, 11));
        var rep = new Button { Name = "prompt-import-replace", Content = "Ersetzen" };
        rep.Click += (_, _) => PromptImportAnswer(PromptLibrary.ImportMode.Replace);
        var copy = new Button { Name = "prompt-import-copy", Content = $"Als Kopie („{Prompts.CopyName(e.Name)}“)" };
        copy.Click += (_, _) => PromptImportAnswer(PromptLibrary.ImportMode.Copy);
        var skip = new Button { Name = "prompt-import-skip", Content = "Überspringen" };
        skip.Click += (_, _) => PromptImportAnswer(null);
        sp.Children.Add(new WrapPanel { Children = { rep, Gap(copy), Gap(skip) } });
        return WCard(sp);
        static Button Gap(Button b) { b.Margin = new Thickness(8, 0, 0, 0); return b; }
    }

    async Task PromptPickImport()
    {
        if (await PickMediaFile("prompt-import", "KI-Anweisung (.md, .txt, .json)", ["*.md", "*.txt", "*.json"]) is { } f) PromptImportFile(f);
    }

    /// <summary>Vorlage(n) in eine Datei schreiben: .md = genau diese Vorlage mit Kopfzeilen, .json = <paramref name="all"/> oder diese.</summary>
    public string PromptExportFile(string file, PromptTemplate? one, bool all)
    {
        var json = Path.GetExtension(file).Equals(".json", StringComparison.OrdinalIgnoreCase);
        if (!json && one is null) throw new InvalidOperationException("Für Markdown bitte eine Vorlage wählen.");
        var text = json ? PromptLibrary.ToJson(all || one is null ? Prompts.All() : [one]) : PromptLibrary.ToMarkdown(one!);
        AtomicFile.WriteAllText(file, text);
        _core.Audit.Append(Principal.LocalOwner.Name, "prompt.export", Path.GetFileName(file));
        return file;
    }

    async Task PromptPickExport(PromptTemplate? one, bool all)
    {
        var name = all || one is null ? "ki-anweisungen.json" : PromptFileName(one.Name) + ".md";
        string? f;
        if (PickOverride is { } fake) f = (await fake(new PickRequest(all ? "prompt-export-all" : "prompt-export", false, true, [Path.GetExtension(name)]))) is [var x, ..] ? x : null;
        else f = (await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = all ? "Alle KI-Anweisungen speichern" : "KI-Anweisung speichern", SuggestedFileName = name,
            DefaultExtension = Path.GetExtension(name).TrimStart('.'),
            FileTypeChoices = all ? [new FilePickerFileType("Sammlung (.json)") { Patterns = ["*.json"] }]
                : [new FilePickerFileType("Markdown mit Kopfzeilen (.md)") { Patterns = ["*.md"] }, new FilePickerFileType("JSON (.json)") { Patterns = ["*.json"] }] }))?.TryGetLocalPath();
        if (f is null) return;
        try { PromptExportFile(f, one, all); _promptMsg = $"✓ Gespeichert: {Path.GetFileName(f)}"; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) { _promptMsg = "Speichern ging nicht: " + ex.Message; }
        Show("ki-vorlagen");
    }

    static string PromptFileName(string name)
    {
        var s = new string(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c).ToArray()).Trim(' ', '.', '-');
        return s.Length == 0 ? "ki-anweisung" : s.Length > 60 ? s[..60] : s;
    }

    // ---------- Texteditor für den Anweisungstext ----------

    /// <summary>Hebt Lücken „{{Name}}“ und „{{Name|Standard}}“ golden hervor; ein „{{“ ohne Schluss wird rot unterlegt.</summary>
    sealed class PlaceholderColorizer : DocumentColorizingTransformer
    {
        static readonly Regex Rx = new(@"\{\{[^{}\n]*\}\}|\{\{", RegexOptions.Compiled);
        static readonly IBrush Gold = new SolidColorBrush(Color.Parse("#F5B83D")), GoldBg = new SolidColorBrush(Color.Parse("#33F5B83D")), BadBg = new SolidColorBrush(Color.Parse("#55F87171"));
        protected override void ColorizeLine(DocumentLine line)
        {
            var text = CurrentContext.Document.GetText(line);
            foreach (Match m in Rx.Matches(text))
            {
                var ok = m.Length > 2;
                ChangeLinePart(line.Offset + m.Index, line.Offset + m.Index + m.Length, el =>
                {
                    if (ok) { el.TextRunProperties.SetForegroundBrush(Gold); el.TextRunProperties.SetBackgroundBrush(GoldBg); el.TextRunProperties.SetTypeface(new Typeface(el.TextRunProperties.Typeface.FontFamily, FontStyle.Normal, FontWeight.SemiBold)); }
                    else el.TextRunProperties.SetBackgroundBrush(BadBg);
                });
            }
        }
    }

    /// <summary>Lücken im Text als Bereiche (Anfang, Länge) – dieselbe Regel wie die Hervorhebung (für Tests).</summary>
    public static IReadOnlyList<(int Start, int Length)> PromptPlaceholderSpans(string text) =>
        Regex.Matches(text, @"\{\{[^{}\n]*\}\}").Select(m => (m.Index, m.Length)).ToList();

    /// <summary>Editor für den Anweisungstext: Zeilenumbruch, Rückgängig im Feld, hervorgehobene Lücken, dunkle Fläche im Trendo-Look.</summary>
    TextEditor PromptTextEditor(string text)
    {
        var ed = new TextEditor { Name = "pedit-text", Document = new TextDocument(text), WordWrap = true, ShowLineNumbers = false, MinHeight = 160, MaxHeight = 420,
            FontFamily = new FontFamily("Segoe UI, Inter, Arial, sans-serif"), FontSize = 14, Background = Brush.Parse("#0E1117"), Foreground = Brush.Parse("#E6E6E6"),
            Padding = new Thickness(8, 6), BorderBrush = EdgeLit, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        ed.Options.EnableHyperlinks = false; ed.Options.EnableEmailHyperlinks = false; ed.Options.HighlightCurrentLine = false;
        ed.TextArea.SelectionBrush = Brush.Parse("#55F5B83D");
        ed.TextArea.TextView.LineTransformers.Add(new PlaceholderColorizer());
        return ed;
    }

    /// <summary>Wie <see cref="WriteMarksBar(TextBox, string, (WriteMarks.Kind, string, string)[])"/>, für den Editor: Auszeichnung auf die Markierung, als ein Rückgängig-Schritt.</summary>
    Control PromptMarksBar(TextEditor ed, string prefix, params (WriteMarks.Kind Kind, string Label, string Tip)[] marks)
    {
        var bar = new WrapPanel { Name = prefix + "-marks", Margin = new Thickness(0, 0, 0, 6) };
        foreach (var (kind, label, tip) in marks)
        {
            var b = new Button { Name = $"{prefix}-mark-{kind.ToString().ToLowerInvariant()}", Content = label, FontSize = 12.5, Padding = new Thickness(10, 4), Margin = new Thickness(0, 0, 6, 0), Focusable = false,
                Background = Brushes.Transparent, BorderBrush = Edge, BorderThickness = new Thickness(1), Foreground = Text };
            ToolTip.SetTip(b, tip);
            var k = kind;
            b.Click += (_, _) => ApplyPromptMark(ed, k);
            bar.Children.Add(b);
        }
        var undo = new Button { Name = prefix + "-text-undo", Content = "↶", FontSize = 13, Padding = new Thickness(8, 3), Margin = new Thickness(6, 0, 0, 0), Focusable = false, Background = Brushes.Transparent, BorderBrush = Edge, BorderThickness = new Thickness(1), Foreground = Text };
        ToolTip.SetTip(undo, "Im Text rückgängig (Strg+Z)");
        undo.Click += (_, _) => { if (ed.CanUndo) ed.Undo(); ed.TextArea.Focus(); };
        var redo = new Button { Name = prefix + "-text-redo", Content = "↷", FontSize = 13, Padding = new Thickness(8, 3), Margin = new Thickness(4, 0, 0, 0), Focusable = false, Background = Brushes.Transparent, BorderBrush = Edge, BorderThickness = new Thickness(1), Foreground = Text };
        ToolTip.SetTip(redo, "Im Text wiederholen (Strg+Y)");
        redo.Click += (_, _) => { if (ed.CanRedo) ed.Redo(); ed.TextArea.Focus(); };
        bar.Children.Add(undo); bar.Children.Add(redo);
        return bar;
    }

    public static void ApplyPromptMark(TextEditor ed, WriteMarks.Kind kind)
    {
        var start = ed.SelectionStart; var end = start + ed.SelectionLength;
        if (ed.SelectionLength == 0) start = end = ed.CaretOffset;
        var r = WriteMarks.Apply(ed.Text ?? "", start, end, kind);
        var doc = ed.Document; var old = doc.Text;
        // nur den geänderten Teil ersetzen (ein Rückgängig-Schritt, Rest des Textes bleibt unberührt)
        var pre = 0; while (pre < old.Length && pre < r.Text.Length && old[pre] == r.Text[pre]) pre++;
        var suf = 0; while (suf < old.Length - pre && suf < r.Text.Length - pre && old[^(suf + 1)] == r.Text[^(suf + 1)]) suf++;
        doc.Replace(pre, old.Length - pre - suf, r.Text.Substring(pre, r.Text.Length - pre - suf));
        ed.Select(r.SelStart, Math.Max(0, r.SelEnd - r.SelStart));
        ed.TextArea.Focus();
    }

    /// <summary>Rechtsklick im Editor: Lücke, Rückgängig, Ausschneiden/Kopieren/Einfügen.</summary>
    ContextMenu PromptEditorMenu(TextEditor ed)
    {
        MenuItem I(string name, string label, Action a, string keys = "") { var m = new MenuItem { Name = name, Header = label, InputGesture = keys.Length > 0 ? KeyGesture.Parse(keys) : null }; m.Click += (_, _) => a(); return m; }
        return new ContextMenu { Items = {
            I("pedit-ctx-placeholder", "Als Lücke einsetzen", () => ApplyPromptMark(ed, WriteMarks.Kind.Placeholder)),
            new Separator(),
            I("pedit-ctx-undo", "Rückgängig", () => { if (ed.CanUndo) ed.Undo(); }, "Ctrl+Z"),
            I("pedit-ctx-redo", "Wiederholen", () => { if (ed.CanRedo) ed.Redo(); }, "Ctrl+Y"),
            new Separator(),
            I("pedit-ctx-cut", "Ausschneiden", () => ed.Cut(), "Ctrl+X"),
            I("pedit-ctx-copy", "Kopieren", () => ed.Copy(), "Ctrl+C"),
            I("pedit-ctx-paste", "Einfügen", () => ed.Paste(), "Ctrl+V"),
            I("pedit-ctx-all", "Alles markieren", () => ed.SelectAll(), "Ctrl+A") } };
    }

    /// <summary>Datei auf die Seite ziehen: .md/.txt/.json wird als Vorlage importiert.</summary>
    void PromptDropTarget(Control c)
    {
        DragDrop.SetAllowDrop(c, true);
        c.AddHandler(DragDrop.DragOverEvent, (_, e) => { e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; });
        c.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            var f = e.DataTransfer.TryGetFiles()?.Select(x => x.TryGetLocalPath()).OfType<string>().FirstOrDefault(p => p.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
            if (f is null) return;
            e.Handled = true; PromptImportFile(f);
        });
    }
}
