using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Trendo.Core.Arch;

/// <summary>T-0256 (SPEC-01): Grundriss als echtes Planungsmodell – Wände mit Stärke und Höhe, Räume als Umriss, Türen und Fenster
/// in den Wänden, Möbel mit Drehung. Alle Maße in Zentimetern, Ursprung oben links, y nach unten (wie auf dem Bildschirm).</summary>
public sealed class FloorPlan
{
    public const int FormatVersion = 1;
    public int Version { get; set; } = FormatVersion;
    public string Name { get; set; } = "Mein Grundriss";
    public List<Wall> Walls { get; set; } = [];
    public List<Opening> Openings { get; set; } = [];
    public List<PlanRoom> Rooms { get; set; } = [];
    public List<Item> Items { get; set; } = [];

    static readonly JsonSerializerOptions Opt = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.Never };
    public static string NewId() => Guid.NewGuid().ToString("N")[..8];

    // ---------- Speichern im Programmdokument ----------
    public static FloorPlan From(JsonObject data) =>
        data["plan"] is JsonObject o ? Migrate(o.Deserialize<FloorPlan>(Opt) ?? new()) : new();
    public void WriteTo(JsonObject data) => data["plan"] = JsonSerializer.SerializeToNode(this, Opt);
    public FloorPlan Clone() => JsonSerializer.Deserialize<FloorPlan>(JsonSerializer.Serialize(this, Opt), Opt)!;

    /// <summary>Ältere oder fremde Stände in die aktuelle Form bringen (fehlende Ids, unsinnige Werte, Türen ohne Wand).</summary>
    static FloorPlan Migrate(FloorPlan p)
    {
        foreach (var w in p.Walls) { if (string.IsNullOrEmpty(w.Id)) w.Id = NewId(); if (w.Thickness <= 0) w.Thickness = 24; if (w.Height <= 0) w.Height = 250; }
        foreach (var r in p.Rooms) { if (string.IsNullOrEmpty(r.Id)) r.Id = NewId(); if (r.Height <= 0) r.Height = 250; }
        foreach (var i in p.Items) if (string.IsNullOrEmpty(i.Id)) i.Id = NewId();
        p.Openings.RemoveAll(o => p.Walls.All(w => w.Id != o.WallId));
        p.Version = FormatVersion;
        return p;
    }

    public Wall? WallOf(string id) => Walls.FirstOrDefault(w => w.Id == id);

    public bool Remove(string id)
    {
        var n = Walls.RemoveAll(w => w.Id == id) + Rooms.RemoveAll(r => r.Id == id) + Items.RemoveAll(i => i.Id == id) + Openings.RemoveAll(o => o.Id == id);
        Openings.RemoveAll(o => Walls.All(w => w.Id != o.WallId));
        return n > 0;
    }

    // ---------- Fangen ----------
    /// <summary>Punkt fangen: zuerst Wandenden, Wandecken (Außen-/Innenkante) und Raumecken in der Nähe, sonst das Raster.</summary>
    public (double X, double Y, bool Snapped) Snap(double x, double y, double grid = 10, double tol = 15)
    {
        var best = (X: x, Y: y, D: tol);
        void Try(double px, double py, double penalty = 0) { var d = Math.Sqrt((px - x) * (px - x) + (py - y) * (py - y)) + penalty; if (d < best.D) best = (px, py, d); }
        // Wandenden (Achse) haben Vorrang vor Kantenecken
        foreach (var w in Walls) { Try(w.X1, w.Y1); Try(w.X2, w.Y2); foreach (var (cx, cy) in w.Outline()) Try(cx, cy, 3); }
        foreach (var r in Rooms) foreach (var p in r.Points) Try(p.X, p.Y);
        if (best.D < tol) return (best.X, best.Y, true);
        return grid > 0 ? (Math.Round(x / grid) * grid, Math.Round(y / grid) * grid, false) : (x, y, false);
    }

    /// <summary>Beim Zeichnen nahe waagrecht/senkrecht einrasten (wie in jedem CAD).</summary>
    public static (double X, double Y) AxisLock(double x0, double y0, double x, double y, double degrees = 7)
    {
        var a = Math.Abs(Math.Atan2(y - y0, x - x0) * 180 / Math.PI);
        if (a < degrees || a > 180 - degrees) return (x, y0);
        if (Math.Abs(a - 90) < degrees) return (x0, y);
        return (x, y);
    }

    // ---------- Zeichnen ----------
    public Wall AddWall(double x1, double y1, double x2, double y2, double thickness = 24, double height = 250)
    {
        if (Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1)) < 5) throw new ArgumentException("Die Wand ist zu kurz (mindestens 5 cm).");
        if (thickness is < 5 or > 100) throw new ArgumentException("Die Wandstärke muss zwischen 5 und 100 cm liegen.");
        var w = new Wall { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Thickness = thickness, Height = height };
        Walls.Add(w); return w;
    }

    /// <summary>Rechteckigen Raum aufziehen: die gezogene Fläche ist das lichte Innenmaß, die Wände kommen außen herum.
    /// Wo schon eine Wand liegt (z. B. beim Nachbarraum), wird keine zweite gebaut.</summary>
    public PlanRoom AddRectRoom(double x1, double y1, double x2, double y2, string name = "Raum", string kind = "wohnen", double wallThickness = 24, double height = 250, bool walls = true)
    {
        var (l, r) = (Math.Min(x1, x2), Math.Max(x1, x2)); var (t, b) = (Math.Min(y1, y2), Math.Max(y1, y2));
        if (r - l < 50 || b - t < 50) throw new ArgumentException("Ein Raum muss mindestens 50 × 50 cm groß sein.");
        var room = new PlanRoom { Name = name, Kind = kind, Height = height, Points = [new(l, t), new(r, t), new(r, b), new(l, b)] };
        Rooms.Add(room);
        if (!walls) return room;
        var h = wallThickness / 2;
        (double, double, double, double)[] edges = [(l - h, t - h, r + h, t - h), (r + h, t - h, r + h, b + h), (r + h, b + h, l - h, b + h), (l - h, b + h, l - h, t - h)];
        foreach (var (ax, ay, bx, by) in edges)
            foreach (var (s, e) in Uncovered(ax, ay, bx, by, wallThickness))
            {
                var len = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
                AddWall(ax + (bx - ax) * s / len, ay + (by - ay) * s / len, ax + (bx - ax) * e / len, ay + (by - ay) * e / len, wallThickness, height);
            }
        return room;
    }

    /// <summary>Abschnitte einer geplanten Wand, die noch nicht von einer vorhandenen parallelen Wand belegt sind (in cm ab Anfang).</summary>
    public List<(double S, double E)> Uncovered(double ax, double ay, double bx, double by, double thickness)
    {
        var len = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
        var ux = (bx - ax) / len; var uy = (by - ay) / len;
        var covered = new List<(double S, double E)>();
        foreach (var w in Walls)
        {
            var wl = w.Length; if (wl < 1) continue;
            var wx = (w.X2 - w.X1) / wl; var wy = (w.Y2 - w.Y1) / wl;
            if (Math.Abs(ux * wy - uy * wx) > 0.02) continue; // nicht parallel
            var dist = Math.Abs((w.X1 - ax) * -uy + (w.Y1 - ay) * ux);
            if (dist > (w.Thickness + thickness) / 2 + 1) continue;
            var s1 = (w.X1 - ax) * ux + (w.Y1 - ay) * uy; var s2 = (w.X2 - ax) * ux + (w.Y2 - ay) * uy;
            covered.Add((Math.Max(0, Math.Min(s1, s2) - w.Thickness / 2), Math.Min(len, Math.Max(s1, s2) + w.Thickness / 2)));
        }
        var free = new List<(double, double)>(); var pos = 0.0;
        foreach (var (s, e) in covered.Where(c => c.E > c.S).OrderBy(c => c.S))
        {
            if (s - pos >= 20) free.Add((pos, s));
            pos = Math.Max(pos, e);
        }
        if (len - pos >= 20) free.Add((pos, len));
        return free;
    }

    /// <summary>Raum durch Doppelklick in eine von Wänden umschlossene Fläche erkennen (Rasterfüllung mit 2 cm, dann Umriss vereinfacht).</summary>
    public PlanRoom? DetectRoom(double x, double y, string name = "Raum", string kind = "wohnen")
    {
        if (Walls.Count < 3) return null;
        const double cell = 2;
        var outs = Walls.Select(w => w.Outline()).ToList();
        double minX = outs.Min(o => o.Min(p => p.X)) - cell, minY = outs.Min(o => o.Min(p => p.Y)) - cell;
        double maxX = outs.Max(o => o.Max(p => p.X)) + cell, maxY = outs.Max(o => o.Max(p => p.Y)) + cell;
        int nx = (int)Math.Ceiling((maxX - minX) / cell), ny = (int)Math.Ceiling((maxY - minY) / cell);
        if ((long)nx * ny > 6_000_000 || x <= minX || y <= minY || x >= maxX || y >= maxY) return null;
        var solid = new bool[nx, ny];
        foreach (var o in outs)
        {
            int x0 = Math.Max(0, (int)((o.Min(p => p.X) - minX) / cell)), x1 = Math.Min(nx - 1, (int)((o.Max(p => p.X) - minX) / cell) + 1);
            int y0 = Math.Max(0, (int)((o.Min(p => p.Y) - minY) / cell)), y1 = Math.Min(ny - 1, (int)((o.Max(p => p.Y) - minY) / cell) + 1);
            for (var i = x0; i <= x1; i++) for (var j = y0; j <= y1; j++)
                if (!solid[i, j] && Geo.Inside(o, minX + (i + 0.5) * cell, minY + (j + 0.5) * cell)) solid[i, j] = true;
        }
        int sx = (int)((x - minX) / cell), sy = (int)((y - minY) / cell);
        if (solid[sx, sy]) return null;
        var fill = new bool[nx, ny]; var q = new Queue<(int, int)>(); q.Enqueue((sx, sy)); fill[sx, sy] = true;
        while (q.Count > 0)
        {
            var (i, j) = q.Dequeue();
            if (i == 0 || j == 0 || i == nx - 1 || j == ny - 1) return null; // nicht geschlossen
            foreach (var (a, b) in new[] { (i + 1, j), (i - 1, j), (i, j + 1), (i, j - 1) })
                if (!fill[a, b] && !solid[a, b]) { fill[a, b] = true; q.Enqueue((a, b)); }
        }
        var poly = Geo.Trace(fill, nx, ny).Select(p => new PlanPoint(Math.Round(minX + p.X * cell, 1), Math.Round(minY + p.Y * cell, 1))).ToList();
        poly = Geo.Simplify(poly, cell * 1.5);
        if (poly.Count < 3) return null;
        var room = new PlanRoom { Name = name, Kind = kind, Points = poly, Height = Walls.Max(w => w.Height) };
        Rooms.Add(room); return room;
    }

    /// <summary>Tür oder Fenster an der nächstgelegenen Wand einsetzen (mittig an der Mausposition, innerhalb der Wand).</summary>
    public Opening? AddOpening(string kind, double x, double y, double? width = null, double maxDist = 60)
    {
        var hit = Walls.Select(w => (W: w, P: w.Project(x, y))).Where(h => h.P.Dist <= Math.Max(maxDist, h.W.Thickness)).OrderBy(h => h.P.Dist).FirstOrDefault();
        if (hit.W is null) return null;
        var o = kind == "fenster"
            ? new Opening { Kind = "fenster", Width = width ?? 120, Height = 135, Sill = 90 }
            : new Opening { Kind = "tuer", Width = width ?? 88.5, Height = 201, Sill = 0 };
        if (o.Width + 10 > hit.W.Length) throw new ArgumentException($"Die Wand ist zu kurz für eine {(o.Kind == "fenster" ? "Fenster" : "Tür")}breite von {o.Width:0} cm.");
        o.WallId = hit.W.Id; o.At = Math.Clamp(hit.P.T, o.Width / 2 + 5, hit.W.Length - o.Width / 2 - 5);
        o.Inward = hit.P.Side >= 0;
        Openings.Add(o); return o;
    }

    /// <summary>Tür zu der Seite aufschlagen lassen, auf der der Punkt liegt (z. B. in den kleineren Raum).</summary>
    public Opening OpenTowards(Opening o, double x, double y)
    {
        if (WallOf(o.WallId) is { } w) o.Inward = w.Project(x, y).Side >= 0;
        return o;
    }

    Opening Door(double x, double y, double towardX, double towardY) => OpenTowards(AddOpening("tuer", x, y) ?? throw new InvalidOperationException("Vorlage: Tür ohne Wand."), towardX, towardY);

    public Item AddItem(string type, double x, double y)
    {
        var c = FurnitureCatalog.Get(type) ?? throw new ArgumentException("Dieses Möbel gibt es im Katalog nicht.");
        var i = new Item { Type = c.Type, Name = c.Name, X = x, Y = y, W = c.W, D = c.D, Rot = 0 };
        Items.Add(i); return i;
    }

    /// <summary>Wandende verschieben – angeschlossene Wände (gleicher Eckpunkt) gehen mit.</summary>
    public void MoveWallEnd(string wallId, bool start, double x, double y)
    {
        var w = WallOf(wallId) ?? throw new KeyNotFoundException("Die Wand gibt es nicht mehr.");
        var (ox, oy) = start ? (w.X1, w.Y1) : (w.X2, w.Y2);
        foreach (var o in Walls)
        {
            if (Near(o.X1, o.Y1, ox, oy)) { o.X1 = x; o.Y1 = y; }
            if (Near(o.X2, o.Y2, ox, oy)) { o.X2 = x; o.Y2 = y; }
        }
        ClampOpenings();
    }

    /// <summary>Ganze Wand verschieben – angeschlossene Wandenden gehen mit.</summary>
    public void MoveWall(string wallId, double dx, double dy)
    {
        var w = WallOf(wallId) ?? throw new KeyNotFoundException("Die Wand gibt es nicht mehr.");
        var a = (w.X1, w.Y1); var b = (w.X2, w.Y2);
        foreach (var o in Walls)
        {
            if (Near(o.X1, o.Y1, a.X1, a.Y1) || Near(o.X1, o.Y1, b.X2, b.Y2)) { o.X1 += dx; o.Y1 += dy; }
            if (Near(o.X2, o.Y2, a.X1, a.Y1) || Near(o.X2, o.Y2, b.X2, b.Y2)) { o.X2 += dx; o.Y2 += dy; }
        }
        ClampOpenings();
    }

    /// <summary>Wandlänge auf ein Maß setzen (das Ende bewegt sich, angeschlossene Wände gehen mit).</summary>
    public void SetWallLength(string wallId, double length)
    {
        var w = WallOf(wallId) ?? throw new KeyNotFoundException("Die Wand gibt es nicht mehr.");
        if (length < 5) throw new ArgumentException("Eine Wand muss mindestens 5 cm lang sein.");
        var l = w.Length; MoveWallEnd(wallId, false, w.X1 + (w.X2 - w.X1) / l * length, w.Y1 + (w.Y2 - w.Y1) / l * length);
    }

    void ClampOpenings()
    {
        foreach (var o in Openings) if (WallOf(o.WallId) is { } w) o.At = Math.Clamp(o.At, Math.Min(o.Width / 2, w.Length / 2), Math.Max(w.Length - o.Width / 2, w.Length / 2));
    }

    static bool Near(double ax, double ay, double bx, double by) => Math.Abs(ax - bx) < 0.5 && Math.Abs(ay - by) < 0.5;

    // ---------- Treffer ----------
    /// <summary>Was liegt unter dem Mauszeiger? Reihenfolge wie beim Anklicken: Öffnungen, Möbel, Wände, Räume.</summary>
    public (string Kind, string Id)? HitTest(double x, double y, double tol = 8)
    {
        foreach (var o in Openings)
            if (WallOf(o.WallId) is { } w && w.Project(x, y) is var p && p.Dist <= w.Thickness / 2 + tol && Math.Abs(p.T - o.At) <= o.Width / 2) return ("opening", o.Id);
        foreach (var i in Items.AsEnumerable().Reverse()) if (Geo.Inside(i.Corners(), x, y)) return ("item", i.Id);
        foreach (var w in Walls) if (w.Project(x, y) is var p && p.T >= -tol && p.T <= w.Length + tol && p.Dist <= w.Thickness / 2 + tol) return ("wall", w.Id);
        foreach (var r in Rooms.AsEnumerable().Reverse()) if (Geo.Inside(r.Points.Select(p => (p.X, p.Y)).ToList(), x, y)) return ("room", r.Id);
        return null;
    }

    public (double MinX, double MinY, double MaxX, double MaxY)? Bounds()
    {
        var pts = Walls.SelectMany(w => w.Outline()).Concat(Rooms.SelectMany(r => r.Points.Select(p => (p.X, p.Y)))).Concat(Items.SelectMany(i => i.Corners())).ToList();
        return pts.Count == 0 ? null : (pts.Min(p => p.X), pts.Min(p => p.Y), pts.Max(p => p.X), pts.Max(p => p.Y));
    }

    // ---------- Kennzahlen ----------
    /// <summary>Öffnungen, die an einer Raumkante liegen (deren Wand die Kante bildet).</summary>
    public List<(Opening O, Wall W)> OpeningsOf(PlanRoom r)
    {
        var res = new List<(Opening, Wall)>();
        var pts = r.Points; if (pts.Count < 3) return res;
        foreach (var o in Openings)
        {
            if (WallOf(o.WallId) is not { } w) continue;
            var (cx, cy) = w.PointAt(o.At);
            for (var k = 0; k < pts.Count; k++)
            {
                var a = pts[k]; var b = pts[(k + 1) % pts.Count];
                if (Geo.SegDist(cx, cy, a.X, a.Y, b.X, b.Y) <= w.Thickness / 2 + 3) { res.Add((o, w)); break; }
            }
        }
        return res;
    }

    public RoomFigures Figures(PlanRoom r)
    {
        var area = r.Area / 10000; var per = r.Perimeter / 100; var h = r.Height / 100;
        var ops = OpeningsOf(r);
        var winA = ops.Where(x => x.O.Kind == "fenster").Sum(x => x.O.Width * x.O.Height) / 10000;
        var doorA = ops.Where(x => x.O.Kind == "tuer").Sum(x => x.O.Width * x.O.Height) / 10000;
        var doorW = ops.Where(x => x.O.Kind == "tuer").Sum(x => x.O.Width) / 100;
        return new RoomFigures(area, per, h, Math.Max(0, per * h - winA - doorA), winA, ops.Count(x => x.O.Kind == "tuer"), ops.Count(x => x.O.Kind == "fenster"), Math.Max(0, per - doorW));
    }

    /// <summary>Gleichwertiger Rechteckraum (gleiche Fläche und gleicher Umfang) – damit greifen die bewährten Rechner für Tapete, Heizung, Licht.</summary>
    public Room AsRoom(PlanRoom r)
    {
        var f = Figures(r); var s = f.Perimeter / 2; var disc = s * s / 4 - f.Area;
        var (l, w) = disc >= 0 ? (s / 2 + Math.Sqrt(disc), s / 2 - Math.Sqrt(disc)) : (Math.Sqrt(f.Area), Math.Sqrt(f.Area));
        return new Room(r.Name, Math.Round(l, 3), Math.Round(w, 3), f.Height, f.Doors, f.Windows);
    }

    /// <summary>Wohnfläche nach WoFlV: ab 2 m Raumhöhe voll, 1–2 m zur Hälfte, darunter nicht; Balkon/Terrasse zu 25 %; Keller zählt nicht.</summary>
    public double LivingArea() => Rooms.Sum(r =>
    {
        var a = r.Area / 10000;
        return r.Kind switch { "balkon" => a * 0.25, "keller" => 0, _ => r.Height >= 200 ? a : r.Height >= 100 ? a / 2 : 0 };
    });

    public MaterialNeed Need(double packM2 = 2.2, double coats = 2, double m2PerLiter = 10, double bucket = 10)
    {
        var rooms = Rooms.Where(r => r.Kind is not ("balkon" or "keller")).ToList();
        var floor = rooms.Where(r => r.Floor != "fliesen").Sum(r => Figures(r).Area);
        var walls = rooms.Where(r => r.WallFinish == "farbe").Sum(r => Figures(r).WallNet + Figures(r).Area);
        var liters = walls * coats / m2PerLiter;
        return new MaterialNeed(Math.Round(liters, 1), (int)Math.Ceiling(liters / bucket), (int)Math.Ceiling(floor * 1.1 / packM2), Math.Round(rooms.Where(r => r.Floor != "fliesen").Sum(r => Figures(r).Skirting), 2), Math.Round(floor, 2), Math.Round(walls, 2));
    }

    // ---------- Prüfen ----------
    static readonly HashSet<string> Living = ["wohnen", "schlafen", "kind", "arbeiten", "kueche", "essen"];

    public List<PlanIssue> Check()
    {
        var res = new List<PlanIssue>();
        foreach (var r in Rooms)
        {
            var f = Figures(r);
            if (r.Kind != "balkon" && f.Doors == 0) res.Add(new("warnung", $"„{r.Name}“ hat keine Tür.", r.Id));
            if (Living.Contains(r.Kind) && f.Area > 0 && f.WindowArea < f.Area / 8)
                res.Add(new("hinweis", $"„{r.Name}“: Fensterfläche {f.WindowArea:0.00} m² ist weniger als 1/8 der Grundfläche ({f.Area / 8:0.00} m²) – die meisten Landesbauordnungen verlangen das für Aufenthaltsräume.", r.Id));
            if (f.Area < 1) res.Add(new("hinweis", $"„{r.Name}“ ist sehr klein ({f.Area:0.00} m²).", r.Id));
        }
        var walls = Walls.Select(w => (w, o: w.Outline())).ToList();
        foreach (var i in Items)
        {
            var c = i.Corners();
            if (walls.FirstOrDefault(x => Geo.Overlap(c, x.o, 0.5)).w is not null) res.Add(new("fehler", $"„{i.Name}“ steht in einer Wand.", i.Id));
            if (i.Type != "teppich")
                foreach (var j in Items) if (j.Id.CompareTo(i.Id) > 0 && j.Type != "teppich" && Geo.Overlap(c, j.Corners(), 0.5)) res.Add(new("fehler", $"„{i.Name}“ und „{j.Name}“ überschneiden sich.", i.Id));
        }
        foreach (var g in Openings.GroupBy(o => o.WallId))
        {
            var l = g.OrderBy(o => o.At).ToList();
            for (var k = 1; k < l.Count; k++) if (l[k].At - l[k].Width / 2 < l[k - 1].At + l[k - 1].Width / 2) res.Add(new("fehler", "Zwei Öffnungen überschneiden sich in derselben Wand.", l[k].Id));
        }
        foreach (var o in Openings.Where(o => o.Kind == "tuer"))
        {
            var swing = DoorSwing(o); if (swing is null) continue;
            foreach (var i in Items) if (i.Type != "teppich" && Geo.Overlap(swing, i.Corners(), 0.5)) res.Add(new("warnung", $"Die Tür lässt sich nicht ganz öffnen – „{i.Name}“ steht im Weg.", o.Id));
        }
        return res;
    }

    /// <summary>Fläche, die das Türblatt beim Öffnen überstreicht (als Quadrat Breite × Breite auf der Öffnungsseite).</summary>
    public List<(double X, double Y)>? DoorSwing(Opening o)
    {
        if (WallOf(o.WallId) is not { } w) return null;
        var (ux, uy) = w.Dir; var (nx, ny) = (-uy, ux); var s = o.Inward ? 1 : -1;
        var (cx, cy) = w.PointAt(o.At); var h = w.Thickness / 2;
        double ax = cx - ux * o.Width / 2 + nx * s * h, ay = cy - uy * o.Width / 2 + ny * s * h;
        return [(ax, ay), (ax + ux * o.Width, ay + uy * o.Width), (ax + ux * o.Width + nx * s * o.Width, ay + uy * o.Width + ny * s * o.Width), (ax + nx * s * o.Width, ay + ny * s * o.Width)];
    }

    // ---------- Vorlagen und Übernahme ----------
    public static FloorPlan Template(string id)
    {
        var p = new FloorPlan();
        switch (id)
        {
            case "wohnung":
                p.Name = "2-Zimmer-Wohnung";
                p.AddRectRoom(0, 0, 500, 420, "Wohnzimmer", "wohnen");
                p.AddRectRoom(524, 0, 884, 420, "Schlafzimmer", "schlafen");
                p.AddRectRoom(0, 444, 300, 724, "Küche", "kueche");
                p.AddRectRoom(324, 444, 584, 724, "Bad", "bad").Floor = "fliesen";
                p.AddRectRoom(608, 444, 884, 724, "Flur", "flur");
                foreach (var (x, y) in new[] { (250.0, -12.0), (704.0, -12.0), (150.0, 736.0) }) p.AddOpening("fenster", x, y);
                p.Door(512, 300, 400, 300); p.Door(700, 432, 700, 600); p.Door(596, 600, 700, 600); p.Door(400, 432, 400, 600); p.Door(150, 432, 150, 600); p.Door(746, 736, 746, 600);
                p.AddItem("sofa", 250, 80); p.AddItem("couchtisch", 250, 190); p.AddItem("tv", 250, 390);
                p.AddItem("doppelbett", 704, 150); p.AddItem("schrank", 704, 385);
                p.AddItem("kuechenzeile", 45, 584).Rot = 90; p.AddItem("esstisch", 200, 640);
                p.AddItem("dusche", 534, 494); p.AddItem("wc", 360, 685); p.AddItem("waschbecken", 470, 702);
                break;
            case "bad":
                p.Name = "Badezimmer";
                p.AddRectRoom(0, 0, 260, 220, "Bad", "bad").Floor = "fliesen";
                p.Rooms[0].WallFinish = "fliesen";
                p.Door(215, 232, 215, 100); p.AddOpening("fenster", 130, -12, 80);
                p.AddItem("badewanne", 175, 45); p.AddItem("wc", 35, 120).Rot = 90; p.AddItem("waschbecken", 120, 196).Rot = 180;
                break;
            case "haus":
                p.Name = "Einfamilienhaus – Erdgeschoss";
                p.AddRectRoom(0, 0, 640, 480, "Wohnen & Essen", "wohnen");
                p.AddRectRoom(664, 0, 1040, 300, "Küche", "kueche");
                p.AddRectRoom(664, 324, 1040, 480, "Gäste-WC", "bad").Floor = "fliesen";
                p.AddRectRoom(0, 504, 400, 780, "Arbeiten", "arbeiten");
                p.AddRectRoom(424, 504, 1040, 780, "Flur & Diele", "flur");
                p.AddRectRoom(0, -324, 640, -24, "Terrasse", "balkon", walls: false).Floor = "fliesen";
                foreach (var (x, y) in new[] { (160.0, -12.0), (480.0, -12.0), (852.0, -12.0), (200.0, 792.0), (-12.0, 240.0) }) p.AddOpening("fenster", x, y, 140);
                p.Door(320, -12, 320, 200); p.Door(652, 150, 800, 150); p.Door(760, 492, 760, 650); p.Door(300, 492, 300, 650); p.Door(560, 492, 560, 650); p.Door(900, 792, 900, 650);
                p.AddItem("sofa", 160, 100); p.AddItem("esstisch", 470, 300); p.AddItem("kuechenzeile", 852, 45); p.AddItem("schreibtisch", 120, 740);
                break;
        }
        return p;
    }

    /// <summary>Räume aus der früheren Raumliste (nur Länge × Breite) als Rechtecke nebeneinander übernehmen.</summary>
    public static FloorPlan FromRoomList(IEnumerable<Room> rooms)
    {
        var p = new FloorPlan { Name = "Übernommene Räume" }; var x = 0.0;
        foreach (var r in rooms)
        {
            var w = Math.Max(50, r.Length * 100); var d = Math.Max(50, r.Width * 100);
            p.AddRectRoom(x, 0, x + w, d, r.Name, "wohnen", 24, r.Height * 100);
            x += w + 24;
        }
        return p;
    }

    public static string Cm(double v) => (v / 100).ToString("0.00", CultureInfo.GetCultureInfo("de-DE")) + " m";
}

