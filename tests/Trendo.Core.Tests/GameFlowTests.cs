using System.Text.Json.Nodes;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0640: Team mit Hierarchie (E2), Ablauf bis zur Abnahme (E3), wählbare Prüfstufe (E4).</summary>
public class GameFlowTests
{
    static JsonObject Doc()
    {
        var d = GameDesign.Templates.First(t => t.Id == "topdown").Bible();
        d["audience"] = "Kinder ab 8"; // Konzept-Kriterien erfüllt
        return d;
    }

    static int Story(JsonObject d, string titleStart) => GameBoard.Tasks(d).FindIndex(t => ((string?)t["title"] ?? "").StartsWith(titleStart, StringComparison.Ordinal));

    static void CheckAll(JsonObject d, int i, bool ok = true) { for (var k = 0; k < GameFlow.Criteria(GameBoard.Tasks(d)[i]).Count; k++) GameFlow.Check(d, i, k, ok); }

    // ───────────── E2 ─────────────

    [Fact(DisplayName = "Team: drei Ebenen unter dem Game Director, jede Fachrolle genau einmal, jede Leitung existiert")]
    public void Tree_three_levels()
    {
        var tree = GameTeam.Tree();
        Assert.Equal(GameExperts.All.Count, tree.Count);
        Assert.Equal(tree.Count, tree.Select(m => m.Id).Distinct().Count());
        Assert.Equal([0, 1, 2], tree.Select(m => m.Level).Distinct().Order());
        Assert.All(tree.Where(m => m.Level == 0), m => Assert.Equal(GameTeam.Head, m.Lead));
        Assert.All(tree.Where(m => m.Level > 0), m => Assert.Equal(m.Level - 1, tree.Single(x => x.Id == m.Lead).Level));
    }

    [Fact(DisplayName = "Team: Widerspruch geht an die nächste gemeinsame Leitung, zwischen Bereichen bis zum Game Director")]
    public void Escalation()
    {
        Assert.Equal("game-designer", GameTeam.Escalate("level-designer", "systems-designer")); // gleiche Abteilung
        Assert.Equal("creative-director", GameTeam.Escalate("level-designer", "animator")); // Design und Grafik
        Assert.Equal(GameTeam.Head, GameTeam.Escalate("level-designer", "gameplay-programmer")); // Kreativ und Technik
        Assert.Equal("lead-programmer", GameTeam.Escalate("unreal-specialist", "gameplay-programmer")); // Engine gehört zur Programmierung
        Assert.Equal("game-designer", GameTeam.Escalate("game-designer", "level-designer")); // die Leitung entscheidet über ihre Fachleute
        Assert.Equal(GameTeam.Head, GameTeam.Escalate("creative-director", "producer"));
        Assert.Equal("Game Director", GameTeam.NameOf(GameTeam.Head));
    }

    [Fact(DisplayName = "Team: Aufbau steht in der Anweisung an die KI, Entscheidungen als Frage mit 2 bis 4 Antworten")]
    public void Team_in_instructions()
    {
        var text = GameDirector.Instructions(false);
        Assert.Contains("nächste gemeinsame Leitung", text); Assert.Contains("2 bis 4 Antworten", text); Assert.Contains("Kreativdirektor", text);
    }

    [Fact(DisplayName = "Team: Frage mit nur einer Antwort ist keine Entscheidung; höchstens vier Antworten; ohne Spielbibel kein Dokument")]
    public void Decisions_need_options()
    {
        var r = GameDirector.Parse("Frage: Welche Engine? [Godot]\nFrage: Welcher Stil? [Pixel | Comic]\nFrage: Wie lang? [1 | 2 | 3 | 4 | 5]");
        Assert.Equal(2, r.Questions.Count);
        Assert.Equal(["Pixel", "Comic"], r.Questions[0].Options);
        Assert.Equal(4, r.Questions[1].Options.Count);
        Assert.Null(r.Doc); // erst nach der Wahl entsteht etwas
    }

    // ───────────── E3 ─────────────

