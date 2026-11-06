namespace Trendo.Core.Programs;

/// <summary>Spiele bauen als Spiel-Editor (Masterprompt 131, Runde 7): Probespielen im Trendo-Fenster. Dieselben Regeln wie das gebaute Browser-Spiel
/// (game.js „plattform“): Kachel 32 px, Figur 24 × 30, Schwerkraft 0,6 bis höchstens 14, Sprung 11,5, Gegner laufen hin und her und kehren an Wand oder Kante um,
/// Draufspringen besiegt sie. Eigenschaften je Objekt: Gegner-Tempo („x,y:speed“) und Münzwert („x,y:value“); ab Level 2 mit Vorsilbe („2:x,y:speed“).
/// Seit 04.11.2026 wie GDevelop/Construct: Sprungfeder (F), Speicherpunkt (C), Schlüssel (K) und Tür (D), Zeitlimit und Ereignis-Regeln.
/// Seit 05.11.2026: mehrere Level hintereinander (Ziel → nächstes Level, Punkte und Leben bleiben) und der Zustand der Figur für Animationen.</summary>
public sealed class PlatformSim
{
    public const int T = 32;
    public sealed class Body { public double X, Y, Vx, Vy; public double W = 24, H = 30; public bool On; public int Facing = 1; }
    public sealed record Coin(int Col, int Row, double X, double Y, int Value) { public bool Taken { get; set; } }
    public sealed class Foe { public int Col, Row; public double X, Y, Dir = 1, Speed = 1; public bool Alive = true; }

    /// <summary>Zustände der Spielfigur – je Zustand kann das Spiel eine eigene Animation zeigen (wie AnimatedSprite in Godot).</summary>
    public static readonly (string Id, string Label)[] States = [("stehen", "Stehen"), ("laufen", "Laufen"), ("springen", "Springen"), ("fallen", "Fallen"), ("treffen", "Getroffen")];

    readonly IReadOnlyList<string[]> _levels; readonly IReadOnlyDictionary<string, double>? _props; readonly int _speed; readonly int _startLives;
    readonly int _timeLimit; readonly IReadOnlyList<GameStudio.GameRule> _rules; readonly bool[] _fired;
    string[] _rows = []; char[][] _grid = [];
    double _sx, _sy; double _boost;
    readonly IReadOnlyList<string> _abilities = []; bool _jumpPrev; int _airJumps;
    readonly IReadOnlyDictionary<string, string> _texts = new Dictionary<string, string>(); readonly HashSet<string> _given = [];
    /// <summary>Inventar: eingesammelte Gegenstände (Paket 01 S1-05).</summary>
    public List<string> Inventory { get; } = [];
    public int ItemsCollected { get; private set; }
    string Text(int x, int y, string k, string def) => _texts.TryGetValue($"{(LevelIndex == 0 ? "" : $"{LevelIndex + 1}:")}{x},{y}:{k}", out var v) && v.Length > 0 ? v : def;
    public bool Has(string ability) => _abilities.Contains(ability);
    /// <summary>Aufgaben (Regeln mit Titel) mit Fortschritt – wie das Aufgabenbuch in Spielen.</summary>
    public IEnumerable<(GameStudio.GameRule Rule, double Value, bool Done)> Quests => _rules.Select((r, i) => (r, RuleValue(r), _fired[i])).Where(q => q.r.Title is not null);
    public int Rows => _rows.Length;
    public int Cols { get; private set; }
    public Body Player { get; private set; } = new();
    public List<Coin> Coins { get; } = [];
    public List<Foe> Foes { get; } = [];
    public int Score { get; private set; }
    public int Lives { get; private set; }
    public bool Won { get; private set; }
    public bool Lost => Lives <= 0;
    public bool Over => Won || Lost;
    public int Frames { get; private set; }
    /// <summary>Kurzes Blinken nach einem Treffer (Bilder).</summary>
    public int Flash { get; private set; }
    public int KeysTotal { get; private set; }
    public int Keys { get; private set; }
    public bool DoorsOpen { get; private set; }
    public int FoesDefeated { get; private set; }
    public bool TimeUp { get; private set; }
    /// <summary>Aktuelles Level (0 = erstes) und Anzahl der Level.</summary>
    public int LevelIndex { get; private set; }
    public int LevelCount => _levels.Count;
    /// <summary>Aktueller Speicherpunkt (Spalte, Zeile).</summary>
    public (int X, int Y) Respawn => ((int)_sx, (int)_sy);
    /// <summary>Letzte Meldung (Regel, Speicherpunkt, Türen, nächstes Level) für die Anzeige.</summary>
    public string Note { get; private set; } = "";
    /// <summary>Verbleibende Sekunden (−1 = ohne Zeitlimit) – gilt je Level.</summary>
    public double SecondsLeft => _timeLimit > 0 ? Math.Max(0, _timeLimit - _levelFrames / 60.0) : -1;
    int _levelFrames;

