# Vorschlag: Statuswechsel in requirements.yaml (T-0687)

**Nur ein Vorschlag – `requirements.yaml` wurde nicht geändert.** Die Entscheidung trifft der Nutzer. Erzeugt mit `python tools/echtlauf/requirements_delta.py --echtlauf reports/echtlauf/2026-11-16`.

Regel (CLAUDE.md): `TESTED`/`PC_VERIFIED` nur mit Rohlog + SHA-256 unter `reports/`. Ein Beleg, der älter ist als die letzte Änderung am Code des Studios, deckt den heutigen Stand nicht. Ein Test mit ersetztem Werkzeug (`*Override`) ist kein Ende-zu-Ende-Nachweis.

Datum „Code geändert“ = letzter Commit an den Quelldateien der Programme des Studios laut `docs/programme/katalog.json`; Datum „Beleg“ = letzter Commit der Belegdatei.

## Zurückstufen vorgeschlagen (37)

Vorschlag: `test_status` → `NOT_RUN` (bzw. bei entfernten Studios Eintrag als entfernt kennzeichnen), bis ein neuer Beleg vorliegt.

| Modul | Name | heute | Beleg | Grund |
|---|---|---|---|---|
| CORE-02 | Trendo Intelligence Core | IMPLEMENTED / PC_VERIFIED | `reports/pc-t0003/UEBERSICHT.md` | Beleg vom 2026-10-24, Code des Studios zuletzt geändert am 2026-11-14; kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: 12) |
| CORE-05 | Ultimate 3D Creation Studio 2.0 | IMPLEMENTED / PC_VERIFIED | `reports/pc-0.9.2/tests-pc-4e6b81d.txt` | Beleg vom 2026-10-21, Code des Studios zuletzt geändert am 2026-11-14 |
| CORE-06 | Ultimate Game Development Studio 2.0 | IMPLEMENTED / PC_VERIFIED | `reports/pc-0.9.2/tests-pc-4e6b81d.txt` | Beleg vom 2026-10-21, Code des Studios zuletzt geändert am 2026-11-14 |
| CORE-07 | Ultimate App & Software Development Studio 2.0 | IMPLEMENTED / PC_VERIFIED | `reports/pc-0.9.2/tests-pc-4e6b81d.txt` | Beleg vom 2026-10-21, Code des Studios zuletzt geändert am 2026-11-16 |
| CORE-08 | Ultimate Creative & Media Studio 2.0 | IMPLEMENTED / PC_VERIFIED | `reports/pc-0.9.2/tests-pc-4e6b81d.txt` | Beleg vom 2026-10-21, Code des Studios zuletzt geändert am 2026-11-15 |
| CORE-09 | Vision & Robotics Control Studio 2.0 | IMPLEMENTED / PC_VERIFIED | `reports/pc-t0003/UEBERSICHT.md` | Studio ist aus der Liste entfernt (StudioCatalog.Removed) – nicht mehr angeboten; Beleg vom 2026-10-24, Code des Studios zuletzt geändert am 2026-11-14; kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: 1) |
| CORE-12 | Ultimate Prompt & Masterprompt Studio 2.0 | IMPLEMENTED / PC_VERIFIED | `reports/pc-t0003/UEBERSICHT.md` | Beleg vom 2026-10-24, Code des Studios zuletzt geändert am 2026-11-05; kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: 4) |
| SYS-02 | Ultimate Desktop Utilities & Media Center | IMPLEMENTED / PC_VERIFIED | `reports/pc-0.9.3/README.md` | Beleg vom 2026-10-21, Code des Studios zuletzt geändert am 2026-11-15 |
| SYS-03 | Ultimate Browser Internet & Download Center | IMPLEMENTED / PC_VERIFIED | `reports/pc-0.9.3/README.md` | Beleg vom 2026-10-21, Code des Studios zuletzt geändert am 2026-11-14 |
| SYS-09 | Communication Mail & Collaboration Center | IMPLEMENTED / PC_VERIFIED | `reports/pc-t0003/UEBERSICHT.md` | Beleg vom 2026-10-24, Code des Studios zuletzt geändert am 2026-11-14 |
| SYS-10 | Office Documents & Productivity Center | IMPLEMENTED / PC_VERIFIED | `reports/pc-0.9.3/README.md` | Beleg vom 2026-10-21, Code des Studios zuletzt geändert am 2026-11-16 |
| SYS-11 | Ready-to-Run & Software Integration | IMPLEMENTED / PC_VERIFIED | `reports/pc-0.9.3/README.md` | Beleg vom 2026-10-21, Code des Studios zuletzt geändert am 2026-11-04 |
| SYS-12 | Adaptive Interface Accessibility & Personalization | IN_PROGRESS / PC_VERIFIED | `reports/pc-0.9.3/README.md` | Beleg vom 2026-10-21, Code des Studios zuletzt geändert am 2026-11-14; kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: 1) |
| SYS-13 | System Architecture Integration & Release Quality | IMPLEMENTED / PC_VERIFIED | `reports/pc-0.9.3/README.md` | Beleg vom 2026-10-21, Code des Studios zuletzt geändert am 2026-11-13; kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: 46) |
| SPEC-01 | Architecture & Interior 2.0 | IMPLEMENTED / PC_VERIFIED | `reports/pc-0.9.2/tests-pc-4e6b81d.txt` | Beleg vom 2026-10-21, Code des Studios zuletzt geändert am 2026-11-14; kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: 2) |
| SPEC-02 | Electronics & PCB 2.0 | IMPLEMENTED / PC_VERIFIED | `reports/pc-0.9.2/tests-pc-4e6b81d.txt` | Beleg vom 2026-10-21, Code des Studios zuletzt geändert am 2026-11-14; kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: 0) |
| SPEC-03 | Engineering & Simulation 2.0 | IMPLEMENTED / PC_VERIFIED | `reports/pc-0.9.2/tests-pc-4e6b81d.txt` | Beleg vom 2026-10-21, Code des Studios zuletzt geändert am 2026-11-09; kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: 3) |
| SPEC-04 | VR/AR/MR 2.0 | IMPLEMENTED / PC_VERIFIED | `reports/pc-0.9.2/tests-pc-4e6b81d.txt` | Studio ist aus der Liste entfernt (StudioCatalog.Removed) – nicht mehr angeboten; Beleg vom 2026-10-21, Code des Studios zuletzt geändert am 2026-11-14; kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: 2) |
| SPEC-05 | Science & Research 2.0 | IMPLEMENTED / PC_VERIFIED | `reports/pc-0.9.2/tests-pc-4e6b81d.txt` | Beleg vom 2026-10-21, Code des Studios zuletzt geändert am 2026-11-02; kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: 1) |
| SPEC-06 | Education & Training 2.0 | IMPLEMENTED / PC_VERIFIED | `reports/pc-0.9.2/tests-pc-4e6b81d.txt` | Beleg vom 2026-10-21, Code des Studios zuletzt geändert am 2026-11-14; kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: 0) |
| SPEC-08 | Fashion & Product 2.0 | IMPLEMENTED / PC_VERIFIED | `reports/pc-t0003/UEBERSICHT.md` | Beleg vom 2026-10-24, Code des Studios zuletzt geändert am 2026-11-14; kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: 5) |
| SPEC-09 | 3D Scanning & Reconstruction 2.0 | IMPLEMENTED / PC_VERIFIED | `reports/pc-t0003/UEBERSICHT.md` | Beleg vom 2026-10-24, Code des Studios zuletzt geändert am 2026-11-02; kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: 8) |
| SPEC-10 | Digital Twin 2.0 | IMPLEMENTED / PC_VERIFIED | `reports/pc-t0003/UEBERSICHT.md` | Beleg vom 2026-10-24, Code des Studios zuletzt geändert am 2026-11-14; kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: 0) |
| SPEC-11 | Business & Finance 2.0 | IMPLEMENTED / PC_VERIFIED | `reports/pc-0.9.2/tests-pc-4e6b81d.txt` | Beleg vom 2026-10-21, Code des Studios zuletzt geändert am 2026-11-14; kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: 0) |
| SPEC-13 | Maps & Geospatial 2.0 | IMPLEMENTED / PC_VERIFIED | `reports/pc-t0003/UEBERSICHT.md` | Beleg vom 2026-10-24, Code des Studios zuletzt geändert am 2026-11-14; kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: 2) |
| SPEC-14 | Manufacturing & CNC 2.0 | IMPLEMENTED / PC_VERIFIED | `reports/pc-t0003/UEBERSICHT.md` | Beleg vom 2026-10-24, Code des Studios zuletzt geändert am 2026-11-14; kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: 1) |
| SPEC-15 | Broadcast & Streaming 2.0 | IMPLEMENTED / PC_VERIFIED | `reports/pc-t0003/UEBERSICHT.md` | Beleg vom 2026-10-24, Code des Studios zuletzt geändert am 2026-11-15; kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: 7) |
| SPEC-16 | Transport & Vehicle 2.0 | IMPLEMENTED / PC_VERIFIED | `reports/pc-t0003/UEBERSICHT.md` | Studio ist aus der Liste entfernt (StudioCatalog.Removed) – nicht mehr angeboten; Beleg vom 2026-10-24, Code des Studios zuletzt geändert am 2026-11-14; kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: 0) |
| X-01 | Universal Studio Fusion & Autonomous Production | IMPLEMENTED / PC_VERIFIED | `reports/pc-0.9.2/tests-pc-4e6b81d.txt` | Beleg vom 2026-10-21, Code des Studios zuletzt geändert am 2026-11-14 |
| X-03 | Visual Builder & No-Code | IMPLEMENTED / PC_VERIFIED | `reports/pc-0.9.2/tests-pc-4e6b81d.txt` | Beleg vom 2026-10-21, Code des Studios zuletzt geändert am 2026-11-14; kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: 7) |
| X-04 | Extension Marketplace & Studio Evolution | IMPLEMENTED / PC_VERIFIED | `reports/pc-0.9.2/tests-pc-4e6b81d.txt` | Beleg vom 2026-10-21, Code des Studios zuletzt geändert am 2026-11-14; kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: 0) |
| X-05 | Free AI Discovery & Fallback | IMPLEMENTED / PC_VERIFIED | `reports/pc-t0003/UEBERSICHT.md` | Beleg vom 2026-10-24, Code des Studios zuletzt geändert am 2026-11-14; kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: 6) |
| X-06 | GitHub Open-Source Integration | IMPLEMENTED / PC_VERIFIED | `reports/pc-0.9.2/tests-pc-4e6b81d.txt` | Beleg vom 2026-10-21, Code des Studios zuletzt geändert am 2026-11-14 |
| X-07 | Global Free Asset Discovery | IMPLEMENTED / PC_VERIFIED | `reports/pc-0.9.2/tests-pc-4e6b81d.txt` | Beleg vom 2026-10-21, Code des Studios zuletzt geändert am 2026-11-14; kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: 4) |
| X-10 | Legal Clean-Room Software Reconstruction | IMPLEMENTED / PC_VERIFIED | `reports/pc-0.9.2/tests-pc-4e6b81d.txt` | Beleg vom 2026-10-21, Code des Studios zuletzt geändert am 2026-11-14; kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: 1) |
| X-11 | Universal Creation & Productivity Suite | IMPLEMENTED / PC_VERIFIED | `reports/pc-0.9.2/tests-pc-4e6b81d.txt` | Beleg vom 2026-10-21, Code des Studios zuletzt geändert am 2026-11-15; kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: 0) |
| STUDIO-ONLINE-BUSINESS-01 | Ultimate Online Business Creation & Innovation Studio GODMODE (both modes) | IMPLEMENTED / PC_VERIFIED | `reports/pc-0.9.2/tests-pc-4e6b81d.txt` | Beleg vom 2026-10-21, Code des Studios zuletzt geändert am 2026-10-27; kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: 0) |

