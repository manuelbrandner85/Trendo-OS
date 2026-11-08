using System.Diagnostics;
using System.Net.Sockets;
using Trendo.Core;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Echte Läufe der Runde-2-Werkzeuge über die Trendo-Adapter. Laufen nur, wenn TRENDO_WERKZEUGE_TEST auf einen Ordner mit den entpackten
/// offiziellen Downloads zeigt (typst/, mediamtx/, streamlink/); sonst übersprungen. Beleg: reports/alle-studios/runde2-werkzeuge-e2e.md.</summary>
public class ToolAdaptersRealTests
{
    public sealed class ToolFactAttribute : FactAttribute
    {
        public ToolFactAttribute(string tool) { if (Exe(tool) is null) Skip = $"Übersprungen – {tool} nicht bereitgestellt (TRENDO_WERKZEUGE_TEST)."; }
    }
    static string? Exe(string tool)
    {
        var d = Environment.GetEnvironmentVariable("TRENDO_WERKZEUGE_TEST"); if (string.IsNullOrEmpty(d) || !Directory.Exists(Path.Combine(d, tool))) return null;
        var name = OperatingSystem.IsWindows() ? tool + ".exe" : tool;
        return Directory.EnumerateFiles(Path.Combine(d, tool), name, SearchOption.AllDirectories).FirstOrDefault(f => !f.Contains("Python", StringComparison.Ordinal));
    }

    [ToolFact("typst")]
    public void Typst_real_letter()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-typst-echt-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            var typ = Path.Combine(dir, "rechnung.typ");
            File.WriteAllText(typ, TypstDoc.Letter("Rechnung #17 *eilig*", "Sehr geehrte Frau Muster,\n\nfür Größe XL & Übergröße berechnen wir: $5 [Rabatt]", [(2, "Stühle \"Eiche\"", 59.5m), (1, "Tisch_rund", 349m)]));
            var (ok, msg) = TypstDoc.Compile(Exe("typst")!, typ, Path.Combine(dir, "rechnung.pdf"), dir);
            Assert.True(ok, msg); Assert.StartsWith("%PDF", File.ReadAllText(msg)[..4]);
            var (ok2, png) = TypstDoc.Compile(Exe("typst")!, typ, Path.Combine(dir, "rechnung.png"), dir);
            Assert.True(ok2, png); Assert.True(new FileInfo(png).Length > 5000);
            if (Environment.GetEnvironmentVariable("TRENDO_WERKZEUGE_BELEG") is { Length: > 0 } beleg) { Directory.CreateDirectory(beleg); File.Copy(png, Path.Combine(beleg, "typst-rechnung.png"), true); }
            var outside = Path.Combine(dir, "boese.typ"); File.WriteAllText(outside, "#read(\"../../geheim.txt\")");
            Assert.False(TypstDoc.Compile(Exe("typst")!, outside, Path.Combine(dir, "boese.pdf"), dir).Ok); // --root sperrt Dateien außerhalb
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    [ToolFact("mediamtx")]
    public void Mediamtx_real_local_only()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-mtx-echt-" + Guid.NewGuid().ToString("N"));
        var args = LiveServer.Prepare(dir, 18554, 18888);
        var psi = new ProcessStartInfo(Exe("mediamtx")!) { WorkingDirectory = dir, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        try
        {
            var ok = false;
            for (var i = 0; i < 50 && !ok; i++) { try { using var c = new TcpClient(); c.Connect("127.0.0.1", 18554); ok = c.Connected; } catch (SocketException) { Thread.Sleep(100); } }
            Assert.True(ok, "RTSP-Port 18554 auf 127.0.0.1 nicht erreichbar");
            using var h = new TcpClient(); h.Connect("127.0.0.1", 18888); Assert.True(h.Connected);
        }
        finally { p.Kill(); p.WaitForExit(5000); try { Directory.Delete(dir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }

    [ToolFact("streamlink")]
    public void Streamlink_real_public_live()
    {
        var exe = Exe("streamlink")!;
        var info = LiveResolve.Info(exe, LiveResolve.Channels[1].Url);
        Assert.True(info.Ok, info.Message); Assert.Contains("best", info.Qualities);
        var (ok, url) = LiveResolve.StreamUrl(exe, LiveResolve.Channels[1].Url);
        Assert.True(ok, url); Assert.Contains(".m3u8", url);
    }
}
