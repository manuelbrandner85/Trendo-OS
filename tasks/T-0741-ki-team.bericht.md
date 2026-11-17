# Bericht T-0741: KI-Team – Stopp, Fortschritt je Rolle, Werkzeuge

**Auftrag:** `tasks/T-0741-ki-team.md` (Restliste Rang 14, CORE-03 „KI-Team“).
**Branch/Arbeitskopie:** `claude/T-0741-ki-team`, frische Kopie von `main` (0.9.104) in `%TEMP%\trendo-auftrag-T-0741`.

## Was es schon gab (im Code nachgewiesen)

- `AgentCenter.RunAgentAsync` (`src/Trendo.Core/W2/AgentRuns.cs`, T-0698): Agenten-Schleife Plan → Werkzeug → Ergebnis mit Protokoll, Grenzen, Stopp über Token und Not-Stopp,
  Werkzeuge aus `AgentTools.All`, Wirkung nach außen über `ApprovalGate`.
- `AgentCenter.RunTeamFilesAsync` (T-0698): Team mit Datei-Übergabe, Prüfer parallel/zurückgeben; in der App über die Team-Knöpfe, Stopp-Knopf vorhanden.
  Lücken: kein Fortschritt je Rolle (nur eine Zeile „Läuft: …“), Stand nach Stopp nur als Meldung, **kein Fortsetzen** (`CanResume` kennt nur die alte Textkette),
  Team-Rollen nur „lesen/schreiben“, Freigabe-Frage eines Teams wurde auf der Seite „ki-rollen“ gar nicht angezeigt (lief bis zum Stopp ins Leere).
- `AgentCenter.RunTeamAsync`/`ResumeTeamAsync` (`Agents.cs`): alte Textkette (Audit-Befund) – bleibt unverändert für die Prompt-Kette (`ConnectedAdapters`) und alte Tests.
- `AssistChain` (`src/Trendo.Core/Programs/AssistChain.cs`) ist die Kette für KI-Aufträge im offenen Dokument – nicht das Team; nicht angefasst.

## Umsetzung (kein zweites Agentensystem)

- `src/Trendo.Core/W2/AgentTeam.cs` (neu, `partial class AgentCenter`): Team-Teil aus `AgentRuns.cs` hierher verschoben und erweitert.
  - `AgentTeamState`/`TeamRoleProgress`/`TeamRoleStatus`: gespeicherter Lauf-Stand `ki/team-laeufe/<id>.json` (atomar), je Rolle wartet/arbeitet/fertig/Fehler/gestoppt,
    Zwischenergebnis (aus dem Agenten-Protokoll), Ergebnis, Übergabe-Datei, Schritte, Runden, Werkzeuge.
  - `RunTeamFilesAsync(..., onProgress)` – gleiche Signatur wie vorher plus Fortschritts-Rückruf; `ResumeTeamFilesAsync`, `CanResumeTeam`, `TeamRuns`, `TeamRun`, `SaveTeamSnapshot`.
  - Stopp: gestoppte Rolle schreibt ihre Übergabe-Datei mit „Bisher erarbeitet“ (Werkzeugaufrufe/Ergebnisse); Lauf ist danach fortsetzbar – auch nach Neustart
    (ein „läuft“-Stand ohne lebenden Lauf gilt als „gestoppt“). Fortsetzen: fertige Rollen und Übergaben bleiben, weiter an der offenen Stufe (bei parallelen Prüfern nur die offenen).
  - Werkzeuge je Rolle (`TeamTools`): „Dateien lesen“ alle, „schreiben“ (Notiz/Datei) nur Rollen mit Schreibrecht, „Im Internet nachlesen“ nur Recherche-Rollen –
    jeder Abruf wird beim `ApprovalGate` angefragt (`agent.team.web`) und nur nach Zustimmung ausgeführt; ohne Person/bei „Nein“ blockiert das Tor, nichts wird abgerufen.
  - Rollen-Anweisung nennt die Zuständigkeit (Anlass: echter Erstversuch, siehe Belege).
- `src/Trendo.Core/W2/AgentRuns.cs` (gemeinsame Datei, minimal): Team-Block (79 Zeilen) nach `AgentTeam.cs` verschoben, dafür ein Verweis-Kommentar;
  eine Zeile im Werkzeug-Fehlerweg: `ApprovalException` → „blockiert“ (statt „Hat nicht geklappt“). Sonst unverändert (Agenten-Lauf, Abläufe-Schritt).
- `src/Trendo.App/MainWindow.AgentRun.cs`: Team-Start über gemeinsamen `TeamRunAsync` (Stopp-Token, Fortschritt live → `TeamLive`), `ResumeTeamFilesAsync`, `SaveTeamSnapshot`.
- `src/Trendo.App/MainWindow.Agents.cs`: Karte „Team-Fortschritt“ (je Rolle Zeichen, Zustand, Schritte, Runde, Zwischenergebnis, Übergabe-Datei, Werkzeuge als Tooltip),
  Freigabe-Karte „Zustimmen/Ablehnen“, „Zwischenstand speichern“, „Team-Lauf fortsetzen“; Karte bleibt nach Stopp/Neustart sichtbar (letzter Lauf des Projekts).
  Keine neue goldene Aktion („Rolle starten“ bleibt die eine Hauptaktion).