## Beibehalten, aber mit Hinweis (16)

Beleg ist nicht älter als der Code, aber die Programme haben keinen Test mit echtem Werkzeug.

| Modul | Name | heute | Beleg | Hinweis |
|---|---|---|---|---|
| CORE-01 | Intelligent Desktop OS | IN_PROGRESS / PC_VERIFIED | `reports/w1/pc/README.md` | kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: 7) |
| CORE-03 | AI Agent Command Center – 12 roles | IMPLEMENTED / PC_VERIFIED | `reports/pc-t0003/UEBERSICHT.md` | kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: 1) |
| CORE-04 | Pi/PC/Android Fusion Engine | IN_PROGRESS / PC_VERIFIED | `reports/pc-0.9.1/tests-pc-7092ce1.txt` | kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: 0) |
| CORE-10 | Knowledge Memory & Self Improvement Core 2.0 | IMPLEMENTED / PC_VERIFIED | `reports/pc-t0003/UEBERSICHT.md` | kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: 4) |
| CORE-11 | Security Privacy Recovery Fortress | IMPLEMENTED / PC_VERIFIED | `reports/pc-t0003/UEBERSICHT.md` | kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: 3) |
| SYS-01 | Triple Mode OS GODMODE | IN_PROGRESS / PC_VERIFIED | `reports/pc-0.9.1/tests-pc-7092ce1.txt` | kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: 0) |
| SYS-04 | Ultimate App Installation & System Control Center | IMPLEMENTED / PC_VERIFIED | `reports/pc-t0003/UEBERSICHT.md` | kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: 4) |
| SYS-05 | Universal Automation & Workflow Engine | IMPLEMENTED / PC_VERIFIED | `reports/pc-t0003/UEBERSICHT.md` | kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: 3) |
| SYS-06 | Ultimate Network File Sharing & Remote Workspace | IMPLEMENTED / PC_VERIFIED | `reports/pc-t0003/UEBERSICHT.md` | kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: 1) |
| SYS-07 | Ultimate Hardware & Device Recognition | IMPLEMENTED / PC_VERIFIED | `reports/pc-t0003/UEBERSICHT.md` | kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: 2) |
| SYS-08 | Deep Web & Dark Web READ-ONLY Research Studio | IMPLEMENTED / PC_VERIFIED | `reports/pc-t0003/UEBERSICHT.md` | kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: 3) |
| SPEC-12 | Cybersecurity & Forensics 2.0 | IMPLEMENTED / PC_VERIFIED | `reports/pc-t0003/UEBERSICHT.md` | kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: 2) |
| X-02 | Zero Touch Setup & Self Healing | IMPLEMENTED / PC_VERIFIED | `reports/pc-t0003/UEBERSICHT.md` | kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: 0) |
| X-08 | Mobile OS Bridge | IN_PROGRESS / PC_VERIFIED | `reports/pc-0.9.1/tests-pc-7092ce1.txt` | kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: 7) |
| X-09 | System Resource & Studio Manager | IN_PROGRESS / PC_VERIFIED | `reports/w1/pc/README.md` | kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: 2) |
| STUDIO-LETTER-01 | Ultimate Letter Business & Legal Correspondence Studio GODMODE | IN_PROGRESS / PC_VERIFIED | `reports/w1/pc/README.md` | kein Test mit echtem Fremdwerkzeug für die Programme des Studios (ersetzt: 3) |

