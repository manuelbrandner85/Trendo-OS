# Bericht T-0717: B6 „Programm-Hülle für alle“ – Etappe E5 KI & Wissen

**Branch:** `claude/T-0717-huelle-e5` (vom Integrationszweig `claude/T-0685-b1-integration`, Stand 492505cb6), eigene Kopie in `%TEMP%\trendo-auftrag-T-0717`.
**Grundlage:** T-0708, T-0711, T-0714, Umzugsliste `docs/audit/2026-11-16-profi-audit/huelle-umzug.md` Abschnitt 4 (E5) und offener Punkt aus E2. Vom Nutzer freigegeben.

## Was war → was ist jetzt

| Programm | Vorher (Beleg) | Jetzt | Nachweis |
|---|---|---|---|
| Abläufe – neuer Ablauf | `_wfDraft` (Name, Auslöser, Zeit, Schritte) nur im Speicher (`MainWindow.Workflows.cs`) | Entwurf an der Notsicherung der Hülle (`LINK-ABLAEUFE-*.tdoc`, 20 s, atomar, Sperre), Rückgängig/Wiederholen (100) über dem Formular, Karte, „Ablauf speichern“ räumt weg; „+ Eigener Ablauf“ schließen verwirft wie vorher | UI `T0717_HuelleE5_Ui.Workflow_draft`; Absturz `T0717_HuelleE5Tests.Kill_while_autosaving_workflow_draft` (CrashProbe `link-ablauf`) |
| Abläufe – Bausteine | `_wfUndo` (50, ohne Wiederholen) | Rückgängig/Wiederholen der Hülle über `ItemUndo` (100), Strg+Z/Strg+Y, Menü. Freigabe unverändert (Rückgängig setzt nur den Inhalt zurück, wie vorher) | UI `Workflow_edits_undo_redo`; bestehend `WorkflowFlow_Ui`, `AblaufEditor_Ui`, `SYS05_Ui` |
| Produktion – Tafel | kein Rückgängig; Karten-Text erst beim Verlassen des Felds gespeichert; `ProjectStore.WriteFile` nicht atomar | Rückgängig/Wiederholen (100, `ItemUndo`), Getipptes nach der Tipp-Pause gespeichert (ohne Neuaufbau, Cursor bleibt), Tafel atomar | UI `Board_typing_undo_redo` (Fenster zu ohne Feld zu verlassen → Text ist da); Absturz `Kill_while_saving_board` (CrashProbe `tafel`, **rot auf altem Code**: halbe tafel.json) |
| Produktion – Angaben eines Ablaufs | `_fusionInput` nur im Speicher | Entwurf je Vorlage an der Notsicherung (`LINK-PRODUKTION-*`), Karte in beiden Reitern; nach dem Ausführen gilt als gespeichert. Bauplan.md, PROTOKOLL.txt, Geräteliste atomar. Freigabe „Plan freigeben …“ unverändert | UI `Fusion_inputs` |
| Lernen | neue Karte (Frage/Antwort) nur im Speicher; CSV-Export nicht atomar | Entwurf (`LINK-LERNEN-*`), Rückgängig, Karte; „＋ Karte“ räumt weg; Export atomar | UI `Learn_new_card` |
| KI fragen | Nachricht nur im Eingabefeld; „Als Dokument“ nicht atomar | Entwurf (`LINK-KIFRAGEN-*`), Rückgängig/Wiederholen neben „Senden“, Karte; Senden räumt weg; Export atomar | UI `Ask_draft` |
| KI-Rollen | Auftrag und neue Rolle nur im Speicher; eigene-rollen.json nicht atomar | zwei Entwürfe (`LINK-ROLLEN-*`, `LINK-ROLLE-*`), Rückgängig, Karte; Start bzw. Anlegen räumt weg; atomar | UI `Agent_goal`, `Agent_new_role` |
| Recherche | Markierungen und Bericht nicht atomar | atomar (Format unverändert) | Core `E5_stores_atomic_and_old_files` (alte markierungen.json) |
| Forensik | fall.json, bericht.md nicht atomar; Beweiskopie direkt unter dem Beweisnamen | **nur atomar** (begründet unten); Kopie erst als `.teil` im Fall-Ordner, geprüft, dann umbenannt | Core (Frist = 120 h, Speichern verlängert nicht, nach 120 h gelöscht, alte fall.json lesbar); UI `Forensics_not_backed_up` |
| Projekte | project.json, Projektdateien, Notiz nicht atomar | atomar | Core (alte project.json lesbar) |
| Aufgaben (aus E2) | `_todoUndoUid/_todoUndoBefore` ohne Grenze, ohne Wiederholen | Hülle über `ItemUndo` (100), Wiederholen-Knopf, Strg+Y, Menü | UI `Todo_undo_redo`; bestehend `TodoBoard_Ui`, `O1O6_PimUi` |
| Kalender (aus E2) | `_calUndo` 50, ohne Wiederholen; Eintragen über das Formular nicht rückgängig | Hülle (100), Wiederholen; Eintragen jetzt rückgängig | UI `Calendar_undo_redo`; bestehend `CalendarGrid_Ui` |
| Vorhaben (aus E2) | `_planUndo` ohne Grenze, ohne Wiederholen | Hülle (100), „↷ Wiederholen“, Menü; Beschriftung „↶ <Schritt>“ bleibt | UI `Plan_undo_redo`; bestehend `Gantt_Ui` |
| Menüleiste | – | „Bearbeiten“ mit Rückgängig/Wiederholen auf ablaeufe, produktion, lernen, ki-rollen, ki-fragen, aufgaben, kalender, vorhaben (über die Anschlüsse, **ohne Änderung an MenuBar.cs**) | UI `Menu_bar_on_new_pages` (Rückgängig über das Menü wirkt) |

