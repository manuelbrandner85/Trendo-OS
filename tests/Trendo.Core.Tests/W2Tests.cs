using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

public sealed class FakeKi(string name, bool up = true, string[]? models = null, Func<IReadOnlyList<KiMessage>, string>? reply = null) : IKiBackend
{
    public string Name => name;
    public string BaseUrl => "http://127.0.0.1:1";
    public List<IReadOnlyList<KiMessage>> Calls { get; } = [];
    public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(up);
    public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(models ?? ["test-modell"]);
    public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> m, CancellationToken ct = default) { Calls.Add(m); return Task.FromResult(reply?.Invoke(m) ?? $"Antwort von {name}"); }
}

public class CORE02_IntelligenceCore
{
    [Fact(DisplayName = "CORE-02: Rückfall auf die nächste Quelle, Wissen wird eingebaut, Inhalt nicht ins Protokoll")]
    public async Task Fallback_knowledge_privacy()
    {
        using var f = new Fixture();
        var down = new FakeKi("A", up: false); var ok = new FakeKi("B");
        var ic = new IntelligenceCore(f.Root, f.Core.Audit, () => [down, ok], (q, n) => [("Rezept Apfelkuchen", "200 g Mehl")]);
        var a = await ic.AskAsync(new KiRequest("Geheimfrage Apfelkuchen?"));
        Assert.True(a.Ok); Assert.Equal("B", a.Backend); Assert.Equal(["Rezept Apfelkuchen"], a.Sources);
        Assert.Contains("200 g Mehl", ok.Calls[0][0].Content);
        Assert.DoesNotContain(f.Core.Audit.ReadAll(), e => e.Detail.Contains("Geheimfrage"));
        Assert.Contains(f.Core.Audit.ReadAll(), e => e.Action == "ki.ask");
        Assert.Single(ic.History());
        ic.ClearHistory(); Assert.Empty(ic.History());
    }

    [Fact(DisplayName = "CORE-02: Ohne erreichbare KI ehrliche Meldung statt Fantasieantwort")]
    public async Task No_backend()
    {
        using var f = new Fixture();
        var ic = new IntelligenceCore(f.Root, f.Core.Audit, () => [new FakeKi("A", up: false)]);
        var a = await ic.AskAsync(new KiRequest("Hallo"));
        Assert.False(a.Ok); Assert.Contains("keine KI", a.Text);
        Assert.False((await ic.AskAsync(new KiRequest("  "))).Ok);
    }
}

public class X05_FreeAi
{
    [Fact(DisplayName = "X-05: Rückfallkette lässt sich sortieren, abschalten und bleibt gespeichert")]
    public void Order_persist()
    {
        using var f = new Fixture();
        var s = KiSourceSettings.Load(f.Root);
        Assert.Equal("ollama", s.Order[0]);
        s.Move("lmstudio", -1); s.Toggle("jan"); s.Save(f.Root);
        var t = KiSourceSettings.Load(f.Root);
        Assert.Equal(["lmstudio", "ollama"], t.Order.Take(2));
        Assert.DoesNotContain(t.Active(), x => x.Id == "jan");
        Assert.Equal(KiCatalog.Sources.Count, t.Order.Count);
    }

    [Fact(DisplayName = "X-05: Nur freie Quellen auf diesem Gerät, Modelle passend zur Hardware")]
    public void Catalog_free_local()
    {
        Assert.All(KiCatalog.Sources, s => Assert.StartsWith("http://127.0.0.1:", s.BaseUrl));
        const long G = 1024L * 1024 * 1024;
        Assert.True(KiCatalog.Fits(KiCatalog.Models.First(m => m.Tag == "qwen2.5:3b"), new HardwareInfo(8 * G, null, 0)));
        Assert.False(KiCatalog.Fits(KiCatalog.Models.First(m => m.Tag == "mistral:7b"), new HardwareInfo(8 * G, null, 0)));
    }
}

public class CORE10_Knowledge
{
    [Fact(DisplayName = "CORE-10: Suche findet Wissen trotz Umlauten und Mehrzahl, bleibt nach Neustart erhalten")]
    public void Search_persist()
    {
        using var f = new Fixture();
        var k = new KnowledgeStore(f.Root, f.Core.Audit);
        k.Add("Stadtwerke", "Meine Kundennummer bei den Stadtwerken lautet 4711.");
        k.Add("Garten", "Tomaten brauchen viel Wasser.");
        var k2 = new KnowledgeStore(f.Root, f.Core.Audit);
        Assert.Equal("Stadtwerke", k2.Search("kundennummer stadtwerke")[0].Item.Title);
        Assert.Equal("Garten", k2.Search("Tomate")[0].Item.Title);
        Assert.Empty(k2.Search("Flugzeug"));
        Assert.True(k2.Remove(k2.All()[0].Id)); Assert.Single(new KnowledgeStore(f.Root, f.Core.Audit).All());
    }

