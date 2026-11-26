using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Trendo.Core.Buero;

namespace Trendo.Core.Wissen;

// ===================== STUDIO-QUIZ-01 Quiz erstellen (T-0779) =====================
// Eigener Kern (Clean-Room, Anforderungen in docs/nachbau/quiz/anforderungen.md). Baut auf dem Lern-Kern von SPEC-06 auf:
// Karteikarten → Quiz und Quiz → Karteikarten über LearnBox; Lücken mit eigener, strengerer Regel (QuizKit.LueckeOk) – die Karteikarten bleiben unverändert.

/// <summary>Eine Antwortmöglichkeit (Auswahl, Mehrfachauswahl, Wahr/Falsch).</summary>
public sealed class QuizAntwort
{
    public string Text { get; set; } = "";
    public bool Richtig { get; set; }
}

/// <summary>Ein Paar beim Zuordnen: links der Begriff, rechts das, was dazugehört.</summary>
public sealed class QuizPaar
{
    public string Links { get; set; } = "";
    public string Rechts { get; set; } = "";
}

/// <summary>Eine Frage. <see cref="Typ"/>: auswahl, mehrfach, wahrfalsch, luecke, zuordnen.
/// Lückentext: Lücken stehen in eckigen Klammern im Text („Die Hauptstadt ist [Berlin].“), mehrere richtige Schreibweisen mit „|“ („[Rom|Roma]“).</summary>
public sealed class QuizFrage
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Typ { get; set; } = "auswahl";
    public string Text { get; set; } = "";
    public List<QuizAntwort> Antworten { get; set; } = [];
    public List<QuizPaar> Paare { get; set; } = [];
    public double Punkte { get; set; } = 1;
    /// <summary>Zeit für diese Frage in Sekunden; 0 = wie im Quiz eingestellt.</summary>
    public int Sekunden { get; set; }
    /// <summary>Erklärung nach der Antwort (und auf dem Lösungsblatt).</summary>
    public string Erklaerung { get; set; } = "";
}

/// <summary>Ein Quiz mit Einstellungen für den Spielmodus.</summary>
public sealed class Quiz
{
    public string Titel { get; set; } = "Neues Quiz";
    public string Beschreibung { get; set; } = "";
    public List<QuizFrage> Fragen { get; set; } = [];
    /// <summary>Zeit je Frage in Sekunden; 0 = ohne Zeitlimit.</summary>
    public int Sekunden { get; set; }
    public bool FragenMischen { get; set; } = true;
    public bool AntwortenMischen { get; set; } = true;
    /// <summary>Ab so viel Prozent der Punkte gilt das Quiz als bestanden.</summary>
    public int Bestehen { get; set; } = 60;
    /// <summary>Wie bei Spiele-Quizzen: Wer schnell richtig antwortet, bekommt bis zu 50 % Bonus (nur mit Zeitlimit).</summary>
    public bool TempoBonus { get; set; }

    internal static readonly JsonSerializerOptions O = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }; // Umlaute lesbar; '</' entschärft QuizHtml.ScriptJson
    public JsonObject ToJson() => JsonSerializer.SerializeToNode(this, O)!.AsObject();
    public static Quiz FromJson(JsonNode? n)
    {
        if (n is not JsonObject o) return new();
        try { return o.Deserialize<Quiz>(O) ?? new(); } catch (JsonException) { return new(); }
    }
    public Quiz Copy() => FromJson(ToJson());
    public string ToJsonText() => JsonSerializer.Serialize(this, O);
    public double MaxPunkte => Fragen.Sum(f => f.Punkte);
    public int ZeitFuer(QuizFrage f) => f.Sekunden > 0 ? f.Sekunden : Sekunden;
}

/// <summary>Was die Person bei einer Frage eingegeben hat. Auswahl-Indizes beziehen sich auf die Reihenfolge in der Frage (nicht die gemischte Anzeige);
/// Zuordnen: für jedes linke Element (Index) der gewählte rechte Index (-1 = leer).</summary>
public sealed record QuizEingabe(IReadOnlyList<int>? Gewaehlt = null, IReadOnlyList<string>? Luecken = null, IReadOnlyList<int>? Zuordnung = null)
{
    public static QuizEingabe Auswahl(params int[] idx) => new(Gewaehlt: idx);
    public static QuizEingabe Text(params string[] luecken) => new(Luecken: luecken);
    public static QuizEingabe Paare(params int[] rechts) => new(Zuordnung: rechts);
}

public static partial class QuizKit
{
    public static readonly (string Id, string Name, string Hint)[] Typen =
    [
        ("auswahl", "Auswahl", "Eine Antwort ist richtig"),
        ("mehrfach", "Mehrfachauswahl", "Mehrere Antworten können richtig sein"),
        ("wahrfalsch", "Wahr oder falsch", "Eine Aussage bewerten"),
        ("luecke", "Lückentext", "Wörter in eckigen Klammern sind die Lücken"),
        ("zuordnen", "Zuordnen", "Paare verbinden"),
    ];
    public static string TypName(string id) => Typen.FirstOrDefault(t => t.Id == id).Name ?? id;
    public static bool IstTyp(string id) => Typen.Any(t => t.Id == id);

