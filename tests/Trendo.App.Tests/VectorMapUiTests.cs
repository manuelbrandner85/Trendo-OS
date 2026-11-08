using System.Buffers.Binary;
using System.IO.Compression;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.Core.Karten;
using Xunit;

namespace Trendo.App.Tests;

/// <summary>Alle Studios, Runde 1 – Karten-Studio: Vektor-PMTiles öffnet die MapLibre-Ansicht; Brücke (bereit → Start, kachel → Antwort) ohne echtes WebView.</summary>
public class VectorMapUi
{
    static void Var(List<byte> o, ulong v) { while (v >= 0x80) { o.Add((byte)(v | 0x80)); v >>= 7; } o.Add((byte)v); }
    static byte[] Gz(byte[] b) { using var ms = new MemoryStream(); using (var g = new GZipStream(ms, CompressionLevel.Fastest)) g.Write(b); return ms.ToArray(); }
    static string Vector(string dir)
    {
        var t = Gz("MVT"u8.ToArray()); var d = new List<byte>(); Var(d, 1); Var(d, 0); Var(d, 1); Var(d, (ulong)t.Length); Var(d, 1); var root = Gz([.. d]);
        var h = new byte[127]; "PMTiles"u8.CopyTo(h); h[7] = 3; void U(int o, ulong v) => BinaryPrimitives.WriteUInt64LittleEndian(h.AsSpan(o), v);
        U(8, 127); U(16, (ulong)root.Length); U(24, (ulong)(127 + root.Length)); U(40, (ulong)(127 + root.Length)); U(56, (ulong)(127 + root.Length)); U(64, (ulong)t.Length); U(72, 1); U(80, 1); U(88, 1);
        h[96] = 1; h[97] = 2; h[98] = 2; h[99] = 1; BinaryPrimitives.WriteInt32LittleEndian(h.AsSpan(110), 100000000); BinaryPrimitives.WriteInt32LittleEndian(h.AsSpan(114), 500000000);
        var f = Path.Combine(dir, "stadt.pmtiles"); File.WriteAllBytes(f, [.. h, .. root, .. t]); return f;
    }

    [AvaloniaFact(DisplayName = "Vektorkarte-Oberfläche: .pmtiles mit MVT → MapLibre füllt die Arbeitsfläche statt der Rasterkarte; bereit → Start, Kachel → Antwort; Schließen")]
    public void Open_vector()
    {
        var (w, _, root) = Ui.Open(); Directory.CreateDirectory(root); var sent = new List<string>();
        try
        {
            w.VectorScriptOverride = sent.Add;
            w.MapLoadPack(Vector(root));
            Assert.NotNull(w.VectorMapOpen); Assert.StartsWith("✓ Vektorkarte „stadt“", w.VectorMapNote);
            w.ProgramNew("karten"); w.Show("karten");
            Assert.NotNull(w.Find<StackPanel>("mk-vector")); Assert.NotNull(w.Find<Button>("mk-vector-close"));
            Assert.NotNull(w.Find<Border>("mk-vector-area")); // Karte füllt die Arbeitsfläche
            w.VectorMapMessage("{\"art\":\"bereit\"}"); Assert.StartsWith("window.trendoKarte({\"name\":\"stadt\"", sent[^1]);
            w.VectorMapMessage("{\"art\":\"kachel\",\"id\":3,\"z\":0,\"x\":0,\"y\":0}"); Assert.Equal($"window.trendoKachel(3,\"{Convert.ToBase64String("MVT"u8.ToArray())}\")", sent[^1]);
            w.VectorMapMessage("{\"art\":\"loeschen\"}"); Assert.Equal(2, sent.Count);
            w.VectorMapMessage("{\"art\":\"geladen\"}"); Assert.Contains("angezeigt", w.VectorMapNote);
            w.Find<Button>("mk-vector-close").Press(); Assert.Null(w.VectorMapOpen); Assert.Equal("", w.MapPackPath);
        }
        finally { w.Close(); }
    }
}
