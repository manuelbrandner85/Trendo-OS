using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Sys;

namespace Trendo.App;

/// <summary>T-0314: GitHub-Projekte ohne Windows-Programm aus dem Quellcode einrichten – Erkennung im Treffer, Zustimmung, offizielle Laufzeit
/// (uv, Node.js, Go, .NET SDK) portabel im Werkzeugordner, Einrichtung mit sichtbarer Ausgabe, Start (Fenster, Browser oder lokale Web-Seite),
/// Alternativen, wenn es nicht geht (Fork mit Windows-Programm, fertiges Paket auf PyPI/npm).</summary>
public partial class MainWindow
{
    readonly Dictionary<string, SourceApps.Detection> _srcDetect = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, (IReadOnlyList<string> Files, string Sha)> _srcTree = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, List<(string Name, string Text, Func<Task> Run)>> _srcAlt = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> _srcAltBusy = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, Process> _srcRunning = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, WebsiteHost> _srcSites = new(StringComparer.OrdinalIgnoreCase);
    string? _srcConfirm;
    public IReadOnlyDictionary<string, SourceApps.Detection> SourceDetections => _srcDetect;
    public IReadOnlyDictionary<string, List<(string Name, string Text, Func<Task> Run)>> SourceAlternatives => _srcAlt;
    public IReadOnlyCollection<string> SourceRunning => _srcRunning.Keys.Concat(_srcSites.Keys).ToList();

    /// <summary>Für Tests: Befehle nicht wirklich ausführen (Programm, Argumente, Ordner, Umgebung, Zeilen) → Exitcode.</summary>
    public static Func<string, string, string, IReadOnlyDictionary<string, string>, Action<string>, CancellationToken, Task<int>>? SourceRunOverride { get; set; }
    /// <summary>Für Tests: Web-Programm „läuft“, ohne einen Prozess zu starten (liefert die Adresse).</summary>
    public static Func<SourceApps.Installed, string, Task<bool>>? SourceWebStartOverride { get; set; }
    public static TimeSpan SourceStepTimeout { get; set; } = TimeSpan.FromMinutes(25);

    async Task<string> GitHubText(string url, CancellationToken ct)
    {
        using var res = await GitHubGet(url, "*/*", ct);
        if (!res.IsSuccessStatusCode) throw new HttpRequestException($"HTTP {(int)res.StatusCode}");
        return await res.Content.ReadAsStringAsync(ct);
    }

