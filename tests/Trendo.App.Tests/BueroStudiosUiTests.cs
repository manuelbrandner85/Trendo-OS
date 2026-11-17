using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Trendo.App;
using Trendo.Core.Buero;
using Trendo.Core.Programs;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0671 Arbeiten &amp; Büro Teil A: Bewerbung &amp; Lebenslauf, Karten &amp; Einladungen, Verträge-Ordner – echte Klicks auf Menüband, Reiter, Felder und Listen.</summary>
public class BueroStudios_Ui
{
    static void Tap(MainWindow w, string name) { var b = w.Find<Button>(name); b.BringIntoView(); Settle(); RealClick(w, b); }
    static void Cmd(MainWindow w, string name) { var b = w.Find<Button>("prog-cmd-" + name); b.BringIntoView(); Settle(); RealClick(w, b); }
    static void WaitFor(Func<bool> done) { for (var i = 0; i < 600 && !done(); i++) { Thread.Sleep(15); Settle(); } }
    static List<string> Texts(Control c) => c.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").Where(t => t.Length > 0).ToList();
    static string Export(MainWindow w, string key, string name, string root)
    {
        var e = MainWindow.Programs[key].Exports.Single(x => x.Name == name);
        return e.Write(w.ProgramActive(key)!, Path.Combine(root, "export-" + key + "-" + name + e.Extension));
    }
    static ProgramDocument Open(MainWindow w, string key, string tpl)
    {
        w.ProgramSetLevel(UserLevel.Profi); w.Show(key); Settle();
        RealClick(w, w.Find<Button>("prog-tpl-" + tpl)); Settle();
        return w.ProgramActive(key)!;
    }
    /// <summary>Getipptes übernehmen: Seite neu zeichnen – das alte Feld übernimmt seinen Text beim Abhängen.</summary>
    static void Type(MainWindow w, string box, string text, string key) { w.Find<TextBox>(box).Text = text; Settle(); w.Show(key); Settle(); }

