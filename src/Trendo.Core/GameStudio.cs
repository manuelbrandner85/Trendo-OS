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
        ("plattform", "Plattformer", "Pfeiltasten laufen, Leertaste springt. Sammle Münzen, weiche Gegnern und Stacheln aus, erreiche die Flagge."),
    ];

    /// <summary>Kacheln des Level-Editors: # Boden/Wand, o Münze, E Gegner (läuft hin und her), ^ Stacheln, S Start, Z Ziel-Flagge, . frei;
    /// seit 04.11.2026 wie in GDevelop/Construct auch F Sprungfeder, C Speicherpunkt, K Schlüssel, D Tür (öffnet, wenn alle Schlüssel gesammelt sind).</summary>
    /// <summary>Seit 06.11.2026 (Paket 01 S1-05): G Gegenstand (kommt ins Inventar), N sprechende Figur (Text, wünscht sich ggf. einen Gegenstand und belohnt).</summary>
    public const string Tiles = "#.oE^SZFCKDGNV";
    /// <summary>Fahrzeuge (Kachel V, Art im Objekt-Text „kind“): Wagen 1,8-fach schnell und fängt einen Treffer ab; Ufo fliegt mit gehaltener Sprungtaste, 10 s Treibstoff.</summary>
    public static readonly (string Id, string Label)[] Vehicles = [("wagen", "Wagen"), ("ufo", "Ufo")];
    /// <summary>Hintergrund-Ebene (05.11.2026, wie die Ebenen in Godot-TileMaps/GDevelop): nur Deko, ohne Wirkung aufs Spiel –
    /// w Wolke, b Busch, t Baum, m Berg, g Gras, . frei.</summary>
    public const string DecoTiles = "wbtmg.";

    /// <summary>Animation eines Zustands (stehen, laufen, springen, fallen): Bildstreifen mit <see cref="Frames"/> gleich breiten Bildern, <see cref="Fps"/> Bilder je Sekunde.</summary>
    public sealed record Anim(string Image, int Frames = 1, int Fps = 8);

    /// <summary>Ereignis-Regel wie die Ereignisblätter in GDevelop/Construct: Wenn <see cref="When"/> mindestens <see cref="N"/> erreicht, dann <see cref="Do"/> (einmal).</summary>
    public sealed record GameRule(string When, int N, string Do, string? Title = null);
    /// <summary>Fähigkeiten der Spielfigur (Paket 01 S1-05): Doppelsprung (in der Luft noch einmal springen) und Sprint (Umschalt-Taste, 1,6-fach schneller).</summary>
    public static readonly (string Id, string Label)[] Abilities = [("doppelsprung", "Doppelsprung"), ("sprint", "Sprint (Umschalt)")];
    /// <summary>Waffe der Spielfigur (Paket 01 S1-05): Angriff mit X oder J – Schwert trifft vor der Figur, Werfen schickt ein Geschoss.</summary>
    public static readonly (string Id, string Label)[] Weapons = [("", "keine"), ("schwert", "Schwert (Nahkampf)"), ("werfen", "Werfen (Geschoss)")];
    /// <summary>Aufgabe mit Fortschritt, z. B. „Schatzsucher: Münzen 3/10“ bzw. „✓ Schatzsucher“.</summary>
    public static string QuestText(GameRule r, double value, bool done) => done ? $"✓ {r.Title}" : $"{r.Title}: {RuleWhens.FirstOrDefault(w => w.Id == r.When).Label ?? r.When} {Math.Min((int)value, r.N)}/{r.N}";
    public static readonly (string Id, string Label)[] RuleWhens = [("muenzen", "Münzen gesammelt"), ("punkte", "Punkte"), ("gegner", "Gegner besiegt"), ("sekunden", "Sekunden gespielt"), ("gegenstaende", "Gegenstände gesammelt")];
    public static readonly (string Id, string Label)[] RuleDos = [("leben", "+1 Leben"), ("tueren", "Türen öffnen"), ("schneller", "Figur schneller"), ("punkte", "+10 Punkte"), ("sieg", "Gewonnen")];

    /// <summary>Regel als Satz, z. B. „Wenn Münzen gesammelt ≥ 5, dann +1 Leben“.</summary>
    public static string RuleText(GameRule r) => $"Wenn {RuleWhens.FirstOrDefault(w => w.Id == r.When).Label ?? r.When} ≥ {r.N}, dann {RuleDos.FirstOrDefault(x => x.Id == r.Do).Label ?? r.Do}";
    public static readonly string[] DefaultPlatform =
    [
        "............................................................",
        "............................................................",
        "............................................................",
        "............................................................",
        "...................ooo...............................o.o....",
        "..................#####............ooo..............#####...",
        "..........ooo.......................####....................",
        ".........#####.................................ooo.......Z..",
        "S.................E.........o.o.....E........#######.....#..",
        "#########..##########...#########..######...........#######.",
        "#########..##########^^^#########..######^^^^^^^^^^^########",
        "############################################################",
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
        /// <summary>Eigenschaften einzelner Objekte im Plattformer: „x,y:speed“ (Gegner-Tempo 0,25–4) und „x,y:value“ (Münzwert 1–100).</summary>
        public Dictionary<string, double> Props { get; init; } = [];
        /// <summary>Zeitlimit in Sekunden (0 = ohne) – läuft die Zeit ab, ist das Spiel vorbei.</summary>
        public int TimeLimit { get; init; }
        /// <summary>Ereignis-Regeln (höchstens 20).</summary>
        public List<GameRule> Rules { get; init; } = [];
        /// <summary>Weitere Level nach dem ersten (Ziel → nächstes Level).</summary>
        public List<string[]> MoreLevels { get; init; } = [];
        /// <summary>Hintergrund-Ebene je Level (gleiche Reihenfolge wie Level, darf fehlen).</summary>
        public List<string[]> Decor { get; init; } = [];
        /// <summary>Namen der Level (für die Anzeige im Spiel).</summary>
        public List<string> LevelNames { get; init; } = [];
        /// <summary>Animationen je Zustand der Spielfigur (Dateiname im Spielordner).</summary>
        public Dictionary<string, Anim> Anims { get; init; } = [];
        /// <summary>Ton-Spur: Geräusch je Ereignis (springen, muenze, treffer, ziel) und Musik („musik“, läuft in Schleife) – Dateiname im Spielordner.</summary>
        public Dictionary<string, string> Sounds { get; init; } = [];
        /// <summary>Spielstand automatisch speichern (Speicherpunkt, Level geschafft) und beim nächsten Start fortsetzen.</summary>
        public bool Saves { get; init; } = true;
        /// <summary>Freigeschaltete Fähigkeiten (siehe <see cref="Abilities"/>).</summary>
        public List<string> Abilities { get; init; } = [];
        /// <summary>Texte je Objekt (Vorsilbe ab Level 2 wie bei Props): „x,y:name“ (Gegenstand), „x,y:say“, „x,y:need“, „x,y:thanks“, „x,y:give“ (sprechende Figur).</summary>
        public Dictionary<string, string> Texts { get; init; } = [];
        /// <summary>Waffe (siehe <see cref="Weapons"/>), leer = keine.</summary>
        public string Weapon { get; init; } = "";
        /// <summary>Startmenü, Pause, Einstellungen, Karte und Controller (Paket 01 S1-14); aus = sofort spielen wie früher.</summary>
        public bool Menu { get; init; } = true;
    }

    /// <summary>Schlüssel der Objekt-Texte.</summary>
    public static readonly string[] TextKeys = ["name", "say", "need", "thanks", "give", "kind"];

    /// <summary>Ereignisse der Ton-Spur.</summary>
    public static readonly (string Id, string Label)[] SoundEvents = [("springen", "Springen"), ("muenze", "Münze"), ("treffer", "Treffer"), ("ziel", "Ziel"), ("musik", "Musik (Schleife)")];

    public static readonly string[] DefaultBreakout = ["########", "########", "########", "########", "########"];
    public static readonly string[] DefaultJump = ["...#....#...##.....#..#...#.....##...."];

    static bool Hex(string s) => System.Text.RegularExpressions.Regex.IsMatch(s, "^#[0-9A-Fa-f]{6}$");

    /// <summary>Hinweise zum Plattformer-Level: genau ein Start, mindestens ein Ziel, Boden unter dem Start.</summary>
    public static List<string> CheckPlatform(IReadOnlyList<string> level)
    {
        var w = new List<string>(); var all = string.Concat(level);
        var starts = all.Count(ch => ch == 'S');
        if (starts == 0) w.Add("Kein Start (S) im Level – die Figur weiß nicht, wo sie beginnt.");
        if (starts > 1) w.Add($"{starts} Startpunkte (S) – es zählt nur einer.");
        if (!all.Contains('Z')) w.Add("Kein Ziel (Z) im Level – man kann das Level nicht schaffen.");
        for (var y = 0; y < level.Count; y++) { var x = level[y].IndexOf('S'); if (x < 0) continue; if (!level.Skip(y + 1).Any(r => x < r.Length && r[x] == '#')) w.Add("Unter dem Start ist kein Boden – die Figur fällt sofort."); }
        return w;
    }

    public static GameSettings Validate(GameSettings s)
    {
        if (!Hex(s.Player) || !Hex(s.Accent) || !Hex(s.Background)) throw new ArgumentException("Farben bitte als # und sechs Zeichen, zum Beispiel #4FB3FF.");
        if (s.Level.Any(r => r.Any(ch => !Tiles.Contains(ch)))) throw new ArgumentException("Im Level nur # (Boden), . (frei), o (Münze), E (Gegner), ^ (Stacheln), S (Start), Z (Ziel), F (Sprungfeder), C (Speicherpunkt), K (Schlüssel), D (Tür), G (Gegenstand), N (sprechende Figur) und V (Fahrzeug).");
        if (s.Rules.Count > 20) throw new ArgumentException("Höchstens 20 Regeln.");
        if (s.Rules.FirstOrDefault(r => !RuleWhens.Any(w => w.Id == r.When) || !RuleDos.Any(x => x.Id == r.Do)) is { } bad) throw new ArgumentException($"Unbekannte Regel „{bad.When} → {bad.Do}“.");
        if (s.Abilities.FirstOrDefault(a => !Abilities.Any(x => x.Id == a)) is { } badAb) throw new ArgumentException($"Unbekannte Fähigkeit „{badAb}“.");
        if (s.Texts.Count > 400) throw new ArgumentException("Höchstens 400 Objekt-Texte.");
        if (!Weapons.Any(w => w.Id == s.Weapon)) throw new ArgumentException($"Unbekannte Waffe „{s.Weapon}“.");
        if (s.Texts.FirstOrDefault(kv => !TextKeys.Contains(kv.Key[(kv.Key.LastIndexOf(':') + 1)..])) is { Key: { } badKey }) throw new ArgumentException($"Unbekannter Objekt-Text „{badKey}“.");
        if (s.Level.Length > 16 || s.Level.Any(r => r.Length > 200)) throw new ArgumentException("Level höchstens 16 Zeilen und 200 Spalten.");
        if (s.MoreLevels.Count > 19) throw new ArgumentException("Höchstens 20 Level.");
        for (var i = 0; i < s.MoreLevels.Count; i++)
        {
            var l = s.MoreLevels[i];
            if (l.Any(r => r.Any(ch => !Tiles.Contains(ch)))) throw new ArgumentException($"Level {i + 2}: unbekanntes Zeichen.");
            if (l.Length > 16 || l.Any(r => r.Length > 200)) throw new ArgumentException($"Level {i + 2}: höchstens 16 Zeilen und 200 Spalten.");
        }
        if (s.Decor.Any(l => l.Any(r => r.Any(ch => !DecoTiles.Contains(ch))))) throw new ArgumentException("Im Hintergrund nur w (Wolke), b (Busch), t (Baum), m (Berg), g (Gras) und . (frei).");
        if (s.Sounds.Keys.FirstOrDefault(k => !SoundEvents.Any(e => e.Id == k)) is { } badSound) throw new ArgumentException($"Unbekanntes Ton-Ereignis „{badSound}“.");
        if (s.Anims.Keys.FirstOrDefault(k => !Programs.PlatformSim.States.Any(x => x.Id == k)) is { } badState) throw new ArgumentException($"Unbekannter Zustand „{badState}“.");
        return s with { Speed = Math.Clamp(s.Speed, 1, 10), Lives = Math.Clamp(s.Lives, 1, 9), WinScore = Math.Clamp(s.WinScore, 0, 9999), TimeLimit = Math.Clamp(s.TimeLimit, 0, 3600),
            Rules = [.. s.Rules.Select(r => r with { N = Math.Clamp(r.N, 0, 9999), Title = string.IsNullOrWhiteSpace(r.Title) ? null : r.Title.Trim().Length > 40 ? r.Title.Trim()[..40] : r.Title.Trim() })], Abilities = [.. s.Abilities.Distinct()], Texts = s.Texts.ToDictionary(kv => kv.Key, kv => kv.Value.Length > 200 ? kv.Value[..200] : kv.Value),
            Anims = s.Anims.ToDictionary(kv => kv.Key, kv => kv.Value with { Frames = Math.Clamp(kv.Value.Frames, 1, 32), Fps = Math.Clamp(kv.Value.Fps, 1, 30) }),
            Props = s.Props.Where(kv => kv.Key.EndsWith(":speed") || kv.Key.EndsWith(":value") || kv.Key.EndsWith(":hp")).ToDictionary(kv => kv.Key, kv => kv.Key.EndsWith(":speed") ? Math.Clamp(kv.Value, 0.25, 4) : kv.Key.EndsWith(":hp") ? Math.Clamp(Math.Round(kv.Value), 1, 9) : Math.Clamp(Math.Round(kv.Value), 1, 100)) };
    }

    public static string ConfigJs(GameSettings s)
    {
        var o = new { speed = s.Speed, lives = s.Lives, win = s.WinScore, player = s.Player, accent = s.Accent, bg = s.Background, sound = s.Sound,
                      level = s.Level, playerImage = s.PlayerImage, bgImage = s.BackgroundImage, props = s.Props, timeLimit = s.TimeLimit,
                      rules = s.Rules.Select(r => r.Title is null ? (object)new { when = r.When, n = r.N, act = r.Do } : new { when = r.When, n = r.N, act = r.Do, title = r.Title }), abilities = s.Abilities, texts = s.Texts, weapon = s.Weapon, menu = s.Menu, more = s.MoreLevels, decor = s.Decor, names = s.LevelNames,
                      anims = s.Anims.ToDictionary(kv => kv.Key, kv => new { img = kv.Value.Image, frames = kv.Value.Frames, fps = kv.Value.Fps }), sounds = s.Sounds, saves = s.Saves };
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
    public static GameSettings Save(string dir, GameSettings s, string? playerImageFile = null, string? backgroundImageFile = null, IReadOnlyDictionary<string, string>? animFiles = null, IReadOnlyDictionary<string, string>? soundFiles = null)
    {
        static string Copy(string dir, string src, string name)
        {
            var ext = Path.GetExtension(src).ToLowerInvariant();
            var sound = name.StartsWith("ton-", StringComparison.Ordinal);
            if (sound ? ext is not (".mp3" or ".ogg" or ".wav" or ".m4a") : ext is not (".png" or ".jpg" or ".jpeg" or ".gif" or ".webp"))
                throw new ArgumentException(sound ? "Bitte eine Tondatei (MP3, OGG, WAV oder M4A)." : "Bitte ein Bild (PNG, JPG, GIF oder WebP).");
            if (!File.Exists(src)) throw new FileNotFoundException((sound ? "Ton" : "Bild") + " nicht gefunden: " + Path.GetFileName(src));
            var dst = name + ext; File.Copy(src, Path.Combine(dir, dst), true); return dst;
        }
        if (!string.IsNullOrWhiteSpace(playerImageFile)) s = s with { PlayerImage = Copy(dir, playerImageFile, "spielfigur") };
        if (!string.IsNullOrWhiteSpace(backgroundImageFile)) s = s with { BackgroundImage = Copy(dir, backgroundImageFile, "hintergrund") };
        // Animationsbilder in den Spielordner (figur-laufen.png …), damit das Spiel weitergegeben werden kann
        if (animFiles is not null)
        {
            var anims = new Dictionary<string, Anim>(s.Anims);
            foreach (var (state, file) in animFiles.Where(kv => !string.IsNullOrWhiteSpace(kv.Value)))
                anims[state] = (anims.TryGetValue(state, out var a) ? a : new Anim("")) with { Image = Copy(dir, file, "figur-" + state) };
            s = s with { Anims = anims };
        }
        if (soundFiles is not null)
        {
            var snd = new Dictionary<string, string>(s.Sounds);
            foreach (var (ev, file) in soundFiles.Where(kv => !string.IsNullOrWhiteSpace(kv.Value))) snd[ev] = Copy(dir, file, "ton-" + ev);
            s = s with { Sounds = snd };
        }
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
<style>html,body{margin:0;background:#0b1730;color:#eef;font-family:system-ui,sans-serif;text-align:center}canvas{border-radius:12px;margin-top:16px;max-width:100%;height:auto}</style></head>
<body><h2>{{TITLE}}</h2><p>{{HELP}}</p><canvas id="c" width="480" height="360"></canvas><p id="s">Punkte: 0</p>
<script src="config.js"></script><script src="game.js"></script></body></html>
""";

    // Gemeinsamer Kopf: Bilder laden, Hintergrund, Ton (WebAudio-Piep), Leben/Sieg
    const string Common = """
const c=document.getElementById('c'),x=c.getContext('2d');const img=n=>{if(!n)return null;const i=new Image();i.src=n;return i;};
const PI=img(CFG.playerImage),BI=img(CFG.bgImage);let A=null;
function beep(f){if(!CFG.sound)return;try{A=A||new AudioContext();const o=A.createOscillator(),g=A.createGain();o.frequency.value=f;g.gain.value=.05*(typeof S!=='undefined'?S.vol/100:1);o.connect(g);g.connect(A.destination);o.start();o.stop(A.currentTime+.08);}catch(e){}}
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
        ["plattform"] = """
const T=32,LV=[(CFG.level&&CFG.level.length?CFG.level:['S.....Z','#######']),...(CFG.more||[]).filter(l=>l&&l.length)];let li=0,L=LV[0],R=L.length,C=Math.max(...L.map(r=>r.length)),DC=(CFG.decor&&CFG.decor[0])||[];
let G=[],open=false,kg=0,KT=0,cg=0,fk=0,fr=0,lf=0,boost=0,fired=[],note='',noteT=0,tu=false,face=1,air=0,inv=[],given={},gc=0,shots=[],cool=0,swing=0,veh=null,fuel=0,safe=0,noEnter=0,parked={};
function use(){if(!veh)return;const ux=Math.floor((pl.x+12)/T),uy=Math.floor((pl.y+15)/T);if(G[uy]&&G[uy][ux]==='.'){G[uy][ux]='V';parked[ux+','+uy]=veh;}veh=null;noEnter=30;say('Ausgestiegen');}
function dmg(f){f.hp--;f.h=10;if(f.hp<=0){f.a=0;fk++;p+=f.mh>1?10:2;beep(520);if(f.mh>1)say('Boss besiegt!');}else beep(380);}
function attack(){if(!CFG.weapon||cool>0||over)return;cool=20;if(CFG.weapon==='schwert'){swing=8;foes.forEach(f=>{if(!f.a)return;const dx=(f.x+12)-(pl.x+12);if((Math.sign(dx)===face||Math.abs(dx)<6)&&Math.abs(dx)<44&&Math.abs(f.y-pl.y)<28)dmg(f);});beep(300);}else{shots.push({x:pl.x+12+face*12,y:pl.y+12,d:face,l:60});beep(500);}}const AB=CFG.abilities||[];
function tx(x,y,k,d){const pre=li===0?'':(li+1)+':';const v=CFG.texts&&CFG.texts[pre+x+','+y+':'+k];return v||d;}
function doAct(a){if(a==='leben'){lives++;say('+1 Leben');}if(a==='tueren'){open=true;say('Türen offen');}if(a==='schneller'){boost=2;say('Schneller!');}if(a==='punkte'){p+=10;say('+10 Punkte');}if(a==='sieg'){over=true;say('Gewonnen!');}}
function rv(r){return r.when==='muenzen'?cg:r.when==='punkte'?p:r.when==='gegner'?fk:r.when==='sekunden'?fr/60:r.when==='gegenstaende'?gc:0;}
const SND={};Object.keys(CFG.sounds||{}).forEach(k=>{try{const a=new Audio(CFG.sounds[k]);if(k==='musik'){a.loop=true;a.volume=.5;}SND[k]=a;}catch(e){}});
function snd(ev,f){const a=SND[ev];if(a){try{a.volume=S.vol/100;a.currentTime=0;a.play();}catch(e){}}else beep(f);}
let music=false;const AI={};Object.keys(CFG.anims||{}).forEach(k=>{const a=CFG.anims[k];if(a&&a.img){const i=new Image();i.src=a.img;AI[k]={i:i,f:a.frames||1,fps:a.fps||8};}});
const at=(cx,cy)=>cy<0||cx<0||cx>=C?'#':cy>=R?'.':(G[cy][cx]||'.');const solid=(cx,cy)=>{const ch=at(cx,cy);return ch==='#'||ch==='F'||(ch==='D'&&!open);};
let S0x=0,S0y=0,sx=0,sy=0;
let pl,coins,foes,p=0,k={},cam=0,flash=0;
function pr(x,y,key,d){const pre=li===0?'':(li+1)+':';return (CFG.props&&CFG.props[pre+x+','+y+':'+key])||d;}
function load(i){li=i;veh=null;parked={};L=LV[i];R=L.length;C=Math.max(...L.map(r=>r.length));DC=(CFG.decor&&CFG.decor[i])||[];G=L.map(r=>[...r]);open=false;kg=0;lf=0;KT=L.join('').split('K').length-1;coins=[];foes=[];
L.forEach((r,y)=>[...r].forEach((ch,x)=>{if(ch==='S'){S0x=x;S0y=y;}if(ch==='o')coins.push({x:x*T+16,y:y*T+16,a:1,v:pr(x,y,'value',1)});if(ch==='E'){const hp=Math.max(1,Math.min(9,pr(x,y,'hp',1)));foes.push({x:x*T+4,y:y*T+6,d:1,a:1,s:pr(x,y,'speed',1),hp:hp,mh:hp,h:0});}}));sx=S0x;sy=S0y;
pl={x:sx*T+4,y:sy*T,w:24,h:30,vx:0,vy:0,on:false};cam=0;}
function reset(full){if(full){p=0;cg=0;fk=0;fr=0;boost=0;fired=[];tu=false;inv=[];given={};gc=0;shots=[];cool=0;swing=0;load(0);return;}pl={x:sx*T+4,y:sy*T,w:24,h:30,vx:0,vy:0,on:false};cam=0;}
reset(true);
/*SPIELSTAND*/const SV0=readSave();if(SV0)resume(SV0);
/*MENUS*/addEventListener('keydown',e=>{k[e.code]=1;if(e.code==='KeyE'&&!e.repeat)use();if((e.code==='KeyX'||e.code==='KeyJ')&&!e.repeat)attack();if(e.code==='KeyN'){dropSave();lives=CFG.lives;over=false;reset(true);say('Neues Spiel');}if(!music&&SND.musik){music=true;try{SND.musik.play();}catch(x){}}if(e.code==='Space'){e.preventDefault();if(over){lives=CFG.lives;over=false;reset(true);}else if(pl.on){pl.vy=-11.5;snd('springen',660);}else if(AB.includes('doppelsprung')&&air>0){air--;pl.vy=-10.5;snd('springen',760);}}});addEventListener('keyup',e=>k[e.code]=0);
function hit(fall){if(veh&&!fall){veh=null;flash=20;safe=45;pl.vy=-6;say('Fahrzeug kaputt – zu Fuß weiter');return;}if(safe>0&&!fall)return;veh=null;lives--;snd('treffer',160);flash=20;if(lives<=0){over=true;dropSave();}else reset(false);}
function say(t){note=t;noteT=120;}
function goal(){p+=10;snd('ziel',990);if(li+1<LV.length){load(li+1);saveGame();say((CFG.names&&CFG.names[li])||('Level '+(li+1)+' von '+LV.length));}else{over=true;dropSave();}}
function rules(){(CFG.rules||[]).forEach((r,i)=>{if(fired[i])return;const v=rv(r);if(v<r.n)return;fired[i]=1;
doAct(r.act);beep(990);});}
function move(o,dx,dy){o.x+=dx;let l=Math.floor(o.x/T),r=Math.floor((o.x+o.w-1)/T),t=Math.floor(o.y/T),b=Math.floor((o.y+o.h-1)/T);
for(let cy=t;cy<=b;cy++)for(let cx=l;cx<=r;cx++)if(solid(cx,cy)){if(dx>0)o.x=cx*T-o.w;if(dx<0)o.x=(cx+1)*T;}
o.y+=dy;o.on=false;l=Math.floor(o.x/T);r=Math.floor((o.x+o.w-1)/T);t=Math.floor(o.y/T);b=Math.floor((o.y+o.h-1)/T);
for(let cy=t;cy<=b;cy++)for(let cx=l;cx<=r;cx++)if(solid(cx,cy)){if(dy>0){o.y=cy*T-o.h;o.vy=0;o.on=true;}if(dy<0){o.y=(cy+1)*T;o.vy=0;}}}
function state(){return flash>0?'treffen':!pl.on?(pl.vy<0?'springen':'fallen'):(Math.abs(pl.vx)>0.1?'laufen':'stehen');}
function drawMe(){const a=AI[state()]||AI['stehen'];if(a&&a.i.complete&&a.i.naturalWidth){const fw=a.i.naturalWidth/a.f,n=Math.floor(fr*a.fps/60)%a.f;x.save();if(face<0){x.translate(pl.x+pl.w,pl.y);x.scale(-1,1);x.drawImage(a.i,n*fw,0,fw,a.i.naturalHeight,0,0,pl.w,pl.h);}else x.drawImage(a.i,n*fw,0,fw,a.i.naturalHeight,pl.x,pl.y,pl.w,pl.h);x.restore();}else me(pl.x,pl.y,pl.w,pl.h);}
function deco(ch,px,py){if(ch==='w'){x.fillStyle='rgba(255,255,255,.75)';x.beginPath();x.arc(px+10,py+18,9,0,7);x.arc(px+20,py+13,11,0,7);x.arc(px+28,py+19,7,0,7);x.fill();}
if(ch==='b'){x.fillStyle='#2E8B3E';x.beginPath();x.arc(px+16,py+24,11,0,7);x.arc(px+8,py+27,7,0,7);x.arc(px+24,py+27,7,0,7);x.fill();}
if(ch==='t'){x.fillStyle='#6B4423';x.fillRect(px+13,py+14,6,18);x.fillStyle='#2F7D3A';x.beginPath();x.arc(px+16,py+11,12,0,7);x.fill();}
if(ch==='m'){x.fillStyle='rgba(120,130,160,.6)';x.beginPath();x.moveTo(px,py+T);x.lineTo(px+16,py);x.lineTo(px+T,py+T);x.fill();}
if(ch==='g'){x.fillStyle='#3FBF5A';for(let i=0;i<4;i++)x.fillRect(px+3+i*8,py+T-8,3,8);}}
function step(){pollPad();if(!over&&mode==='play'){fr++;lf++;const sp=(2.2+CFG.speed*.35+boost)*(AB.includes('sprint')&&(k.ShiftLeft||k.ShiftRight)?1.6:1)*(veh==='wagen'?1.8:veh==='ufo'?1.3:1);pl.vx=(k.ArrowRight?sp:0)-(k.ArrowLeft?sp:0);if(pl.vx>0)face=1;if(pl.vx<0)face=-1;pl.vy=Math.min(14,pl.vy+.6);if(veh==='ufo'&&(k.Space||k.ArrowUp||k.KeyW)&&fuel>0){pl.vy=Math.max(-6,pl.vy-1.8);fuel--;if(fuel<=0){veh=null;say('Treibstoff leer');}}if(safe>0)safe--;if(noEnter>0)noEnter--;move(pl,pl.vx,0);move(pl,0,pl.vy);if(pl.on)air=AB.includes('doppelsprung')?1:0;
if(cool>0)cool--;if(swing>0)swing--;foes.forEach(f=>{if(f.h>0)f.h--;});shots.forEach(s=>{s.x+=s.d*7;s.l--;if(solid(Math.floor(s.x/T),Math.floor(s.y/T)))s.l=0;foes.forEach(f=>{if(f.a&&s.l>0&&Math.abs(f.x+12-s.x)<16&&Math.abs(f.y+13-s.y)<18){dmg(f);s.l=0;}});});shots=shots.filter(s=>s.l>0);
if(pl.on&&at(Math.floor((pl.x+12)/T),Math.floor((pl.y+pl.h+1)/T))==='F'){pl.vy=-17;pl.on=false;beep(720);}
if(pl.y>R*T+60)hit(true);const cx=Math.floor((pl.x+12)/T),cy=Math.floor((pl.y+15)/T),cb=Math.floor((pl.y+29)/T);
if(at(cx,cb)==='^'||at(cx,cy)==='^')hit();else if(at(cx,cy)==='Z'||at(cx,cb)==='Z')goal();else{
[cy,cb].forEach(yy=>{if(at(cx,yy)==='K'){G[yy][cx]='.';kg++;beep(880);if(kg>=KT){open=true;say('Alle Schlüssel – Türen offen');}}if(at(cx,yy)==='V'&&!veh&&noEnter===0){G[yy][cx]='.';const kd=parked[cx+','+yy]||tx(cx,yy,'kind','wagen');veh=kd==='ufo'?'ufo':'wagen';fuel=veh==='ufo'?600:0;say(veh==='ufo'?'Ufo: Sprungtaste halten – E steigt aus':'Wagen: schnell, fängt einen Treffer ab – E steigt aus');}if(at(cx,yy)==='G'){G[yy][cx]='.';const n=tx(cx,yy,'name','Kristall');inv.push(n);gc++;beep(1040);say(n+' eingesammelt');}if(at(cx,yy)==='N'){const key=li+':'+cx+','+yy,need=tx(cx,yy,'need','');if(need&&!given[key]&&inv.includes(need)){inv.splice(inv.indexOf(need),1);given[key]=1;doAct(tx(cx,yy,'give','punkte'));say(tx(cx,yy,'thanks','Danke!'));}else if(noteT<30)say(given[key]?tx(cx,yy,'thanks','Danke!'):tx(cx,yy,'say','Hallo!'));}if(at(cx,yy)==='C'&&(sx!==cx||sy!==yy)){sx=cx;sy=yy;saveGame();say('Gespeichert');}});
coins.forEach(q=>{if(q.a&&Math.abs(q.x-(pl.x+12))<18&&Math.abs(q.y-(pl.y+15))<20){q.a=0;cg++;p+=q.v||1;snd('muenze',880);if(won(p))over=true;}});
foes.forEach(f=>{if(!f.a)return;const nx=f.x+f.d*1.2*(f.s||1),cxx=Math.floor((f.d>0?nx+24:nx)/T),cyy=Math.floor((f.y+20)/T);if(solid(cxx,cyy)||!solid(cxx,cyy+1))f.d*=-1;else f.x=nx;
if(Math.abs(f.x-pl.x)<24&&Math.abs(f.y-pl.y)<26){if(pl.vy>1&&pl.y<f.y-8){dmg(f);pl.vy=-8;}else hit();}});
if(CFG.timeLimit>0&&lf/60>CFG.timeLimit){lives=0;over=true;tu=true;say('Zeit abgelaufen');}
rules();}cam=Math.max(0,Math.min(C*T-c.width,pl.x-c.width/3));}
bg();if(S.contrast){x.fillStyle='#000';x.fillRect(0,0,c.width,c.height);}x.save();x.translate(-cam,Math.min(0,c.height-R*T));
for(let y=0;y<DC.length;y++)for(let xx=Math.floor(cam/T);xx<=Math.floor((cam+c.width)/T)&&xx<(DC[y]||'').length;xx++)deco(DC[y][xx],xx*T,y*T);
for(let y=0;y<R;y++)for(let xx=Math.floor(cam/T);xx<=Math.floor((cam+c.width)/T)&&xx<C;xx++){const ch=at(xx,y);
if(ch==='#'){x.fillStyle='#5B3A1E';x.fillRect(xx*T,y*T,T,T);if(at(xx,y-1)!=='#'){x.fillStyle='#3FBF5A';x.fillRect(xx*T,y*T,T,7);}}
if(ch==='^'){x.fillStyle='#C9CED6';for(let i=0;i<4;i++){x.beginPath();x.moveTo(xx*T+i*8,y*T+T);x.lineTo(xx*T+i*8+4,y*T+T-14);x.lineTo(xx*T+i*8+8,y*T+T);x.fill();}}
if(ch==='Z'){x.fillStyle='#EEE';x.fillRect(xx*T+6,y*T-T,4,2*T);x.fillStyle=CFG.accent;x.beginPath();x.moveTo(xx*T+10,y*T-T);x.lineTo(xx*T+30,y*T-T+9);x.lineTo(xx*T+10,y*T-T+18);x.fill();}
if(ch==='F'){x.fillStyle='#64748B';x.fillRect(xx*T,y*T+T-8,T,8);x.fillStyle='#22C55E';x.fillRect(xx*T+3,y*T+T-16,T-6,6);}
if(ch==='C'){x.fillStyle=(sx===xx&&sy===y)?'#22C55E':'#94A3B8';x.fillRect(xx*T+13,y*T+4,4,T-4);x.fillRect(xx*T+17,y*T+4,11,8);}
if(ch==='K'){x.fillStyle=CFG.accent;x.beginPath();x.arc(xx*T+11,y*T+16,6,0,7);x.fill();x.fillRect(xx*T+15,y*T+14,13,4);}
if(ch==='V'){x.fillStyle='#F97316';x.fillRect(xx*T+2,y*T+14,28,12);x.fillStyle='#111';x.beginPath();x.arc(xx*T+9,y*T+27,5,0,7);x.arc(xx*T+23,y*T+27,5,0,7);x.fill();}if(ch==='G'){x.fillStyle='#A855F7';x.beginPath();x.moveTo(xx*T+16,y*T+6);x.lineTo(xx*T+26,y*T+16);x.lineTo(xx*T+16,y*T+28);x.lineTo(xx*T+6,y*T+16);x.fill();}if(ch==='N'){x.fillStyle='#14B8A6';x.fillRect(xx*T+9,y*T+14,14,18);x.beginPath();x.arc(xx*T+16,y*T+9,6,0,7);x.fill();}if(ch==='D'&&!open){x.fillStyle='#7C4A1E';x.fillRect(xx*T+2,y*T,T-4,T);x.fillStyle=CFG.accent;x.fillRect(xx*T+T-10,y*T+15,4,4);}}
x.fillStyle=CFG.accent;coins.forEach(q=>{if(q.a){x.beginPath();x.arc(q.x,q.y,7,0,7);x.fill();}});
foes.forEach(f=>{if(f.a){x.fillStyle=f.h>0&&f.h%4<2?'#FFF':'#E0475B';x.fillRect(f.x,f.y,24,26);x.fillStyle='#FFF';x.fillRect(f.x+(f.d>0?14:4),f.y+6,6,6);if(f.mh>1){x.fillStyle='#000';x.fillRect(f.x-2,f.y-8,28,4);x.fillStyle='#22C55E';x.fillRect(f.x-2,f.y-8,28*f.hp/f.mh,4);}}});x.fillStyle=CFG.accent;shots.forEach(s=>{x.beginPath();x.arc(s.x,s.y,4,0,7);x.fill();});if(swing>0){x.strokeStyle='#E5E7EB';x.lineWidth=3;x.beginPath();if(face>0)x.arc(pl.x+12,pl.y+14,26,-1,1);else x.arc(pl.x+12,pl.y+14,26,Math.PI-1,Math.PI+1);x.stroke();}
if(veh){if(veh==='ufo'){x.fillStyle='#94A3B8';x.beginPath();x.ellipse(pl.x+12,pl.y+28,20,7,0,0,7);x.fill();}else{x.fillStyle='#F97316';x.fillRect(pl.x-6,pl.y+18,36,12);x.fillStyle='#111';x.beginPath();x.arc(pl.x+2,pl.y+31,5,0,7);x.arc(pl.x+22,pl.y+31,5,0,7);x.fill();}}if(!(flash>0&&flash--%4<2&&!S.calm))drawMe();if(S.contrast){x.strokeStyle='#FFF';x.lineWidth=2;x.strokeRect(pl.x,pl.y,pl.w,pl.h);}x.restore();
x.font=(S.big?'19px':'14px')+' system-ui';x.fillStyle='#FFF';if(LV.length>1)x.fillText('Level '+(li+1)+'/'+LV.length,10,c.height-10);
if(CFG.timeLimit>0)x.fillText('Zeit '+Math.max(0,Math.ceil(CFG.timeLimit-lf/60))+' s',c.width-90,20);
if(KT>0){x.fillStyle=CFG.accent;x.fillText('Schlüssel '+kg+'/'+KT,10,20);}if(veh==='ufo'){x.fillStyle='#FFF';x.fillText('Treibstoff '+Math.ceil(fuel/60)+' s',c.width-130,40);}if(inv.length){x.fillStyle='#E9D5FF';x.fillText('Inventar: '+inv.join(', '),10,c.height-28);}
(CFG.rules||[]).forEach((r,i)=>{if(!r.title)return;const v=rv(r);x.fillStyle=fired[i]?'#22C55E':'#FFF';x.fillText(fired[i]?'✓ '+r.title:r.title+': '+Math.min(Math.floor(v),r.n)+'/'+r.n,10,40+i*18);});
if(noteT>0){noteT--;x.fillStyle='rgba(0,0,0,.6)';x.fillRect(c.width/2-110,40,220,28);x.fillStyle='#FFF';x.font='15px system-ui';x.textAlign='center';x.fillText(note,c.width/2,59);x.textAlign='left';}
drawMenu();status(p);requestAnimationFrame(step);}
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

    /// <summary>Spiel-JavaScript einer Vorlage; der Plattformer bekommt den Spielstand (Paket 01 S1-15).</summary>
    public static string GameJs(string template) => Common + Js[template].TrimStart().Replace("/*SPIELSTAND*/", Programs.GameSaves.Js).Replace("/*MENUS*/", Programs.GameMenus.Js);

    public static string CreateBrowserGame(string parent, string template, string name, GameSettings? settings = null)
    {
        var t = Templates.FirstOrDefault(x => x.Id == template);
        if (t.Id is null) throw new ArgumentException("Unbekannte Vorlage");
        var dir = Path.Combine(parent, DevStudio.SafeName(name));
        if (Directory.Exists(dir)) throw new IOException($"Den Ordner „{Path.GetFileName(dir)}“ gibt es schon.");
        Directory.CreateDirectory(dir);
        var enc = System.Net.WebUtility.HtmlEncode(name);
        File.WriteAllText(Path.Combine(dir, "index.html"), Shell.Replace("{{TITLE}}", enc).Replace("{{HELP}}", t.Help), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(dir, "game.js"), GameJs(t.Id), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(dir, "vorlage.txt"), t.Id);
        Save(dir, settings ?? new GameSettings { Level = t.Id == "breakout" ? DefaultBreakout : t.Id == "jump" ? DefaultJump : t.Id == "plattform" ? DefaultPlatform : [] });
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
        // Unreal-Projekt- und C++-Modulname: nur ASCII-Buchstaben und Ziffern (ä → ae …)
        var ascii = name.Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("Ä", "Ae").Replace("Ö", "Oe").Replace("Ü", "Ue").Replace("ß", "ss");
        var safe = new string(DevStudio.SafeName(ascii).Where(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9').ToArray());
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
