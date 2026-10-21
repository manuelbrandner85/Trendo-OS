# ADR-0006: Ehrliches Statusmodell und Nachweispflicht
**Status:** ACCEPTED (aus Spezifikation) · **Datum:** 2026-10-21 · **Quelle:** SYSTEMBIBEL §7, QA_ABNAHME.md

## Entscheidung
Status: `PROPOSED → APPROVED_SPEC → PLANNED → IN_PROGRESS → IMPLEMENTED → TESTED → ACCEPTED → RELEASED`, plus `FAILED`/`BLOCKED` mit Grund. Teststatus: `NOT_RUN`/`PASS`/`FAIL`/`BLOCKED`.
Ein Wechsel nach `IMPLEMENTED`/`TESTED` braucht: Dateipfad, Commit, Testkommando, Umgebung, Rohlog unter `reports/`, SHA-256/Build-ID. `requirements.yaml` wird aus den CSVs erzeugt; `scripts/validate_requirements.py` verhindert aktuell jeden Fortschrittsstatus ohne Nachweis (in P1 um Nachweisprüfung zu erweitern).
