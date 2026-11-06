using System.Text.Json.Nodes;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Paket 01 / S1-05 (Teil 4): Fahrzeuge – Wagen (schnell, fängt einen Treffer ab) und Ufo (fliegen mit Treibstoff), ein- und aussteigen; Probespiel und Browser-Spiel gleich.</summary>
public class GameVehicleTests
{
    static string[] Lv(string row) => [.. Enumerable.Repeat(new string('.', row.Length), 8), row, new string('#', row.Length)];
    static PlatformSim Into(PlatformSim s) { for (var i = 0; i < 200 && s.Vehicle is null; i++) s.Step(false, true, false); Assert.NotNull(s.Vehicle); return s; }

    [Fact(DisplayName = "Fahrzeug S1-05: Wagen durch Berühren einsteigen, 1,8-fach schnell, fängt einen Stachel-Treffer ab (Leben bleiben), danach zu Fuß")]
    public void Car()
    {
        var s = Into(new PlatformSim(Lv("S.V....................^...........Z"), lives: 3));
        Assert.Equal("wagen", s.Vehicle);
        var x0 = s.Player.X; s.Step(false, true, false); var car = s.Player.X - x0;
        var walk = new PlatformSim(Lv("S..................................Z")); for (var i = 0; i < 12; i++) walk.Step(false, false, false);
        var w0 = walk.Player.X; walk.Step(false, true, false);
        Assert.Equal(1.8, car / (walk.Player.X - w0), 2);
        for (var i = 0; i < 200 && s.Vehicle is not null; i++) s.Step(false, true, false); // in die Stacheln
        Assert.Null(s.Vehicle); Assert.Equal(3, s.Lives); Assert.StartsWith("Fahrzeug kaputt", s.Note);
    }

    [Fact(DisplayName = "Fahrzeug S1-05: E steigt aus, das Fahrzeug bleibt stehen und lässt sich (nach kurzer Pause) wieder nehmen – gleiche Art")]
    public void Exit_and_reenter()
    {
        var s = Into(new PlatformSim(Lv("S.V..............................Z"), texts: new Dictionary<string, string> { ["2,8:kind"] = "ufo" }));
        Assert.Equal("ufo", s.Vehicle);
        s.Step(false, false, false, use: true);
        Assert.Null(s.Vehicle); Assert.Equal("Ausgestiegen", s.Note);
        s.Step(false, false, false, use: true); s.Step(false, false, false); Assert.Null(s.Vehicle); // Pause
        for (var i = 0; i < 40; i++) s.Step(false, false, false);
        Assert.Equal("ufo", s.Vehicle); // wieder eingestiegen, gleiche Art
    }

    [Fact(DisplayName = "Fahrzeug S1-05: Ufo fliegt mit gehaltener Sprungtaste deutlich höher als ein Sprung; Treibstoff 10 s, danach Aussteigen „Treibstoff leer“")]
    public void Ufo()
    {
        var s = Into(new PlatformSim(Lv("S.V..............................Z"), texts: new Dictionary<string, string> { ["2,8:kind"] = "ufo" }));
        var y0 = s.Player.Y; var top = y0;
        for (var i = 0; i < 40; i++) { s.Step(false, false, true); top = Math.Min(top, s.Player.Y); }
        Assert.True(y0 - top > 150, $"nur {y0 - top} px");
        Assert.Equal(600 - 40, s.Fuel);
        for (var i = 0; i < 600 && s.Vehicle is not null; i++) s.Step(false, false, true);
        Assert.Null(s.Vehicle); Assert.Equal("Treibstoff leer", s.Note);
    }

    [Fact(DisplayName = "Fahrzeug S1-05: Absturz trifft auch im Fahrzeug; V und die Art sind erlaubt und landen im Spiel")]
    public void Fall_and_config()
    {
        var s = Into(new PlatformSim(Lv("S.V...............................").Select((r, i) => i == 9 ? "###." + new string('.', r.Length - 4) : r).ToArray(), lives: 3));
        for (var i = 0; i < 300 && s.Lives == 3; i++) s.Step(false, true, false);
        Assert.Equal(2, s.Lives); Assert.Null(s.Vehicle);
        var g = GameStudio.Validate(new GameStudio.GameSettings { Level = Lv("S.V......Z"), Texts = new() { ["2,8:kind"] = "ufo" } });
        Assert.Contains("\"2,8:kind\":\"ufo\"", GameStudio.ConfigJs(g));
    }

    [Fact(DisplayName = "Fahrzeug S1-05 Browser-Spiel (echt mit Node): Wagen einsteigen und schneller, Treffer kostet nur den Wagen; Ufo fliegt mit Treibstoff; E steigt aus")]
    public void Browser_js()
    {
        if (Cli.Find("node") is not { } node) return;
        var dir = Path.Combine(Path.GetTempPath(), "trendo-fahr-js-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            string Run(GameStudio.GameSettings g, string after)
            {
                File.WriteAllText(Path.Combine(dir, "spiel.js"), GameStudio.ConfigJs(g) + GameStudio.GameJs("plattform"));
                File.WriteAllText(Path.Combine(dir, "lauf.js"), Harness);
                var r = Cli.Run(node, [Path.Combine(dir, "lauf.js"), Path.Combine(dir, "spiel.js"), after], dir, 30000);
                Assert.True(r.Ok, r.Err + r.Out); return r.Out.Trim().Split('\n')[^1];
            }
            const string enter = "k.ArrowRight=1;for(let i=0;i<200&&!veh;i++)step();";
            var car = JsonNode.Parse(Run(new() { Level = Lv("S.V..............................Z"), Lives = 3 }, enter + "const v=veh,x0=pl.x;step();const dx=pl.x-x0;hit();out({v,dx,veh,lives});"))!;
            Assert.Equal("wagen", car["v"]!.GetValue<string>()); Assert.Null(car["veh"]); Assert.Equal(3, car["lives"]!.GetValue<int>());
            Assert.Equal((2.2 + 5 * .35) * 1.8, car["dx"]!.GetValue<double>(), 3);
            var ufo = JsonNode.Parse(Run(new() { Level = Lv("S.V..............................Z"), Texts = new() { ["2,8:kind"] = "ufo" } },
                enter + "k.ArrowRight=0;k.Space=1;const y0=pl.y;for(let i=0;i<40;i++)step();const up=y0-pl.y,f=fuel;k.Space=0;key('KeyE');out({up,f,veh,note});"))!;
            Assert.True(ufo["up"]!.GetValue<double>() > 150); Assert.Equal(560, ufo["f"]!.GetValue<int>());
            Assert.Null(ufo["veh"]); Assert.Equal("Ausgestiegen", ufo["note"]!.GetValue<string>());
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
