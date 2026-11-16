# Audit Gruppe 4 – „KI & Automatik“ und „Wissen & Lernen“

Stand: Arbeitskopie `studios-aufraeumen` (ba52927a8), nur gelesen – nichts gebaut, keine Tests gestartet. Belege als `pfad:zeile` relativ zum Repo. Die Datei besteht aus dieser Zusammenfassung, Teil C (SYS-05, X-01) und den Teilen A1, A2, B (je Studio Kriterientabelle 1–15). Laufzeitverhalten wurde nirgends beobachtet; „echt getestet“ heißt: Test ohne Ersatz (`*Override`) oder Beleg unter `reports/`.

## Übersicht

| Studio-Id | Name | Programme | Gesamturteil | größte Lücke | Aufwand |
|---|---|---|---|---|---|
| CORE-02 | KI fragen | ki-fragen, ki, claude-verbinden, keys, verbindungen | TEILWEISE | Chat ohne Dateianhang/Mitschreiben/Neu-Erzeugen; zweite schwächere Chat-Seite „ki“; kein echter Ende-zu-Ende-Test | mittel |
| CORE-03 | KI-Team einsetzen (in CORE-02) | ki-rollen | TEILWEISE | „Team“ ist eine Textkette Rolle → Rolle, ohne Werkzeuge, Parallelität, Stopp, Zeitplan | groß |
| CORE-12 | KI-Anweisungen schreiben (in CORE-02) | ki-vorlagen, ki-skills | TEILWEISE | Formular statt Editor; Vorlagen ohne Import/Export; Skill-Wirkung in Claude Code nie echt geprüft | mittel |
| X-05 | Kostenlose KI finden (in CORE-02) | ki-bibliothek, ki-quellen, ki-internet | TEILWEISE (am weitesten) | Modell-Dateien ohne feste Prüfsumme; Download/Start in Tests ersetzt | mittel |
| STUDIO-SUMMARY-01 | Texte zusammenfassen | zusammenfassen-programm | TEILWEISE | Satzauswahl nach Worthäufigkeit statt Sprachmodell; kein Dateiimport | mittel |
| STUDIO-IMAGEAI-01 | Bilder mit KI | bild-ki-programm | TEILWEISE | sd-cli verdrahtet, aber nie echt getestet; nur Text→Bild; KI-Aktion schreibt nur bildauftrag.txt | mittel |
| STUDIO-VIDEOAI-01 | Videos mit KI | video-ki-programm | TEILWEISE | Diashow aus Standbildern ohne Ton statt KI-Video; „Szenen planen“ ist ein Parser | groß |
| STUDIO-AGENTS-01 | KI-Agenten bauen | agenten-bauen-programm | TEILWEISE | Schreibt nur eine Agent-Datei; kein Lauf, Zeitplan, Auslöser, Protokoll | groß |
| STUDIO-MODELS-01 | KI-Modelle vergleichen | modelle-vergleichen-programm | TEILWEISE | Nur Einzelfragen von Hand; keine Fragenreihe/automatische Bewertung | mittel |
| STUDIO-TRAIN-01 | Eigene KI trainieren | ki-trainieren-programm | TEILWEISE | Trainiert echt, aber nur Naive Bayes; Modell wird nirgends genutzt | mittel / sehr groß (Feintuning) |
| STUDIO-CODEAI-01 | Code mit KI | code-ki-programm | TEILWEISE | „Mit Claude ändern“ ändert keine Datei (Claude läuft nicht im Projektordner, kein Übernehmen) | mittel |
| STUDIO-VISION-01 | Bilder erkennen | bilder-erkennen-programm | TEILWEISE | Bildprüfung (EXIF, Farben, Schärfe) statt Erkennung; OCR nur mit Tesseract, ungetestet | mittel–groß |
| SYS-05 | Aufgaben automatisieren | ablaeufe | TEILWEISE | Nur 7 Datei-Schritte, läuft nur bei offenem Trendo, keine Wiederholung nach Fehler; KI-Aktion nur Plan; n8n nie echt geprüft | groß |
| X-01 | Großprojekte umsetzen (in X-11) | produktion | TEILWEISE | 4 fest verdrahtete Ketten, nicht änderbar; Tafel ist Handarbeit, „KI-Rolle“ nur Etikett; KI-Aktion nennt nur Studionamen | groß |
| CORE-10 | Wissen merken | ki-wissen | TEILWEISE | Textfeld statt Markdown-Editor; kein Export in der Oberfläche; nur Stichwortsuche | mittel |
| SPEC-05 | Messwerte auswerten | wissenschaft (+ SPEC-10 zwilling eingegliedert, nicht geprüft) | TEILWEISE (nah an vollwertig) | CSV nur über eingetippten Pfad; kein PNG/PDF/XLSX | mittel |
| SPEC-06 | Lernen mit Karteikarten | lernen | TEILWEISE | Nur Textkarten, kein Anki-Import, kein Rückgängig, außerhalb der Programm-Hülle | mittel |
| STUDIO-CITE-01 | Recherche mit Quellen | quellen-programm | TEILWEISE (nah an vollwertig) | Kein DOI/ISBN-Import, kein RIS/DOCX, 3 Quellenarten | mittel |
| SYS-08 | Sicher recherchieren | recherche | TEILWEISE | Keine Suche, Sitzung wird bei jedem Start gelöscht; Netzabruf in Tests ersetzt | mittel–groß |
| SPEC-07 | Texte übersetzen | – | ABGELEHNT (nur Vermerk, `src/Trendo.App/StudioCatalog.cs:145`) | – | – |

**Zahl je Gesamturteil:** 19 Studios geprüft → 0 VOLLWERTIG, 19 TEILWEISE (davon 3 nah an vollwertig: STUDIO-CITE-01, SPEC-05, X-05), 0 reine Attrappe; SPEC-07 abgelehnt. Auf Kriterienebene sind die Urteile nicht ausgezählt. X-11 „vorhaben“ (nimmt X-01 auf) gehört zu einer anderen Gruppe und wurde nicht geprüft.

## Antworten auf die Kernfragen

- **Was läuft real?** Lokale KI über Ollama/OpenAI-kompatible Server auf Loopback, Claude über den Prozess des installierten Claude Code (`claude -p`), Internet-Dienste per HTTPS mit Schlüssel im Windows-Speicher, Bild-KI über sd-cli, Videobau über FFmpeg, Training (Naive Bayes) im eigenen Code – Belege in Teil B und A1. Ohne erreichbare KI kommt eine Fehlermeldung, kein Scheintext (`IntelligenceCore.cs:102-127`, siehe Teil B).
- **Was ist nur Text/Plan?** Die KI-Aktionen (Prompt-Kette) dieser Gruppe fast durchweg: `ablauf.planen` (nur Plan, `src/Trendo.Core/PromptAction/SystemPromptAdapter.cs:201-209`), `ablauf.starten` gesperrt (`:289`), `studios.nennen` (`:138-156`), `bild.auftrag`, `video-ki.szenenplan`, `modelle.fragebogen` (Teil A1). Claude-Ergebnisse aus den Studios landen im Chat, nicht im Dokument (Teil A1/A2).
- **Laufen Agenten/Abläufe wirklich?** Abläufe (SYS-05): ja – Minutentakt, drei Auslöser, Freigabe, Not-Stopp, Protokoll; aber nur solange Trendo offen ist und ohne Wiederholung nach Fehlern (Teil C). KI-Team: echte Modellaufrufe nacheinander als Textkette, kein Zeitplan/Auslöser (Teil B). „KI-Agenten bauen“: es läuft kein Agent (Teil A1).
- **Trainiert „Eigene KI trainieren“ wirklich?** Ja, multinomiales Naive Bayes mit Aufteilung, Verwechslungstabelle und Modell-JSON (`src/Trendo.Core/Kreativ/KreativKits3.cs:434-567`), echt getestet – aber kein neuronales Modell, und das Modell wird nirgends in Trendo benutzt (Teil A1).
- **kie.ai:** In den Studios dieser Gruppe nur als Feld/Verweis gefunden (z. B. Videos mit KI, `src/Trendo.App/MainWindow.KreativStudios3.cs:249-254`); der kie.ai-Weg selbst wurde hier nicht geprüft.

---

# Teil C – Abläufe und Großprojekte

## SYS-05 Aufgaben automatisieren (Programm „ablaeufe“)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE | Senkrechte Baustein-Kette (Auslöser → Schritte → Ende), Bausteine anklickbar und per Ziehen umsortierbar (`src/Trendo.App/MainWindow.Workflows.cs:233-292`, Ziehen `:275-280`), Eigenschaften rechts als Felder (`:307-341`). Keine Verzweigung, keine freie Fläche. Neuer Ablauf über Formular (`:162-176`). Profi-Ebene ist der fremde n8n-/Node-RED-Editor (`:145`, `src/Trendo.App/MainWindow.N8n.cs:213 ff.`). |
| 2 | Werkzeuge real | TEILWEISE | 7 Schrittarten, alle echt ausgeführt: Ordner anlegen, Kopieren, Verschieben, Datum voranstellen, Textdatei, ZIP, KI-Zusammenfassung (`src/Trendo.Core/W2/Workflows.cs:9`, `:180-222`). Es fehlen Bedingung, Schleife, Programm starten, Web-Abruf, Mail, Meldung – nur Dateiarbeit. |
| 3 | Neues Projekt | VOLLWERTIG | 4 Vorlagen (`Workflows.cs:89-99`), „+ Eigener Ablauf“ (`MainWindow.Workflows.cs:96`). |
| 4 | Import | FEHLT | Kein Import eines Trendo-Ablaufs aus einer Datei gefunden. Nur n8n-Dateien (`*.n8n.json`, von Claude abgelegt) werden gelesen und geprüft (`MainWindow.N8n.cs:205-210`). |
| 5 | Bearbeiten | VOLLWERTIG | Auslöser und Schritte änderbar, verschieben, löschen (`MainWindow.Workflows.cs:307-341`, `:224-228`); Änderung verlangt neue Freigabe (`:213`, `Workflows.cs:35-37`). |
| 6 | Rückgängig | TEILWEISE | 50 Schritte, Strg+Z und Knopf (`MainWindow.Workflows.cs:128`, `:144`, `:206-221`); nur im Speicher, kein Wiederholen. „Löschen“ eines Ablaufs wirkt sofort, ohne Nachfrage und ohne Rückgängig (`:113`). |
| 7 | Vorschau | VOLLWERTIG | Probelauf ohne Änderung mit Ergebnis je Baustein (`Workflows.cs:155-164`, `MainWindow.Workflows.cs:41-51`); Test `tests/Trendo.App.Tests/AblaufEditorUiTests.cs:20-42`. |
| 8 | Speichern | TEILWEISE | Jede Änderung wird sofort geschrieben (`Workflows.cs:110-112`), aber nicht atomar (`:261`, `File.WriteAllText`); eine beschädigte Datei wird still als leere Liste gelesen (`:86`) – alle Abläufe wären weg. |
| 9 | Absturz | TEILWEISE | Sperrdatei je Lauf, verwaiste Sperre nach 2 h gelöst (`Workflows.cs:57-73`), Test `tests/Trendo.Core.Tests/W2Tests.cs:230-252`. Kein Fortsetzen eines halb gelaufenen Ablaufs. |
| 10 | Export | TEILWEISE | Nur Umwandlung in einen n8n-Ablauf (`src/Trendo.Core/Automation/N8n.cs:126-136`) und Speichern als `.n8n.json` (`MainWindow.N8n.cs:187-188`). Kein eigener Export eines Trendo-Ablaufs. |
| 11 | Rundreise | FEHLT | Kein Export→Import-Weg für Trendo-Abläufe vorhanden. |
| 12 | Fachfunktion real | TEILWEISE | Echt: Zeitgeber jede Minute (`MainWindow.Workflows.cs:60-62`), Auslöser täglich/Intervall/neue Datei (`Workflows.cs:129-152`), Freigabepflicht für Verschieben/Umbenennen (`:36`, `:159`), Not-Stopp (`:160`, `:172`), nur erlaubte Ordner (`:122-126`, `:177`), Protokoll `protokoll.jsonl` (`:248-252`), kein Doppelstart (`:161`). Grenzen: läuft nur, solange Trendo offen ist (kein Dienst, keine Windows-Aufgabe; `src/Trendo.Api/AutomationApi.cs:113`); nach einem Fehler wird `LastRun` trotzdem gesetzt (`Workflows.cs:230`) – ein täglicher Ablauf wird erst am nächsten Tag wieder versucht, keine Wiederholung; ein fehlgeschlagener Lauf (Ok=false) setzt den roten Hinweis nicht, der kommt nur bei Ausnahmen (`MainWindow.Workflows.cs:25-31`), sichtbar nur als „mit Fehler“ in der Liste (`:119`); „neue Datei“ wird im Minutentakt abgefragt. |
| 13 | KI arbeitet mit dem Programm | BLOCKIERT | `ablauf.planen` schreibt nur einen Plan-Text, erzeugt keinen Ablauf (`SystemPromptAdapter.cs:201-209`, `src/Trendo.Core/PromptAction/CapabilityCatalog.cs:79` „Nur der Plan“); `ablauf.starten` ist gesperrt („nicht angeschlossen“, `SystemPromptAdapter.cs:289`). Real: KI-Schritt „Zusammenfassen“ über `Intel.AskAsync` (`MainWindow.Workflows.cs:15`); Claude kann n8n-Dateien ablegen, Trendo prüft sie und legt sie nach „Ja“ an (`N8n.cs:143-158`, `MainWindow.N8n.cs:183-193`). |
| 14 | Tests | TEILWEISE | Echt (echte Dateien im Temp-Ordner): `W2Tests.cs:205-349` (Vorlagen, Auslöser, Freigabe, Not-Stopp, Sperre). Zeitplan in der Oberfläche nur mit Ersatz (`tests/Trendo.App.Tests/WorkflowErrorUiTests.cs:18,23`, `WfDueOverride`). n8n: Einrichten und HTTP vollständig ersetzt (`MainWindow.N8n.cs:26-29`, `:110`; `tests/Trendo.App.Tests/N8nWorkspaceUiTests.cs:15`); kein Beleg für einen echten n8n-Lauf unter `reports/` gefunden. Node-RED lief echt, aber nicht über die Trendo-Oberfläche (`reports/alle-studios/node-red-e2e.md`, Abschnitt „Nicht geprüft“). |
| 15 | Bedienung | TEILWEISE | Umschalter Einfach/Profi (`MainWindow.Workflows.cs:149-159`), Strg+Z, Ziehen. Ordner werden als Text eingetippt (`:166-170`, `:332-334`); Rechtsklick und Vollbild nicht gefunden. |

**Gesamturteil: TEILWEISE** – ein echter, sicher gebauter kleiner Datei-Automat mit Zeitplan; kein vollwertiges Automatisierungsprogramm, und die Profi-Ebene ist ein Starter für fremde Programme, deren echter Lauf mit n8n nie belegt wurde.

**Was fehlt (priorisiert):**
1. KI-Aktion anschließen: aus der Beschreibung einen echten Ablauf anlegen (mit Probelauf und Freigabe) statt Plan-Text; `ablauf.starten` freischalten.
2. Fehlerbehandlung: Wiederholung mit Abstand, `LastRun` nur bei Erfolg, sichtbare Meldung bei jedem fehlgeschlagenen geplanten Lauf.
3. Laufen ohne offenes Trendo (Hintergrunddienst oder Windows-Aufgabe nach Zustimmung) – oder ehrlich in der Oberfläche sagen.
4. Mehr Bausteine: Bedingung, Programm/Skript starten, Web-Abruf, Meldung, Mail-Entwurf, Trendo-Aktionen (die es für n8n schon gibt, `N8n.cs:55-63`).
5. Atomar speichern, beschädigte Datei sichern statt leer lesen; Import/Export eines Ablaufs; Löschen mit Papierkorb.
6. Echter n8n-Lauf über die Oberfläche als Beleg; Ordnerauswahl statt Tippen.

**Abnahmekriterien:**
- „Jeden Tag um 8 Uhr PDFs aus Downloads einsortieren“ als Satz an die KI erzeugt einen Ablauf, der nach Probelauf und Freigabe am nächsten Tag ohne Zutun läuft – auch wenn Trendo geschlossen war, oder mit klarer Meldung, dass er nachgeholt wurde.
- Ein Ablauf, dessen Zielordner fehlt, wird dreimal mit Abstand wiederholt; der Fehler steht rot auf der Seite und im Protokoll.
- Abbruch während des Speicherns: Nach dem Neustart sind alle Abläufe noch da.
- Ablauf exportieren, löschen, importieren → gleicher Hash, gleiche Freigabepflicht.
- Beleg unter `reports/`: n8n über die Oberfläche eingerichtet, gestartet, Vorlage angelegt, Trendo-Ablauf von n8n gestartet.

**Aufwand:** groß. **Risiko:** mittel–hoch (Dateien verschieben/umbenennen ohne Aufsicht, Hintergrundlauf, fremde npm-Pakete; n8n darf nicht mitgeliefert werden).

**Dateien:** `src/Trendo.Core/W2/Workflows.cs`, `src/Trendo.App/MainWindow.Workflows.cs`, `src/Trendo.App/MainWindow.N8n.cs`, `src/Trendo.Core/Automation/N8n.cs`, `src/Trendo.Api/AutomationApi.cs`, `src/Trendo.Core/PromptAction/SystemPromptAdapter.cs`, `tests/Trendo.Core.Tests/W2Tests.cs`, `tests/Trendo.Core.Tests/N8nTests.cs`, `tests/Trendo.App.Tests/AblaufEditorUiTests.cs`, `docs/spec/kataloge/SYS-05_KATALOG.md`.

## X-01 Großprojekte umsetzen (Programm „produktion“, eingegliedert in X-11)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE | Tafel mit drei Spalten, Karten per Ziehen verschiebbar (`src/Trendo.App/MainWindow.W5Fusion.cs:170-188`), Einzelheiten rechts als Felder (`:215-258`). Reiter „Abläufe über mehrere Studios“ ist eine Auswahl mit Eingabefeldern (`:48-101`). |
| 2 | Werkzeuge real | TEILWEISE | 4 fest einprogrammierte Ketten (`src/Trendo.Core/Fusion.cs:24-93`); der Nutzer kann keine eigene Kette bauen oder eine ändern. |
| 3 | Neues Projekt | VOLLWERTIG | Neues Großprojekt, leer oder aus Vorlage (`MainWindow.W5Fusion.cs:137-143`, `src/Trendo.Core/BigBoard.cs:48`). |
| 4 | Import | TEILWEISE | Nur Belegdateien je Karte, mit SHA-256 ins Projekt kopiert (`BigBoard.cs:6-7`, `MainWindow.W5Fusion.cs:249-252`); der Pfad wird eingetippt. Kein Import eines Projektplans. |
| 5 | Bearbeiten | VOLLWERTIG | Tafel liegt im Projekt (`BigBoard.cs:35-45`), laden/ändern/speichern (`MainWindow.W5Fusion.cs:116`). |
| 6 | Rückgängig | FEHLT | Nur Papierkorb für Karten (`MainWindow.W5Fusion.cs:209`, `BigBoard.cs:26-27`); kein Rückgängig für Änderungen. |
| 7 | Vorschau | TEILWEISE | Plan der Schritte vor der Freigabe (`Fusion.cs:102`); keine Vorschau der Ergebnisse. |
| 8 | Speichern | TEILWEISE | Jede Änderung wird geschrieben (`MainWindow.W5Fusion.cs:116`, `BigBoard.cs:45`); ob `ProjectStore.WriteFile` atomar schreibt: nicht geprüft. |
| 9 | Absturz | NICHT GETESTET | Kein Test gefunden; eine laufende Kette lässt sich nicht fortsetzen (jeder Lauf neuer Ordner, `MainWindow.W5Fusion.cs:23`). |
| 10 | Export | TEILWEISE | Ketten schreiben echte Dateien (Bauplan.md, index.html, PNG, STL, MP4/MP3/GIF) plus `PROTOKOLL.txt` (`Fusion.cs:31-38`, `:89-91`, `:132-135`). Die Tafel selbst hat keinen Export (PDF, CSV, Zeitplan). |
| 11 | Rundreise | FEHLT | Kein Export der Tafel, also nichts wieder zu öffnen. |
| 12 | Fachfunktion real | TEILWEISE | Ketten führen echte Schritte aus und halten beim ersten Fehler an (`Fusion.cs:121-136`), Start nur nach Freigabe (`MainWindow.W5Fusion.cs:29-44`). Aber: Der Lauf hängt im Aufruf der Freigabe, ohne Fortschritt und ohne Abbrechen (`:41`); „Spielfigur → fertiges Spiel“ zeichnet als Figur nur einen gelben Kreis mit dem Anfangsbuchstaben (`Fusion.cs:48-50`); die Tafel ist reine Handarbeit – „zuständige KI-Rolle“ ist ein Etikett aus einer festen Liste (`BigBoard.cs:37`), keine KI und kein Studio arbeitet eine Karte ab. Das Versprechen „Mehrere Studios arbeiten zusammen an einem Projekt“ (`src/Trendo.App/StudioCatalog.cs:85`) ist nur für die 4 Ketten eingelöst. |
| 13 | KI arbeitet mit dem Programm | FEHLT | `studios.nennen` schreibt nur Studionamen, „Es wird nichts ausgeführt“ (`SystemPromptAdapter.cs:138-156`, `CapabilityCatalog.cs:80`). `FusionShowAdapter` (`SystemPromptAdapter.cs:284`) nicht geprüft. |
| 14 | Tests | TEILWEISE | Echt: Kette „Idee → Webseite“ (`tests/Trendo.Core.Tests/W5FusionTests.cs:13-14`, `tests/Trendo.App.Tests/W5FusionUiTests.cs:14-19`), „Figur → Spiel“ (`tests/Trendo.App.Tests/StudioUebergabeUiTests.cs:77-96`), Tafel (`KanbanUiTests.cs`, `BigBoardTests.cs`). „Video → Upload-Paket“ mit FFmpeg: Test vorhanden (`W5FusionTests.cs:39`); ob er ohne FFmpeg übersprungen wird: nicht geprüft. „3D-Schriftzug → Druckdatei“ mit Blender: kein Test gefunden → NICHT GETESTET. |
| 15 | Bedienung | TEILWEISE | Ziehen der Karten, Knöpfe ←/→ (`MainWindow.W5Fusion.cs:236-237`). Tastenkürzel, Rechtsklick, Vollbild, Einfach/Profi nicht gefunden. |

