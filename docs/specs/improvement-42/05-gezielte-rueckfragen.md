# I42-05 Gezielte Rückfragen

**Status:** zur Freigabe durch Manuel

**Quelle:** `docs/spec/IMPROVEMENT_42.md`, Abschnitt 3, Funktion 5. Wortlaut: „Nur fehlende, entscheidungsrelevante Daten nachfragen; Teilantworten fortsetzen können.“

**Wörter:** Rückfrage = Trendo fragt nur eine fehlende Angabe und rät sie nicht. Teilantwort = die Person ergänzt einen Teil. Der Rest des Auftrags bleibt stehen. Pflichtfeld = eine Angabe, ohne die die Aktion nicht sinnvoll ist.

## Ziel und Nutzen

Trendo fragt nur, was für die Aktion wirklich fehlt. Was die Person schon gesagt hat, bleibt stehen. Sie ergänzt den Rest und macht an demselben Auftrag weiter. Trendo rät die Lücke nicht zu.

## Nutzerablauf

1. Der Adapter nennt fehlende Pflichtfelder (`Missing`).
2. Die Leiste zeigt den Status „Rückfrage“ und die Fragen. Der Knopf „Antwort übernehmen“ wird sichtbar.
3. Die Person schreibt die fehlenden `Feld: Wert`-Zeilen.
4. `Answer` übernimmt die Angaben in denselben Auftrag und prüft erneut.
5. Fehlt danach noch ein Pflichtfeld, bleibt die Rückfrage. Eine Wirkung startet nicht.

## Anforderungen

- Fragen sind `ClarificationQuestion` mit Kennung, Feld, Satz und `Required` (`Model.cs`).
- Nur `Required` hält den Auftrag in `NeedsClarification`. Ein bereits gesetzter Parameter wird nicht noch einmal gefragt, solange `Missing` ihn nicht erneut liefert.
- `Answer` läuft nur in `NeedsClarification` oder `RevisionRequested`. Sonst bleibt der Hinweis „Gerade ist keine Rückfrage offen.“ und die Datei wird nicht neu geschrieben.
- Teilantworten: gesetzte Schlüssel bleiben in `RunState.Parameters`. `Answer` löscht die Fragen und ruft `Validate` erneut auf.
- Verbotene Schlüssel aus der Antwort werden übersprungen (`Forbidden`).
- Ohne Adapter kommt die Rückfrage „Welche Aktion“ nicht dazu, weil der Lauf vorher „Nicht unterstützt“ ist. Die Leiste ohne Projekt zeigt „Lege zuerst ein Projekt an.“ und den Status „Rückfrage“, bevor `Start` läuft.
- Abschnitt 10 der Quelle, Prüffall 5: ein widersprüchlicher oder unvollständiger Auftrag führt zu einer gezielten Rückfrage, nicht zu einem geratenen Ergebnis.

## Betroffene Module

- `src/Trendo.Core/PromptAction/Model.cs` – `ClarificationQuestion`, `RunStatus.AwaitingInput`, `StatusText.German` („Rückfrage“)
- `src/Trendo.Core/PromptAction/StudioWorkflow.cs` – `Answer`
- `src/Trendo.App/MainWindow.PromptAction.cs` – Knopf `auftrag-antworten`
- `tests/Trendo.Core.Tests/PromptActionTests.cs` – `Rueckfrage_dann_Datei`
- `tests/Trendo.App.Tests/PromptBarTests.cs` – `Brief_leiste_ohne_erfundenes_Ergebnis`

## Sicherheit und Datenschutz

- Lokal zuerst. Fragen und Antworten bleiben im Projektordner (`auftraege/{id}/lauf.json`).
- Cloud nur nach Opt-in. Eine Rückfrage löst keinen Anbieter auf.
- Kein kostenpflichtiger Dienst.
- Keine Geheimnisse in der Frage. Die Antwort läuft durch `Strip`. Die Ablage bleibt `src/Trendo.Core/Secrets.cs`.
- Freigabe über `src/Trendo.Core/ApprovalGate.cs`. Eine Antwort auf eine Rückfrage ist keine Freigabe. Der Knopf „Einmal freigeben“ bleibt dabei unsichtbar.
- Diese Funktion ist nicht Verbesserung 23, 25 oder 34 und nicht Language & Localization Studio 2.0. Punkt 86 steht im Repo nicht. Verbesserung 35: nie aktivieren.

## Abhängigkeiten

- Funktion 3 erkennt die Aktion, zu der die Felder gehören.
- Funktion 4 prüft, sobald die Pflichtfelder da sind.
- Funktion 6 macht aus fehlenden Vorlagen-Platzhaltern dieselben Fragen.
- Keine Abhängigkeit zum KI-Laden. Pull Request #252 ist dafür nicht erforderlich.

## Akzeptanzkriterien

- Ein unvollständiger Auftrag an einen Adapter endet mit den fehlenden Feldern und ohne Ausgabedatei.
- Eine Teilantwort füllt nur die genannten Felder. Der Auftrag behält dieselbe `RunId`.
- Eine Antwort, die `freigabe` setzt, übernimmt dieses Feld nicht.
- Ohne offene Rückfrage ändert „Antwort übernehmen“ die gespeicherte Fassung nicht (`save: false`).

## Offene Fragen

- Die Leiste schickt das ganze Textfeld an `Answer` (`PromptFields.Read`), nicht ein Feld je Frage. Reicht das für „Teilantworten“?
- Der Status „Rückfrage“ auf der Briefseite im Test kommt daher, dass noch kein Projekt existiert. Soll derselbe Satz gelten, wenn das Studio keinen Adapter hat? Heute ist das dann „Nicht unterstützt“, sobald ein Projekt da ist.

## Ist-Stand

`Answer`, die Fragen am Adapter und der Knopf „Antwort übernehmen“ sind vorhanden. In der Produktion gibt es keinen Adapter, der `Missing` füllt. Die Vorlagen-Rückfrage aus Funktion 6 ist der Weg, der ohne Testadapter heute Fragen erzeugt.