    [Fact(DisplayName = "Stories: aus der Spielbibel entstehen Epics und Stories mit Kriterien, ohne Doppelte")]
    public void Generate_stories()
    {
        var d = Doc();
        var n = GameFlow.Generate(d);
        Assert.True(n >= 6, $"Stories: {n}");
        var stories = GameBoard.Tasks(d).Where(GameFlow.IsStory).ToList();
        Assert.Contains(stories, t => (string?)t["epic"] == "Figuren" && ((string)t["title"]!).Contains("Moosgeist"));
        Assert.Contains(GameFlow.Criteria(stories.First(t => ((string)t["title"]!).Contains("Moosgeist"))), c => c.Text.Contains("2 bis 4"));
        Assert.All(stories, t => { Assert.NotEmpty(GameFlow.Criteria(t)); Assert.All(GameFlow.Criteria(t), c => Assert.Equal(GameFlow.Open, c.State)); });
        Assert.Equal(0, GameFlow.Generate(d));
    }

    [Fact(DisplayName = "Stories: fertig erst mit geprüften und erfüllten Kriterien; Hinweise und Blockiert brauchen eine Notiz")]
    public void Complete_rules()
    {
        var d = Doc(); GameFlow.Generate(d); var i = Story(d, "Kernspiel");
        Assert.Throws<InvalidOperationException>(() => GameFlow.Complete(d, i, GameFlow.Done));
        Assert.Throws<InvalidOperationException>(() => GameBoard.Move(d, i, 2)); // nicht am Abnehmen vorbei
        CheckAll(d, i); GameFlow.Check(d, i, 1, false);
        Assert.Throws<InvalidOperationException>(() => GameFlow.Complete(d, i, GameFlow.Done));
        Assert.Throws<InvalidOperationException>(() => GameFlow.Complete(d, i, GameFlow.DoneWithNotes));
        GameFlow.Complete(d, i, GameFlow.DoneWithNotes, "Kernschleife noch vage");
        Assert.Equal("Fertig", GameBoard.StateOf(GameBoard.Tasks(d)[i]));
        GameFlow.Check(d, i, 1, true); // Änderung öffnet die Story wieder
        Assert.Equal("", GameFlow.ResultOf(GameBoard.Tasks(d)[i])); Assert.Equal("In Arbeit", GameBoard.StateOf(GameBoard.Tasks(d)[i]));
        GameFlow.Complete(d, i, GameFlow.Done);
        Assert.Equal(GameFlow.Done, GameFlow.ResultOf(GameBoard.Tasks(d)[i]));
        GameBoard.Move(d, i, -1); // zurückschieben öffnet sie
        Assert.Equal("", GameFlow.ResultOf(GameBoard.Tasks(d)[i]));
        var j = Story(d, "Figur");
        Assert.Throws<InvalidOperationException>(() => GameFlow.Complete(d, j, GameFlow.Blocked));
        GameFlow.Complete(d, j, GameFlow.Blocked, "Animationen fehlen");
        Assert.Equal("In Arbeit", GameBoard.StateOf(GameBoard.Tasks(d)[j]));
    }

    [Fact(DisplayName = "Board: gewöhnliche Aufgaben ohne Kriterien laufen wie bisher bis Fertig")]
    public void Plain_tasks_unchanged()
    {
        var d = new JsonObject(); GameBoard.Add(d, "Boss bauen");
        GameBoard.Move(d, 0, 1); GameBoard.Move(d, 0, 1);
        Assert.Equal("Fertig", GameBoard.StateOf(GameBoard.Tasks(d)[0]));
    }

    [Fact(DisplayName = "Tore (normal): nicht geprüft, bestanden, Bedenken nur mit Risiko, blockiert = durchgefallen")]
    public void Gates_normal()
    {
        var d = Doc(); GameFlow.SetRigor(d, "normal"); GameFlow.Generate(d); var i = Story(d, "Kernspiel");
        Assert.Equal(GameFlow.NotAssessed, GameFlow.Judge(d, "konzept").Verdict);
        Assert.Throws<InvalidOperationException>(() => GameFlow.Accept(d, "konzept"));
        CheckAll(d, i); GameFlow.Check(d, i, 2, false); GameFlow.Complete(d, i, GameFlow.DoneWithNotes, "nur zwei Mechaniken");
        Assert.Equal(GameFlow.Concerns, GameFlow.Judge(d, "konzept").Verdict);
        Assert.Throws<InvalidOperationException>(() => GameFlow.Accept(d, "konzept"));
        GameFlow.Accept(d, "konzept", "Dritte Mechanik kommt im Prototyp");
        Assert.True(GamePro.Accepted(d, "konzept"));
        Assert.Equal("Dritte Mechanik kommt im Prototyp", (string?)d["risks"]!["konzept"]);

        var e = Doc(); GameFlow.SetRigor(e, "normal"); GameFlow.Generate(e); var k = Story(e, "Kernspiel");
        GameFlow.Complete(e, k, GameFlow.Blocked, "Idee unklar");
        Assert.Equal(GameFlow.Failed, GameFlow.Judge(e, "konzept").Verdict);
        CheckAll(e, k); GameFlow.Complete(e, k, GameFlow.Done);
        Assert.Equal(GameFlow.Passed, GameFlow.Judge(e, "konzept").Verdict);
        e["logline"] = "";
        Assert.Equal(GameFlow.Failed, GameFlow.Judge(e, "konzept").Verdict); // Phasen-Kriterium offen
    }

