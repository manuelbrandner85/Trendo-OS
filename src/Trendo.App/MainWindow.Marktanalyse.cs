using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Business;
using Trendo.Core.Office;
using Trendo.Core.Programs;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>STUDIO-MARKET-01 Marktanalyse (T-0677): Wettbewerber, SWOT, Personas, TAM/SAM/SOM mit Rechnung, Preis-Leistungs-Karte – jede Zahl mit Quelle.
/// Clean-Room nach dem Bedienkonzept von Markt-Dashboards (Statista), SWOT-/Canvas-Werkzeugen und Google Trends (nur Konzept).
/// Recherche über „Sicher recherchieren“ (SYS-08), die Seite „Markt“ und den vorhandenen KI-Kern; Bericht über Trendo Text (PDF) und Trendo Präsentation.</summary>
public partial class MainWindow
{
    bool _mkAiBusy;
    static readonly CultureInfo MkDe = CultureInfo.GetCultureInfo("de-DE");

    public static Marktmappe MkOf(ProgramDocument d) => Marktmappe.FromJson(d.Data["mappe"]);
    public void MkChange(ProgramDocument d, string label, Action<Marktmappe> change) { var m = MkOf(d); change(m); var j = m.ToJson(); d.Edit(label, x => x["mappe"] = j); }
    static JsonObject MkDoc(Marktmappe m) => new() { ["mappe"] = m.ToJson(), ["tab"] = "ueberblick", ["sel"] = m.Wettbewerber.FirstOrDefault()?.Id ?? "" };

    private void InitMarktanalyse() => RegisterProgram(BusinessTabs, new ProgramSpec(MkKey, "STUDIO-MARKET-01", "Marktanalyse")
    {
        Sentence = "Den Markt verstehen: Wettbewerber, SWOT, Zielgruppen und Marktgröße mit nachvollziehbarer Rechnung – jede Zahl mit Quelle, als Bericht oder Präsentation.",
        Folder = "Marktanalyse",
        ScrollWorkspace = true,
        GroupLevels = new() { ["Recherche"] = 0, ["Hinzufügen"] = 0, ["Bericht"] = 0 },
        Templates =
        [
            new("leer", "Leere Analyse", "Thema eintragen und loslegen", () => MkDoc(new Marktmappe())),
            new("beispiel", "Beispiel: Kräuter-Abo", "Mit Wettbewerbern, SWOT, Persona und Rechnung", () => MkDoc(Marktanalyse.Beispiel(ShopNow))),
        ],
        Ribbon =
        [
            ("Recherche", [
                new ProgramCmd("mk-research", "Aus „Sicher recherchieren“", d => MkFromResearch(d), Tip: "Gelesene Seiten dieser Sitzung als Quellen übernehmen."),
                new ProgramCmd("mk-from-idea", "Aus „Markt“ übernehmen", d => MkFromIdea(d), Tip: "Mitbewerber deiner Geschäftsidee übernehmen."),
                new ProgramCmd("mk-ai", "Wettbewerber mit KI suchen", d => _ = MkAiCompetitors(d), Primary: true, Tip: "Vorschläge der KI – Preise nur mit Adresse, alles ungeprüft bis du bestätigst."),
            ]),
            ("Hinzufügen", [
                new ProgramCmd("mk-add-comp", "＋ Wettbewerber", d => MkAddCompetitor(d)),
                new ProgramCmd("mk-add-source", "＋ Quelle", d => MkAddSource(d)),
                new ProgramCmd("mk-add-persona", "＋ Persona", d => MkAddPersona(d)),
            ]),
            ("Bericht", [
                new ProgramCmd("mk-pdf", "Bericht (PDF)", d => MkPdfNow(d)),
                new ProgramCmd("mk-slides", "In Präsentation", d => MkToSlides(d), Tip: "Folien in „Trendo Präsentation“ – dort gestalten und vorführen."),
                new ProgramCmd("mk-check", "Quellen prüfen", d => d.Data["tab"] = "quellen"),
            ]),
        ],
        Exports =
        [
            new("md", "Bericht (Markdown + Karte als SVG)", ".md", (d, f) => MkMarkdown(d, f)),
            new("pdf", "Bericht (PDF)", ".pdf", (d, f) => { TextPdf.Export(Marktanalyse.Dokument(MkOf(d), ShopNow, MkChartPng(MkOf(d))), f); return f; }),
            new("pptx", "Präsentation (PowerPoint)", ".pptx", (d, f) => { SlideFiles.Save(SlideOutline.FromOutline(Marktanalyse.Gliederung(MkOf(d)), SlideThemes.Get("schiefer")), f); return f; }),
            new("csv", "Wettbewerber (CSV)", ".csv", (d, f) => { File.WriteAllText(f, Marktanalyse.Csv(MkOf(d)), new UTF8Encoding(true)); return f; }),
            new("svg", "Positionierungskarte (SVG)", ".svg", (d, f) => { File.WriteAllText(f, Marktanalyse.KarteSvg(MkOf(d)), new UTF8Encoding(false)); return f; }),
        ],
        SimpleExports = 2,
        Workspace = MkWorkspace,
        Properties = MkProperties,
        Status = d =>
        {
            var m = MkOf(d); var g = Marktanalyse.Groesse(m); var open = Marktanalyse.Pruefen(m, ShopNow).Count(x => x.Fehler);
            return $"{m.Wettbewerber.Count} Wettbewerber · {m.Quellen.Count} Quellen · SOM {(g.Som is { } s ? Marktanalyse.Zahl(s) + " " + m.Waehrung : "offen")}" + (open > 0 ? $" · ⚠ {open} Zahlen ohne Quelle" : " · alle Zahlen belegt");
        },
        Help = "Oben das Thema eintragen (rechts). Unter „Wettbewerber“ Anbieter mit Preis, Angebot, Stärken und Schwächen sammeln – jeder Preis braucht eine Quelle (Preisliste, Webseite). „SWOT“, „Personas“ und „Marktgröße“ ergänzen: "
             + "TAM = Kunden × Umsatz je Kunde, SAM = erreichbarer Anteil, SOM = realistischer Marktanteil. Jede Zahl bekommt eine Quelle oder wird ausdrücklich als eigene Annahme gekennzeichnet – sonst steht „⚠ Quelle fehlt“ da. "
             + "Quellen kommen aus „Sicher recherchieren“, aus der Seite „Markt“ oder als KI-Vorschlag (ungeprüft, bis du die Seite geöffnet und bestätigt hast). Bericht als PDF oder Markdown, Folien in „Trendo Präsentation“.",
    }, "marktanalyse", "markt", "wettbewerb", "wettbewerber", "konkurrenzanalyse", "swot", "persona", "zielgruppe", "tam sam som", "marktgröße", "positionierung", "statista");

