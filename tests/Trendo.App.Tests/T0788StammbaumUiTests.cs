using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Office;
using Trendo.Core.Programs;
using Trendo.Core.Wissen;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0788 (Etappe 0.9.115 „Wissen &amp; Lernen E“): Studio „Stammbaum“ vom Klick bis zur echten Datei – Personen anlegen, Eltern über den Baum,
/// Prüfung, alle Ausgaben, GEDCOM einlesen (auch per Ziehen, auch ANSEL), Maus im Baum, Rechtsklick, Entf, Rückgängig, Stufe Einfach,
/// KI-Fachaktion „Personen aus Text“ (KI ersetzt) und Echtlauf mit der lokalen KI (nur mit TRENDO_T0788_ECHT=&lt;Belegordner&gt;).</summary>
[Collection("Claude")]
public class T0788Stammbaum_Ui
{
    const string Fam = MainWindow.FamKey;
    static readonly DateTime Today = new(2026, 11, 28);

    const string Foreign = "0 HEAD\r\n1 SOUR FREMDPROGRAMM\r\n1 SUBM @U1@\r\n1 GEDC\r\n2 VERS 5.5.1\r\n2 FORM LINEAGE-LINKED\r\n1 CHAR UTF-8\r\n0 @U1@ SUBM\r\n1 NAME Erika Muster\r\n"
        + "0 @I1@ INDI\r\n1 NAME Jürgen /Größer/\r\n1 SEX M\r\n1 BIRT\r\n2 DATE 12 MAR 1850\r\n2 PLAC Köln\r\n1 DEAT\r\n2 DATE ABT 1920\r\n1 _UID 4F2A11C0E1B44C7A9F0A\r\n1 FAMS @F1@\r\n"
        + "0 @I2@ INDI\r\n1 NAME Anna /Muster/\r\n1 SEX F\r\n1 BIRT\r\n2 DATE BET 1852 AND 1854\r\n1 DEAT\r\n2 DATE 1931\r\n1 FAMS @F1@\r\n"
        + "0 @I3@ INDI\r\n1 NAME Erika /Größer/\r\n1 SEX F\r\n1 BIRT\r\n2 DATE 1 FEB 1880\r\n1 DEAT Y\r\n1 FAMC @F1@\r\n"
        + "0 @F1@ FAM\r\n1 HUSB @I1@\r\n1 WIFE @I2@\r\n1 CHIL @I3@\r\n2 _FREL Natural\r\n1 MARR\r\n2 DATE 5 MAY 1878\r\n0 TRLR\r\n";

