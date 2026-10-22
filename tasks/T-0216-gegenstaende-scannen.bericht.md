# T-0216 – Gegenstände einscannen

## Auftrag
Das Studio „Gegenstände einscannen“ (SPEC-09) macht aus Fotos oder einem Video ein 3D-Modell. Das Werkzeug ist COLMAP oder Meshroom. Trendo fragt einmal über die vorhandene Werkzeugprüfung, bevor etwas installiert wird. Der Fortschritt steht in der Seite. Danach öffnet „Im 3D-Studio öffnen“ genau diese Datei im 3D-Studio aus Pull Request 221. Die bisherigen Punktwolken-Werkzeuge bleiben.

Der PR-Ablauf verlangt den Branch `cursor/gegenstaende-scannen-fec1`. Kameras und Roboter liegen auf einem eigenen Branch und sind hier nicht enthalten.

## Branch
`cursor/gegenstaende-scannen-fec1`

## Geänderte Dateien
- `src/Trendo.Core/W6/PhotoScan.cs` – Plan und Lauf für COLMAP (CPU, ohne CUDA) und Meshroom, Video über FFmpeg, Netz als OBJ, äußere Hülle wenn nur Punkte da sind
- `src/Trendo.App/MainWindow.Spec09Scan.cs` – Karte im Studio: Fotos, Video, Werkzeug, Stopp, Fortschritt, Öffnen im 3D-Studio
- `src/Trendo.App/MainWindow.Spec09.cs` – Einleitung und die neue Karte über den alten Punktwolken-Werkzeugen
- `src/Trendo.Core/Dependencies.cs` – COLMAP als Benutzer-ZIP mit fester Prüfsumme, Meshroom nur über die offizielle Seite, Prozessstart mit Arbeitsordner
- `src/Trendo.Core/ToolCheck.cs` – Prüfung `colmap -h` und `meshroom_batch --help`. SPEC-09 steht nicht in der Pflichtliste beim Öffnen
- `src/Trendo.Core/MeshIO.cs` – OBJ, STL, GLB und glTF als Fläche lesen
- `THIRD_PARTY.md` – Zeilen für COLMAP und Meshroom
- `tests/Trendo.Core.Tests/PhotoScanTests.cs`
- `tests/Trendo.App.Tests/PhotoScanUiTests.cs`

`requirements.yaml` ist unverändert. Status nicht angehoben.

## Testergebnis
`dotnet test` mit Konfiguration Release. Die Ausgabe auf diesem Rechner sagt „Passed!“:

- Trendo.Core.Tests: Passed 450, Skipped 10, Failed 0, Total 460. Dauer 5 s. Darin die 6 neuen Foto-Scan-Tests.
- Trendo.App.Tests: Passed 187, Failed 0, Total 187. Dauer 6 min 4 s. Darin die 3 neuen Oberflächen-Tests.
- Trendo.Api.Tests: Passed 14, Failed 0, Total 14. Dauer 323 ms.

## Offene Punkte / Risiken
- Die längste Seite wird auf 20 cm gesetzt. Fotos haben kein echtes Maß. Das steht in der Seite und in der Abschlussmeldung.
- Eine Punktwolke ohne Flächen wird zur äußeren Hülle, mit diesem Hinweis. Ein Netz mit Flächen bleibt ein Netz.
- COLMAP läuft ohne Grafikkarte (`use_gpu 0`, Delaunay). Meshroom startet mit `-i` und `-o` und dem Ordner der Programmdatei als Arbeitsordner. Weicht die Kommandozeile einer neuen Meshroom-Ausgabe ab, zeigt der Knopf die Meldung des Programms.
- Eine Windows-Programmdatei (`.exe`) startet Trendo auf Linux nicht. Der Hinweis nennt das Paket der Distribution.
- Die Prüfsumme gilt für das offizielle ZIP `colmap-x64-windows-nocuda.zip` 4.2.1. Meshroom hat keine feste Prüfsumme und keinen stillen Installer.

## Nicht geprüft
- Kein echtes COLMAP, kein echtes Meshroom, kein echtes FFmpeg auf einem Gegenstand. Kein Download der 122 MB, keine Windows-Einrichtung, kein echter Scan.
- Kein Codesign. QA-017 und QA-019 nicht als bestanden gewertet. Kein PC_VERIFIED.
