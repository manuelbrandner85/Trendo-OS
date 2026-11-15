using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Trendo.Core.Buero;

// ===================== STUDIO-CARDS-01 Karten & Einladungen (T-0671) =====================
// Clean-Room nach dem Bedienkonzept von Canva, Greetings Island und Paperless Post: Vorlage wählen, Seiten der Karte gestalten,
// Gästeliste mit Zu- und Absagen, Serienkarten mit Namen, Druck-PDF mit Beschnitt, Schnitt- und Falzmarken.

/// <summary>Kartenformat: Endformat <see cref="W"/> × <see cref="H"/> mm. Klappkarten werden senkrecht gefalzt – der Druckbogen ist doppelt so breit.</summary>
public sealed record KartenFormat(string Id, string Name, double W, double H, bool Klapp, string Hint)
{
    public double FlatW => Klapp ? 2 * W : W;
}

public sealed class KartenText
{
    public string Text { get; set; } = "";
    public string Schrift { get; set; } = "Georgia";
    /// <summary>Schriftgröße in Punkt.</summary>
    public double Groesse { get; set; } = 14;
    public string Farbe { get; set; } = "#1F2937";
    /// <summary>mitte, links oder rechts.</summary>
    public string Ausrichtung { get; set; } = "mitte";
    /// <summary>Oberkante des Textes als Anteil der Seitenhöhe (0 = oben, 1 = unten).</summary>
    public double Y { get; set; } = 0.4;
    public bool Fett { get; set; }
    public bool Kursiv { get; set; }
}

public sealed class KartenSeite
{
    /// <summary>Hintergrundfarbe als Hex; leer = Grundfarbe der Karte.</summary>
    public string Hintergrund { get; set; } = "";
    public string Bild { get; set; } = "";
    /// <summary>fuellen (ganze Seite bis in den Beschnitt), oben (obere Hälfte) oder mitte (eingepasst).</summary>
    public string BildModus { get; set; } = "fuellen";
    public List<KartenText> Texte { get; set; } = [];
}

/// <summary>Gast mit Antwort (RSVP): offen, zugesagt, abgesagt oder vielleicht; <see cref="Personen"/> = wie viele kommen mit dieser Einladung.</summary>
public sealed class Gast
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Gruppe { get; set; } = "";
    public int Personen { get; set; } = 1;
    public string Status { get; set; } = "offen";
    public string Essen { get; set; } = "";
    public string Notiz { get; set; } = "";
}

public sealed class Karte
{
    public string Format { get; set; } = "a6";
    public string Anlass { get; set; } = "geburtstag";
    public string Hintergrund { get; set; } = "#FFFFFF";
    public string Akzent { get; set; } = "#B45309";
    public KartenSeite Vorne { get; set; } = new();
    public KartenSeite InnenLinks { get; set; } = new();
    public KartenSeite InnenRechts { get; set; } = new();
    public KartenSeite Hinten { get; set; } = new();
    /// <summary>Datum und Ort der Feier, Antwort bis – setzen die Platzhalter {Datum}, {Ort}, {AntwortBis}.</summary>
    public string Datum { get; set; } = "";
    public string Ort { get; set; } = "";
    public string AntwortBis { get; set; } = "";
    public double Beschnitt { get; set; } = 3;
    public bool Marken { get; set; } = true;
    public List<Gast> Gaeste { get; set; } = [];

    static readonly JsonSerializerOptions O = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public JsonObject ToJson() => JsonSerializer.SerializeToNode(this, O)!.AsObject();
    public static Karte FromJson(JsonNode? n)
    {
        if (n is not JsonObject o) return new();
        try { return o.Deserialize<Karte>(O) ?? new(); } catch (JsonException) { return new(); }
    }
    public Karte Copy() => FromJson(ToJson());

    public KartenSeite Seite(string id) => id switch { "vorne" => Vorne, "innen-links" => InnenLinks, "innen-rechts" => InnenRechts, "hinten" => Hinten, _ => throw new ArgumentException("Diese Kartenseite gibt es nicht: " + id) };
}

