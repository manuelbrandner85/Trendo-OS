# ADR-0003: Windows-Vertical-Slice vor Linux/Pi
**Status:** ACCEPTED (aus Spezifikation) · **Datum:** 2026-10-21 · **Quelle:** ROADMAP P1, Masterprompt §6

## Entscheidung
Erste lauffähige Phase ist ein kleiner Windows-Slice: Core-Dienst (nur localhost), minimale UI, Projektverwaltung mit Isolation, Studio-Registry mit ehrlichem Status, Approval-Gate, Audit-Log, Secret-Adapter (Windows Credential Manager), eine echte Integration (Browser/Explorer starten nach Freigabe), Launcher, Unit-Tests.

## Abnahme
QA-001, QA-003, QA-004, QA-015, QA-019 und Tastatur-Grundnavigation (Teil QA-017) mit Rohlogs; Artefakt-SHA-256; keine Systemänderung an Windows. Danach erst P2 (Linux-SSD) und P3 (Pi).
