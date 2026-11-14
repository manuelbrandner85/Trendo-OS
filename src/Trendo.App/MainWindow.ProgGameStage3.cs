using System.Globalization;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Programs;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>T-0650: Spiele-Studio Stufe 3 in der Oberfläche – Engine einrichten mit ehrlichem Stand (E1), Bildprüfung und Vergleich der Testläufe (E5, E6),
/// gemeinsame Dienst-Auswahl mit Lizenz (E7), ähnliche Spiele mit Beleg (D5).</summary>
public partial class MainWindow
{
    /// <summary>Für Tests: Abruf der Wikipedia-Belege.</summary>
    public IWebFetch? GameRefFetchOverride { get; set; }
    /// <summary>Für Tests: Ist das lokale Werkzeug eines Dienstes eingerichtet (Dienst-Id)?</summary>
    public Func<string, bool>? GameServiceReadyOverride { get; set; }
    /// <summary>Letzter Vergleich zweier Testläufe (für Tests und die Anzeige).</summary>
    public GameRuns.Comparison? LastRunComparison { get; private set; }

    static HttpFetch? _refHttp;
    static HttpFetch RefHttp
    {
        get
        {
            if (_refHttp is null)
            {
                var c = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
                c.DefaultRequestHeaders.UserAgent.ParseAdd("TrendoOS-Spielestudio/1.0"); // Wikipedia verlangt eine Kennung, ohne persönliche Daten
                _refHttp = new HttpFetch(c);
            }
            return _refHttp;
        }
    }

    bool _refBusy; volatile bool _engineBusy; string _refNote = ""; string _svcMsg = "";

    // ───────────── D5: Ähnliche Spiele ─────────────

    public async Task GameReferencesRun(ProgramDocument d)
    {
        if (_refBusy) return;
        _refBusy = true; _refNote = "Sucht ähnliche Spiele und prüft jeden Titel …"; GameRefresh();
        try
        {
            var (text, ki) = await GameAsk(GameReferences.Instructions(), GameReferences.Prompt(d.Data));
            var check = await GameReferences.Verify(GameRefFetchOverride ?? (_opt.Isolated ? new NoWeb() : RefHttp), GameReferences.Candidates(text));
            if (!check.Reached) { _refNote = "⚠ Wikipedia ist nicht erreichbar – nichts geprüft, die bisherigen Einträge bleiben."; return; }
            var refs = check.Refs;
            d.Edit("Ähnliche Spiele", x => GameReferences.Store(x, refs, DateTime.Now));
            _core.Audit.Append(Principal.LocalOwner.Name, "game.referenzen", refs.Count.ToString(CultureInfo.InvariantCulture));
            _refNote = refs.Count > 0 ? $"{refs.Count} belegte Titel, vorgeschlagen von {ki}, geprüft über Wikipedia." : "";
        }
        catch (Exception e) when (e is InvalidOperationException or HttpRequestException or TaskCanceledException or IOException) { _refNote = "⚠ " + e.Message; }
        finally { _refBusy = false; GameRefresh(); }
    }

    sealed class NoWeb : IWebFetch { public Task<WebReply> GetAsync(string url, CancellationToken ct = default) => Task.FromResult(new WebReply(503, [])); }

