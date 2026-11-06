using System.Text.Json.Nodes;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Paket 01 / S1-05 Advanced Gameplay Creator (erster Teil): Fähigkeiten Doppelsprung und Sprint, Aufgaben mit Fortschritt – gleich im Probespiel und im Browser-Spiel.</summary>
public class GameplayTests
{
    static readonly string[] Flat = [.. Enumerable.Repeat(new string('.', 52), 9), "S" + new string('.', 51), new string('#', 52)];

    static PlatformSim Landed(PlatformSim s) { for (var i = 0; i < 10; i++) s.Step(false, false, false); Assert.True(s.Player.On); return s; }

    static double Apex(PlatformSim s, bool second)
    {
        Landed(s); var top = s.Player.Y;
        s.Step(false, false, true); // Absprung
        for (var i = 0; i < 40; i++)
        {
            var jump = second && i is 8 or 9; // losgelassen, dann in der Luft neu gedrückt
            s.Step(false, false, jump); top = Math.Min(top, s.Player.Y);
        }
        return top;
    }

    [Fact(DisplayName = "Gameplay S1-05: Doppelsprung nur mit Fähigkeit und nur ein Mal in der Luft; Halten der Taste löst ihn nicht aus")]
    public void Double_jump()
    {
        var without = Apex(new PlatformSim(Flat), true);
        var with = Apex(new PlatformSim(Flat, abilities: ["doppelsprung"]), true);
        Assert.True(with < without - 20, $"mit {with}, ohne {without}"); // deutlich höher
        var held = Landed(new PlatformSim(Flat, abilities: ["doppelsprung"])); var top = held.Player.Y;
        for (var i = 0; i < 30; i++) { held.Step(false, false, true); top = Math.Min(top, held.Player.Y); if (held.Player.On && i > 2) break; }
        Assert.True(top > with, "gehalten = nur einfacher Sprung");
    }

    [Fact(DisplayName = "Gameplay S1-05: Sprint 1,6-fach nur mit Fähigkeit und gedrückter Umschalt-Taste")]
    public void Sprint()
    {
        double Run(PlatformSim s, bool sprint) { for (var i = 0; i < 60; i++) s.Step(false, true, false, sprint); return s.Player.X; }
        var normal = Run(new PlatformSim(Flat), true);
        var fast = Run(new PlatformSim(Flat, abilities: ["sprint"]), true);
        var noKey = Run(new PlatformSim(Flat, abilities: ["sprint"]), false);
        Assert.Equal(normal, noKey);
        Assert.InRange((fast - 4) / (normal - 4), 1.55, 1.65);
    }

    [Fact(DisplayName = "Gameplay S1-05: Aufgaben (Regeln mit Titel) zeigen Fortschritt und Haken, Regeln ohne Titel bleiben unsichtbar")]
    public void Quests()
    {
        var rules = new List<GameStudio.GameRule> { new("muenzen", 2, "punkte", "Schatzsucher"), new("punkte", 999, "leben") };
        var s = new PlatformSim(["S.o.o....Z", "##########"], rules: rules);
        var q = Assert.Single(s.Quests);
        Assert.Equal("Schatzsucher: Münzen gesammelt 0/2", GameStudio.QuestText(q.Rule, q.Value, q.Done));
        for (var i = 0; i < 40 && !s.Quests.Single().Done; i++) s.Step(false, true, false);
        Assert.True(s.Quests.Single().Done);
        Assert.Equal("✓ Schatzsucher", GameStudio.QuestText(s.Quests.Single().Rule, 2, true));
        Assert.True(s.Score >= 12); // 2 Münzen + Belohnung +10
    }

