using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Trendo.Core.Media;

namespace Trendo.Core.Film;

public enum FountainKind { Heading, Action, Character, Parenthetical, Dialogue, Transition, Section, Synopsis, Shot }

public sealed record FountainLine(FountainKind Kind, string Text, int LineNo);

public sealed record FountainScript(IReadOnlyDictionary<string, string> TitlePage, IReadOnlyList<FountainLine> Lines, string PlanText)
{
    public string Title => TitlePage.TryGetValue("title", out var t) ? t : TitlePage.TryGetValue("titel", out var d) ? d : "";
}

/// <summary>Film-Studio F2 (08.11.2026): Leser für das offene Drehbuch-Format Fountain (eigener Code nach der Beschreibung auf fountain.io), dazu deutsche Szenenköpfe (INNEN./AUSSEN.).
/// Eine Trendo-Ergänzung: Zeilen wie „- Totale: Bahnhof bei Nacht (4 s)“ sind Einstellungen – dieselbe Schreibweise wie im Shot-Plan des Videoschnitts.</summary>
public static partial class Fountain
{
    const string Keyword = "INT|EXT|EST|I/E|INNEN/AUSSEN|AUSSEN/INNEN|INNEN|AUSSEN";

    [GeneratedRegex(@"^\s*(?:\d+\s*[.)]?\s+)?(?:" + Keyword + @")\.\s*\S", RegexOptions.IgnoreCase)] private static partial Regex DottedHead();
    [GeneratedRegex(@"^\s*(?:\d+\s*[.)]?\s+)?(?:" + Keyword + @")\s+\S")] private static partial Regex PlainHead();
    [GeneratedRegex(@"\s*#([\w.-]+)#\s*$")] private static partial Regex SceneNumber();
    [GeneratedRegex(@"^\s*(\d+\s*[.)]?\s+)?(I/E|EST)\b\.?", RegexOptions.IgnoreCase)] private static partial Regex ForeignKeyword();
    [GeneratedRegex(@"^\s*[-•*]\s*([^:]{2,30}):\s*.+$")] private static partial Regex ShotLine();
    [GeneratedRegex(@"\[\[.*?\]\]", RegexOptions.Singleline)] private static partial Regex Note();
    [GeneratedRegex(@"\s*\([^)]*\)\s*$")] private static partial Regex Extension();
    [GeneratedRegex(@"^([A-Za-zÄÖÜäöü ]{2,20}):\s*(.*)$")] private static partial Regex TitleKey();

    static readonly HashSet<string> TitleKeys = new(StringComparer.OrdinalIgnoreCase)
        { "title", "titel", "credit", "author", "authors", "autor", "autorin", "source", "quelle", "draft date", "datum", "contact", "kontakt", "copyright", "notes" };

    /// <summary>Boneyard (/* … */) und Notizen entfernen; Zeilennummern bleiben erhalten.</summary>
    static string StripComments(string text)
    {
        var sb = new StringBuilder(text.Length); var i = 0;
        while (i < text.Length)
        {
            var open = text.IndexOf("/*", i, StringComparison.Ordinal);
            if (open < 0) { sb.Append(text, i, text.Length - i); break; }
            sb.Append(text, i, open - i);
            var close = text.IndexOf("*/", open + 2, StringComparison.Ordinal);
            var end = close < 0 ? text.Length : close + 2;
            for (var k = open; k < end; k++) if (text[k] == '\n') sb.Append('\n');
            i = end;
        }
        return Note().Replace(sb.ToString(), m => new string('\n', m.Value.Count(c => c == '\n')));
    }

    static bool IsUpper(string s) => s.Any(char.IsLetter) && s == s.ToUpperInvariant();

    static bool IsHeading(string t) => DottedHead().IsMatch(t) || (PlainHead().IsMatch(t) && IsUpper(t));