**Gesamturteil: TEILWEISE** – eine brauchbare Kanban-Tafel plus vier echte, aber starre Vorführ-Ketten; kein Programm, das ein Großprojekt über mehrere Studios wirklich steuert.

**Was fehlt (priorisiert):**
1. Karten, die wirklich etwas tun: Karte an ein Studio/eine KI-Rolle übergeben, die Ergebnisdatei kommt als Beleg zurück.
2. Eigene Ketten bauen und ändern (Schritte aus vorhandenen Studio-Aktionen) statt 4 fester Rezepte.
3. Lauf im Hintergrund mit Fortschritt, Abbrechen und Fortsetzen ab dem fehlgeschlagenen Schritt.
4. KI-Aktion: aus einem Ziel ein Großprojekt mit Karten anlegen (statt nur Studionamen).
5. Zeitplan/Meilensteine, Abhängigkeiten, Export der Tafel (PDF/CSV), Rückgängig.
6. Echte Figur aus dem Bildprogramm/der Bild-KI statt Platzhalter-Kreis; Blender-Kette mit Beleg.

**Abnahmekriterien:**
- Aus „Hofladen online bringen“ entsteht ein Großprojekt mit Karten; mindestens drei Karten werden von je einem Studio ausgeführt und hängen danach eine echte Datei als Beleg an.
- Eine selbst gebaute Kette aus zwei Studios läuft nach Freigabe, zeigt Fortschritt, lässt sich abbrechen und ab dem Fehler fortsetzen.
- Alle vier mitgelieferten Ketten haben einen echten Laufbeleg unter `reports/` (mit FFmpeg und Blender).
- Tafel als PDF exportieren; Rückgängig nimmt die letzte Kartenänderung zurück.

**Aufwand:** groß. **Risiko:** mittel (hängt an den Fachadaptern der anderen Studios; lange Läufe mit Blender/FFmpeg).

**Dateien:** `src/Trendo.Core/Fusion.cs`, `src/Trendo.Core/BigBoard.cs`, `src/Trendo.App/MainWindow.W5Fusion.cs`, `src/Trendo.Core/PromptAction/SystemPromptAdapter.cs`, `tests/Trendo.Core.Tests/W5FusionTests.cs`, `tests/Trendo.App.Tests/W5FusionUiTests.cs`, `tests/Trendo.App.Tests/KanbanUiTests.cs`, `docs/claude/vorbilder/produktion-openproject.md`.

### Auffälligkeiten Teil C
- `src/Trendo.Core/W2/Workflows.cs:230` setzt `LastRun` auch nach einem Fehler; zusammen mit `:137` fällt ein fehlgeschlagener Tageslauf bis zum nächsten Tag aus.
- `Workflows.cs:86` liest eine beschädigte `ablaeufe.json` still als leer; `:261` schreibt nicht atomar.
- `src/Trendo.App/MainWindow.Workflows.cs:113`: „Löschen“ ohne Nachfrage und ohne Rückgängig.
- `CapabilityCatalog.cs:79` nennt als Bestandsmodul „WorkflowEngine“; der Adapter erzeugt sie laut eigenem Kommentar nicht (`SystemPromptAdapter.cs:201`).
- `docs/programme/katalog.json` führt „ablaeufe“ und „produktion“ als „IMPLEMENTED / Test PC_VERIFIED“; für n8n und die Blender-Kette wurde kein echter Laufbeleg gefunden. `docs/spec/kataloge/SYS-05_KATALOG.md:7-13` sagt selbst nur „Box getestet“.
- X-01 steht im Katalog unter „KI & Automatik“ (`StudioCatalog.cs:85`), ist in X-11 eingegliedert (`:152`) und wird bei den Geschäfts-Reitern angemeldet (`MainWindow.W5Fusion.cs:21`).
- `MainWindow.W5Fusion.cs:41`: Die Kette läuft im Aufruf der Freigabe mit; ob das die Oberfläche bei FFmpeg/Blender blockiert, wurde nicht beobachtet.

---

# Teil A1: Audit Gruppe KI & Wissen – Teil A1 (sechs Kreativ-KI-Studios)

Stand: Arbeitskopie `studios-aufraeumen`, nur gelesen. Nichts gebaut, kein Test gestartet. Alle Pfade relativ zum Repo.
Kürzel: KS = `src/Trendo.App/MainWindow.KreativStudios.cs`, KS2 = `…KreativStudios2.cs`, KS3 = `…KreativStudios3.cs`, PS = `src/Trendo.App/MainWindow.ProgramShell.cs`, K1/K2/K3 = `src/Trendo.Core/Kreativ/KreativKits.cs` / `KreativKits2.cs` / `KreativKits3.cs`, PA1/2/3 = `src/Trendo.Core/PromptAction/KreativPromptAdapters.cs` / `…2.cs` / `…3.cs`.

## Übersicht (Zeilen für die Gesamttabelle)

| Studio-Id | Name | Programme | Gesamturteil | größte Lücke | Aufwand |
|---|---|---|---|---|---|
| STUDIO-SUMMARY-01 | Texte zusammenfassen | zusammenfassen-programm | TEILWEISE | Kein Dateiimport (PDF/DOCX/TXT), eingebaute „Zusammenfassung“ ist reine Satzauswahl nach Worthäufigkeit; Claude-Ergebnis landet nur im Chat | mittel |
| STUDIO-IMAGEAI-01 | Bilder mit KI | bild-ki-programm | TEILWEISE | Echter sd-cli-Aufruf vorhanden, aber Ende-zu-Ende nie echt getestet (immer `ImageAiOverride`); kein Bild-zu-Bild, kein Nachbearbeiten, KI-Aktion schreibt nur „bildauftrag.txt“ | mittel |
| STUDIO-VIDEOAI-01 | Videos mit KI | video-ki-programm | TEILWEISE | Kein generiertes Video, sondern Diashow aus Standbildern (Ken Burns) ohne Ton; „Szenen planen“ ist ein Zeilen-Parser, keine KI; echter KI-Clip nur Verweis auf kie.ai-Feld | groß |
| STUDIO-AGENTS-01 | KI-Agenten bauen | agenten-bauen-programm | TEILWEISE | Formular, das eine .md-Datei schreibt; kein Agentenlauf, kein Zeitplan, kein Auslöser, kein Laufprotokoll – „Ausprobieren“ ist ein Prompt an den Claude-Chat | groß |
| STUDIO-MODELS-01 | KI-Modelle vergleichen | modelle-vergleichen-programm | TEILWEISE | Fragt echte Quellen (Ollama/Claude) nacheinander, aber nur einzelne Fragen von Hand; keine Fragenreihe, keine automatische Bewertung; UI-Test nur mit Ersatz-Backends | mittel |
| STUDIO-TRAIN-01 | Eigene KI trainieren | ki-trainieren-programm | TEILWEISE (ehrlich, aber klein) | Trainiert wirklich – aber nur Naive Bayes (Wortzählung); kein neuronales Modell, kein Feintuning, trainiertes Modell wird nirgends in Trendo genutzt | mittel (für Nutzung), sehr groß (für echtes Feintuning) |

Gemeinsame Grundlage aller sechs (Programm-Hülle, nicht je Studio wiederholt belegt):
- Dokument `.tdoc`, Öffnen/Speichern unter: `PS:383-391`; Strg+S/Z/Y/N/W: `PS:344-353`; Rückgängig/Wiederholen-Knöpfe: `PS:615-616`; Versionen: `PS:592-593`.
- Autosave alle 30 s, beim Beenden und 1,5 s nach Änderung: `PS:83-88`, `PS:228`, `PS:245-248`; Wiederherstellen auf der Startseite: `PS:520-526`.
- Tests der Hülle (allgemein, nicht je Studio): `tests/Trendo.App.Tests/ProgramShellUiTests.cs:15` (Vorlage → bearbeiten → Rückgängig → Speichern → wieder öffnen), `:56` (Autosicherung + Wiederherstellen), `:71` (Versionen).
- Vollbild/Fokusmodus F11: `src/Trendo.App/MainWindow.ProgramFrame.cs:217`, `:366-371`. Bedienstufen Einfach/Erweitert/Profi: `PS:18`, `PS:627-642`.
- In der Hülle weder Drag-and-drop noch Kontextmenü gefunden (Grep nach `DragDrop|ContextMenu` in `PS` und `MainWindow.ProgramFrame.cs`: keine Treffer). In KS/KS2/KS3 ebenfalls nicht für diese sechs Programme gesehen.

---

## STUDIO-SUMMARY-01 Texte zusammenfassen (zusammenfassen-programm)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE – Textfeld links, Ergebnis-Karte rechts (nur lesbar). Kein Editor für das Ergebnis | `KS:103-128` |
| 2 | Werkzeuge | TEILWEISE – drei Längen-Knöpfe + ein Claude-Knopf, mehr gibt es nicht | `KS:83-92` |
| 3 | Neues Projekt | VOLLWERTIG – Vorlagen „Leerer Text“, „Beispiel“ | `KS:77-81` |
| 4 | Dateien importieren | FEHLT – kein Import (TXT/PDF/DOCX/Webseite); nur Einfügen ins Textfeld | `KS:72-101` (kein Import-Befehl) |
| 5 | Vorhandene bearbeiten | VOLLWERTIG (nur `.tdoc`) | `PS:383-391` |
| 6 | Rückgängig | VOLLWERTIG – `d.Set` mit Etikett, Hülle | `KS:42-44`, `PS:349-350` |
| 7 | Vorschau | VOLLWERTIG – rechnet beim Tippen neu | `KS:106-118`, `KS:124` |
| 8 | Speichern/Autosave | VOLLWERTIG (Hülle) | `PS:83-88` |
| 9 | Wiederherstellung | VOLLWERTIG (Hülle), für dieses Programm nicht eigens getestet | `PS:520-526`; Test nur allgemein `ProgramShellUiTests.cs:56` |
| 10 | Export | TEILWEISE – nur `.md` und `.txt`; kein DOCX/PDF | `KS:93-97` |
| 11 | Export wieder öffnen | FEHLT – MD/TXT lässt sich nicht zurückladen (kein Import) | `KS:72-101` |
| 12 | Fachfunktion real | TEILWEISE – echte Rechnung, aber rein extraktiv: Sätze nach Worthäufigkeit, erster Satz ×1,3. Keine eigene Formulierung, kein Sprachmodell, keine lokale KI (Ollama wird nicht gefragt) | `K1:80-96`; Grenze selbst benannt: `CapabilityCatalog.cs:53` |
| 13 | Claude/KI | TEILWEISE – „Mit Claude ausführlicher“ schickt den Text als Prompt in den Claude-Bereich; Antwort bleibt im Chat, wird nicht ins Dokument geschrieben. Rückgabewert wird verworfen: ohne verbundenes Claude passiert beim Klick nichts, kein Hinweis. Prompt-Aktion `zusammenfassung.schreiben` schreibt echte `zusammenfassung.md` (Stufe Partial) | `KS:90` (kein `ClaudeOrHint` wie in `KS3:45-48`), `src/Trendo.App/MainWindow.ClaudeLos.cs:44-46`, `PA1:6-30` |
| 14 | E2E-Tests | TEILWEISE – Kit-Test und UI-Test (ohne externe Werkzeuge, daher echt); Claude-Weg nicht getestet | `tests/Trendo.Core.Tests/KreativKitsTests.cs:13`, `tests/Trendo.App.Tests/KreativStudiosUiTests.cs:38` |
| 15 | Bedienung | TEILWEISE – Tastenkürzel + F11 aus der Hülle; kein Drag-and-drop einer Datei, kein Rechtsklick | `PS:344-353` |

**Gesamturteil: TEILWEISE** – funktionierender, ehrlich beschriebener Kernsatz-Auszug, aber kein Zusammenfassungs-Programm: keine Dateien, keine KI im Programm selbst.

**Was fehlt (priorisiert):**
1. Dateien öffnen: PDF, DOCX, TXT, Markdown, Webseite (Text-Extraktion gibt es an anderer Stelle im Repo – nicht geprüft, ob wiederverwendbar).
2. Echte abstraktive Zusammenfassung über die vorhandenen KI-Quellen (lokal über `IntelligenceCore`, Claude) mit Ergebnis im Dokument statt im Chat.
3. Hinweis, wenn Claude nicht verbunden ist (`KS:90` schluckt `false`).
4. Ergebnis bearbeitbar machen; Export DOCX/PDF.
5. Lange Texte: Abschnittsweise zusammenfassen, Quellenstellen anspringen.

**Abnahmekriterien:**
- Eine 30-seitige PDF lässt sich öffnen und ergibt eine Zusammenfassung mit Seitenverweisen.
- Mit eingerichteter lokaler KI entsteht eine frei formulierte Fassung im Dokument; ohne KI erscheint klar die Kernsatz-Fassung mit Hinweis.
- Klick auf „Mit Claude ausführlicher“ ohne Claude-Anmeldung zeigt eine verständliche Meldung.
- Export als DOCX öffnet sich fehlerfrei in LibreOffice.

**Aufwand:** mittel · **Risiko:** gering (keine externen Werkzeuge nötig, außer PDF/DOCX-Leser).
**Dateien:** `KS:67-128`, `K1:57-100`, `PA1:5-31`, Tests siehe oben.

---

## STUDIO-IMAGEAI-01 Bilder mit KI (bild-ki-programm)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE – Beschreibungsfeld + Galerie mit Vorschaubildern; keine Bearbeitung am Bild (kein Zuschnitt, Maske, Übermalen) | `KS2:645-672` |
| 2 | Werkzeuge | TEILWEISE – Malen, Stil (7), Format (4), Varianten 1–4, Startwert, Negativtext; „Groß ansehen“ springt in ein anderes Programm (Medien/Fotos) | `KS2:565-573`, `KS2:674-684`, `KS2:663-664` |
| 3 | Neues Projekt | VOLLWERTIG | `KS2:559-563` |
| 4 | Dateien importieren | FEHLT – kein Referenzbild, kein Bild-zu-Bild | `KS2:554-583` |
| 5 | Vorhandene bearbeiten | VOLLWERTIG – Bildordner-Kennung steht im Dokument, Bilder sind nach Wiederöffnen da | `KS2:540-543` |
| 6 | Rückgängig | TEILWEISE – Dokumenteintrag „Bild gemalt“ ist rückgängig machbar, die PNG-Datei bleibt liegen (nicht geprüft, ob aufgeräumt wird) | `KS2:634` |
| 7 | Vorschau | VOLLWERTIG – echte PNG aus dem Bildordner | `KS2:657-661` |
| 8 | Speichern/Autosave | VOLLWERTIG (Hülle) | `PS:83-88` |
| 9 | Wiederherstellung | VOLLWERTIG (Hülle), nicht eigens getestet | `PS:520-526` |
| 10 | Export | TEILWEISE – PNG + Herkunfts-JSON, ZIP aller Bilder; kein JPG/WebP, keine Größenwahl, kein Hochskalieren | `KS2:574-578`, `KS2:585-597` |
| 11 | Export wieder öffnen | FEHLT – exportierte PNG kann nicht zurück ins Programm | `KS2:554-583` |
| 12 | Fachfunktion real | VOLLWERTIG im Code / NICHT GETESTET real – startet wirklich `sd-cli` (stable-diffusion.cpp) mit SDXL-Turbo oder SD-Turbo aus der KI-Bibliothek, zweiter Versuch mit `--offload-to-cpu`, Herkunftsnachweis mit SHA-256. Ohne eingerichtetes Modell nur Hinweis. Feste 4 bzw. 1 Schritte, cfg 1,0 (Turbo) – keine Qualitätseinstellungen | `KS2:599-642`, Modellsuche `src/Trendo.App/MainWindow.MusikCover.cs:19-25`, Argumente `K2:367-370`, Nachweis `K2:372-380` |
| 13 | Claude/KI | TEILWEISE – Prompt-Aktion `bild.auftrag` schreibt nur `bildauftrag.txt` (Plan-Text, kein Bild; „Gemalt wird im Studio“). kie.ai ist dem Studio als Bild-Art zugeordnet (`Kie.cs:237`), der Weg wurde hier nicht geprüft | `PA2:118-145`, `src/Trendo.Core/Kie/Kie.cs:237` |
| 14 | E2E-Tests | NICHT GETESTET (ersetzt) – UI-Test setzt `ImageAiOverride` und schreibt eine 1-Pixel-PNG; kein „echt“-Test und kein Beleg unter `reports/` für diesen Weg (der vorhandene echte sd-cli-Beleg `reports/paket01/s3-13-ki-video-lauf.md` gehört zum Videoschnitt/Wan 2.2, anderes Programm) | `tests/Trendo.App.Tests/KreativStudios2UiTests.cs:126-160` (Override Zeile 140), `KS2:537` |
| 15 | Bedienung | TEILWEISE – Hülle; kein Abbrechen eines laufenden Malvorgangs, kein Fortschritt je Bild außer Textmeldung, kein Rechtsklick/Drag-and-drop | `KS2:619`, `KS2:601` |

**Gesamturteil: TEILWEISE** – echter lokaler Bildgenerator ist verdrahtet, aber schmal (nur Text→Bild, Turbo-Modelle) und der reale Lauf ist für dieses Programm nirgends belegt.

**Was fehlt (priorisiert):**
1. Echter Nachweis: ein „echt“-Test (wie `TRENDO_GENVIDEO_ECHT`) mit sd-cli + Beleg unter `reports/`.
2. Bild-zu-Bild / Referenzbild, Übermalen (Inpainting), Hochskalieren.
3. Abbrechen und Fortschritt während des Malens.
4. Bildbearbeitung im Programm oder nahtlose Übergabe an das Foto-Programm mit Rückweg.
5. KI-Aktion soll malen statt nur „bildauftrag.txt“ schreiben (heute Stufe Partial, `PA2:124`).
6. Mehr Formate (JPG/WebP), Modellwahl, Schritte/Qualität.

