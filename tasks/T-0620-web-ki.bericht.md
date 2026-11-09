# Bericht T-0620: Webseite bauen, KI-Grundlage (Stufe 1)

**Auftrag:** `tasks/T-0620-web-ki.md`, Vorgabe `docs/nachbau/web-ki/anforderungen.md`: A1, A3, A6, A7, K4, K1, K2, K5.
**Branch/Worktree:** `claude/T-0620-web-ki`, `C:\Users\<user>\TrendoEntwicklung\wt-T-0620-web-ki`, Basis 9cff19506.
**Status:** Alle acht Funktionen gebaut, jede Prüfung der Liste hat einen bestandenen Test. PR: manuelbrandner85/Trendo-OS#284. Nicht in `main`, kein Release, nichts veröffentlicht. QA-Prüfung: bestanden mit Empfehlungen, nach zwei nicht bestandenen Runden.

## Was gebaut wurde

| ID | Ergebnis | Belege (Tests) |
|---|---|---|
| A1 | 11 benannte Aktionen in `WebDesignActions`. Die KI liefert einen JSON-Plan, der ganz oder gar nicht läuft. Mensch und KI nutzen denselben Weg (`aktion`-Nachricht des Editors, `WebDesignHumanAction`). | `WebDesignKiTests`: gleicher Plan gleiches Ergebnis, alle Aktionen, 23 Ablehnungsfälle, Bilder nur aus dem Projekt, nichts geschrieben, Bausteine wie im Editor |
| A7 | Jede Aktion nennt ihren Bedarf. Claude nur über das Abo im installierten Claude Code (`IMainKiBackend`), sonst lokal, KI-Dienste mit API-Schlüssel sind ausgeschlossen. Gefragt wird über `IntelligenceCore`, kein zweiter Kern. | Core: Bedarf, Wahl, lokal ohne Claude, API nie benutzt. UI: lokale KI wird genannt, Hinweis ohne KI, Hinweis ohne Bild-KI |
| A3 | `webdesign.md` als Grundregeln, 7 Fachgebiete mit Stichworten, `SkillLibrary.ForTask`. Lokal höchstens 2400 Zeichen, ganze Zeilen, ohne Checkliste. Eigene Formulierungen. | 7 Aufgaben laden je das richtige Fachgebiet, Grenze eingehalten, Fachgebiete nur bei passender Aufgabe |
| K4 | Satz oder eigenes Bild ergibt Editor-Daten, Vorschau, Übernehmen als ein Schritt. | Ungültige Antwort abgelehnt, Ergebnis lässt sich öffnen und ändern, Klickweg mit Rückgängig, echtes Bündel in Chrome öffnet die Daten |
| K1 | „Auswählen“ im Editor, auch mehrere Bausteine. Die KI sieht und ändert nur die Auswahl, Vorschau, ein Rückgängig-Schritt. | Nur Auswahl geändert, außerhalb abgelehnt, Verwerfen lässt die Seite gleich, echtes Bündel in Chrome |
| K2 | Doppelklick auf Text ändert ihn direkt, ohne KI, über `element_aendern`. | Gespeicherte Daten enthalten den Text, keine KI gefragt, echtes Bündel in Chrome mit echten Mausereignissen und Tastatur (Doppelklick, richtiges Feld, Enter, Klick daneben, Escape) |
| K5 | Benannte und automatische Stände, Varianten mit Bildern nebeneinander, Übernehmen mit Sicherung. | Wiederherstellen bytegleich, automatischer Stand vor KI-Änderung, Variante ändert das Original nicht, Stand einer Variante, Grenzen |
| A6 | Prüfung auf dem Export: 3 Breiten in echtem Chrome, Konsole, zu breiter Inhalt, defekte Verweise, `WebsiteQuality` und `AxeCheck` auf der gezeichneten Seite, höchstens 3 Nachbesserungen. Bildbewertung ist als Hinweis gekennzeichnet. | Eingebaute Fehler gefunden, Obergrenze, gezielte Nachbesserung, ungültige Nachbesserung verworfen, Lauf in echtem Chrome |

Beim Bauen behoben: Der Export nahm keine Projektbilder mit, die fertige Seite hatte kein `<main>` und meldete nie „fertig“, wodurch jedes Bild 8 Sekunden wartete. `WebsiteStudio.Pack` nimmt `.trendo-*` nicht mehr ins Paket.

## Geänderte Dateien

Neu: `src/Trendo.Core/Website/WebDesign{Actions,Check,Ki,States}.cs`, `src/Trendo.Core/Skills/webdesign-*.md` (7), `src/Trendo.App/MainWindow.WebDesignKi.cs`, `src/Trendo.App/WebDesignAudit.cs`, `tests/Trendo.Core.Tests/WebDesignKiTests.cs`, `tests/Trendo.App.Tests/WebDesignKiUiTests.cs`, `tools/webdesign-editor/pruefe-auswahl.mjs`, `docs/build-log/2026-11-09-0300-T-0620-web-ki.md`, `docs/nachbau/web-ki/anforderungen.md`, `docs/nachbau/web-ki/anforderungen-ergaenzung.md` (von der Spiele-Sitzung, nur Liste für Stufe 2 und 3).
Geändert: `src/Trendo.Core/Skills/webdesign.md`, `src/Trendo.Core/W2/Skills.cs`, `src/Trendo.Core/Website/WebDesign.cs`, `src/Trendo.Core/Website/WebsiteStudio.cs`, `src/Trendo.App/MainWindow.WebDesign.cs`, `src/Trendo.App/WebsitePreview.cs`, `src/Trendo.App/Assets/webdesign/*`, `tools/webdesign-editor/{README.md,src/main.jsx,src/trendo.css}`, `tests/Trendo.Core.Tests/SkillsTests.cs` (neue Skill-Liste und Zahl der Skills für Claude Code).

