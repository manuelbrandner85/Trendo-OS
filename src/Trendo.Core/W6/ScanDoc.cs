using System.Globalization;
using System.Text.Json.Nodes;

namespace Trendo.Core;

/// <summary>T-0264: Ein Bearbeitungsschritt an einer Punktwolke. Die Schritte werden nicht-destruktiv auf die Quelldatei angewandt (wie die Verlaufsliste in CloudCompare/MeshLab).</summary>
public sealed record ScanOp(string Kind, double[] Args, string? Path = null)
{
    public string Describe() => Kind switch
    {
        "outliers" => $"Störpunkte entfernt (Umkreis {F(Args[0])}, mind. {Args[1]:0} Nachbarn)",
        "thin" => $"Ausgedünnt (Würfel {F(Args[0])})",
        "center" => "Zentriert, Boden auf 0",
        "rotate" => $"Gedreht um {F(Args[0])}°",
        "scale" => $"Skaliert × {F(Args[0])}",
        "crop" => $"Zugeschnitten auf {F(Args[0])}…{F(Args[3])} / {F(Args[1])}…{F(Args[4])} / {F(Args[2])}…{F(Args[5])}",
        "nofloor" => $"Boden entfernt (Toleranz {F(Args[0])})",
        "merge" => $"Zusammengeführt mit {System.IO.Path.GetFileName(Path)}",
        _ => Kind,
    };
    static string F(double v) => v.ToString("0.###", CultureInfo.GetCultureInfo("de-DE"));

    public PointCloud Apply(PointCloud pc) => Kind switch
    {
        "outliers" => pc.RemoveOutliers(Args[0], (int)Args[1]),
        "thin" => pc.Downsample(Args[0]),
        "center" => ScanPlus.Center(pc),
        "rotate" => ScanPlus.RotateZ(pc, Args[0]),
        "scale" => ScanPlus.Scale(pc, Args[0]),
        "crop" => ScanPlus.Crop(pc, new(Math.Min(Args[0], Args[3]), Math.Min(Args[1], Args[4]), Math.Min(Args[2], Args[5])), new(Math.Max(Args[0], Args[3]), Math.Max(Args[1], Args[4]), Math.Max(Args[2], Args[5]))),
        "nofloor" => ScanPlus.RemovePlane(pc, ScanPlus.FindFloor(pc, Args[0]), Args[0]),
        "merge" => ScanPlus.Merge(pc, PointCloud.Load(Path ?? throw new ArgumentException("Für das Zusammenführen fehlt die zweite Datei."))),
        _ => throw new ArgumentException($"Unbekannter Schritt „{Kind}“."),
    };
}

/// <summary>T-0264: Dokument des Programms „Punktwolke“ – Quelle, Bearbeitungsschritte, Ansicht und Werkzeug-Einstellungen.</summary>
public sealed class ScanDoc
{
    public const string DemoSource = "beispiel:raum";
    public string Source { get; set; } = "";
    public List<ScanOp> Ops { get; } = [];
    public string ColorBy { get; set; } = "auto"; // auto = Originalfarben, sonst Höhe
    public double Yaw { get; set; } = -35;
    public double Pitch { get; set; } = 28;
    public double Zoom { get; set; } = 1;
    public double Voxel { get; set; } = 0.02;
    public double Radius { get; set; } = 0.05;
    public double MinNeighbors { get; set; } = 3;
    public double FloorTol { get; set; } = 0.02;
    public double Angle { get; set; } = 90;
    public string UnitFrom { get; set; } = "mm";
    public string UnitTo { get; set; } = "m";
    public double[] Box { get; set; } = [-1, -1, -1, 1, 1, 1];

    public static ScanDoc From(JsonObject o)
    {
        double N(string k, double d) => o[k] is JsonValue v && v.TryGetValue<double>(out var x) ? x : d;
        var s = new ScanDoc
        {
            Source = (string?)o["source"] ?? "", ColorBy = (string?)o["colorBy"] ?? "auto",
            Yaw = N("yaw", -35), Pitch = N("pitch", 28), Zoom = N("zoom", 1),
            Voxel = N("voxel", 0.02), Radius = N("radius", 0.05), MinNeighbors = N("minN", 3), FloorTol = N("floorTol", 0.02), Angle = N("angle", 90),
            UnitFrom = (string?)o["unitFrom"] ?? "mm", UnitTo = (string?)o["unitTo"] ?? "m",
        };
        if (o["box"] is JsonArray b && b.Count == 6) s.Box = b.Select(x => x?.GetValue<double>() ?? 0).ToArray();
        if (o["ops"] is JsonArray ops)
            foreach (var x in ops.OfType<JsonObject>())
                s.Ops.Add(new((string?)x["kind"] ?? "", (x["args"] as JsonArray)?.Select(a => a?.GetValue<double>() ?? 0).ToArray() ?? [], (string?)x["path"]));
        return s;
    }

