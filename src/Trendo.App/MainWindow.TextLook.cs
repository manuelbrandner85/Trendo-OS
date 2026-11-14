using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core.Office;

namespace Trendo.App;

/// <summary>Pilot 0.9.47 (GODMODE V31–V34, Vorbild Word/Writer): Schriftart, Schriftgröße, Schriftfarbe, Hervorhebung, Zeilenabstand,
/// Abstand vor dem Absatz und Erstzeileneinzug im Menüband „Start“ – dazu ein Rechtsklick-Menü im Text wie in Schreibprogrammen.</summary>
public partial class MainWindow
{
    /// <summary>Schriften zur Auswahl: verbreitete Büroschriften zuerst, danach die auf dem Rechner installierten.</summary>
    public static readonly string[] TextFonts = ["Arial", "Calibri", "Cambria", "Georgia", "Times New Roman", "Verdana", "Segoe UI", "Tahoma", "Garamond", "Courier New"];
    public static readonly double[] TextSizes = [8, 9, 10, 11, 12, 14, 16, 18, 20, 24, 28, 32, 36, 48, 72];
    static readonly (string Hex, string Name)[] TextInkColors =
        [("#000000", "Schwarz"), ("#404040", "Dunkelgrau"), ("#C00000", "Dunkelrot"), ("#E03131", "Rot"), ("#E8590C", "Orange"), ("#2F9E44", "Grün"), ("#1971C2", "Blau"), ("#1F3864", "Dunkelblau"), ("#7048E8", "Lila"), ("#C2255C", "Beere")];
    static readonly (string Hex, string Name)[] TextMarkColors =
        [("#FFF59D", "Gelb"), ("#B9F6CA", "Grün"), ("#B3E5FC", "Türkis"), ("#F8BBD0", "Rosa"), ("#FFE0B2", "Orange"), ("#E0E0E0", "Grau")];