    /// <summary>Neue Frage mit sinnvollem Anfangsinhalt (die Person überschreibt nur noch).</summary>
    public static QuizFrage Neu(string typ) => typ switch
    {
        "mehrfach" => new() { Typ = typ, Text = "Welche Antworten sind richtig?", Antworten = [new() { Text = "Richtige Antwort", Richtig = true }, new() { Text = "Noch eine richtige", Richtig = true }, new() { Text = "Falsche Antwort" }, new() { Text = "Falsche Antwort" }] },
        "wahrfalsch" => new() { Typ = typ, Text = "Diese Aussage stimmt.", Antworten = WahrFalsch(true) },
        "luecke" => new() { Typ = typ, Text = "Die Sonne geht im [Osten] auf und im [Westen] unter." },
        "zuordnen" => new() { Typ = typ, Text = "Ordne richtig zu.", Paare = [new() { Links = "Begriff 1", Rechts = "Bedeutung 1" }, new() { Links = "Begriff 2", Rechts = "Bedeutung 2" }, new() { Links = "Begriff 3", Rechts = "Bedeutung 3" }] },
        _ => new() { Typ = "auswahl", Text = "Deine Frage?", Antworten = [new() { Text = "Richtige Antwort", Richtig = true }, new() { Text = "Falsche Antwort" }, new() { Text = "Falsche Antwort" }, new() { Text = "Falsche Antwort" }] },
    };

    public static List<QuizAntwort> WahrFalsch(bool wahr) => [new() { Text = "Wahr", Richtig = wahr }, new() { Text = "Falsch", Richtig = !wahr }];

    /// <summary>Frage in einen anderen Typ umwandeln – Text und passende Teile bleiben erhalten.</summary>
    public static void TypWechseln(QuizFrage f, string typ)
    {
        if (!IstTyp(typ) || f.Typ == typ) return;
        var alt = f.Typ; f.Typ = typ;
        switch (typ)
        {
            case "wahrfalsch": f.Antworten = WahrFalsch(true); break;
            case "auswahl" or "mehrfach":
                if (alt is not ("auswahl" or "mehrfach") || f.Antworten.Count < 2) f.Antworten = Neu(typ).Antworten;
                else if (typ == "auswahl" && f.Antworten.Count(a => a.Richtig) != 1) { var first = Math.Max(0, f.Antworten.FindIndex(a => a.Richtig)); for (var i = 0; i < f.Antworten.Count; i++) f.Antworten[i].Richtig = i == first; }
                break;
            case "luecke": if (Luecken(f.Text).Count == 0) f.Text = f.Text.TrimEnd() + " [Antwort]"; break;
            case "zuordnen": if (f.Paare.Count < 2) f.Paare = Neu("zuordnen").Paare; break;
        }
    }

    [GeneratedRegex(@"\[([^\[\]]+)\]")] private static partial Regex Gap();

    /// <summary>Lücken eines Lückentexts: je Lücke die erlaubten Schreibweisen.</summary>
    public static List<string[]> Luecken(string text) =>
        [.. Gap().Matches(text ?? "").Select(m => m.Groups[1].Value.Split('|').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray()).Where(a => a.Length > 0)];

    /// <summary>Text mit leeren Lücken („____“) zum Anzeigen oder Drucken.</summary>
    public static string LueckenText(string text, string blank = "______") => Gap().Replace(text ?? "", blank);

    /// <summary>Text mit den Lösungen statt der Klammern.</summary>
    public static string MitLoesung(string text) => Gap().Replace(text ?? "", m => m.Groups[1].Value.Split('|')[0].Trim());

    /// <summary>Text, in dem eine Lücke offen bleibt und die übrigen gelöst sind (für Formate, die nur eine Lücke je Frage kennen).</summary>
    public static string NurLuecke(string text, int keep, string marker)
    {
        var i = 0;
        return Gap().Replace(text ?? "", m => i++ == keep ? marker : m.Groups[1].Value.Split('|')[0].Trim());
    }

    // ---------- Prüfen ----------

