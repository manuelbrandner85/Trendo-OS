# Universal Studio Intelligence: Stand je Phase

Auftrag vom 09.11.2026. Hier steht nur Belegtes.

## Phase A: Audit (fertig)

Read-only, keine Änderung am Programmcode. Ergebnis: [Funktionsmatrix](TRENDO_STUDIO_FUNKTIONSMATRIX.md), erzeugt mit `scripts/studio_matrix.py`.

| Stufe | Studios |
|---|---|
| nur spezifiziert | 1 (SPEC-07, abgelehnt und bleibt abgelehnt) |
| teilweise | 14 |
| funktionsfähig ohne Testbeleg | 0 |
| getestet | 43 |
| abgenommen | 0 |

### Wichtigste Befunde

- Vorhandene Systeme sind `StudioCatalog` (Namen), `StudioRegistry` (Status aus `requirements.yaml`), `CapabilityCatalog` (Fachprofile), `ApprovalGate`, `Rbac`, `AuditLog`, `WorkflowEngine`, `AgentCenter`, `ProjectStore`, `IntelligenceCore`. Der Auftrag braucht keine zweite Registry, sondern Erweiterungen dieser Teile.
- 58 Studios, 98 Programme. Nur 12 Studios haben ein Dokumentprogramm mit Befehlen und Dokumentdaten. Bei den anderen erreicht Claude nur, was auf der Seite sichtbar ist.
- Die Funktionskataloge fehlen bei 27 oder 28 Studios (je nach Zählung in den 4.0-Dokumenten). Das ist die größte Lücke für die Funktions-Baseline.
- Die Testzahlen je Studio sind grob, viele Studios teilen dieselben Tests. Der Punkt „mindestens 20 eigene Testfälle“ ist für die meisten Studios nicht belegt.
- Die Belege in `requirements.yaml` stammen teils aus Version 0.9.2.
- Nutzertests mit Anfängern gab es bisher nicht. Sie lassen sich nicht automatisieren, nur vorbereiten (Klickzahl und Zeit bis zur ersten Aktion lassen sich messen).

## Claude-Zugang (Teil von Phase C)

Claude bedient Trendo während der Arbeit selbst über 15 Werkzeuge (`W2/TrendoMcp.cs`, `MainWindow.ClaudeBridge.cs`, `Trendo.Api/ClaudeBridgeHost.cs`). Der Zugang lauscht nur auf 127.0.0.1, gilt für einen Lauf und braucht ein Zufalls-Kennzeichen. Anmeldedaten von Claude berührt er nicht.

Echte Läufe mit Claude Code 2.1.316 am 09.11.2026 gegen ein laufendes Trendo-Fenster:

| Lauf | Ergebnis |
|---|---|
| Auftrag „kleines fertiges Jump-and-Run, Thema Weltraum, drei Level“, Stufe Sicher | Ohne Klick in 2 Minuten 47: Spiel „Sternenspringer“ mit drei Leveln, spielbar gebaut, gespeichert, Claude meldete offene Punkte selbst (Level 1 ohne eigenen Namen, kein eigener Ton). |
| Kurzauftrag, Stufe Voll mit Bildschirmfoto | Titel gesetzt, Fenster dreimal angesehen, gespeichert. Blender, Unreal und Godot waren verbunden. |

Dabei gefundene und behobene Fehler: Das Bildschirmfoto stürzte beim Verkleinern ab. Ein reiner Dateiname bei „Speichern unter“ landete im Arbeitsverzeichnis des Prozesses.

Nicht geprüft: der Lauf durch den echten Chat-Bereich der App (die Läufe gingen direkt an die Werkzeuge), eine echte Abo-Pause, ein Lauf mit Unreal oder Blender in einem Auftrag.

## Phase B: Funktionsschutz und Zuordnung (Baseline erstellt, Lücken offen)

`scripts/function_baseline.py` erfasst aus dem Quellcode jede Funktion mit festem Namen in `baseline/funktionen.csv`. Der Paket-Test `test_funktions_baseline.py` schlägt an, sobald eine davon aus dem Code verschwindet.

| Messgröße | Wert |
|---|---|
| Erfasste Funktionen | 2174 (1212 Knöpfe, 641 Felder, 189 Auswahllisten, 132 Befehle) |
| Mit Test, der den Namen nennt | 1160 |
| Einem Studio zugeordnet | 988 |
| Stellen mit Namen, die erst zur Laufzeit entstehen (nicht erfasst) | 519 |

