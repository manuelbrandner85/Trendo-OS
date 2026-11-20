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
    public const int MaxEntities = 50_000;
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
        var ents = d.Entities.Where(e => e.Kind != "text" && !hidden.Contains(e.Layer)).ToList();
        if (ents.Count > MaxEntities) throw new CadError($"Die Zeichnung hat {ents.Count.ToString("N0", De)} Elemente – zu viele zum Fräsen (höchstens {MaxEntities.ToString("N0", De)}). Bitte nur die nötigen Teile oder Ebenen exportieren.");
        return CadContours.Build(ents, ChainTol, chord, notes, ct);
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
        for (var i = 0; i < cs.Count; i++)
        {
            if (!cs[i].Closed || dup[i]) continue;
            for (var j = i + 1; j < cs.Count; j++)
            {
                if (!cs[j].Closed || dup[j]) continue;
                ct.ThrowIfCancellationRequested();
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
        // Art je Kontur
        var kinds = new string[cs.Count]; var depth = new int[cs.Count];
        for (var i = 0; i < cs.Count; i++)
        {
            if (!cs[i].Closed) { kinds[i] = "auf"; continue; }
            var probe = cs[i].Pts[0];
            depth[i] = cs.Where((c, j) => j != i && c.Closed && !crossing[j] && !dup[j] && Math.Abs(c.SignedArea) > Math.Abs(cs[i].SignedArea) && c.Contains(probe.X, probe.Y)).Count();
            kinds[i] = s.Mode switch { "aussen" => "aussen", "innen" => "innen", "auf" => "auf", _ => depth[i] % 2 == 0 ? "aussen" : "innen" };
        }
        // Bahnen je Kontur (noch ungeprüft)
        var cand = new List<CamPath>?[cs.Count];
        for (var i = 0; i < cs.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            if (crossing[i] || unsure[i] || dup[i]) continue;
            var c = cs[i];
            if (!c.Closed && s.Mode != "auf") // T-0747-fix2: offene Kontur nur ausdrücklich als Gravur
            {
                warn.Add($"{Name(i)} ausgelassen: offene Kontur (Enden {F(Math.Sqrt(D2(c.Pts[0], c.Pts[^1])), "0.###")} mm auseinander) – auf der Linie gefräst würde ein Teil um den Fräserradius kleiner. Nur in der Fräsart „Auf der Linie (Gravur)“ wird sie gefräst; sonst die Zeichnung schließen.");
                continue;
            }
            if (kinds[i] == "auf") { cand[i] = [new CamPath("auf", [.. c.Pts], c.Closed, i, c.Layer)]; continue; }
            var outside = kinds[i] == "aussen";
            var (simp, dev) = Simplify(c.Pts, Math.Min(SimplifyTol, r / 20));
            var loops = OffsetLoops(simp, (outside ? 1 : -1) * (r + dev), s.Chord, ct);
            List<CamPath> list = [];
            if (outside)
            {
                var main = loops.Where(l => SignedArea(l) > 1e-9).MaxBy(SignedArea);
                if (main is null) { warn.Add($"{Name(i)} ausgelassen: der Versatz nach außen ergab keine Bahn – die Kontur hat keine Fläche oder ist entartet."); continue; }
                loops = [main, .. loops.Where(l => l != main && SignedArea(l) < 0)]; // eingeschlossene Buchten (im Uhrzeigersinn) bleiben
                foreach (var l in loops) { var p = new List<(double X, double Y)>(l); p.Reverse(); list.Add(new CamPath("aussen", p, true, i, c.Layer) { TabsAllowed = l == main }); } // im Uhrzeigersinn = Gleichlauf außen
            }
            else
            {
                loops = loops.Where(l => SignedArea(l) > 1e-9).ToList();
                if (loops.Count == 0) { warn.Add($"{Name(i)}: Innenkontur ist zu klein für den Fräser Ø {F(s.ToolD)} mm – ausgelassen, dort bleibt Material stehen."); continue; }
                foreach (var l in loops) list.Add(new CamPath("innen", l, true, i, c.Layer));
            }
            // T-0747-fix: Engstellen – Stellen der Kontur, an die der Fräser nicht herankommt
            var spots = Unreached(c.Pts, loops, r, ct);
            if (spots.Count > 0)
                warn.Add($"{Name(i)}: Bereich enger als der Fräser Ø {F(s.ToolD)} mm – dort bleibt Material stehen ({string.Join("; ", spots.Take(4).Select(p => $"bei X {F(p.X, "0.#")} Y {F(p.Y, "0.#")}"))}{(spots.Count > 4 ? $" und {spots.Count - 4} weitere Stellen" : "")}). Für diese Stellen einen kleineren Fräser wählen.");
            cand[i] = list;
        }
        // T-0747-fix: jede Bahn gegen ALLE geschlossenen Konturen prüfen (Strecke zu Strecke)
        // T-0747-fix3: ALLE Konturen sind Hindernis – auch offene und ausgelassene (ein offener Umriss ist trotzdem ein Teil)
        var obstacles = BuildGrid(cs.Select((c, i) => ((IReadOnlyList<(double X, double Y)>)c.Pts, c.Closed, i)).Where(t => !dup[t.i]), r); // Doppel nur einmal
        var paths = new List<CamPath>();
        for (var i = 0; i < cs.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            if (cand[i] is not { } list) continue;
            var hit = list.Select(p => Nearest(p.Pts, p.Closed, obstacles, r - Clearance, p.Kind == "auf" ? i : -1, ct)).Where(h => h.Tag >= 0)
                .OrderBy(h => h.Tag == i ? 1 : 0).ThenBy(h => h.D).FirstOrDefault((Tag: -1, D: 0.0));
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
    static (int Tag, double D) Nearest(List<(double X, double Y)> path, bool closed, SegGrid obstacles, double limit, int skip, CancellationToken ct = default)
    {
        var best = (Tag: -1, D: double.MaxValue); var m = closed ? path.Count : path.Count - 1;
        for (var i = 0; i < m; i++)
        {
            if ((i & 1023) == 0) ct.ThrowIfCancellationRequested();
            var a = path[i]; var b = path[(i + 1) % path.Count];
            foreach (var j in obstacles.Near(Math.Min(a.X, b.X) - limit, Math.Min(a.Y, b.Y) - limit, Math.Max(a.X, b.X) + limit, Math.Max(a.Y, b.Y) + limit))
            {
                var (c, d, tag) = obstacles.Segs[j]; if (tag == skip) continue;
                var dd = SegSeg(a, b, c, d);
                if (dd < limit && dd < best.D) best = (tag, dd);
            }
        }
        return best;
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

    static SegGrid BuildGrid(IEnumerable<(IReadOnlyList<(double X, double Y)> Pts, bool Closed, int Tag)> polys, double cell)
    {
        var list = polys.ToList();
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var (pts, _, _) in list) foreach (var (x, y) in pts) { minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); }
        var diag = minX > maxX ? 1 : Math.Sqrt((maxX - minX) * (maxX - minX) + (maxY - minY) * (maxY - minY));
        var g = new SegGrid(Math.Max(cell, diag / 2048));
        foreach (var (pts, closed, tag) in list) for (var i = 0; i < pts.Count - (closed ? 0 : 1); i++) g.Add(pts[i], pts[(i + 1) % pts.Count], tag);
        return g;
    }
}

/// <summary>T-0747-fix: Rasterindex für Strecken – „welche Strecken liegen in diesem Rechteck“ und „gibt es eine Strecke näher als d“, ohne alle
/// Strecken durchzugehen (Fräsbahn-Prüfung, Versatz, Kettenbildung, Wandpaare).</summary>
public sealed class SegGrid(double cell)
{
    readonly double _cell = Math.Max(cell, 1e-9);
    readonly Dictionary<long, List<int>> _map = [];
    int[] _stamp = new int[16]; int _query;
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
    public List<int> Near(double minX, double minY, double maxX, double maxY)
    {
        if (_stamp.Length < Segs.Count) Array.Resize(ref _stamp, Math.Max(Segs.Count, _stamp.Length * 2));
        if (++_query == int.MaxValue) { Array.Clear(_stamp); _query = 1; }
        var res = new List<int>();
        for (var x = C(minX); x <= C(maxX); x++)
            for (var y = C(minY); y <= C(maxY); y++)
                if (_map.TryGetValue(Key(x, y), out var l))
                    foreach (var i in l) if (_stamp[i] != _query) { _stamp[i] = _query; res.Add(i); }
        return res;
    }

    /// <summary>T-0747-fix2: wie <see cref="Near"/>, aber ohne Liste – bricht ab, sobald <paramref name="hit"/> wahr ist.</summary>
    public bool Any(double minX, double minY, double maxX, double maxY, Func<int, bool> hit)
    {
        if (_stamp.Length < Segs.Count) Array.Resize(ref _stamp, Math.Max(Segs.Count, _stamp.Length * 2));
        if (++_query == int.MaxValue) { Array.Clear(_stamp); _query = 1; }
        for (var x = C(minX); x <= C(maxX); x++)
            for (var y = C(minY); y <= C(maxY); y++)
                if (_map.TryGetValue(Key(x, y), out var l))
                    foreach (var i in l) if (_stamp[i] != _query) { _stamp[i] = _query; if (hit(i)) return true; }
        return false;
    }

    /// <summary>Gibt es eine Strecke, die dem Punkt näher als <paramref name="d"/> kommt?</summary>
    public bool AnyWithin((double X, double Y) q, double d) => d > 0 && Any(q.X - d, q.Y - d, q.X + d, q.Y + d, i => CncCam.SegDist(q, Segs[i].A, Segs[i].B) < d);
}