    /// <summary>Was vor dem Spielen oder Weitergeben noch fehlt (Fehler) oder auffällt (Hinweis).</summary>
    public static List<CvFinding> Pruefen(Quiz q)
    {
        var r = new List<CvFinding>();
        if (q.Titel.Trim().Length == 0) r.Add(new(true, "Quiz", "Das Quiz hat keinen Titel."));
        if (q.Fragen.Count == 0) r.Add(new(true, "Quiz", "Es gibt noch keine Frage."));
        if (q.Bestehen is < 0 or > 100) r.Add(new(true, "Quiz", "„Bestanden ab“ muss zwischen 0 und 100 % liegen."));
        if (q.Sekunden is < 0 or > 3600) r.Add(new(true, "Quiz", "Die Zeit je Frage muss zwischen 0 und 3600 Sekunden liegen."));
        if (q.TempoBonus && q.Sekunden == 0 && q.Fragen.All(f => f.Sekunden == 0)) r.Add(new(false, "Quiz", "Tempo-Bonus wirkt nur mit Zeitlimit."));
        for (var i = 0; i < q.Fragen.Count; i++)
        {
            var f = q.Fragen[i]; var n = $"Frage {i + 1}";
            if (f.Text.Trim().Length == 0) r.Add(new(true, n, "Der Fragetext fehlt."));
            if (f.Punkte is <= 0 or > 1000) r.Add(new(true, n, "Die Punkte müssen größer als 0 sein."));
            switch (f.Typ)
            {
                case "auswahl":
                    if (f.Antworten.Count(a => a.Text.Trim().Length > 0) < 2) r.Add(new(true, n, "Es braucht mindestens zwei Antworten."));
                    if (f.Antworten.Count(a => a.Richtig) != 1) r.Add(new(true, n, "Bei „Auswahl“ ist genau eine Antwort richtig."));
                    break;
                case "mehrfach":
                    if (f.Antworten.Count(a => a.Text.Trim().Length > 0) < 2) r.Add(new(true, n, "Es braucht mindestens zwei Antworten."));
                    if (!f.Antworten.Any(a => a.Richtig)) r.Add(new(true, n, "Mindestens eine Antwort muss richtig sein."));
                    if (f.Antworten.All(a => a.Richtig)) r.Add(new(false, n, "Alle Antworten sind richtig – so ist die Frage zu leicht."));
                    break;
                case "wahrfalsch":
                    if (f.Antworten.Count != 2 || f.Antworten.Count(a => a.Richtig) != 1) r.Add(new(true, n, "Wahr/Falsch braucht genau eine richtige Antwort."));
                    break;
                case "luecke":
                    if (Luecken(f.Text).Count == 0) r.Add(new(true, n, "Im Text steht keine Lücke – setz das gesuchte Wort in eckige Klammern, z. B. [Berlin]."));
                    else if (Luecken(f.Text).Count > 1) r.Add(new(false, n, "Mehrere Lücken: Moodle (GIFT) bekommt je Lücke eine eigene Frage."));
                    break;
                case "zuordnen":
                    var ok = f.Paare.Count(p => p.Links.Trim().Length > 0 && p.Rechts.Trim().Length > 0);
                    if (ok < 2) r.Add(new(true, n, "Zum Zuordnen braucht es mindestens zwei vollständige Paare."));
                    else if (ok < 3) r.Add(new(false, n, "Moodle (GIFT) verlangt beim Zuordnen mindestens drei Paare."));
                    if (f.Paare.Select(p => p.Rechts.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() < f.Paare.Count) r.Add(new(true, n, "Rechts steht etwas doppelt – so ist die Zuordnung nicht eindeutig."));
                    break;
                default: r.Add(new(true, n, $"Unbekannter Fragetyp „{f.Typ}“.")); break;
            }
            if (f.Typ is "auswahl" or "mehrfach" && f.Antworten.Select(a => a.Text.Trim()).Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Count() < f.Antworten.Count(a => a.Text.Trim().Length > 0))
                r.Add(new(false, n, "Eine Antwort steht doppelt."));
        }
        return r;
    }

    // ---------- Bewerten ----------

    /// <summary>Anteil der Punkte (0 bis 1) für eine Eingabe. Mehrfachauswahl: richtige Treffer minus falsche Kreuze (nie unter 0);
    /// Lückentext und Zuordnen: Anteil der richtigen Lücken bzw. Paare. Lücken verzeihen Groß/klein und kleine Tippfehler (wie „Eintippen“ bei den Karteikarten).</summary>
    public static double Anteil(QuizFrage f, QuizEingabe e)
    {
        switch (f.Typ)
        {
            case "auswahl" or "wahrfalsch":
                return e.Gewaehlt is { Count: 1 } g && g[0] >= 0 && g[0] < f.Antworten.Count && f.Antworten[g[0]].Richtig ? 1 : 0;
            case "mehrfach":
            {
                var sel = (e.Gewaehlt ?? []).Where(i => i >= 0 && i < f.Antworten.Count).Distinct().ToList();
                var right = f.Antworten.Count(a => a.Richtig); var wrong = f.Antworten.Count - right;
                if (right == 0) return 0;
                var hit = sel.Count(i => f.Antworten[i].Richtig); var miss = sel.Count(i => !f.Antworten[i].Richtig);
                return Math.Clamp((double)hit / right - (wrong == 0 ? 0 : (double)miss / wrong), 0, 1);
            }
            case "luecke":
            {
                var gaps = Luecken(f.Text); if (gaps.Count == 0) return 0;
                var typed = e.Luecken ?? [];
                var ok = 0;
                for (var i = 0; i < gaps.Count; i++)
                    if (i < typed.Count && typed[i] is { } t && t.Trim().Length > 0 && gaps[i].Any(s => LueckeOk(s, t))) ok++;
                return (double)ok / gaps.Count;
            }
            case "zuordnen":
            {
                if (f.Paare.Count == 0) return 0;
                var z = e.Zuordnung ?? [];
                var ok = 0;
                for (var i = 0; i < f.Paare.Count; i++)
                    if (i < z.Count && z[i] >= 0 && z[i] < f.Paare.Count && string.Equals(f.Paare[z[i]].Rechts.Trim(), f.Paare[i].Rechts.Trim(), StringComparison.OrdinalIgnoreCase)) ok++;
                return (double)ok / f.Paare.Count;
            }
            default: return 0;
        }
    }

    /// <summary>Punkte für eine Antwort; mit Tempo-Bonus bis zu +50 %, wenn voll richtig und schnell (nur mit Zeitlimit).</summary>
    public static double Punkte(Quiz q, QuizFrage f, QuizEingabe e, double sekunden)
    {
        var a = Anteil(f, e); var p = a * f.Punkte; var limit = q.ZeitFuer(f);
        if (limit > 0 && sekunden > limit) return 0; // Zeit abgelaufen
        if (q.TempoBonus && limit > 0 && a >= 1) p += f.Punkte * 0.5 * Math.Clamp(1 - sekunden / limit, 0, 1);
        return Math.Round(p, 2);
    }

    /// <summary>Bis zu dieser Länge muss eine Lücke genau stimmen.</summary>
    public const int ExactUpTo = 4;

    /// <summary>T-0779 (Gegenprüfung H1/M-A): Regel für Lücken im Quiz – Lösungen bis <see cref="ExactUpTo"/> Zeichen und Lösungen mit Ziffern nur genau
    /// („1988“ ist nicht „1989“, „Ast“ nicht „Ost“); längere Wörter wie bei den Karteikarten mit einem Tippfehler je 6 Zeichen (<see cref="LearnBox.CheckTyped"/>).
    /// Gilt nur im Quiz; „Eintippen“ der Karteikarten bleibt unverändert. Derselbe Ablauf steht als <c>tqTypo</c> im HTML-Spieler.</summary>
    public static bool LueckeOk(string expected, string typed)
    {
        static string N(string s) => string.Join(' ', (s ?? "").Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.', '!');
        var e = N(expected); var t = N(typed);
        if (e == t) return true;
        if (e.Length <= ExactUpTo || e.Any(char.IsDigit)) return false;
        return LearnBox.CheckTyped(expected, typed).Ok;
    }

    /// <summary>Richtige Lösung in einem Satz (Rückmeldung, Auswertung, Lösungsblatt).</summary>
    public static string Loesung(QuizFrage f) => f.Typ switch
    {
        "auswahl" or "wahrfalsch" or "mehrfach" => string.Join(", ", f.Antworten.Where(a => a.Richtig).Select(a => a.Text)),
        "luecke" => string.Join(", ", Luecken(f.Text).Select(g => g[0])),
        "zuordnen" => string.Join("; ", f.Paare.Select(p => $"{p.Links} → {p.Rechts}")),
        _ => "",
    };

    // ---------- Mischen ----------

    /// <summary>Reihenfolge der Fragen für einen Durchgang (gleicher Startwert = gleiche Reihenfolge).</summary>
    public static List<int> Reihenfolge(Quiz q, int seed)
    {
        var idx = Enumerable.Range(0, q.Fragen.Count).ToList();
        if (q.FragenMischen) { var r = new Random(seed); idx = [.. idx.OrderBy(_ => r.Next())]; }
        return idx;
    }

    /// <summary>Anzeige-Reihenfolge der Antworten (Wahr/Falsch bleibt immer „Wahr, Falsch“).</summary>
    public static List<int> AntwortReihenfolge(Quiz q, QuizFrage f, int seed)
    {
        var n = f.Typ == "zuordnen" ? f.Paare.Count : f.Antworten.Count;
        var idx = Enumerable.Range(0, n).ToList();
        if ((q.AntwortenMischen || f.Typ == "zuordnen") && f.Typ != "wahrfalsch") { var r = new Random(seed ^ f.Id.GetHashCode(StringComparison.Ordinal)); idx = [.. idx.OrderBy(_ => r.Next())]; }
        return idx;
    }
}

/// <summary>Ein Durchgang im Spielmodus: Fragen der Reihe nach, Zeit je Frage, Punkte, Auswertung. Die Zeit misst die Oberfläche und gibt sie mit.</summary>
public sealed class QuizSpiel
{
    public sealed record Ergebnis(int Frage, double Anteil, double Punkte, double Sekunden, bool ZeitUm);
    public Quiz Quiz { get; }
    public int Seed { get; }
    public IReadOnlyList<int> Reihenfolge { get; }
    public List<Ergebnis> Ergebnisse { get; } = [];
    public QuizSpiel(Quiz q, int seed)
    {
        if (q.Fragen.Count == 0) throw new InvalidOperationException("Das Quiz hat noch keine Frage.");
        if (QuizKit.Pruefen(q).FirstOrDefault(x => x.Fehler) is { } err) throw new InvalidOperationException($"Vor dem Spielen: {err.Feld} – {err.Text}");
        Quiz = q.Copy(); Seed = seed; Reihenfolge = QuizKit.Reihenfolge(Quiz, seed);
    }
    public bool Fertig => Ergebnisse.Count >= Reihenfolge.Count;
    public int Position => Ergebnisse.Count;
    public QuizFrage? Aktuell => Fertig ? null : Quiz.Fragen[Reihenfolge[Position]];
    /// <summary>Wurde die aktuelle Frage schon beantwortet (Rückmeldung wird gezeigt)?</summary>
    public Ergebnis? Letztes => Ergebnisse.Count > 0 ? Ergebnisse[^1] : null;

    public Ergebnis Antworten(QuizEingabe e, double sekunden)
    {
        var f = Aktuell ?? throw new InvalidOperationException("Das Quiz ist schon zu Ende.");
        var limit = Quiz.ZeitFuer(f); var um = limit > 0 && sekunden > limit;
        var r = new Ergebnis(Reihenfolge[Position], um ? 0 : QuizKit.Anteil(f, e), QuizKit.Punkte(Quiz, f, e, sekunden), Math.Round(sekunden, 1), um);
        Ergebnisse.Add(r); return r;
    }
    /// <summary>Zeit abgelaufen: Frage zählt 0 Punkte.</summary>
    public Ergebnis ZeitAbgelaufen() { var f = Aktuell ?? throw new InvalidOperationException("Das Quiz ist schon zu Ende."); var r = new Ergebnis(Reihenfolge[Position], 0, 0, Quiz.ZeitFuer(f), true); Ergebnisse.Add(r); return r; }

    public double Punkte => Math.Round(Ergebnisse.Sum(r => r.Punkte), 2);
    public double Max => Quiz.MaxPunkte;
    /// <summary>Punkte ohne Tempo-Bonus.</summary>
    public double Grundpunkte => Math.Round(Ergebnisse.Sum(r => r.Anteil * Quiz.Fragen[r.Frage].Punkte), 2);
    /// <summary>Prozent der Grundpunkte (ohne Tempo-Bonus, damit „bestanden“ nicht von der Geschwindigkeit abhängt).</summary>
    public int Prozent => Max <= 0 ? 0 : (int)Math.Round(100 * Ergebnisse.Sum(r => r.Anteil * Quiz.Fragen[r.Frage].Punkte) / Max);
    public bool Bestanden => Fertig && Prozent >= Quiz.Bestehen;
}

/// <summary>Einfaches Textformat für Fragen – zum Einfügen, für Dateien und für die KI:
/// „? Frage“ (oder „F:“), darunter „+ richtig“ / „- falsch“; „L: Text mit [Lücke]“; „Z: Frage“ mit Zeilen „Begriff = Bedeutung“;
/// „W: Aussage“ mit „+ Wahr“ oder „+ Falsch“; optional „E: Erklärung“ und „P: 2“ (Punkte). Fragen trennt eine Leerzeile oder die nächste Fragezeile.</summary>
public static partial class QuizText
{
    [GeneratedRegex(@"^\s*(?:\d{1,3}[.)]\s*)?(\?|F:|Frage:|L:|Lücke:|Luecke:|Z:|Zuordnen:|W:|Wahr/Falsch:)\s*(.*)$", RegexOptions.IgnoreCase)] private static partial Regex Head();
    [GeneratedRegex(@"^\s*([+\-*~=])\s+(.*)$")] private static partial Regex Opt();
    [GeneratedRegex(@"^\s*(.+?)\s*(?:=|->|→)\s*(.+)$")] private static partial Regex Pair();

    public static (List<QuizFrage> Fragen, List<string> Ausgelassen) Parse(string text)
    {
        var res = new List<QuizFrage>(); var skipped = new List<string>();
        QuizFrage? cur = null; string kind = "";
        void Close()
        {
            if (cur is null) return;
            var f = cur; cur = null;
            if (kind == "w")
            {
                var right = f.Antworten.FirstOrDefault(a => a.Richtig)?.Text.Trim().ToLowerInvariant();
                if (right is "wahr" or "richtig" or "stimmt" or "ja" or "true") f.Antworten = QuizKit.WahrFalsch(true);
                else if (right is "falsch" or "nein" or "false" or "stimmt nicht") f.Antworten = QuizKit.WahrFalsch(false);
                else { skipped.Add(Short(f.Text) + " (Wahr/Falsch ohne „+ Wahr“ oder „+ Falsch“)"); return; }
            }
            else if (kind == "q")
            {
                var right = f.Antworten.Count(a => a.Richtig);
                var names = f.Antworten.Select(a => a.Text.Trim().ToLowerInvariant()).ToList();
                if (f.Antworten.Count == 2 && names.Contains("wahr") && names.Contains("falsch")) { f.Typ = "wahrfalsch"; f.Antworten = QuizKit.WahrFalsch(f.Antworten.First(a => a.Text.Trim().Equals("wahr", StringComparison.OrdinalIgnoreCase)).Richtig); }
                else f.Typ = right > 1 ? "mehrfach" : "auswahl";
                if (f.Antworten.Count < 2 || right == 0) { skipped.Add(Short(f.Text) + " (keine richtige Antwort oder weniger als zwei Antworten)"); return; }
            }
            else if (kind == "l" && QuizKit.Luecken(f.Text).Count == 0) { skipped.Add(Short(f.Text) + " (keine Lücke in [ ])"); return; }
            else if (kind == "z" && f.Paare.Count < 2) { skipped.Add(Short(f.Text) + " (weniger als zwei Paare)"); return; }
            if (f.Text.Trim().Length == 0) { skipped.Add("(Frage ohne Text)"); return; }
            res.Add(f);
        }
        foreach (var raw in (text ?? "").Replace("\r", "").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.Trim().Length == 0) { Close(); continue; }
            if (line.TrimStart().StartsWith("//", StringComparison.Ordinal) || line.TrimStart().StartsWith('#')) continue;
            if (Head().Match(line) is { Success: true } h)
            {
                Close();
                var tag = h.Groups[1].Value.ToLowerInvariant();
                kind = tag switch { "l:" or "lücke:" or "luecke:" => "l", "z:" or "zuordnen:" => "z", "w:" or "wahr/falsch:" => "w", _ => "q" };
                cur = new QuizFrage { Typ = kind switch { "l" => "luecke", "z" => "zuordnen", "w" => "wahrfalsch", _ => "auswahl" }, Text = h.Groups[2].Value.Trim() };
                continue;
            }
            if (cur is null) continue;
            var t = line.Trim();
            if (t.StartsWith("E:", StringComparison.OrdinalIgnoreCase) || t.StartsWith("Erklärung:", StringComparison.OrdinalIgnoreCase)) { cur.Erklaerung = t[(t.IndexOf(':') + 1)..].Trim(); continue; }
            if (t.StartsWith("P:", StringComparison.OrdinalIgnoreCase) || t.StartsWith("Punkte:", StringComparison.OrdinalIgnoreCase))
            { if (double.TryParse(t[(t.IndexOf(':') + 1)..].Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var p) && p is > 0 and <= 1000) cur.Punkte = p; continue; }
            if (kind is "q" or "w" && Opt().Match(t) is { Success: true } o)
            { cur.Antworten.Add(new QuizAntwort { Text = o.Groups[2].Value.Trim(), Richtig = o.Groups[1].Value is "+" or "*" or "=" }); continue; }
            if (kind == "z" && Pair().Match(t.TrimStart('-', '+', '*', ' ')) is { Success: true } pm)
            { cur.Paare.Add(new QuizPaar { Links = pm.Groups[1].Value.Trim(), Rechts = pm.Groups[2].Value.Trim() }); continue; }
            cur.Text = (cur.Text + " " + t).Trim(); // Fortsetzung des Fragetexts
        }
        Close();
        return (res, skipped);
    }

    static string Short(string s) => s.Length > 50 ? "„" + s[..50] + "…“" : "„" + s + "“";

    /// <summary>Fragen im selben Textformat ausgeben (Gegenstück zu <see cref="Parse"/>).</summary>
    public static string Write(IEnumerable<QuizFrage> fragen)
    {
        var sb = new StringBuilder();
        foreach (var f in fragen)
        {
            switch (f.Typ)
            {
                case "luecke": sb.AppendLine("L: " + f.Text); break;
                case "zuordnen": sb.AppendLine("Z: " + f.Text); foreach (var p in f.Paare) sb.AppendLine($"{p.Links} = {p.Rechts}"); break;
                case "wahrfalsch": sb.AppendLine("W: " + f.Text); sb.AppendLine("+ " + (f.Antworten.FirstOrDefault(a => a.Richtig)?.Text ?? "Wahr")); break;
                default: sb.AppendLine("? " + f.Text); foreach (var a in f.Antworten) sb.AppendLine((a.Richtig ? "+ " : "- ") + a.Text); break;
            }
            if (f.Punkte != 1) sb.AppendLine("P: " + f.Punkte.ToString("0.##", CultureInfo.InvariantCulture));
            if (f.Erklaerung.Trim().Length > 0) sb.AppendLine("E: " + f.Erklaerung.Trim());
            sb.AppendLine();
        }
        return sb.ToString();
    }
}

/// <summary>Moodle-Austauschformat GIFT (Klartext, UTF-8). Export aller fünf Typen, Import der üblichen Teile (Auswahl, Mehrfach mit Prozent, Wahr/Falsch,
/// Kurzantwort/fehlendes Wort, Zuordnen, Erklärung). Nicht unterstützt und gemeldet: Zahlenfragen, Freitext (Essay), Beschreibungen.</summary>
public static partial class QuizGift
{
    static readonly double[] MoodleWeights = [100, 50, 33.33333, 25, 20, 16.66667, 14.28571, 12.5, 11.11111, 10];
    static string W(int n) => n >= 1 && n <= MoodleWeights.Length ? MoodleWeights[n - 1].ToString("0.#####", CultureInfo.InvariantCulture) : (100.0 / n).ToString("0.#####", CultureInfo.InvariantCulture);

