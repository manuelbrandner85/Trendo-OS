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

/// <summary>W2/W5/W7: Markierung über einem Textbereich – Link, Kommentar, Fußnote/Endnote (Länge 0, steht hinter der Stelle), eingefügt/gelöscht (Änderungen).
/// Pilot 0.9.47: Schriftart, Schriftfarbe und Hervorhebung sind ebenfalls Markierungen (Wert = Schriftname bzw. Farbe #RRGGBB) – so wandern sie beim Tippen, Teilen und Verbinden mit.</summary>
public enum MarkKind { Link, Comment, Footnote, Endnote, Insert, Delete, Font, Color, Highlight }

public sealed record Mark(int Start, int Length, MarkKind Kind, string Value = "")
{
    public int End => Start + Length;
}

public abstract class Block { public abstract Block Copy(); }

/// <summary>W5: automatisches Inhaltsverzeichnis an dieser Stelle (aus den Überschriften 1–3).</summary>
public sealed class TocBlock : Block
{
    public string Title { get; set; } = "Inhalt";
    public override Block Copy() => new TocBlock { Title = Title };
}

/// <summary>W3/W4: Seite einrichten und Design – Ränder, Hoch/Quer, Spalten, Kopf-/Fußzeile, Seitenzahlen, Wasserzeichen, Seitenrand-Linie, Schrift und Überschriftenfarbe.</summary>
public sealed class PageSetup
{
    public double Top { get; set; } = 2.5;
    public double Bottom { get; set; } = 2;
    public double Left { get; set; } = 2.5;
    public double Right { get; set; } = 2.5;
    public bool Landscape { get; set; }
    public int Columns { get; set; } = 1;
    public string Header { get; set; } = "";
    public string Footer { get; set; } = "";
    public bool PageNumbers { get; set; }
    public string Watermark { get; set; } = "";
    public bool PageBorder { get; set; }
    public bool Hyphenate { get; set; }
    public string Design { get; set; } = "standard";
    public string Font => Designs.FirstOrDefault(d => d.Id == Design).Font ?? "Arial";
    public string HeadingColor => Designs.FirstOrDefault(d => d.Id == Design).Color ?? "#1F3864";
    public PageSetup Copy() => (PageSetup)MemberwiseClone();

    public static readonly IReadOnlyList<(string Id, string Name, double Top, double Bottom, double Left, double Right)> Margins =
    [
        ("normal", "Normal", 2.5, 2, 2.5, 2.5), ("schmal", "Schmal", 1.27, 1.27, 1.27, 1.27), ("mittel", "Mittel", 2.54, 2.54, 1.91, 1.91), ("breit", "Breit", 2.54, 2.54, 5.08, 5.08),
    ];

    public static readonly IReadOnlyList<(string Id, string Name, string Font, string Color)> Designs =
    [
        ("standard", "Standard", "Arial", "#1F3864"), ("klassisch", "Klassisch", "Georgia", "#7F1D1D"), ("modern", "Modern", "Segoe UI", "#0F766E"),
        ("freundlich", "Freundlich", "Verdana", "#B45309"), ("seriös", "Seriös", "Times New Roman", "#111827"), ("frisch", "Frisch", "Trebuchet MS", "#2563EB"),
    ];

    public void SetMargins(string id) { var m = Margins.FirstOrDefault(x => x.Id == id); if (m.Id is null) return; (Top, Bottom, Left, Right) = (m.Top, m.Bottom, m.Left, m.Right); }
}

