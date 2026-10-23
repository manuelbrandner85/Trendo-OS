---
name: trendo-audit
description: Bestandsaudit für das bestehende Trendo-OS-Projekt. Bei passenden Aufträgen verwenden.
---
# Bestandsaudit

## Zweck
README, AGENTS.md, docs/spec, branches und tatsächliche Tests prüfen, ohne Dateien zu ändern. Gib Konfliktmatrix Alt-Grok vs. Claude und APPROVED_SPEC vs. IMPLEMENTED aus.

## Ablauf
1. Ist-Repo und Projektentscheidungen prüfen; bei Bedarf einen read-only Audit voranstellen.
2. Eingaben, betroffene Dateien, unveränderliche Vorgaben und konkrete Erfolgsbedingungen nennen.
3. Nur zuständige Agenten/Adapter verwenden. Vor Side Effects konkrete Freigabe prüfen.
4. Tatsächliche Artefakte erzeugen/prüfen, Rohlog und Exitcodes nennen, keinen Erfolg erfinden.
5. Abschlussbericht mit Status, Dateien, Tests, Grenzen, Risiken und nächstem Schritt liefern.
