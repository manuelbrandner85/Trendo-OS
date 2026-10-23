---
name: trendo-release
description: Release Gate für das bestehende Trendo-OS-Projekt. Bei passenden Aufträgen verwenden.
---
# Release Gate

## Zweck
Bewerte vollständiges tatsächliches Prüfprotokoll; kein Push, Merge, Installer-Release oder Veröffentlichung ohne Nutzerfreigabe. Reproduzierbare Builds und SHA256 dokumentieren.

## Ablauf
1. Ist-Repo und Projektentscheidungen prüfen; bei Bedarf einen read-only Audit voranstellen.
2. Eingaben, betroffene Dateien, unveränderliche Vorgaben und konkrete Erfolgsbedingungen nennen.
3. Nur zuständige Agenten/Adapter verwenden. Vor Side Effects konkrete Freigabe prüfen.
4. Tatsächliche Artefakte erzeugen/prüfen, Rohlog und Exitcodes nennen, keinen Erfolg erfinden.
5. Abschlussbericht mit Status, Dateien, Tests, Grenzen, Risiken und nächstem Schritt liefern.