public readonly record struct PlanPoint(double X, double Y);

public sealed class Wall
{
    public string Id { get; set; } = FloorPlan.NewId();
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }
    public double Thickness { get; set; } = 24;
    public double Height { get; set; } = 250;

    [JsonIgnore] public double Length => Math.Sqrt((X2 - X1) * (X2 - X1) + (Y2 - Y1) * (Y2 - Y1));
    [JsonIgnore] public (double X, double Y) Dir { get { var l = Math.Max(Length, 1e-9); return ((X2 - X1) / l, (Y2 - Y1) / l); } }
    public (double X, double Y) PointAt(double t) { var (ux, uy) = Dir; return (X1 + ux * t, Y1 + uy * t); }

    /// <summary>Abstand eines Punkts zur Wandachse, Lage entlang der Wand (t in cm ab Anfang) und Seite (+1 links / −1 rechts).</summary>
    public (double Dist, double T, int Side) Project(double x, double y)
    {
        var (ux, uy) = Dir; var t = (x - X1) * ux + (y - Y1) * uy; var n = (x - X1) * -uy + (y - Y1) * ux;
        var tc = Math.Clamp(t, 0, Length); var (px, py) = PointAt(tc);
        return (Math.Sqrt((x - px) * (x - px) + (y - py) * (y - py)), t, n >= 0 ? 1 : -1);
    }

    /// <summary>Umriss der Wand als Rechteck (Achse ± halbe Stärke).</summary>
    public List<(double X, double Y)> Outline()
    {
        var (ux, uy) = Dir; var h = Thickness / 2; var (nx, ny) = (-uy * h, ux * h);
        return [(X1 + nx, Y1 + ny), (X2 + nx, Y2 + ny), (X2 - nx, Y2 - ny), (X1 - nx, Y1 - ny)];
    }
}

