using PdfSharp.Drawing;

namespace Trendo.Core.Office;

/// <summary>T-0692: Stelle im Dokument – Baustein und Zeichen; in Tabellen zusätzlich Zeile und Spalte der Zelle.</summary>
public readonly record struct TextPos(int Block, int Offset, int Row = -1, int Col = -1) : IComparable<TextPos>
{
    public bool InCell => Row >= 0;
    public bool SameBox(TextPos o) => Block == o.Block && Row == o.Row && Col == o.Col;
    public int CompareTo(TextPos o) => Block != o.Block ? Block.CompareTo(o.Block) : Row != o.Row ? Row.CompareTo(o.Row) : Col != o.Col ? Col.CompareTo(o.Col) : Offset.CompareTo(o.Offset);
    public static bool operator <(TextPos a, TextPos b) => a.CompareTo(b) < 0;
    public static bool operator >(TextPos a, TextPos b) => a.CompareTo(b) > 0;
    public static bool operator <=(TextPos a, TextPos b) => a.CompareTo(b) <= 0;
    public static bool operator >=(TextPos a, TextPos b) => a.CompareTo(b) >= 0;
}

public sealed record FlowFont(string Family, double Size, bool Bold, bool Italic);

// ---------- Was auf einer Seite steht (Maße in Punkt, Ursprung oben links) ----------
public abstract record FlowItem;
/// <summary>Text auf der Grundlinie Y.</summary>
public sealed record FlowText(double X, double Y, string Text, FlowFont Font, uint Color, bool Underline = false, bool Strike = false, double W = 0, uint Pen = 0xFF000000) : FlowItem;
public sealed record FlowFill(double X, double Y, double W, double H, uint Color) : FlowItem;
public sealed record FlowStroke(double X, double Y, double W, double H, uint Color, double Width) : FlowItem;
public sealed record FlowRule(double X1, double Y1, double X2, double Y2, uint Color, double Width) : FlowItem;
public sealed record FlowImage(Picture Pic, double X, double Y, double W, double H) : FlowItem;
/// <summary>Anklickbarer Link über einem Wort (Y = Grundlinie, H = Schriftgröße).</summary>
public sealed record FlowLink(double X, double Y, double W, double H, string Url) : FlowItem;
public sealed record FlowComment(double X, double Y, string Text) : FlowItem;
public sealed record FlowWatermark(string Text, double Size, string Family) : FlowItem;
public sealed record FlowOutline(string Title, bool Top) : FlowItem;
/// <summary>Kopf-/Fußzeile: Text in einem Kasten, Align 0 = links, 1 = mittig, 2 = rechts.</summary>
public sealed record FlowAligned(double X, double Y, double W, string Text, FlowFont Font, uint Color, int Align) : FlowItem;

/// <summary>Ein Stück einer Zeile: Zeichen [Start, Start+Length) des Absatzes; Text ist das gezeichnete Wort (ohne Leerzeichen dahinter).</summary>
public sealed record FlowSeg(int Start, int Length, double X, double W, double Adv, string Text, FlowFont Font);

public sealed class FlowLine
{
    public int Page, Block, Row = -1, Col = -1, Start, End;
    public double X, Y, H, Base, Left, Width;
    public List<FlowSeg> Segs = [];
    public TextPos Box => new(Block, 0, Row, Col);
}

public enum FlowBoxKind { Cell, Picture, Break, Toc, TocEntry }
public sealed record FlowBox(FlowBoxKind Kind, int Page, int Block, double X, double Y, double W, double H, int Row = -1, int Col = -1, int Target = -1);

public sealed class FlowPage { public List<FlowItem> Items { get; } = []; }

/// <summary>T-0692: Der Seitensatz von Trendo Text – ein Umbruch für Bildschirm und PDF. <see cref="Build"/> setzt das Dokument in Seiten;
/// <see cref="TextPdf"/> malt genau diese Seiten ins PDF, das Schreibfeld der Oberfläche zeigt dieselben Seiten und rechnet Cursor und Auswahl daraus.</summary>
public sealed class FlowDoc
{
    public List<FlowPage> Pages { get; } = [];
    public List<FlowLine> Lines { get; } = [];
    public List<FlowBox> Boxes { get; } = [];
    public Dictionary<int, int> HeadingPages { get; } = [];
    public double PageW { get; internal set; }
    public double PageH { get; internal set; }
    public string Title { get; internal set; } = "";
    Dictionary<(int, int, int), List<FlowLine>>? _byBox;

    /// <summary>Dokument setzen. Mit Inhaltsverzeichnis zwei Durchgänge (der erste ermittelt die Seiten der Überschriften).</summary>
    public static FlowDoc Build(TextDoc doc)
    {
        var first = new TextFlowLayout(doc, null).Run();
        return doc.Blocks.Any(b => b is TocBlock) ? new TextFlowLayout(doc, first.HeadingPages).Run() : first;
    }

    public IReadOnlyList<FlowLine> LinesOf(TextPos box)
    {
        if (_byBox is null)
        {
            _byBox = [];
            foreach (var l in Lines) { if (!_byBox.TryGetValue((l.Block, l.Row, l.Col), out var list)) _byBox[(l.Block, l.Row, l.Col)] = list = []; list.Add(l); }
        }
        return _byBox.TryGetValue((box.Block, box.Row, box.Col), out var res) ? res : [];
    }

