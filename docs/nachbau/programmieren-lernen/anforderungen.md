# Programmieren lernen – Anforderungen (Clean-Room)

Studio `STUDIO-CODELEARN-01`, Seite `programmieren-lernen-programm`, Kategorie Wissen & Lernen. Freigabe des Nutzers am 26.11.2026 (Etappe 0.9.113, Paket T-0782).

## Vorbilder (nur Verhalten und Bedienung, kein Code, keine Texte, keine Symbole)

| Vorbild | Quelle | Datum | belegte Arbeitsweise |
|---|---|---|---|
| Exercism | „Test Runners: Docker“, exercism.org/docs/building/tooling/test-runners/docker | Abruf 26.11.2026 | Test-Runner ohne Netzwerk (`--network none`) |
| Exercism | „Test Runner Interface“, exercism.org/docs/building/tooling/test-runners/interface | Abruf 26.11.2026 | Zeitgrenze, Status pass/fail/error, Ergebnis je Test mit Meldung und gekürzter Ausgabe |
| Exercism | „Concept Exercises“, exercism.org/docs/building/tracks/concept-exercises | Abruf 26.11.2026 | Track aus Konzepten mit Voraussetzungen; Hinweise je Aufgabe, die die Lösung nicht verraten |
| Codecademy | „Accessibility on the Platform“, codecademy.com/articles/accessibility-on-the-platform | Abruf 26.11.2026 | Erklärtext, Editor und Ausgabe nebeneinander; automatische Prüfung |
| Codecademy | „New learning environment platform features“, codecademy.com/resources/blog/new-learning-environment-platform-features | 05/09/2024 (Schreibweise der Quelle) | KI-Hinweise ohne volle Lösung, Hilfe bei Fehlermeldungen |
| freeCodeCamp | „How to get started with the fCC Tribute Page“, freecodecamp.org/news/how-to-get-started-with-the-fcc-tribute-page | 23.08.2021 | „Run Tests“ zeigt bestandene/fehlgeschlagene Tests mit Fehlertext |
| freeCodeCamp | „freeCodeCamp Turns 10 + Major Curriculum Updates“, freecodecamp.org/news/freecodecamp-turns-10-major-curriculum-updates | 28.10.2024 | Workshops Schritt für Schritt, Labs gegen eine Testsammlung |
| Scratch | scratchfoundation.org | Abruf 26.11.2026 | Blöcke statt Syntax für Einsteiger |

### Offene Alternativen (nur zur Einordnung, nichts übernommen)

| Projekt | Lizenz | Quelle | Umgang |
|---|---|---|---|
| Exercism Website, Test-Runner | AGPL-3.0 | github.com/exercism/website, github.com/exercism/python-test-runner | nur Clean-Room; nur die öffentlich beschriebene Ergebnisform nachempfunden |
| Exercism problem-specifications | MIT | github.com/exercism/problem-specifications | nicht benutzt (eigene Aufgaben) |
| freeCodeCamp | BSD-3-Clause | github.com/freeCodeCamp/freeCodeCamp | nicht benutzt |
| Scratch-Editor | AGPL-3.0 | github.com/scratchfoundation/scratch-editor | nur Clean-Room |
| Blockly, scratch-blocks | Apache-2.0 | github.com/google/blockly | nicht benutzt (Block-Editor nicht gebaut) |
| Thonny | MIT | github.com/thonny/thonny | nicht benutzt |

## Vorhandenes geprüft und genutzt
- `code-ki-programm` (Code mit KI) erklärt Dateien mit Claude; Editor-Baustein (AvaloniaEdit mit Python-Farben, `CodeDarkColors`) übernommen.
- Sandbox-Bausteine: T-0766 (Wasmtime, nur WASI-Module – kein Python im PC vorhanden) und `Sys/Sandbox.cs` (niedrige Integrität + Job; **Netz dort nicht gesperrt**). Deshalb neu `Lernen/LernSandbox.cs`: dieselbe Job-Technik, zusätzlich **AppContainer ohne Fähigkeiten** – das Betriebssystem sperrt Netz und Loopback und jeden Datei-Zugriff außerhalb der freigegebenen Ordner; ohne Admin.
- Python: auf dem PC vorhanden (System-Python 3.13 und das mitgelieferte eingebettete Python im Trendo-Werkzeugordner). Abgeschottet läuft nur eine Kopie des mitgelieferten Python in Trendos Datenordner (lesbar für den Container); der Werkzeugordner wird nie verändert. C#/Node wären vorhanden, wurden wegen Python nicht gebraucht.

