# Bauplan V2 (Masterprompt MP-1.0) – Phasen 0–10
**Stand:** 2026-10-22 · ersetzt nicht `docs/BUILD_PLAN.md` (Wellen W0–W7), sondern ordnet sie den Phasen zu.
Legende Ort: **GB** = Grok Build am PC · **BOX** = Grok-Bot-Agent auf der Box · **HW** = echte Hardware nötig · **OK** = Freigabe Manuel nötig.

| Phase | Inhalt | Ist | Arbeitspakete | Ort |
|---|---|---|---|---|
| 0 | Bestandsaufnahme, Register, Lückenanalyse | IN_PROGRESS | Volltext Masterprompt einpflegen, Register/Gap neu abgleichen | BOX |
| 1 | Kern + Desktop (Abschnitt 25) | inhaltlich erfüllt (Edition C) | Startmenü/Fenster-Feinschliff (läuft, anderer Builder); förmliche Abnahme | GB · OK |
| 2 | PC-Verifikation aller TESTED_BOX-Module | offen (22 Module) | Lauf je Modul am PC, Logs in `reports/` | GB |
| 3 | Verbesserungen 13–40 (genehmigte) | UNBEKANNT | nach Volltext je Verbesserung Anforderungen + Tests anlegen | BOX → GB |
| 4 | Edition C Windows Release-Qualität | nahe | QA-017 Narrator/Multi-Monitor, QA-019 Offline-Lauf, Code-Signatur | GB · HW · OK (Kauf) |
| 5 | Edition B Linux USB/SSD | IN_PROGRESS | ADR-0007 entscheiden; Persistenz+LUKS; Installer auf Datenträger; Boot auf echter SSD | BOX (Image/QEMU) · HW · OK (Partitionierung) |
| 6 | Edition A Pi 4 Image | NOT_STARTED | pi-gen/Debian ARM64 Image, Companion als Dienst, QEMU-aarch64-Smoke; echter Pi + SD-Karte | BOX · HW |
| 7 | Geräteverbund real (Pi, Android) | IN_PROGRESS | Kopplung mit echten Geräten, Android-APK | GB · HW |
| 8 | Sicherheit, Recovery, Updates | teilweise | Wiederherstellung, Update-Signaturen, Pen-Test-Checkliste | BOX · GB |
| 9 | Release Candidates aller 3 Editionen | offen | RC-Builds, Testberichte | GB · OK |
| 10 | Abnahme & Veröffentlichung | offen | Abnahme Manuel, Releases veröffentlichen | OK |

## Top 10 Arbeitspakete (Priorität)
1. Volltext Masterprompt einpflegen, Register (Titel/Zahlen 13–40) und Lückenanalyse wiederholen – BOX
2. PC-Verifikation der 22 TESTED_BOX-Module via Grok Build – GB
3. Fertigstellen der 7 IN_PROGRESS-Module (CORE-01/04, SYS-01/12, X-08/09, LETTER) – BOX/GB
4. Genehmigte Verbesserungen 13–40 als Anforderungen + Tests anlegen und bauen – BOX → GB
5. Edition A: Pi-4-ARM64-Image (pi-gen) + QEMU-Smoke – BOX
6. Edition B: Persistenz + Verschlüsselung + Datenträger-Installer, ADR-0007 – BOX, danach HW
7. QA-017 Narrator/Multi-Monitor + QA-019 echter Offline-Lauf – GB + Manuel
8. Grok-Build-Workflow produktiv (erste PRs von Grok Build, Logs) – GB (Integrator)
9. Android-Gegenstelle als APK + echte Kopplung – BOX/HW
10. Code-Signatur – OK (Kauf)
