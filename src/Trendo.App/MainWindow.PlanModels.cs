using Avalonia.Platform;
using Trendo.Core;
using Trendo.Core.Arch;

namespace Trendo.App;

/// <summary>T-0280: Echte 3D-Modelle für den Grundriss – Katalogmöbel aus Assets/moebel (Blender, Normmaße), Bibliotheksmodelle („bib:Quelle/Id“)
/// aus der 3D-Bibliothek. Für die Vorschau vereinfacht und zwischengespeichert.</summary>
public partial class MainWindow
{
    public const string BibPrefix = "bib:";
    static readonly Dictionary<string, Mesh?> _planModels = [];

    /// <summary>Datei des vollen Modells (für Foto-Ansicht und Export); null, wenn es keins gibt.</summary>
    public string? PlanModelFile(string type)
    {
        if (type.StartsWith(BibPrefix)) { var key = type[BibPrefix.Length..]; return Bib.Items().FirstOrDefault(i => i.Kind == "modell" && $"{i.Source}/{i.Id}" == key && File.Exists(i.File))?.File; }
        if (type.Length == 0 || type.Any(c => !(char.IsLetterOrDigit(c) || c == '_'))) return null;
        var f = Path.Combine(_core.DataRoot, "cache", "moebel", type + ".glb");
        if (File.Exists(f)) return f;
        try
        {
            using var s = AssetLoader.Open(new Uri($"avares://TrendoOS/Assets/moebel/{type}.glb"));
            Directory.CreateDirectory(Path.GetDirectoryName(f)!); using (var o = File.Create(f)) s.CopyTo(o); return f;
        }
        catch (Exception ex) when (ex is FileNotFoundException or IOException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>Modelle der 3D-Bibliothek als Möbeltypen (Maße aus dem Modell: Breite × Tiefe × Höhe in cm) – im Katalog eingetragen, damit Stellen,
    /// Vorschau und Prüfung wie bei Katalogmöbeln funktionieren.</summary>
    public List<FurnitureType> PlanLibraryTypes()
    {
        var res = new List<FurnitureType>();
        foreach (var it in Bib.Items().Where(i => i.Kind == "modell" && File.Exists(i.File)))
        {
            var type = $"{BibPrefix}{it.Source}/{it.Id}";
            if (FurnitureCatalog.Get(type) is { } known) { res.Add(known); continue; }
            if (PlanModel(type) is not { Triangles: > 0 } m) continue;
            double D(Func<Vec3, double> f) => Math.Max(1, Math.Round(m.V.Max(f) - m.V.Min(f)));
            var ft = new FurnitureType(type, it.Name, "3D-Bibliothek", D(v => v.X), D(v => v.Y), D(v => v.Z));
            FurnitureCatalog.Register(ft); res.Add(ft);
        }
        return res;
    }

    /// <summary>Vorschau-Modell (vereinfacht auf 1,5 cm, höchstens etwa 6000 Dreiecke), je Typ einmal geladen.</summary>
    public Mesh? PlanModel(string type)
    {
        lock (_planModels)
            if (_planModels.TryGetValue(type, out var hit)) return hit;
        Mesh? m = null;
        if (PlanModelFile(type) is { } f)
            try
            {
                var full = Mesh3D.ReadMeshFile(f); var cell = 1.5; m = Plan3D.Simplify(full, cell);
                while (m.Triangles > 6000 && cell < 15) { cell *= 1.6; m = Plan3D.Simplify(full, cell); } // gescannte Modelle haben oft 100 000+ Dreiecke
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or System.Text.Json.JsonException or FormatException or ArgumentException) { m = null; }
        lock (_planModels) _planModels[type] = m;
        return m;
    }
}
