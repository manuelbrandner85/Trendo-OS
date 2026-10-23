# T-0200 – 3D-Studio: eigene Objekte

## Auftrag
Im Studio „3D-Modelle erstellen“ kann man ein Objekt aus Teilen bauen (Würfel, Kugel, Zylinder, Kegel, Torus, Ebene, Text), Größe, Position, Drehung und Farbe setzen, mehrere Teile zusammenfügen, Kanten abrunden, glätten, spiegeln, aushöhlen und ein Loch bohren. Ein Satz an die lokale KI (Ollama) schlägt Teile vor, die man vor dem Bauen sieht. Die Vorschau entsteht in Trendo, ohne Blender. Import: OBJ, STL, GLB, glTF; FBX und Blend über Blender. Export: OBJ (cm), STL (mm), GLB (m), und über Blender FBX und Blend. „In Blender öffnen“ öffnet genau dieses Objekt. Fehlt Blender, fragt Trendo einmal, bevor etwas installiert wird. Speichern und Wiederöffnen laufen über das Projektregister.

Der Auftrag nannte den Branch `box/T-0200-3d-studio`. Dieser Lauf legt `cursor/t-0200-3d-studio-97ef` an, weil der PR-Ablauf dieses Namensschema verlangt. Auf den Remote-Branches lag keine angefangene T-0200-Arbeit; `origin/w4/core05-3d` ist der bereits gemergte alte Stand.

## Branch
`cursor/t-0200-3d-studio-97ef`

## Geänderte Dateien
- `src/Trendo.Core/Scene3D.cs` – Szene, Teile, Maße in cm, JSON, Blender-Auftrag
- `src/Trendo.Core/ScenePlan.cs` – Satz an die KI, Antwort als Teileliste
- `src/Trendo.Core/Mesh3D.cs` – Körper, Loch, Abrundung, Vorschau-PNG
- `src/Trendo.Core/MeshIO.cs` – OBJ, STL, GLB lesen und schreiben
- `src/Trendo.Core/Studio3D.cs` – Blender schreibt die aktuelle Szene als .blend (alter Weg bleibt)
- `src/Trendo.App/MainWindow.W4Studio3D.cs` – Oberfläche
- `src/Trendo.App/MainWindow.axaml.cs` – Studio öffnet ohne Blender; „Später“ kehrt ins 3D-Studio zurück; Projekte-Knopf „Im 3D-Studio öffnen“
- `tests/Trendo.Core.Tests/Scene3DTests.cs`
- `tests/Trendo.App.Tests/W4Studio3DUiTests.cs`

Studioname bleibt „3D-Modelle erstellen“. `requirements.yaml` ist unverändert. SPEC-07 ist nicht angefasst.

## Testergebnis
`dotnet test` mit SDK 10.0.402, Konfiguration Release:

- Trendo.Core.Tests: Bestanden 426, übersprungen 10, Fehler 0, gesamt 436. Dauer 5 s. Die neuen Szenen-Tests (9) sind darin. Der alte Blender-Rundlauf kehrt sofort zurück, weil `blender` nicht im PATH liegt, und zählt damit als bestanden, ohne Blender zu starten.
- Trendo.App.Tests: Bestanden 137, Fehler 1, gesamt 138. Dauer 5 min 10 s. Die vier neuen 3D-Tests sind bestanden (Teil bauen und Vorschau, Export und Projekt, KI-Vorschlag, Blender fragen und öffnen).
- Trendo.Api.Tests: Bestanden 14, Fehler 0, gesamt 14. Dauer 216 ms.

Der eine Fehler in Trendo.App.Tests ist `SYS-03-P14` (Seite als PDF). Auf diesem Rechner liegt `/usr/local/bin/google-chrome`, deshalb läuft der Test und scheitert in `Sys03UiTests.cs` Zeile 78 (`File.Exists`). Ein zweiter Lauf nur dieses Tests scheiterte genauso. Der 3D-Code ruft diese PDF-Funktion nicht auf.

## Offene Punkte / Risiken
- Loch plus Abrundung zusammen, und das Aushöhlen, sind in der Vorschau eine grobe Blocknäherung. Ein einzelnes Loch im Würfel und eine Abrundung am Würfel sind genau. In der Blender-Datei stehen die echten Modifikatoren (Bevel, Subdivision, Spiegel, Solidify, Boolean), damit man dort weiterarbeitet.
- Glätten in der Vorschau ist eine einfache Kantenrundung der Fläche. Blender bekommt Subdivision Surface.
- STL kommt in Millimetern an (Faktor 10). Eine Datei ohne Einheit wird als Millimeter gelesen.

## Nicht geprüft
- Kein echtes Blender auf diesem Rechner: kein Tisch in Blender geöffnet, kein Screenshot aus Blender, keine echte .blend-Datei geprüft. Das bleibt dem Integrator am PC.
- Kein laufendes Ollama. Der KI-Test übergibt die Antwort über die Testhook-Funktion.
- „Ordner zeigen“ startet hier den Ordner, nicht die Windows-Markierung im Explorer (`/select`).
- Kein Codesign, QA-017 und QA-019 nicht als bestanden gewertet. Status in `requirements.yaml` nicht angehoben.
