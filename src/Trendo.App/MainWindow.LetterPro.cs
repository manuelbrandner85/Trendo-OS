using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Office;
using Trendo.Core.Sys;

namespace Trendo.App;

/// <summary>Briefe schreiben (STUDIO-LETTER-01), Vorschläge Teil 4: F1 Vorlagen-Galerie, F2 Fristenrechner, F3 Serienbrief, F4 Unterschrift,
/// U1 DIN-A4-Vorschau im Maßstab, U2 Felder in der Vorschau anklicken, U3 Schrittleiste.</summary>
public partial class MainWindow
{
    private bool? _letterGallery;
    /// <summary>Brief-Assistent (02.11.2026): aktueller Schritt 1–3; „Alles auf einer Seite“ zeigt alle Schritte.</summary>
    private int _letterStep = 1;
    private bool _letterAll;
    public int LetterStep => _letterStep;
    /// <summary>Assistent auf Schritt 1–3 stellen (Bildschirmfotos, Tests).</summary>
    public void LetterGoStep(int n) { _letterStep = Math.Clamp(n, 1, 3); _letterAll = false; Show("brief"); }
    private bool _letterSaved;
    private Action? _letterStepSync;
    private string _letterDl = "";
    private List<LetterDraft>? _letterSerial; private int _letterSerialIdx;
    public IReadOnlyList<LetterDraft>? LetterSerialPreview => _letterSerial;

    private bool LetterGalleryOpen(LetterDraft d) => _letterGallery ?? (d.Category == "frei" && d.Body.Trim().Length == 0);

    /// <summary>F1: Vorlagen nach Anlass, mit Betreff als Vorschau.</summary>
    private Control LetterGallery(LetterDraft d)
    {
        var sp = new StackPanel { Name = "letter-gallery", Spacing = 6 };
        foreach (var g in Letters.Groups)
        {
            var cats = Letters.Categories.Where(c => Letters.GroupOf(c) == g).ToList();
            if (cats.Count == 0) continue;
            sp.Children.Add(new TextBlock { Text = g, Foreground = Muted, FontSize = 11.5, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 4, 0, 0) });
            var wrap = new WrapPanel();
            foreach (var c in cats)
            {
                var card = new StackPanel { Spacing = 3, Children = {
                    new TextBlock { Text = c.Label, Foreground = Text, FontSize = 12.5, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                    new TextBlock { Text = c.Subject.Length > 0 ? c.Subject : "Ohne Vorlage – du schreibst alles selbst.", Foreground = Muted, FontSize = 10.5, TextWrapping = TextWrapping.Wrap, MaxHeight = 30 } } };
                if (c.Legal) card.Children.Add(new TextBlock { Text = "Fristen selbst prüfen", Foreground = Brush.Parse("#FACC15"), FontSize = 10 });
                var b = new Button { Name = "letter-gallery-" + c.Id, Width = 172, MinHeight = 74, Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(10, 8), CornerRadius = new CornerRadius(10),
                    HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Top, Background = c.Id == d.Category ? Accent : TileBg, BorderBrush = Edge, BorderThickness = new Thickness(1), Content = card };
                b.Click += (_, _) =>
                {
                    Letters.ApplyTemplate(d, c.Id); _letterGallery = false; if (!_letterAll && _letterStep == 1) _letterStep = 2; // Assistent: nach der Vorlage weiter zu „An wen?“
                    _letterMsg = c.Id == "frei" ? "Freier Brief: ohne Vorlage. Betreff und Text schreibst du selbst." : $"Vorlage „{c.Label}“ eingesetzt. Passe die eckigen Klammern an.";
                    Show("brief");
                };
                wrap.Children.Add(b);
            }
            sp.Children.Add(wrap);
        }
        return sp;
    }

    /// <summary>F2–F4: Frist berechnen, Serienbrief, Unterschrift.</summary>
    private Control LetterTools(LetterDraft d)
    {
        const string V = "brief";
        Border Box(string title, string help, params Control[] c)
        {
            var sp = new StackPanel { Spacing = 5, Children = { new TextBlock { Text = title, Foreground = Text, FontSize = 13, FontWeight = FontWeight.SemiBold }, P(help, null, 11) } };
            foreach (var x in c) sp.Children.Add(x);
            return new Border { Background = TileBg, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 8), Margin = new Thickness(0, 6, 0, 0), Child = sp };
        }
        WrapPanel Row(params Control[] c) { var w = new WrapPanel(); foreach (var x in c) { x.Margin = new Thickness(0, 0, 6, 6); x.VerticalAlignment = VerticalAlignment.Center; w.Children.Add(x); } return w; }
        Button B(string name, string label, Action go, bool accent = false) { var b = new Button { Name = name, Content = label, FontSize = 12, Padding = new Thickness(10, 4), Background = accent ? Accent : Brushes.Transparent, BorderBrush = Edge, BorderThickness = new Thickness(1) }; b.Click += (_, _) => go(); return b; }
        var de = System.Globalization.CultureInfo.GetCultureInfo("de-DE");

        // F2 Fristenrechner
        var start = new TextBox { Name = "letter-dl-start", Text = DateTime.Today.ToString("dd.MM.yyyy", de), Width = 110, PlaceholderText = "TT.MM.JJJJ" };
        var amount = new TextBox { Name = "letter-dl-amount", Text = "14", Width = 60 };
        var unit = new ComboBox { Name = "letter-dl-unit", ItemsSource = new[] { "Tage", "Wochen", "Monate" }, SelectedIndex = 0 };
        var outText = new TextBlock { Name = "letter-dl-out", Text = _letterDl, Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap };
        DateTime? Calc()
        {
            if (!DateTime.TryParseExact(start.Text?.Trim(), "d.M.yyyy", de, System.Globalization.DateTimeStyles.None, out var s)) { outText.Text = "Startdatum bitte als TT.MM.JJJJ."; return null; }
            if (!int.TryParse(amount.Text?.Trim(), out var n) || n is < 1 or > 1000) { outText.Text = "Bitte eine Zahl von 1 bis 1000 eintragen."; return null; }
            var (date, shifted) = Letters.Deadline(s, n, (string)unit.SelectedItem!);
            _letterDl = outText.Text = $"Frist endet am {date.ToString("dddd, dd.MM.yyyy", de)}{(shifted ? " (fiel aufs Wochenende → nächster Werktag)" : "")}. Feiertage und Fristbeginn bitte selbst prüfen.";
            return date;
        }
        var dl = Box("Frist berechnen", "Ab wann läuft die Frist, wie lang ist sie? Das Ergebnis kannst du in den Brief einsetzen oder in den Kalender eintragen.",
            Row(start, P("+"), amount, unit, B("letter-dl-calc", "Berechnen", () => Calc(), true)), outText,
            Row(B("letter-dl-insert", "In den Brief einsetzen", () => { if (Calc() is { } x) { _letterMsg = Letters.FillDate(d, x) ? "Datum eingesetzt." : "Im Brief steht kein [Datum] zum Ersetzen."; Show(V); } }),
                B("letter-dl-cal", "In den Kalender", () =>
                {
                    if (Calc() is not { } x) return;
                    Cal.Add(new CalEvent(Guid.NewGuid().ToString("N"), x, x.AddDays(1), "Frist: " + (d.Subject.Trim().Length > 0 ? d.Subject.Trim() : "Brief"), AllDay: true, RemindMin: 2 * 24 * 60));
                    _letterMsg = $"Im Kalender eingetragen: {x:dd.MM.yyyy}, mit Erinnerung zwei Tage vorher."; Show(V);
                })));