public static class KartenKit
{
    public static readonly KartenFormat[] Formate =
    [
        new("a6", "Klappkarte A6", 105, 148, true, "105 × 148 mm gefalzt, Bogen A5 quer"),
        new("a5", "Klappkarte A5", 148, 210, true, "148 × 210 mm gefalzt, Bogen A4 quer"),
        new("postkarte", "Postkarte", 148, 105, false, "148 × 105 mm, Vorder- und Rückseite"),
        new("quadrat", "Quadratische Klappkarte", 145, 145, true, "145 × 145 mm gefalzt"),
    ];
    public static KartenFormat Format(string id) => Formate.FirstOrDefault(f => f.Id == id) ?? throw new ArgumentException("Unbekanntes Kartenformat: " + id);

    public static readonly string[] Schriften = ["Georgia", "Segoe Script", "Segoe Print", "Garamond", "Calibri", "Arial", "Bahnschrift", "Times New Roman", "Comic Sans MS"];

    /// <summary>Seiten der Karte in Leserichtung: Klappkarte vorne, innen links, innen rechts, hinten; Postkarte vorne und hinten.</summary>
    public static List<(string Id, string Name)> Seiten(Karte k) => Format(k.Format).Klapp
        ? [("vorne", "Vorderseite"), ("innen-links", "Innen links"), ("innen-rechts", "Innen rechts"), ("hinten", "Rückseite")]
        : [("vorne", "Vorderseite"), ("hinten", "Rückseite")];

    /// <summary>Druckbögen: Klappkarte außen (Rückseite links, Vorderseite rechts – nach dem Falzen liegt vorne oben) und innen; Postkarte je Seite ein Bogen.</summary>
    public static List<(string Name, string[] Panels)> Boegen(Karte k) => Format(k.Format).Klapp
        ? [("Außenseite", ["hinten", "vorne"]), ("Innenseite", ["innen-links", "innen-rechts"])]
        : [("Vorderseite", ["vorne"]), ("Rückseite", ["hinten"])];

    /// <summary>Platzhalter füllen: {Name}, {Personen}, {Datum}, {Ort}, {AntwortBis}.</summary>
    public static string Fill(string text, Karte k, Gast? g)
    {
        var t = (text ?? "").Replace("{Datum}", k.Datum.Trim()).Replace("{Ort}", k.Ort.Trim()).Replace("{AntwortBis}", k.AntwortBis.Trim());
        return g is null ? t : t.Replace("{Name}", g.Name.Trim()).Replace("{Personen}", g.Personen.ToString(CultureInfo.InvariantCulture));
    }
    public static bool HasNamePlaceholder(Karte k) => Seiten(k).Any(s => k.Seite(s.Id).Texte.Any(t => t.Text.Contains("{Name}")));

    /// <summary>Für jede Person auf der Gästeliste (ohne Absagen) eine eigene Karte mit eingesetztem Namen.</summary>
    public static List<(Gast Gast, Karte Karte)> Serie(Karte k, bool auchAbgesagt = false)
    {
        if (k.Gaeste.Count == 0) throw new InvalidOperationException("Die Gästeliste ist leer – erst Gäste eintragen.");
        if (!HasNamePlaceholder(k)) throw new InvalidOperationException("Für Serienkarten braucht ein Text den Platzhalter {Name}, z. B. „Liebe(r) {Name},“.");
        var res = new List<(Gast, Karte)>();
        foreach (var g in k.Gaeste.Where(g => g.Name.Trim().Length > 0 && (auchAbgesagt || g.Status != "abgesagt")))
        {
            var c = k.Copy(); c.Gaeste = [];
            foreach (var (id, _) in Seiten(c)) foreach (var t in c.Seite(id).Texte) t.Text = Fill(t.Text, k, g);
            res.Add((g, c));
        }
        return res;
    }

    static string Bg(Karte k, KartenSeite s) => s.Hintergrund.Trim().Length > 0 ? s.Hintergrund : k.Hintergrund;

