using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Buero;
using Trendo.Core.Programs;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>STUDIO-CONTRACTS-01 Verträge-Ordner (T-0671): Verträge wie in einer Vertragsverwaltung – Ampel nach Kündigungsfrist, Kosten im Überblick,
/// Erinnerung als Kalenderdatei, Kündigungsschreiben an „Briefe schreiben“. Nichts wird versendet.</summary>
public partial class MainWindow
{
    /// <summary>Tests: „heute“ für Fristen und Ampel.</summary>
    public static Func<DateTime>? ConNowOverride { get; set; }
    static DateTime ConNow => (ConNowOverride?.Invoke() ?? DateTime.Now).Date;
    static readonly CultureInfo ConDe = CultureInfo.GetCultureInfo("de-DE");

    public static VertragsOrdner ConOf(ProgramDocument d) => VertragsOrdner.FromJson(d.Data["ordner"]);
    public void ConChange(ProgramDocument d, string label, Action<VertragsOrdner> change)
    {
        var o = ConOf(d); change(o); var j = o.ToJson();
        d.Edit(label, x => x["ordner"] = j);
    }
    static JsonObject ConDoc(VertragsOrdner o) => new() { ["ordner"] = o.ToJson(), ["sel"] = o.Vertraege.FirstOrDefault()?.Id ?? "", ["filter"] = "" };
    public static Vertrag? ConSelected(ProgramDocument d) { var o = ConOf(d); return o.Vertraege.FirstOrDefault(v => v.Id == d.Text("sel")) ?? o.Vertraege.FirstOrDefault(); }

