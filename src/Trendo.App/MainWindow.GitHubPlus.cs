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

    // ───── T-0762 (X-06): Installer erst nach Signaturprüfung und eigener Bestätigung ─────

    /// <summary>Ein Installer wartet auf die Entscheidung der Person (Datei, Projekt, Veröffentlichung, Prüfergebnis, danach starten?).</summary>
    public sealed record PendingInstaller(GitHubApps.InstallerOnlyException Io, OssRepo Repo, GitHubApps.Release Release, GitHubApps.Asset Asset, GitHubApps.InstallerCheck Check, bool Start);
    PendingInstaller? _ghPending; bool _ghRiskAccepted;
    public PendingInstaller? GitHubPendingInstaller => _ghPending;
    /// <summary>Für Tests: Signaturprüfung ersetzen (sonst echte Authenticode-Prüfung über Windows).</summary>
    public Func<string, Trendo.Core.Sys.SignatureInfo>? GitHubSignatureOverride { get; set; }

    void GitHubHoldInstaller(GitHubApps.InstallerOnlyException io, OssRepo repo, GitHubApps.Release rel, GitHubApps.Asset asset, bool start)
    {
        if (_ghPending is { } old && old.Io.File != io.File) try { File.Delete(old.Io.File); } catch (IOException) { }
        var chk = GitHubApps.CheckInstaller(io.File, io.Kind, GitHubSignatureOverride);
        _ghPending = new(io, repo, rel, asset, chk, start); _ghRiskAccepted = false;
        _core.Audit.Append(Principal.LocalOwner.Name, "github.installer.held", $"{repo.FullName} {asset.Name} ampel={chk.Ampel} signer={chk.Publisher ?? "-"} sha256={chk.Sha256}");
        var msg = $"„{asset.Name}“ ist ein Installer ({chk.Title}). Trendo hat ihn NICHT ausgeführt – entscheide in der Karte „Installer wartet“ unter „Projekte von GitHub holen“.";
        FinishSetupOverlay("Installer: deine Entscheidung", msg, []);
        Say(msg, "oss");
    }

    /// <summary>Zweiter, eigener Schritt: Gelb/Rot brauchen vorher die ausdrückliche Risiko-Bestätigung (<see cref="GitHubAcceptInstallerRisk"/>).
    /// Vor dem Lauf wird geprüft, dass die Datei noch genau die geprüfte ist (SHA-256).</summary>
    public async Task<string?> GitHubConfirmInstaller()
    {
        if (_ghPending is not { } p) return null;
        if (p.Check.Blocked) { Say("Der Installer ist nicht mehr da – bitte noch einmal „Installieren“.", "oss"); GitHubCancelInstaller(); return null; }
        if (p.Check.NeedsRiskConsent && !_ghRiskAccepted) { Say("Dieser Installer ist nicht vertrauenswürdig signiert. Erst das Risiko ausdrücklich bestätigen, dann ausführen.", "oss"); return null; }
        if (!File.Exists(p.Io.File) || Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(p.Io.File))) != p.Check.Sha256)
        { Say("Die Installer-Datei hat sich seit der Prüfung verändert – Trendo führt sie nicht aus.", "oss"); GitHubCancelInstaller(); return null; }
        if (_depBusy) return null;
        _depBusy = true; _ghPending = null;
        _core.Audit.Append(Principal.LocalOwner.Name, "github.installer.consent", $"{p.Repo.FullName} {p.Asset.Name} ampel={p.Check.Ampel} risiko-bestaetigt={_ghRiskAccepted}");
        BeginSetupOverlay($"{p.Repo.FullName.Split('/')[1]} wird eingerichtet");
        try
        {
            SetupOverlayProgress(3, 4, $"Installer läuft still für dich ({p.Io.Kind}) …");
            var exe = await GitHubRunInstaller(p.Io, p.Repo, p.Release, p.Asset);
            SetupOverlayProgress(4, 4, "Fertig ✓");
            if (p.Start) { Launch(exe); _core.Audit.Append(Principal.LocalOwner.Name, "github.start", p.Repo.FullName); }
            var msg = $"„{p.Repo.FullName.Split('/')[1]}“ ist eingerichtet{(p.Start ? " und startet gerade" : "")}. Du findest es unter „Programme“.";
            FinishSetupOverlay("Bereit", msg, []); Say(msg, "oss");
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
            if (GitHubFetchOverride is null) try { File.Delete(p.Io.File); } catch (IOException) { }
        }
    }

    /// <summary>Eigene ausdrückliche Bestätigung für Gelb/Rot (Häkchen) – führt selbst nichts aus.</summary>
    public void GitHubAcceptInstallerRisk(bool accepted) { _ghRiskAccepted = accepted && _ghPending is not null; if (_ghRiskAccepted) _core.Audit.Append(Principal.LocalOwner.Name, "github.installer.risk", _ghPending!.Repo.FullName); }

    public void GitHubCancelInstaller()
    {
        if (_ghPending is not { } p) return;
        _ghPending = null; _ghRiskAccepted = false;
        if (GitHubFetchOverride is null) try { File.Delete(p.Io.File); } catch (IOException) { }
        _core.Audit.Append(Principal.LocalOwner.Name, "github.installer.declined", p.Repo.FullName);
        Say($"Installer von „{p.Repo.FullName}“ nicht ausgeführt und gelöscht.", "oss");
    }

    /// <summary>Hinweis-Karte mit Ampel: wer signiert hat, was das bedeutet, Prüfsumme; Ausführen ist ein eigener Klick, bei Gelb/Rot erst nach dem Häkchen.</summary>
    Control GitHubInstallerCard()
    {
        if (_ghPending is not { } p) return new Panel();
        var (col, bg, dot) = p.Check.Ampel switch { GitHubApps.Ampel.Gruen => ("#4ADE80", "#1A22B35E", "● Grün"), GitHubApps.Ampel.Gelb => ("#FACC15", "#1AF5A524", "● Gelb"), _ => ("#F87171", "#1FB91C1C", "● Rot") };
        var run = Act("oss-installer-run", p.Check.Ampel == GitHubApps.Ampel.Gruen ? "Installer ausführen" : "Trotzdem ausführen", () => _ = GitHubConfirmInstaller());
        run.IsEnabled = !p.Check.Blocked && (!p.Check.NeedsRiskConsent || _ghRiskAccepted) && !_depBusy;
        var sp = new StackPanel { Spacing = 6, Children = {
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = {
                new TextBlock { Name = "oss-installer-ampel", Text = dot, Foreground = Brush.Parse(col), FontWeight = FontWeight.Bold, FontSize = 13 },
                new TextBlock { Text = $"Installer wartet: {p.Repo.FullName} · {p.Check.Title}", Foreground = Text, FontWeight = FontWeight.SemiBold, FontSize = 13.5, TextWrapping = TextWrapping.Wrap } } },
            P(p.Check.Text, Text, 12),
            new SelectableTextBlock { Text = $"Datei: {p.Asset.Name} · SHA-256: {p.Check.Sha256}", Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap } } };
        if (p.Check.NeedsRiskConsent && !p.Check.Blocked)
        {
            var risk = new CheckBox { Name = "oss-installer-risk", IsChecked = _ghRiskAccepted, Content = new TextBlock { Text = "Ich habe verstanden: Trendo kann diesen Installer nicht als vertrauenswürdig bestätigen. Er läuft mit meinen Rechten und ich vertraue dem Projekt trotzdem.", Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap } };
            risk.IsCheckedChanged += (_, _) => { GitHubAcceptInstallerRisk(risk.IsChecked == true); run.IsEnabled = _ghRiskAccepted && !_depBusy; };
            sp.Children.Add(risk);
        }
        sp.Children.Add(new WrapPanel { Children = { run, new Border { Width = 8 }, Act("oss-installer-cancel", "Nicht ausführen (löschen)", () => { GitHubCancelInstaller(); Show("oss"); }) } });
        return new Border { Name = "oss-installer-card", Background = Brush.Parse(bg), BorderBrush = Brush.Parse(col), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 10), Child = sp };
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
