using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;

namespace Trendo.App;

/// <summary>X-02: Trendo prüft sich beim Start selbst und repariert sichere Fälle ohne Nachfrage.
/// T-0763: dazu Programmdateien gegen das Installationspaket prüfen und ersetzen, fehlende Zusatzprogramme an dieser Stelle nachholen
/// (vorhandener Nachlade-Weg mit eigener Rückfrage) und beiseitegelegte Dateien zurückholen.</summary>
public partial class MainWindow
{
    public IReadOnlyList<HealResult> LastHeal { get; private set; } = [];
    public Task? StartupHeal { get; private set; }

    private void InitX02()
    {
        _showMap["heilung"] = HealView; SysTabs.Add(("heilung", "Selbstheilung"));
        StudioPages["X-02"] = "heilung";
        ExtraPages.Add(("Selbstheilung", "heilung", ["reparieren", "kaputt", "fehler", "heilen", "selbst", "installation", "programmdateien", "quarantäne", "zurückholen"]));
        var root = _core.DataRoot;
        StartupHeal = Task.Run(() => { var r = new SelfHealer(root, _core.Audit).Run(true, DateTime.Now); _ui.Post(() => LastHeal = r); });
    }

    // ---------- T-0763: Zustand der Zusatzbereiche ----------
    InstallIntegrity.Report? _healInstall; bool _healInstallBusy, _healRepairAsk; string _healMsg = "";
    List<(Component C, ToolCheck R)>? _healProgs; bool _healProgsBusy; string? _healRestoreAsk;
    /// <summary>Für Tests: die laufende Prüfung der Programmdateien bzw. Zusatzprogramme.</summary>
    public Task? HealInstallTask { get; private set; }
    public Task? HealProgramsTask { get; private set; }
    public InstallIntegrity.Report? HealInstallReport => _healInstall;
    public string HealMessage => _healMsg;

    void HealSay(string m) { _healMsg = m; if (CurrentView == "heilung") Show("heilung"); }

    /// <summary>Programmdateien gegen das Installationspaket prüfen (im Hintergrund).</summary>
    public Task HealCheckInstall()
    {
        if (HealInstallTask is { IsCompleted: false } t) return t;
        _healInstallBusy = true; _healRepairAsk = false; var dir = InstallDir;
        return HealInstallTask = Task.Run(() =>
        {
            var r = InstallIntegrity.Check(dir);
            _ui.Post(() => { _healInstall = r; _healInstallBusy = false; if (CurrentView == "heilung") Show("heilung"); });
        });
    }

    /// <summary>Fehlende oder veränderte Programmdateien aus dem Installationspaket ersetzen (nach Rückfrage auf der Seite).</summary>
    public async Task HealRepairInstall()
    {
        if (_healInstall is not { Package: { } pkg } r || r.Findings.Count == 0) return;
        if (!DarfSoftware("heal.repair.install", "heilung")) { _healRepairAsk = false; HealSay(Rbac.NurErwachseneSoftware); return; } // T-0768: Kinderschutz (T-0764) – Programmdateien ersetzt nur ein Erwachsener
        _healRepairAsk = false; _healInstallBusy = true; HealSay("Programmdateien werden aus dem Installationspaket ersetzt …");
        var dir = InstallDir;
        var (ok, bad) = await Task.Run(() => InstallIntegrity.Repair(dir, pkg, r.Findings.Select(f => f.File), _core.Audit));
        var after = await Task.Run(() => InstallIntegrity.Check(dir));
        _healInstall = after; _healInstallBusy = false;
        HealSay(bad.Count == 0 ? $"✓ {ok.Count} Programmdatei(en) aus dem Installationspaket ersetzt."
            : $"{ok.Count} ersetzt, {bad.Count} nicht ({string.Join(", ", bad.Take(5))}{(bad.Count > 5 ? " …" : "")}) – Trendo benutzt sie gerade. Nächster Schritt: Trendo schließen und das Setup erneut ausführen.");
    }

    /// <summary>Zusatzprogramme der gewählten Pakete prüfen (startet sie mit --version, im Hintergrund).</summary>
    public Task HealCheckPrograms()
    {
        if (HealProgramsTask is { IsCompleted: false } t) return t;
        _healProgsBusy = true;
        var comps = SetupPacks.ComponentsFor(_settings.StudioPacks.Count > 0 ? _settings.StudioPacks : _wizPacks).ToList();
        return HealProgramsTask = Task.Run(async () =>
        {
            var checks = (await Task.WhenAll(comps.Select(async c => (C: c, R: await _deps.CheckAsync(c))))).ToList();
            _ui.Post(() => { _healProgs = checks; _healProgsBusy = false; if (CurrentView == "heilung") Show("heilung"); });
        });
    }

