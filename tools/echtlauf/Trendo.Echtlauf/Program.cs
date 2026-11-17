// T-0687 Echtlauf-Station, Helfer. Zwei Befehle:
//   finden <ziel.json>        – jedes externe Werkzeug so suchen, wie Trendo es sucht (DependencyManager, EngineTargets, GameStudio, ClaudeCode,
//                               Browsers/WebsitePreview, KiLibraryStore), Version lesen. Installiert nichts, lädt nichts.
//   lauf <werkzeug> <ordner>  – ein kleiner echter Fachlauf über Trendos Code. Exitcode 0 = ECHT-GRÜN, 1 = ROT, 3 = BLOCKIERT (erste Zeile nennt den Grund).
// Aufgerufen von tools/echtlauf/echtlauf.py. Startet nur eigene Prozesse und beendet nur diese.
using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Bibliothek;
using Trendo.Core.Kreativ;
using Trendo.Core.Programs;

Console.OutputEncoding = Encoding.UTF8;
if (args.Length < 2) { Console.Error.WriteLine("Aufruf: finden <ziel.json> | lauf <werkzeug> <ordner>"); return 2; }
var tools = DataLocation.ToolsRoot();
var deps = new DependencyManager(new AuditLog(Path.Combine(Path.GetTempPath(), $"trendo-echtlauf-audit-{Environment.ProcessId}.jsonl")), new RealProcessRunner()) { ToolsRoot = tools };
var found = Find();
if (args[0] == "finden")
{
    var arr = new JsonArray(found.Select(f => (JsonNode)new JsonObject { ["id"] = f.Id, ["name"] = f.Name, ["pfad"] = f.Path, ["version"] = f.Path is null ? null : Version(f.Id, f.Path), ["suchweg"] = f.How }).ToArray());
    File.WriteAllText(args[1], arr.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    foreach (var f in found) Console.WriteLine($"{f.Id,-12} {(f.Path is null ? "FEHLT" : Anon(f.Path))}");
    return 0;
}
if (args[0] != "lauf" || args.Length < 3) { Console.Error.WriteLine("Unbekannter Befehl"); return 2; }
var outDir = Path.GetFullPath(args[2]); Directory.CreateDirectory(outDir);
string? PathOf(string id) => found.FirstOrDefault(f => f.Id == id)?.Path;
try
{
    return args[1] switch
    {
        "godot" => await Godot(), "7zip" => SevenZip(), "ollama" => await Ollama(), "claude" => await Claude(), "sdcli" => SdCli(),
        "acestep" => await AceStep(), "python" => PythonEnvs(), "unity" => Unity(), "unreal" => Unreal(),
        _ => Blocked("Für dieses Werkzeug gibt es im Helfer keinen eigenen Fachlauf."),
    };
}
catch (Exception e) when (e is IOException or InvalidOperationException or UnauthorizedAccessException or HttpRequestException or TaskCanceledException or JsonException or ArgumentException or System.ComponentModel.Win32Exception)
{
    Console.WriteLine("FEHLER: " + e.GetType().Name + ": " + Anon(e.Message));
    return 1;
}

// ───────────── Suchen wie Trendo ─────────────
List<Found> Find()
{
    var l = new List<Found>();
    string? Dep(string id) { var c = DependencyCatalog.Get(id); return deps.ResolvePath(c) ?? deps.FindOnSystem(c); }
    void Add(string id, string name, string? path, string how) => l.Add(new(id, name, path, how));
    Add("ffmpeg", "FFmpeg", Dep("ffmpeg") ?? Cli.Find("ffmpeg"), "DependencyManager (Vollpaket, bekannte Orte, winget-Ablage, PATH, App Paths), sonst PATH wie Cli.Find");
    Add("node", "Node.js", Cli.Find("node"), "Cli.Find (PATH) wie GameStudio/DevStudio");
    Add("git", "Git", Dep("git") ?? Cli.Find("git"), "DependencyManager, sonst PATH");
    Add("python", "Python", Dep("python") ?? Cli.Find("python"), "DependencyManager, sonst PATH");
    var browser = WebsitePreview.FindBrowser() ?? Browsers.Find().FirstOrDefault()?.Path;
    Add("chrome", "Chrome/Edge (Chromium)", browser, "WebsitePreview.FindBrowser (Chrome/Chromium/Edge), sonst Browsers.Find");
    Add("blender", "Blender", Dep("blender") ?? ModelConverter.FindBlender(), "DependencyManager, sonst ModelConverter.FindBlender");
    var unreal = GameStudio.FindEngines().Select(e => GameStudio.EditorExe(e)).FirstOrDefault(File.Exists);
    Add("unreal", "Unreal Engine", unreal, "GameStudio.FindEngines (LauncherInstalled.dat)");
    var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles); var app = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    static IEnumerable<string> Dirs(string d) { try { return Directory.Exists(d) ? Directory.EnumerateDirectories(d) : []; } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return []; } }
    static IEnumerable<string> Files(string d) { try { return Directory.Exists(d) ? Directory.EnumerateFiles(d, "Godot_v4*", SearchOption.TopDirectoryOnly) : []; } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return []; } }
    static string? Read(string f) { try { return File.Exists(f) ? File.ReadAllText(f) : null; } catch (IOException) { return null; } }
    var unity = EngineTargets.FindUnity(EngineTargets.UnityRoots(pf, app, Read), Dirs, File.Exists);
    Add("unity", "Unity", unity.FirstOrDefault().Exe, "EngineTargets.FindUnity (Unity Hub, Program Files) – wie MainWindow.EngineDetect");
    var godot = EngineTargets.FindGodot([Path.Combine(tools, "godot"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Godot"), Path.Combine(local, "Programs", "Godot"), Path.Combine(pf, "Godot")], Files);
    // für Läufe ohne Fenster die Konsolenfassung daneben nehmen, wenn es sie gibt
    var gexe = godot.FirstOrDefault().Exe; if (gexe is not null && File.Exists(gexe.Replace("_win64.exe", "_win64_console.exe"))) gexe = gexe.Replace("_win64.exe", "_win64_console.exe");
    Add("godot", "Godot", gexe, "EngineTargets.FindGodot (Werkzeugordner, Benutzerordner\\Godot, Programs, Program Files) – wie MainWindow.EngineDetect");
    Add("libreoffice", "LibreOffice", Dep("libreoffice"), "DependencyManager (Vollpaket-Pfad, bekannte Orte, PATH, App Paths)");
    Add("tesseract", "Tesseract OCR", Dep("tesseract"), "DependencyManager");
    var store = new KiLibraryStore(tools);
    var sd = store.RuntimeExe(KiRuntime.StableDiffusionCpp);
    Add("sdcli", "Bild-KI (stable-diffusion.cpp, sd-cli)", File.Exists(sd) ? sd : null, "KiLibraryStore.RuntimeExe(StableDiffusionCpp)");
    Add("acestep", "Musik-KI ACE-Step", store.RuntimeReady(KiRuntime.AceStep) ? store.RuntimeDir(KiRuntime.AceStep) : null, "KiLibraryStore.RuntimeReady(AceStep)");
    Add("ollama", "Ollama", Dep("ollama"), "DependencyManager (Vollpaket ollama/ollama.exe, bekannte Orte, winget, PATH)");
    Add("claude", "Claude Code", ClaudeCode.Find(Environment.GetEnvironmentVariable, File.Exists), "ClaudeCode.Find (offizieller Installer, npm, PATH)");
    Add("7zip", "7-Zip", Dep("7zip"), "DependencyManager");
    return l;
}

string? Version(string id, string path)
{
    if (Directory.Exists(path)) return null;
    var a = id switch { "ffmpeg" => "-version", "node" or "git" or "python" or "blender" or "tesseract" or "ollama" or "claude" or "godot" => "--version", _ => null };
    if (a is null) { try { return FileVersionInfo.GetVersionInfo(path).ProductVersion; } catch (FileNotFoundException) { return null; } }
    var r = Cli.Run(path, [a], timeoutMs: 30000);
    var line = (r.Out + "\n" + r.Err).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault(x => x.Any(char.IsDigit));
    return line is null ? null : line.Length > 120 ? line[..120] : line;
}

int Blocked(string why) { Console.WriteLine("BLOCKIERT: " + Anon(why)); return 3; }
void Say(string s) => Console.WriteLine($"{DateTime.Now:HH:mm:ss} {Anon(s)}");

// T-0730: zentrale Anonymisierung für alles, was als Beleg geschrieben wird (Konsole → Rohlog, Unreal-Protokolle).
// Ersetzt den Benutzerordner (roh, mit /, JSON-maskiert, URL-kodiert), Benutzer- und Rechnernamen sowie Maschinen-/Login-IDs.
static string Anon(string s)
{
    if (string.IsNullOrEmpty(s)) return s;
    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('\\', '/');
    if (home.Length > 3 && System.IO.Path.GetDirectoryName(home) is { Length: > 0 } parent)
    {
        var anon = System.IO.Path.Combine(parent, "<user>");
        (string From, string To)[] forms =
        [
            (home, anon),
            (home.Replace('\\', '/'), anon.Replace('\\', '/')),
            (home.Replace("\\", "\\\\"), anon.Replace("\\", "\\\\")),
            (Uri.EscapeDataString(home), Uri.EscapeDataString(parent) + "%5C<user>"),
        ];
        foreach (var (from, to) in forms.OrderByDescending(f => f.From.Length))
            s = s.Replace(from, to, StringComparison.OrdinalIgnoreCase);
    }
    // Namen auch frei stehend (z. B. „LicenseClient-<name>“, „C--Users-<name>-Desktop“, „User: <name>“) und nach %5C/%2F
    foreach (var (name, rep) in new[] { (System.IO.Path.GetFileName(home), "<user>"), (Environment.UserName, "<user>"), (Environment.MachineName, "<pc>") })
        if (name is { Length: >= 3 })
            s = System.Text.RegularExpressions.Regex.Replace(s, @"(?:(?<![A-Za-z0-9])|(?<=%5[Cc])|(?<=%2[Ff]))" + System.Text.RegularExpressions.Regex.Escape(name) + "(?![A-Za-z0-9])", rep, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    return System.Text.RegularExpressions.Regex.Replace(s, @"(?i)\b((?:MachineId|LoginId)\s*[=:]\s*""?)[0-9a-f]{16,}", "$1<id>");
}
Cli.Result Run(string exe, IEnumerable<string> a, string? cwd = null, int ms = 300000)
{
    var list = a.ToList(); Say("$ " + Path.GetFileName(exe) + " " + string.Join(" ", list.Select(x => x.Contains(' ') ? "\"" + x + "\"" : x)));
    var sw = Stopwatch.StartNew(); var r = Cli.Run(exe, list, cwd, ms, encoding: Encoding.UTF8);
    Say($"  Exit {r.Exit} nach {sw.Elapsed.TotalSeconds:0.0} s");
    foreach (var l in (r.Out + "\n" + r.Err).Split('\n').Select(x => x.TrimEnd()).Where(x => x.Length > 0).TakeLast(40)) Console.WriteLine("  | " + Anon(l));
    return r;
}
static string Sha(string f) { using var s = File.OpenRead(f); return Convert.ToHexString(SHA256.HashData(s)).ToLowerInvariant(); }

// ───────────── Fachläufe ─────────────
// Godot: Trendo legt ein Spiel als Godot-4-Projekt an (EngineTargets.CreateGodotProject), Godot importiert es, prüft die Level, startet das Spiel
// 300 Bilder lang ohne Fenster und lässt die Figur laufen (Trendos Prüfskripte). Gegenprobe: ohne Boden muss die Spiel-Prüfung scheitern.
async Task<int> Godot()
{
    var exe = PathOf("godot"); if (exe is null) return Blocked("Godot 4 fehlt (Godot_v4…_win64.exe im Benutzerordner\\Godot oder im Werkzeugordner).");
    var s = new GameStudio.GameSettings { Level = ["..........", "S..o...E.Z", "##########"], MoreLevels = [["S.K.D...Z.", "##########"]], LevelNames = ["Wiese", "Burg"], Lives = 4 };
    var dir = EngineTargets.CreateGodotProject(outDir, "Echtlauf Spiel", s);
    Say("Projekt angelegt: " + Path.GetFileName(dir));
    var ok = true;
    ok &= Run(exe, EngineTargets.GodotImportArgs(dir)).Ok;
    foreach (var (name, a) in new[] { ("Level-Prüfung", EngineTargets.GodotCheckArgs(dir)), ("Spiel-Prüfung", EngineTargets.GodotPlayArgs(dir)), ("Spielablauf", EngineTargets.GodotGameplayArgs(dir)), ("Menü", EngineTargets.GodotMenuArgs(dir)), ("Spielstand", EngineTargets.GodotSaveArgs(dir)) })
    {
        var r = Run(exe, a); var (good, text) = EngineTargets.ParseGodotCheck(r.Out + "\n" + r.Err);
        Say($"{name}: {(good && r.Ok ? "OK" : "FEHLER")} – {text}"); ok &= good && r.Ok;
    }
    var start = Run(exe, ["--headless", "--path", dir, "--quit-after", "300"]);
    Say("Spiel gestartet und nach 300 Bildern beendet: Exit " + start.Exit); ok &= start.Ok && !(start.Out + start.Err).Contains("SCRIPT ERROR", StringComparison.Ordinal);
    // Gegenprobe: ohne Boden darf die Spiel-Prüfung nicht bestehen
    var bad = EngineTargets.CreateGodotProject(outDir, "Echtlauf Gegenprobe", s with { Level = ["..........", "S..o.....Z", ".........."], MoreLevels = [] });
    Run(exe, EngineTargets.GodotImportArgs(bad));
    var neg = Run(exe, EngineTargets.GodotPlayArgs(bad)); var (negOk, negText) = EngineTargets.ParseGodotCheck(neg.Out + "\n" + neg.Err);
    Say($"Gegenprobe ohne Boden: {(negOk ? "bestanden (FALSCH)" : "scheitert wie erwartet")} – {negText}"); ok &= !negOk;
    Say("Nicht geprüft: Export als Windows-Programm (dafür braucht Godot die Exportvorlagen, die hier nicht eingerichtet sind).");
    await Task.CompletedTask;
    return ok ? 0 : 1;
}

// 7-Zip: das echte 7z.exe packt, Trendos eigener Entpacker (Archive7z, ohne 7-Zip) packt aus; Inhalte byte-gleich.
int SevenZip()
{
    var exe = PathOf("7zip"); if (exe is null) return Blocked("7-Zip fehlt.");
    var src = Path.Combine(outDir, "quelle"); Directory.CreateDirectory(Path.Combine(src, "unter"));
    File.WriteAllText(Path.Combine(src, "brief.txt"), "Sehr geehrte Frau Muster,\näöü ß – Probe für 7-Zip.\n", Encoding.UTF8);
    var rnd = new byte[300_000]; new Random(7).NextBytes(rnd); File.WriteAllBytes(Path.Combine(src, "unter", "daten.bin"), rnd);
    var arc = Path.Combine(outDir, "probe.7z");
    if (!Run(exe, ["a", "-t7z", "-mx=5", arc, "."], src).Ok) return 1;
    var dst = Path.Combine(outDir, "ausgepackt"); var n = Archive7z.Extract(arc, dst);
    Say($"Archive7z.Extract: {n} Dateien");
    var same = Directory.EnumerateFiles(src, "*", SearchOption.AllDirectories).All(f => { var g = Path.Combine(dst, Path.GetRelativePath(src, f)); var eq = File.Exists(g) && Sha(f) == Sha(g); Say($"  {Path.GetRelativePath(src, f)}: {(eq ? "gleich" : "ABWEICHUNG")}"); return eq; });
    return same && n == 2 ? 0 : 1;
}

// Ollama: läuft der Dienst nicht, startet der Helfer „ollama serve“ wie Trendo (Vollpaket.ConfigureOllama, nur 127.0.0.1) und beendet ihn danach wieder.
async Task<int> Ollama()
{
    var exe = PathOf("ollama"); if (exe is null) return Blocked("Ollama fehlt (Vollpaket ollama/ollama.exe oder eigene Installation).");
    // T-0703: dasselbe Modell, das Trendo automatisch wählt (bestes vorhandenes, das zum PC passt) – nicht mehr fest das kleinste
    var have = KiModel.ScanManifests(KiModel.ModelRoots(tools));
    var choice = KiModel.Best(have, (long)GC.GetGCMemoryInfo().TotalAvailableMemoryBytes, SystemMetrics.GpuMemory()?.TotalGb ?? 0);
    if (have.All(h => h.Tag != choice.Model)) return Blocked("Es ist noch kein allgemeines Sprachmodell geladen.");
    Say($"Automatisch gewählt: {choice.Model} – {choice.Reason}");
    using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:11434/"), Timeout = TimeSpan.FromMinutes(5) };
    async Task<bool> Up() { try { return (await http.GetAsync("api/tags")).IsSuccessStatusCode; } catch (HttpRequestException) { return false; } catch (TaskCanceledException) { return false; } }
    Process? mine = null;
    if (!await Up())
    {
        var psi = new ProcessStartInfo(exe, "serve") { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(exe)!, RedirectStandardOutput = true, RedirectStandardError = true };
        Vollpaket.ConfigureOllama(psi, tools); psi.Environment["OLLAMA_HOST"] = "127.0.0.1:11434";
        mine = Process.Start(psi)!; mine.OutputDataReceived += (_, _) => { }; mine.ErrorDataReceived += (_, _) => { }; mine.BeginOutputReadLine(); mine.BeginErrorReadLine();
        Say($"ollama serve gestartet (eigener Prozess {mine.Id})");
        for (var i = 0; i < 60 && !await Up(); i++) await Task.Delay(500);
    }
    else Say("Ollama-Dienst läuft bereits – wird nur benutzt, nicht beendet.");
    try
    {
        var sw = Stopwatch.StartNew();
        var res = await http.PostAsJsonAsync("api/generate", new { model = choice.Model, prompt = "Was ist 17 + 25? Antworte nur mit der Zahl.", stream = false, options = new { temperature = 0 } });
        var body = await res.Content.ReadAsStringAsync(); var answer = JsonNode.Parse(body)?["response"]?.GetValue<string>() ?? "";
        Say($"Antwort nach {sw.Elapsed.TotalSeconds:0.0} s: {answer.Trim()}");
        return res.IsSuccessStatusCode && answer.Contains("42") ? 0 : 1;
    }
    finally { if (mine is { HasExited: false }) { mine.Kill(true); Say("eigener Ollama-Prozess beendet"); } }
}

// Claude Code: nur wenn über das Abo angemeldet (claude auth status). Nie anmelden, keine Tokens, API-Variablen werden entfernt.
async Task<int> Claude()
{
    var exe = PathOf("claude"); if (exe is null) return Blocked("Claude Code ist nicht installiert.");
    var psi = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = outDir };
    foreach (var a in new[] { "auth", "status", "--json" }) psi.ArgumentList.Add(a);
    foreach (var v in ClaudeCode.ApiVariables.Append(ClaudeCode.TokenVariable)) psi.Environment.Remove(v);
    using (var p = Process.Start(psi)!)
    {
        var json = await p.StandardOutput.ReadToEndAsync(); await p.WaitForExitAsync();
        var st = ClaudeCode.ParseStatus(exe, json);
        Say($"Anmeldestatus: angemeldet={st.LoggedIn}, Abo={st.Subscription}, Art={st.Method}");
        if (!st.Ready) return Blocked("Claude Code ist nicht über das Abo angemeldet – die Station meldet nie selbst an.");
    }
    var ask = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = outDir };
    foreach (var a in ClaudeCode.Args("Wie viel ist 17 plus 25? Antworte nur mit der Zahl.", ClaudeCode.Phase.Ask)) ask.ArgumentList.Add(a);
    foreach (var v in ClaudeCode.ApiVariables.Append(ClaudeCode.TokenVariable)) ask.Environment.Remove(v);
    Say("$ claude " + string.Join(" ", ask.ArgumentList.Select(x => x.Length > 60 ? x[..60] + "…" : x)));
    var sw = Stopwatch.StartNew();
    using var q = Process.Start(ask)!;
    var o = q.StandardOutput.ReadToEndAsync(); var e = q.StandardError.ReadToEndAsync();
    if (!q.WaitForExit(300000)) { q.Kill(true); Say("Zeitüberschreitung"); return 1; }
    var all = await o + "\n" + await e;
    var text = string.Join("\n", all.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).Select(l => { try { var n = JsonNode.Parse(l); return n?["type"]?.GetValue<string>() == "result" ? n["result"]?.GetValue<string>() ?? "" : ""; } catch (JsonException) { return ""; } }).Where(x => x.Length > 0));
    Say($"Exit {q.ExitCode} nach {sw.Elapsed.TotalSeconds:0.0} s, Antwort: {(text.Length > 0 ? text : "(kein Ergebnis)")}");
    return q.ExitCode == 0 && text.Contains("42") ? 0 : 1;
}

