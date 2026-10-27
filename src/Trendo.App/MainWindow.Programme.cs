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
    private Control GuideBox(ProgramGuide.Guide g, string name)
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
        return new Expander { Name = name, Header = new TextBlock { Text = "Beschreibung & Funktionsweise", Foreground = Accent, FontSize = 11.5 }, Content = body, HorizontalAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(0) };
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
        var b = new Button { Name = name, Content = armed ? "Wirklich entfernen?" : "Entfernen", IsEnabled = !_depBusy, Background = armed ? Brush.Parse("#B91C1C") : null };
        b.Click += (_, _) =>
        {
            if (!_confirmRemove.Remove(key)) { _confirmRemove.Add(key); Show("components"); return; }
            remove(); ReloadStudios(); Show("components");
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
            var remove = RemoveButton("gh:" + e.Id, "btn-gh-remove-" + e.Id, () =>
            {
                try { if (Path.IsPathRooted(e.Exe) && File.Exists(e.Exe)) GitHubRunUninstaller(e.Exe); _depLog.Text = GitHubApps.Uninstall(_deps.ToolsRoot, e.Id, _core.Audit) ? $"{e.Repo} wurde entfernt." : $"{e.Repo} war nicht installiert."; }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _depLog.Text = $"{e.Repo} läuft noch oder ist gesperrt: bitte schließen und erneut versuchen. ({ex.Message})"; }
            });
            sp.Children.Add(Card(new StackPanel { Spacing = 4, Children = {
                Row(new StackPanel { Children = { new TextBlock { Text = $"{e.Repo}  ·  {e.Version}", Foreground = Text, FontWeight = FontWeight.SemiBold }, P(e.Description, null, 11) } },
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center, Children = { start, remove } }),
                GuideBox(guide, "guide-" + e.Id) } }));
        }
        var find = new Button { Name = "btn-gh-find", Content = "Programme auf GitHub finden" }; find.Click += (_, _) => Show("oss");
        sp.Children.Add(find);
    }

    /// <summary>Für Tests: Installer nicht wirklich ausführen (Datei, Argumente).</summary>
    public Func<string, string[], Task>? GitHubInstallerRunOverride { get; set; }
    /// <summary>Für Tests: Ordner, in denen Installer Programme ablegen (sonst %LOCALAPPDATA%\Programs und %LOCALAPPDATA%).</summary>
    public string[]? GitHubInstallRootsOverride { get; set; }
    static bool Arm64 => System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.Arm64;
    bool Has7z => File.Exists(Path.Combine(_deps.ToolsRoot, "7zip", "7z.exe"));

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
            SetupOverlayProgress(0, 4, "Neueste Version auf GitHub suchen …");
            GitHubApps.Release rel; GitHubApps.Asset? asset;
            if (GitHubFetchOverride is not null) { (rel, file) = await GitHubFetchOverride(repo.FullName); asset = GitHubApps.PickWindows(rel.Assets, Arm64, Has7z); }
            else
            {
                rel = await GitHubLatestOrEmpty(repo.FullName);
                asset = GitHubApps.PickWindows(rel.Assets, Arm64, Has7z);
                if (asset is null)
                {
                    using var res = await GitHubGet(KiSourceSetups.ReleasesUrl(repo.FullName), "application/vnd.github+json");
                    foreach (var r in KiSourceSetups.ParseReleases(await res.Content.ReadAsStringAsync(SetupToken)))
                        if (GitHubApps.PickWindows(r.Assets, Arm64, Has7z) is { } a) { (rel, asset) = (r, a); break; }
                }
            }
            if (asset is null) { Say($"„{repo.FullName}“ bietet kein fertiges Windows-Programm an – nur Quellcode. Mit „Quellcode holen“ landet es in deinem Ordner „Open Source“.", "oss"); return null; }
            SetupOverlayProgress(1, 4, $"{asset.Name} wird geladen …");
            file ??= await GitHubDownload(asset);
            SetupToken.ThrowIfCancellationRequested();
            SetupOverlayProgress(2, 4, "Prüfen und einrichten …");
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
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or IOException or UnauthorizedAccessException or TaskCanceledException or System.Text.Json.JsonException or InvalidDataException or System.ComponentModel.Win32Exception)
        {
            _core.Audit.Append(Principal.LocalOwner.Name, "github.install.failed", $"{repo.FullName}: {ex.Message}");
            FinishSetupOverlay("Installieren hat nicht geklappt", ex.Message, [ex.Message]);
            Say("Installieren hat nicht geklappt: " + ex.Message, "oss");
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
        var args = GitHubApps.SilentArgs(io.Kind);
        if (GitHubInstallerRunOverride is { } run) await run(io.File, args);
        else
        {
            var psi = new System.Diagnostics.ProcessStartInfo(io.File) { UseShellExecute = false };
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = System.Diagnostics.Process.Start(psi) ?? throw new InvalidOperationException("Der Installer ließ sich nicht starten.");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(SetupToken); timeout.CancelAfter(TimeSpan.FromMinutes(15));
            await p.WaitForExitAsync(timeout.Token);
            if (io.Kind == GitHubApps.InstallerKind.Squirrel) await Task.Delay(3000, SetupToken); // Squirrel richtet nach dem Ende noch kurz ein
        }
        var name = repo.FullName.Split('/')[1].ToLowerInvariant();
        var stem = System.Text.RegularExpressions.Regex.Replace(Path.GetFileNameWithoutExtension(asset.Name).ToLowerInvariant(), @"[-_. ]?(setup|installer|x64|win(64)?|\d+(\.\d+)*)", "").Trim('-', '_', ' ');
        var fresh = roots.Where(Directory.Exists).SelectMany(Directory.GetDirectories)
            .Where(d => !before.TryGetValue(d, out var t) || Directory.GetLastWriteTimeUtc(d) > t || Directory.GetLastWriteTimeUtc(d) >= started)
            .OrderByDescending(d => Path.GetFileName(d).ToLowerInvariant().Replace(" ", "-") is var n && (n.Contains(name) || stem.Length > 2 && n.Contains(stem)) ? 1 : 0).ToList();
        var exe = fresh.Select(d => GitHubApps.MainExe(d, repo.FullName)).FirstOrDefault(e => e is not null)
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

    static async Task<GitHubApps.Release> GitHubLatestOrEmpty(string fullName)
    {
        try { return await GitHubLatest(fullName); }
        catch (InvalidOperationException) { return new GitHubApps.Release("", "", []); } // keine „neueste“ Version: Liste prüfen
    }

    static readonly string[] GitHubHosts = ["github.com", "api.github.com", "objects.githubusercontent.com", "release-assets.githubusercontent.com", "github-releases.githubusercontent.com"];
    static readonly HttpClient GitHubHttp = new(new SocketsHttpHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(30) };

    static void CheckHost(Uri u)
    {
        if (u.Scheme != Uri.UriSchemeHttps || u.Port != 443 || !GitHubHosts.Contains(u.Host, StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException("Unerwartete Download-Adresse: " + u.Host);
    }

    /// <summary>GET mit eigenen Weiterleitungen (höchstens 5), jede nur zu GitHub über HTTPS.</summary>
    static async Task<HttpResponseMessage> GitHubGet(string url, string accept)
    {
        var u = new Uri(url);
        for (var i = 0; i < 6; i++)
        {
            CheckHost(u);
            var req = new HttpRequestMessage(HttpMethod.Get, u);
            req.Headers.UserAgent.Add(new ProductInfoHeaderValue("TrendoOS", "1.0")); req.Headers.Accept.ParseAdd(accept);
            var res = await GitHubHttp.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
            if ((int)res.StatusCode is >= 300 and < 400 && res.Headers.Location is { } loc) { u = loc.IsAbsoluteUri ? loc : new Uri(u, loc); res.Dispose(); continue; }
            if (res.StatusCode == HttpStatusCode.NotFound) { res.Dispose(); throw new InvalidOperationException("Dieses Projekt hat noch keine Veröffentlichung mit fertigen Programmen."); }
            if ((int)res.StatusCode == 403) { res.Dispose(); throw new InvalidOperationException("GitHub bremst gerade. Versuch es in einer Minute noch mal."); }
            res.EnsureSuccessStatusCode();
            return res;
        }
        throw new InvalidOperationException("Zu viele Weiterleitungen.");
    }

    static async Task<GitHubApps.Release> GitHubLatest(string fullName)
    {
        using var res = await GitHubGet(GitHubApps.LatestReleaseUrl(fullName), "application/vnd.github+json");
        return GitHubApps.ParseRelease(await res.Content.ReadAsStringAsync());
    }

    async Task<string> GitHubDownload(GitHubApps.Asset asset)
    {
        var file = Path.Combine(Path.GetTempPath(), "trendo-gh-" + Guid.NewGuid().ToString("N") + Path.GetExtension(asset.Name));
        using var res = await GitHubGet(asset.Url, "application/octet-stream");
        var total = res.Content.Headers.ContentLength ?? asset.Size;
        if (total > GitHubApps.MaxBytes) throw new InvalidOperationException("Die Datei ist zu groß.");
        await using var src = await res.Content.ReadAsStreamAsync();
        await using var dst = File.Create(file);
        var buf = new byte[1 << 16]; long done = 0; int n; var last = DateTime.UtcNow;
        while ((n = await src.ReadAsync(buf, SetupToken)) > 0)
        {
            done += n;
            if (done > GitHubApps.MaxBytes) throw new InvalidOperationException("Die Datei ist größer als angegeben.");
            await dst.WriteAsync(buf.AsMemory(0, n));
            if ((DateTime.UtcNow - last).TotalMilliseconds > 250 && total > 0)
            {
                last = DateTime.UtcNow; var d = done;
                Avalonia.Threading.Dispatcher.UIThread.Post(() => SetupOverlayProgress(1, 3, $"{asset.Name}: {d / (1 << 20)} von {total / (1 << 20)} MB"));
            }
        }
        return file;
    }
}
