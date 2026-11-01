using Trendo.Core.Sys;
using Xunit;
using S = Trendo.Core.Sys.DownloadQueue.State;

namespace Trendo.Core.Tests;

/// Download-Manager: Warteschlange mit Höchstzahl gleichzeitiger Downloads, Anhalten/Fortsetzen, Reihenfolge, Prüfsumme, Neustart-sicher.
public class DownloadQueueTests
{
    static string Root() { var r = Path.Combine(Path.GetTempPath(), "trendo-dlq-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(r); return r; }

    [Fact(DisplayName = "Download-Manager: mehrere Links einfügen (nur http/https, ohne Doppelte), Namen ohne Zusammenstoß, höchstens N gleichzeitig in Listenreihenfolge")]
    public void Add_and_schedule()
    {
        var root = Root(); var dir = Path.Combine(root, "dl"); Directory.CreateDirectory(dir);
        try
        {
            var links = DownloadQueue.ParseLinks("Hier: https://example.org/a.zip, und ftp://x/y sowie (https://example.org/b.iso). Nochmal https://example.org/a.zip");
            Assert.Equal(["https://example.org/a.zip", "https://example.org/b.iso"], links.Select(u => u.AbsoluteUri));
            var q = new DownloadQueue(root) { MaxParallel = 2 };
            File.WriteAllText(Path.Combine(dir, "a.zip"), "alt");
            var a = q.Add(links[0], dir); var b = q.Add(links[1], dir); var c = q.Add(new Uri("https://example.org/c.pdf"), dir); var c2 = q.Add(new Uri("https://anders.example/c.pdf"), dir);
            Assert.Equal("a (2).zip", a.Name); Assert.Equal("c (2).pdf", c2.Name);
            Assert.Same(a, q.Add(links[0], dir)); // gleicher Link nicht doppelt
            Assert.Equal([a.Id, b.Id], q.ToStart().Select(j => j.Id));
            q.Started(a); q.Started(b);
            Assert.Empty(q.ToStart());
            q.Move(c2.Id, -1); Assert.Equal(c2.Id, q.Jobs[2].Id);
            Assert.Throws<ArgumentException>(() => q.Add(new Uri("ftp://example.org/x"), dir));
            Assert.Throws<ArgumentException>(() => q.Add(new Uri("https://example.org/x"), dir, "keine-pruefsumme"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "Download-Manager: Anhalten/Fortsetzen, Neustart macht aus „lädt“ „angehalten“ (lädt nie ungefragt weiter), Entfernen löscht nur Teil-Dateien")]
    public void Pause_resume_restart()
    {
        var root = Root(); var dir = Path.Combine(root, "dl"); Directory.CreateDirectory(dir);
        try
        {
            var q = new DownloadQueue(root);
            var a = q.Add(new Uri("https://example.org/gross.iso"), dir); q.Started(a);
            File.WriteAllText(Web.PartFile(a.Target), "teil"); DownloadQueue.Progress(a, 400, 1000, 2); q.Save();
            var q2 = new DownloadQueue(root);
            var a2 = q2.Get(a.Id);
            Assert.Equal((S.Angehalten, 400L, 1000L), (a2.Status, a2.Done, a2.Total)); Assert.Contains("unterbrochen", a2.Message);
            Assert.Empty(q2.ToStart());
            q2.Resume(a2.Id); Assert.Equal(S.Wartet, a2.Status);
            q2.Pause(a2.Id); Assert.Equal(S.Angehalten, a2.Status);
            q2.Remove(a2.Id);
            Assert.False(File.Exists(Web.PartFile(a.Target))); Assert.Empty(new DownloadQueue(root).Jobs);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact(DisplayName = "Download-Manager: Prüfsumme nach dem Laden – stimmt → fertig, stimmt nicht → Fehler mit Warnung; Geschwindigkeit geglättet, Restzeit")]
    public void Hash_and_speed()
    {
        var root = Root(); var dir = Path.Combine(root, "dl"); Directory.CreateDirectory(dir);
        try
        {
            var q = new DownloadQueue(root);
            var sha = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData("Trendo"u8));
            var ok = q.Add(new Uri("https://example.org/ok.txt"), dir, sha); File.WriteAllText(ok.Target, "Trendo"); q.Completed(ok);
            Assert.Equal((S.Fertig, "SHA-256 stimmt"), (ok.Status, ok.HashResult));
            var bad = q.Add(new Uri("https://example.org/bad.txt"), dir, new string('0', 64)); File.WriteAllText(bad.Target, "anders"); q.Completed(bad);
            Assert.Equal(S.Fehler, bad.Status); Assert.Contains("Nicht öffnen", bad.Message);
            Assert.Equal(1, q.ClearFinished()); Assert.Single(q.Jobs); Assert.True(File.Exists(ok.Target)); // fertige Datei bleibt
            var j = new DownloadQueue.Job { Total = 1000 };
            DownloadQueue.Progress(j, 100, 1000, 1); Assert.Equal(100, j.BytesPerSecond);
            DownloadQueue.Progress(j, 400, 1000, 1); Assert.Equal(160, j.BytesPerSecond); // 0,7·100 + 0,3·300
            Assert.Equal(3.75, j.SecondsLeft); Assert.Equal(0.4, j.Fraction);
            Assert.Equal("2 min 5 s", DownloadQueue.Duration(125)); Assert.Equal("–", DownloadQueue.Duration(-1));
        }
        finally { Directory.Delete(root, true); }
    }
}
