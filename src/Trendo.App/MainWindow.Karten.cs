using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Trendo.Core;
using Trendo.Core.Buero;
using Trendo.Core.Programs;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>STUDIO-CARDS-01 Karten &amp; Einladungen (T-0671): Seiten der Karte wie in einem Karten-Editor gestalten, Gästeliste mit Zu- und Absagen,
/// Serienkarten mit Namen, Druck-PDF mit Beschnitt, Schnitt- und Falzmarken.</summary>
public partial class MainWindow
{
    public static Karte CardOf(ProgramDocument d) => Karte.FromJson(d.Data["karte"]);
    public void CardChange(ProgramDocument d, string label, Action<Karte> change)
    {
        var k = CardOf(d); change(k); var j = k.ToJson();
        d.Edit(label, x => x["karte"] = j);
    }
    static JsonObject CardDoc(Karte k) => new() { ["karte"] = k.ToJson(), ["page"] = "vorne", ["sel"] = 0 };

    public static string CardPage(ProgramDocument d)
    {
        var k = CardOf(d); var p = d.Text("page", "vorne");
        return KartenKit.Seiten(k).Any(s => s.Id == p) ? p : "vorne";
    }
    public static int CardSel(ProgramDocument d) => (int)d.Number("sel");

    private void InitKarten() => RegisterProgram(FachTabs, new ProgramSpec(CardKey, "STUDIO-CARDS-01", "Karten & Einladungen")
    {
        Sentence = "Klappkarten und Postkarten für jeden Anlass gestalten – mit Gästeliste, Zu- und Absagen, Serienkarten mit Namen und Druck-PDF mit Beschnitt.",
        Folder = "Karten",
        ScrollWorkspace = true,
        GroupLevels = new() { ["Karte"] = 0, ["Gäste"] = 0, ["Druck"] = 0 },
        Templates =
        [
            .. KartenVorlagen.Alle.Select(v => new ProgramTemplate(v.Id, v.Name, v.Hint + " · Klappkarte A6", () => CardDoc(KartenVorlagen.Neu(v.Id)))),
            new("hochzeit-a5", "Hochzeit (Klappkarte A5)", "Größer, mit Antwort bis", () => CardDoc(KartenVorlagen.Neu("hochzeit", "a5"))),
            new("danke-postkarte", "Danke (Postkarte)", "Vorder- und Rückseite", () => CardDoc(KartenVorlagen.Neu("danke", "postkarte"))),
        ],
        Ribbon =
        [
            ("Karte", [
                new ProgramCmd("cards-add-text", "＋ Text", d => CardAddText(d), Primary: true, Tip: "Neuer Text auf der gewählten Seite."),
                new ProgramCmd("cards-image", "Bild …", d => _ = CardPickImage(d), Tip: "Eigenes oder freies Foto auf die gewählte Seite."),
                new ProgramCmd("cards-check", "Prüfen", d => CardCheckNow(d), Tip: "Schrift lesbar? Text am Rand? Bild scharf genug? Angaben vollständig?"),
            ]),
            ("Gäste", [
                new ProgramCmd("cards-guests-csv", "Gästeliste laden (CSV) …", d => _ = CardPickCsv(d), Tip: "Spalten Name, E-Mail, Gruppe, Personen, Antwort – z. B. aus einer Tabelle."),
            ]),
            ("Druck", [
                new ProgramCmd("cards-print", "Druck-PDF", d => CardPrintNow(d, false), Tip: "Mit 3 mm Beschnitt, Schnitt- und Falzmarken – für den Copyshop oder zu Hause."),
                new ProgramCmd("cards-serie", "Serienkarten", d => CardPrintNow(d, true), Tip: "Für jede Person auf der Gästeliste eine Karte mit ihrem Namen ({Name})."),
            ]),
        ],
        Exports =
        [
            new("pdf", "Druck-PDF mit Beschnitt", ".pdf", (d, f) => { KartenPdf.Write(CardOf(d), f); return f; }),
            new("serie", "Serienkarten mit Namen (PDF)", ".pdf", (d, f) => { KartenPdf.WriteSerie(CardOf(d), f); return f; }),
            new("csv", "Gästeliste (CSV)", ".csv", (d, f) => { File.WriteAllText(f, Gaeste.Csv(CardOf(d).Gaeste), new UTF8Encoding(true)); return f; }),
        ],
        SimpleExports = 2,
        Workspace = CardWorkspace,
        Properties = CardProperties,
        Status = d =>
        {
            var k = CardOf(d); var f = KartenKit.Format(k.Format); var st = Gaeste.Zaehlen(k.Gaeste);
            return $"{f.Name} ({f.W:0} × {f.H:0} mm) · {KartenKit.Seiten(k).Count} Seiten · {st.Eingeladen} Gäste, {st.Zusagen} Zusagen ({st.PersonenZugesagt} Personen), {st.Absagen} Absagen, {st.Offen} offen";
        },
        Help = "Oben die Seite wählen, auf einen Text klicken und rechts Schrift, Größe, Farbe und Höhe ändern. {Name}, {Datum}, {Ort} und {AntwortBis} werden beim Drucken eingesetzt. "
             + "Unten die Gästeliste: eine Person je Zeile, z. B. „Familie Muster, 4“ – Antworten per Auswahl. „Druck-PDF“ legt die Bögen mit 3 mm Beschnitt und Marken an, „Serienkarten“ eine Karte je Gast. Trendo druckt und versendet nichts selbst.",
    }, "karte", "karten", "einladung", "einladungskarte", "glückwunschkarte", "geburtstagskarte", "hochzeitskarte", "weihnachtskarte", "dankeskarte", "rsvp", "gästeliste", "zusage", "postkarte", "klappkarte");

