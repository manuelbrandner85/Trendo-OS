using System.Globalization;
using Trendo.Core.Cad;

namespace Trendo.Core;

/// <summary>T-0759: Sonderfälle beim Fräsen aus Zeichnungen – Hilfslinien-Ebenen (Bemaßung, Mittellinien), Zeichnungsrahmen mit Schriftfeld,
/// Linienverbünde, die (auch über Kreuzungen und über Ebenen hinweg) zusammen mit geschlossenen Konturen einen Bereich einschließen (Brücke
/// zwischen zwei Platten, U mit sich kreuzenden Überständen, Sehne auf anderer Ebene). Grundsatz: Wo eine Deutung nicht eindeutig ist, wird
/// ausgelassen und gemeldet; kein Loch darf je als Außenkontur gefräst werden.</summary>
public static partial class CncCam
{
    // ---------------------------------------------------------------- Meldungen zusammenfassen

    /// <summary>T-0759-fix1: Meldungsarten, die gekürzt werden dürfen – nur Massenmeldungen zu offenen Linien und Spalten, nie Meldungen zu geschlossenen
    /// Konturen (Loch oder Platte ausgelassen, zu klein, zu nah …). Art = Meldungstyp (Muster), Grund für die Zusatzzeile.</summary>
    static readonly (System.Text.RegularExpressions.Regex Rx, string Kind, string Why)[] Summable =
    [
        (new(@"^Kontur \d+( \(Ebene [^)]*\))? ausgelassen: offene Kontur \(Enden ", System.Text.RegularExpressions.RegexOptions.Compiled), "offene Kontur ausgelassen",
            "offene Linien werden nur in der Fräsart „Auf der Linie (Gravur)“ gefräst"),
        (new(@"^Mehrdeutiger Spalt bei X ", System.Text.RegularExpressions.RegexOptions.Compiled), "Mehrdeutiger Spalt",
            "mehrere Linienenden liegen etwa gleich nah und wurden nicht verbunden"),
    ];

    /// <summary>T-0759: Meldungen für die Anzeige zusammenfassen – je Art höchstens <paramref name="perKind"/> Einzelmeldungen in der ursprünglichen
    /// Reihenfolge, danach eine Zeile „… und N weitere Meldungen der Art … (Grund)“. Gekürzt werden nur die Arten in <see cref="Summable"/>;
    /// alle anderen Meldungen (besonders zu Löchern und Platten) bleiben vollständig. <see cref="CamResult.Warnings"/> selbst bleibt unverändert.</summary>
    public static List<string> SummarizeWarnings(IReadOnlyList<string> warnings, int perKind = 20)
    {
        if (warnings.Count <= perKind) return [.. warnings];
        var count = new int[Summable.Length]; var lastAt = new int[Summable.Length];
        var res = new List<string>();
        foreach (var w in warnings)
        {
            var k = Array.FindIndex(Summable, t => t.Rx.IsMatch(w));
            if (k < 0) { res.Add(w); continue; }
            if (count[k]++ < perKind) { res.Add(w); lastAt[k] = res.Count; }
        }
        // Zusatzzeilen hinter der letzten gezeigten Meldung der jeweiligen Art einfügen (von hinten, damit die Positionen stimmen)
        foreach (var k in Enumerable.Range(0, Summable.Length).Where(k => count[k] > perKind).OrderByDescending(k => lastAt[k]))
            res.Insert(lastAt[k], $"… und {(count[k] - perKind).ToString("N0", CultureInfo.GetCultureInfo("de-DE"))} weitere Meldungen der Art „{Summable[k].Kind}“ ({Summable[k].Why}).");
        return res;
    }

    // ---------------------------------------------------------------- Hilfslinien-Ebenen

    /// <summary>Ebenennamen (klein, Umlaute aufgelöst, nur Buchstaben), die eindeutig Bemaßung, Mittel- oder Hilfslinien bezeichnen – auch als Teil eines
    /// zusammengesetzten Namens (z. B. „A-ANNO-DIMS“, „Bemaßung_2“).</summary>
    static readonly HashSet<string> AnnotationStrong = new(StringComparer.Ordinal)
    {
        "defpoints", "bemassung", "bemassungen", "masslinie", "masslinien", "masskette", "massketten", "dimensioning", "dims",
        "mittellinie", "mittellinien", "mittelachse", "mittelachsen", "achskreuz", "centerline", "centerlines", "centreline", "centrelines",
        "hilfslinie", "hilfslinien", "konstruktionslinie", "konstruktionslinien",
    };

    /// <summary>Kurze oder mehrdeutige Namen, die nur als ganzer Ebenenname zählen – ohne angehängte Ziffern oder Wörter („BEM“, „MITTE“; nicht
    /// „Teil-Mitte“, „Achse 1“, „Dimension 3mm“). T-0759-fix1: „dimension“ zählt nur noch so, nicht mehr als Wort in einem längeren Namen.</summary>
    static readonly HashSet<string> AnnotationWhole = new(StringComparer.Ordinal) { "bem", "bema", "dim", "mitte", "achse", "achsen", "center", "centre", "dimension", "dimensions" };

    static readonly HashSet<string> FrameNames = new(StringComparer.Ordinal)
    {
        "rahmen", "zeichnungsrahmen", "blattrahmen", "schriftfeld", "plankopf", "blattrand", "frame", "border", "titleblock", "titelblock", "drawingframe", "sheetframe",
    };

    static (string Whole, string[] Tokens) LayerWords(string layer)
    {
        var low = layer.ToLowerInvariant().Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss");
        var tokens = new List<string>(); var cur = new System.Text.StringBuilder();
        foreach (var ch in low) { if (ch is >= 'a' and <= 'z') cur.Append(ch); else if (cur.Length > 0) { tokens.Add(cur.ToString()); cur.Clear(); } }
        if (cur.Length > 0) tokens.Add(cur.ToString());
        return (string.Concat(tokens), tokens.ToArray());
    }

    /// <summary>Ebenenname klein, Umlaute aufgelöst, ohne Rand-Leerzeichen – für Kurznamen, die nur ganz gelten (Ziffern und Zusätze zählen mit).</summary>
    static string LayerExact(string layer) => layer.Trim().ToLowerInvariant().Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss");

    /// <summary>T-0759: Ebene, deren Name eindeutig Bemaßung, Mittel- oder Hilfslinien bezeichnet (Defpoints, Bemaßung, BEM, Mittellinie, MITTE, DIMS …).
    /// Offene Linien darauf sind kein Hindernis für Fräsbahnen; sie zählen aber weiter, wenn sie mit anderen Linien oder Konturen einen Bereich
    /// einschließen (falls es doch Kanten sind). Geschlossene Konturen darauf werden nicht gefräst, und was in oder an ihnen liegt, wird ausgelassen.</summary>
    public static bool IsAnnotationLayer(string layer)
    {
        if (string.IsNullOrWhiteSpace(layer)) return false;
        var (whole, tokens) = LayerWords(layer);
        return AnnotationWhole.Contains(LayerExact(layer)) || AnnotationStrong.Contains(whole) || tokens.Any(AnnotationStrong.Contains);
    }

