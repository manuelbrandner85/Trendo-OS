# Beleg S4-09 – Busse, Sends, Sidechain, Routing-Kreis (07.11.2026)

- Lauf: `dotnet test tests/Trendo.Core.Tests -c Release --filter "FullyQualifiedName~MixRoutingTests|FullyQualifiedName~AudioMulti|FullyQualifiedName~SpatialAudio"` → Exitcode 0
- Ergebnis: Bestanden!   : Fehler:     0, erfolgreich:     6, übersprungen:     0, gesamt:     6, Dauer: 232 ms
- Oberfläche: `Trendo.App.Tests.exe -class Trendo.App.Tests.AudioBuses_Ui` → Failed 0 (Bus anlegen, Spur darauf leiten, EQ übernehmen und rückgängig machbar, Kreis über die Auswahl abgewiesen)

## Positiv S4-09-P (gemessen an der Mischung, echte WAV-Dateien)
- Bus „Musik“ mit Tiefen −12 dB: Pegel der 110-Hz-Musik sinkt auf unter die Hälfte.
- Sidechain: Bus-Kompressor wird von der Stimme gesteuert – die Musik ist während der Stimme (1–2 s) um mehr als 40 % leiser, davor unverändert.
- Send der Stimme auf den Bus „Hall“ (Hall 0,8): nach dem Ende der Stimme (2,02–2,3 s) bleibt ein hörbarer Ausklang; ohne Send ist dort Stille.

## Negativ S4-09-N
- Bus A → B → C, dann C → A: „Routing-Kreis … abgewiesen“, das Projekt ist Byte für Byte unverändert. Auch A → A abgewiesen.
- Send auf den Bus, auf den die Spur schon direkt geht, unbekanntes Ziel und unbekannte Sidechain-Spur werden abgelehnt.

## Nicht geprüft
- Hörtest durch einen Menschen; Hall ist ein einfacher Kammfilter-Hall
- Busse im 5.1-Abmischen (dort zählen nur die Spuren)
- S4-01 Aufnahme in die Mehrspur: Aufnehmen gibt es im Einzelspur-Editor (10 s); ohne Mikrofon meldet Trendo das statt eine Scheinaufnahme anzulegen – nicht neu geprüft
