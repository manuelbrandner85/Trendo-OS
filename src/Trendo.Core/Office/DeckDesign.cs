using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Trendo.Core.Office;

/// <summary>Folienarten des Gestaltungs-Studios. Jede wird auf eine Folie des vorhandenen Präsentationskerns (<see cref="SlideDoc"/>) abgebildet –
/// es gibt kein zweites Präsentationsformat.</summary>
public enum DeckLayout { Titel, Abschnitt, Stichpunkte, ZweiSpalten, BildText, Zitat, Kennzahl, Zeitleiste }

/// <summary>Ein Abschnitt der Gliederung: Folientitel, Punkte, Sprechernotizen, optional ein Bild und eine fest gewählte Folienart.</summary>
public sealed record DeckSection(string Title, List<string> Points, List<string> Notes, string? Image = null, DeckLayout? Forced = null);

/// <summary>Regeln: höchstens <paramref name="MaxPoints"/> Punkte je Folie, Punkte über <paramref name="MaxPointChars"/> Zeichen werden geteilt bzw. gekürzt
/// (der volle Text wandert in die Notizen); <paramref name="Minutes"/> = Redezeit gesamt (0 = aus den Inhalten schätzen).</summary>
public sealed record DeckOptions(int MaxPoints = 6, int MaxPointChars = 90, double Minutes = 0, bool AutoNotes = true);

/// <summary>Hinweis zu einer Folie (1-basiert, 0 = ganze Präsentation). Arten: aufgeteilt, gekürzt, kontrast, zu-lang, notiz.</summary>
public sealed record DeckFinding(int Slide, string Kind, string Message);

public sealed record DeckResult(SlideDoc Doc, List<DeckLayout> Layouts, List<double> Minutes, List<DeckFinding> Findings);

/// <summary>STUDIO-DECKDESIGN-01 „Präsentationen gestalten“: aus Stichpunkten eine fertig gestaltete Präsentation – Clean-Room nach dem Bedienkonzept
/// von Gamma, Pitch, Beautiful.ai und PowerPoint Designer (kein fremder Code, keine fremden Vorlagen). Baut auf <see cref="SlideDoc"/>,
/// <see cref="SlideLayouts"/>, <see cref="SlideFiles"/> (PPTX) und <see cref="SlidePdf"/> (PDF) auf.</summary>
public static class DeckDesign
{
    const double W = SlideDoc.WidthCm, H = SlideDoc.HeightCm, PtCm = 2.54 / 72;
    static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Design-Themen: die sechs der Präsentation plus eigene mit Schrift. Alle bestehen die Kontrast-Prüfung.</summary>
    public static readonly IReadOnlyList<SlideTheme> Themes =
    [
        new("gold", "Dunkel-Gold", "#111827", "#F5D27A", "#E5E7EB", "#D4A017", "Segoe UI", "#1F2937"),
        new("papier", "Papier", "#FAF7F0", "#1F2937", "#374151", "#B45309", "Georgia"),
        new("minimal", "Minimal", "#FFFFFF", "#111111", "#333333", "#BE123C", "Arial"),
        new("frisch", "Frisch", "#ECFEFF", "#134E4A", "#115E59", "#0F766E", "Verdana"),
        .. SlideThemes.All.Select(FixContrast), // z. B. „Sonne“: Akzent Orange auf Hellgelb wäre zu blass
    ];

    public static SlideTheme Theme(string id) => Themes.FirstOrDefault(t => t.Id == id) ?? Themes[0];

    public static readonly IReadOnlyList<(DeckLayout Layout, string Name)> LayoutNames =
    [
        (DeckLayout.Titel, "Titel"), (DeckLayout.Abschnitt, "Abschnitt"), (DeckLayout.Stichpunkte, "Stichpunkte"), (DeckLayout.ZweiSpalten, "Zwei Spalten"),
        (DeckLayout.BildText, "Bild und Text"), (DeckLayout.Zitat, "Zitat"), (DeckLayout.Kennzahl, "Kennzahlen"), (DeckLayout.Zeitleiste, "Zeitleiste"),
    ];
    public static string Name(DeckLayout l) => LayoutNames.First(n => n.Layout == l).Name;
    public static DeckLayout? ParseLayout(string? s) => LayoutNames.Where(n => string.Equals(n.Layout.ToString(), s?.Trim(), StringComparison.OrdinalIgnoreCase) || string.Equals(n.Name, s?.Trim(), StringComparison.OrdinalIgnoreCase)).Select(n => (DeckLayout?)n.Layout).FirstOrDefault();

    // ---------- Farben und Kontrast (WCAG 2.x) ----------

    public static bool IsHex(string? s) => s is not null && Regex.IsMatch(s.Trim(), "^#[0-9A-Fa-f]{6}$");
    static (int R, int G, int B) Rgb(string hex)
    {
        if (!IsHex(hex)) throw new FormatException($"„{hex}“ ist keine Farbe. Bitte z. B. #1F2937 eingeben.");
        var v = Convert.ToInt32(hex.Trim()[1..], 16); return ((v >> 16) & 255, (v >> 8) & 255, v & 255);
    }
    static string Hex(double r, double g, double b) => $"#{(int)Math.Round(Math.Clamp(r, 0, 255)):X2}{(int)Math.Round(Math.Clamp(g, 0, 255)):X2}{(int)Math.Round(Math.Clamp(b, 0, 255)):X2}";

