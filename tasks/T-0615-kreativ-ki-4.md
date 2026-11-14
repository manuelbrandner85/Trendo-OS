# T-0615: Etappe „Kreativ & KI“ – Teil 4

**Freigabe:** 79 neue Studios, Ja im Chat am 09.11.2026 (zweite Runde); „alles Offene durchziehen“ am 14.11.2026.
**Zweig:** `claude/T-0615-kreativ-ki-4` (von `origin/main` cce5c6261, Studio-Bilder liegen dort schon)

| Studio | ID | Bereich | Programm |
|---|---|---|---|
| Code mit KI | STUDIO-CODEAI-01 | KI & Automatik | `code-ki-programm` |
| Bilder erkennen | STUDIO-VISION-01 | KI & Automatik | `bilder-erkennen-programm` |
| Recherche mit Quellen | STUDIO-CITE-01 | Wissen & Lernen | `quellen-programm` |
| Tagesplaner | STUDIO-DAYPLAN-01 | Arbeiten & Büro | `tagesplaner-programm` |

Vorhandenes nutzen statt doppelt bauen: Projektbaum/Editor des Code-Editors (`CodeProject`, AvaloniaEdit, `CodeOpenFolder`), `Cli.Run` mit Zeitlimit, Claude-Leiste (`ClaudeFromStudioBar`, Anhänge über `ClaudeChatAttach`), Texterkennung (`Ocr`, Tesseract aus den Zusatzprogrammen), Recherche-Studio SYS-08 (`ResearchDesk.SessionSources`/`Highlights`), Kalender und Aufgaben SYS-09 (`Calendar.Occurrences`/`Todos`).

## Fertig, wenn
- Kern `KreativKits4.cs` (CodeAiKit, VisionKit, CiteKit, DayPlanner) mit Unit-Tests, Adapter `KreativPromptAdapters4.cs`, Programme `MainWindow.KreativStudios4.cs`, UI-Tests.
- Pflicht-Stellen (Katalog, Symbole, Fähigkeiten, Profile, Teams, Wissen, Chips, requirements.yaml, Adapter, Programmkatalog, Matrix) und Zähl-Tests angepasst.
- Gezielte Tests grün; unabhängige Durchsicht, Befunde behoben.

## Grenzen
- Befehle (Tests) nur nach zweitem Klick, im Projektordner, mit Zeitlimit; nichts wird ohne Rückfrage überschrieben.
- Bilder: alles lokal, keine Gesichts-/Personenerkennung; Claude nur über den Zugang der Person.
- Quellen: keine erfundenen Quellen; Claude prüft nur auf Wunsch.
- Tagesplaner trägt nichts selbst in den Kalender ein (Kalenderdatei zum Import).
- Nicht nach `main`, kein Merge, kein Release.
