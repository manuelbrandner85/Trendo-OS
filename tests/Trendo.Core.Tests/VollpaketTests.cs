using System.IO.Compression;
using System.Text.Json;
using Trendo.Core;
using Xunit;
namespace Trendo.Core.Tests;

public class T0222_Vollpaket
{
    static string Tmp(string p) { var d = Path.Combine(Path.GetTempPath(), p + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    /// <summary>Kleines Vollpaket mit einem Paket „qpdf“ (bin/qpdf.exe) wie es tools/vollpaket/build.py erzeugt.</summary>
    static string Medium(string root, string? sha = null, string file = "qpdf.zip")
    {
        var prog = Path.Combine(root, Vollpaket.FolderName); Directory.CreateDirectory(prog);
        var zip = Path.Combine(prog, "qpdf.zip");
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create))
        using (var w = new StreamWriter(z.CreateEntry("bin/qpdf.exe").Open())) w.Write("qpdf");
        var entry = new { id = "qpdf", name = "qpdf", version = "12.4.2", license = "Apache-2.0", file, sha256 = sha ?? Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(zip))), bytes = new FileInfo(zip).Length, exe = "bin/qpdf.exe", source_url = "https://github.com/qpdf/qpdf" };
        File.WriteAllText(Path.Combine(prog, Vollpaket.ManifestName), JsonSerializer.Serialize(new { schema = 1, programs = new[] { entry } }));
        return prog;
    }

    [Fact(DisplayName = "T-0222: Vollpaket richtet fehlende Programme ohne Internet ein, vorhandene bleiben unverändert")]
    public void InstallMissing()
    {
        var root = Tmp("trendo-vp-"); var tools = Path.Combine(root, "werkzeuge");
        try
        {
            var prog = Medium(root);
            var audit = new AuditLog(Path.Combine(root, "audit.jsonl"));
            Assert.Equal(1, Vollpaket.InstallMissing(prog, tools, audit));
            Assert.Equal("qpdf", File.ReadAllText(Path.Combine(tools, "qpdf", "bin", "qpdf.exe")));
            Assert.Equal(0, Vollpaket.InstallMissing(prog, tools, audit));
            Assert.Contains("vollpaket.install", File.ReadAllText(Path.Combine(root, "audit.jsonl")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "Befund 27.10.2026: ein kaputtes Paket hält die übrigen nicht auf – Bericht nennt es mit Grund")]
    public void InstallEach_continues()
    {
        var root = Tmp("trendo-vp-"); var tools = Path.Combine(root, "werkzeuge");
        try
        {
            var prog = Medium(root);
            // zweites Paket mit falscher Prüfsumme, an erster Stelle
            var zip = Path.Combine(prog, "mpv.zip");
            using (var z = ZipFile.Open(zip, ZipArchiveMode.Create)) using (var w = new StreamWriter(z.CreateEntry("mpv.exe").Open())) w.Write("mpv");
            var doc = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(prog, Vollpaket.ManifestName)))!;
            var list = doc["programs"]!.AsArray();
            list.Insert(0, new System.Text.Json.Nodes.JsonObject { ["id"] = "mpv", ["name"] = "mpv", ["version"] = "1", ["license"] = "GPL-2.0", ["file"] = "mpv.zip", ["sha256"] = new string('0', 64), ["bytes"] = new FileInfo(zip).Length, ["exe"] = "mpv.exe", ["source_url"] = "https://github.com/mpv-player/mpv" });
            File.WriteAllText(Path.Combine(prog, Vollpaket.ManifestName), doc.ToJsonString());
            var lines = new List<string>();
            var r = Vollpaket.InstallEach(prog, tools, null, lines.Add);
            Assert.Equal((1, 0, 2), (r.Installed, r.Present, r.Total));
            Assert.Equal("mpv", Assert.Single(r.Failed).Name);
            Assert.True(File.Exists(Path.Combine(tools, "qpdf", "bin", "qpdf.exe")));
            Assert.Contains(lines, l => l.StartsWith("mpv: nicht eingerichtet"));
            Assert.Equal(0, Vollpaket.InstallMissing(prog, tools)); // qpdf da, mpv weiter kaputt: kein Abbruch
            using var cts = new CancellationTokenSource(); cts.Cancel();
            var c = Vollpaket.InstallEach(prog, Path.Combine(root, "leer"), null, null, cts.Token);
            Assert.True(c.Canceled); Assert.Equal(0, c.Installed);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "T-0222: falsche Prüfsumme oder Dateiname mit Pfad – nichts wird eingerichtet")]
    public void Rejects()
    {
        var root = Tmp("trendo-vp-"); var tools = Path.Combine(root, "werkzeuge");
        try
        {
            var prog = Medium(root, sha: new string('0', 64));
            Assert.Throws<InvalidOperationException>(() => Vollpaket.InstallMissing(prog, tools));
            Assert.False(Directory.Exists(Path.Combine(tools, "qpdf")));
            var bad = Medium(Tmp("trendo-vp-"), file: "../qpdf.zip");
            Assert.Null(Vollpaket.Load(bad));
            Directory.Delete(Path.GetDirectoryName(bad)!, true);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "T-0222: Vollpaket wird im Ordner neben dem Setup gefunden (auch als Unterordner „Programme“)")]
    public void Locate()
    {
        var root = Tmp("trendo-vp-");
        try
        {
            var prog = Medium(root);
            Assert.Equal(Path.GetFullPath(prog), Vollpaket.Locate([null, Path.Combine(root, "gibt-es-nicht"), root]));
            Assert.Equal(Path.GetFullPath(prog), Vollpaket.Locate([prog]));
            Assert.Null(Vollpaket.Locate([Path.GetTempPath() + "trendo-leer-" + Guid.NewGuid().ToString("N")]));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "T-0222: DependencyManager richtet mitgelieferte Programme aus dem Vollpaket ein und erkennt sie danach")]
    public async Task Manager()
    {
        var root = Tmp("trendo-vp-");
        try
        {
            var prog = Medium(root);
            var audit = new AuditLog(Path.Combine(root, "audit.jsonl"));
            var c = DependencyCatalog.Get("qpdf");
            Assert.Equal(DeliveryMode.Included, c.Mode);
            bool nurHier(string p) => !p.Contains('*') && Path.GetFullPath(p).StartsWith(root, StringComparison.OrdinalIgnoreCase) && File.Exists(p); // echte Programme auf diesem PC zählen nicht
            var ohne = new DependencyManager(audit, new RealProcessRunner(), fileExists: nurHier) { ConsentFile = Path.Combine(root, "consent.txt"), ToolsRoot = Path.Combine(root, "w1") };
            ohne.GiveConsent(Principal.LocalOwner);
            Assert.False(await ohne.InstallAsync(Principal.LocalOwner, c)); // kein Vollpaket: kein Download, nur Hinweis
            Assert.False(Directory.Exists(Path.Combine(root, "w1")));
            var dm = new DependencyManager(audit, new RealProcessRunner(), fileExists: nurHier) { ConsentFile = Path.Combine(root, "consent.txt"), ToolsRoot = Path.Combine(root, "w2"), LocateVollpaket = () => prog };
            Assert.True(await dm.InstallAsync(Principal.LocalOwner, c));
            Assert.True(dm.IsInstalled(c));
            Assert.Equal(Vollpaket.IncludedPath(dm.ToolsRoot, "qpdf"), dm.ResolvePath(c));
        }
        finally { Directory.Delete(root, true); }
    }

    static string RepoFile(string rel)
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) return Path.Combine(d.FullName, rel);
        throw new FileNotFoundException(rel);
    }

    [Fact(DisplayName = "T-0222: Katalog, Vollpaket-Liste und Bauliste passen zusammen; nicht weitergebbare Programme bleiben verlinkt")]
    public void Catalog()
    {
        var included = DependencyCatalog.All.Where(c => c.Mode == DeliveryMode.Included).Select(c => c.Id).Order().ToArray();
        Assert.Equal(Vollpaket.Included.Keys.Order().ToArray(), included);
        using var spec = JsonDocument.Parse(File.ReadAllText(RepoFile("tools/vollpaket/programme.json")));
        var built = spec.RootElement.GetProperty("programs").EnumerateArray().ToDictionary(p => p.GetProperty("id").GetString()!, p => p.GetProperty("exe").GetString()!);
        foreach (var (id, (pkg, exe)) in Vollpaket.Included)
        {
            Assert.True(built.ContainsKey(pkg), $"{id}: Paket {pkg} fehlt in programme.json");
            if (id == pkg) Assert.Equal($"{pkg}/{built[pkg]}", exe);
        }
        foreach (var id in new[] { "unreal", "chrome", "model-3b", "meshroom" })
            Assert.NotEqual(DeliveryMode.Included, DependencyCatalog.Get(id).Mode);
        foreach (var url in spec.RootElement.GetProperty("programs").EnumerateArray().Select(p => p.GetProperty("url").GetString()!))
            Assert.StartsWith("https://", url);
    }
}
