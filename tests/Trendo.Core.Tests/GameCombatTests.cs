using System.Text.Json.Nodes;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Paket 01 / S1-05 (Teil 3): Kampf mit Waffen – Schwert, Werfen, Gegner mit Trefferpunkten (Boss), Abklingzeit; Probespiel und Browser-Spiel gleich.</summary>
public class GameCombatTests
{
    static readonly string[] Lv = [new string('.', 30), "S" + new string('.', 10) + "E" + new string('.', 17) + "Z", new string('#', 30)];

    static PlatformSim Ready(string weapon, int hp = 1)
    {
        var s = new PlatformSim(Lv, weapon: weapon, props: hp > 1 ? new Dictionary<string, double> { ["11,1:hp"] = hp } : null);
        for (var i = 0; i < 10; i++) s.Step(false, false, false);
        return s;
    }
    static void Near(PlatformSim s, double gap) { var f = s.Foes[0]; s.Player.X = f.X - gap; s.Player.Facing = 1; }

    [Fact(DisplayName = "Kampf S1-05: Schwert trifft den Gegner vor der Figur, nicht hinter ihr und nicht zu weit weg; nur neuer Tastendruck greift an")]
    public void Sword()
    {
        var s = Ready("schwert"); Near(s, 30);
        s.Step(false, false, false, attack: true);
        Assert.False(s.Foes[0].Alive); Assert.Equal(1, s.FoesDefeated); Assert.True(s.Swing > 0);
        var behind = Ready("schwert"); Near(behind, 30); behind.Player.Facing = -1;
        behind.Step(false, false, false, attack: true); Assert.True(behind.Foes[0].Alive);
        var far = Ready("schwert"); Near(far, 80); far.Step(false, false, false, attack: true); Assert.True(far.Foes[0].Alive);
        var none = Ready(""); Near(none, 30); none.Step(false, false, false, attack: true); Assert.True(none.Foes[0].Alive); Assert.Equal(0, none.Swing);
    }

    [Fact(DisplayName = "Kampf S1-05: Boss mit 3 Trefferpunkten braucht 3 Schläge (Abklingzeit, Taste neu drücken), bringt 10 Punkte und „Boss besiegt!“")]
    public void Boss()
    {
        var s = Ready("schwert", 3);
        for (var hit = 1; hit <= 3; hit++)
        {
            Near(s, 30); s.Step(false, false, false, attack: true);
            Assert.Equal(3 - hit, Math.Max(0, s.Foes[0].Hp));
            s.Step(false, false, false, attack: true); // gehalten: kein weiterer Schlag
            Assert.Equal(3 - hit, Math.Max(0, s.Foes[0].Hp));
            for (var i = 0; i < 20; i++) s.Step(false, false, false); // Abklingzeit
        }
        Assert.False(s.Foes[0].Alive); Assert.Equal(10, s.Score); Assert.Equal("Boss besiegt!", s.Note);
    }

    [Fact(DisplayName = "Kampf S1-05: Geschoss fliegt zum Gegner und besiegt ihn; Wand hält es auf; Draufspringen kostet den Boss nur einen Trefferpunkt")]
    public void Throw_and_stomp()
    {
        var s = Ready("werfen"); Near(s, 200);
        s.Step(false, false, false, attack: true); Assert.Single(s.Shots);
        for (var i = 0; i < 40 && s.Foes[0].Alive; i++) s.Step(false, false, false);
        Assert.False(s.Foes[0].Alive); Assert.Empty(s.Shots);
        var wall = new PlatformSim([".........", "S..#..E.Z", "#########"], weapon: "werfen");
        for (var i = 0; i < 10; i++) wall.Step(false, false, false);
        wall.Step(false, false, false, attack: true);
        for (var i = 0; i < 60; i++) wall.Step(false, false, false);
        Assert.True(wall.Foes[0].Alive); Assert.Empty(wall.Shots);
        var boss = Ready("", 2); var f = boss.Foes[0];
        boss.Player.X = f.X; boss.Player.Y = f.Y - 30; boss.Player.Vy = 5; boss.Step(false, false, false);
        Assert.True(f.Alive); Assert.Equal(1, f.Hp);
    }

