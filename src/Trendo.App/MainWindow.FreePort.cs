using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.ConsolePort;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>T-0752: Ablauf „Freies Spiel portieren“ im Studio Konsolenspiele (STUDIO-CONSOLE-01): Quelle und Lizenz, Voraussetzungen,
/// Schritte mit Fortschritt und Protokoll, Ergebnisdatei, Abbruch und Probelauf. PS2Recomp richtet die Person selbst ein; fehlt es, bleibt der Ablauf gesperrt.</summary>
public partial class MainWindow
{
    /// <summary>Tests: wie Programme gefunden werden (Standard: echter Suchpfad; im abgeschotteten Testmodus wird nichts gefunden).</summary>
    public FreePort.Probe? FreePortProbeOverride { get; set; }
    /// <summary>Tests: Programmaufrufe nachbilden (Standard: echter Aufruf mit Abbruch).</summary>
    public FreePort.Runner? FreePortRunnerOverride { get; set; }

    CancellationTokenSource? _fpCts;
    int _fpStep = -1;
    readonly List<string> _fpLines = [];
    public FreePort.Outcome? FreePortLast { get; private set; }
    public FreePort.Trial? FreePortLastTrial { get; private set; }
    public bool FreePortBusy => _fpCts is not null;

    FreePort.Probe FpProbe => FreePortProbeOverride ?? (_opt.Isolated ? new FreePort.Probe(_ => null, () => null) : FreePort.SystemProbe);
    FreePort.Runner FpRunner => FreePortRunnerOverride ?? FreePort.RealRunner;

