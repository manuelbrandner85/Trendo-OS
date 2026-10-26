using System.Globalization;
using System.Text;

namespace Trendo.Core.Office;

/// <summary>T-0223 Trendo Text (Stufe 1): eigenes Dokumentmodell statt LibreOffice Writer. Ein Dokument ist eine Folge von Bausteinen –
/// Absatz (mit Absatzart, Ausrichtung, Liste und Zeichenformaten), Tabelle, Bild, Seitenumbruch. Lesen/Schreiben: <see cref="DocxFormat"/>, <see cref="OdtFormat"/>.</summary>
public enum ParaStyle { Text, Title, Subtitle, Heading1, Heading2, Heading3, Quote }
public enum Align { Left, Center, Right, Justify }
public enum ListKind { None, Bullet, Number }

[Flags]
public enum Fmt { None = 0, Bold = 1, Italic = 2, Underline = 4, Strike = 8, Super = 16, Sub = 32 }

/// <summary>Zeichenformat für einen Textbereich. Size in Punkt (null = Größe der Absatzart).</summary>
public sealed record Span(int Start, int Length, Fmt Fmt, double? Size = null)
{
    public int End => Start + Length;
}

public abstract class Block { public abstract Block Copy(); }

public sealed class Para : Block
{
    public string Text { get; set; } = "";
    public ParaStyle Style { get; set; }
    public Align Align { get; set; }
    public ListKind List { get; set; }
    public List<Span> Spans { get; set; } = [];
    public Para() { }
    public Para(string text, ParaStyle style = ParaStyle.Text) { Text = text; Style = style; }
    public override Block Copy() => new Para { Text = Text, Style = Style, Align = Align, List = List, Spans = [.. Spans] };

    /// <summary>Format und Größe an einer Zeichenposition.</summary>
    public (Fmt Fmt, double? Size) At(int i)
    {
        var f = Fmt.None; double? size = null;
        foreach (var s in Spans) if (i >= s.Start && i < s.End) { f |= s.Fmt; size = s.Size ?? size; }
        return (f, size);
    }

    /// <summary>Text in Abschnitte gleichen Formats zerlegen (zum Zeichnen und Speichern).</summary>
    public IEnumerable<(string Text, Fmt Fmt, double? Size)> Runs()
    {
        if (Text.Length == 0) yield break;
        var start = 0; var cur = At(0);
        for (var i = 1; i <= Text.Length; i++)
        {
            var next = i < Text.Length ? At(i) : default;
            if (i == Text.Length || next != cur) { yield return (Text[start..i], cur.Fmt, cur.Size); start = i; cur = next; }
        }
    }

    /// <summary>Spans aus Abschnitten neu bilden (nach dem Ändern, zusammengefasst und ohne leere Bereiche).</summary>
    internal void SetRuns(IEnumerable<(string Text, Fmt Fmt, double? Size)> runs)
    {
        var sb = new StringBuilder(); Spans = [];
        foreach (var (t, f, size) in runs)
        {
            if (t.Length == 0) continue;
            if ((f != Fmt.None || size is not null) && Spans.Count > 0 && Spans[^1] is { } last && last.End == sb.Length && last.Fmt == f && last.Size == size)
                Spans[^1] = last with { Length = last.Length + t.Length };
            else if (f != Fmt.None || size is not null) Spans.Add(new Span(sb.Length, t.Length, f, size));
            sb.Append(t);
        }
        Text = sb.ToString();
    }
}

public sealed class Table : Block
{
    public List<List<string>> Rows { get; set; } = [];
    public Table() { }
    public Table(int rows, int cols) { for (var r = 0; r < rows; r++) Rows.Add(Enumerable.Repeat("", cols).ToList()); }
    public int Cols => Rows.Count == 0 ? 0 : Rows.Max(r => r.Count);
    public override Block Copy() => new Table { Rows = Rows.Select(r => r.ToList()).ToList() };
}

public sealed class Picture : Block
{
    public byte[] Data { get; set; } = [];
    /// <summary>Dateiendung ohne Punkt: png, jpg, gif.</summary>
    public string Ext { get; set; } = "png";
    public double WidthCm { get; set; } = 12;
    public double HeightCm { get; set; } = 8;
    public override Block Copy() => new Picture { Data = Data, Ext = Ext, WidthCm = WidthCm, HeightCm = HeightCm };
}

public sealed class PageBreak : Block { public override Block Copy() => new PageBreak(); }

public sealed record WordCount(int Words, int Characters, int CharactersNoSpaces, int Paragraphs);

