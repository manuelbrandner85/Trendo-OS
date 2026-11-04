using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>KI-Musikvideo, Baustein A (freigegeben 04.11.2026): Trendo versteht das Lied – Takt und Aufbau (A3), jede Liedzeile mit Zeit (A2),
/// wer singt wann: Sängerin, Sänger, zwei Stimmen, beide (A1) und worum es geht (A4). Grundlage für Drehbuch, Lippen und Schnitt.
/// Ausdrückliche Angaben im Liedtext (z. B. „[Refrain – beide]“, „[Strophe 2 – Sie]“) gehen immer vor der Erkennung aus dem Klang.</summary>
public static partial class SongUnderstanding
{
    public sealed record Singer(int Id, string Gender, string Voice);
    public sealed record Phrase(double Start, double End, int Singer, bool Both, double Midi);
    public sealed record Word(string Text, double Start, double End);
    public sealed record Section(double Start, double End, string Part, double Energy);
    public sealed record Line(string Text, string Part, double Start, double End, string Who, bool Timed);
    public sealed record Theme(string Topic, string Story, string Mood, IReadOnlyList<string> Places, string TimeOfDay, IReadOnlyList<string> Colors);
    public sealed record Result(double Bpm, double Duration, IReadOnlyList<double> Beats, IReadOnlyList<Section> Sections, IReadOnlyList<Singer> Singers, IReadOnlyList<Line> Lines, Theme? Theme, string Source);

    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Ergebnis von <c>trendo_analyse.py --saenger</c>: Stimmen (Geschlecht, Stimmlage) und gesungene Phrasen.</summary>
    public static (List<Singer> Singers, List<Phrase> Phrases) ParseSingers(string json)
    {
        using var d = JsonDocument.Parse(json); var r = d.RootElement;
        var singers = r.GetProperty("singers").EnumerateArray().Select(x => new Singer(x.GetProperty("id").GetInt32(), x.GetProperty("gender").GetString() ?? "?", x.GetProperty("voice").GetString() ?? "")).ToList();
        var phrases = r.GetProperty("phrases").EnumerateArray().Select(x => new Phrase(x.GetProperty("start").GetDouble(), x.GetProperty("end").GetDouble(), x.GetProperty("singer").GetInt32(),
            x.TryGetProperty("both", out var b) && b.GetBoolean(), x.TryGetProperty("midi", out var m) ? m.GetDouble() : 0)).ToList();
        return (singers, phrases);
    }

    /// <summary>Wortzeiten aus whisper.cpp (<c>-ml 1 -oj</c>): je Eintrag ein Wort mit Millisekunden.</summary>
    public static List<Word> ParseWhisperWords(string json)
    {
        using var d = JsonDocument.Parse(json);
        var res = new List<Word>();
        foreach (var t in d.RootElement.GetProperty("transcription").EnumerateArray())
        {
            var text = (t.GetProperty("text").GetString() ?? "").Trim();
            if (text.Length == 0 || text.StartsWith('[') || text.StartsWith('(')) continue; // [Musik], (Gesang)
            var o = t.GetProperty("offsets");
            res.Add(new Word(text, o.GetProperty("from").GetDouble() / 1000, o.GetProperty("to").GetDouble() / 1000));
        }
        return res;
    }

    /// <summary>Abschnitte und Schläge aus <c>trendo_analyse.py --abschnitte</c>.</summary>
    public static (double Bpm, double Duration, List<double> Beats, List<(double Start, double End, double Energy)> Parts) ParseSections(string json)
    {
        using var d = JsonDocument.Parse(json); var r = d.RootElement;
        var beats = r.TryGetProperty("beats", out var b) ? b.EnumerateArray().Select(x => x.GetDouble()).ToList() : [];
        var parts = r.GetProperty("sections").EnumerateArray().Select(x => (x.GetProperty("start").GetDouble(), x.GetProperty("end").GetDouble(), x.GetProperty("energy").GetDouble())).ToList();
        return (r.GetProperty("bpm").GetDouble(), r.GetProperty("duration").GetDouble(), beats, parts);
    }

