using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

public class PhotoScanTests
{
    static string Dir()
    {
        var d = Path.Combine(Path.GetTempPath(), "trendo-scan-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        return d;
    }

    [Fact(DisplayName = "Fotos: mindestens drei Bilder, Video- und Rechenbefehle bleiben lokal")]
    public void Commands_need_three_photos()
    {
        var d = Dir();
        File.WriteAllText(Path.Combine(d, "a.jpg"), "x");
        File.WriteAllText(Path.Combine(d, "b.png"), "x");
        Assert.Throws<ArgumentException>(() => PhotoScan.ListPhotos(d));
        File.WriteAllText(Path.Combine(d, "c.webp"), "x");
        Assert.Equal(3, PhotoScan.ListPhotos(d).Count);
        var frames = PhotoScan.FrameArgs(Path.Combine(d, "film.mp4"), d, 2);
        Assert.Contains("fps=1/2", frames);
        Assert.EndsWith("bild-%04d.jpg", frames[^1]);
        Assert.DoesNotContain("testsrc", string.Join(" ", frames));
        var mr = PhotoScan.MeshroomArgs(d, Path.Combine(d, "out"));
        Assert.Equal(["--input", d, "--output", Path.Combine(d, "out")], mr);
        var steps = PhotoScan.ColmapSteps(d, Path.Combine(d, "work"));
        Assert.Equal("feature_extractor", steps[0][0]);
        Assert.Contains(d, steps[0]);
        Assert.Equal("poisson_mesher", steps[^1][0]);
        Assert.Equal(DeliveryMode.Guided, DependencyCatalog.Get("colmap").Mode);
        Assert.Equal(DeliveryMode.Guided, DependencyCatalog.Get("meshroom").Mode);
        Assert.Null(DependencyCatalog.Get("colmap").WingetId);
        Assert.Null(DependencyCatalog.Get("meshroom").WingetId);
        Assert.Contains("/usr/bin/colmap", DependencyCatalog.Get("colmap").DetectPaths!);
        Assert.Contains("https://github.com/alicevision/Meshroom/releases", DependencyCatalog.Get("meshroom").OfficialUrl);
        Assert.False(StudioTools.Required.ContainsKey("SPEC-09"));
        Assert.Null(StudioTools.Verify["colmap"].VerifyArgs);
        Assert.Null(StudioTools.Verify["meshroom"].VerifyArgs);
    }

    [Fact(DisplayName = "Punkte werden eine Hülle mit Dreiecken, ein vorhandenes Netz bleibt das Netz")]
    public void Points_become_a_hull_and_a_mesh_stays()
    {
        var d = Dir();
        File.WriteAllText(Path.Combine(d, "punkte.ply"), """
            ply
            format ascii 1.0
            element vertex 8
            property float x
            property float y
            property float z
            end_header
            0 0 0
            1 0 0
            1 1 0
            0 1 0
            0 0 1
            1 0 1
            1 1 1
            0 1 1
            """);
        var (obj, hull) = PhotoScan.BuildObj(d);
        Assert.True(hull);
        var part = Mesh3D.Import(obj);
        Assert.Equal(12, part.Indices!.Length / 3);
        var flat = Dir();
        File.WriteAllText(Path.Combine(flat, "netz.ply"), """
            ply
            format ascii 1.0
            element vertex 3
            property float x
            property float y
            property float z
            element face 1
            property list uchar int vertex_indices
            end_header
            0 0 0
            2 0 0
            0 2 0
            3 0 1 2
            """);
        var (mesh, fromPoints) = PhotoScan.BuildObj(flat);
        Assert.False(fromPoints);
        Assert.Equal(1, Mesh3D.Import(mesh).Indices!.Length / 3);
        var square = PhotoScan.Hull([new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0), new(0.5, 0.5, 0)]);
        Assert.Equal(2, square.F.Count);
        var tetra = PhotoScan.Hull([new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1)]);
        Assert.Equal(4, tetra.F.Count);
        var center = new Vec3(0.25, 0.25, 0.25);
        foreach (var t in tetra.F)
        {
            var n = Vec3.Cross(tetra.V[t.B] - tetra.V[t.A], tetra.V[t.C] - tetra.V[t.A]);
            Assert.True(Vec3.Dot(n, center - tetra.V[t.A]) < 0);
        }
    }

    [Fact(DisplayName = "COLMAP und Meshroom werden nicht automatisch installiert")]
    public async Task Guided_tools_are_not_installed()
    {
        var f = new Fixture(); using var _ = f;
        var m = new DependencyManager(f.Core.Audit, new NoRun(), p => p, _ => false) { ConsentFile = Path.Combine(f.Core.DataRoot, "c.txt") };
        m.GiveConsent(Principal.LocalOwner);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => m.InstallAsync(Principal.LocalOwner, DependencyCatalog.Get("colmap")));
        Assert.Contains("nicht automatisch", ex.Message);
        ex = await Assert.ThrowsAsync<InvalidOperationException>(() => m.InstallAsync(Principal.LocalOwner, DependencyCatalog.Get("meshroom")));
        Assert.Contains("nicht automatisch", ex.Message);
    }

    sealed class NoRun : IProcessRunner
    {
        public Task<(int Exit, string Output)> RunAsync(string file, string args, Action<string>? line = null, CancellationToken ct = default) =>
            Task.FromResult((0, ""));
    }
}
