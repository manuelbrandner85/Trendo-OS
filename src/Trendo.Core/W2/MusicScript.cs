using System.Text.Json;

namespace Trendo.Core;

/// <summary>KI-Musikvideo, Baustein B (freigegeben 04.11.2026): Drehbuch wie bei einem echten Videodreh – Konzept, Figuren, Drehorte und eine
/// Einstellungsliste (Zeit, Einstellungsgröße, Kamerabewegung, Licht, wer im Bild ist und ob sie/er singt). Jede Einstellung ist bearbeitbar,
/// löschbar und neu würfelbar (B2). Figuren sind immer erfunden (B3: eine feste Beschreibung je Figur, damit sie überall gleich aussieht).
/// Die Video-Beschreibung ist englisch (so verstehen sie die freien Video-KIs am besten), dazu ein fester Ausschluss gegen Fehlbildungen (G7).</summary>
public static class MusicScript
{
    public sealed record Figure(string Id, string Role, string Gender, string Look);
    public sealed record Shot(int Index, double Start, double End, string Part, string Size, string Move, string Light, string Who, bool Singing, string Line, string Place, string Prompt, int Seed, string Status = "geplant");
    public sealed record Script(string Concept, string Logline, IReadOnlyList<Figure> Figures, IReadOnlyList<string> Places, string Look, IReadOnlyList<Shot> Shots);

    /// <summary>Fester Ausschluss für jede Erzeugung (G7) – Fehlbildungen, Text, Wasserzeichen.</summary>
    public const string Negative = "extra limbs, extra arms, extra legs, extra fingers, missing fingers, fused fingers, deformed hands, mutated hands, malformed limbs, " +
        "two heads, duplicate face, distorted face, asymmetric eyes, floating limbs, disconnected body parts, bad anatomy, warped body, " +
        "text, letters, subtitles, watermark, logo, blurry, low quality, jpeg artifacts, flicker, morphing";

    public static readonly (string Id, string Name)[] Concepts = [("gemischt", "Performance und Geschichte"), ("performance", "Performance (Singende im Bild)"), ("geschichte", "Geschichte erzählen")];

    static readonly string[] Sizes = ["Totale", "Halbtotale", "Halbnah", "Nah", "Groß"];
    static readonly Dictionary<string, string> SizeEn = new() { ["Totale"] = "wide establishing shot", ["Halbtotale"] = "full shot", ["Halbnah"] = "medium shot", ["Nah"] = "close-up", ["Groß"] = "extreme close-up of the face" };
    static readonly Dictionary<string, string> MoveEn = new()
    {
        ["statisch"] = "static camera", ["Fahrt rein"] = "slow dolly in", ["Fahrt raus"] = "slow dolly out", ["Seitenfahrt"] = "smooth lateral tracking shot",
        ["Schwenk"] = "slow pan", ["Handkamera"] = "subtle handheld camera", ["Kreisfahrt"] = "slow orbit around the subject", ["Kran"] = "crane shot rising up", ["Drohne"] = "slow aerial drone shot",
    };
    public static IReadOnlyList<string> SizeNames => Sizes;
    public static IReadOnlyCollection<string> MoveNames => MoveEn.Keys;

    /// <summary>B3: feste, erfundene Figurenbeschreibung je Stimme (englisch, für die Video-KI) – Alter, Haare, Kleidung passend zum Stil.</summary>
    public static List<Figure> Figures(IReadOnlyList<SongUnderstanding.Singer> singers, string style, int seed = 0)
    {
        var res = new List<Figure>();
        var hairW = new[] { "long wavy dark brown hair", "shoulder-length auburn hair", "long straight black hair", "curly honey-blonde hair" };
        var hairM = new[] { "short dark hair and light stubble", "medium-length wavy brown hair", "short curly black hair", "neat short blond hair" };
        var outfit = Outfit(style);
        var list = singers.Count > 0 ? singers : [new SongUnderstanding.Singer(0, "w", "")];
        foreach (var s in list)
        {
            var w = s.Gender == "w"; var k = (s.Id + seed) % 4;
            var n = list.Count(x => x.Gender == s.Gender) > 1 ? $" {list.Where(x => x.Gender == s.Gender).ToList().IndexOf(s) + 1}" : "";
            res.Add(new Figure($"fig{s.Id}", (w ? "Sängerin" : "Sänger") + n, s.Gender,
                $"a fictional {(w ? "woman in her" : "man in his")} late twenties, {(w ? hairW[k] : hairM[k])}, natural skin texture, {outfit}"));
        }
        return res;
    }

