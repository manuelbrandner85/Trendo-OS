"""Trendo Musik – Song-Analyse (M5, freigegeben 03.11.2026): Tonart, Tempo (BPM), Takt, Akkordfolge und Tonumfang der Melodie.
Lokal mit librosa (ISC). Aufruf: python trendo_analyse.py [ordner] <audiodatei> <ausgabe.json>  (Trendo übergibt vorn den Programmordner)
Grenzen (ehrlich): Takt und Akkorde sind Schätzungen aus dem Klang; der Tonumfang ist der der lautesten Melodielinie (meist der Gesang).
S1 (03.11.2026): mit „--stimme“ wird eine Mikrofon-Aufnahme der eigenen Stimme untersucht – Umfang, bequemer Bereich, Stimmlage,
Klangfarbe und Hauchanteil (Schätzungen) und passende Tonarten. Mit Prüftönen gemessen: Bariton A2–D4 → „Bariton“, Sopran D4–G5 → „Sopran“.
"""
import json
import sys

import numpy as np
import librosa

NOTES = ['C', 'C#', 'D', 'Eb', 'E', 'F', 'F#', 'G', 'Ab', 'A', 'Bb', 'B']
# Krumhansl-Kessler-Profile für Dur und Moll
MAJOR = np.array([6.35, 2.23, 3.48, 2.33, 4.38, 4.09, 2.52, 5.19, 2.39, 3.66, 2.29, 2.88])
MINOR = np.array([6.33, 2.68, 3.52, 5.38, 2.60, 3.53, 2.54, 4.75, 3.98, 2.69, 3.34, 3.17])


def key_of(chroma):
    prof = chroma.mean(axis=1)
    best = (-2.0, 0, 'major')
    for i in range(12):
        for name, p in (('major', MAJOR), ('minor', MINOR)):
            r = np.corrcoef(prof, np.roll(p, i))[0, 1]
            if r > best[0]:
                best = (r, i, name)
    return f'{NOTES[best[1]]} {best[2]}', float(best[0])


def chords_of(chroma, beat_frames, beats_per_bar):
    # Dreiklänge: Dur (0,4,7) und Moll (0,3,7) je Grundton; je Schlag der passendste, gleiche hintereinander zusammengefasst
    templates = []
    for i in range(12):
        for name, iv in (('', (0, 4, 7)), ('m', (0, 3, 7))):
            t = np.zeros(12); t[[(i + x) % 12 for x in iv]] = 1
            templates.append((NOTES[i] + name, t / np.linalg.norm(t)))
    # je Takt zusammenfassen – je Schlag schwankt die Erkennung zu stark (gemessen 03.11.2026)
    bars = beat_frames[::max(1, beats_per_bar)]
    sync = librosa.util.sync(chroma, bars, aggregate=np.median)
    seq = []
    for k in range(sync.shape[1]):
        v = sync[:, k]
        if np.linalg.norm(v) < 1e-6:
            continue
        v = v / np.linalg.norm(v)
        name, score = max(((n, float(t @ v)) for n, t in templates), key=lambda x: x[1])
        if not seq or seq[-1] != name:
            seq.append(name)
    return seq[:64]


def meter_of(onset_env, beats):
    # 3er- oder 4er-Takt: welche Gruppierung der Schlagstärken schwankt stärker (betonte Eins)?
    if len(beats) < 16:
        return '4/4', 0.0
    s = onset_env[beats]
    def contrast(n):
        m = len(s) // n * n
        g = s[:m].reshape(-1, n)
        return float(g[:, 0].mean() / (g[:, 1:].mean() + 1e-9))
    c3, c4 = contrast(3), contrast(4)
    return ('3/4', c3) if c3 > c4 * 1.08 else ('4/4', c4)


