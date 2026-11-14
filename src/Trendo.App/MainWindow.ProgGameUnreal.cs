using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Trendo.Core;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>3D-Spiele in Unreal ohne Fachwissen (Nutzerwunsch 07.11.2026): Welt wählen (3D-Level oder Open World), Trendo kompiliert das C++-Spiel,
/// lässt die Unreal-Tests laufen, startet das Spiel zur Probe (mit Bildschirmfoto) und zeigt einen verständlichen Bericht. Alles über die offiziellen Kommandozeilen.</summary>
public partial class MainWindow
{
    /// <summary>Für Tests: Unreal-Lauf (Programm, Argumente) → (Exitcode, Protokoll) statt echtem Prozess.</summary>
    public Func<string, IReadOnlyList<string>, (int Exit, string Log)>? UnrealRunOverride { get; set; }
    /// <summary>Für Tests: Unreal-Ordner statt Suche.</summary>
    public string? UnrealEngineOverride { get; set; }
    /// <summary>Für Tests: Laden der Fotomaterialien (Zielordner → Anzahl neu geladener Dateien) statt Poly-Haven-Abruf.</summary>
    public Func<string, int>? NatureFetchOverride { get; set; }
    public string LastUnrealReport { get; private set; } = "";
    public bool UnrealBusy { get; private set; }

    string? UnrealEngineDir() => UnrealEngineOverride ?? (_opt.Isolated ? null : GameStudio.FindEngines().FirstOrDefault(e => File.Exists(GameStudio.EditorExe(e)))?.Path);

    (int Exit, string Log) UnrealRun(string exe, IReadOnlyList<string> args, string logFile, int timeoutMs)
    {
        if (UnrealRunOverride is { } o) { var r = o(exe, args); try { File.WriteAllText(logFile, r.Log); } catch (IOException) { } return r; }
        var run = Cli.Run(exe, args, Path.GetDirectoryName(logFile), timeoutMs);
        var log = File.Exists(logFile) ? File.ReadAllText(logFile) : "";
        if (log.Length == 0) { log = run.Out + "\n" + run.Err; File.WriteAllText(logFile, log); }
        return (run.Exit, log);
    }

