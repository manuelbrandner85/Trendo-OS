# Trendo OS – Testmatrix und Abnahmekriterien

**Teststatus beim Erstellen dieses Dokuments: NICHT DURCHGEFÜHRT.** Erfundene `PASSED`-Werte sind untersagt. Für jeden Test sind Datum/Zielhardware, Build-ID, Kommando/Testschritte, Rohlogs, Fehler und Nachweisreferenz zu erfassen.

| ID | Testkategorie | Pi+PC | Linux USB/SSD | Windows-App | Aktueller Status |
|---|---|---|---|---|---|
| QA-001 | Start und sauberes Beenden | Ja | Ja | Ja | NOT_RUN |
| QA-002 | Erste Einrichtung und Ready-to-Run Essentials | Ja, adaptiert | Ja | Ja, adaptiert | NOT_RUN |
| QA-003 | Projektisolation über Datei/API/Agent | Ja | Ja | Ja | NOT_RUN |
| QA-004 | Berechtigungen für Programme, Adminaktionen und Agenten | Ja | Ja | Ja | NOT_RUN |
| QA-005 | Forensik-Aufbewahrungsdauer ≤120h + Cacheindex-Löschung | Ja | Ja | Ja | NOT_RUN |
| QA-006 | Browser-Profiltrennung, Downloadscan und Freigaben | Auf PC | Ja | Ja | NOT_RUN |
| QA-007 | Darkweb READ-ONLY verhindert Formulare, Uploads, Käufe und persistente Downloads | Auf PC | Ja | Ja | NOT_RUN |
| QA-008 | Lokale deutsche Übersetzung ohne externe Weitergabe | Auf PC | Ja | Ja | NOT_RUN |
| QA-009 | Hardware- und Treibererkennung | Pi+PC separat | Ja | Ja | NOT_RUN |
| QA-010 | Network shares / VPN / Remote scoped permission | Ja | Ja | Ja | NOT_RUN |
| QA-011 | E-Mail-Entwurf ohne unautorisierten Versand | Ja | Ja | Ja | NOT_RUN |
| QA-012 | Office echte DOCX/XLSX/PPTX/PDF-Erstellung und Checks | Auf PC | Ja | Ja | NOT_RUN |
| QA-013 | 3D Blender real editierbarer Mesh, Export und Import | Auf PC | Falls geeignet | Falls geeignet | NOT_RUN |
| QA-014 | Game Engine Vertical Slice und Build | Auf PC | Falls geeignet | Falls geeignet | NOT_RUN |
| QA-015 | Studio-Kapazitätsanzeige bei fehlendem Tool korrekt BLOCKED | Ja | Ja | Ja | NOT_RUN |
| QA-016 | Recovery bei Update/Installationsabbruch | Ja | Ja | Ja | NOT_RUN |
| QA-017 | Keyboard/Screenreader/Kontrast/Multi-Monitor/Reduced Motion | Ja | Ja | Ja | NOT_RUN |
| QA-018 | Windows intern bleibt bei USB-Start/Hostmodus unmodifiziert | Auf PC | Ja | Ja | NOT_RUN |
| QA-019 | Offline-Betrieb bei fehlender Internetverbindung | Ja | Ja | Ja | NOT_RUN |
| QA-020 | SHA256, Release-Lizenzmanifest, SBOM und User-Anleitung vorhanden | Ja | Ja | Ja | NOT_RUN |
| QA-021 | Business-Studio: beide Modi, Quellenvergleich, Businessplan und echte GO/NO_GO-Sperre | Auf PC | Soweit unterstützt | Ja | NOT_RUN |
| QA-022 | Business-Studio: Sandbox-Payments, Rollen-/Mandantentrennung, echte E2E-Prozesse | Auf PC | Soweit unterstützt | Ja | NOT_RUN |
| QA-023 | Business-Studio: ungenehmigte Domainkäufe, Outreach, Live-Deployments blockiert | Ja | Ja | Ja | NOT_RUN |
| QA-024 | Business-Studio: dokumentierter Staging-Pilot und prüfbarer Quellcode/Übergabe | Auf PC | Soweit unterstützt | Ja | NOT_RUN |
| QA-025 | Letter-Studio: bearbeitbare DOCX/ODT und gerendertes PDF mit Layouttest | Auf PC | Ja | Ja | NOT_RUN |
| QA-026 | Letter-Studio: unklare OCR-/Quellen-/Fristenangaben verhindern fingierte Tatsachen | Auf PC | Ja | Ja | NOT_RUN |
| QA-027 | Letter-Studio: Versand, eFiling und Signatur ohne Approval technisch blockiert | Auf PC | Ja | Ja | NOT_RUN |
| QA-028 | Letter-Studio: Projekt-/Briefkopftrennung, Anlagen und 120h-Forensik-TTL | Auf PC | Ja | Ja | NOT_RUN |

## Minimale Qualitäts-Gates
**Build:** erfolgreich gebaute real vorhandene Artefakte.
**Integrität:** Versions- und SHA-256-Zuordnung zu getesteten Artefakten.
**Sicherheit:** keine reproduzierbare Umgehung zwingender Approval-/Isolation-Gates.
**Daten:** sichere Projekttrennung, deterministisches Backup/Restore, richtige Lösch-/Retentionregeln.
**Integration:** mindestens ein repräsentativer End-to-End-Use-Case je ausgeliefertem Studio.
**UX:** Start, Navigation, Tastaturbedienung, Statuswahrheit und eindeutige Freigabedialoge.
**Plattform:** echte Testhardware; virtuelle Maschinen allein genügen nicht als Release-Nachweis.
**Abnahme:** explizite Nutzerfreigabe nach Einsicht offener Probleme; keine automatische produktive Veröffentlichung.

## Nachweisartefakte pro Test
`test_id`, `requirements`, `build_sha`, `platform`, `os_version`, `cpu`, `gpu`, `ram`, `run_at`, `steps`, `expected`, `actual`, `status`, `logs_path`, `screenshots_paths`, `reviewer`, `next_action`.

## Detaillierte Business-Studio-Tests
Vollständiger zusätzlicher Testkatalog: `BUSINESS_QA_MATRIX.csv`, beginnend **ausschließlich mit `NOT_RUN`**. Zahlungen nur in Sandbox; für jedes reale Geschäft gesonderte Rechts-, Kosten-, Hosting- und Live-Freigabe.

## Detaillierte Letter-Studio-Tests
Zusätzlicher Katalog `LETTER_QA_MATRIX.csv` (mindestens 20 benannte Tests), vollständig initial `NOT_RUN`. Spezifische Anforderungen und nachprüfbare Einzelkriterien unter `LETTER_REQUIREMENTS_25.csv`. Keine fingierte Versand-/Einreichungsbestätigung.


## v2.0 – zusätzliche Claude-Code-spezifische Abnahmekriterien
Die Claude-Code-spezifischen Abnahmekriterien stehen in `docs/claude/QA_RELEASE_MATRIX.md`. Alle starten auf `NOT_RUN`. Vor dem ersten Trendo-Release müssen u.a. lokale Claude-Code-Codeänderung, Prüfung von Git-Diffs/Tests, sichere Auftragsübergabe an Claude Code, Cloud-Datenisolation, echte Blender-/UE5-Adapter und Negativtests für nicht genehmigte externe/systemkritische Aktionen nachgewiesen sein. Kein automatischer Erfolg durch Vorhandensein dieses Pakets.
