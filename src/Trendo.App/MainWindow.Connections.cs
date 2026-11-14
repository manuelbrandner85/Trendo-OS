using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Paket D „Verbindungen“ (0.9.48, Wunsch 02.11.2026): GitHub je Person (offizielle GitHub CLI, Anmeldung im Browser), Netlify
/// (Webseiten veröffentlichen) und die Erweiterungen von Claude Code (Skills, Plugins, Marktplätze, MCP) – alles erst nach Ja, mit Audit.</summary>
public partial class MainWindow
{
    /// <summary>Für Tests: Programmläufe (Programm, Argumente, Ordner, Umgebung, sichtbar) → (Exitcode, Ausgabe) – nie echte Programme.</summary>
    public static Func<string, IReadOnlyList<string>, string, IReadOnlyDictionary<string, string>, bool, Task<(int Code, string Out)>>? ConnRunOverride { get; set; }
    /// <summary>Für Tests: Pfad der GitHub CLI (null = nicht installiert).</summary>
    public static Func<string?>? GhExeOverride { get; set; }
    /// <summary>Für Tests: Netlify-Schnittstelle nachgebildet.</summary>
    public static HttpMessageHandler? NetlifyHandlerOverride { get; set; }
    /// <summary>Für Tests: Benutzerordner, in dem Claude Code seine Einstellungen hat.</summary>
    public static string? ClaudeHomeOverride { get; set; }

    string? _ghUser; bool _ghChecked; string _connMsg = "";
    (string Text, Func<Task> Run)? _connAsk;
    NetlifyLink Netlify => new(_core.Secrets, NetlifyHandlerOverride);
    public string ConnMessage => _connMsg;
    public string? GitHubUser => _ghUser;

    void InitConnections()
    {
        _showMap["verbindungen"] = ConnectionsView; KiTabs.Add(("verbindungen", "Verbindungen"));
        PageModules["verbindungen"] = "CORE-02";
        ExtraPages.Add(("Verbindungen", "verbindungen", ["github", "netlify", "repo", "hochladen", "plugin", "plugins", "mcp", "marktplatz", "erweiterung", "verbindung"]));
    }

    string? GhExe()
    {
        if (GhExeOverride is { } o) return o();
        foreach (var p in (DependencyCatalog.Get("gh").DetectPaths ?? []).Select(Environment.ExpandEnvironmentVariables)) if (File.Exists(p)) return p;
        return Cli.Find("gh");
    }