        // F3 Serienbrief
        var sf = new TextBox { Name = "letter-serial-file", PlaceholderText = "Tabelle mit Empfängern (XLSX, ODS oder CSV)", MinWidth = 260 };
        var serial = Box("Serienbrief", "Ein Brief je Zeile der Tabelle. Felder wie {Name} oder «Name» im Text werden ersetzt. Bleibt der Empfänger leer, setzt Trendo die Anschrift aus den Spalten Firma, Name (oder Vorname/Nachname), Straße, PLZ und Ort zusammen.",
            Row(sf, B("letter-serial-run", "Serienbrief erstellen", () => LetterTry(() =>
            {
                var file = Clean(sf);
                if (!File.Exists(file)) throw new ArgumentException("Bitte eine vorhandene Tabelle wählen.");
                var doc = SheetFiles.Open(file); var s = doc.Sheets[0]; var calc = new SheetCalc(doc);
                var cols = Math.Max(0, s.MaxCol) + 1;
                var headers = Enumerable.Range(0, cols).Select(c => calc.Display(s, 0, c).Trim()).ToList();
                var rows = Enumerable.Range(1, Math.Max(0, s.MaxRow)).Select(r => (IReadOnlyList<string>)Enumerable.Range(0, cols).Select(c => calc.Display(s, r, c)).ToList()).ToList();
                var tpl = d.Copy(); if (tpl.Recipient.Trim().Length == 0) tpl.Recipient = "";
                var missing = Letters.SerialFields(tpl).Where(f => !headers.Contains(f, StringComparer.CurrentCultureIgnoreCase)).ToList();
                if (missing.Count > 0) throw new ArgumentException("Diese Felder gibt es in der Tabelle nicht: " + string.Join(", ", missing));
                var letters = Letters.Serial(tpl, headers, rows);
                if (letters.Count == 0) throw new ArgumentException("Die Tabelle hat keine Datenzeilen.");
                var bad = letters.Select((l, i) => (i, Letters.Validate(l).FirstOrDefault(x => x.Blocking))).FirstOrDefault(x => x.Item2 is not null);
                if (bad.Item2 is { } issue) throw new ArgumentException($"Brief {bad.i + 1}: {issue.Field} – {issue.Message}");
                var dir = System.IO.Path.Combine(ProjectsRoot, "Briefe", $"Serienbrief-{DateTime.Now:yyyyMMdd-HHmm}"); Directory.CreateDirectory(dir);
                for (var i = 0; i < letters.Count; i++)
                {
                    var stem = new string(letters[i].Recipient.Split('\n')[0].Where(ch => !System.IO.Path.GetInvalidFileNameChars().Contains(ch)).ToArray()).Trim();
                    Letters.WriteDocx(letters[i], System.IO.Path.Combine(dir, $"{i + 1:000}-{(stem.Length > 0 ? stem : "Brief")}.docx"));
                }
                _core.Audit.Append(Principal.LocalOwner.Name, "letter.serial", $"{letters.Count} Briefe");
                _letterMsg = $"{letters.Count} Briefe gespeichert in {dir}"; Show(V); NotifyFileResult(_letterMsg);
            }), true)));