Grenzen: Die Spalten Ausgabe und Abhängigkeiten stehen auf „offen“, weil sie sich aus dem Code nicht sicher ablesen lassen. Der Schutz prüft, ob der Name noch da ist, nicht ob die Funktion gleich gut arbeitet. Das bleibt Aufgabe der Tests je Studio.

### Zuordnung zu vorhandenen Bausteinen

| Punkt des Auftrags | Vorhanden | Noch zu tun |
|---|---|---|
| 02 Fachverständnis | `CapabilityCatalog`, `StudioProfiles`, `StudioCatalog` | zusammenführen und maschinenlesbar für Claude ausgeben, Absichtserkennung über diese Daten |
| 03 Claude im Studio | Claude-Bereich, `ClaudeCode`, neuer Zugang | Kontext vollständig mitgeben (Projekt, Entscheidungen, Fehler) |
| 04, 05 Einfach und Profi | `UserLevel`, `SimplifyPage`, `SimpleRibbon` | Startansicht je Studio, Ansichten vereinheitlichen |
| 06, 11 Sprache und Hilfe | Studio-Zeile, Claude im Menüband, Rechtsklick | prüfen je Studio |
| 07 Programmsteuerung | `StudioMcp`, `ToolPath`, `ModelConverter.FindBlender` | einheitliche Prüfung: installiert, startbar, Schnittstelle, Aktionen, Rechte |
| 08 Zusammenarbeit | `WorkflowEngine`, `AgentCenter` | Ablauf mit Projektbezug über zwei Studios |
| 09 Projektgedächtnis | `ProjectStore`, `ClaudeMemory`, `KiMemory`, `ClaudeTimeline` | Felder je Projekt ergänzen, sichtbar, löschbar |
| 12 Autonom mit Kontrolle | neuer Zugang, `ApprovalGate`, `Rbac`, `AuditLog`, Not-Stopp | Sicherung vor riskanten Änderungen |
| 14 Tests | `DeadButtonTests`, Studio-Abnahme, Baseline-Test | eigene Tests je Studio, Messung für Anfänger |

Nicht möglich ohne Menschen: Nutzertests mit echten Anfängern. Trendo kann Klickzahl und Zeit bis zur ersten Aktion messen, die Erfolgsquote mit echten Personen muss der Projektinhaber erheben.

## Phase C: Referenzstudio „Spiele bauen“ (CORE-06)

| Punkt | Umsetzung | Beleg |
|---|---|---|
| 02 Fachverständnis | `StudioKnowledge.cs` (Zweck, Aufgaben, Programme, Formate, Abläufe, Qualität, Helfer), ergänzt `CapabilityCatalog` | Tests „Referenz:“ |
| 03 Projektkontext | Werkzeuge `projekt_kontext`, `studio_wissen` | Tests, Lauf unten |
| 04 Startansicht | „Was möchtest du machen?“ mit fünf Aufgaben, darunter Neu und Zuletzt | Bild, Test |
| 05 Einfach und Profi | vorhandene Stufen. Umschalten ändert keine Daten, alle Befehle in jeder Stufe erreichbar | Tests |
| 06 Natürliche Sprache | Absichtserkennung `StudioKnowledgeBase.Match`, Eingabezeile geht an Claude | Tests, Lauf |
| 07 Programmprüfung | `programme_pruefen`: installiert, startbar, Schnittstelle, Aktionen, Rechte. MCP gilt nur nach gemeldeter Verbindung | Tests |
| 09 Gedächtnis | `projekt_merken` im vorhandenen, sichtbaren und löschbaren `ClaudeMemory` | Tests |
| 12 Autonom mit Kontrolle | `ApprovalGate`, `AuditLog`, Not-Stopp | Tests |

### Messwerte

| Messung | Wert |
|---|---|
| Klicks bis zur ersten sinnvollen Aktion | 1, danach nur noch das Thema tippen |
| Öffnen des Studios im Test | unter 1,5 Sekunden (Grenze im Test) |
| Eigene Tests des Studios | 23 |

### Echter Lauf durch den Claude-Bereich der App

