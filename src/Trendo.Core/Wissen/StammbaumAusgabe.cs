using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Trendo.Core.Office;
using static Trendo.Core.Wissen.TimelineKit;

namespace Trendo.Core.Wissen;

/// <summary>T-0788: Prüfungen, Baum-Bilder (Vorfahren, Nachkommen), Bericht, Tabelle und Zeilen für die KI.</summary>
public static partial class FamilyKit
{
    // ---------------- Prüfungen ----------------

    static string Y(double v) => double.IsFinite(v) ? Math.Floor(v).ToString(CultureInfo.InvariantCulture) : "?";

    /// <summary>Widersprüche und Auffälligkeiten. Fehler = sicher unmöglich (auch bei ungefähren Daten mit ±10 Jahren Spielraum), Hinweis = ungewöhnlich.</summary>
    public static List<FamIssue> Check(FamilyTree t)
    {
        using var ix = t.Indexed(); // T-0788 (Gegenprüfung G2): Nachschlagen über Tabellen statt Listen
        var res = new List<FamIssue>();
        void E(string? p, string? f, string text) => res.Add(new(true, p, f, text));
        void H(string? p, string? f, string text) => res.Add(new(false, p, f, text));
        foreach (var p in t.Persons)
        {
            var b = p.Ev("BIRT")?.When; var c = p.Ev("CHR", "BAPM")?.When; var d = p.Ev("DEAT")?.When; var bu = p.Ev("BURI", "CREM")?.When;
            var born = b is { Comparable: true } ? b : c is { Comparable: true } ? c : null;
            var died = d is { Comparable: true } ? d : bu is { Comparable: true } ? bu : null;
            if (p.Given.Length == 0 && p.Surname.Length == 0) H(p.Id, null, $"{p.Id}: Person ohne Namen");
            foreach (var e in p.Events)
            {
                var w = e.When;
                if (w.Kind == GenDateKind.Unreadable) H(p.Id, null, $"{p.Name}: {EventName(e.Tag)} „{e.Date}“ ist kein lesbares Datum");
                else if (w.Reversed) E(p.Id, null, $"{p.Name}: {EventName(e.Tag)} „{w.Show()}“ – Spanne verkehrt (Ende vor dem Anfang)");
                else if (w.NonStandard) H(p.Id, null, $"{p.Name}: {EventName(e.Tag)} „{e.Date}“ nicht in GEDCOM-Schreibweise (gelesen als {w.Show()})");
            }
            if (born is not null && died is not null && born.Lo > died.Hi) E(p.Id, null, $"{p.Name}: Geburt ({born.Show()}) nach dem Tod ({died.Show()})");
            if (b is { Comparable: true } && c is { Comparable: true } && c.Hi < b.Lo) E(p.Id, null, $"{p.Name}: Taufe ({c.Show()}) vor der Geburt ({b.Show()})");
            if (d is { Comparable: true } && bu is { Comparable: true } && bu.Hi < d.Lo) E(p.Id, null, $"{p.Name}: Begräbnis ({bu.Show()}) vor dem Tod ({d.Show()})");
            if (born is not null && died is not null && died.Lo - born.Hi > 120) H(p.Id, null, $"{p.Name}: über 120 Jahre alt geworden ({born.Show()} – {died.Show()})");
            foreach (var e in p.Events.Where(e => e.Tag is not ("DEAT" or "BURI" or "CREM" or "PROB" or "WILL")))
                if (died is not null && e.When is { Comparable: true } w && w.Lo > died.Hi + 1) H(p.Id, null, $"{p.Name}: {EventName(e.Tag)} ({w.Show()}) mehr als ein Jahr nach dem Tod");
        }
        foreach (var f in t.Families)
        {
            var hu = t.P(f.Husband); var wi = t.P(f.Wife);
            if (hu is null && wi is null && f.Children.Count == 0) H(null, f.Id, $"Familie {f.Id} ist leer");
            if (hu?.Sex == "F" && wi?.Sex != "F") H(hu.Id, f.Id, $"{hu.Name} steht als Ehemann in {f.Id}, ist aber weiblich eingetragen");
            var m = f.Marriage?.When;
            foreach (var sp in new[] { hu, wi }.OfType<FamPerson>())
            {
                var sb = sp.Birth?.When; var sd = sp.Death?.When;
                if (m is { Comparable: true } && sb is { Comparable: true } && m.Hi < sb.Lo) E(sp.Id, f.Id, $"{sp.Name}: Heirat ({m.Show()}) vor der eigenen Geburt ({sb.Show()})");
                else if (m is { Comparable: true } && sb is { Comparable: true } && m.Hi - sb.Lo < 13) H(sp.Id, f.Id, $"{sp.Name}: bei der Heirat ({m.Show()}) jünger als 13 Jahre");
                if (m is { Comparable: true } && sd is { Comparable: true } && m.Lo > sd.Hi) E(sp.Id, f.Id, $"{sp.Name}: Heirat ({m.Show()}) nach dem eigenen Tod ({sd.Show()})");
            }
            foreach (var cid in f.Children)
            {
                if (t.P(cid) is not { } ch) continue;
                var cb = ch.Birth?.When; if (cb is not { Comparable: true }) continue;
                foreach (var (par, mother) in new[] { (hu, false), (wi, true) })
                {
                    if (par is null) continue;
                    var pb = par.Birth?.When; var pd = par.Death?.When;
                    var role = mother ? "Mutter" : "Vater";
                    if (pb is { Comparable: true } && pb.Lo > cb.Hi) E(ch.Id, f.Id, $"{ch.Name}: {role} {par.Name} ist jünger als das Kind ({pb.Show()} / Kind {cb.Show()})");
                    else if (pb is { Comparable: true } && cb.Hi - pb.Lo < (mother ? 12 : 13)) H(ch.Id, f.Id, $"{ch.Name}: {role} {par.Name} war bei der Geburt jünger als {(mother ? 12 : 13)} Jahre");
                    else if (pb is { Comparable: true } && cb.Lo - pb.Hi > (mother ? 55 : 80)) H(ch.Id, f.Id, $"{ch.Name}: {role} {par.Name} war bei der Geburt älter als {(mother ? 55 : 80)} Jahre");
                    if (pd is { Comparable: true } && cb.Lo > pd.Hi + (mother ? 0 : 1)) E(ch.Id, f.Id, $"{ch.Name}: geboren ({cb.Show()}) nach dem Tod {(mother ? "der Mutter" : "des Vaters")} ({pd.Show()})");
                }
            }
        }
        // Person ist ihr eigener Vorfahre: einmal über alle Personen starke Zusammenhangskomponenten der Eltern-Kanten (iterativ, linear)
        foreach (var id in CycleMembers(t)) if (t.P(id) is { } p) E(p.Id, null, $"{p.Name} ist ihr/sein eigener Vorfahre (Kreis in den Verknüpfungen)");
        return res;
    }

