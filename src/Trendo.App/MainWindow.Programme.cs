using System.Net;
using System.Net.Http.Headers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Programme verwalten (Vorgabe 2026-10-26): zu jedem Programm eine ausführliche, leicht verständliche Beschreibung,
/// Starten und einzeln Entfernen; Programme von GitHub nach Zustimmung automatisch installieren.</summary>
public partial class MainWindow
{
    private readonly HashSet<string> _confirmRemove = [];
    /// <summary>Für Tests: Werkzeugordner (GitHub-Programme, KI, Vollpaket).</summary>
    public string ToolsRootForTests => _deps.ToolsRoot;
    /// <summary>Für Tests: ersetzt Release-Abfrage und Download (Repo → (Release, Datei)).</summary>
    public Func<string, Task<(GitHubApps.Release Release, string? File)>>? GitHubFetchOverride { get; set; }

    /// <summary>Beschreibung als aufklappbarer Bereich: Was ist das? Wofür? So funktioniert es. Das kann es. Herkunft. Entfernen.</summary>
    private Control GuideBox(ProgramGuide.Guide g, string name, bool open = false)
    {
        // Inhalt erst beim Aufklappen bauen (Zusatzprogramme hatte 21 zugeklappte Beschreibungen mit rund 350 Texten bei jedem Öffnen)
        var ex = new Expander { Name = name, Header = new TextBlock { Text = "Beschreibung & Funktionsweise", Foreground = Accent, FontSize = 11.5 }, HorizontalAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(0), IsExpanded = open, Content = open ? GuideBody(g) : null };
        ex.PropertyChanged += (_, e) => { if (e.Property == Expander.IsExpandedProperty && ex.IsExpanded && ex.Content is null) ex.Content = GuideBody(g); };
        return ex;
    }

    private Control GuideBody(ProgramGuide.Guide g)
    {
        var body = new StackPanel { Spacing = 6, Margin = new Thickness(4, 6, 4, 4) };
        void Sec(string head, Control c) { body.Children.Add(new TextBlock { Text = head, Foreground = Text, FontWeight = FontWeight.SemiBold, FontSize = 12 }); body.Children.Add(c); }
        Sec("Was ist das?", P(g.WhatIs, null, 11.5));
        Sec("Wofür nutze ich es?", P(g.UseFor, null, 11.5));
        var steps = new StackPanel { Spacing = 2 };
        for (var i = 0; i < g.HowItWorks.Count; i++) steps.Children.Add(P($"{i + 1}. {g.HowItWorks[i]}", null, 11.5));
        Sec("So funktioniert es", steps);
        var feats = new StackPanel { Spacing = 2 };
        foreach (var f in g.Features) feats.Children.Add(P("• " + f, null, 11.5));
        Sec("Das kann es alles", feats);
        Sec("Herkunft und Lizenz", P(g.Origin, null, 11));
        Sec("Entfernen", P(g.Remove, null, 11));
        return body;
    }

    /// <summary>Entfernen mit Rückfrage im Knopf auf jeder Seite (erster Klick fragt, zweiter entfernt).</summary>
    private Button ConfirmRemoveButton(string key, string name, string page, Func<Task> remove)
    {
        var armed = _confirmRemove.Contains(key);
        var b = new Button { Name = name, Content = armed ? "Wirklich entfernen?" : "Entfernen", IsEnabled = !_depBusy, Background = armed ? Brush.Parse("#B91C1C") : Brushes.Transparent, Padding = new Thickness(8, 2) };
        b.Click += async (_, _) =>
        {
            if (!_confirmRemove.Remove(key)) { _confirmRemove.Add(key); Show(page); return; }
            await remove(); Show(page);
        };
        return b;
    }

    /// <summary>Entfernen mit Rückfrage im Knopf: erster Klick „Wirklich entfernen?“, zweiter Klick entfernt.</summary>
    private Button RemoveButton(string key, string name, Action remove)
    {
        var armed = _confirmRemove.Contains(key);
        var b = new Button { Name = name, Content = armed ? "Wirklich entfernen?" : "Entfernen", IsEnabled = !_depBusy, Background = armed ? Brush.Parse("#B91C1C") : Brushes.Transparent };
        b.Click += (_, _) =>
        {
            var page = CurrentView is { Length: > 0 } cv ? cv : "components"; // auf der Seite bleiben, auf der der Knopf steht (Chat, Skills, Programme …)
            if (!_confirmRemove.Remove(key)) { _confirmRemove.Add(key); Show(page); return; }
            remove(); ReloadStudios(); Show(page);
        };
        return b;
    }

