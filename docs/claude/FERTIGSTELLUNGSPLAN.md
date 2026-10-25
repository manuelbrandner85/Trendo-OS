# Trendo OS – Fertigstellungsplan (Claude Code)

**Stand:** 2026-10-24, `main` @ `74761b1` (T-02xx-Block und T-0218 übernommen) · **Grundlage:** `requirements.yaml`, `docs/spec/GAP_ANALYSIS.md`, `docs/BUILD_PLAN_V2.md`, `docs/spec/design-41/GAP_DESIGN_41.md`, `handoff/…bis_94.md`, Audit `reports/claude/AUDIT_CLAUDE_MIGRATION_2026-10-23.md`
**Regel:** Jede Zeile wird ein eigener Auftrag `tasks/T-<nr>-<kurz>.md` und ein eigener PR. Status nur mit Rohlog. Nichts hier ist erledigt, wenn es nicht als erledigt markiert ist.

## Was „fertig“ heißt
Trendo OS ist fertig, wenn alle Punkte erfüllt sind:
1. Alle genehmigten Funktionen (Basis-Module, Business 30, Letter 25, Verbesserungen 13–41 und 42–96 ohne 86) sind **IMPLEMENTED** und mit Rohlog **PC_VERIFIED**, für Linux-/Pi-Teile auf echter Hardware.
2. Alle drei Editionen haben ein getestetes Installationsmedium inklusive Deinstallation, Update und Rückfall: **C** Windows-Setup, **B** Linux USB/SSD, **A** Raspberry-Pi-Image.
3. Die QA-Gates sind bestanden: QA-017 (Narrator/Kontrast/Mehrmonitor), QA-019 (echter Offline-Lauf), Studio-Abnahmetests, Sicherheits-Negativtests und die 120-h-Forensiktests.
4. Release-Pflichten sind erfüllt: Code-Signatur, SBOM, Handbuch, Release-Kanal, förmliche Abnahme durch Manuel.

## Ist-Stand in Zahlen (23.10.2026)
| Bereich | Umfang | Fertig (IMPLEMENTED + PC/TESTED) | Offen |
|---|---:|---:|---|
| Basis-Module (`modules`) | 54 | 46 | 7 IN_PROGRESS (CORE-01, CORE-04, SYS-01, SYS-12, X-08, X-09, STUDIO-LETTER-01); SPEC-07 abgelehnt |
| Online-Business-Studio (`BUS-001…030`) | 30 | 0 | alle nur `TESTED_BOX` (Linux-Box), PC-Nachweis fehlt |
| Brief-Studio (`LTR-…`) | 25 | 25 | – (Modul selbst noch IN_PROGRESS) |
| Verbesserungen 13–41 | 25 genehmigte (≈ 340 Funktionen) | 0 abgenommen | im Register (`improvements`), 24 IN_PROGRESS (23 mit Teilfunktionen laut GAP_ANALYSIS §2, dazu Design 41) |
| Verbesserungen 42–96 | 54 genehmigte (≈ 2 400 Funktionen) | 0 abgenommen | 42/43: gemeinsamer Kern auf `main`, Fachadapter in offenen PRs; Rest nur APPROVED_SPEC; 95/96 ohne Funktionsliste; 97 offen |
| Editionen | 3 | 0 abgenommen | C am weitesten, B bootet in QEMU, A nicht begonnen |
| Build/Tests | siehe Commit-Nachricht der Zusammenführung | – | Compiler-Warnungen beheben (A6) |
| QA-Testfälle (`tests` in `requirements.yaml`) | 44 (BQ, LTR) | 0 | alle `NOT_RUN` |
| Offene Aufträge in `tasks/` ohne Bericht | 0 | – | T-0100…T-0108 am 2026-10-25 gegen den Ist-Stand geprüft (Berichte); offen nur T-0101 (Tokens, zurückgestellt) und PC-Messung Start/RAM |
| Offene Pull Requests auf GitHub | 37 | – | von früheren Agenten, siehe O |

## Arbeitspakete in Reihenfolge