    static string Outfit(string style)
    {
        var s = style.ToLowerInvariant();
        return s switch
        {
            _ when s.Contains("rock") || s.Contains("metal") || s.Contains("punk") => "black leather jacket, dark jeans",
            _ when s.Contains("jazz") || s.Contains("soul") || s.Contains("blues") => "elegant dark velvet outfit",
            _ when s.Contains("hip") || s.Contains("rap") || s.Contains("trap") => "oversized streetwear, sneakers",
            _ when s.Contains("country") || s.Contains("folk") || s.Contains("akustik") || s.Contains("acoustic") => "denim jacket, light linen shirt",
            _ when s.Contains("elektro") || s.Contains("techno") || s.Contains("house") || s.Contains("synth") => "minimal modern black outfit",
            _ when s.Contains("schlager") => "bright colorful stage outfit",
            _ => "casual modern outfit in muted colors",
        };
    }

    static string LightFor(SongUnderstanding.Theme? t, double energy)
    {
        var tod = (t?.TimeOfDay ?? "").ToLowerInvariant();
        var basis = tod switch
        {
            _ when tod.Contains("nacht") => "night, city lights and soft practical lights",
            _ when tod.Contains("abend") || tod.Contains("sonnenunter") => "golden hour, warm low sun",
            _ when tod.Contains("morgen") || tod.Contains("früh") => "early morning, soft cool light, light mist",
            _ when tod.Contains("mittag") || tod.Contains("tag") => "bright daylight, natural light",
            _ => "soft natural light",
        };
        return energy >= 1.2 ? basis + ", strong contrast" : energy < 0.75 ? basis + ", gentle and intimate" : basis;
    }

    static string Mood(double energy) => energy >= 1.2 ? "energetic, emotional peak" : energy < 0.75 ? "calm, intimate" : "atmospheric";

