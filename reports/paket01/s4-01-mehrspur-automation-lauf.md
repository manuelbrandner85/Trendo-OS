# Beleg S4-01 – Mehrspur: Automation, Aufnahme als Spur, ohne Gerät keine Scheinaufnahme (07.11.2026)

- Lauf: `dotnet test tests/Trendo.Core.Tests -c Release --filter "FullyQualifiedName~AutomationTests|FullyQualifiedName~MixRoutingTests|FullyQualifiedName~AudioMulti"` → Exitcode 0
- Ergebnis: Bestanden!   : Fehler:     0, erfolgreich:     5, übersprungen:     0, gesamt:     5, Dauer: 197 ms
- Oberfläche: `Trendo.App.Tests.exe -class Trendo.App.Tests.AudioMultiRecord_Ui -class Trendo.App.Tests.AudioPro_Ui` → Failed 0

- Positiv S4-01-P: Mehrspur-Projekt anlegen, Spuren verschieben, Stumm/Solo, Rückgängig und Abmischen (vorhanden, Test `AudioPro_Ui`); neu: Aufnahme landet als neue Spur; Automation „0:0, 1:0, 2:-12, 4:-12, 5:0“ senkt die Musik gemessen genau um 12 dB zwischen 2 und 4 s, vorne und hinten unverändert – auch wenn die Spur über einen Bus läuft.
- Negativ S4-01-N: Ohne Mikrofon entsteht keine Spur und keine Scheinaufnahme, sondern ein Hinweis; ungeordnete Punkte, Pegel über +12 dB und unverständliche Eingaben werden abgelehnt, das Projekt bleibt unverändert.

## Nicht geprüft
- Echte Mikrofonaufnahme in die Mehrspur (im Test ersetzt; der Aufnahmeweg ist derselbe wie im Einzelspur-Editor)
- Automation per Maus als Kurve in der Spur zeichnen (Eingabe als Punkte)
