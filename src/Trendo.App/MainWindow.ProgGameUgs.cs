using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>U1–U6 im Spiele-Prompt (Freigabe 07.11.2026, nach UltraGameStudio, MIT): Rat-Modus mit Fachrollen, Team-Lauf mit Ablaufplan,
/// Befehle /bild /sprite /modell /musik /sprache /video über Trendos kostenlose KI-Werkzeuge, Prüfschleife für Bilder.</summary>
public partial class MainWindow
{
    string _gpMode = "auto"; string? _gpCmdMode; string? _gpLastLog; bool _gpNoCmd;
    /// <summary>Für Tests: Bewerter für die Bild-Prüfschleife (Datei, Anweisung → JSON-Urteil). Ohne Bewerter bleibt ein Bild ehrlich „nicht geprüft“.</summary>
    public static Func<string, string, Task<string>>? GameJudgeOverride { get; set; }
    /// <summary>Für Tests: letzter Team-Lauf (Protokollordner) und Rat-Modus.</summary>
    public string? GameHarnessLog => _gpLastLog;
    public string GameCouncilMode => _gpMode;

    /// <summary>Engine des offenen Spiels (für die Auswahl der Engine-Spezialisten).</summary>
    string? GameEngineOf(ProgramDocument? d) => d is null ? null : GameTools.Decide(d.Data, GameToolsHere()).Engine switch { "browser" => "web", var e => e };

    /// <summary>Fachleute für den Auftrag als Zusatz zur Team-Anweisung (lokale KI und Claude).</summary>
    string GameExpertBrief(string wish, ProgramDocument? d)
    {
        var ex = GameExperts.Pick(wish, GameEngineOf(d), _gpMode);
        return "\n\nFachleute für diesen Auftrag (Rat-Modus „" + GameExperts.Modes.First(m => m.Id == _gpMode).Label + "“):\n"
            + string.Join("\n", ex.Select(e => $"- {e.Name} (berichtet an {GameTeam.NameOf(GameTeam.Find(e.Id)?.Lead ?? GameTeam.Head)}): {e.Does}. Leitlinien: {string.Join("; ", e.Rules)}. Grenze: {e.Limit}."))
            + (ex.Count >= 2 ? $"\nWiderspruch zwischen {ex[0].Name} und {ex[1].Name} entscheidet: {GameTeam.NameOf(GameTeam.Escalate(ex[0].Id, ex[1].Id))}." : "")
            + "\nBeziehe ihre Sicht ein und nenne bei wichtigen Entscheidungen, welche Fachperson sie trägt."
            + "\nPrüfschleife für Bilder: Prüfe jedes Bild, das du mit [[trendo:bild …]] bestellst, danach selbst (Datei ansehen) streng nach: " + AssetCheck.JudgeInstruction
            + " Ist es nicht bestanden, bestelle es höchstens zweimal neu mit nachgebessertem Prompt."
            + GameTools.Brief(GameToolsHere());
    }

    /// <summary>T-0630: was auf diesem PC für die automatische Wahl von Engine und Werkzeugen bereitsteht.</summary>
    GameTools.Available GameToolsHere()
    {
        var found = GameEnginesSeen();
        bool Has(string id) => found.Any(e => e.Id == id && e.Installed);
        return new GameTools.Available(Has("unreal"), Has("unity"), Has("godot"), Has("blender"), GamePromptClaude);
    }

    List<EngineFound>? _gameEngines; DateTime _gameEnginesAt;

    /// <summary>Die Suche liest Dateien; beim Neuzeichnen zählt darum das Ergebnis der letzten 30 Sekunden. Ein Fehler beim Suchen stört das Zeichnen nicht.</summary>
    List<EngineFound> GameEnginesSeen()
    {
        if (EngineProbeOverride is not null || _opt.Isolated) return EngineCheck();
        if (_gameEngines is null || DateTime.UtcNow - _gameEnginesAt > TimeSpan.FromSeconds(30))
        {
            try { _gameEngines = EngineCheck(); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or System.Text.Json.JsonException) { _gameEngines ??= []; }
            _gameEnginesAt = DateTime.UtcNow;
        }
        return _gameEngines;
    }

    /// <summary>T-0630: hat das Team beim Plattformer Godot oder Unity gewählt, entsteht das Engine-Projekt einmal mit. Ein Fehler dabei bricht Bauen und Spielen nicht ab.</summary>
    void GameAutoEngine(ProgramDocument d)
    {
        var route = GameTools.Decide(d.Data, GameToolsHere()).Route;
        if (route is not ("godot" or "unity")) return;
        var built = _progMsg[GK];
        if (Directory.Exists(d.Text(route + "Project"))) { _progMsg[GK] = $"{built} Das {GameTools.NameOf(route)}-Projekt besteht schon und bleibt unverändert."; return; }
        try { GameToEngine(d, route); _progMsg[GK] = built + " " + _progMsg[GK]; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException) { _progMsg[GK] = $"{built} Das {GameTools.NameOf(route)}-Projekt wurde nicht angelegt: {e.Message}"; }
    }

