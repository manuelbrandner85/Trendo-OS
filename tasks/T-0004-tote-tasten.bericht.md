# T-0004 Bericht: keine toten Schaltflächen

**Auftrag:** Ein Headless-Test findet jede sichtbare, aktivierte Schaltfläche ohne Click-Handler und ohne Command. Pro Studio mit Katalogeintrag: Seite öffnet, Überschrift ist der Katalogname, mindestens eine Hauptaktion ist bedienbar.
**Branch:** `grok/T-0004-tote-tasten`

## Geänderte Dateien

- `tests/Trendo.App.Tests/DeadButtonTests.cs` – neuer Test. Sammellauf über Startseite, Alle Studios und jede Seite aus `StudioPages` und `PageModules`. Pro Katalogstudio ein `AvaloniaTheory` mit `MemberData`.
- `AGENTS.md` – unter Arbeitsweise Punkt 8 mit dem vorgegebenen Satz zu einzelnen Terminalbefehlen. Punkt 7 (enger, schon auf `main`) bleibt.
- `docs/build-log/2026-10-22-tote-tasten.md` – dieser Eintrag.

`src/Trendo.App/MainWindow*.cs` ist unverändert. Es gab keine echte tote Schaltfläche.

## Tote Tasten

Prüfung: sichtbar und aktiviert, kein `Command`, kein Eintrag für `Button.ClickEvent`. Aufgeklappte Gruppen zählen mit. Eine Fehlerseite statt der Studio-Seite lässt den Test rot werden.

**Vorher** (erster Lauf, noch ohne die ToggleButton-Ausnahme): 7 Treffer.

| Seite | Name | Text | Was es ist |
| --- | --- | --- | --- |
| bedien-hilfen | easy-lang | Leichte Sprache für Hilfetexte | CheckBox, speichert über IsCheckedChanged |
| bedien-sehen | a11y-motion | Bewegung reduzieren (keine Animationen) | CheckBox, speichert über IsCheckedChanged |
| bedien-sehen | color-blind | Status mit Zeichen statt nur Farbe | CheckBox, speichert über IsCheckedChanged |
| bedien-sehen | simple-mode | Einfachen Modus einschalten | CheckBox, speichert über IsCheckedChanged |
| biz-start | ln-consumers | Ich verkaufe an Privatkunden | CheckBox, wird beim Bauen der Webseite gelesen |
| biz-start | ln-small | Kleinunternehmer | CheckBox, wird beim Bauen der Webseite gelesen |
| finanzen | inv-small | Kleinunternehmer (keine MwSt) | CheckBox, wird beim Rechnung-Erstellen gelesen |

**Nachher:** 0 tote Schaltflächen. Die sieben bleiben bedienbar. Nichts wurde deaktiviert, kein Tooltip „Noch nicht verfügbar“.

Ausnahmen im Test, mit Begründung:

1. `ToggleButton` (CheckBox, RadioButton): der Klick schaltet `IsChecked` um. Das ist die Wirkung, auch ohne Click-Handler.
2. `TemplatedParent` gesetzt: Knopf aus einer Avalonia-Vorlage (Bildlauf, Klappfeld). Die Wirkung sitzt im umgebenden Steuerelement.

SPEC-07 („Texte übersetzen“) hat keine Seite. Der Abnahmefall prüft nur, dass `PageForStudio` null bleibt. Nicht gebaut.
CORE-01 öffnet den Startbildschirm. Der hat kein `page-title`, weil er nicht über `Page()` gebaut wird. Geprüft: Seite `start`, keine tote Taste, mindestens eine Hauptaktion (Kacheln).

Alle anderen 52 Katalogstudios: `page-title` ist `StudioCatalog.ById[id].Name`, keine tote Taste, mindestens eine bedienbare Hauptaktion neben der Hilfe.

## Testergebnis

Release, `dotnet test`, Ausgabe „Bestanden!“:

- `Trendo.Core.Tests`: Fehler 0, erfolgreich 427, übersprungen 0, gesamt 427, Dauer 10 s
- `Trendo.App.Tests`: Fehler 0, erfolgreich 188, übersprungen 0, gesamt 188, Dauer 5 m 29 s (darin die neuen 56 Fälle, davon 54 Studio-Abnahmen)
- `Trendo.Api.Tests`: Fehler 0, erfolgreich 14, übersprungen 0, gesamt 14, Dauer 2 s

Der App-Lauf ist der Stand mit der Zusicherung, dass keine Studio-Seite als Fehlerseite aufgeht. Core und Api sind seit ihrem Lauf unverändert.

## Offene Punkte / Risiken

- Kontrollkästchen ohne eigenen Click-Handler gelten nicht als tote Tasten. Ein späterer `ToggleButton`, der nur so aussieht und nichts schaltet, fällt damit nicht auf. `IsChecked` ändert sich beim Klick trotzdem.
- Knöpfe in geschlossenen Avalonia-Vorlagen und im zugeklappten Startmenü sind nicht sichtbar und werden nicht mitgezählt. Das Startmenü öffnet der Test nicht.
- Seiten, die weder Start, Alle Studios noch Eintrag in `StudioPages` oder `PageModules` sind (zum Beispiel Einstellungen, Projekte, Suche), prüft dieser Auftrag nicht. Dort liegen weitere Kontrollkästchen mit derselben Wirkung.
- Mehrere Reiter eines Studios tragen denselben Katalognamen. Die Abnahme öffnet die Seite aus `PageForStudio`.

## Nicht geprüft

- Kein Lauf der installierten Desktop-App mit der Maus. Geprüft hat der Headless-UI-Test.
- Kein Klick auf jede lebende Taste. Eine Taste mit Click-Handler gilt als wirksam, auch wenn der Handler nur eine Nebenwirkung hat (Zwischenablage, Datei, Freigabe).
- QA-017, QA-019 und Codesignatur nicht als bestanden gewertet.
- `requirements.yaml` nicht geändert. SPEC-07 nicht gebaut. Katalognamen nicht geändert.
