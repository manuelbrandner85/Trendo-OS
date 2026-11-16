using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Trendo.Core.Kreativ;
using Trendo.Core.Media;

namespace Trendo.Core.Business;

// ===================== STUDIO-PRODUCTPHOTO-01 Produktfotos für Shops (T-0677) =====================
// Clean-Room nach dem Bedienkonzept von Stapel-Fotowerkzeugen für Händler (Photoroom, remove.bg, Pixelcut): viele Fotos auf einmal freistellen,
// auf weißen oder farbigen Grund setzen, Schatten, mittig mit Rand auf die Marktplatz-Vorgabe zuschneiden, Licht/Weißabgleich, Wasserzeichen,
// Umbenennen nach Artikelnummer, Export-Paket und Prüfung gegen die Vorgabe. Kein fremder Code. Pixel als RGBA (nicht vormultipliziert).

/// <summary>Bildvorgabe eines Marktplatzes. Stand: siehe <see cref="ProduktFotos.Stand"/> – Vorgaben ändern sich, darum steht die Quelle dabei.</summary>
public sealed record FotoVorgabe(
    string Id, string Name, int Breite, int Hoehe, int MinLangeSeite, int MinKurzeSeite, int MaxSeite, long MaxBytes,
    bool WeissPflicht, double MinFuellung, bool WasserzeichenErlaubt, string[] Formate, string Hinweis, string Quelle);

/// <summary>Einstellungen für die Stapel-Bearbeitung.</summary>
public sealed class FotoEinstellung
{
    public string Markt { get; set; } = "amazon";
    /// <summary>Hintergrundfarbe als #RRGGBB; „transparent“ = ohne Hintergrund (nur PNG).</summary>
    public string Hintergrund { get; set; } = "#FFFFFF";
    /// <summary>aus, weich (Kontaktschatten) oder spiegel (Spiegelung am Boden).</summary>
    public string Schatten { get; set; } = "weich";
    /// <summary>Rand rundum in Prozent der kürzeren Seite (0–30).</summary>
    public double RandProzent { get; set; } = 5;
    /// <summary>Freistellen: erlaubte Abweichung vom Hintergrund je Kanal (0–120).</summary>
    public int Toleranz { get; set; } = 28;
    public bool Freistellen { get; set; } = true;
    public bool AutoLicht { get; set; } = true;
    public bool Weissabgleich { get; set; } = true;
    public string Wasserzeichen { get; set; } = "";
    public double WasserzeichenDeckkraft { get; set; } = 0.35;
    /// <summary>unten-rechts, unten-links, mitte, oben-rechts, oben-links.</summary>
    public string WasserzeichenOrt { get; set; } = "unten-rechts";
    /// <summary>Dateiname: {nr} Artikelnummer, {n} laufende Nummer, {variante} MAIN/PT01…, {markt}, {datum}.</summary>
    public string Muster { get; set; } = "{nr}_{n}";
    public string Format { get; set; } = "jpg";
}

public sealed class ProduktFoto
{
    public string Id { get; set; } = "";
    public string Datei { get; set; } = "";
    public string Artikelnummer { get; set; } = "";
    /// <summary>Hauptbild des Artikels (auf Amazon gelten dafür die strengen Regeln).</summary>
    public bool Hauptbild { get; set; } = true;
    public string Notiz { get; set; } = "";
}

public sealed class FotoStapel
{
    public List<ProduktFoto> Fotos { get; set; } = [];
    public FotoEinstellung Einstellung { get; set; } = new();
    static readonly JsonSerializerOptions O = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public JsonObject ToJson() => JsonSerializer.SerializeToNode(this, O)!.AsObject();
    public static FotoStapel FromJson(JsonNode? n)
    {
        if (n is not JsonObject o) return new();
        try { return o.Deserialize<FotoStapel>(O) ?? new(); } catch (JsonException) { return new(); }
    }
}

/// <summary>Ergebnis des Freistellens: Bild mit Transparenz, erkannte Hintergrundfarbe, wie einheitlich der Rand war.</summary>
public sealed record Freistellung(Pixmap Bild, (byte R, byte G, byte B) Hintergrund, double Einheitlich, double EntferntAnteil, bool Komplex, string Hinweis);