**Abnahmekriterien:**
- Auf dem Zielrechner entsteht mit eingerichtetem SD-Turbo aus einer Beschreibung in einem protokollierten Lauf ein PNG ≥ 512 px mit gültigem Herkunftsnachweis (Rohlog + SHA-256 unter `reports/`).
- Gleicher Startwert ergibt nachweislich (Bildabstand) dasselbe Bild.
- Ein laufender Auftrag lässt sich abbrechen, ohne dass das Programm hängt.
- Die KI-Aktion „Bild malen“ liefert eine Bilddatei im Projekt, nicht nur einen Text.

**Aufwand:** mittel (Nachweis + Abbruch klein; Bild-zu-Bild/Inpainting mittel) · **Risiko:** mittel (GPU-/Modell-Abhängigkeit, Laufzeit).
**Dateien:** `KS2:535-684`, `K2:323-381`, `src/Trendo.App/MainWindow.MusikCover.cs:19-25`, `PA2:118-145`.

---

## STUDIO-VIDEOAI-01 Videos mit KI (video-ki-programm)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE – Szenenliste als Formularzeilen (Beschreibung, Dauer, Bewegung, Einblendung, Vorschaubild). Keine Zeitleiste, kein Player im Programm | `KS3:293-365` |
| 2 | Werkzeuge | TEILWEISE – Planen, Bilder malen, Video bauen, eigenes Bild je Szene, Szene entfernen. Kein Umsortieren, kein Hinzufügen einzelner Szenen außer über den Ideentext (nicht gesehen) | `KS3:269-281`, `KS3:327-330` |
| 3 | Neues Projekt | VOLLWERTIG | `KS3:262-266` |
| 4 | Dateien importieren | TEILWEISE – eigenes PNG/JPG je Szene (wird kopiert); kein Videoclip, kein Ton | `KS3:143-158` |
| 5 | Vorhandene bearbeiten | VOLLWERTIG – Ordnerkennung im Dokument, fremde Pfade werden abgewiesen | `KS3:58-67` |
| 6 | Rückgängig | VOLLWERTIG für Dokumentänderungen (`d.Edit` mit Etikett) | `KS3:75-76` |
| 7 | Vorschau | TEILWEISE – Standbild-Miniaturen; fertiges Video nur „Abspielen“ im externen Player (`Launch`), keine Vorschau der Kamerafahrt vor dem Bauen | `KS3:310-316`, `KS3:354-355` |
| 8 | Speichern/Autosave | VOLLWERTIG (Hülle) | `PS:83-88` |
| 9 | Wiederherstellung | VOLLWERTIG (Hülle), nicht eigens getestet | `PS:520-526` |
| 10 | Export | TEILWEISE – MP4 (H.264, 30 fps, 1280×720 / 720×1280 / 1080×1080) ohne Tonspur (`-an`), dazu Szenenplan .md | `K3:17`, `K3:142-150`, `KS3:283-287` |
| 11 | Export wieder öffnen | TEILWEISE – echter FFmpeg-Test prüft Länge und Auflösung des MP4 per Probe; kein Rückimport ins Programm | `tests/Trendo.Core.Tests/KreativKits3Tests.cs:74-104` |
| 12 | Fachfunktion real | TEILWEISE – **kein KI-Video.** Es entsteht eine Diashow: lokale Bild-KI malt Standbilder (derselbe sd-cli-Weg wie „Bilder mit KI“), FFmpeg legt `zoompan` + `xfade` + Text-PNG darüber. „Szenen planen“ ist ein Zeilen-Parser mit Regex (Dauer, Bewegung, „Text:“), keine KI. Höchstens 60 s, 20 Szenen. Der Katalogsatz beschreibt das korrekt, der Name „Videos mit KI“ verspricht mehr | `KS3:97-139` (Bilder), `KS3:176-229` (FFmpeg-Prozess real), `K3:42-…` (Parser), `K3:117-150` (Filter), Grenzen `K3:18-19`; Satz `src/Trendo.App/StudioCatalog.cs:54` |
| 13 | Claude/KI | TEILWEISE – „Szenen mit Claude verbessern“ = Prompt in den Chat, Ergebnis muss von Hand übernommen werden (mit Hinweis, falls Claude fehlt). „Echter KI-Clip (kie.ai)“ öffnet nur das kie.ai-Feld der Leiste bzw. zeigt einen Hinweistext – der Clip landet nicht im Szenenplan. Prompt-Aktion `video-ki.szenenplan` schreibt nur `szenenplan.md` | `KS3:279-280`, `KS3:249-254`, `KS3:45-48`, `PA3:5-34` |
| 14 | E2E-Tests | TEILWEISE – FFmpeg-Teil echt getestet (`FfmpegFact`, wird übersprungen, wenn FFmpeg fehlt); UI-Weg vollständig ersetzt (`ImageAiOverride` + `VideoAiFfmpegOverride`, „Video“ = 4 Bytes). Bild-KI→Video am Stück nie echt gelaufen, kein Beleg unter `reports/` | `KreativKits3Tests.cs:74-104`; `tests/Trendo.App.Tests/KreativStudios3UiTests.cs:55-130` (Zeilen 83, 85) |
| 15 | Bedienung | TEILWEISE – Hülle; kein Ziehen zum Umsortieren, kein Abbrechen (nur 15-Minuten-Grenze für FFmpeg), kein Rechtsklick | `KS3:215` |

**Gesamturteil: TEILWEISE** – eine solide Bild-Diashow mit Kamerafahrt, aber kein Video-Generator; der einzige Weg zu echten KI-Clips ist ein Verweis auf ein kostenpflichtiges Fremdkonto ohne Rückweg ins Projekt.

**Was fehlt (priorisiert):**
1. Echte bewegte KI-Clips im Programm: das lokale Wan-2.2-Modell ist im Videoschnitt bereits real belegt (`reports/paket01/s3-13-ki-video-lauf.md`, `src/Trendo.Core/Media/GenVideo.cs`) – hier nicht angebunden.
2. kie.ai-Clip (Kling) als Szene übernehmen statt nur das Feld zu öffnen (`KS3:249-254`).
3. Ton: Musik, Sprecher (Piper ist im Repo vorhanden – nicht geprüft), Untertitel; heute `-an`.
4. Zeitleiste mit Vorschau/Player, Szenen umsortieren, Abbrechen.
5. Szenenplan durch KI erzeugen (aus einer Idee statt einer Zeile je Szene) und direkt ins Dokument schreiben.
6. Ende-zu-Ende-Beleg mit echter Bild-KI + FFmpeg.

**Abnahmekriterien:**
- Aus einer Ein-Satz-Idee entsteht ohne Handarbeit ein Szenenplan im Dokument.
- Mindestens eine Szene ist ein wirklich bewegter, generierter Clip (lokal oder kie.ai), sichtbar im fertigen MP4.
- Das MP4 hat eine Tonspur (Musik oder Sprecher) und spielt im Programm ab.
- Protokollierter echter Lauf (Bild-KI + FFmpeg) mit Rohlog und Prüfsumme unter `reports/`.

**Aufwand:** groß · **Risiko:** hoch (GPU-Zeit: lokaler Clip ~100 s je 0,5 s laut Beleg; Kosten bei kie.ai).
**Dateien:** `KS3:50-381`, `K3:21-180`, `PA3:5-34`, `src/Trendo.Core/Kie/Kie.cs:241`.

---

## STUDIO-AGENTS-01 KI-Agenten bauen (agenten-bauen-programm)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE – **Formular** (Kurzname, Aufgabe, Anweisungen, Werkzeug-Häkchen, Beispiele, Grenzen) + schreibgeschützte Vorschau der Agent-Datei. Kein Ablauf-/Baum-Editor | `KS3:499-537` |
| 2 | Werkzeuge | VOLLWERTIG für den Umfang – Prüfen, Kurzname, Datei öffnen, in Projekt speichern, Rolle ins KI-Team, Ausprobieren | `KS3:473-485`, Prüfregeln `K3:221-241` |
| 3 | Neues Projekt | VOLLWERTIG | `KS3:468-472` |
| 4 | Dateien importieren | VOLLWERTIG – Claude-Code-Agent (.md) einlesen, unsichere Werkzeuge werden verworfen und gemeldet; fremde Datei verlangt zweiten Klick vor dem Ausprobieren | `KS3:433-442`, `KS3:451-456`, `K3:270` |
| 5 | Vorhandene bearbeiten | VOLLWERTIG | `PS:383-391` |
| 6 | Rückgängig | VOLLWERTIG | `KS3:515-520` |
| 7 | Vorschau | VOLLWERTIG – Markdown der Agent-Datei live | `KS3:535` |
| 8 | Speichern/Autosave | VOLLWERTIG (Hülle) | `PS:83-88` |
| 9 | Wiederherstellung | VOLLWERTIG (Hülle), nicht eigens getestet | `PS:520-526` |
| 10 | Export | VOLLWERTIG – Claude-Code-Subagent `.md`, Trendo-Rolle `.json`, Ablage unter `<Projekt>/.claude/agents/<name>.md` ohne Überschreiben | `KS3:486-495`, `KS3:405-419`, `K3:312-318` |
| 11 | Export wieder öffnen | VOLLWERTIG – Rundlauf getestet | `tests/Trendo.Core.Tests/KreativKits3Tests.cs:139` |
| 12 | Fachfunktion real | TEILWEISE – **Es wird kein Agent ausgeführt.** Das Programm erzeugt nur die Beschreibungsdatei. Kein Lauf, kein Zeitplan, kein Auslöser, keine Fehlerbehandlung, kein Protokoll, kein Werkzeug-Sandkasten. „Als Rolle ins KI-Team“ legt eine eigene Rolle an (`Agents.AddCustomRole`); ob und wie das KI-Team sie ausführt, liegt außerhalb dieses Teils (nicht geprüft) | `KS3:405-431`, `KS3:446-461` |
| 13 | Claude/KI | TEILWEISE – „Mit Claude ausprobieren“ schickt Anweisungen + Testauftrag als **einen Prompt** in den Claude-Chat des Studios („Arbeite … als der folgende Helfer“). Die Werkzeugbeschränkung steht nur als Satz im Prompt, sie wird technisch nicht durchgesetzt; es wird kein echter Subagent gestartet. Prompt-Aktion `agent.entwurf` schreibt eine echte `<name>.md` | `KS3:457-459`, `PA3:36-77` |
| 14 | E2E-Tests | TEILWEISE – Datei-Erzeugung, Rundlauf, Speichern echt getestet (keine Ersetzung nötig); Ausprobieren mit Claude nicht getestet; ob Claude Code die erzeugte Datei tatsächlich als Agent lädt, ist nirgends belegt | `KreativKits3Tests.cs:111`, `:139`, `:167`; `KreativStudios3UiTests.cs:135-199` (nur Ordner-/Dateiauswahl ersetzt, Zeilen 156, 188) |
| 15 | Bedienung | TEILWEISE – Hülle; kein Drag-and-drop einer .md, kein Rechtsklick | – |

**Gesamturteil: TEILWEISE** – sauberer Editor für Agent-Dateien mit guter Sicherheitsprüfung, aber „bauen“ heißt hier „Textdatei schreiben“; Agenten laufen in diesem Studio nicht.

**Was fehlt (priorisiert):**
1. Echter Probelauf als Claude-Code-Subagent mit technisch durchgesetzter Werkzeugliste und Arbeitsordner (statt Prompt-Nachahmung im Chat).
2. Lauf-Ansicht: Schritte, Werkzeugaufrufe, Ergebnis, Fehler, Abbruch; Laufprotokoll je Agent.
3. Auslöser/Zeitplan (Anbindung an „Abläufe“ SYS-05 – dortige Fähigkeit hier nicht geprüft).
4. Testfälle je Agent (Eingabe → erwartete Antwort) mit Wiederholung nach Änderungen; die „Beispiele“ sind heute nur Text.
5. Nachweis, dass Claude Code die gespeicherte Datei erkennt (echter Lauf, Beleg).
6. Übersicht aller vorhandenen Agenten eines Projekts (heute je Dokument einer).

**Abnahmekriterien:**
- Ein Agent mit Werkzeug „lesen“ kann im Probelauf nachweislich keine Datei schreiben (Versuch wird abgewiesen und protokolliert).
- Der Probelauf zeigt jeden Werkzeugaufruf und lässt sich abbrechen.
- Die gespeicherte `.claude/agents/<name>.md` wird in einem protokollierten Claude-Code-Lauf als Subagent aufgerufen (Beleg unter `reports/`).
- Ein Agent lässt sich an einen Zeitplan oder Auslöser hängen und läuft ohne offenes Fenster einmal nachweislich durch.

**Aufwand:** groß · **Risiko:** hoch (Rechte/Sicherheit bei echten Agentenläufen, Abhängigkeit von der Claude-Code-Anmeldung der Person).
**Dateien:** `KS3:382-554`, `K3:182-320`, `PA3:36-77`.

---

## STUDIO-MODELS-01 KI-Modelle vergleichen (modelle-vergleichen-programm)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE – Quellen-Häkchen, Fragefeld, Antwortspalten A/B/C mit Sternen, Ergebnistabelle; fachgerecht, aber Formular-/Kartenaufbau | `KS3:731-803` |
| 2 | Werkzeuge | VOLLWERTIG für den Umfang – Quellen suchen, Frage stellen, Sterne, Aufdecken, nächste Frage | `KS3:707-716`, `KS3:681-695` |
| 3 | Neues Projekt | VOLLWERTIG | `KS3:701-705` |
| 4 | Dateien importieren | FEHLT – keine Fragenliste aus Datei, kein Import früherer Ergebnisse | `KS3:697-727` |
| 5 | Vorhandene bearbeiten | VOLLWERTIG – Runden stehen im Dokument; beschädigte Runden werden abgefangen | `KS3:575-590` |
| 6 | Rückgängig | VOLLWERTIG (Sterne, Aufdecken, Runde als Dokumentschritt) | `KS3:592-593`, `KS3:671` |
| 7 | Vorschau | VOLLWERTIG – Antworten mit Dauer, Wörtern, Wörter/s | `KS3:771-773` |
| 8 | Speichern/Autosave | VOLLWERTIG (Hülle) | `PS:83-88` |
| 9 | Wiederherstellung | VOLLWERTIG (Hülle), nicht eigens getestet | `PS:520-526` |
| 10 | Export | TEILWEISE – Bericht `.md`, Rohdaten `.csv` (UTF-8 mit BOM); kein XLSX/PDF, keine Grafik | `KS3:718-722` |
| 11 | Export wieder öffnen | FEHLT – kein Rückimport der CSV | – |
| 12 | Fachfunktion real | VOLLWERTIG im Code – fragt die **echten** eingerichteten Quellen über `IKiBackend` (`DefaultKiBackends()`: u. a. Ollama `KiBackends.cs:19`, Claude über Claude Code `MainWindow.Claude.cs:346`) nacheinander über den gemeinsamen Kern `IntelligenceCore.AskAsync`, misst die Zeit, mischt fest je Startwert, deckt erst nach Bewertung auf. Ohne erreichbare Quelle: Hinweis, kein Scheinergebnis | `KS3:598-625`, `KS3:628-664`, Mischen/Bewerten `K3:326-…` |
| 13 | Claude/KI | TEILWEISE – Claude ist als Vergleichsquelle wählbar (wenn verbunden). Prompt-Aktion `modelle.fragebogen` schreibt nur einen Fragebogen als `.md` – gefragt wird dort nichts | `PA3:79-106` |
| 14 | E2E-Tests | NICHT GETESTET (ersetzt) – UI-Test setzt `KiBackendsOverride` mit drei Ersatz-Backends; kein echter Lauf gegen Ollama oder Claude belegt. Logik (Mischen, Sterne, Tabelle, Berichte) im Core echt getestet | `tests/Trendo.App.Tests/KreativStudios3UiTests.cs:201-256` (Zeile 208); `tests/Trendo.Core.Tests/KreativKits3Tests.cs:188` |
| 15 | Bedienung | TEILWEISE – Hülle; Fragen laufen nacheinander ohne Abbrechen; kein Rechtsklick/Drag-and-drop | `KS3:641-656` |

**Gesamturteil: TEILWEISE** – echter Blindvergleich mit echten Modellen, aber nur Frage für Frage von Hand; für einen Benchmark fehlen Reihen, Automatik und ein belegter Echtlauf.

**Was fehlt (priorisiert):**
1. Fragenreihe (Liste/CSV) in einem Durchlauf an alle Quellen, mit Fortschritt und Abbrechen.
2. Belegter Echtlauf mit zwei lokalen Modellen (Rohlog unter `reports/`).
3. Mehr Messwerte: Zeit bis zum ersten Wort, Token/s, Kosten bei Internet-Quellen; mehrere Wiederholungen je Frage.
4. Automatische Vorbewertung (z. B. Richter-Modell oder Soll-Antwort) neben den Sternen.
5. Gleiche Einstellungen je Modell sichtbar machen (Temperatur, Länge) – heute nur Systemtext (`KS3:810`).
6. Diagramm und XLSX/PDF-Bericht.

**Abnahmekriterien:**
- Zehn Fragen aus einer Datei laufen in einem Durchgang gegen zwei lokale Modelle; Ergebnis steht als Tabelle und CSV bereit.
- Ein laufender Durchgang lässt sich abbrechen; bereits erhaltene Antworten bleiben.
- Fällt eine Quelle mitten im Lauf aus, steht das je Frage in der Tabelle (Fehlerzahl), der Rest läuft weiter.
- Protokollierter Lauf gegen echtes Ollama-Modell unter `reports/`.

**Aufwand:** mittel · **Risiko:** gering bis mittel (abhängig von eingerichteten Modellen).
**Dateien:** `KS3:556-813`, `K3:326-433`, `PA3:79-106`, `src/Trendo.App/KiBackends.cs:19`, `src/Trendo.App/MainWindow.Claude.cs:346`.

---

