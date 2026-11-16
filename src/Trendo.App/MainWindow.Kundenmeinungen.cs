using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Trendo.Core;
using Trendo.Core.Business;
using Trendo.Core.Programs;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>STUDIO-REVIEWS-01 Kundenmeinungen (T-0677): echte Bewertungen wie in einem Bewertungs-Werkzeug für Firmen – Posteingang, Statistik,
/// Themen, Antwort-Entwürfe, Zitat-Grafiken, Widget. Clean-Room nach dem Bedienkonzept von Trustpilot Business, Antworten im Unternehmensprofil, Judge.me.
/// Trendo erzeugt keine Bewertungen und veröffentlicht nichts.</summary>
public partial class MainWindow
{
    bool _rvAiBusy;
    static readonly CultureInfo RvDe = CultureInfo.GetCultureInfo("de-DE");

    public static MeinungsSammlung RvOf(ProgramDocument d) => MeinungsSammlung.FromJson(d.Data["sammlung"]);
    public void RvChange(ProgramDocument d, string label, Action<MeinungsSammlung> change) { var s = RvOf(d); change(s); var j = s.ToJson(); d.Edit(label, x => x["sammlung"] = j); }
    static JsonObject RvDoc(MeinungsSammlung s) => new() { ["sammlung"] = s.ToJson(), ["tab"] = "uebersicht", ["sel"] = s.Meinungen.FirstOrDefault()?.Id ?? "", ["filter"] = "alle", ["quoteFormat"] = "quadrat", ["paste"] = "", ["pasteSource"] = "Google" };
    public static Kundenmeinung? RvSelected(ProgramDocument d) { var s = RvOf(d); return s.Meinungen.FirstOrDefault(m => m.Id == d.Text("sel")) ?? s.Meinungen.FirstOrDefault(); }

    private void InitKundenmeinungen() => RegisterProgram(BusinessTabs, new ProgramSpec(RvKey, "STUDIO-REVIEWS-01", "Kundenmeinungen")
    {
        Sentence = "Echte Bewertungen an einem Ort: Sterne und Verlauf, worüber Kunden sprechen, freundliche Antwort-Entwürfe, Zitat-Grafiken und ein Widget für die eigene Seite.",
        Folder = "Kundenmeinungen",
        ScrollWorkspace = true,
        GroupLevels = new() { ["Einlesen"] = 0, ["Antworten"] = 0, ["Teilen"] = 0 },
        Templates =
        [
            new("leer", "Leere Sammlung", "Bewertungen einlesen oder eintragen", () => RvDoc(new MeinungsSammlung())),
            new("beispiel", "Beispiel: Café", "Fünf Beispiel-Bewertungen zum Ausprobieren", () => RvDoc(Kundenmeinungen.Beispiel(ShopNow))),
        ],
        Ribbon =
        [
            ("Einlesen", [
                new ProgramCmd("rv-csv", "CSV einlesen …", d => _ = RvPickCsv(d), Primary: true, Tip: "Export deiner Plattform (Spalten Sterne und Text, dazu Name, Datum, Quelle)."),
                new ProgramCmd("rv-paste", "Text einfügen", d => d.Data["tab"] = "einlesen", Tip: "Kopierte Bewertungen – je Bewertung ein Absatz, Sterne am Anfang."),
                new ProgramCmd("rv-add", "＋ Bewertung", d => RvAdd(d)),
            ]),
            ("Antworten", [
                new ProgramCmd("rv-draft", "Entwurf", d => RvDraft(d), Tip: "Antwort-Entwurf aus Bausteinen – passend zu Sternen und Thema."),
                new ProgramCmd("rv-ai", "Entwurf mit KI", d => _ = RvAiDraft(d), Tip: "Die KI schreibt einen Entwurf – ohne erfundene Zusagen. Veröffentlicht wird von dir."),
                new ProgramCmd("rv-done", "Als beantwortet markieren", d => RvMarkAnswered(d), Tip: "Erst nachdem du die Antwort auf der Plattform veröffentlicht hast."),
            ]),
            ("Teilen", [
                new ProgramCmd("rv-quote", "Zitat-Grafik", d => RvQuoteNow(d)),
                new ProgramCmd("rv-widget", "Widget für die Webseite", d => RvWidgetNow(d)),
            ]),
        ],
        Exports =
        [
            new("md", "Auswertung (Markdown)", ".md", (d, f) => { File.WriteAllText(f, Kundenmeinungen.Bericht(RvOf(d), ShopNow), new UTF8Encoding(false)); return f; }),
            new("csv", "Bewertungen (CSV)", ".csv", (d, f) => { File.WriteAllText(f, Kundenmeinungen.Csv(RvOf(d).Meinungen), new UTF8Encoding(true)); return f; }),
            new("html", "Widget (HTML)", ".html", (d, f) => { File.WriteAllText(f, Kundenmeinungen.WidgetHtml(RvOf(d)), new UTF8Encoding(false)); return f; }),
            new("svg", "Zitat-Grafik (SVG)", ".svg", (d, f) => { File.WriteAllText(f, Kundenmeinungen.ZitatSvg(RvLayout(d)), new UTF8Encoding(false)); return f; }),
            new("png", "Zitat-Grafik (PNG)", ".png", (d, f) => { File.WriteAllBytes(f, RvQuotePng(RvLayout(d))); return f; }),
        ],
        SimpleExports = 2,
        Workspace = RvWorkspace,
        Properties = RvProperties,
        Status = d => { var st = Kundenmeinungen.Statistik(RvOf(d).Meinungen); return $"{st.Anzahl} Bewertungen · {st.Schnitt.ToString("0.00", RvDe)} Sterne · {st.AnteilPositiv:P0} mit 4–5 Sternen · {st.Offen} ohne veröffentlichte Antwort"; },
        Help = "Bewertungen einlesen (CSV deiner Plattform oder kopierten Text) oder einzeln eintragen. „Übersicht“ zeigt Schnitt, Verteilung, Verlauf und die Themen, über die Kunden sprechen (Auswertung mit Wortlisten auf deinem PC). "
             + "Im Posteingang eine Bewertung wählen und rechts antworten: Entwurf aus Bausteinen oder mit KI – kopieren, auf der Plattform selbst veröffentlichen, dann „Als beantwortet markieren“. "
             + "Trendo erzeugt keine Bewertungen: gekaufte oder erfundene Bewertungen sind verboten. Zitate und Widget zeigen nur echte Bewertungen mit Quelle. Keine Rechtsberatung.",
    }, "kundenmeinungen", "bewertungen", "rezensionen", "reviews", "sterne", "kundenstimmen", "trustpilot", "google bewertungen", "judge.me", "antwort auf bewertung", "testimonial");

