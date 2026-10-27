using System.Text;
using System.Text.Json;

namespace Trendo.Core;

/// <summary>CORE-06: Spiele-Studio – spielbare Browser-Spiele aus Vorlagen; Unreal-Projekte anlegen und installierte Engines erkennen.</summary>
public static class GameStudio
{
    public static readonly (string Id, string Label, string Help)[] Templates =
    [
        ("snake", "Schlange", "Pfeiltasten. Friss die Punkte, werde länger."),
        ("breakout", "Mauerbrecher", "Maus oder Pfeiltasten. Zerstöre alle Steine."),
        ("jump", "Hüpfspiel", "Leertaste zum Springen. Weiche den Hindernissen aus."),
    ];

    /// <summary>T-0204: Spielregeln und Aussehen. Wird als config.js neben das Spiel geschrieben; game.js liest nur diese Werte.</summary>
    public sealed record GameSettings
    {
        public int Speed { get; init; } = 5;          // 1 (langsam) … 10 (schnell)
        public int Lives { get; init; } = 3;          // 1 … 9
        public int WinScore { get; init; } = 20;      // Punkte zum Sieg (0 = endlos)
        public string Player { get; init; } = "#4FB3FF";
        public string Accent { get; init; } = "#FACC15";
        public string Background { get; init; } = "#10203F";
        public bool Sound { get; init; } = true;
        /// <summary>Level als Raster: „#“ = Stein (Mauerbrecher) bzw. Hindernis (Hüpfspiel), „.“ = frei. Leer = Standardlevel.</summary>
        public string[] Level { get; init; } = [];
        public string? PlayerImage { get; init; }     // Dateiname im Spielordner
        public string? BackgroundImage { get; init; } // Dateiname im Spielordner
    }

    public static readonly string[] DefaultBreakout = ["########", "########", "########", "########", "########"];
    public static readonly string[] DefaultJump = ["...#....#...##.....#..#...#.....##...."];

    static bool Hex(string s) => System.Text.RegularExpressions.Regex.IsMatch(s, "^#[0-9A-Fa-f]{6}$");

    public static GameSettings Validate(GameSettings s)
    {
        if (!Hex(s.Player) || !Hex(s.Accent) || !Hex(s.Background)) throw new ArgumentException("Farben bitte als # und sechs Zeichen, zum Beispiel #4FB3FF.");
        if (s.Level.Any(r => r.Any(ch => ch is not ('#' or '.')))) throw new ArgumentException("Im Level nur # (Stein/Hindernis) und . (frei).");
        if (s.Level.Length > 12 || s.Level.Any(r => r.Length > 60)) throw new ArgumentException("Level höchstens 12 Zeilen und 60 Spalten.");
        return s with { Speed = Math.Clamp(s.Speed, 1, 10), Lives = Math.Clamp(s.Lives, 1, 9), WinScore = Math.Clamp(s.WinScore, 0, 9999) };
    }

    public static string ConfigJs(GameSettings s)
    {
        var o = new { speed = s.Speed, lives = s.Lives, win = s.WinScore, player = s.Player, accent = s.Accent, bg = s.Background, sound = s.Sound,
                      level = s.Level, playerImage = s.PlayerImage, bgImage = s.BackgroundImage };
        return "const CFG=" + JsonSerializer.Serialize(o) + ";\n";
    }

    public static GameSettings ReadSettings(string dir)
    {
        var f = Path.Combine(dir, "spiel.json");
        try { return File.Exists(f) ? JsonSerializer.Deserialize<GameSettings>(File.ReadAllText(f)) ?? new() : new(); }
        catch (JsonException) { return new(); }
    }

    public static string TemplateOf(string dir) => File.Exists(Path.Combine(dir, "vorlage.txt")) ? File.ReadAllText(Path.Combine(dir, "vorlage.txt")).Trim() : "";