    private void InitVertraege() => RegisterProgram(FachTabs, new ProgramSpec(ConKey, "STUDIO-CONTRACTS-01", "Verträge-Ordner")
    {
        Sentence = "Alle Verträge an einem Ort – wann du spätestens kündigen musst, was sie im Monat und Jahr kosten, Erinnerung im Kalender und Kündigung als Brief-Entwurf.",
        Folder = "Verträge",
        ScrollWorkspace = true,
        GroupLevels = new() { ["Vertrag"] = 0, ["Fristen"] = 0, ["Kündigen"] = 0 },
        Templates =
        [
            new("leer", "Leerer Ordner", "Verträge selbst eintragen", () => ConDoc(new VertragsOrdner())),
            new("beispiel", "Beispiel: Haushalt", "Handy, Versicherung, Streaming, Fitness", () => ConDoc(Vertraege.Beispiel(ConNow))),
        ],
        Ribbon =
        [
            ("Vertrag", [
                new ProgramCmd("con-add", "＋ Vertrag", d => ConAdd(d), Primary: true),
                new ProgramCmd("con-doc", "Dokument anhängen …", d => _ = ConPickDocument(d), Tip: "Vertrag, Rechnung oder Bestätigung als PDF oder Foto."),
                new ProgramCmd("con-del", "Löschen", d => ConDelete(d), Level: 1),
            ]),
            ("Fristen", [
                new ProgramCmd("con-ics", "Erinnerungen (Kalender)", d => ConIcsNow(d), Tip: "Kalenderdatei mit jedem letzten Kündigungstag – Erinnerung vorher. Doppelklick übernimmt sie in den Kalender."),
            ]),
            ("Kündigen", [
                new ProgramCmd("con-letter", "Kündigung schreiben", d => ConToLetter(d), Tip: "Entwurf in „Briefe schreiben“ – mit Kundennummer und Datum. Versendet wird nichts."),
                new ProgramCmd("con-letter-pdf", "Kündigung als PDF", d => ConLetterPdfNow(d)),
                new ProgramCmd("con-done", "Als gekündigt markieren", d => ConMarkCancelled(d), Tip: "Erst wenn die Bestätigung des Anbieters da ist."),
            ]),
        ],
        Exports =
        [
            new("ics", "Erinnerungen (.ics)", ".ics", (d, f) => { File.WriteAllText(f, Vertraege.Ics(ConOf(d).Vertraege, ConNow, ConOf(d).VorlaufTage), new UTF8Encoding(false)); return f; }),
            new("csv", "Übersicht (CSV)", ".csv", (d, f) => { File.WriteAllText(f, Vertraege.Csv(ConOf(d).Vertraege, ConNow), new UTF8Encoding(true)); return f; }),
            new("kuendigung", "Kündigungsschreiben (PDF)", ".pdf", (d, f) => { LetterPdf.Write(ConLetterDraft(d), f); return f; }),
        ],
        SimpleExports = 2,
        // T-0702 (V-02): leerer Ordner – erster Vertrag
        FirstStep = new("Ersten Vertrag eintragen", "Trag einen Vertrag ein, z. B. Handy oder Versicherung – Trendo rechnet aus, bis wann du spätestens kündigen musst.",
            d => { ConAdd(d); ProgramShowProperties(ConKey); }, d => ConOf(d).Vertraege.Count == 0),
        Workspace = ConWorkspace,
        Properties = ConProperties,
        Status = d =>
        {
            var o = ConOf(d); var k = Vertraege.Kosten(o.Vertraege, ConNow);
            var red = o.Vertraege.Count(v => Vertraege.Stand(v, ConNow).Ampel == "rot");
            return $"{o.Vertraege.Count} Verträge · {k.Monat.ToString("0.00", ConDe)} € im Monat · {k.Jahr.ToString("0.00", ConDe)} € im Jahr" + (red > 0 ? $" · {red} jetzt kündigen" : "");
        },
        Help = "Links alle Verträge mit Ampel: rot = höchstens 14 Tage bis zum letzten Kündigungstag, gelb = bis 60 Tage, grün = noch Zeit. Rechts den gewählten Vertrag bearbeiten: Beginn, Mindestlaufzeit, Verlängerung und Kündigungsfrist. "
             + "Die Frist wird rückwärts vom Vertragsende gerechnet (Ende 30.06., 3 Monate → bis 31.03.). „Erinnerungen“ legt eine Kalenderdatei an, „Kündigung schreiben“ öffnet den Brief-Entwurf. Trendo kündigt und versendet nichts – keine Rechtsberatung.",
    }, "verträge", "vertrag", "kündigen", "kündigung", "kündigungsfrist", "abo", "abos", "laufzeit", "fixkosten", "versicherung", "handyvertrag", "vertragsverwaltung");

    // ---------- Befehle ----------

    public Vertrag ConAdd(ProgramDocument d)
    {
        var v = new Vertrag { Id = NewId("v"), Name = "Neuer Vertrag", Beginn = Vertraege.Iso(ConNow), Kategorie = "Sonstiges" };
        ConChange(d, "Vertrag", o => o.Vertraege.Insert(0, v)); d.Data["sel"] = v.Id;
        _progMsg[ConKey] = "Neuer Vertrag – rechts Anbieter, Beginn, Vertragsdauer und Kündigungsfrist eintragen.";
        return v;
    }

    void ConDelete(ProgramDocument d)
    {
        var v = ConSelected(d) ?? throw new InvalidOperationException("Es ist kein Vertrag gewählt.");
        ConChange(d, "Vertrag gelöscht", o => o.Vertraege.RemoveAll(x => x.Id == v.Id)); d.Data["sel"] = ConOf(d).Vertraege.FirstOrDefault()?.Id ?? "";
        _progMsg[ConKey] = $"„{v.Name}“ gelöscht – Rückgängig holt ihn zurück.";
    }

    async Task ConPickDocument(ProgramDocument d)
    {
        var f = await PickMediaFile("con-doc", "Dokument zum Vertrag", ["*.pdf", "*.png", "*.jpg", "*.jpeg"]);
        if (f is not null) ProgramRun(ConKey, () => ConAttach(d, f));
    }