    /// <summary>Kompilieren, Unreal-Tests, Startlauf – Schritt für Schritt mit Meldung; Bericht im Projektordner „Trendo-Pruefung“.</summary>
    public async Task GameUnrealCheck(ProgramDocument d, bool screenshot = true)
    {
        var built = d.Text("built");
        var uproject = built.Length > 0 && Directory.Exists(built) ? Directory.GetFiles(built, "*.uproject").FirstOrDefault() : null;
        if (uproject is null) { _progMsg[GK] = "Bitte zuerst „Bauen“ – dann prüft Trendo das Unreal-Spiel."; Show(GK); return; }
        if (UnrealEngineDir() is not { } engine) { _progMsg[GK] = "Unreal Engine 5 fehlt. Sie ist kostenlos und kommt über den Epic Games Launcher – danach hier noch einmal „Kompilieren und prüfen“."; Show(GK); return; }
        if (UnrealBusy) return;
        UnrealBusy = true;
        var logs = Path.Combine(built, "Trendo-Pruefung"); Directory.CreateDirectory(logs);
        var lines = new List<string>(); var ok = true;
        void Step(string t) { _progMsg[GK] = t; _ui.Post(() => { if (CurrentView == GK) Show(GK); }); }
        var open = UnrealGame.WorldOf(d.Data) == "openworld";
        var total = open ? 6 : 3; var n = 0;
        string No() => $"{++n}/{total}";
        try
        {
            Step(No() + " Unreal kompiliert das Spiel (C++) … beim ersten Mal einige Minuten.");
            var (ce, clog) = await Task.Run(() => UnrealRun(UnrealGame.BuildBat(engine), UnrealGame.CompileArgs(uproject), Path.Combine(logs, "kompilieren.txt"), 1_800_000));
            if (!UnrealGame.CompileOk(clog)) { ok = false; lines.Add($"✗ Kompilieren fehlgeschlagen (Exitcode {ce}) – Protokoll kompilieren.txt"); }
            else
            {
                lines.Add($"✓ Kompiliert (Exitcode {ce})");
                var welt = open && await GameUnrealWelt(built, uproject, engine, logs, lines, t => Step(No() + " " + t), () => n++);
                ok &= !open || welt;
                // Echter Mensch als Spielfigur (MetaHuman); gelingt es nicht, bleibt die Gliederpuppe – das Spiel läuft trotzdem
                if (welt) await GameUnrealMensch(uproject, engine, logs, lines, t => Step(No() + " " + t));
                else if (open) n++;
                Step(No() + " Unreal-Tests laufen (Daten, Regeln, Spielstand) …");
                var (_, tlog) = await Task.Run(() => UnrealRun(UnrealGame.EditorCmd(engine), UnrealGame.TestArgs(uproject, Path.Combine(logs, "tests.txt")), Path.Combine(logs, "tests.txt"), 900_000));
                var tests = UnrealGame.ParseTests(tlog);
                ok &= tests.Count > 0 && tests.All(t => t.Ok);
                lines.Add(tests.Count == 0 ? "✗ Keine Unreal-Tests gefunden – Protokoll tests.txt" : $"{(tests.All(t => t.Ok) ? "✓" : "✗")} Unreal-Tests: {tests.Count(t => t.Ok)} von {tests.Count} bestanden ({string.Join(", ", tests.Select(t => t.Test.Replace("Trendo.", "") + (t.Ok ? "" : " ✗")))})");
                Step(No() + " Das Spiel startet zur Probe" + (screenshot ? " und macht ein Bild …" : " …"));
                var seed = GameRunSeed(d);
                var checkArgs = UnrealGame.CheckArgs(uproject, Path.Combine(logs, "start.txt"), screenshot && !_opt.Isolated, seed);
                var runStart = DateTime.Now.AddSeconds(-2); // nur Bilder dieses Laufs zählen
                var (_, glog) = await Task.Run(() => UnrealRun(UnrealGame.Editor(engine), checkArgs, Path.Combine(logs, "start.txt"), 900_000));
                var (gok, gtext) = UnrealGame.ParseCheck(glog);
                ok &= gok; lines.Add((gok ? "✓ Spiel läuft: " : "✗ Spiel: ") + gtext);
                // Testspieler-Bot: der Startlauf hat das Spiel angespielt – Prüfpunkte mit Messwert; Auffälliges kommt als Aufgabe ins Board
                var bot = Trendo.Core.Programs.GamePlaytest.Read(glog);
                if (bot.Count > 0)
                {
                    lines.Add($"Testspieler-Bot: {bot.Count(b => b.Ok)} von {bot.Count} Prüfpunkten in Ordnung");
                    lines.AddRange(bot.Select(b => $"  {(b.Ok ? "✓" : "⚠")} {b.Area}: {b.Text}"));
                    var bad = bot.Where(b => !b.Ok).ToList();
                    if (bad.Count > 0) _ui.Post(() => d.Edit("Testspieler", x => { foreach (var b in bad) GameBoard.Add(x, $"{b.Area}: {b.Text}", "Testspieler"); }));
                }
                // T-0650 E5 + E6: Bilder prüfen, Lauf speichern und mit dem vorigen vergleichen
                var withShots = screenshot && (!_opt.Isolated || UnrealRunOverride is not null); // ohne Bild-Lauf keine alten Bilder prüfen
                var run = await Task.Run(() => GameRunRecord(built, glog, checkArgs, seed, Trendo.Core.Programs.GamePlaytest.Shots(built), runStart, withShots));
                LastRunComparison = run.Comparison; ok &= !run.Hard;
                lines.AddRange(run.Lines);
                var fresh = run.Tasks.Where(t => !GameBoard.Tasks(d.Data).Any(x => (string?)x["title"] == t)).Distinct().ToList();
                if (fresh.Count > 0) _ui.Post(() => d.Edit("Testlauf", x => { foreach (var t in fresh) GameBoard.Add(x, t, "Testspieler"); }));
            }
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or System.ComponentModel.Win32Exception) { ok = false; lines.Add("✗ " + e.Message); }
        finally { UnrealBusy = false; }
        LastUnrealReport = (ok ? "Alles bereit – „Spielen“ startet dein 3D-Spiel." : "Nicht alles ging – Details unten.") + "\n" + string.Join("\n", lines);
        File.WriteAllText(Path.Combine(logs, "bericht.txt"), $"{DateTime.Now:yyyy-MM-dd HH:mm} {UnrealGame.WorldOf(d.Data)}\n{LastUnrealReport}\n");
        _core.Audit.Append(Principal.LocalOwner.Name, "game.unreal.check", (ok ? "ok" : "fehler") + " " + Path.GetFileName(built));
        _progMsg[GK] = LastUnrealReport; _ui.Post(() => { if (CurrentView == GK) Show(GK); });
    }