    /// <summary>T-0630: sichtbare Zeile, welche Engine und Werkzeuge das Team gewählt hat.</summary>
    TextBlock GameToolLine(ProgramDocument d) => new() { Name = "game-tool-choice", Text = "🧭 " + GameTools.Line(GameTools.Decide(d.Data, GameToolsHere())), FontSize = 12.5, Foreground = Muted, TextWrapping = TextWrapping.Wrap };

    public void GameCouncilSet(string mode) { if (GameExperts.Modes.Any(m => m.Id == mode)) { _gpMode = mode; Show(GK); } }

    /// <summary>Zeile unter dem Eingabefeld: Rat-Modus wählen, wer am Auftrag sitzt, Befehle, Team-Lauf.</summary>
    Control GameCouncilRow(string text, ProgramDocument? d)
    {
        var row = new WrapPanel { Name = "game-council" };
        row.Children.Add(P("Fachleute:", Text, 12));
        foreach (var m in GameExperts.Modes)
        {
            var id = m.Id;
            var b = new Button { Name = "game-council-" + id, Content = m.Label.Split(" – ")[0], FontSize = 11.5, Padding = new Avalonia.Thickness(10, 3), Margin = new Avalonia.Thickness(4, 0, 0, 4),
                Background = _gpMode == id ? Accent : Brushes.Transparent };
            b.Click += (_, _) => GameCouncilSet(id); row.Children.Add(b);
        }
        var who = GameExperts.Pick(text, GameEngineOf(d), _gpMode);
        var sp = new StackPanel { Spacing = 4, Children = { row, new TextBlock { Name = "game-council-who", Text = "Am Auftrag: " + string.Join(", ", who.Select(e => e.Name)), FontSize = 12, Foreground = Muted, TextWrapping = TextWrapping.Wrap } } };
        sp.Children.Add(new TextBlock { Name = "game-commands-help", FontSize = 11.5, Foreground = Muted, TextWrapping = TextWrapping.Wrap,
            Text = (_gpCmdMode is { } cm ? $"Modus {cm} aktiv – jede Eingabe geht dorthin („/{cm}-ende“ beendet). " : "")
                + "Befehle: " + string.Join(" · ", GameCommands.All.Select(c => $"{c.Cmd} {c.Label.Split(' ')[0]}")) + " – mit „-start“/„-ende“ als Modus." });
        return sp;
    }

    /// <summary>Ordner für erzeugte Spiel-Medien: neben der Spieldatei, sonst im Datenordner.</summary>
    string GameMediaFolder(ProgramDocument? d) => d?.Path is { } p ? Path.GetDirectoryName(p)! : Path.Combine(_core.DataRoot, "spiele-medien");

