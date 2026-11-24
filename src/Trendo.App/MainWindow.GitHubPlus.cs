using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>GitHub-Programme, Ausbau (Masterprompt 131): Trefferliste zeigt sofort „Windows-Programm“ oder „nur Quellcode“, Updates für installierte
/// Programme mit einem Klick, optionale GitHub-Anmeldung (Schlüssel im sicheren Windows-Speicher, nur für api.github.com).</summary>
public partial class MainWindow
{
    public const string GitHubTokenKey = "github-token";
    /// <summary>Optionaler GitHub-Schlüssel – nur für Anfragen an api.github.com, nie für Downloads.</summary>
    public static string? GitHubToken { get; set; }
    readonly Dictionary<string, (string Kind, string Text)> _ossAvail = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> _ossAvailBusy = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, string> _ghUpdates = new(StringComparer.OrdinalIgnoreCase);
    bool _ghUpdateChecked; string _ghTokenMsg = "";
    public IReadOnlyDictionary<string, (string Kind, string Text)> OssAvailability => _ossAvail;
    public IReadOnlyDictionary<string, string> GitHubUpdates => _ghUpdates;

    void LoadGitHubToken() { try { GitHubToken = _core.Secrets.Get(GitHubTokenKey) is { Length: > 0 } t ? t : null; } catch (Exception) { GitHubToken = null; } }

    /// <summary>Für die ersten Treffer (mit Anmeldung: alle) prüfen, ob es ein fertiges Windows-Programm gibt – drei gleichzeitig, zwischengespeichert.</summary>
    public async Task OssCheckAvailability(IEnumerable<string> repos)
    {
        var todo = repos.Where(r => !_ossAvail.ContainsKey(r) && _ossAvailBusy.Add(r)).ToList(); if (todo.Count == 0) return;
        using var gate = new SemaphoreSlim(3);
        await Task.WhenAll(todo.Select(async r =>
        {
            await gate.WaitAsync();
            try
            {
                var rel = await GitHubLatestOrEmpty(r, CancellationToken.None); _ossAvail[r] = GitHubApps.Availability(rel, Arm64, Has7z);
                if (_ossAvail[r].Kind != "windows") await SourceDetect(r); // T-0314: gleich sagen, ob es trotzdem läuft
            }
            catch (Exception ex) when (ex is not OutOfMemoryException) { if (ex.Message.Contains("60 Abfragen")) _w45Msg = ex.Message; }
            finally { gate.Release(); _ossAvailBusy.Remove(r); }
        }));
        if (CurrentView == "oss") Show("oss");
    }

