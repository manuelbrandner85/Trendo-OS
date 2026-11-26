using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Programs;
using Trendo.Core.PromptAction;
using Trendo.Core.Wissen;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0779 „Quiz erstellen“ und „Kurse erstellen“: echte Klicks auf Vorlagen, Menüband, Antwort-Knöpfe, Rechtsklick, Ziehen und Ablegen und Tasten im Headless-Fenster,
/// bis zur echten Datei (HTML, PDF, GIFT, SCORM-ZIP, CSV). Die KI ist in den ersten KI-Tests ersetzt (feste Antwort); <see cref="Real_local_ki"/> läuft mit Ollama dieses PCs
/// (nur mit TRENDO_T0779_ECHT=&lt;Belegordner&gt;).</summary>
[Collection("Claude")]
public class T0779QuizKurse_Ui
{
    const string QK = MainWindow.QuizKey, CK = MainWindow.CourseKey;

    static void Pump() { for (var i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs(); }
    static string Repo(string rel) { for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent) if (File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) return Path.Combine(d.FullName, rel); throw new FileNotFoundException(rel); }
    static T V<T>(MainWindow w, string name) where T : Control { Pump(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static T? TryV<T>(MainWindow w, string name) where T : Control { Pump(); return w.GetVisualDescendants().OfType<T>().LastOrDefault(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static void Tap(MainWindow w, string name) { var b = V<Button>(w, name); b.BringIntoView(); Pump(); RealClick(w, V<Button>(w, name)); Pump(); }
    static void Type(MainWindow w, string box, string text, string key) { V<TextBox>(w, box).Text = text; Pump(); w.Show(key); Pump(); w.Show(key); Pump(); }
    static void Key_(MainWindow w, Key k) { w.KeyPress(k, RawInputModifiers.None, PhysicalKey.None, null); Pump(); w.KeyRelease(k, RawInputModifiers.None, PhysicalKey.None, null); Pump(); }
    static string Hash(string f) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))).ToLowerInvariant();
    static ProgramDocument Open(MainWindow w, string key, string tpl, int level = UserLevel.Profi)
    {
        w.Width = 1600; w.Height = 1100; w.ProgramSetLevel(level); w.Show(key); Pump();
        RealClick(w, w.Find<Button>("prog-tpl-" + tpl)); Pump();
        return w.ProgramActive(key)!;
    }
    static string Export(MainWindow w, string key, string name, string root)
    {
        var e = MainWindow.Programs[key].Exports.Single(x => x.Name == name);
        return e.Write(w.ProgramActive(key)!, Path.Combine(root, "export-" + key + "-" + name + e.Extension));
    }
    static int Golds(MainWindow w) => w.ActivePageRoot!.GetVisualDescendants().OfType<Button>().Count(b => b.IsEffectivelyVisible && b.Bounds.Width > 0 && MainWindow.IsGold(b));
    static void MenuClick(MainWindow w, string owner, string item)
    {
        var c = V<Control>(w, owner); var menu = c.ContextMenu!;
        menu.Open(c); Pump();
        IEnumerable<MenuItem> All(ItemsControl ic) => ic.Items.OfType<MenuItem>().SelectMany(m => new[] { m }.Concat(All(m)));
        All(menu).Single(i => i.Name == item).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent)); Pump(); menu.Close(); Pump();
    }

    [AvaloniaFact(DisplayName = "T-0779 Einbindung: beide Studios im Katalog (Wissen & Lernen), Seite, Symbol, Fähigkeit, Profil, Team, Fachwissen, Bildbeschreibung; Vorlagen öffnen; genau eine goldene Hauptaktion")]
    public void Pages()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            foreach (var (id, key, tpl, probe) in new[] { ("STUDIO-QUIZ-01", QK, "beispiel", "quiz-q-0"), ("STUDIO-COURSE-01", CK, "beispiel", "cs-editor") })
            {
                Assert.Equal("Wissen & Lernen", StudioCatalog.For(id)!.Area);
                Assert.Equal(key, MainWindow.StudioPages[id]); Assert.Equal(key, w.PageForStudio(id));
                Assert.NotNull(StudioIcons.For(id)); Assert.NotNull(CapabilityCatalog.Find(id)); Assert.True(StudioProfiles.Has(id));
                Assert.NotNull(StudioTeams.For(id)); Assert.True(StudioKnowledgeBase.For(id)!.Detailed);
                Assert.Contains($"'{id}':", File.ReadAllText(Repo("tools/studio_bilder.py")));
                Open(w, key, tpl);
                Assert.NotNull(TryV<Control>(w, probe));
                Assert.Equal(1, Golds(w));
            }
            foreach (var t in MainWindow.Programs[QK].Templates) Assert.NotNull(t.Create()["quiz"]);
            foreach (var t in MainWindow.Programs[CK].Templates) Assert.Empty(KursKit.Pruefen(Kurs.FromJson(t.Create()["kurs"])).Where(f => f.Fehler && t.Id != "leer"));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0779 Quiz: leere Vorlage → erster Schritt, Frage tippen, Lücke, Zuordnen, richtige Antwort klicken, Rechtsklick duplizieren/löschen + Rückgängig → HTML, PDF (PDFium-Text), GIFT als Datei")]
    public void Quiz_editor_to_files()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        try
        {
            var d = Open(w, QK, "leer", UserLevel.Einfach);
            Assert.NotNull(TryV<Button>(w, "prog-first-step"));
            Tap(w, "prog-first-step");
            Assert.Single(MainWindow.QuizOf(d).Fragen);
            w.ProgramSetLevel(UserLevel.Profi, QK); Pump();
            Type(w, "quiz-title", "Hauptstädte", QK);
            Type(w, "quiz-q-0-text", "Hauptstadt von Österreich?", QK);
            Type(w, "quiz-q-0-a-0", "Graz", QK); Type(w, "quiz-q-0-a-1", "Wien", QK); Type(w, "quiz-q-0-a-2", "Linz", QK); Type(w, "quiz-q-0-a-3", "Salzburg", QK);
            Tap(w, "quiz-q-0-a-1-ok");
            var q = MainWindow.QuizOf(d);
            Assert.Equal("Hauptstädte", q.Titel); Assert.Equal("Wien", q.Fragen[0].Antworten.Single(a => a.Richtig).Text);
            Tap(w, "prog-cmd-quiz-add-luecke");
            Type(w, "quiz-q-1-text", "Die Hauptstadt der Schweiz ist [Bern].", QK);
            Assert.Contains("Bern", V<TextBlock>(w, "quiz-q-1-gaps").Text);
            Tap(w, "prog-cmd-quiz-add-zuordnen");
            Type(w, "quiz-q-2-p-0-l", "Frankreich", QK); Type(w, "quiz-q-2-p-0-r", "Paris", QK);
            Type(w, "quiz-q-2-p-1-l", "Polen", QK); Type(w, "quiz-q-2-p-1-r", "Warschau", QK);
            Type(w, "quiz-q-2-p-2-l", "Ungarn", QK); Type(w, "quiz-q-2-p-2-r", "Budapest", QK);
            Tap(w, "prog-cmd-quiz-add-wahr");
            Type(w, "quiz-q-3-text", "Berlin liegt an der Spree.", QK);
            Tap(w, "quiz-q-3-wahr");
            q = MainWindow.QuizOf(d);
            Assert.Equal(["auswahl", "luecke", "zuordnen", "wahrfalsch"], q.Fragen.Select(f => f.Typ));
            Tap(w, "prog-cmd-quiz-check");
            Assert.StartsWith("✓", w.ProgramMessage(QK));
            // Rechtsklick: duplizieren, löschen, Rückgängig
            MenuClick(w, "quiz-q-0", "quiz-menu-0-dup");
            Assert.Equal(5, MainWindow.QuizOf(d).Fragen.Count);
            MenuClick(w, "quiz-q-1", "quiz-menu-1-del");
            Assert.Equal(4, MainWindow.QuizOf(d).Fragen.Count);
            Assert.True(d.Undo()); Assert.Equal(5, MainWindow.QuizOf(d).Fragen.Count); Assert.True(d.Undo()); Pump(); w.Show(QK); Pump();
            MenuClick(w, "quiz-q-3", "quiz-menu-3-typ-auswahl");
            Assert.Equal("auswahl", MainWindow.QuizOf(d).Fragen[3].Typ); Assert.True(d.Undo());
            // Ausgaben
            var html = Export(w, QK, "html", root); var pdf = Export(w, QK, "pdf", root); var gift = Export(w, QK, "gift", root);
            Assert.Contains("Hauptstadt von Österreich?", File.ReadAllText(html));
 Assert.Contains("Hauptstadt von Österreich?", PdfView.Text(pdf, 0));
            var pages = PdfView.PageCount(pdf); Assert.Contains("Lösungen", PdfView.Text(pdf, pages - 1)); Assert.Contains("Wien", PdfView.Text(pdf, pages - 1));
            var back = QuizGift.Read(File.ReadAllText(gift));
            Assert.Empty(back.Ausgelassen); Assert.Equal(4, back.Quiz.Fragen.Count);
            // Fehler blockiert die Ausgabe mit einem Satz
            Tap(w, "prog-cmd-quiz-add");
            var ni = MainWindow.QuizSel(d); Type(w, $"quiz-q-{ni}-text", "", QK);
            Assert.Throws<InvalidOperationException>(() => Export(w, QK, "html", root));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0779 Quiz: Spielmodus per Klick und Tasten – Antwort wählen (1–4), prüfen (Eingabe), Rückmeldung, Lücke tippen, Zuordnen wählen, Auswertung mit Prozent; Zeitlimit läuft ab")]
    public void Quiz_play()
    {
        var (w, _, _) = Ui.Open();
        var now = new DateTime(2026, 11, 26, 10, 0, 0); MainWindow.QuizNowOverride = () => now;
        try
        {
            var d = Open(w, QK, "beispiel");
            w.QuizChange(d, "Test", q => q.FragenMischen = false);
            Tap(w, "prog-cmd-quiz-play");
            var p = w.QuizPlaying(d)!;
            Assert.Equal(1, Golds(w));
            // Frage 1 Auswahl: richtige Antwort per Taste
            var f0 = p.Spiel.Aktuell!; var order = QuizKit.AntwortReihenfolge(p.Spiel.Quiz, f0, p.Spiel.Seed);
            var right = order.FindIndex(i => f0.Antworten[i].Richtig);
            V<Control>(w, "quiz-play").Focus(); w.ActivePageRoot!.GetVisualDescendants().OfType<StackPanel>().First(s => s.Name == "quiz-play").Focus(); Pump();
            Key_(w, Key.D1 + right); Key_(w, Key.Enter);
            Assert.StartsWith("Richtig!", V<TextBlock>(w, "quiz-play-feedback").Text);
            Tap(w, "quiz-play-next");
            // Frage 2 Mehrfach: alle richtigen anklicken
            var f1 = p.Spiel.Aktuell!; order = QuizKit.AntwortReihenfolge(p.Spiel.Quiz, f1, p.Spiel.Seed);
            for (var k = 0; k < order.Count; k++) if (f1.Antworten[order[k]].Richtig) Tap(w, $"quiz-play-opt-{k}");
            Tap(w, "quiz-play-check"); Assert.StartsWith("Richtig!", V<TextBlock>(w, "quiz-play-feedback").Text); Tap(w, "quiz-play-next");
            // Frage 3 Wahr/Falsch: falsch antworten
            var f2 = p.Spiel.Aktuell!; Tap(w, "quiz-play-opt-" + (f2.Antworten[0].Richtig ? 1 : 0)); Tap(w, "quiz-play-check");
            Assert.Equal("Leider falsch.", V<TextBlock>(w, "quiz-play-feedback").Text); Tap(w, "quiz-play-next");
            // Frage 4 Lücke mit Tippfehler
            V<TextBox>(w, "quiz-play-gap-0").Text = "zugspitz"; Pump(); Tap(w, "quiz-play-check"); Assert.StartsWith("Richtig!", V<TextBlock>(w, "quiz-play-feedback").Text); Tap(w, "quiz-play-next");
            // Frage 5 Zuordnen: zwei von drei richtig
            var f4 = p.Spiel.Aktuell!; var rights = QuizKit.AntwortReihenfolge(p.Spiel.Quiz, f4, p.Spiel.Seed);
            V<ComboBox>(w, "quiz-play-pair-0").SelectedIndex = rights.IndexOf(0) + 1; V<ComboBox>(w, "quiz-play-pair-1").SelectedIndex = rights.IndexOf(1) + 1; Pump();
            Tap(w, "quiz-play-check"); Assert.StartsWith("Teilweise", V<TextBlock>(w, "quiz-play-feedback").Text);
            Tap(w, "quiz-play-next");
            // Auswertung: 1 + 2 + 0 + 1 + 2 = 6 von 8 Punkten
            Assert.Equal("75 %", V<TextBlock>(w, "quiz-play-pct").Text);
            Assert.Contains("6 von 8 Punkten · Bestanden", V<TextBlock>(w, "quiz-play-sum").Text);
            Assert.Contains("75 %", w.ProgramMessage(QK));
            Assert.False(d.Dirty && d.History.Contains("Antwort")); // Spielen ändert das Dokument nicht
            Tap(w, "quiz-play-quit"); Assert.Null(w.QuizPlaying(d)); Assert.NotNull(TryV<Control>(w, "quiz-q-0"));
            // Zeitlimit: Spiel-Vorlage (20 s), Zeit vergeht → Frage zählt 0
            var s = w.ProgramNew(QK, "spiel"); w.Show(QK); Pump();
            Tap(w, "prog-cmd-quiz-play");
            Assert.NotNull(TryV<ProgressBar>(w, "quiz-play-time"));
            // Der DispatcherTimer tickt im Headless-Fenster nicht von selbst – derselbe Tick-Handler wird hier direkt ausgelöst (Ehrlich: Timer selbst nicht headless geprüft)
            Assert.False(MainWindow.QuizPlayTick(w.QuizPlaying(s)!)); now = now.AddSeconds(25); Assert.True(MainWindow.QuizPlayTick(w.QuizPlaying(s)!)); Pump();
            Assert.Equal("Zeit abgelaufen.", V<TextBlock>(w, "quiz-play-feedback").Text);
            Assert.True(w.QuizPlaying(s)!.Spiel.Ergebnisse[0].ZeitUm);
        }
        finally { MainWindow.QuizNowOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0779 Quiz: fremde GIFT-Datei laden (Knopf) und Text für die KI per Ziehen und Ablegen; CSV-Karteikarten werden Auswahlfragen; Quiz → Karteikarten")]
    public void Quiz_import_and_drop()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        try
        {
            var gift = Path.Combine(root, "moodle.txt");
            File.WriteAllText(gift, "// Fragen aus einer Moodle-Fragensammlung\n::Q1:: Welche Farbe hat Gras? {=grün ~blau ~rot}\n\n::Q2:: Die Erde ist rund. {T}\n\n::Q3:: Zahl? {#3}\n");
            var d = Open(w, QK, "leer");
            w.PickOverride = _ => Task.FromResult<string[]?>([gift]);
            Tap(w, "prog-cmd-quiz-import");
            Assert.Equal(2, MainWindow.QuizOf(d).Fragen.Count);
            Assert.Contains("1 ausgelassen", w.ProgramMessage(QK));
            var txt = Path.Combine(root, "vulkane.md"); File.WriteAllText(txt, "Vulkane entstehen dort, wo Magma an die Oberfläche steigt. Der Ätna liegt auf Sizilien.");
            BueroAusbau_Ui.Drop(w, "quiz-list", txt);
            Assert.Contains("Ätna", d.Text("quelle")); Assert.Contains("Text aus „vulkane.md“", w.ProgramMessage(QK));
            var csv = Path.Combine(root, "vokabeln.csv"); File.WriteAllText(csv, "Hund;dog\nKatze;cat\nMaus;mouse\n");
            BueroAusbau_Ui.Drop(w, "quiz-list", csv);
            var q = MainWindow.QuizOf(d); Assert.Equal(5, q.Fragen.Count);
            Assert.Equal("dog", q.Fragen[2].Antworten.Single(a => a.Richtig).Text); Assert.Equal(3, q.Fragen[2].Antworten.Count);
            Tap(w, "prog-cmd-quiz-to-cards");
            Assert.Contains("moodle", w.Learn.Decks);
            Assert.Equal(5, w.Learn.Store.Cards.Count(c => c.Deck == "moodle"));
        }
        finally { w.PickOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0779 Kurs: leere Vorlage → Text, Kapitel, Lektion, Bild und Quiz-Datei per Ziehen, Rechtsklick; Kursansicht mit Person, Quiz spielen, Lektion abschließen; HTML, SCORM, CSV")]
    public void Course_editor_view_files()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        try
        {
            // ein Quiz speichern, das der Kurs einbindet
            var qd = Open(w, QK, "beispiel");
            w.QuizChange(qd, "Test", q => { q.Fragen = [q.Fragen[0], q.Fragen[2]]; q.Titel = "Kurz-Quiz"; q.FragenMischen = false; });
            var quizFile = w.ProgramSave(QK, Path.Combine(root, "Kurz-Quiz.tdoc"));

            var d = Open(w, CK, "leer", UserLevel.Einfach);
            Tap(w, "prog-first-step");
            w.ProgramSetLevel(UserLevel.Profi, CK); Pump();
            Type(w, "cs-b-0-text", "Willkommen im Kurs. Hier lernst du die Grundlagen.", CK);
            Type(w, "cs-lek-title", "Einführung", CK);
            Tap(w, "prog-cmd-cs-add-chapter");
            Type(w, "cs-lek-title", "Abschluss", CK);
            var png = Path.Combine(root, "bild.png"); File.WriteAllBytes(png, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII="));
            BueroAusbau_Ui.Drop(w, "cs-editor", png);
            BueroAusbau_Ui.Drop(w, "cs-editor", quizFile);
            var k = MainWindow.CourseOf(d);
            Assert.Equal(["Einführung", "Abschluss"], KursKit.Lektionen(k).Select(x => x.Lektion.Titel));
            Assert.Equal(["bild", "quiz"], k.Kapitel[1].Lektionen[0].Bloecke.Select(b => b.Art));
            Assert.Equal("Kurz-Quiz", k.Kapitel[1].Lektionen[0].Bloecke[1].Quiz!.Titel);
            // Rechtsklick auf eine Lektion: Text-Baustein, dann Rückgängig
            MenuClick(w, "cs-l-0-0", "cs-l-0-0-menu-text");
            Assert.Equal(2, MainWindow.CourseOf(d).Kapitel[0].Lektionen[0].Bloecke.Count); Assert.True(d.Undo());
            // Lernende(r) und Kursansicht
            Type(w, "cs-person-name", "Erika Muster", CK); V<TextBox>(w, "cs-person-name").Text = "Erika Muster"; Tap(w, "cs-person-add");
            Tap(w, "prog-cmd-cs-view");
            Assert.True(MainWindow.CourseViewing(d)); Assert.Equal(1, Golds(w));
            Tap(w, "cs-l-0-0"); Tap(w, "cs-done");
            Assert.Equal("Abschluss", MainWindow.CourseLesson(d)!.Titel);
            Assert.False(V<Button>(w, "cs-done").IsEnabled); Assert.Contains("braucht mindestens 60 %", V<TextBlock>(w, "cs-missing").Text);
            var bid = MainWindow.CourseOf(d).Kapitel[1].Lektionen[0].Bloecke[1].Id;
            Tap(w, "cs-quiz-start-" + bid);
            var n = "cs-quiz-" + bid;
            for (var i = 0; i < 2; i++)
            {
                var qz = MainWindow.CourseOf(d).Kapitel[1].Lektionen[0].Bloecke[1].Quiz!;
                var f = qz.Fragen[i];
                // richtige Antwort finden: Text der Knöpfe vergleichen
                var opts = w.ActivePageRoot!.GetVisualDescendants().OfType<Button>().Where(b => b.Name?.StartsWith(n + "-opt-") == true && b.IsEffectivelyVisible).ToList();
                var hit = opts.First(b => b.GetLogicalDescendants().OfType<TextBlock>().Any(t => f.Antworten.Where(a => a.Richtig).Any(a => (t.Text ?? "").EndsWith(" " + a.Text))));
                Tap(w, hit.Name!); Tap(w, n + "-check"); Tap(w, n + "-next");
            }
            k = MainWindow.CourseOf(d);
            Assert.Equal(100, k.Lernende[0].Quiz[bid]);
            Assert.True(V<Button>(w, "cs-done").IsEnabled);
            Tap(w, "cs-done");
            Assert.True(KursKit.Fortschritt(MainWindow.CourseOf(d), MainWindow.CourseOf(d).Lernende[0]).Fertig);
            Assert.StartsWith("✓ Kurs abgeschlossen", w.ProgramMessage(CK));
            Assert.Equal("Lektion abgeschlossen", d.UndoLabel);
            // Ausgaben
            var html = Export(w, CK, "html", root); var zip = Export(w, CK, "scorm", root); var csv = Export(w, CK, "csv", root);
            Assert.Contains("Willkommen im Kurs.", File.ReadAllText(html)); Assert.DoesNotContain(png.Replace("\\", "\\\\"), File.ReadAllText(html));
            using (var z = System.IO.Compression.ZipFile.OpenRead(zip)) Assert.NotNull(z.GetEntry("imsmanifest.xml"));
            Assert.Contains("\"Erika Muster\";2;2;100;100;ja;", File.ReadAllText(csv));
            Assert.Equal(0xEF, File.ReadAllBytes(csv)[0]); // BOM für Excel
        }
        finally { w.Close(); }
    }

    // ---------------- KI (ersetzt) ----------------

    sealed class FixedKi(Func<string, string> reply) : IKiBackend
    {
        public string Name => "Ersatz-KI"; public string BaseUrl => "http://127.0.0.1:9";
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["ersatz"]);
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default) => Task.FromResult(reply(messages[^1].Content));
    }
    static string Plan(string action, JsonObject args) => new JsonObject { ["steps"] = new JsonArray(new JsonObject { ["action"] = action, ["args"] = args }) }.ToJsonString();
    static async Task Until(Func<bool> ok, int ms = 10000) { for (var i = 0; i < ms / 20 && !ok(); i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } Assert.True(ok()); }
    static async Task<string> Ask(MainWindow w, string key, string prompt, int ms = 10000)
    {
        var audits = w.Core.Audit.ReadAll().Count;
        bool NoPlan() => w.Core.Audit.ReadAll().Skip(audits).Any(a => a.Action is "assist.ki.ohneplan" or "assist.ki.fehler" && a.Detail.StartsWith(key));
        w.Find<TextBox>("auftrag-prompt").Text = prompt;
        Tap(w, "auftrag-pruefen");
        await Until(() => NoPlan() || w.AssistLastKey == key && w.AssistLast?.Phase is "vorschau" or "fehler" or "verworfen", ms);
        Assert.False(NoPlan(), $"{key}: kein Plan aus der KI-Antwort: {w.AssistLastKiAnswer}");
        return w.AssistLast!.Value.Phase;
    }

    [AvaloniaFact(DisplayName = "T-0779 KI Quiz: „Mach Fragen aus dem Text“ → Vorschau zeigt die Fragen, Original unverändert → „Ja, behalten“ → EIN Rückgängig-Schritt „KI: …“, Rückgängig nimmt alle zurück (KI ersetzt)")]
    public async Task Ki_questions_into_open_quiz()
    {
        var (w, _, _) = Ui.Open(); MainWindow.AssistStepDelayMs = 0;
        try
        {
            const string fragen = "? Wo liegt der Ätna?\n+ Sizilien\n- Sardinien\n- Korsika\n\nL: Vulkane entstehen, wo [Magma] an die Oberfläche steigt.\n\nW: Der Ätna ist ein Vulkan.\n+ Wahr";
            w.KiBackendsOverride = () => [new FixedKi(_ => Plan("quiz.fragen", new JsonObject { ["fragen"] = fragen }))];
            var d = Open(w, QK, "leer");
            w.QuizSetSource(d, "Vulkane entstehen dort, wo Magma an die Oberfläche steigt. Der Ätna auf Sizilien ist ein Vulkan.");
            var before = d.Data.ToJsonString(); var hist = d.History.Count;
            Assert.Equal("vorschau", await Ask(w, QK, "Mach aus dem Text drei Fragen für ein Quiz zum Thema Vulkane"));
            Assert.Equal(before, d.Data.ToJsonString()); // Vorschau ändert das Original nicht
            Assert.DoesNotContain("neue Datei", w.AssistLast!.Value.Message); // T-0779: Fachaktion ohne eigene Datei kündigt keine an
            Assert.All(w.AssistLast.Value.Steps, x => Assert.True(x.Length < 300 && !x.Contains('\n'), "Plan-Zeile zu lang: " + x)); // lange Fragen schoben „Ja, behalten“ aus dem Bild (Echtlauf)
            Tap(w, "assist-apply");
            await Until(() => w.AssistLast?.Phase is "fertig" or "fehler");
            var q = MainWindow.QuizOf(w.ProgramActive(QK)!);
            Assert.Equal(["auswahl", "luecke", "wahrfalsch"], q.Fragen.Select(f => f.Typ));
            Assert.Equal(hist + 1, d.History.Count); Assert.StartsWith(AssistChain.UndoPrefix, d.UndoLabel);
            Tap(w, "assist-undo");
            Assert.Equal(before, w.ProgramActive(QK)!.Data.ToJsonString());
            // unlesbare Antwort: Fehler schon in der Vorschau, nichts geändert
            w.KiBackendsOverride = () => [new FixedKi(_ => Plan("quiz.fragen", new JsonObject { ["fragen"] = "Hier sind Fragen: keine" }))];
            var phase = await Ask(w, QK, "Mach aus dem Text drei Fragen für ein Quiz zum Thema Vulkane bitte");
            Assert.NotEqual("vorschau", phase); Assert.Equal(before, w.ProgramActive(QK)!.Data.ToJsonString());
        }
        finally { MainWindow.AssistStepDelayMs = 140; w.KiBackendsOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0779 KI Kurs: „Schreib eine Lektion“ → neue Lektion mit Überschrift, Liste, Merksatz im offenen Kurs, EIN Rückgängig-Schritt (KI ersetzt)")]
    public async Task Ki_lesson_into_open_course()
    {
        var (w, _, _) = Ui.Open(); MainWindow.AssistStepDelayMs = 0;
        try
        {
            w.KiBackendsOverride = () => [new FixedKi(_ => Plan("kurs.lektion", new JsonObject { ["titel"] = "Mülltrennung", ["inhalt"] = "Was gehört wohin?\n\n## Gelbe Tonne\n- Joghurtbecher\n- Konservendosen\n! Verpackungen nicht ausspülen müssen, nur leer." }))];
            var d = Open(w, CK, "beispiel");
            var n = KursKit.Lektionen(MainWindow.CourseOf(d)).Count; var before = d.Data.ToJsonString();
            Assert.Equal("vorschau", await Ask(w, CK, "Schreib eine kurze Lektion über Mülltrennung im Haushalt für Anfänger"));
            Tap(w, "assist-apply");
            await Until(() => w.AssistLast?.Phase is "fertig" or "fehler");
            var k = MainWindow.CourseOf(w.ProgramActive(CK)!);
            var l = KursKit.Lektionen(k).Single(x => x.Lektion.Titel == "Mülltrennung").Lektion;
            Assert.Equal(["text", "ueberschrift", "liste", "hinweis"], l.Bloecke.Select(b => b.Art));
            Assert.Equal(n + 1, KursKit.Lektionen(k).Count); Assert.StartsWith(AssistChain.UndoPrefix, d.UndoLabel);
            Tap(w, "assist-undo"); Assert.Equal(before, w.ProgramActive(CK)!.Data.ToJsonString());
        }
        finally { MainWindow.AssistStepDelayMs = 140; w.KiBackendsOverride = null; w.Close(); }
    }

    // ---------------- Echtlauf lokale KI ----------------

    static string? Beleg() => Environment.GetEnvironmentVariable("TRENDO_T0779_ECHT") is { Length: > 0 } d ? Directory.CreateDirectory(d).FullName : null;

    [AvaloniaFact(DisplayName = "T-0779 ECHT: lokale KI (Ollama) – Fragen aus einem Text ins offene Quiz und eine Lektion in den offenen Kurs, Rückgängig (nur mit TRENDO_T0779_ECHT)")]
    public async Task Real_local_ki()
    {
        if (Beleg() is not { } beleg) { Assert.Skip("Echtlauf nur mit TRENDO_T0779_ECHT=<Belegordner> (braucht Ollama mit Modell auf diesem PC)."); return; }
        var (w, _, root) = Ui.Open(); MainWindow.AssistStepDelayMs = 0;
        var log = new StringBuilder(); void L(string s) => log.AppendLine(s);
        var seen = 0;
        void KiAudit() { var all = w.Core.Audit.ReadAll(); foreach (var a in all.Skip(seen).Where(a => a.Action.StartsWith("ki.", StringComparison.Ordinal) || a.Action.StartsWith("assist.", StringComparison.Ordinal))) L($"  - Protokoll: {a.Action} {a.Detail}"); seen = all.Count; }
        try
        {
            w.KiBackendsOverride = () => [new OllamaBackend()];
            Ui.UseRealModels(w);
            _ = w.InstalledModels; Assert.Equal("ollama-api", w.LastModelScan?.Source);
            if (Environment.GetEnvironmentVariable("TRENDO_ASSIST_MODELL") is { Length: > 0 } own) KiModel.Select(w.Core.DataRoot, own, w.Core.Audit);
            var choice = w.KiModelChoice;
            L("# T-0779 ECHT: KI schreibt ins offene Dokument (Quiz erstellen, Kurse erstellen)"); L("");
            L($"Datum: {DateTime.Now:yyyy-MM-dd HH:mm} · KI: Ollama 127.0.0.1:11434 · Modell: {choice.Model} ({choice.Reason})");
            L("Bedienung über die Oberfläche (Vorlage, Text für die KI, Auftragszeile „Los“, „Ja, behalten“, „Rückgängig“), Fenster headless. Claude nicht beteiligt."); L("");

            // Quiz
            var d = Open(w, QK, "leer"); seen = w.Core.Audit.ReadAll().Count;
            const string text = "Die Photosynthese findet in den Chloroplasten der Pflanzenzellen statt. Dabei nutzen Pflanzen Lichtenergie, um aus Kohlenstoffdioxid und Wasser Traubenzucker herzustellen. "
                              + "Als Nebenprodukt entsteht Sauerstoff, den die Pflanze an die Luft abgibt. Der grüne Farbstoff Chlorophyll fängt das Licht ein. Nachts findet keine Photosynthese statt, weil das Licht fehlt.";
            V<TextBox>(w, "quiz-source-text").Text = text; Pump(); w.Show(QK); Pump();
            Assert.Equal(text, d.Text("quelle"));
            var before = d.Data.ToJsonString(); var hist = d.History.Count;
            const string p1 = "Mach aus dem Text 4 Quizfragen: zwei Auswahlfragen, einen Lückentext und eine Wahr/Falsch-Aussage.";
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var phase = await Ask(w, QK, p1, 600_000); sw.Stop();
            L("## Quiz erstellen (quiz-programm)"); L("");
            L($"- Text für die KI: {text.Length} Zeichen (Photosynthese)"); L($"- Auftrag: {p1}");
            L($"- Plan ({sw.Elapsed.TotalSeconds:F0} s, Phase {phase}): {string.Join(" → ", w.AssistLast!.Value.Steps)}");
            L($"- KI-Antwort (roh): `{(w.AssistLastKiAnswer ?? "").Replace("`", "'").ReplaceLineEndings(" ")}`"); KiAudit();
            Assert.True(phase == "vorschau", w.AssistLast.Value.Message + " | " + string.Join(" | ", w.AssistLog));
            Assert.Equal(before, d.Data.ToJsonString());
            Tap(w, "assist-apply");
            await Until(() => w.AssistLast?.Phase is "fertig" or "fehler", 60000);
            Assert.True(w.AssistLast!.Value.Phase == "fertig", w.AssistLast.Value.Message);
            var q = MainWindow.QuizOf(w.ProgramActive(QK)!);
            Assert.True(q.Fragen.Count >= 2, "zu wenige Fragen"); Assert.Equal(hist + 1, d.History.Count); Assert.StartsWith(AssistChain.UndoPrefix, d.UndoLabel);
            L($"- Nach „Ja, behalten“: {q.Fragen.Count} Fragen im Quiz ({string.Join(", ", q.Fragen.Select(f => QuizKit.TypName(f.Typ)))}), Prüfung: {QuizKit.Pruefen(q).Count(x => x.Fehler)} Fehler");
            foreach (var f in q.Fragen) L($"  - {QuizKit.TypName(f.Typ)}: {f.Text} → Lösung: {QuizKit.Loesung(f)}");
            L($"- Rückgängig-Liste: {hist} → {d.History.Count} Schritte, oben „{d.UndoLabel}“");
            var tq = Path.Combine(beleg, "quiz-aus-ki.tdoc"); w.ProgramSave(QK, Path.Combine(root, "quiz-aus-ki.tdoc")); File.Copy(Path.Combine(root, "quiz-aus-ki.tdoc"), tq, true);
            if (QuizKit.Pruefen(q).All(x => !x.Fehler)) { var h = Path.Combine(beleg, "quiz-aus-ki.html"); QuizHtml.Write(q, h); L($"- HTML-Quiz: quiz-aus-ki.html, SHA-256 {Hash(h)}"); }
            L($"- Dokument: quiz-aus-ki.tdoc, SHA-256 {Hash(tq)}");
            Tap(w, "assist-undo");
            L($"- Rückgängig: Quiz wieder leer = {MainWindow.QuizOf(w.ProgramActive(QK)!).Fragen.Count == 0}");
            Assert.Empty(MainWindow.QuizOf(w.ProgramActive(QK)!).Fragen);
            L("");

            // Kurs
            var c = Open(w, CK, "beispiel"); var cBefore = c.Data.ToJsonString(); var cHist = c.History.Count; var n = KursKit.Lektionen(MainWindow.CourseOf(c)).Count;
            const string p2 = "Schreib eine neue Lektion mit dem Titel „Blickkontakt“: drei kurze Absätze, eine Liste mit drei Tipps und ein Merksatz.";
            sw.Restart();
            phase = await Ask(w, CK, p2, 600_000); sw.Stop();
            L("## Kurse erstellen (kurse-programm)"); L("");
            L($"- Auftrag: {p2}"); L($"- Plan ({sw.Elapsed.TotalSeconds:F0} s, Phase {phase}): {string.Join(" → ", w.AssistLast!.Value.Steps)}");
            L($"- KI-Antwort (roh): `{(w.AssistLastKiAnswer ?? "").Replace("`", "'").ReplaceLineEndings(" ")}`"); KiAudit();
            Assert.True(phase == "vorschau", w.AssistLast.Value.Message + " | " + string.Join(" | ", w.AssistLog));
            Tap(w, "assist-apply");
            await Until(() => w.AssistLast?.Phase is "fertig" or "fehler", 60000);
            Assert.True(w.AssistLast!.Value.Phase == "fertig", w.AssistLast.Value.Message);
            var k = MainWindow.CourseOf(w.ProgramActive(CK)!);
            Assert.Equal(n + 1, KursKit.Lektionen(k).Count); Assert.Equal(cHist + 1, c.History.Count);
            var neu = KursKit.Lektionen(k).Select(x => x.Lektion).Last(l => !KursKit.Lektionen(Kurs.FromJson(JsonNode.Parse(cBefore)!["kurs"])).Any(o => o.Lektion.Id == l.Id));
            L($"- Nach „Ja, behalten“: neue Lektion „{neu.Titel}“ mit {neu.Bloecke.Count} Bausteinen ({string.Join(", ", neu.Bloecke.Select(b => KursKit.ArtName(b.Art)))})");
            foreach (var b in neu.Bloecke) L($"  - {KursKit.ArtName(b.Art)}: {b.Text.ReplaceLineEndings(" / ")}");
            var kh = Path.Combine(beleg, "kurs-aus-ki.html"); KursHtml.Write(k, kh); L($"- Kurs-Webseite: kurs-aus-ki.html, SHA-256 {Hash(kh)}");
            Tap(w, "assist-undo");
            L($"- Rückgängig: Kurs wieder wie vorher = {cBefore == w.ProgramActive(CK)!.Data.ToJsonString()}");
            Assert.Equal(cBefore, w.ProgramActive(CK)!.Data.ToJsonString());
            L(""); L("Ergebnis: beide Programme bestanden.");
        }
        catch (Exception e) { L(""); L("FEHLER: " + e.Message); throw; }
        finally
        {
            File.WriteAllText(Path.Combine(beleg, "echtlauf-lokale-ki.md"), log.ToString().Replace(Environment.UserName, "<user>"), new UTF8Encoding(false));
            MainWindow.AssistStepDelayMs = 140; w.KiBackendsOverride = null; w.VramOverride = null; w.InstalledModelsOverride = null; w.Close();
        }
    }
}