## Beibehalten ohne Befund (0)

–

## Hochstufen denkbar (2)

Nur wo die Echtlauf-Station das Werkzeug des Studios heute ECHT-GRÜN belegt hat (Rohlog + SHA256SUMS im Beleg-Ordner). Vorschlag: `test_status` → `PC_VERIFIED` für den belegten Fachlauf – `implementation_status` bleibt, wie er ist.

| Modul | Name | heute | Beleg | Ergebnis |
|---|---|---|---|---|
| STUDIO-MUSIK-01 | Trendo Musik (Lieder mit ACE-Step) | IN_PROGRESS / NOT_RUN | `reports/echtlauf/2026-11-16/acestep.log` | ECHT-GRÜN |
| STUDIO-IMAGEAI-01 | Bilder mit KI | IN_PROGRESS / NOT_RUN | `reports/echtlauf/2026-11-16/sdcli.log` | ECHT-GRÜN |

## Weitere Befunde (von Hand geprüft, 16.11.2026)

- **Kaputte Umlaute:** `requirements.yaml` Zeilen 538, 540 und 545 auf dem Zweig `claude/T-0674-buero-etappe` (Audit-Stand) enthalten „Arbeiten & BÃ¼ro“, „â€žJa, weiterâ€œ“ und „Formulare ausfÃ¼llen“ (UTF-8 doppelt kodiert). Auf dem Integrationszweig `claude/T-0685-b1-integration` sind diese Zeilen noch nicht vorhanden – beim Zusammenführen der Büro-Etappe reparieren („Büro“, „„Ja, weiter““, „ausfüllen“).
- **IMP-42 (Prompt-to-Action)** steht auf `IN_PROGRESS / NOT_RUN`, obwohl `PromptActionTests` (88 Tests) am 16.11.2026 grün liefen. Hochstufen auf `TESTED` erst mit Rohlog + SHA-256 unter `reports/` (z. B. TRX-Datei des Laufs); diese Station legt dafür keinen Beleg an.
- **CORE-06** (Spiele) bleibt in der Zurückstufungsliste, obwohl Godot, Unity und Node heute ECHT-GRÜN liefen: Unreal (Kernversprechen des Studios) ist nicht automatisch belegt. Teil-Hochstufung nur, wenn der Nutzer den Godot-/Unity-Beleg für ausreichend hält.

## Grenzen dieses Vorschlags

- „Code geändert“ zählt jeden Commit an den Quelldateien – auch rein optische. Der Nutzer entscheidet je Eintrag, ob die Änderung den Beleg entwertet.
- „kein Test mit echtem Fremdwerkzeug“ beruht auf der Zuordnung Test → Programm im Programmkatalog. Tests, deren Anzeigename weder die Studio-Kennung noch die Seite nennt (z. B. „LETTER-012: Echte Tesseract-Texterkennung“ für STUDIO-LETTER-01), fehlen dort – der Hinweis kann deshalb zu streng sein.
- Der Echtlauf-Beleg zeigt einen Fachlauf je Werkzeug, nicht jede Funktion des Studios.