// Bild-KI: sd-cli mit dem ersten eingerichteten Bildmodell der KI-Bibliothek, Aufruf wie in der KI-Bibliothek der App; Bild wird gelesen und geprüft.
int SdCli()
{
    var exe = PathOf("sdcli"); if (exe is null) return Blocked("Die Bild-KI-Laufzeit (stable-diffusion.cpp) ist nicht eingerichtet.");
    var store = new KiLibraryStore(tools);
    var model = KiLibrary.OfKind(KiKind.BildErzeugen).FirstOrDefault(store.ModelReady);
    if (model is null) return Blocked("Kein Bildmodell der KI-Bibliothek ist geladen.");
    var file = store.ModelFile(model, model.Files[0]); var o = Path.Combine(outDir, "bild.png");
    var (w, h) = (512, 512); var steps = model.Id == "sdxl-turbo" ? "4" : "1";
    var prompt = ImageAiKit.FullPrompt("ein Leuchtturm an der Küste bei Sonnenuntergang", ImageAiKit.Styles[0].Id);
    Say($"Modell {model.Name}, {w}×{h}, {steps} Schritte");
    var r = Run(exe, ["-m", file, "-p", prompt, "-o", o, "--steps", steps, "--cfg-scale", "1.0", "-W", w.ToString(), "-H", h.ToString(), "-s", "42"], outDir, 1_200_000);
    if (!r.Ok || !File.Exists(o)) return 1;
    var bytes = File.ReadAllBytes(o);
    var png = bytes.Length > 24 && bytes[1] == 'P' && bytes[2] == 'N' && bytes[3] == 'G';
    var pw = png ? (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19] : 0; var ph = png ? (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23] : 0;
    Say($"PNG {pw}×{ph}, {bytes.Length} Bytes, SHA-256 {Sha(o)}");
    return png && pw == w && ph == h && bytes.Length > 50_000 ? 0 : 1;
}