public sealed class TextDoc
{
    public List<Block> Blocks { get; set; } = [];
    public string? Title { get; set; }

    public static TextDoc Empty() => new() { Blocks = [new Para()] };
    public TextDoc Copy() => new() { Blocks = Blocks.Select(b => b.Copy()).ToList(), Title = Title };
    public IEnumerable<Para> Paras => Blocks.OfType<Para>();

    /// <summary>Ganzer Text (Absätze, Tabellenzellen mit Tab, Zeilen) – für TXT, Suche und Zählen.</summary>
    public string PlainText()
    {
        var sb = new StringBuilder();
        foreach (var b in Blocks)
            switch (b)
            {
                case Para p: sb.Append(p.List switch { ListKind.Bullet => "• ", _ => "" }).Append(p.Text).Append('\n'); break;
                case Table t: foreach (var r in t.Rows) sb.Append(string.Join('\t', r)).Append('\n'); break;
                case PageBreak: sb.Append('\f'); break;
            }
        return sb.ToString().TrimEnd('\n');
    }

    // ---------- T3 Zeichenformate ----------
    /// <summary>Format im Bereich ein- oder ausschalten. Hoch- und Tiefstellen schließen sich aus.</summary>
    public static void Format(Para p, int start, int length, Fmt f, bool on)
    {
        (start, length) = Clamp(p, start, length); if (length == 0) return;
        p.SetRuns(Split(p, start, length).Select(x => x.Inside ? (x.Text, Apply(x.Fmt, f, on), x.Size) : (x.Text, x.Fmt, x.Size)).ToList());
    }

    static Fmt Apply(Fmt cur, Fmt f, bool on)
    {
        if (!on) return cur & ~f;
        if (f.HasFlag(Fmt.Super)) cur &= ~Fmt.Sub;
        if (f.HasFlag(Fmt.Sub)) cur &= ~Fmt.Super;
        return cur | f;
    }

    /// <summary>Ist das Format im ganzen Bereich gesetzt? (Für Umschalten: alles fett → wieder normal.)</summary>
    public static bool Has(Para p, int start, int length, Fmt f)
    {
        (start, length) = Clamp(p, start, length);
        if (length == 0) return start < p.Text.Length && p.At(start).Fmt.HasFlag(f);
        for (var i = start; i < start + length; i++) if (!p.At(i).Fmt.HasFlag(f)) return false;
        return true;
    }

    public static void Toggle(Para p, int start, int length, Fmt f) => Format(p, start, length, f, !Has(p, start, length, f));

    /// <summary>Schriftgröße im Bereich (Punkt); null = zurück zur Größe der Absatzart.</summary>
    public static void Size(Para p, int start, int length, double? size)
    {
        (start, length) = Clamp(p, start, length); if (length == 0) return;
        if (size is { } s && (s < 4 || s > 200)) throw new ArgumentException("Schriftgröße zwischen 4 und 200 Punkt.");
        p.SetRuns(Split(p, start, length).Select(x => x.Inside ? (x.Text, x.Fmt, size) : (x.Text, x.Fmt, x.Size)).ToList());
    }

    /// <summary>Größe um Schritte wie in Writer ändern (8, 9, 10, 11, 12, 14, 16, 18, 20, 24, 28, 32, 36, 48, 72).</summary>
    public static void Grow(Para p, int start, int length, bool bigger)
    {
        double[] steps = [8, 9, 10, 11, 12, 14, 16, 18, 20, 24, 28, 32, 36, 48, 72];
        var cur = p.At(Math.Clamp(start, 0, Math.Max(0, p.Text.Length - 1))).Size ?? StyleSize(p.Style);
        var next = bigger ? steps.FirstOrDefault(x => x > cur, 96) : steps.LastOrDefault(x => x < cur, 6);
        Size(p, start, length, next);
    }

    public enum Case { Upper, Lower, Title }

    /// <summary>GROSS, klein oder Erster Buchstabe Groß – Formate bleiben erhalten.</summary>
    public static void ChangeCase(Para p, int start, int length, Case c)
    {
        (start, length) = Clamp(p, start, length); if (length == 0) return;
        var de = CultureInfo.GetCultureInfo("de-DE");
        string Map(string s) => c switch { Case.Upper => s.ToUpper(de), Case.Lower => s.ToLower(de), _ => de.TextInfo.ToTitleCase(s.ToLower(de)) };
        // Abschnittsweise ersetzen, Länge bleibt (ß → SS würde verschieben: dann ß behalten)
        p.SetRuns(Split(p, start, length).Select(x => x.Inside ? (Keep(x.Text, Map(x.Text)), x.Fmt, x.Size) : (x.Text, x.Fmt, x.Size)).ToList());
        static string Keep(string a, string b) => a.Length == b.Length ? b : string.Concat(a.Select(ch => ch == 'ß' ? "ß" : char.ToUpper(ch, CultureInfo.GetCultureInfo("de-DE")).ToString()));
    }