public sealed class Para : Block
{
    public string Text { get; set; } = "";
    public ParaStyle Style { get; set; }
    public Align Align { get; set; }
    public ListKind List { get; set; }
    /// <summary>F3 Text: Ebene in einer mehrstufigen Liste (0 = oberste, höchstens 8).</summary>
    public int ListLevel { get; set; }
    public List<Span> Spans { get; set; } = [];
    /// <summary>W4: Einzug links in cm.</summary>
    public double IndentCm { get; set; }
    /// <summary>W4: Abstand nach dem Absatz in Punkt (null = Standard).</summary>
    public double? SpaceAfterPt { get; set; }
    /// <summary>Pilot 0.9.47: Zeilenabstand als Faktor (1 = einfach, 1,15, 1,5, 2); null = Standard der Absatzart.</summary>
    public double? LineSpacing { get; set; }
    /// <summary>Abstand vor dem Absatz in Punkt; null = Standard.</summary>
    public double? SpaceBeforePt { get; set; }
    /// <summary>Einzug der ersten Zeile in cm (negativ = hängend).</summary>
    public double FirstLineCm { get; set; }
    /// <summary>W2/W5/W7: Links, Kommentare, Fuß-/Endnoten, Änderungen.</summary>
    public List<Mark> Marks { get; set; } = [];
    public Para() { }
    public Para(string text, ParaStyle style = ParaStyle.Text) { Text = text; Style = style; }
    public override Block Copy() => new Para { Text = Text, Style = Style, Align = Align, List = List, ListLevel = ListLevel, Spans = [.. Spans], IndentCm = IndentCm, SpaceAfterPt = SpaceAfterPt, LineSpacing = LineSpacing, SpaceBeforePt = SpaceBeforePt, FirstLineCm = FirstLineCm, Marks = [.. Marks] };

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

/// <summary>T-0692: Format einer Tabellenzelle – Zeichenformate und Markierungen (Schrift, Farbe, Hervorhebung, Link, Kommentar, Änderung) zum Zellentext,
/// Ausrichtung, und wie viele Spalten/Zeilen die Zelle überdeckt (verbundene Zellen). Der Zellentext selbst bleibt in <see cref="Table.Rows"/>.</summary>
public sealed class CellFmt
{
    public List<Span> Spans { get; set; } = [];
    public List<Mark> Marks { get; set; } = [];
    public Align Align { get; set; }
    public int ColSpan { get; set; } = 1;
    public int RowSpan { get; set; } = 1;
    public bool IsPlain => Spans.Count == 0 && Marks.Count == 0 && Align == Align.Left && ColSpan <= 1 && RowSpan <= 1;
    public CellFmt Copy() => new() { Spans = [.. Spans], Marks = [.. Marks], Align = Align, ColSpan = ColSpan, RowSpan = RowSpan };
}

public sealed class Table : Block
{
    public List<List<string>> Rows { get; set; } = [];
    /// <summary>T-0692: Zellformate und verbundene Zellen; nur Zellen mit Format stehen hier. Schlüssel = (Zeile, Spalte) der Zelle oben links.</summary>
    public Dictionary<(int Row, int Col), CellFmt> Cells { get; set; } = [];
    /// <summary>T-0692: Spaltenbreiten als Anteile (leer = alle gleich breit).</summary>
    public List<double> Widths { get; set; } = [];
    public Table() { }
    public Table(int rows, int cols) { for (var r = 0; r < rows; r++) Rows.Add(Enumerable.Repeat("", cols).ToList()); }
    public int Cols => Rows.Count == 0 ? 0 : Rows.Max(r => r.Count);
    public override Block Copy() => new Table { Rows = Rows.Select(r => r.ToList()).ToList(), Cells = Cells.ToDictionary(k => k.Key, k => k.Value.Copy()), Widths = [.. Widths] };

    /// <summary>Alle Zeilen auf gleiche Spaltenzahl bringen.</summary>
    public void Pad() { var n = Cols; foreach (var r in Rows) while (r.Count < n) r.Add(""); }

    public string Text(int r, int c) => r >= 0 && r < Rows.Count && c >= 0 && c < Rows[r].Count ? Rows[r][c] : "";

    /// <summary>Spaltenbreiten als Anteile, Summe 1.</summary>
    public double[] Fractions()
    {
        var n = Math.Max(1, Cols); var w = new double[n];
        for (var i = 0; i < n; i++) w[i] = Widths.Count > 0 && i < Widths.Count && Widths[i] > 0 ? Widths[i] : Widths.Count > 0 ? Widths.Average() : 1;
        var sum = w.Sum(); for (var i = 0; i < n; i++) w[i] /= sum;
        return w;
    }

    /// <summary>Spaltenbreiten setzen (beliebige Einheit, es zählt das Verhältnis); mindestens 5 % je Spalte.</summary>
    public void SetWidths(IEnumerable<double> widths)
    {
        var w = widths.Take(Math.Max(1, Cols)).Select(x => Math.Max(0, x)).ToList(); while (w.Count < Cols) w.Add(w.Count > 0 ? w.Average() : 1);
        var sum = w.Sum(); if (sum <= 0) { Widths = []; return; }
        w = w.Select(x => Math.Max(0.05, x / sum)).ToList(); sum = w.Sum();
        Widths = w.Select(x => Math.Round(x / sum, 4)).ToList();
        if (Widths.All(x => Math.Abs(x - Widths[0]) < 0.0005)) Widths = [];
    }

