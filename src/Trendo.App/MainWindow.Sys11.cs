using Avalonia.Controls;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Sys;

namespace Trendo.App;

/// <summary>SYS-11 Programme & Integration (freigegebener Katalog, 21.10.2026): Pflegen, Starten, Prüfen.</summary>
public partial class MainWindow
{
    public static readonly List<(string Key, string Label, string Module)> ProgTabs = [];
    private string _progOut = ""; private bool _resetAsk; private (string File, bool Ask) _offAsk;
    private Software.AutostartItem? _autoAsk;

    private void InitSys11()
    {
        RegisterTab(ProgTabs, "prog-pflege", "Pflegen", "SYS-11", ProgCareView, "updates", "aktualisieren", "portable", "offline-installer", "deinstallieren", "zurücksetzen", "einstellungen sichern");
        RegisterTab(ProgTabs, "prog-start", "Starten", "SYS-11", ProgStartView, "verknüpfung", "parameter", "autostart", "dateizuordnung", "öffnen mit");
        RegisterTab(ProgTabs, "prog-pruefen", "Prüfen", "SYS-11", ProgCheckView, "systemvoraussetzungen", "kompatibel", "integrationstest", "speicherplatz", "softwareliste", "csv", "wiederherstellen", "winget import", "selbsttest");
        RegisterTab(ProgTabs, "prog-extern", "Externe Programme", "SYS-11", ProgExternView, "blender", "unreal", "vs code", "vscode", "libreoffice", "obs", "externe programme", "öffnen in");
    }
    private Control ProgPage(string title, Control body) => W45Page(title, ProgTabs, "prog", body);
    private string ProgFolder => WorkFolder("Programme");
    private string StartupDir => OperatingSystem.IsWindows() ? Environment.GetFolderPath(Environment.SpecialFolder.Startup) : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "autostart");
    private string StartupOff => Path.Combine(_core.DataRoot, "autostart-aus");
    private TextBlock Out(string name) => new() { Name = name, Text = _progOut, Foreground = Text, TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Consolas,monospace"), FontSize = 11 };

    // ---------- Pflegen ----------
    private Control ProgCareView()
    {
        var sp = new StackPanel { Spacing = 10 }; const string V = "prog-pflege";
        sp.Children.Add(Card(Section("Updates anzeigen", "Trendo schaut nur nach – installiert wird nichts automatisch.",
            Line(Act("upd-check", "Nach Updates schauen", async () =>
            {
                var be = AppCenter.Default(new RealProcessRunner());
                try { var u = await be.UpgradesAsync(); _progOut = Software.UpdateSummary(u) + "\n" + string.Join("\n", u.Select(p => $"• {p.Name}: {p.Version} → {p.Available}")); }
                catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or IOException) { _progOut = "Die Paketverwaltung ist hier nicht verfügbar (" + e.Message + ")."; }
                Say("Update-Prüfung fertig.", V);
            }, true)))));

        var pz = Field("portable-zip", "ZIP-Datei des Programms"); var ps = Field("portable-sha", "Prüfsumme (SHA-256) vom Hersteller", 300); var pn = Field("portable-name", "Name, z. B. ffmpeg", 140);
        sp.Children.Add(Card(Section("Portable Programme", "Entpackt ein Programm ohne Installation in deinen Werkzeuge-Ordner – nur wenn die Prüfsumme stimmt.",
            Line(pz, ps, pn, Act("portable-run", "Prüfen und entpacken", () => Try(V, () =>
            { var d = Software.InstallPortable(Clean(pz), Clean(ps), Path.Combine(_core.DataRoot, "werkzeuge"), Clean(pn), _core.Audit); Say("Entpackt nach " + d, V); }))))));

        var of = Field("offline-folder", "Ordner mit Installationsdateien");
        var ol = new StackPanel { Name = "offline-list", Spacing = 4 };
        sp.Children.Add(Card(Section("Offline-Installer", "Neben jeder Datei muss eine Prüfsumme liegen (datei.sha256 oder SHA256SUMS). Gestartet wird erst nach deiner Bestätigung.",
            Line(of, Act("offline-scan", "Prüfen", () => Try(V, () =>
            {
                ol.Children.Clear();
                foreach (var (p, i) in Software.OfflinePackages(Clean(of)).Select((p, i) => (p, i)))
                {
                    var t = p.ExpectedSha is null ? "keine Prüfsumme gefunden – wird nicht gestartet" : p.Ok ? "Prüfsumme stimmt" : "Prüfsumme FALSCH – Datei nicht verwenden";
                    var row = Line(new TextBlock { Text = $"{Path.GetFileName(p.File)}: {t}", Foreground = p.Ok ? Text : Warn });
                    if (p.Ok) row.Children.Add(Act($"offline-run-{i}", "Starten …", () => { _offAsk = (p.File, true); Say($"Wirklich „{Path.GetFileName(p.File)}“ starten?", V); }));
                    ol.Children.Add(row);
                }
                if (ol.Children.Count == 0) ol.Children.Add(P("Keine Installer (.exe, .msi, .msix, .zip) gefunden."));
            }))), ol)));
        if (_offAsk.Ask) sp.Children.Add(Card(Line(P($"„{Path.GetFileName(_offAsk.File)}“ jetzt starten? Der Installer fragt dann selbst weiter."),
            Act("offline-yes", "Ja, starten", () => { var f = _offAsk.File; _offAsk = default; _core.Integrations.LaunchDirect(Principal.LocalOwner, f, _core.Audit); Say("Gestartet.", V); }, true),
            Act("offline-no", "Abbrechen", () => { _offAsk = default; Say("Nicht gestartet.", V); }))));

        var steps = new StackPanel(); foreach (var (s, i) in Software.UninstallSteps.Select((s, i) => (s, i))) steps.Children.Add(P($"{i + 1}. {s}"));
        sp.Children.Add(Card(Section("Programm entfernen", "Trendo entfernt nichts heimlich. So geht's in Windows:", steps,
            Line(Act("uninstall-open", "Windows-Einstellungen öffnen", () => Try(V, () =>
            { if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("Das geht nur unter Windows."); Launch(Software.UninstallSettings); }))))));

        sp.Children.Add(Card(Section("Trendo-Einstellungen zurücksetzen", "Vorher wird automatisch eine Sicherung angelegt – du kannst sie wiederherstellen.",
            _resetAsk ? Line(P("Wirklich alle Einstellungen zurücksetzen?"), Act("reset-yes", "Ja, zurücksetzen", () => Try(V, () => { _resetAsk = false; var b = Software.ResetSettings(_core.DataRoot, _core.Audit); _settings = new(); Say("Zurückgesetzt. Sicherung: " + Path.GetFileName(b), V); }), true), Act("reset-no", "Abbrechen", () => { _resetAsk = false; Say("Nichts geändert.", V); }))
                      : Line(Act("reset-ask", "Zurücksetzen …", () => { _resetAsk = true; Show(V); })))));
        if (_progOut.Length > 0) sp.Children.Add(Card(Out("prog-out")));
        return ProgPage("Programme pflegen", sp);
    }

    // ---------- Starten ----------
    private Control ProgStartView()
    {
        var sp = new StackPanel { Spacing = 10 }; const string V = "prog-start";
        var sn = Field("sc-name", "Name", 150); var se = Field("sc-exe", "Programmdatei"); var sa = Field("sc-args", "Parameter, z. B. --neu \"C:\\Mein Ordner\"", 260);
        var list = new StackPanel { Spacing = 4 };
        foreach (var (s, i) in Software.Shortcuts(_core.DataRoot).Select((s, i) => (s, i)))
            list.Children.Add(Line(P($"{s.Name}: {Path.GetFileName(s.Exe)} {string.Join(' ', s.Args)}"),
                Act($"sc-run-{i}", "Starten", () => Try(V, () => { _core.Integrations.LaunchDirect(Principal.LocalOwner, s.Exe, _core.Audit, string.Join(' ', s.Args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a))); Say(s.Name + " gestartet.", V); })),
                Act($"sc-del-{i}", "Entfernen", () => { Software.RemoveShortcut(_core.DataRoot, s.Name); Say("Entfernt.", V); })));
        sp.Children.Add(Card(Section("Eigene Verknüpfungen", "Ein Programm mit festen Einstellungen starten – z. B. einen Ordner direkt öffnen.",
            Line(sn, se, sa, Act("sc-add", "Anlegen", () => Try(V, () => { Software.AddShortcut(_core.DataRoot, Clean(sn), Clean(se), sa.Text ?? ""); Say("Verknüpfung angelegt.", V); }), true)), list)));

        var al = new StackPanel { Spacing = 4 };
        foreach (var (a, i) in Software.StartupFolder(StartupDir, StartupOff).Select((a, i) => (a, i)))
            al.Children.Add(Line(P($"{a.Name} ({a.Where}) – {(a.Enabled ? "startet mit" : "ausgeschaltet")}"),
                Act($"auto-toggle-{i}", a.Enabled ? "Ausschalten …" : "Einschalten …", () => { _autoAsk = a; Show(V); })));
        foreach (var r in RunKeys()) al.Children.Add(P($"{r.Name} (Registrierung, nur Anzeige) – {r.Command}"));
        if (al.Children.Count == 0) al.Children.Add(P("Hier startet nichts automatisch."));
        if (_autoAsk is { } ask) al.Children.Add(Line(P($"„{ask.Name}“ wirklich {(ask.Enabled ? "aus" : "ein")}schalten?"),
            Act("auto-yes", "Ja", () => Try(V, () => { _autoAsk = null; Software.SetStartupEnabled(ask, StartupDir, StartupOff, !ask.Enabled, true, _core.Audit); Say("Geändert.", V); }), true),
            Act("auto-no", "Abbrechen", () => { _autoAsk = null; Say("Nichts geändert.", V); })));
        sp.Children.Add(Card(Section("Autostart", "Was beim Anmelden mitstartet. Ändern nur nach Rückfrage; ausgeschaltete Einträge werden aufgehoben, nicht gelöscht.", al)));

        var fa = new StackPanel { Name = "assoc-list" };
        if (!OperatingSystem.IsWindows()) fa.Children.Add(P("Dateizuordnungen kann Trendo nur unter Windows lesen."));
        else foreach (var e in Software.CommonExtensions) fa.Children.Add(P($"{e} → {Software.ProgIdName(UserChoice(e))}"));
        sp.Children.Add(Card(Section("Womit öffnet sich was?", "Nur Anzeige. Ändern kannst du das in Windows unter „Standard-Apps“.", fa)));
        return ProgPage("Programme starten", sp);
    }

    private static List<Software.AutostartItem> RunKeys()
    {
        var l = new List<Software.AutostartItem>(); if (!OperatingSystem.IsWindows()) return l;
        using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (k is not null) foreach (var n in k.GetValueNames()) l.Add(new(n, k.GetValue(n)?.ToString() ?? "", "Registrierung", true));
        return l;
    }
    private static string? UserChoice(string ext)
    {
        if (!OperatingSystem.IsWindows()) return null;
        using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey($@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{ext}\UserChoice");
        return k?.GetValue("ProgId")?.ToString();
    }

    // ---------- Prüfen ----------
    private Control ProgCheckView()
    {
        var sp = new StackPanel { Spacing = 10 }; const string V = "prog-pruefen";
        var req = new ComboBox { Name = "req-pick", ItemsSource = Software.Known.Select(r => r.Name).ToList(), SelectedIndex = 0, Width = 220 };
        sp.Children.Add(Card(Section("Passt das Programm auf diesen Rechner?", "Prüft Arbeitsspeicher, freien Platz und Windows-Version, bevor du etwas installierst.",
            Line(req, Act("req-check", "Prüfen", () => Try(V, () =>
            {
                var r = Software.Known[Math.Max(0, req.SelectedIndex)];
                var ram = (long)GC.GetGCMemoryInfo().TotalAvailableMemoryBytes; var free = new DriveInfo(Path.GetPathRoot(_core.DataRoot)!).AvailableFreeSpace;
                var build = OperatingSystem.IsWindows() ? Environment.OSVersion.Version.Build : 0;
                var why = Software.Check(r, ram, free, build);
                _progOut = (why.Count == 0 ? $"{r.Name}: passt." : $"BLOCKED – {r.Name} passt nicht: " + string.Join("; ", why) + ".") + "\nHinweis: " + Software.CompatFor(r.Name.Split(' ')[0].ToLowerInvariant());
                Say("Geprüft.", V);
            }), true)))));

        sp.Children.Add(Card(Section("Zusatzprogramme testen", "Startet jedes eingebundene Programm kurz mit „--version“ und schreibt ein Protokoll. Dazu siehst du, wie viel Platz es belegt.",
            Line(Act("int-test", "Alle testen", () => Try(V, () =>
            {
                var tests = DependencyCatalog.All.Select(c => ToolPath(c.Id) is { } p ? Software.Test(c.Id, p, c.Id == "ffmpeg" ? "-version" : "--version") : new Software.ToolTest(c.Id, false, "nicht installiert", 0)).ToList();
                var log = Software.TestLog(tests); File.WriteAllText(Path.Combine(ProgFolder, $"integrationstest-{DateTime.Now:yyyyMMdd-HHmmss}.txt"), log);
                var sizes = Software.ToolSizes([Path.Combine(_core.DataRoot, "werkzeuge"), Path.Combine(_core.DataRoot, "tools")]);
                _progOut = log + "\n\nPlatzbedarf:\n" + string.Join("\n", sizes.Select(s => $"{s.Name,-14} {s.Bytes / 1048576.0:0.0} MB"));
                Say($"{tests.Count(t => t.Ok)} von {tests.Count} Programmen antworten.", V);
            }), true), Act("selftest", "Selbsttest-Bericht", () => Try(V, () =>
            {
                var tests = DependencyCatalog.All.Where(c => ToolPath(c.Id) is not null).Select(c => Software.Test(c.Id, ToolPath(c.Id)!, c.Id == "ffmpeg" ? "-version" : "--version"));
                var rep = Software.SelfTestReport(_core, tests, _core.Version); var f = Path.Combine(ProgFolder, $"selbsttest-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
                File.WriteAllText(f, rep); _progOut = rep; Say("Bericht gespeichert (ohne Benutzernamen und private Pfade): " + Path.GetFileName(f), V);
            }))))));

        var rf = Field("restore-file", "Wiederherstellungsliste (.json)");
        sp.Children.Add(Card(Section("Softwareliste sichern", "Speichert, was installiert ist – als Tabelle und als Liste, mit der ein neuer Rechner per „winget import“ alles wieder bekommt.",
            Line(Act("sw-export", "Liste speichern", async () =>
            {
                try
                {
                    var be = AppCenter.Default(new RealProcessRunner()); var l = await be.InstalledAsync();
                    File.WriteAllText(Path.Combine(ProgFolder, "softwareliste.csv"), Software.ToCsv(l)); File.WriteAllText(Path.Combine(ProgFolder, "wiederherstellen.json"), Software.WingetExport(l));
                    Say($"{l.Count} Programme gespeichert: softwareliste.csv und wiederherstellen.json.", V);
                }
                catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or IOException) { Say("Die Paketverwaltung ist hier nicht verfügbar.", V); }
            }, true)),
            Line(rf, Act("restore-show", "Liste anzeigen", () => Try(V, () =>
            { var ids = Software.ImportIds(File.ReadAllText(Clean(rf))); _progOut = $"{ids.Count} Programme. Auf dem neuen Rechner: winget import -i \"{Path.GetFileName(Clean(rf))}\"\n" + string.Join("\n", ids); Say("Liste gelesen.", V); }))))));
        if (_progOut.Length > 0) sp.Children.Add(Card(Out("prog-out")));
        return ProgPage("Programme prüfen", sp);
    }
}
