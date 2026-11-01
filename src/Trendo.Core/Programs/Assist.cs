using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Trendo.Core.Programs;

/// <summary>T-0315: Programm-Assistent wie Claude Code – ein Auftrag in eigenen Worten wird zu einem Plan aus den Aktionen des offenen Programms.
/// Fehlt etwas, fragt der Assistent mit Knöpfen (Ja/Nein oder Auswahl, Empfehlung zuerst); Aktionen mit Folgen (senden, löschen, drucken) brauchen ein Ja.
/// Der Planer arbeitet ohne KI (Regeln, deutsch); eine lokale KI kann einen Plan vorschlagen, der hier streng gegen die erlaubten Aktionen geprüft wird.
/// Vorlieben („immer so“) merkt sich der Assistent je Programm – sichtbar und löschbar.</summary>
public enum AssistKind { Text, Number, Date, Choice, YesNo }

/// <summary>Parameter einer Aktion. <paramref name="Words"/>: Wörter, hinter denen der Wert im Auftrag steht (z. B. „Betreff“, „Titel“).</summary>
public sealed record AssistParam(string Name, string Label, AssistKind Kind = AssistKind.Text, IReadOnlyList<string>? Options = null, string? Default = null, bool Required = true, IReadOnlyList<string>? Words = null);

/// <summary>Eine Aktion des Programms. <paramref name="SideEffect"/>: wirkt nach außen (senden, löschen, drucken, Datei schreiben) → nur nach Ja, nie in der Vorschau.
/// <paramref name="Next"/>: sinnvolle nächste Aktionen (für Vorschläge).</summary>
public sealed record AssistAction(string Id, string Label, IReadOnlyList<string> Words, IReadOnlyList<AssistParam>? Params = null, bool SideEffect = false, IReadOnlyList<string>? Next = null)
{
    public IReadOnlyList<AssistParam> Parameters => Params ?? [];
}

/// <summary>Rückfrage mit Antwortknöpfen. <paramref name="Options"/> leer = freie Eingabe; <paramref name="Recommended"/> zeigt die Empfehlung (zuerst).</summary>
public sealed record AssistQuestion(string Id, string Text, IReadOnlyList<string> Options, int Recommended = 0, bool AllowOther = true, int Step = -1, string? Param = null, bool Confirm = false);

public sealed record AssistStep(AssistAction Action, Dictionary<string, string> Args, List<string> FromMemory)
{
    /// <summary>Wie oft (bei Aktionen ohne Parameter: „füge 3 Zeilen hinzu“).</summary>
    public int Times => Args.TryGetValue(AssistPlanner.TimesKey, out var t) && int.TryParse(t, out var n) ? n : 1;
    public string Text => Action.Label + (Times > 1 ? $" ({Times}×)" : "") + (Action.Parameters.Count == 0 ? "" : " – " + string.Join(", ", Action.Parameters.Where(p => Args.ContainsKey(p.Name)).Select(p => $"{p.Label}: {Show(p, Args[p.Name])}")));
    static string Show(AssistParam p, string v) => p.Kind switch { AssistKind.YesNo => v == "ja" ? "ja" : "nein", AssistKind.Date when DateTime.TryParse(v, CultureInfo.InvariantCulture, out var d) => d.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("de-DE")), _ => v };
}

public sealed record AssistSuggestion(string Text, string Prompt);

/// <summary>Plan: Schritte, offene Rückfragen, Hinweis. <see cref="Understood"/> = mindestens ein Schritt erkannt.</summary>
public sealed class AssistPlan
{
    public string Prompt { get; init; } = "";
    public List<AssistStep> Steps { get; } = [];
    public List<AssistQuestion> Questions { get; } = [];
    public List<string> Unclear { get; } = [];
    public string Source { get; set; } = "Regeln";
    public bool Understood => Steps.Count > 0;
    public bool Ready => Steps.Count > 0 && Questions.Count == 0;
    public bool HasSideEffects => Steps.Any(s => s.Action.SideEffect);
    public string Summary => !Understood ? "Das habe ich in diesem Programm nicht verstanden." :
        Questions.Count > 0 ? $"{Steps.Count} Schritt(e) geplant – {Questions.Count} Rückfrage(n) offen." : $"{Steps.Count} Schritt(e) geplant.";
}