    /// <summary>Open World fotoreal einrichten: freie Fotomaterialien und Modelle (Poly Haven, CC0) laden bzw. aus dem Zwischenspeicher nehmen,
    /// ins Projekt kopieren und Unreal die Welt einrichten lassen (Boden, Pflanzen, Felsen, eigene Karte). Gelingt es, startet das Spiel in dieser Welt.</summary>
    async Task<bool> GameUnrealWelt(string built, string uproject, string engine, string logs, List<string> lines, Action<string> step, Action skip)
    {
        var cache = _opt.Isolated ? Path.Combine(_core.DataRoot, "polyhaven") : UnrealNature.CacheDir(_deps.ToolsRoot); // Tests schreiben nie in den echten Werkzeugordner
        step("Trendo holt freie Fotomaterialien und Pflanzen (Poly Haven, CC0) … beim ersten Mal etwa 275 MB.");
        try
        {
            int got;
            if (NatureFetchOverride is { } f) got = f(cache);
            else if (_opt.Isolated) { lines.Add("• Fotomaterialien: im Testmodus nicht geladen"); skip(); return true; }
            else { var (files, download) = NatureHttp(); got = await Task.Run(() => UnrealNature.Fetch(cache, files, download, t => step(t))); }
            var copied = Directory.Exists(cache) ? UnrealNature.CopyTo(cache, built) : 0;
            _core.Audit.Append(Principal.LocalOwner.Name, "game.unreal.assets", $"polyhaven cc0 neu={got} kopiert={copied}");
            lines.Add($"✓ Fotomaterialien bereit (Poly Haven, CC0; {got} neu geladen, {copied} ins Projekt kopiert)");
        }
        catch (Exception e) when (e is HttpRequestException or IOException or TaskCanceledException or InvalidOperationException or System.Text.Json.JsonException)
        {
            lines.Add("✗ Fotomaterialien nicht geladen: " + e.Message + " – das Spiel bleibt in der einfachen Open World."); skip(); return false;
        }
        // Tiere (freie, geriggte Modelle mit Lizenz) legt der Welt-Bau mit an – sie leben dann in der offenen Welt
        var animals = UnrealGame.CopyAnimals(_opt.Isolated ? Path.Combine(_core.DataRoot, "tiere") : UnrealGame.AnimalsDir(_deps.ToolsRoot), built);
        var packs = UnrealGame.CopyAnimalPacks(_opt.Isolated ? Path.Combine(_core.DataRoot, "tiere-pakete") : UnrealGame.AnimalPacksDir(_deps.ToolsRoot), built);
        var biome = "wald";
        try { biome = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(built, "Content", "TrendoDaten", "level.json")))?["biome"]?.GetValue<string>() ?? biome; } catch (Exception e) when (e is IOException or System.Text.Json.JsonException or InvalidOperationException) { }
        if (animals + packs > 0) lines.Add($"✓ Tiere bereit: {animals} Modell(e) und {packs} Tierpaket(e) – im Spiel nur die, die zur Landschaft „{biome}“ passen (Lizenz liegt dem Spiel bei)");
        step("Unreal richtet die Welt ein (Boden, Pflanzen, Felsen, Tiere, Licht) … einige Minuten.");
        var (we, wlog) = await Task.Run(() => UnrealRun(UnrealGame.EditorCmd(engine), UnrealGame.WeltArgs(uproject, Path.Combine(logs, "welt.txt")), Path.Combine(logs, "welt.txt"), 1_800_000));
        var (wok, models, info) = UnrealGame.ParseWelt(wlog);
        if (!wok) { lines.Add($"✗ Welt nicht eingerichtet (Exitcode {we}) – Protokoll welt.txt" + (info.LastOrDefault(l => l.StartsWith("Fehler")) is { } er ? ": " + er : "")); return false; }
        UnrealGame.UseWeltMap(built);
        lines.Add($"✓ Welt eingerichtet: {models} Pflanzen- und Felsmodelle, " + (info.Any(l => l.StartsWith("Bodenmaterial")) ? "fotorealer Boden" : "Boden") + ", Karte " + UnrealGame.WeltMap);
        return true;
    }

    /// <summary>Spielfigur als echter Mensch (MetaHuman): Unreal baut ihn aus einer Epic-Vorlage (Rig und 4K-Hauttexturen aus dem MetaHuman-Dienst
    /// mit der Anmeldung des Epic Games Launchers) und überträgt die Bewegungen auf sein Skelett. Fehlt etwas, nennt Trendo genau den einen Schritt.</summary>
    async Task GameUnrealMensch(string uproject, string engine, string logs, List<string> lines, Action<string> step)
    {
        if (_opt.Isolated && UnrealRunOverride is null) { lines.Add("• Echter Mensch: im Testmodus nicht gebaut"); return; }
        // Kleidung: Outfits aus dem Werkzeugordner (im Testmodus aus dem Testordner) ins Projekt legen
        var clothes = UnrealGame.CopyClothes(_opt.Isolated ? Path.Combine(_core.DataRoot, "kleidung") : UnrealGame.ClothesDir(_deps.ToolsRoot), Path.GetDirectoryName(uproject)!);
        step("Unreal baut die Menschen des Spiels (MetaHuman: Spielfigur, Gegner, Figur – Haut, Haare, Gesicht, Kleidung, Bewegungen) … etwa 15 Minuten.");
        var (me, mlog) = await Task.Run(() => UnrealRun(UnrealGame.EditorCmd(engine), UnrealGame.MenschArgs(uproject, Path.Combine(logs, "mensch.txt")), Path.Combine(logs, "mensch.txt"), 5_400_000));
        var (mok, info, err) = UnrealGame.ParseMensch(mlog);
        _core.Audit.Append(Principal.LocalOwner.Name, "game.unreal.mensch", mok ? "ok" : "fehler");
        var people = info.Where(l => l.Contains(" gebaut: ")).Select(l => l[..l.IndexOf(" gebaut: ")]).ToList();
        lines.Add(mok ? $"✓ Echte Menschen (MetaHuman): {(people.Count > 0 ? string.Join(", ", people) : "Spielfigur")}"
                      + (info.FirstOrDefault(l => l.StartsWith("Kleidung:")) is { } k ? " – " + k : "")
                      + string.Concat(info.Where(l => l.Contains(": nicht gebaut")).Select(l => " – " + l))
                      + (clothes == 0 ? " – eigene Kleidung: kostenloses MetaHuman-Outfit (.mhpkg) von Fab in den Ordner „" + (_opt.Isolated ? "kleidung" : UnrealGame.ClothesDir(_deps.ToolsRoot)) + "“ legen" : "")
                      : $"• Spielfigur bleibt die Gliederpuppe (Exitcode {me}): {err}");
    }

    /// <summary>Abruf über die offizielle Poly-Haven-API (Netzzugriff nur in der App; Ziele auf dl.polyhaven.org beschränkt).</summary>
    static (Func<string, System.Text.Json.Nodes.JsonObject> Files, Action<string, string> Download) NatureHttp()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(UnrealNature.UserAgent);
        return (id => System.Text.Json.Nodes.JsonNode.Parse(http.GetStringAsync(UnrealNature.FilesUrl(id)).GetAwaiter().GetResult())!.AsObject(),
                (url, dest) =>
                {
                    if (!UnrealNature.AllowedDownload(url)) throw new InvalidOperationException("Unerwartete Download-Adresse: " + url);
                    using var s = http.GetStreamAsync(url).GetAwaiter().GetResult(); using var o = File.Create(dest); s.CopyTo(o);
                });
    }

    /// <summary>Spiel in Unreal starten (ohne Editor).</summary>
    public void GameUnrealPlay(ProgramDocument d)
    {
        var built = d.Text("built");
        var uproject = built.Length > 0 && Directory.Exists(built) ? Directory.GetFiles(built, "*.uproject").FirstOrDefault() : null;
        if (uproject is null) throw new InvalidOperationException("Bitte zuerst „Bauen“.");
        if (UnrealEngineDir() is not { } engine) throw new InvalidOperationException("Unreal Engine 5 fehlt – kostenlos über den Epic Games Launcher.");
        if (UnrealRunOverride is { } o) { o(UnrealGame.Editor(engine), UnrealGame.PlayArgs(uproject)); _progMsg[GK] = "Spiel gestartet."; return; }
        if (_opt.Isolated) { _progMsg[GK] = "Testmodus: Spiel nicht gestartet."; return; }
        var psi = new System.Diagnostics.ProcessStartInfo(UnrealGame.Editor(engine)) { UseShellExecute = false };
        foreach (var a in UnrealGame.PlayArgs(uproject)) psi.ArgumentList.Add(a);
        System.Diagnostics.Process.Start(psi);
        _progMsg[GK] = "Dein 3D-Spiel startet – Esc öffnet die Pause.";
    }

    private Control GameUnrealView(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "game-unreal", Spacing = 6, Margin = new Thickness(0, 10, 0, 0) };
        sp.Children.Add(H("3D-SPIEL IN UNREAL", 12.5));
        var world = UnrealGame.WorldOf(d.Data);
        var pick = new ComboBox { Name = "game-world", ItemsSource = UnrealGame.Worlds.Select(w => w.Label).ToList(), SelectedIndex = Math.Max(0, Array.FindIndex(UnrealGame.Worlds, w => w.Id == world)), MinWidth = 320 };
        pick.SelectionChanged += (_, _) => { var v = UnrealGame.Worlds[Math.Max(0, pick.SelectedIndex)].Id; if (v != world) ProgramRun(GK, () => d.Set("world", v, "Welt")); };
        sp.Children.Add(Row(P("Welt", Muted, 11.5), pick));
        sp.Children.Add(P("„Bauen“ schreibt ein spielbares C++-Spiel mit deinen Leveln, Regeln, Gegnern, Gegenständen, Fahrzeugen, Spielstand und Menü. „Kompilieren und prüfen“ erledigt Unreal für dich: übersetzen, testen, zur Probe starten.", null, 12));
        var check = Act("game-ue-check", UnrealBusy ? "Prüfung läuft …" : "Kompilieren und prüfen", () => _ = GameUnrealCheck(d), true); check.IsEnabled = !UnrealBusy;
        var play = Act("game-ue-play", "Spielen (Unreal)", () => ProgramRun(GK, () => GameUnrealPlay(d)));
        sp.Children.Add(new WrapPanel { Children = { check, new Border { Width = 6 }, play } });
        sp.Children.Add(GameRunsPanel(d)); // T-0650 E6
        var built = d.Text("built");
        if (LastUnrealReport.Length > 0) sp.Children.Add(new TextBlock { Name = "game-ue-report", Text = LastUnrealReport, Foreground = Text, FontFamily = new FontFamily("Consolas, monospace"), FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
        var shot = built.Length > 0 ? Path.Combine(built, "Saved", "Screenshots", "trendo-start.png") : "";
        if (shot.Length > 0 && File.Exists(shot))
            try { sp.Children.Add(new Border { CornerRadius = new CornerRadius(8), ClipToBounds = true, MaxWidth = 560, HorizontalAlignment = HorizontalAlignment.Left, Child = new Image { Name = "game-ue-shot", Source = new Bitmap(shot), Stretch = Stretch.Uniform } }); }
            catch (Exception e) when (e is IOException or ArgumentException) { }
        // Bilder des Testspieler-Bots in Spielreihenfolge (Gegner, Kampf, Nahaufnahme, Wagen …)
        var shots = Trendo.Core.Programs.GamePlaytest.Shots(built).Where(s => !s.Path.EndsWith("trendo-start.png")).ToList();
        if (shots.Count > 0)
        {
            var gal = new WrapPanel { Name = "game-ue-gallery" };
            foreach (var (path, label) in shots)
                try
                {
                    gal.Children.Add(new StackPanel { Margin = new Thickness(0, 0, 8, 8), Spacing = 3, Children = {
                        new Border { CornerRadius = new CornerRadius(8), ClipToBounds = true, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), Child = new Image { Source = new Bitmap(path), Width = 200, Height = 113, Stretch = Stretch.UniformToFill } },
                        new TextBlock { Text = label, Foreground = Text, FontSize = 11.5 } } });
                }
                catch (Exception e) when (e is IOException or ArgumentException) { }
            sp.Children.Add(H("Testspieler-Bot – Bilder aus dem Lauf", 12.5)); sp.Children.Add(gal);
        }
        return sp;
    }
}
