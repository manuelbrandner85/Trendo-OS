# Beleg S4-13 – Step-Sequencer mit Arpeggiator und Automation (06.11.2026)

- Werkzeug: Trendo-eigene Klangerzeugung (keine Samples), Prüfung mit ffprobe aus ffmpeg version 9.0.2-full_build-www.gyan.dev
- Lauf: `dotnet test tests/Trendo.Core.Tests -c Release --filter "FullyQualifiedName~StepSequencerTests|FullyQualifiedName~AudioQaTests"` mit `TRENDO_SEQ_BELEG=<temp>/trendo-seqbeleg` → Exitcode 0
- Ergebnis: Bestanden!   : Fehler:     0, erfolgreich:     6, übersprungen:     0, gesamt:     6, Dauer: 424 ms
- Oberfläche: `Trendo.App.Tests.exe -class Trendo.App.Tests.MusikBeat_Ui` → Total 1, Failed 0 (Raster klicken, 600 BPM abgelehnt ohne Speichern, 140 BPM gespeichert als Lied mit WAV 24 Bit und .beat.json)
- Muster: Vorlage EDM – 128 BPM, 4 Takte à 16 Schritte, Kick four-on-the-floor, Clap 2/4, Offbeat-Hats, Bass mit Lautstärke-Automation 40 % → 90 %, Arpeggio Am auf, 2 Oktaven

## Messung
```
ffprobe: pcm_s24le|48000|2 8.500000
BPM erkannt: 128,6
```

### Audio-Prüfung „edm-pattern“

Erstellt: 06.11.2026 23:42 · Ziel: Musik / Streaming (−14 LUFS)

**Freigabe möglich.**

#### Messwerte
| Wert | Ergebnis |
|---|---|
| Lautheit | -14,5 LUFS |
| True Peak | -0,8 dBTP |
| Sample Peak | -1,0 dBFS |
| Übersteuerungen | 0 |
| Format | 48000 Hz · 2 Kanäle · 24 Bit · 8,50 s |
| Phase (Korrelation) | 0,99 |
| Gleichspannung | 0,0008 |
| Stille Anfang/Ende | 0,0 s / 0,9 s |

#### Prüfung
- 🟢 Keine Übersteuerung.
- 🟡 True Peak -0,8 dBTP über -1 – beim Umwandeln (MP3/AAC) kann es verzerren.
- 🟢 Lautheit -14,5 LUFS passt zu „Musik / Streaming (−14 LUFS)“.
- 🟢 Format: 48000 Hz, 2 Kanale, 24 Bit, 8,5 s.
- 🟢 „Step-Sequencer“: selbst erzeugt (Trendo-Synthese).

#### Dateien
| Datei | Größe | SHA-256 |
|---|---|---|
| edm-pattern.wav | 2.448.044 Bytes | `c0af9729618e2c34dae3c0a2ad04881a77fe739f2542aaa181f86e6eae47cedb` |

## Negativfall S4-13-N
Muster über 10 Minuten (z. B. 60 BPM × 512 Takte) wird als mögliche Endlosschleife abgelehnt; Automation außerhalb des Musters, ungeordnet oder mit ungültigem Wert, falsches Raster, unbekannter Akkord und beschädigte .beat.json werden mit Grund abgelehnt (Test `Rejects`).

## Dateien (SHA-256)
```
c0af9729618e2c34dae3c0a2ad04881a77fe739f2542aaa181f86e6eae47cedb *edm-pattern.wav
b6ff568295ef3335106e70ba0751cbdeeb8d71ff909ddf6669a8742c1b4ba72d *edm-pattern.json
```

## Nicht geprüft
- Hörtest durch einen Menschen (nur Messung: Tempo-Erkennung, Pegel, keine Übersteuerung)
- Abspielen im Programm während des Bearbeitens (gespeichert wird als Lied, abgespielt über die Bibliothek)
- Optik des Beat-Modus nicht per Bildschirmfoto geprüft
