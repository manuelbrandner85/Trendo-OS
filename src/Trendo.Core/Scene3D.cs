using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>Ein selbst gebautes 3D-Objekt: mehrere Teile, Maße in Zentimetern, Z nach oben.</summary>
public sealed partial class Scene3D
{
    public string Name { get; set; } = "Mein Objekt";
    public string? ProjectId { get; set; }
    public List<Part3D> Parts { get; set; } = [];

    public static readonly (string Id, string Label)[] PartShapes =
    [
        ("cube", "Würfel"), ("sphere", "Kugel"), ("cylinder", "Zylinder"), ("cone", "Kegel"),
        ("torus", "Torus"), ("plane", "Ebene"), ("text", "Text"),
    ];

    public static readonly (string Name, string Hex)[] Colors =
    [
        ("Blau", "#2F86F6"), ("Rot", "#E23B3B"), ("Grün", "#2E9B4F"), ("Gelb", "#E2B33B"),
        ("Orange", "#E27A2F"), ("Braun", "#8B5A2B"), ("Holz", "#C4A574"), ("Weiß", "#F4F4F4"),
        ("Grau", "#8A8F98"), ("Schwarz", "#22262C"),
    ];

    static readonly JsonSerializerOptions Opt = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    public string ToJson() => JsonSerializer.Serialize(this, Opt);

    public static Scene3D FromJson(string json)
    {
        var s = JsonSerializer.Deserialize<Scene3D>(json, Opt) ?? new Scene3D();
        s.Name = string.IsNullOrWhiteSpace(s.Name) ? "Mein Objekt" : s.Name.Trim();
        s.Parts ??= [];
        foreach (var p in s.Parts)
        {
            p.Ops ??= [];
            p.Name = string.IsNullOrWhiteSpace(p.Name) ? "Teil" : p.Name.Trim();
            p.Shape = string.IsNullOrWhiteSpace(p.Shape) ? "cube" : p.Shape;
            p.Color = ValidColor(p.Color) ? p.Color : "#2F86F6";
            p.Id = string.IsNullOrWhiteSpace(p.Id) ? NewId() : p.Id;
            p.Text ??= "";
        }
        return s;
    }

    public static bool ValidColor(string? hex) => hex is not null && Hex().IsMatch(hex);

    public static string NewId() => Guid.NewGuid().ToString("N")[..8];

    /// <summary>Projekt-ID aus dem Namen (a-z, 0-9, Bindestrich, 2–48 Zeichen).</summary>
    public static string Slug(string name)
    {
        var s = name.Trim().ToLowerInvariant();
        s = s.Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss");
        var b = new string(s.Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-').ToArray());
        while (b.Contains("--", StringComparison.Ordinal)) b = b.Replace("--", "-", StringComparison.Ordinal);
        b = b.Trim('-');
        if (b.Length > 40) b = b[..40].Trim('-');
        if (b.Length < 2 || !char.IsAsciiLetterOrDigit(b[0])) b = "objekt-" + b;
        b = b.Trim('-');
        return b.Length < 2 ? "objekt-3d" : b;
    }

    public static string ShapeLabel(string id) => PartShapes.FirstOrDefault(s => s.Id == id).Label is { Length: > 0 } l ? l : "Datei";

    public string WriteBlenderSpec(string dir)
    {
        Directory.CreateDirectory(dir);
        var parts = new List<object>();
        foreach (var p in Parts)
        {
            string? file = null;
            if (p.Shape == "mesh" && p.Positions is { Length: >= 9 } && p.Indices is { Length: >= 3 })
            {
                file = Path.Combine(dir, p.Id + ".obj");
                Mesh3D.WriteObj(Mesh3D.FromStored(p), file);
            }
            parts.Add(new
            {
                name = p.Name,
                shape = p.Shape,
                sx = p.Sx,
                sy = p.Sy,
                sz = p.Sz,
                x = p.X,
                y = p.Y,
                z = p.Z,
                rx = p.Rx,
                ry = p.Ry,
                rz = p.Rz,
                color = p.Color,
                text = p.Text,
                baseX = p.BaseX,
                baseY = p.BaseY,
                baseZ = p.BaseZ,
                file,
                ops = p.Ops.Select(o => new { kind = o.Kind, amount = o.Amount, axis = o.Axis, diameter = o.Diameter, atX = o.AtX, atY = o.AtY, atZ = o.AtZ }),
            });
        }
        var path = Path.Combine(dir, "blender-szene.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new { name = Name, parts }));
        return path;
    }

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex Hex();
}

public sealed class Part3D
{
    public string Id { get; set; } = Scene3D.NewId();
    public string Name { get; set; } = "Teil";
    public string Shape { get; set; } = "cube";
    public double Sx { get; set; } = 10;
    public double Sy { get; set; } = 10;
    public double Sz { get; set; } = 10;
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public double Rx { get; set; }
    public double Ry { get; set; }
    public double Rz { get; set; }
    public string Color { get; set; } = "#2F86F6";
    public string Text { get; set; } = "";
    public double BaseX { get; set; } = 10;
    public double BaseY { get; set; } = 10;
    public double BaseZ { get; set; } = 10;
    public float[]? Positions { get; set; }
    public int[]? Indices { get; set; }
    public List<PartOp> Ops { get; set; } = [];

    public string Line()
    {
        var ops = Ops.Count == 0 ? "" : " · " + string.Join(", ", Ops.Select(OpText));
        return $"{Name} · {Scene3D.ShapeLabel(Shape)} · {Num(Sx)} × {Num(Sy)} × {Num(Sz)} cm{ops}";
    }

    public static string OpText(PartOp o) => o.Kind switch
    {
        "bevel" => $"Kanten {Num(o.Amount)} cm",
        "smooth" => "geglättet",
        "mirror" => "Spiegel " + o.Axis.ToUpperInvariant(),
        "shell" => $"hohl {Num(o.Amount)} cm",
        "hole" => $"Loch Ø {Num(o.Diameter)} cm",
        _ => o.Kind,
    };

    public static string Num(double v) => v.ToString("0.##", CultureInfo.GetCultureInfo("de-DE"));
}

public sealed class PartOp
{
    public string Kind { get; set; } = "";
    public double Amount { get; set; }
    public string Axis { get; set; } = "z";
    public double Diameter { get; set; }
    public double AtX { get; set; }
    public double AtY { get; set; }
    public double AtZ { get; set; }
}