Auftrag über die Eingabezeile des Studios: „Plattformer-Vorlage zu einem kleinen Dschungel-Spiel mit zwei Leveln, im Programm bleiben, prüfen, speichern“. Claude Code 2.1.316, Stufe Voll, 7 Minuten 28, keine Rückfrage, kein offener Plan, kein Klick. Ergebnis: Spiel „Dschungel-Dash“ gebaut und gespeichert. Abo-Nutzung laut Trendo rund 6,2 Millionen verarbeitete Tokens, der größte Teil davon Zwischenspeicher.

Ein freier Auftrag ohne die Vorgabe „im Programm bleiben“ war nach 13 Minuten nicht fertig, weil Claude zusätzlich eigenen Spielcode schrieb. Der Prüfaufbau brach ihn ab.

### Offen im Referenzstudio

- Claude meldete echte Grenzen des Programms: Das Spiel endet fest bei 20 Punkten, die Hintergrundfarbe und die Tastenbelegung lassen sich nicht einstellen.
- Unreal, Unity und Blender wurden in diesem Studio nicht in einem echten Lauf benutzt. Unreal ist ohne Editorstart nicht auf Startfähigkeit prüfbar.
- Die Ampel im Programmkatalog bleibt rot, bis der Katalog neu erzeugt ist. Die Erkennung des Editors ist angepasst.
- Die obere Eingabezeile und das Feld „Was für ein Spiel möchtest du?“ stehen doppelt da. Das Zusammenlegen muss prüfen, dass nichts verloren geht.
- Tests mit echten Anfängern fehlen und können nur Menschen leisten.
- Keine Abnahme: Das Abnahmeprotokoll und die QA- und Security-Prüfung fehlen noch.

## Phase D: Ablauf über zwei Studios (Bildbearbeitung → Spiele bauen)

Es gibt zwei Wege, beide auf vorhandenen Systemen und ohne zweites Ablauf- oder Projektsystem:

- **Fester Ablauf:** Rezept „Spielfigur → fertiges Spiel“ in der vorhandenen Studio-Fusion unter Produktion. Läuft nur nach Freigabe über `ApprovalGate`, Schritte stehen im `AuditLog` und in `PROTOKOLL.txt`.
- **Über Claude:** Werkzeuge `uebergeben`, `uebergaben`, `uebernehmen`. Die Übergabe liegt als Datei im Projektordner (`.trendo/uebergaben.json`) mit Prüfsumme, gilt nur für dieses Projekt und wird ungültig, wenn die Datei danach verändert wird.

Tests: 3 für die Übergabe, 4 für die Kette über den echten Eingabeweg (Bild exportieren, übergeben, übernehmen, bauen, Figur im gebauten Spiel; falsches Studio, falsche Verwendung, doppelte Annahme; Ablauf nur nach Freigabe; Abbruch ohne Namen).

Echter Lauf durch den Claude-Bereich: Auftrag „einfachen Roboter im Bildprogramm zeichnen, als PNG weitergeben, in der Plattformer-Vorlage einsetzen, Spiel bauen“. 3 Minuten 48, keine Rückfrage, kein Klick. Ergebnis: Spiel „Robo-Sprung“ mit der gezeichneten Figur.

Offen: Die vorhandene `WorkflowEngine` kennt nur Dateischritte und wird nicht benutzt, die Kette läuft über die Studio-Fusion. `AgentCenter` wird im festen Ablauf nicht eingesetzt. Längere Ketten aus dem Auftrag (Stadt mit 3D, Material, Animation, Unreal, Ton, QA) sind nicht gebaut.

## Phase E: übrige Studios

### Paket 1: Kreativ und 3D

Videos und Fotos (CORE-08), Filme (STUDIO-FILM-01), Musik (STUDIO-MUSIK-01), 3D-Modelle (CORE-05), Webseite (CORE-07).

| Baustein | Stand |
|---|---|
| Fachwissen, Aufgaben, Absichtserkennung | für alle fünf, 30 Tests |
| Startseite „Was möchtest du machen?“ | nur bei Dokumentprogrammen (Videoschnitt, Bild, Ton, Medien, Film). Musik, 3D und Webseite behalten ihre Vorschläge in der Eingabezeile |
| Programmprüfung | ffmpeg, Blender, f3d, git zusätzlich zu den Spiele-Engines |
| Claude-Zugriff bis ins Detail | Dokumentprogramme und 3D-Szene. Bei der Webseite arbeitet Claude direkt in den Projektdateien. Musik nur auf Seitenebene |

