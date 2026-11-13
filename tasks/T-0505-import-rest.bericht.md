# Bericht T-0505: Offene Import-Lücken aus T-0504

**Branch/Worktree:** `claude/T-0505-import-rest`, Worktree `Trend Os-worktrees/T-0505-import-rest`, Basis `claude/T-0504-import-budget` 584d3abb9 (nicht in main).

## Geändert
- `Office/OfficeZip.cs`: `LoadXml` für alle Formate mit Tiefengrenze (256) und Knotenbudget je Datei (20 Mio.). `Outermost` und `WithinTextBudget` als gemeinsame Helfer.
- `Office/SheetFiles.cs`:
  - Auswahllisten werden einmal zerlegt und laufen über das Budget.
  - Spaltenbreiten bleiben unter 16.384 Spalten.
  - Die Abschnitte eines Blatts zählen zum Knotenbudget.
  - ODS: höchstens 20 Mio. Zellen, eine Arbeitsgrenze, wiederholte Zeilen teilen sich den Text.
- `Office/DocxFormat.cs`, `Office/SlideRead.cs`:
  - Verschachtelte Texte werden einmal gelesen.
  - Bei DOCX-Kopf- und Fußzeilen sowie bei ODP bleibt das bisherige Verhalten erhalten (Textfelder einzeln), mit Textbudget.
- `Sys/Sys10.cs`: ZIP-Prüfung, begrenztes Lesen, Zeitgrenzen für alle Suchmuster, Spaltengrenze.
- `MeshIO.cs`:
  - `mtllib` und glTF-Puffer müssen im Modellordner liegen: keine Verknüpfungen auf dem Weg, keine Gerätedateien (Länge 0), Materialdatei höchstens 64 MB.
  - Grenzen beim Einlesen für STL, GLB, glTF und FBX.
  - glTF: Speicher- und Arbeitsgrenze, begrenzte Knotenbesuche, Prüfung vor dem Reservieren.
  - Text- und JSON-Dateien höchstens 256 MB.
- `App/MainWindow.W4Studio3D.cs`:
  - Import-Warteschlange; Dateien ab 32 MB werden im Hintergrund gelesen.
  - Wechselt das Projekt während des Lesens, wird verworfen.
  - Die Blender-Zwischendatei wird erst nach dem Import gelöscht.
- `tests/Trendo.Core.Tests/ImportTempoTests.cs`: 28 neue Tests.

## Nachweise
- Negativtests: 23 waren gegen 584d3abb9 rot. Fünf liefen dort bewusst nicht, weil sie Absturz oder Hänger auslösen. Die fünf zuletzt ergänzten Tests liefen nicht gegen den alten Stand.
- Vergleich 584d3abb9 gegen diesen Stand: 617 Lesevorgänge echter DOCX-, ODT-, PPTX-, XLSX-, OBJ-, STL-, GLB-, glTF- und FBX-Dateien. Alle gleich bis auf zwei glTF-Dateien, die vorher wie nachher wegen zu vieler Dreiecke abgelehnt werden, jetzt mit `InvalidDataException`. ODS und ODP waren nicht vorhanden.
- Ein Zwischenstand hätte bei drei echten DOCX-Kopf- oder Fußzeilen Text entfernt. Das ist korrigiert, der Vergleich danach war gleich.

## Prüfung
QA und Sicherheit, nur lesend: Beide `BLOCKED` auf dem ersten Stand (1494266e7). Alle blockierenden Befunde sind behoben:
- ODS-Text je Zeile
- verknüpfte Ordner und Gerätedateien
- verschachtelte Absätze

Ebenfalls umgesetzt aus den nicht blockierenden: Knotenbudget je Datei, glTF-Arbeitsgrenze, `Sys10`-Fristen, Reihenfolge beim Import, OneDrive-Platzhalter, entartete STL-Dreiecke. Eine zweite Prüfrunde fand nicht statt.

## Tests (dd7fb3121)
- `dotnet test Trendo.sln -c Release`, Exitcode 0:
  - Api: 16 von 16
  - Core: 1991 erfolgreich, 5 übersprungen, 1996 gesamt
  - App: 881 erfolgreich, 4 übersprungen, 885 gesamt
- `python -m unittest discover -s tests/claude_handoff`: 9 Tests, OK.

## Offen / nicht geprüft
- Keine zweite QA- und Sicherheitsprüfung nach den Korrekturen.
- Keine Messung auf dem Pi oder unter Linux; der Symlink-Test lief nur mit einer Windows-Junction.
- Der Hintergrund-Import hängt nur an der Dateigröße. Kleine glTF-Dateien können bis zur Arbeitsgrenze noch kurz auf dem UI-Thread rechnen.
- Weitere Regex-Stellen in `Sys10` für PDF haben keine Zeitgrenze (außerhalb des Auftrags).

**Status:** Umsetzung fertig, gepusht, kein PR, Merge offen.