def main(path, out):
    y, sr = librosa.load(path, sr=22050, mono=True)
    dur = float(len(y) / sr)
    harm, perc = librosa.effects.hpss(y)
    hop = 512
    onset = librosa.onset.onset_strength(y=perc, sr=sr, hop_length=hop)
    tempo, beats = librosa.beat.beat_track(onset_envelope=onset, sr=sr, hop_length=hop)
    bpm = float(np.atleast_1d(tempo)[0])
    # Doppel-/Halbzählung des Taktgebers ausgleichen (gemessen: 90 BPM wurde als 184,6 erkannt)
    while bpm > 170: bpm /= 2
    while bpm < 60: bpm *= 2
    chroma = librosa.feature.chroma_cqt(y=harm, sr=sr, hop_length=hop)
    key, conf = key_of(chroma)
    meter, _ = meter_of(onset, beats)
    chords = chords_of(chroma, beats, 3 if meter == '3/4' else 4)
    # Tonumfang der Melodie (Gesang): pyin auf dem harmonischen Anteil, nur sichere Töne, 5.–95. Perzentil gegen Ausreißer
    f0, voiced, prob = librosa.pyin(harm, fmin=librosa.note_to_hz('C2'), fmax=librosa.note_to_hz('C6'), sr=sr, hop_length=hop)
    good = f0[(voiced) & (prob > 0.6)] if f0 is not None else np.array([])
    rng = None
    if good.size > 20:
        lo, hi = np.percentile(good, 5), np.percentile(good, 95)
        lo_n, hi_n = librosa.hz_to_note(lo), librosa.hz_to_note(hi)
        semis = int(round(librosa.hz_to_midi(hi) - librosa.hz_to_midi(lo)))
        mid = float(librosa.hz_to_midi(np.median(good)))
        voice = 'Bass' if mid < 50 else 'Bariton' if mid < 55 else 'Tenor' if mid < 60 else 'Alt' if mid < 65 else 'Sopran'
        rng = {'low': lo_n.replace('♯', '#'), 'high': hi_n.replace('♯', '#'), 'semitones': semis, 'voice': voice}
    json.dump({'duration': round(dur, 1), 'bpm': round(bpm, 1), 'key': key, 'key_confidence': round(conf, 2), 'meter': meter,
               'chords': chords, 'range': rng}, open(out, 'w', encoding='utf-8'), ensure_ascii=False)


