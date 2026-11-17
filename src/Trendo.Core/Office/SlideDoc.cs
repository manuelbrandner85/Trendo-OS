using System.Globalization;

namespace Trendo.Core.Office;

/// <summary>P1: Folienlayouts wie in PowerPoint/Impress.</summary>
public enum SlideLayout { Titel, TitelInhalt, ZweiSpalten, Bild, NurTitel, Leer }

/// <summary>P3: Arten von Objekten auf einer Folie.</summary>
public enum ShapeKind { Text, Bild, Rechteck, Ellipse, Pfeil, Tabelle, Diagramm, Medien }

/// <summary>P4: Übergang beim Wechsel auf die Folie.</summary>
public enum Transition { Keine, Ueberblenden, Schieben, Wischen }

/// <summary>P4: Animation eines Objekts beim Klicken.</summary>
public enum Animation { Keine, Einblenden, Hereinfliegen, Zoomen }

/// <summary>Ein Objekt auf der Folie. Maße in Zentimetern, Folie 33,867 × 19,05 cm (16:9).</summary>
public sealed class SlideShape
{
    public ShapeKind Kind { get; set; } = ShapeKind.Text;
    public double X { get; set; }
    public double Y { get; set; }
    public double W { get; set; } = 10;
    public double H { get; set; } = 3;
    string _text = "";
    double _size = 24; bool _bold; string? _color;
    /// <summary>Text; jede Zeile ein Absatz. Bei <see cref="Bullets"/> wird jede Zeile ein Aufzählungspunkt, führende Tabs/zwei Leerzeichen rücken ein.
    /// T-0701: Hat das Feld mehrere Textformate (<see cref="Runs"/>), behält eine Änderung am Text die Formate der unveränderten Stellen.</summary>
    public string Text { get => _text; set { value ??= ""; if (Runs is { } r && value != _text) Runs = TextRuns.Adjust(r, _text, value); _text = value; } }
    public bool Bullets { get; set; }
    /// <summary>Schrift des ganzen Felds. Ändert man sie, gilt sie für den ganzen Text (eigene Größen einzelner Stellen fallen weg).</summary>
    public double FontSize { get => _size; set { if (value != _size) { if (Runs is { } r) Runs = TextRuns.Normalize(r.Select(x => x with { Size = null })); SizeInherited = false; } _size = value; } }
    public bool Bold { get => _bold; set { if (value != _bold) { if (Runs is { } r) Runs = TextRuns.Normalize(r.Select(x => x with { Bold = null })); BoldInherited = false; } _bold = value; } }
    /// <summary>Textfarbe #RRGGBB; null = aus dem Design.</summary>
    public string? Color { get => _color; set { if (value != _color && Runs is { } r) Runs = TextRuns.Normalize(r.Select(x => x with { Color = null })); _color = value; } }
    /// <summary>T-0701: mehrere Textformate in einem Feld – Abschnitte, die aneinandergereiht genau <see cref="Text"/> ergeben. Null = ein Format für alles.</summary>
    public List<TextRun>? Runs { get; set; }
    /// <summary>T-0701: Nummer des Objekts in der geöffneten PPTX (damit Animationen der Datei ihr Objekt wiederfinden).</summary>
    public int? SourceId { get; set; }
    /// <summary>T-0701: Platzhalter der Folienvorlage („title“, „body“ … und Nummer), damit das fremde Design beim Speichern greift.</summary>
    public string? PhType { get; set; }
    public string? PhIdx { get; set; }
    /// <summary>T-0701: Größe bzw. Fett kommen aus dem Folienmaster der Datei (dann schreibt Trendo sie nicht fest, damit das Design weiter greift).
    /// Ändert man sie in Trendo, gelten sie fest.</summary>
    public bool SizeInherited { get; set; }
    public bool BoldInherited { get; set; }
    /// <summary>Füllfarbe #RRGGBB; null = ohne.</summary>
    public string? Fill { get; set; }
    public Align Align { get; set; } = Align.Left;
    /// <summary>Platzhalterrolle „title“, „body“, „sub“ – für Layouts und PPTX.</summary>
    public string? Role { get; set; }
    public byte[]? Image { get; set; }
    /// <summary>Tabelle: Zeilen mit Zellen.</summary>
    public List<List<string>>? Table { get; set; }
    /// <summary>Diagramm: Art und Werte (eine Reihe) mit Beschriftungen.</summary>
    public ChartKind ChartKind { get; set; }
    public List<string>? ChartLabels { get; set; }
    public List<double>? ChartValues { get; set; }
    /// <summary>Medien: Pfad zu Video/Audio (wird bei der Vorführung abgespielt).</summary>
    public string? MediaPath { get; set; }
    public Animation Animation { get; set; }

