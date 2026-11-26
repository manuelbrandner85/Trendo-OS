using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Trendo.App;
using Trendo.Core;
using Trendo.Core.Arch;
using Trendo.Core.Kreativ;
using Trendo.Core.Programs;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0775 (Querschnitt Q2): Testschicht „echt“ – ein echter Claude-Lauf je Programmart über das installierte Claude Code mit dem Abo der Person.
/// Nichts ist ersetzt außer der Fensterdarstellung: kein <see cref="MainWindow.ClaudeRunOverride"/>, kein <see cref="MainWindow.ClaudeStatusOverride"/>, kein Ersatz-KI-Dienst.
/// Anmeldestand und Läufe gehen durch denselben Code wie im Produkt (<c>ClaudeCheck</c>, <c>ClaudeRun</c>, API-Variablen und Token entfernt); der Schalter
/// <see cref="MainWindow.ClaudeEchtImTestmodus"/> hebt nur die Sperre des Testmodus auf. Trendo meldet nie selbst an.
/// Nur mit TRENDO_ECHT_CLAUDE=&lt;Belegordner&gt;; ohne die Variable erscheinen die Tests als „übersprungen“, nie als bestanden.
/// Ist Claude Code nicht installiert oder nicht angemeldet: „übersprungen“ mit BLOCKIERT und dem Schritt für die Person, dazu eine Datei im Belegordner.</summary>
[Collection("Claude")]
public class ClaudeEchtlauf_Ui
{
    const string Var = "TRENDO_ECHT_CLAUDE";
    const int Minutes = 10;

    static string Beleg()
    {
        var d = Environment.GetEnvironmentVariable(Var);
        if (string.IsNullOrWhiteSpace(d)) Assert.Skip($"übersprungen – Echtlauf mit Claude nur mit {Var}=<Belegordner> (nutzt das Claude-Abo dieses PCs).");
        return Directory.CreateDirectory(d!).FullName;
    }

