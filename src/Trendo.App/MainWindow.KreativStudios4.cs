using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Highlighting;
using SkiaSharp;
using Trendo.Core;
using Trendo.Core.Kreativ;
using Trendo.Core.Programs;
using Path = System.IO.Path;

namespace Trendo.App;

/// <summary>Etappe „Kreativ &amp; KI“ Teil 4 (T-0615): Code mit KI, Bilder erkennen, Recherche mit Quellen, Tagesplaner.
/// Baut auf Vorhandenem auf: Projektbaum und Editor des Code-Editors, Claude-Leiste mit Anhängen, Texterkennung (Tesseract) aus den Zusatzprogrammen,
/// Sitzungsquellen des Recherche-Studios (SYS-08), Kalender und Aufgaben (SYS-09).</summary>
public partial class MainWindow
{
    const string CodeAiKey = "code-ki-programm", VisionKey = "bilder-erkennen-programm", CiteKey = "quellen-programm", DayKey = "tagesplaner-programm";

    private void InitKreativStudios4() { InitCodeAi(); InitVision(); InitCite(); InitDayPlan(); }

    /// <summary>Claude-Knöpfe der Studios: Claude arbeitet mit dem Zugang der Person; ohne Verbindung eine klare Meldung statt Stille. Anhänge über den Anhang-Weg der Claude-Leiste.</summary>
    async Task KrClaude(string key, string studio, string text, IReadOnlyList<string>? attach = null)
    {
        if (!ClaudeTakesStudioPrompts) { _progMsg[key] = "Claude ist nicht verbunden. Verbinde deinen eigenen Claude-Zugang rechts unter „Claude“, dann geht es hier mit einem Klick."; Show(key); return; }
        if (attach is { Count: > 0 } && Windows.Active?.Id is { } id)
        {
            if (!_claudeDocks.ContainsKey(id)) { ClaudeDockToggle(studio, null); if (_claudeDocks.TryGetValue(id, out var dk)) ClaudeDockSetMax(dk, false); }
            ClaudeChatAttach(attach);
        }
        var ok = await ClaudeFromStudioBar(studio, text, m => _progMsg[key] = m);
        if (!ok) _progMsg[key] = "Claude hat den Auftrag nicht übernommen.";
        Show(key);
    }

    static TextBlock K4Mono(string text, IBrush? fg = null, string? name = null) =>
        new() { Name = name, Text = text, Foreground = fg ?? Text, FontFamily = new FontFamily("Cascadia Code, Consolas, Menlo, monospace"), FontSize = 12, TextWrapping = TextWrapping.NoWrap };

    static readonly IBrush K4Warn = Brush.Parse("#F5A524"), K4Good = Brush.Parse("#4ADE80"), K4Bad = Brush.Parse("#F87171");

    // ======================================================================
    // Code mit KI
    // ======================================================================

    /// <summary>Tests: Befehl ausführen (Programm, Argumente, Ordner, Zeitlimit in ms) statt eines echten Prozesses.</summary>
    public static Func<string, IReadOnlyList<string>, string, int, Cli.Result>? CodeAiRunOverride { get; set; }

    string? _caFor, _caDocFile, _caSaved, _caDiffPath, _caDiskSha, _caDiscard; bool _caOverwrite;
    (string File, DateTime When, CodeAiKit.Snapshot? Snap) _caSnapCache;
    TextDocument? _caDoc; bool _caAsk, _caBusy; string _caOut = "";
    readonly HashSet<string> _caCollapsed = new(StringComparer.OrdinalIgnoreCase);
    List<CodeAiKit.Change>? _caChanges; int _caFileCount;

    static JsonObject CodeAiDoc(string root = "") => new() { ["root"] = root, ["file"] = "", ["kind"] = "", ["wish"] = "", ["timeout"] = "300" };

