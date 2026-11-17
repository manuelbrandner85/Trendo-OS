# Bericht T-0698: Agenten laufen wirklich

**Branch:** `claude/T-0698-agenten` (abgezweigt von `claude/T-0685-b1-integration`), Arbeitskopie in eigenem Temp-Ordner.

## Wo angeknüpft wird (Architekturregel: kein zweites Laufzeitsystem, kein zweiter Scheduler)

| Baustein | vorhanden in | Nutzung in T-0698 |
|---|---|---|
| `AgentCenter` | `src/Trendo.Core/W2/Agents.cs` | zu `partial` gemacht; Agenten-Definitionen, Lauf-Schleife, Team-Lauf als neuer Teil `AgentRuns.cs` derselben Klasse. Aufgabenliste (`agenten.json`) jetzt atomar und threadsicher gespeichert (Team-Rollen laufen teils parallel). |
| `IntelligenceCore.AskAsync` | `src/Trendo.Core/W2/IntelligenceCore.cs` | einziger KI-Weg je Schritt. Modellwahl in der App nur über die Quellenliste (Haupt-KI = `Intel`; „lokal“ = nur Quellen ohne `IRemoteKiBackend`; „claude“ = nur `IMainKiBackend` = `ClaudeKiBackend` über das Abo). |
| `ApprovalGate` | `src/Trendo.Core/ApprovalGate.cs` | Wirkung nach außen (Datei hinauskopieren, Studio-Aktion mit Wirkung): `Request` → Person fragt in der Lauf-Ansicht → `Execute` nur nach `Approve`; ohne Zustimmung blockiert das Tor und schreibt `execution.blocked`. Not-Stopp (`HaltToken`) hält Agenten an. |
| `AuditLog` | `src/Trendo.Core/AuditLog.cs` | `agent.lauf.start/ende`, `agent.werkzeug.verweigert`, `file.write`, `agent.team.*`, `agent.stopp`. |
| `ProjectStore` | `src/Trendo.Core/ProjectStore.cs` | Arbeitsordner je Agent (`agent-<id>`), Pfadprüfung über `ResolveInProject` (Ausbruch abgewiesen). |
| `WorkflowEngine` (SYS-05, Stand T-0681) | `src/Trendo.Core/W2/Workflows.cs` | neue Schritt-Art `StepKind.AgentAusfuehren` (hinten angehängt, alte Dateien bleiben lesbar), Haken `RunAgent`/`AgentName` wie `Summarize`. Zeitplan, Neue-Datei-Auslöser, Wiederholung bei Fehler, Sperre gegen Doppelstart kommen unverändert aus der Abläufe-Maschine. Ein Ablauf mit Agent braucht Freigabe (`NeedsApproval`). |
| `StudioWorkflow` + Adapter | `src/Trendo.Core/PromptAction/StudioWorkflow.cs` | Werkzeug `studio.aktion`: `Start` → `Los`; wartet der Auftrag auf Freigabe, fragt der Agent über das Tor, erst dann `Approve`, sonst `Cancel`. |
| Recherche-Lesemodus | `ResearchDesk.Classify/ToReader`, `ReadOnlyWeb.GetPageAsync` | Werkzeug `web.lesen`: gleiche Prüfung wie die Recherche (Heimnetz/Tor abgewiesen, keine Downloads). |
| Mail-Entwurf | `MailCenter.ToEml` (SYS-09) | Werkzeug `email.entwurf`: `.eml` mit `X-Unsent: 1` im Projekt, nie gesendet. |

## Was war → was ist jetzt → Nachweis