    // ---------- Befehle ----------

    public void CardAddText(ProgramDocument d)
    {
        var page = CardPage(d); var n = 0;
        CardChange(d, "Text", k => { var s = k.Seite(page); s.Texte.Add(new KartenText { Text = "Neuer Text", Y = 0.5, Groesse = 14, Farbe = KartenKit.Kontrast("#1F2937", s.Hintergrund.Length > 0 ? s.Hintergrund : k.Hintergrund) >= 3 ? "#1F2937" : "#FFFFFF" }); n = s.Texte.Count - 1; });
        d.Data["sel"] = n;
        _progMsg[CardKey] = "Neuer Text – rechts schreiben und gestalten.";
    }

    public List<CvFinding> CardCheckNow(ProgramDocument d)
    {
        var f = KartenKit.Pruefen(CardOf(d)); var err = f.Count(x => x.Fehler);
        _progMsg[CardKey] = f.Count == 0 ? "✓ Bereit zum Drucken." : $"Prüfung: {err} Pflichtpunkte, {f.Count - err} Hinweise – rechts unter „Prüfung“.";
        return f;
    }

    async Task CardPickImage(ProgramDocument d)
    {
        var f = await PickMediaFile("cards-image", "Bild für die Karte", ["*.jpg", "*.jpeg", "*.png"]);
        if (f is not null) ProgramRun(CardKey, () => CardSetImage(d, f));
    }

    public void CardSetImage(ProgramDocument d, string file)
    {
        if (!File.Exists(file)) throw new FileNotFoundException("Die Bilddatei gibt es nicht.", file);
        var page = CardPage(d);
        CardChange(d, "Bild", k => { var s = k.Seite(page); s.Bild = file; if (s.BildModus.Length == 0) s.BildModus = "fuellen"; });
        var warn = KartenKit.Pruefen(CardOf(d)).FirstOrDefault(x => x.Text.Contains("dpi"));
        _progMsg[CardKey] = "Bild eingefügt." + (warn is null ? "" : " " + warn.Text);
    }

    async Task CardPickCsv(ProgramDocument d)
    {
        var f = await PickMediaFile("cards-csv", "Gästeliste (CSV)", ["*.csv", "*.txt"]);
        if (f is not null) ProgramRun(CardKey, () => CardImportCsv(d, f));
    }

