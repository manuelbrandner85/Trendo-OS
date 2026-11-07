using Trendo.Core.Media;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>Paket 01 – Studio 4, S4-01 (07.11.2026): Lautstärke-Automation je Spur.</summary>
public class AutomationTests
{
    static double Rms(AudioBuf a, double from, double to)
    {
        int s = (int)(from * a.Rate), e = Math.Min(a.Length, (int)(to * a.Rate)); double sum = 0;
        for (var i = s; i < e; i++) sum += a.Data[0][i] * (double)a.Data[0][i];
        return Math.Sqrt(sum / Math.Max(1, e - s));
    }

    [Fact(DisplayName = "S4-01: Automation 0 dB → −12 dB → 0 dB senkt die Musik genau dort (gemessen), auch mit Bussen; ungeordnete oder zu laute Punkte werden abgelehnt")]
    public void Automation()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trendo-auto-" + Guid.NewGuid().ToString("N")[..6]); Directory.CreateDirectory(dir);
        try
        {
            var x = Enumerable.Range(0, 44100 * 6).Select(i => (float)(0.3 * Math.Sin(2 * Math.PI * 220 * i / 44100.0))).ToArray();
            var f = Path.Combine(dir, "musik.wav"); AudioFile.WriteWav(new AudioBuf(44100, [x, (float[])x.Clone()]), f);
            var doc = AudioMulti.New(); var t = AudioMulti.AddTrack(doc, f);
            var before = AudioMulti.Mix(doc);
            AudioMulti.SetAutomation(doc, t.Id, AudioMulti.ParseAutomation("0:0, 1:0, 2:-12, 4:-12, 5:0"));
            var after = AudioMulti.Mix(doc);
            Assert.Equal(1, Rms(after, 0.2, 0.8) / Rms(before, 0.2, 0.8), 2);                   // vorne unverändert
            Assert.Equal(Math.Pow(10, -12 / 20.0), Rms(after, 2.2, 3.8) / Rms(before, 2.2, 3.8), 2); // −12 dB
            Assert.Equal(1, Rms(after, 5.2, 5.8) / Rms(before, 5.2, 5.8), 2);                   // wieder oben
            Assert.Equal(Math.Pow(10, -6 / 20.0), AudioMulti.AutoGain(AudioMulti.Automation(doc, t.Id), 1.5), 4); // dazwischen linear in dB
            MixRouting.Route(doc, t.Id, MixRouting.AddBus(doc, "Musik").Id);
            Assert.Equal(Math.Pow(10, -12 / 20.0), Rms(AudioMulti.Mix(doc), 2.2, 3.8) / Rms(before, 2.2, 3.8), 2); // gilt auch über den Bus
            var json = doc.ToJsonString();
            Assert.Throws<ArgumentException>(() => AudioMulti.SetAutomation(doc, t.Id, [(2, 0), (1, 0)]));
            Assert.Throws<ArgumentException>(() => AudioMulti.SetAutomation(doc, t.Id, [(0, 30)]));
            Assert.Throws<ArgumentException>(() => AudioMulti.ParseAutomation("zwei Sekunden leiser"));
            Assert.Equal(json, doc.ToJsonString()); // nichts verändert
            AudioMulti.SetAutomation(doc, t.Id, []); Assert.Empty(AudioMulti.Automation(doc, t.Id));
        }
        finally { Directory.Delete(dir, true); }
    }
}
