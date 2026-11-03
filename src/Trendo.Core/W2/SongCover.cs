using System.Text;

namespace Trendo.Core;

/// <summary>Wunsch 03.11.2026: Cover-Bilder in Trendo Musik „perfekt wie ein Song-Cover“. Die Bild-KI malt nur das Motiv (Genre und Stimmung des Lieds,
/// ohne Schrift – Bild-KIs verunstalten Buchstaben); Titel und Name setzt Trendo mit echter Schrift in einem Profi-Layout darauf.
/// Beim Export kommt das Cover in die MP3 (ID3v2.3, Bild „Vorderseite“), damit jeder Player es zeigt.</summary>
public static class SongCover
{
    public static readonly (string Id, string Label)[] Layouts = [("unten", "Titel unten"), ("oben", "Titel oben"), ("mittig", "Mittig mit Rahmen"), ("ohne", "Nur Bild")];
    /// <summary>Kantenlänge wie bei Streamingdiensten üblich.</summary>
    public const int Size = 1400;

    // Genre → Motiv (englisch, damit die Bild-KI es gut versteht). Reihenfolge: genauere Begriffe zuerst.
    static readonly (string[] Keys, string Scene)[] Scenes =
    [
        (["lo-fi", "lofi"], "a cozy rainy window at night with a desk lamp, headphones and a steaming mug, soft grainy anime-inspired illustration"),
        (["jazz", "swing", "bossa"], "a dim smoky jazz club stage with a golden saxophone and a double bass in warm amber spotlight"),
        (["blues"], "a lonely guitarist silhouette on a wooden porch at dusk, deep blue tones, Mississippi delta mood"),
        (["soul", "funk", "disco"], "a glowing disco ball above a retro dance floor, warm 70s colors, film grain"),
        (["metal"], "dark stormy mountains with lightning and a burning sky, epic and heavy"),
        (["punk"], "a gritty concrete wall with torn posters and spray paint splashes, high contrast"),
        (["rock"], "an electric guitar on a concert stage under dramatic red and white spotlights with haze"),
        (["techno", "trance", "house", "elektro", "electro", "edm", "dance", "synth", "dubstep", "drum"], "abstract neon light trails and a futuristic night city skyline, glowing magenta and cyan"),
        (["hip-hop", "hiphop", "rap", "trap"], "an urban street at night with wet asphalt reflecting street lights and a graffiti wall"),
        (["reggae", "dancehall", "tropical"], "a tropical beach at sunset with palm trees and warm golden light"),
        (["latin", "reggaeton", "salsa", "flamenco"], "a vibrant summer night street festival with colorful lights and dancers in motion blur"),
        (["country", "folk", "americana"], "an open prairie road with a wooden fence and a warm golden sunset"),
        (["schlager", "volksmusik"], "a colorful summer festival with confetti and string lights under a warm evening sky"),
        (["klassik", "classical", "orchester", "orchestra", "oper"], "a grand piano in an elegant candle-lit concert hall with soft golden light"),
        (["film", "cinematic", "soundtrack", "episch", "epic"], "a vast cinematic landscape at dawn with dramatic clouds and rays of light"),
        (["ambient", "chill", "meditation"], "a calm misty lake at dawn with soft pastel colors and gentle reflections"),
        (["kinder", "kids"], "a playful colorful illustration of a sunny meadow with balloons, friendly and bright"),
        (["weihnacht", "christmas"], "a snowy village at night with warm glowing windows and fairy lights"),
        (["akustik", "acoustic", "singer"], "an acoustic guitar leaning against a sunlit wooden wall, warm natural light"),
        (["pop"], "a dreamy portrait-free scene of a convertible on a coastal road at golden hour, vibrant summer colors"),
    ];

    static readonly (string[] Keys, string Mood)[] Moods =
    [
        (["fröhlich", "happy", "sommer", "summer", "tanzbar"], "bright, warm, joyful colors"),
        (["traurig", "sad", "melanchol", "regen"], "melancholic blue tones, soft rain"),
        (["romantisch", "liebe", "love"], "soft pink and gold sunset light, romantic atmosphere"),
        (["episch", "epic", "kraft"], "dramatic cinematic lighting, grand scale"),
        (["ruhig", "calm", "sanft", "ballade"], "calm, soft, gentle light"),
        (["dunkel", "dark", "nacht", "night"], "moody dark atmosphere with deep shadows"),
    ];

