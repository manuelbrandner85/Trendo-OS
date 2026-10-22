using System.Text;
using System.Text.Json;

namespace Trendo.Core;

/// <summary>CORE-06: Spiele-Studio – spielbare Browser-Spiele aus Vorlagen; Unreal-Projekte anlegen und installierte Engines erkennen.</summary>
public static partial class GameStudio
{
    public static readonly (string Id, string Label, string Help)[] Templates =
    [
        ("snake", "Schlange", "Pfeiltasten. Friss die Punkte, werde länger."),
        ("breakout", "Mauerbrecher", "Maus oder Pfeiltasten. Zerstöre alle Steine."),
        ("jump", "Hüpfspiel", "Leertaste zum Springen. Weiche den Hindernissen aus."),
    ];

    const string Shell = """
<!doctype html><html lang="de"><head><meta charset="utf-8"><title>{{TITLE}}</title>
<style>html,body{margin:0;background:#0b1730;color:#eef;font-family:system-ui,sans-serif;text-align:center}canvas{background:#10203f;border-radius:12px;margin-top:16px}</style></head>
<body><h2>{{TITLE}}</h2><p>{{HELP}}</p><canvas id="c" width="480" height="360"></canvas><p id="s">Punkte: 0</p>
<script src="game.js"></script></body></html>
""";

    static readonly Dictionary<string, string> Js = new()
    {
        ["snake"] = """
const c=document.getElementById('c'),x=c.getContext('2d'),G=20,W=c.width/G,H=c.height/G;let s=[{x:5,y:5}],d={x:1,y:0},f={x:10,y:8},p=0;
addEventListener('keydown',e=>{const m={ArrowUp:[0,-1],ArrowDown:[0,1],ArrowLeft:[-1,0],ArrowRight:[1,0]}[e.key];if(m&&(m[0]!==-d.x||m[1]!==-d.y))d={x:m[0],y:m[1]};});
function step(){const h={x:(s[0].x+d.x+W)%W,y:(s[0].y+d.y+H)%H};if(s.some(q=>q.x===h.x&&q.y===h.y)){s=[{x:5,y:5}];p=0;}s.unshift(h);
if(h.x===f.x&&h.y===f.y){p++;f={x:Math.floor(Math.random()*W),y:Math.floor(Math.random()*H)};}else s.pop();
x.fillStyle='#10203f';x.fillRect(0,0,c.width,c.height);x.fillStyle='#FACC15';x.fillRect(f.x*G,f.y*G,G-2,G-2);x.fillStyle='#4FB3FF';s.forEach(q=>x.fillRect(q.x*G,q.y*G,G-2,G-2));
document.getElementById('s').textContent='Punkte: '+p;}
setInterval(step,110);
""",
        ["breakout"] = """
const c=document.getElementById('c'),x=c.getContext('2d');let px=200,b={x:240,y:300,dx:3,dy:-3},p=0,k={};const R=5,C=8,bw=56,bh=16;let br=[];
for(let r=0;r<R;r++)for(let i=0;i<C;i++)br.push({x:8+i*(bw+2),y:30+r*(bh+4),a:1});
addEventListener('keydown',e=>k[e.key]=1);addEventListener('keyup',e=>k[e.key]=0);c.addEventListener('mousemove',e=>px=e.offsetX-40);
function step(){if(k.ArrowLeft)px-=6;if(k.ArrowRight)px+=6;px=Math.max(0,Math.min(c.width-80,px));b.x+=b.dx;b.y+=b.dy;
if(b.x<6||b.x>c.width-6)b.dx*=-1;if(b.y<6)b.dy*=-1;if(b.y>c.height-20&&b.x>px&&b.x<px+80)b.dy=-Math.abs(b.dy);
if(b.y>c.height){b={x:240,y:300,dx:3,dy:-3};p=0;br.forEach(q=>q.a=1);}
br.forEach(q=>{if(q.a&&b.x>q.x&&b.x<q.x+bw&&b.y>q.y&&b.y<q.y+bh){q.a=0;b.dy*=-1;p++;}});
x.fillStyle='#10203f';x.fillRect(0,0,c.width,c.height);x.fillStyle='#4FB3FF';x.fillRect(px,c.height-14,80,8);
x.fillStyle='#FACC15';x.beginPath();x.arc(b.x,b.y,6,0,7);x.fill();x.fillStyle='#7C4DFF';br.forEach(q=>q.a&&x.fillRect(q.x,q.y,bw,bh));
document.getElementById('s').textContent=br.every(q=>!q.a)?'Gewonnen! Punkte: '+p:'Punkte: '+p;requestAnimationFrame(step);}
step();
""",
        ["jump"] = """
const c=document.getElementById('c'),x=c.getContext('2d');let y=300,v=0,o=[],t=0,p=0;
addEventListener('keydown',e=>{if(e.code==='Space'&&y>=300)v=-11;});
function step(){t++;v+=0.6;y=Math.min(300,y+v);if(t%90===0)o.push({x:c.width});o.forEach(q=>q.x-=4);o=o.filter(q=>q.x>-30);
if(o.some(q=>q.x<70&&q.x>30&&y>270)){o=[];p=0;}if(t%10===0)p++;
x.fillStyle='#10203f';x.fillRect(0,0,c.width,c.height);x.fillStyle='#4FB3FF';x.fillRect(40,y,30,30);x.fillStyle='#FB8C00';o.forEach(q=>x.fillRect(q.x,300,20,30));
x.fillStyle='#334';x.fillRect(0,330,c.width,4);document.getElementById('s').textContent='Punkte: '+p;requestAnimationFrame(step);}
step();
""",
    };

