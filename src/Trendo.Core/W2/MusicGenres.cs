namespace Trendo.Core;

/// <summary>Trendo Musik M1 (freigegeben 03.11.2026): über 150 Genres in Gruppen, deutsch benannt. <c>Tag</c> ist der Begriff, den ACE-Step kennt
/// (geprüft gegen dessen genres_vocab.txt), dazu ein üblicher Tempobereich und typische Instrumente als Vorschlag.</summary>
public static class MusicGenres
{
    public sealed record Genre(string Group, string Name, string Tag, int BpmLow, int BpmHigh, string Instruments, bool Minor = false)
    {
        public int Bpm => (BpmLow + BpmHigh) / 2;
    }

    static Genre G(string group, string name, string tag, int lo, int hi, string ins, bool minor = false) => new(group, name, tag, lo, hi, ins, minor);

    public static readonly string[] Groups = ["Pop", "Rock & Metal", "Elektronisch", "Hip-Hop & R&B", "Jazz, Blues & Soul", "Klassik & Film", "Schlager & Volksmusik", "Welt", "Country & Folk", "Chill & Ambient", "Spezial"];

    public static readonly Genre[] All =
    [
        // Pop
        G("Pop", "Pop", "pop", 100, 125, "Synthesizer, Schlagzeug, Bass"), G("Pop", "Dance-Pop", "dance pop", 115, 128, "Synthesizer, Drum Machine"),
        G("Pop", "Synthpop", "synthpop", 105, 125, "Analog-Synthesizer, Drum Machine"), G("Pop", "Electropop", "electropop", 110, 128, "Synthesizer, Beats"),
        G("Pop", "Indie-Pop", "indie pop", 95, 125, "Gitarre, Synthesizer"), G("Pop", "Dream-Pop", "dream pop", 80, 110, "hallige Gitarren, Pads"),
        G("Pop", "Teen-Pop", "teen pop", 100, 125, "Synthesizer, Schlagzeug"), G("Pop", "Power-Pop", "power pop", 120, 150, "E-Gitarre, Schlagzeug"),
        G("Pop", "Pop-Ballade", "pop ballad", 60, 80, "Klavier, Streicher"), G("Pop", "Akustik-Pop", "acoustic pop", 85, 115, "Akustikgitarre, Klavier"),
        G("Pop", "K-Pop", "k-pop", 100, 130, "Synthesizer, Beats"), G("Pop", "J-Pop", "j-pop", 110, 140, "Synthesizer, Gitarre"),
        G("Pop", "Europop", "europop", 120, 132, "Synthesizer, Beats"), G("Pop", "Chamber-Pop", "chamber pop", 80, 115, "Streicher, Klavier"),
        G("Pop", "80er-Pop", "80s pop", 110, 128, "Synthesizer, Gated Drums"), G("Pop", "90er-Pop", "90s pop", 100, 125, "Synthesizer, Beats"),
        G("Pop", "Hyperpop", "hyperpop", 140, 170, "verzerrte Synths, 808"), G("Pop", "Art-Pop", "art pop", 90, 120, "Synthesizer, Streicher"),
        // Rock & Metal
        G("Rock & Metal", "Rock", "rock", 110, 140, "E-Gitarre, Bass, Schlagzeug"), G("Rock & Metal", "Classic Rock", "classic rock", 105, 135, "E-Gitarre, Hammond-Orgel"),
        G("Rock & Metal", "Hard Rock", "hard rock", 115, 145, "verzerrte E-Gitarre, Schlagzeug"), G("Rock & Metal", "Alternative Rock", "alternative rock", 100, 140, "E-Gitarre, Bass"),
        G("Rock & Metal", "Indie-Rock", "indie rock", 110, 140, "E-Gitarre, Schlagzeug"), G("Rock & Metal", "Punk", "punk rock", 150, 190, "schnelle E-Gitarre, Schlagzeug"),
        G("Rock & Metal", "Pop-Punk", "pop punk", 150, 180, "E-Gitarre, Schlagzeug"), G("Rock & Metal", "Grunge", "grunge", 90, 130, "verzerrte E-Gitarre", true),
        G("Rock & Metal", "Garage Rock", "garage rock", 120, 160, "rohe E-Gitarre"), G("Rock & Metal", "Psychedelic Rock", "psychedelic rock", 90, 125, "Gitarre mit Effekten, Orgel"),
        G("Rock & Metal", "Progressive Rock", "progressive rock", 80, 140, "Gitarre, Synthesizer, Orgel"), G("Rock & Metal", "Soft Rock", "soft rock", 80, 110, "Gitarre, Klavier"),
        G("Rock & Metal", "Rock 'n' Roll", "rock and roll", 150, 180, "Gitarre, Kontrabass, Klavier"), G("Rock & Metal", "Surf Rock", "surf rock", 140, 170, "hallige Gitarre"),
        G("Rock & Metal", "Post-Rock", "post-rock", 70, 120, "Gitarrenflächen, Schlagzeug"), G("Rock & Metal", "Shoegaze", "shoegaze", 80, 120, "Gitarrenwände, Hall"),
        G("Rock & Metal", "Heavy Metal", "heavy metal", 110, 160, "verzerrte E-Gitarren, Doublebass", true), G("Rock & Metal", "Thrash Metal", "thrash metal", 160, 220, "schnelle Riffs", true),
        G("Rock & Metal", "Power Metal", "power metal", 150, 200, "E-Gitarren, Keyboard, Doublebass", true), G("Rock & Metal", "Symphonic Metal", "symphonic metal", 100, 160, "Orchester, E-Gitarren", true),
        G("Rock & Metal", "Metalcore", "metalcore", 120, 180, "Breakdowns, E-Gitarren", true), G("Rock & Metal", "Nu Metal", "nu metal", 90, 120, "tiefe Gitarren, Scratches", true),
        G("Rock & Metal", "Doom Metal", "doom metal", 50, 80, "tiefe, langsame Riffs", true), G("Rock & Metal", "Deutschrock", "German rock", 110, 140, "E-Gitarre, Schlagzeug"),
        G("Rock & Metal", "Neue Deutsche Härte", "Neue Deutsche Härte", 100, 130, "harte Gitarren, Industrial", true),
        // Elektronisch
        G("Elektronisch", "House", "house", 118, 128, "Four-on-the-floor, Klavier-Akkorde"), G("Elektronisch", "Deep House", "deep house", 118, 125, "warme Bässe, Pads"),
        G("Elektronisch", "Tech House", "tech house", 124, 128, "Drum Machine, Bass"), G("Elektronisch", "Progressive House", "progressive house", 124, 130, "Synth-Flächen"),
        G("Elektronisch", "Techno", "techno", 125, 140, "Drum Machine, Synthesizer", true), G("Elektronisch", "Minimal Techno", "minimal techno", 122, 130, "reduzierte Beats", true),
        G("Elektronisch", "Trance", "trance", 132, 142, "Supersaw-Synths, Arpeggios"), G("Elektronisch", "Psytrance", "psytrance", 140, 148, "rollender Bass", true),
        G("Elektronisch", "Drum and Bass", "drum and bass", 165, 178, "Breakbeats, Sub-Bass"), G("Elektronisch", "Liquid DnB", "liquid drum and bass", 168, 176, "Pads, Breaks"),
        G("Elektronisch", "Dubstep", "dubstep", 138, 142, "Wobble-Bass, Halbtempo-Beats", true), G("Elektronisch", "Future Bass", "future bass", 140, 160, "Supersaw-Akkorde, Vocal Chops"),
        G("Elektronisch", "EDM", "EDM", 124, 130, "Synthesizer, Drops"), G("Elektronisch", "Big Room", "big room house", 126, 130, "große Drops"),
        G("Elektronisch", "Electro", "electro", 120, 130, "Synth-Bass, Drum Machine"), G("Elektronisch", "Synthwave", "synthwave", 85, 115, "Analog-Synths, Gated Drums"),
        G("Elektronisch", "Retrowave", "retrowave", 90, 115, "80er-Synths"), G("Elektronisch", "Hardstyle", "hardstyle", 150, 155, "verzerrte Kick"),
        G("Elektronisch", "Hardcore", "hardcore techno", 160, 200, "harte Kicks", true), G("Elektronisch", "UK Garage", "UK garage", 130, 136, "Shuffle-Beats"),
        G("Elektronisch", "Breakbeat", "breakbeat", 120, 140, "Breakbeats"), G("Elektronisch", "IDM", "IDM", 90, 160, "glitchige Beats"),
        G("Elektronisch", "Chillstep", "chillstep", 70, 80, "Pads, sanfter Bass"), G("Elektronisch", "Eurodance", "eurodance", 130, 145, "Synth-Riffs, Beats"),
        G("Elektronisch", "Italo Disco", "italo disco", 115, 125, "Synthesizer, Drum Machine"), G("Elektronisch", "Chiptune", "chiptune", 120, 160, "8-Bit-Sounds"),
        // Hip-Hop & R&B
        G("Hip-Hop & R&B", "Hip-Hop", "hip-hop", 85, 100, "Beats, Samples"), G("Hip-Hop & R&B", "Boom Bap", "boom bap", 85, 95, "Drum-Samples, Vinyl"),
        G("Hip-Hop & R&B", "Trap", "trap", 130, 150, "808, Hi-Hats", true), G("Hip-Hop & R&B", "Drill", "drill", 140, 145, "gleitende 808", true),
        G("Hip-Hop & R&B", "Cloud Rap", "cloud rap", 60, 80, "verträumte Pads"), G("Hip-Hop & R&B", "Lo-Fi Hip-Hop", "lo-fi hip hop", 70, 90, "Rhodes, Vinyl-Knistern"),
        G("Hip-Hop & R&B", "Deutschrap", "German hip hop", 85, 100, "Beats"), G("Hip-Hop & R&B", "R&B", "R&B", 65, 95, "weiche Beats, Keys"),
        G("Hip-Hop & R&B", "Contemporary R&B", "contemporary R&B", 65, 95, "moderne Beats"), G("Hip-Hop & R&B", "Neo-Soul", "neo soul", 70, 95, "Rhodes, Bass"),
        G("Hip-Hop & R&B", "G-Funk", "g-funk", 90, 100, "Synth-Lead, Bass"), G("Hip-Hop & R&B", "Old-School Hip-Hop", "old school hip hop", 90, 110, "Drum Machine, Scratches"),
        G("Hip-Hop & R&B", "Afro-Trap", "afro trap", 100, 115, "Afro-Rhythmen, 808"), G("Hip-Hop & R&B", "Grime", "grime", 138, 142, "harte Synths", true),
        // Jazz, Blues & Soul
        G("Jazz, Blues & Soul", "Jazz", "jazz", 90, 180, "Saxophon, Klavier, Kontrabass"), G("Jazz, Blues & Soul", "Smooth Jazz", "smooth jazz", 80, 100, "Saxophon, E-Piano"),
        G("Jazz, Blues & Soul", "Swing", "swing", 140, 200, "Big Band, Bläser"), G("Jazz, Blues & Soul", "Bebop", "bebop", 180, 280, "Saxophon, Trompete"),
        G("Jazz, Blues & Soul", "Cool Jazz", "cool jazz", 100, 150, "Trompete, Klavier"), G("Jazz, Blues & Soul", "Jazz-Ballade", "jazz ballad", 55, 80, "Klavier, Kontrabass, Besen"),
        G("Jazz, Blues & Soul", "Bossa Nova", "bossa nova", 110, 140, "Nylongitarre, sanfte Percussion"), G("Jazz, Blues & Soul", "Acid Jazz", "acid jazz", 100, 115, "Groove, Bläser"),
        G("Jazz, Blues & Soul", "Jazz Fusion", "jazz fusion", 100, 140, "E-Gitarre, Keys"), G("Jazz, Blues & Soul", "Blues", "blues", 70, 120, "Bluesgitarre, Mundharmonika"),
        G("Jazz, Blues & Soul", "Blues Rock", "blues rock", 100, 130, "E-Gitarre"), G("Jazz, Blues & Soul", "Delta Blues", "delta blues", 70, 100, "Slide-Gitarre"),
        G("Jazz, Blues & Soul", "Soul", "soul", 70, 110, "Bläser, Orgel"), G("Jazz, Blues & Soul", "Motown", "motown", 110, 130, "Bläser, Tamburin"),
        G("Jazz, Blues & Soul", "Funk", "funk", 95, 120, "Slap-Bass, Bläser"), G("Jazz, Blues & Soul", "Disco", "disco", 115, 130, "Streicher, Four-on-the-floor"),
        G("Jazz, Blues & Soul", "Gospel", "gospel", 70, 130, "Chor, Hammond-Orgel"), G("Jazz, Blues & Soul", "Lounge", "lounge", 85, 110, "Vibraphon, E-Piano"),
        // Klassik & Film
        G("Klassik & Film", "Klassik", "classical", 60, 120, "Orchester"), G("Klassik & Film", "Barock", "baroque", 70, 120, "Cembalo, Streicher"),
        G("Klassik & Film", "Romantik", "romantic classical", 60, 110, "großes Orchester"), G("Klassik & Film", "Klavier solo", "solo piano", 60, 100, "Klavier"),
        G("Klassik & Film", "Streichquartett", "cinematic string quartet", 60, 120, "Streicher"), G("Klassik & Film", "Neoklassik", "neoclassical", 60, 90, "Klavier, Streicher"),
        G("Klassik & Film", "Oper", "opera", 60, 120, "Orchester, Sopran"), G("Klassik & Film", "Filmmusik", "film score", 60, 120, "Orchester, Chor"),
        G("Klassik & Film", "Epische Orchestermusik", "epic orchestral", 80, 140, "Orchester, Chor, Trommeln", true), G("Klassik & Film", "Trailer-Musik", "trailer music", 90, 140, "Hybrid-Orchester", true),
        G("Klassik & Film", "Videospiel-Musik", "video game music", 100, 150, "Orchester, Synths"), G("Klassik & Film", "Musical", "musical theatre", 90, 140, "Orchester, Gesang"),
        G("Klassik & Film", "Minimal Music", "minimalism", 90, 130, "Klavier-Pattern"),
        // Schlager & Volksmusik
        G("Schlager & Volksmusik", "Schlager", "schlager", 115, 135, "Synths, Beat, Gitarre"), G("Schlager & Volksmusik", "Discofox", "German Schlager", 118, 130, "Four-on-the-floor"),
        G("Schlager & Volksmusik", "Partyschlager", "party schlager", 125, 140, "Beat, Bläser"), G("Schlager & Volksmusik", "Volksmusik", "German folk", 100, 130, "Akkordeon, Blasinstrumente"),
        G("Schlager & Volksmusik", "Volkstümlicher Schlager", "German Schlager", 100, 125, "Akkordeon, Gitarre"), G("Schlager & Volksmusik", "Blasmusik", "brass band", 100, 130, "Blaskapelle"),
        G("Schlager & Volksmusik", "Polka", "polka", 110, 140, "Akkordeon, Tuba"), G("Schlager & Volksmusik", "Walzer", "waltz", 80, 100, "Streicher, Akkordeon"),
        G("Schlager & Volksmusik", "Liedermacher", "German singer-songwriter", 80, 120, "Akustikgitarre"), G("Schlager & Volksmusik", "Chanson", "chanson", 70, 110, "Akkordeon, Klavier"),
        G("Schlager & Volksmusik", "Kinderlied", "children's music", 100, 130, "Glockenspiel, Gitarre"), G("Schlager & Volksmusik", "Weihnachtslied", "christmas", 70, 120, "Glocken, Streicher"),
        // Welt
        G("Welt", "Reggae", "reggae", 70, 90, "Offbeat-Gitarre, Bass"), G("Welt", "Dancehall", "dancehall", 90, 105, "Riddim"),
        G("Welt", "Reggaeton", "reggaeton", 88, 100, "Dembow-Rhythmus"), G("Welt", "Latin Pop", "latin pop", 95, 120, "Percussion, Gitarre"),
        G("Welt", "Salsa", "salsa", 160, 220, "Bläser, Congas"), G("Welt", "Bachata", "bachata", 120, 140, "Requinto-Gitarre"),
        G("Welt", "Cumbia", "cumbia", 85, 110, "Akkordeon, Percussion"), G("Welt", "Tango", "tango", 110, 130, "Bandoneon, Violine", true),
        G("Welt", "Flamenco", "flamenco", 90, 180, "Flamencogitarre, Klatschen", true), G("Welt", "Samba", "samba", 95, 105, "Surdo, Cavaquinho"),
        G("Welt", "Afrobeats", "afrobeats", 100, 115, "Afro-Percussion, Synths"), G("Welt", "Afrobeat", "afrobeat", 100, 125, "Bläser, Percussion"),
        G("Welt", "Amapiano", "amapiano", 110, 115, "Log-Drum, Klavier"), G("Welt", "Bollywood", "bollywood", 90, 130, "Sitar, Tabla, Streicher"),
        G("Welt", "Arabische Musik", "arabic music", 80, 120, "Oud, Darbuka", true), G("Welt", "Türkischer Pop", "Turkish pop", 100, 125, "Baglama, Percussion"),
        G("Welt", "Keltisch", "celtic", 90, 140, "Fiddle, Tin Whistle"), G("Welt", "Balkan", "Balkan brass", 120, 160, "Blasinstrumente, Akkordeon"),
        G("Welt", "Klezmer", "klezmer", 100, 160, "Klarinette, Violine", true), G("Welt", "Fado", "fado", 60, 90, "portugiesische Gitarre", true),
        G("Welt", "Calypso", "calypso", 100, 120, "Steel Drums"), G("Welt", "Hawaiianisch", "hawaiian", 80, 110, "Ukulele, Slide-Gitarre"),
        G("Welt", "Mariachi", "mariachi", 100, 180, "Trompeten, Violinen, Gitarrón"), G("Welt", "Japanische Traditionelle Musik", "traditional Japanese", 60, 100, "Koto, Shakuhachi"),
        // Country & Folk
        G("Country & Folk", "Country", "country", 90, 120, "Steelgitarre, Fiddle"), G("Country & Folk", "Country Pop", "country pop", 95, 120, "Akustikgitarre, Beat"),
        G("Country & Folk", "Bluegrass", "bluegrass", 120, 170, "Banjo, Mandoline"), G("Country & Folk", "Americana", "americana", 80, 120, "Akustikgitarre, Dobro"),
        G("Country & Folk", "Folk", "folk", 80, 120, "Akustikgitarre"), G("Country & Folk", "Indie-Folk", "indie folk", 80, 115, "Akustikgitarre, Banjo"),
        G("Country & Folk", "Folk Rock", "folk rock", 100, 130, "Gitarren, Schlagzeug"), G("Country & Folk", "Singer-Songwriter", "singer-songwriter", 70, 110, "Akustikgitarre, Klavier"),
        G("Country & Folk", "Western", "western", 90, 120, "Twang-Gitarre"), G("Country & Folk", "Sea Shanty", "sea shanty", 90, 120, "Chor, Fiddle"),
        // Chill & Ambient
        G("Chill & Ambient", "Ambient", "ambient", 60, 90, "Flächen, Hall"), G("Chill & Ambient", "Chillout", "chillout", 80, 105, "sanfte Beats, Pads"),
        G("Chill & Ambient", "Lo-Fi", "lo-fi", 70, 90, "Rhodes, Vinyl"), G("Chill & Ambient", "Downtempo", "downtempo", 80, 110, "Beats, Samples"),
        G("Chill & Ambient", "Trip-Hop", "trip hop", 75, 95, "Breakbeats, Samples", true), G("Chill & Ambient", "New Age", "new age", 60, 90, "Pads, Flöte"),
        G("Chill & Ambient", "Meditation", "meditation", 50, 70, "Klangschalen, Pads"), G("Chill & Ambient", "Chillhop", "chillhop", 80, 95, "Jazz-Samples, Beats"),
        G("Chill & Ambient", "Vaporwave", "vaporwave", 70, 100, "verlangsamte Samples"),
        // Spezial
        G("Spezial", "Acapella", "a cappella", 70, 120, "nur Stimmen"), G("Spezial", "Chor", "choir", 60, 110, "Chor"),
        G("Spezial", "Gregorianik", "gregorian chant", 50, 70, "Männerchor"), G("Spezial", "Marsch", "march", 110, 125, "Blaskapelle, Trommeln"),
        G("Spezial", "Hymne", "anthem", 70, 110, "Orchester, Chor"), G("Spezial", "Werbejingle", "jingle", 100, 130, "Pop-Instrumentierung"),
        G("Spezial", "Podcast-Intro", "jingle", 100, 125, "Beat, Synths"), G("Spezial", "Industrial", "industrial", 110, 140, "metallische Beats", true),
        G("Spezial", "Gothic", "gothic rock", 100, 130, "düstere Gitarren, Synths", true), G("Spezial", "Ska", "ska", 140, 180, "Bläser, Offbeat"),
        G("Spezial", "Emo", "emo", 120, 170, "E-Gitarre", true), G("Spezial", "Experimentell", "experimental", 60, 160, "ungewöhnliche Klänge"),
    ];