    /// <summary>Zeile, in der die Stelle steht (am weichen Zeilenende: die nächste Zeile).</summary>
    public FlowLine? LineAt(TextPos pos)
    {
        var lines = LinesOf(pos); FlowLine? best = null;
        foreach (var l in lines) if (l.Start <= pos.Offset) best = l; else break;
        return best ?? (lines.Count > 0 ? lines[0] : null);
    }

    public double XAt(FlowLine l, int offset)
    {
        if (l.Segs.Count == 0) return l.X;
        foreach (var s in l.Segs)
        {
            if (offset <= s.Start) return s.X;
            if (offset < s.Start + s.Length)
            {
                var k = offset - s.Start;
                return k >= s.Text.Length ? s.X + s.W : s.X + FlowMeasure.Width(s.Text[..k], s.Font);
            }
        }
        var last = l.Segs[^1];
        return last.X + (offset >= last.Start + last.Length && last.Length > last.Text.Length ? last.Adv : last.W);
    }

    public int OffsetAt(FlowLine l, double x)
    {
        if (l.Segs.Count == 0 || x <= l.Segs[0].X) return l.Start;
        var res = -1;
        foreach (var s in l.Segs)
        {
            if (s.Length == 0 || x >= s.X + s.Adv) continue;
            if (s.Text.Length == 0) { res = x > s.X + s.Adv / 2 ? s.Start + s.Length : s.Start; break; } // Tabulator
            if (x >= s.X + s.W) { res = s.Start + (x > s.X + (s.W + s.Adv) / 2 ? s.Length : s.Text.Length); break; }
            var prev = 0.0; res = s.Start + s.Text.Length;
            for (var k = 1; k <= s.Text.Length; k++)
            {
                var w = FlowMeasure.Width(s.Text[..k], s.Font);
                if (x - s.X < (prev + w) / 2) { res = s.Start + k - 1; break; }
                prev = w;
            }
            break;
        }
        var last = l.Segs[^1];
        if (res < 0) res = last.Start + last.Length;
        // weicher Umbruch: hinter dem Leerzeichen am Zeilenende stünde der Cursor schon in der nächsten Zeile
        if (res >= l.End && !IsLastLine(l) && last.Length > last.Text.Length && last.Start + last.Length == l.End) res = l.End - 1;
        return Math.Clamp(res, l.Start, l.End);
    }

    bool IsLastLine(FlowLine l) { var ls = LinesOf(l.Box); return ls.Count == 0 || ReferenceEquals(ls[^1], l); }

    /// <summary>Cursor an der Stelle: Seite, x, oberer Rand und Höhe der Zeile.</summary>
    public (int Page, double X, double Y, double H)? Caret(TextPos pos) => LineAt(pos) is { } l ? (l.Page, XAt(l, pos.Offset), l.Y, l.H) : null;

    /// <summary>Stelle unter einem Punkt der Seite. Bilder, Seitenumbrüche und Verzeichnisse liefern die Stelle (Baustein, 0).</summary>
    public TextPos? Hit(int page, double x, double y)
    {
        foreach (var b in Boxes)
            if (b.Page == page && b.Kind is FlowBoxKind.Picture or FlowBoxKind.Break or FlowBoxKind.Toc && x >= b.X && x <= b.X + b.W && y >= b.Y && y <= b.Y + b.H) return new TextPos(b.Block, 0);
        var cell = Boxes.FirstOrDefault(b => b.Kind == FlowBoxKind.Cell && b.Page == page && x >= b.X && x <= b.X + b.W && y >= b.Y && y <= b.Y + b.H);
        FlowLine? best = null; var bestD = double.MaxValue;
        foreach (var l in Lines)
        {
            if (l.Page != page) continue;
            if (cell is not null ? l.Block != cell.Block || l.Row != cell.Row || l.Col != cell.Col : l.Row >= 0) continue;
            var dy = y < l.Y ? l.Y - y : y > l.Y + l.H ? y - l.Y - l.H : 0;
            var dx = x < l.Left ? l.Left - x : x > l.Left + l.Width ? x - l.Left - l.Width : 0; // mehrere Spalten: die Spalte unter dem Punkt
            var d = dy + dx * 4;
            if (d < bestD) { bestD = d; best = l; }
        }
        if (best is null) return cell is not null ? new TextPos(cell.Block, 0, cell.Row, cell.Col) : null;
        return new TextPos(best.Block, OffsetAt(best, x), best.Row, best.Col);
    }

