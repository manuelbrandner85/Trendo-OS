using System.Net;
using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// Paket D „Verbindungen“ (Wunsch 02.11.2026): GitHub je Person, Netlify, Erweiterungen von Claude Code – alles nachgebildet, nie echte Programme oder Dienste.
[Collection("Claude")]
public class Connections_Ui
{
    static void Pump() { for (var i = 0; i < 5; i++) Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }
    static async Task Until(Func<bool> ok) { for (var i = 0; i < 200 && !ok(); i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } Pump(); Assert.True(ok()); }
    static async Task Run(Task t) { while (!t.IsCompleted) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } await t; }
    static string J(object o) => JsonSerializer.Serialize(o);

    sealed record Call(string Exe, string[] Args, string Cwd, Dictionary<string, string> Env, bool Visible);

    /// <summary>Nachgebildete gh/git/claude: merkt sich jeden Lauf; nach „gh auth login“ gilt man als angemeldet.</summary>
    static Func<string, IReadOnlyList<string>, string, IReadOnlyDictionary<string, string>, bool, Task<(int, string)>> Fake(List<Call> calls)
    {
        var loggedIn = false;
        return (exe, args, cwd, env, vis) =>
        {
            lock (calls) calls.Add(new(exe, [.. args], cwd, env.ToDictionary(), vis));
            if (args is ["auth", "login", ..]) loggedIn = true;
            if (args is ["auth", "logout", ..]) loggedIn = false;
            if (args is ["auth", "status", ..]) return Task.FromResult(loggedIn ? (0, "github.com\n  ✓ Logged in to github.com account erika-muster (keyring)") : (1, "You are not logged into any GitHub hosts."));
            if (args is ["repo", "create", var name, ..]) return Task.FromResult((0, $"✓ Created repository erika-muster/{name} on GitHub\n  https://github.com/erika-muster/{name}"));
            return Task.FromResult((0, ""));
        };
    }

    sealed class FakeNetlify : HttpMessageHandler
    {
        public readonly List<string> Urls = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            lock (Urls) Urls.Add(r.RequestUri!.AbsolutePath);
            var json = r.RequestUri.AbsolutePath.EndsWith("/sites") ? """{"id":"site-1"}""" : """{"ssl_url":"https://kontakt.netlify.app"}""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }

    static void Reset() { MainWindow.ConnRunOverride = null; MainWindow.GhExeOverride = null; MainWindow.NetlifyHandlerOverride = null; MainWindow.ClaudeHomeOverride = null; MainWindow.ClaudeStatusOverride = null; MainWindow.ClaudeRunOverride = null; }

    [AvaloniaFact(DisplayName = "Verbindungen: GitHub-Anmeldung im eigenen gh-Ordner der Person (sichtbar, im Browser), Netlify-Schlüssel nur im Tresor, Claude-Plugins nur nach Ja geändert")]
    public async Task Connections_page()
    {
        var (w, _, _) = Ui.Open();
        var calls = new List<Call>(); var home = Path.Combine(w.Core.DataRoot, "home-test");
        var cfg = Path.Combine(home, ".claude"); Directory.CreateDirectory(Path.Combine(cfg, "plugins"));
        File.WriteAllText(Path.Combine(cfg, "plugins", "installed_plugins.json"), """{"plugins":{"helfer@markt":[{"version":"1.0.0"}]}}""");
        File.WriteAllText(Path.Combine(home, ".claude.json"), """{"mcpServers":{"karte":{"type":"http","url":"https://mcp.example.org/x?key=GEHEIM"}}}""");
        MainWindow.ConnRunOverride = Fake(calls); MainWindow.GhExeOverride = () => @"C:\x\gh.exe"; MainWindow.ClaudeHomeOverride = home;
        try
        {
            // ohne GitHub CLI: Angebot zum Einrichten
            MainWindow.GhExeOverride = () => null;
            w.Show("verbindungen"); Pump();
            Assert.NotNull(w.TryFind<Button>("conn-gh-install"));
            MainWindow.GhExeOverride = () => @"C:\x\gh.exe";
            w.Show("verbindungen"); await Run(w.GitHubCheck()); w.Show("verbindungen"); Pump();
            Assert.Equal("Nicht angemeldet.", w.Find<TextBlock>("conn-gh-state").Text);

            w.Find<Button>("conn-gh-login").Press();
            await Until(() => w.GitHubUser == "erika-muster");
            var login = calls.Single(c => c.Args is ["auth", "login", ..]);
            Assert.True(login.Visible); Assert.Contains("--web", login.Args); // Code und Browser-Anmeldung macht die Person selbst
            Assert.Equal(GitHubLink.ConfigDir(w.Core.DataRoot), login.Env["GH_CONFIG_DIR"]);
            Assert.StartsWith("✓ Angemeldet als erika-muster", w.Find<TextBlock>("conn-gh-state").Text);
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "github.login.ok");

            // Netlify: falscher Schlüssel abgelehnt, richtiger landet im Tresor (nie im Audit)
            w.Find<TextBox>("conn-nl-token").Text = "kurz"; w.Find<Button>("conn-nl-save").Press(); Pump();
            Assert.Null(w.Core.Secrets.Get(NetlifyLink.SecretKey)); Assert.Contains("Netlify-Zugangsschlüssel", w.Find<TextBlock>("conn-msg").Text);
            w.Find<TextBox>("conn-nl-token").Text = "nfp_0123456789abcdefghijklmnop"; w.Find<Button>("conn-nl-save").Press(); Pump();
            Assert.Equal("nfp_0123456789abcdefghijklmnop", w.Core.Secrets.Get(NetlifyLink.SecretKey));
            Assert.DoesNotContain(w.Core.Audit.ReadAll(), a => a.Detail.Contains("nfp_"));
            Assert.NotNull(w.TryFind<Button>("conn-nl-disconnect"));

            // Erweiterungen: Plugin und MCP sichtbar, geheime Abfrage nicht; Ändern erst nach Ja
            var texts = string.Join("|", w.GetLogicalTexts());
            Assert.Contains("helfer@markt", texts); Assert.Contains("karte", texts); Assert.DoesNotContain("GEHEIM", texts);
            w.Find<Button>("conn-plugin-toggle-0").Press(); Pump();
            Assert.NotNull(w.TryFind<Border>("conn-ask")); Assert.DoesNotContain(calls, c => c.Args is ["plugin", ..]);
            w.Find<Button>("conn-yes").Press();
            await Until(() => calls.Any(c => c.Args is ["plugin", "disable", "helfer@markt"]));
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "claude.erweiterung");
            w.Find<TextBox>("conn-mcp-name").Text = "neu"; w.Find<TextBox>("conn-mcp-url").Text = "file:///c:/x"; w.Find<Button>("conn-mcp-add").Press(); Pump();
            Assert.Contains("https://", w.Find<TextBlock>("conn-msg").Text); Assert.Null(w.TryFind<Border>("conn-ask"));
            w.Find<TextBox>("conn-plugin-id").Text = "x & del *"; w.Find<Button>("conn-plugin-add").Press(); Pump();
            Assert.Contains("Ungültige Angabe", w.Find<TextBlock>("conn-msg").Text);

            // abmelden nur nach Ja
            w.Find<Button>("conn-gh-logout").Press(); Pump(); w.Find<Button>("conn-yes").Press();
            await Until(() => w.GitHubUser is null && calls.Any(c => c.Args is ["auth", "logout", ..]));
        }
        finally { Reset(); w.Close(); }
    }

    /// <summary>Nachgebildetes Claude, das eine Webseite schreibt.</summary>
    static Func<IReadOnlyList<string>, string, Action<string>, CancellationToken, Task<int>> FakeClaude() => (args, dir, line, _) =>
    {
        line(J(new { type = "system", subtype = "init", session_id = "s-1", model = "claude" }));
        File.WriteAllText(Path.Combine(dir, "index.html"), ClaudeChat_Ui.Page("<h1>Kontakt</h1>"));
        line(J(new { type = "assistant", message = new { content = new object[] { new { type = "text", text = "Fertig." } } } }));
        line(J(new { type = "result", subtype = "success", is_error = false, result = "Fertig", session_id = "s-1", num_turns = 1 }));
        return Task.FromResult(0);
    };

    [AvaloniaFact(DisplayName = "Claude-Chat: „⬆ GitHub“ lädt das Projekt nach Ja als privates Repo hoch (gh-Ordner der Person); „🌐 Netlify“ nur im Web-Studio, veröffentlicht nach Ja")]
    public async Task Chat_github_netlify()
    {
        var (w, _, _) = Ui.Open();
        var calls = new List<Call>(); var nl = new FakeNetlify();
        MainWindow.ConnRunOverride = Fake(calls); MainWindow.GhExeOverride = () => @"C:\x\gh.exe"; MainWindow.NetlifyHandlerOverride = nl;
        MainWindow.ClaudeStatusOverride = () => Task.FromResult(new ClaudeCode.Status(true, true, "claude.ai", "max", @"C:\x\claude.exe")); MainWindow.ClaudeRunOverride = FakeClaude();
        try
        {
            await Run(w.ClaudeCheck(true));
            w.Show("dev"); w.ClaudeDockToggle("CORE-07"); Pump();
            await Run(w.ClaudeChatSend("Baue eine Kontaktseite"));
            var chat = w.ClaudeChatCurrent()!;
            Assert.True(w.Find<Button>("claude-chat-netlify").IsVisible);

            // ohne Anmeldung: Hinweis und Weg zu den Verbindungen, nichts wird hochgeladen
            w.Find<Button>("claude-chat-github").Press(); Pump();
            await Until(() => w.TryFind<TextBlock>("claude-github-need") is not null);
            Assert.DoesNotContain(calls, c => c.Args is ["repo", ..]);
            await MainWindow.ConnRunOverride!(@"C:xgh.exe", ["auth", "login", "--web"], "", new Dictionary<string, string>(), true); await Run(w.GitHubCheck());
            w.Find<Button>("claude-chat-github").Press(); Pump(); w.Find<Button>("claude-chat-github").Press(); Pump();
            Assert.Equal("baue-eine-kontaktseite", w.Find<TextBox>("claude-github-name").Text);
            Assert.True(w.Find<CheckBox>("claude-github-private").IsChecked);
            w.Find<Button>("claude-github-yes").Press();
            await Until(() => w.LastGitHubRepo is not null);
            var create = calls.Single(c => c.Args is ["repo", "create", ..]);
            Assert.Contains("--private", create.Args); Assert.Equal(chat.Folder, create.Cwd); Assert.Equal(GitHubLink.ConfigDir(w.Core.DataRoot), create.Env["GH_CONFIG_DIR"]);
            Assert.Contains(calls, c => c.Exe == "git" && c.Args is ["init", ..]);
            Assert.Equal("https://github.com/erika-muster/baue-eine-kontaktseite", w.LastGitHubRepo);
            Assert.Contains(".trendo/", File.ReadAllText(Path.Combine(chat.Folder, ".gitignore")));
            Assert.StartsWith("⬆ Auf GitHub hochgeladen (privat)", w.ClaudeChatCurrent()!.Messages[^1].Text);

            // Netlify: ohne Verbindung Hinweis, dann nach Ja veröffentlichen
            w.Find<Button>("claude-chat-netlify").Press(); Pump();
            Assert.NotNull(w.TryFind<TextBlock>("claude-netlify-need")); Assert.Empty(nl.Urls);
            new NetlifyLink(w.Core.Secrets).Connect("nfp_0123456789abcdefghijklmnop");
            w.Find<Button>("claude-chat-netlify").Press(); Pump(); w.Find<Button>("claude-chat-netlify").Press(); Pump();
            w.Find<Button>("claude-netlify-yes").Press();
            await Until(() => w.LastNetlifyUrl is not null);
            Assert.Equal("https://kontakt.netlify.app", w.LastNetlifyUrl);
            Assert.Equal(["/api/v1/sites", "/api/v1/sites/site-1/deploys"], nl.Urls);
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "netlify.veroeffentlicht");

            // Baukasten (kein Web-Studio): kein Netlify-Knopf
            w.Find<Button>("claude-dock-close").Press(); w.Show("baukasten"); w.Find<Button>("auftrag-claude").Press(); Pump();
            Assert.False(w.Find<Button>("claude-chat-netlify").IsVisible); Assert.True(w.Find<Button>("claude-chat-github").IsVisible);
        }
        finally { Reset(); w.Close(); }
    }
}
