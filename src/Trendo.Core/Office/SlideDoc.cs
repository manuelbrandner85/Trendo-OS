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
    /// <summary>Text; jede Zeile ein Absatz. Bei <see cref="Bullets"/> wird jede Zeile ein Aufzählungspunkt, führende Tabs/zwei Leerzeichen rücken ein.</summary>
    public string Text { get; set; } = "";
    public bool Bullets { get; set; }
    public double FontSize { get; set; } = 24;
    public bool Bold { get; set; }
    /// <summary>Textfarbe #RRGGBB; null = aus dem Design.</summary>
    public string? Color { get; set; }
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
        s.Table = Table?.Select(r => r.ToList()).ToList(); s.ChartLabels = ChartLabels?.ToList(); s.ChartValues = ChartValues?.ToList();
        return s;
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

    public Slide Clone()
    {
        var s = new Slide { Layout = Layout, Notes = Notes, Transition = Transition, Background = Background };
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

    public SlideDoc Clone()
    {
        var d = new SlideDoc { Theme = Theme };
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
