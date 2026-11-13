using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Trendo.Core;
using Trendo.Core.Film;
using Trendo.Core.Media;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>Film-Studio „Filme machen“ (STUDIO-FILM-01, freigegeben 08.11.2026), Teil 1: F1 Ablauf, F2 Drehbuch im offenen Fountain-Format, F3 Figuren, Orte und Shotliste aus dem Drehbuch, F4 Figuren-Mappe.
/// Kurzfilme und Trailer: Die Länge eines Clips begrenzt die Video-KI, das Studio rechnet deshalb in Einstellungen von höchstens wenigen Sekunden.</summary>
public partial class MainWindow
{
    const string FilmKey = "film-programm";
    string _filmStep = "drehbuch";

    const string FilmSample = """
Titel: Der letzte Zug

INNEN. BAHNHOF – NACHT

Der Bahnsteig ist leer. Eine Uhr tickt. Eine Frau sitzt allein auf der Bank.

JONA
(leise)
Ist er schon weg?

BAHNWÄRTER
Der letzte Zug fährt um Mitternacht.

AUSSEN. GLEIS – NACHT

Nebel zieht über die Schienen. Ein Licht kommt langsam näher.

JONA
Dann bleibt mir nur noch eins.
""";

    static JsonObject FilmDoc(string logline, string genre, string script) => new() { ["logline"] = logline, ["genre"] = genre, ["script"] = script, ["figures"] = new JsonArray(), ["places"] = new JsonArray() };
    static FountainScript FilmParsed(ProgramDocument d) => Fountain.Parse(d.Text("script"));
    static FilmBreakdown FilmOf(ProgramDocument d) => FilmBreakdowns.Of(FilmParsed(d));

    private void InitFilm() => RegisterProgram(FachTabs, new ProgramSpec(FilmKey, "STUDIO-FILM-01", "Filme machen")
    {
        Sentence = "Von der Idee zum Kurzfilm oder Trailer: Drehbuch schreiben, Figuren festlegen, Einstellungen planen.",
        Folder = "Filme",
        FillHeight = true,
        Templates =
        [
            new("leer", "Leeres Drehbuch", "Mit dem ersten Szenenkopf anfangen", () => FilmDoc("", "", "Titel: Neuer Film\n\nINNEN. ORT – TAG\n\n")),
            new("kurzfilm", "Beispiel-Kurzfilm", "Zwei Szenen zum Ausprobieren", () => FilmDoc("Eine Frau wartet auf den letzten Zug und auf eine Entscheidung.", "Drama", FilmSample)),
        ],
        Ribbon =
        [
            ("Planen", [
                new ProgramCmd("film-shots", "Einstellungen vorschlagen", d => { d.Set("script", Fountain.InsertSuggestedShots(d.Text("script")), "Einstellungen vorgeschlagen"); _filmStep = "shots"; }, Primary: true, Tip: "Füllt nur Szenen ohne Einstellungen, vorhandene bleiben."),
                new ProgramCmd("film-sync", "Figuren & Orte übernehmen", d => { FilmSync(d); _filmStep = "figuren"; }, Tip: "Trägt neue Figuren und Orte aus dem Drehbuch in die Mappe ein."),
            ]),
        ],
        Exports =
        [
            new("fountain", "Drehbuch (.fountain)", ".fountain", (d, f) => { File.WriteAllText(f, d.Text("script")); return f; }),
            new("pdf", "Drehbuch (PDF)", ".pdf", FilmPdf),
            new("csv", "Shotliste (CSV)", ".csv", (d, f) => { File.WriteAllText(f, ShotPlan.Csv(ShotPlan.Parse(FilmParsed(d).PlanText)), new System.Text.UTF8Encoding(true)); return f; }),
            new("report", "Drehbuchauszug", ".md", (d, f) => { File.WriteAllText(f, FilmBreakdowns.Report(d.Title, FilmOf(d))); return f; }),
        ],
        Workspace = FilmWorkspace,
        Properties = FilmProperties,
        Status = d => { var b = FilmOf(d); return $"{b.Scenes.Count} Szenen · {b.Roles.Count} Figuren · {b.ShotCount} Einstellungen · etwa {(b.Seconds / 60).ToString("0.0", De)} Minuten"; },
        Help = "Schreib links das Drehbuch: Szenenkopf wie „INNEN. KÜCHE – TAG“, Figurennamen in Großbuchstaben, darunter was sie sagen. Rechts siehst du es als fertige Seite. Einstellungen schreibst du als „- Totale: Beschreibung (4 s)“.",
    }, "film", "filme", "kurzfilm", "kinofilm", "spielfilm", "trailer", "drehbuch", "storyboard", "shotliste", "fountain", "regie");