    /// <summary>Zustand der Figur: stehen, laufen, springen (steigt) oder fallen.</summary>
    public string State => Flash > 0 ? "treffen" : !Player.On ? (Player.Vy < 0 ? "springen" : "fallen") : Math.Abs(Player.Vx) > 0.1 ? "laufen" : "stehen";

    public PlatformSim(IReadOnlyList<string> level, int speed = 5, int lives = 3, IReadOnlyDictionary<string, double>? props = null, int timeLimit = 0,
        IReadOnlyList<GameStudio.GameRule>? rules = null, IReadOnlyList<IReadOnlyList<string>>? moreLevels = null, IReadOnlyList<string>? abilities = null, IReadOnlyDictionary<string, string>? texts = null)
    {
        _abilities = abilities ?? []; _texts = texts ?? new Dictionary<string, string>();
        var first = level.Count > 0 ? level.ToArray() : ["S.....Z", "#######"];
        _levels = [first, .. (moreLevels ?? []).Where(l => l.Count > 0).Select(l => l.ToArray())];
        _props = props; _timeLimit = Math.Clamp(timeLimit, 0, 3600);
        _rules = rules ?? []; _fired = new bool[_rules.Count];
        _speed = Math.Clamp(speed, 1, 10); _startLives = Math.Clamp(lives, 1, 9); Lives = _startLives;
        Load(0);
    }

    /// <summary>Level laden: Raster, Start, Münzen, Gegner, Schlüssel; Punkte, Leben und Regeln bleiben.</summary>
    void Load(int index)
    {
        LevelIndex = index; _rows = _levels[index]; _grid = [.. _rows.Select(r => r.ToCharArray())];
        Cols = _rows.Max(r => r.Length); Coins.Clear(); Foes.Clear(); Keys = 0; DoorsOpen = false; _levelFrames = 0;
        for (var y = 0; y < _rows.Length; y++) for (var x = 0; x < _rows[y].Length; x++) if (_rows[y][x] == 'S') { _sx = x; _sy = y; }
        KeysTotal = string.Concat(_rows).Count(c => c == 'K');
        var pre = index == 0 ? "" : $"{index + 1}:";
        double P(int x, int y, string k, double def) => _props is not null && _props.TryGetValue($"{pre}{x},{y}:{k}", out var v) ? v : def;
        for (var y = 0; y < _rows.Length; y++)
            for (var x = 0; x < _rows[y].Length; x++)
            {
                if (_rows[y][x] == 'o') Coins.Add(new(x, y, x * T + 16, y * T + 16, (int)Math.Clamp(P(x, y, "value", 1), 1, 100)));
                if (_rows[y][x] == 'E') Foes.Add(new Foe { Col = x, Row = y, X = x * T + 4, Y = y * T + 6, Speed = Math.Clamp(P(x, y, "speed", 1), 0.25, 4) });
            }
        ResetPlayer();
    }

    void ResetPlayer() => Player = new Body { X = _sx * T + 4, Y = _sy * T };

    public char At(int cx, int cy) => cy < 0 || cx < 0 || cx >= Cols ? '#' : cy >= Rows ? '.' : cx < _grid[cy].Length ? _grid[cy][cx] : '.';
    bool Solid(int cx, int cy) { var c = At(cx, cy); return c is '#' or 'F' || (c == 'D' && !DoorsOpen); }

