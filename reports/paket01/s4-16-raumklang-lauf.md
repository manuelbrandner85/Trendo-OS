# Beleg S4-16 – Raumklang 5.1, Kanalplan und Kopfhörer-Vorschau (06.11.2026)

- Werkzeug: Trendo-eigene Abmischung (paarweises Panorama gleicher Leistung, Lautsprecher nach ITU-R BS.775), Prüfung mit ffprobe aus ffmpeg version 9.0.2-full_build-www.gyan.dev
- Lauf: `dotnet test tests/Trendo.Core.Tests -c Release --filter "FullyQualifiedName~SpatialAudioTests|FullyQualifiedName~AudioMulti"` mit `TRENDO_51_BELEG=<temp>/trendo-51beleg` → Exitcode 0
- Ergebnis: Bestanden!   : Fehler:     0, erfolgreich:     4, übersprungen:     0, gesamt:     4, Dauer: 204 ms
- Oberfläche: `Trendo.App.Tests.exe -class Trendo.App.Tests.AudioPro_Ui` → Failed 0 (Spur auf −110°, Knopf „5.1“ schreibt 5.1-WAV mit Kanalmaske, Kopfhörer-Vorschau und Kanalplan)
- Eingaben: Spur „Hall hinten links“ 1 kHz auf −110°, Spur „Stimme rechts“ 300 Hz auf +30°

## ffprobe, Kanalplan und Kopfhörer-Messung

ffprobe: pcm_s24le|48000|6|5.1(side)
2.000000

Kanal | Lautsprecher | Winkel | Pegel (RMS)
---|---|---|---
1 L | Links vorn | -30° | stumm
2 R | Rechts vorn | +30° | -11,0 dBFS
3 C | Mitte | 0° | stumm
4 LFE | Tiefton (LFE) | – | -34,1 dBFS
5 Ls | Links Raum | -110° | -11,0 dBFS
6 Rs | Rechts Raum | +110° | stumm

Kopfhörer-Vorschau nur „Links Raum“: links -15,0 dBFS, rechts -21,9 dBFS, rechts 27 Abtastwerte später (0,56 ms)

- Positiv S4-16-P: richtige Kanäle (R und Ls), alle anderen stumm, LFE nur Tiefen; WAV wird von FFmpeg als 6 Kanäle „5.1(side)“ erkannt (Kanalmaske 0x60F).
- Negativ S4-16-N: Eine Stereo-Datei wird nicht still als 5.1 geschrieben – Fehler „Kein 5.1“, keine Datei entsteht.

## Dateien (SHA-256)
```
9083a7c480ff8c76678f17a5f034f0bf402b85a30c9bed1079ff7bbc008fbc17 *kopfhoerer-vorschau.wav
39cf96fb20318191d1ba1e66090919adbb1127e75e1c27c8de10b35048204e72 *mix-51.wav
```

## Nicht geprüft
- Hörtest an echten 5.1-Lautsprechern und mit Kopfhörern durch einen Menschen
- Messung mit echten HRTF-Daten (SOFA); die Vorschau nutzt ein Kopfmodell (Laufzeit + Kopfschatten)
- Dolby Atmos/Objekt-Formate, 7.1, Engine-Test in Unreal/Godot (Spiele: Sitzung B)