    static FreePort.Title FpTitle(ProgramDocument d) => FreePort.Find(d.Text("fp_title", FreePort.Titles[0].Id)) ?? FreePort.Titles[0];
    static List<string> FpStubs(ProgramDocument d) => (d.Data["fp_stubs"] is null ? string.Join("\n", FpTitle(d).Stubs) : d.Text("fp_stubs")).Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
    string FpWorkDir(ProgramDocument d) => Path.Combine(ConsoleProject(d), "frei", FpTitle(d).Id);
    /// <summary>T-0757 Fix1: Trendos eigene Kopien der Liste „trendo-einsortiert.txt“, je PS2Recomp-Ordner (nicht je Titel oder Projekt).</summary>
    string FpListDir => Path.Combine(_core.DataRoot, "konsolenspiele", "ps2recomp-listen");
    /// <summary>SHA-256 der gewählten ELF-Datei, null wenn sie fehlt oder nicht lesbar ist.</summary>
    static string? FpElfSha(ProgramDocument d)
    {
        try { return d.Text("fp_elf") is { Length: > 0 } e && File.Exists(e) ? FreePort.Sha256(e) : null; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }
    /// <summary>T-0757 R1/Fix1: Bei „Eigenes Homebrew“ gilt die Bestätigung „Ich habe die Rechte an diesem Programm“ nur für genau diese Datei –
    /// gespeichert wird ihre SHA-256, eine geänderte oder andere Datei braucht eine neue Bestätigung.</summary>
    static bool FpRightsOk(ProgramDocument d) => !FreePort.NeedsRights(FpTitle(d)) || FpElfSha(d) is { } sha && d.Text("fp_rights") == sha;
    FreePort.Job FpJob(ProgramDocument d) => new(FpTitle(d), d.Text("fp_elf"), d.Text("fp_license"), d.Text("fp_recomp"), FpStubs(d), FpWorkDir(d), FreePort.NeedsRights(FpTitle(d)) && FpRightsOk(d), FpListDir);
    IReadOnlyList<FreePort.Check> FpChecks(ProgramDocument d)
    {
        var l = FreePort.Prerequisites(FpTitle(d), d.Text("fp_license"), d.Text("fp_elf"), d.Text("fp_recomp"), FpProbe, FpListDir).ToList();
        if (FreePort.NeedsRights(FpTitle(d))) l.Insert(2, new("rechte", "Rechte am Programm", FpRightsOk(d), FpRightsOk(d) ? "bestätigt: " + FreePort.RightsText
            : d.Text("fp_rights").Length > 0 ? $"Das Programm hat sich seit deiner Bestätigung geändert. Setze den Haken „{FreePort.RightsText}“ neu."
            : $"noch nicht bestätigt. Setze unten bei „1 · Quelle und Lizenz“ den Haken „{FreePort.RightsText}“."));
        return l;
    }

    bool _fpAskClean;

    Control FreePortView(ProgramDocument d)
    {
        var t = FpTitle(d);
        var sp = new StackPanel { Name = "fp-view", Spacing = 8, Margin = new Thickness(4, 0, 14, 4), MaxWidth = 820, HorizontalAlignment = HorizontalAlignment.Left };
        sp.Children.Add(P("Übersetzt ein frei lizenziertes PS2-Programm mit dem Recompiler PS2Recomp zu einem Windows-Programm. PS2Recomp, CMake und einen Compiler für C++ richtest du selbst ein – Trendo liefert sie nicht mit und startet nichts, solange etwas fehlt. Kaufspiele und BIOS-Dateien gehen hier nicht.", Muted, 12));

        var checks = FpChecks(d);
        var blocked = checks.FirstOrDefault(c => !c.Ok);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var go = new Button { Name = "fp-start", Content = "Portieren starten", Padding = new Thickness(16, 6), IsEnabled = blocked is null && !FreePortBusy };
        go.Classes.Add("accent"); go.Classes.Add("haupt");
        go.Click += async (_, _) => await FreePortStart(d);
        actions.Children.Add(go);
        if (FreePortBusy)
        {
            var stop = new Button { Name = "fp-cancel", Content = "Abbrechen", Padding = new Thickness(14, 6) };
            stop.Click += (_, _) => FreePortCancel();
            actions.Children.Add(stop);
        }
        sp.Children.Add(actions);
        if (blocked is not null && !FreePortBusy) sp.Children.Add(new TextBlock { Name = "fp-blocked", Text = "BLOCKIERT – " + blocked.Label + ": " + blocked.Detail, TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse("#F5A524"), FontSize = 12 });
        if (FreePortLast is { } last) sp.Children.Add(new TextBlock { Name = "fp-outcome", Text = (last.Status switch { "fertig" => "✓ Fertig: ", "abgebrochen" => "Abgebrochen: ", "blockiert" => "BLOCKIERT: ", _ => "✕ Fehler: " }) + last.Message, TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = last.Status == "fertig" ? Brush.Parse("#3DDC84") : Brush.Parse("#F5A524") });

        sp.Children.Add(H("1 · Quelle und Lizenz", 13));
        var titles = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var x in FreePort.Titles)
        {
            var b = new Button { Name = "fp-title-" + x.Id, Content = new TextBlock { Text = x.Name }, // TextBlock: sonst wird „_“ zum Tastenkürzel
                Padding = new Thickness(12, 5), IsEnabled = !FreePortBusy };
            if (x.Id == t.Id) b.Classes.Add("accent");
            b.Click += (_, _) => { if (x.Id != t.Id) ProgramRun(ConsoleKey, () => { d.Set("fp_title", x.Id, "Titel"); d.Set("fp_stubs", null, "Bindungen"); }); };
            titles.Children.Add(b);
        }
        sp.Children.Add(titles);
        sp.Children.Add(P(t.Note, Muted, 11));
        if (t.Source.Length > 0) sp.Children.Add(new TextBlock { Name = "fp-source", Text = $"Quelle: {t.Source} · Lizenz: {t.License} · Datei: {t.ElfName}", Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        var files = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var lic = new Button { Name = "fp-pick-license", Content = "Lizenzdatei wählen", Padding = new Thickness(12, 5), IsEnabled = !FreePortBusy };
        lic.Click += async (_, _) => { if (await PickMediaFile("fp-license", "Lizenzdatei (LICENSE)", ["LICENSE*", "COPYING*", "*.txt", "*.md", "*"]) is { } f) ProgramRun(ConsoleKey, () => d.Set("fp_license", f, "Lizenzdatei")); };
        var elf = new Button { Name = "fp-pick-elf", Content = "Programm wählen (.elf)", Padding = new Thickness(12, 5), IsEnabled = !FreePortBusy };
        elf.Click += async (_, _) => { if (await PickMediaFile("fp-elf", "PS2-Programm (.elf)", ["*.elf"]) is { } f) ProgramRun(ConsoleKey, () => d.Set("fp_elf", f, "Programm")); };
        files.Children.Add(lic); files.Children.Add(elf);
        sp.Children.Add(files);
        if (FreePort.NeedsRights(t))
        {
            var rights = new CheckBox { Name = "fp-rights", Content = FreePort.RightsText, Foreground = Text, IsChecked = FpRightsOk(d), IsEnabled = !FreePortBusy && d.Text("fp_elf").Length > 0 };
            rights.IsCheckedChanged += (_, _) =>
            {
                var want = rights.IsChecked == true ? FpElfSha(d) : null;
                if (want != (d.Data["fp_rights"] is null ? null : d.Text("fp_rights"))) ProgramRun(ConsoleKey, () => d.Set("fp_rights", want, "Rechte bestätigt"));
            };
            sp.Children.Add(rights);
        }

        sp.Children.Add(H("2 · Voraussetzungen auf diesem PC", 13));
        var cl = new StackPanel { Name = "fp-checks", Spacing = 2 };
        foreach (var c in checks)
            cl.Children.Add(new TextBlock { Name = "fp-check-" + c.Id, Text = $"{(c.Ok ? "✓" : "✕")} {c.Label}: {c.Detail}", TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = c.Ok ? Text : Brush.Parse("#F5A524") });
        sp.Children.Add(cl);
        var recomp = new Button { Name = "fp-pick-recomp", Content = "PS2Recomp-Ordner wählen", Padding = new Thickness(12, 5), IsEnabled = !FreePortBusy };
        recomp.Click += async (_, _) => { if (await PickFolderPath("PS2Recomp-Ordner (kurzer Pfad, z. B. C:\\ps2recomp)") is { } f) ProgramRun(ConsoleKey, () => d.Set("fp_recomp", f, "PS2Recomp-Ordner")); };
        sp.Children.Add(recomp);
        FpCleanArea(d, sp);
        if (ProgramLevel != UserLevel.Einfach || t.Id == FreePort.OwnId)
        {
            sp.Children.Add(P("Bindungen für general.stubs (eine je Zeile, name@0xADRESSE). Sie gelten nur für genau die gewählte ELF-Datei.", Muted, 11));
            var stubs = new TextBox { Name = "fp-stubs", Text = string.Join("\n", FpStubs(d)), AcceptsReturn = true, MinHeight = 60, FontFamily = new FontFamily("Consolas, Courier New, monospace"), FontSize = 12, IsEnabled = !FreePortBusy };
            stubs.LostFocus += (_, _) => { var v = (stubs.Text ?? "").Replace("\r", ""); if (v != string.Join("\n", FpStubs(d))) ProgramRun(ConsoleKey, () => d.Set("fp_stubs", v, "Bindungen")); };
            sp.Children.Add(stubs);
            if (FreePort.CheckStubs(FpStubs(d)) is { } bad) sp.Children.Add(new TextBlock { Name = "fp-stubs-bad", Text = bad, Foreground = Brush.Parse("#F5A524"), FontSize = 12 });
        }

        sp.Children.Add(H("3 · Schritte", 13));
        var plan = FreePort.Plan(d.Text("fp_recomp"));
        var stepList = new StackPanel { Name = "fp-steps", Spacing = 1 };
        for (var i = 0; i < plan.Count; i++)
        {
            var mark = FreePortBusy ? (i < _fpStep ? "✓" : i == _fpStep ? "▶" : "○") : FreePortLast is { Status: "fertig" } ? "✓" : "○";
            stepList.Children.Add(new TextBlock { Name = "fp-step-" + plan[i].Id, Text = $"{mark} {i + 1}. {plan[i].Label}", Foreground = i == _fpStep && FreePortBusy ? Text : Muted, FontSize = 12 });
        }
        sp.Children.Add(stepList);
        if (FreePortBusy) sp.Children.Add(new ProgressBar { Name = "fp-progress", Minimum = 0, Maximum = plan.Count, Value = Math.Max(0, _fpStep), Height = 6 });
        var logFile = Path.Combine(FpWorkDir(d), "protokoll.txt");
        string[] shown; lock (_fpLines) shown = _fpLines.Count > 0 ? [.. _fpLines.TakeLast(80)] : File.Exists(logFile) ? FpReadLog(logFile) : [];
        sp.Children.Add(H("Protokoll", 13));
        sp.Children.Add(new TextBox { Name = "fp-log", Text = shown.Length > 0 ? string.Join("\n", shown) : "Noch kein Lauf.", IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, MaxHeight = 260, FontFamily = new FontFamily("Consolas, Courier New, monospace"), FontSize = 11 });

        var result = Path.Combine(FpWorkDir(d), "ergebnis.json");
        if (File.Exists(result) && !FreePortBusy)
        {
            sp.Children.Add(H("Ergebnis", 13));
            var target = FreePort.ResolveTrial(result, d.Text("fp_recomp"));
            string resultText;
            try { var info = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(result)); resultText = $"{Path.GetFileName(info?["programm"]?.ToString() ?? "")} · SHA-256 {info?["programm_sha256"]} · Datei: ergebnis.json"; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException) { resultText = "✕ Die Ergebnisdatei ist beschädigt: " + FriendlyError.Say(ex); }
            sp.Children.Add(new TextBlock { Name = "fp-result", Text = resultText, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap });
            if (target.Error is { } terr) sp.Children.Add(new TextBlock { Name = "fp-trial-blocked", Text = "✕ " + terr, Foreground = Brush.Parse("#F5A524"), FontSize = 12, TextWrapping = TextWrapping.Wrap });
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var trial = new Button { Name = "fp-trial", Content = "Probelauf (20 s)", Padding = new Thickness(12, 5), IsEnabled = target.Error is null && !FreePortBusy };
            trial.Click += async (_, _) => await FreePortTrial(d);
            var open = new Button { Name = "fp-open", Content = "Ordner öffnen", Padding = new Thickness(12, 5) };
            open.Click += (_, _) => Launch(FpWorkDir(d));
            row.Children.Add(trial); row.Children.Add(open);
            sp.Children.Add(row);
            if (FreePortLastTrial is { } tr) sp.Children.Add(new TextBlock { Name = "fp-trial-state", Text = tr.Message, Foreground = tr.Status is "läuft" or "beendet" ? Brush.Parse("#3DDC84") : Brush.Parse("#F5A524"), FontSize = 12, TextWrapping = TextWrapping.Wrap });
        }
        return sp;
    }

    /// <summary>T-0757 Fix1: Reste früherer Läufe im PS2Recomp-Ordner und die Aktion „Alte Trendo-Dateien aus PS2Recomp entfernen“ mit Rückfrage.
    /// Entfernt werden nur Dateien, deren SHA-256 in beiden Listen steht; fremde Dateien bleiben.</summary>
    void FpCleanArea(ProgramDocument d, StackPanel sp)
    {
        var dir = d.Text("fp_recomp");
        if (dir.Length == 0 || !Directory.Exists(Path.Combine(dir, "ps2xRuntime"))) return;
        FreePort.Leftovers l;
        try { l = FreePort.Inspect(dir, FpListDir); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return; }
        if (l.Owned.Count == 0 && l.Foreign.Count == 0) return;
        var clean = new Button { Name = "fp-clean", Content = "Alte Trendo-Dateien aus PS2Recomp entfernen", Padding = new Thickness(12, 5), IsEnabled = l.Owned.Count > 0 && !FreePortBusy && !_fpAskClean };
        clean.Click += (_, _) => { _fpAskClean = true; FpRefresh(); };
        sp.Children.Add(clean);
        if (l.Foreign.Count > 0) sp.Children.Add(new TextBlock { Name = "fp-clean-foreign", Text = $"{l.Foreign.Count} Code-Dateien kann Trendo nicht als eigene belegen und entfernt sie nicht.", Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap });
        if (!_fpAskClean) return;
        sp.Children.Add(new TextBlock { Name = "fp-clean-ask", Text = $"{l.Owned.Count} Dateien, die Trendo bei früheren Läufen in PS2Recomp abgelegt hat, werden gelöscht (z. B. {l.Owned[0]}). Dateien von PS2Recomp selbst bleiben. Wirklich entfernen?", Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var yes = new Button { Name = "fp-clean-yes", Content = "Ja, entfernen", Padding = new Thickness(12, 5) };
        yes.Click += (_, _) => FreePortCleanOld(d);
        var no = new Button { Name = "fp-clean-no", Content = "Abbrechen", Padding = new Thickness(12, 5) };
        no.Click += (_, _) => { _fpAskClean = false; FpRefresh(); };
        row.Children.Add(yes); row.Children.Add(no);
        sp.Children.Add(row);
    }

    /// <summary>Nach der Rückfrage: alte, belegte Trendo-Dateien aus PS2Recomp entfernen. Liefert die entfernten Dateien.</summary>
    public IReadOnlyList<string> FreePortCleanOld(ProgramDocument d)
    {
        _fpAskClean = false;
        try { UserDirFixedNames(d.Text("fp_recomp")); } catch (UnauthorizedAccessException e) { _progMsg[ConsoleKey] = e.Message; FpRefresh(); return []; } // T-0764 (13. Prüfrunde): Ort aus dem Dokument – Kinder nur Freigabe
        IReadOnlyList<string> removed = [];
        try
        {
            removed = FreePort.RemoveOld(d.Text("fp_recomp"), FpListDir);
            _core.Audit.Append(Principal.LocalOwner.Name, "konsole.frei.aufraeumen", removed.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            _progMsg[ConsoleKey] = $"{removed.Count} alte Trendo-Dateien aus PS2Recomp entfernt.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { _progMsg[ConsoleKey] = "Entfernen ging nicht: " + FriendlyError.Say(e); }
        FpRefresh();
        return removed;
    }

    static string[] FpReadLog(string f)
    {
        try { return File.ReadLines(f).TakeLast(80).ToArray(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return ["✕ Protokoll lässt sich gerade nicht lesen: " + FriendlyError.Say(e)]; }
    }

    /// <summary>Statuszeile im Schritt „Freies Spiel portieren“.</summary>
    string FreePortStatus(ProgramDocument d)
    {
        if (FreePortBusy) return _fpStep >= 0 ? $"Portieren läuft – Schritt {_fpStep + 1} von {FreePort.Plan(d.Text("fp_recomp")).Count}" : "Probelauf läuft";
        if (FreePortLast is { } l) return l.Status == "fertig" ? "Portieren fertig" : l.Message;
        return FpChecks(d).FirstOrDefault(c => !c.Ok) is { } b ? "BLOCKIERT – " + b.Label : "Bereit zum Portieren";
    }

    void FpRefresh() { if (Dispatcher.UIThread.CheckAccess()) Show(ConsoleKey); else Trendo.App.UiThread.Post(() => Show(ConsoleKey)); }

    /// <summary>Startet den ganzen Ablauf. null: läuft schon.</summary>
    public async Task<FreePort.Outcome?> FreePortStart(ProgramDocument d)
    {
        if (_fpCts is not null) return null;
        // T-0764 (13. Prüfrunde, Nebenbefund): Portieren startet die PS2Recomp-Werkzeuge im gewählten Ordner – Programme starten nur Erwachsene (wie Engines)
        if (!DarfAusfuehrenStill("konsole.frei", out var nein)) { _progMsg[ConsoleKey] = nein; FpRefresh(); return null; }
        var job = FpJob(d);
        lock (_fpLines) _fpLines.Clear();
        _fpCts = new CancellationTokenSource(); _fpStep = 0; FreePortLast = null; FreePortLastTrial = null;
        FpRefresh();
        _core.Audit.Append(Principal.LocalOwner.Name, "konsole.frei", job.Title.Id);
        try
        {
            var runner = FpRunner; var probe = FpProbe; var ct = _fpCts.Token;
            FreePortLast = await Task.Run(() => FreePort.RunAsync(job, probe, runner,
                (i, _, _) => { _fpStep = i; FpRefresh(); },
                l => { lock (_fpLines) { _fpLines.Add(l); if (_fpLines.Count > 2000) _fpLines.RemoveRange(0, 500); } }, ct));
        }
        finally { _fpCts.Dispose(); _fpCts = null; _fpStep = -1; }
        _progMsg[ConsoleKey] = FreePortLast.Status == "fertig" ? "Portieren fertig: " + Path.GetFileName(FreePortLast.Program) : FreePortLast.Message;
        FpRefresh();
        return FreePortLast;
    }

    public void FreePortCancel() { try { _fpCts?.Cancel(); } catch (ObjectDisposedException) { } }

    /// <summary>Kurzer Probelauf des fertigen Programms (nur der eigene Prozess, nach 20 s beendet).</summary>
    public async Task<FreePort.Trial?> FreePortTrial(ProgramDocument d, int seconds = 20)
    {
        var result = Path.Combine(FpWorkDir(d), "ergebnis.json");
        if (!File.Exists(result) || _fpCts is not null) return null;
        if (!DarfAusfuehrenStill("konsole.frei.probelauf", out var nein)) { _progMsg[ConsoleKey] = nein; FpRefresh(); return null; } // T-0764 (13. Prüfrunde): startet das fertige Programm
        var target = FreePort.ResolveTrial(result, d.Text("fp_recomp"));
        if (target.Error is { } err) { _progMsg[ConsoleKey] = err; FpRefresh(); return null; }
        _fpCts = new CancellationTokenSource();
        try { FreePortLastTrial = await Task.Run(() => FreePort.TrialAsync(target.Program!, target.Elf!, FpWorkDir(d), FpRunner, seconds, _fpCts.Token)); }
        finally { _fpCts.Dispose(); _fpCts = null; }
        FpRefresh();
        return FreePortLastTrial;
    }
}