    /// <summary>Relative Leuchtdichte nach WCAG.</summary>
    public static double Luminance(string hex)
    {
        static double L(int c) { var x = c / 255.0; return x <= 0.03928 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4); }
        var (r, g, b) = Rgb(hex); return 0.2126 * L(r) + 0.7152 * L(g) + 0.0722 * L(b);
    }

    /// <summary>Kontrastverhältnis 1 … 21.</summary>
    public static double Contrast(string a, string b) { var la = Luminance(a); var lb = Luminance(b); return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05); }

    /// <summary>Mindestwerte: Fließtext 4,5 : 1, Titel (große Schrift) und Akzent 3 : 1.</summary>
    public const double MinText = 4.5, MinLarge = 3.0;

    /// <summary>Prüft alle Schriftfarben gegen beide Hintergründe. Leer = alles gut lesbar.</summary>
    public static List<string> CheckTheme(SlideTheme t)
    {
        var res = new List<string>();
        foreach (var (bg, where) in new[] { (t.Background, "Hintergrund"), (t.Background2, "zweiter Hintergrund") })
        {
            if (bg is null) continue;
            void Need(string fg, double min, string what)
            {
                var c = Contrast(fg, bg);
                if (c < min) res.Add(string.Create(De, $"{what} auf {where}: Kontrast {c:0.0} : 1, nötig {min:0.0} : 1"));
            }
            Need(t.Text, MinText, "Text"); Need(t.Title, MinLarge, "Titel"); Need(t.Accent, MinLarge, "Akzentfarbe");
        }
        return res;
    }

    static string Mix(string a, string b, double f) { var (r1, g1, b1) = Rgb(a); var (r2, g2, b2) = Rgb(b); return Hex(r1 + (r2 - r1) * f, g1 + (g2 - g1) * f, b1 + (b2 - b1) * f); }

    /// <summary>Farbe so wenig wie nötig Richtung Schwarz oder Weiß schieben, bis sie auf allen Hintergründen den Mindestkontrast hat.</summary>
    public static string Readable(string fg, double min, params string?[] backgrounds)
    {
        var bgs = backgrounds.OfType<string>().ToList(); if (bgs.Count == 0 || bgs.All(b => Contrast(fg, b) >= min)) return fg;
        string best = fg; var bestScore = double.MinValue;
        foreach (var target in new[] { "#000000", "#FFFFFF" })
            for (var f = 0.05; f <= 1.0001; f += 0.05)
            {
                var c = Mix(fg, target, f); var worst = bgs.Min(b => Contrast(c, b));
                if (worst >= min) { var score = 1 - f; if (score > bestScore) { bestScore = score; best = c; } break; }
            }
        if (bestScore == double.MinValue) best = bgs.Average(Luminance) > 0.18 ? "#000000" : "#FFFFFF";
        return best;
    }

    /// <summary>Design mit lesbaren Schriftfarben (Name bleibt, Hintergrund bleibt).</summary>
    public static SlideTheme FixContrast(SlideTheme t) => t with
    {
        Text = Readable(t.Text, MinText, t.Background, t.Background2),
        Title = Readable(t.Title, MinLarge, t.Background, t.Background2),
        Accent = Readable(t.Accent, MinLarge, t.Background, t.Background2),
    };

    // ---------- Gliederung lesen ----------

    static readonly Regex Tag = new(@"\s*\[(?<t>[^\]]+)\]\s*$");

    /// <summary>Gliederung lesen: Zeile ohne Einrückung (oder mit „#“) = Folie, eingerückt oder mit „- “/„• “/„* “ = Punkt, „&gt; “ oder „Notiz:“ = Sprechernotiz,
    /// „Bild: datei.png“ = Bild der Folie, „[zitat]“ am Titelende = Folienart fest wählen.</summary>
    public static List<DeckSection> Parse(string outline)
    {
        var res = new List<DeckSection>();
        foreach (var raw in (outline ?? "").Replace("\r\n", "\n").Split('\n'))
        {
            if (raw.Trim().Length == 0 || raw.Trim() is "---" or "```" || raw.TrimStart().StartsWith("```", StringComparison.Ordinal)) continue;
            var line = raw.TrimEnd(); var t = line.TrimStart(); var indented = line.Length > t.Length;
            var cur = res.Count > 0 ? res[^1] : null;
            if (t.StartsWith('>')) { (cur ?? Add(res, "Präsentation")).Notes.Add(t[1..].Trim()); continue; }
            if (t.StartsWith("Notiz:", StringComparison.OrdinalIgnoreCase)) { (cur ?? Add(res, "Präsentation")).Notes.Add(t[6..].Trim()); continue; }
            if (t.StartsWith("Bild:", StringComparison.OrdinalIgnoreCase) && cur is not null) { res[^1] = cur with { Image = t[5..].Trim().Trim('"') }; continue; }
            var bullet = t.Length > 1 && t[0] is '-' or '•' or '*' or '–' && t[1] == ' ';
            if (t.StartsWith('#') || (!indented && !bullet))
            {
                var title = t.TrimStart('#').Trim();
                DeckLayout? forced = null;
                var m = Tag.Match(title);
                if (m.Success && ParseLayout(m.Groups["t"].Value) is { } fl) { forced = fl; title = title[..m.Index].Trim(); }
                res.Add(new(title.TrimEnd(':').Trim(), [], [], null, forced));
                continue;
            }
            var text = bullet ? t[2..].Trim() : t;
            if (text.Length > 0) (cur ?? Add(res, "Präsentation")).Points.Add(text);
        }
        return res;

        static DeckSection Add(List<DeckSection> l, string title) { var s = new DeckSection(title, [], []); l.Add(s); return s; }
    }

    /// <summary>Gliederung aus einer vorhandenen Präsentation (z. B. aus „Trendo Präsentation“) – Titel, Punkte, Notizen.</summary>
    public static string FromDeck(SlideDoc doc)
    {
        var sb = new StringBuilder();
        foreach (var s in doc.Slides)
        {
            sb.Append(s.Title.Length > 0 ? s.Title : "Folie").Append('\n');
            foreach (var shp in s.Shapes.Where(x => x.Kind == ShapeKind.Text && x.Role != "title" && x.Text.Trim().Length > 0))
                foreach (var l in shp.Text.Replace("\r", "").Split('\n').Where(l => l.Trim().Length > 0)) sb.Append("  - ").Append(l.Trim()).Append('\n');
            foreach (var n in s.Notes.Replace("\r", "").Split('\n').Where(n => n.Trim().Length > 0)) sb.Append("  > ").Append(n.Trim()).Append('\n');
        }
        return sb.ToString();
    }

    // ---------- Folienart je Inhalt ----------

    static readonly Regex Figure = new(@"^[+\-−]?\d[\d.,]*\s*(%|‰|€|\$|x|×|k|Mio\.?|Mrd\.?|Tsd\.?|Std\.?|h|min|km|kg|t|m²|Prozent|Euro|Mal)?(\s|$|:)", RegexOptions.IgnoreCase);
    static readonly Regex DatePoint = new(@"^((19|20)\d{2}|Q[1-4]( (19|20)\d{2})?|\d{1,2}\.\d{1,2}\.(\d{2,4})?|(Jan|Feb|Mär|Mar|Apr|Mai|Jun|Jul|Aug|Sep|Okt|Nov|Dez)\w*\.?( (19|20)\d{2})?|(Woche|Monat|Phase|Schritt|Tag) \d+|\d{1,2}[:.]\d{2}( Uhr)?)\s*(:|–|-|—)\s*\S", RegexOptions.IgnoreCase);
    static readonly string[] ColumnHeads = ["links:", "rechts:", "vorher:", "nachher:", "pro:", "contra:", "dafür:", "dagegen:", "heute:", "morgen:", "ist:", "soll:"];

    public static bool IsQuote(string p) { var t = p.Trim(); return t.Length > 2 && t[0] is '„' or '"' or '“' or '»' or '«' or '‚'; }
    /// <summary>Punkt beginnt mit einer Kennzahl („42 %“, „1,2 Mio.“) – keine Aufzählungsnummer wie „1. Schritt“ und keine Jahreszahl mit Doppelpunkt.</summary>
    public static bool IsFigure(string p) { var t = p.Trim(); return Figure.IsMatch(t) && !Regex.IsMatch(t, @"^\d{1,2}\.\s") && !IsDate(t); }
    public static bool IsDate(string p) => DatePoint.IsMatch(p.Trim());

    /// <summary>Folienart aus dem Inhalt, wie es PowerPoint Designer und Beautiful.ai anbieten: Zitat, Kennzahlen, Zeitleiste, Gegenüberstellung, Bild, sonst Stichpunkte.</summary>
    public static DeckLayout Choose(DeckSection s, int index)
    {
        if (s.Forced is { } f) return f;
        var p = s.Points;
        if (index == 0 && p.Count <= 1 && s.Image is null) return DeckLayout.Titel;
        if (s.Image is not null) return DeckLayout.BildText;
        if (p.Count == 0) return DeckLayout.Abschnitt;
        if (p.Count <= 2 && IsQuote(p[0])) return DeckLayout.Zitat;
        if (p.Count <= 4 && p.All(IsFigure)) return DeckLayout.Kennzahl;
        if (p.Count >= 3 && p.All(IsDate)) return DeckLayout.Zeitleiste;
        if (p.Any(x => x.Contains(" | ", StringComparison.Ordinal)) || p.Count(x => ColumnHeads.Any(h => x.StartsWith(h, StringComparison.OrdinalIgnoreCase))) >= 2) return DeckLayout.ZweiSpalten;
        return DeckLayout.Stichpunkte;
    }

    // ---------- Regeln: höchstens N Punkte, zu lange Texte teilen ----------

    /// <summary>Langen Punkt an Satzgrenzen teilen; ein Satz, der allein zu lang ist, wird am Wortende gekürzt („…“).</summary>
    public static List<string> SplitPoint(string p, int max, out bool shortened)
    {
        shortened = false; p = p.Trim();
        if (p.Length <= max) return [p];
        var parts = new List<string>(); var cur = "";
        foreach (var sentence in Regex.Split(p, @"(?<=[.!?;])\s+").Where(x => x.Length > 0))
        {
            if (cur.Length > 0 && cur.Length + 1 + sentence.Length > max) { parts.Add(cur); cur = ""; }
            cur = cur.Length == 0 ? sentence : cur + " " + sentence;
        }
        if (cur.Length > 0) parts.Add(cur);
        for (var i = 0; i < parts.Count; i++)
        {
            if (parts[i].Length <= max) continue;
            var cut = parts[i][..max]; var sp = cut.LastIndexOf(' ');
            parts[i] = (sp > max / 2 ? cut[..sp] : cut[..(max - 1)]).TrimEnd(',', ';', ':', ' ') + " …"; shortened = true;
        }
        return parts;
    }

    /// <summary>Regeln anwenden. Gibt die fertigen Abschnitte mit Folienart zurück; was geändert wurde, steht in <paramref name="findings"/> (Folie = Index in der Ausgabe + 1).</summary>
    public static List<(DeckSection Section, DeckLayout Layout)> ApplyRules(IReadOnlyList<DeckSection> sections, DeckOptions o, List<DeckFinding> findings)
    {
        var max = Math.Clamp(o.MaxPoints, 2, 12); var chars = Math.Clamp(o.MaxPointChars, 30, 400);
        var res = new List<(DeckSection, DeckLayout)>();
        for (var i = 0; i < sections.Count; i++)
        {
            var s = sections[i]; var layout = Choose(s, i);
            if (layout is DeckLayout.Titel or DeckLayout.Abschnitt or DeckLayout.Zitat or DeckLayout.Kennzahl) { res.Add((s, layout)); continue; }
            var points = new List<string>(); var notes = s.Notes.ToList(); var anyShort = false; var anySplit = false;
            foreach (var p in s.Points)
            {
                List<string> parts; var sh = false;
                if (layout == DeckLayout.ZweiSpalten) parts = [p]; else parts = SplitPoint(p, chars, out sh);
                if (parts.Count > 1) anySplit = true;
                if (sh) { anyShort = true; notes.Add(p); }
                points.AddRange(parts);
            }
            var perSlide = layout == DeckLayout.ZweiSpalten ? max * 2 : max;
            var groups = points.Count <= perSlide ? 1 : (int)Math.Ceiling(points.Count / (double)perSlide);
            var size = (int)Math.Ceiling(points.Count / (double)groups);
            for (var g = 0; g < groups; g++)
            {
                var chunk = points.Skip(g * size).Take(size).ToList();
                var title = g == 0 ? s.Title : $"{s.Title} (Forts.)";
                res.Add((new DeckSection(title, chunk, g == 0 ? notes : [], g == 0 ? s.Image : null, s.Forced), layout));
            }
            var at = res.Count - groups + 1;
            if (groups > 1) findings.Add(new(at, "aufgeteilt", $"„{s.Title}“ hatte {points.Count} Punkte – auf {groups} Folien verteilt (höchstens {perSlide} je Folie)."));
            if (anySplit) findings.Add(new(at, "aufgeteilt", $"„{s.Title}“: lange Punkte an Satzgrenzen geteilt (höchstens {chars} Zeichen je Punkt)."));
            if (anyShort) findings.Add(new(at, "gekürzt", $"„{s.Title}“: ein Satz war zu lang und ist gekürzt – der volle Text steht in den Sprechernotizen."));
        }
        return res;
    }

    // ---------- Schriftgröße und Zeitplan ----------

    /// <summary>Größte Schrift (Punkt) zwischen <paramref name="min"/> und <paramref name="max"/>, bei der der Text in das Feld passt (Schätzung: Zeichen 0,5 em breit, Zeile 1,25 em).</summary>
    public static (double Size, bool Fits) FitSize(string text, double wCm, double hCm, double max, double min = 14, bool bullets = false)
    {
        var lines = (text ?? "").Replace("\r", "").Split('\n');
        for (var size = max; size >= min - 0.01; size -= 2)
        {
            var em = size * PtCm; var perLine = Math.Max(1, (int)((wCm - (bullets ? em * 1.2 : 0)) / (em * 0.5)));
            var rows = lines.Sum(l => Math.Max(1, (int)Math.Ceiling(l.Length / (double)perLine)));
            if (rows * em * 1.25 + (bullets ? lines.Length * em * 0.3 : 0) <= hCm) return (size, true);
        }
        return (min, false);
    }

    static int Words(string s) => s.Split([' ', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;

    /// <summary>Redezeit je Folie in Minuten: aus Notizen (130 Wörter je Minute) und Punkten geschätzt; mit <paramref name="totalMinutes"/> &gt; 0 auf diese Gesamtzeit verteilt.</summary>
    public static List<double> Schedule(IReadOnlyList<(DeckLayout Layout, int Points, string Notes)> slides, double totalMinutes = 0)
    {
        var sec = slides.Select(s => s.Layout switch
        {
            DeckLayout.Titel => 20.0, DeckLayout.Abschnitt => 10.0,
            _ => Math.Max(30, 15 + s.Points * 12 + Words(s.Notes) / 130.0 * 60),
        }).ToList();
        var sum = sec.Sum();
        if (totalMinutes > 0 && sum > 0) sec = sec.Select(x => x / sum * totalMinutes * 60).ToList();
        return sec.Select(x => Math.Round(x / 60, 1)).ToList();
    }

    /// <summary>Startzeit jeder Folie als „m:ss“.</summary>
    public static List<string> Clock(IReadOnlyList<double> minutes)
    {
        var res = new List<string>(); var t = 0.0;
        foreach (var m in minutes) { var s = (int)Math.Round(t * 60); res.Add($"{s / 60}:{s % 60:00}"); t += m; }
        return res;
    }

    // ---------- Bauen ----------

    /// <summary>Fertig gestaltete Präsentation aus einer Gliederung. <paramref name="overrides"/>: Folienart je Folie (0-basiert) von Hand;
    /// <paramref name="loadImage"/> liest die Bilder (null = Platzhalter).</summary>
    public static DeckResult Build(string outline, SlideTheme theme, DeckOptions? options = null, IReadOnlyDictionary<int, DeckLayout>? overrides = null, Func<string, byte[]?>? loadImage = null)
    {
        var o = options ?? new DeckOptions(); var findings = new List<DeckFinding>();
        var sections = Parse(outline);
        if (sections.Count == 0) throw new InvalidOperationException("Die Gliederung ist leer. Schreib je Folie eine Zeile und darunter Punkte mit „- “.");
        var contrast = CheckTheme(theme);
        foreach (var c in contrast) findings.Add(new(0, "kontrast", c));
        var plan = ApplyRules(sections, o, findings);
        var doc = new SlideDoc { Theme = theme }; var layouts = new List<DeckLayout>();
        for (var i = 0; i < plan.Count; i++)
        {
            var (s, auto) = plan[i];
            var layout = overrides is not null && overrides.TryGetValue(i, out var ov) ? ov : auto;
            var slide = Compose(s, layout, theme, i, findings, loadImage);
            var notes = s.Notes.Count > 0 ? string.Join('\n', s.Notes) : o.AutoNotes ? AutoNote(s, layout) : "";
            slide.Notes = notes; slide.Transition = i == 0 ? Transition.Keine : Transition.Ueberblenden;
            doc.Slides.Add(slide); layouts.Add(layout);
        }
        var minutes = Schedule([.. plan.Select((p, i) => (layouts[i], p.Section.Points.Count, doc.Slides[i].Notes))], o.Minutes);
        return new(doc, layouts, minutes, findings);
    }

    /// <summary>Sprechernotiz, wenn keine geschrieben wurde: Kernaussage und die Punkte in ganzen Sätzen.</summary>
    public static string AutoNote(DeckSection s, DeckLayout l) => l switch
    {
        DeckLayout.Titel => $"Begrüßen, Thema nennen: {s.Title}." + (s.Points.Count > 0 ? $" {s.Points[0]}." : "") + " Kurz sagen, was die Zuhörer mitnehmen.",
        DeckLayout.Abschnitt => $"Überleitung: Jetzt geht es um {s.Title}.",
        DeckLayout.Zitat => $"Zitat vorlesen, kurz Pause lassen, dann sagen, warum es zu „{s.Title}“ passt.",
        DeckLayout.Kennzahl => $"Jede Zahl einordnen: {string.Join("; ", s.Points)}. Was bedeutet das für die Zuhörer?",
        _ => $"{s.Title}: " + string.Join(" ", s.Points.Select(p => p.TrimEnd('.') + ".")),
    };

    static SlideShape Text(string role, double x, double y, double w, double h, string text, double max, SlideTheme t, Align a = Align.Left, bool bold = false, bool bullets = false, string? color = null, double min = 14) =>
        new() { Role = role, X = x, Y = y, W = w, H = h, Text = text, FontSize = FitSize(text, w, h, max, min, bullets).Size, Align = a, Bold = bold, Bullets = bullets, Color = color };

    static SlideShape Bar(double x, double y, double w, double h, string color) => new() { Kind = ShapeKind.Rechteck, X = x, Y = y, W = w, H = h, Fill = color };

    static Slide Compose(DeckSection s, DeckLayout layout, SlideTheme t, int index, List<DeckFinding> findings, Func<string, byte[]?>? loadImage)
    {
        var slide = new Slide();
        void Check(SlideShape shp) { if (!FitSize(shp.Text, shp.W, shp.H, shp.FontSize, 14, shp.Bullets).Fits) findings.Add(new(index + 1, "zu-lang", $"Folie {index + 1}: Text passt auch in 14 pt nicht ganz – bitte kürzen oder teilen.")); }
        void Title(double y = 1.0, double size = 34) { var ti = Text("title", 2.3, y, 29.2, 3.0, s.Title, size, t, bold: true); slide.Shapes.Add(ti); slide.Shapes.Add(Bar(2.3, y + 3.15, 3.2, 0.18, t.Accent)); }
        switch (layout)
        {
            case DeckLayout.Titel:
                slide.Layout = SlideLayout.Titel;
                slide.Shapes.Add(Text("title", 2.5, 5.6, 28.9, 4.6, s.Title, 48, t, Align.Center, true));
                slide.Shapes.Add(Bar(W / 2 - 2.5, 10.6, 5, 0.22, t.Accent));
                slide.Shapes.Add(Text("sub", 4.2, 11.3, 25.4, 2.6, s.Points.FirstOrDefault() ?? DateTime.Today.ToString("d. MMMM yyyy", De), 24, t, Align.Center));
                break;
            case DeckLayout.Abschnitt:
                slide.Layout = SlideLayout.NurTitel; slide.Background = t.Background2;
                slide.Shapes.Add(Bar(2.3, 7.0, 0.35, 5.0, t.Accent));
                slide.Shapes.Add(Text("title", 3.4, 7.0, 27, 5.0, s.Title, 44, t, bold: true));
                break;
            case DeckLayout.Stichpunkte:
            {
                slide.Layout = SlideLayout.TitelInhalt; Title();
                var body = Text("body", 2.3, 5.0, 29.2, 12.8, string.Join('\n', s.Points), 28, t, bullets: true); slide.Shapes.Add(body); Check(body);
                break;
            }
            case DeckLayout.ZweiSpalten:
            {
                slide.Layout = SlideLayout.ZweiSpalten; Title();
                var (left, right) = Columns(s.Points);
                var l = Text("body", 2.3, 5.0, 14.2, 12.8, string.Join('\n', left), 24, t, bullets: true);
                var r = Text("body2", 17.3, 5.0, 14.2, 12.8, string.Join('\n', right), 24, t, bullets: true);
                l.FontSize = r.FontSize = Math.Min(l.FontSize, r.FontSize);
                slide.Shapes.Add(l); slide.Shapes.Add(r); slide.Shapes.Add(Bar(16.85, 5.2, 0.06, 12.0, t.Accent)); Check(l); Check(r);
                break;
            }
            case DeckLayout.BildText:
            {
                slide.Layout = SlideLayout.Bild; Title();
                var bytes = s.Image is { } img && loadImage is not null ? loadImage(img) : null;
                if (bytes is { Length: > 0 }) slide.Shapes.Add(new SlideShape { Kind = ShapeKind.Bild, Image = bytes, X = 2.3, Y = 5.0, W = 14.5, H = 12.4 });
                else
                {
                    slide.Shapes.Add(new SlideShape { Kind = ShapeKind.Rechteck, Role = "pic", X = 2.3, Y = 5.0, W = 14.5, H = 12.4, Fill = Mix(t.Background, t.Accent, 0.15), Text = "Bild einfügen", Align = Align.Center, FontSize = 18, Color = t.Text });
                    if (s.Image is not null) findings.Add(new(index + 1, "bild", $"Folie {index + 1}: Bild „{Path.GetFileName(s.Image)}“ nicht gefunden – Platzhalter eingesetzt."));
                }
                var body = Text("body", 17.8, 5.0, 13.7, 12.4, string.Join('\n', s.Points), 24, t, bullets: true); slide.Shapes.Add(body); Check(body);
                break;
            }
            case DeckLayout.Zitat:
            {
                slide.Layout = SlideLayout.Leer;
                var quote = s.Points.FirstOrDefault() ?? s.Title; var source = "";
                var m = Regex.Match(quote, @"^(?<q>.*[“""»«’‘])\s*[–—-]\s*(?<s>.+)$");
                if (m.Success) { quote = m.Groups["q"].Value.Trim(); source = m.Groups["s"].Value.Trim(); }
                else if (s.Points.Count > 1) source = s.Points[1].TrimStart('–', '—', '-', ' ');
                slide.Shapes.Add(Text("title", 2.3, 0.9, 29.2, 2.2, s.Title, 24, t, color: t.Accent));
                slide.Shapes.Add(new SlideShape { X = 2.3, Y = 2.4, W = 4, H = 4, Text = "„", FontSize = 120, Color = t.Accent, Bold = true });
                var q = Text("body", 4.0, 5.2, 25.9, 7.8, quote, 40, t, Align.Center); slide.Shapes.Add(q); Check(q);
                if (source.Length > 0) slide.Shapes.Add(Text("sub", 4.0, 13.4, 25.9, 2.0, "– " + source, 22, t, Align.Center, color: t.Accent));
                break;
            }
            case DeckLayout.Kennzahl:
            {
                slide.Layout = SlideLayout.NurTitel; Title();
                var n = Math.Max(1, s.Points.Count); var colW = 29.2 / n;
                for (var k = 0; k < s.Points.Count; k++)
                {
                    var p = s.Points[k]; var fm = Figure.Match(p.Trim());
                    var num = fm.Success ? fm.Value.Trim().TrimEnd(':').Trim() : p; var label = fm.Success ? p.Trim()[fm.Length..].Trim().TrimStart(':', '–', '-').Trim() : "";
                    var x = 2.3 + k * colW;
                    slide.Shapes.Add(Text(k == 0 ? "body" : "zahl" + k, x, 6.0, colW - 0.6, 5.0, num, n == 1 ? 110 : n == 2 ? 88 : 66, t, Align.Center, true, color: t.Accent, min: 28));
                    if (label.Length > 0) slide.Shapes.Add(Text("text" + k, x, 11.4, colW - 0.6, 5.4, label, 22, t, Align.Center));
                }
                break;
            }
            case DeckLayout.Zeitleiste:
            {
                slide.Layout = SlideLayout.NurTitel; Title();
                var n = Math.Max(1, s.Points.Count); var step = 29.2 / n; const double lineY = 10.0;
                slide.Shapes.Add(Bar(2.3, lineY, 29.2, 0.14, t.Accent));
                for (var k = 0; k < s.Points.Count; k++)
                {
                    var p = s.Points[k].Trim(); var sep = Regex.Match(p, @"\s*(:|–|—|\s-)\s*");
                    var when = sep.Success ? p[..sep.Index].Trim() : p; var what = sep.Success ? p[(sep.Index + sep.Length)..].Trim() : "";
                    var cx = 2.3 + step * (k + 0.5);
                    slide.Shapes.Add(new SlideShape { Kind = ShapeKind.Ellipse, X = cx - 0.45, Y = lineY - 0.38, W = 0.9, H = 0.9, Fill = t.Accent });
                    slide.Shapes.Add(Text(k == 0 ? "body" : "wann" + k, cx - step / 2 + 0.2, 6.8, step - 0.4, 2.6, when, 24, t, Align.Center, true, color: t.Accent, min: 12));
                    if (what.Length > 0) slide.Shapes.Add(Text("was" + k, cx - step / 2 + 0.2, 11.2, step - 0.4, 6.0, what, 18, t, Align.Center, min: 11));
                }
                break;
            }
        }
        return slide;
    }

    /// <summary>Punkte auf zwei Spalten: „links | rechts“ je Zeile, Überschriften wie „Vorher:“/„Nachher:“ oder einfach halbiert.</summary>
    public static (List<string> Left, List<string> Right) Columns(IReadOnlyList<string> points)
    {
        if (points.Any(p => p.Contains(" | ", StringComparison.Ordinal)))
        {
            var l = new List<string>(); var r = new List<string>();
            foreach (var p in points) { var i = p.IndexOf(" | ", StringComparison.Ordinal); if (i < 0) l.Add(p); else { l.Add(p[..i].Trim()); r.Add(p[(i + 3)..].Trim()); } }
            return (l, r);
        }
        var heads = points.Select((p, i) => (p, i)).Where(x => ColumnHeads.Any(h => x.p.StartsWith(h, StringComparison.OrdinalIgnoreCase))).Select(x => x.i).ToList();
        if (heads.Count >= 2)
        {
            var cut = heads[1];
            return (points.Take(cut).ToList(), points.Skip(cut).ToList());
        }
        var half = (points.Count + 1) / 2;
        return (points.Take(half).ToList(), points.Skip(half).ToList());
    }

    /// <summary>Anweisung an die lokale KI: Gliederung in dem Format, das <see cref="Parse"/> versteht, mit Hinweisen für Zitat, Kennzahl und Zeitleiste.</summary>
    public static string Prompt(string topic, int slides, double minutes = 0) =>
        SlideOutline.Prompt(topic, slides) + "\n" +
        "Höchstens 6 Punkte je Folie, jeder Punkt höchstens 12 Wörter.\n" +
        "Für ein Zitat: ein Punkt in Anführungszeichen „…“ – Quelle. Für Kennzahlen: Punkte, die mit der Zahl beginnen, z. B. \"  - 42 % weniger Strom\".\n" +
        "Für eine Zeitleiste: Punkte, die mit Jahr oder Datum beginnen, z. B. \"  - 2025: Start\". Schreibe keine erfundenen Zahlen als Tatsachen – kennzeichne Schätzungen." +
        (minutes > 0 ? string.Create(CultureInfo.InvariantCulture, $"\nDie Redezeit ist {minutes:0} Minuten.") : "");

    // ---------- Umsortieren (T-0705: Folien in der Leiste ziehen) ----------

    /// <summary>Ist diese Zeile eine Folienzeile (gleiche Regel wie in <see cref="Parse"/>)?</summary>
    static bool IsHeader(string raw)
    {
        var line = raw.TrimEnd(); var t = line.TrimStart();
        if (t.Length == 0 || t is "---" or "```" || t.StartsWith("```", StringComparison.Ordinal) || t.StartsWith('>')) return false;
        if (t.StartsWith("Notiz:", StringComparison.OrdinalIgnoreCase) || t.StartsWith("Bild:", StringComparison.OrdinalIgnoreCase)) return false;
        var bullet = t.Length > 1 && t[0] is '-' or '•' or '*' or '–' && t[1] == ' ';
        return t.StartsWith('#') || (line.Length == t.Length && !bullet);
    }

    /// <summary>Gliederung in Blöcke je Abschnitt teilen (ein Block = Folienzeile mit ihren Punkten, Notizen, Bild) – jede Zeile bleibt Zeichen für Zeichen erhalten.
    /// Zeilen vor der ersten Folienzeile: eigener Block, wenn <see cref="Parse"/> daraus einen Abschnitt macht, sonst gehören sie zum ersten Block.</summary>
    public static List<string> Blocks(string outline)
    {
        var blocks = new List<List<string>>(); var lead = new List<string>();
        foreach (var l in (outline ?? "").Replace("\r\n", "\n").Split('\n'))
        {
            if (IsHeader(l)) blocks.Add([l]);
            else if (blocks.Count == 0) lead.Add(l);
            else blocks[^1].Add(l);
        }
        if (lead.Count > 0)
        {
            if (Parse(string.Join('\n', lead)).Count > 0 || blocks.Count == 0) blocks.Insert(0, lead);
            else blocks[0].InsertRange(0, lead);
        }
        return [.. blocks.Select(b => string.Join('\n', b))];
    }

    /// <summary>Abschnitt <paramref name="from"/> an die Stelle <paramref name="to"/> verschieben (0-basiert, Abschnitte wie in <see cref="Parse"/>).
    /// Jede Zeile bleibt, wie sie war; nur die Reihenfolge der Blöcke ändert sich.</summary>
    public static string MoveSection(string outline, int from, int to)
    {
        var blocks = Blocks(outline);
        if (from < 0 || from >= blocks.Count) throw new ArgumentOutOfRangeException(nameof(from), "Diesen Abschnitt gibt es nicht.");
        to = Math.Clamp(to, 0, blocks.Count - 1);
        if (from == to) return outline;
        var b = blocks[from]; blocks.RemoveAt(from); blocks.Insert(to, b);
        return string.Join('\n', blocks);
    }

    /// <summary>Abschnitt löschen (Folie entfernen). Der letzte Abschnitt bleibt.</summary>
    public static string RemoveSection(string outline, int index)
    {
        var blocks = Blocks(outline);
        if (index < 0 || index >= blocks.Count) throw new ArgumentOutOfRangeException(nameof(index), "Diesen Abschnitt gibt es nicht.");
        if (blocks.Count == 1) throw new InvalidOperationException("Die letzte Folie bleibt – sonst wäre die Gliederung leer.");
        blocks.RemoveAt(index);
        return string.Join('\n', blocks);
    }

    /// <summary>Zu welchem Abschnitt gehört Folie <paramref name="slide"/>? Ein Abschnitt mit vielen Punkten wird auf mehrere Folien verteilt –
    /// gezählt wird mit denselben Regeln wie beim Gestalten.</summary>
    public static int SectionOfSlide(string outline, DeckOptions? options, int slide)
    {
        var o = options ?? new DeckOptions(); var sections = Parse(outline);
        for (var i = 0; i < sections.Count; i++)
            if (slide < ApplyRules([.. sections.Take(i + 1)], o, []).Count) return i; // ApplyRules arbeitet der Reihe nach – der Anfang zählt gleich
        return Math.Max(0, sections.Count - 1);
    }

    /// <summary>Folienplan als Text: Folie, Art, Zeit, Titel, Notiz – für den Ausdruck neben dem Rednerpult.</summary>
    public static string PlanText(DeckResult r)
    {
        var sb = new StringBuilder(); var clock = Clock(r.Minutes);
        sb.Append(CultureInfo.InvariantCulture, $"Folienplan – {r.Doc.Slides.Count} Folien, {r.Minutes.Sum():0.#} min, Design „{r.Doc.Theme.Name}“ ({r.Doc.Theme.Font})\n\n");
        for (var i = 0; i < r.Doc.Slides.Count; i++)
        {
            var s = r.Doc.Slides[i];
            sb.Append(CultureInfo.InvariantCulture, $"{i + 1}. [{clock[i]}, {r.Minutes[i]:0.0} min] {Name(r.Layouts[i])}: {s.Title}\n");
            if (s.Notes.Length > 0) sb.Append("   Notiz: ").Append(s.Notes.Replace("\n", " ")).Append('\n');
        }
        if (r.Findings.Count > 0) { sb.Append("\nHinweise:\n"); foreach (var f in r.Findings) sb.Append("- ").Append(f.Message).Append('\n'); }
        return sb.ToString();
    }
}