    static (double R, double G, double B) Rgb(string hex)
    {
        var h = (hex ?? "").Trim().TrimStart('#');
        if (h.Length != 6 || !int.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)) return (1, 1, 1);
        return (((v >> 16) & 255) / 255.0, ((v >> 8) & 255) / 255.0, (v & 255) / 255.0);
    }
    public static bool IsHex(string s) => (s ?? "").Trim() is { Length: 7 } t && t[0] == '#' && int.TryParse(t[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _);
    static double Lum(string hex)
    {
        static double C(double c) => c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        var (r, g, b) = Rgb(hex); return 0.2126 * C(r) + 0.7152 * C(g) + 0.0722 * C(b);
    }
    /// <summary>Kontrast nach WCAG (1 bis 21).</summary>
    public static double Kontrast(string a, string b) { var (x, y) = (Lum(a), Lum(b)); return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05); }

    /// <summary>Prüfung vor dem Druck: Text zu klein oder unlesbar auf dem Hintergrund, Text am Rand, Bild zu klein für 300 dpi, fehlendes Bild, fehlende Angaben.</summary>
    public static List<CvFinding> Pruefen(Karte k)
    {
        var f = new List<CvFinding>(); var fmt = Format(k.Format);
        foreach (var (id, name) in Seiten(k))
        {
            var s = k.Seite(id);
            foreach (var t in s.Texte.Where(t => t.Text.Trim().Length > 0))
            {
                var first = t.Text.Trim().Split('\n')[0];
                if (t.Groesse < 7) f.Add(new(false, name, $"„{first}“ ist mit {t.Groesse:0} pt sehr klein – im Druck ab 8 pt gut lesbar."));
                if (s.Bild.Length == 0 && Kontrast(t.Farbe, Bg(k, s)) < 3) f.Add(new(false, name, $"„{first}“ hebt sich kaum vom Hintergrund ab – dunklere oder hellere Schrift wählen."));
                if (t.Y is < 0.03 or > 0.92) f.Add(new(false, name, $"„{first}“ steht sehr nah am Rand – beim Schneiden kann er angeschnitten werden."));
                if (!Schriften.Contains(t.Schrift)) f.Add(new(false, name, $"Schrift „{t.Schrift}“ ist nicht in der Liste – im PDF wird ersatzweise Arial genommen."));
            }
            if (s.Bild.Length > 0)
            {
                if (!File.Exists(s.Bild)) { f.Add(new(true, name, $"Das Bild „{Path.GetFileName(s.Bild)}“ fehlt – neu einfügen.")); continue; }
                try
                {
                    using var img = XImage.FromFile(s.Bild);
                    var wmm = s.BildModus == "mitte" ? fmt.W * 0.7 : fmt.W + (s.BildModus == "fuellen" ? 2 * k.Beschnitt : 0);
                    var dpi = img.PixelWidth / (wmm / 25.4);
                    if (dpi < 150) f.Add(new(false, name, $"Das Bild hat für diese Größe nur etwa {dpi:0} dpi – im Druck wirkt es unscharf (gut ab 300 dpi)."));
                }
                catch (Exception e) when (e is IOException or InvalidOperationException or ArgumentException or NotSupportedException) { f.Add(new(true, name, $"Das Bild „{Path.GetFileName(s.Bild)}“ lässt sich nicht lesen.")); }
            }
        }
        var all = string.Join("\n", Seiten(k).SelectMany(s => k.Seite(s.Id).Texte.Select(t => t.Text)));
        if (all.Contains("{Datum}") && k.Datum.Trim().Length == 0) f.Add(new(true, "Angaben", "Die Karte nennt {Datum}, aber das Datum ist leer."));
        if (all.Contains("{Ort}") && k.Ort.Trim().Length == 0) f.Add(new(true, "Angaben", "Die Karte nennt {Ort}, aber der Ort ist leer."));
        if (all.Contains("{AntwortBis}") && k.AntwortBis.Trim().Length == 0) f.Add(new(true, "Angaben", "Die Karte nennt {AntwortBis}, aber „Antwort bis“ ist leer."));
        if (!fmt.Klapp && (k.InnenLinks.Texte.Count > 0 || k.InnenRechts.Texte.Count > 0 || k.InnenLinks.Bild.Length > 0 || k.InnenRechts.Bild.Length > 0))
            f.Add(new(false, "Format", "Eine Postkarte hat keine Innenseiten – deren Texte und Bilder werden nicht gedruckt. Auf die Rückseite übertragen oder Klappkarte wählen."));
        if (k.Beschnitt is < 0 or > 10) f.Add(new(true, "Druck", "Der Beschnitt muss zwischen 0 und 10 mm liegen (üblich sind 3 mm)."));
        return f;
    }
}

