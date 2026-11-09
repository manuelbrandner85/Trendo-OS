using System.Text.Json.Nodes;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0630: Engine, Blender und Team werden automatisch gewählt, nur Installiertes zählt, ungültige KI-Angaben haben keine Wirkung.</summary>
public class GameToolsTests
{
    static readonly GameTools.Available All = new(true, true, true, true);

    static JsonObject Doc(string scale, string platforms = "PC", string style = "", string browser = "", string? engine = null)
    {
        var d = new JsonObject { ["scale"] = scale, ["platforms"] = platforms, ["style"] = style, ["browser"] = browser, ["genre"] = "", ["logline"] = "" };
        if (engine is not null) d["engine"] = engine;
        return d;
    }

    [Theory(DisplayName = "Engine-Wahl: Regeln nach Umfang, Stil und Plattform, wenn die KI nichts angibt")]
    [InlineData("aaa", "PC", "", "unreal")]
    [InlineData("indie3d", "PC", "Fotorealistisch", "unreal")]
    [InlineData("indie3d", "PC", "Comic", "godot")]
    [InlineData("indie2d", "Smartphone", "", "unity")]
    [InlineData("retro", "Browser, PC", "", "browser")]
    [InlineData("indie2d", "PC", "", "godot")]
    public void Rules(string scale, string platforms, string style, string expected) =>
        Assert.Equal(expected, GameTools.Decide(Doc(scale, platforms, style), All).Engine);

    [Fact(DisplayName = "Engine-Wahl: die Wahl der KI gilt samt Begründung")]
    public void Ai_choice_wins()
    {
        var d = Doc("indie3d", engine: "unity"); d["engineReason"] = "Das Team kennt die Physik dort am besten";
        var c = GameTools.Decide(d, All);
        Assert.Equal("unity", c.Engine); Assert.Equal("Das Team kennt die Physik dort am besten", c.Reason);
    }

    [Fact(DisplayName = "Engine-Wahl: nicht installierte Wahl der KI fällt auf die nächste installierte zurück und sagt es")]
    public void Falls_back_to_installed()
    {
        var c = GameTools.Decide(Doc("aaa", engine: "unity"), new GameTools.Available(true, false, true, true));
        Assert.Equal("unreal", c.Engine); Assert.Contains("Unity ist nicht installiert", c.Note);
    }

    [Fact(DisplayName = "Engine-Wahl: ungültige Angabe der KI wird ignoriert")]
    public void Invalid_ignored() => Assert.Equal("unreal", GameTools.Decide(Doc("aaa", engine: "cryengine"), All).Engine);

    [Fact(DisplayName = "Engine-Wahl: ohne jede Engine ein klarer Hinweis und kein Download")]
    public void None_installed()
    {
        var c = GameTools.Decide(Doc("aaa"), new GameTools.Available(false, false, false, false));
        Assert.Equal("unreal", c.Engine); Assert.Contains("nicht installiert", c.Note); Assert.Contains("offiziellen Seite", c.Note);
    }

    [Fact(DisplayName = "Engine-Wahl: Trendos Bau nimmt Godot nur beim Plattformer, sonst sagt die Notiz, dass Claude es baut")]
    public void Route_is_honest()
    {
        Assert.Equal("godot", GameTools.Decide(Doc("retro", browser: "plattform", engine: "godot"), All).Route);
        var c = GameTools.Decide(Doc("indie3d", style: "Comic"), All);
        Assert.Equal("godot", c.Engine); Assert.Equal("unreal", c.Route); Assert.Contains("erst Claude", c.Note);
        Assert.Contains("Claude baut es direkt in Godot", GameTools.Decide(Doc("indie3d", style: "Comic"), All with { Claude = true }).Note);
    }