    /// <summary>Ein Programm ausführen – im Testmodus nie echt. „visible“: eigenes Fenster (z. B. für den Anmeldecode von GitHub).</summary>
    async Task<(int Code, string Out)> ConnRun(string exe, IReadOnlyList<string> args, string cwd, IReadOnlyDictionary<string, string> env, bool visible = false)
    {
        if (ConnRunOverride is { } o) return await o(exe, args, cwd, env, visible);
        if (_opt.Isolated) throw new InvalidOperationException("Testmodus: es wird kein echtes Programm gestartet.");
        if (!visible) { var r = await Task.Run(() => Cli.Run(exe, args, cwd, 300000, env.ToDictionary())); return (r.Exit, r.Out + "\n" + r.Err); }
        var psi = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = false, WorkingDirectory = cwd };
        foreach (var a in args) psi.ArgumentList.Add(a);
        foreach (var (k, v) in env) psi.Environment[k] = v;
        using var p = Process.Start(psi) ?? throw new InvalidOperationException(Path.GetFileName(exe) + " ließ sich nicht starten.");
        await p.WaitForExitAsync();
        return (p.ExitCode, "");
    }

    Dictionary<string, string> GhEnv() { Directory.CreateDirectory(GitHubLink.ConfigDir(_core.DataRoot)); return GitHubLink.Env(_core.DataRoot); }

    /// <summary>Anmeldestand bei GitHub (eigener gh-Ordner dieser Person).</summary>
    public async Task GitHubCheck()
    {
        _ghChecked = true;
        if (GhExe() is not { } gh) { _ghUser = null; return; }
        try { var (_, outp) = await ConnRun(gh, GitHubLink.StatusArgs, Path.GetTempPath(), GhEnv()); _ghUser = GitHubLink.UserFrom(outp); }
        catch (InvalidOperationException e) { _ghUser = null; _connMsg = e.Message; }
    }

    async Task GitHubLogin()
    {
        if (GhExe() is not { } gh) return;
        _connMsg = "GitHub öffnet sich im Browser. Im Fenster steht ein Code – den gibst du dort ein und bestätigst. Trendo sieht dein Passwort nie.";
        _core.Audit.Append(Principal.LocalOwner.Name, "github.login.start", "");
        Show("verbindungen");
        try { await ConnRun(gh, GitHubLink.LoginArgs, Path.GetTempPath(), GhEnv(), visible: true); }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { _connMsg = e.Message; }
        await GitHubCheck();
        if (_ghUser is not null) _connMsg = $"✓ Mit GitHub verbunden als {_ghUser}.";
        _core.Audit.Append(Principal.LocalOwner.Name, _ghUser is null ? "github.login.offen" : "github.login.ok", "");
        Show("verbindungen");
    }

    async Task GitHubLogout()
    {
        if (GhExe() is not { } gh) return;
        try { await ConnRun(gh, [.. GitHubLink.LogoutArgs], Path.GetTempPath(), GhEnv()); } catch (InvalidOperationException e) { _connMsg = e.Message; }
        await GitHubCheck();
        if (_ghUser is null) _connMsg = "Von GitHub abgemeldet.";
        _core.Audit.Append(Principal.LocalOwner.Name, "github.logout", "");
        Show("verbindungen");
    }

    async Task GitHubInstall()
    {
        var check = await _deps.CheckAsync(DependencyCatalog.Get("gh"));
        _depStudio = "CORE-02"; _depStudioTitle = "Verbindungen"; _depChecks = [check]; _depReturn = "verbindungen";
        _depLog.Text = "Für GitHub brauche ich die offizielle GitHub CLI. Trendo fragt einmal, bevor etwas installiert wird.";
        Show("studio-deps");
    }

    (string Dir, string Json) ClaudeCfg() => ClaudeExtensions.Config(ClaudeCode.PersonConfigDir(_core.DataRoot),
        ClaudeHomeOverride ?? (_opt.Isolated ? Path.Combine(_core.DataRoot, "claude-home") : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));

    /// <summary>Offizieller „claude“-Befehl für Erweiterungen – mit dem Claude-Ordner dieser Person.</summary>
    async Task ClaudeExtRun(string[] args, string done)
    {
        var exe = ClaudeExe();
        if (exe is null && ConnRunOverride is null) { _connMsg = "Claude Code ist nicht installiert."; Show("verbindungen"); return; }
        var env = new Dictionary<string, string>();
        if (ClaudeCode.PersonConfigDir(_core.DataRoot) is { } own) { Directory.CreateDirectory(own); env[ClaudeCode.ConfigVariable] = own; }
        try
        {
            var (code, outp) = await ConnRun(exe ?? "claude", args, Path.GetTempPath(), env);
            _connMsg = code == 0 ? done : "Claude meldet: " + string.Join(" ", outp.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).TakeLast(2));
            _core.Audit.Append(Principal.LocalOwner.Name, code == 0 ? "claude.erweiterung" : "claude.erweiterung.fehler", string.Join(" ", args.Take(3)));
        }
        catch (InvalidOperationException e) { _connMsg = e.Message; }
        Show("verbindungen");
    }

    /// <summary>Rückfrage vor jeder Änderung: Text und „✓ Ja“/„Abbrechen“.</summary>
    void ConnAsk(string text, Func<Task> run) { _connAsk = (text, run); _connMsg = ""; Show("verbindungen"); }
    public async Task ConnYes() { if (_connAsk is { } a) { _connAsk = null; await a.Run(); } }

    Border ConnCard(string name, string title, params Control[] rows)
    {
        var sp = new StackPanel { Spacing = 6, Children = { new TextBlock { Text = title, Foreground = Text, FontSize = 15, FontWeight = FontWeight.SemiBold } } };
        foreach (var r in rows) sp.Children.Add(r);
        return new Border { Name = name, Background = Brush.Parse("#14FFFFFF"), BorderBrush = EdgeLit, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 10), Margin = new Thickness(0, 0, 0, 10), Child = sp };
    }
    static WrapPanel ConnRow(params Control[] cs) { var w = new WrapPanel(); foreach (var c in cs) { c.Margin = new Thickness(0, 0, 6, 4); w.Children.Add(c); } return w; }
    static TextBox ConnBox(string name, string watermark, double width = 260) => new() { Name = name, Watermark = watermark, Width = width, FontSize = 12.5 };

    Control ConnectionsView()
    {
        var sp = new StackPanel { Spacing = 4 };
        sp.Children.Add(P("Hier verbindest du Trendo mit GitHub und Netlify und verwaltest die Erweiterungen deines Claude Code. Jede Person hat ihre eigene Anmeldung; geändert oder hochgeladen wird nur nach deinem Ja.", Muted, 12.5));
        if (_connAsk is { } ask)
        {
            var yes = Act("conn-yes", "✓ Ja", () => { _ = ConnYes(); }, true); var no = Act("conn-no", "Abbrechen", () => { _connAsk = null; Show("verbindungen"); });
            sp.Children.Add(ConnCard("conn-ask", "Bitte bestätigen", P(ask.Text, Text, 13), ConnRow(yes, no)));
        }
        if (_connMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "conn-msg", Text = _connMsg, Foreground = Text, FontSize = 12.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) });

        // GitHub
        if (!_ghChecked) { _ghChecked = true; _ = GitHubCheck().ContinueWith(_ => _ui.Post(() => { if (CurrentView == "verbindungen") Show("verbindungen"); })); }
        var gh = GhExe();
        var ghState = new TextBlock { Name = "conn-gh-state", Text = gh is null ? "GitHub CLI ist noch nicht installiert." : _ghUser is { } u ? $"✓ Angemeldet als {u}" : "Nicht angemeldet.",
            Foreground = _ghUser is not null ? Brush.Parse("#4ADE80") : Text, FontSize = 13, FontWeight = FontWeight.SemiBold };
        var ghRow = gh is null ? ConnRow(Act("conn-gh-install", "GitHub CLI einrichten", () => _ = GitHubInstall(), true))
            : _ghUser is null ? ConnRow(Act("conn-gh-login", "Mit GitHub anmelden", () => _ = GitHubLogin(), true), Act("conn-gh-check", "Neu prüfen", () => { _ = GitHubCheck().ContinueWith(_ => _ui.Post(() => Show("verbindungen"))); }))
            : ConnRow(Act("conn-gh-logout", "Abmelden", () => ConnAsk("Von GitHub abmelden? Deine Repos bleiben, Trendo kann nur nichts mehr hochladen.", GitHubLogout)));
        sp.Children.Add(ConnCard("conn-github", "GitHub", ghState,
            P("Mit der offiziellen GitHub CLI. Danach lädt „GitHub“ im Claude-Chat jedes Projekt als eigenes Repo hoch (Vorgabe: privat).", Muted, 11.5), ghRow));

        // Netlify
        var nl = Netlify; var nlOn = nl.Connected;
        var nlState = new TextBlock { Name = "conn-nl-state", Text = nlOn ? "✓ Verbunden – Webseiten veröffentlichst du im Claude-Chat mit „Netlify“." : "Nicht verbunden.", Foreground = nlOn ? Brush.Parse("#4ADE80") : Text, FontSize = 13, FontWeight = FontWeight.SemiBold };
        Control nlRow;
        if (nlOn) nlRow = ConnRow(Act("conn-nl-disconnect", "Trennen", () => ConnAsk("Netlify trennen? Der Zugangsschlüssel wird gelöscht; deine Seiten bleiben online.", () => { nl.Disconnect(); _connMsg = "Netlify getrennt."; _core.Audit.Append(Principal.LocalOwner.Name, "netlify.getrennt", ""); Show("verbindungen"); return Task.CompletedTask; })));
        else
        {
            var tok = ConnBox("conn-nl-token", "Zugangsschlüssel einfügen", 320); tok.PasswordChar = '•';
            var save = Act("conn-nl-save", "Verbinden", () =>
            {
                try { nl.Connect(tok.Text ?? ""); _connMsg = "✓ Mit Netlify verbunden."; _core.Audit.Append(Principal.LocalOwner.Name, "netlify.verbunden", ""); }
                catch (ArgumentException e) { _connMsg = e.Message; }
                Show("verbindungen");
            }, true);
            nlRow = new StackPanel { Spacing = 4, Children = {
                P("1. Bei Netlify einen persönlichen Zugangsschlüssel anlegen (öffnet sich im Browser). 2. Hier einfügen. Der Schlüssel liegt geschützt im Tresor dieser Person.", Muted, 11.5),
                ConnRow(Act("conn-nl-token-page", "Schlüssel bei Netlify anlegen", () => Launch(NetlifyLink.TokenPage)), tok, save) } };
        }
        sp.Children.Add(ConnCard("conn-netlify", "Netlify", nlState, nlRow));

        // Erweiterungen von Claude Code
        var (cfg, json) = ClaudeCfg();
        var ext = new StackPanel { Spacing = 4 };
        var skills = ClaudeExtensions.Skills(cfg); var plugins = ClaudeExtensions.Plugins(cfg); var markets = ClaudeExtensions.Marketplaces(cfg); var mcps = ClaudeExtensions.McpServers(json);
        ext.Children.Add(H($"Skills ({skills.Count})", 13));
        if (skills.Count == 0) ext.Children.Add(P("Noch keine eigenen Skills.", Muted, 11.5));
        foreach (var s in skills) ext.Children.Add(P($"• {s.Name}" + (s.Description.Length > 0 ? " – " + (s.Description.Length > 110 ? s.Description[..110] + " …" : s.Description) : ""), Text, 12));
        ext.Children.Add(H($"Plugins ({plugins.Count})", 13));
        for (var i = 0; i < plugins.Count; i++)
        {
            var p = plugins[i];
            var toggle = Act($"conn-plugin-toggle-{i}", p.Enabled ? "Ausschalten" : "Einschalten", () => ConnAsk($"Plugin „{p.Id}“ {(p.Enabled ? "ausschalten" : "einschalten")}?", () => ClaudeExtRun(ClaudeExtensions.PluginEnable(p.Id, !p.Enabled), $"Plugin „{p.Id}“ {(p.Enabled ? "ausgeschaltet" : "eingeschaltet")}.")));
            var del = Act($"conn-plugin-del-{i}", "Entfernen", () => ConnAsk($"Plugin „{p.Id}“ entfernen?", () => ClaudeExtRun(ClaudeExtensions.PluginRemove(p.Id), $"Plugin „{p.Id}“ entfernt.")));
            ext.Children.Add(ConnRow(new TextBlock { Text = $"{(p.Enabled ? "●" : "○")} {p.Id} {p.Version}", Foreground = Text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, toggle, del));
        }
        var plugIn = ConnBox("conn-plugin-id", "plugin@marktplatz");
        ext.Children.Add(ConnRow(plugIn, Act("conn-plugin-add", "Plugin installieren", () => Ask(plugIn.Text, v => ($"Plugin „{v}“ mit Claude Code installieren?", ClaudeExtensions.PluginInstall(v), $"Plugin „{v}“ installiert.")))));
        ext.Children.Add(H($"Marktplätze ({markets.Count})", 13));
        foreach (var m in markets) ext.Children.Add(P("• " + m, Text, 12));
        var mkIn = ConnBox("conn-market", "besitzer/repo oder Adresse");
        ext.Children.Add(ConnRow(mkIn, Act("conn-market-add", "Marktplatz hinzufügen", () => Ask(mkIn.Text, v => ($"Marktplatz „{v}“ hinzufügen? Plugins daraus installierst du danach einzeln.", ClaudeExtensions.MarketplaceAdd(v), $"Marktplatz „{v}“ hinzugefügt.")))));
        ext.Children.Add(H($"MCP-Server ({mcps.Count})", 13));
        for (var i = 0; i < mcps.Count; i++)
        {
            var m = mcps[i];
            var del = Act($"conn-mcp-del-{i}", "Entfernen", () => ConnAsk($"MCP-Server „{m.Name}“ entfernen?", () => ClaudeExtRun(ClaudeExtensions.McpRemove(m.Name), $"MCP-Server „{m.Name}“ entfernt.")));
            ext.Children.Add(ConnRow(new TextBlock { Text = $"• {m.Name} ({m.Kind}) {m.Target}", Foreground = Text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, del));
        }
        var mcpName = ConnBox("conn-mcp-name", "Name", 140); var mcpUrl = ConnBox("conn-mcp-url", "https://… (Adresse des Servers)", 280);
        ext.Children.Add(ConnRow(mcpName, mcpUrl, Act("conn-mcp-add", "MCP-Server hinzufügen", () =>
        {
            var n = (mcpName.Text ?? "").Trim(); var u = (mcpUrl.Text ?? "").Trim();
            try { var args = ClaudeExtensions.McpAddHttp(n, u); ConnAsk($"MCP-Server „{n}“ ({u}) für Claude Code hinzufügen? Nur vertrauenswürdige Server – Claude kann dann deren Werkzeuge nutzen.", () => ClaudeExtRun(args, $"MCP-Server „{n}“ hinzugefügt.")); }
            catch (ArgumentException e) { _connMsg = e.Message; Show("verbindungen"); }
        })));
        ext.Children.Add(P("Konnektoren deines claude.ai-Kontos verwaltest du bei Claude selbst; Trendo meldet sich dort nicht an. Geheime Werte (Schlüssel in MCP-Einstellungen) zeigt Trendo nie an.", Muted, 11));
        sp.Children.Add(ConnCard("conn-claude-ext", "Erweiterungen von Claude Code", ext));
        return KiPage("Verbindungen", sp);

        void Ask(string? input, Func<string, (string Text, string[] Args, string Done)> make)
        {
            var v = (input ?? "").Trim();
            try { var (t, a, d) = make(v); ConnAsk(t, () => ClaudeExtRun(a, d)); }
            catch (ArgumentException e) { _connMsg = e.Message; Show("verbindungen"); }
        }
    }

    // ───────────── Im Claude-Chat: ⬆ GitHub und 🌐 Netlify ─────────────
    public string? LastGitHubRepo { get; private set; }
    public string? LastNetlifyUrl { get; private set; }

    Control GitHubPanel(ClaudeDock dock, ClaudeChats.Chat? chat)
    {
        var sp = new StackPanel { Spacing = 6 };
        sp.Children.Add(new TextBlock { Text = "Auf GitHub hochladen", Foreground = CGold, FontSize = 15, FontWeight = FontWeight.SemiBold });
        if (chat is null) { sp.Children.Add(new TextBlock { Text = "Noch kein Projekt.", Foreground = ClaudeFg, FontSize = 13 }); return Appear(CGlass(sp, name: "claude-github")); }
        if (GhExe() is null || _ghUser is null)
        {
            if (!_ghChecked && GhExe() is not null) { _ = GitHubCheck().ContinueWith(_ => _ui.Post(() => RenderChat(dock))); }
            sp.Children.Add(new TextBlock { Name = "claude-github-need", Text = GhExe() is null ? "Dafür brauchst du die GitHub CLI und eine Anmeldung bei GitHub." : "Melde dich zuerst bei GitHub an.", Foreground = ClaudeFg, FontSize = 12.5, TextWrapping = TextWrapping.Wrap });
            var go = new Button { Name = "claude-github-connect", Content = "Zu den Verbindungen", FontSize = 12.5, Padding = new Thickness(12, 4), Background = ClaudeAccent, Foreground = Brushes.White, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(0) };
            go.Click += (_, _) => Show("verbindungen");
            sp.Children.Add(go);
            return Appear(CGlass(sp, name: "claude-github"));
        }
        var name = new TextBox { Name = "claude-github-name", Text = GitHubLink.SafeRepoName(chat.Title), FontSize = 12.5, Width = 260, HorizontalAlignment = HorizontalAlignment.Left };
        var priv = new CheckBox { Name = "claude-github-private", Content = "Privat (nur du siehst es)", IsChecked = true, Foreground = ClaudeFg };
        sp.Children.Add(new TextBlock { Text = $"Trendo legt bei {_ghUser} ein neues Repo an und lädt „{chat.Title}“ hoch – ohne Trendo-Hilfsdateien (.trendo) und ohne Schlüsseldateien.", Foreground = ClaudeFg, FontSize = 12.5, TextWrapping = TextWrapping.Wrap });
        sp.Children.Add(name); sp.Children.Add(priv);
        var yes = new Button { Name = "claude-github-yes", Content = "✓ Ja, hochladen", FontSize = 12.5, Padding = new Thickness(12, 4), Margin = new Thickness(0, 4, 6, 0), Background = ClaudeAccent, Foreground = Brushes.White, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(0) };
        var no = new Button { Name = "claude-github-no", Content = "Abbrechen", FontSize = 12.5, Padding = new Thickness(12, 4), Margin = new Thickness(0, 4, 0, 0), Background = Brushes.Transparent, Foreground = ClaudeFg, BorderBrush = GlassEdge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8) };
        yes.Click += (_, _) => _ = ClaudeGitHubPush(dock, chat, name.Text ?? "", priv.IsChecked == true);
        no.Click += (_, _) => { if (_chatUis.TryGetValue(dock, out var ui)) { ui.Panel = null; RenderChat(dock); } };
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { yes, no } });
        return Appear(CGlass(sp, name: "claude-github"));
    }

    async Task ClaudeGitHubPush(ClaudeDock dock, ClaudeChats.Chat chat, string repo, bool isPrivate)
    {
        string text;
        try
        {
            var steps = GitHubLink.PublishSteps(chat.Folder, GitHubLink.SafeRepoName(repo), isPrivate);
            var gh = GhExe() ?? throw new InvalidOperationException("GitHub CLI fehlt.");
            var env = GhEnv(); var outp = "";
            foreach (var (exe, args) in steps)
            {
                var (code, o) = await ConnRun(exe == "gh" ? gh : "git", args, chat.Folder, env); outp = o;
                if (code != 0) throw new InvalidOperationException($"„{exe} {args[0]}“ hat nicht geklappt: " + string.Join(" ", o.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).TakeLast(2)));
            }
            LastGitHubRepo = System.Text.RegularExpressions.Regex.Match(outp, @"https://github\.com/\S+") is { Success: true } m ? m.Value.TrimEnd('.') : $"https://github.com/{_ghUser}/{GitHubLink.SafeRepoName(repo)}";
            _core.Audit.Append(Principal.LocalOwner.Name, "github.repo.hochgeladen", (isPrivate ? "privat " : "öffentlich ") + GitHubLink.SafeRepoName(repo));
            text = $"Auf GitHub hochgeladen ({(isPrivate ? "privat" : "öffentlich")}): {LastGitHubRepo}";
            var url = LastGitHubRepo; Toast("Auf GitHub hochgeladen", ("Ansehen", () => Launch(url)));
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException or IOException) { text = "GitHub: " + e.Message; }
        CChats.Add(ChatStudio(dock), chat.Id, new ClaudeChats.Msg("assistant", text, [], DateTimeOffset.Now));
        if (_chatUis.TryGetValue(dock, out var ui)) ui.Panel = null;
        RenderChat(dock);
    }

    Control NetlifyPanel(ClaudeDock dock, ClaudeChats.Chat? chat)
    {
        var sp = new StackPanel { Spacing = 6 };
        sp.Children.Add(new TextBlock { Text = "Auf Netlify veröffentlichen", Foreground = CGold, FontSize = 15, FontWeight = FontWeight.SemiBold });
        if (chat is null) { sp.Children.Add(new TextBlock { Text = "Noch kein Projekt.", Foreground = ClaudeFg, FontSize = 13 }); return Appear(CGlass(sp, name: "claude-netlify")); }
        if (!Netlify.Connected)
        {
            sp.Children.Add(new TextBlock { Name = "claude-netlify-need", Text = "Verbinde zuerst Netlify (einmalig).", Foreground = ClaudeFg, FontSize = 12.5 });
            var go = new Button { Name = "claude-netlify-connect", Content = "Zu den Verbindungen", FontSize = 12.5, Padding = new Thickness(12, 4), Background = ClaudeAccent, Foreground = Brushes.White, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(0) };
            go.Click += (_, _) => Show("verbindungen"); sp.Children.Add(go);
            return Appear(CGlass(sp, name: "claude-netlify"));
        }
        var (site, url) = NetlifyLink.SiteOf(chat.Folder);
        sp.Children.Add(new TextBlock { Text = site is null ? $"Trendo legt bei Netlify eine neue Seite an und lädt „{chat.Title}“ hoch. Danach ist sie öffentlich im Internet erreichbar." : $"Trendo lädt den neuen Stand auf deine Seite {url} hoch – sie ist öffentlich erreichbar.", Foreground = ClaudeFg, FontSize = 12.5, TextWrapping = TextWrapping.Wrap });
        var yes = new Button { Name = "claude-netlify-yes", Content = "✓ Ja, veröffentlichen", FontSize = 12.5, Padding = new Thickness(12, 4), Margin = new Thickness(0, 4, 6, 0), Background = ClaudeAccent, Foreground = Brushes.White, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(0) };
        var no = new Button { Name = "claude-netlify-no", Content = "Abbrechen", FontSize = 12.5, Padding = new Thickness(12, 4), Margin = new Thickness(0, 4, 0, 0), Background = Brushes.Transparent, Foreground = ClaudeFg, BorderBrush = GlassEdge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8) };
        yes.Click += (_, _) => _ = ClaudeNetlifyDeploy(dock, chat);
        no.Click += (_, _) => { if (_chatUis.TryGetValue(dock, out var ui)) { ui.Panel = null; RenderChat(dock); } };
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { yes, no } });
        return Appear(CGlass(sp, name: "claude-netlify"));
    }

    async Task ClaudeNetlifyDeploy(ClaudeDock dock, ClaudeChats.Chat chat)
    {
        string text;
        if (_opt.Isolated && NetlifyHandlerOverride is null) text = "Testmodus: es wird nichts hochgeladen.";
        else try
        {
            var url = await Netlify.DeployAsync(chat.Folder, chat.Title);
            LastNetlifyUrl = url;
            _core.Audit.Append(Principal.LocalOwner.Name, "netlify.veroeffentlicht", url);
            text = $"Veröffentlicht: {url}";
            Toast("Auf Netlify veröffentlicht", ("Öffnen", () => Launch(url)));
        }
        catch (Exception e) when (e is InvalidOperationException or HttpRequestException or IOException or TaskCanceledException) { text = "Netlify: " + e.Message; }
        CChats.Add(ChatStudio(dock), chat.Id, new ClaudeChats.Msg("assistant", text, [], DateTimeOffset.Now));
        if (_chatUis.TryGetValue(dock, out var ui)) ui.Panel = null;
        RenderChat(dock);
    }
}