    /// <summary>Ein fehlendes Zusatzprogramm nachholen – über den vorhandenen Weg (Sammel-Rückfrage, dann genau diese Freigabe einlösen).</summary>
    public async Task HealFetchProgram(Component c)
    {
        HealSay($"{c.Name}: Trendo fragt dich gleich, ob es eingerichtet werden darf …");
        var msg = await RepairComponentAsync(c, "nachholen", "nachgeholt");
        _healProgs = null; ReloadStudios(); HealSay(msg);
        _ = HealCheckPrograms();
    }

    /// <summary>Beiseitegelegte Datei zurückholen (nach Rückfrage auf der Seite).</summary>
    public void HealRestore(SelfHealer.QuarantineItem item)
    {
        _healRestoreAsk = null;
        if (!DarfSoftware("heal.restore " + Path.GetFileName(item.File), "heilung")) { HealSay(Rbac.NurErwachseneSoftware); return; } // T-0768: Kinderschutz (T-0764) – wie „Sicherung wiederherstellen“ nur für Erwachsene
        try { HealSay("✓ " + SelfHealer.Restore(_core.DataRoot, item, _core.Audit, DateTime.Now)); }
        catch (Exception e) when (e is InvalidOperationException or IOException or UnauthorizedAccessException) { HealSay(e.Message); }
    }

