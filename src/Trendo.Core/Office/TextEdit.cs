using System.Text;

namespace Trendo.Core.Office;

/// <summary>T-0692: Bearbeiten über das ganze Dokument – Stellen, Bereiche über Absatzgrenzen, Löschen, Tippen, Kopieren und Einfügen mit Format.
/// Das Schreibfeld der Oberfläche ruft nur diese Wege; <see cref="TextDoc"/> bleibt die Wahrheit.
/// Regel für Bereiche: Anfang und Ende liegen im selben Kasten (Absatz oder Tabellenzelle) oder beide in Absätzen – Bausteine dazwischen gehören ganz dazu.</summary>
public static class TextEdit
{
    /// <summary>Absatz an der Stelle: der Absatz selbst oder die Tabellenzelle als Absatz (nach dem Ändern <see cref="Commit"/>).</summary>
    public static Para? ParaAt(TextDoc d, TextPos p) =>
        p.Block < 0 || p.Block >= d.Blocks.Count ? null : d.Blocks[p.Block] switch
        {
            Para para when !p.InCell => para,
            Table t when p.InCell && p.Row < t.Rows.Count && p.Col < t.Cols => t.CellPara(p.Row, p.Col),
            _ => null,
        };

    public static void Commit(TextDoc d, TextPos p, Para para) { if (p.InCell && d.Blocks[p.Block] is Table t) t.SetCell(p.Row, p.Col, para); }

    public static int Length(TextDoc d, TextPos box) => ParaAt(d, box)?.Text.Length ?? 0;

    /// <summary>Stelle gültig machen: Baustein im Dokument, in Tabellen die sichtbare Zelle, Zeichen im Text. Bild, Seitenumbruch und Verzeichnis: Stelle (Baustein, 0).</summary>
    public static TextPos Clamp(TextDoc d, TextPos p)
    {
        if (d.Blocks.Count == 0) d.Blocks.Add(new Para());
        var b = Math.Clamp(p.Block, 0, d.Blocks.Count - 1);
        switch (d.Blocks[b])
        {
            case Para para: return new TextPos(b, Math.Clamp(b == p.Block && !p.InCell ? p.Offset : p.Block > b ? para.Text.Length : 0, 0, para.Text.Length));
            case Table t when t.Rows.Count > 0:
                var same = b == p.Block && p.InCell;
                var (r, c) = t.Anchor(Math.Clamp(same ? p.Row : 0, 0, t.Rows.Count - 1), Math.Clamp(same ? p.Col : 0, 0, Math.Max(0, t.Cols - 1)));
                return new TextPos(b, Math.Clamp(same && (r, c) == (p.Row, p.Col) ? p.Offset : 0, 0, t.Text(r, c).Length), r, c);
            default: return new TextPos(b, 0);
        }
    }

    public static bool IsObject(TextDoc d, TextPos p) => p.Block >= 0 && p.Block < d.Blocks.Count && d.Blocks[p.Block] is not (Para or Table);

    static IEnumerable<(int Row, int Col)> CellsOf(Table t)
    {
        for (var r = 0; r < t.Rows.Count; r++) for (var c = 0; c < t.Cols; c++) if (!t.IsCovered(r, c)) yield return (r, c);
    }

    /// <summary>Anfang des nächsten Textkastens ab Baustein <paramref name="from"/> (vorwärts) bzw. Ende des vorigen (rückwärts); Bilder und Umbrüche werden übersprungen.</summary>
    static TextPos? BoxFrom(TextDoc d, int from, int dir)
    {
        for (var b = from; b >= 0 && b < d.Blocks.Count; b += dir)
            switch (d.Blocks[b])
            {
                case Para p: return new TextPos(b, dir > 0 ? 0 : p.Text.Length);
                case Table t when t.Rows.Count > 0:
                    var cells = CellsOf(t).ToList(); if (cells.Count == 0) break;
                    var (r, c) = dir > 0 ? cells[0] : cells[^1];
                    return new TextPos(b, dir > 0 ? 0 : t.Text(r, c).Length, r, c);
            }
        return null;
    }

    public static TextPos DocStart(TextDoc d) => BoxFrom(d, 0, 1) ?? new TextPos(0, 0);
    public static TextPos DocEnd(TextDoc d) => BoxFrom(d, d.Blocks.Count - 1, -1) ?? new TextPos(0, 0);