    void Move(Body o, double dx, double dy)
    {
        o.X += dx;
        int l = (int)Math.Floor(o.X / T), r = (int)Math.Floor((o.X + o.W - 1) / T), t = (int)Math.Floor(o.Y / T), b = (int)Math.Floor((o.Y + o.H - 1) / T);
        for (var cy = t; cy <= b; cy++) for (var cx = l; cx <= r; cx++) if (Solid(cx, cy)) { if (dx > 0) o.X = cx * T - o.W; if (dx < 0) o.X = (cx + 1) * T; }
        o.Y += dy; o.On = false;
        l = (int)Math.Floor(o.X / T); r = (int)Math.Floor((o.X + o.W - 1) / T); t = (int)Math.Floor(o.Y / T); b = (int)Math.Floor((o.Y + o.H - 1) / T);
        for (var cy = t; cy <= b; cy++) for (var cx = l; cx <= r; cx++) if (Solid(cx, cy)) { if (dy > 0) { o.Y = cy * T - o.H; o.Vy = 0; o.On = true; } if (dy < 0) { o.Y = (cy + 1) * T; o.Vy = 0; } }
        // Steht die Figur (fast) genau auf dem Boden, gilt sie als „am Boden“ – sonst flackert der Zustand und Springen klappt nur jedes zweite Bild.
        if (dy >= 0 && !o.On) { var fb = (int)Math.Floor((o.Y + o.H) / T); if (o.Y + o.H - fb * T < 1 && Enumerable.Range(l, r - l + 1).Any(cx => Solid(cx, fb))) o.On = true; }
    }

    void Hit() { Lives--; Flash = 20; if (!Lost) ResetPlayer(); }

    /// <summary>Ereignis-Regeln prüfen: jede feuert höchstens einmal.</summary>
    double RuleValue(GameStudio.GameRule r) => r.When switch { "muenzen" => Coins.Count(c => c.Taken), "punkte" => Score, "gegner" => FoesDefeated, "sekunden" => Frames / 60.0, "gegenstaende" => ItemsCollected, _ => 0 };

    void Rules()
    {
        for (var i = 0; i < _rules.Count; i++)
        {
            if (_fired[i]) continue;
            var r = _rules[i];
            var v = RuleValue(r);
            if (v < r.N) continue;
            _fired[i] = true; Note = GameStudio.RuleText(r);
            Apply(r.Do);
        }
    }

    /// <summary>Belohnung bzw. Wirkung (Regel oder sprechende Figur).</summary>
    void Apply(string what)
    {
        switch (what)
        {
            case "leben": Lives++; break;
            case "tueren": DoorsOpen = true; break;
            case "schneller": _boost = 2; break;
            case "punkte": Score += 10; break;
            case "sieg": Won = true; break;
        }
    }

    /// <summary>Ziel erreicht: nächstes Level laden oder gewonnen.</summary>
    void Goal()
    {
        Score += 10;
        if (LevelIndex + 1 < LevelCount) { Load(LevelIndex + 1); Note = $"Level {LevelIndex + 1} von {LevelCount}"; }
        else Won = true;
    }

