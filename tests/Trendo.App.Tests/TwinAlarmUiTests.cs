using System.Text;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Trendo.Core;
using Xunit;
using static Trendo.App.Tests.StartMenu_Ui;

namespace Trendo.App.Tests;

/// <summary>T-0750: Anlagen-Monitor – weitere Quellen (wachsende Datei, echte Leistungsindikatoren) und Alarmregeln über den echten Eingabeweg:
/// Klick → Regel im Modell → Alarm bei echter Datei → Benachrichtigung + Protokoll → Regeln als Datei → Rückgängig. Dazu der Echtbeleg mit künstlicher CPU-Last.</summary>
public class TwinAlarm_Ui
{
    static T Get<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().Last(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static T? Try<T>(MainWindow w, string name) where T : Control { Settle(); return w.GetVisualDescendants().OfType<T>().LastOrDefault(c => c.Name == name && c.IsEffectivelyVisible && c.Bounds.Width > 0); }
    static void Press(MainWindow w, string name) { var b = Get<Button>(w, name); b.BringIntoView(); Settle(); RealClick(w, Get<Button>(w, name)); }
    static void Key1(MainWindow w, Key k, PhysicalKey ph) { w.KeyPress(k, RawInputModifiers.None, ph, null); w.KeyRelease(k, RawInputModifiers.None, ph, null); Settle(); }
    static void Retype(MainWindow w, string name, string text) { var tb = Get<TextBox>(w, name); tb.BringIntoView(); RealClick(w, tb); tb.Focus(); tb.SelectAll(); Settle(); Key1(w, Key.Back, PhysicalKey.Backspace); foreach (var ch in text) { w.KeyTextInput(ch.ToString()); Settle(); } }
    static void CloseToasts(MainWindow w) { while (Try<Button>(w, "toast-close") is { } c) RealClick(w, c); }
    static void Open(MainWindow w, string expander) { var e = Get<Expander>(w, expander); e.BringIntoView(); e.IsExpanded = true; Settle(); }
    /// <summary>Gegenprüfung T-0750: Messungen und Datei-Abfragen laufen im Hintergrund – warten, bis ihr Ergebnis auf dem UI-Faden angekommen ist.</summary>
    static void Idle(MainWindow w) { for (var i = 0; i < 2000 && w.TwinBusy; i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); Thread.Sleep(5); } Settle(); Assert.False(w.TwinBusy); }
    static void PressWait(MainWindow w, string name) { Press(w, name); Idle(w); }
    static void Append(string f, string text) { using var s = new FileStream(f, FileMode.Append, FileAccess.Write, FileShare.ReadWrite); s.Write(Encoding.UTF8.GetBytes(text)); }