public sealed class Opening
{
    public string Id { get; set; } = FloorPlan.NewId();
    public string WallId { get; set; } = "";
    /// <summary>„tuer“ oder „fenster“.</summary>
    public string Kind { get; set; } = "tuer";
    /// <summary>Mitte der Öffnung in cm ab Wandanfang.</summary>
    public double At { get; set; }
    public double Width { get; set; } = 88.5;
    public double Height { get; set; } = 201;
    public double Sill { get; set; }
    public bool HingeLeft { get; set; } = true;
    public bool Inward { get; set; } = true;
}

public sealed class PlanRoom
{
    public string Id { get; set; } = FloorPlan.NewId();
    public string Name { get; set; } = "Raum";
    /// <summary>wohnen, schlafen, kind, arbeiten, kueche, essen, bad, flur, abstell, balkon, keller.</summary>
    public string Kind { get; set; } = "wohnen";
    public List<PlanPoint> Points { get; set; } = [];
    /// <summary>laminat, parkett, fliesen, vinyl, teppich.</summary>
    public string Floor { get; set; } = "laminat";
    /// <summary>farbe, tapete, fliesen.</summary>
    public string WallFinish { get; set; } = "farbe";
    public double Height { get; set; } = 250;

    [JsonIgnore] public double Area => Math.Abs(Geo.SignedArea(Points.Select(p => (p.X, p.Y)).ToList()));
    [JsonIgnore] public double Perimeter => Points.Count < 2 ? 0 : Points.Select((p, i) => { var q = Points[(i + 1) % Points.Count]; return Math.Sqrt((q.X - p.X) * (q.X - p.X) + (q.Y - p.Y) * (q.Y - p.Y)); }).Sum();
    [JsonIgnore] public (double X, double Y) Center => Geo.Centroid(Points.Select(p => (p.X, p.Y)).ToList());
}

