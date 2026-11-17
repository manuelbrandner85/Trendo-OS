using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Trendo.Core;

/// <summary>T-0742 (SYS-05): Zeitgesteuerte Abläufe laufen auch, wenn Trendo geschlossen ist – über eine Aufgabe in der Windows-Aufgabenplanung
/// im eigenen Benutzerkonto (schtasks.exe, ohne Administratorrechte, ohne Dienst, ohne Registry). Die Aufgabe startet nur Trendo mit
/// „--ablauf-faellig &lt;Id&gt;“; Trendo führt den Ablauf dann aus, wenn er dran ist, und beendet sich ohne Fenster.
/// Angelegt wird sie nur nach ausdrücklicher Zustimmung in der App, sie trägt einen erkennbaren Namen und lässt sich mit einem Klick entfernen.
/// Grenzen (ehrlich): „neue Datei“ und „von Hand“ laufen nur bei offenem Trendo; Schritte mit KI oder Agent ebenso (die KI hängt an der offenen App);
/// wie jede Benutzeraufgabe ohne gespeichertes Kennwort startet sie nur, solange du angemeldet bist.</summary>
public sealed class WorkflowTaskPlan
{
    public const string Switch = "--ablauf-faellig";

    /// <summary>schtasks mit Argumenten ausführen → (Exitcode, Ausgabe). Tests ersetzen das; im Beleg läuft das echte schtasks.exe.</summary>
    public Func<IReadOnlyList<string>, (int Exit, string Output)> Run { get; set; } = RunSchtasks;

    public static bool Supported => OperatingSystem.IsWindows();

    /// <summary>Erkennbarer, eindeutiger Name: Ablauf-Id plus Kurzzeichen des Datenordners (zwei Profile kommen sich nicht in die Quere).</summary>
    public static string TaskName(string dataRoot, string id) =>
        "Trendo-Ablauf-" + string.Concat(id.Where(char.IsLetterOrDigit)) + "-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Root(dataRoot).ToLowerInvariant())))[..6];

