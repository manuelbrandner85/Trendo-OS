using System.Text.Json.Nodes;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Alle Studios, Runde 3 – KI-Team: geprüfte Antwortform (Konzept aus pydantic-ai, eigener Code). Erster Nutzer: Rolle „Planer“.</summary>
public class AgentAnswerTests
{
    static readonly AnswerField[] Plan = [new("ziel", "text"), new("schritte", "liste")];

    [Fact(DisplayName = "Antwortform +1: Anweisung nennt jedes Feld mit Art, nur JSON")]
    public void Instruction()
    {
        var s = AgentAnswer.Instruction([new("ziel", "text", "ein Satz"), new("dauer", "zahl"), new("fertig", "janein")]);
        Assert.Contains("NUR mit einem JSON-Objekt", s); Assert.Contains("\"ziel\" (Text, ein Satz)", s); Assert.Contains("\"dauer\" (Zahl)", s); Assert.Contains("\"fertig\" (true oder false)", s);
    }

    [Fact(DisplayName = "Antwortform +2: JSON auch aus ```json-Block oder mit Satz davor")]
    public void Extract()
    {
        Assert.Equal("{\"a\":1}", AgentAnswer.ExtractJson("Hier ist der Plan:\n```json\n{\"a\":1}\n```\nViel Erfolg!"));
        Assert.Equal("{\"a\":{\"b\":2}}", AgentAnswer.ExtractJson("Gern: {\"a\":{\"b\":2}} – fertig."));
        Assert.Null(AgentAnswer.ExtractJson("Nur Text, kein JSON."));
    }

    [Fact(DisplayName = "Antwortform +3: gültige Antwort → bereinigte Daten (nur verlangte Felder, Zahl aus Text)")]
    public void Check_valid()
    {
        var (d, e) = AgentAnswer.Check("{\"ziel\":\" Flyer drucken \",\"schritte\":[\"Text\",\"Bild\"],\"extra\":5}", Plan);
        Assert.Empty(e); Assert.Equal("Flyer drucken", (string)d!["ziel"]!); Assert.Equal(2, d["schritte"]!.AsArray().Count); Assert.False(d.ContainsKey("extra"));
        var (d2, _) = AgentAnswer.Check("{\"dauer\":\"2,5\",\"ok\":true}", [new("dauer", "zahl"), new("ok", "janein")]);
        Assert.Equal(2.5, (double)d2!["dauer"]!); Assert.True((bool)d2["ok"]!);
    }

    [Fact(DisplayName = "Antwortform −1: fehlendes Feld, leere Liste, falsche Art, kaputtes JSON → verständliche Fehler")]
    public void Check_errors()
    {
        Assert.Equal(["Feld „schritte“ fehlt."], AgentAnswer.Check("{\"ziel\":\"x\"}", Plan).Errors);
        Assert.Contains("nicht leere Liste", AgentAnswer.Check("{\"ziel\":\"x\",\"schritte\":[]}", Plan).Errors[0]);
        Assert.Contains("nicht leerer Text", AgentAnswer.Check("{\"ziel\":3,\"schritte\":[\"a\"]}", Plan).Errors[0]);
        Assert.Contains("leere Einträge", AgentAnswer.Check("{\"ziel\":\"x\",\"schritte\":[\"a\",\" \"]}", Plan).Errors[0]);
        Assert.Equal(["Das JSON ist nicht gültig."], AgentAnswer.Check("{kaputt}", Plan).Errors);
        Assert.Equal(["Die Antwort enthält kein JSON-Objekt."], AgentAnswer.Check("Schritt 1: …", Plan).Errors);
    }

    [Fact(DisplayName = "Antwortform +4: lesbarer Text – nummerierte Liste, Zahl deutsch, ja/nein")]
    public void Render()
    {
        var t = AgentAnswer.Render(JsonNode.Parse("{\"ziel\":\"Flyer\",\"schritte\":[\"Text\",\"Bild\"]}")!.AsObject(), Plan);
        Assert.Equal("Ziel: Flyer\nSchritte:\n1. Text\n2. Bild", t);
        Assert.Equal("Dauer: 2,5\nOk: ja", AgentAnswer.Render(JsonNode.Parse("{\"dauer\":2.5,\"ok\":true}")!.AsObject(), [new("dauer", "zahl"), new("ok", "janein")]));
    }

