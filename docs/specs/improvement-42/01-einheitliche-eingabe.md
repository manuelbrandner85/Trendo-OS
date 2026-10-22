# I42-01 Einheitliche Eingabe pro Studio

**Status:** zur Freigabe durch Manuel

**Quelle:** `docs/spec/IMPROVEMENT_42.md`, Abschnitt 3, Funktion 1. Wortlaut: „Einheitliche Eingabe pro Studio mit Anhängen, Sprache, Kontext und verlustfreier Verlaufshistorie.“

**Wörter:** Studio = ein Arbeitsbereich, zum Beispiel Briefe oder 3D. Auftrag = der Text im Feld. Anhang = eine Datei aus demselben Projekt. Laufdatei = die gespeicherte Fassung dieses Auftrags auf dem PC. Sprache (`Locale`) = die Sprache dieses einen Auftrags, zum Beispiel `de` für Deutsch.

## Ziel und Nutzen

In jedem Studio gibt die Person den Auftrag an derselben Stelle ein. Anhänge, Sprache und der bisherige Verlauf gehören zu diesem Auftrag und bleiben erhalten. Eine leere Fläche oder ein zweites Programm ist dafür nicht nötig.

## Nutzerablauf

1. Die Person öffnet ein Studio aus `src/Trendo.App/StudioCatalog.cs`.
2. Sie schreibt in das Feld „Was soll dieses Studio tun?“.
3. Sie kann Dateien aus demselben Projekt beilegen und die Sprache des Auftrags nennen.
4. Sie prüft den Auftrag. Der Verlauf dieses Auftrags bleibt in der Laufdatei, auch wenn sie später etwas ergänzt.
5. Fehlt das Projekt, bleibt der Auftrag stehen. Es entsteht keine Datei.

## Anforderungen

- Dieselbe Leiste gilt für jedes Studio, das der Fähigkeitenkatalog kennt (`CapabilityCatalog.Find`). Der Startbildschirm bleibt ohne diese Leiste. Im Code lässt `WithPromptBar` den Schlüssel `start` aus.
- Der gespeicherte Auftrag heißt `StudioPromptRequest`. Darin stehen Auftragsnummer, Projekt, Studio, Text, Anhänge, Sprache, eine gewünschte Aktion, der Datenschutzmodus und die Herkunft. Die Felder liegen in `src/Trendo.Core/PromptAction/Model.cs`.
- Ein Anhang hat einen Namen und einen Pfad innerhalb des Projekts (`PromptAttachment`). Ein Verweis auf einen Ordner außerhalb des Projekts beendet den Auftrag. Die Datei wird dann nicht gelesen (`StudioWorkflow.TakeAttachments`).
- Die Sprache ist das Feld `Locale`. Fehlt sie, setzt der Ablauf `de`. Das ist die Sprache dieses Auftrags. Language & Localization Studio 2.0 bleibt gesperrt und wird hier nicht gebaut.
- Der Kontext ist das Projekt und das Studio der Leiste. Ein anderer Projektordner liefert keinen Verlauf.
- Der Verlauf ist `RunState.History` (`HistoryEntry` mit Zeitpunkt und Text) in `src/Trendo.Core/PromptAction/RunStore.cs`. Eine neue Fassung löscht die älteren Einträge nicht.
- Die Beschriftung bleibt deutsch und alltagsnah. Zwei Navigationsebenen bleiben die Obergrenze.

## Betroffene Module

- `src/Trendo.App/MainWindow.PromptAction.cs` – Feld `auftrag-prompt`, Leiste `auftrag-leiste`
- `src/Trendo.App/MainWindow.Sys13.cs` – hängt `WithPromptBar` an die Seite
- `src/Trendo.App/MainWindow.W4Studio3D.cs` – dieselbe Leiste im linken Bereich der 3D-Seite (`BuildPromptBar("CORE-05")`)
- `src/Trendo.Core/PromptAction/Model.cs` – `StudioPromptRequest`, `PromptAttachment`, `Provenance`
- `src/Trendo.Core/PromptAction/StudioWorkflow.cs` – `Start`, `TakeAttachments`
- `src/Trendo.Core/PromptAction/RunStore.cs` – `auftraege/{id}/lauf.json`
- `src/Trendo.App/StudioCatalog.cs` – Namen der Studios
- `tests/Trendo.App.Tests/PromptBarTests.cs` – Leiste auf der Briefseite, keine Leiste auf dem Startbildschirm, Leiste auf der 3D-Seite

## Sicherheit und Datenschutz

- Lokal zuerst. Der Auftrag aus der Leiste trägt `PrivacyMode.Local`. Anhänge löst `ProjectStore.ResolveInProject` nur im Projekt auf.
- Cloud nur nach Opt-in. Diese Eingabe schickt den Text nicht von selbst an einen Anbieter.
- Kein kostenpflichtiger Dienst. Die Eingabe setzt kein Bezahl-Abo voraus.
- Keine Geheimnisse im Audit. `Finish` schreibt `prompt.phase` mit Studio, Aktion, Phase und Zeichenzahl, nicht den Auftragstext. Schlüssel bleiben in `src/Trendo.Core/Secrets.cs`.
- Freigabe über `src/Trendo.Core/ApprovalGate.cs`, sobald eine Wirkung nach außen ansteht. Das Absenden des Textes in der Leiste ist diese Freigabe nicht.
- Diese Funktion ist nicht Verbesserung 23, 25 oder 34 und nicht Language & Localization Studio 2.0. Punkt 86 steht im Repo nicht. Verbesserung 35: nie aktivieren.

## Abhängigkeiten

- Funktion 2 nennt das Studio, dem die Eingabe gehört.
- Funktion 11 speichert den Verlauf in der Laufdatei.
- Keine Abhängigkeit zum KI-Laden. Pull Request #252 ist dafür nicht erforderlich.

## Akzeptanzkriterien

- In einem Studio mit Katalogeintrag steht das Feld „Was soll dieses Studio tun?“. Der Startbildschirm hat dieses Feld nicht.
- Ein Anhang außerhalb des Projekts oder ein Verweis nach außen endet als Fehlschlag. Die Datei wird nicht gelesen, und es entsteht keine Ausgabedatei.
- Zwei Aufträge im selben Projekt behalten jeweils ihren eigenen Verlauf. Der ältere Text bleibt in `History`.
- Die Sprache des Auftrags steht in der Laufdatei. Ein fehlender Wert ist `de`. Daraus wird kein Language-2.0-Studio.
- Ohne Projekt nennt die Leiste, dass zuerst ein Projekt angelegt wird. Es gibt keine Scheindatei.

## Offene Fragen

- Die Leiste übergibt Anhänge als leere Liste und die Sprache fest als `de` (`StudioPromptRequest` in `MainWindow.PromptAction.cs`). Sollen Anhänge und Sprache in der Oberfläche wählbar sein?
- `RunState.History` steht in der Laufdatei. Die Leiste zeigt die Liste nicht. Reicht die Datei als Verlauf?

## Ist-Stand

Die Leiste hängt an den Studios, deren Seite über `WithPromptBar` läuft, und zusätzlich an der 3D-Seite. Der Startbildschirm bleibt frei. Anhänge und eine andere Sprache als `de` nimmt der Ablauf an, die Leiste schickt beides heute nicht. Der Verlauf wird beim Start mit dem ersten Text angelegt.