1. **Ein Agent läuft** – war: „KI-Agenten bauen“ schrieb nur eine Agent-Datei, „Ausprobieren“ war ein Prompt an den Claude-Chat (Audit A1 Kriterium 12/13). Jetzt: `AgentCenter.RunAgentAsync` – Ziel, Anweisungen, Werkzeuge aus `AgentTools.All` (lesen, schreiben, web, studio, mail, export), Modell, Grenzen (1–50 Schritte, 1–120 Minuten). Schleife: KI antwortet mit JSON-Werkzeugaufruf → Trendo führt aus → echtes Ergebnis zurück. Jede Nutzung im Protokoll (`ki/agent-laeufe/<lauf>.json`), Ergebnis + Protokoll als `ergebnisse/<agent>-<zeit>-<id>.md` im Projekt. Nicht erlaubte Werkzeuge werden abgewiesen und protokolliert. Beleg: `AgentRunTests` (ERSETZTES Modell) + **echte Läufe** Ollama `qwen3:14b` und Claude-Abo (`reports/T-0698/`).
2. **Auslöser und Zeitplan** – war: keiner. Jetzt: Baustein „Agent ausführen“ in „Abläufe“ (Palette + Agent-Auswahl im Inspektor). Neue-Datei-Auslöser kopiert die Datei nach `eingang/` im Agentenprojekt. Läufe ohne Person können nichts nach außen geben (keine Zustimmung möglich). Beleg: `AgentRunTests.Workflow_step_runs_agent_on_new_file` (echte Abläufe-Maschine, ersetztes Modell), `AgentRun_Ui.Workflow_agent_step`.
3. **Team** – war: Textkette Rolle → Rolle. Jetzt: `RunTeamFilesAsync` – jede Rolle arbeitet als Agent im Projekt (lesen; schreiben nur mit Schreibrecht), Übergabe als Datei `team/<lauf>/<nn>-<rolle>.md`, Prüfer (`pruefer`, `sicherheit`, `*.pruefer`) laufen parallel und können mit „ZURÜCK: …“ an die letzte bearbeitende Stufe zurückgeben (höchstens 2-mal), Stopp jederzeit. Die Team-Knöpfe in „KI-Rollen und Teams“ nutzen das jetzt (ohne Projekt: Projekt „KI-Team“), mit Stopp-Knopf und Fortschritt. Die alte Textkette (`RunTeamAsync`/`ResumeTeamAsync`) bleibt für API und vorhandene Aufrufer. Beleg: `AgentRunTests.Team_*`, `AgentRun_Ui.Team_with_files` (ersetztes Modell).
4. **Nachweis** – siehe oben und `reports/T-0698/README.md`.
5. **Oberfläche** – „KI-Agenten bauen“: Bereich „In Trendo ausführen“ (Ziel, Werkzeuge als Häkchen, Modell, Grenzen) direkt unter dem Kurznamen; erster Schritt im Leerzustand (goldener Rahmen); Lauf-Ansicht rechts (Zustand, Stopp, Zustimmen/Ablehnen, Protokoll live, Ergebnisdatei); „Deine Agenten“ (Liste mit Laden/Entfernen, letzter Lauf); goldene Hauptaktion „Agent starten“ (vorher „Prüfen“). Im Bild geprüft: Leerzustand und Beispielvorlage gerendert (Trendo.Render, 1600×1000). Die Lauf-Ansicht mit Protokoll wurde nur über die UI-Tests (Steuerelemente/Texte) geprüft, nicht als Bild – Trendo.Render kann keinen Lauf vorbereiten.

## Kommandos und Ergebnisse

- `dotnet build Trendo.sln -c Release` → erfolgreich.
- `dotnet test tests/Trendo.Core.Tests -c Release --no-build --filter FullyQualifiedName~AgentRunTests` → **Bestanden 13/13**.
- Core gezielt (SYS05, CORE02/03, WorkflowSteps, T0681*, AgentResume, AgentAnswer, AgentRun, StudioTeams, PromptActionTests, KreativKits3, ConnectedPrompt) → **Bestanden 176/176**.
- App: `AgentRun_Ui` 5/5, `KreativStudios3_Ui` 5/5, `CORE03_Ui` 1/1, `StudioTeams_Ui` 2/2, `AblaufEditor_Ui` 1/1, `WorkflowFlow_Ui` 1/1, `SYS05_Ui` 1/1, `W2_Ui` 1/1, `T0681_Ui` 6/6 (einmal 1 Fehlschlag im Code-Editor-Hintergrundtest, bei Wiederholung grün – berührt meine Dateien nicht), `ProgramHeadDesign_Ui` 6/6.
- Echtläufe: `TRENDO_AGENT_ECHT=reports/T-0698 Trendo.App.Tests.exe -method …AgentEchtlauf_Ui.Real_ollama` → bestanden (91 s); `…Real_claude` → bestanden (33 s).
- Beim breiten Core-Filter fielen 4 vorbestehende Tests, die meine Dateien nicht berühren: `QA-019` (Core referenziert schon `System.Net.Http` über andere Dateien – ich habe den einen eigenen Verweis wieder entfernt), `SYS-09 Draft` (Zeilenenden `\r\n`, `MailCenter` unverändert), `SYS-13-P22` (NuGet-Lizenzliste), `assimp` (braucht gebautes TrendoOS vor dem Lauf).

