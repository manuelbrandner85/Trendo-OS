using Avalonia.Controls;
using Avalonia.Layout;
using Trendo.Core;

namespace Trendo.App;

/// <summary>T-0203: externe Programme direkt mit der aktuellen Datei öffnen; fehlt eins, nach einer Rückfrage einrichten (bestehende Programmprüfung/Installation).</summary>
public partial class MainWindow
{
    /// <summary>Für Tests: Programmpfad statt echter Suche (null = nicht installiert).</summary>
    public Func<string, string?>? AppPathOverride { get; set; }
    private readonly HashSet<string> _appAsk = [];
    /// <summary>Für Tests: OBS-Szenenordner.</summary>
    public string? ObsDirOverride { get; set; }

    public string? AppPath(string appId)
    {
        if (AppPathOverride is { } o) return o(appId);
        var a = ExternalApps.Get(appId);
        if (appId == "unreal") { var e = GameStudio.FindEngines().FirstOrDefault(); if (e is not null && File.Exists(GameStudio.EditorExe(e))) return GameStudio.EditorExe(e); }
        if (ToolPath(a.ComponentId) is { } p && File.Exists(p)) return p;
        return a.Exe.Select(Cli.Find).FirstOrDefault(x => x is not null);
    }

    /// <summary>Öffnet <paramref name="file"/> im Programm; Rückgabe ist die Meldung für den Nutzer.</summary>
    public string OpenInApp(string appId, string? file)
    {
        var a = ExternalApps.Get(appId);
        if (AppPath(appId) is not { } exe) return $"{a.Name} ist nicht eingerichtet.";
        try
        {
            var args = ExternalApps.Plan(a, file ?? "", appId == "obs" ? ObsDirOverride ?? ExternalApps.ObsScenesDir() : null);
            _core.Integrations.LaunchArgs(Principal.LocalOwner, exe, _core.Audit, args);
            return $"{a.Name} öffnet „{Path.GetFileName((file ?? "").TrimEnd('/', '\\'))}“.";
        }
        catch (Exception e) when (e is ArgumentException or IOException or InvalidOperationException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { return "⚠ " + e.Message; }
    }

    /// <summary>Knopf „In … öffnen“ bzw. „… einrichten“ mit einer Rückfrage. Nach dem Einrichten ist der Öffnen-Knopf sofort da (Seite wird neu aufgebaut, kein Neustart).</summary>
    private Control OpenInButton(string appId, Func<string?> file, string page, string suffix = "")
    {
        var a = ExternalApps.Get(appId); var id = $"{appId}-{page}{suffix}";
        if (AppPath(appId) is not null)
            return Act($"open-{id}", $"In {a.Name} öffnen", () => { string m; try { m = OpenInApp(appId, file()); } catch (Exception e) when (e is InvalidOperationException or IOException) { m = "⚠ " + e.Message; } Say(m, page); });
        if (!_appAsk.Contains(id))
            return Act($"setup-{id}", $"{a.Name} einrichten …", () => { _appAsk.Add(id); Show(page); });
        var c = DependencyCatalog.All.FirstOrDefault(x => x.Id == a.ComponentId);
        var guided = c is null || c.Mode == DeliveryMode.Guided;
        var q = new TextBlock { Name = $"ask-{id}", Text = guided ? $"{a.Name} fehlt. Herstellerseite öffnen, um es zu installieren?" : $"{a.Name} fehlt. Jetzt einrichten?{(c!.ApproxMb > 0 ? $" (ca. {c.ApproxMb} MB, Lizenz {c.License})" : "")}", Foreground = Text, TextWrapping = Avalonia.Media.TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 380 };
        var yes = Act($"setup-yes-{id}", guided ? "Ja, Seite öffnen" : "Ja, einrichten", async () =>
        {
            _appAsk.Remove(id);
            if (guided) { if (c?.OfficialUrl is { } u) _core.Integrations.LaunchDirect(Principal.LocalOwner, u, _core.Audit); Say($"{a.Name}: Herstellerseite geöffnet. Nach der Installation erscheint hier „In {a.Name} öffnen“.", page); return; }
            var req = _core.Approvals.Request(Principal.LocalOwner, "deps.install", $"{a.Name} einrichten", c!.Id);
            _core.Approvals.Approve(req.Id, Principal.LocalOwner);
            var ok = InstallOverride is { } fake ? await fake(c) : await InstallOne(c, refresh: false);
            Say(ok && AppPath(appId) is not null ? $"{a.Name} ist eingerichtet – „In {a.Name} öffnen“ geht jetzt." : $"{a.Name} konnte nicht eingerichtet werden. Unter „Programme“ kannst du es erneut versuchen.", page);
        }, true);
        var no = Act($"setup-no-{id}", "Abbrechen", () => { _appAsk.Remove(id); _core.Audit.Append(Principal.LocalOwner.Name, "deps.declined", c?.Id ?? appId); Show(page); });
        return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { q, yes, no } };
    }