    /// <summary>Ein Bild weiter (60 je Sekunde). <paramref name="jump"/> springt nur vom Boden aus.</summary>
    public void Step(bool left, bool right, bool jump, bool sprint = false)
    {
        var press = jump && !_jumpPrev; _jumpPrev = jump;
        if (Flash > 0) Flash--;
        if (Over) return;
        Frames++; _levelFrames++;
        var pl = Player; var sp = (2.2 + _speed * .35 + _boost) * (sprint && Has("sprint") ? 1.6 : 1);
        if (jump && pl.On) pl.Vy = -11.5;
        else if (press && !pl.On && _airJumps > 0) { _airJumps--; pl.Vy = -10.5; } // Doppelsprung
        pl.Vx = (right ? sp : 0) - (left ? sp : 0); pl.Vy = Math.Min(14, pl.Vy + .6);
        if (pl.Vx > 0) pl.Facing = 1; else if (pl.Vx < 0) pl.Facing = -1;
        Move(pl, pl.Vx, 0); Move(pl, 0, pl.Vy);
        if (pl.On) _airJumps = Has("doppelsprung") ? 1 : 0;
        if (pl.On && At((int)Math.Floor((pl.X + 12) / T), (int)Math.Floor((pl.Y + pl.H + 1) / T)) == 'F') { pl.Vy = -17; pl.On = false; } // Sprungfeder
        if (pl.Y > Rows * T + 60) { Hit(); return; }
        int cx = (int)Math.Floor((pl.X + 12) / T), cy = (int)Math.Floor((pl.Y + 15) / T), cb = (int)Math.Floor((pl.Y + 29) / T);
        if (At(cx, cb) == '^' || At(cx, cy) == '^') { Hit(); return; }
        if (At(cx, cy) == 'Z' || At(cx, cb) == 'Z') { Goal(); return; }
        foreach (var yy in new[] { cy, cb })
        {
            if (At(cx, yy) == 'K') { _grid[yy][cx] = '.'; Keys++; if (Keys >= KeysTotal) { DoorsOpen = true; Note = "Alle Schlüssel – Türen offen"; } }
            if (At(cx, yy) == 'G') { _grid[yy][cx] = '.'; var n = Text(cx, yy, "name", "Kristall"); Inventory.Add(n); ItemsCollected++; Note = n + " eingesammelt"; }
            if (At(cx, yy) == 'N')
            {
                var key = $"{LevelIndex}:{cx},{yy}"; var need = Text(cx, yy, "need", "");
                if (need.Length > 0 && !_given.Contains(key) && Inventory.Remove(need)) { _given.Add(key); Apply(Text(cx, yy, "give", "punkte")); Note = Text(cx, yy, "thanks", "Danke!"); }
                else Note = _given.Contains(key) ? Text(cx, yy, "thanks", "Danke!") : Text(cx, yy, "say", "Hallo!");
            }
            if (At(cx, yy) == 'C' && ((int)_sx != cx || (int)_sy != yy)) { _sx = cx; _sy = yy; Note = "Speicherpunkt"; }
        }
        foreach (var q in Coins) if (!q.Taken && Math.Abs(q.X - (pl.X + 12)) < 18 && Math.Abs(q.Y - (pl.Y + 15)) < 20) { q.Taken = true; Score += q.Value; }
        foreach (var f in Foes)
        {
            if (!f.Alive) continue;
            var nx = f.X + f.Dir * 1.2 * f.Speed; int fx = (int)Math.Floor((f.Dir > 0 ? nx + 24 : nx) / T), fy = (int)Math.Floor((f.Y + 20) / T);
            if (Solid(fx, fy) || !Solid(fx, fy + 1)) f.Dir *= -1; else f.X = nx;
            if (Math.Abs(f.X - pl.X) < 24 && Math.Abs(f.Y - pl.Y) < 26)
            {
                if (pl.Vy > 1 && pl.Y < f.Y - 8) { f.Alive = false; FoesDefeated++; pl.Vy = -8; Score += 2; }
                else { Hit(); return; }
            }
        }
        if (_timeLimit > 0 && _levelFrames / 60.0 > _timeLimit) { Lives = 0; TimeUp = true; Note = "Zeit abgelaufen"; return; }
        Rules();
    }

    /// <summary>Nochmal von vorn (alle Level, Münzen, Gegner, Schlüssel und Türen zurück).</summary>
    /// <summary>Spielstand des Probespiels (Paket 01 S1-15) – gleiches Format wie im gebauten Spiel.</summary>
    public GameSave Snapshot() => new() { Level = LevelIndex, Score = Score, Lives = Lives, CheckX = (int)_sx, CheckY = (int)_sy, Defeated = FoesDefeated, Coins = Coins.Count(c => c.Taken), PlaySeconds = Frames / 60, Flags = Inventory.Count > 0 ? new() { ["inventar"] = string.Join('|', Inventory) } : [] };

    /// <summary>Spielstand fortsetzen: Level laden, am gültigen Speicherpunkt beginnen, Punkte und Leben übernehmen.</summary>
    public void Restore(GameSave s)
    {
        if (s.Level < 0 || s.Level >= _levels.Count) throw new InvalidDataException($"Level {s.Level + 1} gibt es in diesem Spiel nicht.");
        Load(s.Level);
        if (GameSaves.CheckpointValid(s, _levels[s.Level])) { _sx = s.CheckX; _sy = s.CheckY; ResetPlayer(); }
        Score = s.Score; Lives = Math.Max(1, s.Lives); FoesDefeated = s.Defeated; Won = false; Note = "Spielstand geladen";
        Inventory.Clear(); if (s.Flags.TryGetValue("inventar", out var inv)) Inventory.AddRange(inv.Split('|', StringSplitOptions.RemoveEmptyEntries));
    }

    public PlatformSim Restart() => new(_levels[0], _speed, _startLives, _props, _timeLimit, _rules, [.. _levels.Skip(1)], _abilities, _texts);
}
