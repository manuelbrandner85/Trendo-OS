# T-0209 Prüf- und Umsetzungsplan (nur Analyse)

Stand: 2026-10-22. Dieser Lauf ändert keinen Helfer-Code. Der Branch ist nicht auf `main` gerebased. Nichts ist gemergt.

Es gibt keinen Branch `t-0209-alltag-helfer`. Geprüft ist `cursor/t-0209-alltag-helfer-18f9` (Tipp `5b777b1`). Der ältere Box-Branch `box/T-0209-alltag-helfer` ist bereits als PR [#212](https://github.com/manuelbrandner85/Trendo-OS/pull/212) auf `main`.

Vergleichsstand von `main`: `origin/main` bei `af4f3a7` (Merge-Lauf #243–#259 läuft, `main` kann sich weiter bewegen). Merge-Base dieses Branches mit diesem `main`: `57f000b`. `main` ist von dort 56 Commits voraus.

## Prüfergebnis

| Quelle | Stand |
|---|---|
| Auftrag `tasks/T-0209-alltag-helfer.md` | Absturz bei leerem Feld weg, Dialog plus Vorschau vor dem Ausführen, Papierkorb als Liste oder „leer“ / „unter Linux nicht verfügbar“, drei Testprojekte grün. Die Kästen im Auftrag sind offen. |
| Audit `docs/audit/LOGIK_AUDIT.md` | Absturz: `cur-run` (Geld umrechnen), `color-pick` (Pipette). Stumm: `clip-add`, `color-copy`, `img-left`, `img-resize`, `du-run`, `photo-apply`, `ren-apply`, `trash-empty`, `trash-show`. |
| Katalog `docs/spec/kataloge/SYS-02_KATALOG.md` | P01–P25 stehen als vorhanden oder „umgesetzt, Box getestet“. PC-Prüfung ist dort offen. |
| `requirements.yaml` SYS-02 | `IMPLEMENTED` / `PC_VERIFIED` mit Verweis auf `reports/pc-0.9.3`. Das ist der ältere Modulstatus. Dieser Plan ändert die Datei nicht. |
| PR [#222](https://github.com/manuelbrandner85/Trendo-OS/pull/222) | Geschlossen ohne Merge (2026-10-22). Kommentar: überholt durch `main`, zuletzt #230. #230 ist das 3D-Studio und ersetzt die Helfer-Lücken nicht. |
| PR [#212](https://github.com/manuelbrandner85/Trendo-OS/pull/212) | Gemergt. Zwei Dateien: Meldungen in `MainWindow.Sys02.cs`, Test `HelferFeedbackTests.cs`. |

### Was auf main schon steht

Der gemergte Auftrag #212 und der spätere Dateiauswahl-Auftrag (T-0201) decken den Absturz und die stummen Knöpfe bei leerer Eingabe ab. Auf `origin/main` (`MainWindow.Sys02.cs`, identisch zu `c291903` in diesen Dateien):

- `Try` fängt `FormatException` und `OverflowException` und zeigt „Das ist keine gültige Zahl …“.
- `Need` verlangt einen Text und wirft eine deutsche `ArgumentException`, die `Try` anzeigt. Betroffen sind Umbenennen, Speicherplatz, Fotos ordnen, Drehen, Verkleinern, Pipette, Betrag.
- `clip-add` und `color-copy` sagen bei leerem Feld, was fehlt. `trash-empty` sagt „Nichts zu leeren …“, solange nichts gelesen wurde. `trash-show` sagt „Der Papierkorb ist leer …“ oder „Papierkorb gelesen: N Einträge.“
- Liegen Einträge in `_trash`, zeigt die Seite bis zu 15 Zeilen (Name, Größe, Datum) und fragt vor dem Leeren nach.
- „Vorschau“ (`ren-preview-btn`) und „Probelauf“ (`photo-sim`) füllen eine Liste. „Umbenennen“ und „Ordnen“ führen danach sofort aus, sobald ein Pfad im Feld steht.
- T-0201 hängt an erkannte Pfadfelder „Ordner wählen …“ / „Datei wählen …“ (`MainWindow.Files.cs`, `PathFields`). `ren-folder`, `du-folder`, `photo-folder`, `img-file`, `color-file` sind solche Felder (`PathFieldsTests`).
- Kern und ein UI-Test schreiben schon echte Dateien: `Renamer.Apply`, `PhotoSorter.Plan`, `ImageTools.Rotate`/`Resize` (Kopie, Original bleibt), `Sys02UiTests` benennt `x.txt` nach `Foto_01.txt` um und macht das rückgängig. Zwischenablage speichert in `werkzeuge/zwischenablage.json` und lehnt Passwort-ähnliche Texte ab.
- `HelferFeedbackTests` prüft nur die Meldung bei leerem Feld, „abc“ beim Geld und den Papierkorb-Satz. Es prüft keine geschriebene Datei und keinen Pixel.

Die Studios aus `StudioCatalog.cs` bleiben. „Kleine Helfer & Musik“ (SYS-02) wird nicht gelöscht und nicht durch einen Platzhalter ersetzt.

### Was der Branch selbst enthält

Ein Commit `5b777b1` auf altem Stand (`57f000b`), 16 Dateien, +629/−64:

- `Utilities.cs`: `Calc.Zahl` (de-DE, dann invariant, `€`/`$` abstreifen, deutsche `ArgumentException`).
- `MainWindow.Sys02.cs`: eigene `FolderDialog`/`FileDialog`-Haken, Vorschau-Pflicht vor dem zweiten Klick bei Umbenennen und Fotos, Meldungen für die Audit-Knöpfe, Papierkorb-Text.
- Dieselben Meldungs-Muster in `MainWindow.Sys03.cs`, `Sys10.cs`, `W1Office.cs`, `W4Media.cs`, `W5Fusion.cs`, `X05.cs`, `MainWindow.axaml.cs`, `MainWindow.Look.cs` (Brief, PDF, Medien, Downloads, Ablauf, KI-Suche). Das ist T-0202/T-0211, nicht der Helfer-Auftrag.
- Tests: `AlltagHelferUiTests` (leeres Geld, Pipette mit echter PNG `#2F86F6`, Papierkorb-Satz, Speicherplatz-Satz), `ButtonProbe` plus `EveryButtonTests` über Alltag, Brief, PDF, Medien, Laden, Produktion, KI-Quellen, `Calc.Zahl` in `Sys02Tests`.
- Bericht `tasks/T-0209.bericht.md` und `docs/build-log/2026-10-22-T-0209.md`.

Der Bericht auf dem Branch nennt: Core 418 bestanden / 10 übersprungen, Api 14 bestanden, App 137 bestanden und 1 Fehler `SYS-03-P14`. Dieser Lauf hat diese Zahlen nicht neu gemessen.

### Was kaputt oder nicht übernehmbar ist

- Der Branch als Ganzes auf das heutige `main` zu mergen, würde den Stand vor Design 41, T-0201 und den laufenden PRs #243–#259 wieder aufmachen. 56 Commits liegen dazwischen. Deshalb kein Rebase und kein Merge, solange der Merge-Lauf läuft.
- `FolderDialog` / `FileDialog` / `UseFolderDialog` wären ein zweiter Dialog neben `PickOverride` und `EnhancePathFields`. Der bestehende Dialog bleibt die eine Auswahl.
- `EveryButtonTests` klickt Seiten, die #244 (Brief), #245 (Office), #254 (Medien) und der übrige Stapel anfassen. Diese Seiten gehören nicht in den nächsten Helfer-Diff.
- `Show` nach jedem Klick baut die Seite neu. Ein späterer Patch muss die Pfad-Knöpfe von T-0201 dabei stehen lassen (`EnhancePathFields` läuft beim Seitenaufbau).
- Auf `main` rechnet `double.Parse("1.08", de-DE)` zu **108**, ohne Ausnahme (geprüft mit einem Wegwerf-Programm unter `/tmp/zahlcheck`, nicht im Repo). Das Feld „Kurs“ nutzt genau diesen Aufruf. `1,08` bleibt `1,08`. `abc` wirft `FormatException` und wird zur Meldung. `Calc.Zahl` auf diesem Branch nimmt für `1.08` den invariant-Pfad und liefert `1.08`, weil `TryParse` mit de-DE dort falsch ist und erst danach invariant prüft. Die stille Verhundertfachung ist der offene Geld-Fehler.
- Papierkorb: fehlt `~/.local/share/Trash`, liefert `Trash.List` eine leere Liste, und die Oberfläche sagt „leer“. Der Auftrag verlangt dafür „unter Linux nicht verfügbar“. Die Zeichenkette kommt im Helfer-Code nicht vor. Eine vorhandene, leere `info/`-Ablage bleibt „leer“. Die Liste der Einträge ist im Code da, ein Test legt aber keine `.trashinfo` an und liest sie nicht zurück.
- „Umbenennen“ und „Ordnen“ verschieben beim ersten Klick, sobald das Feld gefüllt ist. Der Auftrag will die Vorschau der betroffenen Dateien vor dem Ausführen.

## Was fehlt

Nach dem Merge-Lauf, auf dem dann aktuellen `main`, nur diese Lücken:

1. Geld: `10` × `1,08` ergibt `10,8`. `1.08` bleibt `1.08` und wird nicht zu `108`. `abc` und ein leeres Feld bleiben eine deutsche Meldung, kein Absturz.
2. Umbenennen und Fotos ordnen: erster gültiger Lauf zeigt die Vorschau (Name → Ziel) und schreibt nichts. Erst der zweite Lauf mit denselben Angaben ruft `Renamer.Apply` und legt die Rückgängig-Datei unter `werkzeuge/rueckgaengig` an. Geänderte Angaben verwerfen die Freigabe.
3. Leeres Ordner- oder Bildfeld am Aktionsknopf öffnet den vorhandenen T-0201-Dialog (`PickInto` / `PickOverride`), nicht einen neuen Haken. Abbrechen lässt die bisherige „Bitte zuerst …“-Meldung stehen.
4. Papierkorb: Einträge als Liste; vorhandene leere Ablage „leer“; fehlende Linux-Ablage „unter Linux nicht verfügbar“. „Ja, endgültig leeren“ bleibt im Test ungeklickt.
5. Echte Dateien in den UI-Tests, nicht nur geänderter Text: umbenannte Datei und Rückgängig, Foto unter `JJJJ/JJJJ-MM/` (Änderungsdatum reicht, EXIF ist optional), gedrehte und verkleinerte PNG-Kopie bei unverändertem Original-Hash, Speicherplatz nennt die angelegte Datei, Pipette liest `#2F86F6` aus einer echten PNG, Zwischenablage-Text steht in `zwischenablage.json`.
6. Die drei Testprojekte laufen auf diesem Stand. Zahlen kommen aus der Ausgabe. `SYS-03-P14` (Chrome druckt hier US Letter) bleibt ein Umgebungsfehler und wird nicht angefasst.

## Sperrfläche (nicht anfassen)

Diese Dateien ändern auch #259 und der Stapel #243–#258. Dieser Auftrag lässt sie aus, auch nach dem Merge:

- `PromptActions.CreateDefault` in `src/Trendo.Core/PromptAction/IntelligencePromptModel.cs`
- `docs/improvement-42/studio-capability-matrix.md`
- `RebuildAdapter` / `src/Trendo.Core/Rebuild/Adapter.cs` (X-10, PR #248)

Dazu nicht anfassen: `StudioCatalog.cs`, bestätigte Studio-Seiten, `requirements.yaml`, Logo, kostenpflichtige APIs, Veröffentlichungen, Installationen. Kein zweites Dialogsystem. Keine Übernahme der Branch-Diffs in `Sys03`, `Sys10`, `W1Office`, `W4Media`, `W5Fusion`, `X05`, `MainWindow.axaml.cs`, `MainWindow.Look.cs`.

Erlaubte Dateien der späteren Umsetzung, und nur wenn `main` sie bis dahin nicht anderweitig belegt:

- `src/Trendo.App/MainWindow.Sys02.cs`
- `src/Trendo.Core/Utilities.cs` ausschließlich um `Calc.Zahl`
- Tests unter `tests/Trendo.App.Tests/` und `tests/Trendo.Core.Tests/Sys02Tests.cs` für die Fälle oben
- danach `tasks/T-0209.bericht.md` mit den echten Testzahlen

## Umsetzungsplan

1. Warten, bis der Merge-Lauf #243–#259 auf `main` steht oder ausdrücklich pausiert. Diesen Branch nicht rebasen und nicht mergen.
2. Den Diff von `5b777b1` nicht wieder aufspielen. Die Meldungen aus #212 und die Dateiauswahl aus T-0201 sind die Grundlage.
3. Neuen Branch vom dann aktuellen `main` ziehen. Umsetzung nur der sechs Punkte unter „Was fehlt“, in `MainWindow.Sys02.cs` und `Calc.Zahl`.
4. Vorschau-Zustand an der Seite halten (`Ordner + Muster` bzw. `Foto-Ordner`). `Renamer.Apply` und das Verschieben der Fotos laufen erst, wenn die Vorschau zu genau diesen Angaben schon sichtbar war.
5. Leeres Pfadfeld: `PickInto` des bestehenden Feldes. Koordinaten der Pipette bleiben ein Textfeld (`x,y`); dafür gibt es keinen Dialog.
6. Papierkorb-Text in der Oberfläche entscheiden, bevor `Trash.List` eine leere Liste als „leer“ ausgibt. `Trash.List` selbst bleibt die Aufzählung.
7. Kleine Commits, kein Force-Push. PR gegen das dann ruhige `main`. Nicht mergen.

## Testplan

Zuerst die vorhandenen Tests unverändert laufen lassen und die Zahlen notieren: `dotnet test tests/Trendo.Core.Tests -c Release`, `dotnet test tests/Trendo.Api.Tests -c Release`, `dotnet test tests/Trendo.App.Tests -c Release`. Ein rotes `SYS-03-P14` auf diesem Linux-Image ist der bekannte Chrome-Papierformat-Fehler und kein Helfer-Fehler.

Neue oder erweiterte Fälle, jeder mit echter Datei oder echtem Pixel. Keine erfundenen Ergebnisse:

| Fall | Ablauf | Erwartung |
|---|---|---|
| `Calc.Zahl` | `1,5`, `1.08`, ` 10 € `, leer, `abc` | `1.5`, `1.08`, `10`; leere und falsche Texte werfen `ArgumentException` mit dem Feldnamen |
| Geld, UI | Betrag `10`, Kurs `1,08`, dann Kurs `1.08`, dann `abc` | Ergebnistext enthält `10,8`; `1.08` bleibt derselbe Wert und nicht `108`; `abc` zeigt die Zahlen-Meldung, Prozess bleibt |
| Umbenennen | zwei Dateien im Temp-Ordner, Muster `Foto_{n:00}` | erster Klick: Vorschauzeilen, Dateinamen unverändert; zweiter Klick: `Foto_01` existiert, Quelle weg; Rückgängig stellt `x.txt` wieder her |
| Fotos | PNG im Ordner, ohne EXIF | Probelauf/erster Klick zeigt den Zielpfad `JJJJ/JJJJ-MM`; zweiter Klick legt die Datei dort ab und lässt eine Rückgängig-Datei zurück |
| Speicherplatz | Ordner mit einer benannten Datei | Ergebnis nennt den Dateinamen und eine Größe |
| Drehen / Verkleinern | PNG, Hash vorher | Kopie `*-gedreht.png` bzw. Größenname existiert, Original-Hash gleich |
| Pipette | PNG 8×8 Farbe `(47,134,246)`, Stelle `1,1`; danach leere Stelle | Text enthält `#2F86F6`; leere Stelle meldet `x,y` |
| Papierkorb | (a) keine Trash-Ablage auf Linux, (b) eine `.trashinfo` unter `info/` plus Datei unter `files/` über die vorhandenen Parser-Pfade, soweit der Test die Ablage in den Datenordner legen kann | (a) „unter Linux nicht verfügbar“; (b) Dateiname in der Liste. Fehlt ein Einsprung ohne den echten Home-Papierkorb, den Test nicht grün schreiben, sondern den fehlenden Einsprung als offen dokumentieren |
| Zwischenablage | Text „Einkaufsliste: Brot“ | `zwischenablage.json` enthält den Text; ein Passwort-ähnlicher Text landet nicht darin |
| Leerer Aktionsknopf | `ren-apply` ohne Ordner, `PickOverride` liefert einen Temp-Ordner | Feld erhält den Pfad, es wird noch nichts umbenannt |
| Dialog abbrechen | `PickOverride` liefert nichts | Meldung „Bitte zuerst …“, keine Dateiänderung |

`trash-yes`, `rec-start` und `shot-screen` bleiben außen vor. Business und Einrichtung bleiben außen vor.

Headless prüft den Dialog nur über `PickOverride`. Ein echter Windows-Ordnerdialog, ein echter Windows-`$Recycle.Bin` und die Zwischenablage des Desktops stehen im Bericht unter „nicht geprüft“. QA-017, QA-019 und Codesignatur bleiben unberührt.

## Offenes

- Dieser Lauf hat `dotnet test` nicht ausgeführt. Die Zahlen im Branch-Bericht `tasks/T-0209.bericht.md` gelten für den alten Commit, nicht für heutiges `main`.
- Ob `main` während #243–#259 `MainWindow.Sys02.cs` oder `Utilities.cs` noch anfasst, ist offen. Vor dem ersten Edit den Diff dieser zwei Dateien gegen den dann aktuellen `main` lesen. Sperrfläche bleibt tabu, auch wenn der Stapel sie verändert hat.
- Ein UI-Test für „unter Linux nicht verfügbar“ braucht einen Weg, der nicht den echten Home-Papierkorb des Laufs leert oder vortäuscht. Fehlt der Weg, bleibt der Punkt offen statt mit einem Scheintest zu schließen.
- `SYS-03-P14` auf diesem Image.
- Windows-Dialog, echter Papierkorb, Desktop-Zwischenablage.
