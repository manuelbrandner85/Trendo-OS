using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Trendo.Core.Programs;

namespace Trendo.Core.Lernen;

/// <summary>T-0782 (STUDIO-TUTOR-01 „Nachhilfe mit KI“): Lernpfad je Fach und Klassenstufe, Übungen mit Prüfung, gestufte Hinweise,
/// sokratischer Modus (Lösungsweg erst nach einem eigenen Versuch), Beherrschungsstufe je Thema und Wiederholungsplan nach Abständen (FSRS aus SPEC-06).
/// Vorbilder nur als Arbeitsweise (Khan Academy/Khanmigo, Duolingo, ANTON, simpleclub) – alle Aufgaben, Texte und Erklärungen sind eigene.
/// Alles arbeitet auf dem Dokument der Programm-Hülle (<see cref="ProgramDocument"/>) – jede Änderung ist ein Rückgängig-Schritt.
/// Anforderungen und Prüfungen: docs/nachbau/nachhilfe/anforderungen.md.</summary>
public static partial class Nachhilfe
{
    public sealed record Fach(string Id, string Name);
    /// <param name="Generator">Erzeugt beliebig viele Aufgaben (Mathe); null = feste Aufgaben in <paramref name="Items"/>.</param>
    public sealed record Thema(string Id, string Fach, int VonKlasse, int BisKlasse, string Titel, string Einfuehrung, Func<Random, Item>? Generator = null, IReadOnlyList<Item>? Items = null);
    /// <summary>Eine Übung: Frage, erlaubte Antworten (erste = Musterlösung), Lösungsweg, Hinweise in Stufen.</summary>
    public sealed record Item(string Frage, IReadOnlyList<string> Antworten, string Erklaerung, IReadOnlyList<string> Hinweise, string Art = "text", string? Einheit = null);

    public static readonly IReadOnlyList<Fach> Faecher = [new("mathe", "Mathe"), new("deutsch", "Deutsch"), new("englisch", "Englisch")];
    public const int RundeLaenge = 5;
    /// <summary>Beherrschungsstufen wie bei Khan Academy (Arbeitsweise), eigene Namen.</summary>
    public static readonly string[] Stufen = ["neu", "versucht", "vertraut", "sicher", "gemeistert"];
    public static string StufeName(string s) => s switch { "versucht" => "Versucht", "vertraut" => "Vertraut", "sicher" => "Sicher", "gemeistert" => "Gemeistert", _ => "Neu" };

    // ───────────────────────── Lernpfad ─────────────────────────

    static Item Num(string frage, double wert, string weg, params string[] hinweise) => new(frage, [Fmt(wert)], weg, hinweise, "zahl");
    static Item NumE(string einheit, string frage, double wert, string weg, params string[] hinweise) => new(frage, [Fmt(wert)], weg, hinweise, "zahl", einheit);
    static string Fmt(double v) => Math.Abs(v - Math.Round(v)) < 1e-9 ? ((long)Math.Round(v)).ToString(CultureInfo.InvariantCulture) : v.ToString("0.###", CultureInfo.GetCultureInfo("de-DE"));
    static int Ggt(int a, int b) { a = Math.Abs(a); b = Math.Abs(b); while (b != 0) (a, b) = (b, a % b); return Math.Max(1, a); }

