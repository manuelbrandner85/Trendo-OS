# I42-11 Bearbeitbare Artefakte

**Status:** zur Freigabe durch Manuel

**Quelle:** `docs/spec/IMPROVEMENT_42.md`, Abschnitt 3, Funktion 11. Wortlaut: „Persistierte, editierbare Zwischen- und Endartefakte mit Projektbezug und Wiederaufnahme.“

**Wörter:** Artefakt = die erzeugte Datei im Projekt. Persistiert = sie liegt auf der Festplatte, nicht nur im Arbeitsspeicher. Bearbeitbar = sie lässt sich wieder öffnen und ändern. Projektbezug = die Datei gehört zu genau diesem Projekt. Wiederaufnahme = nach einem Neustart ist derselbe Auftrag noch da. Prüfsumme (SHA-256) = ein Fingerabdruck der Datei.

## Ziel und Nutzen

Was das Studio erzeugt, liegt als Datei im Projekt und lässt sich wieder öffnen. Der Auftrag merkt sich Pfad, Format, ob die Datei bearbeitbar ist, und die Fassung. Nach einem Abbruch oder Neustart ist der Stand derselbe wie in der Laufdatei.

## Nutzerablauf

1. Ein Adapter liefert `ProducedFile` mit relativem Pfad, Format und `Editable`.
2. `Accept` prüft, dass die Datei im Projekt existiert, und speichert ein `StudioArtifact`.
3. Die Laufdatei `auftraege/{id}/lauf.json` liegt im selben Projekt.
4. Die Person schließt Trendo und öffnet es wieder. `Resume` lädt denselben Stand.
5. Eine neuere Fassung der Laufdatei wird nicht angefasst. Eine ältere wird auf Fassung 1 gehoben, mit einer Warnung.

## Anforderungen

- `StudioArtifact` trägt `artifactId`, `projectId`, `format`, `relativePath`, `editability`, `producer`, `dependencies`, `sha256`, `version`, `previewRef`, `qaReportRef` (`Model.cs`).
- Der Pfad ist relativ. `ProjectStore.ResolveInProject` in `src/Trendo.Core/ProjectStore.cs` lehnt einen Ausbruch ab.
- `RunStore.Save` schreibt zuerst eine `.tmp`-Datei und benennt sie um. Vor dem Überschreiben entsteht `lauf.json.bak`.
- `Resume` setzt einen offenen `Executing`-Lauf nur fort, wenn das Wirkungsbuch keinen offenen Versuch hat. Sonst wird die Wirkung nicht ein zweites Mal gestartet (Funktion 18 und 20).
- `previewRef` und `qaReportRef` bleiben im Typ. `Accept` setzt beide heute auf null und legt Vorschau und QA daneben auf `RunState.Preview` und `RunState.Qa`.
- Bearbeitbar heißt `ProducedFile.Editable`. Die Leiste öffnet die Datei nicht in einem Editor.
- Die Datei muss wirklich existieren. Eine Meldung ohne Datei setzt „Die Datei fehlt:“ und `Failed`.

## Betroffene Module

- `src/Trendo.Core/PromptAction/Model.cs` – `StudioArtifact`, `ProducedFile`
- `src/Trendo.Core/PromptAction/RunStore.cs` – `Save`, `TryLoad`, `TryRollback`, `Rel`
- `src/Trendo.Core/PromptAction/StudioWorkflow.cs` – `Accept`, `Resume`, `Get`
- `src/Trendo.Core/ProjectStore.cs` – `ResolveInProject`, `ProjectIsolationException`
- `src/Trendo.App/MainWindow.PromptAction.cs` – zeigt `RelativePath` und die ersten 12 Zeichen von `Sha256`
- `tests/Trendo.Core.Tests/PromptActionTests.cs` – `Resume_claimed_sendet_nicht_nochmal`, `Rollback_der_Laufdatei`

## Sicherheit und Datenschutz

- Lokal zuerst. Artefakt und Laufdatei liegen unter dem Projektordner.
- Cloud nur nach Opt-in. Das Speichern lädt nichts hoch.
- Kein kostenpflichtiger Dienst.
- Keine Geheimnisse im Artefakt-Datensatz. `Sha256` ist die Prüfsumme der Datei, kein Schlüssel. Ablage der Schlüssel: `src/Trendo.Core/Secrets.cs`.
- Freigabe über `src/Trendo.Core/ApprovalGate.cs`. Ein gespeichertes Artefakt ist keine Veröffentlichung und kein Versand.
- Diese Funktion ist nicht Verbesserung 23, 25 oder 34 und nicht Language & Localization Studio 2.0. Punkt 86 steht im Repo nicht. Verbesserung 35: nie aktivieren.

## Abhängigkeiten

- Funktion 10 liefert die Datei. Ohne Adapter entsteht kein Artefakt.
- Funktion 16 erhöht `Version` bei einer Änderung.
- Funktion 19 verhindert, dass ein Pfad das Projekt verlässt.
- Keine Abhängigkeit zum KI-Laden. Pull Request #252 ist dafür nicht erforderlich.

## Akzeptanzkriterien

- Nach einem erfolgreichen `Produce` existiert die Datei unter dem relativen Pfad, und `lauf.json` nennt denselben Pfad und eine SHA-256.
- Ein relativer Pfad mit Ausbruch wirft `ProjectIsolationException` und endet als Fehlschlag.
- `Resume` auf einen `claimed`-Versuch sendet nicht noch einmal.
- `TryRollback` stellt die vorige `lauf.json` wieder her, wenn `lauf.json.bak` existiert.

## Offene Fragen

- `previewRef` und `qaReportRef` am Artefakt bleiben null. Sollen Vorschau und QA-Bericht eigene Dateien werden, oder reichen `RunState.Preview` und `RunState.Qa`?
- Die Leiste zeigt den Pfad und öffnet ihn nicht. Welches vorhandene Studio (Brief, 3D, Webseite) soll die Datei zum Bearbeiten öffnen?

## Ist-Stand

Laufdatei, Artefakt-Datensatz, `Resume` und Rollback der Laufdatei sind im Kern. Ohne registrierten Adapter schreibt die Produktion keine Fachdatei. Die Leiste hat keinen Editor.
