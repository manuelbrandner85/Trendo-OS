using System.Diagnostics;
using System.Text.Json.Nodes;
using Trendo.Core.Programs;

// T-0680: Hilfsprogramm für die Absturz-Tests. Es arbeitet endlos und wird vom Test mitten in der Arbeit hart beendet.
// Aufruf: <art> <ordner> [zeilen]   – art: speichern | sichern | sperre | roh | szene3d | cad; zeilen: Umfang des Dokuments (je Zeile rund 50 Zeichen)
// T-0691: „szene3d“ und „cad“ schreiben die automatische Sicherung des 3D-Studios (Scene3D.SaveTo) bzw. von Trendo CAD (CadDoc.Save) – wie die App alle 20 s.
// Protokoll <ordner>/protokoll.txt: je Zeile „start <n>“ vor dem Schreiben und „fertig <n> <ms>“ nach dem Schreiben.
// (Das Protokoll wird angehängt und nach jeder Zeile geleert; eine abgeschnittene letzte Zeile ignoriert der Test.)
// T-0726: als „abgebrochenes FFmpeg“ aufgerufen (Vorschaubild, „-frames:v“): schreibt den Anfang eines PNG in die Zieldatei und bricht ab –
// wie ein FFmpeg, das mitten im Schreiben beendet wird (Absturz, Abbruch, Zeitüberschreitung).
if (Array.IndexOf(args, "-frames:v") >= 0)
{
    File.WriteAllBytes(args[^1], [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52]);
    return 1;
}
if (args.Length < 2) { Console.Error.WriteLine("Aufruf: <speichern|sichern|sperre|roh> <ordner>"); return 2; }
var mode = args[0]; var dir = args[1];
Directory.CreateDirectory(dir);
var file = Path.Combine(dir, "arbeit.tdoc");
var backup = Path.Combine(dir, "sicherung");
const string Studio = "SPEC-08";

// T-0726: „sperre-entwurf“ – wie ein zweites Trendo-Fenster mit dem Datenordner <ordner>: sperrt die Datei aus <ordner>/ziel.txt in <ordner>/programme/sperren
if (mode is "sperre" or "sperre-entwurf")
{
    using var locks = new ProgramLocks(mode == "sperre" ? Path.Combine(dir, "sperren") : Path.Combine(dir, "programme", "sperren"));
    var other = locks.Acquire(mode == "sperre" ? file : File.ReadAllText(Path.Combine(dir, "ziel.txt")).Trim());
    File.WriteAllText(Path.Combine(dir, "sperre-" + Environment.ProcessId + ".txt"), other is null ? "gesperrt" : "besetzt " + other.Pid);
    Thread.Sleep(Timeout.Infinite);
    return 0;
}