public sealed class Item
{
    public string Id { get; set; } = FloorPlan.NewId();
    public string Type { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Mittelpunkt.</summary>
    public double X { get; set; }
    public double Y { get; set; }
    public double W { get; set; }
    public double D { get; set; }
    public double Rot { get; set; }

    public List<(double X, double Y)> Corners()
    {
        var a = Rot * Math.PI / 180; var c = Math.Cos(a); var s = Math.Sin(a);
        (double, double) P(double dx, double dy) => (X + dx * c - dy * s, Y + dx * s + dy * c);
        return [P(-W / 2, -D / 2), P(W / 2, -D / 2), P(W / 2, D / 2), P(-W / 2, D / 2)];
    }
}

public sealed record RoomFigures(double Area, double Perimeter, double Height, double WallNet, double WindowArea, int Doors, int Windows, double Skirting);
public sealed record PlanIssue(string Level, string Message, string? TargetId);

public sealed record FurnitureType(string Type, string Name, string Group, double W, double D, double H);

/// <summary>Möbel mit üblichen Maßen (Breite × Tiefe × Höhe in cm).</summary>
public static class FurnitureCatalog
{
    public static readonly IReadOnlyList<FurnitureType> All =
    [
        new("sofa", "Sofa 3-Sitzer", "Wohnen", 220, 90, 85), new("sessel", "Sessel", "Wohnen", 80, 85, 90), new("couchtisch", "Couchtisch", "Wohnen", 110, 60, 45),
        new("tv", "TV-Board", "Wohnen", 180, 45, 50), new("regal", "Regal", "Wohnen", 80, 35, 190), new("pflanze", "Pflanze", "Wohnen", 40, 40, 120), new("teppich", "Teppich", "Wohnen", 200, 140, 1),
        new("bett", "Bett 90 × 200", "Schlafen", 90, 200, 45), new("doppelbett", "Doppelbett 180 × 200", "Schlafen", 180, 200, 50), new("schrank", "Kleiderschrank", "Schlafen", 200, 60, 220),
        new("kommode", "Kommode", "Schlafen", 120, 45, 85), new("nachttisch", "Nachttisch", "Schlafen", 45, 40, 50),
        new("kuechenzeile", "Küchenzeile", "Küche", 270, 60, 90), new("kuehlschrank", "Kühlschrank", "Küche", 60, 65, 185), new("herd", "Herd", "Küche", 60, 60, 90),
        new("spuele", "Spüle", "Küche", 80, 60, 90), new("esstisch", "Esstisch", "Küche", 160, 90, 75), new("stuhl", "Stuhl", "Küche", 45, 50, 90),
        new("badewanne", "Badewanne", "Bad", 170, 75, 60), new("dusche", "Dusche", "Bad", 90, 90, 200), new("wc", "WC", "Bad", 40, 65, 80),
        new("waschbecken", "Waschbecken", "Bad", 60, 45, 85), new("waschmaschine", "Waschmaschine", "Bad", 60, 60, 85),
        new("schreibtisch", "Schreibtisch", "Arbeiten", 140, 70, 75), new("buerostuhl", "Bürostuhl", "Arbeiten", 60, 60, 110),
        new("treppe", "Treppe (gerade)", "Bau", 100, 280, 275),
    ];
    public static FurnitureType? Get(string type) => All.FirstOrDefault(x => x.Type == type);
}

/// <summary>Kleine 2D-Geometrie für den Grundriss.</summary>
public static class Geo
{
    public static double SignedArea(IReadOnlyList<(double X, double Y)> p)
    {
        double s = 0; for (var i = 0; i < p.Count; i++) { var a = p[i]; var b = p[(i + 1) % p.Count]; s += a.X * b.Y - b.X * a.Y; }
        return s / 2;
    }

