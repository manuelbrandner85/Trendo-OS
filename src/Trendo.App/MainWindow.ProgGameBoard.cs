using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Trendo.Core;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>S2 (Modernisierung, freigegeben 07.11.2026): Spiele-Projekt-Dashboard in der Übersicht – Spielvorschau aus dem letzten echten
/// Prüflauf und Aufgaben-Board (Offen / In Arbeit / Fertig) wie in Produktions-Werkzeugen von Spielestudios.</summary>
public partial class MainWindow
{
    private Control GameDashboard(ProgramDocument d) => new WrapPanel { Name = "game-dashboard", Children = { GamePreviewCard(d), GameBoardView(d) } };

    /// <summary>Spielvorschau: Bild des gebauten Spiels (Unreal-Prüflauf), sonst ein klarer nächster Schritt.</summary>
    private Control GamePreviewCard(ProgramDocument d)
    {
        var built = d.Text("built"); var shot = GameBoard.Preview(built);
        var scale = GameDesign.ScaleOf(d.Text("scale"));
        Control body;
        if (shot is not null)
        {
            Bitmap? bmp = null; try { bmp = new Bitmap(shot); } catch (Exception) { /* Bild gesperrt oder beschädigt – Hinweis statt Vorschau */ }
            body = bmp is null ? P("Vorschaubild ließ sich nicht laden.", Brush.Parse("#F5A524"), 12)
                : new Border { CornerRadius = new CornerRadius(10), ClipToBounds = true, Child = new Image { Name = "game-preview-image", Source = bmp, Stretch = Stretch.UniformToFill, Width = 360, Height = 203 } };
        }
        else
        {
            var text = built.Length == 0 ? "Noch nicht gebaut – „Bauen“ legt das Spiel an." : scale.Engine == "browser" ? "Im Browser spielbar – „Spielen“ öffnet es." : "Gebaut – die Vorschau erscheint nach dem ersten Prüflauf in Unreal.";
            body = new Border { Name = "game-preview-empty", Width = 360, Height = 203, CornerRadius = new CornerRadius(10), Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse("#2A2418"), 0), new GradientStop(Color.Parse("#141210"), 1) } }, BorderBrush = EdgeLit, BorderThickness = new Thickness(1),
                Child = new TextBlock { Text = "🎮\n" + text, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Foreground = Text, FontSize = 13, Margin = new Thickness(18), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center } };
        }
        var play = SmallBtn("game-preview-play", built.Length == 0 ? "Bauen" : "Spielen", () => { if (built.Length == 0) GameBuild(d); else GamePlay(d); Show(GK); }, true);
        return new Border { Name = "game-preview", Margin = new Thickness(0, 0, 12, 12), Padding = new Thickness(12), CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), BoxShadow = CardShadow,
            Child = new StackPanel { Spacing = 8, Children = { H("Spielvorschau", 14), body, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { play,
                P(shot is null ? scale.Name : "Prüflauf vom " + File.GetLastWriteTime(shot).ToString("dd.MM. HH:mm"), null, 11.5) } } } } };
    }

    /// <summary>Aufgaben-Board: drei Spalten, Karten mit ◀ ▶ verschieben, × entfernen; neue Aufgabe per Enter; Hinweise und Meilensteine übernehmen.</summary>
    private Control GameBoardView(ProgramDocument d)
    {
        var tasks = GameBoard.Tasks(d.Data);
        var cols = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        for (var c = 0; c < GameBoard.States.Length; c++)
        {
            var state = GameBoard.States[c]; var col = new StackPanel { Spacing = 6, Width = 200 };
            var count = tasks.Count(t => GameBoard.StateOf(t) == state);
            col.Children.Add(new TextBlock { Text = $"{state} · {count}", Foreground = c == 2 ? Brush.Parse("#2FBF71") : c == 1 ? Brush.Parse("#E8B04B") : Text, FontSize = 12.5, FontWeight = FontWeight.SemiBold });
            for (var i = 0; i < tasks.Count; i++)
            {
                if (GameBoard.StateOf(tasks[i]) != state) continue;
                var idx = i; var src = tasks[i]["source"].AsText();
                var row = new DockPanel { LastChildFill = true };
                var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
                if (c > 0) tools.Children.Add(SmallBtn($"game-task-{i}-back", "◀", () => GameBoardEdit(d, "Aufgabe zurück", x => GameBoard.Move(x, idx, -1))));
                if (c < 2) tools.Children.Add(SmallBtn($"game-task-{i}-next", "▶", () => GameBoardEdit(d, "Aufgabe weiter", x => GameBoard.Move(x, idx, +1))));
                tools.Children.Add(SmallBtn($"game-task-{i}-del", "×", () => GameBoardEdit(d, "Aufgabe entfernt", x => GameBoard.Remove(x, idx))));
                DockPanel.SetDock(tools, Avalonia.Controls.Dock.Bottom); row.Children.Add(tools);
                var info = new StackPanel { Spacing = 2, Children = { new TextBlock { Text = tasks[i]["title"].AsText(), Foreground = Text, FontSize = 12.5, TextWrapping = TextWrapping.Wrap },
                    new TextBlock { Text = src, Foreground = Brush.Parse("#9A8F7A"), FontSize = 10.5, IsVisible = src.Length > 0 } } };
                if (GameFlow.IsStory(tasks[i])) info.Children.Add(GameStoryPanel(d, tasks[i], i));
                row.Children.Add(info);
                col.Children.Add(new Border { Name = $"game-task-{i}", Padding = new Thickness(10, 8), CornerRadius = new CornerRadius(10), BorderBrush = EdgeLit, BorderThickness = new Thickness(1),
                    Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                        GradientStops = { new GradientStop(Color.Parse("#2B2721"), 0), new GradientStop(Color.Parse("#1D1A16"), 1) } }, BoxShadow = CardShadow, Child = row });
            }
            cols.Children.Add(col);
        }
        var add = new TextBox { Name = "game-task-new", PlaceholderText = "Neue Aufgabe – Enter", Width = 260 };
        void Commit() { var t = add.Text ?? ""; if (t.Trim().Length > 0) GameBoardEdit(d, "Aufgabe", x => GameBoard.Add(x, t, "eigene")); }
        OnEnter(add, Commit);
        var stories = SmallBtn("game-task-stories", "Stories aus der Spielbibel", () => { var n = 0; GameBoardEdit(d, "Stories angelegt", x => n = GameFlow.Generate(x)); _progMsg[GK] = n > 0 ? $"{n} Story(s) mit Abnahmekriterien angelegt." : "Keine neuen Stories."; Show(GK); });
        var seed = SmallBtn("game-task-seed", "Hinweise und Meilensteine übernehmen", () => { var n = 0; GameBoardEdit(d, "Aufgaben übernommen", x => n = GameBoard.Seed(x)); _progMsg[GK] = n > 0 ? $"{n} Aufgabe(n) übernommen." : "Nichts Neues zu übernehmen."; Show(GK); });
        return new Border { Name = "game-board", Margin = new Thickness(0, 0, 12, 12), Padding = new Thickness(12), CornerRadius = new CornerRadius(14), Background = TileBg, BorderBrush = EdgeLit, BorderThickness = new Thickness(1), BoxShadow = CardShadow,
            Child = new StackPanel { Spacing = 8, Children = { new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { H("Aufgaben", 14), P($"{GameBoard.Progress(d.Data)} % erledigt", null, 11.5) } },
                cols, new WrapPanel { Children = { add, new Border { Width = 8 }, seed, new Border { Width = 8 }, stories } } } } };
    }

    /// <summary>T-0640 E3: Abnahmekriterien einer Story prüfen und die Story abschließen (fertig, mit Hinweisen, blockiert).</summary>
    private Control GameStoryPanel(ProgramDocument d, JsonObject t, int i)
    {
        var sp = new StackPanel { Spacing = 2, Margin = new Thickness(0, 4, 0, 0) };
        var crit = GameFlow.Criteria(t);
        for (var k = 0; k < crit.Count; k++)
        {
            var kk = k; var (text, state) = crit[k];
            var mark = state == GameFlow.Yes ? "✓" : state == GameFlow.No ? "✗" : "○";
            var line = new DockPanel();
            var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
            tools.Children.Add(SmallBtn($"game-task-{i}-c{k}-ok", "✓", () => GameBoardEdit(d, "Kriterium erfüllt", x => GameFlow.Check(x, i, kk, true))));
            tools.Children.Add(SmallBtn($"game-task-{i}-c{k}-no", "✗", () => GameBoardEdit(d, "Kriterium nicht erfüllt", x => GameFlow.Check(x, i, kk, false))));
            DockPanel.SetDock(tools, Avalonia.Controls.Dock.Right); line.Children.Add(tools);
            line.Children.Add(new TextBlock { Name = $"game-task-{i}-c{k}", Text = $"{mark} {text}", FontSize = 11, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center,
                Foreground = state == GameFlow.Yes ? Brush.Parse("#2FBF71") : state == GameFlow.No ? Brush.Parse("#F5A524") : Muted });
            sp.Children.Add(line);
        }
        var result = GameFlow.ResultOf(t);
        if (result.Length > 0)
            sp.Children.Add(new TextBlock { Name = $"game-task-{i}-result", Text = (result == GameFlow.Done ? "✓ Fertig" : result == GameFlow.DoneWithNotes ? "✓ Fertig mit Hinweisen" : "⛔ Blockiert") + (t["note"].AsText() is { Length: > 0 } n ? ": " + n : ""),
                FontSize = 11, TextWrapping = TextWrapping.Wrap, Foreground = result == GameFlow.Blocked ? Brush.Parse("#F06A6A") : Brush.Parse("#2FBF71") });
        if (GameBoard.StateOf(t) != "Fertig")
        {
            var note = new TextBox { Name = $"game-task-{i}-note", PlaceholderText = "Hinweis oder Grund", FontSize = 11, Text = t["note"].AsText() ?? "" };
            sp.Children.Add(note);
            sp.Children.Add(new WrapPanel { Children = {
                SmallBtn($"game-task-{i}-done", "Fertig", () => GameBoardEdit(d, "Story fertig", x => GameFlow.Complete(x, i, GameFlow.Done, note.Text ?? ""))),
                SmallBtn($"game-task-{i}-notes", "Mit Hinweisen", () => GameBoardEdit(d, "Story mit Hinweisen", x => GameFlow.Complete(x, i, GameFlow.DoneWithNotes, note.Text ?? ""))),
                SmallBtn($"game-task-{i}-block", "Blockiert", () => GameBoardEdit(d, "Story blockiert", x => GameFlow.Complete(x, i, GameFlow.Blocked, note.Text ?? ""))) } });
        }
        return sp;
    }

    void GameBoardEdit(ProgramDocument d, string label, Action<JsonObject> change) { d.Edit(label, change); Show(GK); }

    Button SmallBtn(string name, string label, Action click, bool primary = false)
    {
        var b = new Button { Name = name, Content = label, FontSize = 11.5, Padding = new Thickness(primary ? 14 : 8, 4), CornerRadius = new CornerRadius(10),
            Background = primary ? Accent : Brushes.Transparent, Foreground = primary ? Brushes.White : Text, BorderBrush = Edge, BorderThickness = new Thickness(primary ? 0 : 1) };
        b.Click += (_, _) => { try { click(); } catch (Exception ex) { _progMsg[GK] = ex.Message; Show(GK); } };
        return b;
    }
}