    [Fact(DisplayName = "Tore (schlank): wie bisher nur die Phasen-Kriterien, Stories zählen nicht")]
    public void Gates_lean()
    {
        var d = Doc(); GameFlow.Generate(d);
        Assert.Equal("schlank", GameFlow.RigorOf(d));
        Assert.Equal(GameFlow.Passed, GameFlow.Judge(d, "konzept").Verdict);
        GameFlow.Accept(d, "konzept");
        Assert.True(GamePro.Accepted(d, "konzept"));
        Assert.Throws<InvalidOperationException>(() => GameFlow.Accept(d, "alpha")); // Reihenfolge bleibt
    }

    [Fact(DisplayName = "Tore (streng): kein Fertig mit Hinweisen, und eine Story mit Hinweisen lässt die Phase durchfallen")]
    public void Gates_strict()
    {
        var d = Doc(); GameFlow.SetRigor(d, "normal"); GameFlow.Generate(d); var i = Story(d, "Kernspiel");
        CheckAll(d, i); GameFlow.Complete(d, i, GameFlow.DoneWithNotes, "kleine Lücke");
        GameFlow.SetRigor(d, "streng");
        Assert.Equal(GameFlow.Failed, GameFlow.Judge(d, "konzept").Verdict);
        Assert.Throws<InvalidOperationException>(() => GameFlow.Accept(d, "konzept", "trotzdem"));
        GameFlow.Check(d, i, 0, true); // wieder offen
        Assert.Throws<InvalidOperationException>(() => GameFlow.Complete(d, i, GameFlow.DoneWithNotes, "erneut"));
        GameFlow.Complete(d, i, GameFlow.Done);
        GameFlow.Accept(d, "konzept");
        Assert.True(GamePro.Accepted(d, "konzept"));
    }