    IEnumerable<string> TextFontChoices()
    {
        var installed = new List<string>();
        try { installed = FontManager.Current.SystemFonts.Select(f => f.Name).Where(n => n.Length > 0 && !n.StartsWith('@')).Distinct().OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToList(); }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException) { } // ohne Schriftverwaltung (Testmodus) nur die Grundliste
        return TextFonts.Concat(installed.Where(n => !TextFonts.Contains(n, StringComparer.OrdinalIgnoreCase))).Take(160);
    }

    /// <summary>Bereich für Zeichenformate: die Auswahl – ohne Auswahl das Wort am Cursor (wie in Word).</summary>
    (int Start, int Length) TextInkRange(Para p)
    {
        if (_txSelLen > 0) return (_txSel, _txSelLen);
        var at = Math.Clamp(_txSel, 0, p.Text.Length);
        var a = at; while (a > 0 && char.IsLetterOrDigit(p.Text[a - 1])) a--;
        var e = at; while (e < p.Text.Length && char.IsLetterOrDigit(p.Text[e])) e++;
        return (a, e - a);
    }

    /// <summary>Schriftart, Schriftfarbe oder Hervorhebung auf die Auswahl setzen (null = zurück zum Standard). Öffentlich für Tests und Claude.</summary>
    public void TextSetInk(MarkKind kind, string? value)
    {
        if (CurPara() is not { } p) return;
        var (s, l) = TextInkRange(p); if (l == 0) { _txMsg = "Erst Text markieren – oder den Cursor in ein Wort setzen."; return; }
        Snapshot(); TextDoc.SetInk(p, s, l, kind, value); Changed();
    }

    /// <summary>Schriftgröße in Punkt auf die Auswahl (bzw. das Wort am Cursor).</summary>
    public void TextSetSize(double pt)
    {
        if (CurPara() is not { } p) return;
        var (s, l) = TextInkRange(p); if (l == 0) return;
        Snapshot(); TextDoc.Size(p, s, l, pt); Changed();
    }

    /// <summary>Absatzformat: Zeilenabstand (Faktor), Abstand vor/nach in Punkt, Erstzeileneinzug in cm.</summary>
    public void TextSetParagraph(double? lineSpacing = null, double? before = null, double? after = null, double? firstLineCm = null, bool resetLine = false)
    {
        if (CurPara() is not { } p) return;
        Snapshot();
        if (resetLine) p.LineSpacing = null; else if (lineSpacing is { } ls) p.LineSpacing = Math.Clamp(ls, 0.8, 4);
        if (before is { } b) p.SpaceBeforePt = Math.Clamp(b, 0, 96);
        if (after is { } a) p.SpaceAfterPt = Math.Clamp(a, 0, 96);
        if (firstLineCm is { } fl) p.FirstLineCm = Math.Clamp(fl, -5, 5);
        Changed();
    }

    /// <summary>Knöpfe für das Menüband „Start“: Schriftart, Größe, Farbe, Hervorhebung, Zeilen- und Absatzabstand.</summary>
    IEnumerable<Control> TextLookItems()
    {
        var p = CurPara();
        var at = p is null ? 0 : Math.Clamp(_txSel, 0, Math.Max(0, p.Text.Length - 1));
        var font = new ComboBox { Name = "text-font", Focusable = false, Width = 150, Margin = new Thickness(0, 0, 4, 4), IsEnabled = p is not null };
        var fonts = TextFontChoices().ToList(); foreach (var f in fonts) font.Items.Add(new ComboBoxItem { Content = f, FontFamily = new FontFamily(f) });
        var curFont = p is null ? null : TextDoc.InkAt(p, at, MarkKind.Font);
        font.SelectedIndex = Math.Max(0, fonts.FindIndex(f => f.Equals(curFont ?? "Arial", StringComparison.OrdinalIgnoreCase)));
        font.SelectionChanged += (_, _) => { if (font.SelectedItem is ComboBoxItem { Content: string name } && !name.Equals(curFont ?? "Arial", StringComparison.OrdinalIgnoreCase)) { TextSetInk(MarkKind.Font, name == "Arial" ? null : name); Show("text"); } };
        ToolTip.SetTip(font, "Schriftart"); Avalonia.Automation.AutomationProperties.SetName(font, "Schriftart");
        yield return font;
        var size = new ComboBox { Name = "text-size", Focusable = false, Width = 64, Margin = new Thickness(0, 0, 4, 4), IsEnabled = p is not null };
        foreach (var s in TextSizes) size.Items.Add(s.ToString("0"));
        var curSize = p is null ? 11 : p.At(at).Size ?? TextDoc.StyleSize(p.Style);
        size.SelectedIndex = Array.FindIndex(TextSizes, s => Math.Abs(s - curSize) < 0.01);
        size.SelectionChanged += (_, _) => { if (size.SelectedIndex >= 0 && Math.Abs(TextSizes[size.SelectedIndex] - curSize) > 0.01) { TextSetSize(TextSizes[size.SelectedIndex]); Show("text"); } };
        ToolTip.SetTip(size, "Schriftgröße (Punkt)"); Avalonia.Automation.AutomationProperties.SetName(size, "Schriftgröße");
        yield return size;
        var curColor = p is null ? null : TextDoc.InkAt(p, at, MarkKind.Color);
        yield return InkButton("text-color", "A", "Schriftfarbe", curColor ?? "#000000", MarkKind.Color, TextInkColors, "Automatisch (Standard)");
        var curMark = p is null ? null : TextDoc.InkAt(p, at, MarkKind.Highlight);
        yield return InkButton("text-highlight", "🖍", "Texthervorhebung", curMark ?? "#FFF59D", MarkKind.Highlight, TextMarkColors, "Keine Hervorhebung");
        var ls = p?.LineSpacing;
        yield return MenuButton("text-linespacing", "↕", ls is { } v ? v.ToString("0.0#") : "Zeilen", "Zeilenabstand",
            ("1,0 – einfach", () => TextSetParagraph(lineSpacing: 1)), ("1,15", () => TextSetParagraph(lineSpacing: 1.15)), ("1,5", () => TextSetParagraph(lineSpacing: 1.5)),
            ("2,0 – doppelt", () => TextSetParagraph(lineSpacing: 2)), ("Standard der Absatzart", () => TextSetParagraph(resetLine: true)));
        yield return MenuButton("text-paraspace", "¶", "Absatz", "Abstand vor/nach dem Absatz und Einzug der ersten Zeile",
            ("Abstand vor: 0 pt", () => TextSetParagraph(before: 0)), ("Abstand vor: 6 pt", () => TextSetParagraph(before: 6)), ("Abstand vor: 12 pt", () => TextSetParagraph(before: 12)),
            ("Abstand nach: 0 pt", () => TextSetParagraph(after: 0)), ("Abstand nach: 6 pt", () => TextSetParagraph(after: 6)), ("Abstand nach: 12 pt", () => TextSetParagraph(after: 12)),
            ("Erste Zeile: kein Einzug", () => TextSetParagraph(firstLineCm: 0)), ("Erste Zeile: 1,25 cm eingerückt", () => TextSetParagraph(firstLineCm: 1.25)), ("Hängend: 1,25 cm", () => TextSetParagraph(firstLineCm: -1.25)));
    }

    /// <summary>Farbknopf mit Farbfeld darunter (wie in Word); Klick öffnet die Palette.</summary>
    Control InkButton(string name, string glyph, string tip, string current, MarkKind kind, (string Hex, string Name)[] palette, string none)
    {
        var b = new Button { Name = name, Focusable = false, Margin = new Thickness(0, 0, 4, 4), Padding = new Thickness(8, 2), MinWidth = 34,
            Content = new StackPanel { Spacing = 1, Children = { new TextBlock { Text = glyph, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center },
                new Border { Height = 4, Width = 18, CornerRadius = new CornerRadius(1), Background = Brush.Parse(current) } } } };
        ToolTip.SetTip(b, tip); Avalonia.Automation.AutomationProperties.SetName(b, tip);
        var menu = new ContextMenu();
        var i = 0;
        foreach (var (hex, label) in palette)
        {
            var h = hex; var mi = new MenuItem { Name = $"{name}-{i++}", Header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(3), Background = Brush.Parse(hex), BorderBrush = Brush.Parse("#55000000"), BorderThickness = new Thickness(1) }, new TextBlock { Text = label } } } };
            mi.Click += (_, _) => { TextSetInk(kind, h); Show("text"); };
            menu.Items.Add(mi);
        }
        var off = new MenuItem { Name = $"{name}-none", Header = none }; off.Click += (_, _) => { TextSetInk(kind, null); Show("text"); }; menu.Items.Add(off);
        b.ContextMenu = menu; b.Click += (_, _) => menu.Open(b);
        return b;
    }

    /// <summary>Rechtsklick im Text: Ausschneiden, Kopieren, Einfügen, Formate, Absatzart, Link, Kommentar – und „✳ Claude fragen“ zur Markierung.</summary>
    ContextMenu TextParaMenu(int index)
    {
        var m = new ContextMenu { Name = "text-context" };
        MenuItem I(string name, string header, Action go) { var mi = new MenuItem { Name = name, Header = header }; mi.Click += (_, _) => { go(); Show("text"); }; return mi; }
        var p = TextDocument.Blocks.ElementAtOrDefault(index) as Para;
        string Selected() => p is null ? "" : _txSelLen > 0 && _txSel + _txSelLen <= p.Text.Length ? p.Text.Substring(_txSel, _txSelLen) : p.Text;
        if (ClaudeBelongs("text") && ClaudeStudioOf("text") is { } studio)
            m.Items.Add(I("text-context-claude", "Claude fragen", () => ClaudeContextAsk(Selected(), studio)));
        m.Items.Add(I("text-context-cut", "Ausschneiden", () => _txActive?.Cut()));
        m.Items.Add(I("text-context-copy", "Kopieren", () => _txActive?.Copy()));
        m.Items.Add(I("text-context-paste", "Einfügen", () => _txActive?.Paste()));
        m.Items.Add(new Separator());
        m.Items.Add(I("text-context-bold", "Fett", () => TextToggle(Fmt.Bold)));
        m.Items.Add(I("text-context-italic", "Kursiv", () => TextToggle(Fmt.Italic)));
        m.Items.Add(I("text-context-underline", "Unterstrichen", () => TextToggle(Fmt.Underline)));
        var colors = new MenuItem { Name = "text-context-color", Header = "Schriftfarbe" };
        foreach (var (hex, name) in TextInkColors) { var h = hex; colors.Items.Add(I("text-context-color-" + hex.TrimStart('#').ToLowerInvariant(), name, () => TextSetInk(MarkKind.Color, h))); }
        colors.Items.Add(I("text-context-color-auto", "Automatisch", () => TextSetInk(MarkKind.Color, null)));
        m.Items.Add(colors);
        var marks = new MenuItem { Name = "text-context-mark", Header = "Hervorheben" };
        foreach (var (hex, name) in TextMarkColors) { var h = hex; marks.Items.Add(I("text-context-mark-" + hex.TrimStart('#').ToLowerInvariant(), name, () => TextSetInk(MarkKind.Highlight, h))); }
        marks.Items.Add(I("text-context-mark-none", "Keine", () => TextSetInk(MarkKind.Highlight, null)));
        m.Items.Add(marks);
        var styles = new MenuItem { Name = "text-context-style", Header = "Absatzart" };
        foreach (var s in Enum.GetValues<ParaStyle>()) { var st = s; styles.Items.Add(I("text-context-style-" + s.ToString().ToLowerInvariant(), TextDoc.StyleName(s), () => { if (CurPara() is { } cp) { Snapshot(); cp.Style = st; Changed(); } })); }
        m.Items.Add(styles);
        m.Items.Add(new Separator());
        m.Items.Add(I("text-context-link", "Link einfügen …", () => TextAsk("text-link-ask", "Adresse des Links", "https://", v => TextAddMark(MarkKind.Link, v))));
        m.Items.Add(I("text-context-comment", "Kommentar …", () => TextAsk("text-comment-ask", "Kommentar", "", v => TextAddMark(MarkKind.Comment, v))));
        return m;
    }

    /// <summary>Schrift, Farbe und Hervorhebung eines Abschnitts im Blatt zeigen.</summary>
    static void TextApplyInk(Avalonia.Controls.Documents.Run run, Para p, int at)
    {
        if (TextDoc.InkAt(p, at, MarkKind.Font) is { } f) run.FontFamily = new FontFamily(f);
        if (TextDoc.InkAt(p, at, MarkKind.Color) is { } c) run.Foreground = Brush.Parse(c);
        if (TextDoc.InkAt(p, at, MarkKind.Highlight) is { } h) run.Background = Brush.Parse(h);
    }
}