    public SlideShape Clone()
    {
        var s = (SlideShape)MemberwiseClone();
        s.Table = Table?.Select(r => r.ToList()).ToList(); s.ChartLabels = ChartLabels?.ToList(); s.ChartValues = ChartValues?.ToList(); s.Runs = Runs?.ToList();
        return s;
    }

    /// <summary>T-0701: Teil des Texts anders formatieren (z. B. ein Wort fett). <paramref name="change"/> bekommt das bisherige Format des Abschnitts.</summary>
    public void FormatRange(int start, int length, Func<TextRun, TextRun> change)
    {
        start = Math.Clamp(start, 0, Text.Length); length = Math.Clamp(length, 0, Text.Length - start);
        if (length == 0) return;
        var chars = TextRuns.Expand(Runs ?? [new TextRun(Text)], Text);
        for (var i = start; i < start + length; i++) chars[i] = change(chars[i]);
        Runs = TextRuns.Collapse(chars, Text);
    }

    /// <summary>Format an einer Textstelle (für Knöpfe, die den Zustand zeigen).</summary>
    public TextRun RunAt(int pos)
    {
        if (Runs is null || Text.Length == 0) return new TextRun("");
        var chars = TextRuns.Expand(Runs, Text);
        return chars[Math.Clamp(pos, 0, chars.Count - 1)];
    }
}

/// <summary>T-0701: Abschnitt eines Textfelds mit eigenem Format. Null = wie das ganze Feld (<see cref="SlideShape.FontSize"/>, <see cref="SlideShape.Bold"/>, <see cref="SlideShape.Color"/>).
/// Unterstreichung wie in PowerPoint („sng“, „dbl“ …), Schriftart z. B. „Arial“ oder „+mn-lt“ (Schrift des Designs).</summary>
public sealed record TextRun(string Text, double? Size = null, bool? Bold = null, bool? Italic = null, string? Underline = null, string? Color = null, string? Font = null)
{
    /// <summary>Nur das Format (ohne Text). Methode statt Eigenschaft, damit ToString/Vergleich nicht endlos in sich selbst laufen.</summary>
    public TextRun Bare() => this with { Text = "" };
    public bool IsPlain() => Bare() == new TextRun("");
}

/// <summary>T-0701: Hilfen für Textabschnitte: Text ändern und Formate behalten, Abschnitte zusammenfassen.</summary>
public static class TextRuns
{
    /// <summary>Je Zeichen das Format (ohne Text).</summary>
    public static List<TextRun> Expand(IEnumerable<TextRun> runs, string text)
    {
        var list = new List<TextRun>(text.Length);
        foreach (var r in runs) for (var i = 0; i < r.Text.Length && list.Count < text.Length; i++) list.Add(r.Bare());
        var last = list.Count > 0 ? list[^1] : new TextRun("");
        while (list.Count < text.Length) list.Add(last);
        return list;
    }