    /// <summary>Liedtext in Zeilen mit Abschnitt („Strophe 1“, „Refrain“ …) und ausdrücklicher Stimme aus der Abschnittsangabe.</summary>
    public static List<(string Text, string Part, string? Who)> LyricLines(string lyrics)
    {
        var res = new List<(string, string, string?)>();
        string part = "Strophe"; string? who = null;
        foreach (var raw in (lyrics ?? "").Replace("\r", "").Split('\n'))
        {
            var l = raw.Trim();
            if (l.Length == 0) continue;
            if (l.StartsWith('[') && l.EndsWith(']'))
            {
                var tag = l[1..^1].Trim();
                if (tag.Equals("Instrumental", StringComparison.OrdinalIgnoreCase)) continue;
                var bits = tag.Split(['–', '-', ':', '|'], 2, StringSplitOptions.TrimEntries);
                part = PartName(bits[0]);
                who = bits.Length > 1 ? WhoFromTag(bits[1]) : WhoFromTag(bits[0]);
                continue;
            }
            // Zeilen wie „(Sie) …“ oder „Er: …“ legen die Stimme nur für diese Zeile fest
            string? lineWho = null;
            var m = LineWhoRx().Match(l);
            if (m.Success && WhoFromTag(m.Groups[1].Value) is { } w) { lineWho = w; l = l[m.Length..].Trim(); }
            if (l.Length > 0) res.Add((l, part, lineWho ?? who));
        }
        return res;
    }

    [GeneratedRegex(@"^\(?\s*(sie|er|beide|both|he|she|frau|mann|alle|duett)\s*[\):]\s*", RegexOptions.IgnoreCase)]
    private static partial Regex LineWhoRx();

    static string PartName(string tag)
    {
        var t = tag.ToLowerInvariant();
        var n = Regex.Match(t, @"\d+").Value;
        string Num(string s) => n.Length > 0 ? $"{s} {n}" : s;
        return t switch
        {
            _ when t.Contains("pre") && t.Contains("chorus") || t.Contains("pre-refrain") || t.Contains("vorrefrain") => "Vor-Refrain",
            _ when t.Contains("refrain") || t.Contains("chorus") || t.Contains("hook") => "Refrain",
            _ when t.Contains("strophe") || t.Contains("verse") || t.Contains("vers") => Num("Strophe"),
            _ when t.Contains("bridge") || t.Contains("brücke") => "Bridge",
            _ when t.Contains("intro") => "Intro",
            _ when t.Contains("outro") || t.Contains("schluss") || t.Contains("ende") => "Outro",
            _ when t.Contains("rap") => Num("Rap"),
            _ => tag.Length > 0 ? char.ToUpper(tag[0], Inv) + tag[1..] : "Strophe",
        };
    }

    /// <summary>„beide“, „Sie“, „Er“ … → "beide" | "w" | "m"; sonst null.</summary>
    public static string? WhoFromTag(string tag)
    {
        var t = " " + tag.ToLowerInvariant() + " ";
        if (Regex.IsMatch(t, @"\b(beide|both|duett|duet|alle|zusammen)\b")) return "beide";
        if (Regex.IsMatch(t, @"\b(sie|she|frau|female|sängerin|saengerin|woman|girl)\b")) return "w";
        if (Regex.IsMatch(t, @"\b(er|he|mann|male|sänger|saenger|man|boy)\b")) return "m";
        return null;
    }

