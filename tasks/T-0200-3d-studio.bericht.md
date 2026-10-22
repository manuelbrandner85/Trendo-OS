# T-0200 Bericht: 3D-Studio

**Auftrag:** Im Studio „3D-Modelle erstellen“ eigene Objekte aus Formen bauen, ansehen, speichern und das aktuelle Objekt in Blender öffnen. Blender nur nach einmaliger Frage einrichten.
**Branch:** `cursor/t-0200-3d-studio-64f6`

## Geänderte Dateien

- `src/Trendo.Core/Mesh3D.cs` – Punkte, Grundformen (Würfel, Kugel, Zylinder, Kegel, Torus, Ebene, Schrift), Abrunden, Glätten, Wand, Loch.
- `src/Trendo.Core/MeshIo.cs` – Lesen und Schreiben von OBJ, STL, GLB, GLTF, FBX (ASCII). Binäres FBX und BLEND nur über Blender. Vorschau als PNG.
- `src/Trendo.Core/Scene3D.cs` – Teileliste, Maße in Zentimetern, Rückgängig, Projekt-JSON, KI-Auftrag.
- `src/Trendo.Core/Studio3D.cs` – Blender-Umwandlung kann den Zielpfad nennen. Die alten Blender-Aufrufe bleiben.
- `src/Trendo.App/MainWindow.W4Studio3D.cs` – Studio-Seite: Formen, Maße, Werkzeuge, Vorschau, Datei, Export, Projekt, KI, Blender-Frage.
- `src/Trendo.App/MainWindow.axaml.cs` – Die Kachel öffnet das Studio auch ohne Blender. Im Projektregister gibt es „3D-Modell öffnen“, wenn `3d/szene.json` liegt.
- `src/Trendo.App/MainWindow.Look.cs` – Hilfetext für die Seite.
- `tests/Trendo.Core.Tests/Scene3DTests.cs` – Formen, Werkzeuge, Dateien, KI-JSON.
- `tests/Trendo.App.Tests/W4Studio3DUiTests.cs` – Ablauf in der Oberfläche.
- `tests/Trendo.App.Tests/UiTests.cs` – Headless-Zeichnen mit Skia, damit die Oberfläche ein Bild liefern kann.

Der Studioname bleibt „3D-Modelle erstellen“. Der Reiter bleibt „3D“. Blender steht weiter bei den benötigten Programmen der Kachel; die Seite selbst wartet nicht darauf.

## Was der Nutzer tun kann

- Form wählen (Würfel, Kugel, Zylinder, Kegel, Torus, Ebene, 3D-Schrift). Größe, Lage, Drehung und Farbe einstellen. Mehrere Teile, auswählen, ändern, löschen, rückgängig.
- Werkzeuge: Kanten abrunden, glätten, spiegeln, einer Fläche eine Wand geben, ein Loch bohren.
- Satz an die lokale KI. Der Vorschlag erscheint als Zahlen und wird erst nach „Vorschlag bauen“ zum Objekt. Ohne KI ist der Knopf aus, mit Grund. Offline bleibt die Installation aus.
- Vorschau im Studio, sechs Seiten (vorne, hinten, links, rechts, oben, schräg).
- Datei öffnen: Dialog für OBJ, STL, GLB, GLTF, FBX, BLEND. Eine Datei kann auch auf die Seite gezogen werden; derselbe Einleseweg wie der Dialog.
- Speichern: OBJ und FBX in der gewählten Einheit (Vorgabe Zentimeter), STL in Millimetern, GLB in Metern. BLEND und echtes FBX brauchen Blender. „Ordner zeigen“ öffnet den 3D-Ordner und nennt die letzte Datei.
- „In Blender öffnen“ schreibt das aktuelle Objekt als BLEND und startet genau diese Datei. Fehlt Blender, kommt eine Frage. „Später“ installiert nichts. „Ja, installieren“ nutzt die vorhandene Werkzeugprüfung und die Freigabe.
- „Im Projekt speichern“ schreibt `3d/szene.json` und `3d/modell.obj`. Aus dem Projektregister lässt sich das Modell wieder öffnen.

## Testergebnis

Linux-Box, .NET 10.0.402, `dotnet test -c Release`. Die Laufzeit schreibt hier „Passed!“ (englische SDK-Ausgabe).

| Projekt | Bestanden | Fehler | Übersprungen |
| --- | ---: | ---: | ---: |
| tests/Trendo.Core.Tests | 424 | 0 | 10 |
| tests/Trendo.App.Tests | 138 | 2 | 0 |
| tests/Trendo.Api.Tests | 14 | 0 | 0 |

Die 10 Übersprungenen in Core sind die Windows-Prüfungen (`WinRealTests`). Die 3D-Tests in Core (7) und App (6) sind in den grünen Zahlen enthalten.

Die zwei roten App-Tests gehören nicht zu diesem Auftrag:

- `SYS-03-P14` (Seite als PDF): Auf der Box liegt `google-chrome`, `pdfinfo` fehlt. Der Test läuft deshalb und scheitert an der A4-Prüfung. Einzellauf weiterhin rot, Laufzeit etwa 90 Sekunden.
- `X-06` Live-Suche bei GitHub: im Gesamtlauf leer, im Einzellauf danach grün. Hängt am Netz, nicht am 3D-Studio.

## Offene Punkte

- Auf dieser Box ist kein Blender installiert. „Ja, installieren“ wurde nicht gedrückt. Ein echtes BLEND und der Start von Blender mit dem Tisch sind hier nicht nachgewiesen. Das bleibt für den PC mit Blender.
- Ollama lief hier nicht. Der KI-Weg ist mit einer eingesetzten Antwort geprüft, nicht mit einem echten Modell.
- Ziehen einer Datei aus dem Explorer auf die Fläche ist im Code an denselben Einleseweg gehängt. Die kopflose Maus-Simulation hat das Ereignis nicht an den Handler geliefert. Der Dialog-Weg ist geprüft.
- FBX ohne Blender ist eine einfache ASCII-Datei für den Hin-und-her-Weg in Trendo. Ein FBX, das Blender selbst schreibt, entsteht nur, wenn Blender da ist.
- `requirements.yaml` ist nicht angefasst. QA-017, QA-019 und Codesignatur sind nicht als bestanden markiert.

## Nicht geprüft

- Echter Blender-Lauf auf einem Windows-PC (Tisch bauen, STL, in Blender öffnen).
- Echte Ollama-Antwort.
- Datei-Dialog des Betriebssystems (im Test ersetzt).
- Drag aus dem Explorer mit einer echten Maus.
- `pdfinfo` und die A4-Prüfung von SYS-03.
