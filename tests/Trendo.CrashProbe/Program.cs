using System.Diagnostics;
using System.Text.Json.Nodes;
using Trendo.Core.Programs;

// T-0680: Hilfsprogramm für die Absturz-Tests. Es arbeitet endlos und wird vom Test mitten in der Arbeit hart beendet.
// Aufruf: <art> <ordner> [zeilen]   – art: speichern | sichern | sperre | roh | szene3d | cad; zeilen: Umfang des Dokuments (je Zeile rund 50 Zeichen)
// T-0691: „szene3d“ und „cad“ schreiben die automatische Sicherung des 3D-Studios (Scene3D.SaveTo) bzw. von Trendo CAD (CadDoc.Save) – wie die App alle 20 s.
// Protokoll <ordner>/protokoll.txt: je Zeile „start <n>“ vor dem Schreiben und „fertig <n> <ms>“ nach dem Schreiben.
// (Das Protokoll wird angehängt und nach jeder Zeile geleert; eine abgeschnittene letzte Zeile ignoriert der Test.)
if (args.Length < 2) { Console.Error.WriteLine("Aufruf: <speichern|sichern|sperre|roh> <ordner>"); return 2; }
var mode = args[0]; var dir = args[1];
Directory.CreateDirectory(dir);
var file = Path.Combine(dir, "arbeit.tdoc");
var backup = Path.Combine(dir, "sicherung");
const string Studio = "SPEC-08";

if (mode == "sperre")
{
    using var locks = new ProgramLocks(Path.Combine(dir, "sperren"));
    var other = locks.Acquire(file);
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