public static partial class AssistPlanner
{
    public const string TimesKey = "__mal";
    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    static readonly string[] Particles = ["ein", "aus", "an", "zu", "hinzu", "ab", "auf", "um", "weg", "vor", "nach", "fest", "zurueck", "dazu"];
    static readonly string[] NoWords = ["nicht", "ohne", "kein", "keine", "keinen", "nein"];
    static readonly string[] Fillers = ["bitte", "mal", "doch", "noch", "mir", "mich", "ich", "du", "dann", "und", "die", "der", "das", "den", "dem", "des", "ein", "eine", "einen", "einem", "einer", "auf", "zu", "zum", "zur", "in", "im", "mit", "von", "vom", "fuer", "an", "am", "als", "alle", "alles", "es", "sie", "er", "wir", "ihr", "kannst", "soll", "sollst", "moechte", "will", "willst", "neu", "neue", "neuen", "neues"];

    [GeneratedRegex(@"[„""“”']([^„""“”']{1,400})[„""“”']")] private static partial Regex Quoted();
    [GeneratedRegex(@"(?<![\w.,])-?\d{1,9}(?:[.,]\d{1,4})?(?![\w.,]*\d)")] private static partial Regex Num();
    [GeneratedRegex(@"\b(\d{1,2})\.(\d{1,2})\.(\d{2,4})?")] private static partial Regex DateDot();
    [GeneratedRegex(@"\s*(?:,(?!\d)|;|(?<!\d)\.\s|\bund dann\b|\bdanach\b|\bund\b(?=\s+(?:füge|fuege|setze|mach|mache|lösche|loesche|schreib|schreibe|erstelle|zeig|zeige|sortier|sortiere|füg|exportier|exportiere|speicher|speichere|sende|schick|schicke|teile|färbe|faerbe|füll|fülle|benenne|verschieb|verschiebe|dreh|drehe|kopier|kopiere|ändere|aendere|änder|stell|stelle|leg|lege|druck|drucke|öffne|oeffne|trag|trage)\b))\s*", RegexOptions.IgnoreCase)] private static partial Regex Clause();

    /// <summary>Kleinschreibung, Umlaute ausgeschrieben, nur Buchstaben/Ziffern – für Vergleiche.</summary>
    public static string Norm(string s)
    {
        var t = s.ToLowerInvariant().Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss");
        var sb = new StringBuilder(t.Length);
        foreach (var c in t) sb.Append(char.IsLetterOrDigit(c) ? c : ' ');
        return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }

    /// <summary>Grober deutscher Wortstamm („Zeilen“ → „zeil“, „Summe“ → „summ“, „sortiere“ → „sortier“).</summary>
    public static string Stem(string w)
    {
        w = Norm(w);
        foreach (var end in new[] { "ungen", "ung", "ern", "en", "er", "es", "e", "n", "s", "t" })
            if (w.Length > end.Length + 3 && w.EndsWith(end, StringComparison.Ordinal)) return w[..^end.Length];
        return w;
    }