    public static readonly IReadOnlyList<Thema> Themen =
    [
        new("m-einmaleins", "mathe", 2, 4, "Kleines Einmaleins", "Malnehmen heißt: gleich große Gruppen zusammenzählen. 4 · 3 sind vier Dreiergruppen.",
            r => { int a = r.Next(2, 11), b = r.Next(2, 11); return Num($"Rechne: {a} · {b} = ?", a * b, $"{a} · {b} sind {a} Gruppen mit je {b}: {string.Join(" + ", Enumerable.Repeat(b, a))} = {a * b}.",
                $"Denk an {a} Gruppen mit je {b} Dingen.", $"Du kennst vielleicht {a} · {b - 1} = {a * (b - 1)}. Wie viel kommt noch dazu?"); }),
        new("m-addieren", "mathe", 3, 4, "Schriftlich addieren", "Zahlen stellengerecht untereinander schreiben, von rechts nach links addieren, Überträge merken.",
            r => { int a = r.Next(120, 900), b = r.Next(120, 900); return Num($"Rechne schriftlich: {a} + {b} = ?", a + b, $"Einer: {a % 10} + {b % 10}, Zehner und Hunderter genauso, Überträge mitnehmen – Ergebnis {a + b}.",
                "Fang bei den Einern an (ganz rechts).", $"Einer: {a % 10} + {b % 10} = {a % 10 + b % 10}. Ist das mehr als 9, schreibst du die Einerstelle hin und merkst dir 1 für die Zehner."); }),
        new("m-teilen", "mathe", 3, 5, "Teilen mit Rest", "Teilen heißt gerecht verteilen. Was nicht mehr ganz reicht, bleibt als Rest übrig. Schreibweise: 17 : 5 = 3 R 2.",
            r => { int b = r.Next(3, 10), q = r.Next(2, 10), rest = r.Next(0, b); var a = b * q + rest;
                return new Item($"Rechne: {a} : {b} = ? (Schreibe z. B. „3 R 2“, ohne Rest nur die Zahl)", [rest == 0 ? $"{q}" : $"{q} R {rest}"], $"{b} · {q} = {b * q}, bis {a} fehlen noch {rest}. Also {a} : {b} = {q}" + (rest > 0 ? $" Rest {rest}." : "."),
                    [$"Wie oft passt {b} in {a}? Zähl in {b}er-Schritten.", $"{b} · {q} = {b * q}. Was bleibt bis {a} übrig?"], "rest"); }),
        new("m-kuerzen", "mathe", 6, 7, "Brüche kürzen", "Kürzen heißt: Zähler und Nenner durch dieselbe Zahl teilen. Der Wert bleibt gleich, die Zahlen werden kleiner.",
            r => { int z = r.Next(1, 9), n = r.Next(z + 1, 13); var g = Ggt(z, n); z /= g; n /= g; var k = r.Next(2, 7);
                return new Item($"Kürze so weit wie möglich: {z * k}/{n * k} = ?", [$"{z}/{n}"], $"{z * k} und {n * k} sind beide durch {k} teilbar: {z * k}:{k} = {z}, {n * k}:{k} = {n}. Ergebnis {z}/{n}.",
                    ["Suche eine Zahl, durch die Zähler und Nenner beide teilbar sind.", $"Probier es mit {k}."], "bruch"); }),
        new("m-prozent", "mathe", 7, 8, "Prozent berechnen", "p % von G: G durch 100 teilen und mal p nehmen. 20 % von 50 = 50 : 100 · 20 = 10.",
            r => { int[] ps = [10, 20, 25, 30, 40, 50, 75]; var p = ps[r.Next(ps.Length)]; var g = r.Next(2, 30) * 20;
                return Num($"Wie viel sind {p} % von {g}?", g * p / 100.0, $"{g} : 100 = {Fmt(g / 100.0)}, mal {p} = {Fmt(g * p / 100.0)}.", "1 % ist der hundertste Teil.", $"Rechne erst 1 % von {g} aus: {g} : 100."); }),
        new("m-gleichung", "mathe", 7, 9, "Gleichungen lösen", "Eine Gleichung ist wie eine Waage: Was du auf einer Seite machst, machst du auch auf der anderen.",
            r => { int a = r.Next(2, 9), x = r.Next(-5, 13), b = r.Next(-9, 10); var c = a * x + b; var bs = b < 0 ? $"− {-b}" : $"+ {b}";
                return Num($"Löse nach x auf: {a}x {bs} = {c}", x, $"Erst {(b < 0 ? $"{-b} addieren" : $"{b} abziehen")}: {a}x = {c - b}. Dann durch {a} teilen: x = {x}.",
                    $"Bring zuerst die Zahl ohne x auf die andere Seite.", $"{a}x = {c - b}. Was musst du jetzt mit beiden Seiten machen?"); }),
        new("m-pythagoras", "mathe", 9, 10, "Satz des Pythagoras", "Im rechtwinkligen Dreieck gilt a² + b² = c². c ist die längste Seite, gegenüber dem rechten Winkel.",
            r => { int[][] t = [[3, 4, 5], [6, 8, 10], [5, 12, 13], [9, 12, 15], [8, 15, 17], [7, 24, 25]]; var s = t[r.Next(t.Length)];
                return NumE("cm", $"Rechtwinkliges Dreieck mit a = {s[0]} cm und b = {s[1]} cm. Wie lang ist c (in cm)?", s[2], $"{s[0]}² + {s[1]}² = {s[0] * s[0]} + {s[1] * s[1]} = {s[2] * s[2]}, die Wurzel daraus ist {s[2]}.",
                    "Quadriere a und b und zähle sie zusammen.", $"{s[0] * s[0]} + {s[1] * s[1]} = {s[2] * s[2]}. Welche Zahl mal sich selbst ergibt das?"); }),

        new("d-wortarten", "deutsch", 3, 5, "Wortarten erkennen", "Nomen benennen Dinge und Lebewesen (groß geschrieben), Verben sagen, was jemand tut, Adjektive beschreiben, wie etwas ist.", Items:
        [
            new("Welche Wortart ist „springen“? (Nomen, Verb oder Adjektiv)", ["Verb"], "„springen“ sagt, was jemand tut – also ein Verb.", ["Kannst du sagen „ich …“?", "Ich springe, du springst …"], "wahl"),
            new("Welche Wortart ist „Fahrrad“?", ["Nomen"], "Ein Fahrrad ist ein Ding, man kann „das Fahrrad“ sagen – ein Nomen.", ["Passt ein Begleiter wie „der, die, das“ davor?", "das Fahrrad"], "wahl"),
            new("Welche Wortart ist „leise“?", ["Adjektiv"], "„leise“ beschreibt, wie etwas ist – ein Adjektiv.", ["Kannst du sagen „die … Musik“?", "Kann man es steigern: leiser, am leisesten?"], "wahl"),
            new("Welche Wortart ist „Freundschaft“?", ["Nomen"], "„die Freundschaft“ – auch Gefühle und Gedanken können Nomen sein.", ["Passt „die“ davor?", "Wörter auf -schaft sind meist Nomen."], "wahl"),
            new("Welche Wortart ist „schreiben“?", ["Verb"], "Man kann sagen „ich schreibe“ – ein Verb.", ["Ist es eine Tätigkeit?", "ich schreibe, du schreibst"], "wahl"),
            new("Welche Wortart ist „bunt“?", ["Adjektiv"], "„bunt“ beschreibt eine Eigenschaft: ein bunter Ball.", ["Beschreibt das Wort, wie etwas aussieht?", "bunter, am buntesten"], "wahl"),
        ]),
        new("d-dassdas", "deutsch", 5, 7, "„das“ oder „dass“", "„das“ kann man durch „dieses“, „jenes“ oder „welches“ ersetzen. Geht das nicht, schreibt man „dass“.", Items:
        [
            new("Ich hoffe, ___ du morgen kommst. (das oder dass)", ["dass"], "„Ich hoffe, dieses du morgen kommst“ ergibt keinen Sinn – also „dass“.", ["Probiere, „dieses“ oder „welches“ einzusetzen.", "Ergibt der Satz dann noch Sinn?"], "wahl"),
            new("___ Buch liegt auf dem Tisch. (das oder dass)", ["das"], "„Dieses Buch liegt auf dem Tisch“ – passt, also „das“.", ["Setz „dieses“ ein.", "Passt es? Dann ist es der Begleiter „das“."], "wahl"),
            new("Das Haus, ___ dort steht, ist alt. (das oder dass)", ["das"], "„Das Haus, welches dort steht …“ – passt, also „das“.", ["Probiere „welches“.", "Bezieht sich das Wort auf „Haus“?"], "wahl"),
            new("Er sagt, ___ es regnet. (das oder dass)", ["dass"], "„Er sagt, welches es regnet“ passt nicht – also „dass“.", ["Setz „welches“ ein.", "Leitet das Wort einen Satz ein, der sagt, WAS er sagt?"], "wahl"),
            new("Weißt du, ___ heute Ferien sind? (das oder dass)", ["dass"], "„dieses/welches“ passt nicht – also „dass“.", ["Ersatzprobe mit „dieses“.", "Nach „weißt du“ folgt oft „dass“."], "wahl"),
        ]),
        new("d-plural", "deutsch", 2, 4, "Mehrzahl bilden", "Die Mehrzahl (Plural) zeigt, dass es mehrere sind: ein Hund – viele Hunde. Oft ändert sich auch der Selbstlaut: Baum – Bäume.", Items:
        [
            new("Wie heißt die Mehrzahl von „der Baum“?", ["die Bäume", "Bäume"], "Aus „au“ wird „äu“ und es kommt ein „e“ dazu: die Bäume.", ["Sag: ein Baum, viele …", "Hör genau auf den Selbstlaut."]),
            new("Wie heißt die Mehrzahl von „das Kind“?", ["die Kinder", "Kinder"], "das Kind – die Kinder (mit „-er“).", ["Ein Kind, zwei …", "Die Endung ist „-er“."]),
            new("Wie heißt die Mehrzahl von „die Maus“?", ["die Mäuse", "Mäuse"], "„au“ wird zu „äu“, dazu „-e“: die Mäuse.", ["Eine Maus, viele …", "Wie beim Baum ändert sich der Selbstlaut."]),
            new("Wie heißt die Mehrzahl von „das Auto“?", ["die Autos", "Autos"], "Viele Fremdwörter bekommen ein „-s“: die Autos.", ["Ein Auto, zwei …", "Die Endung ist ein einzelner Buchstabe."]),
        ]),

        new("e-verben", "englisch", 5, 7, "Unregelmäßige Verben (Simple Past)", "Regelmäßige Verben bekommen -ed (play – played). Unregelmäßige haben eine eigene Form, die man lernen muss: go – went.", Items:
        [
            new("Simple Past von „go“?", ["went"], "go – went – gone.", ["Es beginnt mit „w“.", "Es reimt sich auf „sent“."]),
            new("Simple Past von „see“?", ["saw"], "see – saw – seen.", ["Drei Buchstaben.", "Es beginnt mit „sa“."]),
            new("Simple Past von „eat“?", ["ate"], "eat – ate – eaten.", ["Drei Buchstaben, beginnt mit „a“.", "Es klingt wie die Zahl „eight“."]),
            new("Simple Past von „have“?", ["had"], "have – had – had.", ["Es beginnt wie „have“.", "Drei Buchstaben."]),
            new("Simple Past von „write“?", ["wrote"], "write – wrote – written.", ["Das „i“ wird zu einem „o“.", "w-r-o-…"]),
            new("Simple Past von „swim“?", ["swam"], "swim – swam – swum.", ["Nur ein Selbstlaut ändert sich.", "Aus „i“ wird „a“."]),
        ]),
        new("e-schule", "englisch", 5, 6, "Vokabeln: Schule", "Wörter rund um die Schule. Sprich sie laut mit – so merkst du sie dir besser.", Items:
        [
            new("Was heißt „Lehrerin“ auf Englisch?", ["teacher", "a teacher", "the teacher"], "Lehrerin oder Lehrer: teacher.", ["Es kommt von „teach“ (unterrichten).", "teach + er"]),
            new("Was heißt „Hausaufgaben“ auf Englisch?", ["homework"], "Hausaufgaben: homework (ohne Mehrzahl-s).", ["Zuhause = home.", "home + work"]),
            new("Was heißt „Pausenhof“ auf Englisch?", ["schoolyard", "playground", "school yard"], "Pausenhof: schoolyard (oft auch playground).", ["Es steckt „school“ darin.", "school + yard"]),
            new("Was heißt „Stundenplan“ auf Englisch?", ["timetable", "schedule"], "Stundenplan: timetable.", ["Zeit = time.", "time + table"]),
        ]),
    ];

