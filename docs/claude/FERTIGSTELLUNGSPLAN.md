# Trendo OS – Fertigstellungsplan (Claude Code)

**Stand:** 2026-10-23 · **Grundlage:** `requirements.yaml`, `docs/spec/GAP_ANALYSIS.md`, `docs/BUILD_PLAN_V2.md`, `docs/spec/design-41/GAP_DESIGN_41.md`, `handoff/…bis_94.md`, Audit `reports/claude/AUDIT_CLAUDE_MIGRATION_2026-10-23.md`
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
| Verbesserungen 13–41 | 25 genehmigte (≈ 340 Funktionen) | 0 einzeln erfasst | nicht im Register, Teilfunktionen vorhanden (GAP_ANALYSIS §2) |
| Verbesserungen 42–96 | 54 genehmigte (≈ 2 400 Funktionen) | 0 | nur APPROVED_SPEC; 95/96 ohne Funktionsliste; 97 offen |
| Editionen | 3 | 0 abgenommen | C am weitesten, B bootet in QEMU, A nicht begonnen |
| Build/Tests | 629 Tests grün | – | 18 Compiler-Warnungen |

## Arbeitspakete in Reihenfolge

### A – Fundament für den reinen Claude-Betrieb
| # | Auftrag | Wer/Freigabe |
|---|---|---|
| A1 | Rechte-Patch `docs/claude/M2_RECHTE_PATCH.diff` anwenden (Push `main`/`--force`) | **Manuel** |
| A2 | Claude Code auf ≥ 2.1.295 aktualisieren (Hook blockiert dann auch bei Ausfall) | **Manuel** |
| A3 | `claude/godmode-2` nach `main` mergen | Claude nach **OK** |
| A4 | M4: Verbesserungen 42–97 in `decisions.yaml`, `source_sha256` des Masterprompts erneuern | Claude nach **OK** (Register) |
| A5 | T-0007: Verbesserungen 13–96 als einzelne Requirements (`IMP-xx-nn`) in `requirements.yaml` + Generator, damit Fortschritt messbar wird | Claude, Register-OK |
| A6 | 18 Compiler-Warnungen beheben | Claude |

### D – Design 41 komplett mit Claude
| # | Auftrag | Wer/Freigabe |
|---|---|---|
| D1 | Design-Referenzen neu mit Claude erstellen: die 10 Ansichten + 2 Atlanten aus `docs/spec/design-41/referenzen/` als HTML-/SVG-Mockups bauen, im Browser rendern und als PNG ablegen; alte Bilder ersetzen, `REFERENZEN_MANIFEST.json` und `SHA256SUMS` erneuern | Claude, danach **JA/NEIN Manuel** |
| D2 | Hintergrundbilder (`Assets/wallpaper*`) neu, von Claude erzeugt (SVG/Shader-Verlauf, lizenzfrei), `THIRD_PARTY.md` anpassen | Claude |
| D3 | Konflikte aus `GAP_DESIGN_41.md` entscheiden (u. a. „Design belassen“ ↔ Ref 01, Studio-Namen, Logo, Dock-Apps) | **Manuel** |
| D4 | T-0100 … T-0106: Baseline, Tokens, responsives Layout, Desktop, Startmenü/KI, Studiokarten, Briefstudio | Claude |
| D5 | T-0108 Prüfstopp: Review-Build, JA/NEIN vor Rollout auf alle Studios | **Manuel** |
| D6 | T-0107 Fenster/Einstellungen/Leistungsprofile, danach Rollout UI41-7 | Claude |
| D7 | T-0109 Logo – nur nach Logo-Freigabe | **Manuel** |

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
1. **G1 Fundament:** 42 Prompt-to-Action (Vertical Slice an einem Studio, z. B. Briefe, über `IntelligenceCore`, `PromptLibrary`, `ApprovalGate`) und 43 ONE Workspace als gemeinsames Muster.
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

## Was nur Manuel liefern oder entscheiden kann
| Punkt | Quelle |
|---|---|
| Rechte-Patch anwenden, Claude-Update | A1, A2 |
| Merge-/Release-OKs, Register-Änderungen | CLAUDE.md, A3–A5 |
| Funktionslisten 95/96, Entscheidung 97 | G4 |
| Design-41-Konflikte, Logo, JA/NEIN Prüfstopp | D3, D5, D7 |
| Code-Signatur (Kauf) | C7 |
| Hardware: Raspberry Pi 4 + SD, externe SSD, Android-Gerät, Mehrmonitor | B, A-Edition, I, C3 |
| Offene Entscheidungen 1–14 (`docs/spec/OFFENE_ENTSCHEIDUNGEN.md`), u. a. Linux-Basis, Vorinstallation, Lizenzen, Release-Ziel | – |
| Optionale Cloud-KI „xAI“ in der App behalten oder entfernen | `MainWindow.axaml.cs` |

## Empfohlene nächste Aufträge
1. A1 + A2 (Manuel), dann A3 Merge.
2. A5 Register (ohne messbaren Fortschritt kann niemand „fertig“ nachweisen).
3. D1 Design-Referenzen mit Claude, dann D4 Design-Umsetzung bis zum Prüfstopp D5.
4. Parallel C2/C4/C5 (Windows-Abnahme), danach G1 als Muster für alle Studios.