### Kern: `ItemUndo` statt eigener Stapel (verlustfrei)
Die drei E2-Programme, die Abläufe-Bausteine und die Tafel speichern jede Änderung sofort. `src/Trendo.Core/Programs/ItemUndo.cs` legt je Änderung nur die berührten Elemente (Kennung → Stand, `null` = gibt es nicht) in einen `ProgramLink` (100 Schritte der Hülle) und liefert bei Rückgängig/Wiederholen genau die Elemente, die sich ändern. Nichts geht verloren gegenüber vorher: gleiche Wirkung je Schritt, dazu Wiederholen; einzige Grenze: Aufgaben/Vorhaben jetzt 100 statt unbegrenzt in der Sitzung (Standard der Hülle). Elemente, die außerhalb geändert wurden, bleiben unberührt (Core-Test). Keine Notsicherung nötig (nichts ungespeichert).

### Forensik: nur atomar (Begründung)
Eine Notsicherung eines Falls läge in `programme/sicherung/` – außerhalb des Fall-Ordners, also außerhalb der Löschung nach 120 h (`ForensicsLab.MaxLife`, CLAUDE.md). Die Forensik-Oberfläche hat ohnehin kein ungespeichertes Dokument (Fälle, Beweise, Bericht werden sofort geschrieben). Deshalb nur atomar; Frist, Warnung und Löschung unverändert. Die Zwischendatei `.teil` liegt im Fall-Ordner und wird mit dem Fall gelöscht.

## Rot-Grün
Neue UI-Tests nutzen Knöpfe/Mitglieder, die es vorher nicht gab (`wfe-undo`, `todo-redo`, `cal-redo`, `suite-plan-redo`, `big-undo`, `fusion-recovery`, `learn-new-recovery`, `ask-recovery`, `agent-goal-recovery`, `WfDraftSteps`, `ItemUndo`, CrashProbe-Modi `link-ablauf`/`tafel`) – auf altem Code nicht übersetzbar/rot. Gegenprobe gefahren: `Kill_while_saving_board` mit dem alten `File.WriteAllText` in `ProjectStore.WriteFile` → **rot** (`JsonReaderException`, halbe tafel.json), mit AtomicFile grün. Zwischenstände rot und behoben: `Board_typing_undo_redo` (Verlassen des Felds baute die Seite neu auf und schluckte den Klick auf „Rückgängig“ – Vergleich jetzt mit dem gespeicherten Stand), Menüleiste auf „vorhaben“ fehlte auf der Startseite (Anschluss jetzt beim Aufbau), CrashProbe `link-ablauf` traf zu selten in den Schreibvorgang (Umfang erhöht).