    public static (double X, double Y) Centroid(IReadOnlyList<(double X, double Y)> p)
    {
        var a = SignedArea(p); if (Math.Abs(a) < 1e-9) return p.Count == 0 ? (0, 0) : (p.Average(q => q.X), p.Average(q => q.Y));
        double cx = 0, cy = 0;
        for (var i = 0; i < p.Count; i++) { var u = p[i]; var v = p[(i + 1) % p.Count]; var f = u.X * v.Y - v.X * u.Y; cx += (u.X + v.X) * f; cy += (u.Y + v.Y) * f; }
        return (cx / (6 * a), cy / (6 * a));
    }

    public static bool Inside(IReadOnlyList<(double X, double Y)> p, double x, double y)
    {
        var inside = false;
        for (int i = 0, j = p.Count - 1; i < p.Count; j = i++)
            if ((p[i].Y > y) != (p[j].Y > y) && x < (p[j].X - p[i].X) * (y - p[i].Y) / (p[j].Y - p[i].Y) + p[i].X) inside = !inside;
        return inside;
    }

    public static double SegDist(double x, double y, double ax, double ay, double bx, double by)
    {
        var dx = bx - ax; var dy = by - ay; var l = dx * dx + dy * dy;
        var t = l == 0 ? 0 : Math.Clamp(((x - ax) * dx + (y - ay) * dy) / l, 0, 1);
        var px = ax + t * dx - x; var py = ay + t * dy - y; return Math.Sqrt(px * px + py * py);
    }