## STUDIO-TRAIN-01 Eigene KI trainieren (ki-trainieren-programm)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE – Tabelle „Text / Kategorie“ als Zeilen von Textfeldern (höchstens 300 sichtbar), rechts Genauigkeit, Verwechslungstabelle, Ausprobieren | `KS3:949-1017`, Grenze `KS3:955` |
| 2 | Werkzeuge | VOLLWERTIG für den Umfang – Trainieren, Beispiel hinzufügen/entfernen, CSV importieren, Modell laden, Prüfanteil und Startwert | `KS3:928-937`, `KS3:1020-1027` |
| 3 | Neues Projekt | VOLLWERTIG – leer oder Beispiel mit 30 E-Mails | `KS3:923-927`, `KS3:820-834` |
| 4 | Dateien importieren | VOLLWERTIG – CSV/TXT (bis 20 MB, 50 000 Zeilen, Kopfzeile erkannt), Modell `.json` | `KS3:889-899`, `KS3:901-913`, `K3:584-599` |
| 5 | Vorhandene bearbeiten | VOLLWERTIG – Modell steckt als JSON im Dokument | `KS3:851-856`, `KS3:875` |
| 6 | Rückgängig | VOLLWERTIG – auch „Trainiert“ ist ein Dokumentschritt | `KS3:875` |
| 7 | Vorschau | VOLLWERTIG – Vorhersage live beim Tippen mit Wahrscheinlichkeit je Kategorie und Warnung bei unbekannten Wörtern | `KS3:997-1013` |
| 8 | Speichern/Autosave | VOLLWERTIG (Hülle) | `PS:83-88` |
| 9 | Wiederherstellung | VOLLWERTIG (Hülle), nicht eigens getestet | `PS:520-526` |
| 10 | Export | TEILWEISE – Modell als eigenes JSON-Format `trendo-textklassifikator`, Auswertung `.md`, Beispiele `.csv`. Kein Standardformat (ONNX o. ä.), außerhalb von Trendo nicht nutzbar | `KS3:939-944`, `K3:437`, `K3:504-506` |
| 11 | Export wieder öffnen | VOLLWERTIG – Modell-JSON und CSV werden im UI-Test exportiert und wieder geladen; JSON-Rundlauf im Core-Test | `tests/Trendo.App.Tests/KreativStudios3UiTests.cs:258-…` (Zeilen 294, 297), `tests/Trendo.Core.Tests/KreativKits3Tests.cs:314` |
| 12 | Fachfunktion real | VOLLWERTIG für das, was der Katalogsatz sagt – **es wird wirklich trainiert**: multinomiales Naive Bayes mit Laplace-Glättung (Zählen von Wörtern je Kategorie), log-Wahrscheinlichkeiten, feste geschichtete Aufteilung Training/Prüfung je Startwert, Genauigkeit, Verwechslungstabelle, Präzision/Trefferquote/F1; Prüfung gilt nur, solange die Beispiele unverändert sind (Hash). **Aber:** kein neuronales Netz, kein Sprachmodell, kein Feintuning/LoRA, keine GPU, keine Epochen – „KI trainieren“ ist ein Lehrbuch-Klassifikator. Kein Kreuzvergleich (nur eine Aufteilung) | `K3:469-482` (Train), `K3:487-502` (Predict), `K3:531-544` (Split), `K3:551-567` (Evaluate), `KS3:858-881`, Hash `KS3:844`; Grenze genannt: `CapabilityCatalog.cs:58`, `KS3:1025` |
| 13 | Claude/KI | VOLLWERTIG (klein) – Prompt-Aktion `ki.trainieren` trainiert aus „Text = Kategorie“-Zeilen wirklich und legt `modell.json` ins Projekt (keine Auswertung dabei, trotz Kommentar „Modell und Auswertung“). Claude selbst trainiert nichts | `PA3:108-145` (Kommentar Zeile 108 vs. Ausgabe Zeile 142) |
| 14 | E2E-Tests | VOLLWERTIG – keine externen Werkzeuge, daher nichts ersetzt: Lehrbuchbeispiel mit erwarteter Wahrscheinlichkeit, Aufteilung, Kennzahlen, CSV/JSON, UI-Durchlauf inkl. Exporten | `KreativKits3Tests.cs:241`, `:249`, `:279`, `:314`, `:345`; `KreativStudios3UiTests.cs:258` |
| 15 | Bedienung | TEILWEISE – Hülle; über 300 Beispiele nur per CSV bearbeitbar, kein Einfügen aus der Zwischenablage als Tabelle, kein Rechtsklick/Drag-and-drop | `KS3:955`, `KS3:966` |

**Gesamturteil: TEILWEISE** – das einzige der sechs Programme, dessen Kernfunktion vollständig echt und echt getestet ist; der Name verspricht aber deutlich mehr als einen Wortzähl-Klassifikator, und das Modell wird nirgends weiterverwendet.

**Was fehlt (priorisiert):**
1. Nutzung des Modells in Trendo: z. B. E-Mails, Dokumente oder Dateien damit sortieren (Suche nach Verwendern von `TextClassifier` außerhalb Studio/Adapter: keine – Grep über `src` zeigt nur `K3`, `KS3`, `PA3` und den Katalogeintrag `CapabilityCatalog.cs:58`).
2. Stapel-Anwendung: Datei/CSV mit neuen Texten einlesen, Kategorien hinzuschreiben, exportieren.
3. Bessere Modelle bei gleichem Bedienweg (z. B. Einbettungen einer lokalen KI + linearer Klassifikator); Kreuzvergleich statt einer Aufteilung.
4. Echte Tabellen-Arbeitsfläche (virtuell, > 300 Zeilen, Einfügen aus Excel, Kategorie-Auswahlliste).
5. Wenn „Eigene KI“ wörtlich gemeint ist: Feintuning eines lokalen Sprach-/Bildmodells (LoRA) – heute bewusst nicht vorhanden; sehr großer Aufwand.
6. Standard-Export (ONNX) oder kleines Kommandozeilen-/API-Stück, um das Modell außerhalb zu nutzen.

**Abnahmekriterien:**
- Eine CSV mit 1 000 ungesehenen Texten wird mit einem trainierten Modell in einem Schritt klassifiziert und mit Kategorie + Sicherheit exportiert.
- Das trainierte Modell lässt sich in mindestens einem anderen Trendo-Programm auswählen und sortiert dort echte Einträge.
- Die Auswertung zeigt Genauigkeit über mindestens fünf Aufteilungen (Mittelwert und Streuung).
- 5 000 Beispiele lassen sich flüssig ansehen und bearbeiten.

**Aufwand:** mittel (Nutzung + Stapel + Tabelle) · sehr groß (echtes Feintuning) · **Risiko:** gering für den Ausbau, hoch für Feintuning (GPU, Laufzeit, Datenmenge).
**Dateien:** `KS3:816-1028`, `K3:435-603`, `PA3:108-145`.

---

## Auffälligkeiten Teil A1

1. **Drei von sechs KI-Aktionen liefern nur Plan-Text statt des versprochenen Produkts:** `bild.auftrag` → `bildauftrag.txt` (`PA2:138-143`), `video-ki.szenenplan` → `szenenplan.md` (`PA3:27-32`), `modelle.fragebogen` → `fragebogen.md` (`PA3:98-104`). Alle sechs Adapter stehen ehrlich auf `CapabilityLevel.Partial`. Echt arbeiten nur `zusammenfassung.schreiben`, `agent.entwurf` (Datei) und `ki.trainieren`.
2. **„Videos mit KI“ erzeugt kein KI-Video**, sondern eine Standbild-Diashow ohne Ton (`K3:117-150`, `-an` in `K3:150`). Der Knopf „Echter KI-Clip (kie.ai)“ öffnet nur ein Flyout oder schreibt einen Hinweis (`KS3:249-254`) – eine Weiterleitung, keine Funktion des Programms. Gleichzeitig existiert im Repo ein real belegter lokaler Videogenerator (Wan 2.2, `reports/paket01/s3-13-ki-video-lauf.md`), der hier nicht genutzt wird.
3. **„KI-Agenten bauen“ führt keine Agenten aus.** „Ausprobieren“ ist ein Rollenspiel-Prompt im Claude-Chat; die Werkzeugliste wird nur als Satz mitgegeben, nicht erzwungen (`KS3:457-459`). Kein Zeitplan, kein Auslöser, keine Fehlerbehandlung in diesem Studio.
4. **Bild-KI nie echt gelaufen (für diese Programme):** `ImageAiOverride` ersetzt sd-cli in allen UI-Tests (`KreativStudios2UiTests.cs:140`, `KreativStudios3UiTests.cs:83`), `VideoAiFfmpegOverride` ersetzt FFmpeg (`:85`), `KiBackendsOverride` ersetzt alle Modelle (`:208`). Kein `TRENDO_*_ECHT`-Schalter und kein Beleg unter `reports/` für bild-ki/video-ki/modelle. Einzige echte Werkzeugprüfung in Teil A1: `KreativKits3Tests.cs:74` (FFmpeg, übersprungen, wenn FFmpeg fehlt).
5. **Stiller Knopf:** `sum-claude` verwirft das Ergebnis von `ClaudeFromStudioBar` (`KS:90`). Ist Claude nicht verbunden (`MainWindow.ClaudeLos.cs:46` gibt `false`), passiert beim Klick nichts – anders als bei Video/Agenten, die `ClaudeOrHint` nutzen (`KS3:45-48`).
6. **Claude-Ergebnisse landen im Chat, nicht im Dokument** (Zusammenfassen `KS:90`, Szenen verbessern `KS3:279`, Agent ausprobieren `KS3:458`). Ob die allgemeine Übernahme „Claude hat geändert“ (`PS:340`) für diese `.tdoc`-Dokumente greift, wurde nicht geprüft.
7. **Doku ≠ Code:** `docs/programme/katalog.json` führt alle sechs Programme mit `Status: IN_PROGRESS / Test NOT_RUN` und leerer Testliste, obwohl Core- und UI-Tests existieren (siehe Belege oben). Der Katalog nennt zudem je Programm die Engines der ganzen Datei (z. B. bei „Eigene KI trainieren“: `VideoAiKit`, `ModelCompare`, `ImageAiKit` …) und Formate wie `.mp4/.ogg/.wav`, die das Programm nicht erzeugt – offenbar je Quelldatei statt je Programm erhoben.
8. **Kommentar ≠ Verhalten:** `PA3:108` sagt „legt Modell und Auswertung im Projekt ab“, geschrieben wird nur `modell.json` (`PA3:142`).
9. **Kein Abbrechen** bei Bildmalen, Videobau und Modellfragen (nur Sperrflags `_imgAiBusy`/`_videoBusy`/`_modelsBusy`; FFmpeg hat eine 15-Minuten-Grenze `KS3:215`).
10. **Rückgängig vs. Dateien:** „Bild gemalt“/„Video gebaut“ lässt sich im Dokument zurücknehmen, die Dateien unter `ki-bilder/` bzw. `ki-videos/` bleiben liegen (`KS2:634`, `KS3:223`) – ob aufgeräumt wird, nicht geprüft.
11. **Bedienung:** In Hülle und den drei Oberflächendateien für diese sechs Programme kein Drag-and-drop und kein Kontextmenü gefunden.
12. Positiv: Sicherheitsgrenzen sind durchgängig echt (nur eigene Ordner `KS2:545-550`, `KS3:61-66`; nie überschreiben `KS3:194`, `K3:318`; Inhaltssperre `K2:350-358`; Herkunftsnachweis mit SHA-256 `K2:375-380`; fremde Agent-Datei verlangt zweite Bestätigung `KS3:451-456`).

Nicht geprüft: tatsächlicher Lauf irgendeines Programms (kein Start, kein Test); kie.ai-Weg aus diesen Studios; Ausführung eigener Rollen im KI-Team; ob `ClaudeChangeBanner` Dokumente dieser Programme ändert; Verhalten bei sehr großen Dokumenten.

---

# Teil A2: Audit Gruppe KI & Wissen – Teil A2 (Code mit KI, Bilder erkennen, Recherche mit Quellen, Messwerte, Karteikarten, Wissen, Sicher recherchieren)

Stand: Arbeitskopie `studios-aufraeumen`, nur gelesen. Pfade relativ zum Repo. „KS4“ = `src/Trendo.App/MainWindow.KreativStudios4.cs`, „Shell“ = `src/Trendo.App/MainWindow.ProgramShell.cs`, „KK4“ = `src/Trendo.Core/Kreativ/KreativKits4.cs`.

Gemeinsame Grundlage der drei Hüllen-Programme (code-ki, bilder-erkennen, quellen, wissenschaft): Die Programm-Hülle liefert Neu/Vorlagen/Zuletzt/Wiederherstellen (Shell:15, :520–526), Rückgängig/Wiederholen auf Dokumentebene (Shell:615–616, `ProgramDocument.cs:87,94`, max. 100 Schritte :38), Autosave alle 30 s + 1,5 s nach Änderung + beim Schließen (Shell:83–88, :228, :245), Versionen (Shell:592–593), Strg+S/Z/Y/N/W (Shell:343–352), Bedienstufen Einfach/Erweitert/Profi (Shell:18, :627, :639). Rechtsklick-Menü, Drag-and-drop und Vollbild: in Shell.cs kein Treffer (Grep `ContextMenu|DragDrop|Vollbild|FullScreen`) – für diese Programme als FEHLT gewertet, an anderer Stelle nicht geprüft.

## Übersicht

| Studio-Id | Name | Programme | Gesamturteil | größte Lücke | Aufwand |
|---|---|---|---|---|---|
| STUDIO-CODEAI-01 | Code mit KI | code-ki-programm | TEILWEISE | Claude arbeitet nicht im geöffneten Projektordner und ändert die Datei nicht selbst – bekommt nur einen 12 000-Zeichen-Ausschnitt und soll die Änderung „zeigen“; kein Übernehmen-Schritt | mittel |
| STUDIO-VISION-01 | Bilder erkennen | bilder-erkennen-programm | TEILWEISE | Name verspricht Erkennung, geliefert wird Bildprüfung (EXIF, Farben, Helligkeit, Schärfe); Inhaltserkennung nur über Claude, OCR nur mit nachinstalliertem Tesseract und ungetestet | mittel–groß |
| STUDIO-CITE-01 | Recherche mit Quellen | quellen-programm | TEILWEISE (nah an vollwertig) | Formularfelder statt Import per DOI/ISBN/URL; kein RIS/CSL/Word-Export; nur 3 Quellenarten | mittel |
| SPEC-05 | Messwerte auswerten | wissenschaft (+ eingegliedert SPEC-10 zwilling) | TEILWEISE (nah an vollwertig) | CSV-Import nur über eingetippten Pfad, kein PNG/PDF/XLSX, keine Fehlerbalken/Mehrfachdiagramme | mittel |
| SPEC-06 | Lernen mit Karteikarten | lernen | TEILWEISE | Nur Textkarten; kein Anki-Import (.apkg), keine Bilder/Audio, kein Rückgängig, nicht in der Programm-Hülle | mittel |
| CORE-10 | Wissen merken | ki-wissen | TEILWEISE | Einfaches Textfeld statt Markdown-Editor; kein Export aus der Oberfläche; Suche ist Stichwort-BM25 (keine Bedeutungssuche) | mittel |
| SYS-08 | Sicher recherchieren | recherche | TEILWEISE | Keine Suche (nur Adresse + feste Startpunkte); Sitzung flüchtig, nichts bleibt; echter Netzabruf nur mit ersetztem Abruf getestet | mittel–groß |
| SPEC-07 | Texte übersetzen | – | ABGELEHNT (nur Vermerk) | – | – |

## STUDIO-CODEAI-01 Code mit KI

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | VOLLWERTIG (klein) | Dateibaum + AvaloniaEdit-Editor mit Syntaxfarben, Testkarte, Diff-Ansicht: KS4:371–489 (Editor :397–400) |
| 2 | Werkzeuge | TEILWEISE | Öffnen/Speichern/Schnappschuss/Diff/Tests real (KS4:111–232); die drei Claude-Knöpfe schicken nur einen Auftragstext (KS4:245–268) |
| 3 | Neues Projekt | TEILWEISE | Nur „Projekt öffnen“ und ein Python-Beispiel (KS4:308–312, :291–300); kein neues Projekt anlegen, keine neue Datei im Baum |
| 4 | Echte Dateien importieren | VOLLWERTIG | Beliebiger lokaler Ordner, Schutz gegen verknüpfte Ordner (KS4:111–121, KK4:121) |
| 5 | Vorhandenes bearbeiten | VOLLWERTIG | Datei laden, ändern, zurückschreiben, Prüfsummen-Abgleich gegen Fremdänderung (KS4:141–155) |
| 6 | Rückgängig | TEILWEISE | Im Editor das Rückgängig von AvaloniaEdit (nicht geprüft/getestet); Dokument-Rückgängig der Hülle betrifft nur root/file/wish, nicht den Dateiinhalt; kein „Claude-Änderung zurücknehmen“ |
| 7 | Vorschau | TEILWEISE | Zeilen-Diff mit Farben (KS4:472–484); keine Programmausgabe außer Tests |
| 8 | Speichern/Autosave | TEILWEISE | Autosave gilt dem .tdoc, die offene Quelldatei wird nur per Klick gespeichert (KS4:141; Hinweis „● ungespeichert“ :402) |
| 9 | Absturz-Wiederherstellung | FEHLT (für Dateiinhalt) | Ungespeicherter Editortext liegt nur im Speicher (`_caDoc`, KS4:59, :98–102); Hüllen-Wiederherstellung sichert nur das .tdoc |
| 10 | Export | TEILWEISE | .patch (Unified Diff) und Bericht .md (KS4:331–335) |
| 11 | Rundreise | NICHT GETESTET | Test prüft nur Inhalt des Patches (KreativStudios4UiTests.cs:100), kein `git apply` |
| 12 | Fachfunktion real | TEILWEISE | Testlauf ist echter Prozessstart über `Cli.Run` mit Zeitlimit (KS4:221–222) für .NET/npm/Python/Cargo/Go (KK4:38–53); „Mit Claude ändern“ ändert nichts selbst (siehe 13) |
| 13 | Claude/KI | TEILWEISE | Echter Claude-Code-Lauf (`ClaudeFromStudioBar` → `ClaudeChatSend` → `ClaudeRun`, MainWindow.ClaudeLos.cs:44–56, ClaudeChat.cs:375, :498). Aber: Arbeitsordner ist der Chat-Ordner `Projekte/Claude/Code mit KI` (ClaudeChat.cs:54–58, :761–766 – nur „dev“ und „spiele“ bekommen den echten Projektordner), Claude erhält nur einen Ausschnitt bis 12 000 Zeichen (KS4:241) und die Anweisung „Zeig die Änderung als vollständige Datei oder als Diff“ (KS4:259). Kein Knopf, der den Vorschlag in die Datei übernimmt. Lokale KI: Adapter `code.projekt` schreibt nur einen Bericht (KreativPromptAdapters4.cs:9–39, Stufe Partial, SideEffect blockiert :39) |
| 14 | Tests | TEILWEISE / Ende-zu-Ende NICHT GETESTET | UI-Test mit `CodeAiRunOverride` (ersetzt, KreativStudios4UiTests.cs:55) und ohne Claude (:104); Core `KreativKits4Tests.Code/Snapshot/Diff`. Bericht nennt selbst: echter Testlauf und Claude nicht geprüft (tasks/T-0615-kreativ-ki-4.bericht.md:37–38) |
| 15 | Bedienung | TEILWEISE | Hüllen-Kürzel, Bedienstufen; kein Rechtsklick im Baum, kein Drag-and-drop, keine Suche, nur eine Datei gleichzeitig (Verweis auf den großen Code-Editor KS4:318) |

**Gesamturteil: TEILWEISE** – solide Prüf- und Vergleichsumgebung, aber das Kernversprechen „mit Claude ändern“ endet als Text im Chat statt als geänderte Datei.

**Was fehlt (priorisiert):**
1. Claude im geöffneten Projektordner arbeiten lassen (Arbeitsordner = `root`, ganze Dateien statt Ausschnitt), Änderungen direkt als Datei, davor automatischer Schnappschuss.
2. Annehmen/Ablehnen je Datei bzw. je Änderungsblock im Diff, „Claude-Änderung zurücknehmen“ aus dem Schnappschuss.
3. Echter Ende-zu-Ende-Nachweis: Testlauf (mind. Python und .NET) und ein Claude-Lauf mit Rohlog unter `reports/`.
4. Sicherung des ungespeicherten Editortexts (Autosave/Wiederherstellung der Quelldatei).
5. Neue Datei/Ordner, mehrere Reiter, Suche im Projekt – oder klare Übergabe an den Code-Editor.
6. Testfehler anklickbar (Sprung zur Zeile), „Fehler an Claude geben“.

**Abnahmekriterien:**
- Wunsch „Teilen durch null gibt eine Meldung“ auf dem Beispielprojekt: `rechner.py` auf der Platte ist danach geändert, der Diff zeigt genau diese Änderung, „Ablehnen“ stellt den alten Stand her.
- „Tests ausführen“ startet echtes `python -m unittest` und zeigt Exit-Code und Ausgabe; Rohlog liegt unter `reports/`.
- Die exportierte .patch lässt sich mit `git apply --check` auf den Schnappschuss-Stand anwenden.
- Nach hartem Beenden mit ungespeichertem Editortext bietet der Neustart den Text zur Wiederherstellung an.

**Aufwand:** mittel. **Risiko:** mittel–hoch (Claude schreibt in fremde Projektordner: Rechte, Geheimnisse in Dateien, verknüpfte Ordner; Abo-Grenzen).
**Dateien:** KS4:50–504, KK4:11–275 (CodeAiKit), `src/Trendo.Core/CodeProject.cs`, `src/Trendo.App/MainWindow.ClaudeLos.cs`, `MainWindow.ClaudeChat.cs`, `src/Trendo.Core/W2/ClaudeCode.cs:93–105`, `src/Trendo.Core/PromptAction/KreativPromptAdapters4.cs`, `tests/Trendo.App.Tests/KreativStudios4UiTests.cs:46–107`.