## Anforderungen

| ID | Funktion | Erwartetes Verhalten | Prüfung | Quelle | Ergebnis |
|---|---|---|---|---|---|
| PL-01 | Lernpfad mit Konzepten | 9 eigene Aufgaben in 7 Konzepten; nächste Aufgabe frei, wenn die vorige gelöst ist | Core `T0782_CodeLernen.Track_unlock`, UI `Code_click_to_real_tests` | Exercism Track | erfüllt |
| PL-02 | Echte Tests | unittest-Tests je Aufgabe, Ergebnis je Test (Name, Status, Meldung, Ausgabe ≤ 500 Zeichen), Status pass/fail/error/timeout | Core `Real_tests_pass_and_fail` (echt), UI `Code_click_to_real_tests` (echt) | Exercism Interface, freeCodeCamp | erfüllt |
| PL-03 | Abgeschottet ohne Netz | AppContainer ohne Fähigkeiten: Verbindung zu 127.0.0.1 und ins Internet scheitert; Gegenprobe ohne Container verbindet | Core `Real_sandbox_hostile` (echt) | Exercism `--network none` | erfüllt |
| PL-04 | Nur Temp-Ordner beschreibbar | Schreiben und Lesen außerhalb (auch im Benutzerordner) scheitert, im eigenen Arbeitsordner geht es; Gegenprobe Lesen ohne Container geht | Core `Real_sandbox_hostile` | – | erfüllt |
| PL-05 | Zeitgrenze, ein Prozess | Endlosschleife → „timeout“ nach 10 s (Test 3 s); kein weiterer Prozess (Job: ein Prozess) | Core `Real_sandbox_hostile` | Exercism (Zeitgrenze) | erfüllt |
| PL-06 | Fehler verständlich | Syntax-, Einrückungs-, Namens- und Importfehler mit einem Satz, Pfade gekürzt | Core `Real_tests_pass_and_fail`, UI `Code_timeout_error_drop` | Exercism (error) | erfüllt |
| PL-07 | Hinweise in Stufen | je Aufgabe 2 eigene Hinweise, nacheinander | UI (Bild), Core über `Hinweis` | Exercism hints | erfüllt |
| PL-08 | KI-Fachaktion | `hinweis.geben` → Vorschau → „Ja, behalten“ → Hinweis bei der Aufgabe, genau ein Rückgängig-Schritt; Code-Block/Funktionsdefinition/Link abgelehnt | UI `Code_ki_hint`, Core `Ki_hint_rules`, Echtlauf `Real_local_ki` | Codecademy KI-Hinweise | erfüllt |
| PL-09 | Bewertung nur durch Tests | die KI ändert weder Code noch Stand; „gelöst“ nur nach grünem Testlauf | UI `Code_ki_hint` (Versuche/gelöst unverändert), Echtlauf | Auftrag | erfüllt |
| PL-10 | Fortschritt | gelöst je Aufgabe, Versuche, Balken, Statuszeile | UI `Code_click_to_real_tests` | Exercism, freeCodeCamp | erfüllt |
| PL-11 | Fremdformat | Export „Lösung mit Tests (.py)“ läuft im System-Python (`Ran 3 tests … OK`); Import/Ablegen einer .py | UI `Code_click_to_real_tests`, `Code_timeout_error_drop`; Beleg `reports/T-0782/fremdprogramm-python.log` | Exercism (lokal arbeiten) | erfüllt |
| PL-12 | Kinderprofil | KI-Kette und „Hinweis von der KI“ nur lokal; .py ist keine gesperrte Endung | Code (`KiLocalOnly = () => _roleChild`) | T-0764 | teilweise (kein eigener UI-Test im Kinderprofil) |
| PL-13 | Block-Programmierung (Scratch-Art) | – | – | Scratch | fehlt |
| PL-14 | Weitere Sprachen (C#, JavaScript) | – | – | Exercism | fehlt (Python reicht für den Lernpfad) |
| PL-15 | Eingabe-Programme (input()) | – (kein Terminal; Tests rufen Funktionen auf) | – | Codecademy Terminal | fehlt |

## Bewusst nicht gebaut
- Kein Hochladen, keine Konten, keine Mentoren-Funktion; kein fremder Aufgabentext.
- Die KI schreibt keinen fertigen Code der Aufgabe und bewertet nicht.
