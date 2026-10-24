# Trendo OS – Claude Code Produktionspaket v2.0

**Erstellt:** 21.10.2026 | **Status:** APPROVED_SPEC – KEINE Trendo-OS-Software gebaut, installiert oder getestet.

**Verbindlicher Wechsel:** Claude Code = Projektleitung und lokaler Coding-/Test-Agent. Eine verpflichtende Abhängigkeit von einem anderen Coding-Assistenten existiert NICHT. **Trendo** ist der Name des späteren integrierten OS-Assistenten. Das Spiel **AION 2026** ist ein separates Projekt und wird NICHT umbenannt.

## Reihenfolge zum Übergeben
1. `SYSTEMBIBEL.md` – vollständiger bisheriger genehmigter Funktionsbestand und die aktualisierte Claude-Code-Architektur.
2. `docs/claude/MASTER_STARTPROMPT.md` – Claude Code die Planung als Projektleiter geben; zuerst NUR Planung, Inventur und Aufgaben, keine unkontrollierten lokalen Aktionen.
3. `CLAUDE.md` – Claude Code lokal im AUTORISIERTEN Trendo-OS-Arbeitsordner verwenden.
4. `CLAUDE.md` – verbindliche lokale Projektinstruktionen für Claude Code.
5. `EINFACH_STARTEN.md` – Schritt-für-Schritt-Prüfung lokaler Voraussetzungen.
6. `docs/claude/GODMODE_2_ARCHITEKTUR.md` – realistische Orchestrierung von Claude Code und seinen Fachagenten, Blender/Unreal-Adapter und Sicherheitsgrenzen.
7. `ROADMAP.md`, `ARCHITEKTUR.md`, `QA_ABNAHME.md`, `docs/claude/QA_RELEASE_MATRIX.md` – Entwicklungsreihenfolge und nicht vorgetäuschte Tests.
8. `MODULE_REGISTER.csv` – bestätigte und abgelehnte Studio-Upgrades.
9. `STUDIO_ONLINE_BUSINESS_30.md`, `BUSINESS_REQUIREMENTS_30.csv`, `BUSINESS_QA_MATRIX.csv`, `BUSINESS_CLAUDE_CODE_AUFTRAG.md` – Genehmigte 30 Funktionen und beide Business-Modi.
10. `STUDIO_LETTER_CORRESPONDENCE_25.md`, `LETTER_REQUIREMENTS_25.csv`, `LETTER_QA_MATRIX.csv`, `LETTER_CLAUDE_CODE_AUFTRAG.md` – Genehmigtes Brief-/Behörden-/Gerichtsstudio mit 25 Funktionen.
11. `SOFTWARE_BUNDLE_MANIFEST.csv`, `OFFENE_ENTSCHEIDUNGEN.md`, `docs/claude/MIGRATION_ZU_CLAUDE.md`, `.mcp.json` (Vorlage, keine realen MCP-Verbindungen).

## Harte technische Wahrheiten
- **Claude Code ist Cloud-basiert.** Bots im gleichen Konto teilen sich einen Cloud-Computer, Browserzustand und Dateien. Mehrere Bots bedeuten KEINE echte Projekt- oder Credential-Isolation.
- **Claude Code ist lokal installierbar** und kann bei vorhandenem Account/geeignetem Modell Programmier- und Testaufträge durchführen. Der Zugriff auf Blender/UE5 erfordert reale Installation/Integration und Tests.
- **Bot → lokaler PC:** Claude Code kann bei unterstützten Plattformen ausdrücklich genehmigte lokale Befehle anfordern. Eine dauerhaft unbeaufsichtigte Direktsteuerung von Claude Code ist damit NICHT automatisch gewährleistet; sichere manuelle Übergabe bleibt Fallback.
- Sensible Daten und insbesondere forensische Kopien NICHT in die Cloud-Dienste hochladen; fremder Cloud-Retention-/Löschstatus ist nicht vollständig unter lokaler Kontrolle.
- Claude Code kann MCP-Server aus `.mcp.json` laden; das Vorhandensein einer Konfigurationsdatei bedeutet nicht, dass echte Blender-/Unreal-MCP-Server eingerichtet sind.
- Keine Zusatzverpflichtung zu kostenpflichtigen KI-APIs im PRODUKT Trendo OS. Die Entwicklung mit Claude Code kann planabhängige Kosten haben. Keine Gratis-Zusage.

## Produktionszustände
`APPROVED_SPEC → PLANNED → IN_PROGRESS → IMPLEMENTED → TESTED → ACCEPTED`, mit `BLOCKED` / `FAILED`; reale Logs/Buildhashes statt fingierter Erfolge.

## Erster Arbeitsauftrag
Repository/Pfad und Claude Code-Zugang real verifizieren. Danach konkrete Architektur und den WINDOWS-VERTICAL-SLICE planen; vor Änderungen Diff/Plan vorlegen, keine Fremdprojekte oder Windows-Konfigurationen ändern.
