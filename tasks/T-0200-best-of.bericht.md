# 3D-Studio: das Beste aus #221 und #224

## Auftrag
PR #221 ist auf main. Entwurf #224 beschreibt dasselbe Studio noch einmal und kollidiert mit main. Es soll eine Implementierung bleiben. Übernommen wird, was #224 kann und main nicht oder schlechter kann.

## Branch
`cursor/3d-studio-best-of-e79e`

## Geänderte Dateien
- `src/Trendo.Core/MeshIO.cs` – FBX (ASCII), OBJ-Farben und Objekte, GLB mit Material und Normalen, glTF-Leser mit Schrittweite und Materialfarbe
- `src/Trendo.Core/Mesh3D.cs` – Ansichten hinten und links, schräge Ansicht mit Fluchtpunkt
- `src/Trendo.Core/ScenePlan.cs` – Auftrag an die KI: Mitte, Boden, höchstens 12 Teile; fehlende Höhe setzt die Mitte auf halbe Höhe
- `src/Trendo.Core/Studio3D.cs` – Blender-Umwandlung kann den Zielpfad nennen
- `src/Trendo.App/MainWindow.W4Studio3D.cs` – zwei Spalten, sechs Ansichten, Maßeinheit, FBX ohne Blender, änderbarer Vorschlag, OBJ im Projekt
- `src/Trendo.App/MainWindow.Look.cs` – Hilfetext der Seite
- `tests/Trendo.Core.Tests/Scene3DTests.cs`
- `tests/Trendo.App.Tests/W4Studio3DUiTests.cs`

Der Studioname bleibt „3D-Modelle erstellen“. `requirements.yaml` ist unverändert.

## Aus #224 übernommen, und warum

- **FBX ohne Blender.** #224 liest und schreibt eine ASCII-FBX. Main konnte FBX nur über Blender. Binäres FBX und Blend gehen weiter über Blender. Ist Blender da, schreibt der Export die FBX darüber, weil andere Programme die besser öffnen. Ohne Blender liegt trotzdem eine Datei im 3D-Ordner.
- **Farben und mehrere Objekte aus OBJ.** #224 liest die MTL-Farbe und trennt `o`/`g`. Main hat jede Datei grau und als ein Teil gelesen. Jedes Teil behält seine Lage, damit ein Tisch nicht ineinander rutscht.
- **GLB mit Material und Normalen, glTF robuster.** #224 schreibt je Farbe ein Material. Main hat die Farbe in gemeinsame Punkte geschrieben, der letzte Dreieck hat gewonnen. Der Leser versteht jetzt Schrittweite, Versatz, kurze Indizes, mehrere Speicher und `baseColorFactor`. Die Knoten-Matrix aus main bleibt.
- **Maßeinheit wählbar.** Automatisch, Millimeter oder Zentimeter. Automatisch lässt STL in Millimetern (wie der Export) und OBJ/FBX in Zentimetern. GLB bleibt Meter. #224 hatte die Wahl, die Vorgabe dort hat STL aber als Zentimeter gelesen und die eigene Druckdatei zehnfach vergrößert. Die Wahl ist drin, die Vorgabe bleibt die richtige.
- **Sechs Ansichten, Schräge mit Tiefe.** Vorne, hinten, links, rechts, oben, schräg. Die schräge Ansicht hat einen Fluchtpunkt. Main hatte vier flache Ansichten. Die gewählte Ansicht ist markiert.
- **KI-Vorschlag vor dem Bauen ändern.** Breite, Tiefe, Höhe und Lage stehen in Feldern. Der Auftrag sagt, dass z die Mitte ist, die Unterkante auf dem Boden steht und sich die Teile berühren, höchstens 12 Stück. Fehlt z, wird die Mitte auf die halbe Höhe gesetzt. Der Hinweis zum Torus aus main bleibt. Unbekannte Formen werden übersprungen, der Rest des Vorschlags bleibt nutzbar.
- **Hilfetext** auf der Seite 3D.
- **Vorschau neben den Teilen**, nicht erst unter dem langen Formular.
- **Blender-Umwandlung in eine genannte Datei**, damit ein Import nicht neben die Originaldatei schreibt.
- **Im Projekt** zusätzlich `3d/modell.obj` und `3d/modell.mtl`.
- **Zuletzt gespeichert** steht im Studio, „Ordner zeigen“ nennt die Datei.
- **Mehrere Dateien** im Dialog.

