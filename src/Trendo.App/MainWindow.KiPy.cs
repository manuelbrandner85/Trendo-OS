using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Paket E (0.9.49, Wunsch 02.11.2026): freie Python-KIs in der KI-Bibliothek – TripoSR (Foto → 3D-Modell) und MMAudio (Geräusche).
/// Einrichten nach dem Klick (= Zustimmung) aus den offiziellen Quellen, jede KI in ihrer eigenen Umgebung, alles im Audit-Log.</summary>
public partial class MainWindow
{
    /// <summary>Für Tests: ersetzt Programmläufe beim Einrichten und Ausführen (Programm, Argumente, Ordner, Umgebung).</summary>
    public Func<string, IReadOnlyList<string>, string, IReadOnlyDictionary<string, string>, Task<Cli.Result>>? KiStepOverride { get; set; }
    /// <summary>Für Tests: NVIDIA-Grafikkarte vorhanden?</summary>
    public Func<bool>? NvidiaOverride { get; set; }

    bool HasNvidia() => NvidiaOverride?.Invoke() ?? File.Exists(Path.Combine(Environment.SystemDirectory, "nvidia-smi.exe"));

    Task<Cli.Result> KiStep(string exe, IReadOnlyList<string> args, string cwd, IReadOnlyDictionary<string, string> env, int timeoutMs = 3_600_000)
    {
        if (KiStepOverride is { } o) return o(exe, args, cwd, env);
        if (_opt.Isolated) return Task.FromResult(new Cli.Result(-1, "", "Testmodus: es wird nichts eingerichtet oder gestartet."));
        Directory.CreateDirectory(cwd);
        return Task.Run(() => Cli.Run(exe, args, cwd, timeoutMs, env.ToDictionary()));
    }

    /// <summary>Python-KI einrichten: uv (falls nötig) laden und prüfen, Quellcode auf festem Stand holen und prüfen, Umgebung und Pakete anlegen.</summary>
    async Task KiPyInstall(KiLibraryModel m, KiPyApp app)
    {
        var store = KiStore; var p = store.PyPaths(m.Runtime); var nvidia = HasNvidia();
        var steps = KiPyApps.Steps(app, p, nvidia);
        var total = steps.Count + 2; var step = 0;
        if (!store.RuntimeReady(KiRuntime.Uv))
        {
            var uv = KiLibrary.RuntimeInfo(KiRuntime.Uv);
            SetupOverlayProgress(step, total, $"{uv.Name} wird geladen …");
            var (zip, _) = await KiDownload(uv.DownloadUrl, uv.Asset, uv.Bytes, step, total);
            try { await Task.Run(() => store.InstallRuntime(KiRuntime.Uv, zip, _core.Audit, Extract7z)); } finally { TryDelete(zip); }
        }
        step++;
        if (KiStepOverride is null && !app.PackagesOnly && !File.Exists(p.Git)) throw new InvalidOperationException("Git fehlt – bitte unter „Programme“ aus dem Vollpaket einrichten.");
        Directory.CreateDirectory(p.Dir);
        var env = KiPyApps.UvEnv(p.UvHome);
        _core.Audit.Append(Principal.LocalOwner.Name, "ki.pyapp.start", $"{app.Repo}@{app.Commit} torch={KiPyApps.TorchIndex(nvidia)}");
        foreach (var s in steps)
        {
            SetupToken.ThrowIfCancellationRequested();
            SetupOverlayProgress(step, total, s.Text + " …");
            var r = await KiStep(s.Exe, s.Args, s.Cwd, env);
            if (!r.Ok) throw new InvalidOperationException($"{s.Text}: " + Short(r.Err.Length > 0 ? r.Err : r.Out));
            step++;
        }
        var head = app.PackagesOnly ? new Cli.Result(0, app.Commit, "") : await KiStep(p.Git, ["-C", p.Repo, "rev-parse", "HEAD"], p.Dir, env);
        if (!KiPyApps.CommitOk(app, head.Out)) throw new InvalidOperationException("Der geladene Quellcode hat nicht den geprüften Stand – nicht eingerichtet.");
        CopyKiScript(app, p);
        File.WriteAllText(p.Marker, app.Commit);
        _core.Audit.Append(Principal.LocalOwner.Name, "ki.pyapp.fertig", $"{app.Repo}@{app.Commit}");
        SetupOverlayProgress(total, total, "Fertig ✓");
    }