// Musik-KI: Server aus der eingerichteten Laufzeit starten wie Trendo (AceStepClient.ServerCommand/ServerEnvironment), 10 s Lied erzeugen, laden, Größe prüfen.
async Task<int> AceStep()
{
    var dir = PathOf("acestep"); if (dir is null) return Blocked("ACE-Step ist in der KI-Bibliothek nicht eingerichtet.");
    using (var probe = new HttpClient { Timeout = TimeSpan.FromSeconds(3) })
        if (await new AceStepClient(probe).HealthyAsync()) return Blocked("Auf Port 8001 läuft bereits ein ACE-Step-Server (z. B. aus Trendo) – die Station stört ihn nicht.");
    var (file, a) = AceStepClient.ServerCommand(dir);
    var psi = new ProcessStartInfo(file) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true };
    foreach (var x in a) psi.ArgumentList.Add(x);
    foreach (var (k, v) in AceStepClient.ServerEnvironment()) psi.Environment[k] = v;
    var serverLog = new StringBuilder();
    using var p = Process.Start(psi)!;
    p.OutputDataReceived += (_, e) => { if (e.Data is { } l) lock (serverLog) serverLog.AppendLine(l); };
    p.ErrorDataReceived += (_, e) => { if (e.Data is { } l) lock (serverLog) serverLog.AppendLine(l); };
    p.BeginOutputReadLine(); p.BeginErrorReadLine();
    Say($"Server gestartet (eigener Prozess {p.Id})");
    try
    {
        var c = new AceStepClient(new HttpClient { Timeout = TimeSpan.FromMinutes(2) });
        var t0 = Stopwatch.StartNew();
        while (!await c.HealthyAsync())
        {
            if (p.HasExited) { Say("Server beendet sich vorzeitig"); return 1; }
            if (t0.Elapsed > TimeSpan.FromMinutes(20)) { Say("Server antwortet nicht nach 20 min"); return 1; }
            await Task.Delay(5000);
        }
        Say($"Server bereit nach {t0.Elapsed.TotalSeconds:0} s");
        var t1 = Stopwatch.StartNew();
        var id = await c.SubmitAsync(new AceStepClient.Song("ruhiger Pop mit Klavier, warme Stimme", "[Verse]\nMorgens liegt der Nebel still am See", 10, "de"));
        AceStepClient.State st;
        while ((st = await c.QueryAsync(id)).Status == 0)
        {
            if (p.HasExited || t1.Elapsed > TimeSpan.FromMinutes(20)) { Say("Erzeugen abgebrochen"); return 1; }
            await Task.Delay(3000);
        }
        if (st.Status != 1 || st.AudioUrl is null) { Say("Fehler: " + st.Error); return 1; }
        var song = Path.Combine(outDir, "lied.mp3"); await c.DownloadAsync(st.AudioUrl, song);
        var len = new FileInfo(song).Length;
        Say($"Lied fertig nach {t1.Elapsed.TotalSeconds:0} s: {len} Bytes, SHA-256 {Sha(song)}");
        if (PathOf("ffmpeg") is { } ff) { var r = Run(ff, ["-hide_banner", "-i", song, "-f", "null", "-"]); if (!r.Ok) return 1; }
        return len > 50_000 ? 0 : 1;
    }
    finally
    {
        try { if (!p.HasExited) { p.Kill(true); Say("eigener Server beendet"); } } catch (InvalidOperationException) { }
        lock (serverLog) foreach (var l in serverLog.ToString().Split('\n').TakeLast(30)) Console.WriteLine("  srv| " + Anon(l.TrimEnd()));
    }
}

