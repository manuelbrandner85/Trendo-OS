using System.Text.Json.Nodes;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Paket 01 / S1-15 Save &amp; Progression: versionierter Spielstand, Hebung alter Stände, Prüfung gegen das Spiel, Probespiel und Browser-Spiel.</summary>
public class GameSavesTests
{
    static readonly string[] L1 = ["S...C...Z", "#########"], L2 = ["S..C.Z", "######"];

    [Fact(DisplayName = "Spielstand S1-15: speichern und laden ergibt denselben Stand (Format 1, Entscheidungen bleiben)")]
    public void Roundtrip()
    {
        var s = new GameSave { Level = 1, Score = 120, Lives = 2, CheckX = 3, CheckY = 0, Coins = 7, Defeated = 2, PlaySeconds = 95, Flags = new() { ["tuer-burg"] = "offen" } };
        var back = GameSaves.Parse(GameSaves.ToJson(s), 2);
        Assert.Equal(s with { SavedUtc = back.SavedUtc, Flags = back.Flags }, back);
        Assert.Equal("offen", back.Flags["tuer-burg"]);
        Assert.Equal(1, JsonNode.Parse(GameSaves.ToJson(s))!["version"]!.GetValue<int>());
        Assert.Equal("Level 2 von 2 · 120 Punkte · 2 Leben · am Speicherpunkt · 1 min gespielt", GameSaves.Describe(back, 2));
    }

    [Fact(DisplayName = "Spielstand S1-15: unversionierter alter Stand (Kurznamen cx/cy/time) wird auf Format 1 gehoben")]
    public void Migrate_unversioned()
    {
        var s = GameSaves.Parse("""{"level":1,"score":30,"lives":4,"cx":3,"cy":0,"time":61}""", 2);
        Assert.Equal((1, 30, 4, 3, 0, 61), (s.Level, s.Score, s.Lives, s.CheckX, s.CheckY, s.PlaySeconds));
        Assert.Equal(GameSaves.Current, GameSaves.Migrate(JsonNode.Parse("""{"v":1,"level":0}""")!.AsObject())["version"]!.GetValue<int>());
    }

    [Fact(DisplayName = "Spielstand S1-15 Gegenproben: neuere Fassung, kaputte Datei, fehlendes Level werden abgelehnt; unsinnige Werte begrenzt")]
    public void Negative()
    {
        Assert.Contains("neueren Fassung", Assert.Throws<InvalidDataException>(() => GameSaves.Parse("""{"version":2,"level":0}""", 1)).Message);
        Assert.Contains("beschädigt", Assert.Throws<InvalidDataException>(() => GameSaves.Parse("{kaputt", 1)).Message);
        Assert.Throws<InvalidDataException>(() => GameSaves.Parse("[1,2]", 1));
        Assert.Contains("passt nicht", Assert.Throws<InvalidDataException>(() => GameSaves.Parse("""{"version":1,"level":5}""", 2)).Message);
        Assert.Throws<InvalidDataException>(() => GameSaves.Parse("""{"version":1,"level":"zwei"}""", 2));
        var c = GameSaves.Parse("""{"version":1,"level":0,"score":-5,"lives":0,"coins":-1}""", 1);
        Assert.Equal((0, 1, 0), (c.Score, c.Lives, c.Coins));
    }

    [Fact(DisplayName = "Spielstand S1-15: Speicherpunkt gilt nur auf C oder S, sonst Levelstart")]
    public void Checkpoint()
    {
        Assert.True(GameSaves.CheckpointValid(new GameSave { CheckX = 4, CheckY = 0 }, L1));
        Assert.False(GameSaves.CheckpointValid(new GameSave { CheckX = 2, CheckY = 0 }, L1));
        Assert.False(GameSaves.CheckpointValid(new GameSave { CheckX = 40, CheckY = 9 }, L1));
        Assert.False(GameSaves.CheckpointValid(new GameSave(), L1));
    }

    [Fact(DisplayName = "Spielstand S1-15 Probespiel: Stand festhalten und in neuem Spiel fortsetzen – Level, Speicherpunkt, Punkte, Leben")]
    public void Sim_snapshot_restore()
    {
        var sim = new PlatformSim(L1, moreLevels: [L2]);
        var save = new GameSave { Level = 1, Score = 55, Lives = 2, CheckX = 3, CheckY = 0 };
        sim.Restore(save);
        Assert.Equal((1, 55, 2), (sim.LevelIndex, sim.Score, sim.Lives));
        Assert.Equal((3, 0), sim.Respawn);
        Assert.Equal(3 * PlatformSim.T + 4, sim.Player.X);
        var snap = sim.Snapshot();
        Assert.Equal((1, 55, 2, 3, 0), (snap.Level, snap.Score, snap.Lives, snap.CheckX, snap.CheckY));
        var fresh = new PlatformSim(L1, moreLevels: [L2]); fresh.Restore(save with { CheckX = 1 }); // ungültiger Punkt → Start
        Assert.Equal((0, 0), fresh.Respawn);
        Assert.Throws<InvalidDataException>(() => new PlatformSim(L1).Restore(save));
    }