### A – Fundament für den reinen Claude-Betrieb
| # | Auftrag | Wer/Freigabe |
|---|---|---|
| A1 | Rechte-Patch `docs/claude/M2_RECHTE_PATCH.diff` anwenden (Push `main`/`--force`) | **Manuel** |
| A2 | ✅ erledigt 2026-10-23: Claude Code 2.1.282 → 2.1.309 (Hook blockiert jetzt auch bei Ausfall) | – |
| A3 | ✅ erledigt 2026-10-24: #281 nach `main` (6d92923), letzte Reste des früheren Agenten entfernt (16c8e99) | – |
| A4 | ✅ erledigt 2026-10-24: IMP-42…97 in `decisions.yaml` (86 REJECTED, 97 UNDECIDED, 95/96 mit Hinweis „Funktionsliste fehlt“), `source_sha256` erneuert, Sperren im Test | – |
| A5 | ✅ erledigt 2026-10-24: `requirements.yaml` mit `improvements` (IMP-13…97) und `improvement_functions` (120 Einzelfunktionen zu 42/93/94); Quellen `docs/spec/IMPROVEMENTS.csv`, `IMPROVEMENT_FUNCTIONS.csv`; Generator + Prüfer erweitert | – |
| A6 | ✅ erledigt 2026-10-24: alle Compiler- und xUnit-Analysator-Warnungen behoben, Build mit 0 Warnungen | – |

### D – Design 41 komplett mit Claude
| # | Auftrag | Wer/Freigabe |
|---|---|---|
| D1 | ✅ erledigt 2026-10-24: 10 Ansichten + 2 Atlanten mit Claude Code als HTML gebaut (`tools/design/views.py`, Rendern `python tools/design/build.py`), alte Bilder ersetzt, Manifest und Prüfsummen neu | – |
| D2 | ✅ erledigt 2026-10-24: 5 Hintergründe lokal mit KI (ComfyUI + FLUX.2 klein, `tools/design/ki_hintergruende.py`), Herkunft in `docs/design/HINTERGRUENDE.json` | – |
| D3 | ✅ erledigt 2026-10-25: T-0100…T-0108 gegen den Ist-Stand geprüft. Erledigt: T-0104, T-0105, T-0106, T-0107 (Leistungsprofile ergänzt) und T-0108. Überholt durch T-0218: T-0100, T-0102, T-0103. Teilweise: T-0101 (Tokens als ResourceDictionary, zurückgestellt) | – |
| D7 | Startzeit/RAM am PC belastbar messen: `--startup-log` schrieb im Setup-Build 0.9.7 keine Datei – Ursache klären (T-0100/T-0107) | Claude |
| D4 | Ist-Stand gegen die neuen Claude-Referenzen aus D1 vergleichen (Vorher/Nachher-Bilder mit `tools/Trendo.Render`), Abweichungen als Aufträge | Claude |
| D5 | Viewport-/DPI-Matrix, Kontrast, Tastatur (Design-41-Fkt. 19/20) am Windows-PC – Renders 1024×768, 1536×800 (hell/dunkel), 3840×2160 für Startseite geprüft (T-0218); echte DPI-Stufen offen | Claude |
| D6 | ✅ erledigt 2026-10-24: T-0218 Startseite nach Referenzbild 1, „Alle Studios“ nach Referenzbild 2; Hintergrund „Abendrot“ und 54 Kartenbilder lokal mit FLUX.2 (`docs/design/STUDIOBILDER.json`); Hinweis bei IMP-41 in `decisions.yaml` | – |

### C – Edition C (Windows) abnahmefähig machen
| # | Auftrag | Wer/Freigabe |
|---|---|---|
| C1 | 7 IN_PROGRESS-Module abschließen; die PC-Seite fehlt für keins, offen sind vor allem Gegenstellen (Pi/Android) → mit B/A/I verzahnt | Claude |
| C2 | BUS-001…030 am Windows-PC prüfen, Rohlog + SHA-256 in `reports/` | Claude |
| C3 | QA-017 Narrator/Kontrast/Mehrmonitor, QA-019 echter Offline-Lauf | Claude + **Manuel am PC** |
| C4 | Deinstallations- und Update-/Rückfalltest (T-0008, IMP-16) | Claude |
| C5 | T-0005 Not-Stopp + Änderungsvorschau im Freigabe-Gate (IMP-19) | Claude |
| C6 | T-0006 KI-Modell offline mitliefern, Prompt-Injection-Filter (IMP-31) | Claude, **OK** Setup-Größe |
| C7 | Code-Signatur | **Manuel (Kauf)** |

