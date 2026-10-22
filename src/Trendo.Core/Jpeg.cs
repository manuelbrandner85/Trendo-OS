namespace Trendo.Core;

/// <summary>Baseline-JPEG (4:4:4, eine Abtastung, Standard-Huffman). Ohne Zusatzpaket.</summary>
public static class Jpeg
{
    static readonly byte[] Zig =
    [
        0, 1, 8, 16, 9, 2, 3, 10, 17, 24, 32, 25, 18, 11, 4, 5,
        12, 19, 26, 33, 40, 48, 41, 34, 27, 20, 13, 6, 7, 14, 21, 28,
        35, 42, 49, 56, 57, 50, 43, 36, 29, 22, 15, 23, 30, 37, 44, 51,
        58, 59, 52, 45, 38, 31, 39, 46, 53, 60, 61, 54, 47, 55, 62, 63,
    ];

    static readonly byte[] Qy =
    [
        16, 11, 10, 16, 24, 40, 51, 61, 12, 12, 14, 19, 26, 58, 60, 55,
        14, 13, 16, 24, 40, 57, 69, 56, 14, 17, 22, 29, 51, 87, 80, 62,
        18, 22, 37, 56, 68, 109, 103, 77, 24, 35, 55, 64, 81, 104, 113, 92,
        49, 64, 78, 87, 103, 121, 120, 101, 72, 92, 95, 98, 112, 100, 103, 99,
    ];

    static readonly byte[] Qc =
    [
        17, 18, 24, 47, 99, 99, 99, 99, 18, 21, 26, 66, 99, 99, 99, 99,
        24, 26, 56, 99, 99, 99, 99, 99, 47, 66, 99, 99, 99, 99, 99, 99,
        99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99,
        99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99, 99,
    ];