    public (int ColSpan, int RowSpan) SpanOf(int r, int c) => Cells.TryGetValue((r, c), out var f) ? (Math.Max(1, f.ColSpan), Math.Max(1, f.RowSpan)) : (1, 1);

    /// <summary>Die Zelle oben links, zu der (r, c) gehört – bei verbundenen Zellen die sichtbare Zelle, sonst (r, c) selbst.</summary>
    public (int Row, int Col) Anchor(int r, int c)
    {
        foreach (var (k, f) in Cells)
            if ((f.ColSpan > 1 || f.RowSpan > 1) && r >= k.Row && r < k.Row + f.RowSpan && c >= k.Col && c < k.Col + f.ColSpan) return k;
        return (r, c);
    }

    public bool IsCovered(int r, int c) => Anchor(r, c) != (r, c);

    /// <summary>Zelle als Absatz (Text aus <see cref="Rows"/>, Formate aus <see cref="Cells"/>) – nach dem Ändern mit <see cref="SetCell"/> zurückschreiben.</summary>
    public Para CellPara(int r, int c)
    {
        var text = Text(r, c); var p = new Para(text);
        if (Cells.TryGetValue((r, c), out var f))
        {
            p.Align = f.Align;
            p.Spans = f.Spans.Where(s => s.Start < text.Length && s.Length > 0).Select(s => s with { Length = Math.Min(s.Length, text.Length - s.Start) }).ToList();
            p.Marks = f.Marks.Where(m => m.Start < text.Length && m.Length > 0).Select(m => m with { Length = Math.Min(m.Length, text.Length - m.Start) }).ToList();
        }
        return p;
    }

    /// <summary>Kommentar ohne markierten Text (aus einer fremden Datei) an ein Zeichen heften – in Zellen gibt es nur Markierungen mit Länge.</summary>
    public static void PinComments(Para cell)
    {
        if (cell.Text.Length == 0) return;
        cell.Marks = cell.Marks.Select(m => m.Kind == MarkKind.Comment && m.Length == 0 ? m with { Start = Math.Min(m.Start, cell.Text.Length - 1), Length = 1 } : m).ToList();
    }

    public void SetCell(int r, int c, Para p)
    {
        if (r < 0 || r >= Rows.Count || c < 0) return;
        while (Rows[r].Count <= c) Rows[r].Add("");
        Rows[r][c] = p.Text;
        var marks = p.Marks.Where(m => m.Length > 0).ToList(); // Fuß- und Endnoten (ohne Länge) gibt es in Zellen nicht
        if (p.Spans.Count == 0 && marks.Count == 0 && p.Align == Align.Left && !Cells.ContainsKey((r, c))) return;
        var f = Cells.TryGetValue((r, c), out var old) ? old : Cells[(r, c)] = new CellFmt();
        f.Spans = [.. p.Spans]; f.Marks = marks; f.Align = p.Align;
        if (f.IsPlain) Cells.Remove((r, c));
    }

    /// <summary>Zellen verbinden: (r, c) überdeckt <paramref name="cols"/> Spalten und <paramref name="rows"/> Zeilen. Texte der überdeckten Zellen wandern in die erste Zelle.
    /// Liefert false, wenn der Bereich nicht passt oder eine andere Verbindung schneidet.</summary>
    public bool Merge(int r, int c, int cols, int rows)
    {
        Pad();
        if (r < 0 || c < 0 || cols < 1 || rows < 1 || r + rows > Rows.Count || c + cols > Cols || cols == 1 && rows == 1 || IsCovered(r, c)) return false;
        for (var y = r; y < r + rows; y++)
            for (var x = c; x < c + cols; x++)
            {
                var a = Anchor(y, x); var (cs, rs) = SpanOf(a.Row, a.Col);
                if (a.Row < r || a.Col < c || a.Row + rs > r + rows || a.Col + cs > c + cols) return false; // ragt hinaus
            }
        var first = CellPara(r, c);
        for (var y = r; y < r + rows; y++)
            for (var x = c; x < c + cols; x++)
            {
                if ((y, x) == (r, c)) continue;
                var other = CellPara(y, x);
                if (other.Text.Length > 0)
                {
                    var glue = first.Text.Length > 0; var at = first.Text.Length + (glue ? 1 : 0);
                    var runs = first.Runs().ToList(); if (glue) runs.Add(("\n", Fmt.None, null)); runs.AddRange(other.Runs());
                    first.SetRuns(runs);
                    first.Marks.AddRange(other.Marks.Select(m => m with { Start = m.Start + at }));
                }
                Rows[y][x] = ""; Cells.Remove((y, x));
            }
        SetCell(r, c, first);
        var f = Cells.TryGetValue((r, c), out var cur) ? cur : Cells[(r, c)] = new CellFmt();
        f.ColSpan = cols; f.RowSpan = rows;
        return true;
    }

