# Beleg S4-14 – Filmmusik auf den Schnitt: Tempo-Map, Leitmotiv am Timecode (07.11.2026)

- Lauf: `dotnet test tests/Trendo.Core.Tests -c Release --filter "FullyQualifiedName~ScoreSyncTests|FullyQualifiedName~PianoRollTests"` mit `TRENDO_SCORE_BELEG=<temp>/trendo-scorebeleg` → Exitcode 0
- Ergebnis: Bestanden!   : Fehler:     0, erfolgreich:     6, übersprungen:     0, gesamt:     6, Dauer: 107 ms
- Oberfläche: `Trendo.App.Tests.exe -class Trendo.App.Tests.MusikNoten_Ui` → Failed 0 (Noten-Modus: Takt 9 auf TC 00:00:12:12 eingepasst = 153,846 BPM, „trifft den Schnitt“; ungültiger Timecode gemeldet)

## Messung
```
eingepasstes Tempo: 153,846 BPM (Takt 9 = 12,48 s)
Tempowechsel in der Datei: Tick 0 → 153,85 BPM, Tick 23040 → 90 BPM
ffprobe: libmodplug
Schnitt Strand: Ziel 12,480 s, Musik 12,480 s, 0 Bilder → OK
Tür (absichtlich daneben): Ziel 16,120 s, Musik 15,600 s, -13,00 Bilder → FAIL
M02;Leitmotiv Meer;00:00:12:12;00:00:16:00;3.52
```

- Positiv S4-14-P: Leitmotiv (A – C – E) beginnt auf Takt 9 genau am Schnitt bei 12,48 s (0 Bilder Abweichung); MIDI enthält beide Tempowechsel; FFmpeg erkennt die Datei; Cue-Zeile im Format der Film-Cue-Liste.
- Negativ S4-14-N: Treffpunkt 13 Bilder daneben → FAIL (und im Test 3 Bilder → FAIL, unter 1 Bild → OK); unmögliches Tempo, Map ohne Takt 1 und ungeordnete Wechsel werden abgelehnt.

## Dateien (SHA-256)
```
d6bcce4019727f3ec2da5b857f930835fbd0d5a6317c14c524dbad05b3d2d245 *leitmotiv.mid
```

## Nicht geprüft
- Abspielen des Leitmotivs mit Klangerzeuger (Notenprogramm öffnet die MIDI)
- Mehrere Treffpunkte mit automatischen Tempowechseln dazwischen (geprüft wird je Treffpunkt; Wechsel setzt man von Hand)