    static readonly byte[] BitsDcY = [0, 1, 5, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0];
    static readonly byte[] ValDcY = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11];
    static readonly byte[] BitsDcC = [0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0];
    static readonly byte[] ValDcC = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11];
    static readonly byte[] BitsAcY = [0, 2, 1, 3, 3, 2, 4, 3, 5, 5, 4, 4, 0, 0, 1, 0x7D];
    static readonly byte[] ValAcY =
    [
        0x01, 0x02, 0x03, 0x00, 0x04, 0x11, 0x05, 0x12, 0x21, 0x31, 0x41, 0x06, 0x13, 0x51, 0x61, 0x07,
        0x22, 0x71, 0x14, 0x32, 0x81, 0x91, 0xA1, 0x08, 0x23, 0x42, 0xB1, 0xC1, 0x15, 0x52, 0xD1, 0xF0,
        0x24, 0x33, 0x62, 0x72, 0x82, 0x09, 0x0A, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x25, 0x26, 0x27, 0x28,
        0x29, 0x2A, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3A, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49,
        0x4A, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59, 0x5A, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69,
        0x6A, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7A, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89,
        0x8A, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9A, 0xA2, 0xA3, 0xA4, 0xA5, 0xA6, 0xA7,
        0xA8, 0xA9, 0xAA, 0xB2, 0xB3, 0xB4, 0xB5, 0xB6, 0xB7, 0xB8, 0xB9, 0xBA, 0xC2, 0xC3, 0xC4, 0xC5,
        0xC6, 0xC7, 0xC8, 0xC9, 0xCA, 0xD2, 0xD3, 0xD4, 0xD5, 0xD6, 0xD7, 0xD8, 0xD9, 0xDA, 0xE1, 0xE2,
        0xE3, 0xE4, 0xE5, 0xE6, 0xE7, 0xE8, 0xE9, 0xEA, 0xF1, 0xF2, 0xF3, 0xF4, 0xF5, 0xF6, 0xF7, 0xF8,
        0xF9, 0xFA,
    ];
    static readonly byte[] BitsAcC = [0, 2, 1, 2, 4, 4, 3, 4, 7, 5, 4, 4, 0, 1, 2, 0x77];
    static readonly byte[] ValAcC =
    [
        0x00, 0x01, 0x02, 0x03, 0x11, 0x04, 0x05, 0x21, 0x31, 0x06, 0x12, 0x41, 0x51, 0x07, 0x61, 0x71,
        0x13, 0x22, 0x32, 0x81, 0x08, 0x14, 0x42, 0x91, 0xA1, 0xB1, 0xC1, 0x09, 0x23, 0x33, 0x52, 0xF0,
        0x15, 0x62, 0x72, 0xD1, 0x0A, 0x16, 0x24, 0x34, 0xE1, 0x25, 0xF1, 0x17, 0x18, 0x19, 0x1A, 0x26,
        0x27, 0x28, 0x29, 0x2A, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3A, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48,
        0x49, 0x4A, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59, 0x5A, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68,
        0x69, 0x6A, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7A, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87,
        0x88, 0x89, 0x8A, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9A, 0xA2, 0xA3, 0xA4, 0xA5,
        0xA6, 0xA7, 0xA8, 0xA9, 0xAA, 0xB2, 0xB3, 0xB4, 0xB5, 0xB6, 0xB7, 0xB8, 0xB9, 0xBA, 0xC2, 0xC3,
        0xC4, 0xC5, 0xC6, 0xC7, 0xC8, 0xC9, 0xCA, 0xD2, 0xD3, 0xD4, 0xD5, 0xD6, 0xD7, 0xD8, 0xD9, 0xDA,
        0xE2, 0xE3, 0xE4, 0xE5, 0xE6, 0xE7, 0xE8, 0xE9, 0xEA, 0xF2, 0xF3, 0xF4, 0xF5, 0xF6, 0xF7, 0xF8,
        0xF9, 0xFA,
    ];

    public static int TabellenSumme(byte[] bits) => bits.Sum(b => b);

    public static byte[] Encode(RgbImage img, int quality = 85)
    {
        if (img.Width <= 0 || img.Height <= 0 || img.Rgb.Length < img.Width * img.Height * 3)
            throw new ArgumentException("Das Bild ist leer.");
        quality = Math.Clamp(quality, 1, 100);
        var qy = Skala(Qy, quality);
        var qc = Skala(Qc, quality);
        var (dcY, acY) = Tabellen(BitsDcY, ValDcY, BitsAcY, ValAcY);
        var (dcC, acC) = Tabellen(BitsDcC, ValDcC, BitsAcC, ValAcC);
        var w = img.Width; var h = img.Height;
        var mw = (w + 7) & ~7; var mh = (h + 7) & ~7;
        var buf = new List<byte>();
        var bits = new BitSchreiber(buf);
        int predY = 0, predCb = 0, predCr = 0;
        for (var my = 0; my < mh; my += 8)
            for (var mx = 0; mx < mw; mx += 8)
            {
                var yb = new double[64]; var cb = new double[64]; var cr = new double[64];
                for (var yy = 0; yy < 8; yy++)
                    for (var xx = 0; xx < 8; xx++)
                    {
                        int sx = Math.Min(w - 1, mx + xx), sy = Math.Min(h - 1, my + yy);
                        int p = (sy * w + sx) * 3;
                        double r = img.Rgb[p], g = img.Rgb[p + 1], b = img.Rgb[p + 2];
                        var i = yy * 8 + xx;
                        yb[i] = 0.299 * r + 0.587 * g + 0.114 * b - 128;
                        cb[i] = 128 - 0.168736 * r - 0.331264 * g + 0.5 * b - 128;
                        cr[i] = 128 + 0.5 * r - 0.418688 * g - 0.081312 * b - 128;
                    }
                predY = Block(yb, qy, dcY, acY, predY, bits);
                predCb = Block(cb, qc, dcC, acC, predCb, bits);
                predCr = Block(cr, qc, dcC, acC, predCr, bits);
            }
        bits.Abschluss();
        var ms = new MemoryStream();
        void W(byte[] a) => ms.Write(a);
        W([0xFF, 0xD8]);
        W([0xFF, 0xE0, 0, 16, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1, 1, 0, 0, 1, 0, 1, 0, 0]);
        Dqt(ms, 0, qy); Dqt(ms, 1, qc);
        Sof(ms, w, h);
        Dht(ms, 0x00, BitsDcY, ValDcY); Dht(ms, 0x10, BitsAcY, ValAcY);
        Dht(ms, 0x01, BitsDcC, ValDcC); Dht(ms, 0x11, BitsAcC, ValAcC);
        W([0xFF, 0xDA, 0, 12, 3, 1, 0x00, 2, 0x11, 3, 0x11, 0, 63, 0]);
        ms.Write(buf.ToArray());
        W([0xFF, 0xD9]);
        return ms.ToArray();
    }

    static int Block(double[] f, int[] q, (int Code, int Len)[] dc, (int Code, int Len)[] ac, int pred, BitSchreiber bits)
    {
        var z = new int[64];
        for (var v = 0; v < 8; v++)
            for (var u = 0; u < 8; u++)
            {
                double s = 0;
                var cu = u == 0 ? 1 / Math.Sqrt(2) : 1;
                var cv = v == 0 ? 1 / Math.Sqrt(2) : 1;
                for (var y = 0; y < 8; y++)
                    for (var x = 0; x < 8; x++)
                        s += f[y * 8 + x] * Math.Cos((2 * x + 1) * u * Math.PI / 16) * Math.Cos((2 * y + 1) * v * Math.PI / 16);
                z[v * 8 + u] = (int)Math.Round(s * 0.25 * cu * cv / q[v * 8 + u]);
            }
        var zz = new int[64];
        for (var i = 0; i < 64; i++) zz[i] = z[Zig[i]];
        var diff = zz[0] - pred;
        Symbol(diff, dc, bits);
        var run = 0;
        for (var i = 1; i < 64; i++)
        {
            if (zz[i] == 0) { run++; continue; }
            while (run >= 16) { bits.Write(ac[0xF0].Code, ac[0xF0].Len); run -= 16; }
            var cat = Kategorie(zz[i]);
            var sym = (run << 4) | cat;
            bits.Write(ac[sym].Code, ac[sym].Len);
            bits.Write(Amplitude(zz[i], cat), cat);
            run = 0;
        }
        if (run > 0) bits.Write(ac[0].Code, ac[0].Len);
        return zz[0];
    }

    static void Symbol(int diff, (int Code, int Len)[] dc, BitSchreiber bits)
    {
        var cat = Kategorie(diff);
        bits.Write(dc[cat].Code, dc[cat].Len);
        if (cat > 0) bits.Write(Amplitude(diff, cat), cat);
    }

    static int Kategorie(int v)
    {
        v = Math.Abs(v);
        var n = 0;
        while (v > 0) { v >>= 1; n++; }
        return n;
    }

    static int Amplitude(int v, int cat) => v < 0 ? v + ((1 << cat) - 1) : v;

    static int[] Skala(byte[] basis, int quality)
    {
        var scale = quality < 50 ? 5000 / quality : 200 - 2 * quality;
        var q = new int[64];
        for (var i = 0; i < 64; i++) q[i] = Math.Clamp((basis[i] * scale + 50) / 100, 1, 255);
        return q;
    }

    static ((int Code, int Len)[] Dc, (int Code, int Len)[] Ac) Tabellen(byte[] bitsDc, byte[] valDc, byte[] bitsAc, byte[] valAc) =>
        (Baum(bitsDc, valDc), Baum(bitsAc, valAc));

    static (int Code, int Len)[] Baum(byte[] bits, byte[] vals)
    {
        var map = new (int, int)[256];
        var code = 0; var k = 0;
        for (var len = 1; len <= 16; len++)
        {
            for (var j = 0; j < bits[len - 1]; j++) map[vals[k++]] = (code++, len);
            code <<= 1;
        }
        return map;
    }

    static void Dqt(Stream ms, int id, int[] natural)
    {
        ms.WriteByte(0xFF); ms.WriteByte(0xDB); ms.WriteByte(0); ms.WriteByte(67); ms.WriteByte((byte)id);
        for (var i = 0; i < 64; i++) ms.WriteByte((byte)natural[Zig[i]]);
    }

    static void Sof(Stream ms, int w, int h)
    {
        ms.WriteByte(0xFF); ms.WriteByte(0xC0);
        Be(ms, 17); ms.WriteByte(8); Be(ms, h); Be(ms, w); ms.WriteByte(3);
        ms.Write([1, 0x11, 0, 2, 0x11, 1, 3, 0x11, 1]);
    }

    static void Dht(Stream ms, int klasseId, byte[] bits, byte[] vals)
    {
        ms.WriteByte(0xFF); ms.WriteByte(0xC4);
        Be(ms, 2 + 1 + 16 + vals.Length);
        ms.WriteByte((byte)klasseId);
        ms.Write(bits); ms.Write(vals);
    }

    static void Be(Stream ms, int v) { ms.WriteByte((byte)(v >> 8)); ms.WriteByte((byte)v); }

    sealed class BitSchreiber(List<byte> ziel)
    {
        long acc; int n;
        public void Write(int wert, int len)
        {
            if (len <= 0) return;
            acc = (acc << len) | (uint)wert;
            n += len;
            while (n >= 8)
            {
                n -= 8;
                var b = (byte)((acc >> n) & 0xFF);
                acc &= (1L << n) - 1;
                ziel.Add(b);
                if (b == 0xFF) ziel.Add(0);
            }
        }

        public void Abschluss()
        {
            if (n > 0) Write((1 << (8 - n)) - 1, 8 - n);
        }
    }
}
