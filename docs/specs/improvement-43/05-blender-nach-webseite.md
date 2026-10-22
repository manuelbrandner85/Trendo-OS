# I43-05 Blender nach Webseite (GLB)

**Status:** zur Freigabe durch Manuel

**Quelle:** `docs/spec/IMPROVEMENT_43.md`, Punkt 5. Wortlaut: „Blender → Webseite: GLB aus 3D-Studio/Blender drehbar oder als Scroll-Animation.“

## Ziel und Nutzen

Ein Modell aus dem 3D-Studio oder aus Blender erscheint auf der Webseite. Die Person sieht es drehbar oder als Bewegung beim Scrollen. Die Datei auf der Seite ist eine GLB, die wirklich aus diesem Modell kommt.

## Nutzerablauf

1. Die Person hat ein Modell im 3D-Ordner oder wählt eine Datei (GLB, glTF oder eine Datei, die Blender wandeln kann).
2. Sie wählt „Drehen“ oder „Beim Scrollen“.
3. Sie baut die Seite.
4. Die Seite bindet die GLB ein. Die Meldung sagt, woher die Datei kommt.
5. Fehlt Blender und die Datei ist keine GLB/glTF, bleibt das Modell draußen und der Satz sagt das.

## Anforderungen

- GLB und glTF werden kopiert, nicht neu erfunden (`WebsiteModels.Place` in `src/Trendo.Core/Website/WebsiteFiles.cs`).
- Andere Formate laufen über den vorhandenen Blender-Adapter `src/Trendo.Core/Studio3D.cs` (Export nach `.glb` ist dort vorgesehen).
- Zwei Bewegungen, wie die Quelle sie nennt: drehbar oder Scroll-Animation. Im Code: `drehen` und `scroll` (`WebsitePlan.Motion`, Liste in `MainWindow.W4Web.cs`).
- Ohne Blender und ohne GLB/glTF entsteht keine Ersatzdatei, die als Blender-Export gilt.
- Ein Beispielkörper erscheint nur, wenn die Person ihn ausdrücklich verlangt, und er ist als Beispiel beschriftet (`WebsiteBrief.ExampleCube`, `WebsiteContent.ExampleModel`).

## Betroffene Module

- `src/Trendo.Core/Studio3D.cs` – Formate inklusive `.glb`, `Preview`, `ExportBlend`
- `src/Trendo.Core/Website/WebsiteFiles.cs` – `WebsiteGlb`, `WebsiteModels`
- `src/Trendo.Core/Website/WebsiteHtml.cs` – `NeedsThree`
- `src/Trendo.Core/Website/WebsiteModels.cs` – `ModelFile`, `ModelMotion`, `ModelNote`, `ExampleModel`
- `src/Trendo.Core/WebAssets/trendo-three.js`, `src/Trendo.Core/WebAssets/trendo-web3-entry.js`
- `src/Trendo.App/MainWindow.W4Web.cs` – „GLB aus dem 3D-Ordner“, Bewegung, Beispielwürfel
- `src/Trendo.App/MainWindow.W4Studio3D.cs` – 3D-Studio, Quelle der GLB im Ordner `Folder3D`
- `src/Trendo.App/StudioCatalog.cs` – „3D-Modelle erstellen“ (`CORE-05`) und „Webseite bauen“ (`CORE-07`)

## Sicherheit und Datenschutz

- Lokal zuerst. Blender startet nur auf diesem PC und nur für die Wandlung der gewählten Datei.
- Cloud nur nach Opt-in. Das Modell wird nicht zu einem Konvertierungsdienst geschickt.
- Kein kostenpflichtiger Dienst. Blender bleibt das vorhandene lokale Programm. Trendo installiert es nur nach Zustimmung (Hinweistext in `MainWindow.Look.cs` zur 3D-Seite).
- Keine Geheimnisse im GLB-Pfad, der im Audit oder in `inhalt.json` landet. Der Pfad bleibt im Projekt.
- Freigabe über `src/Trendo.Core/ApprovalGate.cs`: die Wandlung schreibt eine lokale Datei und veröffentlicht sie nicht. Ein Upload des Modells wäre Punkt 10 und braucht die Freigabe.
- Diese Funktion ist nicht Verbesserung 23, 25 oder 34 und nicht Language & Localization Studio 2.0. Punkt 86 steht im Repo nicht. Verbesserung 35: nie aktivieren.

## Abhängigkeiten

- Punkt 4, Technik „3D im Browser“, zeigt die GLB.
- Keine Abhängigkeit zum KI-Laden (#252).
- Blender muss für Nicht-GLB-Dateien installiert sein. Sonst gilt der ehrliche Hinweis.

## Akzeptanzkriterien

- Eine vorhandene `.glb` oder `.gltf` liegt nach dem Bau unter dem Seitenordner, und `inhalt.json` nennt `modelFile`.
- `modelMotion` ist `drehen` oder `scroll`, passend zur Auswahl.
- Ohne Blender und ohne GLB/glTF enthält die Seite kein Modell und `modelNote` erklärt das. Es gibt keine Datei, die als Export aus Blender ausgegeben wird.
- Der Beispielwürfel entsteht nur bei gesetztem `ExampleCube` und ist über `ExampleModel` als Beispiel erkennbar.

## Offene Fragen

- Die Quelle nennt den Beispielwürfel nicht. Die Checkbox „Beispielwürfel, wenn keine Datei da ist“ steht in `MainWindow.W4Web.cs`. Soll sie bleiben?
- Auf dem Linux-Image von T-0430 war Blender nicht installiert. Die Wandlung einer `.blend` ist dort nicht gelaufen. Wer prüft sie auf einem PC mit Blender?

## Ist-Stand

Kopieren, Hinweis ohne Blender und die beiden Bewegungen sind in `WebsiteModels` und der Web-Fläche. Ein ausgeführter Blender-Export auf diesem Stand ist im Bericht T-0430 nicht belegt.