    public static Thema? ThemaById(string? id) => Themen.FirstOrDefault(t => t.Id == id);
    /// <summary>Lernpfad: Themen eines Fachs, die zur Klassenstufe passen (eine Stufe darunter und darüber gilt mit), nach Klasse geordnet.</summary>
    public static IReadOnlyList<Thema> Pfad(string fach, int klasse) =>
        [.. Themen.Where(t => t.Fach == fach && t.VonKlasse <= klasse + 1 && t.BisKlasse >= klasse - 1).OrderBy(t => t.VonKlasse).ThenBy(t => t.Titel, StringComparer.Ordinal)];

    // ───────────────────────── Dokument ─────────────────────────

    public static JsonObject Neu(string fach = "mathe", int klasse = 5) => new()
    {
        ["fach"] = fach, ["klasse"] = klasse, ["sokratisch"] = true, ["thema"] = Pfad(fach, klasse).FirstOrDefault()?.Id ?? "", ["uebung"] = null,
        ["themen"] = new JsonObject(), ["eigene"] = new JsonArray(), ["verlauf"] = new JsonArray(), ["zaehler"] = 0,
    };

    public static string FachOf(ProgramDocument d) => d.Text("fach", "mathe");
    public static int KlasseOf(ProgramDocument d) => (int)Math.Clamp(d.Number("klasse", 5), 1, 13);
    public static Thema? AktuellesThema(ProgramDocument d) => ThemaById(d.Text("thema"));
    public static JsonObject? Uebung(ProgramDocument d) => d.Data["uebung"] as JsonObject;

    /// <summary>Eigene und von der KI erzeugte Übungen eines Themas.</summary>
    public static List<(string Id, Item Item, string Herkunft)> Eigene(ProgramDocument d, string thema) =>
        [.. d.List("eigene").OfType<JsonObject>().Where(o => o["thema"].AsText() == thema).Select(o => (o["id"].AsText() ?? "", ItemOf(o), o["herkunft"].AsText() ?? "eigen"))];

