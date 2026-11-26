using System.Text;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Trendo.Core;
using Trendo.Core.Lernen;
using Trendo.Core.Programs;

namespace Trendo.App;

/// <summary>T-0782 STUDIO-TUTOR-01 „Nachhilfe mit KI“: Lernpfad je Fach und Klasse links, Übung mit Eingabe, Hinweisen und Lösungsweg in der Mitte,
/// Fortschritt, Wiederholungsplan und eigene Übungen rechts. KI nur auf diesem PC (auch die KI-Kette), sokratisch, ohne Links und ohne Sendewege.
/// Kern: <see cref="Nachhilfe"/>. Vorbilder (nur Arbeitsweise): Khan Academy/Khanmigo, Duolingo, ANTON, simpleclub. Anforderungen: docs/nachbau/nachhilfe/anforderungen.md.</summary>
public partial class MainWindow
{
    public const string TutorKey = "nachhilfe-programm";
    const string TutorStudio = "STUDIO-TUTOR-01";
    /// <summary>Eingabe je Dokument (überlebt das Neuzeichnen, gehört nicht ins Dokument).</summary>
    readonly Dictionary<string, string> _nhInput = [];
    /// <summary>Letzte Rückmeldung, Hinweise und KI-Antwort je Dokument (Anzeige).</summary>
    readonly Dictionary<string, (string Text, bool Ok)> _nhFeedback = [];
    readonly Dictionary<string, string> _nhKi = [];
    readonly Dictionary<string, string> _nhKiQ = [];
    bool _nhBusy;
    /// <summary>Tests: Zufall der erzeugten Aufgaben festlegen.</summary>
    public static int? TutorSeed { get; set; }