    [Fact(DisplayName = "Stories: erneutes Erzeugen nach fertigen Stories legt nichts doppelt an, nur Neues")]
    public void Regenerate_keeps_done()
    {
        var d = Doc(); GameFlow.Generate(d); var i = Story(d, "Kernspiel"); CheckAll(d, i); GameFlow.Complete(d, i, GameFlow.Done);
        var count = GameBoard.Tasks(d).Count;
        ((JsonArray)d["characters"]!).Add(new JsonObject { ["name"] = "Krähe", ["role"] = "Begleiter", ["text"] = "Späherin" });
        Assert.Equal(1, GameFlow.Generate(d));
        Assert.Equal(count + 1, GameBoard.Tasks(d).Count);
        Assert.Single(GameBoard.Tasks(d), t => ((string)t["title"]!).StartsWith("Kernspiel", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Stories: leere oder als Text geschriebene Kriterien hebeln die Abnahme nicht aus")]
    public void Broken_criteria()
    {
        var d = new JsonObject { ["tasks"] = new JsonArray(
            new JsonObject { ["title"] = "Leer", ["state"] = "Offen", ["criteria"] = new JsonArray() },
            new JsonObject { ["title"] = "Text", ["state"] = "Offen", ["criteria"] = new JsonArray("A", 5, new JsonObject { ["text"] = "C", ["state"] = "ja" }) }) };
        Assert.Throws<InvalidOperationException>(() => GameFlow.Complete(d, 0, GameFlow.Done));
        GameFlow.Complete(d, 0, GameFlow.Blocked, "Kriterien fehlen");
        var t = GameBoard.Tasks(d)[1];
        Assert.Equal([("A", GameFlow.Open), ("(unlesbar)", GameFlow.Open), ("C", GameFlow.Yes)], GameFlow.Criteria(t));
        Assert.Throws<InvalidOperationException>(() => GameFlow.Complete(d, 1, GameFlow.Done));
        GameFlow.Check(d, 1, 0, true); GameFlow.Check(d, 1, 1, true); // Texte werden zu Einträgen
        Assert.All(GameFlow.Criteria(GameBoard.Tasks(d)[1]), c => Assert.Equal(GameFlow.Yes, c.State));
        GameFlow.Complete(d, 1, GameFlow.Done);
        Assert.Equal("Fertig", GameBoard.StateOf(GameBoard.Tasks(d)[1]));
    }

    [Fact(DisplayName = "Board: ein Eintrag, der keine Aufgabe ist, verschiebt keine Knöpfe auf eine andere Story")]
    public void Foreign_entries_do_not_shift()
    {
        var d = Doc(); GameFlow.Generate(d);
        ((JsonArray)d["tasks"]!).Insert(0, JsonValue.Create("kaputt"));
        var shown = GameBoard.Tasks(d); var i = shown.FindIndex(t => ((string)t["title"]!).StartsWith("Kernspiel", StringComparison.Ordinal));
        GameFlow.Check(d, i, 0, true);
        Assert.Equal(GameFlow.Yes, GameFlow.Criteria(GameBoard.Tasks(d)[i])[0].State);
        Assert.StartsWith("Kernspiel", (string)GameBoard.Tasks(d)[i]["title"]!);
    }

    [Fact(DisplayName = "Tore: Risiko-Notiz wird beim Wiederöffnen und bei späterem Bestehen entfernt")]
    public void Risks_cleaned()
    {
        var d = Doc(); GameFlow.SetRigor(d, "normal"); GameFlow.Generate(d); var i = Story(d, "Kernspiel");
        CheckAll(d, i); GameFlow.Check(d, i, 2, false); GameFlow.Complete(d, i, GameFlow.DoneWithNotes, "Lücke");
        GameFlow.Accept(d, "konzept", "angenommen");
        Assert.Equal("angenommen", GameFlow.Risks(d)["konzept"]);
        GameFlow.Reopen(d, "konzept");
        Assert.Empty(GameFlow.Risks(d));
        GameFlow.Check(d, i, 2, true); GameFlow.Complete(d, i, GameFlow.Done);
        GameFlow.Accept(d, "konzept");
        Assert.Empty(GameFlow.Risks(d));
    }

    // ───────────── E4 ─────────────

    [Fact(DisplayName = "Prüfstufe: je Stufe genau die vorgesehenen Prüfungen")]
    public void Checks_per_rigor()
    {
        Assert.Equal(["Phasen-Kriterien"], GameFlow.Checks("schlank"));
        Assert.Equal(3, GameFlow.Checks("normal").Count);
        Assert.Equal(5, GameFlow.Checks("streng").Count);
        Assert.Contains("Keine Abnahme mit Bedenken", GameFlow.Checks("streng"));
        Assert.DoesNotContain("Keine Abnahme mit Bedenken", GameFlow.Checks("normal"));
    }

    [Fact(DisplayName = "Prüfstufe: Wechsel ändert nur die Stufe, alle Inhalte bleiben; Unbekanntes wird abgewiesen")]
    public void Rigor_switch_keeps_content()
    {
        var d = Doc(); GameFlow.Generate(d); var i = Story(d, "Kernspiel"); CheckAll(d, i); GameFlow.Complete(d, i, GameFlow.Done);
        var before = d.DeepClone().AsObject();
        foreach (var r in new[] { "normal", "streng", "schlank" })
        {
            GameFlow.SetRigor(d, r);
            var now = d.DeepClone().AsObject(); now.Remove("rigor"); var was = before.DeepClone().AsObject(); was.Remove("rigor");
            Assert.Equal(was.ToJsonString(), now.ToJsonString());
        }
        Assert.Throws<ArgumentException>(() => GameFlow.SetRigor(d, "mittel"));
        d["rigor"] = "kaputt"; Assert.Equal("schlank", GameFlow.RigorOf(d));
    }
}
