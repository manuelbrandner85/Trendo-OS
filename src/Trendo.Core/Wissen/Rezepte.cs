using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Trendo.Core.Office;

namespace Trendo.Core.Wissen;

/// <summary>T-0787: ein Lebensmittel der Nährwert-Tabelle, Werte je 100 g. −1 = in der Quelle nicht angegeben.</summary>
public sealed record Lebensmittel(string Id, string Name, string Such, int FdcId, double Kcal, double Eiweiss, double Fett, double GesFett, double Kh, double Zucker, double Ballast, double Salz,
    double Dichte, double Stueck, string Abteilung);

/// <summary>T-0787 (Rezepte &amp; Ernährung): Nährwert-Tabelle aus USDA FoodData Central, SR Legacy (gemeinfrei, CC0 1.0). Namen und Suchwörter auf Deutsch sind eigene Zuordnung;
/// die Daten erzeugt <c>tools/rezepte/naehrwerte_gen.py</c> aus den Original-CSV-Dateien (Herkunft je Zeile in <c>tools/rezepte/herkunft-usda-sr.tsv</c>).</summary>
public static partial class NaehrwertTabelle
{
    public const string Quelle = "USDA FoodData Central, SR Legacy (April 2018), fdc.nal.usda.gov";
    public const string Lizenz = "gemeinfrei, CC0 1.0 Universal";
    public const string Hinweis = "Richtwerte: berechnet aus Durchschnittswerten der Nährwert-Tabelle (rohe Zutaten, ohne Garverluste). Keine Ernährungs- oder Gesundheitsberatung.";

    static readonly Lazy<Dictionary<string, Lebensmittel>> ById = new(() => Alle.ToDictionary(l => l.Id, StringComparer.OrdinalIgnoreCase));
    public static Lebensmittel? Find(string? id) => id is { Length: > 0 } && ById.Value.TryGetValue(id, out var l) ? l : null;

    static string Norm(string s)
    {
        s = Regex.Replace(s.ToLowerInvariant(), @"\([^)]*\)", " ");
        s = Regex.Replace(s, @"[^\p{L}\p{N}]+", " ").Trim();
        return Regex.Replace(s, @"\s+", " ");
    }

    static readonly Lazy<List<(Lebensmittel L, string Term)>> Terms = new(() =>
    {
        var list = new List<(Lebensmittel, string)>();
        foreach (var l in Alle)
        {
            list.Add((l, Norm(l.Name)));
            list.Add((l, Norm(l.Id.Replace('-', ' '))));
            foreach (var s in l.Such.Split('|', StringSplitOptions.RemoveEmptyEntries)) list.Add((l, Norm(s)));
        }
        return list.Where(x => x.Item2.Length > 0).ToList();
    });

    /// <summary>Ein- und Mehrzahl: „Eier“ → „ei“, „Zwiebeln“ → „zwiebel“, „Tomaten“ → „tomate“, „Knoblauchzehen“ → „knoblauchzehe“.</summary>
    static IEnumerable<string> Formen(string w)
    {
        yield return w;
        if (w.EndsWith("er") && w.Length > 3) yield return w[..^2];
        if (w.EndsWith("en") && w.Length > 4) { yield return w[..^1]; yield return w[..^2]; }
        if (w.EndsWith('n') && w.Length > 3) yield return w[..^1];
        if (w.EndsWith('s') && w.Length > 3) yield return w[..^1];
        if (w.EndsWith('e') && w.Length > 3) yield return w[..^1];
    }

    /// <summary>Beste Zuordnung für einen Zutatennamen (oder null). Genaue Treffer vor ganzen Wörtern vor zusammengesetzten Wörtern („Bio-Weizenmehl“, „Rinderhackfleisch“).</summary>
    public static Lebensmittel? Match(string name) => Kandidaten(name, 1).FirstOrDefault();

    /// <summary>Die besten Zuordnungen (für das Rechtsklick-Menü „Anderes Lebensmittel“).</summary>
    public static List<Lebensmittel> Kandidaten(string name, int max = 6)
    {
        var n = Norm(name); if (n.Length == 0) return [];
        var words = n.Split(' ');
        var scores = new Dictionary<Lebensmittel, int>();
        void Score(Lebensmittel l, int s) { if (!scores.TryGetValue(l, out var o) || s > o) scores[l] = s; }
        foreach (var (l, t) in Terms.Value)
        {
            if (Formen(n).Contains(t)) { Score(l, 1000 + t.Length); continue; }
            if (t.Length < 3) continue;
            if (t.Contains(' ') ? (" " + n + " ").Contains(" " + t + " ") : words.Any(w => Formen(w).Contains(t))) { Score(l, 500 + t.Length * 10); continue; }
            if (t.Length >= 4 && words.Any(w => Formen(w).Any(f => f.EndsWith(t, StringComparison.Ordinal)))) { Score(l, 200 + t.Length * 10); continue; }
            if (t.Length >= 5 && words.Any(w => w.StartsWith(t, StringComparison.Ordinal))) Score(l, 100 + t.Length * 10);
        }
        return scores.OrderByDescending(kv => kv.Value).ThenBy(kv => Array.IndexOf(Alle, kv.Key)).Take(max).Select(kv => kv.Key).ToList();
    }

    /// <summary>Freitext-Suche in der Tabelle (Name und Suchwörter), für die Auswahl von Hand.</summary>
    public static List<Lebensmittel> Suche(string text, int max = 12)
    {
        var n = Norm(text);
        if (n.Length == 0) return [];
        var hits = Kandidaten(text, max);
        foreach (var l in Alle) if (hits.Count < max && !hits.Contains(l) && (Norm(l.Name).Contains(n) || Norm(l.Such).Contains(n))) hits.Add(l);
        return hits;
    }
}

/// <summary>Maßeinheit in der Küche: Masse (g), Volumen (ml) oder Stück; <see cref="Faktor"/> in g, ml bzw. Stück.</summary>
public sealed record KuechenEinheit(string Id, string Kurz, string Lang, string Art, double Faktor, string[] Namen);

/// <summary>Eine Zutat: Menge (bis), Einheit, Name, Hinweis („gewürfelt“), Lebensmittel der Tabelle; <see cref="Gruppe"/> = Zwischenüberschrift („Für den Teig:“).</summary>
public sealed record Zutat(double? Menge, double? Bis, string Einheit, string Name, string Notiz = "", string Lm = "", bool Gruppe = false)
{
    /// <summary>„-“ = die Person hat „keine Zuordnung“ gewählt.</summary>
    public const string KeinLm = "-";
}

/// <summary>Ein Rezept des Rezeptbuchs.</summary>
public sealed record Rezept(string Id, string Name, int Portionen, List<Zutat> Zutaten, List<string> Schritte)
{
    public string Beschreibung { get; init; } = "";
    public string PortionWort { get; init; } = "Portionen";
    public string Kategorie { get; init; } = "";
    public string Kueche { get; init; } = "";
    public string Stichwoerter { get; init; } = "";
    public int Vorbereitung { get; init; }
    public int Garzeit { get; init; }
    public string Quelle { get; init; } = "";
    public string Notizen { get; init; } = "";
}

/// <summary>Nährwerte (Summe oder je Portion) mit Angabe, was nicht erfasst wurde.</summary>
public sealed record Naehrwerte(double Kcal, double Eiweiss, double Fett, double GesFett, double Kh, double Zucker, double Ballast, double Salz, double Gramm,
    int Erfasst, IReadOnlyList<string> Fehlt, IReadOnlyList<string> OhneMenge)
{
    public Naehrwerte Mal(double f) => this with { Kcal = Kcal * f, Eiweiss = Eiweiss * f, Fett = Fett * f, GesFett = GesFett * f, Kh = Kh * f, Zucker = Zucker * f, Ballast = Ballast * f, Salz = Salz * f, Gramm = Gramm * f };
    public double KJ => Kcal * 4.184;
    public bool Vollstaendig => Fehlt.Count == 0;
}

/// <summary>Eintrag im Wochenplan: Tag 0 (Montag) … 6, Mahlzeit, Rezept, Portionen.</summary>
public sealed record PlanEintrag(string Id, int Tag, string Mahlzeit, string Rezept, int Portionen);

/// <summary>Zeile der Einkaufsliste: zusammengefasst über alle geplanten Rezepte.</summary>
public sealed record EinkaufPosten(string Key, string Name, double? Menge, string Einheit, string Abteilung, IReadOnlyList<string> Fuer, string Lm)
{
    public string Text => (Menge is { } m ? RezeptKit.Zahl(m, Einheit) + (Einheit.Length > 0 ? " " + Einheit : "") + " " : "") + Name;
}

/// <summary>T-0787: Fachkern „Rezepte &amp; Ernährung“ – Zutaten lesen, Portionen und Einheiten umrechnen, Nährwerte je Portion, Wochenplan, Einkaufsliste,
/// schema.org/Recipe (JSON-LD) lesen und schreiben, Druckfassung. Eigene Umsetzung (Clean-Room, Anforderungen in docs/nachbau/rezepte/anforderungen.md).</summary>
public static class RezeptKit
{
    public static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    public const int MaxRezepte = 2000, MaxZutaten = 120, MaxSchritte = 80, MaxZeile = 300, MaxPortionen = 500, MaxPlan = 200;
    public static readonly string[] Tage = ["Montag", "Dienstag", "Mittwoch", "Donnerstag", "Freitag", "Samstag", "Sonntag"];
    public static readonly string[] Mahlzeiten = ["Frühstück", "Mittag", "Abend", "Snack"];