    public static string Esc(string s)
    {
        var sb = new StringBuilder();
        foreach (var c in (s ?? "").Replace("\r", "").Replace("\n", " ")) { if (c is '~' or '=' or '#' or '{' or '}' or ':' or '\\') sb.Append('\\'); sb.Append(c); }
        return sb.ToString();
    }

    public static string Write(Quiz q)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// " + q.Titel.Replace("\n", " ") + " – Format GIFT für Moodle"); // T-0784: kein Trendo-Branding in Nutzer-Inhalten
        sb.AppendLine("$CATEGORY: $course$/top/" + Esc(q.Titel.Replace("/", "-")));
        sb.AppendLine();
        var n = 0;
        foreach (var f in q.Fragen)
        {
            n++;
            var name = $"::Frage {n}::";
            var fb = f.Erklaerung.Trim().Length > 0 ? " ####" + Esc(f.Erklaerung.Trim()) : "";
            switch (f.Typ)
            {
                case "auswahl":
                    sb.Append(name).Append(Esc(f.Text)).Append(" {");
                    foreach (var a in f.Antworten.Where(a => a.Text.Trim().Length > 0)) sb.Append(a.Richtig ? " =" : " ~").Append(Esc(a.Text.Trim()));
                    sb.Append(fb).AppendLine(" }");
                    break;
                case "mehrfach":
                {
                    var right = f.Antworten.Count(a => a.Richtig); var wrong = f.Antworten.Count(a => !a.Richtig);
                    sb.Append(name).Append(Esc(f.Text)).Append(" {");
                    foreach (var a in f.Antworten.Where(a => a.Text.Trim().Length > 0)) sb.Append(" ~%").Append(a.Richtig ? W(right) : "-" + W(Math.Max(1, wrong))).Append('%').Append(Esc(a.Text.Trim()));
                    sb.Append(fb).AppendLine(" }");
                    break;
                }
                case "wahrfalsch":
                {
                    var wahr = f.Antworten.FirstOrDefault(a => a.Text.Trim().Equals("Wahr", StringComparison.OrdinalIgnoreCase))?.Richtig ?? true;
                    sb.Append(name).Append(Esc(f.Text)).Append(wahr ? " {TRUE" : " {FALSE").Append(fb).AppendLine("}");
                    break;
                }
                case "luecke":
                {
                    var gaps = QuizKit.Luecken(f.Text);
                    for (var g = 0; g < gaps.Count; g++)
                    {
                        var t = QuizKit.NurLuecke(f.Text, g, "\u0001");
                        var parts = t.Split('\u0001');
                        var nm = gaps.Count == 1 ? name : $"::Frage {n}.{g + 1}::";
                        sb.Append(nm).Append(Esc(parts[0])).Append('{');
                        foreach (var alt in gaps[g]) sb.Append(" =").Append(Esc(alt));
                        sb.Append(fb).Append(" }").AppendLine(parts.Length > 1 ? Esc(parts[1]) : "");
                        if (g < gaps.Count - 1) sb.AppendLine();
                    }
                    break;
                }
                case "zuordnen":
                    sb.Append(name).Append(Esc(f.Text)).Append(" {");
                    foreach (var p in f.Paare.Where(p => p.Links.Trim().Length > 0)) sb.Append(" =").Append(Esc(p.Links.Trim())).Append(" -> ").Append(Esc(p.Rechts.Trim()));
                    sb.Append(fb).AppendLine(" }");
                    break;
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    /// <summary>Text in Fragen-Blöcke zerlegen: Leerzeilen trennen, „//“-Zeilen und „$CATEGORY“ fallen weg; „\“ schützt Sonderzeichen.</summary>
    public static (Quiz Quiz, List<string> Ausgelassen) Read(string text, string titel = "Importiertes Quiz")
    {
        var q = new Quiz { Titel = titel, FragenMischen = false };
        var skipped = new List<string>();
        var blocks = new List<string>(); var cur = new StringBuilder();
        foreach (var raw in (text ?? "").Replace("\r", "").TrimStart('﻿').Split('\n'))
        {
            var l = raw.Trim();
            if (l.StartsWith("//", StringComparison.Ordinal)) { if (q.Fragen.Count == 0 && blocks.Count == 0 && cur.Length == 0 && l.Contains("–")) q.Titel = l[2..].Split('–')[0].Trim(); continue; }
            if (l.StartsWith("$CATEGORY", StringComparison.OrdinalIgnoreCase)) continue;
            if (l.Length == 0) { if (cur.Length > 0) { blocks.Add(cur.ToString()); cur.Clear(); } continue; }
            cur.Append(cur.Length > 0 ? " " : "").Append(l);
        }
        if (cur.Length > 0) blocks.Add(cur.ToString());
        foreach (var b in blocks)
            try { if (Block(b) is { } f) q.Fragen.Add(f); else skipped.Add(Kurz(b)); }
            catch (FormatException e) { skipped.Add(Kurz(b) + " – " + e.Message); }
        if (q.Titel.Trim().Length == 0) q.Titel = titel;
        return (q, skipped);
    }

    static string Kurz(string b) { var s = Unesc(b); return s.Length > 60 ? s[..60] + "…" : s; }

    /// <summary>Position des ersten nicht geschützten Zeichens.</summary>
    static int Find(string s, char c, int from = 0)
    {
        for (var i = from; i < s.Length; i++) { if (s[i] == '\\') { i++; continue; } if (s[i] == c) return i; }
        return -1;
    }

    static string Unesc(string s)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < s.Length; i++) { if (s[i] == '\\' && i + 1 < s.Length) { sb.Append(s[++i]); continue; } sb.Append(s[i]); }
        return sb.ToString().Trim();
    }

