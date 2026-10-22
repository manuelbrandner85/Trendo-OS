# Fahrplan ab Master bis 94

Grundlage: `docs/spec/MASTER_BIS_94.md`. Eine Engine: `StudioWorkflow`, `ApprovalGate`, `ProjectStore`, `IntelligenceCore.AskAsync`. Kein zweiter Agent, kein zweites Freigabetor, keine kostenpflichtige API als Voraussetzung.

Jede Phase endet mit einem grünen `dotnet test Trendo.sln -c Release` oder sie gilt nicht als abgenommen. `blocked` ist nicht `passed`.

## Phase 1 – Fundament

**Inhalt:** Ein Arbeitsplatz. Auftrag, Projekt, Rückfrage, Freigabe, Datei, Vorschau, Prüfung und Export an einer Stelle. Der vorhandene Prompt-zu-Auftrag-Ablauf. Ein lokaler Adapter. Die Prüfung liest die echte Datei.

**Abnahme:**

- Die Seite „Ein Arbeitsplatz“ öffnet sich aus der Leiste.
- X-11 schreibt `arbeitsplatz/blatt-vN.md`. Der Stand ist „QA bestanden“ nur, wenn die Datei gelesen wurde und der Hinweis auf die Grenze darin steht.
- Ein Studio ohne Adapter endet mit „Nicht unterstützt“ und ohne Fachdatei.
- Ohne Projekt kommt eine Rückfrage, keine Datei.
- „Einmal freigeben“ bleibt aus, solange nichts nach außen soll. Ein Satz im Text gibt keine Freigabe.
- 23, 25, 34, Language Studio 2.0 und 86 bleiben aus. 35 wird nicht aktiviert. Die 120-Stunden-Regel bleibt.

**Rechner:** Linux reicht. Kein Windows, kein Blender, kein Unreal, keine Kamera, kein Roboter.

Diese Phase ist der Inhalt dieses Branches. Sie macht 42 und 43 nicht fertig.

## Phase 2 – Alltag: Brief, Tabelle, E-Mail, Rechnung

**Inhalt:** Die offenen PRs #244, #245, #247 und #249 auf den Arbeitsplatz setzen, nicht daneben. Konflikte in `CreateDefault` und der Matrix von Hand lösen, damit nur eine Adapterliste entsteht.

**Abnahme:**

- Brief: DOCX liegt im Projekt, lässt sich ändern und wieder öffnen. PDF nur, wenn LibreOffice wirklich startet und A4 liefert.
- Tabelle: bekannte Werte, Formel SUMME, das Ergebnis stimmt mit einer festen Rechnung überein.
- E-Mail: ohne Freigabe null Nachrichten. Mit Freigabe nur, wenn ein Versandweg angeschlossen ist. Sonst Entwurf und „nicht angeschlossen“.
- Rechnung: Beträge mit Decimal. Keine Buchung, keine Zahlung. Der Zahl-Adapter bleibt ohne `CanPerformSideEffect`, bis ein echter Weg und eine einmalige Freigabe da sind.

**Rechner:** DOCX geht unter Linux. PDF und ein echtes Word-Layout brauchen LibreOffice. Das Chrome in diesem Image druckt US Letter und beendet sich nach dem PDF nicht. Das ist kein App-Fehler. Ein Windows-PC mit LibreOffice ist für die PDF-Abnahme nötig.

## Phase 3 – Webseite, Programm, Nachbau

**Inhalt:** #246 an `StudioWorkflow` hängen, nicht als zweiten Auftragsweg stehen lassen. #250 und #248 darüber legen. Veröffentlichung und Git-Push nur über `ApprovalGate`.

**Abnahme:**

- Aus einer Beschreibung entsteht ein Ordner mit HTML. Die Qualitätsprüfung nennt Handy, Ladezeit, Barrierefreiheit und Links anhand der erzeugten Datei.
- Eine Programmvorlage lässt sich bauen oder zeigt den echten Compilerfehler. Kein Push.
- Ein Nachbau schreibt nur eigene Dateien aus der Beschreibung. Kein Kopieren fremden Quelltexts.

**Rechner:** Lokal ohne Windows. Veröffentlichen braucht eine ausdrückliche Freigabe und ein Ziel, das der Eigentümer nennt.

## Phase 4 – Rechnen, Wissen, übrige Katalogstudios

**Inhalt:** #255, #256 und #257. Nur lokale Dateien oder ehrliche Sperren. Maschine, Scan-Start, Sendung, Quellen-Download und Theme-Umbau bleiben aus.

**Abnahme:**

- Jede rechnende Aktion hat einen Test mit bekannten Eingaben und einem festen Ergebnis, inklusive Einheit.
- Jede gesperrte Aktion hat einen Test, der null Bewegungen, null Sendungen und null Downloads zählt.
- Die Matrix nennt jede der 54 IDs genau einmal. `ready` nur mit einem Test, der die Aktion wirklich ausführt.

**Rechner:** Linux reicht für die Rechnungen. CNC, Roboter und Funkgeräte nicht anfassen.

## Phase 5 – Bild, 3D, Spiel

**Inhalt:** #251, #253, #254. Datei nur, wenn das Werkzeug da ist. Sonst „blockiert“, ohne erfundene Vorschau.

**Abnahme:**

- Bild: ffmpeg wandelt eine vorhandene Datei um. Log und Ausgabe existieren. Hochladen bleibt aus.
- 3D: OBJ liegt im Projekt. Blender läuft nur, wenn `blender` startet und ein Log schreibt.
- Spiel: Browser-Spiel als HTML. Unreal nur mit installiertem Editor und einem echten Projektordner. AION und GENESIS werden nicht aus diesem Repo erzeugt.

