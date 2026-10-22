# I42-06 Vorlagen aus der PromptLibrary

**Status:** zur Freigabe durch Manuel

**Quelle:** `docs/spec/IMPROVEMENT_42.md`, Abschnitt 3, Funktion 6. Wortlaut: „Bestehende Promptvorlagen und Fachinstruktionen referenzieren und versioniert anwenden.“

**Wörter:** Vorlage = ein gespeicherter Text mit Lücken, zum Beispiel `{{Empfänger}}`. PromptLibrary = die vorhandene Sammlung dieser Vorlagen. Es entsteht keine zweite Sammlung. Fassung = welche Version der Vorlage benutzt wurde. Platzhalter = die Lücke im Text, die noch gefüllt werden muss.

## Ziel und Nutzen

Die Person nennt eine vorhandene Vorlage. Trendo füllt die Platzhalter mit den Angaben aus dem Auftrag, merkt sich Fassung und Kennung und fragt nur die Platzhalter, die leer sind. Eine zweite Vorlagensammlung entsteht nicht.

## Nutzerablauf

1. Sie schreibt `Vorlage:` und die Kennung, zum Beispiel `brief-formulieren`.
2. Trendo lädt die Vorlage aus der vorhandenen Bibliothek.
3. Platzhalter `{{Name}}` werden aus den Feldern des Auftrags gefüllt.
4. Fehlt ein Wert, erscheint eine Frage „Was soll bei „Name“ stehen?“.
5. Sind alle da, wird der gefüllte Text der Auftragstext. Fassung und Kennung stehen in der Laufdatei.

## Anforderungen

- Die Bibliothek ist `PromptLibrary` in `src/Trendo.Core/W2/Prompts.cs`. Der Ablauf legt keine zweite an (`StudioWorkflow` erhält dieselbe `PromptLibrary`).
- `ApplyTemplate` liest das Feld `vorlage`, holt `Get`, setzt `TemplateId` und `TemplateVersion`.
- `PromptLibrary.Variables` und `PromptLibrary.Fill` füllen den Text. Fehlende Namen werden `ClarificationQuestion`.
- Eine unbekannte Kennung ergibt die Warnung „Die Vorlage „…“ gibt es nicht.“ Der übrige Auftrag läuft weiter, ohne einen Ersatztext zu erfinden.
- Die mitgelieferte Vorlage `brief-formulieren` hat Fassung 1 und den Hinweis, keine Fakten zu erfinden.
- `PromptLibrary.Check` bleibt die vorhandene Prüfung des Vorlagentextes (Länge, Passwort, offener Platzhalter). Der Auftragskern ruft sie in `ApplyTemplate` nicht auf.
- Studio-Namen und Fachtexte bleiben bei den vorhandenen Modulen. Diese Funktion kopiert sie nicht in 54 eigene Prompt-Dateien.

## Betroffene Module

- `src/Trendo.Core/W2/Prompts.cs` – `PromptLibrary`, `PromptVersion`, `Fill`, `Variables`, `Check`
- `src/Trendo.Core/PromptAction/StudioWorkflow.cs` – `ApplyTemplate`
- `src/Trendo.Core/PromptAction/RunStore.cs` – `TemplateId`, `TemplateVersion`
- `src/Trendo.App/MainWindow.Prompts.cs` – vorhandene Oberfläche der Vorlagen
- `tests/Trendo.Core.Tests/PromptActionTests.cs` – `Vorlage_fragt_fehlende_Felder`
- `tests/Trendo.Core.Tests/W2Tests.cs` – `Fill`, `Check`, `Master`

## Sicherheit und Datenschutz

- Lokal zuerst. Vorlagen liegen im Datenordner der `PromptLibrary` auf diesem PC.
- Cloud nur nach Opt-in. Das Füllen der Platzhalter ruft kein Modell auf.
- Kein kostenpflichtiger Dienst. Eine Vorlage ist kein Bezahl-Prompt.
- Keine Geheimnisse in der Vorlage. `PromptLibrary.Check` meldet ein Passwort im Text. Die Ablage von Schlüsseln bleibt `src/Trendo.Core/Secrets.cs`.
- Freigabe über `src/Trendo.Core/ApprovalGate.cs`. Eine Vorlage erteilt sie nicht, auch wenn der Text „senden“ oder „freigeben“ enthält.
- Diese Funktion ist nicht Verbesserung 23, 25 oder 34 und nicht Language & Localization Studio 2.0. Punkt 86 steht im Repo nicht. Verbesserung 35: nie aktivieren.

## Abhängigkeiten

- Funktion 5 zeigt die fehlenden Platzhalter als Rückfragen.
- Funktion 7 bleibt der einzige Modellaufruf, und diese Funktion braucht ihn zum Füllen nicht.
- Keine Abhängigkeit zum KI-Laden. Pull Request #252 ist dafür nicht erforderlich.

## Akzeptanzkriterien

- `Vorlage: brief-formulieren` ohne `Empfänger` endet mit einer Rückfrage zu diesem Platzhalter und ohne Ausgabedatei.
- Nach der Antwort steht `TemplateId` auf `brief-formulieren` und `TemplateVersion` auf der Fassung der Bibliothek.
- Eine unbekannte Kennung erzeugt die genannte Warnung und keine neue Vorlage.
- Der gefüllte Text ersetzt den Auftragstext erst, wenn kein Platzhalter mehr fehlt.

## Offene Fragen

- Die Leiste bietet die Liste aus `MainWindow.Prompts.cs` nicht an. Reicht die Zeile `Vorlage:`?
- `PromptLibrary.Check` läuft in der Vorlagen-Seite, nicht in `ApplyTemplate`. Soll der Auftrag dieselbe Prüfung sehen, bevor der Text übernommen wird?

## Ist-Stand

`ApplyTemplate` und der Test `Vorlage_fragt_fehlende_Felder` sind auf `main`. Die Vorlagen-Seite bleibt eine eigene Seite. Die Auftragsleiste zeigt keine Vorlagenliste.
