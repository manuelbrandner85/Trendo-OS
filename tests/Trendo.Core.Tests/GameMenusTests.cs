using System.Text.Json.Nodes;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Paket 01 / S1-14 Ultimate Game Interface: Startmenü, Pause, Einstellungen mit Barrierefreiheit, Steuerungshilfe – echt im Spiel-Code mit Node.</summary>
public class GameMenusTests
{
    static readonly string[] Lv = ["..........", "S........Z", "##########"];

    static JsonNode Run(GameStudio.GameSettings g, string store, string after)
    {
        var node = Cli.Find("node") ?? throw new InvalidOperationException("node fehlt");
        var dir = Path.Combine(Path.GetTempPath(), "trendo-menu-js-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "spiel.js"), GameStudio.ConfigJs(g) + GameStudio.GameJs("plattform"));
            File.WriteAllText(Path.Combine(dir, "lauf.js"), Harness);
            var r = Cli.Run(node, [Path.Combine(dir, "lauf.js"), Path.Combine(dir, "spiel.js"), store, after], dir, 30000);
            Assert.True(r.Ok, r.Err + r.Out);
            return JsonNode.Parse(r.Out.Trim().Split('\n')[^1])!;
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "Spielmenü S1-14: Start im Menü (Spielen, Einstellungen, Steuerung), das Spiel steht still, Tasten erreichen das Spiel nicht; Enter (oder Leertaste) auf „Spielen“ startet")]
    public void Start_menu()
    {
        if (Cli.Find("node") is null) return;
        var r = Run(new() { Level = Lv }, "{}", "const m0=mode,it=items().join('|');key('ArrowLeft');step();step();const f0=fr,kl=k.ArrowLeft?1:0;key('Enter');out({m0,it,f0,kl,m1:mode});");
        Assert.Equal("menu", r["m0"]!.GetValue<string>()); Assert.Equal("Spielen|Einstellungen|Steuerung", r["it"]!.GetValue<string>());
        Assert.Equal(0, r["f0"]!.GetValue<int>()); Assert.Equal(0, r["kl"]!.GetValue<int>());
        Assert.Equal("play", r["m1"]!.GetValue<string>());
    }

    [Fact(DisplayName = "Spielmenü S1-14: mit Spielstand steht „Fortsetzen“ oben und setzt fort; „Spielen“ beginnt neu")]
    public void Continue()
    {
        if (Cli.Find("node") is null) return;
        var store = new JsonObject { ["trendo-spielstand:Test"] = """{"version":1,"level":0,"score":42,"lives":2,"checkX":-1,"checkY":-1}""" }.ToJsonString();
        var cont = Run(new() { Level = Lv }, store, "const it=items()[0];key('Enter');out({it,mode,p});");
        Assert.Equal(("Fortsetzen", "play", 42), (cont["it"]!.GetValue<string>(), cont["mode"]!.GetValue<string>(), cont["p"]!.GetValue<int>()));
        var fresh = Run(new() { Level = Lv }, store, "key('ArrowDown');key('Enter');out({mode,p});");
        Assert.Equal(("play", 0), (fresh["mode"]!.GetValue<string>(), fresh["p"]!.GetValue<int>()));
    }

    [Fact(DisplayName = "Spielmenü S1-14: Esc pausiert (Spiel steht), Esc setzt fort; „Zum Menü“ führt zurück")]
    public void Pause()
    {
        if (Cli.Find("node") is null) return;
        var r = Run(new() { Level = Lv, Menu = false }, "{}", "const m0=mode;step();key('Escape');const m1=mode,f1=fr;step();step();const f2=fr;key('Escape');const m2=mode;key('KeyP');key('ArrowDown');key('ArrowDown');key('ArrowDown');key('Enter');out({m0,m1,f1,f2,m2,m3:mode});");
        Assert.Equal(("play", "pause", "play", "menu"), (r["m0"]!.GetValue<string>(), r["m1"]!.GetValue<string>(), r["m2"]!.GetValue<string>(), r["m3"]!.GetValue<string>()));
        Assert.Equal(r["f1"]!.GetValue<int>(), r["f2"]!.GetValue<int>());
    }

