namespace Trendo.Core;

/// <summary>Bild zuschneiden, drehen, Helligkeit, Kontrast, Text, speichern als PNG oder JPG.</summary>
public static class BildStudio
{
    public static RgbImage Zuschneiden(RgbImage img, int x, int y, int breite, int hoehe)
    {
        if (breite <= 0 || hoehe <= 0) throw new ArgumentException("Breite und Höhe müssen größer als 0 sein.");
        if (x < 0 || y < 0 || x + breite > img.Width || y + hoehe > img.Height)
            throw new ArgumentException("Der Ausschnitt liegt außerhalb des Bildes.");
        var rgb = new byte[breite * hoehe * 3];
        for (var row = 0; row < hoehe; row++)
            Array.Copy(img.Rgb, ((y + row) * img.Width + x) * 3, rgb, row * breite * 3, breite * 3);
        return new(breite, hoehe, rgb);
    }

    /// <summary>90 Grad im Uhrzeigersinn.</summary>
    public static RgbImage Drehen90(RgbImage img)
    {
        var rgb = new byte[img.Rgb.Length];
        for (var y = 0; y < img.Height; y++)
            for (var x = 0; x < img.Width; x++)
            {
                var nx = img.Height - 1 - y; var ny = x;
                var s = (y * img.Width + x) * 3; var d = (ny * img.Height + nx) * 3;
                rgb[d] = img.Rgb[s]; rgb[d + 1] = img.Rgb[s + 1]; rgb[d + 2] = img.Rgb[s + 2];
            }
        return new(img.Height, img.Width, rgb);
    }

    /// <summary>Helligkeit und Kontrast in Prozent. 0 lässt den Wert unverändert. Kontrast -100 ist grau, +100 doppelt so hart.</summary>
    public static RgbImage Helligkeit(RgbImage img, double helligkeit, double kontrast)
    {
        var k = (100 + kontrast) / 100.0;
        var add = helligkeit * 2.55;
        var rgb = new byte[img.Rgb.Length];
        for (var i = 0; i < rgb.Length; i++)
        {
            var v = (img.Rgb[i] - 128) * k + 128 + add;
            rgb[i] = (byte)Math.Clamp((int)Math.Round(v), 0, 255);
        }
        return new(img.Width, img.Height, rgb);
    }

    public static RgbImage Text(RgbImage img, string text, int x, int y, byte rot = 255, byte gruen = 255, byte blau = 255)
    {
        var rgb = (byte[])img.Rgb.Clone();
        var cursor = x;
        foreach (var ch in text)
        {
            var g = Glyphe(ch);
            for (var row = 0; row < 7; row++)
                for (var col = 0; col < 5; col++)
                {
                    if (g[row][col] != '#') continue;
                    for (var sy = 0; sy < 3; sy++)
                        for (var sx = 0; sx < 3; sx++)
                        {
                            int px = cursor + col * 3 + sx, py = y + row * 3 + sy;
                            if (px < 0 || py < 0 || px >= img.Width || py >= img.Height) continue;
                            var p = (py * img.Width + px) * 3;
                            rgb[p] = rot; rgb[p + 1] = gruen; rgb[p + 2] = blau;
                        }
                }
            cursor += 6 * 3;
        }
        return new(img.Width, img.Height, rgb);
    }

    public static void SavePng(RgbImage img, string path) => File.WriteAllBytes(path, VisionPlus.EncodePng(img));

    public static void SaveJpg(RgbImage img, string path, int quality = 85) => File.WriteAllBytes(path, Jpeg.Encode(img, quality));

