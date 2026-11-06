# Beleg S4-20 – Audio-Prüfung und Freigabe-Sperre (06.11.2026)

- Werkzeug: Trendo-eigene Messung (BS.1770-Lautheit, 4×-True-Peak), Gegenprobe mit ffmpeg version 9.0.2-full_build-www.gyan.dev ebur128
- Lauf: `dotnet test tests/Trendo.Core.Tests -c Release --filter FullyQualifiedName~AudioQaTests` mit `TRENDO_AQA_BELEG=<temp>/trendo-aqabeleg` → Exitcode 0
- Ergebnis: Bestanden!   : Fehler:     0, erfolgreich:     3, übersprungen:     0, gesamt:     3, Dauer: 448 ms
- Oberfläche: `Trendo.App.Tests.exe -class Trendo.App.Tests.AudioQa_Ui` → Total 1, Failed 0 (Übersteuerung/fehlende Lizenz/KI-Stimme ohne Einwilligung gesperrt, danach Freigabe mit WAV 24 Bit und Prüfbericht)

## Gegenprobe mit FFmpeg (musik.wav: Sinus 220 Hz + rosa Rauschen, Stereo, 48 kHz, 24 Bit)
```
Trendo: -31,84 LUFS, True Peak -25,65 dBTP
FFmpeg ebur128: -31,80 LUFS, True Peak -25,60 dBFS
```

## Prüfbericht (Positivfall S4-20-P)

### Audio-Prüfung „musik“

Erstellt: 06.11.2026 23:30 · Ziel: Musik / Streaming (−14 LUFS)

**Freigabe möglich.**

#### Messwerte
| Wert | Ergebnis |
|---|---|
| Lautheit | -31,8 LUFS |
| True Peak | -25,7 dBTP |
| Sample Peak | -25,7 dBFS |
| Übersteuerungen | 0 |
| Format | 48000 Hz · 2 Kanäle · 24 Bit · 6,00 s |
| Phase (Korrelation) | 1,00 |
| Gleichspannung | 0,0000 |
| Stille Anfang/Ende | 0,0 s / 0,0 s |

#### Prüfung
- 🟢 Keine Übersteuerung.
- 🟢 True Peak -25,7 dBTP.
- 🟡 Lautheit -31,8 LUFS – Ziel -14 LUFS (Musik / Streaming (−14 LUFS)).
- 🟢 Format: 48000 Hz, 2 Kanale, 24 Bit, 6,0 s.
- 🟢 „Eigene Aufnahme“: eigene Rechte.

#### Dateien
| Datei | Größe | SHA-256 |
|---|---|---|
| musik.wav | 1.728.102 Bytes | `cd31ce1a11a5eeb0a7d3d815d363ad8f288adda6b72d5ac3496e43f28b5bcf8c` |

## Prüfbericht (Negativfall S4-20-N, übersteuert)

### Audio-Prüfung „uebersteuert“

Erstellt: 06.11.2026 23:30 · Ziel: Musik / Streaming (−14 LUFS)

**Freigabe gesperrt:** Übersteuert: 3520× volle Aussteuerung (Clipping) – erst leiser mischen oder begrenzen.

#### Messwerte
| Wert | Ergebnis |
|---|---|
| Lautheit | 1,0 LUFS |
| True Peak | 0,1 dBTP |
| Sample Peak | -0,0 dBFS |
| Übersteuerungen | 3520 |
| Format | 48000 Hz · 2 Kanäle · 16 Bit · 2,00 s |
| Phase (Korrelation) | 1,00 |
| Gleichspannung | 0,0000 |
| Stille Anfang/Ende | 0,0 s / 0,0 s |

#### Prüfung
- 🔴 Übersteuert: 3520× volle Aussteuerung (Clipping) – erst leiser mischen oder begrenzen.
- 🟡 True Peak 0,1 dBTP über -1 – beim Umwandeln (MP3/AAC) kann es verzerren.
- 🟡 Lautheit 1,0 LUFS – Ziel -14 LUFS (Musik / Streaming (−14 LUFS)).
- 🟢 Format: 48000 Hz, 2 Kanale, 16 Bit, 2,0 s.
- 🟢 „Eigene Aufnahme“: eigene Rechte.

#### Dateien
| Datei | Größe | SHA-256 |
|---|---|---|
| uebersteuert.wav | 384.044 Bytes | `00883aacf838a1fdf7b2aa6d2855ead0b421db12c48838e31cb30fc2436322f8` |

## Dateien (SHA-256)
```
cd31ce1a11a5eeb0a7d3d815d363ad8f288adda6b72d5ac3496e43f28b5bcf8c *musik.wav
00883aacf838a1fdf7b2aa6d2855ead0b421db12c48838e31cb30fc2436322f8 *uebersteuert.wav
```

## Nicht geprüft
- Rechte werden als Angabe der Person geprüft (Lizenztext, KI-Stimme, Einwilligung) – keine Online-Prüfung von Lizenzen
- MP3/AAC-Ausgabe nach der Freigabe (True Peak nach dem Umwandeln)
- Lautheitsbereich (LRA) und Kurzzeit-Lautheit
