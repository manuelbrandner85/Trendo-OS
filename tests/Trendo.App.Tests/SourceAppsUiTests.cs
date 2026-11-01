using System.IO.Compression;
using System.Net;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// T-0314: Quellcode-Projekte in der Trefferliste – Erkennung als Abzeichen, Zustimmung, Einrichtung mit offizieller Laufzeit, Start, Entfernen;
/// nicht startbare Projekte zeigen den Grund und Alternativen (Fork mit Windows-Programm, PyPI-Paket); Web-Seiten laufen über den lokalen Server.
/// Alles mit nachgebautem GitHub (kein Netz) und ohne echte Prozesse.
[Collection("GitHubNetz")]
public class SourceApps_Ui
{
    sealed class FakeNet(Func<HttpRequestMessage, Task<HttpResponseMessage>> f) : HttpMessageHandler
    {
        public readonly List<string> Calls = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) { lock (Calls) Calls.Add(r.RequestUri!.ToString()); return f(r); }
    }
    static HttpResponseMessage Ok(string s) => new(HttpStatusCode.OK) { Content = new StringContent(s, Encoding.UTF8, "application/json") };
    static HttpResponseMessage Bytes(byte[] b) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(b) };
    static OssRepo Repo(string n) => new(n, "Werkzeug", "https://github.com/" + n, "https://github.com/" + n + ".git", 5, "MIT", "Python", DateTimeOffset.Now);
    static async Task Run(Task t) { while (!t.IsCompleted) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } await t; }
    static async Task Until(Func<bool> ok, int ms = 8000) { for (var i = 0; i < ms / 20 && !ok(); i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } Assert.True(ok()); }

    static byte[] SrcZip(string top, Dictionary<string, string> files)
    {
        var ms = new MemoryStream();
        using (var z = new ZipArchive(ms, ZipArchiveMode.Create, true))
            foreach (var (k, v) in files) using (var s = new StreamWriter(z.CreateEntry($"{top}/{k}").Open())) s.Write(v);
        return ms.ToArray();
    }
    static string Tree(params string[] files) => "{\"sha\":\"stand42\",\"tree\":[" + string.Join(",", files.Select(f => $"{{\"path\":\"{f}\",\"type\":\"blob\"}}")) + "]}";

    [AvaloniaFact(DisplayName = "T-0314: Python-Projekt ohne Windows-Programm → Abzeichen „läuft in Trendo“, Zustimmung, Einrichtung mit uv (sichtbare Schritte), Start im Fenster, Entfernen")]
    public async Task Python_from_source()
    {
        var (w, l, root) = Ui.Open();
        var pyproject = "[project]\nname = \"zeitplan\"\n\n[project.scripts]\nzeitplan = \"zeitplan.cli:main\"\n";
        var files = new Dictionary<string, string> { ["pyproject.toml"] = pyproject, ["zeitplan/cli.py"] = "def main(): print('hallo')", ["README.md"] = "# Zeitplan" };
        var net = new FakeNet(r =>
        {
            var u = r.RequestUri!.ToString();
            if (u.EndsWith("/releases/latest")) return Task.FromResult(Ok("""{"tag_name":"v1","assets":[]}"""));
            if (u.Contains("/releases")) return Task.FromResult(Ok("[]"));
            if (u.Contains("/git/trees/")) return Task.FromResult(Ok(Tree([.. files.Keys])));
            if (u.StartsWith("https://raw.githubusercontent.com/")) return Task.FromResult(Ok(files.GetValueOrDefault(u.Split("/HEAD/")[1]) ?? ""));
            if (u.StartsWith("https://codeload.github.com/")) return Task.FromResult(Bytes(SrcZip("zeitplan-stand42", files)));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });
        // Laufzeit ist schon eingerichtet (kein 15-MB-Download im Test)
        var tools = Path.Combine(root, "werkzeuge"); var uvDir = SourceApps.RuntimeDir(tools, SourceApps.RuntimeFor("uv", (System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.Arm64)));
        Directory.CreateDirectory(uvDir); File.WriteAllText(Path.Combine(uvDir, "uv.exe"), "");
        var ran = new List<string>();
        MainWindow.GitHubHandlerOverride = net;
        MainWindow.SourceRunOverride = (exe, args, dir, env, line, _) =>
        {
            lock (ran) ran.Add($"{Path.GetFileName(exe)} {args}");
            Assert.Equal(Path.Combine(tools, "laufzeiten", "uv-python"), env["UV_PYTHON_INSTALL_DIR"]);
            line("Resolved 3 packages in 120ms");
            if (args.Contains("-e .")) { Directory.CreateDirectory(Path.Combine(dir, ".venv", "Scripts")); File.WriteAllText(Path.Combine(dir, ".venv", "Scripts", "zeitplan.exe"), ""); }
            return Task.FromResult(0);
        };
        try
        {
            w.OssSearchOverride = _ => Task.FromResult<IReadOnlyList<OssRepo>>([Repo("erika/zeitplan")]);
            w.Show("oss"); await w.OssSearch("zeitplan");
            await Until(() => w.SourceDetections.ContainsKey("erika/zeitplan") && w.TryFind<Border>("oss-src-kind-0") is not null);
            Assert.Contains("Python-Programm · läuft in Trendo", ((TextBlock)w.Find<Border>("oss-src-kind-0").Child!).Text);
            Assert.Null(w.TryFind<Button>("oss-install-0")); // kein ausgegrauter Windows-Knopf daneben
            w.Find<Button>("oss-src-install-0").Press();
            var ask = w.GetLogicalTexts().First(t => t.StartsWith("Trendo lädt den Quellcode"));
            Assert.Contains("uv (Python-Werkzeug von Astral)", ask); Assert.Contains("nur bei Projekten, denen du vertraust", ask);
            w.Find<Button>("oss-src-cancel-0").Press(); Assert.Null(w.TryFind<Button>("oss-src-confirm-0")); // Abbrechen tut nichts
            Assert.Empty(ran);
            w.Find<Button>("oss-src-install-0").Press(); w.Find<Button>("oss-src-confirm-0").Press();
            await Until(() => SourceApps.List(tools).Count == 1 && w.SetupFinishShown, 15000);
            Assert.Equal(["uv.exe venv .venv --python \"3.12\"", "uv.exe pip install --python .venv -e ."], ran);
            var e = Assert.Single(SourceApps.List(tools));
            Assert.Equal(("erika/zeitplan", "stand42", SourceApps.Kind.Python), (e.Repo, e.Sha, e.Kind));
            var dir = SourceApps.Dir(tools, e.Id);
            Assert.True(File.Exists(Path.Combine(dir, "zeitplan", "cli.py"))); // Hauptordner aus dem Zip entfernt
            await Until(() => l.Opened.Any(o => o.Contains("trendo-start.cmd")));
            var started = Assert.Single(l.Opened, o => o.Contains("trendo-start.cmd"));
            Assert.StartsWith(Path.Combine(Environment.SystemDirectory, "cmd.exe"), started);
            var cmd = File.ReadAllText(Path.Combine(dir, "trendo-start.cmd"));
            Assert.Contains(".venv\\Scripts\\zeitplan.exe", cmd.Replace('/', '\\')); Assert.Contains("set \"UV_PYTHON_PREFERENCE=only-managed\"", cmd); Assert.Contains("pause", cmd);
            // Programmseite: Eintrag mit Starten/Entfernen
            w.Show("components");
            Assert.Contains(w.GetLogicalTexts(), t => t.StartsWith("erika/zeitplan  ·  Python"));
            w.Find<Button>("btn-src-remove-" + e.Id).Press(); w.Find<Button>("btn-src-remove-" + e.Id).Press(); // zweistufig
            await Until(() => SourceApps.List(tools).Count == 0);
            Assert.False(Directory.Exists(dir));
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "source.install" && a.Detail.StartsWith("erika/zeitplan Python stand42"));
        }
        finally { MainWindow.GitHubHandlerOverride = null; MainWindow.SourceRunOverride = null; }
    }

    [AvaloniaFact(DisplayName = "T-0314: Fehlgeschlagener Schritt → klare Meldung mit letzter Ausgabe, nichts eingetragen, alter Stand bleibt; Rust-Projekt zeigt Grund und findet Fork mit Windows-Programm und PyPI-Paket")]
    public async Task Failure_and_alternatives()
    {
        var (w, _, root) = Ui.Open();
        var tools = Path.Combine(root, "werkzeuge");
        var nodeDir = SourceApps.RuntimeDir(tools, SourceApps.RuntimeFor("node", (System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.Arm64))); Directory.CreateDirectory(nodeDir); File.WriteAllText(Path.Combine(nodeDir, "node.exe"), "");
        var pkg = """{"name":"planer","scripts":{"dev":"vite"},"devDependencies":{"vite":"^7"}}""";
        var net = new FakeNet(r =>
        {
            var u = r.RequestUri!.ToString();
            if (u.Contains("erika/planer"))
            {
                if (u.EndsWith("/releases/latest")) return Task.FromResult(Ok("""{"tag_name":"","assets":[]}"""));
                if (u.Contains("/releases")) return Task.FromResult(Ok("[]"));
                if (u.Contains("/git/trees/")) return Task.FromResult(Ok(Tree("package.json", "index.html")));
                if (u.Contains("raw.githubusercontent.com")) return Task.FromResult(Ok(pkg));
                return Task.FromResult(Bytes(SrcZip("planer-x", new() { ["package.json"] = pkg })));
            }
            if (u.Contains("max/schnell"))
            {
                if (u.EndsWith("/releases/latest")) return Task.FromResult(Ok("""{"tag_name":"v2","assets":[{"name":"schnell-src.tar.gz","browser_download_url":"https://github.com/max/schnell/a.tar.gz","size":4}]}"""));
                if (u.Contains("/releases")) return Task.FromResult(Ok("[]"));
                if (u.Contains("/git/trees/")) return Task.FromResult(Ok(Tree("Cargo.toml", "src/main.rs")));
                if (u.Contains("/forks")) return Task.FromResult(Ok("""[{"full_name":"erika/schnell-win","stargazers_count":9,"license":{"spdx_id":"MIT"},"pushed_at":"2026-10-01T00:00:00Z"},{"full_name":"x/schnell-alt","stargazers_count":2,"pushed_at":"2026-10-01T00:00:00Z"}]"""));
                return Task.FromResult(Ok(""));
            }
            if (u.Contains("erika/schnell-win/releases/latest")) return Task.FromResult(Ok("""{"tag_name":"v2.1","assets":[{"name":"schnell-windows-x64.zip","browser_download_url":"https://github.com/erika/schnell-win/releases/download/v2.1/schnell-windows-x64.zip","size":3145728}]}"""));
            if (u.Contains("x/schnell-alt/releases/latest")) return Task.FromResult(Ok("""{"tag_name":"","assets":[]}"""));
            if (u.Contains("/releases")) return Task.FromResult(Ok("[]"));
            if (u.StartsWith("https://pypi.org/pypi/schnell/json")) return Task.FromResult(Ok("""{"info":{"name":"schnell","version":"2.0.1"}}"""));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });
        MainWindow.GitHubHandlerOverride = net;
        MainWindow.SourceRunOverride = (_, args, _, _, line, _) => { line("npm error code ERESOLVE"); line("npm error could not resolve dependency vite@7"); return Task.FromResult(1); };
        try
        {
            w.OssSearchOverride = _ => Task.FromResult<IReadOnlyList<OssRepo>>([Repo("erika/planer"), Repo("max/schnell")]);
            w.Show("oss"); await w.OssSearch("planer");
            await Until(() => w.SourceDetections.Count == 2 && w.TryFind<Border>("oss-src-kind-1") is not null);
            Assert.StartsWith("Web-App", ((TextBlock)w.Find<Border>("oss-src-kind-0").Child!).Text);
            Assert.StartsWith("Rust-Projekt", ((TextBlock)w.Find<Border>("oss-src-kind-1").Child!).Text);
            Assert.Contains(w.GetLogicalTexts(), t => t.Contains("Visual-Studio-Build-Tools"));
            // Einrichten schlägt fehl
            await Run(w.SourceInstall(Repo("erika/planer"), start: false));
            Assert.True(w.SetupFinishShown);
            Assert.Contains("„Abhängigkeiten laden (npm install)“ ist fehlgeschlagen (Code 1)", w.SetupOverlayStepText);
            Assert.Contains("could not resolve dependency", w.SetupOverlayStepText);
            Assert.Empty(SourceApps.List(tools)); Assert.False(Directory.Exists(SourceApps.Dir(tools, SourceApps.IdFor("erika/planer"))));
            w.Find<Button>("setup-overlay-done").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            // Alternativen
            w.Show("oss"); w.Find<Button>("oss-alt-1").Press();
            await Until(() => w.SourceAlternatives.ContainsKey("max/schnell"));
            var alts = w.SourceAlternatives["max/schnell"];
            Assert.Equal(["Diesen Fork installieren", "Als Python-Paket starten"], alts.Select(a => a.Name));
            Assert.Contains("erika/schnell-win", alts[0].Text); Assert.Contains("v2.1 · 3 MB", alts[0].Text); Assert.Contains("„schnell“ 2.0.1 auf PyPI", alts[1].Text);
            Assert.NotNull(w.TryFind<Button>("oss-alt-use-1-0"));
        }
        finally { MainWindow.GitHubHandlerOverride = null; MainWindow.SourceRunOverride = null; }
    }

    [AvaloniaFact(DisplayName = "T-0314: Reine Web-Seite läuft ohne Laufzeit über Trendos lokalen Server (nur 127.0.0.1, nur der Projektordner) und lässt sich beenden")]
    public async Task Static_site()
    {
        var (w, l, root) = Ui.Open();
        var tools = Path.Combine(root, "werkzeuge");
        var files = new Dictionary<string, string> { ["docs/index.html"] = "<h1>Rezepte</h1>", ["docs/app.js"] = "", ["README.md"] = "" };
        MainWindow.GitHubHandlerOverride = new FakeNet(r => Task.FromResult(r.RequestUri!.Host == "codeload.github.com" ? Bytes(SrcZip("rezepte-1", files)) : new HttpResponseMessage(HttpStatusCode.NotFound)));
        try
        {
            w.Show("oss");
            var e = await w.SourceInstall(Repo("erika/rezepte"));
            Assert.NotNull(e); Assert.Equal(("docs", (string?)null), (e!.StaticRoot, e.Runtime));
            var url = Assert.Single(l.Opened, o => o.StartsWith("http://127.0.0.1:"));
            using var http = new HttpClient();
            Assert.Contains("Rezepte", await http.GetStringAsync(url));
            Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync(url + "../README.md")).StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest ? HttpStatusCode.NotFound : HttpStatusCode.OK);
            Assert.Contains(e.Id, w.SourceRunning);
            w.Show("components"); w.Find<Button>("btn-src-stop-" + e.Id).Press();
            Assert.DoesNotContain(e.Id, w.SourceRunning);
        }
        finally { MainWindow.GitHubHandlerOverride = null; }
    }
}
