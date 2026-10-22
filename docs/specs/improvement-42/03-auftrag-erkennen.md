# I42-03 Auftrag erkennen

**Status:** zur Freigabe durch Manuel

**Quelle:** `docs/spec/IMPROVEMENT_42.md`, Abschnitt 3, Funktion 3. Wortlaut: „Strukturiertes, versioniertes Auftragsobjekt statt freier, unkontrollierter Tool-Aufrufe.“

**Wörter:** Auftragsobjekt = der Text in fester Form: Aktion, Angaben, Fassung. Fassung = die Versionsnummer des gespeicherten Auftrags. Adapter = das Programmstück, das eine Aktion wirklich ausführt. Vorschlag = ein Hinweis der KI. Er ist kein Befehl und keine Freigabe. Tool-Aufruf = ein direkter Start eines Werkzeugs. Den gibt es hier nicht aus dem freien Text.

## Ziel und Nutzen

Aus dem Text wird ein Auftrag mit Fassung, Aktion und Parametern. Die KI darf höchstens Felder vorschlagen. Sie ruft keine Werkzeuge auf, vergibt keine Rechte und ersetzt keine Freigabe.

## Nutzerablauf

1. Der Ablauf liest Zeilen der Form `Feld: Wert` (`PromptFields.Read`).
2. Gibt es für das Studio einen Adapter, sucht `TryParse` eine Aktion. Sonst darf das angeschlossene Modell ein JSON mit `actionId` und `parameters` vorschlagen.
3. Der Auftrag bekommt `SchemaVersion` 1 und eine Aktionskennung.
4. Felder, die eine Freigabe vortäuschen, werden verworfen.
5. Ohne passende Aktion bleibt eine Rückfrage oder der Status „Nicht unterstützt“. Es wird kein Werkzeug geraten.

## Anforderungen

- Das Auftragsobjekt ist `RunState` plus `StudioActionSpec` in `src/Trendo.Core/PromptAction/Model.cs`. Die Fassung der Laufdatei ist `StudioWorkflow.SchemaVersion` (1). Eine neuere Fassung wird nicht ausgeführt (`Resume`).
- Erlaubte Aktionen sind nur die registrierten `IStudioActionAdapter`. `PromptActions.CreateDefault()` ist leer.
- `Suggest` schickt den Text zwischen `UNTRUSTED_START` und `UNTRUSTED_END`. Die Systemanweisung erlaubt nur das JSON und verbietet Freigabe, Zahlung und Versand.
- `ForbiddenKeys` entfernt `approval`, `approved`, `consent`, `freigabe`, `sideeffect` und `side-effect`. Schlüssel mit `passwort` oder `apikey` fallen weg (`Strip`).
- Ein Modelltext ist ein Vorschlag. `ReadSuggestion` übernimmt ihn nur, wenn die `actionId` zu einem Adapter dieses Studios gehört.
- Der Auftrag durchläuft feste Stände: Entwurf, Prüfung, Rückfrage oder Plan, danach bei Bedarf Freigabe, dann Ausführung, Datei, QA, bestanden. Daneben gibt es Änderung, Fehlschlag, „nicht unterstützt“ und Abbruch. Im Code heißen sie `WorkflowPhase`.

## Betroffene Module

- `src/Trendo.Core/PromptAction/Model.cs` – `StudioActionSpec`, `ActionDraft`, `WorkflowPhase`, `IStudioActionAdapter`
- `src/Trendo.Core/PromptAction/PromptFields.cs`
- `src/Trendo.Core/PromptAction/StudioWorkflow.cs` – `Validate`, `Suggest`, `ReadSuggestion`, `Strip`
- `src/Trendo.Core/PromptAction/IntelligencePromptModel.cs` – `IPromptModel`, `PromptActions.CreateDefault`
- `docs/improvement-42/architecture-decision.md` – Abbildung der Soll-Namen auf diese Typen
- `tests/Trendo.Core.Tests/PromptActionTests.cs` – `Prompt_ersetzt_keine_Freigabe`, `Lokale_KI_gibt_nicht_frei`, `Schema_zu_neu_bleibt_liegen`

## Sicherheit und Datenschutz

- Lokal zuerst. Der Vorschlag läuft nur, wenn `IPromptModel.IsLocal` wahr ist (`IntelligencePromptModel.CompleteAsync` liefert sonst null).
- Cloud nur nach Opt-in über `src/Trendo.Core/PromptAction/ConsentStore.cs`. Eine externe Quelle ohne gültige Einwilligung schreibt `ki.external.blocked` und liefert keinen Vorschlag.
- Kein kostenpflichtiger Dienst. Ein Bezahlmodell wird nicht vorausgesetzt.
- Keine Geheimnisse im JSON-Vorschlag. Verbotene Schlüssel werden entfernt. Die Ablage bleibt `src/Trendo.Core/Secrets.cs`.
- Freigabe über `src/Trendo.Core/ApprovalGate.cs`. Ein Feld im Prompt oder im Modelltext setzt sie nicht.
- Diese Funktion ist nicht Verbesserung 23, 25 oder 34 und nicht Language & Localization Studio 2.0. Punkt 86 steht im Repo nicht. Verbesserung 35: nie aktivieren.

## Abhängigkeiten

- Funktion 7 ist der einzige KI-Eingang (`IntelligenceCore.AskAsync`). Eine zweite Conversation-Engine entsteht nicht.
- Das Laden der Modelle und die automatische Wahl sind der KI-Laden, Pull Request #252 (Entwurf, nicht gemergt). Das gilt erst nach Merge von #252. Ohne Modell bleibt `Suggest` bei null, und der Auftrag geht über Felder und Rückfragen weiter.
- Funktion 4 prüft das Objekt, bevor eine Datei entsteht.

## Akzeptanzkriterien

- Ein Prompt mit `freigabe:` oder `approval:` führt die Wirkung nicht aus. Der Test `Prompt_ersetzt_keine_Freigabe` bleibt der Beleg dafür.
- Eine `actionId` außerhalb der registrierten Adapter endet als „Nicht unterstützt“, ohne Datei.
- Eine Laufdatei mit `SchemaVersion` über 1 wird nicht ausgeführt.
- Ein Modellvorschlag, der eine Freigabe behauptet, verliert dieses Feld und erteilt die Freigabe nicht.

## Offene Fragen

- `StudioActionSpec.SchemaVersion` und `RunState.SchemaVersion` sind zwei Fassungen. Welche steigt, wenn sich nur die Parameter eines Studios ändern?
- Der Testadapter in `tests/Trendo.Core.Tests/PromptActionTests.cs` blockiert den Text `???`. Er ist nicht registriert. Soll dieselbe Regel im gemeinsamen Kern stehen?

## Ist-Stand

Felder, Fassung, verbotene Schlüssel und der eine Modellvorschlag sind im Kern. Ohne registrierten Adapter endet der Lauf, bevor eine Aktion gewählt wird. `CreateDefault()` ist leer.