# ───────────── S1: eigene Stimme ─────────────
def note(m):
    m = int(round(m))
    return NOTES[m % 12] + str(m // 12 - 1)


def voice_main(path, out):
    y, sr = librosa.load(path, sr=22050, mono=True)
    hop = 512
    f0, voiced, prob = librosa.pyin(y, fmin=librosa.note_to_hz('C2'), fmax=librosa.note_to_hz('C6'), sr=sr, hop_length=hop)
    ok = (voiced) & (prob > 0.5) & np.isfinite(f0)
    if ok.sum() < 40:
        json.dump({'stimme': None, 'grund': 'zu wenig gesungen – bitte lauter oder länger singen'}, open(out, 'w', encoding='utf-8'), ensure_ascii=False)
        return
    midi = librosa.hz_to_midi(f0[ok])
    lo, hi = np.percentile(midi, 2), np.percentile(midi, 98)
    clo, chi = np.percentile(midi, 15), np.percentile(midi, 85)  # bequemer Bereich
    mid = float(np.median(midi))
    voice = 'Bass' if mid < 50 else 'Bariton' if mid < 55 else 'Tenor' if mid < 60 else 'Alt' if mid < 65 else 'Mezzosopran' if mid < 69 else 'Sopran'
    # Klangfarbe aus den gesungenen Stellen: Schwerpunkt des Spektrums (hell/dunkel) und Anteil Rauschen (gehaucht/klar)
    S = np.abs(librosa.stft(y, hop_length=hop))
    n = min(S.shape[1], len(ok))
    Sv = S[:, :n][:, ok[:n]]
    cent = float(np.median(librosa.feature.spectral_centroid(S=Sv, sr=sr)) / (librosa.midi_to_hz(mid)))
    # Hauchanteil: spektrale Flachheit der gesungenen Stellen (gemessen 03.11.2026: klare Stimme 0,0002; Rauschzusatz 3/6/12 % → 0,002/0,007/0,025)
    flat = float(np.median(librosa.feature.spectral_flatness(S=Sv)))
    color = 'hell' if cent > 14 else 'warm' if cent > 7 else 'dunkel'
    breath = 'gehaucht' if flat > 0.02 else 'leicht gehaucht' if flat > 0.006 else 'klar'
    # passende Tonarten: eine typische Melodie kreist um die Terz über dem Grundton – sie soll in der Mitte des bequemen Bereichs liegen
    center = (clo + chi) / 2
    keys = sorted(((min(abs(12 * o + t + 4 - center) for o in range(2, 8)), NOTES[t]) for t in range(12)))
    rec = [k for d, k in keys[:3]]
    tags = {'Bass': 'deep bass male vocals', 'Bariton': 'baritone male vocals', 'Tenor': 'tenor male vocals', 'Alt': 'alto female vocals',
            'Mezzosopran': 'mezzo-soprano female vocals', 'Sopran': 'soprano female vocals'}[voice]
    tags += ', ' + {'hell': 'bright voice', 'warm': 'warm voice', 'dunkel': 'dark voice'}[color] + ', ' + {'gehaucht': 'breathy', 'leicht gehaucht': 'soft airy', 'klar': 'clear'}[breath]
    res = {'low': note(lo), 'high': note(hi), 'comfy_low': note(clo), 'comfy_high': note(chi), 'semitones': int(round(hi - lo)), 'voice': voice,
           'color': color, 'breath': breath, 'keys': rec, 'tags': tags, 'seconds': round(float(ok.sum()) * hop / sr, 1)}
    json.dump({'stimme': res}, open(out, 'w', encoding='utf-8'), ensure_ascii=False)


# ───────────── S5: Zielmelodie zum Mitsingen ─────────────
def melody_main(path, out, stem):
    # Gesangsspur (aus „Spuren trennen“) direkt, sonst der harmonische Anteil des ganzen Lieds; 50 ms je Wert
    sr, hop = 16000, 800
    y, _ = librosa.load(path, sr=sr, mono=True)
    if not stem:
        y, _ = librosa.effects.hpss(y)
    f0, voiced, prob = librosa.pyin(y, fmin=librosa.note_to_hz('C2'), fmax=librosa.note_to_hz('C6'), sr=sr, hop_length=hop, frame_length=2048)
    midi = [round(float(librosa.hz_to_midi(f)), 2) if v and p > 0.5 and np.isfinite(f) else None for f, v, p in zip(f0, voiced, prob)]
    json.dump({'hop': hop / sr, 'midi': midi}, open(out, 'w', encoding='utf-8'))


# ───────────── V1: Abschnitte und Takt fürs Musikvideo ─────────────
def sections_main(path, out):
    # Abschnittsgrenzen: Schläge, je Schlag Klangfarbe (MFCC) und Harmonie (Chroma), Wiederholungsstruktur (Recurrence) und
    # Lautstärke; Grenzen nur auf Taktanfängen, mindestens 8 s Abstand. Dazu Energie je Abschnitt (für ruhige/kraftvolle Bilder).
    y, sr = librosa.load(path, sr=22050, mono=True)
    hop = 512
    tempo, beats = librosa.beat.beat_track(y=y, sr=sr, hop_length=hop)
    bpm = float(np.atleast_1d(tempo)[0])
    while bpm > 170: bpm /= 2
    while bpm < 60: bpm *= 2
    dur = len(y) / sr
    mfcc = librosa.feature.mfcc(y=y, sr=sr, hop_length=hop, n_mfcc=13)
    chroma = librosa.feature.chroma_cqt(y=y, sr=sr, hop_length=hop)
    feat = np.vstack([librosa.util.normalize(mfcc, axis=1), chroma])
    sync = librosa.util.sync(feat, beats, aggregate=np.median)
    k = int(np.clip(round(dur / 22), 3, 12))
    bounds = librosa.segment.agglomerative(sync, k)
    times = librosa.frames_to_time(librosa.util.fix_frames(beats, x_min=0)[bounds], sr=sr, hop_length=hop) if len(beats) else np.array([0.0])
    bar = 4 * 60 / bpm
    cuts = [0.0]
    for t in sorted(set(round(float(x / bar)) * bar for x in times)):
        if t - cuts[-1] >= 8 and dur - t >= 6:
            cuts.append(float(t))
    rms = librosa.feature.rms(y=y, hop_length=hop)[0]
    rt = librosa.frames_to_time(np.arange(len(rms)), sr=sr, hop_length=hop)
    allm = float(np.mean(rms)) + 1e-9
    secs = []
    for i, a in enumerate(cuts):
        b = cuts[i + 1] if i + 1 < len(cuts) else dur
        e = float(np.mean(rms[(rt >= a) & (rt < b)])) / allm if b > a else 1.0
        secs.append({'start': round(a, 2), 'end': round(b, 2), 'energy': round(e, 2)})
    json.dump({'bpm': round(bpm, 1), 'duration': round(dur, 2), 'sections': secs}, open(out, 'w', encoding='utf-8'))


if __name__ == '__main__':
    if '--abschnitte' in sys.argv:
        sections_main(sys.argv[-2], sys.argv[-1])
    elif '--melodie' in sys.argv:
        melody_main(sys.argv[-2], sys.argv[-1], '--gesangsspur' in sys.argv)
    elif '--stimme' in sys.argv:
        voice_main(sys.argv[-2], sys.argv[-1])
    else:
        main(sys.argv[-2], sys.argv[-1])