    static readonly string[] Framing = ["centered striking composition", "wide cinematic shot", "symmetrical minimalist composition", "close-up with shallow depth of field"];

    /// <summary>Beschreibung für die Bild-KI aus Stil (und Titel) des Lieds; <paramref name="variant"/> wechselt die Bildgestaltung.</summary>
    public static string Prompt(string style, string title, int variant = 0)
    {
        var s = (style + " " + title).ToLowerInvariant();
        var scene = Scenes.FirstOrDefault(x => x.Keys.Any(k => s.Contains(k))).Scene ?? "an abstract artistic composition of flowing colors and light, elegant and modern";
        var moods = Moods.Where(x => x.Keys.Any(k => s.Contains(k))).Select(x => x.Mood).Take(2).ToList();
        var frame = Framing[Math.Abs(variant) % Framing.Length];
        return $"professional music album cover art, {scene}, {string.Join(", ", moods.Append(frame))}, high detail, rich colors, award-winning photography and design, square format, " +
               "no text, no letters, no words, no typography, no logos, no watermark, no people's faces";
    }

    /// <summary>MP3 mit eingebettetem Cover (und Titel/Name) zurückgeben. Ein vorhandener ID3v2-Tag am Anfang wird ersetzt, der Ton bleibt unverändert.</summary>
    public static byte[] EmbedInMp3(byte[] mp3, byte[] jpeg, string title, string artist)
    {
        if (jpeg.Length < 4 || jpeg[0] != 0xFF || jpeg[1] != 0xD8) throw new ArgumentException("Das Cover muss ein JPEG sein.");
        var audio = mp3.AsSpan();
        if (audio.Length >= 10 && audio[0] == 'I' && audio[1] == 'D' && audio[2] == '3')
        {
            var size = (audio[6] << 21) | (audio[7] << 14) | (audio[8] << 7) | audio[9];
            var footer = (audio[5] & 0x10) != 0 ? 10 : 0;
            audio = audio[Math.Min(audio.Length, 10 + size + footer)..];
        }
        using var frames = new MemoryStream();
        void Frame(string id, byte[] body)
        {
            frames.Write(Encoding.ASCII.GetBytes(id));
            frames.Write([(byte)(body.Length >> 24), (byte)(body.Length >> 16), (byte)(body.Length >> 8), (byte)body.Length, 0, 0]);
            frames.Write(body);
        }
        byte[] Text(string t) => [1, .. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(t)]; // UTF-16 mit BOM
        if (title.Trim().Length > 0) Frame("TIT2", Text(title.Trim()));
        if (artist.Trim().Length > 0) Frame("TPE1", Text(artist.Trim()));
        Frame("APIC", [0, .. Encoding.ASCII.GetBytes("image/jpeg"), 0, 3, 0, .. jpeg]); // Typ 3 = Vorderseite
        var tagSize = (int)frames.Length;
        using var o = new MemoryStream();
        o.Write([(byte)'I', (byte)'D', (byte)'3', 3, 0, 0, (byte)((tagSize >> 21) & 0x7F), (byte)((tagSize >> 14) & 0x7F), (byte)((tagSize >> 7) & 0x7F), (byte)(tagSize & 0x7F)]);
        frames.Position = 0; frames.CopyTo(o);
        o.Write(audio);
        return o.ToArray();
    }

    /// <summary>Eingebettetes Cover aus einer MP3 lesen (oder null) – zum Prüfen.</summary>
    public static byte[]? ReadMp3Cover(byte[] mp3)
    {
        if (mp3.Length < 10 || mp3[0] != 'I' || mp3[1] != 'D' || mp3[2] != '3' || mp3[3] != 3) return null;
        var size = (mp3[6] << 21) | (mp3[7] << 14) | (mp3[8] << 7) | mp3[9];
        for (var p = 10; p + 10 <= 10 + size && p + 10 <= mp3.Length;)
        {
            var id = Encoding.ASCII.GetString(mp3, p, 4); var len = (mp3[p + 4] << 24) | (mp3[p + 5] << 16) | (mp3[p + 6] << 8) | mp3[p + 7];
            if (len <= 0) break;
            if (id == "APIC")
            {
                var b = p + 10; var e = b + len; var q = b + 1;
                while (q < e && mp3[q] != 0) q++; // MIME
                q += 2; // Ende MIME + Bildtyp
                while (q < e && mp3[q] != 0) q++; // Beschreibung
                q++;
                return mp3[q..e];
            }
            p += 10 + len;
        }
        return null;
    }
}
