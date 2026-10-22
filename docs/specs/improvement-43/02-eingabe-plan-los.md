# I43-02 Eingabe: Plan, „Los“, Schritte, Folgeeingabe

**Status:** zur Freigabe durch Manuel

**Quelle:** `docs/spec/IMPROVEMENT_43.md`, Punkt 2. Wortlaut: „Eingabefeld wie Claude Code: Plan zuerst, „Los“, sichtbarer Schritt-für-Schritt-Bau, gezielte Änderungen per Folgeeingabe.“

## Ziel und Nutzen

Die Person beschreibt in einem Satz, was das Studio tun soll. Trendo zeigt zuerst den Plan. Erst „Los“ startet den Bau. Die Schritte sind dabei sichtbar. Eine spätere Eingabe ändert das vorhandene Ergebnis gezielt, statt alles neu und ungefragt zu überschreiben.

## Nutzerablauf

1. Im Studio steht ein Eingabefeld für den Auftrag.
2. Trendo legt einen Plan vor: welche Schritte, welche Datei, ob eine Freigabe nötig ist. Noch läuft keine Wirkung.
3. Die Person wählt „Los“.
4. Jeder Schritt erscheint mit einem ehrlichen Stand. Ein Schritt, der nicht lief, wird nicht als erledigt gezeigt.
5. Sie schreibt eine Folgeeingabe. Trendo ändert nur den genannten Teil und zeigt Plan und Ergebnis erneut.

## Anforderungen

- Der Plan erscheint vor dem Bau. „Los“ ist die ausdrückliche Bestätigung für den lokalen Bau.
- Schritte kommen aus dem realen Lauf (`PlanStep` in `src/Trendo.Core/PromptAction/Model.cs`), nicht aus einer fest eingebauten Prozentanzeige.
- Eine Folgeeingabe bezieht sich auf dasselbe Projekt und dasselbe Ergebnis. Der Verlauf bleibt erhalten (`HistoryEntry` in `src/Trendo.Core/PromptAction/RunStore.cs`).
- Wirkt ein Schritt nach außen (Veröffentlichen, Senden, Zahlen, Gerät, externe KI), bleibt er stehen, bis `ApprovalGate` die eine Freigabe erteilt hat.
- Fehlt ein Adapter für das Studio, endet der Lauf als nicht unterstützt. Es entsteht keine Scheindatei.
- Die Beschriftung ist deutsch und alltagsnah. Der Quelltext des Punkts nennt den Knopf „Los“.

## Betroffene Module

- `src/Trendo.App/MainWindow.PromptAction.cs` – Leiste, Feld „Was soll dieses Studio tun?“, Knopf „Auftrag prüfen“
- `src/Trendo.Core/PromptAction/StudioWorkflow.cs` – Plan, Validierung, Verlauf, Freigabeprüfung
- `src/Trendo.Core/PromptAction/Model.cs` – `PlanStep`, `RunStatus`, deutsche Statustexte
- `src/Trendo.Core/PromptAction/RunStore.cs` – gespeicherter Verlauf
- `src/Trendo.Core/PromptAction/IntelligencePromptModel.cs` – Hülle um `IntelligenceCore.AskAsync`, `PromptActions.CreateDefault()` liefert eine leere Adapterliste
- `src/Trendo.Core/PromptAction/PromptFields.cs`, `src/Trendo.Core/W2/Prompts.cs` – vorhandene Vorlagen
- `src/Trendo.App/MainWindow.W4Web.cs` – eigenes Feld „Änderung übernehmen“, getrennt von der Auftragsleiste
- `src/Trendo.Core/Website/WebsiteStudio.cs` – `Revise` für Überschrift, Untertitel, Farbe `#RRGGBB`, sonst ein zusätzlicher Abschnitt

## Sicherheit und Datenschutz

- Lokal zuerst. `StudioPromptRequest` geht mit `PrivacyMode.Local` aus der Leiste (`MainWindow.PromptAction.cs`).
- Cloud nur nach Opt-in über `src/Trendo.Core/PromptAction/ConsentStore.cs`. Die Leiste zeigt den Satz „Cloud-KI ist aus.“ `IntelligencePromptModel.CompleteAsync` liefert null, sobald `IsLocal` falsch ist. `IsLocal` kommt von `BackendsAreLocal` und `Loopback`.
- Kein kostenpflichtiger Dienst. Ein Bezahlmodell wird nicht vorausgesetzt.
- Keine Geheimnisse im Plan. Das Audit von `IntelligenceCore.AskAsync` speichert Länge, Quelle und Modell, nicht den Fragetext. Der lokale Verlauf `ki/verlauf.jsonl` speichert Frage und Antwort auf dem PC (`IntelligenceCore.Save`). Schlüssel aus `src/Trendo.Core/Secrets.cs` gehören dort nicht hinein.
- Freigabe über `src/Trendo.Core/ApprovalGate.cs`. Der Knopf „Einmal freigeben“ erscheint nur bei `RunStatus.AwaitingApproval`. Ein „Los“ ersetzt diese Freigabe nicht. `StudioWorkflow` verwirft Felder, die eine Freigabe vortäuschen (`ForbiddenKeys`).
- Diese Funktion ist nicht Verbesserung 23, 25 oder 34 und nicht Language & Localization Studio 2.0. Punkt 86 steht im Repo nicht. Verbesserung 35: nie aktivieren.

## Abhängigkeiten

- Die lokale Modellwahl und das Laden der Modelle sind der KI-Laden, Pull Request #252 (Entwurf). Das gilt erst nach Merge von #252.
- Punkt 1 zeigt das Ergebnis neben dieser Eingabe.
- Punkt 9 behält die Stände, die eine Folgeeingabe erzeugt.
- Punkt 11 legt diese Eingabe in den Einfach-Modus.
- Verbesserung 42 (`docs/spec/IMPROVEMENT_42.md`, Code unter `src/Trendo.Core/PromptAction/`) ist die vorhandene Auftragskette. Punkt 2 ergänzt sie um Plan, „Los“ und sichtbare Schritte. Er ersetzt die Kette nicht.

## Akzeptanzkriterien

- Vor „Los“ liegt ein Plan vor, und es wurde keine Datei geschrieben und nichts gesendet.
- Nach „Los“ sieht die Person die Schritte in der Reihenfolge des Laufs. Ein fehlender Adapter ergibt den Status „Nicht unterstützt“ (`StatusText.German`).
- Eine Folgeeingabe ändert das benannte Ergebnis im selben Projekt. Der ältere Auftragstext bleibt im Verlauf.
- Ohne Opt-in verlässt der Auftrag den PC nicht.
- Eine Folgeeingabe, die veröffentlichen oder senden will, bleibt bei „Freigabe nötig“.

## Offene Fragen

- Der vorhandene Knopf heißt „Auftrag prüfen“, die Quelle sagt „Los“. Welches Wort steht auf dem Knopf?
- `PromptActions.CreateDefault()` ist leer. Welche Studios bekommen den ersten echten Adapter, und bleibt die Webseite dabei bei `WebsiteStudio.ActionBuild` / `ActionRevise`?
- `WebsiteStudio.Revise` versteht drei Satzmuster und hängt sonst einen Abschnitt an. Soll die Folgeeingabe von Punkt 2 diese Methode ersetzen oder nur aufrufen?

## Ist-Stand

Die Leiste prüft den Auftrag und kann Rückfrage und Freigabe zeigen. Einen Knopf „Los“ und einen sichtbaren Schritt-für-Schritt-Bau gibt es in der Oberfläche nicht. Ohne registrierten Adapter endet der Lauf, bevor eine Fachdatei entsteht.
