using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Trendo.Core;

namespace Trendo.App;

/// <summary>Claude baut Level in „Spiele bauen“ (Claude-Ausbau 2, 05.11.2026): Trendo gibt Claude jedes Level und seinen Hintergrund als Textzeilen,
/// Claude schlägt neue Zeilen vor („[[trendo:feld | game-level-2 | …]]“), der Plan zeigt das Level als Vorschau, erst „Ausführen“ ändert es –
/// danach wie überall „Behalten“ oder „Verwerfen“ und ein Rückgängig-Schritt.</summary>
public partial class MainWindow
{
    const string GameLevelField = "game-level-", GameDecorField = "game-decor-";

    /// <summary>Zusätzliche Felder für Claude: Level und Hintergründe des offenen Spiels, dazu ein leeres Feld für ein neues Level.</summary>
    IEnumerable<ClaudeActions.Field> ClaudeExtraFields(string page)
    {
        if (page == "3d") { foreach (var f in Claude3DFields()) yield return f; yield break; } // 3D-Modelle erstellen (Claude-Ausbau 1)
        if (page != GK || ProgramActive(GK) is not { } d || d.Text("browser") is not ("plattform" or "jump" or "breakout")) yield break;
        var plat = d.Text("browser") == "plattform";
        var n = plat ? GameLevelCount(d) : 1;
        for (var i = 0; i < n; i++)
        {
            yield return new(GameLevelField + (i + 1), $"Level {i + 1} „{GameLevelName(d, i)}“", string.Join("/", GameLevelRows(d, i)));
            if (plat) yield return new(GameDecorField + (i + 1), $"Hintergrund Level {i + 1}", string.Join("/", GameDecorRows(d, i)));
        }
        if (plat && n < 20) yield return new(GameLevelField + (n + 1), $"Neues Level {n + 1}", "");
    }

    /// <summary>Für Claude ausführlich (Feldwerte werden im Seitenbericht gekürzt): alle Level und Hintergründe in voller Länge mit Zeichenerklärung.</summary>
    string? ClaudeGameFocus()
    {
        if (ProgramActive(GK) is not { } d || d.Text("browser") is not ("plattform" or "jump" or "breakout")) return null;
        var kind = d.Text("browser"); var plat = kind == "plattform";
        var sb = new System.Text.StringBuilder($"Spiele bauen: Dokument „{d.Title}“, Spielart {kind}. ");
        sb.Append(plat ? "Level-Zeichen: # Boden, . frei, o Münze, E Gegner, ^ Stacheln, S Start (genau eins), Z Ziel, F Sprungfeder, C Speicherpunkt, K Schlüssel, D Tür (öffnet mit allen Schlüsseln). Hintergrund nur Deko: w Wolke, m Berg, t Baum, b Busch, g Gras, . frei. "
            : $"Zeichen: # {(kind == "breakout" ? "Stein" : "Hindernis")}, . frei. ");
        sb.Append("Ein Level änderst du mit [[trendo:feld | game-level-N | Zeile1/Zeile2/…]] (höchstens 16 Zeilen, 200 Spalten), den Hintergrund mit game-decor-N; ein neues Level mit der nächsten freien Nummer.");
        for (var i = 0; i < (plat ? GameLevelCount(d) : 1); i++)
        {
            sb.Append($"\nLevel {i + 1} „{GameLevelName(d, i)}“: {string.Join("/", GameLevelRows(d, i))}");
            if (plat && GameDecorRows(d, i).Any(r => r.Trim('.').Length > 0)) sb.Append($"\nHintergrund {i + 1}: {string.Join("/", GameDecorRows(d, i))}");
        }
        sb.Append(GameTestSummary(d));
        sb.Append("\nSpielst du ein Level als Prüfer durch, nutze die Prüfung oben: knappe Sprünge, Landungen neben Stacheln und unerreichbare Ziele meldest du dem Nutzer und schlägst eine fairere Fassung vor.");
        return sb.ToString();
    }