    public int CardImportCsv(ProgramDocument d, string file)
    {
        var got = Gaeste.FromCsv(File.ReadAllText(file));
        if (got.Count == 0) throw new InvalidDataException("In der Datei stehen keine Gäste (Spalte „Name“).");
        var added = 0;
        CardChange(d, "Gästeliste geladen", k => { foreach (var g in got.Where(g => !k.Gaeste.Any(x => x.Name.Equals(g.Name, StringComparison.OrdinalIgnoreCase)))) { k.Gaeste.Add(g); added++; } });
        _progMsg[CardKey] = $"{added} Gäste übernommen" + (got.Count > added ? $", {got.Count - added} standen schon auf der Liste." : ".");
        return added;
    }

    public void CardAddGuests(ProgramDocument d, string text)
    {
        var got = Gaeste.ParseLines(text);
        if (got.Count == 0) throw new ArgumentException("Schreib eine Person je Zeile, z. B. „Familie Muster, 4“.");
        CardChange(d, "Gäste", k => k.Gaeste.AddRange(got));
        _progMsg[CardKey] = got.Count == 1 ? $"„{got[0].Name}“ eingetragen ({got[0].Personen} Personen)." : $"{got.Count} Gäste eingetragen.";
    }

    public string CardPrintNow(ProgramDocument d, bool serie)
    {
        var k = CardOf(d);
        var blocking = KartenKit.Pruefen(k).Where(x => x.Fehler).ToList();
        if (blocking.Count > 0) throw new InvalidOperationException("Vor dem Druck: " + blocking[0].Text);
        var stem = DevStudio.SafeName((serie ? "Serienkarten " : "Karte ") + d.Title); var dir = WorkFolder("Karten");
        var f = Path.Combine(dir, stem + ".pdf"); for (var n = 2; File.Exists(f); n++) f = Path.Combine(dir, $"{stem} ({n}).pdf");
        var pages = serie ? KartenPdf.WriteSerie(k, f) : KartenPdf.Write(k, f);
        var (pw, ph) = KartenPdf.PageSize(k);
        _progMsg[CardKey] = (serie ? $"Serienkarten für {pages / KartenKit.Boegen(k).Count} Gäste" : "Druck-PDF") + $" fertig: {pages} Bögen à {pw:0} × {ph:0} mm mit {k.Beschnitt:0.#} mm Beschnitt – {f}";
        NotifyFileResult(_progMsg[CardKey]);
        return f;
    }

    // ---------- Oberfläche ----------