    /// <summary>Eine Zeile höher (dir = -1) oder tiefer (dir = 1) an derselben x-Stelle; null am Dokumentrand.</summary>
    public TextPos? Vertical(TextPos pos, int dir, double wantX)
    {
        var cur = LineAt(pos); if (cur is null) return null;
        var idx = Lines.IndexOf(cur);
        TextPos On(FlowLine t) => new(t.Block, OffsetAt(t, wantX), t.Row, t.Col);
        TextPos? Into(FlowBox? cell, bool fromTop)
        {
            if (cell is null) return null;
            var ls = LinesOf(new TextPos(cell.Block, 0, cell.Row, cell.Col));
            return ls.Count == 0 ? new TextPos(cell.Block, 0, cell.Row, cell.Col) : On(fromTop ? ls[0] : ls[^1]);
        }
        List<FlowBox> CellsUnder(int block)
        {
            var all = Boxes.Where(b => b.Kind == FlowBoxKind.Cell && b.Block == block).ToList();
            var under = all.Where(b => wantX >= b.X - 0.5 && wantX <= b.X + b.W + 0.5).ToList();
            return under.Count > 0 ? under : all;
        }
        if (cur.Row >= 0)
        {
            // in der Zelle bleiben, dann in die Zelle darüber/darunter, dann aus der Tabelle heraus
            var own = LinesOf(cur.Box); var k = -1; for (var i = 0; i < own.Count; i++) if (ReferenceEquals(own[i], cur)) k = i;
            if (k + dir >= 0 && k + dir < own.Count) return On(own[k + dir]);
            var me = Boxes.FirstOrDefault(b => b.Kind == FlowBoxKind.Cell && b.Block == cur.Block && b.Row == cur.Row && b.Col == cur.Col);
            if (me is not null)
            {
                var next = dir > 0
                    ? CellsUnder(cur.Block).Where(b => b.Page > me.Page || b.Page == me.Page && b.Y >= me.Y + me.H - 0.5).OrderBy(b => b.Page).ThenBy(b => b.Y).FirstOrDefault()
                    : CellsUnder(cur.Block).Where(b => b.Page < me.Page || b.Page == me.Page && b.Y + b.H <= me.Y + 0.5).OrderByDescending(b => b.Page).ThenByDescending(b => b.Y).FirstOrDefault();
                if (Into(next, dir > 0) is { } inside) return inside;
            }
            for (var i = idx + dir; i >= 0 && i < Lines.Count; i += dir) if (Lines[i].Block != cur.Block) return Lines[i].Row >= 0 ? EnterTable(Lines[i].Block) : On(Lines[i]);
            return null;
        }
        var j = idx + dir; if (j < 0 || j >= Lines.Count) return null;
        return Lines[j].Row >= 0 ? EnterTable(Lines[j].Block) : On(Lines[j]);

        TextPos? EnterTable(int block)
        {
            var cells = CellsUnder(block);
            return Into(dir > 0 ? cells.OrderBy(b => b.Page).ThenBy(b => b.Y).FirstOrDefault() : cells.OrderByDescending(b => b.Page).ThenByDescending(b => b.Y + b.H).FirstOrDefault(), dir > 0);
        }
    }
}

/// <summary>Textbreiten in Punkt – dieselbe Messung für Bildschirm und PDF (PDFsharp-Schriftmaße).</summary>
public static class FlowMeasure
{
    static readonly object Gate = new();
    static XGraphics? g;
    static readonly Dictionary<FlowFont, XFont> Fonts = [];
    static readonly Dictionary<(string, bool, bool), bool> Known = [];
    static readonly Dictionary<(string, FlowFont), double> Widths = [];

    static FlowMeasure() => System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Sys.PdfTools).TypeHandle); // Schriften einrichten

    static XFontStyleEx Style(bool bold, bool italic) => (bold ? XFontStyleEx.Bold : XFontStyleEx.Regular) | (italic ? XFontStyleEx.Italic : XFontStyleEx.Regular);

    /// <summary>Gibt es die Schrift (in diesem Schnitt)?</summary>
    public static bool Has(string family, bool bold, bool italic)
    {
        lock (Gate)
        {
            if (Known.TryGetValue((family, bold, italic), out var ok)) return ok;
            try { _ = new XFont(family, 10, Style(bold, italic)); ok = true; } catch (Exception ex) when (ex is InvalidOperationException or ArgumentException) { ok = false; }
            return Known[(family, bold, italic)] = ok;
        }
    }

    public static XFont Font(FlowFont f)
    {
        lock (Gate)
        {
            if (Fonts.TryGetValue(f, out var x)) return x;
            try { x = new XFont(f.Family, f.Size, Style(f.Bold, f.Italic)); } catch (Exception ex) when (ex is InvalidOperationException or ArgumentException) { x = new XFont("Arial", f.Size, Style(f.Bold, f.Italic)); }
            if (Fonts.Count > 4000) Fonts.Clear();
            return Fonts[f] = x;
        }
    }

    public static double Width(string s, FlowFont f)
    {
        if (s.Length == 0) return 0;
        lock (Gate)
        {
            if (Widths.TryGetValue((s, f), out var w)) return w;
            g ??= XGraphics.CreateMeasureContext(new XSize(2000, 2000), XGraphicsUnit.Point, XPageDirection.Downwards);
            w = g.MeasureString(s, Font(f)).Width;
            if (Widths.Count > 60000) Widths.Clear();
            return Widths[(s, f)] = w;
        }
    }
}

/// <summary>Der eigentliche Satz (aus T9/T-0230 <see cref="TextPdf"/> herausgelöst): Seite einrichten, Spalten, Zeilenumbruch nach Wörtern, gemischte Zeichenformate,
/// Absatzarten, Listen, Einzug, Abstand, Ausrichtung, Tabellen (mit Zellformaten und verbundenen Zellen), Bilder, Seitenumbrüche, Inhaltsverzeichnis, Fuß- und Endnoten.</summary>
sealed class TextFlowLayout(TextDoc doc, Dictionary<int, int>? tocPages)
{
    const double Cm = 72 / 2.54;
    const uint Black = 0xFF000000, Gray = 0xFF808080, InsColor = 0xFF15803D, DelColor = 0xFFB91C1C, LinkColor = 0xFF0563C1, Soft = 0xFF595959;