    /// <summary>Gleiche Formate nebeneinander zu einem Abschnitt; null, wenn alles ohne eigenes Format ist.</summary>
    public static List<TextRun>? Collapse(List<TextRun> chars, string text)
    {
        var res = new List<TextRun>();
        for (var i = 0; i < text.Length;)
        {
            var j = i; while (j < text.Length && chars[j] == chars[i]) j++;
            res.Add(chars[i] with { Text = text[i..j] }); i = j;
        }
        return res.All(r => r.IsPlain()) ? null : res;
    }

    public static List<TextRun>? Normalize(IEnumerable<TextRun> runs)
    {
        var list = runs.Where(r => r.Text.Length > 0).ToList();
        var text = string.Concat(list.Select(r => r.Text));
        return Collapse(Expand(list, text), text);
    }

    /// <summary>Text von <paramref name="oldText"/> auf <paramref name="newText"/> ändern: Gleicher Anfang und gleiches Ende behalten ihr Format,
    /// neu Eingetipptes bekommt das Format der Stelle davor (wie in PowerPoint).</summary>
    public static List<TextRun>? Adjust(List<TextRun> runs, string oldText, string newText)
    {
        var chars = Expand(runs, oldText);
        var p = 0; while (p < oldText.Length && p < newText.Length && oldText[p] == newText[p]) p++;
        var s = 0; while (s < oldText.Length - p && s < newText.Length - p && oldText[^(s + 1)] == newText[^(s + 1)]) s++;
        // Ersetztes übernimmt das Format seines ersten Zeichens, Eingefügtes das Format der Stelle davor (wie in PowerPoint)
        var tmpl = chars.Count == 0 ? new TextRun("") : oldText.Length - s > p ? chars[p] : p > 0 ? chars[p - 1] : chars[0];
        var res = new List<TextRun>(newText.Length);
        res.AddRange(chars.Take(p));
        for (var i = p; i < newText.Length - s; i++) res.Add(tmpl);
        res.AddRange(chars.Skip(oldText.Length - s));
        return Collapse(res, newText);
    }
}

public sealed class Slide
{
    public SlideLayout Layout { get; set; } = SlideLayout.TitelInhalt;
    public List<SlideShape> Shapes { get; } = [];
    /// <summary>Notizen für die Referentenansicht.</summary>
    public string Notes { get; set; } = "";
    public Transition Transition { get; set; }
    /// <summary>Hintergrund #RRGGBB; null = aus dem Design.</summary>
    public string? Background { get; set; }
    /// <summary>T-0701: Folienlayout der geöffneten PPTX (Teil im Paket), damit Platzhalter und Design der Vorlage erhalten bleiben.</summary>
    public string? SourceLayout { get; set; }
    /// <summary>T-0701: Animationen, Übergang und Hintergrund der Datei, wie sie waren – werden unverändert zurückgeschrieben, solange in Trendo daran nichts geändert wurde.</summary>
    public string? SourceTiming { get; set; }
    public string? SourceAnimSig { get; set; }
    public string? SourceTransition { get; set; }
    public Transition SourceTransitionKind { get; set; }
    public string? SourceBg { get; set; }
    public string? SourceBgColor { get; set; }

    /// <summary>Animationen der Folie als Kennung (Objekt-Nummer und Art) – zeigt, ob in Trendo etwas daran geändert wurde.</summary>
    public string AnimSignature() => string.Join(';', Shapes.Where(s => s.Animation != Animation.Keine).Select(s => $"{s.SourceId}:{s.Animation}"));

    public Slide Clone()
    {
        var s = new Slide { Layout = Layout, Notes = Notes, Transition = Transition, Background = Background, SourceLayout = SourceLayout, SourceTiming = SourceTiming, SourceAnimSig = SourceAnimSig,
            SourceTransition = SourceTransition, SourceTransitionKind = SourceTransitionKind, SourceBg = SourceBg, SourceBgColor = SourceBgColor };
        s.Shapes.AddRange(Shapes.Select(x => x.Clone()));
        return s;
    }

