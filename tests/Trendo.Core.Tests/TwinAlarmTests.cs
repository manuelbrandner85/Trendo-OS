using System.Net;
using System.Text;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0750: Geräte überwachen – Alarmregeln (über/unter, Dauer, Hysterese, Protokoll, Datei), wachsende Datei, HTTP im eigenen Netz (echter Server), echte Leistungsindikatoren.</summary>
public class TwinAlarmTests
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0750-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }
    static readonly DateTime T0 = new(2026, 11, 17, 10, 0, 0);

    static TwinModel Series(params double[] v)
    {
        var m = new TwinModel();
        for (var i = 0; i < v.Length; i++) m.Readings.Add(new(T0.AddSeconds(i * 10), "Ofen", "Temperatur", v[i]));
        return m;
    }

    [Fact(DisplayName = "Regel über: löst erst nach der Dauer aus, Hysterese verhindert Flattern, Aufhebung wird gemeldet")]
    public void Rule_above_duration_hysteresis()
    {
        //                  0   10  20  30  40  50  60  70  80  90 s
        var m = Series(50, 81, 79, 82, 83, 84, 79, 76, 74, 85);
        var r = new TwinRule { Id = "r1", Name = "heiß", Asset = "Ofen", Sensor = "Temperatur", Above = true, Threshold = 80, DurationSeconds = 20, Hysteresis = 5, Created = T0 };
        var ev = TwinRules.Evaluate(m, r);
        // 81 bei 10 s wird durch 79 unterbrochen; ab 30 s (82) dauerhaft drüber → bei 50 s (20 s Dauer) ausgelöst; 79 und 76 liegen im Band; 74 ≤ 75 hebt auf; 85 allein reicht nicht (Dauer)
        Assert.Equal(2, ev.Count);
        Assert.True(ev[0].Raised); Assert.Equal(T0.AddSeconds(50), ev[0].Time); Assert.Equal(84, ev[0].Value);
        Assert.False(ev[1].Raised); Assert.Equal(T0.AddSeconds(80), ev[1].Time);
        Assert.False(TwinRules.IsActive(m, r));
        // ohne Hysterese hebt schon 79 auf
        r.Hysteresis = 0; Assert.Equal(T0.AddSeconds(60), TwinRules.Evaluate(m, r)[1].Time);
    }

    [Fact(DisplayName = "Regel unter, Dauer 0: sofort; nur Werte ab Anlage der Regel zählen")]
    public void Rule_below_and_created()
    {
        var m = Series(5, 2, 3, 6, 1);
        var r = new TwinRule { Asset = "ofen", Sensor = "temperatur", Above = false, Threshold = 3, Hysteresis = 1, Created = T0 };
        var ev = TwinRules.Evaluate(m, r);
        Assert.Equal([true, false, true], ev.Select(e => e.Raised));
        Assert.True(TwinRules.IsActive(m, r));
        r.Created = T0.AddSeconds(25); // alte Werte (2 bei 10 s) lösen nichts mehr aus
        Assert.Single(TwinRules.Evaluate(m, r));
        r.Enabled = false; Assert.False(TwinRules.IsActive(m, r));
    }

    [Fact(DisplayName = "Prüfen schreibt jeden Wechsel genau einmal ins Protokoll, abgeschaltete Regeln schweigen; Protokoll als CSV")]
    public void Check_logs_once()
    {
        var m = Series(90, 91);
        m.Rules.Add(new TwinRule { Id = "a", Name = "heiß", Asset = "Ofen", Sensor = "Temperatur", Threshold = 80, Created = T0 });
        m.Rules.Add(new TwinRule { Id = "b", Name = "aus", Asset = "Ofen", Sensor = "Temperatur", Threshold = 10, Created = T0, Enabled = false });
        Assert.Single(TwinRules.Check(m));
        Assert.Empty(TwinRules.Check(m));
        m.Readings.Add(new(T0.AddSeconds(30), "Ofen", "Temperatur", 20));
        var f = TwinRules.Check(m); Assert.Single(f); Assert.False(f[0].Raised);
        Assert.Equal(2, m.Alarms.Count);
        var csv = TwinRules.LogCsv(m.Alarms);
        Assert.StartsWith("Zeit;Regel;Anlage;Sensor;Ereignis;Wert\n", csv); Assert.Contains(";ausgelöst;90", csv); Assert.Contains(";aufgehoben;20", csv);
    }

    [Fact(DisplayName = "Regeln als Datei: Ausgabe und Einlesen, neue Kennungen, falsche oder ungültige Dateien werden abgelehnt")]
    public void Rules_file_roundtrip()
    {
        var r = new TwinRule { Name = "Kühlraum warm", Asset = "Kühlraum", Sensor = "Temperatur", Above = true, Threshold = 8, DurationSeconds = 300, Hysteresis = 1.5 };
        var json = TwinRules.ExportJson([r]);
        Assert.Contains("trendo-alarmregeln", json);
        var now = new DateTime(2026, 11, 18);
        var back = Assert.Single(TwinRules.ImportJson(json, now));
        Assert.Equal(("Kühlraum warm", 8.0, 300.0, 1.5, true), (back.Name, back.Threshold, back.DurationSeconds, back.Hysteresis, back.Above));
        Assert.NotEqual(r.Id, back.Id); Assert.Equal(now, back.Created);
        Assert.Throws<FormatException>(() => TwinRules.ImportJson("{\"Format\":\"anders\",\"Version\":1,\"Regeln\":[]}", now));
        Assert.Throws<FormatException>(() => TwinRules.ImportJson("kein json", now));
        Assert.Throws<ArgumentException>(() => TwinRules.ImportJson(json.Replace("300", "-1"), now));
        Assert.Throws<FormatException>(() => TwinRules.ImportJson(json.Replace("\"Version\": 1", "\"Version\": 9"), now));
    }

    [Fact(DisplayName = "Zeilen deuten: Zahl, Komma-Zahl, Zeit;Wert, Zeit;Anlage;Sensor;Wert, JSON mit Feld und Zeit, Kopfzeile")]
    public void Parse_lines()
    {
        var s = new TwinSource { Asset = "Keller", Sensor = "Feuchte" };
        var now = T0;
        Assert.Equal(21.5, Assert.Single(TwinFeed.ParseLine("21,5", s, now)).Value);
        Assert.Equal(-3, Assert.Single(TwinFeed.ParseLine(" -3 ", s, now)).Value);
        var tv = Assert.Single(TwinFeed.ParseLine("17.11.2026 09:30;64,2", s, now)); Assert.Equal((new DateTime(2026, 11, 17, 9, 30, 0), 64.2), (tv.Time, tv.Value));
        var iso = Assert.Single(TwinFeed.ParseLine("2026-11-17T09:31:00,65.0", s, now)); Assert.Equal(65.0, iso.Value); Assert.Equal(31, iso.Time.Minute);
        var full = Assert.Single(TwinFeed.ParseLine("17.11.2026 09:32;Heizung;Vorlauf;55,5", s, now)); Assert.Equal(("Heizung", "Vorlauf", 55.5), (full.Asset, full.Sensor, full.Value));
        Assert.Empty(TwinFeed.ParseLine("Zeit;Wert", s, now));
        Assert.Empty(TwinFeed.ParseLine("", s, now));
        var js = Assert.Single(TwinFeed.ParseLine("{\"zeit\":\"2026-11-17 09:33:00\",\"value\":12.5}", s, now)); Assert.Equal((12.5, 33), (js.Value, js.Time.Minute));
        Assert.Equal(230, Assert.Single(TwinFeed.ParseLine("{\"ENERGY\":{\"Power\":230}}", new TwinSource { Asset = "a", Sensor = "b", JsonField = "ENERGY.Power" }, now)).Value);
        Assert.Empty(TwinFeed.ParseLine("{\"anders\":1}", s, now));
    }

    [Fact(DisplayName = "Wachsende Datei: liest nur Neues, angefangene Zeile wartet, neu begonnene Datei wird von vorn gelesen")]
    public void Growing_file()
    {
        var d = Tmp();
        try
        {
            var hub = new TwinHub(d); var f = Path.Combine(d, "werte.csv");
            File.WriteAllText(f, "Zeit;Wert\n17.11.2026 10:00;1\n17.11.2026 10:01;2\n17.11.2026 10:02;3");
            var s = new TwinSource { Target = f, Asset = "Tank", Sensor = "Füllstand" };
            Assert.Equal(2, TwinFeed.PollFile(hub, s, T0)); // „3“ ohne Zeilenende wartet
            Assert.Equal(0, TwinFeed.PollFile(hub, s, T0));
            using (var w = new FileStream(f, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) { w.Write(Encoding.UTF8.GetBytes("\n17.11.2026 10:03;4\n")); }
            Assert.Equal(2, TwinFeed.PollFile(hub, s, T0));
            Assert.Equal([1, 2, 3, 4], hub.Model.Readings.Where(r => r.Sensor == "Füllstand").Select(r => r.Value));
            File.WriteAllText(f, "9\n"); // neu begonnen (kürzer)
            Assert.Equal(1, TwinFeed.PollFile(hub, s, T0));
            Assert.Equal(9, hub.Model.Readings[^1].Value);
            Assert.Equal(9, new TwinHub(d).Model.Readings[^1].Value); // gespeichert
            File.Delete(f); Assert.Throws<IOException>(() => TwinFeed.PollFile(hub, s, T0));
        }
        finally { Directory.Delete(d, true); }
    }

    [Theory(DisplayName = "HTTP nur im eigenen Netz")]
    [InlineData("http://192.168.1.20/status", true)]
    [InlineData("http://10.0.0.5:8080/x", true)]
    [InlineData("http://172.20.1.1/", true)]
    [InlineData("http://127.0.0.1:9/", true)]
    [InlineData("http://sensor.local/werte", true)]
    [InlineData("http://[fe80::1]/", true)]
    [InlineData("http://172.32.0.1/", false)]
    [InlineData("http://8.8.8.8/", false)]
    [InlineData("https://example.com/", false)]
    [InlineData("ftp://192.168.1.2/", false)]
    [InlineData("http://user:pw@192.168.1.2/", false)]
    [InlineData("kein url", false)]
    public void Local_only(string url, bool ok) => Assert.Equal(ok, TwinFeed.IsLocalUrl(url, out _));

    /// <summary>Gegenprüfung T-0750 (QA-019): der HTTP-Abruf liegt in der App (Test mit echtem Server: <c>Trendo.App.Tests.TwinHttp_Tests</c>).
    /// Im Kern bleiben Freigabe, Adressprüfung und das Auswerten der Antwort – geprüft mit hineingereichter Abruf-Funktion.</summary>
    [Fact(DisplayName = "HTTP-Quelle im Kern: ohne Freigabe kein Abruf, nur eigenes Netz, Wert aus JSON-Feld; Abruf-Funktion wird hineingereicht")]
    public async Task Http_fetch_injected()
    {
        var calls = new List<Uri>();
        Task<string> Get(Uri u, CancellationToken ct) { calls.Add(u); return Task.FromResult(Body); }
        var s = new TwinSource { Kind = "http", Target = "http://192.168.1.20/cm", Asset = "Aquarium", Sensor = "Wasser", JsonField = "StatusSNS.DS18B20.Temperature" };
        await Assert.ThrowsAsync<InvalidOperationException>(() => TwinFeed.FetchHttpAsync(s, Get));
        Assert.Empty(calls);
        s.Approved = true;
        Assert.Equal(21.7, await TwinFeed.FetchHttpAsync(s, Get));
        Assert.Equal("http://192.168.1.20/cm", Assert.Single(calls).ToString());
        s.Target = "http://8.8.8.8/"; await Assert.ThrowsAsync<ArgumentException>(() => TwinFeed.FetchHttpAsync(s, Get));
        Assert.Single(calls);
        Assert.Equal(7, TwinFeed.ParseHttpBody("{\"wert\":7}", new TwinSource()));
        Assert.Equal(3.5, TwinFeed.ParseHttpBody(" 3.5 ", new TwinSource()));
        Assert.Throws<FormatException>(() => TwinFeed.ParseHttpBody("<html>", new TwinSource { JsonField = "a.b" }));
    }

    [Theory(DisplayName = "Gegenprüfung: aufgelöste Adresse – nur privat, Loopback oder Link-local")]
    [InlineData("192.168.178.1", true)] [InlineData("10.1.2.3", true)] [InlineData("172.31.255.1", true)] [InlineData("169.254.3.4", true)] [InlineData("127.0.0.5", true)]
    [InlineData("::1", true)] [InlineData("fd00::12", true)] [InlineData("fe80::1", true)] [InlineData("::ffff:192.168.1.2", true)]
    [InlineData("93.184.216.34", false)] [InlineData("8.8.8.8", false)] [InlineData("172.32.0.1", false)] [InlineData("2001:db8::1", false)] [InlineData("::ffff:8.8.8.8", false)] [InlineData("0.0.0.0", false)]
    public void Local_address(string ip, bool ok) => Assert.Equal(ok, TwinFeed.IsLocalAddress(IPAddress.Parse(ip)));

    // ---------- Gegenprüfung T-0750: Regel-Datei streng prüfen ----------

    [Fact(DisplayName = "Gegenprüfung: Regel-Datei mit leerem Eintrag, ohne/mit falscher Version, zu vielen Regeln, zu langen Texten oder zu groß wird mit deutscher Meldung abgelehnt")]
    public void Rules_file_limits()
    {
        var now = new DateTime(2026, 11, 18);
        string F(string regeln, string version = "1") => $"{{\"Format\":\"trendo-alarmregeln\",\"Version\":{version},\"Regeln\":[{regeln}]}}";
        const string ok = "{\"Name\":\"a\",\"Asset\":\"Ofen\",\"Sensor\":\"T\",\"Threshold\":1}";
        Assert.Single(TwinRules.ImportJson(F(ok), now));
        var e = Assert.Throws<FormatException>(() => TwinRules.ImportJson(F("null"), now)); Assert.Contains("Eintrag 1", e.Message);
        Assert.Throws<FormatException>(() => TwinRules.ImportJson(F(ok + ",null"), now));
        Assert.Throws<FormatException>(() => TwinRules.ImportJson(F(ok, "0"), now));
        Assert.Throws<FormatException>(() => TwinRules.ImportJson(F(ok, "-3"), now));
        Assert.Throws<FormatException>(() => TwinRules.ImportJson("{\"Format\":\"trendo-alarmregeln\",\"Regeln\":[" + ok + "]}", now)); // ohne Version
        Assert.Throws<FormatException>(() => TwinRules.ImportJson(F(string.Join(",", Enumerable.Repeat(ok, TwinRules.MaxRules + 1))), now));
        Assert.Equal(TwinRules.MaxRules, TwinRules.ImportJson(F(string.Join(",", Enumerable.Repeat(ok, TwinRules.MaxRules))), now).Count);
        var longName = Assert.Throws<ArgumentException>(() => TwinRules.ImportJson(F(ok.Replace("\"a\"", "\"" + new string('x', TwinRules.MaxNameLength + 1) + "\"")), now));
        Assert.Contains("höchstens", longName.Message); Assert.Contains("Regel 1", longName.Message);
        Assert.Throws<ArgumentException>(() => TwinRules.ImportJson(F(ok.Replace("\"Ofen\"", "\"" + new string('o', TwinRules.MaxFieldLength + 1) + "\"")), now));
        Assert.Throws<ArgumentException>(() => TwinRules.ImportJson(F("{\"Name\":\"a\",\"Asset\":null,\"Sensor\":\"T\",\"Threshold\":1}"), now));
        Assert.Throws<FormatException>(() => TwinRules.ImportJson(F(ok) + new string(' ', TwinRules.MaxFileBytes), now));
    }

    // ---------- Gegenprüfung T-0750: genau eine Meldung je Wechsel ----------

    [Fact(DisplayName = "Gegenprüfung: Kürzen des Protokolls oder des Verlaufs erzeugt keine Doppelmeldung; Zustand je Regel")]
    public void Check_no_duplicates_after_trim()
    {
        var m = Series(90, 85);
        var r = new TwinRule { Id = "a", Name = "heiß", Asset = "Ofen", Sensor = "Temperatur", Threshold = 80, Created = T0 };
        m.Rules.Add(r);
        Assert.Single(TwinRules.Check(m));
        m.Alarms.Clear();                      // Protokoll gekürzt
        Assert.Empty(TwinRules.Check(m));
        m.Readings.RemoveAt(0);                // Verlauf gekürzt: der auslösende Wert (90) ist weg, 85 liegt noch drüber
        Assert.Empty(TwinRules.Check(m));
        m.Readings.Add(new(T0.AddSeconds(30), "Ofen", "Temperatur", 70));
        var f = Assert.Single(TwinRules.Check(m)); Assert.False(f.Raised);
        m.Readings.Add(new(T0.AddSeconds(40), "Ofen", "Temperatur", 95));
        Assert.True(Assert.Single(TwinRules.Check(m)).Raised);
        Assert.Equal(T0.AddSeconds(40), r.State!.Reported);
        Assert.True(TwinRules.IsActive(r));
    }

    [Fact(DisplayName = "Gegenprüfung: IsActive fürs Neuzeichnen liest nur den gespeicherten Zustand; wieder eingeschaltete Regel meldet nichts aus der Aus-Zeit")]
    public void State_and_reenable()
    {
        var m = Series(50);
        var r = new TwinRule { Id = "a", Name = "heiß", Asset = "Ofen", Sensor = "Temperatur", Threshold = 80, Created = T0 };
        m.Rules.Add(r);
        Assert.Empty(TwinRules.Check(m)); Assert.False(TwinRules.IsActive(r));
        Assert.True(TwinRules.SetEnabled(m, r, false));
        m.Readings.Add(new(T0.AddSeconds(10), "Ofen", "Temperatur", 99)); // während die Regel aus ist
        Assert.Empty(TwinRules.Check(m));
        Assert.True(TwinRules.SetEnabled(m, r, true));
        Assert.Empty(TwinRules.Check(m)); Assert.Empty(m.Alarms);
        Assert.True(TwinRules.IsActive(m, r)); // Nachrechnen über alles sähe den Wert – der Zustand nicht
        Assert.False(TwinRules.IsActive(r));
        m.Readings.Add(new(T0.AddSeconds(20), "Ofen", "Temperatur", 98)); // nach dem Einschalten
        Assert.True(Assert.Single(TwinRules.Check(m)).Raised);
        // der gespeicherte Zustand übersteht Speichern/Laden und gehört nicht in die Regel-Datei
        Assert.DoesNotContain("\"State\"", TwinRules.ExportJson(m.Rules).Replace("\"State\": null", ""));
        var json = System.Text.Json.JsonSerializer.Serialize(m);
        var back = System.Text.Json.JsonSerializer.Deserialize<TwinModel>(json)!;
        Assert.Empty(TwinRules.Check(back)); Assert.True(TwinRules.IsActive(back.Rules[0]));
    }

    [Fact(DisplayName = "Gegenprüfung: Rückgängig übernimmt Regeln mit Zustand; wiederhergestellte oder dabei eingeschaltete Regeln melden nichts Altes")]
    public void Adopt_keeps_state()
    {
        var m = Series(90);
        var r = new TwinRule { Id = "a", Name = "heiß", Asset = "Ofen", Sensor = "Temperatur", Threshold = 80, Created = T0 };
        m.Rules.Add(r); Assert.Single(TwinRules.Check(m));
        var snap = m.Rules.Select(x => x.WithoutState()).ToList();
        Assert.Null(snap[0].State);
        TwinRules.Adopt(m, snap); // Stand ohne Zustand zurückholen – Zustand bleibt
        Assert.Empty(TwinRules.Check(m)); Assert.True(TwinRules.IsActive(m.Rules[0]));
        m.Rules.Clear();
        TwinRules.Adopt(m, [r.WithoutState()]); // gelöschte Regel per Wiederholen zurück
        Assert.Empty(TwinRules.Check(m));
    }

    // ---------- Gegenprüfung T-0750: wachsende Datei ----------

    [Fact(DisplayName = "Gegenprüfung: Zeile über 1 MB ohne Zeilenende wird übersprungen und gemeldet, danach geht es weiter")]
    public void Growing_file_overlong_line()
    {
        var d = Tmp();
        try
        {
            var hub = new TwinHub(d); var f = Path.Combine(d, "lang.log");
            File.WriteAllText(f, "1\n");
            var s = new TwinSource { Target = f, Asset = "A", Sensor = "B" };
            Assert.Equal(1, TwinFeed.PollFile(hub, s, T0));
            File.AppendAllText(f, new string('7', TwinFeed.MaxChunk + 500)); // eine riesige Zeile, noch ohne Ende
            Assert.Equal(0, TwinFeed.PollFile(hub, s, T0));
            Assert.Contains("übersprungen", s.LastError);
            File.AppendAllText(f, "777\n2\n3\n"); // Rest der langen Zeile, dann normale Zeilen
            var n = 0; for (var i = 0; i < 3; i++) n += TwinFeed.PollFile(hub, s, T0);
            Assert.Equal(2, n);
            Assert.Equal([1, 2, 3], hub.Model.Readings.Select(r => r.Value));
            Assert.Equal("", s.LastError);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "Gegenprüfung: riesige Altdatei – nur das letzte MB, die dabei angeschnittene erste Zeile wird verworfen")]
    public void Growing_file_big_old_file_drops_cut_line()
    {
        var d = Tmp();
        try
        {
            var hub = new TwinHub(d); var f = Path.Combine(d, "alt.csv");
            // lauter Zeilen „1000000000000000000000“; der Sprung auf „Länge − 1 MB“ landet mitten in einer Zeile (angeschnitten wäre z. B. „000000“ = 0)
            const string line = "1000000000000000000000\n", last = "17.11.2026 10:00;42\n";
            var count = (TwinFeed.MaxChunk + 100_000) / line.Length;
            var sb = new StringBuilder();
            for (var i = 0; i < count; i++) sb.Append(line);
            sb.Append(last);
            File.WriteAllText(f, sb.ToString());
            var cut = (sb.Length - TwinFeed.MaxChunk) % line.Length; Assert.InRange(cut, 2, line.Length - 1); // wirklich mitten in einer Zeile
            var s = new TwinSource { Target = f, Asset = "A", Sensor = "B" };
            var n = TwinFeed.PollFile(hub, s, T0);
            Assert.True(n > 1000);
            // jede gelesene Zeile ist vollständig – ein angeschnittenes Stück würde einen falschen Wert liefern
            Assert.All(hub.Model.Readings.Take(n - 1), r => Assert.Equal(1e21, r.Value));
            Assert.Equal(42, hub.Model.Readings[^1].Value);
            Assert.Equal(new FileInfo(f).Length, s.Offset);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "Gegenprüfung: UTF-16 mit BOM (Little und Big Endian) und UTF-8 mit BOM werden gelesen")]
    public void Growing_file_utf16()
    {
        var d = Tmp();
        try
        {
            foreach (var enc in new Encoding[] { Encoding.Unicode, Encoding.BigEndianUnicode, new UTF8Encoding(true) })
            {
                var hub = new TwinHub(d); hub.Model.Readings.Clear();
                var f = Path.Combine(d, "w-" + enc.WebName + ".csv");
                File.WriteAllText(f, "Zeit;Wert\n17.11.2026 10:00;21,5\n17.11.2026 10:01;22", enc);
                var s = new TwinSource { Target = f, Asset = "Raum", Sensor = "Temperatur" };
                Assert.Equal(1, TwinFeed.PollFile(hub, s, T0, save: false));
                using (var w = new FileStream(f, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) { var b = enc.GetBytes("\n17.11.2026 10:02;23,25\n"); w.Write(b); }
                Assert.Equal(2, TwinFeed.PollFile(hub, s, T0, save: false));
                Assert.Equal([21.5, 22, 23.25], hub.Model.Readings.Select(r => r.Value));
            }
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "Gegenprüfung: gleich lange neue Datei (Rotation) wird an der Dateikennung erkannt und von vorn gelesen")]
    public void Growing_file_rotation_same_length()
    {
        if (!OperatingSystem.IsWindows()) return; // Dateikennung nur unter Windows
        var d = Tmp();
        try
        {
            var hub = new TwinHub(d); var f = Path.Combine(d, "rot.log");
            File.WriteAllText(f, "1\n2\n");
            var s = new TwinSource { Target = f, Asset = "A", Sensor = "B" };
            Assert.Equal(2, TwinFeed.PollFile(hub, s, T0));
            File.Move(f, f + ".1"); File.WriteAllText(f, "5\n6\n"); // gleich lang, neue Datei
            Assert.Equal(2, TwinFeed.PollFile(hub, s, T0));
            Assert.Equal([1, 2, 5, 6], hub.Model.Readings.Select(r => r.Value));
            Assert.Equal(0, TwinFeed.PollFile(hub, s, T0));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "Gegenprüfung: Netzwerkpfade (\\\\Rechner\\…) werden nicht gelesen")]
    public void Growing_file_unc_refused()
    {
        var d = Tmp();
        try
        {
            var s = new TwinSource { Target = @"\\127.0.0.1\trendo-gibt-es-nicht$\werte.csv", Asset = "A", Sensor = "B" };
            var e = Assert.Throws<IOException>(() => TwinFeed.PollFile(new TwinHub(d), s, T0));
            Assert.Contains("anderen Rechnern", e.Message);
            Assert.True(TwinFeed.IsNetworkPath("//nas/freigabe/x.csv")); Assert.False(TwinFeed.IsNetworkPath(@"C:\daten\x.csv"));
        }
        finally { Directory.Delete(d, true); }
    }

    const string Body = "{\"StatusSNS\":{\"DS18B20\":{\"Temperature\":21.7}}}";

    [Fact(DisplayName = "Echte Leistungsindikatoren dieses PCs: CPU, RAM und Netz liefern plausible Werte (ohne Admin)")]
    public void Real_counters()
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux()) return;
        using var pc = new PcCounters();
        var first = pc.Read(DateTime.Now);
        Assert.True(first.ContainsKey(PcCounters.Ram)); Assert.InRange(first[PcCounters.Ram], 1, 100);
        Thread.Sleep(1100);
        var v = pc.Read(DateTime.Now);
        Assert.InRange(v[PcCounters.Cpu], 0, 100);
        Assert.True(v.ContainsKey(PcCounters.NetIn)); Assert.True(v[PcCounters.NetIn] >= 0);
        if (OperatingSystem.IsWindows()) Assert.InRange(v[PcCounters.Disk], 0, 100);
    }
}