    static bool IsShot(string t) => ShotLine().Match(t) is { Success: true } m && ShotPlan.Types.Contains(m.Groups[1].Value.Trim(), StringComparer.OrdinalIgnoreCase);

    /// <summary>Szenenkopf in der Schreibweise des Shot-Plans.</summary>
    static string PlanHeading(string text, bool forced)
    {
        var t = text.Trim(); var number = "";
        if (SceneNumber().Match(t) is { Success: true } n) { number = n.Groups[1].Value + " "; t = t[..n.Index]; }
        t = ForeignKeyword().Replace(t, m => m.Groups[1].Value + (m.Groups[2].Value.Equals("EST", StringComparison.OrdinalIgnoreCase) ? "AUSSEN." : "INNEN/AUSSEN."));
        if (forced && !IsHeading(t)) t = "INNEN/AUSSEN. " + t;
        return number + t;
    }

    public static FountainScript Parse(string? source)
    {
        var raw = StripComments((source ?? "").Replace("\r\n", "\n").Replace('\r', '\n')).Split('\n');
        var title = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = new List<FountainLine>(); var plan = new string[raw.Length]; Array.Fill(plan, "");
        var i = 0;
        while (i < raw.Length && raw[i].Trim().Length == 0) i++;
        if (i < raw.Length && TitleKey().Match(raw[i]) is { Success: true } first && TitleKeys.Contains(first.Groups[1].Value.Trim()))
        {
            string? key = null;
            for (; i < raw.Length && raw[i].Trim().Length > 0; i++)
            {
                if (char.IsWhiteSpace(raw[i][0]) && key is not null) { title[key] = (title[key] + " " + raw[i].Trim()).Trim(); continue; }
                if (TitleKey().Match(raw[i]) is { Success: true } m) { key = m.Groups[1].Value.Trim().ToLowerInvariant(); title[key] = m.Groups[2].Value.Trim(); }
            }
        }
        for (; i < raw.Length; i++)
        {
            var text = raw[i].TrimEnd(); var t = text.Trim();
            if (t.Length == 0 || t.StartsWith("===", StringComparison.Ordinal)) continue;
            var prevBlank = i == 0 || raw[i - 1].Trim().Length == 0;
            var nextBlank = i + 1 >= raw.Length || raw[i + 1].Trim().Length == 0;
            void Add(FountainKind k, string v) => lines.Add(new(k, v, i + 1));

            if (t.StartsWith('!')) { Add(FountainKind.Action, t[1..].Trim()); continue; }
            if (t.StartsWith('#')) { Add(FountainKind.Section, t.TrimStart('#').Trim()); continue; }
            if (t.StartsWith('=')) { Add(FountainKind.Synopsis, t[1..].Trim()); continue; }
            if (t.StartsWith('.') && t.Length > 1 && t[1] != '.') { var p = PlanHeading(t[1..], true); plan[i] = p; Add(FountainKind.Heading, p); continue; }
            if (IsShot(t)) { plan[i] = t; Add(FountainKind.Shot, t.TrimStart('-', '•', '*', ' ')); continue; }
            if (prevBlank && IsHeading(t)) { var p = PlanHeading(t, false); plan[i] = p; Add(FountainKind.Heading, p); continue; }
            if (t.StartsWith('>') && t.EndsWith('<')) { Add(FountainKind.Action, t.Trim('>', '<', ' ')); continue; }
            if (t.StartsWith('>')) { Add(FountainKind.Transition, t[1..].Trim()); continue; }
            if (prevBlank && nextBlank && IsUpper(t) && (t.EndsWith("TO:", StringComparison.Ordinal) || t.EndsWith("BLENDE:", StringComparison.Ordinal) || t.EndsWith("SCHNITT:", StringComparison.Ordinal) || t.EndsWith("AUF:", StringComparison.Ordinal)))
            { Add(FountainKind.Transition, t); continue; }

            var forcedName = t.StartsWith('@');
            var name = Extension().Replace(t.TrimStart('@').TrimEnd('^', ' '), "").Trim();
            if (prevBlank && !nextBlank && name.Length is > 0 and <= 40 && (forcedName || IsUpper(name)))
            {
                Add(FountainKind.Character, forcedName ? name : name.ToUpperInvariant());
                for (i++; i < raw.Length && raw[i].Trim().Length > 0; i++)
                {
                    var d = raw[i].Trim();
                    lines.Add(new(d.StartsWith('(') && d.EndsWith(')') ? FountainKind.Parenthetical : FountainKind.Dialogue, d, i + 1));
                }
                continue;
            }
            Add(FountainKind.Action, t);
        }
        return new(title, lines, string.Join('\n', plan));
    }