    // ---------- Befehle ----------

    public Wettbewerber MkAddCompetitor(ProgramDocument d)
    {
        var w = new Wettbewerber { Id = Marktanalyse.NeueId("w"), Name = "Neuer Wettbewerber" };
        MkChange(d, "Wettbewerber", m => m.Wettbewerber.Add(w)); d.Data["sel"] = w.Id; d.Data["tab"] = "wettbewerber";
        _progMsg[MkKey] = "Neuer Wettbewerber – rechts Name, Preis mit Quelle, Angebot, Stärken und Schwächen eintragen.";
        return w;
    }

    public MarktQuelle MkAddSource(ProgramDocument d, string titel = "", string url = "", string herausgeber = "", string art = "Webseite")
    {
        var q = new MarktQuelle { Id = Marktanalyse.NeueId("q"), Titel = titel, Url = url, Herausgeber = herausgeber, Art = art, Datum = ShopNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) };
        MkChange(d, "Quelle", m => m.Quellen.Add(q)); d.Data["tab"] = "quellen"; d.Data["selq"] = q.Id;
        _progMsg[MkKey] = "Neue Quelle – Titel, Herausgeber und Adresse eintragen.";
        return q;
    }

    public Persona MkAddPersona(ProgramDocument d)
    {
        var p = new Persona { Id = Marktanalyse.NeueId("p"), Name = "Neue Persona" };
        MkChange(d, "Persona", m => m.Personas.Add(p)); d.Data["tab"] = "personas";
        _progMsg[MkKey] = "Neue Persona – eine Rolle beschreiben, keine echte Person. „Grundlage“: Interviews, Umfrage oder Annahme.";
        return p;
    }

    public int MkFromResearch(ProgramDocument d)
    {
        var pages = Desk.SessionSources().Select(s => (s.Title, s.Url, s.At)).ToList();
        var n = 0; MkChange(d, "Aus der Recherche", m => n = Marktanalyse.AusRecherche(m, pages));
        d.Data["tab"] = "quellen";
        _progMsg[MkKey] = n == 0 ? "In „Sicher recherchieren“ wurden in dieser Sitzung keine neuen Seiten gelesen – dort Statistiken und Preislisten öffnen, dann hier übernehmen." : $"{n} gelesene Seiten als Quellen übernommen – jetzt den Zahlen zuordnen.";
        return n;
    }

    public int MkFromIdea(ProgramDocument d)
    {
        var comps = Competitors.All(); var n = 0;
        MkChange(d, "Aus „Markt“", m => { n = Marktanalyse.AusGeschaeftsidee(m, comps); if (m.Thema.Length == 0) m.Thema = BizIdea.Name; if (m.EigenerPreis <= 0 && BizIdea.Price > 0) m.EigenerPreis = BizIdea.Price; });
        d.Data["tab"] = "wettbewerber";
        _progMsg[MkKey] = n == 0 ? "Auf der Seite „Markt“ sind keine neuen Mitbewerber eingetragen." : $"{n} Mitbewerber aus „Markt“ übernommen – Preise ohne Quelle sind markiert.";
        return n;
    }

    public async Task MkAiCompetitors(ProgramDocument d)
    {
        if (_mkAiBusy) return;
        var m = MkOf(d);
        if (m.Thema.Trim().Length == 0) { _progMsg[MkKey] = "Erst rechts das Thema eintragen, z. B. „Kräuter-Abo für die Fensterbank“."; Show(MkKey); return; }
        _mkAiBusy = true; _progMsg[MkKey] = "Die KI sucht Wettbewerber …"; Show(MkKey);
        string? text;
        try { text = await ShopAsk(Marktanalyse.RecherchePrompt(m)); }
        finally { _mkAiBusy = false; }
        if (string.IsNullOrWhiteSpace(text)) { _progMsg[MkKey] = "Keine KI erreichbar – Wettbewerber selbst eintragen oder in „Sicher recherchieren“ suchen. KI einrichten unter „KI fragen“."; Show(MkKey); return; }
        (int Neu, int PreiseVerworfen) r = default;
        MkChange(d, "KI-Vorschläge", x => r = Marktanalyse.KiEinlesen(x, text, ShopNow));
        d.Data["tab"] = "wettbewerber";
        _progMsg[MkKey] = $"{r.Neu} Vorschläge der KI übernommen – alle ungeprüft: Seiten öffnen und unter „Quellen“ bestätigen." + (r.PreiseVerworfen > 0 ? $" {r.PreiseVerworfen} Preis(e) ohne Adresse nicht übernommen." : "");
        Show(MkKey);
    }

    byte[] MkChartPng(Marktmappe m, int w = 1200, int h = 840)
    {
        var k = Marktanalyse.Karte(m);
        using var surf = SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(w, h, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Premul));
        var c = surf.Canvas; c.Clear(SkiaSharp.SKColors.White);
        const float L = 100, R = 40, T = 40, B = 90; float pw = w - L - R, ph = h - T - B;
        using var frame = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColor.Parse("#CBD5E1"), Style = SkiaSharp.SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = true };
        using var fill = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColor.Parse("#F8FAFC") };
        using var dash = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColor.Parse("#CBD5E1"), Style = SkiaSharp.SKPaintStyle.Stroke, StrokeWidth = 2, PathEffect = SkiaSharp.SKPathEffect.CreateDash([8, 8], 0) };
        c.DrawRect(L, T, pw, ph, fill); c.DrawRect(L, T, pw, ph, frame);
        c.DrawLine(L + pw / 2, T, L + pw / 2, T + ph, dash); c.DrawLine(L, T + ph / 2, L + pw, T + ph / 2, dash);
        using var font = new SkiaSharp.SKFont(SkiaSharp.SKTypeface.FromFamilyName("Segoe UI") ?? SkiaSharp.SKTypeface.Default, 22);
        using var ink = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColor.Parse("#0F172A"), IsAntialias = true };
        using var muted = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColor.Parse("#475569"), IsAntialias = true };
        c.DrawText($"Preis ({m.Waehrung}) → {Marktanalyse.Zahl(k.PreisMin, 2)} … {Marktanalyse.Zahl(k.PreisMax, 2)}", L + pw / 2, h - 30, SkiaSharp.SKTextAlign.Center, font, muted);
        c.Save(); c.RotateDegrees(-90, 36, T + ph / 2); c.DrawText("Leistung (eigene Einschätzung) →", 36, T + ph / 2, SkiaSharp.SKTextAlign.Center, font, muted); c.Restore();
        foreach (var p in k.Punkte)
        {
            float x = L + (float)p.X * pw, y = T + (1 - (float)p.Y) * ph;
            using var dot = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColor.Parse(p.Eigen ? "#C9A227" : "#2563EB"), IsAntialias = true };
            using var ring = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColors.White, Style = SkiaSharp.SKPaintStyle.Stroke, StrokeWidth = 3, IsAntialias = true };
            c.DrawCircle(x, y, p.Eigen ? 14 : 11, dot); c.DrawCircle(x, y, p.Eigen ? 14 : 11, ring);
            c.DrawText(p.Name, x + 18, y + 8, SkiaSharp.SKTextAlign.Left, font, ink);
        }
        using var img = surf.Snapshot(); using var data = img.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    string MkMarkdown(ProgramDocument d, string f)
    {
        var m = MkOf(d); var svg = Path.ChangeExtension(f, null) + "-karte.svg";
        File.WriteAllText(svg, Marktanalyse.KarteSvg(m), new UTF8Encoding(false));
        File.WriteAllText(f, Marktanalyse.Markdown(m, ShopNow, Path.GetFileName(svg)), new UTF8Encoding(false));
        return f;
    }

    public string MkPdfNow(ProgramDocument d)
    {
        var m = MkOf(d); var f = ShopFile("Marktanalyse", "Marktanalyse " + (m.Thema.Length > 0 ? m.Thema : d.Title), ".pdf");
        var pages = TextPdf.Export(Marktanalyse.Dokument(m, ShopNow, MkChartPng(m)), f);
        var open = Marktanalyse.Pruefen(m, ShopNow).Count(x => x.Fehler);
        _progMsg[MkKey] = $"Bericht mit {pages} Seite(n): {f}" + (open > 0 ? $" – ⚠ {open} Punkte ohne Quelle sind im Bericht markiert." : "");
        NotifyFileResult(_progMsg[MkKey]); return f;
    }

    /// <summary>Folien an „Trendo Präsentation“ übergeben (deren Kern und Oberfläche).</summary>
    public void MkToSlides(ProgramDocument d)
    {
        DeckFromOutline(Marktanalyse.Gliederung(MkOf(d)));
        _progMsg[MkKey] = $"{Deck.Slides.Count} Folien an „Trendo Präsentation“ übergeben.";
        Avalonia.Threading.Dispatcher.UIThread.Post(() => Show("praesentation"));
    }

    void MkSetComp(ProgramDocument d, string id, string label, Action<Wettbewerber> a) => MkChange(d, label, m => { if (m.Wettbewerber.FirstOrDefault(x => x.Id == id) is { } w) a(w); });

    static double? MkNum(string t) => Trendo.Core.PromptAction.MarktgroesseAdapter.Num(t);

    /// <summary>Auswahl der Herkunft einer Zahl: keine, eigene Annahme oder eine Quelle der Liste.</summary>
    ComboBox MkSourcePick(ProgramDocument d, string name, Marktmappe m, Kennzahl k, Action<Marktmappe, string> set)
    {
        var items = new List<(string, string)> { ("", "⚠ ohne Quelle"), ("annahme", "eigene Annahme") };
        for (var i = 0; i < m.Quellen.Count; i++) items.Add((m.Quellen[i].Id, $"[Q{i + 1}] {(m.Quellen[i].Titel.Length > 0 ? m.Quellen[i].Titel : m.Quellen[i].Url)}"));
        var cur = k.Annahme ? "annahme" : k.QuelleId;
        return ShopCombo(MkKey, name, items, cur, v => MkChange(d, "Herkunft", x => set(x, v)), 200);
    }

    static void MkApplySource(Kennzahl k, string v) { k.Annahme = v == "annahme"; k.QuelleId = v is "" or "annahme" ? "" : v; }

    // ---------- Oberfläche ----------

    Control MkWorkspace(ProgramDocument d)
    {
        var m = MkOf(d); var tab = d.Text("tab", "ueberblick");
        var tabs = new WrapPanel { Name = "mk-tabs", Margin = new Thickness(0, 0, 0, 10) };
        foreach (var (id, label) in new[] { ("ueberblick", "Überblick"), ("wettbewerber", "Wettbewerber"), ("swot", "SWOT"), ("personas", "Personas"), ("groesse", "Marktgröße"), ("quellen", "Quellen") })
        {
            var t = id; var b = KrBtn("mk-tab-" + id, label, () => d.Data["tab"] = t, id == tab, MkKey); b.Margin = new Thickness(0, 0, 6, 6); tabs.Children.Add(b);
        }
        Control body = tab switch { "wettbewerber" => MkCompetitors(d, m), "swot" => MkSwot(d, m), "personas" => MkPersonas(d, m), "groesse" => MkSize(d, m), "quellen" => MkSources(d, m), _ => MkOverview(m) };
        return new StackPanel { Margin = new Thickness(4, 0, 10, 4), Children = { tabs, body } };
    }

    Control MkOverview(Marktmappe m)
    {
        var g = Marktanalyse.Groesse(m); var pr = Marktanalyse.Pruefen(m, ShopNow); var bad = pr.Count(x => x.Fehler);
        var sp = new StackPanel { Name = "mk-overview", Spacing = 12 };
        string Money(double? v) => v is { } x ? (x >= 1e9 ? (x / 1e9).ToString("0.0", MkDe) + " Mrd." : x >= 1e6 ? (x / 1e6).ToString("0.0", MkDe) + " Mio." : Marktanalyse.Zahl(x)) + " " + m.Waehrung : "offen";
        sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,10,*,10,*,10,*"), Children = {
            ShopTile("mk-kpi-tam", Money(g.Tam), "TAM – Gesamtmarkt im Jahr"),
            Pos(ShopTile("mk-kpi-sam", Money(g.Sam), "SAM – erreichbar"), 2),
            Pos(ShopTile("mk-kpi-som", Money(g.Som), "SOM – realistisch in 3 Jahren", Brush.Parse("#C9A227")), 4),
            Pos(ShopTile("mk-kpi-sources", bad == 0 ? "✓ belegt" : $"⚠ {bad}", bad == 0 ? $"{m.Quellen.Count} Quellen" : "Zahlen ohne Quelle", bad == 0 ? K4Good : K4Bad), 6) } });
        sp.Children.Add(KrCard(new StackPanel { Spacing = 6, Children = { H("Preis-Leistungs-Karte", 14), MkMap(m), P(Marktanalyse.Karte(m).Lage, Text, 12),
            P(Marktanalyse.Karte(m).Luecken.Count > 0 ? "Freie Felder: " + string.Join(", ", Marktanalyse.Karte(m).Luecken) + " – mögliche Lücke, Leistung ist eigene Einschätzung." : "", K4Good, 11.5) } }, "mk-map-card"));
        return sp;
    }

    Control MkMap(Marktmappe m)
    {
        var k = Marktanalyse.Karte(m); const double W = 560, Hh = 340;
        var cv = new Canvas { Name = "mk-map", Width = W, Height = Hh, Background = Brush.Parse("#0F172A"), HorizontalAlignment = HorizontalAlignment.Left };
        cv.Children.Add(new Line { StartPoint = new Point(W / 2, 0), EndPoint = new Point(W / 2, Hh), Stroke = Brush.Parse("#334155"), StrokeDashArray = [4, 4] });
        cv.Children.Add(new Line { StartPoint = new Point(0, Hh / 2), EndPoint = new Point(W, Hh / 2), Stroke = Brush.Parse("#334155"), StrokeDashArray = [4, 4] });
        void Label(string t, double x, double y) { var tb = new TextBlock { Text = t, Foreground = Brush.Parse("#64748B"), FontSize = 10.5 }; Canvas.SetLeft(tb, x); Canvas.SetTop(tb, y); cv.Children.Add(tb); }
        Label("günstig · stark", 6, 4); Label("teuer · stark", W - 80, 4); Label("günstig · einfach", 6, Hh - 18); Label("teuer · einfach", W - 86, Hh - 18);
        var i = 0;
        foreach (var p in k.Punkte)
        {
            double x = 20 + p.X * (W - 40), y = 20 + (1 - p.Y) * (Hh - 40);
            var dot = new Ellipse { Name = "mk-dot-" + i++, Width = p.Eigen ? 16 : 12, Height = p.Eigen ? 16 : 12, Fill = p.Eigen ? Accent : Brush.Parse("#60A5FA"), Stroke = Brushes.White, StrokeThickness = 1.5 };
            ToolTip.SetTip(dot, $"{p.Name}: {Marktanalyse.Zahl(p.Preis, 2)} {m.Waehrung}, Leistung {p.Leistung}/10");
            Canvas.SetLeft(dot, x - dot.Width / 2); Canvas.SetTop(dot, y - dot.Height / 2); cv.Children.Add(dot);
            var tb = new TextBlock { Text = p.Name, Foreground = p.Eigen ? Accent : Text, FontSize = 11.5, FontWeight = p.Eigen ? FontWeight.Bold : FontWeight.Normal };
            Canvas.SetLeft(tb, x + 10); Canvas.SetTop(tb, y - 8); cv.Children.Add(tb);
        }
        if (k.Punkte.Count == 0) Label("Noch keine Preise – Wettbewerber mit Preis eintragen.", W / 2 - 150, Hh / 2 + 6);
        return cv;
    }

    Control MkCompetitors(ProgramDocument d, Marktmappe m)
    {
        var sel = d.Text("sel"); var list = new StackPanel { Name = "mk-comp-list", Spacing = 4 };
        list.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("2*,110,2*,2*,2*,60"), Margin = new Thickness(10, 0), Children = {
            P("Name", Muted, 11), Pos(P("Preis", Muted, 11), 1), Pos(P("Angebot", Muted, 11), 2), Pos(P("Stärken", Muted, 11), 3), Pos(P("Schwächen", Muted, 11), 4), Pos(P("Leistung", Muted, 11), 5) } });
        if (m.Wettbewerber.Count == 0) list.Children.Add(P("Noch keine Wettbewerber. Oben „＋ Wettbewerber“, „Aus „Markt“ übernehmen“ oder „Wettbewerber mit KI suchen“.", Muted, 12));
        var i = 0;
        foreach (var w in m.Wettbewerber)
        {
            var id = w.Id; var n = i++; var her = w.Preis.Wert is null ? "" : Marktanalyse.Herkunft(m, w.Preis);
            var row = new Button
            {
                Name = "mk-comp-" + n, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(10, 6),
                Background = id == sel ? Brush.Parse("#33C9A227") : Brush.Parse("#14FFFFFF"),
                Content = new Grid { ColumnDefinitions = new ColumnDefinitions("2*,110,2*,2*,2*,60"), Children = {
                    new StackPanel { Children = { new TextBlock { Text = w.Name, Foreground = Text, FontSize = 12.5, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                        new TextBlock { Text = w.Notiz.Length > 0 ? w.Notiz : w.Url, Foreground = w.Notiz.Length > 0 ? K4Warn : Muted, FontSize = 10.5, TextTrimming = TextTrimming.CharacterEllipsis } } },
                    Pos(new StackPanel { Children = { new TextBlock { Text = w.Preis.Wert is { } p ? Marktanalyse.Zahl(p, 2) + " " + m.Waehrung : "–", Foreground = Text, FontSize = 12 },
                        new TextBlock { Name = $"mk-comp-{n}-src", Text = her, Foreground = her.StartsWith('⚠') || her.Contains("ungeprüft") ? K4Bad : Muted, FontSize = 10.5 } } }, 1),
                    Pos(new TextBlock { Text = w.Angebot, Foreground = Text, FontSize = 11.5, TextWrapping = TextWrapping.Wrap }, 2),
                    Pos(new TextBlock { Text = w.Staerken, Foreground = K4Good, FontSize = 11.5, TextWrapping = TextWrapping.Wrap }, 3),
                    Pos(new TextBlock { Text = w.Schwaechen, Foreground = K4Bad, FontSize = 11.5, TextWrapping = TextWrapping.Wrap }, 4),
                    Pos(new TextBlock { Text = w.Leistung + "/10", Foreground = Muted, FontSize = 11.5 }, 5) } },
            };
            row.Click += (_, _) => { d.Data["sel"] = id; Show(MkKey); };
            list.Children.Add(row);
        }
        var comps = m.Wettbewerber.Where(c => c.Preis.Wert is > 0).Select(c => new Competitor(c.Name, c.Url, c.Preis.Wert!.Value, c.Staerken, c.Schwaechen, c.Angebot)).ToList();
        if (BusinessMarket.Prices(comps, m.EigenerPreis) is { } pp && pp.CommonWeaknesses.Count > 0)
            list.Children.Add(P("Lücke im Markt – mehrere Wettbewerber schwach bei: " + string.Join(", ", pp.CommonWeaknesses.Select(x => x.Word)), K4Good, 11.5));
        return KrCard(list, "mk-comp-card");
    }

    Control MkSwot(ProgramDocument d, Marktmappe m)
    {
        var grid = new Grid { Name = "mk-swot", ColumnDefinitions = new ColumnDefinitions("*,10,*"), RowDefinitions = new RowDefinitions("Auto,10,Auto") };
        var colors = new Dictionary<string, string> { ["staerken"] = "#4ADE80", ["schwaechen"] = "#F87171", ["chancen"] = "#60A5FA", ["risiken"] = "#F5A524" };
        var k = 0;
        foreach (var (id, name) in Marktanalyse.SwotFelder)
        {
            var fid = id; var items = m.Swot.Feld(id);
            var sp = new StackPanel { Spacing = 4, Children = { new TextBlock { Text = name, Foreground = Brush.Parse(colors[id]), FontSize = 14, FontWeight = FontWeight.SemiBold } } };
            var j = 0;
            foreach (var p in items)
            {
                var idx = j++;
                sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = {
                    new TextBlock { Text = "• " + p.Text + (p.QuelleId.Length > 0 ? " " + Marktanalyse.Marke(m, p.QuelleId) : ""), Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap },
                    Pos(KrBtn($"mk-swot-{id}-del-{idx}", "✕", () => MkChange(d, "SWOT-Punkt entfernt", x => x.Swot.Feld(fid).RemoveAt(idx)), false, MkKey).Also(b => b.Padding = new Thickness(6, 0)), 1) } });
            }
            var box = new TextBox { Name = $"mk-swot-{id}-new", PlaceholderText = "Neuer Punkt – Enter", FontSize = 12 };
            OnEnter(box, () => ProgramRun(MkKey, () => MkSwotAdd(d, fid, box.Text ?? "")));
            sp.Children.Add(box);
            var card = KrCard(sp, "mk-swot-" + id); Grid.SetColumn(card, (k % 2) * 2); Grid.SetRow(card, (k / 2) * 2); grid.Children.Add(card); k++;
        }
        return new StackPanel { Spacing = 8, Children = { grid, P("Punkte mit Zahlen (z. B. „30 % wachsen“) brauchen eine Quelle – in den Text „[Q1]“ schreiben ordnet die Quelle 1 zu.", Muted, 11) } };
    }

    /// <summary>SWOT-Punkt hinzufügen; „[Qn]“ im Text ordnet die n-te Quelle zu.</summary>
    public void MkSwotAdd(ProgramDocument d, string feld, string text)
    {
        var t = text.Trim(); if (t.Length == 0) throw new ArgumentException("Bitte einen Punkt eintragen.");
        MkChange(d, "SWOT-Punkt", m =>
        {
            var q = ""; var mm = System.Text.RegularExpressions.Regex.Match(t, @"\[Q(\d+)\]");
            if (mm.Success && int.TryParse(mm.Groups[1].Value, out var n) && n >= 1 && n <= m.Quellen.Count) { q = m.Quellen[n - 1].Id; t = t.Replace(mm.Value, "").Trim(); }
            m.Swot.Feld(feld).Add(new SwotPunkt { Text = t, QuelleId = q });
        });
    }

    Control MkPersonas(ProgramDocument d, Marktmappe m)
    {
        var wrap = new WrapPanel { Name = "mk-personas" };
        if (m.Personas.Count == 0) wrap.Children.Add(P("Noch keine Personas. Oben „＋ Persona“.", Muted, 12));
        var i = 0;
        foreach (var p in m.Personas)
        {
            var id = p.Id; var n = i++;
            void Set(string label, Action<Persona> a) => MkChange(d, label, x => { if (x.Personas.FirstOrDefault(y => y.Id == id) is { } y) a(y); });
            var sp = new StackPanel { Spacing = 4, Width = 300 };
            sp.Children.Add(ShopBox($"mk-persona-{n}-name", p.Name, "Rollenname, z. B. „Die Stadtgärtnerin“", t => Set("Persona", y => y.Name = t.Trim())).Also(b => b.FontWeight = FontWeight.SemiBold));
            foreach (var (key, label, val, set) in new (string, string, string, Action<Persona, string>)[] {
                ("alter", "Alter", p.Alter, (y, t) => y.Alter = t), ("lage", "Lebenslage", p.Lebenslage, (y, t) => y.Lebenslage = t), ("ziele", "Ziele", p.Ziele, (y, t) => y.Ziele = t),
                ("probleme", "Probleme", p.Probleme, (y, t) => y.Probleme = t), ("kanaele", "Erreichbar über", p.Kanaele, (y, t) => y.Kanaele = t),
                ("zahlt", "Zahlungsbereitschaft", p.Zahlungsbereitschaft, (y, t) => y.Zahlungsbereitschaft = t) })
            { var st = set; sp.Children.Add(ShopLabeled(label, ShopBox($"mk-persona-{n}-{key}", val, label, t => Set(label, y => st(y, t.Trim()))))); }
            sp.Children.Add(ShopLabeled("Grundlage", ShopCombo(MkKey, $"mk-persona-{n}-basis", [("Annahme", "Annahme"), ("Interviews", "Interviews"), ("Umfrage", "Umfrage"), ("Verkaufsdaten", "Verkaufsdaten")], p.Grundlage, t => Set("Grundlage", y => y.Grundlage = t), 150)));
            sp.Children.Add(KrBtn($"mk-persona-{n}-del", "Entfernen", () => MkChange(d, "Persona entfernt", x => x.Personas.RemoveAll(y => y.Id == id)), false, MkKey));
            wrap.Children.Add(KrCard(sp).Also(c => c.Margin = new Thickness(0, 0, 10, 10)));
        }
        return wrap;
    }

    Control MkSize(ProgramDocument d, Marktmappe m)
    {
        var g = m.Groesse; var sp = new StackPanel { Name = "mk-size", Spacing = 8 };
        sp.Children.Add(H("Marktgröße (TAM → SAM → SOM)", 14));
        void Row(string key, string label, string hint, Kennzahl k, Func<Marktgroesse, Kennzahl> pick)
        {
            var box = ShopBox($"mk-size-{key}", k.Wert is { } v ? v.ToString("#,##0.##", MkDe) : "", hint, t => MkChange(d, label, x => pick(x.Groesse).Wert = MkNum(t)), false, 160);
            sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("240,170,*"), Children = {
                new TextBlock { Text = label, Foreground = Text, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap }, Pos(box, 1),
                Pos(MkSourcePick(d, $"mk-size-{key}-src", m, k, (x, v) => MkApplySource(pick(x.Groesse), v)), 2) } });
        }
        Row("kunden", "Mögliche Kunden im Gesamtmarkt", "z. B. 41.000.000", g.Kunden, x => x.Kunden);
        Row("umsatz", $"Umsatz je Kunde und Jahr ({m.Waehrung})", "z. B. 60", g.UmsatzJeKunde, x => x.UmsatzJeKunde);
        Row("sam", "Davon erreichbar (%) → SAM", "z. B. 5", g.SamAnteil, x => x.SamAnteil);
        Row("som", "Realistischer Marktanteil (%) → SOM", "z. B. 1", g.SomAnteil, x => x.SomAnteil);
        var r = Marktanalyse.Groesse(m);
        var calc = new StackPanel { Name = "mk-size-calc", Spacing = 3 };
        foreach (var z in r.Zeilen) calc.Children.Add(new TextBlock { Text = z, Foreground = z.Contains('⚠') ? K4Bad : Text, FontSize = 12.5, TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Consolas, Segoe UI") });
        foreach (var f in r.Fehlt) calc.Children.Add(P("⚠ " + f, K4Warn, 11.5));
        sp.Children.Add(KrCard(calc, "mk-size-card"));
        sp.Children.Add(P("Top-down: erst alle möglichen Kunden (Statistik mit Quelle), dann der Teil, den dein Angebot und Vertrieb erreicht, dann ein ehrlicher Marktanteil. Annahmen sind erlaubt – aber als Annahme gekennzeichnet.", Muted, 11));
        return KrCard(sp);
    }

    Control MkSources(ProgramDocument d, Marktmappe m)
    {
        var sp = new StackPanel { Name = "mk-sources", Spacing = 6 };
        var pr = Marktanalyse.Pruefen(m, ShopNow);
        var chk = new StackPanel { Name = "mk-check", Spacing = 3, Children = { H("Prüfung: jede Zahl mit Herkunft", 14) } };
        if (pr.Count == 0) chk.Children.Add(P("✓ Alle Zahlen haben eine Quelle oder sind als Annahme gekennzeichnet.", K4Good, 12));
        var pi = 0;
        foreach (var p in pr) chk.Children.Add(new TextBlock { Name = $"mk-check-{pi++}", Text = (p.Fehler ? "⚠ " : "◐ ") + p.Wo + ": " + p.Text, Foreground = p.Fehler ? K4Bad : K4Warn, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        sp.Children.Add(KrCard(chk, "mk-check-card"));
        var list = new StackPanel { Spacing = 6, Children = { new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,6,Auto"), Children = { H($"Quellen ({m.Quellen.Count})", 14),
            Pos(KrBtn("mk-src-research", "Aus Recherche", () => MkFromResearch(d), false, MkKey), 1), Pos(KrBtn("mk-src-add", "＋ Quelle", () => MkAddSource(d), true, MkKey), 3) } } } };
        for (var i = 0; i < m.Quellen.Count; i++)
        {
            var q = m.Quellen[i]; var id = q.Id; var n = i;
            void Set(string label, Action<MarktQuelle> a) => MkChange(d, label, x => { if (x.Quellen.FirstOrDefault(y => y.Id == id) is { } y) a(y); });
            var row = new StackPanel { Spacing = 4 };
            row.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("50,*,Auto,6,Auto,6,Auto"), Children = {
                new TextBlock { Text = $"[Q{n + 1}]", Foreground = Accent, FontSize = 12.5, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center },
                Pos(ShopBox($"mk-src-{n}-title", q.Titel, "Titel", t => Set("Quelle", y => y.Titel = t.Trim())), 1),
                Pos(q.Geprueft ? new TextBlock { Text = "✓ geprüft", Foreground = K4Good, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center }
                    : KrBtn($"mk-src-{n}-confirm", "Geprüft – stimmt", () => Set("Quelle bestätigt", y => y.Geprueft = true), true, MkKey), 2),
                Pos(KrBtn($"mk-src-{n}-open", "Öffnen", () => { if (!Uri.TryCreate(q.Url, UriKind.Absolute, out var u) || u.Scheme is not ("http" or "https")) throw new InvalidOperationException("Diese Quelle hat keine Web-Adresse."); Launch(u.ToString()); }, false, MkKey), 4),
                Pos(KrBtn($"mk-src-{n}-del", "✕", () => MkChange(d, "Quelle entfernt", x => x.Quellen.RemoveAll(y => y.Id == id)), false, MkKey), 6) } });
            row.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("50,*,8,2*,8,120,8,140"), Children = {
                Pos(ShopBox($"mk-src-{n}-pub", q.Herausgeber, "Herausgeber", t => Set("Herausgeber", y => y.Herausgeber = t.Trim())), 1),
                Pos(ShopBox($"mk-src-{n}-url", q.Url, "https://…", t => Set("Adresse", y => y.Url = t.Trim())), 3),
                Pos(ShopBox($"mk-src-{n}-date", q.Datum, "JJJJ-MM-TT", t => Set("Datum", y => y.Datum = t.Trim())), 5),
                Pos(ShopCombo(MkKey, $"mk-src-{n}-kind", Marktanalyse.Arten.Select(a => (a, a)), q.Art, t => Set("Art", y => y.Art = t), 130), 7) } });
            list.Children.Add(new Border { Padding = new Thickness(8), CornerRadius = new CornerRadius(6), Background = q.Geprueft ? Brush.Parse("#14FFFFFF") : Brush.Parse("#22F87171"), Child = row });
        }
        sp.Children.Add(KrCard(list, "mk-sources-card"));
        return sp;
    }

    Control MkProperties(ProgramDocument d)
    {
        var m = MkOf(d); var sp = new StackPanel { Name = "mk-props", Spacing = 6 };
        sp.Children.Add(H("Analyse", 13));
        sp.Children.Add(ShopLabeled("Thema / Angebot", ShopBox("mk-topic", m.Thema, "z. B. Kräuter-Abo für die Fensterbank", t => MkChange(d, "Thema", x => x.Thema = t.Trim()))));
        sp.Children.Add(ShopLabeled("Region", ShopBox("mk-region", m.Region, "Deutschland", t => MkChange(d, "Region", x => x.Region = t.Trim()))));
        sp.Children.Add(H("Eigenes Angebot (für die Karte)", 13));
        sp.Children.Add(ShopLabeled("Name", ShopBox("mk-own-name", m.EigenerName, "Unser Angebot", t => MkChange(d, "Eigener Name", x => x.EigenerName = t.Trim()))));
        sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,*"), Children = {
            ShopLabeled($"Preis ({m.Waehrung})", ShopBox("mk-own-price", m.EigenerPreis > 0 ? m.EigenerPreis.ToString("0.00", MkDe) : "", "z. B. 14,90", t => MkChange(d, "Eigener Preis", x => x.EigenerPreis = MkNum(t) ?? 0))),
            Pos(ShopLabeled("Leistung 1–10", ShopBox("mk-own-score", m.EigeneLeistung.ToString(CultureInfo.InvariantCulture), "6", t => { if (int.TryParse(t, out var n) && n is >= 1 and <= 10) MkChange(d, "Eigene Leistung", x => x.EigeneLeistung = n); })), 2) } });

        if (d.Text("tab") == "wettbewerber" && m.Wettbewerber.FirstOrDefault(w => w.Id == d.Text("sel")) is { } w)
        {
            var id = w.Id;
            sp.Children.Add(H("Wettbewerber", 13));
            sp.Children.Add(ShopLabeled("Name", ShopBox("mk-c-name", w.Name, "Firma oder Produkt", t => MkSetComp(d, id, "Name", x => x.Name = t.Trim()))));
            sp.Children.Add(ShopLabeled("Webseite", ShopBox("mk-c-url", w.Url, "https://…", t => MkSetComp(d, id, "Webseite", x => x.Url = t.Trim()))));
            sp.Children.Add(ShopLabeled($"Preis ({m.Waehrung})", ShopBox("mk-c-price", w.Preis.Wert is { } p ? p.ToString("0.00", MkDe) : "", "z. B. 19,90", t => MkSetComp(d, id, "Preis", x => x.Preis.Wert = MkNum(t)))));
            sp.Children.Add(ShopLabeled("Herkunft des Preises", MkSourcePick(d, "mk-c-price-src", m, w.Preis, (x, v) => { if (x.Wettbewerber.FirstOrDefault(y => y.Id == id) is { } y) MkApplySource(y.Preis, v); })));
            sp.Children.Add(ShopLabeled("Angebot", ShopBox("mk-c-offer", w.Angebot, "Was bieten sie an?", t => MkSetComp(d, id, "Angebot", x => x.Angebot = t.Trim()), true)));
            sp.Children.Add(ShopLabeled("Stärken", ShopBox("mk-c-strong", w.Staerken, "z. B. große Auswahl", t => MkSetComp(d, id, "Stärken", x => x.Staerken = t.Trim()))));
            sp.Children.Add(ShopLabeled("Schwächen", ShopBox("mk-c-weak", w.Schwaechen, "z. B. teurer Versand", t => MkSetComp(d, id, "Schwächen", x => x.Schwaechen = t.Trim()))));
            sp.Children.Add(ShopLabeled("Leistung 1–10 (eigene Einschätzung)", ShopBox("mk-c-score", w.Leistung.ToString(CultureInfo.InvariantCulture), "5", t => { if (int.TryParse(t, out var n) && n is >= 1 and <= 10) MkSetComp(d, id, "Leistung", x => x.Leistung = n); }, false, 60)));
            if (w.Notiz.Length > 0) sp.Children.Add(new WrapPanel { Children = { P(w.Notiz, K4Warn, 11), KrBtn("mk-c-ok", "Geprüft", () => MkSetComp(d, id, "Geprüft", x => x.Notiz = ""), false, MkKey) } });
            sp.Children.Add(KrBtn("mk-c-del", "Wettbewerber entfernen", () => { MkChange(d, "Wettbewerber entfernt", x => x.Wettbewerber.RemoveAll(y => y.Id == id)); d.Data["sel"] = MkOf(d).Wettbewerber.FirstOrDefault()?.Id ?? ""; }, false, MkKey));
        }
        sp.Children.Add(P("Keine erfundenen Zahlen: Was ohne Quelle ist, steht im Bericht als „⚠ Quelle fehlt“. Keine Anlage- oder Rechtsberatung.", Muted, 10.5));
        return sp;
    }
}
