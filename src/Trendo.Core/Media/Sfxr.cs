namespace Trendo.Core.Media;

/// <summary>Alle Studios, Runde 7 – CORE-06 „Spiele bauen“: Retro-Soundeffekte nach dem sfxr-Verfahren (DrPetter 2007, gemeinfrei;
/// Vorbild jsfxr, Unlicense). Eigener C#-Code ohne Samples – jeder Effekt ist selbst erzeugt und frei verwendbar.
/// Ein Effekt = Wellenform + Hüllkurve + Tonhöhenverlauf + Filter; Vorlagen (Münze, Laser, Explosion …) würfeln passende Regler.</summary>
public static class Sfxr
{
    public const int Rate = 44100;
    public const string License = "selbst erzeugt (Trendo, sfxr-Verfahren gemeinfrei) – frei verwendbar";

    public enum Wave { Rechteck, Saegezahn, Sinus, Rauschen }

    /// <summary>Regler wie im Original (0…1, Rampen −1…1). Standardwerte = neutraler Ton.</summary>
    public sealed record Params
    {
        public Wave WaveType { get; init; } = Wave.Rechteck;
        public double Attack { get; init; }
        public double Sustain { get; init; } = 0.3;
        public double Punch { get; init; }
        public double Decay { get; init; } = 0.4;
        public double BaseFreq { get; init; } = 0.3;
        public double FreqLimit { get; init; }
        public double FreqRamp { get; init; }
        public double FreqDeltaRamp { get; init; }
        public double VibStrength { get; init; }
        public double VibSpeed { get; init; }
        public double ArpMod { get; init; }
        public double ArpSpeed { get; init; }
        public double Duty { get; init; }
        public double DutyRamp { get; init; }
        public double RepeatSpeed { get; init; }
        public double PhaOffset { get; init; }
        public double PhaRamp { get; init; }
        public double LpfFreq { get; init; } = 1;
        public double LpfRamp { get; init; }
        public double LpfResonance { get; init; }
        public double HpfFreq { get; init; }
        public double HpfRamp { get; init; }
        public double Volume { get; init; } = 0.5;
    }

    /// <summary>Vorlagen mit Ereignis-Tag für Spiele (passt zur Geräusch-Bibliothek).</summary>
    public static readonly (string Id, string Label, string Event)[] Presets =
    [
        ("muenze", "Münze / Einsammeln", "Spiel.Muenze"), ("laser", "Laser / Schuss", "Spiel.Schuss"), ("explosion", "Explosion", "Spiel.Explosion"),
        ("powerup", "Power-up", "Spiel.PowerUp"), ("treffer", "Treffer / Schaden", "Spiel.Treffer"), ("sprung", "Sprung", "Spiel.Sprung"), ("auswahl", "Menü-Klick", "Spiel.Menue"),
    ];