    static void Pump() { for (var i = 0; i < 6; i++) Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }
    static async Task Until(Func<bool> ok, int ms = Minutes * 60_000)
    {
        var sw = Stopwatch.StartNew();
        while (!ok() && sw.ElapsedMilliseconds < ms) { Pump(); await Task.Delay(50); }
        Pump();
    }
    static async Task Run(Task t) { while (!t.IsCompleted) { Pump(); await Task.Delay(50); } await t; }
    static void CtrlZ(MainWindow w) { w.KeyPress(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, null); Pump(); w.KeyRelease(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, null); Pump(); }
    static T Visible<T>(MainWindow w, string name) where T : Control { Pump(); return w.GetLogicalDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible); }

    /// <summary>Fenster im Testmodus öffnen, das echte Claude zulassen und den echten Anmeldestand holen. Fehlt Claude oder die Anmeldung: BLOCKIERT.</summary>
    static async Task<MainWindow> OpenReal(string dir, string fall)
    {
        MainWindow.ClaudeEchtImTestmodus = true;
        var (w, _, _) = Ui.Open();
        var st = await w.ClaudeCheck(fresh: true);
        string? block = !st.Installed ? "Claude Code ist auf diesem PC nicht installiert. Schritt für die Person: Claude Code installieren (claude.ai/download bzw. „Claude Code“), danach den Test erneut starten."
            : !st.Ready ? "Claude Code ist nicht mit einem Claude-Abo angemeldet. Schritt für die Person: in einer Eingabeaufforderung „claude“ starten, „/login“ eingeben und mit dem eigenen Claude-Konto (Abo) anmelden – Trendo meldet nie selbst an."
            : null;
        if (block is not null)
        {
            File.WriteAllText(Path.Combine(dir, fall + "-BLOCKIERT.txt"), block + Environment.NewLine);
            w.Close(); MainWindow.ClaudeEchtImTestmodus = false;
            Assert.Skip("BLOCKIERT – " + block);
        }
        return w;
    }

    static void Close(MainWindow w) { MainWindow.ClaudeEchtImTestmodus = false; w.Close(); }

    /// <summary>Rohlog (Mitschrift von Claude Code, stream-json) aus dem Arbeitsordner übernehmen: Benutzername ersetzt; aus der Startzeile nur Technisches
    /// (Modell, Version, Rechte, Anzahl der Werkzeuge) – keine Liste der Erweiterungen, Befehle und Skills der Person. Alle anderen Zeilen unverändert.</summary>
    static string CopyRawLog(string runDir, string beleg, string fall)
    {
        var src = Path.Combine(runDir, ".trendo", "letzter-lauf.jsonl");
        Assert.True(File.Exists(src), "keine Mitschrift von Claude Code: " + src);
        var sb = new StringBuilder();
        foreach (var line in File.ReadAllLines(src))
        {
            var l = line;
            try
            {
                if (JsonNode.Parse(line) is JsonObject o && (string?)o["type"] == "system" && (string?)o["subtype"] == "init")
                {
                    var keep = new JsonObject();
                    foreach (var k in new[] { "type", "subtype", "session_id", "model", "claude_code_version", "permissionMode", "apiKeySource", "cwd", "output_style" })
                        if (o[k] is { } v) keep[k] = v.DeepClone();
                    if (o["tools"] is JsonArray tools) keep["tools_anzahl"] = tools.Count; // Werkzeugnamen enthalten Erweiterungen der Person – nur die Anzahl
                    l = keep.ToJsonString();
                }
                else if (JsonNode.Parse(line) is JsonObject c && (string?)c["type"] == "system" && (string?)c["subtype"] == "commands_changed")
                {
                    // Liste der Befehle und Skills der Person (eigene Erweiterungen) – im Beleg nur die Anzahl je Liste
                    var keep = new JsonObject { ["type"] = "system", ["subtype"] = "commands_changed" };
                    foreach (var kv in c) if (kv.Value is JsonArray arr) keep[kv.Key + "_anzahl"] = arr.Count; else if (kv.Key is "session_id" or "uuid") keep[kv.Key] = kv.Value?.DeepClone();
                    l = keep.ToJsonString();
                }
            }
            catch (System.Text.Json.JsonException) { }
            sb.AppendLine(Clean(l));
        }
        var dst = Path.Combine(beleg, fall + "-rohlog.jsonl");
        File.WriteAllText(dst, sb.ToString(), new UTF8Encoding(false));
        return dst;
    }

    static string Clean(string s)
    {
        foreach (var n in new[] { Environment.UserName, Environment.MachineName }.Where(n => n.Length > 2))
            s = System.Text.RegularExpressions.Regex.Replace(s, System.Text.RegularExpressions.Regex.Escape(n), n == Environment.UserName ? "<user>" : "<pc>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return s;
    }

    static string Sha(string file) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();

    /// <summary>Bericht des Falls schreiben und die SHA-256-Liste des Belegordners neu erzeugen.</summary>
    static void Report(string beleg, string fall, StringBuilder log)
    {
        File.WriteAllText(Path.Combine(beleg, fall + "-bericht.md"), Clean(log.ToString()), new UTF8Encoding(false));
        var sums = new StringBuilder();
        foreach (var f in Directory.GetFiles(beleg).Where(f => Path.GetFileName(f) != "SHA256.txt").Order(StringComparer.Ordinal))
            sums.AppendLine($"{Sha(f)}  {Path.GetFileName(f)}");
        File.WriteAllText(Path.Combine(beleg, "SHA256.txt"), sums.ToString());
    }

    static void Head(StringBuilder log, string title, MainWindow w)
    {
        var st = w.ClaudeState;
        log.AppendLine($"# T-0775 ECHT: {title}").AppendLine();
        log.AppendLine($"Datum: {DateTime.Now:yyyy-MM-dd HH:mm} · Claude Code: installiert={st.Installed}, angemeldet={st.Ready}, Anmeldung={st.Method}, Abo={st.Plan}");
        log.AppendLine("Nichts ersetzt außer der Fensterdarstellung (headless): kein ClaudeRunOverride, kein ClaudeStatusOverride, kein Ersatz-KI-Dienst. API-Variablen und Token entfernt (ClaudeEnv).").AppendLine();
    }

    // ───────────── Kreativ: Logos & Marken – „Ideen mit Claude“ färbt das Logo um ─────────────

    [AvaloniaFact(DisplayName = "T-0775 ECHT Kreativ: „Ideen mit Claude“ in Logos & Marken färbt das offene Logo um – Vorschau, Ja, Datei, Strg+Z (nur mit TRENDO_ECHT_CLAUDE)")]
    public async Task Logo_recolor_real()
    {
        var beleg = Beleg(); const string fall = "logo";
        var w = await OpenReal(beleg, fall);
        var log = new StringBuilder(); var tmp = Directory.CreateTempSubdirectory("trendo-t0775-logo-").FullName;
        MainWindow.AssistStepDelayMs = 0;
        try
        {
            Head(log, "Logos & Marken – „Ideen mit Claude“ färbt das Logo um", w);
            const string K = "logo-programm";
            Assert.True(MainWindow.Programs.ContainsKey(K), "Programm-Schlüssel " + K);
            w.ProgramNew(K, "cafe");
            var file = w.ProgramSave(K, Path.Combine(tmp, "logo.tdoc"));
            w.Show(K); Pump();
            var doc = w.ProgramActive(K)!;
            var before = doc.Data.ToJsonString(); var history = doc.History.Count;
            var palBefore = VectorKit.Palette(MainWindow.LogoVec(doc));
            File.WriteAllText(Path.Combine(beleg, fall + "-vorher.tdoc"), File.ReadAllText(file));
            Assert.Equal(AssistRoute.Dokument, w.AssistRouteFor(K)); // Claude ist die KI der Kette ins offene Dokument
            var sw = Stopwatch.StartNew();
            RealClick(w, Visible<Button>(w, "prog-cmd-logo-claude")); Pump();
            await Until(() => w.AssistLastKey == K && w.AssistLast?.Phase is "vorschau" or "fehler" or "verworfen" || w.AssistProgramNote is not null);
            sw.Stop();
            var raw = CopyRawLog(Path.Combine(Path.GetTempPath(), "trendo-claude-frage"), beleg, fall);
            Assert.True(w.AssistLast?.Phase == "vorschau", $"keine Vorschau: {w.AssistLast?.Phase} – {w.AssistLast?.Message} – {w.AssistProgramNote} – Antwort: {w.AssistLastKiAnswer}");
            w.Show(K); Pump(); // Seite neu aufbauen – die Leiste übernimmt das Panel des Assistenten (AssistAttach)
            var source = w.Find<TextBlock>("assist-source").Text ?? "";
            var steps = w.AssistLast!.Value.Steps;
            Assert.Contains("Claude", source); // die Antwort kam wirklich von Claude (nicht von einer Ersatz-KI auf dem PC)
            Assert.Equal(before, doc.Data.ToJsonString()); // die Vorschau ändert das Original nicht
            RealClick(w, w.Find<Button>("assist-apply"));
            await Until(() => w.AssistLast?.Phase == "fertig", 30_000);
            Assert.Equal("fertig", w.AssistLast?.Phase);
            var palAfter = VectorKit.Palette(MainWindow.LogoVec(doc));
            Assert.NotEqual(string.Join(",", palBefore), string.Join(",", palAfter)); // das gezeichnete Logo hat andere Farben
            Assert.Equal(history + 1, doc.History.Count); // ein Rückgängig-Schritt
            var undoLabel = doc.UndoLabel;
            var saved = ProgramDocument.Load(file).Data.ToJsonString();
            Assert.Equal(doc.Data.ToJsonString(), saved); // echte Datei auf der Platte
            File.Copy(file, Path.Combine(beleg, fall + "-nach-claude.tdoc"), true);
            // Strg+Z auf der Programmseite
            w.Show(K); Pump();
            Visible<Button>(w, "prog-undo").Focus(); Pump();
            CtrlZ(w);
            Assert.Equal(before, w.ProgramActive(K)!.Data.ToJsonString());
            log.AppendLine("## Ablauf").AppendLine();
            log.AppendLine($"- Vorlage „Beispiel: Café“, gespeichert als logo.tdoc; Klick auf „Ideen mit Claude“ (prog-cmd-logo-claude) → Weiche {AssistRoute.Dokument}.");
            log.AppendLine($"- Plan ({source}, {sw.Elapsed.TotalSeconds:F0} s): {string.Join(" → ", steps)}");
            log.AppendLine($"- Rückgängig-Schritt: „{undoLabel}“");
            log.AppendLine($"- Farben vorher: {string.Join(", ", palBefore)}");
            log.AppendLine($"- Farben nach „Ja, übernehmen“: {string.Join(", ", palAfter)}");
            log.AppendLine($"- Datei nach Claude: {fall}-nach-claude.tdoc · SHA-256 {Sha(Path.Combine(beleg, fall + "-nach-claude.tdoc"))}");
            log.AppendLine($"- Strg+Z: Dokument wieder wie vorher = {before == w.ProgramActive(K)!.Data.ToJsonString()}");
            log.AppendLine($"- Rohlog: {Path.GetFileName(raw)} · SHA-256 {Sha(raw)}").AppendLine();
            log.AppendLine("Ergebnis: BESTANDEN.");
        }
        catch (Exception e) when (e is not Xunit.Sdk.SkipException) { log.AppendLine().AppendLine("FEHLER: " + e.Message); throw; }
        finally { Report(beleg, fall, log); MainWindow.AssistStepDelayMs = 140; Close(w); try { Directory.Delete(tmp, true); } catch (IOException) { } }
    }

    // ───────────── Büro: Briefe schreiben – Claude kürzt den offenen Brief ─────────────

    const string LongBody = "Sehr geehrte Damen und Herren,\n\nhiermit möchte ich Ihnen mitteilen, dass ich zum Ende des kommenden Monats umziehe. Meine neue Anschrift lautet Lindenweg 7, 12345 Musterstadt. "
        + "Ich bitte Sie deshalb, alle künftigen Schreiben, Rechnungen und Unterlagen an diese neue Anschrift zu senden. Meine Kundennummer 55-1234 bleibt dabei unverändert bestehen. "
        + "Außerdem möchte ich darauf hinweisen, dass meine Telefonnummer gleich bleibt und ich weiterhin unter der bekannten Nummer erreichbar bin. "
        + "Falls Sie für die Änderung noch weitere Angaben oder Unterlagen von mir benötigen, lassen Sie es mich bitte wissen, ich reiche sie gerne nach. "
        + "Ich bedanke mich im Voraus für Ihre Mühe und die schnelle Bearbeitung meines Anliegens und verbleibe\n\nmit freundlichen Grüßen";

    [AvaloniaFact(DisplayName = "T-0775 ECHT Büro: Claude kürzt im Programm „Briefe schreiben“ den offenen Brief – Änderung im Brief, Strg+Z stellt ihn wieder her (nur mit TRENDO_ECHT_CLAUDE)")]
    public async Task Letter_shorten_real()
    {
        var beleg = Beleg(); const string fall = "brief";
        var w = await OpenReal(beleg, fall);
        var log = new StringBuilder();
        var oldDelay = MainWindow.LiveStepDelay; MainWindow.LiveStepDelay = 0;
        try
        {
            Head(log, "Briefe schreiben – Claude kürzt den offenen Brief", w);
            w.Show("brief"); Pump();
            w.Letter.Subject = "Neue Anschrift ab nächstem Monat"; w.Letter.Recipient = "Stadtwerke Musterstadt\nKundenservice\nHauptstraße 1\n12345 Musterstadt"; w.Letter.Body = LongBody;
            w.Show("brief"); Pump();
            var before = w.Letter.Body;
            File.WriteAllText(Path.Combine(beleg, fall + "-vorher.txt"), before);
            w.ClaudeDockToggle(MainWindow.LetterStudio); Pump();
            var sw = Stopwatch.StartNew();
            var send = w.ClaudeChatSend("Kürze den Text dieses Briefs auf höchstens drei kurze Sätze zwischen Anrede und Grußformel. Inhalt (Umzug, neue Anschrift, Kundennummer) bleibt, Betreff und Empfänger bleiben. Ändere dafür nur das Feld Body in Brief.json.");
            await Run(send);
            sw.Stop();
            var chat = w.ClaudeChatCurrent()!;
            var raw = CopyRawLog(chat.Folder, beleg, fall);
            await Until(() => w.Letter.Body != before, 15_000);
            var after = w.Letter.Body;
            File.WriteAllText(Path.Combine(beleg, fall + "-nach-claude.txt"), after);
            Assert.NotEqual(before, after);
            Assert.True(after.Length < before.Length * 0.75, $"nicht gekürzt: {before.Length} → {after.Length} Zeichen");
            Assert.Contains("Lindenweg", after); // Inhalt bleibt
            Assert.Equal("Neue Anschrift ab nächstem Monat", w.Letter.Subject);
            Assert.Equal("brief", w.CurrentView);
            // Strg+Z im Brief-Programm
            w.Show("brief"); Pump();
            Visible<Button>(w, "letter-undo").Focus(); Pump();
            CtrlZ(w);
            Assert.Equal(before, w.Letter.Body);
            log.AppendLine("## Ablauf").AppendLine();
            log.AppendLine("- Brief im Programm „Briefe schreiben“ (Betreff, Empfänger, langer Text), Claude-Bereich des Studios geöffnet, Auftrag „kürzen“ gesendet.");
            log.AppendLine($"- Claude arbeitete im Projektordner in Brief.json ({sw.Elapsed.TotalSeconds:F0} s); Trendo übernahm die Änderung live in den offenen Brief.");
            log.AppendLine($"- Länge vorher {before.Length} Zeichen, nachher {after.Length} Zeichen.").AppendLine($"- Text nachher: {after.Replace("\n", " ⏎ ")}");
            log.AppendLine($"- Strg+Z: Brief wieder wie vorher = {before == w.Letter.Body}");
            log.AppendLine($"- Rohlog: {Path.GetFileName(raw)} · SHA-256 {Sha(raw)}").AppendLine();
            log.AppendLine("Ergebnis: BESTANDEN.");
        }
        catch (Exception e) when (e is not Xunit.Sdk.SkipException) { log.AppendLine().AppendLine("FEHLER: " + e.Message); throw; }
        finally { Report(beleg, fall, log); MainWindow.LiveStepDelay = oldDelay; Close(w); }
    }

    // ───────────── Technik: Grundriss & Wohnen – Claude verschiebt eine Wand ─────────────

    [AvaloniaFact(DisplayName = "T-0775 ECHT Technik: Claude verschiebt im Programm „Grundriss & Wohnen“ eine Wand im offenen Plan – Strg+Z nimmt es zurück (nur mit TRENDO_ECHT_CLAUDE)")]
    public async Task Floorplan_move_wall_real()
    {
        var beleg = Beleg(); const string fall = "grundriss";
        var w = await OpenReal(beleg, fall);
        var log = new StringBuilder(); var tmp = Directory.CreateTempSubdirectory("trendo-t0775-plan-").FullName;
        var oldDelay = MainWindow.LiveStepDelay; MainWindow.LiveStepDelay = 0;
        try
        {
            Head(log, "Grundriss & Wohnen – Claude verschiebt eine Wand", w);
            const string K = "architektur";
            w.Show(K); Pump();
            w.ProgramNew(K, "bad");
            // eigener, gespeicherter Plan (eine nur geöffnete Vorlage übernimmt Claude bewusst nicht, siehe ClaudeEnsureProgram)
            var file = w.ProgramSave(K, Path.Combine(tmp, "Badezimmer" + ProgramDocument.Extension));
            w.Show(K); Pump();
            var doc = w.ProgramActive(K)!;
            File.Copy(file, Path.Combine(beleg, fall + "-vorher.tdoc"), true);
            var wall = FloorPlan.From(doc.Data).Walls.Where(x => Math.Abs(x.X1 - x.X2) < 0.5).OrderByDescending(x => x.X1).First(); // rechte senkrechte Wand
            var (x1, y1, x2, y2) = (wall.X1, wall.Y1, wall.X2, wall.Y2);
            w.ClaudeDockToggle("SPEC-01"); Pump();
            var sw = Stopwatch.StartNew();
            await Run(w.ClaudeChatSend($"Verschiebe im Grundriss die senkrechte Wand mit der id „{wall.Id}“ um 50 cm nach rechts: X1 und X2 dieser Wand jeweils um 50 erhöhen (Maße in cm), Y-Werte bleiben. Sonst nichts ändern."));
            sw.Stop();
            var chat = w.ClaudeChatCurrent()!;
            var raw = CopyRawLog(chat.Folder, beleg, fall);
            Assert.Equal(K, w.CurrentView);
            doc = w.ProgramActive(K)!;
            await Until(() => FloorPlan.From(doc.Data).WallOf(wall.Id) is { } m && Math.Abs(m.X1 - x1) > 1, 15_000);
            var moved = FloorPlan.From(doc.Data).WallOf(wall.Id);
            Assert.NotNull(moved);
            Assert.InRange(moved!.X1 - x1, 49.5, 50.5); Assert.InRange(moved.X2 - x2, 49.5, 50.5);
            Assert.InRange(moved.Y1, y1 - 0.5, y1 + 0.5); Assert.InRange(moved.Y2, y2 - 0.5, y2 + 0.5);
            var undoLabel = doc.UndoLabel;
            var savedFile = w.ProgramSave(K); // wie Strg+S: Claudes Stand als echte Datei
            Assert.InRange(FloorPlan.From(ProgramDocument.Load(savedFile).Data).WallOf(wall.Id)!.X1 - x1, 49.5, 50.5);
            File.Copy(savedFile, Path.Combine(beleg, fall + "-nach-claude.tdoc"), true);
            // Strg+Z auf der Programmseite
            w.Show(K); Pump();
            Visible<Button>(w, "prog-undo").Focus(); Pump();
            CtrlZ(w);
            var back = FloorPlan.From(w.ProgramActive(K)!.Data).WallOf(wall.Id)!;
            Assert.Equal((x1, y1, x2, y2), (back.X1, back.Y1, back.X2, back.Y2));
            log.AppendLine("## Ablauf").AppendLine();
            log.AppendLine($"- Vorlage „Badezimmer“, als eigener Plan gespeichert ({fall}-vorher.tdoc), Claude-Bereich von „Wohnung planen“ geöffnet; Claude bekam das Dokument als .tdoc im Projektordner ({sw.Elapsed.TotalSeconds:F0} s).");
            log.AppendLine($"- Wand {wall.Id}: vorher ({x1:0.#}|{y1:0.#}) → ({x2:0.#}|{y2:0.#}), nach Claude ({moved.X1:0.#}|{moved.Y1:0.#}) → ({moved.X2:0.#}|{moved.Y2:0.#}).");
            log.AppendLine($"- Gespeichert (wie Strg+S): {fall}-nach-claude.tdoc · SHA-256 {Sha(Path.Combine(beleg, fall + "-nach-claude.tdoc"))}");
            log.AppendLine($"- Rückgängig-Schritt: „{undoLabel}“; Strg+Z: Wand wieder an alter Stelle = {(back.X1, back.X2) == (x1, x2)}");
            log.AppendLine($"- Rohlog: {Path.GetFileName(raw)} · SHA-256 {Sha(raw)}").AppendLine();
            log.AppendLine("Ergebnis: BESTANDEN.");
        }
        catch (Exception e) when (e is not Xunit.Sdk.SkipException) { log.AppendLine().AppendLine("FEHLER: " + e.Message); throw; }
        finally { Report(beleg, fall, log); MainWindow.LiveStepDelay = oldDelay; Close(w); try { Directory.Delete(tmp, true); } catch (IOException) { } }
    }

    // ───────────── Zusatz: Code mit KI – „Mit Claude ändern“ ─────────────

    [AvaloniaFact(DisplayName = "T-0775 ECHT Code mit KI: „Mit Claude ändern“ ändert im Projektordner eine Python-Datei, Diff, „Zurücknehmen“ stellt sie her (nur mit TRENDO_ECHT_CLAUDE)")]
    public async Task CodeAi_change_real()
    {
        var beleg = Beleg(); const string fall = "code-ki";
        var w = await OpenReal(beleg, fall);
        var log = new StringBuilder(); var proj = Directory.CreateTempSubdirectory("trendo-t0775-code-").FullName;
        try
        {
            Head(log, "Code mit KI – „Mit Claude ändern“", w);
            var rechner = Path.Combine(proj, "rechner.py"); const string original = "def teile(a, b):\n    return a / b\n";
            File.WriteAllText(rechner, original);
            MainWindow.FolderPickOverride = _ => proj; // Ordnerauswahl (Fenster des Systems) – Claude selbst ist nicht ersetzt
            const string K = "code-ki-programm";
            w.Show(K); Pump(); RealClick(w, w.Find<Button>("prog-tpl-leer")); Pump();
            w.Find<Button>("ca-open-big").Press(); Pump();
            await Until(() => w.ProgramActive(K)!.Text("root") == proj, 5000);
            var d = w.ProgramActive(K)!;
            w.CodeAiOpenFile(d, rechner); Pump();
            d.Set("wish", "Teilen durch null soll einen ValueError mit der Meldung „Durch null kann man nicht teilen.“ auslösen", "Wunsch"); Pump();
            var sw = Stopwatch.StartNew();
            w.Find<Button>("prog-cmd-ca-change").Press(); Pump();
            await Until(() => w.CodeAiClaudeTask is { IsCompleted: true });
            sw.Stop();
            var raw = CopyRawLog(proj, beleg, fall);
            Assert.True(await w.CodeAiClaudeTask!, "Claude hat nichts geändert: " + w.ProgramMessage(K));
            var changed = File.ReadAllText(rechner);
            File.WriteAllText(Path.Combine(beleg, fall + "-rechner-nach-claude.py"), changed);
            Assert.Contains("ValueError", changed);
            Assert.True(w.CodeAiClaudePending);
            var message = w.ProgramMessage(K);
            w.Find<Button>("ca-claude-revert").Press(); Pump();
            Assert.Equal(original, File.ReadAllText(rechner).Replace("\r\n", "\n"));
            Assert.False(w.CodeAiClaudePending);
            log.AppendLine("## Ablauf").AppendLine();
            log.AppendLine($"- Projektordner mit rechner.py, Wunsch eingetragen, Knopf „Mit Claude ändern“; Claude lief im Projektordner ({sw.Elapsed.TotalSeconds:F0} s).");
            log.AppendLine($"- Meldung nach dem Lauf: {message}").AppendLine($"- Meldung nach „Zurücknehmen“: {w.ProgramMessage(K)}");
            log.AppendLine($"- rechner.py nach Claude: siehe {fall}-rechner-nach-claude.py · SHA-256 {Sha(Path.Combine(beleg, fall + "-rechner-nach-claude.py"))}");
            log.AppendLine("- „Zurücknehmen“: Datei wieder wie vorher = True");
            log.AppendLine($"- Rohlog: {Path.GetFileName(raw)} · SHA-256 {Sha(raw)}").AppendLine();
            log.AppendLine("Ergebnis: BESTANDEN.");
        }
        catch (Exception e) when (e is not Xunit.Sdk.SkipException) { log.AppendLine().AppendLine("FEHLER: " + e.Message); throw; }
        finally { Report(beleg, fall, log); MainWindow.FolderPickOverride = null; Close(w); try { Directory.Delete(proj, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }

    // ───────────── Zusatz: KI fragen mit Anhang ─────────────

    [AvaloniaFact(DisplayName = "T-0775 ECHT KI fragen: Frage mit angehängter Textdatei geht an Claude (Abo), Antwort nennt den Inhalt der Datei (nur mit TRENDO_ECHT_CLAUDE)")]
    public async Task Ask_with_attachment_real()
    {
        var beleg = Beleg(); const string fall = "ki-fragen";
        var w = await OpenReal(beleg, fall);
        var log = new StringBuilder(); var tmp = Directory.CreateTempSubdirectory("trendo-t0775-ask-").FullName;
        try
        {
            Head(log, "KI fragen – Frage mit Anhang an Claude", w);
            var note = Path.Combine(tmp, "lieferschein.txt");
            File.WriteAllText(note, "Lieferschein Nr. 4711\nKunde: Erika Muster\nWare: 12 Kartons Druckerpapier A4\nLiefertag: Donnerstag, 20. November\nFahrer: Tour 3");
            w.Show("ki-fragen"); Pump();
            w.AskAttach([note]); Pump();
            var hint = w.Find<TextBlock>("ask-attach-hint").Text ?? "";
            Assert.Contains("Claude", hint); // die Seite sagt ehrlich, dass der Text an Claude geht
            w.Find<TextBox>("ask-input").Text = "Wie viele Kartons werden laut der angehängten Datei geliefert und an welchem Wochentag? Antworte in einem Satz.";
            var sw = Stopwatch.StartNew();
            RealClick(w, w.Find<Button>("ask-send"));
            await Until(() => w.TryFind<Border>("ask-msg-1") is not null && w.Find<Button>("ask-send").IsEnabled);
            sw.Stop();
            var raw = CopyRawLog(Path.Combine(Path.GetTempPath(), "trendo-claude-frage"), beleg, fall);
            var chat = w.Chats.Get(w.CurrentChatId!)!;
            var answer = chat.Messages[1].Content;
            var state = w.Find<TextBlock>("ask-state").Text ?? "";
            Assert.Contains("Claude", state); // Antwort von Claude, nicht von der KI auf dem PC
            Assert.Contains("12", answer);
            Assert.Contains("Donnerstag", answer, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("lieferschein", File.ReadAllText(raw), StringComparison.OrdinalIgnoreCase); // der Dateitext ging mit der Frage an Claude
            log.AppendLine("## Ablauf").AppendLine();
            log.AppendLine("- Seite „KI fragen“, Datei lieferschein.txt angehängt, Frage gesendet (Knopf „Senden“).");
            log.AppendLine($"- Hinweis der Seite: {hint}");
            log.AppendLine($"- Status: {state} ({sw.Elapsed.TotalSeconds:F0} s)");
            log.AppendLine($"- Antwort: {answer.Replace("\n", " ")}");
            log.AppendLine($"- Rohlog: {Path.GetFileName(raw)} · SHA-256 {Sha(raw)}").AppendLine();
            log.AppendLine("Ergebnis: BESTANDEN.");
        }
        catch (Exception e) when (e is not Xunit.Sdk.SkipException) { log.AppendLine().AppendLine("FEHLER: " + e.Message); throw; }
        finally { Report(beleg, fall, log); Close(w); try { Directory.Delete(tmp, true); } catch (IOException) { } }
    }

    // ───────────── ohne Echtlauf: der Schalter ist aus, der Testmodus startet Claude nie ─────────────

    [AvaloniaFact(DisplayName = "T-0775: ohne Schalter startet der Testmodus nie das echte Claude – Anmeldestand „nicht installiert“, Lauf abgelehnt, Claude nicht Haupt-KI")]
    public async Task Switch_off_never_starts_claude()
    {
        Assert.False(MainWindow.ClaudeEchtImTestmodus);
        var (w, _, _) = Ui.Open();
        try
        {
            var st = await w.ClaudeCheck(fresh: true);
            Assert.False(st.Installed); Assert.False(st.Ready);
            var e = await Assert.ThrowsAsync<InvalidOperationException>(() => w.ClaudeRun(["-p", "x"], Path.GetTempPath(), _ => { }, CancellationToken.None));
            Assert.Contains("Testmodus", e.Message);
            Assert.False(w.ClaudeTakesStudioPrompts);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "T-0775: Strg+Z / Strg+Y im Programm „Briefe schreiben“ – nimmt den letzten Schritt zurück und wiederholt ihn (wie die Programm-Hülle)")]
    public void Letter_ctrl_z()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            w.Show("brief"); Pump();
            w.Letter.Body = "Erster Stand."; w.Show("brief"); Pump();
            w.Letter.Body = "Zweiter Stand."; w.Show("brief"); Pump();
            Visible<Button>(w, "letter-undo").Focus(); Pump();
            CtrlZ(w);
            Assert.Equal("Erster Stand.", w.Letter.Body);
            Visible<Button>(w, "letter-undo").Focus(); Pump();
            w.KeyPress(Key.Y, RawInputModifiers.Control, PhysicalKey.Y, null); Pump(); w.KeyRelease(Key.Y, RawInputModifiers.Control, PhysicalKey.Y, null); Pump();
            Assert.Equal("Zweiter Stand.", w.Letter.Body);
        }
        finally { w.Close(); }
    }
}