    sealed record Word(string Text, FlowFont Font, bool Underline, bool Strike, double Rise, double W, bool SpaceAfter, uint? Color, string? Link, string? Comment, bool Highlight, uint? Mark, int Src, int SrcLen);
    sealed class LineBuf { public readonly List<Word> Words = []; public int Start, End; }

    readonly PageSetup page = doc.Page;
    readonly double pageW = (doc.Page.Landscape ? 29.7 : 21) * Cm, pageH = (doc.Page.Landscape ? 21 : 29.7) * Cm;
    double Top => page.Top * Cm; double Bottom => page.Bottom * Cm; double Left => page.Left * Cm;
    double Body => pageW - (page.Left + page.Right) * Cm;
    int Cols => Math.Clamp(page.Columns, 1, 3);
    const double Gap = 0.8 * Cm;
    double ColW => (Body - Gap * (Cols - 1)) / Cols;
    readonly FlowDoc flow = new();
    FlowPage cur = null!; double y; int col; bool started;
    double footReserve; readonly List<(int No, string Text)> pageNotes = [];
    readonly List<(int No, string Text)> endnotes = [];
    int footNo, endNo, figure;
    readonly string family = FlowMeasure.Has(doc.Page.Font, false, false) ? doc.Page.Font : "Arial";
    uint Heading => ParseColor(page.HeadingColor);
    int PageIndex => flow.Pages.Count - 1;

    double X0 => Left + col * (ColW + Gap);
    double Limit => pageH - Bottom - footReserve;

    static double M(string s, FlowFont f) => FlowMeasure.Width(s, f);
    static double Sp(FlowFont f) => FlowMeasure.Width(" ", f);
    void Add(FlowItem i) => cur.Items.Add(i);

    public FlowDoc Run()
    {
        flow.PageW = pageW; flow.PageH = pageH;
        flow.Title = doc.Title ?? doc.Paras.FirstOrDefault(p => p.Text.Trim().Length > 0)?.Text.Trim() ?? "";
        NewPage();
        var labels = TextTools.ListLabels(doc); var hasH1 = false; // F3: Nummern und Zeichen je Listenebene
        for (var bi = 0; bi < doc.Blocks.Count; bi++)
        {
            switch (doc.Blocks[bi])
            {
                case PageBreak:
                    flow.Boxes.Add(new FlowBox(FlowBoxKind.Break, PageIndex, bi, X0, y, ColW, Math.Max(14, Limit - y)));
                    NewPage(); break;
                case TocBlock toc: DrawToc(toc, bi); break;
                case Para p:
                    if (p.Style is ParaStyle.Heading1 or ParaStyle.Heading2 or ParaStyle.Heading3 && p.Text.Trim().Length > 0)
                    {
                        Need(TextDoc.StyleSize(p.Style) * 1.6 + 10); // Überschrift nicht allein am Seitenende
                        flow.HeadingPages[bi] = flow.Pages.Count;
                        // Lesezeichen wie LibreOffice: Überschrift 1 oben, 2 und 3 darunter
                        var top = p.Style == ParaStyle.Heading1 || !hasH1; hasH1 = true;
                        Add(new FlowOutline(p.Text.Trim().Replace('\n', ' '), top));
                    }
                    DrawPara(p, labels[bi] ?? "", bi);
                    break;
                case Table t: DrawTable(t, bi); break;
                case Picture pic: DrawPicture(pic, bi); break;
            }
        }
        FlushFootnotes();
        if (endnotes.Count > 0)
        {
            Need(40); y += 12;
            Text("Anmerkungen", F(13, true), Heading); y += 6;
            foreach (var (no, t) in endnotes) foreach (var line in Wrap($"{ToRoman(no)}  {t}", F(9.5), ColW)) { Need(13); Add(new FlowText(X0, y + 10, line, F(9.5), Black)); y += 13; }
        }
        // Kopf-/Fußzeile, Seitenzahlen
        var pages = flow.Pages.Count;
        for (var i = 0; i < pages; i++)
        {
            var small = F(8.5); var items = flow.Pages[i].Items;
            if (page.Header.Length > 0) items.Add(new FlowAligned(Left, Top * 0.45, Body, page.Header, small, Soft, 2));
            var fy = pageH - Bottom * 0.55;
            if (page.Footer.Length > 0) items.Add(new FlowAligned(Left, fy, Body, page.Footer, small, Soft, 0));
            if (page.PageNumbers) items.Add(new FlowAligned(Left, fy, Body, $"Seite {i + 1} von {pages}", small, Soft, page.Footer.Length > 0 ? 2 : 1));
        }
        return flow;
    }