    /// <summary>Teile eines Antwortblocks an nicht geschützten „=“ und „~“ (Rückmeldung nach „#“ wird abgetrennt).</summary>
    static List<(char Mark, string Text)> Parts(string body)
    {
        var res = new List<(char, string)>(); var sb = new StringBuilder(); char mark = '\0';
        for (var i = 0; i < body.Length; i++)
        {
            var c = body[i];
            if (c == '\\' && i + 1 < body.Length) { sb.Append(c).Append(body[++i]); continue; }
            if (c is '=' or '~' && !(c == '=' && i + 1 < body.Length && body[i + 1] == '>'))
            {
                if (mark != '\0' || sb.ToString().Trim().Length > 0) res.Add((mark, sb.ToString()));
                sb.Clear(); mark = c; continue;
            }
            sb.Append(c);
        }
        if (mark != '\0' || sb.ToString().Trim().Length > 0) res.Add((mark, sb.ToString()));
        // Rückmeldung je Antwort („#…“) abschneiden
        return [.. res.Select(p => (p.Item1, Find(p.Item2, '#') is var h and >= 0 ? p.Item2[..h] : p.Item2))];
    }

    static QuizFrage? Block(string b)
    {
        var s = b.Trim();
        if (s.StartsWith("::", StringComparison.Ordinal) && s.IndexOf("::", 2, StringComparison.Ordinal) is var e and > 0) s = s[(e + 2)..].Trim();
        if (s.StartsWith('[') && s.IndexOf(']') is var fe and > 0 && s[1..fe] is "html" or "moodle" or "plain" or "markdown") s = s[(fe + 1)..].Trim();
        var open = Find(s, '{'); if (open < 0) return null; // reine Beschreibung
        var close = Find(s, '}', open); if (close < 0) throw new FormatException("„}“ fehlt");
        var before = s[..open]; var after = s[(close + 1)..]; var body = s[(open + 1)..close].Trim();
        var f = new QuizFrage();
        // allgemeine Rückmeldung „####“
        var gf = body.IndexOf("####", StringComparison.Ordinal);
        if (gf >= 0) { f.Erklaerung = Unesc(body[(gf + 4)..]); body = body[..gf].Trim(); }
        if (body.StartsWith('#')) throw new FormatException("Zahlenfragen gibt es im Quiz nicht");
        if (body.Length == 0) throw new FormatException("Freitextfragen (Essay) gibt es im Quiz nicht");
        var upper = body.Split('#')[0].Trim().ToUpperInvariant();
        if (upper is "T" or "TRUE" or "F" or "FALSE")
        {
            f.Typ = "wahrfalsch"; f.Text = Unesc(before + " " + after); f.Antworten = QuizKit.WahrFalsch(upper.StartsWith('T')); return f;
        }
        var parts = Parts(body);
        if (parts.Count == 0) return null;
        if (parts.All(p => p.Mark == '=') && parts.Any(p => Find(p.Text, '-') is var a and >= 0 && a + 1 < p.Text.Length && p.Text[a + 1] == '>'))
        {
            f.Typ = "zuordnen"; f.Text = Unesc(before + " " + after);
            foreach (var p in parts)
            {
                var a = p.Text.IndexOf("->", StringComparison.Ordinal);
                if (a < 0) continue;
                f.Paare.Add(new QuizPaar { Links = Unesc(p.Text[..a]), Rechts = Unesc(p.Text[(a + 2)..]) });
            }
            return f;
        }
        if (parts.All(p => p.Mark == '=')) // Kurzantwort bzw. fehlendes Wort → Lückentext
        {
            var alts = string.Join("|", parts.Select(p => Unesc(Regex.Replace(p.Text, @"^%-?[\d.]+%", "")).Replace("[", "(").Replace("]", ")").Replace("|", "/")).Where(x => x.Length > 0));
            f.Typ = "luecke";
            f.Text = after.Trim().Length > 0 ? (Unesc(before) + " [" + alts + "] " + Unesc(after)).Trim() : (Unesc(before) + " [" + alts + "]").Trim();
            return f;
        }
        // Auswahl oder Mehrfachauswahl
        f.Text = Unesc(before + (after.Trim().Length > 0 ? " ____ " + after : ""));
        var weights = new List<double>();
        foreach (var p in parts)
        {
            var t = p.Text.Trim(); var weight = p.Mark == '=' ? 100.0 : 0;
            if (Regex.Match(t, @"^%(-?[\d.]+)%") is { Success: true } m) { weight = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture); t = t[m.Length..]; }
            f.Antworten.Add(new QuizAntwort { Text = Unesc(t), Richtig = weight > 0 }); weights.Add(weight);
        }
        var right = f.Antworten.Count(a => a.Richtig);
        if (right == 0) throw new FormatException("keine richtige Antwort");
        f.Typ = right == 1 && weights.Max() >= 100 ? "auswahl" : "mehrfach";
        return f;
    }
}