    public void WriteTo(JsonObject o)
    {
        o["source"] = Source; o["colorBy"] = ColorBy; o["yaw"] = Yaw; o["pitch"] = Pitch; o["zoom"] = Zoom;
        o["voxel"] = Voxel; o["radius"] = Radius; o["minN"] = MinNeighbors; o["floorTol"] = FloorTol; o["angle"] = Angle; o["unitFrom"] = UnitFrom; o["unitTo"] = UnitTo;
        o["box"] = new JsonArray([.. Box.Select(x => (JsonNode)x)]);
        o["ops"] = new JsonArray([.. Ops.Select(x => (JsonNode)new JsonObject { ["kind"] = x.Kind, ["args"] = new JsonArray([.. x.Args.Select(a => (JsonNode)a)]), ["path"] = x.Path })]);
    }

    /// <summary>Schlüssel für den Zwischenspeicher: ändert sich, sobald Quelle oder Schritte sich ändern.</summary>
    public string Fingerprint() => Source + "|" + string.Join(";", Ops.Select(o => o.Kind + ":" + string.Join(",", o.Args.Select(a => a.ToString("R", CultureInfo.InvariantCulture))) + ":" + o.Path))
        + "|" + (File.Exists(Source) ? File.GetLastWriteTimeUtc(Source).Ticks : 0);

    public PointCloud LoadSource() => Source == DemoSource ? Demo() : Source.Length == 0 ? throw new ArgumentException("Noch keine Scan-Datei gewählt.") : PointCloud.Load(Source);

    /// <summary>Quelle laden und alle Schritte der Reihe nach anwenden.</summary>
    public PointCloud Build()
    {
        var pc = LoadSource();
        foreach (var op in Ops)
        {
            pc = op.Apply(pc);
            if (pc.Points.Count == 0) throw new InvalidOperationException($"Nach „{op.Describe()}“ bleibt kein Punkt übrig – Schritt entfernen oder Werte ändern.");
        }
        return pc;
    }

    /// <summary>Beispielraum (erfunden, reproduzierbar): Boden 4 × 3 m, Kiste, Kugel, Wandstück und ein paar Störpunkte – in Metern, mit Farben.</summary>
    public static PointCloud Demo(int seed = 7)
    {
        var r = new Random(seed); var pc = new PointCloud(); var col = new List<(byte, byte, byte)>();
        double Noise() => (r.NextDouble() - 0.5) * 0.006;
        void Add(double x, double y, double z, (byte, byte, byte) c) { pc.Points.Add(new(x + Noise(), y + Noise(), z + Noise())); col.Add(c); }
        for (var x = 0.0; x <= 4.0001; x += 0.04) for (var y = 0.0; y <= 3.0001; y += 0.04) Add(x, y, 0, ((byte)(150 + r.Next(20)), (byte)(120 + r.Next(15)), (byte)(90 + r.Next(10)))); // Holzboden
        for (var x = 0.0; x <= 4.0001; x += 0.04) for (var z = 0.04; z <= 1.2001; z += 0.04) Add(x, 3, z, (218, 220, 214)); // Wandstück
        // Kiste 0,6 × 0,4 × 0,5 m bei (1, 1)
        for (var a = 0.0; a <= 0.6001; a += 0.03) for (var b = 0.0; b <= 0.4001; b += 0.03) Add(1 + a, 1 + b, 0.5, (196, 140, 64));
        for (var a = 0.0; a <= 0.6001; a += 0.03) for (var z = 0.03; z < 0.5; z += 0.03) { Add(1 + a, 1, z, (176, 120, 50)); Add(1 + a, 1.4, z, (176, 120, 50)); }
        for (var b = 0.0; b <= 0.4001; b += 0.03) for (var z = 0.03; z < 0.5; z += 0.03) { Add(1, 1 + b, z, (160, 110, 45)); Add(1.6, 1 + b, z, (160, 110, 45)); }
        // Kugel r = 0,3 m bei (2,8 | 1,6), liegt auf dem Boden
        const int sphere = 1200; var golden = Math.PI * (3 - Math.Sqrt(5)); // gleichmäßig verteilt (Fibonacci-Kugel)
        for (var i = 0; i < sphere; i++) { var u = 1 - 2 * (i + 0.5) / sphere; var t = golden * i; var s = Math.Sqrt(1 - u * u); Add(2.8 + 0.3 * s * Math.Cos(t), 1.6 + 0.3 * s * Math.Sin(t), 0.3 + 0.3 * u, (52, 120, 220)); }
        for (var i = 0; i < 40; i++) Add(0.3 + r.NextDouble() * 3.4, 0.2 + r.NextDouble() * 2.4, 0.8 + r.NextDouble() * 1.4, (255, 60, 60)); // Störpunkte frei im Raum (Staub, Spiegelungen)
        pc.Colors = col; return pc;
    }
}