    static void CopyKiScript(KiPyApp app, KiPyApps.Paths p)
    {
        Directory.CreateDirectory(p.Dir);
        using var s = AssetLoader.Open(new Uri("avares://TrendoOS/Assets/ki/" + app.Script));
        using var o = File.Create(Path.Combine(p.Dir, app.Script));
        s.CopyTo(o);
    }

    /// <summary>Python-KI ausführen: Trendos Startskript mit dem Python der eigenen Umgebung.</summary>
    async Task<Cli.Result> KiPyRun(KiRuntime r, IReadOnlyList<string> args)
    {
        var app = KiPyApps.For(r)!; var p = KiStore.PyPaths(r);
        if (KiStepOverride is null && !_opt.Isolated) CopyKiScript(app, p); // immer die Fassung dieser Trendo-Version
        return await KiStep(p.PythonExe, [Path.Combine(p.Dir, app.Script), p.Repo, .. args], p.Dir, KiPyApps.UvEnv(p.UvHome), 1_800_000);
    }

    /// <summary>Ausprobieren „3D aus Bild“: Foto wählen → GLB → im 3D-Studio öffnen.</summary>
    Control KiTry3D(KiLibraryModel m, TextBlock status, Func<string, string> outFile)
    {
        var tex = new CheckBox { Name = "kilib-3d-texture", Content = "Mit Textur (schöner, dauert etwas länger)", IsChecked = true, Foreground = Text };
        var go = new Button { Name = "kilib-run", Content = "Foto wählen und 3D-Modell erzeugen", Background = Accent };
        go.Click += async (_, _) =>
        {
            var src = await PickMedia("kilib-image", ["*.png", "*.jpg", "*.jpeg", "*.webp"]);
            if (src is null) return;
            var o = outFile(".glb");
            go.IsEnabled = false; status.Text = "Das 3D-Modell entsteht … (beim ersten Mal lädt TripoSR sein Modell, ca. 1,7 GB)";
            var res = await KiPyRun(m.Runtime, [src, o, .. tex.IsChecked == true ? new[] { "--texture" } : []]);
            KiPyDone(m, res, o);
        };
        return new StackPanel { Spacing = 6, Children = { P("Tipp: ein einzelner Gegenstand, gut beleuchtet, ruhiger Hintergrund. Den Hintergrund stellt Trendo selbst frei.", Muted, 11.5), tex, go } };
    }