**Rechner:** ffmpeg ist auf diesem Image vorhanden. Blender und Unreal brauchen eine Installation, praktisch einen Windows-PC mit den Programmen und genug Platte. Ohne sie bleibt die Aktion blockiert.

## Phase 6 – Lokale KI wählen

**Inhalt:** #252. Lokale Laufzeit zuerst. Kostenlose Online-Dienste nur nach Einwilligung mit Umfang und Frist. Keine kostenpflichtige API als Voraussetzung.

**Abnahme:**

- Ohne Einwilligung geht kein Aufruf an eine externe Adresse. Der Test zählt die Aufrufe.
- Die Wahl schreibt, welches Modell genommen wurde und warum. Ein fehlendes Modell ist ein Hinweis, kein erfundenes Ergebnis.
- Widerruf stoppt den nächsten externen Aufruf.

**Rechner:** Ein lokales Modell braucht RAM. Die automatische Wahl nach GPU ist erst auf einem Rechner mit echter GPU-Abfrage abgenommen. Dieses Image beweist den Sperrweg, nicht die Modellqualität.

## Phase 7 – Nutzwert ohne neues Studio

Erst wenn Phase 1 bis 6 auf `main` sind. Weiterhin derselbe Ablauf. Reihenfolge nach Alltag:

1. Persönliche Planung auf SYS-02 und SYS-09: Aufgabe, Termin, Notiz als Datei.
2. Lernen auf SPEC-06: Karte anlegen, abfragen, speichern.
3. Wissen auf CORE-10: Eintrag bleibt im Projekt, Suche findet nur diesen Text.
4. Vertragsentwurf auf dem Briefstudio: mit dem Satz, dass das keine Rechtsberatung ist.
5. Buch als DOCX oder Markdown, EPUB erst wenn Pandoc installiert ist und der Lauf ein Log hat.
6. Kennzahlen aus einer CSV mit festem erwartetem Wert. Keine zweite Datenbank.
7. Apps auf dem vorhandenen PageBuilder, Qualität über SYS-13.
8. Erweiterung als Entwurf im vorhandenen Katalog, Installation nur nach Freigabe.

**Abnahme je Punkt:** eine Datei im Projekt, ein Test, ein ehrlicher Stand. QA-017, QA-019 und Codesignatur werden dabei nicht als bestanden markiert.

**Rechner:** Linux für Dateien. Pandoc nur wenn installiert.

## Phase 8 – Fachrechnung, noch ohne Maschine

Architektur, Statik, Elektronik, Messwerte, Karten, Fahrtenkosten, Größen, VR-Skizze, Scan aus vorhandenen Fotos. Deterministische Rechnung mit Einheit. Externe Programme (FreeCAD, KiCad, QGIS) nur nach einem erfolgreichen Start. Sonst blockiert.

Fertigung rechnet Drehzahl und Vorschub. Sie startet keine Fräse und keinen Drucker.

**Rechner:** Die reine Rechnung geht unter Linux. CAD und Slicer brauchen die Programme, meist Windows. Keine Maschinenfreigabe in dieser Phase.

## Phase 9 – Echte Geräte

Kameras, Roboter, Handy, Raspberry Pi, Smart Home, CNC, Fahrzeug. Erst Simulation oder nur Anzeigen. Ein Befehl an Hardware nur nach Eignungsprüfung und einmaliger Freigabe im vorhandenen Tor. Timeout und Abbruch senden nichts nach.

**Rechner:** Windows oder der Zielrechner mit dem Gerät, Kamera, Netz und der passenden Freigabe. Dieses Cloud-Image kann das nicht abnehmen.

## Phase 10 – Schwere 3D- und Spielpakete

49 Rest, 50 Rest, 65, 87 bis 94, soweit sie Blender oder Unreal brauchen. 93 und 94 haben eine volle Spec im Master. Ohne Editor bleibt der Stand „Entwurf“ oder „blockiert“. Keine Behauptung, ein Effekt sei gelaufen.

AION und GENESIS bleiben getrennte Projekte. Story und Figuren werden hier nicht geändert. Der Eigentümer muss die Projektordner nennen, bevor jemand dort schreibt.

**Rechner:** Windows, installierte Engine, GPU, genug RAM und VRAM. Leistungswerte nur aus einem echten Lauf.

## Nicht in diesen Phasen

- 86 wird nicht gebaut.
- 35 wird nicht aktiviert. Reparatur in 85 bleibt Anzeige, bis 35 entschieden ist.
- 23, 25, 34 und Language Studio 2.0 bleiben aus.
- Die 120-Stunden-Forensik bleibt, wie sie in `ForensicsLab` steht.
- Kein Merge auf `main` aus diesem Auftrag.

## Entscheidung des Eigentümers

1. Reihenfolge der offenen PRs #237 und #243–#257. Sie kollidieren in denselben Dateien. Phase 1 sollte die gemeinsame Liste sein, die anderen setzen darauf auf.
2. Ob PR #246 vor dem Merge an `StudioWorkflow` angebunden wird.
3. Ob Gesundheitsdaten (76) überhaupt gespeichert werden dürfen.
4. Ob jemals ein Zahlungsweg (47, 89) angeschlossen wird, und welcher. Bis dahin bleibt Zahlen aus.
5. Wo AION und GENESIS liegen, und dass dort ohne ausdrücklichen Auftrag nichts geändert wird.
6. Welcher Windows-PC mit Blender, Unreal und LibreOffice die späteren Phasen abnimmt.
7. Ob 35 je aktiviert wird. Bis dahin keine Selbstreparatur.