    /// <summary>Überschneiden sich zwei konvexe Vielecke um mehr als <paramref name="eps"/> cm? (Trennachsen-Test)</summary>
    public static bool Overlap(IReadOnlyList<(double X, double Y)> a, IReadOnlyList<(double X, double Y)> b, double eps = 0)
    {
        foreach (var poly in new[] { a, b })
            for (var i = 0; i < poly.Count; i++)
            {
                var p = poly[i]; var q = poly[(i + 1) % poly.Count]; var nx = -(q.Y - p.Y); var ny = q.X - p.X; var l = Math.Sqrt(nx * nx + ny * ny); if (l < 1e-9) continue; nx /= l; ny /= l;
                double amin = double.MaxValue, amax = double.MinValue, bmin = double.MaxValue, bmax = double.MinValue;
                foreach (var v in a) { var d = v.X * nx + v.Y * ny; amin = Math.Min(amin, d); amax = Math.Max(amax, d); }
                foreach (var v in b) { var d = v.X * nx + v.Y * ny; bmin = Math.Min(bmin, d); bmax = Math.Max(bmax, d); }
                if (amax - eps <= bmin || bmax - eps <= amin) return false;
            }
        return true;
    }

    /// <summary>Umriss einer zusammenhängenden Zellfläche (Kanten zwischen gefüllt/leer, zu einem Ring verkettet).</summary>
    public static List<(double X, double Y)> Trace(bool[,] f, int nx, int ny)
    {
        var next = new Dictionary<(int, int), (int, int)>();
        bool F(int i, int j) => i >= 0 && j >= 0 && i < nx && j < ny && f[i, j];
        for (var i = 0; i < nx; i++) for (var j = 0; j < ny; j++)
        {
            if (!f[i, j]) continue;
            if (!F(i, j - 1)) next[(i, j)] = (i + 1, j);         // oben, im Uhrzeigersinn (y nach unten)
            if (!F(i + 1, j)) next[(i + 1, j)] = (i + 1, j + 1); // rechts
            if (!F(i, j + 1)) next[(i + 1, j + 1)] = (i, j + 1); // unten
            if (!F(i - 1, j)) next[(i, j + 1)] = (i, j);         // links
        }
        if (next.Count == 0) return [];
        // längster Ring = Außenumriss (Löcher, z. B. eine freistehende Stütze, bleiben unberücksichtigt)
        var rings = new List<List<(double, double)>>(); var seen = new HashSet<(int, int)>();
        foreach (var start in next.Keys)
        {
            if (seen.Contains(start)) continue;
            var ring = new List<(double, double)>(); var cur = start;
            while (seen.Add(cur) && next.TryGetValue(cur, out var n)) { ring.Add((cur.Item1, cur.Item2)); cur = n; }
            rings.Add(ring);
        }
        return rings.OrderByDescending(r => r.Count).First();
    }