    public static RgbImage Oeffnen(string path)
    {
        if (!File.Exists(path)) throw new IOException("Diese Datei gibt es nicht.");
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is ".png" or ".bmp") return Vision.Load(path);
        if (ext is ".jpg" or ".jpeg" or ".webp" or ".gif")
        {
            var ff = Cli.Find("ffmpeg");
            if (ff is null) throw new InvalidOperationException("JPG und ähnliche Formate lesen braucht FFmpeg. PNG und BMP gehen auch ohne.");
            var png = Path.Combine(Path.GetTempPath(), "trendo-bild-" + Guid.NewGuid().ToString("N") + ".png");
            try
            {
                var r = Cli.Run(ff, ["-y", "-i", path, png], null, 60000);
                if (!r.Ok || !File.Exists(png)) throw new InvalidOperationException("Das Bild ließ sich nicht lesen.");
                return Vision.Load(png);
            }
            finally { if (File.Exists(png)) File.Delete(png); }
        }
        throw new InvalidOperationException("Unterstützt werden PNG, BMP, JPG und – mit FFmpeg – WebP.");
    }

    static string[] Glyphe(char c)
    {
        var u = char.ToUpperInvariant(c);
        if (u is 'Ä') u = 'A'; if (u is 'Ö') u = 'O'; if (u is 'Ü') u = 'U'; if (c is 'ß') u = 'B';
        return Schrift.TryGetValue(u, out var g) ? g : Schrift['?'];
    }

    static readonly Dictionary<char, string[]> Schrift = Bau();

    static Dictionary<char, string[]> Bau()
    {
        var d = new Dictionary<char, string[]>();
        void G(char c, string rows) => d[c] = rows.Split(' ');
        G('A', ".###. #...# #...# ##### #...# #...# #...#");
        G('B', "####. #...# #...# ####. #...# #...# ####.");
        G('C', ".###. #...# #.... #.... #.... #...# .###.");
        G('D', "####. #...# #...# #...# #...# #...# ####.");
        G('E', "##### #.... #.... ####. #.... #.... #####");
        G('F', "##### #.... #.... ####. #.... #.... #....");
        G('G', ".###. #.... #.... #.### #...# #...# .###.");
        G('H', "#...# #...# #...# ##### #...# #...# #...#");
        G('I', "##### ..#.. ..#.. ..#.. ..#.. ..#.. #####");
        G('J', "..### ....# ....# ....# #...# #...# .###.");
        G('K', "#...# #..#. #.#.. ##... #.#.. #..#. #...#");
        G('L', "#.... #.... #.... #.... #.... #.... #####");
        G('M', "#...# ##.## #.#.# #...# #...# #...# #...#");
        G('N', "#...# ##..# #.#.# #..## #...# #...# #...#");
        G('O', ".###. #...# #...# #...# #...# #...# .###.");
        G('P', "####. #...# #...# ####. #.... #.... #....");
        G('Q', ".###. #...# #...# #...# #.#.# #..#. .##.#");
        G('R', "####. #...# #...# ####. #.#.. #..#. #...#");
        G('S', ".#### #.... #.... .###. ....# ....# ####.");
        G('T', "##### ..#.. ..#.. ..#.. ..#.. ..#.. ..#..");
        G('U', "#...# #...# #...# #...# #...# #...# .###.");
        G('V', "#...# #...# #...# #...# #...# .#.#. ..#..");
        G('W', "#...# #...# #...# #.#.# #.#.# ##.## #...#");
        G('X', "#...# #...# .#.#. ..#.. .#.#. #...# #...#");
        G('Y', "#...# #...# .#.#. ..#.. ..#.. ..#.. ..#..");
        G('Z', "##### ....# ...#. ..#.. .#... #.... #####");
        G('0', ".###. #..## #.#.# ##..# #...# #...# .###.");
        G('1', "..#.. .##.. ..#.. ..#.. ..#.. ..#.. .###.");
        G('2', ".###. #...# ....# ..##. .#... #.... #####");
        G('3', "####. ....# ....# .###. ....# ....# ####.");
        G('4', "...#. ..##. .#.#. #..#. ##### ...#. ...#.");
        G('5', "##### #.... ####. ....# ....# #...# .###.");
        G('6', ".##.. #.... #.... ####. #...# #...# .###.");
        G('7', "##### ....# ...#. ..#.. .#... .#... .#...");
        G('8', ".###. #...# #...# .###. #...# #...# .###.");
        G('9', ".###. #...# #...# .#### ....# ....# .##..");
        G(' ', "..... ..... ..... ..... ..... ..... .....");
        G('?', ".###. #...# ....# ..##. ..#.. ..... ..#..");
        G('-', "..... ..... ..... .###. ..... ..... .....");
        G('.', "..... ..... ..... ..... ..... ..#.. ..#..");
        return d;
    }
}