    /// <summary>Verbindung lösen; der Text bleibt in der ersten Zelle.</summary>
    public bool Unmerge(int r, int c)
    {
        var a = Anchor(r, c);
        if (!Cells.TryGetValue(a, out var f) || f.ColSpan <= 1 && f.RowSpan <= 1) return false;
        f.ColSpan = f.RowSpan = 1; if (f.IsPlain) Cells.Remove(a);
        return true;
    }

    void Rekey(Func<(int Row, int Col), CellFmt, (int Row, int Col)?> map)
    {
        var res = new Dictionary<(int, int), CellFmt>();
        foreach (var (k, f) in Cells) if (map(k, f) is { } nk) res[nk] = f;
        Cells = res;
    }

    public void InsertRow(int at)
    {
        at = Math.Clamp(at, 0, Rows.Count);
        Rows.Insert(at, Enumerable.Repeat("", Math.Max(1, Cols)).ToList());
        Rekey((k, f) => { if (k.Row >= at) return (k.Row + 1, k.Col); if (k.Row + f.RowSpan > at) f.RowSpan++; return k; });
    }

    public void InsertColumn(int at)
    {
        Pad(); at = Math.Clamp(at, 0, Cols);
        foreach (var r in Rows) r.Insert(Math.Clamp(at, 0, r.Count), "");
        Rekey((k, f) => { if (k.Col >= at) return (k.Row, k.Col + 1); if (k.Col + f.ColSpan > at) f.ColSpan++; return k; });
        if (Widths.Count > 0) { var w = Widths.ToList(); w.Insert(Math.Min(at, w.Count), w.Average()); SetWidths(w); }
    }

    public void DeleteRow(int at)
    {
        if (Rows.Count <= 1 || at < 0 || at >= Rows.Count) return;
        Pad();
        // Verbundene Zelle beginnt in dieser Zeile und reicht weiter: Inhalt und Format rücken in die nächste Zeile
        foreach (var (k, f) in Cells.Where(x => x.Key.Row == at && x.Value.RowSpan > 1).ToList()) { Rows[at + 1][k.Col] = Rows[at][k.Col]; Cells.Remove(k); f.RowSpan--; Cells[(at + 1, k.Col)] = f; }
        Rows.RemoveAt(at);
        Rekey((k, f) =>
        {
            if (k.Row == at) return null;
            if (k.Row > at) return (k.Row - 1, k.Col);
            if (k.Row + f.RowSpan > at) f.RowSpan--;
            return k;
        });
    }

    public void DeleteColumn(int at)
    {
        if (Cols <= 1 || at < 0 || at >= Cols) return;
        Pad();
        foreach (var (k, f) in Cells.Where(x => x.Key.Col == at && x.Value.ColSpan > 1).ToList()) { Rows[k.Row][at + 1] = Rows[k.Row][at]; Cells.Remove(k); f.ColSpan--; Cells[(k.Row, at + 1)] = f; }
        foreach (var r in Rows) r.RemoveAt(at);
        Rekey((k, f) =>
        {
            if (k.Col == at) return null;
            if (k.Col > at) return (k.Row, k.Col - 1);
            if (k.Col + f.ColSpan > at) f.ColSpan--;
            return k;
        });
        if (at < Widths.Count) { var w = Widths.ToList(); w.RemoveAt(at); SetWidths(w); }
    }
}

public sealed class Picture : Block
{
    public byte[] Data { get; set; } = [];
    /// <summary>Dateiendung ohne Punkt: png, jpg, gif.</summary>
    public string Ext { get; set; } = "png";
    public double WidthCm { get; set; } = 12;
    public double HeightCm { get; set; } = 8;
    /// <summary>W5: Beschriftung („Abbildung 1: …“ wird automatisch nummeriert).</summary>
    public string Caption { get; set; } = "";
    /// <summary>W7: Alternativtext für Bildschirmleser (Barrierefreiheit).</summary>
    public string Alt { get; set; } = "";
    public override Block Copy() => new Picture { Data = Data, Ext = Ext, WidthCm = WidthCm, HeightCm = HeightCm, Caption = Caption, Alt = Alt };
}

public sealed class PageBreak : Block { public override Block Copy() => new PageBreak(); }

public sealed record WordCount(int Words, int Characters, int CharactersNoSpaces, int Paragraphs);

public sealed class TextDoc
{
    public List<Block> Blocks { get; set; } = [];
    public string? Title { get; set; }
    public PageSetup Page { get; set; } = new();
    /// <summary>T-0682: Was beim Öffnen einer fremden Datei nicht übernommen wurde (null = neu angelegt oder nicht geprüft).</summary>
    public ImportReport? Import { get; set; }

