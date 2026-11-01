using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>T-0316: Claude über das Abo des Nutzers – so einfach wie möglich verbunden. Trendo nutzt das installierte Claude Code (eigene Anmeldung von Claude,
/// Trendo sieht nie Passwort oder Schlüssel), zeigt den Stand mit einem Satz und macht Claude zur Haupt-KI, solange es verbunden ist.</summary>
public partial class MainWindow
{
    ClaudeCode.Status _claude = ClaudeCode.Status.Missing; DateTime _claudeAt = DateTime.MinValue; string _claudeMsg = "";
    public string ClaudeMessage => _claudeMsg;
    public ClaudeCode.Status ClaudeState => _claude;
    /// <summary>Eingebettete Abo-Nutzung (Haupt-KI, Läufe im Hintergrund) erst nach Freigabe durch Anthropic – siehe <see cref="ClaudeCode.EmbeddingApproved"/>. Für Tests umschaltbar.</summary>
    public static bool ClaudeEmbedApproved { get; set; } = ClaudeCode.EmbeddingApproved;
    /// <summary>Für Tests: statt das echte Claude Code zu öffnen (Ordner, Argumente).</summary>
    public static Action<string, IReadOnlyList<string>>? ClaudeOpenOverride { get; set; }

    /// <summary>„In Claude Code öffnen“: das echte Claude Code sichtbar im Projektordner starten – der Nutzer arbeitet selbst darin, über sein Abo (API-Variablen entfernt).
    /// Regelkonform ohne Freigabe: Trendo nutzt das Abo nicht im Hintergrund, es startet nur das Programm.</summary>
    public void ClaudeOpen(string folder, string? prompt = null, string? system = null, string? studio = null)
    {
        // T-0317: passende Skills als Claude-Code-Skills in den Projektordner legen – Claude Code nutzt sie wie eigene
        if (studio is not null) try { Directory.CreateDirectory(folder); Skills.ExportForClaude(folder, null, studio); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        var args = ClaudeCode.OpenArgs(prompt, system);
        _core.Audit.Append(Principal.LocalOwner.Name, "claude.open", Path.GetFileName(folder));
        if (ClaudeOpenOverride is { } o) { o(folder, args); return; }
        if (ClaudeSandboxed("Claude Code im Ordner öffnen")) return;
        var exe = ClaudeExe(); if (exe is null) { _claudeMsg = "Claude Code ist nicht installiert."; RefreshKiPage(); return; }
        try
        {
            Directory.CreateDirectory(folder);
            var psi = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = false, WorkingDirectory = folder };
            psi.ArgumentList.Add("/k"); psi.ArgumentList.Add(exe); foreach (var a in args) psi.ArgumentList.Add(a);
            ClaudeEnv(psi);
            Process.Start(psi);
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or IOException) { _claudeMsg = "Claude Code ließ sich nicht öffnen: " + e.Message; RefreshKiPage(); }
    }

    /// <summary>Für Tests: Anmeldestand und Läufe ohne echtes Claude (Argumente, Ordner, Zeile → Exitcode).</summary>
    public static Func<Task<ClaudeCode.Status>>? ClaudeStatusOverride { get; set; }
    public static Func<IReadOnlyList<string>, string, Action<string>, CancellationToken, Task<int>>? ClaudeRunOverride { get; set; }

    string? ClaudeExe() => ClaudeCode.Find(Environment.GetEnvironmentVariable, File.Exists);

    /// <summary>Testmodus (eigener Datenordner): nie ein echtes Programm starten – nur melden und protokollieren, was passieren würde.
    /// Sonst öffnen Knopf-Prüfungen echte Fenster (Installer, Anmeldung, setup-token) auf dem PC.</summary>
    bool ClaudeSandboxed(string what)
    {
        if (!_opt.Isolated) return false;
        _claudeMsg = $"Testmodus: würde jetzt {what} – nichts gestartet.";
        _core.Audit.Append(Principal.LocalOwner.Name, "claude.testmodus", what);
        RefreshKiPage();
        return true;
    }
    public const string ClaudeTokenKey = "claude-abo-token";

    /// <summary>Nur Abo: Variablen für die kostenpflichtige API entfernen; ein gespeicherter Abo-Token (claude setup-token) wird mitgegeben.</summary>
    void ClaudeEnv(ProcessStartInfo psi)
    {
        foreach (var v in ClaudeCode.ApiVariables) psi.Environment.Remove(v);
        psi.Environment.Remove(ClaudeCode.TokenVariable);
        try { if (_core.Secrets.Get(ClaudeTokenKey) is { Length: > 0 } t) psi.Environment[ClaudeCode.TokenVariable] = t; } catch (Exception) { }
    }

    /// <summary>Abo-Token speichern (sicherer Windows-Speicher). API-Schlüssel werden abgelehnt – Trendo nutzt Claude nur über das Abo.</summary>
    public async Task<bool> ClaudeSetToken(string token)
    {
        token = token.Trim();
        if (ClaudeCode.LooksLikeApiKey(token)) { _claudeMsg = "Das ist ein API-Schlüssel (kostenpflichtig). Trendo nutzt Claude nur über dein Abo – bitte den Abo-Token aus „claude setup-token“ nehmen."; RefreshKiPage(); return false; }
        if (!ClaudeCode.LooksLikeAboToken(token)) { _claudeMsg = "Das sieht nicht wie ein Abo-Token aus. Er beginnt mit „sk-ant-oat“."; RefreshKiPage(); return false; }
        _core.Secrets.Set(ClaudeTokenKey, token); _core.Audit.Append(Principal.LocalOwner.Name, "claude.token.set", "");
        var s = await ClaudeCheck(true);
        _claudeMsg = s.Ready ? "Abo-Token gespeichert – Claude ist verbunden." : "Abo-Token gespeichert, aber Claude meldet noch keine Verbindung. Ist der Token abgelaufen?";
        RefreshKiPage(); return s.Ready;
    }
    public void ClaudeRemoveToken() { _core.Secrets.Delete(ClaudeTokenKey); _core.Audit.Append(Principal.LocalOwner.Name, "claude.token.removed", ""); _claudeAt = DateTime.MinValue; _claudeMsg = "Abo-Token entfernt."; RefreshKiPage(); }

    /// <summary>„Abo-Token erzeugen“: öffnet <c>claude setup-token</c> im Fenster; den angezeigten Token fügst du danach hier ein.</summary>
    public void ClaudeMakeToken()
    {
        if (ClaudeSandboxed("„claude setup-token“ öffnen")) return;
        var exe = ClaudeExe(); if (exe is null) return;
        try { var psi = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = false, WorkingDirectory = Path.GetTempPath() }; foreach (var a in new[] { "/k", exe, "setup-token" }) psi.ArgumentList.Add(a); ClaudeEnv(psi); Process.Start(psi); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { _claudeMsg = e.Message; }
        _claudeMsg = "Im neuen Fenster meldest du dich über dein Abo an; danach steht dort ein Token (sk-ant-oat…). Kopiere ihn und füge ihn hier ein.";
        RefreshKiPage();
    }

    /// <summary>Anmeldestand (kostenlos, ohne Nutzung des Abos); 5 Minuten zwischengespeichert.</summary>
    public async Task<ClaudeCode.Status> ClaudeCheck(bool fresh = false)
    {
        if (!fresh && (DateTime.UtcNow - _claudeAt).TotalMinutes < 5) return _claude;
        if (ClaudeStatusOverride is { } o) { _claude = await o(); _claudeAt = DateTime.UtcNow; return _claude; }
        if (_opt.Isolated) { _claudeAt = DateTime.UtcNow; return _claude = ClaudeCode.Status.Missing; } // Tests und Vorschaubilder nutzen nie das echte Abo
        var exe = ClaudeExe();
        if (exe is null) { _claudeAt = DateTime.UtcNow; return _claude = ClaudeCode.Status.Missing; }
        try
        {
            var psi = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = System.Text.Encoding.UTF8, WorkingDirectory = Path.GetTempPath() };
            psi.ArgumentList.Add("auth"); psi.ArgumentList.Add("status"); psi.ArgumentList.Add("--json"); ClaudeEnv(psi);
            using var p = Process.Start(psi)!;
            using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var json = await p.StandardOutput.ReadToEndAsync(limit.Token); await p.WaitForExitAsync(limit.Token);
            _claude = ClaudeCode.ParseStatus(exe, json);
        }
        catch (Exception e) when (e is OperationCanceledException or System.ComponentModel.Win32Exception or IOException or InvalidOperationException) { _claude = new(true, false, "", "", exe); }
        _claudeAt = DateTime.UtcNow;
        return _claude;
    }

    /// <summary>Einen Claude-Lauf starten und jede Ausgabezeile weitergeben. Arbeitsordner = Projekt (bzw. leerer Temp-Ordner für Fragen).</summary>
    public async Task<int> ClaudeRun(IReadOnlyList<string> args, string dir, Action<string> line, CancellationToken ct)
    {
        if (!ClaudeEmbedApproved) throw new InvalidOperationException("Eingebettete Claude-Nutzung über das Abo wartet auf die Freigabe durch Anthropic. Nutze „In Claude Code öffnen“.");
        if (ClaudeRunOverride is { } o) return await o(args, dir, line, ct);
        var exe = _claude.Exe ?? ClaudeExe() ?? throw new InvalidOperationException("Claude Code ist nicht installiert.");
        var psi = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8, StandardErrorEncoding = System.Text.Encoding.UTF8, WorkingDirectory = dir };
        foreach (var a in args) psi.ArgumentList.Add(a);
        ClaudeEnv(psi);
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Claude Code ließ sich nicht starten.");
        p.StandardInput.Close();
        var err = new System.Text.StringBuilder();
        p.OutputDataReceived += (_, e) => { if (e.Data is { Length: > 0 } s) line(s); };
        p.ErrorDataReceived += (_, e) => { if (e.Data is { Length: > 0 } s) lock (err) err.AppendLine(s); };
        p.BeginOutputReadLine(); p.BeginErrorReadLine();
        try { await p.WaitForExitAsync(ct); }
        catch (OperationCanceledException) { try { p.Kill(true); } catch (InvalidOperationException) { } throw; }
        if (p.ExitCode != 0 && err.Length > 0) line("{\"type\":\"trendo_error\",\"text\":" + System.Text.Json.JsonSerializer.Serialize(err.ToString().Trim()) + "}");
        return p.ExitCode;
    }

    /// <summary>„Mit Claude verbinden“: offizielle Anmeldung von Claude im Browser (Abo). Trendo sieht dabei weder Passwort noch Schlüssel.</summary>
    public async Task ClaudeConnect()
    {
        if (ClaudeSandboxed("die Claude-Anmeldung öffnen")) return;
        var exe = ClaudeExe(); if (exe is null) { _claudeMsg = "Zuerst Claude Code installieren."; RefreshKiPage(); return; }
        _core.Audit.Append(Principal.LocalOwner.Name, "claude.login.start", "");
        _claudeMsg = "Die Claude-Anmeldung öffnet sich im Browser – melde dich dort mit deinem Claude-Konto an. Danach hier „Neu prüfen“.";
        RefreshKiPage();
        try
        {
            var psi = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = false, WorkingDirectory = Path.GetTempPath() };
            psi.ArgumentList.Add("auth"); psi.ArgumentList.Add("login"); psi.ArgumentList.Add("--claudeai");
            using var p = Process.Start(psi);
            if (p is not null) await p.WaitForExitAsync();
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { _claudeMsg = "Anmeldung ließ sich nicht starten: " + e.Message; }
        var s = await ClaudeCheck(true);
        _claudeMsg = s.Ready ? "Verbunden. Claude ist jetzt deine Haupt-KI in Trendo." : _claudeMsg;
        _core.Audit.Append(Principal.LocalOwner.Name, s.Ready ? "claude.login.ok" : "claude.login.offen", s.Plan);
        RefreshKiPage();
    }

    /// <summary>Claude Code mit dem offiziellen Installer von claude.ai einrichten – nur für diesen Benutzer, ohne Admin, nach Zustimmung.</summary>
    public async Task ClaudeInstall()
    {
        if (ClaudeSandboxed("den Claude-Code-Installer starten")) return;
        _core.Audit.Append(Principal.LocalOwner.Name, "claude.install.start", "https://claude.ai/install.ps1");
        _claudeMsg = "Claude Code wird aus der offiziellen Quelle (claude.ai) eingerichtet …"; RefreshKiPage();
        try
        {
            var psi = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = false, WorkingDirectory = Path.GetTempPath() };
            foreach (var a in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", "irm https://claude.ai/install.ps1 | iex" }) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi); if (p is not null) await p.WaitForExitAsync();
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { _claudeMsg = "Einrichten ließ sich nicht starten: " + e.Message; }
        var s = await ClaudeCheck(true);
        _claudeMsg = s.Installed ? "Claude Code ist eingerichtet. Jetzt noch „Mit Claude verbinden“." : "Claude Code wurde nicht gefunden – bitte noch einmal versuchen.";
        _core.Audit.Append(Principal.LocalOwner.Name, s.Installed ? "claude.install.ok" : "claude.install.fail", "");
        RefreshKiPage();
    }

    /// <summary>Studios, in denen programmiert wird – dort gibt es „In Claude Code öffnen“ in der Auftragsleiste.</summary>
    public static readonly HashSet<string> CodeStudios = ["CORE-07", "X-03", "X-04", "X-10", "CORE-06"];

    /// <summary>Ordner für Claude Code: neuestes Trendo-Projekt, sonst der Trendo-Projektordner.</summary>
    public string ClaudeFolderFor(string? studio) => NewestProject() is { } id ? _core.Projects.ProjectFilesRoot(id) : ProjectsFolderForClaude();

    /// <summary>Rolle für Claude Code passend zum Studio (wird angehängt; das eigene Verhalten von Claude Code bleibt).</summary>
    public string ClaudeRoleFor(string? studio) =>
        $"Du arbeitest für Trendo OS im Studio „{(studio is null ? "Programmieren" : StudioCatalog.NameOf(studio, studio))}“. Antworte auf Deutsch, plane zuerst sichtbar (Aufgabenliste), frage bei Unklarem nach und arbeite professionell und barrierearm." +
        (studio is not null && Skills.For(null, studio).Count > 0 ? " Nutze die Trendo-Skills unter .claude/skills (trendo-…) und prüfe dein Ergebnis vor dem Abschluss gegen deren Checklisten; bessere nach, bis alle Punkte erfüllt sind." : "");

    /// <summary>Knopf „In Claude Code öffnen“ (nur wenn Claude Code installiert ist).</summary>
    Button? ClaudeOpenButton(string name, Func<string> folder, Func<string?> prompt, string? studio)
    {
        if (!_claude.Installed) { if (!_opt.Isolated || ClaudeStatusOverride is not null) _ = ClaudeCheck(); return null; }
        var b = Act(name, "In Claude Code öffnen", () => ClaudeOpen(folder(), prompt(), ClaudeRoleFor(studio), studio));
        ToolTip.SetTip(b, _claude.Ready ? "Öffnet dein Claude Code sichtbar im Projektordner – mit deinem Auftrag. Du arbeitest dort direkt mit deinem Claude-Abo." : "Claude Code ist installiert, aber nicht angemeldet – unter „KI-Quellen“ mit Claude verbinden.");
        b.IsEnabled = _claude.Ready;
        return b;
    }

    string ProjectsFolderForClaude() => _settings.ProjectsFolder is { Length: > 0 } pf ? pf : UserSettings.DefaultProjectsFolder();

    /// <summary>„Neu prüfen“: Stand frisch lesen und das Ergebnis mit Uhrzeit melden (sichtbare Rückmeldung).</summary>
    async Task ClaudeRecheck()
    {
        var st = await ClaudeCheck(true);
        _claudeMsg = $"Geprüft um {DateTime.Now:HH:mm}: {st.Text}";
        RefreshKiPage();
    }

    void RefreshKiPage() { if (CurrentView is "ki-quellen" or "ki" or "ki-fragen") Show(CurrentView); }

    /// <summary>Karte „Claude (dein Abo)“: Stand in einem Satz, ein Knopf für den nächsten Schritt, Haupt-KI an/aus.</summary>
    Control ClaudeCard()
    {
        var s = _claude; var set = KiSourceSettings.Load(_core.DataRoot);
        // Stand im Hintergrund auffrischen; neu zeichnen nur, wenn er sich geändert hat (sonst endlose Schleife Prüfen → Zeichnen → Prüfen)
        if ((DateTime.UtcNow - _claudeAt).TotalMinutes >= 5) { var before = _claude; _ = ClaudeCheck().ContinueWith(t => { if (t.Result != before) Avalonia.Threading.Dispatcher.UIThread.Post(RefreshKiPage); }); }
        var buttons = new WrapPanel();
        void Add(Control c) { c.Margin = new Thickness(0, 0, 6, 4); buttons.Children.Add(c); }
        if (s.Installed) Add(Act("claude-open", "Claude Code öffnen", () => ClaudeOpen(ProjectsFolderForClaude()), s.Ready));
        if (!s.Installed) Add(Act("claude-install", "Claude Code einrichten", () => _ = ClaudeInstall(), true));
        else if (!s.Ready) Add(Act("claude-connect", "Mit Claude verbinden", () => _ = ClaudeConnect(), true));
        Add(Act("claude-check", "Neu prüfen", () => _ = ClaudeRecheck()));
        var main = new CheckBox { Name = "claude-main", Content = "Claude als Haupt-KI nutzen (wenn verbunden)", IsChecked = set.ClaudeMain, Foreground = Text, IsVisible = ClaudeEmbedApproved };
        var gate = P("Mit „Claude Code öffnen“ startest du dein echtes Claude Code im passenden Ordner und arbeitest dort direkt mit deinem Abo – so, wie Anthropic es für das Abo vorsieht. " +
            "Im Hintergrund der Trendo-Programme nutzt Trendo dein Abo nicht; dort arbeitet die KI auf diesem PC.", Brush.Parse("#FACC15"), 11.5);
        gate.Name = "claude-gate"; gate.IsVisible = !ClaudeEmbedApproved;
        main.IsCheckedChanged += (_, _) => KiSourceSettings.Update(_core.DataRoot, x => x.ClaudeMain = main.IsChecked == true);
        var hasToken = false; try { hasToken = _core.Secrets.Get(ClaudeTokenKey) is { Length: > 0 }; } catch (Exception) { }
        var tokBox = new TextBox { Name = "claude-token", PasswordChar = '•', PlaceholderText = "Abo-Token (sk-ant-oat…)", Width = 300 };
        var tokenRow = new Expander { Name = "claude-token-box", Header = hasToken ? "Abo-Token ✓ (statt Anmeldung)" : "Andere Möglichkeit: Abo-Token", IsExpanded = false,
            Content = new StackPanel { Spacing = 6, Children = {
                P("Für Fortgeschrittene: „claude setup-token“ erzeugt einen langlebigen Token für dein Abo. Trendo legt ihn im sicheren Windows-Speicher ab und gibt ihn nur an Claude Code weiter. API-Schlüssel nimmt Trendo nicht an.", Muted, 11.5),
                hasToken ? new WrapPanel { Children = { Act("claude-token-remove", "Token entfernen", ClaudeRemoveToken) } }
                    : new WrapPanel { Children = { Act("claude-token-make", "Token erzeugen …", ClaudeMakeToken), new Border { Width = 6 }, tokBox, new Border { Width = 6 }, Act("claude-token-save", "Speichern", () => _ = ClaudeSetToken(tokBox.Text ?? ""), true) } } } } };
        return new Border
        {
            Name = "claude-card", Background = Brush.Parse(s.Ready ? "#1A22B35E" : "#1A3B82F6"), BorderBrush = Brush.Parse(s.Ready ? "#4ADE80" : "#D4A84B"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 10), Margin = new Thickness(0, 0, 0, 10),
            Child = new StackPanel { Spacing = 6, Children = {
                new TextBlock { Text = "Claude (dein Abo)", Foreground = Text, FontSize = 15, FontWeight = FontWeight.SemiBold },
                new TextBlock { Name = "claude-state", Text = s.Text, Foreground = s.Ready ? Brush.Parse("#4ADE80") : Text, FontSize = 13, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                P(s.Ready && !ClaudeEmbedApproved ? "Claude Code ist mit deinem Abo bereit. „Claude Code öffnen“ startet es sichtbar im Projektordner – dort arbeitest du direkt mit Claude, ohne API."
                  : s.Ready ? "Claude beantwortet Fragen, plant und baut in allen Programmen – wo programmiert wird, arbeitet es wie Claude Code. Volle Funktionen (Befehle, Web, deine Skills) gibst du je Projekt frei. Deine Dokumente aus „Wissen“ gehen nicht automatisch mit."
                  : "Einmal verbinden, dann ist Claude deine Haupt-KI in Trendo. Die Anmeldung läuft über Claude selbst – Trendo sieht nie dein Passwort. Ohne Claude arbeitet die KI auf diesem PC weiter.", Muted, 12),
                gate, main, buttons, tokenRow,
                new TextBlock { Name = "claude-msg", Text = _claudeMsg, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap, IsVisible = _claudeMsg.Length > 0 } } },
        };
    }
}

/// <summary>Claude als KI-Quelle für <see cref="IntelligenceCore"/> – Fragen laufen über Claude Code mit dem Abo des Nutzers (Frage-Modus: ändert keine Dateien, eingegrenzt).</summary>
public sealed class ClaudeKiBackend(MainWindow owner, Func<string> model) : IMainKiBackend, IStreamingKiBackend
{
    public string Name => "Claude (dein Abo)";
    public string BaseUrl => "claude-code";
    public async Task<bool> IsUpAsync(CancellationToken ct = default) => (await owner.ClaudeCheck()).Ready; // nur über das Abo
    public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>([model() is { Length: > 0 } m ? m : "claude"]);
    public Task<string> ChatAsync(string m, IReadOnlyList<KiMessage> messages, CancellationToken ct = default) => ChatStreamAsync(m, messages, _ => { }, ct);

    public async Task<string> ChatStreamAsync(string m, IReadOnlyList<KiMessage> messages, Action<string> onDelta, CancellationToken ct = default)
    {
        var system = string.Join("\n", messages.Where(x => x.Role == "system").Select(x => x.Content));
        var turns = messages.Where(x => x.Role != "system").ToList();
        var question = turns.LastOrDefault()?.Content ?? "";
        var prompt = turns.Count > 1 ? "Bisheriger Verlauf:\n" + string.Join("\n", turns.SkipLast(1).Select(t => (t.Role == "user" ? "Nutzer: " : "Trendo-KI: ") + t.Content)) + "\n\nNeue Frage: " + question : question;
        var dir = Path.Combine(Path.GetTempPath(), "trendo-claude-frage"); Directory.CreateDirectory(dir);
        var text = new System.Text.StringBuilder(); string? final = null; string? error = null;
        var code = await owner.ClaudeRun(ClaudeCode.Args(prompt, ClaudeCode.Phase.Ask, system: system, model: m == "claude" ? null : m), dir, l =>
        {
            if (l.Contains("\"trendo_error\"")) { error = System.Text.Json.Nodes.JsonNode.Parse(l)?["text"]?.ToString(); return; }
            foreach (var ev in ClaudeCode.Parse(l, dir))
                if (ev is ClaudeCode.Say say) { text.AppendLine(say.Text); onDelta(say.Text + "\n"); }
                else if (ev is ClaudeCode.Done d) final = d.Text;
        }, ct);
        if (final is { Length: > 0 }) return final;
        if (text.Length > 0) return text.ToString().Trim();
        throw new InvalidOperationException(error ?? $"Claude hat nicht geantwortet (Code {code}).");
    }
}