    static Item ItemOf(JsonObject o) => new(o["frage"].AsText() ?? "", [.. (o["antworten"] as JsonArray ?? []).Select(x => x.AsText() ?? "").Where(x => x.Length > 0)],
        o["erklaerung"].AsText() ?? "", [.. (o["hinweise"] as JsonArray ?? []).Select(x => x.AsText() ?? "").Where(x => x.Length > 0)], o["art"].AsText() ?? "text", o["einheit"].AsText() is { Length: > 0 } eh ? eh : null);

    static JsonObject Json(Item i, string thema, string quelle) => new()
    {
        ["thema"] = thema, ["frage"] = i.Frage, ["antworten"] = new JsonArray([.. i.Antworten.Select(a => (JsonNode)a)]), ["erklaerung"] = i.Erklaerung,
        ["hinweise"] = new JsonArray([.. i.Hinweise.Select(h => (JsonNode)h)]), ["art"] = i.Art, ["einheit"] = i.Einheit, ["quelle"] = quelle, ["uid"] = "e" + Guid.NewGuid().ToString("N")[..12],
        ["versuche"] = 0, ["hinweiseGezeigt"] = 0, ["geloest"] = false, ["loesungGezeigt"] = false, ["eingaben"] = new JsonArray(),
    };

    public static void FachWaehlen(ProgramDocument d, string fach, int klasse)
    {
        if (Faecher.All(f => f.Id != fach)) throw new ArgumentException("Dieses Fach gibt es hier nicht: " + fach);
        klasse = Math.Clamp(klasse, 1, 13);
        var first = Pfad(fach, klasse).FirstOrDefault() ?? throw new InvalidOperationException($"Für {Faecher.First(f => f.Id == fach).Name} in Klasse {klasse} gibt es noch keine Themen.");
        d.Edit($"Fach {Faecher.First(f => f.Id == fach).Name}, Klasse {klasse}", x => { x["fach"] = fach; x["klasse"] = klasse; x["thema"] = first.Id; x["uebung"] = null; });
    }

    public static void ThemaWaehlen(ProgramDocument d, string thema)
    {
        var t = ThemaById(thema) ?? throw new ArgumentException("Dieses Thema gibt es nicht.");
        d.Edit("Thema " + t.Titel, x => { x["thema"] = t.Id; x["fach"] = t.Fach; x["uebung"] = null; });
    }

    /// <summary>Nächste Übung im aktuellen Thema: zuerst offene eigene/KI-Übungen, sonst erzeugt (Mathe) oder aus der festen Liste (zuletzt falsch beantwortete zuerst).</summary>
    public static JsonObject NaechsteUebung(ProgramDocument d, Random? rng = null)
    {
        var t = AktuellesThema(d) ?? throw new InvalidOperationException("Wähle zuerst links ein Thema.");
        var zaehler = (int)d.Number("zaehler");
        rng ??= new Random(HashCode.Combine(t.Id, zaehler, DateTime.Today.DayOfYear));
        var geloestEigene = d.List("verlauf").OfType<JsonObject>().Where(v => v["richtig"].AsBool()).Select(v => v["frage"].AsText()).ToHashSet();
        Item item; string quelle;
        if (Eigene(d, t.Id).FirstOrDefault(e => !geloestEigene.Contains(e.Item.Frage)) is { Id.Length: > 0 } eigen) { item = eigen.Item; quelle = eigen.Herkunft; }
        else if (t.Generator is { } g) { item = g(rng); quelle = "erzeugt"; }
        else
        {
            var list = t.Items!; var verlauf = d.List("verlauf").OfType<JsonObject>().ToList();
            var letzte = Uebung(d)?["frage"].AsText();
            int Rang(Item i) { var v = verlauf.LastOrDefault(x => x["frage"].AsText() == i.Frage); return v is null ? 1 : v["richtig"].AsBool() ? 2 : 0; }
            item = list.Where(i => i.Frage != letzte).OrderBy(Rang).ThenBy(_ => rng.Next()).First();
            quelle = "pfad";
        }
        var u = Json(item, t.Id, quelle);
        d.Edit("Neue Übung", x => { x["uebung"] = u; x["zaehler"] = zaehler + 1; });
        return u;
    }

    /// <summary>Antwort normalisieren: klein, Leerzeichen, Anführungszeichen, Komma als Dezimalzeichen.</summary>
    public static string Norm(string s) => Spaces().Replace(s.Trim().Trim('.', '!', '„', '“', '"', '\'').ToLowerInvariant().Replace('’', '\''), " ");

    /// <summary>Antwort der lernenden Person prüfen. Zählt als Versuch (außer leere oder unlesbare Eingaben); bei „richtig“ geht das Ergebnis in den Fortschritt ein.
    /// T-0782 (M6): Versuche und „Lösung gesehen“ stehen zusätzlich außerhalb der Rückgängig-Liste (Dokument-Meta) – Strg+Z setzt sie nicht zurück.
    /// Wer den Lösungsweg schon gesehen hat, bekommt für eine richtige Antwort keinen Punkt.</summary>
    public static Pruefung Pruefen(ProgramDocument d, string eingabe, DateTime? heute = null)
    {
        var u = Uebung(d) ?? throw new InvalidOperationException("Es gibt gerade keine Übung – „Nächste Übung“ holt eine.");
        if (u["geloest"].AsBool() || u["loesungGezeigt"].AsBool()) throw new InvalidOperationException("Diese Übung ist schon abgeschlossen – „Nächste Übung“ holt eine neue.");
        var item = ItemOf(u);
        var p = Vergleiche(item, eingabe);
        if (!p.Zaehlt) return p;
        var meta = MetaFor(d, u, true);
        var gesehen = meta["gesehen"].AsBool();
        var echt = (int)ProgramDocument.ToNumber(meta["versuche"]) + 1;
        d.EditMeta(_ => meta["versuche"] = echt);
        var tag = (heute ?? DateTime.Today).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        d.Edit(p.Richtig ? "Richtig beantwortet" : "Antwort geprüft", x =>
        {
            var ue = (JsonObject)x["uebung"]!;
            ue["versuche"] = Math.Max(echt, (int)ProgramDocument.ToNumber(ue["versuche"]) + 1);
            ((JsonArray)ue["eingaben"]!).Add(eingabe.Trim().Length > 200 ? eingabe.Trim()[..200] : eingabe.Trim());
            if (p.Richtig)
            {
                ue["geloest"] = true;
                if (gesehen) ue["loesungGezeigt"] = true;
                Werten(x, ue["thema"].AsText()!, ue["frage"].AsText()!, eingabe, echt == 1 && !gesehen, tag, falsch: gesehen);
            }
        });
        if (p.Richtig && gesehen) return p with { Rueckmeldung = "Richtig – zählt aber nicht als gelöst, weil du den Lösungsweg schon gesehen hattest." };
        if (!p.Richtig && d.Flag("sokratisch")) return p with { Rueckmeldung = p.Rueckmeldung + " Schau dir einen Hinweis an oder versuch es nochmal – nach deinem Versuch kannst du dir auch den Lösungsweg zeigen lassen." };
        return p;
    }