    // ---------- Seiten und Spalten ----------
    void NewPage()
    {
        if (started) FlushFootnotes();
        started = true;
        cur = new FlowPage(); flow.Pages.Add(cur); y = Top; col = 0; footReserve = 0;
        if (page.Watermark.Length > 0) Add(new FlowWatermark(page.Watermark, Math.Min(110, 900.0 / Math.Max(4, page.Watermark.Length)), family));
        if (page.PageBorder) Add(new FlowStroke(0.9 * Cm, 0.9 * Cm, pageW - 1.8 * Cm, pageH - 1.8 * Cm, Gray, 0.8));
    }

    /// <summary>Platz für h Punkte? Sonst nächste Spalte oder Seite.</summary>
    void Need(double h)
    {
        if (y + h <= Limit || y <= Top + 0.1) return;
        if (col + 1 < Cols) { col++; y = Top; return; }
        NewPage();
    }

    void FlushFootnotes()
    {
        if (pageNotes.Count == 0) return;
        var fy = pageH - Bottom - footReserve + 4;
        Add(new FlowRule(Left, fy, Left + 4 * Cm, fy, Gray, 0.5)); fy += 3;
        foreach (var (no, t) in pageNotes) foreach (var line in Wrap($"{no}  {t}", F(8.5), Body)) { Add(new FlowText(Left, fy + 9, line, F(8.5), Black)); fy += 11; }
        pageNotes.Clear();
    }

    FlowFont F(double size, bool bold = false, bool italic = false) => new(family, size, bold, italic);

    /// <summary>Gleiche Größe und Auszeichnung in einer anderen Schrift; fehlt die Schrift, bleibt die bisherige.</summary>
    static FlowFont FontIn(string fam, FlowFont like) => FlowMeasure.Has(fam, like.Bold, like.Italic) ? like with { Family = fam } : like;

    void Text(string s, FlowFont f, uint color) { var h = f.Size * 1.3; Need(h); Add(new FlowText(X0, y + f.Size, s, f, color)); y += h; }

    FlowFont Font(Para p, Fmt f, double? size, double? baseSize)
    {
        var pt = size ?? baseSize ?? TextDoc.StyleSize(p.Style);
        if (f.HasFlag(Fmt.Super) || f.HasFlag(Fmt.Sub)) pt *= 0.62;
        return F(pt, f.HasFlag(Fmt.Bold) || p.Style is ParaStyle.Title or ParaStyle.Heading1 or ParaStyle.Heading2 or ParaStyle.Heading3, f.HasFlag(Fmt.Italic) || p.Style == ParaStyle.Quote);
    }

    uint ColorOf(Para p) => p.Style switch
    {
        ParaStyle.Title or ParaStyle.Heading1 or ParaStyle.Heading2 or ParaStyle.Heading3 => Heading,
        ParaStyle.Subtitle => Soft, ParaStyle.Quote => 0xFF404040, _ => Black,
    };

