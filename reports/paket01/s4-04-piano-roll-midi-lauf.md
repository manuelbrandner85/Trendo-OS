# Beleg S4-04 – Piano-Roll, Quantisieren, MIDI speichern und öffnen (06.11.2026)

- Werkzeug: Trendo-eigener MIDI-Schreiber/-Leser (Standard-MIDI-Datei Format 1, 480 Ticks je Viertel); Gegenprobe: ffprobe aus ffmpeg version 9.0.2-full_build-www.gyan.dev
- Lauf: `dotnet test tests/Trendo.Core.Tests -c Release --filter FullyQualifiedName~PianoRollTests` mit `TRENDO_MIDI_BELEG=<temp>/trendo-midibeleg` → Exitcode 0
- Ergebnis: Bestanden!   : Fehler:     0, erfolgreich:     3, übersprungen:     0, gesamt:     3, Dauer: 109 ms
- Oberfläche: `Trendo.App.Tests.exe -class Trendo.App.Tests.MusikNoten_Ui` → Total 1, Failed 0 (drei Noten per Mausklick, ＋1 Halbton und Rückgängig, Quantisieren 1/16, MIDI speichern, kaputte Datei abgewiesen, gespeicherte Datei identisch geöffnet)

## Gelesene Datei
```
Noten: 7, Tempo 96 BPM, Takt 4/4, Name „Melodie Erika Muster“, Warnungen: 0
ffprobe format_name: libmodplug
A3   Start     0  Länge  960  Anschlag 90
C4   Start     0  Länge  960  Anschlag 80
E4   Start     0  Länge  960  Anschlag 80
A4   Start   960  Länge  240  Anschlag 96
H4   Start  1200  Länge  240  Anschlag 96
C5   Start  1440  Länge  480  Anschlag 110
C5   Start  1920  Länge  120  Anschlag 60
```

- Positiv S4-04-P: gespeichert → geöffnet → wieder gespeichert ist Byte für Byte gleich (gleiche SHA-256); auch MIDI-Dateien eines anderen Schreibers (Melodie-Export) werden gelesen; FFmpeg erkennt die Datei als MIDI.
- Negativ S4-04-N: Datei ohne MIDI-Kopf → „Nicht geöffnet“, das offene Stück bleibt unverändert; abgeschnittene Datei → lesbarer Teil plus Warnungen; SMPTE-Zeitbasis abgelehnt; ungültige Noten werden nicht geschrieben.

## Dateien (SHA-256)
```
ee650b44b118c394477ea1c430c33453492fd931399e591ae67d7eccc5be8b91 *melodie-wieder.mid
ee650b44b118c394477ea1c430c33453492fd931399e591ae67d7eccc5be8b91 *melodie.mid
```

## Nicht geprüft
- Abspielen der Noten mit einem Klangerzeuger (kein Software-Synth für MIDI in Trendo; Notenprogramme öffnen die Datei)
- Notensatz (Notenblatt), Akkord-Erkennung in der Piano-Roll, mehrere Spuren/Kanäle beim Bearbeiten
- Optik der Piano-Roll nicht per Bildschirmfoto geprüft