    /// <summary>Douglas-Peucker auf einem geschlossenen Ring.</summary>
    public static List<PlanPoint> Simplify(List<PlanPoint> ring, double tol)
    {
        if (ring.Count < 4) return ring;
        // an der am weitesten entfernten Stelle auftrennen, damit Anfang/Ende eine echte Ecke sind
        var far = Enumerable.Range(0, ring.Count).OrderByDescending(i => (ring[i].X - ring[0].X) * (ring[i].X - ring[0].X) + (ring[i].Y - ring[0].Y) * (ring[i].Y - ring[0].Y)).First();
        var a = DP(ring.Take(far + 1).ToList(), tol); var b = DP(ring.Skip(far).Append(ring[0]).ToList(), tol);
        var res = a.Take(a.Count - 1).Concat(b.Take(b.Count - 1)).ToList();
        // gerade durchlaufende Punkte entfernen
        for (var changed = true; changed && res.Count > 3;)
        {
            changed = false;
            for (var i = 0; i < res.Count && res.Count > 3; i++)
            {
                var p = res[(i + res.Count - 1) % res.Count]; var q = res[(i + 1) % res.Count];
                if (SegDist(res[i].X, res[i].Y, p.X, p.Y, q.X, q.Y) < tol / 2) { res.RemoveAt(i); changed = true; break; }
            }
        }
        return res;

        static List<PlanPoint> DP(List<PlanPoint> pts, double tol)
        {
            if (pts.Count < 3) return pts;
            var (ax, ay, bx, by) = (pts[0].X, pts[0].Y, pts[^1].X, pts[^1].Y);
            var idx = 0; var max = 0.0;
            for (var i = 1; i < pts.Count - 1; i++) { var d = SegDist(pts[i].X, pts[i].Y, ax, ay, bx, by); if (d > max) { max = d; idx = i; } }
            if (max <= tol) return [pts[0], pts[^1]];
            var l = DP(pts.Take(idx + 1).ToList(), tol); var r = DP(pts.Skip(idx).ToList(), tol);
            return l.Take(l.Count - 1).Concat(r).ToList();
        }
    }
}