    /// <summary>Nächsten Hinweis zeigen (ohne Versuch erlaubt). Null, wenn alle gezeigt sind.</summary>
    public static string? Hinweis(ProgramDocument d)
    {
        var u = Uebung(d) ?? throw new InvalidOperationException("Es gibt gerade keine Übung.");
        var hs = ItemOf(u).Hinweise; var n = (int)ProgramDocument.ToNumber(u["hinweiseGezeigt"]);
        if (n >= hs.Count) return null;
        d.Edit("Hinweis gezeigt", x => ((JsonObject)x["uebung"]!)["hinweiseGezeigt"] = n + 1);
        return hs[n];
    }

    /// <summary>Sokratischer Modus: Den Lösungsweg gibt es erst nach mindestens einem eigenen Versuch. Danach zählt die Übung als nicht gelöst – auch nach Strg+Z (M6).</summary>
    public static string LoesungZeigen(ProgramDocument d, DateTime? heute = null)
    {
        var u = Uebung(d) ?? throw new InvalidOperationException("Es gibt gerade keine Übung.");
        if (d.Flag("sokratisch") && VersucheEcht(d) == 0 && !u["geloest"].AsBool() && !LoesungGesehen(d))
            throw new InvalidOperationException("Versuch es zuerst selbst – schon ein erster Gedanke zählt. Danach zeigt Trendo dir den ganzen Lösungsweg. Ein Hinweis geht jederzeit.");
        var item = ItemOf(u);
        if (!u["geloest"].AsBool() && !u["loesungGezeigt"].AsBool())
        {
            var meta = MetaFor(d, u, true);
            d.EditMeta(_ => meta["gesehen"] = true);
            var tag = (heute ?? DateTime.Today).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            d.Edit("Lösungsweg gezeigt", x => { var ue = (JsonObject)x["uebung"]!; ue["loesungGezeigt"] = true; Werten(x, ue["thema"].AsText()!, ue["frage"].AsText()!, "", false, tag, falsch: true); });
        }
        return $"Lösung: {item.Antworten[0]}{(item.Einheit is { } e ? " " + e : "")}\n{item.Erklaerung}";
    }

    /// <summary>Ergebnis einer Übung in Verlauf und Thema eintragen; nach <see cref="RundeLaenge"/> Übungen wird die Runde gewertet (FSRS, Beherrschung).</summary>
    static void Werten(JsonObject x, string thema, string frage, string eingabe, bool ersterVersuch, string tag, bool falsch = false)
    {
        var richtig = !falsch;
        var verlauf = (JsonArray)(x["verlauf"] ??= new JsonArray());
        verlauf.Add(new JsonObject { ["datum"] = tag, ["thema"] = thema, ["frage"] = frage, ["eingabe"] = eingabe, ["richtig"] = richtig, ["ersterVersuch"] = ersterVersuch });
        while (verlauf.Count > 300) verlauf.RemoveAt(0);
        var themen = (JsonObject)(x["themen"] ??= new JsonObject());
        var st = (JsonObject)(themen[thema] ??= new JsonObject { ["richtig"] = 0, ["falsch"] = 0, ["runden"] = new JsonArray(), ["runde"] = new JsonObject { ["richtig"] = 0, ["gesamt"] = 0 } });
        st[richtig ? "richtig" : "falsch"] = (int)ProgramDocument.ToNumber(st[richtig ? "richtig" : "falsch"]) + 1;
        var runde = (JsonObject)(st["runde"] ??= new JsonObject { ["richtig"] = 0, ["gesamt"] = 0 });
        // Gelöst erst nach mehreren Versuchen zählt halb – wie „schwer“
        runde["richtig"] = ProgramDocument.ToNumber(runde["richtig"]) + (richtig ? ersterVersuch ? 1 : 0.5 : 0);
        runde["gesamt"] = (int)ProgramDocument.ToNumber(runde["gesamt"]) + 1;
        if ((int)ProgramDocument.ToNumber(runde["gesamt"]) >= RundeLaenge) RundeAbschliessen(st, tag);
    }

