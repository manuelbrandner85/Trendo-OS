# T-0217 – Gegenstände einscannen

## Auftrag
Das Studio „Gegenstände einscannen“ bleibt beim Namen. Aus mindestens drei Fotos oder aus einem Video wird ein 3D-Modell. Meshroom hat Vorrang, sonst COLMAP. Fehlen beide, öffnet Trendo nur die offizielle Seite und installiert nichts. Das Ergebnis geht im Studio „3D-Modelle erstellen“ auf. Gibt es nur Punkte, entsteht eine echte äußere Hülle, und der Text sagt das. Die bisherigen Punktwolken-Werkzeuge bleiben.

## Branch
`cursor/gegenstaende-scannen-db3a`

## Geänderte Dateien
- `src/Trendo.Core/W6/Photogrammetry.cs` – Fotos, FFmpeg-Einzelbilder, Meshroom- und COLMAP-Befehle, PLY nach OBJ, Hülle
- `src/Trendo.Core/Dependencies.cs` – Meshroom und COLMAP nur geführt, keine stille Installation
- `src/Trendo.Core/ToolCheck.cs` – Erkennung, ohne die Programme zu starten
- `src/Trendo.App/MainWindow.Scan3D.cs` – Oberfläche
- `src/Trendo.App/MainWindow.Spec09.cs` – neue Fläche über den bestehenden Werkzeugen
- `tests/Trendo.Core.Tests/PhotoScanTests.cs`
- `tests/Trendo.App.Tests/PhotoScanUiTests.cs`
- `docs/build-log/2026-10-22-1844-gegenstaende-scannen.md`

`requirements.yaml` ist unverändert. SPEC-09 steht nicht in `StudioTools.Required`, damit die Punktwolken-Werkzeuge ohne Meshroom aufgehen.

## Testergebnis
`dotnet test`, Konfiguration Release, 22.10.2026 gegen 18:44 Uhr (Europe/Berlin):

- Trendo.Core.Tests: Bestanden 447, übersprungen 10, Fehler 0, gesamt 457. Dauer 5 s. Die drei neuen Tests sind darin. Die 10 Übersprungenen sind `WinRealTests` (kein Windows).
- Trendo.App.Tests: Gesamtlauf Bestanden 187, Fehler 1, gesamt 188, Dauer 5 min 39 s. Der Fehler ist `X-06: Live-Suche bei GitHub` (leere Antwort). Derselbe Test danach noch einmal leer, der nächste Lauf bestanden. Die vier neuen UI-Tests und die beiden bestehenden SPEC-09-UI-Tests waren grün.
- Trendo.Api.Tests: Bestanden 14, Fehler 0, gesamt 14. Dauer 327 ms.

## Offene Punkte / Risiken
- Kein Meshroom und kein COLMAP auf diesem Rechner. Der Rechenweg ist der echte Befehl; der Test schreibt das OBJ selbst.
- Eine große Punktwolke braucht für die Hülle spürbar Zeit. Das passiert nur, wenn kein Netz mit Flächen da ist.
- Die Hülle ist die äußere Form, kein fotorealistisches Modell. Die Oberfläche sagt das.

## Nicht geprüft
- Kein echter Meshroom- oder COLMAP-Lauf, kein echtes FFmpeg gegen ein Video, keine echte Kamera.
- Kein Codesign. QA-017 und QA-019 nicht als bestanden gewertet. Status in `requirements.yaml` nicht angehoben.
