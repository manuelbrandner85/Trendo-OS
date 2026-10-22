# T-0215 – Kameras und Roboter steuern

## Auftrag
Das Studio „Kameras & Roboter steuern“ soll Kameras wirklich zeigen und Roboter wirklich ansteuern. Bisher waren es nur Bildauswertung und ein Rechner.

## Branch
`cursor/kameras-roboter-fec1`

## Geänderte Dateien
- `src/Trendo.Core/W6/Cameras.cs` – Webcams (sysfs, DirectShow-Text), RTSP, ONVIF-Auswertung, FFmpeg-Aufrufe, Aufnahmeprozess
- `src/Trendo.Core/W6/RobotDrive.cs` – Simulation, Trockenlauf, echte Fahrt nur mit Freigabe, Not-Aus
- `src/Trendo.App/CameraNetwork.cs` – ONVIF im lokalen Netz und TCP zum Gerät (der Kern bleibt ohne Netz-Bibliothek)
- `src/Trendo.App/MainWindow.Core09.cs` – Kamerablock und Armsteuerung über den bisherigen Rechnern
- `src/Trendo.App/MainWindow.Core09Cam.cs` – Livebild, Foto, Aufnahme, Freigabe, Not-Aus
- `tests/Trendo.Core.Tests/CameraRobotTests.cs`
- `tests/Trendo.App.Tests/CameraRobotUiTests.cs`
- `docs/build-log/2026-10-22-CORE-09-Kameras.md`

## Testergebnis
`dotnet test Trendo.sln -c Release`:

- Trendo.Core.Tests: Bestanden 453, übersprungen 10, Fehler 0, gesamt 463. Dauer 6 s.
- Trendo.App.Tests: Bestanden 189, Fehler 0, gesamt 189. Dauer 6 min 37 s.
- Trendo.Api.Tests: Bestanden 14, Fehler 0, gesamt 14. Dauer 324 ms.

## Offene Punkte / Risiken
- Livebild und Aufnahme brauchen FFmpeg. Fehlt es, öffnet „FFmpeg einrichten“ die bestehende Frage vor der Installation.
- Eine echte Fahrt spricht ein Gerät per TCP an (Zeilen `MOVE`, `GRIP`, `STOP`). Serielle Ports ohne Netzwerkbrücke sind nicht angebunden.
- ONVIF wartet zwei Sekunden im lokalen Netz. Antwortet keine Kamera, bleibt die Liste leer und die Meldung sagt das.

## Nicht geprüft
- Keine physische Webcam, keine echte ONVIF-Kamera und kein echter Roboterarm an diesem Rechner. Die TCP-Prüfung läuft gegen einen lokalen Anschluss. Die Webcam-Liste liest das echte sysfs, wenn es Einträge gibt; der UI-Test übergibt eine Kamera.