### V – Verbesserungen 13–41 schließen (Lücken laut GAP_ANALYSIS §2)
Größte offene Blöcke: 18/19/20 (Automatisierungsstufen, Sandbox, Lernen durch Vorführen), 21 (Windows Production Worker), 26 (Offline-Sprache), 27 (mobile Web-App + QR), 29 (Benchmarks, Pi-Modus), 30 (Format-Matrix), 32/38 (Boot, Recovery, Dual-Boot), 37 (Wine/Gaming), 39 (Remote-Streaming, komplett offen), 40 (SDK, App-Pakete). Ein Auftrag je Verbesserung.

### G – Verbesserungen 42–96 (Hauptumfang)
1. **G1 Fundament:** Auf `main` liegt der Prompt-zu-Auftrag-Kern (`src/Trendo.Core/PromptAction/`, Berichte T-0042 und T-0043). Offen sind die Fachadapter je Studio (PRs #244–#258, #277) und die Einzelspezifikationen (#262, #273). Diese zuerst prüfen und übernehmen, dann 43 ONE Workspace als gemeinsames Muster abschließen.
2. **G2 Studios auf vorhandenen Modulen erweitern**, nicht neu bauen. Beispiele: 45 Office → SYS-10, 46 E-Mail → SYS-09, 48 Software → DevStudio, 49 Blender → Studio3D, 50 Game → GameStudio, 56 Engineering → `W6/Engineering*`, 57 Elektronik → `W6/Electronics*`, 60 3D-Scan → `W6/PointCloud*`, 61 CNC → `W6/Cnc*`, 63 Forensik → `W3/Forensics`, 64 Geo → `W6/Geo*`, 67 Robotik → `W6/Robotics*`, 75 Digital Twin → `W6/DigitalTwin*`, 84 Fusion → `W7/Fusion`.
3. **G3 Neue Studios** ohne Vorgänger (z. B. 69 Buch, 70 Smart Home, 76 Medizin-Visualisierung, 78 Recht, 88 Raumfahrt, 89 E-Commerce) – jeweils erst Abgrenzung gegen bestehende Module.
4. **Voraussetzungen von Manuel:** Funktionslisten für **95** (Architecture/BIM, Überschneidung mit 55 klären) und **96** (Lighting/Acoustics); Entscheidung zu **97**.

### P – Externe Produktionsbrücken
| # | Auftrag | Hinweis |
|---|---|---|
| P1 | Blender: echte `.blend` erzeugen/rendern über `tools/blender/` (T-0009) | Blender ist auf dem PC (Blender-MCP verbunden), aber nicht im `PATH` |
| P2 | Unreal: Projektprüfung per Editor-Python über `tools/unreal/` | `TRENDO_UNREAL_EDITOR`/`TRENDO_UPROJECT` setzen; Unreal-MCP war beim Audit nicht erreichbar |

### B – Edition B (Linux USB/SSD)
Persistenz, Datenträger-Installer, Recovery, Wine-Modus, Test auf echter Hardware (T-0011, IMP-32/38); ADR-0007 ist noch PROPOSED. **Manuel:** Partitionierungs-OK, SSD, Testhardware.

### A-Edition – Raspberry Pi 4
Ist: nicht begonnen. pi-gen-ARM64-Image mit Trendo-Desktop, QEMU-aarch64-Smoke, danach echter Pi (T-0010). **Manuel:** Pi + SD-Karte.

### I – Geräte und Remote
IMP-27 mobile Web-App mit QR-Kopplung + Remote-Not-Stopp (T-0012), IMP-33 Geräte, IMP-39 Remote/Streaming, Android-Gegenstelle. **Manuel:** Android-Gerät.

### R – Release
SBOM, Offline-Handbuch, Release-Kanal (Offene Entscheidung 10), förmliche Abnahme je Edition. Release durch Claude nur nach **OK**.

### T – Offene Aufträge in `tasks/`
✅ 2026-10-24 auf `main` mit Bericht: T-0202 Jeder Knopf wirkt (Allseiten-Audit optional, Restliste stummer Knöpfe offen) · T-0204 Spiele-Studio · T-0205 Video/Foto · T-0206 Webseite · T-0207 Fach-Studios · T-0208 Business · T-0209 Kleine Helfer · T-0210 Einrichtung · T-0211 PDF/Druck/Downloads · T-0212 Schrift & Farben · T-0214 Sicherung wiederherstellen · T-0218 Referenz-Design.
Offen: **T-0213** Studionamen – Vorschlagsliste liegt vor (14 Studios mit Handlungsbedarf), Entscheidung **Manuel**. PRs früherer Agenten zu denselben Themen (#263, #264, #270) sind damit überholt → schließen (O).

### O – Offene Pull Requests der früheren Agenten (37)
✅ 2026-10-25 geprüft (`docs/claude/PR_PRUEFUNG_2026-10-25.md`). Übernommen: #262, #268, #272, #273, #277 (mit #244–#258), #279 und #280, mit QA- und Security-Review. Danach übernommen: #271; GRUNDSATZ-01 aus #275 eingetragen. Offen: #278 (nur Teile), #248 zurückgestellt; der Rest wird auf GitHub geschlossen (Manuel).
Trendo OS wird nur noch mit Claude Code gebaut. Deshalb prüft Claude-QA jeden offenen PR einzeln: übernehmen (Merge nach **OK**) oder schließen. Danach werden die 154 Remote-Branches aufgeräumt.
| Gruppe | PRs | Vorschlag |
|---|---|---|
| Verbesserung 42 Fachadapter | #244, #245, #247, #249, #250, #251, #253, #254, #255, #256, #257, #258, #277 | prüfen, mit Tests auf Windows übernehmen |
| Fehlerbehebungen/Funktionen | #263 (T-0214), #264 (T-0210), #268, #270 (T-0213), #272 Begleiter-Sicherheit, #274, #278, #279, #280, #248, #252, #271 | einzeln prüfen und übernehmen |
| Spezifikation/Pläne (nur Doku) | #259, #260, #261, #262, #273 | Inhalt in Aufträge übernehmen, PR schließen |
| Werkzeug früherer Agenten | #236 (`.cursorignore`), #265, #267 (Merge-Skript), #269 (CI, Titel enthält Rechnernamen → umbenennen), #266, #275 | schließen oder für Claude neu aufsetzen |
| Alt | #104 `feat/files-workspace` | gegen `main` prüfen, wahrscheinlich überholt → schließen |

## Was nur Manuel liefern oder entscheiden kann
| Punkt | Quelle |
|---|---|
| Rechte-Patch anwenden | A1 |
| Merge-/Release-OKs, Register-Änderungen | CLAUDE.md, A3–A5 |
| Funktionslisten 95/96, Entscheidung 97 | G4 |
| JA/NEIN zu den neuen Claude-Design-Referenzen | D1 |
| Merge-OK je übernommenem PR | O |
| Code-Signatur (Kauf) | C7 |
| Hardware: Raspberry Pi 4 + SD, externe SSD, Android-Gerät, Mehrmonitor | B, A-Edition, I, C3 |
| Offene Entscheidungen 1–14 (`docs/spec/OFFENE_ENTSCHEIDUNGEN.md`), u. a. Linux-Basis, Vorinstallation, Lizenzen, Release-Ziel | – |
| Namen der 14 Studios aus T-0213 | `tasks/T-0213-namen-versprechen.bericht.md` |

## Empfohlene nächste Aufträge
1. A1 Rechte-Patch (Manuel) und T-0213 Namensentscheidung (Manuel).
2. O: offene PRs prüfen und übernehmen bzw. schließen – v. a. Verbesserung 42 (Fachadapter).
3. D3/D4: T-0100…T-0108 gegen den Ist-Stand prüfen und schließen; übrige Design-41-Ansichten mit den Referenzen vergleichen.
4. C2/C4/C5: Windows-Abnahme (Business am PC, Deinstallation/Update, Not-Stopp).
5. Danach G2/G3 Studio für Studio.
