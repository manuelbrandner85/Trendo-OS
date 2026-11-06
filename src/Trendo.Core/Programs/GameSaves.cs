using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trendo.Core.Programs;

/// <summary>Spielstand eines Trendo-Spiels (Paket 01 / S1-15 „Save &amp; Progression“): Level, Punkte, Leben, Speicherpunkt, Fortschritt und Entscheidungen.
/// Ein Format für alle: Browser-Spiel (localStorage), Probespiel in Trendo, Unity (persistentDataPath) und Godot (user://).</summary>
public sealed record GameSave
{
    public int Version { get; init; } = GameSaves.Current;
    public int Level { get; init; }
    public int Score { get; init; }
    public int Lives { get; init; } = 3;
    /// <summary>Speicherpunkt in Kacheln (Spalte, Zeile); −1 = Start des Levels.</summary>
    public int CheckX { get; init; } = -1;
    public int CheckY { get; init; } = -1;
    public int Coins { get; init; }
    public int Defeated { get; init; }
    public int PlaySeconds { get; init; }
    /// <summary>Entscheidungen und Fortschritt („tuer-burg“: „offen“), höchstens 50 Einträge.</summary>
    public Dictionary<string, string> Flags { get; init; } = [];
    public DateTime SavedUtc { get; init; } = DateTime.UtcNow;
}

public static class GameSaves
{
    /// <summary>Aktuelles Format. Höhere Nummern stammen aus einer neueren Trendo-Fassung und werden nicht still verändert.</summary>
    public const int Current = 1;
    public const string FileName = "trendo-spielstand.json";

    static readonly JsonSerializerOptions Opt = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    public static string ToJson(GameSave s) => JsonSerializer.Serialize(s with { Version = Current }, Opt);

    /// <summary>Älteren oder unversionierten Stand auf das aktuelle Format heben. Unversioniert (frühe Browser-Spiele: nur level/score/lives) gilt als Format 1.</summary>
    public static JsonObject Migrate(JsonObject o)
    {
        var v = o["version"] ?? o["v"];
        var n = v is JsonValue jv && jv.TryGetValue<int>(out var i) ? i : 0;
        if (n > Current) throw new InvalidDataException($"Der Spielstand stammt aus einer neueren Fassung (Format {n}) – bitte Trendo aktualisieren.");
        if (n < 0) throw new InvalidDataException("Der Spielstand hat eine ungültige Formatnummer.");
        var m = (JsonObject)o.DeepClone();
        m.Remove("v");
        if (n == 0) // unversioniert → Format 1: Kurznamen übernehmen
        {
            foreach (var (from, to) in new[] { ("cx", "checkX"), ("cy", "checkY"), ("time", "playSeconds"), ("defeated", "defeated"), ("coins", "coins") })
                if (m[from] is { } val && from != to) { m.Remove(from); m[to] = val.DeepClone(); }
        }
        m["version"] = Current;
        return m;
    }

    /// <summary>Spielstand lesen, heben und gegen das Spiel prüfen. Kaputte Dateien und unpassende Werte werden verständlich abgelehnt bzw. begrenzt.</summary>
    public static GameSave Parse(string json, int levelCount)
    {
        JsonObject o;
        try { o = JsonNode.Parse(json) as JsonObject ?? throw new InvalidDataException("Der Spielstand ist leer oder kein Spielstand."); }
        catch (JsonException) { throw new InvalidDataException("Der Spielstand ist beschädigt und kann nicht geladen werden."); }
        GameSave s;
        try { s = Migrate(o).Deserialize<GameSave>(Opt) ?? throw new InvalidDataException("Der Spielstand ist leer."); }
        catch (Exception e) when (e is JsonException or FormatException or InvalidOperationException) { throw new InvalidDataException("Der Spielstand enthält ungültige Werte."); }
        if (levelCount <= 0) throw new ArgumentException("Das Spiel hat kein Level.");
        if (s.Level < 0 || s.Level >= levelCount) throw new InvalidDataException($"Level {s.Level + 1} gibt es in diesem Spiel nicht (mehr) – der Spielstand passt nicht zum Spiel.");
        return s with
        {
            Score = Math.Clamp(s.Score, 0, 9_999_999), Lives = Math.Clamp(s.Lives, 1, 99), Coins = Math.Max(0, s.Coins), Defeated = Math.Max(0, s.Defeated),
            PlaySeconds = Math.Max(0, s.PlaySeconds), Flags = s.Flags.Take(50).ToDictionary(kv => kv.Key.Length > 40 ? kv.Key[..40] : kv.Key, kv => kv.Value.Length > 200 ? kv.Value[..200] : kv.Value),
        };
    }

    /// <summary>Gilt der Speicherpunkt im Level (Kachel C oder S)? Sonst beginnt das Level am Start.</summary>
    public static bool CheckpointValid(GameSave s, IReadOnlyList<string> level) =>
        s.CheckY >= 0 && s.CheckY < level.Count && s.CheckX >= 0 && s.CheckX < level[s.CheckY].Length && level[s.CheckY][s.CheckX] is 'C' or 'S';

    /// <summary>Kurzbeschreibung für die Anzeige.</summary>
    public static string Describe(GameSave s, int levelCount) =>
        $"Level {s.Level + 1} von {levelCount} · {s.Score} Punkte · {s.Lives} Leben" + (s.CheckX >= 0 ? " · am Speicherpunkt" : "") + $" · {s.PlaySeconds / 60} min gespielt";

    /// <summary>JavaScript fürs Browser-Spiel (gleiches Format, Speicherort localStorage je Spiel).</summary>
    public const string Js = """
const SK='trendo-spielstand:'+document.title;
function saveGame(){if(CFG.saves===false)return;try{localStorage.setItem(SK,JSON.stringify({version:1,level:li,score:p,lives:lives,checkX:sx,checkY:sy,coins:cg,defeated:fk,playSeconds:Math.round(fr/60),flags:{inventar:inv.join('|')},savedUtc:new Date().toISOString()}));}catch(e){}}
function dropSave(){try{localStorage.removeItem(SK);}catch(e){}}
function readSave(){if(CFG.saves===false)return null;try{let s=JSON.parse(localStorage.getItem(SK)||'null');if(!s||typeof s!=='object')return null;const v=s.version!==undefined?s.version:(s.v||0);if(v>1)return null;
if(v===0){s={level:s.level,score:s.score,lives:s.lives,checkX:s.cx,checkY:s.cy,coins:s.coins,defeated:s.defeated,playSeconds:s.time};}
const n=x=>Number.isFinite(x)?Math.floor(x):0;s={level:n(s.level),score:Math.max(0,n(s.score)),lives:Math.max(1,Math.min(99,n(s.lives)||1)),checkX:Number.isFinite(s.checkX)?s.checkX:-1,checkY:Number.isFinite(s.checkY)?s.checkY:-1,coins:Math.max(0,n(s.coins)),defeated:Math.max(0,n(s.defeated)),playSeconds:Math.max(0,n(s.playSeconds)),flags:(s.flags&&typeof s.flags==='object')?s.flags:{}};
if(s.level<0||s.level>=LV.length)return null;return s;}catch(e){return null;}}
function resume(s){load(s.level);const r=LV[s.level];if(s.checkY>=0&&s.checkY<r.length&&'CS'.includes((r[s.checkY]||'')[s.checkX]||'')){sx=s.checkX;sy=s.checkY;}reset(false);p=s.score;lives=s.lives;cg=s.coins;fk=s.defeated;fr=s.playSeconds*60;inv=String(s.flags.inventar||'').split('|').filter(Boolean);say('Spielstand geladen – N: neu beginnen');}

""";
}
