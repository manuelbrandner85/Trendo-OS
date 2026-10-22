# I42-10 Fachadapter ausführen

**Status:** zur Freigabe durch Manuel

**Quelle:** `docs/spec/IMPROVEMENT_42.md`, Abschnitt 3, Funktion 10. Wortlaut: „Echte typisierte Adapter und ausführbare Aktionen, mit Cancellation, Timeouts, Fehlerbehandlung und Ressourcenlimits.“

**Wörter:** Adapter = das Programmstück, das die Aktion ausführt. Typisiert = die Ein- und Ausgaben haben eine feste Form. Abbruch (Cancellation) = die Person stoppt den Lauf. Zeitlimit (Timeout) = der Lauf soll nicht unbegrenzt weiterlaufen. Ressourcenlimit = eine Obergrenze, hier für die Dateigröße. Fehlerbehandlung = ein Fehler wird als Fehler gezeigt und nicht als Erfolg.

## Ziel und Nutzen

Nur ein registrierter Adapter schreibt eine Datei oder löst eine Wirkung aus. Er kann abgebrochen werden, hat eine Grenze für Zeit und Größe und endet bei einem Fehler mit einem ehrlichen Status. Ein Studio ohne Adapter meldet „Nicht unterstützt“ und legt keine Scheindatei an.

## Nutzerablauf

1. Nach Plan und Prüfung ruft der Ablauf `Produce` auf.
2. Die Leiste kann „Abbrechen“ wählen. Läuft noch keine Wirkung, wird der Auftrag `Cancelled`.
3. Dauert es zu lange oder wird die Datei zu groß, endet der Lauf als Fehlschlag, ohne eine zweite Wirkung.
4. Wirft der Adapter eine IO-, Rechte-, Isolations- oder Argument-Ausnahme, steht die Meldung im Auftrag.
5. Eine Wirkung nach außen startet nur nach Funktion 18 und nur, wenn `CanPerformSideEffect` wahr ist.

## Anforderungen

- Der Vertrag ist `IStudioActionAdapter` in `Model.cs`: `StudioId`, `ActionId`, `Label`, `Effect`, `DeclaredLevel`, `Evidence`, `CanPerformSideEffect`, `RequiredFields`, `OutputKinds`, `TryParse`, `Missing`, `Validate`, `Produce`, `SideEffect`.
- `PromptActions.CreateDefault()` liefert eine leere Liste. Produktionsläufe haben damit keinen Adapter.
- `ResourceBudget.Default` erlaubt zwei Minuten und 20 Megabyte (`20 * 1024 * 1024` Byte). `Accept` lehnt eine größere Datei ab.
- Abbruch: `Cancel` setzt `Cancelled`, solange das Wirkungsbuch den Schlüssel nicht als `claimed`, `done` oder `blocked` führt. `ctx.Cancellation` bricht vor `Produce` ab.
- Die Leiste übergibt `CancellationToken.None`. Einen Timer auf `ResourceBudget.Timeout` startet `Produce` nicht.
- Fehler bleiben im Status `Failed` oder `Unsupported`. `produced` oder `verified` ohne vorhandene Datei gibt es nicht (`Accept` prüft `File.Exists`).
- Abschnitt 6 der Quelle nennt die Reihenfolge Brief, Office, E-Mail, Business, Software, 3D, Spiele, Medien. Das sind dieselben Bausteine, die der Katalog schon nennt (`Letters`, `Ooxml`, `MailCenter`, `Finance` / `BusinessStudio`, `DevStudio`, `Studio3D`, `GameStudio`, `MediaStudio`). Ein Adapter dafür ist nicht registriert.
- Fehlt Blender oder Unreal, bleibt der Lauf `blocked` oder `unsupported`. `Studio3D.Create` und `GameStudio.CreateUnrealProject` werden nicht als gelungen gemeldet, wenn das Werkzeug fehlt.
- Abschnitt 10, Prüffall 6: Studio ohne Adapter ergibt `unsupported`, keine Phantom-Datei, keine falsche Erfolgsmeldung.

## Betroffene Module