// Python-Umgebungen: das allgemeine Python und jede von Trendo eingerichtete KI-Laufzeit (TrendoWerkzeuge\ki\laufzeit\<name>\…\python.exe) startet und rechnet.
int PythonEnvs()
{
    var list = new List<(string Name, string Exe)>();
    if (PathOf("python") is { } py) list.Add(("Python (allgemein)", py));
    var root = Path.Combine(tools, "ki", "laufzeit");
    if (Directory.Exists(root))
        foreach (var d in Directory.GetDirectories(root).OrderBy(x => x, StringComparer.Ordinal))
        {
            var exe = new[] { Path.Combine(d, "env", "Scripts", "python.exe"), Path.Combine(d, ".venv", "Scripts", "python.exe"), Path.Combine(d, "python", "python.exe"), Path.Combine(d, "python_embeded", "python.exe") }.FirstOrDefault(File.Exists);
            if (exe is not null) list.Add(("ki/laufzeit/" + Path.GetFileName(d), exe));
            else Say($"ki/laufzeit/{Path.GetFileName(d)}: keine Python-Umgebung an den bekannten Stellen (kein Fehler – nicht jede Laufzeit ist Python)");
        }
    if (list.Count == 0) return Blocked("Kein Python gefunden.");
    var ok = true;
    foreach (var (name, exe) in list)
    {
        var r = Run(exe, ["-I", "-c", "import sys, json, hashlib; print(sys.version.split()[0], hashlib.sha256(json.dumps({'a': [1, 2]}).encode()).hexdigest()[:12])"], ms: 60000);
        var expect = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("{\"a\": [1, 2]}"))).ToLowerInvariant()[..12];
        var good = r.Ok && r.Out.Contains(expect, StringComparison.Ordinal);
        Say($"{name}: {(good ? "OK" : "FEHLER")} {r.Out.Trim()}"); ok &= good;
    }
    return ok ? 0 : 1;
}

