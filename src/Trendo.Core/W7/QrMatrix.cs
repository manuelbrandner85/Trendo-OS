namespace Trendo.Core;

/// <summary>QR-Code (Byte-Modus, Fehlerkorrektur M, Version 1–6) für den Android-Kopplungstext. Keine Bibliothek, keine gespeicherten Schlüssel.</summary>
public static class QrMatrix
{
    public static bool[,] Encode(string text)
    {
        var data = System.Text.Encoding.UTF8.GetBytes(text);
        var version = VersionFor(data.Length);
        var bits = CodewordBits(data, version);
        return Place(version, bits);
    }

    public static int VersionFor(int byteLength)
    {
        for (var v = 1; v <= 6; v++)
        {
            var bits = 4 + 8 + byteLength * 8;
            if (bits <= DataBytes(v) * 8) return v;
        }
        throw new InvalidOperationException("QR-Inhalt passt in keinen Code.");
    }

    static readonly (int Ecc, int Blocks, int Data)[] Medium =
    [
        default,
        (10, 1, 16),
        (16, 1, 28),
        (26, 1, 44),
        (18, 2, 32),
        (24, 2, 43),
        (16, 4, 27),
    ];

    static int DataBytes(int version) => Medium[version].Blocks * Medium[version].Data;

    static bool[] CodewordBits(byte[] payload, int version)
    {
        var info = Medium[version];
        var cap = info.Blocks * info.Data;
        var bb = new List<bool>();
        void Add(int value, int len) { for (var i = len - 1; i >= 0; i--) bb.Add(((value >> i) & 1) != 0); }
        Add(0b0100, 4);
        Add(payload.Length, 8);
        foreach (var b in payload) Add(b, 8);
        var term = Math.Min(4, cap * 8 - bb.Count);
        if (term > 0) Add(0, term);
        while (bb.Count % 8 != 0) bb.Add(false);
        var bytes = new List<byte>();
        for (var i = 0; i < bb.Count; i += 8)
        {
            var v = 0;
            for (var b = 0; b < 8; b++) if (bb[i + b]) v |= 0x80 >> b;
            bytes.Add((byte)v);
        }
        for (var pad = 0; bytes.Count < cap; pad++) bytes.Add((byte)(pad % 2 == 0 ? 0xEC : 0x11));
        var divisor = Divisor(info.Ecc);
        var blocks = new byte[info.Blocks][];
        var ecc = new byte[info.Blocks][];
        for (var i = 0; i < info.Blocks; i++)
        {
            blocks[i] = bytes.GetRange(i * info.Data, info.Data).ToArray();
            ecc[i] = Remainder(blocks[i], divisor);
        }
        var all = new List<byte>();
        for (var i = 0; i < info.Data; i++)
            for (var b = 0; b < info.Blocks; b++) all.Add(blocks[b][i]);
        for (var i = 0; i < info.Ecc; i++)
            for (var b = 0; b < info.Blocks; b++) all.Add(ecc[b][i]);
        var bits = new bool[all.Count * 8];
        for (var i = 0; i < all.Count; i++)
            for (var b = 0; b < 8; b++) bits[i * 8 + b] = ((all[i] >> (7 - b)) & 1) != 0;
        return bits;
    }

    static readonly byte[] Exp = new byte[512];
    static readonly byte[] Log = new byte[256];

    static QrMatrix()
    {
        byte x = 1;
        for (var i = 0; i < 255; i++)
        {
            Exp[i] = x;
            Log[x] = (byte)i;
            var next = x << 1;
            if (next >= 256) next ^= 0x11D;
            x = (byte)next;
        }
        Array.Copy(Exp, 0, Exp, 255, 255);
    }

    static byte Mul(byte a, byte b) => a == 0 || b == 0 ? (byte)0 : Exp[Log[a] + Log[b]];

    static byte[] Divisor(int degree)
    {
        var result = new byte[degree];
        result[degree - 1] = 1;
        byte root = 1;
        for (var i = 0; i < degree; i++)
        {
            for (var j = 0; j < result.Length; j++)
            {
                result[j] = Mul(result[j], root);
                if (j + 1 < result.Length) result[j] ^= result[j + 1];
            }
            root = Mul(root, 0x02);
        }
        return result;
    }

    static byte[] Remainder(byte[] data, byte[] divisor)
    {
        var result = new byte[divisor.Length];
        foreach (var b in data)
        {
            var factor = (result[0] ^ b) & 0xFF;
            Array.Copy(result, 1, result, 0, result.Length - 1);
            result[^1] = 0;
            for (var i = 0; i < result.Length; i++) result[i] ^= Mul(divisor[i], (byte)factor);
        }
        return result;
    }

    static readonly int[][] Align =
    [
        [], [], [6, 18], [6, 22], [6, 26], [6, 30], [6, 34],
    ];

