using System.Text;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Einscannen (Profi 4 + Claude 4) und VR & AR (Profi 3 + Claude 3): Kameras, Beschneiden, Reste, Löcher, echte Größe, Fotoauswahl, Brillen-Reparatur.</summary>
public class ScanXrProTests
{
    static Mesh Cube(double s, Vec3 at)
    {
        var m = new Mesh(); Vec3 P(int x, int y, int z) => new(at.X + x * s, at.Y + y * s, at.Z + z * s);
        void Q(Vec3 a, Vec3 b, Vec3 c, Vec3 d) { m.Add(a, b, c); m.Add(a, c, d); }
        Q(P(0, 0, 0), P(0, 1, 0), P(1, 1, 0), P(1, 0, 0)); Q(P(0, 0, 1), P(1, 0, 1), P(1, 1, 1), P(0, 1, 1));
        Q(P(0, 0, 0), P(1, 0, 0), P(1, 0, 1), P(0, 0, 1)); Q(P(0, 1, 0), P(0, 1, 1), P(1, 1, 1), P(1, 1, 0));
        Q(P(0, 0, 0), P(0, 0, 1), P(0, 1, 1), P(0, 1, 0)); Q(P(1, 0, 0), P(1, 1, 0), P(1, 1, 1), P(1, 0, 1));
        return m;
    }