    public SlideShape? ByRole(string role) => Shapes.FirstOrDefault(s => s.Role == role);
    public string Title => ByRole("title")?.Text.Split('\n')[0].Trim() is { Length: > 0 } t ? t : Shapes.FirstOrDefault(s => s.Kind == ShapeKind.Text && s.Text.Trim().Length > 0)?.Text.Split('\n')[0].Trim() ?? "";
}

/// <summary>P2: Design – Farben und Schrift.</summary>
public sealed record SlideTheme(string Id, string Name, string Background, string Title, string Text, string Accent, string Font = "Calibri", string? Background2 = null);

public sealed class SlideDoc
{
    public const double WidthCm = 33.867, HeightCm = 19.05;
    public List<Slide> Slides { get; } = [];
    public SlideTheme Theme { get; set; } = SlideThemes.All[0];
    /// <summary>T-0682: Was beim Öffnen einer fremden Datei nicht übernommen wurde (null = neu angelegt oder nicht geprüft).</summary>
    public ImportReport? Import { get; set; }
    /// <summary>T-0701: Design einer geöffneten PPTX (Folienmaster, Layouts, Theme samt Bildern). Solange <see cref="Theme"/> das daraus abgeleitete
    /// Design ist, schreibt Trendo diese Teile unverändert zurück, statt sie durch ein eigenes Design zu ersetzen.</summary>
    public PptxDesign? Design { get; set; }
    /// <summary>Wird das Design der Datei beim Speichern beibehalten?</summary>
    public bool KeepsDesign => Design is { } d && Theme == d.Theme;

    public SlideDoc Clone()
    {
        var d = new SlideDoc { Theme = Theme, Design = Design };
        d.Slides.AddRange(Slides.Select(s => s.Clone()));
        return d;
    }

    /// <summary>Neue Folie mit Platzhaltern des Layouts.</summary>
    public Slide AddSlide(SlideLayout layout, int? at = null)
    {
        var s = SlideLayouts.Create(layout);
        Slides.Insert(Math.Clamp(at ?? Slides.Count, 0, Slides.Count), s);
        return s;
    }

    public void Move(int from, int to)
    {
        if (from < 0 || from >= Slides.Count) return;
        to = Math.Clamp(to, 0, Slides.Count - 1);
        var s = Slides[from]; Slides.RemoveAt(from); Slides.Insert(to, s);
    }

    public static SlideDoc Empty() { var d = new SlideDoc(); d.AddSlide(SlideLayout.Titel); return d; }
}

/// <summary>T-0701: Design-Teile einer fremden PPTX. Wird nicht verändert; beim Speichern kopiert Trendo die Teile unverändert in die neue Datei.</summary>
public sealed class PptxDesign
{
    /// <summary>Aus den Farben und Schriften der Datei abgeleitetes Design für die Anzeige in Trendo.</summary>
    public required SlideTheme Theme { get; init; }
    /// <summary>Teile im Paket (Name ohne „/“ vorn) mit Inhalt: Folienmaster, Layouts, Theme, Bilder der Vorlage und ihre Beziehungen.</summary>
    public required IReadOnlyDictionary<string, byte[]> Parts { get; init; }
    /// <summary>Inhaltsart je Teil und je Dateiendung aus [Content_Types].xml.</summary>
    public required IReadOnlyDictionary<string, string> ContentTypes { get; init; }
    public required IReadOnlyDictionary<string, string> Defaults { get; init; }
    /// <summary>Folienmaster mit ihrer Kennung (sldMasterId).</summary>
    public required IReadOnlyList<(string Id, string Part)> Masters { get; init; }
    public string? ThemePart { get; init; }
    /// <summary>Foliengröße in EMU.</summary>
    public long Cx { get; init; } = 12192000;
    public long Cy { get; init; } = 6858000;
    public string? SizeType { get; init; }
    /// <summary>Standard-Textformate der Präsentation (p:defaultTextStyle) als XML.</summary>
    public string? DefaultTextStyle { get; init; }
    /// <summary>Layout je Art („title“, „obj“, „titleOnly“, „blank“, „twoObj“ …) für neue Folien.</summary>
    public required IReadOnlyDictionary<string, string> LayoutByType { get; init; }
    public string? FirstLayout { get; init; }
}