    public static string CreateBrowserGame(string parent, string template, string name)
    {
        var t = Templates.FirstOrDefault(x => x.Id == template);
        if (t.Id is null) throw new ArgumentException("Unbekannte Vorlage");
        var dir = Path.Combine(parent, DevStudio.SafeName(name));
        if (Directory.Exists(dir)) throw new IOException($"Den Ordner „{Path.GetFileName(dir)}“ gibt es schon.");
        Directory.CreateDirectory(dir);
        var enc = System.Net.WebUtility.HtmlEncode(name);
        File.WriteAllText(Path.Combine(dir, "index.html"), Shell.Replace("{{TITLE}}", enc).Replace("{{HELP}}", t.Help), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(dir, "game.js"), Js[t.Id].TrimStart(), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(dir, "README.md"), $"# {name}\n\nVorlage „{t.Label}“ aus Trendo OS. Öffne index.html im Browser. Ändere game.js, um das Spiel anzupassen.\n");
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

    public static string DefaultLauncherFile() => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "UnrealEngineLauncher", "LauncherInstalled.dat");

    /// <summary>Übliche Installationsordner auf dem Windows-PC: „Programme\Epic Games“.</summary>
    public static IEnumerable<string> DefaultEpicRoots()
    {
        foreach (var spec in new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 })
        {
            var pf = Environment.GetFolderPath(spec);
            if (!string.IsNullOrEmpty(pf)) yield return System.IO.Path.Combine(pf, "Epic Games");
        }
    }

    /// <summary>Launcher-Liste und die Ordner unter Programme\Epic Games. Neueste Fassung zuerst.</summary>
    public static IReadOnlyList<Engine> FindEngines() => FindEngines(DefaultLauncherFile(), DefaultEpicRoots());

    public static IReadOnlyList<Engine> FindEngines(string? launcherFile, IEnumerable<string> epicRoots)
    {
        var all = new List<Engine>();
        if (!string.IsNullOrEmpty(launcherFile) && File.Exists(launcherFile))
            all.AddRange(ParseLauncherInstalled(File.ReadAllText(launcherFile)));
        foreach (var root in epicRoots) all.AddRange(ScanEpicFolder(root));
        return Merge(all);
    }

    /// <summary>Liest UE_*-Ordner, in denen der Editor liegt.</summary>
    public static IReadOnlyList<Engine> ScanEpicFolder(string epicRoot)
    {
        if (!Directory.Exists(epicRoot)) return [];
        var list = new List<Engine>();
        IEnumerable<string> dirs;
        try { dirs = Directory.EnumerateDirectories(epicRoot); }
        catch (IOException) { return []; }
        catch (UnauthorizedAccessException) { return []; }
        foreach (var dir in dirs)
        {
            var name = System.IO.Path.GetFileName(dir);
            if (!name.StartsWith("UE_", StringComparison.OrdinalIgnoreCase)) continue;
            var editor = System.IO.Path.Combine(dir, "Engine", "Binaries", "Win64", "UnrealEditor.exe");
            var cmd = System.IO.Path.Combine(dir, "Engine", "Binaries", "Win64", "UnrealEditor-Cmd.exe");
            if (!File.Exists(editor) && !File.Exists(cmd)) continue;
            list.Add(new(name[3..], dir));
        }
        return Merge(list);
    }

    public static IReadOnlyList<Engine> Merge(IEnumerable<Engine> engines)
    {
        var cmp = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var best = new Dictionary<string, Engine>(cmp);
        foreach (var e in engines)
        {
            if (string.IsNullOrWhiteSpace(e.Path) || string.IsNullOrWhiteSpace(e.Version)) continue;
            var key = System.IO.Path.GetFullPath(e.Path).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
            if (!best.TryGetValue(key, out var old) || Ver(e.Version) > Ver(old.Version)) best[key] = e;
        }
        return best.Values.OrderByDescending(e => Ver(e.Version)).ThenBy(e => e.Version, StringComparer.Ordinal).ToList();
    }

    static Version Ver(string s) => Version.TryParse(s, out var v) ? v : new Version(0, 0);

    public static string EditorExe(Engine e) => System.IO.Path.Combine(e.Path, "Engine", "Binaries", "Win64", "UnrealEditor.exe");

    public static string EditorCmdExe(Engine e) => System.IO.Path.Combine(e.Path, "Engine", "Binaries", "Win64", "UnrealEditor-Cmd.exe");

    /// <summary>Legt ein leeres Unreal-Projekt (Blueprint, ohne C++) an, das der Editor öffnen kann.</summary>
    public static string CreateUnrealProject(string parent, string name, string engineVersion)
    {
        var safe = new string(DevStudio.SafeName(name).Where(char.IsLetterOrDigit).ToArray());
        if (safe.Length == 0 || char.IsDigit(safe[0])) safe = "Spiel" + safe;
        var dir = System.IO.Path.Combine(parent, safe);
        if (Directory.Exists(dir)) throw new IOException($"Den Ordner „{safe}“ gibt es schon.");
        Directory.CreateDirectory(System.IO.Path.Combine(dir, "Content")); Directory.CreateDirectory(System.IO.Path.Combine(dir, "Config"));
        var up = new
        {
            FileVersion = 3, EngineAssociation = engineVersion, Category = "", Description = name,
            Plugins = new object[]
            {
                new { Name = "ModelingToolsEditorMode", Enabled = true, TargetAllowList = new[] { "Editor" } },
                new { Name = "PythonScriptPlugin", Enabled = true },
            },
        };
        File.WriteAllText(System.IO.Path.Combine(dir, safe + ".uproject"), JsonSerializer.Serialize(up, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(System.IO.Path.Combine(dir, "Config", "DefaultEngine.ini"), "[/Script/EngineSettings.GameMapsSettings]\nGameDefaultMap=/Engine/Maps/Templates/OpenWorld\n\n[/Script/Engine.RendererSettings]\nr.DefaultFeature.AutoExposure=False\n");
        File.WriteAllText(System.IO.Path.Combine(dir, "Config", "DefaultGame.ini"), $"[/Script/EngineSettings.GeneralProjectSettings]\nProjectName={safe}\n");
        return dir;
    }
}