    private void InitTutor() => RegisterProgram(FachTabs, new ProgramSpec(TutorKey, TutorStudio, "Nachhilfe mit KI")
    {
        Sentence = "Üben nach Lernpfad je Fach und Klasse – mit Hinweisen statt fertiger Lösung, Fortschritt je Thema und Wiederholung zur rechten Zeit. Die KI läuft nur auf diesem PC.",
        Folder = "Nachhilfe",
        ScrollWorkspace = true,
        GroupLevels = new() { ["Üben"] = 0, ["Lernpfad"] = 0, ["Einstellen"] = 1 },
        Templates =
        [
            new("leer", "Mathe, Klasse 5", "Lernpfad Mathe – Fach und Klasse jederzeit änderbar", () => Nachhilfe.Neu("mathe", 5)),
            new("deutsch", "Deutsch, Klasse 4", "Wortarten, Mehrzahl, das/dass", () => Nachhilfe.Neu("deutsch", 4)),
            new("englisch", "Englisch, Klasse 6", "Unregelmäßige Verben, Vokabeln", () => Nachhilfe.Neu("englisch", 6)),
            new("mathe8", "Mathe, Klasse 8", "Prozent, Gleichungen", () => Nachhilfe.Neu("mathe", 8)),
        ],
        Ribbon =
        [
            ("Üben", [
                new ProgramCmd("nh-next", "Nächste Übung", d => TutorNext(d), Primary: true, Tip: "Holt die nächste Übung im gewählten Thema."),
                new ProgramCmd("nh-check", "Prüfen", d => TutorCheck(d), Tip: "Deine Antwort prüfen (Eingabetaste)."),
                new ProgramCmd("nh-hint", "Hinweis", d => TutorHint(d), Tip: "Ein Denkanstoß – die Lösung verrät er nicht."),
                new ProgramCmd("nh-solution", "Lösungsweg zeigen", d => TutorSolution(d), Tip: "Erst nach einem eigenen Versuch (sokratischer Modus)."),
                new ProgramCmd("nh-ki-explain", "KI erklärt das Thema", d => _ = TutorAskAsync(d, "Erklär mir das Thema mit einem einfachen Beispiel, ohne die aktuelle Aufgabe zu lösen."), Tip: "Lokale KI, ohne Internet."),
            ]),
            ("Lernpfad", [
                new ProgramCmd("nh-due", "Heute wiederholen", d => TutorDue(d), Tip: "Springt zum Thema, das als Nächstes wiederholt werden sollte."),
                new ProgramCmd("nh-import", "Übungen einlesen …", d => _ = TutorPickImport(d), Tip: "CSV aus LibreOffice Calc oder Excel: Thema;Frage;Antwort;Erklärung;Hinweise.", Level: 1),
                new ProgramCmd("nh-cards", "In Karteikarten übernehmen", d => TutorToCards(d), Tip: "Eigene Übungen des Themas als Karten in „Lernen mit Karteikarten“.", Level: 1),
            ]),
            ("Einstellen", [
                new ProgramCmd("nh-socratic", "Sokratischer Modus an/aus", d => TutorToggleSocratic(d), Tip: "An: Lösungsweg erst nach einem Versuch. Im Kinderprofil immer an.", Level: 1),
            ]),
        ],
        // T-0782: KI-Fachaktion – eine Übung ins offene Dokument (Vorschau → „Ja, behalten“, ein Rückgängig-Schritt). Nur lokale KI.
        KiActions = [TutorKiExercise],
        KiHint = "Mit „uebung.erzeugen“ legst du GENAU EINE neue Übung zum aktuellen Thema an: frage, antwort (mehrere richtige Antworten mit | trennen), erklaerung (Lösungsweg) und hinweis (ein Denkanstoß, der die Lösung NICHT verrät). "
               + "Keine Links, keine Adressen. Fortschritt, Verlauf und Einstellungen änderst du nicht.",
        KiLocked = ["themen", "verlauf", "eigene", "uebung", "sokratisch", "zaehler"],
        KiLocalOnly = () => true,
        KiView = Nachhilfe.KiSicht, // M6: die KI sieht keine Antworten, Lösungswege, Hinweise und Eingaben
        KiShow = Nachhilfe.FuerAnzeige, // 2. Gegenprüfung M6: Plan, Vorschau und Protokoll ohne Lösungen
        KiAllowed = ["fach", "klasse", "thema"], // 2. Gegenprüfung M6: Positivliste für „dokument.setzen“
        KiAllowedValue = Nachhilfe.KiWertProblem, // 3. Gegenprüfung M6: nur gültige Themen, Fächer und Klassen
        Exports =
        [
            new("odt", "Arbeitsblatt (.odt)", ".odt", (d, f) => { Trendo.Core.Office.OdtFormat.Write(Nachhilfe.Arbeitsblatt(d, 8, TutorSeed ?? Environment.TickCount), f); return f; }),
            new("docx", "Arbeitsblatt (.docx)", ".docx", (d, f) => { Trendo.Core.Office.DocxFormat.Write(Nachhilfe.Arbeitsblatt(d, 8, TutorSeed ?? Environment.TickCount), f); return f; }),
            new("csv", "Eigene Übungen (.csv)", ".csv", (d, f) => { if (d.List("eigene").Count == 0) throw new InvalidOperationException("Es gibt noch keine eigenen Übungen."); File.WriteAllText(f, Nachhilfe.CsvExport(d), new UTF8Encoding(true)); return f; }),
        ],
        SimpleExports = 1,
        Menus = d =>
        [
            new("Üben",
            [
                new("nh-key-check", "Antwort prüfen", () => ProgramRun(TutorKey, () => TutorCheck(d)), "Strg+Umschalt+P", Tip: "Oder Eingabetaste im Antwortfeld"),
                new("nh-key-hint", "Hinweis", () => ProgramRun(TutorKey, () => TutorHint(d)), "Strg+J"),
                new("nh-key-next", "Nächste Übung", () => ProgramRun(TutorKey, () => TutorNext(d)), "Strg+Umschalt+N"),
                new("nh-key-solution", "Lösungsweg zeigen", () => ProgramRun(TutorKey, () => TutorSolution(d)), "Strg+L"),
            ]),
        ],
        FirstStep = new("Erste Übung holen", "Wähle Fach und Klasse und ein Thema im Lernpfad. Dann geht es los – Hinweise gibt es jederzeit, den Lösungsweg nach deinem ersten Versuch.",
            d => TutorNext(d), d => Nachhilfe.Uebung(d) is null && d.List("verlauf").Count == 0),
        Workspace = TutorWorkspace,
        Properties = TutorProperties,
        Status = d => { var t = Nachhilfe.AktuellesThema(d); var s = t is null ? null : Nachhilfe.Stand(d, t); var due = Nachhilfe.Wiederholungsplan(d).Count(x => x.Faellig <= DateTime.Today);
            return $"{Nachhilfe.Faecher.FirstOrDefault(f => f.Id == Nachhilfe.FachOf(d))?.Name} · Klasse {Nachhilfe.KlasseOf(d)} · {t?.Titel ?? "kein Thema"}" + (s is null ? "" : $" · {Nachhilfe.StufeName(s.Stufe)}") + $" · Fortschritt {Nachhilfe.Fortschritt(d)} %" + (due > 0 ? $" · {due} Thema/Themen heute wiederholen" : "") + (d.Flag("sokratisch") ? " · sokratisch" : ""); },
        Help = "Unter „Eigenschaften“ Fach und Klasse wählen, im Lernpfad ein Thema. „Nächste Übung“ holt eine Aufgabe; Antwort eintippen und mit der Eingabetaste prüfen. „Hinweis“ gibt Denkanstöße, "
             + "„Lösungsweg zeigen“ geht erst nach einem eigenen Versuch. Nach fünf Übungen bewertet Trendo die Runde und plant die nächste Wiederholung (FSRS). Die KI läuft nur auf diesem PC, "
             + "antwortet ohne Links und verrät keine Lösung einer offenen Übung. Eigene Übungen legst du unter „Eigenschaften“ an oder liest sie als CSV ein; das Arbeitsblatt gibt es als ODT oder DOCX.",
    }, "nachhilfe", "hausaufgaben", "üben", "lernpfad", "mathe üben", "einmaleins", "brüche", "prozent", "englisch vokabeln", "das dass", "khanmigo", "anton", "duolingo", "simpleclub");

