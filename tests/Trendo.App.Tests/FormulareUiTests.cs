using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Trendo.App;
using Trendo.Core.Programs;
using Trendo.Core.Sys;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>Formulare ausfüllen (STUDIO-FORMS-01, T-0672): echte Klicks und Eingaben auf der Seite, „Meine Angaben“, Unterschrift, Pflichtfelder, Glätten.</summary>
public class Formulare_Ui
{
    const string K = MainWindow.FoKey;

    static void Tap(MainWindow w, string name) { var b = w.Find<Button>(name); b.BringIntoView(); Settle(); b.Press(); Settle(); }
    static void Click(MainWindow w, string name) { var c = V<Control>(w, name); c.BringIntoView(); Settle(); RealClick(w, V<Control>(w, name)); Settle(); }
    static void Type(MainWindow w, string name, string text)
    {
        var t = V<TextBox>(w, name); t.BringIntoView(); Settle();
        t.Focus(); t.Text = text; Settle();
        w.Find<TextBox>("prog-title").Focus(); Settle(); // Fokus weg → Eingabe wird übernommen
    }

    static ProgramDocument Open(MainWindow w, string tpl)
    {
        w.Width = 1700; w.Height = 1150;
        w.ProgramSetLevel(UserLevel.Profi); w.Show(K); Settle();
        RealClick(w, w.Find<Button>("prog-tpl-" + tpl)); Settle();
        return w.ProgramActive(K)!;
    }

    static int Field(MainWindow w, ProgramDocument d, string name) => w.FoFields(d).FindIndex(f => f.Name == name);