    static (AgentCenter C, IntelligenceCore Ki, FakeKi Fake, Fixture F) Setup(Func<int, string> reply)
    {
        var f = new Fixture(); f.Core.Projects.Create("p1", "P1"); var n = 0;
        var fake = new FakeKi("A", reply: m => reply(++n));
        return (new AgentCenter(f.Root, f.Core.Audit, f.Core.Approvals, f.Core.Projects), new IntelligenceCore(f.Root, f.Core.Audit, () => [fake]), fake, f);
    }

    [Fact(DisplayName = "Antwortform +5: Planer mit gültigem JSON → geprüfte Daten gespeichert, lesbarer Text als Ergebnis, eine Anfrage")]
    public async Task Planner_valid()
    {
        var (c, ki, fake, f) = Setup(_ => "```json\n{\"ziel\":\"Flyer\",\"schritte\":[\"Text schreiben\",\"Drucken\"]}\n```");
        using (f)
        {
            var t = await c.RunAsync(c.Create("planer", "Flyer", "p1"), ki);
            Assert.Equal(AgentTaskState.Fertig, t.State); Assert.Single(fake.Calls);
            Assert.Equal("Ziel: Flyer\nSchritte:\n1. Text schreiben\n2. Drucken", t.Result); Assert.Contains("\"schritte\"", t.Data);
            Assert.Contains("NUR mit einem JSON-Objekt", fake.Calls[0][0].Content);
        }
    }

    [Fact(DisplayName = "Antwortform +6: falsche Form → eine Nachfrage mit den Fehlern, danach gültig")]
    public async Task Planner_retry()
    {
        var (c, ki, fake, f) = Setup(n => n == 1 ? "{\"ziel\":\"Flyer\"}" : "{\"ziel\":\"Flyer\",\"schritte\":[\"A\"]}");
        using (f)
        {
            var t = await c.RunAsync(c.Create("planer", "Flyer", "p1"), ki);
            Assert.Equal(2, fake.Calls.Count); Assert.Contains("Feld „schritte“ fehlt.", fake.Calls[1][^1].Content);
            Assert.NotNull(t.Data); Assert.StartsWith("Ziel: Flyer", t.Result);
        }
    }

    [Fact(DisplayName = "Antwortform −2: Antwort ganz ohne JSON → keine Nachfrage, Text unverändert, als ungeprüft (Data leer)")]
    public async Task Planner_plain_text()
    {
        var (c, ki, fake, f) = Setup(_ => "1. Text schreiben\n2. Drucken");
        using (f)
        {
            var t = await c.RunAsync(c.Create("planer", "Flyer", "p1"), ki);
            Assert.Equal(AgentTaskState.Fertig, t.State); Assert.Null(t.Data); Assert.Single(fake.Calls);
            Assert.Equal("1. Text schreiben\n2. Drucken", t.Result);
        }
    }

    [Fact(DisplayName = "Antwortform −3: JSON bleibt auch nach der Nachfrage falsch → erste Antwort bleibt, ungeprüft, kein Abbruch")]
    public async Task Planner_still_wrong()
    {
        var (c, ki, fake, f) = Setup(n => n == 1 ? "{\"ziel\":\"Flyer\"}" : "{\"schritte\":[]}");
        using (f)
        {
            var t = await c.RunAsync(c.Create("planer", "Flyer", "p1"), ki);
            Assert.Equal(AgentTaskState.Fertig, t.State); Assert.Null(t.Data); Assert.Equal(2, fake.Calls.Count);
            Assert.Equal("{\"ziel\":\"Flyer\"}", t.Result);
        }
    }

    [Fact(DisplayName = "Antwortform +7: Rollen ohne Form bleiben unverändert (Texter: eine Anfrage, kein JSON-Zwang)")]
    public async Task Other_roles_unchanged()
    {
        var (c, ki, fake, f) = Setup(_ => "Freier Text.");
        using (f)
        {
            var t = await c.RunAsync(c.Create("texter", "Flyer", "p1"), ki);
            Assert.Equal("Freier Text.", t.Result); Assert.Null(t.Data); Assert.Single(fake.Calls); Assert.DoesNotContain("JSON", fake.Calls[0][0].Content);
        }
    }
}
