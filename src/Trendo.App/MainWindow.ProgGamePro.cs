using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Programs;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>Spiele bauen wie ein Profi-Studio (Profi-Ausbau 1, 05.11.2026): Phasen-Leiste mit Abnahme, Arbeitsplatz je Rolle, Testmodus mit Fehlerliste und Spieler-Notizen, Ton-Spur.</summary>
public partial class MainWindow
{
    // ───────────── Phasen mit Abnahme ─────────────
    public void GameAcceptPhase(ProgramDocument d, string phase) => d.Edit("Phase abnehmen", x => GamePro.Accept(x, phase));
    public void GameReopenPhase(ProgramDocument d, string phase) => d.Edit("Abnahme zurücknehmen", x => GamePro.Reopen(x, phase));

    /// <summary>Phasen-Leiste: Konzept → … → Release; abgenommene Phasen mit Haken, die aktuelle hervorgehoben, darunter ihre Abnahme-Kriterien.</summary>
    private Control GamePhaseBar(ProgramDocument d, bool details)
    {
        var cur = GamePro.Current(d.Data);
        var bar = new WrapPanel { Name = "game-phases" };
        for (var i = 0; i < GamePro.Phases.Length; i++)
        {
            var p = GamePro.Phases[i]; var ok = GamePro.Accepted(d.Data, p.Id); var on = p.Id == cur.Id && !ok;
            var chip = new Border { Name = "game-phase-" + p.Id, CornerRadius = new CornerRadius(14), Padding = new Thickness(12, 5), Margin = new Thickness(0, 0, 4, 6),
                Background = ok ? Brush.Parse("#2A2FBF71") : on ? Accent : Brushes.Transparent, BorderBrush = ok ? Brush.Parse("#2FBF71") : Edge, BorderThickness = new Thickness(on ? 0 : 1),
                Child = new TextBlock { Text = (ok ? "✓ " : on ? "● " : "○ ") + p.Label, Foreground = on ? Brushes.White : Text, FontSize = 12.5 } };
            ToolTip.SetTip(chip, p.Goal);
            bar.Children.Add(chip);
            if (i < GamePro.Phases.Length - 1) bar.Children.Add(new TextBlock { Text = "→", Foreground = Muted, Margin = new Thickness(2, 4, 6, 0) });
        }
        if (!details) return bar;
        var sp = new StackPanel { Spacing = 4, Children = { bar } };
        var allDone = GamePro.Phases.All(p => GamePro.Accepted(d.Data, p.Id));
        if (allDone) sp.Children.Add(P("✓ Alle Phasen abgenommen – das Spiel ist veröffentlicht.", Brush.Parse("#2FBF71"), 12.5));
        else
        {
            sp.Children.Add(new TextBlock { Text = $"Phase „{cur.Label}“: {cur.Goal}", Foreground = Text, FontSize = 13, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
            var crit = GamePro.Criteria(d.Data, cur.Id);
            var list = new StackPanel { Name = "game-phase-criteria", Spacing = 2 };
            foreach (var (t, ok) in crit) list.Children.Add(P((ok ? "✓ " : "✗ ") + t, ok ? Brush.Parse("#2FBF71") : Brush.Parse("#F5A524"), 12));
            sp.Children.Add(list);
            var accept = Act("game-phase-accept", $"✓ „{cur.Label}“ abnehmen", () => ProgramRun(GK, () => GameAcceptPhase(d, cur.Id)), crit.All(c => c.Ok));
            accept.IsEnabled = crit.All(c => c.Ok);
            ToolTip.SetTip(accept, crit.All(c => c.Ok) ? "Abnahme wie im Studio: die Phase ist geschafft, weiter zur nächsten" : "Erst alle Punkte erfüllen");
            var row = new WrapPanel { Children = { accept } };
            var last = GamePro.Phases.LastOrDefault(p => GamePro.Accepted(d.Data, p.Id));
            if (last is not null) { row.Children.Add(new Border { Width = 6 }); row.Children.Add(Act("game-phase-reopen", $"„{last.Label}“ wieder öffnen", () => ProgramRun(GK, () => GameReopenPhase(d, last.Id)))); }
            sp.Children.Add(row);
        }
        return sp;
    }

    // ───────────── Arbeitsplatz je Rolle ─────────────
    private Control GameRolesPanel(ProgramDocument d)
    {
        var grid = new WrapPanel { Name = "game-roles" };
        foreach (var r in GamePro.Roles)
        {
            var who = new TextBox { Name = "game-role-" + r.Id, Text = d.Data["roles"]?[r.Id].AsText() ?? "", PlaceholderText = "Wer? (z. B. Erika Muster)", Width = 170 };
            var rr = r;
            void Commit() { var v = (who.Text ?? "").Trim(); if (v != (d.Data["roles"]?[rr.Id].AsText() ?? "")) ProgramRun(GK, () => d.Edit("Rolle", x => { var o = x["roles"] as JsonObject ?? []; if (v.Length == 0) o.Remove(rr.Id); else o[rr.Id] = v.Length > 60 ? v[..60] : v; x["roles"] = o; })); }
            who.LostFocus += (_, _) => Commit(); OnEnter(who, Commit);
            var open = Act("game-role-open-" + r.Id, "Arbeitsplatz öffnen", () => { _gameSection = rr.Section; if (rr.Id == "ton" || rr.Id == "programm") GameLayer = "spiel"; Show(GK); });
            grid.Children.Add(new Border { Margin = new Thickness(0, 0, 10, 10), Padding = new Thickness(12), CornerRadius = new CornerRadius(12), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), BoxShadow = CardShadow, Width = 210,
                Child = new StackPanel { Spacing = 5, Children = { new TextBlock { Text = r.Label, Foreground = Text, FontSize = 13.5, FontWeight = FontWeight.SemiBold }, P(r.Task, Muted, 11.5), who, open } } });
        }
        return new StackPanel { Spacing = 6, Children = { H("Team und Rollen", 14), P("Wie in einem Spielestudio: jede Rolle hat ihren Arbeitsplatz. Allein arbeitest du einfach alle Rollen nacheinander ab.", null, 12), grid } };
    }

    // ───────────── Testmodus: Spielbarkeit und Notizen ─────────────
    /// <summary>Notiz oder Fehler aus dem Probespiel: Stelle ist die aktuelle Position der Figur (öffentlich für Tests).</summary>
    public void GameAddTestNote(ProgramDocument d, string text, bool bug)
    {
        int lv = (_gamePlay?.LevelIndex ?? _gameLevelIdx) + 1, x = 0, y = 0;
        if (_gamePlay is { } s) { x = (int)((s.Player.X + 12) / PlatformSim.T); y = (int)((s.Player.Y + 15) / PlatformSim.T); }
        d.Edit(bug ? "Fehler melden" : "Test-Notiz", o => GamePro.AddNote(o, lv, x, y, text, bug));
    }

    public List<GamePro.Report> GameFairness(ProgramDocument d) =>
        [.. Enumerable.Range(0, GameLevelCount(d)).Select(i => GamePro.Fairness(GameLevelRows(d, i), (int)d.Number("speed", 5)))];

    private Control GameTestView(ProgramDocument d)
    {
        var sp = new StackPanel { Spacing = 8 };
        sp.Children.Add(H("Spielbarkeit prüfen", 14));
        sp.Children.Add(P("Trendo spielt jedes Level als Prüfer durch – mit denselben Sprungweiten wie im Spiel – und zeigt, wo es unfair oder unschaffbar wird.", null, 12));
        var reports = GameFairness(d);
        var list = new StackPanel { Name = "game-fair-list", Spacing = 4 };
        for (var i = 0; i < reports.Count; i++)
        {
            var r = reports[i]; var li = i;
            list.Children.Add(new TextBlock { Name = $"game-fair-{i + 1}", Text = $"{GameLevelName(d, i)}: " + (r.GoalReachable ? $"✓ Ziel erreichbar ({r.Jumps} Sprünge)" : "✗ Ziel nicht erreichbar"), Foreground = r.GoalReachable ? Brush.Parse("#2FBF71") : Brush.Parse("#F06A6A"), FontSize = 12.5, FontWeight = FontWeight.SemiBold });
            for (var k = 0; k < r.Findings.Count; k++)
            {
                var f = r.Findings[k];
                var show = Act($"game-fair-show-{i + 1}-{k}", "Zeigen", () => { _gameLevelIdx = li; GameLayer = "spiel"; GameObjSel = (f.X, f.Y); GameTile = LevelCanvas.PickTool; _gameSection = "Level"; Show(GK); });
                list.Children.Add(new DockPanel { Children = { Dk(show, Avalonia.Controls.Dock.Right), P($"{(f.Severe ? "⚠" : "•")} Spalte {f.X + 1}, Zeile {f.Y + 1}: {f.Text}", f.Severe ? Brush.Parse("#F5A524") : Text, 12) } });
            }
        }
        sp.Children.Add(list);
        sp.Children.Add(H("Fehler und Spieler-Notizen", 14));
        sp.Children.Add(P("Beim Probespielen hältst du fest, was auffällt – Trendo merkt sich Level und Stelle. Erledigtes abhaken.", null, 12));
        var notes = d.List("testnotes").OfType<JsonObject>().ToList();
        var nl = new StackPanel { Name = "game-notes", Spacing = 3 };
        for (var i = 0; i < notes.Count; i++)
        {
            var n = notes[i]; var idx = i; var done = n["done"]?.GetValueKind() == System.Text.Json.JsonValueKind.True;
            var cb = new CheckBox { Name = $"game-note-done-{i}", IsChecked = done, Content = new TextBlock { Text = $"{(n["kind"].AsText() == "fehler" ? "🐞 Fehler" : "📝 Notiz")} · Level {n["level"]}, Spalte {(int)ProgramDocument.ToNumber(n["x"]) + 1}: {n["text"].AsText()}", Foreground = done ? Muted : Text, FontSize = 12.5, TextWrapping = TextWrapping.Wrap, TextDecorations = done ? TextDecorations.Strikethrough : null } };
            cb.IsCheckedChanged += (_, _) => { if ((cb.IsChecked == true) != done) ProgramRun(GK, () => d.Edit("Notiz erledigt", x => ((JsonObject)((JsonArray)x["testnotes"]!)[idx]!)["done"] = cb.IsChecked == true)); };
            nl.Children.Add(new DockPanel { Children = { Dk(Act($"game-note-del-{i}", "✕", () => ProgramRun(GK, () => d.Edit("Notiz löschen", x => (x["testnotes"] as JsonArray)?.RemoveAt(idx)))), Avalonia.Controls.Dock.Right), cb } });
        }
        if (notes.Count == 0) nl.Children.Add(P("Noch keine Notizen – im Bereich „Level“ auf „▶ Im Fenster spielen“, dann unten „Notiz“ oder „Fehler“.", Muted, 12));
        sp.Children.Add(nl);
        var open = notes.Count(n => n["kind"].AsText() == "fehler" && n["done"]?.GetValueKind() != System.Text.Json.JsonValueKind.True);
        sp.Children.Add(P(open == 0 ? "✓ Keine offenen Fehler." : $"{open} offene(r) Fehler – Beta kann erst danach abgenommen werden.", open == 0 ? Brush.Parse("#2FBF71") : Brush.Parse("#F5A524"), 12));
        return sp;
    }

    /// <summary>Leiste unter dem Probespiel: Notiz oder Fehler zur aktuellen Stelle festhalten.</summary>
    private Control GamePlayNoteBar(ProgramDocument d)
    {
        var t = new TextBox { Name = "game-play-note", PlaceholderText = "Was fällt auf? (z. B. „Sprung hier zu weit“)", Width = 320 };
        void Add(bool bug) => ProgramRun(GK, () => { GameAddTestNote(d, t.Text ?? "", bug); _progMsg[GK] = bug ? "🐞 Fehler festgehalten – siehe Bereich „Test“." : "📝 Notiz festgehalten – siehe Bereich „Test“."; });
        return new WrapPanel { Name = "game-play-notes", Children = { t, new Border { Width = 6 }, Act("game-play-note-add", "📝 Notiz", () => Add(false)), new Border { Width = 4 }, Act("game-play-bug-add", "🐞 Fehler", () => Add(true)) } };
    }

    // ───────────── Ton-Spur ─────────────
    /// <summary>Geräusch oder Musik für ein Ereignis setzen; leerer Pfad entfernt es (öffentlich für Tests und Claude).</summary>
    public void GameSetSound(ProgramDocument d, string ev, string file)
    {
        if (!GameStudio.SoundEvents.Any(e => e.Id == ev)) throw new ArgumentException($"Unbekanntes Ereignis „{ev}“.");
        if (file.Length > 0)
        {
            if (!File.Exists(file)) throw new FileNotFoundException("Tondatei nicht gefunden.", file);
            if (Path.GetExtension(file).ToLowerInvariant() is not (".mp3" or ".ogg" or ".wav" or ".m4a")) throw new ArgumentException("Bitte eine Tondatei (MP3, OGG, WAV oder M4A).");
        }
        d.Edit("Ton " + ev, x => { var o = x["sounds"] as JsonObject ?? []; if (file.Length == 0) o.Remove(ev); else o[ev] = file; x["sounds"] = o; });
    }

    private Control GameSoundPanel(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "game-sound-panel", Spacing = 4 };
        sp.Children.Add(H("Ton-Spur", 13));
        sp.Children.Add(P("Eigene Geräusche je Ereignis und Musik. Ohne Datei piept das Spiel wie bisher. Geräusche macht auch „Geräusche & Effekte“ oben.", Muted, 11));
        var snd = d.Data["sounds"] as JsonObject ?? [];
        foreach (var (id, label) in GameStudio.SoundEvents)
        {
            var file = snd[id].AsText(); var ev = id;
            var pick = Act($"game-sound-pick-{id}", file is { Length: > 0 } ? "✓ " + Path.GetFileName(file) : "Ton…", async () =>
            {
                var got = PickOverride is { } fake ? await fake(new PickRequest($"game-sound-{ev}", false, false, ["mp3", "ogg", "wav", "m4a"]))
                    : (await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions { Title = "Ton für „" + label + "“", FileTypeFilter = [new Avalonia.Platform.Storage.FilePickerFileType("Töne") { Patterns = ["*.mp3", "*.ogg", "*.wav", "*.m4a"] }] }))
                        .Select(f => Avalonia.Platform.Storage.StorageProviderExtensions.TryGetLocalPath(f)).OfType<string>().ToArray();
                if (got is { Length: > 0 }) ProgramRun(GK, () => GameSetSound(d, ev, got[0]));
            });
            var row = new WrapPanel { Children = { new TextBlock { Text = label, Width = 96, Foreground = Text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, pick } };
            if (file is { Length: > 0 }) row.Children.Add(Act($"game-sound-del-{id}", "✕", () => ProgramRun(GK, () => GameSetSound(d, ev, ""))));
            sp.Children.Add(row);
        }
        return sp;
    }

    /// <summary>Für Claude: Ergebnis der Spielbarkeits-Prüfung und offene Notizen.</summary>
    string GameTestSummary(ProgramDocument d)
    {
        var reps = GameFairness(d);
        var sb = new System.Text.StringBuilder("\nSpielbarkeits-Prüfung (Trendo spielt jedes Level mit den Sprungweiten des Spiels durch):");
        for (var i = 0; i < reps.Count; i++) sb.Append("\n" + GamePro.Describe(reps[i], i + 1));
        var open = d.List("testnotes").OfType<JsonObject>().Where(n => n["done"]?.GetValueKind() != System.Text.Json.JsonValueKind.True).ToList();
        if (open.Count > 0) sb.Append("\nOffene Test-Notizen: " + string.Join(" · ", open.Take(15).Select(n => $"Level {n["level"]}, Spalte {(int)ProgramDocument.ToNumber(n["x"]) + 1}: {n["text"].AsText()}")));
        sb.Append($"\nPhase: {GamePro.Current(d.Data).Label}.");
        return sb.ToString();
    }
}