    /// <summary>Runde werten: Anteil → Bewertung → FSRS-Schritt (Stabilität, Schwierigkeit, nächster Termin); Beherrschungsstufe neu bestimmen.</summary>
    static void RundeAbschliessen(JsonObject st, string tag)
    {
        var runde = (JsonObject)st["runde"]!;
        var anteil = ProgramDocument.ToNumber(runde["richtig"]) / Math.Max(1, ProgramDocument.ToNumber(runde["gesamt"]));
        var g = anteil >= 0.95 ? Grade.Leicht : anteil >= 0.75 ? Grade.Gut : anteil >= 0.5 ? Grade.Schwer : Grade.Falsch;
        var heute = DateTime.ParseExact(tag, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        double? s = st["s"] is null ? null : ProgramDocument.ToNumber(st["s"]); double? dd = st["d"] is null ? null : ProgramDocument.ToNumber(st["d"]);
        var last = st["zuletzt"].AsText() is { Length: > 0 } l ? DateTime.ParseExact(l, "yyyy-MM-dd", CultureInfo.InvariantCulture) : heute;
        var (ns, nd, days) = Fsrs.Step(s, dd, (heute - last).TotalDays, g);
        st["s"] = Math.Round(ns, 3); st["d"] = Math.Round(nd, 3); st["zuletzt"] = tag; st["faellig"] = heute.AddDays(days).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var runden = (JsonArray)(st["runden"] ??= new JsonArray());
        runden.Add(new JsonObject { ["datum"] = tag, ["anteil"] = Math.Round(anteil, 2) });
        while (runden.Count > 30) runden.RemoveAt(0);
        st["stufe"] = Stufe(runden);
        st["runde"] = new JsonObject { ["richtig"] = 0, ["gesamt"] = 0 };
    }

    /// <summary>Beherrschung aus den Runden (steigt und fällt wieder): „gemeistert“ = zwei gute Runden an verschiedenen Tagen, die letzte gut;
    /// „sicher“ = letzte Runde ≥ 80 %; „vertraut“ ≥ 60 %; sonst „versucht“.</summary>
    public static string Stufe(JsonArray runden)
    {
        var r = runden.OfType<JsonObject>().Select(o => (Tag: o["datum"].AsText() ?? "", A: ProgramDocument.ToNumber(o["anteil"]))).ToList();
        if (r.Count == 0) return "neu";
        var letzte = r[^1].A;
        if (letzte >= 0.8 && r.Where(x => x.A >= 0.8).Select(x => x.Tag).Distinct().Count() >= 2) return "gemeistert";
        return letzte >= 0.8 ? "sicher" : letzte >= 0.6 ? "vertraut" : "versucht";
    }

    public sealed record ThemaStand(Thema Thema, string Stufe, int Richtig, int Falsch, DateTime? Faellig, int InRunde, double RundeRichtig);

    public static ThemaStand Stand(ProgramDocument d, Thema t)
    {
        var st = (d.Data["themen"] as JsonObject)?[t.Id] as JsonObject;
        if (st is null) return new(t, "neu", 0, 0, null, 0, 0);
        var f = st["faellig"].AsText() is { Length: > 0 } x ? DateTime.ParseExact(x, "yyyy-MM-dd", CultureInfo.InvariantCulture) : (DateTime?)null;
        var stufe = st["stufe"].AsText() ?? ((int)ProgramDocument.ToNumber(st["richtig"]) + (int)ProgramDocument.ToNumber(st["falsch"]) > 0 ? "versucht" : "neu");
        var runde = st["runde"] as JsonObject;
        return new(t, stufe, (int)ProgramDocument.ToNumber(st["richtig"]), (int)ProgramDocument.ToNumber(st["falsch"]), f, (int)ProgramDocument.ToNumber(runde?["gesamt"]), ProgramDocument.ToNumber(runde?["richtig"]));
    }

    /// <summary>Wiederholungsplan: Themen mit Termin, fällige zuerst (nach Datum), danach die kommenden.</summary>
    public static IReadOnlyList<ThemaStand> Wiederholungsplan(ProgramDocument d) =>
        [.. Themen.Select(t => Stand(d, t)).Where(s => s.Faellig is not null).OrderBy(s => s.Faellig)];

    /// <summary>Fortschritt eines Fachs in Prozent: Anteil der Themen des Lernpfads, die „sicher“ oder „gemeistert“ sind (wie bei Khan Academy gezählt).</summary>
    public static int Fortschritt(ProgramDocument d)
    {
        var p = Pfad(FachOf(d), KlasseOf(d)); if (p.Count == 0) return 0;
        return (int)Math.Round(100.0 * p.Count(t => Stand(d, t).Stufe is "sicher" or "gemeistert") / p.Count);
    }

    // ───────────────────────── Eigene und KI-Übungen ─────────────────────────

    [GeneratedRegex(@"\s+")] private static partial Regex Spaces();
    [GeneratedRegex(@"^(-?\d+)/(-?\d+)$")] private static partial Regex Fraction();
    [GeneratedRegex(@"(https?://|ftp://|www\.)\S+|\b[\w.-]+\.(com|de|org|net|io|at|ch|info|ly)\b(/\S*)?|[\w.+-]+@[\w-]+\.[\w.]+", RegexOptions.IgnoreCase)] private static partial Regex Links();

    /// <summary>Kinderprofil-tauglich: Links, Web-Adressen und E-Mail-Adressen werden aus KI-Texten und eigenen Übungen entfernt.</summary>
    public static string OhneLinks(string s) => Spaces().Replace(Links().Replace(s, "[Link entfernt]"), " ").Trim();
    public static bool HatLink(string s) => Links().IsMatch(s);

    [GeneratedRegex(@"\\[dt]?frac\s*\{([^{}]*)\}\s*\{([^{}]*)\}")] private static partial Regex LatexFrac();
    /// <summary>Echtlauf T-0782: lokale Modelle schreiben Formeln als LaTeX („\(\frac{2}{3}\)“) – für Kinder und für die Prüfung in einfache Schreibweise („2/3“).</summary>
    public static string OhneLatex(string s)
    {
        s = LatexFrac().Replace(s, "$1/$2");
        foreach (var (a, b) in new[] { (@"\(", ""), (@"\)", ""), (@"\[", ""), (@"\]", ""), (@"\cdot", "·"), (@"\times", "·"), (@"\div", ":"), (@"\,", " "), ("$", "") }) s = s.Replace(a, b);
        return s.Trim();
    }

    /// <summary>Eigene oder KI-Übung zum Thema anlegen (geprüft: Frage und Antwort nötig, keine Links, Länge begrenzt). <paramref name="herkunft"/>: „eigen“ oder „ki“.</summary>
    public static JsonObject UebungAnlegen(ProgramDocument d, string thema, string frage, string antwort, string erklaerung, IEnumerable<string> hinweise, string herkunft, string? label = null, bool aktivieren = true)
    {
        var t = ThemaById(thema) ?? throw new ArgumentException("Dieses Thema gibt es nicht.");
        string Clean(string s, int max) { s = OhneLinks(OhneLatex(s ?? "")); return s.Length > max ? s[..max] : s; }
        frage = Clean(frage, 400); erklaerung = Clean(erklaerung, 800);
        var antworten = (antwort ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(a => NormSoll(Clean(a, 120))).Where(a => a.Length > 0).Distinct().Take(5).ToList();
        if (frage.Length < 3) throw new ArgumentException("Die Übung braucht eine Frage.");
        if (antworten.Count == 0) throw new ArgumentException("Die Übung braucht eine richtige Antwort.");
        if (ArtVon(antworten) == "text" && VerraetLoesung(frage, antworten)) throw new ArgumentException("Die Frage verrät schon die Antwort – bitte anders fragen."); // 2. Gegenprüfung M6
        var hs = hinweise.Select(h => Clean(h, 300)).Where(h => h.Length > 0).Take(3).ToList();
        // Ein Hinweis darf die Lösung nicht einfach verraten (sokratisch)
        hs = [.. hs.Where(h => !VerraetLoesung(h, antworten, frage))];
        var art = ArtVon(antworten);
        var item = new Item(frage, antworten, erklaerung.Length > 0 ? erklaerung : "Die richtige Antwort ist: " + antworten[0], hs, art);
        var id = "u" + Guid.NewGuid().ToString("N")[..8];
        var o = Json(item, t.Id, herkunft); o["id"] = id; o["herkunft"] = herkunft;
        var saved = new JsonObject { ["id"] = id, ["thema"] = t.Id, ["frage"] = item.Frage, ["antworten"] = new JsonArray([.. antworten.Select(a => (JsonNode)a)]),
            ["erklaerung"] = item.Erklaerung, ["hinweise"] = new JsonArray([.. hs.Select(h => (JsonNode)h)]), ["art"] = art, ["herkunft"] = herkunft };
        d.Edit(label ?? (herkunft == "ki" ? "KI-Übung" : "Eigene Übung"), x =>
        {
            ((JsonArray)(x["eigene"] ??= new JsonArray())).Add(saved);
            if (aktivieren) { x["thema"] = t.Id; x["fach"] = t.Fach; x["uebung"] = o; }
        });
        return o;
    }

    // ───────────────────────── KI (nur lokal) ─────────────────────────

    /// <summary>Rolle der KI: erklären und Rückfragen stellen, nie die Lösung vorwegnehmen; keine Links, kein Weitersenden.</summary>
    public const string KiRolle = "Du bist eine geduldige Nachhilfe-Lehrkraft für Schulkinder. Du arbeitest sokratisch: Du erklärst das Prinzip, stellst eine kleine Rückfrage und gibst den nächsten Denkschritt – "
        + "aber du nennst NIE das Endergebnis der aktuellen Aufgabe. Antworte kurz (höchstens 5 Sätze), freundlich, in einfacher Sprache auf Deutsch. Keine Links, keine Webseiten, keine E-Mail-Adressen, keine persönlichen Fragen.";

    /// <summary>Frage an die KI zur aktuellen Übung. Vor dem ersten Versuch kennt die KI die Lösung nicht (sie steht nicht im Auftrag).</summary>
    public static string KiFrage(ProgramDocument d, string frage)
    {
        var u = Uebung(d); var t = AktuellesThema(d);
        var sb = new StringBuilder();
        sb.Append($"Fach: {Faecher.FirstOrDefault(f => f.Id == FachOf(d))?.Name}, Klasse {KlasseOf(d)}, Thema: {t?.Titel}.\n");
        if (t is not null) sb.Append($"Kurz zum Thema: {t.Einfuehrung}\n");
        if (u is not null)
        {
            sb.Append($"Aktuelle Aufgabe: {u["frage"].AsText()}\n");
            var ein = (u["eingaben"] as JsonArray ?? []).Select(x => x.AsText()).ToList();
            if (ein.Count > 0) sb.Append($"Bisherige Versuche des Kindes: {string.Join(" · ", ein)} (falsch).\n");
            else sb.Append("Das Kind hat noch keinen Versuch gemacht.\n");
        }
        sb.Append($"Frage des Kindes: {OhneLinks(frage)}");
        return sb.ToString();
    }

    /// <summary>KI-Antwort für die Anzeige: Links raus; solange die Übung offen ist, wird eine Antwort mit der Lösung nicht gezeigt.</summary>
    public static (string Text, bool Zurueckgehalten) KiAntwortFiltern(ProgramDocument d, string antwort)
    {
        var text = OhneLinks(OhneLatex(Regex.Replace(antwort, @"<think>[\s\S]*?</think>", "").Trim()));
        var u = Uebung(d);
        if (u is not null && !u["geloest"].AsBool() && !u["loesungGezeigt"].AsBool() && VerraetLoesung(text, ItemOf(u).Antworten, u["frage"].AsText()))
            return ("Die KI hätte dir hier die Lösung verraten – das zeigt Trendo erst nach deinem Versuch mit „Lösungsweg zeigen“. Nimm lieber einen Hinweis.", true);
        return (text.Length > 1500 ? text[..1500] + " …" : text, false);
    }

    // ───────────────────────── Austausch ─────────────────────────

    /// <summary>Eigene Übungen als CSV (Semikolon, UTF-8 mit BOM – öffnet in LibreOffice Calc und Excel): Thema;Frage;Antwort;Erklärung;Hinweis 1;Hinweis 2.</summary>
    public static string CsvExport(ProgramDocument d)
    {
        static string Q(string s) => s.Contains(';') || s.Contains('"') || s.Contains('\n') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        var sb = new StringBuilder("Thema;Frage;Antwort;Erklärung;Hinweis 1;Hinweis 2\r\n");
        foreach (var o in d.List("eigene").OfType<JsonObject>())
        {
            var i = ItemOf(o);
            sb.Append(string.Join(";", new[] { ThemaById(o["thema"].AsText())?.Titel ?? "", i.Frage, string.Join("|", i.Antworten), i.Erklaerung, i.Hinweise.ElementAtOrDefault(0) ?? "", i.Hinweise.ElementAtOrDefault(1) ?? "" }.Select(Q))).Append("\r\n");
        }
        return sb.ToString();
    }

    static List<string> CsvFields(string line, char sep)
    {
        var res = new List<string>(); var sb = new StringBuilder(); var q = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (q) { if (c == '"') { if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; } else q = false; } else sb.Append(c); }
            else if (c == '"') q = true; else if (c == sep) { res.Add(sb.ToString()); sb.Clear(); } else sb.Append(c);
        }
        res.Add(sb.ToString()); return res;
    }