## Kommandos und Ergebnisse
- `dotnet build Trendo.sln -c Release` → 0 Fehler.
- App, ein Lauf mit 90 Klassen (Pflicht: `UebersichtEinfach_Ui`, `ProgramHeadDesign_Ui`, `DesignPaket3_Ui`, `DesignPaket4_Ui`, `T0708_HuelleEtappe1_Ui`, `T0711_HuelleEtappe1b_Ui`, `T0714_HuelleE2_Ui`, `T0710_MenuBar_Ui`, `FreigabenKernUi`, `T0713FreigabenNachtragUi`, `AssistChain_Ui`; dazu alle Klassen, die Abläufe, Produktion, Lernen, KI-Rollen, KI fragen, Aufgaben, Kalender, Vorhaben, Recherche, Forensik, Projekte berühren, `EveryButton_*`, `SimpleMode_Ui`, `EinfachBedienen_Ui`): **463 Tests, 3 rot, 1 übersprungen** (`reports/T-0717/app-tests.log`). Rot: `UebersichtEinfach_Ui.Simple_keeps_every_function` (aus diesem Paket: zwei Knöpfe mehr auf „KI-Rollen“ schoben in „Einfach“ Auftrag und Start hinter „Mehr anzeigen“ – behoben: Knöpfe neben „Rolle starten“, Seite in `NoPageMore`), `FilmSchnitt_Ui.Render_click_to_file_real` und `T0681_Ui.CodeEditor_laeuft_im_Hintergrund_und_bricht_ab` (nicht berührt; einzeln auf diesem Stand grün, je zweimal; `T0681` ist im gemeinsamen Lauf auch auf dem Integrationsstand rot geworden – zeitabhängig).
- Nachlauf nach den Korrekturen (21 Klassen inkl. aller Pflichtklassen, `T0717_HuelleE5_Ui`, KI-Rollen-, Einfach- und Menü-Klassen, `FilmSchnitt_Ui`, `T0681_Ui`): **126 Tests, 1 rot, 1 übersprungen** (`app-tests-nachlauf.log`) – rot nur `T0681_Ui.CodeEditor…` (zeitabhängig, s. o.). `UebersichtEinfach_Ui` 2/2 grün.
- Core gezielt (T0717, T0714, T0711, T0708, ProgramCrash, ProgramDocument*, ProgramLock*, **QA019**, SPEC12_Forensics, ResearchMarks, SYS08_Research, BigBoard, W5_X01_Fusion, W7, SYS05_Workflows, T0681_Ablaeufe, WorkflowSteps, CORE03_Agents, AgentRun, StudioTeams, SPEC06_*, LearnPlan, Fsrs, ChatStore, ProjectFiles, QA003_ProjectIsolation, Pim, Sys02_Alltag, W4_X11_Suite, CreationPlan/Mappe, IcsForeign*): **169 Tests, 165 grün, 4 übersprungen, 0 rot** (`core-tests.log`; übersprungen: CORE-04-Begleiter mit echtem Server, unabhängig). Abbruchtests: `link-ablauf` 10 harte Abbrüche, 7–8 mitten im Schreiben; `tafel` 10, 9–10 mitten im Schreiben; je höchstens der laufende Schritt verloren.
- Funktions-Baseline `python -m unittest discover -s tests/claude_handoff`: **11 Tests OK** (`baseline.log`).
- Bilder (1600×1000, angesehen): `SYS-05.png` (Abläufe: Rückgängig · Wiederholen, „Bearbeiten“ in der Titelleiste), `X-01.png` (Produktion: Menüleiste). Prüfsummen `reports/T-0717/SHA256SUMS.txt`. Kein Volltest.

