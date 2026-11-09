# T-0610 Bericht: Spiele-Studio Stufe 1 – Spieldesign

**Auftrag:** D1 bis D4 aus `docs/nachbau/spiele-ki/anforderungen.md` im Programm „Spiele bauen“ umsetzen: Ideen finden, Einstieg ohne Idee, Spielbibel als Dokument aus Abschnitten, Chat je Abschnitt.
**Branch:** `claude/T-0610-spiele-design`, von `origin/main` 023a6d742, eigener Worktree, zweimal mit `origin/main` zusammengeführt.
**Status:** Stufe 1 gebaut, automatisch getestet, QA-Freigabe erteilt, nach ausdrücklichem OK nach `main` gebracht. Kein Release.

## Was jetzt geht

- **D1 Ideen:** Stichworte, Genre, Plattform, Stil und Perspektive ergeben mehrere Ideen. Bewerten mit 👍/👎, „Neue Ideen“ berücksichtigt das und zeigt Ungesehenes. Ohne KI oder bei unbrauchbarer Antwort kommen Ideen aus einem eigenen Baukasten, mit Hinweis auf die Quelle.
- **D2 Einstieg ohne Idee:** Drei Schritte mit Knöpfen (Stimmung, Spielart, Größe) führen zu Ideen, „Daraus Spiel machen“ legt das Projekt an und öffnet die Spielbibel.
- **D3 Dokument:** Abschnitte verschieben, eigene hinzufügen und entfernen, Bilder im Abschnitt, Ablage für ungenutzte Bilder, Export als PDF und Markdown (beide auch in der Stufe Einfach). Bauen und Prüfen lesen weiterhin dieselben Felder.
- **D4 Chat je Abschnitt:** Vorschläge erscheinen als Vorschau, sind an ihr Spiel gebunden und ändern erst nach „Übernehmen“ nur diesen Abschnitt, als ein Rückgängig-Schritt. Der Verlauf liegt in neuen Begleitdaten der Datei (`Meta`) und erzeugt keine Rückgängig-Schritte.

## Geänderte Dateien

- `src/Trendo.Core/Programs/GameConcept.cs` (neu): Ideen, Baukasten, KI-Antwort lesen, geführter Weg.
- `src/Trendo.Core/Programs/GameDocument.cs` (neu): Abschnitte, Bilder, Markdown, PDF, Chat, Vorschläge, Pfadprüfung.
- `src/Trendo.Core/Programs/ProgramDocument.cs`: Begleitdaten `Meta`/`EditMeta`; Dateien ohne Begleitdaten bleiben unverändert.
- `src/Trendo.Core/Programs/GameDesign.cs`: `BibleMarkdown` nutzt `GameDocument.Markdown`.
- `src/Trendo.App/MainWindow.ProgGameDesign.cs` (neu), `MainWindow.ProgGame.cs`: Oberfläche, Bereich „Ideen“, PDF-Export.
- `src/Trendo.App/MainWindow.ProgramShell.cs` (`SimpleExports`), `MainWindow.Windows.cs` (Neuzeichnen nach Rückkehr).
- `tests/Trendo.Core.Tests/GameConceptTests.cs`, `GameDocumentTests.cs`, `tests/Trendo.App.Tests/GameDesignDocUiTests.cs` (neu).
- `docs/nachbau/spiele-ki/anforderungen.md`, `tasks/T-0610-spiele-design.md`, dieser Bericht.

## Kommandos

- Gezielt: `dotnet test … --filter …` (Debug).
- Volltest: `dotnet test "Trendo.sln" -c Release`, `python -m unittest discover -s tests/claude_handoff`. Beide Exitcode 0.

## Testergebnis (Volltest auf d13e0b7e4, enthält das damalige `origin/main`)

| Projekt | Fehler | Erfolgreich | Übersprungen | Gesamt | Dauer |
|---|---|---|---|---|---|
| Trendo.Api.Tests | 0 | 16 | 0 | 16 | 2 s |
| Trendo.Core.Tests | 0 | 2002 | 5 | 2007 | 1 m 22 s |
| Trendo.App.Tests | 0 | 898 | 4 | 902 | 37 m 29 s |
| tests/claude_handoff | 0 | 9 | 0 | 9 | 36 s |

SHA-256 des dotnet-Protokolls: `6CD42C8089815321021E9D27F7DF0E162EBD44AC6D3706E56002551743CE17F7`. Das Protokoll liegt nur lokal, nicht unter `reports/`; ein Status `TESTED` oder `PC_VERIFIED` ist damit nicht belegt.

Übersprungen sind Tests, die Geräte oder Programme brauchen (BlueZ, Rechtschreibung, LibreOffice, Tesseract, mpv, Musik-KI, Chrome/Edge), und der Klick-Audit T-0202. Nach d13e0b7e4 kam nur dieser Bericht hinzu.

Der erste Volltest hatte 1 Fehler (`GameRobust_Ui.Bible_with_objects`): Mechaniken, die als Eintrag mit Name und Beschreibung gespeichert sind, wurden leer angezeigt. Das war ein Fehler dieses Umbaus und ist behoben.

