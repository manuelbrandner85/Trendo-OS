"""Trendo Musik – Song-Analyse (M5, freigegeben 03.11.2026): Tonart, Tempo (BPM), Takt, Akkordfolge und Tonumfang der Melodie.
Lokal mit librosa (ISC). Aufruf: python trendo_analyse.py [ordner] <audiodatei> <ausgabe.json>  (Trendo übergibt vorn den Programmordner)
Grenzen (ehrlich): Takt und Akkorde sind Schätzungen aus dem Klang; der Tonumfang ist der der lautesten Melodielinie (meist der Gesang).
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


if __name__ == '__main__':
    main(sys.argv[-2], sys.argv[-1])