## Abnahmekriterien (für die Programme dieser Etappe)
1. Aus leerer Vorlage ein echtes Ergebnis: **TEILWEISE** – nebenbei: echter Ablauf (`Workflow_draft`), echte Lernkarte, echte Tafel-Datei; nicht eigenes Ziel.
2. Fremddatei importieren/exportieren im Fremdprogramm: **NICHT GETESTET** (nicht Gegenstand).
3. Prozessabbruch verliert höchstens 30 s: **VOLLWERTIG** für Abläufe-Entwurf (echte harte Abbrüche, Weg der App: ProgramLink + Sperre) und Produktion-Tafel (echte harte Abbrüche, atomar, Tipp-Pause 1 s); übrige Entwürfe über denselben Weg (20 s + 1 s), je UI-Test „Fenster ohne Speichern zu → Karte → exakt zurück“; sofort speichernde Programme verlieren nichts. Ein harter Abbruch der App selbst ist nicht gefahren.
4. Claude/KI-Änderung rückgängig: **TEILWEISE** – KI-Nachricht und KI-Auftrag sind Entwürfe mit Rückgängig; eine KI-Änderung im Dokument selbst nicht mit echter KI geprüft.
5. Klick bis zur echten Datei: **TEILWEISE** – Tafel (`tafel.json`), Ablauf (`ablaeufe`-Datei), Lernkarte, Aufgaben/Kalender/Vorhaben-Dateien über Klicks; ohne Fremdprogramm.
6. Einfach/Profi, Kürzel, Rechtsklick, Ziehen: **TEILWEISE** – Strg+Z/Strg+Y auf Aufgaben, Kalender, Abläufe, Tafel; Menüleiste „Bearbeiten“; keine neuen Rechtsklick-/Zieh-Funktionen.

## Was NICHT geprüft wurde
- Kein Volltest; kein harter Abbruch des App-Prozesses selbst; kein echter KI-Lauf.
- Lernen: Löschen einer Karte hat weiter kein Rückgängig (Karten-Statistik müsste mit), Bearbeiten vorhandener Karten speichert beim Verlassen des Felds (unverändert).
- Projekt-Notiz speichert weiter beim Verlassen des Felds (nur atomar gemacht).
- Titel einer Tafel-Karte: nach der Tipp-Pause gespeichert, auf der Tafel erst nach dem Verlassen des Felds neu gezeichnet (wie vorher).

## Offene Punkte
- Siehe „Was NICHT geprüft wurde“. KI (Modell), Online-Dienste, Verbindungen, KI-Bibliothek, KI-Quellen, Internet-KIs: kein Dokument – Hülle nicht nötig (Liste „—“).

## Berührte Dateien
- neu: `src/Trendo.Core/Programs/ItemUndo.cs`, `src/Trendo.App/MainWindow.ItemLinks.cs`, `tests/Trendo.Core.Tests/T0717_HuelleE5Tests.cs`, `tests/Trendo.App.Tests/T0717HuelleE5UiTests.cs`, `tasks/T-0717-*`, `reports/T-0717/*`
- Core (atomar): `W3/ResearchMarks.cs`, `W3/Research.cs`, `W3/Forensics.cs`, `ProjectStore.cs`, `ProjectFiles.cs`, `Fusion.cs`, `W7/Fusion.cs`, `W2/Agents.cs`
- App: `MainWindow.Pim.cs` (Aufgaben, Kalender), `MainWindow.W4Suite.cs` (Vorhaben-Zeitplan), `MainWindow.Workflows.cs`, `MainWindow.W5Fusion.cs`, `MainWindow.Spec06.cs`, `MainWindow.Agents.cs`, `MainWindow.ChatWindow.cs`, `MainWindow.SimpleMode.cs` (**ein Eintrag** „ki-rollen“ in `NoPageMore`)
- Tests/Werkzeuge: `tests/Trendo.CrashProbe/Program.cs` (+2 Modi `link-ablauf`, `tafel`, additiv), `docs/audit/2026-11-16-profi-audit/huelle-umzug.md` (Kopfzeile, ein Absatz in Abschnitt 1, E5 in Abschnitt 4, Zeilen aufgaben/kalender/vorhaben/lernen/recherche/ablaeufe/produktion/ki-fragen/ki-rollen/projects/forensik)
- **Gemeinsame Dateien NICHT angefasst:** `ProgramLink.cs`, `MainWindow.LinkSlots.cs`, `MainWindow.ProgramLinks.cs`, `MainWindow.MenuBar.cs`, `MainWindow.ProgramShell.cs`, ApprovalGate, Abläufe-Freigabe. Die Menüleiste entsteht über die vorhandene Regel „Seite mit LinkSlot“.

**Status:** Etappe E5 umgesetzt und gezielt geprüft; nicht im Volltest.