    // ---------- Absätze ----------
    /// <summary>Absatz in Zeilen brechen. availOf liefert die Breite der n-ten Zeile; baseSize ersetzt die Größe der Absatzart (Tabellenzellen).</summary>
    (List<LineBuf> Lines, List<(int No, Word Ref, string Text)> Notes) Break(Para p, Func<int, double> availOf, double? baseSize, bool withNotes)
    {
        var lines = new List<LineBuf>(); var line = new LineBuf(); var lineW = 0.0;
        var notesHere = new List<(int No, Word Ref, string Text)>();
        var len = p.Text.Length;
        // Abschnitte nach Format und Markierungen; Fußnotenzeichen als hochgestellte Zahl
        var cuts = new SortedSet<int> { 0, len };
        foreach (var s in p.Spans) { cuts.Add(Math.Clamp(s.Start, 0, len)); cuts.Add(Math.Clamp(s.End, 0, len)); }
        foreach (var m in p.Marks) { cuts.Add(Math.Clamp(m.Start, 0, len)); cuts.Add(Math.Clamp(m.End, 0, len)); }
        var pts = cuts.ToList();
        void Wrap(int nextStart) { line.End = nextStart; lines.Add(line); line = new LineBuf { Start = nextStart }; lineW = 0; }
        void AddText(string text, int src, Fmt f, double? size, uint? color, string? link, string? comment, bool hl, string? fam = null, uint? mark = null, bool note = false)
        {
            var font = fam is null ? Font(p, f, size, baseSize) : FontIn(fam, Font(p, f, size, baseSize));
            var ptSize = size ?? baseSize ?? TextDoc.StyleSize(p.Style);
            var rise = f.HasFlag(Fmt.Super) ? -0.35 * ptSize : f.HasFlag(Fmt.Sub) ? 0.15 * ptSize : 0;
            bool ul = f.HasFlag(Fmt.Underline) || link is not null, st = f.HasFlag(Fmt.Strike);
            Word W(string t, double w, bool space, int at, int n) => new(t, font, ul, st, rise, w, space, color, link, comment, hl, mark, at, n);
            var i = 0;
            while (i < text.Length)
            {
                var avail = availOf(lines.Count);
                var ch = text[i];
                if (ch == '\n' && !note) { line.End = src + i; lines.Add(line); line = new LineBuf { Start = src + i + 1 }; lineW = 0; i++; continue; }
                if (ch == '\t' && !note)
                {
                    var tw = Sp(font) * 4;
                    if (lineW + tw > avail && line.Words.Count > 0) Wrap(src + i);
                    line.Words.Add(W("", tw, false, src + i, 1)); lineW += tw; i++; continue;
                }
                var j = i; while (j < text.Length && text[j] != ' ' && (note || text[j] != '\n' && text[j] != '\t')) j++;
                var space = j < text.Length && text[j] == ' ';
                var word = text[i..j]; var off = 0;
                var w = M(word, font); var sw = space ? Sp(font) : 0;
                if (lineW + w > avail && line.Words.Count > 0) { Wrap(note ? src : src + i); avail = availOf(lines.Count); }
                while (w > avail && word.Length - off > 1) // überlange Wörter hart teilen
                {
                    var n = word.Length - off; while (n > 1 && M(word.Substring(off, n), font) > avail) n--;
                    line.Words.Add(W(word.Substring(off, n), M(word.Substring(off, n), font), false, note ? src : src + i + off, note ? 0 : n));
                    off += n; Wrap(note ? src : src + i + off); avail = availOf(lines.Count); w = M(word[off..], font);
                }
                line.Words.Add(W(word[off..], w, space, note ? src : src + i + off, note ? 0 : j - i - off + (space ? 1 : 0)));
                lineW += w + sw; i = j + (space ? 1 : 0);
            }
        }
        void Notes(int at)
        {
            if (!withNotes) return;
            foreach (var m in p.Marks.Where(m => m.Length == 0 && m.Start == at && m.Kind is MarkKind.Footnote or MarkKind.Endnote))
            {
                var foot = m.Kind == MarkKind.Footnote; var no = foot ? ++footNo : ++endNo;
                AddText(foot ? no.ToString() : ToRoman(no), at, Fmt.Super, null, null, null, null, false, note: true);
                if (foot) notesHere.Add((no, line.Words[^1], m.Value)); else endnotes.Add((no, m.Value));
            }
        }
        Notes(0);
        for (var k = 0; k + 1 < pts.Count; k++)
        {
            int x = pts[k], e = pts[k + 1]; if (e <= x) continue;
            var (f, size) = p.At(x);
            var link = p.Marks.FirstOrDefault(m => m.Kind == MarkKind.Link && m.Start <= x && m.End >= e)?.Value;
            var comment = p.Marks.FirstOrDefault(m => m.Kind == MarkKind.Comment && m.Length > 0 && m.Start <= x && m.End >= e)?.Value;
            uint? color = link is not null ? LinkColor : TextDoc.InkAt(p, x, MarkKind.Color) is { } cHex ? ParseColor(cHex) : null;
            uint? mark = TextDoc.InkAt(p, x, MarkKind.Highlight) is { } hHex ? ParseColor(hHex) : null;
            if (p.Marks.Any(m => m.Kind == MarkKind.Insert && m.Start <= x && m.End >= e)) { color = InsColor; f |= Fmt.Underline; }
            if (p.Marks.Any(m => m.Kind == MarkKind.Delete && m.Start <= x && m.End >= e)) { color = DelColor; f |= Fmt.Strike; }
            AddText(p.Text[x..e], x, f, size, color, link, comment, comment is not null, TextDoc.InkAt(p, x, MarkKind.Font), mark);
            Notes(e);
        }
        line.End = len; lines.Add(line);
        return (lines, notesHere);
    }

    /// <summary>Eine Zeile setzen: Wörter zeichnen und die Zeile für Cursor und Auswahl merken.</summary>
    void Place(LineBuf ln, bool last, Align align, double x0, double avail, double top, double lh, int block, int row, int col2, uint baseColor, HashSet<string> placedComments)
    {
        var words = ln.Words;
        var total = words.Sum(w => w.W) + words.Take(Math.Max(0, words.Count - 1)).Sum(w => w.SpaceAfter ? Sp(w.Font) : 0);
        var x = x0 + align switch { Align.Center => (avail - total) / 2, Align.Right => avail - total, _ => 0 };
        var gaps = words.Take(Math.Max(0, words.Count - 1)).Count(w => w.SpaceAfter);
        var extra = align == Align.Justify && !last && gaps > 0 ? (avail - total) / gaps : 0;
        var fl = new FlowLine { Page = PageIndex, Block = block, Row = row, Col = col2, Start = ln.Start, End = ln.End, X = x, Y = top, H = lh, Base = top + lh * 0.78, Left = x0, Width = avail };
        foreach (var w in words)
        {
            var by = top + lh * 0.78 + w.Rise;
            var adv = w.W + (w.SpaceAfter ? Sp(w.Font) + extra : 0);
            if (w.Mark is { } mk) Add(new FlowFill(x, top + lh * 0.12, adv, lh * 0.82, mk));
            if (w.Highlight) Add(new FlowFill(x, top + lh * 0.12, adv, lh * 0.82, 0x6EFFE678));
            if (w.Text.Length > 0 || w.Underline || w.Strike) Add(new FlowText(x, by, w.Text, w.Font, w.Color ?? baseColor, w.Underline, w.Strike, w.W, w.Color ?? Black));
            if (w.Link is { } url && Uri.TryCreate(url, UriKind.Absolute, out _)) Add(new FlowLink(x, by, Math.Max(1, w.W), w.Font.Size, url));
            if (w.Comment is { } c && placedComments.Add(c)) Add(new FlowComment(Math.Min(pageW - 20, X0 + ColW + 4), top, c));
            fl.Segs.Add(new FlowSeg(w.Src, w.SrcLen, x, w.W, adv, w.Text, w.Font));
            x += adv;
        }
        flow.Lines.Add(fl);
    }

