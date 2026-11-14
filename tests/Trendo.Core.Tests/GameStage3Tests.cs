using System.Text.Json.Nodes;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0650: Spiele-Studio Stufe 3 – Engine einrichten (E1), Prüfung per Bild (E5), wiederholbare Testläufe (E6), gemeinsame Dienste (E7), ähnliche Spiele (D5).</summary>
public class GameStage3Tests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "trendo-stufe3-" + Guid.NewGuid().ToString("N"));
    public GameStage3Tests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    // ───────────── E5: Bilder ─────────────
    string Png(string name, int w, int h, Func<int, int, (byte R, byte G, byte B)> px)
    {
        var rgb = new byte[w * h * 3];
        for (var y = 0; y < h; y++) for (var x = 0; x < w; x++) { var (r, g, b) = px(x, y); var o = (y * w + x) * 3; rgb[o] = r; rgb[o + 1] = g; rgb[o + 2] = b; }
        var f = Path.Combine(_dir, name); File.WriteAllBytes(f, VisionPlus.EncodePng(new RgbImage(w, h, rgb))); return f;
    }
    // Ein „gutes“ Spielbild: Himmel, Boden, ein Rechteck als Figur, etwas Rauschen
    (byte, byte, byte) Good(int x, int y) { var n = (byte)((x * 7 + y * 13) % 23); return y < 60 ? ((byte)(90 + n), (byte)(140 + n), (byte)220) : x is > 70 and < 90 && y is > 50 and < 95 ? ((byte)200, (byte)60, (byte)40) : ((byte)(60 + n), (byte)(110 + n), (byte)(50 + n)); }

    [Fact(DisplayName = "E5: Schwarzbild, einfarbiges Bild und fehlende Texturen werden mit Messwert gefunden")]
    public void Shot_errors_found()
    {
        var black = GameShotCheck.Check("Start", Png("schwarz.png", 160, 100, (_, _) => (2, 2, 3)));
        Assert.Contains(black, f => f.Kind == "schwarz" && !f.Ok && f.Text.Contains("Helligkeit"));
        var flat = GameShotCheck.Check("Kampf", Png("grau.png", 160, 100, (_, _) => (128, 128, 128)));
        Assert.Contains(flat, f => f.Kind == "einfarbig" && !f.Ok);
        var pink = GameShotCheck.Check("Objekte", Png("pink.png", 160, 100, (x, y) => x < 40 && y < 40 ? ((byte)255, (byte)0, (byte)255) : Good(x, y)));
        var t = Assert.Single(pink, f => f.Kind == "textur");
        Assert.Equal("Objekte", t.Shot); Assert.EndsWith("pink.png", t.File); Assert.True(t.Value > 0.05);
    }

    [Fact(DisplayName = "E5: Ein gutes Bild löst keinen Fehlalarm aus")]
    public void Good_shot_no_alarm()
    {
        var f = Assert.Single(GameShotCheck.Check("Start", Png("gut.png", 160, 100, Good)));
        Assert.True(f.Ok); Assert.Equal("ok", f.Kind);
    }

    [Fact(DisplayName = "E5: Fehlt die Figur gegenüber dem Vergleichslauf, kommt ein Hinweis; gleiches Bild bleibt still")]
    public void Shot_against_before()
    {
        var before = Png("vorher.png", 160, 100, Good);
        Assert.Null(GameShotCheck.AgainstBefore("Gegner", Png("gleich.png", 160, 100, Good), before));
        var empty = Png("leer.png", 160, 100, (x, y) => y < 60 ? ((byte)10, (byte)10, (byte)10) : Good(x, y));
        var h = GameShotCheck.AgainstBefore("Gegner", empty, before);
        Assert.NotNull(h); Assert.Contains("Vergleichslauf", h!.Text);
        Assert.Contains(GameShotCheck.Check("Kaputt", Path.Combine(_dir, "fehlt.png")), f => f.Kind == "lesbar" && !f.Ok);
    }

    // ───────────── E6: Läufe ─────────────
    const string Log = """
        [2026.11.14] LogTemp: Display: TRENDO-START Startwert=42
        LogTemp: Display: TRENDO-FIGUR MetaHuman=nein (-) Mensch=ja
        LogTemp: Display: TRENDO-BODEN Gegner Boden=1.5 cm
        LogTemp: Display: TRENDO-WAGEN gesteuert=ja Strecke=31.5 m Tempo=40 km/h Reifen drehen sich
        LogTemp: Display: TRENDO-OK Spiel läuft
        """;

    static GameRuns.Run RunOf(string log, int seed, DateTime at, string[]? args = null) =>
        GameRuns.From(log, seed, "unreal", args ?? ["C:/x/Spiel.uproject", "-game", "-trendocheck", "-abslog=C:/x/start.txt", GameRuns.SeedArg(seed)], [], at);

    [Fact(DisplayName = "E6: Gleicher Startwert und gleiche Ausgabe ergeben gleiche Messwerte, ohne Meldung")]
    public void Same_seed_same_values()
    {
        var a = RunOf(Log, 42, new DateTime(2026, 11, 14, 10, 0, 0)); var b = RunOf(Log, 42, new DateTime(2026, 11, 14, 11, 0, 0));
        Assert.Equal(a.Values, b.Values); Assert.Equal(a.Checks, b.Checks);
        Assert.Equal(31.5, a.Values["WAGEN.Strecke"]); Assert.Equal(42, a.Values["START.Startwert"]);
        Assert.DoesNotContain(a.Inputs, x => x.Contains("C:/x")); // Pfade gehören nicht zu den Eingaben
        var c = GameRuns.Compare(a, b);
        Assert.True(c.Comparable); Assert.Empty(c.Changes);
    }

    [Fact(DisplayName = "E6: Eingebaute Verschlechterung wird gemeldet; anderer Startwert gilt nicht als Verschlechterung")]
    public void Regression_reported()
    {
        var a = RunOf(Log, 42, new DateTime(2026, 11, 14, 10, 0, 0));
        var worse = Log.Replace("Strecke=31.5 m", "Strecke=4 m").Replace("Boden=1.5 cm", "Boden=35 cm");
        var b = RunOf(worse, 42, new DateTime(2026, 11, 14, 11, 0, 0));
        var c = GameRuns.Compare(a, b);
        Assert.True(c.Comparable);
        Assert.Contains(c.Changes, x => x.What == "Fahrzeug" && x.Worse);
        Assert.Contains(c.Changes, x => x.What == "WAGEN.Strecke" && x.Worse);
        Assert.Contains(c.Changes, x => x.What == "BODEN.Boden" && x.Worse);
        var other = GameRuns.Compare(a, RunOf(worse, 7, new DateTime(2026, 11, 14, 12, 0, 0)));
        Assert.False(other.Comparable); Assert.Equal(0, other.Worse); Assert.Contains("Startwert", other.Note);
    }

    [Fact(DisplayName = "E6: Lauf speichern und laden behält Werte, Startwert und Bild-Kopien")]
    public void Save_and_list()
    {
        var shot = Png("trendo-kampf.png", 40, 30, Good);
        var r = GameRuns.From(Log, 42, "unreal", ["-game"], [(shot, "Kampf")], new DateTime(2026, 11, 14, 10, 0, 0));
        var root = Path.Combine(_dir, "laeufe");
        var d1 = GameRuns.Save(root, r); var d2 = GameRuns.Save(root, r with { At = r.At.AddMinutes(1) });
        Assert.NotEqual(d1, d2);
        File.Delete(shot); // die Kopie im Lauf bleibt
        var list = GameRuns.List(root);
        Assert.Equal(2, list.Count); Assert.Equal(42, list[0].Seed);
        Assert.Equal(r.Values, list[1].Values);
        Assert.True(File.Exists(list[1].Shots["Kampf"]));
        Assert.Empty(GameRuns.Compare(list[1], list[0]).Changes);
    }

    // ───────────── E1: Engine einrichten ─────────────
    static List<EngineFound> Found(bool unity, bool godot) =>
    [
        new("unreal", "Unreal Engine", null, null, false, "nicht installiert", ""),
        unity ? new("unity", "Unity", "6000.0.23f1", "C:/U/Unity.exe", true, "bereit", "") : new("unity", "Unity", null, null, false, "blockiert", ""),
        godot ? new("godot", "Godot Engine", "4.4.1-stable", "C:/G/Godot_v4.4.1-stable_win64.exe", true, "bereit", "") : new("godot", "Godot Engine", null, null, false, "nicht gefunden", ""),
    ];

    [Fact(DisplayName = "E1: Fehlt die Engine, nennt Trendo genau einen Schritt mit offizieller Seite, nichts gilt als geprüft")]
    public void Missing_engine_one_step()
    {
        var s = GameEngineSetup.Of("unreal", Found(false, false), null, null);
        Assert.Equal("install", s.Action);
        Assert.NotNull(s.Next); Assert.Contains("unrealengine.com", s.Next); Assert.Contains("lädt keine Engine", s.Next);
        Assert.Single(s.Next!.Split(". ", StringSplitOptions.RemoveEmptyEntries)); // ein Satz
        Assert.DoesNotContain(s.Steps, x => x.State == GameEngineSetup.Checked);
    }

    [Fact(DisplayName = "E1: Geprüft nur mit bestandenem Bericht; Fehlschlag und fehlender Bericht bleiben offen")]
    public void Checked_only_with_report()
    {
        var dir = Path.Combine(_dir, "Spiel-Godot"); Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, "project.godot"), "");
        var none = GameEngineSetup.Of("godot", Found(false, true), dir, null);
        Assert.Equal("check", none.Action); Assert.Equal(GameEngineSetup.Open, none.Steps[2].State);
        var bad = GameEngineSetup.Of("godot", Found(false, true), dir, "2026-11-14 10:00 Godot\nGodot Level-Prüfung: ✓ ok (Exitcode 0)\nGodot Spiel-Prüfung: ⚠ Figur fällt (Exitcode 1)\n");
        Assert.Equal(GameEngineSetup.Failed, bad.Steps[2].State); Assert.Contains("Figur fällt", bad.Next);
        var good = GameEngineSetup.Of("godot", Found(false, true), dir, "2026-11-14 10:00 Godot\nGodot Level-Prüfung: ✓ ok (Exitcode 0)\nGodot Spiel-Prüfung: ✓ ok (Exitcode 0)\n");
        Assert.Null(good.Next); Assert.All(good.Steps, x => Assert.NotEqual(GameEngineSetup.Open, x.State));
        var noProject = GameEngineSetup.Of("godot", Found(false, true), Path.Combine(_dir, "gibtsnicht"), "x\nGodot A: ✓");
        Assert.Equal("create", noProject.Action);
    }

    [Fact(DisplayName = "E1: Unity-Bericht zählt nur, wenn EditMode und PlayMode vollständig bestanden")]
    public void Unity_report()
    {
        Assert.True(GameEngineSetup.ReadReport("unity", "d\nEditMode: 4 von 4 bestanden, 0 fehlgeschlagen (Exitcode 0)\nPlayMode: 6 von 6 bestanden, 0 fehlgeschlagen (Exitcode 0)").Ok);
        Assert.False(GameEngineSetup.ReadReport("unity", "d\nEditMode: 4 von 4 bestanden, 0 fehlgeschlagen (Exitcode 0)\nPlayMode: 5 von 6 bestanden, 1 fehlgeschlagen (Exitcode 2)").Ok);
        Assert.False(GameEngineSetup.ReadReport("unity", "d\nEditMode: kein Ergebnis (Exitcode 1)\nPlayMode: 0 von 0 bestanden").Ok);
        Assert.False(GameEngineSetup.ReadReport("unreal", "d\nNicht alles ging – Details unten.\n✗ Kompilieren fehlgeschlagen").Ok);
        Assert.True(GameEngineSetup.ReadReport("unreal", "d\nAlles bereit – „Spielen“ startet dein 3D-Spiel.\n✓ Kompiliert (Exitcode 0)").Ok);
    }

    // ───────────── E7: Dienste ─────────────
    [Fact(DisplayName = "E7: Ohne Bezahl-Dienst und ohne lokale KI hat jede Art eine nutzbare Wahl")]
    public void Every_kind_without_paid()
    {
        foreach (var (kind, _) in GameServices.Kinds)
        {
            var pick = GameServices.Pick(kind, _ => false);
            if (kind == "sprache") { Assert.Null(pick); Assert.Contains(GameServices.Route(kind, _ => false), o => o.Provider.Type == GameServices.Local); continue; } // Sprache gibt es nur lokal, die Route nennt sie
            Assert.NotNull(pick); Assert.Equal(GameServices.Free, pick!.Provider.Type);
        }
        Assert.Equal("ki-bild", GameServices.Pick("bild", id => id == "ki-bild")!.Provider.Id); // eingerichtete lokale KI zuerst
        Assert.DoesNotContain(GameServices.All, p => p.Type == GameServices.Paid);
    }

    [Fact(DisplayName = "E7: Bezahl-Dienste nur, wenn selbst eingerichtet, und immer nach den freien")]
    public void Paid_only_when_configured()
    {
        var paid = new GameServices.Provider("teuer", "bild", "Bezahl-Bild", GameServices.Paid, "kommerziell", "");
        var providers = GameServices.All.Append(paid).ToList();
        Assert.DoesNotContain(GameServices.Route("bild", _ => false, null, providers), o => o.Provider.Id == "teuer");
        var withPaid = GameServices.Route("bild", _ => false, new HashSet<string> { "teuer" }, providers);
        Assert.Equal("teuer", withPaid[^1].Provider.Id);
    }

    [Fact(DisplayName = "E7: Jedes eingefügte Asset hat Herkunft und Lizenz; ohne Lizenz wird nichts aufgenommen")]
    public void Assets_have_license()
    {
        var doc = new JsonObject { ["title"] = "Test" };
        var file = Png("held.png", 20, 20, Good);
        var ki = GameServices.All.First(p => p.Id == "ki-bild");
        Assert.Throws<ArgumentException>(() => GameServices.Add(doc, Path.Combine(_dir, "assets"), file, ki, "", "sdxl-turbo"));
        var e = GameServices.Add(doc, Path.Combine(_dir, "assets"), file, ki, "Modell-Lizenz: OpenRAIL++", "sdxl-turbo");
        Assert.Contains("sdxl-turbo", (string?)e["source"]);
        GameServices.Add(doc, Path.Combine(_dir, "assets"), file, GameServices.All.First(p => p.Id == "polyhaven"), "", "Fels", "https://polyhaven.com/a/rock");
        Assert.Empty(GameServices.MissingLicense(doc));
        Assert.Equal(2, (doc["assets"] as JsonArray)!.Count);
    }

    // ───────────── D5: Ähnliche Spiele ─────────────
    sealed class FakeWeb(Dictionary<string, string> pages) : IWebFetch
    {
        public List<string> Asked { get; } = [];
        public Task<WebReply> GetAsync(string url, CancellationToken ct = default)
        {
            Asked.Add(url);
            return Task.FromResult(pages.TryGetValue(url, out var t) ? new WebReply(200, System.Text.Encoding.UTF8.GetBytes(t)) : new WebReply(404, []));
        }
    }

    static string Summary(string title, string desc, string lang, string type = "standard") =>
        new JsonObject { ["type"] = type, ["title"] = title, ["description"] = desc, ["extract"] = desc, ["content_urls"] = new JsonObject { ["desktop"] = new JsonObject { ["page"] = $"https://{lang}.wikipedia.org/wiki/{Uri.EscapeDataString(title)}" } } }.ToJsonString();

    [Fact(DisplayName = "D5: Nur belegte Spiele erscheinen, jedes mit Wikipedia-Link; Erfundenes und Nicht-Spiele fallen weg")]
    public async Task References_need_source()
    {
        var web = new FakeWeb(new()
        {
            [GameReferences.SummaryUrl("de", "Stardew Valley")] = Summary("Stardew Valley", "Computerspiel von 2016", "de"),
            [GameReferences.SummaryUrl("en", "Celeste")] = Summary("Celeste (video game)", "2018 video game", "en"),
            [GameReferences.SummaryUrl("de", "Celeste")] = Summary("Celeste", "Begriffsklärung", "de", "disambiguation"),
            [GameReferences.SummaryUrl("de", "Garten")] = Summary("Garten", "abgegrenztes Stück Land", "de"),
        });
        var titles = GameReferences.Candidates("- Stardew Valley (2016)\n2. Celeste\n* Garten\nErfundenes Spiel XY\nStardew Valley");
        Assert.Equal(["Stardew Valley", "Celeste", "Garten", "Erfundenes Spiel XY"], titles);
        var refs = await GameReferences.Verify(web, titles);
        Assert.Equal(["Stardew Valley", "Celeste (video game)"], refs.Select(r => r.Title));
        Assert.All(refs, r => Assert.StartsWith("https://", r.Url));
        Assert.All(refs, r => Assert.Contains("wikipedia.org", r.Url));
    }

    [Fact(DisplayName = "D5: Lässt sich nichts belegen, bleibt die Liste leer; gespeicherte Einträge ohne Beleg werden nie gezeigt")]
    public async Task References_none()
    {
        var refs = await GameReferences.Verify(new FakeWeb([]), GameReferences.Candidates("keine"));
        Assert.Empty(refs);
        var d = new JsonObject();
        GameReferences.Store(d, refs, new DateTime(2026, 11, 14));
        Assert.Empty(GameReferences.Read(d));
        d["references"] = new JsonArray(new JsonObject { ["title"] = "Ohne Quelle", ["url"] = "" }, new JsonObject { ["title"] = "Http", ["url"] = "http://x.example" });
        Assert.Empty(GameReferences.Read(d));
        Assert.Null(GameReferences.FromSummary("Spiel", Summary("Spiel", "Videospiel", "de").Replace("de.wikipedia.org", "evil.example"), "de"));
    }
}
