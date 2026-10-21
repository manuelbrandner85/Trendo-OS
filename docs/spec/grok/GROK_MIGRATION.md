# Wechsel vom bisherigen Coding-Agenten zu Grok Bot + Grok Build

**Freigabe:** Nutzer hat Wechsel am 21.10.2026 ausdrücklich bestätigt. Produkt Trendo OS, Assistent Trendo, Studio-Module und Sicherheitsregeln unverändert.

| Vorherige Rolle | Neue verbindliche Rolle | Übernahme |
|---|---|---|
| Coding-Agent im Terminal | Grok Build | Alle lokalen Coding-/Testing-/Scripting-Aufträge |
| Projektüberwachung | Grok Bot | Taskplanung, Status, Zuarbeit, jedoch Cloud-/Datenschutzgrenzen |
| MCP-Adapter | Grok Build MCP-Konfiguration | Server einzeln verifizieren; nicht nur umbenennen |
| Agententeam | Grok Subagents / Skills | Rollen nach Fähigkeiten, pro Projekt Rechte begrenzen |
| Automatisierte Jobs | `grok -p` / geeignete Workflow-Adapter | Nur nach Auth/ Freigabe; keinen unbeaufsichtigten Cloud→PC-Zugriff annehmen |
| Produktinterne KI | Trendo | Endprodukt unterstützt freie/offene geeignete lokale Modelle, keine zusätzliche bezahlte KI-API Pflicht |

## Dateimigration
`CLAUDE_CODE_MASTERPROMPT.md` → `GROK_BUILD_MASTERPROMPT.md`
`BUSINESS_CLAUDE_CODE_AUFTRAG.md` → `BUSINESS_GROK_BUILD_AUFTRAG.md`
`LETTER_CLAUDE_CODE_AUFTRAG.md` → `LETTER_GROK_BUILD_AUFTRAG.md`
`README_START_HERE.md`, `SYSTEMBIBEL.md`, `ARCHITEKTUR.md`, `ROADMAP.md`, `QA_ABNAHME.md`, `SOFTWARE_BUNDLE_MANIFEST.csv`, `OFFENE_ENTSCHEIDUNGEN.md` aktualisiert.

Alte Dateinamen werden im neuen Paket nicht benötigt; sie sind hier ausschließlich für die nachvollziehbare Migration genannt. **Der ältere Agent ist keine technische oder lizenzrechtliche Voraussetzung mehr.**

## Nicht angepasste Fakten
- Triple Mode / Alle Studio-APPROVED_SPEC / 25 Letter-Funktionen / 30 Online-Business-Funktionen.
- NICHT genehmigt: generelles Upgrade Language & Localization Studio 2.0 (Grundstudio bleibt).
- Keine autonomen externen Aktionen; keine gefährliche reale Maschinen-/Fahrzeugsteuerung; 120h Forensik inklusive abgeleiteter Dateien/Caches/Indizes; User approval.
- Noch keine Trendo-OS-Software gebaut/getestet.
