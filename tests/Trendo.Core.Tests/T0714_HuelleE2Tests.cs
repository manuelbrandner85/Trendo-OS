using System.Text.Json.Nodes;
using Trendo.Core;
using Trendo.Core.Cad;
using Trendo.Core.Office;
using Trendo.Core.Programs;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0714 (B6 „Programm-Hülle für alle“, Etappe E2): eine Notsicherung für Text, Tabelle, Präsentation, CAD und 3D über <see cref="ProgramLink"/>
/// (Stand = die Datei im eigenen Format), Versionsfeld in .tcad und Szene-JSON mit Migrationsweg, atomares Schreiben der Büro-Speicherorte, echte Abbruchtests.</summary>
public class T0714_HuelleE2Tests(ITestOutputHelper output)
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0714-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "T-0714 ProgramLink: Stand als Datei (Base64) hin und zurück, Track legt keinen Rückgängig-Schritt an, kaputte Sicherung wird verständlich abgelehnt")]
    public void File_state_and_track()
    {
        var dir = Tmp();
        try
        {
            var doc = new TextDoc { Blocks = [new Para("Angebot für Erika Muster"), new Para("Zweiter Absatz")] };
            var st = ProgramLink.FileStateVia("text.docx", f => TextFiles.Save(doc, f), Path.Combine(dir, ".arbeit"));
            Assert.Empty(Directory.GetFiles(Path.Combine(dir, ".arbeit"))); // Zwischendatei ist weg
            var l = new ProgramLink("LINK-TEXT", "text");
            l.Track(st, "Angebot");
            Assert.True(l.Dirty); Assert.False(l.CanUndo); // nur Notsicherung, kein Rückgängig-Schritt
            var f = l.Autosave(Path.Combine(dir, "sicherung"))!;
            var b = new ProgramLink("LINK-TEXT", "text");
            var (name, bytes) = ProgramLink.FileOf(b.Restore(Assert.Single(b.Pending(Path.Combine(dir, "sicherung"))).File, Path.Combine(dir, "sicherung")));
            Assert.Equal("text.docx", name);
            var back = Path.Combine(dir, "zurueck.docx"); File.WriteAllBytes(back, bytes);
            Assert.Equal(["Angebot für Erika Muster", "Zweiter Absatz"], TextFiles.Open(back).Paras.Select(p => p.Text));
            Assert.Equal("Angebot", ProgramDocument.Load(f).Title);
            Assert.Throws<InvalidDataException>(() => ProgramLink.FileOf(new JsonObject { ["datei"] = "x.docx", ["base64"] = "kein base64!" }));
            Assert.Throws<InvalidDataException>(() => ProgramLink.FileOf(null));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact(DisplayName = "T-0714 ProgramLink: Zurückholen einer Sicherung löscht die Sicherung des offenen, ungespeicherten Stands nicht mehr – sie wird danach angeboten")]
    public void Restore_keeps_unsaved_backup()
    {
        var dir = Tmp();
        try
        {
            var old = new ProgramLink("LINK-TEXT", "text"); old.Track(new JsonObject { ["datei"] = "text.docx", ["base64"] = "YWx0" }, "Alter Stand"); old.Autosave(dir);
            var a = new ProgramLink("LINK-TEXT", "text"); a.Track(new JsonObject { ["datei"] = "text.docx", ["base64"] = "bmV1" }, "Offener Stand");
            var mine = a.Autosave(dir)!;
            a.Restore(Assert.Single(a.Pending(dir)).File, dir);
            Assert.True(File.Exists(mine)); // vorher: gelöscht – der offene Stand war weg
            Assert.Equal("Offener Stand", Assert.Single(a.Pending(dir)).Title);
            // sauber (gespeichert) – dann entfällt die eigene Sicherung wie bisher
            var c = new ProgramLink("LINK-CAD", "cad"); c.Track(new JsonObject { ["datei"] = "cad.tcad", ["base64"] = "e30=" }); var cf = c.Autosave(dir)!;
            var d = new ProgramLink("LINK-CAD", "cad"); d.Track(new JsonObject { ["datei"] = "cad.tcad", ["base64"] = "e30=" }); var df = d.Autosave(dir)!; d.Saved(dir);
            Assert.False(File.Exists(df));
            d.Restore(cf, dir); Assert.True(d.Dirty);
        }
        finally { Directory.Delete(dir, true); }
    }

    // Aufbau wie von CadDoc.ToJson vor T-0714 geschrieben (ohne „Version“; vor T-0691 auch ohne Drawing/References)
    const string OldTcad = "{\n  \"Params\": { \"Breite\": 40 },\n  \"Bodies\": [ { \"Name\": \"Teil 1\", \"Color\": \"#2F86F6\", \"Features\": [ { \"Op\": \"Quader\", \"Name\": \"Quader\", \"P\": { \"breite\": \"Breite\", \"tiefe\": \"30\", \"hoehe\": \"10\", \"x\": \"0\", \"y\": \"0\", \"z\": \"0\" }, \"Sketch\": null, \"Off\": false } ], \"X\": \"0\", \"Y\": \"0\", \"Z\": \"0\", \"Hidden\": false } ],\n  \"Segments\": 48\n}";

    [Fact(DisplayName = "T-0714 CAD: .tcad hat ein Versionsfeld; alte Dateien ohne Angabe bleiben lesbar (Fassung 1), eine neuere Fassung wird verständlich abgelehnt")]
    public void Cad_version()
    {
        var dir = Tmp();
        try
        {
            var f = Path.Combine(dir, "alt.tcad"); File.WriteAllText(f, OldTcad);
            var d = CadDoc.Load(f);
            Assert.Equal(1, d.Version); Assert.Equal(CadDoc.FormatVersion, d.Version);
            var built = Assert.Single(d.Build()); Assert.Null(built.Error);
            Assert.InRange(built.Solid.Volume(), 11999.0, 12001.0); // 40 × 30 × 10 – der Bauplan rechnet wie früher
            d.Save(f);
            var raw = JsonNode.Parse(File.ReadAllText(f))!.AsObject();
            Assert.Equal("Version", raw.First().Key); Assert.Equal(CadDoc.FormatVersion, raw["Version"]!.GetValue<int>());
            Assert.Equal(d.ToJson(), CadDoc.Load(f).ToJson());
            Assert.Equal(d.Version, d.Clone().Version);
            raw["Version"] = CadDoc.FormatVersion + 1; File.WriteAllText(f, raw.ToJsonString());
            var e = Assert.Throws<InvalidDataException>(() => CadDoc.Load(f));
            Assert.Contains("neueren Trendo-Fassung", e.Message);
        }
        finally { Directory.Delete(dir, true); }
    }

    // Aufbau wie von Scene3D.ToJson vor T-0714 geschrieben (ohne „version“)
    const string OldScene = "{\n  \"name\": \"Alter Tisch\",\n  \"parts\": [ { \"id\": \"ab12cd34\", \"name\": \"Platte\", \"shape\": \"cube\", \"sx\": 120, \"sy\": 60, \"sz\": 3, \"x\": 0, \"y\": 0, \"z\": 72, \"rx\": 0, \"ry\": 0, \"rz\": 0, \"color\": \"#C4A574\", \"text\": \"\", \"baseX\": 10, \"baseY\": 10, \"baseZ\": 10, \"ops\": [], \"hidden\": false } ],\n  \"lightPower\": 1,\n  \"cameraLens\": 0,\n  \"cameraHeight\": 0\n}";

    [Fact(DisplayName = "T-0714 3D: Szene-JSON hat ein Versionsfeld; alte Szenen ohne Angabe bleiben lesbar (Fassung 1), eine neuere Fassung wird verständlich abgelehnt")]
    public void Scene_version()
    {
        var s = Scene3D.FromJson(OldScene);
        Assert.Equal(1, s.Version); Assert.Equal("Alter Tisch", s.Name);
        var p = Assert.Single(s.Parts); Assert.Equal("Platte", p.Name); Assert.Equal(120, p.Sx); Assert.Equal(72, p.Z);
        var raw = JsonNode.Parse(s.ToJson())!.AsObject();
        Assert.Equal("version", raw.First().Key); Assert.Equal(Scene3D.FormatVersion, raw["version"]!.GetValue<int>());
        Assert.Equal(s.ToJson(), Scene3D.FromJson(s.ToJson()).ToJson());
        raw["version"] = Scene3D.FormatVersion + 1;
        var e = Assert.Throws<InvalidDataException>(() => Scene3D.FromJson(raw.ToJsonString()));
        Assert.Contains("neueren Trendo-Fassung", e.Message);
    }

    /// <summary>Gemeinsamer Ablauf der Abbruchtests: das Hilfsprogramm sichert in einer Schleife über ProgramLink (wie die App) und wird hart beendet.</summary>
    void KillLoop(string mode, string studio, Func<byte[], int> counter, int rounds = 10)
    {
        var dir = Tmp(); var rnd = new Random(714); int during = 0, worst = 0;
        try
        {
            for (var round = 1; round <= 3 * rounds && (round <= rounds || during < rounds / 2); round++) // unter Last weiter abbrechen, bis genug mitten im Schreiben lagen (wie T0708)
            {
                if (ProgramCrashTests.RunAndKill(mode, dir, 2, rnd)) during++;
                var (started, done, _) = ProgramCrashTests.ReadLog(dir);
                var bak = Path.Combine(dir, "sicherung");
                Assert.Empty(ProgramDocument.OrphanScan(bak, studio).Broken); // nie eine halbe Sicherung
                using var locks = new ProgramLocks(Path.Combine(dir, "sperren"));
                var link = new ProgramLink(studio, studio == "LINK-TEXT" ? "text" : "tabelle") { Locks = locks };
                var pending = link.Pending(bak); // die liegengebliebene Sperre des abgestürzten Prozesses gilt als verwaist
                Assert.True(pending.Count == 1, $"Runde {round}: {pending.Count} lesbare Sicherungen statt einer");
                var (_, bytes) = ProgramLink.FileOf(link.Restore(pending[0].File, bak));
                var n = counter(bytes); // die Datei lässt sich mit dem Programm öffnen
                Assert.True(n >= done, $"Runde {round}: Sicherung hat Stand {n}, fertig gesichert war {done}");
                Assert.True(n <= started, $"Runde {round}: Stand {n} > begonnen {started}");
                worst = Math.Max(worst, started - n);
            }
            Assert.True(worst <= 1, "Mehr als der eine laufende Schritt ging verloren: " + worst);
            Assert.True(during >= rounds / 2, $"Nur {during} von {rounds} Abbrüchen fielen in einen laufenden Schreibvorgang – der Test taugt so nicht.");
            output.WriteLine($"{mode}: {rounds} harte Abbrüche, {during} mitten im Schreiben, höchstens {worst} Schritt verloren.");
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    static int Open(byte[] bytes, string ext, Func<string, int> read)
    {
        var f = Path.Combine(Path.GetTempPath(), "trendo-t0714-" + Guid.NewGuid().ToString("N")[..8] + ext);
        try { File.WriteAllBytes(f, bytes); return read(f); } finally { File.Delete(f); }
    }

    [Fact(DisplayName = "T-0714 Absturz: Trendo Text an der Notsicherung der Hülle (DOCX über ProgramLink, mit Sperre) – 10-mal hart beendet, die Sicherung öffnet sich jedes Mal, höchstens der laufende Schritt fehlt")]
    public void Kill_while_autosaving_text_link() =>
        KillLoop("link-text", "LINK-TEXT", b => Open(b, ".docx", f => int.Parse(TextFiles.Open(f).Paras.First().Text.Split(' ')[1], System.Globalization.CultureInfo.InvariantCulture)));

    [Fact(DisplayName = "T-0714 Absturz: Trendo Tabelle an der Notsicherung der Hülle (XLSX über ProgramLink, mit Sperre) – 10-mal hart beendet, die Sicherung öffnet sich jedes Mal, höchstens der laufende Schritt fehlt")]
    public void Kill_while_autosaving_sheet_link() =>
        KillLoop("link-tabelle", "LINK-TABELLE", b => Open(b, ".xlsx", f => int.Parse(SheetFiles.Open(f).Sheets[0].Raw(0, 0), System.Globalization.CultureInfo.InvariantCulture)));

    [Fact(DisplayName = "T-0714 Atomar: Briefköpfe, Verkaufen (Ideen, Listen je Idee, Gründungsschritte), Vorhaben (Schrittliste), Adressbuch (Kontakte, Fotos), Kalender-Export schreiben über AtomicFile")]
    public void Office_stores_atomic()
    {
        var dir = Tmp();
        try
        {
            var audit = new AuditLog(Path.Combine(dir, "audit.jsonl"));
            // Briefköpfe: speichern, löschen – Inhalt richtig, keine Zwischendatei bleibt liegen
            var cases = new CaseStore(dir, audit);
            var h = cases.SaveLetterhead(new Letterhead("", "", "privat", "Erika Muster", "Musterweg 1", "12345 Musterstadt", "erika@example.org"), Principal.LocalOwner);
            Assert.Equal("Erika Muster", Assert.Single(cases.Letterheads(Principal.LocalOwner)).Name);
            cases.DeleteLetterhead(h.Id, Principal.LocalOwner); Assert.Empty(cases.Letterheads(Principal.LocalOwner));
            // Vorhaben: Schritt anhängen und ändern – Zeilen bleiben wie vorher
            var plan = Path.Combine(dir, "vorhaben"); Directory.CreateDirectory(plan);
            File.WriteAllText(Path.Combine(plan, CreationSuite.ListFile), "# Umzug\n\n- [ ] Kartons besorgen\n");
            CreationSuite.AddStep(plan, "Möbel abbauen");
            Assert.Equal(["Kartons besorgen", "Möbel abbauen"], CreationSuite.Steps(plan).Select(s => s.Text));
            CreationSuite.Toggle(plan, CreationSuite.Steps(plan)[1].Line);
            Assert.True(CreationSuite.Steps(plan)[1].Done); Assert.StartsWith("# Umzug", File.ReadAllText(Path.Combine(plan, CreationSuite.ListFile)));
            // Adressbuch
            var mail = new MailCenter(dir, audit);
            var c = mail.AddContact("Erika Muster", "erika@example.org");
            mail.SetPhoto(c.Id, [1, 2, 3], "png");
            Assert.NotNull(Assert.Single(new MailCenter(dir, audit).Contacts()).Photo);
            Assert.Empty(Directory.GetFiles(dir, "*.tmp", SearchOption.AllDirectories));
        }
        finally { Directory.Delete(dir, true); }

        // Gegenprobe im Quelltext: an diesen Speicherorten kein direktes File.Write*/Append* mehr (das war vor T-0714 so – nach einem Abbruch konnte eine halbe Datei bleiben)
        var root = new DirectoryInfo(AppContext.BaseDirectory); while (root is not null && !File.Exists(Path.Combine(root.FullName, "Trendo.sln"))) root = root.Parent;
        Assert.NotNull(root);
        string Src(string rel) => File.ReadAllText(Path.Combine(root!.FullName, "src", rel));
        foreach (var (file, bad) in new[]
        {
            ("Trendo.Core/LetterCase.cs", "File.WriteAllText(HeadsFile"),
            ("Trendo.Core/BusinessStudio.cs", "File.WriteAllText(_file"),
            ("Trendo.Core/BusinessMarket.cs", "File.WriteAllText(_file"),
            ("Trendo.Core/FinanceExtra.cs", "File.WriteAllText(F,"),
            ("Trendo.Core/FinanceExtra.cs", "File.WriteAllText(file,"),
            ("Trendo.Core/CreationSuite.cs", "File.AppendAllText("),
            ("Trendo.Core/CreationSuite.cs", "File.WriteAllLines("),
            ("Trendo.Core/CreationSuite.cs", "File.WriteAllText(Path.Combine(dir, ListFile)"),
            ("Trendo.Core/CreationMappe.cs", "File.WriteAllLines("),
            ("Trendo.Core/W3/Mail.cs", "File.WriteAllText(_file"),
            ("Trendo.Core/W3/AddressBook.cs", "File.WriteAllBytes(f, bytes)"),
            ("Trendo.Core/Sys/Sys02.cs", "File.WriteAllText(ics"),
            ("Trendo.App/MainWindow.W4Suite.cs", "File.WriteAllText(x.File"),
        })
            Assert.False(System.Text.RegularExpressions.Regex.IsMatch(Src(file), @"(?<![A-Za-z])" + System.Text.RegularExpressions.Regex.Escape(bad)), $"{file}: „{bad}“ schreibt nicht atomar"); // „AtomicFile.WriteAllText(…“ zählt nicht
    }
}