public static class SlideThemes
{
    public static readonly IReadOnlyList<SlideTheme> All =
    [
        new("klar", "Klar", "#FFFFFF", "#1F2937", "#374151", "#2F86F6"),
        new("nacht", "Nacht", "#0F172A", "#F8FAFC", "#CBD5E1", "#38BDF8", Background2: "#1E293B"),
        new("ozean", "Ozean", "#E0F2FE", "#0C4A6E", "#075985", "#0284C7"),
        new("wald", "Wald", "#F0FDF4", "#14532D", "#166534", "#16A34A"),
        new("sonne", "Sonne", "#FFFBEB", "#7C2D12", "#9A3412", "#F59E0B"),
        new("schiefer", "Schiefer", "#334155", "#FFFFFF", "#E2E8F0", "#F472B6", Background2: "#1F2937"),
    ];

    public static SlideTheme Get(string id) => All.FirstOrDefault(t => t.Id == id) ?? All[0];
}

/// <summary>P1: Platzhalter je Layout (Lage wie in den Standardlayouts von PowerPoint, 16:9).</summary>
public static class SlideLayouts
{
    public static readonly IReadOnlyList<(SlideLayout Layout, string Name)> Names =
    [
        (SlideLayout.Titel, "Titelfolie"), (SlideLayout.TitelInhalt, "Titel und Inhalt"), (SlideLayout.ZweiSpalten, "Zwei Spalten"),
        (SlideLayout.Bild, "Titel und Bild"), (SlideLayout.NurTitel, "Nur Titel"), (SlideLayout.Leer, "Leer"),
    ];

    public static string Name(SlideLayout l) => Names.First(n => n.Layout == l).Name;

    public static Slide Create(SlideLayout layout)
    {
        var s = new Slide { Layout = layout };
        SlideShape T(string role, double x, double y, double w, double h, double size, string text, bool bullets = false, Align a = Align.Left, bool bold = false) =>
            new() { Role = role, X = x, Y = y, W = w, H = h, FontSize = size, Text = text, Bullets = bullets, Align = a, Bold = bold };
        switch (layout)
        {
            case SlideLayout.Titel:
                s.Shapes.Add(T("title", 2.5, 6.2, 28.9, 4.2, 44, "Titel der Präsentation", a: Align.Center, bold: true));
                s.Shapes.Add(T("sub", 4.2, 10.8, 25.4, 2.9, 22, "Untertitel", a: Align.Center));
                break;
            case SlideLayout.TitelInhalt:
                s.Shapes.Add(T("title", 2.3, 1.0, 29.2, 3.4, 36, "Titel", bold: true));
                s.Shapes.Add(T("body", 2.3, 5.0, 29.2, 12.4, 22, "Erster Punkt\nZweiter Punkt\nDritter Punkt", bullets: true));
                break;
            case SlideLayout.ZweiSpalten:
                s.Shapes.Add(T("title", 2.3, 1.0, 29.2, 3.4, 36, "Titel", bold: true));
                s.Shapes.Add(T("body", 2.3, 5.0, 14.2, 12.4, 20, "Links: erster Punkt\nZweiter Punkt", bullets: true));
                s.Shapes.Add(T("body2", 17.3, 5.0, 14.2, 12.4, 20, "Rechts: erster Punkt\nZweiter Punkt", bullets: true));
                break;
            case SlideLayout.Bild:
                s.Shapes.Add(T("title", 2.3, 1.0, 29.2, 3.4, 36, "Titel", bold: true));
                s.Shapes.Add(new SlideShape { Kind = ShapeKind.Rechteck, Role = "pic", X = 6.9, Y = 5.0, W = 20, H = 12.4, Fill = "#E5E7EB", Text = "Bild einfügen", Align = Align.Center, FontSize = 18 });
                break;
            case SlideLayout.NurTitel:
                s.Shapes.Add(T("title", 2.3, 1.0, 29.2, 3.4, 36, "Titel", bold: true));
                break;
        }
        return s;
    }
}