    // ---------- Fachaktion für die KI ----------

    ProgramKiAction TutorKiExercise => new(new AssistAction("uebung.erzeugen", "Übung erzeugen", ["übung", "uebung", "aufgabe", "erzeug", "neue", "üben", "frage"],
        [new AssistParam("frage", "Frage"), new AssistParam("antwort", "Antwort"),
         new AssistParam("erklaerung", "Lösungsweg", Required: false), new AssistParam("hinweis", "Hinweis", Required: false)]),
        (d, a) =>
        {
            var t = Nachhilfe.AktuellesThema(d) ?? throw new InvalidOperationException("Wähle zuerst ein Thema im Lernpfad.");
            Nachhilfe.UebungAnlegen(d, t.Id, a.GetValueOrDefault("frage") ?? "", a.GetValueOrDefault("antwort") ?? "", a.GetValueOrDefault("erklaerung") ?? "",
                a.GetValueOrDefault("hinweis") is { Length: > 0 } h ? [h] : [], "ki");
            _nhInput.Remove(d.Id); _nhFeedback.Remove(d.Id);
        },
        (_, _) => [])
    { ApplyNote = "Die Übung steht erst nach „Ja, behalten“ im offenen Lernplan – „Rückgängig“ nimmt sie wieder heraus." };

    // ---------- Befehle ----------

    Random? TutorRng() => TutorSeed is { } s ? new Random(s + _nhFeedback.Count) : null;

    void TutorNext(ProgramDocument d)
    {
        Nachhilfe.NaechsteUebung(d, TutorRng());
        _nhInput.Remove(d.Id); _nhFeedback.Remove(d.Id); _nhKi.Remove(d.Id);
        _progMsg[TutorKey] = "Neue Übung – viel Erfolg!";
    }

    void TutorCheck(ProgramDocument d)
    {
        var (ok, msg) = Nachhilfe.Pruefen(d, _nhInput.GetValueOrDefault(d.Id) ?? "");
        _nhFeedback[d.Id] = (msg, ok);
        var st = Nachhilfe.AktuellesThema(d) is { } t ? Nachhilfe.Stand(d, t) : null;
        _progMsg[TutorKey] = ok ? "✓ Richtig!" + (st is { InRunde: 0, Faellig: { } f } ? $" Runde fertig – nächste Wiederholung am {f:dd.MM.yyyy}." : "") : msg;
    }