## Nicht übernommen, und warum

- Kein zweites Datenmodell. `ScenePart`, `Poly3D` und `MeshIo` aus #224 wären eine zweite Szene neben `Part3D` und `Mesh`. Es bleibt ein Weg.
- Die Werkzeuge aus #224 (Fase, Bohrung, Wand als Dicke, Glätten als Unterteilung) ersetzen nicht die vorhandenen. Main bohrt ein Loch im Würfel genau, rundet die Würfelkante und gibt Blender echte Modifikatoren (Abrunden, Boolean, Aushöhlen, Spiegel, Glätten). #224 hat das Netz festgebacken und Blender nur eine GLB gegeben. In Blender wären die Werkzeuge dann weg. Das ist für „genau dieses Objekt öffnen“ schlechter.
- Spiegeln bleibt der Modifikator, der das Teil über die nahe Seite verdoppelt. #224 hat eine Kopie durch den Weltursprung gelegt. Die liegt oft auf dem Original.
- „Aushöhlen“ bleibt. #224 hat daraus „einer Fläche eine Wand geben“ gemacht. Der Auftrag und der Test meinen die hohle Mitte.
- Der Seitentitel bleibt „3D-Modelle erstellen“. #224 hat ihn in „3D-Studio“ geändert. Studionamen kommen aus dem Katalog.
- „Später“ bei der Blender-Frage kehrt ins 3D-Studio zurück. #224 hat das entfernt und immer die Studio-Liste geöffnet. Die Pfadfelder der Seite bleiben.
- Die Blender-Frage bleibt die vorhandene Seite für Zusatzprogramme. Den Kopfhörer-Zeichner von #224 (Skia in jedem UI-Test) braucht die Vorschau hier nicht.

Entwurf #224 kann nach dem Merge dieses Stands geschlossen werden. Diese Änderung schließt ihn nicht.

## Testergebnis
`dotnet test Trendo.sln -c Release`, SDK 10:

- Trendo.Core.Tests: Bestanden 444, übersprungen 10, Fehler 0, gesamt 454. Dauer 8 s. Die 10 Übersprungenen sind die Windows-Prüfungen. Die 3D-Tests sind darin, darunter FBX, Farbe und die sechs Ansichten.
- Trendo.Api.Tests: Bestanden 14, Fehler 0, gesamt 14. Dauer 696 ms.
- Trendo.App.Tests: Bestanden 183, Fehler 1, gesamt 184. Dauer 5 min 33 s. Die 3D-Oberflächentests sind in den grünen Zahlen, inklusive FBX-Export ohne Blender.

Der eine Fehler ist `X-06` Live-Suche bei GitHub (`W4OssUiTests`, die Liste kam leer zurück). Ein zweiter Lauf nur dieses Tests war grün. Er hängt am Netz, nicht am 3D-Studio. SYS-03-P14 war in diesem Lauf nicht rot.

## Offene Punkte / Risiken
- ASCII-FBX ist der Hin-und-her-Weg in Trendo. Eine FBX, die andere Programme sicher öffnen, schreibt Blender, wenn es da ist.
- Loch plus Abrundung und das Aushöhlen bleiben in der Vorschau die bisherige Näherung. In der Blender-Datei stehen die echten Modifikatoren.
- `requirements.yaml` ist nicht angefasst. QA-017, QA-019 und Codesignatur sind nicht als bestanden markiert.

## Nicht geprüft
- Kein echtes Blender und kein echtes Ollama auf diesem Rechner.
- Kein Ziehen aus dem Explorer mit einer echten Maus. Der Ablage-Weg ist derselbe wie der Dialog.
- Die kopflose Oberfläche zeigt das Studio mit einem Würfel und der schrägen Vorschau. Ein echter Klick mit der Maus ist hier nicht gelaufen.
