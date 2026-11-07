using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Paket 01 – Studio 4, S4-09 (07.11.2026): Busse, Sends, Sidechain, Routing-Kreis.</summary>
public class MixRoutingTests
{
    static string Tone(string dir, string name, double hz, double seconds, double amp = 0.3, double from = 0, double to = 99)
    {
        const int rate = 44100; var n = (int)(seconds * rate);
        var d = Enumerable.Range(0, n).Select(i => { var t = i / (double)rate; return (float)(t >= from && t < to ? amp * Math.Sin(2 * Math.PI * hz * t) : 0); }).ToArray();
        var f = Path.Combine(dir, name); AudioFile.WriteWav(new AudioBuf(rate, [d, (float[])d.Clone()]), f); return f;
    }

    static double Rms(AudioBuf a, double from, double to)
    {
        int s = (int)(from * a.Rate), e = Math.Min(a.Length, (int)(to * a.Rate)); double sum = 0;
        for (var i = s; i < e; i++) sum += a.Data[0][i] * (double)a.Data[0][i];
        return Math.Sqrt(sum / Math.Max(1, e - s));
    }

    [Fact(DisplayName = "S4-09-P: Bus mit EQ verändert den Klang messbar, Send mit Hall verlängert den Ausklang, Sidechain-Ducking senkt die Musik nur während der Stimme")]
    public void Buses_sends_sidechain()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-route-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            var doc = AudioMulti.New();
            var music = AudioMulti.AddTrack(doc, Tone(dir, "musik.wav", 110, 4), 0, "Musik"); // tiefer Ton
            var voice = AudioMulti.AddTrack(doc, Tone(dir, "stimme.wav", 800, 4, 0.3, 1, 2), 0, "Stimme"); // spricht von 1 bis 2 s
            var plain = AudioMulti.Mix(doc);
            var bus = MixRouting.AddBus(doc, "Musik-Bus"); MixRouting.Route(doc, music.Id, bus.Id);
            MixRouting.SetBus(doc, bus.Id, low: -12);
            var eq = AudioMulti.Mix(doc);
            Assert.True(Rms(eq, 2.5, 3.5) < Rms(plain, 2.5, 3.5) * 0.5, $"EQ wirkt nicht: {Rms(eq, 2.5, 3.5)} vs {Rms(plain, 2.5, 3.5)}");
            // Sidechain: Stimme drückt den Musik-Bus
            MixRouting.SetBus(doc, bus.Id, low: 0, threshold: -30, ratio: 8, sidechain: voice.Id);
            var duck = AudioMulti.Mix(doc);
            var musicOnlyBefore = Rms(duck, 0.3, 0.9); var musicDuring = Rms(MixRouting.Mix(WithMuted(doc, voice.Id)), 1.3, 1.9);
            Assert.True(musicDuring < musicOnlyBefore * 0.6, $"Ducking fehlt: {musicDuring} vs {musicOnlyBefore}");
            // Send der Stimme auf einen Hall-Bus: Ausklang nach 2 s wird hörbar
            var hall = MixRouting.AddBus(doc, "Hall"); MixRouting.SetBus(doc, hall.Id, reverb: 0.8);
            MixRouting.SetSend(doc, voice.Id, hall.Id, 0.7);
            MixRouting.SetBus(doc, bus.Id, ratio: 1, sidechain: ""); MixRouting.Route(doc, music.Id, MixRouting.Master);
            var noMusic = WithMuted(doc, music.Id);
            Assert.True(Rms(MixRouting.Mix(noMusic), 2.02, 2.3) > 0.01, "kein Hall-Ausklang");
            MixRouting.SetSend(doc, voice.Id, hall.Id, 0);
            Assert.True(Rms(MixRouting.Mix(WithMuted(doc, music.Id)), 2.05, 2.3) < 0.002, "ohne Send kein Ausklang");
        }
        finally { Directory.Delete(dir, true); }
    }

    static System.Text.Json.Nodes.JsonObject WithMuted(System.Text.Json.Nodes.JsonObject doc, string trackId)
    {
        var c = (System.Text.Json.Nodes.JsonObject)doc.DeepClone(); AudioMulti.SetTrack(c, trackId, mute: true); return c;
    }

    [Fact(DisplayName = "S4-09-N: Routing-Kreis (Bus A → B → A) wird erkannt und abgewiesen, das Projekt bleibt unverändert; doppelter Weg und unbekannte Ziele abgelehnt")]
    public void Cycle_rejected()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-route-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            var doc = AudioMulti.New(); var t = AudioMulti.AddTrack(doc, Tone(dir, "a.wav", 440, 1), 0);
            var a = MixRouting.AddBus(doc, "A"); var b = MixRouting.AddBus(doc, "B"); var c = MixRouting.AddBus(doc, "C");
            MixRouting.Route(doc, a.Id, b.Id); MixRouting.Route(doc, b.Id, c.Id);
            var before = doc.ToJsonString();
            Assert.Contains("Routing-Kreis", Assert.Throws<InvalidOperationException>(() => MixRouting.Route(doc, c.Id, a.Id)).Message);
            Assert.Throws<InvalidOperationException>(() => MixRouting.Route(doc, a.Id, a.Id));
            Assert.Equal(before, doc.ToJsonString());
            Assert.Equal([c.Id, b.Id, a.Id], MixRouting.Order(doc).Select(x => x.Id).Reverse());
            MixRouting.Route(doc, t.Id, a.Id);
            Assert.Throws<InvalidOperationException>(() => MixRouting.SetSend(doc, t.Id, a.Id, 0.5)); // schon direkt drauf
            Assert.Throws<ArgumentException>(() => MixRouting.Route(doc, t.Id, "gibt-es-nicht"));
            Assert.Throws<ArgumentException>(() => MixRouting.SetBus(doc, a.Id, sidechain: "fremd"));
            Assert.True(AudioMulti.Mix(doc).Length > 0); // A → B → C → Summe läuft
        }
        finally { Directory.Delete(dir, true); }
    }
}