    public static TextDoc Empty() => new() { Blocks = [new Para()] };
    public TextDoc Copy() => new() { Blocks = Blocks.Select(b => b.Copy()).ToList(), Title = Title, Page = Page.Copy() };
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

    /// <summary>T-0692: Text an einer Stelle eintippen. Das Eingefügte erbt das Format des Zeichens davor (am Absatzanfang das des ersten Zeichens);
    /// Schrift, Farbe und Links laufen weiter wie beim Weitertippen.</summary>
    public static void InsertAt(Para p, int at, string s)
    {
        if (s.Length == 0) return;
        at = Math.Clamp(at, 0, p.Text.Length); var n = s.Length;
        var spans = new List<Span>();
        foreach (var sp in p.Spans)
        {
            if (sp.Start >= at && !(at == 0 && sp.Start == 0)) spans.Add(sp with { Start = sp.Start + n });
            else if (sp.End >= at) spans.Add(sp with { Length = sp.Length + n });
            else spans.Add(sp);
        }
        MapMarks(p, at, 0, n);
        p.Text = p.Text.Insert(at, s); p.Spans = spans;
        p.SetRuns(p.Runs().ToList()); // normalisieren
    }

    /// <summary>T-0692: Einen formatierten Absatzteil an einer Stelle einfügen (Einfügen aus der Zwischenablage, Verbinden von Absätzen) – der Teil behält sein eigenes Format.</summary>
    public static void InsertPara(Para p, int at, Para piece)
    {
        if (piece.Text.Length == 0) return;
        at = Math.Clamp(at, 0, p.Text.Length); var n = piece.Text.Length;
        var runs = new List<(string, Fmt, double?)>(); var pos = 0; var done = false;
        foreach (var (t, f, size) in p.Runs())
        {
            if (!done && at >= pos && at <= pos + t.Length)
            {
                runs.Add((t[..(at - pos)], f, size)); runs.AddRange(piece.Runs()); runs.Add((t[(at - pos)..], f, size)); done = true;
            }
            else runs.Add((t, f, size));
            pos += t.Length;
        }
        if (!done) runs.AddRange(piece.Runs());
        var marks = new List<Mark>();
        foreach (var m in p.Marks)
        {
            if (m.Start >= at && (m.Length > 0 || m.Start > at)) marks.Add(m with { Start = m.Start + n });
            else if (m.Length > 0 && m.End > at) { marks.Add(m with { Length = at - m.Start }); marks.Add(m with { Start = at + n, Length = m.End - at }); } // Markierung um das Eingefügte herum teilen
            else marks.Add(m);
        }
        marks.AddRange(piece.Marks.Select(m => m with { Start = m.Start + at }));
        p.SetRuns(runs); p.Marks = marks; NormalizeInk(p);
        p.Marks.Sort((x, y) => x.Start.CompareTo(y.Start));
    }

    /// <summary>T-0692: Ausschnitt eines Absatzes als eigener Absatz (mit Formaten und Markierungen).</summary>
    public static Para Slice(Para p, int start, int length)
    {
        (start, length) = Clamp(p, start, length);
        var c = (Para)p.Copy();
        ReplaceAt(c, start + length, c.Text.Length - start - length, "");
        ReplaceAt(c, 0, start, "");
        return c;
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
        MapMarks(p, pre, removed, inserted);
    }