    /// <summary>Zeilen aus Claudes Wert lesen und prüfen (Zeichen, Größe, Start/Ziel) – bei Fehlern null und der Grund.</summary>
    static (List<string>? Rows, string? Error) GameParseRows(string value, bool decor, string kind)
    {
        var rows = value.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
        if (rows.Count == 0) return (null, "keine Zeilen");
        if (rows.Count > 16 || rows.Any(r => r.Length > 200)) return (null, "höchstens 16 Zeilen und 200 Spalten");
        var allowed = decor ? GameStudio.DecoTiles : kind == "plattform" ? GameStudio.Tiles : "#.";
        if (rows.SelectMany(r => r).FirstOrDefault(ch => !allowed.Contains(ch)) is var bad && bad != default) return (null, $"unbekanntes Zeichen „{bad}“");
        var cols = rows.Max(r => r.Length); rows = [.. rows.Select(r => r.PadRight(cols, '.'))];
        if (!decor && kind == "plattform" && GameStudio.CheckPlatform(rows).FirstOrDefault(x => x.StartsWith("Kein")) is { } miss) return (null, miss);
        return (rows, null);
    }

    /// <summary>Einen Zusatz-Schritt ausführen (Level oder Hintergrund setzen, neues Level anlegen). false = kein Zusatzfeld oder ungültig.</summary>
    bool ClaudeApplyExtra(string page, ClaudeActions.Step s)
    {
        if (page == "3d") return Claude3DApply(s);
        if (page != GK || s.Kind != "feld" || ProgramActive(GK) is not { } d) return false;
        var decor = s.Target.StartsWith(GameDecorField, StringComparison.Ordinal);
        if (!decor && !s.Target.StartsWith(GameLevelField, StringComparison.Ordinal)) return false;
        if (!int.TryParse(s.Target[(decor ? GameDecorField : GameLevelField).Length..], out var no) || no < 1) return false;
        var kind = d.Text("browser"); var count = kind == "plattform" ? GameLevelCount(d) : 1;
        if (string.IsNullOrWhiteSpace(s.Value)) return false;
        var (rows, err) = GameParseRows(s.Value, decor, kind);
        if (rows is null) { _progMsg[GK] = $"Claude-Vorschlag für {(decor ? "Hintergrund" : "Level")} {no} nicht übernommen: {err}."; return false; }
        if (no == count + 1 && !decor && kind == "plattform") { GameAddLevel(d); _gameLevelIdx = no - 1; }
        else if (no > count) return false;
        GameSetRows(d, no - 1, rows, decor ? "Claude: Hintergrund" : "Claude: Level", decor ? "decor" : "rows");
        _gameLevelIdx = no - 1; GameLayer = decor ? "hintergrund" : "spiel"; _gameSection = "Level";
        return true;
    }

    /// <summary>Vorschau in der Plan-Karte: das vorgeschlagene Level klein gezeichnet (vor dem Ausführen).</summary>
    Control? ClaudeExtraPreview(string page, ClaudeActions.Step s)
    {
        if (page == "3d") return Claude3DPreview(s);
        if (page != GK || s.Kind != "feld" || s.Value is not { Length: > 0 } v || ProgramActive(GK) is not { } d) return null;
        var decor = s.Target.StartsWith(GameDecorField, StringComparison.Ordinal);
        if (!decor && !s.Target.StartsWith(GameLevelField, StringComparison.Ordinal)) return null;
        var (rows, err) = GameParseRows(v, decor, d.Text("browser"));
        if (rows is null) return new TextBlock { Name = "claude-plan-warn", Text = "So nicht möglich: " + err, Foreground = Brush.Parse("#FFF8B26A"), FontSize = 12, TextWrapping = TextWrapping.Wrap };
        var cell = Math.Clamp(300.0 / Math.Max(1, rows.Max(r => r.Length)), 6, 14);
        var canvas = new LevelCanvas { Name = "claude-plan-level", Rows = rows, Cell = cell, IsHitTestVisible = false };
        if (int.TryParse(s.Target[(decor ? GameDecorField : GameLevelField).Length..], out var no) && no >= 1 && no <= GameLevelCount(d))
            canvas.Ghost = decor ? GameLevelRows(d, no - 1) : GameDecorRows(d, no - 1);
        return new Border { CornerRadius = new CornerRadius(6), ClipToBounds = true, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, Child = canvas };
    }
}
