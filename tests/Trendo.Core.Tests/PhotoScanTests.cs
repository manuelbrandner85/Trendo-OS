using System.Text;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

public class PhotoScanTests
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-scan-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    static void Touch(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, [1]);
    }

    [Fact(DisplayName = "SPEC-09 Foto: Ordner listet Bilder, zu wenige Fotos werden abgelehnt")]
    public void Listet_Bilder()
    {
        var d = Tmp();
        File.WriteAllText(Path.Combine(d, "b.JPG"), "x");
        File.WriteAllText(Path.Combine(d, "a.png"), "x");
        File.WriteAllText(Path.Combine(d, "notiz.txt"), "x");
        var list = PhotoScan.ListImages(d);
        Assert.Equal(2, list.Count);
        Assert.EndsWith("a.png", list[0]);
        Assert.EndsWith("b.JPG", list[1]);
        var work = Path.Combine(d, "arbeit");
        var ex = Assert.Throws<InvalidOperationException>(() => PhotoScan.Steps(new(work, d, null, PhotoEngine.Colmap), "colmap", null));
        Assert.Contains("mindestens 3", ex.Message);
    }

    [Fact(DisplayName = "SPEC-09 Foto: COLMAP-Plan ohne Grafikkarte, Video nutzt FFmpeg und die Reihe")]
    public void Plan_colmap_und_video()
    {
        var d = Tmp();
        var photos = Path.Combine(d, "fotos"); Directory.CreateDirectory(photos);
        foreach (var n in new[] { "1.jpg", "2.jpg", "3.jpg" }) File.WriteAllText(Path.Combine(photos, n), "x");
        var work = Path.Combine(d, "arbeit");
        var steps = PhotoScan.Steps(new(work, photos, null, PhotoEngine.Colmap), "/opt/colmap", null);
        var args = string.Join("\n", steps.Select(s => s.Args));
        Assert.Contains("feature_extractor", args);
        Assert.Contains("exhaustive_matcher", args);
        Assert.Contains("SiftExtraction.use_gpu 0", args);
        Assert.Contains("SiftMatching.use_gpu 0", args);
        Assert.DoesNotContain("patch_match", args);
        Assert.Contains(PhotoScan.ModelMark, args);
        Assert.Equal("Oberfläche bauen", steps[^1].Title);

        var video = Path.Combine(d, "rundgang.mp4"); File.WriteAllText(video, "x");
        var vsteps = PhotoScan.Steps(new(work, null, video, PhotoEngine.Colmap), "/opt/colmap", "/usr/bin/ffmpeg");
        Assert.Equal("Bilder aus dem Video holen", vsteps[0].Title);
        Assert.Contains("fps=2", vsteps[0].Args);
        Assert.Contains("sequential_matcher", string.Join("\n", vsteps.Select(s => s.Args)));
        Assert.DoesNotContain("exhaustive_matcher", string.Join("\n", vsteps.Select(s => s.Args)));
        Assert.Throws<InvalidOperationException>(() => PhotoScan.Steps(new(work, null, video, PhotoEngine.Colmap), "/opt/colmap", null));
        var note = Path.Combine(d, "notiz.txt"); File.WriteAllText(note, "x");
        Assert.Throws<InvalidOperationException>(() => PhotoScan.Steps(new(work, null, note, PhotoEngine.Colmap), "/opt/colmap", "ffmpeg"));
    }

    [Fact(DisplayName = "SPEC-09 Foto: Meshroom bekommt Eingabe und Ausgabe, Arbeitsordner liegt beim Programm")]
    public void Plan_meshroom()
    {
        var d = Tmp();
        var photos = Path.Combine(d, "fotos"); Directory.CreateDirectory(photos);
        foreach (var n in new[] { "1.jpg", "2.jpg", "3.jpg" }) File.WriteAllText(Path.Combine(photos, n), "x");
        var tool = Path.Combine(d, "Meshroom", "meshroom_batch");
        var steps = PhotoScan.Steps(new(Path.Combine(d, "arbeit"), photos, null, PhotoEngine.Meshroom), tool, null);
        Assert.Single(steps);
        Assert.Contains("-i ", steps[0].Args);
        Assert.Contains("-o ", steps[0].Args);
        Assert.Equal(Path.Combine(d, "Meshroom"), steps[0].WorkDir);
    }

    [Fact(DisplayName = "SPEC-09 Foto: Lauf schreibt OBJ fürs 3D-Studio, Abbruch bei Fehler und ohne Kameramodell")]
    public async Task Lauf_und_fehler()
    {
        var d = Tmp();
        var photos = Path.Combine(d, "fotos"); Directory.CreateDirectory(photos);
        foreach (var n in new[] { "1.jpg", "2.jpg", "3.jpg" }) File.WriteAllText(Path.Combine(photos, n), "x");
        var work = Path.Combine(d, "arbeit");
        var calls = new List<string>();
        var progress = new List<string>();
        var run = new ScanFake((file, args) =>
        {
            calls.Add(args);
            if (args.Contains("mapper"))
            {
                var sparse = Path.Combine(work, "sparse", "0");
                Directory.CreateDirectory(sparse);
                File.WriteAllText(Path.Combine(sparse, "cameras.bin"), "cams");
            }
            if (args.Contains("delaunay_mesher"))
            {
                var ply = Path.Combine(work, "dicht", "meshed-delaunay.ply");
                Directory.CreateDirectory(Path.GetDirectoryName(ply)!);
                File.WriteAllText(ply, TetraPly());
            }
            return (0, "Schritt ok");
        });
        var result = await PhotoScan.RunAsync(new(work, photos, null, PhotoEngine.Colmap), "/opt/colmap", null, run, progress.Add);
        Assert.True(File.Exists(result.ObjPath));
        Assert.False(result.OuterShell);
        Assert.Contains("Merkmale in den Fotos suchen", progress);
        Assert.Contains("Oberfläche bauen", progress);
        Assert.Contains(Path.Combine(work, "sparse", "0"), calls.First(a => a.Contains("image_undistorter")));
        var part = Mesh3D.Import(result.ObjPath);
        Assert.Equal(4, part.Indices!.Length / 3);
        Assert.Equal(20, Longest(result.ObjPath), 3);

        var fail = new ScanFake((_, _) => (1, "kein gemeinsamer Punkt"));
        var bad = Path.Combine(d, "leer");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => PhotoScan.RunAsync(new(bad, photos, null, PhotoEngine.Colmap), "/opt/colmap", null, fail));
        Assert.Contains("Merkmale in den Fotos suchen", ex.Message);
        Assert.Contains("kein gemeinsamer Punkt", ex.Message);

        var half = new ScanFake((file, args) => args.Contains("mapper") ? (0, "ok") : (0, "ok"));
        var noModel = await Assert.ThrowsAsync<InvalidOperationException>(() => PhotoScan.RunAsync(new(Path.Combine(d, "ohne"), photos, null, PhotoEngine.Colmap), "/opt/colmap", null, half));
        Assert.Contains("kein gemeinsames Modell", noModel.Message);
    }

    [Fact(DisplayName = "SPEC-09 Foto: Fläche aus PLY, Hülle aus Punkten, Binärformat, Quadrat, Linie")]
    public void Flaeche_und_huelle()
    {
        var d = Tmp();
        var tetra = Path.Combine(d, "tetra.ply"); File.WriteAllText(tetra, TetraPly());
        var (mesh, shell) = PhotoScan.LoadSurface(tetra);
        Assert.False(shell);
        Assert.Equal(4, mesh.Triangles);
        Assert.Equal(20, Side(mesh), 6);

        var bin = Path.Combine(d, "bin.ply"); File.WriteAllBytes(bin, BinaryTetra());
        var (bmesh, bshell) = PhotoScan.LoadSurface(bin);
        Assert.False(bshell);
        Assert.Equal(4, bmesh.Triangles);

        var cube = Path.Combine(d, "wuerfel.ply");
        File.WriteAllText(cube, PointsPly(
            (0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0),
            (0, 0, 1), (1, 0, 1), (1, 1, 1), (0, 1, 1),
            (0.5, 0.5, 0.5)));
        var (hull, outer) = PhotoScan.LoadSurface(cube);
        Assert.True(outer);
        Assert.InRange(hull.Triangles, 12, 24);
        Assert.Equal(20, Side(hull), 6);

        var flat = Path.Combine(d, "quadrat.ply");
        File.WriteAllText(flat, PointsPly((0, 0, 0), (2, 0, 0), (2, 1, 0), (0, 1, 0)));
        Assert.Equal(2, PhotoScan.LoadSurface(flat).Mesh.Triangles);

        var line = Path.Combine(d, "linie.ply");
        File.WriteAllText(line, PointsPly((0, 0, 0), (1, 0, 0), (2, 0, 0)));
        Assert.Contains("Linie", Assert.Throws<InvalidOperationException>(() => PhotoScan.LoadSurface(line)).Message);
    }

    [Fact(DisplayName = "SPEC-09 Katalog: COLMAP als geprüftes Windows-ZIP, Meshroom nur über die offizielle Seite")]
    public void Katalog()
    {
        var c = DependencyCatalog.Get("colmap");
        Assert.Equal(DeliveryMode.AutoUser, c.Mode);
        Assert.Equal("c493d88cb4a43f21afba42cb55bf786e5434a0420e73db0ba1454c5d8b3686ae", c.PortableSha256);
        Assert.EndsWith("colmap-x64-windows-nocuda.zip", c.PortableZip);
        Assert.Contains("/usr/bin/colmap", c.DetectPaths!);
        var m = DependencyCatalog.Get("meshroom");
        Assert.Equal(DeliveryMode.Guided, m.Mode);
        Assert.Null(m.WingetId);
        Assert.Contains("meshroom", StudioTools.Verify.Keys);
        Assert.Contains("colmap", StudioTools.Verify.Keys);
    }

    static string TetraPly() => """
        ply
        format ascii 1.0
        element vertex 4
        property float x
        property float y
        property float z
        element face 4
        property list uchar int vertex_indices
        end_header
        0 0 0
        1 0 0
        0 1 0
        0 0 1
        3 0 1 2
        3 0 2 3
        3 0 3 1
        3 1 3 2
        """;

    static string PointsPly(params (double X, double Y, double Z)[] pts)
    {
        var sb = new StringBuilder();
        sb.Append("ply\nformat ascii 1.0\nelement vertex ").Append(pts.Length).Append("\nproperty float x\nproperty float y\nproperty float z\nend_header\n");
        foreach (var p in pts) sb.Append(FormattableString.Invariant($"{p.X} {p.Y} {p.Z}\n"));
        return sb.ToString();
    }

    static byte[] BinaryTetra()
    {
        var header = "ply\nformat binary_little_endian 1.0\nelement vertex 4\nproperty float x\nproperty float y\nproperty float z\nproperty float nx\nproperty float ny\nproperty float nz\nelement face 4\nproperty list uchar int vertex_indices\nend_header\n";
        using var ms = new MemoryStream();
        ms.Write(Encoding.ASCII.GetBytes(header));
        void V(float x, float y, float z)
        {
            ms.Write(BitConverter.GetBytes(x)); ms.Write(BitConverter.GetBytes(y)); ms.Write(BitConverter.GetBytes(z));
            ms.Write(BitConverter.GetBytes(0f)); ms.Write(BitConverter.GetBytes(0f)); ms.Write(BitConverter.GetBytes(1f));
        }
        V(0, 0, 0); V(1, 0, 0); V(0, 1, 0); V(0, 0, 1);
        void Face(int a, int b, int c)
        {
            ms.WriteByte(3);
            ms.Write(BitConverter.GetBytes(a)); ms.Write(BitConverter.GetBytes(b)); ms.Write(BitConverter.GetBytes(c));
        }
        Face(0, 1, 2); Face(0, 2, 3); Face(0, 3, 1); Face(1, 3, 2);
        return ms.ToArray();
    }

    static double Side(Mesh mesh)
    {
        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
        foreach (var v in mesh.V)
        {
            minX = Math.Min(minX, v.X); minY = Math.Min(minY, v.Y); minZ = Math.Min(minZ, v.Z);
            maxX = Math.Max(maxX, v.X); maxY = Math.Max(maxY, v.Y); maxZ = Math.Max(maxZ, v.Z);
        }
        return Math.Max(maxX - minX, Math.Max(maxY - minY, maxZ - minZ));
    }

    static double Longest(string obj)
    {
        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
        foreach (var raw in File.ReadLines(obj))
        {
            var l = raw.Trim();
            if (!l.StartsWith("v ")) continue;
            var p = l.Split(' ');
            var x = double.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture);
            var y = double.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture);
            var z = double.Parse(p[3], System.Globalization.CultureInfo.InvariantCulture);
            minX = Math.Min(minX, x); minY = Math.Min(minY, y); minZ = Math.Min(minZ, z);
            maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); maxZ = Math.Max(maxZ, z);
        }
        return Math.Max(maxX - minX, Math.Max(maxY - minY, maxZ - minZ));
    }

    sealed class ScanFake(Func<string, string, (int Exit, string Output)> run) : IProcessRunner
    {
        public Task<(int Exit, string Output)> RunAsync(string file, string args, Action<string>? line = null, CancellationToken ct = default)
        {
            line?.Invoke("ok");
            var r = run(file, args);
            return Task.FromResult(r);
        }
    }
}