    [AvaloniaFact(DisplayName = "T-0750 UI: wachsende Datei beobachten, Regel per Klick anlegen, Alarm löst bei echter Datei aus (Benachrichtigung, Protokoll), Regeln speichern/einlesen, Rückgängig")]
    public void File_rule_alarm()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1100;
        try
        {
            Directory.CreateDirectory(root);
            var f = Path.Combine(root, "kessel.csv");
            File.WriteAllText(f, "Zeit;Wert\n" + DateTime.Now.AddSeconds(-1).ToString("yyyy-MM-dd HH:mm:ss") + ";40\n");
            w.Show("zwilling"); Settle();
            Open(w, "twin-src-box");
            Retype(w, "twin-src-asset", "Kessel"); Retype(w, "twin-src-sensor", "Druck");
            // V-07: Pfad nie tippen – „Datei wählen …“ (Dateiauswahl ersetzt), danach liest „Datei hinzufügen“ sofort
            w.PickOverride = _ => Task.FromResult<string[]?>([f]); PressWait(w, "twin-src-file-pick");
            Assert.Equal(("Kessel", "Druck"), (w.TwinAsset, w.TwinSensor));
            Assert.Contains("1 Werte gelesen", Get<TextBlock>(w, "twin-msg").Text);
            var src = Assert.Single(w.Twin.Model.Sources); Assert.Equal("datei", src.Kind);

            // Regel: über 50 für mindestens 2 s, Hysterese 5 – über die Felder der rechten Spalte
            Retype(w, "twin-rule-thr", "50"); Retype(w, "twin-rule-dur", "2"); Retype(w, "twin-rule-hys", "5");
            Press(w, "twin-rule-add");
            var rule = Assert.Single(w.Twin.Model.Rules);
            Assert.Equal((true, 50.0, 2.0, 5.0), (rule.Above, rule.Threshold, rule.DurationSeconds, rule.Hysteresis));
            Assert.Contains("Regel angelegt", Get<TextBlock>(w, "twin-msg").Text);

            // die Datei wächst: erst einmal über 50 (noch zu kurz), dann 3 s lang drüber
            var t = DateTime.Now.AddSeconds(1);
            Append(f, $"{t:yyyy-MM-dd HH:mm:ss};55\n"); PressWait(w, "twin-src-poll");
            Assert.Empty(w.Twin.Model.Alarms);
            Append(f, $"{t.AddSeconds(1):yyyy-MM-dd HH:mm:ss};57\n{t.AddSeconds(3):yyyy-MM-dd HH:mm:ss};58\n"); PressWait(w, "twin-src-poll");
            var alarm = Assert.Single(w.Twin.Model.Alarms); Assert.True(alarm.Raised); Assert.Equal(58, alarm.Value);
            Assert.Contains(w.TwinAlarmNotes, n => n.StartsWith("⚠ Alarm") && n.Contains("Kessel – Druck 58"));
            Assert.NotNull(Try<Border>(w, "toast"));
            Assert.Contains("⚠ ausgelöst (58,0)", Get<SelectableTextBlock>(w, "twin-alarm-log").Text);
            Assert.Contains("ausgelöst", Get<CheckBox>(w, "twin-rule-on-" + rule.Id).Content is TextBlock tb ? tb.Text : "");
            // im Hystereseband (47) bleibt der Alarm, bei 44 endet er
            Append(f, $"{t.AddSeconds(4):yyyy-MM-dd HH:mm:ss};47\n"); PressWait(w, "twin-src-poll"); Assert.Single(w.Twin.Model.Alarms);
            Append(f, $"{t.AddSeconds(5):yyyy-MM-dd HH:mm:ss};44\n"); PressWait(w, "twin-src-poll");
            Assert.Equal(2, w.Twin.Model.Alarms.Count); Assert.False(w.Twin.Model.Alarms[1].Raised);
            Assert.Contains(w.TwinAlarmNotes, n => n.StartsWith("✓ Alarm"));
            Assert.Contains(w.Core.Audit.ReadAll(), e => e.Action == "zwilling.alarm"); Assert.Contains(w.Core.Audit.ReadAll(), e => e.Action == "zwilling.alarm.ende");

            // Protokoll und Regeln als Datei (Benachrichtigungen wie ein Nutzer wegklicken)
            CloseToasts(w);
            Press(w, "twin-alarm-export");
            var logCsv = File.ReadAllText(Get<TextBlock>(w, "twin-msg").Text!["Gespeichert: ".Length..]);
            Assert.Contains(";ausgelöst;58", logCsv); Assert.Contains(";aufgehoben;44", logCsv);
            CloseToasts(w); Press(w, "twin-rules-export");
            var rf = Get<TextBlock>(w, "twin-msg").Text!["Gespeichert: ".Length..];
            Assert.Contains("trendo-alarmregeln", File.ReadAllText(rf));
            if (Environment.GetEnvironmentVariable("TRENDO_BELEG_T0750") is { Length: > 0 } beleg)
            {
                Directory.CreateDirectory(beleg);
                File.WriteAllText(Path.Combine(beleg, "T-0750-datei-messwerte.csv"), File.ReadAllText(f));
                File.WriteAllText(Path.Combine(beleg, "T-0750-datei-alarmprotokoll.csv"), logCsv);
                File.WriteAllText(Path.Combine(beleg, "T-0750-alarmregeln.json"), File.ReadAllText(rf));
                File.WriteAllText(Path.Combine(beleg, "T-0750-datei-benachrichtigungen.txt"), string.Join("\n", w.TwinAlarmNotes) + "\n"
                    + string.Join("\n", w.Core.Audit.ReadAll().Where(e => e.Action.StartsWith("zwilling.")).Select(e => $"Prüfprotokoll: {e.Action} | {e.Detail}")) + "\n");
            }
            // Regel entfernen, dann aus der Datei zurückholen – „Datei wählen …“ (Auswahl ersetzt), „Regeln einlesen“ läuft danach von selbst
            CloseToasts(w); Press(w, "twin-rule-del-" + rule.Id); Assert.Empty(w.Twin.Model.Rules);
            w.PickOverride = _ => Task.FromResult<string[]?>([rf]); Press(w, "twin-rules-path-pick");
            Assert.Contains("1 Alarmregel(n) übernommen", Get<TextBlock>(w, "twin-msg").Text);
            var back = Assert.Single(w.Twin.Model.Rules); Assert.Equal(50, back.Threshold); Assert.NotEqual(rule.Id, back.Id);

            // Rückgängig (Hülle): neue Regel per Klick, dann zurücknehmen – Regel weg, auch in der Datei
            Retype(w, "twin-rule-thr", "99"); Press(w, "twin-rule-add"); Assert.Equal(2, w.Twin.Model.Rules.Count);
            Press(w, "twin-undo");
            Assert.Single(w.Twin.Model.Rules);
            Assert.Single(new TwinHub(w.Core.DataRoot).Model.Rules);
            Press(w, "twin-redo"); Assert.Equal(2, w.Twin.Model.Rules.Count);
            // Messwerte bleiben bei Rückgängig unberührt, die Quelle auch
            Assert.Equal(6, w.Twin.Model.Readings.Count(r => r.Sensor == "Druck")); Assert.Single(w.Twin.Model.Sources);

            // HTTP nur nach Freigabe
            Retype(w, "twin-src-url", "http://192.168.178.30/status");
            Press(w, "twin-src-http-add");
            Assert.StartsWith("⚠ Bitte zuerst bestätigen", Get<TextBlock>(w, "twin-msg").Text);
            Retype(w, "twin-src-url", "http://example.com/x"); RealClick(w, Get<CheckBox>(w, "twin-src-http-ok")); Press(w, "twin-src-http-add");
            Assert.StartsWith("⚠ Nur Adressen im eigenen Netz", Get<TextBlock>(w, "twin-msg").Text);
            Assert.Single(w.Twin.Model.Sources);
        }
        finally { w.Close(); }
    }

    /// <summary>Echtbeleg: echte Werte dieses PCs (Leistungsindikatoren) und künstliche CPU-Last; eine Regel „CPU über X für 3 s“ löst echt aus.
    /// Normal 12 s; mit <c>TRENDO_BELEG_T0750=&lt;Ordner&gt;</c> 60 s und Rohprotokoll in diesen Ordner.</summary>
    [AvaloniaFact(DisplayName = "T-0750 Echtbeleg: echte CPU-/RAM-/Datenträger-/Netzwerte dieses PCs, künstliche Last löst die CPU-Regel aus (Benachrichtigung + Protokoll)")]
    public void Real_cpu_load_alarm()
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux()) return;
        var dir = Environment.GetEnvironmentVariable("TRENDO_BELEG_T0750");
        var seconds = string.IsNullOrEmpty(dir) ? 12 : 60;
        var (w, _, root) = Ui.Open();
        var log = new StringBuilder();
        void L(string s) => log.Append(DateTime.Now.ToString("HH:mm:ss.fff")).Append(' ').Append(s).Append('\n');
        var stop = false; var load = new List<Thread>();
        try
        {
            w.Show("zwilling"); Settle();
            PressWait(w, "twin-pc"); // erste Messung: Bezug der Leistungsindikatoren
            Thread.Sleep(1000); PressWait(w, "twin-pc");
            var cpu0 = TwinPlus.Series(w.Twin.Model, "Dieser PC", PcCounters.Cpu);
            Assert.NotEmpty(cpu0);
            var baseline = cpu0[^1].Value;
            var thr = Math.Clamp(Math.Round(baseline + 30), 40, 85);
            w.TwinSelect("Dieser PC", PcCounters.Cpu); Settle();
            Retype(w, "twin-rule-thr", thr.ToString(Fach.De)); Retype(w, "twin-rule-dur", "3"); Retype(w, "twin-rule-hys", "10");
            Press(w, "twin-rule-add");
            var rule = Assert.Single(w.Twin.Model.Rules);
            L($"Regel: {rule.Name} (Grundlast {baseline:0.0} %, {Environment.ProcessorCount} logische Kerne)");
            var loadFrom = seconds / 3; var loadTo = loadFrom + Math.Max(6, seconds / 3);
            for (var s = 0; s < seconds; s++)
            {
                if (s == loadFrom)
                {
                    L("künstliche Last an: " + Environment.ProcessorCount + " Rechen-Threads");
                    for (var i = 0; i < Environment.ProcessorCount; i++) { var th = new Thread(() => { var x = 0.0; while (!Volatile.Read(ref stop)) x = Math.Sqrt(x + 1.0); }) { IsBackground = true, Priority = ThreadPriority.BelowNormal }; th.Start(); load.Add(th); }
                }
                if (s == loadTo) { Volatile.Write(ref stop, true); foreach (var th in load) th.Join(); L("künstliche Last aus"); }
                Thread.Sleep(1000);
                var before = w.Twin.Model.Alarms.Count;
                PressWait(w, "twin-pc");
                var v = w.Twin.Model.Readings.Where(r => r.Asset == "Dieser PC").GroupBy(r => r.Sensor).ToDictionary(g => g.Key, g => g.Last().Value);
                L(string.Join(" | ", PcCounters.Sensors.Where(x => v.ContainsKey(x.Name)).Select(x => $"{x.Name} {v[x.Name]:0.0} {x.Unit}")));
                foreach (var a in w.Twin.Model.Alarms.Skip(before)) L((a.Raised ? "ALARM AUSGELÖST " : "ALARM AUFGEHOBEN ") + $"{a.Rule}: {a.Value:0.0} %");
            }
            if (!stop) { Volatile.Write(ref stop, true); foreach (var th in load) th.Join(); }
            Assert.Contains(w.Twin.Model.Alarms, a => a.Raised && a.Sensor == PcCounters.Cpu);
            Assert.Contains(w.TwinAlarmNotes, n => n.StartsWith("⚠ Alarm") && n.Contains(PcCounters.Cpu));
            foreach (var n in w.TwinAlarmNotes) L("Benachrichtigung: " + n);
            var cpu = TwinPlus.Series(w.Twin.Model, "Dieser PC", PcCounters.Cpu);
            Assert.True(cpu.Count >= seconds);
            L($"{cpu.Count} CPU-Werte, min {cpu.Min(r => r.Value):0.0} %, max {cpu.Max(r => r.Value):0.0} %");
            L("Protokoll (CSV):\n" + TwinRules.LogCsv(w.Twin.Model.Alarms).TrimEnd());
            if (!string.IsNullOrEmpty(dir)) { Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, "T-0750-cpu-last-alarm.log"), log.ToString()); }
        }
        finally { Volatile.Write(ref stop, true); foreach (var th in load) th.Join(); w.Close(); }
    }

    // ---------- Gegenprüfung T-0750 ----------

    [AvaloniaFact(DisplayName = "Gegenprüfung: Datei lesen und erste PDH-Abfrage laufen nicht auf dem UI-Faden, Ergebnis kommt dort an, kein Doppelstart")]
    public void Background_reads_off_ui_thread()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1100;
        var gate = new ManualResetEventSlim(false);
        try
        {
            Directory.CreateDirectory(root);
            var f = Path.Combine(root, "kessel.csv"); File.WriteAllText(f, "1\n");
            w.TwinAddFileSource(f, "Kessel", "Druck");
            w.Show("zwilling"); Settle();
            var ui = Environment.CurrentManagedThreadId;
            var seen = new System.Collections.Concurrent.ConcurrentQueue<(string Kind, int Thread)>();
            w.TwinBackgroundHook = k => { seen.Enqueue((k, Environment.CurrentManagedThreadId)); gate.Wait(TimeSpan.FromSeconds(20)); };
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Press(w, "twin-src-poll"); Press(w, "twin-pc");   // das Lesen hängt (wie bei einem langsamen Netzlaufwerk) – der UI-Faden nicht
            Press(w, "twin-src-poll"); Press(w, "twin-pc");   // zweiter Klick startet nichts doppelt
            Assert.Contains("gerade schon gelesen", Get<TextBlock>(w, "twin-msg").Text);
            for (var i = 0; i < 400 && seen.Count < 2; i++) Thread.Sleep(5);
            Thread.Sleep(100);
            Assert.True(sw.ElapsedMilliseconds < 10_000);
            Assert.True(w.TwinBusy);
            Assert.Equal(1, seen.Count(x => x.Kind == "datei")); Assert.Equal(1, seen.Count(x => x.Kind == "pc"));
            Assert.All(seen, x => Assert.NotEqual(ui, x.Thread));
            Assert.DoesNotContain(w.Twin.Model.Readings, r => r.Sensor == "Druck");
            gate.Set(); Idle(w);
            Assert.Equal(1, Assert.Single(w.Twin.Model.Readings, r => r.Sensor == "Druck").Value);
            Assert.Contains(w.Twin.Model.Readings, r => r.Asset == "Dieser PC" && r.Sensor == PcCounters.Ram);
        }
        finally { gate.Set(); w.TwinBackgroundHook = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Gegenprüfung: Leistungsindikatoren werden beim Schließen des Fensters freigegeben")]
    public void Counters_disposed_on_close()
    {
        var (w, _, _) = Ui.Open();
        w.Show("zwilling"); Settle();
        PressWait(w, "twin-pc");
        var pcc = w.TwinCounters;
        Assert.NotNull(pcc); Assert.False(pcc.IsDisposed);
        w.Close(); Settle();
        Assert.True(pcc.IsDisposed); Assert.Null(w.TwinCounters);
        Assert.Empty(pcc.Read(DateTime.Now));
    }

    [AvaloniaFact(DisplayName = "Gegenprüfung: wieder eingeschaltete Regel meldet nichts aus der Zeit, in der sie aus war")]
    public void Reenabled_rule_silent()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1100;
        try
        {
            w.Twin.Define("Ofen", "Temperatur", "°C");
            var t = DateTime.Now;
            var r = w.TwinAddRule("Ofen", "Temperatur", true, 80, 0, 0);
            w.Twin.Record(t.AddSeconds(1), "Ofen", "Temperatur", 50); Assert.Empty(w.TwinCheckRules());
            w.TwinSelect("Ofen", "Temperatur"); Settle();
            RealClick(w, Get<CheckBox>(w, "twin-rule-on-" + r.Id)); Settle(); Assert.False(r.Enabled); // per Klick aus
            w.Twin.Record(t.AddSeconds(2), "Ofen", "Temperatur", 99); Assert.Empty(w.TwinCheckRules());
            RealClick(w, Get<CheckBox>(w, "twin-rule-on-" + r.Id)); Settle(); Assert.True(r.Enabled);  // wieder an
            Assert.Empty(w.TwinCheckRules()); Assert.Empty(w.Twin.Model.Alarms);
            w.Twin.Record(t.AddSeconds(3), "Ofen", "Temperatur", 98);
            Assert.True(Assert.Single(w.TwinCheckRules()).Raised);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Gegenprüfung: Regel-Datei mit leerem Eintrag stürzt nicht ab, sondern meldet es auf Deutsch")]
    public void Rules_file_null_entry()
    {
        var (w, _, root) = Ui.Open(); w.Width = 1600; w.Height = 1100;
        try
        {
            Directory.CreateDirectory(root);
            var rf = Path.Combine(root, "kaputt.json"); File.WriteAllText(rf, "{\"Format\":\"trendo-alarmregeln\",\"Version\":1,\"Regeln\":[null]}");
            w.Twin.Define("Ofen", "Temperatur", "°C"); w.Twin.Record(DateTime.Now, "Ofen", "Temperatur", 20);
            w.Show("zwilling"); w.TwinSelect("Ofen", "Temperatur"); Settle();
            w.PickOverride = _ => Task.FromResult<string[]?>([rf]); Press(w, "twin-rules-path-pick");
            Assert.StartsWith("⚠ Eintrag 1 der Regel-Datei ist leer", Get<TextBlock>(w, "twin-msg").Text);
            Assert.Empty(w.Twin.Model.Rules);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Gegenprüfung: Netzwerkpfad als wachsende Datei wird abgelehnt, ohne darauf zuzugreifen")]
    public void Unc_file_refused()
    {
        var (w, _, _) = Ui.Open(); w.Width = 1600; w.Height = 1100;
        try
        {
            w.Show("zwilling"); Settle(); Open(w, "twin-src-box");
            Retype(w, "twin-src-asset", "Kessel"); Retype(w, "twin-src-sensor", "Druck");
            w.PickOverride = _ => Task.FromResult<string[]?>([@"\\127.0.0.1\trendo-gibt-es-nicht$\werte.csv"]); // „Datei wählen …“ (Auswahl ersetzt)
            PressWait(w, "twin-src-file-pick");
            Assert.StartsWith("⚠ Dateien auf anderen Rechnern", Get<TextBlock>(w, "twin-msg").Text);
            Assert.Empty(w.Twin.Model.Sources);
        }
        finally { w.Close(); }
    }
}
