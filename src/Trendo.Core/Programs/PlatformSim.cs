namespace Trendo.Core.Programs;

/// <summary>Spiele bauen als Spiel-Editor (Masterprompt 131, Runde 7): Probespielen im Trendo-Fenster. Dieselben Regeln wie das gebaute Browser-Spiel
/// (game.js „plattform“): Kachel 32 px, Figur 24 × 30, Schwerkraft 0,6 bis höchstens 14, Sprung 11,5, Gegner laufen hin und her und kehren an Wand oder Kante um,
/// Draufspringen besiegt sie. Eigenschaften je Objekt: Gegner-Tempo („x,y:speed“) und Münzwert („x,y:value“).</summary>
public sealed class PlatformSim
{
    public const int T = 32;
    public sealed class Body { public double X, Y, Vx, Vy; public double W = 24, H = 30; public bool On; }
    public sealed record Coin(int Col, int Row, double X, double Y, int Value) { public bool Taken { get; set; } }
    public sealed class Foe { public int Col, Row; public double X, Y, Dir = 1, Speed = 1; public bool Alive = true; }

    readonly string[] _rows; readonly IReadOnlyDictionary<string, double>? _props; readonly int _speed; readonly int _startLives; readonly double _sx, _sy;
    public int Rows => _rows.Length;
    public int Cols { get; }
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

    public PlatformSim(IReadOnlyList<string> level, int speed = 5, int lives = 3, IReadOnlyDictionary<string, double>? props = null)
    {
        _rows = level.Count > 0 ? [.. level] : ["S.....Z", "#######"]; _props = props;
        Cols = _rows.Max(r => r.Length); _speed = Math.Clamp(speed, 1, 10); _startLives = Math.Clamp(lives, 1, 9); Lives = _startLives;
        for (var y = 0; y < _rows.Length; y++) for (var x = 0; x < _rows[y].Length; x++) if (_rows[y][x] == 'S') { _sx = x; _sy = y; }
        double P(int x, int y, string k, double def) => props is not null && props.TryGetValue($"{x},{y}:{k}", out var v) ? v : def;
        for (var y = 0; y < _rows.Length; y++)
            for (var x = 0; x < _rows[y].Length; x++)
            {
                if (_rows[y][x] == 'o') Coins.Add(new(x, y, x * T + 16, y * T + 16, (int)Math.Clamp(P(x, y, "value", 1), 1, 100)));
                if (_rows[y][x] == 'E') Foes.Add(new Foe { Col = x, Row = y, X = x * T + 4, Y = y * T + 6, Speed = Math.Clamp(P(x, y, "speed", 1), 0.25, 4) });
            }
        ResetPlayer();
    }

    void ResetPlayer() => Player = new Body { X = _sx * T + 4, Y = _sy * T };

    public char At(int cx, int cy) => cy < 0 || cx < 0 || cx >= Cols ? '#' : cy >= Rows ? '.' : cx < _rows[cy].Length ? _rows[cy][cx] : '.';
    bool Solid(int cx, int cy) => At(cx, cy) == '#';

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

    /// <summary>Ein Bild weiter (60 je Sekunde). <paramref name="jump"/> springt nur vom Boden aus.</summary>
    public void Step(bool left, bool right, bool jump)
    {
        if (Flash > 0) Flash--;
        if (Over) return;
        Frames++;
        var pl = Player; var sp = 2.2 + _speed * .35;
        if (jump && pl.On) pl.Vy = -11.5;
        pl.Vx = (right ? sp : 0) - (left ? sp : 0); pl.Vy = Math.Min(14, pl.Vy + .6);
        Move(pl, pl.Vx, 0); Move(pl, 0, pl.Vy);
        if (pl.Y > Rows * T + 60) { Hit(); return; }
        int cx = (int)Math.Floor((pl.X + 12) / T), cy = (int)Math.Floor((pl.Y + 15) / T), cb = (int)Math.Floor((pl.Y + 29) / T);
        if (At(cx, cb) == '^' || At(cx, cy) == '^') { Hit(); return; }
        if (At(cx, cy) == 'Z' || At(cx, cb) == 'Z') { Score += 10; Won = true; return; }
        foreach (var q in Coins) if (!q.Taken && Math.Abs(q.X - (pl.X + 12)) < 18 && Math.Abs(q.Y - (pl.Y + 15)) < 20) { q.Taken = true; Score += q.Value; }
        foreach (var f in Foes)
        {
            if (!f.Alive) continue;
            var nx = f.X + f.Dir * 1.2 * f.Speed; int fx = (int)Math.Floor((f.Dir > 0 ? nx + 24 : nx) / T), fy = (int)Math.Floor((f.Y + 20) / T);
            if (Solid(fx, fy) || !Solid(fx, fy + 1)) f.Dir *= -1; else f.X = nx;
            if (Math.Abs(f.X - pl.X) < 24 && Math.Abs(f.Y - pl.Y) < 26)
            {
                if (pl.Vy > 1 && pl.Y < f.Y - 8) { f.Alive = false; pl.Vy = -8; Score += 2; }
                else { Hit(); return; }
            }
        }
    }

    /// <summary>Nochmal von vorn (alle Münzen und Gegner zurück).</summary>
    public PlatformSim Restart() => new(_rows, _speed, _startLives, _props);
}