    /// <summary>Einheiten (eigene Festlegung): 1 EL = 15 ml, 1 TL = 5 ml, 1 Tasse = 250 ml, 1 Becher = 200 ml, 1 Prise ≈ 0,5 g, 1 Msp. ≈ 0,3 g.</summary>
    public static readonly KuechenEinheit[] Einheiten =
    [
        new("g", "g", "Gramm", "masse", 1, ["g", "gr", "gramm", "grm"]),
        new("kg", "kg", "Kilogramm", "masse", 1000, ["kg", "kilo", "kilogramm"]),
        new("mg", "mg", "Milligramm", "masse", 0.001, ["mg", "milligramm"]),
        new("pfund", "Pfund", "Pfund", "masse", 500, ["pfund", "pfd"]),
        new("ml", "ml", "Milliliter", "volumen", 1, ["ml", "milliliter"]),
        new("cl", "cl", "Zentiliter", "volumen", 10, ["cl"]),
        new("dl", "dl", "Deziliter", "volumen", 100, ["dl"]),
        new("l", "l", "Liter", "volumen", 1000, ["l", "liter", "ltr"]),
        new("el", "EL", "Esslöffel", "volumen", 15, ["el", "essl", "esslöffel", "eßlöffel", "essloeffel"]),
        new("tl", "TL", "Teelöffel", "volumen", 5, ["tl", "teel", "teelöffel", "teeloeffel"]),
        new("tasse", "Tasse", "Tasse", "volumen", 250, ["tasse", "tassen"]),
        new("becher", "Becher", "Becher", "volumen", 200, ["becher"]),
        new("prise", "Prise", "Prise", "masse", 0.5, ["prise", "prisen"]),
        new("msp", "Msp.", "Messerspitze", "masse", 0.3, ["msp", "messerspitze", "messerspitzen"]),
        new("stueck", "Stück", "Stück", "stueck", 1, ["stück", "stk", "st", "stueck"]),
        new("zehe", "Zehe", "Zehe", "stueck", 1, ["zehe", "zehen"]),
        new("scheibe", "Scheibe", "Scheibe", "anders", 1, ["scheibe", "scheiben"]),
        new("bund", "Bund", "Bund", "anders", 1, ["bund"]),
        new("stange", "Stange", "Stange", "anders", 1, ["stange", "stangen"]),
        new("dose", "Dose", "Dose", "anders", 1, ["dose", "dosen"]),
        new("pck", "Pck.", "Päckchen", "anders", 1, ["pck", "päckchen", "packung", "pkg", "tüte"]),
        new("handvoll", "Handvoll", "Handvoll", "anders", 1, ["handvoll"]),
        new("zweig", "Zweig", "Zweig", "anders", 1, ["zweig", "zweige"]),
        new("blatt", "Blatt", "Blatt", "anders", 1, ["blatt", "blätter"]),
    ];

    /// <summary>Gewicht je Packungseinheit (nur wo eindeutig): Dose 400 g (abgetropft 240 g), Päckchen Backpulver 15 g, Trockenhefe 7 g, Würfel Hefe 42 g.</summary>
    static readonly Dictionary<(string Lm, string Einheit), double> PackGramm = new()
    {
        [("tomaten-dose", "dose")] = 400, [("kidneybohnen", "dose")] = 240, [("kichererbsen", "dose")] = 240, [("kokosmilch", "dose")] = 400, [("thunfisch-dose", "dose")] = 140, [("mais", "dose")] = 285,
        [("backpulver", "pck")] = 15, [("hefe-trocken", "pck")] = 7, [("hefe-frisch", "stueck")] = 42, [("hefe-frisch", "pck")] = 42,
        [("sahne", "becher")] = 200, [("saure-sahne", "becher")] = 200, [("joghurt", "becher")] = 150, [("frischkaese", "pck")] = 200, [("mozzarella", "pck")] = 125, [("feta", "pck")] = 200,
        [("vollkornbrot", "scheibe")] = 45, [("weissbrot", "scheibe")] = 25, [("roggenbrot", "scheibe")] = 40, [("kochschinken", "scheibe")] = 20, [("gouda", "scheibe")] = 20, [("emmentaler", "scheibe")] = 20,
        [("petersilie", "bund")] = 30, [("schnittlauch", "bund")] = 25, [("dill", "bund")] = 20, [("basilikum", "bund")] = 20, [("lauchzwiebel", "bund")] = 100, [("radieschen", "bund")] = 150,
    };

    public static KuechenEinheit? EinheitVon(string? s)
    {
        if (s is not { Length: > 0 }) return null;
        var k = s.Trim().TrimEnd('.').ToLowerInvariant();
        return Einheiten.FirstOrDefault(e => e.Id == k || e.Kurz.TrimEnd('.').Equals(k, StringComparison.OrdinalIgnoreCase) || e.Namen.Contains(k));
    }

    // ------------------------------------------------------------ Zahlen

    static readonly Dictionary<char, double> Brueche = new() { ['½'] = 0.5, ['¼'] = 0.25, ['¾'] = 0.75, ['⅓'] = 1.0 / 3, ['⅔'] = 2.0 / 3, ['⅛'] = 0.125, ['⅜'] = 0.375, ['⅝'] = 0.625, ['⅞'] = 0.875 };
    const string ZahlMuster = @"(?:\d+\s+\d+/\d+|\d+/\d+|\d+(?:[.,]\d+)?(?:\s*[½¼¾⅓⅔⅛⅜⅝⅞])?|[½¼¾⅓⅔⅛⅜⅝⅞])";