    void TutorHint(ProgramDocument d)
    {
        var h = Nachhilfe.Hinweis(d);
        _progMsg[TutorKey] = h is null ? "Alle Hinweise sind schon zu sehen." + (Nachhilfe.Uebung(d)?["versuche"].AsText() is "0" ? " Versuch es jetzt – danach gibt es den Lösungsweg." : "") : "Hinweis: " + h;
    }

    void TutorSolution(ProgramDocument d)
    {
        var s = Nachhilfe.LoesungZeigen(d);
        _nhFeedback[d.Id] = (s, false);
        _progMsg[TutorKey] = "Lösungsweg gezeigt – die Übung zählt als „noch üben“.";
    }

    void TutorDue(ProgramDocument d)
    {
        var plan = Nachhilfe.Wiederholungsplan(d);
        var first = plan.FirstOrDefault(p => p.Faellig <= DateTime.Today) ?? plan.FirstOrDefault();
        if (first is null) { _progMsg[TutorKey] = "Noch nichts zu wiederholen – übe zuerst eine Runde (5 Übungen)."; return; }
        Nachhilfe.ThemaWaehlen(d, first.Thema.Id);
        TutorNext(d);
        _progMsg[TutorKey] = first.Faellig <= DateTime.Today ? $"Wiederholung: {first.Thema.Titel}." : $"Heute ist nichts fällig. Als Nächstes: {first.Thema.Titel} am {first.Faellig:dd.MM.yyyy}.";
    }

    void TutorToggleSocratic(ProgramDocument d)
    {
        if (_roleChild && d.Flag("sokratisch")) throw new InvalidOperationException("Im Kinderprofil bleibt der sokratische Modus an – das ändert nur eine erwachsene Person.");
        d.Set("sokratisch", !d.Flag("sokratisch"), d.Flag("sokratisch") ? "Sokratisch aus" : "Sokratisch an");
        _progMsg[TutorKey] = d.Flag("sokratisch") ? "Sokratischer Modus an: Lösungsweg erst nach einem Versuch." : "Sokratischer Modus aus: Lösungsweg jederzeit.";
    }

    void TutorToCards(ProgramDocument d)
    {
        var t = Nachhilfe.AktuellesThema(d) ?? throw new InvalidOperationException("Wähle zuerst ein Thema.");
        var own = Nachhilfe.Eigene(d, t.Id);
        if (own.Count == 0) throw new InvalidOperationException("Dieses Thema hat noch keine eigenen Übungen – lege unter „Eigenschaften“ eine an oder lies eine CSV ein.");
        var box = new LearnBox(_core.DataRoot); var deck = $"{Nachhilfe.Faecher.First(f => f.Id == t.Fach).Name}: {t.Titel}";
        var have = box.Store.Cards.Where(c => c.Deck == deck).Select(c => c.Front).ToHashSet();
        var n = 0;
        foreach (var (_, item, _) in own.Where(o => !have.Contains(o.Item.Frage))) { box.Add(deck, item.Frage, item.Antworten[0] + (item.Erklaerung.Length > 0 ? " – " + item.Erklaerung : "")); n++; }
        _core.Audit.Append(CurrentPrincipal.Name, "nachhilfe.karteikarten", $"{t.Id} {n}");
        _progMsg[TutorKey] = n == 0 ? "Alle Übungen stehen schon als Karten im Stapel „" + deck + "“." : $"{n} Karte(n) im Stapel „{deck}“ – unter „Lernen mit Karteikarten“.";
    }

    async Task TutorPickImport(ProgramDocument d)
    {
        if (await PickMediaFile("nh-import", "Übungen (CSV)", ["*.csv", "*.txt", "*.tsv"]) is { } f) ProgramRun(TutorKey, () => TutorImport(d, f));
    }

