---
name: trendo-jobs
description: Persistente Jobs für das bestehende Trendo-OS-Projekt. Bei passenden Aufträgen verwenden.
---
# Persistente Jobs

## Zweck
SQLite-Jobdaten prüfen; STATUS_READY -> RUNNING nur bei echtem Start, Run/Resume nur auf expliziten Benutzeraufruf, Unterbrechungen markiert dokumentieren, kein Fake-Erfolg.

## Ablauf
1. Ist-Repo und Projektentscheidungen prüfen; bei Bedarf einen read-only Audit voranstellen.
2. Eingaben, betroffene Dateien, unveränderliche Vorgaben und konkrete Erfolgsbedingungen nennen.
3. Nur zuständige Agenten/Adapter verwenden. Vor Side Effects konkrete Freigabe prüfen.
4. Tatsächliche Artefakte erzeugen/prüfen, Rohlog und Exitcodes nennen, keinen Erfolg erfinden.
5. Abschlussbericht mit Status, Dateien, Tests, Grenzen, Risiken und nächstem Schritt liefern.