## QA-Review (unabhängiger Agent nach `19-qa-verification`, nur lesend)

Runde 1: **keine Freigabe.** Runde 2: **keine Freigabe.** Runde 3: **Freigabe** (Code gelesen, keine eigenen Testläufe). Ein PR existiert nicht, weil das `gh`-Konto auf diesem PC nicht zum Projekt gehört; das Ergebnis steht deshalb hier statt im PR. Einen eigenen Security-Agenten gab es nicht, der QA-Auftrag umfasste die Sicherheitsprüfung der Bildpfade.

| Befund | Stufe | Ergebnis |
|---|---|---|
| Vorschlag nicht an sein Dokument gebunden, konnte nach Tab-Wechsel das falsche Spiel überschreiben | BLOCKER | behoben, Tests |
| KI-Antwort verwarf getippten Text und holte aus anderen Programmen | HOCH | behoben, Tests |
| Bildpfade aus fremden Dateien (Netzwerkzugriff beim Anzeigen), auch über `\??\` | HOCH | behoben: nur noch lokale Laufwerkspfade mit PNG/JPG, Tests |
| PDF in der Stufe Einfach nicht erreichbar | HOCH | behoben: PDF und Markdown sichtbar; die Anzeige in Einfach prüft nur der Wert `SimpleExports` |
| Absolute Pfade in Markdown und KI-Auftrag | MITTEL | behoben: nur Dateiname |
| Liste ohne Einträge wirkte als „alles löschen“ | MITTEL | behoben, Tests |
| „Neue Ideen“ ohne KI lieferte dasselbe | MITTEL | behoben, Tests |
| Fenster blieb nach Antwort auf „KI arbeitet“ stehen | MITTEL | behoben, Test |

## Bekannte Restpunkte (alle NIEDRIG, bewusst nicht behoben)

- Das Entfernen eines Abschnitts lässt sich rückgängig machen, sein Chat-Verlauf aber nicht.
- Die exportierte `.md` nennt Bilder nur mit Dateinamen und zeigt sie deshalb nicht an; das ist der Preis für den Datenschutz.
- Reine Chat-Änderungen erzeugen beim Speichern Dateiversionen und können bei höchstens 20 ältere Stände verdrängen.
- Bilder werden je Neuzeichnen gelesen und dekodiert (verkleinert, nicht gemessen).
- Beim Zurückkehren in das Fenster kann noch nicht übernommener Text in einem Spiele-Feld verloren gehen, und der Neuaufbau kann den ersten Klick schlucken.
- `_gameStale` kann einen unnötigen Neuaufbau beim nächsten Fensterwechsel auslösen.
- Text im Titelfeld des Programms geht bei einer KI-Antwort verloren.
- Ein einzeiliger Vorschlagsblock, der mit „Text “ beginnt, verliert dieses Wort; sichtbar in der Vorschau.

## Abweichungen und Grenzen

- Konzeptbild nur auf Knopfdruck je Idee, nicht automatisch für alle, weil jedes Bild die Grafikkarte belegt.
- Der Text-Export ist Markdown; eine eigene `.txt`-Ausgabe gibt es nicht.
- Figuren, Welt, Missionen und Meilensteine werden in ihren Bereichen bearbeitet; im Dokument sind sie Anzeige. Der Chat kann für sie trotzdem Vorschläge machen.
- Wiederherstellen nutzt die vorhandenen Dateiversionen und wirkt erst, wenn das Projekt gespeichert wurde. Es setzt den Chat-Verlauf nicht zurück (gewollt, getestet).
- Bilder werden als Pfad gespeichert, nicht ins Projekt kopiert. Fehlt die Datei, läuft das PDF trotzdem durch und nennt den Dateinamen.
- Die Markdown-Ausgabe hat sich leicht geändert: Zielgruppe in der Kopfzeile, Mechaniken als Einträge lesbar, Bildzeilen, andere Leerzeilen am Ende.

## Nicht geprüft

- Die App wurde nicht von Hand bedient; alle Oberflächen-Nachweise stammen aus Headless-Tests. Das gilt auch für Fenster und Fokus.
- Keine Prüfung des Aussehens gegen die Regeln „hochwertige Optik“ und „Design je Programm“.
- Antworten echter KIs (Claude, Internet-KI, lokale KI) sind nicht geprüft; die Tests bilden die KI nach.
- Echte Bilderzeugung ist nicht geprüft.
- PDF: Text über Seitenzahl und eingebettete Schrift, Bilder über XObjects. Das Druckbild wurde nicht angesehen.
- Echte Netzwerkverbindung über gesperrte Pfade nicht ausprobiert. Linux- und Pi-Edition nicht geprüft.

## Offene Punkte

- Release braucht ein ausdrückliches OK.
- Stufe 2 (E2 bis E4) und Stufe 3 (E1, E5 bis E7, D5) sind nicht begonnen.