    static bool IsFrameLayer(string layer)
    {
        if (string.IsNullOrWhiteSpace(layer)) return false;
        // T-0759-fix1: nur der ganze Name als Wort („Rahmen“, „FRAME“, „Zeichnungsrahmen“) – nicht „Rahmen-Teil 1“ oder „Rahmenplatte“
        return FrameNames.Contains(LayerExact(layer));
    }

    // ---------------------------------------------------------------- Zeichnungsrahmen

    /// <summary>T-0759-fix1: Lage aller Texte (TEXT/MTEXT) einer Zeichnung – für <see cref="CamSettings.Texts"/> (Beleg für ein Schriftfeld).</summary>
    public static List<(double X, double Y)> TextPoints(CadDrawing d) =>
        d.Entities.Where(e => e.Kind == "text" && e.Points.Count > 0 && e.Points[0].Length >= 2 && double.IsFinite(e.Points[0][0]) && double.IsFinite(e.Points[0][1]))
            .Select(e => (e.Points[0][0], e.Points[0][1])).ToList();

    /// <summary>Blattgrößen A0–A4 (mm) – Rahmen auf der Blattkante oder mit üblichem Heftrand (ISO 5457: 20 mm links, 10 mm sonst; auch 10 mm rundum).</summary>
    static readonly (double W, double H)[] Sheets = [(210, 297), (297, 420), (420, 594), (594, 841), (841, 1189)];

    static bool StandardSize(double w, double h)
    {
        foreach (var (a, b) in Sheets)
            foreach (var (W, H) in new[] { (a, b), (b, a) })
                foreach (var (dw, dh) in new[] { (0.0, 0.0), (20.0, 20.0), (30.0, 20.0), (20.0, 30.0) })
                    if (Math.Abs(w - (W - dw)) <= 1 && Math.Abs(h - (H - dh)) <= 1) return true;
        return false;
    }