    static (int, int) Clamp(Para p, int start, int length)
    {
        start = Math.Clamp(start, 0, p.Text.Length);
        return (start, Math.Clamp(length, 0, p.Text.Length - start));
    }

    static List<(string Text, Fmt Fmt, double? Size, bool Inside)> Split(Para p, int start, int length)
    {
        var res = new List<(string, Fmt, double?, bool)>(); var pos = 0;
        foreach (var (t, f, size) in p.Runs())
        {
            var a = pos; var b = pos + t.Length; pos = b;
            var cut = new[] { a, Math.Clamp(start, a, b), Math.Clamp(start + length, a, b), b };
            for (var k = 0; k < 3; k++)
                if (cut[k + 1] > cut[k]) res.Add((t[(cut[k] - a)..(cut[k + 1] - a)], f, size, k == 1));
        }
        return res;
    }

    /// <summary>Neuer Absatztext aus dem Eingabefeld: Formate wandern mit (gemeinsamer Anfang und Ende bleiben, Eingefügtes erbt das Format davor).</summary>
    public static void EditText(Para p, string text)
    {
        var old = p.Text; if (old == text) return;
        var pre = 0; while (pre < old.Length && pre < text.Length && old[pre] == text[pre]) pre++;
        var suf = 0; while (suf < old.Length - pre && suf < text.Length - pre && old[^(suf + 1)] == text[^(suf + 1)]) suf++;
        var removed = old.Length - pre - suf; var inserted = text.Length - pre - suf;
        var spans = new List<Span>();
        foreach (var s in p.Spans)
        {
            int Map(int i) => i <= pre ? i : i >= pre + removed ? i - removed + inserted : pre;
            var a = Map(s.Start); var b = Map(s.End);
            if (s.Start < pre && s.End >= pre && s.End <= pre + removed && inserted > 0) b = pre + inserted; // Tippen am Ende eines Formats erweitert es
            if (b > a) spans.Add(s with { Start = a, Length = b - a });
        }
        p.Text = text; p.Spans = spans;
        p.SetRuns(p.Runs().ToList()); // normalisieren
    }

    /// <summary>Enter: Absatz an der Stelle teilen. Der neue Absatz bekommt den Rest samt Formaten; nach Überschriften geht es mit „Text“ weiter, Listen laufen weiter.</summary>
    public Para SplitAt(int block, int at)
    {
        var p = (Para)Blocks[block];
        at = Math.Clamp(at, 0, p.Text.Length);
        var parts = Split(p, at, p.Text.Length - at);
        var next = new Para { Style = p.Style is ParaStyle.Text or ParaStyle.Quote ? p.Style : ParaStyle.Text, Align = p.Align, List = p.List };
        next.SetRuns(parts.Where(x => x.Inside).Select(x => (x.Text, x.Fmt, x.Size)));
        p.SetRuns(parts.Where(x => !x.Inside).Select(x => (x.Text, x.Fmt, x.Size)));
        if (p.Text.Length == 0 && p.List != ListKind.None && next.Text.Length == 0) { p.List = ListKind.None; } // leerer Listenpunkt + Enter beendet die Liste
        Blocks.Insert(block + 1, next);
        return next;
    }

    /// <summary>Rücktaste am Absatzanfang: mit dem vorigen Absatz verbinden. Liefert die Stelle, an der der Cursor im vorigen Absatz steht (oder -1).</summary>
    public int MergeWithPrevious(int block)
    {
        if (block <= 0 || Blocks[block] is not Para p) return -1;
        if (Blocks[block - 1] is not Para prev)
        {
            if (Blocks[block - 1] is PageBreak) { Blocks.RemoveAt(block - 1); return 0; }
            return -1;
        }
        var at = prev.Text.Length;
        prev.SetRuns(prev.Runs().Concat(p.Runs()).ToList());
        Blocks.RemoveAt(block);
        return at;
    }

    public static double StyleSize(ParaStyle s) => s switch
    {
        ParaStyle.Title => 26, ParaStyle.Subtitle => 16, ParaStyle.Heading1 => 18, ParaStyle.Heading2 => 15, ParaStyle.Heading3 => 13, _ => 11,
    };