    /// <summary>Schreibt Regeln/Aussehen ins Spiel. Bilder werden in den Spielordner kopiert, damit das Spiel weitergegeben werden kann.</summary>
    public static GameSettings Save(string dir, GameSettings s, string? playerImageFile = null, string? backgroundImageFile = null)
    {
        static string Copy(string dir, string src, string name)
        {
            var ext = Path.GetExtension(src).ToLowerInvariant();
            if (ext is not (".png" or ".jpg" or ".jpeg" or ".gif" or ".webp")) throw new ArgumentException("Bitte ein Bild (PNG, JPG, GIF oder WebP).");
            if (!File.Exists(src)) throw new FileNotFoundException("Bild nicht gefunden: " + Path.GetFileName(src));
            var dst = name + ext; File.Copy(src, Path.Combine(dir, dst), true); return dst;
        }
        if (!string.IsNullOrWhiteSpace(playerImageFile)) s = s with { PlayerImage = Copy(dir, playerImageFile, "spielfigur") };
        if (!string.IsNullOrWhiteSpace(backgroundImageFile)) s = s with { BackgroundImage = Copy(dir, backgroundImageFile, "hintergrund") };
        s = Validate(s);
        File.WriteAllText(Path.Combine(dir, "spiel.json"), JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(dir, "config.js"), ConfigJs(s), new UTF8Encoding(false));
        return s;
    }

    /// <summary>Packt das Spiel als ZIP zum Weitergeben (index.html im ZIP öffnen genügt).</summary>
    public static string Export(string dir, string targetFolder)
    {
        Directory.CreateDirectory(targetFolder);
        var zip = Path.Combine(targetFolder, Path.GetFileName(dir) + ".zip");
        for (var i = 2; File.Exists(zip); i++) zip = Path.Combine(targetFolder, $"{Path.GetFileName(dir)}-{i}.zip");
        System.IO.Compression.ZipFile.CreateFromDirectory(dir, zip);
        return zip;
    }

    const string Shell = """
<!doctype html><html lang="de"><head><meta charset="utf-8"><title>{{TITLE}}</title>
<style>html,body{margin:0;background:#0b1730;color:#eef;font-family:system-ui,sans-serif;text-align:center}canvas{border-radius:12px;margin-top:16px}</style></head>
<body><h2>{{TITLE}}</h2><p>{{HELP}}</p><canvas id="c" width="480" height="360"></canvas><p id="s">Punkte: 0</p>
<script src="config.js"></script><script src="game.js"></script></body></html>
""";

    // Gemeinsamer Kopf: Bilder laden, Hintergrund, Ton (WebAudio-Piep), Leben/Sieg
    const string Common = """
const c=document.getElementById('c'),x=c.getContext('2d');const img=n=>{if(!n)return null;const i=new Image();i.src=n;return i;};
const PI=img(CFG.playerImage),BI=img(CFG.bgImage);let A=null;
function beep(f){if(!CFG.sound)return;try{A=A||new AudioContext();const o=A.createOscillator(),g=A.createGain();o.frequency.value=f;g.gain.value=.05;o.connect(g);g.connect(A.destination);o.start();o.stop(A.currentTime+.08);}catch(e){}}
function bg(){if(BI&&BI.complete&&BI.naturalWidth)x.drawImage(BI,0,0,c.width,c.height);else{x.fillStyle=CFG.bg;x.fillRect(0,0,c.width,c.height);}}
function me(px,py,w,h){if(PI&&PI.complete&&PI.naturalWidth)x.drawImage(PI,px,py,w,h);else{x.fillStyle=CFG.player;x.fillRect(px,py,w,h);}}
let lives=CFG.lives,over=false;function status(p){document.getElementById('s').textContent=over?(lives>0?'Gewonnen! Punkte: '+p:'Vorbei – Punkte: '+p+' (Leertaste: neu)'):'Punkte: '+p+' · Leben: '+lives+(CFG.win?' · Ziel: '+CFG.win:'');}
function won(p){return CFG.win>0&&p>=CFG.win;}

""";

