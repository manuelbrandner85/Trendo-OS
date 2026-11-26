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
    /// <summary>T-0759-fix1: Lage der Texte (TEXT/MTEXT, Zeichnungskoordinaten) – Beleg für ein Schriftfeld eines Zeichnungsrahmens.</summary>
    public IReadOnlyList<(double X, double Y)> Texts { get; init; } = [];
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
public static partial class CncCam
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

    /// <summary>T-0747-fix19: Gesamtgrenze je Datei für Schutzbereich und Linienzug-Regel (Schritte). Nur für Tests verstellbar, je Thread; <c>null</c> = 150 Mio.</summary>
    [ThreadStatic] public static long? ProtectFileBudget;

    public static CamResult Generate(IReadOnlyList<CadContour> source, CamSettings s, CancellationToken ct = default)
    {
        if (source.Count == 0) throw new InvalidOperationException("Die Zeichnung hat keine Konturen (Linien, Kreise, Bögen, Pfade).");
        if (s.ToolD <= 0 || s.Depth <= 0 || s.StepDown <= 0 || s.Feed <= 0 || s.Plunge <= 0) throw new ArgumentException("Fräser-Ø, Tiefe, Zustellung und Vorschübe müssen größer als 0 sein.");
        if (s.Tabs > 0 && (s.TabH <= 0 || s.TabH >= s.Depth)) throw new ArgumentException("Haltestege: Höhe muss größer als 0 und kleiner als die Tiefe sein.");
        var r = s.ToolD / 2; var warn = new List<string>();
        var frameUnsure = new HashSet<int>();
        source = DropFrame(source, s, warn, frameUnsure, ct); // T-0759-fix2: Verdacht auf Zeichnungsrahmen – nichts entfernt, Umriss und Inhalt ausgelassen
        // verschieben: linke untere Ecke aufs Nullpunkt
        double sx = 0, sy = 0;
        if (s.ToOrigin) { sx = -source.Min(c => c.Pts.Min(p => p.X)); sy = -source.Min(c => c.Pts.Min(p => p.Y)); }
        var cs = source.Select((c, i) => { if ((i & 255) == 0) ct.ThrowIfCancellationRequested(); return c with { Pts = c.Pts.Select(p => (p.X + sx, p.Y + sy)).ToList() }; }).ToList(); // T-0758: abbrechbar
        string Name(int i) => $"Kontur {i + 1}" + (cs[i].Layer is { Length: > 0 } and not "0" ? $" (Ebene {cs[i].Layer})" : "");
        // T-0747-fix4: doppelt gezeichnete (deckungsgleiche) geschlossene Konturen – egal welche Richtung oder Startecke – nur einmal verwenden. Sonst zählt
        // ein Loch in einer doppelten Platte zwei Umrisse und wird als „außen“ gefräst (um den Fräser-Ø zu groß).
        var dup = new bool[cs.Count];
        var dupSel = new DupSelector(); // T-0772
        // T-0747-fix5: Vorfilter – nur Paare mit fast gleichem Rahmen (Raster über die linke untere Ecke) und fast gleicher Fläche werden genau geprüft
        var boxes = cs.Select((c, i) => { if ((i & 255) == 0) ct.ThrowIfCancellationRequested(); return c.Closed ? c.Box : (0.0, 0.0, -1.0, -1.0); }).ToArray();
        var areas = cs.Select(c => c.Closed ? Math.Abs(c.SignedArea) : 0).ToArray();
        var perims = cs.Select((c, i) => { if ((i & 255) == 0) ct.ThrowIfCancellationRequested(); return c.Closed ? Length(c.Pts, true) : 0; }).ToArray();
        const double dupCell = 0.01;
        // T-0758: Raster über alle vier Rahmenwerte statt nur die linke untere Ecke – ein Paar, das die Rahmenprüfung (je Wert ≤ DupTol ≤ dupCell) besteht,
        // liegt in benachbarten Zellen; die Kandidaten sind also dieselben, nur ohne die vielen Konturen mit gleicher Ecke und anderem Rahmen
        // (20 000 Kreise im „Eimer“: vorher 10 s). Rahmen mit nicht endlichen Werten (dort besteht jeder Vergleich) laufen wie bisher über die Ecke.
        static bool Fin((double, double, double, double) b) => double.IsFinite(b.Item1) && double.IsFinite(b.Item2) && double.IsFinite(b.Item3) && double.IsFinite(b.Item4);
        static long K(double v) => (long)Math.Floor(v / dupCell);
        var corner = new Dictionary<(long, long), List<int>>(); var odd = new Dictionary<(long, long), List<int>>();
        var frame4 = new Dictionary<(long, long, long, long), List<int>>();
        for (var i = 0; i < cs.Count; i++)
            if (cs[i].Closed)
            {
                var key = (K(boxes[i].Item1), K(boxes[i].Item2)); if (!corner.TryGetValue(key, out var l)) corner[key] = l = []; l.Add(i);
                if (!Fin(boxes[i])) { if (!odd.TryGetValue(key, out var o)) odd[key] = o = []; o.Add(i); continue; }
                var k4 = (key.Item1, key.Item2, K(boxes[i].Item3), K(boxes[i].Item4)); if (!frame4.TryGetValue(k4, out var f)) frame4[k4] = f = []; f.Add(i);
            }
        for (var i = 0; i < cs.Count; i++)
        {
            if (!cs[i].Closed || dup[i]) continue;
            if ((i & 255) == 0) ct.ThrowIfCancellationRequested();
            var (kx, ky) = (K(boxes[i].Item1), K(boxes[i].Item2));
            var near = new List<int>();
            if (!Fin(boxes[i])) { for (var dx = -1L; dx <= 1; dx++) for (var dy = -1L; dy <= 1; dy++) if (corner.TryGetValue((kx + dx, ky + dy), out var l)) near.AddRange(l.Where(j => j > i)); }
            else
            {
                var (kx1, ky1) = (K(boxes[i].Item3), K(boxes[i].Item4));
                for (var dx = -1L; dx <= 1; dx++) for (var dy = -1L; dy <= 1; dy++)
                {
                    if (odd.TryGetValue((kx + dx, ky + dy), out var o)) near.AddRange(o.Where(j => j > i));
                    for (var ex = -1L; ex <= 1; ex++) for (var ey = -1L; ey <= 1; ey++) if (frame4.TryGetValue((kx + dx, ky + dy, kx1 + ex, ky1 + ey), out var l)) near.AddRange(l.Where(j => j > i));
                }
            }
            near.Sort();
            if (near.Count > 64 && dupSel.Candidates(cs, i, near, ct) is { } sel) near = sel; // T-0772: viele fast gleiche Rahmen – nur mögliche Doppel prüfen
            foreach (var j in near)
            {
                if (dup[j]) continue;
                ct.ThrowIfCancellationRequested();
                var (a, b) = (boxes[i], boxes[j]);
                if (Math.Abs(a.Item1 - b.Item1) > DupTol || Math.Abs(a.Item2 - b.Item2) > DupTol || Math.Abs(a.Item3 - b.Item3) > DupTol || Math.Abs(a.Item4 - b.Item4) > DupTol) continue;
                if (Math.Abs(areas[i] - areas[j]) > DupTol * (perims[i] + perims[j])) continue;
                if (!Coincident(cs[i].Pts, cs[j].Pts, DupTol, ct)) continue;
                dup[j] = true;
                warn.Add($"{Name(j)}: doppelt gezeichnet (deckungsgleich mit Kontur {i + 1}) – einmal verwendet.");
            }
        }
        // T-0747-fix: selbst schneidende Konturen (Acht) ablehnen – innen und außen sind dort nicht eindeutig
        var crossing = new bool[cs.Count]; var annClosed = new bool[cs.Count];
        long annScan = 0;
        bool ContainsClosed(int i) // T-0759-fix1: liegt eine andere geschlossene Kontur (Rahmen und erster Punkt) in Kontur i? Budget erschöpft: ja (wie main)
        {
            var bi = boxes[i];
            for (var j = 0; j < cs.Count; j++)
            {
                if (j == i || !cs[j].Closed || dup[j]) continue;
                if (++annScan > 20_000_000) return true;
                if ((annScan & 4095) == 0) ct.ThrowIfCancellationRequested();
                var bj = boxes[j];
                if (bj.Item1 < bi.Item1 || bj.Item2 < bi.Item2 || bj.Item3 > bi.Item3 || bj.Item4 > bi.Item4) continue;
                if (bj.Item1 == bi.Item1 && bj.Item2 == bi.Item2 && bj.Item3 == bi.Item3 && bj.Item4 == bi.Item4) continue;
                if (cs[i].Contains(cs[j].Pts[0].X, cs[j].Pts[0].Y)) return true;
            }
            return false;
        }
        bool InsideClosed(int i) // liegt Kontur i (erster Punkt) in einer anderen geschlossenen Kontur? Budget erschöpft: nein (dann wie main)
        {
            var bi = boxes[i]; var q = cs[i].Pts[0];
            for (var j = 0; j < cs.Count; j++)
            {
                if (j == i || !cs[j].Closed || dup[j]) continue;
                if (++annScan > 20_000_000) return false;
                var bj = boxes[j];
                if (bi.Item1 < bj.Item1 || bi.Item2 < bj.Item2 || bi.Item3 > bj.Item3 || bi.Item4 > bj.Item4) continue;
                if (bj.Item1 == bi.Item1 && bj.Item2 == bi.Item2 && bj.Item3 == bi.Item3 && bj.Item4 == bi.Item4) continue;
                if (cs[j].Contains(q.X, q.Y)) return true;
            }
            return false;
        }
        for (var i = 0; i < cs.Count; i++)
            if (cs[i].Closed && !dup[i] && s.Mode != "auf" && SelfIntersects(cs[i].Pts, ct))
            {
                crossing[i] = true;
                warn.Add($"{Name(i)} ausgelassen: die Kontur kreuzt sich selbst (z. B. eine Acht) – dann ist nicht klar, was innen und außen ist. Bitte in der Zeichnung in einzelne geschlossene Umrisse auflösen.");
            }
            else if (cs[i].Closed && !dup[i] && s.Mode != "auf" && IsAnnotationLayer(cs[i].Layer) && ContainsClosed(i) && !InsideClosed(i))
                // T-0759-fix1: äußerste Linie, die selbst geschlossene Konturen enthält – eher eine Platte auf falscher Ebene: wie bisher (main) als Kante behandeln.
                // Liegt sie in einer anderen Kontur (Lochkreis um eine Bohrung in einer Platte), bleibt sie Hilfslinie (nicht gefräst, Inhalt ausgelassen).
                warn.Add($"{Name(i)} liegt auf der Ebene {cs[i].Layer} (Bemaßung/Mittellinien), enthält aber andere Konturen – sie wird wie eine Kante (Umriss) gefräst. Bitte prüfen, ob das stimmt.");
            else if (cs[i].Closed && !dup[i] && s.Mode != "auf" && IsAnnotationLayer(cs[i].Layer))
            {
                // T-0759: geschlossene Linie auf einer Bemaßungs-/Mittellinien-Ebene (z. B. Lochkreis) – nicht fräsen. Was darin oder daran liegt, hängt davon
                // ab, ob sie doch eine Kante ist (Loch oder Teil) – es wird wie bei einer sich kreuzenden Kontur ausgelassen.
                crossing[i] = true; annClosed[i] = true;
                warn.Add($"{Name(i)} nicht gefräst: geschlossene Linie auf der Ebene {cs[i].Layer} (Bemaßung, Mittel- oder Hilfslinien, z. B. Lochkreis).");
            }
        // T-0747-fix2: Konturen in oder an einer abgelehnten, sich selbst kreuzenden Kontur – innen/außen ist dort nicht sicher zu deuten (ein Loch würde
        // sonst als Umriss gefräst und größer). Auslassen und melden.
        var unsure = new bool[cs.Count];
        foreach (var i in frameUnsure) if (!dup[i] && !crossing[i] && !unsure[i]) { unsure[i] = true; warn.Add($"{Name(i)} ausgelassen: Umriss oder Kontur darin, der Platte mit Beschriftungsfeld oder Zeichnungsrahmen sein kann (siehe oben) – als Teil wäre sie außen, als Loch innen zu fräsen."); }
        var allBox = cs.Select((c, i) => { if ((i & 255) == 0) ct.ThrowIfCancellationRequested(); return c.Box; }).ToArray(); // T-0747-fix18: Rahmen einmal (vorher je Paar neu berechnet)
        for (var j = 0; j < cs.Count; j++)
        {
            // T-0747-fix4: auch ein ausgelassener offener Umriss (über seine geschlossene Annäherung: Enden verbunden) macht die Deutung darin unsicher
            var openOutline = !cs[j].Closed && s.Mode != "auf" && cs[j].Pts.Count >= 3 && Math.Abs(CncCam.SignedArea(cs[j].Pts)) > 1e-6;
            if (!crossing[j] && !openOutline) continue;
            var bj = allBox[j];
            for (var i = 0; i < cs.Count; i++)
            {
                if (i == j || crossing[i] || unsure[i] || dup[i] || !cs[i].Closed && !openOutline) continue;
                ct.ThrowIfCancellationRequested();
                var bi = allBox[i]; if (bi.MaxX < bj.MinX || bi.MinX > bj.MaxX || bi.MaxY < bj.MinY || bi.MinY > bj.MaxY) continue;
                var pr = cs[i].Pts[0]; // T-0747-fix3: auch doppelt umlaufene Bereiche (Windungszahl ≠ 0, z. B. Limaçon) zählen als „in der Kontur“
                if (!cs[j].Contains(pr.X, pr.Y) && Winding(cs[j].Pts, pr) == 0 && !Touches(cs[i].Pts, cs[i].Closed, cs[j].Pts, ct)) continue;
                if (!cs[i].Closed) continue; // offene Konturen werden ohnehin nur als Gravur gefräst
                unsure[i] = true;
                warn.Add(annClosed[j]
                    ? $"{Name(i)} ausgelassen: liegt in oder an Kontur {j + 1} auf der Ebene {cs[j].Layer} (Bemaßung/Mittellinie) – wäre sie doch eine Kante, wäre diese Kontur Loch statt Teil oder umgekehrt; das lässt sich nicht sicher sagen. Bitte die Linie auf eine andere Ebene legen oder löschen."
                    : crossing[j]
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
        long fileWork = 0; var FileMax = ProtectFileBudget ?? 150_000_000; // T-0747-fix18: Gesamtgrenze je Datei für Schutzbereich und Teil b (sichere Folge bei Erschöpfung)
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
                    if (!exhausted) { var w0 = w; Check(tag, idx); fileWork += w - w0 + 1; if (w > MaxW || fileWork > FileMax) exhausted = true; }
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

        // T-0759: Verbünde offener Linien (auch über Kreuzungen, Berührungen mitten auf einer Linie und über Ebenen hinweg) und die Bereiche, die sie mit
        // geschlossenen Konturen einschließen. Ein Verbund ohne Kreis (Baum, z. B. Mittelkreuz, Bemaßung mit Pfeilen) kann für sich keinen Umriss
        // bilden; was er mit geschlossenen Konturen einschließt, findet Enclosures. Reicht das Arbeitsbudget nicht, bleibt alles wie bisher.
        var tolE = ChainTol + 1e-9;
        SegGrid? cgShared = null;
        SegGrid ClosedGrid() => cgShared ??= BuildGrid(Enumerable.Range(0, cs.Count).Where(i => cs[i].Closed && !dup[i] && !crossing[i]).Select(i => ((IReadOnlyList<(double X, double Y)>)cs[i].Pts, true, i)), Math.Max(r, tolE), ct);
        var rowIdxE = new Dictionary<int, RowIndex>();
        bool InCE(int i, (double X, double Y) q) { if (!rowIdxE.TryGetValue(i, out var ri)) rowIdxE[i] = ri = new RowIndex(cs[i].Pts); return ri.Contains(q.X, q.Y); }
        OpenNet? net = null; List<(List<int> Contours, List<int> Structs)>? encl = null;
        if (s.Mode != "auf" && cs.Any(c => !c.Closed && c.Pts.Count >= 2))
        {
            net = AnalyzeOpen(cs, j => !cs[j].Closed && cs[j].Pts.Count >= 2, tolE, 30_000_000, ct);
            if (!net.Exhausted) encl = Enclosures(cs, net, ClosedGrid(), i => cs[i].Closed && !dup[i] && !crossing[i], InCE, i => allBox[i], tolE, 60_000_000, ct);
            if (net.Exhausted || encl is null) warn.Add("Sehr viele Linien berühren oder kreuzen sich – der Prüfaufwand für Linienverbünde ist zu groß; sie werden wie bisher vorsichtig behandelt (Konturen in Linienverbünden mit Stoß werden ausgelassen).");
        }
        bool Analysed() => net is { Exhausted: false } && encl is not null;
        // T-0759: Ein Linienverbund ohne Kreis (Baum) schließt für sich keinen Bereich ein – mit einer gedachten Schließsehne über eine Lücke aber schon
        // (wie bei der Regel für offene Umrisse; z. B. „[“ und „]“ mit 0,2 mm Spalt). Ausgenommen werden deshalb nur Bäume, deren Linien alle auf einer
        // sicher erkannten Bemaßungs-/Mittellinien-Ebene liegen (keine Kanten) und in deren konvexer Hülle keine geschlossene Kontur ganz liegt.
        long annWork = 0; Dictionary<(long, long), List<(int J, (double X, double Y) P)>>? openEnds = null;
        bool AnnotationTreeFree(List<int> members, List<(double X, double Y)> pts)
        {
            if (!members.All(j => IsAnnotationLayer(cs[j].Layer))) return false;
            // T-0759-fix1: liegen zwei freie Enden des Baums dicht beieinander (≤ max(2 mm, Fräser-Ø)), ist er eher ein Umriss mit kleinen Spalten
            // (Prüferfall a5: Quadrat mit 0,3-mm-Spalten auf BEM) als Bemaßung/Mittelkreuz – dann vorsichtig schützen wie bisher.
            // Kurze Linien (≤ 3 × diese Grenze, z. B. Pfeilspitzen) zählen dabei nicht – ihre Enden liegen naturgemäß dicht beieinander.
            var gapMax = Math.Max(2.0, s.ToolD); var ends = new List<(double X, double Y)>();
            bool Short(int j) => Length(cs[j].Pts, false) <= 3 * gapMax;
            foreach (var j in members)
                foreach (var e in Short(j) ? [] : new[] { cs[j].Pts[0], cs[j].Pts[^1] })
                {
                    var touches = false;
                    foreach (var o in members)
                    {
                        if (o == j) continue;
                        var op = cs[o].Pts;
                        for (var k = 0; k + 1 < op.Count && !touches; k++) { if (++annWork > 50_000_000) return false; if (SegDist(e, op[k], op[k + 1]) <= tolE) touches = true; }
                        if (touches) break;
                    }
                    if (!touches) ends.Add(e);
                }
            for (var a = 0; a < ends.Count; a++)
                for (var b = a + 1; b < ends.Count; b++)
                {
                    if (++annWork > 50_000_000) return false;
                    if (D2(ends[a], ends[b]) > 1e-18 && D2(ends[a], ends[b]) <= gapMax * gapMax) return false;
                }
            // … auch zu Enden anderer offener Linien (ein Umriss mit Spalt kann aus mehreren Verbünden bestehen)
            if (openEnds is null)
            {
                openEnds = [];
                for (var j = 0; j < cs.Count; j++)
                    if (!cs[j].Closed && cs[j].Pts.Count >= 2)
                        foreach (var e in new[] { cs[j].Pts[0], cs[j].Pts[^1] })
                        {
                            var key = ((long)Math.Floor(e.X / gapMax), (long)Math.Floor(e.Y / gapMax));
                            if (!openEnds.TryGetValue(key, out var l)) openEnds[key] = l = []; l.Add((j, e));
                        }
            }
            var mem = members.ToHashSet();
            foreach (var e in ends)
            {
                long kx = (long)Math.Floor(e.X / gapMax), ky = (long)Math.Floor(e.Y / gapMax);
                for (var dx = -1L; dx <= 1; dx++)
                    for (var dy = -1L; dy <= 1; dy++)
                        if (openEnds.TryGetValue((kx + dx, ky + dy), out var l))
                            foreach (var (j, q) in l)
                            {
                                if (++annWork > 50_000_000) return false;
                                if (!mem.Contains(j) && !Short(j) && D2(e, q) <= gapMax * gapMax) return false;
                            }
            }
            var h = Hull(pts); if (h.Count < 3) return true;
            double hx0 = h.Min(p => p.X), hy0 = h.Min(p => p.Y), hx1 = h.Max(p => p.X), hy1 = h.Max(p => p.Y);
            for (var i = 0; i < cs.Count; i++)
            {
                if (!cs[i].Closed || dup[i]) continue;
                if (++annWork > 50_000_000) return false; // Prüfaufwand zu groß: vorsichtig schützen
                if ((annWork & 4095) == 0) ct.ThrowIfCancellationRequested();
                var b = boxes[i];
                if (b.Item1 < hx0 || b.Item2 < hy0 || b.Item3 > hx1 || b.Item4 > hy1) continue;
                var all = true;
                foreach (var v in cs[i].Pts) { if (++annWork > 50_000_000) return false; if (!InConvex(h, v, 1e-6)) { all = false; break; } }
                if (all) return false;
            }
            return true;
        }
        // a) Linienverbund mit Stoß (Gruppe samt über Spalte angeschlossener Ketten)
        var jgroups = cs.Select((c, j) => (c, j)).Where(t => t.c.JunctionGroup >= 0).GroupBy(t => t.c.JunctionGroup).ToList();
        if (jgroups.Count > 0)
        {
            warn.Add($"{jgroups.Count} Linienverbund/-verbünde (zusammen {jgroups.Sum(g => g.Count())} Linien, z. B. ab Kontur {jgroups[0].First().j + 1}) nicht zu Umrissen verkettet: an einem Punkt stoßen mehr als zwei Linien zusammen oder die Enden liegen nicht eindeutig. Diese Linien werden nicht gefräst (außer als Gravur), Konturen darin werden ausgelassen{(Analysed() ? ", wenn der Verbund einen Bereich einschließen kann" : "")}.");
            var doneComp = new HashSet<int>();
            foreach (var grp in jgroups)
            {
                ct.ThrowIfCancellationRequested();
                var first = grp.First().j;
                var pts = new List<(double X, double Y)>();
                if (Analysed())
                {
                    var comp = net!.Comp[first];
                    if (!doneComp.Add(comp)) continue; // Verbund schon geschützt
                    foreach (var j in net.Members[comp]) pts.AddRange(cs[j].Pts);
                    if (!net.Cyclic[comp] && AnnotationTreeFree(net.Members[comp], pts)) continue; // T-0759: Bemaßung/Mittellinie als Baum – keine Kante
                }
                else foreach (var (c, _) in grp) pts.AddRange(c.Pts);
                Protect(pts, i => SkipAs(i, $"liegt im Linienverbund ab Kontur {first + 1} (oder teilweise darin), an dem mehr als zwei Linien zusammenstoßen – welcher Umriss gemeint ist und ob sie Loch oder Teil ist, lässt sich nicht sicher sagen. Bitte die Umrisse in der Zeichnung trennen oder als Polylinie zeichnen."), i => SkipAs(i, Budget));
            }
        }
        // T-0759: Verbund ohne Stoß, der für sich einen Bereich einschließt (sich kreuzende oder mitten auf einer Linie endende Linien, z. B. „#“)
        if (Analysed())
            for (var comp = 0; comp < net!.Members.Count; comp++)
            {
                if (!net.Cyclic[comp] || net.Members[comp].Any(j => cs[j].JunctionGroup >= 0)) continue;
                ct.ThrowIfCancellationRequested();
                var pts = new List<(double X, double Y)>(); foreach (var j in net.Members[comp]) pts.AddRange(cs[j].Pts);
                var first = net.Members[comp].Min();
                // Was die Linien des Verbunds allein einschließen, liegt in ihrer konvexen Hülle. Eine geschlossene Kontur, die die Hülle (samt
                // Kettentoleranz) weder berührt noch schneidet, liegt in keinem solchen Bereich – ihre Lesart (Loch oder Teil) hängt nicht von diesem
                // Verbund ab (sonst würde schon der Rahmen der Hülle ausreichen, um z. B. Löcher neben dichten Gravurlinien auszulassen). Bereiche aus
                // Verbund und geschlossenen Konturen prüft c) getrennt. Reicht das Budget nicht, wird nicht verschont (vorsichtig).
                var hullV = Hull(pts); long spareWork = 0;
                bool OutsideHull(int i)
                {
                    if (hullV.Count < 3 || !cs[i].Closed) return false;
                    var pp = cs[i].Pts; var hn = hullV.Count;
                    foreach (var v in pp) { Tick(); if ((spareWork += 8) > 20_000_000 || InConvexLog(hullV, v, -tolE)) return false; }
                    for (var k = 0; k < pp.Count; k++)
                    {
                        var a0 = pp[k]; var b0 = pp[(k + 1) % pp.Count];
                        if ((spareWork += hn) > 20_000_000) return false;
                        for (var t = 0; t < hn; t++) { Tick(); if (SegSeg(a0, b0, hullV[t], hullV[(t + 1) % hn]) <= tolE) return false; }
                    }
                    return true;
                }
                Protect(pts, i => SkipAs(i, $"liegt in einem Bereich, den die offenen Linien ab Kontur {first + 1} einschließen (sie kreuzen oder berühren sich) – welcher Umriss gemeint ist und ob sie Loch oder Teil ist, lässt sich nicht sicher sagen. Bitte den Umriss in der Zeichnung als geschlossene Polylinie zeichnen."), i => SkipAs(i, Budget), OutsideHull);
            }

        // b) T-0747-fix17/18: offener Linienzug (oder Stoß-Verbund), der mit mindestens zwei Enden (≤ 0,1 mm, gleiche Ebene) an dieselbe geschlossene Kontur C
        // stößt und teilweise außerhalb von C liegt, schließt zusammen mit dem Rand von C einen Bereich ein (U vor einer Kerbe, auch eine einzelne gerade
        // Linie über die Kerbe). Ob dort Loch oder Teil gemeint ist und ob C so gemeint ist, lässt sich nicht sicher sagen: C und alles in der Hülle aus
        // Linienzug ∪ C, das nicht ganz in C liegt, auslassen. Liegt der Linienzug ganz in C (Gravur von Rand zu Rand), entsteht kein neuer Bereich.
        // T-0747-fix18: „teilweise außerhalb“ wird je Strecke geprüft – die Strecke wird an ihren Schnitten mit dem Rand von C geteilt, und von jedem
        // Teilstück wird die Mitte getestet (jedes Teilstück liegt ganz innen, ganz außen oder auf dem Rand). Alle Linienzüge an derselben C werden
        // gemeinsam behandelt: die Kandidaten (Konturen nicht ganz in C) werden je C einmal bestimmt.
        if (s.Mode != "auf")
        {
            var comps = cs.Select((c, j) => (c, j)).Where(t => !t.c.Closed && t.c.Pts.Count >= 2).GroupBy(t => t.c.JunctionGroup >= 0 ? -1 - t.c.JunctionGroup : t.j).ToList();
            if (comps.Count > 0)
            {
                var closedOk = Enumerable.Range(0, cs.Count).Where(i => cs[i].Closed && !dup[i] && !crossing[i]).ToArray();
                var tolT = ChainTol + 1e-9;
                var cg = ClosedGrid(); // T-0759: dasselbe Raster wie für die Linienverbünde (gleiche Konturen, gleiche Zellgröße)
                var rowIdx = new Dictionary<int, RowIndex>();
                RowIndex Ri(int i) { if (!rowIdx.TryGetValue(i, out var ri)) rowIdx[i] = ri = new RowIndex(cs[i].Pts); return ri; }
                bool NearC(int cTag, (double X, double Y) v, double d) =>
                    cg.Any(v.X - d, v.Y - d, v.X + d, v.Y + d, t => cg.Segs[t].Tag == cTag && SegDist(v, cg.Segs[t].A, cg.Segs[t].B) <= d);
                bool InC(int cTag, (double X, double Y) v)
                {
                    var bC = boxes[cTag];
                    return v.X >= bC.Item1 && v.X <= bC.Item3 && v.Y >= bC.Item2 && v.Y <= bC.Item4 && Ri(cTag).Contains(v.X, v.Y);
                }
                // T-0747-fix18/19/20: Liegt ein Teil der Strecke a→b außerhalb von C, weiter als tol vom Rand? Genaue Prüfung (T-0747-fix20):
                // Für jede nahe Randstrecke e von C ist die Menge der Punkte der Strecke mit Abstand ≤ tol zu e ein Intervall (Schnitt der Geraden mit der
                // Kapsel um e: Rechteck + zwei Kreise, konvex). Die Vereinigung dieser Intervalle ist der Teil „am Rand“. Jede Lücke dazwischen liegt
                // überall weiter als tol vom Rand und kann die Seite nicht wechseln – ihre Mitte entscheidet: nicht in C → außen. Genau tol, keine Abtastung;
                // kollinear auf dem Rand liegende Strecken haben keine Lücke. Nahe Randstrecken werden in Zellschritten entlang der Strecke gesucht; jede
                // besuchte Strecke zählt auf die Gesamtgrenze. Ergebnis: 0 = nicht außen, 1 = außen, 2 = unbekannt (Grenze erreicht).
                int SegOutside(int cTag, (double X, double Y) a, (double X, double Y) b)
                {
                    double dx = b.X - a.X, dy = b.Y - a.Y, len2 = dx * dx + dy * dy, R = tolT;
                    if (len2 < 1e-24) return InC(cTag, a) || NearC(cTag, a, R) ? 0 : 1;
                    var len = Math.Sqrt(len2); var iv = new List<(double Lo, double Hi)>(); var seen = new HashSet<int>();
                    var cell = cg.CellSize; var nq = (long)Math.Ceiling(len / cell) + 1;
                    if (nq > 4_000_000) return 2;
                    var unknown = false;
                    // Intervall der Parameter t ∈ [0,1] mit Abstand ≤ R zur Strecke c0→c1
                    void Capsule((double X, double Y) c0, (double X, double Y) c1)
                    {
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
                            // u(t) = ((a + t d − c0)·w)/|w|² ∈ [0,1],  v(t) = cross(w, a + t d − c0)/|w| ∈ [−R, R]
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
                        if (lo <= hi) iv.Add((lo, hi));
                    }
                    for (long k = 0; k <= nq && !unknown; k++)
                    {
                        Tick(); fileWork++; if (fileWork > FileMax) { unknown = true; break; }
                        double px = a.X + dx * k / nq, py = a.Y + dy * k / nq, hw = cell / 2 + R;
                        cg.Any(px - hw, py - hw, px + hw, py + hw, t =>
                        {
                            fileWork++; if (fileWork > FileMax) { unknown = true; return true; }
                            var sg = cg.Segs[t]; if (sg.Tag != cTag || !seen.Add(t)) return false;
                            Capsule(sg.A, sg.B);
                            return false;
                        });
                    }
                    if (unknown) return 2;
                    iv.Sort((x, y) => x.Lo.CompareTo(y.Lo));
                    var cur = 0.0; // bis hierhin am Rand abgedeckt
                    foreach (var (lo, hi) in iv)
                    {
                        if (lo > cur + 1e-12)
                        {
                            var m = (cur + lo) / 2; // Lücke (cur, lo): überall weiter als tol vom Rand
                            if (!InC(cTag, (a.X + dx * m, a.Y + dy * m))) return 1;
                        }
                        cur = Math.Max(cur, hi);
                        if (cur >= 1) return 0;
                    }
                    if (cur < 1 - 1e-12) { var m = (cur + 1) / 2; if (!InC(cTag, (a.X + dx * m, a.Y + dy * m))) return 1; }
                    return 0;
                }
                var qual = new Dictionary<int, List<(int First, List<(double X, double Y)> Pts, int Touch, bool Unknown)>>();
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
                        var res = 0; // 0 nicht außen, 1 außen, 2 unbekannt (Prüfaufwand zu groß – sichere Folge)
                        foreach (var (c, _) in comp)
                        {
                            for (var k = 0; k + 1 < c.Pts.Count && res != 1; k++) { Tick(); res = Math.Max(res, SegOutside(cTag, c.Pts[k], c.Pts[k + 1])); }
                            if (res == 1) break;
                        }
                        if (res == 0) continue;
                        var pts = new List<(double X, double Y)>(); foreach (var (c, _) in comp) pts.AddRange(c.Pts);
                        if (!qual.TryGetValue(cTag, out var ql)) qual[cTag] = ql = [];
                        ql.Add((comp.First().j, pts, nTouch, res == 2));
                    }
                }
                EnsureIndex();
                foreach (var (cTag, ql) in qual)
                {
                    ct.ThrowIfCancellationRequested();
                    var cc = cs[cTag]; var hc = Hull(cc.Pts);
                    string Why((int First, List<(double X, double Y)> Pts, int Touch, bool Unknown) u) => u.Unknown ? Budget : $"Linienzug ab Kontur {u.First + 1} stößt mit {u.Touch} Enden an Kontur {cTag + 1} und schließt mit deren Rand einen Bereich ein – welcher Umriss gemeint ist und ob die Kontur Loch oder Teil ist, lässt sich nicht sicher sagen. Bitte den Umriss in der Zeichnung schließen oder die Linien auf eine eigene Ebene legen.";
                    SkipAs(cTag, Why(ql.FirstOrDefault(x => !x.Unknown) is { Pts: not null } q0 ? q0 : ql[0])); // T-0747-fix20: zutreffender Grund zuerst
                    // Kandidaten je C einmal: offene Konturen, deren Rahmen den gemeinsamen Rahmen überlappt und die nicht ganz in C liegen
                    double rx0 = boxes[cTag].Item1, ry0 = boxes[cTag].Item2, rx1 = boxes[cTag].Item3, ry1 = boxes[cTag].Item4;
                    foreach (var u in ql) foreach (var p in u.Pts) { rx0 = Math.Min(rx0, p.X); ry0 = Math.Min(ry0, p.Y); rx1 = Math.Max(rx1, p.X); ry1 = Math.Max(ry1, p.Y); }
                    SegGrid? ccGrid = null; var candB = new List<int>(); var budgetOut = false;
                    bool InsideC(int i)
                    {
                        var pp = cs[i].Pts; var step = Math.Max(1, pp.Count / 64);
                        for (var k = 0; k < pp.Count; k += step) { fileWork++; if (!Ri(cTag).Contains(pp[k].X, pp[k].Y)) return false; }
                        ccGrid ??= BuildGrid([((IReadOnlyList<(double X, double Y)>)cc.Pts, true, 0)], AvgLen(cc.Pts, true) * 2, ct);
                        for (var k = 0; k < pp.Count; k++)
                        {
                            var a0 = pp[k]; var b0 = pp[(k + 1) % pp.Count]; fileWork++;
                            if (ccGrid.Any(Math.Min(a0.X, b0.X) - 1e-6, Math.Min(a0.Y, b0.Y) - 1e-6, Math.Max(a0.X, b0.X) + 1e-6, Math.Max(a0.Y, b0.Y) + 1e-6, t => SegSeg(a0, b0, ccGrid.Segs[t].A, ccGrid.Segs[t].B) <= 1e-6)) return false;
                        }
                        return true;
                    }
                    for (var q = NextA(Lower(minXs!, rx0 - maxW - 1e-6)); q < byMinX!.Length && minXs![q] <= rx1 + 1e-6; q = NextA(q + 1))
                    {
                        Tick(); var i = byMinX[q]; var bi = boxes[i]; fileWork++;
                        if (bi.Item3 < rx0 - 1e-6 || bi.Item2 > ry1 + 1e-6 || bi.Item4 < ry0 - 1e-6) continue;
                        if (fileWork > FileMax) { budgetOut = true; break; }
                        if (!InsideC(i)) candB.Add(i);
                    }
                    if (budgetOut)
                    {
                        for (var q = NextA(Lower(minXs!, rx0 - maxW - 1e-6)); q < byMinX!.Length && minXs![q] <= rx1 + 1e-6; q = NextA(q + 1))
                        {
                            Tick(); var i = byMinX[q]; var bi = boxes[i];
                            if (bi.Item3 >= rx0 - 1e-6 && bi.Item2 <= ry1 + 1e-6 && bi.Item4 >= ry0 - 1e-6) SkipAs(i, Budget);
                        }
                        continue;
                    }
                    // je Linienzug: Regeln 1–3 nur gegen die Kandidaten
                    foreach (var u in ql)
                    {
                        ct.ThrowIfCancellationRequested();
                        var pts = new List<(double X, double Y)>(hc); pts.AddRange(u.Pts);
                        var hull = Hull(pts); var lg = 1 + (long)Math.Log2(Math.Max(2, hull.Count)); const double e = 1e-6;
                        double gx0 = pts.Min(p => p.X), gy0 = pts.Min(p => p.Y), gx1 = pts.Max(p => p.X), gy1 = pts.Max(p => p.Y);
                        var why = Why(u);
                        foreach (var i in candB)
                        {
                            if (!Open(i)) continue;
                            var bi = boxes[i];
                            if (bi.Item1 > gx1 + e || bi.Item3 < gx0 - e || bi.Item2 > gy1 + e || bi.Item4 < gy0 - e) continue;
                            if (bi.Item1 >= gx0 - e && bi.Item2 >= gy0 - e && bi.Item3 <= gx1 + e && bi.Item4 <= gy1 + e) { SkipAs(i, why); continue; } // 1.
                            var pp = cs[i].Pts; var hitIn = false; var reason = why; // T-0747-fix19: Grund je Kandidat
                            fileWork += lg; if (hull.Count >= 3 && InConvexLog(hull, pp[0], -e)) hitIn = true; // 2.
                            for (var k = 1; k < pp.Count && !hitIn; k++)
                            {
                                Tick(); var v = pp[k];
                                if (v.X <= gx0 + e || v.X >= gx1 - e || v.Y <= gy0 + e || v.Y >= gy1 - e) continue;
                                fileWork += lg; if (hull.Count >= 3 && InConvexLog(hull, v, e)) hitIn = true; // 3.
                                if (fileWork > FileMax) { hitIn = true; reason = Budget; } // sichere Folge
                            }
                            if (hitIn) SkipAs(i, reason);
                        }
                    }
                }
            }
        }
        // c) T-0759: Bereiche, die Linienverbünde mit geschlossenen Konturen einschließen, auch wenn kein einzelner Linienzug zwei Enden an derselben
        // Kontur hat – zwei Linien zwischen zwei Platten (Brücke), U aus sich kreuzenden Überständen, Sehne auf einer anderen Ebene. Ausgelassen werden
        // die beteiligten Konturen und alles in der Hülle aus Konturen ∪ Linien, das nicht ganz in einer der beteiligten Konturen liegt (dort bleibt die
        // Tiefe in jeder Lesart gleich).
        if (encl is { Count: > 0 })
        {
            EnsureIndex();
            var gridC = new Dictionary<int, SegGrid>();
            bool InsideOf(int i, int c)
            {
                var bc = boxes[c]; var bi = boxes[i];
                if (bi.Item1 < bc.Item1 || bi.Item2 < bc.Item2 || bi.Item3 > bc.Item3 || bi.Item4 > bc.Item4) return false;
                var pp = cs[i].Pts; var step = Math.Max(1, pp.Count / 64);
                for (var k = 0; k < pp.Count; k += step) { Tick(); if (!InCE(c, pp[k])) return false; }
                if (!gridC.TryGetValue(c, out var gc)) gridC[c] = gc = BuildGrid([((IReadOnlyList<(double X, double Y)>)cs[c].Pts, true, 0)], AvgLen(cs[c].Pts, true) * 2, ct);
                for (var k = 0; k < pp.Count; k++)
                {
                    Tick(); var a0 = pp[k]; var b0 = pp[(k + 1) % pp.Count];
                    if (gc.Any(Math.Min(a0.X, b0.X) - 1e-6, Math.Min(a0.Y, b0.Y) - 1e-6, Math.Max(a0.X, b0.X) + 1e-6, Math.Max(a0.Y, b0.Y) + 1e-6, t => SegSeg(a0, b0, gc.Segs[t].A, gc.Segs[t].B) <= 1e-6)) return false;
                }
                return true;
            }
            // reicht Linie j in ein Loch c hinein? Jede Strecke wird an den Schnittstellen mit dem Rand von c geteilt; liegt die Mitte eines Stücks, das länger
            // als 2 × Kettentoleranz ist, in c, reicht sie hinein. Eine Linie, die nur am Rand endet oder ihn berührt, reicht nicht hinein.
            var holeOf = new Dictionary<int, bool>();
            bool IsHole(int c) // liegt c in einer ungeraden Zahl anderer geschlossener Konturen (Loch)? Budget erschöpft: ja (vorsichtig)
            {
                if (holeOf.TryGetValue(c, out var h)) return h;
                var q0 = cs[c].Pts[0]; var n = 0; long w = 0;
                for (var i = 0; i < cs.Count; i++)
                {
                    if (i == c || !cs[i].Closed || dup[i]) continue;
                    if (++w > 5_000_000) return holeOf[c] = true;
                    var bi = boxes[i]; if (q0.X < bi.Item1 || q0.X > bi.Item3 || q0.Y < bi.Item2 || q0.Y > bi.Item4) continue;
                    if (InCE(i, q0)) n++;
                }
                return holeOf[c] = n % 2 == 1;
            }
            bool LineEnters(int j, int c)
            {
                if (!IsHole(c)) return false; // in eine Platte hineinreichende Linien sind Gravur (T-0747) – die Tiefe darin bleibt
                var bc = boxes[c]; var pp = cs[j].Pts;
                if (!gridC.TryGetValue(c, out var gc)) gridC[c] = gc = BuildGrid([((IReadOnlyList<(double X, double Y)>)cs[c].Pts, true, 0)], AvgLen(cs[c].Pts, true) * 2, ct);
                var ts = new List<double>();
                for (var k = 0; k + 1 < pp.Count; k++)
                {
                    Tick(); var a0 = pp[k]; var b0 = pp[k + 1];
                    if (Math.Max(a0.X, b0.X) < bc.Item1 || Math.Min(a0.X, b0.X) > bc.Item3 || Math.Max(a0.Y, b0.Y) < bc.Item2 || Math.Min(a0.Y, b0.Y) > bc.Item4) continue;
                    double dx = b0.X - a0.X, dy = b0.Y - a0.Y, len = Math.Sqrt(dx * dx + dy * dy); if (len < 1e-12) continue;
                    ts.Clear(); ts.Add(0); ts.Add(1);
                    gc.Any(Math.Min(a0.X, b0.X) - 1e-6, Math.Min(a0.Y, b0.Y) - 1e-6, Math.Max(a0.X, b0.X) + 1e-6, Math.Max(a0.Y, b0.Y) + 1e-6, t =>
                    {
                        var (c0, c1) = (gc.Segs[t].A, gc.Segs[t].B); double sx = c1.X - c0.X, sy = c1.Y - c0.Y, den = dx * sy - dy * sx;
                        if (Math.Abs(den) < 1e-18) return false;
                        double qx = c0.X - a0.X, qy = c0.Y - a0.Y, tt = (qx * sy - qy * sx) / den, uu = (qx * dy - qy * dx) / den;
                        if (tt >= -1e-9 && tt <= 1 + 1e-9 && uu >= -1e-9 && uu <= 1 + 1e-9) ts.Add(Math.Clamp(tt, 0, 1));
                        return false;
                    });
                    ts.Sort();
                    for (var q = 0; q + 1 < ts.Count; q++)
                    {
                        if ((ts[q + 1] - ts[q]) * len <= 2 * tolE) continue;
                        var tm = (ts[q] + ts[q + 1]) / 2; var m = (a0.X + dx * tm, a0.Y + dy * tm);
                        if (InCE(c, m)) return true;
                    }
                }
                return false;
            }
            foreach (var (gcs, gss) in encl)
            {
                ct.ThrowIfCancellationRequested();
                var first = gss.SelectMany(st => net!.Members[st]).Min();
                var why = $"die offenen Linien ab Kontur {first + 1} schließen zusammen mit Kontur {string.Join(", ", gcs.Take(6).Select(c => c + 1))}{(gcs.Count > 6 ? " …" : "")} einen Bereich ein (auch über Kreuzungen oder andere Ebenen) – welcher Umriss gemeint ist und ob die Kontur Loch oder Teil ist, lässt sich nicht sicher sagen. Bitte den Umriss in der Zeichnung schließen oder die Linien löschen.";
                foreach (var c in gcs) SkipAs(c, why);
                var pts = new List<(double X, double Y)>();
                foreach (var c in gcs) pts.AddRange(cs[c].Pts);
                foreach (var st in gss) foreach (var j in net!.Members[st]) pts.AddRange(cs[j].Pts);
                var hull = Hull(pts); const double e = 1e-6;
                // T-0759-fix1: „ganz in einer beteiligten Kontur c“ lässt die Tiefe nur gleich, wenn keine Linie des Verbunds in c hineinreicht (sonst kann
                // der Bereich in c liegen – z. B. zwei Linien quer über ein Loch mit Insel: Steg, die Insel wäre ein Loch darin)
                var spareIn = gcs.Where(c => !gss.Any(st => net!.Members[st].Any(j => LineEnters(j, c)))).ToList();
                double gx0 = pts.Min(p => p.X), gy0 = pts.Min(p => p.Y), gx1 = pts.Max(p => p.X), gy1 = pts.Max(p => p.Y);
                for (var q = NextA(Lower(minXs!, gx0 - maxW - e)); q < byMinX!.Length && minXs![q] <= gx1 + e; q = NextA(q + 1))
                {
                    Tick(); var i = byMinX[q]; var bi = boxes[i];
                    if (bi.Item3 < gx0 - e || bi.Item2 > gy1 + e || bi.Item4 < gy0 - e) continue;
                    if (spareIn.Any(c => InsideOf(i, c))) continue; // ganz in einer beteiligten Kontur, in die keine Linie hineinreicht: Tiefe bleibt
                    if (bi.Item1 >= gx0 - e && bi.Item2 >= gy0 - e && bi.Item3 <= gx1 + e && bi.Item4 <= gy1 + e) { SkipAs(i, why); continue; } // 1.
                    var pp = cs[i].Pts; var hitIn = hull.Count >= 3 && InConvexLog(hull, pp[0], -e); // 2.
                    for (var k = 1; k < pp.Count && !hitIn; k++)
                    {
                        Tick(); var v = pp[k];
                        if (v.X <= gx0 + e || v.X >= gx1 - e || v.Y <= gy0 + e || v.Y >= gy1 - e) continue;
                        if (hull.Count >= 3 && InConvexLog(hull, v, e)) hitIn = true; // 3.
                    }
                    if (hitIn) SkipAs(i, why);
                }
            }
        }
        // Art je Kontur (T-0747-fix5: Kandidaten für „liegt in“ aus einem Raster der Rahmen statt aus allen Konturen)
        var closedBoxes = boxes.Where((_, j) => cs[j].Closed).ToList();
        var spanAll = closedBoxes.Count == 0 ? 1 : Math.Max(closedBoxes.Max(b => b.Item3) - closedBoxes.Min(b => b.Item1), closedBoxes.Max(b => b.Item4) - closedBoxes.Min(b => b.Item2));
        // T-0758: Gezählt werden nur Rahmen, die den Prüfpunkt enthalten – die findet jedes Raster; das feinere (RefGrid) liefert nur weniger
        // Unbeteiligte (30 000 winzige Teile in wenigen groben Zellen: vorher je Teil alle 30 000 Rahmen)
        var frames = new RefGrid(Enumerable.Range(0, cs.Count).Where(j => cs[j].Closed && !crossing[j] && !dup[j])
            .Select(j => ((IReadOnlyList<(double X, double Y)>)new[] { (boxes[j].Item1, boxes[j].Item2), (boxes[j].Item3, boxes[j].Item4) }, false, j)),
            Math.Max(Math.Max(1e-3, spanAll / 1024), closedBoxes.Count == 0 ? 1 : closedBoxes.Average(b => Math.Max(b.Item3 - b.Item1, b.Item4 - b.Item2))), ct, 1e-12);
        var kinds = new string[cs.Count]; var depth = new int[cs.Count];
        // T-0758: große Konturen, in denen viele Rahmen liegen (riesige Platte neben 30 000 kleinen Teilen): Punkt-in-Kontur über waagrechte Streifen –
        // gleiche Rechnung je Strecke wie CadContour.Contains, nur ohne die Strecken, die die Höhe des Punkts nicht überdecken
        var strips = new Dictionary<int, StripIndex>();
        bool InContour(int j, double x, double y)
        {
            if (cs[j].Pts.Count < 64) return cs[j].Contains(x, y);
            if (!strips.TryGetValue(j, out var si)) strips[j] = si = new StripIndex(cs[j].Pts);
            return si.Contains(x, y);
        }
        for (var i = 0; i < cs.Count; i++)
        {
            if (!cs[i].Closed) { kinds[i] = "auf"; continue; }
            var probe = cs[i].Pts[0];
            ct.ThrowIfCancellationRequested(); // T-0758: je Kontur (Punkt-in-Kontur kann bei großen Konturen dauern)
            depth[i] = frames.Near(probe.X, probe.Y, probe.X, probe.Y, 0, _ => true).Select(e => frames.Segs[e].Tag).Distinct()
                .Count(j => j != i && boxes[j].Item1 <= probe.X && probe.X <= boxes[j].Item3 && boxes[j].Item2 <= probe.Y && probe.Y <= boxes[j].Item4 && areas[j] > areas[i] && InContour(j, probe.X, probe.Y));
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
            var (simp, dev) = Simplify(c.Pts, Math.Min(SimplifyTol, r / 20), ct);
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
        // T-0759: offene Linien auf Bemaßungs-/Mittellinien-Ebenen sind kein Hindernis (keine Kante); was sie einschließen, ist oben schon ausgelassen
        var obstacles = new RefGrid(cs.Select((c, i) => ((IReadOnlyList<(double X, double Y)>)c.Pts, c.Closed, i)).Where(t => !dup[t.i] && (t.Closed || !IsAnnotationLayer(cs[t.i].Layer))), r, ct, r); // Doppel nur einmal; T-0758: feineres Raster, gleiche Treffer (Abfragen reichen den Radius über die Bahn hinaus)
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
                ct.ThrowIfCancellationRequested(); // T-0758
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
        // T-0758: der Text je Bahn hängt nur von der Bahn ab – in Blöcken auf allen Kernen schreiben, dann in Reihenfolge anhängen (Text, Hinweise und
        // der erste Fehler wie vorher der Reihe nach; vorher 18 s für 310 MB G-Code)
        void Path(CamPath p, StringBuilder g, List<string> warn)
        {
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
                var lifts = Loop(g, pts, p.Closed, z, useTabs ? tabs : [], zTab, s, ct);
                if (useTabs && lifts != tabs.Count) throw new InvalidOperationException($"Haltestege an Kontur {p.Source + 1} ließen sich nicht sicher setzen ({lifts} statt {tabs.Count} Anhebungen) – bitte Anzahl oder Breite der Stege ändern."); // T-0747-fix3
                if (!p.Closed) forward = !forward;
            }
            g.Append($"G0 Z{I(s.Safe)}\n");
        }
        for (var b0 = 0; b0 < order.Count; b0 += 512)
        {
            var nb = Math.Min(512, order.Count - b0);
            var txt = new string?[nb]; var tw = new List<string>?[nb]; var err = new Exception?[nb];
            Par(nb, ct, k =>
            {
                var sb = new StringBuilder(); var w = new List<string>();
                try { Path(order[b0 + k], sb, w); txt[k] = sb.ToString(); } catch (Exception e) when (e is not OperationCanceledException) { err[k] = e; }
                tw[k] = w;
            });
            for (var k = 0; k < nb; k++)
            {
                n++; warn.AddRange(tw[k]!);
                if (err[k] is { } e) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e).Throw();
                if ((long)g.Length + txt[k]!.Length + 16 > MaxGcodeChars) // T-0772: sonst Absturz (Speicher) statt Meldung
                    throw new InvalidOperationException($"Der G-Code würde zu groß (über {MaxGcodeChars / 1_000_000:N0} Millionen Zeichen, rund {MaxGcodeChars / 500_000_000.0:0.#} GB im Speicher) – das kann Trendo nicht schreiben und keine Steuerung verarbeiten. Bitte die Zeichnung aufteilen und in mehreren Aufträgen fräsen.");
                g.Append(txt[k]); txt[k] = null;
            }
        }
        g.Append("M5\nM30\n");
        if (n == 0) throw new InvalidOperationException("Es blieb keine fräsbare Kontur übrig: " + string.Join(" ", SummarizeWarnings(warn))); // T-0759: zusammengefasst
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
        var sub = new Dictionary<(long, long), SubIndex>(); // T-0758: Zellen mit sehr vielen Startpunkten (30 000 winzige Teile in einer Zelle) fein unterteilt
        for (var n = 0; n < paths.Count; n++)
        {
            if ((n & 1023) == 0) ct.ThrowIfCancellationRequested();
            var here0 = at; var (cx, cy) = C(here0); var best = -1; var bd = double.MaxValue;
            void Cell(long gx, long gy)
            {
                if (!grid.TryGetValue((gx, gy), out var l)) return;
                if (l.Count > 64) { (sub.TryGetValue((gx, gy), out var si) ? si : sub[(gx, gy)] = new SubIndex(l, paths)).Nearest(here0, used, ref bd, ref best); return; } // T-0758
                foreach (var i in l) { if (used[i]) continue; var d = D2(paths[i].Pts[0], here0); if (d < bd) { bd = d; best = i; } }
            }
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

    /// <summary>T-0758: feine Unterteilung einer Rasterzelle von <see cref="OrderMany"/>. <see cref="Nearest"/> wirkt wie die Schleife über die Zelle
    /// („unbenutzt und D2 &lt; bisher → merken“, in Nummernfolge): gesucht wird das kleinste (D2, Nummer); Teilzellen, deren Rahmen schon weiter weg ist als
    /// das bisher Beste, werden übersprungen (der Abstand zum Rahmen ist in Gleitkomma nie größer als der zu einem Punkt darin).</summary>
    sealed class SubIndex
    {
        readonly List<(double X0, double Y0, double X1, double Y1, List<int> L)> _cells = [];
        readonly List<CamPath> _paths;
        public SubIndex(List<int> l, List<CamPath> paths)
        {
            _paths = paths;
            double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
            foreach (var i in l) { var q = paths[i].Pts[0]; x0 = Math.Min(x0, q.X); y0 = Math.Min(y0, q.Y); x1 = Math.Max(x1, q.X); y1 = Math.Max(y1, q.Y); }
            var m = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(l.Count / 8.0))); double sx = (x1 - x0) / m, sy = (y1 - y0) / m;
            var lists = new Dictionary<(int, int), List<int>>();
            foreach (var i in l) // in Nummernfolge: jede Teilliste bleibt aufsteigend
            {
                var q = paths[i].Pts[0];
                var cx = sx > 0 && double.IsFinite(sx) ? Math.Clamp((int)Math.Floor((q.X - x0) / sx), 0, m - 1) : 0; var cy = sy > 0 && double.IsFinite(sy) ? Math.Clamp((int)Math.Floor((q.Y - y0) / sy), 0, m - 1) : 0;
                if (!double.IsFinite(q.X) || !double.IsFinite(q.Y)) (cx, cy) = (-1, -1); // nicht endliche Punkte: eigene Liste, immer durchsucht
                if (!lists.TryGetValue((cx, cy), out var li)) lists[(cx, cy)] = li = []; li.Add(i);
            }
            foreach (var li in lists.Values)
            {
                double a0 = double.MaxValue, b0 = double.MaxValue, a1 = double.MinValue, b1 = double.MinValue; var odd = false;
                foreach (var i in li) { var q = paths[i].Pts[0]; if (!double.IsFinite(q.X) || !double.IsFinite(q.Y)) odd = true; a0 = Math.Min(a0, q.X); b0 = Math.Min(b0, q.Y); a1 = Math.Max(a1, q.X); b1 = Math.Max(b1, q.Y); }
                _cells.Add(odd ? (double.NegativeInfinity, double.NegativeInfinity, double.PositiveInfinity, double.PositiveInfinity, li) : (a0, b0, a1, b1, li));
            }
        }
        public void Nearest((double X, double Y) q, bool[] used, ref double bd, ref int best)
        {
            double cd = double.MaxValue; var ci = -1; // Bestes dieser Zelle
            for (var c = 0; c < _cells.Count; c++)
            {
                var (x0, y0, x1, y1, l) = _cells[c];
                double dx = q.X < x0 ? x0 - q.X : q.X > x1 ? q.X - x1 : 0, dy = q.Y < y0 ? y0 - q.Y : q.Y > y1 ? q.Y - y1 : 0;
                var lb = dx * dx + dy * dy; if (lb > cd || lb >= bd) continue; // bei Gleichstand mit dem Zellbesten weiter prüfen (kleinere Nummer); nicht näher als bd: nichts zu holen
                var k = 0;
                for (var t = 0; t < l.Count; t++)
                {
                    var i = l[t]; if (used[i]) continue; l[k++] = i; // benutzte Einträge entfernen, Folge bleibt
                    var d = D2(_paths[i].Pts[0], q);
                    if (d < cd || d == cd && i < ci) { cd = d; ci = i; }
                }
                l.RemoveRange(k, l.Count - k);
            }
            if (ci >= 0 && cd < bd) { bd = cd; best = ci; }
        }
    }

    /// <summary>Eine Runde fahren; in den Stegbereichen auf Steghöhe anheben. Rückgabe: Anzahl der Anhebungen.
    /// T-0747-fix3: Steggrenzen genau auf einem Bahnpunkt werden erkannt (jede Strecke deckt [Anfang, Ende) ab), vorher fielen sie still weg.</summary>
    static int Loop(StringBuilder g, List<(double X, double Y)> pts, bool closed, double z, List<(double S, double E)> tabs, double zTab, CamSettings s, CancellationToken ct = default)
    {
        const double e = 1e-7;
        var seq = closed ? [.. pts, pts[0]] : pts;
        var pos = 0.0; var up = false; var first = true; var lifts = 0;
        string F() { if (!first) return ""; first = false; return $" F{I(s.Feed)}"; }
        for (var i = 1; i < seq.Count; i++)
        {
            if ((i & 4095) == 0) ct.ThrowIfCancellationRequested(); // T-0758
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
        // T-0758: Nach innen kann ein gültiger Bahnpunkt nur innerhalb der Kontur und mindestens |dist| − 1e-7 von allen Strecken entfernt liegen – sein
        // Abstand zum Rand ist aber höchstens die halbe kleinere Rahmenseite. Ist die kleiner, bleibt beim Beschneiden kein Stück übrig (Ergebnis leer wie
        // bisher), nur ohne die rohe Linie, deren Strecken sich bei zu kleinen Löchern alle gegenseitig schneiden (quadratisch viele Schnittpunkte).
        if (dist < 0)
        {
            double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
            foreach (var (x, y) in pts) { x0 = Math.Min(x0, x); y0 = Math.Min(y0, y); x1 = Math.Max(x1, x); y1 = Math.Max(y1, y); }
            var scale = Math.Max(Math.Max(Math.Abs(x0), Math.Abs(x1)), Math.Max(Math.Abs(y0), Math.Abs(y1)));
            if (Math.Min(x1 - x0, y1 - y0) / 2 + 1e-6 + 1e-9 * scale < -dist) return [];
        }
        var (raw, gen, arcs) = RawOffset(pts, dist, chord);
        if (raw.Count < 3) return [];
        var ad = Math.Abs(dist); var np = pts.Count;
        var polyGrid = new RefGrid([((IReadOnlyList<(double X, double Y)>)pts, true, 0)], ad, ct, ad); // T-0758: feineres Raster, gleiche Treffer
        // Eine Ecke mit Bogen misst über die Sehnenrichtungen (höchstens der echte Abstand, mindestens cos(halber Schritt) davon): sie kann bis
        // ad / cos − ad außerhalb des Abfragerechtecks liegen und trotzdem „zu nah“ zählen.
        var cosHalf = 1.0;
        foreach (var w in arcs) if (w is not null) for (var k = 0; k + 1 < w.Length; k++) cosHalf = Math.Min(cosHalf, Math.Sqrt(Math.Max(0, (1 + w[k].X * w[k + 1].X + w[k].Y * w[k + 1].Y) / 2)));
        var validExpand = cosHalf > 0.5 ? ad / cosHalf - ad + 1e-6 : 2 * ad;
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
        StripIndex? ptsIndex = null; RowIndex? rowIndex = null;
        var ptsScale = 0.0; foreach (var (x, y) in pts) ptsScale = Math.Max(ptsScale, Math.Max(Math.Abs(x), Math.Abs(y)));
        bool Valid((double X, double Y) q, int k)
        {
            var gi = gen[k];
            var dg = gi >= 0 ? EdgeD(gi, q) : VertexD(-gi - 1, q);
            if (double.IsInfinity(dg) || Math.Abs(dg - ad) > 1e-6) return false; // nicht im Abstand zur eigenen Strecke/Ecke: Stück liegt im Innern
            // Strecke e verbindet Punkt e und e + 1; käme das Stück einer anderen Stelle zu nah?
            if (polyGrid.Any(q.X - ad, q.Y - ad, q.X + ad, q.Y + ad, validExpand, e => EdgeD(e, q) < ad - 1e-7 || VertexD(e, q) < ad - 1e-7 || VertexD((e + 1) % np, q) < ad - 1e-7)) return false;
            if (np < 64) return dist > 0 != Inside(pts, q); // falsche Seite der Kontur (zuletzt geprüft – teuer, T-0747-fix2)
            // T-0758: Hier liegt q mindestens ad − 1e-7 von jeder Strecke entfernt. Für so einen Punkt ist das Ergebnis der Strahlprüfung in Gleitkomma
            // genau (der Abstand zum Schnittpunkt auf dem Strahl ist mindestens der Abstand zur Strecke, der Rundungsfehler liegt bei 1e-15 der Koordinaten)
            // und hängt nicht von der Strahlrichtung ab (gerade/ungerade = Windungszahl modulo 2). Darum darf der Streifenindex den günstigeren Strahl
            // nehmen (beim Kamm senkrecht statt waagrecht über 200 000 Strecken).
            if (ad - 1e-7 > 1e-6 + 1e-10 * ptsScale) { rowIndex ??= new RowIndex(pts); return dist > 0 != rowIndex.Contains(q.X, q.Y); }
            ptsIndex ??= new StripIndex(pts); // gleiche Rechnung wie Inside, nur die Strecken in Höhe des Punkts
            return dist > 0 != ptsIndex.Contains(q.X, q.Y);
        }
        // Selbstschnitte der rohen Linie
        var m = raw.Count;
        // T-0758: feineres Raster, gleiche Schnittpunkte in gleicher Reihenfolge (ein Schnitt liegt in beiden Rahmen, bis auf 1e-12 der Streckenlänge)
        var rawGrid = new RefGrid([((IReadOnlyList<(double X, double Y)>)raw, true, 0)], AvgLen(raw, true) * 2, ct);
        var maxLen = 0.0; for (var k = 0; k < m; k++) maxLen = Math.Max(maxLen, D2(raw[k], raw[(k + 1) % m]));
        var hitExpand = 1e-6 + 1e-9 * Math.Sqrt(maxLen);
        var splits = new List<(double T, (double X, double Y) P)>?[m];
        for (var k = 0; k < m; k++)
        {
            if ((k & 255) == 0) ct.ThrowIfCancellationRequested();
            var a = raw[k]; var b = raw[(k + 1) % m]; var kk = k;
            foreach (var j in rawGrid.Near(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), hitExpand,
                j => !(j <= kk || j == kk + 1 || kk == 0 && j == m - 1) && Hit(a, b, raw[j], raw[(j + 1) % m]) is not null))
            {
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
        // T-0758: gleiche Wahl wie „erstes unbenutztes Stück“ – je Startpunkt ein Zeiger hinter die schon benutzten (benutzt bleibt benutzt), und für
        // „erstes unbenutztes Stück mit Start näher als 1e-7“ ein Raster der Startpunkte (Zelle 2e-7) statt alle Stücke durchzugehen (vorher quadratisch)
        var head = new Dictionary<(double, double), int>();
        int FirstUnused((double X, double Y) key, List<int> l)
        {
            var h = head.GetValueOrDefault(key); while (h < l.Count && used[l[h]]) h++; head[key] = h;
            return h < l.Count ? l[h] : -1;
        }
        Dictionary<(long, long), List<int>>? near = null; const double nc = 2e-7;
        static (long, long) NK((double X, double Y) q) => ((long)Math.Floor(q.X / nc), (long)Math.Floor(q.Y / nc));
        int NearStart((double X, double Y) e)
        {
            if (near is null)
            {
                near = [];
                for (var c = 0; c < valid.Count; c++)
                {
                    var q = valid[c][0];
                    if (!double.IsFinite(q.X) || !double.IsFinite(q.Y)) continue; // nie „gleich“ (Abstand NaN oder unendlich)
                    var k = NK(q); if (!near.TryGetValue(k, out var l)) near[k] = l = []; l.Add(c);
                }
            }
            if (!double.IsFinite(e.X) || !double.IsFinite(e.Y)) return -1;
            var (kx, ky) = NK(e); var best = -1;
            for (var dx = -1L; dx <= 1; dx++)
                for (var dy = -1L; dy <= 1; dy++)
                    if (near.TryGetValue((kx + dx, ky + dy), out var l))
                        foreach (var c in l) { if (best >= 0 && c >= best) break; if (!used[c] && Same(valid[c][0], e)) { best = c; break; } }
            return best;
        }
        for (var i = 0; i < valid.Count; i++)
        {
            if ((i & 255) == 0) ct.ThrowIfCancellationRequested(); // T-0758
            if (used[i]) continue; used[i] = true;
            var chain = new List<(double X, double Y)>(valid[i]); var first = valid[i][0]; var end = valid[i][^1]; var ok = true;
            for (var guard = 0; !Same(end, first) && guard <= valid.Count; guard++)
            {
                var nx = starts.TryGetValue(end, out var cands) ? FirstUnused(end, cands) : -1;
                if (nx < 0) nx = NearStart(end);
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
    /// <summary>T-0772: längster G-Code (Zeichen) – die Grenze einer .NET-Zeichenkette; darüber gab es bisher einen Absturz wegen Speicher.</summary>
    public const int MaxGcodeChars = 0x3FFFFFDF;

    /// <summary>T-0747-fix4: Sind zwei geschlossene Züge deckungsgleich (jeder Punkt des einen höchstens <paramref name="tol"/> vom anderen Zug,
    /// in beide Richtungen, auch Stückmitten)? Richtung und Startecke spielen keine Rolle.</summary>
    /// <summary>T-0772: Vorauswahl für die Doppelprüfung, wenn sehr viele Konturen fast denselben Rahmen und dieselbe Fläche haben (20 000 Quadrate mit
    /// einer versteckten Kerbe an verschiedener Stelle – bisher wurde jedes Paar mit zwei eigenen Rastern geprüft, quadratisch, über 150 s).
    /// <see cref="Coincident"/> verlangt unter anderem, dass JEDER Eckpunkt und jede Streckenmitte von a näher als DupTol (+1e-9) an einer Strecke von b
    /// liegt. Für einen einzigen solchen Prüfpunkt von a werden aus einem gemeinsamen Raster aller Strecken die Konturen geholt, die ihm so nahe kommen –
    /// nur sie können deckungsgleich sein. Gewählt wird ein seltener Prüfpunkt (wenige Konturen in seiner Zelle, z. B. die Kerbspitze). Die übrigen
    /// Prüfungen laufen unverändert in derselben Reihenfolge, das Ergebnis ist also dasselbe. Liefert ein Punkt zu viele Konturen, bleibt alles wie bisher.</summary>
    sealed class DupSelector
    {
        const int Cap = 64; const double Cell = 0.01;
        SegGrid? _grid; bool[]? _added; readonly Dictionary<(long, long), int> _density = []; readonly Dictionary<(long, long), int> _lastTag = [];
        static IEnumerable<(double X, double Y)> TestPoints(List<(double X, double Y)> p)
        {
            for (var i = 0; i < p.Count; i++) { var u = p[i]; var v = p[(i + 1) % p.Count]; yield return u; yield return ((u.X + v.X) / 2, (u.Y + v.Y) / 2); }
        }
        static (long, long) K((double X, double Y) q) => ((long)Math.Floor(q.X / Cell), (long)Math.Floor(q.Y / Cell));
        void Add(IReadOnlyList<CadContour> cs, int j)
        {
            _added![j] = true; var pts = cs[j].Pts;
            for (var k = 0; k < pts.Count; k++) _grid!.Add(pts[k], pts[(k + 1) % pts.Count], j);
            foreach (var q in TestPoints(pts))
            {
                var key = K(q);
                if (_lastTag.TryGetValue(key, out var t) && t == j) continue; // je Kontur und Zelle einmal zählen
                _lastTag[key] = j; _density[key] = _density.GetValueOrDefault(key) + 1;
            }
        }
        public List<int>? Candidates(IReadOnlyList<CadContour> cs, int i, List<int> near, CancellationToken ct)
        {
            _grid ??= new SegGrid(Cell); _added ??= new bool[cs.Count];
            var n = 0;
            foreach (var j in near) if (!_added[j]) { if ((++n & 255) == 0) ct.ThrowIfCancellationRequested(); Add(cs, j); }
            var d = DupTol + 1e-9; var pts = cs[i].Pts;
            foreach (var q in TestPoints(pts).OrderBy(q => _density.GetValueOrDefault(K(q))).Take(4))
            {
                var tags = new HashSet<int>(); var tooMany = false;
                _grid.Any(q.X - d, q.Y - d, q.X + d, q.Y + d, k =>
                {
                    var (a, b, tag) = _grid.Segs[k];
                    if (tag == i || CncCam.SegDist(q, a, b) >= d) return false;
                    tags.Add(tag); tooMany = tags.Count > Cap; return tooMany;
                });
                if (!tooMany) return near.Where(tags.Contains).ToList();
            }
            return null;
        }
    }

    public static bool Coincident(List<(double X, double Y)> a, List<(double X, double Y)> b, double tol, CancellationToken ct = default)
    {
        var (ax0, ay0, ax1, ay1) = (a.Min(p => p.X), a.Min(p => p.Y), a.Max(p => p.X), a.Max(p => p.Y));
        var (bx0, by0, bx1, by1) = (b.Min(p => p.X), b.Min(p => p.Y), b.Max(p => p.X), b.Max(p => p.Y));
        if (Math.Abs(ax0 - bx0) > tol || Math.Abs(ay0 - by0) > tol || Math.Abs(ax1 - bx1) > tol || Math.Abs(ay1 - by1) > tol) return false;
        if (Math.Abs(Math.Abs(SignedArea(a)) - Math.Abs(SignedArea(b))) > tol * (Length(a, true) + Length(b, true))) return false;
        static bool Covered(List<(double X, double Y)> p, List<(double X, double Y)> q, double tol, CancellationToken ct)
        {
            var g = BuildGrid([((IReadOnlyList<(double X, double Y)>)q, true, 0)], Math.Max(tol * 4, AvgLen(q, true)));
            for (var i = 0; i < p.Count; i++)
            {
                if ((i & 4095) == 0) ct.ThrowIfCancellationRequested(); // T-0758
                var u = p[i]; var v = p[(i + 1) % p.Count]; (double X, double Y) m = ((u.X + v.X) / 2, (u.Y + v.Y) / 2);
                if (!g.AnyWithin(u, tol + 1e-9) || !g.AnyWithin(m, tol + 1e-9)) return false;
            }
            return true;
        }
        return Covered(a, b, tol, ct) && Covered(b, a, tol, ct);
    }

    /// <summary>T-0747-fix2: berührt oder schneidet der Zug <paramref name="a"/> den geschlossenen Zug <paramref name="b"/>?</summary>
    static bool Touches(List<(double X, double Y)> a, bool aClosed, List<(double X, double Y)> b, CancellationToken ct = default)
    {
        var grid = BuildGrid([((IReadOnlyList<(double X, double Y)>)b, true, 0)], AvgLen(b, true) * 2);
        for (var k = 0; k < a.Count - (aClosed ? 0 : 1); k++)
        {
            if ((k & 1023) == 0) ct.ThrowIfCancellationRequested(); // T-0758
            var p = a[k]; var q = a[(k + 1) % a.Count];
            foreach (var j in grid.Near(Math.Min(p.X, q.X), Math.Min(p.Y, q.Y), Math.Max(p.X, q.X), Math.Max(p.Y, q.Y)))
                if (SegSeg(p, q, grid.Segs[j].A, grid.Segs[j].B) < 1e-7) return true;
        }
        return false;
    }

    /// <summary>T-0747-fix: kreuzt oder berührt sich der geschlossene Zug selbst (nicht benachbarte Strecken)?</summary>
    public static bool SelfIntersects(IReadOnlyList<(double X, double Y)> poly, CancellationToken ct = default)
    {
        var p = Clean(poly); var n = p.Count; if (n < 4) return false;
        var grid = new RefGrid([((IReadOnlyList<(double X, double Y)>)p, true, 0)], AvgLen(p, true) * 2, ct); // T-0758: feineres Raster, gleiche Treffer
        for (var k = 0; k < n; k++)
        {
            if ((k & 1023) == 0) ct.ThrowIfCancellationRequested();
            var a = p[k]; var b = p[(k + 1) % n]; var kk = k;
            // eine Strecke näher als 1e-7 liegt höchstens 1e-7 außerhalb des Rahmens
            if (grid.Any(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), 2e-7,
                j => !(j <= kk || j == kk + 1 || kk == 0 && j == n - 1) && SegSeg(a, b, p[j], p[(j + 1) % n]) < 1e-7)) return true;
        }
        return false;
    }

    /// <summary>T-0747-fix: Stellen der Kontur, an die der Fräser (Mitte auf den Bahnen) nicht herankommt – weiter als Radius + halber Radius
    /// (mindestens 0,2 mm). Normale Innenecken (90°) liegen darunter. Ergebnis: Mitte jeder zusammenhängenden Stelle (Messpunkte alle r/8, 0,02–0,25 mm).</summary>
    static List<(double X, double Y)> Unreached(IReadOnlyList<(double X, double Y)> contour, List<List<(double X, double Y)>> loops, double r, CancellationToken ct = default)
    {
        var reach = r + Math.Max(0.5 * r, 0.2);
        var grid = new RefGrid(loops.Select(l => ((IReadOnlyList<(double X, double Y)>)l, true, 0)), r, ct, reach); // T-0758: feineres Raster, gleiche Treffer
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
    static (int Tag, double D) Nearest(List<(double X, double Y)> path, bool closed, RefGrid obstacles, double limit, int skip, CancellationToken ct = default, SegGrid.Cursor? cur = null)
    {
        cur ??= obstacles.Fine.Own; // ohne eigenen Merker: der des Rasters (nur einfädig)
        // T-0747-fix5: erst schnell prüfen, ob überhaupt etwas zu nah ist (ohne Listen, Abbruch beim ersten Treffer) – meist nicht
        // T-0772: liegt im Rahmen der ganzen Bahn (± limit) gar keine fremde Strecke, kann keine Strecke der Bahn ihr näher als limit kommen –
        // eine Abfrage statt einer je Bahnstrecke (100 000 Kreise: je Kreis rund 90 Abfragen gespart). Gleiches Ergebnis.
        if (path.Count > 2)
        {
            double rx0 = path.Min(p => p.X) - limit, ry0 = path.Min(p => p.Y) - limit, rx1 = path.Max(p => p.X) + limit, ry1 = path.Max(p => p.Y) + limit;
            if (!obstacles.AnyInRect(rx0, ry0, rx1, ry1, j =>
                {
                    var (c, d, tag) = obstacles.Segs[j];
                    return tag != skip && Math.Max(c.X, d.X) >= rx0 && Math.Min(c.X, d.X) <= rx1 && Math.Max(c.Y, d.Y) >= ry0 && Math.Min(c.Y, d.Y) <= ry1;
                }, cur)) return (-1, double.MaxValue);
        }
        var any = false; var mm = closed ? path.Count : path.Count - 1;
        for (var i = 0; i < mm && !any; i++)
        {
            if ((i & 1023) == 0) ct.ThrowIfCancellationRequested();
            var a0 = path[i]; var b0 = path[(i + 1) % path.Count];
            any = obstacles.AnyNear(a0, b0, limit, 1e-6, // T-0772: lange Strecken stückweise (gleiches Ergebnis)
                j => obstacles.Segs[j].Tag != skip && SegSeg(a0, b0, obstacles.Segs[j].A, obstacles.Segs[j].B) < limit, cur);
        }
        if (!any) return (-1, double.MaxValue);
        var best = (Tag: -1, D: double.MaxValue); var m = closed ? path.Count : path.Count - 1;
        for (var i = 0; i < m; i++)
        {
            if ((i & 1023) == 0) ct.ThrowIfCancellationRequested();
            var a = path[i]; var b = path[(i + 1) % path.Count];
            // T-0758: kleinster Abstand dieser Strecke; bei Gleichstand gewinnt, was das bisherige Raster zuerst geliefert hätte – wie die Schleife
            // „dd < bisher“ über die Trefferliste, nur ohne Liste und Sortierung
            var (bj, bd) = obstacles.MinNear(a, b, limit, 1e-6, // T-0772: lange Strecken stückweise (gleiches Ergebnis, auch bei Gleichstand)
                j => { var (c, d, tag) = obstacles.Segs[j]; if (tag == skip) return double.NaN; var dd = SegSeg(a, b, c, d); return dd < limit ? dd : double.NaN; }, cur);
            if (bj >= 0 && bd < best.D) best = (obstacles.Segs[bj].Tag, bd);
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
    public static (List<(double X, double Y)> Pts, double Dev) Simplify(IReadOnlyList<(double X, double Y)> poly, double eps, CancellationToken ct = default)
    {
        var p = Clean(poly); var n = p.Count; if (n < 8 || eps <= 0) return (p, 0);
        var far = Enumerable.Range(1, n - 1).MaxBy(k => D2(p[0], p[k]));
        var keep = new bool[n]; keep[0] = keep[far] = true; var dev = 0.0;
        var stack = new Stack<(int A, int B)>(); stack.Push((0, far)); stack.Push((far, n));
        var steps = 0L; BoxTree? tree = null;
        while (stack.Count > 0)
        {
            var (a, b) = stack.Pop(); if (b - a < 2) continue;
            var pa = p[a]; var pb = p[b % n]; double max = -1; var at = -1;
            if ((steps += b - a) > 1 << 20) { steps = 0; ct.ThrowIfCancellationRequested(); } // T-0758
            if (b - a > 65536) (max, at) = FarthestPar(p, a + 1, b - 1, pa, pb, ct); // T-0758: sehr lange Bereiche auf allen Kernen, gleiches Ergebnis
            else if (b - a > 512) (max, at) = (tree ??= new BoxTree(p)).Farthest(a + 1, b - 1, pa, pb); // T-0758: gleiches Ergebnis, Blöcke ohne Chance übersprungen
            else for (var k = a + 1; k < b; k++) { var dk = SegDist(p[k], pa, pb); if (dk > max) { max = dk; at = k; } }
            if (max > eps) { keep[at] = true; stack.Push((a, at)); stack.Push((at, b)); }
            else dev = Math.Max(dev, max);
        }
        var res = p.Where((_, k) => keep[k]).ToList();
        return res.Count >= 3 && Math.Abs(SignedArea(res)) > 1e-9 ? (res, dev) : (p, 0);
    }

    /// <summary>T-0758: wie die Schleife „für k von lo bis hi: wenn SegDist &gt; bisher, merken“, in gleich großen Stücken parallel; die Stücke werden der
    /// Reihe nach mit „größer als“ zusammengeführt – bei Gleichstand gewinnt also wie vorher der erste Punkt.</summary>
    static (double Max, int At) FarthestPar(List<(double X, double Y)> p, int lo, int hi, (double X, double Y) pa, (double X, double Y) pb, CancellationToken ct)
    {
        var parts = Math.Max(2, Environment.ProcessorCount * 2); var len = hi - lo + 1; var res = new (double Max, int At)[parts];
        Parallel.For(0, parts, new ParallelOptions { CancellationToken = ct }, c =>
        {
            int k0 = lo + (int)((long)len * c / parts), k1 = lo + (int)((long)len * (c + 1) / parts) - 1; double max = -1; var at = -1;
            for (var k = k0; k <= k1; k++) { var dk = SegDist(p[k], pa, pb); if (dk > max) { max = dk; at = k; } }
            res[c] = (max, at);
        });
        double m = -1; var best = -1;
        foreach (var (mx, at) in res) if (mx > m) { m = mx; best = at; }
        return (m, best);
    }

    /// <summary>T-0758: Rahmenbaum über die Punkte eines Zugs für Douglas-Peucker. <see cref="Farthest"/> liefert genau, was die Schleife
    /// „für k von lo bis hi: wenn SegDist &gt; bisher, merken“ liefert (größter Abstand, bei Gleichstand der erste), überspringt aber Teilbäume, deren
    /// Punkte sicher kleiner sind: der Abstand zur Strecke ist konvex, sein Größtwert über einem Rahmen liegt an einer Ecke; mit Spielraum für Rundung.
    /// Beim Kamm mit 50 000 Zähnen (200 000 Punkte) fiel die Glättung so von rund 120 s auf Sekundenbruchteile.</summary>
    sealed class BoxTree
    {
        const int Leaf = 32;
        readonly List<(double X, double Y)> _p; readonly List<(int Lo, int Hi, double X0, double Y0, double X1, double Y1, int L, int R)> _n = []; readonly double _scale;
        public BoxTree(List<(double X, double Y)> p)
        {
            _p = p; var sc = 0.0; foreach (var (x, y) in p) sc = Math.Max(sc, Math.Max(Math.Abs(x), Math.Abs(y))); _scale = sc;
            Build(0, p.Count - 1);
        }
        int Build(int lo, int hi)
        {
            var id = _n.Count; _n.Add(default);
            int l = -1, r = -1; double x0, y0, x1, y1;
            if (hi - lo + 1 <= Leaf)
            {
                x0 = y0 = double.PositiveInfinity; x1 = y1 = double.NegativeInfinity;
                for (var k = lo; k <= hi; k++) { var q = _p[k]; x0 = Math.Min(x0, q.X); y0 = Math.Min(y0, q.Y); x1 = Math.Max(x1, q.X); y1 = Math.Max(y1, q.Y); }
            }
            else
            {
                var mid = (lo + hi) / 2; l = Build(lo, mid); r = Build(mid + 1, hi);
                var (a, b) = (_n[l], _n[r]); x0 = Math.Min(a.X0, b.X0); y0 = Math.Min(a.Y0, b.Y0); x1 = Math.Max(a.X1, b.X1); y1 = Math.Max(a.Y1, b.Y1);
            }
            _n[id] = (lo, hi, x0, y0, x1, y1, l, r);
            return id;
        }
        public (double Max, int At) Farthest(int lo, int hi, (double X, double Y) pa, (double X, double Y) pb)
        {
            double max = -1; var at = -1; var stack = new Stack<int>(); stack.Push(0);
            var margin = 1e-9 * (1 + _scale);
            while (stack.Count > 0)
            {
                var (nlo, nhi, x0, y0, x1, y1, l, r) = _n[stack.Pop()];
                if (nhi < lo || nlo > hi) continue;
                // alle Punkte des Rahmens sicher kleiner als das bisher Größte (bei NaN wird nicht übersprungen)
                var ub = Math.Max(Math.Max(SegDist((x0, y0), pa, pb), SegDist((x1, y0), pa, pb)), Math.Max(SegDist((x0, y1), pa, pb), SegDist((x1, y1), pa, pb)));
                if (ub + margin < max) continue;
                if (l < 0) { for (var k = Math.Max(lo, nlo); k <= Math.Min(hi, nhi); k++) { var dk = SegDist(_p[k], pa, pb); if (dk > max) { max = dk; at = k; } } continue; }
                stack.Push(r); stack.Push(l); // links zuerst: Reihenfolge der Punkte bleibt
            }
            return (max, at);
        }
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

    /// <summary>T-0747-fix18: Parameter t (0…1) auf a→b, an dem a→b die Strecke c→d schneidet oder berührt, sonst <c>null</c>.</summary>
    static double? Cross((double X, double Y) a, (double X, double Y) b, (double X, double Y) c, (double X, double Y) d)
    {
        double rx = b.X - a.X, ry = b.Y - a.Y, sx = d.X - c.X, sy = d.Y - c.Y, den = rx * sy - ry * sx;
        if (Math.Abs(den) < 1e-18) return null; // parallel: Teilstücke werden über ihre Mitte geprüft
        double qx = c.X - a.X, qy = c.Y - a.Y, t = (qx * sy - qy * sx) / den, u = (qx * ry - qy * rx) / den;
        return t >= 0 && t <= 1 && u >= -1e-9 && u <= 1 + 1e-9 ? t : null;
    }

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
        internal static (int Rows, long Cost) Plan(IReadOnlyList<(double X, double Y)> p, bool swap)
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

    /// <summary>T-0758: Punkt in geschlossenem Zug wie <see cref="CadContour.Contains"/> (gleiche Formel je Strecke, gleiche Reihenfolge der Ecken), nur über
    /// waagrechte Streifen: eine Strecke kann das Ergebnis nur ändern, wenn sie die Höhe des Punkts überdeckt (min ≤ y &lt; max) – und dann steht sie im
    /// Streifen des Punkts (Streifennummer wächst mit y; außerhalb wird auf den ersten/letzten Streifen begrenzt, nicht abgewiesen).</summary>
    sealed class StripIndex
    {
        readonly List<(double X, double Y)> _p; readonly double _y0, _h; readonly int[][] _rows;
        public StripIndex(List<(double X, double Y)> p)
        {
            _p = p; var n = p.Count; var (rows, _) = RowIndex.Plan(p, false);
            _y0 = p.Min(q => q.Y); var y1 = p.Max(q => q.Y); _h = Math.Max((y1 - _y0) / rows, 1e-12);
            var lists = new List<int>[rows]; for (var r = 0; r < rows; r++) lists[r] = [];
            for (var i = 0; i < n; i++)
            {
                var a = p[i]; var b = p[i == 0 ? n - 1 : i - 1]; // Strecke (i, i−1) wie in Contains
                int r0 = Row(Math.Min(a.Y, b.Y), rows), r1 = Row(Math.Max(a.Y, b.Y), rows);
                for (var r = r0; r <= r1; r++) lists[r].Add(i);
            }
            _rows = lists.Select(l => l.ToArray()).ToArray();
        }
        int Row(double y, int rows) { var f = Math.Floor((y - _y0) / _h); return f < 0 ? 0 : f >= rows - 1 ? rows - 1 : (int)f; }
        public bool Contains(double x, double y)
        {
            if (double.IsNaN(y)) return false;
            var inside = false; var n = _p.Count;
            foreach (var i in _rows[Row(y, _rows.Length)])
            {
                var (xi, yi) = _p[i]; var (xj, yj) = _p[i == 0 ? n - 1 : i - 1];
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

    /// <summary>T-0758: Raster für Prüfungen, deren Ergebnis nur von den Strecken abhängt, die eine Bedingung erfüllen (Abstand, Schnitt). Gesucht wird in
    /// einem feineren Raster; gezählt wird eine Strecke aber nur, wenn sie auch im bisherigen Raster (Bezugszelle wie <see cref="BuildGrid"/>) gefunden
    /// worden wäre – das Ergebnis ist also dasselbe, nur ohne die vielen Strecken, die in groben Zellen bloß mitgelaufen sind (Kamm mit 50 000 Zähnen:
    /// 2000 Strecken je Zelle). <c>expand</c>: so weit kann eine Strecke, die die Bedingung erfüllt, höchstens außerhalb des Abfragerechtecks liegen.
    /// <see cref="Near"/> liefert die Treffer in der bisherigen Reihenfolge.</summary>
    sealed class RefGrid
    {
        readonly double _ref; public readonly SegGrid Fine;
        readonly bool _same; // nicht verfeinert: das Raster ist das bisherige – Abfragen genau wie vorher (ohne Erweiterung und Bezugsprüfung)
        public List<((double X, double Y) A, (double X, double Y) B, int Tag)> Segs => Fine.Segs;
        public RefGrid(IEnumerable<(IReadOnlyList<(double X, double Y)> Pts, bool Closed, int Tag)> polys, double cell, CancellationToken ct = default, double pointQuery = 0)
        {
            var list = polys.ToList();
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            foreach (var (pts, _, _) in list) foreach (var (x, y) in pts) { minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); }
            var diag = minX > maxX ? 1 : Math.Sqrt((maxX - minX) * (maxX - minX) + (maxY - minY) * (maxY - minY));
            _ref = Math.Max(Math.Max(cell, diag / 2048), 1e-9); // genau die Zelle von BuildGrid/SegGrid
            // feinere Zelle: halbieren (höchstens 1/1024 der Bezugszelle), solange der Aufwand höchstens 16 Zellen je Strecke beträgt. Abfragen über die
            // eigenen Strecken (Schnitte, Bahn gegen Konturen): Aufwand = überdeckte Zellen. Abfragen um Punkte (pointQuery = halbe Breite des
            // Abfragerechtecks): Aufwand = Einträge (lange Strecken liegen im groben Raster), Zelle nicht kleiner als die halbe Abfragebreite.
            long n = 0; foreach (var (pts, closed, _) in list) n += Math.Max(0, pts.Count - (closed ? 0 : 1));
            var fine = _ref;
            // nur verfeinern, wenn das bisherige Raster überladen ist (im Mittel mehr als 8 Strecken je belegter Zelle) – sonst ist es schon gut
            long entries = 0; var occupied = new HashSet<(long, long)>();
            foreach (var (pts, closed, _) in list)
                for (var i = 0; i < pts.Count - (closed ? 0 : 1); i++)
                {
                    var a = pts[i]; var b = pts[(i + 1) % pts.Count];
                    long x0 = R(Math.Min(a.X, b.X)), x1 = R(Math.Max(a.X, b.X)), y0 = R(Math.Min(a.Y, b.Y)), y1 = R(Math.Max(a.Y, b.Y));
                    if (((double)x1 - x0 + 1) * ((double)y1 - y0 + 1) > SegGrid.SmallCells) continue;
                    for (var x = x0; x <= x1; x++) for (var y = y0; y <= y1; y++) { entries++; occupied.Add((x, y)); }
                }
            var occ = (double)entries / Math.Max(1, occupied.Count);
            // Aufwand je Zelle c: Abfragen um Punkte zählen Einträge (lange Strecken liegen grob, 4 Einträge); Abfragen über die eigenen Strecken zählen
            // die überdeckten Zellen, höchstens so viele, wie das lineare Durchgehen kostet (2 × Strecken, siehe SegGrid)
            double Cost(double c)
            {
                double e = 0;
                foreach (var (pts, closed, _) in list)
                    for (var i = 0; i < pts.Count - (closed ? 0 : 1); i++)
                    {
                        var a = pts[i]; var b = pts[(i + 1) % pts.Count];
                        var cells = (Math.Floor(Math.Max(a.X, b.X) / c) - Math.Floor(Math.Min(a.X, b.X) / c) + 1) * (Math.Floor(Math.Max(a.Y, b.Y) / c) - Math.Floor(Math.Min(a.Y, b.Y) / c) + 1);
                        e += pointQuery > 0 ? (cells > SegGrid.SmallCells ? 4 : cells) : Math.Min(cells, 2.0 * n + 64);
                    }
                return e;
            }
            if (occ > 8 && n > 0)
            {
                var limit = Math.Max(16.0 * n, 4 * Cost(_ref));
                for (var k = 1; k <= 10; k++)
                {
                    var c = _ref / (1 << k);
                    if (pointQuery > 0 && c < pointQuery) break; // kleinere Zellen als die halbe Abfragebreite bringen kaum weniger Kandidaten, aber viel mehr Zellen
                    if (Cost(c) > limit) break;
                    fine = c;
                    if (occ * (c / _ref) <= 4) break; // vorsichtige Schätzung der Belegung (wie bei Linien: halbe Zelle, halbe Belegung)
                }
            }
            Fine = new SegGrid(fine); _same = fine == _ref; _along = new(BuildAlong);
            var m = 0;
            foreach (var (pts, closed, tag) in list) for (var i = 0; i < pts.Count - (closed ? 0 : 1); i++) { if ((++m & 4095) == 0) ct.ThrowIfCancellationRequested(); Fine.Add(pts[i], pts[(i + 1) % pts.Count], tag); }
        }
        long R(double v) => (long)Math.Floor(v / _ref);
        bool InRef(int j, long qx0, long qy0, long qx1, long qy1)
        {
            var (a, b, _) = Fine.Segs[j];
            return R(Math.Max(a.X, b.X)) >= qx0 && R(Math.Min(a.X, b.X)) <= qx1 && R(Math.Max(a.Y, b.Y)) >= qy0 && R(Math.Min(a.Y, b.Y)) <= qy1;
        }
        public bool Any(double minX, double minY, double maxX, double maxY, double expand, Func<int, bool> pred, SegGrid.Cursor? cur = null)
        {
            if (_same) return Fine.Any(cur ?? Fine.Own, minX, minY, maxX, maxY, pred);
            long qx0 = R(minX), qy0 = R(minY), qx1 = R(maxX), qy1 = R(maxY);
            return Fine.Any(cur ?? Fine.Own, minX - expand, minY - expand, maxX + expand, maxY + expand, j => InRef(j, qx0, qy0, qx1, qy1) && pred(j));
        }
        public bool AnyWithin((double X, double Y) q, double d) => d > 0 && Any(q.X - d, q.Y - d, q.X + d, q.Y + d, 0, i => SegDist(q, Segs[i].A, Segs[i].B) < d);
        /// <summary>Strecke mit dem kleinsten Wert (NaN = nicht zählen); bei Gleichstand die, die das bisherige Raster zuerst geliefert hätte.</summary>
        public (int J, double D) Min(double minX, double minY, double maxX, double maxY, double expand, Func<int, double> score, SegGrid.Cursor? cur = null)
        {
            long qx0 = R(minX), qy0 = R(minY), qx1 = R(maxX), qy1 = R(maxY);
            var bj = -1; var bd = double.MaxValue; (long X, long Y) bk = default;
            (long X, long Y) Key(int j) { var (a, b, _) = Fine.Segs[j]; return (Math.Max(R(Math.Min(a.X, b.X)), qx0), Math.Max(R(Math.Min(a.Y, b.Y)), qy0)); }
            if (_same) expand = 0;
            Fine.Any(cur ?? Fine.Own, minX - expand, minY - expand, maxX + expand, maxY + expand, j =>
            {
                if (!_same && !InRef(j, qx0, qy0, qx1, qy1)) return false;
                var d = score(j); if (double.IsNaN(d) || d > bd) return false;
                if (d < bd || bj < 0) { bj = j; bd = d; bk = Key(j); return false; }
                var k = Key(j); // Gleichstand: frühere Stelle im bisherigen Raster gewinnt
                if (k.X < bk.X || k.X == bk.X && (k.Y < bk.Y || k.Y == bk.Y && j < bj)) { bj = j; bk = k; }
                return false;
            });
            return (bj, bd);
        }
        /// <summary>T-0772: Kandidaten für „Strecke näher als limit an a–b“ bei langen Strecken nicht über das ganze Rechteck (eine schräge Gravurlinie
        /// über die Platte: alle Strecken der Platte, sortiert), sondern Stück für Stück entlang der Linie. Jede Strecke, die a–b näher als limit kommt,
        /// liegt im Rechteck eines Stücks (± limit). Geliefert werden genau die Strecken, die die bisherige Abfrage auch geliefert hätte
        /// (<paramref name="orig"/>: gleicher Zellen- und Bezugsrahmen), mehrfach gelieferte stören nicht: Any ist ja/nein, Min nimmt den kleinsten
        /// Wert, bei Gleichstand nach Stelle im bisherigen Raster – beides hängt nicht von der Reihenfolge ab.</summary>
        void Along((double X, double Y) a, (double X, double Y) b, double limit, Func<int, bool> orig, Func<int, bool> visit, SegGrid.Cursor cur)
        {
            var len = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y)); var step = 16 * Fine.CellSize;
            var n = (int)Math.Min(1 << 20, Math.Ceiling(len / step));
            var m = limit + 1e-9 * (1 + Math.Abs(a.X) + Math.Abs(a.Y) + Math.Abs(b.X) + Math.Abs(b.Y) + len); // Reserve gegen Rundung der Teilpunkte
            for (var k = 0; k < n; k++)
            {
                double t0 = (double)k / n, t1 = (double)(k + 1) / n;
                double x0 = a.X + (b.X - a.X) * t0, y0 = a.Y + (b.Y - a.Y) * t0;
                double x1 = k == n - 1 ? b.X : a.X + (b.X - a.X) * t1, y1 = k == n - 1 ? b.Y : a.Y + (b.Y - a.Y) * t1;
                if (Rect(cur, Math.Min(x0, x1) - m, Math.Min(y0, y1) - m, Math.Max(x0, x1) + m, Math.Max(y0, y1) + m, j => orig(j) && visit(j))) return;
            }
        }
        /// <summary>T-0772: Index der langen Strecken (grobes Raster) entlang ihres Verlaufs: Zellen der Größe 8 × Feinzelle, eingetragen alle
        /// ac/4 entlang der Strecke. Ein Punkt der Strecke liegt höchstens ac/8 von einem Eintrag entfernt – Abfragen mit ± 1 Zelle finden jede lange
        /// Strecke, die das Rechteck wirklich berührt (das grobe Raster liefert dagegen jede, deren Rahmen es berührt: eine schräge Linie über die
        /// ganze Platte bei jeder Abfrage). Sehr lange Strecken (über 200 000 Einträge) werden immer geliefert.</summary>
        (Dictionary<(long, long), List<int>> Idx, double Cell, List<int> Always) AlongIndex => _along.Value;
        readonly Lazy<(Dictionary<(long, long), List<int>>, double, List<int>)> _along;
        (Dictionary<(long, long), List<int>>, double, List<int>) BuildAlong()
        {
            var ac = 8 * Fine.CellSize; var idx = new Dictionary<(long, long), List<int>>(); var always = new List<int>();
            foreach (var j in Fine.BigList)
            {
                var (p, q, _) = Fine.Segs[j];
                var len = Math.Sqrt((q.X - p.X) * (q.X - p.X) + (q.Y - p.Y) * (q.Y - p.Y)); var steps = Math.Ceiling(len / (ac / 4));
                if (!(steps <= 200_000)) { always.Add(j); continue; }
                var n = Math.Max(1, (int)steps); (long, long) lastKey = (long.MinValue, long.MinValue);
                for (var k = 0; k <= n; k++)
                {
                    double t = (double)k / n; var key = ((long)Math.Floor((p.X + (q.X - p.X) * t) / ac), (long)Math.Floor((p.Y + (q.Y - p.Y) * t) / ac));
                    if (key == lastKey) continue; lastKey = key;
                    if (!idx.TryGetValue(key, out var l)) idx[key] = l = []; if (l.Count == 0 || l[^1] != j) l.Add(j);
                }
            }
            return (idx, ac, always);
        }
        /// <summary>T-0772: Strecken, die das Rechteck berühren können (Obermenge): kurze über das feine Raster, lange über <see cref="AlongIndex"/>.
        /// Sehr große Rechtecke wie bisher über <see cref="SegGrid.Any(SegGrid.Cursor, double, double, double, double, Func{int, bool})"/>.</summary>
        /// <summary>T-0772: Gibt es eine Strecke mit <paramref name="pred"/>, die das Rechteck berühren kann? (Obermenge – „nein“ ist sicher)</summary>
        public bool AnyInRect(double minX, double minY, double maxX, double maxY, Func<int, bool> pred, SegGrid.Cursor? cur = null) => Rect(cur ?? Fine.Own, minX, minY, maxX, maxY, pred);
        bool Rect(SegGrid.Cursor cur, double minX, double minY, double maxX, double maxY, Func<int, bool> visit)
        {
            if (Fine.CellCount(minX, minY, maxX, maxY) > 4096) return Fine.Any(cur, minX, minY, maxX, maxY, visit);
            var (idx, ac, always) = AlongIndex;
            return Fine.AnyMixed(cur, minX, minY, maxX, maxY, idx, ac, always, visit);
        }
        long FC(double v) => (long)Math.Floor(v / Fine.CellSize);
        /// <summary>Hätte die bisherige Abfrage (Rechteck ± e im feinen Raster, ggf. mit Bezugsrahmen) Strecke j geliefert?</summary>
        Func<int, bool> Orig(double minX, double minY, double maxX, double maxY, double e)
        {
            long fx0 = FC(minX - e), fy0 = FC(minY - e), fx1 = FC(maxX + e), fy1 = FC(maxY + e);
            long qx0 = R(minX), qy0 = R(minY), qx1 = R(maxX), qy1 = R(maxY);
            return j =>
            {
                var (p, q, _) = Fine.Segs[j];
                if (FC(Math.Max(p.X, q.X)) < fx0 || FC(Math.Min(p.X, q.X)) > fx1 || FC(Math.Max(p.Y, q.Y)) < fy0 || FC(Math.Min(p.Y, q.Y)) > fy1) return false;
                return _same || InRef(j, qx0, qy0, qx1, qy1);
            };
        }
        /// <summary>T-0772: wie <see cref="Any"/> mit dem Rechteck der Strecke a–b ± limit, für ein Prädikat, das nur Strecken näher als limit an a–b
        /// annimmt – gleiches Ergebnis, bei langen Strecken über <see cref="Along"/>.</summary>
        public bool AnyNear((double X, double Y) a, (double X, double Y) b, double limit, double expand, Func<int, bool> pred, SegGrid.Cursor? cur = null)
        {
            double minX = Math.Min(a.X, b.X) - limit, minY = Math.Min(a.Y, b.Y) - limit, maxX = Math.Max(a.X, b.X) + limit, maxY = Math.Max(a.Y, b.Y) + limit;
            var hit = false;
            Along(a, b, limit, Orig(minX, minY, maxX, maxY, _same ? 0 : expand), j => hit = pred(j), cur ?? Fine.Own);
            return hit;
        }
        /// <summary>T-0772: wie <see cref="Min"/> mit dem Rechteck der Strecke a–b ± limit, für eine Bewertung, die nur Strecken näher als limit an a–b
        /// zählt (sonst NaN) – gleiches Ergebnis (auch bei Gleichstand), bei langen Strecken über <see cref="Along"/>.</summary>
        public (int J, double D) MinNear((double X, double Y) a, (double X, double Y) b, double limit, double expand, Func<int, double> score, SegGrid.Cursor? cur = null)
        {
            double minX = Math.Min(a.X, b.X) - limit, minY = Math.Min(a.Y, b.Y) - limit, maxX = Math.Max(a.X, b.X) + limit, maxY = Math.Max(a.Y, b.Y) + limit;
            long qx0 = R(minX), qy0 = R(minY);
            var bj = -1; var bd = double.MaxValue; (long X, long Y) bk = default;
            (long X, long Y) Key(int j) { var (p, q, _) = Fine.Segs[j]; return (Math.Max(R(Math.Min(p.X, q.X)), qx0), Math.Max(R(Math.Min(p.Y, q.Y)), qy0)); }
            Along(a, b, limit, Orig(minX, minY, maxX, maxY, _same ? 0 : expand), j =>
            {
                var d = score(j); if (double.IsNaN(d) || d > bd) return false;
                if (d < bd || bj < 0) { bj = j; bd = d; bk = Key(j); return false; }
                var k = Key(j); // Gleichstand: frühere Stelle im bisherigen Raster gewinnt
                if (k.X < bk.X || k.X == bk.X && (k.Y < bk.Y || k.Y == bk.Y && j < bj)) { bj = j; bk = k; }
                return false;
            }, cur ?? Fine.Own);
            return (bj, bd);
        }
        /// <summary>Strecken mit <paramref name="keep"/>, in der Reihenfolge, in der das bisherige Raster sie geliefert hätte.</summary>
        public List<int> Near(double minX, double minY, double maxX, double maxY, double expand, Func<int, bool> keep, SegGrid.Cursor? cur = null)
        {
            long qx0 = R(minX), qy0 = R(minY), qx1 = R(maxX), qy1 = R(maxY);
            var res = new List<(long X, long Y, int I)>();
            if (_same) return Fine.Near(cur ?? Fine.Own, minX, minY, maxX, maxY).Where(keep).ToList();
            foreach (var j in Fine.Near(cur ?? Fine.Own, minX - expand, minY - expand, maxX + expand, maxY + expand))
            {
                if (!InRef(j, qx0, qy0, qx1, qy1) || !keep(j)) continue;
                var (a, b, _) = Fine.Segs[j];
                res.Add((Math.Max(R(Math.Min(a.X, b.X)), qx0), Math.Max(R(Math.Min(a.Y, b.Y)), qy0), j));
            }
            res.Sort((p, q) => p.X != q.X ? p.X.CompareTo(q.X) : p.Y != q.Y ? p.Y.CompareTo(q.Y) : p.I.CompareTo(q.I));
            return res.Select(t => t.I).ToList();
        }
    }

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
/// Strecken durchzugehen (Fräsbahn-Prüfung, Versatz, Kettenbildung, Wandpaare).
/// T-0758: Eine Strecke gilt weiter als „in jeder Zelle ihres Rahmens“ (gleiche Treffer in gleicher Reihenfolge wie bisher), wird aber nicht mehr in
/// jede dieser Zellen eingetragen: Strecken, deren Rahmen mehr als <see cref="SmallCells"/> Zellen überdeckt (lange schräge Linien, Plattenkanten),
/// kommen in ein gröberes Raster (je Richtung eine Stufe: Zellen 2^Lx bzw. 2^Ly-mal so breit und hoch, höchstens 2 × 2 Zellen je Strecke). Vorher belegte eine schräge 2000-mm-Linie bei
/// 3 mm Zellen rund 445 000 Zellen; 100 solche Linien brauchten 28 s und 240 MB nur für den Aufbau.</summary>
public sealed class SegGrid(double cell)
{
    readonly double _cell = Math.Max(cell, 1e-9);
    /// <summary>T-0747-fix12: Zellgröße (für die Wahl Raster-Abfrage oder lineares Durchgehen bei sehr großen Abfragerechtecken).</summary>
    public double CellSize => _cell;
    readonly Dictionary<long, List<int>> _map = [];
    /// <summary>T-0758: höchstens so viele Zellen trägt eine Strecke ins feine Raster ein; größere Rahmen kommen ins grobe Raster.</summary>
    public const int SmallCells = 64;
    readonly Dictionary<(int Lx, int Ly, long X, long Y), List<int>> _big = [];
    readonly List<int> _bigList = [];
    readonly List<(int Lx, int Ly)> _levels = []; // benutzte Stufen des groben Rasters (je Richtung eigene, damit lange senkrechte Strecken schmal bleiben)
    /// <summary>T-0747-fix5: Merker für „schon gesehen“ – je Thread ein eigener, dann darf das Raster parallel abgefragt werden (nur lesen).</summary>
    public sealed class Cursor { internal int[] Stamp = new int[16]; internal int Query; }
    readonly Cursor _own = new();
    internal Cursor Own => _own;
    public List<((double X, double Y) A, (double X, double Y) B, int Tag)> Segs { get; } = [];
    /// <summary>T-0758: Zahl der Strecken im groben Raster (für Tests).</summary>
    public int BigCount => _bigList.Count;
    /// <summary>T-0758: Zahl der Einträge im feinen Raster (für Tests).</summary>
    public long FineEntries { get; private set; }

    static long Key(long x, long y) => (x << 32) ^ (y & 0xFFFFFFFFL);
    long C(double v) => (long)Math.Floor(v / _cell);

    (long X0, long Y0, long X1, long Y1) Box(int i)
    {
        var (a, b, _) = Segs[i];
        return (C(Math.Min(a.X, b.X)), C(Math.Min(a.Y, b.Y)), C(Math.Max(a.X, b.X)), C(Math.Max(a.Y, b.Y)));
    }

    public int Add((double X, double Y) a, (double X, double Y) b, int tag)
    {
        var i = Segs.Count; Segs.Add((a, b, tag));
        var (x0, y0, x1, y1) = Box(i);
        if (((double)x1 - x0 + 1) * ((double)y1 - y0 + 1) <= SmallCells)
        {
            for (var x = x0; x <= x1; x++)
                for (var y = y0; y <= y1; y++)
                {
                    var k = Key(x, y); if (!_map.TryGetValue(k, out var l)) _map[k] = l = []; l.Add(i); FineEntries++;
                }
            return i;
        }
        var Lx = 0; while (Lx < 63 && (x1 >> Lx) - (x0 >> Lx) > 1) Lx++;
        var Ly = 0; while (Ly < 63 && (y1 >> Ly) - (y0 >> Ly) > 1) Ly++;
        if (!_levels.Contains((Lx, Ly))) _levels.Add((Lx, Ly));
        _bigList.Add(i);
        for (var x = x0 >> Lx; x <= x1 >> Lx; x++)
            for (var y = y0 >> Ly; y <= y1 >> Ly; y++)
            {
                if (!_big.TryGetValue((Lx, Ly, x, y), out var l)) _big[(Lx, Ly, x, y)] = l = []; l.Add(i);
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
        var res = new List<int>();
        Walk(cur, minX, minY, maxX, maxY, res, null);
        return res;
    }

    /// <summary>T-0747-fix2: wie <see cref="Near"/>, aber ohne Liste – bricht ab, sobald <paramref name="hit"/> wahr ist.</summary>
    public bool Any(double minX, double minY, double maxX, double maxY, Func<int, bool> hit) => Any(_own, minX, minY, maxX, maxY, hit);

    public bool Any(Cursor cur, double minX, double minY, double maxX, double maxY, Func<int, bool> hit) => Walk(cur, minX, minY, maxX, maxY, null, hit);

    /// <summary>T-0758: alle Strecken, deren Zellrahmen die Zellen des Rechtecks berührt, in der bisherigen Reihenfolge: Zellen spaltenweise (x, dann y),
    /// je Zelle nach Nummer; eine Strecke zählt bei der ersten Zelle, in der sie vorkommt. Strecken des groben Rasters werden mit ihrer ersten Zelle
    /// (größtes x0/y0 von Strecke und Rechteck) einsortiert.</summary>
    bool Walk(Cursor cur, double minX, double minY, double maxX, double maxY, List<int>? res, Func<int, bool>? hit)
    {
        bool visit(int i) { if (res is not null) { res.Add(i); return false; } return hit!(i); }
        Next(cur, Segs.Count); var stamp = cur.Stamp; var query = cur.Query;
        long qx0 = C(minX), qx1 = C(maxX), qy0 = C(minY), qy1 = C(maxY);
        List<(long X, long Y, int I)>? big = null;
        // T-0758: Rechteck mit mehr Zellen als Strecken (lange schräge Bahnstrecke über die ganze Platte): statt jede Zelle nachzusehen alle Strecken über
        // ihren Zellrahmen prüfen – eine Strecke steht in jeder Zelle ihres Rahmens, Treffer und Reihenfolge bleiben also gleich.
        if (qx0 <= qx1 && qy0 <= qy1 && ((double)qx1 - qx0 + 1) * ((double)qy1 - qy0 + 1) > 2.0 * Segs.Count + 64)
        {
            var all = new List<(long X, long Y, int I)>();
            for (var i = 0; i < Segs.Count; i++)
            {
                var (x0, y0, x1, y1) = Box(i);
                if (x1 < qx0 || x0 > qx1 || y1 < qy0 || y0 > qy1) continue;
                all.Add((Math.Max(x0, qx0), Math.Max(y0, qy0), i));
            }
            all.Sort((p, q) => p.X != q.X ? p.X.CompareTo(q.X) : p.Y != q.Y ? p.Y.CompareTo(q.Y) : p.I.CompareTo(q.I));
            foreach (var t in all) { stamp[t.I] = query; if (visit(t.I)) return true; }
            return false;
        }
        if (_levels.Count != 0 && qx0 <= qx1 && qy0 <= qy1)
        {
            void Take(int i)
            {
                if (stamp[i] == query) return;
                var (x0, y0, x1, y1) = Box(i);
                if (x1 < qx0 || x0 > qx1 || y1 < qy0 || y0 > qy1) return;
                stamp[i] = query; (big ??= []).Add((Math.Max(x0, qx0), Math.Max(y0, qy0), i));
            }
            // wenige grobe Strecken oder sehr großes Rechteck: Liste durchgehen, sonst die groben Zellen jeder benutzten Stufe
            double coarse = 0; foreach (var (Lx, Ly) in _levels) coarse += ((double)(qx1 >> Lx) - (qx0 >> Lx) + 1) * ((double)(qy1 >> Ly) - (qy0 >> Ly) + 1);
            if (coarse >= _bigList.Count) foreach (var i in _bigList) Take(i);
            else
                foreach (var (Lx, Ly) in _levels)
                    for (var x = qx0 >> Lx; x <= qx1 >> Lx; x++)
                        for (var y = qy0 >> Ly; y <= qy1 >> Ly; y++)
                            if (_big.TryGetValue((Lx, Ly, x, y), out var l)) foreach (var i in l) Take(i);
            big?.Sort((p, q) => p.X != q.X ? p.X.CompareTo(q.X) : p.Y != q.Y ? p.Y.CompareTo(q.Y) : p.I.CompareTo(q.I));
        }
        var nb = big?.Count ?? 0; var bp = 0;
        for (var x = qx0; x <= qx1; x++)
            for (var y = qy0; y <= qy1; y++)
            {
                var hasL = _map.TryGetValue(Key(x, y), out var l);
                if (bp < nb && big![bp].X == x && big[bp].Y == y)
                {
                    // grobe Strecken mit dieser ersten Zelle nach Nummer zwischen die Einträge der Zelle einreihen
                    var k = 0; var lc = hasL ? l!.Count : 0;
                    while (k < lc || bp < nb && big[bp].X == x && big[bp].Y == y)
                    {
                        if (bp < nb && big[bp].X == x && big[bp].Y == y && (k >= lc || big[bp].I < l![k])) { if (visit(big[bp++].I)) return true; continue; }
                        var i = l![k++]; if (stamp[i] != query) { stamp[i] = query; if (visit(i)) return true; }
                    }
                    continue;
                }
                if (hasL) foreach (var i in l!) if (stamp[i] != query) { stamp[i] = query; if (visit(i)) return true; }
            }
        return false;
    }

    /// <summary>Gibt es eine Strecke, die dem Punkt näher als <paramref name="d"/> kommt?</summary>
    public bool AnyWithin((double X, double Y) q, double d) => d > 0 && Any(q.X - d, q.Y - d, q.X + d, q.Y + d, i => CncCam.SegDist(q, Segs[i].A, Segs[i].B) < d);

    /// <summary>T-0772: Nummern der Strecken im groben Raster.</summary>
    public IReadOnlyList<int> BigList => _bigList;
    /// <summary>T-0772: Zahl der feinen Zellen eines Rechtecks.</summary>
    public double CellCount(double minX, double minY, double maxX, double maxY) => ((double)C(maxX) - C(minX) + 1) * ((double)C(maxY) - C(minY) + 1);
    /// <summary>T-0772: Strecken des feinen Rasters in den Zellen des Rechtecks und die Strecken aus <paramref name="extra"/> (Zellen der Größe
    /// <paramref name="extraCell"/>, ± 1 Zelle) sowie <paramref name="always"/> – jede höchstens einmal, Reihenfolge ohne Bedeutung.</summary>
    public bool AnyMixed(Cursor cur, double minX, double minY, double maxX, double maxY, Dictionary<(long, long), List<int>> extra, double extraCell, List<int> always, Func<int, bool> hit)
    {
        Next(cur, Segs.Count); var stamp = cur.Stamp; var query = cur.Query;
        long qx0 = C(minX), qx1 = C(maxX), qy0 = C(minY), qy1 = C(maxY);
        for (var x = qx0; x <= qx1; x++)
            for (var y = qy0; y <= qy1; y++)
                if (_map.TryGetValue(Key(x, y), out var l))
                    foreach (var i in l) if (stamp[i] != query) { stamp[i] = query; if (hit(i)) return true; }
        long ex0 = (long)Math.Floor(minX / extraCell) - 1, ex1 = (long)Math.Floor(maxX / extraCell) + 1, ey0 = (long)Math.Floor(minY / extraCell) - 1, ey1 = (long)Math.Floor(maxY / extraCell) + 1;
        for (var x = ex0; x <= ex1; x++)
            for (var y = ey0; y <= ey1; y++)
                if (extra.TryGetValue((x, y), out var l))
                    foreach (var i in l) if (stamp[i] != query) { stamp[i] = query; if (hit(i)) return true; }
        foreach (var i in always) if (stamp[i] != query) { stamp[i] = query; if (hit(i)) return true; }
        return false;
    }
}
