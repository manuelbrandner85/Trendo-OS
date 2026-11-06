using System.Text.Json.Nodes;
using Trendo.Core;
using Trendo.Core.Programs;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Paket 01 / S1-05 (Teil 2): Inventar mit Gegenständen und sprechende Figuren, die sich etwas wünschen und belohnen – Probespiel und Browser-Spiel gleich.</summary>
public class GameInventoryTests
{
    static readonly string[] Lv = ["..............", "S.G..N.......Z", "##############"];
    static readonly Dictionary<string, string> Tx = new() { ["2,1:name"] = "Apfel", ["5,1:say"] = "Ich habe Hunger!", ["5,1:need"] = "Apfel", ["5,1:thanks"] = "Lecker, danke!", ["5,1:give"] = "leben" };

    static PlatformSim Walk(PlatformSim s, Func<PlatformSim, bool> until) { for (var i = 0; i < 400 && !s.Over && !until(s); i++) s.Step(false, true, false); Assert.True(until(s), "Ziel im Test nicht erreicht"); return s; }
    static int Col(PlatformSim s) => (int)((s.Player.X + 12) / PlatformSim.T);

    [Fact(DisplayName = "Inventar S1-05: Gegenstand kommt ins Inventar; Figur nimmt den gewünschten Gegenstand und belohnt (+1 Leben, Dank); Regel „Gegenstände gesammelt“ zählt")]
    public void Give_item()
    {
        var s = new PlatformSim(Lv, lives: 3, texts: Tx, rules: [new("gegenstaende", 1, "punkte", "Finder")]);
        Walk(s, x => x.Inventory.Count > 0); // über den Apfel
        Assert.Equal(["Apfel"], s.Inventory); Assert.Equal(1, s.ItemsCollected);
        Assert.True(s.Quests.Single().Done);
        Walk(s, x => x.Lives == 4); // zur Figur
        Assert.Empty(s.Inventory); Assert.Equal(4, s.Lives);
        Assert.Equal("Lecker, danke!", s.Note);
    }

    [Fact(DisplayName = "Inventar S1-05: ohne Gegenstand sagt die Figur ihren Text und belohnt nicht; ohne Texte gelten Kristall/Hallo")]
    public void Without_item()
    {
        var noApple = new PlatformSim(["S....N.......Z", "##############"], lives: 3, texts: new Dictionary<string, string> { ["5,0:say"] = "Ich habe Hunger!", ["5,0:need"] = "Apfel" });
        Walk(noApple, x => Col(x) == 5);
        Assert.Equal("Ich habe Hunger!", noApple.Note); Assert.Equal(3, noApple.Lives);
        var plain = Walk(new PlatformSim(["S.G..N.......Z", "##############"]), x => Col(x) == 5);
        Assert.Equal(["Kristall"], plain.Inventory); Assert.Equal("Hallo!", plain.Note);
    }

    [Fact(DisplayName = "Inventar S1-05: Inventar steckt im Spielstand und kommt beim Fortsetzen zurück")]
    public void Save_inventory()
    {
        var s = Walk(new PlatformSim(Lv, texts: Tx), x => x.Inventory.Count > 0);
        var json = GameSaves.ToJson(s.Snapshot());
        var back = new PlatformSim(Lv, texts: Tx); back.Restore(GameSaves.Parse(json, 1));
        Assert.Equal(["Apfel"], back.Inventory);
    }

    [Fact(DisplayName = "Inventar S1-05: G und N sind erlaubte Kacheln, Texte landen im Spiel, unbekannter Text-Schlüssel abgelehnt, lange Texte gekürzt")]
    public void Validate()
    {
        var g = GameStudio.Validate(new GameStudio.GameSettings { Level = Lv, Texts = new(Tx) { ["2,1:name"] = new string('x', 300) } });
        Assert.Equal(200, g.Texts["2,1:name"].Length);
        Assert.Contains("\"5,1:need\":\"Apfel\"", GameStudio.ConfigJs(g));
        Assert.Throws<ArgumentException>(() => GameStudio.Validate(new GameStudio.GameSettings { Level = Lv, Texts = new() { ["2,1:farbe"] = "rot" } }));
        Assert.Equal("Apfel", GameDesign.Texts(new JsonObject { ["texts"] = new JsonObject { ["2,1:name"] = "Apfel", ["3,1:name"] = "" } }).Single().Value);
    }

    [Fact(DisplayName = "Inventar S1-05 Browser-Spiel (echt mit Node): Apfel einsammeln, der Figur geben, +1 Leben; Inventar im Spielstand")]
    public void Browser_js()
    {
        if (Cli.Find("node") is not { } node) return;
        var dir = Path.Combine(Path.GetTempPath(), "trendo-inv-js-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "spiel.js"), GameStudio.ConfigJs(new GameStudio.GameSettings { Level = Lv, Lives = 3, Texts = Tx }) + GameStudio.GameJs("plattform"));
            File.WriteAllText(Path.Combine(dir, "lauf.js"), Harness);
            string Run(string after) { var r = Cli.Run(node, [Path.Combine(dir, "lauf.js"), Path.Combine(dir, "spiel.js"), after], dir, 30000); Assert.True(r.Ok, r.Err + r.Out); return r.Out.Trim().Split('\n')[^1]; }
            var mid = JsonNode.Parse(Run("k.ArrowRight=1;for(let i=0;i<300&&!inv.length;i++)step();out({inv,gc});"))!;
            Assert.Equal("Apfel", mid["inv"]![0]!.GetValue<string>()); Assert.Equal(1, mid["gc"]!.GetValue<int>());
            var end = JsonNode.Parse(Run("k.ArrowRight=1;for(let i=0;i<300&&lives<4;i++)step();out({inv,lives,note});"))!;
            Assert.Empty(end["inv"]!.AsArray()); Assert.Equal(4, end["lives"]!.GetValue<int>()); Assert.Equal("Lecker, danke!", end["note"]!.GetValue<string>());
            var saved = JsonNode.Parse(Run("k.ArrowRight=1;for(let i=0;i<300&&!inv.length;i++)step();saveGame();out(JSON.parse(localStorage.getItem(SK)));"))!;
            Assert.Equal("Apfel", saved["flags"]!["inventar"]!.GetValue<string>());
        }
        finally { Directory.Delete(dir, true); }
    }

    const string Harness = """
const fs=require('fs'),vm=require('vm');const [,,file,after]=process.argv;const store={};
const noop=new Proxy(function(){},{get:(t,k)=>k==='measureText'?(()=>({width:0})):noop,apply:()=>undefined});
const canvas={width:480,height:360,getContext:()=>noop,addEventListener(){}};const status={textContent:''};
const ctx={document:{title:'Test',getElementById:id=>id==='c'?canvas:status},localStorage:{getItem:k=>k in store?store[k]:null,setItem:(k,v)=>{store[k]=String(v);},removeItem:k=>{delete store[k];}},
requestAnimationFrame(){},addEventListener(){},Image:function(){},Audio:function(){},AudioContext:function(){},Math,JSON,Number,Object,Date,console,String,
out:o=>console.log(JSON.stringify(o))};vm.createContext(ctx);
vm.runInContext(fs.readFileSync(file,'utf8'),ctx);vm.runInContext(after,ctx);
""";
}