    Control GameReferencesCard(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "game-refs", Spacing = 5 };
        sp.Children.Add(H("Ähnliche Spiele", 14));
        sp.Children.Add(P("Echte Spiele als Vergleich für deine Entscheidungen. Gezeigt wird nur, was ein Wikipedia-Artikel als Spiel belegt. Nichts davon wird übernommen.", Muted, 12));
        var run = Act("game-refs-run", _refBusy ? "Sucht …" : "Ähnliche Spiele suchen", () => _ = GameReferencesRun(d)); run.IsEnabled = !_refBusy;
        sp.Children.Add(run);
        var refs = GameReferences.Read(d.Data);
        for (var i = 0; i < refs.Count; i++)
        {
            var r = refs[i];
            sp.Children.Add(new DockPanel { Name = $"game-ref-{i}", Children = {
                Dk(Act($"game-ref-{i}-open", "Quelle", () => Launch(r.Url)), Avalonia.Controls.Dock.Right),
                new StackPanel { Children = { new TextBlock { Text = r.Title, Foreground = Text, FontWeight = FontWeight.SemiBold, FontSize = 12.5 }, new TextBlock { Text = $"{r.Note} · {r.Source}", Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap } } } } });
        }
        if (refs.Count == 0 && d.Text("referencesChecked").Length > 0) sp.Children.Add(new TextBlock { Name = "game-refs-none", Text = GameReferences.None, Foreground = Muted, FontSize = 12 });
        if (_refNote.Length > 0) sp.Children.Add(P(_refNote, _refNote.StartsWith('⚠') ? Brush.Parse("#F5A524") : Muted, 11.5));
        return new Border { Padding = new Thickness(12, 10), CornerRadius = new CornerRadius(12), Background = Brush.Parse(C("#14FFFFFF", "#08000000")), BorderBrush = Edge, BorderThickness = new Thickness(1), Child = sp };
    }

    // ───────────── E1: Engine einrichten ─────────────

    GameEngineSetup.Status GameEngineStatus(ProgramDocument d)
    {
        var engine = GameTools.Decide(d.Data, GameToolsHere()).Engine;
        var dir = GameEngineSetup.ProjectDir(engine, d.Text("built"), d.Text("unityProject"), d.Text("godotProject"));
        string? report = null; var stale = false;
        if (dir is not null && engine != "browser" && Path.Combine(dir, "Trendo-Pruefung", "bericht.txt") is var f && File.Exists(f))
            try { stale = GameEngineSetup.Stale(engine, dir, f); report = File.ReadAllText(f); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { report = null; } // nicht lesbar: gilt als nicht geprüft
        return GameEngineSetup.Of(engine, GameEnginesSeen(), dir, report, stale);
    }

    void GameEngineNext(ProgramDocument d, GameEngineSetup.Status s)
    {
        switch (s.Action)
        {
            case "install": Launch(EngineTargets.Engines.First(e => e.Id == s.Engine).Source); break;
            case "build": ProgramRun(GK, () => GameBuild(d)); break;
            case "create": ProgramRun(GK, () => GameToEngine(d, s.Engine)); break;
            case "check":
                if (s.Engine == "unreal") _ = GameUnrealCheck(d);
                else
                {
                    // Engine-Tests dauern bis zu einer Stunde: im Hintergrund, die Seite bleibt bedienbar
                    if (_engineBusy) return;
                    _engineBusy = true; _progMsg[GK] = $"{GameTools.NameOf(s.Engine)} öffnet und baut das Projekt … das dauert einige Minuten."; Show(GK);
                    _ = Task.Run(() =>
                    {
                        try { GameEngineTest(d, s.Engine); }
                        catch (Exception e) { GameEngineSay("Prüfung nicht möglich: " + Trendo.Core.FriendlyError.Say(e)); } // im Hintergrund darf kein Fehler verloren gehen
                        finally { _engineBusy = false; _ui.Post(() => { if (CurrentView == GK) Show(GK); }); }
                    });
                }
                break;
        }
    }

    Control GameEngineSetupCard(ProgramDocument d)
    {
        var s = GameEngineStatus(d);
        var sp = new StackPanel { Name = "game-engine-setup", Spacing = 4, Margin = new Thickness(0, 6, 0, 0) };
        sp.Children.Add(H("ENGINE EINRICHTEN – " + GameTools.NameOf(s.Engine).ToUpperInvariant(), 12.5));
        foreach (var st in s.Steps)
        {
            var (mark, color) = st.State switch
            {
                GameEngineSetup.Checked => ("✓", "#2FBF71"), GameEngineSetup.Done => ("●", "#2FBF71"), GameEngineSetup.Failed => ("✗", "#E5484D"), _ => ("○", "#F5A524"),
            };
            sp.Children.Add(new TextBlock { Name = "game-engine-step-" + st.Id, Text = $"{mark} {st.Label}: {st.State} – {st.Evidence}", Foreground = Brush.Parse(color), FontSize = 12, TextWrapping = TextWrapping.Wrap });
        }
        if (s.Next is { } next)
        {
            sp.Children.Add(new TextBlock { Name = "game-engine-next", Text = "Nächster Schritt: " + next, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap });
            var label = s.Action switch { "install" => "Offizielle Seite öffnen", "build" => "Bauen", "create" => "Projekt anlegen", _ => "Öffnen und bauen prüfen" };
            var b = Act("game-engine-next-go", label, () => GameEngineNext(d, s), true); b.IsEnabled = !(s.Action == "check" && (s.Engine == "unreal" ? UnrealBusy : _engineBusy));
            sp.Children.Add(b);
        }
        else sp.Children.Add(new TextBlock { Name = "game-engine-next", Text = "Alles geprüft.", Foreground = Brush.Parse("#2FBF71"), FontSize = 12 });
        return sp;
    }

    // ───────────── E5 + E6: Testläufe ─────────────

    /// <summary>Startwert der Testläufe: bleibt gleich, damit Läufe vergleichbar sind; „Neuer Startwert“ würfelt neu.</summary>
    int GameRunSeed(ProgramDocument d)
    {
        if (d.Meta["testSeed"] is JsonValue v && v.TryGetValue<int>(out var s) && s > 0) return s;
        var n = GameRuns.NewSeed(); d.EditMeta(m => m["testSeed"] = n); return n;
    }

    public static string GameRunsDir(string built) => Path.Combine(built, "Trendo-Pruefung", "laeufe");

    public sealed record RunOutcome(List<string> Lines, List<string> Tasks, bool Hard, GameRuns.Comparison? Comparison);

    /// <summary>Nach dem Startlauf: Bilder dieses Laufs prüfen (nur ab <paramref name="since"/> geschriebene), Lauf speichern, mit dem vorigen vergleichen.
    /// Hard: ein Bild ist schwarz oder einfarbig – dann gilt der Lauf nicht als bestanden. Fehler beim Speichern enden als Berichtszeile.</summary>
    internal RunOutcome GameRunRecord(string built, string log, IReadOnlyList<string> args, int seed, IEnumerable<(string Path, string Label)> shots, DateTime since, bool withShots)
    {
        var lines = new List<string>(); var tasks = new List<string>(); var hard = false; GameRuns.Comparison? c = null;
        try
        {
            var list = withShots ? shots.Where(s => File.GetLastWriteTime(s.Path) >= since).ToList() : [];
            var root = GameRunsDir(built);
            var before = GameRuns.List(root).FirstOrDefault();
            // E5: jedes Bild prüfen, dazu gegen das Bild desselben Schritts im vorigen Lauf
            var findings = new List<GameShotCheck.Finding>();
            foreach (var (path, label) in list)
            {
                findings.AddRange(GameShotCheck.Check(label, path).Where(f => !f.Ok));
                if (before is not null && before.Seed == seed && before.Shots.TryGetValue(label, out var old) && GameShotCheck.AgainstBefore(label, path, old) is { } hint) findings.Add(hint);
            }
            if (withShots && before is not null && before.Seed == seed)
                foreach (var label in before.Shots.Keys.Where(k => list.All(s => s.Label != k)))
                    findings.Add(new(label, "", "fehlt", false, "Bild fehlt in diesem Lauf (im Vergleichslauf vorhanden) – der Lauf kam vielleicht nicht bis zu diesem Schritt", double.NaN));
            hard = findings.Any(f => f.Kind is "schwarz" or "einfarbig");
            if (withShots)
            {
                lines.Add(list.Count == 0 && findings.Count == 0 ? "⚠ Bildprüfung: in diesem Lauf ist kein Bild entstanden – nichts geprüft" : findings.Count == 0 ? $"✓ Bildprüfung: {list.Count} Bilder unauffällig" : $"{(hard ? "✗" : "⚠")} Bildprüfung: {findings.Count} Befund(e) in {list.Count} Bildern");
                foreach (var f in findings)
                {
                    lines.Add($"  {(f.Kind is "schwarz" or "einfarbig" ? "✗" : "⚠")} {f.Shot}{(f.File.Length > 0 ? $" ({Path.GetFileName(f.File)})" : "")}: {f.Text}");
                    tasks.Add($"Bild {f.Shot}: " + f.Kind switch { "schwarz" => "schwarzes Bild", "einfarbig" => "einfarbiges Bild", "textur" => "fehlende Materialien?", "fehlt" => "fehlt im Lauf", "abweichung" => "weicht vom Vergleichslauf ab", _ => "nicht lesbar" }); // ohne Messwert: kein neuer Eintrag je Lauf
                }
            }
            // E6: Lauf festhalten und vergleichen
            var now = GameRuns.From(log, seed, "unreal", args, list);
            var saved = GameRuns.Save(root, now);
            lines.Add($"Lauf {Path.GetFileName(saved)} gespeichert (Startwert {seed})");
            if (before is null) { lines.Add("  Erster Lauf – beim nächsten Lauf vergleicht Trendo die Messwerte."); return new(lines, tasks, hard, null); }
            c = GameRuns.Compare(before, now);
            lines.Add(!c.Comparable ? $"Vergleich mit {before.Id}: {c.Note}" : c.Changes.Count == 0 ? $"✓ Vergleich mit {before.Id}: keine Veränderung" : $"{(c.Worse > 0 ? "⚠" : "•")} Vergleich mit {before.Id}: {c.Worse} Verschlechterung(en), {c.Changes.Count} Veränderung(en)");
            foreach (var ch in c.Changes.Take(12)) lines.Add($"  {(ch.Worse ? "⚠" : "•")} {ch.What}: {ch.Before} → {ch.After}");
            tasks.AddRange(c.Changes.Where(x => x.Worse).Select(x => $"Verschlechterung: {x.What}"));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException) { lines.Add("⚠ Lauf nicht gespeichert: " + Trendo.Core.FriendlyError.Say(e)); }
        return new(lines, tasks, hard, c);
    }

    Control GameRunsPanel(ProgramDocument d)
    {
        var built = d.Text("built");
        var runs = built.Length > 0 ? GameRuns.List(GameRunsDir(built)) : [];
        var sp = new StackPanel { Name = "game-runs", Spacing = 4, Margin = new Thickness(0, 6, 0, 0) };
        sp.Children.Add(H("WIEDERHOLBARE TESTLÄUFE", 12.5));
        var seed = d.Meta["testSeed"] is JsonValue v && v.TryGetValue<int>(out var sv) && sv > 0 ? sv.ToString(CultureInfo.InvariantCulture) : "wird beim ersten Lauf gewählt";
        sp.Children.Add(new TextBlock { Name = "game-run-seed", Text = $"Startwert {seed} – gleicher Startwert und gleiche Eingaben machen Läufe vergleichbar. {runs.Count} Lauf/Läufe gespeichert.", Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
        sp.Children.Add(Act("game-run-newseed", "Neuer Startwert", () => { d.EditMeta(m => m["testSeed"] = GameRuns.NewSeed()); Show(GK); }));
        return sp;
    }

    // ───────────── E7: Dienste ─────────────

    KiLibraryModel? GameServiceModel(string tool)
    {
        if (!ClaudeTools.Kinds.TryGetValue(tool, out var info)) return null;
        try { var store = KiStore; return KiLibrary.Models.FirstOrDefault(x => x.Kind == info.Kind && store.ModelReady(x) && store.RuntimeReady(x.Runtime)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException) { return null; }
    }

    bool GameServiceReady(GameServices.Provider p)
    {
        if (GameServiceReadyOverride is { } o) return o(p.Id);
        if (p.Id == "blender") return GameEnginesSeen().Any(e => e.Id == "blender" && e.Installed);
        return p.Tool.Length > 0 && GameServiceModel(p.Tool) is not null;
    }

    public async Task GameServiceMake(ProgramDocument d, GameServices.Provider p, string input)
    {
        if (input.Trim().Length == 0) { _svcMsg = "⚠ Bitte beschreiben, was entstehen soll."; GameRefresh(); return; }
        var folder = GameMediaFolder(d); Directory.CreateDirectory(folder);
        var ext = ClaudeTools.Kinds[p.Tool].Ext;
        var file = $"medien/{p.Kind}-{DateTime.Now:yyyyMMdd-HHmmss}{ext}";
        _svcMsg = $"{p.Name} arbeitet …"; GameRefresh();
        var (ok, msg) = await KiToolRun(p.Tool, input.Trim(), folder, file);
        if (!ok) { _svcMsg = "⚠ " + msg; GameRefresh(); return; }
        var full = Path.GetFullPath(Path.Combine(folder, file.Replace('/', Path.DirectorySeparatorChar)));
        var model = GameServiceModel(p.Tool);
        var license = model is null ? "eigene Erzeugung, Modell-Lizenz bitte prüfen" : $"eigene Erzeugung, Modell-Lizenz {model.License}";
        try
        {
            d.Edit($"{p.Name}: {Path.GetFileName(full)}", x => GameServices.Add(x, GameAssetDir(d), full, p, license, model?.Name ?? ""));
            _core.Audit.Append(Principal.LocalOwner.Name, "game.dienst", $"{p.Id} {Path.GetFileName(full)}");
            _svcMsg = $"✓ {Path.GetFileName(full)} ist in der Asset-Liste ({license}).";
        }
        catch (Exception e) when (e is ArgumentException or IOException or InvalidOperationException) { _svcMsg = "⚠ " + e.Message; }
        GameRefresh();
    }

    Control GameServicesCard(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "game-services", Spacing = 6 };
        sp.Children.Add(H("Dienste für Bilder, 3D, Töne, Musik und Sprache", 14));
        sp.Children.Add(P("Erst eingerichtete Werkzeuge auf dem PC, dann freie Quellen. Bezahl-Dienste nur, wenn du sie selbst eingerichtet hast. Alles landet mit Herkunft und Lizenz in der Asset-Liste.", Muted, 12));
        var ready = GameServices.All.ToDictionary(p => p.Id, GameServiceReady); // einmal prüfen, nicht je Art
        foreach (var (kind, label) in GameServices.Kinds)
        {
            var route = GameServices.Route(kind, id => ready.GetValueOrDefault(id));
            var first = route.FirstOrDefault(o => o.Ready);
            var row = new StackPanel { Name = "game-svc-" + kind, Spacing = 3 };
            row.Children.Add(new TextBlock { Text = label + ": " + (first is null ? "noch nichts nutzbar" : first.Provider.Name), Foreground = Text, FontWeight = FontWeight.SemiBold, FontSize = 12.5 });
            row.Children.Add(new TextBlock { Name = $"game-svc-{kind}-route", Text = string.Join("  ›  ", route.Select(o => $"{o.Provider.Name} ({o.Note})")), Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap });
            var acts = new WrapPanel();
            if (route.FirstOrDefault(o => o.Ready && o.Provider.Tool.Length > 0) is { } local)
            {
                var input = new TextBox { Name = $"game-svc-{kind}-input", PlaceholderText = kind == "sprache" ? "Satz zum Sprechen" : "Beschreibung", Width = 300, Margin = new Thickness(0, 0, 6, 4) };
                var p = local.Provider;
                acts.Children.Add(input);
                acts.Children.Add(Act($"game-svc-{kind}-make", "Erzeugen", () => _ = GameServiceMake(d, p, input.Text ?? ""), true));
            }
            else if (route.FirstOrDefault(o => o.Provider.Type == GameServices.Local && !o.Ready && o.Provider.Tool.Length > 0) is { } missing)
                acts.Children.Add(new TextBlock { Name = $"game-svc-{kind}-hint", Text = $"{missing.Provider.Name}: {missing.Provider.How}.", Foreground = Muted, FontSize = 11, Margin = new Thickness(0, 0, 8, 4), VerticalAlignment = VerticalAlignment.Center });
            foreach (var o in route.Where(o => o.Provider.Url.Length > 0))
            {
                var url = o.Provider.Url; var b = Act($"game-svc-{o.Provider.Id}-open", o.Provider.Name + " öffnen", () => Launch(url.EndsWith('=') ? url + Uri.EscapeDataString(d.Text("genre")) : url));
                b.Margin = new Thickness(0, 0, 6, 4); acts.Children.Add(b);
            }
            row.Children.Add(acts);
            sp.Children.Add(row);
        }
        if (_svcMsg.Length > 0) sp.Children.Add(new TextBlock { Name = "game-svc-msg", Text = _svcMsg, Foreground = _svcMsg.StartsWith('⚠') ? Brush.Parse("#F5A524") : Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        return new Border { Padding = new Thickness(12, 10), CornerRadius = new CornerRadius(12), Background = Brush.Parse(C("#14FFFFFF", "#08000000")), BorderBrush = Edge, BorderThickness = new Thickness(1), Child = sp };
    }
}