    static readonly Dictionary<string, string> Js = new()
    {
        ["snake"] = """
const G=20,W=c.width/G,H=c.height/G;let s=[{x:5,y:5}],d={x:1,y:0},f={x:10,y:8},p=0;
addEventListener('keydown',e=>{if(over&&e.code==='Space'){lives=CFG.lives;over=false;p=0;s=[{x:5,y:5}];}const m={ArrowUp:[0,-1],ArrowDown:[0,1],ArrowLeft:[-1,0],ArrowRight:[1,0]}[e.key];if(m&&(m[0]!==-d.x||m[1]!==-d.y))d={x:m[0],y:m[1]};});
function step(){if(!over){const h={x:(s[0].x+d.x+W)%W,y:(s[0].y+d.y+H)%H};if(s.some(q=>q.x===h.x&&q.y===h.y)){lives--;beep(160);s=[{x:5,y:5}];if(lives<=0)over=true;}else{s.unshift(h);
if(h.x===f.x&&h.y===f.y){p++;beep(880);f={x:Math.floor(Math.random()*W),y:Math.floor(Math.random()*H)};if(won(p))over=true;}else s.pop();}}
bg();x.fillStyle=CFG.accent;x.fillRect(f.x*G,f.y*G,G-2,G-2);s.forEach((q,i)=>{if(i===0)me(q.x*G,q.y*G,G-2,G-2);else{x.fillStyle=CFG.player;x.fillRect(q.x*G,q.y*G,G-2,G-2);}});status(p);}
setInterval(step,Math.round(220-CFG.speed*16));
""",
        ["breakout"] = """
const L=(CFG.level&&CFG.level.length?CFG.level:['########','########','########','########','########']);const C=Math.max(...L.map(r=>r.length)),bw=Math.floor((c.width-16)/C)-2,bh=16;
let px=200,v=1.5+CFG.speed*.45,b={x:240,y:300,dx:v,dy:-v},p=0,k={},br=[];
L.forEach((r,ri)=>[...r].forEach((ch,i)=>{if(ch==='#')br.push({x:8+i*(bw+2),y:30+ri*(bh+4),a:1});}));
addEventListener('keydown',e=>{k[e.key]=1;if(over&&e.code==='Space'){lives=CFG.lives;over=false;p=0;br.forEach(q=>q.a=1);}});addEventListener('keyup',e=>k[e.key]=0);c.addEventListener('mousemove',e=>px=e.offsetX-40);
function step(){if(!over){if(k.ArrowLeft)px-=6;if(k.ArrowRight)px+=6;px=Math.max(0,Math.min(c.width-80,px));b.x+=b.dx;b.y+=b.dy;
if(b.x<6||b.x>c.width-6)b.dx*=-1;if(b.y<6)b.dy*=-1;if(b.y>c.height-20&&b.x>px&&b.x<px+80){b.dy=-Math.abs(b.dy);beep(440);}
if(b.y>c.height){lives--;beep(160);b={x:240,y:300,dx:v,dy:-v};if(lives<=0)over=true;}
br.forEach(q=>{if(q.a&&b.x>q.x&&b.x<q.x+bw&&b.y>q.y&&b.y<q.y+bh){q.a=0;b.dy*=-1;p++;beep(880);}});if(br.every(q=>!q.a)||won(p))over=true;}
bg();me(px,c.height-14,80,8);x.fillStyle=CFG.accent;x.beginPath();x.arc(b.x,b.y,6,0,7);x.fill();x.fillStyle='#7C4DFF';br.forEach(q=>{if(q.a)x.fillRect(q.x,q.y,bw,bh);});status(p);requestAnimationFrame(step);}
step();
""",
        ["jump"] = """
const L=(CFG.level&&CFG.level.length?CFG.level[0]:'...#....#...##.....#..#...#.....##....');let y=300,vy=0,o=[],t=0,p=0,i=0;const sp=2+CFG.speed*.6;
addEventListener('keydown',e=>{if(e.code==='Space'){if(over){lives=CFG.lives;over=false;p=0;o=[];}else if(y>=300){vy=-11;beep(660);}}});
function step(){if(!over){t++;vy+=0.6;y=Math.min(300,y+vy);if(t%Math.round(120/sp)===0){if(L[i%L.length]==='#')o.push({x:c.width});i++;}o.forEach(q=>q.x-=sp);o=o.filter(q=>q.x>-30);
if(o.some(q=>q.x<70&&q.x>30&&y>270)){o=[];lives--;beep(160);if(lives<=0)over=true;}if(t%10===0){p++;if(won(p))over=true;}}
bg();me(40,y,30,30);x.fillStyle=CFG.accent;o.forEach(q=>x.fillRect(q.x,300,20,30));x.fillStyle='#334';x.fillRect(0,330,c.width,4);status(p);requestAnimationFrame(step);}
step();
""",
    };