    /// <summary>T-0788 (Gegenprüfung G2): alle Personen, die auf einem Kreis „Kind → Elternteil → … → Kind“ liegen (Tarjan, ohne Rekursion).</summary>
    public static HashSet<string> CycleMembers(FamilyTree t)
    {
        using var ix = t.Indexed();
        IEnumerable<string> Parents(string id) => t.ParentFamilies(id).SelectMany(f => new[] { f.Husband, f.Wife }).OfType<string>().Where(x => t.P(x) is not null);
        var index = new Dictionary<string, int>(StringComparer.Ordinal); var low = new Dictionary<string, int>(StringComparer.Ordinal);
        var onStack = new HashSet<string>(StringComparer.Ordinal); var stack = new Stack<string>(); var res = new HashSet<string>(StringComparer.Ordinal);
        var next = 0;
        foreach (var start in t.Persons.Select(p => p.Id))
        {
            if (index.ContainsKey(start)) continue;
            var work = new Stack<(string Node, IEnumerator<string> Edges)>();
            void Open(string v) { index[v] = low[v] = next++; stack.Push(v); onStack.Add(v); work.Push((v, Parents(v).ToList().GetEnumerator())); }
            Open(start);
            while (work.Count > 0)
            {
                var (v, edges) = work.Peek();
                if (edges.MoveNext())
                {
                    var w = edges.Current;
                    if (!index.ContainsKey(w)) Open(w);
                    else if (onStack.Contains(w)) low[v] = Math.Min(low[v], index[w]);
                    continue;
                }
                work.Pop();
                if (work.Count > 0) { var u = work.Peek().Node; low[u] = Math.Min(low[u], low[v]); }
                if (low[v] != index[v]) continue;
                var comp = new List<string>(); string x;
                do { x = stack.Pop(); onStack.Remove(x); comp.Add(x); } while (x != v);
                if (comp.Count > 1 || Parents(v).Contains(v)) res.UnionWith(comp);
            }
        }
        return res;
    }

    // ---------------- Baum-Bilder ----------------

    public const double BoxW = 214, BoxH = 64, GapX = 46, GapY = 46;

    sealed record Colors(string Bg, string Box, string Border, string Sel, string Ink, string Muted, string Line, string M, string F);
    static Colors Pal(bool paper) => paper
        ? new("#FFFFFF", "#FBF7EC", "#C9B27A", "#B8860B", "#1F1A12", "#6B6253", "#9C8B66", "#5E8C7A", "#B06A7E")
        : new("#101014", "#1B1A17", "#4A3F22", "#D4A017", "#F2EEE3", "#A8A29E", "#6B5A2E", "#6FA894", "#C98A9C");