    public void TutorImport(ProgramDocument d, string file)
    {
        if (new FileInfo(file).Length > 2_000_000) throw new InvalidDataException("Die Datei ist zu groß (höchstens 2 MB).");
        var (n, skip) = Nachhilfe.CsvImport(d, File.ReadAllText(file));
        _progMsg[TutorKey] = $"{n} Übung(en) aus „{Path.GetFileName(file)}“ übernommen" + (skip.Count > 0 ? $", {skip.Count} übersprungen: " + string.Join("; ", skip.Take(3)) : ".") + (n > 0 ? " Strg+Z nimmt sie wieder heraus." : "");
    }

    /// <summary>Frage an die KI auf diesem PC (nie Internet). Antwort ohne Links; verrät sie die Lösung einer offenen Übung, wird sie zurückgehalten.</summary>
    public async Task TutorAskAsync(ProgramDocument d, string frage)
    {
        if (_nhBusy) { _progMsg[TutorKey] = "Die KI denkt noch nach …"; Show(TutorKey); return; }
        if (string.IsNullOrWhiteSpace(frage)) { _progMsg[TutorKey] = "Schreib zuerst deine Frage an die KI."; Show(TutorKey); return; }
        _nhBusy = true; _nhKi[d.Id] = "Die KI auf diesem PC denkt nach …"; Show(TutorKey);
        try
        {
            var core = AgentKi("lokal", out var pref);
            var a = await core.AskAsync(new KiRequest(Nachhilfe.KiFrage(d, frage), Nachhilfe.KiRolle, pref, UseKnowledge: false));
            if (!a.Ok) { _nhKi[d.Id] = "Die KI auf diesem PC antwortet gerade nicht: " + a.Text + " (Einrichten unter „KI fragen“ → Modell.)"; return; }
            var (text, held) = Nachhilfe.KiAntwortFiltern(d, a.Text);
            _nhKi[d.Id] = text;
            _core.Audit.Append(CurrentPrincipal.Name, "nachhilfe.ki", $"{d.Text("thema")} zeichen={a.Text.Length} zurueckgehalten={held} quelle={a.Backend}");
        }
        finally { _nhBusy = false; Show(TutorKey); }
    }

    // ---------- Oberfläche ----------

    static IBrush TutorStufeBrush(string s) => s switch { "gemeistert" => Brush.Parse("#D4A017"), "sicher" => K4Good, "vertraut" => Brush.Parse("#93C5FD"), "versucht" => K4Warn, _ => Muted };