    // ---------- Befehle ----------

    async Task RvPickCsv(ProgramDocument d)
    {
        if (await PickMediaFile("rv-csv", "Bewertungen (CSV)", ["*.csv", "*.txt", "*.tsv"]) is { } f) ProgramRun(RvKey, () => RvImportCsv(d, f));
    }

    public int RvImportCsv(ProgramDocument d, string file)
    {
        if (!File.Exists(file)) throw new FileNotFoundException("Die Datei gibt es nicht.", file);
        if (new FileInfo(file).Length > 20 * 1024 * 1024) throw new InvalidOperationException("Die Datei ist größer als 20 MB.");
        var (neu, hinweise) = Kundenmeinungen.CsvLesen(File.ReadAllText(file), d.Text("pasteSource", "Sonstige"));
        var n = 0; RvChange(d, "CSV eingelesen", s => n = Kundenmeinungen.Uebernehmen(s, neu));
        _progMsg[RvKey] = $"{n} Bewertungen eingelesen" + (neu.Count > n ? $", {neu.Count - n} doppelt übersprungen" : "") + (hinweise.Count > 0 ? $". ⚠ {hinweise[0]}" + (hinweise.Count > 1 ? $" (+{hinweise.Count - 1})" : "") : ".");
        d.Data["tab"] = "posteingang";
        return n;
    }

    public int RvImportText(ProgramDocument d, string text, string quelle)
    {
        if (Kundenmeinungen.FragtNachFake(text)) throw new InvalidOperationException("Trendo erzeugt keine Bewertungen – hier nur echte Bewertungen einfügen.");
        var (neu, hinweise) = Kundenmeinungen.TextLesen(text, quelle);
        if (neu.Count == 0) throw new InvalidOperationException(hinweise.FirstOrDefault() ?? "Keine Bewertung erkannt – je Absatz Sterne am Anfang, z. B. „5 | Toller Service“.");
        var n = 0; RvChange(d, "Text eingelesen", s => n = Kundenmeinungen.Uebernehmen(s, neu));
        d.Data["paste"] = ""; d.Data["tab"] = "posteingang";
        _progMsg[RvKey] = $"{n} Bewertungen übernommen" + (neu.Count > n ? $", {neu.Count - n} doppelt" : "") + (hinweise.Count > 0 ? $". ⚠ {hinweise[0]}" : ".");
        return n;
    }

    public Kundenmeinung RvAdd(ProgramDocument d)
    {
        var m = new Kundenmeinung { Id = NewId("m"), Sterne = 5, Datum = ShopNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Quelle = "Eigene Seite" };
        RvChange(d, "Bewertung", s => s.Meinungen.Insert(0, m)); d.Data["sel"] = m.Id; d.Data["tab"] = "posteingang";
        _progMsg[RvKey] = "Neue Bewertung – rechts so eintragen, wie die Kundin oder der Kunde sie geschrieben hat.";
        return m;
    }

