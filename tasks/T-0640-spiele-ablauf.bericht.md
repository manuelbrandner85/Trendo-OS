# T-0640 Bericht: Spiele-Studio Stufe 2 – Team, Ablauf und Prüfstufe

**Auftrag:** E2, E3 und E4 aus `docs/nachbau/spiele-ki/anforderungen.md` im Programm „Spiele bauen“ umsetzen. Freigabe des Nutzers für Stufe 2, weitergeleitet von der Sitzung, die Stufe 1 gebaut hat.
**Branch:** `claude/T-0640-spiele-ablauf`, von `origin/main` 7f0f9e3fa, eigener Worktree. `origin/main` hat sich seitdem nicht bewegt.
**Status:** gebaut, automatisch getestet, QA-Freigabe. Nicht in `main`, kein Release: beides braucht ein eigenes OK des Nutzers.

## Was jetzt geht

- **E2 Team mit Hierarchie:** Unter dem Game Director stehen drei Ebenen: Leitung (Kreativ, Technik, Produktion), Abteilungsleitungen und Fachleute. Jede der 53 Fachrollen aus `GameExperts` hat genau eine Leitung. Bei Streit entscheidet die nächste gemeinsame Leitung, zwischen den Direktoren der Game Director. Der Aufbau steht in der Anweisung an die KI und unter „Produktion“. Eine Frage der KI gilt nur mit 2 bis 4 Antworten als Entscheidung; bis gewählt ist, entsteht nichts.
- **E3 Ablauf bis zur Abnahme:** „Stories aus der Spielbibel“ legt Epics und Stories mit Abnahmekriterien auf das vorhandene Aufgaben-Board. Kriterien werden einzeln abgehakt. Eine Story endet als fertig (alle Kriterien erfüllt), fertig mit Hinweisen (geprüft, mit Notiz) oder blockiert (mit Grund). Phasentore urteilen bestanden, Bedenken, nicht geprüft oder durchgefallen. Bedenken rücken nur mit festgehaltener Risiko-Annahme vor, die angezeigt und beim Wiederöffnen entfernt wird.
- **E4 Prüfstufe:** schlank (Standard), normal, streng. Je Stufe laufen genau die vorgesehenen Prüfungen. Ein Wechsel ändert nur die Stufe, alle Inhalte bleiben und er lässt sich rückgängig machen.

## Geänderte Dateien

- `src/Trendo.Core/Programs/GameTeam.cs` (neu): Hierarchie, Eskalation, Text für die KI.
- `src/Trendo.Core/Programs/GameFlow.cs` (neu): Stories, Kriterien, Abschluss, Tore, Prüfstufe, Risiken.
- `src/Trendo.Core/Programs/GameBoard.cs`: Stories nur über die Abnahme fertig; Aufgaben über ihre Nummer in der Anzeige (`At`).
- `src/Trendo.Core/Programs/GameDirector.cs`: Team-Aufbau in der Anweisung; Fragen nur mit 2 bis 4 Antworten.
- `src/Trendo.App/MainWindow.ProgGamePro.cs`: Prüfstufe, Torurteil, Risiko-Notiz, Team-Aufbau.
- `src/Trendo.App/MainWindow.ProgGameBoard.cs`: Stories, Kriterien, Abschluss-Knöpfe.
- `src/Trendo.App/MainWindow.ProgGameUgs.cs`: Fachleute mit ihrer Leitung und Entscheider bei Widerspruch.
- `tests/Trendo.Core.Tests/GameFlowTests.cs`, `tests/Trendo.App.Tests/GameFlowUiTests.cs` (neu).
- `docs/nachbau/spiele-ki/anforderungen.md`, `tasks/T-0640-spiele-ablauf.md`, dieser Bericht.

## Kommandos

- Gezielt: `dotnet test … --filter …` (Debug).
- Volltest: `dotnet test "Trendo.sln" -c Release`, `python -m unittest discover -s tests/claude_handoff`. Beide Exitcode 0.

## Testergebnis (Volltest auf d6f945d31)

| Projekt | Fehler | Erfolgreich | Übersprungen | Gesamt | Dauer |
|---|---|---|---|---|---|
| Trendo.Api.Tests | 0 | 16 | 0 | 16 | 2 s |
| Trendo.Core.Tests | 0 | 2240 | 5 | 2245 | 1 m 11 s |
| Trendo.App.Tests | 0 | 1210 | 4 | 1214 | 40 m 58 s |
| tests/claude_handoff | 0 | 11 | 0 | 11 | 37 s |

SHA-256 des dotnet-Protokolls: `83675E84AE249319B72DF7DF4880C0F95EA255B43A28D7E26E96DC6D820AC272`. Das Protokoll liegt nur lokal, nicht unter `reports/`. Übersprungen sind wie bisher Tests, die Geräte oder fremde Programme brauchen.

## QA-Review (unabhängiger Agent nach `19-qa-verification`, nur lesend)

Runde 1 und 2: **Freigabe**, ohne BLOCKER oder HOCH. Die mittleren Befunde wurden trotzdem vor dem Merge behoben:

| Befund | Stufe | Ergebnis |
|---|---|---|
| Erneutes Erzeugen legte fertige Stories doppelt an | MITTEL | behoben, Test |
| Leere oder als Text geschriebene Kriterien hebelten die Abnahme aus; gemischte Listen verschoben die Kriterien | MITTEL | behoben, Test |
| Einträge, die keine Aufgaben sind, verschoben die Knöpfe auf eine andere Story | MITTEL | behoben, Test |
| Die erste Korrektur löschte solche Einträge vor einer Fehlermeldung ohne Rückgängig-Schritt | MITTEL | behoben: nichts wird mehr gelöscht, Test |
| Risiko-Notiz unsichtbar und nie entfernt | NIEDRIG | behoben, Logik getestet, Anzeige ohne Test |
| Abweichung nicht dokumentiert | NIEDRIG | behoben in `anforderungen.md` |

Die letzte Korrektur (d6f945d31) hat der QA-Agent nicht mehr gesehen; sie setzt seine Empfehlung um und ist durch Test und Volltest belegt.

## Abweichungen und Grenzen

- **schlank** behält die bisherige Phasen-Abnahme nach den Phasen-Kriterien, damit vorhandene Projekte und Tests gleich bleiben. „Eine Seite Konzept“ ist nicht umgesetzt. Die QA hält das für vertretbar.
- **streng** prüft an den Toren und verbietet „fertig mit Hinweisen“; eine zusätzliche Prüfung bei jedem einzelnen Schritt gibt es nicht.
- In „normal“ und „streng“ urteilt eine Phase ohne Stories (z. B. Prototyp, Release) nur nach ihren Phasen-Kriterien.
- „Ohne Wahl keine Änderung“ ist nur im Weg über die lokale KI technisch erzwungen. Arbeitet Claude direkt im Studio, ist es eine Anweisung, keine Sperre.
- Die Übersetzungs-Leitung steht als Fachperson unter dem Release-Manager.

## Nicht geprüft

- Die App wurde nicht von Hand bedient; alle Oberflächen-Nachweise stammen aus Headless-Tests.
- Aussehen gegen die Regeln „hochwertige Optik“ und „Design je Programm“, besonders die engen Board-Spalten mit Kriterien.
- Verhalten echter KIs (Claude, lokale KI) bei Entscheidungsfragen.
- Linux- und Pi-Edition.

## Offene Punkte

- Merge nach `main` und Release brauchen ein ausdrückliches OK des Nutzers.
- Stufe 3 (E1, E5 bis E7, D5) ist nicht begonnen.
