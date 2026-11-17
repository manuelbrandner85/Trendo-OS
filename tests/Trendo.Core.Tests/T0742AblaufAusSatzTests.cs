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
    // ---------- Gegenprüfung T-0742: Aufgabenprozess schreibt nur seine Laufstände, prüft vorher frisch ----------
    static string Datei(Fixture f) => Path.Combine(f.Root, "ablaeufe", "ablaeufe.json");
    static List<Workflow> Frisch(Fixture f) => JsonSerializer.Deserialize<List<Workflow>>(File.ReadAllText(Datei(f)))!;

    [Fact(DisplayName = "T-0742 Gegenprüfung: Pausiert das offene Trendo den Ablauf, schaltet ihn die Windows-Aufgabe nicht wieder ein und läuft nicht")]
    public async Task Pausieren_bleibt_erhalten()
    {
        var (f, app, home) = Make(); using var _ = f;
        var w = app.Add(new Workflow { Name = "W", Trigger = TriggerKind.Intervall, EveryMinutes = 60,
            Steps = [new() { Kind = StepKind.TextSchreiben, To = Path.Combine(home, "w-{zeit}.txt"), Text = "x" }] });
        var aufgabe = new WorkflowEngine(f.Root, f.Core.Audit, f.Core.Approvals, [home]); // Aufgabenprozess hat die Datei schon geladen
        w.Enabled = false; app.Update(w); // offenes Trendo: pausieren
        var (run, why) = await aufgabe.RunOneIfDueAsync(w.Id, new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero));
        Assert.Null(run); Assert.Contains("ausgeschaltet", why);
        Assert.False(Frisch(f).Single(x => x.Id == w.Id).Enabled);
        Assert.Empty(Directory.GetFiles(home, "w-*.txt"));
    }

    [Fact(DisplayName = "T-0742 Gegenprüfung: Während die Aufgabe läuft, bleiben neu angelegte, gelöschte und freigegebene Abläufe des offenen Trendo erhalten")]
    public async Task Neu_geloescht_Freigabe_bleiben()
    {
        var (f, app, home) = Make(); using var _ = f;
        var w = app.Add(new Workflow { Name = "W", Trigger = TriggerKind.Intervall, EveryMinutes = 60,
            Steps = [new() { Kind = StepKind.TextSchreiben, To = Path.Combine(home, "w-{zeit}.txt"), Text = "x" }] });
        var y = app.Add(new Workflow { Name = "Y", Trigger = TriggerKind.Manuell, Steps = [new() { Kind = StepKind.OrdnerAnlegen, To = Path.Combine(home, "y") }] });
        var z = app.Add(new Workflow { Name = "Z", Trigger = TriggerKind.Manuell, Steps = [new() { Kind = StepKind.Verschieben, From = home, To = Path.Combine(home, "z"), Pattern = "*.pdf" }] });
        var aufgabe = new WorkflowEngine(f.Root, f.Core.Audit, f.Core.Approvals, [home]);
        var x = app.Add(new Workflow { Name = "X", Trigger = TriggerKind.Manuell, Steps = [new() { Kind = StepKind.OrdnerAnlegen, To = Path.Combine(home, "x") }] });
        app.Remove(y.Id);
        var req = app.RequestApproval(z); f.Core.Approvals.Approve(req.Id, Principal.LocalOwner); app.ApplyApproval(z, req.Id);
        var t = new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero);
        Assert.True((await aufgabe.RunOneIfDueAsync(w.Id, t)).Run!.Ok);
        var disk = Frisch(f);
        Assert.Contains(disk, d => d.Id == x.Id);
        Assert.DoesNotContain(disk, d => d.Id == y.Id);
        Assert.True(disk.Single(d => d.Id == z.Id).IsApproved);
        Assert.Equal(t, disk.Single(d => d.Id == w.Id).LastRun); // der Laufstand der Aufgabe ist da
        app.Update(app.Get(z.Id)!); // offenes Trendo speichert danach: Laufstand der Aufgabe bleibt
        Assert.Equal(t, Frisch(f).Single(d => d.Id == w.Id).LastRun);
    }

    [Fact(DisplayName = "T-0742 Gegenprüfung: Nachträglich eingefügter KI-Schritt oder geänderter freigabepflichtiger Inhalt – die Windows-Aufgabe führt nichts aus")]
    public async Task KI_Schritt_und_Freigabe_vor_dem_Lauf()
    {
        var (f, app, home) = Make(); using var _ = f;
        var w = app.Add(new Workflow { Name = "W", Trigger = TriggerKind.Intervall, EveryMinutes = 60,
            Steps = [new() { Kind = StepKind.TextSchreiben, To = Path.Combine(home, "w.txt"), Text = "x" }] });
        w.Steps.Add(new() { Kind = StepKind.KiZusammenfassen, From = home }); app.Update(w);
        var t = new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero);
        var (r1, why1) = await new WorkflowEngine(f.Root, f.Core.Audit, f.Core.Approvals, [home]).RunOneIfDueAsync(w.Id, t);
        Assert.Null(r1); Assert.Contains("offen", why1);
        Assert.False(File.Exists(Path.Combine(home, "w.txt")));
        var v = app.Add(new Workflow { Name = "V", Trigger = TriggerKind.Intervall, EveryMinutes = 60,
            Steps = [new() { Kind = StepKind.Verschieben, From = home, To = Path.Combine(home, "ziel"), Pattern = "*.pdf" }] });
        var req = app.RequestApproval(v); f.Core.Approvals.Approve(req.Id, Principal.LocalOwner); app.ApplyApproval(v, req.Id);
        v.Steps[0].Pattern = "*.*"; app.Update(v); // Inhalt geändert → Freigabe gilt nicht mehr
        var (r2, why2) = await new WorkflowEngine(f.Root, f.Core.Audit, f.Core.Approvals, [home]).RunOneIfDueAsync(v.Id, t);
        Assert.Null(r2); Assert.Contains("Freigabe", why2);
        Assert.Equal(0, Frisch(f).Single(d => d.Id == v.Id).FailCount); // kein Fehlversuch eingetragen
    }

    [Theory(DisplayName = "T-0742 Gegenprüfung: Argumente der Aufgabe – Id nur aus Buchstaben, Ziffern, Bindestrich; Datenordner ohne abschließenden Backslash")]
    [InlineData("abc 12")]
    [InlineData("abc\"12")]
    [InlineData("")]
    [InlineData("a&b")]
    public void Arguments_lehnt_unsichere_Id_ab(string id) =>
        Assert.Throws<ArgumentException>(() => WorkflowTaskPlan.Arguments(id, Path.GetTempPath()));

    [Fact(DisplayName = "T-0742 Gegenprüfung: Datenordner mit Backslash am Ende zerbricht die Anführungszeichen nicht")]
    public void Arguments_ohne_abschliessenden_Backslash()
    {
        var root = Path.Combine(Path.GetTempPath(), "t0742-daten") + Path.DirectorySeparatorChar;
        var a = WorkflowTaskPlan.Arguments("abc-123", root);
        Assert.Equal($"--ablauf-faellig abc-123 --data-root \"{Path.TrimEndingDirectorySeparator(Path.GetFullPath(root))}\"", a);
        Assert.DoesNotContain("\\\"", a);
        Assert.Equal(WorkflowTaskPlan.TaskName(root, "abc-123"), WorkflowTaskPlan.TaskName(root.TrimEnd(Path.DirectorySeparatorChar), "abc-123"));
    }
    [Fact(DisplayName = "T-0742 Gegenprüfung: Pausiert das offene Trendo den Ablauf WÄHREND die Aufgabe läuft, bleibt er pausiert; nur der Laufstand kommt hinzu")]
    public async Task Pausieren_waehrend_des_Laufs_bleibt()
    {
        var (f, app, home) = Make(); using var _ = f;
        var w = app.Add(new Workflow { Name = "W", Trigger = TriggerKind.Intervall, EveryMinutes = 60,
            Steps = [new() { Kind = StepKind.TextSchreiben, To = Path.Combine(home, "w-{zeit}.txt"), Text = "x" }] });
        var aufgabe = new WorkflowEngine(f.Root, f.Core.Audit, f.Core.Approvals, [home]);
        var t = new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero);
        Workflow? x = null;
        var (run, _) = await aufgabe.RunOneIfDueAsync(w.Id, t, onStep: _ =>
        {
            w.Enabled = false; w.Name = "W neu"; app.Update(w); // offenes Trendo: pausieren und umbenennen, während die Aufgabe läuft
            x = app.Add(new Workflow { Name = "X", Trigger = TriggerKind.Manuell, Steps = [new() { Kind = StepKind.OrdnerAnlegen, To = Path.Combine(home, "x") }] });
            return Task.CompletedTask;
        });
        Assert.True(run!.Ok);
        var d = Frisch(f).Single(v => v.Id == w.Id);
        Assert.False(d.Enabled); Assert.Equal("W neu", d.Name); Assert.Equal(t, d.LastRun);
        Assert.Contains(Frisch(f), v => v.Id == x!.Id);
        // gelöscht während des Laufs: die Aufgabe legt ihn nicht wieder an
        var g = app.Add(new Workflow { Name = "G", Trigger = TriggerKind.Intervall, EveryMinutes = 60, Steps = [new() { Kind = StepKind.OrdnerAnlegen, To = Path.Combine(home, "g") }] });
        var (rg, _) = await new WorkflowEngine(f.Root, f.Core.Audit, f.Core.Approvals, [home]).RunOneIfDueAsync(g.Id, t, onStep: _ => { app.Remove(g.Id); return Task.CompletedTask; });
        Assert.True(rg!.Ok);
        Assert.DoesNotContain(Frisch(f), v => v.Id == g.Id);
        Assert.False(File.Exists(Path.Combine(f.Root, "ablaeufe", "ablaeufe.json.sperre")) && IsLocked(Path.Combine(f.Root, "ablaeufe", "ablaeufe.json.sperre"))); // Sperre wieder frei
    }

    static bool IsLocked(string p) { try { using var s = new FileStream(p, FileMode.Open, FileAccess.ReadWrite, FileShare.None); return false; } catch (IOException) { return true; } }

    [Fact(DisplayName = "T-0742 Gegenprüfung: Nach dem Belegen wird erneut geprüft – hat ein anderer Prozess den Ablauf gerade erledigt, läuft er nicht doppelt (kein Fehler)")]
    public async Task Nach_der_Sperre_erneut_pruefen()
    {
        var (f, app, home) = Make(); using var _ = f;
        var w = app.Add(new Workflow { Name = "W", Trigger = TriggerKind.Intervall, EveryMinutes = 60,
            Steps = [new() { Kind = StepKind.TextSchreiben, To = Path.Combine(home, "w-{zeit}.txt"), Text = "x" }] });
        var t = new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero);
        Assert.True(WorkflowEngine.IsTimeDue(w, t)); // das offene Trendo hält ihn für fällig (Stand von vorhin)
        Assert.True((await new WorkflowEngine(f.Root, f.Core.Audit, f.Core.Approvals, [home]).RunOneIfDueAsync(w.Id, t)).Run!.Ok); // Aufgabe erledigt ihn
        var r = await app.RunAsync(w, false, null, t.AddMinutes(1), onlyIfDue: true);
        Assert.True(r.Skipped); Assert.Contains("Nicht mehr dran", r.Lines[0]);
        Assert.Single(Directory.GetFiles(home, "w-*.txt"));
        Assert.Equal(0, app.Get(w.Id)!.FailCount); Assert.Null(app.Get(w.Id)!.LastError);
        Assert.False((await app.RunAsync(w, false, null, t.AddMinutes(2))).Skipped); // „Jetzt ausführen“ von Hand prüft nicht nach
    }

    const string Liste = """
        <Tasks>
        <!-- \Fremd -->
        <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task"><RegistrationInfo><URI>\Fremd</URI></RegistrationInfo>
          <Actions><Exec><Command>C:\Programme\Trendo\TrendoOS.exe</Command><Arguments>--ablauf-faellig a1 --data-root "C:\x"</Arguments></Exec></Actions></Task>
        {0}
        </Tasks>
        """;
    static string Eintrag(string name, string exe, string args) =>
        $"<!-- \\{name} -->\n<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\"><RegistrationInfo><URI>\\{name}</URI></RegistrationInfo><Actions Context=\"Author\"><Exec><Command>{System.Security.SecurityElement.Escape(exe)}</Command><Arguments>{System.Security.SecurityElement.Escape(args)}</Arguments></Exec></Actions></Task>\n";

    [Fact(DisplayName = "T-0742 Gegenprüfung: Aufräumen beim Start und Deinstallieren – nur eigene Aufgaben (Namensmuster + eigenes Programm), verwaiste weg, passende bleiben (schtasks ersetzt)")]
    public void Verwaiste_Aufgaben_aufraeumen()
    {
        if (!WorkflowTaskPlan.Supported) Ueberspringen.Weil("nur unter Windows.");
        var (f, app, home) = Make(); using var _ = f;
        var exe = Path.Combine(f.Root, "TrendoOS.exe"); var fremdExe = Path.Combine(f.Root, "Anderes.exe");
        var lebt = app.Add(new Workflow { Name = "lebt", Trigger = TriggerKind.Taeglich, At = "08:00", Steps = [new() { Kind = StepKind.OrdnerAnlegen, To = Path.Combine(home, "a") }] });
        var alterOrdner = Path.Combine(f.Root, "alter-datenordner"); // Datenordner gewechselt: den alten gibt es nicht mehr
        string N(string root, string id) => WorkflowTaskPlan.TaskName(root, id);
        var tasks = new Dictionary<string, (string Exe, string Args)>
        {
            [N(f.Root, lebt.Id)] = (exe, WorkflowTaskPlan.Arguments(lebt.Id, f.Root)),               // passt → bleibt
            [N(f.Root, "geloescht1")] = (exe, WorkflowTaskPlan.Arguments("geloescht1", f.Root)),    // Ablauf gelöscht → weg
            [N(alterOrdner, lebt.Id)] = (exe, WorkflowTaskPlan.Arguments(lebt.Id, alterOrdner)),   // alter Datenordner → weg
            [N(f.Root, "fremd123")] = (fremdExe, WorkflowTaskPlan.Arguments("fremd123", f.Root)), // anderes Programm → nie anfassen
        };
        var calls = new List<string>();
        var plan = new WorkflowTaskPlan { Run = a =>
        {
            calls.Add(string.Join(" ", a));
            if (a[0] == "/Query") return (0, string.Format(Liste, string.Concat(tasks.Select(t => Eintrag(t.Key, t.Value.Exe, t.Value.Args)))));
            if (a[0] == "/Delete") return tasks.Remove(a[2]) ? (0, "ERFOLGREICH") : (1, "nicht gefunden");
            return (1, "unerwartet");
        } };
        var own = WorkflowTaskPlan.ParseOwn(string.Format(Liste, string.Concat(tasks.Select(t => Eintrag(t.Key, t.Value.Exe, t.Value.Args)))), exe);
        Assert.Equal(3, own.Count); // „Fremd“ (anderer Name) und das andere Programm zählen nicht
        var gone = plan.CleanupOrphans(exe);
        Assert.Equal(2, gone.Count);
        Assert.Equal(new[] { N(f.Root, lebt.Id), N(f.Root, "fremd123") }.Order(), tasks.Keys.Order());
        Assert.Equal("/Query /XML ONE", calls[0]);
        Assert.DoesNotContain(calls, c => c.StartsWith("/Create") || c.StartsWith("/Run"));
        Assert.False(Directory.Exists(alterOrdner)); // nichts neu angelegt
        // Deinstallieren: alle eigenen Aufgaben dieses Programms, die fremde bleibt
        Assert.Equal(1, plan.RemoveAllOwn(exe));
        Assert.Equal([N(f.Root, "fremd123")], tasks.Keys.ToList());
        // Start durch eine Aufgabe mit fehlendem Datenordner: nichts anlegen, eigene Aufgabe entfernen
        calls.Clear();
        Assert.True(plan.RemoveIfRootMissing("abc123", alterOrdner));
        Assert.False(Directory.Exists(alterOrdner));
        Assert.Equal([$"/Delete /TN {N(alterOrdner, "abc123")} /F"], calls);
        Assert.False(plan.RemoveIfRootMissing("abc123", f.Root));
        Assert.False(plan.DeleteByName("Fremde-Aufgabe")); // fremdes Namensmuster wird nie gelöscht

        // Sammelstand 0.9.105: Datenordner auf einem gerade nicht erreichbaren Laufwerk (Ordner darüber fehlt) – die Aufgabe bleibt
        var weg = Path.Combine(f.Root, "usb-" + Guid.NewGuid().ToString("N"), "TrendoOS");
        Assert.True(WorkflowTaskPlan.Unreachable(weg));
        Assert.False(WorkflowTaskPlan.Unreachable(alterOrdner)); // Ordner darüber da: der Datenordner wurde wirklich gelöscht
        Assert.True(WorkflowTaskPlan.Matches(new WorkflowTaskPlan.OwnTask(N(weg, "abc123"), "abc123", weg)));
        calls.Clear();
        Assert.True(plan.RemoveIfRootMissing("abc123", weg));
        Assert.Empty(calls); // nichts gelöscht
        Assert.False(Directory.Exists(weg));
    }
}
