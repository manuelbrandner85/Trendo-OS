using System.IO.Compression;
using Trendo.Core.Sys;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Alle Studios, Runde 8 – SPEC-12: yara-x mit den ReversingLabs-Regeln. Ausgabe im echten NDJSON-Format von yr 1.22 (scan.rs); Echtlauf erst nach Freigabe.</summary>
public class YaraScanTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "trendo-yara-" + Guid.NewGuid().ToString("N"));
    public YaraScanTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    const string Out = """
        {"path":"C:\\scan\\rechnung.exe","rules":[{"identifier":"Win32_Ransomware_WannaCry","meta":{"author":"ReversingLabs","category":"MALWARE","malware":"WANNACRY","description":"Yara rule that detects WannaCry ransomware.","tc_detection_type":"Ransomware","tc_detection_name":"WannaCry","tc_detection_factor":5},"tags":["tc_detection","malicious"]}]}
        {"path":"C:\\scan\\tool.exe","rules":[{"identifier":"Win32_Hacktool_Mimikatz","meta":{"category":"MALWARE","tc_detection_type":"Hacktool","tc_detection_name":"Mimikatz","tc_detection_factor":4}},{"identifier":"Neu","meta":{"category":"PUA"}}]}
        warning: something
        """;

    [Fact(DisplayName = "YARA +1: NDJSON lesen – deutsche Art, Name, Schwere (höchste zuerst), Zeilen ohne JSON übersprungen")]
    public void Parse()
    {
        var f = YaraScan.Parse(Out);
        Assert.Equal(3, f.Count);
        Assert.Equal(new YaraScan.Finding(@"C:\scan\rechnung.exe", "Win32_Ransomware_WannaCry", "Erpressersoftware", "WannaCry", "Yara rule that detects WannaCry ransomware.", 5), f[0]);
        Assert.Equal(("Hackerwerkzeug", 4), (f[1].Kind, f[1].Severity));
        Assert.Equal(("möglicherweise unerwünschtes Programm", 3), (f[2].Kind, f[2].Severity));
    }

    [Fact(DisplayName = "YARA +2: Zusammenfassung – betroffene Dateien, Art und Name, Hinweis „nichts verändert“; ohne Treffer kein Freibrief")]
    public void Summary()
    {
        var s = YaraScan.Summary(YaraScan.Parse(Out), 4);
        Assert.StartsWith("2 Datei(en) mit 3 Treffer(n): rechnung.exe → Erpressersoftware WannaCry; tool.exe → Hackerwerkzeug Mimikatz", s); Assert.Contains("Nichts wurde verändert", s);
        Assert.Contains("kein Freibrief", YaraScan.Summary([], 2));
    }

    [Fact(DisplayName = "YARA +3: Argumente wie yr 1.22 erwartet – scan, ndjson, Meta, Tags, rekursiv, 200-MB-Grenze, 30 s je Datei, Regelordner, Ziel")]
    public void Args()
    {
        Assert.Equal(["scan", "-o", "ndjson", "-m", "-g", "--recursive", "--disable-console-logs", "-z", "209715200", "-a", "30", "R", "T"], YaraScan.Args("R", "T"));
        IReadOnlyList<string>? seen = null; var target = Path.Combine(_dir, "x.bin"); File.WriteAllText(target, "x");
        var (f, msg) = YaraScan.Scan(_dir, target, run: (e, a, _) => { seen = a; Assert.EndsWith("yr.exe", e); return new Cli.Result(0, Out, ""); });
        Assert.Equal(3, f!.Count); Assert.StartsWith("2 Datei(en)", msg); Assert.Equal(YaraScan.RulesDir(_dir), seen![^2]); Assert.Equal(target, seen[^1]);
    }

    [Fact(DisplayName = "YARA +4: Regeln flach übernehmen – nur *.yara unter yara/<kategorie>/, keine Pfade aus dem Archiv, sonst nichts")]
    public void Extract()
    {
        using var ms = new MemoryStream();
        using (var z = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            void Add(string n, string t) { using var w = new StreamWriter(z.CreateEntry(n).Open()); w.Write(t); }
            Add("rl-abc/yara/ransomware/Win32.Ransomware.WannaCry.yara", "rule a { condition: false }");
            Add("rl-abc/yara/trojan/Win32.Trojan.X.yara", "rule b { condition: false }");
            Add("rl-abc/README.md", "x"); Add("rl-abc/yara/../../boese.yara", "x"); Add("rl-abc/yara/ransomware/tief/zu.yara", "x"); Add("rl-abc/yara/trojan/skript.ps1", "x");
        }
        ms.Position = 0;
        Assert.Equal(2, YaraScan.ExtractRules(ms, _dir));
        Assert.Equal(["ransomware-Win32.Ransomware.WannaCry.yara", "trojan-Win32.Trojan.X.yara"], Directory.GetFiles(YaraScan.RulesDir(_dir)).Select(Path.GetFileName).Order());
        Assert.False(File.Exists(Path.Combine(Path.GetTempPath(), "boese.yara")));
    }

    [Fact(DisplayName = "YARA +5: Download nur mit passender Prüfsumme; Programm-Archiv liefert nur yr.exe; feste Stände gepinnt")]
    public async Task Fetch()
    {
        using var ms = new MemoryStream();
        using (var z = new ZipArchive(ms, ZipArchiveMode.Create, true)) { using (var w = new StreamWriter(z.CreateEntry("yr.exe").Open())) w.Write("MZ"); using (var w = new StreamWriter(z.CreateEntry("LIESMICH.txt").Open())) w.Write("x"); }
        var data = ms.ToArray(); var sha = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(data));
        var (ok, _) = await YaraScan.FetchAsync(_ => Task.FromResult<Stream>(new MemoryStream(data)), "u", sha); Assert.NotNull(ok);
        Assert.True(YaraScan.ExtractExe(ok!, _dir)); Assert.Equal(["yr.exe"], Directory.GetFiles(_dir).Select(Path.GetFileName));
        var (bad, msg) = await YaraScan.FetchAsync(_ => Task.FromResult<Stream>(new MemoryStream([1])), "u", sha); Assert.Null(bad); Assert.Contains("Prüfsumme", msg);
        Assert.Equal(64, YaraScan.ZipSha256.Length); Assert.Equal(64, YaraScan.RulesSha256.Length); Assert.Contains(YaraScan.RulesCommit, YaraScan.RulesUrl); Assert.Contains("v" + YaraScan.Version, YaraScan.ZipUrl);
    }

    [Fact(DisplayName = "YARA −1: fehlendes Ziel, fehlende Einrichtung, Programmfehler ohne Ausgabe, kaputte Antwort → klare Meldung")]
    public void Errors()
    {
        Assert.Equal("Diese Datei oder diesen Ordner gibt es nicht.", YaraScan.Scan(_dir, Path.Combine(_dir, "weg")).Message);
        Assert.Equal("Die Regel-Prüfung (yara-x) ist noch nicht eingerichtet.", YaraScan.Scan(_dir, _dir).Message);
        Assert.StartsWith("yara-x meldet: error: syntax", YaraScan.Scan(_dir, _dir, run: (_, _, _) => new Cli.Result(1, "", "error: syntax error\nmehr")).Message);
        Assert.Equal("yara-x hat keine lesbare Antwort geliefert.", YaraScan.Scan(_dir, _dir, run: (_, _, _) => new Cli.Result(0, "{kaputt", "")).Message);
    }
}