    Control OssAvailBadge(OssRepo r, int i)
    {
        if (_ossAvail.TryGetValue(r.FullName, out var a))
        {
            var (col, bg) = a.Kind == "windows" ? ("#4ADE80", "#1A22B35E") : ("#FACC15", "#1AF5A524");
            return new Border { Name = $"oss-avail-{i}", Background = Brush.Parse(bg), CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 2), HorizontalAlignment = HorizontalAlignment.Left,
                Child = new TextBlock { Text = a.Text, Foreground = Brush.Parse(col), FontSize = 11.5, FontWeight = FontWeight.SemiBold } };
        }
        if (_ossAvailBusy.Contains(r.FullName)) return new TextBlock { Name = $"oss-avail-{i}", Text = "Prüfe Windows-Programm …", Foreground = Muted, FontSize = 11.5 };
        var b = Act($"oss-check-{i}", "Windows-Programm? prüfen", () => _ = OssCheckAvailability([r.FullName])); b.FontSize = 11; b.HorizontalAlignment = HorizontalAlignment.Left;
        return b;
    }

    /// <summary>Installierte GitHub-Programme mit der neuesten Veröffentlichung vergleichen (nur wenn dort ein Windows-Programm liegt).</summary>
    public async Task GitHubCheckUpdates()
    {
        _ghUpdateChecked = true; var n = 0;
        foreach (var e in GitHubApps.List(_deps.ToolsRoot))
        {
            try
            {
                var rel = await GitHubLatestOrEmpty(e.Repo, CancellationToken.None, fresh: true);
                if (GitHubApps.PickWindows(rel.Assets, Arm64, Has7z) is not null && GitHubApps.IsNewer(e.Version, rel.Tag)) { _ghUpdates[e.Id] = rel.Tag; n++; } else _ghUpdates.Remove(e.Id);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException) { _depLog.Text = GitHubWhy(ex); }
        }
        if (n == 0 && _depLog.Text?.Contains("60 Abfragen") != true) _depLog.Text = "Alle GitHub-Programme sind aktuell.";
        else if (n > 0) _depLog.Text = n == 1 ? "1 Update verfügbar." : $"{n} Updates verfügbar.";
        if (CurrentView == "components") Show("components");
    }

    /// <summary>Update: neueste Version laden und einrichten – die alte bleibt, bis die neue fertig ist.</summary>
    public async Task<string?> GitHubUpdate(GitHubApps.Installed e)
    {
        var exe = await GitHubInstall(new OssRepo(e.Repo, e.Description, "https://github.com/" + e.Repo, "https://github.com/" + e.Repo + ".git", 0, e.License, "", DateTimeOffset.Now), start: false);
        if (exe is not null) { _ghUpdates.Remove(e.Id); _core.Audit.Append(Principal.LocalOwner.Name, "github.update", $"{e.Repo} {e.Version} → {GitHubApps.List(_deps.ToolsRoot).FirstOrDefault(x => x.Id == e.Id)?.Version}"); }
        if (CurrentView == "components") Show("components");
        return exe;
    }

    /// <summary>Optionale Anmeldung: Schlüssel prüfen (Limit abfragen) und im sicheren Windows-Speicher ablegen.</summary>
    public async Task<bool> GitHubSetToken(string token)
    {
        token = token.Trim();
        if (token.Length < 20 || token.Any(char.IsWhiteSpace)) { _ghTokenMsg = "Das sieht nicht wie ein GitHub-Schlüssel aus."; Show("oss"); return false; }
        var old = GitHubToken; GitHubToken = token;
        try
        {
            var json = await GitHubJson("https://api.github.com/rate_limit", CancellationToken.None);
            using var d = System.Text.Json.JsonDocument.Parse(json); var limit = d.RootElement.GetProperty("resources").GetProperty("core").GetProperty("limit").GetInt32();
            _core.Secrets.Set(GitHubTokenKey, token); _core.Audit.Append(Principal.LocalOwner.Name, "github.token.set", $"Limit {limit}/h");
            _ghTokenMsg = $"✓ Angemeldet – {limit} Abfragen pro Stunde. Der Schlüssel liegt im sicheren Windows-Speicher."; Show("oss"); return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            GitHubToken = old; _ghTokenMsg = ex.Message.Contains("401") || ex is HttpRequestException ? "GitHub lehnt diesen Schlüssel ab." : GitHubWhy(ex); Show("oss"); return false;
        }
    }
    public void GitHubRemoveToken() { _core.Secrets.Delete(GitHubTokenKey); GitHubToken = null; _ghTokenMsg = "Abgemeldet – wieder 60 Abfragen pro Stunde."; _core.Audit.Append(Principal.LocalOwner.Name, "github.token.removed", ""); Show("oss"); }

    // ───── T-0762 (X-06): Installer und fremde Programme erst nach Signaturprüfung und eigener Bestätigung ─────

    /// <summary>Ein Installer wartet auf die Entscheidung der Person (Datei, Projekt, Veröffentlichung, Prüfergebnis, danach starten?).
    /// <see cref="Lock"/>: die Datei bleibt vom Prüfen bis zum Start gesperrt (nur Lesen erlaubt) – kein Austausch dazwischen (B5).</summary>
    public sealed record PendingInstaller(GitHubApps.InstallerOnlyException Io, OssRepo Repo, GitHubApps.Release Release, GitHubApps.Asset Asset, GitHubApps.InstallerCheck Check, bool Start, FileStream? Lock);
    PendingInstaller? _ghPending; bool _ghRiskAccepted;
    public PendingInstaller? GitHubPendingInstaller => _ghPending;
    /// <summary>Für Tests: Signaturprüfung ersetzen (sonst echte Authenticode-Prüfung über Windows).</summary>
    public Func<string, Trendo.Core.Sys.SignatureInfo>? GitHubSignatureOverride { get; set; }

    static FileStream? TryLock(string file) { try { return File.Exists(file) ? GitHubApps.OpenLocked(file) : null; } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; } }

    void GitHubHoldInstaller(GitHubApps.InstallerOnlyException io, OssRepo repo, GitHubApps.Release rel, GitHubApps.Asset asset, bool start)
    {
        if (_ghPending is { } old) { old.Lock?.Dispose(); if (old.Io.File != io.File && GitHubFetchOverride is null) try { File.Delete(old.Io.File); } catch (IOException) { } }
        var lk = TryLock(io.File);
        var chk = GitHubApps.CheckInstaller(io.File, io.Kind, GitHubSignatureOverride, lk);
        if (lk is null) chk = chk with { Blocked = true };
        _ghPending = new(io, repo, rel, asset, chk, start, lk); _ghRiskAccepted = false;
        _core.Audit.Append(Principal.LocalOwner.Name, "github.installer.held", $"{repo.FullName} {asset.Name} ampel={chk.Ampel} signer={chk.Publisher ?? "-"} sha256={chk.Sha256}");
        var msg = $"„{asset.Name}“ ist ein Installer ({chk.Title}). Trendo hat ihn NICHT ausgeführt – entscheide in der Karte „Installer wartet“ unter „Projekte von GitHub holen“.";
        FinishSetupOverlay("Installer: deine Entscheidung", msg, []);
        Say(msg, "oss");
    }

    /// <summary>Zweiter, eigener Schritt: Gelb/Rot brauchen vorher die ausdrückliche Risiko-Bestätigung (<see cref="GitHubAcceptInstallerRisk"/>).
    /// <paramref name="expected"/> (B6): nur genau der angezeigte Installer – wurde er inzwischen ersetzt, passiert nichts.
    /// Vor dem Lauf wird die gesperrte Datei erneut gehasht (SHA-256).</summary>
    public async Task<string?> GitHubConfirmInstaller(PendingInstaller? expected = null)
    {
        if (_ghPending is not { } p) return null;
        if (expected is not null && !ReferenceEquals(expected, p)) { Say("Inzwischen wartet ein anderer Installer – bitte die neue Karte prüfen.", "oss"); return null; }
        if (p.Check.Blocked || p.Lock is null) { Say("Der Installer ist nicht mehr da – bitte noch einmal „Installieren“.", "oss"); GitHubCancelInstaller(); return null; }
        if (p.Check.NeedsRiskConsent && !_ghRiskAccepted) { Say("Dieser Installer ist nicht vertrauenswürdig signiert. Erst das Risiko ausdrücklich bestätigen, dann ausführen.", "oss"); return null; }
        if (GitHubApps.Sha256Of(p.Lock) != p.Check.Sha256)
        { Say("Die Installer-Datei hat sich seit der Prüfung verändert – Trendo führt sie nicht aus.", "oss"); GitHubCancelInstaller(); return null; }
        if (_depBusy) return null;
        _depBusy = true; _ghPending = null;
        _core.Audit.Append(Principal.LocalOwner.Name, "github.installer.consent", $"{p.Repo.FullName} {p.Asset.Name} ampel={p.Check.Ampel} risiko-bestaetigt={_ghRiskAccepted}");
        BeginSetupOverlay($"{p.Repo.FullName.Split('/')[1]} wird eingerichtet");
        try
        {
            SetupOverlayProgress(3, 4, $"Installer läuft still für dich ({p.Io.Kind}) …");
            var exe = await GitHubRunInstaller(p.Io, p.Repo, p.Release, p.Asset); // die Sperre hält, bis der Installer fertig ist
            SetupOverlayProgress(4, 4, "Fertig ✓");
            // Z1b: auch nach dem Installer ist der erste Start ein eigener bestätigter Schritt (Karte „Erster Start“)
            var entry = GitHubApps.List(_deps.ToolsRoot).FirstOrDefault(x => x.Id == GitHubApps.IdFor(p.Repo.FullName));
            FinishSetupOverlay("Bereit", $"„{p.Repo.FullName.Split('/')[1]}“ ist eingerichtet.", []);
            if (p.Start && entry is not null) { _depBusy = false; GitHubGate(entry, exe, () => Launch(exe)); return exe; }
            var msg = $"„{p.Repo.FullName.Split('/')[1]}“ ist eingerichtet. Du findest es unter „Programme“.";
            Say(msg, "oss");
            return exe;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            var why = GitHubWhy(ex);
            _core.Audit.Append(Principal.LocalOwner.Name, "github.install.failed", $"{p.Repo.FullName}: {ex.GetType().Name}: {ex.Message}");
            FinishSetupOverlay("Installieren hat nicht geklappt", why, [why]); Say("Installieren hat nicht geklappt: " + why, "oss");
            return null;
        }
        finally
        {
            _depBusy = false; _ghRiskAccepted = false;
            p.Lock.Dispose();
            if (GitHubFetchOverride is null) try { File.Delete(p.Io.File); } catch (IOException) { }
        }
    }

    /// <summary>Eigene ausdrückliche Bestätigung für Gelb/Rot (Häkchen) – führt selbst nichts aus.</summary>
    public void GitHubAcceptInstallerRisk(bool accepted, PendingInstaller? expected = null)
    {
        _ghRiskAccepted = accepted && _ghPending is not null && (expected is null || ReferenceEquals(expected, _ghPending));
        if (_ghRiskAccepted) _core.Audit.Append(Principal.LocalOwner.Name, "github.installer.risk", _ghPending!.Repo.FullName);
    }

    public void GitHubCancelInstaller()
    {
        if (_ghPending is not { } p) return;
        _ghPending = null; _ghRiskAccepted = false;
        p.Lock?.Dispose();
        if (GitHubFetchOverride is null) try { File.Delete(p.Io.File); } catch (IOException) { }
        _core.Audit.Append(Principal.LocalOwner.Name, "github.installer.declined", p.Repo.FullName);
        Say($"Installer von „{p.Repo.FullName}“ nicht ausgeführt und gelöscht.", "oss");
    }

    /// <summary>Hinweis-Karte mit Ampel: wer signiert hat, was das bedeutet, Prüfsumme; Ausführen ist ein eigener Klick, bei Gelb/Rot erst nach dem Häkchen.</summary>
    Control GitHubInstallerCard()
    {
        if (_ghPending is not { } p) return new Panel();
        return AmpelCard("oss-installer", p.Check, $"Installer wartet: {p.Repo.FullName} · {p.Check.Title}", $"Datei: {p.Asset.Name}",
            p.Check.Ampel == GitHubApps.Ampel.Gruen ? "Installer ausführen" : "Trotzdem ausführen", "Nicht ausführen (löschen)",
            "Ich habe verstanden: Trendo kann diesen Installer nicht als vertrauenswürdig bestätigen. Er läuft mit meinen Rechten und ich vertraue dem Projekt trotzdem.",
            () => _ghRiskAccepted, ok => GitHubAcceptInstallerRisk(ok, p), () => _ = GitHubConfirmInstaller(p), () => { GitHubCancelInstaller(); Show("oss"); }, () => _depBusy);
    }

    Border AmpelCard(string id, GitHubApps.InstallerCheck c, string title, string file, string runLabel, string cancelLabel, string riskText, Func<bool> risk, Action<bool> setRisk, Action run, Action cancel, Func<bool> busy)
    {
        var (col, bg, dot) = c.Ampel switch { GitHubApps.Ampel.Gruen => ("#4ADE80", "#1A22B35E", "● Grün"), GitHubApps.Ampel.Gelb => ("#FACC15", "#1AF5A524", "● Gelb"), _ => ("#F87171", "#1FB91C1C", "● Rot") };
        var runBtn = Act($"{id}-run", runLabel, run);
        runBtn.IsEnabled = !c.Blocked && (!c.NeedsRiskConsent || risk()) && !busy();
        var sp = new StackPanel { Spacing = 6, Children = {
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = {
                new TextBlock { Name = $"{id}-ampel", Text = dot, Foreground = Brush.Parse(col), FontWeight = FontWeight.Bold, FontSize = 13 },
                new TextBlock { Text = title, Foreground = Text, FontWeight = FontWeight.SemiBold, FontSize = 13.5, TextWrapping = TextWrapping.Wrap } } },
            P(c.Text, Text, 12),
            new SelectableTextBlock { Text = $"{file} · SHA-256: {c.Sha256}", Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap } } };
        if (c.NeedsRiskConsent && !c.Blocked)
        {
            var box = new CheckBox { Name = $"{id}-risk", IsChecked = risk(), Content = new TextBlock { Text = riskText, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap } };
            box.IsCheckedChanged += (_, _) => { setRisk(box.IsChecked == true); runBtn.IsEnabled = risk() && !busy(); };
            sp.Children.Add(box);
        }
        sp.Children.Add(new WrapPanel { Children = { runBtn, new Border { Width = 8 }, Act($"{id}-cancel", cancelLabel, cancel) } });
        return new Border { Name = $"{id}-card", Background = Brush.Parse(bg), BorderBrush = Brush.Parse(col), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 10), Child = sp };
    }

    // ───── B2/N2/Z1: erster Start eines Programms – immer ein eigener bestätigter Schritt ─────

    /// <summary>Ein Programmstart wartet auf die eigene Bestätigung. <see cref="Locks"/>: Startdatei und alle ausführbaren Inhalte des Ordners bleiben bis dahin gesperrt.
    /// <see cref="Run"/> startet danach genau so, wie der Aufrufer es wollte; <see cref="Approve"/> merkt die Erlaubnis zur Ordner-Prüfsumme.</summary>
    public sealed record PendingStart(string Key, string Label, string Exe, string Dir, GitHubApps.InstallerCheck Check, GitHubApps.FileLocks Locks, Action Run, Action<string> Approve, string? EntryId);
    PendingStart? _ghStart; bool _ghStartRisk;
    public PendingStart? GitHubPendingStart => _ghStart;

    /// <summary>Programmordner eines Eintrags: eigener Ordner im Werkzeugordner bzw. (bei Installern) der Ordner der Startdatei.</summary>
    string GitHubProgramDir(GitHubApps.Installed e) => Path.IsPathRooted(e.Exe) ? Path.GetDirectoryName(e.Exe)! : Path.Combine(_deps.ToolsRoot, e.Id);

    /// <summary>Gehört <paramref name="exe"/> zu einem Programm von GitHub? (für KI-Quellen und Bambu Studio)</summary>
    GitHubApps.Installed? GitHubEntryFor(string exe)
    {
        var full = Path.GetFullPath(exe);
        return GitHubApps.List(_deps.ToolsRoot).FirstOrDefault(e => full.StartsWith(Path.GetFullPath(GitHubProgramDir(e)).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Programm von GitHub starten (ohne Argumente über die Shell).</summary>
    public bool GitHubLaunch(GitHubApps.Installed e) { var exe = GitHubApps.ExePath(_deps.ToolsRoot, e); return GitHubGate(e, exe, () => Launch(exe)); }

    /// <summary>Tor vor jedem Start eines GitHub-Programms.</summary>
    public bool GitHubGate(GitHubApps.Installed e, string exe, Action run)
    {
        var dir = Path.GetFullPath(exe).StartsWith(Path.GetFullPath(GitHubProgramDir(e)), StringComparison.OrdinalIgnoreCase) ? GitHubProgramDir(e) : Path.GetDirectoryName(exe)!;
        return StartGate("gh:" + e.Id, e.Repo, exe, dir, e.StartApproved, h => GitHubApps.ApproveStart(_deps.ToolsRoot, e.Id, h), run, e.Id);
    }

    /// <summary>Z3a: Tor für ein Programm außerhalb der GitHub-Liste (KI-Programm vom Hersteller); Erlaubnis in „start-freigaben.json“.</summary>
    public bool ExternalGate(string key, string label, string exe, Action run) =>
        StartGate(key, label, exe, Path.GetDirectoryName(Path.GetFullPath(exe))!, GitHubApps.ExternalApproval(_deps.ToolsRoot, key), h => GitHubApps.ApproveExternal(_deps.ToolsRoot, key, h), run, null);

    /// <summary>Z1: Startdatei und ausführbare Inhalte sperren, Prüfsumme über ALLE Dateien des Ordners. Schon erlaubt (gleiche Prüfsumme) → <paramref name="run"/> sofort
    /// (unter Sperre). Sonst – auch bei Grün – Karte „Erster Start“: Grün ohne Häkchen, aber eigener Klick; Gelb/Rot mit Häkchen. True = gestartet.</summary>
    bool StartGate(string key, string label, string exe, string dir, string? approved, Action<string> approve, Action run, string? entryId)
    {
        GitHubApps.FileLocks locks;
        try
        {
            if (!File.Exists(exe)) throw new FileNotFoundException(exe);
            var exec = GitHubApps.ExecutableFiles(dir);
            locks = new GitHubApps.FileLocks(exec.Count <= GitHubApps.MaxExecutables ? exec.Append(exe).Distinct(StringComparer.OrdinalIgnoreCase) : [exe]);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Say($"„{Path.GetFileName(exe)}“ fehlt oder ist gesperrt – bitte neu einrichten. ({ex.Message})", "oss"); return false; }
        GitHubApps.InstallerCheck chk;
        try
        {
            if (approved is { } ok && GitHubApps.FolderHashAll(dir) == ok)
            { using (locks) run(); _core.Audit.Append(Principal.LocalOwner.Name, "github.start", label); return true; }
            chk = GitHubApps.CheckProgramFolder(exe, dir, GitHubSignatureOverride);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { locks.Dispose(); Say("Das Programm ließ sich nicht prüfen: " + ex.Message, "oss"); return false; }
        if (_ghStart is { } old) old.Locks.Dispose();
        _ghStart = new(key, label, exe, dir, chk, locks, run, approve, entryId); _ghStartRisk = false;
        _core.Audit.Append(Principal.LocalOwner.Name, "github.start.held", $"{label} ampel={chk.Ampel} ordner-sha256={chk.Sha256}");
        Say($"„{label}“ ist noch nicht gestartet ({chk.Title}). Der erste Start ist ein eigener Schritt: entscheide in der Karte „Erster Start“ unter „Projekte von GitHub holen“.", "oss"); // N6: springt dorthin
        return false;
    }

    /// <summary>Eigene Bestätigung für den ersten Start (Gelb/Rot erst nach dem Häkchen); nur für genau die angezeigte Karte (B6).
    /// Merkt die Erlaubnis zur Prüfsumme über alle Dateien des Programmordners – jede Änderung danach fragt erneut (Z1).</summary>
    public bool GitHubConfirmStart(PendingStart? expected = null)
    {
        if (_ghStart is not { } p) return false;
        if (expected is not null && !ReferenceEquals(expected, p)) return false;
        if (p.Check.NeedsRiskConsent && !_ghStartRisk) { Say("Dieses Programm ist nicht vollständig vertrauenswürdig signiert. Erst das Risiko ausdrücklich bestätigen, dann starten.", "oss"); return false; }
        _ghStart = null;
        using (p.Locks)
        {
            string now;
            try { now = GitHubApps.FolderHashAll(p.Dir); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { now = ""; }
            if (now != p.Check.Sha256) { Say("Der Programmordner hat sich seit der Prüfung verändert – Trendo startet nicht. Bitte erneut „Starten“.", "oss"); return false; }
            p.Approve(now);
            _core.Audit.Append(Principal.LocalOwner.Name, "github.start.consent", $"{p.Label} ampel={p.Check.Ampel} risiko-bestaetigt={_ghStartRisk}");
            p.Run();
        }
        _ghStartRisk = false;
        Say($"„{p.Label}“ startet. Künftig startet es ohne Rückfrage, solange sich keine Datei im Programmordner ändert.", "oss");
        return true;
    }

    /// <summary>Wartenden Start eines Programms verwerfen, das gerade entfernt wird (gibt die Sperren frei).</summary>
    void GitHubReleaseStart(string id) { if (_ghStart is { } p && p.EntryId == id) { _ghStart = null; _ghStartRisk = false; p.Locks.Dispose(); } }

    public void GitHubCancelStart()
    {
        if (_ghStart is not { } p) return;
        _ghStart = null; _ghStartRisk = false; p.Locks.Dispose();
        _core.Audit.Append(Principal.LocalOwner.Name, "github.start.declined", p.Label);
        Say($"„{p.Label}“ wurde nicht gestartet.", "oss");
    }

    Control GitHubStartCard()
    {
        if (_ghStart is not { } p) return new Panel();
        return AmpelCard("oss-firststart", p.Check, $"Erster Start: {p.Label} · {p.Check.Title}", $"Programm: {Path.GetFileName(p.Exe)} · Prüfsumme über alle Dateien des Programmordners",
            p.Check.Ampel == GitHubApps.Ampel.Gruen ? "Starten" : "Trotzdem starten", "Nicht starten",
            "Ich habe verstanden: Trendo kann dieses Programm (samt seinem Ordner) nicht als vertrauenswürdig bestätigen. Es läuft mit meinen Rechten und ich vertraue dem Projekt trotzdem.",
            () => _ghStartRisk, ok => { _ghStartRisk = ok && ReferenceEquals(_ghStart, p); if (_ghStartRisk) _core.Audit.Append(Principal.LocalOwner.Name, "github.start.risk", p.Label); },
            () => { GitHubConfirmStart(p); Show("oss"); }, () => { GitHubCancelStart(); Show("oss"); }, () => false);
    }

    /// <summary>Für Tests und die Karte: Häkchen „Risiko verstanden“ beim ersten Start.</summary>
    public void GitHubAcceptStartRisk(bool ok) { _ghStartRisk = ok && _ghStart is not null; }

    // ───── Z3: KI-Programme vom Hersteller ─────

    bool _kiExtGateOpen;
    /// <summary>Z3a: Start eines nicht portablen KI-Programms (Herstellerinstallation) durch das Tor. Null = schon durch das Tor, normal weiter starten;
    /// sonst Ergebnis des Tors (true = gestartet, false = Karte „Erster Start“ wartet). <paramref name="again"/> ruft den Start erneut auf.</summary>
    bool? KiExternalStartGate(string id, string exe, Func<bool> again)
    {
        if (_kiExtGateOpen) return null;
        return ExternalGate("ki:" + id, KiCatalog.Sources.FirstOrDefault(x => x.Id == id)?.Name ?? id, exe, () => { _kiExtGateOpen = true; try { again(); } finally { _kiExtGateOpen = false; } });
    }

    /// <summary>Z3b: Installer des Herstellers vor dem Start prüfen (Authenticode, Ampel, SHA-256) und eigene Rückfrage (eigener Dialog, nie im selben Klick wie „Einrichten“).
    /// Die Datei bleibt von der Prüfung bis zum Start gesperrt; Rückgabe: Sperre (vom Aufrufer nach dem Start freizugeben) oder null = nicht ausführen.</summary>
    async Task<IDisposable?> KiInstallerGate(string file, string name)
    {
        FileStream lk;
        try { lk = GitHubApps.OpenLocked(file); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _srcMsg = "Installer nicht lesbar: " + ex.Message; return null; }
        var c = GitHubApps.CheckVendorInstaller(file, name, GitHubSignatureOverride, lk);
        var ampel = c.Ampel switch { GitHubApps.Ampel.Gruen => "● Grün", GitHubApps.Ampel.Gelb => "● Gelb", _ => "● Rot" };
        var text = $"{ampel} – {c.Title}\n\n{c.Text}\n\nSHA-256: {c.Sha256}" + (c.NeedsRiskConsent ? "\n\nMit „Trotzdem ausführen“ bestätigst du ausdrücklich, dass du dem Hersteller trotzdem vertraust." : "");
        var ok = await FreigabeAsync(new Wirkung("ki.installer.run", name, $"Installer für {name} ausführen?", text, c.NeedsRiskConsent ? "Trotzdem ausführen" : "Installer ausführen"));
        _core.Audit.Append(Principal.LocalOwner.Name, ok is null ? "ki.installer.declined" : "ki.installer.consent", $"{name} ampel={c.Ampel} sha256={c.Sha256}");
        if (ok is null || GitHubApps.Sha256Of(lk) != c.Sha256) { lk.Dispose(); if (ok is not null) _srcMsg = "Die Installer-Datei hat sich verändert – nicht ausgeführt."; return null; }
        return lk;
    }

    Control GitHubTokenBox()
    {
        var box = new TextBox { Name = "oss-token", PasswordChar = '•', PlaceholderText = "GitHub-Schlüssel (fine-grained, nur Lesen)", Width = 320 };
        var content = new StackPanel { Spacing = 6, Children = {
            P("Freiwillig. Ohne Anmeldung erlaubt GitHub 60 Abfragen pro Stunde, mit eigenem Schlüssel 5000. Ein Schlüssel ohne besondere Rechte reicht (github.com › Settings › Developer settings › Personal access tokens). Trendo schickt ihn nur an api.github.com.", Muted, 11.5),
            GitHubToken is null
                ? new WrapPanel { Children = { box, new Border { Width = 6 }, Act("oss-token-save", "Prüfen & speichern", () => _ = GitHubSetToken(box.Text ?? ""), true) } }
                : new WrapPanel { Children = { new TextBlock { Text = "✓ Angemeldet (Schlüssel im sicheren Windows-Speicher)", Foreground = Brush.Parse("#4ADE80"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) }, Act("oss-token-remove", "Abmelden", GitHubRemoveToken) } },
            new TextBlock { Name = "oss-token-msg", Text = _ghTokenMsg, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap, IsVisible = _ghTokenMsg.Length > 0 } } };
        return new Expander { Name = "oss-token-box", Header = GitHubToken is null ? "GitHub-Anmeldung (optional, mehr Abfragen)" : "GitHub-Anmeldung ✓", Content = content, IsExpanded = _ghTokenMsg.Length > 0 };
    }
}
