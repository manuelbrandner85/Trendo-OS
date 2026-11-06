# Beleg S3-02/14/15/18/20 – Film-Auslieferung mit echtem FFmpeg (06.11.2026)

- Werkzeug: ffmpeg version 9.0.2-full_build-www.gyan.dev
- Lauf: `dotnet test tests/Trendo.Core.Tests -c Release --filter FullyQualifiedName~FilmDeliveryTests` mit `TRENDO_FILM_BELEG=<temp>/trendo-filmbeleg` → Exitcode 0
- Ergebnis: Bestanden!   : Fehler:     0, erfolgreich:     3, übersprungen:     0, gesamt:     3, Dauer: 814 ms
- Oberfläche: `Trendo.App.Tests.exe -class Trendo.App.Tests.FilmDelivery_Ui` → Total 1, Failed 0 (FFmpeg dort ersetzt; fehlendes Medium wird gemeldet, nichts ausgeliefert)
- Eingaben: szene1.mp4 (testsrc2, 2 s, Sinus 330 Hz), szene2.mp4 (blau, 2 s, Sinus 550 Hz), thema.wav (Sinus 220 Hz, 3 s) – alle mit FFmpeg lavfi erzeugt; Musik ab 0,5 s, Pegel 50 %, 25 fps

## Ausgaben (ffprobe)
```
Unser Film.mp4
stream|codec_name=h264|width=320|height=180|r_frame_rate=25/1|bits_per_raw_sample=8
stream|codec_name=aac|r_frame_rate=0/0|bits_per_raw_sample=N/A
format|duration=4.000000
exit 0
Unser Film – Stem Dialog.wav
stream|codec_name=pcm_s24le|r_frame_rate=0/0|bits_per_raw_sample=24
format|duration=4.000000
exit 0
Unser Film – Stem Musik.wav
stream|codec_name=pcm_s24le|r_frame_rate=0/0|bits_per_raw_sample=24
format|duration=4.000000
exit 0
```

## SHA-256 (sha256sum, unabhängig vom Prüfbericht)
```
f327664bc236ff0d1d947a1d5c2ef4d58822aee251a4ef35afba02c298e093a6 *Prüfbericht.md
82b8237478d302d9d16399ed8d9062c1805f60903ac931827ff4cc7e9f3a7a5a *Unser Film – Cue-Liste.csv
f1de62530251096b93469077dc8f9885d323ab01fcf76dac2969772efda97c1d *Unser Film – Stem Dialog.wav
7982ae685325c7d4f71cd04690c6e4be6350659ad04212aedb4db475761a3ab3 *Unser Film – Stem Musik.wav
afb3e92811b066fee44a3c288c4aae9d9ff85186f5750764c65df23061ff1b37 *Unser Film.edl
106548b1705065b4d0fc343372c8ca4e4ae685cc44da274bb827e82d873c3fcb *Unser Film.mp4
b9592f992cade12d41f48edf4c17917547717f58e76abe0e7574a1aba85101b8 *Unser Film.otio
39f745624acf9f3c355d448a9730653c4e39388ccb1ad829c0ef47541f35947c *szene1.mp4
70853d3c470d51a98bda2999dcfcecc5905b94802ab8ab6781b1f35ec405ec35 *szene2.mp4
539d8e5f0ee62214a57c914b05c49b9c3f62162daad8222f45d2c8eb73ce82b1 *thema.wav
```

## Schnittliste (EDL, CMX 3600; Pfade neutralisiert)
```
TITLE: Unser Film
FCM: NON-DROP FRAME

001  AX       AA/V  C         00:00:00:00 00:00:02:00 00:00:00:00 00:00:02:00
* FROM CLIP NAME: szene1.mp4
* SOURCE FILE: <temp>/szene1.mp4

002  AX       AA/V  C         00:00:00:00 00:00:02:00 00:00:02:00 00:00:04:00
* FROM CLIP NAME: szene2.mp4
* SOURCE FILE: <temp>/szene2.mp4

003  AX       A2    C         00:00:00:00 00:00:03:00 00:00:00:13 00:00:03:13
* FROM CLIP NAME: thema.wav
* SOURCE FILE: <temp>/thema.wav

```

## Cue-Liste
```
Cue;Titel;Ein (TC);Aus (TC);Dauer (s);Datei;Pegel
M01;thema;00:00:00:13;00:00:03:13;3.00;thema.wav;50 %
```

## Prüfbericht (von Trendo erzeugt)

### Auslieferung „Unser Film“

Erstellt: 06.11.2026 23:04 · Werkzeug: FFmpeg (Test)

#### Prüfung
- 🟢 Alle Quellmedien vorhanden.
- 🟢 Filmlänge 4,00 s wie im Schnitt.
- 🟢 Kein Schwarzbild.
- 🟢 Stem „Dialog“ gleich lang wie der Film – synchron.
- 🟢 Stem „Musik“ gleich lang wie der Film – synchron.
- 🟡 Lautheit -21,0 LUFS – Ziel -14 LUFS (im Export „Lautheit angleichen“).
- 🟢 Spitze -16,0 dBFS.

#### Dateien
| Datei | Größe | SHA-256 |
|---|---|---|
| Unser Film.mp4 | 165.891 Bytes | `106548b1705065b4d0fc343372c8ca4e4ae685cc44da274bb827e82d873c3fcb` |
| Unser Film – Stem Dialog.wav | 1.152.102 Bytes | `f1de62530251096b93469077dc8f9885d323ab01fcf76dac2969772efda97c1d` |
| Unser Film – Stem Musik.wav | 1.152.102 Bytes | `7982ae685325c7d4f71cd04690c6e4be6350659ad04212aedb4db475761a3ab3` |
| Unser Film.edl | 613 Bytes | `afb3e92811b066fee44a3c288c4aae9d9ff85186f5750764c65df23061ff1b37` |
| Unser Film.otio | 4.017 Bytes | `b9592f992cade12d41f48edf4c17917547717f58e76abe0e7574a1aba85101b8` |
| Unser Film – Cue-Liste.csv | 107 Bytes | `82b8237478d302d9d16399ed8d9062c1805f60903ac931827ff4cc7e9f3a7a5a` |

## Nicht geprüft
- Import der EDL/OTIO in DaVinci Resolve, Premiere oder OpenTimelineIO selbst (nicht installiert; OTIO-Aufbau nur im Test gegen das Schema „Timeline.1/Stack.1/Track.1/Clip.2/Gap.1“ geprüft)
- Effekt-Stem (S3-14 nennt Dialog/Musik/Effekte): Trendo hat keine eigene Effektspur – nur Dialog und Musik
- Drop-Frame-Timecode (29,97 fps): nur ganzzahlige Bildraten
- Lange Projekte und echte Kamera-Dateien: nur kurze Testmedien