## STUDIO-VISION-01 Bilder erkennen

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE | Bildvorschau + Faktenliste + Ordnerliste (KS4:704–770); keine Bearbeitung, keine Markierungen im Bild |
| 2 | Werkzeuge | TEILWEISE | Bild öffnen, Ordner prüfen (bis 2000 Bilder, KS4:561–587), OCR, Claude; real, aber wenige |
| 3 | Neues Projekt | VOLLWERTIG (klein) | Vorlagen leer/Testbild (KS4:655–659) |
| 4 | Import | VOLLWERTIG | jpg/png/bmp/gif/webp/tif, 60 MB/100 MP-Grenze (KS4:514–515, :527–541) |
| 5 | Bearbeiten | TEILWEISE | Dokument merkt Bild, Ordner, Grenzwert, OCR-Text; Messwerte werden beim Öffnen neu berechnet (KS4:708–709), Ordnerergebnis nicht gespeichert (`_viFolder` nur im Speicher, KS4:513) |
| 6 | Rückgängig | TEILWEISE | Hülle; fachlich kaum relevant |
| 7 | Vorschau | VOLLWERTIG (klein) | Bild + Farbfelder + Balken (KS4:719–743) |
| 8 | Speichern | VOLLWERTIG | Hülle (.tdoc) |
| 9 | Wiederherstellung | VOLLWERTIG | Hülle |
| 10 | Export | TEILWEISE | Bildbericht .md, Liste .csv mit BOM (KS4:674–678); keine Umbenennung/Sortierung/Verschlagwortung der Bilder |
| 11 | Rundreise | NICHT GETESTET | CSV wird nur auf Zeilenzahl/Kopf geprüft (KreativStudios4UiTests.cs:156–157) |
| 12 | Fachfunktion real | TEILWEISE | Echte Pixelmessung: Laplace-Schärfe, Belichtung, k-means-Farben, EXIF-Parser (KK4:277–495). **Keine** Objekt-, Szenen- oder Gesichtserkennung, keine Bildunterschrift lokal (bewusst, KS4:779). OCR über Tesseract-Prozess (KS4:592–615), nur wenn installiert |
| 13 | Claude/KI | TEILWEISE | „Mit Claude beschreiben“ hängt das Bild an und fragt Claude (KS4:695–702) – Antwort bleibt Text im Chat, wird nicht ins Dokument/den Bericht übernommen. Lokaler Adapter listet nur Format/Größe/EXIF (KreativPromptAdapters4.cs:41–82; CapabilityCatalog.cs:82) |
| 14 | Tests | TEILWEISE | UI-Test `Vision` mit `VisionTesseractOverride = () => null` – OCR-Erfolgsweg NICHT GETESTET (KreativStudios4UiTests.cs:129, :143); Core `Pixels`, `Facts` (KreativKits4Tests.cs:149, :218). Bericht: Tesseract und Claude nicht geprüft (T-0615-Bericht:38) |
| 15 | Bedienung | TEILWEISE | Hüllen-Kürzel; kein Drag-and-drop eines Bildes, kein Rechtsklick, kein Vollbild |

**Gesamturteil: TEILWEISE** – ehrliche lokale Bildprüfung, aber kein „Erkennen“ im Sinne des Namens; der Katalogsatz („Bilder ohne Internet prüfen“, StudioCatalog.cs:87) ist ehrlicher als der Studioname.

**Was fehlt:**
1. Lokale Inhaltserkennung (Bildbeschreibung/Schlagworte über ein lokales Bild-Sprach-Modell, falls vorhanden) oder Umbenennung des Studios in „Bilder prüfen“.
2. OCR-Nachweis mit echtem Tesseract (deu/eng), Textlage im Bild, Kopieren/Export als .txt.
3. Claude-Beschreibung ins Dokument und in den Bericht übernehmen.
4. Ordnerergebnis speichern; Aktionen auf Treffern (unscharfe aussortieren/verschieben in Unterordner, mit Rückgängig).
5. Drag-and-drop, Doppelte finden, Vergleich zweier Bilder.

**Abnahmekriterien:**
- Ein Foto mit gedrucktem deutschem Text ergibt mit installiertem Tesseract den Text im Feld; Rohlog unter `reports/`.
- „Ordner prüfen“ auf 200 echten Fotos markiert unscharfe/zu dunkle; die Liste ist nach Neustart wieder da.
- Entweder liefert das Studio ohne Internet eine Inhaltsbeschreibung, oder Name und Kacheltext versprechen nur „prüfen“.
- Claude-Beschreibung steht danach im Bildbericht (.md).

**Aufwand:** mittel (ohne lokales Modell) bis groß (mit). **Risiko:** mittel (Modellgröße/Lizenz, Datenschutz bei Personen).
**Dateien:** KS4:506–781, KK4:277–545 (VisionKit), `Ocr` (Aufruf KS4:605–608), `KreativPromptAdapters4.cs:41–82`, Test KreativStudios4UiTests.cs:117–161.

## STUDIO-CITE-01 Recherche mit Quellen

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE | Quellenliste links, Feldformular rechts mit Live-Vorschau der Angabe, Literaturverzeichnis unten (KS4:989–1085) – fachüblich für Literaturverwaltung, aber Formular |
| 2 | Werkzeuge | VOLLWERTIG | Quelle anlegen, Stil wechseln, Doppelte, BibTeX, Übernahme aus SYS-08 (KS4:949–964) |
| 3 | Neues Projekt | VOLLWERTIG | leer/Beispiel (KS4:944–948) |
| 4 | Import | TEILWEISE | BibTeX bis 5 MB mit Warnungen (KS4:890–900, KK4:978); kein RIS, kein DOI/ISBN-Abruf, kein PDF |
| 5 | Bearbeiten | VOLLWERTIG | Felder je Quellenart, Zitate mit Seite/Notiz (KS4:861–887) |
| 6 | Rückgängig | VOLLWERTIG | Jede Änderung über `d.Edit` (KS4:825–827) |
| 7 | Vorschau | VOLLWERTIG | Formatierte Angabe mit Kursiv, fehlende Angaben (KS4:1030–1041) |
| 8/9 | Speichern / Wiederherstellung | VOLLWERTIG | Hülle |
| 10 | Export | TEILWEISE | .md, .bib, .txt (KS4:965–970); kein .docx/.odt/RIS/CSL-JSON |
| 11 | Rundreise | VOLLWERTIG | BibTeX hin und zurück im UI-Test (KreativStudios4UiTests.cs:201–205) und `BibTex_roundtrip` (KreativKits4Tests.cs:338) |
| 12 | Fachfunktion real | VOLLWERTIG | APA 7, Harvard, DIN-ISO-690-nah, Kurzbeleg, Sortierung, Doppelte, DOI-Prüfung (KK4:578–950) |
| 13 | Claude/KI | TEILWEISE | „Mit Claude prüfen“ = Frage, ob die Quelle die Aussage belegt, Antwort als Chat-Text (KS4:921–932); Claude liest die Quelle nicht selbst. Adapter formatiert eine Quelle als .md (KreativPromptAdapters4.cs:84 ff.) |
| 14 | Tests | VOLLWERTIG (ohne Fremdwerkzeug) | UI `Cite` (KreativStudios4UiTests.cs:165–213), Core `Styles`, `People_sort_missing`, `Duplicates`, `BibTex_*` (KreativKits4Tests.cs:259–400); Claude-Weg nicht getestet |
| 15 | Bedienung | TEILWEISE | Hüllen-Kürzel; kein Rechtsklick, kein Drag-and-drop, kein „Zitat in die Zwischenablage“-Knopf gefunden (nicht geprüft) |

**Gesamturteil: TEILWEISE (nah an vollwertig)** – fachlich saubere kleine Literaturverwaltung; der Name „Recherche“ verspricht mehr als Verwalten und Formatieren (die Recherche selbst liegt in SYS-08).

**Was fehlt:** 1. Quelle aus DOI/ISBN/URL automatisch füllen. 2. Weitere Quellenarten (Sammelband-Kapitel, Hochschulschrift, Gesetz/Urteil, Video). 3. Export als .docx/.odt und RIS/CSL-JSON; Einfügen in das Text-Programm. 4. Übernahme aus SYS-08 überlebt das Sitzungsende nicht (Quelle sind flüchtige Sitzungsdaten, KS4:907, Research.cs:113). 5. PDF/Datei an die Quelle hängen, Volltext für die Claude-Prüfung. 6. Fußnoten-Stil.

**Abnahmekriterien:**
- Eine DOI eingeben füllt Autor, Titel, Jahr, Zeitschrift.
- 50 Quellen aus einer Zotero-BibTeX-Datei: Export und Wiedereinlesen ergibt dieselben Datensätze.
- Literaturverzeichnis als .docx öffnet in Word/LibreOffice mit Kursiv und hängendem Einzug.
- „Mit Claude prüfen“ mit angehängtem PDF nennt die Fundstelle (Seite) oder sagt „nicht belegt“.

**Aufwand:** mittel. **Risiko:** niedrig–mittel (Netzabruf bei DOI: Offline-Modus, Herkunftsnachweis).
**Dateien:** KS4:783–1100, KK4:548–1180 (CiteKit), `KreativPromptAdapters4.cs:84 ff.`, Tests s. o.

## SPEC-05 Messwerte auswerten (Programm „Messdaten & Auswertung“)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | VOLLWERTIG | Eintipp-Tabelle (`MeasureGrid`) + Live-Diagramm (MainWindow.Spec05.cs:76–84) |
| 2 | Werkzeuge | VOLLWERTIG | 5 Diagrammarten, 4 Ausgleichskurven mit R², Kennzahlen, Welch-/gepaarter t-Test, Chi², Stichprobe, Fehlerfortpflanzung (Spec05.cs:33–49, :103–152) |
| 3 | Neu | VOLLWERTIG | 4 Vorlagen (Spec05.cs:25–31) |
| 4 | Import | TEILWEISE | CSV über eingetipptes Pfadfeld `ms-csv-path`, kein Dateidialog (Spec05.cs:143–145); kein XLSX/ODS |
| 5 | Bearbeiten | VOLLWERTIG | Zellen, Spalten benennen/löschen, abgeleitete Spalten (Spec05.cs:68–74, :97–101, :135–138) |
| 6 | Rückgängig | VOLLWERTIG | alles über `d.Edit` (Spec05.cs:68–72) |
| 7 | Vorschau | VOLLWERTIG | Diagramm live |
| 8/9 | Speichern / Wiederherstellung | VOLLWERTIG | Hülle |
| 10 | Export | TEILWEISE | CSV, Auswertung .md, Diagramm .svg (Spec05.cs:51–56); kein PNG/PDF/XLSX |
| 11 | Rundreise | TEILWEISE | „CSV laden“ im UI-Test (MeasureUiTests.cs:57); CSV-Export→Import als Rundreise nicht geprüft |
| 12 | Fachfunktion real | VOLLWERTIG | Statistik mit nachgerechneten Tests (W6Tests.cs:104, :450–459) |
| 13 | Claude/KI | TEILWEISE | Adapter `messung.auswerten` rechnet Kennzahlen aus eingetippten Werten (MeasurePromptAdapter.cs:273–296, Partial); Claude kann über das .tdoc im Chat-Ordner das Dokument ändern (allgemeiner Weg ClaudeChat.cs:428–433) – für dieses Programm nicht getestet |
| 14 | Tests | VOLLWERTIG (kein Fremdwerkzeug nötig) | MeasureUiTests.cs:23, :57; W6Tests SPEC-05 F05–F13 |
| 15 | Bedienung | TEILWEISE | Enter/Tab/Entf in der Tabelle (Spec05.cs:60, MeasureGrid.cs:91), Bedienstufen (Spec05.cs:21); Einfügen aus der Zwischenablage, Rechtsklick, Drag-and-drop einer CSV: im Code nicht gefunden |

**Gesamturteil: TEILWEISE (nah an vollwertig)** – echtes kleines Auswerteprogramm; es fehlen Komfort beim Import und druckfähige Ausgabe.

**Was fehlt:** 1. Dateidialog/Drag-and-drop und Einfügen aus Excel. 2. PNG/PDF-Export des Diagramms, Bericht als PDF. 3. Fehlerbalken, mehrere Diagramme, Achsen (log, Bereich, Titel) – laut eigener Liste offen (docs/claude/vorbilder/messdaten-origin-scidavis.md:14–15). 4. XLSX/ODS-Import. 5. Formelspalten. 6. Eingegliedertes SPEC-10 „Geräte überwachen“ (`zwilling`, StudioCatalog.cs:150) gehört fachlich zur Technik-Gruppe – hier nicht bewertet.

Doku≠Code: Die Vorbild-Liste führt „✗ Statistik-Tests“ (messdaten-origin-scidavis.md:16), im Code sind t-Test, gepaarter Test und Chi² vorhanden (Spec05.cs:118–127, :147).

**Abnahmekriterien:** CSV mit deutschen Zahlen per Dialog oder Ziehen laden; Diagramm mit Ausgleichsgerade als PNG und PDF exportieren; Werte aus Excel per Strg+V einfügen; CSV-Export wieder laden ergibt dieselbe Tabelle.
**Aufwand:** mittel. **Risiko:** niedrig.
**Dateien:** `src/Trendo.App/MainWindow.Spec05.cs`, `src/Trendo.App/MeasureGrid.cs`, `src/Trendo.Core/Messdaten/MeasureSheet.cs`, `MeasurePromptAdapter.cs:273 ff.`, `tests/Trendo.App.Tests/MeasureUiTests.cs`.

## SPEC-06 Lernen mit Karteikarten

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | VOLLWERTIG (für Lernen) | Große Karte, Modi Lernen/Eintippen/Quiz/Karten/Lernplan (MainWindow.Spec06.cs:43–61, :85–91) |
| 2 | Werkzeuge | VOLLWERTIG | SM-2 und FSRS mit Behaltensrate (Spec06.cs:291–300), Tippfehler-Toleranz (:146), Quiz, Problemkarten, Prüfungsplan, Kalender, Verlauf (:276–358) |
| 3 | Neu | TEILWEISE | Stapel entsteht beim Anlegen einer Karte (Spec06.cs:192–196); kein Dokument-/Projektbegriff, `RegisterTab` statt Programm-Hülle (:26) |
| 4 | Import | TEILWEISE | Nur eingefügte Liste „Frage;Antwort“ (Spec06.cs:219–222); kein Datei-Import, kein .apkg |
| 5 | Bearbeiten | VOLLWERTIG | Tabelle mit Suche, Ändern, Löschen, Umbenennen, Umkehren (Spec06.cs:199–209, :262–265) |
| 6 | Rückgängig | FEHLT | Keine Rückgängig-Funktion im Code (Grep `Undo` in Spec06.cs und Learning.cs ohne Treffer); Löschen ist endgültig (Learning.cs:128) |
| 7 | Vorschau | VOLLWERTIG | Abstand-Vorschau auf den Bewertungsknöpfen (Test „Abstand-Vorschau“, s. u.) |
| 8 | Speichern | VOLLWERTIG | Sofort in den Kartenspeicher (Learning.cs:41); Neustart-Test FSRS +8 |
| 9 | Wiederherstellung | TEILWEISE | Durch Sofort-Speichern kein Verlust; keine Versionen/Sicherung des Kartenspeichers gefunden |
| 10 | Export | TEILWEISE | Nur CSV je Stapel (Spec06.cs:267–268); kein Anki-Format, kein Druck |
| 11 | Rundreise | NICHT GETESTET | CSV-Export → Wieder-Einfügen nicht als Test gefunden |
| 12 | Fachfunktion real | VOLLWERTIG | Wiederholungsplaner von Hand nachgerechnet (FSRS +1 … +10) |
| 13 | Claude/KI | TEILWEISE | Adapter `karte.anlegen` legt eine Karte an (StudyPromptAdapter.cs:25–48, Partial); kein „Karten aus Text/PDF erzeugen“ im Studio gefunden |
| 14 | Tests | VOLLWERTIG | „SPEC-06 UI“ (2 Tests), FSRS-Oberfläche, Lernplan, FSRS +1…+10 |
| 15 | Bedienung | TEILWEISE | Leertaste, 1–4 (Spec06.cs:63–71); keine Bedienstufen, kein Rechtsklick/Drag-and-drop |

**Gesamturteil: TEILWEISE** – das Lernen selbst ist echt und gut getestet; als Karteikarten-Programm fehlen Medienkarten, Austauschformate und Rückgängig.

**Was fehlt:** 1. Rückgängig für Bewertung und Löschen (Papierkorb). 2. Anki-Import/-Export (.apkg) und CSV-Datei-Import per Dialog. 3. Karten mit Bild, Ton, Formel, Lückentext. 4. Karten aus eigenem Text/PDF mit KI erzeugen (mit Durchsicht). 5. Sicherung/Versionen des Kartenspeichers. 6. Vorlesen (Piper ist im Projekt vorhanden – Anbindung hier nicht gefunden).

**Abnahmekriterien:** Ein Anki-Stapel mit 100 Karten und Bildern wird eingelesen und zeigt die Bilder; versehentliche Bewertung lässt sich mit Strg+Z zurücknehmen; gelöschte Karte kommt aus dem Papierkorb zurück; CSV-Export und Wiedereinlesen ergibt denselben Stapel samt Lernstand.
**Aufwand:** mittel. **Risiko:** niedrig–mittel (Anki-Format ist SQLite+Medien).
**Dateien:** `src/Trendo.App/MainWindow.Spec06.cs`, `src/Trendo.Core/W6/Learning.cs`, `W6/LearnPlan.cs`, `StudyPromptAdapter.cs:25–48`.

## CORE-10 Wissen merken (Programm „Wissen für die KI“)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE | Drei Spalten Seiten/Lesen-Bearbeiten/Rückverweise; Bearbeiten in einfachem Textfeld (MainWindow.Wissen.cs:123–125), keine Formatierung |
| 2 | Werkzeuge | VOLLWERTIG | [[Verweise]], Rückverweise, fehlende Seiten, Umbenennen passt Verweise an (KnowledgeWiki.cs:15–43), Wissensnetz (MainWindow.WissenNetz.cs), Wochenrückblick, Papierkorb |
| 3 | Neu | VOLLWERTIG | „＋ Neue Seite“ (Wissen.cs:66) |
| 4 | Import | TEILWEISE | .txt/.md/.csv aus Ordner über Pfadfeld (Wissen.cs:87–95); Word/PDF/PowerPoint/Excel/HTML nur nach Einrichtung eines Zusatzwerkzeugs (WissenDocs.cs:21–28, Test „ohne Einrichtung Einrichten-Knopf“) – echter Umwandlungslauf nicht geprüft |
| 5 | Bearbeiten | VOLLWERTIG | Update mit Titelprüfung (Wissen.cs:138–141) |
| 6 | Rückgängig | TEILWEISE | Papierkorb mit Wiederherstellen (Wissen.cs:110–120); kein Rückgängig/keine Versionen für Textänderungen gefunden |
| 7 | Vorschau | VOLLWERTIG | Lesemodus mit anklickbaren Verweisen; Netzgrafik (Test W3: Wissensnetz) |
| 8 | Speichern | TEILWEISE | Nur per Knopf „Speichern/Merken“ (Wissen.cs:128–141); kein Autosave des Bearbeitungsfelds gefunden |
| 9 | Wiederherstellung | FEHLT | Ungespeicherter Seitentext geht bei Absturz verloren (kein Entwurfsspeicher gefunden) |
| 10 | Export | FEHLT (Oberfläche) | `ExportJson` existiert im Kern (Knowledge.cs:101), wird in `src/Trendo.App` nicht aufgerufen (Grep ohne Treffer); kein Markdown-Ordner-Export |
| 11 | Rundreise | FEHLT | folgt aus 10 |
| 12 | Fachfunktion real | VOLLWERTIG | Suche BM25 über Titel/Text/Schlagworte (Knowledge.cs:79–98), Test „Suche findet Wissen trotz Umlauten und Mehrzahl, bleibt nach Neustart“ |
| 13 | Claude/KI | TEILWEISE | Lokale KI nutzt das Wissen wirklich: `IntelligenceCore` baut Treffer ein, wenn `UseKnowledge` (IntelligenceCore.cs:32, :81; Anbindung MainWindow.W2.cs:27 → Wissen.cs:30; Test „CORE-02: … Wissen wird eingebaut“). „Frag dein Wissen“ antwortet nur aus den Treffern mit Quellenliste (Wissen.cs:41–56). Einschränkungen: Stichwort-Suche, keine Bedeutungssuche; Wissen geht bewusst nie an Dienste außerhalb des PCs (Test T-0225) – ob Claude-Chats das Wissen bekommen: nicht geprüft; viele Studios schalten `UseKnowledge: false` (z. B. KreativStudios3.cs:651, Mail.cs:51). Adapter `wissen.merken` legt einen Eintrag an (StudyPromptAdapter.cs:4–23) |
| 14 | Tests | VOLLWERTIG (Kern) | Notiz-Wiki (UI + Core, 4 Tests), „CORE-10 UI: Merken, Suchen, Löschen und Korrektur“, W3 Wissensnetz, Dokumente einlesen (mit ersetzter Umwandlung – nicht geprüft, ob echt) |
| 15 | Bedienung | TEILWEISE | Enter in der Suche; keine Kürzel fürs Bearbeiten, kein Rechtsklick, kein Drag-and-drop von Dateien (Pfad eintippen), keine Bedienstufen |

