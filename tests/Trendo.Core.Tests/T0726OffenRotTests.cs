using System.Text.Json;
using Trendo.Core;
using Xunit;
using Xunit.Abstractions;

namespace Trendo.Core.Tests;

/// <summary>T-0726 (offene Probleme, Gruppe A): Tests, die nur die bisherige Schnittstelle benutzen – sie laufen auch auf dem alten Stand und sind dort rot.
/// A1 Fach.Save atomar, A4 offen.json zwischen Prozessen, A8 Vorschaubilder der Zeitleiste atomar.</summary>
public class T0726OffenRot(ITestOutputHelper output)
{
    static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "trendo-t0726-" + Guid.NewGuid().ToString("N")[..8]); Directory.CreateDirectory(d); return d; }

    public sealed class Probe { public int N { get; set; } public List<string> Zeilen { get; set; } = []; }

    [Fact(DisplayName = "T-0726 A1: Fach.Save aus zwei Fenstern gleichzeitig – kein Fehler, Datei immer vollständig lesbar, keine liegengebliebene Zwischendatei")]
    public void Fach_save_two_windows()
    {
        var dir = Tmp();
        try
        {
            var big = Enumerable.Range(0, 3000).Select(i => "Zeile " + i + " – Messwert, Grenzwert, Hinweis").ToList();
            var errors = new System.Collections.Concurrent.ConcurrentBag<string>();
            var stop = DateTime.UtcNow.AddSeconds(4);
            var readers = 0;
            void Writer(int start)
            {
                for (var n = start; DateTime.UtcNow < stop; n += 2)
                    try { Fach.Save(dir, "monitor", "anlagen", new Probe { N = n, Zeilen = big }); }
                    catch (Exception e) { errors.Add(e.GetType().Name + ": " + e.Message); }
            }
            void Reader()
            {
                var p = Path.Combine(Fach.StoreDir(dir, "monitor"), "anlagen.json");
                while (DateTime.UtcNow < stop)
                {
                    if (!File.Exists(p)) continue;
                    string text;
                    try { text = File.ReadAllText(p); } catch (IOException) { continue; } // gerade ersetzt
                    try { var x = JsonSerializer.Deserialize<Probe>(text)!; if (x.Zeilen.Count != big.Count) errors.Add("halbe Datei"); Interlocked.Increment(ref readers); }
                    catch (JsonException) { errors.Add("unlesbar"); }
                }
            }
            // eigene Threads statt Parallel.Invoke: unter Last (voller Testlauf) lief der Leser sonst erst nach Ablauf der Zeit
            var threads = new[] { new Thread(() => Writer(0)), new Thread(() => Writer(1)), new Thread(Reader) };
            foreach (var t in threads) t.Start();
            foreach (var t in threads) t.Join();
            Assert.True(errors.IsEmpty, $"{errors.Count} Fehler, z. B.: {errors.FirstOrDefault()}");
            Assert.True(readers > 0, "Der Leser kam nie zum Zug – Test taugt so nicht.");
            Assert.Equal(big.Count, Fach.Load<Probe>(dir, "monitor", "anlagen").Zeilen.Count);
            Assert.Empty(Directory.GetFiles(Fach.StoreDir(dir, "monitor"), "*.tmp"));
            output.WriteLine($"zwei Schreiber, {readers} Lesungen, kein Fehler");
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    [Fact(DisplayName = "T-0726 A4: offen.json – zwei Trendo-Fenster (eigene Freigabe-Tore, gleicher Datenordner) legen je eine fortsetzbare Anfrage an; nach dem Neustart sind beide da")]
    public void Offen_json_two_processes()
    {
        var dir = Tmp();
        try
        {
            var audit = new AuditLog(Path.Combine(dir, "audit", "audit.jsonl"));
            var store = Path.Combine(dir, "freigaben", "offen.json");
            var a = new ApprovalGate(audit, null, store); // Fenster 1
            var b = new ApprovalGate(audit, null, store); // Fenster 2 (eigener Speicher, wie ein zweiter Prozess)
            var ra = a.Request(Principal.LocalOwner, Integrations.OpenUrl, "Seite öffnen", "https://example.org/a", resumable: true);
            var rb = b.Request(Principal.LocalOwner, Integrations.OpenUrl, "Seite öffnen", "https://example.org/b", resumable: true);
            // viele Anfragen gleichzeitig aus beiden Fenstern
            var more = new System.Collections.Concurrent.ConcurrentBag<string>();
            Parallel.For(0, 40, i => more.Add((i % 2 == 0 ? a : b).Request(Principal.LocalOwner, Integrations.OpenUrl, "Seite öffnen", $"https://example.org/p{i}", resumable: true).Id));
            // Fenster 1 lehnt seine erste Anfrage ab – die Anfragen von Fenster 2 bleiben in der Ablage
            a.Deny(ra.Id, Principal.LocalOwner);
            var after = new ApprovalGate(audit, null, store).Pending().Select(r => r.Id).ToHashSet();
            Assert.DoesNotContain(ra.Id, after);
            Assert.Contains(rb.Id, after);
            Assert.Equal(41, after.Count);
            Assert.All(more, id => Assert.Contains(id, after));
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(store)!, "*.tmp"));
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    static string ProbeExe()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var config = here.Parent!.Name;
        var root = here; while (root is not null && !File.Exists(Path.Combine(root.FullName, "Trendo.sln"))) root = root.Parent;
        var exe = Path.Combine(root!.FullName, "tests", "Trendo.CrashProbe", "bin", config, here.Name, OperatingSystem.IsWindows() ? "Trendo.CrashProbe.exe" : "Trendo.CrashProbe");
        Assert.True(File.Exists(exe), "Hilfsprogramm fehlt: " + exe);
        return exe;
    }

    [Fact(DisplayName = "T-0726 A8: abgebrochenes FFmpeg hinterlässt kein halbes Vorschaubild – kein Bild in der Zeitleiste, beim nächsten Mal neu erzeugt (nicht ewig kaputt)")]
    public void Frames_atomic_when_ffmpeg_dies()
    {
        var dir = Tmp();
        try
        {
            var video = Path.Combine(dir, "clip.mp4"); File.WriteAllBytes(video, [0, 0, 0, 0x18]);
            var thumbs = Path.Combine(dir, "bilder");
            var list = MediaStudio.Frames(ProbeExe(), video, 10, 4, thumbs);
            Assert.Empty(list); // kein halbes Bild als „Vorschaubild“
            Assert.Empty(Directory.GetFiles(thumbs)); // auch keine Zwischendatei
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    [Fact(DisplayName = "T-0726 A8 (echtes FFmpeg): Vorschaubilder sind gültige PNG, keine Zwischendateien, zweiter Aufruf verwendet sie wieder")]
    public void Frames_real_ffmpeg()
    {
        var ffDir = EchtWerkzeuge.FfmpegDir() ?? (Cli.Find("ffmpeg") is { } f ? Path.GetDirectoryName(f) : null);
        if (ffDir is null) { output.WriteLine("BLOCKIERT: kein FFmpeg auf diesem PC"); return; }
        var ff = Path.Combine(ffDir, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
        var dir = Tmp();
        try
        {
            var video = Path.Combine(dir, "test.mp4");
            var r = Cli.Run(ff, ["-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i", "testsrc=duration=3:size=320x240:rate=10", "-pix_fmt", "yuv420p", video], dir, 60000);
            Assert.True(r.Ok, r.Err);
            var thumbs = Path.Combine(dir, "bilder");
            var list = MediaStudio.Frames(ff, video, 3, 4, thumbs);
            Assert.Equal(4, list.Count);
            foreach (var png in list)
            {
                var b = File.ReadAllBytes(png);
                Assert.True(b.Length > 100 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47, png);
                Assert.Equal([0x49, 0x45, 0x4E, 0x44], b[^8..^4]); // IEND: vollständiges PNG
            }
            Assert.Empty(Directory.GetFiles(thumbs, "*.teil*"));
            var t0 = File.GetLastWriteTimeUtc(list[0]);
            Assert.Equal(list, MediaStudio.Frames(ff, video, 3, 4, thumbs));
            Assert.Equal(t0, File.GetLastWriteTimeUtc(list[0]));
            output.WriteLine($"echtes FFmpeg ({ff.Replace(Environment.UserName, "<user>")}): 4 gültige PNG");
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
