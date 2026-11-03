using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Trendo.App;
using Trendo.Core;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Wunsch 03.11.2026: Leiste unten fotorealistisch, WhatsApp, Telegram & Co. mit Original-Symbol.</summary>
public class DockLookUiTests
{
    static string RepoRoot() { for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent) if (File.Exists(Path.Combine(d.FullName, "Trendo.sln"))) return d.FullName; throw new DirectoryNotFoundException(); }
    static async Task Pump() { for (var i = 0; i < 10; i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(15); } }

    static byte[] Png(int w, int h)
    {
        static uint Crc(byte[] d) { uint c = 0xFFFFFFFF; foreach (var b in d) { c ^= b; for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1; } return ~c; }
        static byte[] Be(uint v) => [(byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v];
        var ms = new MemoryStream(); ms.Write([0x89, 0x50, 0x4E, 0x47, 13, 10, 26, 10]);
        void Chunk(string type, byte[] data) { ms.Write(Be((uint)data.Length)); var td = System.Text.Encoding.ASCII.GetBytes(type).Concat(data).ToArray(); ms.Write(td); ms.Write(Be(Crc(td))); }
        Chunk("IHDR", [.. Be((uint)w), .. Be((uint)h), 8, 2, 0, 0, 0]);
        var raw = new byte[h * (1 + 3 * w)]; new Random(7).NextBytes(raw); for (var y = 0; y < h; y++) raw[y * (1 + 3 * w)] = 0; // Rauschen: so groß wie ein echtes Symbol
        var z = new MemoryStream(); using (var zs = new System.IO.Compression.ZLibStream(z, System.IO.Compression.CompressionLevel.Optimal, true)) zs.Write(raw);
        Chunk("IDAT", z.ToArray()); Chunk("IEND", []);
        return ms.ToArray();
    }

    [AvaloniaFact(DisplayName = "Leiste: fotorealistische Bilder auf Glas-Kacheln für Dateien, Einstellungen, Papierkorb …; Blender/Unreal und Start behalten ihr Zeichen")]
    public void Photo_tiles()
    {
        var (w, _, _) = Ui.Open();
        try
        {
            foreach (var id in new[] { "dock-files", "dock-settings", "dock-security", "dock-trash" })
                Assert.True(w.Find<Button>(id).GetLogicalDescendants().OfType<Image>().Any(), "ohne Foto: " + id);
            Assert.NotNull(w.TryFind<Border>("dock-bar"));
            foreach (var id in new[] { "files", "browser", "mail", "ki", "media", "settings", "components", "code", "office", "security", "trash" })
                Assert.True(File.Exists(Path.Combine(RepoRoot(), "src", "Trendo.App", "Assets", "dock", id + ".png")), id);
        }
        finally { w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Leiste: hinzugefügte App erscheint unten; Original-Symbol nur von den Seiten des Dienstes (fremde Adressen nie gefragt), danach mit Bild; Entfernen nimmt sie wieder heraus")]
    public async Task Web_apps_with_original_icon()
    {
        var (w, _, root) = Ui.Open();
        var asked = new List<string>();
        w.WebIconFetchOverride = u =>
        {
            asked.Add(u.ToString());
            return Task.FromResult<byte[]?>(u.AbsolutePath switch
            {
                "/a/" => System.Text.Encoding.UTF8.GetBytes("""<link rel="apple-touch-icon" sizes="180x180" href="./apple-touch-icon.png"><link rel="icon" href="https://evil.example/x.png">"""),
                "/a/apple-touch-icon.png" => Png(180, 180),
                _ => null,
            });
        };
        try
        {
            Assert.Null(w.TryFind<Button>("dock-app-telegram"));
            w.InstallWebApp("telegram"); await Pump();
            var file = await w.FetchWebAppIcon("telegram"); await Pump();
            Assert.NotNull(file); Assert.Equal(file, WebAppIcons.Cached(root, "telegram"));
            Assert.DoesNotContain(asked, a => a.Contains("evil.example"));
            Assert.True(w.Find<Button>("dock-app-telegram").GetLogicalDescendants().OfType<Image>().Any());
            w.RemoveWebApp("telegram"); await Pump();
            Assert.Null(w.TryFind<Button>("dock-app-telegram"));
        }
        finally { w.WebIconFetchOverride = null; w.Close(); }
    }

    [AvaloniaFact(DisplayName = "Leiste offline: App erscheint mit Farbkachel, Trendo fragt kein Netz")]
    public async Task Offline_no_fetch()
    {
        var (w, _, _) = Ui.Open(offline: true);
        var asked = 0; w.WebIconFetchOverride = _ => { asked++; return Task.FromResult<byte[]?>(null); };
        try
        {
            w.InstallWebApp("whatsapp"); await Pump();
            Assert.NotNull(w.TryFind<Button>("dock-app-whatsapp"));
            Assert.Null(await w.FetchWebAppIcon("whatsapp"));
            Assert.Equal(0, asked);
        }
        finally { w.WebIconFetchOverride = null; w.Close(); }
    }
}