    void DrawPara(Para p, string label, int block)
    {
        var indent = p.IndentCm * Cm + (p.List != ListKind.None ? 0.9 * Cm + p.ListLevel * TextTools.ListLevelCm * Cm : p.Style == ParaStyle.Quote ? 1 * Cm : 0);
        var avail = ColW - indent - (p.Style == ParaStyle.Quote ? 1 * Cm : 0);
        // Einzug der ersten Zeile (positiv) bzw. hängender Einzug (negativ: alle weiteren Zeilen rücken ein)
        double firstOff = Math.Min(Math.Max(0, p.FirstLineCm) * Cm, avail * 0.6), restOff = Math.Min(Math.Max(0, -p.FirstLineCm) * Cm, avail * 0.6);
        double Off(int li) => li == 0 ? firstOff : restOff;
        y += p.SpaceBeforePt ?? (p.Style is ParaStyle.Heading1 or ParaStyle.Heading2 or ParaStyle.Heading3 ? 10.0 : 0);
        var baseColor = ColorOf(p);
        var (lines, notesHere) = Break(p, li => avail - Off(li), null, true);
        var baseSize = TextDoc.StyleSize(p.Style);
        var placedComments = new HashSet<string>();
        for (var li = 0; li < lines.Count; li++)
        {
            var ln = lines[li];
            var lh = Math.Max(baseSize, ln.Words.Count == 0 ? 0 : ln.Words.Max(w => w.Font.Size)) * 1.32 * (p.LineSpacing ?? 1.0);
            // Fußnoten dieser Zeile brauchen unten Platz
            var lineNotes = notesHere.Where(n => ln.Words.Contains(n.Ref)).ToList();
            var noteH = lineNotes.Sum(n => Wrap(n.Text, F(8.5), Body).Count * 11.0) + (pageNotes.Count == 0 && lineNotes.Count > 0 ? 8 : 0);
            Need(lh + noteH);
            foreach (var n in lineNotes) pageNotes.Add((n.No, n.Text));
            footReserve += noteH;
            if (li == 0 && p.List != ListKind.None) Add(new FlowText(X0 + indent - 0.75 * Cm, y + baseSize, label, Font(p, Fmt.None, null, null), Black));
            Place(ln, li == lines.Count - 1, p.Align, X0 + indent + Off(li), avail - Off(li), y, lh, block, -1, -1, baseColor, placedComments);
            y += lh;
        }
        y += p.SpaceAfterPt ?? (p.Style == ParaStyle.Title ? 10 : 6);
    }

    static string ToRoman(int n) { var vals = new[] { 10, 9, 5, 4, 1 }; var syms = new[] { "x", "ix", "v", "iv", "i" }; var s = ""; for (var i = 0; i < vals.Length; i++) while (n >= vals[i]) { s += syms[i]; n -= vals[i]; } return s; }

    // ---------- Inhaltsverzeichnis ----------
    void DrawToc(TocBlock toc, int block)
    {
        y += 6;
        var startPage = PageIndex; var startY = y;
        Text(toc.Title, F(16, true), Heading); y += 4;
        foreach (var e in TextTools.Toc(doc))
        {
            var f = F(e.Level == 1 ? 11 : 10.5, e.Level == 1);
            var h = f.Size * 1.5; Need(h);
            var ind = (e.Level - 1) * 0.6 * Cm; var no = tocPages is not null && tocPages.TryGetValue(e.Block, out var pg) ? pg.ToString() : "…";
            var nw = M(no, f); var avail = ColW - ind - nw - 6;
            var text = e.Text; while (text.Length > 1 && M(text, f) > avail - 10) text = text[..^1];
            Add(new FlowText(X0 + ind, y + f.Size, text, f, Black));
            var tw = M(text, f); var dots = "";
            while (M(dots + " .", f) < avail - tw - 4) dots += " .";
            Add(new FlowText(X0 + ind + tw + 2, y + f.Size, dots, f, Gray));
            Add(new FlowText(X0 + ColW - nw, y + f.Size, no, f, Black));
            flow.Boxes.Add(new FlowBox(FlowBoxKind.TocEntry, PageIndex, block, X0, y, ColW, h, Target: e.Block));
            y += h;
        }
        if (PageIndex == startPage) flow.Boxes.Add(new FlowBox(FlowBoxKind.Toc, startPage, block, X0, startY, ColW, Math.Max(20, y - startY)));
        else flow.Boxes.Add(new FlowBox(FlowBoxKind.Toc, startPage, block, X0, startY, ColW, 16 * 1.3));
        y += 8;
    }

