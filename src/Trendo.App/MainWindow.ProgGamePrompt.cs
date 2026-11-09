using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Spiele-Prompt im Programm „Spiele bauen“ (Nutzerwunsch 07.11.2026): ein Eingabefeld, spezialisiert auf Spieleentwicklung – jede Spielart per Satz.
/// Mit Claude (eigenes Abo, eingerichtet und angemeldet) arbeitet Claude mit allen Fähigkeiten direkt im Programm; sonst die lokalen bzw. kostenlosen KIs von Trendo.
/// Ergebnis der lokalen KI: Vorschau (was ändert sich), Übernehmen als ein Rückgängig-Schritt, Rückfragen als Knöpfe, Team-Vorschläge mit „Ja“.</summary>
public partial class MainWindow
{
    /// <summary>Für Tests: KI nachbilden (Anweisungen, Auftrag → Antwort).</summary>
    public static Func<string, string, Task<string>>? GamePromptOverride { get; set; }
    string _gpText = ""; bool _gpBusy; bool _gpChange;
    GameDirector.Result? _gpResult; JsonObject? _gpBase; string _gpKi = "";
    /// <summary>Letztes Ergebnis (für Tests): Vorschau-Zeilen der geplanten Änderung.</summary>
    public IReadOnlyList<string> GamePromptPreview => _gpResult?.Doc is { } nd ? GameDirector.Diff(_gpBase ?? new JsonObject(), nd) : [];
    public GameDirector.Result? GamePromptResult => _gpResult;

    /// <summary>Claude mit allen Fähigkeiten, wenn eingerichtet, angemeldet und das Kontingent frei ist; sonst die lokalen KIs.</summary>
    bool GamePromptClaude => _claude.Ready && !CLimit.Active(DateTimeOffset.Now) && GamePromptOverride is null;

    /// <summary>Spiel per Satz entwickeln (<paramref name="change"/>: das offene Spiel ändern statt ein neues anzulegen).</summary>
    public async Task GamePromptRun(string wish, bool change)
    {
        if (_gpBusy || string.IsNullOrWhiteSpace(wish)) return;
        if (await GameCommandRun(wish)) return; // U6: /bild, /sprite, /modell, /musik, /sprache, /video
        var current = change ? ProgramActive(GK) : null;
        if (change && current is null) { _progMsg[GK] = "Zum Ändern zuerst ein Spiel öffnen – oder „Neues Spiel“."; Show(GK); return; }
        _gpText = wish.Trim(); _gpChange = change; _gpResult = null; _gpBase = current?.Data.DeepClone().AsObject();
        var prompt = GameDirector.Prompt(_gpText, _gpBase);
        _core.Audit.Append(Principal.LocalOwner.Name, "game.prompt", change ? "ändern" : "neu");
        if (GamePromptClaude)
        {
            // Claude arbeitet mit allen Fähigkeiten im Claude-Bereich dieses Studios – direkt im Programm (Studio-Dokument), baut, testet und verbessert
            if (current is null) ProgramRun(GK, () => ProgramNew(GK, "leer"));
            if (Windows.Active?.Id is { } id && !ClaudeDockOpen(id)) ClaudeDockToggle("CORE-06");
            _gpKi = ClaudeKi; _progMsg[GK] = "✳ Claude entwickelt dein Spiel mit allen Fähigkeiten – rechts im Claude-Bereich siehst du jeden Schritt.";
            Show(GK);
            await ClaudeChatSend(GameDirector.Instructions(true) + GameExpertBrief(_gpText, current) + "\n\n" + prompt);
            return;
        }
        _gpBusy = true; _progMsg[GK] = "Die KI entwickelt dein Spiel …"; Show(GK);
        try
        {
            string answer;
            if (GamePromptOverride is { } o) { answer = await o(GameDirector.Instructions(false) + GameExpertBrief(_gpText, current), prompt); _gpKi = "Test-KI"; }
            else
            {
                var all = DefaultKiBackends().Where(b => b is not IMainKiBackend).OrderBy(b => b is IRemoteKiBackend ? 0 : 1).ToList();
                var core = new IntelligenceCore(_core.DataRoot, _core.Audit, () => all);
                var a = await core.AskAsync(new KiRequest(prompt, GameDirector.Instructions(false) + GameExpertBrief(_gpText, current), UseKnowledge: false, AllowRemote: true));
                if (!a.Ok) throw new InvalidOperationException("Keine KI hat geantwortet: " + a.Text + " – richte auf der KI-Seite eine kostenlose Internet-KI ein, lade eine KI für diesen PC oder melde Claude an.");
                answer = a.Text; _gpKi = KiLabel(a, all);
            }
            _gpResult = GameDirector.Parse(answer, _gpBase);
            _progMsg[GK] = _gpResult.Error ?? (_gpResult.Doc is null ? "Die KI hat Rückfragen – bitte wählen." : "Vorschau fertig – prüfen und „Übernehmen“.");
        }
        catch (Exception e) when (e is InvalidOperationException or HttpRequestException or TaskCanceledException or IOException) { _progMsg[GK] = e.Message; }
        finally { _gpBusy = false; Show(GK); }
    }

