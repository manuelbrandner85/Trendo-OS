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
    FreePort.Job FpJob(ProgramDocument d) => new(FpTitle(d), d.Text("fp_elf"), d.Text("fp_license"), d.Text("fp_recomp"), FpStubs(d), FpWorkDir(d));
    IReadOnlyList<FreePort.Check> FpChecks(ProgramDocument d) => FreePort.Prerequisites(FpTitle(d), d.Text("fp_license"), d.Text("fp_elf"), d.Text("fp_recomp"), FpProbe);

    Control FreePortView(ProgramDocument d)
    {
        var t = FpTitle(d);
        var sp = new StackPanel { Name = "fp-view", Spacing = 8, Margin = new Thickness(4, 0, 14, 4), MaxWidth = 820, HorizontalAlignment = HorizontalAlignment.Left };
        sp.Children.Add(P("Übersetzt ein frei lizenziertes PS2-Programm mit dem Recompiler PS2Recomp zu einem Windows-Programm. PS2Recomp, CMake und einen Compiler für C++ richtest du selbst ein – Trendo liefert sie nicht mit und startet nichts, solange etwas fehlt. Kaufspiele und BIOS-Dateien gehen hier nicht.", Muted, 12));

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

        sp.Children.Add(H("2 · Voraussetzungen auf diesem PC", 13));
        var checks = FpChecks(d);
        var cl = new StackPanel { Name = "fp-checks", Spacing = 2 };
        foreach (var c in checks)
            cl.Children.Add(new TextBlock { Name = "fp-check-" + c.Id, Text = $"{(c.Ok ? "✓" : "✕")} {c.Label}: {c.Detail}", TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = c.Ok ? Text : Brush.Parse("#F5A524") });
        sp.Children.Add(cl);
        var recomp = new Button { Name = "fp-pick-recomp", Content = "PS2Recomp-Ordner wählen", Padding = new Thickness(12, 5), IsEnabled = !FreePortBusy };
        recomp.Click += async (_, _) => { if (await PickFolderPath("PS2Recomp-Ordner (kurzer Pfad, z. B. C:\\ps2recomp)") is { } f) ProgramRun(ConsoleKey, () => d.Set("fp_recomp", f, "PS2Recomp-Ordner")); };
        sp.Children.Add(recomp);
        if (ProgramLevel != UserLevel.Einfach || t.Id == FreePort.OwnId)
        {
            sp.Children.Add(P("Bindungen für general.stubs (eine je Zeile, name@0xADRESSE). Sie gelten nur für genau die gewählte ELF-Datei.", Muted, 11));
            var stubs = new TextBox { Name = "fp-stubs", Text = string.Join("\n", FpStubs(d)), AcceptsReturn = true, MinHeight = 60, FontFamily = new FontFamily("Consolas, Courier New, monospace"), FontSize = 12, IsEnabled = !FreePortBusy };
            stubs.LostFocus += (_, _) => { var v = (stubs.Text ?? "").Replace("\r", ""); if (v != string.Join("\n", FpStubs(d))) ProgramRun(ConsoleKey, () => d.Set("fp_stubs", v, "Bindungen")); };
            sp.Children.Add(stubs);
            if (FreePort.CheckStubs(FpStubs(d)) is { } bad) sp.Children.Add(new TextBlock { Name = "fp-stubs-bad", Text = bad, Foreground = Brush.Parse("#F5A524"), FontSize = 12 });
        }

        sp.Children.Add(H("3 · Portieren", 13));
        var blocked = checks.FirstOrDefault(c => !c.Ok);
        if (blocked is not null && !FreePortBusy) sp.Children.Add(new TextBlock { Name = "fp-blocked", Text = "BLOCKIERT – " + blocked.Label + ": " + blocked.Detail, TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse("#F5A524"), FontSize = 12 });
        var plan = FreePort.Plan(d.Text("fp_recomp"));
        var stepList = new StackPanel { Name = "fp-steps", Spacing = 1 };
        for (var i = 0; i < plan.Count; i++)
        {
            var mark = FreePortBusy ? (i < _fpStep ? "✓" : i == _fpStep ? "▶" : "○") : FreePortLast is { Status: "fertig" } ? "✓" : "○";
            stepList.Children.Add(new TextBlock { Name = "fp-step-" + plan[i].Id, Text = $"{mark} {i + 1}. {plan[i].Label}", Foreground = i == _fpStep && FreePortBusy ? Text : Muted, FontSize = 12 });
        }
        sp.Children.Add(stepList);
        if (FreePortBusy) sp.Children.Add(new ProgressBar { Name = "fp-progress", Minimum = 0, Maximum = plan.Count, Value = Math.Max(0, _fpStep), Height = 6 });
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
        if (FreePortLast is { } last) sp.Children.Add(new TextBlock { Name = "fp-outcome", Text = (last.Status switch { "fertig" => "✓ Fertig: ", "abgebrochen" => "Abgebrochen: ", "blockiert" => "BLOCKIERT: ", _ => "✕ Fehler: " }) + last.Message, TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = last.Status == "fertig" ? Brush.Parse("#3DDC84") : Brush.Parse("#F5A524") });

        var logFile = Path.Combine(FpWorkDir(d), "protokoll.txt");
        string[] shown; lock (_fpLines) shown = _fpLines.Count > 0 ? [.. _fpLines.TakeLast(80)] : File.Exists(logFile) ? File.ReadLines(logFile).TakeLast(80).ToArray() : [];
        sp.Children.Add(H("Protokoll", 13));
        sp.Children.Add(new TextBox { Name = "fp-log", Text = shown.Length > 0 ? string.Join("\n", shown) : "Noch kein Lauf.", IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, MaxHeight = 260, FontFamily = new FontFamily("Consolas, Courier New, monospace"), FontSize = 11 });

        var result = Path.Combine(FpWorkDir(d), "ergebnis.json");
        if (File.Exists(result) && !FreePortBusy)
        {
            sp.Children.Add(H("Ergebnis", 13));
            var info = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(result));
            var prog = info?["programm"]?.GetValue<string>() ?? "";
            sp.Children.Add(new TextBlock { Name = "fp-result", Text = $"{Path.GetFileName(prog)} · SHA-256 {info?["programm_sha256"]?.GetValue<string>()} · Datei: ergebnis.json", Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap });
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var trial = new Button { Name = "fp-trial", Content = "Probelauf (20 s)", Padding = new Thickness(12, 5), IsEnabled = File.Exists(prog) && !FreePortBusy };
            trial.Click += async (_, _) => await FreePortTrial(d);
            var open = new Button { Name = "fp-open", Content = "Ordner öffnen", Padding = new Thickness(12, 5) };
            open.Click += (_, _) => Launch(FpWorkDir(d));
            row.Children.Add(trial); row.Children.Add(open);
            sp.Children.Add(row);
            if (FreePortLastTrial is { } tr) sp.Children.Add(new TextBlock { Name = "fp-trial-state", Text = tr.Message, Foreground = tr.Status == "läuft" || tr.Status == "beendet" ? Brush.Parse("#3DDC84") : Brush.Parse("#F5A524"), FontSize = 12, TextWrapping = TextWrapping.Wrap });
        }
        return sp;
    }

    void FpRefresh() { if (Dispatcher.UIThread.CheckAccess()) Show(ConsoleKey); else Dispatcher.UIThread.Post(() => Show(ConsoleKey)); }

    /// <summary>Startet den ganzen Ablauf. null: läuft schon.</summary>
    public async Task<FreePort.Outcome?> FreePortStart(ProgramDocument d)
    {
        if (_fpCts is not null) return null;
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
        var info = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(result));
        var prog = info?["programm"]?.GetValue<string>() ?? "";
        if (!File.Exists(prog)) { _progMsg[ConsoleKey] = "Das fertige Programm ist nicht mehr da. Starte das Portieren neu."; FpRefresh(); return null; }
        _fpCts = new CancellationTokenSource();
        try { FreePortLastTrial = await Task.Run(() => FreePort.TrialAsync(prog, d.Text("fp_elf"), FpWorkDir(d), FpRunner, seconds, _fpCts.Token)); }
        finally { _fpCts.Dispose(); _fpCts = null; }
        FpRefresh();
        return FreePortLastTrial;
    }
}
