# Belege T-0775 – echte Claude-Abo-Läufe (Q2)

Erzeugt am 26.11.2026 mit `TRENDO_ECHT_CLAUDE=reports/T-0775 Trendo.App.Tests.exe -class Trendo.App.Tests.ClaudeEchtlauf_Ui` (Konsolenausgabe: `testlauf-mit-TRENDO_ECHT_CLAUDE.txt`; ohne Variable: `testlauf-ohne-variable.txt`).

Je Fall (`logo`, `brief`, `grundriss`, `code-ki`, `ki-fragen`): `*-rohlog.jsonl` (stream-json-Mitschrift von Claude Code), Stand vorher/nachher, `*-bericht.md`. Prüfsummen: `SHA256.txt`.

**Nachträgliche Kürzung (Gegenprüfung T-0775):** Die Rohlogs wurden nach dem Lauf **nur um Kontodaten gekürzt** – ohne neuen Claude-Lauf:
- alle Zeilen `rate_limit_event` (Kontingent und Zusatzkosten-Status des Abos) entfernt,
- die Felder `credential_expires_at`, `total_cost_usd`, `costBasis` und `costUSD` aus den Ergebniszeilen entfernt (diese Zeilen sind dadurch neu als JSON geschrieben, sonst unverändert),
- in den `*-bericht.md` die Angabe des Abo-Typs entfernt.

Alle anderen Zeilen sind unverändert. `SHA256.txt` ist danach neu erzeugt. Künftige Läufe filtern dasselbe schon beim Kopieren (`CopyRawLog`, `StripAccount` in `tests/Trendo.App.Tests/ClaudeEchtlaufUiTests.cs`).
Schon beim ursprünglichen Kopieren ersetzt bzw. gekürzt: Benutzername → `<user>`; Start- und Befehlszeile nur mit Technischem und Anzahlen (keine Liste der Erweiterungen, Befehle und Skills der Person).