/// <summary>Vorlagen für Geburtstag, Hochzeit, Einladung, Danke und Weihnachten – eigene Gestaltung (Farben, Schriften, Texte), keine fremden Grafiken.</summary>
public static class KartenVorlagen
{
    public static readonly (string Id, string Name, string Hint)[] Alle =
    [
        ("geburtstag", "Geburtstag", "Warme Farben, Schreibschrift"),
        ("hochzeit", "Hochzeit", "Creme und Gold, mit Antwort bis"),
        ("einladung", "Einladung zur Feier", "Dunkel mit Gelb – Datum, Ort, Antwort"),
        ("danke", "Danke", "Frisches Grün, persönlich mit Namen"),
        ("weihnachten", "Weihnachten", "Dunkelrot und Gold"),
    ];

    static KartenText T(string text, double y, double pt, string font, string color, bool bold = false, bool italic = false, string align = "mitte") =>
        new() { Text = text, Y = y, Groesse = pt, Schrift = font, Farbe = color, Fett = bold, Kursiv = italic, Ausrichtung = align };

    public static Karte Neu(string id, string format = "a6")
    {
        KartenKit.Format(format);
        var k = new Karte { Format = format, Anlass = id };
        var post = !KartenKit.Format(format).Klapp;
        switch (id)
        {
            case "geburtstag":
                k.Hintergrund = "#FFF4E0"; k.Akzent = "#E8590C";
                k.Vorne.Texte = [T("Alles Gute", 0.30, 30, "Segoe Script", "#E8590C", true), T("zum Geburtstag!", 0.48, 20, "Georgia", "#7C2D12")];
                k.InnenRechts.Texte = [T("Liebe(r) {Name},", 0.22, 14, "Segoe Script", "#7C2D12"), T("zu deinem Geburtstag wünsche ich dir Gesundheit, viel Freude und ein wunderbares neues Lebensjahr.", 0.36, 11.5, "Georgia", "#1F2937")];
                k.Hinten.Texte = [T("Mit herzlichen Grüßen", 0.8, 9, "Georgia", "#7C2D12")];
                break;
            case "hochzeit":
                k.Hintergrund = "#FBF8F3"; k.Akzent = "#B08D57"; k.Datum = "12.06.2027"; k.Ort = "Gut Beispielhof, Musterstadt"; k.AntwortBis = "01.05.2027";
                k.Vorne.Texte = [T("Wir heiraten", 0.32, 28, "Segoe Script", "#8A6A35"), T("{Datum}", 0.52, 14, "Georgia", "#5B4636")];
                k.InnenLinks.Texte = [T("Liebe(r) {Name},", 0.3, 15, "Segoe Script", "#8A6A35")];
                k.InnenRechts.Texte = [T("wir laden dich herzlich zu unserer Hochzeit ein.", 0.2, 12, "Georgia", "#1F2937", italic: true),
                    T("am {Datum}\n{Ort}", 0.42, 12, "Georgia", "#5B4636", true), T("Bitte gib uns bis {AntwortBis} Bescheid, ob du kommst.", 0.7, 10, "Georgia", "#5B4636")];
                break;
            case "einladung":
                k.Hintergrund = "#1E293B"; k.Akzent = "#FACC15"; k.Datum = "Samstag, 20.03.2027, 19 Uhr"; k.Ort = "Gemeindesaal Musterstadt"; k.AntwortBis = "10.03.2027";
                k.Vorne.Texte = [T("Einladung", 0.30, 30, "Bahnschrift", "#FACC15", true), T("Wir feiern – und du bist dabei!", 0.52, 13, "Bahnschrift", "#F8FAFC")];
                k.InnenRechts.Hintergrund = "#FFFFFF";
                k.InnenRechts.Texte = [T("Hallo {Name},", 0.18, 14, "Bahnschrift", "#1E293B", true), T("wann: {Datum}\nwo: {Ort}", 0.34, 12, "Bahnschrift", "#1E293B"),
                    T("Bitte sag bis {AntwortBis} zu oder ab.", 0.66, 10.5, "Bahnschrift", "#475569")];
                break;
            case "danke":
                k.Hintergrund = "#F0FDF4"; k.Akzent = "#15803D";
                k.Vorne.Texte = [T("Danke!", 0.36, 36, "Segoe Script", "#15803D", true)];
                k.InnenRechts.Texte = [T("Liebe(r) {Name},", 0.22, 14, "Segoe Script", "#166534"), T("vielen Dank, dass du an mich gedacht hast. Ich habe mich sehr gefreut!", 0.36, 11.5, "Georgia", "#1F2937")];
                break;
            case "weihnachten":
                k.Hintergrund = "#7F1D1D"; k.Akzent = "#FDE68A";
                k.Vorne.Texte = [T("Frohe", 0.28, 26, "Segoe Script", "#FDE68A"), T("Weihnachten", 0.42, 30, "Georgia", "#FDE68A", true)];
                k.InnenRechts.Hintergrund = "#FFFBEB";
                k.InnenRechts.Texte = [T("Liebe(r) {Name},", 0.22, 14, "Segoe Script", "#7F1D1D"), T("wir wünschen dir ein friedliches Weihnachtsfest und ein gesundes neues Jahr.", 0.36, 11.5, "Georgia", "#1F2937")];
                break;
            default: throw new ArgumentException("Unbekannte Vorlage: " + id);
        }
        if (post)
        {
            // Postkarte: Innentexte wandern auf die Rückseite
            if (k.InnenRechts.Texte.Count > 0 || k.InnenLinks.Texte.Count > 0)
            {
                k.Hinten.Hintergrund = k.InnenRechts.Hintergrund;
                k.Hinten.Texte = [.. k.InnenLinks.Texte, .. k.InnenRechts.Texte];
                k.InnenLinks = new(); k.InnenRechts = new();
            }
        }
        return k;
    }
}