    Control TutorWorkspace(ProgramDocument d)
    {
        var fach = Nachhilfe.FachOf(d); var klasse = Nachhilfe.KlasseOf(d);
        var pfad = Nachhilfe.Pfad(fach, klasse);
        var left = new StackPanel { Name = "nh-pfad", Spacing = 6, Width = 260 };
        if (ProgramLevel == UserLevel.Einfach) left.Children.Add(TutorFachBox(d)); // Einfach: keine Eigenschaften-Spalte – Fach und Klasse stehen dann hier
        left.Children.Add(H($"Lernpfad {Nachhilfe.Faecher.First(f => f.Id == fach).Name} · Klasse {klasse}", 14));
        left.Children.Add(new ProgressBar { Name = "nh-fortschritt", Minimum = 0, Maximum = 100, Value = Nachhilfe.Fortschritt(d), Height = 8, Foreground = Accent });
        left.Children.Add(P($"{Nachhilfe.Fortschritt(d)} % sicher beherrscht", Muted, 11));
        var i = 0;
        foreach (var t in pfad)
        {
            var tt = t; var st = Nachhilfe.Stand(d, t); var on = d.Text("thema") == t.Id;
            var due = st.Faellig is { } f && f <= DateTime.Today;
            var b = new Button { Name = "nh-thema-" + i++, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(10, 6),
                Background = on ? Brush.Parse("#33D4A017") : Brushes.Transparent, BorderBrush = on ? Accent : Brush.Parse("#22FFFFFF"), BorderThickness = new Thickness(1),
                Content = new StackPanel { Spacing = 2, Children = {
                    new TextBlock { Text = t.Titel, Foreground = Text, FontSize = 13, FontWeight = on ? FontWeight.SemiBold : FontWeight.Normal, TextWrapping = TextWrapping.Wrap },
                    new TextBlock { Text = $"● {Nachhilfe.StufeName(st.Stufe)}" + (st.Faellig is { } ff ? (due ? " · heute wiederholen" : $" · wieder am {ff:dd.MM.}") : "") + $" · Kl. {t.VonKlasse}–{t.BisKlasse}", Foreground = due ? K4Warn : TutorStufeBrush(st.Stufe), FontSize = 11 } } } };
            ToolTip.SetTip(b, t.Einfuehrung);
            b.Click += (_, _) => ProgramRun(TutorKey, () => { Nachhilfe.ThemaWaehlen(d, tt.Id); _nhFeedback.Remove(d.Id); _nhKi.Remove(d.Id); });
            b.ContextMenu = BueroMenu(TutorKey, "nh-thema-menu", null,
                ("nh-ctx-practice", "Dieses Thema üben", () => { Nachhilfe.ThemaWaehlen(d, tt.Id); TutorNext(d); }, true),
                ("nh-ctx-sheet", "Arbeitsblatt dazu (Export)", () => { Nachhilfe.ThemaWaehlen(d, tt.Id); _progMsg[TutorKey] = "Oben unter „Exportieren“ das Arbeitsblatt als ODT oder DOCX speichern."; }, true));
            left.Children.Add(b);
        }
        if (pfad.Count == 0) left.Children.Add(P("Für diese Klasse gibt es in diesem Fach noch keine Themen – wähle eine andere Klasse.", Muted, 12));

        var center = new StackPanel { Name = "nh-mitte", Spacing = 10 };
        var thema = Nachhilfe.AktuellesThema(d);
        if (thema is not null) center.Children.Add(KrCard(new StackPanel { Spacing = 4, Children = { H(thema.Titel, 16), P(thema.Einfuehrung, Muted, 12.5) } }, "nh-thema-karte"));
        var u = Nachhilfe.Uebung(d);
        if (u is null) center.Children.Add(KrCard(P("Noch keine Übung offen. Oben „Nächste Übung“ – oder ein Thema im Lernpfad wählen.", Muted, 13)));
        else
        {
            var card = new StackPanel { Spacing = 8 };
            var herkunft = u["quelle"].AsText() switch { "ki" => " · von der KI erzeugt", "eigen" => " · eigene Übung", _ => "" };
            card.Children.Add(new TextBlock { Text = "Aufgabe" + herkunft, Foreground = Muted, FontSize = 11 });
            card.Children.Add(new SelectableTextBlock { Name = "nh-frage", Text = u["frage"].AsText(), Foreground = Text, FontSize = 20, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
            var done = u["geloest"].AsBool() || u["loesungGezeigt"].AsBool();
            var input = new TextBox { Name = "nh-eingabe", Text = _nhInput.GetValueOrDefault(d.Id) ?? "", PlaceholderText = "Deine Antwort …", FontSize = 18, IsEnabled = !done, MinWidth = 260 };
            input.TextChanged += (_, _) => _nhInput[d.Id] = input.Text ?? "";
            input.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; ProgramRun(TutorKey, () => TutorCheck(d)); } };
            var check = new Button { Name = "nh-pruefen", Content = "Prüfen", Padding = new Thickness(14, 6), IsEnabled = !done };
            check.Click += (_, _) => ProgramRun(TutorKey, () => TutorCheck(d));
            var hint = new Button { Name = "nh-hinweis", Content = "Hinweis", Padding = new Thickness(12, 6), IsEnabled = !done };
            hint.Click += (_, _) => ProgramRun(TutorKey, () => TutorHint(d));
            var versuche = Nachhilfe.VersucheEcht(d); // M6: auch nach Strg+Z
            var sol = new Button { Name = "nh-loesung", Content = "Lösungsweg zeigen", Padding = new Thickness(12, 6), IsEnabled = !d.Flag("sokratisch") || versuche > 0 || done };
            ToolTip.SetTip(sol, d.Flag("sokratisch") && versuche == 0 ? "Erst nach deinem ersten Versuch." : "Zeigt die Lösung mit Rechenweg.");
            sol.Click += (_, _) => ProgramRun(TutorKey, () => TutorSolution(d));
            card.Children.Add(new WrapPanel { Children = { input, new Border { Width = 8 }, check, new Border { Width = 6 }, hint, new Border { Width = 6 }, sol } });
            if (_nhFeedback.TryGetValue(d.Id, out var fb))
                card.Children.Add(new SelectableTextBlock { Name = "nh-rueckmeldung", Text = (fb.Ok ? "✓ " : "") + fb.Text, Foreground = fb.Ok ? K4Good : u["loesungGezeigt"].AsBool() ? Text : K4Warn, FontSize = 13.5, TextWrapping = TextWrapping.Wrap });
            var shown = (int)ProgramDocument.ToNumber(u["hinweiseGezeigt"]);
            var hs = (u["hinweise"] as JsonArray ?? []).Select(x => x.AsText() ?? "").Take(shown).ToList();
            for (var h = 0; h < hs.Count; h++) card.Children.Add(new TextBlock { Name = "nh-hinweis-" + h, Text = $"💡 Hinweis {h + 1}: {hs[h]}", Foreground = Brush.Parse("#FDE68A"), FontSize = 12.5, TextWrapping = TextWrapping.Wrap });
            if (versuche > 0) card.Children.Add(P($"Versuche: {versuche}" + ((u["eingaben"] as JsonArray)?.Count > 0 ? " · zuletzt: " + (u["eingaben"] as JsonArray)!.Last()!.AsText() : ""), Muted, 11));
            center.Children.Add(KrCard(card, "nh-uebung"));
        }
        // KI-Bereich (nur lokal)
        var q = new TextBox { Name = "nh-ki-frage", Text = _nhKiQ.GetValueOrDefault(d.Id) ?? "", PlaceholderText = "Frag die KI, z. B. „Wie fange ich an?“", MinWidth = 300 };
        q.TextChanged += (_, _) => _nhKiQ[d.Id] = q.Text ?? "";
        q.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; _ = TutorAskAsync(d, q.Text ?? ""); } };
        var ask = new Button { Name = "nh-ki-fragen", Content = _nhBusy ? "Denkt nach …" : "KI fragen", Padding = new Thickness(12, 6), IsEnabled = !_nhBusy };
        ask.Click += (_, _) => _ = TutorAskAsync(d, q.Text ?? "");
        var ki = new StackPanel { Spacing = 6, Children = {
            new TextBlock { Text = "KI-Lernhilfe · nur auf diesem PC, ohne Internet, verrät keine Lösung", Foreground = Muted, FontSize = 11 },
            new DockPanel { Children = { new Border { [DockPanel.DockProperty] = Avalonia.Controls.Dock.Right, Margin = new Thickness(8, 0, 0, 0), Child = ask }, q } } } };
        if (_nhKi.GetValueOrDefault(d.Id) is { Length: > 0 } kt) ki.Children.Add(new SelectableTextBlock { Name = "nh-ki-antwort", Text = kt, Foreground = Text, FontSize = 13, TextWrapping = TextWrapping.Wrap });
        center.Children.Add(KrCard(ki, "nh-ki"));

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,16,*") };
        grid.Children.Add(left); Grid.SetColumn(left, 0);
        grid.Children.Add(center); Grid.SetColumn(center, 2);
        BueroFileDrop(grid, TutorKey, BueroExt(".csv", ".tsv", ".txt"), "CSV-Dateien mit Übungen", (doc, files) => { foreach (var f in files) TutorImport(doc, f); });
        return grid;
    }

    Control TutorProperties(ProgramDocument d)
    {
        var sp = new StackPanel { Spacing = 8, Width = 300 };
        sp.Children.Add(H("Fach und Klasse", 13));
        sp.Children.Add(TutorFachBox(d));
        sp.Children.Add(P(d.Flag("sokratisch") ? "Sokratischer Modus: an – Lösungsweg erst nach einem Versuch." + (_roleChild ? " (Kinderprofil: bleibt an)" : "") : "Sokratischer Modus: aus.", Muted, 11.5));
        TutorPlanAndOwn(d, sp);
        return sp;
    }

    /// <summary>Fach und Klasse wählen – in „Einfach“ (ohne Eigenschaften-Spalte) steht das über dem Lernpfad, sonst in den Eigenschaften.</summary>
    Control TutorFachBox(ProgramDocument d)
    {
        var fach = new ComboBox { Name = "nh-fach", ItemsSource = Nachhilfe.Faecher.Select(f => f.Name).ToList(), SelectedIndex = Math.Max(0, Nachhilfe.Faecher.ToList().FindIndex(f => f.Id == Nachhilfe.FachOf(d))), MinWidth = 140 };
        var klasse = new ComboBox { Name = "nh-klasse", ItemsSource = Enumerable.Range(1, 13).Select(k => $"Klasse {k}").ToList(), SelectedIndex = Nachhilfe.KlasseOf(d) - 1, MinWidth = 110 };
        void Apply() { if (fach.SelectedIndex >= 0 && klasse.SelectedIndex >= 0) ProgramRun(TutorKey, () => { var f = Nachhilfe.Faecher[fach.SelectedIndex].Id; var k = klasse.SelectedIndex + 1; if (f != Nachhilfe.FachOf(d) || k != Nachhilfe.KlasseOf(d)) Nachhilfe.FachWaehlen(d, f, k); }); }
        fach.SelectionChanged += (_, _) => Apply(); klasse.SelectionChanged += (_, _) => Apply();
        return new WrapPanel { Children = { fach, new Border { Width = 6 }, klasse } };
    }

    void TutorPlanAndOwn(ProgramDocument d, StackPanel sp)
    {

        sp.Children.Add(H("Wiederholungsplan", 13));
        var plan = Nachhilfe.Wiederholungsplan(d);
        if (plan.Count == 0) sp.Children.Add(P("Nach der ersten Runde (5 Übungen) plant Trendo hier, wann jedes Thema wiederholt wird – wer sicher ist, wiederholt seltener.", Muted, 11.5));
        var pi = 0;
        foreach (var p in plan.Take(8))
            sp.Children.Add(new TextBlock { Name = "nh-plan-" + pi++, Text = $"{(p.Faellig <= DateTime.Today ? "heute" : p.Faellig!.Value.ToString("dd.MM.yyyy"))} · {p.Thema.Titel} · {Nachhilfe.StufeName(p.Stufe)}", Foreground = p.Faellig <= DateTime.Today ? K4Warn : Text, FontSize = 12, TextWrapping = TextWrapping.Wrap });

        sp.Children.Add(H("Eigene Übung", 13));
        var fq = new TextBox { Name = "nh-eigen-frage", PlaceholderText = "Frage" };
        var fa = new TextBox { Name = "nh-eigen-antwort", PlaceholderText = "Antwort (mehrere mit | trennen)" };
        var fe = new TextBox { Name = "nh-eigen-erklaerung", PlaceholderText = "Lösungsweg (optional)" };
        var fh = new TextBox { Name = "nh-eigen-hinweis", PlaceholderText = "Hinweis ohne Lösung (optional)" };
        var add = new Button { Name = "nh-eigen-anlegen", Content = "Übung anlegen", Padding = new Thickness(12, 5) };
        add.Click += (_, _) => ProgramRun(TutorKey, () =>
        {
            var t = Nachhilfe.AktuellesThema(d) ?? throw new InvalidOperationException("Wähle zuerst ein Thema im Lernpfad.");
            Nachhilfe.UebungAnlegen(d, t.Id, fq.Text ?? "", fa.Text ?? "", fe.Text ?? "", fh.Text is { Length: > 0 } h ? [h] : [], "eigen");
            _nhInput.Remove(d.Id); _nhFeedback.Remove(d.Id);
            _progMsg[TutorKey] = "Übung angelegt und geöffnet.";
        });
        sp.Children.Add(fq); sp.Children.Add(fa); sp.Children.Add(fe); sp.Children.Add(fh); sp.Children.Add(add);
        var own = Nachhilfe.AktuellesThema(d) is { } th ? Nachhilfe.Eigene(d, th.Id) : [];
        if (own.Count > 0) sp.Children.Add(P($"{own.Count} eigene/KI-Übung(en) in diesem Thema.", Muted, 11.5));
    }
}