    /// <summary>Ein Zeichen weiter (dir = 1) oder zurück (dir = -1), über Absatz- und Zellgrenzen hinweg.</summary>
    public static TextPos Step(TextDoc d, TextPos p, int dir)
    {
        var len = Length(d, p);
        if (!IsObject(d, p))
        {
            if (dir > 0 && p.Offset < len) return p with { Offset = p.Offset + (char.IsHighSurrogate(ParaAt(d, p)!.Text[p.Offset]) && p.Offset + 1 < len ? 2 : 1) };
            if (dir < 0 && p.Offset > 0) return p with { Offset = p.Offset - (p.Offset > 1 && char.IsLowSurrogate(ParaAt(d, p)!.Text[p.Offset - 1]) ? 2 : 1) };
        }
        if (p.InCell && d.Blocks[p.Block] is Table t)
        {
            var cells = CellsOf(t).ToList(); var i = cells.IndexOf((p.Row, p.Col)) + dir;
            if (i >= 0 && i < cells.Count) return new TextPos(p.Block, dir > 0 ? 0 : t.Text(cells[i].Row, cells[i].Col).Length, cells[i].Row, cells[i].Col);
        }
        return BoxFrom(d, p.Block + dir, dir) ?? p;
    }

    static bool IsWord(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '\'' || c == '’';

    /// <summary>Wortweise springen (Strg+Pfeil): zum Anfang des nächsten bzw. dieses/vorigen Worts.</summary>
    public static TextPos WordStep(TextDoc d, TextPos p, int dir)
    {
        var text = ParaAt(d, p)?.Text ?? ""; var i = Math.Clamp(p.Offset, 0, text.Length);
        if (dir > 0)
        {
            if (i >= text.Length) return Step(d, p, 1);
            while (i < text.Length && IsWord(text[i])) i++;
            while (i < text.Length && !IsWord(text[i])) i++;
        }
        else
        {
            if (i <= 0) return Step(d, p, -1);
            while (i > 0 && !IsWord(text[i - 1])) i--;
            while (i > 0 && IsWord(text[i - 1])) i--;
        }
        return p with { Offset = i };
    }

    /// <summary>Das Wort an der Stelle (Doppelklick).</summary>
    public static (TextPos A, TextPos B) WordAt(TextDoc d, TextPos p)
    {
        var text = ParaAt(d, p)?.Text ?? ""; var i = Math.Clamp(p.Offset, 0, text.Length);
        int a = i, e = i;
        if (i < text.Length && IsWord(text[i]) || i > 0 && IsWord(text[i - 1]))
        {
            while (a > 0 && IsWord(text[a - 1])) a--;
            while (e < text.Length && IsWord(text[e])) e++;
        }
        else if (i < text.Length) e = i + 1;
        return (p with { Offset = a }, p with { Offset = e });
    }

    public static (TextPos A, TextPos B) Order(TextPos a, TextPos b) => a <= b ? (a, b) : (b, a);

    /// <summary>Die Textstücke eines Bereichs: je Absatz bzw. Zelle (Stelle des Kastens, Anfang, Länge). Tabellen zwischen Anfang und Ende liefern alle ihre Zellen.</summary>
    public static List<(TextPos Box, int Start, int Length)> Parts(TextDoc d, TextPos a, TextPos b)
    {
        (a, b) = Order(a, b); var res = new List<(TextPos, int, int)>();
        if (a.SameBox(b)) { if (ParaAt(d, a) is not null) res.Add((a with { Offset = 0 }, a.Offset, b.Offset - a.Offset)); return res; }
        for (var i = a.Block; i <= b.Block && i < d.Blocks.Count; i++)
            switch (d.Blocks[i])
            {
                case Para p:
                    var s = i == a.Block && !a.InCell ? a.Offset : 0; var e = i == b.Block && !b.InCell ? b.Offset : p.Text.Length;
                    res.Add((new TextPos(i, 0), Math.Clamp(s, 0, p.Text.Length), Math.Max(0, Math.Min(e, p.Text.Length) - s)));
                    break;
                case Table t:
                    foreach (var (r, c) in CellsOf(t)) res.Add((new TextPos(i, 0, r, c), 0, t.Text(r, c).Length));
                    break;
            }
        return res;
    }

    /// <summary>Etwas auf jedes Textstück des Bereichs anwenden (Zeichenformat, Farbe …); Zellen werden zurückgeschrieben.</summary>
    public static int Apply(TextDoc d, TextPos a, TextPos b, Action<Para, int, int> change)
    {
        var n = 0;
        foreach (var (box, start, length) in Parts(d, a, b))
        {
            if (ParaAt(d, box) is not { } p) continue;
            change(p, start, length); Commit(d, box, p); n++;
        }
        return n;
    }

    /// <summary>Die Absätze (bzw. Zellen) im Bereich – für Absatzformate wie Ausrichtung, Liste, Absatzart.</summary>
    public static int ApplyParas(TextDoc d, TextPos a, TextPos b, Action<Para> change) => Apply(d, a, b, (p, _, _) => change(p));

    /// <summary>Reiner Text des Bereichs (Absätze mit Zeilenwechsel, Tabellenzellen mit Tabulator).</summary>
    public static string Plain(IEnumerable<Block> blocks)
    {
        var sb = new StringBuilder(); var first = true;
        foreach (var b in blocks)
        {
            if (!first) sb.Append('\n'); first = false;
            switch (b)
            {
                case Para p: sb.Append(p.Text); break;
                case Table t: sb.Append(string.Join('\n', t.Rows.Select(r => string.Join('\t', r)))); break;
            }
        }
        return sb.ToString();
    }

    /// <summary>Bereich als eigene Bausteine herauskopieren (mit Formaten). Ein Bereich innerhalb eines Absatzes oder einer Zelle ergibt genau einen Absatz.</summary>
    public static List<Block> Copy(TextDoc d, TextPos a, TextPos b)
    {
        (a, b) = Order(a, b); var res = new List<Block>();
        if (a.SameBox(b)) { if (ParaAt(d, a) is { } p) { var piece = TextDoc.Slice(p, a.Offset, b.Offset - a.Offset); if (a.InCell) { piece.Align = Align.Left; } res.Add(piece); } return res; }
        for (var i = a.Block; i <= b.Block && i < d.Blocks.Count; i++)
        {
            if (d.Blocks[i] is Para p && (i == a.Block || i == b.Block))
            {
                var s = i == a.Block ? a.Offset : 0; var e = i == b.Block ? b.Offset : p.Text.Length;
                res.Add(TextDoc.Slice(p, s, e - s));
            }
            else res.Add(d.Blocks[i].Copy());
        }
        return res;
    }

    /// <summary>Bereich löschen; liefert die Stelle, an der der Cursor danach steht.</summary>
    public static TextPos Delete(TextDoc d, TextPos a, TextPos b)
    {
        (a, b) = Order(a, b);
        if (a == b) return a;
        if (a.SameBox(b))
        {
            if (ParaAt(d, a) is { } p) { TextDoc.ReplaceAt(p, a.Offset, b.Offset - a.Offset, ""); Commit(d, a, p); }
            return a;
        }
        if (d.Blocks[a.Block] is not Para first || d.Blocks[b.Block] is not Para last) return a; // nur Absatz bis Absatz
        TextDoc.ReplaceAt(first, a.Offset, first.Text.Length - a.Offset, "");
        TextDoc.ReplaceAt(last, 0, b.Offset, "");
        d.Blocks.RemoveRange(a.Block + 1, b.Block - a.Block - 1);
        d.MergeWithPrevious(a.Block + 1);
        return a;
    }

    /// <summary>Text eintippen oder als reinen Text einfügen. Zeilenwechsel teilen den Absatz (in Tabellenzellen bleiben sie Zeilenwechsel in der Zelle).</summary>
    public static TextPos InsertText(TextDoc d, TextPos pos, string text)
    {
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        if (text.Length == 0 || ParaAt(d, pos) is not { } p) return pos;
        if (pos.InCell || !text.Contains('\n')) { TextDoc.InsertAt(p, pos.Offset, text); Commit(d, pos, p); return pos with { Offset = pos.Offset + text.Length }; }
        var lines = text.Split('\n'); var cur = pos;
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0) { d.SplitAt(cur.Block, cur.Offset); cur = new TextPos(cur.Block + 1, 0); }
            TextDoc.InsertAt((Para)d.Blocks[cur.Block], cur.Offset, lines[i]); cur = cur with { Offset = cur.Offset + lines[i].Length };
        }
        return cur;
    }

    /// <summary>Kopierte Bausteine an der Stelle einfügen – Formate bleiben. In Tabellenzellen wird alles zu einem Absatz der Zelle.</summary>
    public static TextPos Paste(TextDoc d, TextPos pos, IReadOnlyList<Block> fragment)
    {
        if (fragment.Count == 0 || ParaAt(d, pos) is not { } p) return pos;
        if (pos.InCell || fragment.Count == 1 && fragment[0] is Para)
        {
            var at = pos.Offset;
            for (var i = 0; i < fragment.Count; i++)
            {
                var piece = fragment[i] as Para ?? new Para(Plain([fragment[i]]));
                if (i > 0) { TextDoc.InsertAt(p, at, "\n"); at++; }
                TextDoc.InsertPara(p, at, piece); at += piece.Text.Length;
            }
            Commit(d, pos, p);
            return pos with { Offset = at };
        }
        var tail = d.SplitAt(pos.Block, pos.Offset); var head = (Para)d.Blocks[pos.Block];
        var insertAt = pos.Block + 1; var from = 0; var to = fragment.Count;
        if (fragment[0] is Para f0)
        {
            var wasEmpty = head.Text.Length == 0;
            TextDoc.InsertPara(head, head.Text.Length, f0); from = 1;
            if (wasEmpty) { head.Style = f0.Style; head.Align = f0.Align; head.List = f0.List; head.ListLevel = f0.ListLevel; head.IndentCm = f0.IndentCm; head.LineSpacing = f0.LineSpacing; head.SpaceBeforePt = f0.SpaceBeforePt; head.SpaceAfterPt = f0.SpaceAfterPt; head.FirstLineCm = f0.FirstLineCm; }
        }
        Para? lastPara = fragment[^1] as Para; if (lastPara is not null && fragment.Count > 1) to--; else lastPara = null;
        for (var i = from; i < to; i++) d.Blocks.Insert(insertAt++, fragment[i].Copy());
        // insertAt zeigt jetzt auf den Rest des geteilten Absatzes
        var endAt = 0;
        if (lastPara is not null)
        {
            var tailEmpty = tail.Text.Length == 0;
            TextDoc.InsertPara(tail, 0, lastPara); endAt = lastPara.Text.Length;
            if (tailEmpty) { tail.Style = lastPara.Style; tail.Align = lastPara.Align; tail.List = lastPara.List; tail.ListLevel = lastPara.ListLevel; }
        }
        return new TextPos(insertAt, endAt);
    }

    /// <summary>Rücktaste ohne Auswahl: ein Zeichen zurück löschen; am Absatzanfang Liste beenden bzw. mit dem vorigen Absatz verbinden.</summary>
    public static TextPos Backspace(TextDoc d, TextPos pos)
    {
        if (ParaAt(d, pos) is not { } p) return pos;
        if (pos.Offset > 0) { var prev = Step(d, pos, -1); return Delete(d, prev, pos); }
        if (pos.InCell) return pos;
        if (p.List != ListKind.None) { if (p.ListLevel > 0) p.ListLevel--; else p.List = ListKind.None; return pos; }
        if (pos.Block > 0 && d.Blocks[pos.Block - 1] is PageBreak) { d.Blocks.RemoveAt(pos.Block - 1); return new TextPos(pos.Block - 1, 0); }
        var at = d.MergeWithPrevious(pos.Block);
        return at >= 0 ? new TextPos(pos.Block - 1, at) : pos;
    }

    /// <summary>Entf ohne Auswahl: ein Zeichen vorwärts löschen; am Absatzende den nächsten Absatz heranholen.</summary>
    public static TextPos DeleteForward(TextDoc d, TextPos pos)
    {
        if (ParaAt(d, pos) is not { } p) return pos;
        if (pos.Offset < p.Text.Length) return Delete(d, pos, Step(d, pos, 1));
        if (pos.InCell || pos.Block + 1 >= d.Blocks.Count) return pos;
        if (d.Blocks[pos.Block + 1] is PageBreak) { d.Blocks.RemoveAt(pos.Block + 1); return pos; }
        if (d.Blocks[pos.Block + 1] is Para) d.MergeWithPrevious(pos.Block + 1);
        return pos;
    }

    /// <summary>Tabulator in einer Tabelle: nächste (bzw. vorige) Zelle; hinter der letzten Zelle entsteht eine neue Zeile.</summary>
    public static TextPos NextCell(TextDoc d, TextPos pos, int dir)
    {
        if (!pos.InCell || d.Blocks[pos.Block] is not Table t) return pos;
        var cells = CellsOf(t).ToList(); var i = cells.IndexOf((pos.Row, pos.Col)) + dir;
        if (i < 0) return pos;
        if (i >= cells.Count) { t.InsertRow(t.Rows.Count); cells = CellsOf(t).ToList(); if (i >= cells.Count) return pos; }
        return new TextPos(pos.Block, t.Text(cells[i].Row, cells[i].Col).Length, cells[i].Row, cells[i].Col);
    }
}
