using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>27.10.2026: Wer nur die Setup-Datei bekommt (ohne Vollpaket), kann mitgelieferte Programme wie mpv trotzdem einrichten –
/// aus der offiziellen Quelle, nach Zustimmung, mit Protokoll.</summary>
public class NoVollpaketTests
{
    [Fact(DisplayName = "Ohne Vollpaket: mpv kommt aus der offiziellen Quelle (winget) und wird danach in der winget-Ablage gefunden")]
    public async Task Included_without_vollpaket_goes_online()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-novp-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var audit = new AuditLog(Path.Combine(root, "audit.jsonl"));
            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            const string link = @"C:\Nutzer\AppData\Local\Microsoft\WinGet\Links\mpv.exe";
            var runner = new FakeRunner((file, args) =>
            {
                if (file == "winget" && args.Contains("shinchiro.mpv")) files.Add(link);
                return (0, "ok");
            });
            var deps = new DependencyManager(audit, runner, p => p.Replace("%LOCALAPPDATA%", @"C:\Nutzer\AppData\Local"), files.Contains)
            { ConsentFile = Path.Combine(root, "c.txt"), ToolsRoot = Path.Combine(root, "werkzeuge") };
            deps.GiveConsent(Principal.LocalOwner);
            var mpv = DependencyCatalog.Get("mpv");
            Assert.Equal(DeliveryMode.Included, mpv.Mode);
            Assert.False(deps.IsInstalled(mpv));
            var lines = new List<string>();
            Assert.True(await deps.InstallAsync(Principal.LocalOwner, mpv, lines.Add));
            Assert.Contains(runner.Calls, c => c.StartsWith("winget install --id shinchiro.mpv -e --silent"));
            Assert.Contains(lines, l => l.Contains("offiziellen Quelle"));
            Assert.Equal(link, deps.ResolvePath(mpv));
            Assert.Contains(audit.ReadAll(), e => e.Action == "deps.install.online" && e.Detail == "mpv");

            // Ohne Internet/winget: klare Meldung statt „Vollpaket nicht gefunden“
            var failing = new DependencyManager(audit, new FakeRunner((_, _) => (1, "winget fehlt")), p => p, _ => false)
            { ConsentFile = Path.Combine(root, "c.txt"), ToolsRoot = Path.Combine(root, "werkzeuge") };
            var msgs = new List<string>();
            Assert.False(await failing.InstallAsync(Principal.LocalOwner, DependencyCatalog.Get("ffmpeg"), msgs.Add));
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }

    [Fact(DisplayName = "Online-Vollpaket: ohne USB-Stick kommt dasselbe geprüfte Paket vom GitHub-Release – vor winget, mit Prüfsumme, Paket danach gelöscht")]
    public async Task Online_vollpaket_before_winget()
    {
        var root = Path.Combine(Path.GetTempPath(), "trendo-vpon-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var online = Path.Combine(root, "online"); Directory.CreateDirectory(online);
            var zip = Path.Combine(online, "mpv.zip");
            using (var z = System.IO.Compression.ZipFile.Open(zip, System.IO.Compression.ZipArchiveMode.Create))
            using (var w = new StreamWriter(z.CreateEntry("mpv.exe").Open())) w.Write("mpv");
            var sha = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(zip)));
            File.WriteAllText(Path.Combine(online, Vollpaket.ManifestName), System.Text.Json.JsonSerializer.Serialize(new { programs = new[] {
                new { id = "mpv", name = "mpv", version = "1", license = "GPL", file = "mpv.zip", sha256 = sha, bytes = new FileInfo(zip).Length, exe = "mpv.exe", source_url = "https://mpv.io" } } }));
            var audit = new AuditLog(Path.Combine(root, "audit.jsonl"));
            var runner = new FakeRunner((_, _) => (0, "ok"));
            var tools = Path.Combine(root, "werkzeuge");
            var deps = new DependencyManager(audit, runner, p => p, File.Exists) { ConsentFile = Path.Combine(root, "c.txt"), ToolsRoot = tools };
            deps.GiveConsent(Principal.LocalOwner);
            var asked = new List<string>();
            deps.FetchVollpaketPackage = (pkg, _, _) => { asked.Add(pkg); return Task.FromResult<string?>(online); };
            Assert.True(await deps.InstallAsync(Principal.LocalOwner, DependencyCatalog.Get("mpv")));
            Assert.Equal(["mpv"], asked);
            Assert.Empty(runner.Calls); // kein winget nötig
            Assert.True(File.Exists(Path.Combine(tools, "mpv", "mpv.exe")));
            Assert.False(File.Exists(zip)); // geladenes Paket aufgeräumt
            Assert.Contains(audit.ReadAll(), e => e.Action == "deps.install.vollpaket-online");

            // offline: kein Online-Vollpaket, kein winget
            var off = new DependencyManager(audit, runner, p => p, _ => false) { ConsentFile = Path.Combine(root, "c.txt"), ToolsRoot = Path.Combine(root, "w2"), Offline = true };
            off.FetchVollpaketPackage = (pkg, _, _) => { asked.Add("offline:" + pkg); return Task.FromResult<string?>(null); };
            Assert.False(await off.InstallAsync(Principal.LocalOwner, DependencyCatalog.Get("ffmpeg")));
            Assert.DoesNotContain("offline:ffmpeg", asked);
            Assert.Empty(runner.Calls);
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }
}