    /// <summary>Markierungen an eine Textänderung anpassen (ab <paramref name="pre"/> wurden <paramref name="removed"/> Zeichen durch <paramref name="inserted"/> ersetzt).</summary>
    static void MapMarks(Para p, int pre, int removed, int inserted)
    {
        if (p.Marks.Count == 0) return;
        var res = new List<Mark>();
        foreach (var m in p.Marks)
        {
            int Map(int i) => i <= pre ? i : i >= pre + removed ? i - removed + inserted : pre;
            var a = Map(m.Start); var b = Map(m.End);
            if (m.Length > 0 && m.Start < pre && m.End >= pre && m.End <= pre + removed && inserted > 0 && (m.Kind is MarkKind.Link or MarkKind.Insert || IsInk(m.Kind))) b = pre + inserted; // Weitertippen behält Link, Schrift, Farbe
            if (m.Length == 0) { if (m.Start > pre && m.Start < pre + removed) continue; res.Add(m with { Start = a }); }   // Fußnote im gelöschten Text fällt weg
            else if (b > a) res.Add(m with { Start = a, Length = b - a });
        }
        p.Marks = res;
    }

    // ---------- Pilot 0.9.47: Schriftart, Schriftfarbe, Hervorhebung ----------
    public static bool IsInk(MarkKind k) => k is MarkKind.Font or MarkKind.Color or MarkKind.Highlight;

    /// <summary>Schriftart, Farbe bzw. Hervorhebung an einer Zeichenposition (innerste Markierung gewinnt); null = Standard.</summary>
    public static string? InkAt(Para p, int i, MarkKind kind) =>
        p.Marks.Where(m => m.Kind == kind && m.Length > 0 && i >= m.Start && i < m.End).OrderBy(m => m.Length).FirstOrDefault()?.Value;

    /// <summary>Schriftart, Farbe oder Hervorhebung für einen Bereich setzen (null = entfernen). Überlappende alte Werte werden zugeschnitten.</summary>
    public static void SetInk(Para p, int start, int length, MarkKind kind, string? value)
    {
        if (!IsInk(kind)) throw new ArgumentException("Nur Schriftart, Farbe oder Hervorhebung.");
        (start, length) = Clamp(p, start, length); if (length == 0) return;
        if (kind != MarkKind.Font && value is not null && !System.Text.RegularExpressions.Regex.IsMatch(value, "^#[0-9A-Fa-f]{6}$")) throw new ArgumentException("Farbe als #RRGGBB angeben.");
        if (kind == MarkKind.Font && value is not null && (value.Trim().Length == 0 || value.Length > 64)) throw new ArgumentException("Bitte einen Schriftnamen angeben.");
        var end = start + length; var res = new List<Mark>();
        foreach (var m in p.Marks)
        {
            if (m.Kind != kind || m.End <= start || m.Start >= end) { res.Add(m); continue; }
            if (m.Start < start) res.Add(m with { Length = start - m.Start });   // linker Rest
            if (m.End > end) res.Add(m with { Start = end, Length = m.End - end }); // rechter Rest
        }
        if (value is not null) res.Add(new Mark(start, length, kind, kind == MarkKind.Font ? value.Trim() : value.ToUpperInvariant()));
        p.Marks = res; NormalizeInk(p);
    }

    /// <summary>Angrenzende Bereiche mit gleichem Wert zusammenfassen (z. B. nach dem Öffnen einer Datei mit vielen kleinen Abschnitten).</summary>
    public static void NormalizeInk(Para p)
    {
        var other = p.Marks.Where(m => !IsInk(m.Kind)).ToList();
        foreach (var g in p.Marks.Where(m => IsInk(m.Kind) && m.Length > 0).GroupBy(m => (m.Kind, m.Value)))
        {
            Mark? cur = null;
            foreach (var m in g.OrderBy(m => m.Start))
            {
                if (cur is { } c && m.Start <= c.End) { cur = c with { Length = Math.Max(c.End, m.End) - c.Start }; continue; }
                if (cur is not null) other.Add(cur); cur = m;
            }
            if (cur is not null) other.Add(cur);
        }
        other.Sort((x, y) => x.Start.CompareTo(y.Start));
        p.Marks = other;
    }

    /// <summary>Markierung hinzufügen (Link, Kommentar, Fuß-/Endnote).</summary>
    public static void AddMark(Para p, int start, int length, MarkKind kind, string value)
    {
        (start, length) = Clamp(p, start, length);
        if (kind is MarkKind.Footnote or MarkKind.Endnote) { start += length; length = 0; }
        p.Marks.Add(new Mark(start, length, kind, value));
        p.Marks.Sort((x, y) => x.Start.CompareTo(y.Start));
    }

