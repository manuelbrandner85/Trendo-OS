# T-0610 Bericht: Spiele-Studio Stufe 1 – Spieldesign

**Auftrag:** D1 bis D4 aus `docs/nachbau/spiele-ki/anforderungen.md` im Programm „Spiele bauen“ umsetzen: Ideen finden, Einstieg ohne Idee, Spielbibel als Dokument aus Abschnitten, Chat je Abschnitt.
**Branch:** `claude/T-0610-spiele-design`, von `origin/main` 023a6d742, eigener Worktree. `origin/main` ist inzwischen 12 Commits weiter; nicht zusammengeführt.
**Status:** Stufe 1 gebaut, automatisch getestet, Branch gepusht. Kein Merge, kein Release.

## Was jetzt geht

- **D1 Ideen:** Stichworte, Genre, Plattform, Stil und Perspektive ergeben mehrere Ideen. Bewerten mit 👍/👎, „Neue Ideen“ berücksichtigt das. Ohne KI oder bei unbrauchbarer Antwort kommen Ideen aus einem eigenen Baukasten, mit Hinweis auf die Quelle.
- **D2 Einstieg ohne Idee:** Drei Schritte mit Knöpfen (Stimmung, Spielart, Größe) führen zu Ideen, „Daraus Spiel machen“ legt das Projekt an und öffnet die Spielbibel.
- **D3 Dokument:** Abschnitte verschieben, eigene hinzufügen und entfernen, Bilder im Abschnitt, Ablage für ungenutzte Bilder, Export als Markdown und PDF. Bauen und Prüfen lesen weiterhin dieselben Felder.
- **D4 Chat je Abschnitt:** Vorschläge erscheinen als Vorschau und ändern erst nach „Übernehmen“ nur diesen Abschnitt, als ein Rückgängig-Schritt. Der Verlauf liegt in neuen Begleitdaten der Datei (`Meta`) und erzeugt keine Rückgängig-Schritte.

## Geänderte Dateien

- `src/Trendo.Core/Programs/GameConcept.cs` (neu): Ideen, Baukasten, KI-Antwort lesen, geführter Weg.
- `src/Trendo.Core/Programs/GameDocument.cs` (neu): Abschnitte, Bilder, Markdown, PDF, Chat, Vorschläge.
- `src/Trendo.Core/Programs/ProgramDocument.cs`: Begleitdaten `Meta`/`EditMeta`; Dateien ohne Begleitdaten bleiben unverändert.
- `src/Trendo.Core/Programs/GameDesign.cs`: `BibleMarkdown` nutzt `GameDocument.Markdown`.
- `src/Trendo.App/MainWindow.ProgGameDesign.cs` (neu), `src/Trendo.App/MainWindow.ProgGame.cs`: Oberfläche, Bereich „Ideen“, PDF-Export.
- `tests/Trendo.Core.Tests/GameConceptTests.cs`, `GameDocumentTests.cs`, `tests/Trendo.App.Tests/GameDesignDocUiTests.cs` (neu).
- `docs/nachbau/spiele-ki/anforderungen.md`, `tasks/T-0610-spiele-design.md`, dieser Bericht.

## Kommandos

- Gezielt: `dotnet test … --filter …` (Debug).
- Volltest: `dotnet test "Trendo.sln" -c Release`, `python -m unittest discover -s tests/claude_handoff`. Beide Exitcode 0.

## Testergebnis (zweiter Volltest, auf dem committeten Stand)

| Projekt | Fehler | Erfolgreich | Übersprungen | Gesamt | Dauer |
|---|---|---|---|---|---|
| Trendo.Api.Tests | 0 | 16 | 0 | 16 | 2 s |
| Trendo.Core.Tests | 0 | 1899 | 5 | 1904 | 1 m 25 s |
| Trendo.App.Tests | 0 | 877 | 4 | 881 | 36 m 55 s |
| tests/claude_handoff | 0 | 9 | 0 | 9 | 31 s |

SHA-256 des dotnet-Protokolls: `6D6EDB8A04FC7D301FB4C9BADCEB15986C8ADBD78D725CD9D00789A1C1FE0358`. Das Protokoll liegt nur lokal, nicht unter `reports/`; ein Status `TESTED` oder `PC_VERIFIED` ist damit nicht belegt.

Die 9 übersprungenen Tests hängen von Geräten oder Programmen ab (BlueZ, Rechtschreibung, LibreOffice, Tesseract, mpv, Musik-KI, Chrome/Edge) oder sind der Klick-Audit T-0202. Keiner betrifft diesen Auftrag direkt.

Der erste Volltest hatte 1 Fehler (`GameRobust_Ui.Bible_with_objects`): Mechaniken, die als Eintrag mit Name und Beschreibung gespeichert sind, wurden leer angezeigt. Das war ein Fehler dieses Umbaus, er ist behoben.

## Abweichungen und Grenzen

- Konzeptbild nur auf Knopfdruck je Idee, nicht automatisch für alle, weil jedes Bild die Grafikkarte belegt.
- Der Text-Export ist Markdown; eine eigene `.txt`-Ausgabe gibt es nicht.
- Figuren, Welt, Missionen und Meilensteine werden in ihren Bereichen bearbeitet; im Dokument sind sie Anzeige. Der Chat kann für sie trotzdem Vorschläge machen.
- Wiederherstellen nutzt die vorhandenen Dateiversionen und wirkt erst, wenn das Projekt gespeichert wurde.
- Das Wiederherstellen setzt den Chat-Verlauf nicht zurück (gewollt).
- Bilder werden als Pfad gespeichert, nicht ins Projekt kopiert. Wird die Datei verschoben, fehlt sie; das PDF läuft trotzdem durch und nennt bei nicht einbettbaren Bildern den Dateinamen. Eine fehlende Datei wird im Test geprüft.

## Nicht geprüft

- Die App wurde nicht von Hand bedient; alle Oberflächen-Nachweise stammen aus Headless-Tests.
- Keine Prüfung des Aussehens gegen die Regeln „hochwertige Optik“ und „Design je Programm“.
- Antworten echter KIs (Claude, Internet-KI, lokale KI) sind nicht geprüft; die Tests bilden die KI nach.
- Echte Bilderzeugung ist nicht geprüft; die Tests bilden das Werkzeug nach bzw. nutzen den Testmodus.
- PDF: Text nur über die Seitenzahl bei langem Text, Bilder über XObjects. Das Druckbild wurde nicht angesehen.
- Linux- und Pi-Edition nicht geprüft.
- QA-Agent-Review nach Projektregel steht aus.

## Offene Punkte

- Merge nach `main` und Release brauchen ein ausdrückliches OK.
- Stufe 2 (E2 bis E4) und Stufe 3 (E1, E5 bis E7, D5) sind nicht begonnen.