    /// <summary>Für jede Szene ohne Einstellungen einen regelbasierten Vorschlag direkt unter den Szenenkopf schreiben: Totale, dann je Rede eine Einstellung (höchstens 5 s, so lang wie ein KI-Clip).
    /// Vorhandene Einstellungen bleiben unberührt.</summary>
    public static string InsertSuggestedShots(string? source)
    {
        var text = (source ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
        var parsed = Parse(text); var src = text.Split('\n');
        var headings = parsed.Lines.Select((l, n) => (l, n)).Where(x => x.l.Kind == FountainKind.Heading).ToList();
        var inserts = new SortedDictionary<int, List<string>>();
        for (var h = 0; h < headings.Count; h++)
        {
            var from = headings[h].n + 1; var to = h + 1 < headings.Count ? headings[h + 1].n : parsed.Lines.Count;
            var body = parsed.Lines.Skip(from).Take(to - from).ToList();
            if (body.Any(l => l.Kind == FountainKind.Shot)) continue;
            var firstAction = body.FirstOrDefault(l => l.Kind == FountainKind.Action)?.Text ?? "Überblick über den Ort";
            var shots = new List<string> { $"- Totale: {Clip(firstAction, 80)} (4 s)" };
            for (var k = 0; k < body.Count; k++)
            {
                if (body[k].Kind != FountainKind.Character) continue;
                var said = string.Join(' ', body.Skip(k + 1).TakeWhile(l => l.Kind is FountainKind.Dialogue or FountainKind.Parenthetical).Where(l => l.Kind == FountainKind.Dialogue).Select(l => l.Text));
                var seconds = Math.Clamp((int)Math.Ceiling(Words(said) / 2.5) + 1, 2, 5);
                shots.Add($"- Nah: {body[k].Text} sagt „{Clip(said, 60)}“ ({seconds} s)");
            }
            inserts[headings[h].l.LineNo] = shots;
        }
        var result = new List<string>();
        for (var i = 0; i < src.Length; i++)
        {
            result.Add(src[i]);
            if (inserts.TryGetValue(i + 1, out var add)) { result.Add(""); result.AddRange(add); }
        }
        return string.Join('\n', result);
    }

    internal static int Words(string s) => s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    static string Clip(string s, int max) => s.Length <= max ? s : s[..max].TrimEnd() + "…";

    /// <summary>Drehbuch als Typst-Quelle im üblichen Filmsatz: Schreibmaschinenschrift, Figurennamen und Rede eingerückt, Seitenzahlen. Einstellungen gehören in die Shotliste, nicht ins Drehbuch.</summary>
    public static string ToTypst(FountainScript s, string fallbackTitle)
    {
        var sb = new StringBuilder();
        sb.Append("#set page(paper: \"a4\", margin: (left: 3.8cm, right: 2.5cm, top: 2.5cm, bottom: 2.5cm), numbering: \"1\", number-align: top + right)\n");
        sb.Append("#set text(font: \"DejaVu Sans Mono\", size: 10.5pt, lang: \"de\")\n#set par(leading: 0.65em, justify: false)\n\n");
        var title = s.Title.Length > 0 ? s.Title : fallbackTitle;
        sb.Append("#align(center + horizon)[#text(size: 18pt, weight: \"bold\")[").Append(TypstDoc.Escape(title)).Append("]");
        foreach (var key in new[] { "credit", "author", "authors", "autor", "autorin", "draft date", "datum" })
            if (s.TitlePage.TryGetValue(key, out var v) && v.Length > 0) sb.Append("\\ #v(0.6em) ").Append(TypstDoc.Escape(v));
        sb.Append("]\n#pagebreak()\n\n");
        foreach (var l in s.Lines)
        {
            var t = TypstDoc.Escape(l.Text);
            switch (l.Kind)
            {
                case FountainKind.Heading: sb.Append("#v(1em)\n#block(sticky: true)[#strong[").Append(t).Append("]]\n\n"); break;
                case FountainKind.Action: sb.Append("#block[").Append(t).Append("]\n\n"); break;
                case FountainKind.Character: sb.Append("#pad(left: 35%, top: 0.6em, bottom: -0.4em)[#block(sticky: true)[").Append(t).Append("]]\n\n"); break;
                case FountainKind.Parenthetical: sb.Append("#pad(left: 27%, right: 22%, bottom: -0.4em)[").Append(t).Append("]\n\n"); break;
                case FountainKind.Dialogue: sb.Append("#pad(left: 18%, right: 15%, bottom: -0.4em)[").Append(t).Append("]\n\n"); break;
                case FountainKind.Transition: sb.Append("#align(right)[").Append(t).Append("]\n\n"); break;
            }
        }
        return sb.ToString();
    }
}

public sealed record FilmScene(string Code, string Place, string Location, string Time, IReadOnlyList<string> Characters, int DialogueWords, int ActionWords, double Seconds, bool Planned, IReadOnlyList<ShotPlan.Shot> Shots);
public sealed record FilmRole(string Name, int Scenes, int Speeches, int Words);
public sealed record FilmLocation(string Name, int Scenes, double Seconds);
public sealed record FilmBreakdown(IReadOnlyList<FilmScene> Scenes, IReadOnlyList<FilmRole> Roles, IReadOnlyList<FilmLocation> Locations, IReadOnlyList<string> Problems)
{
    public double Seconds => Scenes.Sum(s => s.Seconds);
    public int ShotCount => Scenes.Sum(s => s.Shots.Count);
}

/// <summary>Film-Studio F3: Auszug aus dem Drehbuch – Szenen, Figuren, Orte, geschätzte Länge. Regeln statt KI: Das Ergebnis ist bei gleichem Text immer gleich.</summary>
public static class FilmBreakdowns
{
    public const double SpokenWordsPerSecond = 2.5, ActionWordsPerSecond = 4;