**Gesamturteil: TEILWEISE** – echtes Notiz-Wiki mit funktionierender Wissensübergabe an die lokale KI; als „Obsidian-artiges“ Programm fehlen Editor, Export und Sicherheit beim Schreiben.

Doku≠Code: Die Vorbild-Liste führt Verknüpfungen und Wochenrückblick als „✗ fehlt“ (docs/claude/vorbilder/wissen-obsidian.md:14, :16), beides ist gebaut (Wissen.cs:67–68, KnowledgeWiki.cs).

**Was fehlt:** 1. Autosave/Entwurf und Versionen je Seite. 2. Export (Markdown-Ordner, JSON) und Wiedereinlesen. 3. Markdown-Editor mit Überschriften, Listen, Bildern, Vorschau. 4. Bedeutungssuche (lokale Einbettungen) zusätzlich zu BM25, Trefferanzeige „warum“. 5. Dateien per Dialog/Ziehen einlesen; PDF/Word ohne Zusatz-Einrichtung. 6. Sichtbar machen, welche Seiten die KI in einer Antwort benutzt hat (über „Frag dein Wissen“ hinaus).

**Abnahmekriterien:** Tippen und App hart beenden – der Text ist beim Neustart da; Export als Markdown-Ordner und Einlesen in ein leeres Wissen ergibt dieselben Seiten und Verweise; eine Frage in „KI fragen“ zu einem nur im Wissen stehenden Fakt wird richtig beantwortet und nennt die Seite; eine PDF wird ohne Zusatzschritt zur Seite.
**Aufwand:** mittel. **Risiko:** niedrig–mittel.
**Dateien:** `src/Trendo.App/MainWindow.Wissen.cs`, `MainWindow.WissenDocs.cs`, `MainWindow.WissenNetz.cs`, `MainWindow.WissenWoche.cs`, `src/Trendo.Core/W2/Knowledge.cs`, `W2/KnowledgeWiki.cs`, `W2/IntelligenceCore.cs:81`.

## SYS-08 Sicher recherchieren (Programm „Recherche im Lesemodus“)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE | Lese-Browser: Adresse, Startpunkte/Sitzungsquellen, Lesebereich, Markierungen/Zitate/Notizen (MainWindow.Research.cs:71–161); nur Text (bis 20 000 Zeichen angezeigt, :123), keine Bilder/Tabellen |
| 2 | Werkzeuge | TEILWEISE | Lesen, Artikel/ganze Seite (:54–58), Markieren mit Farbe/Notiz, Zitat kopieren, Literaturliste einfach/APA (Test T-0309), Übersetzen mit lokaler KI (:116–117) |
| 3 | Neu | TEILWEISE | Jede App-Sitzung beginnt leer (Research.cs:26) |
| 4 | Import | TEILWEISE | Echter Netzabruf nur lesend (`ReadOnlyWeb.GetPageAsync`, Research.cs:29–33), max. 2 MB (W3/Research.cs:18); keine PDFs, keine Dateien |
| 5 | Vorhandenes bearbeiten | FEHLT | Sitzung ist flüchtig: `EndSession` beim Start löscht Quellen, Notizen, Markierungen (Research.cs:26, W3/Research.cs:113); keine gespeicherten Recherchen |
| 6 | Rückgängig | FEHLT | nicht gefunden |
| 7 | Vorschau | VOLLWERTIG (Lesetext) | Lesebereich mit Markierungen (Research.cs:118–127) |
| 8 | Speichern | TEILWEISE | Nur „Als Bericht speichern“ (Research.cs:150–151); Sitzungsdaten in `tmp/recherche-sitzung` (W3/Research.cs:30) |
| 9 | Wiederherstellung | FEHLT (bewusst) | Neustart löscht die Sitzung (Research.cs:26) |
| 10 | Export | TEILWEISE | Bericht mit Zitaten und Literaturliste (W3/Research.cs:115); Format/Stile über Test T-0309 belegt, weitere Formate nicht gefunden |
| 11 | Rundreise | FEHLT | Bericht lässt sich nicht wieder als Recherche öffnen; Weitergabe nur in derselben Sitzung an STUDIO-CITE-01 (KS4:903–919) |
| 12 | Fachfunktion real | TEILWEISE | Lesemodus mit Artikel-Erkennung (W3/ReaderArticle.cs, 5 Tests „Lesemodus: …“), Sicherheitsregeln: .onion nur Tor Browser, Heimnetz und andere Schemata abgelehnt (W3/Research.cs:32–63; Tests „SYS-08: .onion …“, „Sicherheit B1“). **Keine Suche**: kein Suchfeld, keine Suchmaschine – nur Adresse und feste Startpunkte (Research.cs:74–76, :88–93; Grep nach Suchdiensten in W3/Research.cs ohne Treffer). Herkunft: jede Seite mit URL, Uhrzeit, SHA-256 (W3/Research.cs:12, :100) |
| 13 | Claude/KI | TEILWEISE | Lokale KI übersetzt die ersten 4000 Zeichen (Research.cs:117); Adapter „Seite nur lesen“ (ConnectedAdapters.cs:111, CapabilityCatalog.cs:74). Kein „KI fasst mehrere Quellen mit Belegen zusammen“, kein Faktencheck (laut eigener Liste offen, docs/claude/vorbilder/recherche-zotero.md:15) |
| 14 | Tests | TEILWEISE / echter Netzabruf NICHT GETESTET | UI-Tests ersetzen den Abruf (`ReaderFetchOverride` in ResearchMarksUiTests.cs, Runde11UiTests.cs, W3UiTests.cs); Kern-Tests für Lesemodus und URL-Regeln; kein Beleg unter `reports/` gefunden (Suche nach recherch/sys-08/reader ohne Treffer) |
| 15 | Bedienung | TEILWEISE | Enter in der Adresszeile (Research.cs:77); kein Zurück/Vor, keine Reiter, keine Lesezeichen, kein Rechtsklick, kein Vollbild gefunden |

**Gesamturteil: TEILWEISE** – sicherer Lesemodus mit guter Herkunftsspur, aber kein Recherche-Programm: ohne Suche, ohne bleibende Sammlung. Der freigegebene Ausbau zum vollen Recherche-Studio ist im Code nicht zu erkennen.

Doku≠Code: Vorbild-Liste führt „✗ Sammlung mit Quellen, Notizen und Markierungen“ und „✗ Zitierstile“ (recherche-zotero.md:13–14) – Markierungen, Notizen und APA-Liste gibt es inzwischen (ResearchMarks, Test T-0309), allerdings nur flüchtig.

**Was fehlt:** 1. Suche über eine datensparsame Suchquelle mit Trefferliste und Herkunft. 2. Bleibende Recherche-Projekte (Sammlung, Schlagworte) – wahlweise, mit sichtbarem Löschen. 3. Mehrere Quellen von der KI zusammenfassen lassen, jede Aussage mit Quelle. 4. Zurück/Vor, Reiter, Bilder/Tabellen im Lesetext, PDF lesen. 5. Faktencheck-Ansicht (belegt/umstritten). 6. Echter Abruf-Nachweis mit Rohlog.

**Abnahmekriterien:** Suchbegriff eingeben liefert Treffer mit Quelle, Klick öffnet den Lesemodus; eine gespeicherte Recherche ist nach Neustart mit Markierungen wieder da und lässt sich vollständig löschen; „Zusammenfassen“ über drei gelesene Seiten liefert Text, in dem jede Aussage auf eine Sitzungsquelle zeigt; .onion, Heimnetz, Formulare und Downloads bleiben nachweislich gesperrt; ein echter Abruf ist unter `reports/` belegt.
**Aufwand:** mittel–groß. **Risiko:** mittel (Suchdienst-Bedingungen, Datenschutz, Sicherheitsregeln dürfen nicht aufweichen).
**Dateien:** `src/Trendo.App/MainWindow.Research.cs`, `MainWindow.ResearchMarks.cs`, `src/Trendo.Core/W3/Research.cs`, `W3/ReaderArticle.cs`, `W3/ResearchMarks.cs`, `PromptAction/ConnectedAdapters.cs:111`.

## SPEC-07 Texte übersetzen
Abgelehnt und entfernt – nicht als fehlend gewertet. Beleg: `CLAUDE.md` („SPEC-07 abgelehnt – nicht bauen“), `src/Trendo.App/StudioCatalog.cs:145` (`Removed`), CapabilityCatalog.cs:72 („kein neuer Übersetzungsadapter“). Hinweis: Der Katalogeintrag StudioCatalog.cs:76 steht noch mit Kacheltext drin; SYS-08 bietet „Auf Deutsch (KI auf diesem PC)“ als Übersetzung des Lesetexts (Research.cs:116–117) – das ist eine Funktion innerhalb der Recherche, kein Studio.

## Auffälligkeiten der Gruppe (Teil A2)

1. **„Mit Claude ändern“ ändert die Datei nicht.** Claude läuft im eigenen Chat-Ordner, nicht im geöffneten Projekt (ClaudeChat.cs:54–58, :761–766), bekommt einen Ausschnitt (KS4:241) und soll die Änderung nur zeigen (KS4:259). Der vorab erzeugte Schnappschuss (KS4:258) vergleicht deshalb im Normalfall nichts. Studio-Satz „Dateien mit Claude erklären und ändern“ (StudioCatalog.cs:86) ist damit nur halb wahr.
2. **Studioname „Bilder erkennen“ ≠ Leistung.** Lokal nur Messwerte; keine Inhaltserkennung (KK4:277–545, KS4:779). OCR-Erfolgsweg in Tests ersetzt (`VisionTesseractOverride = () => null`).
3. **Claude-Ergebnisse bleiben Chat-Text** in allen drei T-0615-Studios (KS4:268, :701, :930) – keine Übernahme ins Dokument, den Bericht oder die Datei.
4. **Externe Werkzeuge in Tests ersetzt:** `CodeAiRunOverride` (KreativStudios4UiTests.cs:55), `VisionTesseractOverride` (:129), `ReaderFetchOverride` (SYS-08), Claude nie echt; der Bericht T-0615 räumt das selbst ein (Zeilen 35–38).
5. **Katalogstatus:** `docs/programme/katalog.json` führt code-ki-, bilder-erkennen- und quellen-programm als „IN_PROGRESS / Test NOT_RUN“ mit leerer Testliste, obwohl UI- und Kern-Tests existieren (KreativStudios4UiTests.cs, KreativKits4Tests.cs) – Katalog veraltet. Umgekehrt stehen ki-wissen/recherche/lernen als „IMPLEMENTED / PC_VERIFIED“; passende Rohlogs unter `reports/` für diese Programme habe ich nicht gefunden (nur Namenssuche, nicht vollständig geprüft).
6. **Katalog-Engines falsch zugeordnet:** quellen-, bilder-erkennen- und code-ki-programm listen alle dieselbe Engine-Sammlung (CiteKit, CodeAiKit, DayPlanner, VisionKit …) – offenbar je Quelldatei statt je Programm erzeugt.
7. **Vorbild-Listen veraltet** (wissen-obsidian.md, messdaten-origin-scidavis.md, recherche-zotero.md): führen Gebautes als „✗ fehlt“.
8. **Pfad-Eintippfelder statt Dateidialog** in SPEC-05 (Spec05.cs:143) und CORE-10 (Wissen.cs:87) – widerspricht „Dateiauswahl“ der Einfach-Vorgaben.
9. **Zwei Programmarten nebeneinander:** SPEC-06, CORE-10, SYS-08 sind `RegisterTab`/eigene Seiten ohne Programm-Hülle – dadurch kein Rückgängig, kein Autosave/Versionen, keine Bedienstufen, anders als die Hüllen-Programme.
10. **SYS-08 ohne Suche und ohne Bestand** – der Name „recherchieren“ und der freigegebene Recherche-Ausbau sind im Code nicht eingelöst; STUDIO-CITE-01 kann Recherche-Quellen nur innerhalb derselben App-Sitzung übernehmen (Research.cs:26).

Nicht geprüft: tatsächliches Verhalten zur Laufzeit (nichts gestartet), AvaloniaEdit-Rückgängig, `Ocr`-Klasse und deren eigene Tests, Inhalte unter `reports/` über die Namenssuche hinaus, ob Claude-Chats Wissen aus CORE-10 erhalten, MQTT/„zwilling“ (SPEC-10).

---

# Teil B: Audit Gruppe KI & Automatik – Teil B (CORE-02, CORE-03, CORE-12, X-05)

Stand: Arbeitskopie `studios-aufraeumen`, nur gelesen. Pfade relativ zum Repo. „≈“ = Zeile auf wenige Zeilen genau.
Nicht geprüft: echter Lauf der App, echte Modelle, echtes Claude-Abo; `MainWindow.ClaudeChat.cs` (Studio-Chat), `ClaudeBridge.cs`, `KiMemory.cs`, `KiPy.cs`, `KiStore.InstallModelFile` wurden nicht im Detail gelesen.

## Übersicht

| Studio-Id | Name | Programme | Gesamturteil | größte Lücke | Aufwand |
|---|---|---|---|---|---|
| CORE-02 | KI fragen | ki-fragen, ki, claude-verbinden, keys, verbindungen | TEILWEISE | Chat ohne Dateianhang, ohne sichtbares Mitschreiben der Antwort, ohne Neu-Erzeugen/Bearbeiten; zweite, schwächere Chat-Seite „ki“; kein echter Ende-zu-Ende-Test | mittel |
| CORE-03 | KI-Team einsetzen (in CORE-02) | ki-rollen | TEILWEISE | „Team“ ist eine feste Textkette (Rolle → Rolle) ohne Werkzeuge, ohne Abbruchknopf, ohne Parallelität; Ergebnis nur Text | groß |
| CORE-12 | KI-Anweisungen schreiben (in CORE-02) | ki-vorlagen, ki-skills | TEILWEISE | Formular statt Editor; Vorlagen ohne Import/Export; Skill-Wirkung in Claude Code nie echt geprüft | mittel |
| X-05 | Kostenlose KI finden (in CORE-02) | ki-bibliothek, ki-quellen, ki-internet | TEILWEISE (am weitesten) | Modell-Dateien ohne feste Prüfsumme; Ollama-Laden ohne Fortschritt; Download/Start in Tests vollständig ersetzt | mittel |

## Was antwortet wirklich? (Kernfrage 1)

- **Ein Eingang:** `IntelligenceCore.AskAsync` geht die Quellen der Reihe nach durch, nimmt die erste erreichbare, sonst Fehlermeldung „Gerade ist keine KI auf diesem PC erreichbar …“ – **kein Regelwerk, kein Platzhaltertext** (`src/Trendo.Core/W2/IntelligenceCore.cs:102-127`).
- **Lokal – Ollama:** echte HTTP-Aufrufe an `127.0.0.1:11434` (`/api/version`, `/api/tags`, `/api/chat` mit Stream, `/api/pull`, `/api/delete`) (`src/Trendo.App/KiBackends.cs:19-78`); Nicht-Loopback wird abgelehnt (`KiBackends.cs:10-16`). Dienst wird bei Bedarf mit `ollama serve` gestartet (`src/Trendo.App/MainWindow.axaml.cs:≈955-968`).
- **Lokal – LM Studio/llama.cpp/Jan/KoboldCpp:** OpenAI-Format unter `/v1`, nur Loopback (`KiBackends.cs:112-148`).
- **Claude über Claude Code:** echter Prozessstart des installierten `claude` mit `-p … --output-format stream-json --verbose`, im Frage-Modus `--restricted --strict-mcp-config --disallowedTools "Edit Write NotebookEdit MultiEdit"` (`src/Trendo.Core/W2/ClaudeCode.cs:93-108`; Prozess `src/Trendo.App/MainWindow.Claude.cs:107-118`, Auswertung `Claude.cs:344-377`). API-Variablen und Token-Variable werden je Lauf entfernt, eigene `CLAUDE_CONFIG_DIR` je Person (`Claude.cs:≈63-70`, `ClaudeCode.cs:40-41,59-64`). Alter gespeicherter Token wird gelöscht (`Claude.cs:≈72-79`). Abbruch, 20-Minuten-Wächter, Mitschrift `.trendo/letzter-lauf.jsonl` vorhanden (`Claude.cs:≈121-160`). Die Schalter `--restricted`/`--strict-mcp-config` gibt es im installierten Claude Code 2.1.319 wirklich (per `claude --help` nachgesehen).
- **Internet-Dienste mit Schlüssel:** OpenRouter (nur `:free`), Gemini, Mistral, Hugging Face, Anthropic-API, OpenAI – echte HTTPS-Aufrufe `…/models` und `…/chat/completions`, nur feste Hosts, keine Weiterleitung (`KiBackends.cs:153-245`; Liste `src/Trendo.Core/W2/KiSources.cs:44-52`). Schlüssel im Windows-Anmeldespeicher (`MainWindow.axaml.cs:≈1239,1281`). Internet-KI bekommt weder Verlauf noch eigene Dokumente (`IntelligenceCore.cs:99,116-117`).
- **kie.ai:** kein Chat (bewusst, `src/Trendo.Core/Kie/Kie.cs:183-184`); Aufträge für Bild/Video/Musik/Sprache über `POST /api/v1/jobs/createTask`, `GET /api/v1/jobs/recordInfo`, Guthaben `GET /api/v1/chat/credit` (`Kie.cs:71,79,85`), Schlüssel in `Secrets` (`src/Trendo.App/MainWindow.Kie.cs:58`). In dieser Gruppe nur als Knopf in der Programm-Leiste; Details gehören zu den Medien-Studios.
- **Tests:** alle KI-Wege sind in den UI-Tests ersetzt (`KiBackendsOverride` `MainWindow.W2.cs:15`, `ClaudeRunOverride`/`ClaudeStatusOverride` `Claude.cs:45-46`, `KiChatOverride` `axaml.cs:983`, `CloudBackendFactory` `W2.cs:46`); im Testmodus startet Claude nie (`Claude.cs:≈111`). Echte Läufe sind nur als Bericht belegt: Ollama/qwen2.5-coder:7b (`reports/alle-studios/ki-gedaechtnis-e2e.md:22`, `reports/alle-studios/runde11-artikel-anweisungstest-e2e.md:44`), Modell laden (`reports/p1/ki/README.md:5`). Für Claude als Standard-KI steht ausdrücklich „Nicht geprüft … mit einem echten Claude-Abo“ (`reports/volltest-0.9.84/README.md`, Zeile „Nicht geprüft“).

## CORE-02 KI fragen