    public static IEnumerable<Genre> InGroup(string group) => All.Where(g => g.Group == group);

    /// <summary>Suche nach Name oder Begriff (Groß-/Kleinschreibung egal).</summary>
    public static IEnumerable<Genre> Search(string q) =>
        string.IsNullOrWhiteSpace(q) ? All : All.Where(g => g.Name.Contains(q.Trim(), StringComparison.OrdinalIgnoreCase) || g.Tag.Contains(q.Trim(), StringComparison.OrdinalIgnoreCase));

    public static Genre? Find(string name) => All.FirstOrDefault(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Stil-Beschreibung für ACE-Step aus gewählten Genres (Begriffe, die ACE-Step kennt) und Zusatz.</summary>
    public static string Style(IEnumerable<Genre> picked, string extra = "") =>
        string.Join(", ", picked.Select(g => g.Tag).Append(extra.Trim()).Where(s => s.Length > 0).Distinct());

    // ───────────── M8 Stimmen ─────────────
    /// <summary>Gesangsarten (M8): deutscher Name → Beschreibung für ACE-Step.</summary>
    public static readonly (string Name, string Tag)[] Voices =
    [
        ("Frauenstimme", "female vocals"), ("Männerstimme", "male vocals"), ("Duett Frau & Mann", "male and female duet vocals"),
        ("Chor", "choir vocals"), ("Rap", "rap vocals"), ("Rap und Gesang", "rap verses and sung chorus"),
        ("Flüstern / gehaucht", "breathy whispered vocals"), ("Kraftvoll (Belting)", "powerful belting vocals"), ("Kopfstimme (Falsett)", "falsetto vocals"),
        ("Rau / rauchig", "raspy gritty vocals"), ("Kinderstimme", "child vocals"), ("Opernstimme", "operatic vocals"),
        ("Sprechgesang", "spoken word vocals"), ("Mehrstimmig", "layered vocal harmonies"),
    ];
}