// Unity: nur mit --langsam aus echtlauf.py. Trendo legt das Projekt an (EngineTargets.CreateUnityProject) und lässt die EditMode-Tests laufen.
int Unity()
{
    var exe = PathOf("unity"); if (exe is null) return Blocked("Unity fehlt.");
    var s = new GameStudio.GameSettings { Level = ["..........", "S..o...E.Z", "##########"], LevelNames = ["Wiese"] };
    var dir = EngineTargets.CreateUnityProject(outDir, "EchtlaufSpiel", s);
    var results = Path.Combine(outDir, "unity-editmode.xml"); var log = Path.Combine(outDir, "unity-editmode.log");
    var r = Run(exe, EngineTargets.UnityTestArgs(dir, "EditMode", results, log), outDir, 3_600_000);
    if (File.Exists(log)) foreach (var l in File.ReadLines(log).Where(l => l.Contains("error", StringComparison.OrdinalIgnoreCase) || l.Contains("licens", StringComparison.OrdinalIgnoreCase)).Take(20)) Console.WriteLine("  log| " + Anon(l));
    if (!File.Exists(results)) return (File.Exists(log) && File.ReadAllText(log).Contains("licens", StringComparison.OrdinalIgnoreCase)) ? Blocked("Unity startet, aber ohne gültige Lizenz/Anmeldung im Unity Hub laufen keine Tests (macht der Nutzer selbst).") : 1;
    var res = EngineTargets.ParseUnityResults(File.ReadAllText(results));
    Say($"EditMode: {res}");
    return res is { Total: > 0, Failed: 0 } && res.Value.Passed == res.Value.Total ? 0 : 1;
}