    void RvSet(ProgramDocument d, string id, string label, Action<Kundenmeinung> a) => RvChange(d, label, s => { if (s.Meinungen.FirstOrDefault(x => x.Id == id) is { } m) a(m); });

    public string RvDraft(ProgramDocument d)
    {
        var m = RvSelected(d) ?? throw new InvalidOperationException("Erst eine Bewertung wählen.");
        var s = RvOf(d); var text = Kundenmeinungen.Entwurf(m, s.Firma, s.Ton);
        RvSet(d, m.Id, "Antwort-Entwurf", x => { x.Antwort = text; if (x.Status == "offen") x.Status = "entwurf"; });
        _progMsg[RvKey] = "Entwurf steht rechts – anpassen, kopieren und selbst auf der Plattform veröffentlichen.";
        return text;
    }

    public async Task RvAiDraft(ProgramDocument d)
    {
        if (_rvAiBusy) return;
        var m = RvSelected(d); if (m is null) { _progMsg[RvKey] = "Erst eine Bewertung wählen."; Show(RvKey); return; }
        var s = RvOf(d); _rvAiBusy = true; _progMsg[RvKey] = "Die KI schreibt einen Entwurf …"; Show(RvKey);
        string? text;
        try { text = await ShopAsk(Kundenmeinungen.AntwortPrompt(m, s.Firma, s.Ton)); }
        finally { _rvAiBusy = false; }
        if (string.IsNullOrWhiteSpace(text))
        {
            RvDraft(d); _progMsg[RvKey] = "Keine KI erreichbar – stattdessen steht der Entwurf aus Bausteinen da. KI einrichten unter „KI fragen“.";
        }
        else
        {
            var clean = Kundenmeinungen.Saeubern(text);
            RvSet(d, m.Id, "Antwort (KI)", x => { x.Antwort = clean; if (x.Status == "offen") x.Status = "entwurf"; });
            _progMsg[RvKey] = "Die KI hat einen Entwurf geschrieben – bitte prüfen: stimmt jede Aussage? Veröffentlicht wird von dir.";
        }
        Show(RvKey);
    }

    public void RvMarkAnswered(ProgramDocument d)
    {
        var m = RvSelected(d) ?? throw new InvalidOperationException("Erst eine Bewertung wählen.");
        if (m.Antwort.Trim().Length == 0) throw new InvalidOperationException("Erst eine Antwort schreiben und auf der Plattform veröffentlichen.");
        RvSet(d, m.Id, "Beantwortet", x => x.Status = "beantwortet");
        _progMsg[RvKey] = "Als beantwortet markiert. Im Widget erscheint die Antwort unter der Bewertung.";
    }

    ZitatLayout RvLayout(ProgramDocument d)
    {
        var m = RvSelected(d) ?? throw new InvalidOperationException("Erst eine Bewertung wählen.");
        return Kundenmeinungen.Zitat(m, d.Text("quoteFormat", "quadrat"), d.Text("quoteInk", "#1F2937"), d.Text("quoteBg", "#FDF6E3"));
    }