    [Fact(DisplayName = "Werkzeuge: Blender bei 3D, nicht beim Browser-Spiel, fehlendes Blender wird genannt")]
    public void Blender_when_needed()
    {
        Assert.True(GameTools.Decide(Doc("aaa"), All).Blender);
        Assert.False(GameTools.Decide(Doc("retro", "Browser"), All).Blender);
        var c = GameTools.Decide(Doc("aaa"), All with { Blender = false });
        Assert.False(c.Blender); Assert.Contains("Blender ist nicht installiert", c.Note);
    }

    [Fact(DisplayName = "Engine-Wahl: Regelentscheidung ändert das Dokument nicht und folgt neu installierten Engines und neuem Umfang")]
    public void Rule_is_not_frozen()
    {
        var d = Doc("indie2d");
        var before = d.ToJsonString();
        Assert.Equal("godot", GameTools.Decide(d, new GameTools.Available(false, false, true, false)).Engine);
        Assert.Equal(before, d.ToJsonString());
        d["scale"] = "aaa";
        Assert.Equal("unreal", GameTools.Decide(d, All).Engine);
    }

    [Fact(DisplayName = "Engine-Wahl: Blender bleibt nach späterer Installation verfügbar, nur ein ausdrückliches Nein der KI schaltet es ab")]
    public void Blender_not_frozen()
    {
        var d = Doc("aaa");
        Assert.False(GameTools.Decide(d, All with { Blender = false }).Blender);
        Assert.True(GameTools.Decide(d, All).Blender);
        d["blender"] = false;
        Assert.False(GameTools.Decide(d, All).Blender);
    }

    [Fact(DisplayName = "Engine-Wahl: Schreibweise der KI ist egal")]
    public void Case_insensitive() => Assert.Equal("unity", GameTools.Decide(Doc("indie3d", engine: "Unity"), All).Engine);

    [Fact(DisplayName = "Engine-Wahl: Browser-Umfang ohne Engine bleibt im Browser, auch für Smartphones, und erzeugt kein Engine-Projekt")]
    public void Browser_is_fallback()
    {
        var c = GameTools.Decide(Doc("retro", "Smartphone", browser: "plattform"), new GameTools.Available(false, false, false, false));
        Assert.Equal("browser", c.Engine); Assert.Equal("browser", c.Route);
    }

    [Fact(DisplayName = "Engine-Wahl: Weicht die Regel auf eine andere Engine aus, wird das gesagt")]
    public void Rule_fallback_is_named()
    {
        var c = GameTools.Decide(Doc("aaa"), new GameTools.Available(false, false, true, false));
        Assert.Equal("godot", c.Engine); Assert.Contains("Unreal Engine 5 wäre passender", c.Note);
    }

    [Fact(DisplayName = "Engine-Wahl: Blender-Text nur, wenn wirklich 3D gebaut wird; doppelter Punkt entfällt")]
    public void Honest_line()
    {
        Assert.False(GameTools.Decide(Doc("retro", "Browser", engine: "unreal"), All).Blender);
        var d = Doc("aaa", engine: "unreal"); d["engineReason"] = "Riesige Welt.";
        Assert.DoesNotContain("..", GameTools.Line(GameTools.Decide(d, All)));
    }

    [Fact(DisplayName = "Änderung per Satz: ungültige Engine löscht keine Wahl, neue Engine übernimmt keinen alten Grund, ohne Angabe bleibt alles")]
    public void Parse_merges_engine()
    {
        var current = new JsonObject { ["title"] = "A", ["scale"] = "aaa", ["engine"] = "unreal", ["engineReason"] = "Riesige Welt" };
        Assert.Equal("unreal", (string)GameDirector.Parse("```json\n{\"engine\":\"xyz\"}\n```", current).Doc!["engine"]!);
        Assert.Equal("unreal", (string)GameDirector.Parse("```json\n{\"title\":\"B\"}\n```", current).Doc!["engine"]!);
        var changed = GameDirector.Parse("```json\n{\"engine\":\"godot\"}\n```", current).Doc!;
        Assert.Equal("godot", (string)changed["engine"]!); Assert.Equal("", (string)changed["engineReason"]!);
    }

