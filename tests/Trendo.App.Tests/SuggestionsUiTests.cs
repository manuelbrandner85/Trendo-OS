using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Media;
using Trendo.Core.Sys;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Masterprompt 131: Programme schlagen vor, Ja führt aus (mit Rückgängig), Nein wird gemerkt.</summary>
public class Suggestions_Ui
{
    static void Click(MainWindow w, string name) { var b = V<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, V<Button>(w, name)); }
    static readonly DateTime Mo = new(2026, 11, 2, 8, 10, 0);

    [AvaloniaFact(DisplayName = "Vorschläge UI: Kalender plant fällige Aufgabe ein (Ja, Rückgängig), Aufgaben aus E-Mails (Nein bleibt gemerkt), Abläufe, Videoschnitt-Stille")]
    public void Suggest_yes_no()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1000; w.SuggestNow = () => Mo;
        w.Cal.AddTodo(new TodoItem("a", "Steuer abgeben", Mo.Date.AddDays(3), Priority: 1));
        w.CalDay = Mo; w.CalMode = "woche"; w.Show("kalender"); Settle();
        Assert.Contains("Steuer abgeben", V<TextBlock>(w, "sugg-title-0").Text);
        Click(w, "sugg-yes-0");
        var ev = Assert.Single(w.Cal.All()); Assert.Equal(Mo.Date.AddHours(9), ev.Start); Assert.Equal("aufgabe:a", ev.Notes);
        Assert.Null(w.TryFind<Button>("sugg-yes-0")); // erledigt, kein zweiter Vorschlag
        w.CalUndo(); Assert.Empty(w.Cal.All());
        // Aufgaben aus E-Mails: Nein für die erste, Ja für die zweite
        w.Inbox = [new(11, "Vermietung Muster <info@example.de>", "Widerspruch bis 15.11. möglich", Mo, false), new(12, "Schule", "Bitte Elternbrief unterschreiben", Mo, false)];
        w.Show("aufgaben"); Settle();
        Click(w, "sugg-no-0"); Assert.True(w.SuggestionDismissed("mail:11"));
        Assert.Contains("Elternbrief", V<TextBlock>(w, "sugg-title-0").Text);
        Click(w, "sugg-yes-0"); Assert.Contains(w.Cal.Todos(), t => t.Title.Contains("Elternbrief") && t.List == "E-Mail");
        w.Show("aufgaben"); Settle(); Assert.Null(w.TryFind<Button>("sugg-yes-0")); // beide erledigt, auch nach neuem Öffnen
        // Abläufe: volle Downloads → Vorlage vorschlagen
        var home = Path.Combine(root, "heim"); Directory.CreateDirectory(Path.Combine(home, "Downloads")); MainWindow.WorkflowHome = home;
        for (var i = 0; i < 6; i++) File.WriteAllText(Path.Combine(home, "Downloads", $"r{i}.pdf"), "x");
        w.Show("ablaeufe"); Settle(); Click(w, "sugg-yes-0");
        Assert.Single(w.Workflows.All()); Assert.Equal(w.Workflows.All()[0].Id, w.WfSelected);
        // Videoschnitt: Stille finden (FFmpeg ersetzt) → Ja schneidet heraus, mit Rückgängig
        var f = Path.Combine(root, "rede.mp4"); File.WriteAllBytes(f, [1]);
        w.SilenceProbe = _ => "silence_start: 3\nsilence_end: 5\n";
        w.Show("schnitt"); Settle(); RealClick(w, V<Button>(w, "prog-tpl-leer")); w.EditAdd(f, length: 10, audio: true); w.Show("schnitt"); Settle();
        Click(w, "prog-cmd-ve-silence"); Settle();
        Assert.Contains("Stille", V<TextBlock>(w, "sugg-title-0").Text);
        Click(w, "sugg-yes-0");
        var d = w.ProgramActive("schnitt")!; Assert.Equal(2, VideoEdit.Clips(d.Data).Count); Assert.Equal(8, VideoEdit.Duration(d.Data), 3);
        Assert.True(d.Undo()); Assert.Single(VideoEdit.Clips(d.Data));
    }
}