    /// <summary>Ausprobieren „Geräusche“: Beschreibung und Länge – oder ein Video, zu dem der Ton passt.</summary>
    Control KiTrySound(KiLibraryModel m, TextBlock status, Func<string, string> outFile)
    {
        var p = new TextBox { Name = "kilib-prompt", PlaceholderText = "Beschreibe das Geräusch (am besten auf Englisch), z. B. „rain on a tin roof, distant thunder“", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 60 };
        var len = new ComboBox { Name = "kilib-sound-len", ItemsSource = new[] { "4 Sekunden", "8 Sekunden", "10 Sekunden" }, SelectedIndex = 1 };
        double Secs() => len.SelectedIndex switch { 0 => 4, 2 => 10, _ => 8 };
        var go = new Button { Name = "kilib-run", Content = "Geräusch erzeugen", Background = Accent };
        go.Click += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(p.Text)) { status.Text = "Bitte zuerst beschreiben, was zu hören sein soll."; return; }
            var o = outFile(".wav");
            go.IsEnabled = false; status.Text = "Das Geräusch entsteht … (beim ersten Mal lädt MMAudio seine Modelle, ca. 6 GB)";
            KiPyDone(m, await KiPyRun(m.Runtime, [o, "--prompt", p.Text!.Trim(), "--duration", Secs().ToString(System.Globalization.CultureInfo.InvariantCulture)]), o);
        };
        var video = new Button { Name = "kilib-sound-video", Content = "Ton passend zu einem Video" };
        video.Click += async (_, _) =>
        {
            var src = await PickMedia("kilib-video", ["*.mp4", "*.mov", "*.mkv", "*.webm"]);
            if (src is null) return;
            var o = outFile(".wav");
            video.IsEnabled = false; status.Text = "Der Ton zum Video entsteht …";
            KiPyDone(m, await KiPyRun(m.Runtime, [o, "--video", src, "--prompt", p.Text?.Trim() ?? "", "--duration", Secs().ToString(System.Globalization.CultureInfo.InvariantCulture)]), o);
        };
        return new StackPanel { Spacing = 6, Children = { p, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { len, go, video } },
            P("Hinweis: Die Modelle von MMAudio sind nur für private, nicht-kommerzielle Zwecke freigegeben (CC BY-NC 4.0).", Muted, 11) } };
    }

    /// <summary>Vorschlag 36: „✨ Freie KI hier“ in der Kopfkarte des Studios – ✓ wenn schon eingerichtet; der Klick öffnet die KI-Bibliothek.</summary>
    Control? FreeKiRow(string studioId)
    {
        var kinds = KiSuggestions.For(studioId);
        if (kinds.Count == 0) return null;
        var row = new WrapPanel { Name = "page-freeki", MaxWidth = 380, HorizontalAlignment = HorizontalAlignment.Right };
        row.Children.Add(new TextBlock { Text = "✨ Freie KI:", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 3) });
        var store = KiStore;
        foreach (var k in kinds)
        {
            var kk = k;
            var ready = KiLibrary.OfKind(k).Any(m => m.Runtime != KiRuntime.Ollama && store.ModelReady(m));
            var b = new Button { Name = "page-freeki-" + k, Content = (ready ? "✓ " : "") + KiLibrary.Kinds[k].Name, FontSize = 12, Padding = new Thickness(9, 2), Margin = new Thickness(0, 0, 5, 3),
                CornerRadius = new CornerRadius(10), Background = Brush.Parse("#22FFFFFF"), Foreground = Text, BorderThickness = new Thickness(0) };
            ToolTip.SetTip(b, KiLibrary.Kinds[k].Description + (ready ? " (schon eingerichtet)" : " – kostenlos, läuft auf deinem PC; geladen wird erst nach deinem Klick dort."));
            b.Click += (_, _) => KiLibraryShow(kk);
            row.Children.Add(b);
        }
        return row;
    }

    /// <summary>Absturzschutz (03.11.2026): ein abgefangener Fehler wird kurz gemeldet – Trendo bleibt offen, Einzelheiten stehen im Protokoll.</summary>
    DateTime _lastUiErrorToast;
    void OnUiErrorCaught(Exception e) => _ui.Post(() =>
    {
        if ((DateTime.UtcNow - _lastUiErrorToast).TotalSeconds < 10) return; // nicht bei jedem Folgefehler erneut
        _lastUiErrorToast = DateTime.UtcNow;
        Toast("Ein Fehler wurde abgefangen – Trendo läuft weiter. Einzelheiten stehen unter „System & Sicherheit“ im Absturzprotokoll.");
    });

    void KiPyDone(KiLibraryModel m, Cli.Result r, string resultFile)
    {
        _core.Audit.Append(Principal.LocalOwner.Name, "ki.library.run", $"{m.Id} exit={r.Exit}");
        if (r.Ok && File.Exists(resultFile)) { _kiResult = resultFile; _kiLibMsg = "Fertig. Gespeichert unter: " + resultFile; }
        else _kiLibMsg = "Hat nicht geklappt: " + Short(string.IsNullOrWhiteSpace(r.Err) ? r.Out : r.Err);
        Show("ki-bibliothek");
    }
}