- Nicht angefasst: `IntelligenceCore`, `ApprovalGate`, `WorkflowEngine`, Abläufe-Schritt „Agent ausführen“ (Rang 17).

## Kommandos und Ergebnisse

- `dotnet build Trendo.sln -c Release` – 0 Fehler (Warnungen nur vorhandene, z. B. `Watermark` in `MainWindow.Agents.cs:233-235`, älter).
- Core gezielt (`reports/T-0741/rohlog-core-tests.txt`): AgentTeamT0741Tests 4/4, AgentRunTests 13/13, AgentResumeTests 3/3, CORE03_Agents 3/3, StudioTeamsTests 3/3,
  AgentAnswerTests 10/10, GameStudioUgsTests 6/6, T0005_NotStopp 2/2 – alle „Bestanden!“.
- App gezielt (`reports/T-0741/rohlog-app-tests.txt`): KiTeamT0741_Ui 2/2, AgentRun_Ui 5/5, CORE03_Ui 1/1, StudioTeams_Ui 2/2, ClaudeWork_Ui 3/3, T0717_HuelleE5_Ui 13/13,
  Modern_Ui 13/13 – 0 Failed.
- Echtlauf `KiTeamEchtlauf_Ui` mit `TRENDO_TEAM_ECHT` (Ollama, qwen3:14b): 1/1 bestanden, 121 s, Belege `reports/T-0741/` mit `SHA256.txt`.
- Rot-Grün: die neuen Tests nutzen neue Schnittstellen (`TeamRun`, `ResumeTeamFilesAsync`, `TeamLive`, Karte `team-progress`) – auf altem Code nicht übersetzbar (rot als Kompilierfehler, nicht als Testlauf).
- Kein Volltest (macht die Hauptsitzung).

## Abnahmekriterien

1. Leere Vorlage → echtes Ergebnis über die Oberfläche: **TEILWEISE** – leeres Projekt + Ziel → Team legt `einladung.md` an (Echtlauf); kein eigener Dokumenttyp „Team“.
2. Fremddatei importieren/exportieren, im Fremdprogramm öffnen: **FEHLT** (nicht Teil dieses Pakets; Eingabe bleibt das Zielfeld).
3. Prozessabbruch ≤ 30 s Verlust: **TEILWEISE** – Lauf-Stand wird bei jeder Fortschrittsänderung atomar gespeichert, nach Neustart „gestoppt“ und fortsetzbar (Test);
   die gerade arbeitende Rolle beginnt beim Fortsetzen neu (ihre geschriebenen Dateien bleiben). Echter Prozess-Kill nicht geprüft.
4. KI führt Fachaufgabe aus, Ergebnis im offenen Dokument, rückgängig: **TEILWEISE** – echte KI-Rollen mit Werkzeugen erzeugen echte Projektdateien; kein offenes Dokument, kein Rückgängig.
5. Test vom Klick bis zur echten Datei: **VOLLWERTIG** für den Ablauf Start → Stopp → Zwischenstand → Fortsetzen (UI-Test ersetztes Modell + Echtlauf mit Ollama über echte Klicks).
6. Einfach/Profi, Tastenkürzel, Rechtsklick, Ziehen und Ablegen: **FEHLT** für die neue Karte (keine eigenen Kürzel/Rechtsklick).

## Nicht geprüft

- Sichtprüfung im Bild: Die Seite „ki-rollen“ wurde mit `tools/Trendo.Render` gerendert (Leerzustand ohne Karte sieht unverändert aus); die Karte „Team-Fortschritt“ selbst
  ließ sich nicht ins Bild bringen (das Render-Werkzeug löscht seinen Datenordner beim Start, die Kopf-los-Tests liefern kein Bild). Inhalt der Karte ist über UI-Tests und
  die Bildschirmzeilen im Echtlauf-Log (`team-ablauf.log`) geprüft – Farben/Abstände nicht.
- „Im Internet nachlesen“ mit echtem Netz (in Tests ersetzt); Claude als Team-Modell; echter Prozessabbruch während eines Laufs; Prüfer-Rückgabe mit echtem Modell.
- Ein zweiter Echtversuch scheiterte ohne erhaltenes Protokoll (siehe `reports/T-0741/README.md`).

## Offene Punkte / Risiken

- „Rolle starten“ (Einzelrolle, alte Textkette `RunAsync`) hat weiter keinen Stopp.
- Eigene Teams zusammenstellen, Ergebnis je Rolle bearbeiten, Rolle einzeln neu laufen lassen (audit-4 Punkte 3–4) – nicht Teil dieses Pakets.
- Echte Websuche gibt es nicht: „Im Internet nachlesen“ liest eine genannte Adresse im Lesemodus (wie bisher `web.lesen`).
- Zusammenführung: `AgentRuns.cs` ist gemeinsam (Rang 17). Geändert sind nur der verschobene Team-Block am Dateiende und eine `catch`-Zeile in `UseToolAsync`.

**Status:** umgesetzt, gezielt getestet, Echtlauf belegt; nicht gemergt.