/// <summary>Gästeliste mit Zu- und Absagen, Zählung, CSV hin und zurück.</summary>
public static class Gaeste
{
    public static readonly (string Id, string Name)[] Status = [("offen", "Offen"), ("zugesagt", "Zugesagt"), ("vielleicht", "Vielleicht"), ("abgesagt", "Abgesagt")];
    public static string StatusName(string id) => Status.FirstOrDefault(s => s.Id == id).Name ?? id;
    public static string ParseStatus(string s) => (s ?? "").Trim().ToLowerInvariant() switch
    {
        "ja" or "zugesagt" or "zusage" or "kommt" or "yes" => "zugesagt",
        "nein" or "abgesagt" or "absage" or "kommt nicht" or "no" => "abgesagt",
        "vielleicht" or "unsicher" or "maybe" => "vielleicht",
        _ => "offen",
    };

    public sealed record Stand(int Eingeladen, int Zusagen, int Absagen, int Vielleicht, int Offen, int PersonenZugesagt, int PersonenMoeglich);
    public static Stand Zaehlen(IEnumerable<Gast> list)
    {
        var l = list.ToList();
        return new(l.Count, l.Count(g => g.Status == "zugesagt"), l.Count(g => g.Status == "abgesagt"), l.Count(g => g.Status == "vielleicht"), l.Count(g => g.Status == "offen"),
            l.Where(g => g.Status == "zugesagt").Sum(g => Math.Max(1, g.Personen)), l.Where(g => g.Status is "zugesagt" or "vielleicht" or "offen").Sum(g => Math.Max(1, g.Personen)));
    }

