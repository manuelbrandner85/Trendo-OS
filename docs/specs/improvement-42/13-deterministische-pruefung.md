# I42-13 Deterministische Fachprüfung

**Status:** zur Freigabe durch Manuel

**Quelle:** `docs/spec/IMPROVEMENT_42.md`, Abschnitt 3, Funktion 13. Wortlaut: „Fachrechner/Validatoren mit Einheiten, Eingangsannahmen, Versionsstand und reproduzierbarem Prüfergebnis.“

**Wörter:** Deterministisch = dieselbe Eingabe ergibt immer dasselbe Ergebnis. Der Rechner im Programm macht das, nicht die KI. Validator = diese Prüfung. Einheit = zum Beispiel Meter oder Euro. Annahme = die Voraussetzung, unter der gerechnet wurde. Reproduzierbar = ein zweiter Lauf mit denselben Eingaben liefert denselben Wert. Versionsstand = welche Fassung des Rechners benutzt wurde.

## Ziel und Nutzen

Rechnen und Prüfen macht ein festes Werkzeug, nicht die KI. Das Ergebnis nennt Einheit, die Eingaben, die Annahmen und die Fassung des Werkzeugs. Dieselbe Eingabe ergibt dasselbe Ergebnis. Weicht ein KI-Text davon ab, ist das ein QA-Fehler.

## Nutzerablauf

1. Der Adapter ruft den vorhandenen Rechner für dieses Fach auf.
2. Das Ergebnis wird ein `QaResult` mit `CheckId`, bestanden oder nicht, Satz, Einheit, Eingaben und Werkzeugfassung.
3. `Judge` verlangt mindestens ein QA-Ergebnis. Fehlt es, ist der Lauf nicht bestanden.
4. Ein nicht bestandener Satz steht in den Fehlern. Der Status wird „Fehlgeschlagen“, nicht „QA bestanden“.
5. Die Person sieht den Satz in der Leiste.

## Anforderungen

- `QaResult` in `Model.cs` hat `CheckId`, `Passed`, `Detail`, `Units`, `Inputs`, `ToolVersion`.
- `Judge` setzt `Failed`, wenn `Qa` leer ist oder ein Eintrag `Passed == false` hat. Der leere Fall sagt „Die Aktion hat keine Prüfung geliefert.“
- Die KI darf Annahmen vorschlagen. Sie liefert den Mess- oder Rechenwert nicht als Fakt. Der Vergleich steht in der Prüfung des Adapters.
- Vorhandene Rechner, die ein Adapter aufrufen kann, sobald er registriert ist:
  - `Calc` und `Units` in `src/Trendo.Core/Utilities.cs` und `src/Trendo.Core/Sys/Sys02.cs`
  - `Finance` mit `decimal` in `src/Trendo.Core/Finance.cs`
  - `Electronics` in `src/Trendo.Core/W6/Electronics.cs`
  - `Engineering` in `src/Trendo.Core/W6/Engineering.cs`
  - `Stats` in `src/Trendo.Core/W6/Science.cs`
  - `Letters.Validate` in `src/Trendo.Core/Letters.cs`
- Abschnitt 10, Prüffall 2 und 10: eine Tabelle mit bekannten Formeln und eine technische Rechnung mit Einheiten, Grenzfällen und demselben Wert bei Wiederholung. Eine KI-Abweichung ist ein QA-Fehler.
- Geld und Steuer bleiben `decimal`. Ein Betrag wird nicht als „gebucht“ markiert, solange keine Transaktion gelaufen ist (`Ledger` in `Finance.cs` ist die vorhandene Buchhaltung, kein Prompt-Adapter).
- Ohne Adapter läuft `Judge` in der Produktion nicht, weil der Lauf vorher „Nicht unterstützt“ ist.

## Betroffene Module

- `src/Trendo.Core/PromptAction/Model.cs` – `QaResult`
- `src/Trendo.Core/PromptAction/StudioWorkflow.cs` – `Judge`
- `src/Trendo.Core/Utilities.cs` – `Calc`
- `src/Trendo.Core/Sys/Sys02.cs` – `Units`
- `src/Trendo.Core/Finance.cs` – `Finance`, `Ledger`
- `src/Trendo.Core/W6/Electronics.cs`, `src/Trendo.Core/W6/Engineering.cs`, `src/Trendo.Core/W6/Science.cs`
- `src/Trendo.Core/Letters.cs` – `Letters.Validate`
- `tests/Trendo.Core.Tests/PromptActionTests.cs` – der Testadapter liefert QA; ohne QA endet `Judge` als Fehlschlag

## Sicherheit und Datenschutz

- Lokal zuerst. Die Rechner laufen im Prozess, ohne Netz.
- Cloud nur nach Opt-in. Eine Rechnung wird nicht zur Kontrolle hochgeladen.
- Kein kostenpflichtiger Dienst. Ein externes Rechen-API ist keine Voraussetzung.
- Keine Geheimnisse in `QaResult`. `Inputs` nennt die fachlichen Größen, keine Schlüssel. Ablage: `src/Trendo.Core/Secrets.cs`.
- Freigabe über `src/Trendo.Core/ApprovalGate.cs`. Eine bestandene Rechnung gibt eine Zahlung nicht frei.
- Diese Funktion ist nicht Verbesserung 23, 25 oder 34 und nicht Language & Localization Studio 2.0. Punkt 86 steht im Repo nicht. Verbesserung 35: nie aktivieren.

## Abhängigkeiten

- Funktion 10 muss den Rechner im Adapter aufrufen. Heute ist keiner registriert.
- Funktion 14 prüft Format und Fachregeln neben der Zahl.
- Keine Abhängigkeit zum KI-Laden. Pull Request #252 ist dafür nicht erforderlich. Die Prüfung ist deterministisch.

## Akzeptanzkriterien

- Ein Lauf mit leerer `Qa`-Liste endet mit „Die Aktion hat keine Prüfung geliefert.“ und nicht mit „QA bestanden“.
- Ein `QaResult` mit Einheit, Eingaben und `ToolVersion` lässt sich aus denselben Eingaben erneut erzeugen.
- Eine KI-Zahl, die vom Rechner abweicht, setzt `Passed` auf falsch.
- `Finance` rechnet Geldbeträge mit `decimal`. Der Auftrag markiert keine Buchung, die `Ledger` nicht geschrieben hat.

## Offene Fragen

- Welcher vorhandene Rechner wird der erste Beleg im Adapter: `Letters.Validate`, `Finance` oder `Electronics`?
- `ToolVersion` hat im Kern keinen gemeinsamen Wert. Soll dort die Assembly-Fassung von Trendo stehen oder die Fassung des Fachmoduls?

## Ist-Stand

`QaResult` und `Judge` sind im Kern. Die genannten Rechner liegen in ihren Modulen und sind nicht an `CreateDefault()` angeschlossen. Ein Auftrag in der Leiste erreicht `Judge` deshalb nicht.