    public static string StyleName(ParaStyle s) => s switch
    {
        ParaStyle.Title => "Titel", ParaStyle.Subtitle => "Untertitel", ParaStyle.Heading1 => "Überschrift 1", ParaStyle.Heading2 => "Überschrift 2",
        ParaStyle.Heading3 => "Überschrift 3", ParaStyle.Quote => "Zitat", _ => "Text",
    };

    // ---------- T5 Tabellen ----------
    public static void InsertRow(Table t, int at) { t.Rows.Insert(Math.Clamp(at, 0, t.Rows.Count), Enumerable.Repeat("", Math.Max(1, t.Cols)).ToList()); }
    public static void InsertColumn(Table t, int at) { foreach (var r in t.Rows) r.Insert(Math.Clamp(at, 0, r.Count), ""); }
    public static void DeleteRow(Table t, int at) { if (t.Rows.Count > 1 && at >= 0 && at < t.Rows.Count) t.Rows.RemoveAt(at); }
    public static void DeleteColumn(Table t, int at) { if (t.Cols > 1) foreach (var r in t.Rows) if (at >= 0 && at < r.Count) r.RemoveAt(at); }

    // ---------- T7 Suchen, Ersetzen, Zählen ----------
    public sealed record Hit(int Block, int Row, int Col, int Start, int Length);

    /// <summary>Fundstellen in Absätzen und Tabellenzellen (Row/Col = -1 bei Absätzen).</summary>
    public List<Hit> Find(string query, bool matchCase = false, bool wholeWord = false)
    {
        var hits = new List<Hit>();
        if (string.IsNullOrEmpty(query)) return hits;
        var cmp = matchCase ? StringComparison.Ordinal : StringComparison.CurrentCultureIgnoreCase;
        void In(string text, int b, int r, int c)
        {
            for (var i = text.IndexOf(query, cmp); i >= 0; i = text.IndexOf(query, i + query.Length, cmp))
                if (!wholeWord || (Boundary(text, i - 1) && Boundary(text, i + query.Length))) hits.Add(new Hit(b, r, c, i, query.Length));
        }
        for (var b = 0; b < Blocks.Count; b++)
            switch (Blocks[b])
            {
                case Para p: In(p.Text, b, -1, -1); break;
                case Table t: for (var r = 0; r < t.Rows.Count; r++) for (var c = 0; c < t.Rows[r].Count; c++) In(t.Rows[r][c], b, r, c); break;
            }
        return hits;
        static bool Boundary(string s, int i) => i < 0 || i >= s.Length || !char.IsLetterOrDigit(s[i]);
    }

    /// <summary>Alle ersetzen; das Format der Fundstelle bleibt für den neuen Text. Liefert die Anzahl.</summary>
    public int ReplaceAll(string query, string with, bool matchCase = false, bool wholeWord = false)
    {
        var hits = Find(query, matchCase, wholeWord);
        foreach (var g in hits.GroupBy(h => (h.Block, h.Row, h.Col)))
            foreach (var h in g.OrderByDescending(h => h.Start))
            {
                if (Blocks[h.Block] is Para p) ReplaceAt(p, h.Start, h.Length, with);
                else if (Blocks[h.Block] is Table t) t.Rows[h.Row][h.Col] = t.Rows[h.Row][h.Col].Remove(h.Start, h.Length).Insert(h.Start, with);
            }
        return hits.Count;
    }

    public static void ReplaceAt(Para p, int start, int length, string with)
    {
        var parts = Split(p, start, length);
        var fmt = parts.FirstOrDefault(x => x.Inside);
        var runs = new List<(string, Fmt, double?)>(); var done = false;
        foreach (var x in parts)
            if (!x.Inside) runs.Add((x.Text, x.Fmt, x.Size));
            else if (!done) { runs.Add((with, fmt.Fmt, fmt.Size)); done = true; }
        if (!done) runs.Insert(0, (with, Fmt.None, null));
        p.SetRuns(runs);
    }

    public WordCount Count()
    {
        var t = PlainText().Replace('\f', ' ');
        var words = t.Split((char[])[' ', '\n', '\t', '\r', ' '], StringSplitOptions.RemoveEmptyEntries).Count(w => w.Any(char.IsLetterOrDigit));
        return new(words, t.Replace("\n", "").Length, t.Count(c => !char.IsWhiteSpace(c)), Paras.Count(p => p.Text.Trim().Length > 0));
    }
}