    void CaState(ProgramDocument d)
    {
        if (_caFor == d.Id) return;
        _caFor = d.Id; _caDoc = null; _caDocFile = null; _caSaved = null; _caAsk = false; _caOut = ""; _caChanges = null; _caDiffPath = null; _caCollapsed.Clear();
        if (CaRoot(d) is { } r) foreach (var e in CodeProject.Tree(r).Where(e => e.IsDir)) _caCollapsed.Add(e.Path);
        if (CaFile(d) is { } f) try { CaLoad(f); } catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException) { }
    }

    static string? CaRoot(ProgramDocument d) => KreativSafe.LocalDir(d.Text("root")) ? Path.GetFullPath(d.Text("root")) : null;

    /// <summary>Nur Dateien im Projektordner – eine fremde Projektdatei darf auf nichts anderes zeigen.</summary>
    static string? CaFile(ProgramDocument d)
    {
        if (CaRoot(d) is not { } r || !KreativSafe.LocalFile(d.Text("file"))) return null;
        var f = Path.GetFullPath(d.Text("file"));
        return CodeAiKit.InsideNoLinks(r, f) ? f : null;
    }

    static string? Sha(string file) { try { using var s = File.OpenRead(file); return Convert.ToHexStringLower(SHA256.HashData(s)); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; } }

    string CaSnapFile(string root) => Path.Combine(_core.DataRoot, "code-ki", Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(root.ToLowerInvariant())))[..16], "schnappschuss.json");

    /// <summary>Schnappschuss laden – zwischengespeichert, solange sich die Datei nicht ändert (Status und Arbeitsfläche fragen bei jedem Neuzeichnen).</summary>
    CodeAiKit.Snapshot? CaSnapshot(ProgramDocument d)
    {
        if (CaRoot(d) is not { } r) return null;
        var f = CaSnapFile(r);
        if (!File.Exists(f)) return null;
        var when = File.GetLastWriteTimeUtc(f);
        if (_caSnapCache.File == f && _caSnapCache.When == when) return _caSnapCache.Snap;
        var s = CodeAiKit.Load(File.ReadAllText(f)); _caSnapCache = (f, when, s); return s;
    }

    void CaLoad(string file)
    {
        var text = CodeProject.ReadText(file);
        _caDoc = new TextDocument(text) { FileName = file }; _caDocFile = file; _caSaved = text; _caDiskSha = Sha(file); _caOverwrite = false; _caDiscard = null;
    }

    CodeAiKit.Kind? CaKind(ProgramDocument d)
    {
        if (CaRoot(d) is not { } r) return null;
        var kinds = CodeAiKit.DetectProject(r);
        return kinds.FirstOrDefault(k => k.Id == d.Text("kind")) ?? kinds.FirstOrDefault();
    }

    public void CodeAiOpenFolder(ProgramDocument d, string dir)
    {
        if (!KreativSafe.LocalDir(dir)) throw new ArgumentException("Wähle einen Projektordner auf diesem Rechner.");
        var full = Path.GetFullPath(dir);
        if (_caFor == d.Id && _caDoc is not null && _caDocFile is not null && _caDoc.Text != _caSaved && _caDiscard != full)
        { _caDiscard = full; throw new InvalidOperationException($"„{Path.GetFileName(_caDocFile)}“ hat ungespeicherte Änderungen. Erst speichern – oder nochmal öffnen, dann gehen sie verloren."); }
        d.Edit("Projekt geöffnet", x => { x["root"] = full; x["file"] = ""; x["kind"] = ""; });
        if (d.Title is "Unbenannt" or "") d.Title = Path.GetFileName(full.TrimEnd(Path.DirectorySeparatorChar));
        _caFor = null; CaState(d);
        var kinds = CodeAiKit.DetectProject(full);
        _progMsg[CodeAiKey] = $"Projekt „{Path.GetFileName(full.TrimEnd(Path.DirectorySeparatorChar))}“ geöffnet" + (kinds.Count > 0 ? ": " + string.Join(", ", kinds.Select(k => k.Label)) + "." : ". Keine bekannte Projektart erkannt.");
    }

    public void CodeAiOpenFile(ProgramDocument d, string file)
    {
        CaState(d);
        var r = CaRoot(d) ?? throw new InvalidOperationException("Öffne zuerst einen Projektordner.");
        var full = Path.GetFullPath(file);
        if (!CodeAiKit.InsideNoLinks(r, full)) throw new InvalidOperationException("Nur Dateien aus dem Projektordner (ohne verknüpfte Ordner).");
        // ungespeicherte Änderungen nie still verwerfen: erst beim zweiten Klick
        if (_caDoc is not null && _caDocFile is not null && _caDoc.Text != _caSaved && _caDiscard != full)
        {
            _caDiscard = full;
            throw new InvalidOperationException($"„{Path.GetFileName(_caDocFile)}“ hat ungespeicherte Änderungen. Erst speichern – oder nochmal klicken, dann gehen sie verloren.");
        }
        CaLoad(full);
        d.Set("file", full, "Datei");
        _progMsg[CodeAiKey] = "Geöffnet: " + Path.GetRelativePath(r, full);
    }

    public void CodeAiSaveFile(ProgramDocument d)
    {
        CaState(d);
        if (_caDoc is null || _caDocFile is null || CaFile(d) is not { } f || !string.Equals(f, _caDocFile, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Es ist keine Datei offen.");
        if (_caDoc.Text == _caSaved) { _progMsg[CodeAiKey] = "Nichts zu speichern."; return; }
        // inzwischen auf der Platte geändert (z. B. von Claude)? Nie ohne Rückfrage überschreiben
        if (File.Exists(f) && Sha(f) != _caDiskSha && !_caOverwrite)
        {
            _caOverwrite = true;
            _progMsg[CodeAiKey] = $"⚠ „{Path.GetFileName(f)}“ wurde inzwischen geändert (z. B. von Claude). Nochmal speichern überschreibt diese Änderung – oder die Datei links neu öffnen.";
            return;
        }
        CodeProject.WriteText(f, _caDoc.Text); _caSaved = _caDoc.Text; _caDiskSha = Sha(f); _caOverwrite = false;
        _core.Audit.Append(Principal.LocalOwner.Name, "code-ki.save", Path.GetFileName(f));
        _progMsg[CodeAiKey] = "Gespeichert: " + Path.GetFileName(f);
    }

    public string CodeAiSnapshot(ProgramDocument d)
    {
        var r = CaRoot(d) ?? throw new InvalidOperationException("Öffne zuerst einen Projektordner.");
        var s = CodeAiKit.Take(r);
        var file = CaSnapFile(r); Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, CodeAiKit.Save(s));
        _caChanges = []; _caDiffPath = null;
        _progMsg[CodeAiKey] = $"Schnappschuss von {s.Files.Count} Dateien angelegt. Was sich ab jetzt ändert, siehst du unter „Seit Schnappschuss geändert“.";
        return file;
    }

    public List<CodeAiKit.Change> CodeAiChanges(ProgramDocument d)
    {
        var r = CaRoot(d) ?? throw new InvalidOperationException("Öffne zuerst einen Projektordner.");
        var snap = CaSnapshot(d) ?? throw new InvalidOperationException("Lege zuerst einen Schnappschuss an.");
        _caChanges = CodeAiKit.Changed(snap, CodeAiKit.Take(r));
        _caDiffPath = _caChanges.FirstOrDefault()?.Path;
        _progMsg[CodeAiKey] = _caChanges.Count == 0 ? "✓ Seit dem Schnappschuss hat sich nichts geändert." : $"{_caChanges.Count} Dateien seit dem Schnappschuss geändert.";
        return _caChanges;
    }

    public IReadOnlyList<CodeAiKit.Line> CodeAiDiff(ProgramDocument d, string rel)
    {
        var r = CaRoot(d) ?? throw new InvalidOperationException("Öffne zuerst einen Projektordner.");
        var snap = CaSnapshot(d) ?? throw new InvalidOperationException("Lege zuerst einen Schnappschuss an.");
        var old = snap.Files.FirstOrDefault(f => string.Equals(f.Path, rel, StringComparison.OrdinalIgnoreCase));
        if (old is not null && old.Text is null) throw new InvalidOperationException("Binärdatei oder zu groß – nur „geändert“, kein Zeilenvergleich.");
        var full = Path.GetFullPath(Path.Combine(r, rel));
        if (!CodeAiKit.InsideNoLinks(r, full)) throw new InvalidOperationException("Nur Dateien aus dem Projektordner.");
        string now = "";
        if (File.Exists(full))
        {
            if (new FileInfo(full).Length > CodeAiKit.MaxTextBytes) throw new InvalidOperationException("Die Datei ist zu groß für den Zeilenvergleich.");
            now = CodeProject.ReadText(full);
        }
        return CodeAiKit.Diff(old?.Text, now);
    }

    string CaPatch(ProgramDocument d)
    {
        var changes = CodeAiChanges(d); var sb = new StringBuilder();
        foreach (var c in changes)
            try { sb.Append(CodeAiKit.Unified(CodeAiDiff(d, c.Path), c.Path, added: c.Kind == CodeAiKit.ChangeKind.Neu, removed: c.Kind == CodeAiKit.ChangeKind.Geloescht)); }
            catch (InvalidOperationException) { sb.Append($"# {c.Path}: {c.Kind} (ohne Zeilenvergleich)\n"); }
        if (sb.Length == 0) throw new InvalidOperationException("Seit dem Schnappschuss hat sich nichts geändert.");
        return sb.ToString();
    }

    /// <summary>Testbefehl starten (erst nach Bestätigung in der Oberfläche), mit Zeitlimit; Ausgabe in die Arbeitsfläche.</summary>
    public async Task<Cli.Result?> CodeAiRunTests(ProgramDocument d)
    {
        CaState(d);
        if (_caBusy) { _progMsg[CodeAiKey] = "Die Tests laufen schon."; Show(CodeAiKey); return null; }
        var r = CaRoot(d); var kind = CaKind(d);
        var cmd = r is null || kind is null ? null : CodeAiKit.TestCommand(r, kind.Id);
        if (r is null || cmd is null) { _progMsg[CodeAiKey] = "Für dieses Projekt kennt Trendo keinen Testbefehl (.NET, npm, Python, Cargo oder Go)."; Show(CodeAiKey); return null; }
        _caAsk = false;
        if (_opt.Isolated && CodeAiRunOverride is null) { _caOut = "Testmodus: Trendo startet keine Programme."; Show(CodeAiKey); return null; }
        var limit = int.TryParse(d.Text("timeout"), out var t) ? Math.Clamp(t, 10, 3600) : 300;
        _caBusy = true; _caOut = $"Läuft: {cmd.Display} …"; _progMsg[CodeAiKey] = "Tests laufen …"; Show(CodeAiKey);
        try
        {
            var env = kind!.Id == "python" ? new Dictionary<string, string> { ["PYTHONIOENCODING"] = "utf-8" } : null;
            var res = CodeAiRunOverride is { } o ? await Task.Run(() => o(cmd.Exe, cmd.Args, r, limit * 1000))
                : await Task.Run(() => Cli.Run(cmd.Exe, cmd.Args, r, limit * 1000, env, kind.Id == "python" ? new UTF8Encoding(false) : null));
            var all = (res.Out + "\n" + res.Err).Trim();
            if (all.Length > 20000) all = "… (gekürzt)\n" + all[^20000..];
            var head = res.Ok ? "✓ Tests bestanden (Exit 0)" : res.Exit == -2 ? $"✕ Zeitlimit ({limit} s) überschritten – abgebrochen" : res.Exit == -1 ? "✕ Ließ sich nicht starten" : $"✕ Exit {res.Exit}";
            _caOut = $"{head}\n$ {cmd.Display}\n\n{all}";
            _progMsg[CodeAiKey] = head;
            _core.Audit.Append(Principal.LocalOwner.Name, "code-ki.test", $"{Path.GetFileName(r)} {cmd.Exe} exit={res.Exit}");
            return res;
        }
        finally { _caBusy = false; Show(CodeAiKey); }
    }

    string CaExcerpt(ProgramDocument d, out string rel)
    {
        CaState(d);
        var r = CaRoot(d) ?? throw new InvalidOperationException("Öffne zuerst einen Projektordner.");
        if (_caDoc is null || _caDocFile is null) throw new InvalidOperationException("Öffne links eine Datei.");
        rel = Path.GetRelativePath(r, _caDocFile).Replace('\\', '/');
        var text = _caDoc.Text;
        var cut = KreativSafe.Cut(text, 12000);
        return $"Datei „{rel}“ ({CodeProject.Language(_caDocFile).Label}) im Projektordner „{r}“:\n\n```\n{cut}\n```" + (cut.Length < text.Length ? "\n(Ausschnitt, die Datei ist länger.)" : "");
    }

    void CaClaude(ProgramDocument d, string what)
    {
        string excerpt, rel;
        try { excerpt = CaExcerpt(d, out rel); }
        catch (InvalidOperationException ex) { _progMsg[CodeAiKey] = ex.Message; Show(CodeAiKey); return; }
        var kind = CaKind(d); var cmd = kind is null || CaRoot(d) is not { } r ? null : CodeAiKit.TestCommand(r, kind.Id);
        string prompt;
        switch (what)
        {
            case "aendern":
                var wish = d.Text("wish").Trim();
                if (wish.Length == 0) { _progMsg[CodeAiKey] = "Schreib rechts unter „Was soll Claude ändern?“ deinen Wunsch."; Show(CodeAiKey); return; }
                // vorher ein Schnappschuss, damit jede Änderung vergleichbar ist
                if (CaSnapshot(d) is null) CodeAiSnapshot(d);
                prompt = $"Ändere „{rel}“: {wish}\nÄndere nur, was dafür nötig ist, und halte den Stil des Projekts ein. Führe keine Programme aus. Zeig die Änderung als vollständige Datei oder als Diff.\n\n{excerpt}";
                break;
            case "test":
                prompt = $"Schreib Unit-Tests für „{rel}“, passend zum Projekt ({kind?.Label ?? "unbekannte Projektart"}{(cmd is null ? "" : $", Testbefehl „{cmd.Display}“")}). Nenne Dateiname und Inhalt der Testdatei. Führe nichts aus.\n\n{excerpt}";
                break;
            default:
                prompt = $"Erkläre verständlich, was diese Datei macht: Aufbau, wichtige Stellen, mögliche Fehler. Kurz und auf Deutsch.\n\n{excerpt}";
                break;
        }
        _ = KrClaude(CodeAiKey, "STUDIO-CODEAI-01", prompt);
    }

    async Task CaPickFolder(ProgramDocument d)
    {
        if (await PickFolderPath("Projektordner öffnen") is { } f) ProgramRun(CodeAiKey, () => CodeAiOpenFolder(d, f));
    }

    void CaTestClick(ProgramDocument d)
    {
        var r = CaRoot(d); var kind = CaKind(d);
        var cmd = r is null || kind is null ? null : CodeAiKit.TestCommand(r, kind.Id);
        if (cmd is null) { _progMsg[CodeAiKey] = "Für dieses Projekt kennt Trendo keinen Testbefehl (.NET, npm, Python, Cargo oder Go)."; Show(CodeAiKey); return; }
        if (!_caAsk)
        {
            var limit = int.TryParse(d.Text("timeout"), out var t) ? Math.Clamp(t, 10, 3600) : 300;
            _caAsk = true; _progMsg[CodeAiKey] = $"Trendo startet „{cmd.Display}“ im Projektordner (Zeitlimit {limit} s). Nochmal klicken zum Ausführen.";
            Show(CodeAiKey); return;
        }
        _ = CodeAiRunTests(d);
    }

    /// <summary>Beispielprojekt in einem eigenen Ordner der Projekte; ein vorhandener Ordner wird nie überschrieben.</summary>
    string CaSample()
    {
        var parent = WorkFolder("Code mit KI"); var dir = Path.Combine(parent, "beispiel-rechner");
        for (var i = 2; Directory.Exists(dir) && !File.Exists(Path.Combine(dir, "rechner.py")); i++) dir = Path.Combine(parent, $"beispiel-rechner-{i}");
        if (Directory.Exists(dir)) return dir; // schon angelegt: weiterverwenden, nichts überschreiben
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "rechner.py"), "\"\"\"Kleiner Rechner zum Ausprobieren.\"\"\"\n\n\ndef addiere(a, b):\n    return a + b\n\n\ndef teile(a, b):\n    if b == 0:\n        raise ValueError(\"Durch null kann man nicht teilen.\")\n    return a / b\n");
        File.WriteAllText(Path.Combine(dir, "test_rechner.py"), "import unittest\n\nfrom rechner import addiere, teile\n\n\nclass RechnerTest(unittest.TestCase):\n    def test_addiere(self):\n        self.assertEqual(addiere(2, 3), 5)\n\n    def test_teile(self):\n        self.assertEqual(teile(9, 3), 3)\n        with self.assertRaises(ValueError):\n            teile(1, 0)\n\n\nif __name__ == \"__main__\":\n    unittest.main()\n");
        File.WriteAllText(Path.Combine(dir, "README.md"), "# Beispiel-Rechner\n\nZwei kleine Funktionen und ein Test – zum Ausprobieren von „Code mit KI“.\n");
        return dir;
    }

    private void InitCodeAi() => RegisterProgram(FachTabs, new ProgramSpec(CodeAiKey, "STUDIO-CODEAI-01", "Code mit KI")
    {
        Sentence = "Projektordner öffnen, Dateien ansehen und bearbeiten, mit Claude erklären und ändern lassen, Tests ausführen und Änderungen vergleichen.",
        Folder = "Code mit KI",
        ScrollWorkspace = true,
        Templates =
        [
            new("leer", "Projekt öffnen", "Einen vorhandenen Projektordner wählen", () => CodeAiDoc()),
            new("beispiel", "Beispiel: kleines Python-Projekt", "Rechner mit Test zum Ausprobieren", () => { try { var dir = CaSample(); var j = CodeAiDoc(dir); j["file"] = Path.Combine(dir, "rechner.py"); return j; } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return CodeAiDoc(); } }),
        ],
        Ribbon =
        [
            ("Projekt", [
                new ProgramCmd("ca-open", "Ordner öffnen …", d => _ = CaPickFolder(d), Primary: true, Tip: "Projektordner auf diesem Rechner wählen."),
                new ProgramCmd("ca-save", "Datei speichern", d => CodeAiSaveFile(d), Tip: "Speichert die offene Datei (Zeilenenden bleiben)."),
                new ProgramCmd("ca-editor", "Im Code-Editor öffnen", d => { var r = CaRoot(d) ?? throw new InvalidOperationException("Öffne zuerst einen Projektordner."); CodeOpenFolder(r); if (CaFile(d) is { } f) CodeOpenFile(f); Show(CEK); }, Tip: "Großer Editor mit Reitern, Suche und Git."),
            ]),
            ("Claude", [
                new ProgramCmd("ca-explain", "Mit Claude erklären", d => CaClaude(d, "erklaeren"), Tip: "Schickt die offene Datei an Claude (dein Zugang)."),
                new ProgramCmd("ca-change", "Mit Claude ändern", d => CaClaude(d, "aendern"), Tip: "Dein Wunsch steht rechts. Vorher entsteht ein Schnappschuss zum Vergleichen."),
                new ProgramCmd("ca-testwrite", "Test schreiben mit Claude", d => CaClaude(d, "test")),
            ]),
            ("Prüfen", [
                new ProgramCmd("ca-test", "Tests ausführen …", CaTestClick, Tip: "Erst nach deiner Bestätigung, mit Zeitlimit."),
                new ProgramCmd("ca-snap", "Schnappschuss", d => CodeAiSnapshot(d), Tip: "Merkt sich den Stand aller Dateien (Prüfsummen)."),
                new ProgramCmd("ca-changes", "Seit Schnappschuss geändert", d => CodeAiChanges(d)),
            ]),
        ],
        Exports =
        [
            new("patch", "Änderungen (.patch)", ".patch", (d, f) => { File.WriteAllText(f, CaPatch(d)); return f; }),
            new("md", "Bericht (.md)", ".md", (d, f) => { File.WriteAllText(f, CaReport(d)); return f; }),
        ],
        Workspace = CodeAiWorkspace,
        Properties = CodeAiProperties,
        Status = d => CaRoot(d) is { } r ? $"{Path.GetFileName(r.TrimEnd(Path.DirectorySeparatorChar))} · {CaKind(d)?.Label ?? "Projektart unbekannt"} · {_caFileCount} Dateien" + (CaSnapshot(d) is { } s ? $" · Schnappschuss {s.TakenUtc.ToLocalTime().ToString("dd.MM. HH:mm", De)}" : "") : "Kein Projekt geöffnet",
        Help = "Links „Ordner öffnen“ und eine Datei wählen. In der Mitte ansehen und bearbeiten, „Datei speichern“ schreibt sie zurück. Claude erklärt, ändert oder schreibt Tests – mit deinem eigenen Zugang. „Tests ausführen“ zeigt erst den Befehl und startet nach dem zweiten Klick. Der Schnappschuss merkt sich alle Dateien; danach siehst du Zeile für Zeile, was sich geändert hat.",
    }, "code mit ki", "code ki", "programmieren mit ki", "code erklären", "code ändern", "unit test", "tests ausführen", "diff", "schnappschuss", "copilot", "claude code");

    string CaReport(ProgramDocument d)
    {
        var r = CaRoot(d) ?? throw new InvalidOperationException("Öffne zuerst einen Projektordner.");
        var sb = new StringBuilder($"# Projekt „{Path.GetFileName(r.TrimEnd(Path.DirectorySeparatorChar))}“\n\nStand: {DateTime.Now.ToString("dd.MM.yyyy HH:mm", De)}\n\n");
        foreach (var k in CodeAiKit.DetectProject(r)) sb.Append($"- {k.Label} (erkannt an {k.Marker}), Test: `{CodeAiKit.TestCommand(r, k.Id)?.Display ?? "–"}`\n");
        if (_caOut.Length > 0) sb.Append("\n## Letzter Testlauf\n\n```\n").Append(_caOut).Append("\n```\n");
        if (CaSnapshot(d) is not null)
        {
            var ch = CodeAiChanges(d);
            sb.Append("\n## Seit dem Schnappschuss geändert\n\n");
            if (ch.Count == 0) sb.Append("Nichts.\n");
            foreach (var c in ch) sb.Append($"- {c.Path} ({c.Kind switch { CodeAiKit.ChangeKind.Neu => "neu", CodeAiKit.ChangeKind.Geloescht => "gelöscht", _ => "geändert" }})\n");
        }
        return sb.ToString();
    }

    Control CodeAiWorkspace(ProgramDocument d)
    {
        CaState(d);
        var sp = new StackPanel { Name = "ca-workspace", Spacing = 12, Margin = new Thickness(4, 0, 10, 4) };
        var root = CaRoot(d);
        if (root is null)
        {
            var open = new Button { Name = "ca-open-big", Content = "Projektordner öffnen …", Padding = new Thickness(16, 8) }; open.Classes.Add("accent");
            open.Click += (_, _) => _ = CaPickFolder(d);
            sp.Children.Add(KrCard(new StackPanel { Spacing = 8, Children = { H("Projekt öffnen", 15), P("Wähle den Ordner deines Programms. Trendo erkennt .NET, npm (JavaScript), Python, Cargo (Rust) und Go und schlägt den passenden Testbefehl vor.", Muted, 12), open } }, "ca-welcome"));
            return sp;
        }
        // Dateibaum
        var tree = new StackPanel { Name = "ca-tree", Spacing = 1 };
        var entries = CodeProject.Tree(root, _caCollapsed, 4000);
        _caFileCount = entries.Count(e => !e.IsDir);
        var i = 0;
        foreach (var e in entries.Take(800))
        {
            var en = e; var openFile = !e.IsDir && string.Equals(e.Path, _caDocFile, StringComparison.OrdinalIgnoreCase);
            var b = new Button { Name = "ca-node-" + i++, Background = openFile ? Brush.Parse("#2A3A55") : Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(6 + 14 * e.Depth, 3, 6, 3), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
                Content = new TextBlock { Text = (e.IsDir ? (_caCollapsed.Contains(e.Path) ? "▸ " : "▾ ") : "  ") + e.Name, Foreground = e.IsDir ? Text : Muted, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis } };
            ToolTip.SetTip(b, Path.GetRelativePath(root, e.Path));
            b.Click += (_, _) => ProgramRun(CodeAiKey, () =>
            {
                if (en.IsDir) { if (!_caCollapsed.Remove(en.Path)) _caCollapsed.Add(en.Path); }
                else CodeAiOpenFile(d, en.Path);
            });
            tree.Children.Add(b);
        }
        if (entries.Count > 800) tree.Children.Add(P($"… und {entries.Count - 800} weitere – im Code-Editor alle.", Muted, 11));
        var left = KrCard(new StackPanel { Spacing = 6, Children = { H(Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar)), 14), new ScrollViewer { MaxHeight = 620, Content = tree } } }, "ca-tree-card");

        // Editor
        var center = new StackPanel { Spacing = 12 };
        if (_caDoc is not null && _caDocFile is not null)
        {
            CodeDarkColors();
            var lang = CodeProject.Language(_caDocFile);
            var ed = new TextEditor { Name = "ca-editor", Document = _caDoc, ShowLineNumbers = true, FontFamily = new FontFamily("Cascadia Code, Consolas, Menlo, monospace"), FontSize = 13, Height = 420,
                Background = Brush.Parse("#0E1117"), Foreground = Brush.Parse("#D4D4D4"), LineNumbersForeground = Brush.Parse("#5A6475"), Padding = new Thickness(6, 4),
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                SyntaxHighlighting = lang.Highlight is { } hl ? HighlightingManager.Instance.GetDefinition(hl) : null };
            ed.Options.ConvertTabsToSpaces = true; ed.Options.EnableHyperlinks = false;
            var dirty = new TextBlock { Name = "ca-dirty", Foreground = K4Warn, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0), Text = _caDoc.Text != _caSaved ? "● ungespeichert" : "" };
            ed.TextChanged += (_, _) => dirty.Text = _caDoc.Text != _caSaved ? "● ungespeichert" : "";
            var save = new Button { Name = "ca-save-file", Content = "Speichern", Padding = new Thickness(10, 4) };
            save.Click += (_, _) => ProgramRun(CodeAiKey, () => CodeAiSaveFile(d));
            center.Children.Add(KrCard(new StackPanel { Spacing = 6, Children = {
                new DockPanel { Children = { new Border { [DockPanel.DockProperty] = Avalonia.Controls.Dock.Right, Child = save }, new StackPanel { Orientation = Orientation.Horizontal, Children = { H(Path.GetRelativePath(root, _caDocFile), 13), new TextBlock { Text = " · " + lang.Label, Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }, dirty } } } },
                new Border { CornerRadius = new CornerRadius(6), ClipToBounds = true, Child = ed } } }, "ca-editor-card"));
        }
        else center.Children.Add(KrCard(P("Wähle links eine Datei, um sie anzusehen und zu bearbeiten.", Muted, 12)));

        // Tests
        var kinds = CodeAiKit.DetectProject(root); var kind = CaKind(d);
        var tests = new StackPanel { Spacing = 6 };
        tests.Children.Add(H("Tests", 14));
        if (kinds.Count == 0) tests.Children.Add(P("Keine bekannte Projektart erkannt (.NET, npm, Python, Cargo, Go).", Muted, 12));
        else
        {
            var chips = new WrapPanel();
            foreach (var k in kinds)
            {
                var kk = k; var on = kind?.Id == k.Id;
                var c = new Button { Name = "ca-kind-" + k.Id, Content = $"{k.Label} · {k.Marker}", Padding = new Thickness(10, 3), Margin = new Thickness(0, 0, 6, 6), FontSize = 12 };
                if (on) c.Classes.Add("accent");
                c.Click += (_, _) => ProgramRun(CodeAiKey, () => { d.Set("kind", kk.Id, "Projektart"); _caAsk = false; });
                chips.Children.Add(c);
            }
            tests.Children.Add(chips);
            var cmd = kind is null ? null : CodeAiKit.TestCommand(root, kind.Id);
            tests.Children.Add(K4Mono("$ " + (cmd?.Display ?? "–"), Muted, "ca-command"));
            var run = new Button { Name = "ca-test-run", Content = _caBusy ? "Läuft …" : _caAsk ? "Ja, jetzt ausführen" : "Tests ausführen …", IsEnabled = !_caBusy && cmd is not null, Padding = new Thickness(14, 6) };
            if (_caAsk) run.Classes.Add("accent");
            run.Click += (_, _) => CaTestClick(d);
            tests.Children.Add(run);
        }
        if (_caOut.Length > 0)
            tests.Children.Add(new Border { Background = Brush.Parse("#0E1117"), CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 8),
                Child = new ScrollViewer { MaxHeight = 260, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                    Content = new SelectableTextBlock { Name = "ca-output", Text = _caOut, FontFamily = new FontFamily("Cascadia Code, Consolas, Menlo, monospace"), FontSize = 12,
                        Foreground = _caOut.StartsWith('✓') ? K4Good : _caOut.StartsWith('✕') ? K4Bad : Text } } });
        center.Children.Add(KrCard(tests, "ca-tests"));

        // Schnappschuss und Vergleich
        var snap = CaSnapshot(d);
        var cmp = new StackPanel { Spacing = 6 };
        cmp.Children.Add(H("Änderungen", 14));
        cmp.Children.Add(P(snap is null ? "Noch kein Schnappschuss. „Schnappschuss“ merkt sich jetzt alle Dateien." : $"Schnappschuss vom {snap.TakenUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm", De)} mit {snap.Files.Count} Dateien.", Muted, 12));
        var row = new WrapPanel();
        var snapBtn = new Button { Name = "ca-snap-now", Content = snap is null ? "Schnappschuss" : "Neuer Schnappschuss", Padding = new Thickness(10, 4), Margin = new Thickness(0, 0, 6, 6) };
        snapBtn.Click += (_, _) => ProgramRun(CodeAiKey, () => CodeAiSnapshot(d));
        row.Children.Add(snapBtn);
        if (snap is not null)
        {
            var chk = new Button { Name = "ca-changes-now", Content = "Seit Schnappschuss geändert", Padding = new Thickness(10, 4), Margin = new Thickness(0, 0, 6, 6) };
            chk.Click += (_, _) => ProgramRun(CodeAiKey, () => CodeAiChanges(d));
            row.Children.Add(chk);
        }
        cmp.Children.Add(row);
        if (_caChanges is { Count: > 0 } changes)
        {
            var list = new WrapPanel { Name = "ca-changes" };
            var n = 0;
            foreach (var c in changes.Take(300))
            {
                var cc = c; var label = c.Kind switch { CodeAiKit.ChangeKind.Neu => "neu", CodeAiKit.ChangeKind.Geloescht => "gelöscht", _ => "geändert" };
                var b = new Button { Name = "ca-change-" + n++, Content = $"{c.Path} · {label}", FontSize = 12, Padding = new Thickness(8, 3), Margin = new Thickness(0, 0, 6, 6) };
                if (c.Path == _caDiffPath) b.Classes.Add("accent");
                b.Click += (_, _) => { _caDiffPath = cc.Path; Show(CodeAiKey); };
                list.Children.Add(b);
            }
            cmp.Children.Add(list);
            if (_caDiffPath is { } dp)
            {
                var view = new StackPanel { Name = "ca-diff" };
                try
                {
                    var diff = CodeAiDiff(d, dp); var (add, rem) = CodeAiKit.Count(diff);
                    cmp.Children.Add(P($"{dp}: +{add} −{rem} Zeilen", Muted, 12));
                    foreach (var l in diff.Where((l, k) => l.Op != CodeAiKit.Op.Gleich || diff.Skip(Math.Max(0, k - 3)).Take(7).Any(x => x.Op != CodeAiKit.Op.Gleich)).Take(1500))
                        view.Children.Add(new Border { Background = l.Op switch { CodeAiKit.Op.Neu => Brush.Parse("#1A3D2A"), CodeAiKit.Op.Weg => Brush.Parse("#4A1F24"), _ => Brushes.Transparent },
                            Child = K4Mono($"{(l.Old > 0 ? l.Old.ToString(CultureInfo.InvariantCulture) : ""),5} {(l.New > 0 ? l.New.ToString(CultureInfo.InvariantCulture) : ""),5} {(l.Op switch { CodeAiKit.Op.Neu => '+', CodeAiKit.Op.Weg => '-', _ => ' ' })} {l.Text}") });
                }
                catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException) { view.Children.Add(P(ex.Message, Muted, 12)); }
                cmp.Children.Add(new Border { Background = Brush.Parse("#0E1117"), CornerRadius = new CornerRadius(6), Padding = new Thickness(6), Child = new ScrollViewer { MaxHeight = 420, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, Content = view } });
            }
        }
        center.Children.Add(KrCard(cmp, "ca-compare"));

        sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("260,12,*"), Children = { left, Pos(center, 2) } });
        return sp;
    }

    Control CodeAiProperties(ProgramDocument d)
    {
        var sp = new StackPanel { Spacing = 4 };
        sp.Children.Add(P("Was soll Claude ändern?", null, 11));
        var wish = KrBox(d, "wish", "ca-p-wish", "z. B. „Teilen durch null soll eine verständliche Meldung geben“", true); wish.MinHeight = 80;
        sp.Children.Add(wish);
        sp.Children.Add(P("Zeitlimit für Tests (Sekunden)", null, 11));
        sp.Children.Add(KrBox(d, "timeout", "ca-p-timeout", "300"));
        sp.Children.Add(H("Sicherheit", 13));
        sp.Children.Add(P("Trendo führt Befehle nur nach deinem zweiten Klick aus, im Projektordner und mit Zeitlimit. Claude arbeitet mit deinem eigenen Zugang. Geheimnisse (Passwörter, Schlüssel) gehören nicht in Dateien, die du an Claude gibst.", Muted, 11));
        return sp;
    }

    // ======================================================================
    // Bilder erkennen
    // ======================================================================

    /// <summary>Tests: Pfad zu Tesseract oder null (= nicht installiert), statt im Werkzeugordner zu suchen.</summary>
    public static Func<string?>? VisionTesseractOverride { get; set; }

    string? _viFor; VisionKit.Report? _viReport; List<VisionKit.Report>? _viFolder; int _viSkipped; bool _viBusy;
    public const long VisionMaxBytes = 60L * 1024 * 1024, VisionMaxPixels = 100_000_000;
    static readonly string[] VisionExt = [".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tif", ".tiff"];

    static JsonObject VisionDoc(string image = "") => new() { ["image"] = image, ["folder"] = "", ["limit"] = "100", ["text"] = "" };
    static double ViLimit(ProgramDocument d) => double.TryParse(d.Text("limit").Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var l) && l is > 0 and < 100000 ? l : VisionKit.DefaultSharpLimit;

    void ViState(ProgramDocument d)
    {
        if (_viFor == d.Id) return;
        _viFor = d.Id; _viReport = null; _viFolder = null; _viSkipped = 0;
    }

    /// <summary>Bild lesen und prüfen: höchstens 60 MB, zum Messen auf 1024 Bildpunkte verkleinert (gleiche Grundlage für Schärfe in jeder Größe).</summary>
    public static VisionKit.Report VisionAnalyze(string file, double limit = VisionKit.DefaultSharpLimit)
    {
        if (!KreativSafe.LocalFile(file, VisionMaxBytes)) throw new InvalidOperationException("Wähle ein Bild auf diesem Rechner (höchstens 60 MB).");
        var bytes = File.ReadAllBytes(file);
        // Bildgröße vor dem Dekodieren prüfen: eine kleine Datei kann riesige Bildmaße haben
        using (var codec = SKCodec.Create(new MemoryStream(bytes)))
            if (codec is not null && (long)codec.Info.Width * codec.Info.Height > VisionMaxPixels) throw new InvalidDataException($"„{Path.GetFileName(file)}“ ist mit {codec.Info.Width} × {codec.Info.Height} Bildpunkten zu groß (höchstens 100 Megapixel).");
        using var src = SKBitmap.Decode(bytes) ?? throw new InvalidDataException($"„{Path.GetFileName(file)}“ lässt sich nicht als Bild lesen ({VisionKit.Format(bytes)}).");
        int ow = src.Width, oh = src.Height; var s = Math.Min(1.0, 1024.0 / Math.Max(ow, oh));
        int w = Math.Max(1, (int)Math.Round(ow * s)), h = Math.Max(1, (int)Math.Round(oh * s));
        using var small = s < 1 ? src.Resize(new SKImageInfo(w, h), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear)) ?? throw new InvalidDataException("Bild ließ sich nicht verkleinern.") : null;
        using var rgba = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        using (var cv = new SKCanvas(rgba)) { cv.Clear(SKColors.Transparent); cv.DrawBitmap(small ?? src, 0, 0); }
        return VisionKit.Analyze(file, bytes, bytes.LongLength, VisionKit.Exif(bytes), rgba.Bytes, w, h, ow, oh, limit);
    }

    public async Task VisionOpen(ProgramDocument d, string file)
    {
        ViState(d);
        if (!KreativSafe.LocalFile(file, VisionMaxBytes)) { _progMsg[VisionKey] = "Wähle ein Bild auf diesem Rechner (höchstens 60 MB)."; Show(VisionKey); return; }
        d.Edit("Bild geöffnet", x => { x["image"] = file; x["text"] = ""; });
        if (d.Title is "Unbenannt" or "") d.Title = Path.GetFileNameWithoutExtension(file);
        _viBusy = true; Show(VisionKey);
        try
        {
            var limit = ViLimit(d);
            _viReport = await Task.Run(() => VisionAnalyze(file, limit));
            _progMsg[VisionKey] = $"{Path.GetFileName(file)}: {_viReport.Light.Verdict}, {_viReport.Sharp.Verdict}.";
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or ArgumentException) { _viReport = null; _progMsg[VisionKey] = ex.Message; }
        finally { _viBusy = false; Show(VisionKey); }
    }

    /// <summary>Ordner prüfen (mit Unterordnern, höchstens 2000 Bilder); unlesbare Dateien werden gezählt und übersprungen.</summary>
    public async Task<int> VisionScanFolder(ProgramDocument d, string dir)
    {
        ViState(d);
        if (!KreativSafe.LocalDir(dir)) { _progMsg[VisionKey] = "Wähle einen Bildordner auf diesem Rechner."; Show(VisionKey); return 0; }
        if (_viBusy) return 0;
        d.Set("folder", dir, "Ordner");
        _viBusy = true; _progMsg[VisionKey] = "Bilder werden geprüft …"; Show(VisionKey);
        try
        {
            var limit = ViLimit(d);
            var (list, skipped) = await Task.Run(() =>
            {
                var res = new List<VisionKit.Report>(); var skip = 0;
                var files = Directory.EnumerateFiles(dir, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true })
                    .Where(f => VisionExt.Contains(Path.GetExtension(f).ToLowerInvariant())).Order(StringComparer.OrdinalIgnoreCase).Take(2000);
                foreach (var f in files)
                    try { res.Add(VisionAnalyze(f, limit)); }
                    catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or ArgumentException) { skip++; }
                return (res, skip);
            });
            _viFolder = list; _viSkipped = skipped;
            var bad = list.Count(VisionKit.Problem);
            _progMsg[VisionKey] = $"{list.Count} Bilder geprüft, {bad} auffällig" + (skipped > 0 ? $", {skipped} nicht lesbar" : "") + ".";
            return list.Count;
        }
        finally { _viBusy = false; Show(VisionKey); }
    }

    string? VisionTesseract() => VisionTesseractOverride is { } o ? o() : _opt.Isolated ? null : ToolPath("tesseract");

    /// <summary>Text im Bild mit Tesseract, ohne Internet: Deutsch, wenn das Sprachpaket da ist, sonst Englisch. Ohne Tesseract ein klarer Hinweis.</summary>
    public async Task VisionOcr(ProgramDocument d)
    {
        ViState(d);
        var img = d.Text("image");
        if (!KreativSafe.LocalFile(img, VisionMaxBytes)) { _progMsg[VisionKey] = "Öffne zuerst ein Bild."; Show(VisionKey); return; }
        if (VisionTesseract() is not { } tess)
        {
            _progMsg[VisionKey] = "⚠ Für Text im Bild fehlt die Texterkennung (Tesseract). Einrichten: oben „Texterkennung einrichten“ → Zusatzprogramme → Tesseract OCR (kostenlos, läuft ohne Internet).";
            Show(VisionKey); return;
        }
        _viBusy = true; _progMsg[VisionKey] = "Text wird erkannt …"; Show(VisionKey);
        try
        {
            var run = new RealProcessRunner(); string? data = null; var lang = "eng";
            if (Ocr.HasUserGerman(_core.DataRoot)) { data = Ocr.UserTessdata(_core.DataRoot); lang = "deu"; }
            else if ((await Ocr.LanguagesAsync(run, tess)).Contains("deu")) lang = "deu";
            var r = await Ocr.RunAsync(run, tess, img, default, data, lang);
            if (!r.Ok) { _progMsg[VisionKey] = r.Message; return; }
            d.Set("text", r.Text.Trim(), "Text erkannt");
            _progMsg[VisionKey] = r.Text.Trim().Length == 0 ? "Im Bild wurde kein Text erkannt." : $"Text erkannt ({(lang == "deu" ? "Deutsch" : "Englisch")}).";
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { _progMsg[VisionKey] = "Texterkennung fehlgeschlagen: " + ex.Message; }
        finally { _viBusy = false; Show(VisionKey); }
    }

    List<VisionKit.Report> ViAll(ProgramDocument d)
    {
        ViState(d);
        var all = new List<VisionKit.Report>();
        if (_viReport is { } r) all.Add(r with { Text = d.Text("text") });
        if (_viFolder is { } f) all.AddRange(f.Where(x => !string.Equals(x.File, _viReport?.File, StringComparison.OrdinalIgnoreCase)));
        if (all.Count == 0) throw new InvalidOperationException("Öffne zuerst ein Bild oder prüfe einen Ordner.");
        return all;
    }

    /// <summary>Beispiel: ein selbst gezeichnetes Testbild in den Projekten (vorhandene Dateien bleiben).</summary>
    string ViSample()
    {
        var dir = WorkFolder("Bilder erkennen"); var f = Path.Combine(dir, "beispiel-testbild.png");
        if (File.Exists(f)) return f;
        using var bmp = new SKBitmap(640, 400);
        using (var c = new SKCanvas(bmp))
        {
            c.Clear(new SKColor(0x2E, 0x5E, 0x8C));
            using var sand = new SKPaint { Color = new SKColor(0xD9, 0xB8, 0x6C), IsAntialias = true };
            c.DrawRect(0, 260, 640, 140, sand);
            using var sun = new SKPaint { Color = new SKColor(0xF5, 0xC5, 0x42), IsAntialias = true };
            c.DrawCircle(500, 110, 56, sun);
            using var house = new SKPaint { Color = new SKColor(0xF4, 0xF1, 0xEA), IsAntialias = true };
            c.DrawRect(120, 170, 150, 110, house);
            using var roof = new SKPaint { Color = new SKColor(0xA6, 0x3D, 0x32), IsAntialias = true };
            using var path = new SKPath(); path.MoveTo(105, 172); path.LineTo(195, 110); path.LineTo(285, 172); path.Close(); c.DrawPath(path, roof);
        }
        using var img = SKImage.FromBitmap(bmp); using var data = img.Encode(SKEncodedImageFormat.Png, 90);
        using (var fs = new FileStream(f, FileMode.CreateNew)) data.SaveTo(fs);
        return f;
    }

    private void InitVision() => RegisterProgram(FachTabs, new ProgramSpec(VisionKey, "STUDIO-VISION-01", "Bilder erkennen")
    {
        Sentence = "Bilder ohne Internet prüfen: Größe, Aufnahmedaten, Hauptfarben, Helligkeit, Schärfe und Text im Bild – auch ganze Ordner.",
        Folder = "Bilder erkennen",
        ScrollWorkspace = true,
        Templates =
        [
            new("leer", "Bild prüfen", "Ein Bild öffnen", () => VisionDoc()),
            new("beispiel", "Beispiel: Testbild", "Ein gezeichnetes Bild zum Ausprobieren", () => { try { return VisionDoc(ViSample()); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return VisionDoc(); } }),
        ],
        Ribbon =
        [
            ("Bild", [
                new ProgramCmd("vi-open", "Bild öffnen …", d => _ = ViPick(d), Primary: true),
                new ProgramCmd("vi-folder", "Ordner prüfen …", d => _ = ViPickFolder(d), Tip: "Findet unscharfe, zu dunkle und zu helle Fotos."),
            ]),
            ("Text", [
                new ProgramCmd("vi-ocr", "Text im Bild erkennen", d => _ = VisionOcr(d), Tip: "Mit Tesseract, ohne Internet."),
                new ProgramCmd("vi-ocr-setup", "Texterkennung einrichten", d => Show("components"), Tip: "Öffnet die Zusatzprogramme."),
            ]),
            ("Claude", [
                new ProgramCmd("vi-claude", "Mit Claude beschreiben", d => ViClaude(d), Tip: "Hängt das Bild an und fragt Claude (dein Zugang)."),
            ]),
        ],
        Exports =
        [
            new("md", "Bildbericht (.md)", ".md", (d, f) => { File.WriteAllText(f, VisionKit.Markdown(ViAll(d), "Bildbericht")); return f; }),
            new("csv", "Liste (.csv)", ".csv", (d, f) => { File.WriteAllText(f, VisionKit.Csv(ViAll(d)), new UTF8Encoding(true)); return f; }),
        ],
        Workspace = VisionWorkspace,
        Properties = VisionProperties,
        Status = d => (_viReport is { } r && _viFor == d.Id ? $"{Path.GetFileName(r.File)} · {r.Light.Verdict} · {r.Sharp.Verdict}" : "Noch kein Bild") + (_viFolder is { } f && _viFor == d.Id ? $" · Ordner: {f.Count} Bilder, {f.Count(VisionKit.Problem)} auffällig" : ""),
        Help = "„Bild öffnen“ zeigt Größe, Aufnahmedatum und Kamera (EXIF), Hauptfarben, Helligkeit und Schärfe. „Ordner prüfen“ listet unscharfe und falsch belichtete Fotos. Text im Bild liest Tesseract, wenn es eingerichtet ist. Alles bleibt auf deinem Rechner; nur „Mit Claude beschreiben“ gibt das Bild an deinen Claude-Zugang.",
    }, "bilder erkennen", "bild analysieren", "bilderkennung", "exif", "unscharfe fotos", "schärfe prüfen", "hauptfarben", "ocr", "text im bild", "texterkennung", "bild beschreiben");

    async Task ViPick(ProgramDocument d)
    {
        if (await PickMediaFile("vi-bild", "Bild", ["*.jpg", "*.jpeg", "*.png", "*.bmp", "*.gif", "*.webp", "*.tif", "*.tiff"]) is { } f) await VisionOpen(d, f);
    }

    async Task ViPickFolder(ProgramDocument d)
    {
        if (await PickFolderPath("Bildordner prüfen") is { } f) await VisionScanFolder(d, f);
    }

    void ViClaude(ProgramDocument d)
    {
        var img = d.Text("image");
        // nur ein geprüftes Bild anhängen – eine fremde Projektdatei darf keine andere Datei zu Claude schicken
        if (!KreativSafe.LocalFile(img, VisionMaxBytes) || !VisionExt.Contains(Path.GetExtension(img).ToLowerInvariant()) || !string.Equals(_viReport?.File, img, StringComparison.OrdinalIgnoreCase)) { _progMsg[VisionKey] = "Öffne zuerst ein Bild."; Show(VisionKey); return; }
        var facts = _viReport is { } r ? $" Gemessen: {r.Width} × {r.Height}, Helligkeit {r.Light.Mean.ToString("0", De)}/255 ({r.Light.Verdict}), Schärfe {r.Sharp.Verdict}." : "";
        _ = KrClaude(VisionKey, "STUDIO-VISION-01", $"Beschreibe das angehängte Bild „{Path.GetFileName(img)}“ sachlich auf Deutsch: was zu sehen ist, Licht, Farben, Schärfe, Text im Bild und einen Vorschlag zur Verbesserung. Erkenne keine Personen und rate keine Namen.{facts}", [img]);
    }

    Control VisionWorkspace(ProgramDocument d)
    {
        ViState(d);
        var img = d.Text("image");
        if (_viReport is null && !_viBusy && KreativSafe.LocalFile(img, VisionMaxBytes))
            try { _viReport = VisionAnalyze(img, ViLimit(d)); } catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or ArgumentException) { _progMsg[VisionKey] = ex.Message; }
        var sp = new StackPanel { Name = "vi-workspace", Spacing = 12, Margin = new Thickness(4, 0, 10, 4) };
        var open = new Button { Name = "vi-open-big", Content = _viBusy ? "Prüft …" : "Bild öffnen …", Padding = new Thickness(14, 6), IsEnabled = !_viBusy }; open.Classes.Add("accent");
        open.Click += (_, _) => _ = ViPick(d);
        var folder = new Button { Name = "vi-folder-big", Content = "Ordner prüfen …", Padding = new Thickness(14, 6), Margin = new Thickness(8, 0, 0, 0), IsEnabled = !_viBusy };
        folder.Click += (_, _) => _ = ViPickFolder(d);
        sp.Children.Add(new WrapPanel { Children = { open, folder } });

        if (_viReport is { } r)
        {
            var pic = new Image { Name = "vi-preview", Stretch = Stretch.Uniform, MaxHeight = 380 };
            try { using var fs = File.OpenRead(r.File); pic.Source = Bitmap.DecodeToWidth(fs, 900); }
            catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or UnauthorizedAccessException) { pic.IsVisible = false; }
            var facts = new StackPanel { Name = "vi-facts", Spacing = 5 };
            void Row(string k, string v, IBrush? c = null) => facts.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("110,*"), Children = { new TextBlock { Text = k, Foreground = Muted, FontSize = 12 }, Pos(new TextBlock { Text = v, Foreground = c ?? Text, FontSize = 12, TextWrapping = TextWrapping.Wrap }, 1) } });
            facts.Children.Add(H(Path.GetFileName(r.File), 14));
            Row("Format", $"{r.Format}, {r.Width} × {r.Height} Bildpunkte");
            Row("Dateigröße", (r.Bytes / 1024.0).ToString("N0", De) + " KB");
            if (r.Exif is { } e)
            {
                if (e.Taken is { } t) Row("Aufgenommen", t.ToString("dd.MM.yyyy HH:mm", De));
                if (e.Camera.Length > 0) Row("Kamera", e.Camera);
                var set = string.Join(", ", new[] { e.Exposure, e.FNumber, e.Iso is { } iso ? "ISO " + iso : "", e.Focal }.Where(x => x.Length > 0));
                if (set.Length > 0) Row("Einstellung", set);
            }
            else Row("EXIF", "keine Aufnahmedaten");
            Row("Helligkeit", $"{(r.Light.Verdict == VisionKit.Ok ? "✓" : "⚠")} {r.Light.Mean.ToString("0", De)} von 255 – {r.Light.Verdict}", r.Light.Verdict == VisionKit.Ok ? K4Good : K4Warn);
            facts.Children.Add(new ProgressBar { Name = "vi-light", Minimum = 0, Maximum = 255, Value = r.Light.Mean, Height = 6 });
            Row("Schärfe", $"{(r.Sharp.Verdict == VisionKit.SharpOk ? "✓" : "⚠")} {r.Sharp.Variance.ToString("N0", De)} – {r.Sharp.Verdict}", r.Sharp.Verdict == VisionKit.SharpOk ? K4Good : K4Warn);
            var colors = new WrapPanel { Name = "vi-colors" };
            foreach (var c in r.Colors)
                colors.Children.Add(new StackPanel { Margin = new Thickness(0, 4, 8, 0), Children = {
                    new Border { Width = 44, Height = 28, CornerRadius = new CornerRadius(4), Background = Brush.Parse(c.Hex), BorderBrush = Brush.Parse("#33FFFFFF"), BorderThickness = new Thickness(1) },
                    new TextBlock { Text = c.Hex, FontSize = 10, Foreground = Muted }, new TextBlock { Text = (c.Share * 100).ToString("0", De) + " %", FontSize = 10, Foreground = Muted } } });
            facts.Children.Add(P("Hauptfarben", Muted, 12)); facts.Children.Add(colors);
            var ocr = new Button { Name = "vi-ocr-now", Content = "Text im Bild erkennen", Padding = new Thickness(10, 4), Margin = new Thickness(0, 8, 0, 0), IsEnabled = !_viBusy };
            ocr.Click += (_, _) => _ = VisionOcr(d);
            facts.Children.Add(ocr);
            if (d.Text("text").Length > 0) facts.Children.Add(new TextBox { Name = "vi-text", Text = d.Text("text"), IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 200 });
            sp.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,14,340"), Children = { new Border { CornerRadius = new CornerRadius(8), ClipToBounds = true, Background = Brush.Parse("#0A0F1A"), Child = pic }, Pos(KrCard(facts, "vi-facts-card"), 2) } });
        }
        else if (!_viBusy) sp.Children.Add(KrCard(P("Öffne ein Bild oder prüfe einen ganzen Ordner. Alles läuft auf deinem Rechner, ohne Internet.", Muted, 12)));

        if (_viFolder is { } list)
        {
            var box = new StackPanel { Name = "vi-folder-list", Spacing = 3 };
            var bad = list.Where(VisionKit.Problem).ToList();
            box.Children.Add(H($"{list.Count} Bilder geprüft – {bad.Count} auffällig" + (_viSkipped > 0 ? $" ({_viSkipped} nicht lesbar)" : ""), 14));
            var root = d.Text("folder");
            foreach (var x in bad.Concat(list.Where(l => !VisionKit.Problem(l))).Take(400))
            {
                var xx = x; var problem = VisionKit.Problem(x);
                var why = string.Join(", ", new[] { x.Sharp.Verdict == VisionKit.Blurry ? "unscharf" : "", x.Light.Verdict != VisionKit.Ok ? x.Light.Verdict : "" }.Where(s => s.Length > 0));
                var b = new Button { Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(4, 2), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
                    Content = new TextBlock { Text = (problem ? "⚠ " : "✓ ") + (KreativSafe.LocalDir(root) ? Path.GetRelativePath(root, x.File) : Path.GetFileName(x.File)) + (why.Length > 0 ? " – " + why : ""), Foreground = problem ? K4Warn : Muted, FontSize = 12 } };
                b.Click += (_, _) => _ = VisionOpen(d, xx.File);
                box.Children.Add(b);
            }
            sp.Children.Add(KrCard(box, "vi-folder-card"));
        }
        return sp;
    }

    Control VisionProperties(ProgramDocument d)
    {
        var sp = new StackPanel { Spacing = 4 };
        sp.Children.Add(P("Grenze für „unscharf“ (Laplace-Varianz)", null, 11));
        sp.Children.Add(KrBox(d, "limit", "vi-p-limit", "100"));
        sp.Children.Add(P("Kleiner = strenger. Typisch: unter 100 wirkt ein Foto weich; Nachtaufnahmen und Nebel liegen oft darunter, ohne verwackelt zu sein.", Muted, 11));
        sp.Children.Add(H("Datenschutz", 13));
        sp.Children.Add(P("Trendo erkennt keine Gesichter oder Personen. Messwerte entstehen nur auf diesem Rechner.", Muted, 11));
        return sp;
    }

    // ======================================================================
    // Recherche mit Quellen
    // ======================================================================

    string? _ciFor, _ciSel; bool _ciDups;

    static readonly (string Key, string Label, string[] Kinds)[] CiFields =
    [
        ("authors", "Autorin / Autor (Nachname, Vorname; …) – Einrichtung in {…}", ["buch", "artikel", "webseite"]),
        ("title", "Titel", ["buch", "artikel", "webseite"]),
        ("year", "Jahr", ["buch", "artikel", "webseite"]),
        ("publisher", "Verlag", ["buch"]),
        ("place", "Ort", ["buch"]),
        ("edition", "Auflage", ["buch"]),
        ("journal", "Zeitschrift", ["artikel"]),
        ("volume", "Jahrgang (Band)", ["artikel"]),
        ("issue", "Heft", ["artikel"]),
        ("pages", "Seiten (z. B. 45–67)", ["artikel", "buch"]),
        ("journal", "Name der Webseite", ["webseite"]),
        ("url", "Adresse (URL)", ["webseite", "artikel", "buch"]),
        ("doi", "DOI", ["artikel", "buch"]),
        ("accessed", "Abgerufen am (TT.MM.JJJJ)", ["webseite"]),
    ];

    static CiteKit.Source CiSource(JsonObject o) => new()
    {
        Id = Js(o["id"]), Kind = CiteKit.Kinds.Any(k => k.Id == Js(o["kind"])) ? Js(o["kind"]) : "buch", Authors = Js(o["authors"]), Title = Js(o["title"]), Year = Js(o["year"]),
        Publisher = Js(o["publisher"]), Place = Js(o["place"]), Edition = Js(o["edition"]), Journal = Js(o["journal"]), Volume = Js(o["volume"]), Issue = Js(o["issue"]),
        Pages = Js(o["pages"]), Url = Js(o["url"]), Doi = Js(o["doi"]), Accessed = Js(o["accessed"]),
    };

    static JsonObject CiJson(CiteKit.Source s) => new()
    {
        ["id"] = s.Id, ["kind"] = s.Kind, ["authors"] = s.Authors, ["title"] = s.Title, ["year"] = s.Year, ["publisher"] = s.Publisher, ["place"] = s.Place, ["edition"] = s.Edition,
        ["journal"] = s.Journal, ["volume"] = s.Volume, ["issue"] = s.Issue, ["pages"] = s.Pages, ["url"] = s.Url, ["doi"] = s.Doi, ["accessed"] = s.Accessed,
    };

    public static List<CiteKit.Source> CiteSources(ProgramDocument d) => d.List("sources").OfType<JsonObject>().Select(CiSource).Where(s => s.Id.Length > 0).ToList();
    public static List<CiteKit.Quote> CiteQuotes(ProgramDocument d) =>
        d.List("quotes").OfType<JsonObject>().Select(o => new CiteKit.Quote(Js(o["id"]), Js(o["source"]), Js(o["text"]), Js(o["page"]), Js(o["note"]))).Where(q => q.Id.Length > 0).ToList();
    static string CiStyle(ProgramDocument d) => CiteKit.Styles.Any(s => s.Id == d.Text("style")) ? d.Text("style") : "apa";

    static void CiSave(ProgramDocument d, IEnumerable<CiteKit.Source> list, string label) => d.Edit(label, x => x["sources"] = new JsonArray(list.Select(s => (JsonNode)CiJson(s)).ToArray()));
    static void CiSaveQuotes(ProgramDocument d, IEnumerable<CiteKit.Quote> list, string label) =>
        d.Edit(label, x => x["quotes"] = new JsonArray(list.Select(q => (JsonNode)new JsonObject { ["id"] = q.Id, ["source"] = q.SourceId, ["text"] = q.Text, ["page"] = q.Page, ["note"] = q.Note }).ToArray()));
    static string NewId(string p) => p + Guid.NewGuid().ToString("N")[..8];

    static JsonObject CiteDoc(IEnumerable<CiteKit.Source>? sources = null, IEnumerable<CiteKit.Quote>? quotes = null) => new()
    {
        ["style"] = "apa", ["claim"] = "",
        ["sources"] = new JsonArray((sources ?? []).Select(s => (JsonNode)CiJson(s)).ToArray()),
        ["quotes"] = new JsonArray((quotes ?? []).Select(q => (JsonNode)new JsonObject { ["id"] = q.Id, ["source"] = q.SourceId, ["text"] = q.Text, ["page"] = q.Page, ["note"] = q.Note }).ToArray()),
    };

    static JsonObject CiteSample() => CiteDoc(
        [
            new() { Id = "q-buch", Kind = "buch", Authors = "Muster, Erika; Beispiel, Max", Title = "Gärten für Einsteiger", Year = "2024", Publisher = "Grünverlag", Place = "Musterstadt", Edition = "2" },
            new() { Id = "q-artikel", Kind = "artikel", Authors = "Erika Muster", Title = "Boden und Klima", Year = "2023", Journal = "Zeitschrift für Gartenbau", Volume = "12", Issue = "3", Pages = "45–67" },
            new() { Id = "q-web", Kind = "webseite", Authors = "{Gartenverband Musterstadt}", Title = "Wasser sparen im Garten", Year = "2025", Journal = "example.org", Url = "https://example.org/wasser-sparen", Accessed = "2026-11-03" },
        ],
        [new("z-1", "q-buch", "Ein guter Boden braucht vor allem Zeit und Geduld.", "12", "Für die Einleitung")]);

    void CiState(ProgramDocument d)
    {
        if (_ciFor == d.Id) return;
        _ciFor = d.Id; _ciDups = false; _ciSel = CiteSources(d).FirstOrDefault()?.Id;
    }

    public CiteKit.Source CiteAdd(ProgramDocument d, string kind)
    {
        CiState(d);
        if (!CiteKit.Kinds.Any(k => k.Id == kind)) throw new ArgumentException("Art: Buch, Artikel oder Webseite.");
        var s = new CiteKit.Source { Id = NewId("q"), Kind = kind, Accessed = kind == "webseite" ? DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "" };
        CiSave(d, CiteSources(d).Append(s), "Neue Quelle");
        _ciSel = s.Id; _progMsg[CiteKey] = $"Neue Quelle ({CiteKit.Kinds.First(k => k.Id == kind).Label}) – rechts ausfüllen.";
        return s;
    }

    public void CiteSet(ProgramDocument d, string id, string key, string value)
    {
        var list = CiteSources(d); var i = list.FindIndex(s => s.Id == id);
        if (i < 0) throw new InvalidOperationException("Diese Quelle gibt es nicht mehr.");
        var v = KreativSafe.Cut((value ?? "").Trim(), 2000);
        if (key == "accessed" && v.Length > 0 && CiteKit.Date(v) is { } dt) v = dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var o = CiJson(list[i]); if (!o.ContainsKey(key)) throw new ArgumentException("Unbekanntes Feld.");
        o[key] = v; list[i] = CiSource(o);
        CiSave(d, list, "Quelle geändert");
    }

    public void CiteRemove(ProgramDocument d, string id)
    {
        CiSave(d, CiteSources(d).Where(s => s.Id != id), "Quelle entfernt");
        CiSaveQuotes(d, CiteQuotes(d).Where(q => q.SourceId != id), "Zitate entfernt");
        if (_ciSel == id) _ciSel = CiteSources(d).FirstOrDefault()?.Id;
    }

    public CiteKit.Quote CiteAddQuote(ProgramDocument d, string sourceId, string text, string page, string note = "")
    {
        if (CiteSources(d).All(s => s.Id != sourceId)) throw new InvalidOperationException("Wähle zuerst eine Quelle.");
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Trag das Zitat ein.");
        var q = new CiteKit.Quote(NewId("z"), sourceId, KreativSafe.Cut(text.Trim(), 4000), KreativSafe.Cut((page ?? "").Trim(), 40), KreativSafe.Cut((note ?? "").Trim(), 1000));
        CiSaveQuotes(d, CiteQuotes(d).Append(q), "Zitat");
        _progMsg[CiteKey] = string.IsNullOrWhiteSpace(page) ? "⚠ Zitat gespeichert – ohne Seitenangabe." : "✓ Zitat gespeichert.";
        return q;
    }

    /// <summary>BibTeX einlesen (höchstens 5 MB). Neue Kennungen, damit nichts überschrieben wird.</summary>
    public int CiteImportBib(ProgramDocument d, string file)
    {
        if (!KreativSafe.LocalFile(file, CiteKit.MaxBibChars)) throw new InvalidOperationException("Wähle eine BibTeX-Datei auf diesem Rechner (höchstens 5 MB).");
        var (got, warn) = CiteKit.ParseBibTex(File.ReadAllText(file));
        var all = CiteSources(d); all.AddRange(got.Select(s => s with { Id = NewId("q") }));
        CiSave(d, all, "BibTeX eingelesen");
        var dups = CiteKit.Duplicates(all).Count;
        _progMsg[CiteKey] = $"{got.Count} Quellen eingelesen" + (warn.Count > 0 ? $", {warn.Count} Einträge übersprungen ({warn[0]})" : "") + (dups > 0 ? $". ⚠ {dups}× doppelt – „Doppelte suchen“ zeigt sie." : ".");
        _ciSel ??= all.FirstOrDefault()?.Id;
        return got.Count;
    }

    /// <summary>Gelesene Seiten und Markierungen aus dem Recherche-Studio (SYS-08) übernehmen: Webseiten mit Abrufdatum, Markierungen als Zitate.</summary>
    public int CiteFromResearch(ProgramDocument d)
    {
        var all = CiteSources(d); var quotes = CiteQuotes(d); var added = 0;
        var known = all.Select(s => CiteKit.NormUrl(s.Url)).Where(u => u.Length > 0).ToHashSet();
        foreach (var r in Desk.SessionSources().GroupBy(s => s.Url).Select(g => g.First()))
        {
            var norm = CiteKit.NormUrl(r.Url);
            if (known.Contains(norm)) continue;
            var host = Uri.TryCreate(r.Url, UriKind.Absolute, out var u) ? (u.Host.StartsWith("www.", StringComparison.Ordinal) ? u.Host[4..] : u.Host) : "";
            var s = new CiteKit.Source { Id = NewId("q"), Kind = "webseite", Title = r.Title, Url = r.Url, Journal = host, Accessed = r.At.LocalDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) };
            all.Add(s); known.Add(norm); added++;
            foreach (var h in Desk.Highlights(r.Url)) quotes.Add(new(NewId("z"), s.Id, h.Text, "", h.Note));
        }
        CiSave(d, all, "Aus der Recherche"); CiSaveQuotes(d, quotes, "Markierungen");
        _progMsg[CiteKey] = added == 0 ? "Im Recherche-Studio wurden in dieser Sitzung keine neuen Seiten gelesen." : $"{added} Seiten aus der Recherche übernommen – Autorin und Jahr bitte ergänzen.";
        return added;
    }

    void CiClaude(ProgramDocument d)
    {
        CiState(d);
        var s = CiteSources(d).FirstOrDefault(x => x.Id == _ciSel);
        var claim = d.Text("claim").Trim();
        if (s is null) { _progMsg[CiteKey] = "Wähle zuerst eine Quelle."; Show(CiteKey); return; }
        if (claim.Length == 0) { _progMsg[CiteKey] = "Schreib rechts unter „Aussage prüfen“, was die Quelle belegen soll."; Show(CiteKey); return; }
        var style = CiStyle(d);
        var qs = CiteQuotes(d).Where(q => q.SourceId == s.Id).Take(10).Select(q => $"- „{KreativSafe.Cut(q.Text, 800)}“" + (q.Page.Length > 0 ? $" (S. {q.Page})" : "")).ToList();
        _ = KrClaude(CiteKey, "STUDIO-CITE-01",
            $"Belegt diese Quelle die Aussage? Antworte mit „belegt“, „teilweise belegt“ oder „nicht belegt“ und begründe kurz. Erfinde nichts: Wenn du die Quelle nicht kennst oder nicht lesen kannst, sag das.\n\nAussage: {KreativSafe.Cut(claim, 2000)}\nQuelle: {CiteKit.Plain(CiteKit.Format(s, style))}" + (qs.Count > 0 ? "\nZitate daraus:\n" + string.Join("\n", qs) : ""));
    }

    async Task CiPickBib(ProgramDocument d)
    {
        if (await PickMediaFile("cite-bib", "BibTeX-Datei", ["*.bib", "*.bibtex", "*.txt"]) is { } f) ProgramRun(CiteKey, () => CiteImportBib(d, f));
    }

    private void InitCite() => RegisterProgram(FachTabs, new ProgramSpec(CiteKey, "STUDIO-CITE-01", "Recherche mit Quellen")
    {
        Sentence = "Quellen sammeln, Zitate mit Seite festhalten und sauber zitieren – APA 7, Harvard oder DIN ISO 690, als Literaturverzeichnis oder BibTeX.",
        Folder = "Quellen",
        ScrollWorkspace = true,
        Templates =
        [
            new("leer", "Leere Quellenliste", "Quellen anlegen oder einlesen", () => CiteDoc()),
            new("beispiel", "Beispiel: Hausarbeit Garten", "Buch, Artikel und Webseite mit Zitat", CiteSample),
        ],
        Ribbon =
        [
            ("Quelle", [
                new ProgramCmd("cite-add-buch", "Buch", d => CiteAdd(d, "buch"), Primary: true),
                new ProgramCmd("cite-add-artikel", "Artikel", d => CiteAdd(d, "artikel")),
                new ProgramCmd("cite-add-webseite", "Webseite", d => CiteAdd(d, "webseite")),
                new ProgramCmd("cite-research", "Aus der Recherche übernehmen", d => CiteFromResearch(d), Tip: "Gelesene Seiten und Markierungen aus „Sicher recherchieren“."),
                new ProgramCmd("cite-import", "BibTeX einlesen …", d => _ = CiPickBib(d)),
            ]),
            ("Stil", CiteKit.Styles.Select((s, i) => new ProgramCmd("cite-style-" + s.Id, s.Label, d => d.Set("style", s.Id, "Stil " + s.Label), Primary: i == 0)).ToList()),
            ("Prüfen", [
                new ProgramCmd("cite-dups", "Doppelte suchen", d => { _ciDups = true; var n = CiteKit.Duplicates(CiteSources(d)).Count; _progMsg[CiteKey] = n == 0 ? "✓ Keine doppelten Quellen." : $"⚠ {n}× doppelt – oben markiert."; }),
                new ProgramCmd("cite-claude", "Mit Claude prüfen", d => CiClaude(d), Tip: "Belegt die Quelle die Aussage? Claude mit deinem Zugang."),
                new ProgramCmd("cite-read", "Im Recherche-Studio lesen", d => { var u = CiteSources(d).FirstOrDefault(x => x.Id == _ciSel)?.Url ?? ""; if (!Uri.TryCreate(u, UriKind.Absolute, out _)) throw new InvalidOperationException("Die gewählte Quelle hat keine Adresse."); _ = ReadUrlAsync(u); Show("recherche"); }, Tip: "Öffnet die Adresse im Lesemodus von „Sicher recherchieren“."),
            ]),
        ],
        Exports =
        [
            new("md", "Literaturverzeichnis (.md)", ".md", (d, f) => { File.WriteAllText(f, CiteKit.Markdown(CiNeed(d), CiteQuotes(d), CiStyle(d))); return f; }),
            new("bib", "BibTeX (.bib)", ".bib", (d, f) => { File.WriteAllText(f, CiteKit.BibTex(CiNeed(d)), new UTF8Encoding(false)); return f; }),
            new("txt", "Nur Text (.txt)", ".txt", (d, f) => { File.WriteAllText(f, string.Join("\n\n", CiteKit.Bibliography(CiNeed(d), CiStyle(d)).Select(CiteKit.Plain)) + "\n"); return f; }),
        ],
        Workspace = CiteWorkspace,
        Properties = CiteProperties,
        Status = d => { var s = CiteSources(d); var style = CiStyle(d); var miss = s.Count(x => CiteKit.Missing(x, style).Count > 0); return $"{s.Count} Quellen · {CiteQuotes(d).Count} Zitate · {CiteKit.Styles.First(x => x.Id == style).Label}" + (miss > 0 ? $" · ⚠ {miss} unvollständig" : ""); },
        Help = "Oben eine Quelle anlegen (Buch, Artikel, Webseite), rechts die Angaben eintragen und Zitate mit Seite sammeln. Der Stil gilt für alle Quellen; fehlende Angaben stehen mit ⚠ an der Quelle. Webseiten aus „Sicher recherchieren“ übernimmst du mit einem Klick. Export als Literaturverzeichnis oder BibTeX für LaTeX und Zotero.",
    }, "quellen", "zitieren", "literaturverzeichnis", "bibliografie", "apa", "harvard", "din iso 690", "bibtex", "zotero", "hausarbeit", "quellenangabe", "fußnote");

    static List<CiteKit.Source> CiNeed(ProgramDocument d) { var s = CiteSources(d); return s.Count > 0 ? s : throw new InvalidOperationException("Noch keine Quellen."); }

    /// <summary>Markdown mit *kursiv* als Text mit kursiven Stellen.</summary>
    static SelectableTextBlock CiRich(string md, string? name = null)
    {
        var t = new SelectableTextBlock { Name = name, TextWrapping = TextWrapping.Wrap, Foreground = Text, FontSize = 13, Tag = CiteKit.Plain(md) };
        var ic = new InlineCollection(); var parts = md.Split('*');
        for (var i = 0; i < parts.Length; i++) if (parts[i].Length > 0) ic.Add(new Run(parts[i]) { FontStyle = i % 2 == 1 ? FontStyle.Italic : FontStyle.Normal });
        t.Inlines = ic;
        return t;
    }

    Control CiteWorkspace(ProgramDocument d)
    {
        CiState(d);
        var sources = CiteSources(d); var quotes = CiteQuotes(d); var style = CiStyle(d);
        if (_ciSel is null || sources.All(s => s.Id != _ciSel)) _ciSel = sources.FirstOrDefault()?.Id;
        var dupOf = new Dictionary<string, List<string>>();
        if (_ciDups) foreach (var g in CiteKit.Duplicates(sources)) foreach (var s in g) dupOf[s.Id] = g.Where(x => x.Id != s.Id).Select(x => x.Title.Length > 0 ? x.Title : "ohne Titel").ToList();

        var left = new StackPanel { Name = "cite-list", Spacing = 8 };
        left.Children.Add(H($"Quellen ({CiteKit.Styles.First(x => x.Id == style).Label})", 14));
        if (sources.Count == 0) left.Children.Add(P("Noch keine Quellen. Oben „Buch“, „Artikel“ oder „Webseite“ – oder BibTeX einlesen.", Muted, 12));
        var n = 0;
        foreach (var s in CiteKit.Sorted(sources))
        {
            var ss = s; var on = s.Id == _ciSel; var missing = CiteKit.Missing(s, style);
            var card = new StackPanel { Spacing = 3 };
            card.Children.Add(new TextBlock { Text = CiteKit.Kinds.First(k => k.Id == s.Kind).Label + (quotes.Count(q => q.SourceId == s.Id) is var qc and > 0 ? $" · {qc} Zitat(e)" : ""), Foreground = Muted, FontSize = 11 });
            card.Children.Add(CiRich(CiteKit.Format(s, style), "cite-entry-" + n));
            if (missing.Count > 0) card.Children.Add(new TextBlock { Text = "⚠ Fehlt: " + string.Join(", ", missing), Foreground = K4Warn, FontSize = 11, TextWrapping = TextWrapping.Wrap });
            if (dupOf.TryGetValue(s.Id, out var other)) card.Children.Add(new TextBlock { Text = "⚠ Doppelt mit: " + string.Join("; ", other), Foreground = K4Warn, FontSize = 11, TextWrapping = TextWrapping.Wrap });
            var pick = new Button { Name = "cite-pick-" + n++, Content = card, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(10, 8),
                Background = on ? Brush.Parse("#2A3A55") : Brush.Parse("#151D2B"), BorderBrush = on ? Accent : Brush.Parse("#22FFFFFF"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8) };
            pick.Click += (_, _) => { _ciSel = ss.Id; Show(CiteKey); };
            left.Children.Add(pick);
        }

        var right = new StackPanel { Spacing = 6 };
        var sel = sources.FirstOrDefault(s => s.Id == _ciSel);
        if (sel is null) right.Children.Add(P("Wähle links eine Quelle oder lege eine an.", Muted, 12));
        else
        {
            right.Children.Add(H("Angaben", 14));
            var kindRow = new WrapPanel();
            foreach (var (kid, klabel) in CiteKit.Kinds)
            {
                var k = kid; var b = new Button { Name = "cite-kind-" + kid, Content = klabel, Padding = new Thickness(10, 3), Margin = new Thickness(0, 0, 6, 6) };
                if (sel.Kind == kid) b.Classes.Add("accent");
                b.Click += (_, _) => ProgramRun(CiteKey, () => CiteSet(d, sel.Id, "kind", k));
                kindRow.Children.Add(b);
            }
            right.Children.Add(kindRow);
            var preview = CiRich(CiteKit.Format(sel, style), "cite-preview");
            var warn = new TextBlock { Name = "cite-missing", Foreground = K4Warn, FontSize = 11, TextWrapping = TextWrapping.Wrap };
            void Refresh()
            {
                var cur = CiteSources(d).FirstOrDefault(x => x.Id == sel.Id); if (cur is null) return;
                var md = CiteKit.Format(cur, style); preview.Tag = CiteKit.Plain(md);
                var ic = new InlineCollection(); var parts = md.Split('*');
                for (var i = 0; i < parts.Length; i++) if (parts[i].Length > 0) ic.Add(new Run(parts[i]) { FontStyle = i % 2 == 1 ? FontStyle.Italic : FontStyle.Normal });
                preview.Inlines = ic;
                var m = CiteKit.Missing(cur, style); warn.Text = m.Count > 0 ? "⚠ Fehlt für diesen Stil: " + string.Join(", ", m) : "✓ Vollständig für diesen Stil.";
            }
            Refresh();
            var o = CiJson(sel);
            foreach (var (key, label, _) in CiFields.Where(f => f.Kinds.Contains(sel.Kind)))
            {
                var k = key; var shown = Js(o[k]);
                var box = new TextBox { Name = "cite-f-" + k, Text = k == "accessed" && CiteKit.Date(shown) is { } dt ? dt.ToString("dd.MM.yyyy", De) : shown, PlaceholderText = label };
                void Commit() { var now = box.Text ?? ""; if (now.Trim() == shown.Trim()) return; try { CiteSet(d, sel.Id, k, now); shown = now; Refresh(); } catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { _progMsg[CiteKey] = ex.Message; } }
                box.LostFocus += (_, _) => Commit();
                OnEnter(box, Commit);
                right.Children.Add(P(label, null, 11)); right.Children.Add(box);
            }
            right.Children.Add(new Border { Margin = new Thickness(0, 8, 0, 0), Padding = new Thickness(10, 8), CornerRadius = new CornerRadius(6), Background = Brush.Parse("#0E1520"), Child = new StackPanel { Spacing = 4, Children = { preview, warn } } });
            var del = new Button { Name = "cite-remove", Content = "Quelle entfernen", Padding = new Thickness(10, 4), Margin = new Thickness(0, 6, 0, 0) };
            del.Click += (_, _) => ProgramRun(CiteKey, () => CiteRemove(d, sel.Id));
            right.Children.Add(del);

            right.Children.Add(H("Zitate", 14));
            var mine = quotes.Where(q => q.SourceId == sel.Id).ToList();
            var qi = 0;
            foreach (var q in mine)
            {
                var qq = q;
                var x = new Button { Name = "cite-q-del-" + qi, Content = "✕", Padding = new Thickness(6, 1), VerticalAlignment = VerticalAlignment.Top };
                ToolTip.SetTip(x, "Zitat entfernen");
                x.Click += (_, _) => ProgramRun(CiteKey, () => CiSaveQuotes(d, CiteQuotes(d).Where(z => z.Id != qq.Id), "Zitat entfernt"));
                right.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = {
                    new StackPanel { Children = { new SelectableTextBlock { Name = "cite-q-" + qi++, Text = CiteKit.QuoteLine(q, sel, style), Foreground = Text, FontSize = 12, TextWrapping = TextWrapping.Wrap },
                        q.Page.Length == 0 ? new TextBlock { Text = "⚠ ohne Seitenangabe", Foreground = K4Warn, FontSize = 11 } : new TextBlock { Text = q.Note, Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap } } }, Pos(x, 1) } });
            }
            var qt = new TextBox { Name = "cite-q-text", PlaceholderText = "Zitat (wörtlich)", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 60 };
            var qp = new TextBox { Name = "cite-q-page", PlaceholderText = "Seite", Width = 90 };
            var qn = new TextBox { Name = "cite-q-note", PlaceholderText = "Notiz (optional)" };
            var add = new Button { Name = "cite-q-add", Content = "Zitat hinzufügen", Padding = new Thickness(10, 4) };
            add.Click += (_, _) => ProgramRun(CiteKey, () => CiteAddQuote(d, sel.Id, qt.Text ?? "", qp.Text ?? "", qn.Text ?? ""));
            right.Children.Add(qt);
            right.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,8,*,8,Auto"), Children = { qp, Pos(qn, 2), Pos(add, 4) } });
        }

        var bib = new StackPanel { Name = "cite-bibliography", Spacing = 4 };
        bib.Children.Add(H("Literaturverzeichnis", 14));
        foreach (var line in CiteKit.Bibliography(sources, style)) bib.Children.Add(CiRich(line));
        return new StackPanel { Spacing = 12, Margin = new Thickness(4, 0, 10, 4), Children = {
            new Grid { ColumnDefinitions = new ColumnDefinitions("*,14,*"), Children = { left, Pos(KrCard(right, "cite-edit"), 2) } },
            KrCard(bib, "cite-bib-card") } };
    }

    Control CiteProperties(ProgramDocument d)
    {
        var sp = new StackPanel { Spacing = 4 };
        sp.Children.Add(H("Aussage prüfen", 13));
        sp.Children.Add(P("Welche Aussage soll die gewählte Quelle belegen?", null, 11));
        var claim = KrBox(d, "claim", "cite-p-claim", "z. B. „Mulchen spart im Sommer Gießwasser.“", true); claim.MinHeight = 70;
        sp.Children.Add(claim);
        var ask = new Button { Name = "cite-claude-now", Content = "Mit Claude prüfen", Padding = new Thickness(10, 4), Margin = new Thickness(0, 4, 0, 0) };
        ask.Click += (_, _) => CiClaude(d);
        sp.Children.Add(ask);
        sp.Children.Add(H("Hinweise", 13));
        sp.Children.Add(P("APA 7 nach den deutschen Regeln (DGPs), Harvard nach „Cite Them Right“, DIN ISO 690 in einer verbreiteten Form. Prüfe die Vorgaben deiner Hochschule oder Redaktion. Einrichtungen als Autor in geschweiften Klammern: {Statistisches Amt}.", Muted, 11));
        return sp;
    }

    // ======================================================================
    // Tagesplaner
    // ======================================================================

    /// <summary>Tests: „jetzt“ für den Tagesplaner (Planen ab der aktuellen Uhrzeit, wenn der Plan für heute ist).</summary>
    public static Func<DateTime>? DayNowOverride { get; set; }
    static DateTime DayNow => DayNowOverride?.Invoke() ?? DateTime.Now;

    static JsonObject DayDoc(DateTime day, IEnumerable<DayPlanner.Item>? items = null, IEnumerable<(string Title, string Start, string End)>? appts = null) => new()
    {
        ["day"] = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), ["start"] = "08:00", ["end"] = "17:00", ["work"] = "90", ["brk"] = "10",
        ["tasks"] = new JsonArray((items ?? []).Select(i => (JsonNode)DpTaskJson(i)).ToArray()),
        ["appts"] = new JsonArray((appts ?? []).Select(a => (JsonNode)new JsonObject { ["id"] = NewId("a"), ["title"] = a.Title, ["start"] = a.Start, ["end"] = a.End, ["uid"] = "" }).ToArray()),
        ["plan"] = new JsonArray(), ["later"] = new JsonArray(),
    };

    static JsonObject DpTaskJson(DayPlanner.Item i) => new()
    {
        ["id"] = i.Id, ["title"] = i.Title, ["min"] = i.Minutes.ToString(CultureInfo.InvariantCulture), ["imp"] = i.Importance.ToString(CultureInfo.InvariantCulture),
        ["due"] = i.Due?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "", ["done"] = i.Done ? "ja" : "",
    };

    static DateTime DpDay(ProgramDocument d) =>
        DateTime.TryParseExact(d.Text("day"), ["yyyy-MM-dd", "dd.MM.yyyy", "d.M.yyyy"], De, DateTimeStyles.None, out var x) ? x.Date : DayNow.Date;

    static List<DayPlanner.Item> DpItems(ProgramDocument d) => d.List("tasks").OfType<JsonObject>().Select(o => new DayPlanner.Item(Js(o["id"]), Js(o["title"]),
        int.TryParse(Js(o["min"]), out var m) ? m : 30, int.TryParse(Js(o["imp"]), out var i) ? Math.Clamp(i, 1, 3) : 2,
        DateTime.TryParseExact(Js(o["due"]), ["yyyy-MM-dd", "dd.MM.yyyy"], De, DateTimeStyles.None, out var due) ? due : null, Js(o["done"]) == "ja")).Where(i => i.Id.Length > 0).DistinctBy(i => i.Id).ToList(); // doppelte Kennungen aus fremden Dateien fallen weg

    static List<DayPlanner.Appointment> DpAppts(ProgramDocument d)
    {
        var day = DpDay(d);
        return d.List("appts").OfType<JsonObject>().Select(o => (O: o, S: DayPlanner.ParseTime(Js(o["start"])), E: DayPlanner.ParseTime(Js(o["end"]))))
            .Where(x => x.S is not null && x.E is not null && x.E > x.S).Select(x => new DayPlanner.Appointment(Js(x.O["id"]), Js(x.O["title"]), day + x.S!.Value, day + x.E!.Value)).ToList();
    }

    static DayPlanner.Options DpOptions(ProgramDocument d)
    {
        var s = DayPlanner.ParseTime(d.Text("start")) ?? throw new FormatException("Arbeitsbeginn als Uhrzeit, z. B. 08:00.");
        var e = DayPlanner.ParseTime(d.Text("end")) ?? throw new FormatException("Arbeitsende als Uhrzeit, z. B. 17:00.");
        var w = int.TryParse(d.Text("work"), out var ww) ? ww : 90; var b = int.TryParse(d.Text("brk"), out var bb) ? bb : 10;
        return new(s, e, w, b);
    }

    static void DpSaveItems(ProgramDocument d, IEnumerable<DayPlanner.Item> items, string label) => d.Edit(label, x => x["tasks"] = new JsonArray(items.Select(i => (JsonNode)DpTaskJson(i)).ToArray()));

    static DayPlanner.Plan DpStored(ProgramDocument d)
    {
        var items = DpItems(d).ToDictionary(i => i.Id);
        var blocks = d.List("plan").OfType<JsonObject>().Select(o => (O: o, S: DateTime.TryParse(Js(o["s"]), CultureInfo.InvariantCulture, DateTimeStyles.None, out var s) ? s : (DateTime?)null, E: DateTime.TryParse(Js(o["e"]), CultureInfo.InvariantCulture, DateTimeStyles.None, out var e) ? e : (DateTime?)null))
            .Where(x => x.S is not null && x.E is not null && x.E > x.S).Select(x => new DayPlanner.Block(x.S!.Value, x.E!.Value, Js(x.O["title"]), Js(x.O["kind"]) is DayPlanner.Fixed or DayPlanner.Pause ? Js(x.O["kind"]) : DayPlanner.Work, Js(x.O["item"]) is { Length: > 0 } it ? it : null)).ToList();
        var later = d.List("later").OfType<JsonObject>().Where(o => items.ContainsKey(Js(o["id"]))).Select(o => new DayPlanner.Later(items[Js(o["id"])], Js(o["why"]))).ToList();
        return new(DpDay(d), blocks, later);
    }

    /// <summary>Tag planen und den Plan im Dokument speichern; für heute erst ab jetzt.</summary>
    public DayPlanner.Plan DayPlanRun(ProgramDocument d)
    {
        var day = DpDay(d); var o = DpOptions(d);
        var plan = DayPlanner.Make(day, DpItems(d), DpAppts(d), o, day == DayNow.Date ? DayNow : null);
        d.Edit("Tag geplant", x =>
        {
            x["plan"] = new JsonArray(plan.Blocks.Select(b => (JsonNode)new JsonObject { ["s"] = b.Start.ToString("s", CultureInfo.InvariantCulture), ["e"] = b.End.ToString("s", CultureInfo.InvariantCulture), ["title"] = b.Title, ["kind"] = b.Kind, ["item"] = b.ItemId ?? "" }).ToArray());
            x["later"] = new JsonArray(plan.Tomorrow.Select(l => (JsonNode)new JsonObject { ["id"] = l.Item.Id, ["why"] = l.Why }).ToArray());
        });
        var n = plan.Blocks.Count(b => b.Kind == DayPlanner.Work);
        _progMsg[DayKey] = $"{n} Aufgaben eingeplant" + (plan.Tomorrow.Count > 0 ? $", {plan.Tomorrow.Count} passen nicht mehr – stehen unter „Morgen“." : ".");
        return plan;
    }

    /// <summary>Termine des Tages aus dem eigenen Kalender (SYS-09) übernehmen, ohne ganztägige und ohne doppelte.</summary>
    public int DayPlanFromCalendar(ProgramDocument d)
    {
        var day = DpDay(d); var added = 0;
        var have = d.List("appts").OfType<JsonObject>().Select(o => Js(o["uid"])).Where(u => u.Length > 0).ToHashSet();
        var list = d.List("appts").OfType<JsonObject>().Select(o => (JsonNode)o.DeepClone()).ToList();
        foreach (var (e, s, en) in Cal.Occurrences(day, day.AddDays(1)).Where(o => !o.Event.AllDay))
        {
            var uid = e.Uid + "#" + s.ToString("s", CultureInfo.InvariantCulture);
            if (!have.Add(uid)) continue;
            var from = s < day ? day : s; var to = en > day.AddDays(1) ? day.AddDays(1) : en;
            list.Add(new JsonObject { ["id"] = NewId("a"), ["title"] = e.Title, ["start"] = from.ToString("HH:mm", CultureInfo.InvariantCulture), ["end"] = to >= day.AddDays(1) ? "24:00" : to.ToString("HH:mm", CultureInfo.InvariantCulture), ["uid"] = uid });
            added++;
        }
        d.Edit("Termine aus dem Kalender", x => x["appts"] = new JsonArray(list.ToArray()));
        _progMsg[DayKey] = added == 0 ? "Im Kalender stehen für diesen Tag keine weiteren Termine." : $"{added} Termine aus dem Kalender übernommen.";
        return added;
    }

    /// <summary>Offene Aufgaben aus „E-Mails &amp; Termine“ übernehmen (gleicher Titel kommt nicht doppelt).</summary>
    public int DayPlanFromTodos(ProgramDocument d)
    {
        var items = DpItems(d); var titles = items.Select(i => i.Title.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase); var added = 0;
        foreach (var t in Cal.Todos().Where(t => !t.Done))
        {
            if (!titles.Add(t.Title.Trim())) continue;
            items.Add(new(NewId("t"), KreativSafe.Cut(t.Title.Trim(), 200), t.Priority == 1 ? 60 : 30, Math.Clamp(t.Priority, 1, 3), t.Due?.Date)); added++;
        }
        DpSaveItems(d, items, "Aufgaben übernommen");
        _progMsg[DayKey] = added == 0 ? "Keine weiteren offenen Aufgaben gefunden." : $"{added} offene Aufgaben übernommen – Dauer bei Bedarf anpassen.";
        return added;
    }

    public void DayPlanAddLines(ProgramDocument d, string text)
    {
        var got = DayPlanner.ParseItems(text, DpDay(d)).Select(i => i with { Id = NewId("t") }).ToList();
        if (got.Count == 0) throw new ArgumentException("Schreib eine Aufgabe, z. B. „Angebot schreiben, 90 min, hoch, bis 20.11.“.");
        DpSaveItems(d, DpItems(d).Concat(got), "Aufgabe");
        _progMsg[DayKey] = got.Count == 1 ? $"Aufgabe „{got[0].Title}“ ({got[0].Minutes} min) eingetragen." : $"{got.Count} Aufgaben eingetragen.";
    }

    public void DayPlanToggle(ProgramDocument d, string id, bool done) =>
        DpSaveItems(d, DpItems(d).Select(i => i.Id == id ? i with { Done = done } : i), done ? "Erledigt" : "Wieder offen");

    public void DayPlanAddAppt(ProgramDocument d, string title, string start, string end)
    {
        if (DayPlanner.ParseTime(start) is not { } s || DayPlanner.ParseTime(end) is not { } e || e <= s) throw new FormatException("Termin mit Beginn und Ende, z. B. 10:00 bis 10:30.");
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Wie heißt der Termin?");
        d.Edit("Termin", x => { if (x["appts"] is not JsonArray a) x["appts"] = a = []; a.Add(new JsonObject { ["id"] = NewId("a"), ["title"] = KreativSafe.Cut(title.Trim(), 200), ["start"] = $"{(int)s.TotalHours:00}:{s.Minutes:00}", ["end"] = $"{(int)e.TotalHours:00}:{e.Minutes:00}", ["uid"] = "" }); });
    }

    /// <summary>Auf morgen: Tag +1, nur offene Aufgaben bleiben, Plan und Termine des alten Tags fallen weg.</summary>
    public void DayPlanCarry(ProgramDocument d)
    {
        var day = DpDay(d).AddDays(1); var open = DayPlanner.Carry(DpItems(d));
        d.Edit("Auf morgen", x =>
        {
            x["day"] = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            x["tasks"] = new JsonArray(open.Select(i => (JsonNode)DpTaskJson(i)).ToArray());
            x["appts"] = new JsonArray(); x["plan"] = new JsonArray(); x["later"] = new JsonArray();
        });
        _progMsg[DayKey] = $"{day.ToString("dddd, d. MMMM", De)}: {open.Count} offene Aufgaben übernommen. Termine holst du mit „Termine aus dem Kalender“.";
    }

    DayPlanner.Plan DpForExport(ProgramDocument d) => d.List("plan").Count > 0 ? DpStored(d) : DayPlanRun(d);

    private void InitDayPlan() => RegisterProgram(FachTabs, new ProgramSpec(DayKey, "STUDIO-DAYPLAN-01", "Tagesplaner")
    {
        Sentence = "Aufgaben mit Dauer, Wichtigkeit und Frist um feste Termine herum in den Tag legen – mit Pausen; was nicht passt, kommt auf morgen.",
        Folder = "Tagesplaner",
        ScrollWorkspace = true,
        Templates =
        [
            new("leer", "Leerer Tag", "Heute planen", () => DayDoc(DayNow.Date)),
            new("beispiel", "Beispiel: Bürotag", "Vier Aufgaben, drei Termine", () => DayDoc(DayNow.Date,
                [new("t1", "Angebot für Erika Muster schreiben", 90, 1, DayNow.Date), new("t2", "Rechnungen prüfen", 45, 2), new("t3", "Steuerunterlagen sortieren", 120, 3), new("t4", "Rückruf beim Kundendienst", 15, 1)],
                [("Teamrunde", "10:00", "10:30"), ("Mittagessen", "12:30", "13:15"), ("Zahnarzt", "15:00", "16:00")])),
        ],
        Ribbon =
        [
            ("Planen", [
                new ProgramCmd("dp-plan", "Tag planen", d => DayPlanRun(d), Primary: true, Tip: "Wichtiges und Fälliges zuerst, um Termine herum, mit Pausen."),
                new ProgramCmd("dp-cal", "Termine aus dem Kalender", d => DayPlanFromCalendar(d), Tip: "Aus „E-Mails & Termine“."),
                new ProgramCmd("dp-todos", "Offene Aufgaben übernehmen", d => DayPlanFromTodos(d), Tip: "Aus der Aufgabenliste von „E-Mails & Termine“."),
            ]),
            ("Tag", [
                new ProgramCmd("dp-today", "Heute", d => { d.Set("day", DayNow.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "Heute"); }),
                new ProgramCmd("dp-carry", "Unerledigtes auf morgen", d => DayPlanCarry(d), Tip: "Erledigtes fällt weg, der Rest zieht auf den nächsten Tag."),
            ]),
        ],
        Exports =
        [
            new("ics", "Kalenderdatei (.ics)", ".ics", (d, f) => { File.WriteAllText(f, DayPlanner.Ics(DpForExport(d), DateTime.UtcNow), new UTF8Encoding(false)); return f; }),
            new("txt", "Tagesplan (.txt)", ".txt", (d, f) => { File.WriteAllText(f, DayPlanner.Text(DpForExport(d), DpOptions(d))); return f; }),
        ],
        Workspace = DayWorkspace,
        Properties = DayProperties,
        Status = d => { var items = DpItems(d); var p = DpStored(d); return $"{DpDay(d).ToString("ddd d. MMM", De)} · {items.Count(i => !i.Done)} offen, {items.Count(i => i.Done)} erledigt" + (p.Blocks.Count > 0 ? $" · {p.Blocks.Count(b => b.Kind == DayPlanner.Work)} eingeplant" : " · noch nicht geplant"); },
        Help = "Aufgaben links eintragen, z. B. „Angebot schreiben, 90 min, hoch, bis 20.11.“. Feste Termine darunter oder aus dem Kalender holen. „Tag planen“ legt alles in Zeitblöcke zwischen Arbeitsbeginn und -ende, um die Termine herum und mit Pausen. Was nicht passt, steht unter „Morgen“. Abends „Unerledigtes auf morgen“. Die Kalenderdatei öffnet jedes Kalenderprogramm.",
    }, "tagesplaner", "tag planen", "tagesplan", "zeitblöcke", "timeboxing", "aufgaben planen", "to-do", "todo", "pausen", "morgen", "zeitplan");

    Control DayWorkspace(ProgramDocument d)
    {
        var items = DpItems(d); var plan = DpStored(d); var day = DpDay(d);
        var left = new StackPanel { Spacing = 12 };

        // Aufgaben
        var tasks = new StackPanel { Name = "dp-tasks", Spacing = 4 };
        tasks.Children.Add(H($"Aufgaben ({items.Count(i => !i.Done)} offen)", 14));
        // eigenes Feld (nicht im Dokument gespeichert): nach „Eintragen“ darf beim Neuzeichnen nichts zurückgeschrieben werden
        var quick = new TextBox { Name = "dp-quick", PlaceholderText = "z. B. „Angebot schreiben, 90 min, hoch, bis 20.11.“ – eine je Zeile", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 44 };
        var addBtn = new Button { Name = "dp-add", Content = "Eintragen", Padding = new Thickness(10, 4), VerticalAlignment = VerticalAlignment.Top };
        addBtn.Click += (_, _) => ProgramRun(DayKey, () => DayPlanAddLines(d, quick.Text ?? ""));
        tasks.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,Auto"), Children = { quick, Pos(addBtn, 2) } });
        var n = 0;
        foreach (var it in DayPlanner.Order(items).Concat(items.Where(i => i.Done)))
        {
            var ii = it; var idx = n++;
            var cb = new CheckBox { Name = "dp-done-" + idx, IsChecked = it.Done, VerticalAlignment = VerticalAlignment.Center };
            cb.IsCheckedChanged += (_, _) => { if ((cb.IsChecked == true) != ii.Done) ProgramRun(DayKey, () => DayPlanToggle(d, ii.Id, cb.IsChecked == true)); };
            var del = new Button { Name = "dp-del-" + idx, Content = "✕", Padding = new Thickness(6, 1) };
            ToolTip.SetTip(del, "Aufgabe entfernen");
            del.Click += (_, _) => ProgramRun(DayKey, () => DpSaveItems(d, DpItems(d).Where(x => x.Id != ii.Id), "Aufgabe entfernt"));
            var meta = $"{it.Minutes} min · {DayPlanner.Level(it.Importance)}" + (it.Due is { } due ? $" · bis {due.ToString("dd.MM.", De)}" + (due.Date < day ? " (überfällig)" : "") : "");
            tasks.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Children = { cb,
                Pos(new StackPanel { Margin = new Thickness(4, 0), Children = { new TextBlock { Text = it.Title, Foreground = it.Done ? Muted : Text, FontSize = 13, TextDecorations = it.Done ? TextDecorations.Strikethrough : null, TextWrapping = TextWrapping.Wrap },
                    new TextBlock { Text = meta, Foreground = it.Due is { } dd && dd.Date < day && !it.Done ? K4Warn : Muted, FontSize = 11 } } }, 1), Pos(del, 2) } });
        }
        left.Children.Add(KrCard(tasks, "dp-tasks-card"));

        // Termine
        var appts = new StackPanel { Name = "dp-appts", Spacing = 4 };
        appts.Children.Add(H("Feste Termine", 14));
        var ai = 0;
        foreach (var a in DpAppts(d).OrderBy(a => a.Start))
        {
            var aa = a; var del = new Button { Name = "dp-appt-del-" + ai++, Content = "✕", Padding = new Thickness(6, 1) };
            ToolTip.SetTip(del, "Termin entfernen");
            del.Click += (_, _) => ProgramRun(DayKey, () => d.Edit("Termin entfernt", x => x["appts"] = new JsonArray(d.List("appts").OfType<JsonObject>().Where(o => Js(o["id"]) != aa.Id).Select(o => (JsonNode)o.DeepClone()).ToArray())));
            appts.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("110,*,Auto"), Children = { new TextBlock { Text = $"{a.Start:HH:mm}–{(a.End.Date > day ? "24:00" : a.End.ToString("HH:mm", CultureInfo.InvariantCulture))}", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center },
                Pos(new TextBlock { Text = a.Title, Foreground = Text, FontSize = 13, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis }, 1), Pos(del, 2) } });
        }
        var at = new TextBox { Name = "dp-appt-title", PlaceholderText = "Termin" };
        var af = new TextBox { Name = "dp-appt-from", PlaceholderText = "von", Width = 70 };
        var au = new TextBox { Name = "dp-appt-to", PlaceholderText = "bis", Width = 70 };
        var aadd = new Button { Name = "dp-appt-add", Content = "Hinzufügen", Padding = new Thickness(10, 4) };
        aadd.Click += (_, _) => ProgramRun(DayKey, () => DayPlanAddAppt(d, at.Text ?? "", af.Text ?? "", au.Text ?? ""));
        appts.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,6,Auto,6,Auto,6,Auto"), Children = { at, Pos(af, 2), Pos(au, 4), Pos(aadd, 6) } });
        left.Children.Add(KrCard(appts, "dp-appts-card"));

        // Plan
        var right = new StackPanel { Name = "dp-plan", Spacing = 4 };
        right.Children.Add(H($"Plan für {day.ToString("dddd, d. MMMM yyyy", De)}", 14));
        if (plan.Blocks.Count == 0)
        {
            var go = new Button { Name = "dp-plan-now", Content = "Tag planen", Padding = new Thickness(14, 6) }; go.Classes.Add("accent");
            go.Click += (_, _) => ProgramRun(DayKey, () => DayPlanRun(d));
            right.Children.Add(P("Noch kein Plan. Trag Aufgaben und Termine ein und klick auf „Tag planen“.", Muted, 12));
            right.Children.Add(go);
        }
        var byId = items.ToDictionary(i => i.Id);
        var bi = 0;
        foreach (var b in plan.Blocks)
        {
            var color = b.Kind switch { DayPlanner.Fixed => Brush.Parse("#5FA8D3"), DayPlanner.Pause => Brush.Parse("#6B7280"), _ => Accent };
            var done = b.ItemId is { } iid && byId.TryGetValue(iid, out var item) && item.Done;
            var label = new TextBlock { Text = b.Kind switch { DayPlanner.Fixed => "Termin: " + b.Title, DayPlanner.Pause => "Pause", _ => b.Title }, Foreground = b.Kind == DayPlanner.Pause ? Muted : done ? Muted : Text, FontSize = 13, TextWrapping = TextWrapping.Wrap,
                TextDecorations = done ? TextDecorations.Strikethrough : null, VerticalAlignment = VerticalAlignment.Center };
            Control tail = new Border();
            if (b.Kind == DayPlanner.Work && b.ItemId is { } tid && byId.ContainsKey(tid))
            {
                var cb = new CheckBox { Name = "dp-block-done-" + bi, IsChecked = done }; ToolTip.SetTip(cb, "Erledigt");
                cb.IsCheckedChanged += (_, _) => { if ((cb.IsChecked == true) != done) ProgramRun(DayKey, () => DayPlanToggle(d, tid, cb.IsChecked == true)); };
                tail = cb;
            }
            bi++;
            right.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("4,8,100,*,Auto"), MinHeight = Math.Clamp((b.End - b.Start).TotalMinutes * 0.5, 22, 90), Children = {
                new Border { Background = color, CornerRadius = new CornerRadius(2) },
                Pos(new TextBlock { Text = $"{b.Start:HH:mm}–{(b.End.Date > day ? "24:00" : b.End.ToString("HH:mm", CultureInfo.InvariantCulture))}", Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, FontFamily = new FontFamily("Cascadia Code, Consolas, monospace") }, 2),
                Pos(label, 3), Pos(tail, 4) } });
        }
        if (plan.Blocks.Count > 0)
        {
            var (pl, br, fr) = DayPlanner.Summary(plan, DpOptionsSafe(d));
            right.Children.Add(P($"Geplant {pl} min · Pausen {br} min · frei {fr} min", Muted, 11));
        }
        if (plan.Tomorrow.Count > 0)
        {
            var later = new StackPanel { Name = "dp-later", Spacing = 3, Margin = new Thickness(0, 10, 0, 0) };
            later.Children.Add(H("Morgen", 14));
            foreach (var l in plan.Tomorrow) later.Children.Add(new TextBlock { Text = $"⚠ {l.Item.Title} ({l.Item.Minutes} min) – {l.Why}", Foreground = K4Warn, FontSize = 12, TextWrapping = TextWrapping.Wrap });
            right.Children.Add(later);
        }
        return new Grid { ColumnDefinitions = new ColumnDefinitions("*,14,*"), Margin = new Thickness(4, 0, 10, 4), Children = { left, Pos(KrCard(right, "dp-plan-card"), 2) } };
    }

    static DayPlanner.Options DpOptionsSafe(ProgramDocument d) { try { return DpOptions(d); } catch (FormatException) { return new(TimeSpan.FromHours(8), TimeSpan.FromHours(17)); } }

    Control DayProperties(ProgramDocument d)
    {
        var sp = new StackPanel { Spacing = 4 };
        void Txt(string key, string label, string hint) { sp.Children.Add(P(label, null, 11)); sp.Children.Add(KrBox(d, key, "dp-p-" + key, hint)); }
        Txt("day", "Tag (JJJJ-MM-TT)", "2026-11-16");
        Txt("start", "Arbeitsbeginn", "08:00");
        Txt("end", "Arbeitsende", "17:00");
        Txt("work", "Pause nach … Minuten Arbeit", "90");
        Txt("brk", "Pausenlänge (Minuten, 0 = keine)", "10");
        sp.Children.Add(P("Termine und Lücken ab Pausenlänge zählen als Unterbrechung. Aufgaben werden nicht geteilt; eine Aufgabe, die nirgends ganz passt, kommt auf morgen.", Muted, 11));
        return sp;
    }
}