/// <summary>Ein Prüfpunkt gegen die Vorgabe. <paramref name="Pflicht"/> = Regel des Marktplatzes, sonst Empfehlung.</summary>
public sealed record FotoBefund(string Punkt, bool Ok, string Text, bool Pflicht);

/// <summary>Fertiges Bild mit Kennzahlen.</summary>
public sealed record FotoErgebnis(Pixmap Bild, double Fuellung, bool Vergroessert, Freistellung? Frei);

public static partial class ProduktFotos
{
    public const string Stand = "15.11.2026";

    /// <summary>Vorgaben der Marktplätze (Stand 15.11.2026, aus den Hilfeseiten und Händler-Leitfäden – vor dem Hochladen in der Hilfe des Marktplatzes gegenprüfen).</summary>
    public static readonly FotoVorgabe[] Vorgaben =
    [
        new("amazon", "Amazon (Hauptbild)", 2000, 2000, 1000, 500, 10000, 10L * 1024 * 1024, true, 0.85, false, ["jpg", "png", "tif", "gif"],
            "Reinweißer Hintergrund (RGB 255/255/255), Produkt füllt mindestens 85 % des Bildes, längste Seite ab 1000 px (für Zoom), keine Texte, Logos oder Wasserzeichen.",
            "Amazon Seller Central, Produktbild-Richtlinien; Händler-Leitfäden 2026"),
        new("etsy", "Etsy", 3000, 2250, 2000, 2000, 10000, 1L * 1024 * 1024, false, 0, true, ["jpg", "png", "gif"],
            "Empfohlen 3000 × 2250 px (4:3), kürzeste Seite ab 2000 px; Dateien unter 1 MB laden zuverlässig hoch. Hintergrund frei wählbar.",
            "Etsy Hilfe „Fotos für Angebote“; Händler-Leitfäden 2026"),
        new("ebay", "eBay", 1600, 1600, 500, 500, 9000, 12L * 1024 * 1024, false, 0, false, ["jpg", "png", "gif", "tif", "webp"],
            "Längste Seite mindestens 500 px, empfohlen 1600 px; keine Rahmen, Texte oder Wasserzeichen im Bild.",
            "eBay Hilfe „Fotos hinzufügen“"),
        new("shopify", "Shopify", 2048, 2048, 800, 800, 5000, 20L * 1024 * 1024, false, 0, true, ["jpg", "png", "gif", "webp"],
            "Empfohlen 2048 × 2048 px (quadratisch), höchstens 5000 × 5000 px und unter 20 MB.",
            "Shopify Hilfe „Produktmedien“"),
        new("frei", "Eigener Shop / Social Media", 1600, 1600, 600, 600, 10000, 20L * 1024 * 1024, false, 0, true, ["jpg", "png", "webp"],
            "Keine feste Vorgabe – quadratisch 1600 px passt für die meisten Shops und Netzwerke.", "eigene Empfehlung"),
    ];

    public static FotoVorgabe Vorgabe(string? id) => Vorgaben.FirstOrDefault(v => v.Id == id) ?? Vorgaben[0];

    // ---------- Farben ----------

    public static bool IsHex(string? s) => s is not null && HexRx().IsMatch(s.Trim());
    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")] private static partial Regex HexRx();
    public static (byte R, byte G, byte B) Rgb(string hex)
    {
        if (!IsHex(hex)) throw new FormatException($"„{hex}“ ist keine Farbe – bitte z. B. #FFFFFF.");
        var h = hex.Trim();
        return (Convert.ToByte(h.Substring(1, 2), 16), Convert.ToByte(h.Substring(3, 2), 16), Convert.ToByte(h.Substring(5, 2), 16));
    }

    static int Dist(byte[] p, int i, (byte R, byte G, byte B) c) => Math.Max(Math.Abs(p[i] - c.R), Math.Max(Math.Abs(p[i + 1] - c.G), Math.Abs(p[i + 2] - c.B)));

    // ---------- Freistellen ----------

