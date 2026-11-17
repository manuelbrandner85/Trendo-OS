using System.IO.Compression;
using System.Security.Cryptography;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>KI-Bibliothek: Katalog vollständig und ehrlich, Ablage prüft Größe und Prüfsumme (Vorgabe 2026-10-26).</summary>
public class KiLibraryTests
{
    [Fact(DisplayName = "KI-Bibliothek: jede Art hat Modelle, jedes Modell Lizenz, Quelle und Bedienhinweis")]
    public void Catalog_complete()
    {
        foreach (var k in Enum.GetValues<KiKind>())
        {
            Assert.True(KiLibrary.Kinds.ContainsKey(k), "Name fehlt: " + k);
            Assert.NotEmpty(KiLibrary.OfKind(k));
        }
        Assert.Equal(KiLibrary.Models.Count, KiLibrary.Models.Select(m => m.Id).Distinct().Count());
        foreach (var m in KiLibrary.Models)
        {
            Assert.False(string.IsNullOrWhiteSpace(m.License), m.Id);
            Assert.False(string.IsNullOrWhiteSpace(m.HowTo), m.Id);
            if (m.Runtime == KiRuntime.Ollama) Assert.False(string.IsNullOrWhiteSpace(m.Tag), m.Id);
            foreach (var f in m.Files)
            {
                Assert.True(f.Url.StartsWith("https://huggingface.co/") || f.Url.StartsWith(Vollpaket.OnlineBase), f.Url); // offizielle Quelle oder Trendos eigene Ablage (Apache-2.0-Modelle der Video-Prüfung)
                Assert.True(f.Bytes > 0, f.FileName);
                Assert.Equal(Path.GetFileName(f.FileName), f.FileName);
            }
        }
        foreach (var r in KiLibrary.Runtimes.Where(r => !r.FromVollpaket && KiPyApps.For(r.Id) is null)) // Python-KIs: Stand per Git-Commit geprüft (KiPyAppsTests)
        {
            Assert.Matches("^[0-9a-f]{64}$", r.Sha256);
            if (r.Url is null) Assert.StartsWith("https://github.com/" + r.Repo + "/releases/download/", r.DownloadUrl);
            else Assert.Equal("https://files.acemusic.ai/acemusic/win/ACE-Step-1.5.7z", r.DownloadUrl); // einzige Ausnahme: im offiziellen README verlinkt
            Assert.True(r.Bytes > 0 && r.Exe.Length > 0, r.Name);
        }
    }

    static string Temp() { var d = Path.Combine(Path.GetTempPath(), "trendo-kilib-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    [Fact(DisplayName = "KI-Bibliothek: Modelldatei nur mit passender Größe und Prüfsumme, Entfernen löscht nur den eigenen Ordner")]
    public void Store_checks_files()
    {
        var d = Temp();
        try
        {
            var store = new KiLibraryStore(d);
            var m = KiLibrary.Models.First(x => x.Id == "piper-eva");
            var f = m.Files[1] with { Sha256 = null, ShaSource = null }; // die kleine JSON-Datei – hier ohne feste Katalog-Summe (Weg „Summe vom Server“, T-0739)
            var wrong = Path.Combine(d, "x"); File.WriteAllBytes(wrong, new byte[10]);
            Assert.Throws<InvalidOperationException>(() => store.InstallModelFile(m, f, wrong, null));
            var ok = Path.Combine(d, "y"); File.WriteAllBytes(ok, new byte[f.Bytes]);
            Assert.Throws<InvalidOperationException>(() => store.InstallModelFile(m, f, ok, new string('a', 64)));
            Assert.False(File.Exists(ok)); // T-0739: abgelehnte Datei wird verworfen
            File.WriteAllBytes(ok, new byte[f.Bytes]);
            var sha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(ok)));
            store.InstallModelFile(m, f, ok, sha);
            Assert.True(File.Exists(store.ModelFile(m, f)));
            Assert.False(store.ModelReady(m)); // .onnx fehlt noch
            Directory.CreateDirectory(Path.Combine(d, "anderes")); File.WriteAllText(Path.Combine(d, "anderes", "bleibt.txt"), "x");
            Assert.True(store.RemoveModel(m));
            Assert.False(Directory.Exists(store.ModelDir(m)));
            Assert.True(File.Exists(Path.Combine(d, "anderes", "bleibt.txt")));
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "T-0739 X-05: jede Hugging-Face-Datei hat eine feste SHA-256 mit Herkunft; ohne Summe steht ehrlich „ohne Prüfsumme“")]
    public void Catalog_checksums()
    {
        foreach (var m in KiLibrary.Models)
            foreach (var f in m.Files)
            {
                if (f.Url.StartsWith("https://huggingface.co/"))
                {
                    Assert.Matches("^[0-9a-f]{64}$", f.Sha256);
                    Assert.False(string.IsNullOrWhiteSpace(f.ShaSource), f.FileName);
                }
                else { Assert.Null(f.Sha256); Assert.Null(f.ShaSource); } // eigene Ablage: Summe nicht aus offizieller Quelle belegt – nicht raten
            }
        // gleiche Datei in zwei Modellen: dieselbe Summe
        foreach (var g in KiLibrary.Models.SelectMany(m => m.Files).GroupBy(f => f.FileName))
            Assert.Single(g.Select(f => f.Sha256).Distinct());
        Assert.Equal((true, "Prüfsumme: SHA-256 je Datei fest im Katalog"), KiLibrary.Models.Single(m => m.Id == "whisper-base").Checksum);
        Assert.False(KiLibrary.Models.Single(m => m.Id == "video-pruefung").Checksum.Fixed);
        Assert.StartsWith("ohne Prüfsumme", KiLibrary.Models.Single(m => m.Id == "video-pruefung").Checksum.Text);
        Assert.StartsWith("ohne Prüfsumme", KiLibrary.Models.Single(m => m.Id == "triposr").Checksum.Text);
        Assert.Contains("Ollama", KiLibrary.Models.Single(m => m.Id == "qwen3-4b").Checksum.Text);
        Assert.True(KiLibrary.Models.Single(m => m.Id == "realesrgan-x4").Checksum.Fixed); // Programmpaket mit fester SHA-256
    }