    string FilmPdf(ProgramDocument d, string file)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(file))!;
        var exe = ToolExe("typst") ?? throw new InvalidOperationException("Für das PDF fehlt das Werkzeug typst. Du richtest es in „Texte & Tabellen“ mit einem Klick ein.");
        var typ = Path.ChangeExtension(file, ".typ");
        File.WriteAllText(typ, Fountain.ToTypst(FilmParsed(d), d.Title));
        var (ok, msg) = TypstDoc.Compile(exe, typ, file, dir, run: ToolRunOverride);
        if (!ok) throw new InvalidOperationException("Das PDF konnte nicht gesetzt werden: " + msg);
        File.Delete(typ);
        return file;
    }

    /// <summary>Neue Figuren und Orte aus dem Drehbuch in die Mappe; vorhandene Einträge und ihre Beschreibungen bleiben.</summary>
    static void FilmSync(ProgramDocument d)
    {
        var b = FilmOf(d);
        d.Edit("Figuren und Orte übernommen", x =>
        {
            void Merge(string key, IEnumerable<string> names)
            {
                if (x[key] is not JsonArray list) x[key] = list = [];
                var have = list.OfType<JsonObject>().Select(o => (string?)o["name"]).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var n in names) if (have.Add(n)) list.Add(new JsonObject { ["name"] = n, ["look"] = "" });
            }
            Merge("figures", b.Roles.Select(r => r.Name));
            Merge("places", b.Locations.Select(l => l.Name));
        });
    }

    private Control FilmWorkspace(ProgramDocument d)
    {
        var steps = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(4, 4, 4, 10), [DockPanel.DockProperty] = Avalonia.Controls.Dock.Top };
        foreach (var (id, label) in new[] { ("drehbuch", "1 Drehbuch"), ("figuren", "2 Figuren & Orte"), ("shots", "3 Einstellungen") })
        {
            var b = new Button { Name = "film-step-" + id, Content = label, Padding = new Thickness(14, 6), FontWeight = _filmStep == id ? FontWeight.SemiBold : FontWeight.Normal };
            if (_filmStep == id) b.Classes.Add("accent");
            b.Click += (_, _) => { _filmStep = id; Show(FilmKey); };
            steps.Children.Add(b);
        }
        // Die Schritte bleiben stehen, nur der Inhalt darunter scrollt
        Control body = _filmStep switch { "figuren" => new ScrollViewer { Content = FilmFigures(d) }, "shots" => new ScrollViewer { Content = FilmShots(d) }, _ => FilmScript(d) };
        return new DockPanel { Name = "film-workspace", Children = { steps, body } };
    }

    Control FilmScript(ProgramDocument d)
    {
        var preview = new StackPanel { Name = "film-preview", Spacing = 3 };
        void Render(string text)
        {
            preview.Children.Clear();
            foreach (var l in Fountain.Parse(text).Lines)
            {
                var t = new TextBlock { Text = l.Text, TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse("#1F2933"), FontFamily = new FontFamily("Consolas, Courier New, monospace"), FontSize = 13 };
                switch (l.Kind)
                {
                    case FountainKind.Heading: t.FontWeight = FontWeight.Bold; t.Margin = new Thickness(0, 12, 0, 4); break;
                    case FountainKind.Character: t.Margin = new Thickness(170, 8, 0, 0); break;
                    case FountainKind.Parenthetical: t.Margin = new Thickness(130, 0, 90, 0); break;
                    case FountainKind.Dialogue: t.Margin = new Thickness(90, 0, 60, 0); break;
                    case FountainKind.Transition: t.HorizontalAlignment = HorizontalAlignment.Right; break;
                    case FountainKind.Shot: t.Foreground = Brush.Parse("#8A6D1F"); t.FontSize = 11; t.Text = "" + l.Text; break;
                    case FountainKind.Section or FountainKind.Synopsis: t.Foreground = Brush.Parse("#7B8794"); t.FontStyle = FontStyle.Italic; break;
                }
                preview.Children.Add(t);
            }
        }
        var box = new TextBox { Name = "film-script", Text = d.Text("script"), AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Consolas, Courier New, monospace"), FontSize = 13, VerticalContentAlignment = VerticalAlignment.Top, PlaceholderText = "INNEN. KÜCHE – TAG" };
        Render(d.Text("script"));
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        var typed = false; // nur getippten Text übernehmen: Beim Neuzeichnen nach einem Befehl darf das alte Feld den neuen Stand nicht überschreiben
        void Commit() { timer.Stop(); if (typed && (box.Text ?? "") != d.Text("script")) d.Set("script", box.Text ?? "", "Drehbuch geändert"); typed = false; }
        timer.Tick += (_, _) => Commit();
        box.TextChanged += (_, _) => { Render(box.Text ?? ""); typed = (box.Text ?? "") != d.Text("script"); timer.Stop(); if (typed) timer.Start(); };
        box.LostFocus += (_, _) => Commit();
        box.DetachedFromVisualTree += (_, _) => Commit();
        var page = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(6), Padding = new Thickness(26, 20), BoxShadow = BoxShadows.Parse("0 6 24 0 #66000000"), Child = new ScrollViewer { Content = preview } };
        return new Grid { ColumnDefinitions = new ColumnDefinitions("*,14,*"), Margin = new Thickness(4, 0, 10, 4), Children = { box, Pos(page, 2) } };
    }

    Control FilmFigures(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "film-figures", Spacing = 8, Margin = new Thickness(4, 0, 10, 4) };
        sp.Children.Add(P("Beschreibe jede Figur und jeden Ort genau. Daraus entstehen später die Bilder, darum immer gleiche Angaben: Alter, Haare, Kleidung, Licht. Nur erfundene Figuren, keine echten Personen.", Muted, 12));
        void Group(string key, string title, string placeholder)
        {
            sp.Children.Add(H(title, 14));
            var list = d.List(key).OfType<JsonObject>().ToList();
            if (list.Count == 0)
            {
                var b = new Button { Name = "film-sync-" + key, Content = "Aus dem Drehbuch übernehmen", Padding = new Thickness(12, 5) };
                b.Click += (_, _) => ProgramRun(FilmKey, () => FilmSync(d));
                sp.Children.Add(b);
                return;
            }
            for (var i = 0; i < list.Count; i++)
            {
                var index = i; var name = (string?)list[i]["name"] ?? "";
                var box = new TextBox { Name = $"film-{key}-look-{i}", Text = (string?)list[i]["look"] ?? "", PlaceholderText = placeholder, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 54 };
                box.LostFocus += (_, _) =>
                {
                    if ((box.Text ?? "") != ((string?)list[index]["look"] ?? "")) d.Edit("Beschreibung geändert", x => { if (x[key] is JsonArray a && a[index] is JsonObject o) o["look"] = box.Text ?? ""; });
                };
                sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("170,*"), Children = { new TextBlock { Text = name, Foreground = Text, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap }, Pos(box, 1) } });
            }
        }
        Group("figures", "Figuren", "z. B. Frau, etwa 35, kurze dunkle Haare, grauer Mantel");
        Group("places", "Orte", "z. B. alter Bahnhof, kaltes Neonlicht, nasser Boden");
        return sp;
    }

    Control FilmShots(ProgramDocument d)
    {
        var b = FilmOf(d);
        var sp = new StackPanel { Name = "film-shots", Spacing = 6, Margin = new Thickness(4, 0, 10, 4) };
        sp.Children.Add(P($"{b.ShotCount} Einstellungen. Eine KI-Einstellung ist höchstens etwa 5 Sekunden lang. Längere Einstellungen werden aus mehreren Clips zusammengesetzt.", Muted, 12));
        foreach (var p in b.Problems) sp.Children.Add(P("" + p, Brush.Parse("#F5A524"), 12));
        foreach (var s in b.Scenes)
        {
            sp.Children.Add(H($"{s.Code} · {s.Place}. {s.Location}{(s.Time.Length > 0 ? " – " + s.Time : "")}", 13));
            if (s.Shots.Count == 0) { sp.Children.Add(P("Noch keine Einstellungen. Oben auf „Einstellungen vorschlagen“ klicken oder selbst ins Drehbuch schreiben.", Muted, 12)); continue; }
            foreach (var x in s.Shots)
                sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("90,110,*,60"), Children = {
                    new TextBlock { Text = x.Id, Foreground = Muted, FontSize = 12 },
                    Pos(new TextBlock { Text = x.Type, Foreground = Text, FontSize = 12 }, 1),
                    Pos(new TextBlock { Text = x.Description, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap }, 2),
                    Pos(new TextBlock { Text = x.Seconds.ToString("0.#", De) + " s", Foreground = Muted, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Right }, 3) } });
        }
        return sp;
    }

    private Control FilmProperties(ProgramDocument d)
    {
        var sp = new StackPanel { Spacing = 4 };
        void Txt(string key, string label, bool multi = false)
        {
            var t = new TextBox { Name = "film-p-" + key, Text = d.Text(key), PlaceholderText = label, AcceptsReturn = multi, TextWrapping = multi ? TextWrapping.Wrap : TextWrapping.NoWrap, MinHeight = multi ? 70 : 0 };
            t.LostFocus += (_, _) => { if ((t.Text ?? "") != d.Text(key)) d.Set(key, t.Text ?? "", label); };
            sp.Children.Add(P(label, null, 11)); sp.Children.Add(t);
        }
        Txt("logline", "Worum geht es, in einem Satz?", true);
        Txt("genre", "Genre");
        var b = FilmOf(d);
        sp.Children.Add(H("Figuren", 13));
        foreach (var r in b.Roles.Take(12)) sp.Children.Add(P($"{r.Name}: {r.Speeches} Reden, {r.Words} Wörter", Text, 12));
        sp.Children.Add(H("Orte", 13));
        foreach (var l in b.Locations.Take(12)) sp.Children.Add(P($"{l.Name}: {l.Scenes} Szene(n)", Text, 12));
        return sp;
    }
}