using var log = new FileStream(Path.Combine(dir, "protokoll.txt"), FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
void Log(string line) { log.Write(System.Text.Encoding.ASCII.GetBytes(line + "\n")); log.Flush(true); }

if (mode is "szene3d" or "cad")
{
    var rec = Path.Combine(dir, mode == "cad" ? "cad.tcad" : "3d-szene.json");
    var size = args.Length > 2 ? int.Parse(args[2]) : 6000;
    // großes Netz, damit ein Schreibvorgang einige Millisekunden dauert
    var pos = new float[size * 9]; var idx = new int[size * 3];
    for (var i = 0; i < pos.Length; i++) pos[i] = (i % 97) * 0.37f;
    for (var i = 0; i < idx.Length; i++) idx[i] = i;
    var k = 0;
    if (File.Exists(rec))
        k = mode == "cad" ? (int)Trendo.Core.Cad.CadDoc.Load(rec).Params["n"] : int.Parse(Trendo.Core.Scene3D.Load(rec).Name.Split(' ')[1], System.Globalization.CultureInfo.InvariantCulture);
    var scene = new Trendo.Core.Scene3D { Parts = { new Trendo.Core.Part3D { Shape = "mesh", Name = "Netz", Positions = pos, Indices = idx } } };
    var cad = new Trendo.Core.Cad.CadDoc { References = { new Trendo.Core.Cad.CadReference { Name = "Netz", Positions = pos, Indices = idx } } };
    var sw3 = new Stopwatch();
    while (true)
    {
        k++;
        Log("start " + k); sw3.Restart();
        if (mode == "cad") { cad.Params["n"] = k; cad.Save(rec); }
        else { scene.Name = "Stand " + k; scene.SaveTo(rec); }
        Log($"fertig {k} {sw3.ElapsedMilliseconds}");
    }
}

// T-0708: „link“ – ein angeschlossenes Programm außerhalb der Hülle (ProgramLink, z. B. Briefe): jede Änderung ein Schritt, danach die Notsicherung der Hülle.
// Beim Neustart wird – wie in der App – die Sicherung des vorigen Laufs zurückgeholt und weitergearbeitet.
if (mode == "link")
{
    var linkDir = Path.Combine(dir, "sicherung");
    var link = new ProgramLink("STUDIO-LETTER-01", "brief", new JsonObject { ["n"] = 0 }, "Brief");
    if (link.Pending(linkDir) is [var last, ..]) link.Restore(last.File, linkDir);
    var big = new JsonArray(); for (var i = 0; i < (args.Length > 2 ? int.Parse(args[2]) : 6000); i++) big.Add("Zeile " + i + " – Sehr geehrte Damen und Herren, hiermit kündige ich");
    var m = (int)ProgramDocument.ToNumber(link.State?["n"]);
    var swl = new Stopwatch();
    while (true)
    {
        m++;
        link.Commit("Text geschrieben", new JsonObject { ["n"] = m, ["text"] = big.DeepClone() });
        Log("start " + m); swl.Restart();
        link.Autosave(linkDir);
        Log($"fertig {m} {swl.ElapsedMilliseconds}");
    }
}

// T-0711: „link-editor“ – wie „link“, aber mit dem Stand des Editors der Übersicht (Reiter mit Text, Kodierung, Zeilenende) und mit der Sperre des Fensters
// (ProgramLocks): Der harte Abbruch lässt die Sperrdatei liegen – sie muss als verwaist gelten, sonst würde die Sicherung nie wieder angeboten.
if (mode == "link-editor")
{
    var linkDir = Path.Combine(dir, "sicherung");
    using var locks = new ProgramLocks(Path.Combine(dir, "sperren"));
    var link = new ProgramLink("LINK-EDITOR", "editor", new JsonObject { ["idx"] = 0, ["docs"] = new JsonArray() }, "Editor") { Locks = locks };
    if (link.Pending(linkDir) is [var last, ..]) link.Restore(last.File, linkDir);
    var text = string.Concat(Enumerable.Range(0, args.Length > 2 ? int.Parse(args[2]) * 4 : 24000).Select(i => "Zeile " + i + " – Einkaufsliste: Milch, Brot, Äpfel\r\n"));
    var m = (int)ProgramDocument.ToNumber(link.State?["n"]);
    var swe = new Stopwatch();
    while (true)
    {
        m++;
        link.Commit("Text geschrieben", new JsonObject { ["n"] = m, ["idx"] = 0, ["docs"] = new JsonArray { new JsonObject { ["path"] = null, ["text"] = text + m, ["enc"] = "UTF-8", ["bom"] = false, ["eol"] = "CRLF", ["dirty"] = true } } });
        Log("start " + m); swe.Restart();
        link.Autosave(linkDir);
        Log($"fertig {m} {swe.ElapsedMilliseconds}");
    }
}

// T-0715: „sendung“ – Live streamen speichert jede Änderung sofort: Sendeablauf (BroadcastDesk.Save → fach/sendung/ablauf.json) und Studio (LiveShow.Save → szenen.json, atomar),
// genau die Speicherwege der App. Je Schritt ändern sich Titel der Sendung und Name der ersten Szene („Stand <n>“). Beim Neustart wird – wie in der App – geladen und weitergezählt.
if (mode == "sendung")
{
    var desk = new Trendo.Core.BroadcastDesk(dir);
    var liveDir = Trendo.Core.Fach.StoreDir(dir, "sendung");
    var show = Trendo.Core.LiveShow.Load(liveDir);
    if (desk.Plan.Segments.Count == 0)
        for (var i = 0; i < (args.Length > 2 ? int.Parse(args[2]) / 10 : 4000); i++) desk.Plan.Segments.Add(new Trendo.Core.Segment { Title = "Programmpunkt " + i, Seconds = 90, Note = "Kamera 2, Gast im Studio, Einspieler vorbereiten" });
    for (var i = show.Scenes.Count; i < 12; i++) show.Scenes.Add(new Trendo.Core.LiveScene("s" + i, "Szene " + i, Trendo.Core.VideoKind.Screen, [.. Enumerable.Range(0, 400).Select(j => new Trendo.Core.LiveOverlay("o" + j, "text", "Bauchbinde " + j + " – Name, Funktion, Ort", 0.05, 0.78, 0.9))]));
    var k = desk.Plan.Show.StartsWith("Stand ") ? int.Parse(desk.Plan.Show[6..], System.Globalization.CultureInfo.InvariantCulture) : 0;
    var sws = new Stopwatch();
    while (true)
    {
        k++;
        desk.Plan.Show = "Stand " + k; show.Scenes[0] = show.Scenes[0] with { Name = "Stand " + k };
        Log("start " + k); sws.Restart();
        desk.Save(); show.Save(liveDir);
        Log($"fertig {k} {sws.ElapsedMilliseconds}");
    }
}

// T-0715: „link-medien“ – Arbeitsstand von „Medien bearbeiten“ (Datei, Ausschnitt, Clipliste, Bildeinstellungen) an der Notsicherung der Hülle (ProgramLink, mit Sperre),
// in der Form, die die App schreibt (MainWindow.MediaState). Beim Neustart wird – wie in der App – die Sicherung zurückgeholt und weitergearbeitet.
if (mode == "link-medien")
{
    var linkDir = Path.Combine(dir, "sicherung");
    using var locks = new ProgramLocks(Path.Combine(dir, "sperren"));
    var link = new ProgramLink("LINK-MEDIEN", "medien", new JsonObject(), "Medien-Bearbeitung") { Locks = locks };
    if (link.Pending(linkDir) is [var last, ..]) link.Restore(last.File, linkDir);
    var clips = Enumerable.Range(0, args.Length > 2 ? int.Parse(args[2]) * 2 : 80000).Select(i => @"C:\Videos\Urlaub 2026\Tag " + (i % 14) + @"\clip-" + i + ".mp4").ToArray();
    var m = (int)ProgramDocument.ToNumber(link.State?["anfang"]);
    var swm = new Stopwatch();
    while (true)
    {
        m++;
        link.Commit("Geändert", new JsonObject { ["datei"] = @"C:\Videos\film.mp4", ["anfang"] = m, ["ende"] = m + 10, ["clips"] = new JsonArray([.. clips.Select(c => (JsonNode?)c)]),
            ["bild"] = new JsonObject { ["Brightness"] = m % 100, ["Format"] = ".jpg" } });
        Log("start " + m); swm.Restart();
        link.Autosave(linkDir);
        Log($"fertig {m} {swm.ElapsedMilliseconds}");
    }
}

// T-0717: „link-ablauf“ – Abläufe: der neue Ablauf als Entwurf an der Notsicherung der Hülle (Formular wie in der App: id, Name, Auslöser, Zeit, Schritte als JSON),
// mit Sperre des Fensters. Beim Neustart wird die Sicherung des vorigen Laufs zurückgeholt und weitergearbeitet.
if (mode == "link-ablauf")
{
    var linkDir = Path.Combine(dir, "sicherung");
    using var locks = new ProgramLocks(Path.Combine(dir, "sperren"));
    var link = new ProgramLink("LINK-ABLAEUFE", "ablaeufe-neu", null, "Ein neuer Ablauf") { Locks = locks };
    if (link.Pending(linkDir) is [var last, ..]) link.Restore(last.File, linkDir);
    var steps = new JsonArray(); for (var i = 0; i < (args.Length > 2 ? int.Parse(args[2]) : 6000); i++) steps.Add(new JsonObject { ["Kind"] = 0, ["From"] = "Downloads", ["To"] = "Dokumente/PDF", ["Pattern"] = "*.pdf", ["Text"] = "Schritt " + i });
    var stepsJson = steps.ToJsonString();
    var m = link.State?["name"]?.GetValue<string>() is { } nm && nm.StartsWith("Ablauf ") ? int.Parse(nm[7..], System.Globalization.CultureInfo.InvariantCulture) : 0;
    var swa = new Stopwatch();
    while (true)
    {
        m++;
        link.Commit("Text geschrieben", new JsonObject { ["id"] = "neu", ["name"] = "Ablauf " + m, ["trigger"] = "1", ["at"] = "08:00", ["steps"] = stepsJson });
        Log("start " + m); swa.Restart();
        link.Autosave(linkDir);
        Log($"fertig {m} {swa.ElapsedMilliseconds}");
    }
}

// T-0717: „tafel“ – Produktion: die Tafel eines Großprojekts wird bei jeder Änderung sofort gespeichert (BigBoards.Save → ProjectStore.WriteFile, seit T-0717 atomar).
// Vorher (File.WriteAllText) konnte ein Abbruch eine halbe tafel.json hinterlassen – Trendo hätte dann eine leere Tafel gezeigt.
if (mode == "tafel")
{
    var store = new Trendo.Core.ProjectStore(Path.Combine(dir, "projekte"), new Trendo.Core.AuditLog(Path.Combine(dir, "audit.jsonl")));
    if (!store.List().Any(p => p.Id == "hofladen")) store.Create("hofladen", "Hofladen online");
    var board = Trendo.Core.BigBoards.Load(store, "hofladen");
    var cards = args.Length > 2 ? Math.Max(100, int.Parse(args[2]) / 10) : 4000;
    if (board.Cards.Count != cards) board.Cards = [.. Enumerable.Range(0, cards).Select(i => new Trendo.Core.BoardCard { Id = "k" + i, Title = "Karte " + i, Detail = "Texte für die Startseite schreiben, Fotos auswählen, Preise prüfen", Role = "Text" })];
    var m = board.Goal.StartsWith("Stand ") ? int.Parse(board.Goal[6..], System.Globalization.CultureInfo.InvariantCulture) : 0;
    var swb = new Stopwatch();
    while (true)
    {
        m++; board.Goal = "Stand " + m; board.Cards[m % cards].Progress = m % 100;
        Log("start " + m); swb.Restart();
        Trendo.Core.BigBoards.Save(store, "hofladen", board);
        Log($"fertig {m} {swb.ElapsedMilliseconds}");
    }
}

// T-0714: „link-text“ und „link-tabelle“ – Trendo Text bzw. Trendo Tabelle an der einen Notsicherung der Hülle (vorher eigener Weg „wiederherstellen/“):
// je Schritt ein geändertes Dokument, als DOCX bzw. XLSX über genau den Weg der App (ProgramLink.FileStateVia + Track + Autosave), mit Sperre des Fensters.
// Beim Neustart wird – wie in der App – die Sicherung des vorigen Laufs zurückgeholt (Datei öffnen, Zähler aus dem Dokument) und weitergearbeitet.
if (mode is "link-text" or "link-tabelle")
{
    var isText = mode == "link-text";
    var linkDir = Path.Combine(dir, "sicherung"); var scratch = Path.Combine(linkDir, ".arbeit");
    using var locks = new ProgramLocks(Path.Combine(dir, "sperren"));
    var studio = isText ? "LINK-TEXT" : "LINK-TABELLE"; var kind = isText ? "text" : "tabelle"; var name = isText ? "text.docx" : "tabelle.xlsx";
    var link = new ProgramLink(studio, kind, null, isText ? "Neues Text-Dokument" : "Neue Tabelle") { Locks = locks };
    var rows = args.Length > 2 ? Math.Max(50, int.Parse(args[2]) / 20) : 1500; // Testumfang 40 000 → 2 000 Absätze bzw. Zeilen (Schreiben dauert so einige zehn Millisekunden)
    var m = 0;
    if (link.Pending(linkDir) is [var last, ..])
    {
        var (_, bytes) = ProgramLink.FileOf(link.Restore(last.File, linkDir));
        var back = Path.Combine(dir, "zurueck-" + name); File.WriteAllBytes(back, bytes);
        m = isText ? int.Parse(Trendo.Core.Office.TextFiles.Open(back).Paras.First().Text.Split(' ')[1], System.Globalization.CultureInfo.InvariantCulture)
                   : int.Parse(Trendo.Core.Office.SheetFiles.Open(back).Sheets[0].Raw(0, 0), System.Globalization.CultureInfo.InvariantCulture);
    }
    var text = new Trendo.Core.Office.TextDoc { Blocks = [new Trendo.Core.Office.Para("Stand 0"), .. Enumerable.Range(0, rows).Select(i => (Trendo.Core.Office.Block)new Trendo.Core.Office.Para("Absatz " + i + " – Sehr geehrte Damen und Herren, hiermit bestätige ich den Termin."))] };
    var sheet = new Trendo.Core.Office.SheetDoc();
    for (var r = 1; r <= rows; r++) { sheet.Sheets[0].Set(r, 0, "Posten " + r); sheet.Sheets[0].Set(r, 1, (r * 1.5).ToString(System.Globalization.CultureInfo.InvariantCulture)); }
    var swo = new Stopwatch();
    while (true)
    {
        m++;
        if (isText) ((Trendo.Core.Office.Para)text.Blocks[0]).Text = "Stand " + m; else sheet.Sheets[0].Set(0, 0, m.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Log("start " + m); swo.Restart();
        link.Track(isText ? ProgramLink.FileStateVia(name, f => Trendo.Core.Office.TextFiles.Save(text, f), scratch) : ProgramLink.FileStateVia(name, f => Trendo.Core.Office.SheetFiles.Save(sheet, f), scratch));
        link.Autosave(linkDir);
        link.Track(null);
        Log($"fertig {m} {swo.ElapsedMilliseconds}");
    }
}

// T-0716: „baukasten“ – der Baukasten (sofort speichernd, B6 Etappe E4): je Schritt die Seite in seite.json (SitePage.Save, seit T-0716 atomar) und – wie in der App
// (LiveSlot) – der Stand mit getippten, noch nicht übernommenen Eingaben als Notsicherung der Hülle (ProgramLink mit Sperre des Fensters).
// Beim Neustart: Zähler aus seite.json (muss sich öffnen lassen), Sicherung des vorigen Laufs zurückholen, weiterarbeiten.
if (mode == "baukasten")
{
    var seite = Path.Combine(dir, "seite.json"); var linkDir = Path.Combine(dir, "sicherung");
    using var locks = new ProgramLocks(Path.Combine(dir, "sperren"));
    var link = new ProgramLink("LINK-BAUKASTEN", "baukasten", null, "Seite") { Locks = locks };
    var m = File.Exists(seite) ? int.Parse(Trendo.Core.SitePage.Load(seite).Title.Split(' ')[1], System.Globalization.CultureInfo.InvariantCulture) : 0;
    if (link.Pending(linkDir) is [var last, ..]) link.Restore(last.File, linkDir);
    var rows = args.Length > 2 ? Math.Max(100, int.Parse(args[2]) / 10) : 3000;
    var page = new Trendo.Core.SitePage { Blocks = [.. Enumerable.Range(0, rows).Select(i => new Trendo.Core.Block("p", "Absatz " + i + " – Willkommen auf meiner Seite, hier steht, was ich anbiete."))] };
    var swb = new Stopwatch();
    while (true)
    {
        m++;
        page.Title = "Stand " + m;
        Log("start " + m); swb.Restart();
        page.Save(seite); // gespeicherte Änderung (sofort, wie die App)
        link.Commit("Text geschrieben", new JsonObject { ["daten"] = JsonNode.Parse(page.ToJson()), ["ort"] = "0", ["eingaben"] = new JsonObject { ["builder-text-0"] = "getippt " + m } });
        link.Autosave(linkDir);
        Log($"fertig {m} {swb.ElapsedMilliseconds}");
    }
}

// Fortsetzen, wo der letzte (abgebrochene) Lauf stand – wie ein Programm nach dem Neustart
ProgramDocument doc;
if (File.Exists(file)) doc = ProgramDocument.Load(file, Studio);
else { doc = new ProgramDocument(Studio, "Arbeit"); doc.Set("n", 0); if (mode != "roh") doc.Save(file); }
// wie die Hülle beim Öffnen: Liegt eine Notsicherung neben der Datei, geht es mit ihr weiter
if (mode == "sichern" && File.Exists(ProgramDocument.AutosaveFile(file))) doc = ProgramDocument.Restore(ProgramDocument.AutosaveFile(file));
var n = (int)doc.Number("n");
var fresh = new ProgramDocument(Studio, "Noch ohne Namen");
var recent = new ProgramRecent(dir);
// großer Inhalt: das Schreiben dauert lange genug, dass ein Abbruch auch mitten hinein fällt
var lines = args.Length > 2 ? int.Parse(args[2]) : 6000;
var filler = new JsonArray(); for (var i = 0; i < lines; i++) filler.Add("Zeile " + i + " – Stoff, Schnitt, Maß, Farbe, Preis, Größe");
doc.Set("inhalt", filler);
var sw = new Stopwatch();
while (true)
{
    n++;
    doc.Set("n", n);
    Log("start " + n);
    sw.Restart();
    switch (mode)
    {
        case "speichern": // Dokument, Versionen und „Zuletzt“
            doc.Save(file); recent.Add(Studio, file);
            break;
        case "sichern": // Notsicherung neben der Datei und die eines noch unbenannten Dokuments
            doc.Autosave(backup);
            fresh.Set("n", n); fresh.Autosave(backup);
            break;
        case "roh": // Gegenprobe: so wurde die Notsicherung früher geschrieben (direkt in die Zieldatei)
            File.WriteAllText(file, doc.Serialize());
            break;
        default: return 2;
    }
    Log($"fertig {n} {sw.ElapsedMilliseconds}");
}