    [Fact(DisplayName = "Gameplay S1-05: Fähigkeiten und Aufgaben landen im gebauten Spiel; unbekannte Fähigkeit abgelehnt; Titel gekürzt")]
    public void Config()
    {
        var g = GameStudio.Validate(new GameStudio.GameSettings { Abilities = ["sprint", "sprint"], Rules = [new("gegner", 3, "leben", "  Held der Wiese mit einem sehr sehr langen Namen ohne Ende  ")] });
        Assert.Equal(["sprint"], g.Abilities); Assert.Equal(40, g.Rules[0].Title!.Length);
        var cfg = JsonNode.Parse(GameStudio.ConfigJs(g)["const CFG=".Length..].TrimEnd(';', '\n'))!;
        Assert.Equal("sprint", cfg["abilities"]![0]!.GetValue<string>());
        Assert.StartsWith("Held der Wiese", cfg["rules"]![0]!["title"]!.GetValue<string>());
        Assert.Throws<ArgumentException>(() => GameStudio.Validate(new GameStudio.GameSettings { Abilities = ["fliegen"] }));
        var doc = new JsonObject { ["abilities"] = new JsonArray("doppelsprung", "fliegen", "doppelsprung"), ["rules"] = new JsonArray(new JsonObject { ["when"] = "muenzen", ["n"] = 3, ["do"] = "leben", ["title"] = "Sammler" }) };
        Assert.Equal(["doppelsprung"], GameDesign.Abilities(doc));
        Assert.Equal("Sammler", GameDesign.Rules(doc).Single().Title);
    }

    [Fact(DisplayName = "Gameplay S1-05 Browser-Spiel (echt mit Node): Doppelsprung per neuem Tastendruck in der Luft, nur mit Fähigkeit; Sprint mit Umschalt")]
    public void Browser_js()
    {
        if (Cli.Find("node") is not { } node) return;
        var dir = Path.Combine(Path.GetTempPath(), "trendo-gameplay-js-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            string Run(GameStudio.GameSettings s, string after)
            {
                File.WriteAllText(Path.Combine(dir, "spiel.js"), GameStudio.ConfigJs(s) + GameStudio.GameJs("plattform"));
                File.WriteAllText(Path.Combine(dir, "lauf.js"), Harness);
                var r = Cli.Run(node, [Path.Combine(dir, "lauf.js"), Path.Combine(dir, "spiel.js"), after], dir, 30000);
                Assert.True(r.Ok, r.Err + r.Out);
                return r.Out.Trim().Split('\n')[^1];
            }
            const string air = "pl.on=false;pl.vy=3;air=1;key('Space');out({vy:pl.vy,air});";
            var with = JsonNode.Parse(Run(new() { Level = Flat, Abilities = ["doppelsprung"] }, air))!;
            Assert.Equal((-10.5, 0), (with["vy"]!.GetValue<double>(), with["air"]!.GetValue<int>()));
            var without = JsonNode.Parse(Run(new() { Level = Flat }, air))!;
            Assert.Equal(3, without["vy"]!.GetValue<double>());
            const string run = "pl.on=true;k.ArrowRight=1;k.ShiftLeft=1;step();out({vx:pl.vx});";
            var fast = JsonNode.Parse(Run(new() { Level = Flat, Abilities = ["sprint"], Speed = 5 }, run))!["vx"]!.GetValue<double>();
            var slow = JsonNode.Parse(Run(new() { Level = Flat, Speed = 5 }, run))!["vx"]!.GetValue<double>();
            Assert.Equal(slow * 1.6, fast, 3);
        }
        finally { Directory.Delete(dir, true); }
    }

    const string Harness = """
const fs=require('fs'),vm=require('vm');const [,,file,after]=process.argv;const store={};const H={};
const noop=new Proxy(function(){},{get:(t,k)=>k==='measureText'?(()=>({width:0})):noop,apply:()=>undefined});
const canvas={width:480,height:360,getContext:()=>noop,addEventListener(){}};const status={textContent:''};
const ctx={document:{title:'Test',getElementById:id=>id==='c'?canvas:status},localStorage:{getItem:k=>k in store?store[k]:null,setItem:(k,v)=>{store[k]=String(v);},removeItem:k=>{delete store[k];}},
requestAnimationFrame(){},addEventListener(t,f){(H[t]=H[t]||[]).push(f);},Image:function(){},Audio:function(){},AudioContext:function(){},Math,JSON,Number,Object,Date,console,
key:c=>(H.keydown||[]).forEach(f=>f({code:c,key:c,preventDefault(){}})),out:o=>console.log(JSON.stringify(o))};vm.createContext(ctx);
vm.runInContext(fs.readFileSync(file,'utf8'),ctx);vm.runInContext(after,ctx);
""";
}
