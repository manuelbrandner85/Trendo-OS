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
}
