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
                Assert.StartsWith("https://huggingface.co/", f.Url);
                Assert.True(f.Bytes > 0, f.FileName);
                Assert.Equal(Path.GetFileName(f.FileName), f.FileName);
            }
        }
        foreach (var r in KiLibrary.Runtimes.Where(r => !r.FromVollpaket))
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
            var f = m.Files[1]; // die kleine JSON-Datei
            var wrong = Path.Combine(d, "x"); File.WriteAllBytes(wrong, new byte[10]);
            Assert.Throws<InvalidOperationException>(() => store.InstallModelFile(m, f, wrong, null));
            var ok = Path.Combine(d, "y"); File.WriteAllBytes(ok, new byte[f.Bytes]);
            Assert.Throws<InvalidOperationException>(() => store.InstallModelFile(m, f, ok, new string('a', 64)));
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
