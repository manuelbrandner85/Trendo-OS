# AGENTS.md – Verbindliche Regeln für Grok Build im Trendo-OS-Projekt

## Projekt
Trendo OS (Desktop-/Studio-OS) mit KI-Assistent Trendo; Grok Bot Projektleitung, Grok Build lokaler Entwicklungsagent. Derzeit nur bestätigte Spezifikation, kein nachgewiesener Build.

## Repository-Layout
Spezifikation unter `docs/spec/` (Pfade in diesen Regeln beziehen sich dorthin), ADRs in `docs/adr/`, Aufträge in `tasks/`, Berichte/Logs in `reports/`.
**Öffentliches Repo:** keine Secrets, keine personenbezogenen Daten, keine Pfade mit Benutzernamen in Berichten. Nie direkt auf `main` pushen; jede Arbeit auf eigenem Branch + PR.
Nach Änderungen an Spezifikations-CSVs: `python scripts/generate_requirements.py && python scripts/validate_requirements.py`.

## Vor jeder Änderung
1. `SYSTEMBIBEL.md`, `MODULE_REGISTER.csv`, `GROK_BUILD_MASTERPROMPT.md`, `ROADMAP.md`, `QA_ABNAHME.md`, `GROK_ARCHITEKTUR_AGENTEN_MCP.md` und `OFFENE_ENTSCHEIDUNGEN.md` lesen.
2. Reales Zielrepository, Git-Status, OS, Buildtool, Sicherheitsfreigaben prüfen. Nie fremde Projekte ändern. Änderungen auf einem reversiblen Branch; signifikante Edits nach Planfreigabe.
3. Keine hardcodierten Secrets; keine Cloud-Exfiltration und keine forensischen Beweiskopien in Grok Bot Cloud.
4. Kritische externe Aktionen oder Systemänderungen: eindeutige aufgabenbezogene Nutzerfreigabe und überprüfbarer Auth-Mechanismus. Keine gefährlichen Realwelt-Aktoren.
5. Forensik-Kopien und Derivate max. 120h, 24h Warnung, KEINE dauerhaften Offline-USB-Kopien; kein Browser- / Tor-READ-ONLY-Bypass.
6. Technische Arbeit nur mit real vorhandenen Tools; keine erfundenen MCP-Verbindungen/UE5-/Blender-Resultate; sämtliche Testresultate ursprünglich NOT_RUN.
7. Explizit abgelehnte breite Language & Localization Studio 2.0-Erweiterung nicht implementieren.
8. Keine zusätzliche kostenpflichtige KI-API-Abhängigkeit IN Trendo OS voraussetzen.

## Verbindlicher Bericht
Scope/Änderung/Dateien/Tests/Logs/Pass-Fail-Blocked/Risiken/Freigabe/Nächster Schritt. `APPROVED_SPEC` ist kein `IMPLEMENTED`.
