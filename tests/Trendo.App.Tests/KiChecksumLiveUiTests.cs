using System.Security.Cryptography;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>T-0739 ECHT (X-05, nur mit <c>TRENDO_ECHT_PRUEFSUMME</c> = Ordner für den Beleg): eine echte kleine Modelldatei aus dem Katalog
/// (Begleitdatei der Stimme „Eva“, 4 158 Bytes, Hugging Face) wird über Trendos eigenen Ladeweg geholt und gegen die feste Katalog-Summe geprüft und
/// installiert. Danach dieselben echten Bytes mit einem veränderten Byte: verworfen, klare Meldung, nichts installiert.
/// Ersetzt ist nur im zweiten Teil der Download (er liefert die manipulierten Bytes) und die Laufzeit-Datei von Piper (wird nicht gestartet).</summary>
public class KiChecksumLive_Ui
{
    [AvaloniaFact(DisplayName = "T-0739 ECHT: echte Modelldatei laden und per Katalog-SHA-256 prüfen; veränderte Bytes werden verworfen (nur mit TRENDO_ECHT_PRUEFSUMME)")]
    public async Task Real_file_checked_and_tampered_rejected()
    {
        if (Environment.GetEnvironmentVariable("TRENDO_ECHT_PRUEFSUMME") is not { Length: > 0 } dirEnv) return;
        var dir = Directory.CreateDirectory(dirEnv).FullName;
        var log = new StringBuilder();
        void Log(string s) => log.Append($"{DateTime.Now:HH:mm:ss} {s}\n");
        var eva = KiLibrary.Models.Single(x => x.Id == "piper-eva");
        var f = eva.Files.Single(x => x.FileName.EndsWith(".onnx.json", StringComparison.Ordinal));
        var m = eva with { Files = [f] }; // nur die kleine echte Datei – die 20-MB-Stimme wird für den Beleg nicht gebraucht
        var (w, _, root) = Ui.Open();
        try
        {
            Log($"Katalog: {m.Id} · {f.FileName} · {f.Bytes} Bytes · SHA-256 {f.Sha256}");
            Log($"Herkunft der Summe: {f.ShaSource}");
            Log($"Adresse: {f.Url}");
            var exe = w.KiStore.RuntimeExe(KiRuntime.Piper); // Laufzeit gilt als da (wird nicht gestartet), damit nur die Modelldatei geladen wird
            Directory.CreateDirectory(Path.GetDirectoryName(exe)!); File.WriteAllText(exe, "MZ");

            // 1. Echter Download über Trendos Ladeweg (erlaubte Server, Größenkontrolle) und Prüfung gegen die Katalog-Summe
            await w.KiLibraryInstall(m);
            var msg1 = w.Find<TextBlock>("kilib-msg").Text ?? "";
            Log("Meldung nach echtem Laden: " + msg1);
            var installed = w.KiStore.ModelFile(m, f);
            if (!File.Exists(installed) && (msg1.Contains("Internet") || msg1.Contains("HttpRequest") || msg1.Contains("Verbindung") || msg1.Contains("hat nicht geklappt")))
            { Assert.Skip("BLOCKIERT – keine Verbindung zu huggingface.co: " + msg1); return; }
            Assert.True(File.Exists(installed), msg1);
            var real = await File.ReadAllBytesAsync(installed);
            var realSha = Convert.ToHexStringLower(SHA256.HashData(real));
            Log($"Installiert: {real.Length} Bytes, SHA-256 {realSha} – Katalog stimmt: {(realSha == f.Sha256 ? "ja" : "NEIN")}");
            Assert.Equal(f.Sha256, realSha);
            Assert.True(w.KiStore.ModelFileOk(m, f));

            // 2. Dieselben echten Bytes, ein Byte verändert (gleiche Größe): Download ersetzt durch die manipulierte Datei
            Assert.True(w.KiStore.RemoveModel(m));
            var bad = (byte[])real.Clone(); bad[100] ^= 0x20;
            Log($"Manipuliert: Byte 100 von 0x{real[100]:x2} auf 0x{bad[100]:x2}, SHA-256 {Convert.ToHexStringLower(SHA256.HashData(bad))}");
            var dl = Path.Combine(root, "manipuliert-" + f.FileName);
            w.KiDownloadOverride = (url, name) => { File.WriteAllBytes(dl, bad); return Task.FromResult<(string, string?)>((dl, null)); };
            await w.KiLibraryInstall(m);
            var msg2 = w.Find<TextBlock>("kilib-msg").Text ?? "";
            Log("Meldung nach manipulierter Datei: " + msg2);
            Assert.Contains("Prüfsumme passt nicht", msg2);
            Assert.Contains("verworfen, nichts installiert", msg2);
            Assert.False(File.Exists(installed));
            Assert.False(File.Exists(dl));
            Log("Ergebnis: installierte Datei vorhanden = nein, heruntergeladene Datei verworfen = ja");
            w.Show("ki-bibliothek"); w.KiLibraryShow(KiKind.TextZuSprache);
            Log("Anzeige in der KI-Bibliothek: " + w.Find<TextBlock>("kilib-sha-piper-eva").Text + " / " + w.Find<TextBlock>("kilib-sha-piper-thorsten").Text);
        }
        finally
        {
            w.Close();
            File.WriteAllText(Path.Combine(dir, "t0739-pruefsumme.log"), log.ToString(), new UTF8Encoding(false));
        }
    }
}