    /// <summary>Zitat-Grafik als PNG (gleiche Aufteilung wie das SVG, gezeichnet mit Skia).</summary>
    static byte[] RvQuotePng(ZitatLayout z)
    {
        using var surf = SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(z.Breite, z.Hoehe, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Premul));
        var c = surf.Canvas; c.Clear(SkiaSharp.SKColor.Parse(z.Grund));
        using var serif = new SkiaSharp.SKFont(SkiaSharp.SKTypeface.FromFamilyName("Georgia") ?? SkiaSharp.SKTypeface.Default, (float)z.Schrift);
        using var sans = new SkiaSharp.SKFont(SkiaSharp.SKTypeface.FromFamilyName("Segoe UI") ?? SkiaSharp.SKTypeface.Default, (float)(z.Schrift * 0.9));
        using var small = new SkiaSharp.SKFont(sans.Typeface, (float)(z.Schrift * 0.5));
        using var ink = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColor.Parse(z.Farbe), IsAntialias = true };
        using var gold = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColor.Parse("#F59E0B"), IsAntialias = true };
        using var muted = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColor.Parse(z.Farbe).WithAlpha(190), IsAntialias = true };
        var lineH = z.Schrift * 1.3; var block = z.Zeilen.Count * lineH + z.Schrift * 3.2; var y = (float)((z.Hoehe - block) / 2 + z.Schrift);
        c.DrawText(z.Sterne, z.Breite / 2f, y, SkiaSharp.SKTextAlign.Center, sans, gold); y += (float)(z.Schrift * 1.6);
        foreach (var l in z.Zeilen) { c.DrawText(l, z.Breite / 2f, y, SkiaSharp.SKTextAlign.Center, serif, ink); y += (float)lineH; }
        y += (float)(z.Schrift * 0.6);
        c.DrawText(z.Unterzeile, z.Breite / 2f, y, SkiaSharp.SKTextAlign.Center, small, muted);
        using var img = surf.Snapshot(); using var data = img.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    public string RvQuoteNow(ProgramDocument d)
    {
        var m = RvSelected(d) ?? throw new InvalidOperationException("Erst eine Bewertung wählen.");
        var z = RvLayout(d);
        var f = ShopFile("Kundenmeinungen", $"Zitat {(m.Autor.Length > 0 ? m.Autor : "Kunde")} {d.Text("quoteFormat", "quadrat")}", ".png");
        File.WriteAllBytes(f, RvQuotePng(z)); File.WriteAllText(Path.ChangeExtension(f, ".svg"), Kundenmeinungen.ZitatSvg(z), new UTF8Encoding(false));
        _progMsg[RvKey] = $"Zitat-Grafik {z.Breite} × {z.Hoehe}: {f} (dazu SVG)." + (m.ZitatErlaubt ? "" : " Tipp: Für Werbung das Einverständnis der Person einholen und hier abhaken.");
        NotifyFileResult(_progMsg[RvKey]); return f;
    }

    public string RvWidgetNow(ProgramDocument d)
    {
        var s = RvOf(d); if (s.Meinungen.Count == 0) throw new InvalidOperationException("Noch keine Bewertungen für das Widget.");
        var f = ShopFile("Kundenmeinungen", "Bewertungs-Widget", ".html");
        File.WriteAllText(f, Kundenmeinungen.WidgetHtml(s), new UTF8Encoding(false));
        _progMsg[RvKey] = $"Widget gespeichert: {f} – den Inhalt in deine Seite kopieren (z. B. im Web-Design-Studio). Es lädt nichts nach und setzt keine Cookies.";
        NotifyFileResult(_progMsg[RvKey]); return f;
    }

    // ---------- Oberfläche ----------

    static string RvStars(int n) => new string('★', Math.Clamp(n, 0, 5)) + new string('☆', 5 - Math.Clamp(n, 0, 5));
    static IBrush RvStarBrush(int n) => n >= 4 ? K4Good : n == 3 ? K4Warn : K4Bad;

    Control RvWorkspace(ProgramDocument d)
    {
        var s = RvOf(d); var tab = d.Text("tab", "uebersicht");
        var tabs = new WrapPanel { Name = "rv-tabs", Margin = new Thickness(0, 0, 0, 10) };
        foreach (var (id, label) in new[] { ("uebersicht", "Übersicht"), ("posteingang", "Posteingang"), ("einlesen", "Einlesen"), ("teilen", "Zitate & Widget"), ("regeln", "Regeln") })
        {
            var t = id; var b = KrBtn("rv-tab-" + id, label, () => d.Data["tab"] = t, id == tab, RvKey); b.Margin = new Thickness(0, 0, 6, 6); tabs.Children.Add(b);
        }
        Control body = tab switch { "posteingang" => RvInbox(d, s), "einlesen" => RvPaste(d), "teilen" => RvShare(d, s), "regeln" => RvRules(s), _ => RvOverview(s) };
        return new StackPanel { Margin = new Thickness(4, 0, 10, 4), Children = { tabs, body } };
    }

    Control RvOverview(MeinungsSammlung s)
    {
        var st = Kundenmeinungen.Statistik(s.Meinungen); var sp = new StackPanel { Name = "rv-overview", Spacing = 12 };
        var trend = Kundenmeinungen.Trend(s.Meinungen, ShopNow);
        sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,10,*,10,*,10,*"), Children = {
            ShopTile("rv-kpi-avg", st.Schnitt.ToString("0.0", RvDe) + " ★", $"Schnitt aus {st.Anzahl} Bewertungen", RvStarBrush((int)Math.Round(st.Schnitt))),
            Pos(ShopTile("rv-kpi-pos", st.AnteilPositiv.ToString("P0", RvDe), "mit 4 oder 5 Sternen"), 2),
            Pos(ShopTile("rv-kpi-open", st.Offen.ToString(CultureInfo.InvariantCulture), "noch ohne Antwort", st.Offen > 0 ? K4Warn : null), 4),
            Pos(ShopTile("rv-kpi-trend", trend is { } t ? t.Neu.ToString("0.0", RvDe) + (t.Neu > t.Vorher + 0.05 ? " ↑" : t.Neu < t.Vorher - 0.05 ? " ↓" : " →") : "–", trend is { } t2 ? $"letzte 90 Tage (davor {t2.Vorher.ToString("0.0", RvDe)})" : "Verlauf braucht Datumsangaben"), 6) } });

        var dist = new StackPanel { Name = "rv-dist", Spacing = 4, Children = { H("Verteilung", 14) } };
        var max = Math.Max(1, st.Verteilung.Max());
        for (var i = 5; i >= 1; i--)
            dist.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("70,*,40"), Children = {
                new TextBlock { Text = RvStars(i), Foreground = Brush.Parse("#F59E0B"), FontSize = 12 },
                Pos(new Border { Height = 12, CornerRadius = new CornerRadius(3), Background = RvStarBrush(i), HorizontalAlignment = HorizontalAlignment.Left, Width = Math.Max(3, st.Verteilung[i - 1] / (double)max * 320), VerticalAlignment = VerticalAlignment.Center }, 1),
                Pos(new TextBlock { Name = $"rv-dist-{i}", Text = st.Verteilung[i - 1].ToString(CultureInfo.InvariantCulture), Foreground = Text, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Right }, 2) } });
        sp.Children.Add(KrCard(dist));

        var verlauf = Kundenmeinungen.Verlauf(s.Meinungen);
        if (verlauf.Count > 0)
        {
            var bars = new StackPanel { Name = "rv-history", Orientation = Orientation.Horizontal, Spacing = 6, Height = 130, VerticalAlignment = VerticalAlignment.Bottom };
            foreach (var m in verlauf.TakeLast(12))
            {
                var col = new StackPanel { Width = 46, VerticalAlignment = VerticalAlignment.Bottom, Spacing = 2 };
                col.Children.Add(new TextBlock { Text = m.Schnitt.ToString("0.0", RvDe), Foreground = Text, FontSize = 10.5, HorizontalAlignment = HorizontalAlignment.Center });
                col.Children.Add(new Border { Height = Math.Max(4, m.Schnitt / 5 * 80), Background = RvStarBrush((int)Math.Round(m.Schnitt)), CornerRadius = new CornerRadius(3, 3, 0, 0) });
                col.Children.Add(new TextBlock { Text = m.Monat[2..].Replace("-", "/") + $" ({m.Anzahl})", Foreground = Muted, FontSize = 9.5, HorizontalAlignment = HorizontalAlignment.Center });
                bars.Children.Add(col);
            }
            sp.Children.Add(KrCard(new StackPanel { Spacing = 6, Children = { H("Verlauf je Monat (Sterne, Anzahl)", 14), bars } }));
        }

        var themen = Kundenmeinungen.Themen(s.Meinungen);
        var th = new StackPanel { Name = "rv-topics", Spacing = 4, Children = { H("Worüber Kunden sprechen", 14), P("Auswertung mit Wortlisten auf deinem PC – eine Schätzung, kein Urteil.", Muted, 10.5) } };
        if (themen.Count == 0) th.Children.Add(P("Noch keine Themen erkannt.", Muted, 12));
        var ti = 0;
        foreach (var t in themen)
            th.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("150,*,80"), Children = {
                new TextBlock { Name = $"rv-topic-{ti++}", Text = t.Thema, Foreground = Text, FontSize = 12.5 },
                Pos(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children = {
                    new Border { Width = t.Positiv * 18, Height = 10, Background = K4Good, CornerRadius = new CornerRadius(2) }, new Border { Width = t.Negativ * 18, Height = 10, Background = K4Bad, CornerRadius = new CornerRadius(2) },
                    new TextBlock { Text = $"  {t.Positiv} gut · {t.Negativ} Kritik", Foreground = Muted, FontSize = 11 } } }, 1),
                Pos(new TextBlock { Text = t.Schnitt.ToString("0.0", RvDe) + " ★", Foreground = RvStarBrush((int)Math.Round(t.Schnitt)), FontSize = 12, HorizontalAlignment = HorizontalAlignment.Right }, 2) } });
        sp.Children.Add(KrCard(th));

        var auff = Kundenmeinungen.Auffaelligkeiten(s.Meinungen);
        if (auff.Count > 0)
        {
            var fl = new StackPanel { Name = "rv-flags", Spacing = 3, Children = { H("Auffälligkeiten", 14) } };
            foreach (var a in auff) fl.Children.Add(P("⚠ " + a, K4Warn, 12));
            sp.Children.Add(KrCard(fl));
        }
        return sp;
    }

    Control RvInbox(ProgramDocument d, MeinungsSammlung s)
    {
        var filter = d.Text("filter", "alle"); var sel = RvSelected(d)?.Id;
        var list = new StackPanel { Name = "rv-list", Spacing = 4 };
        list.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { H($"Posteingang ({s.Meinungen.Count})", 14),
            Pos(ShopCombo(RvKey, "rv-filter", [("alle", "Alle"), ("offen", "Ohne Antwort"), ("kritisch", "1–2 Sterne"), ("widerspruch", "Sterne passen nicht zum Text")], filter, v => d.Data["filter"] = v, 200), 1) } });
        if (s.Meinungen.Count == 0) list.Children.Add(P("Noch keine Bewertungen. Oben „CSV einlesen“ oder „Text einfügen“.", Muted, 12));
        var i = 0;
        foreach (var m in s.Meinungen.OrderByDescending(x => x.Datum, StringComparer.Ordinal))
        {
            var st = Kundenmeinungen.Bewerten(m.Titel + ". " + m.Text, m.Sterne);
            if (filter == "offen" && m.Status == "beantwortet") continue;
            if (filter == "kritisch" && m.Sterne > 2) continue;
            if (filter == "widerspruch" && !st.Widerspruch) continue;
            var id = m.Id; var n = i++;
            var chips = new WrapPanel();
            foreach (var t in st.Themen) chips.Children.Add(new Border { Margin = new Thickness(0, 2, 4, 0), Padding = new Thickness(6, 1), CornerRadius = new CornerRadius(8), Background = Brush.Parse("#22FFFFFF"), Child = new TextBlock { Text = t, Foreground = Muted, FontSize = 10.5 } });
            var badge = m.Status switch { "beantwortet" => ("beantwortet", K4Good), "entwurf" => ("Entwurf", K4Warn), _ => ("offen", Muted) };
            var row = new Button
            {
                Name = "rv-row-" + n, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(10, 8),
                Background = id == sel ? Brush.Parse("#33C9A227") : Brush.Parse("#14FFFFFF"),
                Content = new Grid { ColumnDefinitions = new ColumnDefinitions("90,*,90"), Children = {
                    new StackPanel { Children = { new TextBlock { Text = RvStars(m.Sterne), Foreground = Brush.Parse("#F59E0B"), FontSize = 12 }, new TextBlock { Text = m.Quelle, Foreground = Muted, FontSize = 10.5 } } },
                    Pos(new StackPanel { Children = {
                        new TextBlock { Text = (m.Titel.Length > 0 ? m.Titel + " – " : "") + m.Text, Foreground = Text, FontSize = 12.5, TextWrapping = TextWrapping.Wrap, MaxLines = 3 },
                        new TextBlock { Text = $"{(m.Autor.Length > 0 ? m.Autor : "ohne Name")} · {m.Datum} · Stimmung {st.Art}" + (st.Widerspruch ? " · ⚠ Sterne passen nicht zum Text" : "") + (m.Verifiziert ? " · verifiziert" : ""), Foreground = st.Widerspruch ? K4Warn : Muted, FontSize = 10.5 },
                        chips } }, 1),
                    Pos(new TextBlock { Name = $"rv-row-{n}-status", Text = badge.Item1, Foreground = badge.Item2, FontSize = 11.5, HorizontalAlignment = HorizontalAlignment.Right }, 2) } },
            };
            row.Click += (_, _) => { d.Data["sel"] = id; Show(RvKey); };
            list.Children.Add(row);
        }
        return KrCard(list, "rv-list-card");
    }

    Control RvPaste(ProgramDocument d)
    {
        var box = ShopBox("rv-paste-box", d.Text("paste"), "5 | Super Service, gerne wieder\nName: Erika M.\nDatum: 03.11.2026\n\n★★☆☆☆ Lieferung kam zu spät", t => d.Data["paste"] = t, true);
        box.MinHeight = 200;
        return KrCard(new StackPanel { Name = "rv-paste", Spacing = 8, Children = {
            H("Bewertungen einfügen", 14),
            P("Je Bewertung ein Absatz (Leerzeile dazwischen), Sterne am Anfang: „5“, „4/5“, „4 Sterne“ oder „★★★★☆“. Optional Zeilen „Name: …“ und „Datum: …“. Nur echte Bewertungen – nichts erfinden.", Muted, 11.5),
            box,
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = {
                new TextBlock { Text = "Quelle", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center },
                ShopCombo(RvKey, "rv-paste-source", Kundenmeinungen.Quellen.Select(q => (q, q)), d.Text("pasteSource", "Google"), v => d.Data["pasteSource"] = v, 150),
                KrBtn("rv-paste-ok", "Übernehmen", () => RvImportText(d, box.Text ?? "", d.Text("pasteSource", "Google")), true, RvKey) } } } });
    }

    Control RvShare(ProgramDocument d, MeinungsSammlung s)
    {
        var sp = new StackPanel { Name = "rv-share", Spacing = 12 };
        var m = RvSelected(d);
        var q = new StackPanel { Spacing = 8, Children = { H("Zitat-Grafik", 14) } };
        if (m is null || m.Text.Trim().Length == 0) q.Children.Add(P("Im Posteingang eine Bewertung mit Text wählen.", Muted, 12));
        else
        {
            q.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = {
                ShopCombo(RvKey, "rv-quote-format", Kundenmeinungen.ZitatFormate.Select(f => (f.Id, f.Name)), d.Text("quoteFormat", "quadrat"), v => d.Data["quoteFormat"] = v, 260),
                ShopCombo(RvKey, "rv-quote-style", [("#FDF6E3", "Hell (Creme)"), ("#FFFFFF", "Weiß"), ("#111827", "Dunkel")], d.Text("quoteBg", "#FDF6E3"), v => { d.Data["quoteBg"] = v; d.Data["quoteInk"] = v == "#111827" ? "#F9FAFB" : "#1F2937"; }, 140),
                KrBtn("rv-quote-save", "Als PNG und SVG speichern", () => RvQuoteNow(d), true, RvKey) } });
            var z = RvLayout(d);
            using var ms = new MemoryStream(RvQuotePng(z));
            q.Children.Add(new Image { Name = "rv-quote-preview", Source = new Bitmap(ms), MaxWidth = 380, MaxHeight = 380, HorizontalAlignment = HorizontalAlignment.Left, Stretch = Stretch.Uniform });
            q.Children.Add(P("Der Text bleibt unverändert; zu lange Bewertungen werden an einer Satzgrenze mit „[…]“ gekürzt. Name gekürzt, Quelle und Monat stehen dabei.", Muted, 10.5));
        }
        sp.Children.Add(KrCard(q));
        var html = Kundenmeinungen.WidgetHtml(s);
        sp.Children.Add(KrCard(new StackPanel { Spacing = 8, Children = {
            H("Widget für die eigene Webseite", 14),
            P("Statisches HTML – ohne Skripte, ohne Cookies, ohne fremden Dienst. Es zeigt die neuesten Bewertungen mit allen Sternen und deinen Prüfhinweis.", Muted, 11.5),
            new TextBox { Name = "rv-widget-code", Text = html, IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, Height = 180, FontFamily = new FontFamily("Consolas, monospace"), FontSize = 11 },
            KrBtn("rv-widget-save", "Widget speichern (HTML)", () => RvWidgetNow(d), false, RvKey) } }));
        return sp;
    }

    static Control RvRules(MeinungsSammlung s)
    {
        var sp = new StackPanel { Name = "rv-rules", Spacing = 6, Children = { H("Regeln für Bewertungen", 14) } };
        foreach (var r in Kundenmeinungen.Regeln) sp.Children.Add(P("• " + r, Text, 12));
        sp.Children.Add(P("Keine Rechtsberatung – im Zweifel bei IHK oder Verbraucherzentrale nachfragen.", Muted, 10.5));
        return KrCard(sp);
    }

    Control RvProperties(ProgramDocument d)
    {
        var s = RvOf(d); var sp = new StackPanel { Name = "rv-props", Spacing = 6 };
        sp.Children.Add(H("Firma", 13));
        sp.Children.Add(ShopLabeled("Name für die Antworten", ShopBox("rv-firm", s.Firma, "z. B. Café Beispiel", t => RvChange(d, "Firma", x => x.Firma = t.Trim()))));
        sp.Children.Add(ShopLabeled("Ton der Antworten", ShopCombo(RvKey, "rv-tone", Kundenmeinungen.Toene, s.Ton, t => RvChange(d, "Ton", x => x.Ton = t), 160)));
        sp.Children.Add(ShopLabeled("Prüfhinweis (§ 5b UWG, steht unter dem Widget)", ShopBox("rv-check-note", s.Pruefhinweis, "Wie prüfst du, ob Bewertungen echt sind?", t => RvChange(d, "Prüfhinweis", x => x.Pruefhinweis = t.Trim()), true)));
        var m = RvSelected(d);
        if (m is null) { sp.Children.Add(P("Keine Bewertung gewählt.", Muted, 12)); return sp; }
        var id = m.Id; var st = Kundenmeinungen.Bewerten(m.Titel + ". " + m.Text, m.Sterne);
        sp.Children.Add(H("Bewertung", 13));
        var stars = new StackPanel { Name = "rv-stars", Orientation = Orientation.Horizontal, Spacing = 2 };
        for (var k = 1; k <= 5; k++)
        {
            var n = k; var b = new Button { Name = "rv-star-" + k, Content = k <= m.Sterne ? "★" : "☆", Foreground = Brush.Parse("#F59E0B"), FontSize = 16, Padding = new Thickness(4, 0), Background = Brushes.Transparent };
            b.Click += (_, _) => ProgramRun(RvKey, () => RvSet(d, id, "Sterne", x => x.Sterne = n)); stars.Children.Add(b);
        }
        sp.Children.Add(stars);
        sp.Children.Add(ShopLabeled("Titel", ShopBox("rv-f-title", m.Titel, "optional", t => RvSet(d, id, "Titel", x => x.Titel = t))));
        sp.Children.Add(ShopLabeled("Text (so wie geschrieben)", ShopBox("rv-f-text", m.Text, "Text der Bewertung", t => RvSet(d, id, "Text", x => x.Text = t), true)));
        sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,*"), Children = {
            ShopLabeled("Name (gekürzt)", ShopBox("rv-f-author", m.Autor, "Erika M.", t => RvSet(d, id, "Name", x => x.Autor = Kundenmeinungen.Kuerzen(t)))),
            Pos(ShopLabeled("Datum (TT.MM.JJJJ)", ShopBox("rv-f-date", Kundenmeinungen.DatumAus(m.Datum) is { } dd ? DateTime.ParseExact(dd, "yyyy-MM-dd", CultureInfo.InvariantCulture).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) : m.Datum, "03.11.2026", t => RvSet(d, id, "Datum", x => x.Datum = Kundenmeinungen.DatumAus(t) ?? t.Trim()))), 2) } });
        sp.Children.Add(ShopLabeled("Quelle", ShopCombo(RvKey, "rv-f-source", Kundenmeinungen.Quellen.Select(q => (q, q)), m.Quelle, t => RvSet(d, id, "Quelle", x => x.Quelle = t), 160)));
        foreach (var (name, label, val, set) in new (string, string, bool, Action<Kundenmeinung, bool>)[] { ("rv-f-verified", "Verifizierter Kauf", m.Verifiziert, (x, b) => x.Verifiziert = b), ("rv-f-consent", "Einverstanden mit Zitat in Werbung", m.ZitatErlaubt, (x, b) => x.ZitatErlaubt = b) })
        {
            var cb = new CheckBox { Name = name, Content = label, IsChecked = val, Foreground = Text, FontSize = 12 }; var st2 = set; var old = val; var lb = label;
            cb.IsCheckedChanged += (_, _) => { if ((cb.IsChecked == true) != old) ProgramRun(RvKey, () => RvSet(d, id, lb, x => st2(x, cb.IsChecked == true))); };
            sp.Children.Add(cb);
        }
        sp.Children.Add(P($"Stimmung: {st.Art}" + (st.Plus.Count > 0 ? " · + " + string.Join(", ", st.Plus.Take(4)) : "") + (st.Minus.Count > 0 ? " · − " + string.Join(", ", st.Minus.Take(4)) : "") + (st.Themen.Count > 0 ? " · Themen: " + string.Join(", ", st.Themen) : ""), st.Widerspruch ? K4Warn : Muted, 11));

        sp.Children.Add(H("Antwort", 13));
        sp.Children.Add(ShopBox("rv-reply", m.Antwort, "Antwort-Entwurf – oben „Entwurf“ oder „Entwurf mit KI“", t => RvSet(d, id, "Antwort", x => { x.Antwort = t; if (x.Status == "offen" && t.Trim().Length > 0) x.Status = "entwurf"; }), true).Also(b => b.MinHeight = 140));
        sp.Children.Add(new WrapPanel { Children = {
            KrBtn("rv-draft-btn", "Entwurf", () => RvDraft(d), false, RvKey).Also(b => b.Margin = new Thickness(0, 0, 6, 6)),
            KrBtn("rv-ai-btn", "Mit KI", () => _ = RvAiDraft(d), false, RvKey).Also(b => b.Margin = new Thickness(0, 0, 6, 6)),
            KrBtn("rv-copy-btn", "Kopieren", () => _ = Clipboard?.SetTextAsync(RvSelected(d)?.Antwort ?? ""), false, RvKey).Also(b => b.Margin = new Thickness(0, 0, 6, 6)),
            KrBtn("rv-done-btn", m.Status == "beantwortet" ? "✓ beantwortet" : "Als beantwortet markieren", () => RvMarkAnswered(d), m.Status != "beantwortet", RvKey).Also(b => b.Margin = new Thickness(0, 0, 6, 6)) } });
        sp.Children.Add(P("Trendo sendet nichts – Antwort kopieren und selbst auf der Plattform veröffentlichen.", Muted, 10.5));
        sp.Children.Add(KrBtn("rv-del", "Bewertung löschen", () => { RvChange(d, "Bewertung gelöscht", x => x.Meinungen.RemoveAll(y => y.Id == id)); d.Data["sel"] = RvOf(d).Meinungen.FirstOrDefault()?.Id ?? ""; _progMsg[RvKey] = "Gelöscht – Rückgängig holt sie zurück."; }, false, RvKey));
        return sp;
    }
}