    [AvaloniaFact(DisplayName = "Arbeiten & Büro A: alle drei Studios haben eine eigene Seite und öffnen ihre Vorlagen")]
    public void Pages()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            foreach (var (studio, key, tpl, probe) in new[] { ("STUDIO-CV-01", MainWindow.CvKey, "beispiel", "cv-preview"), ("STUDIO-CARDS-01", MainWindow.CardKey, "hochzeit", "cards-canvas"), ("STUDIO-CONTRACTS-01", MainWindow.ConKey, "beispiel", "con-list") })
            {
                Assert.Equal(key, MainWindow.StudioPages[studio]);
                var d = Open(w, key, tpl);
                Assert.NotNull(d); Assert.NotNull(w.Find<Control>(probe));
            }
            foreach (var t in MainWindow.Programs[MainWindow.CardKey].Templates) Assert.NotNull(t.Create()["karte"]);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Bewerbung: Prüfen, Feld tippen → Prüfung meldet falsches Datum, Station löschen und Rückgängig, Layout wählen, Exporte PDF/DOCX/ATS")]
    public void Cv_editor()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        MainWindow.CvNowOverride = () => new DateTime(2026, 11, 15);
        try
        {
            const string K = MainWindow.CvKey;
            var d = Open(w, K, "beispiel");
            Cmd(w, "cv-check");
            Assert.StartsWith("✓ Der Lebenslauf ist vollständig", w.ProgramMessage(K));
            Assert.Contains(Texts(w.Find<StackPanel>("cv-findings")), t => t.StartsWith("✓"));

            Type(w, "cv-st-0-von", "Mai 2021", K);
            Assert.Equal("Mai 2021", MainWindow.CvOf(d).Cv.Stationen[0].Von);
            Assert.Contains(Texts(w.Find<StackPanel>("cv-findings")), t => t.Contains("„Mai 2021“"));
            Type(w, "cv-st-0-von", "01.2021", K);

            Tap(w, "cv-st-1-del");
            Assert.Single(MainWindow.CvOf(d).Cv.Stationen);
            Assert.True(d.Undo()); Assert.Equal(2, MainWindow.CvOf(d).Cv.Stationen.Count);

            Cmd(w, "cv-add-station"); Assert.Equal(3, MainWindow.CvOf(d).Cv.Stationen.Count);
            Assert.Contains(Texts(w.Find<StackPanel>("cv-findings")), t => t.Contains("keine Bezeichnung"));
            Assert.True(d.Undo());

            w.Find<ComboBox>("cv-layout").SelectedIndex = 1; Settle();
            Assert.Equal("modern", MainWindow.CvOf(d).Cv.Layout);
            Tap(w, "cv-accent-1"); Assert.Equal("#0F766E", MainWindow.CvOf(d).Cv.Akzent);
            Assert.Contains("Modern", w.Find<TextBlock>("cv-preview-pages").Text);

            var pdf = Export(w, K, "pdf", root); Assert.True(Trendo.Core.Sys.PdfTools.Read(pdf).Pages >= 1);
            var docx = Export(w, K, "docx", root); Assert.Contains("Erika Muster", Trendo.Core.Office.DocxFormat.Read(docx).PlainText());
            Assert.Contains("BERUFSERFAHRUNG", File.ReadAllText(Export(w, K, "ats", root)));
        }
        finally { MainWindow.CvNowOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Bewerbung: Anschreiben – Entwurf ohne KI, Stichwörter der Anzeige, mit KI (Ersatz im Test), an „Briefe schreiben“; Mappe mit Anlage; Übersicht mit Frist")]
    public void Cv_letter_mappe_list()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        MainWindow.CvNowOverride = () => new DateTime(2026, 11, 15);
        try
        {
            const string K = MainWindow.CvKey;
            var d = Open(w, K, "beispiel");
            Tap(w, "cv-tab-anschreiben");
            Assert.NotNull(w.Find<WrapPanel>("cv-keywords"));
            Assert.Contains(Texts(w.Find<WrapPanel>("cv-keywords")), t => t == "✓ DATEV");
            Cmd(w, "cv-letter-draft");
            Assert.Contains("Musterwerke AG", MainWindow.CvOf(d).Anschreiben);
            Assert.Contains(Texts(w.Find<StackPanel>("cv-letter-preview")), t => t == "Bewerbung als Sachbearbeiterin Finanzbuchhaltung (m/w/d)");

            string? asked = null;
            MainWindow.CvAiOverride = p => { asked = p; return Task.FromResult<string?>("Ich bringe fünf Jahre Erfahrung in der Buchhaltung mit.\n\nMit freundlichen Grüßen\nErika Muster"); };
            Tap(w, "cv-ai");
            WaitFor(() => MainWindow.CvOf(d).Anschreiben.Contains("fünf Jahre"));
            Assert.Contains("erfinde keine", asked);
            Assert.Equal("Sehr geehrte Frau Beispiel,\n\nIch bringe fünf Jahre Erfahrung in der Buchhaltung mit.", MainWindow.CvOf(d).Anschreiben);
            var letterPdf = Export(w, K, "anschreiben", root); Assert.Equal(1, Trendo.Core.Sys.PdfTools.Read(letterPdf).Pages);

            // Mappe mit einer Anlage über den Dateidialog
            var att = Path.Combine(root, "zeugnis.pdf"); Trendo.Core.Sys.PdfTools.NewBlank(att);
            w.PickOverride = _ => Task.FromResult<string[]?>([att]);
            Tap(w, "cv-tab-mappe"); Tap(w, "cv-attach-add"); Settle();
            Assert.Equal([att], MainWindow.CvOf(d).Anlagen);
            Assert.Contains(Texts(w.Find<StackPanel>("cv-attachments")), t => t.Contains("zeugnis.pdf"));
            Tap(w, "cv-mappe-go");
            var mappe = w.ProgramMessage(K)[(w.ProgramMessage(K).IndexOf(": ", StringComparison.Ordinal) + 2)..];
            Assert.True(File.Exists(mappe), w.ProgramMessage(K));
            var lp = Trendo.Core.Sys.PdfTools.Read(letterPdf).Pages; var cp = Trendo.Core.Sys.PdfTools.Read(Export(w, K, "pdf", root)).Pages;
            Assert.Equal(1 + lp + cp + 1, Trendo.Core.Sys.PdfTools.Read(mappe).Pages);
            Assert.Empty(Directory.GetDirectories(Path.GetTempPath(), "trendo-mappe-*").Where(x => Directory.GetLastWriteTime(x) > DateTime.Now.AddMinutes(-1)));

            // Übersicht
            Cmd(w, "cv-track");
            var a = MainWindow.CvOf(d);
            Assert.Equal(("Musterwerke AG", "versendet", "2026-11-29"), (a.Bewerbungen[0].Firma, a.Bewerbungen[0].Status, a.Bewerbungen[0].Frist));
            Assert.NotNull(w.Find<StackPanel>("cv-list"));
            w.Find<ComboBox>("cv-b-0-status").SelectedIndex = 2; Settle();
            Assert.Equal("gespraech", MainWindow.CvOf(d).Bewerbungen[0].Status);
            Assert.Contains("\"Gespräch\"", File.ReadAllText(Export(w, K, "csv", root)));

            Cmd(w, "cv-letter-open");
            Assert.Equal("brief", w.CurrentView);
            Assert.Equal("Bewerbung als Sachbearbeiterin Finanzbuchhaltung (m/w/d)", w.Letter.Subject);
            Assert.Equal("Sehr geehrte Frau Beispiel,", w.Letter.Salutation);
        }
        finally { MainWindow.CvNowOverride = null; MainWindow.CvAiOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Karten: Seite wählen, Text anklicken und ändern, neuer Text, Gäste eintragen und Antwort wählen, Format wechseln, Druck-PDF und Serienkarten, CSV")]
    public void Cards()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1500; w.Height = 1000; Directory.CreateDirectory(root);
        try
        {
            const string K = MainWindow.CardKey;
            var d = Open(w, K, "einladung");
            Tap(w, "cards-page-innen-rechts");
            Assert.Equal("innen-rechts", MainWindow.CardPage(d));
            var t1 = w.Find<Border>("cards-canvas-text-1"); t1.BringIntoView(); Settle(); RealClick(w, t1);
            Assert.Equal(1, MainWindow.CardSel(d));
            Assert.Equal("wann: {Datum}\nwo: {Ort}", w.Find<TextBox>("cards-t-text").Text);
            Assert.Contains(Texts(w.Find<Border>("cards-canvas")), t => t.Contains("Gemeindesaal Musterstadt")); // Platzhalter in der Vorschau eingesetzt

            Type(w, "cards-t-size", "16", K);
            Assert.Equal(16, MainWindow.CardOf(d).InnenRechts.Texte[1].Groesse);
            Cmd(w, "cards-add-text");
            Assert.Equal(4, MainWindow.CardOf(d).InnenRechts.Texte.Count); Assert.Equal(3, MainWindow.CardSel(d));
            Type(w, "cards-t-text", "Wir freuen uns auf dich, {Name}!", K);
            Assert.Equal("Wir freuen uns auf dich, {Name}!", MainWindow.CardOf(d).InnenRechts.Texte[3].Text);

            w.Find<TextBox>("cards-guest-quick").Text = "Familie Muster, 4, ja\nOnkel Kurt, nein\nTante Ilse"; Tap(w, "cards-guest-add");
            Assert.Equal("3 Gäste eingetragen.", w.ProgramMessage(K));
            Assert.Contains(Texts(w.Find<WrapPanel>("cards-rsvp")), t => t == "Zusagen: 1 (4 Personen)");
            w.Find<ComboBox>("cards-g-2-status").SelectedIndex = 1; Settle(); // Tante Ilse sagt zu
            Assert.Equal("zugesagt", MainWindow.CardOf(d).Gaeste[2].Status);
            Assert.Contains(Texts(w.Find<WrapPanel>("cards-rsvp")), t => t == "Zusagen: 2 (5 Personen)");

            Cmd(w, "cards-print");
            Assert.Contains("Druck-PDF fertig: 2 Bögen à 236 × 174 mm mit 3 mm Beschnitt", w.ProgramMessage(K));
            Cmd(w, "cards-serie");
            Assert.Contains("Serienkarten für 2 Gäste", w.ProgramMessage(K));
            Assert.Equal(4, Trendo.Core.Sys.PdfTools.Read(Export(w, K, "serie", root)).Pages);
            var csv = File.ReadAllText(Export(w, K, "csv", root));
            Assert.Contains("\"Tante Ilse\";\"\";\"\";1;\"Zugesagt\"", csv);

            w.Find<ComboBox>("cards-format").SelectedIndex = 2; Settle(); // Postkarte
            Assert.Equal("postkarte", MainWindow.CardOf(d).Format);
            Assert.Null(w.GetLogicalDescendants().OfType<Button>().FirstOrDefault(b => b.Name == "cards-page-innen-rechts"));
            Assert.Contains(Texts(w.Find<StackPanel>("cards-findings")), t => t.Contains("keine Innenseiten"));
            Assert.True(d.Undo()); Assert.Equal("a6", MainWindow.CardOf(d).Format);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Verträge: Liste mit Ampel und Kosten, Vertrag wählen und Frist ändern, Erinnerungen als Kalenderdatei, Kündigung an „Briefe schreiben“, als gekündigt markieren")]
    public void Contracts()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root);
        MainWindow.ConNowOverride = () => new DateTime(2026, 11, 15);
        try
        {
            const string K = MainWindow.ConKey;
            var d = Open(w, K, "beispiel");
            Assert.Equal(4, w.Find<StackPanel>("con-list").Children.OfType<Button>().Count());
            Assert.Contains(Texts(w.Find<Border>("con-kpi-month")), t => t == "90,88 €"); // 29,99 + 84/12 + 13,99 + 39,90
            Assert.Contains(Texts(w.Find<StackPanel>("con-costs")), t => t.StartsWith("Fitness & Verein (1)"));

            // Streaming-Abo wählen (unbefristet, zum Monatsende)
            var row = w.Find<StackPanel>("con-list").Children.OfType<Button>().First(b => Texts(b).Contains("Streaming-Abo"));
            row.BringIntoView(); Settle(); RealClick(w, row);
            Assert.Equal("v3", d.Text("sel"));
            Assert.Equal("Spätestens kündigen bis 30.11.2026 (in 15 Tagen) – dann endet er am 30.11.2026", w.Find<TextBlock>("con-stand-text").Text);
            Type(w, "con-f-frist", "1", K); // 1 Tag … Einheit noch „Tage“
            Assert.Equal(1, MainWindow.ConOf(d).Vertraege.Single(v => v.Id == "v3").FristWert);
            Assert.Equal("Spätestens kündigen bis 29.11.2026 (in 14 Tagen) – dann endet er am 30.11.2026", w.Find<TextBlock>("con-stand-text").Text);
            Assert.True(d.Undo());

            Cmd(w, "con-ics");
            var ics = w.ProgramMessage(K); var file = ics[(ics.IndexOf("vorher: ", StringComparison.Ordinal) + 8)..ics.IndexOf(" – Doppelklick", StringComparison.Ordinal)];
            Assert.StartsWith("4 Fristen als Kalenderdatei", ics);
            Assert.Equal(4, Ical.Net.Calendar.Load(File.ReadAllText(file))!.Events.Count);

            Cmd(w, "con-add");
            Assert.Equal(5, MainWindow.ConOf(d).Vertraege.Count); Assert.Equal("Neuer Vertrag", MainWindow.ConSelected(d)!.Name);
            Type(w, "con-f-name", "Stromvertrag", K);
            Assert.Equal("Stromvertrag", MainWindow.ConSelected(d)!.Name);
            Assert.True(d.Undo()); Assert.True(d.Undo()); Assert.Equal(4, MainWindow.ConOf(d).Vertraege.Count);

            // Handyvertrag kündigen
            d.Data["sel"] = "v1"; w.Show(K); Settle();
            Cmd(w, "con-letter");
            Assert.Equal("brief", w.CurrentView);
            Assert.Equal("Kündigung meines Vertrags KD-123456 (Handyvertrag)", w.Letter.Subject);
            Assert.Contains("fristgerecht zum 03.01.2027, hilfsweise zum nächstmöglichen Zeitpunkt", w.Letter.Body);
            Assert.Equal(1, Trendo.Core.Sys.PdfTools.Read(Export(w, K, "kuendigung", root)).Pages);

            w.Show(K); Settle();
            Cmd(w, "con-done");
            Assert.Equal("2027-01-03", MainWindow.ConOf(d).Vertraege.Single(v => v.Id == "v1").GekuendigtZum);
            Assert.Equal("Gekündigt zum 03.01.2027", Trendo.Core.Buero.Vertraege.Stand(MainWindow.ConOf(d).Vertraege.Single(v => v.Id == "v1"), new DateTime(2026, 11, 15)).Text);
            Assert.EndsWith(";grau", File.ReadAllText(Export(w, K, "csv", root)).Split("\r\n").Single(l => l.StartsWith("\"Handyvertrag\"")));
        }
        finally { MainWindow.ConNowOverride = null; w.Close(); }
    }
}