    static IEnumerable<string> Tokens(string s) => Norm(s).Split(' ', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Wie gut passt ein Satzteil zu einer Aktion? Wortstämme aus Bezeichnung und Stichwörtern; Füllwörter zählen nicht.</summary>
    public static int Score(string clause, AssistAction a)
    {
        var all = Tokens(clause).ToList();
        var words = all.Where(t => !Fillers.Contains(t)).Select(Stem).Where(t => t.Length >= 2).ToList();
        // trennbare Verben: „Trag 30 € ein“ = eintragen, „Füge eine Zeile hinzu“ = hinzufügen
        if (all.Count > 1 && Particles.Contains(all[^1])) words.AddRange(all.Take(all.Count - 1).Where(t => !Fillers.Contains(t) && t.Length >= 3 && !char.IsDigit(t[0])).Select(t => Stem(all[^1] + t)));
        var keys = a.Words.Concat([a.Label]).SelectMany(Tokens).Where(t => !Fillers.Contains(t)).Select(Stem).Where(t => t.Length >= 2).Distinct().ToList();
        var score = 0;
        foreach (var k in keys)
            if (words.Contains(k)) score += 3;
            else if (k.Length >= 4 && words.Any(w => w.Length >= 4 && (w.StartsWith(k, StringComparison.Ordinal) || k.StartsWith(w, StringComparison.Ordinal)))) score += 1;
        return score;
    }

    /// <summary>Teilt einen Auftrag in Satzteile („Füge Zeile hinzu und sortiere nach Betrag“ → zwei). Text in Anführungszeichen wird dabei nicht zerschnitten.</summary>
    public static IReadOnlyList<string> Clauses(string prompt)
    {
        var quotes = new List<string>();
        var masked = Quoted().Replace(prompt, m => { quotes.Add(m.Value); return $"\u0001{quotes.Count - 1}\u0001"; });
        return Clause().Split(masked).Select(c => Regex.Replace(c, "\u0001(\\d+)\u0001", m => quotes[int.Parse(m.Groups[1].Value)]))
            .Select(c => Regex.Replace(c, @"^\s*dann\s+|\s+dann\b", " ", RegexOptions.IgnoreCase).Trim()).Where(c => c.Length > 0).ToList();
    }

    /// <summary>Plan ohne KI. <paramref name="prefs"/>: gemerkte Werte („aktion.parameter“ → Wert).</summary>
    public static AssistPlan Plan(string prompt, IReadOnlyList<AssistAction> actions, IReadOnlyDictionary<string, string>? prefs = null, DateTime? today = null)
    {
        var plan = new AssistPlan { Prompt = prompt.Trim() };
        if (plan.Prompt.Length == 0 || actions.Count == 0) return plan;
        AssistAction? Best(string c) => actions.Select(a => (a, s: Score(c, a))).Where(x => x.s >= 3).OrderByDescending(x => x.s).ThenBy(x => x.a.Label.Length).FirstOrDefault().a;
        var clauses = new List<string>();
        foreach (var c in Clauses(plan.Prompt))
        {
            // „Füge 3 Zeilen hinzu und zwei Spalten“: „und“ ohne Verb trennt, wenn jeder Teil für sich eine andere Aktion ergibt
            var parts = Regex.Split(c, @"\s+und\s+", RegexOptions.IgnoreCase);
            if (parts.Length > 1 && parts.Select(Best).ToList() is var bs && bs.All(b => b is not null) && bs.Distinct().Count() == bs.Count) clauses.AddRange(parts);
            else clauses.Add(c);
        }
        foreach (var clause in clauses)
        {
            var best = Best(clause);
            if (best is null) { plan.Unclear.Add(clause); continue; }
            plan.Steps.Add(Fill(best, clause, prefs, today ?? DateTime.Today));
        }
        Ask(plan);
        return plan;
    }

    /// <summary>Werte aus dem Satzteil holen; was fehlt, kommt aus den Vorlieben oder dem Standardwert, sonst wird gefragt.</summary>
    static AssistStep Fill(AssistAction a, string clause, IReadOnlyDictionary<string, string>? prefs, DateTime today)
    {
        var args = new Dictionary<string, string>(); var mem = new List<string>();
        var rest = clause; var n = Norm(clause);
        var quotes = Quoted().Matches(clause).Select(m => m.Groups[1].Value.Trim()).ToList();
        foreach (var p in a.Parameters.OrderBy(p => p.Kind switch { AssistKind.Date => 0, AssistKind.Choice => 1, AssistKind.YesNo => 2, AssistKind.Number => 3, _ => 4 }))
        {
            string? v = null;
            switch (p.Kind)
            {
                case AssistKind.Choice:
                    v = (p.Options ?? []).FirstOrDefault(o => Tokens(o).Select(Stem).All(t => Tokens(clause).Select(Stem).Contains(t)));
                    break;
                case AssistKind.YesNo:
                    var key = (p.Words ?? [p.Label]).SelectMany(Tokens).Select(Stem).ToList();
                    var toks = Tokens(clause).ToList();
                    var at = toks.FindIndex(t => key.Contains(Stem(t)));
                    if (at >= 0) v = toks.Take(at).TakeLast(2).Any(t => NoWords.Contains(t)) ? "nein" : "ja";
                    break;
                case AssistKind.Date:
                    if (n.Contains("uebermorgen")) v = today.AddDays(2).ToString("yyyy-MM-dd");
                    else if (n.Contains("morgen")) v = today.AddDays(1).ToString("yyyy-MM-dd");
                    else if (Regex.IsMatch(n, @"\bheute\b")) v = today.ToString("yyyy-MM-dd");
                    else if (DateDot().Match(rest) is { Success: true } dm && int.TryParse(dm.Groups[1].Value, out var dd) && int.TryParse(dm.Groups[2].Value, out var mo) && dd is >= 1 and <= 31 && mo is >= 1 and <= 12)
                    {
                        var y = dm.Groups[3].Success ? int.Parse(dm.Groups[3].Value) : today.Year; if (y < 100) y += 2000;
                        try { v = new DateTime(y, mo, Math.Min(dd, DateTime.DaysInMonth(y, mo))).ToString("yyyy-MM-dd"); rest = rest.Remove(dm.Index, dm.Length); } catch (ArgumentOutOfRangeException) { }
                    }
                    break;
                case AssistKind.Number:
                    if (Num().Match(rest) is { Success: true } nm && double.TryParse(nm.Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                    { v = d.ToString(CultureInfo.InvariantCulture); rest = rest.Remove(nm.Index, nm.Length); }
                    break;
                case AssistKind.Text:
                    if (quotes.Count > 0) { v = quotes[0]; quotes.RemoveAt(0); }
                    else if ((p.Words ?? [p.Label]).Select(w => Regex.Match(clause, $@"\b{Regex.Escape(w)}\b\s*[:=]?\s*(.+)$", RegexOptions.IgnoreCase)).FirstOrDefault(m => m.Success) is { } wm)
                        v = wm.Groups[1].Value.Trim().TrimEnd('.', '!', '?');
                    break;
            }
            if (v is null && prefs?.TryGetValue($"{a.Id}.{p.Name}", out var pv) == true) { v = pv; mem.Add(p.Name); }
            if (v is null && p.Default is not null && !p.Required) v = p.Kind == AssistKind.Date ? DayWord(p.Default, today) : p.Default;
            if (v is not null) args[p.Name] = v;
        }
        // Aktion ohne Parameter mit kleiner Zahl im Satz: so oft ausführen („füge 3 Zeilen hinzu“, höchstens 50)
        if (a.Parameters.Count == 0 && Num().Match(clause) is { Success: true } tm && int.TryParse(tm.Value, out var times) && times is >= 2 and <= 50) args[TimesKey] = times.ToString();
        else if (a.Parameters.Count == 0 && Regex.IsMatch(n, @"\b(zwei|drei|vier|fuenf|sechs|sieben|acht|neun|zehn)\b") && Regex.Match(n, @"\b(zwei|drei|vier|fuenf|sechs|sieben|acht|neun|zehn)\b").Value is var wn)
            args[TimesKey] = (Array.IndexOf(["zwei", "drei", "vier", "fuenf", "sechs", "sieben", "acht", "neun", "zehn"], wn) + 2).ToString();
        return new AssistStep(a, args, mem);
    }

    static string DayWord(string w, DateTime today) => Norm(w) switch
    {
        "heute" => today.ToString("yyyy-MM-dd"), "morgen" => today.AddDays(1).ToString("yyyy-MM-dd"), "uebermorgen" => today.AddDays(2).ToString("yyyy-MM-dd"), _ => w,
    };

    /// <summary>Rückfragen für fehlende Pflichtwerte und ein Ja vor Aktionen mit Folgen.</summary>
    public static void Ask(AssistPlan plan)
    {
        plan.Questions.Clear();
        for (var i = 0; i < plan.Steps.Count; i++)
        {
            var s = plan.Steps[i];
            foreach (var p in s.Action.Parameters.Where(p => p.Required && !s.Args.ContainsKey(p.Name)))
            {
                var (opts, rec) = p.Kind switch
                {
                    AssistKind.YesNo => ((IReadOnlyList<string>)["Ja", "Nein"], p.Default == "nein" ? 1 : 0),
                    AssistKind.Choice => (p.Options ?? [], Math.Max(0, (p.Options ?? []).ToList().IndexOf(p.Default ?? ""))),
                    _ => ((IReadOnlyList<string>)[], 0),
                };
                var q = p.Kind switch
                {
                    AssistKind.YesNo => $"{s.Action.Label}: {p.Label}?",
                    AssistKind.Choice => $"{s.Action.Label}: Welche {p.Label}?",
                    AssistKind.Date => $"{s.Action.Label}: An welchem Tag ({p.Label})?",
                    AssistKind.Number => $"{s.Action.Label}: Welche Zahl für „{p.Label}“?",
                    _ => $"{s.Action.Label}: Was soll bei „{p.Label}“ stehen?",
                };
                plan.Questions.Add(new AssistQuestion($"q{i}-{p.Name}", q, opts, rec, AllowOther: p.Kind != AssistKind.YesNo, Step: i, Param: p.Name));
            }
            if (s.Action.SideEffect && !s.Args.ContainsKey("__ok"))
                plan.Questions.Add(new AssistQuestion($"q{i}-ok", $"„{s.Text}“ wirkt nach außen. Wirklich ausführen?", ["Ja", "Nein"], 1, AllowOther: false, Step: i, Confirm: true));
        }
    }

    /// <summary>Antwort auf eine Rückfrage. „Nein“ bei einer Bestätigung streicht den Schritt. <paramref name="remember"/> merkt den Wert für das nächste Mal.</summary>
    public static void Answer(AssistPlan plan, string questionId, string value, AssistPrefs? prefs = null, bool remember = false)
    {
        var q = plan.Questions.FirstOrDefault(x => x.Id == questionId) ?? throw new ArgumentException("Diese Rückfrage gibt es nicht (mehr).");
        var s = plan.Steps[q.Step];
        if (q.Confirm)
        {
            if (value.Equals("Ja", StringComparison.OrdinalIgnoreCase)) s.Args["__ok"] = "ja"; else plan.Steps.RemoveAt(q.Step);
        }
        else
        {
            var p = s.Action.Parameters.First(x => x.Name == q.Param);
            var v = p.Kind switch
            {
                AssistKind.YesNo => value.Equals("Ja", StringComparison.OrdinalIgnoreCase) ? "ja" : "nein",
                AssistKind.Number => double.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d.ToString(CultureInfo.InvariantCulture) : throw new ArgumentException("Bitte eine Zahl eingeben."),
                AssistKind.Date => DateTime.TryParse(value, De, DateTimeStyles.None, out var dt) ? dt.ToString("yyyy-MM-dd") : throw new ArgumentException("Bitte ein Datum wie 15.11.2026 eingeben."),
                _ => value.Trim(),
            };
            if (v.Length == 0) throw new ArgumentException("Bitte eine Antwort eingeben.");
            s.Args[p.Name] = v;
            if (remember && prefs is not null) prefs.Set($"{s.Action.Id}.{p.Name}", v, $"{s.Action.Label}: {p.Label} = {value.Trim()}");
        }
        Ask(plan);
    }

    /// <summary>Nächste sinnvolle Schritte: zuerst Hinweise aus dem Inhalt (vom Programm), dann die „Next“-Aktionen der erledigten Schritte.</summary>
    public static IReadOnlyList<AssistSuggestion> Suggest(IEnumerable<AssistAction> done, IReadOnlyList<AssistAction> actions, IEnumerable<AssistSuggestion>? fromContent = null, int max = 3)
    {
        var doneIds = done.Select(a => a.Id).ToHashSet();
        var list = new List<AssistSuggestion>(fromContent ?? []);
        foreach (var id in actions.Where(a => doneIds.Contains(a.Id)).SelectMany(a => a.Next ?? []).Distinct())
            if (actions.FirstOrDefault(a => a.Id == id) is { } n && !doneIds.Contains(n.Id)) list.Add(new AssistSuggestion(n.Label, n.Label));
        return list.GroupBy(s => s.Prompt, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).Take(max).ToList();
    }

    // ───────────── KI-Vorschlag (optional) ─────────────

    /// <summary>Anweisung an die KI: nur die erlaubten Aktionen, Antwort als JSON. <paramref name="role"/> und <paramref name="skills"/> machen sie zur Fachkraft des Programms.</summary>
    public static string KiPrompt(string program, IReadOnlyList<AssistAction> actions, string? role = null, string? skills = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine(role is { Length: > 0 } ? role : $"Du bist der Assistent im Programm „{program}“ von Trendo OS.");
        if (skills is { Length: > 0 }) { sb.AppendLine("Arbeite nach diesen Fachregeln:"); sb.AppendLine(skills); }
        sb.AppendLine("Übersetze den Auftrag in Schritte. Erlaubt sind NUR diese Aktionen (id: Bezeichnung (Parameter)):");
        foreach (var a in actions) sb.AppendLine($"- {a.Id}: {a.Label}{(a.Parameters.Count > 0 ? " (" + string.Join(", ", a.Parameters.Select(p => p.Name + (p.Options is { Count: > 0 } o ? "=" + string.Join("|", o) : ""))) + ")" : "")}");
        sb.AppendLine("Antworte nur mit JSON: {\"steps\":[{\"action\":\"id\",\"args\":{\"name\":\"wert\"}}],\"frage\":\"falls etwas unklar ist\"}. Unbekanntes weglassen, nichts erfinden.");
        return sb.ToString();
    }

    /// <summary>KI-Antwort streng prüfen: nur bekannte Aktionen und Parameter, Auswahlwerte nur aus der Liste. Danach dieselben Rückfragen wie ohne KI.</summary>
    public static AssistPlan FromKi(string prompt, string answer, IReadOnlyList<AssistAction> actions)
    {
        var plan = new AssistPlan { Prompt = prompt, Source = "KI" };
        var start = answer.IndexOf('{'); var end = answer.LastIndexOf('}');
        if (start < 0 || end <= start) return plan;
        try
        {
            if (JsonNode.Parse(answer[start..(end + 1)]) is not JsonObject root || root["steps"] is not JsonArray steps) return plan;
            foreach (var st in steps.OfType<JsonObject>().Take(12))
            {
                if (actions.FirstOrDefault(a => a.Id == (string?)st["action"]) is not { } a) continue;
                var args = new Dictionary<string, string>();
                if (st["args"] is JsonObject ao)
                    foreach (var p in a.Parameters)
                        if (ao[p.Name] is JsonValue jv && jv.ToString() is { Length: > 0 and < 2000 } v && (p.Kind != AssistKind.Choice || (p.Options ?? []).Contains(v)) && (p.Kind != AssistKind.Number || double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out _)))
                            args[p.Name] = p.Kind == AssistKind.YesNo ? (v is "ja" or "true" or "Ja" ? "ja" : "nein") : v;
                plan.Steps.Add(new AssistStep(a, args, []));
            }
        }
        catch (JsonException) { return plan; }
        Ask(plan);
        return plan;
    }
}

/// <summary>Gemerkte Vorlieben je Programm („immer so“) – Datei unter <c>assist/&lt;programm&gt;.json</c>; sichtbar und einzeln löschbar.</summary>
public sealed class AssistPrefs
{
    public sealed record Item(string Key, string Value, string Text, DateTimeOffset At);
    readonly string _file;
    List<Item> _items;

    public AssistPrefs(string dataRoot, string program)
    {
        var safe = string.Concat(program.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_'));
        _file = Path.Combine(dataRoot, "assist", (safe.Length > 0 ? safe : "programm") + ".json");
        try { _items = File.Exists(_file) ? JsonSerializer.Deserialize<List<Item>>(File.ReadAllText(_file)) ?? [] : []; }
        catch (Exception e) when (e is JsonException or IOException) { _items = []; }
    }

    public IReadOnlyList<Item> All => _items;
    public IReadOnlyDictionary<string, string> Values => _items.ToDictionary(i => i.Key, i => i.Value);

    public void Set(string key, string value, string text)
    {
        _items.RemoveAll(i => i.Key == key); _items.Add(new Item(key, value, text, DateTimeOffset.Now)); Save();
    }
    public bool Forget(string key) { var n = _items.RemoveAll(i => i.Key == key); if (n > 0) Save(); return n > 0; }
    public void ForgetAll() { _items.Clear(); Save(); }

    void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        File.WriteAllText(_file + ".tmp", JsonSerializer.Serialize(_items, new JsonSerializerOptions { WriteIndented = true })); SafeFile.Move(_file + ".tmp", _file);
    }
}
