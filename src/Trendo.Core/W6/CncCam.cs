using System.Globalization;
using System.Text;
using Trendo.Core.Cad;

namespace Trendo.Core;

/// <summary>T-0747: Einstellungen für Fräsbahnen aus einer Zeichnung. Maße in mm, Vorschübe in mm/min.</summary>
public sealed record CamSettings
{
    public double ToolD { get; init; } = 6;
    public double Depth { get; init; } = 3;
    public double StepDown { get; init; } = 1;
    public double Feed { get; init; } = 600;
    public double Plunge { get; init; } = 200;
    public double Safe { get; init; } = 5;
    /// <summary>„auto“ (äußerster Umriss außen, Löcher innen, offene Linien auf der Linie), „aussen“, „innen“ oder „auf“.</summary>
    public string Mode { get; init; } = "auto";
    /// <summary>Haltestege je Außenkontur (0 = keine), Breite (Material, das stehen bleibt) und Höhe über dem Boden.</summary>
    public int Tabs { get; init; }
    public double TabW { get; init; } = 6;
    public double TabH { get; init; } = 1;
    /// <summary>Spindeldrehzahl für M3 (0 = kein M3 im Programm).</summary>
    public double Rpm { get; init; }
    /// <summary>Teil so verschieben, dass seine linke untere Ecke auf dem Nullpunkt liegt.</summary>
    public bool ToOrigin { get; init; } = true;
    /// <summary>Höchste Abweichung der Bahn von Bögen (mm).</summary>
    public double Chord { get; init; } = 0.01;
    /// <summary>T-0747-fix5: Ausdehnung nicht gelesener Elemente (Zeichnungskoordinaten, wie die Konturen) – was darin liegt, wird ausgelassen und gemeldet.</summary>
    public IReadOnlyList<(string What, CadDxf.Box B)> Unread { get; init; } = [];
}

/// <summary>Eine Fräsbahn (Fräsermitte) mit Art und Herkunft.</summary>
public sealed record CamPath(string Kind, List<(double X, double Y)> Pts, bool Closed, int Source, string Layer)
{
    /// <summary>T-0747-fix: Haltestege nur auf dem äußeren Umriss einer Außenkontur – nicht in eingeschlossenen Buchten.</summary>
    public bool TabsAllowed { get; init; } = true;
}

/// <summary>Ergebnis: G-Code, Bahnen, Hinweise, Verschiebung und die (verschobenen) Konturen der Zeichnung.</summary>
public sealed record CamResult(string Gcode, List<CamPath> Paths, List<string> Warnings, double ShiftX, double ShiftY, List<CadContour> Contours);