    public static FilmBreakdown Of(FountainScript script)
    {
        var plan = ShotPlan.Parse(script.PlanText);
        var heads = script.Lines.Select((l, n) => (l, n)).Where(x => x.l.Kind == FountainKind.Heading).ToList();
        var scenes = new List<FilmScene>(); var speeches = new Dictionary<string, (HashSet<int> Scenes, int Count, int Words)>();
        for (var s = 0; s < plan.Scenes.Count && s < heads.Count; s++)
        {
            var from = heads[s].n + 1; var to = s + 1 < heads.Count ? heads[s + 1].n : script.Lines.Count;
            var body = script.Lines.Skip(from).Take(to - from).ToList();
            var cast = new List<string>(); var dialogue = 0; var action = 0;
            for (var k = 0; k < body.Count; k++)
            {
                if (body[k].Kind == FountainKind.Action) action += Fountain.Words(body[k].Text);
                if (body[k].Kind != FountainKind.Character) continue;
                var name = body[k].Text;
                if (!cast.Contains(name)) cast.Add(name);
                var words = body.Skip(k + 1).TakeWhile(l => l.Kind is FountainKind.Dialogue or FountainKind.Parenthetical).Where(l => l.Kind == FountainKind.Dialogue).Sum(l => Fountain.Words(l.Text));
                dialogue += words;
                var e = speeches.GetValueOrDefault(name, ([], 0, 0)); e.Scenes.Add(s); speeches[name] = (e.Scenes, e.Count + 1, e.Words + words);
            }
            var sc = plan.Scenes[s]; var planned = sc.Shots.Count > 0;
            var seconds = planned ? sc.Shots.Sum(x => x.Seconds) : Math.Max(3, dialogue / SpokenWordsPerSecond + action / ActionWordsPerSecond);
            scenes.Add(new(sc.Code, sc.Place, sc.Location, sc.Time, cast, dialogue, action, seconds, planned, sc.Shots));
        }
        var roles = speeches.Select(kv => new FilmRole(kv.Key, kv.Value.Scenes.Count, kv.Value.Count, kv.Value.Words)).OrderByDescending(r => r.Words).ThenBy(r => r.Name, StringComparer.Ordinal).ToList();
        var locations = scenes.GroupBy(s => s.Location, StringComparer.OrdinalIgnoreCase).Select(g => new FilmLocation(g.First().Location, g.Count(), g.Sum(x => x.Seconds))).OrderByDescending(l => l.Scenes).ThenBy(l => l.Name, StringComparer.Ordinal).ToList();
        var problems = new List<string>(plan.Problems.Where(p => !p.Contains("hat keine Einstellung", StringComparison.Ordinal)));
        for (var a = 0; a < roles.Count; a++)
            for (var b = a + 1; b < roles.Count; b++)
                if (roles[a].Name.Length >= 4 && roles[b].Name.Length >= 4 && Distance(roles[a].Name, roles[b].Name) == 1)
                    problems.Add($"Die Figuren „{roles[a].Name}“ und „{roles[b].Name}“ heißen fast gleich – Tippfehler?");
        return new(scenes, roles, locations, problems);
    }

