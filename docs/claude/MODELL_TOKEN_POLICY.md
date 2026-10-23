# Agentenmodell- und Token-Strategie

- Agenten verwenden als Richtlinie `sonnet` für die meisten Implementierungen; `opus` für komplexe Architektur, Sicherheits- und Kernfragen. tatsächliche Verfügbarkeit und Limits in Claude Code prüfen, keine Kostenversprechen.
- Kein Agententeam für einfache Tasks. Standard 1 Hauptagent + 1 Fachagent oder 1 Review-Agent; bei komplexen abhängigen Änderungen 2–3 parallel, keine doppelten Arbeitsbereiche.
- Lange Logs über eigene Werkzeuge in Datei schreiben, Ergebnis nur als Pfad + zusammengefasste Fehlerdaten; nicht die ganze Codebasis in Prompts kopieren.
- Kontext nur Delta zu genehmigten Spezifikationen; Quelle und Versionsstand jeder Entscheidung angeben. `/compact` bei langen Sitzungen, `CLAUDE.md` kurz halten.
- Keine externen bezahlten APIs automatisch aktivieren; Subagenten teilen die Nutzungslimits des Claude-Kontos.