    /// <summary>B1: Drehbuch aus dem Liedverständnis. Einstellungen höchstens <paramref name="maxShot"/> s (so lang wie ein Video-Clip),
    /// Schnitte auf ganzen Takten; im Refrain schneller, in der Strophe ruhiger; wer singt, ist im Bild.</summary>
    public static Script Plan(SongUnderstanding.Result u, string style, string concept = "gemischt", string look = "kino", int seed = 0, double maxShot = 5.0)
    {
        var figs = Figures(u.Singers, style, seed);
        var places = u.Theme?.Places is { Count: > 0 } p ? p.ToList() : DefaultPlaces(style);
        var bar = 4 * 60 / Math.Max(40, u.Bpm);
        var shots = new List<Shot>();
        var rng = new Random(seed * 7919 + (int)u.Duration);
        var sections = u.Sections.Count > 0 ? u.Sections : [new SongUnderstanding.Section(0, u.Duration, "Strophe", 1)];
        for (var si = 0; si < sections.Count; si++)
        {
            var sec = sections[si];
            var refrain = sec.Part.StartsWith("Refrain");
            var fit = Math.Max(1, (int)Math.Floor(maxShot / bar));
            var calm = Math.Max(1, Math.Min(fit, 3));
            var bars = refrain ? Math.Max(1, (int)Math.Round(calm * 0.6)) : sec.Part is "Intro" or "Outro" ? fit : calm; // Refrain schneller geschnitten
            var len = Math.Min(maxShot, bars * bar);
            var t = sec.Start; var k = 0;
            var place = places[si % places.Count];
            while (t < sec.End - 0.05)
            {
                var l = Math.Min(len, sec.End - t);
                if (l < bar * 0.75 && shots.Count > 0 && shots[^1].Part == sec.Part && shots[^1].End - shots[^1].Start + l <= maxShot + 0.01) { shots[^1] = shots[^1] with { End = Math.Round(t + l, 3) }; break; }
                var line = u.Lines.Where(x => x.Timed && x.Start < t + l && x.End > t).OrderByDescending(x => Math.Min(t + l, x.End) - Math.Max(t, x.Start)).FirstOrDefault();
                var singing = line is not null && concept != "geschichte";
                // Geschichte zeigen: in Strophen jede zweite Einstellung, im Instrumental immer, bei „Geschichte erzählen“ meistens
                var story = !singing || (concept == "gemischt" && !refrain && k % 2 == 1) || (concept == "geschichte" && k % 4 != 0);
                if (story) singing = false;
                var who = line?.Who ?? (figs.Count > 1 && k % 3 == 2 ? "beide" : figs[k % figs.Count].Role);
                var size = sec.Part switch
                {
                    "Intro" => k == 0 ? "Totale" : "Halbtotale",
                    "Outro" => k % 2 == 0 ? "Halbtotale" : "Totale",
                    _ when refrain => Sizes[(k + 2) % Sizes.Length],
                    _ => singing ? (k % 2 == 0 ? "Halbnah" : "Nah") : Sizes[k % 3],
                };
                if (singing && size is "Totale" or "Halbtotale") size = "Halbnah"; // Lippen müssen zu sehen sein
                var moves = refrain ? new[] { "Kreisfahrt", "Handkamera", "Kran", "Seitenfahrt" } : sec.Part is "Intro" ? ["Drohne", "Fahrt rein"] : sec.Part is "Outro" ? ["Fahrt raus", "Kran"] : ["Fahrt rein", "Seitenfahrt", "statisch", "Schwenk"];
                var move = moves[(k + rng.Next(2)) % moves.Length];
                if (size == "Groß" && move is "Kreisfahrt" or "Drohne" or "Kran") move = "statisch"; // ruhige Nahaufnahmen: weniger Fehlbildungen (G7)
                var light = LightFor(u.Theme, sec.Energy);
                var sh = new Shot(shots.Count, Math.Round(t, 3), Math.Round(t + l, 3), sec.Part, size, move, light, who, singing, line?.Text ?? "", place, "", seed * 1000 + shots.Count);
                shots.Add(sh with { Prompt = Prompt(sh, figs, style, u.Theme, sec.Energy) });
                t += l; k++;
            }
        }
        var logline = u.Theme is { } th && th.Story.Length > 0 ? th.Story : $"Musikvideo zu einem {style}-Lied" + (figs.Count > 1 ? " mit zwei Stimmen." : ".");
        return new Script(concept, logline, figs, places, look, shots);
    }

    static List<string> DefaultPlaces(string style)
    {
        var s = style.ToLowerInvariant();
        if (s.Contains("rock") || s.Contains("metal")) return ["verlassene Lagerhalle", "nächtliche Straße", "Bühne mit Gegenlicht"];
        if (s.Contains("jazz") || s.Contains("soul")) return ["kleiner Jazzclub", "regennasse Straße bei Nacht", "Altbauwohnung mit Klavier"];
        if (s.Contains("country") || s.Contains("folk") || s.Contains("akustik") || s.Contains("acoustic")) return ["weites Feld", "Landstraße", "Veranda eines Holzhauses"];
        if (s.Contains("elektro") || s.Contains("synth") || s.Contains("techno")) return ["Neonstraße bei Nacht", "Dachterrasse über der Stadt", "leere U-Bahn-Station"];
        return ["Dachterrasse über der Stadt", "Strand im Abendlicht", "helles Studio mit großem Fenster"];
    }