Echter Lauf durch den Claude-Bereich mit echtem Blender: Auftrag „Holztisch 120 × 70 × 75 cm“. Claude baute neun Teile mit den richtigen Maßen, renderte dreimal, sah sich jedes Bild an und korrigierte selbst einen Farbfehler und zu helles Licht. Kein Klick, keine Rückfrage.

Grenzen dieses Laufs: Die Holzmaserung fehlte, weil der Prüfaufbau keine Oberflächen aus dem Internet lädt. In der normalen App ist das nicht geprüft. Claude schlug eine Regel zum Lernen vor, die mit Ja oder Nein bestätigt wird. Sie hält die Arbeit nicht auf.

Dabei behoben: Claude übergab Werte teils als Text mit JSON darin, das Teil wurde dadurch blau.

Offen: die übrigen Studios. Sitzung A legt parallel 16 Studios zusammen (freigegeben vom Nutzer), die Studio-Kennungen ändern sich beim Zusammenführen.

### Paket 2: Büro und Geld

Briefe (STUDIO-LETTER-01), Texte und Tabellen (SYS-10), E-Mails und Termine (SYS-09), Geld und Rechnungen (SPEC-11), Online verkaufen (STUDIO-ONLINE-BUSINESS-01), Print on Demand (STUDIO-POD-01), Kleine Helfer (SYS-02).

| Baustein | Stand |
|---|---|
| Fachwissen, Aufgaben, Absichtserkennung | für alle sieben, 36 Tests |
| Startseite „Was möchtest du machen?“ | nur Print on Demand. Die anderen behalten ihre Vorschläge in der Eingabezeile |
| Programmprüfung | zusätzlich LibreOffice, qpdf, Tesseract |
| Claude-Zugriff | Print on Demand bis ins Detail, Briefe über die Datei `Brief.json`. Die übrigen auf Seitenebene |
| Rückfrage | Senden, Bezahlen und Bestellen fragen immer, Entwerfen nicht |

Echter Lauf durch den Claude-Bereich: Auftrag „Kündigung Fitnessstudio“ mit erfundenen Beispieldaten. 1 Minute 27, kein Klick, keine Rückfrage. Ergebnis: Brief nach DIN 5008 im Programm und als Word-Datei. Claude nannte offen, was es nicht wissen kann (Kündigungsfrist, Form laut Vertrag), und versandte nichts.

Offen: In Texte, Tabellen, E-Mails und Geld gab es keinen echten Claude-Lauf, nur Tests. Claude schlug wieder eine Lernregel vor, die auf Ja oder Nein wartet, die Arbeit aber nicht aufhält.

### Paket 3: Technik und Wissen

Wohnung (SPEC-01), Schaltungen (SPEC-02), Statik (SPEC-03), Geräte überwachen (SPEC-10), Karten (SPEC-13), Fräsen (SPEC-14), Auto (SPEC-16), Kameras und Roboter (CORE-09), Messwerte (SPEC-05), Lernen (SPEC-06), Wissen (CORE-10), Recherche (SYS-08).

| Baustein | Stand |
|---|---|
| Fachwissen, Aufgaben, Absichtserkennung | für alle zwölf |
| Startseite „Was möchtest du machen?“ | bei den sechs Dokumentprogrammen |
| Claude-Zugriff | sechs Dokumentprogramme bis ins Detail, die übrigen auf Seitenebene |

Sicherheitslücke gefunden und geschlossen: Solange Trendo auf eine Freigabe wartet (etwa vor einer Roboterbewegung), hätte Claude den Freigabe-Knopf selbst drücken können. Jetzt fragt jeder Klick und jeder Befehl von Claude beim Nutzer nach, solange eine Freigabe offen ist. Wörter wie Freigeben, Genehmigen, Einverstanden und Mikrofon fragen immer.

Echter Lauf durch den Claude-Bereich: Einfeldträger IPE 200, 5 m, 10 kN/m. 1 Minute 41, kein Klick, keine Rückfrage. Das Programm lieferte das von Hand nachprüfbare Moment von 31,25 kNm. Claude meldete von sich aus, dass die Durchbiegung über L/300 liegt und das Kippen vom Programm nicht geprüft wird (das stammt aus seiner eigenen Handrechnung).

Offen: In den anderen elf Studios gab es keinen echten Claude-Lauf, nur Tests.

## Phase F

Noch nicht begonnen.
