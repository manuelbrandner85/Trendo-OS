using System.Net;
using System.Net.Sockets;
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

    [Fact(DisplayName = "HTTP-Quelle mit echtem Server auf 127.0.0.1: Wert aus JSON-Feld, ohne Freigabe kein Abruf, keine Weiterleitung")]
    public async Task Http_real_server()
    {
        // kleiner echter HTTP/1.1-Server über TCP (HttpListener bräuchte für 127.0.0.1 eine URL-Freigabe vom Admin)
        var l = new TcpListener(IPAddress.Loopback, 0); l.Start(); var port = ((IPEndPoint)l.LocalEndpoint).Port;
        var served = 0;
        _ = Task.Run(async () =>
        {
            while (true)
            {
                TcpClient c; try { c = await l.AcceptTcpClientAsync(); } catch (Exception) { return; }
                using (c)
                {
                    var st = c.GetStream(); var buf = new byte[4096]; var n = await st.ReadAsync(buf); var req = Encoding.ASCII.GetString(buf, 0, n);
                    served++;
                    var resp = req.StartsWith("GET /weiter ")
                        ? "HTTP/1.1 302 Found\r\nLocation: http://8.8.8.8/\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"
                        : $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {Body.Length}\r\nConnection: close\r\n\r\n{Body}";
                    await st.WriteAsync(Encoding.ASCII.GetBytes(resp));
                }
            }
        });
        using var http = TwinFeed.NewHttp();
        var s = new TwinSource { Kind = "http", Target = $"http://127.0.0.1:{port}/cm", Asset = "Aquarium", Sensor = "Wasser", JsonField = "StatusSNS.DS18B20.Temperature" };
        await Assert.ThrowsAsync<InvalidOperationException>(() => TwinFeed.FetchHttpAsync(http, s));
        Assert.Equal(0, served);
        s.Approved = true;
        Assert.Equal(21.7, await TwinFeed.FetchHttpAsync(http, s));
        s.Target = $"http://127.0.0.1:{port}/weiter";
        await Assert.ThrowsAsync<IOException>(() => TwinFeed.FetchHttpAsync(http, s));
        s.Target = "http://8.8.8.8/"; await Assert.ThrowsAsync<ArgumentException>(() => TwinFeed.FetchHttpAsync(http, s));
        l.Stop();
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