    /// <summary>Übungen aus einer CSV/Tabelle (z. B. aus LibreOffice Calc) einlesen: Thema (Titel oder Kennung, leer = aktuelles);Frage;Antwort;Erklärung;Hinweise.
    /// Ein Schritt zum Rückgängigmachen. Gibt (übernommen, übersprungen mit Grund) zurück.</summary>
    public static (int Uebernommen, List<string> Uebersprungen) CsvImport(ProgramDocument d, string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n').Where(l => l.Trim().Length > 0).ToList();
        if (lines.Count == 0) throw new InvalidDataException("Die Datei ist leer.");
        var sep = lines[0].Count(c => c == ';') >= lines[0].Count(c => c == ',') ? ';' : lines[0].Contains('\t') ? '\t' : ',';
        if (lines[0].Contains('\t') && !lines[0].Contains(';')) sep = '\t';
        var start = Norm(CsvFields(lines[0], sep)[0]).TrimStart('﻿') is "thema" or "﻿thema" ? 1 : 0;
        var skipped = new List<string>(); var rows = new List<JsonObject>();
        for (var i = start; i < lines.Count; i++)
        {
            var f = CsvFields(lines[i].TrimStart('﻿'), sep);
            if (f.Count < 3) { skipped.Add($"Zeile {i + 1}: zu wenige Spalten"); continue; }
            var thema = f[0].Trim() is { Length: > 0 } th ? Themen.FirstOrDefault(t => t.Id == th || string.Equals(t.Titel, th, StringComparison.OrdinalIgnoreCase))?.Id : d.Text("thema");
            if (thema is null) { skipped.Add($"Zeile {i + 1}: Thema „{f[0]}“ unbekannt"); continue; }
            if (f[1].Trim().Length < 3 || f[2].Trim().Length == 0) { skipped.Add($"Zeile {i + 1}: Frage oder Antwort fehlt"); continue; }
            if (HatLink(string.Join(" ", f))) { skipped.Add($"Zeile {i + 1}: enthält einen Link (im Kinderprofil nicht erlaubt)"); continue; }
            var antworten = f[2].Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(NormSoll).Where(a => a.Length > 0).Distinct().ToArray(); // M8: Soll-Antworten vereinheitlicht
            if (antworten.Length == 0) { skipped.Add($"Zeile {i + 1}: Antwort fehlt"); continue; }
            var art = ArtVon(antworten);
            rows.Add(new JsonObject { ["id"] = "u" + Guid.NewGuid().ToString("N")[..8], ["thema"] = thema, ["frage"] = f[1].Trim(), ["antworten"] = new JsonArray([.. antworten.Select(a => (JsonNode)a)]),
                ["erklaerung"] = f.ElementAtOrDefault(3)?.Trim() ?? "", ["hinweise"] = new JsonArray([.. f.Skip(4).Select(h => h.Trim()).Where(h => h.Length > 0 && !VerraetLoesung(h, antworten, f[1])).Take(3).Select(h => (JsonNode)h)]),
                ["art"] = art, ["herkunft"] = "eigen" });
        }
        if (rows.Count > 0) d.Edit($"{rows.Count} Übungen eingelesen", x => { var e = (JsonArray)(x["eigene"] ??= new JsonArray()); foreach (var r in rows) e.Add(r); });
        return (rows.Count, skipped);
    }