/// <summary>T-0747 (SPEC-14): CAM aus Zeichnungen – Konturen aus DXF (<see cref="CadDxf"/>) oder SVG (<see cref="CadSvg"/>) werden zu Fräsbahnen.
/// Außenkontur: Fräsermitte um den Radius nach außen versetzt, Gleichlauf (im Uhrzeigersinn); Innenkontur: nach innen versetzt, gegen den Uhrzeigersinn;
/// offene Linien: auf der Linie (Gravur). Die Werkzeugradius-Korrektur rechnet Trendo selbst – im G-Code stehen nur G0/G1, kein G41/G42.
/// T-0747-fix (Gegenprüfung): Der Versatz wird nicht mehr durch „größere Schleife behalten“ bereinigt, sondern beschnitten: die rohe Versatzlinie wird an
/// allen Selbstschnitten geteilt, jedes Stück bleibt nur, wenn es genau im Radius-Abstand zur Kontur auf der richtigen Seite liegt; die Stücke werden wieder
/// zu Schleifen verbunden (eine Hantel ergibt zwei Kammern). Jede Bahn wird danach Strecke für Strecke gegen ALLE geschlossenen Konturen geprüft (Abstand
/// mindestens Radius − <see cref="Clearance"/>); Verstöße werden weggelassen und gemeldet. Selbst schneidende Konturen werden abgelehnt. Stellen, an die der
/// Fräser nicht herankommt (Engstellen), werden mit Lage gemeldet. Konturen werden vorher leicht geglättet (Douglas-Peucker, <see cref="SimplifyTol"/>; die
/// Abweichung wird dem Versatz zugeschlagen). Zustellung in Stufen bis zur Tiefe, Haltestege auf Außenkonturen, Löcher vor dem Umriss.</summary>
public static class CncCam
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    static string I(double v) => Math.Round(v, 4).ToString("0.####", Inv);
    static string F(double v, string fmt = "0.###") => v.ToString(fmt, De);

    /// <summary>T-0747-fix: so viel darf die Fräsermitte näher als der Radius an eine Kontur kommen (Rechenungenauigkeit), sonst wird die Bahn weggelassen.</summary>
    public const double Clearance = 0.001;
    /// <summary>T-0747-fix: höchstens so viele Elemente (ohne Texte) liest das Fräsen aus einer Zeichnung.</summary>
    public const int MaxEntities = 200_000; // T-0747-fix5: 50 000 Kreise sollen gehen (vorher 50 000)
    /// <summary>T-0747-fix: Glätten vor dem Versatz (Douglas-Peucker). Klein gewählt: das Beschneiden kommt auch mit verrauschten Konturen zurecht, das Glätten
    /// entfernt nur überflüssige Punkte; seine Abweichung wird dem Versatz zugeschlagen, das Teil wird also höchstens um diesen Betrag größer (Loch kleiner).</summary>
    public const double SimplifyTol = 0.001;

    /// <summary>T-0747-fix: Bögen und Kreise werden fürs Fräsen feiner zerlegt (höchstens 0,001 mm Abweichung vom echten Bogen) – die Sehnen liegen innerhalb
    /// des Bogens, der echte Bogen also bis zu dieser Abweichung näher an einer Außenbahn.</summary>
    public const double ContourChord = 0.001;

    /// <summary>T-0747-fix2: Enden, die näher als 0,1 mm beieinanderliegen, gelten beim Verketten als verbunden – kleine Spalten in Umrissen
    /// (z. B. vier LINE mit 0,05 mm Lücke) schließen sich so; bleibt eine Kontur offen, wird sie nur in der Fräsart „auf der Linie“ gefräst.</summary>
    public const double ChainTol = 0.1;

    /// <summary>Konturen einer Zeichnung (sichtbare Ebenen, Texte bleiben weg).</summary>
    public static List<CadContour> Contours(CadDrawing d, double chord = ContourChord, CancellationToken ct = default) => Contours(d, null, chord, ct);

    /// <summary>Wie oben; <paramref name="notes"/> bekommt Hinweise beim Verketten (T-0747-fix3: mehrdeutige Spalten).</summary>
    public static List<CadContour> Contours(CadDrawing d, List<string>? notes, double chord = ContourChord, CancellationToken ct = default)
    {
        var hidden = d.Layers.Where(l => !l.Visible).Select(l => l.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ents = d.Entities.Where(e => e.Kind != "text" && !hidden.Contains(e.Layer) && !e.Invisible).ToList(); // T-0747-fix11: unsichtbar (60 = 1) wie ausgeblendet
        if (ents.Count > MaxEntities) throw new CadError($"Die Zeichnung hat {ents.Count.ToString("N0", De)} Elemente – zu viele zum Fräsen (höchstens {MaxEntities.ToString("N0", De)}). Bitte nur die nötigen Teile oder Ebenen exportieren.");
        return CadContours.Build(ents, ChainTol, chord, notes, true, ct); // T-0747-fix13: Linien-Stöße nicht gierig verketten
    }

    public static CamResult Generate(IReadOnlyList<CadContour> source, CamSettings s, CancellationToken ct = default)
    {
        if (source.Count == 0) throw new InvalidOperationException("Die Zeichnung hat keine Konturen (Linien, Kreise, Bögen, Pfade).");
        if (s.ToolD <= 0 || s.Depth <= 0 || s.StepDown <= 0 || s.Feed <= 0 || s.Plunge <= 0) throw new ArgumentException("Fräser-Ø, Tiefe, Zustellung und Vorschübe müssen größer als 0 sein.");
        if (s.Tabs > 0 && (s.TabH <= 0 || s.TabH >= s.Depth)) throw new ArgumentException("Haltestege: Höhe muss größer als 0 und kleiner als die Tiefe sein.");
        var r = s.ToolD / 2; var warn = new List<string>();
        // verschieben: linke untere Ecke aufs Nullpunkt
        double sx = 0, sy = 0;
        if (s.ToOrigin) { sx = -source.Min(c => c.Pts.Min(p => p.X)); sy = -source.Min(c => c.Pts.Min(p => p.Y)); }
        var cs = source.Select(c => c with { Pts = c.Pts.Select(p => (p.X + sx, p.Y + sy)).ToList() }).ToList();
        string Name(int i) => $"Kontur {i + 1}" + (cs[i].Layer is { Length: > 0 } and not "0" ? $" (Ebene {cs[i].Layer})" : "");
        // T-0747-fix4: doppelt gezeichnete (deckungsgleiche) geschlossene Konturen – egal welche Richtung oder Startecke – nur einmal verwenden. Sonst zählt
        // ein Loch in einer doppelten Platte zwei Umrisse und wird als „außen“ gefräst (um den Fräser-Ø zu groß).
        var dup = new bool[cs.Count];
        // T-0747-fix5: Vorfilter – nur Paare mit fast gleichem Rahmen (Raster über die linke untere Ecke) und fast gleicher Fläche werden genau geprüft
        var boxes = cs.Select(c => c.Closed ? c.Box : (0.0, 0.0, -1.0, -1.0)).ToArray();
        var areas = cs.Select(c => c.Closed ? Math.Abs(c.SignedArea) : 0).ToArray();
        var perims = cs.Select(c => c.Closed ? Length(c.Pts, true) : 0).ToArray();
        const double dupCell = 0.01;
        var corner = new Dictionary<(long, long), List<int>>();
        for (var i = 0; i < cs.Count; i++)
            if (cs[i].Closed) { var key = ((long)Math.Floor(boxes[i].Item1 / dupCell), (long)Math.Floor(boxes[i].Item2 / dupCell)); if (!corner.TryGetValue(key, out var l)) corner[key] = l = []; l.Add(i); }
        for (var i = 0; i < cs.Count; i++)
        {
            if (!cs[i].Closed || dup[i]) continue;
            if ((i & 255) == 0) ct.ThrowIfCancellationRequested();
            var (kx, ky) = ((long)Math.Floor(boxes[i].Item1 / dupCell), (long)Math.Floor(boxes[i].Item2 / dupCell));
            var near = new List<int>();
            for (var dx = -1L; dx <= 1; dx++) for (var dy = -1L; dy <= 1; dy++) if (corner.TryGetValue((kx + dx, ky + dy), out var l)) near.AddRange(l.Where(j => j > i));
            near.Sort();
            foreach (var j in near)
            {
                if (dup[j]) continue;
                ct.ThrowIfCancellationRequested();
                var (a, b) = (boxes[i], boxes[j]);
                if (Math.Abs(a.Item1 - b.Item1) > DupTol || Math.Abs(a.Item2 - b.Item2) > DupTol || Math.Abs(a.Item3 - b.Item3) > DupTol || Math.Abs(a.Item4 - b.Item4) > DupTol) continue;
                if (Math.Abs(areas[i] - areas[j]) > DupTol * (perims[i] + perims[j])) continue;
                if (!Coincident(cs[i].Pts, cs[j].Pts, DupTol)) continue;
                dup[j] = true;
                warn.Add($"{Name(j)}: doppelt gezeichnet (deckungsgleich mit Kontur {i + 1}) – einmal verwendet.");
            }
        }
        // T-0747-fix: selbst schneidende Konturen (Acht) ablehnen – innen und außen sind dort nicht eindeutig
        var crossing = new bool[cs.Count];
        for (var i = 0; i < cs.Count; i++)
            if (cs[i].Closed && !dup[i] && s.Mode != "auf" && SelfIntersects(cs[i].Pts))
            {
                crossing[i] = true;
                warn.Add($"{Name(i)} ausgelassen: die Kontur kreuzt sich selbst (z. B. eine Acht) – dann ist nicht klar, was innen und außen ist. Bitte in der Zeichnung in einzelne geschlossene Umrisse auflösen.");
            }
        // T-0747-fix2: Konturen in oder an einer abgelehnten, sich selbst kreuzenden Kontur – innen/außen ist dort nicht sicher zu deuten (ein Loch würde
        // sonst als Umriss gefräst und größer). Auslassen und melden.
        var unsure = new bool[cs.Count];
        for (var j = 0; j < cs.Count; j++)
        {
            // T-0747-fix4: auch ein ausgelassener offener Umriss (über seine geschlossene Annäherung: Enden verbunden) macht die Deutung darin unsicher
            var openOutline = !cs[j].Closed && s.Mode != "auf" && cs[j].Pts.Count >= 3 && Math.Abs(CncCam.SignedArea(cs[j].Pts)) > 1e-6;
            if (!crossing[j] && !openOutline) continue;
            var bj = cs[j].Box;
            for (var i = 0; i < cs.Count; i++)
            {
                if (i == j || crossing[i] || unsure[i] || dup[i] || !cs[i].Closed && !openOutline) continue;
                ct.ThrowIfCancellationRequested();
                var bi = cs[i].Box; if (bi.MaxX < bj.MinX || bi.MinX > bj.MaxX || bi.MaxY < bj.MinY || bi.MinY > bj.MaxY) continue;
                var pr = cs[i].Pts[0]; // T-0747-fix3: auch doppelt umlaufene Bereiche (Windungszahl ≠ 0, z. B. Limaçon) zählen als „in der Kontur“
                if (!cs[j].Contains(pr.X, pr.Y) && Winding(cs[j].Pts, pr) == 0 && !Touches(cs[i].Pts, cs[i].Closed, cs[j].Pts)) continue;
                if (!cs[i].Closed) continue; // offene Konturen werden ohnehin nur als Gravur gefräst
                unsure[i] = true;
                warn.Add(crossing[j]
                    ? $"{Name(i)} ausgelassen: liegt in oder an Kontur {j + 1}, die sich selbst kreuzt – ob sie Loch oder Teil ist, lässt sich nicht sicher sagen. Bitte die Zeichnung bereinigen."
                    : $"{Name(i)} ausgelassen: liegt in oder an der offenen Kontur {j + 1} – ob sie Loch oder Teil ist, lässt sich nicht sicher sagen. Bitte den Umriss in der Zeichnung schließen.");
            }
        }
        // T-0747-fix5: Konturen in der Ausdehnung eines nicht gelesenen Elements (Blockverweis, SPLINE, schräge Normale) – ob Loch oder Teil, ist nicht sicher.
        // Liegt umgekehrt ein nicht gelesenes Element in einer Kontur, wird diese gefräst, aber gemeldet (dort fehlt etwas).
        foreach (var (what, ub) in s.Unread)
        {
            if (ub.IsEmpty) continue;
            var u = ub.ToSafe().Shift(sx, sy); // T-0747-fix8: NaN-Rahmen wie „überall“
            // T-0747-fix7: formgleiche Füllflächen-Schleifen entfernt schon der Leser (CadDxf.Parse), hier bleibt nur, was wirklich ungelesen ist
            for (var i = 0; i < cs.Count; i++)
            {
                if (!cs[i].Closed || dup[i] || crossing[i] || unsure[i]) continue;
                var bi = boxes[i]; const double e = 1e-6;
                if (bi.Item1 >= u.MinX - e && bi.Item2 >= u.MinY - e && bi.Item3 <= u.MaxX + e && bi.Item4 <= u.MaxY + e)
                {
                    unsure[i] = true;
                    warn.Add(u.IsAll
                        ? $"{Name(i)} ausgelassen: nicht gelesenes Element {what} hat keine bestimmbare Ausdehnung – es könnte diese Kontur umschließen, ob sie Loch oder Teil ist (die Seite), lässt sich nicht sicher sagen."
                        : $"{Name(i)} ausgelassen: liegt in nicht gelesenem Element {what} – ob sie Loch oder Teil ist (die Seite), lässt sich nicht sicher sagen.");
                }
                else if (!u.IsAll && u.MinX >= bi.Item1 - e && u.MinY >= bi.Item2 - e && u.MaxX <= bi.Item3 + e && u.MaxY <= bi.Item4 + e && cs[i].Contains((u.MinX + u.MaxX) / 2, (u.MinY + u.MaxY) / 2))
                    warn.Add($"{Name(i)}: enthält nicht gelesenes Element {what} (bei X {F((u.MinX + u.MaxX) / 2, "0.#")} Y {F((u.MinY + u.MaxY) / 2, "0.#")}) – dort wird nichts gefräst.");
            }
        }
        // T-0747-fix13…17: Schutzbereiche für unklare Verbünde. Ein Umriss aus Stücken eines Verbunds liegt immer in dessen konvexer Hülle; er kann nur
        // Konturen umschließen, die ganz in der Hülle liegen. Ausgelassen und gemeldet wird jede geschlossene Kontur,
        //  1. deren Rahmen im Verbundrahmen liegt,
        //  2. deren erster Punkt in der Hülle (± 1e-6) liegt – trifft jede Kontur, die ganz darin liegt, auch auf dem Rand,
        //  3. von deren Eckpunkten einer echt in der Hülle liegt (mehr als 1e-6 vom Rand) – Konturen, die teilweise darin liegen.
        // Eine Kontur, die die Hülle umschließt oder nur berührt bzw. ohne Eckpunkt darin kreuzt, behält ihre Tiefe und wird gefräst (kommt ihre
        // Fräsbahn den Linien zu nah, meldet das die Abstandsprüfung als „zu nah an Kontur …“ – das trifft dann auch zu).
        // T-0747-fix17: Gesucht wird über ein Raster aller Eckpunkte geschlossener Konturen (je Aufruf nur die Zellen des Verbundrahmens; Einträge
        // schon ausgelassener Konturen werden dabei entfernt). Budget je Verbund; ist es erschöpft, werden die Konturen, deren Rahmen den Verbundrahmen
        // wirklich überlappt, mit eigener Meldung („Prüfaufwand zu groß“) ausgelassen.
        bool Open(int i) => cs[i].Closed && !dup[i] && !crossing[i] && !unsure[i];
        var nOps = 0; void Tick() { if ((++nOps & 1023) == 0) ct.ThrowIfCancellationRequested(); }
        Dictionary<(long, long), List<(int Tag, int Idx)>>? vgrid = null; double vcell = 1;
        int[]? byMinX = null; double[]? minXs = null; int[]? jumpA = null; double maxW = 0;
        void EnsureIndex()
        {
            if (vgrid is not null) return;
            var closedIdx = Enumerable.Range(0, cs.Count).Where(i => cs[i].Closed && !dup[i]).ToArray();
            long nv = 0; double ax0 = double.MaxValue, ay0 = double.MaxValue, ax1 = double.MinValue, ay1 = double.MinValue;
            foreach (var i in closedIdx) { nv += cs[i].Pts.Count; ax0 = Math.Min(ax0, boxes[i].Item1); ay0 = Math.Min(ay0, boxes[i].Item2); ax1 = Math.Max(ax1, boxes[i].Item3); ay1 = Math.Max(ay1, boxes[i].Item4); }
            vcell = nv == 0 ? 1 : Math.Max(1e-3, 2 * Math.Sqrt(Math.Max(1e-9, (ax1 - ax0) * (ay1 - ay0)) / nv));
            vgrid = [];
            foreach (var i in closedIdx)
                for (var k = 0; k < cs[i].Pts.Count; k++)
                {
                    Tick(); var p = cs[i].Pts[k]; var key = ((long)Math.Floor(p.X / vcell), (long)Math.Floor(p.Y / vcell));
                    if (!vgrid.TryGetValue(key, out var l)) vgrid[key] = l = []; l.Add((i, k));
                }
            byMinX = closedIdx.OrderBy(i => boxes[i].Item1).ToArray(); minXs = byMinX.Select(i => boxes[i].Item1).ToArray();
            jumpA = new int[byMinX.Length + 1]; for (var i = 0; i < jumpA.Length; i++) jumpA[i] = i;
            maxW = closedIdx.Length == 0 ? 0 : closedIdx.Max(i => boxes[i].Item3 - boxes[i].Item1);
        }
        int NextA(int q)
        {
            var r0 = q;
            while (r0 < byMinX!.Length && (jumpA![r0] != r0 || !Open(byMinX[r0]))) { if (jumpA![r0] == r0) jumpA[r0] = r0 + 1; r0 = jumpA[r0]; }
            while (q < r0) { var t = jumpA![q]; jumpA[q] = r0; q = t; }
            return r0;
        }
        static int Lower(double[] a, double v) { var lo = Array.BinarySearch(a, v); if (lo < 0) lo = ~lo; while (lo > 0 && a[lo - 1] >= v) lo--; return lo; }
        // Schutzbereich eines Verbunds: skip(i) lässt Kontur i aus (eigene Meldung je Grund)
        void Protect(List<(double X, double Y)> pts, Action<int> skip, Action<int> skipBudget, Func<int, bool>? spare = null)
        {
            EnsureIndex();
            double gx0 = pts.Min(p => p.X), gy0 = pts.Min(p => p.Y), gx1 = pts.Max(p => p.X), gy1 = pts.Max(p => p.Y);
            const double e = 1e-6; var hull = Hull(pts); var lg = 1 + (long)Math.Log2(Math.Max(2, hull.Count));
            long w = 0; const long MaxW = 20_000_000; var exhausted = false;
            void Check(int tag, int idx)
            {
                if (!Open(tag)) return;
                var v = cs[tag].Pts[idx];
                if (v.X < gx0 - e || v.X > gx1 + e || v.Y < gy0 - e || v.Y > gy1 + e) return;
                if (idx == 0)
                {
                    var bi = boxes[tag];
                    if (bi.Item1 >= gx0 - e && bi.Item2 >= gy0 - e && bi.Item3 <= gx1 + e && bi.Item4 <= gy1 + e) { if (spare?.Invoke(tag) != true) skip(tag); return; } // 1.
                    w += lg; if (hull.Count >= 3 && InConvexLog(hull, v, -e)) { if (spare?.Invoke(tag) != true) skip(tag); return; } // 2.
                }
                else if (hull.Count >= 3 && v.X > gx0 + e && v.X < gx1 - e && v.Y > gy0 + e && v.Y < gy1 - e) { w += lg; if (InConvexLog(hull, v, e) && spare?.Invoke(tag) != true) skip(tag); } // 3.
            }
            long cx0 = (long)Math.Floor((gx0 - e) / vcell), cx1 = (long)Math.Floor((gx1 + e) / vcell), cy0 = (long)Math.Floor((gy0 - e) / vcell), cy1 = (long)Math.Floor((gy1 + e) / vcell);
            var cells = (double)(cx1 - cx0 + 1) * (cy1 - cy0 + 1);
            void Cell(List<(int Tag, int Idx)> l)
            {
                var keepN = 0;
                for (var q = 0; q < l.Count; q++)
                {
                    Tick(); var (tag, idx) = l[q];
                    if (!Open(tag)) continue; // Eintrag einer schon ausgelassenen Kontur: entfernen
                    l[keepN++] = l[q];
                    if (!exhausted) { Check(tag, idx); if (w > MaxW) exhausted = true; }
                }
                l.RemoveRange(keepN, l.Count - keepN);
            }
            if (cells <= 4.0 * vgrid!.Count + 64)
            {
                for (var cx = cx0; cx <= cx1 && !exhausted; cx++)
                    for (var cy = cy0; cy <= cy1 && !exhausted; cy++)
                        if (vgrid.TryGetValue((cx, cy), out var l)) Cell(l);
            }
            else
                foreach (var (key, l) in vgrid) { if (exhausted) break; if (key.Item1 >= cx0 && key.Item1 <= cx1 && key.Item2 >= cy0 && key.Item2 <= cy1) Cell(l); }
            if (!exhausted) return;
            // sichere Folge: alles, dessen Rahmen den Verbundrahmen wirklich überlappt, auslassen
            for (var q = NextA(Lower(minXs!, gx0 - maxW - e)); q < byMinX!.Length && minXs![q] <= gx1 + e; q = NextA(q + 1))
            {
                Tick(); var i = byMinX[q]; var bi = boxes[i];
                if (bi.Item3 >= gx0 - e && bi.Item2 <= gy1 + e && bi.Item4 >= gy0 - e) skipBudget(i);
            }
        }
        void SkipAs(int i, string why) { if (!Open(i)) return; unsure[i] = true; warn.Add($"{Name(i)} ausgelassen: {why}"); }
        const string Budget = "Prüfaufwand zu groß – die Kontur liegt am oder im Rahmen eines unklaren Linienverbunds und wird vorsichtshalber ausgelassen.";

        // a) Linienverbund mit Stoß (Gruppe samt über Spalte angeschlossener Ketten)
        var jgroups = cs.Select((c, j) => (c, j)).Where(t => t.c.JunctionGroup >= 0).GroupBy(t => t.c.JunctionGroup).ToList();
        if (jgroups.Count > 0)
        {
            warn.Add($"{jgroups.Count} Linienverbund/-verbünde (zusammen {jgroups.Sum(g => g.Count())} Linien, z. B. ab Kontur {jgroups[0].First().j + 1}) nicht zu Umrissen verkettet: an einem Punkt stoßen mehr als zwei Linien zusammen oder die Enden liegen nicht eindeutig. Diese Linien werden nicht gefräst (außer als Gravur), Konturen darin werden ausgelassen.");
            foreach (var grp in jgroups)
            {
                ct.ThrowIfCancellationRequested();
                var pts = new List<(double X, double Y)>(); foreach (var (c, _) in grp) pts.AddRange(c.Pts);
                var first = grp.First().j;
                Protect(pts, i => SkipAs(i, $"liegt im Linienverbund ab Kontur {first + 1} (oder teilweise darin), an dem mehr als zwei Linien zusammenstoßen – welcher Umriss gemeint ist und ob sie Loch oder Teil ist, lässt sich nicht sicher sagen. Bitte die Umrisse in der Zeichnung trennen oder als Polylinie zeichnen."), i => SkipAs(i, Budget));
            }
        }

        // b) T-0747-fix17: offener Linienzug (oder Stoß-Verbund), der mit mindestens zwei Enden (≤ 0,1 mm, gleiche Ebene) an dieselbe geschlossene Kontur C
        // stößt und teilweise außerhalb von C liegt, schließt zusammen mit dem Rand von C einen Bereich ein (z. B. ein U aus Linien vor einer Kerbe).
        // Ob dort Loch oder Teil gemeint ist und ob C so gemeint ist, lässt sich nicht sicher sagen: C und alles in der Hülle aus Linienzug ∪ C auslassen.
        // Liegt der Linienzug ganz in C (Gravur von Rand zu Rand), entsteht kein neuer Bereich außerhalb von C – dann bleibt alles wie es ist.
        if (s.Mode != "auf")
        {
            var comps = cs.Select((c, j) => (c, j)).Where(t => !t.c.Closed && t.c.Pts.Count >= 2).GroupBy(t => t.c.JunctionGroup >= 0 ? -1 - t.c.JunctionGroup : t.j).ToList();
            if (comps.Count > 0)
            {
                var closedOk = Enumerable.Range(0, cs.Count).Where(i => cs[i].Closed && !dup[i] && !crossing[i]).ToArray();
                var tolT = ChainTol + 1e-9;
                var cg = BuildGrid(closedOk.Select(i => ((IReadOnlyList<(double X, double Y)>)cs[i].Pts, true, i)), Math.Max(r, tolT), ct);
                var rowIdx = new Dictionary<int, RowIndex>(); var hullOf = new Dictionary<int, List<(double X, double Y)>>(); var spareOf = new Dictionary<(int, int), bool>();
                foreach (var comp in comps)
                {
                    ct.ThrowIfCancellationRequested();
                    var layer = comp.First().c.Layer; var touches = new Dictionary<int, int>();
                    foreach (var (c, _) in comp)
                        foreach (var end in new[] { c.Pts[0], c.Pts[^1] })
                        {
                            var hit = new HashSet<int>();
                            cg.Any(end.X - tolT, end.Y - tolT, end.X + tolT, end.Y + tolT, t =>
                            {
                                Tick(); var sg = cg.Segs[t];
                                if (cs[sg.Tag].Layer == layer && !hit.Contains(sg.Tag) && SegDist(end, sg.A, sg.B) <= tolT) hit.Add(sg.Tag);
                                return false;
                            });
                            foreach (var h in hit) touches[h] = touches.GetValueOrDefault(h) + 1;
                        }
                    foreach (var (cTag, nTouch) in touches)
                    {
                        if (nTouch < 2) continue;
                        // teilweise außerhalb von C? (ein Punkt nicht in C und weiter als tol von C) – Punkt-in-Zug über Zeilen-Index (T-0747-fix17)
                        var outside = false; var bC = boxes[cTag];
                        if (!rowIdx.TryGetValue(cTag, out var ri)) rowIdx[cTag] = ri = new RowIndex(cs[cTag].Pts);
                        foreach (var (c, _) in comp)
                        {
                            foreach (var v in c.Pts)
                            {
                                Tick();
                                if (v.X >= bC.Item1 && v.X <= bC.Item3 && v.Y >= bC.Item2 && v.Y <= bC.Item4 && ri.Contains(v.X, v.Y)) continue;
                                if (!cg.Any(v.X - tolT, v.Y - tolT, v.X + tolT, v.Y + tolT, t => cg.Segs[t].Tag == cTag && SegDist(v, cg.Segs[t].A, cg.Segs[t].B) <= tolT)) { outside = true; break; }
                            }
                            if (outside) break;
                        }
                        if (!outside) continue;
                        var first = comp.First().j;
                        var why = $"Linienzug ab Kontur {first + 1} stößt mit {nTouch} Enden an Kontur {cTag + 1} und schließt mit deren Rand einen Bereich ein – welcher Umriss gemeint ist und ob die Kontur Loch oder Teil ist, lässt sich nicht sicher sagen. Bitte den Umriss in der Zeichnung schließen oder die Linien auf eine eigene Ebene legen.";
                        if (!hullOf.TryGetValue(cTag, out var hc)) hullOf[cTag] = hc = Hull(cs[cTag].Pts); // Hülle von C einmal (Hülle der Vereinigung = Hülle aus Hülle(C) ∪ Linienzug)
                        var pts = new List<(double X, double Y)>(hc); foreach (var (c, _) in comp) pts.AddRange(c.Pts);
                        // Konturen ganz in C behalten ihre Tiefe in beiden Lesarten (C allein oder C samt eingeschlossenem Bereich) – sie werden weiter gefräst.
                        // Unklar ist nur, was außerhalb von C im eingeschlossenen Bereich liegt, und C selbst (sein Rand dort wäre dann kein Außenrand).
                        var cc = cs[cTag]; long inWork = 0; SegGrid? ccGrid = null;
                        bool InsideC(int i)
                        {
                            if (spareOf.TryGetValue((cTag, i), out var known)) return known;
                            return spareOf[(cTag, i)] = InsideC0(i);
                        }
                        bool InsideC0(int i)
                        {
                            var pp = cs[i].Pts; var step = Math.Max(1, pp.Count / 64);
                            for (var k = 0; k < pp.Count; k += step)
                            {
                                inWork++; if (inWork > 50_000_000) return false; // zu teuer: vorsichtshalber „nicht drin“
                                if (!ri.Contains(pp[k].X, pp[k].Y)) return false;
                            }
                            ccGrid ??= BuildGrid([((IReadOnlyList<(double X, double Y)>)cc.Pts, true, 0)], AvgLen(cc.Pts, true) * 2, ct);
                            for (var k = 0; k < pp.Count; k++) // berührt oder kreuzt i den Rand von C? dann nicht „ganz drin“
                            {
                                var a0 = pp[k]; var b0 = pp[(k + 1) % pp.Count]; inWork++;
                                if (ccGrid.Any(Math.Min(a0.X, b0.X) - 1e-6, Math.Min(a0.Y, b0.Y) - 1e-6, Math.Max(a0.X, b0.X) + 1e-6, Math.Max(a0.Y, b0.Y) + 1e-6, t => SegSeg(a0, b0, ccGrid.Segs[t].A, ccGrid.Segs[t].B) <= 1e-6)) return false;
                            }
                            return true;
                        }
                        SkipAs(cTag, why);
                        Protect(pts, i => SkipAs(i, why), i => SkipAs(i, Budget), InsideC);
                    }
                }
            }
        }
        // Art je Kontur (T-0747-fix5: Kandidaten für „liegt in“ aus einem Raster der Rahmen statt aus allen Konturen)
        var closedBoxes = boxes.Where((_, j) => cs[j].Closed).ToList();
        var spanAll = closedBoxes.Count == 0 ? 1 : Math.Max(closedBoxes.Max(b => b.Item3) - closedBoxes.Min(b => b.Item1), closedBoxes.Max(b => b.Item4) - closedBoxes.Min(b => b.Item2));
        var frames = new SegGrid(Math.Max(Math.Max(1e-3, spanAll / 1024), closedBoxes.Count == 0 ? 1 : closedBoxes.Average(b => Math.Max(b.Item3 - b.Item1, b.Item4 - b.Item2))));
        for (var j = 0; j < cs.Count; j++) if (cs[j].Closed && !crossing[j] && !dup[j]) frames.Add((boxes[j].Item1, boxes[j].Item2), (boxes[j].Item3, boxes[j].Item4), j);
        var kinds = new string[cs.Count]; var depth = new int[cs.Count];
        for (var i = 0; i < cs.Count; i++)
        {
            if (!cs[i].Closed) { kinds[i] = "auf"; continue; }
            var probe = cs[i].Pts[0];
            if ((i & 1023) == 0) ct.ThrowIfCancellationRequested();
            depth[i] = frames.Near(probe.X, probe.Y, probe.X, probe.Y).Select(e => frames.Segs[e].Tag).Distinct()
                .Count(j => j != i && boxes[j].Item1 <= probe.X && probe.X <= boxes[j].Item3 && boxes[j].Item2 <= probe.Y && probe.Y <= boxes[j].Item4 && areas[j] > areas[i] && cs[j].Contains(probe.X, probe.Y));
            kinds[i] = s.Mode switch { "aussen" => "aussen", "innen" => "innen", "auf" => "auf", _ => depth[i] % 2 == 0 ? "aussen" : "innen" };
        }
        // Bahnen je Kontur (noch ungeprüft)
        var cand = new List<CamPath>?[cs.Count];
        var cw = new List<string>?[cs.Count]; // T-0747-fix5: je Kontur parallel gerechnet, Meldungen danach in Konturreihenfolge
        Par(cs.Count, ct, i =>
        {
            var w = cw[i] = [];
            if (crossing[i] || unsure[i] || dup[i]) return;
            var c = cs[i];
            if (!c.Closed && s.Mode != "auf") // T-0747-fix2: offene Kontur nur ausdrücklich als Gravur
            {
                if (c.JunctionGroup >= 0) return; // T-0747-fix14: steht in der Sammelmeldung zum Linienverbund
                w.Add($"{Name(i)} ausgelassen: offene Kontur (Enden {F(Math.Sqrt(D2(c.Pts[0], c.Pts[^1])), "0.###")} mm auseinander) – auf der Linie gefräst würde ein Teil um den Fräserradius kleiner. Nur in der Fräsart „Auf der Linie (Gravur)“ wird sie gefräst; sonst die Zeichnung schließen.");
                return;
            }
            if (kinds[i] == "auf") { cand[i] = [new CamPath("auf", [.. c.Pts], c.Closed, i, c.Layer)]; return; }
            var outside = kinds[i] == "aussen";
            var (simp, dev) = Simplify(c.Pts, Math.Min(SimplifyTol, r / 20));
            var loops = OffsetLoops(simp, (outside ? 1 : -1) * (r + dev), s.Chord, ct);
            List<CamPath> list = [];
            if (outside)
            {
                var main = loops.Where(l => SignedArea(l) > 1e-9).MaxBy(SignedArea);
                if (main is null) { w.Add($"{Name(i)} ausgelassen: der Versatz nach außen ergab keine Bahn – die Kontur hat keine Fläche oder ist entartet."); return; }
                loops = [main, .. loops.Where(l => l != main && SignedArea(l) < 0)]; // eingeschlossene Buchten (im Uhrzeigersinn) bleiben
                foreach (var l in loops) { var p = new List<(double X, double Y)>(l); p.Reverse(); list.Add(new CamPath("aussen", p, true, i, c.Layer) { TabsAllowed = l == main }); } // im Uhrzeigersinn = Gleichlauf außen
            }
            else
            {
                loops = loops.Where(l => SignedArea(l) > 1e-9).ToList();
                if (loops.Count == 0) { w.Add($"{Name(i)}: Innenkontur ist zu klein für den Fräser Ø {F(s.ToolD)} mm – ausgelassen, dort bleibt Material stehen."); return; }
                foreach (var l in loops) list.Add(new CamPath("innen", l, true, i, c.Layer));
            }
            // T-0747-fix: Engstellen – Stellen der Kontur, an die der Fräser nicht herankommt
            var spots = Unreached(c.Pts, loops, r, ct);
            if (spots.Count > 0)
                w.Add($"{Name(i)}: Bereich enger als der Fräser Ø {F(s.ToolD)} mm – dort bleibt Material stehen ({string.Join("; ", spots.Take(4).Select(p => $"bei X {F(p.X, "0.#")} Y {F(p.Y, "0.#")}"))}{(spots.Count > 4 ? $" und {spots.Count - 4} weitere Stellen" : "")}). Für diese Stellen einen kleineren Fräser wählen.");
            cand[i] = list;
        });
        foreach (var w in cw) if (w is not null) warn.AddRange(w);
        // T-0747-fix: jede Bahn gegen ALLE geschlossenen Konturen prüfen (Strecke zu Strecke)
        // T-0747-fix3: ALLE Konturen sind Hindernis – auch offene und ausgelassene (ein offener Umriss ist trotzdem ein Teil)
        var obstacles = BuildGrid(cs.Select((c, i) => ((IReadOnlyList<(double X, double Y)>)c.Pts, c.Closed, i)).Where(t => !dup[t.i]), r, ct); // Doppel nur einmal
        var paths = new List<CamPath>();
        using var curs = new ThreadLocal<SegGrid.Cursor>(() => new SegGrid.Cursor()); // ein Merker je Thread (nicht je Kontur – er ist so groß wie das Raster)
        var hits = new (int Tag, double D)[cs.Count]; // T-0747-fix5: parallel prüfen (Raster nur lesen, Merker je Thread), danach in Reihenfolge übernehmen
        Par(cs.Count, ct, i =>
        {
            if (cand[i] is not { } list) return;
            var cur = curs.Value!;
            hits[i] = list.Select(p => Nearest(p.Pts, p.Closed, obstacles, r - Clearance, p.Kind == "auf" ? i : -1, ct, cur)).Where(h => h.Tag >= 0)
                .OrderBy(h => h.Tag == i ? 1 : 0).ThenBy(h => h.D).FirstOrDefault((Tag: -1, D: 0.0));
        });
        for (var i = 0; i < cs.Count; i++)
        {
            if (cand[i] is not { } list) continue;
            var hit = hits[i];
            if (hit.Tag < 0) { paths.AddRange(list); continue; }
            warn.Add(hit.Tag == i
                ? $"{Name(i)} ausgelassen: die Fräsbahn käme der eigenen Kontur auf {F(hit.D)} mm nahe (Fräserradius {F(r)} mm). Kleineren Fräser wählen."
                : $"{Name(i)} ausgelassen: zu nah an Kontur {hit.Tag + 1} – die Fräsbahn käme ihr auf {F(hit.D)} mm nahe, der Fräser braucht {F(r)} mm (Radius). Teile weiter auseinanderlegen oder kleineren Fräser wählen.");
        }
        // Reihenfolge: Löcher (tiefste zuerst), Gravuren, dann Umrisse – jeweils der nächste zuerst
        var order = new List<CamPath>(); (double X, double Y) at = (0, 0);
        foreach (var group in new[] { paths.Where(p => p.Kind == "innen").GroupBy(p => -depth[p.Source]).OrderBy(g => g.Key).SelectMany(g => g).ToList(), paths.Where(p => p.Kind == "auf").ToList(), paths.Where(p => p.Kind == "aussen").ToList() })
        {
            var left = group.ToList();
            if (left.Count > 200) { order.AddRange(OrderMany(left, ref at, ct)); continue; } // T-0747-fix5: viele Bahnen – Nachbarsuche im Raster
            while (left.Count > 0)
            {
                var next = left.OrderBy(p => p.Closed ? p.Pts.Min(q => D2(q, at)) : Math.Min(D2(p.Pts[0], at), D2(p.Pts[^1], at))).First(); left.Remove(next);
                var pts = next.Pts;
                if (next.Closed) { var k = Enumerable.Range(0, pts.Count).MinBy(j => D2(pts[j], at)); pts = [.. pts.Skip(k), .. pts.Take(k)]; }
                else if (D2(pts[^1], at) < D2(pts[0], at)) pts = Enumerable.Reverse(pts).ToList();
                order.Add(next with { Pts = pts }); at = pts[0];
            }
        }
        // G-Code
        var g = new StringBuilder();
        g.Append("(Konturen aus Zeichnung – Trendo OS)\n");
        g.Append($"(Fräser Ø {I(s.ToolD)} mm, Tiefe {I(s.Depth)} mm, Zustellung {I(s.StepDown)} mm – Radiuskorrektur ist eingerechnet, Bahn = Fräsermitte)\n");
        g.Append("G21 G90 G17\n").Append($"G0 Z{I(s.Safe)}\n");
        if (s.Rpm > 0) g.Append($"M3 S{I(Math.Round(s.Rpm))}\n");
        var passes = (int)Math.Ceiling(s.Depth / s.StepDown - 1e-9);
        var zTab = -s.Depth + s.TabH; var n = 0;
        foreach (var p in order)
        {
            n++;
            var label = p.Kind switch { "aussen" => "außen", "innen" => "innen", _ => "auf der Linie" };
            g.Append($"(Kontur {p.Source + 1}: {label}{(p.Layer is { Length: > 0 } and not "0" ? ", Ebene " + p.Layer : "")})\n");
            g.Append($"G0 X{I(p.Pts[0].X)} Y{I(p.Pts[0].Y)}\n");
            var tabs = p.Kind == "aussen" && p.TabsAllowed && s.Tabs > 0 ? TabRanges(p.Pts, s, name: $"Kontur {p.Source + 1}", warn) : [];
            var forward = true;
            for (var k = 1; k <= passes; k++)
            {
                var z = -Math.Min(s.Depth, k * s.StepDown);
                var pts = p.Closed || forward ? p.Pts : Enumerable.Reverse(p.Pts).ToList();
                g.Append($"G1 Z{I(z)} F{I(s.Plunge)}\n");
                var useTabs = tabs.Count > 0 && z < zTab - 1e-9;
                var lifts = Loop(g, pts, p.Closed, z, useTabs ? tabs : [], zTab, s);
                if (useTabs && lifts != tabs.Count) throw new InvalidOperationException($"Haltestege an Kontur {p.Source + 1} ließen sich nicht sicher setzen ({lifts} statt {tabs.Count} Anhebungen) – bitte Anzahl oder Breite der Stege ändern."); // T-0747-fix3
                if (!p.Closed) forward = !forward;
            }
            g.Append($"G0 Z{I(s.Safe)}\n");
        }
        g.Append("M5\nM30\n");
        if (n == 0) throw new InvalidOperationException("Es blieb keine fräsbare Kontur übrig: " + string.Join(" ", warn));
        return new CamResult(g.ToString(), order, warn, sx, sy, cs);
    }

    /// <summary>T-0747-fix5: Reihenfolge vieler Bahnen – jeweils die nächste (über ihren ersten Punkt, Raster), dann am nächstgelegenen Punkt beginnen.</summary>
    static List<CamPath> OrderMany(List<CamPath> paths, ref (double X, double Y) at, CancellationToken ct)
    {
        var res = new List<CamPath>(paths.Count); var used = new bool[paths.Count];
        double minX = paths.Min(p => p.Pts[0].X), minY = paths.Min(p => p.Pts[0].Y), maxX = paths.Max(p => p.Pts[0].X), maxY = paths.Max(p => p.Pts[0].Y);
        var cell = Math.Max(1e-3, Math.Sqrt((maxX - minX + 1) * (maxY - minY + 1) / paths.Count));
        var grid = new Dictionary<(long, long), List<int>>();
        (long, long) C((double X, double Y) q) => ((long)Math.Floor(q.X / cell), (long)Math.Floor(q.Y / cell));
        for (var i = 0; i < paths.Count; i++) { var k = C(paths[i].Pts[0]); if (!grid.TryGetValue(k, out var l)) grid[k] = l = []; l.Add(i); }
        for (var n = 0; n < paths.Count; n++)
        {
            if ((n & 1023) == 0) ct.ThrowIfCancellationRequested();
            var here0 = at; var (cx, cy) = C(here0); var best = -1; var bd = double.MaxValue;
            void Cell(long gx, long gy) { if (grid.TryGetValue((gx, gy), out var l)) foreach (var i in l) { if (used[i]) continue; var d = D2(paths[i].Pts[0], here0); if (d < bd) { bd = d; best = i; } } }
            for (var ring = 0L; ring <= 64; ring++)
            {
                if (best >= 0 && (ring - 1) * cell > Math.Sqrt(bd)) break; // weiter außen kann nichts Näheres liegen
                if (ring == 0) { Cell(cx, cy); continue; }
                for (var t = -ring; t <= ring; t++) { Cell(cx + t, cy - ring); Cell(cx + t, cy + ring); }
                for (var t = -ring + 1; t <= ring - 1; t++) { Cell(cx - ring, cy + t); Cell(cx + ring, cy + t); }
            }
            if (best < 0) for (var i = 0; i < paths.Count; i++) { if (used[i]) continue; var d = D2(paths[i].Pts[0], at); if (d < bd) { bd = d; best = i; } } // weit weg: alle durchsehen
            if (best < 0) best = Array.FindIndex(used, u => !u);
            used[best] = true; var p = paths[best]; var pts = p.Pts;
            if (p.Closed) { var here = at; var k = Enumerable.Range(0, pts.Count).MinBy(j => D2(pts[j], here)); pts = [.. pts.Skip(k), .. pts.Take(k)]; }
            else if (D2(pts[^1], at) < D2(pts[0], at)) pts = Enumerable.Reverse(pts).ToList();
            res.Add(p with { Pts = pts }); at = pts[0];
        }
        return res;
    }

    /// <summary>Eine Runde fahren; in den Stegbereichen auf Steghöhe anheben. Rückgabe: Anzahl der Anhebungen.
    /// T-0747-fix3: Steggrenzen genau auf einem Bahnpunkt werden erkannt (jede Strecke deckt [Anfang, Ende) ab), vorher fielen sie still weg.</summary>
    static int Loop(StringBuilder g, List<(double X, double Y)> pts, bool closed, double z, List<(double S, double E)> tabs, double zTab, CamSettings s)
    {
        const double e = 1e-7;
        var seq = closed ? [.. pts, pts[0]] : pts;
        var pos = 0.0; var up = false; var first = true; var lifts = 0;
        string F() { if (!first) return ""; first = false; return $" F{I(s.Feed)}"; }
        for (var i = 1; i < seq.Count; i++)
        {
            var a = seq[i - 1]; var b = seq[i]; var len = Math.Sqrt(D2(a, b)); if (len < 1e-9) continue;
            // Grenzen der Stege auf diesem Stück (einschließlich einer Grenze genau am Anfangspunkt)
            var cuts = tabs.SelectMany(t => new[] { t.S, t.E }).Where(t => t >= pos - e && t < pos + len - e).OrderBy(t => t).ToList();
            foreach (var c in cuts)
            {
                var f = Math.Clamp((c - pos) / len, 0, 1);
                if (f * len > 1e-9) { var (x, y) = (a.X + (b.X - a.X) * f, a.Y + (b.Y - a.Y) * f); g.Append($"G1 X{I(x)} Y{I(y)}{F()}\n"); }
                var inTab = tabs.Any(t => c + e >= t.S && c + e < t.E);
                if (inTab && !up) { g.Append($"G1 Z{I(zTab)}\n"); up = true; lifts++; }
                else if (!inTab && up) { g.Append($"G1 Z{I(z)} F{I(s.Plunge)}\n"); up = false; first = true; }
            }
            g.Append($"G1 X{I(b.X)} Y{I(b.Y)}{F()}\n");
            pos += len;
        }
        if (up) g.Append($"G1 Z{I(z)} F{I(s.Plunge)}\n");
        return lifts;
    }

    /// <summary>T-0747-fix3: Windungszahl des geschlossenen Zugs um den Punkt (0 = außerhalb, auch bei Selbstüberschneidung aussagekräftig).</summary>
    static int Winding(List<(double X, double Y)> poly, (double X, double Y) q)
    {
        var w = 0;
        for (var i = 0; i < poly.Count; i++)
        {
            var a = poly[i]; var b = poly[(i + 1) % poly.Count];
            if (a.Y <= q.Y) { if (b.Y > q.Y && Orient(a, b, q) > 0) w++; }
            else if (b.Y <= q.Y && Orient(a, b, q) < 0) w--;
        }
        return w;
    }

    /// <summary>Stegbereiche entlang der Bahn (Bogenlänge): Stegbreite plus Fräser-Ø, gleichmäßig verteilt, nicht am Startpunkt.</summary>
    static List<(double S, double E)> TabRanges(List<(double X, double Y)> pts, CamSettings s, string name, List<string> warn)
    {
        var total = 0.0; for (var i = 0; i < pts.Count; i++) total += Math.Sqrt(D2(pts[i], pts[(i + 1) % pts.Count]));
        var half = (s.TabW + s.ToolD) / 2;
        if (2 * half * s.Tabs > total * 0.6) { warn.Add($"{name}: zu kurz für {s.Tabs} Haltestege – ohne Stege."); return []; }
        return Enumerable.Range(0, s.Tabs).Select(k => { var c = total * (k + 0.5) / s.Tabs; return (c - half, c + half); }).ToList();
    }

    // ---------------------------------------------------------------- Versatz

    /// <summary>Versatz eines geschlossenen Zugs um <paramref name="dist"/> (positiv = nach außen): die größte Schleife gegen den Uhrzeigersinn, sonst leer.
    /// Alle Teilschleifen liefert <see cref="OffsetLoops"/>.</summary>
    public static List<(double X, double Y)> Offset(IReadOnlyList<(double X, double Y)> poly, double dist, double chord = 0.01) =>
        OffsetLoops(poly, dist, chord).Where(l => SignedArea(l) > 1e-9).MaxBy(SignedArea) ?? [];

    /// <summary>T-0747-fix: Versatz mit Beschneiden. Rohe Versatzlinie: jede Strecke um <paramref name="dist"/> verschoben, an Außenecken ein Bogen (Sehnen
    /// berühren den Kreis von außen, die Fräsermitte kommt der Ecke nie näher als der Abstand), an Innenecken ein Verbinder über den Eckpunkt. Diese Linie wird an allen
    /// Selbstschnitten geteilt; ein Stück bleibt nur, wenn es im Abstand |dist| zu seiner eigenen Strecke bzw. Ecke liegt, keiner Stelle der Kontur näher
    /// kommt und auf der richtigen Seite liegt. Die Stücke werden zu Schleifen verbunden. Ergebnis: Schleifen gegen den Uhrzeigersinn (Fläche, die der Fräser
    /// erreicht) und – nur nach außen – Schleifen im Uhrzeigersinn um eingeschlossene Buchten.</summary>
    public static List<List<(double X, double Y)>> OffsetLoops(IReadOnlyList<(double X, double Y)> poly, double dist, double chord = 0.01, CancellationToken ct = default)
    {
        var pts = Clean(poly);
        if (pts.Count < 3 || Math.Abs(dist) < 1e-9) return [];
        if (SignedArea(pts) < 0) pts.Reverse();
        var (raw, gen, arcs) = RawOffset(pts, dist, chord);
        if (raw.Count < 3) return [];
        var ad = Math.Abs(dist); var np = pts.Count;
        var polyGrid = BuildGrid([((IReadOnlyList<(double X, double Y)>)pts, true, 0)], ad);
        // Abstand so gemessen, wie die rohe Linie gebaut ist: Strecken nur senkrecht (Fußpunkt auf der Strecke), Ecken mit Bogen als Sehnenpolygon
        // (die Sehnen liegen außerhalb des Kreises), übrige Ecken als Kreis. So passt jeder Schnittpunkt genau zu einem gültigen Stück hinein und einem hinaus.
        double EdgeD(int e, (double X, double Y) q)
        {
            var pa = pts[e]; var pb = pts[(e + 1) % np]; double dx = pb.X - pa.X, dy = pb.Y - pa.Y, l2 = dx * dx + dy * dy;
            if (l2 < 1e-24) return double.PositiveInfinity;
            var t = ((q.X - pa.X) * dx + (q.Y - pa.Y) * dy) / l2;
            return t < -1e-12 || t > 1 + 1e-12 ? double.PositiveInfinity : Math.Abs((q.X - pa.X) * dy - (q.Y - pa.Y) * dx) / Math.Sqrt(l2);
        }
        double VertexD(int v, (double X, double Y) q)
        {
            var pv = pts[v]; double dx = q.X - pv.X, dy = q.Y - pv.Y;
            if (arcs[v] is not { } w) return Math.Sqrt(dx * dx + dy * dy);
            var w0 = w[0]; var wm = w[^1];
            if (dx * (w0.X + wm.X) + dy * (w0.Y + wm.Y) < 0) return double.PositiveInfinity;
            double span = w0.X * wm.Y - w0.Y * wm.X, c0 = w0.X * dy - w0.Y * dx, c1 = dx * wm.Y - dy * wm.X;
            var inCone = span >= 0 ? c0 >= -1e-12 && c1 >= -1e-12 : c0 <= 1e-12 && c1 <= 1e-12;
            if (!inCone) return double.PositiveInfinity;
            var f = double.MinValue; foreach (var u in w) f = Math.Max(f, dx * u.X + dy * u.Y);
            return f;
        }
        bool Valid((double X, double Y) q, int k)
        {
            var gi = gen[k];
            var dg = gi >= 0 ? EdgeD(gi, q) : VertexD(-gi - 1, q);
            if (double.IsInfinity(dg) || Math.Abs(dg - ad) > 1e-6) return false; // nicht im Abstand zur eigenen Strecke/Ecke: Stück liegt im Innern
            // Strecke e verbindet Punkt e und e + 1; käme das Stück einer anderen Stelle zu nah?
            if (polyGrid.Any(q.X - ad, q.Y - ad, q.X + ad, q.Y + ad, e => EdgeD(e, q) < ad - 1e-7 || VertexD(e, q) < ad - 1e-7 || VertexD((e + 1) % np, q) < ad - 1e-7)) return false;
            return dist > 0 != Inside(pts, q); // falsche Seite der Kontur (zuletzt geprüft – teuer, T-0747-fix2)
        }
        // Selbstschnitte der rohen Linie
        var m = raw.Count;
        var rawGrid = BuildGrid([((IReadOnlyList<(double X, double Y)>)raw, true, 0)], AvgLen(raw, true) * 2);
        var splits = new List<(double T, (double X, double Y) P)>?[m];
        for (var k = 0; k < m; k++)
        {
            if ((k & 1023) == 0) ct.ThrowIfCancellationRequested();
            var a = raw[k]; var b = raw[(k + 1) % m];
            foreach (var j in rawGrid.Near(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y)))
            {
                if (j <= k || j == k + 1 || k == 0 && j == m - 1) continue;
                var c = raw[j]; var d = raw[(j + 1) % m];
                if (Hit(a, b, c, d) is not { } h) continue;
                (splits[k] ??= []).Add((h.T, h.P)); (splits[j] ??= []).Add((h.U, h.P));
            }
        }
        var k0 = Array.FindIndex(splits, x => x is { Count: > 0 });
        if (k0 < 0) return Valid(Mid(raw, out var kk), kk) ? [raw] : [];
        foreach (var sp in splits) sp?.Sort((x, y) => x.T.CompareTo(y.T));
        // Stücke zwischen den Schnittpunkten; je Punkt die rohe Strecke, auf der das Stück dorthin läuft (für die Prüfung)
        var pieces = new List<(List<(double X, double Y)> Pts, List<int> Seg)>();
        var s0 = splits[k0]![0].P; List<(double X, double Y)> cur = [s0]; List<int> curSeg = [k0];
        for (var step = 0; step < m; step++)
        {
            var k = (k0 + step) % m; var sp = splits[k] ?? [];
            for (var q = step == 0 ? 1 : 0; q < sp.Count; q++) { cur.Add(sp[q].P); curSeg.Add(k); pieces.Add((cur, curSeg)); cur = [sp[q].P]; curSeg = [k]; }
            cur.Add(raw[(k + 1) % m]); curSeg.Add(k);
        }
        cur.Add(s0); curSeg.Add(k0); pieces.Add((cur, curSeg));
        var valid = new List<List<(double X, double Y)>>();
        var pc = 0;
        foreach (var (pp, seg) in pieces)
        {
            if ((++pc & 1023) == 0) ct.ThrowIfCancellationRequested();
            if (Length(pp, false) < 1e-9) continue;
            var mid = MidOpen(pp, out var at);
            if (Valid(mid, seg[at])) valid.Add(pp);
        }
        // zu Schleifen verbinden: Endpunkt eines Stücks = Anfang des nächsten (derselbe Schnittpunkt)
        static bool Same((double X, double Y) a, (double X, double Y) b) => D2(a, b) < 1e-14;
        var starts = new Dictionary<(double, double), List<int>>();
        for (var i = 0; i < valid.Count; i++) { var key = valid[i][0]; if (!starts.TryGetValue(key, out var l)) starts[key] = l = []; l.Add(i); }
        var used = new bool[valid.Count]; var loops = new List<List<(double X, double Y)>>();
        for (var i = 0; i < valid.Count; i++)
        {
            if (used[i]) continue; used[i] = true;
            var chain = new List<(double X, double Y)>(valid[i]); var first = valid[i][0]; var end = valid[i][^1]; var ok = true;
            for (var guard = 0; !Same(end, first) && guard <= valid.Count; guard++)
            {
                var nx = starts.TryGetValue(end, out var cands) ? cands.FirstOrDefault(c => !used[c], -1) : -1;
                if (nx < 0) nx = Enumerable.Range(0, valid.Count).FirstOrDefault(c => !used[c] && Same(valid[c][0], end), -1);
                if (nx < 0) { ok = false; break; }
                used[nx] = true; chain.AddRange(valid[nx].Skip(1)); end = valid[nx][^1];
            }
            if (!ok || !Same(end, first)) continue;
            var loop = Clean(chain);
            if (loop.Count >= 3) loops.Add(loop);
        }
        return loops;
    }

    /// <summary>Rohe Versatzlinie und je Punkt der Ursprung der Strecke, die dort beginnt: Index der Kontur-Strecke (≥ 0) oder −(Eckpunkt + 1) für Bogenstücke.</summary>
    static (List<(double X, double Y)> Pts, List<int> Gen, (double X, double Y)[]?[] Arcs) RawOffset(List<(double X, double Y)> pts, double dist, double chord)
    {
        var n = pts.Count; var res = new List<(double X, double Y)>(n * 2); var src = new List<int>(n * 2);
        var arcs = new (double X, double Y)[]?[n]; var sg = Math.Sign(dist); // je Ecke mit Bogen: Richtungen der Sehnen-Berührpunkte (die Sehnen berühren den Kreis dort)
        for (var i = 0; i < n; i++)
        {
            var prev = pts[(i - 1 + n) % n]; var cur = pts[i]; var next = pts[(i + 1) % n];
            var d0 = Unit(cur.X - prev.X, cur.Y - prev.Y); var d1 = Unit(next.X - cur.X, next.Y - cur.Y);
            (double X, double Y) n0 = (d0.Y, -d0.X), n1 = (d1.Y, -d1.X); // rechts = außen bei Gegenuhrzeigersinn
            var cross = d0.X * d1.Y - d0.Y * d1.X; var dot = d0.X * d1.X + d0.Y * d1.Y; var turn = Math.Atan2(cross, dot);
            if (Math.Abs(turn) < 1e-9) { res.Add((cur.X + n0.X * dist, cur.Y + n0.Y * dist)); src.Add(i); continue; }
            if (turn * dist > 0)
            {
                // Bogen um die Ecke: Sehnen berühren den Kreis mit Radius dist von außen – die Fräsermitte kommt nie näher als dist an die Ecke
                var m = Math.Max(1, (int)Math.Ceiling(CadContours.Segments(Math.Abs(dist), 2 * Math.PI, chord) * Math.Abs(turn) / (2 * Math.PI)));
                var a0 = Math.Atan2(n0.Y, n0.X); var rr = dist / Math.Cos(turn / (2 * m));
                res.Add((cur.X + n0.X * dist, cur.Y + n0.Y * dist)); src.Add(-i - 1);
                arcs[i] = Enumerable.Range(0, m + 1).Select(j => (sg * Math.Cos(a0 + turn * j / m), sg * Math.Sin(a0 + turn * j / m))).ToArray();
                for (var k = 1; k <= m; k++) { var a = a0 + turn * (k - 0.5) / m; res.Add((cur.X + Math.Cos(a) * rr, cur.Y + Math.Sin(a) * rr)); src.Add(-i - 1); }
                res.Add((cur.X + n1.X * dist, cur.Y + n1.Y * dist)); src.Add(i);
            }
            else
            {
                // Innenecke: Verbinder über den Eckpunkt (wie Clipper). Jede verschobene Strecke bleibt so ganz in ihrem Streifen; der Schnittpunkt
                // (die „Gehrung“) entsteht beim Beschneiden. Die Verbinder liegen näher als der Abstand an der Kontur und fallen dabei weg.
                res.Add((cur.X + n0.X * dist, cur.Y + n0.Y * dist)); src.Add(-i - 1);
                res.Add(cur); src.Add(-i - 1);
                res.Add((cur.X + n1.X * dist, cur.Y + n1.Y * dist)); src.Add(i);
            }
        }
        // doppelte Punkte entfernen; der verbleibende Punkt übernimmt den Ursprung der Strecke, die tatsächlich bei ihm beginnt
        var outP = new List<(double X, double Y)>(res.Count); var outS = new List<int>(res.Count);
        for (var i = 0; i < res.Count; i++)
        {
            if (outP.Count > 0 && D2(outP[^1], res[i]) <= 1e-18) { outS[^1] = src[i]; continue; }
            outP.Add(res[i]); outS.Add(src[i]);
        }
        while (outP.Count > 1 && D2(outP[0], outP[^1]) <= 1e-18) { outP.RemoveAt(outP.Count - 1); outS.RemoveAt(outS.Count - 1); }
        return (outP, outS, arcs);
    }

    /// <summary>Schnitt zweier Strecken (auch Berührung an einem Endpunkt, halboffen [0, 1)); parallele Strecken zählen nicht.</summary>
    static (double T, double U, (double X, double Y) P)? Hit((double X, double Y) a, (double X, double Y) b, (double X, double Y) c, (double X, double Y) d)
    {
        double rx = b.X - a.X, ry = b.Y - a.Y, sx = d.X - c.X, sy = d.Y - c.Y, den = rx * sy - ry * sx;
        if (Math.Abs(den) < 1e-18) return null;
        double t = ((c.X - a.X) * sy - (c.Y - a.Y) * sx) / den, u = ((c.X - a.X) * ry - (c.Y - a.Y) * rx) / den;
        const double e = 1e-12;
        if (!(t >= -e && t < 1 - e && u >= -e && u < 1 - e)) return null;
        t = Math.Max(0, t); u = Math.Max(0, u);
        return (t, u, (a.X + rx * t, a.Y + ry * t));
    }

    // ---------------------------------------------------------------- Prüfungen

    /// <summary>T-0747-fix4: Toleranz für „deckungsgleich“ (doppelt gezeichnete Konturen).</summary>
    public const double DupTol = 0.001;

    /// <summary>T-0747-fix4: Sind zwei geschlossene Züge deckungsgleich (jeder Punkt des einen höchstens <paramref name="tol"/> vom anderen Zug,
    /// in beide Richtungen, auch Stückmitten)? Richtung und Startecke spielen keine Rolle.</summary>
    public static bool Coincident(List<(double X, double Y)> a, List<(double X, double Y)> b, double tol)
    {
        var (ax0, ay0, ax1, ay1) = (a.Min(p => p.X), a.Min(p => p.Y), a.Max(p => p.X), a.Max(p => p.Y));
        var (bx0, by0, bx1, by1) = (b.Min(p => p.X), b.Min(p => p.Y), b.Max(p => p.X), b.Max(p => p.Y));
        if (Math.Abs(ax0 - bx0) > tol || Math.Abs(ay0 - by0) > tol || Math.Abs(ax1 - bx1) > tol || Math.Abs(ay1 - by1) > tol) return false;
        if (Math.Abs(Math.Abs(SignedArea(a)) - Math.Abs(SignedArea(b))) > tol * (Length(a, true) + Length(b, true))) return false;
        static bool Covered(List<(double X, double Y)> p, List<(double X, double Y)> q, double tol)
        {
            var g = BuildGrid([((IReadOnlyList<(double X, double Y)>)q, true, 0)], Math.Max(tol * 4, AvgLen(q, true)));
            for (var i = 0; i < p.Count; i++)
            {
                var u = p[i]; var v = p[(i + 1) % p.Count]; (double X, double Y) m = ((u.X + v.X) / 2, (u.Y + v.Y) / 2);
                if (!g.AnyWithin(u, tol + 1e-9) || !g.AnyWithin(m, tol + 1e-9)) return false;
            }
            return true;
        }
        return Covered(a, b, tol) && Covered(b, a, tol);
    }

    /// <summary>T-0747-fix2: berührt oder schneidet der Zug <paramref name="a"/> den geschlossenen Zug <paramref name="b"/>?</summary>
    static bool Touches(List<(double X, double Y)> a, bool aClosed, List<(double X, double Y)> b)
    {
        var grid = BuildGrid([((IReadOnlyList<(double X, double Y)>)b, true, 0)], AvgLen(b, true) * 2);
        for (var k = 0; k < a.Count - (aClosed ? 0 : 1); k++)
        {
            var p = a[k]; var q = a[(k + 1) % a.Count];
            foreach (var j in grid.Near(Math.Min(p.X, q.X), Math.Min(p.Y, q.Y), Math.Max(p.X, q.X), Math.Max(p.Y, q.Y)))
                if (SegSeg(p, q, grid.Segs[j].A, grid.Segs[j].B) < 1e-7) return true;
        }
        return false;
    }

    /// <summary>T-0747-fix: kreuzt oder berührt sich der geschlossene Zug selbst (nicht benachbarte Strecken)?</summary>
    public static bool SelfIntersects(IReadOnlyList<(double X, double Y)> poly)
    {
        var p = Clean(poly); var n = p.Count; if (n < 4) return false;
        var grid = BuildGrid([((IReadOnlyList<(double X, double Y)>)p, true, 0)], AvgLen(p, true) * 2);
        for (var k = 0; k < n; k++)
        {
            var a = p[k]; var b = p[(k + 1) % n];
            foreach (var j in grid.Near(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y)))
            {
                if (j <= k || j == k + 1 || k == 0 && j == n - 1) continue;
                if (SegSeg(a, b, p[j], p[(j + 1) % n]) < 1e-7) return true;
            }
        }
        return false;
    }

    /// <summary>T-0747-fix: Stellen der Kontur, an die der Fräser (Mitte auf den Bahnen) nicht herankommt – weiter als Radius + halber Radius
    /// (mindestens 0,2 mm). Normale Innenecken (90°) liegen darunter. Ergebnis: Mitte jeder zusammenhängenden Stelle (Messpunkte alle r/8, 0,02–0,25 mm).</summary>
    static List<(double X, double Y)> Unreached(IReadOnlyList<(double X, double Y)> contour, List<List<(double X, double Y)>> loops, double r, CancellationToken ct = default)
    {
        var reach = r + Math.Max(0.5 * r, 0.2);
        var grid = BuildGrid(loops.Select(l => ((IReadOnlyList<(double X, double Y)>)l, true, 0)), r);
        var total = Length(contour, true); var step = Math.Clamp(r / 8, 0.02, 0.25); if (total / step > 50_000) step = total / 50_000; // T-0747-fix2: feiner, damit auch kurze Engstellen auffallen
        var samples = new List<((double X, double Y) P, bool Ok)>();
        for (var i = 0; i < contour.Count; i++)
        {
            var a = contour[i]; var b = contour[(i + 1) % contour.Count]; var len = Math.Sqrt(D2(a, b)); var k = Math.Max(1, (int)Math.Ceiling(len / step));
            if ((i & 255) == 0) ct.ThrowIfCancellationRequested();
            for (var j = 0; j < k; j++) { (double X, double Y) q = (a.X + (b.X - a.X) * j / k, a.Y + (b.Y - a.Y) * j / k); samples.Add((q, grid.AnyWithin(q, reach))); }
        }
        var res = new List<(double X, double Y)>(); var ns = samples.Count;
        if (ns == 0) return res;
        var s0 = samples.FindIndex(x => x.Ok);
        if (s0 < 0) return [samples[ns / 2].P];
        for (var q = 1; q <= ns; q++)
        {
            var idx = (s0 + q) % ns; if (samples[idx].Ok) continue;
            var run = 0; while (!samples[(idx + run) % ns].Ok) run++;
            res.Add(samples[(idx + run / 2) % ns].P); // T-0747-fix2: auch ein einzelner Messpunkt zählt (kurze Engstelle)
            q += run;
        }
        return res;
    }

    /// <summary>Nächste Kontur, der die Bahn näher als <paramref name="limit"/> kommt (Tag −1 = keine). <paramref name="skip"/>: eigene Kontur bei Bahnen auf der Linie.</summary>
    static (int Tag, double D) Nearest(List<(double X, double Y)> path, bool closed, SegGrid obstacles, double limit, int skip, CancellationToken ct = default, SegGrid.Cursor? cur = null)
    {
        cur ??= obstacles.Own; // ohne eigenen Merker: der des Rasters (nur einfädig)
        // T-0747-fix5: erst schnell prüfen, ob überhaupt etwas zu nah ist (ohne Listen, Abbruch beim ersten Treffer) – meist nicht
        var any = false; var mm = closed ? path.Count : path.Count - 1;
        for (var i = 0; i < mm && !any; i++)
        {
            if ((i & 1023) == 0) ct.ThrowIfCancellationRequested();
            var a0 = path[i]; var b0 = path[(i + 1) % path.Count];
            any = obstacles.Any(cur, Math.Min(a0.X, b0.X) - limit, Math.Min(a0.Y, b0.Y) - limit, Math.Max(a0.X, b0.X) + limit, Math.Max(a0.Y, b0.Y) + limit,
                j => obstacles.Segs[j].Tag != skip && SegSeg(a0, b0, obstacles.Segs[j].A, obstacles.Segs[j].B) < limit);
        }
        if (!any) return (-1, double.MaxValue);
        var best = (Tag: -1, D: double.MaxValue); var m = closed ? path.Count : path.Count - 1;
        for (var i = 0; i < m; i++)
        {
            if ((i & 1023) == 0) ct.ThrowIfCancellationRequested();
            var a = path[i]; var b = path[(i + 1) % path.Count];
            foreach (var j in obstacles.Near(cur, Math.Min(a.X, b.X) - limit, Math.Min(a.Y, b.Y) - limit, Math.Max(a.X, b.X) + limit, Math.Max(a.Y, b.Y) + limit))
            {
                var (c, d, tag) = obstacles.Segs[j]; if (tag == skip) continue;
                var dd = SegSeg(a, b, c, d);
                if (dd < limit && dd < best.D) best = (tag, dd);
            }
        }
        return best;
    }

    /// <summary>T-0747-fix5: Schleife über 0..n−1 auf allen Kernen, abbrechbar; Fehler kommen unverpackt heraus.</summary>
    static void Par(int n, CancellationToken ct, Action<int> body)
    {
        ct.ThrowIfCancellationRequested();
        try { Parallel.For(0, n, new ParallelOptions { CancellationToken = ct }, body); }
        catch (AggregateException ae) when (ae.InnerExceptions.Count > 0)
        {
            var e = ae.InnerExceptions.FirstOrDefault(x => x is not OperationCanceledException) ?? ae.InnerExceptions[0];
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e).Throw();
        }
    }

    // ---------------------------------------------------------------- Glätten

    /// <summary>T-0747-fix: Douglas-Peucker für geschlossene Züge mit Toleranz <paramref name="eps"/>. Liefert auch die tatsächliche größte Abweichung,
    /// die der Versatz zusätzlich bekommt – so bleibt der Abstand zur Originalkontur mindestens der Radius.</summary>
    public static (List<(double X, double Y)> Pts, double Dev) Simplify(IReadOnlyList<(double X, double Y)> poly, double eps)
    {
        var p = Clean(poly); var n = p.Count; if (n < 8 || eps <= 0) return (p, 0);
        var far = Enumerable.Range(1, n - 1).MaxBy(k => D2(p[0], p[k]));
        var keep = new bool[n]; keep[0] = keep[far] = true; var dev = 0.0;
        var stack = new Stack<(int A, int B)>(); stack.Push((0, far)); stack.Push((far, n));
        while (stack.Count > 0)
        {
            var (a, b) = stack.Pop(); if (b - a < 2) continue;
            var pa = p[a]; var pb = p[b % n]; double max = -1; var at = -1;
            for (var k = a + 1; k < b; k++) { var dk = SegDist(p[k], pa, pb); if (dk > max) { max = dk; at = k; } }
            if (max > eps) { keep[at] = true; stack.Push((a, at)); stack.Push((at, b)); }
            else dev = Math.Max(dev, max);
        }
        var res = p.Where((_, k) => keep[k]).ToList();
        return res.Count >= 3 && Math.Abs(SignedArea(res)) > 1e-9 ? (res, dev) : (p, 0);
    }

    // ---------------------------------------------------------------- Hilfen

    /// <summary>Kleinster Abstand der Punkte von <paramref name="path"/> (auch Stückmitten) zum Zug <paramref name="poly"/>.</summary>
    public static double MinDistance(IReadOnlyList<(double X, double Y)> path, IReadOnlyList<(double X, double Y)> poly, bool closed)
    {
        var min = double.MaxValue; var m = poly.Count - (closed ? 0 : 1);
        for (var i = 0; i < path.Count; i++)
        {
            var q = path[i]; var q2 = path[(i + 1) % path.Count]; var mid = ((q.X + q2.X) / 2, (q.Y + q2.Y) / 2);
            for (var j = 0; j < m; j++) { var a = poly[j]; var b = poly[(j + 1) % poly.Count]; min = Math.Min(min, Math.Min(SegDist(q, a, b), SegDist(mid, a, b))); }
        }
        return min;
    }

    public static double SegDist((double X, double Y) p, (double X, double Y) a, (double X, double Y) b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y, l2 = dx * dx + dy * dy;
        var t = l2 < 1e-18 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / l2, 0, 1);
        double x = a.X + dx * t - p.X, y = a.Y + dy * t - p.Y; return Math.Sqrt(x * x + y * y);
    }

    /// <summary>T-0747-fix: kleinster Abstand zweier Strecken (0, wenn sie sich kreuzen).</summary>
    public static double SegSeg((double X, double Y) a, (double X, double Y) b, (double X, double Y) c, (double X, double Y) d)
    {
        double o1 = Orient(c, d, a), o2 = Orient(c, d, b), o3 = Orient(a, b, c), o4 = Orient(a, b, d);
        if ((o1 > 0 && o2 < 0 || o1 < 0 && o2 > 0) && (o3 > 0 && o4 < 0 || o3 < 0 && o4 > 0)) return 0;
        return Math.Min(Math.Min(SegDist(a, c, d), SegDist(b, c, d)), Math.Min(SegDist(c, a, b), SegDist(d, a, b)));
    }

    static double Orient((double X, double Y) o, (double X, double Y) a, (double X, double Y) b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);

    /// <summary>T-0747-fix17: Punkt in geschlossenem Zug (gerade/ungerade) über waagrechte Streifen – je Abfrage nur die Strecken des Streifens.
    /// Die Streifenzahl wird so gewählt, dass der Index höchstens etwa 16 × so groß wird wie der Zug.</summary>
    sealed class RowIndex
    {
        // Streifen waagrecht (Strahl nach rechts) oder – wenn das besser verteilt (z. B. Zickzack mit hohen Zähnen) – senkrecht (Strahl nach oben)
        readonly (double X, double Y)[] _p; readonly double _y0, _h; readonly List<int>[] _rows; readonly bool _swap;
        public RowIndex(IReadOnlyList<(double X, double Y)> p)
        {
            var (rowsY, costY) = Plan(p, false); var (rowsX, costX) = Plan(p, true);
            _swap = rowsX > rowsY || rowsX == rowsY && costX < costY;
            _p = p.Select(q => _swap ? (q.Y, q.X) : q).ToArray();
            var n = _p.Length; _y0 = _p.Min(q => q.Y); var y1 = _p.Max(q => q.Y); var rows = _swap ? rowsX : rowsY;
            _h = Math.Max((y1 - _y0) / rows, 1e-12); _rows = new List<int>[rows];
            for (var r = 0; r < rows; r++) _rows[r] = [];
            for (var i = 0; i < n; i++)
            {
                var a = _p[i]; var b = _p[(i + 1) % n];
                var r0 = Math.Clamp((int)Math.Floor((Math.Min(a.Y, b.Y) - _y0) / _h), 0, rows - 1); var r1 = Math.Clamp((int)Math.Floor((Math.Max(a.Y, b.Y) - _y0) / _h), 0, rows - 1);
                for (var r = r0; r <= r1; r++) _rows[r].Add(i);
            }
        }
        static (int Rows, long Cost) Plan(IReadOnlyList<(double X, double Y)> p, bool swap)
        {
            var n = p.Count; double Y(int i) => swap ? p[i].X : p[i].Y;
            var y0 = double.MaxValue; var y1 = double.MinValue; for (var i = 0; i < n; i++) { y0 = Math.Min(y0, Y(i)); y1 = Math.Max(y1, Y(i)); }
            var rows = Math.Clamp(n / 8, 1, 1 << 16); long tot = 0;
            while (rows > 1)
            {
                double h = Math.Max((y1 - y0) / rows, 1e-12); tot = 0;
                for (var i = 0; i < n && tot <= 16L * n; i++) { double a = Y(i), b = Y((i + 1) % n); tot += (long)Math.Floor((Math.Max(a, b) - y0) / h) - (long)Math.Floor((Math.Min(a, b) - y0) / h) + 1; }
                if (tot <= 16L * n) break;
                rows /= 4;
            }
            return (rows, tot);
        }
        public bool Contains(double x, double y)
        {
            if (_swap) (x, y) = (y, x);
            var r = (int)Math.Floor((y - _y0) / _h); if (r < 0 || r >= _rows.Length) return false;
            var inside = false;
            foreach (var i in _rows[r])
            {
                var (xi, yi) = _p[(i + 1) % _p.Length]; var (xj, yj) = _p[i];
                if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) inside = !inside;
            }
            return inside;
        }
    }

    /// <summary>T-0747-fix15: konvexe Hülle (Monotone Chain), gegen den Uhrzeigersinn; bei weniger als 3 verschiedenen Punkten die Punkte selbst.</summary>
    static List<(double X, double Y)> Hull(List<(double X, double Y)> pts)
    {
        var p = pts.Distinct().OrderBy(q => q.X).ThenBy(q => q.Y).ToList();
        if (p.Count < 3) return p;
        static double Cross((double X, double Y) o, (double X, double Y) a, (double X, double Y) b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
        var h = new List<(double X, double Y)>();
        foreach (var q in p) { while (h.Count >= 2 && Cross(h[^2], h[^1], q) <= 0) h.RemoveAt(h.Count - 1); h.Add(q); }
        var lower = h.Count + 1;
        for (var i = p.Count - 2; i >= 0; i--) { var q = p[i]; while (h.Count >= lower && Cross(h[^2], h[^1], q) <= 0) h.RemoveAt(h.Count - 1); h.Add(q); }
        h.RemoveAt(h.Count - 1);
        return h;
    }

    /// <summary>T-0747-fix16: Punkt in konvexer Hülle (gegen den Uhrzeigersinn) in O(log H): Fächer ab h[0], Binärsuche des Keils, dann die Außenkante.
    /// <paramref name="m"/> ≤ 0: höchstens |m| außerhalb zählt als drin; <paramref name="m"/> &gt; 0: mindestens m innerhalb (Keilkante und die beiden
    /// Fächerkanten am Rand; nahe anderen Ecken eher „drin“ – also vorsichtig).</summary>
    static bool InConvexLog(List<(double X, double Y)> h, (double X, double Y) q, double m)
    {
        var n = h.Count; if (n < 3) return false;
        if (n <= 64) return InConvex(h, q, -m, m > 0);
        static double Cr((double X, double Y) o, (double X, double Y) a, (double X, double Y) b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
        static double D((double X, double Y) a, (double X, double Y) b, (double X, double Y) q) { var l = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y)); return l > 0 ? Cr(a, b, q) / l : 0; }
        var h0 = h[0];
        if (Cr(h0, h[1], q) < 0 || Cr(h0, h[n - 1], q) > 0)
            return m <= 0 && (SegDist(q, h0, h[1]) <= -m || SegDist(q, h0, h[n - 1]) <= -m); // außerhalb des Fächers, höchstens knapp am Rand
        int lo = 1, hi = n - 1;
        while (hi - lo > 1) { var mid = (lo + hi) / 2; if (Cr(h0, h[mid], q) >= 0) lo = mid; else hi = mid; }
        var d = D(h[lo], h[lo + 1], q);
        if (m <= 0) return d >= m;
        return d >= m && D(h0, h[1], q) >= m && D(h[n - 1], h0, q) >= m;
    }

    /// <summary>T-0747-fix15: liegt der Punkt in der konvexen Hülle (gegen den Uhrzeigersinn) oder höchstens <paramref name="e"/> davor?</summary>
    static bool InConvex(List<(double X, double Y)> h, (double X, double Y) q, double e, bool strict = false)
    {
        if (strict) // T-0747-fix16: mindestens e innerhalb jeder Kante
        {
            for (var i = 0; i < h.Count; i++)
            {
                var a = h[i]; var b = h[(i + 1) % h.Count]; double dx = b.X - a.X, dy = b.Y - a.Y, l = Math.Sqrt(dx * dx + dy * dy);
                if (l > 0 && (dx * (q.Y - a.Y) - dy * (q.X - a.X)) / l < -e) return false;
            }
            return true;
        }
        for (var i = 0; i < h.Count; i++)
        {
            var a = h[i]; var b = h[(i + 1) % h.Count]; double dx = b.X - a.X, dy = b.Y - a.Y, l = Math.Sqrt(dx * dx + dy * dy);
            if (l > 0 && (dx * (q.Y - a.Y) - dy * (q.X - a.X)) / l < -e) return false;
        }
        return true;
    }

    public static double SignedArea(IReadOnlyList<(double X, double Y)> p)
    {
        var a = 0.0; for (var i = 0; i < p.Count; i++) { var q = p[i]; var w = p[(i + 1) % p.Count]; a += q.X * w.Y - w.X * q.Y; }
        return a / 2;
    }

    static bool Inside(List<(double X, double Y)> pts, (double X, double Y) q)
    {
        var inside = false;
        for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
        {
            var (xi, yi) = pts[i]; var (xj, yj) = pts[j];
            if ((yi > q.Y) != (yj > q.Y) && q.X < (xj - xi) * (q.Y - yi) / (yj - yi) + xi) inside = !inside;
        }
        return inside;
    }

    static double Length(IReadOnlyList<(double X, double Y)> p, bool closed)
    {
        var l = 0.0; for (var i = 0; i < p.Count - (closed ? 0 : 1); i++) l += Math.Sqrt(D2(p[i], p[(i + 1) % p.Count]));
        return l;
    }

    static double AvgLen(IReadOnlyList<(double X, double Y)> p, bool closed) => Math.Max(1e-6, Length(p, closed) / Math.Max(1, p.Count - (closed ? 0 : 1)));

    /// <summary>Punkt auf halber Länge eines geschlossenen Zugs; <paramref name="seg"/> = Strecke, auf der er liegt.</summary>
    static (double X, double Y) Mid(List<(double X, double Y)> p, out int seg)
    {
        var half = Length(p, true) / 2; var n = p.Count; seg = 0;
        for (var i = 0; i < n; i++)
        {
            var a = p[i]; var b = p[(i + 1) % n]; var l = Math.Sqrt(D2(a, b));
            if (half <= l || i == n - 1) { seg = i; var f = l < 1e-15 ? 0 : Math.Clamp(half / l, 0, 1); return (a.X + (b.X - a.X) * f, a.Y + (b.Y - a.Y) * f); }
            half -= l;
        }
        return p[0];
    }

    /// <summary>Punkt auf halber Länge eines offenen Stücks; <paramref name="at"/> = Index des Stückpunkts, an dem die Strecke dorthin endet.</summary>
    static (double X, double Y) MidOpen(List<(double X, double Y)> p, out int at)
    {
        var half = Length(p, false) / 2; at = 1;
        for (var i = 0; i + 1 < p.Count; i++)
        {
            var a = p[i]; var b = p[i + 1]; var l = Math.Sqrt(D2(a, b));
            if (half <= l || i + 2 == p.Count) { at = i + 1; var f = l < 1e-15 ? 0 : Math.Clamp(half / l, 0, 1); return (a.X + (b.X - a.X) * f, a.Y + (b.Y - a.Y) * f); }
            half -= l;
        }
        return p[0];
    }

    static List<(double X, double Y)> Clean(IReadOnlyList<(double X, double Y)> p)
    {
        var res = new List<(double X, double Y)>(p.Count);
        foreach (var q in p) if (res.Count == 0 || D2(res[^1], q) > 1e-12) res.Add(q);
        while (res.Count > 1 && D2(res[0], res[^1]) <= 1e-12) res.RemoveAt(res.Count - 1);
        return res;
    }

    static (double X, double Y) Unit(double x, double y) { var l = Math.Sqrt(x * x + y * y); return l < 1e-15 ? (1, 0) : (x / l, y / l); }
    static double D2((double X, double Y) a, (double X, double Y) b) => (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);

    static SegGrid BuildGrid(IEnumerable<(IReadOnlyList<(double X, double Y)> Pts, bool Closed, int Tag)> polys, double cell, CancellationToken ct = default)
    {
        var list = polys.ToList();
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var (pts, _, _) in list) foreach (var (x, y) in pts) { minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); }
        var diag = minX > maxX ? 1 : Math.Sqrt((maxX - minX) * (maxX - minX) + (maxY - minY) * (maxY - minY));
        var g = new SegGrid(Math.Max(cell, diag / 2048));
        var n = 0; // T-0747-fix17: abbrechbar
        foreach (var (pts, closed, tag) in list) for (var i = 0; i < pts.Count - (closed ? 0 : 1); i++) { if ((++n & 4095) == 0) ct.ThrowIfCancellationRequested(); g.Add(pts[i], pts[(i + 1) % pts.Count], tag); }
        return g;
    }
}

