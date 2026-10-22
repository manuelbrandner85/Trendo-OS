# I42-04 Prüfung von Studio, Aktion und Format

**Status:** zur Freigabe durch Manuel

**Quelle:** `docs/spec/IMPROVEMENT_42.md`, Abschnitt 3, Funktion 4. Wortlaut: „Studio-, Aktions- und Dateiformat-spezifische Validierung mit verständlichen Fehlermeldungen.“

**Wörter:** Validierung = die Prüfung, ob Studio, Aktion und Angaben zusammenpassen, bevor etwas geschrieben wird. Befund = ein Satz, was nicht stimmt. Blockierend = der Auftrag stoppt. Schema = die vereinbarte Form der Daten. Fassung = die Versionsnummer dieser Form.

## Ziel und Nutzen

Bevor etwas geschrieben oder gesendet wird, prüft Trendo Studio, Aktion und die Angaben dazu. Die Person liest einen deutschen Satz, was fehlt oder widerspricht. Ein unverständlicher Code allein bleibt nicht die einzige Meldung.

## Nutzerablauf

1. Ein leerer Auftrag endet mit „Bitte schreib zuerst, was das Studio tun soll.“
2. Eine unbekannte Studio-Kennung endet mit „Dieses Studio gibt es nicht.“
3. Der Adapter liefert `ValidationFinding` mit Code, Satz und der Angabe, ob der Befund blockiert.
4. Ein blockierender Befund setzt den Status „Fehlgeschlagen“ und schreibt die Sätze in `Errors`. Es entsteht keine Ausgabedatei.
5. Ein Hinweis, der nicht blockiert, bleibt eine Warnung. Die Arbeit kann weitergehen, wenn die übrigen Regeln passen.

## Anforderungen

- Die Prüfung hängt an `IStudioActionAdapter.Validate`. Der gemeinsame Kern ruft sie auf, nachdem Pflichtfelder gefüllt sind (`StudioWorkflow.Validate`).
- Meldungen sind ganze Sätze auf Deutsch. Der Code (`ValidationFinding.Code`) bleibt für den Test, der Satz (`Message`) für die Person.
- Die Laufdatei mit einer höheren `SchemaVersion` als `StudioWorkflow.SchemaVersion` wird nicht ausgeführt. Die Meldung nennt, dass die Fassung neuer ist als diese Trendo-Version.
- Dateiformate prüfen die vorhandenen Fachprüfungen, sobald ein Adapter sie aufruft. Brief: `Letters.Validate` in `src/Trendo.Core/Letters.cs`. Tabelle und Text: `Ooxml` in `src/Trendo.Core/Sys/Sys10.cs`. Webseite: `WebsiteQuality.Check` in `src/Trendo.Core/Website/WebsiteQuality.cs`.
- Ein Studio ohne Adapter endet vorher als „Nicht unterstützt“ und erreicht `Validate` nicht.
- Die Oberfläche zeigt `Errors` in `auftrag-detail`.

## Betroffene Module

- `src/Trendo.Core/PromptAction/Model.cs` – `ValidationFinding`
- `src/Trendo.Core/PromptAction/StudioWorkflow.cs` – `Validate`, `Resume`
- `src/Trendo.Core/Letters.cs` – `Letters.Validate`
- `src/Trendo.Core/Sys/Sys10.cs` – `Ooxml`, `Csv`, `Pdf`
- `src/Trendo.Core/Website/WebsiteQuality.cs` – `Check`
- `src/Trendo.App/MainWindow.PromptAction.cs`
- `tests/Trendo.Core.Tests/PromptActionTests.cs` – `Widerspruch_blockiert`, `Schema_zu_neu_bleibt_liegen`

## Sicherheit und Datenschutz

- Lokal zuerst. Die Prüfung liest Angaben und Dateien im Projekt.
- Cloud nur nach Opt-in. Die Validierung selbst stellt keine Verbindung her.
- Kein kostenpflichtiger Dienst. Eine externe Prüf-API ist keine Voraussetzung.
- Keine Geheimnisse in der Fehlermeldung. `Redact` entfernt erkennbare Geheimnis-Muster aus dem Freigabetext. Die Ablage bleibt `src/Trendo.Core/Secrets.cs`.
- Freigabe über `src/Trendo.Core/ApprovalGate.cs` kommt erst nach einer bestandenen Prüfung einer Wirkung. Ein blockierender Befund überspringt die Freigabe und führt die Wirkung nicht aus.
- Diese Funktion ist nicht Verbesserung 23, 25 oder 34 und nicht Language & Localization Studio 2.0. Punkt 86 steht im Repo nicht. Verbesserung 35: nie aktivieren.

## Abhängigkeiten

- Funktion 3 liefert das Auftragsobjekt.
- Funktion 5 fragt, bevor die Prüfung einen leeren Pflichtwert als Fehler behandelt (`Missing` läuft vor `Validate`).
- Keine Abhängigkeit zum KI-Laden. Pull Request #252 ist dafür nicht erforderlich.

## Akzeptanzkriterien

- Ein leerer Text endet mit dem Satz „Bitte schreib zuerst, was das Studio tun soll.“ und ohne Datei.
- Ein blockierender `ValidationFinding` steht als Satz in der Leiste. Der Status ist „Fehlgeschlagen“.
- Eine neuere Auftragsfassung bleibt liegen und wird nicht ausgeführt.
- Solange kein Adapter registriert ist, erscheint die Meldung, dass es für dieses Studio noch keine Aktion gibt. Eine erfundene Formatprüfung gilt nicht als bestanden.

## Offene Fragen

- Es liegt keine eigene Schema-Datei pro Dateiformat im Repo. Reicht `Validate` am Adapter, sobald Brief, Tabelle oder Webseite angeschlossen werden?
- Der Testadapter blockiert `???` nur in `tests/Trendo.Core.Tests/PromptActionTests.cs`. Soll der Kern denselben Satz für jeden künftigen Adapter erzwingen?

## Ist-Stand

Leerer Text, unbekanntes Studio, Adapter-`Validate` und die Fassungsprüfung sind im Kern. Fachprüfungen für Brief, Office und Webseite liegen in den genannten Dateien und sind an `PromptActions.CreateDefault()` nicht angeschlossen.