    [Fact(DisplayName = "CORE-10: Ordner einlesen nur Textdateien, keine Doppelten")]
    public void Import_folder()
    {
        using var f = new Fixture();
        var d = Path.Combine(f.Root, "in"); Directory.CreateDirectory(d);
        File.WriteAllText(Path.Combine(d, "a.md"), "# Rezept\nApfelkuchen mit Zimt");
        File.WriteAllBytes(Path.Combine(d, "b.png"), [1, 2, 3]);
        var k = new KnowledgeStore(f.Root, f.Core.Audit);
        Assert.Equal((1, 1), k.Import(d));
        Assert.Equal((0, 2), k.Import(d));
        Assert.NotEmpty(k.Search("zimt"));
    }

    [Fact(DisplayName = "CORE-10: Korrektur wird Wissen und fließt in die nächste KI-Antwort")]
    public async Task Learns_from_feedback()
    {
        using var f = new Fixture();
        var k = new KnowledgeStore(f.Root, f.Core.Audit);
        Assert.Null(k.Feedback("Wie heißt mein Hund?", "Weiß ich nicht", true));
        k.Feedback("Wie heißt mein Hund?", "Bello", false, "Mein Hund heißt Rex.");
        Assert.Equal((1, 1), k.FeedbackStats());
        var ki = new FakeKi("A");
        var ic = new IntelligenceCore(f.Root, f.Core.Audit, () => [ki], (q, n) => k.Search(q, n).Select(h => (h.Item.Title, h.Item.Text)).ToList());
        var a = await ic.AskAsync(new KiRequest("Wie heißt mein Hund"));
        Assert.Contains("Rex", ki.Calls[0][0].Content);
        Assert.Single(a.Sources);
    }
}

public class CORE12_Prompts
{
    [Fact(DisplayName = "CORE-12: Platzhalter mit Standardwert ausfüllen, fehlende werden genannt")]
    public void Fill_vars()
    {
        var t = "Erkläre {{Thema}} für {{Alter|12}}-Jährige. {{Thema}}!";
        Assert.Equal([("Thema", null), ("Alter", "12")], PromptLibrary.Variables(t));
        var (txt, miss) = PromptLibrary.Fill(t, new Dictionary<string, string> { ["Thema"] = "Strom" });
        Assert.Equal("Erkläre Strom für 12-Jährige. Strom!", txt); Assert.Empty(miss);
        Assert.Equal(["Thema"], PromptLibrary.Fill(t, new Dictionary<string, string>()).Missing);
    }

    [Fact(DisplayName = "CORE-12: Versionen speichern und zurückholen, Mitgelieferte nicht löschbar")]
    public void Versions()
    {
        using var f = new Fixture();
        var lib = new PromptLibrary(f.Root, f.Core.Audit);
        Assert.Equal(PromptLibrary.Defaults.Length, lib.All().Count);
        var p = lib.Save(null, "Mein Test", "Test", "Schreibe A");
        lib.Save(p.Id, "Mein Test", "Test", "Schreibe B");
        Assert.Equal(2, lib.Get(p.Id)!.Version);
        lib.Restore(p.Id, 1);
        var re = new PromptLibrary(f.Root, f.Core.Audit).Get(p.Id)!;
        Assert.Equal("Schreibe A", re.Text); Assert.Equal(3, re.Version);
        Assert.False(lib.Delete("zusammenfassen")); Assert.True(lib.Delete(p.Id));
    }

    [Fact(DisplayName = "CORE-12: Qualitätsprüfung erkennt kurze Aufträge, Geheimnisse, offene Platzhalter; Masterprompt hat Regeln")]
    public void Check_and_master()
    {
        Assert.Contains(PromptLibrary.Check("Hund"), h => h.Text.Contains("kurz"));
        Assert.Contains(PromptLibrary.Check("Schreibe in 3 Sätzen. password: geheim123"), h => h.Text.Contains("Passwort"));
        Assert.Contains(PromptLibrary.Check("Schreibe {{Thema in 3 Sätzen über Hunde"), h => h.Text.Contains("Platzhalter"));
        Assert.Equal("ok", PromptLibrary.Check("Erkläre Photosynthese in 3 kurzen Sätzen.")[0].Level);
        var m = PromptLibrary.Master("ein Lehrer", "Plan für Mathe", "", "Tabelle", "- kurz\nfreundlich");
        Assert.Contains("## Rolle\nDu bist ein Lehrer.", m); Assert.Contains("- freundlich", m); Assert.Contains("Erfinde keine Fakten", m);
        Assert.DoesNotContain("Hintergrund", m);
    }
}