    public void ConAttach(ProgramDocument d, string file)
    {
        var v = ConSelected(d) ?? throw new InvalidOperationException("Erst einen Vertrag wählen.");
        if (!File.Exists(file)) throw new FileNotFoundException("Die Datei gibt es nicht.", file);
        ConChange(d, "Dokument", o => { var x = o.Vertraege.First(y => y.Id == v.Id); if (!x.Dokumente.Contains(file)) x.Dokumente.Add(file); });
        _progMsg[ConKey] = $"„{Path.GetFileName(file)}“ an „{v.Name}“ angehängt.";
    }

    public string ConIcsNow(ProgramDocument d)
    {
        var o = ConOf(d); var ics = Vertraege.Ics(o.Vertraege, ConNow, o.VorlaufTage);
        var f = Path.Combine(WorkFolder("Verträge"), $"Kündigungsfristen {ConNow:yyyy-MM-dd}.ics");
        File.WriteAllText(f, ics, new UTF8Encoding(false));
        var n = ics.Split("\r\n").Count(l => l == "BEGIN:VEVENT");
        _core.Audit.Append(Principal.LocalOwner.Name, "contracts.calendar", $"{n} Fristen");
        _progMsg[ConKey] = $"{n} Fristen als Kalenderdatei mit Erinnerung {o.VorlaufTage} Tage vorher: {f} – Doppelklick übernimmt sie in deinen Kalender.";
        NotifyFileResult(_progMsg[ConKey]);
        return f;
    }

    LetterDraft ConLetterDraft(ProgramDocument d)
    {
        var v = ConSelected(d) ?? throw new InvalidOperationException("Erst einen Vertrag wählen.");
        return Vertraege.Kuendigung(v, ConNow, NewLetter());
    }

    /// <summary>Kündigung als Entwurf an „Briefe schreiben“ übergeben (dessen Kern und Oberfläche) – dort prüfen, unterschreiben, selbst abschicken.</summary>
    public void ConToLetter(ProgramDocument d)
    {
        var v = ConSelected(d)!; var draft = ConLetterDraft(d);
        _letter = draft; _letterGallery = false; _letterSaved = false; _letterStep = 3;
        _letterMsg = $"Kündigung „{v.Name}“ aus dem Verträge-Ordner übernommen – Anschrift prüfen, unterschreiben, selbst abschicken (am besten per Einschreiben). Trendo versendet nichts.";
        _progMsg[ConKey] = "Kündigung an „Briefe schreiben“ übergeben.";
        Avalonia.Threading.Dispatcher.UIThread.Post(() => Show("brief")); // nach dem Neuzeichnen des Programms (ProgramRun) wechseln
    }

    public string ConLetterPdfNow(ProgramDocument d)
    {
        var v = ConSelected(d) ?? throw new InvalidOperationException("Erst einen Vertrag wählen.");
        var stem = DevStudio.SafeName("Kündigung " + v.Name); var dir = WorkFolder("Verträge");
        var f = Path.Combine(dir, stem + ".pdf"); for (var n = 2; File.Exists(f); n++) f = Path.Combine(dir, $"{stem} ({n}).pdf");
        LetterPdf.Write(ConLetterDraft(d), f);
        _progMsg[ConKey] = $"Kündigungsschreiben als PDF: {f} – ausdrucken, unterschreiben, selbst abschicken.";
        NotifyFileResult(_progMsg[ConKey]);
        return f;
    }

    public void ConMarkCancelled(ProgramDocument d)
    {
        var v = ConSelected(d) ?? throw new InvalidOperationException("Erst einen Vertrag wählen.");
        var s = Vertraege.Stand(v, ConNow);
        if (s.Ende is not { } end) throw new InvalidOperationException("Für diesen Vertrag lässt sich kein Ende berechnen – Beginn und Vertragsdauer prüfen.");
        ConChange(d, "Gekündigt", o => o.Vertraege.First(x => x.Id == v.Id).GekuendigtZum = Vertraege.Iso(end));
        _progMsg[ConKey] = $"„{v.Name}“ als gekündigt zum {Vertraege.Text(end)} markiert – zählt danach nicht mehr bei den Kosten.";
    }