    /// <summary>Hintergrund vom Rand her entfernen: Randfarbe (Median) bestimmen, zusammenhängende Fläche innerhalb der Toleranz transparent machen,
    /// Kante weich auslaufen lassen und Krümel entfernen. Funktioniert gut bei einfarbigem Hintergrund (Fotokarton, Lichtzelt);
    /// bei unruhigem Hintergrund meldet es <see cref="Freistellung.Komplex"/> – dann den KI-Freisteller nutzen.</summary>
    public static Freistellung Freistellen(Pixmap src, int toleranz = 28)
    {
        toleranz = Math.Clamp(toleranz, 0, 120);
        int w = src.W, h = src.H; var px = (byte[])src.Px.Clone();
        // Randpixel sammeln (höchstens ~4000)
        var border = new List<int>();
        var step = Math.Max(1, (2 * (w + h)) / 4000);
        for (var x = 0; x < w; x += step) { border.Add(x); border.Add((h - 1) * w + x); }
        for (var y = 0; y < h; y += step) { border.Add(y * w); border.Add(y * w + w - 1); }
        var opaque = border.Where(i => px[i * 4 + 3] > 200).ToList();
        if (opaque.Count < border.Count / 2)
        {
            // schon freigestellt (PNG mit Transparenz)
            var removed0 = Enumerable.Range(0, w * h).Count(i => px[i * 4 + 3] < 16) / (double)(w * h);
            return new(new Pixmap(w, h, px), (255, 255, 255), 1, removed0, false, "Das Bild hat schon einen durchsichtigen Hintergrund.");
        }
        static byte Med(List<byte> l) { l.Sort(); return l[l.Count / 2]; }
        var bg = (Med(opaque.Select(i => px[i * 4]).ToList()), Med(opaque.Select(i => px[i * 4 + 1]).ToList()), Med(opaque.Select(i => px[i * 4 + 2]).ToList()));
        var uniform = opaque.Count(i => Dist(px, i * 4, bg) <= toleranz) / (double)opaque.Count;

        // Flutfüllung vom Rand
        var isBg = new bool[w * h]; var q = new Queue<int>();
        void Seed(int i) { if (!isBg[i] && (px[i * 4 + 3] == 0 || Dist(px, i * 4, bg) <= toleranz)) { isBg[i] = true; q.Enqueue(i); } }
        for (var x = 0; x < w; x++) { Seed(x); Seed((h - 1) * w + x); }
        for (var y = 0; y < h; y++) { Seed(y * w); Seed(y * w + w - 1); }
        while (q.Count > 0)
        {
            var i = q.Dequeue(); int x = i % w, y = i / w;
            if (x > 0) Seed(i - 1); if (x < w - 1) Seed(i + 1); if (y > 0) Seed(i - w); if (y < h - 1) Seed(i + w);
        }
        // Kante: Vordergrund neben Hintergrund läuft je nach Farbabstand weich aus
        var soft = Math.Max(6, toleranz);
        var alpha = new byte[w * h];
        for (var i = 0; i < w * h; i++)
        {
            if (isBg[i]) { alpha[i] = 0; continue; }
            int x = i % w, y = i / w;
            var edge = (x > 0 && isBg[i - 1]) || (x < w - 1 && isBg[i + 1]) || (y > 0 && isBg[i - w]) || (y < h - 1 && isBg[i + w]);
            var a = (int)px[i * 4 + 3];
            if (edge) a = Math.Min(a, (int)Math.Round(255.0 * Math.Clamp((Dist(px, i * 4, bg) - toleranz) / (double)soft, 0.35, 1)));
            alpha[i] = (byte)a;
        }
        // Krümel: nur Teile behalten, die mindestens 1 % des größten Teils groß sind
        var label = new int[w * h]; var sizes = new List<int> { 0 }; var st = new Stack<int>();
        for (var i = 0; i < w * h; i++)
        {
            if (alpha[i] == 0 || label[i] != 0) continue;
            var id = sizes.Count; sizes.Add(0); label[i] = id; st.Push(i);
            while (st.Count > 0)
            {
                var j = st.Pop(); sizes[id]++; int x = j % w, y = j / w;
                void P(int k) { if (alpha[k] != 0 && label[k] == 0) { label[k] = id; st.Push(k); } }
                if (x > 0) P(j - 1); if (x < w - 1) P(j + 1); if (y > 0) P(j - w); if (y < h - 1) P(j + w);
            }
        }
        var max = sizes.Max();
        for (var i = 0; i < w * h; i++)
        {
            var a = alpha[i];
            if (a != 0 && sizes[label[i]] < max * 0.01) a = 0;
            px[i * 4 + 3] = a;
        }
        var removed = alpha.Length == 0 ? 0 : Enumerable.Range(0, w * h).Count(i => px[i * 4 + 3] == 0) / (double)(w * h);
        var complex = uniform < 0.8 || removed < 0.02;
        var hint = max == 0 ? "Kein Objekt gefunden – Toleranz verringern."
            : complex ? $"◐ Der Hintergrund ist nicht einfarbig (Rand {uniform:P0} gleich) – Ergebnis prüfen. Für unruhige Hintergründe den KI-Freisteller nutzen."
            : $"Hintergrund {ToHex(bg)} entfernt ({removed:P0} des Bildes).";
        return new(new Pixmap(w, h, px), bg, uniform, removed, complex || max == 0, hint);
    }

