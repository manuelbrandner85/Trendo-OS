# Umstellung auf Claude Code

**Stand:** 2026-10-23 · **Entscheidung:** `docs/adr/0009-claude-code-als-einziger-entwicklungsagent.md` · **Audit:** `reports/claude/AUDIT_CLAUDE_MIGRATION_2026-10-23.md`

Trendo OS wird ab 2026-10-23 ausschließlich mit Claude Code entwickelt. Alle Regeln stehen in `CLAUDE.md`.

## Erledigt
| Schritt | Inhalt |
|---|---|
| M1 | Alle Fach-, Sicherheits-, QA- und Berichtsregeln der früheren `AGENTS.md` vollständig in `CLAUDE.md` übernommen; `AGENTS.md` entfernt |
| M2 | Rollen festgelegt (Manuel, 2026-10-23): Claude pusht nach grünen Tests (auch `main`/`--force`), Claude-QA reviewt, Merge und Release durch Claude nur nach OK pro PR/Release |
| M3 | `.gitignore` für lokale Claude-Daten; harte `permissions.deny` gegen Installationen, Registry, Datenträger, Neustart, `reset --hard`, `rebase`, `clean`; Rückfrage vor Merge und Release |
| Bereinigung | Rechtekonfiguration, Startskript, Prompt-/Setup-Dokumente, Paket-Prüfsummenlisten, agentenspezifische QA-Matrix und erledigte Auftragsdateien des Vorgängers entfernt; Agenten-Übergabe im Produkt (`AgentCenter.ExportClaudeCodeJob`) und Abhängigkeitsliste auf Claude Code umgestellt; Dokumente umbenannt bzw. umformuliert |

## Offen
| Punkt | Warum offen | Wer |
|---|---|---|
| Lockerung in `.claude/settings.json` und Hook für Push auf `main`/`--force` | Claude darf seine eigenen Schutzregeln nicht lockern | Manuel wendet `docs/claude/M2_RECHTE_PATCH.diff` an |
| `decisions.yaml`: Verbesserungen 42–97 eintragen, `source_sha256` des geänderten `docs/spec/MASTERPROMPT.md` erneuern | Register ändert nur Manuel bzw. ein freigegebener Auftrag | M4 |
| Optionale Cloud-KI „xAI“ in der Trendo-App (`MainWindow.axaml.cs`) | Produktfunktion für Endnutzer, kein Entwicklungswerkzeug | Entscheidung Manuel |

## Bleibt erhalten
Testnachweise in `reports/` und Einträge in `docs/build-log/` sind Belege für die Status in `requirements.yaml` und bleiben unverändert, auch wenn sie den früheren Agenten erwähnen.
