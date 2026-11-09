namespace Trendo.Core.Statik;

/// <summary>SPEC-03: Tragwerk als 3D-Körper. Biegung stark überhöht, Farbe zeigt die Belastung: Grundfarbe des Werkstoffs bis Rot beim größten Moment.</summary>
public static class Frame3D
{
    public static (byte R, byte G, byte B) MaterialColor(string material) => material.ToLowerInvariant() switch
    {
        var m when m.Contains("holz") || m.Contains("fichte") => (176, 132, 82),
        var m when m.Contains("beton") => (168, 166, 160),
        var m when m.Contains("alu") => (196, 200, 205),
        _ => (120, 128, 138),
    };

    public static (byte R, byte G, byte B) Stress((byte R, byte G, byte B) basis, double share)
    {
        var t = Math.Clamp(share, 0, 1);
        return ((byte)(basis.R + (225 - basis.R) * t), (byte)(basis.G + (40 - basis.G) * t), (byte)(basis.B + (35 - basis.B) * t));
    }

    /// <summary>Überhöhung so, dass die größte Verschiebung 8 % der längsten Bauteilachse erreicht.</summary>
    public static double Exaggeration(FrameModel model, FrameResult result)
    {
        var max = result.Members.SelectMany(m => m.Dx.Zip(m.Dy, (x, y) => Math.Sqrt(x * x + y * y))).DefaultIfEmpty(0).Max();
        var longest = result.Members.Select(m => m.L).DefaultIfEmpty(1).Max();
        return max < 1e-12 ? 0 : longest * 0.08 / max;
    }

    public static Mesh Build(FrameModel model, FrameResult result)
    {
        var mesh = new Mesh();
        var k = Exaggeration(model, result);
        var maxMoment = Math.Max(1e-9, result.Members.SelectMany(m => m.M).Select(Math.Abs).DefaultIfEmpty(0).Max());
        foreach (var r in result.Members)
        {
            var member = model.Member(r.Id); if (member is null) continue;
            var a = model.Node(member.A)!; var b = model.Node(member.B)!;
            double c = (b.X - a.X) / r.L, s = (b.Y - a.Y) / r.L;
            var sec = Sections.Get(member.Section);
            double h = Math.Max(0.02, sec.H_mm / 1000), halfH = h / 2, halfW = h * 0.3;
            var basis = MaterialColor(member.Material);
            Vec3 Corner(int i, double up, double side)
            {
                double x = a.X + c * r.Xs[i] + r.Dx[i] * k, y = a.Y + s * r.Xs[i] + r.Dy[i] * k;
                return new Vec3((x - s * up) * 100, side * 100, (y + c * up) * 100);
            }
            for (var i = 1; i < r.Xs.Length; i++)
            {
                var color = Stress(basis, Math.Abs(r.M[i]) / maxMoment);
                mesh.CR = color.R; mesh.CG = color.G; mesh.CB = color.B;
                void Quad(Vec3 p, Vec3 q, Vec3 t, Vec3 u) { mesh.Add(p, q, t); mesh.Add(p, t, u); }
                Quad(Corner(i - 1, halfH, -halfW), Corner(i, halfH, -halfW), Corner(i, halfH, halfW), Corner(i - 1, halfH, halfW));
                Quad(Corner(i - 1, -halfH, halfW), Corner(i, -halfH, halfW), Corner(i, -halfH, -halfW), Corner(i - 1, -halfH, -halfW));
                Quad(Corner(i - 1, -halfH, -halfW), Corner(i, -halfH, -halfW), Corner(i, halfH, -halfW), Corner(i - 1, halfH, -halfW));
                Quad(Corner(i - 1, halfH, halfW), Corner(i, halfH, halfW), Corner(i, -halfH, halfW), Corner(i - 1, -halfH, halfW));
            }
        }
        return mesh;
    }
}
