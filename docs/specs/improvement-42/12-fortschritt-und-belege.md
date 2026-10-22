# I42-12 Fortschritt und Belege

**Status:** zur Freigabe durch Manuel

**Quelle:** `docs/spec/IMPROVEMENT_42.md`, Abschnitt 3, Funktion 12. Wortlaut: „Wahrheitsgemäßer Fortschritt, Schritte, Tool-Ergebnisse, Protokolle und Dateinachweise, keine erfundenen Prozente.“

**Wörter:** Beleg = der Nachweis, dass ein Schritt wirklich gelaufen ist oder eine Datei wirklich existiert. Fortschritt = der aktuelle Stand. Eine Prozentzahl, die niemand gemessen hat, zählt nicht. Protokoll = eine kurze Liste der Schritte, ohne den vollen Auftragstext. Tool-Ergebnis = das, was ein Werkzeug tatsächlich zurückgegeben hat.

## Ziel und Nutzen

Die Person sieht, wo der Auftrag steht, und nur das, was wirklich passiert ist. Schritte kommen aus dem Lauf. Eine Prozentzahl, die niemand gemessen hat, erscheint nicht. Eine Datei gilt erst als da, wenn sie auf der Platte liegt.

## Nutzerablauf

1. Vor dem Lauf steht „Noch nicht gestartet“.
2. Während die Prüfung läuft, zeigt die Leiste einen Status aus `StatusText.German`.
3. Jeder Schritt landet als `StepEvidence` mit Kennung, Bezeichnung, ob er gelaufen ist, einem Satz und einem Zeitpunkt.
4. Dateien erscheinen mit Pfad und dem Anfang der SHA-256. Ein Beleg einer Wirkung erscheint als „Beleg:“.
5. Ein Schritt, der nicht gelaufen ist, wird nicht als erledigt geführt. `Mark` schreibt nur Schritte, die der Ablauf wirklich betreten hat.

## Anforderungen

- Deutsche Stati aus `StatusText.German`: „Noch nicht gestartet“, „Rückfrage“, „Freigabe nötig“, „Wird wirklich ausgeführt“, „Datei erstellt“, „QA bestanden“, „Fehlgeschlagen“, „Abgebrochen“, „Nicht unterstützt“. Das sind die Worte aus Abschnitt 8 der Quelle, plus „Abgebrochen“.
- `PlanStep` hat Kennung, Bezeichnung und ob der Schritt eine Wirkung ist. Der Plan setzt „Lokal erstellen“, „Prüfen“ und bei einer Wirkung „Wirkung nur nach Freigabe“.
- Es gibt keine Prozentanzeige in der Leiste und kein fest eingebautes Fortschrittsbalken-Ziel.
- `src/Trendo.Core/Progress.cs` ist der Bau-Fortschritt aus `requirements.yaml`. Er ist nicht der Fortschritt eines Auftrags. Diese Funktion liest ihn nicht als Laufanzeige.
- Das Audit `prompt.phase` nennt Studio, Aktion, Phase und Zeichenzahl.
- Eine Datei ohne `File.Exists` ist kein Beleg. `Accept` setzt dann „Die Datei fehlt:“.
- Die Leiste setzt den Text „Wird wirklich ausgeführt“, bevor `Start` zurückkommt (`check.Click`). Danach ersetzt `ShowResult` den Text durch den Status des Ergebnisses.

## Betroffene Module

- `src/Trendo.Core/PromptAction/Model.cs` – `PlanStep`, `StepEvidence`, `StatusText`
- `src/Trendo.Core/PromptAction/StudioWorkflow.cs` – `Mark`, `Finish`
- `src/Trendo.Core/AuditLog.cs`
- `src/Trendo.App/MainWindow.PromptAction.cs` – `auftrag-status`, `auftrag-detail`
- `src/Trendo.Core/Progress.cs` – anderer Fortschritt, nicht die Auftragskette
- `tests/Trendo.App.Tests/PromptBarTests.cs` – Anfangstext „Noch nicht gestartet“

## Sicherheit und Datenschutz

- Lokal zuerst. Belege liegen in der Laufdatei im Projekt.
- Cloud nur nach Opt-in. Die Anzeige sendet den Status nicht weg.
- Kein kostenpflichtiger Dienst. Eine externe Fortschritts-API ist keine Voraussetzung.
- Keine Geheimnisse im Schritttext. `Finish` schreibt den Auftragstext nicht ins Audit. Ablage: `src/Trendo.Core/Secrets.cs`.
- Freigabe über `src/Trendo.Core/ApprovalGate.cs`. Der Status „Freigabe nötig“ führt die Wirkung nicht aus.
- Diese Funktion ist nicht Verbesserung 23, 25 oder 34 und nicht Language & Localization Studio 2.0. Punkt 86 steht im Repo nicht. Verbesserung 35: nie aktivieren.

## Abhängigkeiten

- Funktion 10 erzeugt die Schritte „Erstellen“ und, nach Freigabe, „Ausgeführt“.
- Funktion 11 liefert den Dateinachweis.
- Keine Abhängigkeit zum KI-Laden. Pull Request #252 ist dafür nicht erforderlich.

## Akzeptanzkriterien

- Die Leiste zeigt keinen Prozentwert.
- Nach einem Lauf steht einer der neun Sätze aus `StatusText.German`, passend zur `WorkflowPhase`.
- `StepEvidence` enthält nur Phasen, die `Mark` betreten hat. Eine übersprungene Wirkung hat keinen Eintrag „Ausgeführt“.
- Ein fehlender Dateipfad steht als Fehler, nicht als „Datei erstellt“.

## Offene Fragen

- Die Leiste setzt „Wird wirklich ausgeführt“, noch bevor `Start` einen Plan hat. Soll der erste sichtbare Satz „Noch nicht gestartet“ bleiben, bis `Mark` die Phase `Executing` schreibt?
- `PlanStep` steht in der Laufdatei. Die Leiste zeigt die Schrittliste nicht einzeln, nur Status, Fragen, Fehler, Warnungen, Vorschau und Pfade. Soll die Liste selbst sichtbar sein?

## Ist-Stand

Statussätze, `StepEvidence` und das Audit sind im Kern. Die Leiste zeigt den deutschen Status und die Detailzeilen. Eine Prozentanzeige gibt es nicht. `Progress.cs` bleibt der getrennte Bau-Fortschritt.
