using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Trendo.Core;

namespace Trendo.App;

/// <summary>T-0203 und Paket 44: externe Programme. „Starten“ und „Einrichten“ prüfen zuerst die Verbindung. Ohne bestandene Prüfung wird nichts geöffnet und nichts heruntergeladen. Spiele und 3D bleiben beim alten Weg.</summary>
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

    /// <summary>Knopf „In … öffnen“ bzw. „… einrichten“. Spiele und 3D bleiben beim alten Weg, bis deren Aufträge gemergt sind.</summary>
    private Control OpenInButton(string appId, Func<string?> file, string page, string suffix = "") =>
        page is "spiele" or "3d" ? OpenInButtonDirekt(appId, file, page, suffix) : OpenInButtonGeprueft(appId, file, page, suffix);

    /// <summary>Alter Weg ohne die neue Prüfung. Nur Spiele und 3D.</summary>
    private Control OpenInButtonDirekt(string appId, Func<string?> file, string page, string suffix = "")
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

    /// <summary>Prüfung zuerst. Erst danach die einmalige Freigabe. Ohne bestandene Prüfung kein Start und kein Download.</summary>
    private Control OpenInButtonGeprueft(string appId, Func<string?> file, string page, string suffix = "")
    {
        var a = ExternalApps.Get(appId); var id = $"{appId}-{page}{suffix}";
        if (AppPath(appId) is not null)
            return Act($"open-{id}", $"In {a.Name} öffnen", async () =>
            {
                var b = await PruefenUndMerken(appId);
                if (b.Stand != FachprogrammStand.Erreichbar) { Say(Gesperrt(b.Hinweis), page); return; }
                IReadOnlyList<string> args;
                try
                {
                    var datei = file();
                    args = string.IsNullOrWhiteSpace(datei) ? [] : ExternalApps.Plan(a, datei, appId == "obs" ? ObsDirOverride ?? ExternalApps.ObsScenesDir() : null);
                }
                catch (Exception e) when (e is ArgumentException or IOException or InvalidOperationException or UnauthorizedAccessException)
                {
                    Say("⚠ " + e.Message, page);
                    return;
                }
                Say(MitFreigabeOeffnen(appId, a.Name, args).Hinweis, page);
            });
        if (!_appAsk.Contains(id))
            return Act($"setup-{id}", $"{a.Name} einrichten …", () => { _appAsk.Add(id); Show(page); });
        var c = DependencyCatalog.All.FirstOrDefault(x => x.Id == a.ComponentId);
        var q = new TextBlock { Name = $"ask-{id}", Text = $"{a.Name} fehlt. Einrichten geht erst, wenn die Prüfung bestanden ist. Ohne bestandene Prüfung wird nichts geöffnet und nichts heruntergeladen.", Foreground = Text, TextWrapping = Avalonia.Media.TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 380 };
        var yes = Act($"setup-yes-{id}", "Ja, einrichten", async () =>
        {
            _appAsk.Remove(id);
            var b = await PruefenUndMerken(appId);
            Say(b.Stand == FachprogrammStand.Erreichbar ? "Schon vorhanden. Es wird nichts heruntergeladen." : Gesperrt(b.Hinweis), page);
        }, true);
        var no = Act($"setup-no-{id}", "Abbrechen", () => { _appAsk.Remove(id); _core.Audit.Append(Principal.LocalOwner.Name, "deps.declined", c?.Id ?? appId); Show(page); });
        return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { q, yes, no } };
    }

    private static string Gesperrt(string hinweis) => hinweis + " Nichts geöffnet, nichts heruntergeladen.";

    private async Task<FachprogrammBericht> PruefenUndMerken(string id)
    {
        var b = await FachAdapter().ProbeAsync(id);
        if (b.Stand == FachprogrammStand.Erreichbar) b = FachAdapter().Anbinden(id);
        _fachStand[id] = b;
        return b;
    }

    /// <summary>Einmalige Freigabe, dann öffnen. Die Freigabe gilt nur für diesen einen Aufruf.</summary>
    private FachprogrammBericht MitFreigabeOeffnen(string id, string name, IReadOnlyList<string> args)
    {
        var req = _core.Approvals.Request(Principal.LocalOwner, FachprogrammAdapter.AufrufAktion, name + " öffnen", id);
        _core.Approvals.Approve(req.Id, Principal.LocalOwner);
        var b = FachAdapter().Aufrufen(req.Id, id, args);
        _fachStand[id] = b.Angebunden ? b : (_fachStand.TryGetValue(id, out var alt) ? b with { Angebunden = alt.Angebunden } : b);
        return b;
    }

    /// <summary>Für Tests: Installation ersetzen. Der neue Installieren-Knopf nutzt das nicht.</summary>
    public Func<Component, Task<bool>>? InstallOverride { get; set; }

    /// <summary>Aus, bis ein Installer eingetragen ist. Nichts lädt beim Start.</summary>
    public bool InstallierenSichtbar { get; set; }

    /// <summary>Leer heißt: kein Baustein, kein Knopf. Trendo setzt hier keinen echten Installer ein.</summary>
    public IProgramInstaller? ProgramInstaller { get; set; }

    private readonly Dictionary<string, InstallMaske> _installMaske = new();

    private sealed class InstallMaske
    {
        public required ProgramOffer Angebot { get; init; }
        public bool NurHinweis { get; init; }
        public bool Laeuft { get; set; }
        public string Stand { get; set; } = "";
        public CancellationTokenSource? Abbruch { get; set; }
    }

    private static bool InstallKnopfAn(bool sichtbar, IProgramInstaller? installer) => sichtbar && installer is not null;

    private static string GroesseText(ProgramOffer angebot)
    {
        if (!string.IsNullOrWhiteSpace(angebot.Groesse)) return angebot.Groesse!;
        if (angebot.SizeBytes is long n and >= 0)
        {
            if (n >= 1_000_000) return $"{n / 1_000_000.0:0.#} MB";
            if (n >= 1_000) return $"{n / 1_000.0:0.#} KB";
            return n + " Bytes";
        }
        return "Größe unbekannt";
    }

    private static string FrageText(ProgramOffer angebot) =>
        $"{angebot.Name} kommt von {angebot.Quelle}, Größe {GroesseText(angebot)}, Lizenz {angebot.Lizenz ?? "nicht genannt"}. Jetzt installieren?";

    private static string HinweisText(ProgramOffer angebot)
    {
        var link = string.IsNullOrWhiteSpace(angebot.InfoLink) ? "" : " Link: " + angebot.InfoLink;
        return $"{angebot.Name} wird nicht automatisch installiert. {angebot.Hinweis}{link}";
    }

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
                ? Act($"prog-run-{a.Id}", "Starten", async () =>
                {
                    var b = await PruefenUndMerken(a.Id);
                    if (b.Stand != FachprogrammStand.Erreichbar) { Say(Gesperrt(b.Hinweis), V); return; }
                    Say(MitFreigabeOeffnen(a.Id, a.Name, []).Hinweis, V);
                })
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
            if (InstallKnopfAn(InstallierenSichtbar, ProgramInstaller) && !KannOeffnen(stand))
            {
                if (_installMaske.TryGetValue(f.Id, out var maske)) box.Children.Add(InstallMaskeAnzeigen(f, maske, view));
                else buttons.Children.Add(Act("fach-inst-" + f.Id, "Installieren", async () => await InstallierenFragen(f, view)));
            }
            sp.Children.Add(Card(box));
        }
        return sp;
    }

    /// <summary>Liest nur das Angebot. Installiert nichts und läuft nicht von selbst.</summary>
    private async Task InstallierenFragen(Fachprogramm programm, string view)
    {
        var installer = ProgramInstaller;
        if (!InstallKnopfAn(InstallierenSichtbar, installer)) return;
        ProgramOffer? angebot;
        try { angebot = await installer!.GetOfferAsync(programm.Id, CancellationToken.None); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Say("Das Angebot konnte nicht gelesen werden. Es wurde nichts geladen.", view);
            return;
        }
        if (angebot is null)
        {
            Say("Dafür gibt es kein Angebot. Es wurde nichts geladen.", view);
            return;
        }
        _installMaske[programm.Id] = new InstallMaske
        {
            Angebot = angebot,
            NurHinweis = !angebot.IsFree || angebot.NeedsUserStep
        };
        Show(view);
    }

    private Control InstallMaskeAnzeigen(Fachprogramm programm, InstallMaske maske, string view)
    {
        if (maske.NurHinweis) return InstallHinweis(programm, maske, view);
        if (maske.Laeuft) return InstallLaeuft(programm, maske);
        var zeile = new TextBlock { Name = "fach-inst-text-" + programm.Id, Text = FrageText(maske.Angebot), Foreground = Text, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        var ja = Act("fach-inst-yes-" + programm.Id, "Ja, installieren", async () => await InstallierenJa(programm, maske, view), true);
        var nein = Act("fach-inst-no-" + programm.Id, "Abbrechen", () => { _installMaske.Remove(programm.Id); Show(view); });
        return new StackPanel { Spacing = 6, Children = { zeile, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { ja, nein } } } };
    }

    private Control InstallHinweis(Fachprogramm programm, InstallMaske maske, string view)
    {
        var zeile = new TextBlock { Name = "fach-inst-text-" + programm.Id, Text = HinweisText(maske.Angebot), Foreground = Text, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        var knoepfe = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        if (!string.IsNullOrWhiteSpace(maske.Angebot.InfoLink))
            knoepfe.Children.Add(Act("fach-inst-link-" + programm.Id, "Seite ansehen", () =>
            {
                _core.Integrations.LaunchDirect(Principal.LocalOwner, maske.Angebot.InfoLink!, _core.Audit);
                Say("Die Seite ist offen. Installiert wurde nichts.", view);
            }));
        knoepfe.Children.Add(Act("fach-inst-no-" + programm.Id, "Schließen", () => { _installMaske.Remove(programm.Id); Show(view); }));
        return new StackPanel { Spacing = 6, Children = { zeile, knoepfe } };
    }

    private Control InstallLaeuft(Fachprogramm programm, InstallMaske maske)
    {
        var zeile = new TextBlock { Name = "fach-inst-stand-" + programm.Id, Text = maske.Stand, Foreground = Text, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        var stop = Act("fach-inst-stop-" + programm.Id, "Abbrechen", () => maske.Abbruch?.Cancel());
        return new StackPanel { Spacing = 6, Children = { zeile, stop } };
    }

    /// <summary>Freigabe, dann nur der eingetragene Installer. Kein eigener Download.</summary>
    private async Task InstallierenJa(Fachprogramm programm, InstallMaske maske, string view)
    {
        var installer = ProgramInstaller;
        if (!InstallKnopfAn(InstallierenSichtbar, installer) || maske.Laeuft) return;
        var cts = new CancellationTokenSource();
        maske.Abbruch = cts;
        maske.Laeuft = true;
        maske.Stand = "Wird vorbereitet …";
        Show(view);
        var progress = new Progress<InstallProgress>(p =>
        {
            var text = Math.Clamp(p.Percent, 0, 100) + " Prozent. " + p.Meldung;
            maske.Stand = text;
            Dispatcher.UIThread.Post(() =>
            {
                var tb = this.GetLogicalDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Name == "fach-inst-stand-" + programm.Id);
                if (tb is not null) tb.Text = text;
            });
        });
        try
        {
            var req = _core.Approvals.Request(Principal.LocalOwner, ProgramInstall.Aktion, programm.Name + " installieren", programm.Id);
            _core.Approvals.Approve(req.Id, Principal.LocalOwner);
            var ergebnis = await _core.Approvals.Execute(req.Id, ProgramInstall.Aktion, programm.Id, () => installer!.InstallAsync(programm.Id, progress, cts.Token));
            _installMaske.Remove(programm.Id);
            if (!ergebnis.Success)
            {
                Say(string.IsNullOrWhiteSpace(ergebnis.UserMessageDe) ? "Hat nicht geklappt. Es wurde nichts weiter geladen." : ergebnis.UserMessageDe!, view);
                return;
            }
            if (!await installer!.IsInstalledAsync(programm.Id, CancellationToken.None))
            {
                Say("Fertig gemeldet, aber das Programm ist nicht da. Es wird nichts weiter geladen.", view);
                return;
            }
            var bericht = await PruefenUndMerken(programm.Id);
            Say(bericht.Hinweis, view);
        }
        catch (OperationCanceledException)
        {
            _installMaske.Remove(programm.Id);
            Say("Abgebrochen. Es wurde nichts weiter geladen.", view);
        }
        catch (ApprovalException)
        {
            _installMaske.Remove(programm.Id);
            Say("Ohne Freigabe passiert nichts.", view);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _installMaske.Remove(programm.Id);
            Say("Hat nicht geklappt (" + ex.GetType().Name + "). Es wurde nichts weiter geladen.", view);
        }
    }
}

/// <summary>Öffnet ein Fachprogramm über die vorhandene Freigabe-Strecke. Hält keinen Prozess fest.</summary>
sealed class FachLauncher(Integrations integrations, AuditLog audit) : IProcessLauncher
{
    public void Open(string target) => integrations.LaunchDirect(Principal.LocalOwner, target, audit);
    public void OpenWithArgs(string exe, IReadOnlyList<string> args) => integrations.LaunchArgs(Principal.LocalOwner, exe, audit, args);
}