// Unreal (T-0724): nur mit --langsam aus echtlauf.py. Trendo legt wie „Bauen“ im Programm „Spiele bauen“ ein C++-Spiel (3D-Level) in einem eigenen
// Temp-Ordner an (GameDesign.BuildUnreal) und fährt genau die Schritte von „Kompilieren und prüfen“ (MainWindow.GameUnrealCheck): Build.bat,
// Unreal-Tests (UnrealEditor-Cmd, Automation RunTests Trendo), Startlauf (-game -trendocheck ohne Grafik). Zeitgrenzen wie in der App
// (30 / 15 / 15 min); bei Zeitüberschreitung beendet Cli.Run nur den eigenen Prozessbaum. Unreal selbst und fremde Projekte bleiben unberührt.
int Unreal()
{
    var engine = GameStudio.FindEngines().FirstOrDefault(e => File.Exists(GameStudio.EditorExe(e)));
    if (engine is null) return Blocked("Unreal Engine 5 fehlt (Epic Games Launcher, LauncherInstalled.dat).");
    var d = new JsonObject { ["title"] = "Echtlauf Spiel", ["world3d"] = "level", ["genre"] = "Jump 'n' Run",
        ["level"] = new JsonArray("..........", "S..o...E.Z", "##########"), ["lives"] = 3 };
    var dir = GameDesign.BuildUnreal(d, outDir, engine.Version);
    var uproject = Directory.GetFiles(dir, "*.uproject").Single();
    Say($"Projekt angelegt: {Path.GetFileName(uproject)} (Unreal {engine.Version}, eigener Temp-Ordner)");
    var logs = Path.Combine(dir, "Trendo-Pruefung"); Directory.CreateDirectory(logs);
    // T-0730: Protokoll wird als Beleg übernommen – deshalb anonymisiert zurückschreiben
    string Log(string f, Cli.Result r) { var p = Path.Combine(logs, f); var raw = File.Exists(p) ? File.ReadAllText(p) : r.Out + "\n" + r.Err; var t = Anon(raw); if (!File.Exists(p) || t != raw) File.WriteAllText(p, t); return t; }
    var sw = Stopwatch.StartNew();
    var c = Run(UnrealGame.BuildBat(engine.Path), UnrealGame.CompileArgs(uproject), logs, 1_800_000);
    var clog = Log("kompilieren.txt", c);
    if (c.Exit == -2) { Say("Kompilieren: Zeitgrenze 30 min erreicht – eigener Prozessbaum beendet."); return 1; }
    var cok = UnrealGame.CompileOk(clog); Say($"Kompilieren: {(cok ? "OK" : "FEHLER")} (Exit {c.Exit}, {sw.Elapsed.TotalMinutes:0.0} min)");
    if (!cok) { foreach (var l in clog.Split('\n').Where(l => l.Contains("error", StringComparison.OrdinalIgnoreCase)).Take(20)) Console.WriteLine("  log| " + Anon(l.TrimEnd())); return 1; }
    sw.Restart();
    var tl = Path.Combine(logs, "tests.txt");
    var t = Run(UnrealGame.EditorCmd(engine.Path), UnrealGame.TestArgs(uproject, tl), logs, 900_000);
    var tests = UnrealGame.ParseTests(Log("tests.txt", t));
    Say($"Unreal-Tests: {tests.Count(x => x.Ok)} von {tests.Count} bestanden ({string.Join(", ", tests.Select(x => x.Test + (x.Ok ? "" : " FEHLER")))}) – Exit {t.Exit}, {sw.Elapsed.TotalMinutes:0.0} min{(t.Exit == -2 ? ", Zeitgrenze 15 min erreicht" : "")}");
    sw.Restart();
    var sl = Path.Combine(logs, "start.txt");
    var g = Run(UnrealGame.Editor(engine.Path), UnrealGame.CheckArgs(uproject, sl, screenshot: false), logs, 900_000);
    var (gok, gtext) = UnrealGame.ParseCheck(Log("start.txt", g));
    Say($"Startlauf: {(gok ? "OK" : "FEHLER")} – {gtext} (Exit {g.Exit}, {sw.Elapsed.TotalMinutes:0.0} min{(g.Exit == -2 ? ", Zeitgrenze 15 min erreicht" : "")})");
    foreach (var f in Directory.GetFiles(logs)) Say($"  Protokoll {Path.GetFileName(f)}: {new FileInfo(f).Length / 1024} KB, SHA256 {Sha(f)}");
    Say("Nicht geprüft: Open World (lädt 275 MB Fotomaterial), MetaHuman-Figur, Startlauf mit Grafik/Bildschirmfoto, gepacktes Spiel (BuildCookRun).");
    return tests.Count > 0 && tests.All(x => x.Ok) && gok ? 0 : 1;
}

sealed record Found(string Id, string Name, string? Path, string How);
