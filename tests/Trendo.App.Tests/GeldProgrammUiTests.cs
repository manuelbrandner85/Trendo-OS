using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Trendo.Core;
using Trendo.Core.Business;
using Trendo.Core.Tests;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0694 „Geld &amp; Rechnungen“ über die Oberfläche: vom leeren Programm bis zur echten Datei (PDF, ZUGFeRD-PDF, XRechnung, XLSX, DATEV-CSV, Tilgungsplan),
/// Kontoauszug einlesen mit Zahlungsabgleich, Rückgängig/Wiederholen. Nichts ist ersetzt außer dem Dateidialog (PickOverride liefert den Pfad, den sonst der Nutzer wählt).</summary>
public class GeldProgramm_Ui
{
    static void Tab(MainWindow w, string id) { w.Find<Button>("geld-tab-" + id).Press(); Settle(); }
    static void Type(MainWindow w, string name, string text) { w.Find<TextBox>(name).Text = text; Settle(); }
    static void Click(MainWindow w, string name) { w.Find<Button>(name).Press(); Settle(); }

    [AvaloniaFact(DisplayName = "T-0694 UI: leeres Programm → Meine Firma → neue Rechnung mit zwei Steuersätzen → fehlende Pflichtangabe wird genau benannt → festschreiben → PDF, ZUGFeRD-PDF und XRechnung als echte Dateien; Zahlung zuordnen; Rückgängig und Wiederholen")]
    public void Rechnung_von_null_bis_datei()
    {
        var (w, l, _) = Ui.Open();
        try
        {
            w.Show("finanzen"); Settle();
            Assert.True(w.Find<StackPanel>("geld-page-uebersicht").IsVisible);
            Tab(w, "rechnungen");
            Assert.Equal("rechnungen", w.GeldTab);
            Assert.Contains(w.GetLogicalTexts(), t => t.StartsWith("Noch keine Rechnung. Erster Schritt"));
            // Meine Firma (ohne Steuernummer – die Prüfung soll es sagen)
            Type(w, "geld-firma-name", "Erika Muster Webdesign"); Type(w, "geld-firma-strasse", "Musterstraße 1"); Type(w, "geld-firma-ort", "12345 Musterstadt");
            Type(w, "geld-firma-mail", "rechnung@example.org"); Type(w, "geld-firma-iban", "DE89 3704 0044 0532 0130 00"); Type(w, "geld-firma-bank", "Beispielbank");
            Assert.Equal("Erika Muster Webdesign", w.GeldBuch().Firma.Name); Assert.Equal("DE89370400440532013000", w.GeldBuch().Firma.Iban);
            Click(w, "geld-re-neu");
            var id = Assert.Single(w.GeldBuch().Vorgaenge).Id; Assert.Equal(id, w.GeldAuswahl);
            Type(w, "geld-kunde", "Beispiel GmbH\nHauptstraße 5\n54321 Beispielhausen"); Type(w, "geld-kunde-mail", "einkauf@example.com"); Type(w, "geld-referenz", "BEST-17");
            Type(w, "geld-leistung", "November 2026");
            Type(w, "geld-pos-text-0", "Webseite Grundpaket"); Type(w, "geld-pos-menge-0", "1"); Type(w, "geld-pos-preis-0", "1200");
            Click(w, "geld-pos-neu");
            Type(w, "geld-pos-text-1", "Fachbuch"); Type(w, "geld-pos-menge-1", "2"); Type(w, "geld-pos-preis-1", "28,04");
            w.Find<ComboBox>("geld-pos-ust-1").SelectedItem = "7 %"; Settle();
            Assert.Equal("Netto 1.256,08 € · USt 19 %: 228,00 € · USt 7 %: 3,93 € · Gesamt 1.488,01 €", w.Find<TextBlock>("geld-summen").Text);
            Assert.Contains(w.GetLogicalTexts(), t => t.StartsWith("✗ Steuernummer oder USt-IdNr."));
            Assert.Null(w.GeldFestschreiben(id));
            Assert.Contains("Steuernummer oder USt-IdNr. fehlt", w.GeldMeldung); Assert.Contains("Meine Firma", w.GeldMeldung);
            Assert.True(w.GeldBuch().Vorgaenge[0].Entwurf); // keine Nummer verbraucht
            Type(w, "geld-firma-steuernr", "12/345/67890");
            Click(w, "geld-festschreiben");
            var v = w.GeldBuch().Vorgaenge.Single(); Assert.False(v.Entwurf);
            var nr = v.Nummer; Assert.Matches(@"^RE-\d{4}-001$", nr);
            var pdf = Path.Combine(w.BusinessFolder, "Rechnungen", nr + ".pdf"); Assert.True(File.Exists(pdf));
            if (GeldBeispiele.PdfToText() is { } tool)
            {
                var text = GeldBeispiele.ReadPdfText(tool, pdf);
                foreach (var must in new[] { "Erika Muster Webdesign", "Beispiel GmbH", "Hauptstraße 5", "12/345/67890", nr, "November 2026", "1.256,08 €", "228,00 €", "3,93 €", "1.488,01 €" }) Assert.Contains(must, text);
            }
            // ZUGFeRD und XRechnung über die Knöpfe – nach dem Schreiben wieder eingelesen
            Click(w, "geld-zugferd");
            var zf = Path.Combine(w.BusinessFolder, "Rechnungen", nr + "-zugferd.pdf"); Assert.True(File.Exists(zf)); Assert.StartsWith("✓ E-Rechnung (ZUGFeRD)", w.GeldMeldung);
            var back = ERechnung.ReadDetailed(ZugferdPdf.Extract(zf)!); Assert.Equal((nr, 1256.08m, 231.93m, 1488.01m, 2), (back.Number, back.Net, back.Vat, back.Gross, back.Lines.Count));
            Click(w, "geld-xrechnung");
            var xr = Path.Combine(w.BusinessFolder, "Rechnungen", ERechnung.FileName(nr)); Assert.True(File.Exists(xr)); Assert.Equal(1488.01m, ERechnung.Read(File.ReadAllBytes(xr)).Gross);
            Assert.DoesNotContain(l.Opened, o => o.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)); // nichts versendet
            // Zahlung zuordnen: bezahlt, zwei Einnahmen (19 % und 7 %)
            Type(w, "geld-zahlung-betrag", "1488,01"); Click(w, "geld-zahlung");
            Assert.Equal("bezahlt", Rechnungen.Status(w.GeldBuch().Vorgaenge.Single(), DateOnly.FromDateTime(DateTime.Today)).Id);
            Assert.Equal(1488.01m, w.Money.All().Sum(e => e.Amount)); Assert.Equal([7m, 19m], w.Money.All().Select(e => e.VatRate).Order());
            // Rückgängig nimmt die Zahlung zurück (Rechnung wieder offen, Buchungen weg), Wiederholen bringt sie zurück
            Assert.Equal("Zahlung zuordnen", w.GeldUndoLabel);
            Click(w, "geld-undo");
            Assert.Empty(w.Money.All()); Assert.Equal(1488.01m, Rechnungen.Offen(w.GeldBuch().Vorgaenge.Single()));
            Click(w, "geld-redo");
            Assert.Equal(2, w.Money.All().Count); Assert.Equal(0m, Rechnungen.Offen(w.GeldBuch().Vorgaenge.Single()));
            // Strg+Z auf der Arbeitsfläche (kein Textfeld im Fokus)
            w.Find<StackPanel>("geld").Focus(); w.KeyPress(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, null); w.KeyRelease(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, null); Settle();
            Assert.Empty(w.Money.All());
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0694 UI: Angebot → Rechnung, Rechnung aus Beschreibung (auch für Claude), Storno mit eigener Nummer; neue Buchung ist rückgängig machbar")]
    public void Angebot_ki_storno()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show("finanzen"); Settle();
            w.GeldChange("Beispielfirma", () => { var b = Rechnungen.Beispiel(DateOnly.FromDateTime(DateTime.Today)); b.Vorgaenge.Clear(); w.GeldStore.Speichern(b); });
            Tab(w, "rechnungen");
            Type(w, "geld-ki-text", "Art: Angebot\nKunde: Beispiel GmbH\nPosition: Beratung | 6\nPosition: Druck | 100 | Stück | 0,50 | 19 | 10\nLeistung: Dezember 2026");
            Click(w, "geld-ki-anlegen");
            var an = Assert.Single(w.GeldBuch().Vorgaenge); Assert.Equal("angebot", an.Art); Assert.Equal(2, an.Positionen.Count);
            Assert.Equal(("Std.", 85m), (an.Positionen[0].Einheit, an.Positionen[0].Preis)); Assert.Equal(45m, Rechnungen.PosNetto(an.Positionen[1]));
            Assert.Equal("Hauptstraße 5", an.KundeAnschrift.Split('\n')[2]); // Kunde aus dem Stamm
            Click(w, "geld-festschreiben");
            Assert.Matches(@"^AN-\d{4}-001$", w.GeldBuch().Vorgaenge.Single().Nummer);
            Click(w, "geld-weiter");
            var re = w.GeldBuch().Vorgaenge.Single(v => v.Art == "rechnung"); Assert.True(re.Entwurf); Assert.Equal(re.Id, w.GeldAuswahl);
            Click(w, "geld-festschreiben");
            var nr = w.GeldBuch().Vorgaenge.Single(v => v.Art == "rechnung").Nummer;
            Click(w, "geld-storno");
            var st = w.GeldBuch().Vorgaenge.Single(v => v.Art == "storno"); Assert.Matches(@"^SR-\d{4}-001$", st.Nummer); Assert.True(File.Exists(Path.Combine(w.BusinessFolder, "Rechnungen", st.Nummer + ".pdf")));
            Assert.True(w.GeldBuch().Vorgaenge.Single(v => v.Nummer == nr).Storniert);
            // unverständliche Beschreibung: klarer Satz, nichts angelegt
            var before = w.GeldBuch().Vorgaenge.Count;
            Assert.Null(w.GeldEntwurfAusText("Kunde: X\nPosition: nur Text"));
            Assert.Contains("Leistung | Menge", w.GeldMeldung); Assert.Equal(before, w.GeldBuch().Vorgaenge.Count);
            // neue Buchung → Rückgängig
            Tab(w, "uebersicht");
            Type(w, "fin-text", "Papier"); Type(w, "fin-amount", "-20,00"); Click(w, "fin-add");
            Assert.Single(w.Money.All()); Assert.Equal("Neue Buchung", w.GeldUndoLabel);
            Assert.True(w.GeldUndo()); Assert.Empty(w.Money.All());
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0694 UI: Kontoauszug öffnen (CAMT.053) → Vorschau mit Kontrolle der Salden und Vorschlag „Rechnung bezahlt“ → Ja übernimmt Buchungen und markiert die Rechnung; MT940 derselben Buchungen wird als doppelt erkannt; unbekannte CSV fragt nach den Spalten")]
    public void Kontoauszug_import()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            w.Show("finanzen"); Settle();
            // offene Rechnung RE-2026-001 über 2.034,22 € (Beispiel)
            w.GeldChange("Beispiel", () => { var b = Rechnungen.Beispiel(new DateOnly(2026, 11, 1)); Rechnungen.Festschreiben(b, b.Vorgaenge[0], w.GeldKreis); w.GeldStore.Speichern(b); });
            Assert.Equal("RE-2026-001", w.GeldBuch().Vorgaenge[0].Nummer);
            var camt = Path.Combine(root, "auszug-camt053.xml"); File.WriteAllText(camt, GeldBeispiele.Camt);
            w.PickOverride = _ => Task.FromResult<string[]?>([camt]);
            Tab(w, "buchungen"); Click(w, "geld-import-open");
            for (var i = 0; i < 50 && w.GeldAuszug is null; i++) { Thread.Sleep(10); Settle(); }
            Assert.Equal("CAMT.053", w.GeldAuszug!.Format); Assert.True(w.GeldAuszug.Stimmt);
            Assert.Contains("passt zu Anfangs- und Endsaldo", w.GeldMeldung);
            Assert.Equal(5, w.Find<StackPanel>("hb-import-rows").Children.Count);
            var vor = Assert.Single(w.GeldZahlungsvorschlaege); Assert.Equal(("RE-2026-001", "sicher"), (vor.Nummer, vor.Sicherheit));
            Assert.True(w.Find<CheckBox>("geld-vorschlag-0").IsChecked);
            Click(w, "hb-import-yes");
            Assert.Equal(6, w.Money.All().Count); Assert.Equal(1176.72m, w.Money.All().Sum(e => e.Amount)); // 5 Umsätze, die Zahlung zur Rechnung mit 19 % und 7 % anteilig auf zwei Buchungen
            var paid = w.GeldBuch().Vorgaenge[0]; Assert.Equal(0m, Rechnungen.Offen(paid)); Assert.Contains("Kontoauszug", paid.Zahlungen.Single().Hinweis);
            Assert.Equal([19m, 7m], w.Money.All().Where(e => e.Amount > 0).Select(e => e.VatRate).OrderDescending()); // Zahlung je Steuersatz der Rechnung
            // derselbe Auszug als MT940: nur die anders geschnittene Sammelbuchung ist neu
            var mt = Path.Combine(root, "auszug.sta"); File.WriteAllText(mt, GeldBeispiele.Mt940);
            w.GeldImport(mt); Settle();
            Assert.Equal("MT940", w.GeldAuszug!.Format);
            Assert.Contains(w.GetLogicalTexts(), t => t.Contains("– 4 Zeilen, 1 neu"));
            Click(w, "hb-import-no");
            // Spalten unbekannt: Zuordnung abfragen, merken, übernehmen
            var csv = Path.Combine(root, "eigene.csv"); File.WriteAllText(csv, "Wann;Wieviel;Wer;Wofuer\n20.11.2026;-12,50;Papierladen Beispiel;Druckerpapier\n");
            w.GeldImport(csv); Settle();
            Assert.NotNull(w.TryFind<StackPanel>("geld-map"));
            w.Find<ComboBox>("geld-map-datum").SelectedIndex = 1; w.Find<ComboBox>("geld-map-betrag").SelectedIndex = 2; w.Find<ComboBox>("geld-map-zweck").SelectedIndex = 4; w.Find<ComboBox>("geld-map-gegen").SelectedIndex = 3;
            Click(w, "geld-map-ok");
            Assert.Equal(1, w.Find<StackPanel>("hb-import-rows").Children.Count);
            Click(w, "hb-import-yes");
            Assert.Contains(w.Money.All(), e => e.Text == "Papierladen Beispiel – Druckerpapier" && e.Amount == -12.5m);
            // Rückgängig nimmt den ganzen Auszug zurück
            Assert.Equal("Kontoauszug übernehmen", w.GeldUndoLabel); w.GeldUndo();
            Assert.DoesNotContain(w.Money.All(), e => e.Text.Contains("Druckerpapier") && e.Date == new DateOnly(2026, 11, 20)); Assert.Equal(6, w.Money.All().Count);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0694 UI: Auswertung als PDF, XLSX und CSV, DATEV-Buchungsstapel; Kredit-Rechner zeigt Rate und Restschuld 0,00 € und speichert den Tilgungsplan; Einfach blendet DATEV aus")]
    public void Auswertung_datev_kredit()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            var y = DateTime.Today.Year;
            w.Money.Add(new(new DateOnly(y, 1, 5), "Rechnung RE-1", 1190m, "Einnahmen", 19m)); w.Money.Add(new(new DateOnly(y, 1, 9), "Papier", -11.90m, "Büro", 19m));
            w.Show("finanzen"); Settle(); Tab(w, "auswertung");
            Assert.Equal("990,00 €", w.Find<TextBlock>("geld-aw-ueberschuss").Text);
            Click(w, "geld-aw-pdf"); Click(w, "geld-aw-xlsx"); Click(w, "geld-aw-csv");
            foreach (var ext in new[] { "pdf", "xlsx", "csv" }) Assert.True(File.Exists(Path.Combine(w.BusinessFolder, $"Auswertung-{y}.{ext}")), ext);
            Assert.Equal(990m, Finance.ParseAmount(Trendo.Core.Office.SheetFiles.Open(Path.Combine(w.BusinessFolder, $"Auswertung-{y}.xlsx"))["Übersicht"].Raw(3, 2)) - 10m);
            Type(w, "geld-datev-berater", "1001"); Type(w, "geld-datev-mandant", "456"); Click(w, "geld-datev");
            var datev = Path.Combine(w.BusinessFolder, $"EXTF_Buchungsstapel_{y}.csv"); Assert.True(File.Exists(datev));
            Assert.StartsWith("\"EXTF\";700;21;", File.ReadAllText(datev, System.Text.Encoding.Latin1)); Assert.Contains("ungeprüft gegen DATEV-Software", w.GeldMeldung);
            // Kredit
            Tab(w, "kredit");
            Type(w, "geld-kredit-betrag", "10000"); Type(w, "geld-kredit-zins", "5"); Type(w, "geld-kredit-monate", "60");
            Click(w, "geld-kredit-rechnen");
            Assert.Equal("188,71 €", w.Find<TextBlock>("geld-kredit-rate-kpi").Text); Assert.Equal("0,00 €", w.Find<TextBlock>("geld-kredit-rest").Text);
            Assert.Equal("5,12 %", w.Find<TextBlock>("geld-kredit-eff").Text);
            Click(w, "geld-kredit-pdf"); Assert.True(File.Exists(Path.Combine(w.BusinessFolder, $"Tilgungsplan-{DateTime.Today:yyyy-MM-dd}.pdf")));
            Type(w, "geld-kredit-rate", "20"); Click(w, "geld-kredit-rechnen");
            Assert.Contains("deckt nicht einmal die Zinsen", w.GeldMeldung);
            // Einfach: DATEV und Sondertilgung sind ausgeblendet, Kredit und Auswertung bleiben
            w.ProgramSetLevel(Trendo.Core.Programs.UserLevel.Einfach); w.Show("finanzen"); Settle();
            Assert.Null(w.TryFind<Button>("geld-datev")); Assert.Null(w.TryFind<TextBox>("geld-kredit-sonder")); Assert.NotNull(w.TryFind<Button>("geld-kredit-rechnen"));
        }
        finally { w.ProgramSetLevel(Trendo.Core.Programs.UserLevel.Erweitert); w.Close(); }
    }
}