    /// <summary>Schnell eintragen: eine Person je Zeile, z. B. „Familie Muster, 4“ oder „Erika Muster; erika@example.org“.</summary>
    public static List<Gast> ParseLines(string text)
    {
        var res = new List<Gast>();
        foreach (var raw in (text ?? "").Replace("\r", "").Split('\n'))
        {
            var parts = raw.Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;
            var g = new Gast { Id = "g" + Guid.NewGuid().ToString("N")[..8], Name = parts[0] };
            foreach (var p in parts.Skip(1))
            {
                if (int.TryParse(p, out var n) && n is >= 1 and <= 50) g.Personen = n;
                else if (p.Contains('@')) g.Email = p;
                else if (ParseStatus(p) != "offen") g.Status = ParseStatus(p);
                else g.Gruppe = p;
            }
            res.Add(g);
        }
        return res;
    }

    static string Q(string s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
    public static string Csv(IEnumerable<Gast> list)
    {
        var sb = new StringBuilder("Name;E-Mail;Gruppe;Personen;Antwort;Essen;Notiz\r\n");
        foreach (var g in list) sb.Append(string.Join(";", Q(g.Name), Q(g.Email), Q(g.Gruppe), g.Personen.ToString(CultureInfo.InvariantCulture), Q(StatusName(g.Status)), Q(g.Essen), Q(g.Notiz))).Append("\r\n");
        return sb.ToString();
    }

    static List<string> Fields(string line, char sep)
    {
        var res = new List<string>(); var cur = new StringBuilder(); var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quoted) { if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; } else if (c == '"') quoted = false; else cur.Append(c); }
            else if (c == '"') quoted = true;
            else if (c == sep) { res.Add(cur.ToString()); cur.Clear(); }
            else cur.Append(c);
        }
        res.Add(cur.ToString()); return res;
    }

    /// <summary>CSV lesen (Trenner ; oder , – erste Zeile mit Spaltennamen; „Name“ ist Pflicht).</summary>
    public static List<Gast> FromCsv(string text)
    {
        var lines = (text ?? "").TrimStart('﻿').Replace("\r", "").Split('\n').Where(l => l.Trim().Length > 0).ToList();
        if (lines.Count == 0) return [];
        var sep = lines[0].Count(c => c == ';') >= lines[0].Count(c => c == ',') ? ';' : ',';
        var head = Fields(lines[0], sep).Select(h => h.Trim().ToLowerInvariant()).ToList();
        int Col(params string[] names) => head.FindIndex(h => names.Contains(h));
        var cName = Col("name", "gast", "vorname nachname"); if (cName < 0) throw new InvalidDataException("In der ersten Zeile fehlt die Spalte „Name“.");
        int cMail = Col("e-mail", "email", "mail"), cGrp = Col("gruppe", "tisch"), cN = Col("personen", "anzahl"), cSt = Col("antwort", "status", "rsvp"), cEss = Col("essen", "menü", "menu"), cNote = Col("notiz", "bemerkung");
        string At(List<string> f, int i) => i >= 0 && i < f.Count ? f[i].Trim() : "";
        var res = new List<Gast>();
        foreach (var l in lines.Skip(1))
        {
            var f = Fields(l, sep); if (At(f, cName).Length == 0) continue;
            res.Add(new Gast { Id = "g" + Guid.NewGuid().ToString("N")[..8], Name = At(f, cName), Email = At(f, cMail), Gruppe = At(f, cGrp), Personen = int.TryParse(At(f, cN), out var n) && n >= 1 ? n : 1,
                Status = ParseStatus(At(f, cSt)), Essen = At(f, cEss), Notiz = At(f, cNote) });
        }
        return res;
    }
}

/// <summary>Druck-PDF: je Druckbogen eine Seite mit Beschnitt (Hintergrund und Bilder laufen hinaus), Schnittmarken an den Ecken, Falzmarke an der Mitte,
/// TrimBox/BleedBox für die Druckerei. Gezeichnet mit PDFsharp (MIT).</summary>
public static class KartenPdf
{
    const double Mm = 72 / 25.4;
    /// <summary>Rand außerhalb des Beschnitts für die Marken (mm).</summary>
    public const double Slug = 10;