    public static string ToHex((byte R, byte G, byte B) c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    /// <summary>Rechteck um alles Sichtbare (Alpha &gt; 16); null = leer.</summary>
    public static (int X, int Y, int W, int H)? Rahmen(Pixmap p, int minAlpha = 16)
    {
        int x0 = p.W, y0 = p.H, x1 = -1, y1 = -1;
        for (var y = 0; y < p.H; y++)
            for (var x = 0; x < p.W; x++)
                if (p.Px[(y * p.W + x) * 4 + 3] > minAlpha) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
        return x1 < 0 ? null : (x0, y0, x1 - x0 + 1, y1 - y0 + 1);
    }

    public static Pixmap Ausschnitt(Pixmap p, int x, int y, int w, int h)
    {
        var o = new Pixmap(w, h);
        for (var r = 0; r < h; r++) Buffer.BlockCopy(p.Px, ((y + r) * p.W + x) * 4, o.Px, r * w * 4, w * 4);
        return o;
    }

    /// <summary>Skalieren mit Vorverkleinern in Halbschritten (Mittelwert über 2 × 2) – scharf und ohne Treppen auch bei großen Fotos.</summary>
    public static Pixmap Skalieren(Pixmap p, int w, int h)
    {
        w = Math.Max(1, w); h = Math.Max(1, h);
        while (p.W >= w * 2 && p.H >= h * 2)
        {
            var o = new Pixmap(p.W / 2, p.H / 2);
            for (var y = 0; y < o.H; y++)
                for (var x = 0; x < o.W; x++)
                {
                    double a = 0, r = 0, g = 0, b = 0;
                    for (var dy = 0; dy < 2; dy++)
                        for (var dx = 0; dx < 2; dx++)
                        {
                            var i = ((y * 2 + dy) * p.W + x * 2 + dx) * 4; double al = p.Px[i + 3];
                            a += al; r += p.Px[i] * al; g += p.Px[i + 1] * al; b += p.Px[i + 2] * al;
                        }
                    var j = (y * o.W + x) * 4;
                    if (a > 0) { o.Px[j] = (byte)Math.Round(r / a); o.Px[j + 1] = (byte)Math.Round(g / a); o.Px[j + 2] = (byte)Math.Round(b / a); }
                    o.Px[j + 3] = (byte)Math.Round(a / 4);
                }
            p = o;
        }
        return p.W == w && p.H == h ? p : p.Resize(w, h);
    }

    // ---------- Licht und Weißabgleich ----------

    /// <summary>Helligkeit automatisch: Tonwerte des sichtbaren Objekts (0,5 % Ausreißer abgeschnitten) auf den vollen Bereich spreizen.
    /// Nutzt <see cref="ImageOps.AutoLevels"/>. Liefert, ob etwas geändert wurde.</summary>
    public static bool AutoLicht(Pixmap p)
    {
        var (lo, hi) = ImageOps.AutoLevels(p.Px);
        if (lo <= 2 && hi >= 253) return false;
        var lut = new byte[256];
        for (var v = 0; v < 256; v++) lut[v] = (byte)Math.Clamp(Math.Round((v - lo) * 255.0 / Math.Max(1, hi - lo)), 0, 255);
        for (var i = 0; i < p.Px.Length; i += 4) { if (p.Px[i + 3] == 0) continue; p.Px[i] = lut[p.Px[i]]; p.Px[i + 1] = lut[p.Px[i + 1]]; p.Px[i + 2] = lut[p.Px[i + 2]]; }
        return true;
    }

    /// <summary>Weißabgleich nach dem Grauwelt-Verfahren auf dem Objekt: Farbstich (Mittel der Kanäle ungleich) wird mit der Stärke
    /// <paramref name="staerke"/> ausgeglichen. Liefert den gemessenen Stich (R, G, B relativ zum Grau).</summary>
    public static (double R, double G, double B) Weissabgleich(Pixmap p, double staerke = 0.7)
    {
        double r = 0, g = 0, b = 0; long n = 0;
        for (var i = 0; i < p.Px.Length; i += 4) { if (p.Px[i + 3] < 128) continue; r += p.Px[i]; g += p.Px[i + 1]; b += p.Px[i + 2]; n++; }
        if (n == 0) return (1, 1, 1);
        r /= n; g /= n; b /= n; var gray = (r + g + b) / 3;
        if (gray < 1) return (1, 1, 1);
        double fr = 1 + (gray / Math.Max(1, r) - 1) * staerke, fg = 1 + (gray / Math.Max(1, g) - 1) * staerke, fb = 1 + (gray / Math.Max(1, b) - 1) * staerke;
        for (var i = 0; i < p.Px.Length; i += 4)
        {
            if (p.Px[i + 3] == 0) continue;
            p.Px[i] = (byte)Math.Clamp(Math.Round(p.Px[i] * fr), 0, 255); p.Px[i + 1] = (byte)Math.Clamp(Math.Round(p.Px[i + 1] * fg), 0, 255); p.Px[i + 2] = (byte)Math.Clamp(Math.Round(p.Px[i + 2] * fb), 0, 255);
        }
        return (r / gray, g / gray, b / gray);
    }

    // ---------- Zusammensetzen ----------

    static void Over(Pixmap dst, Pixmap src, int ox, int oy, double opacity = 1)
    {
        for (var y = 0; y < src.H; y++)
        {
            var dy = oy + y; if (dy < 0 || dy >= dst.H) continue;
            for (var x = 0; x < src.W; x++)
            {
                var dx = ox + x; if (dx < 0 || dx >= dst.W) continue;
                var s = (y * src.W + x) * 4; var a = src.Px[s + 3] / 255.0 * opacity; if (a <= 0) continue;
                var d = (dy * dst.W + dx) * 4; var da = dst.Px[d + 3] / 255.0; var oa = a + da * (1 - a);
                for (var c = 0; c < 3; c++) dst.Px[d + c] = (byte)Math.Round((src.Px[s + c] * a + dst.Px[d + c] * da * (1 - a)) / Math.Max(1e-6, oa));
                dst.Px[d + 3] = (byte)Math.Round(oa * 255);
            }
        }
    }

    /// <summary>Weicher Kontaktschatten: dunkle, weich auslaufende Ellipse unter dem Objekt.</summary>
    static void Kontaktschatten(Pixmap dst, int cx, int bottom, int objW, double staerke = 0.35)
    {
        var rx = Math.Max(4, objW * 0.46); var ry = Math.Max(2, objW * 0.06);
        for (var y = (int)(bottom - ry * 2); y <= bottom + ry * 2; y++)
        {
            if (y < 0 || y >= dst.H) continue;
            for (var x = (int)(cx - rx * 1.4); x <= cx + rx * 1.4; x++)
            {
                if (x < 0 || x >= dst.W) continue;
                double nx = (x - cx) / rx, ny = (y - bottom) / ry; var d = nx * nx + ny * ny;
                var a = staerke * Math.Exp(-d * 1.6); if (a < 0.004) continue;
                var i = (y * dst.W + x) * 4; var da = dst.Px[i + 3] / 255.0; var oa = a + da * (1 - a);
                for (var c = 0; c < 3; c++) dst.Px[i + c] = (byte)Math.Round((0 * a + dst.Px[i + c] * da * (1 - a)) / Math.Max(1e-6, oa));
                dst.Px[i + 3] = (byte)Math.Round(oa * 255);
            }
        }
    }

    /// <summary>Spiegelung am Boden: gespiegeltes Objekt, nach unten ausblendend.</summary>
    static void Spiegelung(Pixmap dst, Pixmap obj, int ox, int oy)
    {
        var hgt = (int)(obj.H * 0.3);
        for (var y = 0; y < hgt; y++)
        {
            var dy = oy + obj.H + y; if (dy >= dst.H) break;
            var fade = 0.28 * (1 - y / (double)hgt);
            var sy = obj.H - 1 - y;
            for (var x = 0; x < obj.W; x++)
            {
                var dx = ox + x; if (dx < 0 || dx >= dst.W) continue;
                var s = (sy * obj.W + x) * 4; var a = obj.Px[s + 3] / 255.0 * fade; if (a <= 0) continue;
                var d = (dy * dst.W + dx) * 4; var da = dst.Px[d + 3] / 255.0; var oa = a + da * (1 - a);
                for (var c = 0; c < 3; c++) dst.Px[d + c] = (byte)Math.Round((obj.Px[s + c] * a + dst.Px[d + c] * da * (1 - a)) / Math.Max(1e-6, oa));
                dst.Px[d + 3] = (byte)Math.Round(oa * 255);
            }
        }
    }

    /// <summary>Ganze Bearbeitung eines Fotos: freistellen, Licht, Weißabgleich, mittig mit Rand auf die Zielgröße, Hintergrund, Schatten, Wasserzeichen.
    /// <paramref name="zeichen"/> ist das fertige Wasserzeichen (Text, von der Oberfläche gesetzt, oder Logo) – null = keins.
    /// <paramref name="breite"/>/<paramref name="hoehe"/> überschreiben die Größe der Vorgabe (z. B. für die Vorschau).</summary>
    public static FotoErgebnis Bearbeiten(Pixmap src, FotoEinstellung e, Pixmap? zeichen = null, int breite = 0, int hoehe = 0)
    {
        var v = Vorgabe(e.Markt);
        int W = breite > 0 ? breite : v.Breite, H = hoehe > 0 ? hoehe : v.Hoehe;
        Freistellung? frei = null; var obj = src.Clone();
        if (e.Freistellen) { frei = Freistellen(src, e.Toleranz); obj = frei.Bild; }
        if (e.AutoLicht) AutoLicht(obj);
        if (e.Weissabgleich) Weissabgleich(obj);
        var box = Rahmen(obj) ?? throw new InvalidOperationException("Im Bild ist kein Objekt zu finden – beim Freistellen die Toleranz verringern.");
        obj = Ausschnitt(obj, box.X, box.Y, box.W, box.H);

        var rand = Math.Clamp(e.RandProzent, 0, 30) / 100.0 * Math.Min(W, H);
        var schattenPlatz = e.Schatten == "spiegel" ? 0.22 : e.Schatten == "weich" ? 0.04 : 0;
        double innerW = W - 2 * rand, innerH = (H - 2 * rand) / (1 + schattenPlatz);
        var scale = Math.Min(innerW / obj.W, innerH / obj.H);
        int ow = Math.Max(1, (int)Math.Round(obj.W * scale)), oh = Math.Max(1, (int)Math.Round(obj.H * scale));
        var scaled = Skalieren(obj, ow, oh);

        var canvas = new Pixmap(W, H);
        if (e.Hintergrund != "transparent") { var (r, g, b) = Rgb(e.Hintergrund); canvas.Fill(r, g, b, 255); }
        var ox = (W - ow) / 2; var oy = (int)Math.Round((H - oh * (1 + schattenPlatz)) / 2);
        if (e.Schatten == "weich") Kontaktschatten(canvas, W / 2, oy + oh - Math.Max(1, oh / 100), ow);
        if (e.Schatten == "spiegel") Spiegelung(canvas, scaled, ox, oy);
        Over(canvas, scaled, ox, oy);
        if (zeichen is not null && e.WasserzeichenDeckkraft > 0) Stempeln(canvas, zeichen, e.WasserzeichenOrt, Math.Clamp(e.WasserzeichenDeckkraft, 0.05, 1));
        // Reinweiß sicherstellen: fast weiße Hintergrundpixel (Rundung beim Mischen) auf 255 setzen
        if (e.Hintergrund.Equals("#FFFFFF", StringComparison.OrdinalIgnoreCase))
            for (var i = 0; i < canvas.Px.Length; i += 4)
                if (canvas.Px[i] >= 254 && canvas.Px[i + 1] >= 254 && canvas.Px[i + 2] >= 254) { canvas.Px[i] = canvas.Px[i + 1] = canvas.Px[i + 2] = 255; canvas.Px[i + 3] = 255; }
        return new(canvas, Math.Max(ow / (double)W, oh / (double)H), scale > 1.01, frei);
    }

    /// <summary>Wasserzeichen setzen (Ort mit 3 % Abstand vom Rand).</summary>
    public static void Stempeln(Pixmap dst, Pixmap zeichen, string ort, double deckkraft)
    {
        var m = (int)(Math.Min(dst.W, dst.H) * 0.03);
        var (x, y) = ort switch
        {
            "unten-links" => (m, dst.H - zeichen.H - m), "oben-rechts" => (dst.W - zeichen.W - m, m), "oben-links" => (m, m),
            "mitte" => ((dst.W - zeichen.W) / 2, (dst.H - zeichen.H) / 2), _ => (dst.W - zeichen.W - m, dst.H - zeichen.H - m),
        };
        Over(dst, zeichen, x, y, deckkraft);
    }

    // ---------- Prüfung gegen die Vorgabe ----------

    /// <summary>Anteil der Bildbreite/-höhe, den das Objekt einnimmt (alles, was sich vom Randton abhebt), und ob der Rand reinweiß ist.</summary>
    public static (double Fuellung, double WeissAmRand) Messen(Pixmap p)
    {
        var bg = p.At(0, 0); int x0 = p.W, y0 = p.H, x1 = -1, y1 = -1; long white = 0, border = 0;
        for (var y = 0; y < p.H; y++)
            for (var x = 0; x < p.W; x++)
            {
                var i = (y * p.W + x) * 4;
                if (x < 2 || y < 2 || x >= p.W - 2 || y >= p.H - 2) { border++; if (p.Px[i] == 255 && p.Px[i + 1] == 255 && p.Px[i + 2] == 255 && p.Px[i + 3] == 255) white++; }
                var diff = Math.Max(Math.Abs(p.Px[i] - bg.R), Math.Max(Math.Abs(p.Px[i + 1] - bg.G), Math.Max(Math.Abs(p.Px[i + 2] - bg.B), Math.Abs(p.Px[i + 3] - bg.A))));
                if (diff > 12) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
            }
        var fill = x1 < 0 ? 0 : Math.Max((x1 - x0 + 1) / (double)p.W, (y1 - y0 + 1) / (double)p.H);
        return (fill, border == 0 ? 0 : white / (double)border);
    }

    public static IReadOnlyList<FotoBefund> Pruefen(Pixmap bild, FotoVorgabe v, string format, long bytes, bool mitWasserzeichen, bool hauptbild = true, (int W, int H)? endGroesse = null)
    {
        var f = new List<FotoBefund>(); var fmt = Norm(format);
        var (bw, bh) = endGroesse ?? (bild.W, bild.H);
        int lang = Math.Max(bw, bh), kurz = Math.Min(bw, bh);
        f.Add(new("Größe", lang >= v.MinLangeSeite && kurz >= v.MinKurzeSeite, $"{bw} × {bh} px – verlangt: längste Seite ab {v.MinLangeSeite} px" + (v.MinKurzeSeite > 0 ? $", kürzeste ab {v.MinKurzeSeite} px" : ""), true));
        f.Add(new("Höchstgröße", lang <= v.MaxSeite, $"längste Seite höchstens {v.MaxSeite} px", true));
        var ratioOk = Math.Abs(bw / (double)bh - v.Breite / (double)v.Hoehe) < 0.02;
        f.Add(new("Seitenverhältnis", ratioOk, $"empfohlen {v.Breite} × {v.Hoehe}" + (ratioOk ? "" : $" – dieses Bild hat {bw / (double)bh:0.00}:1"), false));
        f.Add(new("Format", v.Formate.Contains(fmt), $"{fmt.ToUpperInvariant()} – erlaubt: {string.Join(", ", v.Formate.Select(x => x.ToUpperInvariant()))}", true));
        if (bytes >= 0) f.Add(new("Dateigröße", bytes <= v.MaxBytes, $"{bytes / 1024.0:0} KB – höchstens {v.MaxBytes / 1024 / 1024} MB", v.Id != "etsy"));
        var (fill, white) = Messen(bild);
        if (v.WeissPflicht && hauptbild)
        {
            f.Add(new("Hintergrund", white >= 0.999, white >= 0.999 ? "reinweiß (255/255/255) am ganzen Rand" : $"nur {white:P1} des Randes reinweiß – Hauptbild braucht RGB 255/255/255", true));
            f.Add(new("Füllung", fill >= v.MinFuellung, $"Objekt füllt {fill:P0} – verlangt mindestens {v.MinFuellung:P0}", true));
            var transp = fmt == "png" && Enumerable.Range(0, bild.W * bild.H).Any(i => bild.Px[i * 4 + 3] < 255);
            if (transp) f.Add(new("Transparenz", false, "durchsichtige Stellen – das Hauptbild braucht einen weißen Hintergrund", true));
        }
        if (!v.WasserzeichenErlaubt && mitWasserzeichen) f.Add(new("Wasserzeichen", false, $"{v.Name}: keine Texte, Logos oder Wasserzeichen im Bild", true));
        return f;
    }

    public static bool Bestanden(IEnumerable<FotoBefund> f) => f.All(x => x.Ok || !x.Pflicht);

    static string Norm(string format) => format.Trim().TrimStart('.').ToLowerInvariant() switch { "jpeg" => "jpg", "tiff" => "tif", var x => x };

    // ---------- Umbenennen und Paket ----------

    [GeneratedRegex(@"[^\p{L}\p{N}._-]+")] private static partial Regex UnsafeRx();

    /// <summary>Dateiname nach Muster. {variante}: Amazon-Kennung MAIN für das erste Bild, danach PT01 … PT08.</summary>
    public static string Dateiname(string muster, string nr, int n, string markt, string ext, DateTime? datum = null)
    {
        var art = nr.Trim().Length > 0 ? nr.Trim() : "artikel";
        var variante = n <= 1 ? "MAIN" : $"PT{Math.Min(n - 1, 99):00}";
        var s = (string.IsNullOrWhiteSpace(muster) ? "{nr}_{n}" : muster)
            .Replace("{nr}", art).Replace("{n}", n.ToString("00", CultureInfo.InvariantCulture)).Replace("{variante}", variante)
            .Replace("{markt}", markt).Replace("{datum}", (datum ?? DateTime.Today).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        s = UnsafeRx().Replace(s, "-").Trim('-', '.');
        if (s.Length == 0) s = "foto";
        if (s.Length > 120) s = s[..120];
        return s + "." + Norm(ext);
    }

    /// <summary>Laufende Nummer je Artikelnummer (1, 2, 3 … – je Artikel neu).</summary>
    public static List<int> Nummern(IReadOnlyList<ProduktFoto> fotos)
    {
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase); var l = new List<int>();
        foreach (var f in fotos) { var k = f.Artikelnummer.Trim(); seen[k] = seen.GetValueOrDefault(k) + 1; l.Add(seen[k]); }
        return l;
    }

    /// <summary>Prüfbericht als CSV (Semikolon, für Excel/Trendo Tabelle).</summary>
    public static string BerichtCsv(IEnumerable<(string Datei, IReadOnlyList<FotoBefund> Befunde)> rows, FotoVorgabe v)
    {
        var sb = new StringBuilder();
        sb.Append("Datei;Marktplatz;Ergebnis;Punkte\n");
        static string Q(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
        foreach (var (datei, b) in rows)
            sb.Append(Q(datei)).Append(';').Append(Q(v.Name)).Append(';').Append(Bestanden(b) ? "bestanden" : "nicht bestanden").Append(';')
              .Append(Q(string.Join(" | ", b.Where(x => !x.Ok).Select(x => (x.Pflicht ? "Pflicht: " : "Empfehlung: ") + x.Punkt + " – " + x.Text)))).Append('\n');
        sb.Append(Q($"Vorgaben Stand {Stand}: {v.Quelle}")).Append('\n');
        return sb.ToString();
    }

    /// <summary>Export-Paket als ZIP: alle Bilder plus Prüfbericht. Gleiche Namen bekommen „-2“, „-3“ …</summary>
    public static string Paket(string zip, IEnumerable<(string Name, byte[] Daten)> dateien, string berichtCsv)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(zip))!);
        if (File.Exists(zip)) File.Delete(zip);
        using var z = ZipFile.Open(zip, ZipArchiveMode.Create);
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, data) in dateien)
        {
            var n = name; var stem = Path.GetFileNameWithoutExtension(name); var ext = Path.GetExtension(name);
            for (var k = 2; !used.Add(n); k++) n = $"{stem}-{k}{ext}";
            using var s = z.CreateEntry(n, CompressionLevel.Fastest).Open(); s.Write(data);
        }
        using (var s = z.CreateEntry("pruefbericht.csv").Open()) s.Write(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(berichtCsv)).ToArray());
        return zip;
    }
}
