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
        if (studio is not null) try { Directory.CreateDirectory(folder); Skills.ExportForClaude(folder, null, studio); ClaudeTeamExport(folder, studio); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
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
    /// Sonst öffnen Knopf-Prüfungen echte Fenster (Installer, Anmeldung) auf dem PC.</summary>
    bool ClaudeSandboxed(string what)
    {
        if (!_opt.Isolated) return false;
        _claudeMsg = $"Testmodus: würde jetzt {what} – nichts gestartet.";
        _core.Audit.Append(Principal.LocalOwner.Name, "claude.testmodus", what);
        RefreshKiPage();
        return true;
    }
    /// <summary>Früher gespeicherter Abo-Token (bis 0.9.30). Anthropic erlaubt fremden Programmen nicht, Claude-Tokens zu speichern – Trendo löscht ihn beim Start.</summary>
    public const string ClaudeTokenKey = "claude-abo-token";

    /// <summary>Nur Abo, nur Anthropics eigene Anmeldung: Variablen für die kostenpflichtige API und Tokens entfernen; jede weitere Person hat ihren eigenen Claude-Code-Ordner.</summary>
    void ClaudeEnv(ProcessStartInfo psi)
    {
        foreach (var v in ClaudeCode.ApiVariables) psi.Environment.Remove(v);
        psi.Environment.Remove(ClaudeCode.TokenVariable);
        psi.Environment.Remove(ClaudeCode.ConfigVariable);
        if (ClaudeCode.PersonConfigDir(_core.DataRoot) is { } own) { Directory.CreateDirectory(own); psi.Environment[ClaudeCode.ConfigVariable] = own; } // eigene Anmeldung je Person
    }

    /// <summary>Alten gespeicherten Token entfernen (einmal beim Start; öffentlich für Tests). Gibt true zurück, wenn einer da war.</summary>
    public bool ClaudeForgetStoredToken()
    {
        try { if (_core.Secrets.Get(ClaudeTokenKey) is null) return false; _core.Secrets.Delete(ClaudeTokenKey); }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { return false; }
        _core.Audit.Append(Principal.LocalOwner.Name, "claude.token.entfernt", "Anmeldung nur noch über Claude selbst");
        return true;
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

    /// <summary>Wie lange Claude nichts melden darf, bevor Trendo den Lauf beendet (Wächter).</summary>
    public static TimeSpan ClaudeSilenceLimit { get; set; } = TimeSpan.FromMinutes(20);

    /// <summary>Einen Claude-Lauf starten und jede Ausgabezeile weitergeben. Arbeitsordner = Projekt (bzw. leerer Temp-Ordner für Fragen).</summary>
    public async Task<int> ClaudeRun(IReadOnlyList<string> args, string dir, Action<string> line, CancellationToken ct)
    {
        if (!ClaudeEmbedApproved) throw new InvalidOperationException("Eingebettete Claude-Nutzung über das Abo wartet auf die Freigabe durch Anthropic. Nutze „In Claude Code öffnen“.");
        if (ClaudeRunOverride is { } o) return await o(args, dir, line, ct);
        if (_opt.Isolated) throw new InvalidOperationException("Testmodus: Claude wird nicht gestartet."); // Tests und Vorschaubilder nutzen nie das echte Abo
        var exe = _claude.Exe ?? ClaudeExe() ?? throw new InvalidOperationException("Claude Code ist nicht installiert.");
        var psi = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8, StandardErrorEncoding = System.Text.Encoding.UTF8, WorkingDirectory = dir };
        foreach (var a in args) psi.ArgumentList.Add(a);
        ClaudeEnv(psi);
        return await ClaudeRunProcess(psi, dir, line, ct);
    }

    /// <summary>Prozess eines Claude-Laufs abwarten (öffentlich für Tests mit einem echten Windows-Prozess): fertig, sobald Claude „result“ meldet – auch wenn
    /// mitgestartete MCP-Werkzeuge ihn offen halten; Wächter beendet ihn, wenn er zu lange nichts meldet; Mitschrift in .trendo/letzter-lauf.jsonl.</summary>
    public static async Task<int> ClaudeRunProcess(ProcessStartInfo psi, string dir, Action<string> line, CancellationToken ct)
    {
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Claude Code ließ sich nicht starten.");
        p.StandardInput.Close();
        var err = new System.Text.StringBuilder();
        // Mitschrift des Laufs im Projekt (.trendo/letzter-lauf.jsonl) – zum Nachsehen, wenn etwas hakt
        StreamWriter? log = null;
        try { Directory.CreateDirectory(Path.Combine(dir, ".trendo")); log = new StreamWriter(Path.Combine(dir, ".trendo", "letzter-lauf.jsonl"), false, new System.Text.UTF8Encoding(false)) { AutoFlush = true }; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lastOutput = DateTime.UtcNow;
        p.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not { Length: > 0 } s) return;
            lastOutput = DateTime.UtcNow;
            lock (p) log?.WriteLine(s);
            try { line(s); }
            catch (Exception ex) { try { CrashLog.Write(Path.Combine(dir, ".trendo"), ex, "Claude-Ausgabe (abgefangen)"); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
            if (s.Contains("\"type\":\"result\"")) done.TrySetResult(); // Claude ist fertig – auch wenn MCP-Werkzeuge den Prozess noch offen halten
        };
        p.ErrorDataReceived += (_, e) => { if (e.Data is { Length: > 0 } s) lock (err) err.AppendLine(s); };
        p.BeginOutputReadLine(); p.BeginErrorReadLine();
        var silent = false;
        try
        {
            var exit = p.WaitForExitAsync(ct);
            while (!exit.IsCompleted && !done.Task.IsCompleted)
            {
                await Task.WhenAny(exit, done.Task, Task.Delay(TimeSpan.FromSeconds(Math.Clamp(ClaudeSilenceLimit.TotalSeconds / 4, 0.2, 15)), ct));
                if (!exit.IsCompleted && !done.Task.IsCompleted && DateTime.UtcNow - lastOutput > ClaudeSilenceLimit) { silent = true; break; } // Wächter: zu lange nichts gemeldet
            }
            if (done.Task.IsCompleted && !exit.IsCompleted) await Task.WhenAny(exit, Task.Delay(TimeSpan.FromSeconds(8), ct)); // kurz aufräumen lassen
            await exit.WaitAsync(TimeSpan.Zero).ContinueWith(_ => { }, TaskScheduler.Default);
        }
        catch (OperationCanceledException) { try { p.Kill(true); } catch (InvalidOperationException) { } lock (p) log?.Dispose(); throw; }
        if (!p.HasExited) try { p.Kill(true); } catch (InvalidOperationException) { } // eigener Lauf: samt der von ihm gestarteten Werkzeuge beenden
        lock (p) { log?.Dispose(); log = null; }
        if (silent) { line("{\"type\":\"trendo_error\",\"text\":" + System.Text.Json.JsonSerializer.Serialize($"Claude hat {ClaudeSilenceLimit.TotalMinutes:0} Minuten nichts mehr gemeldet – Trendo hat den Lauf beendet. Bisher Gemachtes bleibt; schreib „mach weiter“, um fortzusetzen.") + "}"); return -1; }
        if (done.Task.IsCompleted) return 0;
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
            psi.ArgumentList.Add("auth"); psi.ArgumentList.Add("login"); psi.ArgumentList.Add("--claudeai"); ClaudeEnv(psi); // Anmeldung landet im Claude-Ordner dieser Person
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
        (StudioProfiles.Has(studio) ? StudioProfiles.For(studio).Role + " " : "") +
        $"Du arbeitest für Trendo OS im Studio „{(studio is null ? "Programmieren" : StudioCatalog.NameOf(studio, studio))}“. Antworte auf Deutsch, plane zuerst sichtbar (Aufgabenliste), frage bei Unklarem nach und arbeite professionell und barrierearm." +
        (studio is not null && Skills.For(null, studio).Count > 0 ? " Nutze die Trendo-Skills unter .claude/skills (trendo-…) und prüfe dein Ergebnis vor dem Abschluss gegen deren Checklisten; bessere nach, bis alle Punkte erfüllt sind." : "");

    /// <summary>T-0630: nur für Wege, die das Team vorher als Agenten in den Projektordner legen (Claude-Chat, „In Claude Code öffnen“).</summary>
    public string ClaudeTeamRule(string? studio) => StudioTeams.For(studio) is { } team
        ? $" Dein {team.Name} steht als Agenten unter .claude/agents (trendo-…) bereit: Ziehe ohne Rückfrage die Rollen hinzu, die der Auftrag braucht ({string.Join(", ", team.Roles.Select(r => r.Name))})."
        : "";

    /// <summary>Knopf „In Claude Code öffnen“ (nur wenn Claude Code installiert ist).</summary>
    Button? ClaudeOpenButton(string name, Func<string> folder, Func<string?> prompt, string? studio)
    {
        if (!_claude.Installed) { if (!_opt.Isolated || ClaudeStatusOverride is not null) _ = ClaudeCheck(); return null; }
        var b = Act(name, "In Claude Code öffnen", () => ClaudeOpen(folder(), prompt(), ClaudeRoleFor(studio) + ClaudeTeamRule(studio), studio));
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
        if ((DateTime.UtcNow - _claudeAt).TotalMinutes >= 5) { var before = _claude; _ = ClaudeCheck().ContinueWith(t => { if (t.Result != before) _ui.Post(RefreshKiPage); }); }
        var buttons = new WrapPanel();
        void Add(Control c) { c.Margin = new Thickness(0, 0, 6, 4); buttons.Children.Add(c); }
        if (s.Installed) Add(Act("claude-open", "Claude Code öffnen", () => ClaudeOpen(ProjectsFolderForClaude()), s.Ready));
        if (!s.Installed) Add(Act("claude-install", "Claude Code einrichten", () => _ = ClaudeInstall(), true));
        else if (!s.Ready) Add(Act("claude-connect", "Mit Claude verbinden", () => _ = ClaudeConnect(), true));
        Add(Act("claude-check", "Neu prüfen", () => _ = ClaudeRecheck()));
        Add(Act("claude-links", "GitHub, Netlify & Erweiterungen", () => Show("verbindungen")));
        var main = new CheckBox { Name = "claude-main", Content = "Claude als Haupt-KI nutzen (wenn verbunden)", IsChecked = set.ClaudeMain, Foreground = Text, IsVisible = ClaudeEmbedApproved };
        var gate = P("Mit „Claude Code öffnen“ startest du dein echtes Claude Code im passenden Ordner und arbeitest dort direkt mit deinem Abo – so, wie Anthropic es für das Abo vorsieht. " +
            "Im Hintergrund der Trendo-Programme nutzt Trendo dein Abo nicht; dort arbeitet die KI auf diesem PC.", Brush.Parse("#FACC15"), 11.5);
        gate.Name = "claude-gate"; gate.IsVisible = !ClaudeEmbedApproved;
        main.IsCheckedChanged += (_, _) => KiSourceSettings.Update(_core.DataRoot, x => x.ClaudeMain = main.IsChecked == true);
        var always = new CheckBox { Name = "claude-always-full", Content = "Claude darf im Claude-Chat bei Bedarf ins Internet und Befehle ausführen", IsChecked = set.ClaudeAlwaysFull, Foreground = Text };
        ToolTip.SetTip(always, "Volle Claude-Code-Funktionen in allen Studios (auch deine Skills, Agenten, Plugins und MCP-Werkzeuge). Einzelne Projekte stellst du im Chat mit „Sicher“ ab.");
        var noClicks = new CheckBox { Name = "claude-no-clicks", Content = "Claude arbeitet selbständig ohne Klicks (aus: Plan und Vergleich zum Bestätigen)", IsChecked = set.ClaudeNoClicks, Foreground = Text };
        noClicks.IsCheckedChanged += (_, _) => { KiSourceSettings.Update(_core.DataRoot, x => x.ClaudeNoClicks = noClicks.IsChecked == true); _core.Audit.Append(Principal.LocalOwner.Name, "claude.ohne-klicks", noClicks.IsChecked == true ? "an" : "aus"); };
        always.IsCheckedChanged += (_, _) => { KiSourceSettings.Update(_core.DataRoot, x => x.ClaudeAlwaysFull = always.IsChecked == true); _core.Audit.Append(Principal.LocalOwner.Name, always.IsChecked == true ? "claude.immer.voll.an" : "claude.immer.voll.aus", ""); };
        var looks = new CheckBox { Name = "claude-looks", Content = "Claude startet sein Ergebnis echt und sieht es sich selbst an (Bildschirmfoto), bevor du es bekommst", IsChecked = set.ClaudeLooks, Foreground = Text };
        ToolTip.SetTip(looks, "Webseiten und Spiele: Trendo startet sie unsichtbar, meldet Fehler an Claude und zeigt Claude ein Bildschirmfoto – bis zu drei Durchgänge. Kostet etwas mehr von deinem Abo.");
        looks.IsCheckedChanged += (_, _) => KiSourceSettings.Update(_core.DataRoot, x => x.ClaudeLooks = looks.IsChecked == true);
        var suggests = new CheckBox { Name = "claude-suggests", Content = "Claude schlägt am Ende jeder Antwort nächste Schritte vor (anklickbar, auch in der Befehlsleiste Strg+K)", IsChecked = set.ClaudeSuggests, Foreground = Text };
        suggests.IsCheckedChanged += (_, _) => KiSourceSettings.Update(_core.DataRoot, x => x.ClaudeSuggests = suggests.IsChecked == true);
        var sounds = new CheckBox { Name = "claude-sounds", Content = "Dezente Töne, wenn Claude fertig ist, etwas fragt oder ein Fehler kam", IsChecked = set.ClaudeSounds, Foreground = Text };
        sounds.IsCheckedChanged += (_, _) => KiSourceSettings.Update(_core.DataRoot, x => x.ClaudeSounds = sounds.IsChecked == true);
        return new Border
        {
            Name = "claude-card", Background = Brush.Parse(s.Ready ? "#1A22B35E" : "#1AC9A227"), BorderBrush = Brush.Parse(s.Ready ? "#4ADE80" : "#D4A84B"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 10), Margin = new Thickness(0, 0, 0, 10),
            Child = new StackPanel { Spacing = 6, Children = {
                new TextBlock { Text = "Claude (dein Abo)", Foreground = Text, FontSize = 15, FontWeight = FontWeight.SemiBold },
                new TextBlock { Name = "claude-state", Text = s.Text, Foreground = s.Ready ? Brush.Parse("#4ADE80") : Text, FontSize = 13, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                P(s.Ready && !ClaudeEmbedApproved ? "Claude Code ist mit deinem Abo bereit. „Claude Code öffnen“ startet es sichtbar im Projektordner – dort arbeitest du direkt mit Claude, ohne API."
                  : s.Ready ? "Claude beantwortet Fragen, plant und baut in allen Programmen – wo programmiert wird, arbeitet es wie Claude Code. Volle Funktionen (Befehle, Web, deine Skills) gibst du je Projekt frei. Deine Dokumente aus „Wissen“ gehen nicht automatisch mit."
                  : "Einmal verbinden, dann ist Claude deine Haupt-KI in Trendo. Die Anmeldung läuft über Claude selbst – Trendo sieht nie dein Passwort. Ohne Claude arbeitet die KI auf diesem PC weiter.", Muted, 12),
                gate, main, always, noClicks, looks, suggests, sounds, buttons, ClaudeWebRow(),
                new TextBlock { Name = "claude-msg", Text = _claudeMsg, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap, IsVisible = _claudeMsg.Length > 0 } } },
        };
    }

    /// <summary>Zeile „Claude im Web“ auf der Claude-Karte: Stand der einmaligen Verbindung und Knopf dazu.</summary>
    Control ClaudeWebRow()
    {
        var w = CWeb;
        var state = new TextBlock { Name = "claude-web-connected", Text = w.Connected ? $"✓ Claude im Web verbunden (seit {w.ConnectedAt!.Value.LocalDateTime.ToString("dd.MM.yyyy", De)}) – in jedem Studio rechts unter „Claude“" : "Claude im Web: noch nicht verbunden",
            Foreground = w.Connected ? Brush.Parse("#4ADE80") : Text, FontSize = 12.5, TextWrapping = TextWrapping.Wrap };
        var row = new WrapPanel { Children = { state } };
        if (!w.Connected) { var b = Act("claude-web-connect", "Mit Claude verbinden", () => Show("claude-verbinden"), true); b.Margin = new Thickness(8, 0, 0, 0); row.Children.Add(b); }
        else
        {
            // Trennen meldet auch bei claude.ai ab – danach kann sich jemand anderes (oder ein anderes Konto) anmelden
            var b = Act("claude-web-disconnect", "Trennen und abmelden", () => { CWeb.Disconnect(); _claudeLogout = true; _core.Audit.Append(Principal.LocalOwner.Name, "claude.web.getrennt", ""); Show("claude-verbinden"); });
            b.Margin = new Thickness(8, 0, 0, 0); row.Children.Add(b);
        }
        return row;
    }

    /// <summary>Zuletzt in „Mit Claude verbinden“ aufgerufene Adresse (öffentlich für Tests).</summary>
    public string ClaudeConnectUrl { get; private set; } = "";
    bool _claudeLogout;

    /// <summary>Einmalig mit Claude verbinden: Anmeldeseite von claude.ai im Trendo-Profil; sobald die Anmeldung durch ist, gilt Claude in allen Studios als verbunden.</summary>
    Control ClaudeConnectView()
    {
        var sp = new StackPanel { Spacing = 8 };
        var w = CWeb;
        sp.Children.Add(new TextBlock { Name = "claude-connect-state", Text = w.Connected ? "✓ Verbunden. Claude ist jetzt in jedem Studio rechts unter „Claude Code“ → Reiter „Claude“ angemeldet – mit eigenem Chat je Studio." : "Melde dich unten einmal mit deinem Claude-Abo an (E-Mail, Google oder Apple). Trendo merkt sich die Anmeldung in einem eigenen, geschützten Profil – dein Passwort sieht Trendo nie.",
            Foreground = w.Connected ? Brush.Parse("#4ADE80") : Text, FontSize = 14, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        if (w.Connected) sp.Children.Add(Act("claude-connect-done", "Fertig", () => Show("ki-quellen"), true));
        Control view = _opt.Isolated ? new Border { Name = "claude-connect-web", Height = 200, Child = P("Testmodus: claude.ai wird nicht geladen.", Muted, 12) }
            : ClaudeWebView("claude-connect-web", u => { var was = CWeb.Connected; if (CWeb.Navigated(u, null) && !was && CWeb.Connected) { _core.Audit.Append(Principal.LocalOwner.Name, "claude.web.verbunden", ""); Show("claude-verbinden"); } });
        ClaudeConnectUrl = _claudeLogout ? ClaudeWeb.Logout : w.Connected ? ClaudeWeb.NewChat : ClaudeWeb.Login; _claudeLogout = false;
        if (view is NativeWebView nv) { nv.Height = 640; nv.Navigate(new Uri(ClaudeConnectUrl)); }
        if (!w.Connected) sp.Children.Add(ClaudeLoginLinkRow("claude-connect-link", u => { ClaudeConnectUrl = u.AbsoluteUri; if (view is NativeWebView v) v.Navigate(u); }));
        sp.Children.Add(new Border { CornerRadius = new CornerRadius(12), ClipToBounds = true, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Child = view });
        return KiPage("Mit Claude verbinden", sp);
    }
}

/// <summary>Claude als KI-Quelle für <see cref="IntelligenceCore"/> – Fragen laufen über Claude Code mit dem Abo des Nutzers (Frage-Modus: ändert keine Dateien, eingegrenzt).</summary>
public sealed class ClaudeKiBackend(MainWindow owner, Func<string> model) : IMainKiBackend, IStreamingKiBackend
{
    public string Name => "Claude (dein Abo)";
    public string BaseUrl => "claude-code";
    public async Task<bool> IsUpAsync(CancellationToken ct = default) => !owner.CLimit.Active(DateTimeOffset.Now) && (await owner.ClaudeCheck()).Ready; // nur über das Abo; Kontingent aufgebraucht → Ersatz-KI
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