    public static string CreateBrowserGame(string parent, string template, string name, GameSettings? settings = null)
    {
        var t = Templates.FirstOrDefault(x => x.Id == template);
        if (t.Id is null) throw new ArgumentException("Unbekannte Vorlage");
        var dir = Path.Combine(parent, DevStudio.SafeName(name));
        if (Directory.Exists(dir)) throw new IOException($"Den Ordner „{Path.GetFileName(dir)}“ gibt es schon.");
        Directory.CreateDirectory(dir);
        var enc = System.Net.WebUtility.HtmlEncode(name);
        File.WriteAllText(Path.Combine(dir, "index.html"), Shell.Replace("{{TITLE}}", enc).Replace("{{HELP}}", t.Help), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(dir, "game.js"), Common + Js[t.Id].TrimStart(), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(dir, "vorlage.txt"), t.Id);
        Save(dir, settings ?? new GameSettings { Level = t.Id == "breakout" ? DefaultBreakout : t.Id == "jump" ? DefaultJump : [] });
        File.WriteAllText(Path.Combine(dir, "README.md"), $"# {name}\n\nVorlage „{t.Label}“. Öffne index.html im Browser. Regeln, Farben und Level stehen in config.js (einstellbar im Spiele-Studio).\n");
        return dir;
    }

    // ---------- Unreal ----------
    public sealed record Engine(string Version, string Path);

    /// <summary>Liest die Epic-Launcher-Liste (LauncherInstalled.dat) und liefert installierte Unreal-Engines.</summary>
    public static IReadOnlyList<Engine> ParseLauncherInstalled(string json)
    {
        var list = new List<Engine>();
        try
        {
            using var d = JsonDocument.Parse(json);
            if (!d.RootElement.TryGetProperty("InstallationList", out var arr)) return list;
            foreach (var e in arr.EnumerateArray())
            {
                var app = e.GetProperty("AppName").GetString() ?? ""; var loc = e.GetProperty("InstallLocation").GetString() ?? "";
                if (app.StartsWith("UE_", StringComparison.Ordinal)) list.Add(new(app[3..], loc));
            }
        }
        catch (JsonException) { }
        catch (KeyNotFoundException) { }
        return list.OrderByDescending(e => Version.TryParse(e.Version, out var v) ? v : new Version(0, 0)).ToList();
    }

    public static IReadOnlyList<Engine> FindEngines()
    {
        var f = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "UnrealEngineLauncher", "LauncherInstalled.dat");
        return File.Exists(f) ? ParseLauncherInstalled(File.ReadAllText(f)) : [];
    }

    public static string EditorExe(Engine e) => System.IO.Path.Combine(e.Path, "Engine", "Binaries", "Win64", "UnrealEditor.exe");

    /// <summary>Legt ein leeres Unreal-Projekt (Blueprint, ohne C++) an, das der Editor öffnen kann.</summary>
    public static string CreateUnrealProject(string parent, string name, string engineVersion)
    {
        var safe = new string(DevStudio.SafeName(name).Where(char.IsLetterOrDigit).ToArray());
        if (safe.Length == 0 || char.IsDigit(safe[0])) safe = "Spiel" + safe;
        var dir = System.IO.Path.Combine(parent, safe);
        if (Directory.Exists(dir)) throw new IOException($"Den Ordner „{safe}“ gibt es schon.");
        Directory.CreateDirectory(System.IO.Path.Combine(dir, "Content")); Directory.CreateDirectory(System.IO.Path.Combine(dir, "Config"));
        var up = new { FileVersion = 3, EngineAssociation = engineVersion, Category = "", Description = name, Plugins = new[] { new { Name = "ModelingToolsEditorMode", Enabled = true, TargetAllowList = new[] { "Editor" } } } };
        File.WriteAllText(System.IO.Path.Combine(dir, safe + ".uproject"), JsonSerializer.Serialize(up, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(System.IO.Path.Combine(dir, "Config", "DefaultEngine.ini"), "[/Script/EngineSettings.GameMapsSettings]\nGameDefaultMap=/Engine/Maps/Templates/OpenWorld\n\n[/Script/Engine.RendererSettings]\nr.DefaultFeature.AutoExposure=False\n");
        File.WriteAllText(System.IO.Path.Combine(dir, "Config", "DefaultGame.ini"), $"[/Script/EngineSettings.GeneralProjectSettings]\nProjectName={safe}\n");
        return dir;
    }
}