    /// <summary>T-0759: Zeichnungsrahmen mit Schriftfeld erkennen und weglassen. Nur wenn alles eindeutig ist:
    /// 1. ein Verbund (Linien-Stoß-Gruppe oder geschlossenes Rechteck samt daran hängender Linien) umfasst die ganze Zeichnung,
    /// 2. er besteht nur aus waagrechten und senkrechten Strecken, sein Rand ist ein vollständiges Rechteck (Spalte höchstens <see cref="ChainTol"/>),
    /// 3. alle übrigen Strecken bilden ein Schriftfeld in einer Ecke (höchstens ¾ der Breite und ½ der Höhe, die beiden inneren Kanten durchgezogen),
    /// 4. Ebenenname genau „Rahmen“/„Schriftfeld“/„Frame“ … – oder genormte Blatt-/Rahmengröße (A0–A4) mit Schriftfeld 170–190 mm breit (ISO 7200: 180 mm),
    ///    und in jedem Fall (T-0759-fix1) Text im Schriftfeld und ein unterteiltes Schriftfeld; fehlt das, werden die Konturen darin ausgelassen
    ///    („Rahmen oder Platte nicht eindeutig“) statt sie als Teile außen zu fräsen,
    /// 5. jede andere Kontur und jedes nicht gelesene Element liegt mit Abstand im Zeichenfeld (nicht am Rahmen, nicht im Schriftfeld), und es gibt
    ///    mindestens eine geschlossene Kontur darin.
    /// Sonst bleibt alles, wie es ist (ein Rahmen aus Linien mit Stößen lässt die Teile darin dann vorsichtshalber aus).</summary>
    static IReadOnlyList<CadContour> DropFrame(IReadOnlyList<CadContour> src, CamSettings s, List<string> warn, HashSet<int> unsureKids, CancellationToken ct)
    {
        const double tol = ChainTol + 1e-9;
        if (src.Count < 2) return src;
        double bx0 = double.MaxValue, by0 = double.MaxValue, bx1 = double.MinValue, by1 = double.MinValue;
        foreach (var c in src) foreach (var (x, y) in c.Pts) { bx0 = Math.Min(bx0, x); by0 = Math.Min(by0, y); bx1 = Math.Max(bx1, x); by1 = Math.Max(by1, y); }
        var unread = new List<CadDxf.Box>();
        foreach (var (_, ub) in s.Unread)
        {
            if (ub.IsEmpty) continue;
            var u = ub.ToSafe(); if (u.IsAll) return src; // Ausdehnung unbekannt: nicht entscheidbar
            unread.Add(u);
        }
        bool SameBox((double X0, double Y0, double X1, double Y1) b) => Math.Abs(b.X0 - bx0) <= tol && Math.Abs(b.Y0 - by0) <= tol && Math.Abs(b.X1 - bx1) <= tol && Math.Abs(b.Y1 - by1) <= tol;
        (double, double, double, double) BoxOf(IEnumerable<int> idx)
        {
            double a0 = double.MaxValue, a1 = double.MaxValue, a2 = double.MinValue, a3 = double.MinValue;
            foreach (var i in idx) foreach (var (x, y) in src[i].Pts) { a0 = Math.Min(a0, x); a1 = Math.Min(a1, y); a2 = Math.Max(a2, x); a3 = Math.Max(a3, y); }
            return (a0, a1, a2, a3);
        }
        static bool Axis((double X, double Y) a, (double X, double Y) b) => Math.Abs(a.X - b.X) <= 1e-6 || Math.Abs(a.Y - b.Y) <= 1e-6;
        bool AllAxis(int i)
        {
            var p = src[i].Pts; var n = src[i].Closed ? p.Count : p.Count - 1;
            for (var k = 0; k < n; k++) if (!Axis(p[k], p[(k + 1) % p.Count])) return false;
            return true;
        }
        var openAxis = Enumerable.Range(0, src.Count).Where(j => !src[j].Closed && src[j].JunctionGroup < 0 && AllAxis(j)).ToList();
        if (openAxis.Count > 5000) return src;
        // daran (und aneinander) hängende waagrechte/senkrechte Linien mit beiden Enden am Verbund gehören zum Rahmen (Schriftfeld-Teilung)
        List<int> Close(List<int> set)
        {
            var inSet = set.ToHashSet();
            bool Touches((double X, double Y) q)
            {
                ct.ThrowIfCancellationRequested();
                foreach (var j in set)
                {
                    var p = src[j].Pts; var n = src[j].Closed ? p.Count : p.Count - 1;
                    for (var k = 0; k < n; k++) if (SegDist(q, p[k], p[(k + 1) % p.Count]) <= tol) return true;
                }
                return false;
            }
            for (var changed = true; changed;)
            {
                ct.ThrowIfCancellationRequested(); changed = false;
                foreach (var j in openAxis)
                    if (!inSet.Contains(j) && Touches(src[j].Pts[0]) && Touches(src[j].Pts[^1])) { set.Add(j); inSet.Add(j); changed = true; }
            }
            return set;
        }
        var cands = new List<List<int>>();
        foreach (var g in Enumerable.Range(0, src.Count).Where(i => src[i].JunctionGroup >= 0).GroupBy(i => src[i].JunctionGroup))
            if (SameBox(BoxOf(g))) cands.Add(Close([.. g]));
        for (var i = 0; i < src.Count; i++)
            if (src[i].Closed && SameBox(BoxOf([i])) && AllAxis(i)) cands.Add(Close([i]));
        foreach (var f in cands)
        {
            ct.ThrowIfCancellationRequested();
            var fset = f.ToHashSet();
            if (!f.All(AllAxis)) continue;
            // Strecken: Rand oder innen
            var sides = new List<(double Lo, double Hi)>[4]; for (var k = 0; k < 4; k++) sides[k] = [];
            var inner = new List<((double X, double Y) A, (double X, double Y) B)>();
            foreach (var i in f)
            {
                var p = src[i].Pts; var n = src[i].Closed ? p.Count : p.Count - 1;
                for (var k = 0; k < n; k++)
                {
                    var a = p[k]; var b = p[(k + 1) % p.Count];
                    if (D2(a, b) < 1e-18) continue;
                    var vert = Math.Abs(a.X - b.X) <= 1e-6;
                    if (vert && Math.Abs(a.X - bx0) <= tol) sides[0].Add((Math.Min(a.Y, b.Y), Math.Max(a.Y, b.Y)));
                    else if (vert && Math.Abs(a.X - bx1) <= tol) sides[1].Add((Math.Min(a.Y, b.Y), Math.Max(a.Y, b.Y)));
                    else if (!vert && Math.Abs(a.Y - by0) <= tol) sides[2].Add((Math.Min(a.X, b.X), Math.Max(a.X, b.X)));
                    else if (!vert && Math.Abs(a.Y - by1) <= tol) sides[3].Add((Math.Min(a.X, b.X), Math.Max(a.X, b.X)));
                    else inner.Add((a, b));
                }
            }
            static bool Covers(List<(double Lo, double Hi)> iv, double lo, double hi, double gap)
            {
                if (iv.Count == 0) return false;
                iv.Sort((x, y) => x.Lo.CompareTo(y.Lo)); var cur = lo;
                foreach (var (a, b) in iv) { if (a > cur + gap) return false; cur = Math.Max(cur, b); }
                return cur >= hi - gap;
            }
            if (!Covers(sides[0], by0, by1, tol) || !Covers(sides[1], by0, by1, tol) || !Covers(sides[2], bx0, bx1, tol) || !Covers(sides[3], bx0, bx1, tol)) continue;
            if (inner.Count == 0) continue; // ohne Schriftfeld nicht von einer Platte zu unterscheiden
            double tx0 = inner.Min(e => Math.Min(e.A.X, e.B.X)), tx1 = inner.Max(e => Math.Max(e.A.X, e.B.X)), ty0 = inner.Min(e => Math.Min(e.A.Y, e.B.Y)), ty1 = inner.Max(e => Math.Max(e.A.Y, e.B.Y));
            var right = Math.Abs(tx1 - bx1) <= tol; var left = Math.Abs(tx0 - bx0) <= tol; var bottom = Math.Abs(ty0 - by0) <= tol; var top = Math.Abs(ty1 - by1) <= tol;
            if (right == left || bottom == top) continue; // genau eine Ecke
            double W = bx1 - bx0, H = by1 - by0, tw = tx1 - tx0, th = ty1 - ty0;
            if (tw > 0.75 * W || th > 0.5 * H || tw < 10 || th < 5) continue;
            // die beiden inneren Kanten des Schriftfelds durchgezogen
            var vx = right ? tx0 : tx1; var hy = bottom ? ty1 : ty0;
            var vIv = inner.Where(e => Math.Abs(e.A.X - e.B.X) <= 1e-6 && Math.Abs(e.A.X - vx) <= tol).Select(e => (Math.Min(e.A.Y, e.B.Y), Math.Max(e.A.Y, e.B.Y))).ToList();
            var hIv = inner.Where(e => Math.Abs(e.A.Y - e.B.Y) <= 1e-6 && Math.Abs(e.A.Y - hy) <= tol).Select(e => (Math.Min(e.A.X, e.B.X), Math.Max(e.A.X, e.B.X))).ToList();
            if (!Covers(vIv, ty0, ty1, tol) || !Covers(hIv, tx0, tx1, tol)) continue;
            var named = f.All(i => IsFrameLayer(src[i].Layer));
            var sheet = StandardSize(W, H) && tw >= 170 && tw <= 190;
            if (!named && !sheet) continue; // kein Hinweis auf einen Rahmen: Platte (wie bisher)
            // T-0759-fix1: Von der Gestalt her sind „Platte mit Löchern“ und „Rahmen mit Teilen“ dasselbe. Name oder Blattgröße allein reichen nie; als Rahmen
            // gilt er nur, wenn zusätzlich Text im Schriftfeld steht und das Schriftfeld unterteilt ist (mindestens eine weitere Linie darin).
            bool InTitle((double X, double Y) q) => q.X >= tx0 - tol && q.X <= tx1 + tol && q.Y >= ty0 - tol && q.Y <= ty1 + tol;
            var texts = s.Texts.Count(InTitle);
            var divided = inner.Any(e =>
                !(Math.Abs(e.A.X - e.B.X) <= 1e-6 && Math.Abs(e.A.X - vx) <= tol) && !(Math.Abs(e.A.Y - e.B.Y) <= 1e-6 && Math.Abs(e.A.Y - hy) <= tol) && InTitle(e.A) && InTitle(e.B));
            var sure = texts > 0 && divided;
            // Inhalt: mit Abstand im Zeichenfeld, nicht im Schriftfeld
            bool Clear(double x0, double y0, double x1, double y1) =>
                x0 > bx0 + tol && y0 > by0 + tol && x1 < bx1 - tol && y1 < by1 - tol && (x1 < tx0 - tol || x0 > tx1 + tol || y1 < ty0 - tol || y0 > ty1 + tol);
            var ok = true; var closedInside = 0;
            for (var i = 0; i < src.Count && ok; i++)
            {
                if ((i & 1023) == 0) ct.ThrowIfCancellationRequested();
                if (fset.Contains(i)) continue;
                var (x0, y0, x1, y1) = BoxOf([i]);
                if (!Clear(x0, y0, x1, y1)) ok = false;
                if (src[i].Closed) closedInside++;
            }
            foreach (var u in unread) if (!Clear(u.MinX, u.MinY, u.MaxX, u.MaxY)) ok = false;
            if (!ok || closedInside == 0) continue;
            if (!sure)
            {
                // Rahmen vermutet, aber nicht zweifelsfrei: nichts weglassen, die geschlossenen Konturen darin auslassen (als Teil wären sie außen, als Loch
                // innen zu fräsen – im Zweifel lieber kein G-Code als ein Loch außen).
                for (var i = 0; i < src.Count; i++) if (!fset.Contains(i) && src[i].Closed) unsureKids.Add(i);
                warn.Add($"Rahmen oder Platte nicht eindeutig ({F(W, "0.#")} × {F(H, "0.#")} mm mit abgeteiltem Eckfeld{(named ? $", Ebene {src[f[0]].Layer}" : ", Blattgröße")}; "
                    + $"{(texts == 0 ? "kein Text im Schriftfeld" : "")}{(texts == 0 && !divided ? ", " : "")}{(!divided ? "Schriftfeld nicht unterteilt" : "")}) – der Umriss wird als Platte gefräst, "
                    + "die Konturen darin werden ausgelassen (als Teile wären sie außen, als Löcher innen zu fräsen). Ist es eine Platte: Ebene umbenennen; ist es ein Zeichnungsrahmen: Rahmen und Schriftfeld vor dem Fräsen löschen.");
                return src;
            }
            warn.Add($"Zeichnungsrahmen mit Schriftfeld erkannt ({F(W, "0.#")} × {F(H, "0.#")} mm, {f.Count} Linienzug/-züge{(named ? $", Ebene {src[f[0]].Layer}" : ", Blattgröße")}, Text im unterteilten Schriftfeld) – er wird nicht gefräst, die Teile darin werden normal gefräst.");
            return src.Where((_, i) => !fset.Contains(i)).ToList();
        }
        return src;
    }

    // ---------------------------------------------------------------- offene Linienverbünde

    /// <summary>T-0759: offene Linienzüge, die sich an Enden, mitten auf einer Linie oder über Kreuzungen berühren (Abstand höchstens <see cref="ChainTol"/>,
    /// über Ebenen hinweg), bilden einen Verbund. <see cref="Cyclic"/>: der Verbund schließt für sich einen Bereich ein (Zyklenzahl des Berührgraphen &gt; 0).</summary>
    sealed class OpenNet
    {
        public int[] Comp = [];
        public List<List<int>> Members = [];
        public bool[] Cyclic = [];
        public bool Exhausted;
        /// <summary>Berührungen zwischen zwei Linienzügen: Kontur, Stelle (Strecke + t) auf ihr.</summary>
        public List<(int A, double GA, int B, double GB)> Events = [];
    }

    static (double T, double U) ClosestParams((double X, double Y) a, (double X, double Y) b, (double X, double Y) c, (double X, double Y) d)
    {
        double rx = b.X - a.X, ry = b.Y - a.Y, sx = d.X - c.X, sy = d.Y - c.Y, den = rx * sy - ry * sx;
        if (Math.Abs(den) > 1e-18)
        {
            double qx = c.X - a.X, qy = c.Y - a.Y, t = (qx * sy - qy * sx) / den, u = (qx * ry - qy * rx) / den;
            if (t >= 0 && t <= 1 && u >= 0 && u <= 1) return (t, u);
        }
        static double Proj((double X, double Y) p, (double X, double Y) a, (double X, double Y) b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y, l2 = dx * dx + dy * dy;
            return l2 < 1e-24 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / l2, 0, 1);
        }
        (double X, double Y) At((double X, double Y) a, (double X, double Y) b, double t) => (a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
        var best = (T: 0.0, U: Proj(a, c, d)); var bd = D2(a, At(c, d, best.U));
        void Try(double t, double u) { var dd = D2(At(a, b, t), At(c, d, u)); if (dd < bd) { bd = dd; best = (t, u); } }
        Try(1, Proj(b, c, d)); Try(Proj(c, a, b), 0); Try(Proj(d, a, b), 1);
        return best;
    }

    /// <summary>Verbünde offener Linienzüge bestimmen. <paramref name="use"/>: welche Konturen mitzählen. Ist das Arbeitsbudget erschöpft, gilt
    /// <see cref="OpenNet.Exhausted"/> – der Aufrufer bleibt dann beim bisherigen, vorsichtigen Verhalten.</summary>
    /// <summary>Höchstzahl der Berührstellen; darüber gilt das Arbeitsbudget als erschöpft (sichere Folge: bisheriges Verhalten).</summary>
    const int MaxEvents = 1_000_000;

    static OpenNet AnalyzeOpen(IReadOnlyList<CadContour> cs, Func<int, bool> use, double tol, long maxWork, CancellationToken ct)
    {
        var net = new OpenNet { Comp = Enumerable.Repeat(-1, cs.Count).ToArray() };
        var pieces = Enumerable.Range(0, cs.Count).Where(use).ToList();
        if (pieces.Count == 0) return net;
        var sa = new List<(double X, double Y)>(); var sb = new List<(double X, double Y)>(); var sp = new List<int>(); var sk = new List<int>();
        var cum = new Dictionary<int, double[]>(); double total = 0;
        foreach (var j in pieces)
        {
            var p = cs[j].Pts; var c = new double[p.Count];
            for (var k = 0; k + 1 < p.Count; k++) { var l = Math.Sqrt(D2(p[k], p[k + 1])); c[k + 1] = c[k] + l; total += l; sa.Add(p[k]); sb.Add(p[k + 1]); sp.Add(j); sk.Add(k); }
            cum[j] = c;
        }
        double Arc(int j, double g) { var c = cum[j]; var k = Math.Clamp((int)Math.Floor(g), 0, Math.Max(0, c.Length - 2)); return c.Length < 2 ? 0 : c[k] + (c[k + 1] - c[k]) * (g - k); }
        var nseg = sa.Count; long work = 0;
        var cell = Math.Max(4 * tol, 4 * total / 200_000.0); // höchstens etwa 200 000 Rastereinträge (lange Linien: gröberes Raster)
        static long Key(long x, long y) => (x << 32) ^ (y & 0xFFFFFFFFL);
        long C(double v) => (long)Math.Floor(v / cell);
        var map = new Dictionary<long, List<int>>();
        IEnumerable<(long X, long Y)> Cells(int i)
        {
            var a = sa[i]; var b = sb[i]; var n = (int)Math.Min(1_000_000, Math.Ceiling(Math.Sqrt(D2(a, b)) / (cell / 4)));
            long lx = long.MinValue, ly = long.MinValue;
            for (var q = 0; q <= n; q++)
            {
                var f = n == 0 ? 0 : (double)q / n; var cx = C(a.X + (b.X - a.X) * f); var cy = C(a.Y + (b.Y - a.Y) * f);
                if (cx == lx && cy == ly) continue; lx = cx; ly = cy; yield return (cx, cy);
            }
        }
        for (var i = 0; i < nseg; i++)
        {
            if ((i & 1023) == 0) ct.ThrowIfCancellationRequested();
            foreach (var (cx, cy) in Cells(i))
            {
                if (++work > maxWork) { net.Exhausted = true; return net; }
                if ((work & 4095) == 0) ct.ThrowIfCancellationRequested();
                var k = Key(cx, cy); if (!map.TryGetValue(k, out var l)) map[k] = l = [];
                if (l.Count == 0 || l[^1] != i) l.Add(i);
            }
        }
        var selfCyc = new HashSet<int>();
        var ev = new List<(int SA, int SB, double T, double U, double X, double Y)>();
        var stamp = new int[nseg]; Array.Fill(stamp, -1);
        for (var i = 0; i < nseg; i++)
        {
            if ((i & 255) == 0) ct.ThrowIfCancellationRequested();
            var a = sa[i]; var b = sb[i];
            double ax0 = Math.Min(a.X, b.X) - tol, ax1 = Math.Max(a.X, b.X) + tol, ay0 = Math.Min(a.Y, b.Y) - tol, ay1 = Math.Max(a.Y, b.Y) + tol;
            foreach (var (cx, cy) in Cells(i))
                for (var dx = -1L; dx <= 1; dx++)
                    for (var dy = -1L; dy <= 1; dy++)
                    {
                        if (!map.TryGetValue(Key(cx + dx, cy + dy), out var l)) continue;
                        foreach (var j in l)
                        {
                            if (j <= i || stamp[j] == i) continue;
                            stamp[j] = i;
                            if (++work > maxWork) { net.Exhausted = true; return net; }
                            if ((work & 4095) == 0) ct.ThrowIfCancellationRequested();
                            var c = sa[j]; var d = sb[j];
                            if (Math.Max(c.X, d.X) < ax0 || Math.Min(c.X, d.X) > ax1 || Math.Max(c.Y, d.Y) < ay0 || Math.Min(c.Y, d.Y) > ay1) continue;
                            int pi = sp[i], pj = sp[j];
                            if (pi == pj && Math.Abs(sk[i] - sk[j]) <= 1) continue;
                            if (SegSeg(a, b, c, d) > tol) continue;
                            var (t, u) = ClosestParams(a, b, c, d);
                            if (pi == pj) { if (Math.Abs(Arc(pi, sk[i] + t) - Arc(pj, sk[j] + u)) > 3 * tol) selfCyc.Add(pi); continue; }
                            var px = a.X + (b.X - a.X) * t; var py = a.Y + (b.Y - a.Y) * t;
                            ev.Add((i, j, t, u, px, py));
                            if (ev.Count > MaxEvents) { net.Exhausted = true; return net; } // sehr viele Kreuzungen (z. B. tausende Linien durch einen Punkt): wie bisher
                        }
                    }
        }
        // Berührstellen: Ereignisse an derselben Stelle (≤ tol) oder entlang benachbarter Strecken desselben Paares gehören zusammen
        var m = ev.Count; var up = new int[m]; for (var i = 0; i < m; i++) up[i] = i;
        int Find(int x) { var r = x; while (up[r] != r) r = up[r]; while (up[x] != r) { var q = up[x]; up[x] = r; x = q; } return r; }
        void Union(int x, int y) { x = Find(x); y = Find(y); if (x != y) up[x] = y; }
        var byPair = new Dictionary<(int, int), List<int>>();
        for (var e = 0; e < m; e++) { if ((e & 4095) == 0) ct.ThrowIfCancellationRequested(); var (pa, pb) = (sp[ev[e].SA], sp[ev[e].SB]); var key = pa < pb ? (pa, pb) : (pb, pa); if (!byPair.TryGetValue(key, out var l)) byPair[key] = l = []; l.Add(e); }
        foreach (var (key, l) in byPair)
        {
            if (l.Count < 2) continue;
            ct.ThrowIfCancellationRequested();
            int SegOf(int e, int piece) => sp[ev[e].SA] == piece ? sk[ev[e].SA] : sk[ev[e].SB];
            l.Sort((x, y) => SegOf(x, key.Item1).CompareTo(SegOf(y, key.Item1)));
            for (var q = 0; q < l.Count; q++)
                for (var w = q + 1; w < l.Count && SegOf(l[w], key.Item1) - SegOf(l[q], key.Item1) <= 1; w++)
                {
                    if (++work > maxWork) { net.Exhausted = true; return net; }
                    if (Math.Abs(SegOf(l[w], key.Item2) - SegOf(l[q], key.Item2)) <= 1) Union(l[q], l[w]);
                }
        }
        // Ort: Zellen der Größe tol/√2 – zwei Stellen in derselben Zelle sind immer ≤ tol und werden sofort vereinigt; zwischen Nachbarzellen (±2) wird nur
        // gesucht, solange die Wurzeln verschieden sind, und beim ersten Paar ≤ tol aufgehört (linear, auch wenn sich sehr viele Linien in einem Punkt kreuzen)
        var lc = Math.Max(tol / Math.Sqrt(2), 1e-9); var lmap = new Dictionary<long, List<int>>();
        for (var e = 0; e < m; e++)
        {
            if ((e & 4095) == 0) ct.ThrowIfCancellationRequested();
            var k = Key((long)Math.Floor(ev[e].X / lc), (long)Math.Floor(ev[e].Y / lc));
            if (!lmap.TryGetValue(k, out var l)) lmap[k] = l = []; else Union(l[0], e);
            l.Add(e);
        }
        foreach (var (k, l) in lmap)
        {
            if ((++work & 4095) == 0) ct.ThrowIfCancellationRequested();
            var (cx, cy) = (k >> 32, (long)(int)(k & 0xFFFFFFFFL));
            for (var dx = 0L; dx <= 2; dx++)
                for (var dy = -2L; dy <= 2; dy++)
                {
                    if (dx == 0 && dy <= 0) continue;
                    if (!lmap.TryGetValue(Key(cx + dx, cy + dy), out var l2) || Find(l[0]) == Find(l2[0])) continue;
                    var hit = false;
                    foreach (var e in l)
                    {
                        foreach (var f in l2) { if (++work > maxWork) { net.Exhausted = true; return net; } if (D2((ev[e].X, ev[e].Y), (ev[f].X, ev[f].Y)) <= tol * tol) { hit = true; break; } }
                        if (hit) break;
                    }
                    if (hit) Union(l[0], l2[0]);
                }
        }
        // Verbünde (Union-Find über Linienzüge)
        var pidx = new Dictionary<int, int>(); for (var q = 0; q < pieces.Count; q++) pidx[pieces[q]] = q;
        var pu = new int[pieces.Count]; for (var q = 0; q < pu.Length; q++) pu[q] = q;
        int PF(int x) { var r = x; while (pu[r] != r) r = pu[r]; while (pu[x] != r) { var q = pu[x]; pu[x] = r; x = q; } return r; }
        foreach (var e in ev) { var a = PF(pidx[sp[e.SA]]); var b = PF(pidx[sp[e.SB]]); if (a != b) pu[a] = b; }
        var compOf = new Dictionary<int, int>();
        for (var q = 0; q < pieces.Count; q++)
        {
            var r = PF(q); if (!compOf.TryGetValue(r, out var c)) { compOf[r] = c = net.Members.Count; net.Members.Add([]); }
            net.Comp[pieces[q]] = c; net.Members[c].Add(pieces[q]);
        }
        var nc = net.Members.Count; var inc = new long[nc]; var nodes = new long[nc]; var cyc = new bool[nc];
        foreach (var j in selfCyc) cyc[net.Comp[j]] = true;
        var byNode = new Dictionary<int, List<int>>();
        for (var e = 0; e < m; e++) { if ((e & 4095) == 0) ct.ThrowIfCancellationRequested(); var r = Find(e); if (!byNode.TryGetValue(r, out var l)) byNode[r] = l = []; l.Add(e); }
        foreach (var (_, l) in byNode)
        {
            ct.ThrowIfCancellationRequested();
            var comp = net.Comp[sp[ev[l[0]].SA]]; nodes[comp]++;
            var at = new Dictionary<int, List<double>>(); double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue; var pairs = new HashSet<(int, int)>();
            foreach (var e in l)
            {
                var (s1, s2, t, u, x, y) = ev[e];
                foreach (var (sg, par) in new[] { (s1, t), (s2, u) }) { var j = sp[sg]; if (!at.TryGetValue(j, out var al)) at[j] = al = []; al.Add(Arc(j, sk[sg] + par)); }
                x0 = Math.Min(x0, x); y0 = Math.Min(y0, y); x1 = Math.Max(x1, x); y1 = Math.Max(y1, y);
                var (pa, pb) = (sp[s1], sp[s2]); pairs.Add(pa < pb ? (pa, pb) : (pb, pa));
            }
            foreach (var (_, al) in at)
            {
                al.Sort(); inc[comp]++;
                for (var q = 1; q < al.Count; q++) if (al[q] - al[q - 1] > 3 * tol) inc[comp]++; // dieselbe Linie an zwei Stellen dieser Berührung: Schleife
            }
            if (pairs.Count > 1 && (x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0) > 16 * tol * tol) cyc[comp] = true; // ausgedehnter Haufen: vorsichtig
        }
        net.Cyclic = new bool[nc];
        for (var c = 0; c < nc; c++) net.Cyclic[c] = cyc[c] || inc[c] - net.Members[c].Count - nodes[c] + 1 > 0;
        foreach (var e in ev) net.Events.Add((sp[e.SA], sk[e.SA] + e.T, sp[e.SB], sk[e.SB] + e.U));
        return net;
    }

    /// <summary>T-0759: Bereiche, die Verbünde offener Linien zusammen mit geschlossenen Konturen einschließen. Jeder Linienzug eines Verbunds wird an den
    /// Rändern der berührten geschlossenen Konturen geteilt (genaue Abstandsprüfung wie <c>SegOutside</c>: Kapsel um jede Randstrecke); Stücke am Rand
    /// (≤ tol) sind Anschlüsse, Stücke dazwischen gehören zu einem Teilverbund (verbunden über Berührungen der Linien untereinander). Ein Anschluss an
    /// Kontur X zählt nur, wenn das anschließende Stück außerhalb von X liegt (Linien ganz in einer Kontur sind Gravur von Rand zu Rand).
    /// Graph: Konturen und Teilverbünde, Kanten = Anschlüsse. Jeder Kreis darin schließt einen Bereich ein (U vor einer Kerbe: zweimal an derselben
    /// Kontur; Brücke: zwei Linien zwischen zwei Platten; auch über Kreuzungen und über Ebenen hinweg). Ergebnis: Gruppen (Konturen, Verbünde) auf
    /// Kreisen; <c>null</c>, wenn das Arbeitsbudget nicht reichte.</summary>
    static List<(List<int> Contours, List<int> Structs)>? Enclosures(IReadOnlyList<CadContour> cs, OpenNet net, SegGrid cg, Func<int, bool> closedOk,
        Func<int, (double X, double Y), bool> inC, Func<int, (double, double, double, double)> box, double tol, long maxWork, CancellationToken ct)
    {
        long work = 0; var R = tol; var cell = cg.CellSize;
        // Rahmenraster aller geschlossenen Konturen (erst bei Bedarf)
        Dictionary<(long, long), List<int>>? bgrid = null; double bcell = 1; var bgridBad = false;
        bool BuildB()
        {
            if (bgrid is not null) return !bgridBad;
            bgrid = [];
            var ids = Enumerable.Range(0, cs.Count).Where(closedOk).ToList();
            if (ids.Count == 0) return true;
            double x0 = ids.Min(i => box(i).Item1), y0 = ids.Min(i => box(i).Item2), x1 = ids.Max(i => box(i).Item3), y1 = ids.Max(i => box(i).Item4);
            bcell = Math.Max(1e-6, Math.Max(x1 - x0, y1 - y0) / Math.Max(1, Math.Sqrt(ids.Count)));
            foreach (var i in ids)
            {
                var b = box(i);
                for (var cx = (long)Math.Floor(b.Item1 / bcell); cx <= (long)Math.Floor(b.Item3 / bcell); cx++)
                    for (var cy = (long)Math.Floor(b.Item2 / bcell); cy <= (long)Math.Floor(b.Item4 / bcell); cy++)
                    {
                        if (++work > maxWork) { bgridBad = true; return false; }
                        if (!bgrid.TryGetValue((cx, cy), out var l)) bgrid[(cx, cy)] = l = []; l.Add(i);
                    }
            }
            return true;
        }
        // liegt ein Punkt in Luft (gerade Zahl umschließender Konturen)? Budget erschöpft: nein (Material – Anschluss zählt, vorsichtig)
        bool InAir((double X, double Y) q)
        {
            if (!BuildB()) return false;
            if (!bgrid!.TryGetValue(((long)Math.Floor(q.X / bcell), (long)Math.Floor(q.Y / bcell)), out var cand)) return true;
            var n = 0;
            foreach (var i in cand) { if (++work > maxWork) return false; var b = box(i); if (q.X >= b.Item1 && q.X <= b.Item3 && q.Y >= b.Item2 && q.Y <= b.Item4 && inC(i, q)) n++; }
            return n % 2 == 0;
        }
        // T-0759-fix1: liegt in Kontur x keine andere geschlossene Kontur (leeres Loch)? Budget erschöpft: nein (vorsichtig)
        var emptyOf = new Dictionary<int, bool>();
        bool Empty(int x)
        {
            if (emptyOf.TryGetValue(x, out var e)) return e;
            if (!BuildB()) return emptyOf[x] = false;
            var bx = box(x); var seen = new HashSet<int>();
            for (var cx = (long)Math.Floor(bx.Item1 / bcell); cx <= (long)Math.Floor(bx.Item3 / bcell); cx++)
                for (var cy = (long)Math.Floor(bx.Item2 / bcell); cy <= (long)Math.Floor(bx.Item4 / bcell); cy++)
                    if (bgrid!.TryGetValue((cx, cy), out var l))
                        foreach (var j in l)
                        {
                            if (++work > maxWork) return emptyOf[x] = false;
                            if (j == x || !seen.Add(j)) continue;
                            var bj = box(j);
                            if (bj.Item1 < bx.Item1 || bj.Item2 < bx.Item2 || bj.Item3 > bx.Item3 || bj.Item4 > bx.Item4) continue;
                            if (inC(x, cs[j].Pts[0])) return emptyOf[x] = false;
                        }
            return emptyOf[x] = true;
        }
        var evOf =new List<(int A, double GA, int B, double GB)>[net.Members.Count]; for (var q = 0; q < evOf.Length; q++) evOf[q] = [];
        for (var q = 0; q < net.Events.Count; q++) { if ((q & 4095) == 0) ct.ThrowIfCancellationRequested(); evOf[net.Comp[net.Events[q].A]].Add(net.Events[q]); }
        var compNodes = new List<(int Struct, HashSet<(int Zone, int Contour)> Att)>();
        for (var sIdx = 0; sIdx < net.Members.Count; sIdx++)
        {
            ct.ThrowIfCancellationRequested();
            var members = net.Members[sIdx];
            // 1. Randnahe Intervalle je Strecke
            var near = new Dictionary<(int Piece, int Seg), List<(double Lo, double Hi, int Tag)>>(); var K = new HashSet<int>();
            foreach (var j in members)
            {
                var p = cs[j].Pts;
                for (var k = 0; k + 1 < p.Count; k++)
                {
                    var a = p[k]; var b = p[k + 1]; double dx = b.X - a.X, dy = b.Y - a.Y, len2 = dx * dx + dy * dy;
                    var len = Math.Sqrt(len2); var nq = (long)Math.Ceiling(len / cell) + 1;
                    if (nq > 4_000_000) return null;
                    var seen = new HashSet<int>(); var iv = new List<(double Lo, double Hi, int Tag)>();
                    for (long q = 0; q <= nq; q++)
                    {
                        if (++work > maxWork) return null;
                        if ((q & 1023) == 0) ct.ThrowIfCancellationRequested();
                        double px = a.X + dx * q / nq, py = a.Y + dy * q / nq, hw = cell / 2 + R;
                        cg.Any(px - hw, py - hw, px + hw, py + hw, t =>
                        {
                            work++;
                            var sg = cg.Segs[t]; if (!closedOk(sg.Tag) || !seen.Add(t)) return false;
                            var c = CapsuleT(a, b, sg.A, sg.B, R);
                            if (c is { } cv) iv.Add((cv.Lo, cv.Hi, sg.Tag));
                            return false;
                        });
                    }
                    if (work > maxWork) return null;
                    if (iv.Count > 0) { near[(j, k)] = iv; foreach (var x in iv) K.Add(x.Tag); }
                }
            }
            if (K.Count == 0) continue;
            int[] InSet((double X, double Y) q) => K.Where(x => { var bx = box(x); return q.X >= bx.Item1 && q.X <= bx.Item3 && q.Y >= bx.Item2 && q.Y <= bx.Item4 && inC(x, q); }).OrderBy(x => x).ToArray();
            // 2. Läufe je Linienzug: Zone (am Rand) oder Lücke (mit der Menge der Konturen, in denen sie liegt)
            var runs = new List<(int Piece, double G0, double G1, bool Zone, HashSet<int> Tags, int[] In)>();
            var runsOf = new Dictionary<int, List<int>>();
            foreach (var j in members)
            {
                var p = cs[j].Pts; var my = runsOf[j] = [];
                void Push(double g0, double g1, bool zone, HashSet<int> tags, int[] inn)
                {
                    if (my.Count > 0)
                    {
                        var lst = runs[my[^1]];
                        if (lst.Zone == zone && Math.Abs(lst.G1 - g0) < 1e-12 && (zone || lst.In.SequenceEqual(inn)))
                        {
                            if (zone) lst.Tags.UnionWith(tags);
                            runs[my[^1]] = lst with { G1 = g1 }; return;
                        }
                    }
                    my.Add(runs.Count); runs.Add((j, g0, g1, zone, tags, inn));
                }
                for (var k = 0; k + 1 < p.Count; k++)
                {
                    if (++work > maxWork) return null;
                    var a = p[k]; var b = p[k + 1];
                    (double X, double Y) At(double t) => (a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
                    if (!near.TryGetValue((j, k), out var iv)) { Push(k, k + 1, false, [], InSet(At(0.5))); continue; }
                    iv.Sort((x, y) => x.Lo.CompareTo(y.Lo));
                    var cur = 0.0; var q = 0;
                    while (q < iv.Count)
                    {
                        var (lo, hi, tag) = iv[q];
                        if (lo > cur + 1e-12) Push(k + cur, k + lo, false, [], InSet(At((cur + lo) / 2)));
                        var tags = new HashSet<int> { tag }; var zHi = hi; q++;
                        while (q < iv.Count && iv[q].Lo <= zHi + 1e-12) { tags.Add(iv[q].Tag); zHi = Math.Max(zHi, iv[q].Hi); q++; }
                        Push(k + Math.Max(cur, lo), k + Math.Min(1, zHi), true, tags, []);
                        cur = Math.Max(cur, zHi);
                    }
                    if (cur < 1 - 1e-12) Push(k + cur, k + 1, false, [], InSet(At((cur + 1) / 2)));
                }
            }
            int RunAt(int piece, double g)
            {
                var l = runsOf[piece]; int lo = 0, hi = l.Count - 1;
                while (lo < hi) { var mid = (lo + hi) / 2; if (runs[l[mid]].G1 < g - 1e-12) lo = mid + 1; else hi = mid; }
                return l[lo];
            }
            // 3. Teilverbünde (Lücken) und Anschluss-Identitäten (Zonen) über Berührungen der Linien untereinander
            var nr = runs.Count; var ru = new int[nr]; for (var i = 0; i < nr; i++) ru[i] = i;
            int RF(int x) { var r = x; while (ru[r] != r) r = ru[r]; while (ru[x] != r) { var q = ru[x]; ru[x] = r; x = q; } return r; }
            void RU(int x, int y) { x = RF(x); y = RF(y); if (x != y) ru[x] = y; }
            var extra = new List<(int Gap, int Zone)>();
            foreach (var (pa, ga, pb, gb) in evOf[sIdx])
            {
                if (++work > maxWork) return null;
                if ((work & 4095) == 0) ct.ThrowIfCancellationRequested();
                var x = RunAt(pa, ga); var y = RunAt(pb, gb);
                var (zx, zy) = (runs[x].Zone, runs[y].Zone);
                if (zx == zy) RU(x, y); // Lücke–Lücke: derselbe Teilverbund; Zone–Zone: dieselbe Anschlussstelle
                else extra.Add(zx ? (y, x) : (x, y));
            }
            // Anschlüsse je Teilverbund: Nachbarzonen entlang des Linienzugs und über Berührungen, nur von außerhalb der Kontur
            var att = new Dictionary<int, HashSet<(int Zone, int Contour)>>();
            // Durchgang durch ein Loch: Rand X – Stück in X, das in Luft liegt (Loch) – Rand X. Eine Linie, die ein Loch ganz durchquert, ist dort keine
            // Kante (eine Kante in Luft trennt nichts) – sie wird wie Gravur von Rand zu Rand behandelt: beide Anschlüsse an X zählen nicht. Ein Überstand,
            // der in X endet, oder ein Durchgang durch Material (Platte) zählt weiter.
            var through = new HashSet<(int Zone, int Contour)>();
            var airOf = new Dictionary<(int, double), bool>();
            bool GapInAir((int Piece, double G0, double G1, bool Zone, HashSet<int> Tags, int[] In) gy)
            {
                if (airOf.TryGetValue((gy.Piece, gy.G0), out var v)) return v;
                var p = cs[gy.Piece].Pts; var gm = (gy.G0 + gy.G1) / 2; var k = Math.Clamp((int)Math.Floor(gm), 0, p.Count - 2); var tt = gm - k;
                return airOf[(gy.Piece, gy.G0)] = InAir((p[k].X + (p[k + 1].X - p[k].X) * tt, p[k].Y + (p[k + 1].Y - p[k].Y) * tt));
            }
            foreach (var j in members)
            {
                var l = runsOf[j];
                for (var q = 1; q + 1 < l.Count; q++)
                {
                    var (zx, gy, zz) = (runs[l[q - 1]], runs[l[q]], runs[l[q + 1]]);
                    if (!zx.Zone || gy.Zone || !zz.Zone) continue;
                    foreach (var tag in zx.Tags)
                    {
                        if (!zz.Tags.Contains(tag) || Array.BinarySearch(gy.In, tag) < 0) continue;
                        if (!GapInAir(gy)) continue;
                        // T-0759-fix1: nur ein leeres Loch – liegt eine Insel darin, könnten die Linien einen Steg über das Loch bilden (Prüferfälle t2, t6):
                        // dann zählen die Anschlüsse weiter (die Insel wird ausgelassen statt außen gefräst)
                        if (!Empty(tag)) continue;
                        through.Add((l[q - 1], tag)); through.Add((l[q + 1], tag));
                    }
                }
            }
            void Attach(int gap, int zone)
            {
                var g = runs[gap]; if (g.Zone) return;
                foreach (var tag in runs[zone].Tags)
                {
                    // Stück liegt in dieser Kontur: Gravur, kein Anschluss – außer es liegt in Luft in einem Loch mit Insel (T-0759-fix1: zwei Linien quer über das
                    // Loch können einen Steg bilden, die Insel darin wäre dann ein Loch im Steg)
                    if (Array.BinarySearch(g.In, tag) >= 0 && !(GapInAir(g) && !Empty(tag))) continue;
                    if (through.Contains((zone, tag))) continue; // T-0759: Linie durchquert hier ein Loch

                    var root = RF(gap); if (!att.TryGetValue(root, out var hs)) att[root] = hs = [];
                    hs.Add((RF(zone), tag));
                }
            }
            foreach (var j in members)
            {
                var l = runsOf[j];
                for (var q = 0; q + 1 < l.Count; q++)
                {
                    var (x, y) = (l[q], l[q + 1]);
                    if (!runs[x].Zone && runs[y].Zone) Attach(x, y); else if (runs[x].Zone && !runs[y].Zone) Attach(y, x);
                }
            }
            foreach (var (gap, zone) in extra) Attach(gap, zone);
            foreach (var (_, hs) in att) compNodes.Add((sIdx, hs));
        }
        // 4. Graph Konturen – Teilverbünde, Blätter abschneiden; übrig bleiben Kreise (und Wege zwischen ihnen)
        var edges = new List<(int Node, int Contour)>(); // Knoten der Teilverbünde: 0…n−1, Konturen über ihre Nummer
        for (var n = 0; n < compNodes.Count; n++) foreach (var (_, c) in compNodes[n].Att) edges.Add((n, c));
        if (edges.Count == 0) return [];
        var degN = new int[compNodes.Count]; var degC = new Dictionary<int, int>();
        foreach (var (n, c) in edges) { degN[n]++; degC[c] = degC.GetValueOrDefault(c) + 1; }
        var alive = Enumerable.Repeat(true, edges.Count).ToArray();
        var byNodeE = new List<int>[compNodes.Count]; for (var n = 0; n < byNodeE.Length; n++) byNodeE[n] = [];
        var byCE = new Dictionary<int, List<int>>();
        for (var e = 0; e < edges.Count; e++) { byNodeE[edges[e].Node].Add(e); if (!byCE.TryGetValue(edges[e].Contour, out var l)) byCE[edges[e].Contour] = l = []; l.Add(e); }
        var stack = new Stack<(bool IsC, int Id)>();
        for (var n = 0; n < degN.Length; n++) if (degN[n] == 1) stack.Push((false, n));
        foreach (var (c, d) in degC) if (d == 1) stack.Push((true, c));
        while (stack.Count > 0)
        {
            var (isC, id) = stack.Pop();
            var list = isC ? byCE[id] : byNodeE[id];
            foreach (var e in list)
            {
                if (!alive[e]) continue; alive[e] = false;
                var (n, c) = edges[e];
                if (--degN[n] == 1) stack.Push((false, n));
                degC[c]--; if (degC[c] == 1) stack.Push((true, c));
            }
        }
        // Gruppen der übrigen Kanten (Knoten: Teilverbünde 0…n−1, Konturen n + Nummer)
        var nN = compNodes.Count; var gp = new Dictionary<int, int>();
        int GF(int x) { if (!gp.TryGetValue(x, out var p)) { gp[x] = x; return x; } var r = x; while (gp[r] != r) r = gp[r]; while (gp[x] != r) { var q = gp[x]; gp[x] = r; x = q; } return r; }
        for (var e = 0; e < edges.Count; e++)
        {
            if (!alive[e]) continue;
            var a = GF(edges[e].Node); var b = GF(nN + edges[e].Contour); if (a != b) gp[a] = b;
        }
        var groups = new Dictionary<int, (SortedSet<int> C, SortedSet<int> S)>();
        for (var e = 0; e < edges.Count; e++)
        {
            if (!alive[e]) continue;
            var r = GF(nN + edges[e].Contour);
            if (!groups.TryGetValue(r, out var g)) groups[r] = g = ([], []);
            g.C.Add(edges[e].Contour); g.S.Add(compNodes[edges[e].Node].Struct);
        }
        return groups.OrderBy(kv => kv.Value.C.Min).Select(kv => (kv.Value.C.ToList(), kv.Value.S.ToList())).ToList();
    }

    /// <summary>Parameterintervall der Strecke a→b mit Abstand ≤ R zur Strecke c0→c1 (Kapsel: Rechteck + zwei Kreise), oder <c>null</c>.</summary>
    static (double Lo, double Hi)? CapsuleT((double X, double Y) a, (double X, double Y) b, (double X, double Y) c0, (double X, double Y) c1, double R)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y, len2 = dx * dx + dy * dy;
        if (len2 < 1e-24) return SegDist(a, c0, c1) <= R ? (0, 1) : null;
        double lo = double.MaxValue, hi = double.MinValue;
        void Circle((double X, double Y) c)
        {
            double fx = a.X - c.X, fy = a.Y - c.Y, B = 2 * (fx * dx + fy * dy), Cc = fx * fx + fy * fy - R * R, disc = B * B - 4 * len2 * Cc;
            if (disc < 0) return; var sq = Math.Sqrt(disc);
            lo = Math.Min(lo, (-B - sq) / (2 * len2)); hi = Math.Max(hi, (-B + sq) / (2 * len2));
        }
        Circle(c0); Circle(c1);
        double wx = c1.X - c0.X, wy = c1.Y - c0.Y, wl2 = wx * wx + wy * wy;
        if (wl2 > 1e-24)
        {
            var wl = Math.Sqrt(wl2);
            double u0 = ((a.X - c0.X) * wx + (a.Y - c0.Y) * wy) / wl2, u1 = (dx * wx + dy * wy) / wl2;
            double v0 = (wx * (a.Y - c0.Y) - wy * (a.X - c0.X)) / wl, v1 = (wx * dy - wy * dx) / wl;
            double tl = double.MinValue, th = double.MaxValue;
            void Slab(double s0, double s1, double min, double max)
            {
                if (Math.Abs(s1) < 1e-18) { if (s0 < min || s0 > max) { tl = 1; th = 0; } return; }
                var t1 = (min - s0) / s1; var t2 = (max - s0) / s1; if (t1 > t2) (t1, t2) = (t2, t1);
                tl = Math.Max(tl, t1); th = Math.Min(th, t2);
            }
            Slab(u0, u1, 0, 1); Slab(v0, v1, -R, R);
            if (tl <= th) { lo = Math.Min(lo, tl); hi = Math.Max(hi, th); }
        }
        lo = Math.Max(lo, 0); hi = Math.Min(hi, 1);
        return lo <= hi ? (lo, hi) : null;
    }
}