    /// <summary>Regler für eine Vorlage würfeln (gleicher Startwert → gleicher Effekt).</summary>
    public static Params Preset(string id, int seed)
    {
        var rng = new Random(seed);
        double R(double x) => rng.NextDouble() * x;           // frnd
        bool Coin() => rng.Next(2) == 1;                        // rnd(1)
        int Rn(int n) => rng.Next(n + 1);                       // rnd(n), 0…n
        var p = new Params();
        switch (id)
        {
            case "muenze":
                p = p with { BaseFreq = 0.4 + R(0.5), Sustain = R(0.1), Decay = 0.1 + R(0.4), Punch = 0.3 + R(0.3) };
                if (Coin()) p = p with { ArpSpeed = 0.5 + R(0.2), ArpMod = 0.2 + R(0.4) };
                break;
            case "laser":
            {
                var w = Rn(2); if (w == 2 && Coin()) w = Rn(1);
                p = p with { WaveType = (Wave)w, BaseFreq = 0.5 + R(0.5) };
                p = p with { FreqLimit = Math.Max(0.2, p.BaseFreq - 0.2 - R(0.6)), FreqRamp = -0.15 - R(0.2) };
                if (Rn(2) == 0) p = p with { BaseFreq = 0.3 + R(0.6), FreqLimit = R(0.1), FreqRamp = -0.35 - R(0.3) };
                p = Coin() ? p with { Duty = R(0.5), DutyRamp = R(0.2) } : p with { Duty = 0.4 + R(0.5), DutyRamp = -R(0.7) };
                p = p with { Sustain = 0.1 + R(0.2), Decay = R(0.4) };
                if (Coin()) p = p with { Punch = R(0.3) };
                if (Rn(2) == 0) p = p with { PhaOffset = R(0.2), PhaRamp = -R(0.2) };
                if (Coin()) p = p with { HpfFreq = R(0.3) };
                break;
            }
            case "explosion":
                p = p with { WaveType = Wave.Rauschen };
                p = Coin() ? p with { BaseFreq = 0.1 + R(0.4), FreqRamp = -0.1 + R(0.4) } : p with { BaseFreq = 0.2 + R(0.7), FreqRamp = -0.2 - R(0.2) };
                p = p with { BaseFreq = p.BaseFreq * p.BaseFreq };
                if (Rn(4) == 0) p = p with { FreqRamp = 0 };
                if (Rn(2) == 0) p = p with { RepeatSpeed = 0.3 + R(0.5) };
                p = p with { Sustain = 0.1 + R(0.3), Decay = R(0.5) };
                if (!Coin()) p = p with { PhaOffset = -0.3 + R(0.9), PhaRamp = -R(0.3) };
                p = p with { Punch = 0.2 + R(0.6) };
                if (Coin()) p = p with { VibStrength = R(0.7), VibSpeed = R(0.6) };
                if (Rn(2) == 0) p = p with { ArpSpeed = 0.6 + R(0.3), ArpMod = 0.8 - R(1.6) };
                break;
            case "powerup":
                p = Coin() ? p with { WaveType = Wave.Saegezahn } : p with { Duty = R(0.6) };
                p = Coin() ? p with { BaseFreq = 0.2 + R(0.3), FreqRamp = 0.1 + R(0.4), RepeatSpeed = 0.4 + R(0.4) } : p with { BaseFreq = 0.2 + R(0.3), FreqRamp = 0.05 + R(0.2) };
                if (Coin()) p = p with { VibStrength = R(0.7), VibSpeed = R(0.6) };
                p = p with { Sustain = R(0.4), Decay = 0.1 + R(0.4) };
                break;
            case "treffer":
            {
                var w = Rn(2); if (w == 2) w = 3;
                p = p with { WaveType = (Wave)w };
                if (w == 0) p = p with { Duty = R(0.6) };
                p = p with { BaseFreq = 0.2 + R(0.6), FreqRamp = -0.3 - R(0.4), Sustain = R(0.1), Decay = 0.1 + R(0.2) };
                if (Coin()) p = p with { HpfFreq = R(0.3) };
                break;
            }
            case "sprung":
                p = p with { WaveType = Wave.Rechteck, Duty = R(0.6), BaseFreq = 0.3 + R(0.3), FreqRamp = 0.1 + R(0.2), Sustain = 0.1 + R(0.3), Decay = 0.1 + R(0.2) };
                if (Coin()) p = p with { HpfFreq = R(0.3) };
                if (Coin()) p = p with { LpfFreq = 1 - R(0.6) };
                break;
            case "auswahl":
                p = p with { WaveType = (Wave)Rn(1) };
                if (p.WaveType == Wave.Rechteck) p = p with { Duty = R(0.6) };
                p = p with { BaseFreq = 0.2 + R(0.4), Sustain = 0.1 + R(0.1), Decay = R(0.2), HpfFreq = 0.1 };
                break;
            default: throw new ArgumentException("Unbekannte Vorlage: " + id);
        }
        return p;
    }

    /// <summary>Effekt berechnen (Mono, 44,1 kHz; höchstens <paramref name="maxSeconds"/>). Ergebnis als Stereo-Puffer für den Audioeditor.</summary>
    public static AudioBuf Render(Params p, double maxSeconds = 5)
    {
        var s = new Synth(p, new Random(1234));
        var max = (int)(Math.Clamp(maxSeconds, 0.05, 10) * Rate);
        var buf = new List<float>(Rate);
        while (s.Playing && buf.Count < max) buf.Add(s.Next());
        // kurzes Ausblenden gegen Knacken
        var fade = Math.Min(buf.Count, 64); for (var i = 0; i < fade; i++) buf[buf.Count - 1 - i] *= i / (float)fade;
        var mono = buf.ToArray();
        var peak = mono.Length == 0 ? 0 : mono.Max(v => Math.Abs(v));
        if (peak > 1e-6) { var g = 0.708f / peak; for (var i = 0; i < mono.Length; i++) mono[i] *= g; } // −3 dBFS wie die Geräusch-Werkstatt
        return new AudioBuf(Rate, [mono, (float[])mono.Clone()]);
    }

    sealed class Synth
    {
        readonly Params p; readonly Random rng;
        public bool Playing = true;
        int phase, period, envStage, envTime, arpTime, arpLimit, repTime, repLimit, iphase, ipp;
        double fperiod, fmaxperiod, fslide, fdslide, squareDuty, squareSlide, envVol, arpMod;
        double fphase, fdphase, fltp, fltdp, fltw, fltwD, fltdmp, fltphp, flthp, flthpD, vibPhase, vibSpeed, vibAmp;
        readonly int[] envLength = new int[3];
        readonly double[] phaser = new double[1024], noise = new double[32];

        public Synth(Params p, Random rng) { this.p = p; this.rng = rng; Reset(false); }

        static double Sq(double x) => x * x;
        double Noise() => rng.NextDouble() * 2 - 1;

