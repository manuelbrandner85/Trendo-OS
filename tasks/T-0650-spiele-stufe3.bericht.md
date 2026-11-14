# T-0650 Bericht: Spiele-Studio Stufe 3 – Prüfung und Anbindung

**Auftrag:** E1, E5, E6, E7 und D5 aus `docs/nachbau/spiele-ki/anforderungen.md` im Programm „Spiele bauen“. Freigabe des Nutzers am 14.11.2026 („Ja“ zu Stufe 3).
**Branch:** `claude/T-0650-spiele-stufe3`, von `origin/main` 92b46dd0a, eigener Worktree; vor dem Merge mit dem neuen `origin/main` (2ba49d090, u. a. T-0612 kie.ai, T-0613 ruhiges Design) zusammengeführt, ohne Konflikte.
**Status:** gebaut, drei QA-Runden (unabhängige, nur lesende Prüfung), alle Befunde behoben, Volltest siehe unten.

## Was jetzt geht

- **E1 Engine einrichten:** Karte unter „Bauen“: Engine erkannt, Projekt angelegt, Öffnen und Bauen. „Geprüft“ steht nur mit bestandenem `Trendo-Pruefung/bericht.txt`, der nicht älter als der letzte Bau ist. Genau ein nächster Schritt mit Knopf; fehlt die Engine, ist es die offizielle Seite, nichts wird geladen. Lange Unity- und Godot-Prüfungen laufen im Hintergrund, ein zweiter Lauf am selben Projekt ist gesperrt.
- **E5 Prüfung per Bild (nur Unreal):** Bilder dieses Laufs werden gemessen: schwarz und einfarbig (Lauf fällt durch), leuchtend pinke Flächen (Hinweis auf fehlende Materialien bei Godot/Unity), Abweichung zum Bild desselben Schritts im Vergleichslauf (Hinweis), fehlende Bilder gegenüber dem Vergleichslauf. Jeder Befund mit Bild und Messwert, als Aufgabe im Board (ohne Messwert im Titel, keine Doppelten). Keine KI-Bildbewertung.
- **E6 Wiederholbare Testläufe (nur Unreal):** Startwert `-trendoseed` (Spielcode setzt FMath::RandInit/SRandInit), jeder Lauf mit Messwerten aller TRENDO-Zeilen und Bildkopien gespeichert (höchstens 20 Läufe, Aufräumen nur im eigenen Ordner), Vergleich meldet Verschlechterungen mit Mindeständerung je Messwert. Gleiche Werte sind nicht garantiert (Bildrate und Physik hängen an der Zeit).
- **E7 Dienste:** eine Auswahl je Art (Bild, 3D, Geräusche, Musik, Sprache): eingerichtete Werkzeuge auf dem PC, dann freie Quellen; Bezahl-Dienste nur, wenn selbst eingerichtet. Erzeugtes kommt mit Herkunft und Lizenz in die Asset-Liste, ohne Lizenz nicht. kie.ai (T-0612, seit heute in main) ist in diese Auswahl noch nicht eingebunden.
- **D5 Ähnliche Spiele:** Die KI schlägt Titel vor, gezeigt wird nur, was ein Wikipedia-Artikel (de, sonst en) in seiner Kurzbeschreibung als Computer- oder Videospiel belegt; Filme, Serien, Brett- und Kartenspiele, Spielreihen fallen weg. Sonst „keine Referenz gefunden“. Ist Wikipedia (auch teilweise) nicht erreichbar, bleibt der alte Stand. Gespeicherte Links gelten nur, wenn sie auf `*.wikipedia.org` zeigen.

## Geänderte Dateien

- Neu: `src/Trendo.Core/Programs/GameEngineSetup.cs`, `GameShotCheck.cs`, `GameRuns.cs`, `GameServices.cs`, `GameReferences.cs`, `src/Trendo.App/MainWindow.ProgGameStage3.cs`, `tests/Trendo.Core.Tests/GameStage3Tests.cs`, `tests/Trendo.App.Tests/GameStage3UiTests.cs`.
- Geändert: `UnrealGame.cs` (Startwert), `MainWindow.ProgGame.cs`, `ProgGameUnreal.cs`, `ProgGameEngines.cs`, `ProgGameAssets.cs`, `ProgGameDesign.cs`, `docs/nachbau/spiele-ki/anforderungen.md`, `tasks/T-0650-spiele-stufe3.md`.

## QA

- Runde 1: 13 Befunde (u. a. WAGEN-Messwerte gingen bei echten Läufen verloren, alte Bilder als neue geprüft, „geprüft“ trotz veraltetem Bericht, zu weiche Spiel-Belege). Alle behoben.
- Runde 2: alle 13 bestätigt; 7 neue (Doppelstart der Engine-Prüfung, Aufräumen über die Id aus der Datei, erledigte Aufgaben, Teilausfall Wikipedia u. a.). Alle behoben.
- Volltest 1 fand einen Verstoß gegen QA-019 (Netz-Typ im Kern); behoben, Netzfehler werden am Typnamen erkannt.

## Testergebnis

Volltest auf dem zusammengeführten Stand 0aaec8baa: `dotnet test Trendo.sln -c Release` Exitcode 0, `python -m unittest discover -s tests/claude_handoff` OK.

| Projekt | Fehler | Erfolgreich | Übersprungen | Gesamt | Dauer |
|---|---|---|---|---|---|
| Trendo.Api.Tests | 0 | 16 | 0 | 16 | 2 s |
| Trendo.Core.Tests | 0 | 2436 | 5 | 2441 | 1 m 29 s |
| Trendo.App.Tests | 0 | 1242 | 5 | 1247 | 37 m 13 s |
| tests/claude_handoff | 0 | – | – | OK | – |

SHA-256 des dotnet-Protokolls: `0D09C51B010328AFEDEE09D80A7D672B20CFADD0E390B35EC104815F45310AD0` (nur lokal). Übersprungen sind wie bisher Tests, die Geräte oder fremde Programme brauchen.

## Nicht geprüft / offen

- Kein Lauf an echter Engine in dieser Aufgabe; die Startwert-Zeile im Unreal-Spielcode wird beim nächsten „Kompilieren und prüfen“ kompiliert.
- Kein echter Wikipedia-Abruf (nur Testersatz).
- Laufvergleich und Bildprüfung nur für Unreal, nicht für Browser, Godot, Unity.
- kie.ai in die Dienste-Auswahl einbinden (Folgeaufgabe).