    static Bitmap? CardBitmap(string file)
    {
        if (file.Length == 0 || !File.Exists(file)) return null;
        try { return new Bitmap(file); } catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException or NotSupportedException) { return null; }
    }

    /// <summary>Eine Kartenseite maßstäblich (Pixel je mm), Texte mit eingesetzten Platzhaltern; klickbare Texte wählen sich aus.</summary>
    Control CardPageView(ProgramDocument d, Karte k, string page, double scale, bool interactive, string name)
    {
        var f = KartenKit.Format(k.Format); var s = k.Seite(page);
        var w = f.W * scale; var h = f.H * scale;
        var canvas = new Canvas { Width = w, Height = h, ClipToBounds = true, Background = Brush.Parse(KartenKit.IsHex(s.Hintergrund) ? s.Hintergrund : KartenKit.IsHex(k.Hintergrund) ? k.Hintergrund : "#FFFFFF") };
        if (CardBitmap(s.Bild) is { } bmp)
        {
            var img = new Image { Source = bmp, Stretch = s.BildModus == "mitte" ? Stretch.Uniform : Stretch.UniformToFill };
            var (iw, ih, ix, iy) = s.BildModus switch { "oben" => (w, h * 0.55, 0.0, 0.0), "mitte" => (w * 0.7, h * 0.5, w * 0.15, h * 0.15), _ => (w, h, 0.0, 0.0) };
            img.Width = iw; img.Height = ih; Canvas.SetLeft(img, ix); Canvas.SetTop(img, iy);
            canvas.Children.Add(new Border { Width = iw, Height = ih, ClipToBounds = true, Child = img }.Also(b => { Canvas.SetLeft(b, ix); Canvas.SetTop(b, iy); }));
        }
        var sel = CardSel(d); var current = CardPage(d) == page;
        for (var i = 0; i < s.Texte.Count; i++)
        {
            var t = s.Texte[i]; var ii = i;
            var tb = new TextBlock
            {
                Text = KartenKit.Fill(t.Text, k, null), FontFamily = new FontFamily(t.Schrift), FontSize = Math.Max(1, t.Groesse * 0.3528 * scale), Foreground = Brush.Parse(KartenKit.IsHex(t.Farbe) ? t.Farbe : "#1F2937"),
                FontWeight = t.Fett ? FontWeight.Bold : FontWeight.Normal, FontStyle = t.Kursiv ? FontStyle.Italic : FontStyle.Normal, TextWrapping = TextWrapping.Wrap,
                TextAlignment = t.Ausrichtung switch { "links" => TextAlignment.Left, "rechts" => TextAlignment.Right, _ => TextAlignment.Center }, Width = w - 16 * scale,
            };
            Control c = tb;
            if (interactive)
            {
                var border = new Border { Name = $"{name}-text-{i}", Child = tb, BorderThickness = new Thickness(1), BorderBrush = current && sel == i ? Brush.Parse("#C9A227") : Brushes.Transparent, Background = Brushes.Transparent, Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand) };
                border.PointerPressed += (_, _) => { d.Data["page"] = page; d.Data["sel"] = ii; Show(CardKey); };
                c = border;
            }
            Canvas.SetLeft(c, 8 * scale); Canvas.SetTop(c, Math.Clamp(t.Y, 0, 1) * h);
            canvas.Children.Add(c);
        }
        return new Border { Name = name, Child = canvas, BoxShadow = BoxShadows.Parse("0 6 18 0 #66000000"), HorizontalAlignment = HorizontalAlignment.Center };
    }

    Control CardWorkspace(ProgramDocument d)
    {
        var k = CardOf(d); var page = CardPage(d); var f = KartenKit.Format(k.Format);
        var sp = new StackPanel { Spacing = 12, Margin = new Thickness(4, 0, 10, 4) };

        // Seiten als kleine Vorschau zum Anklicken
        var strip = new WrapPanel { Name = "cards-pages" };
        foreach (var (id, nm) in KartenKit.Seiten(k))
        {
            var pid = id;
            var b = new Button { Name = "cards-page-" + id, Padding = new Thickness(6), Margin = new Thickness(0, 0, 8, 0), Background = id == page ? Brush.Parse("#33C9A227") : Brushes.Transparent,
                Content = new StackPanel { Spacing = 4, Children = { CardPageView(d, k, id, 72 / f.H, false, "cards-thumb-" + id), new TextBlock { Text = nm, Foreground = id == page ? Accent : Muted, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center } } } };
            b.Click += (_, _) => { d.Data["page"] = pid; d.Data["sel"] = 0; Show(CardKey); };
            strip.Children.Add(b);
        }
        sp.Children.Add(strip);

        // große Seite
        var scale = Math.Min(560 / f.W, 520 / f.H);
        sp.Children.Add(new StackPanel { Spacing = 6, Children = { CardPageView(d, k, page, scale, true, "cards-canvas"),
            new TextBlock { Text = $"{KartenKit.Seiten(k).First(s => s.Id == page).Name} · {f.Name} · {f.W:0} × {f.H:0} mm" + (f.Klapp ? $" · Druckbogen {f.FlatW:0} × {f.H:0} mm" : ""), Foreground = Muted, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center } } });

        // Gästeliste
        var st = Gaeste.Zaehlen(k.Gaeste);
        var g = new StackPanel { Name = "cards-guests", Spacing = 5 };
        g.Children.Add(H($"Gästeliste ({st.Eingeladen})", 14));
        var chips = new WrapPanel { Name = "cards-rsvp" };
        chips.Children.Add(BueroChip($"Zusagen: {st.Zusagen} ({st.PersonenZugesagt} Personen)", Brush.Parse("#1F4D35")));
        chips.Children.Add(BueroChip($"Vielleicht: {st.Vielleicht}", Brush.Parse("#4D3A1F")));
        chips.Children.Add(BueroChip($"Absagen: {st.Absagen}", Brush.Parse("#4D1F1F")));
        chips.Children.Add(BueroChip($"Offen: {st.Offen}", Brush.Parse("#2A3A55")));
        chips.Children.Add(BueroChip($"Höchstens {st.PersonenMoeglich} Personen", Brush.Parse("#2A3A55")));
        g.Children.Add(chips);
        var quick = new TextBox { Name = "cards-guest-quick", PlaceholderText = "z. B. „Familie Muster, 4“ oder „Erika Muster, erika@example.org“ – eine je Zeile", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 40, FontSize = 12.5 };
        var add = new Button { Name = "cards-guest-add", Content = "Eintragen", Padding = new Thickness(10, 4), VerticalAlignment = VerticalAlignment.Top };
        add.Click += (_, _) => ProgramRun(CardKey, () => CardAddGuests(d, quick.Text ?? ""));
        g.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,Auto"), Children = { quick, Pos(add, 2) } });
        if (k.Gaeste.Count > 0)
            g.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,70,8,130,8,*,8,*,8,Auto"), Children = { P("Name", Muted, 11), Pos(P("Personen", Muted, 11), 2), Pos(P("Antwort", Muted, 11), 4), Pos(P("Gruppe / Tisch", Muted, 11), 6), Pos(P("Essen / Notiz", Muted, 11), 8) } });
        var i = 0;
        foreach (var gast in k.Gaeste)
        {
            var id = gast.Id; var n = i++;
            TextBox F(string key, string label, string value, Action<Gast, string> set, double w = double.NaN) => BueroBox($"cards-g-{n}-{key}", value, label, v => CardChange(d, label, x => { if (x.Gaeste.FirstOrDefault(y => y.Id == id) is { } y) set(y, v); }), false, w);
            g.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,70,8,130,8,*,8,*,8,Auto"), Children = {
                F("name", "Name", gast.Name, (y, v) => y.Name = v),
                Pos(F("n", "Personen", gast.Personen.ToString(CultureInfo.InvariantCulture), (y, v) => y.Personen = int.TryParse(v, out var p) && p is >= 1 and <= 50 ? p : y.Personen), 2),
                Pos(BueroCombo(CardKey, $"cards-g-{n}-status", Gaeste.Status, gast.Status, v => CardChange(d, "Antwort", x => { if (x.Gaeste.FirstOrDefault(y => y.Id == id) is { } y) y.Status = v; }), 120), 4),
                Pos(F("gruppe", "Gruppe", gast.Gruppe, (y, v) => y.Gruppe = v), 6), Pos(F("essen", "z. B. vegetarisch", gast.Essen, (y, v) => y.Essen = v), 8),
                Pos(KrBtn($"cards-g-{n}-del", "✕", () => CardChange(d, "Gast entfernt", x => x.Gaeste.RemoveAll(y => y.Id == id)), key: CardKey), 10) } });
        }
        sp.Children.Add(KrCard(g, "cards-guests-card"));
        return sp;
    }

    static readonly string[] CardSwatches = ["#FFFFFF", "#FFF4E0", "#FBF8F3", "#F0FDF4", "#EFF6FF", "#FDF2F8", "#1E293B", "#7F1D1D", "#14532D", "#1F2937"];

    Control CardProperties(ProgramDocument d)
    {
        var k = CardOf(d); var page = CardPage(d); var s = k.Seite(page); var sel = CardSel(d);
        var sp = new StackPanel { Name = "cards-props", Spacing = 6 };

        // Text
        sp.Children.Add(H("Text", 13));
        if (sel >= 0 && sel < s.Texte.Count)
        {
            var t = s.Texte[sel];
            void Set(string label, Action<KartenText> a) => CardChange(d, label, x => { var tt = x.Seite(page).Texte; if (sel < tt.Count) a(tt[sel]); });
            sp.Children.Add(BueroBox("cards-t-text", t.Text, "Text – {Name}, {Datum}, {Ort}, {AntwortBis} werden eingesetzt", v => { Set("Text", x => x.Text = v); }, true));
            sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
                BueroCombo(CardKey, "cards-t-font", KartenKit.Schriften.Select(x => (x, x)), t.Schrift, v => Set("Schrift", x => x.Schrift = v), 150),
                BueroBox("cards-t-size", t.Groesse.ToString("0.#", CultureInfo.InvariantCulture), "Größe in pt", v => { if (double.TryParse(v.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var z) && z is >= 4 and <= 120) Set("Schriftgröße", x => x.Groesse = z); }, false, 56) } });
            sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
                BueroBox("cards-t-color", t.Farbe, "Farbe als #RRGGBB", v => { if (KartenKit.IsHex(v)) Set("Schriftfarbe", x => x.Farbe = v.Trim().ToUpperInvariant()); }, false, 90),
                BueroCombo(CardKey, "cards-t-align", [("mitte", "Mitte"), ("links", "Links"), ("rechts", "Rechts")], t.Ausrichtung, v => Set("Ausrichtung", x => x.Ausrichtung = v), 90) } });
            var bold = new CheckBox { Name = "cards-t-bold", Content = "Fett", IsChecked = t.Fett, Foreground = Text, FontSize = 12 };
            bold.IsCheckedChanged += (_, _) => { if ((bold.IsChecked == true) != t.Fett) ProgramRun(CardKey, () => Set("Fett", x => x.Fett = bold.IsChecked == true)); };
            var ital = new CheckBox { Name = "cards-t-italic", Content = "Kursiv", IsChecked = t.Kursiv, Foreground = Text, FontSize = 12 };
            ital.IsCheckedChanged += (_, _) => { if ((ital.IsChecked == true) != t.Kursiv) ProgramRun(CardKey, () => Set("Kursiv", x => x.Kursiv = ital.IsChecked == true)); };
            sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { bold, ital } });
            var y = new Slider { Name = "cards-t-y", Minimum = 0, Maximum = 0.95, Value = t.Y, Width = 200 };
            y.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty && Math.Abs(y.Value - t.Y) > 0.005) { var v = Math.Round(y.Value, 3); ProgramRun(CardKey, () => Set("Höhe", x => x.Y = v)); } };
            sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Text = "Höhe", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, y } });
            sp.Children.Add(KrBtn("cards-t-del", "Text löschen", () => { CardChange(d, "Text gelöscht", x => { var tt = x.Seite(page).Texte; if (sel < tt.Count) tt.RemoveAt(sel); }); d.Data["sel"] = 0; }, key: CardKey));
        }
        else sp.Children.Add(P("Auf dieser Seite ist kein Text gewählt. Oben „＋ Text“ oder auf einen Text klicken.", Muted, 11));

        // Seite
        sp.Children.Add(H("Seite: " + KartenKit.Seiten(k).First(x => x.Id == page).Name, 13));
        sp.Children.Add(BueroLabeled("Hintergrund dieser Seite (leer = wie die Karte)", BueroBox("cards-page-bg", s.Hintergrund, "#RRGGBB", v => { if (v.Trim().Length == 0 || KartenKit.IsHex(v)) CardChange(d, "Seitenfarbe", x => x.Seite(page).Hintergrund = v.Trim().ToUpperInvariant()); })));
        sp.Children.Add(new WrapPanel { Children = {
            KrBtn("cards-img-add", s.Bild.Length > 0 ? "Bild ersetzen …" : "Bild einfügen …", () => _ = CardPickImage(d), key: CardKey).Also(b => b.Margin = new Thickness(0, 0, 6, 6)),
            s.Bild.Length > 0 ? KrBtn("cards-img-del", "Bild entfernen", () => CardChange(d, "Bild entfernt", x => x.Seite(page).Bild = ""), key: CardKey) : new Border() } });
        if (s.Bild.Length > 0) sp.Children.Add(BueroCombo(CardKey, "cards-img-mode", [("fuellen", "Ganze Seite (bis in den Beschnitt)"), ("oben", "Obere Hälfte"), ("mitte", "Eingepasst in der Mitte")], s.BildModus, v => CardChange(d, "Bildlage", x => x.Seite(page).BildModus = v), 220));

        // Karte
        sp.Children.Add(H("Karte", 13));
        sp.Children.Add(BueroCombo(CardKey, "cards-format", KartenKit.Formate.Select(x => (x.Id, $"{x.Name} – {x.Hint}")), k.Format, v => { CardChange(d, "Format", x => x.Format = v); d.Data["page"] = "vorne"; d.Data["sel"] = 0; }, 220));
        var sw = new WrapPanel();
        for (var i = 0; i < CardSwatches.Length; i++)
        {
            var c = CardSwatches[i];
            var b = new Button { Name = "cards-bg-" + i, Width = 24, Height = 24, Margin = new Thickness(0, 0, 4, 4), Background = Brush.Parse(c), BorderBrush = k.Hintergrund.Equals(c, StringComparison.OrdinalIgnoreCase) ? Accent : Brush.Parse("#55FFFFFF"), BorderThickness = new Thickness(2) };
            ToolTip.SetTip(b, "Grundfarbe " + c);
            b.Click += (_, _) => ProgramRun(CardKey, () => CardChange(d, "Grundfarbe", x => x.Hintergrund = c));
            sw.Children.Add(b);
        }
        sp.Children.Add(new TextBlock { Text = "Grundfarbe", Foreground = Muted, FontSize = 11 }); sp.Children.Add(sw);
        sp.Children.Add(BueroLabeled("Datum der Feier ({Datum})", BueroBox("cards-datum", k.Datum, "z. B. Samstag, 12.06.2027, 14 Uhr", v => CardChange(d, "Datum", x => x.Datum = v))));
        sp.Children.Add(BueroLabeled("Ort ({Ort})", BueroBox("cards-ort", k.Ort, "z. B. Gemeindesaal Musterstadt", v => CardChange(d, "Ort", x => x.Ort = v))));
        sp.Children.Add(BueroLabeled("Antwort bis ({AntwortBis})", BueroBox("cards-antwort", k.AntwortBis, "z. B. 01.05.2027", v => CardChange(d, "Antwort bis", x => x.AntwortBis = v))));

        // Druck
        sp.Children.Add(H("Druck", 13));
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
            new TextBlock { Text = "Beschnitt (mm)", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center },
            BueroBox("cards-bleed", k.Beschnitt.ToString("0.#", CultureInfo.InvariantCulture), "üblich 3 mm", v => { if (double.TryParse(v.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var z) && z is >= 0 and <= 10) CardChange(d, "Beschnitt", x => x.Beschnitt = z); }, false, 56) } });
        var marks = new CheckBox { Name = "cards-marks", Content = "Schnitt- und Falzmarken", IsChecked = k.Marken, Foreground = Text, FontSize = 12 };
        marks.IsCheckedChanged += (_, _) => { if ((marks.IsChecked == true) != k.Marken) ProgramRun(CardKey, () => CardChange(d, "Marken", x => x.Marken = marks.IsChecked == true)); };
        sp.Children.Add(marks);
        sp.Children.Add(H("Prüfung", 13));
        sp.Children.Add(BueroFindings("cards-findings", KartenKit.Pruefen(k), "Bereit zum Drucken."));
        return sp;
    }
}