    /// <summary>Für Tests: Installation ersetzen.</summary>
    public Func<Component, Task<bool>>? InstallOverride { get; set; }

    private FachprogrammAdapter? _fach;
    private readonly Dictionary<string, FachprogrammBericht> _fachStand = new();

    private FachprogrammAdapter FachAdapter()
    {
        if (_fach is not null) return _fach;
        _fach = new FachprogrammAdapter(_deps, _core.Approvals, _core.Secrets, _core.Audit, new FachLauncher(_core.Integrations, _core.Audit), _core.Offline)
        {
            PfadSuche = p => ExternalApps.All.Any(a => a.Id == p.Id) ? AppPath(p.Id) : null
        };
        return _fach;
    }

    private static bool KannOeffnen(FachprogrammBericht? bericht) =>
        bericht is { Angebunden: true, Pfad: not null } && bericht.Stand is FachprogrammStand.Erreichbar or FachprogrammStand.FreigabeNoetig;

    // ---------- Zentrale Liste ----------
    private Control ProgExternView()
    {
        const string V = "prog-extern";
        var sp = new StackPanel { Spacing = 8 };
        sp.Children.Add(FachAbschnitt(V));
        sp.Children.Add(P("Alle Programme, die Trendo-Studios von außen nutzen. Erkannte Programme kannst du hier starten; fehlende einrichten (Trendo fragt vorher).", Text));
        foreach (var a in ExternalApps.All)
        {
            var path = AppPath(a.Id);
            var line = new TextBlock { Name = $"prog-state-{a.Id}", Text = path is not null ? $"✓ {a.Name} – {a.Use}\n{path}" : $"✗ {a.Name} – {a.Use} – nicht gefunden", Foreground = Text, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
            Control act = path is not null
                ? Act($"prog-run-{a.Id}", "Starten", () => { _core.Integrations.LaunchArgs(Principal.LocalOwner, path, _core.Audit, []); Say($"{a.Name} startet.", V); })
                : OpenInButton(a.Id, () => null, V);
            sp.Children.Add(Card(Row(line, act)));
        }
        return ProgPage("Externe Programme", sp);
    }

    /// <summary>Paket 44: nichts vorinstalliert, nichts im Hintergrund. Die Prüfung startet erst auf Knopfdruck.</summary>
    private Control FachAbschnitt(string view)
    {
        var sp = new StackPanel { Spacing = 8 };
        sp.Children.Add(new TextBlock
        {
            Name = "fach-hinweis",
            Text = "Diese Programme sind nicht vorinstalliert. Trendo lädt sie nicht im Hintergrund. Prüfe die Verbindung erst, wenn du sie brauchst.",
            Foreground = Text,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        });
        foreach (var f in FachprogrammKatalog.Alle)
        {
            _fachStand.TryGetValue(f.Id, out var stand);
            var text = new TextBlock
            {
                Name = "fach-stand-" + f.Id,
                Text = stand is null ? $"{f.Name}: Nicht geladen. Es läuft nichts im Hintergrund." : $"{f.Name}: {stand.Hinweis}",
                Foreground = Text,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            buttons.Children.Add(Act("fach-probe-" + f.Id, "Verbindung prüfen", async () =>
            {
                var b = await FachAdapter().ProbeAsync(f.Id);
                if (b.Stand == FachprogrammStand.Erreichbar) b = FachAdapter().Anbinden(f.Id);
                _fachStand[f.Id] = b;
                Say(b.Hinweis, view);
            }));
            if (KannOeffnen(stand))
                buttons.Children.Add(Act("fach-start-" + f.Id, "Jetzt öffnen", () =>
                {
                    var req = _core.Approvals.Request(Principal.LocalOwner, FachprogrammAdapter.AufrufAktion, f.Name + " öffnen", f.Id);
                    _core.Approvals.Approve(req.Id, Principal.LocalOwner);
                    var b = FachAdapter().Aufrufen(req.Id, f.Id, []);
                    _fachStand[f.Id] = b.Angebunden ? b : b with { Angebunden = stand!.Angebunden };
                    Say(b.Hinweis, view);
                }));
            var box = new StackPanel { Spacing = 6 };
            box.Children.Add(text);
            box.Children.Add(buttons);
            sp.Children.Add(Card(box));
        }
        return sp;
    }
}

/// <summary>Öffnet ein Fachprogramm über die vorhandene Freigabe-Strecke. Hält keinen Prozess fest.</summary>
sealed class FachLauncher(Integrations integrations, AuditLog audit) : IProcessLauncher
{
    public void Open(string target) => integrations.LaunchDirect(Principal.LocalOwner, target, audit);
    public void OpenWithArgs(string exe, IReadOnlyList<string> args) => integrations.LaunchArgs(Principal.LocalOwner, exe, audit, args);
}
