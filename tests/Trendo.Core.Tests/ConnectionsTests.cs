using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// Paket D „Verbindungen“ (Wunsch 02.11.2026): GitHub je Person, Netlify-Veröffentlichung, Erweiterungen von Claude Code.
public class ConnectionsTests
{
    static string Temp() { var d = Path.Combine(Path.GetTempPath(), "trendo-conn-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "GitHub: eigener gh-Ordner je Person, sichere Repo-Namen, Hochladen = git init/add/commit + gh repo create --private --push; .trendo kommt nie ins Repo")]
    public void GitHub_steps()
    {
        var dir = Temp();
        try
        {
            var env = GitHubLink.Env(dir);
            Assert.Equal(Path.Combine(dir, "github"), env["GH_CONFIG_DIR"]); Assert.Equal("1", env["GH_PROMPT_DISABLED"]);
            Assert.Equal("preisrechner-cafe", GitHubLink.SafeRepoName("Preisrechner Café"));
            Assert.Equal("grosse-uberraschung", GitHubLink.SafeRepoName("Große Überraschung!"));
            Assert.Equal("trendo-projekt", GitHubLink.SafeRepoName("!!!"));
            Assert.Contains("--web", GitHubLink.LoginArgs);
            Assert.Equal("erika-muster", GitHubLink.UserFrom("github.com\n  ✓ Logged in to github.com account erika-muster (keyring)"));
            Assert.Null(GitHubLink.UserFrom("You are not logged into any GitHub hosts."));

            var steps = GitHubLink.PublishSteps(dir, "meine-seite", true);
            Assert.Equal(["init", "add", "-c", "repo"], steps.Select(s => s.Args[0]));
            var create = steps[^1]; Assert.Equal("gh", create.Exe);
            Assert.Equal(["repo", "create", "meine-seite", "--private", "--source", ".", "--remote", "origin", "--push"], create.Args);
            Assert.Contains("--public", GitHubLink.PublishSteps(dir, "x", false)[^1].Args);
            var ignore = File.ReadAllLines(Path.Combine(dir, ".gitignore")); Assert.Contains(".trendo/", ignore); Assert.Contains(".env", ignore);
            GitHubLink.EnsureIgnore(dir); Assert.Equal(ignore.Length, File.ReadAllLines(Path.Combine(dir, ".gitignore")).Length); // nicht doppelt
            Assert.Throws<ArgumentException>(() => GitHubLink.PublishSteps(dir, "böse name; rm", true));
            Assert.Equal("GitHub.cli", DependencyCatalog.Get("gh").WingetId); Assert.Equal(DeliveryMode.AutoUser, DependencyCatalog.Get("gh").Mode);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "Claude-Erweiterungen: Skills, Plugins (an/aus), Marktplätze und MCP-Server gelesen – ohne Geheimnisse; Befehle nur in geprüfter Form")]
    public void Claude_extensions()
    {
        var home = Temp();
        try
        {
            var (cfg, json) = ClaudeExtensions.Config(null, home);
            Assert.Equal(Path.Combine(home, ".claude"), cfg);
            Assert.Equal((Path.Combine(home, "p"), Path.Combine(home, "p", ".claude.json")), ClaudeExtensions.Config(Path.Combine(home, "p"), home));
            Directory.CreateDirectory(Path.Combine(cfg, "skills", "brief-stil")); File.WriteAllText(Path.Combine(cfg, "skills", "brief-stil", "SKILL.md"), "---\nname: brief-stil\ndescription: Briefe wie im Amt\n---\nText");
            Directory.CreateDirectory(Path.Combine(cfg, "skills", "leer"));
            Directory.CreateDirectory(Path.Combine(cfg, "plugins"));
            File.WriteAllText(Path.Combine(cfg, "plugins", "installed_plugins.json"), """{"version":2,"plugins":{"helfer@markt":[{"version":"1.2.0"}],"aus@markt":[{"version":"0.1"}]}}""");
            File.WriteAllText(Path.Combine(cfg, "settings.json"), """{"enabledPlugins":{"aus@markt":false}}""");
            File.WriteAllText(Path.Combine(cfg, "plugins", "known_marketplaces.json"), """{"markt":{"source":{"repo":"x/y"}}}""");
            File.WriteAllText(json, """{"mcpServers":{"karte":{"type":"http","url":"https://mcp.example.org/api?key=GEHEIM"},"lokal":{"command":"C:\\tools\\srv.exe","env":{"TOKEN":"GEHEIM"}}}}""");

            var s = Assert.Single(ClaudeExtensions.Skills(cfg)); Assert.Equal("brief-stil", s.Name); Assert.Equal("Briefe wie im Amt", s.Description);
            var pl = ClaudeExtensions.Plugins(cfg); Assert.Equal(["aus@markt", "helfer@markt"], pl.Select(p => p.Id)); Assert.False(pl[0].Enabled); Assert.True(pl[1].Enabled); Assert.Equal("1.2.0", pl[1].Version);
            Assert.Equal(["markt"], ClaudeExtensions.Marketplaces(cfg));
            var mcp = ClaudeExtensions.McpServers(json);
            Assert.Equal(["karte", "lokal"], mcp.Select(m => m.Name));
            Assert.Equal("https://mcp.example.org/api", mcp[0].Target); Assert.Equal("srv.exe", mcp[1].Target); Assert.Equal("stdio", mcp[1].Kind);
            Assert.DoesNotContain("GEHEIM", string.Join("|", mcp));

            Assert.Equal(["plugin", "install", "helfer@markt"], ClaudeExtensions.PluginInstall("helfer@markt"));
            Assert.Equal(["plugin", "disable", "helfer@markt"], ClaudeExtensions.PluginEnable("helfer@markt", false));
            Assert.Equal(["plugin", "marketplace", "add", "anthropics/claude-plugins-official"], ClaudeExtensions.MarketplaceAdd("anthropics/claude-plugins-official"));
            Assert.Equal(["mcp", "remove", "--scope", "user", "karte"], ClaudeExtensions.McpRemove("karte"));
            Assert.Contains("https://mcp.example.org/mcp", ClaudeExtensions.McpAddHttp("karte", "https://mcp.example.org/mcp"));
            Assert.Throws<ArgumentException>(() => ClaudeExtensions.PluginInstall("x & del *"));
            Assert.Throws<ArgumentException>(() => ClaudeExtensions.McpAddHttp("karte", "file:///c:/x"));

            var src = Path.Combine(home, "neu"); Directory.CreateDirectory(src); File.WriteAllText(Path.Combine(src, "SKILL.md"), "---\nname: neu\n---");
            var t = ClaudeExtensions.AddSkill(cfg, src); Assert.True(File.Exists(Path.Combine(t, "SKILL.md")));
            Assert.Throws<InvalidOperationException>(() => ClaudeExtensions.AddSkill(cfg, src));
        }
        finally { Directory.Delete(home, true); }
    }
}