    [Fact(DisplayName = "Fachleute: nur am Wortanfang, kurze Stichwörter als ganzes Wort, Engine verdrängt im Automatikmodus nicht die Standardrollen")]
    public void Experts_precise()
    {
        Assert.Equal(["game-designer", "technical-director"], GameExperts.Pick("Mach den Boss schwerer", "godot", "auto").Select(e => e.Id));
        Assert.DoesNotContain(GameExperts.Pick("Spiel für Kinder mit einer Kiste", null, "auto"), e => e.Id == "ai-programmer");
        Assert.DoesNotContain(GameExperts.Pick("schwierig und traurig", null, "auto"), e => e.Name.Contains("Animator") || e.Name.Contains("Technical Artist"));
    }

    [Fact(DisplayName = "Studio-Team: Füllwörter zählen nicht, Wörter nur am Wortanfang")]
    public void Studio_pick_precise()
    {
        var team = StudioTeams.All.First();
        Assert.Equal(team.Roles[0], Assert.Single(StudioTeams.Pick(team.StudioId, "bitte eine")));
        Assert.Contains(StudioTeams.Pick(team.StudioId, "Ton"), r => r.Name == "Lektor");
    }

    [Fact(DisplayName = "Spielbibel: KI-Antwort mit Engine wird übernommen, Unsinn als leer bereinigt")]
    public void Parse_keeps_engine()
    {
        var ok = GameDirector.Parse("```json\n{\"title\":\"A\",\"scale\":\"aaa\",\"engine\":\"Unreal\",\"engineReason\":\"Riesige Welt\",\"blender\":true}\n```").Doc!;
        Assert.Equal("unreal", (string)ok["engine"]!); Assert.Equal("Riesige Welt", (string)ok["engineReason"]!); Assert.True((bool)ok["blender"]!);
        Assert.Equal("", (string)GameDirector.Parse("```json\n{\"title\":\"A\",\"scale\":\"aaa\",\"engine\":\"xyz\"}\n```").Doc!["engine"]!);
    }

    [Fact(DisplayName = "Anweisung an die KI: nennt nur, was installiert ist, und verlangt die Entscheidung ohne Nachfrage")]
    public void Brief_lists_installed()
    {
        var b = GameTools.Brief(new GameTools.Available(true, false, true, false));
        Assert.Contains("vorhanden: Unreal Engine 5, Godot,", b); Assert.DoesNotContain("vorhanden: Unity", b); Assert.Contains("fragst dafür nicht nach", b); Assert.Contains("lade oder installiere nichts", b);
    }

    [Fact(DisplayName = "Fachleute automatisch: Anzahl richtet sich nach dem Auftrag, mindestens zwei, höchstens sechs")]
    public void Experts_auto()
    {
        Assert.Equal(2, GameExperts.Pick("xyz", null, "auto").Count);
        Assert.True(GameExperts.Pick("Mehrspieler mit Fähigkeiten, Gegner, Shop, Übersetzung, Server und Community", null, "auto").Count > 2);
        Assert.True(GameExperts.Pick("Mehrspieler mit Fähigkeiten, Gegner, Shop, Übersetzung, Server und Community", null, "auto").Count <= 6);
    }

    [Fact(DisplayName = "Studio-Team automatisch: passende Rollen zum Auftrag, ohne Treffer die erste Rolle, ohne Team leer")]
    public void Studio_team_auto()
    {
        var team = StudioTeams.All.First();
        Assert.Contains(StudioTeams.Pick(team.StudioId, "Bitte die Rechtschreibung und den Ton prüfen"), r => r.Name == "Lektor");
        Assert.Equal(team.Roles[0], Assert.Single(StudioTeams.Pick(team.StudioId, "xyz")));
        Assert.Contains("automatisch mit", StudioTeams.Brief(team.StudioId, "xyz"));
        Assert.Equal("", StudioTeams.Brief("GIBT-ES-NICHT", "Auftrag"));
    }
}