- `src/Trendo.Core/PromptAction/Model.cs` – `IStudioActionAdapter`, `AdapterCall`, `AdapterOutcome`, `ResourceBudget`
- `src/Trendo.Core/PromptAction/StudioWorkflow.cs` – `Produce`, `Cancel`, `Accept`
- `src/Trendo.Core/PromptAction/IntelligencePromptModel.cs` – `PromptActions.CreateDefault`
- `src/Trendo.Core/Letters.cs`, `src/Trendo.Core/Sys/Sys10.cs`, `src/Trendo.Core/W3/Mail.cs`, `src/Trendo.Core/Finance.cs`, `src/Trendo.Core/BusinessStudio.cs`, `src/Trendo.Core/DevStudio.cs`, `src/Trendo.Core/Studio3D.cs`, `src/Trendo.Core/GameStudio.cs`, `src/Trendo.Core/MediaStudio.cs` – vorhandene Fachbausteine, noch ohne Auftragsadapter
- `src/Trendo.App/MainWindow.PromptAction.cs` – Knopf `auftrag-abbrechen`
- `tests/Trendo.Core.Tests/PromptActionTests.cs` – `Ohne_Adapter_keine_Datei`, `Abbruch_blockiert_Wirkung`

## Sicherheit und Datenschutz

- Lokal zuerst. `Produce` erhält `SideEffectAuthorized: false`. Die Datei liegt im Projekt.
- Cloud nur nach Opt-in. Ein Adapter darf kein stilles Netz öffnen. Externe KI bleibt `SideEffectClass.ExternalAi` und Funktion 8.
- Kein kostenpflichtiger Dienst. Ein fehlendes Werkzeug wird nicht durch einen Bezahldienst ersetzt.
- Keine Geheimnisse in `AdapterOutcome`. Die Ablage bleibt `src/Trendo.Core/Secrets.cs`.
- Freigabe über `src/Trendo.Core/ApprovalGate.cs`. `SideEffect` läuft nur innerhalb von `ApprovalGate.Execute`.
- Diese Funktion ist nicht Verbesserung 23, 25 oder 34 und nicht Language & Localization Studio 2.0. Punkt 86 steht im Repo nicht. Verbesserung 35: nie aktivieren. Maschinen, CNC, Roboter und Boot bleiben ohne sicheren Controller und ohne Freigabe aus (`SPEC-14`, `CORE-09` im Katalog).

## Abhängigkeiten

- Funktion 9 sagt, welche Aktion überhaupt deklariert ist.
- Funktion 18 gibt die Wirkung frei. Ohne sie bleibt `CanPerformSideEffect` folgenlos.
- Keine Abhängigkeit zum KI-Laden. Pull Request #252 ist dafür nicht erforderlich. Der Adapter ruft die KI nicht selbst auf.

## Akzeptanzkriterien

- `CreateDefault()` leer: jedes der 54 Studios endet ohne Ausgabedatei und mit „Nicht unterstützt“.
- Eine Datei über 20 Megabyte wird abgelehnt. Der Status ist „Fehlgeschlagen“.
- „Abbrechen“ vor einer Wirkung lässt das Wirkungsbuch leer und sendet nichts.
- Ein Adapter, der eine Ausnahme wirft, schreibt die Meldung in `Errors` und meldet nicht „Datei erstellt“.

## Offene Fragen

- `ResourceBudget.Timeout` steht auf zwei Minuten. `Produce` hängt den Abbruch nicht an diese Frist, und die Leiste übergibt `CancellationToken.None`. Wer soll die zwei Minuten starten?
- Welcher der genannten Fachbausteine wird der erste registrierte Adapter? Die Quelle nennt das Briefstudio als erstes P0. `CreateDefault()` enthält ihn noch nicht.

## Ist-Stand

Schnittstelle, leere Liste, Abbruch, Größengrenze und die Tests mit einem nicht registrierten Testadapter sind auf `main`. Ein Brief-, Office-, Mail-, Geschäfts-, Software-, 3D-, Spiele- oder Medienadapter ist nicht registriert.