/// <summary>Echte Daten: das Regel-Archiv von ReversingLabs (fester Stand) – nur entpacken und lesen, nichts ausführen.</summary>
public class YaraRulesRealTests
{
    static string Zip => Path.Combine(Path.GetTempPath(), "trendo-tools-b", "yara-regeln", "rl-rules-e0a0be54.zip");
    public sealed class RulesFactAttribute : FactAttribute { public RulesFactAttribute() { if (!File.Exists(Zip)) Skip = "Übersprungen – Regel-Archiv nicht im Prüfordner."; } }

    [RulesFact(DisplayName = "YARA echt: Regel-Archiv hat die gepinnte Prüfsumme; alle 310 Regeln landen flach im Regelordner, jede mit Regelkopf, 309 mit Erkennungsart")]
    public void Real_rules()
    {
        var data = File.ReadAllBytes(Zip);
        Assert.Equal(YaraScan.RulesSha256, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(data)));
        var dir = Path.Combine(Path.GetTempPath(), "trendo-yararules-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.Equal(310, YaraScan.ExtractRules(new MemoryStream(data), dir));
            var files = Directory.GetFiles(YaraScan.RulesDir(dir), "*.yara");
            Assert.Equal(310, files.Length);
            Assert.All(files, f => Assert.Contains("rule ", File.ReadAllText(f)));
            Assert.Equal(309, files.Count(f => File.ReadAllText(f).Contains("tc_detection_type"))); // eine Zertifikats-Regel hat nur „category“ – Parse fällt darauf zurück
            Assert.Contains(files, f => Path.GetFileName(f).StartsWith("ransomware-", StringComparison.Ordinal));
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
