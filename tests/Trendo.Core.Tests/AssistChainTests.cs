using System.Text.Json.Nodes;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// T-0707 (B5 „Ein Assistenzweg“): Bausteine der einen Kette – Auftrag prüfen, Rechte, KI-Plan mit „dokument.setzen“, Prüfung jeder KI-Änderung, ein Rückgängig-Schritt.
public class AssistChainTests
{
    static JsonObject Doc() => new() { ["titel"] = "Alt", ["text"] = "# Kapitel 1\n\n", ["aufgaben"] = new JsonArray(new JsonObject { ["title"] = "A", ["min"] = "30" }), ["zustimmung"] = new JsonObject { ["consent"] = false } };

    [Fact(DisplayName = "T-0707: Auftrag wird geprüft – leer, zu lang und ohne Programm gibt einen Satz für die Person")]
    public void Validate()
    {
        Assert.Equal("Schreib", AssistChain.Validate("buch", "  Schreib  ", AssistEntry.Auftragszeile).Prompt);
        Assert.Throws<ArgumentException>(() => AssistChain.Validate("buch", " ", AssistEntry.Auftragszeile));
        Assert.Throws<ArgumentException>(() => AssistChain.Validate(null, "x", AssistEntry.Auftragszeile));
        Assert.Throws<ArgumentException>(() => AssistChain.Validate("buch", new string('a', AssistChain.MaxPrompt + 1), AssistEntry.Programmbefehl));
    }

    [Fact(DisplayName = "T-0707: Rechte – Gast darf keine KI-Aufträge geben, Not-Stopp hält die Kette an (vorhandene Freigabe-API)")]
    public void Rights()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-t0707-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            var gate = new ApprovalGate(new AuditLog(Path.Combine(dir, "audit.jsonl")));
            Assert.Null(AssistChain.RightsBlock(Principal.LocalOwner, gate));
            Assert.NotNull(AssistChain.RightsBlock(new Principal("gast", Role.Guest), gate));
            gate.Halt(Principal.LocalOwner, "Test");
            Assert.Contains("Not-Stopp", AssistChain.RightsBlock(Principal.LocalOwner, gate));
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    [Fact(DisplayName = "T-0707: KI-Plan mit „dokument.setzen“ – Text, Liste und Gruppe als Wert, Denkteil wird entfernt, unbekannte Aktionen fallen weg")]
    public void Ki_plan_with_edit()
    {
        var answer = "<think>{\"steps\":[]}</think>{\"steps\":[{\"action\":\"dokument.setzen\",\"args\":{\"pfad\":\"text\",\"wert\":\"# Kapitel 1\\n\\nEs war einmal.\"}},"
            + "{\"action\":\"dokument.setzen\",\"args\":{\"pfad\":\"aufgaben/1\",\"wert\":{\"title\":\"B\",\"min\":\"60\"}}},{\"action\":\"loeschen-alles\",\"args\":{}}]}";
        var plan = AssistChain.FromKi("Schreib weiter", answer, []);
        Assert.True(plan.Ready);
        Assert.Equal(2, plan.Steps.Count);
        Assert.Equal("text", plan.Steps[0].Args["pfad"]);
        Assert.Contains("Es war einmal.", plan.Steps[0].Args["wert"]);
        Assert.Equal("{\"title\":\"B\",\"min\":\"60\"}", plan.Steps[1].Args["wert"]);
        // ohne Kette (alter Planer) kennt die KI diese Aktion nicht
        Assert.False(AssistPlanner.FromKi("Schreib weiter", answer, []).Understood);
    }

    [Fact(DisplayName = "T-0707: echte Antwort von qwen3:14b (Tagesplaner) – fehlende Klammer wird ergänzt, überzählige nicht erraten, die ganze Liste wird verstanden")]
    public void Real_answer_day_planner()
    {
        const string broken = """{"steps":[{"action":"dokument.setzen","args":{"pfad":"tasks","wert":[{"id":1,"title":"Wocheneinkauf","min":60,"imp":2,"due":"2026-11-16","done":false}]}],"frage":""}""";
        var fixedPlan = AssistChain.FromKi("Trag den Wocheneinkauf ein", broken, []); // fehlende Klammer wird ergänzt
        Assert.True(fixedPlan.Ready, fixedPlan.Summary);
        Assert.StartsWith("[{", fixedPlan.Steps[0].Args["wert"]);
        // zweiter echter Fall: vollständiger Plan, danach ein zweites Objekt – das erste vollständige Objekt gilt
        const string trailing = """{"steps":[{"action":"dokument.setzen","args":{"pfad":"tasks/0","wert":{"id":"a1","title":"Wocheneinkauf","min":60}}}]},{"frage":""}""";
        Assert.Equal("tasks/0", AssistChain.FromKi("x", trailing, []).Steps.Single().Args["pfad"]);
        Assert.False(AssistChain.FromKi("x", """{"steps":[{"action":"dokument.setzen","args":{"pfad":"text","wert":"a"}]]""", []).Understood); // falsche Klammerart: nichts erraten
        Assert.Equal("{\"a\":[{\"b\":\"}]\"}]}", AssistChain.CloseBrackets("{\"a\":[{\"b\":\"}]\"]}")); // Klammern in Texten zählen nicht
        Assert.True(AssistChain.LooksLikePlan(broken));
        Assert.False(AssistChain.LooksLikePlan("Gern, hier ist mein Vorschlag."));
        const string answer = """{"steps":[{"action":"dokument.setzen","args":{"pfad":"tasks","wert":[{"id":1,"title":"Wocheneinkauf","min":60,"imp":2,"due":"2026-11-16","done":false}]}}],"frage":""}""";
        var plan = AssistChain.FromKi("Trag den Wocheneinkauf ein", answer, []);
        Assert.True(plan.Ready, plan.Summary);
        var d = new JsonObject { ["tasks"] = new JsonArray() };
        AssistChain.ApplyEdit(d, plan.Steps[0].Args["pfad"], plan.Steps[0].Args["wert"]);
        Assert.Equal("Wocheneinkauf", d["tasks"]![0]!["title"]!.GetValue<string>());
    }