    static bool[,] Place(int version, bool[] bits)
    {
        var size = 17 + 4 * version;
        var dark = new bool[size * size];
        var fn = new bool[size * size];
        int At(int x, int y) => y * size + x;
        void Mark(int x, int y, bool on)
        {
            if ((uint)x >= (uint)size || (uint)y >= (uint)size) return;
            dark[At(x, y)] = on;
            fn[At(x, y)] = true;
        }
        void Finder(int x0, int y0)
        {
            for (var dy = -1; dy <= 7; dy++)
                for (var dx = -1; dx <= 7; dx++)
                {
                    var on = dx is >= 0 and <= 6 && dy is >= 0 and <= 6 && (dx == 0 || dx == 6 || dy == 0 || dy == 6 || (dx is >= 2 and <= 4 && dy is >= 2 and <= 4));
                    Mark(x0 + dx, y0 + dy, on);
                }
        }
        Finder(0, 0);
        Finder(size - 7, 0);
        Finder(0, size - 7);
        for (var i = 0; i < size; i++)
        {
            if (!fn[At(i, 6)]) Mark(i, 6, i % 2 == 0);
            if (!fn[At(6, i)]) Mark(6, i, i % 2 == 0);
        }
        var align = Align[version];
        for (var row = 0; row < align.Length; row++)
            for (var col = 0; col < align.Length; col++)
            {
                // Die drei Ecken liegen im Suchmuster und entfallen. Muster auf der Taktspur bleiben.
                if ((row == 0 && col == 0) || (row == 0 && col == align.Length - 1) || (row == align.Length - 1 && col == 0)) continue;
                var r = align[row];
                var c = align[col];
                for (var dy = -2; dy <= 2; dy++)
                    for (var dx = -2; dx <= 2; dx++)
                        Mark(c + dx, r + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
            }
        Mark(8, size - 8, true);
        foreach (var (x, y, _) in FormatSpots(size)) Mark(x, y, false);

        var iBit = 0;
        for (var right = size - 1; right >= 1; right -= 2)
        {
            if (right == 6) right = 5;
            for (var vert = 0; vert < size; vert++)
            {
                for (var j = 0; j < 2; j++)
                {
                    var x = right - j;
                    var upward = ((right + 1) & 2) == 0;
                    var y = upward ? size - 1 - vert : vert;
                    if (fn[At(x, y)] || iBit >= bits.Length) continue;
                    dark[At(x, y)] = bits[iBit++];
                }
            }
        }

        bool[]? best = null;
        var bestPen = int.MaxValue;
        for (var mask = 0; mask < 8; mask++)
        {
            var trial = (bool[])dark.Clone();
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                    if (!fn[At(x, y)] && Mask(mask, y, x)) trial[At(x, y)] ^= true;
            var format = FormatBits(mask);
            foreach (var (x, y, bit) in FormatSpots(size)) trial[At(x, y)] = ((format >> bit) & 1) != 0;
            var pen = Penalty(trial, size);
            if (pen < bestPen) { bestPen = pen; best = trial; }
        }
        var m = new bool[size, size];
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++) m[y, x] = best![At(x, y)];
        return m;
    }

    static IEnumerable<(int X, int Y, int Bit)> FormatSpots(int size)
    {
        (int x, int y)[] first = [(8, 0), (8, 1), (8, 2), (8, 3), (8, 4), (8, 5), (8, 7), (8, 8), (7, 8), (5, 8), (4, 8), (3, 8), (2, 8), (1, 8), (0, 8)];
        for (var i = 0; i < 15; i++) yield return (first[i].x, first[i].y, i);
        for (var i = 0; i < 8; i++) yield return (size - 1 - i, 8, i);
        for (var i = 8; i < 15; i++) yield return (8, size - 15 + i, i);
    }

    /// <summary>Fehlerkorrektur M hat die Format-Bits 00. Maske 0 ergibt deshalb genau die Maske 0x5412.</summary>
    public static int FormatBits(int mask)
    {
        var data = mask & 7;
        var rem = data;
        for (var i = 0; i < 10; i++)
            rem = (rem << 1) ^ (((rem >> 9) & 1) * 0x537);
        return ((data << 10) | (rem & 0x3FF)) ^ 0x5412;
    }

    static bool Mask(int mask, int y, int x) => mask switch
    {
        0 => (y + x) % 2 == 0,
        1 => y % 2 == 0,
        2 => x % 3 == 0,
        3 => (y + x) % 3 == 0,
        4 => (y / 2 + x / 3) % 2 == 0,
        5 => (y * x) % 2 + (y * x) % 3 == 0,
        6 => ((y * x) % 2 + (y * x) % 3) % 2 == 0,
        _ => ((y + x) % 2 + (y * x) % 3) % 2 == 0,
    };

    static int Penalty(bool[] m, int size)
    {
        bool At(int x, int y) => m[y * size + x];
        var pen = 0;
        for (var pass = 0; pass < 2; pass++)
            for (var i = 0; i < size; i++)
            {
                var run = 0;
                var color = false;
                for (var j = 0; j < size; j++)
                {
                    var c = pass == 0 ? At(j, i) : At(i, j);
                    if (j == 0 || c != color) { color = c; run = 1; }
                    else { run++; if (run == 5) pen += 3; else if (run > 5) pen++; }
                }
            }
        for (var y = 0; y < size - 1; y++)
            for (var x = 0; x < size - 1; x++)
                if (At(x, y) == At(x + 1, y) && At(x, y) == At(x, y + 1) && At(x, y) == At(x + 1, y + 1)) pen += 3;
        bool[] Pat(string s) => s.Select(c => c == '1').ToArray();
        var p1 = Pat("10111010000");
        var p2 = Pat("00001011101");
        for (var pass = 0; pass < 2; pass++)
            for (var i = 0; i < size; i++)
                for (var j = 0; j <= size - 11; j++)
                {
                    bool Hit(bool[] p)
                    {
                        for (var k = 0; k < 11; k++)
                            if ((pass == 0 ? At(j + k, i) : At(i, j + k)) != p[k]) return false;
                        return true;
                    }
                    if (Hit(p1) || Hit(p2)) pen += 40;
                }
        var dark = m.Count(x => x);
        pen += Math.Abs(dark * 100 / (size * size) - 50) / 5 * 10;
        return pen;
    }
}