    [Fact(DisplayName = "SPEC-09 Scan: Kameras aus COLMAP (images.txt und images.bin) – Kameramitte C = −Rᵀt, in die Lage des Modells umgerechnet und gespeichert; Meshroom cameras.sfm")]
    public void Cameras()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-scanpro-" + Guid.NewGuid().ToString("N")[..6]); var sp = Path.Combine(dir, "sparse", "0"); Directory.CreateDirectory(sp);
        try
        {
            File.WriteAllText(Path.Combine(sp, "cameras.txt"), "# x\n");
            // Drehung keine (qw=1), t = (0,0,-10) → Kamera bei (0,0,10)
            File.WriteAllText(Path.Combine(sp, "images.txt"), "# Bilder\n1 1 0 0 0 0 0 -10 1 bild1.jpg\n\n2 1 0 0 0 -5 0 0 1 bild2.jpg\n\n");
            var cams = ScanPro.FindCameras(dir);
            Assert.Equal(2, cams.Count); Assert.Equal((0d, 0d, 10d), (cams[0].Center.X, cams[0].Center.Y, cams[0].Center.Z)); Assert.Equal(5, cams[1].Center.X, 6);
            Assert.Equal(2, ScanPro.SaveCameras(dir, new Vec3(0, 0, 5), 2)); // gleiche Mitte/Skalierung wie das Modell
            Assert.Equal(10, ScanPro.LoadCameras(dir)[0].Center.Z, 6);
            // Binärform
            File.Delete(Path.Combine(sp, "images.txt"));
            using (var w = new BinaryWriter(File.Create(Path.Combine(sp, "images.bin"))))
            {
                w.Write(1UL); w.Write(7u); foreach (var v in new[] { 1.0, 0, 0, 0, 0, -3, 0 }) w.Write(v); w.Write(1u); w.Write(Encoding.ASCII.GetBytes("a.jpg")); w.Write((byte)0); w.Write(1UL); w.Write(1.0); w.Write(2.0); w.Write(-1L);
            }
            Assert.Equal(3, Assert.Single(ScanPro.FindCameras(dir)).Center.Y, 6);
            var sfm = Path.Combine(dir, "mr", "cameras.sfm"); Directory.CreateDirectory(Path.GetDirectoryName(sfm)!);
            File.WriteAllText(sfm, """{"views":[{"poseId":"9","path":"C:/f/x.jpg"}],"poses":[{"poseId":"9","pose":{"transform":{"rotation":["1","0","0","0","1","0","0","0","1"],"center":["1.5","2","3"]}}}]}""");
            var mr = Assert.Single(ScanPro.ReadMeshroomSfm(sfm)); Assert.Equal(("x.jpg", 1.5), (mr.Name, mr.Center.X));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "SPEC-09 Scan: Beschneiden (unten weg), kleine Reste entfernen (größtes Teil bleibt), Löcher schließen (offener Würfel wird dicht), echte Größe direkt und über einen bekannten Gegenstand")]
    public void Edit_mesh()
    {
        var big = Cube(10, new(0, 0, 0)); var crumb = Cube(0.5, new(30, 30, 30)); big.Append(crumb);
        Assert.Equal(12, ScanPro.KeepLargest(big).Triangles);
        var tall = Cube(10, new(0, 0, 0));
        var cropped = ScanPro.Crop(tall, (0, 1), (0, 1), (0.2, 1)); // Boden (z=0) fällt weg
        Assert.True(cropped.Triangles < 12); Assert.Throws<InvalidOperationException>(() => ScanPro.Crop(tall, (0, 0.01), (0, 0.01), (0.99, 1)));
        var open = Cube(10, new(0, 0, 0)); open.I.RemoveRange(0, 6); // Bodenquadrat fehlt → ein Loch mit 4 Kanten
        var (closed, holes) = ScanPro.FillHoles(open);
        Assert.Equal(1, holes); Assert.Equal(10 + 4, closed.Triangles);
        Assert.Equal(0, ScanPro.FillHoles(Cube(10, new(0, 0, 0))).Holes); // dichtes Netz bleibt
        var sized = ScanPro.ScaleTo(Cube(20, new(-10, -10, 0)), 'z', 45);
        var (lo, hi) = ScanPro.Bounds(sized); Assert.Equal(45, hi.Z - lo.Z, 6); Assert.Equal(0, lo.Z, 6);
        var byRef = ScanPro.ScaleByReference(Cube(20, new(0, 0, 0)), measuredInModelCm: 4, realCm: 8.56); // Kreditkarte im Modell 4 cm lang → Faktor 2,14
        Assert.Equal(42.8, ScanPro.Bounds(byRef).Max.X - ScanPro.Bounds(byRef).Min.X, 6);
        Assert.Contains(ScanPro.References, r => r.Name.StartsWith("Kreditkarte") && r.Cm == 8.56);
    }

    [Fact(DisplayName = "SPEC-09 Scan: gute Fotos auswählen – wahrscheinlich unscharfe (wenig Bilddetails) und doppelte werden aussortiert; Meshroom bekommt die gewählte Texturgröße")]
    public void Photos_and_texture()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-fotos-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            string F(string n, int bytes) { var p = Path.Combine(dir, n); File.WriteAllBytes(p, new byte[bytes]); return p; }
            var files = new[] { F("a.jpg", 900_000), F("b.jpg", 1_000_000), F("c.jpg", 300_000), F("d.jpg", 950_000), F("e.jpg", 950_100) };
            var r = ScanPro.RankPhotos(files, _ => (1000, 1000));
            Assert.Equal([true, true, false, true, false], r.Select(x => x.Keep));
            Assert.Contains("unscharf", r[2].Why); Assert.Contains("doppelt", r[4].Why);
            var steps = PhotoScan.Steps(new PhotoRequest(Path.Combine(dir, "w"), dir, null, PhotoEngine.Meshroom, 8192), "meshroom_batch.exe", null);
            Assert.Contains("Texturing:textureSide=8192", steps[0].Args);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "SPEC-04 VR: Brillen-Reparatur – Dreiecke auf 90 % des Budgets, Texturen auf die Gerätegrenze, Zentimeter-Modell auf Meter; Blender-Skript mit Dezimieren, Bildverkleinern, GLB-Export; passendes Modell braucht nichts")]
    public void Xr_fix_plan()
    {
        var info = new XrModelInfo("sofa.glb", 50L << 20, 3, 4, 1_000_000, 600_000, 2, 2, 5, 0, "");
        var det = new XrDetail(info, 2000, 900, 800, [], [], [], [new XrTexture("holz", "image/png", 4096, 4096, 64L << 20), new XrTexture("stoff", "image/png", 1024, 1024, 4L << 20)], new Dictionary<string, int>(), [], []);
        var plan = XrPro.Plan(info, det, 1); // Quest 2: 400 000 Dreiecke, Texturen bis 1024
        Assert.Equal(0.36, plan.DecimateRatio, 3); Assert.Equal(1024, plan.MaxTexture); Assert.Equal(0.001, plan.ScaleToMeters); // 2000 „m“ = 2 m in Millimetern
        Assert.True(plan.Needed); Assert.Contains(plan.Steps, s => s.StartsWith("Texturen verkleinern: 1"));
        var py = XrPro.FixScript("C:/m/sofa.glb", "C:/m/sofa-brille.glb", plan);
        Assert.Contains("'DECIMATE'", py); Assert.Contains("m.ratio = max(0.01, r)", py); Assert.Contains("img.scale(", py); Assert.Contains("export_format='GLB'", py); Assert.Contains("s = 0.001", py);
        var ok = new XrModelInfo("klein.glb", 1L << 20, 1, 1, 10_000, 5_000, 1, 1, 1, 0, "");
        var okDet = new XrDetail(ok, 0.5, 0.4, 0.3, [], [], [], [new XrTexture("t", "image/png", 1024, 1024, 4L << 20)], new Dictionary<string, int>(), [], []);
        Assert.False(XrPro.Plan(ok, okDet, 0).Needed);
        Assert.Equal(4096, XrPro.MaxTextureSide(3));
    }
}
