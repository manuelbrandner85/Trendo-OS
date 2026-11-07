# Beleg S4-19 – Melodie → MIDI mit Sicherheit und Mehrstimmigkeits-Erkennung (07.11.2026)

- Werkzeug: Trendo-eigene Erkennung (YIN-Grundton, Spektrum-Prüfung), kein KI-Modell
- Lauf: `dotnet test tests/Trendo.Core.Tests -c Release --filter FullyQualifiedName~AudioToMidiTests` → Exitcode 0
- Ergebnis: Bestanden!   : Fehler:     0, erfolgreich:     2, übersprungen:     0, gesamt:     2, Dauer: 350 ms
- Oberfläche: `Trendo.App.Tests.exe -class Trendo.App.Tests.AudioMidi_Ui` → Failed 0 (WAV mit C4 D4 E4 F4 → MIDI mit genau diesen Noten, Unsicherheitsbericht, Noten liegen in Trendo Musik; Akkord → „NICHT exakt“)

- Positiv S4-19-P: Melodie A3 C4 E4 A4 G4 (mit Obertönen) wird Note für Note erkannt, jede Note mit Sicherheit ≥ 80 %, Ergebnis „Einstimmig und sicher erkannt“; MIDI über die Piano-Roll identisch lesbar.
- Negativ S4-19-N: Akkord Am (A3+C4+E4 gleichzeitig) → „Mehrstimmig … NICHT exakt“; Rauschen → nicht exakt; Stille → „Keine Melodie erkannt“.
- Sprach-Transkript (Text) gibt es bereits über Whisper (Videoschnitt-Untertitel, Lied verstehen).

## Nicht geprüft
- Echte Gesangsaufnahmen mit Vibrato und Gleiten (nur erzeugte Töne)
- Mehrstimmige Transkription (Akkorde werden bewusst nicht geschätzt, sondern gemeldet)
