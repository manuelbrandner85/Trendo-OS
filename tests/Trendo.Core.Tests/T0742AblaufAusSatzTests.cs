using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0742 (SYS-05): Aus einem Satz wird ein echter Ablauf der vorhandenen <see cref="WorkflowEngine"/> (Auslöser, Bedingung, Schritte),
/// zeitgesteuerte Abläufe laufen über eine Windows-Aufgabe auch bei geschlossenem Trendo. Hier ohne KI und mit ersetztem schtasks –
/// der Echtlauf mit Claude und dem echten schtasks.exe steht in <c>T0742_Echtlauf_Ui</c> (Beleg reports/T-0742/).</summary>
public class T0742_Ablauf_aus_Satz
{
    static (Fixture F, WorkflowEngine E, string Home) Make()
    {
        var f = new Fixture(); var home = Path.Combine(f.Root, "home"); Directory.CreateDirectory(home);
        return (f, new WorkflowEngine(f.Root, f.Core.Audit, f.Core.Approvals, [home]), home);
    }

    const string Antwort = """
        Gern, hier ist der Ablauf:
        ```json
        {"name": "Wochenbericht", "ausloeser": {"art": "taeglich", "uhrzeit": "17:00", "wochentage": ["mo", "Freitag"]},
         "schritte": [{"art": "ordner_anlegen", "nach": "~/Berichte"},
                      {"art": "text_schreiben", "nach": "~/Berichte/bericht-{datum}.txt", "text": "Bericht vom {datum}"},
                      {"art": "zippen", "von": "~/Berichte", "nach": "~/Archiv/berichte-{datum}.zip"}]}
        ```
        """;

