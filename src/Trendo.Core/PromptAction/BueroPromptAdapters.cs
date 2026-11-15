using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Trendo.Core.Buero;

namespace Trendo.Core.PromptAction;

/// <summary>STUDIO-CV-01 Bewerbung &amp; Lebenslauf: Lebenslauf aus Stichpunkten prüfen (Kontakt, Datumsformat, Lücken, Länge, GER-Stufen) und als ATS-Textfassung ablegen.
/// Versendet nichts – beworben wird im Studio.</summary>
public sealed partial class LebenslaufAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-CV-01";
    public string ActionId => "lebenslauf.pruefen";
    public string Label => "Lebenslauf prüfen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "BueroTests.Lebenslauf_Adapter";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["name"];
    public IReadOnlyList<string> OutputKinds => ["txt"];

    [GeneratedRegex(@"^(\S+)\s*[-–]\s*(\S*)\s+(.+?)(?:\s+(?:bei|an der|am|an)\s+(.+))?$", RegexOptions.IgnoreCase)]
    private static partial Regex StationRx();
    [GeneratedRegex(@"^(.+?)\s+(A1|A2|B1|B2|C1|C2|Muttersprache)$", RegexOptions.IgnoreCase)]
    private static partial Regex LangRx();

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "name") ? [] : [new("name", "name", "Wie heißt du? Schreib z. B. „Name: Erika Muster“ und darunter „Stationen: 04.2021-heute Sachbearbeiterin bei Beispiel GmbH“.", true)];

    /// <summary>„04.2021-heute Sachbearbeiterin bei Beispiel GmbH; 08.2019-12.2020 Bürokauffrau bei Muster KG“.</summary>
    public static List<(string Von, string Bis, string Titel, string Firma)> Rows(string text)
    {
        var res = new List<(string, string, string, string)>();
        foreach (var part in (text ?? "").Split([';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var m = StationRx().Match(part);
            if (!m.Success) throw new FormatException($"„{part}“ bitte als „MM.JJJJ-MM.JJJJ Bezeichnung bei Firma“ (Ende leer oder „heute“ = läuft).");
            res.Add((m.Groups[1].Value, CvDate.IsOpen(m.Groups[2].Value) ? "" : m.Groups[2].Value, m.Groups[3].Value.Trim(), m.Groups[4].Value.Trim()));
        }
        return res;
    }

    public static List<CvSprache> Languages(string text) =>
        [.. (text ?? "").Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(p => LangRx().Match(p) is { Success: true } m
            ? new CvSprache { Name = m.Groups[1].Value.Trim(), Stufe = m.Groups[2].Value.Equals("muttersprache", StringComparison.OrdinalIgnoreCase) ? "Muttersprache" : m.Groups[2].Value.ToUpperInvariant() }
            : throw new FormatException($"Sprache „{p}“ bitte mit Stufe, z. B. „Englisch B2“ (A1 bis C2 oder Muttersprache)."))];

    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        var f = new List<ValidationFinding>();
        foreach (var k in new[] { "stationen", "ausbildung" })
            if (FieldKit.Has(parameters, k)) try { Rows(FieldKit.Text(parameters, k)); } catch (FormatException e) { f.Add(new(k, e.Message, true)); }
        if (FieldKit.Has(parameters, "sprachen")) try { Languages(FieldKit.Text(parameters, "sprachen")); } catch (FormatException e) { f.Add(new("sprachen", e.Message, true)); }
        return f;
    }

    public static Lebenslauf Build(IReadOnlyDictionary<string, string> p)
    {
        var cv = new Lebenslauf();
        cv.Person.Name = FieldKit.Text(p, "name"); cv.Person.Email = FieldKit.Text(p, "email", "e-mail", "mail"); cv.Person.Telefon = FieldKit.Text(p, "telefon", "tel");
        cv.Person.Ort = FieldKit.Text(p, "ort", "wohnort"); cv.Person.Titel = FieldKit.Text(p, "beruf", "titel"); cv.Person.Profil = FieldKit.Text(p, "profil");
        var i = 0;
        foreach (var r in Rows(FieldKit.Text(p, "stationen"))) cv.Stationen.Add(new() { Id = "s" + ++i, Von = r.Von, Bis = r.Bis, Titel = r.Titel, Firma = r.Firma });
        foreach (var r in Rows(FieldKit.Text(p, "ausbildung"))) cv.Ausbildung.Add(new() { Id = "a" + ++i, Von = r.Von, Bis = r.Bis, Abschluss = r.Titel, Einrichtung = r.Firma });
        cv.Kenntnisse = [.. FieldKit.Text(p, "kenntnisse").Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
        cv.Sprachen = Languages(FieldKit.Text(p, "sprachen"));
        return cv;
    }

    public AdapterOutcome Produce(AdapterCall call)
    {
        var cv = Build(call.Parameters);
        var today = DateTime.TryParseExact(FieldKit.Text(call.Parameters, "stichtag"), ["yyyy-MM-dd", "dd.MM.yyyy"], CultureInfo.GetCultureInfo("de-DE"), DateTimeStyles.None, out var d) ? d : DateTime.Today;
        var check = CvCheck.Run(cv, today);
        var sb = new StringBuilder("Prüfung des Lebenslaufs\n");
        if (check.Count == 0) sb.Append("✓ Alles in Ordnung.\n");
        foreach (var x in check) sb.Append(x.Fehler ? "✗ " : "• ").Append(x.Feld).Append(": ").Append(x.Text).Append('\n');
        var ad = FieldKit.Text(call.Parameters, "anzeige", "stelle");
        if (ad.Length > 0)
        {
            var (found, missing) = CvText.Abgleich(ad, cv);
            sb.Append("\nAbgleich mit der Anzeige\nSchon drin: ").Append(found.Count == 0 ? "–" : string.Join(", ", found)).Append("\nFehlt noch (nur eintragen, wenn es stimmt): ").Append(missing.Count == 0 ? "–" : string.Join(", ", missing)).Append('\n');
        }
        sb.Append("\nATS-Fassung (für Bewerbungsportale)\n\n").Append(CvText.Ats(cv));
        var errors = check.Count(c => c.Fehler);
        var qa = new QaResult(ActionId, true, $"{errors} Pflichtpunkte, {check.Count - errors} Hinweise, {CvCheck.Gaps(cv, today).Count} Lücken.", null, cv.Person.Name, "CvCheck");
        return FieldKit.Save(call, FieldKit.Slot("lebenslauf", call.Version, "lebenslauf.txt"), sb.ToString(), $"{cv.Stationen.Count} Stationen, {errors} Pflichtpunkte", qa);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Bewerbungen schickst du selbst ab – Trendo versendet nichts.");
}

/// <summary>STUDIO-CARDS-01 Karten &amp; Einladungen: Karte aus einer Vorlage anlegen, Platzhalter füllen, Gästeliste mit Zu- und Absagen zählen. Druckt und versendet nichts.</summary>
public sealed class KarteAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-CARDS-01";
    public string ActionId => "karte.anlegen";
    public string Label => "Karte anlegen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "BueroTests.Karte_Adapter";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["anlass"];
    public IReadOnlyList<string> OutputKinds => ["txt"];

    public static string? Anlass(string s)
    {
        var t = (s ?? "").Trim().ToLowerInvariant();
        return KartenVorlagen.Alle.FirstOrDefault(v => t.Contains(v.Id) || t.Contains(v.Name.ToLowerInvariant())).Id
            ?? (t.Contains("party") || t.Contains("feier") ? "einladung" : t.Contains("dank") ? "danke" : t.Contains("advent") || t.Contains("xmas") ? "weihnachten" : null);
    }

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "anlass") ? [] : [new("anlass", "anlass", "Wofür ist die Karte? Schreib z. B. „Anlass: Hochzeit“ (Geburtstag, Hochzeit, Einladung, Danke, Weihnachten).", true)];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        var f = new List<ValidationFinding>();
        if (FieldKit.Has(parameters, "anlass") && Anlass(FieldKit.Text(parameters, "anlass")) is null) f.Add(new("anlass", "Anlass: Geburtstag, Hochzeit, Einladung, Danke oder Weihnachten.", true));
        if (FieldKit.Has(parameters, "format") && !KartenKit.Formate.Any(x => x.Id == FieldKit.Text(parameters, "format").ToLowerInvariant())) f.Add(new("format", "Format: a6, a5, postkarte oder quadrat.", true));
        return f;
    }

    public AdapterOutcome Produce(AdapterCall call)
    {
        var p = call.Parameters;
        var fmt = FieldKit.Has(p, "format") ? FieldKit.Text(p, "format").ToLowerInvariant() : "a6";
        var k = KartenVorlagen.Neu(Anlass(FieldKit.Text(p, "anlass"))!, fmt);
        if (FieldKit.Has(p, "datum")) k.Datum = FieldKit.Text(p, "datum");
        if (FieldKit.Has(p, "ort")) k.Ort = FieldKit.Text(p, "ort");
        if (FieldKit.Has(p, "antwort")) k.AntwortBis = FieldKit.Text(p, "antwort");
        k.Gaeste = Gaeste.ParseLines(FieldKit.Text(p, "gaeste", "gäste").Replace(';', '\n'));
        var f = KartenKit.Format(k.Format); var sb = new StringBuilder();
        sb.Append("Karte: ").Append(KartenVorlagen.Alle.First(v => v.Id == k.Anlass).Name).Append(" – ").Append(f.Name).Append(" (").Append(f.Hint).Append(")\n\n");
        foreach (var (id, name) in KartenKit.Seiten(k))
        {
            var texts = k.Seite(id).Texte.Select(t => KartenKit.Fill(t.Text, k, null).Replace('\n', ' ')).ToList();
            if (texts.Count > 0) sb.Append(name).Append(": ").Append(string.Join(" | ", texts)).Append('\n');
        }
        var st = Gaeste.Zaehlen(k.Gaeste);
        sb.Append(CultureInfo.InvariantCulture, $"\nGäste: {st.Eingeladen} eingeladen, {st.Zusagen} Zusagen ({st.PersonenZugesagt} Personen), {st.Absagen} Absagen, {st.Offen} offen\n");
        var check = KartenKit.Pruefen(k);
        foreach (var x in check) sb.Append(x.Fehler ? "✗ " : "• ").Append(x.Feld).Append(": ").Append(x.Text).Append('\n');
        if (k.Gaeste.Count > 0) sb.Append("\nGästeliste (CSV):\n").Append(Gaeste.Csv(k.Gaeste));
        var qa = new QaResult(ActionId, check.All(c => !c.Fehler), $"{KartenKit.Seiten(k).Count} Seiten, {st.Eingeladen} Gäste, {check.Count(c => c.Fehler)} Pflichtpunkte.", null, k.Format, "Karten");
        return FieldKit.Save(call, FieldKit.Slot("karte", call.Version, "karte.txt"), sb.ToString(), $"{f.Name}, {st.Eingeladen} Gäste", qa);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Gedruckt und verschickt wird von dir – Trendo legt die Druck-PDF an, versendet aber nichts.");
}