    // ---------- Oberfläche ----------

    Border? _conStand;
    static IBrush ConAmpel(string a) => Brush.Parse(a switch { "rot" => "#F87171", "gelb" => "#F5A524", "gruen" => "#4ADE80", _ => "#6B7280" });

    Control ConWorkspace(ProgramDocument d)
    {
        var o = ConOf(d); var now = ConNow; var k = Vertraege.Kosten(o.Vertraege, now); var sel = ConSelected(d)?.Id;
        var stands = o.Vertraege.Select(v => (V: v, S: Vertraege.Stand(v, now))).ToList();
        var sp = new StackPanel { Spacing = 12, Margin = new Thickness(4, 0, 10, 4) };

        // Kennzahlen
        Border Tile(string name, string big, string small, IBrush? fg = null) => KrCard(new StackPanel { Spacing = 2, Children = {
            new TextBlock { Text = big, Foreground = fg ?? Text, FontSize = 22, FontWeight = FontWeight.Bold }, new TextBlock { Text = small, Foreground = Muted, FontSize = 11.5 } } }, name);
        var red = stands.Count(x => x.S.Ampel == "rot"); var yellow = stands.Count(x => x.S.Ampel == "gelb");
        sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,10,*,10,*,10,*"), Children = {
            Tile("con-kpi-month", k.Monat.ToString("0.00", ConDe) + " €", "im Monat"),
            Pos(Tile("con-kpi-year", k.Jahr.ToString("0.00", ConDe) + " €", "im Jahr"), 2),
            Pos(Tile("con-kpi-red", red.ToString(CultureInfo.InvariantCulture), "jetzt kündigen (≤ 14 Tage)", red > 0 ? ConAmpel("rot") : null), 4),
            Pos(Tile("con-kpi-yellow", yellow.ToString(CultureInfo.InvariantCulture), "bald kümmern (≤ 60 Tage)", yellow > 0 ? ConAmpel("gelb") : null), 6) } });

        // Liste
        var filter = d.Text("filter");
        var list = new StackPanel { Name = "con-list", Spacing = 4 };
        var cats = new List<(string, string)> { ("", "Alle Kategorien") }; cats.AddRange(o.Vertraege.Select(v => v.Kategorie).Distinct().OrderBy(x => x).Select(x => (x, x)));
        list.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { H($"Verträge ({o.Vertraege.Count})", 14),
            Pos(BueroCombo(ConKey, "con-filter", cats, filter, v => d.Data["filter"] = v, 170), 1) } });
        if (o.Vertraege.Count == 0) list.Children.Add(P("Noch keine Verträge. Oben „＋ Vertrag“ – z. B. Handy, Strom, Versicherung, Streaming.", Muted, 12));
        var i = 0;
        foreach (var (v, s) in stands.Where(x => filter.Length == 0 || x.V.Kategorie == filter).OrderBy(x => x.S.Ampel switch { "rot" => 0, "gelb" => 1, "gruen" => 2, _ => 3 }).ThenBy(x => x.S.KuendigenBis ?? DateTime.MaxValue))
        {
            var id = v.Id; var n = i++;
            var row = new Button
            {
                Name = "con-row-" + n, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(10, 8),
                Background = id == sel ? Brush.Parse("#33C9A227") : Brush.Parse("#14FFFFFF"),
                Content = new Grid { ColumnDefinitions = new ColumnDefinitions("14,10,*,10,110,10,2*"), Children = {
                    new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(6), Background = ConAmpel(s.Ampel), VerticalAlignment = VerticalAlignment.Center },
                    Pos(new StackPanel { Children = { new TextBlock { Text = v.Name, Foreground = Text, FontSize = 13, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                        new TextBlock { Text = $"{v.Anbieter} · {v.Kategorie}", Foreground = Muted, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis } } }, 2),
                    Pos(new TextBlock { Text = Vertraege.ProMonat(v).ToString("0.00", ConDe) + " €/Monat", Foreground = Text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right }, 4),
                    Pos(new TextBlock { Name = "con-row-" + n + "-text", Text = s.Text, Foreground = s.Ampel is "rot" or "gelb" ? ConAmpel(s.Ampel) : Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center }, 6) } },
            };
            row.Click += (_, _) => { d.Data["sel"] = id; Show(ConKey); };
            // T-0705: Rechtsklick am Vertrag; Vertrag, Rechnung oder Foto direkt auf die Zeile ziehen = an diesen Vertrag hängen
            var cancelled = Vertraege.Datum(v.GekuendigtZum) is not null;
            row.ContextMenu = BueroMenu(ConKey, "con-row-menu-" + n, () => d.Data["sel"] = id,
                ("con-ctx-letter", "Kündigung schreiben", () => ConToLetter(d), !cancelled),
                ("con-ctx-letter-pdf", "Kündigung als PDF", () => ConLetterPdfNow(d), true),
                ("con-ctx-done", "Als gekündigt markieren", () => ConMarkCancelled(d), !cancelled),
                ("-", "", () => { }, true),
                ("con-ctx-doc", "Dokument anhängen …", () => _ = ConPickDocument(d), true),
                ("con-ctx-del", "Löschen", () => ConDelete(d), true));
            BueroFileDrop(row, ConKey, BueroExt(ConDocExt), "PDFs oder Fotos (PNG, JPG)", (dd, files) => { dd.Data["sel"] = id; foreach (var f in files) ConAttach(dd, f); });
            list.Children.Add(row);
        }
        sp.Children.Add(KrCard(list, "con-list-card"));

        // Kosten nach Kategorie
        if (k.Kategorien.Count > 0)
        {
            var bars = new StackPanel { Name = "con-costs", Spacing = 5, Children = { H("Kosten nach Kategorie", 14) } };
            var max = k.Kategorien.Max(c => c.Jahr);
            foreach (var (cat, m, y, cnt) in k.Kategorien)
                bars.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("160,*,150"), Children = {
                    new TextBlock { Text = $"{cat} ({cnt})", Foreground = Text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center },
                    Pos(new Border { Height = 12, CornerRadius = new CornerRadius(3), Background = Accent, HorizontalAlignment = HorizontalAlignment.Left, Width = max > 0 ? Math.Max(4, (double)(y / max) * 300) : 4, VerticalAlignment = VerticalAlignment.Center }, 1),
                    Pos(new TextBlock { Text = $"{m.ToString("0.00", ConDe)} € / {y.ToString("0", ConDe)} € im Jahr", Foreground = Muted, FontSize = 11.5, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center }, 2) } });
            if (k.Gespart > 0) bars.Children.Add(P($"Durch Kündigungen gespart: {k.Gespart.ToString("0.00", ConDe)} € im Jahr", K4Good, 12));
            sp.Children.Add(KrCard(bars, "con-costs-card"));
        }
        // T-0705: auf die freie Fläche gezogen hängt das Dokument am gewählten Vertrag (ohne Vertrag: neuer Vertrag mit dem Dokument)
        BueroFileDrop(sp, ConKey, BueroExt(ConDocExt), "PDFs oder Fotos (PNG, JPG)", (dd, files) => { if (ConSelected(dd) is null) ConAdd(dd); foreach (var f in files) ConAttach(dd, f); });
        return sp;
    }

    static readonly string[] ConDocExt = [".pdf", ".png", ".jpg", ".jpeg"];

    Control ConProperties(ProgramDocument d)
    {
        var sp = new StackPanel { Name = "con-props", Spacing = 6 };
        var v = ConSelected(d);
        if (v is null) { sp.Children.Add(P("Kein Vertrag gewählt.", Muted, 12)); return sp; }
        var id = v.Id; var s = Vertraege.Stand(v, ConNow);
        void Set(string label, Action<Vertrag> a)
        {
            ConChange(d, label, o => { if (o.Vertraege.FirstOrDefault(x => x.Id == id) is { } x) a(x); });
            // Stand sofort zeigen, ohne die Seite neu zu zeichnen (das Eingabefeld behält den Cursor)
            if (ConOf(d).Vertraege.FirstOrDefault(x => x.Id == id) is { } now && _conStand is { } box && box.Child is TextBlock tb)
            { var st = Vertraege.Stand(now, ConNow); tb.Text = st.Text; box.BorderBrush = ConAmpel(st.Ampel); }
        }
        TextBox F(string key, string label, string value, Action<Vertrag, string> set, bool multi = false, double w = double.NaN) => BueroBox("con-f-" + key, value, label, t => Set(label, x => set(x, t)), multi, w);

        sp.Children.Add(_conStand = new Border { Name = "con-stand", Padding = new Thickness(10, 8), CornerRadius = new CornerRadius(8), Background = Brush.Parse("#1A000000"), BorderBrush = ConAmpel(s.Ampel), BorderThickness = new Thickness(2, 0, 0, 0),
            Child = new TextBlock { Name = "con-stand-text", Text = s.Text, Foreground = Text, FontSize = 12.5, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap } });
        sp.Children.Add(H("Vertrag", 13));
        sp.Children.Add(BueroLabeled("Name", F("name", "z. B. Handyvertrag", v.Name, (x, t) => x.Name = t)));
        sp.Children.Add(BueroLabeled("Anbieter", F("anbieter", "Firma", v.Anbieter, (x, t) => x.Anbieter = t)));
        sp.Children.Add(BueroLabeled("Anschrift des Anbieters (für die Kündigung)", F("anschrift", "Straße\nPLZ Ort", v.Anschrift, (x, t) => x.Anschrift = t, true)));
        sp.Children.Add(BueroLabeled("Kunden- oder Vertragsnummer", F("nr", "steht auf jeder Rechnung", v.Kundennummer, (x, t) => x.Kundennummer = t)));
        sp.Children.Add(BueroLabeled("Kategorie", BueroCombo(ConKey, "con-f-kategorie", Vertraege.Kategorien.Select(c => (c, c)), v.Kategorie, t => Set("Kategorie", x => x.Kategorie = t), 200)));

        sp.Children.Add(H("Vertragsdauer und Kündigungsfrist", 13));
        sp.Children.Add(BueroLabeled("Beginn (TT.MM.JJJJ)", F("beginn", "z. B. 15.03.2025", Vertraege.Datum(v.Beginn) is { } b ? Vertraege.Text(b) : v.Beginn, (x, t) => x.Beginn = Vertraege.Datum(t) is { } dd ? Vertraege.Iso(dd) : t)));
        static int Num(string t, int old) => int.TryParse(t.Trim(), out var n) && n is >= 0 and <= 600 ? n : old;
        sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,*"), Children = {
            BueroLabeled("Mindestdauer (Monate, 0 = unbefristet)", F("laufzeit", "24", v.LaufzeitMonate.ToString(CultureInfo.InvariantCulture), (x, t) => x.LaufzeitMonate = Num(t, x.LaufzeitMonate))),
            Pos(BueroLabeled("Verlängert sich um (Monate, 0 = endet)", F("verl", "1", v.VerlaengerungMonate.ToString(CultureInfo.InvariantCulture), (x, t) => x.VerlaengerungMonate = Num(t, x.VerlaengerungMonate))), 2) } });
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
            new TextBlock { Text = "Kündigungsfrist", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center },
            F("frist", "1", v.FristWert.ToString(CultureInfo.InvariantCulture), (x, t) => x.FristWert = Num(t, x.FristWert), false, 50),
            BueroCombo(ConKey, "con-f-einheit", Vertraege.Einheiten, v.FristEinheit, t => Set("Fristeinheit", x => x.FristEinheit = t), 100) } });
        if (v.LaufzeitMonate == 0)
        {
            var me = new CheckBox { Name = "con-f-monatsende", Content = "Kündigung nur zum Monatsende", IsChecked = v.ZumMonatsende, Foreground = Text, FontSize = 12 };
            me.IsCheckedChanged += (_, _) => { if ((me.IsChecked == true) != v.ZumMonatsende) ProgramRun(ConKey, () => Set("Monatsende", x => x.ZumMonatsende = me.IsChecked == true)); };
            sp.Children.Add(me);
        }

        sp.Children.Add(H("Kosten", 13));
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
            F("kosten", "0,00", v.Kosten.ToString("0.00", ConDe), (x, t) => x.Kosten = decimal.TryParse(t.Replace("€", "").Trim(), NumberStyles.Number, ConDe, out var c) && c >= 0 ? c : x.Kosten, false, 90),
            new TextBlock { Text = "€", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center },
            BueroCombo(ConKey, "con-f-intervall", Vertraege.Intervalle.Select(x => (x.Id, x.Name)), v.Intervall, t => Set("Zahlweise", x => x.Intervall = t), 130) } });
        sp.Children.Add(P($"= {Vertraege.ProMonat(v).ToString("0.00", ConDe)} € im Monat, {Vertraege.ProJahr(v).ToString("0.00", ConDe)} € im Jahr", Muted, 11));

        sp.Children.Add(H("Dokumente", 13));
        var di = 0;
        foreach (var f in v.Dokumente)
        {
            var ff = f; var n = di++;
            sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,4,Auto"), Children = {
                new TextBlock { Text = (File.Exists(f) ? "📄 " : "⚠ fehlt: ") + Path.GetFileName(f), Foreground = File.Exists(f) ? Text : K4Warn, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis },
                Pos(KrBtn($"con-doc-{n}-open", "Öffnen", () => { if (!File.Exists(ff)) throw new FileNotFoundException("Die Datei gibt es nicht mehr.", ff); Launch(ff); }, key: ConKey), 1),
                Pos(KrBtn($"con-doc-{n}-del", "✕", () => Set("Dokument entfernt", x => x.Dokumente.Remove(ff)), key: ConKey), 3) } });
        }
        sp.Children.Add(KrBtn("con-doc-add", "Dokument anhängen …", () => _ = ConPickDocument(d), key: ConKey));
        sp.Children.Add(BueroLabeled("Notiz", F("notiz", "z. B. Kündigung per Einschreiben geschickt am …", v.Notiz, (x, t) => x.Notiz = t, true)));
        if (v.GekuendigtZum.Length > 0) sp.Children.Add(KrBtn("con-undone", "Doch nicht gekündigt", () => Set("Nicht gekündigt", x => x.GekuendigtZum = ""), key: ConKey));
        else sp.Children.Add(new WrapPanel { Children = {
            KrBtn("con-letter-btn", "Kündigung schreiben", () => ConToLetter(d), true, ConKey).Also(b => b.Margin = new Thickness(0, 0, 6, 6)),
            KrBtn("con-done-btn", "Als gekündigt markieren", () => ConMarkCancelled(d), key: ConKey) } });
        sp.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = {
            new TextBlock { Text = "Erinnerung", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center },
            BueroBox("con-lead", ConOf(d).VorlaufTage.ToString(CultureInfo.InvariantCulture), "Tage vorher", t => { if (int.TryParse(t, out var n) && n is >= 0 and <= 365) ConChange(d, "Vorlauf", o => o.VorlaufTage = n); }, false, 50),
            new TextBlock { Text = "Tage vor dem letzten Kündigungstag", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center } } });
        sp.Children.Add(P("Keine Rechtsberatung. Sonderkündigungsrechte (Umzug, Preiserhöhung) prüfst du selbst.", Muted, 10.5));
        return sp;
    }
}