/// <summary>T-0747-fix: Rasterindex für Strecken – „welche Strecken liegen in diesem Rechteck“ und „gibt es eine Strecke näher als d“, ohne alle
/// Strecken durchzugehen (Fräsbahn-Prüfung, Versatz, Kettenbildung, Wandpaare).</summary>
public sealed class SegGrid(double cell)
{
    readonly double _cell = Math.Max(cell, 1e-9);
    /// <summary>T-0747-fix12: Zellgröße (für die Wahl Raster-Abfrage oder lineares Durchgehen bei sehr großen Abfragerechtecken).</summary>
    public double CellSize => _cell;
    readonly Dictionary<long, List<int>> _map = [];
    /// <summary>T-0747-fix5: Merker für „schon gesehen“ – je Thread ein eigener, dann darf das Raster parallel abgefragt werden (nur lesen).</summary>
    public sealed class Cursor { internal int[] Stamp = new int[16]; internal int Query; }
    readonly Cursor _own = new();
    internal Cursor Own => _own;
    public List<((double X, double Y) A, (double X, double Y) B, int Tag)> Segs { get; } = [];

    static long Key(long x, long y) => (x << 32) ^ (y & 0xFFFFFFFFL);
    long C(double v) => (long)Math.Floor(v / _cell);

    public int Add((double X, double Y) a, (double X, double Y) b, int tag)
    {
        var i = Segs.Count; Segs.Add((a, b, tag));
        for (var x = C(Math.Min(a.X, b.X)); x <= C(Math.Max(a.X, b.X)); x++)
            for (var y = C(Math.Min(a.Y, b.Y)); y <= C(Math.Max(a.Y, b.Y)); y++)
            {
                var k = Key(x, y); if (!_map.TryGetValue(k, out var l)) _map[k] = l = []; l.Add(i);
            }
        return i;
    }