/// <summary>P7: Präsentation aus Stichpunkten oder einem Text. Jede Zeile ohne Einrückung (oder mit „#“) wird eine Folie,
/// eingerückte Zeilen bzw. „-“/„•“/„*“ werden Aufzählungspunkte; „&gt;“ am Zeilenanfang wird Notiz. Die erste Folie wird Titelfolie, wenn sie keine Punkte hat.</summary>
public static class SlideOutline
{
    public static SlideDoc FromOutline(string outline, SlideTheme? theme = null)
    {
        var d = new SlideDoc { Theme = theme ?? SlideThemes.All[0] };
        (string Title, List<string> Points, List<string> Notes)? cur = null;
        var blocks = new List<(string Title, List<string> Points, List<string> Notes)>();
        foreach (var raw in outline.Replace("\r\n", "\n").Split('\n'))
        {
            if (raw.Trim().Length == 0) continue;
            var line = raw.TrimEnd(); var trimmed = line.TrimStart();
            var indented = line.Length > trimmed.Length;
            if (trimmed.StartsWith('>')) { cur?.Notes.Add(trimmed[1..].Trim()); continue; }
            var bullet = trimmed[0] is '-' or '•' or '*' or '–' && trimmed.Length > 1 && trimmed[1] == ' ';
            if (trimmed.StartsWith('#') || (!indented && !bullet))
            {
                cur = (trimmed.TrimStart('#').Trim().TrimEnd(':'), [], []); blocks.Add(cur.Value); continue;
            }
            var text = bullet ? trimmed[2..].Trim() : trimmed;
            var depth = (line.Length - trimmed.Length) >= 4 || line.StartsWith("\t\t", StringComparison.Ordinal) ? "\t" : "";
            if (cur is null) { cur = ("Präsentation", [], []); blocks.Add(cur.Value); }
            cur.Value.Points.Add(depth + text);
        }
        for (var i = 0; i < blocks.Count; i++)
        {
            var (title, points, notes) = blocks[i];
            Slide s;
            if (i == 0 && points.Count <= 1)
            {
                s = d.AddSlide(SlideLayout.Titel);
                s.ByRole("title")!.Text = title; s.ByRole("sub")!.Text = points.FirstOrDefault()?.Trim() ?? DateTime.Today.ToString("d. MMMM yyyy", CultureInfo.GetCultureInfo("de-DE"));
            }
            else if (points.Count == 0) { s = d.AddSlide(SlideLayout.NurTitel); s.ByRole("title")!.Text = title; }
            else
            {
                s = d.AddSlide(SlideLayout.TitelInhalt);
                s.ByRole("title")!.Text = title; s.ByRole("body")!.Text = string.Join('\n', points);
                if (points.Count > 7) s.ByRole("body")!.FontSize = 18;
            }
            s.Notes = string.Join('\n', notes);
        }
        if (d.Slides.Count == 0) d.AddSlide(SlideLayout.Titel);
        return d;
    }

    /// <summary>Anweisung an die lokale KI: Gliederung im Format, das <see cref="FromOutline"/> versteht.</summary>
    public static string Prompt(string topic, int slides) =>
        $"Erstelle die Gliederung einer Präsentation auf Deutsch zum Thema: {topic}\n" +
        $"Genau {Math.Clamp(slides, 3, 20)} Folien. Format, ohne weiteren Text:\n" +
        "Erste Zeile: Titel der Präsentation\n  - Untertitel\nDann je Folie eine Zeile mit dem Folientitel, darunter 3 bis 5 kurze Punkte, jeder mit \"  - \" am Anfang.\n" +
        "Optional eine Zeile mit \"> \" am Anfang als Sprechernotiz.";
}