    static string Cut(string s, double width, double size)
    {
        if (TextW(s, size) <= width) return s;
        var n = Math.Max(1, (int)(width / (size * 0.56)) - 1);
        return s.Length <= n ? s : s[..n] + "…";
    }

    static string Life(FamPerson p, bool full)
    {
        var b = p.Birth; var d = p.Death;
        var s1 = b is null ? "" : "* " + b.When.Show(!full) + (full && b.Place.Length > 0 ? ", " + b.Place : "");
        var s2 = d is null ? "" : "† " + d.When.Show(!full) + (full && d.Place.Length > 0 ? ", " + d.Place : "");
        return full ? s1 + (s1.Length > 0 && s2.Length > 0 ? "  " : "") + s2 : (s1 + (s2.Length > 0 ? "  " + s2 : "")).Trim();
    }

    static void Box(List<Prim> prims, Dictionary<string, (double, double, double, double)> hits, Colors c, FamPerson p, double x, double y, string? selected, string? extra)
    {
        var sel = p.Id == selected;
        prims.Add(new PRect(x, y, BoxW, BoxH, c.Box, sel ? c.Sel : c.Border, 6, false, 1, p.Id));
        prims.Add(new PRect(x, y + 6, 4, BoxH - 12, p.Sex == "M" ? c.M : p.Sex == "F" ? c.F : c.Muted, null, 2, false, 1, p.Id));
        prims.Add(new PText(x + 12, y + 20, Cut(p.Name, BoxW - 20, 13), 13, c.Ink, true, -1, p.Id));
        var life = Life(p, true); if (TextW(life, 10.5) > BoxW - 20) life = Life(p, false);
        if (life.Length > 0) prims.Add(new PText(x + 12, y + 38, Cut(life, BoxW - 20, 10.5), 10.5, c.Muted, false, -1, p.Id));
        if (extra is { Length: > 0 }) prims.Add(new PText(x + 12, y + 55, Cut(extra, BoxW - 20, 10.5), 10.5, c.Muted, false, -1, p.Id));
        hits[p.Id] = (x, y, BoxW, BoxH);
    }

    static Scene Frame(string title, string subtitle, double w, double h, Colors c, List<Prim> body, Dictionary<string, (double, double, double, double)> hits)
    {
        var prims = new List<Prim> { new PRect(0, 0, w, h, c.Bg), new PText(20, 30, title, 19, c.Ink, true, -1), new PText(20, 50, subtitle, 11, c.Muted, false, -1) };
        prims.AddRange(body);
        return new Scene(w, h, 0, 0, prims, hits);
    }

    /// <summary>Vorfahren (Ahnentafel): die Person links, Eltern, Großeltern … nach rechts. Fehlende Eltern als gestrichelter Kasten „+ Vater“/„+ Mutter“
    /// (Treffer „+vater:&lt;Kind&gt;“ / „+mutter:&lt;Kind&gt;“, nicht in Ausgaben).</summary>
    public static Scene Ancestors(FamilyTree t, string? rootId, int gens, string? selected = null, bool paper = false)
    {
        using var ix = t.Indexed();
        gens = Math.Clamp(gens, 2, 6); var c = Pal(paper);
        var prims = new List<Prim>(); var hits = new Dictionary<string, (double, double, double, double)>();
        const double top = 72, rowH = BoxH + 14;
        var slots = 1 << (gens - 1);
        var w = 40 + gens * BoxW + (gens - 1) * GapX; var h = top + slots * rowH + 24;
        var root = t.P(rootId) ?? t.Persons.FirstOrDefault();
        if (root is null) return Frame(t.Title.Length > 0 ? t.Title : "Stammbaum", "Noch keine Personen", Math.Max(w, 600), 200, c, prims, hits);
        void Place(FamPerson? p, int g, int k, FamPerson? child, double childCy, bool mother, HashSet<string> path)
        {
            var cy = top + (k + 0.5) * (1 << (gens - 1 - g)) * rowH; var x = 20 + g * (BoxW + GapX); var y = cy - BoxH / 2;
            if (child is not null)
            {
                var x0 = x - GapX; var mid = x0 + GapX / 2;
                if (p is not null || !paper) prims.AddRange([new PLine(x0, childCy, mid, childCy, c.Line, 1.4), new PLine(mid, childCy, mid, cy, c.Line, 1.4), new PLine(mid, cy, x, cy, c.Line, 1.4)]);
            }
            if (p is null)
            {
                if (paper || child is null) return;
                var id = (mother ? "+mutter:" : "+vater:") + child.Id;
                prims.Add(new PRect(x, y + 12, BoxW, BoxH - 24, c.Bg, c.Border, 6, true, 1, id));
                prims.Add(new PText(x + BoxW / 2, cy + 4, mother ? "+ Mutter" : "+ Vater", 12, c.Muted, false, 0, id));
                hits[id] = (x, y + 12, BoxW, BoxH - 24);
                return;
            }
            if (!path.Add(p.Id)) { prims.Add(new PText(x, cy, "↺ " + Cut(p.Name, BoxW, 11), 11, c.Muted, false, -1)); return; }
            Box(prims, hits, c, p, x, y, selected, g == 0 ? $"{t.Spouses(p.Id).Count()} Partner · {t.Children(p.Id).Count()} Kinder" : null);
            if (g + 1 < gens)
            {
                var (fa, mo) = t.Parents(p.Id);
                Place(fa, g + 1, 2 * k, p, cy, false, [.. path]);
                Place(mo, g + 1, 2 * k + 1, p, cy, true, [.. path]);
            }
        }
        Place(root, 0, 0, null, 0, false, []);
        return Frame(t.Title.Length > 0 ? t.Title : "Stammbaum", $"Vorfahren von {root.Name} · {gens} Generationen", w, h, c, prims, hits);
    }