    /// <summary>Erkennung über den Dateibaum (eine GitHub-Abfrage) und einzelne Dateien (raw.githubusercontent.com, ohne Abfragelimit).</summary>
    public async Task<SourceApps.Detection?> SourceDetect(string repo)
    {
        if (_srcDetect.TryGetValue(repo, out var have)) return have;
        try
        {
            var (files, sha, _) = SourceApps.ParseTree(await GitHubJson(SourceApps.TreeUrl(repo), CancellationToken.None));
            _srcTree[repo] = (files, sha);
            var cache = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var wanted = files.Where(f => (!f.Contains('/') && (f.EndsWith(".toml") || f.EndsWith(".txt") || f.EndsWith(".json") || f.EndsWith(".mod") || f.EndsWith(".py") || f.EndsWith(".go") || f.EndsWith("proj")))
                || (f.EndsWith("proj", StringComparison.OrdinalIgnoreCase) && f.Count(c => c == '/') <= 3) || (f.StartsWith("cmd/") && f.EndsWith(".go"))).Take(24).ToList();
            await Task.WhenAll(wanted.Select(async f => { try { var t = await GitHubText(SourceApps.RawUrl(repo, f), CancellationToken.None); lock (cache) cache[f] = t; } catch (Exception e) when (e is HttpRequestException or InvalidOperationException or TaskCanceledException or TimeoutException) { lock (cache) cache[f] = null; } }));
            var d = SourceApps.Detect(repo, files, f => cache.GetValueOrDefault(f));
            _srcDetect[repo] = d;
            if (CurrentView == "oss") Show("oss");
            return d;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { if (ex.Message.Contains("60 Abfragen")) _w45Msg = ex.Message; return null; }
    }

    Control SourceBadge(OssRepo r, int i)
    {
        if (!_srcDetect.TryGetValue(r.FullName, out var d)) return new Panel();
        var (col, bg) = d.Runnable ? ("#60A5FA", "#1AC9A227") : ("#FDBA74", "#1AF97316");
        var sp = new StackPanel { Spacing = 3, Children = { new Border { Name = $"oss-src-kind-{i}", Background = Brush.Parse(bg), CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 2), HorizontalAlignment = HorizontalAlignment.Left,
            Child = new TextBlock { Text = d.Text, Foreground = Brush.Parse(col), FontSize = 11.5, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap } } } };
        if (!d.Runnable && d.Why is { } why) sp.Children.Add(P(why, Muted, 11));
        if (_srcConfirm == r.FullName && d.Runnable)
        {
            var rt = d.Runtime is { } id ? SourceApps.RuntimeFor(id, Arm64) : null;
            var have = rt is not null && SourceApps.RuntimeExe(_deps.ToolsRoot, rt) is not null;
            sp.Children.Add(new Border { Name = $"oss-src-ask-{i}", Background = Brush.Parse("#1AF5A524"), CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 8), Child = new StackPanel { Spacing = 6, Children = {
                P($"Trendo lädt den Quellcode von „{r.FullName}“{(rt is null ? "" : have ? $" und nutzt das schon eingerichtete {rt.Name}" : $" und das offizielle Grundprogramm {rt.Name} {rt.Version} ({(rt.Size > 0 ? $"{rt.Size / (1 << 20)} MB" : "Größe laut Hersteller")}, Prüfsumme fest hinterlegt)")}. Beim Einrichten lädt das Projekt seine Bausteine nach. " +
                  "Der Code läuft dann auf deinem PC wie ein Programm – mach das nur bei Projekten, denen du vertraust. Alles bleibt im Trendo-Werkzeugordner, ohne Admin-Rechte.", Text, 12),
                new WrapPanel { Children = { Act($"oss-src-confirm-{i}", "Ja, einrichten & starten", () => { _srcConfirm = null; _ = SourceInstall(r); }, true), new Border { Width = 6 },
                    Act($"oss-src-cancel-{i}", "Abbrechen", () => { _srcConfirm = null; Show("oss"); }) } } } } });
        }
        if (_srcAlt.TryGetValue(r.FullName, out var alts))
        {
            if (alts.Count == 0) sp.Children.Add(P("Keine Alternative gefunden – weder ein Fork mit Windows-Programm noch ein fertiges Paket. Mit „Quellcode holen“ bekommst du den Code zum Selbstbauen.", Muted, 11));
            var k = 0;
            foreach (var a in alts) { var run = a.Run; sp.Children.Add(new WrapPanel { Children = { P("• " + a.Text, Text, 11.5), Act($"oss-alt-use-{i}-{k++}", a.Name, () => _ = run()) } }); }
        }
        else if (_srcAltBusy.Contains(r.FullName)) sp.Children.Add(P("Suche Alternativen …", Muted, 11));
        return sp;
    }

    /// <summary>Knöpfe im Treffer: einrichten (wenn startbar) oder Alternativen suchen.</summary>
    void SourceButtons(OssRepo r, int i, OpenSource.LicenseKind kind, Action<Control> add)
    {
        if (!_ossAvail.TryGetValue(r.FullName, out var av) || av.Kind == "windows") return;
        if (!_srcDetect.TryGetValue(r.FullName, out var d)) { add(Act($"oss-src-detect-{i}", "Läuft es trotzdem? prüfen", () => _ = SourceDetect(r.FullName))); return; }
        if (SourceApps.List(_deps.ToolsRoot).FirstOrDefault(x => x.Repo.Equals(r.FullName, StringComparison.OrdinalIgnoreCase)) is { } inst)
        { add(Act($"oss-src-start-{i}", "Starten", () => _ = SourceStart(inst), true)); return; }
        if (d.Runnable)
        {
            var b = Act($"oss-src-install-{i}", "Aus Quellcode einrichten & starten", () => { _srcConfirm = r.FullName; Show("oss"); }, true);
            b.IsEnabled = kind != OpenSource.LicenseKind.Unklar && !_depBusy;
            if (kind == OpenSource.LicenseKind.Unklar) ToolTip.SetTip(b, "Die Lizenz ist unklar – Trendo richtet es deshalb nicht ein.");
            add(b);
        }
        else add(Act($"oss-alt-{i}", "Alternativen suchen", () => _ = SourceFindAlternatives(r)));
    }

    /// <summary>Alternativen: Forks mit fertigem Windows-Programm (bis zu 4 geprüft), fertiges Paket auf PyPI oder npm.</summary>
    public async Task SourceFindAlternatives(OssRepo r)
    {
        if (!_srcAltBusy.Add(r.FullName)) return;
        if (CurrentView == "oss") Show("oss");
        var list = new List<(string, string, Func<Task>)>();
        try
        {
            var d = _srcDetect.GetValueOrDefault(r.FullName);
            try
            {
                var forks = SourceApps.ParseForks(await GitHubJson(SourceApps.ForksUrl(r.FullName), CancellationToken.None)).Where(f => f.Stars > 0 || f.Updated > DateTimeOffset.Now.AddYears(-2)).Take(4).ToList();
                foreach (var f in forks)
                {
                    var rel = await GitHubLatestOrEmpty(f.FullName, CancellationToken.None);
                    var a = GitHubApps.Availability(rel, Arm64, Has7z); _ossAvail[f.FullName] = a;
                    if (a.Kind == "windows") { var fork = f with { LicenseId = f.LicenseId.Length > 0 ? f.LicenseId : r.LicenseId }; list.Add(("Diesen Fork installieren", $"Fork {f.FullName} (★ {f.Stars}): {a.Text}", () => GitHubInstall(fork))); }
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException) { if (ex.Message.Contains("Abfragen")) _w45Msg = ex.Message; }
            var name = d?.Package ?? r.FullName.Split('/')[1];
            if (d?.Kind is SourceApps.Kind.Python or SourceApps.Kind.Bibliothek or SourceApps.Kind.Rust or SourceApps.Kind.Cpp or SourceApps.Kind.Unbekannt)
                try
                {
                    if (SourceApps.ParsePyPi(await GitHubText(SourceApps.PyPiUrl(name), CancellationToken.None)) is { } py && py.Name.Length > 0)
                        list.Add(("Als Python-Paket starten", $"Fertig gebautes Paket „{py.Name}“ {py.Version} auf PyPI (pypi.org)", () => SourceInstallPackage(r, "pypi", py.Name, null)));
                }
                catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException or TimeoutException) { }
            if (d?.Kind is SourceApps.Kind.Node or SourceApps.Kind.Bibliothek or SourceApps.Kind.Unbekannt)
                try
                {
                    if (SourceApps.ParseNpm(await GitHubText(SourceApps.NpmUrl(name), CancellationToken.None)) is { } np)
                        list.Add(("Als npm-Paket starten", $"Fertiges Paket „{np.Name}“ {np.Version} auf npm mit dem Befehl „{np.Command}“", () => SourceInstallPackage(r, "npm", np.Name, np.Command)));
                }
                catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException or TimeoutException) { }
        }
        finally { _srcAlt[r.FullName] = list; _srcAltBusy.Remove(r.FullName); if (CurrentView == "oss") Show("oss"); }
    }

    /// <summary>Laufzeit holen, falls noch nicht da: Download (mit Fortsetzen), Prüfsumme des Herstellers, entpacken in „laufzeiten/&lt;id&gt;-&lt;version&gt;“.</summary>
    async Task<string> SourceEnsureRuntime(string id, double lo, double hi)
    {
        var rt = SourceApps.RuntimeFor(id, Arm64);
        if (SourceApps.RuntimeExe(_deps.ToolsRoot, rt) is { } exe) return exe;
        SetupOverlayFraction(lo, $"{rt.Name} {rt.Version} wird geladen (offizielle Quelle) …");
        var file = await GitHubDownload(new GitHubApps.Asset(Path.GetFileName(new Uri(rt.Url).AbsolutePath), rt.Url, rt.Size, null), lo, hi);
        try
        {
            SetupOverlayFraction(hi, $"{rt.Name}: Prüfsumme prüfen und entpacken …");
            await Task.Run(() =>
            {
                if (!SourceApps.HashOk(file, rt.Hash)) throw new InvalidOperationException($"Die Prüfsumme von {rt.Name} stimmt nicht – nichts eingerichtet.");
                var dir = SourceApps.RuntimeDir(_deps.ToolsRoot, rt); var tmp = dir + ".neu";
                if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
                Archives.Unpack(file, tmp, ArchiveConflict.Overwrite);
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
                Directory.Move(tmp, dir);
            });
            _core.Audit.Append(Principal.LocalOwner.Name, "source.runtime", $"{rt.Id} {rt.Version} {rt.Hash[..Math.Min(20, rt.Hash.Length)]}");
        }
        finally { try { File.Delete(file); } catch (IOException) { } }
        return SourceApps.RuntimeExe(_deps.ToolsRoot, rt) ?? throw new InvalidOperationException($"{rt.Name} ist entpackt, aber {rt.Exe} fehlt.");
    }

    /// <summary>Werte für Platzhalter und Umgebung eines Projekts.</summary>
    (Dictionary<string, string> Values, Dictionary<string, string> Env) SourceContext(string? runtimeExe, IReadOnlyDictionary<string, string> planEnv, string port)
    {
        var values = new Dictionary<string, string> { ["port"] = port, ["rid"] = Arm64 ? "win-arm64" : "win-x64" };
        var env = SourceApps.RuntimeEnv(_deps.ToolsRoot);
        if (runtimeExe is not null)
        {
            var dir = Path.GetDirectoryName(runtimeExe)!;
            values["uv"] = values["node"] = values["go"] = values["dotnet"] = runtimeExe;
            values["npm"] = "\"" + Path.Combine(dir, "node_modules", "npm", "bin", "npm-cli.js") + "\"";
            env["PATH"] = dir + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
            if (Path.GetFileName(runtimeExe).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase)) env["DOTNET_ROOT"] = dir;
        }
        foreach (var (k, v) in planEnv) env[k] = SourceApps.Fill(v, values);
        return (values, env);
    }

    async Task<int> SourceRun(string exe, string args, string dir, IReadOnlyDictionary<string, string> env, Action<string> line, CancellationToken ct)
    {
        if (SourceRunOverride is { } o) return await o(exe, args, dir, env, line, ct);
        if (_opt.Isolated) throw new InvalidOperationException("Testmodus: Programme aus dem Quellcode werden nicht wirklich eingerichtet.");
        var psi = new ProcessStartInfo(exe, args) { WorkingDirectory = dir, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8, StandardErrorEncoding = System.Text.Encoding.UTF8 };
        foreach (var (k, v) in env) psi.Environment[k] = v;
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Konnte nicht starten: " + Path.GetFileName(exe));
        p.OutputDataReceived += (_, e) => { if (e.Data is { Length: > 0 } s) line(s); };
        p.ErrorDataReceived += (_, e) => { if (e.Data is { Length: > 0 } s) line(s); };
        p.BeginOutputReadLine(); p.BeginErrorReadLine();
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct); limit.CancelAfter(SourceStepTimeout);
        try { await p.WaitForExitAsync(limit.Token); }
        catch (OperationCanceledException) { try { p.Kill(true); } catch (InvalidOperationException) { } if (ct.IsCancellationRequested) throw; throw new TimeoutException($"Der Schritt dauert länger als {(int)SourceStepTimeout.TotalMinutes} Minuten – abgebrochen."); }
        return p.ExitCode;
    }

    /// <summary>Quellcode laden, Laufzeit holen, einrichten, eintragen, starten. Liefert den Eintrag oder null.</summary>
    public Task<SourceApps.Installed?> SourceInstall(OssRepo repo, bool start = true) => SourceSetup(repo, null, start);

    /// <summary>Alternative: fertiges Paket aus PyPI oder npm statt des Quellcodes.</summary>
    public async Task SourceInstallPackage(OssRepo repo, string registry, string package, string? command) => await SourceSetup(repo, (registry, package, command), true);

    async Task<SourceApps.Installed?> SourceSetup(OssRepo repo, (string Registry, string Package, string? Command)? pkg, bool start)
    {
        if (_core.Offline) { Say("Trendo ist offline. Das Einrichten braucht Internet.", "oss"); return null; }
        if (_depBusy) return null;
        _depBusy = true;
        var name = repo.FullName.Split('/')[1];
        var id = SourceApps.IdFor(repo.FullName); var dir = SourceApps.Dir(_deps.ToolsRoot, id); var old = dir + ".alt";
        _core.Audit.Append(Principal.LocalOwner.Name, "source.install.start", $"{repo.FullName} {repo.LicenseId}{(pkg is { } pp ? $" {pp.Registry}:{pp.Package}" : "")}");
        BeginSetupOverlay($"{name} wird eingerichtet");
        string? zip = null; var log = new List<string>();
        try
        {
            if (Directory.Exists(old)) Directory.Delete(old, true);
            if (Directory.Exists(dir)) Directory.Move(dir, old); // alter Stand bleibt, bis der neue fertig ist
            Directory.CreateDirectory(dir);
            SourceApps.Plan plan; var sha = "";
            if (pkg is { } p) { plan = SourceApps.PackagePlan(p.Registry, p.Package, p.Command); sha = p.Registry + ":" + p.Package; }
            else
            {
                SetupOverlayFraction(0.03, "Quellcode von GitHub laden …");
                zip = await GitHubDownload(new GitHubApps.Asset(name + ".zip", SourceApps.ZipUrl(repo.FullName), 0, null), 0.03, 0.30);
                SetupOverlayFraction(0.30, "Quellcode entpacken …");
                await Task.Run(() =>
                {
                    var tmp = dir + ".zip-inhalt"; if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
                    Archives.Unpack(zip, tmp, ArchiveConflict.Overwrite);
                    var top = Directory.GetDirectories(tmp) is [var only] && Directory.GetFiles(tmp).Length == 0 ? only : tmp; // GitHub packt alles in „name-stand/“
                    foreach (var f in Directory.GetFileSystemEntries(top)) { var t = Path.Combine(dir, Path.GetFileName(f)); if (Directory.Exists(f)) Directory.Move(f, t); else File.Move(f, t); }
                    Directory.Delete(tmp, true);
                });
                plan = await Task.Run(() => SourceApps.MakePlan(repo.FullName, dir));
                sha = _srcTree.TryGetValue(repo.FullName, out var tr) ? tr.Sha : "";
            }
            var rtExe = plan.Runtime is { } rid ? await SourceEnsureRuntime(rid, 0.32, 0.62) : null;
            var (values, env) = SourceContext(rtExe, plan.Env, "0");
            for (var s = 0; s < plan.Setup.Count; s++)
            {
                var step = plan.Setup[s]; var frac = 0.64 + 0.30 * s / Math.Max(1, plan.Setup.Count);
                SetupOverlayFraction(frac, step.Text + " …");
                var exe = SourceApps.Fill(step.Exe, values); var args = SourceApps.Fill(step.Args, values);
                var last = DateTime.MinValue; var live = true; // späte Ausgabezeilen dürfen die Meldung nach dem Schritt nicht überschreiben
                var code = await SourceRun(exe, args, dir, env, l =>
                {
                    lock (log) { log.Add(l); if (log.Count > 400) log.RemoveAt(0); }
                    if ((DateTime.UtcNow - last).TotalMilliseconds < 300) return; last = DateTime.UtcNow;
                    var t = l.Length > 110 ? l[..110] + "…" : l;
                    _ui.Post(() => { if (live) SetupOverlayFraction(frac, $"{step.Text}: {t}"); });
                }, SetupToken);
                live = false;
                SetupToken.ThrowIfCancellationRequested();
                if (code != 0)
                {
                    string tail; lock (log) tail = string.Join("\n", log.TakeLast(6));
                    throw new InvalidOperationException($"„{step.Text}“ ist fehlgeschlagen (Code {code}).{(tail.Length > 0 ? "\nLetzte Ausgabe:\n" + tail : "")}");
                }
            }
            if (plan.Start is { } st && !st.Exe.StartsWith('{') && !File.Exists(Path.Combine(dir, SourceApps.Fill(st.Exe, values))) && SourceRunOverride is null)
                throw new InvalidOperationException($"Eingerichtet, aber die Startdatei „{st.Exe}“ fehlt. Das Projekt braucht wohl eine andere Startweise – „Quellcode holen“ und die Anleitung (README) helfen weiter.");
            var entry = SourceApps.Register(_deps.ToolsRoot, repo.FullName, sha, plan, repo.LicenseId, repo.Description, _core.Audit);
            if (Directory.Exists(old)) try { Directory.Delete(old, true); } catch (IOException) { }
            SetupOverlayProgress(4, 4, "Fertig ✓");
            var msg = $"„{name}“ ist aus dem Quellcode eingerichtet{(start ? " und startet gerade" : "")}. Du findest es unter „Programme“ › „Aus Quellcode eingerichtet“.";
            FinishSetupOverlay("Bereit", msg, []); Say(msg, "oss");
            if (start) await SourceStart(entry);
            return entry;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            var cancelled = ex is OperationCanceledException && SetupCancelRequested;
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); if (Directory.Exists(old)) Directory.Move(old, dir); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            var why = cancelled ? "Abgebrochen – nichts geändert." : ex is InvalidOperationException ? ex.Message : GitHubWhy(ex);
            _core.Audit.Append(Principal.LocalOwner.Name, cancelled ? "source.install.cancelled" : "source.install.failed", $"{repo.FullName}: {ex.GetType().Name}: {ex.Message.Split('\n')[0]}");
            FinishSetupOverlay(cancelled ? "Abgebrochen" : "Einrichten hat nicht geklappt", why, cancelled ? [] : [why]);
            Say((cancelled ? "" : "Einrichten hat nicht geklappt: ") + why.Split('\n')[0], "oss");
            return null;
        }
        finally
        {
            _depBusy = false;
            if (zip is not null) try { File.Delete(zip); } catch (IOException) { }
            if (CurrentView is "oss" or "components") Show(CurrentView);
        }
    }

    /// <summary>Starten: Web-Seite über Trendos lokalen Server, Web-Programm unsichtbar + Browser, sonst in einem Fenster mit Ausgabe (bleibt nach dem Ende offen).</summary>
    public async Task SourceStart(SourceApps.Installed e)
    {
        var dir = SourceApps.Dir(_deps.ToolsRoot, e.Id);
        try
        {
            if (e.StaticRoot is { } sr)
            {
                if (!_srcSites.TryGetValue(e.Id, out var host)) _srcSites[e.Id] = host = WebsiteHost.Start(sr.Length == 0 ? dir : Path.Combine(dir, sr));
                Launch(host.Origin + "/"); _core.Audit.Append(Principal.LocalOwner.Name, "source.start", $"{e.Repo} web {host.Origin}");
                Say($"„{e.Repo.Split('/')[1]}“ läuft im Browser unter {host.Origin}.", CurrentView); return;
            }
            if (e.Start is not { } st) throw new InvalidOperationException("Kein Startbefehl hinterlegt.");
            var rt = e.Runtime is { } id ? SourceApps.RuntimeExe(_deps.ToolsRoot, SourceApps.RuntimeFor(id, Arm64)) : null;
            if (e.Runtime is not null && rt is null) throw new InvalidOperationException("Das Grundprogramm fehlt – bitte „Neu einrichten“.");
            var port = e.Web ? WebsitePreview.FreePort().ToString() : "0";
            var (values, env) = SourceContext(rt, e.Env, port);
            var exe = SourceApps.Fill(st.Exe, values); if (!Path.IsPathRooted(exe)) exe = Path.Combine(dir, exe.Replace('/', Path.DirectorySeparatorChar));
            var args = SourceApps.Fill(st.Args, values);
            if (e.Web)
            {
                var url = $"http://127.0.0.1:{port}/";
                if (SourceWebStartOverride is { } wo) { if (await wo(e, url)) Launch(url); return; }
                if (_srcRunning.TryGetValue(e.Id, out var running) && !running.HasExited) { Say($"„{e.Repo.Split('/')[1]}“ läuft schon.", CurrentView); return; }
                if (_opt.Isolated) { Say("Testmodus: Web-Programme werden nicht wirklich gestartet.", CurrentView); return; }
                var psi = new ProcessStartInfo(exe, args) { WorkingDirectory = dir, UseShellExecute = false, CreateNoWindow = true };
                foreach (var (k, v) in env) psi.Environment[k] = v;
                var p = Process.Start(psi)!; _srcRunning[e.Id] = p;
                _core.Audit.Append(Principal.LocalOwner.Name, "source.start", $"{e.Repo} web pid={p.Id}");
                Say($"„{e.Repo.Split('/')[1]}“ startet – die Seite öffnet sich gleich im Browser …", CurrentView);
                for (var i = 0; i < 360 && !p.HasExited; i++)
                {
                    try { using var c = new System.Net.Sockets.TcpClient(); await c.ConnectAsync("127.0.0.1", int.Parse(port)); Launch(url); Say($"„{e.Repo.Split('/')[1]}“ läuft unter {url} (Beenden unter „Programme“).", CurrentView); if (CurrentView == "components") Show("components"); return; }
                    catch (System.Net.Sockets.SocketException) { await Task.Delay(500); }
                }
                Say(p.HasExited ? $"„{e.Repo.Split('/')[1]}“ hat sich gleich wieder beendet (Code {p.ExitCode})." : $"„{e.Repo.Split('/')[1]}“ antwortet noch nicht – es läuft weiter, die Adresse ist {url}.", CurrentView);
                return;
            }
            // Fenster mit Ausgabe: kleine Startdatei im Projektordner (Umgebung, Ordner, Programm, danach „Taste drücken“)
            var cmd = Path.Combine(dir, "trendo-start.cmd");
            var lines = new List<string> { "@echo off", "chcp 65001 >nul", $"cd /d \"{dir}\"" };
            foreach (var (k, v) in env) lines.Add($"set \"{k}={v.Replace("\"", "")}\"");
            lines.Add($"\"{exe}\" {args}"); lines.Add("echo."); lines.Add("echo (Programm beendet - eine Taste schliesst das Fenster)"); lines.Add("pause >nul");
            await File.WriteAllLinesAsync(cmd, lines, new System.Text.UTF8Encoding(false));
            _core.Integrations.LaunchArgs(Principal.LocalOwner, Path.Combine(Environment.SystemDirectory, "cmd.exe"), _core.Audit, ["/c", cmd]);
            _core.Audit.Append(Principal.LocalOwner.Name, "source.start", e.Repo);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or System.ComponentModel.Win32Exception or UnauthorizedAccessException)
        { Say($"Starten hat nicht geklappt: {ex.Message}", CurrentView); }
    }

    public void SourceStop(string id)
    {
        if (_srcRunning.Remove(id, out var p)) { try { if (!p.HasExited) p.Kill(true); } catch (InvalidOperationException) { } p.Dispose(); }
        if (_srcSites.Remove(id, out var h)) h.Dispose();
        _core.Audit.Append(Principal.LocalOwner.Name, "source.stop", id);
        if (CurrentView == "components") Show("components");
    }

    /// <summary>Abschnitt „Aus Quellcode eingerichtet“ auf der Programmseite – Starten, Beenden, neu einrichten (neuester Code), entfernen; ungenutzte Laufzeiten aufräumen.</summary>
    private void AddSourcePrograms(StackPanel sp)
    {
        var list = SourceApps.List(_deps.ToolsRoot);
        var unused = SourceApps.UnusedRuntimes(_deps.ToolsRoot);
        if (list.Count == 0 && unused.Count == 0) return;
        sp.Children.Add(H("Aus Quellcode eingerichtet", 13));
        sp.Children.Add(P("GitHub-Projekte ohne fertiges Windows-Programm, die Trendo mit einem offiziellen Grundprogramm (Python, Node.js, Go, .NET) eingerichtet hat. Alles liegt im Trendo-Werkzeugordner.", null, 11));
        foreach (var e in list)
        {
            var running = _srcRunning.TryGetValue(e.Id, out var p) && !p.HasExited || _srcSites.ContainsKey(e.Id);
            var kind = e.Kind switch { SourceApps.Kind.Python => "Python", SourceApps.Kind.Node => "Node.js", SourceApps.Kind.Web => "Web", SourceApps.Kind.Go => "Go", SourceApps.Kind.DotNet => ".NET", _ => e.Kind.ToString() };
            var repo = new OssRepo(e.Repo, e.Description, "https://github.com/" + e.Repo, "https://github.com/" + e.Repo + ".git", 0, e.License, "", DateTimeOffset.Now);
            var buttons = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
            void Add(Control c) { c.Margin = new Thickness(0, 0, 6, 4); buttons.Children.Add(c); }
            Add(Act("btn-src-start-" + e.Id, running ? "Öffnen" : "Starten", () => _ = SourceStart(e), true));
            if (running) Add(Act("btn-src-stop-" + e.Id, "Beenden", () => SourceStop(e.Id)));
            if (ClaudeOpenButton("btn-src-claude-" + e.Id, () => SourceApps.Dir(_deps.ToolsRoot, e.Id), () => null, "X-06") is { } cc) Add(cc);
            if (!e.Sha.Contains(':')) Add(Act("btn-src-again-" + e.Id, "Neu einrichten (neuester Code)", () => { _srcTree.Remove(e.Repo); _ = SourceInstall(repo, start: false); }));
            Add(RemoveButton("src:" + e.Id, "btn-src-remove-" + e.Id, () =>
            {
                SourceStop(e.Id);
                try { SourceApps.Remove(_deps.ToolsRoot, e.Id, _core.Audit); _depLog.Text = $"{e.Repo} wurde entfernt."; }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _depLog.Text = $"{e.Repo} läuft noch oder ist gesperrt: bitte schließen und erneut versuchen. ({ex.Message})"; }
            }));
            sp.Children.Add(Card(Row(new StackPanel { Children = {
                new TextBlock { Text = $"{e.Repo}  ·  {kind}{(running ? "  ·  ● läuft" : "")}", Foreground = Text, FontWeight = FontWeight.SemiBold },
                P($"{e.Description}{(e.Description.Length > 0 ? " · " : "")}eingerichtet am {e.InstalledAt:dd.MM.yyyy}{(e.Sha.Contains(':') ? $" · Paket {e.Sha}" : "")}", null, 11) } }, buttons)));
        }
        if (unused.Count > 0)
            sp.Children.Add(Act("btn-src-runtimes-clean", $"Ungenutzte Grundprogramme entfernen ({string.Join(", ", unused.Select(Path.GetFileName))})", () =>
            {
                foreach (var d in unused) try { Directory.Delete(d, true); _core.Audit.Append(Principal.LocalOwner.Name, "source.runtime.removed", Path.GetFileName(d)); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                Show("components");
            }));
    }
}