    /// <summary>Strecken, deren Rasterzellen das Rechteck berühren (jede höchstens einmal).</summary>
    public List<int> Near(double minX, double minY, double maxX, double maxY) => Near(_own, minX, minY, maxX, maxY);

    static void Next(Cursor c, int n)
    {
        if (c.Stamp.Length < n) Array.Resize(ref c.Stamp, Math.Max(n, c.Stamp.Length * 2));
        if (++c.Query == int.MaxValue) { Array.Clear(c.Stamp); c.Query = 1; }
    }

    public List<int> Near(Cursor cur, double minX, double minY, double maxX, double maxY)
    {
        Next(cur, Segs.Count); var _stamp = cur.Stamp; var _query = cur.Query;
        var res = new List<int>();
        for (var x = C(minX); x <= C(maxX); x++)
            for (var y = C(minY); y <= C(maxY); y++)
                if (_map.TryGetValue(Key(x, y), out var l))
                    foreach (var i in l) if (_stamp[i] != _query) { _stamp[i] = _query; res.Add(i); }
        return res;
    }

    /// <summary>T-0747-fix2: wie <see cref="Near"/>, aber ohne Liste – bricht ab, sobald <paramref name="hit"/> wahr ist.</summary>
    public bool Any(double minX, double minY, double maxX, double maxY, Func<int, bool> hit) => Any(_own, minX, minY, maxX, maxY, hit);

    public bool Any(Cursor cur, double minX, double minY, double maxX, double maxY, Func<int, bool> hit)
    {
        Next(cur, Segs.Count); var _stamp = cur.Stamp; var _query = cur.Query;
        for (var x = C(minX); x <= C(maxX); x++)
            for (var y = C(minY); y <= C(maxY); y++)
                if (_map.TryGetValue(Key(x, y), out var l))
                    foreach (var i in l) if (_stamp[i] != _query) { _stamp[i] = _query; if (hit(i)) return true; }
        return false;
    }

    /// <summary>Gibt es eine Strecke, die dem Punkt näher als <paramref name="d"/> kommt?</summary>
    public bool AnyWithin((double X, double Y) q, double d) => d > 0 && Any(q.X - d, q.Y - d, q.X + d, q.Y + d, i => CncCam.SegDist(q, Segs[i].A, Segs[i].B) < d);
}
