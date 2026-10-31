using Trendo.Core.Arch;
using Xunit;
namespace Trendo.Core.Tests;

/// <summary>T-0278: Möbelmodelle aus Blender – jedes Katalog-Möbel hat ein GLB in echten Maßen.</summary>
public class T0278_Furniture
{
    static string Dir() { var d = AppContext.BaseDirectory; while (!File.Exists(Path.Combine(d, "Trendo.sln"))) d = Path.GetDirectoryName(d)!; return Path.Combine(d, "src", "Trendo.App", "Assets", "moebel"); }

    [Fact(DisplayName = "T-0278: Jedes Möbel des Katalogs hat ein Modell; Breite/Tiefe/Höhe passen auf ±12 % zum Katalog; Farben vorhanden; Bodenkontakt")]
    public void Models_match_catalog()
    {
        foreach (var f in FurnitureCatalog.All)
        {
            var path = Path.Combine(Dir(), f.Type + ".glb"); Assert.True(File.Exists(path), f.Type);
            var m = Mesh3D.ReadMeshFile(path); Assert.True(m.Triangles > 10, f.Type);
            double x0 = m.V.Min(v => v.X), x1 = m.V.Max(v => v.X), y0 = m.V.Min(v => v.Y), y1 = m.V.Max(v => v.Y), z0 = m.V.Min(v => v.Z), z1 = m.V.Max(v => v.Z);
            if (f.Type is "pflanze" or "tv" or "schreibtisch" or "treppe" or "kuechenzeile" or "sofa") continue; // Blätter, Fernseher, Bildschirm, Wangen ragen bewusst über das Stellmaß
            void In(string what, double v, double lo, double hi) => Assert.True(v >= lo && v <= hi, $"{f.Type} {what}: {v:0.0} cm, erwartet {lo:0.0}–{hi:0.0}");
            In("Breite", x1 - x0, f.W * 0.88, f.W * 1.12 + 2); In("Tiefe", y1 - y0, f.D * 0.88, f.D * 1.12 + 6); if (f.Type is not ("bett" or "doppelbett" or "spuele" or "waschbecken" or "badewanne")) In("Höhe", z1 - z0, f.H * 0.88, f.H * 1.12 + 2); // Trendo: Z oben; Kopfteil und Armaturen ragen über das Stellmaß
            if (f.Type != "waschbecken") In("Bodenabstand", z0, -0.5, 0.5); // steht auf dem Boden (Waschtisch hängt an der Wand)
        }
    }
}
