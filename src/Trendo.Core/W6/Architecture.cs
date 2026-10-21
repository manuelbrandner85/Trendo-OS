using System.Text;

namespace Trendo.Core;

/// <summary>Ein Raum mit Maßen in Metern.</summary>
public sealed record Room(string Name, double Length, double Width, double Height = 2.5, int Doors = 1, int Windows = 1)
{
    public const double DoorArea = 2.0, WindowArea = 1.5, DoorWidth = 0.9;
    public double Floor => Length * Width;
    public double Volume => Floor * Height;
    public double Perimeter => 2 * (Length + Width);
    public double WallNet => Math.Max(0, Perimeter * Height - Doors * DoorArea - Windows * WindowArea);
    public double Skirting => Math.Max(0, Perimeter - Doors * DoorWidth);
}

public sealed record MaterialNeed(double PaintLiters, int PaintBuckets, int FloorPacks, double SkirtingMeters, double Floor, double Walls);

public sealed class RoomProject { public string Name { get; set; } = "Meine Wohnung"; public List<Room> Rooms { get; set; } = []; }

/// <summary>SPEC-01: Räume planen – Flächen, Farbe, Bodenbelag, Sockelleisten, Grundriss als SVG.</summary>
public sealed class RoomPlanner(string dataRoot)
{
    public RoomProject Project { get; private set; } = Fach.Load<RoomProject>(dataRoot, "architektur", "projekt");

    public Room Add(string name, double l, double w, double h = 2.5, int doors = 1, int windows = 1)
    {
        if (l <= 0 || w <= 0 || h <= 0 || l > 200 || w > 200 || h > 20) throw new ArgumentException("Die Maße müssen größer als 0 und realistisch sein (in Metern).");
        if (doors < 0 || windows < 0) throw new ArgumentException("Türen und Fenster dürfen nicht negativ sein.");
        var r = new Room(string.IsNullOrWhiteSpace(name) ? $"Raum {Project.Rooms.Count + 1}" : name.Trim(), l, w, h, doors, windows);
        Project.Rooms.Add(r); Save(); return r;
    }

    public void Remove(int index) { if (index >= 0 && index < Project.Rooms.Count) { Project.Rooms.RemoveAt(index); Save(); } }
    public void Save() => Fach.Save(dataRoot, "architektur", "projekt", Project);

    /// <summary>Bedarf: Farbe 2 Anstriche à 10 m²/l, Eimer 10 l; Boden +10 % Verschnitt je Paket.</summary>
    public static MaterialNeed Need(IEnumerable<Room> rooms, double packM2 = 2.2, double coats = 2, double m2PerLiter = 10, double bucket = 10)
    {
        var list = rooms.ToList();
        var walls = list.Sum(r => r.WallNet + r.Floor); // Wände + Decke
        var liters = walls * coats / m2PerLiter;
        var floor = list.Sum(r => r.Floor);
        return new(Math.Round(liters, 1), (int)Math.Ceiling(Math.Round(liters / bucket, 6)), packM2 <= 0 ? 0 : (int)Math.Ceiling(Math.Round(floor * 1.1 / packM2, 6)), Math.Round(list.Sum(r => r.Skirting), 2), Math.Round(floor, 2), Math.Round(walls, 2));
    }

    /// <summary>Einfacher Grundriss: Räume nebeneinander, maßstäblich (1 m = 50 px), mit Beschriftung.</summary>
    public static string Svg(IReadOnlyList<Room> rooms, double scale = 50)
    {
        double x = 10, maxH = 0; var sb = new StringBuilder();
        foreach (var r in rooms)
        {
            double w = r.Length * scale, h = r.Width * scale;
            sb.Append($"<rect x=\"{x:0.#}\" y=\"10\" width=\"{w:0.#}\" height=\"{h:0.#}\" fill=\"#f4f1ea\" stroke=\"#333\" stroke-width=\"3\"/>");
            sb.Append($"<text x=\"{x + w / 2:0.#}\" y=\"{10 + h / 2:0.#}\" text-anchor=\"middle\" font-family=\"sans-serif\" font-size=\"14\">{System.Security.SecurityElement.Escape(r.Name)}</text>");
            sb.Append($"<text x=\"{x + w / 2:0.#}\" y=\"{28 + h / 2:0.#}\" text-anchor=\"middle\" font-family=\"sans-serif\" font-size=\"11\">{Fach.F(r.Length)} × {Fach.F(r.Width)} m = {Fach.F(r.Floor)} m²</text>");
            x += w + 10; maxH = Math.Max(maxH, h);
        }
        return $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{x:0.#}\" height=\"{maxH + 20:0.#}\">{sb}</svg>";
    }

    public string ExportSvg()
    {
        var p = Path.Combine(Fach.StoreDir(dataRoot, "architektur"), "grundriss.svg");
        File.WriteAllText(p, Svg(Project.Rooms)); return p;
    }
}