### Programm „ki-fragen“ (Chatfenster)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE – echtes Chatfenster (Gesprächsliste links, Sprechblasen, Code/Tabellen getrennt), aber Seite wird nach jedem Schritt neu gebaut; Antwort erscheint erst am Ende, während des Wartens nur „Trendo denkt nach …“ | `src/Trendo.App/MainWindow.ChatWindow.cs:65-167,122-126` |
| 2 | Werkzeuge | TEILWEISE – Senden, Stopp, Suchen, Umbenennen, Löschen, Kopieren, „Als Dokument“, Gut/Verbessern funktionieren; es fehlen Datei anhängen, Antwort neu erzeugen, Frage bearbeiten, Modellwahl im Chat | `ChatWindow.cs:70-95,112-118,145-149,212-214` |
| 3 | Neues Projekt | VOLLWERTIG – „Neues Gespräch“ | `ChatWindow.cs:70,117-118` |
| 4 | Dateien importieren | FEHLT – kein Anhang im Chat (Datei-Fragen nur auf der Seite „ki-internet“) | `ChatWindow.cs:111-118` (nur Textfeld); `MainWindow.OpenRouter.cs:≈214-225` |
| 5 | Vorhandenes bearbeiten | VOLLWERTIG – Gespräch öffnen und fortsetzen, Verlauf geht als Kontext mit (letzte 10) | `ChatWindow.cs:58-63`; `IntelligenceCore.cs:94` |
| 6 | Rückgängig | FEHLT – kein Undo für Nachrichten; Löschen über Bestätigungsknopf | `ChatWindow.cs:93` |
| 7 | Vorschau | entfällt (Antwort ist das Ergebnis); Code und Tabellen werden dargestellt | `ChatWindow.cs:178-205` |
| 8 | Speichern | VOLLWERTIG – jedes Gespräch als Datei, atomar (tmp + Move), nach jeder Antwort | `src/Trendo.Core/W2/ChatStore.cs:38-45`; `ChatWindow.cs:49` |
| 9 | Wiederherstellung | TEILWEISE – gespeicherte Gespräche bleiben; laufende Frage und ungesendeter Text gehen verloren | `ChatWindow.cs:39-54` |
| 10 | Export | TEILWEISE – nur Markdown (Gespräch oder einzelne Antwort) | `ChatWindow.cs:95,214`; `ChatStore.cs:71` |
| 11 | Rundreise | FEHLT – kein Import eines exportierten Gesprächs | kein Import in `ChatStore.cs` |
| 12 | Fachfunktion real | VOLLWERTIG – echte Modellaufrufe, keine Scheinantwort | siehe Abschnitt oben |
| 13 | Claude/KI direkt | TEILWEISE – Claude antwortet als Haupt-KI im Frage-Modus (ändert nichts); Wissen wird nur der lokalen KI mitgegeben | `MainWindow.W2.cs:41`; `Claude.cs:344-377` |
| 14 | Ende-zu-Ende-Tests | NICHT GETESTET – UI-Tests mit ersetzter KI; echter Lauf nur als Bericht mit Ollama | `tests/Trendo.App.Tests/ChatWindowUiTests.cs:15`; `W2UiTests.cs:26,62` |
| 15 | Bedienung | TEILWEISE – Enter sendet, Umschalt+Enter neue Zeile; kein Rechtsklick-Menü, kein Drag-and-drop; Einfach/Profi und Vollbild nicht geprüft | `ChatWindow.cs:111-114` |

### Programm „ki“ (Trendo KI lokal)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1–2 | Arbeitsfläche/Werkzeuge | TEILWEISE – ein Textfeld, ein Antwortfeld, kein Verlauf: jede Frage ersetzt die vorige | `src/Trendo.App/MainWindow.axaml.cs:911-933,993-999` |
| 3–11 | Projekt, Import, Bearbeiten, Undo, Speichern, Export | FEHLT – nichts wird gespeichert (nur Audit-Zeile mit Länge) | `axaml.cs:993-999` |
| 12 | Fachfunktion real | TEILWEISE – echter Ollama-Aufruf, aber fester Systemtext, ohne Verlauf, ohne Wissen, nur Ollama (keine Rückfallkette) | `src/Trendo.App/OllamaClient.cs:62-81` |
| 13 | Claude/KI direkt | FEHLT – Begrüßung verspricht „eine Aufgabe geben, ein Studio öffnen, ein Projekt starten“, der Code schickt nur Text an Ollama | `axaml.cs:≈979` gegen `axaml.cs:993-999` |
| 14 | Tests | NICHT GETESTET (ersetzt durch `KiChatOverride`) | `axaml.cs:983` |
| 15 | Bedienung | TEILWEISE – Senden-Knopf; mehr nicht geprüft | `axaml.cs:≈919-921` |

### Programm „claude-verbinden“

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1–2 | Fläche/Werkzeuge | TEILWEISE – eingebettete claude.ai-Anmeldeseite in eigenem Browser-Profil je Person; Trennen meldet ab | `src/Trendo.App/MainWindow.Claude.cs:≈326-342`; `src/Trendo.Core/W2/ClaudeWeb.cs:8-17` |
| 3–11 | Projekt/Import/Undo/Export | entfällt (Anmeldeseite) | – |
| 8 | Speichern | TEILWEISE – nur „verbunden seit“ in `trendo.json`; die Sitzung selbst liegt im Browser-Profil (nicht geprüft, was dort liegt) | `ClaudeWeb.cs:21-31` |
| 12 | Fachfunktion real | TEILWEISE – zwei getrennte Wege: (a) Claude Code CLI mit `claude auth login --claudeai` im eigenen Fenster (`Claude.cs:≈177-197`), (b) claude.ai im eingebetteten Fenster. Im Testmodus wird nichts geladen | `Claude.cs:≈335` |
| 13 | Claude direkt | TEILWEISE – Weg (a) macht Claude zur Haupt-KI; Weg (b) zeigt nur die Webseite | `ClaudeWeb.cs:8-11` |
| 14 | Tests | NICHT GETESTET – Anmeldung und Status ersetzt (`ClaudeStatusOverride`), echtes Abo nie geprüft | `Claude.cs:45`; `reports/volltest-0.9.84/README.md` |
| 15 | Bedienung | nicht geprüft | – |

### Programm „keys“ (Online-Dienste)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1–2 | Fläche/Werkzeuge | VOLLWERTIG als Einstellseite – je Dienst „Schlüssel holen“, „Prüfen & speichern“, Schalter „Als Ersatz-KI nutzen“ | `src/Trendo.App/MainWindow.axaml.cs:1225-1290` |
| 3–7, 9–11 | – | entfällt | – |
| 8 | Speichern | VOLLWERTIG – nur nach Live-Prüfung, im Windows-Anmeldespeicher, Schlüssel nie im Protokoll | `axaml.cs:≈1239-1241,1280-1283` |
| 12 | Fachfunktion real | VOLLWERTIG – echter Prüfaufruf (Modelliste des Dienstes) | `KiBackends.cs:208`; `axaml.cs:≈1293-1300` |
| 13 | Claude/KI | entfällt; Seite ist für Claude als sensibel gesperrt | `src/Trendo.App/MainWindow.ClaudeBridge.cs:249` |
| 14 | Tests | NICHT GETESTET – Prüfung ersetzt (`KeyValidatorOverride`); „Schlüssel-Live-Prüfung mit echtem Schlüssel“ ausdrücklich nicht ausgeführt | `axaml.cs:≈1238`; `reports/p1/deps/README.md:8` |
| 15 | Bedienung | TEILWEISE – kein Entfernen-Knopf für gespeicherte Schlüssel auf dieser Seite gesehen | `axaml.cs:1225-1290` |

### Programm „verbindungen“

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1–2 | Fläche/Werkzeuge | TEILWEISE – Listen und Knöpfe: GitHub anmelden/abmelden (gh CLI), Netlify-Schlüssel, Claude-Plugins, Marktplätze, MCP-Server hinzufügen; jede Änderung mit Ja/Abbrechen | `src/Trendo.App/MainWindow.Connections.cs:133-315` (Knöpfe ≈139-204) |
| 3–7, 9–11 | – | entfällt | – |
| 8 | Speichern | VOLLWERTIG – nichts in Trendo; Anmeldungen liegen bei gh bzw. Claude Code je Person | `src/Trendo.Core/W2/Connections.cs:7-11,59-67` |
| 12 | Fachfunktion real | TEILWEISE – ruft die offiziellen Befehle `claude …` und `gh …` wirklich auf; geheime Werte werden nicht gelesen | `Connections.cs (App):103-118`; `Connections.cs (Core):59-60,104` |
| 13 | Claude direkt | VOLLWERTIG (Verwaltung) – Plugins/MCP landen im Claude-Ordner der Person | `Connections.cs (App):100-101,108-109` |
| 14 | Tests | NICHT GETESTET – `ConnRunOverride`, `GhExeOverride`, `NetlifyHandlerOverride`, `ClaudeHomeOverride` | `Connections.cs (App):15-21` |
| 15 | Bedienung | nicht geprüft | – |

**Gesamturteil CORE-02: TEILWEISE.** Die KI-Anbindung ist echt und sauber abgesichert, aber das Chatprogramm selbst liegt klar unter einer Claude-/ChatGPT-App, und kein Weg ist mit echter KI automatisch getestet.

**Was fehlt (priorisiert):**
1. Dateien an die Frage hängen (Bild, PDF, Text) – lokal über Bildmodell bzw. Claude, mit sichtbarem Hinweis, wohin die Datei geht.
2. Antwort beim Entstehen im Chat anzeigen (Stream liegt vor: `IntelligenceCore.cs:115-117`, wird nur in die Fortschrittsanzeige geleitet).
3. Antwort neu erzeugen, Frage bearbeiten, Modell je Gespräch wählen.
4. Seite „ki“ entfernen oder auf „ki-fragen“ umleiten; ihr Begrüßungstext verspricht Aktionen, die es nicht gibt.
5. Echter Ende-zu-Ende-Test je Quelle (Ollama, Claude Code, ein Internet-Dienst) als überspringbarer „echt“-Test mit Beleg unter `reports/`.
6. Entscheidung zur eingebetteten claude.ai-Anmeldung (siehe Auffälligkeiten 1).

**Abnahmekriterien:**
- Mit laufendem Ollama beantwortet „KI fragen“ drei Folgefragen mit Bezug auf die vorherige Antwort; die Antwort wächst sichtbar im Chat; Stopp beendet sie in unter 2 s.
- Ein angehängtes Bild wird von der lokalen Bild-KI beschrieben; ohne passendes Modell kommt eine klare Meldung statt einer erfundenen Antwort.
- Mit angemeldetem Claude Code antwortet Claude; `.trendo/letzter-lauf.jsonl` und das Audit-Log zeigen den Lauf; im Datenordner liegt kein Token.
- Nach hartem Beenden während einer Antwort sind alle früheren Gespräche vollständig da.
- Die Seite „ki“ existiert nicht mehr als zweiter Chat.

**Aufwand/Risiko:** mittel. Risiko: Nutzungsregeln von Anthropic (Abo in Fremdprogramm), Kosten bei Internet-Diensten, kleine lokale Modelle liefern schwache Antworten.

**Dateien:** `src/Trendo.Core/W2/IntelligenceCore.cs`, `ClaudeCode.cs`, `ClaudeWeb.cs`, `ChatStore.cs`, `KiSources.cs`; `src/Trendo.App/KiBackends.cs`, `OllamaClient.cs`, `MainWindow.W2.cs`, `MainWindow.ChatWindow.cs`, `MainWindow.Claude.cs`, `MainWindow.Connections.cs`, `MainWindow.axaml.cs` (KiView 911, KeysView 1225).

## CORE-03 KI-Team einsetzen (eingegliedert in CORE-02)

### Programm „ki-rollen“

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE – Formular: Studio-Auswahl, Rollenkarten, Zielfeld, Startknöpfe, Aufgabenliste; kein Ablaufbild, keine Live-Ansicht | `src/Trendo.App/MainWindow.Agents.cs:48-153` |
| 2 | Werkzeuge | TEILWEISE – Rolle starten, Team starten, eigene Rolle anlegen/entfernen, im Projekt speichern (nach Freigabe), an Claude Code übergeben, Team-Lauf fortsetzen | `Agents.cs (App):92,99,118,124,131,141` |
| 3 | Neues Projekt | TEILWEISE – neue Aufgabe/eigene Rolle; kein eigenes Team zusammenstellen (Teams sind fest: 3 allgemeine + je Studio eines) | `src/Trendo.Core/W2/Agents.cs:50-55,88-92` |
| 4 | Dateien importieren | FEHLT – Eingabe ist nur das Zielfeld | `Agents.cs (App):≈90` |
| 5 | Vorhandenes bearbeiten | TEILWEISE – Ergebnis nicht bearbeitbar; nur fortsetzen oder entfernen | `Agents.cs (App):≈133-151` |
| 6 | Rückgängig | FEHLT | – |
| 7 | Vorschau | TEILWEISE – Ergebnistext, auf 600 Zeichen gekürzt | `Agents.cs (App):≈155` |
| 8 | Speichern | TEILWEISE – Aufgaben in `ki/agenten.json`, nicht atomar geschrieben | `Agents.cs (Core):63,245` |
| 9 | Wiederherstellung | VOLLWERTIG – laufende Aufgaben werden beim Start als „Unterbrochen“ markiert; Team-Lauf ab erster offener Rolle fortsetzbar | `Agents.cs (Core):66,187-213` |
| 10 | Export | TEILWEISE – Markdown im Projekt (nur schreibberechtigte Rollen, nur nach Freigabe); Auftragsdatei für Claude Code | `Agents.cs (Core):216-242` |
| 11 | Rundreise | FEHLT | – |
| 12 | Fachfunktion real | TEILWEISE – **echte, aber rein textliche Kette:** je Rolle ein Modellaufruf mit Rollen-Systemtext, Ergebnis der Vorrolle als Eingabe, streng nacheinander, Abbruch beim ersten Fehler. Keine Werkzeuge, keine Dateizugriffe, keine Parallelität, kein Zusammenführen außer durch die letzte Rolle. Antwortform wird bei einer Rolle (Planer) als JSON geprüft, einmal nachgefragt | `Agents.cs (Core):142-181,35-36` |
| 13 | Claude/KI direkt | TEILWEISE – läuft über `Intel` (also Claude, wenn Haupt-KI, sonst lokal); „An Claude Code übergeben“ legt nur eine Datei ab, Status „nicht automatisch ausgeführt“ | `Agents.cs (App):41-42`; `Agents.cs (Core):234-242` |
| 14 | Tests | NICHT GETESTET – UI-Test mit ersetzter KI | `tests/Trendo.App.Tests/W2UiTests.cs:159` |
| 15 | Bedienung | TEILWEISE – kein Stopp-Knopf (Aufruf ohne Abbruch-Token), kein Fortschritt je Rolle während des Laufs | `Agents.cs (App):36-46` |

**Gesamturteil: TEILWEISE.** Es laufen wirklich mehrere Modellaufrufe mit Rollen, aber es ist eine Prompt-Kette, kein Agenten-Team: keine Werkzeuge, kein Zeitplan, kein Auslöser, Ergebnis nur Text.

**Was fehlt (priorisiert):**
1. Rollen brauchen Werkzeuge (Dateien im Projekt lesen/schreiben, Studio-Aktionen) – sonst bleibt es Text.
2. Stopp-Knopf und Fortschritt je Rolle (Abbruch-Token durchreichen, `Agents.cs (App):41-42`).
3. Eigene Teams zusammenstellen (Reihenfolge wählen), Ablauf als Bild.
4. Ergebnis je Rolle ansehen, bearbeiten, Rolle einzeln neu laufen lassen.
5. Prüfer-Rolle soll das Ergebnis wirklich zurückweisen können (Schleife statt Einbahnkette).
6. Echter Test mit lokaler KI.

**Abnahmekriterien:**
- Team „Text schreiben & prüfen“ erzeugt mit echter lokaler KI drei Aufgaben; die dritte enthält nachweislich den Text der zweiten.
- Stopp beendet den Lauf, „Team-Lauf fortsetzen“ macht bei der offenen Rolle weiter.
- Ein selbst gebautes Team mit eigener Reihenfolge läuft und ist nach Neustart noch da.
- Eine Rolle mit Schreibrecht legt nach Freigabe eine Datei im Projekt an, die sich im zugehörigen Programm öffnen lässt.

**Aufwand/Risiko:** groß (Werkzeuge für Rollen) · Risiko: kein zweites Agenten-Laufzeitsystem bauen (Projektregel), kleine Modelle halten Rollen schlecht.

**Dateien:** `src/Trendo.Core/W2/Agents.cs`, `src/Trendo.App/MainWindow.Agents.cs`.

## CORE-12 KI-Anweisungen schreiben (eingegliedert in CORE-02)

### Programm „ki-vorlagen“

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE – Karten + Formular (Name, Bereich, Text); kein Editor mit Hervorhebung der Lücken | `src/Trendo.App/MainWindow.Prompts.cs:26-64,113-146` |
| 2 | Werkzeuge | VOLLWERTIG im Rahmen – Lücken ausfüllen, senden, kopieren, bearbeiten, Hinweise live, Masterprompt bauen, Anweisungs-Test | `Prompts.cs:86-111,≈77-83,≈148-153`; `MainWindow.PromptTest.cs:16-56` |
| 3 | Neues Projekt | VOLLWERTIG – „+ Neue Vorlage“ | `Prompts.cs:≈51-52` |
| 4 | Dateien importieren | FEHLT | kein Import in `src/Trendo.Core/W2/Prompts.cs` |
| 5 | Vorhandenes bearbeiten | VOLLWERTIG – mit Versionen | `Prompts.cs (Core):49-64` |
| 6 | Rückgängig | TEILWEISE – frühere Version zurückholen (letzte 5), kein Undo im Feld | `Prompts.cs (App):≈134-143` |
| 7 | Vorschau | VOLLWERTIG – Antwort der KI auf die ausgefüllte Vorlage | `Prompts.cs (App):≈100-105,61-62` |
| 8 | Speichern | TEILWEISE – direktes Überschreiben der JSON-Datei, nicht atomar | `Prompts.cs (Core):125` |
| 9 | Wiederherstellung | TEILWEISE – ungespeicherte Eingaben weg; kaputte Datei → leere Liste ohne Meldung | `Prompts.cs (Core):41` |
| 10–11 | Export/Rundreise | FEHLT – nur „Text kopieren“ | `Prompts.cs (App):≈106-107` |
| 12 | Fachfunktion real | VOLLWERTIG – **Anweisungs-Test misst wirklich:** Regeln (enthält/nicht/Muster/höchstens-mindestens Wörter/ist JSON), bis 5 Durchläufe je Fassung gegen dieselbe KI, aktuelle gegen vorherige Fassung, Zählung bestandener Läufe. Rein formale Regeln, keine inhaltliche Bewertung | `src/Trendo.Core/W2/PromptEval.cs:21-84`; `PromptTest.cs:24-46` |
| 13 | Claude/KI | TEILWEISE – nutzt `Intel` ohne Wissen | `Prompts.cs (App):≈102`; `PromptTest.cs:≈41` |
| 14 | Tests | TEILWEISE – UI mit ersetzter KI; ein echter Lauf mit Ollama als Bericht | `W2UiTests.cs:132`; `reports/alle-studios/runde11-artikel-anweisungstest-e2e.md:44` |
| 15 | Bedienung | nicht geprüft (kein Kürzel/Rechtsklick im gelesenen Code) | – |