    [Fact(DisplayName = "Spielstand S1-15 Browser-Spiel (echt mit Node): Speichern am Speicherpunkt, neuer Start setzt fort, neuere Fassung wird ignoriert, Spiel ohne Spielstände speichert nicht")]
    public void Browser_game_js()
    {
        Assert.Contains("saveGame()", GameStudio.GameJs("plattform")); Assert.DoesNotContain("/*SPIELSTAND*/", GameStudio.GameJs("plattform"));
        Assert.DoesNotContain("saveGame", GameStudio.GameJs("snake"));
        if (Cli.Find("node") is not { } node) return; // ohne Node nur die Textprüfung
        var dir = Path.Combine(Path.GetTempPath(), "trendo-save-js-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            string[] Lines(GameStudio.GameSettings s, string store, string after)
            {
                File.WriteAllText(Path.Combine(dir, "spiel.js"), GameStudio.ConfigJs(s) + GameStudio.GameJs("plattform"));
                File.WriteAllText(Path.Combine(dir, "lauf.js"), Harness);
                var r = Cli.Run(node, [Path.Combine(dir, "lauf.js"), Path.Combine(dir, "spiel.js"), store, after], dir, 30000);
                Assert.True(r.Ok, r.Err + r.Out);
                return r.Out.Trim().Split('\n');
            }
            string Run(GameStudio.GameSettings s, string store, string after) => Lines(s, store, after)[^1]; // Speicher nach dem Lauf
            string Out(GameStudio.GameSettings s, string store, string after) => Lines(s, store, after)[^2]; // Zustand aus out()
            var g = new GameStudio.GameSettings { Level = L1, MoreLevels = [L2], Lives = 3 };
            // 1. Lauf: Level 2 erreicht, am Speicherpunkt gespeichert
            var store = JsonNode.Parse(Run(g, "{}", "load(1);sx=3;sy=0;p=77;lives=2;cg=4;saveGame();"))!;
            var saved = JsonNode.Parse(store["trendo-spielstand:Test"]!.GetValue<string>())!;
            Assert.Equal((1, 77, 2, 3), (saved["version"]!.GetValue<int>(), saved["score"]!.GetValue<int>(), saved["lives"]!.GetValue<int>(), saved["checkX"]!.GetValue<int>()));
            Assert.Equal(1, GameSaves.Parse(saved.ToJsonString(), 2).Level); // gleiches Format wie in Trendo
            // 2. Lauf: setzt fort
            var st = JsonNode.Parse(Out(g, store.ToJsonString(), "out({li,p,lives,sx,sy});"))!;
            Assert.Equal((1, 77, 2, 3, 0), (st["li"]!.GetValue<int>(), st["p"]!.GetValue<int>(), st["lives"]!.GetValue<int>(), st["sx"]!.GetValue<int>(), st["sy"]!.GetValue<int>()));
            // neuere Fassung wird nicht geladen
            var newer = new JsonObject { ["trendo-spielstand:Test"] = """{"version":9,"level":1,"score":5,"lives":1}""" };
            Assert.Equal(0, JsonNode.Parse(Out(g, newer.ToJsonString(), "out({li});"))!["li"]!.GetValue<int>());
            // Spielende löscht den Stand; „ohne Spielstände“ speichert nicht
            Assert.False(JsonNode.Parse(Run(g, store.ToJsonString(), "lives=1;hit();"))!.AsObject().ContainsKey("trendo-spielstand:Test"));
            Assert.Empty(JsonNode.Parse(Run(g with { Saves = false }, "{}", "saveGame();"))!.AsObject());
        }
        finally { Directory.Delete(dir, true); }
    }

    // Kleine Browser-Nachbildung für Node: Leinwand ohne Zeichnen, localStorage im Speicher, kein Bild-Takt.
    const string Harness = """
const fs=require('fs'),vm=require('vm');const [,,file,storeJson,after]=process.argv;const store=JSON.parse(storeJson);
const noop=new Proxy(function(){},{get:(t,k)=>k==='measureText'?(()=>({width:0})):noop,apply:()=>undefined});
const canvas={width:480,height:360,getContext:()=>noop,addEventListener(){}};const status={textContent:''};
const ctx={document:{title:'Test',getElementById:id=>id==='c'?canvas:status},localStorage:{getItem:k=>k in store?store[k]:null,setItem:(k,v)=>{store[k]=String(v);},removeItem:k=>{delete store[k];}},
requestAnimationFrame(){},addEventListener(){},Image:function(){},Audio:function(){},AudioContext:function(){},Math,JSON,Number,Object,Date,console,
out:o=>console.log(JSON.stringify(o))};vm.createContext(ctx);
vm.runInContext(fs.readFileSync(file,'utf8'),ctx);vm.runInContext(after,ctx);console.log(JSON.stringify(store));
""";
}