    /// <summary>Zeile für ein mitgeliefertes Programm: Beschreibung, Starten, Entfernen.</summary>
    private Control? IncludedExtras(Component c, bool installed)
    {
        if (!Vollpaket.Included.TryGetValue(c.Id, out var inc) || !ProgramGuide.Included.TryGetValue(inc.Package, out var g)) return null;
        var sp = new StackPanel { Spacing = 4 };
        sp.Children.Add(GuideBox(g, "guide-" + c.Id));
        if (installed && Directory.Exists(Path.Combine(_deps.ToolsRoot, inc.Package)))
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            var exe = Vollpaket.IncludedPath(_deps.ToolsRoot, c.Id);
            if (exe is not null && exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(exe) && c.Id is not ("ffmpeg" or "qpdf" or "tesseract" or "git" or "python" or "adb" or "ollama"))
            {
                var start = new Button { Name = "btn-start-" + c.Id, Content = "Starten" }; start.Click += (_, _) => Launch(exe); row.Children.Add(start);
            }
            row.Children.Add(RemoveButton("inc:" + inc.Package, "btn-remove-" + c.Id, () =>
            {
                try { _depLog.Text = Vollpaket.Uninstall(_deps.ToolsRoot, inc.Package, _core.Audit) ? $"{g.Name} wurde entfernt. Über das Vollpaket kannst du es jederzeit wieder einrichten." : $"{g.Name} war nicht eingerichtet."; }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _depLog.Text = $"{g.Name} läuft noch oder ist gesperrt: bitte schließen und erneut versuchen. ({ex.Message})"; }
            }));
            sp.Children.Add(row);
        }
        return sp;
    }

    /// <summary>Abschnitt „Programme von GitHub“ auf der Programmseite.</summary>
    private void AddGitHubPrograms(StackPanel sp)
    {
        sp.Children.Add(H("Programme von GitHub", 13));
        var list = GitHubApps.List(_deps.ToolsRoot);
        sp.Children.Add(P(list.Count == 0 ? "Noch keine. Unter „Open Source“ findest du freie Programme auf GitHub und installierst sie mit einem Klick." :
            $"{list.Count} installiert. Sie liegen im Trendo-Werkzeugordner und werden bei der Deinstallation von Trendo OS mit entfernt.", null, 11));
        foreach (var e in list)
        {
            var exe = GitHubApps.ExePath(_deps.ToolsRoot, e);
            var rd = _ossReadme.GetValueOrDefault(e.Repo);
            var guide = ProgramGuide.ForGitHub(e.Repo, e.Description, e.License, null, 0, e.Version, e.Asset, null, rd.Intro, rd.Features, _ossGerman.GetValueOrDefault(e.Repo));
            var start = new Button { Name = "btn-gh-start-" + e.Id, Content = "Starten", IsEnabled = File.Exists(exe) }; start.Click += (_, _) => Launch(exe);
            var upd = _ghUpdates.TryGetValue(e.Id, out var newTag) ? Act("btn-gh-update-" + e.Id, $"Auf {newTag} aktualisieren", () => _ = GitHubUpdate(e), true) : null;
            var remove = RemoveButton("gh:" + e.Id, "btn-gh-remove-" + e.Id, () =>
            {
                try { if (Path.IsPathRooted(e.Exe) && File.Exists(e.Exe)) GitHubRunUninstaller(e.Exe); _depLog.Text = GitHubApps.Uninstall(_deps.ToolsRoot, e.Id, _core.Audit) ? $"{e.Repo} wurde entfernt." : $"{e.Repo} war nicht installiert."; }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _depLog.Text = $"{e.Repo} läuft noch oder ist gesperrt: bitte schließen und erneut versuchen. ({ex.Message})"; }
            });
            sp.Children.Add(Card(new StackPanel { Spacing = 4, Children = {
                Row(new StackPanel { Children = { new TextBlock { Text = $"{e.Repo}  ·  {e.Version}", Foreground = Text, FontWeight = FontWeight.SemiBold }, P(e.Description, null, 11) } },
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center, Children = { upd ?? (Control)new Panel(), start, remove } }),
                GuideBox(guide, "guide-" + e.Id) } }));
        }
        if (list.Count > 0) sp.Children.Add(Act("btn-gh-updates", _ghUpdates.Count > 0 ? $"{_ghUpdates.Count} Update(s) verfügbar – erneut prüfen" : "Nach Updates suchen", () => _ = GitHubCheckUpdates()));
        if (list.Count > 0 && !_ghUpdateChecked && !_opt.Isolated && !_core.Offline) { _ghUpdateChecked = true; _ = GitHubCheckUpdates(); } // einmal je Sitzung von selbst
        var find = new Button { Name = "btn-gh-find", Content = "Programme auf GitHub finden" }; find.Click += (_, _) => Show("oss");
        sp.Children.Add(find);
    }

    /// <summary>Für Tests: Installer nicht wirklich ausführen (Datei, Argumente).</summary>
    public Func<string, string[], Task>? GitHubInstallerRunOverride { get; set; }
    /// <summary>Für Tests: Ordner, in denen Installer Programme ablegen (sonst %LOCALAPPDATA%\Programs und %LOCALAPPDATA%).</summary>
    public string[]? GitHubInstallRootsOverride { get; set; }
    static bool Arm64 => System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.Arm64;
    bool Has7z => true; // .7z entpackt Trendo selbst (Archive7z); 7-Zip wird nur genutzt, wenn es schon da ist

    /// <summary>Wunsch 27.10.2026: Auswählen = laden, einrichten und sofort starten. Neueste Veröffentlichung (notfalls Vorabversion) holen,
    /// passendes Windows-Programm wählen, prüfen, einrichten: ZIP/Programm direkt, Programmpaket (*.nsis.7z) und NSIS-Installer mit 7-Zip auspacken,
    /// andere Installer still nur für diesen Benutzer ausführen und das Programm danach erkennen. Nur HTTPS zu GitHub. Liefert den Programmpfad.</summary>
    public async Task<string?> GitHubInstall(OssRepo repo, bool start = true)
    {
        if (_core.Offline) { Say("Trendo ist offline. Das Installieren braucht Internet.", "oss"); return null; }
        if (_depBusy) return null;
        _depBusy = true;
        _core.Audit.Append(Principal.LocalOwner.Name, "github.install.start", $"{repo.FullName} {repo.LicenseId}");
        BeginSetupOverlay($"{repo.FullName.Split('/')[1]} wird eingerichtet");
        string? file = null; string? exe = null;
        try
        {
            GitHubApps.Release rel; GitHubApps.Asset? asset;
            // Suchen: Sekunden mitzählen, damit sichtbar ist, dass es weitergeht (vorher stand hier „0 %“ ohne Bewegung)
            var t0 = DateTime.UtcNow;
            SetupOverlayProgress(0, 0, "Neueste Version auf GitHub suchen …");
            var tick = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            tick.Tick += (_, _) => SetupOverlayProgress(0, 0, $"Neueste Version auf GitHub suchen … {(int)(DateTime.UtcNow - t0).TotalSeconds} s");
            tick.Start();
            try
            {
                if (GitHubFetchOverride is not null) { (rel, file) = await GitHubFetchOverride(repo.FullName); asset = GitHubApps.PickWindows(rel.Assets, Arm64, Has7z); }
                else
                {
                    rel = await GitHubLatestOrEmpty(repo.FullName, SetupToken);
                    asset = GitHubApps.PickWindows(rel.Assets, Arm64, Has7z);
                    if (asset is null)
                        foreach (var r in KiSourceSetups.ParseReleases(await GitHubJson(KiSourceSetups.ReleasesUrl(repo.FullName), SetupToken)))
                            if (GitHubApps.PickWindows(r.Assets, Arm64, Has7z) is { } a) { (rel, asset) = (r, a); break; }
                }
            }
            finally { tick.Stop(); }
            if (asset is null)
            {
                // vorher blieb die Anzeige hier für immer bei „Neueste Version suchen … 0 %“ stehen
                var none = $"„{repo.FullName}“ bietet kein fertiges Windows-Programm an – nur Quellcode. Mit „Quellcode holen“ landet es in deinem Ordner „Open Source“.";
                FinishSetupOverlay("Kein Windows-Programm", none, [none]); Say(none, "oss"); return null;
            }
            SetupOverlayFraction(0.10, $"{asset.Name} wird geladen …");
            file ??= await GitHubDownload(asset);
            SetupToken.ThrowIfCancellationRequested();
            SetupOverlayFraction(0.82, "Prüfen und einrichten …");
            try
            {
                var entry = await Task.Run(() => GitHubApps.Install(_deps.ToolsRoot, repo.FullName, rel, asset, file, repo.LicenseId, repo.Description, _core.Audit, Has7z ? Extract7z : null, Arm64));
                exe = GitHubApps.ExePath(_deps.ToolsRoot, entry);
            }
            catch (GitHubApps.InstallerOnlyException io)
            {
                SetupOverlayProgress(3, 4, $"Installer wird still für dich ausgeführt ({io.Kind}) …");
                exe = await GitHubRunInstaller(io, repo, rel, asset);
            }
            SetupOverlayProgress(4, 4, "Fertig ✓");
            if (start && exe is not null) { Launch(exe); _core.Audit.Append(Principal.LocalOwner.Name, "github.start", repo.FullName); }
            var msg = $"„{repo.FullName.Split('/')[1]}“ ist eingerichtet{(start ? " und startet gerade" : "")}. Du findest es unter „Programme“ und im Startmenü.";
            FinishSetupOverlay("Bereit", msg, []);
            Say(msg, "oss");
            return exe;
        }
        catch (OperationCanceledException) when (SetupCancelRequested) { Say($"Installation von „{repo.FullName}“ abgebrochen.", "oss"); return null; }
        catch (Exception ex) when (ex is not OutOfMemoryException) // nie mehr eine hängende Anzeige: jeder Fehler beendet sie mit einer Meldung
        {
            var why = GitHubWhy(ex);
            _core.Audit.Append(Principal.LocalOwner.Name, "github.install.failed", $"{repo.FullName}: {ex.GetType().Name}: {ex.Message}");
            FinishSetupOverlay("Installieren hat nicht geklappt", why, [why]);
            Say("Installieren hat nicht geklappt: " + why, "oss");
            return null;
        }
        finally
        {
            _depBusy = false;
            if (file is not null && GitHubFetchOverride is null) try { File.Delete(file); } catch (IOException) { }
        }
    }

    /// <summary>Installer still für den aktuellen Benutzer ausführen, dann das neue Programm unter %LOCALAPPDATA% finden und eintragen.</summary>
    async Task<string> GitHubRunInstaller(GitHubApps.InstallerOnlyException io, OssRepo repo, GitHubApps.Release rel, GitHubApps.Asset asset)
    {
        var roots = GitHubInstallRootsOverride ?? [Path.Combine(LocalAppDataDir, "Programs"), LocalAppDataDir];
        var before = roots.Where(Directory.Exists).SelectMany(Directory.GetDirectories).ToDictionary(d => d, Directory.GetLastWriteTimeUtc, StringComparer.OrdinalIgnoreCase);
        var started = DateTime.UtcNow.AddSeconds(-2);
        var uninstBefore = UninstallKeys();
        var args = GitHubApps.SilentArgs(io.Kind);
        if (GitHubInstallerRunOverride is { } run) await run(io.File, args);
        else
        {
            var psi = new System.Diagnostics.ProcessStartInfo(io.File) { UseShellExecute = false };
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = System.Diagnostics.Process.Start(psi) ?? throw new InvalidOperationException("Der Installer ließ sich nicht starten.");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(SetupToken); timeout.CancelAfter(TimeSpan.FromMinutes(15));
            // Sichtbar, dass es weitergeht: Sekunden mitzählen
            var t0 = DateTime.UtcNow;
            var tick = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            tick.Tick += (_, _) => SetupOverlayProgress(3, 4, $"Installer läuft still für dich ({io.Kind}) … {(int)(DateTime.UtcNow - t0).TotalSeconds} s");
            tick.Start();
            try { await p.WaitForExitAsync(timeout.Token); } finally { tick.Stop(); }
            if (io.Kind == GitHubApps.InstallerKind.Squirrel) await Task.Delay(3000, SetupToken); // Squirrel richtet nach dem Ende noch kurz ein
        }
        SetupOverlayProgress(3, 4, "Programm wird gesucht …");
        // 1. Was der Installer bei Windows eingetragen hat (genau und sofort), 2. sonst nur neu angelegte bzw. passend benannte Ordner
        var exe = (GitHubInstallRootsOverride is null ? NewUninstallExe(uninstBefore, repo.FullName) : null)
            ?? await Task.Run(() => GitHubApps.FreshDirs(roots.Where(Directory.Exists).SelectMany(Directory.GetDirectories), before, Directory.GetLastWriteTimeUtc, started, repo.FullName, asset.Name)
                .Select(d => GitHubApps.MainExe(d, repo.FullName)).FirstOrDefault(e => e is not null))
            ?? throw new InvalidOperationException("Der Installer ist durchgelaufen, aber Trendo findet das Programm nicht. Schau im Startmenü von Windows nach.");
        var sha = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(io.File)));
        GitHubApps.Register(_deps.ToolsRoot, repo.FullName, rel, asset, exe, sha, repo.LicenseId, repo.Description, _core.Audit);
        return exe;
    }

    /// <summary>Per Installer eingerichtet: dessen Deinstaller still ausführen (NSIS, Inno, Squirrel).</summary>
    void GitHubRunUninstaller(string exe)
    {
        var dir = Path.GetDirectoryName(exe)!;
        var (un, args) = Directory.EnumerateFiles(dir, "Uninstall*.exe").FirstOrDefault() is { } n ? (n, new[] { "/S" })
            : Directory.EnumerateFiles(dir, "unins*.exe").FirstOrDefault() is { } i ? (i, new[] { "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART" })
            : Path.Combine(Path.GetDirectoryName(dir) ?? dir, "Update.exe") is var u && File.Exists(u) ? (u, new[] { "--uninstall", "-s" }) : (null, Array.Empty<string>());
        if (un is null) return;
        var psi = new System.Diagnostics.ProcessStartInfo(un) { UseShellExecute = false }; foreach (var a in args) psi.ArgumentList.Add(a);
        System.Diagnostics.Process.Start(psi)?.WaitForExit(120_000);
        _core.Audit.Append(Principal.LocalOwner.Name, "github.uninstall.installer", Path.GetFileName(un));
    }

    static async Task<GitHubApps.Release> GitHubLatestOrEmpty(string fullName, CancellationToken ct, bool fresh = false)
    {
        try { return await GitHubLatest(fullName, ct, fresh); }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("Dieses Projekt hat noch keine", StringComparison.Ordinal)) { return new GitHubApps.Release("", "", []); } // keine „neueste“ Version: Liste prüfen
    }

    static readonly string[] GitHubHosts = ["github.com", "api.github.com", "objects.githubusercontent.com", "release-assets.githubusercontent.com", "github-releases.githubusercontent.com",
        .. SourceApps.RuntimeHosts, "pypi.org", "registry.npmjs.org"]; // T-0314: offizielle Laufzeiten, Quellcode-Zip, Paketverzeichnisse
    /// <summary>Für Tests: eigener Netz-Handler und kürzere Zeitlimits.</summary>
    public static HttpMessageHandler? GitHubHandlerOverride { get; set; }
    public static TimeSpan GitHubApiTimeout { get; set; } = TimeSpan.FromSeconds(20);
    public static TimeSpan GitHubStallTimeout { get; set; } = TimeSpan.FromSeconds(45);
    static HttpClient? _gitHubHttp; static HttpMessageHandler? _gitHubHttpFor;
    /// <summary>Verbindung: IPv4 zuerst (kaputtes IPv6 ließ Abfragen minutenlang hängen), 8 s je Adresse; Gesamtzeit regeln die Aufrufer.</summary>
    static HttpClient GitHubHttp
    {
        get
        {
            if (_gitHubHttp is not null && ReferenceEquals(_gitHubHttpFor, GitHubHandlerOverride)) return _gitHubHttp;
            _gitHubHttpFor = GitHubHandlerOverride;
            return _gitHubHttp = new HttpClient(GitHubHandlerOverride ?? new SocketsHttpHandler
            {
                AllowAutoRedirect = false, ConnectTimeout = TimeSpan.FromSeconds(15), PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                ConnectCallback = async (ctx, ct) =>
                {
                    var addrs = (await System.Net.Dns.GetHostAddressesAsync(ctx.DnsEndPoint.Host, ct)).OrderBy(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 0 : 1).ToList();
                    Exception? last = null;
                    foreach (var a in addrs)
                    {
                        var sock = new System.Net.Sockets.Socket(a.AddressFamily, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp) { NoDelay = true };
                        using var one = CancellationTokenSource.CreateLinkedTokenSource(ct); one.CancelAfter(TimeSpan.FromSeconds(8));
                        try { await sock.ConnectAsync(a, ctx.DnsEndPoint.Port, one.Token); return new System.Net.Sockets.NetworkStream(sock, ownsSocket: true); }
                        catch (Exception e) when (e is System.Net.Sockets.SocketException or OperationCanceledException && !ct.IsCancellationRequested) { sock.Dispose(); last = e; }
                    }
                    throw new HttpRequestException("Keine Verbindung zu " + ctx.DnsEndPoint.Host, last);
                },
            }, disposeHandler: GitHubHandlerOverride is null) { Timeout = Timeout.InfiniteTimeSpan };
        }
    }

    static void CheckHost(Uri u)
    {
        if (u.Scheme != Uri.UriSchemeHttps || u.Port != 443 || !GitHubHosts.Contains(u.Host, StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException("Unerwartete Download-Adresse: " + u.Host);
    }

    /// <summary>Verständliche Meldung statt Technik.</summary>
    static string GitHubWhy(Exception ex) => ex switch
    {
        TimeoutException t => t.Message,
        HttpRequestException h when h.InnerException is System.Net.Sockets.SocketException || h.Message.StartsWith("Keine Verbindung", StringComparison.Ordinal) => "Keine Verbindung zu GitHub. Ist das Internet da? Ein Virenschutz oder Proxy kann GitHub auch blockieren.",
        HttpRequestException h => "GitHub hat nicht richtig geantwortet (" + h.Message + "). Bitte gleich noch einmal versuchen.",
        KeyNotFoundException or System.Text.Json.JsonException => "GitHub hat eine unerwartete Antwort geliefert. Bitte gleich noch einmal versuchen.",
        _ => ex.Message,
    };

    /// <summary>GET mit eigenen Weiterleitungen (höchstens 5), jede nur zu GitHub über HTTPS. Bis die Antwort beginnt, gilt <paramref name="wait"/> (Abbrechen wirkt sofort).</summary>
    static async Task<HttpResponseMessage> GitHubGet(string url, string accept, CancellationToken ct = default, TimeSpan? wait = null, long rangeFrom = 0)
    {
        var u = new Uri(url);
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct); limit.CancelAfter(wait ?? GitHubApiTimeout);
        try
        {
            for (var i = 0; i < 6; i++)
            {
                CheckHost(u);
                var req = new HttpRequestMessage(HttpMethod.Get, u);
                req.Headers.UserAgent.Add(new ProductInfoHeaderValue("TrendoOS", "1.0")); req.Headers.Accept.ParseAdd(accept);
                if (GitHubToken is { Length: > 0 } tok && u.Host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase)) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tok); // nie an Download-Server
                if (rangeFrom > 0) req.Headers.Range = new RangeHeaderValue(rangeFrom, null);
                var res = await GitHubHttp.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, limit.Token);
                if ((int)res.StatusCode is >= 300 and < 400 && res.Headers.Location is { } loc) { u = loc.IsAbsoluteUri ? loc : new Uri(u, loc); res.Dispose(); continue; }
                if (res.StatusCode == HttpStatusCode.NotFound) { res.Dispose(); throw new InvalidOperationException("Dieses Projekt hat noch keine Veröffentlichung mit fertigen Programmen."); }
                if ((int)res.StatusCode is 403 or 429)
                {
                    var reset = res.Headers.TryGetValues("X-RateLimit-Reset", out var rv) && long.TryParse(rv.FirstOrDefault(), out var unix) ? DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime() : (DateTimeOffset?)null;
                    var left = res.Headers.TryGetValues("X-RateLimit-Remaining", out var rl) ? rl.FirstOrDefault() : null; res.Dispose();
                    throw new InvalidOperationException(left == "0" && reset is { } r
                        ? (GitHubToken is null ? $"GitHub erlaubt ohne Anmeldung 60 Abfragen pro Stunde – die sind aufgebraucht. Ab {r:HH:mm} Uhr geht es wieder (oder mit der freiwilligen GitHub-Anmeldung)." : $"Das GitHub-Limit ist aufgebraucht. Ab {r:HH:mm} Uhr geht es wieder.")
                        : "GitHub bremst gerade. Versuch es in einer Minute noch mal.");
                }
                res.EnsureSuccessStatusCode();
                return res;
            }
            throw new InvalidOperationException("Zu viele Weiterleitungen.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException($"GitHub antwortet nicht (nach {(int)(wait ?? GitHubApiTimeout).TotalSeconds} s). Prüfe die Internetverbindung und versuch es noch einmal.");
        }
    }

    /// <summary>JSON von der GitHub-API (mit Zeitlimit), ein zweiter Versuch bei Zeitüberschreitung.</summary>
    static async Task<string> GitHubJson(string url, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var res = await GitHubGet(url, "application/vnd.github+json", ct);
                using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct); limit.CancelAfter(GitHubApiTimeout);
                return await res.Content.ReadAsStringAsync(limit.Token);
            }
            catch (TimeoutException) when (attempt == 0) { }
        }
    }

    static readonly Dictionary<string, (DateTime At, GitHubApps.Release Rel)> _ghLatest = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Neueste Veröffentlichung (10 Minuten zwischengespeichert – schont das Limit von 60 Abfragen pro Stunde).</summary>
    static async Task<GitHubApps.Release> GitHubLatest(string fullName, CancellationToken ct = default, bool fresh = false)
    {
        lock (_ghLatest) if (!fresh && _ghLatest.TryGetValue(fullName, out var c) && DateTime.UtcNow - c.At < TimeSpan.FromMinutes(10)) return c.Rel;
        var rel = GitHubApps.ParseRelease(await GitHubJson(GitHubApps.LatestReleaseUrl(fullName), ct));
        lock (_ghLatest) _ghLatest[fullName] = (DateTime.UtcNow, rel);
        return rel;
    }

    /// <summary>Programme, die Windows unter „Apps“ für diesen Benutzer kennt (nur lesen).</summary>
    static HashSet<string> UninstallKeys()
    {
        if (!OperatingSystem.IsWindows()) return [];
        using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall");
        return k is null ? [] : [.. k.GetSubKeyNames()];
    }

    /// <summary>Neuer Eintrag des Installers bei Windows → Programm aus „DisplayIcon“ oder dem Installationsordner.</summary>
    static string? NewUninstallExe(HashSet<string> before, string repo)
    {
        if (!OperatingSystem.IsWindows()) return null;
        using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall");
        if (k is null) return null;
        foreach (var n in k.GetSubKeyNames().Where(n => !before.Contains(n)))
        {
            using var e = k.OpenSubKey(n);
            if (e?.GetValue("DisplayIcon") is string icon && icon.Split(',')[0].Trim('"') is var ico && ico.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(ico)
                && !Path.GetFileName(ico).StartsWith("unins", StringComparison.OrdinalIgnoreCase)) return ico;
            if (e?.GetValue("InstallLocation") is string dir && Directory.Exists(dir.Trim('"')) && GitHubApps.MainExe(dir.Trim('"'), repo) is { } exe) return exe;
        }
        return null;
    }

    async Task<string> GitHubDownload(GitHubApps.Asset asset, double lo = 0.10, double hi = 0.80)
    {
        // Fortsetzen: angefangene Datei je Download-Adresse bleibt bei Abbruch liegen und wird beim nächsten Versuch weitergeladen
        var partDir = Path.Combine(Path.GetTempPath(), "trendo-gh-teile"); Directory.CreateDirectory(partDir);
        foreach (var old in Directory.GetFiles(partDir).Where(f => File.GetLastWriteTimeUtc(f) < DateTime.UtcNow.AddDays(-7))) try { File.Delete(old); } catch (IOException) { }
        var part = Path.Combine(partDir, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(asset.Url)))[..20] + Path.GetExtension(asset.Name) + ".part");
        var have = File.Exists(part) ? new FileInfo(part).Length : 0;
        if (asset.Size > 0 && have >= asset.Size) { File.Delete(part); have = 0; }
        using var res = await GitHubGet(asset.Url, "application/octet-stream", SetupToken, TimeSpan.FromSeconds(45), have);
        var resumed = have > 0 && res.StatusCode == HttpStatusCode.PartialContent;
        if (!resumed) have = 0;
        var total = resumed ? have + (res.Content.Headers.ContentLength ?? asset.Size - have) : res.Content.Headers.ContentLength ?? asset.Size;
        if (total > GitHubApps.MaxBytes) throw new InvalidOperationException("Die Datei ist zu groß.");
        if (resumed) _ui.Post(() => SetupOverlayFraction(lo + (hi - lo) * have / Math.Max(1.0, total), $"Wird fortgesetzt ab {have / (1 << 20)} MB …"));
        await using var src = await res.Content.ReadAsStreamAsync(SetupToken);
        var buf = new byte[1 << 20]; long done = have; int n; var last = DateTime.MinValue; var t0 = DateTime.UtcNow;
        await using (var dst = new FileStream(part, resumed ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None))
        {
        while (true)
        {
            // Stockt der Download (keine Daten mehr), gibt es eine klare Meldung statt endlosen Wartens
            using var stall = CancellationTokenSource.CreateLinkedTokenSource(SetupToken); stall.CancelAfter(GitHubStallTimeout);
            try { n = await src.ReadAsync(buf, stall.Token); }
            catch (OperationCanceledException) when (!SetupToken.IsCancellationRequested) { throw new TimeoutException($"Der Download stockt seit {(int)GitHubStallTimeout.TotalSeconds} s. Prüfe die Internetverbindung und versuch es noch einmal."); }
            if (n <= 0) break;
            done += n;
            if (done > GitHubApps.MaxBytes) throw new InvalidOperationException("Die Datei ist größer als angegeben.");
            await dst.WriteAsync(buf.AsMemory(0, n), SetupToken);
            if ((DateTime.UtcNow - last).TotalMilliseconds > 250)
            {
                last = DateTime.UtcNow; var d = done;
                var speed = (d - have) / Math.Max(0.5, (DateTime.UtcNow - t0).TotalSeconds) / (1 << 20);
                var frac = total > 0 ? Math.Min(1, d / (double)total) : 0;
                var eta = total > 0 && speed > 0.01 ? (total - d) / (1 << 20) / speed : -1;
                _ui.Post(() => SetupOverlayFraction(lo + (hi - lo) * frac,
                    total > 0 ? $"{asset.Name}: {d / (1 << 20)} von {total / (1 << 20)} MB · {speed:0.0} MB/s{(eta >= 0 ? $" · noch etwa {Math.Ceiling(eta)} s" : "")}" : $"{asset.Name}: {d / (1 << 20)} MB · {speed:0.0} MB/s"));
            }
        }
        }
        if (total > 0 && done < total) throw new IOException($"Der Download ist unvollständig ({done / (1 << 20)} von {total / (1 << 20)} MB). Beim nächsten Versuch geht es an dieser Stelle weiter.");
        var file = Path.Combine(Path.GetTempPath(), "trendo-gh-" + Guid.NewGuid().ToString("N") + Path.GetExtension(asset.Name));
        File.Move(part, file);
        return file;
    }
}