    [Fact(DisplayName = "T-0707: Jede KI-Änderung wird geprüft – Freigabefelder gesperrt, Stelle muss es geben, Text bleibt Text, Liste bleibt Liste")]
    public void Check_edit()
    {
        var d = Doc();
        AssistChain.ApplyEdit(d, "titel", "Neu");
        Assert.Equal("Neu", d["titel"]!.GetValue<string>());
        AssistChain.ApplyEdit(d, "titel", "\"In Anführung\"");
        Assert.Equal("In Anführung", d["titel"]!.GetValue<string>());
        AssistChain.ApplyEdit(d, "aufgaben/1", "{\"title\":\"B\",\"min\":\"60\"}"); // hinten anhängen
        Assert.Equal(2, ((JsonArray)d["aufgaben"]!).Count);
        AssistChain.ApplyEdit(d, "neu", "Feld"); // neues Feld in vorhandener Gruppe
        AssistChain.ApplyEdit(d, "aufgaben[0]/title", "A2"); // echter Fall qwen3:14b: „tasks[0]“ statt „tasks/0“
        Assert.Equal("A2", d["aufgaben"]![0]!["title"]!.GetValue<string>());
        Assert.Null(d["aufgaben[0]"]);
        Assert.Throws<ArgumentException>(() => AssistChain.CheckEdit(d, "aufgaben[9]", JsonValue.Create("x"))); // ohne Vereinheitlichung kein seltsamer Feldname
        Assert.Throws<ArgumentException>(() => AssistChain.ApplyEdit(d, "zustimmung/consent", "true"));
        AssistChain.ApplyEdit(d, "gruppe/neu/x", "1"); // fehlende Gruppen entstehen unterwegs (wie bei DocPath.Set)
        Assert.Equal(1, d["gruppe"]!["neu"]!["x"]!.GetValue<int>());
        Assert.Throws<ArgumentException>(() => AssistChain.ApplyEdit(d, "gibts/0/x", "1")); // eine Liste entsteht nicht nebenbei
        Assert.Throws<ArgumentException>(() => AssistChain.ApplyEdit(d, "titel/x", "1")); // unter einem Text gibt es keine Stelle
        Assert.Throws<ArgumentException>(() => AssistChain.ApplyEdit(d, "aufgaben/7", "{}"));
        Assert.Throws<ArgumentException>(() => AssistChain.ApplyEdit(d, "aufgaben", "kein JSON"));
        Assert.Throws<ArgumentException>(() => AssistChain.ApplyEdit(d, "aufgaben/0", "[1,2]"));
        Assert.Throws<ArgumentException>(() => AssistChain.ApplyEdit(d, "", "x"));
    }

    [Fact(DisplayName = "T-0707: Anweisung an die KI enthält Dokumentdaten, die Aktion „dokument.setzen“ und die Befehle; große Dokumente werden gekürzt")]
    public void Ki_prompt()
    {
        var p = AssistChain.KiPrompt("Bücher schreiben", [new AssistAction("book-chapter", "Neues Kapitel", ["kapitel"])], "Rolle", null, Doc());
        Assert.Contains("dokument.setzen", p); Assert.Contains("book-chapter", p); Assert.Contains("\"titel\":\"Alt\"", p); Assert.StartsWith("Rolle", p);
        var big = new JsonObject { ["text"] = new string('x', AssistChain.MaxDocJson * 2) };
        Assert.Contains("gekürzt", AssistChain.KiPrompt("X", [], null, null, big));
    }

    [Fact(DisplayName = "T-0707: Fähigkeit – Hülle mit Dokument = im Dokument, sonst einheitlich „Noch nicht im Dokument möglich“")]
    public void Reach_and_note()
    {
        Assert.Equal(AssistReach.InDocument, AssistChain.Reach(true, true, true));
        Assert.Equal(AssistReach.NotInDocument, AssistChain.Reach(false, false, true));
        Assert.Equal(AssistReach.NotInDocument, AssistChain.Reach(true, false, true));
        Assert.Equal(AssistReach.NoProgram, AssistChain.Reach(false, false, false));
        Assert.StartsWith(AssistChain.NotInDocumentLead, AssistChain.NotInDocumentText("Trendo Text", true));
        Assert.Contains("Projekt", AssistChain.NotInDocumentText("Trendo Text", true));
    }
}