    static string Norm(string s) => new string(s.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD)
        .Where(c => char.IsLetterOrDigit(c)).ToArray());

    /// <summary>A2: Liedzeilen den erkannten Wörtern zuordnen (vorwärts, jede Zeile im Fenster mit der besten Wortübereinstimmung).
    /// Ohne passende Wörter: Zeit aus den gesungenen Phrasen. Zeilen ohne Zeit bleiben ehrlich als „ohne Zeit“ markiert.</summary>
    public static List<(string Text, string Part, string? Who, double Start, double End, bool Timed)> Time(
        IReadOnlyList<(string Text, string Part, string? Who)> lines, IReadOnlyList<Word> words, IReadOnlyList<Phrase> phrases)
    {
        var res = new List<(string, string, string?, double, double, bool)>();
        var w = words.Select(x => (N: Norm(x.Text), x.Start, x.End)).Where(x => x.N.Length > 0).ToList();
        var pos = 0;
        var untimed = new List<int>();
        foreach (var (text, part, who) in lines)
        {
            var toks = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Norm).Where(t => t.Length > 0).ToList();
            int bestAt = -1, bestLen = 0; double bestScore = 0;
            for (var a = pos; a < Math.Min(w.Count, pos + 80) && toks.Count > 0; a++)
            {
                var len = Math.Min(w.Count - a, toks.Count + 2);
                var win = w.Skip(a).Take(len).Select(x => x.N).ToList();
                var hit = toks.Count(t => win.Any(x => x == t || (t.Length > 3 && x.Length > 3 && (x.StartsWith(t[..^1]) || t.StartsWith(x[..^1])))));
                var score = (double)hit / toks.Count - (a - pos) * 0.002;
                if (score > bestScore) { bestScore = score; bestAt = a; bestLen = Math.Min(len, Math.Max(1, toks.Count)); }
            }
            if (bestAt >= 0 && bestScore >= 0.5)
            {
                var end = Math.Min(w.Count - 1, bestAt + bestLen - 1);
                res.Add((text, part, who, w[bestAt].Start, w[end].End, true));
                pos = end + 1;
            }
            else { untimed.Add(res.Count); res.Add((text, part, who, 0, 0, false)); }
        }
        // Lücken mit gesungenen Phrasen füllen (z. B. ohne Whisper-Modell): Phrasen zwischen den Nachbarzeilen gleichmäßig verteilen
        if (untimed.Count > 0 && phrases.Count > 0)
        {
            if (res.All(r => !r.Item6))
            {
                var al = LyricTiming.Align(res.Select(r => r.Item1).ToList(), phrases.Select(p => (p.Start, p.End)).ToList());
                for (var i = 0; i < res.Count && i < al.Count; i++) res[i] = (res[i].Item1, res[i].Item2, res[i].Item3, al[i].Start, al[i].End, true);
            }
            else
                foreach (var i in untimed)
                {
                    var prevEnd = res.Take(i).Where(r => r.Item6).Select(r => r.Item5).DefaultIfEmpty(0).Max();
                    var nextStart = res.Skip(i + 1).Where(r => r.Item6).Select(r => r.Item4).DefaultIfEmpty(double.MaxValue).Min();
                    var p = phrases.FirstOrDefault(x => x.Start >= prevEnd - 0.1 && x.End <= nextStart + 0.1);
                    if (p is not null) res[i] = (res[i].Item1, res[i].Item2, res[i].Item3, p.Start, p.End, true);
                }
        }
        return res;
    }

    /// <summary>A1: Wer singt diese Zeit? Ausdrückliche Angabe zuerst, sonst die Phrasen mit der größten Überdeckung.</summary>
    public static string WhoSings(double start, double end, string? tagged, IReadOnlyList<Phrase> phrases, IReadOnlyList<Singer> singers)
    {
        string Label(int id)
        {
            var s = singers.FirstOrDefault(x => x.Id == id);
            var g = s?.Gender == "w" ? "Sängerin" : s?.Gender == "m" ? "Sänger" : "Stimme";
            var same = singers.Count(x => x.Gender == s?.Gender);
            return same > 1 ? $"{g} {singers.Where(x => x.Gender == s?.Gender).ToList().IndexOf(s!) + 1}" : g;
        }
        if (tagged == "beide") return "beide";
        if (tagged is "w" or "m")
        {
            var match = singers.FirstOrDefault(x => x.Gender == tagged);
            return match is not null ? Label(match.Id) : tagged == "w" ? "Sängerin" : "Sänger";
        }
        if (end <= start || phrases.Count == 0) return singers.Count == 1 ? Label(singers[0].Id) : "?";
        var over = phrases.Select(p => (p, o: Math.Min(end, p.End) - Math.Max(start, p.Start))).Where(x => x.o > 0).ToList();
        if (over.Count == 0) return singers.Count == 1 ? Label(singers[0].Id) : "?";
        var total = over.Sum(x => x.o);
        if (over.Where(x => x.p.Both).Sum(x => x.o) > total * 0.4) return "beide";
        var by = over.GroupBy(x => x.p.Singer).Select(g => (Id: g.Key, O: g.Sum(x => x.o))).OrderByDescending(x => x.O).ToList();
        if (by.Count > 1 && by[1].O > total * 0.35) return "beide"; // beide Stimmen etwa gleich lang in dieser Zeile
        return Label(by[0].Id);
    }

    /// <summary>Abschnitt jeder erkannten Klangstrecke: Liedtext-Abschnitt der Zeilen darin, sonst Intro/Zwischenspiel/Outro.</summary>
    public static List<Section> NameSections(IReadOnlyList<(double Start, double End, double Energy)> parts, IReadOnlyList<Line> lines, double duration)
    {
        var res = new List<Section>();
        for (var i = 0; i < parts.Count; i++)
        {
            var (a, b, e) = parts[i];
            var inside = lines.Where(l => l.Timed && l.Start < b && l.End > a).GroupBy(l => l.Part).OrderByDescending(g => g.Sum(l => Math.Min(b, l.End) - Math.Max(a, l.Start))).FirstOrDefault()?.Key;
            res.Add(new Section(a, b, inside ?? (i == 0 ? "Intro" : b >= duration - 0.5 ? "Outro" : "Zwischenspiel"), e));
        }
        return res;
    }

    /// <summary>Alles zusammenführen.</summary>
    public static Result Build(string lyrics, string sectionsJson, string? singersJson, string? whisperJson, Theme? theme)
    {
        var (bpm, dur, beats, parts) = ParseSections(sectionsJson);
        var (singers, phrases) = singersJson is null ? ([], []) : ParseSingers(singersJson);
        var words = whisperJson is null ? [] : ParseWhisperWords(whisperJson);
        var timed = Time(LyricLines(lyrics), words, phrases);
        var lines = timed.Select(t => new Line(t.Text, t.Part, Math.Round(t.Start, 2), Math.Round(t.End, 2), WhoSings(t.Start, t.End, t.Who, phrases, singers), t.Timed)).ToList();
        var src = words.Count > 0 ? "Wortzeiten (Whisper)" : phrases.Count > 0 ? "gesungene Phrasen" : "nur Liedtext";
        return new Result(bpm, dur, beats, NameSections(parts, lines, dur), singers, lines, theme, src);
    }

    // ───────────── A4: worum es geht ─────────────
    public static string ThemePrompt(string title, string style, string lyrics) =>
        "Du bist Regisseur für Musikvideos. Lies den Liedtext und beschreibe, worum es geht – als Grundlage für ein Drehbuch.\n" +
        "Antworte NUR mit einem JSON-Objekt mit diesen Feldern (alle Texte auf Deutsch, kurz):\n" +
        "{\"thema\": \"…\", \"geschichte\": \"2–3 Sätze, was im Video passieren könnte\", \"stimmung\": \"…\", \"orte\": [\"…\"], \"tageszeit\": \"…\", \"farben\": [\"…\"]}\n" +
        "Keine echten oder bekannten Personen, keine Markennamen.\n\n" +
        $"Titel: {title}\nStil: {style}\nLiedtext:\n{(lyrics.Length > 4000 ? lyrics[..4000] : lyrics)}";

    /// <summary>Antwort der KI lesen (JSON auch mitten im Text); unbrauchbar = null.</summary>
    public static Theme? ParseTheme(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var a = text.IndexOf('{'); var b = text.LastIndexOf('}');
        if (a < 0 || b <= a) return null;
        try
        {
            using var d = JsonDocument.Parse(text[a..(b + 1)]); var r = d.RootElement;
            string S(string k) => r.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
            List<string> L(string k) => r.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).Where(x => x.Length > 0).Take(6).ToList() : [];
            var t = new Theme(S("thema"), S("geschichte"), S("stimmung"), L("orte"), S("tageszeit"), L("farben"));
            return t.Topic.Length == 0 && t.Story.Length == 0 ? null : t;
        }
        catch (JsonException) { return null; }
    }

    public static string Save(Result r) => JsonSerializer.Serialize(r, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    public static Result? Load(string json) { try { return JsonSerializer.Deserialize<Result>(json); } catch (JsonException) { return null; } }

    /// <summary>Kurzfassung für die Oberfläche.</summary>
    public static string Summary(Result r)
    {
        var who = r.Singers.Count switch
        {
            0 => "Stimmen noch nicht erkannt",
            1 => r.Singers[0].Gender == "w" ? "eine Sängerin" : r.Singers[0].Gender == "m" ? "ein Sänger" : "eine Stimme",
            _ => r.Singers.All(s => s.Gender == "w") ? "zwei Sängerinnen" : r.Singers.All(s => s.Gender == "m") ? "zwei Sänger" : "Duett: Sängerin und Sänger",
        };
        var both = r.Lines.Count(l => l.Who == "beide");
        return $"{r.Bpm:0} BPM · {r.Sections.Count} Abschnitte · {r.Lines.Count(l => l.Timed)} von {r.Lines.Count} Zeilen mit Zeit · {who}" + (both > 0 ? $" · {both} Zeilen zusammen" : "");
    }
}