        // Masterprompt 131: Empfänger aus dem Adressbuch
        var people = Mail.Contacts().Where(c => c.Street.Trim().Length > 0 || c.City.Trim().Length > 0 || c.Company.Trim().Length > 0).ToList();
        var pick = new ComboBox { Name = "letter-contact-pick", ItemsSource = people.Select(c => c.Name + (c.City.Length > 0 ? " · " + c.City : "")).ToList(), PlaceholderText = people.Count == 0 ? "Im Adressbuch hat noch niemand eine Anschrift" : "Kontakt wählen …", MinWidth = 260 };
        var book = Box("Empfänger aus dem Adressbuch", "Setzt Anschrift und eine passende Anrede ein („Guten Tag …“ bzw. „Sehr geehrte Damen und Herren“ bei Firmen).",
            Row(pick, B("letter-contact-use", "Einsetzen", () => LetterTry(() =>
            {
                if (pick.SelectedIndex < 0) throw new ArgumentException("Bitte einen Kontakt wählen.");
                var c = people[pick.SelectedIndex]; d.Recipient = Letters.RecipientOf(c);
                if (d.Salutation.Trim() is "" or "Sehr geehrte Damen und Herren,") d.Salutation = Letters.SalutationFor(c);
                _letterMsg = $"Empfänger „{c.Name}“ eingesetzt."; Show(V);
            }), true)));
        // Briefform und Informationsblock (DIN 5008)
        var msgDate = new TextBox { Name = "letter-your-message", Text = d.YourMessage, PlaceholderText = "Ihre Nachricht vom (z. B. 28.10.2026)", Width = 200 };
        var ourRef = new TextBox { Name = "letter-our-ref", Text = d.OurReference, PlaceholderText = "Unser Zeichen", Width = 150 };
        msgDate.TextChanged += (_, _) => { d.YourMessage = msgDate.Text ?? ""; _letterSync?.Invoke(); };
        ourRef.TextChanged += (_, _) => { d.OurReference = ourRef.Text ?? ""; _letterSync?.Invoke(); };
        var formToggle = B(d.Form == "A" ? "letter-form-b" : "letter-form-a", d.Form == "A" ? "Jetzt Form A – zu Form B wechseln" : "Jetzt Form B – zu Form A wechseln", () => { d.Form = d.Form == "A" ? "B" : "A"; Show(V); }, true);
        // F3 Briefe: Druckoptionen – Falz- und Lochmarken (DIN 5008) im DOCX/PDF und in der Vorschau
        var marks = new CheckBox { Name = "letter-print-marks", Content = "Falz- und Lochmarken mitdrucken", IsChecked = d.PrintMarks, Foreground = Text };
        ToolTip.SetTip(marks, "Kleine Striche am linken Rand: zum Falten (Form B bei 105 und 210 mm) und Lochen (148,5 mm). Für Fensterumschläge empfohlen.");
        marks.IsCheckedChanged += (_, _) =>
        {
            d.PrintMarks = marks.IsChecked == true; _letterSync?.Invoke();
            LetterNote(d.PrintMarks ? "Falz- und Lochmarken werden mitgedruckt." : "Ohne Falz- und Lochmarken – der Brief wird ohne Striche am Rand gedruckt.");
        };
        var form = Box("Briefform, Informationsblock und Druck", "Form B: Anschrift ab 45 mm (üblich). Form A: ab 27 mm, für Briefbögen mit kleinem Kopf. Die Angaben erscheinen rechts neben der Anschrift.",
            Row(formToggle), Row(msgDate, ourRef), marks,
            P("Papierschacht und Drucker wählst du beim Drucken im Druckdialog von Windows (PDF öffnen → Drucken).", null, 11));
        // Serienbrief an eine Adressbuch-Gruppe
        var groups = new List<string> { "Alle Kontakte", "Favoriten" }; groups.AddRange(Mail.GroupNames().Select(g => g.Name));
        var grp = new ComboBox { Name = "letter-serial-group", ItemsSource = groups, SelectedIndex = 0, MinWidth = 180 };
        List<Contact> GroupContacts() => grp.SelectedIndex switch { 0 => Mail.Contacts().ToList(), 1 => Mail.Contacts().Where(c => c.Favorite).ToList(), _ => Mail.Search("", (string)grp.SelectedItem!).ToList() };
        var serialBook = Box("Serienbrief an das Adressbuch", "Ein Brief je Kontakt der Gruppe (mit Anschrift). Felder wie {Vorname}, {Nachname}, {Firma} oder {Ort} werden ersetzt. Erst ansehen, dann speichern.",
            Row(grp, B("letter-serial-group-preview", "Vorschau", () => LetterTry(() =>
            {
                var skipped = new List<string>(); var list = Letters.SerialFromContacts(d, GroupContacts(), skipped);
                if (list.Count == 0) throw new ArgumentException("In dieser Gruppe hat niemand eine vollständige Anschrift (Straße und Ort).");
                _letterSerial = list; _letterSerialIdx = 0;
                _letterMsg = $"{list.Count} Briefe – mit über der Vorschau durchblättern." + (skipped.Count > 0 ? $" Ohne Anschrift übersprungen: {string.Join(", ", skipped)}." : ""); Show(V);
            })), B("letter-serial-group-run", "Alle als DOCX speichern", () => LetterTry(() =>
            {
                var list = _letterSerial ?? Letters.SerialFromContacts(d, GroupContacts());
                if (list.Count == 0) throw new ArgumentException("Keine Empfänger mit Anschrift in dieser Gruppe.");
                var bad = list.Select((l, i) => (i, Letters.Validate(l).FirstOrDefault(x => x.Blocking))).FirstOrDefault(x => x.Item2 is not null);
                if (bad.Item2 is { } issue) throw new ArgumentException($"Brief {bad.i + 1}: {issue.Field} – {issue.Message}");
                var dir = System.IO.Path.Combine(ProjectsRoot, "Briefe", $"Serienbrief-{DateTime.Now:yyyyMMdd-HHmmss}"); Directory.CreateDirectory(dir);
                for (var i = 0; i < list.Count; i++)
                {
                    var stem = new string(list[i].Recipient.Split('\n')[0].Where(ch => !System.IO.Path.GetInvalidFileNameChars().Contains(ch)).ToArray()).Trim();
                    Letters.WriteDocx(list[i], System.IO.Path.Combine(dir, $"{i + 1:000}-{(stem.Length > 0 ? stem : "Brief")}.docx"));
                }
                _core.Audit.Append(Principal.LocalOwner.Name, "letter.serial", $"{list.Count} Briefe (Adressbuch)");
                _letterSerial = null; _letterMsg = $"{list.Count} Briefe gespeichert in {dir}"; Show(V); NotifyFileResult(_letterMsg);
            }), true)));
        return new StackPanel { Children = { LetterKindCard(d), book, form, dl, serial, serialBook } }; // Briefart zuerst (Claude 5)
    }

    /// <summary>Gezeichnete Striche (Pixel im Zeichenfeld) – bleiben beim Neuzeichnen der Seite erhalten.</summary>
    private readonly List<List<Point>> _sigStrokes = [];
    public int LetterSignatureStrokes => _sigStrokes.Count;

    /// <summary>F4 + F3 Briefe (Vorbild-Analyse, nächster Schritt 1): Unterschrift mit Maus, Stift oder Finger zeichnen – oder ein Bild wählen.</summary>
    private Control LetterSignBox(LetterDraft d)
    {
        const string V = "brief";
        Button B(string name, string label, Action go, bool accent = false) { var b = new Button { Name = name, Content = label, FontSize = 12, Padding = new Thickness(10, 4), Margin = new Thickness(0, 0, 6, 6), Background = accent ? Accent : Brushes.Transparent, BorderBrush = Edge, BorderThickness = new Thickness(1) }; b.Click += (_, _) => go(); return b; }
        var padBox = SignaturePad("letter-sig", V, f =>
        {
            d.SignatureFile = f; _letterMsg = "✓ Gezeichnete Unterschrift steht unter dem Gruß."; Show(V);
        }, m => LetterTry(() => throw new ArgumentException(m)));
        var sig = new TextBox { Name = "letter-signature", Text = _settings.LetterSignature, PlaceholderText = "… oder Bild deiner Unterschrift (PNG oder JPG)", MinWidth = 260, Margin = new Thickness(0, 0, 6, 6) };
        var set = B("letter-signature-set", "Bild übernehmen", () => LetterTry(() =>
        {
            var f = Clean(sig);
            if (!File.Exists(f) || XrPlus.ImageSize(File.ReadAllBytes(f)) is (0, _) or (_, 0)) throw new ArgumentException("Bitte ein PNG- oder JPG-Bild wählen.");
            _settings.LetterSignature = d.SignatureFile = f; SaveSettings(); _letterMsg = "Unterschrift wird unter den Gruß gesetzt."; Show(V);
        }));
        var off = B("letter-signature-off", "Ohne Unterschrift", () => { _settings.LetterSignature = d.SignatureFile = ""; SaveSettings(); Show(V); });
        var sp = new StackPanel { Name = "letter-sign", Spacing = 5, Children = {
            new TextBlock { Text = "Unterschrift", Foreground = Text, FontSize = 13, FontWeight = FontWeight.SemiBold },
            P("Mit Maus, Stift oder Finger unterschreiben – sie kommt unter den Gruß. Das ist keine qualifizierte elektronische Signatur.", null, 11),
            padBox, new WrapPanel { Children = { sig, set, off } } } };
        if (File.Exists(d.SignatureFile)) sp.Children.Add(P("✓ Im Brief: " + System.IO.Path.GetFileName(d.SignatureFile), Brush.Parse("#4ADE80"), 11));
        return new Border { Background = TileBg, BorderBrush = Edge, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 8), Margin = new Thickness(0, 6, 0, 0), Child = sp };
    }

    /// <summary>Zeichenfeld für die Unterschrift (Briefe und PDF): Maus, Stift oder Finger. „Übernehmen“ speichert ein PNG im Datenordner,
    /// merkt es als Unterschrift der Person und gibt den Pfad an <paramref name="saved"/>. <paramref name="error"/> zeigt eine Meldung.</summary>
    private Control SignaturePad(string prefix, string page, Action<string> saved, Action<string> error, double width = 340, double height = 110)
    {
        Button B(string name, string label, Action go, bool accent = false) { var b = new Button { Name = name, Content = label, FontSize = 12, Padding = new Thickness(10, 4), Margin = new Thickness(0, 0, 6, 6), Background = accent ? Accent : Brushes.Transparent, BorderBrush = Edge, BorderThickness = new Thickness(1) }; b.Click += (_, _) => go(); return b; }
        var ink = Brush.Parse("#1B2A4A");
        var canvas = new Canvas { Name = prefix + "-canvas", Width = width, Height = height, Background = Brushes.White, ClipToBounds = true, Cursor = new Cursor(StandardCursorType.Cross) };
        var hint = new TextBlock { Text = "Hier unterschreiben", Foreground = Brush.Parse("#9AA6B2"), FontSize = 12, IsHitTestVisible = false, IsVisible = _sigStrokes.Count == 0 };
        Canvas.SetLeft(hint, 12); Canvas.SetTop(hint, 8); canvas.Children.Add(hint);
        canvas.Children.Add(new Line { StartPoint = new Point(20, height - 24), EndPoint = new Point(width - 20, height - 24), Stroke = Brush.Parse("#C9D2DB"), StrokeThickness = 1, IsHitTestVisible = false });
        Polyline Poly(List<Point> pts) => new() { Points = [.. pts], Stroke = ink, StrokeThickness = 2.4, StrokeLineCap = PenLineCap.Round, StrokeJoin = PenLineJoin.Round, IsHitTestVisible = false };
        foreach (var st in _sigStrokes) canvas.Children.Add(Poly(st));
        List<Point>? cur = null; Polyline? curLine = null; Button? clearBtn = null, useBtn = null;
        canvas.PointerPressed += (_, e) =>
        {
            cur = [e.GetPosition(canvas)]; _sigStrokes.Add(cur); curLine = Poly(cur); canvas.Children.Add(curLine); hint.IsVisible = false; clearBtn?.SetValue(IsEnabledProperty, true); useBtn?.SetValue(IsEnabledProperty, true);
            e.Pointer.Capture(canvas); e.Handled = true;
        };
        canvas.PointerMoved += (_, e) =>
        {
            if (cur is null || curLine is null) return;
            var p = e.GetPosition(canvas); if (cur.Count > 0 && (cur[^1] - p) is var v && v.X * v.X + v.Y * v.Y < 1) return;
            cur.Add(p); curLine.Points = [.. cur];
        };
        canvas.PointerReleased += (_, e) => { cur = null; curLine = null; e.Pointer.Capture(null); };
        var pad = new Border { CornerRadius = new CornerRadius(8), BorderBrush = Edge, BorderThickness = new Thickness(1), ClipToBounds = true, HorizontalAlignment = HorizontalAlignment.Left, Child = canvas };
        var use = B(prefix + "-use", width < 300 ? "Übernehmen" : "Gezeichnete Unterschrift übernehmen", () =>
        {
            var png = LetterSignature.ToPng(_sigStrokes.Select(x => (IReadOnlyList<(double, double)>)x.Select(p => (p.X, p.Y)).ToList()).ToList());
            if (png is null) { error("Bitte erst im weißen Feld unterschreiben."); return; }
            var dir = System.IO.Path.Combine(_core.DataRoot, "briefe"); Directory.CreateDirectory(dir);
            var f = System.IO.Path.Combine(dir, $"unterschrift-gezeichnet-{DateTime.Now:yyyyMMdd-HHmmss-fff}.png"); File.WriteAllBytes(f, png);
            _settings.LetterSignature = f; SaveSettings(); _sigStrokes.Clear();
            _core.Audit.Append(Principal.LocalOwner.Name, "signature.drawn", System.IO.Path.GetFileName(f));
            saved(f);
        }, true);
        var clear = B(prefix + "-clear", "Feld leeren", () => { _sigStrokes.Clear(); Show(page); });
        clear.IsEnabled = use.IsEnabled = _sigStrokes.Count > 0; clearBtn = clear; useBtn = use; // erst nach dem ersten Strich
        return new StackPanel { Spacing = 5, Children = { pad, new WrapPanel { Children = { use, clear } } } };
    }

    // ───────────── F3 Briefe: Rechtschreibung beim Tippen ─────────────
    private HashSet<string> _letterSpellBad = new(StringComparer.Ordinal);
    private Avalonia.Threading.DispatcherTimer? _letterSpellTimer;
    private Action? _letterSpellSync;
    public IReadOnlyCollection<string> LetterSpellingMistakes => _letterSpellBad;
    Spelling? LetterSpelling()
    {
        // Ohne Wörterbuch erneut suchen (LibreOffice kann inzwischen eingerichtet sein) – die Suche prüft nur, ob Dateien da sind
        if (_spelling is null) { _spellTried = true; _spelling = Spelling.Load(Spelling.Candidates(_deps.ToolsRoot), System.IO.Path.Combine(_core.DataRoot, "text", "eigene-woerter.txt")); }
        return _spelling;
    }
    /// <summary>Kurz nach dem Tippen prüfen (nicht bei jedem Tastendruck).</summary>
    void LetterSpellLater()
    {
        _letterSpellTimer ??= new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _letterSpellTimer.Stop(); _letterSpellTimer.Tick -= SpellTick; _letterSpellTimer.Tick += SpellTick; _letterSpellTimer.Start();
        void SpellTick(object? s, EventArgs e) { _letterSpellTimer!.Stop(); _letterSpellSync?.Invoke(); }
    }
    /// <summary>Sofort prüfen (Tests, Seitenaufbau).</summary>
    public void LetterSpellNow() => _letterSpellSync?.Invoke();

    /// <summary>Leiste unter dem Brieftext: unbekannte Wörter mit Vorschlag (Klick ersetzt) und „merken“; in der Vorschau rot unterstrichen.</summary>
    private Control LetterSpellBar(LetterDraft d, TextBox subject, TextBox body)
    {
        var bar = new WrapPanel { Name = "letter-spell" };
        void Sync()
        {
            bar.Children.Clear();
            d.Subject = subject.Text ?? ""; d.Body = body.Text ?? ""; // Felder lesen – TextChanged kann noch ausstehen
            var sp = LetterSpelling();
            if (sp is null) { _letterSpellBad.Clear(); bar.Children.Add(P("Rechtschreibung: Das deutsche Wörterbuch fehlt – es kommt mit LibreOffice (Zusatzprogramme).", null, 11)); return; }
            var found = sp.CheckText(d.Subject).Select(m => (Field: subject, m)).Concat(sp.CheckText(d.Body).Select(m => (Field: body, m))).ToList();
            var bad = found.Select(f => f.m.Word).ToHashSet(StringComparer.Ordinal);
            var changed = !bad.SetEquals(_letterSpellBad); _letterSpellBad = bad;
            if (found.Count == 0) bar.Children.Add(new TextBlock { Name = "letter-spell-ok", Text = "✓ Rechtschreibung: keine unbekannten Wörter", Foreground = Brush.Parse("#4ADE80"), FontSize = 11.5 });
            else bar.Children.Add(new TextBlock { Text = $"Rechtschreibung – {found.Count} unbekannt:", Foreground = Brush.Parse("#F87171"), FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 4) });
            var i = 0;
            foreach (var (field, m) in found.DistinctBy(f => f.m.Word).Take(8))
            {
                var best = sp.Suggest(m.Word).FirstOrDefault();
                var n = i++;
                var fix = new Button { Name = $"letter-spell-fix-{n}", Content = best is null ? m.Word : $"{m.Word} → {best}", FontSize = 11.5, Padding = new Thickness(8, 2), Margin = new Thickness(0, 0, 4, 4), IsEnabled = best is not null, BorderBrush = Brush.Parse("#F87171"), BorderThickness = new Thickness(1), Background = Brushes.Transparent };
                ToolTip.SetTip(fix, best is null ? "Kein Vorschlag – bitte selbst prüfen" : "Alle Stellen ersetzen. Weitere Vorschläge: " + string.Join(", ", sp.Suggest(m.Word).Skip(1).Take(4)));
                var tf = field; var word = m.Word;
                fix.Click += (_, _) =>
                {
                    if (best is null) return;
                    tf.Text = System.Text.RegularExpressions.Regex.Replace(tf.Text ?? "", $@"(?<![\p{{L}}]){System.Text.RegularExpressions.Regex.Escape(word)}(?![\p{{L}}])", best);
                    if (ReferenceEquals(tf, body)) d.Body = tf.Text ?? ""; else d.Subject = tf.Text ?? "";
                    Sync(); _letterSync?.Invoke();
                };
                var own = new Button { Name = $"letter-spell-own-{n}", Content = "+", FontSize = 11.5, Padding = new Thickness(6, 2), Margin = new Thickness(0, 0, 10, 4) };
                ToolTip.SetTip(own, $"„{word}“ ist richtig – ins eigene Wörterbuch");
                own.Click += (_, _) => { sp.AddOwn(word); Sync(); _letterSync?.Invoke(); };
                bar.Children.Add(fix); bar.Children.Add(own);
            }
            if (changed) _letterSync?.Invoke();
        }
        _letterSpellSync = Sync;
        subject.TextChanged += (_, _) => LetterSpellLater(); body.TextChanged += (_, _) => LetterSpellLater();
        Sync();
        return bar;
    }

    /// <summary>Brieftext der Vorschau mit rot unterstrichenen unbekannten Wörtern.</summary>
    void LetterSpellMark(TextBlock tb, string text)
    {
        if (_letterSpellBad.Count == 0) return;
        var rx = new System.Text.RegularExpressions.Regex(@"[\p{L}][\p{L}\p{M}'’\-]*[\p{L}]|[\p{L}]");
        var deco = new TextDecorationCollection { new TextDecoration { Location = TextDecorationLocation.Underline, Stroke = Brush.Parse("#E11D48"), StrokeThickness = 0.35, StrokeThicknessUnit = TextDecorationUnit.Pixel } };
        var inl = new Avalonia.Controls.Documents.InlineCollection(); var pos = 0; var any = false;
        foreach (System.Text.RegularExpressions.Match m in rx.Matches(text))
        {
            if (!_letterSpellBad.Contains(m.Value)) continue;
            if (m.Index > pos) inl.Add(new Avalonia.Controls.Documents.Run(text[pos..m.Index]));
            inl.Add(new Avalonia.Controls.Documents.Run(m.Value) { TextDecorations = deco, Foreground = Brush.Parse("#B91C1C") }); pos = m.Index + m.Length; any = true;
        }
        if (!any) return;
        if (pos < text.Length) inl.Add(new Avalonia.Controls.Documents.Run(text[pos..]));
        tb.Inlines = inl;
    }

    /// <summary>U3: Schrittleiste – zeigt, was schon fertig ist, und springt zum passenden Feld.</summary>
    private Control LetterSteps(LetterDraft d)
    {
        var row = new StackPanel { Name = "letter-steps", Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(0, 0, 0, 8) };
        var steps = new (string Label, Func<bool> Done, Action Go)[]
        {
            ("Absender", () => d.SenderName.Trim().Length > 0 && d.SenderStreet.Trim().Length > 0 && d.SenderCity.Trim().Length > 0, () => LetterGo("letter-sender-name", "Trag hier deinen Namen und deine Anschrift ein.")),
            ("Empfänger", () => d.Recipient.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries).Length >= 3, () => LetterGo("letter-recipient", "Trag die Anschrift des Empfängers ein: Name, Straße, PLZ Ort.")),
            ("Inhalt", () => d.Subject.Trim().Length > 0 && d.Body.Trim().Length > 0 && !d.Subject.Contains('[') && !d.Body.Contains('['), () => LetterGo("letter-subject", "Schreib Betreff und Text. Eckige Klammern [ ] noch ausfüllen.")),
            ("Prüfen", () => !Letters.Validate(d).Any(x => x.Blocking), () => ClickLetter("letter-check")),
            ("Fertig", () => _letterSaved, () => ClickLetter("letter-save")),
        };
        var buttons = new List<Button>();
        for (var i = 0; i < steps.Length; i++)
        {
            var (label, _, go) = steps[i];
            if (i > 0) row.Children.Add(new TextBlock { Text = "›", Foreground = Muted, FontSize = 16, VerticalAlignment = VerticalAlignment.Center });
            var b = new Button { Name = $"letter-step-{i + 1}", FontSize = 12, Padding = new Thickness(12, 5), CornerRadius = new CornerRadius(14), BorderBrush = Edge, BorderThickness = new Thickness(1), Foreground = Text };
            b.Click += (_, _) => go();
            buttons.Add(b); row.Children.Add(b);
        }
        void Sync()
        {
            var current = Array.FindIndex(steps, s => !s.Done());
            for (var i = 0; i < steps.Length; i++)
            {
                var done = steps[i].Done();
                buttons[i].Content = (done ? "✓ " : $"{i + 1} ") + steps[i].Label;
                buttons[i].Background = i == current ? Accent : done ? Brush.Parse(C("#3322B35E", "#3322B35E")) : Brushes.Transparent;
            }
        }
        _letterStepSync = Sync; Sync();
        return row;
    }

    /// <summary>Meldungen der Zusatzwerkzeuge erscheinen in der Briefzeile („letter-msg“), nicht in einer unsichtbaren Sammelmeldung.</summary>
    private void LetterTry(Action a)
    {
        try { a(); }
        catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or UnauthorizedAccessException or InvalidDataException or FormatException) { _letterMsg = ex.Message; Show("brief"); }
    }

    /// <summary>Kurze Rückmeldung in der Briefzeile, ohne die Seite neu aufzubauen.</summary>
    private void LetterNote(string text)
    {
        _letterMsg = text;
        if (this.GetLogicalDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Name == "letter-msg") is { } m) m.Text = text;
    }

    private void LetterGo(string field, string hint)
    {
        if (this.GetLogicalDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Name == "letter-msg") is { } m) m.Text = hint;
        FocusLetterField(field);
    }

    private void FocusLetterField(string name)
    {
        // Assistent: liegt das Feld in einem anderen Schritt (oder unter „Mehr“), erst dorthin wechseln
        var step = name switch { "letter-sender-name" or "letter-sender-street" or "letter-sender-city" or "letter-sender-contact" or "letter-recipient" => 2,
            "letter-subject" or "letter-salutation" or "letter-body" or "letter-closing" => 3, _ => 0 };
        if (!_letterAll && CurrentView == "brief" && ((step > 0 && step != _letterStep) || (step == 0 && name is "letter-place" or "letter-reference" or "letter-attachments")))
        {
            if (step > 0) _letterStep = step; else _letterAll = true;
            Show("brief");
            _ui.Post(() => FocusLetterField(name)); // nach dem Neuaufbau ins Feld springen
            return;
        }
        if (this.GetLogicalDescendants().OfType<TextBox>().FirstOrDefault(t => t.Name == name) is { } t) { t.BringIntoView(); t.Focus(); t.CaretIndex = t.Text?.Length ?? 0; }
    }

    private void ClickLetter(string name)
    {
        if (this.GetLogicalDescendants().OfType<Button>().FirstOrDefault(b => b.Name == name) is { } b) b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    /// <summary>U1 + Masterprompt 131: DIN-A4-Seiten im Maßstab (DIN 5008 Form A oder B) mit Sichtfenster, Falz- und Lochmarken, Informationsblock,
    /// echtem Seitenumbruch und Folgeseiten („Seite n von N“). U2: Klick auf einen Teil springt ins passende Feld. Serienbrief: Brief für Brief ansehen.</summary>
    private Control LetterPreview(LetterDraft d)
    {
        var pagesHost = new StackPanel { Name = "letter-pages", Spacing = 10 };
        var serialBar = new StackPanel { Spacing = 6 };
        void Sync()
        {
            var shown = _letterSerial is { Count: > 0 } ser ? ser[Math.Clamp(_letterSerialIdx, 0, ser.Count - 1)] : d;
            pagesHost.Children.Clear();
            foreach (var page in LetterPages(shown, interactive: ReferenceEquals(shown, d)))
                pagesHost.Children.Add(new Viewbox { Stretch = Stretch.Uniform, MaxWidth = 560, Child = new Border { BoxShadow = BoxShadows.Parse("0 2 8 0 #44000000"), Child = page } });
            serialBar.Children.Clear();
            if (_letterSerial is { Count: > 0 } s2)
                serialBar.Children.Add(new Border { Name = "letter-serial-bar", Background = Brush.Parse("#33C9A227"), BorderBrush = Brush.Parse("#D4A84B"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 4),
                    Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
                        Act("letter-serial-prev", "◀", () => { _letterSerialIdx = Math.Max(0, _letterSerialIdx - 1); _letterSync?.Invoke(); }),
                        new TextBlock { Name = "letter-serial-pos", Text = $"Serienbrief {_letterSerialIdx + 1} von {s2.Count}: {s2[_letterSerialIdx].Recipient.Split('\n')[0]}", Foreground = Text, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeight.SemiBold },
                        Act("letter-serial-next", "▶", () => { _letterSerialIdx = Math.Min(s2.Count - 1, _letterSerialIdx + 1); _letterSync?.Invoke(); }),
                        Act("letter-serial-close", "Zurück zum Entwurf", () => { _letterSerial = null; _letterSerialIdx = 0; _letterSync?.Invoke(); }) } } });
            _letterStepSync?.Invoke();
        }
        _letterSync = Sync; Sync();
        var sheet = new Border { Name = "letter-preview", Background = Brush.Parse("#E6EBF0"), CornerRadius = new CornerRadius(8), Padding = new Thickness(10), MinWidth = 180, VerticalAlignment = VerticalAlignment.Stretch,
            Child = new ScrollViewer { Content = pagesHost, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto } };
        var wrap = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*") };
        wrap.Children.Add(new TextBlock { Text = $"Vorschau (DIN A4, DIN 5008 Form {(d.Form == "A" ? "A" : "B")}) – auf einen Teil klicken, um ihn zu bearbeiten", Foreground = Text, FontSize = 12, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 6), TextWrapping = TextWrapping.Wrap });
        Grid.SetRow(serialBar, 1); wrap.Children.Add(serialBar);
        Grid.SetRow(sheet, 2); wrap.Children.Add(sheet);
        return wrap;
    }

    /// <summary>Design-Kopf (Wunsch 02.11.2026): Farbband, Linie oder Seitenleiste in der Akzentfarbe, Name groß, Zeile darunter (z. B. Beruf), Kontakt rechts, Foto oben rechts.
    /// Das Anschriftfeld und die Falzmarken nach DIN 5008 bleiben unberührt.</summary>
    static void LetterDesignHead(Canvas p1, LetterDraft d, bool formA, Func<string, string, double, IBrush, string?, bool, double?, bool, TextBlock> T, Action<Canvas, Control, double, double> At, IBrush accent, IBrush grey)
    {
        var h = d.Header; var hdrH = formA ? 22.0 : 32.0;
        var band = h is "band" or "zweifarbig";
        IBrush onBand = Brushes.White, onBandSoft = Brush.Parse("#E6FFFFFF");
        if (h == "band") At(p1, new Rectangle { Name = "letter-design-band", Width = 210, Height = hdrH, Fill = accent }, 0, 0);
        if (h == "zweifarbig")
        {
            At(p1, new Rectangle { Name = "letter-design-band", Width = 210, Height = hdrH - 3, Fill = accent }, 0, 0);
            At(p1, new Rectangle { Name = "letter-design-stripe", Width = 210, Height = 3, Fill = Brush.Parse(LetterDesigns.Second(d)) }, 0, hdrH - 3);
        }
        if (h == "seitenleiste") At(p1, new Rectangle { Name = "letter-design-side", Width = 7, Height = 297, Fill = accent }, 0, 0);
        var hasPhoto = File.Exists(d.Photo);
        var contactLines = new[] { d.SenderStreet, d.SenderCity, d.SenderContact }.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        if (h == "mittig")
        {
            var name = T("letter-preview-sender-name", d.SenderName, formA ? 16 : 20, accent, "letter-sender-name", true, 160, false); name.TextAlignment = TextAlignment.Center; At(p1, name, 25, formA ? 4 : 7);
            if (d.Tagline.Length > 0) { var tl = T("letter-preview-tagline", d.Tagline, 10, grey, "letter-sender-name", false, 160, true); tl.TextAlignment = TextAlignment.Center; At(p1, tl, 25, formA ? 11.5 : 15.5); }
            var c = T("letter-preview-sender", string.Join("  ·  ", contactLines), 8.5, grey, "letter-sender-street", false, 180, true); c.TextAlignment = TextAlignment.Center; At(p1, c, 15, formA ? 16 : 21.5);
            At(p1, new Rectangle { Name = "letter-design-line", Width = 60, Height = 0.6, Fill = accent }, 75, hdrH - 3);
        }
        else
        {
            At(p1, T("letter-preview-sender-name", d.SenderName, formA ? 16 : 20, band ? onBand : accent, "letter-sender-name", true, 125, false), 25, formA ? 4 : 7);
            if (d.Tagline.Length > 0) At(p1, T("letter-preview-tagline", d.Tagline, 10, band ? onBandSoft : grey, "letter-sender-name", false, 125, true), 25, formA ? 11.5 : 15.5);
            var contact = T("letter-preview-sender", string.Join("\n", contactLines), 8.5, band ? onBandSoft : grey, "letter-sender-street", false, 60, true);
            contact.TextAlignment = TextAlignment.Right;
            At(p1, contact, hasPhoto ? 103 : 130, formA ? 4 : 7);
            if (h is "linie" or "seitenleiste" or "rahmen") At(p1, new Rectangle { Name = "letter-design-line", Width = 165, Height = 0.6, Fill = accent }, 25, hdrH - 3);
        }
        if (hasPhoto)
        {
            var img = new Image { Name = "letter-preview-photo", Width = formA ? 19 : 26, Height = formA ? 24 : 33, Stretch = Stretch.UniformToFill };
            try { img.Source = new Avalonia.Media.Imaging.Bitmap(d.Photo); } catch (Exception) { img.IsVisible = false; }
            At(p1, new Border { ClipToBounds = true, BorderBrush = band ? onBand : accent, BorderThickness = new Thickness(0.6), Child = img }, formA ? 178 : 170, 4);
        }
    }

    /// <summary>Gestaltung, die auf jeder Seite steht: Rahmen und Fußband (wie im PDF).</summary>
    static void LetterPageDeco(Canvas p, LetterDraft d, IBrush accent, Func<string, string, double, IBrush, string?, bool, double?, bool, TextBlock> T, Action<Canvas, Control, double, double> At)
    {
        if (!d.Designed) return;
        if (d.Header == "rahmen") At(p, new Rectangle { Name = "letter-design-frame", Width = 194, Height = 281, Stroke = accent, StrokeThickness = 0.5 }, 8, 8);
        if (d.Header == "fussband")
        {
            At(p, new Rectangle { Name = "letter-design-foot", Width = 210, Height = 12, Fill = accent }, 0, 285);
            var c = T("letter-preview-foot", string.Join("  ·  ", new[] { d.SenderName, d.SenderStreet, d.SenderCity, d.SenderContact }.Where(s => !string.IsNullOrWhiteSpace(s))), 8.5, Brushes.White, null, false, 180, false);
            c.TextAlignment = TextAlignment.Center; At(p, c, 15, 288.5);
        }
    }

    /// <summary>Zeilen eines Textes so, wie sie auf der Seite umbrechen (Arial, Breite in mm).</summary>
    static List<string> LetterLines(string text, double pt, double width, bool bold = false)
    {
        if (text.Length == 0) return [];
        var tl = new Avalonia.Media.TextFormatting.TextLayout(text.Replace("\r", ""), new Typeface(new FontFamily("Arial, Liberation Sans, Helvetica"), FontStyle.Normal, bold ? FontWeight.Bold : FontWeight.Normal), pt * 0.3528, Brushes.Black,
            textWrapping: TextWrapping.Wrap, maxWidth: width);
        var src = text.Replace("\r", "");
        return tl.TextLines.Select(l => src.Substring(l.FirstTextSourceIndex, Math.Min(l.Length, src.Length - l.FirstTextSourceIndex)).TrimEnd('\n')).ToList();
    }

    /// <summary>Die Seiten des Briefs: Seite 1 mit Kopf, Anschrift, Informationsblock, Betreff und Anrede; Text fließt weiter; Gruß, Unterschrift und Anlagen am Ende.</summary>
    List<Canvas> LetterPages(LetterDraft d, bool interactive)
    {
        const double Pt = 0.3528; double lineH = 11 * Pt * 1.2; var formA = d.Form == "A"; var shift = formA ? -18 : 0;
        LetterDesigns.Normalize(d);
        var font = d.Designed ? d.Font + ", Arial, Liberation Sans, Helvetica" : "Arial, Liberation Sans, Helvetica"; // Design: gewählte Schrift
        IBrush accent = Brush.Parse(d.Accent);
        IBrush ink = Brushes.Black, grey = Brush.Parse("#5C6B7A");
        TextBlock T(string name, string text, double pt, IBrush fg, string? field, bool bold = false, double? width = null, bool wrap = true)
        {
            var t = new TextBlock { Name = name, Text = text, FontFamily = new FontFamily(font), FontSize = pt * Pt, Foreground = fg, TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap, LineHeight = pt * Pt * 1.2, FontWeight = bold ? FontWeight.Bold : FontWeight.Normal };
            if (width is { } w) t.Width = w;
            if (field is not null && interactive)
            {
                t.Cursor = new Cursor(StandardCursorType.Hand); ToolTip.SetTip(t, "Zum Bearbeiten klicken");
                t.PointerPressed += (_, e) => { FocusLetterField(field); e.Handled = true; };
            }
            return t;
        }
        Canvas NewPage() => new() { Width = 210, Height = 297, Background = Brushes.White, ClipToBounds = true };
        void At(Canvas p, Control c, double x, double y) { Canvas.SetLeft(c, x); Canvas.SetTop(c, y); p.Children.Add(c); }
        void Marks(Canvas p)
        {
            if (d.Designed) LetterPageDeco(p, d, accent, T, At); // Rahmen, Fußband auf jeder Seite
            if (!d.PrintMarks) return;
            foreach (var y in formA ? new[] { 87.0, 192.0 } : [105.0, 210.0]) At(p, new Line { StartPoint = new Point(0, 0), EndPoint = new Point(5, 0), Stroke = grey, StrokeThickness = 0.3 }, 3, y);
            At(p, new Line { StartPoint = new Point(0, 0), EndPoint = new Point(7, 0), Stroke = grey, StrokeThickness = 0.3 }, 3, 148.5);
        }

        var pages = new List<Canvas>(); var p1 = NewPage(); pages.Add(p1); Marks(p1);
        // Kopf, Sichtfenster, Anschrift, Informationsblock
        At(p1, new Rectangle { Width = 85, Height = 45, Stroke = Brush.Parse("#9CB3C9"), StrokeThickness = 0.3, StrokeDashArray = [4, 3] }, 20, 45 + shift);
        var sender = T("letter-preview-sender", string.Join("\n", new[] { d.SenderName, d.SenderStreet, d.SenderCity, d.SenderContact }.Where(s => !string.IsNullOrWhiteSpace(s))), 9, grey, "letter-sender-name", width: 85); sender.TextAlignment = TextAlignment.Right;
        if (d.Designed) LetterDesignHead(p1, d, formA, T, At, accent, grey);
        else At(p1, sender, 105, formA ? 8 : 15);
        var back = T("letter-preview-return", string.Join(" · ", new[] { d.SenderName, d.SenderStreet, d.SenderCity }.Where(s => !string.IsNullOrWhiteSpace(s))), 7, grey, "letter-sender-street", width: 80, wrap: false); back.TextTrimming = TextTrimming.CharacterEllipsis;
        At(p1, back, 25, 58 + shift);
        At(p1, T("letter-preview-recipient", d.Recipient, 11, ink, "letter-recipient", width: 80), 25, 62.7 + shift);
        At(p1, T("letter-preview-date", string.Join("\n", Letters.InfoBlock(d)), 9, ink, "letter-place", width: 75), 125, 50 + shift);
        // Betreff und Anrede
        var y = 98.5 + shift;
        var subjLines = LetterLines(d.Subject, 11, 165, bold: true);
        At(p1, T("letter-preview-subject", d.Subject, 11, d.Designed ? accent : ink, "letter-subject", bold: true, width: 165), 25, y); y += Math.Max(1, subjLines.Count) * lineH + 8.5;
        var salLines = LetterLines(d.Salutation, 11, 165);
        At(p1, T("letter-preview-salutation", d.Salutation, 11, ink, "letter-salutation", width: 165), 25, y); y += Math.Max(1, salLines.Count) * lineH + 4.2;
        // Text mit Seitenumbruch (unten 20 mm frei, Folgeseiten ab 27 mm)
        var lines = LetterLines(d.Body, 11, 165);
        double sigH = 0; if (File.Exists(d.SignatureFile)) try { var (sw, sh) = XrPlus.ImageSize(File.ReadAllBytes(d.SignatureFile)); if (sw > 0) sigH = 45.0 * sh / sw; } catch (IOException) { }
        var attLines = string.IsNullOrWhiteSpace(d.Attachments) ? 0 : 1 + LetterLines(d.Attachments.Trim(), 11, 165).Count;
        var endH = 4.2 + lineH + (sigH > 0 ? sigH + 1 : 12) + lineH + (attLines > 0 ? 6 + attLines * lineH : 0);
        double Bottom = d.Designed && d.Header == "fussband" ? 270 : 277; const double FollowTop = 27;
        var page = p1; var chunk = new List<string>(); var chunkTop = y; var pageNo = 1;
        void Flush()
        {
            if (chunk.Count == 0) return;
            var tb = T(pageNo == 1 ? "letter-preview-body" : $"letter-preview-body-{pageNo}", string.Join("\n", chunk), 11, ink, "letter-body", width: 170, wrap: false);
            if (interactive) LetterSpellMark(tb, tb.Text ?? "");
            At(page, tb, 25, chunkTop); chunk.Clear();
        }
        void NextPage() { Flush(); page = NewPage(); pages.Add(page); pageNo++; Marks(page); y = FollowTop; chunkTop = y; }
        foreach (var l in lines) { if (y + lineH > Bottom) NextPage(); chunk.Add(l); y += lineH; }
        if (y + endH > Bottom) NextPage();
        Flush();
        // Gruß, Unterschrift, Name, Anlagen
        y += 4.2; At(page, T("letter-preview-closing", d.Closing, 11, ink, "letter-closing", width: 165), 25, y); y += lineH;
        var sig = new Image { Name = "letter-preview-signature", Width = 45, IsVisible = sigH > 0 };
        if (sigH > 0) try { sig.Source = new Avalonia.Media.Imaging.Bitmap(d.SignatureFile); } catch (Exception) { sig.IsVisible = false; }
        At(page, sig, 25, y + 1); y += sigH > 0 ? sigH + 1 : 12;
        At(page, T("letter-preview-name", d.SenderName, 11, ink, "letter-sender-name", width: 165), 25, y); y += lineH;
        var att = T("letter-preview-attachments", attLines > 0 ? "Anlagen\n" + d.Attachments.Trim() : "", 11, ink, "letter-attachments", width: 165); att.IsVisible = attLines > 0;
        At(page, att, 25, y + 6);
        // Seitenzahlen (DIN 5008: Folgeseiten nummerieren; bei mehreren Seiten auch „Seite 1 von N“)
        if (pages.Count > 1)
            for (var i = 0; i < pages.Count; i++)
            {
                At(pages[i], T(i == 0 ? "letter-preview-pageno" : $"letter-preview-pageno-{i + 1}", $"Seite {i + 1} von {pages.Count}", 8, grey, null, width: 40), 165, d.Designed && d.Header == "fussband" ? 279 : 287);
                if (i > 0 && d.Designed) At(pages[i], new Rectangle { Width = 165, Height = 0.5, Fill = accent }, 25, 20); // Design: feine Linie auch auf Folgeseiten
                if (i > 0) At(pages[i], T($"letter-preview-head-{i + 1}", (d.SenderName.Trim().Length > 0 ? d.SenderName.Trim() + " · " : "") + d.Subject, 8, grey, null, width: 160, wrap: false), 25, 15);
            }
        return pages;
    }
}
