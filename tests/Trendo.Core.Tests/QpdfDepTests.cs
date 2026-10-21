using System.IO.Compression;
using System.Security.Cryptography;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

public class QpdfDepTests
{
    static (DependencyManager M, string Root, string Zip, string Sha) Setup(List<string> calls)
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-qpdf-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(root);
        var src = Path.Combine(root, "src", "qpdf-12.4.2-msvc64", "bin"); Directory.CreateDirectory(src); File.WriteAllText(Path.Combine(src, "qpdf.exe"), "x");
        var zip = Path.Combine(root, "q.zip"); ZipFile.CreateFromDirectory(Path.Combine(root, "src"), zip);
        var sha = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(zip))).ToLowerInvariant();
        var runner = new FakeRunner((f, a) => { calls.Add(f + " " + a); var o = System.Text.RegularExpressions.Regex.Match(a, "-o \"([^\"]+)\"").Groups[1].Value; File.Copy(zip, o, true); return (0, ""); });
        var m = new DependencyManager(new AuditLog(Path.Combine(root, "a.jsonl")), runner) { ConsentFile = Path.Combine(root, "c.txt"), ToolsRoot = Path.Combine(root, "werkzeuge") };
        m.GiveConsent(Principal.LocalOwner);
        return (m, root, zip, sha);
    }

    [Fact]
    public void Katalog_qpdf_portable_mit_offizieller_Pruefsumme()
    {
        var c = DependencyCatalog.Get("qpdf");
        Assert.Equal(DeliveryMode.AutoUser, c.Mode); Assert.Null(c.WingetId);
        Assert.StartsWith("https://github.com/qpdf/qpdf/releases/download/", c.PortableZip);
        Assert.Equal("db87077e683630c1217e0e8f9a20a9749d952ab676e881c3689187763a5de25d", c.PortableSha256);
    }

    [Fact]
    public async Task Portable_install_nur_mit_passender_Pruefsumme_ins_Benutzerverzeichnis()
    {
        var calls = new List<string>(); var (m, root, _, sha) = Setup(calls);
        var bin = Path.Combine(root, "werkzeuge", "qpdf", "qpdf-*", "bin", "qpdf.exe");
        var good = DependencyCatalog.Get("qpdf") with { PortableSha256 = sha, DetectPaths = [bin] };
        var bad = good with { PortableSha256 = new string('0', 64) };
        Assert.False(await m.InstallAsync(Principal.LocalOwner, bad));
        Assert.False(Directory.Exists(Path.Combine(root, "werkzeuge", "qpdf")));
        Assert.True(await m.InstallAsync(Principal.LocalOwner, good));
        Assert.NotNull(m.ResolvePath(good));
        Assert.All(calls, c => Assert.Contains("--fail", c));
        Assert.Empty(Directory.GetFiles(Path.GetTempPath(), "trendo-dl-qpdf-*"));
        Directory.Delete(root, true);
    }
}
