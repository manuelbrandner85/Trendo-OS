using System.Net;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>CORE-06 G1–G3: Freie Assets im Spiele-Editor – Godot-Suche, Download-Übernahme (auch ZIP), als Animation (Bilder aus dem Streifen) und Ton verwenden,
/// Asset-Liste, Abspann, Lizenzwarnung auch unter „Veröffentlichen“.</summary>
public class GameAssets_Ui
{
    const string GK = "spiele-programm";
    static void Pump() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    sealed class Fake : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            var u = r.RequestUri!.AbsoluteUri;
            var body = u.StartsWith("https://godotengine.org/asset-library/api/asset?") ? """{"result":[{"asset_id":"5","title":"Retro Töne","author":"Erika Muster","cost":"CC0","category":"Audio","icon_url":""}]}""" : null;
            return Task.FromResult(body is null ? new HttpResponseMessage(HttpStatusCode.NotFound) : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8) });
        }
    }

    /// <summary>Echtes, kleines PNG (einfarbig) – unabhängig vom Zeichner der Testumgebung.</summary>
    static byte[] Png(int w, int h)
    {
        static byte[] Be(int v) => [(byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v];
        static uint Crc(byte[] d) { uint c = 0xFFFFFFFF; foreach (var b in d) { c ^= b; for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1; } return ~c; }
        byte[] Chunk(string type, byte[] data) { var t = Encoding.ASCII.GetBytes(type); var crc = Crc([.. t, .. data]); return [.. Be(data.Length), .. t, .. data, .. Be((int)crc)]; }
        var raw = new MemoryStream(); for (var y = 0; y < h; y++) { raw.WriteByte(0); for (var x = 0; x < w; x++) { raw.WriteByte(47); raw.WriteByte(134); raw.WriteByte(246); } }
        var z = new MemoryStream(); using (var zs = new System.IO.Compression.ZLibStream(z, System.IO.Compression.CompressionLevel.Fastest, true)) raw.WriteTo(zs);
        return [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, .. Chunk("IHDR", [.. Be(w), .. Be(h), 8, 2, 0, 0, 0]), .. Chunk("IDAT", z.ToArray()), .. Chunk("IEND", [])];
    }

    [AvaloniaFact(DisplayName = "CORE-06 G1–G3: Assets – Godot-Suche, neue Downloads übernehmen, Bildstreifen als Lauf-Animation (4 Bilder), Ton als Sprung-Geräusch, Asset-Liste mit Abspann, NC-Lizenz warnt beim Veröffentlichen")]
    public async Task Assets()
    {
        var (w, _, root) = Ui.Open();
        try
        {
            w.BibHttpOverride = new Fake();
            w.Show(GK); w.Find<Button>("prog-tpl-retro-plattform").Press();
            w.GameSection = "Assets"; w.Show(GK); Pump();
            var d = w.ProgramActive(GK)!;
            Assert.NotNull(w.TryFind<StackPanel>("game-assets"));
            await w.GameAssetSearch("retro"); Pump();
            Assert.Equal("Retro Töne", Assert.Single(w.GameAssetHits).Title);
            // Download-Ordner: Bildstreifen 128×32 und ein Ton
            var dl = Path.Combine(root, "Downloads"); Directory.CreateDirectory(dl); w.DownloadsOverride = dl;
            var strip = Path.Combine(dl, "lauf.png"); File.WriteAllBytes(strip, Png(128, 32));
            File.WriteAllBytes(Path.Combine(dl, "sprung.wav"), [0x52, 0x49, 0x46, 0x46]);
            w.Find<Button>("game-asset-pickup").Press(); Pump();
            Assert.Equal(2, w.GameAssetPending.Count);
            var iPng = w.GameAssetPending.ToList().FindIndex(f => f.EndsWith("lauf.png")); var iWav = 1 - iPng;
            w.Find<ComboBox>($"game-asset-use-{iPng}").SelectedIndex = 2; // Bilder: Figur, Stehen, Laufen …
            w.Find<Button>($"game-asset-use-go-{iPng}").Press(); Pump();
            var anim = d.Data["anims"]!["laufen"]!;
            Assert.Equal(4, (int)Trendo.Core.Programs.ProgramDocument.ToNumber(anim["frames"]));
            iWav = w.GameAssetPending.ToList().FindIndex(f => f.EndsWith("sprung.wav"));
            w.Find<TextBox>($"game-asset-lic-{iWav}").Text = "CC BY-NC 4.0"; w.Find<TextBox>($"game-asset-author-{iWav}").Text = "Erika Muster";
            w.Find<Button>($"game-asset-use-go-{iWav}").Press(); Pump(); // erste Ton-Verwendung: Springen
            Assert.NotNull(d.Data["sounds"]?["springen"]);
            Assert.Equal(2, d.List("assets").Count);
            w.Show(GK); Pump();
            Assert.Contains("Kenney", w.Find<TextBox>("game-asset-credits").Text);
            Assert.Contains("verkauften Spiel", string.Join("|", w.GetLogicalTexts()));
            w.GameSection = "Veröffentlichen"; w.Show(GK); Pump();
            Assert.Contains("verkauften Spiel", string.Join("|", w.GetLogicalTexts()));
            Assert.Contains(w.Core.Audit.ReadAll(), a => a.Action == "spiele.asset.verwendet");
            d.Undo(); Assert.Single(d.List("assets")); // Rückgängig nimmt das Asset wieder heraus
        }
        finally { w.BibHttpOverride = null; w.DownloadsOverride = null; w.Close(); }
    }
}