    /// <summary>U6: Befehl erkennen und ausführen. true = es war ein Befehl (oder ein aktiver Modus), sonst false (normaler Spiel-Auftrag).</summary>
    public async Task<bool> GameCommandRun(string input)
    {
        if (_gpNoCmd) return false;
        var c = GameCommands.Parse(input);
        if (c is null && _gpCmdMode is { } mode && !input.TrimStart().StartsWith('/')) c = new GameCommands.Parsed(mode, input.Trim(), null);
        if (c is null) return false;
        if (c.Mode == "start") { _gpCmdMode = c.Kind; _progMsg[GK] = $"Modus „{c.Kind}“ an – jede Eingabe wird ein {GameCommands.All.First(x => x.Kind == c.Kind).Label}. „/{c.Kind}-ende“ beendet."; Show(GK); return true; }
        if (c.Mode == "ende") { _gpCmdMode = null; _progMsg[GK] = $"Modus „{c.Kind}“ beendet."; Show(GK); return true; }
        if (string.IsNullOrWhiteSpace(c.Text)) { _progMsg[GK] = $"Bitte beschreiben, z. B. „/{c.Kind} Ritter mit Schild“."; Show(GK); return true; }
        var d = ProgramActive(GK); var folder = GameMediaFolder(d); Directory.CreateDirectory(folder);
        _core.Audit.Append(Principal.LocalOwner.Name, "game.befehl", c.Kind);
        if (c.Kind == "modell")
        {
            // 3D-Modelle baut Claude in Blender (Paket 01); ohne Claude über den normalen Spiel-Auftrag ans Team
            _gpNoCmd = true;
            try { await GamePromptRun("Baue ein 3D-Modell für das Spiel (Blender, echte Maße, fotorealistisch passend zum Stil): " + c.Text, d is not null); }
            finally { _gpNoCmd = false; }
            return true;
        }
        var (tool, prompt, ext) = c.Kind switch
        {
            "sprite" => ("bild", SpriteSheet.Prompt(c.Text, SpriteSheet.Moves.FirstOrDefault(m => c.Text.Contains(m.Id, StringComparison.OrdinalIgnoreCase)).Id ?? "stehen", SpriteSheet.Moves.FirstOrDefault(m => c.Text.Contains(m.Id, StringComparison.OrdinalIgnoreCase)).Frames is > 0 and var f ? f : 4), ".png"),
            "sprache" => ("vorlesen", c.Text, ".wav"),
            "musik" => ("musik", c.Text, ".mp3"),
            "video" => ("video", c.Text, ".webm"),
            _ => ("bild", c.Text, ".png"),
        };
        var stem = $"medien/{c.Kind}-{DateTime.Now:yyyyMMdd-HHmmss}";
        _progMsg[GK] = $"{GameCommands.All.First(x => x.Kind == c.Kind).Label} wird erzeugt (kostenlose KI auf diesem PC) …"; Show(GK);
        if (tool == "bild")
        {
            // U4: Prüfschleife – erzeugen, bewerten, Prompt nachbessern (höchstens 3 Versuche)
            Func<string, string, CancellationToken, Task<string>>? judge = GameJudgeOverride is { } j ? (f, q, _) => j(f, q) : null;
            IReadOnlyList<AssetCheck.Attempt> tries;
            try { tries = await AssetCheck.Loop(prompt, async (p, i, _) => { var file = $"{stem}-{i}{ext}"; var (ok, msg) = await KiToolRun(tool, p, folder, file); return ok ? file : throw new InvalidOperationException(msg); }, judge); }
            catch (InvalidOperationException e) { _progMsg[GK] = e.Message; Show(GK); return true; }
            var last = tries[^1];
            _progMsg[GK] = last.Verdict is null ? $"Fertig: {last.File} (nicht automatisch geprüft – mit Claude prüft das Team jedes Bild selbst)."
                : last.Verdict.Pass ? $"Fertig: {last.File} – Prüfung bestanden ({last.Verdict.Score}/100) nach {tries.Count} Versuch(en)."
                : $"{last.File}: Prüfung nach {tries.Count} Versuchen nicht bestanden ({last.Verdict.Score}/100) – Mängel: {string.Join(", ", last.Verdict.Defects)}.";
        }
        else
        {
            var (ok, msg) = await KiToolRun(tool, prompt, folder, stem + ext);
            _progMsg[GK] = ok ? $"Fertig: {stem}{ext} im Spielordner." : msg;
        }
        Show(GK); return true;
    }

    /// <summary>U3: Team-Lauf mit Ablaufplan – Strategien, Fachrollen, Gegenprüfung und Abnahme; Protokoll im Spielordner (.trendo-lauf).</summary>
    public async Task GameHarnessRun(string wish)
    {
        if (_gpBusy || string.IsNullOrWhiteSpace(wish)) return;
        var d = ProgramActive(GK); var plan = GameHarness.Build(wish.Trim(), GameEngineOf(d), _gpMode);
        _gpBusy = true; _progMsg[GK] = $"Team-Lauf: {string.Join(" → ", plan.Strategies.Select(s => GameHarness.Strategies.First(x => x.Id == s).Label))} · {plan.Steps.Count} Schritte …"; Show(GK);
        _core.Audit.Append(Principal.LocalOwner.Name, "game.teamlauf", string.Join(",", plan.Strategies));
        try
        {
            async Task<string> Ask(string role, string q, CancellationToken ct)
            {
                if (GamePromptOverride is { } o) return await o(role, q);
                var all = DefaultKiBackends().Where(b => b is not IMainKiBackend).OrderBy(b => b is IRemoteKiBackend ? 0 : 1).ToList();
                var a = await new IntelligenceCore(_core.DataRoot, _core.Audit, () => all).AskAsync(new KiRequest(q, role, UseKnowledge: false, AllowRemote: true));
                if (!a.Ok) throw new InvalidOperationException("Keine KI hat geantwortet – richte auf der KI-Seite eine kostenlose KI ein oder melde Claude an.");
                return a.Text;
            }
            var (ok, _, log) = await GameHarness.Run(plan, Ask, GameMediaFolder(d));
            _gpLastLog = log;
            _progMsg[GK] = ok ? $"Team-Lauf fertig, Abnahme bestanden – Ergebnis und Protokoll: {log}" : $"Team-Lauf fertig, Abnahme nicht bestanden – offene Mängel im Protokoll: {log}";
        }
        catch (Exception e) when (e is InvalidOperationException or HttpRequestException or TaskCanceledException or IOException) { _progMsg[GK] = e.Message; }
        finally { _gpBusy = false; Show(GK); }
    }
}
