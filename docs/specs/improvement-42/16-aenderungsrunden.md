# I42-16 Änderungsrunden

**Status:** zur Freigabe durch Manuel

**Quelle:** `docs/spec/IMPROVEMENT_42.md`, Abschnitt 3, Funktion 16. Wortlaut: „Gezielt änderbare Artefakte, Diff/Versionen, Undo/Redo nach realer Unterstützung, QA nach Änderungen erneut ausführen.“

**Wörter:** Änderungsrunde = derselbe Auftrag wird gezielt geändert, nicht still durch einen neuen ersetzt. Fassung (Version) = die Nummer dieser Änderung. Diff = ein Vergleich alt gegen neu. Den gibt es nur, wo der Code ihn wirklich kann. Undo = rückgängig. Redo = dieselbe Änderung noch einmal vorwärts. Beides nur bei vorhandener Unterstützung. QA = die Prüfung läuft nach der Änderung erneut.

## Ziel und Nutzen

Die Person ändert einen vorhandenen Auftrag, ohne den alten Text zu verlieren. Die Fassung steigt. Die Prüfung läuft danach erneut. Rückgängig gibt es nur dort, wo der Code es wirklich kann: die vorige Laufdatei. Ein Undo, das es nicht gibt, wird nicht angezeigt.

## Nutzerablauf

1. Sie gibt einen neuen Text für denselben Auftrag.
2. `Revise` hängt den Text an `History`, erhöht `Version`, leert Parameter, Freigabe, Artefakte, QA und Vorschau und prüft neu.
3. Die alte Laufdatei liegt als `lauf.json.bak`, solange `Save` vorher kopiert hat.
4. `TryRollback` kopiert die `.bak` zurück, wenn sie da ist.
5. Die QA von Funktion 13 und 14 läuft auf der neuen Fassung erneut, sobald ein Adapter `Produce` erreicht.

## Anforderungen

- `Revise` läuft nicht während `Executing`. In den anderen Phasen startet es `RevisionRequested` und danach `Validate`.
- `History` behält jeden bisherigen Prompt. `Version` beginnt bei 1 und steigt um eins.
- Eine neue Fassung verwirft `ApprovalId`, `IdempotencyKey` und `SideEffectReceipt`. Eine alte Freigabe gilt nicht für den neuen Text.
- Undo der Laufdatei ist `RunStore.TryRollback`. Gibt es keine `.bak`, gibt die Methode falsch zurück und ändert nichts.
- Ein Datei-Diff der Artefakte ist im Auftragskern nicht vorhanden. Die Quelle verlangt Diff und Undo nur nach realer Unterstützung. Solange kein Diff-Code da ist, zeigt die Oberfläche keinen Diff-Knopf.
- Redo über eine zweite Sicherung ist nicht vorhanden. Es wird keiner behauptet.
- Die Leiste hat keinen Knopf, der `Revise` aufruft. „Antwort übernehmen“ ruft `Answer` auf.
- Die Webseite hat einen eigenen Weg `WebsiteStudio.Revise` in `src/Trendo.Core/Website/WebsiteStudio.cs` (Überschrift, Untertitel, Farbe `#RRGGBB`, sonst ein zusätzlicher Abschnitt). Das ist nicht `StudioWorkflow.Revise`.

## Betroffene Module

- `src/Trendo.Core/PromptAction/StudioWorkflow.cs` – `Revise`
- `src/Trendo.Core/PromptAction/RunStore.cs` – `History`, `Version`, `TryRollback`, `lauf.json.bak`
- `src/Trendo.App/MainWindow.PromptAction.cs` – kein Revisionsknopf
- `src/Trendo.Core/Website/WebsiteStudio.cs` – `Revise` der Webseite, getrennt vom Auftrag
- `src/Trendo.App/MainWindow.W4Web.cs` – „Änderung übernehmen“
- `tests/Trendo.Core.Tests/PromptActionTests.cs` – `Revision_behaelt_Verlauf`, `Rollback_der_Laufdatei`

## Sicherheit und Datenschutz

- Lokal zuerst. Fassung und Verlauf bleiben im Projekt.
- Cloud nur nach Opt-in. Eine Änderung lädt die alte Fassung nicht hoch.
- Kein kostenpflichtiger Dienst.
- Keine Geheimnisse im Verlauf. Der Prompt-Text steht in der Laufdatei auf dem PC, nicht im Audit. Ablage der Schlüssel: `src/Trendo.Core/Secrets.cs`.
- Freigabe über `src/Trendo.Core/ApprovalGate.cs`. `Revise` löscht die alte Freigabe-Kennung. Die neue Wirkung braucht eine neue Freigabe.
- Diese Funktion ist nicht Verbesserung 23, 25 oder 34 und nicht Language & Localization Studio 2.0. Punkt 86 steht im Repo nicht. Verbesserung 35: nie aktivieren.

## Abhängigkeiten

- Funktion 11 speichert die Laufdatei, die `TryRollback` zurückholt.
- Funktion 13 und 14 laufen nach der neuen Fassung erneut, sobald `Produce` sie erreicht.
- Keine Abhängigkeit zum KI-Laden. Pull Request #252 ist dafür nicht erforderlich.

## Akzeptanzkriterien

- `Revise` behält den älteren Prompt in `History` und erhöht `Version`.
- Die alte Freigabe-Kennung ist danach null. Ein erneuter Seiteneffekt braucht eine neue Freigabe.
- `TryRollback` stellt die vorige Laufdatei nur her, wenn `lauf.json.bak` existiert.
- Es gibt keinen Knopf „Diff“ oder „Redo“, solange kein Code dafür vorhanden ist.

## Offene Fragen

- Die Leiste ruft `Revise` nicht auf. Soll ein eigener Knopf denselben Auftrag ändern, oder reicht vorerst ein neuer Auftrag?
- `WebsiteStudio.Revise` und `StudioWorkflow.Revise` heißen gleich und tun verschiedenes. Soll die Webseite bei einem Auftrag den Workflow rufen?

## Ist-Stand

`Revise`, Verlauf und Rollback der Laufdatei sind im Kern und durch Tests belegt. Die Leiste bietet die Änderung nicht an. Einen Artefakt-Diff gibt es nicht. Die Webseiten-Änderung bleibt ihr eigener Weg.