    /// <summary>Arbeitsblatt des aktuellen Themas als Textdokument (für ODT/DOCX): Einführung, Aufgaben zum Ausfüllen, Lösungen am Ende.</summary>
    public static Office.TextDoc Arbeitsblatt(ProgramDocument d, int anzahl = 8, int seed = 1)
    {
        var t = AktuellesThema(d) ?? throw new InvalidOperationException("Wähle zuerst ein Thema.");
        var rng = new Random(seed);
        var items = new List<Item>();
        items.AddRange(Eigene(d, t.Id).Select(e => e.Item));
        if (t.Generator is { } g) while (items.Count < anzahl) items.Add(g(rng));
        else items.AddRange(t.Items!.Where(i => items.All(x => x.Frage != i.Frage)));
        items = [.. items.Take(anzahl)];
        var doc = new Office.TextDoc { Title = "Arbeitsblatt " + t.Titel, Blocks = [] };
        doc.Blocks.Add(new Office.Para($"Arbeitsblatt: {t.Titel}", Office.ParaStyle.Title));
        doc.Blocks.Add(new Office.Para($"{Faecher.First(f => f.Id == t.Fach).Name} · Klasse {KlasseOf(d)} · Name: ____________________ · Datum: ____________", Office.ParaStyle.Subtitle));
        doc.Blocks.Add(new Office.Para(t.Einfuehrung));
        doc.Blocks.Add(new Office.Para("Aufgaben", Office.ParaStyle.Heading1));
        for (var i = 0; i < items.Count; i++) { doc.Blocks.Add(new Office.Para($"{i + 1}. {items[i].Frage}")); doc.Blocks.Add(new Office.Para("Antwort: ______________________________")); }
        doc.Blocks.Add(new Office.Para("Lösungen", Office.ParaStyle.Heading1));
        for (var i = 0; i < items.Count; i++) doc.Blocks.Add(new Office.Para($"{i + 1}. {items[i].Antworten[0]} – {items[i].Erklaerung}"));
        return doc;
    }
}