### Programm „ki-skills“

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE – Liste + Formular (Name, Wofür, Studios, Fachregeln, Checkliste) | `src/Trendo.App/MainWindow.Skills.cs:35-104` |
| 2 | Werkzeuge | TEILWEISE – anlegen, bearbeiten, löschen, ansehen, prüfen (Lint), als Datei sichern, aus Datei laden, aus GitHub-Sammlung übernehmen (nur Text, mit Lizenz/Herkunft) | `Skills.cs (App):≈41-42,≈58-66,≈69,106-146`; `src/Trendo.Core/W2/Skills.cs:155-218` |
| 3 | Neues | VOLLWERTIG | `Skills.cs (App):≈41` |
| 4 | Import | TEILWEISE – `.md`/`.txt` bis 100 KB, **Pfad muss ins Textfeld getippt werden** (kein Dateidialog); kein ZIP-Import in `SkillLibrary` (Katalog nennt `.zip`) | `Skills.cs (App):≈40-42`; `Skills.cs (Core):212-218` |
| 5 | Bearbeiten | TEILWEISE – nur eigene; geladene nicht änderbar | `Skills.cs (App):≈60-64` |
| 6 | Rückgängig | TEILWEISE – Versionen über PromptLibrary, in der Skill-Seite kein Zurückholen-Knopf gesehen | `Skills.cs (Core):154-162` |
| 7 | Vorschau | FEHLT – kein „an einem Beispiel ausprobieren“ (auch in der alten Liste als ✗) | `docs/claude/vorbilder/ki-team-crewai-gpts.md:23` |
| 8 | Speichern | TEILWEISE – eigene über PromptLibrary (nicht atomar), geladene atomar | `Prompts.cs (Core):125`; `Skills.cs (Core):175` |
| 9 | Wiederherstellung | nicht geprüft | – |
| 10 | Export | VOLLWERTIG – Markdown mit Kopf; für Claude Code als `.claude/skills/trendo-<id>/SKILL.md` | `Skills.cs (Core):70-72,202-209,221-230` |
| 11 | Rundreise | TEILWEISE – Export-Format ist das Import-Format; als Test nicht gesucht/geprüft | `Skills.cs (Core):58-68,167-178` |
| 12 | Fachfunktion real | TEILWEISE – echte Dateien; Fachregeln gehen als Text an die KI (gekürzt 7000 bzw. 2400 Zeichen). Ob Claude Code die abgelegten Skills wirklich lädt, ist nirgends echt belegt | `Skills.cs (Core):81,106,120-134`; `Claude.cs:≈28` |
| 13 | Claude/KI | TEILWEISE – Export in den Projektordner beim Öffnen von Claude Code | `Claude.cs:≈28`; `Skills.cs (Core):221-230` |
| 14 | Tests | NICHT GETESTET (Wirkung in Claude Code); Datei- und UI-Logik mit ersetzten Aufrufen | `Claude.cs:20,46` |
| 15 | Bedienung | TEILWEISE – Studios als Kennungen eintippen („CORE-07, STUDIO-LETTER-01“) statt auswählen | `Skills.cs (App):≈94` |

**Gesamturteil CORE-12: TEILWEISE.** Vorlagen, Versionen, Anweisungs-Test und Skill-Dateien sind echt; die Oberfläche ist ein Formular, Austausch fehlt bei Vorlagen ganz und die Skill-Wirkung ist ungeprüft.

**Was fehlt (priorisiert):**
1. Vorlagen importieren/exportieren (Datei), Sammlung teilen.
2. Skill an einem Beispiel ausprobieren (mit/ohne Skill nebeneinander).
3. Dateidialog und Studio-Auswahl statt getippter Pfade und Kennungen.
4. Atomares Speichern der Vorlagen-Datei, Meldung bei beschädigter Datei.
5. Echter Nachweis: Claude Code meldet die exportierten Skills im Start-Ereignis (`Init.Skills`, `ClaudeCode.cs:114,144`).
6. Anweisungs-Test: Bewertung durch eine zweite KI oder Vergleichstext, nicht nur formale Regeln.

**Abnahmekriterien:**
- Eine exportierte Vorlagen-Datei lässt sich auf einem leeren Datenordner wieder einlesen und liefert denselben Text und dieselben Lücken.
- Der Anweisungs-Test zeigt mit echter lokaler KI für zwei Fassungen unterschiedliche Trefferzahlen.
- Nach „In Claude Code öffnen“ nennt der Lauf die Trendo-Skills in der Liste verfügbarer Skills (Beleg unter `reports/`).
- Ein Skill wird über den Dateidialog geladen; ein zu großer oder binärer Skill wird mit klarer Meldung abgelehnt.

**Aufwand/Risiko:** mittel · Risiko gering.

**Dateien:** `src/Trendo.Core/W2/Prompts.cs`, `PromptEval.cs`, `Skills.cs`, `src/Trendo.Core/Skills/*.md` (16 mitgelieferte); `src/Trendo.App/MainWindow.Prompts.cs`, `MainWindow.PromptTest.cs`, `MainWindow.Skills.cs`.

## X-05 Kostenlose KI finden (eingegliedert in CORE-02)

### Programm „ki-bibliothek“

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1 | Arbeitsfläche | TEILWEISE – Katalog nach 14 Arten mit Karten, je Modell Laden/Ausprobieren/Entfernen; „Ausprobieren“ ist ein kleines Eingabefeld je Art | `src/Trendo.App/MainWindow.KiLibrary.cs:≈40-125,285-418` |
| 2 | Werkzeuge | VOLLWERTIG im Rahmen – echtes Laden mit Fortschritt und Abbruch, Entpacken (ZIP/7z), Ausprobieren startet die echte Laufzeit (sd-cli, whisper-cli, piper, realesrgan) | `KiLibrary.cs (App):141-203,237-281,293-300,≈308-400,430-440` |
| 3–5 | Neu/Import/Bearbeiten | entfällt (Katalog); eigene Modelle hinzufügen FEHLT (Liste ist fest im Code) | `src/Trendo.Core/W2/KiLibrary.cs:113-224` |
| 6 | Rückgängig | TEILWEISE – Entfernen mit Rückfrage | `KiLibrary.cs (App):≈127` |
| 7 | Vorschau | VOLLWERTIG – Bild, Text, Audio („Anhören“), 3D-Übergabe | `KiLibrary.cs (App):≈401-415` |
| 8–9 | Speichern/Wiederherstellung | TEILWEISE – vollständig geladene Dateien werden an der Größe erkannt und übersprungen; abgebrochene Datei wird gelöscht, kein Fortsetzen mitten in der Datei | `KiLibrary.cs (App):184,231-235` |
| 10–11 | Export/Rundreise | entfällt; Ergebnisse landen im Ordner „KI-Ergebnisse“ | `KiLibrary.cs (App):≈283` |
| 12 | Fachfunktion real | TEILWEISE – echte Downloads von GitHub/Hugging Face über HTTPS, Host-Liste, Größenkontrolle; **Laufzeiten mit fester SHA-256** (`KiLibrary.cs (Core):57-72`), **Modell-Dateien nur mit Größe**, SHA nur wenn Hugging Face sie im Kopf mitliefert (`KiLibrary.cs (App):245`, Übergabe `:187`; Prüfung in `InstallModelFile` nicht gelesen). Python-Laufzeiten (TripoSR, MMAudio, Demucs …) ohne Datei-Prüfsumme, nur feste Version/Commit | `KiLibrary.cs (Core):73-106` |
| 13 | Claude/KI | entfällt | – |
| 14 | Tests | NICHT GETESTET – Download, Start und Dateiauswahl ersetzt (`KiDownloadOverride`, `KiRunOverride`, `KiStepOverride`); einziger echter Lauf-Test ist die Musik-KI und der wird ohne Umgebungsvariable übersprungen | `KiLibrary.cs (App):22-25`; `MainWindow.KiPy.cs:15`; `tests/Trendo.App.Tests/MusikLiveTests.cs:14`; `KiLibraryUiTests.cs:30` |
| 15 | Bedienung | TEILWEISE – Hinweis „passt zu deinem PC“ nur nach Arbeitsspeicher, nicht nach Grafikspeicher | `KiLibrary.cs (App):79`; `docs/claude/vorbilder/ki-bibliothek-ollama-lmstudio.md:14` |

### Programm „ki-quellen“

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1–2 | Fläche/Werkzeuge | TEILWEISE – Liste: Quellen suchen, Reihenfolge ändern, ein-/ausschalten, freie Modelle laden/entfernen, KI-Programme (LM Studio, llama.cpp …) herunterladen, einrichten, Server starten | `src/Trendo.App/MainWindow.X05.cs:99-171`; `MainWindow.KiSourceSetup.cs:35-108` |
| 8 | Speichern | VOLLWERTIG – Einstellungen je Datenordner | `X05.cs:≈118-121` |
| 12 | Fachfunktion real | TEILWEISE – echter Scan und echtes `ollama pull`/`delete`; **Laden ohne Fortschritt** (`stream=false`, ein einziger Aufruf, Zeitgrenze 30 min – bei mehreren GB und langsamer Leitung Abbruch ohne Meldung außer „konnte nicht geladen werden“) | `KiBackends.cs:21,54-58`; `X05.cs:≈147-152` |
| 14 | Tests | NICHT GETESTET – Quellen ersetzt; echter Pull nur als alter Bericht | `W2UiTests.cs:90`; `reports/p1/ki/README.md:5` |
| übrige | – | entfällt (Einstellseite) | – |

### Programm „ki-internet“ (OpenRouter)

| # | Kriterium | Urteil | Beleg |
|---|---|---|---|
| 1–2 | Fläche/Werkzeuge | TEILWEISE – geführte Einrichtung in 3 Schritten, Modus (PC zuerst/Internet zuerst), Tageszähler, Modellliste aktualisieren, Modell je Aufgabe wählen, Datei-Frage ausprobieren | `src/Trendo.App/MainWindow.OpenRouter.cs:127-233` |
| 4 | Dateien | VOLLWERTIG – Bild/Audio/Video bis 20 MB an ein passendes Modell | `KiBackends.cs:188-206` |
| 12 | Fachfunktion real | VOLLWERTIG – echte Aufrufe, nur `:free`-Modelle, Ausweichen auf höchstens 3 Modelle, Kontogrenze erkannt | `KiBackends.cs:219-220,234-240`; `IntelligenceCore.cs:60,109,123` |
| 14 | Tests | NICHT GETESTET – `OpenRouterModelsOverride`, `OpenRouterValidateOverride`, `OpenRouterFileOverride` | `OpenRouter.cs:20-24` |
| übrige | – | entfällt (Einstellseite) | – |

**Gesamturteil X-05: TEILWEISE (am weitesten der vier).** Laden, Einrichten und Ausprobieren sind echt gebaut; es fehlen feste Prüfsummen für Modelle, Fortschritt beim Ollama-Laden und jeder automatische echte Nachweis.

**Was fehlt (priorisiert):**
1. Feste SHA-256 je Modell-Datei im Katalog (wie bei den Laufzeiten) und Abbruch bei Abweichung.
2. Ollama-Laden mit Fortschritt, Abbruch und Fortsetzen (Stream von `/api/pull`).
3. Empfehlung nach Grafikspeicher; Hinweis vor dem Laden, wenn das Modell nicht passt.
4. Eigenes Modell hinzufügen (GGUF-Datei oder Ollama-Name).
5. Unterbrochene große Downloads fortsetzen statt neu beginnen.
6. Überspringbare „echt“-Tests: Piper laden und vorlesen, Whisper mitschreiben, ein Ollama-Modell laden – mit Beleg.

**Abnahmekriterien:**
- Auf einem frischen Datenordner lädt „Deutsche Stimme Thorsten“ Laufzeit und Stimme, „Ausprobieren“ erzeugt eine abspielbare WAV-Datei.
- Eine absichtlich veränderte Modell-Datei wird abgelehnt und gelöscht.
- Beim Laden eines 2-GB-Modells über Ollama zeigt Trendo Prozent und lässt sich abbrechen.
- Nach Netzabbruch setzt ein erneutes Laden an der alten Stelle fort.

**Aufwand/Risiko:** mittel · Risiko: Adressen und Größen der Hersteller ändern sich; Lizenzen (einige Modelle nur nicht-kommerziell, z. B. MMAudio `KiLibrary.cs (Core):202`, Qwen 2.5 Coder 3B „Qwen Research“ `:127`) bei Verkauf des Produkts.

**Dateien:** `src/Trendo.Core/W2/KiLibrary.cs`, `KiSources.cs`, `KiPyApps.cs`, `OpenRouterFree.cs`; `src/Trendo.App/MainWindow.KiLibrary.cs`, `MainWindow.KiPy.cs`, `MainWindow.X05.cs`, `MainWindow.KiSourceSetup.cs`, `MainWindow.OpenRouter.cs`, `KiBackends.cs`.

## Auffälligkeiten (Teil B)

1. **Widerspruch Regel ↔ Code bei der Claude-Anmeldung.** `ClaudeCode.cs:46` hält fest: „Verboten bleiben: claude.ai-Anmeldung in Trendo anbieten …“. Die Seite „Mit Claude verbinden“ lädt aber genau `https://claude.ai/login` in ein eingebettetes Fenster mit eigenem Profil und der Aufforderung „Melde dich unten einmal mit deinem Claude-Abo an“ (`MainWindow.Claude.cs:≈326-342`, `ClaudeWeb.cs:8-17`), inklusive Google-/Apple-Anmeldefenstern, die bewusst in Trendo gehalten werden (`ClaudeWeb.cs:≈38-41`). Braucht eine Entscheidung des Nutzers.
2. **Claude über bezahlte API ist wählbar** (`KiSources.cs:50`, Anthropic-Kopfzeilen `KiBackends.cs:169`), während `ClaudeCode.cs:14` „nur das Abo zählt“ festlegt. Freiwillig und als kostenpflichtig beschriftet – trotzdem zwei Wege zu Claude, Abgleich mit der Vorgabe „nie API“ nötig.
3. **Zwei Chat-Seiten.** „ki“ (ein Feld, kein Verlauf, nur Ollama, `axaml.cs:911-999`) neben „ki-fragen“ (voller Chat). Der Begrüßungstext von „ki“ verspricht „Aufgabe geben, Studio öffnen, Projekt starten“ (`axaml.cs:≈979`), der Code sendet nur Text (`axaml.cs:993-999`) – Scheinversprechen.
4. **„KI-Team“ ist eine Prompt-Kette.** Kein Werkzeug, kein Abbruch, keine Parallelität (`Agents.cs (Core):169-181`, `Agents.cs (App):36-46`). Die Desktop-Kachel nennt es „Agenten-Zentrale – 12 Rollen, Teams, Aufgaben“ (`MainWindow.DesktopIcons.cs:18`).
5. **„An Claude Code übergeben“ führt nichts aus** – legt nur eine Markdown-Datei ab (ehrlich beschriftet: `Agents.cs (Core):238`).
6. **Alle KI-Wege in Tests ersetzt.** Für keinen der vier Bereiche gibt es einen automatischen Test mit echter KI; Claude als Standard-KI ist laut eigenem Bericht nie mit echtem Abo geprüft (`reports/volltest-0.9.84/README.md`). Katalog-Status „IMPLEMENTED / Test PC_VERIFIED“ für ki-fragen, ki-rollen, ki-vorlagen, ki-bibliothek ist dadurch nicht gedeckt.
7. **Ollama-Laden ohne Fortschritt** und mit 30-Minuten-Grenze (`KiBackends.cs:21,56`) – betrifft „ki-quellen“ und die Ollama-Modelle der Bibliothek (`KiLibrary.cs (App):≈151`).
8. **Modell-Dateien ohne feste Prüfsumme** (nur Größe; `KiFile` hat kein SHA-Feld, `KiLibrary.cs (Core):21`).
9. **Nicht atomares Speichern:** Vorlagen (`Prompts.cs (Core):125`), Agenten-Aufgaben (`Agents.cs (Core):245`), eigene Rollen (`Agents.cs (Core):111`); beschädigte Datei wird still als leer gelesen (`Prompts.cs (Core):41`, `Agents.cs (Core):65`) – Datenverlust ohne Meldung möglich.
10. **Freigabe ohne Rückfrage:** Beim Modell-Laden wird die Freigabe angelegt und im selben Schritt selbst bestätigt (`KiLibrary.cs (App):144-145`, `X05.cs:≈149-150`, `axaml.cs:≈1272-1273`) – das Freigabe-Tor ist hier nur Protokoll.
11. **Veraltete Vorbild-Listen:** `ki-fragen-claude-app.md:16` nennt „✗ Gesprächsliste mit Suche“ – ist gebaut (`ChatWindow.cs:70-85`); `ki-vorlagen-promptbibliothek.md:13` nennt „✗ Variablen mit Eingabefeldern“ – ist gebaut (`Prompts.cs (App):94-99`).
12. **Katalog ≠ Code:** `docs/programme/katalog.json` nennt für „ki-skills“/„claude-verbinden“ das Format `.zip`; in `SkillLibrary` gibt es nur `.md`/`.txt` (`Skills.cs (Core):212-218`). Für CORE-02-Programme stehen dort teils unpassende Werkzeug-/Formatlisten (z. B. `.gltf`, `.obj` bei „verbindungen“).
13. **Bedienfehler-Quelle:** Skill-Datei per getipptem Pfad (`Skills.cs (App):≈40-42`), Studios als Kennungen (`Skills.cs (App):≈94`).

---

## Auffälligkeiten der Gruppe (Zusammenfassung; Einzelheiten in den Listen der Teile A1, A2, B, C)

1. **Kein echter Ende-zu-Ende-Beleg für die KI-Wege.** Claude, Ollama, Downloads, sd-cli, Tesseract, Netzabruf und n8n sind in den Tests ersetzt (`KiBackendsOverride`, `ClaudeRunOverride`, `KiDownloadOverride`, `ImageAiOverride`, `VisionTesseractOverride`, `AutomationHttpOverride`, `WfDueOverride`). Der Katalog-Status „PC_VERIFIED“ ist für diese Wege dadurch nicht gedeckt.
2. **KI-Aktionen liefern Plan-Text.** `ablauf.planen`, `studios.nennen`, `bild.auftrag`, `video-ki.szenenplan`, `modelle.fragebogen`; `ablauf.starten` ist gesperrt.
3. **Claude-Ergebnisse bleiben im Chat.** „Mit Claude ändern“ (Code mit KI) ändert keine Datei: Claude läuft im Chat-Ordner (`src/Trendo.App/MainWindow.ClaudeChat.cs:54-58`), bekommt 12 000 Zeichen (`src/Trendo.App/MainWindow.KreativStudios4.cs:241`) und soll die Änderung „zeigen“ (`:259`).
4. **Widerspruch bei der Claude-Anmeldung (Entscheidung des Nutzers nötig).** `src/Trendo.Core/W2/ClaudeCode.cs:46` verbietet, die claude.ai-Anmeldung in Trendo anzubieten; `src/Trendo.Core/W2/ClaudeWeb.cs:14` führt `claude.ai/login`, und die Seite „claude-verbinden“ lädt sie laut Teil B eingebettet. Zusätzlich ist Claude über die bezahlte API wählbar (Teil B), obwohl „nie API“ gilt.
5. **Namen versprechen mehr als die Leistung.** „Videos mit KI“ = Diashow; „Bilder erkennen“ = Bildprüfung; „KI-Agenten bauen“ = Datei schreiben; „KI-Team“ = Textkette; „Großprojekte umsetzen“ = Tafel + 4 feste Ketten; „Eigene KI trainieren“ = Naive Bayes ohne Nutzung.
6. **Robustheit.** Nicht atomares Speichern und stilles Leerlesen bei Abläufen (`src/Trendo.Core/W2/Workflows.cs:86,261`) sowie bei Vorlagen, Agenten-Aufgaben und Rollen (Teil B); die Recherche-Sitzung wird bei jedem Start gelöscht (`src/Trendo.App/MainWindow.Research.cs:26`).
7. **Doku ≠ Code.** `docs/programme/katalog.json` führt neun STUDIO-*-Programme als „IN_PROGRESS / Test NOT_RUN“ mit leerer Testliste, obwohl Tests existieren, und die übrigen als „PC_VERIFIED“ ohne echten Lauf; Engines und Formate sind je Quelldatei statt je Programm eingetragen; die Vorbild-Listen zu Wissen, Messdaten und Recherche führen Gebautes als fehlend.
8. **Zweite Chat-Seite „ki“** mit einer Begrüßung, die mehr verspricht, als der Code tut (Teil B; Zeilenangaben in `src/Trendo.App/MainWindow.axaml.cs` dort nur ungefähr).

**Nicht geprüft (gesamt):** Laufzeitverhalten aller Programme; der kie.ai-Weg; `MainWindow.ClaudeBridge.cs`, `KiPy.cs`, `KiStore.InstallModelFile` im Detail; X-11 „vorhaben“ und SPEC-10 „zwilling“ (andere Gruppen); Inhalte unter `reports/` über eine Namenssuche hinaus. Die Teile A1, A2 und B stammen aus drei parallelen Lese-Läufen; stichprobenartig gegen den Code geprüft wurden daraus die Befunde 3 und 4 sowie das Naive-Bayes-Training.