    /// <summary>Nachkommen: die Person oben, darunter Kinder, Enkel … (je Generation eine Reihe), Partner im Kasten („⚭ …“).</summary>
    public static Scene Descendants(FamilyTree t, string? rootId, int gens, string? selected = null, bool paper = false)
    {
        using var ix = t.Indexed();
        gens = Math.Clamp(gens, 2, 6); var c = Pal(paper);
        var prims = new List<Prim>(); var hits = new Dictionary<string, (double, double, double, double)>();
        const double top = 72; const int maxBoxes = 800;
        var root = t.P(rootId) ?? t.Persons.FirstOrDefault();
        if (root is null) return Frame(t.Title.Length > 0 ? t.Title : "Stammbaum", "Noch keine Personen", 600, 200, c, prims, hits);
        var widths = new Dictionary<string, double>(); var count = 0; var cut = false;
        List<FamPerson> Kids(FamPerson p) => [.. t.Children(p.Id)];
        double Width(FamPerson p, int g, HashSet<string> path)
        {
            if (count++ > maxBoxes) { cut = true; return BoxW; }
            var kids = g + 1 < gens ? Kids(p).Where(k => !path.Contains(k.Id)).ToList() : [];
            var sum = 0.0;
            foreach (var k in kids) sum += Width(k, g + 1, [.. path, k.Id]) + GapX;
            var wv = Math.Max(BoxW, sum - (kids.Count > 0 ? GapX : 0));
            widths[p.Id + "@" + g] = wv; return wv;
        }
        var total = Width(root, 0, [root.Id]);
        var maxG = 0;
        void Place(FamPerson p, int g, double left, HashSet<string> path, double? parentX, double? parentBottom)
        {
            if (!widths.TryGetValue(p.Id + "@" + g, out var wv)) return;
            maxG = Math.Max(maxG, g);
            var x = left + (wv - BoxW) / 2; var y = top + g * (BoxH + GapY);
            if (parentX is { } px && parentBottom is { } pb)
            {
                var mid = pb + GapY / 2;
                prims.AddRange([new PLine(px, pb, px, mid, c.Line, 1.4), new PLine(px, mid, x + BoxW / 2, mid, c.Line, 1.4), new PLine(x + BoxW / 2, mid, x + BoxW / 2, y, c.Line, 1.4)]);
            }
            var sp = t.Spouses(p.Id).Select(s => s.Name).ToList();
            Box(prims, hits, c, p, x, y, selected, sp.Count > 0 ? "⚭ " + string.Join(", ", sp) : null);
            if (g + 1 >= gens) return;
            var kids = Kids(p).Where(k => !path.Contains(k.Id)).ToList();
            var sum = kids.Sum(k => widths.GetValueOrDefault(k.Id + "@" + (g + 1), BoxW)) + Math.Max(0, kids.Count - 1) * GapX;
            var cx = left + (wv - sum) / 2;
            foreach (var k in kids)
            {
                Place(k, g + 1, cx, [.. path, k.Id], x + BoxW / 2, y + BoxH);
                cx += widths.GetValueOrDefault(k.Id + "@" + (g + 1), BoxW) + GapX;
            }
        }
        Place(root, 0, 20, [root.Id], null, null);
        var w = total + 40; var h = top + (maxG + 1) * (BoxH + GapY) + 10;
        if (cut) prims.Add(new PText(20, h - 8, "… gekürzt (sehr viele Nachkommen) – weniger Generationen wählen", 11, c.Muted, false, -1));
        return Frame(t.Title.Length > 0 ? t.Title : "Stammbaum", $"Nachkommen von {root.Name} · {gens} Generationen", Math.Max(w, 600), h, c, prims, hits);
    }

    // ---------------- Bericht ----------------

    static string Place(FamEvent e) => e.Place.Length > 0 ? " in " + e.Place : "";