    [Fact(DisplayName = "T-0739 X-05: feste Katalog-Summe entscheidet – richtige Datei wird installiert, veränderte Bytes werden verworfen, Widerspruch Server/Katalog ebenso")]
    public void Catalog_sha_enforced()
    {
        var d = Temp();
        try
        {
            var store = new KiLibraryStore(d);
            var audit = new AuditLog(Path.Combine(d, "audit.jsonl"));
            var m = KiLibrary.Models.First(x => x.Id == "piper-eva");
            var bytes = System.Text.Encoding.UTF8.GetBytes("{\"audio\":{\"sample_rate\":16000}}");
            var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
            var f = m.Files[1] with { Bytes = bytes.Length, Sha256 = sha, ShaSource = "Test" };
            // 1. veränderte Bytes, gleiche Größe: verworfen, Datei gelöscht, nichts installiert – auch wenn der Server die falsche Summe mitschickt
            var bad = Path.Combine(d, "bad"); var b2 = (byte[])bytes.Clone(); b2[5] ^= 0x01; File.WriteAllBytes(bad, b2);
            var ex = Assert.Throws<InvalidOperationException>(() => store.InstallModelFile(m, f, bad, null, audit));
            Assert.Contains("Prüfsumme passt nicht", ex.Message); Assert.Contains("verworfen, nichts installiert", ex.Message);
            Assert.False(File.Exists(bad)); Assert.False(File.Exists(store.ModelFile(m, f)));
            File.WriteAllBytes(bad, b2);
            Assert.Throws<InvalidOperationException>(() => store.InstallModelFile(m, f, bad, Convert.ToHexStringLower(SHA256.HashData(b2)), audit));
            Assert.False(File.Exists(bad)); Assert.False(File.Exists(store.ModelFile(m, f)));
            // 2. richtige Datei, Server nennt eine andere Summe: Widerspruch → verworfen
            var good = Path.Combine(d, "good"); File.WriteAllBytes(good, bytes);
            Assert.Contains("weicht vom Katalog ab", Assert.Throws<InvalidOperationException>(() => store.InstallModelFile(m, f, good, new string('b', 64), audit)).Message);
            Assert.False(File.Exists(good));
            // 3. richtige Datei: installiert, Audit nennt Summe und Quelle
            File.WriteAllBytes(good, bytes);
            store.InstallModelFile(m, f, good, null, audit);
            Assert.Equal(bytes, File.ReadAllBytes(store.ModelFile(m, f)));
            Assert.True(store.ModelFileOk(m, f));
            // 4. installierte Datei nachträglich verändert (gleiche Größe): gilt nicht mehr als vorhanden – wird beim nächsten Laden neu geholt
            File.WriteAllBytes(store.ModelFile(m, f), b2);
            Assert.False(store.ModelFileOk(m, f));
            var log = File.ReadAllText(Path.Combine(d, "audit.jsonl"));
            Assert.Contains("ki.model.file.rejected", log);
            Assert.Contains("sha256=" + sha + " quelle=katalog", log);
        }
        finally { Directory.Delete(d, true); }
    }

    [Fact(DisplayName = "KI-Bibliothek: Laufzeit-ZIP mit falscher Größe oder Prüfsumme wird abgelehnt")]
    public void Runtime_checked()
    {
        var d = Temp();
        try
        {
            var zip = Path.Combine(d, "rt.zip");
            using (var z = ZipFile.Open(zip, ZipArchiveMode.Create)) using (var s = new StreamWriter(z.CreateEntry("piper/piper.exe").Open())) s.Write("MZ");
            var store = new KiLibraryStore(d);
            Assert.Throws<InvalidOperationException>(() => store.InstallRuntime(KiRuntime.Piper, zip));
            Assert.False(store.RuntimeReady(KiRuntime.Piper));
            Assert.Throws<InvalidOperationException>(() => store.InstallRuntime(KiRuntime.Ollama, zip));
        }
        finally { Directory.Delete(d, true); }
    }
}