    // ---------- Tabellen und Bilder ----------
    void DrawTable(Table t, int block)
    {
        var cols = Math.Max(1, t.Cols); var n = t.Rows.Count; if (n == 0) return;
        var fr = t.Fractions(); var xs = new double[cols + 1];
        for (var c = 0; c < cols; c++) xs[c + 1] = xs[c] + fr[c] * ColW;
        var rowH = new double[n];
        var cells = new List<(int R, int C, int Cs, int Rs, Para P, List<LineBuf> Lines, double[] Lh, double H)>();
        for (var r = 0; r < n; r++)
        {
            rowH[r] = 13 + 6;
            for (var c = 0; c < cols; c++)
            {
                if (t.IsCovered(r, c)) continue;
                var (cs, rs) = t.SpanOf(r, c); cs = Math.Min(cs, cols - c); rs = Math.Min(rs, n - r);
                var w = xs[c + cs] - xs[c];
                var p = t.CellPara(r, c);
                var lines = Break(p, _ => w - 6, 10, false).Lines;
                var lhs = lines.Select(ln => Math.Max(10, ln.Words.Count == 0 ? 0 : ln.Words.Max(x => x.Font.Size)) * 1.3).ToArray();
                var h = lhs.Sum() + 6;
                cells.Add((r, c, cs, rs, p, lines, lhs, h));
                if (rs == 1) rowH[r] = Math.Max(rowH[r], h);
            }
        }
        foreach (var cell in cells.Where(x => x.Rs > 1))
        {
            var sum = 0.0; for (var r = cell.R; r < cell.R + cell.Rs; r++) sum += rowH[r];
            if (cell.H > sum) rowH[cell.R + cell.Rs - 1] += cell.H - sum;
        }
        var none = new HashSet<string>();
        for (var r0 = 0; r0 < n;)
        {
            // Zeilen, die über verbundene Zellen zusammenhängen, bleiben auf einer Seite
            var end = r0 + 1;
            for (var grown = true; grown;) { grown = false; foreach (var cell in cells) if (cell.R >= r0 && cell.R < end && cell.R + cell.Rs > end) { end = cell.R + cell.Rs; grown = true; } }
            var groupH = 0.0; for (var r = r0; r < end; r++) groupH += rowH[r];
            Need(groupH);
            var yr = y;
            for (var r = r0; r < end; r++)
            {
                foreach (var cell in cells.Where(x => x.R == r))
                {
                    var x = X0 + xs[cell.C]; var w = xs[cell.C + cell.Cs] - xs[cell.C];
                    var h = 0.0; for (var k = r; k < r + cell.Rs; k++) h += rowH[k];
                    Add(new FlowStroke(x, yr, w, h, Gray, 0.5));
                    flow.Boxes.Add(new FlowBox(FlowBoxKind.Cell, PageIndex, block, x, yr, w, h, cell.R, cell.C));
                    var ty = yr + 3;
                    for (var li = 0; li < cell.Lines.Count; li++)
                    {
                        Place(cell.Lines[li], li == cell.Lines.Count - 1, cell.P.Align, x + 3, w - 6, ty, cell.Lh[li], block, cell.R, cell.C, Black, none);
                        ty += cell.Lh[li];
                    }
                }
                yr += rowH[r];
            }
            y += groupH; r0 = end;
        }
        y += 8;
    }

    static bool LooksLikeImage(byte[] d) =>
        d.Length > 8 && (d[0] == 0x89 && d[1] == 0x50 || d[0] == 0xFF && d[1] == 0xD8 || d[0] == 0x47 && d[1] == 0x49 || d[0] == 0x42 && d[1] == 0x4D);

    void DrawPicture(Picture pic, int block)
    {
        if (LooksLikeImage(pic.Data))
        {
            var w = Math.Min(ColW, pic.WidthCm * Cm); var h = w * (pic.HeightCm / Math.Max(0.1, pic.WidthCm));
            var maxH = pageH - Top - Bottom - 30;
            if (h > maxH) { h = maxH; w = h * pic.WidthCm / pic.HeightCm; }
            Need(h + 6); Add(new FlowImage(pic, X0, y, w, h));
            flow.Boxes.Add(new FlowBox(FlowBoxKind.Picture, PageIndex, block, X0, y, w, h));
            y += h + 8;
        }
        else
        {
            Need(14); Add(new FlowText(X0, y + 10, "[Bild konnte nicht gezeichnet werden]", F(9), Gray));
            flow.Boxes.Add(new FlowBox(FlowBoxKind.Picture, PageIndex, block, X0, y, ColW, 14));
            y += 16;
        }
        if (pic.Caption.Trim().Length > 0)
        {
            var f = F(9, false, true);
            foreach (var line in Wrap($"Abbildung {++figure}: {pic.Caption.Trim()}", f, ColW)) { Need(12); Add(new FlowText(X0, y + 9, line, f, Soft)); y += 12; }
            y += 6;
        }
    }

    static List<string> Wrap(string text, FlowFont font, double width)
    {
        var lines = new List<string>();
        foreach (var part in text.Split('\n'))
        {
            var line = "";
            foreach (var w in part.Split(' '))
            {
                var test = line.Length == 0 ? w : line + " " + w;
                if (M(test, font) > width && line.Length > 0) { lines.Add(line); line = w; } else line = test;
            }
            lines.Add(line);
        }
        return lines;
    }

    internal static uint ParseColor(string hex)
    {
        var h = hex.TrimStart('#'); if (h.Length == 6) h = "FF" + h;
        return uint.TryParse(h, System.Globalization.NumberStyles.HexNumber, null, out var v) ? v : Black;
    }
}
