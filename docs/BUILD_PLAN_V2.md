# Bauplan V2 (Masterprompt MP-1.0, Abschnitt 21) – Phasen 0–10
**Stand:** 2026-10-22 · ordnet die Wellen W0–W7 (`docs/BUILD_PLAN.md`) den Phasen zu. Kein Status hochgesetzt.
Ort: **GB** = Claude Code am PC (Standard für Code laut MP §2.2) · **BOX** = Cloud-Agent (nur Image-/QEMU-/Doku-Arbeit) · **HW** = echte Hardware · **OK** = Freigabe Manuel.

| Phase (MP §21) | Ist | Offene Arbeit | Ort |
|---|---|---|---|
| 0 Entwicklungsumgebung | IN_PROGRESS | Claude-Code-Workflow (#195/#197) produktiv, T-0002 abschließen, Register (diese PR) | GB · BOX |
| 1 Systemarchitektur | weitgehend | 18 Ebenen §15 als Architekturkarte; Betriebssystemadapter Pi; Studio-SDK-Paket | GB |
| 2 Desktop | IMPLEMENTED (C) | Fenster-Feinschliff (läuft), Deinstallationstest, Abnahme | GB · OK |
| 3 Standardprogramme | IMPLEMENTED/PC_VERIFIED teilweise | 22 TESTED_BOX-Module am PC prüfen; One-Setup-Entscheidung (mitliefern vs. nachinstallieren) | GB · OK |
| 4 Lokale KI & Automatisierung | teilweise | IMP-31 Modell offline mitliefern, Prompt-Injection-Schutz, Offline-Sprache; IMP-18/19/20 Automatisierungsstufen, Sandbox-Vorschau, Not-Stopp | GB |
| 5 Studioausbau | IMPLEMENTED | Studio-Abnahmetests (IMP-13), CORE-12 40 Kategorien, IMP-17/30 | GB |
| 6 Externe Produktionsbrücken | gering | Blender `.blend`-Nachweis, UE5 Python/Editor-Automation, Windows Production Worker (IMP-21) | GB (+ installiertes Blender/UE5) |
| 7 Geräte & Remote | IN_PROGRESS | IMP-27 mobile Web-App + QR, IMP-33 Geräte, IMP-39 Remote/Streaming | GB · HW |
| 8 Betriebssystembuilds | B IN_PROGRESS, A NOT_STARTED | Edition A pi-gen ARM64 `.img.xz` + QEMU-aarch64; Edition B Persistenz/Installer/Recovery/Wine; IMP-32/38 | BOX (Build/QEMU) · HW · OK (Partitionierung, SD-Karte) |
| 9 Integrations- & QA-Tests | teilweise | QA-017, QA-019, Neuinstallation auf Zielhardware (IMP-24), Benchmarks (IMP-29), Update-Tests (IMP-16) | GB · HW · Manuel |
| 10 Fertigstellung & Veröffentlichung | offen | Code-Signatur, SBOM, Handbuch, Release-Kanäle (IMP-16/28), Abnahme | OK (Kauf, Veröffentlichung) |

## Top 10 Claude-Code-Aufträge
| # | Auftrag | Phase | Ort | Freigabe |
|---|---|---|---|---|
| T-0003 | 22 TESTED_BOX-Module am PC verifizieren, Logs in reports/ | 3 | GB | – |
| T-0004 | IMP-13: Test „keine toten Schaltflächen“ (jede Kachel/Taste hat Handler) + Studio-Abnahmetests | 5 | GB | – |
| T-0005 | IMP-19: Not-Stopp für alle lokalen Automationen + Änderungsvorschau im Freigabe-Gate | 4 | GB | – |
| T-0006 | IMP-31: KI-Modell offline im Setup mitliefern, Prompt-Injection-Filter, echter Offline-Lauf (QA-019) | 4 | GB | Größe Setup (OK) |
| T-0007 | Verbesserungen 13–40 als Requirements (IMP-xx-nn) in requirements.yaml + Generator | 0 | GB | – |
| T-0008 | Deinstallations- und Update-/Rollback-Test Edition C (IMP-16) | 9 | GB | – |
| T-0009 | Blender-Brücke: echte `.blend` erzeugen/rendern; UE5-Projektprüfung per Editor-Python | 6 | GB | – |
| T-0010 | Edition A: pi-gen ARM64-Image mit Trendo-Desktop, QEMU-aarch64-Smoke | 8 | BOX/GB | echter Pi+SD (HW) |
| T-0011 | Edition B: Datenträger-Installer + Recovery, Persistenz-Test, ADR-0007 | 8 | BOX/GB | Partitionierung (OK), HW |
| T-0012 | IMP-27: mobile Trendo-Web-App mit QR-Kopplung + Remote-Not-Stopp | 7 | GB | Android (HW) |