    public static void RemoveMarks(Para p, int start, int length, MarkKind kind) =>
        p.Marks.RemoveAll(m => m.Kind == kind && (length == 0 ? m.Start <= start && m.End >= start : m.Start < start + length && m.End > start));

    /// <summary>Enter: Absatz an der Stelle teilen. Der neue Absatz bekommt den Rest samt Formaten; nach Überschriften geht es mit „Text“ weiter, Listen laufen weiter.</summary>
    public Para SplitAt(int block, int at)
    {
        var p = (Para)Blocks[block];
        at = Math.Clamp(at, 0, p.Text.Length);
        var parts = Split(p, at, p.Text.Length - at);
        var next = new Para { Style = p.Style is ParaStyle.Text or ParaStyle.Quote ? p.Style : ParaStyle.Text, Align = p.Align, List = p.List };
        next.SetRuns(parts.Where(x => x.Inside).Select(x => (x.Text, x.Fmt, x.Size)));
        p.SetRuns(parts.Where(x => !x.Inside).Select(x => (x.Text, x.Fmt, x.Size)));
        next.ListLevel = p.ListLevel; next.IndentCm = p.IndentCm; next.SpaceAfterPt = p.SpaceAfterPt; next.LineSpacing = p.LineSpacing; next.SpaceBeforePt = p.SpaceBeforePt; next.FirstLineCm = p.FirstLineCm;
        next.Marks = p.Marks.Where(m => m.Start >= at && (m.Length > 0 || m.Start > at)).Select(m => m with { Start = m.Start - at })
            .Concat(p.Marks.Where(m => IsInk(m.Kind) && m.Start < at && m.End > at).Select(m => m with { Start = 0, Length = m.End - at })).ToList(); // Schrift und Farbe laufen im neuen Absatz weiter
        p.Marks = p.Marks.Where(m => m.Start < at || m.Length == 0 && m.Start == at).Select(m => m.End > at && m.Length > 0 ? m with { Length = at - m.Start } : m).ToList();
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
        prev.Marks.AddRange(p.Marks.Select(m => m with { Start = m.Start + at }));
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
    public static void InsertRow(Table t, int at) => t.InsertRow(at);
    public static void InsertColumn(Table t, int at) => t.InsertColumn(at);
    public static void DeleteRow(Table t, int at) => t.DeleteRow(at);
    public static void DeleteColumn(Table t, int at) => t.DeleteColumn(at);

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
                else if (Blocks[h.Block] is Table t) { var cp = t.CellPara(h.Row, h.Col); ReplaceAt(cp, h.Start, h.Length, with); t.SetCell(h.Row, h.Col, cp); } // Zellformate wandern mit
            }
        return hits.Count;
    }

    public static void ReplaceAt(Para p, int start, int length, string with)
    {
        (start, length) = Clamp(p, start, length);
        MapMarks(p, start, length, with.Length);
        var parts = Split(p, start, length);
        var fmt = parts.FirstOrDefault(x => x.Inside);
        var runs = new List<(string, Fmt, double?)>(); var done = false; var pos = 0;
        foreach (var x in parts)
        {
            // reines Einfügen (Länge 0): an der Stelle, mit dem Format des folgenden Abschnitts
            if (!done && length == 0 && pos == start) { runs.Add((with, x.Fmt, x.Size)); done = true; }
            if (!x.Inside) runs.Add((x.Text, x.Fmt, x.Size));
            else if (!done) { runs.Add((with, fmt.Fmt, fmt.Size)); done = true; }
            pos += x.Text.Length;
        }
        if (!done) runs.Add((with, parts.Count > 0 ? parts[^1].Fmt : Fmt.None, parts.Count > 0 ? parts[^1].Size : null)); // am Ende anhängen
        p.SetRuns(runs);
    }

    public WordCount Count()
    {
        var t = PlainText().Replace('\f', ' ');
        var words = t.Split((char[])[' ', '\n', '\t', '\r', ' '], StringSplitOptions.RemoveEmptyEntries).Count(w => w.Any(char.IsLetterOrDigit));
        return new(words, t.Replace("\n", "").Length, t.Count(c => !char.IsWhiteSpace(c)), Paras.Count(p => p.Text.Trim().Length > 0));
    }
}