    static int Distance(string a, string b)
    {
        if (Math.Abs(a.Length - b.Length) > 1) return 2;
        var prev = Enumerable.Range(0, b.Length + 1).ToArray();
        for (var i = 1; i <= a.Length; i++)
        {
            var cur = new int[b.Length + 1]; cur[0] = i;
            for (var j = 1; j <= b.Length; j++) cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            prev = cur;
        }
        return prev[b.Length];
    }

    public static string Report(string title, FilmBreakdown b)
    {
        var sb = new StringBuilder($"# Drehbuchauszug: {title}\n\n");
        sb.Append(CultureInfo.GetCultureInfo("de-DE"), $"{b.Scenes.Count} Szenen, {b.Roles.Count} Figuren, {b.Locations.Count} Orte, geschätzte Länge {b.Seconds / 60:0.0} Minuten.\n\n## Figuren\n\n| Figur | Szenen | Reden | Wörter |\n|---|---|---|---|\n");
        foreach (var r in b.Roles) sb.Append($"| {r.Name} | {r.Scenes} | {r.Speeches} | {r.Words} |\n");
        sb.Append("\n## Orte\n\n| Ort | Szenen |\n|---|---|\n");
        foreach (var l in b.Locations) sb.Append($"| {l.Name} | {l.Scenes} |\n");
        sb.Append("\n## Szenen\n\n| Szene | Ort | Zeit | Figuren | Sekunden |\n|---|---|---|---|---|\n");
        foreach (var s in b.Scenes) sb.Append(CultureInfo.GetCultureInfo("de-DE"), $"| {s.Code} | {s.Place}. {s.Location} | {s.Time} | {string.Join(", ", s.Characters)} | {s.Seconds:0}{(s.Planned ? "" : " (geschätzt)")} |\n");
        if (b.Problems.Count > 0) { sb.Append("\n## Bitte prüfen\n\n"); foreach (var p in b.Problems) sb.Append("- ").Append(p).Append('\n'); }
        return sb.ToString();
    }
}