## Kommandos

```
npm ci --ignore-scripts; npx vite build; node lizenzen.mjs   (tools/webdesign-editor, 78 Pakete, nicht freizügig: keine)
dotnet test Trendo.sln -c Release
python -m unittest discover -s tests/claude_handoff
```

## Testergebnis (letzter Volltest, Exitcodes 0)

| Projekt | Bestanden | Übersprungen | Fehler | Gesamt |
|---|---|---|---|---|
| Trendo.Api.Tests | 16 | 0 | 0 | 16 |
| Trendo.Core.Tests | 1995 | 5 | 0 | 2000 |
| Trendo.App.Tests | 898 | 4 | 0 | 902 |
| tests/claude_handoff | 9 | 0 | 0 | 9 |

Die beiden Prüfungen in echtem Chrome liefen und wurden nicht übersprungen. Die Prüfung des Bündels nutzt echte Mausereignisse und echte Tastatur.

Zwei Befunde auf dem Weg, beide behoben: Die Paket-Tests schlugen fehl, solange `tools/webdesign-editor/node_modules` im Worktree lag (Treffer in einem Fremdpaket); der Ordner und `dist` sind wieder entfernt. Elf UI-Tests schlugen reihenfolgeabhängig fehl, weil Browser-Tests in derselben Klasse `MainWindow` auf dem falschen Thread initialisierten; sie stehen jetzt in einer eigenen Klasse.

## QA-Prüfung

Unabhängige QA- und Sicherheitsprüfung in drei Runden, Ergebnis im PR.

- Runde 1, nicht bestanden: K2 ging mit echter Maus nicht (der Test nutzte ein künstliches Ereignis), Datenverlust bei offener KI-Vorschau und Fassungswechsel, ungesicherter Entwurf, ein Plan konnte beliebige Projektdateien in den Export ziehen und `editor.js` ersetzen, Variantendateien gingen ungeprüft in `InvokeScript`. Alle fünf sind behoben, mit Tests belegt, dazu die meisten Empfehlungen (fehlerhafte Pläne, Schemaprüfung, Symlinks, Audit, Stichworte).
- Runde 2, nicht bestanden: Die KI-Sperre blieb nach einer Ausnahme hängen. Behoben, Regressionstest ergänzt, dazu fünf Empfehlungen.
- Runde 3, bestanden mit Empfehlungen, nur am Code geprüft, weil gleichzeitig der Volltest lief. Offen bleibt: Ein langsamer Doppelklick (über 240 ms Abstand) wählt den Baustein zusätzlich aus oder ab; das Bearbeiten selbst funktioniert.

## Artefakte

Kein Bericht unter `reports/`, kein Statuswechsel in `requirements.yaml`. Editor-Bündel: `src/Trendo.App/Assets/webdesign/`.

## Was NICHT geprüft wurde

- Kein Lauf mit echtem Claude und keiner mit einem echten lokalen Modell. Alle KI-Antworten in den Tests sind nachgebildet. Ob Modelle gute Pläne liefern, ist offen; gerade kleine lokale Modelle könnten oft abgelehnt werden.
- Der Weg „Claude liest das Bild aus seinem Arbeitsordner“ ist gebaut, aber nie live gelaufen. Dasselbe gilt für die Bildauswertung über Ollama.
- Die App wurde nicht im Fenster bedient. Die Oberfläche ist headless getestet, das Bündel in Chrome, nicht im `NativeWebView` der App. `invokeCSharpAction` war dabei nachgebildet.
- `AxeCheck` lief im Chrome-Test mit nachgebildetem Ergebnis, die echte Regelprüfung über Node nicht.
- Linux-USB und Pi 4 nicht geprüft.
- „Varianten nebeneinander ansehen“ erzeugt die Bilder nur außerhalb des Testmodus und hat keinen automatischen Test.
- Offen ist, ob das Umhängen des Browserfensters bei `Show("dev")` beim Auswählen im echten Fenster den Fokus nimmt.

## Offene Punkte und Grenzen

- Ziehen und Eigenschaftsfelder im Editor laufen weiter über Puck und `WebDesign.Validate`, nicht über die Aktionen. Beide enden in denselben geprüften Daten, sind aber nicht derselbe Code.
- `ansicht_anpassen` legt nur die Geräte-Ansicht fest, in der der Editor öffnet. Werte je Ansicht kommen mit W1 (Stufe 2).
- `seite_anlegen` ersetzt die eine Seite. Mehrere Seiten sind Voraussetzung von Stufe 3.
- Der Bedarf `Code` ist in der Wahl vorhanden, aber keine Aktion nutzt ihn, weil A1 rohen Code ablehnt.
- Playwright und Chrome DevTools MCP aus A6 sind nicht eingebunden; geprüft wird lokal über Trendos Vorschau.
- Die Bildbewertung nutzt nur lokale Modelle, nicht Claude.
- Sprungziele: Bausteine haben keine Ankerziele. Jeder Link wie `#kontakt` wird daher korrekt als defekt gemeldet, auch beim Standardwert des Knopfs im Editor.
- Der Export rendert weiter per Skript (W3, Stufe 3). Darum läuft die Inhaltsprüfung auf der gezeichneten Seite.
- `origin/main` ist seit der Basis um vier T-0502-Commits weiter; sie berühren keine Datei dieses Auftrags.
- Merge nach `main` und Release nur nach ausdrücklichem OK.

## Rust

Nicht eingesetzt. Die Arbeit liegt in JSON-Prüfung, Oberfläche und Browser-Steuerung ohne Engpass; bestehender Code ist C# und JavaScript.