    /// <summary>Vorschau übernehmen: neues Spiel anlegen bzw. das offene ändern – ein Schritt, rückgängig machbar.</summary>
    public void GamePromptAccept()
    {
        if (_gpResult?.Doc is not { } neu) return;
        var d = _gpChange ? ProgramActive(GK) : null;
        if (d is null) { ProgramNew(GK, "leer"); d = ProgramActive(GK)!; }
        var tools = GameToolsHere();
        d.Edit(_gpChange ? "Spiel-Auftrag: " + _gpText : "Spiel aus einem Satz", x => { foreach (var (k, v) in neu) x[k] = v?.DeepClone(); GameTools.Apply(x, tools); });
        _core.Audit.Append(Principal.LocalOwner.Name, "game.prompt.apply", d.Text("title"));
        _progMsg[GK] = $"„{d.Text("title")}“ übernommen – „Bauen“ macht es spielbar. Rückgängig mit Strg+Z.";
        _gpResult = null; _gameSection = "Übersicht"; Show(GK);
    }

    public void GamePromptDiscard() { _gpResult = null; _progMsg[GK] = "Verworfen."; Show(GK); }

    /// <summary>Spiele-Prompt als Karte: Eingabe, „Neues Spiel“, „Ändern“, welche KI arbeitet, Vorschau, Rückfragen, Vorschläge.</summary>
    Control GamePromptBar(ProgramDocument? d)
    {
        var claude = GamePromptClaude;
        var input = new TextBox { Name = "game-prompt", Text = _gpText, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 64, FontSize = 14,
            PlaceholderText = "Beschreibe dein Spiel – z. B. „Ein Open-World-Abenteuer mit einer Kartografin, Pferden und einem Drachen als Boss“ oder „Mach den Boss schwerer“" };
        var ki = new TextBlock { Name = "game-prompt-ki", FontSize = 12, Foreground = claude ? ClaudeAccent : Muted, VerticalAlignment = VerticalAlignment.Center,
            Text = claude ? "✳ Claude – alle Fähigkeiten (dein Abo)" : "💻 Lokale KI von Trendo – mit Claude (Seite „KI“) arbeitet hier Claude mit allen Fähigkeiten" };
        var btnNew = Act("game-prompt-new", _gpBusy ? "KI arbeitet …" : "▶ Neues Spiel", () => GamePromptRun(input.Text ?? "", false), true);
        var btnChange = Act("game-prompt-change", "✎ Dieses Spiel ändern", () => GamePromptRun(input.Text ?? "", true), IsSimple && d is not null);
        var btnTeam = Act("game-prompt-team-run", "🧭 Team-Lauf mit Ablaufplan", () => GameHarnessRun(input.Text ?? ""));
        btnNew.IsEnabled = btnChange.IsEnabled = btnTeam.IsEnabled = !_gpBusy; btnChange.IsVisible = d is not null;
        if (IsSimple)
        {
            input.PlaceholderText = d is null ? "Beschreibe dein Spiel in eigenen Worten, zum Beispiel: Ein Abenteuer mit Pferden und einem Drachen" : "Was soll an deinem Spiel anders werden? Zum Beispiel: Mach den Boss schwerer";
            btnNew.IsVisible = d is null; btnTeam.IsVisible = false;
        }
        var sp = new StackPanel { Spacing = 8, Children = {
            new TextBlock { Text = IsSimple ? (d is null ? "🎮 Was für ein Spiel möchtest du?" : "🎮 Was soll sich ändern?") : "🎮 Spiel-Auftrag – dein Entwicklerteam", FontSize = 16, FontWeight = FontWeight.SemiBold, Foreground = Text },
            IsSimple ? new Border() : P("Jede Spielart: Jump'n'Run, Puzzle, Rennspiel, Horror, Rollenspiel, Open World … Game Director, Design, Story, Level, Grafik, Ton, Test und Produktion arbeiten mit – dazu passende Fachleute aus 53 Spiele-Rollen.", null, 12),
            input,
            IsSimple ? new Border() : GameCouncilRow(input.Text ?? "", d),
            new WrapPanel { Children = { btnNew, new Border { Width = 8 }, btnChange, new Border { Width = 8 }, btnTeam, new Border { Width = 12 }, IsSimple ? new Border() : ki } } } };
        if (_gpResult is { } r)
        {
            if (r.Error is { } err) sp.Children.Add(P("⚠ " + err, Brush.Parse("#F5A524"), 12.5));
            if (r.Doc is { } neu)
            {
                var diff = GameDirector.Diff(_gpBase ?? new JsonObject(), neu);
                var prev = new StackPanel { Name = "game-prompt-preview", Spacing = 2 };
                prev.Children.Add(P($"Vorschau „{neu["title"]}“ · {GameDesign.ScaleOf(neu["scale"]?.ToString() ?? "").Name} · {neu["genre"]} · erstellt von {_gpKi}", Text, 12.5));
                foreach (var l in diff.Take(14)) prev.Children.Add(P(l, Muted, 12));
                if (diff.Count > 14) prev.Children.Add(P($"… und {diff.Count - 14} weitere Änderungen", Muted, 12));
                sp.Children.Add(prev);
                sp.Children.Add(new WrapPanel { Children = { Act("game-prompt-accept", "✓ Übernehmen", GamePromptAccept, true), new Border { Width = 8 }, Act("game-prompt-discard", "✕ Verwerfen", GamePromptDiscard) } });
            }
            var qi = 0;
            foreach (var q in r.Questions)
            {
                var row = new WrapPanel { Name = $"game-prompt-q{qi}" };
                row.Children.Add(P("❓ " + q.Text, Text, 12.5));
                var oi = 0;
                foreach (var opt in q.Options)
                {
                    var answer = $"{_gpText}\n{q.Text} → {opt}";
                    row.Children.Add(Act($"game-prompt-q{qi}-{oi++}", opt, () => GamePromptRun(answer, _gpChange && ProgramActive(GK) is not null)));
                }
                sp.Children.Add(row); qi++;
            }
            var si = 0;
            foreach (var s in r.Suggestions)
            {
                var wish = s;
                sp.Children.Add(Row(P("💡 " + s, Text, 12.5), Act($"game-prompt-s{si++}", "Ja", () => { GamePromptAccept(); return GamePromptRun(wish, true); })));
            }
        }
        if (d is not null)
        {
            // Team-Vorschläge ohne KI: aus der Studio-Prüfung, der Planung und dem letzten Unreal-Prüfbericht – jeder mit „Ja“ als Auftrag an das Team
            var team = GameDirector.Suggest(d.Data, LastUnrealReport);
            if (team.Count > 0)
            {
                var ts = new StackPanel { Name = "game-prompt-team", Spacing = 3, Children = { P("Vorschläge deines Teams", Text, 12.5) } };
                var ti = 0;
                foreach (var t in team.Take(6)) { var wish = t; ts.Children.Add(Row(P("• " + t, Muted, 12), Act($"game-prompt-t{ti++}", "Ja", () => GamePromptRun(wish, true)))); }
                sp.Children.Add(ts);
            }
        }
        return new Border { Name = "game-prompt-card", Padding = new Thickness(16), Margin = new Thickness(0, 0, 0, 10), CornerRadius = new CornerRadius(16),
            Background = TileBg, BorderBrush = claude ? ClaudeAccent : EdgeLit, BorderThickness = new Thickness(1), BoxShadow = CardShadow, Child = sp };
    }
}