    [Fact(DisplayName = "Kampf S1-05: Waffe und Trefferpunkte landen im Spiel; unbekannte Waffe abgelehnt; Trefferpunkte auf 1–9 begrenzt")]
    public void Config()
    {
        var g = GameStudio.Validate(new GameStudio.GameSettings { Level = Lv, Weapon = "werfen", Props = new() { ["11,1:hp"] = 20 } });
        Assert.Equal(9, g.Props["11,1:hp"]);
        Assert.Contains("\"weapon\":\"werfen\"", GameStudio.ConfigJs(g));
        Assert.Throws<ArgumentException>(() => GameStudio.Validate(new GameStudio.GameSettings { Weapon = "laser" }));
        Assert.Equal("", GameDesign.Weapon(new JsonObject { ["weapon"] = "laser" }));
        Assert.Equal("schwert", GameDesign.Weapon(new JsonObject { ["weapon"] = "schwert" }));
    }

    [Fact(DisplayName = "Kampf S1-05 Browser-Spiel (echt mit Node): Schwert besiegt Gegner vor der Figur; Boss braucht zwei Schläge; Geschoss fliegt und trifft; ohne Waffe nichts")]
    public void Browser_js()
    {
        if (Cli.Find("node") is not { } node) return;
        var dir = Path.Combine(Path.GetTempPath(), "trendo-kampf-js-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            string Run(GameStudio.GameSettings g, string after)
            {
                File.WriteAllText(Path.Combine(dir, "spiel.js"), GameStudio.ConfigJs(g) + GameStudio.GameJs("plattform"));
                File.WriteAllText(Path.Combine(dir, "lauf.js"), Harness);
                var r = Cli.Run(node, [Path.Combine(dir, "lauf.js"), Path.Combine(dir, "spiel.js"), after], dir, 30000);
                Assert.True(r.Ok, r.Err + r.Out); return r.Out.Trim().Split('\n')[^1];
            }
            const string near = "const f=foes[0];pl.x=f.x-30;face=1;";
            var sword = JsonNode.Parse(Run(new() { Level = Lv, Weapon = "schwert" }, near + "key('KeyX');out({a:f.a,fk});"))!;
            Assert.Equal((0, 1), (sword["a"]!.GetValue<int>(), sword["fk"]!.GetValue<int>()));
            var boss = JsonNode.Parse(Run(new() { Level = Lv, Weapon = "schwert", Props = new() { ["11,1:hp"] = 2 } },
                near + "key('KeyX');const h1=f.hp;for(let i=0;i<25;i++)step();pl.x=f.x-30;face=1;key('KeyX');out({h1,a:f.a,p});"))!;
            Assert.Equal((1, 0, 10), (boss["h1"]!.GetValue<int>(), boss["a"]!.GetValue<int>(), boss["p"]!.GetValue<int>()));
            var thrown = JsonNode.Parse(Run(new() { Level = Lv, Weapon = "werfen" }, "const f=foes[0];pl.x=f.x-200;pl.vx=0;face=1;key('KeyJ');const n=shots.length;for(let i=0;i<40&&f.a;i++)step();out({n,a:f.a});"))!;
            Assert.Equal((1, 0), (thrown["n"]!.GetValue<int>(), thrown["a"]!.GetValue<int>()));
            var unarmed = JsonNode.Parse(Run(new() { Level = Lv }, near + "key('KeyX');out({a:f.a,swing});"))!;
            Assert.Equal((1, 0), (unarmed["a"]!.GetValue<int>(), unarmed["swing"]!.GetValue<int>()));
        }
        finally { Directory.Delete(dir, true); }
    }

    const string Harness = """
const fs=require('fs'),vm=require('vm');const [,,file,after]=process.argv;const store={};const H={};
const noop=new Proxy(function(){},{get:(t,k)=>k==='measureText'?(()=>({width:0})):noop,apply:()=>undefined});
const canvas={width:480,height:360,getContext:()=>noop,addEventListener(){}};const status={textContent:''};
const ctx={document:{title:'Test',getElementById:id=>id==='c'?canvas:status},localStorage:{getItem:k=>k in store?store[k]:null,setItem:(k,v)=>{store[k]=String(v);},removeItem:k=>{delete store[k];}},
requestAnimationFrame(){},addEventListener(t,f){(H[t]=H[t]||[]).push(f);},Image:function(){},Audio:function(){},AudioContext:function(){},Math,JSON,Number,Object,Date,console,String,
key:c=>(H.keydown||[]).forEach(f=>f({code:c,key:c,repeat:false,preventDefault(){}})),out:o=>console.log(JSON.stringify(o))};vm.createContext(ctx);
vm.runInContext(fs.readFileSync(file,'utf8'),ctx);vm.runInContext(after,ctx);
""";
}