    /// <summary>Englische Beschreibung für die Video-KI aus den Feldern der Einstellung (wird nach jeder Änderung neu gebaut).</summary>
    public static string Prompt(Shot s, IReadOnlyList<Figure> figs, string style, SongUnderstanding.Theme? theme, double energy = 1)
    {
        var people = s.Who == "beide" ? figs : figs.Where(f => f.Role == s.Who || s.Who.StartsWith(f.Role)).DefaultIfEmpty(figs.FirstOrDefault()).Where(f => f is not null).ToList();
        var who = people.Count == 0 ? "" : string.Join(" and ", people.Select(f => f!.Look));
        var action = s.Singing ? (people.Count > 1 ? "singing together towards each other, mouths moving clearly with the lyrics" : "singing with emotion, mouth moving clearly with the lyrics, looking slightly past the camera")
            : people.Count > 0 ? "acting naturally in the scene, not singing" : "no people";
        var hands = s.Size is "Nah" or "Groß" ? ", hands out of frame" : s.Singing ? ", one hand holding a microphone" : "";
        return $"cinematic music video, {SizeEn.GetValueOrDefault(s.Size, "medium shot")}, {MoveEn.GetValueOrDefault(s.Move, "static camera")}, " +
            (who.Length > 0 ? $"{who}, {action}{hands}, " : "") +
            $"location: {s.Place}, {s.Light}, {Mood(energy)}, {style.Trim().TrimEnd(',')}" + (theme?.Colors is { Count: > 0 } c ? $", color palette {string.Join(" and ", c)}" : "") +
            ", shot on 35mm film, anamorphic lens, shallow depth of field, natural skin texture, correct human anatomy, photorealistic";
    }

    // ───────────── B2: bearbeiten ─────────────
    public static Script Edit(Script sc, int index, Func<Shot, Shot> change, string style, SongUnderstanding.Theme? theme)
    {
        var list = sc.Shots.ToList();
        var s = change(list[index]);
        list[index] = s with { Prompt = s.Prompt == list[index].Prompt ? Prompt(s, sc.Figures, style, theme) : s.Prompt, Status = "geplant" };
        return sc with { Shots = list };
    }

    /// <summary>Einstellung löschen: die vorige wird bis zum Ende verlängert (keine Lücke im Video).</summary>
    public static Script Remove(Script sc, int index)
    {
        var list = sc.Shots.ToList();
        if (list.Count <= 1) return sc;
        var gone = list[index]; list.RemoveAt(index);
        if (index > 0) list[index - 1] = list[index - 1] with { End = gone.End };
        else list[0] = list[0] with { Start = gone.Start };
        return sc with { Shots = list.Select((x, i) => x with { Index = i }).ToList() };
    }

    /// <summary>Neu würfeln: anderer Zufallswert, andere Kamerabewegung.</summary>
    public static Script Reroll(Script sc, int index, string style, SongUnderstanding.Theme? theme)
    {
        var s = sc.Shots[index];
        var moves = MoveEn.Keys.ToList();
        var next = moves[(moves.IndexOf(s.Move) + 1 + s.Seed % 3) % moves.Count];
        if (s.Singing && next is "Drohne") next = "Fahrt rein";
        return Edit(sc, index, x => x with { Seed = x.Seed + 101, Move = next }, style, theme);
    }

    public static string Save(Script s) => JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    public static Script? Load(string json) { try { return JsonSerializer.Deserialize<Script>(json); } catch (JsonException) { return null; } }

    public static string Summary(Script s) =>
        $"{Concepts.FirstOrDefault(c => c.Id == s.Concept).Name ?? s.Concept} · {s.Shots.Count} Einstellungen · {s.Shots.Count(x => x.Singing)} mit Gesang im Bild · Figuren: {string.Join(", ", s.Figures.Select(f => f.Role))} · Orte: {string.Join(", ", s.Places)}";
}