    sealed class FixedKi(Func<string, string> reply) : IKiBackend
    {
        public string Name => "Ersatz-KI"; public string BaseUrl => "http://127.0.0.1:9";
        public readonly List<string> Systems = [];
        public Task<bool> IsUpAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<string>> ModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["ersatz"]);
        public Task<string> ChatAsync(string model, IReadOnlyList<KiMessage> messages, CancellationToken ct = default) { Systems.Add(messages[0].Content); return Task.FromResult(reply(messages[^1].Content)); }
    }

    static string Plan(string action, JsonObject args) => new JsonObject { ["steps"] = new JsonArray(new JsonObject { ["action"] = action, ["args"] = args }) }.ToJsonString();
    static async Task Until(Func<bool> ok, int ms = 10000) { for (var i = 0; i < ms / 20 && !ok(); i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); } Assert.True(ok()); }
    static void Tap(MainWindow w, string name) { var b = w.Find<Button>(name); b.BringIntoView(); Settle(); RealClick(w, b); Settle(); }
    static void Type(MainWindow w, string name, string text) { var t = w.Find<TextBox>(name); t.BringIntoView(); t.Focus(); Settle(); t.Text = text; Settle(); w.Find<Button>("prog-undo").Focus(); Settle(); }
    static string Hash(string f) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))).ToLowerInvariant();
    static string Exported(MainWindow w, string export)
    {
        Tap(w, "prog-export-" + export);
        var msg = w.ProgramMessage(Fam);
        Assert.StartsWith("Exportiert: ", msg);
        var f = msg["Exportiert: ".Length..].Trim();
        Assert.True(File.Exists(f), f);
        return f;
    }
    static Point OnCanvas(MainWindow w, string hit)
    {
        var c = w.Find<FamilyTreeCanvas>("fam-canvas"); Settle();
        Assert.True(c.Scene.Hits.TryGetValue(hit, out var r), $"{hit} nicht im Baum: {string.Join(", ", c.Scene.Hits.Keys)}");
        var p = c.TranslatePoint(c.ToScreen(new Point(r.X + r.W / 2, r.Y + r.H / 2)), w)!.Value;
        Assert.Same(c, w.InputHitTest(p)); // die Stelle ist sichtbar und gehört dem Baum
        return p;
    }
    /// <summary>Freie Stelle im Baum (keine Person, kein Platzhalter), die wirklich die Arbeitsfläche trifft.</summary>
    static (Point Window, Point Local) Free(MainWindow w)
    {
        var c = w.Find<FamilyTreeCanvas>("fam-canvas");
        for (var y = 20.0; y < c.Bounds.Height - 20; y += 15)
            for (var x = 20.0; x < c.Bounds.Width - 20; x += 25)
            {
                var local = new Point(x, y); var p = c.TranslatePoint(local, w)!.Value;
                if (c.HitAt(local) is null && ReferenceEquals(w.InputHitTest(p), c)) return (p, local);
            }
        throw new InvalidOperationException("keine freie Stelle im Baum");
    }
    static void Click(MainWindow w, Point p) { w.MouseDown(p, MouseButton.Left); w.MouseUp(p, MouseButton.Left); Settle(); }

    static async Task<string> Ask(MainWindow w, string prompt, int ms = 10000)
    {
        var audits = w.Core.Audit.ReadAll().Count;
        bool NoPlan() => w.Core.Audit.ReadAll().Skip(audits).Any(a => a.Action is "assist.ki.ohneplan" or "assist.ki.fehler" && a.Detail.StartsWith(Fam));
        w.Find<TextBox>("auftrag-prompt").Text = prompt;
        Tap(w, "auftrag-pruefen");
        bool Planned() => w.Core.Audit.ReadAll().Skip(audits).Any(a => a.Action is "assist.plan" or "assist.hinweis" or "assist.rechte" && a.Detail.StartsWith(Fam));
        bool Done() => NoPlan() || Planned() && w.AssistLastKey == Fam && w.AssistLast?.Phase is "vorschau" or "fehler" or "verworfen" or "fragen";
        for (var i = 0; i < ms / 20 && !Done(); i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Delay(20); }
        Assert.True(Done(), $"nach {ms / 1000} s noch Phase „{w.AssistLast?.Phase}“ – {w.AssistLast?.Message} | {string.Join(" | ", w.AssistLog)}");
        Assert.False(NoPlan(), $"kein Plan aus der KI-Antwort: {w.AssistLastKiAnswer}");
        return w.AssistLast!.Value.Phase;
    }

    [AvaloniaFact(DisplayName = "T-0788 Stammbaum (UI): leere Vorlage → erste Person → Vater (Menüband) → Mutter (Klick auf „+ Mutter“ im Baum) → Prüfung zeigt Widerspruch → alle acht Ausgaben als echte Dateien, Lebende ausgeblendet")]
    public void Click_to_files()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.FamTodayOverride = Today;
            w.Width = 1600; w.Height = 1400; w.ProgramSetLevel(UserLevel.Profi);
            w.Show(Fam); Settle(); Tap(w, "prog-tpl-leer");
            var d = w.ProgramActive(Fam)!;
            Assert.NotNull(w.TryFind<Button>("prog-first-step"));
            Tap(w, "prog-first-step");
            Type(w, "fam-f-given", "Erika"); Type(w, "fam-f-surname", "Muster"); Type(w, "fam-f-birth", "15.08.1952"); Type(w, "fam-f-birthplace", "Neustadt");
            var combo = w.Find<ComboBox>("fam-f-sex"); combo.SelectedIndex = 1; Settle();
            var erika = MainWindow.FamData(d).Persons.Single();
            Assert.Equal(("Erika", "Muster", "F", "15 AUG 1952", "Neustadt"), (erika.Given, erika.Surname, erika.Sex, erika.Birth!.Date, erika.Birth.Place));
            // Vater über das Menüband
            Tap(w, "prog-cmd-fam-add-father");
            Type(w, "fam-f-given", "Hans"); Type(w, "fam-f-birth", "2.2.1921"); Type(w, "fam-f-death", "1999");
            var t = MainWindow.FamData(d);
            Assert.Equal("Hans Muster", t.Parents(erika.Id).Father!.Name);
            // Mutter über den Platzhalter im Baum
            w.FamSelect(d, erika.Id); w.Show(Fam); Settle();
            Click(w, OnCanvas(w, "+mutter:" + erika.Id));
            Type(w, "fam-f-given", "Marie"); Type(w, "fam-f-surname", "Sommer"); Type(w, "fam-f-birth", "um 1925"); Type(w, "fam-f-death", "2011");
            t = MainWindow.FamData(d);
            Assert.Equal("Marie Sommer", t.Parents(erika.Id).Mother!.Name); Assert.Equal("ABT 1925", t.Parents(erika.Id).Mother!.Birth!.Date);
            // Prüfung: Tod vor Geburt wird unten sofort genannt, Rückgängig nimmt es zurück
            var hans = t.Parents(erika.Id).Father!;
            w.FamSelect(d, hans.Id); w.Show(Fam); Settle();
            Type(w, "fam-f-death", "1900");
            Assert.Contains("Hans Muster: Geburt (2. Februar 1921) nach dem Tod (1900)", ((TextBlock)w.Find<Button>("fam-issue-0").Content!).Text);
            Tap(w, "prog-undo");
            Assert.Equal("1999", MainWindow.FamData(w.ProgramActive(Fam)!).P(hans.Id)!.Death!.Date);
            Type(w, "fam-p-title", "Familie Muster");
            // Ausgaben
            var ged = Exported(w, "ged"); var gtext = File.ReadAllText(ged);
            Assert.Contains("1 CHAR UTF-8", gtext); Assert.Contains("1 NAME Hans /Muster/", gtext); Assert.Contains("1 NAME Lebende Person /Muster/", gtext);
            Assert.DoesNotContain("Erika", gtext); Assert.DoesNotContain("1952", gtext);
            var back = FamilyKit.FromGedcom(File.ReadAllBytes(ged)); Assert.Equal(3, back.Persons.Count); Assert.Single(back.Families);
            var pdf = Exported(w, "pdf"); using (var pd = PdfSharp.Pdf.IO.PdfReader.Open(pdf, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import)) Assert.True(pd.PageCount >= 1);
            var docx = Exported(w, "docx"); var dx = DocxFormat.Read(docx);
            Assert.Contains(dx.Paras, p => p.Text == "Hans Muster (männlich)"); Assert.Contains(dx.Paras, p => p.Text == "Familie Muster");
            var odt = Exported(w, "odt"); Assert.Contains(OdtFormat.Read(odt).Paras, p => p.Text == "Prüfung");
            var svg = Exported(w, "svg"); var x = System.Xml.Linq.XDocument.Load(svg);
            var texts = x.Descendants().Where(e => e.Name.LocalName == "text").Select(e => e.Value).ToList();
            Assert.Contains("Hans Muster", texts); Assert.Contains("Lebende Person Muster", texts); Assert.DoesNotContain(texts, s => s.StartsWith("+ "));
            var png = Exported(w, "png"); Assert.Equal(0x89, File.ReadAllBytes(png)[0]);
            var tpdf = Exported(w, "baum-pdf"); using (var pd = PdfSharp.Pdf.IO.PdfReader.Open(tpdf, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import)) Assert.Equal(1, pd.PageCount);
            var csv = Exported(w, "csv"); Assert.StartsWith("Kennung;Vorname;Nachname", File.ReadAllText(csv).TrimStart('﻿'));
            // Schutz aus: jetzt steht Erika in der GEDCOM-Datei
            var hide = w.Find<CheckBox>("fam-p-hideliving"); hide.IsChecked = false; Settle();
            Assert.Contains("1 NAME Erika /Muster/", File.ReadAllText(Exported(w, "ged")));
        }
        finally { w.FamTodayOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0788 Stammbaum (UI): Person im Baum anklicken wählt sie, Mausrad zoomt, Ziehen verschiebt, Rechtsklick-Menü, „In die Mitte“, Nachkommen-Ansicht, Entf löscht, Rückgängig")]
    public void Mouse_and_keys()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Width = 1600; w.Height = 1400; w.ProgramSetLevel(UserLevel.Profi);
            w.Show(Fam); Settle(); Tap(w, "prog-tpl-beispiel");
            var d = w.ProgramActive(Fam)!;
            Assert.Equal("I5", w.FamSelected(d)); // Person in der Mitte ist gewählt
            Click(w, OnCanvas(w, "I3"));
            Assert.Equal("I3", w.FamSelected(d)); Assert.Equal("Hans", w.Find<TextBox>("fam-f-given").Text);
            // Mausrad und Ziehen
            var canvas = w.Find<FamilyTreeCanvas>("fam-canvas");
            var z0 = canvas.Current.Zoom;
            var (free, _) = Free(w);
            w.MouseWheel(free, new Vector(0, 1)); Settle();
            Assert.True(w.Find<FamilyTreeCanvas>("fam-canvas").Current.Zoom > z0 * 1.1, "kein Zoom");
            canvas = w.Find<FamilyTreeCanvas>("fam-canvas"); var x0 = canvas.Current.X;
            w.MouseDown(free, MouseButton.Left); for (var i = 1; i <= 4; i++) { w.MouseMove(free + new Point(30 * i, 0)); Settle(); } w.MouseUp(free + new Point(120, 0), MouseButton.Left); Settle();
            Assert.InRange(w.Find<FamilyTreeCanvas>("fam-canvas").Current.X - x0, 100, 140);
            Assert.Equal("I3", w.FamSelected(d)); // Ziehen wählt nichts ab
            Assert.Equal("fam-menu", w.Find<FamilyTreeCanvas>("fam-canvas").ContextMenu!.Name);
            // In die Mitte, Nachkommen
            Tap(w, "prog-cmd-fam-root"); Assert.Equal("I3", MainWindow.FamData(d).Root);
            Tap(w, "prog-cmd-fam-view-desc");
            var sc = w.Find<FamilyTreeCanvas>("fam-canvas").Scene;
            Assert.True(sc.Hits["I5"].Y > sc.Hits["I3"].Y); Assert.Contains(sc.Prims.OfType<TimelineKit.PText>(), p => p.Text == "⚭ Marie Sommer");
            // Entf löscht die gewählte Person, Rückgängig holt sie zurück
            w.FamSelect(d, "I6"); w.Show(Fam); Settle();
            var c2 = w.Find<FamilyTreeCanvas>("fam-canvas"); c2.Focus(); Settle();
            w.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None); Settle();
            Assert.Null(MainWindow.FamData(w.ProgramActive(Fam)!).P("I6"));
            Tap(w, "prog-undo");
            Assert.NotNull(MainWindow.FamData(w.ProgramActive(Fam)!).P("I6"));
            // Orte umbenennen
            Tap(w, "prog-cmd-fam-view-places");
            Type(w, "fam-place-0", "Musterstadt am See");
            Assert.DoesNotContain(FamilyKit.Places(MainWindow.FamData(w.ProgramActive(Fam)!)), p => p.Name == "Beispieldorf");
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0788 Stammbaum (UI): GEDCOM aus einem Fremdprogramm einlesen (Knopf), zweite Datei in ANSEL per Ziehen in neuen Reiter; Export behält unbekannte Angaben")]
    public async Task Import_gedcom()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            w.Width = 1600; w.Height = 1400; w.ProgramSetLevel(UserLevel.Profi);
            Directory.CreateDirectory(root);
            var ged = Path.Combine(root, "fremd.ged"); File.WriteAllText(ged, Foreign, new UTF8Encoding(false));
            w.Show(Fam); Settle(); Tap(w, "prog-tpl-leer");
            var d = w.ProgramActive(Fam)!;
            w.PickOverride = _ => Task.FromResult<string[]?>([ged]);
            Tap(w, "prog-cmd-fam-import"); await Until(() => MainWindow.FamData(d).Persons.Count == 3); // T-0788 G3: liest im Hintergrund
            var t = MainWindow.FamData(d);
            Assert.Equal(["Jürgen Größer", "Anna Muster", "Erika Größer"], t.Persons.Select(p => p.Name));
            Assert.Contains("Zeichensatz: UTF-8", w.ProgramMessage(Fam));
            Assert.StartsWith("Eingelesen", d.UndoLabel); // ein Schritt auf dem leeren Dokument
            w.FamSetOption(d, "hideLiving", "false");
            var out1 = File.ReadAllText(Exported(w, "ged"));
            Assert.Contains("1 _UID 4F2A11C0E1B44C7A9F0A", out1); Assert.Contains("2 _FREL Natural", out1); Assert.Contains("1 NAME Jürgen /Größer/", out1);
            // ANSEL-Datei per Ziehen: Dokument ist nicht leer → neuer Reiter
            var ansel = Path.Combine(root, "ansel.ged");
            File.WriteAllBytes(ansel, Gedcom.EncodeAnsel("0 HEAD\r\n1 CHAR ANSEL\r\n0 @I1@ INDI\r\n1 NAME Zoë /Weiß/\r\n1 BIRT\r\n2 DATE 1901\r\n2 PLAC Mönchengladbach\r\n0 TRLR\r\n"));
            Assert.Equal(DragDropEffects.Copy, BueroAusbau_Ui.Drop(w, "fam-canvas", ansel));
            await Until(() => w.ProgramDocs(Fam).Count == 2);
            Assert.Equal(2, w.ProgramDocs(Fam).Count);
            var d2 = w.ProgramActive(Fam)!; Assert.NotSame(d, d2);
            var z = MainWindow.FamData(d2).Persons.Single();
            Assert.Equal(("Zoë", "Weiß", "Mönchengladbach"), (z.Given, z.Surname, z.Birth!.Place));
            Assert.Contains("ANSEL", w.ProgramMessage(Fam));
        }
        finally { w.PickOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0788 Stammbaum (UI): Stufe Einfach zeigt Personenzeile, Baum und genau eine goldene Hauptaktion („Person“)")]
    public void Simple_level()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Width = 1600; w.Height = 1400; w.ProgramSetLevel(UserLevel.Einfach);
            w.Show(Fam); Settle(); Tap(w, "prog-tpl-beispiel");
            Assert.NotNull(w.TryFind<TextBox>("fam-f-given")); Assert.NotNull(w.TryFind<FamilyTreeCanvas>("fam-canvas"));
            Assert.NotNull(w.TryFind<Button>("prog-cmd-fam-add-person"));
            var gold = w.GetLogicalDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible && MainWindow.IsGold(b) && ((b.Name ?? "").StartsWith("prog-", StringComparison.Ordinal) || (b.Name ?? "").StartsWith("fam-", StringComparison.Ordinal))).Select(b => b.Name).ToList();
            Assert.True(gold.Count == 1, "goldene Knöpfe: " + string.Join(", ", gold));
            Tap(w, "prog-cmd-fam-add-person");
            Assert.Equal(8, MainWindow.FamData(w.ProgramActive(Fam)!).Persons.Count);
        }
        finally { w.ProgramSetLevel(UserLevel.Profi); w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0788 Stammbaum KI: „Personen aus Text“ – Vorschau ändert nichts, „Ja, behalten“ fügt Personen und Eltern als EIN Rückgängig-Schritt ein; unlesbare Daten und Änderung von Quellen werden abgelehnt (KI ersetzt)")]
    public async Task Ki_persons()
    {
        var (w, _, _) = Ui.Open(); MainWindow.AssistStepDelayMs = 0;
        var reply = "";
        var ki = new FixedKi(_ => reply);
        try
        {
            w.KiBackendsOverride = () => [ki];
            w.Width = 1600; w.Height = 1400; w.ProgramSetLevel(UserLevel.Profi);
            w.Show(Fam); Settle(); Tap(w, "prog-tpl-beispiel");
            var d = w.ProgramActive(Fam)!; var before = d.Data.ToJsonString(); var history = d.History.Count;
            reply = Plan("stammbaum.personen", new JsonObject { ["personen"] = "Otto Muster | m | Ostern 1890 | Musterstadt" });
            Assert.Equal("fehler", await Ask(w, "Füge die Personen aus diesem Text ein: Otto Muster wurde Ostern 1890 geboren."));
            Assert.Equal(before, d.Data.ToJsonString());
            reply = Plan("dokument.setzen", new JsonObject { ["pfad"] = "sources", ["wert"] = new JsonArray() });
            Assert.Equal("fehler", await Ask(w, "Lösche alle Quellen."));
            Assert.Equal(before, d.Data.ToJsonString());
            reply = Plan("stammbaum.personen", new JsonObject { ["personen"] = "Otto Muster | m | 3.4.1858 | Musterstadt | 1930 | Musterstadt | |\nKarl Muster | m | 12.03.1890 | Musterstadt | | | Otto Muster | Berta Klein\nEhe: Otto Muster + Berta Klein | 1885 | Musterstadt" });
            Assert.Equal("vorschau", await Ask(w, "Füge die Personen aus diesem Text ein: Karl Muster (geb. 12.03.1890 in Musterstadt) war der Sohn von Otto Muster (1858–1930) und Berta Klein, die 1885 heirateten."));
            Assert.Contains("stammbaum.personen", ki.Systems[^1]);
            Assert.DoesNotContain("fam-add-person", ki.Systems[^1]);
            Assert.Equal(before, d.Data.ToJsonString());
            Tap(w, "assist-apply");
            await Until(() => w.AssistLast?.Phase is "fertig" or "fehler");
            Assert.True(w.AssistLast!.Value.Phase == "fertig", w.AssistLast.Value.Message);
            var t = MainWindow.FamData(d);
            Assert.Equal(("Otto Muster", "Berta Klein"), (t.Parents("I1").Father!.Name, t.Parents("I1").Mother!.Name)); // Karl gab es schon: nur die Eltern kommen dazu
            Assert.Equal("12 MAR 1890", t.P("I1")!.Birth!.Date); // vorhandene Angaben unverändert
            Assert.Equal("1885", t.Families.Single(f => t.P(f.Husband)?.Name == "Otto Muster").Marriage!.Date);
            Assert.Equal(history + 1, d.History.Count); Assert.StartsWith(AssistChain.UndoPrefix, d.UndoLabel);
            Tap(w, "assist-undo");
            Assert.Equal(before, w.ProgramActive(Fam)!.Data.ToJsonString());
        }
        finally { MainWindow.AssistStepDelayMs = 140; w.KiBackendsOverride = null; w.Close(); }
    }

    // ======================= Echtlauf lokale KI =======================

    static string? Beleg() => Environment.GetEnvironmentVariable("TRENDO_T0788_ECHT") is { Length: > 0 } d ? Directory.CreateDirectory(d).FullName : null;

    [AvaloniaFact(DisplayName = "T-0788 ECHT: lokale KI (Ollama) – „Personen aus Text“ im offenen Stammbaum, Prüfung, GEDCOM, Rückgängig (nur mit TRENDO_T0788_ECHT)")]
    public async Task Real_local_ki()
    {
        if (Beleg() is not { } beleg) { Assert.Skip("Echtlauf nur mit TRENDO_T0788_ECHT=<Belegordner> (braucht Ollama mit Modell auf diesem PC)."); return; }
        var (w, _, root) = Ui.Open(); MainWindow.AssistStepDelayMs = 0;
        var log = new StringBuilder();
        void L(string s) => log.AppendLine(s);
        var seen = 0;
        void KiAudit() { var all = w.Core.Audit.ReadAll(); foreach (var a in all.Skip(seen).Where(a => a.Action.StartsWith("ki.", StringComparison.Ordinal) || a.Action.StartsWith("assist.", StringComparison.Ordinal))) L($"  - Protokoll: {a.Action} {a.Detail}"); seen = all.Count; }
        try
        {
            w.KiBackendsOverride = () => [new OllamaBackend()];
            Ui.UseRealModels(w);
            w.Intel.LocalFirstAnswerTimeout = TimeSpan.FromMinutes(8);
            _ = w.InstalledModels; Assert.Equal("ollama-api", w.LastModelScan?.Source);
            if (Environment.GetEnvironmentVariable("TRENDO_ASSIST_MODELL") is { Length: > 0 } own) KiModel.Select(w.Core.DataRoot, own, w.Core.Audit);
            var choice = w.KiModelChoice;
            L("# T-0788 ECHT: KI schreibt Personen in den offenen Stammbaum"); L("");
            L($"Datum: {DateTime.Now:yyyy-MM-dd HH:mm} · KI: Ollama 127.0.0.1:11434 · Modell: {choice.Model} ({choice.Reason})");
            L("Wartezeit der Kette auf die erste KI-Antwort für diesen Lauf: 8 min (Standard 90 s) – der Grafikspeicher ist mit anderen Programmen geteilt.");
            L("Bedienung über die Oberfläche (Vorlage, Auftragszeile „Los“, „Ja, behalten“, „Rückgängig“), Fenster headless. Claude nicht beteiligt. Namen frei erfunden (neutral)."); L("");
            w.Width = 1600; w.Height = 1600; w.ProgramSetLevel(UserLevel.Profi);
            w.Show(Fam); Settle(); Tap(w, "prog-tpl-leer");
            var d = w.ProgramActive(Fam)!; var before = d.Data.ToJsonString(); var history = d.History.Count;
            const string text = "Füge die Personen aus diesem Text ein: Erika Muster wurde am 15.08.1952 in Neustadt geboren. Ihre Eltern waren Hans Muster, geboren 1921 in Musterstadt und gestorben 1999 in Neustadt, und Marie Sommer, geboren um 1925 in Talheim. Hans Muster und Marie Sommer heirateten 1950 in Neustadt.";
            seen = w.Core.Audit.ReadAll().Count;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var phase = await Ask(w, text, 900_000); sw.Stop();
            L($"- Auftrag: {text}");
            L($"- Phase nach {sw.Elapsed.TotalSeconds:F0} s: {phase} – {w.AssistLast!.Value.Message}");
            L($"- Plan: {string.Join(" → ", w.AssistLast.Value.Steps)}");
            L($"- KI-Antwort (roh): `{(w.AssistLastKiAnswer ?? "").Replace("`", "'").ReplaceLineEndings(" ")}`"); KiAudit();
            Assert.Equal("vorschau", phase);
            Assert.Equal(before, d.Data.ToJsonString());
            Tap(w, "assist-apply");
            await Until(() => w.AssistLast?.Phase is "fertig" or "fehler", 60000);
            L($"- Nach „Ja, behalten“: {w.AssistLast!.Value.Phase} {w.AssistLast.Value.Message}");
            Assert.Equal("fertig", w.AssistLast.Value.Phase);
            var t = MainWindow.FamData(d);
            L($"- Personen im Dokument: {string.Join(" | ", t.Persons.Select(p => $"{p.Name} ({p.Sex}) * {p.Birth?.When.Show() ?? "-"} {p.Birth?.Place} † {p.Death?.When.Show() ?? "-"}"))}");
            L($"- Familien: {string.Join(" | ", t.Families.Select(f => $"{t.P(f.Husband)?.Name} + {t.P(f.Wife)?.Name}, Kinder: {string.Join(", ", f.Children.Select(c => t.P(c)?.Name))}, Heirat {f.Marriage?.When.Show()} {f.Marriage?.Place}"))}");
            var issues = FamilyKit.Check(t);
            L($"- Prüfung: {issues.Count(i => i.Error)} Fehler, {issues.Count(i => !i.Error)} Hinweise {string.Join("; ", issues.Select(i => i.Text))}");
            L($"- Rückgängig-Liste: {history} → {d.History.Count}, oben „{d.UndoLabel}“");
            var erika = t.Persons.FirstOrDefault(p => p.Given == "Erika" && p.Surname == "Muster");
            Assert.NotNull(erika); Assert.Equal("15 AUG 1952", erika!.Birth?.Date);
            Assert.Equal("Hans Muster", t.Parents(erika.Id).Father?.Name);
            Assert.Equal("1921", t.Persons.Single(p => p.Name == "Hans Muster").Birth?.Date); // Befund erster Echtlauf: Daten des zuerst als Vater genannten Hans fehlten
            Assert.Equal(history + 1, d.History.Count);
            Directory.CreateDirectory(root);
            var ged = Path.Combine(root, "stammbaum-ki.ged");
            File.WriteAllText(ged, FamilyKit.ToGedcom(MainWindow.FamData(d), hideLiving: false), new UTF8Encoding(false)); // Beleg mit allen Angaben (Dokument bleibt unverändert)
            var to = Path.Combine(beleg, "stammbaum-mit-ki.ged"); File.Copy(ged, to, true);
            L($"- Datei stammbaum-mit-ki.ged: {new FileInfo(to).Length} Bytes, SHA-256 {Hash(to)}");
            Tap(w, "assist-undo");
            L($"- Rückgängig: Dokument wie vorher = {before == w.ProgramActive(Fam)!.Data.ToJsonString()}");
            Assert.Equal(before, w.ProgramActive(Fam)!.Data.ToJsonString());
            // T-0788 Gegenprüfung G10: ehrlich führen – fehlt die Heirat aus dem Text, ist der Lauf nur teilweise gelungen
            var marr = t.Families.FirstOrDefault(f => t.P(f.Husband)?.Name == "Hans Muster")?.Marriage;
            L($"- Heirat im Dokument: {(marr is null ? "fehlt" : marr.When.Show() + " " + marr.Place)}");
            L(""); L(marr?.Date == "1950" && marr.Place == "Neustadt" ? "Ergebnis: bestanden." : "Ergebnis: TEILWEISE – Personen, Eltern und Daten stimmen, die Heirat (1950 in Neustadt) hat die KI nicht geliefert; Trendo ergänzt nichts selbst.");
        }
        catch (Exception e) { L(""); L("FEHLER: " + e.Message); throw; }
        finally
        {
            File.WriteAllText(Path.Combine(beleg, "echtlauf-lokale-ki.md"), log.ToString().Replace(Environment.UserName, "<user>"), new UTF8Encoding(false));
            MainWindow.AssistStepDelayMs = 140; w.KiBackendsOverride = null; w.VramOverride = null; w.InstalledModelsOverride = null; w.PickOverride = null; w.Close();
        }
    }
}