## Was NICHT geprüft wurde

- Kein Volltest (macht die Hauptsitzung).
- Team-Lauf und Ablauf-Schritt nur mit ersetztem Modell, nicht mit Ollama/Claude.
- `web.lesen` nur mit ersetztem Abruf (kein echter Internetabruf im Test); `studio.aktion` mit echtem Adapter SPEC-11, aber ohne Adapter mit Wirkung nach außen.
- Ablauf „läuft ohne offenes Fenster“: Die Abläufe-Maschine läuft weiterhin nur, solange Trendo offen ist (SYS-05-Grenze, nicht Teil dieses Pakets).
- Lauf-Ansicht nicht als Bild (nur Leerzustand/Beispiel gerendert).

## Offene Punkte

- Ein Lauf zur Zeit je Fenster (bewusst, damit „Stopp“ eindeutig ist).
- Prüfer-Rückgabe erkennt „ZURÜCK“ am Anfang der Antwort – kleine Modelle halten sich nicht immer daran.
- Kleine lokale Modelle (z. B. `qwen2.5:1.5b`) wurden nicht genutzt; bestes vorhandenes Modell war `qwen3:14b`.

## Berührte Dateien

`src/Trendo.Core/W2/Agents.cs`, `src/Trendo.Core/W2/AgentRuns.cs` (neu), `src/Trendo.Core/W2/Workflows.cs`, `src/Trendo.App/MainWindow.AgentRun.cs` (neu), `src/Trendo.App/MainWindow.Agents.cs`, `src/Trendo.App/MainWindow.KreativStudios3.cs`, `src/Trendo.App/MainWindow.Workflows.cs`, `tests/Trendo.Core.Tests/AgentRunTests.cs` (neu), `tests/Trendo.App.Tests/AgentRunUiTests.cs` (neu), `tests/Trendo.App.Tests/AgentEchtlaufUiTests.cs` (neu), `reports/T-0698/*`, `tasks/T-0698-agenten*.md`.

## Abnahmekriterien

1. Aus leerer Vorlage echtes Ergebnis über die Oberfläche: **VOLLWERTIG** – leere Vorlage → Kurzname + Ziel → „Agent starten“ → echte Datei (UI-Test ersetztes Modell; echte Läufe Ollama/Claude über denselben Knopf).
2. Fremddatei importieren/bearbeiten/exportieren, Export im Fremdprogramm: **TEILWEISE** – Agent-Datei (.md) Einlesen/Export bestand schon; der Agent erzeugt `.eml`-Entwürfe und Markdown, Öffnen im Fremdprogramm nicht geprüft.
3. Prozessabbruch verliert höchstens 30 s: **TEILWEISE** – Lauf, Protokoll und Projektdateien werden nach jedem Schritt atomar geschrieben; ein abgebrochener Lauf erscheint als „Unterbrochen“; der Lauf selbst wird nicht fortgesetzt.
4. KI führt Fachaufgabe aus, Ergebnis im Dokument, rückgängig machbar: **TEILWEISE** – Ergebnis als Datei im Projekt; Rückgängig gibt es nur für die Agenten-Einstellungen im Dokument, nicht für Dateien, die der Agent geschrieben hat.
5. Test vom Klick bis zur echten Datei: **VOLLWERTIG** (`AgentRun_Ui.Start_creates_real_file`; echt: `AgentEchtlauf_Ui`).
6. Einfach/Profi, Tastenkürzel, Rechtsklick, Ziehen und Ablegen: **TEILWEISE** – Befehle in der Programm-Hülle (Einfach/Profi wie bisher); keine eigenen Kürzel, kein Rechtsklick, kein Ziehen in der Lauf-Ansicht.

**Rust:** nicht eingesetzt – C#/.NET-Erweiterung bestehender Module, kein messbarer Vorteil.
**Status:** IMPLEMENTED, gezielt getestet; Volltest steht aus.