    /// <summary>Bericht wie ein Personen- und Familienbuch: Übersicht, je Person Lebensdaten, Ereignisse, Eltern, Partner, Kinder, Quellen und Notizen;
    /// dann Prüfung, Quellen- und Ortsverzeichnis. Lebende werden bei <see cref="FamilyTree.HideLiving"/> gekürzt.</summary>
    public static TextDoc BuildReport(FamilyTree src, DateTime now)
    {
        var t = src.HideLiving ? Private(src, now) : src;
        using var ix = t.Indexed(); using var ix2 = src.Indexed();
        var doc = new TextDoc { Blocks = [] };
        doc.Page.PageNumbers = true;
        doc.Page.Footer = "Stammbaum – " + (t.Title.Length > 0 ? t.Title : "Bericht");
        doc.Blocks.Add(new Para(t.Title.Length > 0 ? t.Title : "Stammbaum", ParaStyle.Title));
        doc.Blocks.Add(new Para($"Personen- und Familienbericht · Stand {now:dd.MM.yyyy}", ParaStyle.Subtitle));
        var living = src.Persons.Count(p => IsLiving(src, p, now));
        doc.Blocks.Add(new Para($"{t.Persons.Count} Personen, {t.Families.Count} Familien, {t.Sources.Count} Quellen." + (src.HideLiving && living > 0 ? $" {living} vermutlich lebende Personen sind ausgeblendet (nur „Lebende Person“ und Nachname)." : "")));
        var de = StringComparer.Create(CultureInfo.GetCultureInfo("de-DE"), true);
        var order = t.Persons.OrderBy(p => p.Surname, de).ThenBy(p => p.Given, de).ThenBy(p => p.Birth?.When.Sort ?? double.MaxValue).ToList();
        var srcNo = t.Sources.Select((s, i) => (s.Id, i + 1)).ToDictionary(x => x.Id, x => x.Item2);
        string CiteText(IEnumerable<FamCite> cs) => string.Join(", ", cs.Select(c => srcNo.TryGetValue(c.Source, out var n) ? $"[{n}]" + (c.Page.Length > 0 ? " " + c.Page : "") : c.Source));
        doc.Blocks.Add(new Para("Personen", ParaStyle.Heading1));
        foreach (var p in order)
        {
            doc.Blocks.Add(new Para(p.Name + $" ({SexName(p.Sex)})", ParaStyle.Heading2));
            foreach (var e in p.Events)
            {
                var line = $"{(e.Type.Length > 0 ? e.Type : EventName(e.Tag))}: " + string.Join(" ", new[] { e.Value, e.When.Show() }.Where(s => s.Length > 0)) + Place(e);
                if (e.Cites.Count > 0) line += " – Quelle " + CiteText(e.Cites);
                doc.Blocks.Add(new Para(line) { List = ListKind.Bullet });
            }
            var (fa, mo) = t.Parents(p.Id);
            if (fa is not null || mo is not null) doc.Blocks.Add(new Para("Eltern: " + string.Join(" und ", new[] { fa?.Name, mo?.Name }.OfType<string>())) { List = ListKind.Bullet });
            foreach (var f in t.OwnFamilies(p.Id))
            {
                var other = t.P(f.Husband == p.Id ? f.Wife : f.Husband);
                var m = f.Marriage;
                var line = "Partner: " + (other?.Name ?? "unbekannt") + (m is null ? "" : $", Heirat {m.When.Show()}{Place(m)}".TrimEnd(','));
                var kids = f.Children.Select(t.P).OfType<FamPerson>().Select(k => k.Name).ToList();
                if (kids.Count > 0) line += $"; Kinder: {string.Join(", ", kids)}";
                doc.Blocks.Add(new Para(line) { List = ListKind.Bullet });
            }
            if (p.Cites.Count > 0) doc.Blocks.Add(new Para("Quellen: " + CiteText(p.Cites)) { List = ListKind.Bullet });
            if (p.Note.Length > 0) doc.Blocks.Add(new Para(p.Note.Replace('\n', ' ')) { IndentCm = 0.6 });
        }
        var issues = Check(t);
        doc.Blocks.Add(new Para("Prüfung", ParaStyle.Heading1));
        if (issues.Count == 0) doc.Blocks.Add(new Para("Keine Widersprüche gefunden."));
        foreach (var i in issues) doc.Blocks.Add(new Para((i.Error ? "Fehler: " : "Hinweis: ") + i.Text) { List = ListKind.Bullet });
        if (t.Sources.Count > 0)
        {
            doc.Blocks.Add(new Para("Quellen", ParaStyle.Heading1));
            foreach (var s in t.Sources) doc.Blocks.Add(new Para($"[{srcNo[s.Id]}] " + string.Join(". ", new[] { s.Author, s.Title, s.Publication }.Where(x => x.Length > 0))) { IndentCm = 1, FirstLineCm = -1 });
        }
        var places = Places(t);
        if (places.Count > 0)
        {
            doc.Blocks.Add(new Para("Orte", ParaStyle.Heading1));
            var tab = new Table { Rows = [["Ort", "Ereignisse", "Koordinaten"]] };
            foreach (var pl in places) tab.Rows.Add([pl.Name, pl.Count.ToString(CultureInfo.InvariantCulture), pl.Lat.Length > 0 ? pl.Lat + " " + pl.Long : ""]);
            tab.Widths = [0.6, 0.15, 0.25];
            doc.Blocks.Add(tab);
        }
        // T-0788: Ein DOCX, das mit einer Tabelle endet, bringt LibreOffice beim Einlesen zum Hängen (Beleg reports/T-0788) – daher immer ein Absatz am Ende
        doc.Blocks.Add(new Para($"Stand {now:dd.MM.yyyy} · {t.Persons.Count} Personen") { SpaceBeforePt = 12 });
        return doc;
    }