    private Control HealView()
    {
        var sp = new StackPanel { Spacing = 6, Children = { P("Bei jedem Start prüft Trendo sich selbst und repariert, was sicher geht – ohne dich zu fragen. Was du entscheiden musst, steht hier.") } };
        var last = SelfHealer.Last(_core.DataRoot);
        var res = LastHeal.Count > 0 ? LastHeal : last?.Results ?? [];
        sp.Children.Add(new TextBlock { Name = "heal-summary", Foreground = Text, FontSize = 14, Text = res.Count == 0 ? "Noch nicht geprüft." :
            res.All(r => r.State == HealState.Ok) ? "✓ Alles in Ordnung." : $"{res.Count(r => r.State == HealState.Repaired)} repariert · {res.Count(r => r.State == HealState.NeedsYou)} brauchen dich" });
        if (last is { } l) sp.Children.Add(P($"Zuletzt geprüft: {l.At:dd.MM.yyyy HH:mm}", null, 11));
        if (_healMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "heal-msg", Text = _healMsg, Foreground = _healMsg.StartsWith('✓') ? Brush.Parse("#4ADE80") : Brush.Parse("#FACC15"), TextWrapping = TextWrapping.Wrap, FontSize = 12.5 });
        foreach (var r in res)
            sp.Children.Add(WCard(new StackPanel { Children = {
                new TextBlock { Text = r.Title, Foreground = Brush.Parse(r.State switch { HealState.Ok => "#4ADE80", HealState.Repaired => "#D4A84B", HealState.NeedsYou => "#FACC15", _ => "#ECEDEF" }), FontSize = 13 },
                P(r.Detail, r.State == HealState.NeedsYou ? Brush.Parse("#FACC15") : null, 11) } }));
        var go = new Button { Name = "heal-run", Content = "Jetzt prüfen und reparieren", Background = Accent };
        go.Click += async (_, _) => { go.IsEnabled = false; var root = _core.DataRoot; LastHeal = await Task.Run(() => new SelfHealer(root, _core.Audit).Run(true, DateTime.Now)); Show("heilung"); };
        sp.Children.Add(go);
        sp.Children.Add(HealInstallCard());
        sp.Children.Add(HealProgramsCard());
        sp.Children.Add(HealQuarantineCard());
        return SysPage("Selbstheilung", sp);
    }

    Control HealInstallCard()
    {
        var body = new StackPanel { Name = "heal-install", Spacing = 5, Children = {
            new TextBlock { Text = "Programmdateien", Foreground = Text, FontSize = 14, FontWeight = FontWeight.SemiBold },
            P("Vergleicht jede Programmdatei mit dem Installationspaket, das bei der Installation neben Trendo abgelegt wurde (Größe und Prüfsumme). Fehlende oder veränderte Dateien lassen sich daraus ersetzen. " + InstallIntegrity.Limit, null, 11) } };
        if (_healInstall is null && HealInstallTask is null) _ = HealCheckInstall(); // T-0763 Fix N6: beim Öffnen gleich ein Ergebnis
        if (_healInstall is null) body.Children.Add(new TextBlock { Name = "heal-install-busy", Text = "Programmdateien werden geprüft …", Foreground = Muted, FontSize = 12 });
        var check = new Button { Name = "heal-install-check", Content = _healInstallBusy ? "Prüft …" : "Programmdateien prüfen", IsEnabled = !_healInstallBusy, FontSize = 12 };
        check.Click += (_, _) => { _ = HealCheckInstall(); Show("heilung"); };
        var row = new WrapPanel { Children = { check } };
        if (_healInstall is { } r)
        {
            var color = r.Ok ? "#4ADE80" : !r.Installed ? "#93A4C3" : "#FACC15";
            body.Children.Add(new TextBlock { Name = "heal-install-result", Text = (r.Ok ? "✓ " : "") + r.Note, Foreground = Brush.Parse(color), FontSize = 12, TextWrapping = TextWrapping.Wrap });
            if (r.Links.Count > 0) body.Children.Add(new TextBlock { Name = "heal-install-links", Foreground = Brush.Parse("#FACC15"), FontSize = 12, TextWrapping = TextWrapping.Wrap, // T-0763 Fix F4
                Text = $"Hinweis: {r.Links.Count} Verknüpfung(en) (Junction/Link) im Programmordner: {string.Join(", ", r.Links.Take(8))}{(r.Links.Count > 8 ? " …" : "")}. Darüber ersetzt Trendo nichts. Hast du sie nicht selbst angelegt, führe das Setup erneut aus." });
            if (r.Extra.Count > 0) body.Children.Add(new TextBlock { Name = "heal-install-extra", Foreground = Brush.Parse("#FACC15"), FontSize = 12, TextWrapping = TextWrapping.Wrap,
                Text = $"Hinweis: {r.Extra.Count} Datei(en) im Programmordner sind nicht aus dem Paket: {string.Join(", ", r.Extra.Take(8))}{(r.Extra.Count > 8 ? " …" : "")}. Hast du sie nicht selbst abgelegt, führe das Setup erneut aus." });
            if (!r.Installed) body.Children.Add(P("Nur für Entwickler: Ohne Installation gibt es kein Paket zum Vergleichen.", Muted, 11));
            else if (r.Package is null) body.Children.Add(new TextBlock { Name = "heal-install-next", Text = "Nächster Schritt: Trendo-Setup erneut ausführen – es legt alle Programmdateien und das Paket neu an. Deine Daten bleiben erhalten.", Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap });
            else if (r.Findings.Count > 0)
            {
                body.Children.Add(P(string.Join("\n", r.Findings.Take(12).Select(f => $"• {f.File} – {f.Problem}")) + (r.Findings.Count > 12 ? $"\n… und {r.Findings.Count - 12} weitere" : ""), Text, 11.5));
                var fix = new Button { Name = "heal-install-repair", Content = _healRepairAsk ? $"Wirklich {r.Findings.Count} Datei(en) ersetzen?" : "Aus dem Installationspaket ersetzen", FontSize = 12, IsEnabled = !_healInstallBusy };
                fix.Click += async (_, _) => { if (!_healRepairAsk) { _healRepairAsk = true; Show("heilung"); return; } await HealRepairInstall(); };
                row.Children.Add(fix);
                if (_healRepairAsk) { var no = new Button { Name = "heal-install-cancel", Content = "Abbrechen", FontSize = 12, Margin = new Thickness(6, 0, 0, 0) }; no.Click += (_, _) => { _healRepairAsk = false; Show("heilung"); }; row.Children.Add(no); }
            }
        }
        body.Children.Add(row);
        return WCard(body);
    }

    Control HealProgramsCard()
    {
        var body = new StackPanel { Name = "heal-programs", Spacing = 5, Children = {
            new TextBlock { Text = "Zusatzprogramme", Foreground = Text, FontSize = 14, FontWeight = FontWeight.SemiBold },
            P("Prüft die Programme, die deine gewählten Studios brauchen. Fehlt eines, das Trendo selbst einrichten darf, holst du es hier nach – Trendo zeigt vorher Lizenz, Größe und Herkunft und fragt dich. Bei den anderen steht, dass du sie selbst installierst.", null, 11) } };
        if (_healProgs is null && HealProgramsTask is null) _ = HealCheckPrograms(); // beim ersten Öffnen gleich nachsehen, was fehlt
        var check = new Button { Name = "heal-programs-check", Content = _healProgsBusy ? "Prüft …" : "Zusatzprogramme prüfen", IsEnabled = !_healProgsBusy, FontSize = 12 };
        check.Click += (_, _) => { _ = HealCheckPrograms(); Show("heilung"); };
        if (_healProgs is { } list)
        {
            var open = list.Where(x => x.R.State is ToolState.Missing or ToolState.Broken).ToList();
            body.Children.Add(new TextBlock { Name = "heal-programs-result", FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = open.Count == 0 ? Brush.Parse("#4ADE80") : Brush.Parse("#FACC15"),
                Text = list.Count == 0 ? "✓ Deine Auswahl braucht keine Zusatzprogramme." : open.Count == 0 ? $"✓ Alle {list.Count} Zusatzprogramme bereit." : $"{open.Count} von {list.Count} fehlen oder starten nicht." });
            foreach (var (c, ch) in open)
            {
                var comp = c;
                var can = c.Mode is DeliveryMode.AutoUser or DeliveryMode.AutoElevated or DeliveryMode.Included;
                Control act = can ? Act("heal-fetch-" + c.Id, ch.State == ToolState.Broken ? "Neu einrichten" : "Nachholen", () => _ = HealFetchProgram(comp))
                    : new TextBlock { Text = "Bitte selbst installieren – Trendo darf das nicht automatisch.", Foreground = Muted, FontSize = 11, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
                if (act is Button b) b.IsEnabled = !_depBusy;
                body.Children.Add(Row(new TextBlock { Name = "heal-prog-" + c.Id, Text = $"{c.Name} – {(ch.State == ToolState.Broken ? "startet nicht" : "fehlt")}", Foreground = Text, FontSize = 12, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center }, act));
            }
        }
        body.Children.Add(new WrapPanel { Children = { check } });
        return WCard(body);
    }

    Control HealQuarantineCard()
    {
        var items = SelfHealer.Quarantined(_core.DataRoot);
        var body = new StackPanel { Name = "heal-quarantine", Spacing = 5, Children = {
            new TextBlock { Text = "Beiseitegelegte Dateien", Foreground = Text, FontSize = 14, FontWeight = FontWeight.SemiBold },
            P("Kaputte Einstellungsdateien löscht Trendo nie, sondern legt sie in den Ordner „quarantaene“. Hier holst du sie zurück – die Datei, die inzwischen an ihrem Platz liegt, bleibt als Kopie daneben.", null, 11) } };
        if (items.Count == 0) body.Children.Add(new TextBlock { Name = "heal-quarantine-empty", Text = "Keine beiseitegelegten Dateien.", Foreground = Muted, FontSize = 12 });
        var i = 0;
        foreach (var it in items.Take(30))
        {
            var item = it; var key = it.File; var n = i++;
            var at = DateTime.TryParseExact(it.Stamp, "yyyyMMdd-HHmmss", null, System.Globalization.DateTimeStyles.None, out var d) ? d.ToString("dd.MM.yyyy HH:mm") : it.Stamp;
            Control act;
            if (it.Original is null) act = new TextBlock { Text = "Alter Ort unbekannt – Datei liegt im Ordner quarantaene", Foreground = Muted, FontSize = 11, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            else if (_healRestoreAsk == key)
            {
                var yes = Act($"heal-restore-{n}", "Ja, zurückholen", () => HealRestore(item));
                var no = Act($"heal-restore-no-{n}", "Abbrechen", () => { _healRestoreAsk = null; Show("heilung"); });
                act = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6, Children = { yes, no } };
            }
            else act = Act($"heal-restore-{n}", "Zurückholen", () => { _healRestoreAsk = key; Show("heilung"); });
            body.Children.Add(Row(new TextBlock { Name = $"heal-q-{n}", Text = $"{it.Original ?? Path.GetFileName(it.File)} · beiseitegelegt am {at}", Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center }, act));
        }
        if (items.Count > 30) body.Children.Add(P($"… und {items.Count - 30} weitere im Ordner quarantaene.", Muted, 11));
        return WCard(body);
    }
}
