# I43-09 Versionen und Rückgängig

**Status:** zur Freigabe durch Manuel

**Quelle:** `docs/spec/IMPROVEMENT_43.md`, Punkt 9. Wortlaut: „Versionen und Rückgängig für jede Änderung.“

## Ziel und Nutzen

Jede Änderung an einem Studio-Ergebnis bleibt als eigene Version liegen. „Rückgängig“ holt den Stand davor zurück. Die Person kann einen Schritt zurücknehmen, ohne die Datei aus dem Papierkorb zu suchen.

## Nutzerablauf

1. Die Person ändert ein Ergebnis (Folgeeingabe, Stil, „Jetzt beheben“ oder eine Fachänderung im Studio).
2. Trendo behält den vorherigen Stand.
3. Sie wählt „Rückgängig“.
4. Der Ordner oder die Datei entspricht wieder dem vorigen Stand. Die neuere Version bleibt in der Liste, bis sie einen anderen Stand auswählt.
5. Ein erneutes Ändern legt eine weitere Version an.

## Anforderungen

- Jede Änderung im Sinne der Quelle erzeugt einen wiederherstellbaren vorherigen Stand. Für die Webseite heißt das: Bau, `Revise`, `ApplyStyle` und `WebsiteQuality.Fix`.
- Rückgängig stellt Dateien wieder her. Ein reiner Texteintrag im Verlauf reicht nicht, wenn die Datei schon überschrieben ist.
- Versionen bleiben im Projektordner dieser Arbeit. Sie wandern nicht in ein anderes Projekt.
- Vorhandene Rückgängig-Wege bleiben gültig und werden nicht durch einen zweiten, abweichenden Mechanismus ersetzt:
  - Umbenennen: `src/Trendo.Core/Sys/Sys02.cs` (`Undo`)
  - Office-Dateien: `src/Trendo.Core/Sys/Sys10.cs` (Versionen vor dem Überschreiben)
  - Briefakten: `src/Trendo.Core/LetterCase.cs`
- Die Webseite hat diesen Stand heute nicht. `WebsiteStudio.Revise` schreibt `index.html`, `stil.css` und `inhalt.json` neu.

## Betroffene Module

- `src/Trendo.Core/Website/WebsiteStudio.cs` – `Revise`, `ApplyStyle` (überschreiben ohne Version)
- `src/Trendo.Core/Website/WebsiteQuality.cs` – `Fix`
- `src/Trendo.Core/Website/WebsiteHtml.cs` – `Write`
- `src/Trendo.Core/PromptAction/RunStore.cs` – Auftragstext-Verlauf, keine Dateiversion
- `src/Trendo.Core/Sys/Sys02.cs`
- `src/Trendo.Core/Sys/Sys10.cs`
- `src/Trendo.Core/LetterCase.cs`
- `src/Trendo.App/MainWindow.W4Web.cs` – „Änderung übernehmen“, „Farben übernehmen“, „Jetzt beheben“

Ein Speicherort für Web-Versionen ist in der Quelle nicht genannt. Diese Spezifikation legt keinen neuen Pfad fest.

## Sicherheit und Datenschutz

- Lokal zuerst. Versionen liegen im Projekt auf diesem PC.
- Cloud nur nach Opt-in. Versionen werden nicht in ein Konto synchronisiert.
- Kein kostenpflichtiger Dienst für die Historie.
- Keine Geheimnisse in den Versionsdateien. Enthält ein Stand keine Schlüssel, enthält die Kopie auch keine.
- Freigabe über `src/Trendo.Core/ApprovalGate.cs`: Rückgängig auf lokalen Entwurfsdateien ist keine Veröffentlichung. Das Löschen eines ganzen Projekts oder einer fremden Datei bleibt eine eigene Freigabe (Regeln in `docs/spec/IMPROVEMENT_42.md`, Abschnitt 7).
- Diese Funktion ist nicht Verbesserung 25 (Smart Migration). Sie verschiebt keine Daten auf ein anderes System. Sie ist nicht Verbesserung 23 oder 34 und nicht Language & Localization Studio 2.0. Punkt 86 steht im Repo nicht. Verbesserung 35: nie aktivieren.

## Abhängigkeiten

- Punkt 2 erzeugt die Folgeänderungen, die versioniert werden.
- Punkt 10 bleibt erfüllt: auch eine ältere Version ist eine echte Datei, kein nur behaupteter Stand.
- Keine Abhängigkeit zum KI-Laden (#252).

## Akzeptanzkriterien

- Nach einer Änderung der Webseite lässt sich der unmittelbar vorige `index.html`-Inhalt wiederherstellen.
- Zweimal „Rückgängig“ geht zwei Stände zurück, solange zwei ältere Versionen existieren.
- Der Umbenennen-Rückgängigweg in `Sys02.Undo` verhält sich nach dieser Arbeit wie vorher.
- Versionen eines Projekts sind in einem zweiten Projekt nicht lesbar.

## Offene Fragen

- Wie viele Versionen bleiben liegen, und wann werden sie gelöscht? Die Quelle sagt das nicht. Die 120-Stunden-Forensikregel aus Verbesserung 42 gilt für forensische Kopien, nicht automatisch für diese Entwurfsversionen. Die Frist wird hier nicht umgedeutet.
- Reicht „Rückgängig“ um einen Schritt, oder soll die Person jede ältere Version direkt wählen?
- Gelten Stilwechsel und „Jetzt beheben“ als „jede Änderung“, oder nur die Folgeeingabe? Diese Spezifikation zählt alle drei, weil die Quelle „jede Änderung“ sagt.

## Ist-Stand

`tasks/T-0430-webseite-profi.bericht.md` sagt ausdrücklich: Versionen und Rückgängig (Punkt 9) waren nicht Teil dieses Auftrags. Eine Änderung schreibt die erzeugten Dateien neu. Eigene Extra-Dateien im Ordner bleiben.