    [Fact(DisplayName = "T-0742: KI-Antwort → echter Ablauf mit Auslöser, Wochentag-Bedingung und Schritten; Pfade im Benutzerordner; AUS bis zur Bestätigung")]
    public void Antwort_wird_echter_Ablauf()
    {
        var home = Path.Combine(Path.GetTempPath(), "t0742-heim");
        var r = WorkflowSentence.FromAnswer(Antwort, home, "Jeden Montag und Freitag um 17 Uhr einen Bericht schreiben und sichern");
        Assert.Empty(r.Problems);
        var w = r.Workflow!;
        Assert.False(w.Enabled);
        Assert.Equal("Jeden Montag und Freitag um 17 Uhr einen Bericht schreiben und sichern", w.Origin);
        Assert.Equal(TriggerKind.Taeglich, w.Trigger); Assert.Equal("17:00", w.At);
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Friday], w.Days);
        Assert.Equal([StepKind.OrdnerAnlegen, StepKind.TextSchreiben, StepKind.Zippen], w.Steps.Select(s => s.Kind));
        Assert.Equal(Path.Combine(home, "Berichte", "bericht-{datum}.txt"), w.Steps[1].To);
        Assert.Equal(Path.Combine(home, "Berichte"), w.Steps[2].From);
    }

    [Theory(DisplayName = "T-0742: Unsichere oder unlesbare KI-Antworten legen nichts an")]
    [InlineData("Ich kann das leider nicht.", "keinen Ablauf")]
    [InlineData("""{"name":"x","ausloeser":{"art":"manuell"},"schritte":[{"art":"ordner_anlegen","nach":"C:/Windows/Neu"}]}""", "außerhalb")]
    [InlineData("""{"name":"x","ausloeser":{"art":"manuell"},"schritte":[{"art":"ordner_anlegen","nach":"~/../fremd"}]}""", "..")]
    [InlineData("""{"name":"x","ausloeser":{"art":"manuell"},"schritte":[{"art":"loeschen","von":"~/Downloads"}]}""", "unbekannte Art")]
    [InlineData("""{"name":"x","ausloeser":{"art":"manuell"},"schritte":[{"art":"agent_ausfuehren","text":"a1"}]}""", "Agenten")]
    [InlineData("""{"name":"x","ausloeser":{"art":"taeglich","uhrzeit":"25:99"},"schritte":[{"art":"ordner_anlegen","nach":"~/a"}]}""", "Uhrzeit")]
    [InlineData("""{"name":"x","ausloeser":{"art":"manuell"},"schritte":[]}""", "keine Schritte")]
    [InlineData("""{"name":"x","ausloeser":{"art":"manuell"},"schritte":[{"art":"kopieren","nach":"~/a"}]}""", "„von“ fehlt")]
    public void Unsicheres_wird_abgelehnt(string answer, string expected)
    {
        var r = WorkflowSentence.FromAnswer(answer, Path.Combine(Path.GetTempPath(), "t0742-heim"), "Satz");
        Assert.Null(r.Workflow);
        Assert.Contains(r.Problems, p => p.Contains(expected));
    }

    [Fact(DisplayName = "T-0742: Bedingung Wochentage – nur an den genannten Tagen fällig; ohne Wochentage bleibt die alte Freigabe-Prüfsumme gleich")]
    public void Wochentage_und_alte_Freigaben()
    {
        var w = new Workflow { Trigger = TriggerKind.Taeglich, At = "08:00", Steps = [new() { Kind = StepKind.Verschieben, From = "a", To = "b" }] };
        var alt = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { w.Trigger, w.At, w.EveryMinutes, w.WatchFolder, w.WatchPattern, w.Steps }))))[..16];
        Assert.Equal(alt, w.Hash()); // gespeicherte Freigaben älterer Abläufe bleiben gültig
        w.Days = [DayOfWeek.Friday];
        Assert.NotEqual(alt, w.Hash()); // Bedingung geändert = neue Freigabe
        var montag = new DateTimeOffset(2026, 11, 16, 9, 0, 0, TimeSpan.Zero); var freitag = montag.AddDays(4);
        Assert.False(WorkflowEngine.IsTimeDue(w, montag));
        Assert.True(WorkflowEngine.IsTimeDue(w, freitag));
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Friday], WorkflowSentence.ParseDays("montags und Freitag"));
        Assert.Empty(WorkflowSentence.ParseDays("")!);
        Assert.Null(WorkflowSentence.ParseDays("Feiertag"));
        Assert.Equal("Mo, Fr", WorkflowSentence.DaysText([DayOfWeek.Friday, DayOfWeek.Monday]));
    }

    [Fact(DisplayName = "T-0742: Start durch die Windows-Aufgabe – genau dieser Ablauf, nur wenn eingeschaltet und dran; legt die Datei wirklich an; danach nicht doppelt")]
    public async Task Einzelstart_wenn_dran()
    {
        var (f, e, home) = Make(); using var _ = f;
        var w = e.Add(new Workflow { Name = "Notiz", Enabled = false, Origin = "Satz", Trigger = TriggerKind.Taeglich, At = "08:00",
            Steps = [new() { Kind = StepKind.TextSchreiben, To = Path.Combine(home, "notiz-{datum}.txt"), Text = "Hallo {datum}" }] });
        var tag = new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero);
        var (r0, why0) = await e.RunOneIfDueAsync(w.Id, tag);
        Assert.Null(r0); Assert.Contains("ausgeschaltet", why0); // ohne Bestätigung läuft nichts
        Assert.False(File.Exists(Path.Combine(home, "notiz-2026-11-17.txt")));
        var req = e.RequestEnable(w); f.Core.Approvals.Approve(req.Id, Principal.LocalOwner); e.ApplyEnable(w, req.Id);
        Assert.True(w.Enabled);
        var (r1, _) = await e.RunOneIfDueAsync(w.Id, tag);
        Assert.True(r1!.Ok);
        Assert.Equal("Hallo 2026-11-17", File.ReadAllText(Path.Combine(home, "notiz-2026-11-17.txt")));
        var (r2, why2) = await e.RunOneIfDueAsync(w.Id, tag.AddMinutes(5));
        Assert.Null(r2); Assert.Contains("nicht dran", why2);
        Assert.Null((await e.RunOneIfDueAsync("gibtsnicht", tag)).Run);
    }

    [Fact(DisplayName = "T-0742: Läuft der Ablauf in einem zweiten Prozess (Windows-Aufgabe), übernimmt das offene Trendo den Laufstand – kein Doppellauf, nichts überschrieben")]
    public async Task Zweiter_Prozess_kein_Doppellauf()
    {
        var (f, app, home) = Make(); using var _ = f;
        var w = app.Add(new Workflow { Name = "Zip", Trigger = TriggerKind.Intervall, EveryMinutes = 60,
            Steps = [new() { Kind = StepKind.TextSchreiben, To = Path.Combine(home, "lauf-{zeit}.txt"), Text = "x" }] });
        var t = new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero);
        var aufgabe = new WorkflowEngine(f.Root, f.Core.Audit, f.Core.Approvals, [home]); // zweiter Prozess, liest die Datei frisch
        Assert.True((await aufgabe.RunOneIfDueAsync(w.Id, t)).Run!.Ok);
        File.SetLastWriteTimeUtc(Path.Combine(f.Root, "ablaeufe", "ablaeufe.json"), DateTime.UtcNow.AddSeconds(5)); // Zeitstempel sicher anders
        Assert.Empty(await app.RunDueAsync(t.AddMinutes(1))); // das offene Trendo sieht den Lauf und startet nicht noch einmal
        Assert.Equal(t, app.Get(w.Id)!.LastRun);
        Assert.Single(Directory.GetFiles(home, "lauf-*.txt"));
        app.Update(w); // Speichern im offenen Trendo überschreibt den Laufstand nicht
        Assert.Equal(t, new WorkflowEngine(f.Root, f.Core.Audit, f.Core.Approvals, [home]).Get(w.Id)!.LastRun);
    }

    [Fact(DisplayName = "T-0742: Windows-Aufgabe – Zeitplan je Auslöser, nur eigenes Konto (kein Konto/Kennwort, geringste Rechte), Grenzen ehrlich benannt")]
    public void Aufgabe_Argumente_und_Grenzen()
    {
        var root = Path.Combine(Path.GetTempPath(), "t0742-daten");
        var daily = new Workflow { Id = "abc12345", Trigger = TriggerKind.Taeglich, At = "07:30", Steps = [new() { Kind = StepKind.TextSchreiben, To = "x" }] };
        var exe = @"C:\Programme\Trendo\Trendo.App.exe";
        var now = new DateTime(2026, 11, 17, 10, 0, 0);
        var xml = System.Xml.Linq.XDocument.Parse(WorkflowTaskPlan.Xml(daily, exe, root, now));
        System.Xml.Linq.XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
        var exec = xml.Descendants(ns + "Exec").Single();
        Assert.Equal(exe, exec.Element(ns + "Command")!.Value); // nur Trendo selbst
        Assert.Equal(@$"--ablauf-faellig abc12345 --data-root ""{Path.GetFullPath(root)}""", exec.Element(ns + "Arguments")!.Value);
        var principal = xml.Descendants(ns + "Principal").Single();
        Assert.Equal("InteractiveToken", principal.Element(ns + "LogonType")!.Value); // eigenes Konto, kein gespeichertes Kennwort
        Assert.Equal("LeastPrivilege", principal.Element(ns + "RunLevel")!.Value); // keine Administratorrechte
        Assert.Null(principal.Element(ns + "UserId"));
        Assert.Equal("2026-11-17T07:30:00", xml.Descendants(ns + "StartBoundary").Single().Value);
        Assert.Single(xml.Descendants(ns + "ScheduleByDay"));
        Assert.Contains("Trendo OS", xml.Descendants(ns + "Description").Single().Value);
        Assert.Equal("DAILY 07:30", WorkflowTaskPlan.ScheduleKey(daily));
        Assert.StartsWith("Trendo-Ablauf-abc12345-", WorkflowTaskPlan.TaskName(root, "abc12345"));
        Assert.NotEqual(WorkflowTaskPlan.TaskName(root, "abc12345"), WorkflowTaskPlan.TaskName(root + "2", "abc12345"));
        daily.Days = [DayOfWeek.Friday, DayOfWeek.Monday]; // Bedingung → wöchentlicher Zeitplan
        var week = System.Xml.Linq.XDocument.Parse(WorkflowTaskPlan.Xml(daily, exe, root, now)).Descendants(ns + "DaysOfWeek").Single();
        Assert.Equal(["Monday", "Friday"], week.Elements().Select(e => e.Name.LocalName));
        Assert.Equal("WEEKLY Monday,Friday 07:30", WorkflowTaskPlan.ScheduleKey(daily));
        var every = new Workflow { Trigger = TriggerKind.Intervall, EveryMinutes = 120 };
        Assert.Equal("PT120M", System.Xml.Linq.XDocument.Parse(WorkflowTaskPlan.Xml(every, exe, root, now)).Descendants(ns + "Interval").Single().Value);
        Assert.Equal("", WorkflowTaskPlan.ScheduleKey(new Workflow { Trigger = TriggerKind.Intervall, EveryMinutes = 32 * 1440 }));
        // lange Pfade sind kein Problem mehr (Programm und Argumente getrennt)
        Assert.Contains(new string('x', 300), WorkflowTaskPlan.Xml(daily, exe, Path.Combine(root, new string('x', 300)), now));
        if (WorkflowTaskPlan.Supported)
        {
            Assert.Contains("neue Datei", WorkflowTaskPlan.WhyNot(new Workflow { Trigger = TriggerKind.NeueDatei })!, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("von Hand", WorkflowTaskPlan.WhyNot(new Workflow { Trigger = TriggerKind.Manuell })!);
            Assert.Contains("KI", WorkflowTaskPlan.WhyNot(new Workflow { Trigger = TriggerKind.Taeglich, Steps = [new() { Kind = StepKind.KiZusammenfassen }] })!);
            Assert.Null(WorkflowTaskPlan.WhyNot(daily));
        }
        else Assert.NotNull(WorkflowTaskPlan.WhyNot(daily));
    }

    [Fact(DisplayName = "T-0742: Windows-Aufgabe anlegen/prüfen/entfernen ruft schtasks mit genau diesen Befehlen auf und steht im Protokoll (schtasks ersetzt)")]
    public void Aufgabe_anlegen_und_entfernen()
    {
        if (!WorkflowTaskPlan.Supported) Ueberspringen.Weil("nur unter Windows.");
        using var f = new Fixture();
        var exe = Path.Combine(f.Root, "Trendo.App.exe"); File.WriteAllText(exe, "");
        var calls = new List<string>(); var exists = false;
        var plan = new WorkflowTaskPlan { Run = a => { calls.Add(string.Join(" ", a.Take(1))); if (a[0] == "/Create") exists = true; if (a[0] == "/Delete") exists = false; return (a[0] == "/Query" && !exists ? 1 : 0, ""); } };
        var w = new Workflow { Trigger = TriggerKind.Taeglich, At = "06:00", Steps = [new() { Kind = StepKind.TextSchreiben, To = "x" }] };
        var made = plan.Create(w, exe, f.Root, f.Core.Audit, Principal.LocalOwner); Assert.True(made is null, made);
        Assert.True(plan.Exists(f.Root, w.Id));
        Assert.True(plan.Delete(w, f.Root, f.Core.Audit, Principal.LocalOwner));
        Assert.False(plan.Exists(f.Root, w.Id));
        Assert.Equal(["/Create", "/Query", "/Query", "/Delete", "/Query"], calls);
        Assert.Empty(Directory.GetFiles(Path.Combine(f.Root, "ablaeufe"), "aufgabe-*.xml")); // Hilfsdatei wieder weg
        Assert.NotNull(plan.Create(w, Path.Combine(f.Root, "fehlt.exe"), f.Root, f.Core.Audit, Principal.LocalOwner)); // ohne Programm keine Aufgabe
        var audit = File.ReadAllText(Path.Combine(f.Root, "audit", "audit.jsonl"));
        Assert.Contains("workflow.windows-aufgabe.angelegt", audit); Assert.Contains("workflow.windows-aufgabe.entfernt", audit);
    }
}
