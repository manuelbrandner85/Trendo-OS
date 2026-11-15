using System.Text;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Trendo.Core.Kreativ;
using Trendo.Core.PromptAction;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Formulare ausfüllen (STUDIO-FORMS-01, T-0672): Prüfungen aus docs/nachbau/formulare/anforderungen.md. Das Test-Formular entsteht im Test selbst.</summary>
public class FormulareTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "trendo-forms-" + Guid.NewGuid().ToString("N")[..8]);
    public FormulareTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }
    string F(string name) => Path.Combine(_dir, name);

    /// <summary>Antrag mit Textfeldern (zwei Pflicht), Ankreuzfeld, Auswahl und Optionsfeld (drei Knöpfe) auf Seite 1, Seite 2 ohne Felder.</summary>
    internal static string Antrag(string dir)
    {
        var blank = PdfWerkstattTests.TextPdf(Path.Combine(dir, "leer.pdf"), "Antrag auf Musterleistung\nVorname          Nachname", "Anlage");
        var form = Path.Combine(dir, "antrag.pdf");
        PdfFormular.AddFields(blank, form,
        [
            new("Vorname", "text", 0, 60, 120, 180, 20, Required: true),
            new("Nachname", "text", 0, 260, 120, 180, 20, Required: true),
            new("PLZ_Ort", "text", 0, 60, 160, 380, 20, Label: "PLZ und Wohnort"),
            new("Geburtsdatum", "text", 0, 60, 200, 120, 20),
            new("IBAN", "text", 0, 60, 240, 260, 20),
            new("Einverstanden", "haken", 0, 60, 280, 14, 14, Required: true),
            new("Familienstand", "auswahl", 0, 60, 320, 160, 20, Options: ["ledig", "verheiratet", "geschieden"]),
            new("Zahlweise", "optionen", 0, 60, 360, 14, 14, Options: ["Monat"]),
            new("Zahlweise", "optionen", 0, 160, 360, 14, 14, Options: ["Quartal"]),
            new("Zahlweise", "optionen", 0, 260, 360, 14, 14, Options: ["Jahr"]),
        ]);
        return form;
    }

    [Fact(DisplayName = "FO-01: Felder lesen – Art, Pflicht, Auswahlmöglichkeiten und Lage auf der Seite")]
    public void Read_fields()
    {
        var f = PdfFormular.Fields(Antrag(_dir));
        Assert.Equal(["Vorname", "Nachname", "PLZ_Ort", "Geburtsdatum", "IBAN", "Einverstanden", "Familienstand", "Zahlweise"], f.Select(x => x.Name));
        Assert.Equal(["Text", "Text", "Text", "Text", "Text", "Ankreuzfeld", "Auswahl", "Optionsfeld"], f.Select(x => x.Kind));
        Assert.True(f[0].Required); Assert.False(f[2].Required); Assert.True(f[5].Required);
        Assert.Equal(["ledig", "verheiratet", "geschieden"], f[6].Options);
        Assert.Equal(["Monat", "Quartal", "Jahr"], f[7].Options);
        Assert.Equal((0, 60.0, 120.0, 180.0, 20.0), (f[0].Page, f[0].X, f[0].YTop, f[0].W, f[0].H));
        Assert.Equal("PLZ und Wohnort", f[2].Label);
        Assert.Empty(PdfFormular.Fields(PdfWerkstattTests.TextPdf(F("ohne.pdf"), "x")));
    }

    [Fact(DisplayName = "FO-02: Ausfüllen (Text, Ankreuzfeld, Auswahl, Optionsfeld) als neue Datei, Original bleibt leer; falsche Auswahl wird gemeldet")]
    public void Fill_all_kinds()
    {
        var form = Antrag(_dir);
        var r = PdfFormular.Fill(form, F("aus.pdf"), new Dictionary<string, string>
        {
            ["Vorname"] = "Erika", ["Nachname"] = "Muster", ["Einverstanden"] = "ja", ["Familienstand"] = "verheiratet", ["Zahlweise"] = "Quartal", ["Gibtsnicht"] = "x",
        });
        Assert.Equal(5, r.Filled); Assert.Equal(["Gibtsnicht"], r.Unknown); Assert.Empty(r.MissingRequired);
        var f = PdfFormular.Fields(F("aus.pdf")).ToDictionary(x => x.Name);
        Assert.Equal("Erika", f["Vorname"].Value); Assert.Equal("ja", f["Einverstanden"].Value);
        Assert.Equal("verheiratet", f["Familienstand"].Value); Assert.Equal("Quartal", f["Zahlweise"].Value);
        // Knopf „Quartal“ zeigt den an-Zustand, die anderen /Off
        using (var d = PdfReader.Open(F("aus.pdf"), PdfDocumentOpenMode.Import))
        {
            var states = d.Pages[0].Annotations.Elements.Select(e => (e as PdfSharp.Pdf.Advanced.PdfReference)?.Value as PdfDictionary).OfType<PdfDictionary>()
                .Where(w => w.Elements.ContainsKey("/Parent")).Select(w => w.Elements.GetName("/AS")).ToList();
            Assert.Equal(["/Off", "/Quartal", "/Off"], states);
        }
        Assert.Equal("", PdfFormular.Fields(form).Single(x => x.Name == "Vorname").Value);
        Assert.Throws<ArgumentException>(() => PdfFormular.Fill(form, F("x.pdf"), new Dictionary<string, string> { ["Zahlweise"] = "Woche" }));
        Assert.Throws<ArgumentException>(() => PdfFormular.Fill(form, F("y.pdf"), new Dictionary<string, string> { ["Familienstand"] = "verwitwet" }));
        Assert.Throws<ArgumentException>(() => PdfFormular.Fill(form, form, new Dictionary<string, string>()));
    }

    [Fact(DisplayName = "FO-03: Pflichtfelder leer – Liste der fehlenden; Ankreuzfeld „nein“ zählt als leer")]
    public void Required_missing()
    {
        var fields = PdfFormular.Fields(Antrag(_dir));
        Assert.Equal(["Vorname", "Nachname", "Einverstanden"], PdfFormular.MissingRequired(fields, new Dictionary<string, string>()));
        Assert.Equal(["Einverstanden"], PdfFormular.MissingRequired(fields, new Dictionary<string, string> { ["Vorname"] = "A", ["Nachname"] = "B", ["Einverstanden"] = "nein" }));
        var r = PdfFormular.Fill(Antrag(_dir), F("teil.pdf"), new Dictionary<string, string> { ["Vorname"] = "A" });
        Assert.Equal(["Nachname", "Einverstanden"], r.MissingRequired);
    }

    [Fact(DisplayName = "FO-04: Glätten beim Export – danach keine Felder mehr, Werte stehen fest in der Seite")]
    public void Flatten()
    {
        var r = PdfFormular.Fill(Antrag(_dir), F("glatt.pdf"), new Dictionary<string, string> { ["Vorname"] = "Erika", ["Nachname"] = "Muster", ["Einverstanden"] = "ja" }, flatten: true);
        Assert.True(r.Flattened);
        Assert.Empty(PdfFormular.Fields(F("glatt.pdf")));
        using var d = PdfReader.Open(F("glatt.pdf"), PdfDocumentOpenMode.Import);
        Assert.True(d.Pages[0].Annotations.Count == 0 || d.Pages[0].Annotations.Elements.Select(e => (e as PdfSharp.Pdf.Advanced.PdfReference)?.Value as PdfDictionary).All(a => a?.Elements.GetName("/Subtype") != "/Widget"));
        var content = string.Concat(d.Pages[0].Contents.Elements.Select(e => (e as PdfSharp.Pdf.Advanced.PdfReference)?.Value as PdfDictionary).OfType<PdfDictionary>().Select(c => Encoding.Latin1.GetString(c.Stream.UnfilteredValue)));
        Assert.Contains("Do", content); // Feld-Aussehen als Form-XObject in der Seite
    }

    [Fact(DisplayName = "FO-05: Ohne Felder: Text, Datum, Häkchen, Kreuz und Unterschrift frei setzen")]
    public void Overlays_and_signature()
    {
        var plain = PdfWerkstattTests.TextPdf(F("frei.pdf"), "Formular ohne Felder");
        // gezeichnete Unterschrift: derselbe Kern wie bei Briefen (LetterSignature)
        var sig = LetterSignature.ToPng([[(10, 40), (40, 10), (70, 45), (110, 15)], [(20, 50), (120, 50)]])!;
        Assert.Equal((0x89, 0x50), (sig[0], sig[1]));
        File.WriteAllBytes(F("sig.png"), sig);
        var before = new FileInfo(plain).Length;
        var r = PdfFormular.Fill(plain, F("frei-aus.pdf"), new Dictionary<string, string>(),
            [new("text", 0, 60, 300, "Erika Muster\nMusterweg 1"), new("datum", 0, 400, 300), new("haken", 0, 60, 360, Size: 12), new("kreuz", 0, 100, 360, Size: 12), new("bild", 0, 60, 420, Image: F("sig.png"), W: 140)]);
        Assert.Equal(5, r.Overlays); Assert.True(new FileInfo(F("frei-aus.pdf")).Length > before);
        Assert.Throws<ArgumentException>(() => PdfFormular.Fill(plain, F("z.pdf"), new Dictionary<string, string>(), [new("bild", 0, 1, 1, Image: F("fehlt.png"))]));
        Assert.Throws<ArgumentException>(() => PdfFormular.Fill(plain, F("z2.pdf"), new Dictionary<string, string>(), [new("text", 4, 1, 1, "x")]));
    }

    [Fact(DisplayName = "FO-06: „Meine Angaben“: Zuordnung nach Feldname, Vorschläge nur für leere Felder, IBAN geprüft, maskiert und nur als Platzhalter")]
    public void Profile_suggestions()
    {
        Assert.Equal(["vorname"], FormProfile.Match("Vorname"));
        Assert.Equal(["nachname"], FormProfile.Match("antrag.person.Name"));
        Assert.Equal(["geburtsort"], FormProfile.Match("Geburtsort"));
        Assert.Equal(["geburtsdatum"], FormProfile.Match("txtGebDatum"));
        Assert.Equal(["plz", "ort"], FormProfile.Match("PLZ_Ort"));
        Assert.Equal(["ort"], FormProfile.Match("Feld17", "Wohnort"));
        Assert.Empty(FormProfile.Match("Sportart"));
        Assert.Equal(["iban"], FormProfile.Match("IBAN"));
        Assert.True(FormProfile.IbanValid("DE89 3704 0044 0532 0130 00")); Assert.False(FormProfile.IbanValid("DE89 3704 0044 0532 0130 01"));
        Assert.Equal("DE89 •••• •••• •••• •• 3000", FormProfile.Mask("DE89370400440532013000"));

        var store = new InMemorySecretStore();
        var p = new FormProfile { ["vorname"] = "Erika", ["nachname"] = "Muster", ["plz"] = "12345", ["ort"] = "Musterstadt", ["geburtsdatum"] = "01.02.1980", ["iban"] = "DE89370400440532013000" };
        p.Save(store);
        var q = FormProfile.Load(store);
        Assert.Equal("Musterstadt", q["ort"]);
        var fields = PdfFormular.Fields(Antrag(_dir));
        var s = q.Suggest(fields, new Dictionary<string, string> { ["Nachname"] = "Beispiel" });
        Assert.Equal("Erika", s["Vorname"]); Assert.False(s.ContainsKey("Nachname")); // schon ausgefüllt
        Assert.Equal("12345 Musterstadt", s["PLZ_Ort"]); Assert.Equal("01.02.1980", s["Geburtsdatum"]);
        Assert.Equal("@profil:iban", s["IBAN"]); // nie die echte IBAN im Dokument
        Assert.DoesNotContain(s.Values, v => v.Contains("0532"));
        Assert.StartsWith("Aus „Meine Angaben“: DE89", q.Display(s["IBAN"]));
        var real = q.Resolve(s);
        Assert.Equal("DE89370400440532013000", real["IBAN"]);
        var r = PdfFormular.Fill(Antrag(_dir), F("profil.pdf"), real);
        Assert.Equal("DE89370400440532013000", PdfFormular.Fields(F("profil.pdf")).Single(x => x.Name == "IBAN").Value);
        // Protokoll enthält keine Werte
        var core = new TrendoCore(Path.Combine(_dir, "daten"), new InMemorySecretStore(), offline: true);
        PdfFormular.Fill(Antrag(_dir), F("profil2.pdf"), real, audit: core.Audit);
        var log = string.Join("\n", Directory.GetFiles(Path.Combine(_dir, "daten"), "*", SearchOption.AllDirectories).Where(x => !x.EndsWith(".pdf")).Select(x => { try { return File.ReadAllText(x); } catch (IOException) { return ""; } }));
        Assert.DoesNotContain("0532013000", log); Assert.DoesNotContain("Musterstadt", log);
        Assert.Equal(4, r.Filled); // Vorname, PLZ_Ort, Geburtsdatum, IBAN (Nachname war schon ausgefüllt)
    }

    [Fact(DisplayName = "FO-07: Formular vorbereiten – Felder anlegen; doppelte Namen und unbekannte Arten werden abgelehnt")]
    public void Prepare_form()
    {
        var plain = PdfWerkstattTests.TextPdf(F("p.pdf"), "x");
        Assert.Equal(1, PdfFormular.AddFields(plain, F("p2.pdf"), [new("Name", "text", 0, 50, 50, 200, 20)]));
        Assert.Throws<ArgumentException>(() => PdfFormular.AddFields(F("p2.pdf"), F("p3.pdf"), [new("Name", "text", 0, 50, 90, 200, 20)]));
        Assert.Throws<ArgumentException>(() => PdfFormular.AddFields(plain, F("p4.pdf"), [new("A", "kreis", 0, 1, 1, 5, 5)]));
        Assert.Equal(2, PdfFormular.AddFields(F("p2.pdf"), F("p5.pdf"), [new("Ort", "text", 0, 50, 90, 200, 20), new("Ok", "haken", 0, 50, 130, 12, 12)]));
        Assert.Equal(["Name", "Ort", "Ok"], PdfFormular.Fields(F("p5.pdf")).Select(f => f.Name));
    }

    [Fact(DisplayName = "STUDIO-FORMS-01 Adapter: Feldnamen werden „Meinen Angaben“ zugeordnet – ohne Werte, Pflichtfeld Felder")]
    public void Adapter()
    {
        var all = PromptActions.CreateDefault(); var ad = all.Single(a => a.StudioId == "STUDIO-FORMS-01");
        Assert.False(ad.CanPerformSideEffect); Assert.Equal(SideEffectClass.None, ad.Effect);
        Assert.NotEmpty(ad.Missing(new Dictionary<string, string>()));
        Assert.Equal(CapabilityLevel.Partial, CapabilityCatalog.Level("STUDIO-FORMS-01", all));
        var root = Path.Combine(_dir, "ad");
        var core = new TrendoCore(Path.Combine(root, "daten"), new InMemorySecretStore(), offline: true);
        var project = core.Projects.Create("probe", "Probe").Id;
        var wf = new StudioWorkflow(core, all);
        var ctx = new StudioExecutionContext(project, Principal.LocalOwner, CancellationToken.None, ResourceBudget.Default);
        var req = new StudioPromptRequest("forms-1", project, "STUDIO-FORMS-01", "Felder: Vorname; Familienname; PLZ, Ort; IBAN; Lieblingsfarbe", [], "de", null, PrivacyMode.Local, new Provenance("test", null, null));
        var done = wf.Los(project, wf.Start(req, ctx).RunId, ctx);
        Assert.Equal(RunStatus.Verified, done.Status);
        var text = File.ReadAllText(core.Projects.ResolveInProject(project, done.Artifacts.Single(a => a.RelativePath.EndsWith("formular-zuordnung.txt")).RelativePath));
        Assert.Contains("✓ Vorname ← Vorname", text); Assert.Contains("✓ Familienname ← Nachname", text); Assert.Contains("✓ PLZ, Ort ← PLZ + Ort", text);
        Assert.Contains("IBAN ← IBAN (wird nur beim Export eingesetzt", text); Assert.Contains("– Lieblingsfarbe: selbst ausfüllen", text);
        Assert.Contains("4 von 5 Feldern", text);
    }
}