        void Reset(bool restart)
        {
            if (!restart) phase = 0;
            fperiod = 100.0 / (Sq(p.BaseFreq) + 0.001); period = (int)fperiod;
            fmaxperiod = 100.0 / (Sq(p.FreqLimit) + 0.001);
            fslide = 1.0 - Math.Pow(p.FreqRamp, 3) * 0.01; fdslide = -Math.Pow(p.FreqDeltaRamp, 3) * 0.000001;
            squareDuty = 0.5 - p.Duty * 0.5; squareSlide = -p.DutyRamp * 0.00005;
            arpMod = p.ArpMod >= 0 ? 1.0 - Sq(p.ArpMod) * 0.9 : 1.0 + Sq(p.ArpMod) * 10.0;
            arpTime = 0; arpLimit = p.ArpSpeed >= 1 ? 0 : (int)(Sq(1.0 - p.ArpSpeed) * 20000 + 32);
            if (restart) return;
            fltp = 0; fltdp = 0; fltw = Math.Pow(p.LpfFreq, 3) * 0.1; fltwD = 1 + p.LpfRamp * 0.0001;
            fltdmp = Math.Min(0.8, 5.0 / (1.0 + Sq(p.LpfResonance) * 20) * (0.01 + fltw));
            fltphp = 0; flthp = Sq(p.HpfFreq) * 0.1; flthpD = 1 + p.HpfRamp * 0.0003;
            vibPhase = 0; vibSpeed = Sq(p.VibSpeed) * 0.01; vibAmp = p.VibStrength * 0.5;
            envVol = 0; envStage = 0; envTime = 0;
            envLength[0] = (int)(Sq(p.Attack) * 100000); envLength[1] = (int)(Sq(p.Sustain) * 100000); envLength[2] = (int)(Sq(p.Decay) * 100000);
            fphase = Sq(p.PhaOffset) * 1020 * Math.Sign(p.PhaOffset); fdphase = Sq(p.PhaRamp) * Math.Sign(p.PhaRamp);
            iphase = Math.Abs((int)fphase); ipp = 0; Array.Clear(phaser);
            for (var i = 0; i < 32; i++) noise[i] = Noise();
            repTime = 0; repLimit = p.RepeatSpeed <= 0 ? 0 : (int)(Sq(1.0 - p.RepeatSpeed) * 20000 + 32);
        }

        public float Next()
        {
            if (repLimit != 0 && ++repTime >= repLimit) { repTime = 0; Reset(true); }
            if (arpLimit != 0 && ++arpTime >= arpLimit) { arpLimit = 0; fperiod *= arpMod; }
            fslide += fdslide; fperiod *= fslide;
            if (fperiod > fmaxperiod) { fperiod = fmaxperiod; if (p.FreqLimit > 0) Playing = false; }
            var rfperiod = fperiod;
            if (vibAmp > 0) { vibPhase += vibSpeed; rfperiod = fperiod * (1.0 + Math.Sin(vibPhase) * vibAmp); }
            period = Math.Max(8, (int)rfperiod);
            squareDuty = Math.Clamp(squareDuty + squareSlide, 0, 0.5);
            if (++envTime > envLength[envStage]) { envTime = 0; if (++envStage == 3) { Playing = false; envStage = 2; } }
            envVol = envStage switch
            {
                0 => envLength[0] == 0 ? 1 : envTime / (double)envLength[0],
                1 => 1.0 + (envLength[1] == 0 ? 0 : (1.0 - envTime / (double)envLength[1])) * 2.0 * p.Punch,
                _ => envLength[2] == 0 ? 0 : 1.0 - envTime / (double)envLength[2],
            };
            fphase += fdphase; iphase = Math.Min(1023, Math.Abs((int)fphase));
            if (flthpD != 0) flthp = Math.Clamp(flthp * flthpD, 0.00001, 0.1);
            double ssample = 0;
            for (var si = 0; si < 8; si++) // 8-fach überabgetastet
            {
                if (++phase >= period) { phase %= period; if (p.WaveType == Wave.Rauschen) for (var i = 0; i < 32; i++) noise[i] = Noise(); }
                var fp = phase / (double)period;
                var sample = p.WaveType switch
                {
                    Wave.Rechteck => fp < squareDuty ? 0.5 : -0.5,
                    Wave.Saegezahn => 1.0 - fp * 2,
                    Wave.Sinus => Math.Sin(fp * 2 * Math.PI),
                    _ => noise[Math.Min(31, phase * 32 / period)],
                };
                var pp = fltp;
                fltw = Math.Clamp(fltw * fltwD, 0, 0.1);
                if (p.LpfFreq < 1) { fltdp += (sample - fltp) * fltw; fltdp -= fltdp * fltdmp; } else { fltp = sample; fltdp = 0; }
                fltp += fltdp;
                fltphp += fltp - pp; fltphp -= fltphp * flthp; sample = fltphp;
                phaser[ipp & 1023] = sample; sample += phaser[(ipp - iphase + 1024) & 1023]; ipp = (ipp + 1) & 1023;
                ssample += sample * envVol;
            }
            return (float)(ssample / 8 * 2.0 * p.Volume); // Pegel wird in Render auf −3 dBFS Spitze gebracht
        }
    }
}
