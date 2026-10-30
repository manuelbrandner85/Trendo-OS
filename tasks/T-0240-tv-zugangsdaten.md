# T-0240 – TV-Zugangsdaten in den sicheren Speicher

- Quelle: Audit 98–113 (`docs/claude/audit_98_113/AUDIT_BESTAND.md`, Befund 1), Verbesserung 98; Auftrag `TASK_ERSTE_UMSETZUNG.md`
- Freigabe: Nutzer im Chat am 30.10.2026 („Ja, T-0240“)
- Branch: `claude/T-0240-tv-zugangsdaten`
- Builder: 03-security-permissions · QA: 19-qa-verification

## Ziel
Xtream-Passwörter nie im Klartext in `tv.json`, Senderlisten, Film-/Serien-/Folgen-Dateien; Passwort im `ISecretStore` unter `tv:<quellen-id>`, Einsetzen erst beim Abruf; Umzug alter Einträge mit Sicherung; Entfernen löscht das Passwort; klare Meldung, wenn es fehlt.

## Abnahme
Positivtest (Anlegen/Laden/Abspielen), Umzugstest, Negativtest (Passwort fehlt → kein Abruf, Meldung), Entfernen, keine Passwörter in Audit/Dateien; Volltest aller drei Testprojekte + Paket-Tests.