    /// <summary>Datenordner als voller Pfad ohne abschließenden Trenner („…\daten\“ würde in Anführungszeichen das schließende Zeichen maskieren).
    /// Ein Laufwerk allein („C:\“) wird zu „C:\.“, damit auch dort kein Backslash am Ende steht.</summary>
    public static string Root(string dataRoot)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataRoot));
        return Path.EndsInDirectorySeparator(full) ? full + "." : full;
    }

    /// <summary>Ablauf-Ids, die in die Aufgabe dürfen: nur Buchstaben, Ziffern, Bindestrich (die Id steht ohne Anführungszeichen in den Argumenten).</summary>
    public static bool IdOk(string? id) => id is { Length: > 0 and <= 64 } && id.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');

    /// <summary>Warum dieser Ablauf keine Windows-Aufgabe bekommen kann (null = er kann).</summary>
    public static string? WhyNot(Workflow w) =>
        !Supported ? "Die Windows-Aufgabenplanung gibt es nur unter Windows. Abläufe laufen hier nur, solange Trendo offen ist." : WhyNotWithoutApp(w);

    /// <summary>Kann dieser Ablauf ohne offenes Trendo laufen? (null = ja). Prüft auch der Start durch die Aufgabe, unmittelbar vor dem Lauf –
    /// wer nachträglich einen KI- oder Agent-Schritt einfügt, bekommt keinen Lauf ohne offenes Trendo.</summary>
    public static string? WhyNotWithoutApp(Workflow w)
    {
        if (w.Trigger == TriggerKind.Manuell) return "Dieser Ablauf startet nur von Hand.";
        if (w.Trigger == TriggerKind.NeueDatei) return "„Neue Datei im Ordner“ prüft Trendo nur, solange es offen ist – dafür gibt es keine Windows-Aufgabe.";
        if (w.Steps.Any(s => s.Kind is StepKind.KiZusammenfassen or StepKind.AgentAusfuehren)) return "Schritte mit KI oder Agent brauchen das offene Trendo – dieser Ablauf läuft nur, solange Trendo offen ist.";
        if (w.Trigger == TriggerKind.Taeglich && !TimeOnly.TryParse(w.At, out _)) return "Die Uhrzeit ist ungültig.";
        if (w.Trigger == TriggerKind.Intervall && ScheduleKey(w).Length == 0) return "Das Intervall muss zwischen 1 Minute und 31 Tagen liegen.";
        return null;
    }

    /// <summary>Kurzbeschreibung des Zeitplans; wird gespeichert, um eine veraltete Aufgabe zu erkennen („" = nicht abbildbar).</summary>
    public static string ScheduleKey(Workflow w)
    {
        if (w.Trigger == TriggerKind.Taeglich && TimeOnly.TryParse(w.At, out var at))
            return w.Days.Count is > 0 and < 7 ? $"WEEKLY {string.Join(",", Week(w.Days))} {at:HH:mm}" : $"DAILY {at:HH:mm}";
        if (w.Trigger == TriggerKind.Intervall && w.EveryMinutes is > 0 and <= 31 * 1440) return $"EVERY {w.EveryMinutes}M";
        return "";
    }

    static IEnumerable<string> Week(IEnumerable<DayOfWeek> days) => days.Distinct().OrderBy(d => ((int)d + 6) % 7).Select(d => d.ToString());

    /// <summary>Argumente, mit denen die Aufgabe Trendo startet: nur der Schalter, die Ablauf-Id und der Datenordner.</summary>
    public static string Arguments(string id, string dataRoot) =>
        IdOk(id) ? $"{Switch} {id} --data-root \"{Root(dataRoot)}\"" : throw new ArgumentException("Ungültige Ablauf-Kennung für die Windows-Aufgabe.", nameof(id));

    /// <summary>Befehl zur Anzeige in der Rückfrage.</summary>
    public static string Command(string exe, string id, string dataRoot) => $"\"{exe}\" {Arguments(id, dataRoot)}";

    /// <summary>Aufgabendefinition (Task-Scheduler-XML 1.2) – Programm und Argumente getrennt (keine 261-Zeichen-Grenze von /TR).
    /// Kein Benutzerkonto, kein Kennwort: Anmeldeart „interaktiv“ und „geringste Rechte“ – die Aufgabe läuft im eigenen Konto, solange man angemeldet ist.
    /// „Verpassten Start nachholen“ ist an: war der PC zur Uhrzeit aus, läuft der Ablauf beim nächsten Anmelden (Trendo prüft trotzdem, ob er dran ist).</summary>
    public static string Xml(Workflow w, string exe, string dataRoot, DateTime now)
    {
        static string E(string v) => System.Security.SecurityElement.Escape(v) ?? "";
        string trigger;
        if (w.Trigger == TriggerKind.Taeglich && TimeOnly.TryParse(w.At, out var at))
        {
            var start = (now.Date + at.ToTimeSpan()).ToString("yyyy-MM-ddTHH:mm:ss");
            trigger = w.Days.Count is > 0 and < 7
                ? $"<CalendarTrigger><StartBoundary>{start}</StartBoundary><Enabled>true</Enabled><ScheduleByWeek><DaysOfWeek>{string.Concat(Week(w.Days).Select(d => $"<{d} />"))}</DaysOfWeek><WeeksInterval>1</WeeksInterval></ScheduleByWeek></CalendarTrigger>"
                : $"<CalendarTrigger><StartBoundary>{start}</StartBoundary><Enabled>true</Enabled><ScheduleByDay><DaysInterval>1</DaysInterval></ScheduleByDay></CalendarTrigger>";
        }
        else if (w.Trigger == TriggerKind.Intervall && ScheduleKey(w).Length > 0)
            trigger = $"<TimeTrigger><StartBoundary>{now.AddMinutes(1):yyyy-MM-ddTHH:mm:00}</StartBoundary><Enabled>true</Enabled><Repetition><Interval>PT{w.EveryMinutes}M</Interval><StopAtDurationEnd>false</StopAtDurationEnd></Repetition></TimeTrigger>";
        else throw new InvalidOperationException(WhyNot(w) ?? "Kein Zeitplan.");
        return "<?xml version=\"1.0\" encoding=\"UTF-16\"?>\n" +
            "<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">" +
            $"<RegistrationInfo><Author>Trendo OS</Author><Description>{E($"Von Trendo OS angelegt (mit deiner Zustimmung): startet den Ablauf „{w.Name}“ ({w.Id}), auch wenn Trendo geschlossen ist. In Trendo unter „Abläufe“ mit einem Klick entfernbar.")}</Description></RegistrationInfo>" +
            $"<Triggers>{trigger}</Triggers>" +
            "<Principals><Principal id=\"Author\"><LogonType>InteractiveToken</LogonType><RunLevel>LeastPrivilege</RunLevel></Principal></Principals>" +
            "<Settings><MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy><DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries><StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>" +
            "<StartWhenAvailable>true</StartWhenAvailable><ExecutionTimeLimit>PT2H</ExecutionTimeLimit><Enabled>true</Enabled><Hidden>false</Hidden></Settings>" +
            $"<Actions Context=\"Author\"><Exec><Command>{E(exe)}</Command><Arguments>{E(Arguments(w.Id, dataRoot))}</Arguments></Exec></Actions></Task>";
    }

    /// <summary>Windows-Aufgabe anlegen (bzw. erneuern). Nur nach Zustimmung aufrufen. Liefert null bei Erfolg, sonst die Meldung.</summary>
    public string? Create(Workflow w, string exe, string dataRoot, AuditLog audit, Principal by)
    {
        if (WhyNot(w) is { } why) return why;
        if (!IdOk(w.Id)) return "Die Kennung dieses Ablaufs eignet sich nicht für eine Windows-Aufgabe.";
        if (!File.Exists(exe)) return "Das Trendo-Programm wurde nicht gefunden: " + exe;
        var dir = Path.Combine(dataRoot, "ablaeufe"); Directory.CreateDirectory(dir);
        var xml = Path.Combine(dir, $"aufgabe-{string.Concat(w.Id.Where(char.IsLetterOrDigit))}.xml");
        File.WriteAllText(xml, Xml(w, exe, dataRoot, DateTime.Now), Encoding.Unicode);
        try
        {
            var (exit, output) = Run(["/Create", "/TN", TaskName(dataRoot, w.Id), "/XML", xml, "/F"]);
            audit.Append(by.Name, exit == 0 ? "workflow.windows-aufgabe.angelegt" : "workflow.windows-aufgabe.fehler", $"{w.Id} {TaskName(dataRoot, w.Id)} {ScheduleKey(w)} exit={exit}");
            return exit != 0 ? "Die Windows-Aufgabe ließ sich nicht anlegen: " + Short(output) : null;
        }
        finally { try { File.Delete(xml); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { } }
    }

    /// <summary>Gibt es die Aufgabe in der Windows-Aufgabenplanung?</summary>
    public bool Exists(string dataRoot, string id) => Supported && Run(["/Query", "/TN", TaskName(dataRoot, id)]).Exit == 0;

    /// <summary>Aufgabe entfernen (ein Klick in der App, auch beim Löschen des Ablaufs). true = jetzt keine Aufgabe mehr da.</summary>
    public bool Delete(Workflow w, string dataRoot, AuditLog audit, Principal by)
    {
        if (!Supported) return true;
        var name = TaskName(dataRoot, w.Id);
        if (Run(["/Query", "/TN", name]).Exit != 0) return true;
        var (exit, output) = Run(["/Delete", "/TN", name, "/F"]);
        audit.Append(by.Name, exit == 0 ? "workflow.windows-aufgabe.entfernt" : "workflow.windows-aufgabe.fehler", $"{w.Id} {name} exit={exit} {Short(output)}");
        return exit == 0;
    }

    /// <summary>Eine Aufgabe, die Trendo selbst angelegt hat (eigenes Namensmuster, startet genau dieses Programm mit „--ablauf-faellig“).</summary>
    public sealed record OwnTask(string Name, string Id, string DataRoot);

    static readonly Regex OwnName = new(@"^\\?(Trendo-Ablauf-[A-Za-z0-9]+-[0-9A-F]{6})$", RegexOptions.CultureInvariant);
    static readonly Regex OwnArgs = new("^" + Regex.Escape(Switch) + @" ([A-Za-z0-9-]+) --data-root ""([^""]+)""$", RegexOptions.CultureInvariant);
    static readonly Regex TaskBlock = new(@"<Task\b.*?</Task>", RegexOptions.Singleline | RegexOptions.CultureInvariant);

    /// <summary>Eigene Aufgaben aus der Ausgabe von „schtasks /Query /XML ONE“ (alle Aufgaben des Kontos als ein XML). Fremde Aufgaben, andere Programme
    /// oder andere Argumente zählen nicht – angefasst wird nur, was eindeutig von diesem Trendo stammt.</summary>
    public static IReadOnlyList<OwnTask> ParseOwn(string xml, string exe)
    {
        var res = new List<OwnTask>(); var me = Path.GetFullPath(exe);
        static string? Tag(string block, string tag) => Regex.Match(block, $"<{tag}>(.*?)</{tag}>", RegexOptions.Singleline) is { Success: true } m ? System.Net.WebUtility.HtmlDecode(m.Groups[1].Value).Trim() : null;
        foreach (Match b in TaskBlock.Matches(xml))
        {
            if (Tag(b.Value, "URI") is not { } uri || OwnName.Match(uri) is not { Success: true } n) continue;
            if (Tag(b.Value, "Command")?.Trim('"') is not { Length: > 0 } cmd) continue;
            try { if (!string.Equals(Path.GetFullPath(cmd), me, StringComparison.OrdinalIgnoreCase)) continue; } catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { continue; }
            if (Tag(b.Value, "Arguments") is not { } args || OwnArgs.Match(args) is not { Success: true } a) continue;
            res.Add(new OwnTask(n.Groups[1].Value, a.Groups[1].Value, a.Groups[2].Value));
        }
        return res;
    }

    /// <summary>Alle eigenen Aufgaben dieses Programms (null = die Abfrage ging nicht). Ohne Administratorrechte: nur die Aufgaben, die das Konto sehen darf.</summary>
    public IReadOnlyList<OwnTask>? ListOwn(string exe)
    {
        if (!Supported) return [];
        var (exit, output) = Run(["/Query", "/XML", "ONE"]);
        return exit == 0 ? ParseOwn(output, exe) : null;
    }

    /// <summary>Passt die Aufgabe noch zu einem Ablauf? Nein, wenn ihr Datenordner fehlt oder der Ablauf dort nicht (mehr) steht.
    /// Ist die Ablaufdatei unlesbar, gilt sie als passend – im Zweifel bleibt die Aufgabe.</summary>
    public static bool Matches(OwnTask t)
    {
        if (!Directory.Exists(t.DataRoot)) return false;
        if (TaskName(t.DataRoot, t.Id) != t.Name) return false; // Name passt nicht zu Datenordner/Id (z. B. alter Ordnername)
        var file = Path.Combine(t.DataRoot, "ablaeufe", "ablaeufe.json");
        if (!File.Exists(file)) return false;
        try { return JsonSerializer.Deserialize<List<Workflow>>(File.ReadAllText(file)) is not { } list || list.Any(w => w?.Id == t.Id); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return true; }
    }

    /// <summary>Beim Start: eigene Aufgaben entfernen, die zu keinem Ablauf mehr passen (Ablauf gelöscht, Datenordner gewechselt oder weg).
    /// Nur Aufgaben mit eigenem Namensmuster, die auf dieses Programm zeigen. Liefert die entfernten Namen.</summary>
    public IReadOnlyList<string> CleanupOrphans(string exe, Action<string>? log = null)
    {
        var gone = new List<string>();
        foreach (var t in ListOwn(exe) ?? [])
            if (!Matches(t) && DeleteByName(t.Name, log)) gone.Add(t.Name);
        return gone;
    }

    /// <summary>Beim Deinstallieren: alle eigenen Aufgaben dieses Programms entfernen. Liefert die Anzahl der entfernten.</summary>
    public int RemoveAllOwn(string exe, Action<string>? log = null) => (ListOwn(exe) ?? []).Count(t => DeleteByName(t.Name, log));

    /// <summary>Start durch eine Aufgabe, deren Datenordner es nicht mehr gibt: nichts anlegen, die eigene Aufgabe entfernen. true = Ordner fehlt.</summary>
    public bool RemoveIfRootMissing(string id, string dataRoot, Action<string>? log = null)
    {
        if (Directory.Exists(Root(dataRoot))) return false;
        if (Supported && IdOk(id)) DeleteByName(TaskName(dataRoot, id), log);
        return true;
    }

    /// <summary>Eigene Aufgabe über ihren Namen entfernen (nur das eigene Namensmuster).</summary>
    public bool DeleteByName(string name, Action<string>? log = null)
    {
        if (!Supported || OwnName.Match(name) is not { Success: true }) return false;
        var (exit, output) = Run(["/Delete", "/TN", name, "/F"]);
        log?.Invoke(exit == 0 ? $"Windows-Aufgabe entfernt: {name}" : $"Windows-Aufgabe {name} ließ sich nicht entfernen: {Short(output)}");
        return exit == 0;
    }

    static string Short(string s) { s = s.Replace("\r", " ").Replace("\n", " ").Trim(); return s.Length > 200 ? s[..200] + " …" : s; }

    /// <summary>schtasks schreibt in der OEM-Codepage der Konsole (z. B. 850) – sonst kommen Umlaute in Meldungen kaputt an.</summary>
    public static Encoding Oem()
    {
        try { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); return Encoding.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.OEMCodePage); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException) { return Encoding.UTF8; }
    }

    static (int, string) RunSchtasks(IReadOnlyList<string> args)
    {
        var exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "schtasks.exe");
        var psi = new ProcessStartInfo(File.Exists(exe) ? exe : "schtasks.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Oem(), StandardErrorEncoding = Oem() };
        foreach (var a in args) psi.ArgumentList.Add(a);
        try
        {
            using var p = Process.Start(psi)!;
            var o = p.StandardOutput.ReadToEndAsync(); var e = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(30_000)) { try { p.Kill(); } catch (InvalidOperationException) { } return (-1, "Zeitüberschreitung"); }
            return (p.ExitCode, o.Result + e.Result);
        }
        catch (System.ComponentModel.Win32Exception ex) { return (-1, ex.Message); }
    }
}