    [AvaloniaFact(DisplayName = "Formulare: eigene Seite im Studio; Beispiel-Antrag zeigt Felder auf der Seite und in der Liste, leere Pflichtfelder sind markiert")]
    public void Page()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            Assert.Equal(K, MainWindow.StudioPages["STUDIO-FORMS-01"]);
            var d = Open(w, "beispiel");
            Assert.EndsWith("Beispiel-Antrag.pdf", d.Text("file"));
            Assert.Equal(10, w.FoFields(d).Count);
            Assert.NotNull(w.Find<Canvas>("fo-page"));
            Assert.NotNull(w.Find<TextBox>("fo-onpage-0")); Assert.NotNull(w.Find<TextBox>("fo-field-0"));
            Assert.Contains("Vorname", w.Find<TextBlock>("fo-missing").Text); Assert.Contains("Bestaetigung", w.Find<TextBlock>("fo-missing").Text);
            foreach (var t in new[] { "fill", "text", "datum", "haken", "kreuz", "unterschrift" }) Assert.NotNull(w.Find<Button>("prog-cmd-fo-tool-" + t));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "FO-02/03/04 UI: auf der Seite tippen, ankreuzen, auswählen; Pflichtfeld-Hinweis verschwindet; geglättet speichern – Werte stehen fest im Text der Seite")]
    public void Fill_on_page_and_flatten()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            var d = Open(w, "beispiel");
            var original = File.ReadAllBytes(d.Text("file"));
            Type(w, $"fo-onpage-{Field(w, d, "Vorname")}", "Erika");
            Type(w, $"fo-onpage-{Field(w, d, "Nachname")}", "Muster");
            Assert.Equal("Erika", w.FoFields(d).Count > 0 ? d.Data["values"]!["Vorname"]!.GetValue<string>() : "");
            Click(w, $"fo-field-{Field(w, d, "Bestaetigung")}");
            Assert.Equal("ja", d.Data["values"]!["Bestaetigung"]!.GetValue<string>());
            w.Find<ComboBox>($"fo-field-{Field(w, d, "Zahlweise")}").SelectedIndex = 2; Settle(); // Quartal
            Assert.Equal("Quartal", d.Data["values"]!["Zahlweise"]!.GetValue<string>());
            Assert.Empty(w.FoMissing(d));
            Assert.Contains("Alle Pflichtfelder", w.Find<TextBlock>("fo-missing").Text);
            Click(w, "fo-save-flat-now");
            var saved = d.Text("lastSaved");
            Assert.True(File.Exists(saved)); Assert.EndsWith("-fertig.pdf", saved);
            Assert.Empty(PdfFormular.Fields(saved)); // geglättet: keine Felder mehr
            var text = PdfView.Text(saved, 0);
            Assert.Contains("Erika", text); Assert.Contains("Muster", text);
            Assert.Contains("alle Pflichtfelder ausgefüllt", w.ProgramMessage(K));
            Assert.Equal(original, File.ReadAllBytes(d.Text("file"))); // Original unverändert
            // falsche Auswahl wird abgelehnt
            Assert.Throws<ArgumentException>(() => w.FoSetValue(d, "Familienstand", "unbekannt"));
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "FO-06 UI: „Meine Angaben“ speichern (IBAN geprüft), einsetzen – IBAN nur maskiert und nie im Dokument; beim Speichern steht sie in der PDF")]
    public void Profile_suggest()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            var d = Open(w, "beispiel");
            Type(w, "fo-me-vorname", "Erika"); Type(w, "fo-me-nachname", "Muster"); Type(w, "fo-me-plz", "12345"); Type(w, "fo-me-ort", "Musterstadt");
            Type(w, "fo-me-strasse", "Musterweg"); Type(w, "fo-me-hausnummer", "7"); Type(w, "fo-me-geburtsdatum", "01.02.1980");
            Type(w, "fo-me-iban", "DE89 3704 0044 0532 0130 01"); // Prüfziffer falsch
            Click(w, "fo-me-save");
            Assert.Contains("IBAN", w.ProgramMessage(K)); Assert.Empty(w.FoProfile().Values);
            Type(w, "fo-me-vorname", "Erika"); Type(w, "fo-me-nachname", "Muster"); Type(w, "fo-me-plz", "12345"); Type(w, "fo-me-ort", "Musterstadt");
            Type(w, "fo-me-strasse", "Musterweg"); Type(w, "fo-me-hausnummer", "7"); Type(w, "fo-me-geburtsdatum", "01.02.1980");
            Type(w, "fo-me-iban", "DE89 3704 0044 0532 0130 00");
            Click(w, "fo-me-save");
            Assert.Equal("Musterstadt", w.FoProfile()["ort"]);
            Assert.Contains("••••", w.Find<TextBox>("fo-me-iban").PlaceholderText); Assert.Equal("", w.Find<TextBox>("fo-me-iban").Text);
            Click(w, "fo-suggest-now");
            var v = d.Data["values"]!.AsObject();
            Assert.Equal("Erika", v["Vorname"]!.GetValue<string>()); Assert.Equal("12345 Musterstadt", v["PLZ_Ort"]!.GetValue<string>());
            Assert.Equal("Musterweg 7", v["Strasse"]!.GetValue<string>()); Assert.Equal("@profil:iban", v["IBAN"]!.GetValue<string>());
            Assert.DoesNotContain("0532013000", d.Serialize());
            Assert.Contains(w.GetLogicalTexts(), t => t.StartsWith("Aus „Meine Angaben“: DE89 ••••"));
            Assert.Contains("Felder aus „Meinen Angaben“ eingesetzt", w.ProgramMessage(K));
            Click(w, "fo-save-now");
            var saved = d.Text("lastSaved");
            Assert.Equal("DE89370400440532013000", PdfFormular.Fields(saved).Single(f => f.Name == "IBAN").Value);
            Assert.Contains("noch leere Pflichtfelder: Bestaetigung", w.ProgramMessage(K));
            // Protokoll ohne persönliche Werte
            var log = string.Join("\n", Directory.GetFiles(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(saved)!)!)!, "*.jsonl", SearchOption.AllDirectories).Select(File.ReadAllText));
            Assert.DoesNotContain("0532013000", log); Assert.DoesNotContain("Musterstadt", log);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "FO-05 UI: Formular ohne Felder – Text, Datum und Häkchen per Klick setzen, Unterschrift zeichnen und setzen, speichern")]
    public void Overlays_by_clicks()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            var plain = PdfWerkstatt_Ui.TextPdf(Path.Combine(root, "Scan-Formular.pdf"), "Name: ____________", "Seite 2");
            var d = Open(w, "oeffnen");
            w.PickOverride = _ => Task.FromResult<string[]?>([plain]);
            Click(w, "fo-open-now");
            Assert.Equal(plain, d.Text("file")); Assert.Equal("text", w.FoMode);
            d.Set("placeText", "Erika Muster"); Settle();
            var page = V<Canvas>(w, "fo-page");
            Point At(double x, double y) => page.TranslatePoint(new Point(x * MainWindow.FoScale, y * MainWindow.FoScale), w)!.Value;
            void Hit(double x, double y) { var p = At(x, y); w.MouseDown(p, MouseButton.Left); w.MouseUp(p, MouseButton.Left); Settle(); }
            page.BringIntoView(); Settle();
            Hit(120, 82);
            Tap(w, "prog-cmd-fo-tool-datum"); page = V<Canvas>(w, "fo-page"); Hit(300, 200);
            Tap(w, "prog-cmd-fo-tool-haken"); page = V<Canvas>(w, "fo-page"); Hit(70, 250);
            Assert.Equal(3, d.List("overlays").Count);
            // Unterschrift zeichnen (gleiches Zeichenfeld wie Briefe), dann setzen
            var pad = V<Canvas>(w, "fo-sig-canvas"); pad.BringIntoView(); Settle();
            var a = pad.TranslatePoint(new Point(30, 60), w)!.Value; var b = pad.TranslatePoint(new Point(180, 30), w)!.Value;
            w.MouseDown(a, MouseButton.Left); for (var i = 1; i <= 8; i++) { w.MouseMove(a + (b - a) * (i / 8.0)); Settle(); } w.MouseUp(b, MouseButton.Left); Settle();
            Click(w, "fo-sig-use");
            Assert.Equal("unterschrift", w.FoMode);
            page = V<Canvas>(w, "fo-page"); page.BringIntoView(); Settle(); Hit(200, 400);
            Assert.Equal(4, d.List("overlays").Count); Assert.Equal("bild", d.List("overlays")[3]!["kind"].AsText());
            // eines wieder entfernen
            Click(w, "fo-overlay-del-2"); Assert.Equal(3, d.List("overlays").Count);
            Click(w, "fo-save-now");
            var saved = d.Text("lastSaved");
            var text = PdfView.Text(saved, 0);
            Assert.Contains("Erika Muster", text); Assert.Contains(DateTime.Now.ToString("dd.MM.yyyy"), text);
            Assert.True(new FileInfo(saved).Length > new FileInfo(plain).Length);
            // Text setzen ohne Text → verständlicher Hinweis
            Tap(w, "prog-cmd-fo-tool-text"); d.Set("placeText", ""); Settle(); page = V<Canvas>(w, "fo-page"); page.BringIntoView(); Settle(); Hit(100, 300);
            Assert.Contains("Text zum Setzen", w.ProgramMessage(K));
        }
        finally { w.Close(); }
    }
}