    public sealed record PlaceInfo(string Name, int Count, string Lat, string Long);

    /// <summary>Alle Orte mit Zahl der Ereignisse und (ersten) Koordinaten.</summary>
    public static List<PlaceInfo> Places(FamilyTree t)
    {
        var de = StringComparer.Create(CultureInfo.GetCultureInfo("de-DE"), true);
        return t.Persons.SelectMany(p => p.Events).Concat(t.Families.SelectMany(f => f.Events)).Where(e => e.Place.Trim().Length > 0)
            .GroupBy(e => e.Place.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new PlaceInfo(g.First().Place.Trim(), g.Count(), g.FirstOrDefault(e => e.Lat.Length > 0)?.Lat ?? "", g.FirstOrDefault(e => e.Lat.Length > 0)?.Long ?? ""))
            .OrderBy(p => p.Name, de).ToList();
    }

    // ---------------- Tabelle ----------------

    public static readonly string[] CsvHeader = ["Kennung", "Vorname", "Nachname", "Geschlecht", "Geburt", "Geburtsort", "Tod", "Sterbeort", "Vater", "Mutter", "Partner"];

    /// <summary>T-0788 (Gegenprüfung G8): Zellen, die mit = + - @ Tab oder Zeilenumbruch beginnen, bekommen ein „'“ davor – sonst rechnet ein Tabellenprogramm sie als Formel.</summary>
    static string Csv(string s)
    {
        if (s.Length > 0 && s[0] is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n') s = "'" + s;
        return s.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }

    /// <summary>Personenliste (Semikolon, UTF-8) – öffnet in Tabellenprogrammen; Lebende bei <see cref="FamilyTree.HideLiving"/> gekürzt.</summary>
    public static string ToCsv(FamilyTree src, DateTime now)
    {
        var t = src.HideLiving ? Private(src, now) : src;
        using var ix = t.Indexed();
        var sb = new StringBuilder(string.Join(";", CsvHeader)).Append("\r\n");
        foreach (var p in t.Persons)
        {
            var (fa, mo) = t.Parents(p.Id);
            sb.Append(string.Join(";", new[] { p.Id, p.Given, p.Surname, SexName(p.Sex), p.Birth?.When.Show() ?? "", p.Birth?.Place ?? "", p.Death?.When.Show() ?? "", p.Death?.Place ?? "",
                fa?.Name ?? "", mo?.Name ?? "", string.Join(", ", t.Spouses(p.Id).Select(s => s.Name)) }.Select(Csv))).Append("\r\n");
        }
        return sb.ToString();
    }

    // ---------------- Zeilen (Eingabe und KI) ----------------

    public sealed record LinePerson(string Given, string Surname, string Sex, string Birth, string BirthPlace, string Death, string DeathPlace, string Father, string Mother);
    public sealed record LineMarriage(string A, string B, string Date, string Place);

    public static (string Given, string Surname) NameOf(string full)
    {
        var s = full.Trim();
        if (s.Contains('/')) return SplitName(s);
        var i = s.LastIndexOf(' ');
        return i < 0 ? (s, "") : (s[..i].Trim(), s[(i + 1)..].Trim());
    }

    static string SexOf(string s) => s.Trim().ToLowerInvariant() switch { "m" or "männlich" or "mann" or "male" => "M", "w" or "f" or "weiblich" or "frau" or "female" => "F", _ => "U" };

    /// <summary>Zeilen „Name | Geschlecht | Geburt | Geburtsort | Tod | Sterbeort | Vater | Mutter“ und „Ehe: Name + Name | Datum | Ort“.
    /// Daten werden geprüft (deutsch oder GEDCOM); Zeilen mit unlesbarem Datum oder ohne Namen kommen in die Problemliste – nichts wird geraten.</summary>
    public static (List<LinePerson> Persons, List<LineMarriage> Marriages, List<string> Problems) ParseLines(string text)
    {
        var ps = new List<LinePerson>(); var ms = new List<LineMarriage>(); var bad = new List<string>();
        foreach (var raw in (text ?? "").Split('\n'))
        {
            var line = raw.Trim().TrimStart('-', '*', '•').Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var cells = line.Split('|').Select(x => x.Trim()).ToList();
            while (cells.Count < 8) cells.Add("");
            string D(string s) { try { return GenDate.FromInput(s.Trim('*', '†', ' ')); } catch (ArgumentException) { throw new FormatException(s); } }
            try
            {
                if (cells[0].StartsWith("Ehe:", StringComparison.OrdinalIgnoreCase) || cells[0].StartsWith("Heirat:", StringComparison.OrdinalIgnoreCase))
                {
                    var pair = cells[0][(cells[0].IndexOf(':') + 1)..].Split(['+', '&', '⚭'], 2, StringSplitOptions.TrimEntries);
                    if (pair.Length < 2 || pair[0].Length == 0 || pair[1].Length == 0) { bad.Add(line); continue; }
                    ms.Add(new(pair[0], pair[1], D(cells[1]), cells[2]));
                    continue;
                }
                if (cells[0].Length == 0) { bad.Add(line); continue; }
                if (cells[0].Equals("Name", StringComparison.OrdinalIgnoreCase)) continue; // Kopfzeile
                var (g, s) = NameOf(cells[0]);
                ps.Add(new(g, s, SexOf(cells[1]), D(cells[2]), cells[3], D(cells[4]), cells[5], cells[6], cells[7]));
            }
            catch (FormatException) { bad.Add(line); }
        }
        return (ps, ms, bad);
    }

    static bool SameName(FamPerson p, string full) { var (g, s) = NameOf(full); return string.Equals(p.Given, g, StringComparison.OrdinalIgnoreCase) && string.Equals(p.Surname, s, StringComparison.OrdinalIgnoreCase) || string.Equals(p.Name, full.Trim(), StringComparison.OrdinalIgnoreCase); }

    /// <summary>Zeilen in den Stammbaum übernehmen. Vorhandene Personen (gleicher Name) werden nicht verändert – nur neue Personen und neue Verknüpfungen entstehen.
    /// Rückgabe: Zahl neuer Personen, neuer Verknüpfungen.</summary>
    public static (int Persons, int Links) AddLines(FamilyTree t, IReadOnlyList<LinePerson> ps, IReadOnlyList<LineMarriage> ms)
    {
        var addedP = 0; var addedL = 0;
        var created = new HashSet<string>(); // in diesem Aufruf neu angelegt (z. B. erst als Elternteil genannt) – deren Angaben dürfen noch ergänzt werden
        FamPerson Find(string full, string sexHint = "U")
        {
            var hit = t.Persons.FirstOrDefault(p => SameName(p, full));
            if (hit is not null) return hit;
            var (g, s) = NameOf(full);
            var np = new FamPerson { Id = t.NewId("I"), Sex = sexHint }; SetName(np, g, s);
            t.Persons.Add(np); addedP++; created.Add(np.Id); return np;
        }
        FamFamily Couple(FamPerson? a, FamPerson? b)
        {
            var f = t.Families.FirstOrDefault(x => (a is null || x.Husband == a.Id || x.Wife == a.Id) && (b is null || x.Husband == b.Id || x.Wife == b.Id) && (a is not null || b is not null));
            if (f is not null) return f;
            f = new FamFamily { Id = t.NewId("F") };
            foreach (var x in new[] { a, b }.OfType<FamPerson>()) { if (x.Sex != "F" && f.Husband is null) f.Husband = x.Id; else if (f.Wife is null) f.Wife = x.Id; else f.Husband ??= x.Id; }
            t.Families.Add(f); addedL++; return f;
        }
        foreach (var lp in ps)
        {
            var full = (lp.Given + " " + lp.Surname).Trim();
            var p = Find(full, lp.Sex);
            if (created.Contains(p.Id))
            {
                if (lp.Sex != "U") p.Sex = lp.Sex;
                if ((lp.Birth.Length > 0 || lp.BirthPlace.Length > 0) && p.Ev("BIRT") is null) p.Events.Insert(0, new FamEvent { Tag = "BIRT", Date = lp.Birth, Place = lp.BirthPlace });
                if ((lp.Death.Length > 0 || lp.DeathPlace.Length > 0) && p.Ev("DEAT") is null) p.Events.Add(new FamEvent { Tag = "DEAT", Date = lp.Death, Place = lp.DeathPlace });
            }
            if (lp.Father.Length > 0 || lp.Mother.Length > 0)
            {
                var fa = lp.Father.Length > 0 ? Find(lp.Father, "M") : null; var mo = lp.Mother.Length > 0 ? Find(lp.Mother, "F") : null;
                var f = Couple(fa, mo);
                if (fa is not null && f.Husband is null && f.Wife != fa.Id) f.Husband = fa.Id;
                if (mo is not null && f.Wife is null && f.Husband != mo.Id) f.Wife = mo.Id;
                if (!f.Children.Contains(p.Id) && !t.ParentFamilies(p.Id).Any()) { f.Children.Add(p.Id); addedL++; }
            }
        }
        foreach (var m in ms)
        {
            var a = Find(m.A); var b = Find(m.B);
            var f = Couple(a, b);
            if (f.Marriage is null && (m.Date.Length > 0 || m.Place.Length > 0)) { f.Events.Add(new FamEvent { Tag = "MARR", Date = m.Date, Place = m.Place }); addedL++; }
        }
        return (addedP, addedL);
    }

    /// <summary>T-0788: Prüfung nach jeder KI-Änderung (ProgramSpec.KiCheck): vorhandene Personen, Familien und Quellen dürfen sich nicht ändern –
    /// die KI darf nur neue Personen, neue Familien und neue Kinder-Verknüpfungen hinzufügen; jedes neue Datum muss lesbar sein.</summary>
    public static string? KiProblem(JsonObject before, JsonObject after)
    {
        var a = FromJson(before); var b = FromJson(after);
        // T-0788 (Gegenprüfung G6): doppelte Kennungen, Datensätze, Kopf, Titel und Mitte – nie mit der ersten Kennung urteilen und die zweite durchlassen
        if (b.Persons.GroupBy(p => p.Id).Any(g => g.Count() > 1) || b.Families.GroupBy(f => f.Id).Any(g => g.Count() > 1) || b.Sources.GroupBy(x => x.Id).Any(g => g.Count() > 1)
            || b.Persons.Select(p => p.Id).Intersect(b.Families.Select(f => f.Id)).Any())
            return "Die KI hat eine Kennung doppelt vergeben – nicht übernommen.";
        if (GedNode.ListJson(a.Records).ToJsonString() != GedNode.ListJson(b.Records).ToJsonString() || GedNode.ListJson(a.Head).ToJsonString() != GedNode.ListJson(b.Head).ToJsonString())
            return "Die KI darf weitere Datensätze und den Dateikopf nicht ändern – nicht übernommen.";
        if (a.Title != b.Title || a.Root != b.Root || a.Generations != b.Generations) return "Die KI darf Titel, Mitte und Generationen des Stammbaums nicht ändern – nicht übernommen.";
        if (b.Persons.Any(p => a.P(p.Id) is null && p.Given.StartsWith("Lebende Person", StringComparison.OrdinalIgnoreCase)))
            return "Die KI hat eine ausgeblendete „Lebende Person“ als neue Person angelegt – nicht übernommen.";
        if (CycleMembers(b).Except(CycleMembers(a)).FirstOrDefault() is { } loop)
            return $"Die KI hat einen Kreis in den Verknüpfungen erzeugt ({b.P(loop)?.Name ?? loop} wäre ihr/sein eigener Vorfahre) – nicht übernommen.";
        if (ToJsonText(a.Sources) != ToJsonText(b.Sources)) return "Die KI darf Quellen nicht ändern – nur du legst Quellen an. Nichts übernommen.";
        if (a.HideLiving != b.HideLiving) return "Die KI darf den Schutz lebender Personen nicht umstellen. Nichts übernommen.";
        using var ixa = a.Indexed(); using var ixb = b.Indexed();
        foreach (var p in a.Persons)
        {
            var q = b.P(p.Id);
            if (q is null) return $"Die KI hat „{p.Name}“ entfernt – nicht übernommen.";
            if (PersonJson(p).ToJsonString() != PersonJson(q).ToJsonString()) return $"Die KI hat vorhandene Angaben zu „{p.Name}“ geändert – nicht übernommen. Sie darf nur neue Personen und Verknüpfungen ergänzen.";
        }
        foreach (var f in a.Families)
        {
            var g = b.F(f.Id);
            if (g is null || (f.Husband is not null && g.Husband != f.Husband) || (f.Wife is not null && g.Wife != f.Wife) || f.Children.Except(g.Children).Any()
                || ToJsonText(f.Events.Select(EventJson)) != ToJsonText(g.Events.Select(EventJson)) || f.Note != g.Note)
                return $"Die KI hat die Familie {f.Id} verändert – nicht übernommen.";
        }
        foreach (var p in b.Persons.Where(p => a.P(p.Id) is null))
            foreach (var e in p.Events)
                if (e.When.Kind == GenDateKind.Unreadable) return $"Die KI hat für „{p.Name}“ ein unlesbares Datum „{e.Date}“ geliefert – nicht übernommen.";
        return null;
    }

    static string ToJsonText(IEnumerable<FamSource> s) => new JsonArray([.. s.Select(x => (JsonNode)SourceJson(x))]).ToJsonString();
    static string ToJsonText(IEnumerable<JsonObject> s) => new JsonArray([.. s.Select(x => (JsonNode)x)]).ToJsonString();
}