    /// <summary>„1,5“, „1.5“, „1/2“, „½“, „1 ½“, „1 1/2“ → Zahl.</summary>
    public static double? LiesZahl(string s)
    {
        s = s.Trim(); if (s.Length == 0) return null;
        double sum = 0; var any = false;
        foreach (var part in Regex.Matches(s, @"\d+/\d+|\d+(?:[.,]\d+)?|[½¼¾⅓⅔⅛⅜⅝⅞]").Select(m => m.Value))
        {
            if (part.Contains('/')) { var ab = part.Split('/'); if (double.TryParse(ab[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var b) && b > 0) sum += double.Parse(ab[0], CultureInfo.InvariantCulture) / b; else return null; }
            else if (Brueche.TryGetValue(part[0], out var f)) sum += f;
            else sum += double.Parse(part.Replace(',', '.'), CultureInfo.InvariantCulture);
            any = true;
        }
        return any ? sum : null;
    }

    /// <summary>Menge schön schreiben: Löffel, Tassen und Stück als Bruch (¼, ⅓, ½ …), Gramm und Milliliter sinnvoll gerundet.</summary>
    public static string Zahl(double v, string einheit = "")
    {
        if (v <= 0) return "0";
        var e = EinheitVon(einheit);
        var bruch = e is null || e.Art is "stueck" or "anders" || e.Id is "el" or "tl" or "tasse" or "becher" or "prise" or "msp";
        if (bruch)
        {
            var ganz = Math.Floor(v); var rest = v - ganz;
            (double W, string S)[] teile = [(0, ""), (0.125, "⅛"), (0.25, "¼"), (1.0 / 3, "⅓"), (0.5, "½"), (2.0 / 3, "⅔"), (0.75, "¾"), (1, "")];
            var best = teile.OrderBy(t => Math.Abs(t.W - rest)).First();
            if (Math.Abs(best.W - rest) < 0.04 || v >= 10)
            {
                if (best.W == 1) { ganz += 1; best = (0, ""); }
                if (v >= 10) return Math.Round(v).ToString(De);
                return ganz == 0 ? (best.S.Length > 0 ? best.S : "0") : ganz.ToString(De) + (best.S.Length > 0 ? " " + best.S : "");
            }
            return Math.Round(v, 2).ToString("0.##", De);
        }
        if (e!.Id is "kg" or "l") return Math.Round(v, 2).ToString("0.##", De);
        if (v >= 100) return (Math.Round(v / 5) * 5).ToString("0", De);
        if (v >= 10) return Math.Round(v).ToString("0", De);
        return Math.Round(v, 1).ToString("0.#", De);
    }

    // ------------------------------------------------------------ Zutaten lesen und schreiben

    static readonly Regex ZeileRx = new(@"^\s*(?:(?<a>" + ZahlMuster + @")(?:\s*(?:-|–|bis)\s*(?<b>" + ZahlMuster + @"))?)?\s*(?<rest>.*)$", RegexOptions.Compiled);
    static readonly string[] OhneMengeWoerter = ["etwas", "einige", "einen schuss", "ein schuss", "nach belieben", "nach geschmack", "n. b.", "n.b.", "evtl.", "optional", "zum bestreuen", "zum braten"];

    /// <summary>Eine Zutatenzeile lesen: „200 g Mehl“, „1/2 TL Salz“, „2–3 Eier, Größe M“, „1 ½ EL Olivenöl“, „Salz und Pfeffer nach Geschmack“, „Für den Teig:“ (Zwischenüberschrift).</summary>
    public static Zutat LiesZutat(string zeile) => Lies(zeile).Zutat;

    /// <summary>Größte Menge, die eine Zeile haben darf (1 000 000 g/ml/Stück) – mehr ist ein Tippfehler.</summary>
    public const double MaxMenge = 1_000_000;
    static readonly Regex UngefaehrRx = new(@"^(?:ca\.?|circa|zirka|etwa|ungefähr|rund)\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex TausenderRx = new(@"^(\d{1,3}(?:\.\d{3})+)(?![.,\d])", RegexOptions.Compiled);
    static readonly Regex MalRx = new(@"^(\d{1,4})\s*[x×]\s*(?=\d)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Wie <see cref="LiesZutat"/>, dazu ein Hinweis, wenn etwas mehrdeutig oder nicht lesbar war (Tausenderpunkt, „1e5“, Menge 0 oder riesig).</summary>
    public static (Zutat Zutat, string? Hinweis) Lies(string zeile)
    {
        var z = Regex.Replace((zeile ?? "").Replace('\t', ' '), @"\s+", " ").Trim().TrimStart('-', '–', '—', '*', '•', '·').Trim();
        if (z.Length > MaxZeile) z = z[..MaxZeile];
        if (z.Length == 0) return (new(null, null, "", ""), null);
        if (z.EndsWith(':') && !Regex.IsMatch(z, @"^\s*\d")) return (new(null, null, "", z.TrimEnd(':').Trim(), Gruppe: true), null);
        string? hinweis = null; var notizVorn = "";
        // „ca. 200 g“, „etwa 200 g“: Menge gilt, „ca.“ wandert in den Hinweis der Zutat
        if (UngefaehrRx.Match(z) is { Success: true } um0 && z.Length > um0.Length && char.IsDigit(z[um0.Length])) { notizVorn = "ca."; z = z[um0.Length..]; }
        // „2 x 200 g“ → 400 g
        double mal = 1;
        if (MalRx.Match(z) is { Success: true } mm) { mal = double.Parse(mm.Groups[1].Value, CultureInfo.InvariantCulture); z = z[mm.Length..]; }
        // „1.000 g“: Punkt mit genau drei Ziffern danach = Tausenderpunkt (deutsche Schreibweise) – wird gemeldet
        if (TausenderRx.Match(z) is { Success: true } tm)
        {
            var ohne = tm.Groups[1].Value.Replace(".", "");
            hinweis = $"„{tm.Groups[1].Value}“ als {ohne} gelesen (Punkt als Tausendertrennzeichen).";
            z = ohne + z[tm.Length..];
        }
        var m = ZeileRx.Match(z);
        double? a = m.Groups["a"].Success ? LiesZahl(m.Groups["a"].Value) : null, b = m.Groups["b"].Success ? LiesZahl(m.Groups["b"].Value) : null;
        var rest = m.Groups["rest"].Value.Trim();
        // „1e5 g“, „1E+20 g“: keine Küchenzahl
        if (a is not null && Regex.IsMatch(rest, @"^[eE][+-]?\d"))
            return (new(null, null, "", z, notizVorn), $"„{z.Split(' ')[0]}“ ist keine lesbare Menge – bitte als normale Zahl schreiben.");
        if (a is not null) { a *= mal; b *= mal; }
        if (a is <= 0 or > MaxMenge || b is <= 0 or > MaxMenge)
        {
            hinweis = $"Menge „{m.Groups["a"].Value}“ liegt außerhalb von 0 bis 1 000 000 – nicht übernommen.";
            a = null; b = null;
        }
        var einheit = "";
        // Einheit: erstes Wort (auch direkt an der Zahl wie „200g“)
        var um = Regex.Match(rest, @"^(?<u>[\p{L}]+\.?)(?=\s|$)");
        if (a is not null && um.Success && EinheitVon(um.Groups["u"].Value) is { } e && rest.Length > um.Length) { einheit = e.Id; rest = rest[um.Length..].Trim(); }
        else if (a is null && um.Success && EinheitVon(um.Groups["u"].Value) is { Art: "masse" or "volumen" } e2 && e2.Id is "prise" or "msp" && rest.Length > um.Length) { a = 1; einheit = e2.Id; rest = rest[um.Length..].Trim(); }
        // „große/kleine/mittelgroße …“ bleibt im Namen; Hinweis nach dem ersten Komma
        var notiz = "";
        var komma = rest.IndexOf(',');
        if (komma > 0) { notiz = rest[(komma + 1)..].Trim(); rest = rest[..komma].Trim(); }
        foreach (var w in OhneMengeWoerter)
        {
            var i = rest.IndexOf(w, StringComparison.OrdinalIgnoreCase);
            if (i >= 0) { notiz = (rest[i..].Trim() + (notiz.Length > 0 ? ", " + notiz : "")).Trim(); rest = (rest[..i]).Trim(); }
        }
        if (rest.Length == 0 && notiz.Length > 0) { rest = notiz; notiz = ""; }
        if (notizVorn.Length > 0) notiz = notiz.Length > 0 ? notizVorn + " " + notiz : notizVorn;
        if (b is not null && a is not null && b < a) (a, b) = (b, a);
        // „1 Dose Tomaten“: Dosenware vor Frischware
        var lm = einheit == "dose" ? (NaehrwertTabelle.Kandidaten(rest, 6) is var kd && kd.FirstOrDefault(k => k.Id.EndsWith("-dose", StringComparison.Ordinal) || DosenWare.Contains(k.Id)) is { } dk ? dk.Id : kd.FirstOrDefault()?.Id ?? "")
            : NaehrwertTabelle.Match(rest)?.Id ?? "";
        return (new(a, b, einheit, rest, notiz, lm), hinweis);
    }

    static readonly HashSet<string> DosenWare = ["kidneybohnen", "kichererbsen", "kokosmilch", "mais", "oliven"];

    /// <summary>Zeile wieder als Text (für das Eingabefeld und den Druck); <paramref name="faktor"/> für umgerechnete Portionen.</summary>
    public static string Zeile(Zutat z, double faktor = 1)
    {
        if (z.Gruppe) return z.Name + ":";
        var sb = new StringBuilder();
        if (z.Menge is { } m) { sb.Append(Zahl(m * faktor, z.Einheit)); if (z.Bis is { } b) sb.Append('–').Append(Zahl(b * faktor, z.Einheit)); sb.Append(' '); }
        if (EinheitVon(z.Einheit) is { } e) sb.Append(e.Kurz).Append(' ');
        sb.Append(z.Name);
        if (z.Notiz.Length > 0) sb.Append(", ").Append(z.Notiz);
        return sb.ToString().Trim();
    }

    /// <summary>Menge mit Einheit allein („200 g“, „½ TL“) – für die Spalte „Menge“.</summary>
    public static string MengeText(Zutat z, double faktor = 1)
    {
        if (z.Menge is not { } m0) return "";
        if (faktor != 1) { z = Glaette(z with { Menge = m0 * faktor, Bis = z.Bis * faktor }); faktor = 1; } // umgerechnet: passende Einheit (1000 ml → 1 l)
        var m = z.Menge!.Value;
        var s = Zahl(m * faktor, z.Einheit) + (z.Bis is { } b ? "–" + Zahl(b * faktor, z.Einheit) : "");
        return EinheitVon(z.Einheit) is { } e ? s + " " + e.Kurz : s;
    }

    public static Lebensmittel? LmVon(Zutat z) => z.Lm == Zutat.KeinLm ? null : NaehrwertTabelle.Find(z.Lm);

    /// <summary>Gramm einer Zutat (Mittelwert bei „2–3“), null = nicht bestimmbar (z. B. „1 Bund“ ohne Gewicht, Volumen ohne Dichte).</summary>
    public static double? Gramm(Zutat z, double faktor = 1)
    {
        if (z.Gruppe || z.Menge is not { } m0) return null;
        var m = (z.Bis is { } b ? (m0 + b) / 2 : m0) * faktor;
        var lm = LmVon(z); var e = EinheitVon(z.Einheit);
        if (e is null) return lm is { Stueck: > 0 } ? m * lm.Stueck : null; // „2 Eier“
        if (lm is not null && PackGramm.TryGetValue((lm.Id, e.Id), out var pg)) return m * pg;
        return e.Art switch
        {
            "masse" => m * e.Faktor,
            "volumen" when lm is { Dichte: > 0 } => m * e.Faktor * lm.Dichte,
            "stueck" when lm is { Stueck: > 0 } => m * lm.Stueck,
            _ => null,
        };
    }

    static double W(double v) => v < 0 ? 0 : v;

    /// <summary>Nährwerte des ganzen Rezepts (Richtwerte). Zutaten ohne Menge („Salz nach Geschmack“) zählen nicht und stehen in <see cref="Naehrwerte.OhneMenge"/>;
    /// Zutaten mit Menge, aber ohne Zuordnung oder ohne Gewicht stehen in <see cref="Naehrwerte.Fehlt"/>.</summary>
    public static Naehrwerte Berechne(Rezept r, double faktor = 1)
    {
        double kcal = 0, ei = 0, fett = 0, gf = 0, kh = 0, zu = 0, ba = 0, sa = 0, g = 0; var n = 0;
        var fehlt = new List<string>(); var ohne = new List<string>();
        foreach (var z in r.Zutaten.Where(z => !z.Gruppe && z.Name.Length > 0))
        {
            if (z.Menge is null) { ohne.Add(z.Name); continue; }
            var lm = LmVon(z); var gramm = Gramm(z, faktor);
            if (lm is null || gramm is null) { fehlt.Add(z.Name); continue; }
            var f = gramm.Value / 100;
            kcal += W(lm.Kcal) * f; ei += W(lm.Eiweiss) * f; fett += W(lm.Fett) * f; gf += W(lm.GesFett) * f; kh += W(lm.Kh) * f; zu += W(lm.Zucker) * f; ba += W(lm.Ballast) * f; sa += W(lm.Salz) * f; g += gramm.Value; n++;
        }
        return new(kcal, ei, fett, gf, kh, zu, ba, sa, g, n, fehlt, ohne);
    }

    /// <summary>Je Portion (bei <paramref name="portionen"/> Portionen des umgerechneten Rezepts).</summary>
    public static Naehrwerte JePortion(Rezept r) => Berechne(r).Mal(1.0 / Math.Max(1, r.Portionen));

    /// <summary>Rezept auf andere Portionen umrechnen (alle Mengen mal neu/alt). Ganze Stück bleiben als Bruch lesbar.</summary>
    public static Rezept Skaliere(Rezept r, int portionen)
    {
        portionen = Math.Clamp(portionen, 1, MaxPortionen);
        var f = portionen / (double)Math.Max(1, r.Portionen);
        return r with { Portionen = portionen, Zutaten = r.Zutaten.Select(z => z.Menge is null ? z : Glaette(z with { Menge = z.Menge * f, Bis = z.Bis * f })).ToList() };
    }

    /// <summary>Einheit passend wählen: 1000 g → 1 kg, 0,25 kg → 250 g, 3 TL → 1 EL, 1500 ml → 1,5 l.</summary>
    public static Zutat Glaette(Zutat z)
    {
        if (z.Menge is not { } m) return z;
        var e = EinheitVon(z.Einheit);
        Zutat To(string id, double f) => z with { Einheit = id, Menge = m * f, Bis = z.Bis * f };
        return e?.Id switch
        {
            "g" when m >= 1000 => To("kg", 0.001),
            "kg" when m < 1 => To("g", 1000),
            "ml" when m >= 1000 => To("l", 0.001),
            "l" when m < 0.5 => To("ml", 1000),
            "tl" when m >= 3 && Math.Abs(m / 3 - Math.Round(m / 3)) < 0.01 => To("el", 1.0 / 3),
            _ => z,
        };
    }

    /// <summary>Löffel, Tassen und Becher in Gramm bzw. Milliliter umrechnen (wo die Dichte bekannt ist: Gramm, sonst Milliliter).</summary>
    public static (Rezept Rezept, int Geaendert) InMetrisch(Rezept r)
    {
        var n = 0;
        var list = r.Zutaten.Select(z =>
        {
            var e = EinheitVon(z.Einheit);
            if (z.Menge is null || e is null || e.Id is not ("el" or "tl" or "tasse" or "becher" or "pfund" or "cl" or "dl")) return z;
            n++;
            var lm = LmVon(z);
            if (e.Art == "masse") return Glaette(z with { Einheit = "g", Menge = z.Menge * e.Faktor, Bis = z.Bis * e.Faktor });
            var fluessig = lm is null || lm.Abteilung is "Getränke" or "Öle & Fette" || lm.Id is "vollmilch" or "fettarme-milch" or "sahne" or "kochsahne" or "sojasauce" or "apfelessig" or "balsamico" or "zitronensaft" or "orangensaft" or "haehnchenbruehe" or "ahornsirup";
            if (!fluessig && lm is { Dichte: > 0 } && Gramm(z) is { } g) { var f = g / z.Menge!.Value; return Glaette(z with { Einheit = "g", Menge = g, Bis = z.Bis * f }); }
            return Glaette(z with { Einheit = "ml", Menge = z.Menge * e.Faktor, Bis = z.Bis * e.Faktor });
        }).ToList();
        return (r with { Zutaten = list }, n);
    }

    // ------------------------------------------------------------ Dokument (JSON)

    public static JsonObject Neu() => new() { ["rezepte"] = new JsonArray(), ["plan"] = new JsonObject { ["start"] = Montag(DateTime.Today).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), ["eintraege"] = new JsonArray() }, ["liste"] = new JsonObject { ["abgehakt"] = new JsonArray(), ["extra"] = new JsonArray() } };

    public static DateTime Montag(DateTime d) => d.Date.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    static string S(JsonNode? n) => n is JsonValue v ? (v.TryGetValue<string>(out var s) ? s : v.GetValueKind() == JsonValueKind.Number ? v.ToJsonString() : "") : "";
    /// <summary>Zahl lesen – gleich ob als Ganzzahl oder Kommazahl gespeichert (ein JsonValue aus int liefert über TryGetValue&lt;double&gt; nichts).</summary>
    static double? D(JsonNode? n) => n is JsonValue v && v.GetValueKind() == JsonValueKind.Number && double.TryParse(v.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var x) && double.IsFinite(x) ? x : null;
    static int I(JsonNode? n, int fb = 0) => D(n) is { } x ? (int)Math.Round(x) : int.TryParse(S(n), out var i) ? i : fb;
    static string Cut(string s, int max) => s.Length <= max ? s : s[..max];
    public static string NewId(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8];

    public static JsonObject ZutatJson(Zutat z)
    {
        var o = new JsonObject { ["name"] = Cut(z.Name, MaxZeile) };
        if (z.Gruppe) { o["gruppe"] = true; return o; }
        if (z.Menge is { } m) o["menge"] = Math.Round(m, 4);
        if (z.Bis is { } b) o["bis"] = Math.Round(b, 4);
        if (z.Einheit.Length > 0) o["einheit"] = z.Einheit;
        if (z.Notiz.Length > 0) o["notiz"] = Cut(z.Notiz, MaxZeile);
        if (z.Lm.Length > 0) o["lm"] = z.Lm;
        return o;
    }

    public static Zutat ZutatVon(JsonNode? n)
    {
        if (n is not JsonObject o) return LiesZutat(S(n));
        var e = EinheitVon(S(o["einheit"]))?.Id ?? "";
        var lm = S(o["lm"]); if (lm != Zutat.KeinLm && NaehrwertTabelle.Find(lm) is null) lm = "";
        double? Pos(JsonNode? x) => D(x) is { } v && v > 0 && v < 1e6 ? v : null;
        return new(Pos(o["menge"]), Pos(o["bis"]), e, Cut(S(o["name"]), MaxZeile), Cut(S(o["notiz"]), MaxZeile), lm, o["gruppe"]?.GetValueKind() == JsonValueKind.True);
    }

    public static JsonObject RezeptJson(Rezept r) => new()
    {
        ["id"] = r.Id, ["name"] = Cut(r.Name, 200), ["beschreibung"] = Cut(r.Beschreibung, 4000), ["portionen"] = Math.Clamp(r.Portionen, 1, MaxPortionen), ["portionWort"] = Cut(r.PortionWort, 40),
        ["kategorie"] = Cut(r.Kategorie, 100), ["kueche"] = Cut(r.Kueche, 100), ["stichwoerter"] = Cut(r.Stichwoerter, 400), ["vorbereitung"] = Math.Clamp(r.Vorbereitung, 0, 100_000), ["garzeit"] = Math.Clamp(r.Garzeit, 0, 100_000),
        ["quelle"] = Cut(r.Quelle, 1000), ["notizen"] = Cut(r.Notizen, 4000),
        ["zutaten"] = new JsonArray([.. r.Zutaten.Take(MaxZutaten).Select(z => (JsonNode)ZutatJson(z))]),
        ["schritte"] = new JsonArray([.. r.Schritte.Take(MaxSchritte).Select(s => (JsonNode)Cut(s, 4000))]),
    };

    public static Rezept RezeptVon(JsonNode? n)
    {
        var o = n as JsonObject ?? [];
        return new(S(o["id"]) is { Length: > 0 } id ? id : NewId("r"), Cut(S(o["name"]), 200), Math.Clamp(I(o["portionen"], 4), 1, MaxPortionen),
            (o["zutaten"] as JsonArray ?? []).Take(MaxZutaten).Select(ZutatVon).Where(z => z.Name.Length > 0).ToList(),
            (o["schritte"] as JsonArray ?? []).Take(MaxSchritte).Select(x => Cut(S(x), 4000)).Where(s => s.Trim().Length > 0).ToList())
        {
            Beschreibung = Cut(S(o["beschreibung"]), 4000), PortionWort = S(o["portionWort"]) is { Length: > 0 } pw ? Cut(pw, 40) : "Portionen", Kategorie = Cut(S(o["kategorie"]), 100), Kueche = Cut(S(o["kueche"]), 100),
            Stichwoerter = Cut(S(o["stichwoerter"]), 400), Vorbereitung = Math.Clamp(I(o["vorbereitung"]), 0, 100_000), Garzeit = Math.Clamp(I(o["garzeit"]), 0, 100_000), Quelle = Cut(S(o["quelle"]), 1000), Notizen = Cut(S(o["notizen"]), 4000),
        };
    }

    /// <summary>Rezepte des Dokuments. Kennungen sind danach eindeutig und stabil: leere Kennung → „r-&lt;Stelle&gt;“, doppelte → „&lt;id&gt;-2“, „-3“ … (Gegenprüfung T-0787:
    /// sonst warfen Einkaufsliste, Wochendruck und KiCheck beim Zeichnen ArgumentException). Der Wochenplan zeigt auf das erste Rezept mit der Kennung.</summary>
    public static List<Rezept> Rezepte(JsonObject doc)
    {
        var list = new List<Rezept>(); var seen = new HashSet<string>(StringComparer.Ordinal);
        var i = 0;
        foreach (var n in (doc["rezepte"] as JsonArray ?? []).Take(MaxRezepte))
        {
            var raw = (n as JsonObject)?["id"] is JsonValue jv && jv.TryGetValue<string>(out var sid) ? sid.Trim() : "";
            var id = raw.Length > 0 ? Cut(raw, 60) : "r-" + i.ToString(CultureInfo.InvariantCulture);
            var basis = id; for (var k = 2; !seen.Add(id); k++) id = basis + "-" + k.ToString(CultureInfo.InvariantCulture);
            list.Add(RezeptVon(n) with { Id = id }); i++;
        }
        return list;
    }
    public static JsonArray RezepteJson(IEnumerable<Rezept> list) => new([.. list.Take(MaxRezepte).Select(r => (JsonNode)RezeptJson(r))]);

    public static DateTime PlanStart(JsonObject doc) => DateTime.TryParseExact(S(doc["plan"]?["start"]), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? Montag(d) : Montag(DateTime.Today);

    public static List<PlanEintrag> Plan(JsonObject doc)
    {
        var ids = Rezepte(doc).Select(r => r.Id).ToHashSet();
        return (doc["plan"]?["eintraege"] as JsonArray ?? []).OfType<JsonObject>().Take(MaxPlan)
            .Select(o => new PlanEintrag(S(o["id"]) is { Length: > 0 } id ? id : NewId("p"), Math.Clamp(I(o["tag"]), 0, 6), Mahlzeiten.Contains(S(o["mahlzeit"])) ? S(o["mahlzeit"]) : "Mittag", S(o["rezept"]), Math.Clamp(I(o["portionen"], 2), 1, MaxPortionen)))
            .Where(p => ids.Contains(p.Rezept)).ToList();
    }

    public static JsonObject PlanJson(DateTime start, IEnumerable<PlanEintrag> list) => new()
    {
        ["start"] = Montag(start).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ["eintraege"] = new JsonArray([.. list.Take(MaxPlan).Select(p => (JsonNode)new JsonObject { ["id"] = p.Id, ["tag"] = p.Tag, ["mahlzeit"] = p.Mahlzeit, ["rezept"] = p.Rezept, ["portionen"] = p.Portionen })]),
    };

    public static HashSet<string> Abgehakt(JsonObject doc) => (doc["liste"]?["abgehakt"] as JsonArray ?? []).Select(S).Where(s => s.Length > 0).ToHashSet();
    public static List<string> Extra(JsonObject doc) => (doc["liste"]?["extra"] as JsonArray ?? []).Select(S).Where(s => s.Trim().Length > 0).Take(200).ToList();

    // ------------------------------------------------------------ Einkaufsliste

    /// <summary>Einkaufsliste aus dem Wochenplan: gleiche Zutaten werden zusammengefasst (Gramm mit Gramm, Milliliter mit Milliliter, Stück mit Stück; gemischt über die Dichte in Gramm),
    /// nach Abteilung im Laden sortiert. Zutaten ohne Menge erscheinen einmal ohne Menge.</summary>
    public static List<EinkaufPosten> Einkaufsliste(JsonObject doc)
    {
        var rezepte = Rezepte(doc).ToDictionary(r => r.Id);
        var teile = new List<(Zutat Z, double F, string Rezept)>();
        foreach (var p in Plan(doc)) if (rezepte.TryGetValue(p.Rezept, out var r)) foreach (var z in r.Zutaten.Where(z => !z.Gruppe && z.Name.Length > 0)) teile.Add((z, p.Portionen / (double)Math.Max(1, r.Portionen), r.Name));
        return Zusammenfassen(teile);
    }

    public static List<EinkaufPosten> Zusammenfassen(IEnumerable<(Zutat Z, double F, string Rezept)> teile)
    {
        var groups = teile.GroupBy(t => LmVon(t.Z)?.Id ?? "n:" + Regex.Replace(t.Z.Name.ToLowerInvariant(), @"[^\p{L}\p{N}]+", " ").Trim());
        var outList = new List<EinkaufPosten>();
        foreach (var g in groups)
        {
            var lm = LmVon(g.First().Z); var name = lm?.Name ?? g.First().Z.Name; var abt = lm?.Abteilung ?? "Sonstiges";
            var fuer = g.Select(t => t.Rezept).Distinct().ToList();
            // nach Art der Einheit sammeln
            var byArt = new Dictionary<string, (double Sum, string Einheit)>();
            var ohne = false;
            foreach (var (z, f, _) in g)
            {
                if (z.Menge is not { } m0) { ohne = true; continue; }
                var m = (z.Bis is { } b ? (m0 + b) / 2 : m0) * f;
                var e = EinheitVon(z.Einheit);
                var (art, wert, einheit) = e switch
                {
                    null => ("stueck", m, ""),
                    { Art: "masse" } => ("masse", m * e.Faktor, "g"),
                    { Art: "volumen" } => ("volumen", m * e.Faktor, "ml"),
                    { Art: "stueck" } => ("stueck:" + e.Id, m, e.Id),
                    _ => ("anders:" + e.Id, m, e.Id),
                };
                byArt[art] = (byArt.GetValueOrDefault(art).Sum + wert, einheit);
            }
            // Masse und Volumen derselben Zutat über die Dichte zusammenlegen
            if (lm is { Dichte: > 0 } && byArt.ContainsKey("masse") && byArt.TryGetValue("volumen", out var vol)) { byArt["masse"] = (byArt["masse"].Sum + vol.Sum * lm.Dichte, "g"); byArt.Remove("volumen"); }
            // Stück ohne Einheit und „Stück“ sind dasselbe
            if (byArt.TryGetValue("stueck:stueck", out var st)) { byArt["stueck"] = (byArt.GetValueOrDefault("stueck").Sum + st.Sum, ""); byArt.Remove("stueck:stueck"); }
            // Stück und Gramm derselben Zutat (Gegenprüfung T-0787): mit bekanntem Stückgewicht in Gramm zusammenlegen; ohne bleiben es getrennte, beschriftete Zeilen
            if (lm is { Stueck: > 0 } && byArt.ContainsKey("masse") && byArt.TryGetValue("stueck", out var stk)) { byArt["masse"] = (byArt["masse"].Sum + stk.Sum * lm.Stueck, "g"); byArt.Remove("stueck"); }
            var i = 0;
            foreach (var (art, (sum, einheit)) in byArt.OrderBy(kv => kv.Key))
            {
                var z = Glaette(new Zutat(sum, null, einheit, name));
                var kurz = EinheitVon(z.Einheit)?.Kurz ?? "";
                outList.Add(new(g.Key + "|" + art, name, Math.Round(z.Menge!.Value, 3), kurz, abt, fuer, lm?.Id ?? ""));
                i++;
            }
            if (i == 0 && ohne) outList.Add(new(g.Key + "|ohne", name, null, "", abt, fuer, lm?.Id ?? ""));
        }
        return outList.OrderBy(p => AbteilungRang(p.Abteilung)).ThenBy(p => p.Name, StringComparer.Create(De, true)).ToList();
    }

    /// <summary>Reihenfolge wie beim Gang durch den Laden.</summary>
    public static readonly string[] Abteilungen = ["Obst & Gemüse", "Brot & Backzutaten", "Milch, Käse & Eier", "Kühlregal", "Fleisch & Fisch", "Getreide, Nudeln & Reis", "Vorrat", "Nüsse & Samen", "Süßes & Backzutaten", "Öle & Fette", "Gewürze & Soßen", "Getränke", "Sonstiges"];
    static int AbteilungRang(string a) { var i = Array.IndexOf(Abteilungen, a); return i < 0 ? Abteilungen.Length : i; }

    public static string EinkaufText(JsonObject doc)
    {
        var sb = new StringBuilder();
        var start = PlanStart(doc);
        sb.Append("Einkaufsliste für die Woche ab ").Append(start.ToString("dddd, d. MMMM yyyy", De)).Append("\n");
        var done = Abgehakt(doc);
        foreach (var g in Einkaufsliste(doc).GroupBy(p => p.Abteilung))
        {
            sb.Append('\n').Append(g.Key).Append('\n');
            foreach (var p in g) sb.Append(done.Contains(p.Key) ? "[x] " : "[ ] ").Append(p.Text).Append('\n');
        }
        var extra = Extra(doc);
        if (extra.Count > 0) { sb.Append("\nAußerdem\n"); foreach (var e in extra) sb.Append("[ ] ").Append(e).Append('\n'); }
        return sb.ToString();
    }

    static string CsvZelle(string s)
    {
        if (s.Length > 0 && "=+-@\t\r".Contains(s[0])) s = "'" + s;
        return s.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }

    public static string EinkaufCsv(JsonObject doc)
    {
        var sb = new StringBuilder("Abteilung;Menge;Einheit;Zutat;Für;Erledigt\n");
        var done = Abgehakt(doc);
        foreach (var p in Einkaufsliste(doc))
            sb.Append(string.Join(';', CsvZelle(p.Abteilung), p.Menge is { } m ? Zahl(m, p.Einheit) : "", CsvZelle(p.Einheit), CsvZelle(p.Name), CsvZelle(string.Join(", ", p.Fuer)), done.Contains(p.Key) ? "ja" : "nein")).Append('\n');
        foreach (var e in Extra(doc)) sb.Append(string.Join(';', "Außerdem", "", "", CsvZelle(e), "", "nein")).Append('\n');
        return sb.ToString();
    }

    // ------------------------------------------------------------ Keine Gesundheitsversprechen

    /// <summary>Heil- und Gesundheitsversprechen als ganze Wörter bzw. Wendungen (Wortgrenzen an Buchstaben: „verbeugt“ und „hilft beim Frischhalten“ sind kein Treffer).</summary>
    static readonly string[] Versprechen =
    [
        @"heil(?:t|en|end|ende|ender|endes|wirkung|wirkungen|mittel|kraft|kräfte|kräftig|kräftige)", @"gegen\s+krebs", @"krebs(?:vorbeugend|hemmend|schützend)\w*",
        @"beug(?:t|en)\s+(?:\p{L}+\s+){0,4}vor", @"vorbeugend\w*", @"senk(?:t|en)\s+(?:den\s+|das\s+|die\s+)?(?:blutdruck|blutzucker\w*|cholesterin\w*|zuckerspiegel)",
        @"(?:blutzucker|blutdruck|cholesterin)senkend\w*", @"entzündungshemmend\w*", @"entgift(?:et|en|end|ende|ung)", @"detox\w*",
        @"stärk(?:t|en)\s+(?:das\s+|die\s+)?(?:immunsystem|abwehr\w*|abwehrkräfte)", @"immunbooster", @"fatburner", @"fettverbrennung\w*", @"schlank\s+über\s+nacht",
        @"garantiert\s+abnehmen", @"abnehmen\s+garantiert", @"macht\s+schlank", @"wundermittel", @"medizinisch\w*", @"therapeutisch\w*", @"gegen\s+diabetes",
        @"hilft\s+gegen", @"hilft\s+bei", @"lindert", @"lindern", @"schützt\s+vor\s+(?:krankheit\w*|krebs|herzinfarkt|erkältung\w*|infekt\w*)",
    ];
    static readonly Regex[] VersprechenRx = [.. Versprechen.Select(v => new Regex(@"(?<!\p{L})" + v + @"(?!\p{L})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromSeconds(2)))];

    /// <summary>Gesundheits- oder Heilversprechen in einem Text (Trendo gibt keine; die KI darf keine schreiben). Liefert die gefundenen Wendungen.</summary>
    public static List<string> Gesundheitsversprechen(string text)
    {
        var t = text ?? ""; if (t.Length > 200_000) t = t[..200_000];
        return VersprechenRx.Select(r => r.Match(t)).Where(m => m.Success).Select(m => m.Value.Trim()).ToList();
    }

    /// <summary>Aller Text eines Rezepts, in dem ein Versprechen stehen kann – auch Zutaten und deren Hinweise („1 TL Kurkuma, heilt Entzündungen“).</summary>
    public static string AllerText(Rezept r) => string.Join(" \n ", [r.Name, r.Beschreibung, r.Notizen, r.Stichwoerter, r.Kategorie, r.Kueche, .. r.Schritte, .. r.Zutaten.Select(z => z.Name + " " + z.Notiz)]);

    /// <summary>Prüfung einer KI-Änderung (vorher, nachher): keine Gesundheitsversprechen, keine doppelten Kennungen, Wochenplan und Einkaufsliste nur durch die Person.</summary>
    public static string? KiCheck(JsonObject vorher, JsonObject nachher)
    {
        if ((vorher["plan"]?.ToJsonString() ?? "") != (nachher["plan"]?.ToJsonString() ?? "") || (vorher["liste"]?.ToJsonString() ?? "") != (nachher["liste"]?.ToJsonString() ?? ""))
            return "Wochenplan und Einkaufsliste ändert nur die Person selbst – die KI schreibt nur Rezepte.";
        var roh = (nachher["rezepte"] as JsonArray ?? []).OfType<JsonObject>().Select(o => S(o["id"])).Where(id => id.Length > 0).ToList();
        if (roh.Count != roh.Distinct().Count()) return "Die KI hat zwei Rezepte mit derselben Kennung geschrieben – Trendo nimmt das nicht an.";
        var alt = Rezepte(vorher).ToDictionary(r => r.Id, r => RezeptJson(r).ToJsonString());
        foreach (var r in Rezepte(nachher))
        {
            if (alt.TryGetValue(r.Id, out var a) && a == RezeptJson(r).ToJsonString()) continue;
            if (Gesundheitsversprechen(AllerText(r)) is { Count: > 0 } hits) return $"Die KI hat ein Gesundheitsversprechen geschrieben („{hits[0]}“) – Trendo nimmt das nicht an. Nährwerte sind nur Richtwerte.";
        }
        return null;
    }

    /// <summary>Hinweise zum Rezept (für „Prüfen“ und den Druck): leere Felder, Zutaten ohne Nährwerte, Gesundheitsversprechen im eigenen Text.</summary>
    public static List<string> Pruefe(Rezept r)
    {
        var f = new List<string>();
        if (r.Name.Trim().Length == 0) f.Add("Das Rezept hat noch keinen Namen.");
        if (r.Zutaten.Count(z => !z.Gruppe) == 0) f.Add("Es fehlen die Zutaten.");
        if (r.Schritte.Count == 0) f.Add("Es fehlen die Arbeitsschritte.");
        var n = Berechne(r);
        if (n.Fehlt.Count > 0) f.Add("Ohne Nährwerte (nicht zugeordnet oder ohne Gewicht): " + string.Join(", ", n.Fehlt) + ".");
        if (Gesundheitsversprechen(AllerText(r)) is { Count: > 0 } v) f.Add($"Bitte ohne Heil- oder Gesundheitsversprechen formulieren („{v[0]}“).");
        return f;
    }

    // ------------------------------------------------------------ Freitext → Rezept

    /// <summary>Portionen aus einer Zahl im Text – zu groß oder nicht lesbar: Hinweis statt Absturz.</summary>
    static int Portionen(string zahl, int bisher, List<string>? hinweise)
    {
        if (int.TryParse(zahl, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n is >= 1 and <= MaxPortionen) return n;
        hinweise?.Add($"„{(zahl.Length > 12 ? zahl[..12] + "…" : zahl)} Portionen“ ist keine gültige Angabe (1 bis {MaxPortionen}) – es bleibt bei {bisher}.");
        return bisher;
    }

    /// <summary>Rezept aus eingefügtem Text: erste Zeile = Name, Zeilen mit Menge/Einheit (oder unter „Zutaten“) = Zutaten, der Rest = Schritte. Für Text aus einer Webseite oder Mail.
    /// <paramref name="hinweise"/> sammelt, was nicht eindeutig war (Portionen, Mengen).</summary>
    public static Rezept AusText(string text, List<string>? hinweise = null)
    {
        var lines = (text ?? "").Replace("\r", "").Split('\n').Select(l => l.Trim()).ToList();
        while (lines.Count > 0 && lines[0].Length == 0) lines.RemoveAt(0);
        var name = lines.Count > 0 ? lines[0].TrimStart('#').Trim() : "Neues Rezept"; if (lines.Count > 0) lines.RemoveAt(0);
        var zutaten = new List<Zutat>(); var schritte = new List<string>(); var portionen = 4; var mode = "";
        var absatz = new StringBuilder();
        void Absatz() { if (absatz.Length > 0) { schritte.Add(absatz.ToString().Trim()); absatz.Clear(); } }
        foreach (var l0 in lines)
        {
            var l = l0.Length > MaxZeile * 10 ? l0[..(MaxZeile * 10)] : l0;
            var low = l.ToLowerInvariant().TrimEnd(':');
            if (low is "zutaten" or "zutaten für" || low.StartsWith("zutaten für") || low.StartsWith("zutaten (")) { mode = "z"; if (Regex.Match(low, @"(\d+)\s*(portion|person|stück)") is { Success: true } pm) portionen = Portionen(pm.Groups[1].Value, portionen, hinweise); continue; }
            if (low is "zubereitung" or "anleitung" or "so geht's" or "so gehts" or "schritte" or "arbeitsschritte") { mode = "s"; continue; }
            if (Regex.Match(low, @"^(?:für\s+)?(\d+)\s*(portionen|personen|portion|person)\b") is { Success: true } pp) { portionen = Portionen(pp.Groups[1].Value, portionen, hinweise); continue; }
            if (l.Length == 0) { Absatz(); continue; }
            var istZutat = mode == "z" || mode == "" && Regex.IsMatch(l, @"^\s*[-•*]?\s*" + ZahlMuster + @"\s*\p{L}");
            if (istZutat && mode != "s") { var (z, h) = Lies(l); zutaten.Add(z); if (h is not null) hinweise?.Add(h); continue; }
            mode = "s";
            var step = Regex.Replace(l, @"^\s*(?:\d+[.)]|schritt\s*\d+:?)\s*", "", RegexOptions.IgnoreCase);
            if (Regex.IsMatch(l, @"^\s*(?:\d+[.)]|schritt\s*\d+)", RegexOptions.IgnoreCase)) { Absatz(); absatz.Append(step); Absatz(); }
            else absatz.Append(absatz.Length > 0 ? " " : "").Append(step);
        }
        Absatz();
        return new(NewId("r"), Cut(name, 200), Math.Clamp(portionen, 1, MaxPortionen), zutaten.Where(z => z.Name.Length > 0).Take(MaxZutaten).ToList(), schritte.Take(MaxSchritte).Select(s => Cut(s, 4000)).ToList());
    }

    // ------------------------------------------------------------ schema.org/Recipe (JSON-LD)

    /// <summary>ISO-8601-Dauer „PT1H30M“ ↔ Minuten.</summary>
    public static int? Minuten(string? iso)
    {
        if (iso is not { Length: > 0 }) return null;
        var m = Regex.Match(iso.Trim(), @"^P(?:(?<d>\d+)D)?(?:T(?:(?<h>\d+(?:\.\d+)?)H)?(?:(?<m>\d+(?:\.\d+)?)M)?(?:(?<s>\d+)S)?)?$", RegexOptions.IgnoreCase);
        if (!m.Success) return int.TryParse(iso, out var plain) ? Math.Clamp(plain, 0, 100_000) : null;
        double v(string g) => m.Groups[g].Success ? double.Parse(m.Groups[g].Value, CultureInfo.InvariantCulture) : 0;
        return (int)Math.Clamp(Math.Round(v("d") * 1440 + v("h") * 60 + v("m") + v("s") / 60), 0, 100_000);
    }
    public static string Iso(int minuten) => minuten <= 0 ? "PT0M" : "PT" + (minuten >= 60 ? $"{minuten / 60}H" : "") + (minuten % 60 > 0 || minuten < 60 ? $"{minuten % 60}M" : "");

    static string G(double v, int dec = 1) => Math.Round(v, dec).ToString("0.#", CultureInfo.InvariantCulture);

    /// <summary>Ein Rezept als schema.org-Objekt „Recipe“ (Nährwerte je Portion, als Richtwert gekennzeichnet).</summary>
    public static JsonObject SchemaRecipe(Rezept r)
    {
        var o = new JsonObject
        {
            ["@type"] = "Recipe", ["name"] = r.Name.Trim().Length > 0 ? r.Name.Trim() : "Rezept", ["inLanguage"] = "de",
            ["recipeYield"] = new JsonArray(r.Portionen.ToString(CultureInfo.InvariantCulture), $"{r.Portionen} {r.PortionWort}"),
            ["recipeIngredient"] = new JsonArray([.. r.Zutaten.Where(z => !z.Gruppe).Select(z => (JsonNode)Zeile(z))]),
        };
        if (r.Beschreibung.Trim().Length > 0) o["description"] = r.Beschreibung.Trim();
        if (r.Vorbereitung > 0) o["prepTime"] = Iso(r.Vorbereitung);
        if (r.Garzeit > 0) o["cookTime"] = Iso(r.Garzeit);
        if (r.Vorbereitung + r.Garzeit > 0) o["totalTime"] = Iso(r.Vorbereitung + r.Garzeit);
        if (r.Kategorie.Trim().Length > 0) o["recipeCategory"] = r.Kategorie.Trim();
        if (r.Kueche.Trim().Length > 0) o["recipeCuisine"] = r.Kueche.Trim();
        if (r.Stichwoerter.Trim().Length > 0) o["keywords"] = r.Stichwoerter.Trim();
        if (Uri.TryCreate(r.Quelle.Trim(), UriKind.Absolute, out var u) && u.Scheme is "http" or "https") o["url"] = u.ToString();
        o["recipeInstructions"] = new JsonArray([.. r.Schritte.Select((s, i) => (JsonNode)new JsonObject { ["@type"] = "HowToStep", ["position"] = i + 1, ["text"] = s })]);
        var n = JePortion(r);
        if (n.Erfasst > 0)
            o["nutrition"] = new JsonObject
            {
                ["@type"] = "NutritionInformation", ["servingSize"] = $"1 Portion (von {r.Portionen})",
                ["calories"] = $"{Math.Round(n.Kcal)} kcal", ["proteinContent"] = $"{G(n.Eiweiss)} g", ["fatContent"] = $"{G(n.Fett)} g", ["saturatedFatContent"] = $"{G(n.GesFett)} g",
                ["carbohydrateContent"] = $"{G(n.Kh)} g", ["sugarContent"] = $"{G(n.Zucker)} g", ["fiberContent"] = $"{G(n.Ballast)} g", ["sodiumContent"] = $"{Math.Round(n.Salz / 2.5 * 1000)} mg",
                ["description"] = "Richtwert je Portion, berechnet aus " + NaehrwertTabelle.Quelle + (n.Vollstaendig ? "" : " – ohne: " + string.Join(", ", n.Fehlt)),
            };
        return o;
    }

    /// <summary>JSON-LD-Datei: ein Rezept als einzelnes Objekt, mehrere als „@graph“.</summary>
    public static string JsonLd(IReadOnlyList<Rezept> list)
    {
        JsonObject root;
        if (list.Count == 1) { root = new JsonObject { ["@context"] = "https://schema.org" }; foreach (var kv in SchemaRecipe(list[0])) root[kv.Key] = kv.Value?.DeepClone(); }
        else root = new JsonObject { ["@context"] = "https://schema.org", ["@graph"] = new JsonArray([.. list.Select(r => (JsonNode)SchemaRecipe(r))]) };
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }

    static string Esc(string s) => System.Net.WebUtility.HtmlEncode(s);

    /// <summary>Rezeptseite (eine HTML-Datei, ohne Netz) mit eingebettetem JSON-LD – so lesen Rezept-Apps und Suchmaschinen das Rezept. „#pruefen“ in der Adresse liest das
    /// eingebettete JSON-LD im Browser und schreibt das Ergebnis in data-Attribute (Selbsttest).</summary>
    public static string Html(IReadOnlyList<Rezept> list)
    {
        var json = JsonLd(list).Replace("</", "<\\/").Replace("<!--", "<\\u0021--");
        var sb = new StringBuilder();
        sb.Append("<!doctype html>\n<html lang=\"de\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">\n<title>")
          .Append(Esc(list.Count == 1 ? list[0].Name : "Rezepte")).Append("</title>\n<script type=\"application/ld+json\">\n").Append(json).Append("\n</script>\n<style>")
          .Append("body{font-family:Georgia,serif;max-width:820px;margin:2em auto;padding:0 16px;color:#222;background:#fffdf8}h1{color:#7a5a00;margin-bottom:.2em}.meta{color:#666}")
          .Append("table{border-collapse:collapse}td{padding:2px 10px 2px 0;vertical-align:top}.nw td{border-bottom:1px solid #eee}.hinweis{color:#666;font-size:.9em}article{page-break-after:always;margin-bottom:3em}")
          .Append("</style></head><body>\n");
        foreach (var r in list)
        {
            sb.Append("<article class=\"rezept\">\n<h1>").Append(Esc(r.Name)).Append("</h1>\n");
            if (r.Beschreibung.Length > 0) sb.Append("<p>").Append(Esc(r.Beschreibung)).Append("</p>\n");
            sb.Append("<p class=\"meta\">").Append(Esc($"{r.Portionen} {r.PortionWort}" + (r.Vorbereitung > 0 ? $" · Vorbereitung {r.Vorbereitung} min" : "") + (r.Garzeit > 0 ? $" · Garzeit {r.Garzeit} min" : ""))).Append("</p>\n<h2>Zutaten</h2>\n<ul>\n");
            foreach (var z in r.Zutaten) sb.Append(z.Gruppe ? "</ul><h3>" + Esc(z.Name) + "</h3><ul>\n" : "<li>" + Esc(Zeile(z)) + "</li>\n");
            sb.Append("</ul>\n<h2>Zubereitung</h2>\n<ol>\n");
            foreach (var s in r.Schritte) sb.Append("<li>").Append(Esc(s)).Append("</li>\n");
            sb.Append("</ol>\n");
            var n = JePortion(r);
            if (n.Erfasst > 0)
            {
                sb.Append("<h2>Nährwerte je Portion (Richtwerte)</h2>\n<table class=\"nw\">");
                foreach (var (k, v) in NaehrwertZeilen(n)) sb.Append("<tr><td>").Append(Esc(k)).Append("</td><td>").Append(Esc(v)).Append("</td></tr>");
                sb.Append("</table>\n<p class=\"hinweis\">").Append(Esc(NaehrwertTabelle.Hinweis + (n.Vollstaendig ? "" : " Nicht erfasst: " + string.Join(", ", n.Fehlt) + ".") + " Quelle: " + NaehrwertTabelle.Quelle + " (" + NaehrwertTabelle.Lizenz + ").")).Append("</p>\n");
            }
            sb.Append("</article>\n");
        }
        sb.Append("<script>if(location.hash==='#pruefen'){var b=document.body;try{var s=document.querySelector('script[type=\"application/ld+json\"]');var j=JSON.parse(s.textContent);")
          .Append("var a=j['@graph']||[j];var ok=a.filter(function(x){return x['@type']==='Recipe'&&x.name&&x.recipeIngredient&&x.recipeIngredient.length&&x.recipeInstructions;});")
          .Append("b.setAttribute('data-ld-ok',ok.length===a.length?'ja':'nein');b.setAttribute('data-ld-rezepte',String(ok.length));b.setAttribute('data-ld-zutaten',String(ok.reduce(function(n,x){return n+x.recipeIngredient.length;},0)));")
          .Append("b.setAttribute('data-ld-kcal',ok.map(function(x){return x.nutrition?x.nutrition.calories:'-';}).join('|'));}catch(e){b.setAttribute('data-ld-ok','fehler:'+e.message);}}</script>\n</body></html>\n");
        return sb.ToString();
    }

    /// <summary>Alle Rezepte aus einem JSON-LD-Text oder einer HTML-Seite mit &lt;script type="application/ld+json"&gt; (auch „@graph“, Listen, HowToSection).</summary>
    public static (List<Rezept> Rezepte, List<string> Hinweise) Import(string text)
    {
        var found = new List<Rezept>(); var notes = new List<string>();
        var blocks = new List<string>();
        var t = (text ?? "").TrimStart('﻿').Trim();
        if (t.StartsWith('{') || t.StartsWith('[')) blocks.Add(t);
        else ScriptBloecke(t, blocks, notes);
        if (blocks.Count == 0) { notes.Add("Kein schema.org-Rezept (JSON-LD) gefunden."); return (found, notes); }
        foreach (var b in blocks)
        {
            JsonNode? node;
            try { node = JsonNode.Parse(b, documentOptions: new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip, MaxDepth = 64 }); }
            catch (JsonException e) { notes.Add("Ein JSON-LD-Block ist kein gültiges JSON: " + e.Message.Split('.')[0] + "."); continue; }
            foreach (var o in Walk(node, 0)) if (IsRecipe(o)) { found.Add(VonSchema(o)); if (found.Count >= MaxRezepte) break; }
            if (found.Count >= MaxRezepte) { notes.Add($"Höchstens {MaxRezepte} Rezepte je Datei – der Rest wurde nicht gelesen."); break; } // Grenze über alle Blöcke
        }
        if (found.Count == 0 && notes.Count == 0) notes.Add("Die Datei enthält schema.org-Daten, aber kein „Recipe“.");
        return (found, notes);
    }

    /// <summary>JSON-LD-Blöcke einer HTML-Seite in EINEM Durchlauf mit IndexOf (linear – auch bei offenen &lt;script&gt;-Marken ohne Ende, Gegenprüfung T-0787).
    /// Der Inhalt eines script-Elements ist kein HTML und wird unverändert gelesen. Höchstens 200 Blöcke.</summary>
    static void ScriptBloecke(string t, List<string> blocks, List<string> notes)
    {
        var pos = 0;
        while (pos < t.Length && blocks.Count < 200)
        {
            var i = t.IndexOf("<script", pos, StringComparison.OrdinalIgnoreCase); if (i < 0) return;
            var j = t.IndexOf('>', i); if (j < 0) { notes.Add("Die Seite endet mitten in einer <script>-Marke."); return; }
            var k = t.IndexOf("</script", j, StringComparison.OrdinalIgnoreCase);
            var ld = t.AsSpan(i, j - i).Contains("application/ld+json", StringComparison.OrdinalIgnoreCase);
            if (k < 0) { if (ld) notes.Add("Ein JSON-LD-Block der Seite ist nicht geschlossen (</script> fehlt) – nicht gelesen."); return; }
            if (ld) blocks.Add(t[(j + 1)..k].Trim());
            pos = k + 8;
        }
    }

    static IEnumerable<JsonObject> Walk(JsonNode? n, int depth)
    {
        if (depth > 12 || n is null) yield break;
        if (n is JsonArray a) { foreach (var x in a) foreach (var y in Walk(x, depth + 1)) yield return y; yield break; }
        if (n is not JsonObject o) yield break;
        yield return o;
        if (o["@graph"] is { } g) foreach (var y in Walk(g, depth + 1)) yield return y;
        if (o["mainEntity"] is { } me) foreach (var y in Walk(me, depth + 1)) yield return y;
    }

    static bool IsRecipe(JsonObject o) => o["@type"] switch
    {
        JsonValue v => S(v).EndsWith("Recipe", StringComparison.Ordinal),
        JsonArray a => a.Any(x => S(x).EndsWith("Recipe", StringComparison.Ordinal)),
        _ => false,
    };

    static IEnumerable<string> Texte(JsonNode? n) => n switch
    {
        null => [],
        JsonValue v => [S(v)],
        JsonArray a => a.SelectMany(Texte),
        JsonObject o => o["name"] is not null && o["@type"] is null ? [S(o["name"])] : o["text"] is not null ? [S(o["text"])] : o["name"] is not null ? [S(o["name"])] : [],
        _ => [],
    };

    /// <summary>Text ohne HTML-Marken – linear (eine offene „&lt;“ ohne „&gt;“ bleibt stehen), höchstens 20 000 Zeichen.</summary>
    static string Plain(string s)
    {
        s ??= ""; if (s.Length > 20_000) s = s[..20_000];
        var sb = new StringBuilder(s.Length);
        for (var i = 0; i < s.Length;)
        {
            if (s[i] != '<') { sb.Append(s[i++]); continue; }
            var close = s.IndexOf('>', i + 1);
            if (close < 0) { sb.Append(s, i, s.Length - i); break; }
            sb.Append(' '); i = close + 1;
        }
        return Regex.Replace(System.Net.WebUtility.HtmlDecode(sb.ToString()), @"\s+", " ").Trim();
    }

    /// <summary>schema.org „recipeInstructions“: Text, Liste von Texten, HowToStep, HowToSection (mit Zwischenüberschrift im Schritt).</summary>
    static List<string> Schritte(JsonNode? n)
    {
        var list = new List<string>();
        void Add(JsonNode? x, string prefix)
        {
            switch (x)
            {
                case null: return;
                case JsonValue v:
                    foreach (var line in Regex.Split(S(v).Replace("\r", ""), @"\n|<br\s*/?>|</p>|</li>", RegexOptions.IgnoreCase)) if (Plain(line) is { Length: > 0 } p) list.Add(prefix + p);
                    return;
                case JsonArray a: foreach (var y in a) Add(y, prefix); return;
                case JsonObject o:
                    var type = S(o["@type"]);
                    if (type == "HowToSection" || o["itemListElement"] is not null) { var nm = Plain(S(o["name"])); Add(o["itemListElement"], nm.Length > 0 ? nm + ": " : prefix); return; }
                    var text = Plain(S(o["text"]) is { Length: > 0 } tx ? tx : S(o["name"]));
                    if (text.Length > 0) list.Add(prefix + text);
                    return;
            }
        }
        Add(n, "");
        return list.Take(MaxSchritte).ToList();
    }

    static int Yield(JsonNode? n)
    {
        foreach (var s in Texte(n))
            if (Regex.Match(s, @"\d+") is { Success: true } m && int.TryParse(m.Value, out var v) && v > 0) return Math.Clamp(v, 1, MaxPortionen);
        return 4;
    }

    static string Liste(JsonNode? n) => string.Join(", ", Texte(n).Select(Plain).Where(s => s.Length > 0).Distinct());

    static Rezept VonSchema(JsonObject o)
    {
        var ing = Texte(o["recipeIngredient"] ?? o["ingredients"]).Select(Plain).Where(s => s.Length > 0).Take(MaxZutaten).Select(LiesZutat).ToList();
        var yieldWord = Texte(o["recipeYield"]).Select(s => Regex.Match(s, @"\d+\s+(\p{L}+)")).FirstOrDefault(m => m.Success)?.Groups[1].Value;
        return new Rezept(NewId("r"), Cut(Plain(S(o["name"])), 200), Yield(o["recipeYield"]), ing, Schritte(o["recipeInstructions"]))
        {
            Beschreibung = Cut(Plain(S(o["description"])), 4000),
            PortionWort = yieldWord is { Length: > 1 } w ? Cut(char.ToUpper(w[0]) + w[1..], 40) : "Portionen",
            Vorbereitung = Math.Clamp(Minuten(S(o["prepTime"])) ?? 0, 0, 100_000), // Grenze schon beim Einlesen (negativ oder riesig: 0 bzw. 100 000)
            Garzeit = Math.Clamp(Minuten(S(o["cookTime"])) ?? Math.Max(0, (Minuten(S(o["totalTime"])) ?? 0) - (Minuten(S(o["prepTime"])) ?? 0)), 0, 100_000),
            Kategorie = Cut(Liste(o["recipeCategory"]), 100), Kueche = Cut(Liste(o["recipeCuisine"]), 100), Stichwoerter = Cut(Liste(o["keywords"]), 400),
            Quelle = Cut(S(o["url"]) is { Length: > 0 } url ? url : S(o["@id"]), 1000),
        };
    }

    // ------------------------------------------------------------ Druck

    public static IEnumerable<(string Name, string Wert)> NaehrwertZeilen(Naehrwerte n)
    {
        string g(double v) => Math.Round(v, 1).ToString("0.#", De) + " g";
        yield return ("Energie", $"{Math.Round(n.Kcal).ToString("0", De)} kcal / {Math.Round(n.KJ).ToString("0", De)} kJ");
        yield return ("Fett", g(n.Fett)); yield return ("davon gesättigte Fettsäuren", g(n.GesFett));
        yield return ("Kohlenhydrate", g(n.Kh)); yield return ("davon Zucker", g(n.Zucker));
        yield return ("Ballaststoffe", g(n.Ballast)); yield return ("Eiweiß", g(n.Eiweiss)); yield return ("Salz", Math.Round(n.Salz, 2).ToString("0.##", De) + " g");
    }

    static void RezeptBloecke(TextDoc doc, Rezept r)
    {
        doc.Blocks.Add(new Para(r.Name.Trim().Length > 0 ? r.Name.Trim() : "Rezept", ParaStyle.Heading1));
        if (r.Beschreibung.Trim().Length > 0) doc.Blocks.Add(new Para(r.Beschreibung.Trim()) { LineSpacing = 1.15 });
        var info = $"{r.Portionen} {r.PortionWort}" + (r.Vorbereitung > 0 ? $" · Vorbereitung {r.Vorbereitung} min" : "") + (r.Garzeit > 0 ? $" · Garzeit {r.Garzeit} min" : "") + (r.Kategorie.Length > 0 ? " · " + r.Kategorie : "");
        var ip = new Para(info); ip.Spans.Add(new Span(0, info.Length, Fmt.Italic)); doc.Blocks.Add(ip);
        doc.Blocks.Add(new Para("Zutaten", ParaStyle.Heading2));
        var t = new Table();
        foreach (var z in r.Zutaten)
        {
            if (z.Gruppe) { t.Rows.Add([z.Name + ":", ""]); t.Cells[(t.Rows.Count - 1, 0)] = new CellFmt { ColSpan = 2, Spans = [new Span(0, z.Name.Length + 1, Fmt.Bold)] }; continue; }
            t.Rows.Add([MengeText(z), z.Name + (z.Notiz.Length > 0 ? ", " + z.Notiz : "")]);
        }
        if (t.Rows.Count > 0) { t.SetWidths([1, 3]); doc.Blocks.Add(t); }
        doc.Blocks.Add(new Para("Zubereitung", ParaStyle.Heading2));
        foreach (var s in r.Schritte) doc.Blocks.Add(new Para(s) { List = ListKind.Number, LineSpacing = 1.15 });
        var n = JePortion(r);
        if (n.Erfasst > 0)
        {
            doc.Blocks.Add(new Para("Nährwerte je Portion (Richtwerte)", ParaStyle.Heading2));
            var nt = new Table(); foreach (var (k, v) in NaehrwertZeilen(n)) nt.Rows.Add([k, v]);
            nt.SetWidths([3, 2]); doc.Blocks.Add(nt);
            var hint = NaehrwertTabelle.Hinweis + (n.Vollstaendig ? "" : " Nicht erfasst: " + string.Join(", ", n.Fehlt) + ".") + $" Quelle: {NaehrwertTabelle.Quelle} ({NaehrwertTabelle.Lizenz}).";
            var hp = new Para(hint) { SpaceBeforePt = 6 }; hp.Spans.Add(new Span(0, hint.Length, Fmt.Italic, 8.5)); doc.Blocks.Add(hp);
        }
        if (r.Notizen.Trim().Length > 0) { doc.Blocks.Add(new Para("Notizen", ParaStyle.Heading2)); doc.Blocks.Add(new Para(r.Notizen.Trim())); }
        if (r.Quelle.Trim().Length > 0) { var q = "Quelle: " + r.Quelle.Trim(); var qp = new Para(q); qp.Spans.Add(new Span(0, q.Length, Fmt.Italic, 9)); doc.Blocks.Add(qp); }
    }

    /// <summary>Druckfassung: ein Rezept (Zutaten, Schritte, Nährwerte je Portion als Richtwert) – für PDF, DOCX und ODT.</summary>
    public static TextDoc Druck(Rezept r)
    {
        var doc = new TextDoc { Title = r.Name, Page = new PageSetup { PageNumbers = false, Top = 2, Bottom = 2, Left = 2.2, Right = 2.2, Design = "standard" } };
        RezeptBloecke(doc, r);
        return doc;
    }

    /// <summary>Kochbuch: alle Rezepte mit Inhaltsverzeichnis, jedes auf eigener Seite.</summary>
    public static TextDoc Kochbuch(IReadOnlyList<Rezept> list, string titel)
    {
        if (list.Count == 0) throw new InvalidOperationException("Das Rezeptbuch ist noch leer.");
        var doc = new TextDoc { Title = titel, Page = new PageSetup { PageNumbers = true, Top = 2, Bottom = 2, Left = 2.2, Right = 2.2, Design = "standard" } };
        doc.Blocks.Add(new Para(titel, ParaStyle.Title) { Align = Align.Center, SpaceBeforePt = 120 });
        doc.Blocks.Add(new Para($"{list.Count} Rezepte", ParaStyle.Subtitle) { Align = Align.Center });
        doc.Blocks.Add(new PageBreak());
        doc.Blocks.Add(new TocBlock { Title = "Inhalt" });
        foreach (var r in list) { doc.Blocks.Add(new PageBreak()); RezeptBloecke(doc, r); }
        return doc;
    }

    /// <summary>Wochenplan mit Einkaufsliste zum Ausdrucken.</summary>
    public static TextDoc WochenDruck(JsonObject d)
    {
        var start = PlanStart(d); var rezepte = Rezepte(d).ToDictionary(r => r.Id); var plan = Plan(d);
        if (plan.Count == 0) throw new InvalidOperationException("Der Wochenplan ist noch leer – erst Rezepte auf Tage legen.");
        var doc = new TextDoc { Title = "Wochenplan", Page = new PageSetup { PageNumbers = true, Top = 2, Bottom = 2, Left = 2, Right = 2, Design = "standard" } };
        doc.Blocks.Add(new Para($"Wochenplan ab {start.ToString("dddd, d. MMMM yyyy", De)}", ParaStyle.Heading1));
        var t = new Table(); t.Rows.Add(["Tag", "Mahlzeit", "Rezept", "Portionen"]);
        foreach (var p in plan.OrderBy(p => p.Tag).ThenBy(p => Array.IndexOf(Mahlzeiten, p.Mahlzeit)))
            t.Rows.Add([$"{Tage[p.Tag]}, {start.AddDays(p.Tag).ToString("d. M.", De)}", p.Mahlzeit, rezepte[p.Rezept].Name, p.Portionen.ToString(De)]);
        t.SetWidths([2, 1.3, 3.5, 1]); doc.Blocks.Add(t);
        doc.Blocks.Add(new Para("Einkaufsliste", ParaStyle.Heading1) { SpaceBeforePt = 14 });
        var done = Abgehakt(d);
        foreach (var g in Einkaufsliste(d).GroupBy(p => p.Abteilung))
        {
            doc.Blocks.Add(new Para(g.Key, ParaStyle.Heading3));
            foreach (var p in g) doc.Blocks.Add(new Para((done.Contains(p.Key) ? "☑ " : "☐ ") + p.Text));
        }
        var extra = Extra(d);
        if (extra.Count > 0) { doc.Blocks.Add(new Para("Außerdem", ParaStyle.Heading3)); foreach (var e in extra) doc.Blocks.Add(new Para("☐ " + e)); }
        return doc;
    }
}