    static XColor C(string hex)
    {
        var h = (hex ?? "").Trim().TrimStart('#');
        return h.Length == 6 && int.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v) ? XColor.FromArgb((v >> 16) & 255, (v >> 8) & 255, v & 255) : XColors.White;
    }

    /// <summary>Seitengröße eines Bogens in mm (Endformat + Beschnitt + Markenrand).</summary>
    public static (double W, double H) PageSize(Karte k)
    {
        var f = KartenKit.Format(k.Format); var o = k.Beschnitt + (k.Marken ? Slug : 0);
        return (f.FlatW + 2 * o, f.H + 2 * o);
    }

    /// <summary>Eine Karte (oder eine Serie, je Gast ein Satz Bögen) in ein PDF. Gibt die Seitenzahl zurück.</summary>
    public static int Write(Karte k, string path, Gast? gast = null) => Write([(gast, k)], k, path);
    public static int WriteSerie(Karte k, string path) => Write([.. KartenKit.Serie(k).Select(x => ((Gast?)x.Gast, x.Karte))], k, path);

    static int Write(IReadOnlyList<(Gast? Gast, Karte Karte)> cards, Karte master, string path)
    {
        if (master.Beschnitt is < 0 or > 10) throw new ArgumentException("Der Beschnitt muss zwischen 0 und 10 mm liegen.");
        Sys.PdfTools.EnsureFonts();
        using var doc = new PdfDocument(); doc.Info.Title = "Karte – " + master.Anlass;
        var measure = XGraphics.CreateMeasureContext(new XSize(595, 842), XGraphicsUnit.Point, XPageDirection.Downwards);
        foreach (var (gast, k) in cards)
        {
            var f = KartenKit.Format(k.Format); var b = k.Beschnitt; var o = b + (k.Marken ? Slug : 0);
            var (pw, ph) = PageSize(k);
            foreach (var (_, panels) in KartenKit.Boegen(k))
            {
                var page = doc.AddPage(); page.Width = XUnit.FromMillimeter(pw); page.Height = XUnit.FromMillimeter(ph);
                page.TrimBox = new PdfRectangle(new XRect(o * Mm, o * Mm, f.FlatW * Mm, f.H * Mm));
                page.BleedBox = new PdfRectangle(new XRect((o - b) * Mm, (o - b) * Mm, (f.FlatW + 2 * b) * Mm, (f.H + 2 * b) * Mm));
                using var g = XGraphics.FromPdfPage(page);
                for (var i = 0; i < panels.Length; i++)
                {
                    var s = k.Seite(panels[i]);
                    // Feld der Seite im Endformat, außen um den Beschnitt erweitert
                    double x0 = o + i * f.W, y0 = o, w = f.W, h = f.H;
                    double bx0 = x0 - (i == 0 ? b : 0), bx1 = x0 + w + (i == panels.Length - 1 ? b : 0);
                    var bleed = new XRect(bx0 * Mm, (y0 - b) * Mm, (bx1 - bx0) * Mm, (h + 2 * b) * Mm);
                    g.DrawRectangle(new XSolidBrush(C(s.Hintergrund.Trim().Length > 0 ? s.Hintergrund : k.Hintergrund)), bleed);
                    if (s.Bild.Length > 0 && File.Exists(s.Bild)) Image(g, s, bleed, new XRect(x0 * Mm, y0 * Mm, w * Mm, h * Mm));
                    foreach (var t in s.Texte.Where(t => t.Text.Trim().Length > 0))
                    {
                        var style = (t.Fett ? XFontStyleEx.Bold : XFontStyleEx.Regular) | (t.Kursiv ? XFontStyleEx.Italic : XFontStyleEx.Regular);
                        XFont font;
                        try { font = new XFont(KartenKit.Schriften.Contains(t.Schrift) ? t.Schrift : "Arial", Math.Clamp(t.Groesse, 4, 120), style); }
                        catch (Exception e) when (e is InvalidOperationException or ArgumentException) { font = new XFont("Arial", Math.Clamp(t.Groesse, 4, 120), style); }
                        var margin = 8.0; var maxW = (w - 2 * margin) * Mm; var lh = t.Groesse * 1.25;
                        var y = (y0 + Math.Clamp(t.Y, 0, 1) * h) * Mm;
                        foreach (var line in Wrap(KartenKit.Fill(t.Text, k, gast), font, maxW, measure))
                        {
                            var fmt = t.Ausrichtung switch { "links" => XStringFormats.TopLeft, "rechts" => XStringFormats.TopRight, _ => XStringFormats.TopCenter };
                            g.DrawString(line, font, new XSolidBrush(C(t.Farbe)), new XRect((x0 + margin) * Mm, y, maxW, lh), fmt);
                            y += lh;
                        }
                    }
                }
                if (k.Marken) Marks(g, f, o, b, panels.Length > 1);
            }
        }
        var n = doc.PageCount;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        doc.Save(path);
        return n;
    }

    static List<string> Wrap(string text, XFont f, double max, XGraphics m)
    {
        var res = new List<string>();
        foreach (var para in text.Replace("\r", "").Split('\n'))
        {
            var line = "";
            foreach (var word in para.Split(' '))
            {
                var test = line.Length == 0 ? word : line + " " + word;
                if (m.MeasureString(test, f).Width <= max || line.Length == 0) { line = test; continue; }
                res.Add(line); line = word;
            }
            res.Add(line);
        }
        return res;
    }

    static void Image(XGraphics g, KartenSeite s, XRect bleed, XRect trim)
    {
        try
        {
            using var img = XImage.FromFile(s.Bild);
            double iw = img.PixelWidth, ih = img.PixelHeight;
            var area = s.BildModus switch
            {
                "oben" => new XRect(bleed.X, bleed.Y, bleed.Width, (trim.Y - bleed.Y) + trim.Height * 0.55),
                "mitte" => new XRect(trim.X + trim.Width * 0.15, trim.Y + trim.Height * 0.15, trim.Width * 0.7, trim.Height * 0.5),
                _ => bleed,
            };
            var state = g.Save();
            g.IntersectClip(area);
            var sc = s.BildModus == "mitte" ? Math.Min(area.Width / iw, area.Height / ih) : Math.Max(area.Width / iw, area.Height / ih);
            var dw = iw * sc; var dh = ih * sc;
            g.DrawImage(img, area.X + (area.Width - dw) / 2, area.Y + (area.Height - dh) / 2, dw, dh);
            g.Restore(state);
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or ArgumentException or NotSupportedException) { }
    }

    /// <summary>Schnittmarken an den vier Ecken des Endformats (außerhalb des Beschnitts) und Falzmarke (gestrichelt) oben und unten an der Mitte.</summary>
    static void Marks(XGraphics g, KartenFormat f, double o, double b, bool fold)
    {
        var pen = new XPen(XColors.Black, 0.25 * Mm); var len = Slug - 3;
        double l = o, r = o + f.FlatW, t = o, btm = o + f.H; var gap = b + 1;
        foreach (var x in new[] { l, r })
        {
            g.DrawLine(pen, x * Mm, (t - gap) * Mm, x * Mm, (t - gap - len) * Mm);
            g.DrawLine(pen, x * Mm, (btm + gap) * Mm, x * Mm, (btm + gap + len) * Mm);
        }
        foreach (var y in new[] { t, btm })
        {
            g.DrawLine(pen, (l - gap) * Mm, y * Mm, (l - gap - len) * Mm, y * Mm);
            g.DrawLine(pen, (r + gap) * Mm, y * Mm, (r + gap + len) * Mm, y * Mm);
        }
        if (fold)
        {
            var dash = new XPen(XColors.Black, 0.25 * Mm) { DashStyle = XDashStyle.Dash }; var x = o + f.W;
            g.DrawLine(dash, x * Mm, (t - gap) * Mm, x * Mm, (t - gap - len) * Mm);
            g.DrawLine(dash, x * Mm, (btm + gap) * Mm, x * Mm, (btm + gap + len) * Mm);
        }
    }
}