/// <summary>STUDIO-CONTRACTS-01 Verträge-Ordner: spätestes Kündigungsdatum mit Ampel berechnen (Monatsende richtig). Kündigt nichts.</summary>
public sealed partial class VertragFristAdapter : IStudioActionAdapter
{
    public string StudioId => "STUDIO-CONTRACTS-01";
    public string ActionId => "vertrag.frist";
    public string Label => "Kündigungsfrist berechnen";
    public SideEffectClass Effect => SideEffectClass.None;
    public CapabilityLevel DeclaredLevel => CapabilityLevel.Partial;
    public string Evidence => "BueroTests.Vertrag_Adapter";
    public bool CanPerformSideEffect => false;
    public IReadOnlyList<string> RequiredFields => ["beginn"];
    public IReadOnlyList<string> OutputKinds => ["txt"];

    [GeneratedRegex(@"^(\d{1,3})\s*(tage?n?|wochen?|monate?n?)$", RegexOptions.IgnoreCase)]
    private static partial Regex FristRx();
    [GeneratedRegex(@"^(\d{1,3})")]
    private static partial Regex NumRx();

    public static (int Wert, string Einheit) Frist(string s)
    {
        var m = FristRx().Match((s ?? "").Trim());
        if (!m.Success) throw new FormatException($"Frist „{s}“ bitte z. B. als „3 Monate“, „4 Wochen“ oder „14 Tage“.");
        var u = m.Groups[2].Value.ToLowerInvariant();
        return (int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), u.StartsWith("tag") ? "tage" : u.StartsWith("woche") ? "wochen" : "monate");
    }
    static int Months(string s, string what) => NumRx().Match((s ?? "").Trim()) is { Success: true } m ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)
        : (s ?? "").Trim().ToLowerInvariant() is "unbefristet" or "keine" or "0" ? 0 : throw new FormatException($"{what} bitte in Monaten, z. B. „24“.");

    public ActionDraft? TryParse(string prompt, IReadOnlyDictionary<string, string> known) => new(ActionId, FieldKit.Read(prompt, known));
    public IReadOnlyList<ClarificationQuestion> Missing(IReadOnlyDictionary<string, string> parameters) =>
        FieldKit.Has(parameters, "beginn") ? [] : [new("beginn", "beginn", "Wann hat der Vertrag begonnen? Schreib z. B. „Beginn: 15.03.2025“, dazu „Laufzeit: 24“ und „Frist: 1 Monat“.", true)];
    public IReadOnlyList<ValidationFinding> Validate(IReadOnlyDictionary<string, string> parameters)
    {
        var f = new List<ValidationFinding>();
        if (FieldKit.Has(parameters, "beginn") && Vertraege.Datum(FieldKit.Text(parameters, "beginn")) is null) f.Add(new("beginn", "Beginn als Datum, z. B. 15.03.2025.", true));
        if (FieldKit.Has(parameters, "frist")) try { Frist(FieldKit.Text(parameters, "frist")); } catch (FormatException e) { f.Add(new("frist", e.Message, true)); }
        foreach (var k in new[] { "laufzeit", "verlaengerung" })
            if (FieldKit.Has(parameters, k)) try { Months(FieldKit.Text(parameters, k), k); } catch (FormatException e) { f.Add(new(k, e.Message, true)); }
        return f;
    }

    public static Vertrag Build(IReadOnlyDictionary<string, string> p)
    {
        var (w, e) = FieldKit.Has(p, "frist") ? Frist(FieldKit.Text(p, "frist")) : (1, "monate");
        return new Vertrag
        {
            Id = "v1", Name = FieldKit.Has(p, "name") ? FieldKit.Text(p, "name") : "Vertrag", Anbieter = FieldKit.Text(p, "anbieter"), Beginn = FieldKit.Text(p, "beginn"),
            LaufzeitMonate = FieldKit.Has(p, "laufzeit") ? Months(FieldKit.Text(p, "laufzeit"), "Laufzeit") : 24,
            VerlaengerungMonate = FieldKit.Has(p, "verlaengerung") ? Months(FieldKit.Text(p, "verlaengerung"), "Verlängerung") : 1,
            FristWert = w, FristEinheit = e, Kosten = decimal.TryParse(FieldKit.Text(p, "kosten").Replace("€", "").Trim().Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var k) ? k : 0m,
        };
    }

    public AdapterOutcome Produce(AdapterCall call)
    {
        var v = Build(call.Parameters);
        var today = Vertraege.Datum(FieldKit.Text(call.Parameters, "stichtag")) ?? DateTime.Today;
        var s = Vertraege.Stand(v, today);
        var sb = new StringBuilder();
        sb.Append(v.Name).Append(v.Anbieter.Length > 0 ? " bei " + v.Anbieter : "").Append('\n');
        sb.Append("Beginn: ").Append(Vertraege.Datum(v.Beginn) is { } b ? Vertraege.Text(b) : v.Beginn).Append(CultureInfo.InvariantCulture, $", Mindestlaufzeit {v.LaufzeitMonate} Monate, Verlängerung {v.VerlaengerungMonate} Monate, Frist {v.FristWert} {v.FristEinheit}\n");
        sb.Append(s.Text).Append('\n');
        sb.Append("Ampel: ").Append(s.Ampel switch { "rot" => "rot – jetzt handeln", "gelb" => "gelb – bald kümmern", "gruen" => "grün – noch Zeit", _ => "grau" }).Append('\n');
        if (v.Kosten > 0) sb.Append(CultureInfo.GetCultureInfo("de-DE"), $"Kosten: {Vertraege.ProMonat(v):0.00} € im Monat, {Vertraege.ProJahr(v):0.00} € im Jahr\n");
        var qa = new QaResult(ActionId, s.Ende is not null, s.Text, null, v.Beginn, "Vertraege");
        return FieldKit.Save(call, FieldKit.Slot("vertrag", call.Version, "vertrag.txt"), sb.ToString(), s.Text, qa);
    }
    public AdapterOutcome SideEffect(AdapterCall call) => FieldKit.Blocked("Gekündigt wird von dir – Trendo schreibt den Brief-Entwurf, versendet aber nichts.");
}