    [Fact(DisplayName = "Spielmenü S1-14: Einstellungen – Lautstärke in 25er-Schritten, hoher Kontrast, große Schrift, ruhige Effekte, Karte; bleiben beim nächsten Start; Steuerungshilfe")]
    public void Settings()
    {
        if (Cli.Find("node") is null) return;
        var r = Run(new() { Level = Lv }, "{}",
            "key('ArrowDown');key('Enter');const m=mode;key('Enter');key('ArrowDown');key('Enter');key('ArrowDown');key('Enter');key('ArrowDown');key('Enter');key('ArrowDown');key('Enter');out({m,S,store:localStorage.getItem(MK)});");
        Assert.Equal("settings", r["m"]!.GetValue<string>());
        var s = r["S"]!;
        Assert.Equal((0, true, true, true, false), (s["vol"]!.GetValue<int>(), s["contrast"]!.GetValue<bool>(), s["big"]!.GetValue<bool>(), s["calm"]!.GetValue<bool>(), s["map"]!.GetValue<bool>()));
        var again = Run(new() { Level = Lv }, new JsonObject { ["trendo-einstellungen:Test"] = r["store"]!.GetValue<string>() }.ToJsonString(), "out({S});");
        Assert.True(again["S"]!["contrast"]!.GetValue<bool>());
        var help = Run(new() { Level = Lv }, "{}", "key('ArrowDown');key('ArrowDown');key('Enter');const m=mode;key('Escape');out({m,back:mode});");
        Assert.Equal(("help", "menu"), (help["m"]!.GetValue<string>(), help["back"]!.GetValue<string>()));
    }

    [Fact(DisplayName = "Spielmenü S1-14: ausgeschaltet startet das Spiel sofort; Einstellung landet im Spiel; Zeichnen mit Menü, Karte und Kontrast läuft ohne Fehler")]
    public void Off_and_draw()
    {
        Assert.Contains("\"menu\":false", GameStudio.ConfigJs(new GameStudio.GameSettings { Menu = false }));
        Assert.Contains("function drawMenu()", GameStudio.GameJs("plattform")); Assert.DoesNotContain("/*MENUS*/", GameStudio.GameJs("plattform"));
        Assert.DoesNotContain("drawMenu", GameStudio.GameJs("snake"));
        if (Cli.Find("node") is null) return;
        var r = Run(new() { Level = Lv, Menu = false }, "{}", "S.contrast=true;S.big=true;S.map=true;step();drawMenu();mode='settings';drawMenu();mode='help';drawMenu();out({ok:1,mode});");
        Assert.Equal(1, r["ok"]!.GetValue<int>());
    }

    const string Harness = """
const fs=require('fs'),vm=require('vm');const [,,file,storeJson,after]=process.argv;const store=JSON.parse(storeJson);const H={};
const noop=new Proxy(function(){},{get:(t,k)=>k==='measureText'?(()=>({width:0})):noop,apply:()=>undefined});
const canvas={width:480,height:360,getContext:()=>noop,addEventListener(){}};const status={textContent:''};
const ctx={document:{title:'Test',getElementById:id=>id==='c'?canvas:status},localStorage:{getItem:k=>k in store?store[k]:null,setItem:(k,v)=>{store[k]=String(v);},removeItem:k=>{delete store[k];}},
requestAnimationFrame(){},addEventListener(t,f){(H[t]=H[t]||[]).push(f);},Image:function(){},Audio:function(){},AudioContext:function(){},Math,JSON,Number,Object,Date,console,String,
key:c=>{let stop=false;const e={code:c,key:c,repeat:false,preventDefault(){},stopImmediatePropagation(){stop=true;}};for(const f of (H.keydown||[])){f(e);if(stop)break;}},out:o=>console.log(JSON.stringify(o))};vm.createContext(ctx);
vm.runInContext(fs.readFileSync(file,'utf8'),ctx);vm.runInContext(after,ctx);
""";
}
